
using System.ComponentModel.DataAnnotations;

namespace PaymentSystem.DTOs;

public class CreateKhqrRequest
{
    [Required]
    [StringLength(50)]
    public string OrderId { get; set; } = string.Empty;

    [Required]
    [Range(typeof(decimal), "0.01", "99999999")]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression("^(USD|KHR)$")]
    public string Currency { get; set; } = "KHR";
}
