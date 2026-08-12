# Erenshor Reference Pack

**Research snapshot:** 2026-08-10  
**Intended use:** persistent source/context for AI-assisted Erenshor play, mod design, debugging, and development.  
**Spoilers:** heavy. This pack discusses systems, raids, developer/debug commands, technical implementation, and mod internals.

## Why this is split into several files

Erenshor changes quickly in Early Access. A single huge source makes it harder to distinguish:
- stable game concepts from patch-sensitive implementation details;
- official game behavior from wiki descriptions;
- observed public mod techniques from an official API;
- BepInEx-era practices from the newer Lunaris ecosystem.

The focused files are therefore the preferred sources. `ERENSHOR_REFERENCE_PACK_COMBINED.md` contains the same material in one file for convenience.

## Files

1. **01_ERENSHOR_GAME_SYSTEMS.md**  
   Current game identity, core loops, SimPlayers, parties, combat roles, progression, items, economy, crafting, guilds, raids, zones, saves, and important v0.7-era changes.

2. **02_ERENSHOR_MODDING_AND_TOOLING.md**  
   BepInEx 5, Harmony/HarmonyX, Lunaris, Unity/Mono assembly inspection, build/reference setup, configuration/logging, packaging, testing, and compatibility practices.

3. **03_ERENSHOR_TECHNICAL_SURFACE_AND_PATTERNS.md**  
   A practical map of public game symbols and modding surfaces that have been observed in open-source mods, plus safe architecture patterns and confidence labels.

4. **04_ERENSHOR_MOD_ECOSYSTEM.md**  
   Survey of public Erenshor mods and repositories, what they demonstrate technically, and how the user's current Deep Sims / Follow / Practice Duels work.

5. **05_ERENSHOR_AI_MOD_DEVELOPMENT_GUIDE.md**  
   A compact design doctrine for future AI-assisted Erenshor mod work: evidence hierarchy, game-authority boundaries, compatibility strategy, validation, and common failure modes.

6. **ERENSHOR_REFERENCE_PACK_COMBINED.md**  
   Combined copy of all five focused documents.

## Confidence labels

The documents use these labels:

- **[OFFICIAL]** — Erenshor Steam store, Steam developer announcements, official BepInEx docs, official project docs.
- **[WIKI]** — Official Erenshor Wiki. Usually strong for player-facing mechanics, but page age matters.
- **[CODE]** — Public source code or README from an Erenshor mod. Strong evidence for what that mod currently does; not a promise that Erenshor internals are stable.
- **[COMMUNITY]** — Thunderstore, Erenshor Vault, community tools, or secondary ecosystem material.
- **[INFERENCE]** — A reasoned conclusion from multiple sources. Treat as a working model, not a contract.
- **[STALE-RISK]** — A source is useful but demonstrably old enough that names or signatures may have changed.

## Source precedence for future work

For game behavior:
1. current live game / installed assemblies;
2. current official Erenshor patch notes;
3. recently updated official wiki pages;
4. current public mod code that compiles against the same build;
5. older wiki/mod examples.

For mod implementation:
1. installed `Assembly-CSharp.dll` and Unity assemblies from the target profile;
2. a minimal live instrumentation test;
3. current source from working mods;
4. reflection only when necessary;
5. never invent a member, enum, field, command, or network capability just because it would be convenient.

## Important freshness note

Erenshor is in Early Access. The Steam store currently states that the game is planned to leave Early Access in 2027. The large **v0.7 Planar March** raid update launched on **2026-07-13**, and the developer said on **2026-07-26** that routine v0.7 adjustments were ending as work moved to v0.8. Any technical hook in this pack should therefore be revalidated after a game update.

## Core source index

### Official game
- https://store.steampowered.com/app/2382520/Erenshor/
- https://steamcommunity.com/app/2382520/allnews/
- https://erenshor.wiki.gg/

### Core wiki pages
- https://erenshor.wiki.gg/wiki/Player_Guide
- https://erenshor.wiki.gg/wiki/Simulated_Players
- https://erenshor.wiki.gg/wiki/Classes
- https://erenshor.wiki.gg/wiki/Stats
- https://erenshor.wiki.gg/wiki/Proficiencies
- https://erenshor.wiki.gg/wiki/Ascension_Skills
- https://erenshor.wiki.gg/wiki/Item_Quality
- https://erenshor.wiki.gg/wiki/Auction_House
- https://erenshor.wiki.gg/wiki/Guild

### Established modding
- https://thunderstore.io/c/erenshor/
- https://docs.bepinex.dev/
- https://github.com/BepInEx/HarmonyX

### Erenshor-specific newer tooling
- https://github.com/MizukiBelhi/Lunaris
- https://mizukibelhi.github.io/Lunaris-Docs/
- https://erenshorvault.app/

### Representative public mods
- https://github.com/MizukiBelhi/ErenshorCoop
- https://github.com/Brumdail/ErenshorQoL
- https://github.com/aepod/ErenshorLLM
- https://github.com/xJeris/siminspect
- https://github.com/et508/Erenshor.Shornet
- https://github.com/forgetwhtuno/DeepSim-erenshor
- https://github.com/forgetwhtuno/ErenshorFollow
- https://github.com/forgetwhtuno/Erenshor-Duel

## Recommended AI instruction when attaching this pack

> Treat these documents as a dated technical reference, not an official stable API. Prefer current installed Erenshor assemblies and current repository code over old examples. When a symbol or behavior is uncertain, identify the uncertainty and inspect/verify it rather than inventing an API. Keep Erenshor authoritative for game state and gameplay decisions.
