using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Data;
using PaymentSystem.Models;
using PaymentSystem.Services;

namespace PaymentSystem.Pages.Payments;
public class CreateModel(ApplicationDbContext db, PaymentService payments, ILogger<CreateModel> logger, IConfiguration config) : PageModel
{
    [BindProperty] public PaymentInput Input { get; set; } = new();
    public Product? Product { get; set; }
    public bool IsTestEnvironment => config["Bakong:BaseUrl"]?.Contains("sit-", StringComparison.OrdinalIgnoreCase) == true;
    public class PaymentInput
    {
        [Required, StringLength(50)] public string OrderId { get; set; } = "ORD-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public int? ProductId { get; set; }
        [Range(1, 100)] public int Quantity { get; set; } = 1;
    }
    public async Task<IActionResult> OnGetAsync(int? productId)
    {
        if (productId.HasValue)
        {
            Product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId && p.IsActive);
            if (Product is null) return NotFound();
            Input.ProductId = Product.Id; Input.Amount = Product.Price; Input.Currency = Product.Currency;
        }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Input.ProductId.HasValue)
        {
            Product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == Input.ProductId && p.IsActive, ct);
            if (Product is null) return NotFound();
        }
        if (!ModelState.IsValid) return Page();
        try
        {
            var payment = await payments.CreateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, Input.OrderId,
                Input.Amount, Input.Currency, Input.ProductId, Input.Quantity, ct);
            return RedirectToPage("Details", new { id = payment.Id });
        }
        catch (PaymentException ex) { ModelState.AddModelError("", ex.Message); }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "KHQR generation failed.");
            ModelState.AddModelError("", "KHQR generation is unavailable. Ask the administrator to check the receiving account configuration.");
        }
        return Page();
    }
}
