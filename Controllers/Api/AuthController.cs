using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Data.Identity;
using RetailERP.Models.Api;
using RetailERP.Services;
using System.IdentityModel.Tokens.Jwt;

namespace RetailERP.Controllers.Api;

/// <summary>Sprint 5: JWT authentication endpoints.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly SignInManager<ApplicationUser> _signInMgr;
    private readonly JwtTokenService _jwt;
    private readonly JwtOptions _jwtOpts;
    private readonly ApplicationDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userMgr,
        SignInManager<ApplicationUser> signInMgr,
        JwtTokenService jwt,
        JwtOptions jwtOpts,
        ApplicationDbContext db)
    {
        _userMgr = userMgr;
        _signInMgr = signInMgr;
        _jwt = jwt;
        _jwtOpts = jwtOpts;
        _db = db;
    }

    /// <summary>Login with email/password, returns JWT access + refresh tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Invalid request."));

        var user = await _userMgr.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(ApiResponse<object>.Fail("Invalid email or password."));

        if (!user.IsActive)
            return Unauthorized(ApiResponse<object>.Fail("Account is deactivated."));

        var result = await _signInMgr.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return Unauthorized(ApiResponse<object>.Fail("Account locked out. Try again later."));
        if (!result.Succeeded)
            return Unauthorized(ApiResponse<object>.Fail("Invalid email or password."));

        var roles = await _userMgr.GetRolesAsync(user);
        var accessToken = _jwt.GenerateAccessToken(
            user.Id, user.Email!, user.DisplayName, user.CompanyId, roles);

        var refreshToken = _jwt.GenerateRefreshToken();
        var refreshEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOpts.RefreshTokenExpiryDays)
        };
        _db.RefreshTokens.Add(refreshEntity);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<TokenResponse>.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOpts.AccessTokenExpiryMinutes)
        }));
    }

    /// <summary>Exchange expired access token + valid refresh token for a new token pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Invalid request."));

        var principal = _jwt.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
            return Unauthorized(ApiResponse<object>.Fail("Invalid access token."));

        var userIdStr = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token claims."));

        var stored = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.Token == request.RefreshToken && !r.IsRevoked)
            .FirstOrDefaultAsync();

        if (stored is null || stored.ExpiresAtUtc < DateTime.UtcNow)
            return Unauthorized(ApiResponse<object>.Fail("Refresh token is invalid or expired."));

        // Revoke old refresh token
        stored.IsRevoked = true;

        var user = await _userMgr.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
            return Unauthorized(ApiResponse<object>.Fail("User not found or deactivated."));

        var roles = await _userMgr.GetRolesAsync(user);
        var newAccessToken = _jwt.GenerateAccessToken(userId, user.Email!, user.DisplayName, user.CompanyId, roles);
        var newRefreshToken = _jwt.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Token = newRefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOpts.RefreshTokenExpiryDays)
        });
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<TokenResponse>.Ok(new TokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOpts.AccessTokenExpiryMinutes)
        }));
    }

    /// <summary>Get the current authenticated user's profile.</summary>
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize(
        AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Me()
    {
        var userIdStr = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token."));

        var user = await _userMgr.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User not found."));

        var roles = await _userMgr.GetRolesAsync(user);

        return Ok(ApiResponse<UserProfileResponse>.Ok(new UserProfileResponse
        {
            UserId = userId,
            Email = user.Email!,
            DisplayName = user.DisplayName ?? user.Email!,
            CompanyId = user.CompanyId,
            Roles = roles.ToList()
        }));
    }

    /// <summary>Revoke all refresh tokens for current user (logout everywhere).</summary>
    [HttpPost("logout")]
    [Microsoft.AspNetCore.Authorization.Authorize(
        AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout()
    {
        var userIdStr = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdStr, out var userId))
        {
            var tokens = await _db.RefreshTokens
                .Where(r => r.UserId == userId && !r.IsRevoked)
                .ToListAsync();
            foreach (var t in tokens) t.IsRevoked = true;
            await _db.SaveChangesAsync();
        }

        return Ok(ApiResponse<string>.Ok("Logged out successfully."));
    }
}
