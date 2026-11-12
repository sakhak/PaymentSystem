using System.ComponentModel.DataAnnotations;

namespace PaymentSystem.Models;

public class Product
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string Description { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "9999999")] public decimal Price { get; set; }
    [RegularExpression("^(USD|KHR)$")] public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
