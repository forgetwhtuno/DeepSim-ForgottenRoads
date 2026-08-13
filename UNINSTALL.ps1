param(
    [string]$GameDir = "",
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"

function Resolve-Erenshor {
    param([string]$Explicit)
    if ($Explicit -and (Test-Path (Join-Path $Explicit "Erenshor.exe"))) { return (Resolve-Path $Explicit).Path }
    $manual = Read-Host "Paste the Erenshor folder containing Erenshor.exe"
    if ($manual -and (Test-Path (Join-Path $manual "Erenshor.exe"))) { return (Resolve-Path $manual).Path }
    throw "Erenshor installation not found."
}

$GameDir = Resolve-Erenshor $GameDir
$Plugin = Join-Path $GameDir "plugins\ErenshorDeepSims.dll"
$Config = Join-Path $GameDir "plugins\config\erenshordeepsims.lpcfg"
$Data = Join-Path $GameDir "plugins\config\DeepSims"

if (Test-Path $Plugin) {
    Remove-Item -LiteralPath $Plugin -Force
    Write-Host "Removed plugin: $Plugin" -ForegroundColor Green
} else {
    Write-Host "Plugin DLL was not present: $Plugin" -ForegroundColor DarkGray
}

if ($RemoveData) {
    if (Test-Path $Config) { Remove-Item -LiteralPath $Config -Force; Write-Host "Removed config: $Config" }
    if (Test-Path $Data) { Remove-Item -LiteralPath $Data -Recurse -Force; Write-Host "Removed Deep Sims sidecar data: $Data" }
    Write-Host "Erenshor save files were not touched." -ForegroundColor Yellow
} else {
    Write-Host "Deep Sims config and memory were preserved." -ForegroundColor Cyan
    Write-Host "Run again with -RemoveData only if you intentionally want to delete Deep Sims-owned config/memory."
}
