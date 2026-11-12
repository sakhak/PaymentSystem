using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PaymentSystem.Data;
using PaymentSystem.Models;
using PaymentSystem.Services;

namespace PaymentSystem.Pages.Admin.Products;
public class EditModel(ApplicationDbContext db) : PageModel
{
    [BindProperty] public Product Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null) return Page();
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        Input = product;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (!PaymentService.ValidAmount(Input.Price, Input.Currency))
            ModelState.AddModelError("Input.Price", "USD allows two decimals. KHR requires whole riel. The price must be positive.");
        if (!ModelState.IsValid) return Page();
        var product = id.HasValue ? await db.Products.FindAsync(id) : new Product();
        if (product is null) return NotFound();
        product.Name = Input.Name.Trim(); product.Description = Input.Description.Trim();
        product.Price = Input.Price; product.Currency = Input.Currency; product.IsActive = Input.IsActive;
        if (!id.HasValue) db.Products.Add(product);
        await db.SaveChangesAsync();
        TempData["Notice"] = "Product saved.";
        return RedirectToPage("Index");
    }
}
