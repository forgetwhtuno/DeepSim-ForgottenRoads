# Erenshor Reference Pack

> **Dated development/reference snapshot (2026-08-10).** This file aggregates the component
> reference documents in this folder and may become stale as Erenshor changes. Current Deep Sims
> source and the current game assemblies remain authoritative.

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



---

# SOURCE FILE: 01_ERENSHOR_GAME_SYSTEMS.md

# Erenshor Game Systems Reference

**Research snapshot:** 2026-08-10  
**Primary purpose:** give an AI or mod developer a coherent mental model of Erenshor itself before touching implementation.  
**Version context:** v0.7 Planar March is live; routine v0.7 daily adjustment work ended in late July 2026 as v0.8 work began.

---

## 1. What Erenshor is

**[OFFICIAL]** Erenshor is a single-player RPG intentionally structured to feel like an older MMORPG. It is built around persistent simulated players, separated world zones, deliberate combat, long-tail item progression, faction consequences, guilds, and raid-style endgame content.

The important architectural fact is that the game is **not an actual MMO**. The world, puzzles, balance, and progression are designed around one human player. Community mods can add networking, but official game logic should be understood first as local single-player logic.

**[OFFICIAL]** SimPlayers do **not** use an LLM. The Steam page explicitly describes them as being driven by a mixture of state machines and decision trees. This is useful for mod design: a mod that adds generated dialogue is extending the social expression layer, not replacing the game's authoritative Sim AI.

### Current scale

The Steam Early Access description currently advertises:
- six playable classes;
- 36+ unique zones;
- 1,200+ items;
- level 1–35 content;
- dozens of quests and hundreds of NPCs;
- roughly 80–130+ hours for a first playthrough, with much more for min-maxing;
- endgame raid and horizontal progression systems.

The store states a planned Early Access exit in 2027. Treat all implementation details as patch-sensitive until 1.0.

Sources:
- https://store.steampowered.com/app/2382520/Erenshor/
- https://steamcommunity.com/app/2382520/allnews/

---

## 2. Core play loop

At a high level, Erenshor repeatedly cycles through:

```text
explore a zone
  -> find quests / camps / dungeons / named enemies
  -> group with SimPlayers
  -> fight and learn encounter behavior
  -> acquire loot / spells / skills / currency
  -> improve player and Sim gear
  -> sell/buy through vendors and Auction House
  -> unlock new zones, faction access, quests, runes
  -> build guild and endgame roster
  -> raid
  -> continue horizontal Ascension / item-quality progression
```

This loop matters to mods because most useful game state falls into one of a few domains:
- player identity and stats;
- SimPlayer identity and behavioral state;
- party/group role state;
- current target/combat encounter;
- zone/scene and navigation state;
- inventory/equipment/items;
- quests/flags/factions;
- economy/Auction House;
- guild/raid state;
- UI/chat state;
- persistence/save transitions.

A clean mod normally owns only one or two of those domains and observes the rest.

---

## 3. World model: zones rather than one continuous scene

**[OFFICIAL]** The Steam description explicitly calls Erenshor "a world divided" into separate zones. Zone boundaries are therefore first-class game transitions.

Practical implications:
- a Unity scene/zone change can invalidate object references;
- a Sim existing in world progression does not imply that its current Unity GameObject is loaded;
- "nearby" and "same zone" are materially different from "exists in the server simulation";
- mods that move actors across zones are touching a save/lifecycle-sensitive boundary;
- navigation mods should not infer a complete world route from a local NavMesh route.

The official wiki includes city, outdoor, dungeon, event, and raid zones. Port Azure is a central service hub containing systems such as the bank, Auction House, and class-related services.

**[INFERENCE]** A useful conceptual split for a mod is:

```text
persistent identity
    Sim/player exists in game data
loaded identity
    actor's zone/scene is loaded enough to inspect runtime state
local actor
    actor has a live Unity object in the current scene
eligible actor
    actor satisfies the mod's own safety constraints
```

Do not collapse these into one boolean.

Sources:
- https://store.steampowered.com/app/2382520/Erenshor/
- https://erenshor.wiki.gg/wiki/Zones
- https://erenshor.wiki.gg/wiki/Teleportation

---

## 4. Classes: current six-class roster

**[WIKI, CURRENT]** The official wiki currently lists six classes:

| Class | Broad identity | Common role emphasis |
|---|---|---|
| Arcanist | cloth magic user, broad spell toolkit | ranged magic DPS, control |
| Druid | nature/life/death magic | healing, magic DPS, utility |
| Paladin | heavy armor and weapon combat with Solunarian magic | primary tank, support |
| Reaver | melee class with stance-driven behavior | melee DPS and/or tanking |
| Stormcaller | bow + lightning/spell hybrid | ranged physical/magic DPS |
| Windblade | fast offensive melee, dual-wield style | melee DPS, debuffs/utility |

### Important stale-data warning

Older wiki pages and old mods may still refer to **Duelist**. Current class pages and the current Classes index use **Windblade**, Reaver, and Stormcaller. Any role/class test copied from 2024–2025 code should be checked against the current installed game.

This is a recurring Erenshor research problem: the wiki can contain old terminology in otherwise useful pages.

### Reaver

Recent wiki and v0.7 patch material describe Reaver as a stance-based melee class that can DPS or tank. The Planar March patch improved its tanking role, added Hateful Stance, raised its mitigation cap, removed Reckless Stance, and added other class adjustments.

### Stormcaller

Stormcaller blends bow attacks with magic. Its gameplay can depend on casting/imbuing interactions and mana. The wiki emphasizes Strength for bow damage, Intelligence for spell contribution, with Dexterity also useful.

### Windblade

Windblade is an aggressive physical damage class with dual-wielding, attack speed/extra attack/crit-style benefits, lifesteal, and utility/debuff effects. v0.7 included additional help for Windblade attack performance and Sim AI.

Sources:
- https://erenshor.wiki.gg/wiki/Classes
- https://erenshor.wiki.gg/wiki/Reaver
- https://erenshor.wiki.gg/wiki/Stormcaller
- https://erenshor.wiki.gg/wiki/Windblade
- https://steamcommunity.com/app/2382520/allnews/

---

## 5. Stats and proficiencies

### Seven main stats

**[WIKI]** Current wiki documentation lists:
- Strength
- Endurance
- Dexterity
- Agility
- Intelligence
- Wisdom
- Charisma

The page states that players begin with 12 in each main stat and primarily raise them through equipment and temporary effects rather than automatically increasing them each level.

Broad relationships described by the wiki include:
- Strength: melee/bow damage contribution;
- Endurance: HP and health regeneration;
- Dexterity: melee damage, hit, crit, block contribution;
- Agility: AC/dodge;
- Intelligence: spell damage, spell crit, MP, wand damage;
- Wisdom: healing strength and mana regeneration;
- Charisma: spell hit/damage and resist-related effects.

Other visible or derived stats include:
- HP;
- MP;
- Armor Class;
- elemental/magic/void/poison resistances;
- Resonance;
- Haste.

The wiki also calls out hidden/less-direct values such as run speed, attack speed, attack roll, mana regeneration, and life steal.

### Proficiencies

**[WIKI]** Proficiencies modify the effectiveness of associated stats. Current page mapping:
- Physicality -> Strength
- Hardiness -> Endurance
- Finesse -> Dexterity
- Defense -> Agility
- Arcanism -> Intelligence
- Restoration -> Wisdom
- Mind -> Charisma

The exact formulas for combat should be inspected in the live build when a mod needs authoritative damage/healing calculations. Public-facing descriptions are good enough for UI/knowledge work but should not be substituted for native calculation methods.

Sources:
- https://erenshor.wiki.gg/wiki/Stats
- https://erenshor.wiki.gg/wiki/Proficiencies

---

## 6. Leveling and Ascension

### Level cap

**[WIKI]** Level 35 is the current character cap and is also described by Steam as the planned launch cap.

### Ascension

At level 35, normal progression continues through Ascension:
- each Ascension level currently requires 38,000 XP;
- one Ascension level produces one point;
- there is no stated cap;
- general and class-specific Ascension skills exist;
- SimPlayers earn Ascension points too and allocate them automatically;
- `/ascview` can inspect a targeted SimPlayer's Ascensions.

**[STALE-RISK]** The older Leveling wiki page includes a full 1–35 XP table, but that page is much older than the current class pages. Use the current Ascension page for endgame facts and verify exact level-XP formulas before hardcoding them.

Sources:
- https://erenshor.wiki.gg/wiki/Leveling
- https://erenshor.wiki.gg/wiki/Ascension_Skills

---

## 7. SimPlayers: the system at the center of Erenshor

SimPlayers are not decorative NPCs. They are a persistent population that participates in many systems.

**[OFFICIAL/WIKI]** SimPlayers can:
- exist independently in the world;
- level and gear;
- be encountered solo or in groups;
- group with the human player;
- buy and sell through the Auction House;
- join/leave guilds;
- remember some interaction history;
- react to player treatment;
- participate in raid groups;
- progress while the human player is offline.

There are fixed/unique SimPlayers with established identity/personality/class, as well as generated Sims.

### Fixed identity vs runtime actor

For modding, a SimPlayer should be thought of as more than its scene object. A useful model is:

```text
Sim identity / progression record
    name, class, progression, personality, relationships, equipment, etc.

runtime representation
    current zone
    live actor GameObject/MonoBehaviour if loaded
    combat/target/path state
    current party membership

social presentation
    chat lines
    grouping responses
    guild behavior
```

