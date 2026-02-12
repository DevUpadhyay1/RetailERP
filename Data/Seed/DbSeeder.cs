using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Entities;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Seed;

public sealed class DbSeeder
{
    public const string RoleAdmin = "Admin";
    public const string RoleManager = "Manager";
    public const string RoleCashier = "Cashier";
    public const string RoleInventory = "Inventory";

    // Optional (keep for future if you want)
    public const string RoleFinance = "Finance";
    public const string RoleHR = "HR";

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

        // 1) Roles (recommended set)
        string[] roles =
        [
            RoleAdmin, RoleManager, RoleCashier, RoleInventory,
            RoleFinance, RoleHR // optional
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

        // 2) Demo users (admin + one user per role)
        await EnsureUserAsync(
            email: "admin@retailerp.com",
            password: "Admin@12345",
            roles: [RoleAdmin, RoleManager] // admin also acts as manager
        );

        await EnsureUserAsync(
            email: "manager@retailerp.com",
            password: "Manager@12345",
            roles: [RoleManager]
        );

        await EnsureUserAsync(
            email: "cashier@retailerp.com",
            password: "Cashier@12345",
            roles: [RoleCashier]
        );

        await EnsureUserAsync(
            email: "inventory@retailerp.com",
            password: "Inventory@12345",
            roles: [RoleInventory]
        );

        // 3) Demo master data (only if empty)
        if (!await _db.Warehouses.AnyAsync())
        {
            _db.Warehouses.AddRange(
                new Warehouse { Name = "Main Warehouse", Address = "Ahmedabad" },
                new Warehouse { Name = "Store Warehouse", Address = "Surat" }
            );
            await _db.SaveChangesAsync();
        }

        if (!await _db.Items.AnyAsync())
        {
            _db.Items.AddRange(
                new Item { SKU = "SKU-001", Name = "Laptop", UnitPrice = 55000, ReorderLevel = 2, IsActive = true },
                new Item { SKU = "SKU-002", Name = "Mouse", UnitPrice = 499, ReorderLevel = 10, IsActive = true },
                new Item { SKU = "SKU-003", Name = "Keyboard", UnitPrice = 1299, ReorderLevel = 5, IsActive = true }
            );
            await _db.SaveChangesAsync();
        }

        if (!await _db.Customers.AnyAsync())
        {
            _db.Customers.AddRange(
                new Customer { Name = "Walk-in Customer", Phone = "9999999999", Email = "walkin@demo.com" },
                new Customer { Name = "ABC Traders", Phone = "8888888888", Email = "abc@demo.com" }
            );
            await _db.SaveChangesAsync();
        }

        // 4) Stock (only if empty)
        if (!await _db.Stocks.AnyAsync())
        {
            var warehouses = await _db.Warehouses.OrderBy(x => x.Name).ToListAsync();
            var items = await _db.Items.OrderBy(x => x.SKU).ToListAsync();

            var main = warehouses.First();

            _db.Stocks.AddRange(
                new Stock { WarehouseId = main.WarehouseId, ItemId = items[0].ItemId, Quantity = 5 },
                new Stock { WarehouseId = main.WarehouseId, ItemId = items[1].ItemId, Quantity = 50 },
                new Stock { WarehouseId = main.WarehouseId, ItemId = items[2].ItemId, Quantity = 20 }
            );

            await _db.SaveChangesAsync();
        }
    }

    private async Task EnsureUserAsync(string email, string password, string[] roles)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var created = await _userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                var msg = string.Join("; ", created.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"User create failed ({email}): {msg}");
            }
        }

        // Ensure user has exactly these roles (simple ERP approach)
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