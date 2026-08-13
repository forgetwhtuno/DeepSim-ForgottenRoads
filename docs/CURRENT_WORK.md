# Current Work — Deep Sims

## 2026-08-13 three-audit integration pass — SOURCE VERIFIED + TEST WIRED, INSTALLED FOR LIVE TEST

`DeepSims.patch` from `DeepSims-Full-Deep-Audit` applied against base `b21b4d4150e0b...` (current
HEAD matched exactly). `DeepSimsControlApi` normalized (`ApiVersion=1`, `ModuleId="deepsims"`,
`HasDedicatedPanel=false`, `IsPanelOpen=false`); new `DeepSimsSuiteAuraProvider` added exposing
`socialMode`/`activity`/`roleplay` settings and a `refreshStatus` action, status-only (no raw
memory/prompts/keys ever surfaced).

Two real bugs found and fixed while reconciling, not just ported: `RoleplayPerspective.cs` had a
`Regex.Escape(phrase).Replace(" ", @"\s+")` ordering bug — `Regex.Escape` escapes the space itself,
so the pattern could never match, silently disabling every multi-word reject phrase ("this game",
"on steam", "on discord", etc.) in the final output Roleplay guard. `SocialFoundation.cs`'s
`IdentityFact` regex matched the article "a" as its subject placeholder, misrouting "what is a
windblade?" as an identity question instead of `FactualGameQuestion`. Both fixed.

Legacy flat/global memory is preserved untouched on disk; new writes go to
`Memory/Characters/<key>/`; `HasLegacyGlobalMemory()` + an `Awake()` warning surface the migration
notice; a conservative one-time import only fires into the still-empty legacy-flat location and
never deletes/overwrites. Pending-HTTP-unload traced end-to-end through `EnqueueMainThread`'s
`_requestStopping` gate and `QueueGroupMessage`'s own check — **finding: INERT**, a late-finishing
background call cannot reach chat/UI/memory after unload. No `Abort()` plumbing was added, per
instruction.

Build: PASS against real installed assemblies, zero BepInEx references. Tests:
`RUN_DETERMINISTIC_TESTS.ps1` — **721/721 PASS**. Installed to the live game's `plugins\` folder
with the game closed. Nothing committed/pushed.

**NEEDS LIVE TEST**: duplicate-instance coexistence, character A/B memory-scope switch in-game, and
the 3–5x Ollama pending-request unload/re-enable stress test — all require the running game and
were not simulated.

**Audit snapshot:** 2026-08-13

This file records the state after the full Deep Sims source audit. Evidence labels are deliberate:

- **LIVE VERIFIED** — observed in the supplied 2026-08-13 Lunaris log.
- **SOURCE VERIFIED** — proved by tracing the current source.
- **TEST WIRED** — deterministic regression exists and is called by the standalone runner.
- **NEEDS LOCAL VERIFICATION** — this audit environment lacked the Windows C# compiler/runtime references needed to execute it.
- **NEEDS LIVE TEST** — requires Erenshor + Lunaris in game.

Do not upgrade SOURCE/TEST evidence to LIVE VERIFIED without a new live run.

## Base branch / SHA

- Branch audited: `agent/lunaris-native-deepsims`
- Exact audit base: `b21b4d4150e0baffcc2d48ec6b44d6489e057d86`
- `main` observed at audit start: `232a6e03a7b7d9bee7a7f36b7d03b7312f202351`
- The previous version of this document incorrectly listed `f75a54aba91400484315bb0d464b61830fe51415` as current HEAD. `b21b4d4` is the current draft-branch tip; its additional commit is documentation/audit-handoff work on top of `f75a54a`.

No commit, push, merge, reset, clean, or other Git write was performed by this audit.

## Live evidence carried into this audit

### Roleplay failure history — LIVE VERIFIED, pre-current-fix DLL

The supplied live log proves the perspective/config/prompt route was already working while final output enforcement was not:

- the model system prompt says the Sim is the in-world adventurer;
- diagnostics report `perspective=Roleplay` and `roleplayPromptApplied=True`;
- the same run reports `roleplayGuardApplied=False` on bad visible output;
- visible examples include `online again`, `playing NES`, `lmao`, `heh`, and `:D`.

Therefore the historical failure was not simply `/dsroleplay` failing to set state. It was a final-output/pipeline enforcement problem. The current guard work described below has **not** been live-tested in this audit.

### Dancer / Windblade failure — LIVE VERIFIED + SOURCE VERIFIED

