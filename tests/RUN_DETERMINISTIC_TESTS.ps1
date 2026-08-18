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
    (Join-Path $repoRoot "src\DeepSimsLog.cs"),
    (Join-Path $repoRoot "src\DiagnosticPrivacy.cs"),
    (Join-Path $repoRoot "src\DiagnosticPrivacyTests.cs"),
    (Join-Path $repoRoot "src\CharacterScopeKey.cs"),
    (Join-Path $repoRoot "src\CharacterScopeWriteGuard.cs"),
    (Join-Path $repoRoot "src\CharacterScopeDeterministicTests.cs"),
    (Join-Path $repoRoot "src\DeepSimsControlPolicy.cs"),
    (Join-Path $repoRoot "src\DeepSimsControlPolicyTests.cs"),
    (Join-Path $repoRoot "src\Models.cs"),
    (Join-Path $repoRoot "src\LivePartyFacts.cs"),
    (Join-Path $repoRoot "src\PartyStanceGuard.cs"),
    (Join-Path $repoRoot "src\PartyGroundingRequestContext.cs"),
    (Join-Path $repoRoot "src\DeepSlotSelectionPolicy.cs"),
    (Join-Path $repoRoot "src\LivePartyGroundingTests.cs"),
    (Join-Path $repoRoot "src\RelationshipModel.cs"),
    (Join-Path $repoRoot "src\SocialFoundation.cs"),
    (Join-Path $repoRoot "src\GroupMessageQueue.cs"),
    (Join-Path $repoRoot "src\ConversationHistory.cs"),
    (Join-Path $repoRoot "src\SocialSession.cs"),
    (Join-Path $repoRoot "src\RelaxSocialPolicy.cs"),
    (Join-Path $repoRoot "src\DuelSocialSemantics.cs"),
    (Join-Path $repoRoot "src\EventConversationDirector.cs"),
    (Join-Path $repoRoot "src\JsonUtil.cs"),
    (Join-Path $repoRoot "src\MemoryStore.cs"),
    (Join-Path $repoRoot "src\SessionTelemetry.cs"),
    (Join-Path $repoRoot "src\GroundingGuard.cs"),
    (Join-Path $repoRoot "src\RoleplayPerspective.cs"),
    (Join-Path $repoRoot "src\RoleplayDeterministicTests.cs"),
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
    (Join-Path $repoRoot "src\PromptCaptureModel.cs"),
    (Join-Path $repoRoot "src\PromptCapturePacket.cs"),
    (Join-Path $repoRoot "src\PromptCaptureWriter.cs"),
    (Join-Path $repoRoot "src\PromptCaptureScope.cs"),
    (Join-Path $repoRoot "src\PromptCaptureDeterministicTests.cs"),
    (Join-Path $repoRoot "src\DeepSimsModelResolution.cs"),
    (Join-Path $repoRoot "src\DeepSimsModelResolutionTests.cs"),
    (Join-Path $repoRoot "src\ShouldReplyDeterministic.cs"),
    (Join-Path $repoRoot "src\SimResponseDecision.cs"),
    (Join-Path $repoRoot "src\RecentEventQuestionPolicy.cs"),
    (Join-Path $scriptRoot "FrameworkStubs.cs"),
    (Join-Path $scriptRoot "ChatRoutingRegression.cs"),
    (Join-Path $scriptRoot "StandaloneRegressionMain.cs")
)
foreach ($source in $sourceFiles) { if (-not (Test-Path $source)) { throw "Regression source missing: $source" } }

