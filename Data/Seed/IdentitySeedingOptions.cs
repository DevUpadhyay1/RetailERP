namespace RetailERP.Data.Seed;

public sealed class IdentitySeedingOptions
{
    public bool Enabled { get; set; } = true;
    public List<SeedUserOptions> EnvironmentUsers { get; set; } = [];
}

public sealed class SeedUserOptions
{
    public string Email { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
    public bool IsGlobal { get; set; }

    // Do not commit secrets in source control; use environment variables or secret providers.
    public string? PasswordEnvVar { get; set; }
}
