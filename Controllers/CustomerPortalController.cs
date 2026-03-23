using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RetailERP.Services;

namespace RetailERP.Controllers;

[AllowAnonymous]
[Route("portal/customer")]
[EnableRateLimiting("Api")]
public class CustomerPortalController : Controller
{
    private readonly PortalService _portal;

    public CustomerPortalController(PortalService portal)
    {
        _portal = portal;
    }

    [HttpGet("access")]
    public async Task<IActionResult> Access(string token)
    {
        var validation = await _portal.ValidateCustomerTokenAsync(token);
        if (!validation.Success || validation.Context?.CustomerId is null)
        {
            ViewBag.Message = validation.Message;
            return View("Invalid");
        }

        var vm = await _portal.BuildCustomerPortalAsync(validation.Context.CustomerId.Value);
        if (vm is null)
        {
            ViewBag.Message = "Customer profile not found.";
            return View("Invalid");
        }

        ViewBag.Token = token;
        ViewBag.ExpiresAtUtc = validation.Context.ExpiresAtUtc;
        return View(vm);
    }

    [HttpPost("request-return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReturn(string token, Guid posBillId, string? reason)
    {
        var validation = await _portal.ValidateCustomerTokenAsync(token);
        if (!validation.Success || validation.Context?.CustomerId is null)
        {
            TempData["Err"] = validation.Message;
            return RedirectToAction(nameof(Access), new { token });
        }

        var result = await _portal.SubmitReturnRequestAsync(validation.Context.CustomerId.Value, posBillId, reason);
        TempData[result.Success ? "Ok" : "Err"] = result.Message;
        return RedirectToAction(nameof(Access), new { token });
    }
}
