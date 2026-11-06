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

Write-Host "Starting Nest Hub Keep-Alive Loop..." -ForegroundColor Cyan

while ($true) {
    try {
        $status = & $catt -d $HubIp status 2>$null
        # If not playing or returned to ambient/idle screen, recast
        if ($status -notmatch "Casting" -and $status -notmatch "Stro-Deck") {
            Write-Host "Re-casting to ensure screen stays forever..." -ForegroundColor Yellow
            & $catt -d $HubIp cast_site $DeckUrl 2>$null
        }
    } catch {
        Write-Warning "Nest Hub keep-alive check failed; it will retry."
    }
    Start-Sleep -Seconds 180
}
