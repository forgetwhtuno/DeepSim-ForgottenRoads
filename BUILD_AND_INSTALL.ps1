param(
    [string]$GameDir = "",
    [string]$LunarisLibDir = "",
    [switch]$BuildCompanionMods
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

function Find-Csc {
    $paths = @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )
    foreach ($path in $paths) { if ($path -and (Test-Path $path)) { return $path } }
    throw "Could not find csc.exe. Install .NET Framework 4.8 Developer Pack or Visual Studio Build Tools, then rerun."
}

function Find-LunarisLibDir {
    param([string]$Explicit, [string]$DetectedGameDir)

    $candidates = New-Object System.Collections.Generic.List[string]
    if ($Explicit) { $candidates.Add($Explicit) }
    $candidates.Add((Join-Path $ScriptRoot "LunarisLibs"))
    $candidates.Add($DetectedGameDir)

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (-not $candidate) { continue }
        $lunaris = Join-Path $candidate "Lunaris.dll"
        $harmony = Join-Path $candidate "0Harmony.dll"
        if ((Test-Path $lunaris) -and (Test-Path $harmony)) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "Could not find Lunaris developer references. Put Lunaris.dll and 0Harmony.dll in '$ScriptRoot\LunarisLibs' or pass -LunarisLibDir."
}

function Add-ReferenceIfPresent {
    param([System.Collections.Generic.List[string]]$List, [string]$Path)
    if ($Path -and (Test-Path $Path)) { $List.Add((Resolve-Path $Path).Path) }
}

function Invoke-OptionalModBuild {
    param([string]$Name, [string]$Directory, [string]$DetectedGameDir)

    if (-not $BuildCompanionMods) { return }
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
    Write-Host "Building optional companion mod: $Name" -ForegroundColor Cyan
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $build -GameDir $DetectedGameDir
    if ($LASTEXITCODE -ne 0) { throw "$Name build failed." }
}

$GameDir = Find-Erenshor $GameDir
$LunarisLibDir = Find-LunarisLibDir $LunarisLibDir $GameDir
$Csc = Find-Csc
$Managed = Join-Path $GameDir "Erenshor_Data\Managed"
$PluginRoot = Join-Path $GameDir "plugins"
$ConfigRoot = Join-Path $PluginRoot "config"
$MemoryDir = Join-Path $ConfigRoot "DeepSims\Memory"

$AssemblyCSharp = Join-Path $Managed "Assembly-CSharp.dll"
$JsonModule = Join-Path $Managed "UnityEngine.JSONSerializeModule.dll"
$LunarisDll = Join-Path $LunarisLibDir "Lunaris.dll"
$HarmonyDll = Join-Path $LunarisLibDir "0Harmony.dll"

foreach ($required in @($AssemblyCSharp, $JsonModule, $LunarisDll, $HarmonyDll)) {
    if (-not (Test-Path $required)) { throw "Required reference not found: $required" }
}

New-Item -ItemType Directory -Force -Path $PluginRoot, $ConfigRoot, $MemoryDir | Out-Null

$Refs = New-Object System.Collections.Generic.List[string]
foreach ($required in @($LunarisDll, $HarmonyDll, $AssemblyCSharp, $JsonModule)) { Add-ReferenceIfPresent $Refs $required }

$optionalNames = @(
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.AIModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.AnimationModule.dll",
    "UnityEngine.UIModule.dll",
    "UnityEngine.UI.dll",
    "UnityEngine.UnityWebRequestModule.dll",
    "UnityEngine.AudioModule.dll",
    "UnityEngine.ParticleSystemModule.dll",
    "Unity.TextMeshPro.dll",
    "netstandard.dll"
)
foreach ($name in $optionalNames) { Add-ReferenceIfPresent $Refs (Join-Path $Managed $name) }

$TempDir = Join-Path $env:TEMP ("ErenshorDeepSims-build-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $TempDir | Out-Null
$TempDll = Join-Path $TempDir "ErenshorDeepSims.dll"
$Rsp = Join-Path $TempDir "ErenshorDeepSims.rsp"
$OutDll = Join-Path $PluginRoot "ErenshorDeepSims.dll"

try {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("/nologo")
    $lines.Add("/target:library")
    $lines.Add("/optimize+")
    $lines.Add('/out:"' + $TempDll + '"')
    foreach ($r in ($Refs | Select-Object -Unique)) { $lines.Add('/reference:"' + $r + '"') }
    Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" | Sort-Object Name | ForEach-Object { $lines.Add('"' + $_.FullName + '"') }

    # Shared contract conformance tests are source-only and optional. They remain part of a normal
    # Deep Sims build when the directory is present, exactly as before the loader migration.
    $SharedDir = Join-Path $ScriptRoot "shared"
    if (Test-Path $SharedDir) {
        $lines.Add("/define:SHARED_CONTRACTS")
        Get-ChildItem $SharedDir -Filter "*.cs" | Sort-Object Name | ForEach-Object { $lines.Add('"' + $_.FullName + '"') }
    }
    $lines | Set-Content -Path $Rsp -Encoding ASCII

    $lunarisInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($LunarisDll)
    $lunarisHash = (Get-FileHash -Algorithm SHA256 -Path $LunarisDll).Hash.ToLowerInvariant()

    Write-Host "Building Deep Sims 0.7.1 as a native Lunaris plugin..." -ForegroundColor Cyan
    Write-Host "  Game:    $GameDir"
    Write-Host "  Lunaris: $LunarisDll"
    Write-Host "  Version: $($lunarisInfo.FileVersion)"
    Write-Host "  SHA256:  $lunarisHash"

    & $Csc "@$Rsp"
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed. Copy the compiler errors and send them to me." }
    if (-not (Test-Path $TempDll)) { throw "Compiler reported success but did not produce $TempDll" }

    # Copy only after a complete successful compile so Lunaris' file watcher never sees a partial DLL.
    Copy-Item -LiteralPath $TempDll -Destination $OutDll -Force

    Write-Host ""
    Write-Host "Deep Sims 0.7.1 installed as a native Lunaris plugin." -ForegroundColor Green
    Write-Host "  Plugin: $OutDll"
    Write-Host "  Config: $ConfigRoot\erenshordeepsims.lpcfg"
    Write-Host "  Memory: $MemoryDir"
    Write-Host ""
    Write-Host "Lunaris runtime libraries are NOT copied into the plugin folder by this script."
    Write-Host "Use /aistatus, /dsims, /dssession, /dsperf, and the normal Deep Sims chat commands after launch."
}
finally {
    if (Test-Path $TempDir) { Remove-Item -LiteralPath $TempDir -Recurse -Force -ErrorAction SilentlyContinue }
}

Invoke-OptionalModBuild "Erenshor Follow" (Join-Path $ScriptRoot "ErenshorFollow") $GameDir
Invoke-OptionalModBuild "Practice Duels" (Join-Path $ScriptRoot "ErenshorDuel") $GameDir
Invoke-OptionalModBuild "Erenshor PvP" (Join-Path $ScriptRoot "Erenshor-PvP") $GameDir
Invoke-OptionalModBuild "Erenshor Nemesis" (Join-Path $ScriptRoot "Erenshor-Nemesis") $GameDir
Invoke-OptionalModBuild "Erenshor Party Tools" (Join-Path $ScriptRoot "Erenshor-Party-Tools") $GameDir
Invoke-OptionalModBuild "Erenshor Campmaster" (Join-Path $ScriptRoot "ErenShorCampRelax") $GameDir
Invoke-OptionalModBuild "Erenshor Crafting Expanded" (Join-Path $ScriptRoot "Erenshor-Crafting-Expanded") $GameDir
