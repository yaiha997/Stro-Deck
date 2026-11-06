$ErrorActionPreference = "Stop"

$compilerCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw "The .NET Framework C# compiler (csc.exe) is required." }

$testExecutable = Join-Path $env:TEMP ("stro-deck-server-test-" + [Guid]::NewGuid().ToString("N") + ".exe")
try {
    & $compiler /nologo /target:exe "/out:$testExecutable" `
        /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
        (Join-Path $PSScriptRoot "Deck.cs") `
        (Join-Path $PSScriptRoot "tests\DeckServerTests.cs")
    if ($LASTEXITCODE -ne 0) { throw "Deck-server test build failed ($LASTEXITCODE)." }

    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw "Deck-server tests failed ($LASTEXITCODE)." }
} finally {
    if (Test-Path -LiteralPath $testExecutable) {
        Remove-Item -LiteralPath $testExecutable -Force
    }
}
