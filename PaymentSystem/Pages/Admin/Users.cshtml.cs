using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PaymentSystem.Data;
using PaymentSystem.Models;

namespace PaymentSystem.Pages.Admin;
public class UsersModel(ApplicationDbContext db, UserManager<ApplicationUser> manager) : PageModel
{
    public List<ApplicationUser> Users { get; set; } = [];
    public HashSet<string> AdminIds { get; set; } = [];
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [TempData] public string? Notice { get; set; }
    public async Task OnGetAsync()
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Search)) query = query.Where(u => u.Email!.Contains(Search) || u.DisplayName.Contains(Search));
        Users = await query.OrderByDescending(u => u.CreatedAt).Take(100).ToListAsync();
        AdminIds = (await manager.GetUsersInRoleAsync("Admin")).Select(u => u.Id).ToHashSet();
    }
    public async Task<IActionResult> OnPostAsync(string id, string role, bool isActive)
    {
        if (id == User.FindFirstValue(ClaimTypes.NameIdentifier))
        { Notice = "You cannot change your own access here."; return RedirectToPage(); }
        if (role is not ("Admin" or "User")) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await manager.FindByIdAsync(id);
        if (user is null) return NotFound();
        var admins = await manager.GetUsersInRoleAsync("Admin");
        if (admins.Any(u => u.Id == id) && (role != "Admin" || !isActive) && admins.Count(u => u.IsActive) <= 1)
        { Notice = "The last active administrator must remain enabled."; return RedirectToPage(); }
        var oldRoles = await manager.GetRolesAsync(user);
        var result = await manager.RemoveFromRolesAsync(user, oldRoles);
        if (result.Succeeded) result = await manager.AddToRoleAsync(user, role);
        user.IsActive = isActive;
        if (result.Succeeded) result = await manager.UpdateAsync(user);
        if (result.Succeeded) result = await manager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) { Notice = "Access could not be updated."; return RedirectToPage(); }
        await transaction.CommitAsync();
        Notice = "User access updated. Existing sessions will be signed out.";
        return RedirectToPage();
    }
}
