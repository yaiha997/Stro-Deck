param(
    [string]$HubIp = "",
    [string]$DeckUrl = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($HubIp)) { $HubIp = Read-Host "Enter the Nest Hub IP address" }
if ([string]::IsNullOrWhiteSpace($DeckUrl)) { $DeckUrl = Read-Host "Enter the current Stro-Deck URL shown in the app" }
if ([string]::IsNullOrWhiteSpace($HubIp) -or [string]::IsNullOrWhiteSpace($DeckUrl)) {
    throw "Both the Nest Hub IP and current Stro-Deck URL are required."
}

$cattCommand = Get-Command "catt.exe" -ErrorAction SilentlyContinue
$catt = if ($cattCommand) { $cattCommand.Source } else { "" }
if (-not $catt) {
    $pythonRoot = Join-Path $env:LOCALAPPDATA "Programs\Python"
    if (Test-Path -LiteralPath $pythonRoot) {
        $candidate = Get-ChildItem -Path (Join-Path $pythonRoot "Python*\Scripts\catt.exe") -File -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
        if ($candidate) { $catt = $candidate.FullName }
    }
}
if (-not $catt) { throw "catt.exe was not found. Install catt and add it to PATH, or run setup_mcp.ps1." }

$env:PYTHONIOENCODING = "utf-8"

# 1. Ensure Stro-Deck is running
$deckProc = Get-Process -Name "Stro-Deck" -ErrorAction SilentlyContinue
if (-not $deckProc) {
    Write-Host "Starting Stro-Deck..." -ForegroundColor Yellow
    Start-Process (Join-Path $PSScriptRoot "Stro-Deck.exe")
    Start-Sleep -Seconds 2
}

# 2. Push/Cast to Nest Hub
Write-Host "Casting Stro-Deck to Nest Hub..." -ForegroundColor Cyan
& $catt -d $HubIp cast_site $DeckUrl
if ($LASTEXITCODE -ne 0) { throw "Casting failed ($LASTEXITCODE)." }

# 3. Ensure Keep-Alive is running
$keepAliveProc = Get-WmiObject Win32_Process | Where-Object { $_.CommandLine -like "*hub_keepalive.ps1*" }
if (-not $keepAliveProc) {
    $keepAliveScript = Join-Path $PSScriptRoot "hub_keepalive.ps1"
    Start-Process "powershell.exe" -ArgumentList @(
        "-NoProfile", "-WindowStyle", "Hidden", "-ExecutionPolicy", "Bypass",
        "-File", "`"$keepAliveScript`"", "-HubIp", $HubIp, "-DeckUrl", $DeckUrl
    )
}

Write-Host "Done! Stro-Deck is running on Google Nest Hub." -ForegroundColor Green
Start-Sleep -Seconds 3
