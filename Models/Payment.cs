using Microsoft.AspNetCore.Http.HttpResults;

namespace PaymentSystem.API.Models;

public class Payment
{
    public int Id {get; set;}
	public string OrderId { get; set; } = "";
	public decimal Amount { get; set; }
	public string Currency { get; set; } = "USD";
	public string Status { get; set; } = "Pending";
	public string? TransactionHash { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public string? KhqrMd5 { get; set; }
}