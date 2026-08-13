# Deep Sims Architecture Audit

**Audit date:** 2026-08-13  
**Base:** `agent/lunaris-native-deepsims` @ `b21b4d4150e0baffcc2d48ec6b44d6489e057d86`

## 1. Authority model

```text
Erenshor native state
    owns movement / combat / heal / loot / grouping / roles / quest / faction / saves
            |
            v
Deep Sims observation + provenance
    party snapshots / telemetry / verified events / bounded social memory / reference knowledge
            |
            v
Social admission
    intent + grounding + cadence + SocialBudget + current eligibility
            |
            v
Expression only
    Templates OR local Ollama
            |
            v
Quality + grounding + perspective enforcement
            |
            v
main-thread revalidation -> visible social log
```

Generated dialogue is evidence that a line was said, never evidence that its factual content happened.

## 2. Plugin lifecycle

```text
Lunaris Awake
  -> register typed config
  -> initialize Deep Sims data directories
  -> create wiki/news/external-news/Ollama clients
  -> resolve current player-character scope
  -> create scoped MemoryStore / DeepSlotManager / SessionTelemetry
  -> create SocialBudget + SocialDirector
  -> Harmony PatchAll

Update (Unity main thread)
  -> sync MMO/Roleplay config
  -> frame-hitch observation
  -> verify/switch character scope
  -> duplicate-instance heartbeat
  -> drain queued main-thread callbacks
  -> flush delayed group output (FINAL visible group boundary)
  -> clone/queue dirty memory snapshots
  -> periodic party refresh
       -> current Deep Sim slots
       -> WorldSnapshot / native identity
       -> SessionTelemetry
       -> SocialDirector
       -> Campmaster context

OnDestroy
  -> stop request admission / clear pending lanes
  -> advance conversation generation / clear delayed output
  -> clear queued main-thread callbacks
  -> stop legacy local-player follow
  -> clear Duel/PvP/Nemesis Deep-Sims-owned static bookkeeping
  -> finish telemetry / shutdown scoped memory writer
  -> unsubscribe COOP + Campmaster AssemblyLoad handlers
  -> Harmony UnpatchSelf
  -> reset Roleplay transient state
  -> clear Instance if this object owns it
```

The inference semaphore is intentionally not disposed while an old worker might still call `Release()`.

## 3. Configuration and logging

`DeepSimsSettings` is the Lunaris typed config source; compatibility `DeepSimsConfigEntry<T>` wrappers preserve existing `.Value` consumers. Config includes AI endpoint/model/inference, expression mode, activity cadence, perspective, knowledge sources, memory/social settings, performance diagnostics, and optional integrations.

Default logs now prefer metadata/reasons over content. Prompt JSON, raw model reply text, raw user news/wiki queries, rejected generated text, and final generated line text are not normal diagnostic payloads. Explicit `/dsexport` and `/dsmemory` remain user-requested content surfaces.

## 4. Command parser

Deep Sims intentionally retains its established Harmony-backed `TypeText.CheckCommands` command parser rather than a Lunaris command-attribute rewrite. Recognized commands preserve current syntax, including `/aistatus`, `/dsims`, `/dssocial`, `/dsroleplay`, `/dstalk`, `/dwhisper`, `/dsmemory`, `/dssession`, `/dsperf`, `/dswiki`, `/dsxnews`, `/dsexport`, and diagnostics.

Unknown vanilla/game commands fall through. Party/gameplay-control phrases are not handed to the LLM as authority; standalone Follow remains the deterministic owner of its movement commands when present.

## 5. Party snapshot and Sim identity

`DeepSlotManager`/`SimContextReader` build immutable-ish `SimSnapshot` data on the main thread from live game state. Background model work receives snapshots/strings, not Unity objects.

Identity authority:

