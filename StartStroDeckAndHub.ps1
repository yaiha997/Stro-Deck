$ipHub = "192.168.68.102"
$url = "http://192.168.68.108:8090/"
$catt = "$env:LOCALAPPDATA\Programs\Python\Python312\Scripts\catt.exe"
$deckExe = Join-Path $PSScriptRoot "Stro-Deck.exe"
$env:PYTHONIOENCODING = "utf-8"

# 1. Start Stro-Deck if not running
$deckProc = Get-Process -Name "Stro-Deck" -ErrorAction SilentlyContinue
if (-not $deckProc) {
    Start-Process $deckExe
    Start-Sleep -Seconds 3
}

# 2. Stop any existing cast then cast fresh
& $catt -d $ipHub stop 2>$null
Start-Sleep -Seconds 1
& $catt -d $ipHub cast_site $url

# 3. Ensure Keep-Alive Daemon is running
$keepAliveProc = Get-WmiObject Win32_Process | Where-Object { $_.CommandLine -like "*hub_keepalive.ps1*" }
if (-not $keepAliveProc) {
    $keepAliveScript = Join-Path $PSScriptRoot "hub_keepalive.ps1"
    Start-Process "powershell.exe" -ArgumentList "-WindowStyle Hidden -ExecutionPolicy Bypass -File `"$keepAliveScript`""
}