Different mods may hook different layers.

### Offline world progression

**[WIKI, UPDATED JULY 2026]** Logging out causes simulated world activity. The wiki says every logout simulates at least six hours; if the player was offline longer, the full duration is simulated. This can advance SimPlayer auctions, equipment, and leveling.

This explains why a mod must not treat "I didn't observe it live" as equivalent to "it could not have happened" for ordinary vanilla progression.

### Friends and character binding

The `/friend` command can bind/rebind a targeted SimPlayer to the current character so its progression remains near enough for grouping. This is important when a mod stores relationships: vanilla friend/progression binding and a mod's own social memory are different systems.

### Sim memory/personality

The Steam store says SimPlayers have pre-written personalities/motivations and opinions, and remember how they are treated. The wiki describes greetings that can reference prior adventures, grouping, items, and time apart.

Do not assume this means there is one public "memory object." It is a player-facing behavioral guarantee, not an official modding API.

Sources:
- https://store.steampowered.com/app/2382520/Erenshor/
- https://erenshor.wiki.gg/wiki/Simulated_Players

---

## 8. Party management and roles

The party UI and role manager are core control surfaces.

### Roles documented by the official wiki

- **Main Tank** — attempts to hold aggression with damage/taunts.
- **Main Assist** — establishes the target other Sims focus.
- **Healing/Mana** — prioritizes healing/resource behavior.
- **Crowd Control** — manages control effects; exact interaction rules matter.
- **Puller** — actor designated to pull a target or auto-pull.

### Auto-pull settings

The wiki documents controls for:
- maximum target level above puller's level;
- maximum target level below puller's level;
- max pull distance;
- holding pulls under a configured group mana threshold.

### Party control buttons / common hotkeys

Current wiki describes:
- Attack (`Shift+1`)
- Assist MA (`Shift+2`)
- Follow (`Shift+3`)
- Pull Target (`Shift+4`)
- Auto-pull Toggle (`Shift+5`)
- Guard (`Shift+6`)
- Run Away (`Shift+7`)
- Manage Roles

Bindings can be customized; v0.7-era UI work changed hotkey labels to show the actual assigned key.

### Natural-language group command keywords

The Simulated Players page documents keyword detection in `/group` messages. Examples include:
- attack / kill / fight;
- pull / grab;
- stop pulling / hold pulls / no pulls;
- wait / guard / stay;
- follow / come;
- run / flee / escape;
- careful / cautious;
- aggressive / burn;
- mana;
- where / loc.

This is important for a chat mod: vanilla party chat can be a **gameplay command channel**. A mod that paraphrases, delays, suppresses, or consumes player group chat can accidentally change gameplay.

### Experience and grouping

The wiki documents party XP reductions per individual kill but describes grouping as increasing experience/loot throughput through faster respawns and party efficiency. Current documented split values:
- solo: 100%;
- 2 members: 70%;
- 3 members: 50%;
- 4 members: 40%.

Treat numeric values as balance data that can change.

Sources:
- https://erenshor.wiki.gg/wiki/Simulated_Players

---

## 9. Combat mental model

Erenshor combat is deliberate and can be lethal. The game emphasizes:
- target selection;
- aggro/threat;
- positioning and facing;
- class rotations;
- crowd control;
- healing/resource management;
- pulls and recovery;
- retreat/wipe handling.

The v0.7 update increased UI visibility for combat by adding or improving:
- floating NPC lifebars;
- target indicators;
- target-ring facing;
- an extended target window;
- more accurate DPS metering;
- raid/battle event communication.

### Sim awareness matters

A February 2026 patch fixed an edge case in which a hostile could be inside a Sim's awareness area but outside line of sight, engage the party, and fail to be re-assessed. This is evidence that vanilla Sim combat behavior has explicit perception/awareness logic.

### Do not emulate native damage unless necessary

For a mod like a duel system, native damage calculation should be treated as authoritative whenever possible. Reimplementing attack rolls, armor, resists, crits, buffs, procs, and class modifiers in mod code is fragile.

A safe pattern is:

```text
native game computes what the action would do
        ↓
mod intercepts at a narrow boundary
        ↓
mod translates/contains the result
```

rather than:

```text
mod guesses the game's entire combat formula
```

Source:
- https://steamcommunity.com/app/2382520/allnews/

---

## 10. Items and item quality

### Eight current quality tiers

**[WIKI, RECENT]**
1. Standard
2. Improved +1
3. Improved +2
4. Improved +3
5. Improved +4
6. Improved +5
7. Blessed
8. Ascended

Visual shorthand:
- Improved -> green sparkle
- Blessed -> blue
- Ascended -> purple

Only weapons and armor receive the normal quality stat bonuses. Charms may visually sparkle without receiving those bonuses; auras/general items are treated differently.

### Enemy drop quality roll

The recent wiki page lists:
- Standard: 94%
- Blessed: 1%
- Improved +1: 3.75%
- Improved +2: 0.875%
- Improved +3: 0.25%
- Improved +4: 0.10%
- Improved +5: 0.025%

Chest loot can use a different quality path.

### Merging Vessel

Two identical items of the same Standard/Improved tier can be combined at a forge to advance one Improved tier:
- 2 Standard -> +1
- 2 +1 -> +2
- 2 +2 -> +3
- 2 +3 -> +4
- 2 +4 -> +5

The wiki says these valid merges are deterministic. Reaching +5 from raw Standards takes 32 base copies.

### Blessed / Ascended paths

Current systems include:
- Ancient Coal for Blessed crafting;
- Braxonian Flame Well offerings;
- Mold: An Otherworldly Box + Planar Stone for guaranteed Blessed -> Ascended conversion;
- Inert Diamond for removing Blessed/Ascended quality back to Standard and generating Planar Stone Shards.

The recent wiki page contains detailed formulas. If a mod needs exact stat transformations, use that page or, preferably, inspect the native implementation rather than copying a summarized formula from another mod.

### SimPlayer equipment quality

The Sim inspection UI can perform certain quality upgrades on Sim equipment, with separate rules from the player offering path.

Source:
- https://erenshor.wiki.gg/wiki/Item_Quality

---

## 11. Inventory, bank, vendors, and Auction House

### Shared bank / multiple characters

**[OFFICIAL]** Erenshor supports multiple character slots and a shared bank. This is a major persistence boundary: a mod should not assume that every inventory-like item belongs solely to the active character.

### Auction House

**[WIKI]** The Auction House is a simulated economy used by players and SimPlayers.

Features include:
- filters by slot/class;
- name search;
- sorting including damage/delay;
- a player listings window;
- 18 listing slots according to the current wiki page;
- prices affected by Sim greed/personality;
- world economy affecting how much money Sims have available.

The wiki says SimPlayers currently buy gear such as armor and weapons, not ordinary scrolls/potions.

### World-economy implications

A sale is not simply a timer. Sims gain and spend resources through world activity. Logout/offline simulation can advance the economy.

For mods:
- do not assume Auction House listings are static;
- do not write direct item duplication/deletion paths casually;
- item transfer should respect inventory capacity/trash behavior;
- v0.7 included a substantial Auction House script rewrite and a July 24 hotfix for an item-duplication issue.

Sources:
- https://erenshor.wiki.gg/wiki/Auction_House
- https://steamcommunity.com/app/2382520/allnews/

---

## 12. Crafting and gathering

The official wiki documents several non-combat systems.

### Smithing

Smithing uses recipes/templates and materials at a forge. Fuel choice can matter; Ancient Coal has special quality implications.

### Mining

Mining requires a pickaxe and produces ores/materials. The wiki has historically documented a "Mining Power" property whose current mechanical role may be limited or planned, so treat exact mining formulas as patch-sensitive.

### Fishing

Fishing requires a pole and an appropriate water-facing position. Fish tables can depend on context such as time/day-night.

These systems matter for item databases and automation mods because:
- the player may temporarily equip a tool;
- crafting consumes several inventory objects atomically;
- time/context can matter;
- using native UI/actions is usually safer than mutating inventory data directly.

Sources:
- https://erenshor.wiki.gg/wiki/Smithing
- https://erenshor.wiki.gg/wiki/Mining
- https://erenshor.wiki.gg/wiki/Fishing

---

## 13. Guilds

**[WIKI]** The player can create or join a guild and recruit SimPlayers. SimPlayers may also initiate guild quests.

The Guild Manager provides:
- guild rankings;
- member lists;
- guild-hall summon functionality;
- group invitations;
- invite/kick controls;
- create/leave controls.

Friendliness/friend status helps when recruiting Sims.

### v0.7-era additions

A July 2026 update added a Guild Manager **Recruit** button and `/recruit` behavior that shouts in the current zone to attract SimPlayers. Guild rating affects recruitment success.

For a social mod, guild membership is a valuable **verified identity fact**, but a generated statement such as "we've raided together for years" is not implied by shared guild membership.

Sources:
- https://erenshor.wiki.gg/wiki/Guild
- https://steamcommunity.com/app/2382520/allnews/

---

## 14. Raids and Planar March

**[OFFICIAL]** v0.7 Planar March launched July 13, 2026 and was a major systems update.

The launch notes describe:
- four raid zones;
- 21 bosses in the launch notes;
- 130+ new items, spells, and skills;
- raids started/ended through Guild UI;
- saved raid configurations;
- an "A Team" roster in Guild UI;
- significant class and Sim AI changes;
- new group/raid loot-distribution UI;
- cosmetics;
- Sim inspect tabs for Ascensions, spells, and cosmetics.

