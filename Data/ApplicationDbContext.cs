using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Entities;
using RetailERP.Data.Identity;

namespace RetailERP.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    // ERP Tables
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Stock> Stocks => Set<Stock>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---- Unique rules (from your data dictionary) ----
        builder.Entity<Item>()
            .HasIndex(x => x.SKU)
            .IsUnique();

        builder.Entity<Warehouse>()
            .HasIndex(x => x.Name)
            .IsUnique();

        builder.Entity<Employee>()
            .HasIndex(x => x.EmployeeCode)
            .IsUnique();

        builder.Entity<Invoice>()
    .HasOne(x => x.Warehouse)
    .WithMany()
    .HasForeignKey(x => x.WarehouseId)
    .OnDelete(DeleteBehavior.Restrict);

        // Stock: one row per (Item, Warehouse)
        builder.Entity<Stock>()
            .HasIndex(x => new { x.ItemId, x.WarehouseId })
            .IsUnique();

        // ---- Relationships + delete behavior (ERP-safe) ----
        builder.Entity<Stock>()
            .HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Stock>()
            .HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Invoice>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InvoiceLine>()
            .HasOne(x => x.Invoice)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InvoiceLine>()
            .HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}