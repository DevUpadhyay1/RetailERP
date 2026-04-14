# ============================================================
# FIX: GitHub Actions Runner Service - Login Failure Fix
# Run this script AS ADMINISTRATOR in PowerShell
# ============================================================
# Problem: Runner service configured as .\Dev but password is wrong/expired
# Solution: Switch to LocalSystem (no password needed) + add to docker-users
# ============================================================

$serviceName = "actions.runner.DevUpadhyay1-RetailERP.retailerp-local"

Write-Host "=== Step 1: Stop the runner service ===" -ForegroundColor Cyan
Stop-Service $serviceName -Force -ErrorAction SilentlyContinue
Start-Sleep 2

Write-Host "=== Step 2: Reconfigure to run as LocalSystem ===" -ForegroundColor Cyan
sc.exe config $serviceName obj= "LocalSystem" password= ""
if ($LASTEXITCODE -eq 0) {
    Write-Host "  SUCCESS: Service account changed to LocalSystem" -ForegroundColor Green
} else {
    Write-Host "  FAILED: sc.exe config returned $LASTEXITCODE" -ForegroundColor Red
    Write-Host "  Are you running as Administrator?" -ForegroundColor Yellow
    exit 1
}

Write-Host "=== Step 3: Ensure LocalSystem has Docker access ===" -ForegroundColor Cyan
# LocalSystem already has admin privileges, but let's also ensure docker-users has it
net localgroup docker-users "NT AUTHORITY\SYSTEM" /add 2>$null
Write-Host "  docker-users group updated" -ForegroundColor Green

Write-Host "=== Step 4: Start the runner service ===" -ForegroundColor Cyan
Start-Service $serviceName
Start-Sleep 5

$svc = Get-Service $serviceName
Write-Host ""
if ($svc.Status -eq "Running") {
    Write-Host "=== RUNNER IS RUNNING! ===" -ForegroundColor Green
    Write-Host "The Deploy Production workflow will now work on pushes to main." -ForegroundColor Green
} else {
    Write-Host "=== RUNNER STATUS: $($svc.Status) ===" -ForegroundColor Yellow
    Write-Host "Check Event Viewer > System log for details." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Current service account:" -ForegroundColor Gray
Get-WmiObject Win32_Service -Filter "Name='$serviceName'" | Select-Object StartName
