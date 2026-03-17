using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class Sprint8_GST_EInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EInvoices",
                columns: table => new
                {
                    EInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Irn = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AckNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AckDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedInvoice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedQrCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EInvoices", x => x.EInvoiceId);
                    table.CheckConstraint("CK_EInvoices_Status", "[Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_EInvoices_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EInvoices_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EInvoices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EInvoices_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EInvoices_PosBills_PosBillId",
                        column: x => x.PosBillId,
                        principalTable: "PosBills",
                        principalColumn: "PosBillId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EWayBills",
                columns: table => new
                {
                    EWayBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EwbNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidUpto = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PosBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierGstin = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    RecipientGstin = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    DocType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DocNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DocDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TransporterId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    TransporterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VehicleNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TransMode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Distance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FromPincode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    ToAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ToPincode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EWayBills", x => x.EWayBillId);
                    table.CheckConstraint("CK_EWayBills_Status", "[Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_EWayBills_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EWayBills_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EWayBills_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EWayBills_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EWayBills_PosBills_PosBillId",
                        column: x => x.PosBillId,
                        principalTable: "PosBills",
                        principalColumn: "PosBillId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EInvoices_CompanyId",
                table: "EInvoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EInvoices_CreatedByUserId",
                table: "EInvoices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EInvoices_InvoiceId",
                table: "EInvoices",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EInvoices_Irn",
                table: "EInvoices",
                column: "Irn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EInvoices_PosBillId",
                table: "EInvoices",
                column: "PosBillId");

            migrationBuilder.CreateIndex(
                name: "IX_EInvoices_UpdatedByUserId",
                table: "EInvoices",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EWayBills_CompanyId",
                table: "EWayBills",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EWayBills_CreatedByUserId",
                table: "EWayBills",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EWayBills_EwbNo",
                table: "EWayBills",
                column: "EwbNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EWayBills_InvoiceId",
                table: "EWayBills",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EWayBills_PosBillId",
                table: "EWayBills",
                column: "PosBillId");

            migrationBuilder.CreateIndex(
                name: "IX_EWayBills_UpdatedByUserId",
                table: "EWayBills",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EInvoices");

            migrationBuilder.DropTable(
                name: "EWayBills");
        }
    }
}
