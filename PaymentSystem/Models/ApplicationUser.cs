using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PaymentSystem.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(80)]
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