Developer FAQ material says raids support three groups totaling **15 characters: the human player + 14 SimPlayers**.

### Raid access

The wiki documents raid unlock items/runes and four current raid destinations associated with major planar/god content. Exact display names have seen wording variations across wiki/news, so identify raids by current live UI when scripting.

### Why raids matter technically

Raid support forced broad game-system restructuring. A mod built against pre-v0.7 assumptions may fail around:
- group membership;
- "party" vs raid-group identity;
- assist/target assignment;
- event chat filters;
- loot distribution;
- Sim awareness distances;
- guild/raid UI state.

A mod that only understands the ordinary four-character party may not correctly understand a raid.

Sources:
- https://steamcommunity.com/app/2382520/allnews/
- https://erenshor.wiki.gg/wiki/Raids

---

## 15. Chat is both presentation and control surface

Erenshor chat includes:
- local/global-style social output;
- group chat;
- whispers;
- shout;
- guild communication;
- combat/event logs;
- raid/world/battle event filters.

### March 10, 2026 chat refactor

**[OFFICIAL, TECHNICALLY IMPORTANT]** The developer explicitly warned modders that chat messages changed from simple strings to a richer structure containing color/filter data. Mods using `LogAdd`, `LocalLogAdd`, or `CombatLogAdd` needed rework. Compatibility handlers existed, but support was not guaranteed.

This means:
- do not assume chat = one string;
- preserve log type/filter/color metadata when injecting output;
- do not consume a group command before vanilla parses it;
- do not hardcode colors when the game can expose native style;
- patch at the narrowest verified point.

Source:
- https://steamcommunity.com/app/2382520/allnews/

---

## 16. Save lifecycle and persistence

**[WIKI]** The Player Guide documents:
- player save recorded when opening/closing inventory;
- individual SimPlayers saved when their equipped items change;
- all SimPlayers saved on zoning;
- full game data saved on disconnect;
- Alt+F4 properly saves SimPlayer inventory;
- last 10 disconnect saves are retained for restoration through the login screen.

### July 15, 2026 save/load fix

**[OFFICIAL, HIGH IMPORTANCE FOR MODS]** The developer fixed a rare sequencing bug that could cause SimPlayers to lose Ascensions and level-35+ granted spells/skills. Stated triggers included:
- crashing while zoning/loading;
- mods that teleport SimPlayers;
- unexpected process closure;
- antivirus/backups or other system issues that interfere with save/load during zoning.

This is an explicit warning that actor movement and zone transitions can intersect sensitive persistence sequencing.

### Mod rule of thumb

Do not modify Erenshor save files unless the mod's explicit purpose is save editing and the format has been validated.

For ordinary mods:
- store mod-owned state in `BepInEx/config/...`, a Lunaris-owned config/data path, or another sidecar file;
- key records by stable game identity when possible;
- save atomically;
- tolerate missing/deleted sidecar data;
- never make the game save depend on a network/LLM request.

Sources:
- https://erenshor.wiki.gg/wiki/Player_Guide
- https://steamcommunity.com/app/2382520/

---

## 17. Built-in player and developer command surface

The game exposes many slash commands. Some are ordinary supported player features; others are debug/GM controls discovered/documented by community mods.

### Common player commands documented by wiki/community

Examples:
- `/players`
- `/time`
- `/loc`
- `/friend`
- `/ascview`
- `/r`
- `/dance`
- `/all players`
- `/shout`
- `/whisper`
- `/keyring`
- `/group`
- `/setname`

### Debug/GM commands

The ErenshorQoL project documents many internal/debug commands including scene listing, item spawning, Sim inspection, NPC lists, group data, target/loot inspection, developer invulnerability, faction adjustment, and scene teleportation.

**[CODE/STALE-RISK]** These are extremely useful for development but are not a stable public API. Names and availability can vary between Demo/full builds and patches.

Useful conceptual categories:
- enumerate items/scenes/NPCs;
- inspect selected actor;
- inspect loot;
- inspect group state;
- move to scene;
- modify level/proficiencies/faction;
- debug combat attackers;
- restore/test content.

Representative source:
- https://github.com/Brumdail/ErenshorQoL

---

## 18. Server Admin Panel / local ruleset

The login-screen Server Admin Panel exposes player/server-save modifiers and QOL toggles. This matters because a mod should not assume every user has the same:
- XP rate;
- NPC health scaling;
- minimap/QOL state;
- miscellaneous rules.

If a mod calculates "expected" progression or combat difficulty, it must account for the local ruleset or explicitly state that it is using defaults.

Source:
- https://erenshor.wiki.gg/wiki/Server_Admin_Panel

---

## 19. What changed materially in v0.7

A non-exhaustive mod-relevant list:
- raid groups and raid UI;
- four live raid zones;
- major class/Sim combat AI tuning;
- SimPlayers use food and water;
- SimPlayers disperse in Port Azure and can be recalled;
- Auction House rewrite;
- new item-quality progression;
- new loot-distribution UI;
- cosmetics for player and Sims;
- Sim inspection tabs for Ascensions/spells/cosmetics;
- Quick Bank/Sell/Trade interactions;
- floating health bars and target indicators;
- extended target window;
- improved DPS meter;
- guild recruiting changes;
- raid assist/targeting fixes;
- multiple save/load fixes.

Any mod written against an early 2025 binary should be considered suspect until rebuilt/tested against current assemblies.

---

## 20. Current development direction

As of the research date:
- v0.7 is the live raid-era baseline;
- the July 26, 2026 patch announcement said routine daily v0.7 changes were ending and v0.8 work was beginning;
- major v0.7 bugs may still be fixed;
- Erenshor remains Early Access, with 1.0 planned later.

Do not encode speculative v0.8 features as facts until they are in a current official announcement/build.

---

## 21. High-value mental rules for future AI reasoning

1. **Erenshor is authoritative.** Mods observe or narrowly intercept; they should not invent state.
2. **Sim identity is broader than a loaded GameObject.**
3. **Party chat may issue gameplay commands.**
4. **A normal party is not the whole raid model.**
5. **Zone transitions are lifecycle/save boundaries.**
6. **SimPlayers progress offline.**
7. **Current class names matter; old "Duelist" material is stale-risk.**
8. **Native damage/heal/item calculations beat reimplementation.**
9. **Current wiki pages beat old wiki pages, but installed assemblies beat both for technical work.**
10. **Game patches routinely refactor internal systems. Treat every Harmony target as versioned.**

---

## 22. Primary sources

- Steam store: https://store.steampowered.com/app/2382520/Erenshor/
- Steam news: https://steamcommunity.com/app/2382520/allnews/
- Official wiki home: https://erenshor.wiki.gg/
- Player Guide: https://erenshor.wiki.gg/wiki/Player_Guide
- Simulated Players: https://erenshor.wiki.gg/wiki/Simulated_Players
- Classes: https://erenshor.wiki.gg/wiki/Classes
- Stats: https://erenshor.wiki.gg/wiki/Stats
- Proficiencies: https://erenshor.wiki.gg/wiki/Proficiencies
- Ascension Skills: https://erenshor.wiki.gg/wiki/Ascension_Skills
- Item Quality: https://erenshor.wiki.gg/wiki/Item_Quality
- Auction House: https://erenshor.wiki.gg/wiki/Auction_House
- Guild: https://erenshor.wiki.gg/wiki/Guild



---

# SOURCE FILE: 02_ERENSHOR_MODDING_AND_TOOLING.md

# Erenshor Modding and Tooling Reference

**Research snapshot:** 2026-08-10  
**Purpose:** practical development reference for creating, inspecting, testing, and maintaining Erenshor mods.

---

## 1. There is no stable official Erenshor mod API

Erenshor modding is primarily runtime modification of a Unity Mono game. Public mods commonly reference the game's managed assemblies and use runtime patching, direct calls, Unity APIs, or reflection.

This means an Erenshor internal symbol is not a supported contract simply because several mods use it.

Use this priority:

```text
installed target build
  > exact Assembly-CSharp member/signature
  > current working mod source for same build
  > recent developer patch notes
  > older source examples
  > guesses (never)
```

---

## 2. The established path: BepInEx 5 + Harmony/HarmonyX

### BepInEx

**[OFFICIAL]** BepInEx is a plugin/patch framework for Unity games using the Mono scripting backend. It provides:
- plugin loading;
- configuration;
- logging;
- dependency management;
- runtime patching through Harmony;
- in-memory assembly patching possibilities.

A BepInEx plugin normally:
- is a .NET DLL;
- contains a class deriving from `BaseUnityPlugin`;
- is placed under `BepInEx/plugins`;
- is loaded after BepInEx starts with the game.

Erenshor's Thunderstore category pins a BepInEx pack for Mono Unity games, and many public Erenshor repositories are BepInEx plugins.

Sources:
- https://docs.bepinex.dev/v5.4.21/
- https://docs.bepinex.dev/v5.4.11/articles/dev_guide/plugin_tutorial/
- https://thunderstore.io/c/erenshor/

### Harmony / HarmonyX

Harmony-style patches intercept managed methods without permanently rewriting the installed DLL on disk.

Common patch forms:
- **Prefix** — runs before original; can inspect/change args/state and, with appropriate signature, skip the original.
- **Postfix** — runs after original and is useful for observation/augmentation.
- **Transpiler** — rewrites IL instruction flow; powerful and more brittle.
- **Finalizer** — exception/finalization behavior.
- dynamic target selection can locate a method at runtime.

