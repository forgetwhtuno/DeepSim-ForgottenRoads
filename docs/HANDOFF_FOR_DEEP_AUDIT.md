# Deep Sims — Post-Audit Handoff

**Audit date:** 2026-08-13  
**Base branch:** `agent/lunaris-native-deepsims`  
**Exact base SHA:** `b21b4d4150e0baffcc2d48ec6b44d6489e057d86`

This is a local-integration handoff. No Git write was performed by the audit.

## Read first

1. `AGENTS.md` — architectural authority.
2. `docs/DEEP_ARCHITECTURE_AUDIT.md` — current technical map and request flows.
3. `docs/CURRENT_WORK.md` — evidence-labelled current status and release blockers.
4. `LUNARIS_RELEASE_CHECKLIST.md` — broader migration/live checks.

## Non-negotiable authority boundary

Erenshor owns gameplay truth and gameplay decisions. Deep Sims may observe, remember bounded verified/social context, and express social flavor. The LLM never gets authority to move Sims, attack, heal, loot, equip, quest, choose pulls/targets, toggle combat automation, change faction, or write native save truth.

The embedded legacy `/dsfollow` path moves only the local human player toward a selected local Sim and is automatically disabled when standalone Erenshor Follow is detected. This audit did not expand it.

## Main changes made by this audit

### Roleplay

- final Roleplay enforcement at the actual group display boundary;
- whisper replacement fallback is revalidated before display;
- structural out-of-world language is rejected while typed-chat texture is sanitize-able;
- ambiguous ordinary uses of words such as `game`, `session`, and `player` are no longer blanket-rejected;
- explicit `IdentityFact` intent separates native identity from wiki definition and from subjective opinion;
- Roleplay deterministic ritual/thread templates no longer fall through to MMO templates;
- final Roleplay diagnostics expose ran/changed/rejected without logging prompt text.

### Character scope / memory

- per-character memory directories using verified slot+name where available;
- old flat memory retained as unscoped legacy data, never auto-claimed;
- scope switch invalidates queued/in-flight presentation ownership;
- delayed conversation memory writes and autonomous preference callbacks are generation/store guarded;
- stale external-news lookups cannot seed the new character's temporary context.

### Lifecycle

- Deep Sims static legacy follow state resets on unload;
- Duel/PvP/Nemesis Deep-Sims-owned dedup/diagnostic state resets on unload;
- existing request-generation/Harmony/AppDomain cleanup retained;
- no singleton workaround added for the suite-wide duplicate-Lunaris discovery issue.

### Privacy

- default Ollama/wiki/news diagnostics no longer dump raw prompt/query/reply text;
- ordinary final/rejection logs use metadata/reason rather than generated content;
- export/diagnostic chat responses no longer reveal absolute filesystem paths;
- explicit session export remains intentionally content-bearing and is labeled private.

### Optional Hub API

- `DeepSimsControlApi` schema v1; late-bound and optional;
- primitive/string status only;
- safe setters for social mode/activity/Roleplay and status refresh;
- no raw memory, key, arbitrary command, Unity object, or gameplay action exposure.

## Runtime assumptions that require the real game

The new character identity adapter uses the same current game surface already used by sibling suite mods:

- `GameData.InCharSelect`
- `GameData.PlayerControl.Myself`
- `GameData.CurrentCharacterSlot` / `GameData.ActiveSaveSlot`
- `SaveGameData.index` / `SaveGameData.CharName`

Sim class identity remains sourced from native Sim/Stats state and normalizes legacy/internal `Duelist` to current `Windblade` terminology. Wiki results are never identity authority.

These symbols must still compile against the user's current `Assembly-CSharp.dll`; Erenshor is Early Access and no internal member is a stable SDK promise.

## Exact local test/build commands

From the DeepSim-erenshor repository root on Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\RUN_DETERMINISTIC_TESTS.ps1
```

For a real current-assembly build:

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1 `
  -GameDir "<Erenshor>" `
  -LunarisLibDir "<directory-containing-Lunaris.dll-and-0Harmony.dll>"
```

