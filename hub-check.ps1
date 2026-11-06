param(
    [string]$Ip = "",
    [int[]]$Ports = @(9222, 9223, 9229, 8009, 8008, 8080, 22, 4321, 5555)
)

$ErrorActionPreference = 'Continue'

if (-not $Ip) {
    $Ip = Read-Host "اكتب IP جهاز Google Hub (من Technical Information)"
}
$Ip = $Ip.Trim()

Write-Host ""
Write-Host "=== فحص الجهاز: $Ip ===" -ForegroundColor Cyan

# 1) ICMP
$ping = Test-Connection -ComputerName $Ip -Count 2 -Quiet -ErrorAction SilentlyContinue
if ($ping) { Write-Host "[OK]  الجهاز يرد على ping" -ForegroundColor Green }
else { Write-Host "[--]  ما يرد على ping (قد يكون محجوب، نكمل فحص المنافذ)" -ForegroundColor Yellow }

# 2) المنافذ
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
            9222 { "Chrome DevTools / WebView debugging  <== المطلوب" }
            9223 { "DevTools alt" }
            9229 { "Node inspector" }
            5555 { "ADB" }
            22   { "SSH" }
            4321 { "Fuchsia / misc" }
            default { "" }
        }
        Write-Host ("[OK]  منفذ {0} مفتوح  {1}" -f $p, $label) -ForegroundColor Green
        $open += $p
    } else {
        Write-Host ("[--]  منفذ {0} مغلق" -f $p) -ForegroundColor DarkGray
    }
}

# 3) لو منفذ الديباغ مفتوح، نقرأ قائمة الـ targets
$devtools = @(9222, 9223) | Where-Object { $open -contains $_ }
foreach ($dp in $devtools) {
    Write-Host ""
    Write-Host "=== DevTools على المنفذ $dp ===" -ForegroundColor Cyan
    try {
        $ver = Invoke-RestMethod -Uri "http://${Ip}:${dp}/json/version" -TimeoutSec 4
        Write-Host ("المتصفح: " + $ver.Browser)
        Write-Host ("WebSocket: " + $ver.webSocketDebuggerUrl)
        $list = Invoke-RestMethod -Uri "http://${Ip}:${dp}/json/list" -TimeoutSec 4
        Write-Host "الصفحات/الـ webviews المتاحة:"
        $i = 0
        foreach ($t in $list) {
            Write-Host ("  [$i] " + $t.type + "  " + $t.title + "  " + $t.url) -ForegroundColor Green
            $i++
        }
        if ($i -eq 0) { Write-Host "  (ما فيه صفحات — غالبًا webview مخفي)" -ForegroundColor Yellow }
    } catch {
        Write-Host ("تعذر قراءة /json: " + $_.Exception.Message) -ForegroundColor Red
    }
}

Write-Host ""
if ($open.Count -eq 0) {
    Write-Host "النتيجة: ما فيه أي منفذ مفتوح — الجهاز غير قابل للوصول من هذا الكمبيوتر." -ForegroundColor Red
    Write-Host "تأكد أن الهب على نفس شبكة الكمبيوتر (نفس الراوتر)." -ForegroundColor Red
} else {
    Write-Host ("النتيجة: منافذ مفتوحة -> " + ($open -join ", ")) -ForegroundColor Green
}
