
namespace PaymentSystem.Models;

public class Payment
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public int? ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; } = 1;
    public string? ReceiverAccountId { get; set; }
    public string? TransactionHash { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public DateTime? NextVerificationAt { get; set; }
    public string? VerificationCode { get; set; }
    public string? VerificationMessage { get; set; }

    public string OrderId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Qr { get; set; } = string.Empty;

    public string? Md5 { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }
}
