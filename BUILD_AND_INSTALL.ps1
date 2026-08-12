param(
    [string]$GameDir = "",
    [string]$BepInExRoot = ""
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Erenshor {
    param([string]$Explicit)
    if ($Explicit -and (Test-Path (Join-Path $Explicit "Erenshor.exe"))) { return (Resolve-Path $Explicit).Path }

    $candidates = New-Object System.Collections.Generic.List[string]
    if (${env:ProgramFiles(x86)}) { $candidates.Add((Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\Erenshor")) }
    if ($env:ProgramFiles) { $candidates.Add((Join-Path $env:ProgramFiles "Steam\steamapps\common\Erenshor")) }

    $steamRoots = @()
    if (${env:ProgramFiles(x86)}) { $steamRoots += (Join-Path ${env:ProgramFiles(x86)} "Steam") }
    if ($env:ProgramFiles) { $steamRoots += (Join-Path $env:ProgramFiles "Steam") }
    foreach ($steamRoot in $steamRoots) {
        $vdf = Join-Path $steamRoot "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            $content = Get-Content $vdf -Raw
            [regex]::Matches($content, '"path"\s+"([^"]+)"') | ForEach-Object {
                $library = $_.Groups[1].Value -replace '\\\\','\'
                # Steam can retain old library entries for drives that no longer exist.
                # Avoid Join-Path/Test-Path on a missing PowerShell drive because
                # $ErrorActionPreference = "Stop" would abort the installer.
                if ([System.IO.Directory]::Exists($library)) {
                    $candidates.Add([System.IO.Path]::Combine($library, "steamapps", "common", "Erenshor"))
                }
            }
        }
    }
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path (Join-Path $candidate "Erenshor.exe")) { return (Resolve-Path $candidate).Path }
    }
    $manual = Read-Host "Could not auto-find Erenshor. Paste the folder containing Erenshor.exe"
    if ($manual -and (Test-Path (Join-Path $manual "Erenshor.exe"))) { return (Resolve-Path $manual).Path }
    throw "Erenshor installation not found."
}

function Find-BepInExRoots {
    param([string]$Explicit, [string]$DetectedGameDir)
    $roots = New-Object System.Collections.Generic.List[string]
    if ($Explicit -and (Test-Path (Join-Path $Explicit "BepInEx\core\BepInEx.dll"))) { $roots.Add((Resolve-Path $Explicit).Path) }
    if (Test-Path (Join-Path $DetectedGameDir "BepInEx\core\BepInEx.dll")) { $roots.Add((Resolve-Path $DetectedGameDir).Path) }

    $profileParents = @(
        (Join-Path $env:APPDATA "r2modmanPlus-local\Erenshor\profiles"),
        (Join-Path $env:APPDATA "Thunderstore Mod Manager\DataFolder\Erenshor\profiles")
    )
    foreach ($parent in $profileParents) {
        if (Test-Path $parent) {
            Get-ChildItem $parent -Directory -ErrorAction SilentlyContinue | ForEach-Object {
                if (Test-Path (Join-Path $_.FullName "BepInEx\core\BepInEx.dll")) { $roots.Add($_.FullName) }
            }
        }
    }
    return @($roots | Select-Object -Unique)
}

function Choose-BepInExRoot {
    param([string[]]$Roots)
    if ($Roots.Count -eq 0) {
        Write-Host ""
        Write-Host "No Erenshor BepInEx profile was found." -ForegroundColor Yellow
        Write-Host "In r2modman: choose Erenshor, make a profile, install BepInExPack, launch Modded once, then rerun this script."
        throw "BepInEx not found."
    }
    if ($Roots.Count -eq 1) { return $Roots[0] }
    Write-Host "Found multiple BepInEx profiles:" -ForegroundColor Cyan
    for ($i = 0; $i -lt $Roots.Count; $i++) { Write-Host "[$i] $($Roots[$i])" }
    $choice = Read-Host "Choose the profile number for DeepSims"
    $index = 0
    if (-not [int]::TryParse($choice, [ref]$index) -or $index -lt 0 -or $index -ge $Roots.Count) { throw "Invalid profile selection." }
    return $Roots[$index]
}

function Find-Csc {
    $paths = @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )
    foreach ($path in $paths) { if (Test-Path $path) { return $path } }
    throw "Could not find csc.exe. Install .NET Framework 4.8 Developer Pack or Visual Studio Build Tools, then rerun."
}

$GameDir = Find-Erenshor $GameDir
$roots = Find-BepInExRoots $BepInExRoot $GameDir
$InstallRoot = Choose-BepInExRoot $roots
$Csc = Find-Csc
$Managed = Join-Path $GameDir "Erenshor_Data\Managed"
$BepCore = Join-Path $InstallRoot "BepInEx\core"
if (-not (Test-Path (Join-Path $Managed "Assembly-CSharp.dll"))) { throw "Assembly-CSharp.dll not found under $Managed" }

$PluginDir = Join-Path $InstallRoot "BepInEx\plugins\DeepSims"
$MemoryDir = Join-Path $InstallRoot "BepInEx\config\DeepSims\Memory"
New-Item -ItemType Directory -Force -Path $PluginDir, $MemoryDir | Out-Null

