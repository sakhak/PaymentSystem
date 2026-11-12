using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Models;
using PaymentSystem.Services;

namespace PaymentSystem.Pages.Payments;
public class IndexModel(PaymentService payments) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public bool IssuesOnly { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; }
    public List<Payment> Payments { get; set; } = [];
    public async Task OnGetAsync()
    {
        var query = payments.VisibleTo(User).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Search)) query = query.Where(p => p.OrderId.Contains(Search));
        if (IssuesOnly) query = query.Where(p => p.Status != "Paid" && p.VerificationCode != null &&
            p.VerificationCode != "NotFound" && p.VerificationCode != "Checking");
        var now = DateTime.UtcNow;
        query = Status switch
        {
            "Paid" => query.Where(p => p.Status == "Paid"),
            "Pending" => query.Where(p => p.Status == "Pending" && p.ExpiresAt > now),
            "Expired" => query.Where(p => p.Status == "Pending" && p.ExpiresAt <= now), _ => query
        };
        TotalPages = Math.Max(1, (int)Math.Ceiling(await query.CountAsync() / 20d));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
        Payments = await query.OrderByDescending(p => p.Id).Skip((PageNumber - 1) * 20).Take(20).ToListAsync();
    }

    public async Task<IActionResult> OnGetLiveAsync()
    {
        Response.Headers.CacheControl = "no-store";
        await OnGetAsync();
        return new JsonResult(new { Payments = Payments.Select(p => new { p.Id, Status = PaymentService.Status(p) }) });
    }
}
