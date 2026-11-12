using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Data;
using PaymentSystem.Models;

namespace PaymentSystem.Pages.Admin.Products;
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<Product> Products { get; set; } = [];
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [TempData] public string? Notice { get; set; }
    public async Task OnGetAsync()
    {
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Search)) query = query.Where(p => p.Name.Contains(Search));
        Products = await query.OrderByDescending(p => p.Id).Take(100).ToListAsync();
    }
    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        product.IsActive = !product.IsActive;
        await db.SaveChangesAsync();
        Notice = product.IsActive ? "Product published." : "Product archived. Existing orders are preserved.";
        return RedirectToPage();
    }
}
