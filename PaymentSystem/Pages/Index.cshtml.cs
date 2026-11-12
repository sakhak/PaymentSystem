using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Models;
using PaymentSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace PaymentSystem.Pages;
public class IndexModel(PaymentService payments) : PageModel
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Paid { get; set; }
    public int Expired { get; set; }
    public List<Payment> Recent { get; set; } = [];
    public Dictionary<string, decimal> Revenue { get; set; } = [];
    public async Task OnGetAsync()
    {
        var query = payments.VisibleTo(User).AsNoTracking();
        var now = DateTime.UtcNow;
        Total = await query.CountAsync();
        Pending = await query.CountAsync(p => p.Status == "Pending" && p.ExpiresAt > now);
        Paid = await query.CountAsync(p => p.Status == "Paid");
        Expired = await query.CountAsync(p => p.Status == "Pending" && p.ExpiresAt <= now);
        Recent = await query.OrderByDescending(p => p.Id).Take(6).ToListAsync();
        var paid = await query.Where(p => p.Status == "Paid").Select(p => new { p.Currency, p.Amount }).ToListAsync();
        Revenue = paid.GroupBy(p => p.Currency).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
    }

    public async Task<IActionResult> OnGetLiveAsync()
    {
        Response.Headers.CacheControl = "no-store";
        await OnGetAsync();
        return new JsonResult(new { Total, Pending, Paid, Expired, Revenue,
            Payments = Recent.Select(p => new { p.Id, Status = PaymentService.Status(p) }) });
    }
}
