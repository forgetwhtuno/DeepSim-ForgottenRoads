# Nemesis System TODO

## Phase 0 — shared foundation

- [ ] Reuse the versioned per-character special-relationship registry.
- [ ] Define `NemesisProgress` bounds and migrations.
- [ ] Enforce one nemesis, no companion overlap, and explicit disable/replacement.
- [ ] Add duplicate-event and corrupt-sidecar recovery tests.

Acceptance: no registry state is selected or mutated by generated text.

## Phase 1 — eligibility and selection

- [ ] Establish verified readers for stable identity, level, Rival signal, locality, party membership, and co-op classification.
- [ ] Decide whether existing native friends are excluded; fail closed if friend state is unknown.
- [ ] Build deterministic candidate scoring and tie-breaking.
- [ ] Require player confirmation for initial selection and replacement.
- [ ] Add candidates/status/disable commands and matching UI.
- [ ] Do not implement mechanical friend-request refusal without a separately verified API.

Acceptance: the same inputs select the same candidate; ineligible Sims never appear; LLM availability does not affect selection.

## Phase 2 — cadence and escalation

- [ ] Define coarse grudge stages from verified results and bounded competitive exchanges.
- [ ] Add UTC cooldown, per-session cap, and trigger deduplication.
- [ ] Require verified online/current availability before off-map messaging.
- [ ] Add non-abusive short taunt templates plus optional LLM rewriting under grounding guards.
- [ ] Add direct reply routing without turning player claims into facts.
- [ ] Add silence and suppression rules during combat, zoning, tutorials, character select, and unrelated active conversations.

Acceptance: reload/zone spam cannot generate repeated taunts or advance grudge; absent/unknown nemeses do not claim presence.

## Phase 3 — memory and social integration

- [ ] Store verified duel/PvP outcome IDs once.
- [ ] Store taunt/reply summaries as bounded `HEARD` continuity.
- [ ] Add prompt context only when the nemesis is speaker/topic relevant.
- [ ] Extend grounding guards for invented prior fights, fake recruited allies, unsupported guild claims, and false friendship state.
- [ ] Add concise diagnostic/export sections.

Acceptance: a remembered exchange can affect tone but cannot prove a fight, win, loss, guild, zone, or alliance.

## Phase 4 — optional PvP crossover

- [ ] Define a reflection-only request/observation contract between Deep Sims and PvP.
- [ ] Let PvP validate off-map status, zone, level, party size, cooldown, and consent mode independently.
- [ ] Allow selection weighting but never bypass PvP safety policy.
- [ ] Deduplicate completed results by match ID before advancing Nemesis state.
- [ ] Surface personal matchup history without creating a fictional global ladder.

Acceptance: either mod works alone; removing Deep Sims leaves PvP unchanged; removing PvP leaves the social Nemesis system safe and non-combatant.