For Erenshor, prefer:
1. Postfix observation;
2. Prefix with minimal changes;
3. small targeted reflection;
4. transpiler only if there is no stable boundary.

HarmonyX is Unity/BepInEx-oriented and is shipped by some current tooling.

Source:
- https://github.com/BepInEx/HarmonyX

---

## 3. The newer Erenshor-specific path: Lunaris

**[CODE/OFFICIAL PROJECT DOCS]** Lunaris is an Erenshor-specific plugin loader and mod manager intended to replace BepInEx for native Lunaris plugins while retaining a legacy compatibility wrapper for many BepInEx mods.

Current documented capabilities include:
- install/enable/disable/manage mods while the game is running;
- automatic mod updates;
- Erenshor Vault integration;
- a native Lunaris plugin API;
- compatibility wrapping for legacy BepInEx mods;
- built-in WIP console;
- built-in Unity Explorer through its developer bar;
- automatic runtime plugin reload during development;
- command registration;
- config registration;
- IPC ("Aura") between mods;
- included Mono.Cecil, MonoMod, Newtonsoft.Json, ImGui.NET/dear ImGui, 0Harmony, and modified BepInEx wrapper components.

### Minimal Lunaris shape

Project documentation shows a plugin derived from `LunarisPlugin` with a `[LunarisPlugin(...)]` attribute.

It also supports:
- `[LunarisCommand(...)]` for commands without manually patching `CheckCommands`;
- `Config.Register<T>()` for configuration;
- IPC endpoints through Aura.

### Critical cleanup difference

Because Lunaris supports unloading/reloading plugins while the game is running, `OnDestroy()` cleanup is not optional engineering hygiene—it is part of correctness.

Clean up:
- Harmony patches;
- Unity GameObjects;
- event handlers/delegates;
- static references;
- coroutines;
- tasks/cancellation tokens;
- ImGui registrations;
- IPC handlers;
- file watchers;
- sockets/HTTP listeners;
- UI objects.

Lunaris docs explicitly warn that failure to clean up causes leaks, especially with ImGui.

### Current limitation

The docs currently say load ordering is not supported yet. Do not design a Lunaris integration that silently depends on plugin A's `Awake()` always preceding plugin B.

Sources:
- https://github.com/MizukiBelhi/Lunaris
- https://mizukibelhi.github.io/Lunaris-Docs/
- https://erenshorvault.app/

---

## 4. BepInEx vs Lunaris: how to choose

| Need | BepInEx 5 | Lunaris |
|---|---|---|
| widest legacy Erenshor compatibility | strong | wraps many legacy mods |
| established Thunderstore workflow | strong | separate Vault path |
| Erenshor-specific command API | manual/game hook usually | built in |
| hot load/unload | not the normal model | first-class feature |
| built-in Unity Explorer | separate tool normally | included |
| IPC between mods | ad hoc/reflection/dependency | Aura |
| in-game mod install/update | manager outside game | core feature |
| old public examples | many | newer/fewer |
| cleanup on unload | still good practice | mandatory |

### Sensible 2026 strategy

For a new small standalone utility:
- BepInEx remains reasonable if targeting the existing Thunderstore ecosystem.
- Lunaris is attractive for native Erenshor tooling, runtime reload, command/config APIs, and cleaner cross-mod IPC.
- If the audience is mixed, keep game logic separate from loader glue so a thin BepInEx host and a thin Lunaris host can eventually share a core library.

---

## 5. Erenshor is a managed Unity/Mono target

Public projects commonly reference:
- `Assembly-CSharp.dll`
- UnityEngine assemblies
- `0Harmony.dll`
- `BepInEx.dll`

The managed game assemblies live under a path equivalent to:

```text
Erenshor/
  Erenshor_Data/
    Managed/
      Assembly-CSharp.dll
      UnityEngine*.dll
      Unity.TextMeshPro.dll
      ...
```

**[CODE]** `Brumdail/ErenshorQoL` currently targets `netstandard2.1` and references the installed game's `Assembly-CSharp.dll` and Unity libraries.

**[CODE]** `MizukiBelhi/ErenshorCoop` uses an older-style .NET Framework 4.7.2 project and references many Unity/Steam/network libraries.

**[CODE]** `aepod/ErenshorLLM` documents Unity 2022.3 LTS, Mono, BepInEx 5.4.x + HarmonyX, and .NET Standard 2.1.

This demonstrates that there is not one mandatory `.csproj` style. What matters is compatibility with the game's Mono runtime and referenced assemblies.

Representative sources:
- https://github.com/Brumdail/ErenshorQoL
- https://github.com/MizukiBelhi/ErenshorCoop
- https://github.com/aepod/ErenshorLLM

---

## 6. Build against the installed target profile, not a copied fantasy API

A robust local build should discover:
- game root;
- managed assembly directory;
- BepInEx core or Lunaris API;
- active mod profile if a manager is being used.

### Why this matters

Erenshor is updated frequently. A DLL built against an old `Assembly-CSharp.dll` can:
- fail to load;
- throw `MissingMethodException` / `MissingFieldException`;
- patch the wrong overload;
- silently miss a target;
- compile against a field that no longer has the same semantic role.

A build script that compiles against the user's **current installed assemblies** turns many compatibility errors into compile-time failures instead of runtime surprises.

### Representative approach from public ErenshorQoL source

The project tries:
- Steam App install registry;
- current Steam library;
- common Steam paths;
- an explicit game path;
- Thunderstore/BepInEx profile paths;
- then references `Erenshor_Data/Managed`.

This is a good pattern for user-facing build/install scripts.

---

## 7. Recommended project structure

For non-trivial mods, separate loader, compatibility, domain logic, UI, and persistence.

```text
MyErenshorMod/
  src/
    Plugin.cs                 # BepInEx or Lunaris host
    Compatibility/
      GameApi.cs              # all patch-sensitive game access
      OptionalMods.cs
    Domain/
      FeatureState.cs
      FeatureController.cs
    Hooks/
      ChatHooks.cs
      CombatHooks.cs
    UI/
      FeatureWindow.cs
    Persistence/
      ConfigStore.cs
  tests/
    DomainTests.cs
  README.md
  CHANGELOG.md
  LICENSE
```

The highest-value boundary is `Compatibility/GameApi.cs`: callers should ask for semantic facts such as `TryGetCurrentParty()` instead of scattering reflection across the whole project.

---

## 8. Direct game references vs reflection

### Direct references are usually best when:
- the symbol is visible/public;
- the mod builds against the active `Assembly-CSharp.dll`;
- the target has been stable;
- a compile break is preferable to a silent behavior change.

Advantages:
- type safety;
- better IDE navigation;
- faster;
- failures visible during compilation.

### Reflection is appropriate when:
- a member is private/internal;
- supporting multiple game versions;
- integrating an optional third-party mod without a hard dependency;
- a type may not exist at all.

### Safe reflection pattern

Do not do this repeatedly in `Update()`:

```text
Find type
Find field
Read it
Find another method
Invoke it
```

Instead:
1. resolve once at startup/scene transition;
2. cache `Type`, `FieldInfo`, `PropertyInfo`, `MethodInfo`, or delegates;
3. expose a typed semantic wrapper;
4. report capability status;
5. fail closed if shape is unexpected.

### Never invent a reflected member

If you cannot establish the member from:
- installed assembly;
- current public source;
- instrumentation;

return `Unknown` / disable the feature.

---

## 9. Reverse-engineering tool workflow

### ILSpy / dnSpy-family tools

BepInEx documentation itself points developers toward managed debugging/decompilation workflows. For Erenshor, `Assembly-CSharp.dll` is the highest-value assembly for exploring game-owned C# types.

Use decompilation for:
- locating a slash command path;
- finding target/combat methods;
- tracing save/zoning sequences;
- identifying UI components;
- enumerating class/enum names;
- discovering fields backing player-facing UI state.

### Lunaris Unity Explorer

Lunaris includes a Unity Explorer in its developer bar. This is particularly useful for:
- live GameObject hierarchy;
- components and fields;
- canvases/UI;
- active scenes;
- object names;
- checking whether a target is a real `SimPlayer` runtime component;
- confirming component lifetimes before/after zone changes.

### Logging/instrumentation first

Before patching behavior:
- log method entry;
- log actor/target type/name;
- log scene name;
- log relevant booleans/enums;
- verify exact sequence in game;
- then add mutation.

This is slower for five minutes and faster for five hours.

---

## 10. Game symbols known from public mod source

These are **observed public symbols**, not an official API.

Public ErenshorQoL documentation/source describes Harmony patches on:
- `TypeText.CheckCommands`
- `PlayerCombat.ToggleAttack`
- `NPC.AggroOn`
- `AuctionHouseUI.OpenListItem`

Public LLM/chat projects refer to:
- `UpdateSocialLog`
- `UpdateSocialLog.GlobalAddLine`
- historically `LogAdd`, `LocalLogAdd`, `CombatLogAdd`

Official March 2026 patch notes also name the old log methods when warning modders of the chat refactor.

These are useful search anchors in `Assembly-CSharp.dll`.

---

## 11. Chat hooking rules

### Rule 1: preserve vanilla command handling

`/group` text can control Sim behavior. A social/chat mod should not intercept and discard it before vanilla command parsing.

