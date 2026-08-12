# Companion System TODO

## Phase 0 — shared relationship foundation

- [ ] Define a versioned per-character `SpecialRelationshipRegistry` sidecar.
- [ ] Define stable Sim eligibility and identity rules.
- [ ] Prevent companion/nemesis role overlap.
- [ ] Add migration, normalization, bounds, atomic write, and corrupt-file recovery tests.
- [ ] Keep ordinary `SimMemory` valid when no registry exists.

Acceptance: selection survives reload, is isolated by player character, and never edits Erenshor saves.

## Phase 1 — selection and status

- [ ] Implement explicit select/status/release commands.
- [ ] Add ambiguous-name handling and confirmation before replacement.
- [ ] Exclude remote humans, temporary actors, unknown identities, and nemesis role.
- [ ] Add a compact UI selection/status surface consistent with the shared mod panels.
- [ ] Show unavailable/offline status without inventing presence.

Acceptance: only the chosen stable Sim key is stored; release removes the role but preserves normal memories.

## Phase 2 — stage policy

- [ ] Define thresholds using completed outings, grouped minutes, verified shared events, familiarity, and rapport.
- [ ] Cap progress from conversation exchanges so spam cannot unlock stages.
- [ ] Make regression possible only if intentionally designed; do not silently oscillate stages.
- [ ] Add deterministic threshold/boundary tests.
- [ ] Expose reasons in diagnostics without showing raw dating-sim-style meters in normal UI.

Acceptance: unsupported chat or reconnect loops cannot advance a stage; verified outings can.

## Phase 3 — companion-aware social direction

- [ ] Add companion context only when relevant and within prompt-size limits.
- [ ] Prefer—but do not force—the companion as speaker when present and eligible.
- [ ] Add grounded milestone/reunion/close-call templates.
- [ ] Add bounded explicit commitment records labeled `HEARD`.
- [ ] Extend grounding tests for false presence, false promises, unsupported history, and stage claims.
- [ ] Preserve `NO_MESSAGE` and autonomous chatter limits.

Acceptance: the companion never speaks from the current scene when absent and never turns dialogue into a verified event.

## Phase 4 — optional Follow bridge

- [ ] Decide whether this ships at all in the first release.
- [ ] If enabled, expose only explicit player actions.
- [ ] Use a reflection-only primitive bridge to Erenshor Follow.
- [ ] Let Follow revalidate identity, party/locality, co-op authority, pathing, and cancellation.
- [ ] Verify Deep Sims never calls movement from LLM output or autonomous social events.

Acceptance: removing Follow leaves Companion fully functional socially; failed movement validation changes no companion state.

