param(
    [string]$AppDir = "C:\retailerp",
    [string]$ComposeFile = "docker-compose.prod.yml",
    [string]$EnvFile = ".env.production",
    [string]$Image = "",
    [string[]]$KnownProxies = @(),
    [string[]]$KnownNetworks = @(),
    [switch]$SkipForwardedHeaderValidation,
    [switch]$SkipPull,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

function Read-EnvMap {
    param([string]$Path)

    $map = @{}
    if (-not (Test-Path $Path)) {
        return $map
    }

    foreach ($line in Get-Content $Path) {
        if ($line -match '^\s*#') {
            continue
        }

        if ($line -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*)\s*$') {
            $map[$matches[1]] = $matches[2].Trim()
        }
    }

    return $map
}

function Set-EnvValue {
    param(
        [string]$Path,
        [string]$Key,
        [string]$Value
    )

    $normalizedValue = if ($null -eq $Value) { "" } else { $Value.Trim() }
    $lines = [System.Collections.Generic.List[string]]::new()
    if (Test-Path $Path) {
        foreach ($line in Get-Content -Path $Path) {
            $lines.Add([string]$line)
        }
    }

    $pattern = "^\s*" + [Regex]::Escape($Key) + "\s*="
    $updated = $false

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $pattern) {
            $lines[$i] = "$Key=$normalizedValue"
            $updated = $true
            break
        }
    }

    if (-not $updated) {
        $lines.Add("$Key=$normalizedValue")
    }

    Set-Content -Path $Path -Value $lines -Encoding UTF8
}

function Test-IpAddress {
    param([string]$Value)

    $ip = $null
    return [System.Net.IPAddress]::TryParse($Value, [ref]$ip)
}

function Test-CidrNotation {
    param([string]$Value)

    $parts = $Value.Split('/')
    if ($parts.Count -ne 2) {
        return $false
    }

    $ip = $null
    if (-not [System.Net.IPAddress]::TryParse($parts[0], [ref]$ip)) {
        return $false
    }

    $prefix = 0
    if (-not [int]::TryParse($parts[1], [ref]$prefix)) {
        return $false
    }

    $maxPrefix = if ($ip.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork) { 32 } else { 128 }
    return $prefix -ge 0 -and $prefix -le $maxPrefix
}

