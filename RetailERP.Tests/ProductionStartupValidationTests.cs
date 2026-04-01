using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using RetailERP.Infrastructure.Production;

namespace RetailERP.Tests;

public class ProductionStartupValidationTests
{
    [Fact]
    public void ThrowIfInvalidForProduction_ShouldNotThrow_WhenEnvironmentIsNotProduction()
    {
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var ex = Record.Exception(() => ProductionStartupValidation.ThrowIfInvalidForProduction(env, config));

        Assert.Null(ex);
    }

    [Fact]
    public void ThrowIfInvalidForProduction_ShouldThrow_WhenConnectionStringMissing()
    {
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "ThisIsAVeryLongSecretKeyForProd1234567890"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProductionStartupValidation.ThrowIfInvalidForProduction(env, config));

        Assert.Contains("ConnectionStrings:DefaultConnection", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThrowIfInvalidForProduction_ShouldThrow_WhenJwtSecretTooShort()
    {
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=RetailERP;Trusted_Connection=True;",
                ["Jwt:SecretKey"] = "short-secret"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProductionStartupValidation.ThrowIfInvalidForProduction(env, config));

        Assert.Contains("at least 32 characters", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThrowIfInvalidForProduction_ShouldThrow_WhenJwtContainsKnownDevMarker()
    {
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=RetailERP;Trusted_Connection=True;",
                ["Jwt:SecretKey"] = "RetailERP_Sprint5_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProductionStartupValidation.ThrowIfInvalidForProduction(env, config));

        Assert.Contains("development marker", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThrowIfInvalidForProduction_ShouldNotThrow_WhenSettingsAreValid()
    {
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=RetailERP;Trusted_Connection=True;",
                ["Jwt:SecretKey"] = "ThisIsAVeryLongSecretKeyForProd1234567890",
                ["AllowedHosts"] = "retailerp.example.com"
            })
            .Build();

        var ex = Record.Exception(() => ProductionStartupValidation.ThrowIfInvalidForProduction(env, config));

        Assert.Null(ex);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "RetailERP.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
