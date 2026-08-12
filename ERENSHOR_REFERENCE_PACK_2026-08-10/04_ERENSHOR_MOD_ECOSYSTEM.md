# Erenshor Mod Ecosystem Reference

**Research snapshot:** 2026-08-10  
**Purpose:** map the current community ecosystem and show what each project teaches a future mod author.

This is not a complete catalog. Thunderstore, GitHub, and the newer Erenshor Vault change regularly.

---

## 1. Distribution and discovery hubs

### Thunderstore

https://thunderstore.io/c/erenshor/

Current Erenshor category includes:
- BepInExPack;
- r2modman / Gale tooling;
- Erenshor COOP;
- ErenshorQoL;
- AdventureGuide;
- minimap/UI/equipment/targeting/loot and many smaller mods.

Thunderstore remains the most visible legacy/BepInEx ecosystem.

### Erenshor Vault

https://erenshorvault.app/

Newer independent Erenshor-specific repository associated with Lunaris tooling.

### GitHub

A large share of Erenshor mod source is public on GitHub, making the ecosystem unusually useful for reverse-engineering and compatibility research.

---

## 2. Mod loaders/managers

### BepInExPack

Category: framework/runtime  
Role: established Mono Unity plugin framework and Harmony patch base.

Use it to learn:
- ordinary DLL plugin deployment;
- config/log layout;
- Harmony patching;
- Thunderstore packaging.

### r2modman / Thunderstore Mod Manager / Gale

Category: external mod/profile managers  
Role:
- install/update packages;
- isolate profiles;
- manage config;
- simplify dependencies.

Gale is a modern lightweight Thunderstore manager and supports profile syncing/config editing.

### Lunaris

Repository:
- https://github.com/MizukiBelhi/Lunaris

Category:
- Erenshor-specific loader/API/mod manager.

Key features:
- replaces BepInEx for native plugins;
- legacy BepInEx compatibility wrapper;
- hot load/unload;
- in-game plugin installer;
- auto-updates;
- built-in Unity Explorer;
- WIP console;
- ImGui;
- command/config API;
- Aura IPC;
- Erenshor Vault integration.

What it teaches:
- Erenshor's mod ecosystem is moving beyond generic BepInEx;
- future cross-mod APIs can be deliberate rather than reflection-only;
- unload cleanup should become a design requirement.

---

## 3. Erenshor COOP

Repository:
- https://github.com/MizukiBelhi/ErenshorCoop

Category:
- multiplayer/network synchronization.

Documented features include:
- Steam lobbies;
- player sync;
- enemies/NPCs;
- chat/whispers;
- grouping;
- spell effects;
- Sims;
- HP/MP healing;
- buffs/debuffs;
- XP;
- summons;
- trading;
- player markers.

The project source is broad: connection managers, networked players/Sims/NPCs, packets, zone ownership, group sync, status effects, drops, weather, Steam networking, and UI.

What it teaches:
1. co-op is a full authority/synchronization layer;
2. remote humans can appear inside game systems that were originally single-player;
3. local-only mods must positively exclude remote humans;
4. co-op compatibility is a feature to test, not assume.

Its README explicitly says compatibility with other mods is largely untested.

---

## 4. ErenshorQoL

Repository:
- https://github.com/Brumdail/ErenshorQoL

Category:
- commands/QOL/automation.

Documented features:
- `/auction`;
- `/bank`;
- `/forge`;
- `/guildinvite`;
- expanded `/help`;
- auto pet/autoattack options;
- automatic Auction House pricing.

Technical value:
- documents concrete Harmony anchors such as `TypeText.CheckCommands`, `PlayerCombat.ToggleAttack`, `NPC.AggroOn`, `AuctionHouseUI.OpenListItem`;
- contains a useful list of player and developer/debug commands;
- demonstrates an SDK-style .NET Standard 2.1 project that locates installed Erenshor assemblies.

Caution:
- parts of README history reference older game versions;
- debug command availability can differ by build;
- copy concepts, not signatures blindly.

---

## 5. ErenshorLLM

Repository:
- https://github.com/aepod/ErenshorLLM

