$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$executable = Join-Path $root 'Stro-Deck.exe'
$runtime = Join-Path $root 'System.Runtime.WindowsRuntime.dll'
$outputDirectory = Join-Path $root 'release'
$zipPath = Join-Path $outputDirectory 'Stro-Deck-v1.0.2.zip'

foreach ($required in @(
    $executable,
    $runtime,
    (Join-Path $root 'deck-config.txt'),
    (Join-Path $root 'app.ico'),
    (Join-Path $root 'icons'),
    (Join-Path $root 'backgrounds')
)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required release file is missing: $required"
    }
}

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable).FileVersion
if ($version -ne '1.0.2.0') {
    throw "Expected application file version 1.0.2.0, found $version. Build the application first."
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$staging = Join-Path $outputDirectory 'Stro-Deck-v1.0.2'
if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
New-Item -ItemType Directory -Path $staging | Out-Null

try {
    Copy-Item -LiteralPath $executable -Destination $staging
    Copy-Item -LiteralPath $runtime -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'deck-config.txt') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'app.ico') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'app-256.png') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $staging
    Copy-Item -LiteralPath (Join-Path $root 'icons') -Destination $staging -Recurse
    Copy-Item -LiteralPath (Join-Path $root 'backgrounds') -Destination $staging -Recurse

    Compress-Archive -Path $staging -DestinationPath $zipPath -CompressionLevel Optimal
}
finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
}

Write-Host "Release ZIP created: $zipPath" -ForegroundColor Green
