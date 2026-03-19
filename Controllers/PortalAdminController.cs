using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory")]
public class PortalAdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PortalService _portal;

    public PortalAdminController(ApplicationDbContext db, PortalService portal)
    {
        _db = db;
        _portal = portal;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vm = new PortalAdminVm
        {
            Customers = await _db.Customers
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Take(400)
                .Select(x => new SelectRow
                {
                    Id = x.CustomerId,
                    Label = x.Name + (x.Phone != null ? $" ({x.Phone})" : "")
                })
                .ToListAsync(),

            Suppliers = await _db.Suppliers
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Take(400)
                .Select(x => new SelectRow
                {
                    Id = x.SupplierId,
                    Label = x.Name + (x.Phone != null ? $" ({x.Phone})" : "")
                })
                .ToListAsync(),

            RecentLinks = await _db.PortalAccessLinks
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Supplier)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(80)
                .Select(x => new LinkRow
                {
                    PortalAccessLinkId = x.PortalAccessLinkId,
                    PortalType = x.PortalType,
                    TargetName = x.Customer != null ? x.Customer.Name : (x.Supplier != null ? x.Supplier.Name : "-"),
                    TokenHint = x.TokenHint,
                    Label = x.Label,
                    CreatedAtUtc = x.CreatedAtUtc,
                    ExpiresAtUtc = x.ExpiresAtUtc,
                    IsRevoked = x.IsRevoked,
                    LastAccessedAtUtc = x.LastAccessedAtUtc
                })
                .ToListAsync(),

            ReturnRequests = await _db.PortalReturnRequests
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.PosBill)
                .Include(x => x.ReviewedByUser)
                .OrderByDescending(x => x.RequestedAtUtc)
                .Take(80)
                .Select(x => new ReturnRequestRow
                {
                    PortalReturnRequestId = x.PortalReturnRequestId,
                    CustomerName = x.Customer != null ? x.Customer.Name : "-",
                    BillNo = x.PosBill != null ? x.PosBill.BillNo : "-",
                    RequestedAtUtc = x.RequestedAtUtc,
                    Status = x.Status,
                    Reason = x.Reason,
                    AdminNote = x.AdminNote,
                    ReviewedBy = x.ReviewedByUser != null ? (x.ReviewedByUser.DisplayName ?? x.ReviewedByUser.UserName ?? x.ReviewedByUser.Email ?? "-") : "-"
                })
                .ToListAsync(),

            SupplierPoResponses = await _db.SupplierPoResponses
                .AsNoTracking()
                .Include(x => x.Supplier)
                .Include(x => x.Purchase)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Take(80)
                .Select(x => new SupplierPoResponseRow
                {
                    SupplierPoResponseId = x.SupplierPoResponseId,
                    SupplierName = x.Supplier != null ? x.Supplier.Name : "-",
                    PurchaseNo = x.Purchase != null ? x.Purchase.PurchaseNo : "-",
                    ResponseStatus = x.ResponseStatus,
                    RespondedAtUtc = x.RespondedAtUtc,
                    ExpectedDeliveryDate = x.ExpectedDeliveryDate,
                    SupplierNote = x.SupplierNote
                })
                .ToListAsync()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateCustomerLink(Guid customerId, int validHours = 72, string? label = null)
    {
        var result = await _portal.GenerateCustomerLinkAsync(customerId, validHours, label);
        if (!result.Success)
        {
            TempData["Err"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        var url = $"{Request.Scheme}://{Request.Host}" +
                  Url.Action("Access", "CustomerPortal", new { token = result.Token });
        TempData["Ok"] = $"Customer portal link created. Expires UTC: {result.ExpiresAtUtc:yyyy-MM-dd HH:mm}.";
        TempData["PortalLink"] = url;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateSupplierLink(Guid supplierId, int validHours = 72, string? label = null)
    {
        var result = await _portal.GenerateSupplierLinkAsync(supplierId, validHours, label);
        if (!result.Success)
        {
            TempData["Err"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        var url = $"{Request.Scheme}://{Request.Host}" +
                  Url.Action("Access", "SupplierPortal", new { token = result.Token });
        TempData["Ok"] = $"Supplier portal link created. Expires UTC: {result.ExpiresAtUtc:yyyy-MM-dd HH:mm}.";
        TempData["PortalLink"] = url;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeLink(Guid portalAccessLinkId)
    {
        var ok = await _portal.RevokeLinkAsync(portalAccessLinkId);
        TempData[ok ? "Ok" : "Err"] = ok ? "Portal link revoked." : "Portal link not found.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReturnRequest(Guid portalReturnRequestId, byte status, string? adminNote = null)
    {
        Guid? reviewerId = null;
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(uid, out var parsed)) reviewerId = parsed;

        var result = await _portal.ReviewReturnRequestAsync(portalReturnRequestId, status, adminNote, reviewerId);
        TempData[result.Success ? "Ok" : "Err"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    public sealed class PortalAdminVm
    {
        public List<SelectRow> Customers { get; set; } = new();
        public List<SelectRow> Suppliers { get; set; } = new();
        public List<LinkRow> RecentLinks { get; set; } = new();
        public List<ReturnRequestRow> ReturnRequests { get; set; } = new();
        public List<SupplierPoResponseRow> SupplierPoResponses { get; set; } = new();
    }

    public sealed class SelectRow
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public sealed class LinkRow
    {
        public Guid PortalAccessLinkId { get; set; }
        public byte PortalType { get; set; }
        public string TargetName { get; set; } = "-";
        public string? TokenHint { get; set; }
        public string? Label { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? LastAccessedAtUtc { get; set; }
        public bool IsRevoked { get; set; }
    }

    public sealed class ReturnRequestRow
    {
        public Guid PortalReturnRequestId { get; set; }
        public string CustomerName { get; set; } = "-";
        public string BillNo { get; set; } = "-";
        public DateTime RequestedAtUtc { get; set; }
        public byte Status { get; set; }
        public string? Reason { get; set; }
        public string? AdminNote { get; set; }
        public string ReviewedBy { get; set; } = "-";
    }

    public sealed class SupplierPoResponseRow
    {
        public Guid SupplierPoResponseId { get; set; }
        public string SupplierName { get; set; } = "-";
        public string PurchaseNo { get; set; } = "-";
        public byte ResponseStatus { get; set; }
        public DateTime? RespondedAtUtc { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? SupplierNote { get; set; }
    }
}
