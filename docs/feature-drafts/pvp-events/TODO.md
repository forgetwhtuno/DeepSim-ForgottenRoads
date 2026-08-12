# PvP Events TODO

## Phase 0 — contracts and tests

- [ ] Add `DeclaredSkirmish`, `KingOfTheHill`, and `CaptureTheFlag` mode identifiers without changing current arranged/ambush behavior.
- [ ] Define an event-session state machine with terminal cleanup for refuse, timeout, zoning, player death, proxy defeat, escape, cancellation, and shutdown.
- [ ] Extend semantic events with versioned objective/mode fields while retaining contract-version compatibility.
- [ ] Add deterministic lifecycle tests before any Unity actor work.

Acceptance: existing PvP policy/planner/reward tests remain green and every new session state has one legal transition table.

## Phase 1 — Declared Skirmish

- [ ] Create a mutual-opt-in offer with team size, guild/leader, zone, expiry, and staging countdown.
- [ ] Require an eligible non-protected current zone for the first version.
- [ ] Reuse existing team planning, spawn, containment, rewards, cooldowns, flee, and cleanup.
- [ ] Add mode-aware sporting dialogue and sanitized Deep Sims events.
- [ ] Add compact pending/active cards and EVENTS detail view.
- [ ] Add `/epvp event status|accept|refuse` and a hidden deterministic force command.

Acceptance: accepted skirmish enters combat once, refused/expired offers spawn nothing, and every terminal path leaves no proxies or pending state.

## Phase 2 — rivalry records

- [ ] Define stable opponent and guild keys; never key solely by display name.
- [ ] Implement a versioned, bounded sidecar store with atomic replacement and recovery.
- [ ] Record only verified terminal results.
- [ ] Deduplicate by match ID.
- [ ] Add top rivals, personal matchup, and guild record to SCORE/EVENTS.
- [ ] Add export/reset controls that cannot touch Erenshor saves.

Acceptance: repeated delivery of one result changes the record once; reload preserves it; bounded retention is deterministic.

## Phase 3 — King of the Hill

- [ ] Define exact-scene objective centers, radius, match duration, score rate, and leash radius.
- [ ] Build pure contest/score/overtime rules.
- [ ] Add a visible but non-blocking hill marker using a verified native or mod-owned asset.
- [ ] Verify native attackers enter and remain capable of contesting the objective.
- [ ] Add objective-based victory/defeat and no-reward cancellation paths.
- [ ] Add score/timer display and semantic results.

Acceptance: score cannot accrue while contested; zoning/cleanup removes the marker; no LLM output moves an actor or awards points.

## Phase 4 — Capture the Flag investigation and stretch implementation

- [ ] Establish safe APIs for flag object spawn, interaction, attachment, drop, return, and destruction.
- [ ] Document pathing and fixed-area constraints before implementation.
- [ ] Build pure flag-state and scoring tests.
- [ ] Implement one configured arena/zone first.
- [ ] Verify death, flee, zoning, timeout, and shutdown always return/destroy flags.

Acceptance: do not mark this phase started until the spawn/carry APIs are proven from the installed assemblies or a live diagnostic.

