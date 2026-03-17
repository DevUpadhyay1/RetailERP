using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class Professionalize_Audit_StockMovements_Company : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Warehouses",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Warehouses",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Units",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Units",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Units",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Units",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Units",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Suppliers",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Suppliers",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Stores",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Stores",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Stores",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Stores",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Stores",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Stocks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Stocks",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Stocks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Stocks",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Stocks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Purchases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Purchases",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Purchases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Purchases",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Purchases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "PurchaseLines",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "PurchaseLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemNameSnapshot",
                table: "PurchaseLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemSkuSnapshot",
                table: "PurchaseLines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "PurchaseLines",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "PurchaseLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Items",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Items",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Invoices",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Invoices",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "InvoiceLines",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "InvoiceLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GstPercentSnapshot",
                table: "InvoiceLines",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HsnCodeSnapshot",
                table: "InvoiceLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemNameSnapshot",
                table: "InvoiceLines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemSkuSnapshot",
                table: "InvoiceLines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "InvoiceLines",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "InvoiceLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Customers",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Customers",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Categories",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Categories",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.CompanyId);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    StockMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityChange = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.StockMovementId);
                    table.CheckConstraint("CK_StockMovements_QtyNonZero", "[QuantityChange] <> 0");
                    table.ForeignKey(
                        name: "FK_StockMovements_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "PurchaseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_CompanyId",
                table: "Warehouses",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_CreatedByUserId",
                table: "Warehouses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_UpdatedByUserId",
                table: "Warehouses",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_CompanyId",
                table: "Units",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_CreatedByUserId",
                table: "Units",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_UpdatedByUserId",
                table: "Units",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CompanyId",
                table: "Suppliers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CreatedByUserId",
                table: "Suppliers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_UpdatedByUserId",
                table: "Suppliers",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CompanyId",
                table: "Stores",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CreatedByUserId",
                table: "Stores",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_UpdatedByUserId",
                table: "Stores",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_CompanyId",
                table: "Stocks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_CreatedByUserId",
                table: "Stocks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_UpdatedByUserId",
                table: "Stocks",
                column: "UpdatedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_Quantity",
                table: "Stocks",
                sql: "[Quantity] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_CompanyId",
                table: "Purchases",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_CreatedByUserId",
                table: "Purchases",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_UpdatedByUserId",
                table: "Purchases",
                column: "UpdatedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Purchases_Status",
                table: "Purchases",
                sql: "[Status] IN (1,2)");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLines_CreatedByUserId",
                table: "PurchaseLines",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseLines_UpdatedByUserId",
                table: "PurchaseLines",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CompanyId",
                table: "Items",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CreatedByUserId",
                table: "Items",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_UpdatedByUserId",
                table: "Items",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId",
                table: "Invoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreatedByUserId",
                table: "Invoices",
                column: "CreatedByUserId");

            // Some databases may already have a non-unique index with this name.
            // Drop it first to ensure we can create the unique index deterministically.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_InvoiceNo' AND object_id = OBJECT_ID(N'[dbo].[Invoices]'))
    DROP INDEX [IX_Invoices_InvoiceNo] ON [dbo].[Invoices];
