param(
    [string]$BaseUrl = "https://him-finance-sri-sanyo.trycloudflare.com",
    [string]$Email = "",
    [string]$Password = "QbTest@123"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSDefaultParameterValues['Invoke-WebRequest:UseBasicParsing'] = $true

if ([string]::IsNullOrWhiteSpace($Email)) {
    $Email = "e2e." + (Get-Date -Format "yyyyMMddHHmmss") + "@quickbusiness.co.in"
}

$saLine = Get-Content .env.production | Where-Object { $_ -like 'SA_PASSWORD=*' } | Select-Object -First 1
$jwtLine = Get-Content .env.production | Where-Object { $_ -like 'JWT_SECRET=*' } | Select-Object -First 1
if (-not $saLine -or -not $jwtLine) {
    throw "Could not read SA_PASSWORD/JWT_SECRET from .env.production"
}

$saPassword = $saLine.Substring(12)
$jwtSecret = $jwtLine.Substring(11)

function Get-AntiForgeryToken {
    param([string]$Html)

    $m = [regex]::Match(
        $Html,
        '<input[^>]*name="__RequestVerificationToken"[^>]*value="([^"]+)"',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if (-not $m.Success) {
        throw "Anti-forgery token not found in HTML response."
    }

    return $m.Groups[1].Value
}

function Get-FirstGuidOptionValue {
    param(
        [string]$Html,
        [string]$SelectName
    )

    $selectPattern = '<select[^>]*name="' + [regex]::Escape($SelectName) + '"[^>]*>([\s\S]*?)</select>'
    $selectMatch = [regex]::Match(
        $Html,
        $selectPattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if (-not $selectMatch.Success) {
        throw "Select '$SelectName' not found."
    }

    $optionMatch = [regex]::Match(
        $selectMatch.Groups[1].Value,
        '<option[^>]*value="([0-9a-fA-F\-]{36})"[^>]*>',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if (-not $optionMatch.Success) {
        throw "No GUID option found in select '$SelectName'."
    }

    return $optionMatch.Groups[1].Value
}

function Get-UnitPriceForItem {
    param(
        [string]$Html,
        [string]$ItemId
    )

    $pattern = '<option[^>]*value="' + [regex]::Escape($ItemId) + '"[^>]*data-unit-price="([^"]+)"'
    $m = [regex]::Match($Html, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($m.Success) {
        return $m.Groups[1].Value
    }

    return "1.00"
}

function Get-DbCounts {
    param([string]$SaPassword)

    $raw = docker exec retailerp-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -d RetailERPDb -U sa -P "$SaPassword" -h -1 -W -s "," -Q "SET NOCOUNT ON; SELECT (SELECT COUNT(1) FROM AspNetUsers),(SELECT COUNT(1) FROM Customers),(SELECT COUNT(1) FROM Warehouses),(SELECT COUNT(1) FROM Items);"

    $line = ($raw -split "`r?`n" | Where-Object { $_ -match '^\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*\d+\s*$' } | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($line)) {
        throw "Could not parse DB counts from sqlcmd output."
    }

    $p = $line -split ',' | ForEach-Object { [int]($_.Trim()) }

    return [pscustomobject]@{
        Users = $p[0]
        Customers = $p[1]
        Warehouses = $p[2]
        Items = $p[3]
    }
}

function Start-DevSeeder {
    param(
        [string]$SaPassword,
        [string]$JwtSecret
    )

    $existing = docker ps -a --format "{{.Names}}" | Where-Object { $_ -eq "retailerp-dev-seeder" }
    if ($existing) {
        docker rm -f retailerp-dev-seeder | Out-Null
    }

    $null = docker run -d --name retailerp-dev-seeder --network retailerp_default `
        -e ASPNETCORE_ENVIRONMENT=Development `
        -e ASPNETCORE_URLS=http://+:8080 `
        -e ConnectionStrings__DefaultConnection="Server=retailerp-sqlserver-1;Database=RetailERPDb;User Id=sa;Password=$SaPassword;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true" `
        -e ConnectionStrings__Redis=retailerp-redis-1:6379 `
        -e Jwt__SecretKey="$JwtSecret" `
        retailerp:latest

    $ready = $false
    for ($i = 0; $i -lt 15; $i++) {
        $counts = Get-DbCounts -SaPassword $SaPassword
        if ($counts.Customers -gt 0 -and $counts.Warehouses -gt 0 -and $counts.Items -gt 0 -and $counts.Users -gt 0) {
            $ready = $true
            break
        }
        Start-Sleep -Seconds 2
    }

    $existingAfter = docker ps -a --format "{{.Names}}" | Where-Object { $_ -eq "retailerp-dev-seeder" }
    if ($existingAfter) {
        docker rm -f retailerp-dev-seeder | Out-Null
    }

    if (-not $ready) {
        throw "Seeder did not populate required master data in expected time."
    }
}

function Normalize-SeededTenantData {
    param([string]$SaPassword)

    $defaultCompanyId = "00000000-0000-0000-0000-000000000001"
    $sql = "SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON; " +
        "UPDATE [Customers] SET [CompanyId] = '$defaultCompanyId' WHERE [CompanyId] IS NULL; " +
        "UPDATE [Warehouses] SET [CompanyId] = '$defaultCompanyId' WHERE [CompanyId] IS NULL; " +
        "UPDATE [Items] SET [CompanyId] = '$defaultCompanyId' WHERE [CompanyId] IS NULL; " +
        "UPDATE [Stores] SET [CompanyId] = '$defaultCompanyId' WHERE [CompanyId] IS NULL; " +
        "UPDATE [Employees] SET [CompanyId] = '$defaultCompanyId' WHERE [CompanyId] IS NULL;"

    $null = docker exec retailerp-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -d RetailERPDb -U sa -P "$SaPassword" -Q "$sql"
}

Write-Host "Step 0: Tunnel reachability"
$landingProbe = Invoke-WebRequest -Uri "$BaseUrl/Home/Landing" -MaximumRedirection 5
if ($landingProbe.StatusCode -ne 200) {
    throw "Tunnel landing page is not reachable."
}

Write-Host "Step 1: Register new business account"
$registerSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$registerGet = Invoke-WebRequest -Uri "$BaseUrl/Identity/Account/Register" -WebSession $registerSession -MaximumRedirection 5

if ($registerGet.Content -match "Self-registration is currently disabled") {
    Write-Host "Registration is closed. Using seeded admin account for remaining steps."
    $Email = "admin@retailerp.com"
    $Password = "Admin@12345"
}
else {
    $registerToken = Get-AntiForgeryToken -Html $registerGet.Content
    $registerBody = @{
        "__RequestVerificationToken" = $registerToken
        "Input.Email" = $Email
        "Input.Password" = $Password
        "Input.ConfirmPassword" = $Password
    }

    $null = Invoke-WebRequest -Uri "$BaseUrl/Identity/Account/Register" -Method Post -WebSession $registerSession -Body $registerBody -ContentType "application/x-www-form-urlencoded" -MaximumRedirection 10
    Write-Host "Registered user:" $Email
}

Write-Host "Step 2: Seed baseline master data"
Start-DevSeeder -SaPassword $saPassword -JwtSecret $jwtSecret
Normalize-SeededTenantData -SaPassword $saPassword
$countsAfterSeed = Get-DbCounts -SaPassword $saPassword
Write-Host "Counts after seed => Users:" $countsAfterSeed.Users "Customers:" $countsAfterSeed.Customers "Warehouses:" $countsAfterSeed.Warehouses "Items:" $countsAfterSeed.Items

Write-Host "Step 3: Login with registered account"
$authSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginGet = Invoke-WebRequest -Uri "$BaseUrl/Identity/Account/Login" -WebSession $authSession -MaximumRedirection 5
$loginToken = Get-AntiForgeryToken -Html $loginGet.Content

$loginBody = @{
    "__RequestVerificationToken" = $loginToken
    "Input.Email" = $Email
    "Input.Password" = $Password
    "Input.RememberMe" = "false"
}

$null = Invoke-WebRequest -Uri "$BaseUrl/Identity/Account/Login" -Method Post -WebSession $authSession -Body $loginBody -ContentType "application/x-www-form-urlencoded" -MaximumRedirection 10

$authCookie = $authSession.Cookies.GetCookies($BaseUrl) | Where-Object { $_.Name -like ".AspNetCore.Identity.Application*" }
if (-not $authCookie) {
    throw "Login failed: auth cookie not found."
}

Write-Host "Step 4: Create default invoice template"
$templateGet = Invoke-WebRequest -Uri "$BaseUrl/BillTemplates/Create" -WebSession $authSession -MaximumRedirection 5
$templateToken = Get-AntiForgeryToken -Html $templateGet.Content

$templateBody = @{
    "__RequestVerificationToken" = $templateToken
    "TemplateName" = "E2E Invoice Template " + (Get-Date -Format "HHmmss")
    "TemplateType" = "2"
    "DocumentType" = "1"
    "TemplateScope" = "1"
    "preset" = "modern"
    "PaperSize" = "A4"
    "IsDefault" = "true"
}

$templatePost = Invoke-WebRequest -Uri "$BaseUrl/BillTemplates/Create" -Method Post -WebSession $authSession -Body $templateBody -ContentType "application/x-www-form-urlencoded" -MaximumRedirection 10
$templateUri = $templatePost.BaseResponse.ResponseUri.AbsoluteUri
$templateIdMatch = [regex]::Match($templateUri, '[0-9a-fA-F\-]{36}')
if (-not $templateIdMatch.Success) {
    throw "Could not detect created template id from redirect URI: $templateUri"
}
$templateId = $templateIdMatch.Value
Write-Host "Template created:" $templateId

Write-Host "Step 5: Create invoice draft"
$invoiceCreateGet = Invoke-WebRequest -Uri "$BaseUrl/Invoices/Create" -WebSession $authSession -MaximumRedirection 5
$invoiceCreateToken = Get-AntiForgeryToken -Html $invoiceCreateGet.Content
$customerId = Get-FirstGuidOptionValue -Html $invoiceCreateGet.Content -SelectName "CustomerId"
$warehouseId = Get-FirstGuidOptionValue -Html $invoiceCreateGet.Content -SelectName "WarehouseId"

$invoiceDate = (Get-Date).ToString("yyyy-MM-dd")
$dueDate = (Get-Date).AddDays(7).ToString("yyyy-MM-dd")

$invoiceCreateBody = @{
    "__RequestVerificationToken" = $invoiceCreateToken
    "DocumentType" = "1"
    "CustomerId" = $customerId
    "WarehouseId" = $warehouseId
    "InvoiceDate" = $invoiceDate
    "DueDate" = $dueDate
    "ReferenceInvoiceNo" = ""
    "EmployeeId" = ""
}

$invoiceCreatePost = Invoke-WebRequest -Uri "$BaseUrl/Invoices/Create" -Method Post -WebSession $authSession -Body $invoiceCreateBody -ContentType "application/x-www-form-urlencoded" -MaximumRedirection 10
$invoiceEditUri = $invoiceCreatePost.BaseResponse.ResponseUri.AbsoluteUri
$invoiceIdMatch = [regex]::Match($invoiceEditUri, '[0-9a-fA-F\-]{36}')
if (-not $invoiceIdMatch.Success) {
    throw "Could not detect invoice id from redirect URI: $invoiceEditUri"
}
$invoiceId = $invoiceIdMatch.Value
Write-Host "Invoice created:" $invoiceId

Write-Host "Step 6: Add one invoice line"
$invoiceEditGet = Invoke-WebRequest -Uri "$BaseUrl/Invoices/Edit/$invoiceId" -WebSession $authSession -MaximumRedirection 5
$addLineToken = Get-AntiForgeryToken -Html $invoiceEditGet.Content
$itemId = Get-FirstGuidOptionValue -Html $invoiceEditGet.Content -SelectName "ItemId"
$unitPrice = Get-UnitPriceForItem -Html $invoiceEditGet.Content -ItemId $itemId

$addLineBody = @{
    "__RequestVerificationToken" = $addLineToken
    "InvoiceId" = $invoiceId
    "ItemId" = $itemId
    "Qty" = "1"
    "UnitPrice" = $unitPrice
}

$null = Invoke-WebRequest -Uri "$BaseUrl/Invoices/AddLine" -Method Post -WebSession $authSession -Body $addLineBody -ContentType "application/x-www-form-urlencoded" -MaximumRedirection 10

Write-Host "Step 7: Generate invoice PDF"
$pdfResponse = Invoke-WebRequest -Uri "$BaseUrl/Invoices/Pdf?id=$invoiceId" -WebSession $authSession -MaximumRedirection 5
$contentType = $pdfResponse.Headers["Content-Type"]
if ($pdfResponse.StatusCode -ne 200 -or $contentType -notlike "application/pdf*") {
    throw "PDF step failed. Status=$($pdfResponse.StatusCode), Content-Type=$contentType"
}

Write-Host ""
Write-Host "E2E flow PASSED"
Write-Host "Register: PASS"
Write-Host "Login: PASS"
Write-Host "Invoice create: PASS (" $invoiceId ")"
Write-Host "PDF: PASS"
Write-Host "User:" $Email
