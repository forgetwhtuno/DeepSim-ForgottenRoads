# PvP architecture inspiration notes

These are implementation-neutral ideas for a future Erenshor PvP mod. They intentionally contain no copied external code and assume the current Practice Duels safety boundary remains the starting point.

## Challenge flow

Use a per-match state machine: `Idle`, `Offered`, `Accepted`, `Countdown`, `Active`, `Resolving`, `Cleaned`. An offer identifies both participants by stable local identity, expires quickly, may be refused, and has an opponent-pair cooldown. Only `Accepted` may reserve participants; only `Active` may accept match damage.

## Combat containment

Use virtual match health and a yield threshold. Native gameplay retains ownership of movement, attack selection, casts, targeting, and Sim AI. The PvP layer only validates the participant boundary and captures verified damage/heal/effect paths. Unknown third-party effects fail closed; real death, XP, loot, faction, item changes, and save mutation remain out of scope.

## Sim hostility / aggro

Hostility is match-local, not a global faction flip. Track the allowed opponent set explicitly. Suppress party and bystander assists, block unrelated healing/buffs, permit only pre-existing confirmed owned pets, and clear combat/aggro references on all terminal paths. Never use a display name as the primary identity key.

## Match lifecycle

Capture a complete pre-match snapshot before activation. End on yield, timeout, explicit stop, zone change, distance break, camp state, participant loss, verified hostile interruption, or exception. Cleanup must be idempotent and restore real HP/effects, prior legal target, aggro, follow/guard, autoattack, pet state, and temporary flags. Run a brief bounded post-cleanup sweep for delayed combat references.

## Team PvP

Treat a team as a stable set of identities with a single match token. Do not clone persistent Sims or mutate ordinary grouping until a verified temporary-avatar lifecycle exists. Start with local Sim-vs-Sim spectated matches; add mixed/team modes only after all participant, pet, effect, and cleanup paths have live tests. Use level/composition caps and average-level checks before matchmaking.

## Ranking

Begin with unranked practice. If ranking is added, store sidecar ratings per mode/team size, preserve a match audit record, and update only from validated terminal results. Use a documented expected-score rating calculation, provisional placement period, rating floor, and separate leaderboards per ruleset. Cancellations, interrupted matches, and admin stops never change rating.

## Rewards

Use sidecar PvP Marks rather than normal game progression. Reward only completed, eligible matches and keep cosmetic/trophy redemption separate from combat. Make reward eligibility explainable in diagnostics.

## Anti-farm

Maintain a bounded, sidecar opponent-pair ledger: repeated wins against the same identity quickly decay to zero marks/rating eligibility; apply a rematch cooldown and daily cap. Require a minimum active duration plus meaningful bilateral participation. Exclude forfeits, invalid states, protected zones, and cancellations. Detect only deterministic facts; do not guess intent.

## Spectators

Spectator is a distinct role, not a nearby actor. Spectators may observe and receive fact-only events but are ineligible for damage, healing, aggro, rewards, or rank changes. Join/leave is explicit and cleanup removes spectators at terminal state. Remote COOP humans remain excluded unless a later, verified host-authoritative bridge safely supports them.

## Social integration

Emit structured events with source-of-truth fields only: participants, stable IDs, zone, mode, terminal result, cancellation reason, and reward/rank result if applicable. Deep Sims may give short grounded reactions, but may not initiate/accept challenges, choose combat actions, determine results, or turn dialogue into facts.

## UI

Use native chat/menu styling and an explicit consent prompt. Show opponent, mode, expiry, and a short eligibility failure reason. The status screen should expose only match facts: state, participants, virtual health, timeout, and terminal/cancellation reason. Keep advanced ranking, anti-farm, and patch diagnostics behind a debug command.

## Validation gates before implementation

1. Reconfirm every patch target against the installed current `Assembly-CSharp.dll`.
2. Extend deterministic tests for state transitions, identity, locality, party exclusion, COOP remote-human exclusion, reward suppression, and cleanup idempotence.
3. Run live tests for physical, magic, DoT, pet, heal, status, aggro/assist, interruption, zone change, and delayed cleanup.
4. Keep rewards/ranking disabled until those tests pass.
