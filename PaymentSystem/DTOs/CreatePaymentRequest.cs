using System.ComponentModel.DataAnnotations;

namespace PaymentSystem.DTOs
{
    public class CreatePaymentRequest
    {
        [Required]
        public string OrderId { get; set; } = string.Empty;
        [Range(0.01, 1000000)]
        public decimal Amount { get; set; }
        [Required]
        public string Currency { get; set; } = "USD";
    }
}