Category:
- dynamic SimPlayer dialogue / local AI.

Architecture:
- C# BepInEx/Harmony mod;
- central chat interception around `UpdateSocialLog.GlobalAddLine`;
- Rust sidecar;
- llama.cpp inference;
- embeddings;
- vector lore/memory;
- local OpenAI-compatible API.

What it teaches:
- sidecar process architecture;
- structured chat interception;
- sync fast-path templates vs async generation;
- RAG/lore search;
- per-Sim personality and memory.

Important design contrast:
its project explores replacing/paraphrasing scripted social output, whereas a more conservative social layer can leave gameplay-controlled speech/commands intact and add grounded social expression around observed events.

---

## 6. Deep Sims

Repository:
- https://github.com/forgetwhtuno/DeepSim-erenshor

Current README baseline at research time:
- 0.7.0 / 0.7.x development.

Category:
- grounded local-LLM social layer around Erenshor SimPlayers.

Documented architecture:
- Erenshor remains authoritative for movement, combat, loot, grouping, progression;
- Deep Sims owns observation, verified memory, grounding, and social dialogue;
- persistent familiarity/rapport/rivalry tone;
- event-driven conversations from verified events;
- encounter/session memory;
- wiki and official-news retrieval;
- optional real-world current-news context;
- host-authority bridge for COOP;
- template/LLM expression modes;
- diagnostics for performance/grounding.

Core design rule:

```text
Erenshor decides what happened.
Deep Sims decides whether a social reaction is appropriate.
Templates or an LLM decide how it is expressed.
```

Technical lessons:
- provenance-aware memory;
- temporal history guard;
- silence/budget/cooldowns;
- small-model token discipline;
- deterministic fallback;
- optional runtime integrations rather than hard dependencies;
- never make generated dialogue proof of game history.

---

## 7. Erenshor Follow

Repository:
- https://github.com/forgetwhtuno/ErenshorFollow

Current README baseline:
- 0.3.2.

Category:
- local player follow/lead and party-Sim action menu.

Features:
- follow a grouped local Sim;
- Sim leads player to verified adjacent destination;
- party-Sim action menu;
- travel status UI;
- optional Practice Duel button.

Safety/architecture:
- current local party Sims only;
- remote COOP humans excluded;
- local NavMesh;
- no invented global world route;
- no teleporting;
- player movement cancels follow;
- combat pauses Lead;
- bounded retry for partial paths.

Technical lesson:
movement assistance should operate on **verified local navigation**, not an AI-generated route.

---

## 8. Practice Duels

Repository:
- https://github.com/forgetwhtuno/Erenshor-Duel

Current README baseline:
- 0.3.1.

Category:
- friendly non-lethal combat sandbox.

Features:
- local Sim party duels;
- virtual health;
- native effective damage;
- class combat/spells/skills;
- self-healing/HoTs/lifesteal/consumables;
- no real death/XP/loot/faction/save intent.

Safety:
- third-party party assistance suppressed;
- duelists blocked from harming unrelated actors;
- outside verified hostile engagement cancels;
- real health/effects/state restored;
- COOP remote humans excluded.

Technical lesson:
a gameplay sandbox is mostly about **complete boundary enforcement and cleanup**, not the challenge command.

---

## 9. Sim Inspector

Repository:
- https://github.com/xJeris/siminspect

Category:
- developer/player inspection UI.

Features:
- browse loaded Sim players from a searchable list;
- inspect gear, attributes, derived stats, resists, proficiencies;
- native item info window;
- current-zone highlighting;
- resizable UI.

Current requirement:
- Lunaris Mod Manager/API.

Technical lesson:
- a useful Lunaris-native reference;
- demonstrates inspecting persistent Sim information beyond nearby actors;
- good source for learning Lunaris UI/data access patterns.

---

## 10. ShorNet

Repository:
- https://github.com/et508/Erenshor.Shornet

Category:
- online/social service layer.

Current public feature:
- global chat.

Planned directions documented in README:
- private messages/channels;
- mail;
- attachments;
- auctions;
- friends/presence.

