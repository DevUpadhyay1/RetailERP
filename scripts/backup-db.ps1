# ====================================================================
# RetailERP Professional Database Backup Script
# Automatically retrieves credentials, executes backup securely
# and exports to Host Machine with a 7-day retention policy.
# ====================================================================

$ErrorActionPreference = 'Stop'

$backupDir = "C:\retailerp-backups"
$containerName = "retailerp-sqlserver-1"
$dbName = "RetailERPDb"
$date = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$backupFile = "retailerp-$date.bak"
$containerPath = "/var/opt/mssql/$backupFile"
$hostPath = "$backupDir\$backupFile"

if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
}

$envPath = "C:\7th_Semester\RetailERP\.env.production"
if (-not (Test-Path $envPath)) { throw "Environment file not found at $envPath" }

$envData = Get-Content $envPath
$pwdLine = $envData | Where-Object { $_ -match "^SA_PASSWORD=" }
if (-not $pwdLine) { throw "SA_PASSWORD not found" }
$saPassword = $pwdLine.Split('=', 2)[1].Trim()

Write-Host "Starting backup of '$dbName' in container '$containerName'..."

# Single line to prevent powershell multiline errors
docker exec $containerName /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$saPassword" -Q "BACKUP DATABASE [$dbName] TO DISK = N'$containerPath' WITH FORMAT, INIT, STATS = 10"

if ($LASTEXITCODE -ne 0) { throw "SQL Backup failed!" }

Write-Host "Copying backup to host storage ($hostPath)..."
docker cp "${containerName}:${containerPath}" $hostPath

if (-not (Test-Path $hostPath)) { throw "Failed to extract backup file to host!" }

Write-Host "Cleaning up temporary container storage..."
docker exec $containerName rm $containerPath

Write-Host "Applying 7-day retention policy..."
Get-ChildItem -Path $backupDir -Filter "*.bak" | Where-Object { $_.CreationTime -lt (Get-Date).AddDays(-7) } | Remove-Item -Force

Write-Host "
[SUCCESS] Backup completed successfully: $hostPath"
