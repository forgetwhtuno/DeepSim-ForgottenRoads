param([string]$BepInExRoot = "")
$ErrorActionPreference = "Stop"
if (-not $BepInExRoot) {
    $BepInExRoot = Read-Host "Paste the r2modman/Thunderstore profile folder that contains BepInEx"
}
$core = Join-Path $BepInExRoot "BepInEx\core\BepInEx.dll"
if (-not (Test-Path $core)) { throw "The selected folder is not a BepInEx root: $BepInExRoot" }
$plugins = @(
    @{ Name = "DeepSims"; Path = (Join-Path $BepInExRoot "BepInEx\plugins\DeepSims") }
    @{ Name = "Erenshor PvP"; Path = (Join-Path $BepInExRoot "BepInEx\plugins\ErenshorPvP") }
)
foreach ($entry in $plugins) {
    if (Test-Path $entry.Path) {
        Remove-Item -LiteralPath $entry.Path -Recurse -Force
        Write-Host "Removed $($entry.Name) plugin."
    }
}
Write-Host "Memory/config under BepInEx\config\DeepSims was intentionally kept. Delete that folder too if you want a full reset."
