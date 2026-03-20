using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class Sprint15_FranchiseMultiLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentCompanyId",
                table: "Companies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FranchiseAgreements",
                columns: table => new
                {
                    FranchiseAgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FranchisorCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FranchiseeCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoyaltyPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MonthlyFlatFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinMonthlyRoyalty = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Territory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseAgreements", x => x.FranchiseAgreementId);
                    table.CheckConstraint("CK_FranchiseAgreements_Status", "[Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_FranchiseAgreements_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseAgreements_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseAgreements_Companies_FranchiseeCompanyId",
                        column: x => x.FranchiseeCompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseAgreements_Companies_FranchisorCompanyId",
                        column: x => x.FranchisorCompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoyaltyPayments",
                columns: table => new
                {
                    RoyaltyPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FranchiseAgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<byte>(type: "tinyint", nullable: false),
                    GrossSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RoyaltyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FlatFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoyaltyPayments", x => x.RoyaltyPaymentId);
                    table.CheckConstraint("CK_RoyaltyPayments_Status", "[Status] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_RoyaltyPayments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoyaltyPayments_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoyaltyPayments_FranchiseAgreements_FranchiseAgreementId",
                        column: x => x.FranchiseAgreementId,
                        principalTable: "FranchiseAgreements",
                        principalColumn: "FranchiseAgreementId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_ParentCompanyId",
                table: "Companies",
                column: "ParentCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseAgreements_AgreementCode",
                table: "FranchiseAgreements",
                column: "AgreementCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseAgreements_CreatedByUserId",
                table: "FranchiseAgreements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseAgreements_FranchiseeCompanyId",
                table: "FranchiseAgreements",
                column: "FranchiseeCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseAgreements_FranchisorCompanyId_FranchiseeCompanyId",
                table: "FranchiseAgreements",
                columns: new[] { "FranchisorCompanyId", "FranchiseeCompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseAgreements_UpdatedByUserId",
                table: "FranchiseAgreements",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoyaltyPayments_CreatedByUserId",
                table: "RoyaltyPayments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoyaltyPayments_FranchiseAgreementId_PeriodYear_PeriodMonth",
                table: "RoyaltyPayments",
                columns: new[] { "FranchiseAgreementId", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoyaltyPayments_UpdatedByUserId",
                table: "RoyaltyPayments",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Companies_ParentCompanyId",
                table: "Companies",
                column: "ParentCompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Companies_ParentCompanyId",
                table: "Companies");

            migrationBuilder.DropTable(
                name: "RoyaltyPayments");

            migrationBuilder.DropTable(
                name: "FranchiseAgreements");

            migrationBuilder.DropIndex(
                name: "IX_Companies_ParentCompanyId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ParentCompanyId",
                table: "Companies");
        }
    }
}
