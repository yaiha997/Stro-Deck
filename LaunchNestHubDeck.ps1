$ipHub = "192.168.68.102"
$url = "http://192.168.68.108:8090/"
$catt = "$env:LOCALAPPDATA\Programs\Python\Python312\Scripts\catt.exe"
$env:PYTHONIOENCODING = "utf-8"

# 1. Ensure StreamDeckPC is running
$deckProc = Get-Process -Name "StreamDeckPC" -ErrorAction SilentlyContinue
if (-not $deckProc) {
    Write-Host "Starting StreamDeckPC..." -ForegroundColor Yellow
    Start-Process "C:\Users\pc\Documents\Default Project\StreamDeck\StreamDeckPC.exe"
    Start-Sleep -Seconds 2
}

# 2. Push/Cast to Nest Hub
Write-Host "Casting Stream Deck to Nest Hub ($ipHub)..." -ForegroundColor Cyan
& $catt -d $ipHub cast_site $url

# 3. Ensure Keep-Alive is running
$keepAliveProc = Get-WmiObject Win32_Process | Where-Object { $_.CommandLine -like "*hub_keepalive.ps1*" }
if (-not $keepAliveProc) {
    Start-Process "powershell.exe" -ArgumentList "-WindowStyle Hidden -ExecutionPolicy Bypass -File ""`C:\Users\pc\Documents\Default Project\StreamDeck\hub_keepalive.ps1`""
}

Write-Host "Done! Stream Deck is running on Google Nest Hub." -ForegroundColor Green
Start-Sleep -Seconds 3
