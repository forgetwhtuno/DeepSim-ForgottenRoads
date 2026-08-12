# Deep Sims Nemesis System — Design Draft

## Goal

Designate one persistent antagonist Sim whose rivalry develops through bounded, remembered social exchanges and verified competitive outcomes.

The Nemesis role is a social director feature. It does not create a mechanical faction, rewrite Erenshor friendship data, or let the LLM spawn/command an enemy.

## Selection

The original idea of “LLM-selected” should be narrowed: deterministic code builds and scores an eligible candidate set; the LLM may provide flavor about the matchup but cannot choose identity or eligibility. The player should approve the first assignment and be able to replace/disable it.

Eligibility may consider verified:

- Stable Sim identity and similar level.
- Native Rival trait/personality signals.
- Existing bounded rivalry/competitive exchanges.
- Prior verified Practice Duel or PvP results.
- Current friend/party state when a safe reader exists.

Exclude the companion, remote humans, temporary proxies, active party members, invalid tracking identities, and protected tutorial-only characters.

## Friendship boundary

Do not intercept or modify real friend requests in the first version. No safe reversible native friend-request API has been established, and Deep Sims must not edit core saves. A nemesis can reject warmth in dialogue without changing Erenshor's actual friend list. If mechanical refusal is still desired later, investigate and verify the native API separately.

## Persistence and escalation

```text
NemesisProgress
- simKey
- designatedUtc
- verifiedWinsAgainstPlayer
- verifiedLossesAgainstPlayer
- verifiedPracticeDuels
- competitiveExchangeCount
- tauntsSent/tauntsAnswered
- lastTauntUtc
- lastTriggerId
- derivedGrudgeStage
```

Grudge stages are deterministic views over verified outcomes and bounded exchange metadata. Taunts do not prove game events. Repeated reloads, zone changes, or duplicate event delivery cannot increase state.

## Cadence and dialogue

- Cooldown measured in real UTC plus per-session caps.
- Trigger candidates: verified milestone, verified duel/PvP result, first eligible zone entry after cooldown, or direct player message.
- Off-map messages require verified online/tracking availability; otherwise the nemesis stays silent.
- Player replies enter `HEARD` conversation context and may affect tone metadata, not factual history.
- Language remains short MMO rivalry, not abusive harassment.

“Recruited” Sims are flavor-only unless a real PvP team contains them. Dialogue cannot assert a mechanical faction or physical presence without verified roster/scene evidence.

## PvP bridge

After Nemesis and PvP are independently stable, PvP may prioritize the nemesis as an eligible off-map leader. PvP still owns level/zone/team rules, consent mode, actors, combat, rewards, and outcomes. Deep Sims receives sanitized facts afterward.

Useful crossover:

- Standing arranged challenge from the nemesis.
- Higher selection weight, never guaranteed eligibility bypass.
- Rivalry record shown in both Nemesis status and PvP SCORE.
- Outcome advances grudge only once by stable match ID.

## Proposed code scaffold

```text
NemesisProgress
NemesisEligibilityPolicy
NemesisSelectionPolicy
NemesisStagePolicy
NemesisCadencePolicy
NemesisService
NemesisConversationContext
NemesisPvpBridge (optional, reflection-only)
```

## UI and commands

```text
/dsnemesis status
/dsnemesis candidates
/dsnemesis select <Sim>
/dsnemesis disable
/dsnemesis history
```

Normal UI should show identity, coarse rivalry stage, last verified competitive result, and cooldown state—not hidden prompts or raw manipulation scores.

## Opinion

The idea is compelling and pairs naturally with PvP, but it has more grounding hazards than Companion. The deterministic-selection correction is important: allowing the LLM to choose or enforce hostility would blur social flavor with authoritative state. Build it after Companion establishes the shared registry and stage model.

