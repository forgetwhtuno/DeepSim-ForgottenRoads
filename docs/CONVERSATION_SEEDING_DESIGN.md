# Deep Sims Ambient Conversation & Conversation Seeding Design

Status: architecture draft for local implementation  
Research snapshot: GitHub `forgetwhtuno/DeepSim-erenshor`, `main` at `5449401570e923017f820d1585305247d97cde28` on 2026-08-10.  
Important: local source may be ahead of this snapshot. Inspect local source before implementing.

## 1. Problem statement

Deep Sims can produce dialogue that is factually grounded but socially unnatural because the current idle path asks for speech after enough quiet time rather than first asking whether any subject is worth discussing.

Typical failure family:

- "Nothing is happening."
- "Not much going on."
- "I'm waiting."
- "We're just standing here."
- repeated remarks about mana/recovery/waiting that are technically safe but have no social reason to recur.

The problem is not primarily wording duplication. It is **topic-selection duplication**.

The desired architecture is:

```text
ERENSHOR / VERIFIED DEEP SIMS STATE
    what is true
            |
            v
DETERMINISTIC SEED PRODUCERS
    what could be socially relevant
            |
            v
SEED SELECTOR + TOPIC FATIGUE + SILENCE
    what is worth discussing now
            |
            v
CONVERSATION THREAD CONTEXT
    who may speak and what this conversation is about
            |
            v
EXPRESSION ROUTER
    Templates or LLM
            |
            v
GROUNDING / OUTPUT SAFETY
```

The LLM should normally receive a selected subject and bounded facts. It should not be responsible for inventing the subject merely because a quiet timer fired.

## 2. Goals

1. Make autonomous speech feel motivated by a particular fact, event, memory, relationship, or player topic.
2. Make silence an explicit, healthy outcome.
3. Suppress semantic/topic repetition, not only duplicate strings.
4. Preserve the existing verified-event, social-budget, grounding, expression-mode, and short-thread architecture.
5. Keep knowledge scope explicit so Sims do not learn private or unwitnessed facts.
6. Keep seed selection deterministic, bounded, diagnosable, and cheap.
7. Let personality influence willingness/topic affinity without creating facts.
8. Support Camp now and leave clean extension points for Relax and Expedition.
9. Prefer native Erenshor state where it is safely available rather than reconstructing a parallel truth.
10. Keep deterministic regression coverage independent of Ollama.

## 3. Non-goals

- No LLM control of movement, combat, pulling, healing, travel, grouping, loot, or quests.
- No embedding/vector database in v1.
- No LLM call to classify candidate topics in v1.
- No ambient web/wiki lookup merely to manufacture a conversation subject.
- No permanent memory for every seed.
- No full transcript persistence as episodic memory.
- No speculative reflection against undocumented Erenshor fields.
- No generalized multi-agent simulation or endless Sim-to-Sim conversations.
- No attempt to make Sims speak continuously.

## 4. Research classification

### CURRENT DEEP SIMS BEHAVIOR

The current 0.7.x architecture already has several strong pieces that should be retained.

#### 4.1 Verified event path

`SocialDirector` observes/promotes a bounded set of verified events and completed encounters.

Existing path:

```text
GameEventHooks / SessionTelemetry / SocialDirector
    -> SocialEventCandidate
    -> EventConversationDirector
    -> SocialBudget
    -> QueueVerifiedEventConversation
    -> Templates or LLM
    -> Grounding / output gate
```

`SocialEventCandidate` already carries useful seed-like fields:

- Type
- ObservedUtc
- InvolvedNames
- EligibleSpeakerNames
- VerifiedEntities
- Trust
- Importance
- Novelty
- CooldownCategory
- VerifiedContext
- BaseChance

`EventConversationDirector` already provides:

- a short candidate lifetime
- duplicate fingerprints
- priority competition
- current-party speaker eligibility
- per-Sim cooldown awareness
- a probability gate
- one central `SocialBudget` admission call
- accepted/suppressed diagnostics
- verified 1–3 line event conversation chains

This is the best foundation for generalized seeding.

#### 4.2 Central social admission

`SocialBudget` already owns global autonomous cadence and one-winner behavior:

- global cooldown
- per-speaker cooldown
- event-type cooldown
- rolling message budget
- player-recently-spoke suppression
- live-conversation suppression
- combat gating
- one claimed social moment
- exact-ish semantic key duplicate suppression
- emitted-message duplicate suppression

Do not replace this with another autonomous budget.

The current `NormalizeSemantic` is lexical normalization, not true concept-level topic fatigue. It is still useful as a final duplicate barrier.

#### 4.3 Existing short conversation chains