# Prompt-capture packaging/publication exclusion guard. Captured packets are a private diagnostic
# dataset: they must never be committable and never packageable.
$gitignore = Get-Content (Join-Path $repoRoot ".gitignore") -Raw
if ($gitignore -notmatch 'Diagnostics') {
    throw "Prompt capture exclusion guard failed: .gitignore does not exclude the Diagnostics directory."
}
$captureSourceFiles = @("PromptCaptureModel.cs", "PromptCapturePacket.cs", "PromptCaptureWriter.cs", "PromptCaptureScope.cs")
foreach ($file in $captureSourceFiles) {
    if (-not (Test-Path (Join-Path $repoRoot "src\$file"))) { throw "Prompt capture source missing: $file" }
}
# The capture root must be derived from the mod's existing path abstraction, never hard-coded.
$pathsSource = Get-Content (Join-Path $repoRoot "src\DeepSimsPaths.cs") -Raw
if ($pathsSource -notmatch 'PromptCaptureRoot' -or $pathsSource -match 'C:\\\\Users') {
    throw "Prompt capture path guard failed: capture root missing or an absolute user path was hard-coded."
}
# Capture must remain opt-in.
$settingsSource = Get-Content (Join-Path $repoRoot "src\DeepSimsSettings.cs") -Raw
if ($settingsSource -notmatch 'PromptCaptureEnabled\s*=\s*false') {
    throw "Prompt capture default guard failed: PromptCaptureEnabled is not false by default."
}
# The suite release whitelist packages an explicit file allowlist; assert Deep Sims never names a
# diagnostics/capture artifact in it.
$whitelistPath = Join-Path (Split-Path -Parent (Split-Path -Parent $repoRoot)) "Erenshor-Mod-Suite\release-whitelist.json"
if (Test-Path $whitelistPath) {
    $whitelistText = Get-Content $whitelistPath -Raw
    if ($whitelistText -match 'Diagnostics' -or $whitelistText -match 'PromptCapture') {
        throw "Prompt capture packaging guard failed: release whitelist references a capture artifact."
    }
    Write-Host "PASS: prompt capture is excluded from git and from release packaging" -ForegroundColor Green
}
else {
    Write-Host "PASS: prompt capture git exclusion verified (suite whitelist not present in this checkout)" -ForegroundColor Green
}

# ---------------------------------------------------------------------------------------------
# Single-model pipeline guards.
#
# Deep Sims must request exactly ONE Ollama model for every call. These are structural/source
# guards rather than call-site mocks: DeepSimsPlugin is too large to instantiate outside Unity,
# so the funnel property is proven instead - if TimedChatAsync and ClassifySemanticTurnAsync are
# the ONLY two call sites of _ollama.ChatAsync(, and BOTH resolve their model exclusively through
# ResolvedModel/DeepSimsModelResolution, then EVERY production caller (classifier, direct reply,
# autonomous, connected Sim-to-Sim, /dsbanter, factual/retrieval, ordinary retry, quality/
# grounding retry, guard regeneration) inherits the single-model guarantee for free, and so does
# every retry/fallback attempt inside OllamaClient.ChatAsync since they all reuse the same
# `model` parameter passed in from that one call.
# ---------------------------------------------------------------------------------------------
$pluginSource = Get-Content (Join-Path $repoRoot "src\DeepSimsPlugin.cs") -Raw
$ollamaClientSource = Get-Content (Join-Path $repoRoot "src\OllamaClient.cs") -Raw
$modelResolutionSource = Get-Content (Join-Path $repoRoot "src\DeepSimsModelResolution.cs") -Raw

# Exactly two call sites request an Ollama chat completion; both must exist.
$chatAsyncCallSites = [regex]::Matches($pluginSource, '_ollama\.ChatAsync\(')
if ($chatAsyncCallSites.Count -ne 2) {
    throw "Single-model guard failed: expected exactly 2 production _ollama.ChatAsync( call sites (classifier + TimedChatAsync), found $($chatAsyncCallSites.Count)."
}

