using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Entities;
using RetailERP.Data.Identity;
using RetailERP.Services;

namespace RetailERP.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ITenantProvider? _tenant;

    // Properties EF Core uses to parameterise global query filters per-request.
    public bool TenantIsNull => _tenant is null;
    public bool TenantIsSuperAdmin => _tenant?.IsSuperAdmin ?? false;
    public Guid? TenantCompanyId => _tenant?.CompanyId;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider? tenant = null)
        : base(options)
    {
        _tenant = tenant;
    }
    // ERP Tables
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Phase 3 – POS Billing
    public DbSet<PosBill> PosBills => Set<PosBill>();
    public DbSet<PosBillLine> PosBillLines => Set<PosBillLine>();

    // Phase 4 – Payments
    public DbSet<Payment> Payments => Set<Payment>();

    // Phase 5 – Returns
    public DbSet<PosReturn> PosReturns => Set<PosReturn>();
    public DbSet<PosReturnLine> PosReturnLines => Set<PosReturnLine>();

    // Phase 6 – Loyalty + Coupons
    public DbSet<LoyaltyCard> LoyaltyCards => Set<LoyaltyCard>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();

    // Phase 7 – EOD
    public DbSet<EodReport> EodReports => Set<EodReport>();

    // Phase 8 – Offline Sync
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    // Sprint 3 – Dashboard Layouts
    public DbSet<UserDashboardLayout> UserDashboardLayouts => Set<UserDashboardLayout>();

    // Sprint 5 – JWT Refresh Tokens
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Sprint 6 – Bill Templates
    public DbSet<BillTemplate> BillTemplates => Set<BillTemplate>();

    // Sprint 7 – Promotions
    public DbSet<Promotion> Promotions => Set<Promotion>();

    // Sprint 8 – GST E-Invoice & E-Way Bill
    public DbSet<EInvoice> EInvoices => Set<EInvoice>();
    public DbSet<EWayBill> EWayBills => Set<EWayBill>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---- Company ----
        builder.Entity<Company>()
            .HasIndex(x => x.Code)
            .IsUnique();

        // ---- Unique rules (from your data dictionary) ----
        // Sprint 4: unique indexes are now composite with CompanyId
        // so different tenants can have the same SKU, name, code, etc.
        builder.Entity<Item>()
            .HasIndex(x => new { x.SKU, x.CompanyId })
            .IsUnique();

        // DMART Phase 1: barcode is optional for now; enforce uniqueness when provided.
        builder.Entity<Item>()
            .HasIndex(x => new { x.Barcode, x.CompanyId })
            .IsUnique()
            .HasFilter("[Barcode] IS NOT NULL");

        builder.Entity<Unit>()
            .HasIndex(x => new { x.Name, x.CompanyId })
            .IsUnique();

        builder.Entity<Category>()
            .HasIndex(x => new { x.Name, x.CompanyId })
            .IsUnique();

        builder.Entity<Store>()
            .HasIndex(x => new { x.StoreCode, x.CompanyId })
            .IsUnique();

        // Sprint 6: Only one default template per type per company
        builder.Entity<BillTemplate>()
            .HasIndex(x => new { x.CompanyId, x.TemplateType, x.IsDefault })
            .HasFilter("[IsDefault] = 1")
            .IsUnique();

        builder.Entity<Warehouse>()
            .HasIndex(x => new { x.Name, x.CompanyId })
            .IsUnique();

        builder.Entity<Supplier>()
            .HasIndex(x => new { x.Name, x.CompanyId })
            .IsUnique();

        builder.Entity<Employee>()
            .HasIndex(x => x.EmployeeCode)
            .IsUnique();

        builder.Entity<Employee>()
            .ToTable(t => t.HasCheckConstraint("CK_Employees_Status", "[Status] IN (1,2,3,4)"));

        builder.Entity<Purchase>()
            .HasIndex(x => new { x.PurchaseNo, x.CompanyId })
            .IsUnique();

        builder.Entity<Invoice>()
            .HasIndex(x => new { x.InvoiceNo, x.CompanyId })
            .IsUnique();

        builder.Entity<Invoice>()
    .HasOne(x => x.Warehouse)
    .WithMany()
    .HasForeignKey(x => x.WarehouseId)
    .OnDelete(DeleteBehavior.Restrict);

        // Optional: connect invoice/purchase to employee for attribution
        builder.Entity<Invoice>()
            .HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Purchase>()
            .HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional: connect audit logs to identity users for "who did it" tracing
        builder.Entity<AuditLog>()
            .HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---- Status constraints (professional safety) ----
        builder.Entity<Invoice>()
            .ToTable(t => t.HasCheckConstraint("CK_Invoices_Status", "[Status] IN (1,2)"));

        builder.Entity<Purchase>()
            .ToTable(t => t.HasCheckConstraint("CK_Purchases_Status", "[Status] IN (1,2)"));

        builder.Entity<Stock>()
            .ToTable(t => t.HasCheckConstraint("CK_Stocks_Quantity", "[Quantity] >= 0"));

        builder.Entity<Purchase>()
            .HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Purchase>()
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

        builder.Entity<PurchaseLine>()
            .HasOne(x => x.Purchase)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PurchaseLine>()
            .HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Phase 1 relationships (safe, optional) ---
        builder.Entity<Item>()
            .HasOne(x => x.Unit)
            .WithMany()
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Item>()
            .HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Category>()
            .HasOne(x => x.ParentCategory)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Warehouse>()
            .HasOne(x => x.Store)
            .WithMany()
            .HasForeignKey(x => x.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---- Stock Movement ledger ----
        builder.Entity<StockMovement>()
            .HasIndex(x => new { x.WarehouseId, x.ItemId, x.OccurredAtUtc });

        builder.Entity<StockMovement>()
            .ToTable(t => t.HasCheckConstraint("CK_StockMovements_QtyNonZero", "[QuantityChange] <> 0"));

        builder.Entity<StockMovement>()
            .HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StockMovement>()
            .HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StockMovement>()
            .HasOne(x => x.Purchase)
            .WithMany()
            .HasForeignKey(x => x.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StockMovement>()
            .HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---- DMART Phase 2: Stock Transaction ledger ----
        builder.Entity<StockTransaction>()
            .HasIndex(x => new { x.WarehouseId, x.ItemId, x.OccurredAtUtc });

        builder.Entity<StockTransaction>()
            .ToTable(t => t.HasCheckConstraint("CK_StockTransactions_QtyNonZero", "[Qty] <> 0"));

        builder.Entity<StockTransaction>()
            .ToTable(t => t.HasCheckConstraint("CK_StockTransactions_Type", "[Type] IN ('IN','OUT','ADJUSTMENT','TRANSFER','RETURN')"));

        builder.Entity<StockTransaction>()
            .HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StockTransaction>()
            .HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StockTransaction>()
            .HasOne(x => x.Store)
            .WithMany()
            .HasForeignKey(x => x.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StockTransaction>()
            .HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ═══════════════════════════════════════════════════════════
        // Phase 3 – POS Billing
        // ═══════════════════════════════════════════════════════════
        builder.Entity<PosBill>()
            .HasIndex(x => new { x.BillNo, x.CompanyId })
            .IsUnique();

        builder.Entity<PosBill>()
            .ToTable(t => t.HasCheckConstraint("CK_PosBills_Status", "[Status] IN (1,2,3,4)"));

        builder.Entity<PosBill>()
            .HasOne(x => x.Store)
            .WithMany()
            .HasForeignKey(x => x.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosBill>()
            .HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosBill>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosBill>()
            .HasOne(x => x.CashierUser)
            .WithMany()
            .HasForeignKey(x => x.CashierUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosBillLine>()
            .HasOne(x => x.PosBill)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.PosBillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosBillLine>()
            .HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // ═══════════════════════════════════════════════════════════
        // Phase 4 – Payments
        // ═══════════════════════════════════════════════════════════
        builder.Entity<Payment>()
            .ToTable(t => t.HasCheckConstraint("CK_Payments_Method", "[Method] IN ('Cash','Card','UPI','Other')"));

        builder.Entity<Payment>()
            .HasOne(x => x.PosBill)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.PosBillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Payment>()
            .HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Payment>()
            .HasOne(x => x.PosReturn)
            .WithMany()
            .HasForeignKey(x => x.PosReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        // ═══════════════════════════════════════════════════════════
        // Phase 5 – Returns & Refunds
        // ═══════════════════════════════════════════════════════════
        builder.Entity<PosReturn>()
            .HasIndex(x => new { x.ReturnNo, x.CompanyId })
            .IsUnique();

        builder.Entity<PosReturn>()
            .ToTable(t => t.HasCheckConstraint("CK_PosReturns_Status", "[Status] IN (1,2)"));

        builder.Entity<PosReturn>()
            .HasOne(x => x.OriginalBill)
            .WithMany()
            .HasForeignKey(x => x.OriginalBillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosReturn>()
            .HasOne(x => x.Store)
            .WithMany()
            .HasForeignKey(x => x.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosReturn>()
            .HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosReturn>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosReturn>()
            .HasOne(x => x.ProcessedByUser)
            .WithMany()
            .HasForeignKey(x => x.ProcessedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosReturnLine>()
            .HasOne(x => x.PosReturn)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.PosReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosReturnLine>()
            .HasOne(x => x.OriginalBillLine)
            .WithMany()
            .HasForeignKey(x => x.OriginalBillLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PosReturnLine>()
            .HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // ═══════════════════════════════════════════════════════════
        // Phase 6 – Loyalty + Coupons
        // ═══════════════════════════════════════════════════════════
        builder.Entity<LoyaltyCard>()
            .HasIndex(x => new { x.CardNumber, x.CompanyId })
            .IsUnique();

        builder.Entity<LoyaltyCard>()
            .HasIndex(x => x.CustomerId)
            .IsUnique(); // one card per customer

        builder.Entity<LoyaltyCard>()
            .ToTable(t => t.HasCheckConstraint("CK_LoyaltyCards_Tier", "[Tier] IN (1,2,3,4)"));

        builder.Entity<LoyaltyCard>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LoyaltyTransaction>()
            .ToTable(t => t.HasCheckConstraint("CK_LoyaltyTransactions_Type", "[Type] IN ('Earn','Redeem')"));

        builder.Entity<LoyaltyTransaction>()
            .HasOne(x => x.LoyaltyCard)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.LoyaltyCardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LoyaltyTransaction>()
            .HasOne(x => x.PosBill)
            .WithMany()
            .HasForeignKey(x => x.PosBillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Coupon>()
            .HasIndex(x => new { x.Code, x.CompanyId })
            .IsUnique();

        builder.Entity<Coupon>()
            .ToTable(t => t.HasCheckConstraint("CK_Coupons_DiscountType", "[DiscountType] IN ('Percent','Flat')"));

        builder.Entity<CouponUsage>()
            .HasOne(x => x.Coupon)
            .WithMany(x => x.Usages)
            .HasForeignKey(x => x.CouponId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CouponUsage>()
            .HasOne(x => x.PosBill)
            .WithMany()
            .HasForeignKey(x => x.PosBillId)
            .OnDelete(DeleteBehavior.Restrict);

        // PosBill → LoyaltyCard FK
        builder.Entity<PosBill>()
            .HasOne(x => x.LoyaltyCard)
            .WithMany()
            .HasForeignKey(x => x.LoyaltyCardId)
            .OnDelete(DeleteBehavior.Restrict);

        // PosBill → Coupon FK
        builder.Entity<PosBill>()
            .HasOne(x => x.Coupon)
            .WithMany()
            .HasForeignKey(x => x.CouponId)
            .OnDelete(DeleteBehavior.Restrict);

        // ═══════════════════════════════════════════════════════════
        // Phase 7 – EOD
        // ═══════════════════════════════════════════════════════════
        builder.Entity<EodReport>()
            .HasIndex(x => new { x.StoreId, x.ReportDate })
            .IsUnique();

        builder.Entity<EodReport>()
            .ToTable(t => t.HasCheckConstraint("CK_EodReports_Status", "[Status] IN (1,2)"));

        builder.Entity<EodReport>()
            .HasOne(x => x.Store)
            .WithMany()
            .HasForeignKey(x => x.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<EodReport>()
            .HasOne(x => x.ClosedByUser)
            .WithMany()
            .HasForeignKey(x => x.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ═══════════════════════════════════════════════════════════
        // Phase 8 – Offline Sync
        // ═══════════════════════════════════════════════════════════
        builder.Entity<SyncLog>()
            .HasIndex(x => new { x.DeviceId, x.Status });

        builder.Entity<SyncLog>()
            .ToTable(t => t.HasCheckConstraint("CK_SyncLogs_Status", "[Status] IN (1,2,3)"));

        builder.Entity<SyncLog>()
            .ToTable(t => t.HasCheckConstraint("CK_SyncLogs_Action", "[Action] IN ('Create','Update','Delete')"));

        // ---- Auditing user FK (optional) ----
        ConfigureAuditUserFks<Item>(builder);
        ConfigureAuditUserFks<Unit>(builder);
        ConfigureAuditUserFks<Category>(builder);
        ConfigureAuditUserFks<Store>(builder);
        ConfigureAuditUserFks<Warehouse>(builder);
        ConfigureAuditUserFks<Stock>(builder);
        ConfigureAuditUserFks<Customer>(builder);
        ConfigureAuditUserFks<Supplier>(builder);
        ConfigureAuditUserFks<Purchase>(builder);
        ConfigureAuditUserFks<PurchaseLine>(builder);
        ConfigureAuditUserFks<Invoice>(builder);
        ConfigureAuditUserFks<InvoiceLine>(builder);
        ConfigureAuditUserFks<StockMovement>(builder);
        ConfigureAuditUserFks<StockTransaction>(builder);
        ConfigureAuditUserFks<PosBill>(builder);
        ConfigureAuditUserFks<PosBillLine>(builder);
        ConfigureAuditUserFks<Payment>(builder);
        ConfigureAuditUserFks<PosReturn>(builder);
        ConfigureAuditUserFks<PosReturnLine>(builder);
        ConfigureAuditUserFks<LoyaltyCard>(builder);
        ConfigureAuditUserFks<LoyaltyTransaction>(builder);
        ConfigureAuditUserFks<Coupon>(builder);
        ConfigureAuditUserFks<CouponUsage>(builder);
        ConfigureAuditUserFks<EodReport>(builder);
        ConfigureAuditUserFks<SyncLog>(builder);

        ConfigureAuditDefaults<Item>(builder);
        ConfigureAuditDefaults<Unit>(builder);
        ConfigureAuditDefaults<Category>(builder);
        ConfigureAuditDefaults<Store>(builder);
        ConfigureAuditDefaults<Warehouse>(builder);
        ConfigureAuditDefaults<Stock>(builder);
        ConfigureAuditDefaults<Customer>(builder);
        ConfigureAuditDefaults<Supplier>(builder);
        ConfigureAuditDefaults<Purchase>(builder);
        ConfigureAuditDefaults<PurchaseLine>(builder);
        ConfigureAuditDefaults<Invoice>(builder);
        ConfigureAuditDefaults<InvoiceLine>(builder);
        ConfigureAuditDefaults<StockMovement>(builder);
        ConfigureAuditDefaults<StockTransaction>(builder);
        ConfigureAuditDefaults<PosBill>(builder);
        ConfigureAuditDefaults<PosBillLine>(builder);
        ConfigureAuditDefaults<Payment>(builder);
        ConfigureAuditDefaults<PosReturn>(builder);
        ConfigureAuditDefaults<PosReturnLine>(builder);
        ConfigureAuditDefaults<LoyaltyCard>(builder);
        ConfigureAuditDefaults<LoyaltyTransaction>(builder);
        ConfigureAuditDefaults<Coupon>(builder);
        ConfigureAuditDefaults<CouponUsage>(builder);
        ConfigureAuditDefaults<EodReport>(builder);
        ConfigureAuditDefaults<SyncLog>(builder);

        // ---- Company FK (optional) ----
        ConfigureCompanyFk<Item>(builder);
        ConfigureCompanyFk<Unit>(builder);
        ConfigureCompanyFk<Category>(builder);
        ConfigureCompanyFk<Store>(builder);
        ConfigureCompanyFk<Warehouse>(builder);
        ConfigureCompanyFk<Stock>(builder);
        ConfigureCompanyFk<Customer>(builder);
        ConfigureCompanyFk<Supplier>(builder);
        ConfigureCompanyFk<Purchase>(builder);
        ConfigureCompanyFk<Invoice>(builder);
        ConfigureCompanyFk<StockMovement>(builder);
        ConfigureCompanyFk<StockTransaction>(builder);
        ConfigureCompanyFk<PosBill>(builder);
        ConfigureCompanyFk<Payment>(builder);
        ConfigureCompanyFk<PosReturn>(builder);
        ConfigureCompanyFk<LoyaltyCard>(builder);
        ConfigureCompanyFk<LoyaltyTransaction>(builder);
        ConfigureCompanyFk<Coupon>(builder);
        ConfigureCompanyFk<EodReport>(builder);
        ConfigureCompanyFk<SyncLog>(builder);
        ConfigureCompanyFk<Promotion>(builder);

        // ═══════════════════════════════════════════════════════════
        // Sprint 7 – Promotions
        // ═══════════════════════════════════════════════════════════
        builder.Entity<Promotion>()
            .HasIndex(x => new { x.CompanyId, x.Name })
            .IsUnique();

        builder.Entity<Promotion>()
            .ToTable(t => t.HasCheckConstraint("CK_Promotions_PromoType",
                "[PromoType] IN ('FlatPercent','FlatAmount','BOGO','BuyXGetY','ComboDiscount','HappyHour')"));

        builder.Entity<Promotion>()
            .HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Promotion>()
            .HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Promotion>()
            .HasOne(x => x.FreeItem)
            .WithMany()
            .HasForeignKey(x => x.FreeItemId)
            .OnDelete(DeleteBehavior.Restrict);

        ConfigureAuditUserFks<Promotion>(builder);
        ConfigureAuditDefaults<Promotion>(builder);

        // ═══════════════════════════════════════════════════════════
        // Sprint 8 – GST E-Invoice & E-Way Bill
        // ═══════════════════════════════════════════════════════════
        builder.Entity<EInvoice>()
            .HasIndex(x => x.Irn)
            .IsUnique();

        builder.Entity<EInvoice>()
            .ToTable(t => t.HasCheckConstraint("CK_EInvoices_Status", "[Status] IN (1,2,3)"));

        builder.Entity<EInvoice>()
            .HasOne(x => x.PosBill)
            .WithMany()
            .HasForeignKey(x => x.PosBillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<EInvoice>()
            .HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<EWayBill>()
            .HasIndex(x => x.EwbNo)
            .IsUnique();

        builder.Entity<EWayBill>()
            .ToTable(t => t.HasCheckConstraint("CK_EWayBills_Status", "[Status] IN (1,2,3)"));

        builder.Entity<EWayBill>()
            .HasOne(x => x.PosBill)
            .WithMany()
            .HasForeignKey(x => x.PosBillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<EWayBill>()
            .HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        ConfigureAuditUserFks<EInvoice>(builder);
        ConfigureAuditDefaults<EInvoice>(builder);
        ConfigureCompanyFk<EInvoice>(builder);
        ConfigureAuditUserFks<EWayBill>(builder);
        ConfigureAuditDefaults<EWayBill>(builder);
        ConfigureCompanyFk<EWayBill>(builder);

        // ═══════════════════════════════════════════════════════════
        // Sprint 3 – Dashboard Layouts
        // ═══════════════════════════════════════════════════════════
        builder.Entity<UserDashboardLayout>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        builder.Entity<UserDashboardLayout>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserDashboardLayout>()
            .Property(x => x.LastModifiedUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        // ═══════════════════════════════════════════════════════════
        // Sprint 4 – Multi-tenant global query filters
        // ═══════════════════════════════════════════════════════════
        // When a tenant is set and user is NOT SuperAdmin, every ITenantEntity
        // query is automatically filtered to only show rows for that CompanyId.
        // SuperAdmin (tenant == null OR IsSuperAdmin) sees everything.
        ApplyTenantFilters(builder);

        // ---- Matching query filters for dependent/line-item entities ----
        // These entities don't own a CompanyId; filter through their parent
        // to silence EF Core warning 10622.
        builder.Entity<InvoiceLine>().HasQueryFilter(
            e => TenantIsNull || TenantIsSuperAdmin || e.Invoice!.CompanyId == TenantCompanyId);
        builder.Entity<PurchaseLine>().HasQueryFilter(
            e => TenantIsNull || TenantIsSuperAdmin || e.Purchase!.CompanyId == TenantCompanyId);
        builder.Entity<PosBillLine>().HasQueryFilter(
            e => TenantIsNull || TenantIsSuperAdmin || e.PosBill!.CompanyId == TenantCompanyId);
        builder.Entity<PosReturnLine>().HasQueryFilter(
            e => TenantIsNull || TenantIsSuperAdmin || e.PosReturn!.CompanyId == TenantCompanyId);
        builder.Entity<CouponUsage>().HasQueryFilter(
            e => TenantIsNull || TenantIsSuperAdmin || e.Coupon!.CompanyId == TenantCompanyId);
    }

    /// <summary>
    /// Sprint 4 – Apply global query filter to every entity implementing ITenantEntity.
    /// Uses a lambda closure so EF Core can parameterise correctly per-request.
    /// </summary>
    private void ApplyTenantFilters(ModelBuilder builder)
    {
        var method = GetType().GetMethod(nameof(ApplyTenantFilter),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            method.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { builder });
        }
    }

    private void ApplyTenantFilter<T>(ModelBuilder builder) where T : class, ITenantEntity
    {
        builder.Entity<T>().HasQueryFilter(
            e => TenantIsNull || TenantIsSuperAdmin || e.CompanyId == TenantCompanyId);
    }

    // ═══════════════════════════════════════════════════════════
    // Sprint 4 – Auto-stamp CompanyId on new entities
    // ═══════════════════════════════════════════════════════════
    public override int SaveChanges(bool acceptChangesOnSuccess)
    {
        StampTenantOnNewEntities();
        return base.SaveChanges(acceptChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenantOnNewEntities();
        return base.SaveChangesAsync(acceptChangesOnSuccess, cancellationToken);
    }

    private void StampTenantOnNewEntities()
    {
        if (_tenant is null || _tenant.IsSuperAdmin || _tenant.CompanyId is null) return;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CompanyId is null)
            {
                entry.Entity.CompanyId = _tenant.CompanyId;
            }
        }
    }

    private static void ConfigureAuditUserFks<TEntity>(ModelBuilder builder)
        where TEntity : class
    {
        builder.Entity<TEntity>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey("CreatedByUserId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TEntity>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey("UpdatedByUserId")
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCompanyFk<TEntity>(ModelBuilder builder)
        where TEntity : class
    {
        builder.Entity<TEntity>()
            .HasOne<Company>()
            .WithMany()
            .HasForeignKey("CompanyId")
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureAuditDefaults<TEntity>(ModelBuilder builder)
        where TEntity : class
    {
        builder.Entity<TEntity>()
            .Property<DateTime>("CreatedAtUtc")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Entity<TEntity>()
            .Property<DateTime>("UpdatedAtUtc")
            .HasDefaultValueSql("GETUTCDATE()");
    }
}