`BuildVerifiedEventThread` already preserves the original verified event across replies, marks generated lines as HEARD rather than factual evidence, and permits `NO_MESSAGE`.

This already solves much of the desired chain behavior for event conversations.

#### 4.4 Current memory/social history

`MemoryStore` already persists:

- recent verified events
- important memories
- outing summary strings
- recent group chat as HEARD context
- bounded direct conversation history
- coarse conversation-topic summaries
- Sim-to-player familiarity/rapport/rivalry
- Sim-to-Sim shared outings/minutes/conversation threads

`SessionTelemetry` already maintains a structured current outing and a bounded structured encounter history, then produces verified outing summaries when an outing ends.

Therefore a useful form of outing/episodic memory already exists. A new structured persisted `OutingEpisode` should be deferred until the existing summary + encounter data proves inadequate for seed retrieval.

#### 4.5 Current personality use

Deep Sims already reads Erenshor Sim fields such as:

- personality code/raw personality
- Greed
- Patience
- GearChase
- Rival
- typing/style fields
- class
- guild
- HP/dead state

Speaker scoring already applies small topic-sensitive nudges for loot, mana/healing language, setbacks/waiting, rivalry, and relationship state.

These should remain small modifiers. They must not become factual evidence.

#### 4.6 Current idle/camp path

`SocialDirector.EvaluateIdlePressure` is time-pressure based.

After the minimum quiet time, the chance of an idle opportunity rises. At the configured maximum quiet time, the opportunity chance reaches 100%.

Normal idle currently produces:

```text
type = idle
situation = "Quiet moment with the current visible party."
```

Camp idle produces a camp situation string.

The current implementation then builds a spontaneous situation using a random entry from `CampTopicSeeds`, optionally accompanied by one random verified outing fact.

Examples of those current prompt seeds include:

- zone preference
- class/role/spell opinion
- what adventure sounds good next
- waiting on mana/cooldowns/recovery
- careful vs fast pace
- gear aesthetics
- enemy design
- food/weather/music
- asking another Sim a preference
- a quiet-moment joke

This is useful recent work, but it is a **prompt-hint seeding layer**, not yet a deterministic candidate selection system.

It currently lacks:

- typed seed provenance
- a canonical semantic `TopicKey`
- ranking across facts/memories/events
- topic fatigue
- knowledge scope
- expiry per seed
- an explicit silence candidate
- diagnostics explaining why one subject beat another

Because the quiet timer decides that an opportunity exists before the topic has been evaluated, the LLM can still be asked to manufacture a safe subject from generic downtime hints.

### NATIVE ERENSHOR BEHAVIOR

Public Erenshor documentation indicates that the base game already owns significant social state:

- SimPlayers may be solo or already grouped.
- Group Builder exposes Sim name, class, current zone, and friend status.
- `/whisper doing` exposes current activity.
- `/whisper where` exposes current zone/location context.
- native greetings may reference past adventures, items received, grouping history, and elapsed time since playing together.
- SimPlayers remember player name, past adventures, items received, and grouping history.
- Sim opinion/friendliness affects responses.
- guild membership and guild social behavior exist.
- Role Manager owns roles such as Main Tank, Main Assist, Healing/Mana, Crowd Control, and Puller.
- group mana and pull-threshold behavior are native concepts.

Deep Sims already consumes some native state (party, active Sim roster, class, current zone, guild, personality/style, HP), but the inspected GitHub snapshot does not demonstrate safe consumption of the native friend list, native friendliness/history, exact Manage Roles assignment, or a general native current-activity record.

### PROPOSED CHANGE

Treat safely exposed native social state as another deterministic seed producer or relevance input. Do not mirror it into a second permanent Deep Sims truth unless Deep Sims needs a sidecar cache for a specific reason.

### LOCAL VERIFICATION NEEDED

The wiki describes game behavior, not a stable C# API contract.

Before consuming any of the following, inspect the local Erenshor assemblies/source references and existing mod code:

- friend-list binding/status
- friendliness/opinion
- native remembered grouping/item history
- current Sim activity
- exact Manage Roles assignment
- current mana / maximum mana
- nearby-witness semantics
- public guild event visibility

Fail closed when the source field/API cannot be established.

## 5. Why repetitive idle conversation happens

The current system has good cadence suppression but poor **subject competition**.

The important distinction is:

```text
CURRENT:
quiet long enough
    -> decide to attempt chatter
    -> choose a generic/random prompt seed
    -> LLM may say NO_MESSAGE

DESIRED:
quiet moment opens an evaluation window
    -> build grounded candidate seeds
    -> score seeds against SILENCE
    -> only then decide whether anyone speaks
```

