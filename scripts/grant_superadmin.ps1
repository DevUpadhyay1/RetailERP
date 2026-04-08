param(
    [Parameter(Mandatory = $true)]
    [string]$Email,
    [string]$EnvFile = ".env.production",
    [string]$SqlContainer = "retailerp-sqlserver-1",
    [string]$Database = "RetailERPDb"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $EnvFile)) {
    throw "Env file '$EnvFile' not found."
}

$saLine = Get-Content $EnvFile | Where-Object { $_ -match '^SA_PASSWORD=' } | Select-Object -First 1
if (-not $saLine) {
    throw "SA_PASSWORD not found in '$EnvFile'."
}

$saPassword = $saLine.Substring($saLine.IndexOf('=') + 1)
$safeEmail = $Email.Replace("'", "''")

$sql = @"
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
DECLARE @email nvarchar(256) = N'$safeEmail';
DECLARE @userId uniqueidentifier = (SELECT TOP 1 Id FROM AspNetUsers WHERE Email = @email);

IF @userId IS NULL
BEGIN
    DECLARE @msg nvarchar(400) = N'User not found for email ' + @email;
    THROW 50000, @msg, 1;
END

DECLARE @roleId uniqueidentifier = (SELECT TOP 1 Id FROM AspNetRoles WHERE Name = 'SuperAdmin');
IF @roleId IS NULL
BEGIN
    THROW 50001, 'Role SuperAdmin not found.', 1;
END

UPDATE AspNetUsers
SET IsActive = 1,
    EmailConfirmed = 1
WHERE Id = @userId;

IF NOT EXISTS (
    SELECT 1
    FROM AspNetUserRoles
    WHERE UserId = @userId AND RoleId = @roleId
)
BEGIN
    INSERT INTO AspNetUserRoles (UserId, RoleId)
    VALUES (@userId, @roleId);
END

SELECT u.Email, u.IsActive, u.EmailConfirmed, r.Name AS RoleName
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON ur.UserId = u.Id
JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE u.Id = @userId
ORDER BY r.Name;
"@

docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -b -C -S localhost -d $Database -U sa -P "$saPassword" -Q "$sql"

if ($LASTEXITCODE -ne 0) {
    throw "SuperAdmin assignment failed for $Email"
}

Write-Host "SuperAdmin assignment completed for $Email"
