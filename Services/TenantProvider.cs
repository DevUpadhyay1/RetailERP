using System.Security.Claims;

namespace RetailERP.Services;

/// <summary>Sprint 4 – Resolves the current tenant (CompanyId) from the logged-in user's claims.</summary>
public interface ITenantProvider
{
    /// <summary>Current tenant id, or null for SuperAdmin / anonymous.</summary>
    Guid? CompanyId { get; }

    /// <summary>True when the user is SuperAdmin (sees all tenants).</summary>
    bool IsSuperAdmin { get; }
}

public sealed class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _http;

    /// <summary>Reads lazily so the value is correct even when the provider is
    /// resolved during authentication (before HttpContext.User is set).</summary>
    public Guid? CompanyId
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;
            var raw = user.FindFirstValue("CompanyId");
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public bool IsSuperAdmin
    {
        get
        {
            var user = _http.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true && user.IsInRole("SuperAdmin");
        }
    }

    public TenantProvider(IHttpContextAccessor http)
    {
        _http = http;
    }
}
