using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Controllers;

/// <summary>
/// Sprint 17: Tenant admin settings.
/// Admin can configure payment gateway credentials for their own company only.
/// </summary>
[Authorize(Roles = "Admin")]
public class AdminSettingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly RazorpayService _razorpay;

    public AdminSettingsController(ApplicationDbContext db, RazorpayService razorpay)
    {
        _db = db;
        _razorpay = razorpay;
    }

    private Guid? GetCompanyId()
    {
        var raw = User.FindFirstValue("CompanyId");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    [HttpGet]
    public async Task<IActionResult> PaymentGateway()
    {
        var companyId = GetCompanyId();
        if (!companyId.HasValue) return Forbid();

        var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId.Value);
        if (company is null) return NotFound();

        var vm = new PaymentGatewaySettingsVm
        {
            CompanyName = company.Name,
            GatewayProvider = company.GatewayProvider,
            GatewayKeyId = company.GatewayKeyId,
            HasSavedSecret = !string.IsNullOrWhiteSpace(company.GatewayKeySecret)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PaymentGateway(PaymentGatewaySettingsVm vm)
    {
        var companyId = GetCompanyId();
        if (!companyId.HasValue) return Forbid();

        var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId.Value);
        if (company is null) return NotFound();

        if (vm.GatewayProvider == PaymentGatewayProvider.Stripe)
            ModelState.AddModelError(nameof(vm.GatewayProvider), "Stripe support is coming soon. Please use Razorpay for now.");

        if (vm.GatewayProvider == PaymentGatewayProvider.Razorpay)
        {
            if (string.IsNullOrWhiteSpace(vm.GatewayKeyId) && string.IsNullOrWhiteSpace(company.GatewayKeyId))
                ModelState.AddModelError(nameof(vm.GatewayKeyId), "Razorpay Key ID is required.");

            if (string.IsNullOrWhiteSpace(vm.GatewayKeySecret) && string.IsNullOrWhiteSpace(company.GatewayKeySecret))
                ModelState.AddModelError(nameof(vm.GatewayKeySecret), "Razorpay Key Secret is required.");
        }

        if (!ModelState.IsValid)
        {
            vm.CompanyName = company.Name;
            vm.HasSavedSecret = !string.IsNullOrWhiteSpace(company.GatewayKeySecret);
            return View(vm);
        }

        company.GatewayProvider = vm.GatewayProvider;

        if (vm.GatewayProvider == PaymentGatewayProvider.None)
        {
            company.GatewayKeyId = null;
            company.GatewayKeySecret = null;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(vm.GatewayKeyId))
                company.GatewayKeyId = vm.GatewayKeyId.Trim();

            if (!string.IsNullOrWhiteSpace(vm.GatewayKeySecret))
                company.GatewayKeySecret = vm.GatewayKeySecret.Trim();
        }

        company.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Payment gateway settings saved.";
        return RedirectToAction(nameof(PaymentGateway));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestRazorpayConnection([FromForm] RazorpayTestVm vm)
    {
        var companyId = GetCompanyId();
        if (!companyId.HasValue)
            return Json(new { success = false, message = "Access denied." });

        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId.Value);
        if (company is null)
            return Json(new { success = false, message = "Company not found." });

        var keyId = string.IsNullOrWhiteSpace(vm.GatewayKeyId) ? company.GatewayKeyId : vm.GatewayKeyId.Trim();
        var keySecret = string.IsNullOrWhiteSpace(vm.GatewayKeySecret) ? company.GatewayKeySecret : vm.GatewayKeySecret.Trim();

        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
            return Json(new { success = false, message = "Enter Key ID and Key Secret (or save existing first)." });

        var test = await _razorpay.TestCredentialsAsync(keyId, keySecret);
        return Json(new { success = test.Success, message = test.Message });
    }

    public sealed class PaymentGatewaySettingsVm
    {
        public string CompanyName { get; set; } = "";

        [Display(Name = "Gateway Provider")]
        public PaymentGatewayProvider GatewayProvider { get; set; } = PaymentGatewayProvider.None;

        [Display(Name = "Razorpay Key ID")]
        [StringLength(100)]
        public string? GatewayKeyId { get; set; }

        [Display(Name = "Razorpay Key Secret")]
        [StringLength(100)]
        public string? GatewayKeySecret { get; set; }

        public bool HasSavedSecret { get; set; }
    }

    public sealed class RazorpayTestVm
    {
        [StringLength(100)]
        public string? GatewayKeyId { get; set; }

        [StringLength(100)]
        public string? GatewayKeySecret { get; set; }
    }
}
