# Deep Sims Native Lunaris Migration Report

**Prepared:** 2026-08-12  
**Migration branch in uploaded checkout:** `agent/lunaris-native-deepsims`  
**Uploaded checkout base HEAD:** `5f18be1f755a051b2a11dd25d7e452059611ef1b`  
**Base commit message:** `Add attribution and AI-development disclosure`

## Status

This tree is a **native-Lunaris migration candidate**, not a verified release build yet.

The source-side migration is substantially complete and static validation is clean. A real C# compile and the Lunaris in-game hot-unload/reload matrix still need to be run on Windows because this execution environment has no .NET Framework `csc.exe`, `dotnet`, Mono compiler, MSBuild, or PowerShell runtime.

No commit, push, PR, or remote write was performed.

## Local Lunaris references inspected

The uploaded checkout contains local development references under `LunarisLibs/`.

- `Lunaris.dll`
  - SHA-256: `5a70f3d1fd9441ceae6d8e1f80cafce86ff2a47245fbcfa36bfcf8e88fd20b29`
  - embedded strings include runtime `0.1.9`
  - PE/FileVersion string includes `0.1.0.0`
- `0Harmony.dll`
  - SHA-256: `c349e1a3fd13fa5a9facc9805a5e160161b14489f46f6bdd38202b8e124f78df`

The migration was checked against the current Lunaris source/API shape used by that DLL family, including `LunarisPlugin`, `ILog`, typed `Config.Register<T>()`, config save behavior, native plugin paths, permissions, and unload/config-handle cleanup.

## Important pre-existing local state preserved

The uploaded ZIP had widespread apparent Git modifications caused by line-ending normalization. After normalized-content comparison, the substantive pre-existing local edits were:

1. `.gitignore` entries for local Lunaris development libraries/release-prep files.
2. The existing `BUILD_AND_INSTALL.ps1` one-click sibling invocation for **Erenshor Crafting Expanded**.

Both were preserved.

Pre-existing untracked sibling work such as `Erenshor-Crafting-Expanded/` and `mod updates/` was not modified or packaged as part of this migration.

## Architecture changes

### 1. Native Lunaris host

`DeepSimsPlugin` now derives from `LunarisPlugin` and uses native Lunaris plugin metadata/permissions instead of:

- `BaseUnityPlugin`
- `[BepInPlugin]`
- `[BepInProcess]`

Permissions are limited to the capabilities Deep Sims actually uses:

- file access
- network
- reflection
- Harmony

Harmony remains intentionally retained for verified Erenshor hooks and the existing command parser.

### 2. Logging boundary

Added a Deep-Sims-owned logging abstraction:

- `IDeepSimsLog`
- `NullDeepSimsLog`
- `LunarisDeepSimsLog`

Subsystems no longer receive BepInEx `ManualLogSource`. The Lunaris adapter uses the actual `ILog` methods (`LogDebug`, `LogInfo`, `LogWarning`, `LogError`).

### 3. Typed Lunaris config

Added `DeepSimsSettings` registered through Lunaris typed config and a small loader-neutral `DeepSimsConfigEntry<T>` shim that preserves the existing `.Value` access pattern.

This intentionally avoids a giant behavior rewrite across the social/grounding code.

Static config migration audit:

- original BepInEx binds: **75**
- Lunaris wrapper mappings: **77**
- Lunaris typed fields: **77**
- mismatches across the original 75 on section/key/default/description: **0**

The two additional settings are:

- `SocialPerspective` (`MMO` / `Roleplay`)
- `VerboseLogging` diagnostics setting used by the migrated logging path

Existing validation, normalization, and `ConfigVersion` migrations remain in the plugin startup flow.

### 4. Existing BepInEx config policy

This pass deliberately does **not** parse arbitrary old BepInEx config files. Native Lunaris configuration starts fresh rather than risking incorrect migration of 75+ settings.

The old BepInEx config is left untouched for manual reference.

### 5. Memory / sidecar persistence

Deep Sims-owned persistent data now resolves under:

`<Erenshor>/plugins/config/DeepSims/`

with memory under:

`<Erenshor>/plugins/config/DeepSims/Memory/`

A conservative one-time memory copy is allowed only from the direct legacy game-root path:

`<Erenshor>/BepInEx/config/DeepSims/Memory/`

and only when the new memory directory is empty. The old data is never deleted.

No Erenshor save-file path is modified.

### 6. Command strategy

Deep Sims intentionally keeps its existing Harmony-backed `TypeText.CheckCommands` parsing instead of switching to `[LunarisCommand]`.

Reasons:

- optional arguments
- multiword arguments
- Sim names
- free-form news queries
- established command grammar
- current Lunaris command registration does not expose a reviewed public unregister API suitable for safe hot-unload ownership

No `[LunarisCommand]` attributes were added.

### 7. Hot-unload behavior

`OnDestroy()` now treats Lunaris unload as a correctness boundary:

1. stops admitting request work under the request lock;
2. clears pending party/whisper/autonomous requests;
3. advances the conversation generation so in-flight work becomes stale;
4. clears scheduled group output;
5. drains already queued main-thread closures;
6. finishes telemetry and shuts down memory sidecar persistence;
7. explicitly unsubscribes the process-wide `AppDomain.AssemblyLoad` handlers owned by COOP/Campmaster compatibility and clears their reflected type/member caches;
8. unpatches the plugin-owned Harmony instance;
9. resets Roleplay transient state;
10. clears `DeepSimsPlugin.Instance`.

