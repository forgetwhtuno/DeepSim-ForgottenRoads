# Deep Sims Conversation Seeding — Local AI Implementation Handoff

You are implementing ambient conversation and conversation seeding in the user's local `DeepSim-erenshor` source.

The local source may be ahead of GitHub. **Inspect local source first. Do not assume this research snapshot is authoritative over newer local work.**

Research snapshot used by this handoff:

```text
Repository: forgetwhtuno/DeepSim-erenshor
GitHub main snapshot: 5449401570e923017f820d1585305247d97cde28
Development line: 0.7.x
```

Read:

```text
AGENTS.md
docs/CONVERSATION_SEEDING_DESIGN.md
docs/CONVERSATION_SEED_SCHEMA.md
```

Then inspect the actual local implementations of:

```text
DeepSimsPlugin.cs
SocialDirector.cs
EventConversationDirector.cs
SocialFoundation.cs
PromptBuilder.cs
MemoryStore.cs
GroundingGuard.cs
GameEventHooks.cs
SessionTelemetry.cs
SimContextReader.cs
RelationshipModel.cs
Models.cs
DeterministicRegressionTests.cs
tests/RUN_DETERMINISTIC_TESTS.ps1
```

Also search the local tree for:

```text
seed
topic
fatigue
semantic
ambient
idle
camp
conversation director
social budget
```

## Mission

Refactor Deep Sims toward:

```text
verified game state + memory + relationships + recent events
    -> deterministic candidate conversation seeds
    -> semantic/topic fatigue
    -> explicit SILENCE candidate
    -> one selected grounded subject
    -> existing social budget
    -> short seed-bound conversation
    -> template or LLM expression
```

The desired result is not more dialogue.

The desired result is:

> When a Sim speaks autonomously, there is usually a concrete reason that particular Sim brought up that particular subject.

## Non-negotiable constraints

1. Erenshor still controls gameplay.
2. The LLM never invents or selects gameplay actions.
3. Code retrieves/derives facts; the LLM only expresses a supplied subject.
4. Generated/player/vanilla dialogue remains HEARD unless an existing explicit policy says otherwise.
5. Do not let relationship scores establish factual history.
6. Do not let a private player conversation become global Sim knowledge.
7. No valid seed may safely result in silence.
8. Keep `GroundingGuard` as the final safety boundary.
9. Keep the existing central `SocialBudget`; do not build a second autonomous cadence system.
10. Preserve existing expression modes (`Auto`, `LLM`, `Templates`, `Off`).
11. Preserve COOP host-authority behavior.
12. Do not add ambient web/wiki/news lookups merely to fill silence.
13. Do not persist every seed as memory.
14. Do not guess Erenshor API fields.

## What the GitHub snapshot already contains

Before changing code, verify whether local source still matches these observations:

### Existing event conversation system

The GitHub snapshot has `SocialEventCandidate` + `EventConversationDirector`.

It already supports:

- importance
- novelty
- verified context
- eligible speakers
- duplicate fingerprints
- higher-priority event competition
- short expiry
- probability gate
- central `SocialBudget`
- 1–3 line verified event threads
- `NO_MESSAGE`
- deterministic self-tests

**Preserve/refactor this. Do not create a competing independent seed path.**

### Existing SocialBudget

The GitHub snapshot has one central budget for:

- global cooldown
- per-Sim cooldown
- type cooldown
- rolling message budget
- recent player speech
- active conversation
- combat gating
- one social-moment winner
- lexical semantic duplicate suppression
- message duplicate suppression

Keep it as the cadence/admission layer.

### Existing recent idle/camp seed work

The GitHub snapshot has `CampTopicSeeds` and `BuildSpontaneousSituation`.

It randomly chooses a generic prompt hint and may add one random verified outing fact.

Examples include:

- zone preference
- class/role opinion
- future adventure preference
- waiting on mana/cooldowns/recovery
- pace preference
- gear aesthetics
- enemy design
- ordinary downtime subjects
- light teasing

This is useful work to **preserve conceptually where useful**, but it is not yet a ranked/provenance-aware seed selector.

Do not simply delete newer local seeding work. Compare it with the proposed design and refactor it into typed candidate producers if appropriate.

### Existing outing/memory system

The GitHub snapshot already has:

- structured current outing telemetry
- structured recent encounters
- verified outing summaries
- recent events
- important memories
- Sim relationship/social-history counters
- coarse conversation-topic summaries

Use these before inventing another persistent episode store.

## First implementation target

Do not attempt the entire architecture in one pass.

### Phase 1 — Instrument current idle/camp selection

First explain the current behavior with diagnostics.

Add or extend developer diagnostics so one ambient opportunity can report:

