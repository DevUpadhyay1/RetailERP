using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class OptimiseMultiTenantIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Warehouses_CompanyId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_Name_CompanyId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Units_CompanyId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Units_Name_CompanyId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CompanyId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Name_CompanyId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Stores_CompanyId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Stores_StoreCode_CompanyId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_CompanyId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_PurchaseNo_CompanyId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Items_CompanyId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_SKU_CompanyId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNo_CompanyId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CompanyId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name_CompanyId",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_CompanyId_Name",
                table: "Warehouses",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Units_CompanyId_Name",
                table: "Units",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CompanyId_Name",
                table: "Suppliers",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CompanyId_StoreCode",
                table: "Stores",
                columns: new[] { "CompanyId", "StoreCode" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_CompanyId_PurchaseNo",
                table: "Purchases",
                columns: new[] { "CompanyId", "PurchaseNo" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CompanyId_SKU",
                table: "Items",
                columns: new[] { "CompanyId", "SKU" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_InvoiceNo",
                table: "Invoices",
                columns: new[] { "CompanyId", "InvoiceNo" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyId_Name",
                table: "Categories",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Warehouses_CompanyId_Name",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Units_CompanyId_Name",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CompanyId_Name",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Stores_CompanyId_StoreCode",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_CompanyId_PurchaseNo",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Items_CompanyId_SKU",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_InvoiceNo",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CompanyId_Name",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_CompanyId",
                table: "Warehouses",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Name_CompanyId",
                table: "Warehouses",
                columns: new[] { "Name", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Units_CompanyId",
                table: "Units",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name_CompanyId",
                table: "Units",
                columns: new[] { "Name", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CompanyId",
                table: "Suppliers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name_CompanyId",
                table: "Suppliers",
                columns: new[] { "Name", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_CompanyId",
                table: "Stores",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_StoreCode_CompanyId",
                table: "Stores",
                columns: new[] { "StoreCode", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_CompanyId",
                table: "Purchases",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_PurchaseNo_CompanyId",
                table: "Purchases",
                columns: new[] { "PurchaseNo", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CompanyId",
                table: "Items",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_SKU_CompanyId",
                table: "Items",
                columns: new[] { "SKU", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId",
                table: "Invoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNo_CompanyId",
                table: "Invoices",
                columns: new[] { "InvoiceNo", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyId",
                table: "Categories",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name_CompanyId",
                table: "Categories",
                columns: new[] { "Name", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");
        }
    }
}
