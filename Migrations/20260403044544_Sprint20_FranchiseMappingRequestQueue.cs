using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class Sprint20_FranchiseMappingRequestQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FranchiseMappingRequests",
                columns: table => new
                {
                    FranchiseMappingRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestingCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MappedOperatorCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedOperatorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedOperatorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RequestedOperatorCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedOperatorState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseMappingRequests", x => x.FranchiseMappingRequestId);
                    table.CheckConstraint("CK_FranchiseMappingRequests_Status", "[Status] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_FranchiseMappingRequests_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseMappingRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseMappingRequests_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseMappingRequests_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseMappingRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseMappingRequests_Companies_MappedOperatorCompanyId",
                        column: x => x.MappedOperatorCompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FranchiseMappingRequests_Companies_RequestingCompanyId",
                        column: x => x.RequestingCompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseMappingRequests_CompanyId",
                table: "FranchiseMappingRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseMappingRequests_CreatedByUserId",
                table: "FranchiseMappingRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseMappingRequests_MappedOperatorCompanyId",
                table: "FranchiseMappingRequests",
                column: "MappedOperatorCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseMappingRequests_RequestedByUserId",
                table: "FranchiseMappingRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseMappingRequests_RequestingCompanyId_Status_RequestedAtUtc",
                table: "FranchiseMappingRequests",
                columns: new[] { "RequestingCompanyId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseMappingRequests_ReviewedByUserId",
                table: "FranchiseMappingRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseMappingRequests_UpdatedByUserId",
                table: "FranchiseMappingRequests",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FranchiseMappingRequests");
        }
    }
}