The live log's PvP roster identifies `Dancer L12 Duelist/Striker`. Current Deep Sims normalizes native/internal `Duelist` to the current player-facing class `Windblade`. The same log shows Deep Sims answering the subjective Windblade question incorrectly as Druid, rejecting that line, retrying with identity uncertainty, then falling back to ignorance.

Current source now keeps the authorities separate:

- native `CharacterClass` -> Sim identity;
- wiki result -> definition/knowledge only;
- LLM/memory/name -> never class authority.

The subjective/identity/factual routing changes below are **SOURCE VERIFIED / TEST WIRED**, not live verified.

### Duplicate native-Lunaris discovery — LIVE VERIFIED; coexistence still unresolved

The supplied log shows a full native-plugin discovery/load pass, then another `Plugin found:`/load pass for the same suite plugins in a different order, including Deep Sims, with no visible `OnDestroy` between those passes. Non-native Adventure Guide / Auto Sort do not show the same native discovery sequence.

This proves duplicate native discovery/Awake-style behavior in the log. It does **not yet prove** two Deep Sims `MonoBehaviour` instances remain concurrently alive. Deep Sims now logs a per-instance serial, Unity instance ID, `Instance` ownership, heartbeat, and destruction state so the next live run can resolve that question. No singleton-suppression hack was added.

### Performance — LIVE VERIFIED correlation only

The log includes multi-second frame hitches, but its own Deep Sims measurements show nearby party refreshes completing in roughly sub-10ms samples (examples include 0.5ms, 1.5ms, 9.2ms). That is evidence that those measured refreshes were not themselves multi-second operations; it is not proof that Deep Sims cannot contribute elsewhere.

## Source fixes from the deep audit

### 1. Roleplay final-output invariant — SOURCE VERIFIED / TEST WIRED / NEEDS LIVE TEST

For Sim-spoken visible chat:

- **group speech:** every producer funnels through `QueueGroupMessage`; the main-thread display path then reacquires the speaker, applies native typing style/sanitization, and runs `RoleplayOutputGuard` again immediately before `WriteChat`;
- **whispers:** the final main-thread whisper candidate runs the same guard immediately before persistence/display.

A real bypass was fixed: when a whisper candidate was rejected and replaced with a deterministic fallback, the replacement was previously displayed without a second final validation. Replacement candidates are now treated as new candidates and validated again.

The Roleplay vocabulary was also narrowed to avoid breaking ordinary in-world language. Structural phrases such as `this game`, `the game`, `player character`, `hit me up`, login/server/internet language, etc. reject/regenerate/suppress; typed-chat texture such as `lol`, `lmao`, `heh`, `haha`, `XD`, common emoticons, etc. is sanitize-able. Ordinary phrases such as `a game of dice`, `training session`, and `lute player` are allowed.

MMO perspective does not run the Roleplay guard.

### 2. Roleplay Templates/LLM parity — SOURCE VERIFIED / TEST WIRED / NEEDS LIVE TEST

Roleplay already had separate ambient/event templates, but two deterministic paths still called MMO `SocialTemplates` directly:

- player ritual replies;
- Sim-to-Sim thread continuations.

Roleplay now uses Roleplay-specific deterministic renderers for those paths. MMO templates remain unchanged.

### 3. Identity-fact vs factual-definition vs subjective opinion — SOURCE VERIFIED / TEST WIRED / NEEDS LIVE TEST

`PartyReplyIntent` now distinguishes:

- `What is a Windblade?` -> factual game knowledge;
- `Are you a Windblade?` / `Is Dancer a Windblade?` -> native identity fact;
- `What do you think about being a Windblade?` -> subjective opinion.

Opinion phrases are checked before broad factual heuristics. Prompt context explicitly cross-references the asked class with the Sim's verified native class. Grounding still rejects contradictory class claims, while bounded preference/opinion answers do not require fabricated historical evidence.

### 4. Per-character Deep Sims memory — SOURCE VERIFIED / TEST WIRED / NEEDS LIVE TEST

Previously, all Sim memory JSON lived in one global `Memory/` namespace, so familiarity, rapport, preferences, conversation history, and outing-derived social state could leak between player characters.

Memory now lives under:

`plugins/config/DeepSims/Memory/Characters/<character-key>/`

The key follows the suite's already-used convention: verified save-slot index + live character name where available, otherwise a sanitized name fallback. Old flat memory is preserved as **unscoped legacy data** and is deliberately not silently assigned to whichever character is currently loaded.

