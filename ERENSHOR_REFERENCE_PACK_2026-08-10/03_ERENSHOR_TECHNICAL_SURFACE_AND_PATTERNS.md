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
