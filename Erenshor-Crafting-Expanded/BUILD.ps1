<#
.SYNOPSIS
  Build-only script for Erenshor Crafting Expanded. Never installs anything.

.DESCRIPTION
  Locates the installed Erenshor assemblies and the BepInEx core (for BepInEx.dll/0Harmony.dll
  reference purposes only - nothing under BepInEx is written to), compiles the mod, and places
  the output DLL under this mod's own bin\ folder. Stops on the first compiler error. Never
  touches any BepInEx plugins/config directory - use INSTALL_TEST.ps1 for that, separately and
  explicitly.

.PARAMETER GameDir
  Erenshor install directory. Auto-detected under Program Files if omitted.

.PARAMETER BepInExRoot
  A BepInEx root (contains BepInEx\core\BepInEx.dll) used only to source reference DLLs for
  compilation. Auto-detected via r2modman profiles if omitted. Nothing under this path is
  modified.

.OUTPUTS
  Prints the exact output DLL path and the exact Erenshor assembly path used.
#>
param(
    [string]$GameDir = "",
    [string]$BepInExRoot = ""
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Game([string]$Explicit) {
    if ($Explicit -and (Test-Path (Join-Path $Explicit "Erenshor.exe"))) { return (Resolve-Path $Explicit).Path }
    $candidates = @()
    if (${env:ProgramFiles(x86)}) { $candidates += Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\Erenshor" }
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles "Steam\steamapps\common\Erenshor" }
    foreach ($drive in @("C","D","E","F")) {
        $candidates += "${drive}:\SteamLibrary\steamapps\common\Erenshor"
    }
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path (Join-Path $candidate "Erenshor.exe")) { return (Resolve-Path $candidate).Path }
    }
    throw "Erenshor installation not found. Pass -GameDir 'C:\path\to\Erenshor'."
}

function Find-Roots([string]$Explicit, [string]$Game) {
    if ($Explicit -and (Test-Path (Join-Path $Explicit "BepInEx\core\BepInEx.dll"))) { return ,(Resolve-Path $Explicit).Path }
    $roots = @()
    if (Test-Path (Join-Path $Game "BepInEx\core\BepInEx.dll")) { $roots += (Resolve-Path $Game).Path }
    $profiles = Join-Path $env:APPDATA "r2modmanPlus-local\Erenshor\profiles"
    if (Test-Path $profiles) {
        Get-ChildItem $profiles -Directory | ForEach-Object {
            if (Test-Path (Join-Path $_.FullName "BepInEx\core\BepInEx.dll")) { $roots += $_.FullName }
        }
    }
    return @($roots | Select-Object -Unique)
}

function Find-Csc {
    $paths = @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )
    foreach ($path in $paths) { if (Test-Path $path) { return $path } }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

Write-Host "=== Erenshor Crafting Expanded - build only, no install ===" -ForegroundColor Cyan

$GameDir = Find-Game $GameDir
$roots = @(Find-Roots $BepInExRoot $GameDir)
if ($roots.Count -eq 0) { throw "No BepInEx installation found (checked the game dir and r2modman profiles). Pass -BepInExRoot explicitly." }
if ($roots.Count -gt 1 -and -not $BepInExRoot) {
    Write-Host "Multiple BepInEx installations found - refusing to choose reference assemblies implicitly. Re-run with -BepInExRoot:" -ForegroundColor Red
    $roots | ForEach-Object { Write-Host "  candidate: $_" }
    throw "Ambiguous BepInEx reference root."
}
$ReferenceRoot = if ($BepInExRoot) { (Resolve-Path $BepInExRoot).Path } else { $roots[0] }

$csc = Find-Csc
$managed = Join-Path $GameDir "Erenshor_Data\Managed"
$core = Join-Path $ReferenceRoot "BepInEx\core"
$outDir = Join-Path $ScriptRoot "bin"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir "ErenshorCraftingExpanded.dll"

$refs = @(
    (Join-Path $core "BepInEx.dll"),
    (Join-Path $core "0Harmony.dll"),
    (Join-Path $managed "Assembly-CSharp.dll"),
    (Join-Path $managed "netstandard.dll"),
    (Join-Path $managed "UnityEngine.dll"),
    (Join-Path $managed "UnityEngine.CoreModule.dll"),
    (Join-Path $managed "UnityEngine.AIModule.dll"),
    (Join-Path $managed "UnityEngine.AnimationModule.dll"),
    (Join-Path $managed "UnityEngine.PhysicsModule.dll"),
    (Join-Path $managed "UnityEngine.UI.dll"),
    (Join-Path $managed "UnityEngine.IMGUIModule.dll"),
    (Join-Path $managed "UnityEngine.TextRenderingModule.dll"),
    (Join-Path $managed "UnityEngine.InputLegacyModule.dll"),
    (Join-Path $managed "UnityEngine.JSONSerializeModule.dll")
)
foreach ($ref in $refs) {
    if (-not (Test-Path $ref)) { throw "Missing reference DLL: $ref" }
}

$sourceFiles = Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" -Recurse
if ($sourceFiles.Count -eq 0) { throw "No source files found under src\." }

$rsp = Join-Path $env:TEMP "ErenshorCraftingExpanded.build.rsp"
$lines = @('/nologo', '/target:library', '/optimize+', ('/out:"{0}"' -f $out))
$refs | ForEach-Object { $lines += ('/reference:"{0}"' -f $_) }
$sourceFiles | ForEach-Object { $lines += ('"' + $_.FullName + '"') }
$lines | Set-Content $rsp -Encoding ASCII

Write-Host "Erenshor assemblies: $managed"
Write-Host "BepInEx/Harmony refs: $core"
Write-Host "Compiling $($sourceFiles.Count) source file(s)..."

& $csc "@$rsp"
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed (csc exit code $LASTEXITCODE). No install attempted - there is nothing to install."
}

Write-Host ""
Write-Host "BUILD OK" -ForegroundColor Green
Write-Host "Output DLL:        $out"
Write-Host "Erenshor assembly: $(Join-Path $managed 'Assembly-CSharp.dll')"
Write-Host ""
Write-Host "Nothing was installed. Run INSTALL_TEST.ps1 separately and explicitly to test it in-game."