All main-thread worker callbacks now go through an admission helper that fails closed after shutdown begins.

The inference semaphore is deliberately not disposed during unload because an already-running request may still reach its `Release()` path. Late work is neutralized by request-stop/generation guards instead of blocking Unity waiting for Ollama/network completion.

The network clients use bounded `HttpWebRequest` calls rather than owning a persistent plugin-lifetime `HttpClient`, so there is no long-lived HTTP client object requiring disposal in this implementation.

### 8. Optional integrations

Campmaster, Practice Duels, PvP, Nemesis, COOP, and related optional bridges remain optional/reflection-based for this migration.

They were **not** prematurely rewritten to Aura IPC. This avoids creating one-sided hard dependencies while companion mods are at different migration stages.

### 9. Build/install/uninstall

`BUILD_AND_INSTALL.ps1` now:

- finds current Erenshor;
- finds local `Lunaris.dll` + `0Harmony.dll` references;
- compiles against current `Assembly-CSharp.dll` / Unity managed assemblies;
- does not reference `BepInEx.dll`;
- writes to a temporary DLL first;
- copies only after successful compile;
- installs `ErenshorDeepSims.dll` under `<Erenshor>/plugins`;
- does not bundle/copy Lunaris runtime libraries;
- prints the Lunaris DLL version/hash used for compilation;
- only builds companion mods when explicitly requested.

`UNINSTALL.ps1` removes only the native plugin DLL by default and preserves Deep Sims config/memory. `-RemoveData` explicitly removes Deep-Sims-owned Lunaris config/data, never Erenshor saves.

## Roleplay reconciliation

The uploaded private-history checkout predates the newer public Roleplay merge. The public repository currently contains the Roleplay perspective work on top of the same pre-Roleplay source blobs used by the upload.

To avoid producing a migration branch that loses the newer feature, this tree reconciles the Roleplay behavior into the Lunaris candidate, including:

- `/dsroleplay on|off|status`
- MMO default
- perspective independent from `Auto/LLM/Templates/Off`
- Roleplay prompt identity/voice contract
- deterministic Roleplay templates
- no MMO/meta vocabulary in autonomous Roleplay output
- deterministic single salvage then `NO_MESSAGE`
- class cultural affinities that never imply faction membership/history
- bounded verified faction exposure context
- post-personalization chat-texture filtering in Roleplay
- deterministic regression coverage

`src/ChatCommandParser.cs` and `tests/StandaloneRegressionMain.cs` match the public Roleplay merge blobs exactly. Some larger Roleplay support portions were recreated/adapted from the public PR diff rather than copied byte-for-byte, so they require the same real compile/test validation as the loader migration before release.

## Static validation completed

Passed:

- `git diff --check`
- C# delimiter/string/comment structural scan across **53 C# files**: **0 problems**
- native-dependency source scan: no occurrences of:
  - `using BepInEx`
  - `BaseUnityPlugin`
  - `[BepInPlugin]`
  - `[BepInProcess]`
  - `BepInEx.Configuration`
  - `BepInEx.Logging`
  - `Paths.ConfigPath`
  - `Config.Bind(`
  - `ManualLogSource`
  - `[LunarisCommand]`
- original 75-setting typed config equivalence audit: **0 mismatches**
- privacy/path scan found no personal Windows user paths or personal-name strings in the migrated source/docs inspected

Intentional remaining text references to `BepInEx` are documentation/history and the legacy memory import path only.

## Validation NOT completed in this environment

Not claimed:

- full C# compile against the uploaded Erenshor/Lunaris assemblies;
- execution of the PowerShell deterministic test harness;
- in-game native Lunaris plugin load;
- in-game config UI validation;
- in-game command regression matrix;
- live COOP validation;
- live optional-mod integration validation;
- Lunaris unload/reload while an Ollama request is in flight;
- repeated hot reload leak/duplicate-event test.

Reason: this Linux execution container has no .NET Framework compiler, `dotnet`, Mono compiler, MSBuild, or PowerShell runtime. The provided Windows/game/Lunaris reference DLLs are present, but there is no compatible compiler executable here.

## Recommended next action

On the Windows machine with Erenshor installed, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1
```

If it compiles, immediately run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\RUN_DETERMINISTIC_TESTS.ps1
```

Then work through `LUNARIS_RELEASE_CHECKLIST.md`, especially the pending-request hot-unload test.

If compilation reports any error, capture the complete compiler output before making further behavioral changes.

## Remaining risks before release

1. **Real compiler validation is still mandatory.** Static scans cannot prove C# API/overload compatibility the way `csc.exe` can.
2. **Roleplay reconciliation needs the same compile/game validation.** The uploaded checkout was behind the public Roleplay merge, and the migration candidate carries that behavior forward rather than silently dropping it.
3. **External consumers can still defeat unload if they cache Deep Sims reflection objects forever.** Deep Sims now removes its own process-wide event handlers and Harmony patches and clears `Instance`, but another mod that permanently retains a `Type`, `MethodInfo`, or delegate into an old Deep Sims assembly is outside Deep Sims' control. The current optional integration matrix should therefore be exercised during repeated Lunaris reloads.
4. **Lunaris config UX must be visually checked.** The typed settings preserve the original values, but section ordering/labels and hidden-field presentation are runtime UI concerns.
5. **Legacy config values are not automatically imported.** This is intentional; a fragile parser would be riskier than a documented fresh Lunaris config while preserving the old file for reference.
