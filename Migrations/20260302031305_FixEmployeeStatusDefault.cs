using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RetailERP.Migrations
{
    /// <inheritdoc />
    public partial class FixEmployeeStatusDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure DB default matches check constraint (Active=1).
            migrationBuilder.Sql(@"
DECLARE @df sysname;
SELECT @df = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
JOIN sys.tables t ON t.object_id = c.object_id
WHERE t.name = 'Employees' AND c.name = 'Status';

IF @df IS NOT NULL
BEGIN
    EXEC('ALTER TABLE [Employees] DROP CONSTRAINT [' + @df + ']');
END

ALTER TABLE [Employees] ADD CONSTRAINT [DF_Employees_Status] DEFAULT (1) FOR [Status];
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('DF_Employees_Status', 'D') IS NOT NULL
BEGIN
    ALTER TABLE [Employees] DROP CONSTRAINT [DF_Employees_Status];
END
");
        }
    }
}