- name: native Sim tracking/avatar;
- class: native `CharacterClass`, normalized (`Duelist` -> `Windblade`);
- level: native stats;
- current party/group membership: native grouping/party structures plus eligibility checks;
- zone: current live world/scene snapshot;
- health/combat: native observed state where used;
- role: native role reader where verified; class capability is not role assignment;
- guild/friend/equipment: only when an explicit verified reader supplies it; not inferred by LLM/wiki/name.

Wiki/official knowledge can explain a class. It cannot decide which class a Sim is.

## 6. Memory and persistence

```text
verified character identity
  -> CharacterScopeKey (slot+name when trustworthy; name fallback)
  -> plugins/config/DeepSims/Memory/Characters/<key>/
  -> per-Sim bounded JSON memory
  -> single bounded writer queue
  -> temp file + replace/overwrite fallback
```

Memory classes keep experienced/verified event summaries separate from heard/generated conversation. Historical generated `deep_group_chat` factual events are migrated out rather than promoted to truth.

Character switch is a full conversational ownership boundary. It invalidates delayed display/request generations and replaces memory/telemetry/social-director state. Delayed thread writes are allowed only if both character generation and conversation generation still match their origin and the same `MemoryStore` instance is current.

Old flat `Memory/*.json` data is preserved as unscoped legacy data and is not auto-claimed.

## 7. Grounding / provenance

Approximate authority order:

```text
OBSERVED NOW
  > VERIFIED EVENT / experienced telemetry
  > bounded derived memory from verified events
  > official/wiki reference knowledge
  > HEARD player/remote-human/dialogue context
  > generated text
  > unknown
```

Grounding rejects unsupported history, class contradictions, unsupported acquisitions/events/relationships, temporal claims such as `again`/`last time` without evidence, and instruction/assistant leakage. It separately allows bounded subjective expression—preference, emotion, humor, uncertainty—when no fabricated fact/history is implied.

## 8. Direct party request flow

```text
player party text
  -> command/control exclusion + visible-player-line handling
  -> advance conversation generation (newest player turn owns the thread)
  -> classify intent
       factual knowledge
       native identity fact
       opinion/preference/social banter
       history/current encounter/etc.
  -> capture WorldSnapshot + SimSnapshot + scoped SimMemory
  -> optional knowledge retrieval (experienced -> official/wiki/external as applicable)
  -> bounded Party request lane
  -> PromptBuilder (MMO OR Roleplay identity, never both)
  -> Ollama model selection / bounded retry
  -> sanitize
  -> grounding / quality / retry with original critical context retained
  -> perspective-aware deterministic fallback if needed
  -> QueueGroupMessage (central Roleplay/quality admission)
  -> delayed typing queue carries conversation generation
  -> main-thread fresh speaker eligibility
  -> native typing personalization
  -> Roleplay spoken-style cleanup + FINAL RoleplayOutputGuard
  -> final quality + stale-generation checks
  -> UpdateSocialLog.LogAdd
```

## 9. Whisper flow

```text
/dwhisper or supported whisper input
  -> resolve active local Deep Sim
  -> capture scoped memory/world
  -> bounded Whisper request lane (max pending lanes are bounded)
  -> prompt / Ollama / grounding retry
  -> main-thread re-resolve current speaker
  -> native style + sanitize + instruction checks
  -> deterministic fallback if needed
  -> FINAL Roleplay guard
       if replacement fallback is created, validate replacement again
  -> conversation record only in current scoped store
  -> UpdateSocialLog.LogAdd
```

This second final boundary is structural: whispers do not use the group delayed-typing queue.

## 10. Autonomous/social director flow

```text
verified events + telemetry + current party state + bounded memory callbacks + idle/camp context
  -> SocialDirector opportunity selection
  -> priority / global cooldown / per-topic/per-Sim suppression / recent player speech
  -> SocialBudget
  -> expression mode
       Off -> silence
       Templates -> MMO templates OR Roleplay templates
       Auto/LLM -> local Ollama if healthy, safe template fallback otherwise
  -> grounded/perspective-safe candidate
  -> QueueGroupMessage
  -> same FINAL group display boundary
```