$Refs = New-Object System.Collections.Generic.List[string]
$requiredRefs = @(
    (Join-Path $BepCore "BepInEx.dll"),
    (Join-Path $BepCore "0Harmony.dll"),
    (Join-Path $Managed "Assembly-CSharp.dll"),
    (Join-Path $Managed "UnityEngine.JSONSerializeModule.dll")
)
foreach ($r in $requiredRefs) { if (-not (Test-Path $r)) { throw "Required reference not found: $r" } else { $Refs.Add($r) } }

$optionalRefs = @(
    (Join-Path $Managed "UnityEngine.AIModule.dll"),
    (Join-Path $Managed "UnityEngine.InputLegacyModule.dll"),
    (Join-Path $Managed "UnityEngine.PhysicsModule.dll"),
    (Join-Path $Managed "UnityEngine.AnimationModule.dll"),
    (Join-Path $Managed "UnityEngine.dll"),
    (Join-Path $Managed "UnityEngine.CoreModule.dll"),
    (Join-Path $Managed "UnityEngine.UIModule.dll"),
    (Join-Path $Managed "Unity.TextMeshPro.dll"),
    (Join-Path $Managed "UnityEngine.UI.dll"),
    (Join-Path $Managed "netstandard.dll")
)
foreach ($r in $optionalRefs) { if (Test-Path $r) { $Refs.Add($r) } }

$OutDll = Join-Path $PluginDir "ErenshorDeepSims.dll"
$Rsp = Join-Path $env:TEMP "ErenshorDeepSims-v061.rsp"
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("/nologo")
$lines.Add("/target:library")
$lines.Add("/optimize+")
$lines.Add('/out:"' + $OutDll + '"')
foreach ($r in ($Refs | Select-Object -Unique)) { $lines.Add('/reference:"' + $r + '"') }
Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" | ForEach-Object { $lines.Add('"' + $_.FullName + '"') }
# Cross-mod contract conformance tests, shared with Erenshor PvP and Erenshor Nemesis. Optional so
# a source tree without the sibling mods still builds; /dsguardtest simply covers less without it.
$SharedDir = Join-Path $ScriptRoot "shared"
if (Test-Path $SharedDir) {
    $lines.Add("/define:SHARED_CONTRACTS")
    Get-ChildItem $SharedDir -Filter "*.cs" | ForEach-Object { $lines.Add('"' + $_.FullName + '"') }
}
$lines | Set-Content -Path $Rsp -Encoding ASCII

Write-Host "Building DeepSims 0.7.0 against your installed Erenshor..." -ForegroundColor Cyan
& $Csc "@$Rsp"
if ($LASTEXITCODE -ne 0) { throw "Compilation failed. Copy the compiler errors and send them to me." }

Write-Host ""
Write-Host "DeepSims 0.7.0 installed." -ForegroundColor Green
Write-Host "  Plugin: $OutDll"
Write-Host "  Memory: $MemoryDir"
Write-Host "  BepInEx profile: $InstallRoot"

Write-Host ""
Write-Host "No CustomSimFramework dependency is required for this prototype."
Write-Host "0.7.0 includes native chat styling, social expression routing, Social History, and performance diagnostics."
Write-Host "Recommended model is qwen3.5:2b. After launch, use /aistatus, /dsims, /dssession, /dsmemory <Sim>, and optionally /dsnews latest expansion, then simply talk with your group using /p. /dw, /dstalk, and /dsbanter remain debug tools."
Write-Host "If /dsims cannot find your party, type /dsinspect and send me party-diagnostic.txt."

function Invoke-OptionalModBuild {
    param(
        [string]$Name,
        [string]$Directory
    )

    $build = Join-Path $Directory "BUILD_AND_INSTALL.ps1"
    if (-not (Test-Path -LiteralPath $Directory)) {
        Write-Host "Skipping $Name (directory not present)." -ForegroundColor DarkGray
        return
    }
    if (-not (Test-Path -LiteralPath $build)) {
        Write-Host "Skipping $Name (BUILD_AND_INSTALL.ps1 not present)." -ForegroundColor Yellow
        return
    }

    Write-Host ""
    Write-Host "Building optional mod: $Name" -ForegroundColor Cyan
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $build `
        -GameDir $GameDir -BepInExRoot $InstallRoot
    if ($LASTEXITCODE -ne 0) {
        throw "$Name build failed."
    }
}

# Keep one-click installs coherent when sibling mod directories are present. Each
# sibling remains standalone and receives the already-selected game/profile paths.
Invoke-OptionalModBuild "Erenshor Follow" (Join-Path $ScriptRoot "ErenshorFollow")
Invoke-OptionalModBuild "Practice Duels" (Join-Path $ScriptRoot "ErenshorDuel")
Invoke-OptionalModBuild "Erenshor PvP" (Join-Path $ScriptRoot "Erenshor-PvP")
Invoke-OptionalModBuild "Erenshor Nemesis" (Join-Path $ScriptRoot "Erenshor-Nemesis")
Invoke-OptionalModBuild "Erenshor Party Tools" (Join-Path $ScriptRoot "Erenshor-Party-Tools")
Invoke-OptionalModBuild "Erenshor Campmaster" (Join-Path $ScriptRoot "ErenShorCampRelax")