The build script intentionally uses the installed game's managed assemblies and must produce `ErenshorDeepSims.dll` with no `BepInEx.dll` dependency. It also contains pre-existing suite convenience logic that can invoke sibling builds when they are present; run it from the intended local suite workspace and review its output before installing anything.

If doing compile-only verification, follow `BUILD_AND_INSTALL.ps1`'s exact reference list but redirect `/out:` to a scratch directory instead of the live plugin directory.

## Compiler/API discrepancies to watch for

- `SaveGameData`, `CurrentCharacterSlot`, `ActiveSaveSlot`, `CharName`, or `index` changed in a newer Erenshor build.
- Harmony target shape changed for `TypeText.CheckCommands`, social-log methods, or optional Duel patches.
- current Lunaris `LunarisPlugin`/config/log API changed from the branch's migration baseline.
- `UpdateSocialLog.LogAdd` overload shape changed after another Erenshor chat refactor.
- a current loader changes whether duplicate native plugin discovery produces duplicate live plugin objects.

Fail closed and inspect current assemblies rather than inventing replacement members.

## Exact live tests before release

### Roleplay LLM

1. `/dsroleplay on`
2. ask normal greeting/small talk repeatedly; no `online`, `server`, `NES`, `hit me up`, `lol/lmao/heh`, emoticons.
3. `Dancer, what is a Windblade?` -> definition/knowledge route.
4. `Dancer, are you a Windblade?` -> native identity route; Dancer should resolve legacy Duelist as Windblade if that is still the native live class.
5. `Dancer, what do you think about being a Windblade?` -> bounded opinion, e.g. “I like it. Getting in close suits me.”; no invented years/history.
6. Ask the same subjective question about a class Dancer is not -> correct the premise, do not adopt the class.
7. Confirm logs show `roleplayGuardRan=True` for final Sim-spoken Roleplay output and do not contain prompt/reply payload dumps.

### Perspective/template matrix

Test all four:

- Roleplay + LLM
- Roleplay + Templates
- MMO + LLM
- MMO + Templates

Roleplay must remain in-world; MMO mode may retain MMO-player framing and shorthand. Templates must not silently switch perspective.

### Lunaris pending-request unload blocker

Repeat 3-5 times:

1. load Deep Sims;
2. start a visibly pending Ollama request;
3. disable Deep Sims in Lunaris before it returns;
4. wait longer than the request's normal completion window;
5. **no old Sim output appears**;
6. re-enable;
7. exactly one command path and one response path;
8. confirm one active heartbeat/instance owner with `[DeepSimsInstanceDiag]`;
9. repeat after zoning.

If two instance serials heartbeat concurrently, stop release reconciliation and inspect Lunaris/Hub loader behavior before adding any per-mod singleton workaround.

### Character switching

1. Character A: group with a known Sim, create visible familiarity/conversation, then exit to character select.
2. Character B: group with the same Sim; A's conversation/familiarity/preferences must not appear.
3. Trigger an Ollama/thread request on A and switch before it completes; no A line or memory write may appear under B.
4. Switch back to A; A's scoped memory should still be present.
5. Inspect `Memory/Characters/` only if needed; do not manually alter Erenshor saves.

### Privacy

- normal Ollama requests/replies: logs show metadata only, not player lines/system prompts/memory text;
- `/dsexport`: explicit file contains expected social/session content and chat labels it private without absolute path;
- `/dsdump`: chat gives relative diagnostic location only;
- inspect the exported/diagnostic file before attaching it to a public bug report.

### Performance

Use `/dsperf` while reproducing a hitch. Compare party snapshot, telemetry, social, queue, and Ollama timing. A temporal overlap is not causation; report measured stage cost separately from frame hitch duration.

## Deliberately untouched behavior

- command syntax is preserved;
- unknown/vanilla commands still fall through;
- MMO perspective semantics remain intentionally distinct from Roleplay;
- social cadence/budget values were not retuned;
- native combat/movement/loot/save authority was not expanded;
- COOP still uses conservative host-local generated speech where party-targeted replication is unproven;
- optional sibling integrations remain absent-safe/reflection based;
- no Hub UI was implemented here;
- no loader-level duplicate-instance hack was added.
