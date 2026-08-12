# Deep Sims for Erenshor 0.7.1 — native Lunaris development candidate

Deep Sims makes Erenshor's existing SimPlayers feel more like persistent MMO companions. It observes verified game state, keeps bounded sidecar memory, and produces short social dialogue through deterministic templates or an optional local Ollama model.

**Deep Sims does not replace Erenshor's Sim AI and does not control gameplay.** Erenshor remains authoritative for movement, combat, pulls, healing, targeting, loot, grouping, roles, equipment, quests, faction, progression, and saves.

> This branch is being prepared as a native Lunaris build. Treat 0.7.1 as a development/migration candidate until the compile and in-game hot-reload checklist is completed on a current Erenshor installation.

## Requirements

- Erenshor
- [Lunaris](https://github.com/MizukiBelhi/Lunaris)
- Ollama only if you want LLM-backed dialogue. `Templates` and `Off` do not require inference.

Deep Sims no longer requires BepInEx as its native plugin loader. Harmony is still used intentionally for verified Erenshor hooks and the existing rich command parser.

## Install

### Normal/manual install

1. Install Lunaris and launch Erenshor once.
2. Put `ErenshorDeepSims.dll` in:

```text
<Erenshor>\plugins\ErenshorDeepSims.dll
```

3. Launch Erenshor through the normal Lunaris installation.
4. Use `/aistatus` or `/dsims` after entering the game.

Do **not** copy `Lunaris.dll`, `0Harmony.dll`, Newtonsoft, ImGui, or other Lunaris runtime libraries into the Deep Sims package. Lunaris owns those dependencies.

### Developer build

`BUILD_AND_INSTALL.ps1` compiles against your current installed Erenshor assemblies plus local Lunaris developer references.

Put at least these two developer references in `LunarisLibs\` or pass `-LunarisLibDir`:

```text
Lunaris.dll
0Harmony.dll
```

Then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1
```

The script compiles to a temporary file first and copies only a completed DLL into `<Erenshor>\plugins`, avoiding a partial DLL being observed by Lunaris' runtime watcher.

## 30-second quick start

After entering the game and grouping with SimPlayers:

```text
/aistatus                 Deep Sims / Ollama status
/dsims                    current Deep Sim party status
/dssocial status          social expression/activity status
/dsroleplay status        MMO vs Roleplay perspective
/dssession                current/last/outing encounter state
/dsperf                   performance and request diagnostics
```

Then simply talk in normal party chat. Deep Sims preserves Erenshor's ordinary command-bearing group chat path.

## Social expression modes

`/dssocial` controls **how** autonomous social lines are expressed:

```text
Auto        use LLM when appropriate/healthy; deterministic fallback when not
LLM         prefer the local model
Templates   deterministic social expression only
Off         disable autonomous Deep Sims social expression
```

Activity can independently be `Adaptive`, `Quiet`, `Normal`, or `Lively`.

## MMO vs Roleplay perspective

`/dsroleplay` controls **who the Sim is speaking as**, independently from expression mode:

```text
/dsroleplay on        Roleplay: speak as the adventurer represented by the Sim
/dsroleplay off       MMO: speak like another old-school MMO player
/dsroleplay status
```

Roleplay does not change talk frequency, grounding, gameplay authority, or memory truth. It changes voice/perspective only. Roleplay autonomous output has an additional guard against MMO/meta language, narration, invented affiliation/history, and post-generation typing texture such as `lol` or text faces.

## Lunaris settings and data

Native settings are registered through Lunaris and appear in its config system. The expected config file is:

```text
<Erenshor>\plugins\config\erenshordeepsims.lpcfg
```

Deep Sims-owned sidecar data lives under:

```text
<Erenshor>\plugins\config\DeepSims\
    Memory\
    Exports\
```

Deep Sims never writes its social memory into Erenshor save files.

### Existing BepInEx installs

The native Lunaris configuration starts as a fresh typed Lunaris config; this migration deliberately does not guess at or rewrite arbitrary old BepInEx settings.

For a simple direct game-root legacy install only, if the new Lunaris memory directory is empty, Deep Sims will copy existing files from:

```text
<Erenshor>\BepInEx\config\DeepSims\Memory
```

into the new Lunaris sidecar directory. The old files are left untouched. r2modman/Thunderstore profile directories are not searched automatically.

## Grounding and memory boundary

Deep Sims separates observed/verified state from heard or generated text. Broadly:

```text
OBSERVED_NOW
> verified EXPERIENCE / EVENTS
> bounded REMEMBERED summaries
> wiki / official game news
> external real-world news
> HEARD dialogue/player claims
> UNKNOWN
```

Generated dialogue is evidence only that a line was said. It is never proof that its content happened. Unsupported phrases such as `again`, `last time`, or shared-history claims are rejected unless verified history supports them.

## Main commands

```text
/aistatus                 model/plugin status
/aitest                   AI/integration smoke test
/aimodel <model>          change Ollama model
/dwhisper <Sim> <text>    force an AI whisper
/vwhisper <Sim> <text>    request vanilla-style handling
/dssession                encounter/session state
/dsperf                   performance/request diagnostics
/dsmemory [Sim]           bounded memory inspection
/dsforget <Sim> ...       forget eligible flavor/social data
/dsexport [full]          concise/detailed session export
/dsevents ...             verified event-director controls
/dsseeds ...              autonomous seed diagnostics
/dsguardtest              grounding/contract smoke tests
/dsinference ...          auto/CPU/GPU inference mode
/dsreasoning ...          reasoning-model routing
/dsxnews <query>          explicit external-news lookup
/dsnewsources             external-news source status
/dscamp ...               Campmaster context integration
/dssocial ...             expression/activity controls
/dsroleplay ...           MMO/Roleplay perspective
```

Deep Sims deliberately keeps its existing Harmony-backed `TypeText.CheckCommands` parser instead of converting these to Lunaris command attributes. The current commands include optional and multiword/free-form grammar, and the inspected Lunaris command surface does not provide the unregister semantics this project requires for safe hot unload.

## Co-op behavior

COOP is optional. When detected, Deep Sims preserves its conservative host-authority model. Remote humans are not classified as local Sims, remote chat remains HEARD rather than verified fact, and generated Sim speech is not given invented network replication authority.

## Optional companion mods

Deep Sims remains standalone. Current optional integrations include Campmaster, Practice Duels, PvP, Nemesis, and related suite components. Existing narrow runtime/reflection contracts remain optional and absent-safe during this migration; they are not being forced to Aura until both sides have stable native contracts.

`BUILD_AND_INSTALL.ps1` does **not** build sibling mods unless `-BuildCompanionMods` is explicitly supplied.

## Privacy and network behavior

- Ollama defaults to a local endpoint.
- Wiki/news lookups are optional and bounded.
- External real-world news is conversation-scoped and never becomes Erenshor lore or permanent Sim memory.
- API keys are never intentionally logged or exported.
- Deep Sims memory remains local sidecar data.
- Do not publish personal memory exports or private logs with bug reports unless you have reviewed them.

## Hot reload / development safety

Lunaris can unload plugins while Erenshor is running, so `OnDestroy()` is part of correctness. Deep Sims now stops new request admission, invalidates conversation generations, clears queued display work, finishes/flushes sidecar state, removes its Harmony patches, clears Roleplay runtime context, and clears the plugin singleton without waiting indefinitely for an Ollama request.

Before calling a native Lunaris build release-ready, test:

1. load Deep Sims and join a party;
2. start an Ollama request;
3. unload Deep Sims through Lunaris while the request is pending;
4. confirm no late chat appears and no Harmony behavior remains;
5. reload and confirm exactly one working instance;
6. repeat unload/reload several times;
7. zone, then repeat unload/reload;
8. verify no duplicated chat, callbacks, social events, or memory writers.

See `LUNARIS_RELEASE_CHECKLIST.md` in the migration package for the complete matrix.

## Uninstall

`UNINSTALL.ps1` removes only the native Deep Sims DLL by default and preserves config/memory.

To intentionally remove Deep Sims-owned config and sidecar data too:

```powershell
.\UNINSTALL.ps1 -RemoveData
```

Erenshor save files are never deleted by this script.

## Troubleshooting

If Deep Sims does not appear:

- verify Lunaris itself loads;
- verify `ErenshorDeepSims.dll` is under `<Erenshor>\plugins`;
- check the Lunaris console/log for an assembly or Harmony error;
- rebuild against the current `Assembly-CSharp.dll` after an Erenshor update;
- use `/aistatus`, `/dsperf`, `/dsguardtest`, and `/dsinspect` when available.

- if Ollama is unavailable, use `Templates` mode to verify the social layer independently from model inference.

## Related mods

- [Erenshor Practice Duels](https://github.com/forgetwhtuno/Erenshor-Duel): friendly, non-lethal virtual-health duels with local Sims.
- [Erenshor PvP](https://github.com/forgetwhtuno/Erenshor-PvP): standalone off-map Sim-profile PvP encounters.
- [Erenshor Follow](https://github.com/forgetwhtuno/ErenshorFollow): deterministic player follow, Sim-led travel, and expeditions.
- [Erenshor Party Tools](https://github.com/forgetwhtuno/Erenshor-PartyTools): ready checks, cosmetic rolls, friend availability, and a compact command panel.
- [Erenshor Campmaster](https://github.com/forgetwhtuno/Erenshor-Campmaster): read-only Hunt Camp and Relax social-context modes.
- [Erenshor Nemesis](https://github.com/forgetwhtuno/Erenshor-Nemesis): an optional persistent rival system that can use PvP results when PvP is installed.

## Credits and inspiration

- **[CustomSimFramework](https://github.com/PuzzelPiece/CustomSimFramework) by PuzzelPiece / TeamSaltyBois** — inspiration for exploring richer Sim social behavior. Deep Sims is an independent implementation and does not use its code.
- **[Erenshor COOP](https://github.com/MizukiBelhi/ErenshorCoop) by MizukiBelhi** — important compatibility/reference work, especially for local-vs-remote actor boundaries. No COOP code is included.
- **[Lunaris](https://github.com/MizukiBelhi/Lunaris) by MizukiBelhi** — native Erenshor loader/config/plugin API used by this migration.

## Development note

This project has been developed substantially with AI-assisted coding tools, guided through design, testing, playtesting, audits, and iteration against Erenshor. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.
