using System.Net;

namespace RetailERP.Tests;

/// <summary>
/// Integration test exercising the Development-only Swagger endpoint.
/// Uses a derived factory that sets ASPNETCORE_ENVIRONMENT to Development
/// so the Swagger middleware is enabled.
/// </summary>
public class SwaggerIntegrationTests : IClassFixture<DevelopmentWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SwaggerIntegrationTests(DevelopmentWebApplicationFactory factory)
        => _client = factory.CreateClient();

    /// <summary>
    /// GET /swagger/v1/swagger.json returns 200 and body contains "openapi"
    /// (proves Swagger gen is wired and controllers are discoverable).
    /// </summary>
    [Fact]
    public async Task SwaggerJson_InDevelopment_ReturnsOkWithOpenApi()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("openapi", body, StringComparison.OrdinalIgnoreCase);
    }
}