# TimedChatAsync must resolve its model through ResolvedModel and must never independently branch
# to a second configured model string.
$timedChatMethod = [regex]::Match($pluginSource, 'private\s+async\s+Task<string>\s+TimedChatAsync\(List<ChatMessage>\s+messages,\s+bool\s+preferStrongModel\)[\s\S]*?\n        \}')
if (-not $timedChatMethod.Success) { throw "Single-model guard failed: could not locate TimedChatAsync(messages, preferStrongModel)." }
if ($timedChatMethod.Value -notmatch 'string\s+requestModel\s*=\s*ResolvedModel;') {
    throw "Single-model guard failed: TimedChatAsync no longer resolves requestModel from ResolvedModel."
}
foreach ($forbidden in @('ReasoningModelConfig', 'primaryModel', 'configuredReasoningModel', 'useReasoningModel', 'qwen3.5:2b')) {
    if ($timedChatMethod.Value -match [regex]::Escape($forbidden)) {
        throw "Single-model guard failed: TimedChatAsync still references '$forbidden' - a second model can no longer be selected here."
    }
}
if (([regex]::Matches($timedChatMethod.Value, '_ollama\.ChatAsync\(')).Count -ne 1) {
    throw "Single-model guard failed: TimedChatAsync must call _ollama.ChatAsync exactly once (no fallback-to-a-different-model retry)."
}

# ClassifySemanticTurnAsync must resolve its model through ResolvedModel, never through an
# independent "qwen3.5:2b" default.
$classifierMethod = [regex]::Match($pluginSource, 'private\s+async\s+Task<SemanticTurnRoute>\s+ClassifySemanticTurnAsync\([\s\S]*?\n        \}')
if (-not $classifierMethod.Success) { throw "Single-model guard failed: could not locate ClassifySemanticTurnAsync." }
if ($classifierMethod.Value -notmatch 'string\s+model\s*=\s*ResolvedModel;') {
    throw "Single-model guard failed: ClassifySemanticTurnAsync no longer resolves its model from ResolvedModel."
}
if ($classifierMethod.Value -match 'qwen3\.5:2b') {
    throw "Single-model guard failed: ClassifySemanticTurnAsync still hardcodes a qwen3.5:2b default."
}

# ResolvedModel itself must be the single seam that reads ModelConfig for live use, and must not
# read ReasoningModelConfig at all (only the one-time migration block may do that).
$resolvedModelProperty = [regex]::Match($pluginSource, 'internal\s+string\s+ResolvedModel[\s\S]*?\n        \}')
if (-not $resolvedModelProperty.Success) { throw "Single-model guard failed: ResolvedModel property not found." }
if ($resolvedModelProperty.Value -match 'ReasoningModelConfig') {
    throw "Single-model guard failed: ResolvedModel must not depend on ReasoningModelConfig."
}

# The one-time migration is the ONLY place ReasoningModelConfig may still be read for model
# purposes, plus the /aimodel command keeping the legacy field in sync. Enumerate every remaining
# reference and require each to be inside an allowed context.
$reasoningModelRefs = [regex]::Matches($pluginSource, '.*ReasoningModelConfig.*')
foreach ($lineMatch in $reasoningModelRefs) {
    $line = $lineMatch.Value
    $allowed = $line -match 'internal DeepSimsConfigEntry<string> ReasoningModelConfig;' -or
               $line -match 'ReasoningModelConfig = new DeepSimsConfigEntry' -or
               $line -match 'DeepSimsModelResolution\.Resolve\(ModelConfig\.Value, ReasoningModelConfig\.Value\)' -or
               $line -match 'ReasoningModelConfig\.Value = migratedModel' -or
               $line -match 'if \(!string\.Equals\(ReasoningModelConfig\.Value, migratedModel' -or
               $line -match 'if \(ReasoningModelConfig != null\) ReasoningModelConfig\.Value = ModelConfig\.Value;'
    if (-not $allowed) {
        throw "Single-model guard failed: unexpected ReasoningModelConfig reference outside the one-time migration/legacy-sync paths: $line"
    }
}
Write-Host "PASS: exactly one Ollama call site each for classification and generation, both resolve through ResolvedModel" -ForegroundColor Green
Write-Host "PASS: ReasoningModelConfig is read only by the one-time migration and the /aimodel legacy-sync" -ForegroundColor Green