Technical lesson:
network-connected MMO-like services can be layered on top of Erenshor without trying to synchronize the entire combat world like COOP.

This is conceptually different from COOP:
- COOP synchronizes world/player gameplay.
- ShorNet focuses on service/social features.

---

## 11. AdventureGuide

Thunderstore category:
- https://thunderstore.io/c/erenshor/

Category:
- quest/navigation knowledge utility.

Thunderstore describes it as an in-game quest companion with:
- 170+ quest walkthroughs;
- item sources;
- world markers;
- clickable steps/GPS-style navigation.

Technical lesson:
a high-value Erenshor mod can be a knowledge/navigation overlay without changing gameplay state.

---

## 12. Erenshor minimap mods

Representative:
- https://github.com/drizzlx/erenshor-minimap

Category:
- world/UI visualization.

Thunderstore descriptions mention tracking:
- vendors;
- mobs;
- Sims;
- mining nodes;
- dungeon entrances.

Technical lesson:
- scene enumeration and marker classification;
- coordinate-to-UI transforms;
- actor/node lifecycle across zones.

Also note Erenshor's Server Admin Panel has a minimap-related option, so avoid assuming every user needs/mod wants the same map behavior.

---

## 13. Advanced Auction House

Representative:
- https://github.com/drizzlx/Erenshor-AdvancedAuctionHouse

Category:
- economy/UI enhancement.

Technical lesson:
- Auction House data/UI inspection;
- sorting/filtering;
- item metadata;
- patch-sensitive because v0.7 rewrote Auction House systems.

---

## 14. Character Browser / inspectors

Representative:
- https://github.com/xJeris/Erenshor-Char-Browser
- https://github.com/xJeris/siminspect

Category:
- character/Sim data inspection.

Technical lesson:
- persistent player/Sim records;
- equipment and stat presentation;
- useful reverse-engineering references for identity vs live actor.

---

## 15. Save file editor

Representative:
- https://github.com/Reckimus/Recks-Erenshor-Save-File-Editor

Category:
- external persistence tooling.

Technical lesson:
- save formats can be investigated independently of runtime mods.

Caution:
ordinary gameplay mods should **not** take this as permission to mutate save files. Save editing is its own high-risk tool category.

---

## 16. UI-focused mods

Representative public repositories:
- `lucas-xk/Erenshor-UI-Manager`
- `lucas-xk/Erenshor-Clean-Hotbars`
- `Tingilinde/ErenshorBankTabLabels`
- compare-equipment/UI packages on Thunderstore.

Technical lesson:
- reusable windows;
- hotbar/UI hierarchy;
- item hover/info;
- bank/UI tabs.

Risk:
Erenshor's UI and chat systems have already changed substantially during Early Access, so UI-hierarchy patches generally have a shorter shelf life than domain logic.

---

## 17. Combat/QOL mods

Representative public projects/packages:
- `hhawk51/Erenshor-Healbot`
- `Blackhorse311/ErenshorSwingSync`
- `Brad522/Erenshor-WeaponSets`
- `Reckimus/ErenshorPvP`
- `Reckimus/StatMultiplier`
- `staticextasy/XP-Multiplier`
- `staticextasy/SkipLogin`
- `staticextasy/Confirm-UI-Reset`
- `tigwyk/erenshor-glider`
- `GabrielleAkers/erenshor-speedup` (archived)

What they are useful for:
- locating combat/stat methods;
- skill/weapon state;
- targeting;
- login/UI flow;
- movement;
- time/XP modifications.

Caution:
small mods are often written to one exact game version and may use very direct patches. Treat them as symbol discovery, not architecture templates.

---

## 18. Content/localization mods

Representative:
- `sitxovski/ErenshorRU`
- `seyelive5/erenshor-korean-localization`
- `cammaron/Arcanism`
- `xJeris/ErenshorDoom`
- music/shader/transmog projects.

Useful for:
- text assets;
- localization interception;
- visual/material/shader changes;
- item/spell content;
- cosmetic systems.

These can require very different techniques from behavior mods.

---

## 19. Mod Installer / Vault ecosystem

