using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Models;
using PaymentSystem.Services;
using QRCoder;

namespace PaymentSystem.Pages.Payments;
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DetailsModel(PaymentService payments, IConfiguration configuration) : PageModel
{
    public Payment Payment { get; set; } = null!;
    public string Status => PaymentService.Status(Payment);
    public string? QrImage { get; set; }
    [TempData] public string? Notice { get; set; }
    public bool IsTestEnvironment => configuration["Bakong:BaseUrl"]?.Contains("sit-", StringComparison.OrdinalIgnoreCase) == true;
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var payment = await payments.VisibleTo(User).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
        if (payment is null) return NotFound();
        Payment = payment;
        if (Status == "Pending" && !string.IsNullOrWhiteSpace(payment.Qr))
            QrImage = "data:image/png;base64," + Convert.ToBase64String(PngByteQRCodeHelper.GetQRCode(payment.Qr, QRCodeGenerator.ECCLevel.M, 8));
        return Page();
    }
    public async Task<IActionResult> OnPostVerifyAsync(int id, CancellationToken ct)
    {
        var payment = await payments.VisibleTo(User).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (payment is null) return NotFound();
        var message = await payments.VerifyAsync(payment, ct);
        if (Request.Headers.Accept.ToString().Contains("application/json"))
            return await SnapshotAsync(id, ct);
        Notice = message;
        return RedirectToPage(new { id });
    }

    public Task<IActionResult> OnGetStatusAsync(int id, CancellationToken ct) => SnapshotAsync(id, ct);

    private async Task<IActionResult> SnapshotAsync(int id, CancellationToken ct)
    {
        var payment = await payments.VisibleTo(User).AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (payment is null) return NotFound();
        static string? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToString("O") : null;
        return new JsonResult(new
        {
            payment.Id, Status = PaymentService.Status(payment), payment.VerificationCode,
            Message = payment.VerificationMessage ?? "Waiting for the first automatic bank check.",
            LastCheckedAt = Utc(payment.LastCheckedAt), PaidAt = Utc(payment.PaidAt),
            ExpiresAt = Utc(payment.ExpiresAt), payment.TransactionHash,
            AutoChecking = configuration.GetValue("PaymentVerification:Enabled", true) &&
                payment.CreatedAt >= DateTime.UtcNow.AddDays(-1) && !string.IsNullOrEmpty(payment.ReceiverAccountId),
            ServerTime = DateTime.UtcNow.ToString("O")
        });
    }
}
