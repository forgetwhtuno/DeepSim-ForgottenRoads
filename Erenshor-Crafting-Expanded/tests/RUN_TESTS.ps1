$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Csc {
    foreach ($path in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) {
        if (Test-Path $path) { return $path }
    }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

$csc = Find-Csc
$out = Join-Path $env:TEMP "ErenshorCraftingExpanded.Tests.exe"

# Only the pure-logic files (no UnityEngine/BepInEx dependency) plus their tests, matching the
# repo-wide convention (see Erenshor-Party-Tools/RUN_TESTS.ps1) of keeping domain logic
# testable outside the game.
& $csc /nologo /target:exe ("/out:{0}" -f $out) `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingRecipeInfo.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftableCountPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingProgression.cs") `
    (Join-Path $ScriptRoot "..\src\Compatibility\SimIdentitySnapshot.cs") `
    (Join-Path $ScriptRoot "..\src\Commissions\CommissionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\UI\CraftingPanelPositioning.cs") `
    (Join-Path $ScriptRoot "..\src\UI\CraftingUiState.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeDefinition.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeCatalog.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeState.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingRuntimeConfigValidation.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingScanPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Items\CraftingExpandedItemIds.cs") `
    (Join-Path $ScriptRoot "..\src\Items\CustomItemDefinition.cs") `
    (Join-Path $ScriptRoot "..\src\Items\CustomItemRegistry.cs") `
    (Join-Path $ScriptRoot "RunAllTests.cs")
if ($LASTEXITCODE -ne 0) {
    throw "Test compilation failed."
}

try {
    & $out
    if ($LASTEXITCODE -ne 0) {
        throw "Erenshor Crafting Expanded tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item $out -Force -ErrorAction SilentlyContinue
}