Representative:
- `et508/ErenshorModInstaller`
- Erenshor Vault
- Lunaris

This indicates an ecosystem trend toward:
- game-specific discovery;
- dependency/update handling;
- standard metadata;
- easier installation than manual DLL copying.

A future mod intended for broad community use should consider both:
- Thunderstore/BepInEx audience;
- Lunaris/Vault audience.

---

## 20. Tool selection by problem

| Problem | Good reference projects |
|---|---|
| command parsing | ErenshorQoL, Lunaris docs |
| chat/social log | ErenshorLLM, Deep Sims |
| Sim identity/stats | Sim Inspector, Character Browser |
| local movement/NavMesh | Erenshor Follow |
| combat containment | Practice Duels |
| networking/world sync | Erenshor COOP |
| online social service | ShorNet |
| AH/economy | ErenshorQoL, Advanced Auction House |
| quest/world markers | AdventureGuide, minimap |
| Lunaris-native API | Lunaris, Sim Inspector |
| BepInEx build reference | ErenshorQoL, ErenshorLLM |
| sidecar/local model | ErenshorLLM, Deep Sims |

---

## 21. Compatibility relationships to keep in mind

### Deep Sims + Follow + Practice Duels

The three are designed as standalone modules:
- Deep Sims: observation/social/memory;
- Follow: movement/action UI;
- Practice Duels: combat sandbox.

Optional interaction should be detected at runtime rather than creating a forced dependency.

This decomposition is healthy because failure in one layer does not need to disable the others.

### Deep Sims + COOP

Deep Sims documents a host-authority model:
- host runs social director/LLM;
- normal client chat can become host-side context;
- generated Deep Sim chat remains host-local where the public COOP API cannot prove safe party-targeted replication.

This is a strong example of **not inventing a network capability**.

### Lunaris + legacy BepInEx

Lunaris claims legacy compatibility through a wrapper, but hot unload cannot guarantee every BepInEx mod is safe to unload. Test legacy mods in Lunaris before claiming support.

---

## 22. What is missing from the ecosystem

There is still no universally adopted, stable cross-mod semantic API for concepts like:
- current local party snapshot;
- raid roster;
- stable Sim identity;
- verified combat start/end;
- verified zone arrival;
- duel lifecycle;
- social-event bus.

This creates repeated reflection/Harmony work.

A high-value community project could eventually define a tiny **read-only compatibility/event library**. It should not try to become a giant game reimplementation.

Possible contracts:

```text
PartySnapshotChanged
CombatStateChanged
ZoneStable
LocalSimLoaded
LocalSimUnloaded
VerifiedLootObserved
OptionalFeatureCapabilities
```

Lunaris Aura may be a natural carrier for native Lunaris plugins; BepInEx could use a small public assembly/event surface.

---

## 23. Ecosystem source links

### Frameworks/managers
- https://thunderstore.io/c/erenshor/
- https://github.com/MizukiBelhi/Lunaris
- https://mizukibelhi.github.io/Lunaris-Docs/
- https://erenshorvault.app/

### Large/representative mods
- https://github.com/MizukiBelhi/ErenshorCoop
- https://github.com/Brumdail/ErenshorQoL
- https://github.com/aepod/ErenshorLLM
- https://github.com/xJeris/siminspect
- https://github.com/et508/Erenshor.Shornet
- https://github.com/drizzlx/erenshor-minimap
- https://github.com/drizzlx/Erenshor-AdvancedAuctionHouse

### Current standalone suite
- https://github.com/forgetwhtuno/DeepSim-erenshor
- https://github.com/forgetwhtuno/ErenshorFollow
- https://github.com/forgetwhtuno/Erenshor-Duel

### Additional public repositories worth searching
- https://github.com/Reckimus/Recks-Erenshor-Save-File-Editor
- https://github.com/xJeris/Erenshor-Char-Browser
- https://github.com/hhawk51/Erenshor-Healbot
- https://github.com/Brad522/Erenshor-WeaponSets
- https://github.com/lucas-xk/Erenshor-UI-Manager
- https://github.com/lucas-xk/Erenshor-Clean-Hotbars
