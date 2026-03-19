using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class Sprint14_CustomerSupplierPortals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortalAccessLinks",
                columns: table => new
                {
                    PortalAccessLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortalType = table.Column<byte>(type: "tinyint", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TokenHint = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAccessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalAccessLinks", x => x.PortalAccessLinkId);
                    table.CheckConstraint("CK_PortalAccessLinks_PortalType", "[PortalType] IN (1,2)");
                    table.CheckConstraint("CK_PortalAccessLinks_Target", "(([PortalType] = 1 AND [CustomerId] IS NOT NULL AND [SupplierId] IS NULL) OR ([PortalType] = 2 AND [SupplierId] IS NOT NULL AND [CustomerId] IS NULL))");
                    table.ForeignKey(
                        name: "FK_PortalAccessLinks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalAccessLinks_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalAccessLinks_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalAccessLinks_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalAccessLinks_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortalReturnRequests",
                columns: table => new
                {
                    PortalReturnRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PosReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalReturnRequests", x => x.PortalReturnRequestId);
                    table.CheckConstraint("CK_PortalReturnRequests_Status", "[Status] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_PortalReturnRequests_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalReturnRequests_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalReturnRequests_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalReturnRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalReturnRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalReturnRequests_PosBills_PosBillId",
                        column: x => x.PosBillId,
                        principalTable: "PosBills",
                        principalColumn: "PosBillId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortalReturnRequests_PosReturns_PosReturnId",
                        column: x => x.PosReturnId,
                        principalTable: "PosReturns",
                        principalColumn: "PosReturnId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPoResponses",
                columns: table => new
                {
                    SupplierPoResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupplierNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPoResponses", x => x.SupplierPoResponseId);
                    table.CheckConstraint("CK_SupplierPoResponses_ResponseStatus", "[ResponseStatus] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_SupplierPoResponses_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPoResponses_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPoResponses_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPoResponses_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "PurchaseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPoResponses_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccessLinks_CompanyId",
                table: "PortalAccessLinks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccessLinks_CreatedByUserId",
                table: "PortalAccessLinks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccessLinks_CustomerId",
                table: "PortalAccessLinks",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccessLinks_SupplierId",
                table: "PortalAccessLinks",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccessLinks_TokenHash_CompanyId",
                table: "PortalAccessLinks",
                columns: new[] { "TokenHash", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccessLinks_UpdatedByUserId",
                table: "PortalAccessLinks",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalReturnRequests_CompanyId",
                table: "PortalReturnRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalReturnRequests_CreatedByUserId",
                table: "PortalReturnRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalReturnRequests_CustomerId_RequestedAtUtc",
                table: "PortalReturnRequests",
                columns: new[] { "CustomerId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalReturnRequests_PosBillId",
                table: "PortalReturnRequests",
                column: "PosBillId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalReturnRequests_PosReturnId",
                table: "PortalReturnRequests",
                column: "PosReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalReturnRequests_ReviewedByUserId",
                table: "PortalReturnRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalReturnRequests_UpdatedByUserId",
                table: "PortalReturnRequests",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPoResponses_CompanyId",
                table: "SupplierPoResponses",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPoResponses_CreatedByUserId",
                table: "SupplierPoResponses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPoResponses_PurchaseId",
                table: "SupplierPoResponses",
                column: "PurchaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPoResponses_SupplierId_ResponseStatus",
                table: "SupplierPoResponses",
                columns: new[] { "SupplierId", "ResponseStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPoResponses_UpdatedByUserId",
                table: "SupplierPoResponses",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalAccessLinks");

            migrationBuilder.DropTable(
                name: "PortalReturnRequests");

            migrationBuilder.DropTable(
                name: "SupplierPoResponses");
        }
    }
}
