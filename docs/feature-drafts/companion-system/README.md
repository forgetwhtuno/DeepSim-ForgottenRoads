# Deep Sims Companion System — Design Draft

## Goal

Let the player designate one verified persistent Sim as a long-term social companion. The companion gains richer continuity and conversational priority over many outings while Erenshor remains authoritative for gameplay.

This should feature an existing Sim identity, not create a replacement NPC or duplicate Sim.

## Relationship stages

Stages are derived from verified evidence and existing relationship dimensions:

```text
stranger -> acquainted -> trusted -> bonded
```

Suggested evidence includes completed outings, actual grouped minutes, verified shared encounters, reciprocal conversation exchanges, and durable rapport. Chat volume alone cannot grind a stage, and an LLM cannot directly set or increase it.

`Familiarity`, `Rapport`, and `Rivalry` already exist in `SimMemory`. The companion feature should add typed evidence/capability unlocks rather than a second conflicting relationship score.

```text
CompanionProgress
- simKey
- designatedUtc
- completedOutingsTogether
- verifiedSharedEventIds (bounded)
- explicitPlayerCommitments (bounded HEARD records)
- lastCompanionExchangeUtc
- derivedStage
```

Promises and conversation history require special care: an explicit player promise may be retained as `HEARD` conversational continuity, but it is not a verified world fact and must never prove that an action occurred.

## Selection and lifecycle

- Player explicitly selects from verified known Sims; assignment should never be random by default.
- Selection excludes remote humans, temporary PvP proxies, missing identities, and the active nemesis.
- Changing/releasing a companion is reversible and does not delete ordinary Sim memory.
- The role is per player character, not accidentally shared across character slots.
- Missing/offline companions remain remembered but cannot speak as though physically present.

## Social behavior

Companion status may influence:

- Speaker preference when the companion is actually eligible to speak.
- Reunion, departure, close-call, victory, defeat, and milestone reactions.
- Bounded continuity around explicit prior conversation topics.
- Opinions and tone based on the existing preference/relationship system.

It may not cause movement, attacks, healing, targeting, equipment changes, invitations, or quest actions.

## Optional gameplay presence

If travel presence is desired, use an explicit optional bridge to standalone Erenshor Follow. Deep Sims may expose a player-facing action such as “Follow companion,” but Follow must validate the target and own all movement. Companion status alone must never automatically issue movement or grouping commands.

Cosmetic presence without a live verified Sim should be deferred; spawning a duplicate visual creates identity and lifecycle risks similar to early PvP prototypes.

## Proposed code scaffold

```text
SpecialRelationshipRegistry
CompanionProgress
CompanionStagePolicy
CompanionEligibilityPolicy
CompanionService
CompanionConversationContext
CompanionEventProjector
CompanionFollowBridge (optional, reflection-only)
```

## UI and commands

Use a compact Deep Sims social panel or a Party Tools-compatible section rather than another permanently open window.

```text
/dscompanion status
/dscompanion select <Sim>
/dscompanion release
/dscompanion memories
```

Selection and release require confirmation in UI when they would replace an existing role.

## Opinion

This is a strong fit for Deep Sims and should come before Nemesis. Most required data already exists, and building explicit selection, derived stages, and bounded continuity creates the reusable relationship foundation. The best first version is social designation and richer context—not a new follower or combat pet.