Current repetition mechanisms are mostly:

- cooldown by event type
- lexical semantic-key duplicate suppression
- normalized emitted-message duplicate suppression
- short recent conversation context

Those can prevent exact repetitions but they do not know that all of these are the same concept:

```text
Nothing is happening.
Not much going on.
I'm waiting.
We're just standing here.
```

All should share a canonical topic such as:

```text
idle_waiting
```

The newer generic seed:

```text
make a light observation about waiting on mana, cooldowns, or recovery
```

can also make waiting/recovery a disproportionately available safe subject even when no current mana fact is exposed.

## 6. Recommended architecture

### 6.1 Do not build a parallel seed director

Generalize the existing event-conversation architecture.

Recommended shape:

```text
                     +---------------------------+
Game/telemetry ----> | CurrentStateSeedProducer  |
Events ------------> | EventSeedProducer         |
Memory ------------> | MemorySeedProducer        |
Relationships -----> | SocialHistorySeedProducer |
Player chat --------> | PlayerTopicSeedProducer   |
Native state -------> | NativeSocialSeedProducer  |
Cached lore --------> | WorldSeedProducer         |
                     +-------------+-------------+
                                   |
                                   v
                         ConversationSeedDirector
                         - collect bounded seeds
                         - apply knowledge scope
                         - discard expired/unsafe
                         - score seed/speaker pairs
                         - apply TopicFatigueTracker
                         - compare with SILENCE
                                   |
                      selected seed or silence
                                   |
                    +--------------+--------------+
                    |                             |
                  silence                     seed chosen
                                                  |
                                                  v
                                             SocialBudget
                                             admission
                                                  |
                                                  v
                                      SeedBoundConversationThread
                                                  |
                                                  v
                                      Templates / LLM expression
                                                  |
                                                  v
                                      GroundingGuard / output gate
```

`EventConversationDirector` should either:

1. be renamed/evolved into `ConversationSeedDirector`, or
2. become a specialized event producer feeding a new small selector.

Option 1 is preferable if local code has not already created a seed selector. It preserves one director and avoids two competing autonomous systems.

### 6.2 Preserve SocialBudget responsibilities

`ConversationSeedDirector` answers:

> What subject, if any, is socially worth discussing?

`SocialBudget` answers:

> Is this autonomous social moment allowed to emit now?

Do not merge these responsibilities.

A high-scoring seed can still be suppressed by:

- recent player speech
- active thread
- combat policy
- global cooldown
- rolling budget
- per-Sim cooldown
- expression mode Off

Likewise, a budget opening must not force a seed to exist.

## 7. Seed model

See `CONVERSATION_SEED_SCHEMA.md` for the full proposed schema.

The minimum model should include:

- `SeedId`
- `TopicKey`
- `Category`
- `Fact`
- `Source`
- `Trust`
- `KnowledgeScope`
- `Participants`
- `EligibleSpeakers`
- `Subject`
- `CreatedUtc`
- `Importance`
- `Novelty`
- `MemoryEligible`
- `ExpiresUtc`
- `CooldownGroup`
- `GroundingRefs`

The essential distinction is:

```text
SeedId     = this particular fact/event
TopicKey   = the semantic subject for fatigue
```

Example:

```text
SeedId:   duel:8472
TopicKey: duel:fiora:player
```

A later duel may have a new `SeedId` but the same or related `TopicKey`/`CooldownGroup`, allowing novelty to overcome some fatigue without forgetting that the subject has been discussed repeatedly.

## 8. Candidate producers

Keep each producer deterministic, bounded, and free of LLM calls.

### 8.1 Immediate current-state producer

Examples only when authoritative state exists:

- recovery threshold transition
- low mana threshold transition
- combat just ended
- current role/state change
- party join/leave
- travel departure/arrival
- death/revive
- level-up
- verified unusual state

Rules:

- Prefer edge-triggered changes over polling a condition every evaluation.
- Current-state seeds should expire quickly.
- Continuous state should not create a fresh seed every tick.
- "Nothing is happening" is not a useful current-state fact.

Current implementation note: `SimSnapshot` in the inspected GitHub snapshot has HP but no mana fields. Low-mana live seeds require local API verification before implementation.

### 8.2 Recent verified-event producer

Adapt the existing `SocialEventCandidate` path.

Sources include:

- completed encounter
- death/revive
- level-up
- quest completion
- friendly duel
- verified party change
- future verified unusual loot

Preserve:

- current participant capture
- eligible speaker checks
- short expiry
- significance/importance
- one-winner priority behavior

Do not promote an event merely because a log string sounds interesting if attribution/importance cannot be verified.

