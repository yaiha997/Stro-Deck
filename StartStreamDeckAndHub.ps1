$ipHub = "192.168.68.102"
$url = "http://192.168.68.108:8090/"
$catt = "$env:LOCALAPPDATA\Programs\Python\Python312\Scripts\catt.exe"
$deckExe = "C:\Users\pc\Documents\Default Project\StreamDeck\StreamDeckPC.exe"
$env:PYTHONIOENCODING = "utf-8"

# 1. Start StreamDeckPC if not running
$deckProc = Get-Process -Name "StreamDeckPC" -ErrorAction SilentlyContinue
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
    Start-Process "powershell.exe" -ArgumentList "-WindowStyle Hidden -ExecutionPolicy Bypass -File ""`C:\Users\pc\Documents\Default Project\StreamDeck\hub_keepalive.ps1`""
}