function Validate-ForwardedHeadersConfiguration {
    param([hashtable]$EnvMap)

    $placeholderTokens = @("CHANGE_ME", "YOUR_PROXY", "10.0.0.10", "10.0.0.0/24")

    $proxyValues = @(
        $EnvMap.Keys |
        Where-Object { $_ -match '^FORWARDED_HEADERS_KNOWN_PROXY_\d+$' } |
        Sort-Object |
        ForEach-Object { $EnvMap[$_] }
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    $networkValues = @(
        $EnvMap.Keys |
        Where-Object { $_ -match '^FORWARDED_HEADERS_KNOWN_NETWORK_\d+$' } |
        Sort-Object |
        ForEach-Object { $EnvMap[$_] }
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    if (($proxyValues.Count + $networkValues.Count) -eq 0) {
        throw "Forwarded headers trust is not configured. Set FORWARDED_HEADERS_KNOWN_PROXY_* and/or FORWARDED_HEADERS_KNOWN_NETWORK_* in '$EnvFile'."
    }

    foreach ($proxy in $proxyValues) {
        if ($placeholderTokens | Where-Object { $proxy -like "*$_*" }) {
            throw "FORWARDED_HEADERS_KNOWN_PROXY value '$proxy' looks like a placeholder. Replace it with a real proxy IP."
        }

        if (-not (Test-IpAddress $proxy)) {
            throw "FORWARDED_HEADERS_KNOWN_PROXY value '$proxy' is invalid. Expected an IP address."
        }
    }

    foreach ($network in $networkValues) {
        if ($placeholderTokens | Where-Object { $network -like "*$_*" }) {
            throw "FORWARDED_HEADERS_KNOWN_NETWORK value '$network' looks like a placeholder. Replace it with a real CIDR (e.g. 10.0.0.0/24)."
        }

        if (-not (Test-CidrNotation $network)) {
            throw "FORWARDED_HEADERS_KNOWN_NETWORK value '$network' is invalid. Expected CIDR notation."
        }
    }
}

function Invoke-CheckedCommand {
    param(
        [scriptblock]$Command,
        [string]$FailureMessage
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code: $LASTEXITCODE)"
    }
}

function Test-IsLikelyLocalImageName {
    param([string]$ImageName)

    if ([string]::IsNullOrWhiteSpace($ImageName)) {
        return $false
    }

    # Registry images almost always include a repository path segment (e.g. ghcr.io/org/app:tag).
    # A bare name like "retailerp:latest" is treated as local-tag workflow in this script.
    return -not $ImageName.Contains('/')
}

if (-not (Test-Path $AppDir)) {
    throw "AppDir '$AppDir' not found."
}

Set-Location $AppDir

if (-not (Test-Path $EnvFile)) {
    throw "Env file '$EnvFile' not found. Copy deploy/.env.production.template and update values."
}

$maxForwardedHeaderEntries = 3

if ($KnownProxies.Count -gt $maxForwardedHeaderEntries) {
    throw "Too many KnownProxies values. Maximum supported entries: $maxForwardedHeaderEntries."
}

if ($KnownNetworks.Count -gt $maxForwardedHeaderEntries) {
    throw "Too many KnownNetworks values. Maximum supported entries: $maxForwardedHeaderEntries."
}

if ($KnownProxies.Count -gt 0 -or $KnownNetworks.Count -gt 0) {
    for ($i = 0; $i -lt $maxForwardedHeaderEntries; $i++) {
        $proxyValue = if ($i -lt $KnownProxies.Count) { $KnownProxies[$i] } else { "" }
        $networkValue = if ($i -lt $KnownNetworks.Count) { $KnownNetworks[$i] } else { "" }

        Set-EnvValue -Path $EnvFile -Key "FORWARDED_HEADERS_KNOWN_PROXY_$i" -Value $proxyValue
        Set-EnvValue -Path $EnvFile -Key "FORWARDED_HEADERS_KNOWN_NETWORK_$i" -Value $networkValue
    }

    Write-Host "Updated forwarded headers trust values in '$EnvFile'."
}

$envMap = Read-EnvMap -Path $EnvFile
if (-not $SkipForwardedHeaderValidation) {
    Validate-ForwardedHeadersConfiguration -EnvMap $envMap
}

$resolvedImage = if ($Image -ne "") {
    $Image
} elseif ($envMap.ContainsKey("APP_IMAGE")) {
    $envMap["APP_IMAGE"]
} else {
    ""
}

$autoSkippedPull = $false
if (-not $SkipPull -and (Test-IsLikelyLocalImageName $resolvedImage)) {
    $SkipPull = $true
    $autoSkippedPull = $true
}

if ($ValidateOnly) {
    if ($autoSkippedPull) {
        Write-Host "Detected local APP_IMAGE '$resolvedImage'; pull step will be skipped during deployment."
    }
    Write-Host "Validation completed successfully. Deployment was skipped because -ValidateOnly was provided."
    return
}

if ($Image -ne "") {
    $env:APP_IMAGE = $Image
}

if (-not $SkipPull) {
    Invoke-CheckedCommand -FailureMessage "docker compose pull app failed" -Command {
        docker compose --env-file $EnvFile -f $ComposeFile pull app
    }
}
else {
    if ($autoSkippedPull) {
        Write-Host "Skipping docker compose pull because APP_IMAGE '$resolvedImage' looks like a local image tag."
    }
    else {
        Write-Host "Skipping docker compose pull because -SkipPull was provided."
    }
}

Invoke-CheckedCommand -FailureMessage "docker compose up failed" -Command {
    docker compose --env-file $EnvFile -f $ComposeFile up -d
}

Invoke-CheckedCommand -FailureMessage "docker image prune failed" -Command {
    docker image prune -f
}

Write-Host "Deployment completed successfully."
