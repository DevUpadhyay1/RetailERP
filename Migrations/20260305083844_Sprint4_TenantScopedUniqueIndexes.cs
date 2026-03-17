using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4_TenantScopedUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Warehouses_Name",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Units_Name",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Stores_StoreCode",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_PurchaseNo",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_PosReturns_ReturnNo",
                table: "PosReturns");

            migrationBuilder.DropIndex(
                name: "IX_PosBills_BillNo",
                table: "PosBills");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_CardNumber",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_Items_Barcode",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_SKU",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNo",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Coupons_Code",
                table: "Coupons");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Name_CompanyId",
                table: "Warehouses",
                columns: new[] { "Name", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name_CompanyId",
                table: "Units",
                columns: new[] { "Name", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name_CompanyId",
                table: "Suppliers",
                columns: new[] { "Name", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_StoreCode_CompanyId",
                table: "Stores",
                columns: new[] { "StoreCode", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_PurchaseNo_CompanyId",
                table: "Purchases",
                columns: new[] { "PurchaseNo", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_ReturnNo_CompanyId",
                table: "PosReturns",
                columns: new[] { "ReturnNo", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PosBills_BillNo_CompanyId",
                table: "PosBills",
                columns: new[] { "BillNo", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_CardNumber_CompanyId",
                table: "LoyaltyCards",
                columns: new[] { "CardNumber", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Barcode_CompanyId",
                table: "Items",
                columns: new[] { "Barcode", "CompanyId" },
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Items_SKU_CompanyId",
                table: "Items",
                columns: new[] { "SKU", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNo_CompanyId",
                table: "Invoices",
                columns: new[] { "InvoiceNo", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code_CompanyId",
                table: "Coupons",
                columns: new[] { "Code", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name_CompanyId",
                table: "Categories",
                columns: new[] { "Name", "CompanyId" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Warehouses_Name_CompanyId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Units_Name_CompanyId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Name_CompanyId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Stores_StoreCode_CompanyId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_PurchaseNo_CompanyId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_PosReturns_ReturnNo_CompanyId",
                table: "PosReturns");

            migrationBuilder.DropIndex(
                name: "IX_PosBills_BillNo_CompanyId",
                table: "PosBills");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_CardNumber_CompanyId",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_Items_Barcode_CompanyId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_SKU_CompanyId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNo_CompanyId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Coupons_Code_CompanyId",
                table: "Coupons");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name_CompanyId",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Name",
                table: "Warehouses",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name",
                table: "Units",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_StoreCode",
                table: "Stores",
                column: "StoreCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_PurchaseNo",
                table: "Purchases",
                column: "PurchaseNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosReturns_ReturnNo",
                table: "PosReturns",
                column: "ReturnNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosBills_BillNo",
                table: "PosBills",
                column: "BillNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_CardNumber",
                table: "LoyaltyCards",
                column: "CardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_Barcode",
                table: "Items",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Items_SKU",
                table: "Items",
                column: "SKU",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNo",
                table: "Invoices",
                column: "InvoiceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code",
                table: "Coupons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);
        }
    }
}
