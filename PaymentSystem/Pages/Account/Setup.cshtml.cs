using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Data;
using PaymentSystem.Models;

namespace PaymentSystem.Pages.Account;

[AllowAnonymous]
public class SetupModel(ApplicationDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, IWebHostEnvironment environment) : PageModel
{
    [BindProperty] public SetupInput Input { get; set; } = new();
    public class SetupInput
    {
        [Required, StringLength(80)] public string DisplayName { get; set; } = "";
        [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
        [Required, StringLength(100, MinimumLength = 10), DataType(DataType.Password)] public string Password { get; set; } = "";
        [Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = "";
    }
    private bool IsLocalDevelopment => environment.IsDevelopment() && HttpContext.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);
    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsLocalDevelopment) return NotFound();
        return await users.Users.AnyAsync() ? RedirectToPage("/Account/Login") : Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsLocalDevelopment) return NotFound();
        if (!ModelState.IsValid) return Page();
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await users.Users.AnyAsync()) return RedirectToPage("/Account/Login");
        var user = new ApplicationUser { UserName = Input.Email.Trim(), Email = Input.Email.Trim(), DisplayName = Input.DisplayName.Trim() };
        var result = await users.CreateAsync(user, Input.Password);
        if (result.Succeeded) result = await users.AddToRoleAsync(user, "Admin");
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return Page();
        }
        await transaction.CommitAsync();
        await signIn.SignInAsync(user, false);
        return RedirectToPage("/Index");
    }
}
