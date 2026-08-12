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