Adaptive/Quiet/Normal/Lively alter cadence/probability, not authority. Lively remains subject to budgets, cooldowns, duplicate suppression, current eligibility, combat/player-speech priority, and thread caps.

## 11. Templates and perspective

Expression mode and perspective are orthogonal:

```text
              MMO perspective             Roleplay perspective
LLM           MMO-player prompt            in-world adventurer prompt + final guard
Templates     SocialTemplates              RoleplayTemplates/RoleplayExpressionRouter
Off           silence                      silence
Auto          healthy LLM or templates     healthy LLM or Roleplay templates
```

The audit fixed two Roleplay template leaks: ritual replies and Sim-to-Sim continuation replies no longer call MMO templates when Roleplay is active.

## 12. Ollama / request scheduler

The plugin has replaceable bounded request lanes rather than unbounded model work:

- party: one replaceable pending item;
- autonomous: one replaceable pending item;
- whispers: bounded small pending set;
- one pump per plugin instance;
- one `SemaphoreSlim` inference gate per plugin instance.

Requests use bounded `HttpWebRequest` timeouts. Conversation generations are checked before/after expensive work and again at queue/display boundaries. Late stale work cannot become visible or commit scoped social memory after the new guards.

Residual lifecycle limitation: an already-running HTTP request is not actively cancelled by `OnDestroy`; it may finish inertly after unload. This is why pending-request hot-unload remains a live release test.

## 13. Display boundary inventory

Only `DeepSimsPlugin.WriteChat` injects native social-log output. Most calls are `[DeepSims]` status/diagnostic/player echoes rather than Sim speech.

Sim-spoken boundaries are:

1. delayed group line -> final Roleplay guard -> `WriteChat("<Sim> tells the group: ...")`;
2. incoming generated whisper -> final Roleplay guard -> `WriteChat("<Sim> tells you: ...")`.

`/dswiki`, `/dsnews`, `/dsxnews`, `/aitest`, diagnostics, and memory inspection are explicit system/user tools, not Sim dialogue and therefore are not subjected to the Sim Roleplay voice invariant.

## 14. Knowledge sources

- Session telemetry/current outing: highest relevance when it directly answers the question.
- Official Erenshor Steam news: current patch/update facts.
- Wiki: game definitions/mechanics/lore reference, never current Sim identity/history authority.
- External real-world news: optional, TTL/request-scoped context; never persisted as Erenshor lore or Sim history.

All network work is background/bounded and final presentation is main-thread marshalled.

## 15. Optional integrations

### Campmaster

Reflection/capability-based, read/social only. `AssemblyLoad` handler is explicitly removed on shutdown. Deep Sims does not toggle Guard/Auto Pull or choose pull targets.

### Follow

Standalone Follow is detected and owns normal follow/lead movement when present. The legacy Deep Sims `/dsfollow` path affects only the local human player and is stopped during Deep Sims unload. Deep Sims does not use it to move Sims.

### Duel

Structured/fallback verified duel lifecycle enters the existing social director. Duel owns accept/decline/combat/result. Deep Sims owns only whether/how current Deep Sims react. Dedup state resets on unload.

### PvP / Nemesis

Fact-only sanitized event bridges. Gameplay/outcome authority remains in those mods. Deep Sims' short dedup windows reset on unload.

### COOP

Remote humans are not local Deep Sims. Remote-human chat is HEARD/conversation context, not verified game-event truth. Generated Deep Sim speech remains host-local because the reviewed COOP send surface does not prove a safe party-recipient filter. No invented replication is added.

### Party Tools / Contracts / Guild / Journal

No hard compile dependency is required for core Deep Sims behavior. Shared suite integration should use explicit versioned status contracts as they mature, not private-field mutation.

## 16. Suite Hub API