### Rule 2: handle structured messages

After March 10, 2026, chat has richer color/filter structures. Do not assume every output is a bare `string`.

### Rule 3: avoid recursion

If your hook observes a chat output and then adds a replacement line through the same output function, mark/inhibit self-generated calls to avoid infinite interception.

### Rule 4: preserve source identity

Know whether a line is:
- player party chat;
- Sim group chat;
- whisper;
- combat;
- zone/world event;
- mod output.

### Rule 5: UI is not memory

A line displayed in chat is not automatically a verified game event.

---

## 12. Unity lifecycle rules

### Scene/zone changes

On a zone transition:
- cached GameObjects can be destroyed;
- UI canvases may be rebuilt;
- NPC/Sim runtime components can be recreated;
- NavMesh data changes;
- the game may save Sim state.

Therefore:
- invalidate scene-bound references;
- reacquire after scene load;
- do not retain stale `Transform` or `MonoBehaviour` references;
- do not run actor mutations during partially loaded transitions.

### `Update()` is a hot path

Avoid:
- `FindObjectsOfType` every frame;
- large LINQ scans;
- regex construction;
- reflection lookup;
- file I/O;
- HTTP/LLM calls;
- JSON serialization;
- full-party snapshot rebuilding when nothing changed.

Prefer:
- event hooks;
- periodic low-frequency refresh;
- cached collections;
- dirty flags.

---

## 13. NavMesh and movement mods

Erenshor Follow demonstrates a conservative movement architecture:
- local player only;
- current grouped local Sim;
- local NavMesh paths;
- verified adjacent exits;
- no invented global routing;
- movement input cancels follow;
- partial path retries bounded;
- pauses during real combat;
- resumes only after a safety delay;
- remote COOP humans excluded.

This is a good model for any movement feature:
**navigation should be bounded by what the current scene can prove.**

Source:
- https://github.com/forgetwhtuno/ErenshorFollow

---

## 14. Combat-interception mods

Practice Duels demonstrates a containment architecture:
- real/native attack math remains authoritative;
- results are intercepted and mapped to virtual health;
- third-party effects are blocked at the duel boundary;
- real health is restored on all exit paths;
- outside hostile combat cancels the sandbox;
- real resources such as mana/cooldowns/consumables remain real unless explicitly virtualized.

The larger principle:

```text
define the safety boundary first
then enumerate every path that can cross it
```

For a combat sandbox, that includes:
- direct melee;
- spells;
- skills;
- DoTs;
- HoTs;
- procs;
- pets;
- lifesteal;
- potions;
- group assist;
- death;
- aggro;
- zone transition;
- cleanup.

Source:
- https://github.com/forgetwhtuno/Erenshor-Duel

---

## 15. Network/sidecar work must stay off Unity's main thread

ErenshorLLM and Deep Sims both illustrate sidecar/local-model patterns.

A safe model:

```text
Unity main thread
  capture bounded immutable snapshot
        ↓
background I/O / model request
        ↓
parse + validate outside hot frame work
        ↓
marshal tiny final action back to main thread
```

Do not:
- synchronously call HTTP in `Update()`;
- hold Unity objects across background threads;
- mutate Unity GameObjects from arbitrary worker threads;
- let an LLM response directly issue gameplay commands.

---

## 16. Optional mod integration

There are three common approaches.

### Hard dependency

Use when feature cannot function without the other mod.

Pros:
- typed API.
Cons:
- installation/load-order constraints.

### Runtime reflection

Useful for BepInEx-era optional integration:
- find plugin/type;
- verify version/member;
- bind delegate;
- expose capability;
- fail safely.

This is how a standalone suite can remain independent.

### Lunaris Aura IPC

For Lunaris-native plugins, Aura provides a more deliberate inter-plugin communication path when both sides expose endpoints.

Best long-term design:
- small, versioned data contracts;
- read-only status/event surfaces by default;
- no cross-mod poking private fields.

---

## 17. Co-op is a separate authority problem

Community Erenshor COOP is not merely "enable another human actor." Its project contains:
- Steam lobby support;
- player sync;
- enemy/NPC sync;
- chat and whispers;
- grouping;
- spells/effects;
- Sim sync;
- healing;
- buffs/debuffs;
- XP;
- summons;
- trading;
- network entity/action/status/transform packets;
- client/server grouping;
- zone ownership.

Its project references Steamworks.NET, LiteNetLib, Unity modules, and substantial networking code.

Therefore a local mod must explicitly distinguish:
- local human;
- local vanilla Sim;
- remote human represented in a game-compatible actor shape;
- networked Sim/proxy;
- ordinary NPC.

Never use "looks like SimPlayer" as sufficient authorization for movement, PvP, memory, or social ownership.

Source:
- https://github.com/MizukiBelhi/ErenshorCoop

---

## 18. Configuration and logging

### BepInEx

Use `Config.Bind`-style BepInEx configuration and the plugin logger. First-run config files normally live under `BepInEx/config`.

### Lunaris

Use `Config.Register<T>()` for native Lunaris plugins.

### Logging principles

Log:
- plugin version and detected game build;
- whether each Harmony target was found;
- optional integration detection;
- scene/zone transition;
- feature start/stop reason;
- safety cancellation reason;
- exception with enough actor/method context to reproduce.

Avoid:
- per-frame spam;
- full LLM prompts in normal logs;
- private user content by default;
- thousands of repeated identical errors.

---

## 19. Packaging on Thunderstore

Thunderstore packages generally require:
- `manifest.json`
- `README.md`
- 256x256 `icon.png`

Common optional file:
- `CHANGELOG.md`

Manifest dependencies use strings in the form:

```text
TeamName-PackageName-Version
```

The Erenshor Thunderstore category is a mature distribution path for BepInEx mods.

Sources:
- https://thunderstore.io/c/erenshor/
- https://wiki.thunderstore.io/mods/creating-a-package

---

## 20. Erenshor Vault / Lunaris distribution

Lunaris integrates with Erenshor Vault:
- https://erenshorvault.app/

Lunaris docs show optional assembly metadata to associate a manually installed mod with a Vault ID.

Because this ecosystem is newer and evolving:
- inspect current Vault/Lunaris docs before packaging;
- do not assume Thunderstore metadata maps 1:1;
- keep your README usable independent of either manager.

---

## 21. Development test matrix

Every behavior-changing mod should test at least:

### Startup
- clean startup;
- missing config;
- old config;
- dependency absent;
- dependency present.

### Scene lifecycle
- login;
- enter game;
- zone once;
- zone repeatedly;
- return to Port Azure;
- disconnect;
- reconnect.

### Actor lifecycle
- Sim joins;
- Sim leaves;
- Sim dies;
- Sim revives;
- actor moves out of range;
- target changes;
- party disbands.

### Combat
- combat start;
- combat clear;
- add joins;
- pet participates;
- group heal/buff;
- wipe/run;
- cooldown/effect cleanup.

### Save-sensitive
- change Sim equipment;
- zone;
- disconnect normally;
- relaunch;
- verify no vanilla state was lost.

### Compatibility
- no other mods;
- common UI/QOL mods;
- COOP if claimed compatible;
- Lunaris legacy wrapper if claimed compatible.

### Update
- rebuild against current assemblies;
- check patch targets;
- run smoke test before publishing.

---

## 22. Acceptance checks should be observable

Bad acceptance criterion:

> "The mod safely supports raid groups."

Good acceptance criterion:

> "With a 15-character raid active, `/mydiag party` identifies the player's ordinary subgroup separately from the full raid roster; the feature never targets a member outside the configured scope; zoning and ending the raid clear cached references without exceptions."

For every feature, define:
- trigger;
- allowed actors;
- forbidden actors;
- expected state transition;
- cleanup;
- diagnostic output.

---

## 23. Common failure modes

1. **Copied stale method signature**  
   Fix: compile against current `Assembly-CSharp.dll`.

2. **Patch succeeds but semantics changed**  
   Fix: live instrumentation and post-patch acceptance test.

3. **Stale Unity reference after zone change**  
   Fix: clear scene-bound caches.

4. **Reflection in `Update()`**  
   Fix: cache metadata/delegates.

5. **Chat mod consumes party command**  
   Fix: preserve vanilla command route.

6. **LLM blocks main thread**  
   Fix: background request with bounded snapshot.

7. **Third-party actor mistaken for local Sim**  
   Fix: positive local identity/authority checks.

8. **UI duplicated after reload**  
   Fix: idempotent create/destroy and Lunaris `OnDestroy()` cleanup.

9. **Save corruption/loss after actor teleport**  
   Fix: avoid crossing zone/save sequencing without proof.

10. **Compatibility via secret private-field mutation**  
    Fix: expose explicit runtime API/IPC/event contract.

---

## 24. Recommended minimal development toolchain

### BepInEx path
- .NET SDK
- IDE/editor with C# support
- current Erenshor installation
- BepInEx 5 pack/profile
- `Assembly-CSharp.dll` + needed Unity assemblies
- Harmony/HarmonyX
- ILSpy or equivalent managed decompiler
- BepInEx log
- Git

### Lunaris path
- .NET SDK
- current Erenshor installation
- `Lunaris.dll`
- built-in Lunaris dev bar/console/Unity Explorer
- current Lunaris API docs
- Git

Optional:
- PowerShell build/install script;
- unit tests for domain logic;
- file-based golden tests for parsers/grounding;
- GitHub Actions for source-only tests that do not require proprietary game assemblies.

