param(
    [string]$CscPath = ""
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

function Find-Csc {
    if ($CscPath -and (Test-Path $CscPath)) { return (Resolve-Path $CscPath).Path }
    $candidates = @(
        (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
        (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
    )
    foreach ($candidate in $candidates) { if (Test-Path $candidate) { return $candidate } }
    throw "csc.exe was not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

$csc = Find-Csc
$framework = Split-Path -Parent $csc
$webExtensions = Join-Path $framework "System.Web.Extensions.dll"
if (-not (Test-Path $webExtensions)) { throw "System.Web.Extensions.dll was not found beside $csc" }

$sourceFiles = @(
    (Join-Path $repoRoot "src\Models.cs"),
    (Join-Path $repoRoot "src\RelationshipModel.cs"),
    (Join-Path $repoRoot "src\SocialFoundation.cs"),
    (Join-Path $repoRoot "src\GroupMessageQueue.cs"),
    (Join-Path $repoRoot "src\ConversationHistory.cs"),
    (Join-Path $repoRoot "src\RelaxSocialPolicy.cs"),
    (Join-Path $repoRoot "src\DuelSocialSemantics.cs"),
    (Join-Path $repoRoot "src\EventConversationDirector.cs"),
    (Join-Path $repoRoot "src\JsonUtil.cs"),
    (Join-Path $repoRoot "src\MemoryStore.cs"),
    (Join-Path $repoRoot "src\SessionTelemetry.cs"),
    (Join-Path $repoRoot "src\GroundingGuard.cs"),
    (Join-Path $repoRoot "src\PromptBuilder.cs"),
    (Join-Path $repoRoot "src\ExternalNewsClient.cs"),
    (Join-Path $repoRoot "src\NetworkTimeoutHelper.cs"),
    (Join-Path $repoRoot "src\ChatCommandParser.cs"),
    (Join-Path $repoRoot "src\TravelCommandGrammar.cs"),
    (Join-Path $repoRoot "src\WikiClient.cs"),
    (Join-Path $repoRoot "src\ConversationSeeds.cs"),
    (Join-Path $repoRoot "src\ConversationSeedTests.cs"),
    (Join-Path $repoRoot "src\DeterministicRegressionTests.cs"),
    (Join-Path $repoRoot "src\QualityReliabilityDeterministicTests.cs"),
    (Join-Path $repoRoot "src\ConversationTurnGuard.cs"),
    (Join-Path $repoRoot "src\ConversationTurnGuardTests.cs"),
    (Join-Path $repoRoot "src\ConversationPacingTests.cs"),
    (Join-Path $scriptRoot "FrameworkStubs.cs"),
    (Join-Path $scriptRoot "ChatRoutingRegression.cs"),
    (Join-Path $scriptRoot "StandaloneRegressionMain.cs")
)
foreach ($source in $sourceFiles) { if (-not (Test-Path $source)) { throw "Regression source missing: $source" } }

$outputDir = Join-Path $env:TEMP ("DeepSimsRegression-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $outputDir | Out-Null
try {
    $output = Join-Path $outputDir "DeepSimsRegressionTests.exe"
    $arguments = @("/nologo", "/target:exe", "/optimize+", ('/out:"{0}"' -f $output), ('/reference:"{0}"' -f $webExtensions)) + $sourceFiles
    & $csc $arguments
    if ($LASTEXITCODE -ne 0) { throw "Regression test compilation failed." }
    & $output
    exit $LASTEXITCODE
}
finally {
    if (Test-Path -LiteralPath $outputDir) { Remove-Item -LiteralPath $outputDir -Recurse -Force }
}