### 8.3 Shared-memory producer

Read existing `MemoryStore` structures:

- `RecentEvents`
- `ImportantMemories`
- `OutingSummaries`
- verified duel memory
- verified zone history

Prefer compact existing memory first.

A memory seed must carry:

- which Sim owns/remembers it
- who participated, if known
- source memory key/string/event
- age
- topic key
- whether another active Sim is allowed to reference it

If participant scope cannot be reconstructed from an old string summary, restrict the seed to the memory owner rather than widening knowledge.

### 8.4 Relationship/social-history producer

Use familiarity/rapport/rivalry and shared Sim history as **relevance and tone**, not fact.

Safe examples:

- a familiar Sim may be more willing to initiate
- a rival Sim may weight a verified duel seed higher
- a pair with many shared conversation threads may be slightly more likely to respond to each other

Unsafe:

```text
high familiarity -> fabricate a past rescue
high rivalry -> assume a duel occurred
high rapport -> call the player a best friend
```

### 8.5 Native Erenshor social-state producer

Only after local API verification.

Potential inputs:

- native friend status
- current activity
- already grouped/adventuring state
- current zone
- exact assigned role
- guild membership/public guild facts
- native remembered grouping/item history

Do not import native information into permanent Deep Sims memory merely because it can be read. Prefer a live `NativeSocialSnapshot`.

### 8.6 Player-topic producer

Player topics are conversational knowledge, not automatically world facts.

Party chat:

```text
KnowledgeScope = PlayerTold
EligibleSpeakers = Sims who were actually in that party-chat scope
```

Private whisper:

```text
KnowledgeScope = PlayerTold
EligibleSpeakers = target Sim only
```

Use the existing deterministic thread-topic classifier as a starting point and extend it into canonical keys such as:

- `player_topic:loot`
- `player_topic:zone`
- `player_topic:guild`
- `player_topic:duel`
- `player_topic:class`
- `player_topic:future_activity`

A later Sim can raise the **subject** without treating the player's claim as verified fact.

### 8.7 World/lore producer

For v1:

- current verified zone can be a weak topic
- cached already-retrieved wiki/news context may be eligible if its scope permits it
- do not perform a network lookup just because an ambient evaluation occurred

Lore should usually lose to current events, shared memories, and silence.

### 8.8 Idle producer

Prefer no explicit idle fact.

If an idle seed is retained for Lively/manual testing:

```text
TopicKey = idle_waiting
Category = Idle
Base score = strongly negative
MemoryEligible = false
```

It should almost always lose to silence after recent use.

## 9. Knowledge scope and provenance

Suggested scopes:

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

Eligibility is code-owned.

### SELF

Only the Sim whose state/fact this is about may claim first-person knowledge when appropriate.

### DIRECT_PARTICIPANT

Only captured participants.

### PARTY_WITNESSED

Only party members captured when the event occurred.

Do not define this as "currently in party"; joining afterward must not confer witness knowledge.

### NEARBY_WITNESSED

Use only if actual presence/proximity can be established. Same zone alone is insufficient.

### GUILD_PUBLIC

Only for facts the game actually exposes as guild-public.

### WORLD_PUBLIC

For truly public game-world/system facts.

### PLAYER_TOLD

Only recipients of the player communication.

### SHARED_MEMORY

Only Sims whose persisted verified memory contains or safely references the fact.

Every seed should carry provenance sufficient to answer:

```text
What is the fact?
Where did it come from?
Who may know it?
How old is it?
Is it still valid?
Can it become memory?
What exact grounding source should be supplied to expression?
```

## 10. Memory vs seed

A seed is an ephemeral selection object. It is not a memory write.

Examples:

| Subject | Seed | Permanent memory? |
|---|---:|---:|
| low mana | yes | no |
| sitting at camp | context only | no |
| level-up | yes | existing memory policy may persist |
| completed meaningful fight | yes | existing encounter/outing policy |
| friendly duel | yes | existing verified social history |
| random generic preference prompt | maybe | no |
| player chat topic | yes as HEARD topic | not factual memory |
| rare boss kill | yes | potentially outing/important memory if verified |

Memory persistence should remain owned by `SessionTelemetry` / `MemoryStore` policy.

The selector must never persist a fact merely because it selected it.

## 11. Outing / episodic memory

### Current state

Deep Sims already has:

- live structured outing telemetry
- current/last/recent structured encounters
- participant overlap
- verified outing summary generation
- up to several persisted outing summary strings
- important memories for longer/notable outings

This is enough to begin memory seeding.

### Proposed v1

Generate memory seeds from the existing summaries and structured current-session encounter records.