# Source-wide audit: the deprecated model literal may only appear in the resolver (as the frozen
# legacy sentinel), its own tests, and this guard file's own pattern strings.
$srcDir = Join-Path $repoRoot "src"
$allSourceFiles = Get-ChildItem $srcDir -Filter "*.cs"
$allowedLegacyLiteralFiles = @("DeepSimsModelResolution.cs", "DeepSimsModelResolutionTests.cs", "DeepSimsSettings.cs", "PromptCaptureDeterministicTests.cs")
foreach ($file in $allSourceFiles) {
    if ($allowedLegacyLiteralFiles -contains $file.Name) { continue }
    $text = Get-Content $file.FullName -Raw
    if ($text -match 'qwen3\.5:2b') {
        throw "Single-model source audit failed: deprecated model literal 'qwen3.5:2b' found outside the resolver/tests/legacy-default doc comment: $($file.Name)"
    }
}
# DeepSimsSettings.cs may only mention it as ReasoningModel's OWN historical default value (legacy
# compatibility field), never as Model's default.
$settingsSourceForAudit = Get-Content (Join-Path $srcDir "DeepSimsSettings.cs") -Raw
if ($settingsSourceForAudit -match 'public\s+string\s+Model\s*=\s*"qwen3\.5:2b"') {
    throw "Single-model source audit failed: DeepSimsSettings.Model still defaults to the deprecated qwen3.5:2b."
}
if ($settingsSourceForAudit -notmatch 'public\s+string\s+Model\s*=\s*"qwen3\.5:4b"') {
    throw "Single-model source audit failed: DeepSimsSettings.Model does not default to the canonical qwen3.5:4b."
}
Write-Host "PASS: qwen3.5:2b appears only as the resolver's frozen legacy sentinel; Model defaults to qwen3.5:4b" -ForegroundColor Green

# The canonical constant used by the resolver/tests must actually be qwen3.5:4b, not merely
# "whatever ReasoningModel happens to default to" - this guards against the constant silently
# drifting away from the desired model in a future edit.
if ($modelResolutionSource -notmatch 'CanonicalModel\s*=\s*"qwen3\.5:4b"') {
    throw "Single-model guard failed: DeepSimsModelResolution.CanonicalModel is not qwen3.5:4b."
}

# The Ollama client's own retry ladder (post-load / expanded-budget / flattened fallback) must
# keep reusing the single `model` parameter passed into ChatAsync - never a second variable.
$chatAsyncMethod = [regex]::Match($ollamaClientSource, 'internal\s+Task<string>\s+ChatAsync\([^\)]*PromptCapturePacket capture\)[\s\S]*?\n        \}');
if (-not $chatAsyncMethod.Success) { throw "Single-model guard failed: could not locate OllamaClient.ChatAsync(..., PromptCapturePacket capture)." }
$sendChatCalls = [regex]::Matches($chatAsyncMethod.Value, 'SendChat\(endpoint,\s*(\w+),')
if ($sendChatCalls.Count -lt 4) {
    throw "Single-model guard failed: expected at least 4 SendChat attempts (primary/post-load/expanded/flattened) inside ChatAsync, found $($sendChatCalls.Count)."
}
foreach ($call in $sendChatCalls) {
    if ($call.Groups[1].Value -ne "model") {
        throw "Single-model guard failed: a retry attempt inside ChatAsync passes '$($call.Groups[1].Value)' instead of the single 'model' parameter."
    }
}
Write-Host "PASS: every OllamaClient retry/fallback attempt reuses the single model parameter" -ForegroundColor Green

# PromptCapture must record configuredModel/resolvedModel alongside serializedRequest.model so a
# captured session can prove the single-model invariant rather than merely assume it.
$capturePacketSource = Get-Content (Join-Path $srcDir "PromptCapturePacket.cs") -Raw
foreach ($token in @('"configuredModel"', '"resolvedModel"', 'ConfiguredModel')) {
    if ($capturePacketSource -notmatch [regex]::Escape($token)) {
        throw "Single-model guard failed: PromptCapture packet/serializer is missing $token."
    }
}
Write-Host "PASS: PromptCapture records configuredModel/resolvedModel for the single-model invariant check" -ForegroundColor Green

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
