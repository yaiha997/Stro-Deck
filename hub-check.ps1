param(
    [string]$Ip = "",
    [int[]]$Ports = @(9222, 9223, 9229, 8009, 8008, 8080, 22, 4321, 5555)
)

$ErrorActionPreference = 'Continue'

if (-not $Ip) {
    $Ip = Read-Host "Enter the Google Hub IP address (from Technical Information)"
}
$Ip = $Ip.Trim()

Write-Host ""
Write-Host "=== Checking device: $Ip ===" -ForegroundColor Cyan

# 1) ICMP reachability
$ping = Test-Connection -ComputerName $Ip -Count 2 -Quiet -ErrorAction SilentlyContinue
if ($ping) { Write-Host "[OK]  Device responds to ping" -ForegroundColor Green }
else { Write-Host "[--]  No ping response (it may be blocked; continuing with port checks)" -ForegroundColor Yellow }

# 2) TCP ports
function Test-Port {
    param([string]$HostName, [int]$Port, [int]$TimeoutMs = 600)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $ar = $client.BeginConnect($HostName, $Port, $null, $null)
        if ($ar.AsyncWaitHandle.WaitOne($TimeoutMs, $false) -and $client.Connected) {
            $client.EndConnect($ar)
            return $true
        }
        return $false
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

$open = @()
foreach ($p in $Ports) {
    $ok = Test-Port -HostName $Ip -Port $p
    if ($ok) {
        $label = switch ($p) {
            8009 { "Google Cast (Chromecast)" }
            9222 { "Chrome DevTools / WebView debugging  <== required" }
            9223 { "DevTools alternate port" }
            9229 { "Node inspector" }
            5555 { "ADB" }
            22   { "SSH" }
            4321 { "Fuchsia / miscellaneous" }
            default { "" }
        }
        Write-Host ("[OK]  Port {0} is open  {1}" -f $p, $label) -ForegroundColor Green
        $open += $p
    } else {
        Write-Host ("[--]  Port {0} is closed" -f $p) -ForegroundColor DarkGray
    }
}

# 3) Read the target list if a DevTools port is open
$devtools = @(9222, 9223) | Where-Object { $open -contains $_ }
foreach ($dp in $devtools) {
    Write-Host ""
    Write-Host "=== DevTools on port $dp ===" -ForegroundColor Cyan
    try {
        $ver = Invoke-RestMethod -Uri "http://${Ip}:${dp}/json/version" -TimeoutSec 4
        Write-Host ("Browser: " + $ver.Browser)
        Write-Host ("WebSocket: " + $ver.webSocketDebuggerUrl)
        $list = Invoke-RestMethod -Uri "http://${Ip}:${dp}/json/list" -TimeoutSec 4
        Write-Host "Available pages / WebViews:"
        $i = 0
        foreach ($t in $list) {
            Write-Host ("  [$i] " + $t.type + "  " + $t.title + "  " + $t.url) -ForegroundColor Green
            $i++
        }
        if ($i -eq 0) { Write-Host "  (No pages found; the WebView may be hidden)" -ForegroundColor Yellow }
    } catch {
        Write-Host ("Could not read /json: " + $_.Exception.Message) -ForegroundColor Red
    }
}

Write-Host ""
if ($open.Count -eq 0) {
    Write-Host "Result: No ports are open; the device is not reachable from this computer." -ForegroundColor Red
    Write-Host "Make sure the Hub is on the same network as the computer." -ForegroundColor Red
} else {
    Write-Host ("Result: Open ports -> " + ($open -join ", ")) -ForegroundColor Green
}