### Deferred structured episode

Only if retrieval quality proves inadequate, add a compact persisted object such as:

```text
OutingEpisode
- EpisodeId
- StartedUtc
- EndedUtc
- Zones
- ParticipantKeys
- EncounterIds / compact encounter facts
- VerifiedDeaths
- VerifiedRetreats
- VerifiedDuels
- VerifiedNotableLoot
```

Do not generate prose with the LLM for storage.

Do not store exact counts that telemetry cannot verify.

Do not duplicate data already available safely in Erenshor native memory unless Deep Sims needs it for its own grounding.

## 12. Semantic topic fatigue

### 12.1 Do not infer semantic fatigue from generated wording

The cleanest solution is to attach the semantic key **before generation**.

When a selected seed emits a message, record the seed's `TopicKey`.

Examples:

```text
idle_waiting
zone:azure
party_member:fiora
duel:fiora:player
loot:aetheria
loot
mana:cyndara
camp_recovery
recent_wipe
next_destination
guild:lantern
player_topic:loot
outing:2026-08-09:azure
```

For player/vanilla lines, use a deterministic classifier to obtain a coarse topic key.

### 12.2 Topic usage record

Minimum transient record:

```text
TopicUsage
- TopicKey
- CooldownGroup
- LastUsedUtc
- RecentUseCount
- LastSpeaker
- LastConversationId
```

Keep only a short bounded window.

### 12.3 Suggested penalties

Initial tuning values, deliberately simple:

```text
same TopicKey in active thread               exclude / -100
same TopicKey used < 90s ago                 -55
same TopicKey used < 5m ago                  -35
same CooldownGroup used < 2m ago             -20
same speaker + same TopicKey < 10m            -12
each additional party-wide use in 10m         -8 (cap -24)
current conversation already covered subject  -60
```

A new high-importance event may overcome group fatigue.

Example:

- duel #1 discussed: `duel:fiora:player` becomes fatigued
- duel #2 happens ten minutes later: new `SeedId`, fresh recency/novelty and high importance can still win
- no new duel: a memory seed about the same duel should usually lose

### 12.4 Keep existing message duplicate checks

`SocialBudget.NormalizeSemantic` / `NormalizeMessage` remain a final output-level safety net.

They solve a different problem than topic fatigue.

## 13. Deterministic scoring

Keep the formula additive and inspectable.

Evaluate bounded `(seed, speaker)` pairs. The active Deep Sim party is small, so this remains cheap.

Suggested v1:

```text
score =
    category_base
  + importance_bonus
  + recency_bonus
  + knowledge_relevance_bonus
  + shared_relevance_bonus
  + novelty_bonus
  + personality_affinity
  - topic_fatigue
  - speaker_repeat_penalty
  - grounding_risk_penalty
```

### 13.1 Category base

```text
ImmediateCurrentState     +24
RecentVerifiedEvent       +28
SharedMemory              +18
PlayerTopic               +14
NativeSocial              +14
RelationshipSocialHistory +10
WorldLore                  +8
Idle                      -20
```

### 13.2 Bonuses

```text
Importance:          0..25  = Importance / 4
Recency:             0..15
Knowledge relevance: 0..12  (self/participant > witness > public)
Shared relevance:    0..10
Novelty:             0..10
Personality affinity:-4..+6
```

Personality is intentionally weak.

### 13.3 Grounding risk

Prefer categorical penalties:

```text
verified / direct source       0
allowed but conversational    10
source scope incomplete       30
unsupported / ambiguous      exclude
```

Do not let a large importance score rescue an unsupported source.

### 13.4 Explicit silence candidate

Silence has a real score.

Initial context values:

```text
Normal      42
Camp        38
Relax       30
Expedition  44
Combat      55 for ambient/non-urgent subjects
```

These are tuning defaults, not sacred constants.

A quiet personality can raise the effective silence threshold slightly for that speaker. A more social personality can lower it slightly. Keep this influence small.

### 13.5 Example diagnostic

```text
mode=Camp
speaker candidates=Fiora, Phanty, Dancer

duel:fiora:player
  speaker=Fiora
  score=78
  +28 event
  +21 importance
  +15 recency
  +12 participant
  +10 novelty
  -8 fatigue

memory:azure_outing
  speaker=Phanty
  score=45

zone:azure
  speaker=Phanty
  score=31

idle_waiting
  speaker=Dancer
  score=-52

SILENCE
  score=38

selected=duel:fiora:player
```

## 14. Speaker selection and personality

The current `SelectBestSpeaker` logic is valuable and can be reused/refactored into the seed score.

Personality may influence:

- whether the best seed/speaker pair beats silence
- topic affinity
- reply likelihood
- expression style

Personality must not influence:

- whether a fact exists
- who witnessed an event
- whether a duel happened
- whether a Sim owns loot
- whether a relationship is friendship
- what the assigned game role is

Avoid inventing a "quiet" meaning from undocumented personality enum values.

If the base game exposes an explicit sociability/talkativeness trait, use it only after local verification.

## 15. Conversation chains

### 15.1 Reuse the existing event-thread design

The existing verified event chain already has the right principle:

```text
one verified subject
  -> opening line
  -> 0–2 relevant replies
  -> close
```

Generalize it to a seed-bound thread.

Suggested context:

```text
SeedConversationContext
- ConversationId
- SeedId
- TopicKey
- Fact
- Trust
- KnowledgeScope
- GroundingRefs
- EligibleSpeakers
- OpenedUtc
- ExpiresUtc
- MaxLines
- LinesEmitted
```

### 15.2 Thread rules

- Total autonomous lines: normally 1–3.
- Normal/Camp: prefer 1–2; 3 only when natural.
- Relax may allow up to 3.
- Every reply receives the original seed fact/provenance.
- Generated prior lines remain HEARD context.
- Reply speaker must still be current and eligible.
- Player speech invalidates or takes ownership of the thread.
- No unrelated seed selection while a thread is active.
- `NO_MESSAGE` closes naturally.
- After close, record TopicKey usage/cooldown.
- Never allow a reply to create a new factual branch.

### 15.3 Do not independently reseed each reply

Bad:

```text
Fiora: duel
Phanty: Azure
Dancer: waiting
```

Good:

```text
seed=duel:fiora:player
Fiora: ...
Phanty: reply about the duel
Fiora: optional final reply
close
```

## 16. Camp integration

Camp already exists in `SocialDirector` and should become a **context modifier**, not a reason to talk about waiting.

Camp should:

- lower silence modestly compared with Normal
- boost recovery/recent-encounter/shared-memory seeds
- suppress `idle_waiting`
- permit a recent pull/fight to remain discussable
- keep chains short
- avoid network lore lookup

Potential seeds, only when verified:

- `camp_recovery`
- `mana:<sim>`
- `recent_encounter:<id>`
- `recent_wipe`
- `repeated_enemy:<enemy>`
- `loot:<item>`
- `role:<sim>` if exact role is known
- `recent_add`
- `memory:<outing>`

The fact that the player is sitting/meditating is context. It normally should not itself be the topic.

### Current limitation

The inspected GitHub `SimSnapshot` has HP fields but no mana fields.

Tests for low healer mana can be written against the abstract seed selector now, but the live `ManaSeedProducer` must remain disabled/TODO until local source establishes an authoritative mana signal and healer/role assignment.

## 17. Relax integration

Relax is proposed future state.

Effects:

```text
SharedMemory base       +10
Relationship base       +8
PlayerTopic base        +6
World/cached-lore base  +4
urgent gameplay topics  -8 unless actually important
silence score           ~30
max thread lines        up to 3
idle_waiting            heavily suppressed
```

Relax may allow older memories to enter the candidate set, but memory provenance remains mandatory.

It may allow a Sim to initiate an old player topic if that Sim actually received that topic previously.

It must not make private conversations global.

## 18. Expedition integration

Expedition state remains game/companion-mod owned.

Deep Sims only reacts to verified state:

- departure
- verified leader
- verified destination
- combat interruption
- regroup
- resume
- zone transition
- arrival
- return
- prior verified expedition memory

The seed system must never select the destination, leader, route, or combat action.

If no companion/native expedition API exists, no expedition seed should be produced.

## 19. Grounding

Preserve the existing contract:

```text
CODE retrieves/derives facts.
SELECTOR chooses a fact/topic.
LLM expresses supplied facts.
GroundingGuard verifies the final line.
```

A new seed prompt should not say:

```text
"Find something interesting to talk about."
```

It should say approximately:

```text
SELECTED SOCIAL SUBJECT:
Topic: duel:fiora:player
Verified fact: <bounded fact>
Knowledge scope: direct participant / witnessed
Allowed entities: <bounded>
Stay on this subject. Do not add a new event or history.
Return one short line or NO_MESSAGE.
```

`GroundingGuard` remains the final safety boundary. The seed system should reduce guard failures by giving the model less room to invent.

No valid seed:

```text
SILENCE
```

Direct player question with no factual source:

```text
safe uncertainty / direct conversational response
```

Those are separate modes.

## 20. Diagnostics

Add bounded developer diagnostics without dumping full prompts.

Suggested command:

```text
/dsseeds status
/dsseeds recent
/dsseeds test
```

For backward compatibility:

```text
/dsevents recent
```

can continue to show event-only history or alias/filter generalized seed diagnostics.

Suggested record:

```text
[DeepSims Seeds]
opportunity=184
mode=Camp
utc=...
social_budget=eligible

candidates:
- duel:fiora:player      speaker=Fiora  score=78
- memory:azure:outing42 speaker=Phanty score=45
- zone:azure             speaker=Phanty score=31
- idle_waiting           speaker=Dancer score=-52
- SILENCE                               score=38

selected:
duel:fiora:player / Fiora

reasons:
recent event +15
participant +12
importance +21
novelty +10
topic fatigue -8
```

Keep only perhaps the last 16–32 decisions.

Log why candidates were excluded:

- expired
- unsupported provenance
- no eligible speaker
- private scope
- already covered in thread
- topic fatigue
- below silence
- social budget suppressed
- stale party
- local capability unavailable

Do not print giant prompt bodies in ordinary logs.

## 21. Configuration

Avoid a large tuning surface in v1.

Recommended first config:

```text
ConversationSeeding/Enabled = true
ConversationSeeding/Diagnostics = false
ConversationSeeding/FatigueSeconds = 300
ConversationSeeding/RecentTopicWindowMinutes = 10
ConversationSeeding/SilenceNormal = 42
ConversationSeeding/SilenceCamp = 38
```

Continue to respect existing:

- Social Director Enabled
- EventChatter
- IdleChatter
- social activity preset
- global/per-Sim/event cooldowns
- rolling message budget
- expression mode
- conversation-thread settings
- combat pause policy

Do not create duplicate knobs for those.

Later tuning may expose category weights only if diagnostics demonstrate a real need.

## 22. Deterministic tests

Add a no-Ollama seed-selector suite and wire it into the existing standalone regression runner.

Required scenarios:

1. Nothing has happened -> silence commonly/deterministically wins.
2. `idle_waiting` recently used -> idle seed is strongly suppressed.
3. New verified duel -> duel seed beats idle and silence.
4. Same duel discussed repeatedly -> fatigue increases.
5. Low healer mana at camp -> relevant camp seed is available **only when authoritative mana/role input is supplied**.
6. Low-mana seed expires after recovery.
7. Old shared outing can appear during Relax.
8. Sim not present for event cannot claim witnessing it.
9. Direct participant can reference event.
10. Third Sim can reference guild/world-public fact only when scope allows.
11. Replies stay on original TopicKey.
12. Conversation chain terminates at hard cap / `NO_MESSAGE`.
13. Quiet/speech-averse personality modifier causes fewer winning speaker pairs or a higher effective silence threshold.
14. Social personality does not create additional facts/candidates.
15. No valid seed -> no fabricated anecdote; selector returns silence.
16. Recent player topic may become a candidate for actual recipients.
17. Semantic duplicates share one `TopicKey` and receive the same fatigue.
18. Camp strongly suppresses generic waiting commentary.
19. Relax increases memory/social seed scores.
20. Event significance and generalized selector do not double-fire; one social moment has one winner.

Additional recommended scenarios:

21. A private whisper topic is not visible to another Sim.
22. A party-chat topic is visible only to Sims present in that scope.
23. A new high-importance event can overcome cooldown-group fatigue.
24. An expired current-state seed cannot win.
25. SocialBudget suppression does not mark a topic as emitted.
26. Topic usage is recorded only after an actual emitted message.
27. Player interruption closes an autonomous seed thread.
28. Native-social producer disabled/unavailable fails closed.
29. Ambient evaluation performs no wiki/news network request.
30. Candidate ordering/diagnostics are deterministic for a fixed clock/input.

Prefer an injected clock for fatigue/expiry tests.

## 23. Migration plan

### Phase 1 — Instrument the current path

Behavior change: none or minimal.

- Add diagnostics around `EvaluateIdlePressure`.
- Record which current `CampTopicSeeds` entry was selected.
- Record whether one outing fact was attached.
- Record current SocialBudget suppression.
- Measure how often the selected idea is idle/wait/recovery/zone/generic preference.

Acceptance:

```text
/dsseeds recent
```

can explain why an idle opportunity generated or did not generate a line.

### Phase 2 — Topic keys, fatigue, and silence for idle/camp only

- Replace raw `CampTopicSeeds` strings with small typed definitions carrying `TopicKey`.
- Add `TopicFatigueTracker`.
- Treat the quiet timer as an evaluation opportunity, not a speech requirement.
- Build a small candidate list.
- Add explicit SILENCE.
- Remove/strongly demote `idle_waiting`.
- Record selected `TopicKey` only when a line actually emits.

