using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RetailERP.Data.Entities;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Seed;

/// <summary>
/// Production-grade database seeder.
/// - Roles and default company are always ensured.
/// - User accounts are driven by <see cref="IdentitySeedingOptions"/> in appsettings
///   (per-environment). No hardcoded passwords in source code.
/// - Demo master data is ONLY seeded in Development environment.
/// </summary>
public sealed class DbSeeder
{
    public const string RoleSuperAdmin = "SuperAdmin";
    public const string RoleAdmin = "Admin";
    public const string RoleManager = "Manager";
    public const string RoleCashier = "Cashier";
    public const string RoleInventory = "Inventory";
    public const string RoleFinance = "Finance";
    public const string RoleHR = "HR";

    /// <summary>Well-known default company id for backfilling existing data.</summary>
    public static readonly Guid DefaultCompanyId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ApplicationDbContext _db;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DbSeeder> _logger;
    private readonly IdentitySeedingOptions _seedingOptions;

    public DbSeeder(
        ApplicationDbContext db,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger<DbSeeder> logger,
        IOptions<IdentitySeedingOptions> seedingOptions)
    {
        _db = db;
        _roleManager = roleManager;
        _userManager = userManager;
        _logger = logger;
        _seedingOptions = seedingOptions.Value;
    }

    // ═══════════════════════════════════════════════════════════
    // Development seed — demo data + config-driven users
    // ═══════════════════════════════════════════════════════════
    public async Task SeedAsync()
    {
        await EnsureRolesAndDefaultCompanyAsync();
        await SeedConfigDrivenUsersAsync();
        await BackfillCompanyIdAsync();

        // Demo master data (Development only — only if tables are empty)
        await SeedDemoDataIfEmptyAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // Production seed — NO demo data, config-driven users only
    // ═══════════════════════════════════════════════════════════
    public async Task SeedProductionAsync()
    {
        await EnsureRolesAndDefaultCompanyAsync();
        await SeedConfigDrivenUsersAsync();
        await BackfillCompanyIdAsync();

        // No demo data in production — ever.
    }

    // ═══════════════════════════════════════════════════════════
    // Config-driven user seeding (reads from appsettings)
    // ═══════════════════════════════════════════════════════════
    private async Task SeedConfigDrivenUsersAsync()
    {
        if (!_seedingOptions.Enabled)
        {
            _logger.LogInformation("IdentitySeeding is disabled — skipping user bootstrap.");
            return;
        }

        if (_seedingOptions.EnvironmentUsers.Count == 0)
        {
            _logger.LogWarning("IdentitySeeding.EnvironmentUsers is empty — no users will be seeded.");
            return;
        }

        foreach (var userDef in _seedingOptions.EnvironmentUsers)
        {
            if (string.IsNullOrWhiteSpace(userDef.Email))
            {
                _logger.LogWarning("IdentitySeeding: skipping entry with empty email.");
                continue;
            }

            // Resolve password: env var takes priority, then inline (for dev only)
            string? password = null;
            if (!string.IsNullOrWhiteSpace(userDef.PasswordEnvVar))
            {
                password = Environment.GetEnvironmentVariable(userDef.PasswordEnvVar);
                if (string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogWarning(
                        "IdentitySeeding: env var '{EnvVar}' is not set for user {Email}. " +
                        "New user creation will fail; existing user role sync will still run.",
                        userDef.PasswordEnvVar, userDef.Email);
                }
            }

            // Fallback to inline password (only acceptable in Development)
            if (string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(userDef.Password))
            {
                password = userDef.Password;
            }

            var companyId = userDef.IsGlobal ? null : (Guid?)DefaultCompanyId;

            await EnsureUserAsync(userDef.Email.Trim(), password, userDef.Roles, companyId);
            _logger.LogInformation(
                "IdentitySeeding: ensured user {Email} with roles [{Roles}], IsGlobal={IsGlobal}",
                userDef.Email, string.Join(", ", userDef.Roles), userDef.IsGlobal);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Roles + default company (always runs)
    // ═══════════════════════════════════════════════════════════
    private async Task EnsureRolesAndDefaultCompanyAsync()
    {
        await _db.Database.MigrateAsync();

        string[] roles =
        [
            RoleSuperAdmin, RoleAdmin, RoleManager, RoleCashier, RoleInventory,
            RoleFinance, RoleHR
        ];

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                var created = await _roleManager.CreateAsync(new ApplicationRole { Name = role });
                if (!created.Succeeded)
                {
                    var msg = string.Join("; ", created.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Role create failed ({role}): {msg}");
                }
            }
        }

        var defaultCompany = await _db.Companies.FindAsync(DefaultCompanyId);
        if (defaultCompany is null)
        {
            defaultCompany = new Company
            {
                CompanyId = DefaultCompanyId,
                Code = "DEFAULT",
                Name = "Default Company",
                Email = "admin@retailerp.com",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.Companies.Add(defaultCompany);
            await _db.SaveChangesAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // User ensure (idempotent create-or-sync)
    // ═══════════════════════════════════════════════════════════
    private async Task EnsureUserAsync(string email, string? password, string[] roles, Guid? companyId)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning(
                    "Cannot create user {Email} — no password available. " +
                    "Set the PasswordEnvVar in IdentitySeeding config.",
                    email);
                return;
            }

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CompanyId = companyId
            };

            var created = await _userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                var msg = string.Join("; ", created.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"User create failed ({email}): {msg}");
            }

            _logger.LogInformation("Created new user {Email}", email);
        }
        else
        {
            // Update CompanyId if changed
            if (user.CompanyId != companyId)
                user.CompanyId = companyId;

            if (!user.EmailConfirmed)
                user.EmailConfirmed = true;

            var updated = await _userManager.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                var msg = string.Join("; ", updated.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"User update failed ({email}): {msg}");
            }
        }

