using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSaaSPaymentGateways : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayKeyId",
                table: "Companies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayKeySecret",
                table: "Companies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "GatewayProvider",
                table: "Companies",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayKeyId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "GatewayKeySecret",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "GatewayProvider",
                table: "Companies");
        }
    }
}
