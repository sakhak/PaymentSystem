using System.ComponentModel.DataAnnotations;

namespace PaymentSystem.DTOs
{
    public class CheckPaymentRequest
    {
        [Required]
        [RegularExpression(
            "^[a-fA-F0-9]{32}$", ErrorMessage = "MD5 must contain 32 hexadecimal characters.")]
        public string Md5 { get; set; } = string.Empty;
    }
}
