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
    private readonly AuditService _audit;

    public FranchiseController(ApplicationDbContext db, FranchiseService svc, AuditService audit)
    {
        _db = db;
        _svc = svc;
        _audit = audit;
    }

    // ── List ──
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RoyaltyDashboard()
    {
        var scopeCompanyId = ResolveScopeCompanyId();
        if (!IsSuperAdmin() && !scopeCompanyId.HasValue)
            return Forbid();

        var dashboard = await _svc.GetDashboardAsync(scopeCompanyId);
        return View(dashboard);
    }

    // ── Calculate Royalty ──
    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Admin")]
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

    // ── Franchise Mapping Request Queue ──
    [HttpGet]
    public async Task<IActionResult> MappingRequests()
    {
        var isSuperAdmin = IsSuperAdmin();
        var scopeCompanyId = ResolveScopeCompanyId();

        if (!isSuperAdmin && !scopeCompanyId.HasValue)
            return Forbid();

        var canSubmitRequest = false;
        string? requestBlockReason = null;
        string? requestingCompanyName = null;

        if (!isSuperAdmin)
        {
            var ownCompany = await _db.Companies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == scopeCompanyId!.Value && c.IsActive);
            if (ownCompany is null)
                return Forbid();

            requestingCompanyName = ownCompany.Name;

            if (ownCompany.ParentCompanyId.HasValue)
                requestBlockReason = "Only brand-owner company admins can raise franchise operator mapping requests.";
            else
                canSubmitRequest = true;
        }

        IQueryable<FranchiseMappingRequest> query = _db.FranchiseMappingRequests
            .AsNoTracking();

        if (isSuperAdmin)
            query = query.IgnoreQueryFilters();

        query = query
            .Include(x => x.RequestingCompany)
            .Include(x => x.MappedOperatorCompany)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewedByUser)
            .AsQueryable();

        if (!isSuperAdmin)
            query = query.Where(x => x.RequestingCompanyId == scopeCompanyId!.Value);

        var rows = await query
            .OrderBy(x => x.Status == 1 ? 0 : 1)
            .ThenByDescending(x => x.RequestedAtUtc)
            .Take(250)
            .ToListAsync();

        ViewBag.IsSuperAdmin = isSuperAdmin;
        ViewBag.CanSubmitRequest = canSubmitRequest;
        ViewBag.RequestBlockReason = requestBlockReason;
        ViewBag.RequestingCompanyName = requestingCompanyName;

        if (isSuperAdmin)
        {
            var options = await _db.Companies.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(c => c.IsActive && c.BusinessType == BusinessType.Franchise)
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem($"{c.Code} — {c.Name}", c.CompanyId.ToString()))
                .ToListAsync();

            ViewBag.OperatorCompanyOptions = options;
            ViewBag.CreateCompanyUrl = Url.Action("Create", "Companies", new { businessType = BusinessType.Franchise });
            ViewBag.CompanyIndexUrl = Url.Action("Index", "Companies");
        }

        return View(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitMappingRequest(CreateFranchiseMappingRequestVm vm)
    {
        if (IsSuperAdmin())
        {
            TempData["Err"] = "SuperAdmin can map companies directly from the request queue.";
            return RedirectToAction(nameof(MappingRequests));
        }

        var scopeCompanyId = ResolveScopeCompanyId();
        if (!scopeCompanyId.HasValue)
            return Forbid();

        var ownCompany = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == scopeCompanyId.Value && c.IsActive);
        if (ownCompany is null)
            return Forbid();

        if (ownCompany.ParentCompanyId.HasValue)
        {
            TempData["Err"] = "Only brand-owner company admins can raise franchise mapping requests.";
            return RedirectToAction(nameof(MappingRequests));
        }

        if (!ModelState.IsValid)
        {
            TempData["Err"] = "Please fill required operator details before submitting mapping request.";
            return RedirectToAction(nameof(MappingRequests));
        }

        var request = new FranchiseMappingRequest
        {
            FranchiseMappingRequestId = Guid.NewGuid(),
            RequestingCompanyId = ownCompany.CompanyId,
            RequestedOperatorName = vm.RequestedOperatorName.Trim(),
            RequestedOperatorCode = string.IsNullOrWhiteSpace(vm.RequestedOperatorCode) ? null : vm.RequestedOperatorCode.Trim(),
            RequestedOperatorCity = string.IsNullOrWhiteSpace(vm.RequestedOperatorCity) ? null : vm.RequestedOperatorCity.Trim(),
            RequestedOperatorState = string.IsNullOrWhiteSpace(vm.RequestedOperatorState) ? null : vm.RequestedOperatorState.Trim(),
            RequestNote = string.IsNullOrWhiteSpace(vm.RequestNote) ? null : vm.RequestNote.Trim(),
            Status = 1,
            RequestedByUserId = GetCurrentUserId(),
            RequestedAtUtc = DateTime.UtcNow,
            CompanyId = ownCompany.CompanyId
        };

        _db.FranchiseMappingRequests.Add(request);
        await _db.SaveChangesAsync();

        try
        {
            await _audit.LogAsync(
                action: "FranchiseMappingRequested",
                entityType: "FranchiseMappingRequest",
                entityId: request.FranchiseMappingRequestId.ToString(),
                data: new
                {
                    CompanyId = request.RequestingCompanyId,
                    FranchisorCompanyId = request.RequestingCompanyId,
                    request.RequestingCompanyId,
                    request.RequestedOperatorName,
                    request.RequestedOperatorCode,
                    request.RequestedByUserId
                });
        }
        catch
        {
            // Audit logging is best-effort and should not block business flow.
        }

        TempData["Ok"] = "Franchise mapping request submitted to SuperAdmin.";
        return RedirectToAction(nameof(MappingRequests));
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveMappingRequest(Guid id, Guid mappedOperatorCompanyId, string? reviewNote = null)
    {
        if (mappedOperatorCompanyId == Guid.Empty)
        {
            TempData["Err"] = "Select an existing operator company from the queue. If it is not available, create it first from Companies and then approve mapping.";
            return RedirectToAction(nameof(MappingRequests));
        }

        var request = await _db.FranchiseMappingRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.FranchiseMappingRequestId == id && x.Status == 1);
        if (request is null)
        {
            TempData["Err"] = "Request not found or already processed.";
            return RedirectToAction(nameof(MappingRequests));
        }

        var operatorCompany = await _db.Companies
            .FirstOrDefaultAsync(c => c.CompanyId == mappedOperatorCompanyId && c.IsActive);
        if (operatorCompany is null)
        {
            TempData["Err"] = "Selected operator company is invalid. Create the operator company first in Companies, then approve this mapping request.";
            return RedirectToAction(nameof(MappingRequests));
        }

        if (operatorCompany.CompanyId == request.RequestingCompanyId)
        {
            TempData["Err"] = "Operator company cannot be same as requesting brand-owner company.";
            return RedirectToAction(nameof(MappingRequests));
        }

        if (operatorCompany.BusinessType != BusinessType.Franchise)
        {
            TempData["Err"] = "Selected company is not a franchise company. Create/select a franchise company first.";
            return RedirectToAction(nameof(MappingRequests));
        }

        if (operatorCompany.ParentCompanyId.HasValue && operatorCompany.ParentCompanyId.Value != request.RequestingCompanyId)
        {
            TempData["Err"] = "Operator company is already mapped to another brand owner.";
            return RedirectToAction(nameof(MappingRequests));
        }

        operatorCompany.ParentCompanyId = request.RequestingCompanyId;
        operatorCompany.BusinessType = BusinessType.Franchise;

        request.Status = 2;
        request.MappedOperatorCompanyId = operatorCompany.CompanyId;
        request.ReviewedByUserId = GetCurrentUserId();
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ReviewNote = string.IsNullOrWhiteSpace(reviewNote)
            ? "Approved and mapped by SuperAdmin"
            : reviewNote.Trim();

        await _db.SaveChangesAsync();

        try
        {
            await _audit.LogAsync(
                action: "FranchiseMappingApproved",
                entityType: "FranchiseMappingRequest",
                entityId: request.FranchiseMappingRequestId.ToString(),
                data: new
                {
                    CompanyId = request.RequestingCompanyId,
                    FranchisorCompanyId = request.RequestingCompanyId,
                    FranchiseCompanyId = request.MappedOperatorCompanyId,
                    request.RequestingCompanyId,
                    request.MappedOperatorCompanyId,
                    request.ReviewedByUserId
                });
        }
        catch
        {
            // Audit logging is best-effort and should not block business flow.
        }

        TempData["Ok"] = "Franchise mapping approved. Operator is now mapped to the requested brand owner.";
        return RedirectToAction(nameof(MappingRequests));
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectMappingRequest(Guid id, string? reviewNote = null)
    {
        var request = await _db.FranchiseMappingRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.FranchiseMappingRequestId == id && x.Status == 1);
        if (request is null)
        {
            TempData["Err"] = "Request not found or already processed.";
            return RedirectToAction(nameof(MappingRequests));
        }

        request.Status = 3;
        request.ReviewedByUserId = GetCurrentUserId();
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? "Rejected by SuperAdmin" : reviewNote.Trim();
        await _db.SaveChangesAsync();

        try
        {
            await _audit.LogAsync(
                action: "FranchiseMappingRejected",
                entityType: "FranchiseMappingRequest",
                entityId: request.FranchiseMappingRequestId.ToString(),
                data: new
                {
                    CompanyId = request.RequestingCompanyId,
                    FranchisorCompanyId = request.RequestingCompanyId,
                    request.RequestingCompanyId,
                    request.ReviewedByUserId,
                    request.ReviewNote
                });
        }
        catch
        {
            // Audit logging is best-effort and should not block business flow.
        }

        TempData["Ok"] = "Franchise mapping request rejected.";
        return RedirectToAction(nameof(MappingRequests));
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

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    // ── ViewModels ──
    public class CreateFranchiseMappingRequestVm
    {
        [Required, StringLength(200)]
        public string RequestedOperatorName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? RequestedOperatorCode { get; set; }

        [StringLength(100)]
        public string? RequestedOperatorCity { get; set; }

        [StringLength(100)]
        public string? RequestedOperatorState { get; set; }

        [StringLength(500)]
        public string? RequestNote { get; set; }
    }

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
