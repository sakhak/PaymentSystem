using Microsoft.EntityFrameworkCore;
using PaymentSystem.Data;

namespace PaymentSystem.Services;

/// <summary>Reconciles recent pending orders even when no payment page is open.</summary>
public sealed class PaymentVerificationWorker(IServiceScopeFactory scopes,
    IConfiguration configuration, ILogger<PaymentVerificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("PaymentVerification:Enabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await ReconcileAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "Automatic payment reconciliation failed; it will retry."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        List<int> ids;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-1);
            ids = await db.Payments.AsNoTracking()
                .Where(p => p.Status == "Pending" && p.CreatedAt >= cutoff && p.Md5 != null &&
                    p.ReceiverAccountId != null && (p.NextVerificationAt == null || p.NextVerificationAt <= now))
                .OrderBy(p => p.NextVerificationAt).ThenByDescending(p => p.Id)
                .Select(p => p.Id).Take(20).ToListAsync(ct);
        }
        await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (id, token) =>
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, token);
                if (payment is not null)
                    await scope.ServiceProvider.GetRequiredService<PaymentService>().VerifyAsync(payment, token);
            });
    }
}
