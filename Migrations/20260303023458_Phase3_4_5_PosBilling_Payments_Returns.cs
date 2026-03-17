using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_4_5_PosBilling_Payments_Returns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosBills",
                columns: table => new
                {
                    PosBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CashierUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BillDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosBills", x => x.PosBillId);
                    table.CheckConstraint("CK_PosBills_Status", "[Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_PosBills_AspNetUsers_CashierUserId",
                        column: x => x.CashierUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosBills_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosBills_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosBills_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosBills_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosBills_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosBills_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosBillLines",
                columns: table => new
                {
                    PosBillLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BarcodeSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SkuSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ItemNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Qty = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MrpSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GstPercentSnapshot = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    HsnCodeSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosBillLines", x => x.PosBillLineId);
                    table.ForeignKey(
                        name: "FK_PosBillLines_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosBillLines_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosBillLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosBillLines_PosBills_PosBillId",
                        column: x => x.PosBillId,
                        principalTable: "PosBills",
                        principalColumn: "PosBillId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosReturns",
                columns: table => new
                {
                    PosReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OriginalBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TotalRefund = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosReturns", x => x.PosReturnId);
                    table.CheckConstraint("CK_PosReturns_Status", "[Status] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_PosReturns_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturns_AspNetUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturns_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturns_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturns_PosBills_OriginalBillId",
                        column: x => x.OriginalBillId,
                        principalTable: "PosBills",
                        principalColumn: "PosBillId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturns_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturns_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRefund = table.Column<bool>(type: "bit", nullable: false),
                    PosReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentId);
                    table.CheckConstraint("CK_Payments_Method", "[Method] IN ('Cash','Card','UPI','Other')");
                    table.ForeignKey(
                        name: "FK_Payments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_PosBills_PosBillId",
                        column: x => x.PosBillId,
                        principalTable: "PosBills",
                        principalColumn: "PosBillId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_PosReturns_PosReturnId",
                        column: x => x.PosReturnId,
                        principalTable: "PosReturns",
                        principalColumn: "PosReturnId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosReturnLines",
                columns: table => new
                {
                    PosReturnLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalBillLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosReturnLines", x => x.PosReturnLineId);
                    table.ForeignKey(
                        name: "FK_PosReturnLines_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturnLines_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturnLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturnLines_PosBillLines_OriginalBillLineId",
                        column: x => x.OriginalBillLineId,
                        principalTable: "PosBillLines",
                        principalColumn: "PosBillLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosReturnLines_PosReturns_PosReturnId",
                        column: x => x.PosReturnId,
                        principalTable: "PosReturns",
                        principalColumn: "PosReturnId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CompanyId",
                table: "Payments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreatedByUserId",
                table: "Payments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PosBillId",
                table: "Payments",
                column: "PosBillId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PosReturnId",
                table: "Payments",
                column: "PosReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UpdatedByUserId",
                table: "Payments",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBillLines_CreatedByUserId",
                table: "PosBillLines",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBillLines_ItemId",
                table: "PosBillLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBillLines_PosBillId",
                table: "PosBillLines",
                column: "PosBillId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBillLines_UpdatedByUserId",
                table: "PosBillLines",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBills_BillNo",
                table: "PosBills",
                column: "BillNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosBills_CashierUserId",
                table: "PosBills",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBills_CompanyId",
                table: "PosBills",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBills_CreatedByUserId",
                table: "PosBills",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBills_CustomerId",
                table: "PosBills",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBills_StoreId",
                table: "PosBills",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBills_UpdatedByUserId",
                table: "PosBills",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosBills_WarehouseId",
                table: "PosBills",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturnLines_CreatedByUserId",
                table: "PosReturnLines",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturnLines_ItemId",
                table: "PosReturnLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturnLines_OriginalBillLineId",
                table: "PosReturnLines",
                column: "OriginalBillLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturnLines_PosReturnId",
                table: "PosReturnLines",
                column: "PosReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturnLines_UpdatedByUserId",
                table: "PosReturnLines",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_CompanyId",
                table: "PosReturns",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_CreatedByUserId",
                table: "PosReturns",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_CustomerId",
                table: "PosReturns",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_OriginalBillId",
                table: "PosReturns",
                column: "OriginalBillId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_ProcessedByUserId",
                table: "PosReturns",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_ReturnNo",
                table: "PosReturns",
                column: "ReturnNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_StoreId",
                table: "PosReturns",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_UpdatedByUserId",
                table: "PosReturns",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_WarehouseId",
                table: "PosReturns",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PosReturnLines");

            migrationBuilder.DropTable(
                name: "PosBillLines");

            migrationBuilder.DropTable(
                name: "PosReturns");

            migrationBuilder.DropTable(
                name: "PosBills");
        }
    }
}
