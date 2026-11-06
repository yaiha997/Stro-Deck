$ipHub = "192.168.68.102"
$url = "http://192.168.68.108:8090/"
$catt = "$env:LOCALAPPDATA\Programs\Python\Python312\Scripts\catt.exe"
$env:PYTHONIOENCODING = "utf-8"

# 1. Ensure Stro-Deck is running
$deckProc = Get-Process -Name "Stro-Deck" -ErrorAction SilentlyContinue
if (-not $deckProc) {
    Write-Host "Starting Stro-Deck..." -ForegroundColor Yellow
    Start-Process (Join-Path $PSScriptRoot "Stro-Deck.exe")
    Start-Sleep -Seconds 2
}

# 2. Push/Cast to Nest Hub
Write-Host "Casting Stro-Deck to Nest Hub ($ipHub)..." -ForegroundColor Cyan
& $catt -d $ipHub cast_site $url

# 3. Ensure Keep-Alive is running
$keepAliveProc = Get-WmiObject Win32_Process | Where-Object { $_.CommandLine -like "*hub_keepalive.ps1*" }
if (-not $keepAliveProc) {
    $keepAliveScript = Join-Path $PSScriptRoot "hub_keepalive.ps1"
    Start-Process "powershell.exe" -ArgumentList "-WindowStyle Hidden -ExecutionPolicy Bypass -File `"$keepAliveScript`""
}

Write-Host "Done! Stro-Deck is running on Google Nest Hub." -ForegroundColor Green
Start-Sleep -Seconds 3
