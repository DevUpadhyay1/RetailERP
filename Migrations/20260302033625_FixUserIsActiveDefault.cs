using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class FixUserIsActiveDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [AspNetUsers] SET [IsActive] = 1;

DECLARE @df sysname;
SELECT @df = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
JOIN sys.tables t ON t.object_id = c.object_id
WHERE t.name = 'AspNetUsers' AND c.name = 'IsActive';

IF @df IS NOT NULL
BEGIN
    EXEC('ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @df + ']');
END

ALTER TABLE [AspNetUsers] ADD CONSTRAINT [DF_AspNetUsers_IsActive] DEFAULT (1) FOR [IsActive];
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('DF_AspNetUsers_IsActive', 'D') IS NOT NULL
BEGIN
    ALTER TABLE [AspNetUsers] DROP CONSTRAINT [DF_AspNetUsers_IsActive];
END
");
        }
    }
}
