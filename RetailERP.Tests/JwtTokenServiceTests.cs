using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RetailERP.Services;

namespace RetailERP.Tests;

public class JwtTokenServiceTests
{
    [Fact]
    public void GenerateAccessToken_ShouldContainExpectedClaims()
    {
        var opts = new JwtOptions
        {
            SecretKey = "RetailERP_Sprint16_Testing_Secret_Key_Length_AtLeast_32!",
            Issuer = "RetailERP.Tests",
            Audience = "RetailERP.Tests.Api",
            AccessTokenExpiryMinutes = 30
        };
        var service = new JwtTokenService(opts);

        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var token = service.GenerateAccessToken(
            userId,
            "owner@test.com",
            "Owner",
            companyId,
            new[] { "Admin", "Manager" });

        Assert.False(string.IsNullOrWhiteSpace(token));

        var principal = service.GetPrincipalFromExpiredToken(token);
        Assert.NotNull(principal);

        var resolvedUserId =
            principal!.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var resolvedEmail =
            principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value ??
            principal.FindFirst(ClaimTypes.Email)?.Value;

        Assert.Equal(userId.ToString(), resolvedUserId);
        Assert.Equal("owner@test.com", resolvedEmail);
        Assert.Equal(companyId.ToString(), principal.FindFirst("companyId")?.Value);

        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList();
        Assert.Contains("Admin", roles);
        Assert.Contains("Manager", roles);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnRandomBase64Tokens()
    {
        var service = new JwtTokenService(new JwtOptions
        {
            SecretKey = "RetailERP_Sprint16_Testing_Secret_Key_Length_AtLeast_32!"
        });

        var t1 = service.GenerateRefreshToken();
        var t2 = service.GenerateRefreshToken();

        Assert.NotEqual(t1, t2);
        Assert.NotEmpty(Convert.FromBase64String(t1));
        Assert.NotEmpty(Convert.FromBase64String(t2));
    }
}
