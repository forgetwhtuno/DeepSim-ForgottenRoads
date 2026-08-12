# Feature Drafts

These packages are design scaffolding, not claims that the features are implemented. Each package contains a scope README and an acceptance-driven TODO.

## Packages

- [PvP Events](pvp-events/README.md) and [PvP Events TODO](pvp-events/TODO.md)
- [Companion System](companion-system/README.md) and [Companion System TODO](companion-system/TODO.md)
- [Nemesis System](nemesis-system/README.md) and [Nemesis System TODO](nemesis-system/TODO.md)

## Recommended program order

1. Build the shared special-relationship persistence and selection rules used by Companion and Nemesis.
2. Add Declared Skirmish using the existing PvP encounter pipeline.
3. Add bounded per-Sim/per-guild rivalry records and panel display.
4. Ship Companion as a social designation over an existing verified Sim.
5. Ship Nemesis as a deterministic social role with user approval.
6. Add King of the Hill only after the encounter pipeline supports objective-based endings.
7. Treat Capture the Flag as a separate stretch milestone.

## Shared boundaries

- Erenshor and its native Sim AI remain authoritative for movement, combat, spells, grouping, targeting, loot, and equipment.
- The LLM may choose social intent and wording only. It does not select encounter outcomes, relationship scores, targets, or schedules.
- Verified events may advance state. Player/AI dialogue is `HEARD` context and cannot become verified history by repetition.
- New persistent data remains bounded sidecar data. Core Erenshor saves are not edited.
- Optional bridges are primitive-data, reflection-only contracts so PvP, Follow, and Deep Sims remain independently usable.
- Co-op features fail closed until ownership and replication are explicitly verified.

## Shared relationship scaffold

Companion and Nemesis should share one small role registry instead of growing unrelated fields throughout `SimMemory`:

```text
SpecialRelationshipRegistry
- schemaVersion
- playerCharacterKey
- companionSimKey (optional)
- nemesisSimKey (optional)
- selectedUtc
- lastChangedUtc
```

Per-Sim progress stays in bounded, typed records attached to the existing memory sidecar. Stages are derived views over verified evidence; they are not free-form LLM state. One Sim cannot hold both special roles simultaneously.

