using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Data.Identity;
using System.ComponentModel.DataAnnotations;

namespace RetailERP.Controllers;

/// <summary>Sprint 4 – Company (tenant) management. SuperAdmin only.</summary>
[Authorize(Roles = "SuperAdmin")]
public class CompaniesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public CompaniesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index(string? q, bool? active = null, string sort = "code", string dir = "asc", int page = 1, int pageSize = 20)
    {
        q = (q ?? string.Empty).Trim();
        if (page < 1) page = 1;
        if (pageSize is < 10 or > 200) pageSize = 20;

        ViewData["q"] = q;
        ViewData["active"] = active;
        ViewData["sort"] = sort;
        ViewData["dir"] = dir;
        ViewData["page"] = page;
        ViewData["pageSize"] = pageSize;

        // SuperAdmin should see all companies — bypass tenant filter
        var query = _db.Companies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Code.Contains(q) || x.Name.Contains(q));

        if (active.HasValue)
            query = query.Where(x => x.IsActive == active.Value);

        var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLowerInvariant() switch
        {
            "name" => ascending ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
            "status" => ascending ? query.OrderBy(x => x.IsActive) : query.OrderByDescending(x => x.IsActive),
            _ => ascending ? query.OrderBy(x => x.Code) : query.OrderByDescending(x => x.Code),
        };

        var total = await query.CountAsync();
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewData["total"] = total;
        ViewData["totalPages"] = totalPages < 1 ? 1 : totalPages;
        ViewData["from"] = total == 0 ? 0 : ((page - 1) * pageSize + 1);
        ViewData["to"] = Math.Min(page * pageSize, total);

        return View(rows);
    }

    public async Task<IActionResult> Details(Guid? id)
    {
        if (id is null) return NotFound();
        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == id);
        if (company is null) return NotFound();

        var userCount = await _db.Users.AsNoTracking().CountAsync(u => u.CompanyId == id);
        var storeCount = await _db.Stores.IgnoreQueryFilters().AsNoTracking().CountAsync(s => s.CompanyId == id);
        ViewData["UserCount"] = userCount;
        ViewData["StoreCount"] = storeCount;

        return View(company);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? mainCompanyId = null, BusinessType? businessType = null)
    {
        var vm = new CreateCompanyVm();
        if (mainCompanyId.HasValue)
            vm.MainCompanyId = mainCompanyId;
        if (businessType.HasValue)
            vm.BusinessType = businessType.Value;

        await PopulateMainCompanyOptionsAsync(vm.MainCompanyId);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCompanyVm vm)
    {
        if (vm.MainCompanyId.HasValue)
        {
            var mainCompanyExists = await _db.Companies
                .AsNoTracking()
                .IgnoreQueryFilters()
                .AnyAsync(c => c.CompanyId == vm.MainCompanyId.Value && c.IsActive && !c.ParentCompanyId.HasValue);

            if (!mainCompanyExists)
                ModelState.AddModelError(nameof(vm.MainCompanyId), "Select a valid active main company.");

            if (vm.BusinessType != BusinessType.Franchise)
                ModelState.AddModelError(nameof(vm.BusinessType), "When main company is selected, Business Type must be Franchise.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateMainCompanyOptionsAsync(vm.MainCompanyId);
            return View(vm);
        }

        // Check if admin email already exists
        var existingUser = await _userManager.FindByEmailAsync(vm.AdminEmail);
        if (existingUser is not null)
        {
            ModelState.AddModelError(nameof(vm.AdminEmail), "A user with this email already exists.");
            await PopulateMainCompanyOptionsAsync(vm.MainCompanyId);
            return View(vm);
        }

        var company = new Company
        {
            CompanyId = Guid.NewGuid(),
            Code = vm.Code,
            Name = vm.Name,
            BusinessType = vm.BusinessType,
            Address = vm.Address,
            City = vm.City,
            State = vm.State,
            Pincode = vm.Pincode,
            Phone = vm.Phone,
            Email = vm.Email,
            Website = vm.Website,
            GstNo = vm.GstNo,
            PanNo = vm.PanNo,
            CinNo = vm.CinNo,
            ParentCompanyId = vm.MainCompanyId,
            MaxUsers = vm.MaxUsers,
            MaxStores = vm.MaxStores,
            IsActive = vm.IsActive,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.Companies.Add(company);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(vm.Code), "Company code must be unique.");
            await PopulateMainCompanyOptionsAsync(vm.MainCompanyId);
            return View(vm);
        }

        // Create admin user for the new company
        if (!await _roleManager.RoleExistsAsync("Admin"))
            await _roleManager.CreateAsync(new ApplicationRole { Name = "Admin" });

        var adminUser = new ApplicationUser
        {
            UserName = vm.AdminEmail,
            Email = vm.AdminEmail,
            DisplayName = vm.AdminDisplayName ?? vm.Name,
            EmailConfirmed = true,
            IsActive = true,
            CompanyId = company.CompanyId
        };

        var createResult = await _userManager.CreateAsync(adminUser, vm.AdminPassword);
        if (!createResult.Succeeded)
        {
            // Rollback company if user creation fails
            _db.Companies.Remove(company);
            await _db.SaveChangesAsync();
            foreach (var e in createResult.Errors)
                ModelState.AddModelError(nameof(vm.AdminPassword), e.Description);
            await PopulateMainCompanyOptionsAsync(vm.MainCompanyId);
            return View(vm);
        }

        await _userManager.AddToRoleAsync(adminUser, "Admin");

        TempData["Ok"] = $"Company '{company.Name}' created with admin account {adminUser.Email}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateMainCompanyOptionsAsync(Guid? selectedMainCompanyId)
    {
        var options = await _db.Companies
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.IsActive && !c.ParentCompanyId.HasValue)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem($"{c.Code} - {c.Name}", c.CompanyId.ToString(), selectedMainCompanyId.HasValue && c.CompanyId == selectedMainCompanyId.Value))
            .ToListAsync();

        options.Insert(0, new SelectListItem("None (Independent Company)", string.Empty, !selectedMainCompanyId.HasValue));
        ViewBag.MainCompanyOptions = options;
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id is null) return NotFound();
        var company = await _db.Companies.FindAsync(id);
        if (company is null) return NotFound();
        return View(company);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Company company)
    {
        if (id != company.CompanyId) return NotFound();
        if (!ModelState.IsValid) return View(company);

        var existing = await _db.Companies.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Code = company.Code;
        existing.Name = company.Name;
        existing.BusinessType = company.BusinessType;
        existing.Address = company.Address;
        existing.City = company.City;
        existing.State = company.State;
        existing.Pincode = company.Pincode;
        existing.Phone = company.Phone;
        existing.Email = company.Email;
        existing.Website = company.Website;
        existing.GstNo = company.GstNo;
        existing.PanNo = company.PanNo;
        existing.CinNo = company.CinNo;
        existing.MaxUsers = company.MaxUsers;
        existing.MaxStores = company.MaxStores;
        existing.IsActive = company.IsActive;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Company updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Company.Code), "Company code must be unique.");
            return View(company);
        }
    }

    // ── ViewModel for company creation (includes admin user fields) ──
    public class CreateCompanyVm
    {
        // Company fields
        [Required, StringLength(20)]
        public string Code { get; set; } = "";

        [Required, StringLength(200)]
        public string Name { get; set; } = "";

        public BusinessType BusinessType { get; set; }

        [Display(Name = "Main Company (Franchisor)")]
        public Guid? MainCompanyId { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? State { get; set; }

        [StringLength(10)]
        public string? Pincode { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [EmailAddress, StringLength(200)]
        public string? Email { get; set; }

        [StringLength(200)]
        public string? Website { get; set; }

        [StringLength(20)]
        public string? GstNo { get; set; }

        [StringLength(10)]
        public string? PanNo { get; set; }

        [StringLength(25)]
        public string? CinNo { get; set; }

        public int MaxUsers { get; set; }
        public int MaxStores { get; set; }
        public bool IsActive { get; set; } = true;

        // Admin user fields
        [Required, Display(Name = "Admin Display Name"), StringLength(100)]
        public string? AdminDisplayName { get; set; }

        [Required, EmailAddress, Display(Name = "Admin Email")]
        public string AdminEmail { get; set; } = "";

        [Required, DataType(DataType.Password), Display(Name = "Admin Password")]
        [StringLength(100, MinimumLength = 6)]
        public string AdminPassword { get; set; } = "";

        [Required, DataType(DataType.Password), Display(Name = "Confirm Password")]
        [Compare(nameof(AdminPassword), ErrorMessage = "Passwords do not match.")]
        public string AdminConfirmPassword { get; set; } = "";
    }
}
