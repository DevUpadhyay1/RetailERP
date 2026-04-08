using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class Sprint21_ProfessionalInvoiceCustomizationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillTemplates_Companies_CompanyId",
                table: "BillTemplates");

            migrationBuilder.DropIndex(
                name: "IX_BillTemplates_CompanyId_TemplateType_IsDefault",
                table: "BillTemplates");

            migrationBuilder.AddColumn<byte>(
                name: "DocumentType",
                table: "Invoices",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceInvoiceNo",
                table: "Invoices",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignaturePath",
                table: "Companies",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StampPath",
                table: "Companies",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "BillTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte>(
                name: "DocumentType",
                table: "BillTemplates",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<bool>(
                name: "EnableFreeItemQuantity",
                table: "BillTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowItemDescription",
                table: "BillTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowPartyBalance",
                table: "BillTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowPhoneOnInvoice",
                table: "BillTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowSignature",
                table: "BillTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowStamp",
                table: "BillTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "BillTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "TemplateScope",
                table: "BillTemplates",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "ThemeName",
                table: "BillTemplates",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "InvoiceNumberingRules",
                columns: table => new
                {
                    InvoiceNumberingRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentType = table.Column<byte>(type: "tinyint", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Suffix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NumberWidth = table.Column<int>(type: "int", nullable: false),
                    NextNumber = table.Column<int>(type: "int", nullable: false),
                    ResetPolicy = table.Column<byte>(type: "tinyint", nullable: false),
                    LastResetAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceNumberingRules", x => x.InvoiceNumberingRuleId);
                    table.ForeignKey(
                        name: "FK_InvoiceNumberingRules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceNumberingRules_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillTemplates_CompanyId_TemplateType_DocumentType_TemplateScope_StoreId_IsDefault",
                table: "BillTemplates",
                columns: new[] { "CompanyId", "TemplateType", "DocumentType", "TemplateScope", "StoreId", "IsDefault" },
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BillTemplates_StoreId",
                table: "BillTemplates",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceNumberingRules_CompanyId_StoreId_DocumentType_IsActive",
                table: "InvoiceNumberingRules",
                columns: new[] { "CompanyId", "StoreId", "DocumentType", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceNumberingRules_StoreId",
                table: "InvoiceNumberingRules",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_BillTemplates_Companies_CompanyId",
                table: "BillTemplates",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BillTemplates_Stores_StoreId",
                table: "BillTemplates",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "StoreId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillTemplates_Companies_CompanyId",
                table: "BillTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_BillTemplates_Stores_StoreId",
                table: "BillTemplates");

            migrationBuilder.DropTable(
                name: "InvoiceNumberingRules");

            migrationBuilder.DropIndex(
                name: "IX_BillTemplates_CompanyId_TemplateType_DocumentType_TemplateScope_StoreId_IsDefault",
                table: "BillTemplates");

            migrationBuilder.DropIndex(
                name: "IX_BillTemplates_StoreId",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReferenceInvoiceNo",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SignaturePath",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "StampPath",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "EnableFreeItemQuantity",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "ShowItemDescription",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "ShowPartyBalance",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "ShowPhoneOnInvoice",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "ShowSignature",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "ShowStamp",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "TemplateScope",
                table: "BillTemplates");

            migrationBuilder.DropColumn(
                name: "ThemeName",
                table: "BillTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_BillTemplates_CompanyId_TemplateType_IsDefault",
                table: "BillTemplates",
                columns: new[] { "CompanyId", "TemplateType", "IsDefault" },
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_BillTemplates_Companies_CompanyId",
                table: "BillTemplates",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "CompanyId");
        }
    }
}
