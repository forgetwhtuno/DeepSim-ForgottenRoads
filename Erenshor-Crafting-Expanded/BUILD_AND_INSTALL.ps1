<#
  Convenience one-shot build+install. Unlike INSTALL_TEST.ps1 this intentionally performs NO
  backup/restore bookkeeping, so it is not the recommended development path.

  Safety rules:
    - resolves exactly one install target; never silently picks the first of multiple profiles
    - builds against that same target's BepInEx/Harmony references via BUILD.ps1
    - copies only ErenshorCraftingExpanded.dll into this mod's own plugin folder

  Preferred reversible workflow:
    .\BUILD.ps1 -BepInExRoot <profile>
    .\INSTALL_TEST.ps1 -BepInExRoot <profile>
    .\REMOVE_TEST.ps1 -BepInExRoot <profile>
#>
param(
    [string]$GameDir = "",
    [string]$BepInExRoot = ""
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Roots([string]$Explicit, [string]$Game) {
    if ($Explicit) {
        if (-not (Test-Path (Join-Path $Explicit "BepInEx\core\BepInEx.dll"))) {
            throw "-BepInExRoot '$Explicit' does not contain BepInEx\\core\\BepInEx.dll."
        }
        return ,(Resolve-Path $Explicit).Path
    }
    $roots = @()
    if ($Game -and (Test-Path (Join-Path $Game "BepInEx\core\BepInEx.dll"))) { $roots += (Resolve-Path $Game).Path }
    $profiles = Join-Path $env:APPDATA "r2modmanPlus-local\Erenshor\profiles"
    if (Test-Path $profiles) {
        Get-ChildItem $profiles -Directory | ForEach-Object {
            if (Test-Path (Join-Path $_.FullName "BepInEx\core\BepInEx.dll")) { $roots += $_.FullName }
        }
    }
    return @($roots | Select-Object -Unique)
}

Write-Host "=== Erenshor Crafting Expanded - one-shot build + install (NO backup) ===" -ForegroundColor Cyan
$roots = @(Find-Roots $BepInExRoot $GameDir)
if ($roots.Count -eq 0) { throw "No BepInEx profile found. Pass -BepInExRoot explicitly." }
if ($roots.Count -gt 1 -and -not $BepInExRoot) {
    Write-Host "Multiple BepInEx profiles found - refusing to guess. Re-run with -BepInExRoot:" -ForegroundColor Red
    $roots | ForEach-Object { Write-Host "  $_" }
    throw "Ambiguous install target."
}
$InstallRoot = if ($BepInExRoot) { (Resolve-Path $BepInExRoot).Path } else { $roots[0] }

$buildArgs = @("-BepInExRoot", $InstallRoot)
if ($GameDir) { $buildArgs += @("-GameDir", $GameDir) }
& (Join-Path $ScriptRoot "BUILD.ps1") @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed; install not attempted." }

$builtDll = Join-Path $ScriptRoot "bin\ErenshorCraftingExpanded.dll"
if (-not (Test-Path $builtDll)) { throw "BUILD.ps1 completed but output DLL was not found: $builtDll" }
$pluginDir = Join-Path $InstallRoot "BepInEx\plugins\ErenshorCraftingExpanded"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
$out = Join-Path $pluginDir "ErenshorCraftingExpanded.dll"
Copy-Item $builtDll $out -Force

Write-Host "Installed Erenshor Crafting Expanded to $out" -ForegroundColor Green
Write-Host "WARNING: this one-shot path did not back up a prior install. Use INSTALL_TEST.ps1 for reversible testing." -ForegroundColor Yellow
