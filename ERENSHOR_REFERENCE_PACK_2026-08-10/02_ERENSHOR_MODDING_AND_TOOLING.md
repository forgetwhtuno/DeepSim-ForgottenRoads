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