");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNo",
                table: "Invoices",
                column: "InvoiceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UpdatedByUserId",
                table: "Invoices",
                column: "UpdatedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Status",
                table: "Invoices",
                sql: "[Status] IN (1,2)");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_CreatedByUserId",
                table: "InvoiceLines",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_UpdatedByUserId",
                table: "InvoiceLines",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId",
                table: "Customers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CreatedByUserId",
                table: "Customers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UpdatedByUserId",
                table: "Customers",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyId",
                table: "Categories",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CreatedByUserId",
                table: "Categories",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UpdatedByUserId",
                table: "Categories",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Code",
                table: "Companies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CompanyId",
                table: "StockMovements",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CreatedByUserId",
                table: "StockMovements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InvoiceId",
                table: "StockMovements",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId",
                table: "StockMovements",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PurchaseId",
                table: "StockMovements",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_UpdatedByUserId",
                table: "StockMovements",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WarehouseId_ItemId_OccurredAtUtc",
                table: "StockMovements",
                columns: new[] { "WarehouseId", "ItemId", "OccurredAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_AspNetUsers_CreatedByUserId",
                table: "Categories",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_AspNetUsers_UpdatedByUserId",
                table: "Categories",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Companies_CompanyId",
                table: "Categories",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_AspNetUsers_CreatedByUserId",
                table: "Customers",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_AspNetUsers_UpdatedByUserId",
                table: "Customers",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Companies_CompanyId",
                table: "Customers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLines_AspNetUsers_CreatedByUserId",
                table: "InvoiceLines",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLines_AspNetUsers_UpdatedByUserId",
                table: "InvoiceLines",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_AspNetUsers_CreatedByUserId",
                table: "Invoices",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_AspNetUsers_UpdatedByUserId",
                table: "Invoices",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Companies_CompanyId",
                table: "Invoices",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_AspNetUsers_CreatedByUserId",
                table: "Items",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_AspNetUsers_UpdatedByUserId",
                table: "Items",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Companies_CompanyId",
                table: "Items",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseLines_AspNetUsers_CreatedByUserId",
                table: "PurchaseLines",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseLines_AspNetUsers_UpdatedByUserId",
                table: "PurchaseLines",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_AspNetUsers_CreatedByUserId",
                table: "Purchases",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_AspNetUsers_UpdatedByUserId",
                table: "Purchases",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Companies_CompanyId",
                table: "Purchases",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_AspNetUsers_CreatedByUserId",
                table: "Stocks",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_AspNetUsers_UpdatedByUserId",
                table: "Stocks",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_Companies_CompanyId",
                table: "Stocks",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_AspNetUsers_CreatedByUserId",
                table: "Stores",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_AspNetUsers_UpdatedByUserId",
                table: "Stores",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Companies_CompanyId",
                table: "Stores",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_AspNetUsers_CreatedByUserId",
                table: "Suppliers",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_AspNetUsers_UpdatedByUserId",
                table: "Suppliers",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Companies_CompanyId",
                table: "Suppliers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Units_AspNetUsers_CreatedByUserId",
                table: "Units",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Units_AspNetUsers_UpdatedByUserId",
                table: "Units",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Units_Companies_CompanyId",
                table: "Units",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_AspNetUsers_CreatedByUserId",
                table: "Warehouses",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_AspNetUsers_UpdatedByUserId",
                table: "Warehouses",
                column: "UpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Companies_CompanyId",
                table: "Warehouses",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_AspNetUsers_CreatedByUserId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_AspNetUsers_UpdatedByUserId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Companies_CompanyId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_AspNetUsers_CreatedByUserId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_AspNetUsers_UpdatedByUserId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Companies_CompanyId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLines_AspNetUsers_CreatedByUserId",
                table: "InvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLines_AspNetUsers_UpdatedByUserId",
                table: "InvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_AspNetUsers_CreatedByUserId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_AspNetUsers_UpdatedByUserId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Companies_CompanyId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_AspNetUsers_CreatedByUserId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_AspNetUsers_UpdatedByUserId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Companies_CompanyId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseLines_AspNetUsers_CreatedByUserId",
                table: "PurchaseLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseLines_AspNetUsers_UpdatedByUserId",
                table: "PurchaseLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_AspNetUsers_CreatedByUserId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_AspNetUsers_UpdatedByUserId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Companies_CompanyId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_AspNetUsers_CreatedByUserId",
                table: "Stocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_AspNetUsers_UpdatedByUserId",
                table: "Stocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_Companies_CompanyId",
                table: "Stocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_AspNetUsers_CreatedByUserId",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_AspNetUsers_UpdatedByUserId",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Companies_CompanyId",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_AspNetUsers_CreatedByUserId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_AspNetUsers_UpdatedByUserId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Companies_CompanyId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Units_AspNetUsers_CreatedByUserId",
                table: "Units");

            migrationBuilder.DropForeignKey(
                name: "FK_Units_AspNetUsers_UpdatedByUserId",
                table: "Units");

            migrationBuilder.DropForeignKey(
                name: "FK_Units_Companies_CompanyId",
                table: "Units");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_AspNetUsers_CreatedByUserId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_AspNetUsers_UpdatedByUserId",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Companies_CompanyId",
                table: "Warehouses");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_CompanyId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_CreatedByUserId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_UpdatedByUserId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Units_CompanyId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Units_CreatedByUserId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Units_UpdatedByUserId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CompanyId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CreatedByUserId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_UpdatedByUserId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Stores_CompanyId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Stores_CreatedByUserId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Stores_UpdatedByUserId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_CompanyId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_CreatedByUserId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_UpdatedByUserId",
                table: "Stocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_Quantity",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_CompanyId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_CreatedByUserId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_UpdatedByUserId",
                table: "Purchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Purchases_Status",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseLines_CreatedByUserId",
                table: "PurchaseLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseLines_UpdatedByUserId",
                table: "PurchaseLines");

            migrationBuilder.DropIndex(
                name: "IX_Items_CompanyId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_CreatedByUserId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_UpdatedByUserId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CreatedByUserId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNo",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_UpdatedByUserId",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Status",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_CreatedByUserId",
                table: "InvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_UpdatedByUserId",
                table: "InvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CreatedByUserId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_UpdatedByUserId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CompanyId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CreatedByUserId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UpdatedByUserId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "ItemNameSnapshot",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "ItemSkuSnapshot",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "PurchaseLines");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "GstPercentSnapshot",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "HsnCodeSnapshot",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "ItemNameSnapshot",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "ItemSkuSnapshot",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Categories");
        }
    }
}
