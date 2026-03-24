using System.Net;
using System.Text.Json;

namespace RetailERP.Tests;

/// <summary>
/// Integration tests that exercise the real HTTP pipeline via
/// <see cref="CustomWebApplicationFactory"/> (WebApplicationFactory).
/// </summary>
public class IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IntegrationTests(CustomWebApplicationFactory factory)
        => _client = factory.CreateClient(new()
        {
            // Don't follow redirects so we can assert 302 for auth tests.
            AllowAutoRedirect = false
        });

    /// <summary>GET /health returns 200 Healthy.</summary>
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>GET /health/ready returns 200 with JSON containing "status" key.</summary>
    [Fact]
    public async Task HealthReady_ReturnsJsonWithStatusKey()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("status", out _),
            "Expected JSON to contain a 'status' key");
    }

    /// <summary>
    /// Anonymous GET to / (protected by global FallbackPolicy
    /// RequireAuthenticatedUser) returns 302 redirect to a login page.
    /// Chosen because "/" is the default route and is always present.
    /// </summary>
    [Fact]
    public async Task ProtectedRoute_AnonymousGet_Returns302ToLogin()
    {
        var response = await _client.GetAsync("/");

        // FallbackPolicy + Cookie auth → 302 redirect
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    /// <summary>
    /// Verify CorrelationIdMiddleware: when caller sends X-Correlation-Id it is
    /// echoed back; when omitted a new non-empty GUID is generated.
    /// </summary>
    [Fact]
    public async Task Health_ReturnsCorrelationIdHeader_EchoAndGenerate()
    {
        // 1. Echo: send a known value and expect it back.
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "test-123");
        var echoResponse = await _client.SendAsync(request);
        Assert.True(echoResponse.Headers.TryGetValues("X-Correlation-Id", out var echoValues));
        Assert.Equal("test-123", echoValues!.First());

        // 2. Generate: omit header and expect a non-empty value back.
        var genResponse = await _client.GetAsync("/health");
        Assert.True(genResponse.Headers.TryGetValues("X-Correlation-Id", out var genValues));
        var generated = genValues!.First();
        Assert.False(string.IsNullOrWhiteSpace(generated), "Expected a generated correlation ID");
        Assert.NotEqual("test-123", generated); // must be a fresh value
    }

    /// <summary>
    /// Confirm Manager role receives 403 Forbidden (or AccessDenied redirect) on a SuperAdmin route.
    /// Addresses the "Admin role boundary" security requirement.
    /// </summary>
    [Fact]
    public async Task AdminBoundary_ManagerCannotAccessSuperAdminRoute()
    {
        // Setup client with a mock auth handler that injects "Manager" role
        using var factory = new CustomWebApplicationFactory();
        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(defaultScheme: "TestScheme")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                        "TestScheme", options => { });
            });
        }).CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("TestScheme");

        // Target a route restricted to [Authorize(Roles = "SuperAdmin")]
        var response = await client.GetAsync("/Companies");

        // MVC will issue a 403 Forbidden or redirect to AccessDenied 
        // depending on how the Authorization middleware is configured.
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected Forbid (403) or Redirect (302) to AccessDenied, got {response.StatusCode}");
        
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("AccessDenied", response.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }
}

public class TestAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public TestAuthHandler(Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options, 
        Microsoft.Extensions.Logging.ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { 
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "TestManager"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Manager") 
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestScheme");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}
