using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RetailERP.Controllers.Api;

/// <summary>
/// Sprint 5: Base class for all REST API controllers.
/// Accepts JWT Bearer or Cookie authentication. Routes under /api/v1/{controller}.
/// </summary>
[Area("api")]
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + ",Identity.Application")]
[Produces("application/json")]
public abstract class ApiBaseController : ControllerBase
{
    /// <summary>Current user's CompanyId from JWT claims.</summary>
    protected Guid? GetCompanyId()
    {
        var claim = User.FindFirst("companyId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>Current user's ID from JWT sub claim.</summary>
    protected Guid GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
