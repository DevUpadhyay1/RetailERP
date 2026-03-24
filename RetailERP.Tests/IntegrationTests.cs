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
}