---

## 25. Sources

- BepInEx docs: https://docs.bepinex.dev/
- BepInEx plugin tutorial: https://docs.bepinex.dev/v5.4.11/articles/dev_guide/plugin_tutorial/
- HarmonyX: https://github.com/BepInEx/HarmonyX
- Thunderstore Erenshor: https://thunderstore.io/c/erenshor/
- Lunaris: https://github.com/MizukiBelhi/Lunaris
- Lunaris docs: https://mizukibelhi.github.io/Lunaris-Docs/
- Erenshor Vault: https://erenshorvault.app/
- ErenshorQoL: https://github.com/Brumdail/ErenshorQoL
- Erenshor COOP: https://github.com/MizukiBelhi/ErenshorCoop
- ErenshorLLM: https://github.com/aepod/ErenshorLLM
- Deep Sims: https://github.com/forgetwhtuno/DeepSim-erenshor
- Erenshor Follow: https://github.com/forgetwhtuno/ErenshorFollow
- Practice Duels: https://github.com/forgetwhtuno/Erenshor-Duel



---

# SOURCE FILE: 03_ERENSHOR_TECHNICAL_SURFACE_AND_PATTERNS.md

# Erenshor Technical Surface and Safe Modding Patterns

**Research snapshot:** 2026-08-10  
**Purpose:** a searchable index of public technical evidence.  
**Critical caveat:** this is not an official API reference. Every exact game symbol is versioned evidence.

---

## 1. Confidence model for technical symbols

Use:

- **HIGH** — named by current official patch notes and/or used by current public mod code.
- **MEDIUM** — used in public source but repository/game compatibility may be older.
- **LOW** — inferred from filenames/behavior or old source only.

A symbol can exist and still be semantically unsafe. "Method exists" and "method is the right hook" are separate questions.

---

## 2. Known assemblies

### Game-owned

Public projects commonly reference:

```text
Erenshor_Data/Managed/Assembly-CSharp.dll
```

This is the main search target for Erenshor-authored managed game code.

Common Unity assemblies used by mods include:
- `UnityEngine.dll`
- `UnityEngine.CoreModule.dll`
- `UnityEngine.UI.dll`
- `Unity.TextMeshPro.dll`
- `UnityEngine.InputLegacyModule.dll`
- `UnityEngine.AIModule.dll`
- `UnityEngine.PhysicsModule.dll`
- `UnityEngine.AudioModule.dll`
- `UnityEngine.ParticleSystemModule.dll`
- `UnityEngine.UnityWebRequestModule.dll`

Exact assembly split depends on what the mod uses.

### Mod framework

Common:
- `BepInEx.dll`
- `0Harmony.dll`

Lunaris-native:
- `Lunaris.dll`

Representative evidence:
- https://github.com/Brumdail/ErenshorQoL
- https://github.com/MizukiBelhi/ErenshorCoop
- https://github.com/aepod/ErenshorLLM
- https://github.com/MizukiBelhi/Lunaris

---

## 3. Public symbol anchors

### `TypeText.CheckCommands`

**Confidence: MEDIUM-HIGH**

ErenshorQoL documents Prefix patching of `TypeText.CheckCommands` to add slash commands.

Use:
- as a reverse-engineering anchor for legacy BepInEx command injection;
- not as the only possible command system forever.

Lunaris-native plugins can register commands through `[LunarisCommand]`, avoiding a manual `CheckCommands` patch.

Risk:
- command parsing can change;
- multiple mods patching the same method must coexist;
- a Prefix that returns false too broadly can suppress vanilla commands/chat.

---

### `PlayerCombat.ToggleAttack`

**Confidence: MEDIUM**

ErenshorQoL documents a Postfix to this method for auto-attack automation.

Potential semantic use:
- observe player autoattack state transition.

Risk:
- do not infer all combat starts from one toggle;
- offensive skills/spells can engage combat through other paths.

---

### `NPC.AggroOn`

**Confidence: MEDIUM**

ErenshorQoL documents a Postfix used to react when a hostile NPC aggros.

Potential semantic use:
- observe an aggro transition.

Risk:
- one NPC's aggro is not necessarily "party combat";
- pets, adds, friendly/non-hostile actors, remote COOP proxies, scripted bosses, and raid mechanics may require filtering.

---

### `AuctionHouseUI.OpenListItem`

**Confidence: MEDIUM**

ErenshorQoL documents a Postfix used for automatic listing-price behavior.

Potential use:
- augment AH listing UI.

Risk:
- v0.7 rewrote much of the Auction House scripts;
- July 2026 also saw an AH item-dupe bug fix;
- revalidate signatures and item lifecycle before copying old code.

---

### `UpdateSocialLog`

**Confidence: HIGH as a chat-system anchor**

Multiple public LLM/social mods reference `UpdateSocialLog`.

`aepod/ErenshorLLM` documents a Harmony patch on:

```text
UpdateSocialLog.GlobalAddLine
```

to intercept chat through a centralized path and classify by log type.

The official March 10, 2026 patch notes explicitly mention prior methods:
- `LogAdd`
- `LocalLogAdd`
- `CombatLogAdd`

and state that chat moved from strings to a richer data structure containing color/filter information.

Potential use:
- observe/inject chat;
- learn native style/filter behavior.

Risks:
- recursion from injected lines;
- suppressing command-bearing group chat;
- losing color/filter metadata;
- false memory if treating displayed dialogue as verified game events.

Sources:
- https://github.com/aepod/ErenshorLLM
- https://github.com/forgetwhtuno/DeepSim-erenshor
- https://steamcommunity.com/app/2382520/allnews/

---

## 4. File/class names visible in Erenshor COOP architecture

These are **mod-owned classes/files**, not vanilla game classes, but they show the domain boundaries required to network Erenshor.

The public project contains concepts such as:
- `NetworkedPlayer`
- `NetworkedSim`
- `NetworkedNPC`
- `PlayerSync`
- `SimSync`
- `NPCSync`
- client/server connection managers;
- client/server grouping;
- group packets;
- entity spawn/data/transform/action/attack/status-effect packets;
- player message/action/request packets;
- zone ownership;
- dropped items;
- weather;
- guild modules;
- Steam lobby/networking.

This is evidence that multiplayer representation crosses many systems. A local mod cannot safely assume a remote human is "just another Sim."

Source:
- https://github.com/MizukiBelhi/ErenshorCoop

---

## 5. Chat pipeline pattern from ErenshorLLM

The public README describes:
- BepInEx C# plugin;
- Harmony hook at a centralized social-log path;
- classification by log type;
- synchronous fast combat-template handling;
- asynchronous dialog paraphrase;
- Rust sidecar on localhost;
- OpenAI-compatible HTTP endpoints;
- local RAG/memory.

This is a useful general pattern even when not using that implementation:

```text
game event
  -> classify
  -> decide whether to leave vanilla untouched
  -> create bounded context
  -> optional async external/local processing
  -> validate output
  -> inject presentation
```

Crucially, do not use:

```text
game event
  -> LLM
  -> gameplay command
```

unless the mod's explicit purpose and safety design require it—and even then deterministic game validation must remain authoritative.

Source:
- https://github.com/aepod/ErenshorLLM

---

## 6. Deep Sims trust-boundary pattern

Deep Sims 0.7.x explicitly separates:

```text
OBSERVED_NOW
> EXPERIENCED
> REMEMBERED
> WIKI / official game news
> external real-world news
> HEARD
> UNKNOWN
```

The high-value technical idea is not the specific labels; it is that **provenance is stored with facts**.

Examples:
- current party roster -> observed;
- verified duel result -> experienced event;
- compact record derived from verified outings -> remembered;
- player says "we killed X yesterday" -> heard, not automatically true;
- generated Sim line -> conversation, not world state.

This should be reused for any AI/social/history mod.

Source:
- https://github.com/forgetwhtuno/DeepSim-erenshor

---

## 7. Safe actor classification

Before acting on a target, prefer a positive classification pipeline.

```text
Is target non-null and live?
Is it a player-like actor or ordinary NPC?
Is it a vanilla/local Sim identity?
Is it in the current scene?
Is it in the allowed party/raid scope?
Is it a remote human proxy from COOP?
Is it alive?
Is it already in conflicting combat/state?
Is the feature allowed in this mode?
```

For a feature that should affect only local SimPlayers, exclusion should include:
- human local player;
- remote COOP humans;
- ordinary vendors/quest NPCs;
- pets/summons;
- hostile monsters;
- non-loaded persistent Sim identities;
- local Sims outside permitted scope.

### Why "component exists" is insufficient

A networking mod may deliberately represent a remote human using game-compatible components. Authorization must be based on ownership/origin/capability, not just class shape.

---

## 8. State snapshots beat live-object threading

For background processing:

Bad:
```text
worker task retains SimPlayer MonoBehaviour
worker reads transform/fields later
```

Better:
```text
main thread:
  capture SimSnapshot {
    stable id
    name
    class
    level
    zone
    hp/mana
    role
    target summary
  }

worker:
  consumes immutable plain data
```

Then marshal only the final UI/social result back to the Unity main thread.

---

## 9. Encounter-state model

A strong combat-observation mod should distinguish:

```text
CURRENT ENCOUNTER
  still mutable

LAST COMPLETED ENCOUNTER
  frozen after verified quiet/completion

SESSION/OUTING TOTAL
  aggregate
```

