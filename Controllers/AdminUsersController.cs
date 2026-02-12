using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Identity;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin")]
public class AdminUsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public AdminUsersController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null)
    {
        var usersQuery = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            usersQuery = usersQuery.Where(u =>
                (u.Email != null && u.Email.Contains(q)) ||
                (u.UserName != null && u.UserName.Contains(q)));
        }

        var users = await usersQuery.OrderBy(u => u.Email).Take(200).ToListAsync();

        var roles = await _roleManager.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync();

        var rows = new List<UserRowVm>(users.Count);
        foreach (var u in users)
        {
            var fullUser = await _userManager.FindByIdAsync(u.Id.ToString());
            var userRoles = fullUser is null ? new List<string>() : (await _userManager.GetRolesAsync(fullUser)).ToList();

            rows.Add(new UserRowVm
            {
                UserId = u.Id,
                Email = u.Email ?? u.UserName ?? "(no email)",
                Roles = userRoles
            });
        }

        return View(new IndexVm { Query = q, Roles = roles, Users = rows });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(Guid userId, string role)
    {
        role = (role ?? "").Trim();
        if (string.IsNullOrWhiteSpace(role))
        {
            TempData["Err"] = "Role is required.";
            return RedirectToAction(nameof(Index));
        }

        if (!await _roleManager.RoleExistsAsync(role))
        {
            TempData["Err"] = "Role does not exist.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            TempData["Err"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        // Simple ERP: one primary role per user
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

        var add = await _userManager.AddToRoleAsync(user, role);
        if (!add.Succeeded)
        {
            TempData["Err"] = string.Join("; ", add.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["Ok"] = $"Role updated: {user.Email} → {role}";
        return RedirectToAction(nameof(Index));
    }

    public sealed class IndexVm
    {
        public string? Query { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<UserRowVm> Users { get; set; } = new();
    }

    public sealed class UserRowVm
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = "";
        public List<string> Roles { get; set; } = new();
    }
}