using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Identity;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminUsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _db;

    public AdminUsersController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
    }

    private Guid? GetCompanyId()
    {
        var raw = User.FindFirstValue("CompanyId");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, string? role = null, bool? active = null, string? sort = null, string? dir = null, int page = 1, int pageSize = 5)
    {
        q = (q ?? "").Trim();
        role = (role ?? "").Trim();
        sort ??= "email";
        dir ??= "asc";
        if (page < 1) page = 1;
        if (pageSize is < 5 or > 200) pageSize = 5;

        var usersQuery = _userManager.Users.AsNoTracking().AsQueryable();

        // ── Multi-tenant: Admin sees only their company's users ──
        var companyId = GetCompanyId();
        if (!IsSuperAdmin() && companyId.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.CompanyId == companyId.Value);
        }

        // Non-SuperAdmin users must not see SuperAdmin accounts
        if (!IsSuperAdmin())
        {
            var superAdminRoleId = await _db.Roles.AsNoTracking()
                .Where(r => r.Name == "SuperAdmin")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();
            if (superAdminRoleId != Guid.Empty)
                usersQuery = usersQuery.Where(u => !_db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == superAdminRoleId));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            usersQuery = usersQuery.Where(u =>
                (u.Email != null && u.Email.Contains(q)) ||
                (u.UserName != null && u.UserName.Contains(q)) ||
                (u.DisplayName != null && u.DisplayName.Contains(q)));
        }

        if (active.HasValue)
            usersQuery = usersQuery.Where(u => u.IsActive == active.Value);

        Guid? roleId = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            roleId = await _db.Roles.AsNoTracking()
                .Where(r => r.Name == role)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync();

            if (roleId is not null)
            {
                usersQuery = usersQuery.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
            }
        }

        var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        usersQuery = sort?.ToLowerInvariant() switch
        {
            "name" => ascending
                ? usersQuery.OrderBy(u => u.DisplayName).ThenBy(u => u.Email)
                : usersQuery.OrderByDescending(u => u.DisplayName).ThenByDescending(u => u.Email),
            _ => ascending
                ? usersQuery.OrderBy(u => u.Email)
                : usersQuery.OrderByDescending(u => u.Email)
        };

        var total = await usersQuery.CountAsync();
        var users = await usersQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var roles = await _roleManager.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync();

        // Non-SuperAdmin users cannot see or assign SuperAdmin role
        if (!IsSuperAdmin())
            roles.Remove("SuperAdmin");
        // Admin role can only be assigned by SuperAdmin
        if (!IsSuperAdmin())
            roles.Remove("Admin");

        var userIds = users.Select(u => u.Id).ToList();
        var roleMap = await (from ur in _db.UserRoles.AsNoTracking()
                             join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                             where userIds.Contains(ur.UserId)
                             select new { ur.UserId, RoleName = r.Name! })
            .ToListAsync();

        var rolesByUser = roleMap
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

        var rows = users.Select(u => new UserRowVm
        {
            UserId = u.Id,
            DisplayName = u.DisplayName,
            Email = u.Email ?? u.UserName ?? "(no email)",
            IsActive = u.IsActive,
            Roles = rolesByUser.TryGetValue(u.Id, out var roleList) ? roleList : new List<string>()
        }).ToList();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        var currentUser = await _userManager.GetUserAsync(User);
        return View(new IndexVm
        {
            Query = q,
            Role = role,
            Active = active,
            Sort = sort!,
            Dir = dir!,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages < 1 ? 1 : totalPages,
            Roles = roles,
            Users = rows,
            CurrentUserId = currentUser?.Id
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var roles = await _roleManager.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync();

        if (!IsSuperAdmin())
        {
            roles.Remove("SuperAdmin");
            roles.Remove("Admin");
        }

        var vm = new CreateVm { Roles = roles };

        // SuperAdmin can assign users to any company
        if (IsSuperAdmin())
        {
            vm.Companies = await _db.Companies.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new CompanyDropdownItem { CompanyId = c.CompanyId, Name = c.Name })
                .ToListAsync();
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateVm vm)
    {
        vm.Roles = await _roleManager.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync();

        // Non-SuperAdmin cannot create SuperAdmin users or Admin users
        if (!IsSuperAdmin())
        {
            vm.Roles.Remove("SuperAdmin");
            vm.Roles.Remove("Admin");
        }

        // Reload companies for SuperAdmin
        if (IsSuperAdmin())
        {
            vm.Companies = await _db.Companies.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new CompanyDropdownItem { CompanyId = c.CompanyId, Name = c.Name })
                .ToListAsync();
        }

        if (!ModelState.IsValid)
            return View(vm);

        var email = vm.Email.Trim();
        var role = vm.Role.Trim();
        var displayName = (vm.DisplayName ?? "").Trim();

        if (!await _roleManager.RoleExistsAsync(role))
        {
            ModelState.AddModelError(nameof(CreateVm.Role), "Selected role does not exist.");
            return View(vm);
        }

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(CreateVm.Email), "A user with this email already exists.");
            return View(vm);
        }

        // Determine CompanyId: SuperAdmin must pick a company, regular Admin uses their own
        Guid? assignedCompanyId;
        if (IsSuperAdmin())
        {
            if (vm.CompanyId is null || vm.CompanyId == Guid.Empty)
            {
                ModelState.AddModelError(nameof(CreateVm.CompanyId), "Please select a company for this user.");
                return View(vm);
            }
            assignedCompanyId = vm.CompanyId;
        }
        else
        {
            assignedCompanyId = GetCompanyId();
        }

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? InferDisplayNameFromEmail(email) : displayName,
            EmailConfirmed = true,
            IsActive = true,
            CompanyId = assignedCompanyId
        };

        var create = await _userManager.CreateAsync(user, vm.Password);
        if (!create.Succeeded)
        {
            foreach (var e in create.Errors)
                ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        var addRole = await _userManager.AddToRoleAsync(user, role);
        if (!addRole.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            foreach (var e in addRole.Errors)
                ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        TempData["Ok"] = $"User created: {email} ({role})";
        return RedirectToAction(nameof(Index));
    }

    private static string InferDisplayNameFromEmail(string email)
    {
        var at = email.IndexOf('@');
        var local = at > 0 ? email[..at] : email;
        local = local.Replace('.', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(local) ? email : local;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(Guid userId, string role, string? returnUrl = null)
    {
        role = (role ?? "").Trim();
        if (string.IsNullOrWhiteSpace(role))
        {
            TempData["Err"] = "Role is required.";
            return RedirectToLocal(returnUrl);
        }

        // Protect SuperAdmin: only another SuperAdmin can change a SuperAdmin's role
        var targetUser = await _userManager.FindByIdAsync(userId.ToString());
        if (targetUser is not null && await _userManager.IsInRoleAsync(targetUser, "SuperAdmin") && !IsSuperAdmin())
        {
            TempData["Err"] = "You cannot modify a SuperAdmin account.";
            return RedirectToLocal(returnUrl);
        }

        // Multi-tenant: Admin can only modify users in their own company
        if (!IsSuperAdmin() && targetUser is not null && targetUser.CompanyId != GetCompanyId())
        {
            TempData["Err"] = "User does not belong to your company.";
            return RedirectToLocal(returnUrl);
        }

        // Non-SuperAdmin cannot assign SuperAdmin or Admin role
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && !IsSuperAdmin())
        {
            TempData["Err"] = "Only a SuperAdmin can assign the SuperAdmin role.";
            return RedirectToLocal(returnUrl);
        }
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) && !IsSuperAdmin())
        {
            TempData["Err"] = "Only a SuperAdmin can assign the Admin role.";
            return RedirectToLocal(returnUrl);
        }

        if (!await _roleManager.RoleExistsAsync(role))
        {
            TempData["Err"] = "Role does not exist.";
            return RedirectToLocal(returnUrl);
        }

        var user = targetUser ?? await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            TempData["Err"] = "User not found.";
            return RedirectToLocal(returnUrl);
        }

        // Simple ERP: one primary role per user
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

        var add = await _userManager.AddToRoleAsync(user, role);
        if (!add.Succeeded)
        {
            TempData["Err"] = string.Join("; ", add.Errors.Select(e => e.Description));
            return RedirectToLocal(returnUrl);
        }

        TempData["Ok"] = $"Role updated: {user.Email} → {role}";
        return RedirectToLocal(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid userId, string? returnUrl = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is not null && currentUser.Id == userId)
        {
            TempData["Err"] = "You cannot deactivate your own account.";
            return RedirectToLocal(returnUrl);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            TempData["Err"] = "User not found.";
            return RedirectToLocal(returnUrl);
        }

        // Protect SuperAdmin from deactivation by non-SuperAdmin
        if (await _userManager.IsInRoleAsync(user, "SuperAdmin") && !IsSuperAdmin())
        {
            TempData["Err"] = "You cannot deactivate a SuperAdmin account.";
            return RedirectToLocal(returnUrl);
        }

        // Multi-tenant: Admin can only toggle users in their own company
        if (!IsSuperAdmin() && user.CompanyId != GetCompanyId())
        {
            TempData["Err"] = "User does not belong to your company.";
            return RedirectToLocal(returnUrl);
        }

        user.IsActive = !user.IsActive;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["Err"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToLocal(returnUrl);
        }

        TempData["Ok"] = user.IsActive ? $"Activated: {user.Email}" : $"Deactivated: {user.Email}";
        return RedirectToLocal(returnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    public sealed class IndexVm
    {
        public string? Query { get; set; }
        public string? Role { get; set; }
        public bool? Active { get; set; }
        public string Sort { get; set; } = "email";
        public string Dir { get; set; } = "asc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalCount { get; set; }
        public int TotalPages { get; set; } = 1;
        public List<string> Roles { get; set; } = new();
        public List<UserRowVm> Users { get; set; } = new();
        public Guid? CurrentUserId { get; set; }
    }

    public sealed class UserRowVm
    {
        public Guid UserId { get; set; }
        public string? DisplayName { get; set; }
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public sealed class CreateVm
    {
        [Display(Name = "Display name")]
        [StringLength(100)]
        public string? DisplayName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";

        [Required]
        public string Role { get; set; } = "";

        // SuperAdmin can select which company the user belongs to
        [Display(Name = "Company")]
        public Guid? CompanyId { get; set; }

        public List<string> Roles { get; set; } = new();
        public List<CompanyDropdownItem> Companies { get; set; } = new();
    }

    public sealed class CompanyDropdownItem
    {
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = "";
    }
}