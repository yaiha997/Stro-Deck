param(
    [Parameter(Mandatory=$true)][string]$Ip,
    [int]$Port = 9222,
    [string]$Url = "",
    [switch]$List,
    [switch]$ClickSelector,
    [string]$Selector = ""
)

$ErrorActionPreference = 'Stop'
$base = "http://${Ip}:${Port}"

function Get-Targets {
    $v = Invoke-RestMethod -Uri "$base/json/list" -TimeoutSec 5
    return @($v)
}

Write-Host "DevTools: $base" -ForegroundColor Cyan
$targets = Get-Targets
if (-not $targets -or $targets.Count -eq 0) {
    Write-Host "No debug targets exposed." -ForegroundColor Red
    exit 1
}

$i = 0
foreach ($t in $targets) {
    Write-Host ("  [{0}] {1}  |  {2}  |  {3}" -f $i, $t.type, $t.title, $t.url)
    $i++
}
if ($List -and -not $Url) { exit 0 }

$target = $targets | Where-Object { $_.type -eq 'page' } | Select-Object -First 1
if (-not $target) { $target = $targets[0] }
if (-not $target.webSocketDebuggerUrl) {
    Write-Host "Target has no webSocketDebuggerUrl (needs a page/webview target)." -ForegroundColor Red
    exit 1
}
Write-Host ("Attaching to: " + $target.url) -ForegroundColor Green

$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ct = [System.Threading.CancellationToken]::None
$ws.ConnectAsync([Uri]$target.webSocketDebuggerUrl, $ct).Wait(8000)

function Send-Cdp {
    param($obj)
    $json = $obj | ConvertTo-Json -Compress -Depth 5
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $seg = New-Object System.ArraySegment[byte] -ArgumentList @(,$bytes)
    $ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $ct).Wait(8000)
}

function Receive-Cdp {
    param([int]$TimeoutMs = 4000)
    $buf = New-Object byte[] 65536
    $sb = New-Object System.Text.StringBuilder
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    while ((Get-Date) -lt $deadline) {
        $seg = New-Object System.ArraySegment[byte] -ArgumentList @(,$buf)
        $task = $ws.ReceiveAsync($seg, $ct)
        if (-not $task.Wait([Math]::Max(50, [int]($deadline - (Get-Date)).TotalMilliseconds))) { break }
        $r = $task.Result
        [void]$sb.Append([System.Text.Encoding]::UTF8.GetString($buf, 0, $r.Count))
        if ($r.EndOfMessage) { break }
    }
    return $sb.ToString()
}

Send-Cdp @{ id = 1; method = "Page.enable" } | Out-Null
[void](Receive-Cdp -TimeoutMs 1500)

if ($Url) {
    Send-Cdp @{ id = 2; method = "Page.navigate"; params = @{ url = $Url } } | Out-Null
    Write-Host "Navigate sent -> $Url" -ForegroundColor Green
    $resp = Receive-Cdp -TimeoutMs 6000
    Write-Host $resp
}

if ($Selector) {
    $js = @"
(function(){
  var el = document.querySelector('$Selector');
  if (!el) return 'not-found';
  var r = el.getBoundingClientRect();
  el.dispatchEvent(new MouseEvent('click', {bubbles:true, clientX:r.left+r.width/2, clientY:r.top+r.height/2}));
  return 'clicked';
})()
"@
    Send-Cdp @{ id = 3; method = "Runtime.evaluate"; params = @{ expression = $js; returnByValue = $true } } | Out-Null
    Write-Host (Receive-Cdp -TimeoutMs 5000)
}

$ws.Dispose()
Write-Host "Done." -ForegroundColor Green