Acceptance:

- repeated waiting variants map to one topic
- silence beats weak idle after no meaningful events
- no regression to event conversations

### Phase 3 — Unify event, state, memory, and player-topic production

- Evolve `SocialEventCandidate` into/generalize it as `ConversationSeed`.
- Preserve EventConversationDirector validation/priority logic as the event producer or generalized director.
- Add bounded producers for current state, existing memories, social history, and player topics.
- Keep one SocialBudget.

Acceptance:

- a new duel and a memory seed compete in one diagnostic list
- exactly one winner can reach SocialBudget
- no duplicate event + seed conversation for the same event

### Phase 4 — Generalize existing event thread into seed-bound chains

Much of this already exists for verified events.

- introduce `SeedConversationContext`
- reuse `BuildVerifiedEventThread` behavior
- keep the original selected seed/provenance across 0–2 replies
- prevent independent reseeding inside an active chain

Acceptance:

- all replies retain the same TopicKey
- chain stops by `NO_MESSAGE`, player interruption, expiry, or hard cap

### Phase 5 — Context modes

- Camp becomes weight modifier rather than idle-topic generator.
- Add Relax only when its mode/state is explicitly defined.
- Add Expedition only from verified companion/native state.

Acceptance:

- same candidate set scores differently by context without changing facts
- no context mode invents state

### Phase 6 — Structured outing episode only if justified

First evaluate seed quality from current `OutingSummaries`, `RecentEvents`, and structured encounter history.

Add a persisted `OutingEpisode` only if diagnostics show that string summaries cannot reliably recover participant/provenance/topic data.

Acceptance:

- structured episode is derived only from verified telemetry
- no LLM-written memory
- no duplicate or contradictory memory stores

## 24. Smallest useful first improvement

The smallest high-value behavior change is:

1. Turn the existing `CampTopicSeeds` strings into typed definitions with `TopicKey`.
2. Add a small bounded `TopicFatigueTracker`.
3. Add an explicit SILENCE candidate.
4. Stop treating `quiet >= max` as a requirement to find something to say.
5. Record the topic only after a line is actually emitted.

This can be implemented without touching persistent memory, event hooks, GroundingGuard, or Ollama routing.

It directly attacks the current failure mode while preserving the recent seeding work.

## 25. Risks of overengineering

Avoid:

1. A second autonomous director beside `EventConversationDirector` + `SocialBudget`.
2. Embeddings/vector search for a five-member party and a short recent topic window.
3. LLM-based topic classification or scoring.
4. A large provenance graph when a small enum + source reference is sufficient.
5. Persisting all seed usage to each Sim memory file.
6. Treating every current-state tick as an event.
7. Adding mana/role/friend-state reflection by guessing field names.
8. Letting personality enums become invented psychological profiles.
9. Giving every seed dozens of configuration weights.
10. Replacing existing event chains instead of generalizing them.
11. Auto-fetching wiki/news for ambient chatter.
12. Storing LLM-created outing summaries as facts.

The architecture should remain understandable from one diagnostic line.

## 26. Recommended ownership by class

A conservative target layout:

```text
Models.cs
    ConversationSeed enums/data or a new small ConversationSeeds.cs

ConversationSeedDirector.cs
    bounded collection/evaluation
    scoring
    silence candidate
    diagnostics
    thread opening

ConversationSeedProducers.cs
    current-state/event/memory/player/native producer helpers
    (split later only if it becomes large)

TopicFatigueTracker.cs
    short transient usage window

SocialDirector.cs
    detects social evaluation windows/context mode
    submits verified event/state changes
    no longer invents idle subject

SocialFoundation.cs
    SocialBudget remains cadence/admission/output budget

PromptBuilder.cs
    BuildSeedThread / selected-seed expression prompt

MemoryStore.cs
    source of existing verified/social memory
    no seed persistence by default

GroundingGuard.cs
    unchanged final safety boundary except optional seed-provenance-aware checks

DeterministicRegressionTests.cs
    seed selector/fatigue/knowledge-scope tests
```

Do not split into this many files merely for aesthetics. Follow the local source's current organization and make the smallest coherent refactor.

## 27. Final architectural rule

A good autonomous line should be explainable before the model is called:

```text
Why is Fiora speaking?
Because she is eligible to know this verified duel happened,
the duel is recent and important,
the topic is not fatigued,
her competitive personality slightly favors it,
and that seed beat silence.

What did the LLM decide?
Only how Fiora phrased that grounded social intent.
```

If the system cannot answer the first question, it should usually stay silent.