Do not answer:
- "How was the last fight?" using session totals;
- "How is this fight going?" using a previous fight;
- "we killed another X" unless a prior verified X kill exists.

A completion quiet period is useful because combat can briefly drop between adds/mechanics.

---

## 10. Party vs raid state

v0.7 raids support three groups / 15 total characters.

Do not expose a single ambiguous method like:

```text
GetPartyMembers()
```

if the feature may run in raids.

Prefer explicit semantics:

```text
GetLocalSubgroup()
GetRaidRoster()
GetCurrentMainTank(groupId)
GetCurrentMainAssist(groupId)
IsInRaid()
```

Even if the first implementation returns only normal party state, the name prevents accidental future overreach.

---

## 11. Roles: capability is not assignment

Class-based guesses are not enough.

Example:
- Druid **can** heal.
- That does not prove the Druid is the currently assigned Healing/Mana role.
- Paladin **can** tank.
- That does not prove it is the Main Tank.

For grounded social/UI work, preserve:

```text
class capability
actual verified role assignment
inferred likely role
unknown
```

as separate values.

---

## 12. Zone transition state machine

Recommended compatibility wrapper:

```text
Stable
  -> ZoneTransitionStarted
  -> InvalidatingSceneRefs
  -> SceneLoaded
  -> WaitingForGameStateReady
  -> Reacquire
  -> Stable
```

During the middle:
- do not start a duel;
- do not issue NavMesh movement;
- do not persist a "completed arrival" event merely because a scene changed;
- do not resolve actor identity through stale objects.

### Why conservative waiting helps

The July 2026 save/load bug report explicitly connected crashes/loading and mod-driven Sim teleportation to a historical save sequencing problem. Even though that bug was fixed, it demonstrates that transition order matters.

---

## 13. UI architecture choices

### Native Unity UI

Benefits:
- visual consistency with Erenshor;
- can reuse native windows/styles.

Risks:
- hierarchy/name changes;
- scene-dependent lifetime;
- EventSystem/input conflicts.

### IMGUI / `OnGUI`

Benefits:
- fast diagnostics;
- independent of complex canvas hierarchy.

Risks:
- older styling;
- input/focus issues;
- per-frame allocations if careless.

### ImGui via Lunaris

Benefits:
- developer-friendly;
- strong for tooling;
- built into Lunaris ecosystem.

Risk:
- must unregister/clean up on hot unload.

### Native-style hybrid

Erenshor Follow uses Erenshor-like panels and buttons while keeping its own feature state. This is a good consumer-mod pattern.

---

## 14. Navigation technical pattern

A local navigation feature should answer:
- start point;
- target point;
- same scene?
- NavMesh path complete?
- partial?
- actor moving?
- player gave manual movement input?
- combat active?
- target changed/destroyed?
- timeout exceeded?

State example:

```text
Idle
Following
LeadingToExit
PausedForCombat
WaitingForGroup
RecoveringPartialPath
Stopped
```

Never hide failed routing by teleporting unless teleportation is the explicit feature.

---

## 15. Virtualized combat technical pattern

For a non-lethal duel:

```text
real HP saved
virtual HP initialized
native action occurs/intercepts
effective native result computed
real lethal consequence suppressed
virtual HP updated
yield threshold checked
cleanup always runs
real HP restored
```

Every ingress path must be categorized:
- direct player -> duel Sim;
- duel Sim -> player;
- player self-heal;
- Sim self-heal;
- party heal;
- pet hit;
- hostile third party;
- status effect tick;
- proc/summon;
- consumable.

"Works for autoattack" is not sufficient.

---

## 16. Third-party effect isolation

A good 1v1 boundary uses two questions:

1. **Who caused the effect?**
2. **Who receives the effect?**

This permits:
- duelists affecting each other;
- duelists affecting themselves where allowed;

while rejecting:
- party member -> duelist;
- duelist -> unrelated world actor;
- friendly pet -> duelist;
- hostile world actor -> duelist (usually cancel and hand control back to vanilla).

---

## 17. Cross-mod compatibility contracts

### Bad integration

```text
Find "OtherMod.InternalController"
Set private bool "_duelActive"
```

### Better reflection integration

```text
Try find public static:
  IsActive
  TryChallenge(actorId)
  GetStatus()
```

### Best native ecosystem integration

Versioned event/status API or Lunaris Aura:

```text
PracticeDuel.Completed {
  duelId
  participantStableIds
  completedUtc
  reason
}
```

Consumers should not need to know the internal health implementation.

---

## 18. Sidecar AI architecture

A robust local AI feature has separate failure domains:

```text
Erenshor
  remains playable if mod fails

mod social controller
  remains usable in deterministic/template mode if LLM fails

local inference service
  can restart independently

external knowledge
  optional and request-scoped
```

Add:
- timeout;
- cancellation;
- health status;
- failure cooldown;
- bounded queue;
- max prompt size;
- max generated length;
- duplicate suppression.

Never make zoning, combat, save, or party command handling wait for inference.

---

## 19. Deterministic social layer

For tiny MMO phrases, a model is often unnecessary.

Examples of events that can use deterministic templates:
- ready acknowledgement;
- thanks;
- brief loot reaction;
- post-duel spectator line;
- "oom"/recovering;
- greeting.

Benefits:
- instant;
- zero inference load;
- predictable grounding;
- survives Ollama/sidecar outage;
- makes LLM use reserved for richer language.

This is the architectural direction documented in Deep Sims 0.7.x.

---

## 20. Observability / diagnostics

Every complex feature should expose a compact diagnostic command.

Useful fields:

```text
plugin version
game build / scene
mode
local actor
selected actor
party size / raid status
current combat
feature state
last transition reason
last Harmony target result
optional mod capabilities
last exception
```

For AI:
```text
request id
queue time
wall time
provider/model
prompt token estimate / actual if available
generation tokens
timeout/failure
frame hitch correlation (not causation)
```

For movement:
```text
path status
distance
destination
pause reason
last replanning time
```

For duels:
```text
participants
virtual HP
real HP saved?
interference/cancel reason
effects tracked for cleanup
```

---

## 21. Public technical sources

- Erenshor official patch notes: https://steamcommunity.com/app/2382520/allnews/
- ErenshorQoL: https://github.com/Brumdail/ErenshorQoL
- Erenshor COOP: https://github.com/MizukiBelhi/ErenshorCoop
- ErenshorLLM: https://github.com/aepod/ErenshorLLM
- Lunaris: https://github.com/MizukiBelhi/Lunaris
- Lunaris docs: https://mizukibelhi.github.io/Lunaris-Docs/
- Sim Inspector: https://github.com/xJeris/siminspect
- Deep Sims: https://github.com/forgetwhtuno/DeepSim-erenshor
- Erenshor Follow: https://github.com/forgetwhtuno/ErenshorFollow
- Practice Duels: https://github.com/forgetwhtuno/Erenshor-Duel

---

## 22. How an AI should use this file

When asked to write Erenshor code:
1. locate the current repository/branch;
2. read its `AGENTS.md`/README if present;
3. inspect relevant existing source;
4. establish the target game's current assembly shape;
5. identify whether the requested symbol is HIGH/MEDIUM/LOW confidence;
6. reuse an already-working accessor/hook when possible;
7. do not invent a missing game member;
8. keep the change narrow;
9. add a diagnostic/acceptance check;
10. preserve cleanup and save boundaries.



---

# SOURCE FILE: 04_ERENSHOR_MOD_ECOSYSTEM.md

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



---

# SOURCE FILE: 05_ERENSHOR_AI_MOD_DEVELOPMENT_GUIDE.md

# Erenshor AI-Assisted Mod Development Guide

**Research snapshot:** 2026-08-10  
**Purpose:** a durable set of rules for an AI coding agent working on Erenshor mods.

This file intentionally focuses on process and architecture rather than one feature.

---

## 1. First principle: do not hallucinate the game API

Erenshor does not expose a stable official mod SDK.

When asked to implement a feature:

```text
READ current repo instructions
  -> INSPECT current code
  -> ESTABLISH exact current game/member evidence
  -> MAKE smallest coherent change
  -> BUILD against installed assemblies
  -> TEST/DIAGNOSE
```

Never:
- invent a class;
- invent a field;
- invent an enum value;
- invent a slash command;
- invent a network API;
- assume an old wiki class name is current;
- assume a public mod built for an old patch still has correct signatures.

If evidence is missing, leave a clear compatibility TODO or runtime capability-off path.

---

## 2. Source trust order

For coding:

```text
1. current repository source
2. current installed Erenshor assemblies
3. current live diagnostics
4. current official patch notes
5. recent working public mod source
6. recent official wiki
7. old public source/wiki
8. inference
```

For player-facing lore/mechanics:

```text
1. observed current game
2. official developer announcement
3. recently maintained official wiki
4. older wiki
5. community guide
```

Do not silently merge contradictions.

---

## 3. Erenshor should own gameplay truth

For social/AI mods:

```text
Erenshor decides:
  movement
  pathfinding
  target legality
  attacks
  heals
  loot
  grouping
  roles
  quests
  saves
  item ownership
  faction
  combat result

mod decides:
  whether a verified event is interesting
  what UI to show
  what bounded memory to retain
  whether to request language generation

LLM decides:
  wording/social intent only
```

The model should not call game methods directly.

---

## 4. Separate fact provenance

Every fact in an AI context should have provenance.

Recommended categories:

