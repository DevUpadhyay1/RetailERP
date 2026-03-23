using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RetailERP.Services;

namespace RetailERP.Controllers;

[AllowAnonymous]
[Route("portal/supplier")]
[EnableRateLimiting("Api")]
public class SupplierPortalController : Controller
{
    private readonly PortalService _portal;

    public SupplierPortalController(PortalService portal)
    {
        _portal = portal;
    }

    [HttpGet("access")]
    public async Task<IActionResult> Access(string token)
    {
        var validation = await _portal.ValidateSupplierTokenAsync(token);
        if (!validation.Success || validation.Context?.SupplierId is null)
        {
            ViewBag.Message = validation.Message;
            return View("Invalid");
        }

        var vm = await _portal.BuildSupplierPortalAsync(validation.Context.SupplierId.Value);
        if (vm is null)
        {
            ViewBag.Message = "Supplier profile not found.";
            return View("Invalid");
        }

        ViewBag.Token = token;
        ViewBag.ExpiresAtUtc = validation.Context.ExpiresAtUtc;
        return View(vm);
    }

    [HttpPost("respond-po")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondPo(string token, Guid purchaseId, byte responseStatus, DateTime? expectedDeliveryDate, string? supplierNote)
    {
        var validation = await _portal.ValidateSupplierTokenAsync(token);
        if (!validation.Success || validation.Context?.SupplierId is null)
        {
            TempData["Err"] = validation.Message;
            return RedirectToAction(nameof(Access), new { token });
        }

        var result = await _portal.SaveSupplierPoResponseAsync(
            validation.Context.SupplierId.Value,
            purchaseId,
            responseStatus,
            expectedDeliveryDate,
            supplierNote);

        TempData[result.Success ? "Ok" : "Err"] = result.Message;
        return RedirectToAction(nameof(Access), new { token });
    }
}
