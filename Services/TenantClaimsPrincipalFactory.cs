using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RetailERP.Data.Identity;

namespace RetailERP.Services;

/// <summary>
/// Sprint 4 – Adds the CompanyId claim to the user's cookie so TenantProvider
/// can read it on every request without a database round-trip.
/// </summary>
public sealed class TenantClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    public TenantClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        var roles = await UserManager.GetRolesAsync(user);
        var isSuperAdmin = roles.Any(r => string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase));

        if (!isSuperAdmin && user.CompanyId.HasValue)
            identity.AddClaim(new Claim("CompanyId", user.CompanyId.Value.ToString()));

        return identity;
    }
}
