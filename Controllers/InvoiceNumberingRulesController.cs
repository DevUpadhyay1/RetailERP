using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class InvoiceNumberingRulesController : Controller
{
    private readonly ApplicationDbContext _db;

    public InvoiceNumberingRulesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rules = await _db.InvoiceNumberingRules
            .AsNoTracking()
            .Include(r => r.Store)
            .OrderBy(r => r.DocumentType)
            .ThenBy(r => r.StoreId.HasValue)
            .ThenBy(r => r.StoreId == null ? string.Empty : (r.Store != null ? r.Store.Name : string.Empty))
            .ThenByDescending(r => r.IsActive)
            .ThenBy(r => r.Prefix)
            .ToListAsync();

        return View(rules);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        return View(new UpsertVm
        {
            Scope = InvoiceTemplateScope.Company,
            DocumentType = InvoiceDocumentType.TaxInvoice,
            Prefix = "TAX",
            NumberWidth = 4,
            NextNumber = 1,
            ResetPolicy = InvoiceNumberResetPolicy.Yearly,
            IsActive = true
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UpsertVm vm)
    {
        var companyId = GetCompanyIdOrNull();
        if (!companyId.HasValue)
            return BadRequest("Tenant company context missing.");

        ValidateScope(vm);
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return View(vm);
        }

        var normalizedPrefix = (vm.Prefix ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedSuffix = string.IsNullOrWhiteSpace(vm.Suffix) ? null : vm.Suffix.Trim().ToUpperInvariant();
        var scopeStoreId = vm.Scope == InvoiceTemplateScope.Store ? vm.StoreId : null;

        if (vm.IsActive)
            await DeactivateSameScopeActiveRuleAsync(companyId.Value, vm.DocumentType, scopeStoreId, null);

        var entity = new InvoiceNumberingRule
        {
            InvoiceNumberingRuleId = Guid.NewGuid(),
            CompanyId = companyId,
            StoreId = scopeStoreId,
            DocumentType = vm.DocumentType,
            Prefix = normalizedPrefix,
            Suffix = normalizedSuffix,
            NumberWidth = Math.Clamp(vm.NumberWidth, 3, 8),
            NextNumber = Math.Max(1, vm.NextNumber),
            ResetPolicy = vm.ResetPolicy,
            LastResetAtUtc = vm.LastResetAtUtc,
            IsActive = vm.IsActive,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.InvoiceNumberingRules.Add(entity);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Numbering rule created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var entity = await _db.InvoiceNumberingRules.FirstOrDefaultAsync(r => r.InvoiceNumberingRuleId == id);
        if (entity is null) return NotFound();

        await LoadLookupsAsync();
        return View(new UpsertVm
        {
            InvoiceNumberingRuleId = entity.InvoiceNumberingRuleId,
            Scope = entity.StoreId.HasValue ? InvoiceTemplateScope.Store : InvoiceTemplateScope.Company,
            StoreId = entity.StoreId,
            DocumentType = entity.DocumentType,
            Prefix = entity.Prefix,
            Suffix = entity.Suffix,
            NumberWidth = entity.NumberWidth,
            NextNumber = entity.NextNumber,
            ResetPolicy = entity.ResetPolicy,
            LastResetAtUtc = entity.LastResetAtUtc,
            IsActive = entity.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, UpsertVm vm)
    {
        if (id != vm.InvoiceNumberingRuleId) return NotFound();

        var companyId = GetCompanyIdOrNull();
        if (!companyId.HasValue)
            return BadRequest("Tenant company context missing.");

        var entity = await _db.InvoiceNumberingRules.FirstOrDefaultAsync(r => r.InvoiceNumberingRuleId == id);
        if (entity is null) return NotFound();

        ValidateScope(vm);
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return View(vm);
        }

        var normalizedPrefix = (vm.Prefix ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedSuffix = string.IsNullOrWhiteSpace(vm.Suffix) ? null : vm.Suffix.Trim().ToUpperInvariant();
        var scopeStoreId = vm.Scope == InvoiceTemplateScope.Store ? vm.StoreId : null;

        if (vm.IsActive)
            await DeactivateSameScopeActiveRuleAsync(companyId.Value, vm.DocumentType, scopeStoreId, id);

        entity.StoreId = scopeStoreId;
        entity.DocumentType = vm.DocumentType;
        entity.Prefix = normalizedPrefix;
        entity.Suffix = normalizedSuffix;
        entity.NumberWidth = Math.Clamp(vm.NumberWidth, 3, 8);
        entity.NextNumber = Math.Max(1, vm.NextNumber);
        entity.ResetPolicy = vm.ResetPolicy;
        entity.LastResetAtUtc = vm.LastResetAtUtc;
        entity.IsActive = vm.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Numbering rule updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid id)
    {
        var companyId = GetCompanyIdOrNull();
        if (!companyId.HasValue)
            return BadRequest("Tenant company context missing.");

        var entity = await _db.InvoiceNumberingRules.FirstOrDefaultAsync(r => r.InvoiceNumberingRuleId == id);
        if (entity is null) return NotFound();

        await DeactivateSameScopeActiveRuleAsync(companyId.Value, entity.DocumentType, entity.StoreId, id);
        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Rule activated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.InvoiceNumberingRules.FirstOrDefaultAsync(r => r.InvoiceNumberingRuleId == id);
        if (entity is null) return NotFound();

        _db.InvoiceNumberingRules.Remove(entity);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Rule deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task DeactivateSameScopeActiveRuleAsync(
        Guid companyId,
        InvoiceDocumentType documentType,
        Guid? storeId,
        Guid? excludeId)
    {
        var query = _db.InvoiceNumberingRules.Where(r =>
            r.CompanyId == companyId &&
            r.DocumentType == documentType &&
            r.StoreId == storeId &&
            r.IsActive);

        if (excludeId.HasValue)
            query = query.Where(r => r.InvoiceNumberingRuleId != excludeId.Value);

        var activeRows = await query.ToListAsync();
        foreach (var row in activeRows)
        {
            row.IsActive = false;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private void ValidateScope(UpsertVm vm)
    {
        if (vm.Scope == InvoiceTemplateScope.Store && !vm.StoreId.HasValue)
            ModelState.AddModelError(nameof(UpsertVm.StoreId), "Store is required for store-level override.");
    }

    private async Task LoadLookupsAsync()
    {
        ViewBag.DocumentTypes = new SelectList(new[]
        {
            new { Value = (byte)InvoiceDocumentType.TaxInvoice, Text = "Tax Invoice" },
            new { Value = (byte)InvoiceDocumentType.BillOfSupply, Text = "Bill of Supply" },
            new { Value = (byte)InvoiceDocumentType.CreditNote, Text = "Credit Note" },
            new { Value = (byte)InvoiceDocumentType.DebitNote, Text = "Debit Note" },
            new { Value = (byte)InvoiceDocumentType.ProformaInvoice, Text = "Proforma Invoice" }
        }, "Value", "Text");

        ViewBag.ResetPolicies = new SelectList(new[]
        {
            new { Value = (byte)InvoiceNumberResetPolicy.Never, Text = "Never" },
            new { Value = (byte)InvoiceNumberResetPolicy.Yearly, Text = "Yearly" },
            new { Value = (byte)InvoiceNumberResetPolicy.Monthly, Text = "Monthly" }
        }, "Value", "Text");

        ViewBag.Stores = new SelectList(
            await _db.Stores.AsNoTracking().OrderBy(s => s.Name).ToListAsync(),
            "StoreId",
            "Name");
    }

    private Guid? GetCompanyIdOrNull()
    {
        var raw = User.FindFirstValue("CompanyId");
        return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
    }

    public sealed class UpsertVm
    {
        public Guid InvoiceNumberingRuleId { get; set; }

        public InvoiceTemplateScope Scope { get; set; } = InvoiceTemplateScope.Company;

        public Guid? StoreId { get; set; }

        [Required]
        public InvoiceDocumentType DocumentType { get; set; } = InvoiceDocumentType.TaxInvoice;

        [Required, StringLength(20)]
        public string Prefix { get; set; } = "INV";

        [StringLength(20)]
        public string? Suffix { get; set; }

        [Range(3, 8)]
        public int NumberWidth { get; set; } = 4;

        [Range(1, int.MaxValue)]
        public int NextNumber { get; set; } = 1;

        public InvoiceNumberResetPolicy ResetPolicy { get; set; } = InvoiceNumberResetPolicy.Yearly;

        public DateTime? LastResetAtUtc { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
