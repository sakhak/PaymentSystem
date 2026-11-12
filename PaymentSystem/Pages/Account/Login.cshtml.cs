using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Models;

namespace PaymentSystem.Pages.Account;
[AllowAnonymous]
public class LoginModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public class LoginInput
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    }
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await users.Users.AnyAsync()) return RedirectToPage("/Account/Setup");
        return User.Identity?.IsAuthenticated == true ? RedirectToPage("/Index") : Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await users.FindByEmailAsync(Input.Email.Trim());
        var result = user is { IsActive: true }
            ? await signIn.PasswordSignInAsync(user, Input.Password, false, lockoutOnFailure: true)
            : Microsoft.AspNetCore.Identity.SignInResult.Failed;
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Sign-in failed. Check your details or try again later if the account is locked.");
            return Page();
        }
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Page("/Index")!);
    }
}
