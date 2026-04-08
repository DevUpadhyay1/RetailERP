param(
    [Parameter(Mandatory = $true)]
    [string]$CurrentEmail,
    [Parameter(Mandatory = $true)]
    [string]$NewEmail,
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
$fromEmail = $CurrentEmail.Trim()
$toEmail = $NewEmail.Trim()

if ([string]::IsNullOrWhiteSpace($fromEmail) -or [string]::IsNullOrWhiteSpace($toEmail)) {
    throw "CurrentEmail and NewEmail are required."
}

$fromEscaped = $fromEmail.Replace("'", "''")
$toEscaped = $toEmail.Replace("'", "''")
$toNormalized = $toEmail.ToUpperInvariant().Replace("'", "''")

$sql = @"
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;

DECLARE @fromEmail nvarchar(256) = N'$fromEscaped';
DECLARE @toEmail nvarchar(256) = N'$toEscaped';
DECLARE @toNormalized nvarchar(256) = N'$toNormalized';

DECLARE @userId uniqueidentifier = (
    SELECT TOP 1 Id
    FROM AspNetUsers
    WHERE Email = @fromEmail
);

IF @userId IS NULL
BEGIN
    DECLARE @msg1 nvarchar(400) = N'Current user not found for email ' + @fromEmail;
    THROW 50010, @msg1, 1;
END

IF EXISTS (
    SELECT 1
    FROM AspNetUsers
    WHERE Email = @toEmail
      AND Id <> @userId
)
BEGIN
    DECLARE @msg2 nvarchar(400) = N'Another user already exists with email ' + @toEmail;
    THROW 50011, @msg2, 1;
END

UPDATE AspNetUsers
SET Email = @toEmail,
    UserName = @toEmail,
    NormalizedEmail = @toNormalized,
    NormalizedUserName = @toNormalized,
    EmailConfirmed = 1,
    IsActive = 1
WHERE Id = @userId;

SELECT Id, Email, UserName, IsActive, EmailConfirmed
FROM AspNetUsers
WHERE Id = @userId;
"@

docker exec $SqlContainer /opt/mssql-tools18/bin/sqlcmd -b -C -S localhost -d $Database -U sa -P "$saPassword" -Q "$sql"

if ($LASTEXITCODE -ne 0) {
    throw "Email update failed from $CurrentEmail to $NewEmail"
}

Write-Host "Email updated from $CurrentEmail to $NewEmail"
