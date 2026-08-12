# Deep Sims Conversation Seed Schema

Status: proposed v1 schema  
Purpose: a small deterministic contract between verified Deep Sims/game state and autonomous social expression.

## 1. Design constraints

The schema should:

- fit the existing C# architecture
- generalize the useful parts of `SocialEventCandidate`
- carry enough provenance to determine who may speak
- separate a specific event/fact from its semantic topic
- support expiry and fatigue without becoming permanent memory
- remain easy to log and deterministic-test
- avoid an ontology or knowledge graph

The selector must never treat the schema itself as proof. Seed producers are responsible for creating seeds only from allowed sources.

## 2. Proposed enums

```csharp
internal enum ConversationSeedCategory
{
    CurrentState,
    RecentEvent,
    SharedMemory,
    Relationship,
    NativeSocial,
    PlayerTopic,
    WorldLore,
    Idle
}

internal enum ConversationSeedTrust
{
    ObservedNow,
    Experienced,
    Remembered,
    NativeState,
    Heard,
    Reference
}

internal enum KnowledgeScope
{
    Self,
    DirectParticipant,
    PartyWitnessed,
    NearbyWitnessed,
    GuildPublic,
    WorldPublic,
    PlayerTold,
    SharedMemory
}
```

Notes:

- `ObservedNow`, `Experienced`, and `Remembered` align with the project's existing trust model.
- `NativeState` means a safely read Erenshor-owned state value, not "whatever vanilla dialogue said."
- `Heard` remains conversational/non-factual.
- `Reference` is cached wiki/news/lore context and must not certify personal experience.
- `NearbyWitnessed` should remain unused until actual presence/proximity can be verified.

If local code already has a generalized trust enum, reuse it rather than adding another.

## 3. Proposed seed class

```csharp
internal sealed class ConversationSeed
{
    // Identity
    internal readonly string SeedId;
    internal readonly string TopicKey;
    internal readonly ConversationSeedCategory Category;

    // Grounded content
    internal readonly string Fact;
    internal readonly string Source;
    internal readonly ConversationSeedTrust Trust;
    internal readonly KnowledgeScope KnowledgeScope;
    internal readonly List<string> GroundingRefs;

    // People/entities
    internal readonly List<string> Participants;
    internal readonly List<string> EligibleSpeakerNames;
    internal readonly string Subject;

    // Lifetime / selection
    internal readonly DateTime CreatedUtc;
    internal readonly DateTime ExpiresUtc;
    internal readonly int Importance;       // 0..100
    internal readonly double Novelty;       // 0..1
    internal readonly string CooldownGroup;
    internal readonly bool MemoryEligible;

    // Existing social priority may be derived instead of stored.
}
```

Do not require all fields to be persisted or serialized.

## 4. Field semantics

### SeedId

Identifies one particular candidate fact/event.

Examples:

```text
encounter:42
duel:8472
party_join:fiora:20260810T140511Z
memory:phanty:outing:7
player_topic:party:193
state:cyndara:mana_low:transition:12
```

Properties:

- unique enough for the short active window
- stable for the lifetime of the candidate
- should not be a random GUID when the source already has a stable event ID
- not used for semantic fatigue by itself

### TopicKey

Canonical semantic subject.

Examples:

```text
idle_waiting
zone:azure
duel:fiora:player
party_member:fiora
mana:cyndara
camp_recovery
loot:aetheria
loot
recent_wipe
guild:lantern
player_topic:loot
outing:azure
```

This is the primary key for idea-level fatigue.

Producer code should assign it. Do not ask the LLM to discover it.

### Category

Where this seed belongs for scoring/configuration.

Keep categories broad. Do not create one enum member per event type.

### Fact

The smallest bounded factual/conversational payload required for expression.

Good:

```text
Fiora and the player just completed a verified friendly duel.
```

Better when known:

```text
The verified friendly duel between Fiora and the player ended moments ago.
```

Do not include unsupported winner/close-fight details.

For a HEARD player topic:

```text
The player recently discussed loot and equipment in current party chat.
```

This supports raising the subject; it does not make the player's claims true.

### Source

Human-readable producer/source label.

Examples:

```text
SessionTelemetry encounter 42
Practice Duel verified completion event
MemoryStore Phanty outing summary 7
current party snapshot
native Erenshor guild roster
party chat topic classifier
cached wiki result
```

Keep this compact; detailed object dumps do not belong in logs.

### Trust

Maps to existing fact hierarchy.

Suggested prompt/guard ordering remains:

