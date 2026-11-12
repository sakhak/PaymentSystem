using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PaymentSystem.Models;

namespace PaymentSystem.Pages.Account;
public class ManageModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : PageModel
{
    [BindProperty] public ProfileInput Input { get; set; } = new();
    public string Email { get; set; } = "";
    [TempData] public string? Notice { get; set; }
    public class ProfileInput
    {
        [Required, StringLength(80)] public string DisplayName { get; set; } = "";
        [DataType(DataType.Password)] public string? CurrentPassword { get; set; }
        [StringLength(100, MinimumLength = 10), DataType(DataType.Password)] public string? NewPassword { get; set; }
        [Compare(nameof(NewPassword)), DataType(DataType.Password)] public string? ConfirmPassword { get; set; }
    }
    public async Task<IActionResult> OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        Input.DisplayName = user.DisplayName; Email = user.Email!;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        Email = user.Email!;
        if (!ModelState.IsValid) return Page();
        if (!string.IsNullOrEmpty(Input.NewPassword))
        {
            var password = await users.ChangePasswordAsync(user, Input.CurrentPassword ?? "", Input.NewPassword);
            if (!password.Succeeded)
            {
                foreach (var error in password.Errors) ModelState.AddModelError("", error.Description);
                return Page();
            }
        }
        user.DisplayName = Input.DisplayName.Trim();
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) { ModelState.AddModelError("", "Profile could not be saved."); return Page(); }
        await signIn.RefreshSignInAsync(user);
        Notice = "Your profile has been updated.";
        return RedirectToPage();
    }
}
