[CmdletBinding()]
param (
    [string]$BaseUrl = "https://localhost:7240"
)

# 1. Login to get JWT Token
$loginUrl = "$BaseUrl/api/auth/login"
$loginBody = @{
    email = "admin@retailerp.com"
    password = "Admin@12345"
} | ConvertTo-Json

# Bypass self-signed cert validation for local https
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

Write-Host "Logging in to get token..."
try {
    $loginResponse = Invoke-RestMethod -Uri $loginUrl -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginResponse.data.token
    if (-not $token) { throw "Token not found in response." }
} catch {
    Write-Error "Failed to login. Is the app running on $BaseUrl?"
    Write-Error $_
    exit 1
}

$headers = @{
    Authorization = "Bearer $token"
}

# 2. Benchmark the Endpoint
$targetUrl = "$BaseUrl/api/items?page=1&pageSize=20&search="
Write-Host "Benchmarking endpoint: GET $targetUrl"

# Warmup request
$null = Invoke-RestMethod -Uri $targetUrl -Headers $headers -Method Get

$times = @()
$iterations = 10

Write-Host "Running $iterations timed requests..."
for ($i = 1; $i -le $iterations; $i++) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $null = Invoke-RestMethod -Uri $targetUrl -Headers $headers -Method Get
    $sw.Stop()
    $times += $sw.ElapsedMilliseconds
    Write-Host "  Request $i : $($sw.ElapsedMilliseconds) ms"
}

$min = ($times | Measure-Object -Minimum).Minimum
$max = ($times | Measure-Object -Maximum).Maximum
$avg = ($times | Measure-Object -Average).Average

Write-Host "`n--- RESULT ---"
Write-Host "Endpoint: $targetUrl"
Write-Host "Min Latency: $min ms"
Write-Host "Max Latency: $max ms"
Write-Host "Avg Latency: $([math]::Round($avg, 2)) ms"
Write-Host "----------------"
