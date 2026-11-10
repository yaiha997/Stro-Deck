$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

$out = Join-Path $PSScriptRoot 'Stro-Deck.exe'

$icon = Join-Path $PSScriptRoot 'app.ico'
$cscArgs = @(
    '/nologo', '/codepage:65001', '/target:winexe', '/platform:anycpu', '/optimize+',
    "/out:$out",
    '/r:System.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
    "/resource:$PSScriptRoot\deck.html,deck.html",
    "$PSScriptRoot\Deck.cs",
    "$PSScriptRoot\LocalAddresses.cs",
    "$PSScriptRoot\NowPlaying.cs",
    "$PSScriptRoot\Ui.cs",
    "$PSScriptRoot\MainForm.cs",
    "$PSScriptRoot\Program.cs"
)
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon" }

& $csc @cscArgs

if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

$winrtCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\System.Runtime.WindowsRuntime.dll')
)
$winrt = $winrtCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $winrt) { throw "System.Runtime.WindowsRuntime.dll was not found in the .NET Framework installation." }
Copy-Item -LiteralPath $winrt -Destination (Join-Path $PSScriptRoot 'System.Runtime.WindowsRuntime.dll') -Force

Write-Host ""
Write-Host "Built: $out" -ForegroundColor Green
