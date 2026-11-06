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
    "$PSScriptRoot\Ui.cs",
    "$PSScriptRoot\MainForm.cs",
    "$PSScriptRoot\Program.cs"
)
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon" }

& $csc @cscArgs

if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

Write-Host ""
Write-Host "Built: $out" -ForegroundColor Green
