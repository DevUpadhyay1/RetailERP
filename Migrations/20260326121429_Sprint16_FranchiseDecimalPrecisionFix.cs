using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class Sprint16_FranchiseDecimalPrecisionFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "RoyaltyPercent",
                table: "FranchiseAgreements",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "RoyaltyPercent",
                table: "FranchiseAgreements",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,4)",
                oldPrecision: 9,
                oldScale: 4);
        }
    }
}
