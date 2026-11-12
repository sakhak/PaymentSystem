using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Data;
using PaymentSystem.Models;

namespace PaymentSystem.Pages.Products;
public class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public List<Product> Products { get; set; } = [];
    public int TotalPages { get; set; }
    public async Task OnGetAsync()
    {
        var query = db.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(Search)) query = query.Where(p => p.Name.Contains(Search) || p.Description.Contains(Search));
        TotalPages = Math.Max(1, (int)Math.Ceiling(await query.CountAsync() / 12d));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
        Products = await query.OrderBy(p => p.Name).Skip((PageNumber - 1) * 12).Take(12).ToListAsync();
    }
}
