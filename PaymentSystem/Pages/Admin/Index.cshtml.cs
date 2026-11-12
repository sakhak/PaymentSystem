using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Data;

namespace PaymentSystem.Pages.Admin;
public class IndexModel(ApplicationDbContext db, IConfiguration config) : PageModel
{
    public int UserCount { get; set; }
    public int ProductCount { get; set; }
    public int PaymentCount { get; set; }
    public int VerificationIssues { get; set; }
    public string ProviderHost => Uri.TryCreate(config["Bakong:BaseUrl"], UriKind.Absolute, out var uri) ? uri.Host : "Not configured";
    public bool IsTestEnvironment => ProviderHost.Contains("sit-", StringComparison.OrdinalIgnoreCase);
    public bool AutomaticChecks => config.GetValue("PaymentVerification:Enabled", true);
    public bool KhqrConfigured => !string.IsNullOrWhiteSpace(config["Khqr:BakongAccountId"]) && !string.IsNullOrWhiteSpace(config["Khqr:MerchantName"]);
    public bool VerificationConfigured => !string.IsNullOrWhiteSpace(config["Bakong:Token"]);
    public async Task OnGetAsync()
    {
        UserCount = await db.Users.CountAsync();
        ProductCount = await db.Products.CountAsync();
        PaymentCount = await db.Payments.CountAsync();
        VerificationIssues = await db.Payments.CountAsync(p => p.Status != "Paid" && p.VerificationCode != null &&
            p.VerificationCode != "NotFound" && p.VerificationCode != "Checking");
    }
}