```text
OBSERVED_NOW
VERIFIED_EVENT
DERIVED_MEMORY
OFFICIAL_KNOWLEDGE
WIKI_KNOWLEDGE
PLAYER_SAID
SIM_SAID
GENERATED
UNKNOWN
```

### Example

```text
OBSERVED_NOW:
  Phanty is in current subgroup.
  Phanty class = Arcanist.

VERIFIED_EVENT:
  Practice duel between player and Dancer ended.

PLAYER_SAID:
  "Phanty tanked this last night."

GENERATED:
  Dancer: "Phanty would probably tank it."
```

Only the first two can establish game history.

---

## 5. Do not turn dialogue into memory truth

Generated chat is evidence that a line was said, not evidence that its content happened.

Unsafe:
```text
LLM says "remember when we killed Brax?"
-> save "party killed Brax"
```

Safe:
```text
verified boss-death event says Brax died
-> save compact verified event
-> later LLM may reference it
```

---

## 6. Temporal language requires evidence

Guard phrases such as:
- again;
- last time;
- remember when;
- back here;
- another one;
- still;
- used to;
- always.

They often imply history.

If no verified supporting memory exists, rewrite:

```text
"Let's see how this one goes."
```

instead of:

```text
"Let's not wipe again."
```

---

## 7. Class/role grounding

Never conflate:
- class;
- possible role;
- assigned role;
- current behavior.

Example:

```text
Druid
  possible: healing / magic DPS
  assigned: unknown
  current observed action: casting heal
```

A single heal does not prove the role assignment. A class does not prove the role assignment.

---

## 8. Party and raid grounding

At v0.7:
- ordinary group logic exists;
- raids can contain three groups and 15 total characters.

A social/controller mod should carry:
- local subgroup;
- whole raid roster;
- party/raid membership;
- human vs local Sim vs remote human;
- role scope.

Do not call the whole raid "the party" internally if that creates ambiguity.

---

## 9. Actor authority check before every action feature

For movement/combat/action UI, require:
- known actor;
- local;
- correct actor category;
- current scene;
- alive;
- allowed membership;
- no conflicting state.

If COOP is installed, explicitly exclude remote humans unless the feature was designed and tested for them.

---

## 10. Chat must preserve vanilla gameplay commands

Group chat can drive Sim behavior.

Therefore:
- observe but do not consume command-bearing player lines;
- inject social output after vanilla has had its command path;
- preserve log/filter metadata;
- mark mod-generated output to avoid reprocessing.

A "better chat AI" that breaks `/group follow` is a regression.

---

## 11. Use a Social Director, not random chatter hooks

All autonomous speech should flow through one admission controller.

It should consider:
- event priority;
- global cooldown;
- per-Sim cooldown;
- per-event cooldown;
- recent player speech;
- current combat;
- semantic duplication;
- rolling message budget;
- current eligibility;
- authority/COOP host state.

Then one social moment produces at most one coherent thread.

This avoids five independent features all talking at once.

---

## 12. Templates are a first-class mode

Use deterministic responses for:
- ready checks;
- rolls;
- brief greetings;
- trivial acknowledgements;
- OOM/recovery;
- simple post-fight reactions;
- duel spectator reactions.

Use an LLM when:
- language variation matters;
- context is richer;
- several verified facts need synthesis;
- the player asks an open-ended question.

This reduces latency and hallucination risk.

---

## 13. External knowledge stays separate

Distinguish:
- game wiki;
- official game patch/news;
- real-world web/news;
- conversation.

A real-world news result should never become:
- Erenshor lore;
- Sim personal history;
- permanent relationship memory.

Give external context a TTL.

---

## 14. Never block Unity for AI

Required architecture:

```text
main thread:
  capture snapshot
  enqueue work

background:
  HTTP/model/RAG
  parse response

main thread:
  validate actor/session still relevant
  display line
```

Use:
- timeout;
- cancellation;
- bounded queue;
- failure cooldown;
- max concurrency.

If a request returns after the player zoned or party changed, revalidate before output.

---

## 15. Keep prompts bounded

Good prompt context is not "everything known."

Use:
- current question/event;
- current party identity;
- current encounter;
- last completed encounter if relevant;
- a few verified memories;
- a few conversation lines;
- relevant wiki snippet only when needed.

Do not serialize the whole session/history on every line.

---

## 16. Performance methodology

Measure separately:
- Unity-side snapshot time;
- queue delay;
- request wall time;
- provider/model load time;
- prompt evaluation;
- generation;
- main-thread frame hitches.

Do not say "the AI caused a hitch" just because the times overlap. Report correlation.

---

## 17. Sidecar failure should degrade gracefully

Recommended modes:

```text
Auto
  templates for trivial events
  LLM where useful if healthy
  templates if provider unavailable

LLM
  prefer LLM
  safe fallback on failure

Templates
  no inference requests

Off
  no autonomous social output
```

Core game and deterministic utility features must keep working if Ollama/model/service is gone.

---

## 18. Cross-mod integration should be optional and versioned

If Deep Sims wants Practice Duel events:
- detect whether Practice Duels is installed;
- bind a small read-only event/status surface;
- never require it for base startup;
- ignore unknown future versions safely.

If Follow wants a duel button:
- show it only when capability exists;
- do not compile the whole feature into a hard dependency unless necessary.

With Lunaris, prefer Aura IPC for new native integrations.

---

## 19. Game update response procedure

After every substantial Erenshor patch:

1. read official patch notes for subsystem refactors;
2. record current game/build;
3. rebuild against current assemblies;
4. verify every Harmony target;
5. run deterministic unit tests;
6. run smoke test:
   - login;
   - party;
   - combat;
   - zone;
   - disconnect;
7. test exact feature paths;
8. test cleanup/unload if Lunaris;
9. update compatibility docs.

The March 2026 chat rewrite and July 2026 raid/AH/save changes demonstrate why this is necessary.

---

## 20. Do not regress working safety boundaries

When expanding a feature, preserve the strongest established constraint.

Example, Practice Duels:
- adding nearby non-party Sims should not loosen third-party isolation;
- a broader challenge target set must still positively exclude remote humans;
- keep virtual health/restoration;
- keep hostile cancellation.

Example, Follow:
- adding route intelligence should not turn into teleportation;
- global destination knowledge should not bypass local NavMesh proof.

Example, Deep Sims:
- richer memory should not let generated dialogue become verified history;
- more autonomous banter should not bypass the central budget.

---

## 21. Prefer feature flags/capabilities to version guesses

Bad:
```text
if gameVersion >= 0.7:
  call NewMethod()
```

Better:
```text
if compatibility.TryBindNewMethod(out handler):
  capability = true
else:
  capability = false
```

Version strings help diagnostics, but actual member shape is what matters for a runtime mod.

---

## 22. Build/install scripts should fail safe

A build script should:
- locate game/profile;
- validate required DLLs;
- compile;
- stop on errors;
- copy only the intended output;
- avoid overwriting source or arbitrary files;
- print exact target path/version.

A patch/apply script should:
- verify source blocks;
- refuse partial mutation if assumptions fail;
- make a backup or use git;
- be idempotent where possible.

---

## 23. Test deterministic core without the game

Move logic out of MonoBehaviours where possible.

Unit-test:
- event admission;
- cooldowns;
- memory provenance;
- temporal-history guard;
- duplicate suppression;
- relationship math;
- duel virtual-health math;
- target eligibility using plain DTOs;
- command parsing;
- serialized sidecar data migrations.

Keep Unity/game access behind adapters.

---

## 24. In-game diagnostics are part of the product

When something fails, users should be able to produce:
- plugin version;
- detected capabilities;
- feature state;
- last rejection/cancel reason;
- last relevant actor;
- current zone;
- optional integration status.

This shortens debugging dramatically.

---

## 25. Security/privacy for local-AI/network mods

Do not commit:
- API keys;
- personal memory exports;
- private endpoints;
- auth tokens.

Default local services to:
- localhost;
- no unauthenticated LAN binding unless explicitly enabled;
- limited endpoints;
- bounded input size.

Do not send chat/history to a cloud provider without a clear user setting.

---

## 26. Recommended AI coding-agent prompt fragment

Use this with future tasks:

> Work from the current repository and current installed Erenshor assemblies, not remembered API names. Inspect relevant code before modifying it. Erenshor internals are not a stable public API: do not invent members, enum values, commands, network capabilities, or game behavior. Prefer existing proven accessors/hooks. Keep gameplay authoritative in Erenshor; AI may observe and express, not directly control gameplay unless the requested deterministic feature explicitly requires it. Preserve current safety boundaries, COOP remote-human exclusions, save/zone lifecycle safety, and cleanup. Add an observable acceptance check and diagnostics for new compatibility assumptions.

---

## 27. Representative sources

Game/current behavior:
- https://store.steampowered.com/app/2382520/Erenshor/
- https://steamcommunity.com/app/2382520/allnews/
- https://erenshor.wiki.gg/

Modding:
- https://docs.bepinex.dev/
- https://github.com/BepInEx/HarmonyX
- https://github.com/MizukiBelhi/Lunaris
- https://mizukibelhi.github.io/Lunaris-Docs/

Architecture examples:
- https://github.com/MizukiBelhi/ErenshorCoop
- https://github.com/Brumdail/ErenshorQoL
- https://github.com/aepod/ErenshorLLM
- https://github.com/forgetwhtuno/DeepSim-erenshor
- https://github.com/forgetwhtuno/ErenshorFollow
- https://github.com/forgetwhtuno/Erenshor-Duel