```text
context=Camp
quiet=...
current generic seed key=...
verified outing fact attached=yes/no
social budget accepted/suppressed
expression route=template/llm/off
message emitted=yes/no
```

Prefer `/dsseeds recent` or an equivalent local command.

Do not dump giant prompts.

Run deterministic tests and compile before continuing.

### Phase 2 — Smallest useful behavior change

Implement typed topic keys + fatigue + explicit silence for the current idle/camp path only.

Minimum shape:

```text
AmbientSeedDefinition
- TopicKey
- PromptHint
- CooldownGroup
- base score/category
```

Examples:

```text
zone_preference
class_opinion
future_activity
recovery
pace_preference
gear_aesthetics
enemy_design
ordinary_downtime
other_sim_preference
light_tease
idle_waiting
```

Then:

1. add a bounded `TopicFatigueTracker`
2. create an explicit SILENCE score
3. treat the quiet timer as "evaluate now", not "someone must speak"
4. rank candidate(s) before calling Ollama
5. record `TopicKey` only after an actual line emits
6. make `idle_waiting` extremely weak
7. preserve SocialBudget as the outer cadence gate

If no candidate beats silence:

```text
do nothing
```

Do not call Ollama merely to have it return `NO_MESSAGE` for an obviously empty moment.

### Phase 3 — Generalize existing event candidates

Once Phase 2 tests pass, adapt/refactor existing `SocialEventCandidate` into the generalized `ConversationSeed` model from the schema document.

Do not cut over all producers at once.

Recommended order:

```text
existing verified events
current verified state
existing memory summaries/events
player-topic summaries
native social state only after API verification
world/cached lore last
```

At every step preserve parity tests for existing event behavior.

## Architecture target

Prefer one generalized director:

```text
ConversationSeedDirector
    collect candidates
    validate provenance/knowledge scope
    score seed/speaker pairs
    apply fatigue
    compare against silence
    send one winner to SocialBudget
    open one seed-bound thread
```

`EventConversationDirector` may evolve into this class or become an event producer.

Do not keep:

```text
old EventConversationDirector independently initiating
+
new SeedDirector independently initiating
```

That would recreate the exact double-fire problem this work is meant to solve.

## Topic fatigue rules

Use producer-assigned semantic keys, not generated-sentence similarity.

At minimum track:

```text
TopicKey
CooldownGroup
LastUsedUtc
RecentUseCount
LastSpeaker
LastConversationId
```

Initial penalties can follow the design document.

Semantic equivalents must share one key:

```text
"Nothing is happening."
"Not much going on."
"I'm waiting."
"We're just standing here."
    => idle_waiting
```

Existing `SocialBudget.NormalizeMessage` remains a final output-level duplicate check.

## Knowledge scope

Implement/retain code-owned scope such as:

```text
SELF
DIRECT_PARTICIPANT
PARTY_WITNESSED
NEARBY_WITNESSED
GUILD_PUBLIC
WORLD_PUBLIC
PLAYER_TOLD
SHARED_MEMORY
```

Important rules:

- capture party witnesses at event time
- joining later does not grant witness knowledge
- same zone does not automatically mean nearby witness
- private whisper topics stay with the target Sim
- party chat topics are available only to appropriate recipients/present scope
- HEARD conversation is not game-world evidence
- a memory seed must not widen beyond the Sim(s) whose stored provenance supports it

## Native Erenshor integration

Public documentation suggests Erenshor itself knows friend status, current zone/activity, grouping, roles, guilds, mana behavior, and native history.

Do not guess field names from the wiki.

Search local assemblies/source/references first.

If a stable existing API is found, isolate it behind a small compatibility reader and fail closed.

Specific local-verification targets:

```text
friend status / binding
friendliness/opinion
native grouping history
native item-received history
current activity
exact Manage Roles assignment
current/max mana
guild-public events
nearby/presence semantics
```

### Important mana caveat

The researched GitHub `SimSnapshot` has HP but no mana fields.

Do not ship a live "low healer mana" producer by parsing text or inventing a field.

It is acceptable to implement deterministic selector tests with an abstract/test seed input first while leaving the live producer disabled/TODO until mana and role state are proven.

## Personality

Reuse verified current personality/state only as small modifiers.

Allowed:

```text
Rival slightly favors verified duel/competitive seed
GearChase/Greed slightly favors already-valid loot seed
Patience can slightly modify recovery/setback affinity
relationship familiarity can slightly modify willingness/reply choice
```

Not allowed:

```text
Rival => fabricate a duel
high familiarity => fabricate a shared adventure
unknown PersonalityType enum => invent "quiet" psychology
```

If an explicit native sociability trait exists locally, document the source before using it.

## Conversation chains

Reuse/generalize the existing verified event thread.

One selected seed should own the autonomous thread:

```text
seed
-> opening speaker
-> 0–2 relevant replies
-> close
```

Every reply gets:

```text
original SeedId
original TopicKey
original bounded fact
original provenance/scope
current eligible speakers
HEARD prior lines
```

Do not run the ambient seed selector independently for each reply.

Stop on:

```text
NO_MESSAGE
hard cap
player interruption
seed expiry
speaker no longer eligible
stale party/conversation generation
```

## Memory

Do not write seeds to memory.

Keep existing memory policy authoritative.

Use existing:

```text
RecentEvents
ImportantMemories
OutingSummaries
ConversationSummaries
relationship records
structured encounters
```

as seed inputs.

Only consider a new structured persisted outing episode after diagnostics show a concrete retrieval/provenance failure that current structures cannot solve.

## Required deterministic/regression tests

Add all of these to the existing no-Ollama suite:

1. nothing happened -> silence wins
2. idle topic recently used -> strongly suppressed
3. new duel -> duel beats idle/silence
4. repeated same duel -> fatigue increases
5. low healer mana at camp -> seed available when authoritative test input exists
6. mana recovery -> low-mana seed invalid/expires
7. old shared outing -> can win in Relax
8. absent Sim -> cannot claim witnessing event
9. participant -> may reference event
10. third Sim -> may reference guild/world public fact only when scope permits
11. replies retain TopicKey
12. chain terminates
13. quiet/speech-averse modifier raises silence/wins less often
14. social modifier does not generate facts
15. no valid seed -> no anecdote / no Ollama ambient request
16. recent player topic -> candidate for valid recipient
17. semantic duplicate variants -> same key/fatigue
18. Camp -> generic waiting strongly suppressed
19. Relax -> memory/social weights rise
20. event system + generalized selector -> one winner, no double fire
21. private whisper topic does not leak
22. player interruption closes thread
23. high-importance new event may overcome broad-group fatigue
24. expired seed cannot win
25. SocialBudget rejection does not count topic as emitted
26. topic usage recorded only after actual output
27. ambient evaluation performs no wiki/news network lookup
28. deterministic fixed-input ordering/diagnostics

Use an injectable clock for expiry/fatigue tests.

## Build/test procedure

Follow the repository's actual local instructions.

At minimum:

1. run the standalone deterministic suite
2. build against the locally installed/modded Erenshor assemblies
3. do not assume GitHub test stubs cover runtime reflection
4. inspect compiler warnings/errors
5. inspect the final diff
6. confirm no unrelated systems changed
7. confirm no generated memory/config/output files were accidentally added
8. confirm existing event/grounding tests still pass

If a test cannot run in the environment, state exactly which one and why.

## Diff review checklist

Before reporting completion, inspect the final diff for:

- duplicated director/budget logic
- new reflection guesses
- unbounded dictionaries/lists
- per-frame allocations
- background/main-thread shared state without locking
- accidental memory writes from seed selection
- private-topic knowledge leaks
- event double firing
- prompt growth
- large prompt logging
- changes to gameplay ownership
- changes to COOP authority
- regressions to Templates/LLM/Off routing

`SocialBudget` is shared across Unity/background activity in the researched snapshot; maintain the established locking/thread-ownership discipline.

## What to report back

Return:

### 1. Local-source comparison

```text
What differed from the GitHub research snapshot?
What newer seeding work already existed?
What was preserved?
What was replaced/refactored?
```

### 2. Behavior changes

Examples:

```text
idle timer now opens an evaluation but does not force speech
semantic topic key idle_waiting has fatigue
silence can beat all candidate seeds
event candidate path now feeds generalized selector
```

### 3. Tuning knobs

List only actual config/constant changes and their defaults.

### 4. Diagnostics

Show one example candidate/score trace.

### 5. Test results

Include:

```text
standalone deterministic regression summary
local build result
new seed tests
existing event/grounding tests
```

### 6. Remaining local verification

Especially:

```text
mana
exact role assignment
friend/native history
native activity
nearby witness scope
```

### 7. Final diff summary

List files changed and why.

## Implementation priority

Prefer this order:

```text
A. inspect local source
B. instrument current idle/seeding behavior
C. topic keys + fatigue + silence
D. prove tests/build
E. generalize event candidates
F. unify other producers
G. generalize chains
H. Camp/Relax/Expedition weighting
I. structured outing memory only if justified
```

Do not jump to H or I while B–D remain unproven.

## Completion definition

The first useful milestone is complete when:

```text
- a long quiet period can legitimately result in no request/no line
- repeated idle/waiting ideas are recognized as one topic
- a new verified duel or other meaningful event reliably outranks idle
- diagnostics explain the winner
- no existing event, grounding, memory, COOP, or expression-mode tests regress
```

Do not push or publish changes unless the user explicitly asks.
