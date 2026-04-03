using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace RetailERP.Controllers;

/// <summary>Sprint 15 – Franchise agreement management and royalty dashboard.</summary>
[Authorize(Roles = "SuperAdmin,Admin")]
public class FranchiseController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly FranchiseService _svc;

    public FranchiseController(ApplicationDbContext db, FranchiseService svc)
    {
        _db = db;
        _svc = svc;
    }

    // ── List ──
    public async Task<IActionResult> Index(string? q, byte? status, int page = 1, int pageSize = 25)
    {
        if (page < 1) page = 1;
        ViewData["q"] = q;
        ViewData["status"] = status;

        var scopeCompanyId = ResolveScopeCompanyId();
        if (!IsSuperAdmin() && !scopeCompanyId.HasValue)
            return Forbid();

        var total = await _svc.CountAgreementsAsync(q, status, scopeCompanyId);
        var rows = await _svc.GetAgreementsAsync(q, status, page, pageSize, scopeCompanyId);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        ViewData["total"] = total;
        ViewData["page"] = page;
        ViewData["pageSize"] = pageSize;
        ViewData["totalPages"] = totalPages;

        return View(rows);
    }

    // ── Details + Royalty History ──
    public async Task<IActionResult> Details(Guid? id)
    {
        if (id is null) return NotFound();

        var scopeCompanyId = ResolveScopeCompanyId();
        if (!IsSuperAdmin() && !scopeCompanyId.HasValue)
            return Forbid();

        var agreement = await _svc.GetByIdAsync(id.Value, scopeCompanyId);
        if (agreement is null) return NotFound();
        return View(agreement);
    }

    // ── Create ──
    public async Task<IActionResult> Create()
    {
        Guid? lockedFranchisorId = null;
        var vm = new CreateFranchiseAgreementVm();
        var noOperatorAvailable = false;

        if (!IsSuperAdmin())
        {
            var scopeCompanyId = ResolveScopeCompanyId();
            if (!scopeCompanyId.HasValue)
                return Forbid();

            var ownCompany = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == scopeCompanyId.Value && c.IsActive);
            if (ownCompany is null)
                return Forbid();

            if (ownCompany.ParentCompanyId.HasValue)
            {
                TempData["Err"] = "Only brand-owner company admins can create franchise agreements.";
                return RedirectToAction(nameof(Index));
            }

            lockedFranchisorId = ownCompany.CompanyId;
            vm.FranchisorCompanyId = ownCompany.CompanyId;

            noOperatorAvailable = !await _db.Companies.AsNoTracking()
                .AnyAsync(c => c.IsActive && c.ParentCompanyId == ownCompany.CompanyId);
        }

        await PopulateCompanyListsAsync(lockedFranchisorId);
        ViewBag.NoOperatorAvailable = noOperatorAvailable;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateFranchiseAgreementVm vm)
    {
        Guid? lockedFranchisorId = null;
        var noOperatorAvailable = false;

        if (!IsSuperAdmin())
        {
            var scopeCompanyId = ResolveScopeCompanyId();
            if (!scopeCompanyId.HasValue)
                return Forbid();

            var ownCompany = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == scopeCompanyId.Value && c.IsActive);
            if (ownCompany is null)
                return Forbid();

            if (ownCompany.ParentCompanyId.HasValue)
            {
                TempData["Err"] = "Only brand-owner company admins can create franchise agreements.";
                return RedirectToAction(nameof(Index));
            }

            lockedFranchisorId = ownCompany.CompanyId;
            vm.FranchisorCompanyId = ownCompany.CompanyId; // server-side lock against tampered posts

            noOperatorAvailable = !await _db.Companies.AsNoTracking()
                .AnyAsync(c => c.IsActive && c.ParentCompanyId == ownCompany.CompanyId);

            if (noOperatorAvailable)
            {
                ModelState.AddModelError(nameof(vm.FranchiseeCompanyId),
                    "No operator company is mapped to your brand yet. Please request SuperAdmin to create/map franchise operator first.");
            }
        }

        var franchiseeQuery = _db.Companies
            .AsNoTracking()
            .Where(c => c.CompanyId == vm.FranchiseeCompanyId && c.IsActive);

        if (lockedFranchisorId.HasValue)
        {
            // Tenant admin can only choose operator companies already mapped under their brand owner company.
            franchiseeQuery = franchiseeQuery.Where(c => c.ParentCompanyId == lockedFranchisorId.Value);
        }

        var franchisee = await franchiseeQuery.FirstOrDefaultAsync();

        if (franchisee is null)
        {
            ModelState.AddModelError(nameof(vm.FranchiseeCompanyId), lockedFranchisorId.HasValue
                ? "Select a valid operator company already mapped to your brand."
                : "Select a valid active operator company.");
        }
        else if (franchisee.CompanyId == vm.FranchisorCompanyId)
            ModelState.AddModelError(nameof(vm.FranchiseeCompanyId), "Franchisee must be different from franchisor.");
        else if (franchisee.ParentCompanyId.HasValue && franchisee.ParentCompanyId.Value != vm.FranchisorCompanyId)
            ModelState.AddModelError(nameof(vm.FranchiseeCompanyId), "This operator is already linked to another brand owner.");

        var franchisor = await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == vm.FranchisorCompanyId && c.IsActive);
        if (franchisor is null)
            ModelState.AddModelError(nameof(vm.FranchisorCompanyId), "Select a valid active brand-owner company.");

        if (!ModelState.IsValid)
        {
            await PopulateCompanyListsAsync(lockedFranchisorId);
            ViewBag.NoOperatorAvailable = noOperatorAvailable;
            return View(vm);
        }

        var agreement = new FranchiseAgreement
        {
            AgreementCode = vm.AgreementCode,
            FranchisorCompanyId = vm.FranchisorCompanyId,
            FranchiseeCompanyId = vm.FranchiseeCompanyId,
            RoyaltyPercent = vm.RoyaltyPercent,
            MonthlyFlatFee = vm.MonthlyFlatFee,
            MinMonthlyRoyalty = vm.MinMonthlyRoyalty,
            Territory = vm.Territory ?? string.Empty,
            StartDate = vm.StartDate,
            EndDate = vm.EndDate,
            Notes = vm.Notes,
            Status = 1
        };

        var (ok, error) = await _svc.CreateAgreementAsync(agreement);
        if (!ok)
        {
            ModelState.AddModelError("", error!);
            await PopulateCompanyListsAsync(lockedFranchisorId);
            ViewBag.NoOperatorAvailable = noOperatorAvailable;
            return View(vm);
        }

        TempData["Ok"] = $"Franchise agreement {agreement.AgreementCode} created.";
        return RedirectToAction(nameof(Details), new { id = agreement.FranchiseAgreementId });
    }

    // ── Edit ──
    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id is null) return NotFound();

        var scopeCompanyId = ResolveScopeCompanyId();
        if (!IsSuperAdmin() && !scopeCompanyId.HasValue)
            return Forbid();

        var agreement = await _svc.GetByIdAsync(id.Value, scopeCompanyId);
        if (agreement is null) return NotFound();

        var vm = new EditFranchiseAgreementVm
        {
            FranchiseAgreementId = agreement.FranchiseAgreementId,
            AgreementCode = agreement.AgreementCode,
            RoyaltyPercent = agreement.RoyaltyPercent,
            MonthlyFlatFee = agreement.MonthlyFlatFee,
            MinMonthlyRoyalty = agreement.MinMonthlyRoyalty,
            Territory = agreement.Territory,
            StartDate = agreement.StartDate,
            EndDate = agreement.EndDate,
            Status = agreement.Status,
            Notes = agreement.Notes,
            FranchisorName = agreement.FranchisorCompany?.Name ?? "—",
            FranchiseeName = agreement.FranchiseeCompany?.Name ?? "—"
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditFranchiseAgreementVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var scopeCompanyId = ResolveScopeCompanyId();
        if (!IsSuperAdmin() && !scopeCompanyId.HasValue)
            return Forbid();

        var agreement = await _svc.GetByIdAsync(vm.FranchiseAgreementId, scopeCompanyId);
        if (agreement is null) return NotFound();

        agreement.AgreementCode = vm.AgreementCode;
        agreement.RoyaltyPercent = vm.RoyaltyPercent;
        agreement.MonthlyFlatFee = vm.MonthlyFlatFee;
        agreement.MinMonthlyRoyalty = vm.MinMonthlyRoyalty;
        agreement.Territory = vm.Territory ?? string.Empty;
        agreement.StartDate = vm.StartDate;
        agreement.EndDate = vm.EndDate;
        agreement.Status = vm.Status;
        agreement.Notes = vm.Notes;

        await _svc.UpdateAgreementAsync(agreement);
        TempData["Ok"] = "Agreement updated.";
        return RedirectToAction(nameof(Details), new { id = agreement.FranchiseAgreementId });
    }

    // ── Royalty Dashboard ──
    public async Task<IActionResult> RoyaltyDashboard()
    {
        var scopeCompanyId = ResolveScopeCompanyId();
        if (!IsSuperAdmin() && !scopeCompanyId.HasValue)
            return Forbid();

        var dashboard = await _svc.GetDashboardAsync(scopeCompanyId);
        return View(dashboard);
    }

    // ── Calculate Royalty ──
    public async Task<IActionResult> CalculateRoyalty(Guid? agreementId)
    {
        if (agreementId is null) return NotFound();

        var scopeCompanyId = ResolveScopeCompanyId();
        if (!IsSuperAdmin() && !scopeCompanyId.HasValue)
            return Forbid();

        var agreement = await _svc.GetByIdAsync(agreementId.Value, scopeCompanyId);
        if (agreement is null) return NotFound();

        var now = DateTime.UtcNow;
        ViewData["Year"] = now.Year;
        ViewData["Month"] = now.Month;
        ViewData["Agreement"] = agreement;

        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CalculateRoyalty(Guid agreementId, int year, int month)
    {
        var scopeCompanyId = ResolveScopeCompanyId();
        if (!IsSuperAdmin() && !scopeCompanyId.HasValue)
            return Forbid();

        var calc = await _svc.CalculateRoyaltyAsync(agreementId, year, month, scopeCompanyId);
        var agreement = await _svc.GetByIdAsync(agreementId, scopeCompanyId);

        ViewData["Year"] = year;
        ViewData["Month"] = month;
        ViewData["Agreement"] = agreement;
        ViewData["Calculation"] = calc;

        return View();
    }

    // ── Generate Royalty Payment ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GeneratePayment(Guid agreementId, int year, int month)
    {
        var scopeCompanyId = ResolveScopeCompanyId();
        if (!IsSuperAdmin() && !scopeCompanyId.HasValue)
            return Forbid();

        var (ok, error, payment) = await _svc.GenerateRoyaltyPaymentAsync(agreementId, year, month, scopeCompanyId);

        if (!ok)
        {
            TempData["Err"] = error;
            return RedirectToAction(nameof(Details), new { id = agreementId });
        }

        TempData["Ok"] = $"Royalty payment generated for {year}/{month:D2}. Total due: ₹{payment!.TotalDue:N2}";
        return RedirectToAction(nameof(Details), new { id = agreementId });
    }

    // ── Record Payment ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(Guid paymentId, decimal amountPaid, string? remarks, Guid agreementId)
    {
        var scopeCompanyId = ResolveScopeCompanyId();
        if (!IsSuperAdmin() && !scopeCompanyId.HasValue)
            return Forbid();

        var (ok, error) = await _svc.RecordPaymentAsync(paymentId, amountPaid, remarks, scopeCompanyId);

        if (!ok)
            TempData["Err"] = error;
        else
            TempData["Ok"] = $"Payment of ₹{amountPaid:N2} recorded.";

        return RedirectToAction(nameof(Details), new { id = agreementId });
    }

    // ── Helpers ──
    private async Task PopulateCompanyListsAsync(Guid? lockedFranchisorCompanyId = null)
    {
        var companies = await _db.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new { c.CompanyId, c.Name, c.Code, c.ParentCompanyId })
            .ToListAsync();

        var franchisorCompanies = lockedFranchisorCompanyId.HasValue
            ? companies.Where(c => c.CompanyId == lockedFranchisorCompanyId.Value).ToList()
            : companies;

        var franchiseeCompanies = lockedFranchisorCompanyId.HasValue
            ? companies.Where(c => c.ParentCompanyId == lockedFranchisorCompanyId.Value).ToList()
            : companies;

        ViewBag.FranchisorCompanies = franchisorCompanies
            .Select(c => new SelectListItem($"{c.Code} — {c.Name}", c.CompanyId.ToString()))
            .ToList();

        ViewBag.FranchiseeCompanies = franchiseeCompanies
            .Select(c => new SelectListItem($"{c.Code} — {c.Name}", c.CompanyId.ToString()))
            .ToList();

        ViewBag.LockFranchisor = lockedFranchisorCompanyId.HasValue;
        ViewBag.LockedFranchisorName = lockedFranchisorCompanyId.HasValue
            ? companies.FirstOrDefault(c => c.CompanyId == lockedFranchisorCompanyId.Value)?.Name
            : null;
    }

    private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");

    private Guid? ResolveScopeCompanyId()
    {
        if (IsSuperAdmin())
            return null;

        var raw = User.FindFirstValue("CompanyId");
        return Guid.TryParse(raw, out var companyId) ? companyId : null;
    }

    // ── ViewModels ──
    public class CreateFranchiseAgreementVm
    {
        [Required, StringLength(100)]
        public string AgreementCode { get; set; } = string.Empty;

        [Required]
        public Guid FranchisorCompanyId { get; set; }

        [Required]
        public Guid FranchiseeCompanyId { get; set; }

        [Range(0, 100)]
        public decimal RoyaltyPercent { get; set; } = 5;

        [Range(0, 999999)]
        public decimal MonthlyFlatFee { get; set; }

        [Range(0, 999999)]
        public decimal MinMonthlyRoyalty { get; set; }

        [StringLength(50)]
        public string? Territory { get; set; }

        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public DateOnly? EndDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class EditFranchiseAgreementVm
    {
        public Guid FranchiseAgreementId { get; set; }

        [Required, StringLength(100)]
        public string AgreementCode { get; set; } = string.Empty;

        [Range(0, 100)]
        public decimal RoyaltyPercent { get; set; }

        [Range(0, 999999)]
        public decimal MonthlyFlatFee { get; set; }

        [Range(0, 999999)]
        public decimal MinMonthlyRoyalty { get; set; }

        [StringLength(50)]
        public string? Territory { get; set; }

        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }

        public byte Status { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public string FranchisorName { get; set; } = "";
        public string FranchiseeName { get; set; } = "";
    }
}
