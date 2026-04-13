using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Entities;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Seed;

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

    public DbSeeder(
        ApplicationDbContext db,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task SeedAsync()
    {
        await _db.Database.MigrateAsync();

        // 1) Roles (including new SuperAdmin)
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

        // 2) Default company (tenant) — Sprint 4
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

        // 3) SuperAdmin user — retailerp.global@gmail.com
        await EnsureUserAsync(
            email: "retailerp.global@gmail.com",
            password: "SuperAdmin@12345",
            roles: [RoleSuperAdmin],
            companyId: null  // SuperAdmin sees all tenants
        );

        // 4) Demo users (tenant-scoped to default company)
        await EnsureUserAsync(
            email: "admin@retailerp.com",
            password: "Admin@12345",
            roles: [RoleAdmin, RoleManager],
            companyId: DefaultCompanyId
        );

        await EnsureUserAsync(
            email: "manager@retailerp.com",
            password: "Manager@12345",
            roles: [RoleManager],
            companyId: DefaultCompanyId
        );

        await EnsureUserAsync(
            email: "cashier@retailerp.com",
            password: "Cashier@12345",
            roles: [RoleCashier],
            companyId: DefaultCompanyId
        );

        await EnsureUserAsync(
            email: "inventory@retailerp.com",
            password: "Inventory@12345",
            roles: [RoleInventory],
            companyId: DefaultCompanyId
        );

        // 5) Backfill: set CompanyId on all existing rows that have null CompanyId
        await BackfillCompanyIdAsync();

        // 6) Demo master data (only if empty — use IgnoreQueryFilters so tenant filters don't hide existing rows)
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

        // 7) Stock (only if empty)
        if (!await _db.Stocks.IgnoreQueryFilters().AnyAsync())
        {
            var warehouses = await _db.Warehouses.IgnoreQueryFilters().OrderBy(x => x.Name).ToListAsync();
            var items = await _db.Items.IgnoreQueryFilters().OrderBy(x => x.SKU).ToListAsync();

            var main = warehouses.First();

            _db.Stocks.AddRange(
                new Stock { WarehouseId = main.WarehouseId, ItemId = items[0].ItemId, Quantity = 5 },
                new Stock { WarehouseId = main.WarehouseId, ItemId = items[1].ItemId, Quantity = 50 },
                new Stock { WarehouseId = main.WarehouseId, ItemId = items[2].ItemId, Quantity = 20 }
            );

            await _db.SaveChangesAsync();
        }
    }

    /// <summary>Sprint 4 – Backfill null CompanyId to DefaultCompanyId on all tenant entities.</summary>
    private async Task BackfillCompanyIdAsync()
    {
        // Use raw SQL for performance — updates all rows in one shot per table
        var tables = new[]
        {
            "Items", "Units", "Categories", "Stores", "Warehouses", "Stocks",
            "Customers", "Suppliers", "Purchases", "Invoices", "StockMovements",
            "StockTransactions", "PosBills", "Payments", "PosReturns",
            "LoyaltyCards", "LoyaltyTransactions", "Coupons", "EodReports", "SyncLogs"
        };

        // Use raw ADO.NET to avoid EF Core command-error logging on expected conflicts.
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
            // Let EF Core manage the connection lifetime; only close if we opened it.
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }

        // Also backfill users
#pragma warning disable EF1002
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE [AspNetUsers] SET [CompanyId] = {0} WHERE [CompanyId] IS NULL AND [Email] <> {1}",
            DefaultCompanyId, "retailerp.global@gmail.com");
#pragma warning restore EF1002
    }

    private async Task EnsureUserAsync(string email, string password, string[] roles, Guid? companyId)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
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
        }
        else
        {
            // Update CompanyId if changed
            if (user.CompanyId != companyId)
            {
                user.CompanyId = companyId;
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
            }

            var updated = await _userManager.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                var msg = string.Join("; ", updated.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"User update failed ({email}): {msg}");
            }
        }

        // Ensure user has exactly these roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removed = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removed.Succeeded)
            {
                var msg = string.Join("; ", removed.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Role remove failed ({email}): {msg}");
            }
        }

        var added = await _userManager.AddToRolesAsync(user, roles);
        if (!added.Succeeded)
        {
            var msg = string.Join("; ", added.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Role assign failed ({email}): {msg}");
        }
    }
}