```text
ObservedNow
> Experienced
> Remembered
> NativeState (depending on the exact state)
> Reference
> Heard
```

`NativeState` often belongs near `ObservedNow`, but classify per field rather than assuming all native state is equally current.

### KnowledgeScope

Describes how eligibility was established.

It must not be inferred by the LLM.

### GroundingRefs

Small internal references that let prompt/guard code find the supporting source if needed.

Examples:

```text
encounter:42
memory:phanty:event:15
duel:event:8472
native:guild:12
chat:party:line:193
```

Avoid raw Unity object references in persisted state.

### Participants

People directly involved in the event/memory.

This is not the same as who may speak.

### EligibleSpeakerNames

Explicit current candidates who are allowed to know/reference the seed.

Compute this before selection.

Re-check against the current active party immediately before output.

### Subject

Optional compact entity the seed is "about".

Examples:

```text
Fiora
Aetheria
Azure
Lantern
Lost Sea Giant
```

Useful for speaker relevance and diagnostics.

### CreatedUtc

When the source fact/seed became available.

Use game time too only if an existing verified game-time abstraction is useful. Do not invent one solely for this system.

### ExpiresUtc

Ephemeral state must expire.

Suggested classes:

```text
low mana / current recovery       10–30s after condition clears
party join/leave                  ~20s event window
combat just ended                 ~20–45s
duel/death/level/quest event      existing event window / short queue
player topic                      a few minutes
current zone                      while current
shared memory                     no short expiry, but recency score decays
```

A condition-clearing event should invalidate a seed immediately instead of waiting for time expiry.

### Importance

Use the current 0–100 convention.

Importance is source/event significance, not "how funny this might be."

### Novelty

0..1 estimate determined by code.

Examples:

```text
new duel event                1.0
first discussion of outing    0.8
current zone                   0.2
idle                           0.0
```

Topic fatigue is still applied separately.

### MemoryEligible

Metadata only.

It does not cause persistence.

Examples:

```text
low mana            false
camp state          false
verified duel       true under existing policy
completed encounter maybe
player topic        false as factual memory
```

MemoryStore/SessionTelemetry policy remains authoritative.

### CooldownGroup

Groups related topics more broadly than TopicKey.

Examples:

```text
duel
loot
mana
party_change
zone
outing_memory
idle
guild
```

A new exact `TopicKey` can still receive a smaller penalty from recent use of its broader group.

## 5. Topic fatigue record

Transient only:

```csharp
internal sealed class TopicUsage
{
    internal string TopicKey;
    internal string CooldownGroup;
    internal DateTime LastUsedUtc;
    internal int RecentUseCount;
    internal string LastSpeaker;
    internal long LastConversationId;
}
```

Recommended storage:

```text
Dictionary<TopicKey, TopicUsage>
bounded queue/list for 10-minute pruning
optional Dictionary<CooldownGroup, DateTime>
```

Do not write this to every Sim's permanent memory in v1.

If cross-session repetition later proves noticeable, persist only a tiny coarse recent-topic cache, not full usage history.

## 6. Selected result

Keep selection output separate from input seed:

```csharp
internal sealed class ConversationSeedDecision
{
    internal DateTime Utc;
    internal ConversationSeed Seed;   // null when silence
    internal string Speaker;
    internal double Score;
    internal double SilenceScore;
    internal bool Selected;
    internal string Reason;
    internal List<SeedScoreComponent> Components;
}
```

For production memory efficiency, `Components` may be diagnostics-only.

### Silence

Do not fake silence as a normal seed with fabricated `Fact`.

Represent it explicitly:

```csharp
internal sealed class SilenceCandidate
{
    internal double Score;
    internal string ContextMode;
}
```

or a nullable selected seed with a recorded `SilenceScore`.

## 7. Score component

Diagnostics-friendly:

```csharp
internal sealed class SeedScoreComponent
{
    internal string Name;
    internal double Value;
}
```

Typical components:

```text
category
importance
recency
knowledge_scope
shared_relevance
novelty
personality
topic_fatigue
speaker_repeat
grounding_risk
```

The runtime can avoid allocation-heavy component lists when diagnostics are disabled.

## 8. Context mode

Optional small enum:

```csharp
internal enum SocialContextMode
{
    Normal,
    Camp,
    Relax,
    Expedition
}
```

This changes weights/silence/thread cap only.

It does not establish facts.

`Camp` already has a verified detector in current code. `Relax` and `Expedition` should not be emitted until their state is explicitly defined/verified.

