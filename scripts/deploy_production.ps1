param(
    [string]$AppDir = "C:\retailerp",
    [string]$ComposeFile = "docker-compose.prod.yml",
    [string]$EnvFile = ".env.production",
    [string]$Image = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $AppDir)) {
    throw "AppDir '$AppDir' not found."
}

Set-Location $AppDir

if (-not (Test-Path $EnvFile)) {
    throw "Env file '$EnvFile' not found. Copy deploy/.env.production.template and update values."
}

if ($Image -ne "") {
    $env:APP_IMAGE = $Image
}

docker compose --env-file $EnvFile -f $ComposeFile pull app
docker compose --env-file $EnvFile -f $ComposeFile up -d
docker image prune -f

Write-Host "Deployment completed successfully."
