# Runs Dose locally on Windows: SQL Server (Docker) + API (:5080) + Angular site (:4200).
# Usage (PowerShell, from the repo folder; if scripts are blocked use: powershell -ExecutionPolicy Bypass -File .\run-local.ps1):
#   .\run-local.ps1
#   .\run-local.ps1 -ConnectionString "Server=.\SQLEXPRESS;Database=Dose;Trusted_Connection=True;TrustServerCertificate=True"
# The second form uses an existing SQL Server instead of Docker.
# Development only - sample data is labelled SAMPLE and the admin account is local.

param(
    [string]$ConnectionString = "",
    [string]$AdminEmail = "admin@dose.local",
    [string]$AdminPassword = "Admin_Dev_12345"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

function Need($cmd, $hint) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) { Write-Host "Missing: $cmd - $hint" -ForegroundColor Red; exit 1 }
}
Need dotnet "install the .NET 9 SDK: https://dotnet.microsoft.com/download/dotnet/9.0"
Need node   "install Node.js 22 LTS or newer: https://nodejs.org"
Need npm    "comes with Node.js"
if (-not ((dotnet --list-sdks) -match "^(9\.0\.[3-9]|1[0-9]\.)")) { Write-Host "The .NET 9 SDK (9.0.300 or newer) is required: https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Red; exit 1 }

# ---------- 1. Database ----------
# No Docker? Use SQL Server LocalDB (installed with Visual Studio / SQL Server Express) or a running local SQL Server.
if (-not $ConnectionString -and -not (Get-Command docker -ErrorAction SilentlyContinue)) {
    if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) {
        Write-Host "Docker not found - using SQL Server LocalDB." -ForegroundColor Cyan
        sqllocaldb start MSSQLLocalDB | Out-Null
        $ConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=Dose;Trusted_Connection=True;TrustServerCertificate=True"
    } else {
        $svc = Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq "MSSQLSERVER" -or $_.Name -like 'MSSQL$*' } | Select-Object -First 1
        if ($svc) {
            if ($svc.Status -ne "Running") { Write-Host "SQL Server service '$($svc.Name)' is installed but stopped. Start it (Services app) and run again." -ForegroundColor Red; exit 1 }
            $instance = if ($svc.Name -eq "MSSQLSERVER") { "." } else { ".\" + $svc.Name.Substring(6) }
            Write-Host "Docker not found - using local SQL Server $instance (Windows login)." -ForegroundColor Cyan
            $ConnectionString = "Server=$instance;Database=Dose;Trusted_Connection=True;TrustServerCertificate=True"
        }
    }
}

if (-not $ConnectionString) {
    Need docker "no SQL Server found. Install SQL Server Express (https://www.microsoft.com/sql-server/sql-server-downloads - choose Express) or Docker Desktop, or pass -ConnectionString"
    if (-not (Test-Path "$root\.env")) {
        # A random local-only SA password, kept in .env (git-ignored).
        $saPwd = "Dose_" + [guid]::NewGuid().ToString("N").Substring(0, 12) + "!a1"
        Set-Content "$root\.env" "DOSE_SQL_SA_PASSWORD=$saPwd"
    }
    $sa = ((Get-Content "$root\.env" | Where-Object { $_ -like "DOSE_SQL_SA_PASSWORD=*" }) -split "=", 2)[1]
    Write-Host "Starting SQL Server in Docker..." -ForegroundColor Cyan
    docker compose up -d
    if ($LASTEXITCODE -ne 0) { Write-Host "Docker failed - is Docker Desktop running?" -ForegroundColor Red; exit 1 }
    $ConnectionString = "Server=localhost,1433;Database=Dose;User Id=sa;Password=$sa;TrustServerCertificate=True"
    Write-Host "Waiting for SQL Server to accept connections (first start can take about 30s)..."
    $ok = $false
    for ($i = 0; $i -lt 60; $i++) {
        try { $c = New-Object System.Net.Sockets.TcpClient("localhost", 1433); $c.Close(); $ok = $true; break } catch { Start-Sleep 2 }
    }
    if (-not $ok) { Write-Host "SQL Server did not start. Check: docker logs dose-sqlserver" -ForegroundColor Red; exit 1 }
    Start-Sleep 10
}

# ---------- 2. API (http://localhost:5080) ----------
Write-Host "Starting the API in a new window (creates the database and SAMPLE data on first run)..." -ForegroundColor Cyan
$apiCmd = @"
`$env:ConnectionStrings__Dose = '$ConnectionString'
`$env:Seed__AdminEmail = '$AdminEmail'
`$env:Seed__AdminPassword = '$AdminPassword'
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
Set-Location '$root'
dotnet run --project src/Dose.Api --launch-profile http
"@
# Encoded so paths and passwords survive the hand-off to the new window unchanged.
$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($apiCmd))
Start-Process powershell -ArgumentList "-NoExit", "-EncodedCommand", $encoded

# ---------- 3. Website (http://localhost:4200) ----------
Set-Location "$root\src\Dose.Web"
if (-not (Test-Path "node_modules")) { Write-Host "Installing website packages (first run only)..." -ForegroundColor Cyan; npm install }

Write-Host "Waiting for the API..."
for ($i = 0; $i -lt 90; $i++) {
    try { if ((Invoke-WebRequest "http://localhost:5080/api/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { break } } catch { Start-Sleep 2 }
}

Write-Host ""
Write-Host "Dose is starting:" -ForegroundColor Green
Write-Host "  Student site : http://localhost:4200"
Write-Host "  Team login   : http://localhost:4200/login   ($AdminEmail / $AdminPassword)"
Write-Host "  Stop         : Ctrl+C here, close the API window, then 'docker compose stop'"
Write-Host ""
Start-Process "http://localhost:4200"
npm start
