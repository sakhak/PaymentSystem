using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Data;
using PaymentSystem.Models;

namespace PaymentSystem.Services;

public class PaymentService(ApplicationDbContext db, KhqrService khqr, BakongService bakong,
    IConfiguration config, ILogger<PaymentService> logger)
{
    public IQueryable<Payment> VisibleTo(ClaimsPrincipal user) => user.IsInRole("Admin")
        ? db.Payments : db.Payments.Where(p => p.UserId == user.FindFirstValue(ClaimTypes.NameIdentifier));

    public async Task<Payment> CreateAsync(string userId, string orderId, decimal amount, string currency,
        int? productId = null, int quantity = 1, CancellationToken ct = default)
    {
        orderId = orderId.Trim();
        if (orderId.Length is < 1 or > 50 || quantity is < 1 or > 100)
            throw new PaymentException("Enter an order ID of up to 50 characters and quantity between 1 and 100.");
        Product? product = null;
        if (productId.HasValue)
        {
            product = await db.Products.SingleOrDefaultAsync(p => p.Id == productId && p.IsActive, ct)
                ?? throw new PaymentException("This product is no longer available.");
            amount = product.Price * quantity;
            currency = product.Currency;
        }
        if (!ValidAmount(amount, currency))
            throw new PaymentException("Use a positive amount up to 99,999,999. USD supports two decimal places; KHR uses whole riel.");
        if (await db.Payments.AnyAsync(p => p.OrderId == orderId, ct))
            throw new PaymentException("That order ID is already in use. Please choose another.");
        var generated = khqr.GenerateIndividual(amount, currency, orderId);
        var payment = new Payment
        {
            UserId = userId, OrderId = orderId, Amount = amount, Currency = currency,
            ProductId = product?.Id, ProductName = product?.Name, Quantity = quantity,
            ReceiverAccountId = config["Khqr:BakongAccountId"], Qr = generated.Qr, Md5 = generated.Md5,
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        db.Payments.Add(payment);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Payment creation rejected by database constraints.");
            throw new PaymentException("Payment could not be saved. The order ID may already exist.");
        }
        return payment;
    }

    public static bool ValidAmount(decimal amount, string currency) =>
        amount > 0 && amount <= 99999999 &&
        (currency == "USD" && decimal.Round(amount, 2) == amount || currency == "KHR" && decimal.Truncate(amount) == amount);

    public static string Status(Payment payment) => payment.Status == "Pending" && payment.ExpiresAt <= DateTime.UtcNow
        ? "Expired" : payment.Status;

    public async Task<string> VerifyAsync(Payment payment, CancellationToken ct = default)
    {
        if (payment.Status == "Paid") return "This payment has already been verified.";
        var now = DateTime.UtcNow;
        // A database lease prevents the worker, manual button, and multiple tabs
        // from issuing concurrent provider checks for the same payment.
        var claimed = await db.Payments.Where(p => p.Id == payment.Id && p.Status != "Paid" &&
                (p.NextVerificationAt == null || p.NextVerificationAt <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.NextVerificationAt, now.AddSeconds(30))
                .SetProperty(p => p.VerificationCode, "Checking")
                .SetProperty(p => p.VerificationMessage, "Checking payment with Bakong."), ct);
        if (claimed == 0)
        {
            var latest = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == payment.Id, ct);
            return latest.Status == "Paid" ? "This payment has already been verified."
                : latest.VerificationMessage ?? "A payment check is already in progress.";
        }
        VerificationAssessment assessment;
        try
        {
            assessment = string.IsNullOrWhiteSpace(payment.Md5) || string.IsNullOrWhiteSpace(payment.ReceiverAccountId)
                ? new("LegacyPayment", "This older order has no receiving account snapshot and cannot be automatically confirmed.")
                : AssessTransaction(await bakong.CheckPaymentAsync(payment.Md5, ct), payment);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { assessment = new("Timeout", "Bakong did not respond in time. Automatic checks will retry."); }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        { assessment = new("CredentialRejected", "Bakong rejected the API credential. An administrator must check the token and payment environment."); }
        catch (InvalidOperationException)
        { assessment = new("ConfigurationRequired", "Payment verification is not configured. Contact the administrator."); }
        catch (JsonException)
        { assessment = new("InvalidResponse", "Bakong returned an unreadable response. Payment has not been confirmed."); }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Unable to contact the configured Bakong provider for payment {Id}.", payment.Id);
            assessment = new("ProviderUnavailable", "Cannot reach Bakong right now. Automatic checks will retry.");
        }

        var checkedAt = DateTime.UtcNow;
        if (assessment.Hash is not null)
        {
            try
            {
                await db.Payments.Where(p => p.Id == payment.Id && p.Status != "Paid")
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "Paid")
                        .SetProperty(p => p.TransactionHash, assessment.Hash).SetProperty(p => p.PaidAt, checkedAt)
                        .SetProperty(p => p.LastCheckedAt, checkedAt).SetProperty(p => p.NextVerificationAt, (DateTime?)null)
                        .SetProperty(p => p.VerificationCode, "Verified")
                        .SetProperty(p => p.VerificationMessage, assessment.Message), ct);
                logger.LogInformation("Payment {Id} confirmed by Bakong.", payment.Id);
                return assessment.Message;
            }
            catch (Exception ex) when (ex is DbUpdateException || ex is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 })
            { assessment = new("TransactionReused", "This bank transaction has already confirmed another order. Contact the administrator."); }
        }

        var interval = Math.Clamp(config.GetValue("PaymentVerification:IntervalSeconds", 5), 1, 60);
        var delay = assessment.Code == "NotFound" && payment.ExpiresAt > checkedAt ? interval
            : assessment.Code == "NotFound" ? 30 : 60;
        await db.Payments.Where(p => p.Id == payment.Id && p.Status != "Paid")
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastCheckedAt, checkedAt)
                .SetProperty(p => p.NextVerificationAt, checkedAt.AddSeconds(delay))
                .SetProperty(p => p.VerificationCode, assessment.Code)
                .SetProperty(p => p.VerificationMessage, assessment.Message), ct);
        if (assessment.Code != "NotFound")
            logger.LogWarning("Payment {Id} verification result: {Code}.", payment.Id, assessment.Code);
        return assessment.Message;
    }

    public static string? MatchTransaction(JsonElement result, Payment payment)
        => AssessTransaction(result, payment).Hash;

    public static VerificationAssessment AssessTransaction(JsonElement result, Payment payment)
    {
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("responseCode", out var code) || code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var number))
            return new("InvalidResponse", "Bakong returned an unexpected response. Payment has not been confirmed.");
        if (number != 0)
        {
            var error = result.TryGetProperty("errorCode", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var errorCode)
                ? errorCode : 0;
            return error switch
            {
                1 => new("NotFound", "No successful transaction was found in the configured Bakong environment yet. Checking will continue."),
                2 => new("UnsupportedQr", "Bakong cannot verify this QR type. Contact the administrator."),
                3 => new("BankRejected", "Bakong reports that the bank transaction failed. Payment has not been confirmed."),
                6 => new("CredentialRejected", "Bakong rejected the API credential. An administrator must check the token and payment environment."),
                9 => new("ProviderUnavailable", "Bakong is temporarily unavailable. Automatic checks will retry."),
                11 or 12 => new("ReceiverInvalid", "Bakong could not validate the receiving account. Contact the administrator."),
                _ => new("ProviderRejected", "Bakong could not complete verification. An administrator should check the integration.")
            };
        }
        if (!result.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return new("InvalidResponse", "Bakong returned no transaction details. Payment has not been confirmed.");
        static string? Text(JsonElement data, string key) => data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (Text(data, "toAccountId") != payment.ReceiverAccountId)
            return new("ReceiverMismatch", "A transaction was found, but the receiving account does not match this order.");
        if (Text(data, "currency") != payment.Currency)
            return new("CurrencyMismatch", "A transaction was found, but its currency does not match this order.");
        if (!data.TryGetProperty("amount", out var amount) ||
            !decimal.TryParse(amount.ToString(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var paid) || paid != payment.Amount)
            return new("AmountMismatch", "A transaction was found, but its amount does not match this order.");
        if (!data.TryGetProperty("createDateMs", out var created) || created.ValueKind != JsonValueKind.Number || !created.TryGetInt64(out var timestamp))
            return new("InvalidResponse", "The transaction has no valid payment time and cannot be confirmed.");
        var start = new DateTimeOffset(DateTime.SpecifyKind(payment.CreatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var end = new DateTimeOffset(DateTime.SpecifyKind(payment.ExpiresAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var hash = Text(data, "hash");
        if (timestamp < start - 60000 || timestamp > end)
            return new("TimeMismatch", "The bank transaction time is outside this order's payment window. Contact the administrator for reconciliation.");
        return hash is { Length: 64 } && hash.All(Uri.IsHexDigit)
            ? new("Verified", "Payment received and verified by Bakong.", hash.ToLowerInvariant())
            : new("InvalidResponse", "The transaction has no valid bank reference and cannot be confirmed.");
    }
}

public class PaymentException(string message) : Exception(message);
public sealed record VerificationAssessment(string Code, string Message, string? Hash = null);