Character switching invalidates pending request lanes, whisper generations, conversation generation, delayed group output, queued main-thread callbacks, recent AI/chat context, social cadence/director state, telemetry, external-news context, and Roleplay transient context before the new memory store is installed.

Additional races found and closed:

- stale conversation continuations cannot post-loop write character A's social thread into character B's memory;
- delayed continuation inference reads from the memory store it started with, not whichever store is current later;
- stale autonomous topic/preference callbacks cannot mutate the new character's director/memory;
- a stale party external-news lookup cannot seed the new character/topic's temporary news cache.

Name-only fallback can still collide if two save slots use the same character name and no trustworthy slot can be read. This is a documented fallback, not a hidden guarantee.

### 5. Lunaris unload cleanup — SOURCE VERIFIED / NEEDS LIVE STRESS TEST

`OnDestroy()` now additionally clears Deep Sims-owned static runtime state:

- legacy embedded `/dsfollow` target/state;
- Duel social dedup/counters;
- PvP social dedup window;
- Nemesis social dedup window.

Existing teardown already stops request admission, clears pending lanes, advances conversation generation, clears delayed display and main-thread closures, flushes telemetry/memory, unsubscribes COOP/Campmaster `AssemblyLoad` handlers, removes Harmony patches, and resets Roleplay/static singleton state.

There is intentionally no `SemaphoreSlim.Dispose()` while an in-flight worker may still release it.

**Residual release blocker:** in-flight `HttpWebRequest` work is bounded by timeout but is not actively cancelled on unload. Source gates make its late display/persistence inert, but an old network call can still finish after unload and can overlap a newly enabled instance. The exact unload-pending-request/re-enable scenario therefore still requires live stress testing before release.

### 6. Default-log privacy — SOURCE VERIFIED / TEST WIRED / NEEDS LOCAL VERIFICATION

The old live DLL logged truncated Ollama request JSON and raw model reply text. Those can include player chat, retrieved facts, and bounded memory context.

Current source now logs request/response metadata instead of payload content: model, message count, character counts, eval counts, timing/outcome. Wiki/news/external-news diagnostics likewise omit raw user query text. Generated rejected/duplicate/final lines are no longer dumped into ordinary diagnostic logs.

`/dsexport` remains an explicit user action and can contain session/social history; its chat response now labels the export private and shows only a Deep-Sims-relative filename. `/dsdump` likewise no longer prints a full absolute path and warns the user to review the diagnostic before sharing. File-operation logs use exception types rather than path-bearing exception messages.

### 7. Suite Hub optional API — SOURCE VERIFIED / TEST WIRED / NEEDS INTEGRATION TEST

Added `DeepSimsControlApi` schema v1. It is optional and late-bound; Deep Sims has no Hub dependency.

Exposed status is primitive/string-only and normal-player-safe: module/version, coarse Ollama status/model, social mode/activity, perspective, coarse character/memory-writer health, active Deep Sim summary, and session summary. Safe setters exist for social mode/activity/Roleplay plus status refresh.

It does not expose API keys, raw memories, thread/task objects, Unity objects, arbitrary command execution, or gameplay-control actions.

## Tests / build status

The standalone deterministic runner now calls **13 suites**, including Roleplay, character scope, Hub control policy, diagnostic privacy, and Duel lifecycle dedup reset. The PowerShell runner lists all required pure source files and no referenced test source is missing.

This Linux audit environment has no Windows `csc.exe`, PowerShell, Mono, or supplied current Erenshor/Lunaris managed reference DLLs, so the deterministic executable and full runtime build could not be executed here. Static delimiter/wiring checks passed, but actual compilation/execution is **NEEDS LOCAL VERIFICATION**.

Exact local commands are in `docs/HANDOFF_FOR_DEEP_AUDIT.md` and the audit ZIP's `HANDOFF.md`.

## Release blockers / next live checks

1. Run Roleplay + LLM exact bad-line regression and Windblade subjective/identity/factual matrix.
2. Run Roleplay + Templates and MMO + Templates parity checks.
3. Run pending-Ollama Lunaris unload/re-enable 3-5 times; no old output, no duplicated command/reply paths.
4. Use the new instance diagnostics to establish whether two Deep Sims instances coexist during the suite-wide duplicate native discovery.
5. Switch between two player characters and verify memory/social context never crosses scopes.
6. Re-run `/dsperf` during a real hitch and correlate measured Deep Sims stage times without claiming causation from timing alone.
