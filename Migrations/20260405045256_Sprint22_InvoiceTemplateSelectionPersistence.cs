using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class Sprint22_InvoiceTemplateSelectionPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BillTemplateId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BillTemplateId",
                table: "Invoices",
                column: "BillTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_BillTemplates_BillTemplateId",
                table: "Invoices",
                column: "BillTemplateId",
                principalTable: "BillTemplates",
                principalColumn: "BillTemplateId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_BillTemplates_BillTemplateId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_BillTemplateId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BillTemplateId",
                table: "Invoices");
        }
    }
}
