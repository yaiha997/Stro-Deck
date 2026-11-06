$catt = "$env:LOCALAPPDATA\Programs\Python\Python312\Scripts\catt.exe"
$ip = "192.168.68.102"
$url = "http://192.168.68.108:8090/"
$env:PYTHONIOENCODING = "utf-8"

Write-Host "Starting Nest Hub Keep-Alive Loop..." -ForegroundColor Cyan

while ($true) {
    try {
        $status = & $catt -d $ip status 2>$null
        # If not playing or returned to ambient/idle screen, recast
        if ($status -notmatch "Casting" -and $status -notmatch "Stream Deck") {
            Write-Host "Re-casting to ensure screen stays forever..." -ForegroundColor Yellow
            & $catt -d $ip cast_site $url 2>$null
        }
    } catch { }
    Start-Sleep -Seconds 180
}
