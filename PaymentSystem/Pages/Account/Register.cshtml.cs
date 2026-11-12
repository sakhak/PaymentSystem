using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Models;

namespace PaymentSystem.Pages.Account;
[AllowAnonymous]
public class RegisterModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : PageModel
{
    [BindProperty] public SetupModel.SetupInput Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync() => !await users.Users.AnyAsync() ? RedirectToPage("Setup") : Page();
    public async Task<IActionResult> OnPostAsync()
    {
        if (!await users.Users.AnyAsync()) return RedirectToPage("Setup");
        if (!ModelState.IsValid) return Page();
        var user = new ApplicationUser { UserName = Input.Email.Trim(), Email = Input.Email.Trim(), DisplayName = Input.DisplayName.Trim() };
        var result = await users.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            // New accounts always get the User role. No submitted role is trusted.
            result = await users.AddToRoleAsync(user, "User");
            if (!result.Succeeded) await users.DeleteAsync(user);
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Could not create an account. Use a different email and a password meeting the requirements.");
            return Page();
        }
        await signIn.SignInAsync(user, false);
        return RedirectToPage("/Products/Index");
    }
}