`DeepSimsControlApi` schema v1 is a late-bound optional status/control surface. It exposes primitive/string data only and safe social settings. It deliberately excludes raw memory, secrets, task/thread handles, Unity objects, arbitrary command execution, and gameplay actions. There is no dedicated Deep Sims UI to open yet, so no fake UI action is exposed.

## 17. Performance hot paths

SOURCE VERIFIED observations:

- party scanning/telemetry/social refresh is periodic, not a full scene scan every frame;
- reflection-heavy optional compatibility resolves/caches members rather than rediscovering every frame;
- HTTP/model work and memory disk writes are background work;
- `MemoryStore.FlushPending` on the main thread clones bounded dirty state and queues writes rather than performing the file write itself;
- Update's `_mainThreadActions` drain is an unbounded `while` loop. Current request scheduling is bounded, so no evidence shows it caused the supplied hitches, but it remains a theoretical burst point worth measuring before changing;
- default debug logging is now smaller because raw prompt/reply payloads are removed.

LIVE VERIFIED correlation: supplied hitch durations were vastly larger than the adjacent measured party-refresh durations. Do not equate “Deep Sims observed the hitch” with “Deep Sims caused the hitch.”

## 18. Privacy surfaces

Normal logs should not contain raw prompt/reply/player-query/memory payloads. Explicit content-bearing user actions remain:

- `/dsmemory <Sim>` — shows bounded memory in local chat;
- `/dsexport` — writes session/social-history export and labels it private;
- `/dsdump` — writes detailed current-game diagnostic and warns to review before sharing.

API keys are config-only and are not intentionally logged/exported. Deep Sims writes only mod-owned sidecar data, never native save files.

## 19. Test architecture

`tests/RUN_DETERMINISTIC_TESTS.ps1` compiles a standalone pure-logic executable. `StandaloneRegressionMain` currently calls 13 suites:

1. `DeterministicRegressionTests`
2. `GroundingGuard`
3. `ReplyCompletenessGuard`
4. `DuelSocialSemantics`
5. `RelationshipModel`
6. `ChatRoutingRegression`
7. `QualityReliabilityDeterministicTests`
8. `ConversationTurnGuardTests`
9. `ConversationPacingTests`
10. `RoleplayDeterministicTests`
11. `CharacterScopeDeterministicTests`
12. `DeepSimsControlPolicyTests`
13. `DiagnosticPrivacyTests`

The runner's source list is complete in this audit snapshot. The audit environment could verify wiring/lexical structure but could not execute C# because Windows `csc.exe` and the real managed game/Lunaris references were not available.

## 20. Release conclusions

### SOURCE VERIFIED

- gameplay-authority boundary remains intact;
- Roleplay has final Sim-speech enforcement at both visible Sim-speech boundaries;
- native class identity is not inferred from wiki/LLM/memory/name;
- template perspective parity covers the audited paths;
- request queues/generations are bounded and stale-display guarded;
- memory is now character-scoped and delayed persistence is generation/store guarded;
- COOP and optional integrations remain conservative/absent-safe;
- Hub API is optional/primitive/safe;
- unload removes Harmony/AppDomain handlers and resets Deep-Sims-owned static runtime state;
- normal diagnostic payload logging is substantially privacy-hardened.

### NEEDS LOCAL VERIFICATION

- deterministic suite compile/run;
- full plugin compile against the user's current `Assembly-CSharp.dll`, Unity modules, `Lunaris.dll`, and `0Harmony.dll`;
- compiled assembly dependency inspection for zero BepInEx reference.

### NEEDS LIVE TEST / RELEASE BLOCKERS

- final Roleplay output quality in LLM and Templates modes;
- exact Dancer/Windblade question matrix;
- pending-request Lunaris unload/re-enable, repeated;
- duplicate native-Lunaris instance coexistence via new instance diagnostics;
- multi-character memory/context isolation;
- any performance fix beyond measurement/correlation.