        // Keep seeded accounts usable after prior failed-attempt lockouts.
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, null);

        // Ensure user has exactly these roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(roles, StringComparer.OrdinalIgnoreCase).ToList();
        var rolesToAdd = roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();

        if (rolesToRemove.Count > 0)
        {
            var removed = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removed.Succeeded)
            {
                var msg = string.Join("; ", removed.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Role remove failed ({email}): {msg}");
            }
        }

        if (rolesToAdd.Count > 0)
        {
            var added = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!added.Succeeded)
            {
                var msg = string.Join("; ", added.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Role assign failed ({email}): {msg}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Backfill CompanyId on all tenant entities
    // ═══════════════════════════════════════════════════════════
    /// <summary>Sprint 4 – Backfill null CompanyId to DefaultCompanyId on all tenant entities.</summary>
    private async Task BackfillCompanyIdAsync()
    {
        var tables = new[]
        {
            "Items", "Units", "Categories", "Stores", "Warehouses", "Stocks",
            "Customers", "Suppliers", "Purchases", "Invoices", "StockMovements",
            "StockTransactions", "PosBills", "Payments", "PosReturns",
            "LoyaltyCards", "LoyaltyTransactions", "Coupons", "EodReports", "SyncLogs"
        };

        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            foreach (var table in tables)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"UPDATE [{table}] SET [CompanyId] = @cid WHERE [CompanyId] IS NULL";
                var p = cmd.CreateParameter();
                p.ParameterName = "@cid";
                p.Value = DefaultCompanyId;
                cmd.Parameters.Add(p);
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    // Duplicate-key conflict — skip gracefully; data already has CompanyId.
                }
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }

        // Also backfill users (non-SuperAdmin users get default company)
#pragma warning disable EF1002
        await _db.Database.ExecuteSqlRawAsync(
            @"UPDATE u
              SET u.[CompanyId] = {0}
              FROM [AspNetUsers] u
              WHERE u.[CompanyId] IS NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM [AspNetUserRoles] ur
                  INNER JOIN [AspNetRoles] r ON ur.[RoleId] = r.[Id]
                  WHERE ur.[UserId] = u.[Id] AND r.[Name] = {1}
              )",
            DefaultCompanyId,
            RoleSuperAdmin);
#pragma warning restore EF1002
    }

    // ═══════════════════════════════════════════════════════════
    // Demo data (Development only)
    // ═══════════════════════════════════════════════════════════
    private async Task SeedDemoDataIfEmptyAsync()
    {
        if (!await _db.Warehouses.IgnoreQueryFilters().AnyAsync())
        {
            _db.Warehouses.AddRange(
                new Warehouse { Name = "Main Warehouse", Address = "Ahmedabad" },
                new Warehouse { Name = "Store Warehouse", Address = "Surat" }
            );
            await _db.SaveChangesAsync();
        }

        if (!await _db.Items.IgnoreQueryFilters().AnyAsync())
        {
            _db.Items.AddRange(
                new Item { SKU = "SKU-001", Name = "Laptop", UnitPrice = 55000, ReorderLevel = 2, IsActive = true },
                new Item { SKU = "SKU-002", Name = "Mouse", UnitPrice = 499, ReorderLevel = 10, IsActive = true },
                new Item { SKU = "SKU-003", Name = "Keyboard", UnitPrice = 1299, ReorderLevel = 5, IsActive = true }
            );
            await _db.SaveChangesAsync();
        }

        if (!await _db.Customers.IgnoreQueryFilters().AnyAsync())
        {
            _db.Customers.AddRange(
                new Customer { Name = "Walk-in Customer", Phone = "9999999999", Email = "walkin@demo.com" },
                new Customer { Name = "ABC Traders", Phone = "8888888888", Email = "abc@demo.com" }
            );
            await _db.SaveChangesAsync();
        }

        if (!await _db.Suppliers.IgnoreQueryFilters().AnyAsync())
        {
            _db.Suppliers.AddRange(
                new Supplier { Name = "ABC Distributors", Phone = "7777777777", Email = "abc@suppliers.com", Address = "Ahmedabad", IsActive = true },
                new Supplier { Name = "Local Wholesale", Phone = "6666666666", Email = "local@suppliers.com", Address = "Surat", IsActive = true }
            );
            await _db.SaveChangesAsync();
        }

        if (!await _db.Stocks.IgnoreQueryFilters().AnyAsync())
        {
            var warehouses = await _db.Warehouses.IgnoreQueryFilters().OrderBy(x => x.Name).ToListAsync();
            var items = await _db.Items.IgnoreQueryFilters().OrderBy(x => x.SKU).ToListAsync();

            if (warehouses.Count > 0 && items.Count >= 3)
            {
                var main = warehouses.First();
                _db.Stocks.AddRange(
                    new Stock { WarehouseId = main.WarehouseId, ItemId = items[0].ItemId, Quantity = 5 },
                    new Stock { WarehouseId = main.WarehouseId, ItemId = items[1].ItemId, Quantity = 50 },
                    new Stock { WarehouseId = main.WarehouseId, ItemId = items[2].ItemId, Quantity = 20 }
                );
                await _db.SaveChangesAsync();
            }
        }
    }
}