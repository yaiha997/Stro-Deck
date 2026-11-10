$ErrorActionPreference = 'Stop'

$scriptRoot = $PSScriptRoot
$compilerCandidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$compiler = $compilerCandidates |
    Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
    Select-Object -First 1
if (-not $compiler) {
    throw 'Inno Setup 6 (ISCC.exe) is required to create the setup executable. Install it, then rerun this script.'
}

& (Join-Path $scriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw "Application build failed ($LASTEXITCODE)." }

$outputDirectory = Join-Path $scriptRoot 'installer\output'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
& $compiler (Join-Path $scriptRoot 'installer\Stro-Deck.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed ($LASTEXITCODE)." }

Write-Host "Installer created: $(Join-Path $outputDirectory 'Stro-Deck-Setup.exe')" -ForegroundColor Green