## 9. Seed-bound thread

Generalize the existing verified event chain:

```csharp
internal sealed class SeedConversationContext
{
    internal long ConversationId;
    internal string SeedId;
    internal string TopicKey;
    internal string Fact;
    internal ConversationSeedTrust Trust;
    internal KnowledgeScope KnowledgeScope;
    internal List<string> GroundingRefs;
    internal List<string> EligibleSpeakerNames;
    internal DateTime OpenedUtc;
    internal DateTime ExpiresUtc;
    internal int MaxLines;
    internal int LinesEmitted;
}
```

Every autonomous continuation uses this original context.

Do not create a new independent seed between Sim replies.

## 10. Example seeds

### Verified duel

```text
SeedId: duel:8472
TopicKey: duel:fiora:player
Category: RecentEvent
Fact: A verified friendly duel between Fiora and the player just ended.
Source: Practice Duel completion event 8472
Trust: Experienced
KnowledgeScope: DirectParticipant / PartyWitnessed as captured
Participants: Fiora, Player
EligibleSpeakers: captured allowed speakers only
Subject: Fiora
Importance: 85
Novelty: 1.0
MemoryEligible: true
CooldownGroup: duel
```

Do not add a winner unless the verified event supplies it.

### Low mana at Camp

Only when live API is verified:

```text
SeedId: state:cyndara:mana_low:transition:12
TopicKey: mana:cyndara
Category: CurrentState
Fact: Cyndara's verified current mana is below the configured recovery threshold.
Source: live party resource snapshot
Trust: ObservedNow
KnowledgeScope: Self / PartyWitnessed
Subject: Cyndara
Importance: 55
Novelty: 0.7
MemoryEligible: false
CooldownGroup: mana
ExpiresUtc: condition clears
```

The current GitHub snapshot does not expose mana in `SimSnapshot`; live production must remain disabled until local verification.

### Old outing memory

```text
SeedId: memory:phanty:outing:7
TopicKey: outing:azure
Category: SharedMemory
Fact: <existing verified compact outing summary>
Source: MemoryStore OutingSummaries[7]
Trust: Remembered
KnowledgeScope: SharedMemory
EligibleSpeakers: Phanty, plus only other Sims whose own memory/provenance supports it
Importance: 55
Novelty: 0.5
MemoryEligible: false
CooldownGroup: outing_memory
```

### Recent player topic

```text
SeedId: player_topic:party:193
TopicKey: player_topic:loot
Category: PlayerTopic
Fact: The player recently discussed loot and equipment in party chat.
Source: party conversation line/topic classifier
Trust: Heard
KnowledgeScope: PlayerTold
EligibleSpeakers: Sims present in that conversation scope
Importance: 35
Novelty: 0.5
MemoryEligible: false
CooldownGroup: player_topic
```

This seed permits returning to the subject. It does not establish any loot claim the player made.

### Idle

If retained:

```text
SeedId: idle:current-window
TopicKey: idle_waiting
Category: Idle
Fact: none
Trust: ObservedNow
KnowledgeScope: PartyWitnessed
Importance: 0
Novelty: 0
MemoryEligible: false
CooldownGroup: idle
```

It should have a strongly negative base score and normally lose to silence.

## 11. Validation rules

A seed is invalid if:

- `TopicKey` is empty
- the source fact is required but empty
- it has expired
- its producer cannot prove the declared scope
- no currently eligible speaker remains
- a direct-participant scope has no participant identity
- a memory seed widens knowledge beyond the memory owner without provenance
- a player-topic seed originates from a private conversation but includes other Sims
- a current-state condition has already cleared
- the source is a generated line being treated as game fact

The selector must exclude invalid seeds, not merely subtract a score.

## 12. Compatibility with existing SocialEventCandidate

Migration can be incremental.

Adapter:

```text
SocialEventCandidate
    Type              -> Topic/event subtype
    ObservedUtc       -> CreatedUtc
    InvolvedNames     -> Participants
    EligibleSpeakers  -> EligibleSpeakerNames
    Trust             -> ConversationSeedTrust
    Importance        -> Importance
    Novelty           -> Novelty
    CooldownCategory  -> CooldownGroup
    VerifiedContext   -> Fact
    VerifiedEntities  -> GroundingRefs/Subject hints
```

Add:

```text
SeedId
TopicKey
Category
KnowledgeScope
Source
ExpiresUtc
MemoryEligible
```

Do not delete working event behavior until the generalized path has deterministic parity tests.
