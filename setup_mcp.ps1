param(
    [string]$HubIp = "",
    [string]$DeckUrl = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($HubIp)) {
    $HubIp = Read-Host "Enter the Nest Hub IP address"
}
if ([string]::IsNullOrWhiteSpace($DeckUrl)) {
    $DeckUrl = Read-Host "Enter the full Stro-Deck URL, including http:// and port"
}
if ([string]::IsNullOrWhiteSpace($HubIp) -or [string]::IsNullOrWhiteSpace($DeckUrl)) {
    throw "Both the Nest Hub IP address and Stro-Deck URL are required."
}

function ConvertTo-Hashtable {
    param($Value)
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $result = @{}
        foreach ($property in $Value.PSObject.Properties) {
            $result[$property.Name] = ConvertTo-Hashtable $property.Value
        }
        return ,$result
    }
    if ($Value -is [System.Array]) {
        return ,@($Value | ForEach-Object { ConvertTo-Hashtable $_ })
    }
    return $Value
}

$pythonLauncher = Get-Command "py.exe" -ErrorAction SilentlyContinue
if ($pythonLauncher) {
    $pythonPath = (& $pythonLauncher.Source -3 -c "import sys; print(sys.executable)" | Select-Object -Last 1).Trim()
} else {
    $pythonCommand = Get-Command "python.exe" -ErrorAction SilentlyContinue
    if (-not $pythonCommand) { throw "Python was not found. Install Python 3 and try again." }
    $pythonPath = $pythonCommand.Source
}
if (-not (Test-Path -LiteralPath $pythonPath)) { throw "Python executable not found: $pythonPath" }

Write-Host "Installing MCP server dependencies..." -ForegroundColor Cyan
& $pythonPath -m pip install -r (Join-Path $PSScriptRoot "requirements-mcp.txt")
if ($LASTEXITCODE -ne 0) { throw "Dependency installation failed ($LASTEXITCODE)." }

$cattPath = Join-Path (Split-Path $pythonPath -Parent) "Scripts\catt.exe"
if (-not (Test-Path -LiteralPath $cattPath)) {
    $cattCommand = Get-Command "catt.exe" -ErrorAction SilentlyContinue
    if ($cattCommand) { $cattPath = $cattCommand.Source }
}
if (-not (Test-Path -LiteralPath $cattPath)) { throw "catt.exe was not installed or could not be located." }

$configPath = Join-Path $HOME ".gemini\config\mcp_config.json"
$configDirectory = Split-Path $configPath -Parent
New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null

$config = @{}
if (Test-Path -LiteralPath $configPath) {
    $existing = Get-Content -LiteralPath $configPath -Raw
    if (-not [string]::IsNullOrWhiteSpace($existing)) {
        try {
            $config = ConvertTo-Hashtable (ConvertFrom-Json -InputObject $existing -ErrorAction Stop)
        } catch {
            throw "Existing MCP config is not valid JSON; it was left unchanged: $configPath"
        }
    }
}

if (-not $config.ContainsKey("mcpServers")) { $config["mcpServers"] = @{} }
if ($config["mcpServers"] -isnot [System.Collections.IDictionary]) {
    throw "The mcpServers value in the existing config is not an object; it was left unchanged."
}

$config["mcpServers"]["stro-deck-nest-hub"] = @{
    command = $pythonPath
    args = @((Join-Path $PSScriptRoot "hub_mcp_server.py"))
    env = @{
        STRO_DECK_HUB_IP = $HubIp
        STRO_DECK_URL = $DeckUrl
        STRO_DECK_CATT_PATH = $cattPath
    }
}

$json = $config | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText($configPath, $json + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "MCP server configured:" -ForegroundColor Green
Write-Host "  Hub IP: $HubIp"
Write-Host "  Stro-Deck URL: $DeckUrl"
Write-Host "  Config: $configPath"
Write-Host ""
Write-Host "Restart or reload Antigravity's MCP servers, then run:" -ForegroundColor Cyan
Write-Host ('  "{0}" "{1}"' -f $pythonPath, (Join-Path $PSScriptRoot "test_mcp_stdio.py"))
