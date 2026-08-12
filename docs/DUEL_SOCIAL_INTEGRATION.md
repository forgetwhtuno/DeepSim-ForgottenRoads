# Practice Duels social integration

Deep Sims treats Practice Duels as a **verified social event source**, not a gameplay controller.
Erenshor/Practice Duels remains authoritative for eligibility, acceptance/decline, virtual health,
combat state, cancellation, and result. Deep Sims may only decide whether one of the current Deep
Sims should say something about an already-verified event, then express that fact through a safe
template or the grounded LLM path.

## Optional structured boundary

Current Practice Duels can discover this public static method through reflection:

```text
ErenshorDeepSims.DuelEventBridge.NotifyDuelEvent(
    eventType,
    opponent,
    scope,
    decision,
    outcome,
    winner,
    yielded,
    reasonToken,
    reason)
```

There is no compile-time dependency between the mods. Practice Duels should prefer this structured
surface and use its existing `NotifyObservedGameEvent(...)` reflection path only as a compatibility
fallback. Deep Sims intercepts duel-shaped generic fallback events before generic telemetry/memory
handling and sends them through the same adapter. A short semantic fingerprint suppresses a caller
that accidentally sends both forms.

Supported lifecycle types are exactly:

```text
duel_challenge
duel_accepted
duel_declined
duel_started
duel_completed
duel_cancelled
```

The structured fields are facts. Generated prose is never parsed back into a gameplay decision.

## Social policy

- `duel_challenge`: observed structurally; no autonomous chatter and no durable memory.
- `duel_accepted`: only the challenged Sim may speak, and only when that opponent is a current
  **party Deep Sim**. Nearby non-party Sims do not gain a Deep Sim identity.
- `duel_declined`: same speaker restriction. The deterministic Duel decision token remains
  authoritative; templates/LLM may not reverse or invent the reason.
- `duel_started`: structural/silent.
- `duel_completed`: higher-value event. Current party Deep Sims may be eligible, but the existing
  `friendly_duel` event routing keeps it to at most one post-duel line and still uses the central
  SocialBudget, speaker cooldowns, priority arbitration, and social-authority rules.
- `duel_cancelled`: ordinary cancellation is silent. A verified `hostile_interruption` reason token
  may create one bounded reaction; it still loses to combat/social gating when the moment is busy.

`Auto` mode treats short duel reactions as ritual chatter so deterministic templates can be used even
while Ollama is healthy. `Templates` needs no Ollama or internet. `LLM` receives a compact
`DUEL_EVENT:` block and the same verified-event grounding pipeline as other Deep Sims events.

## Grounding

A friendly duel verifies only sparring facts explicitly present in the contract. It never, by itself,
verifies real death/killing, loot, XP, rewards, wagers, faction hostility, permanent injury, class-
specific technique, or combat-style details. A timeout has no winner unless Duel supplies one. A
single completion cannot justify `always`/`every time`, and comparison language such as `again` is
rejected unless the authoritative decision itself is `decline_recent_duel`.

No `close match` wording is generated because the current Duel contract does not expose a verified
closeness metric.

## Memory

Only `duel_completed` qualifies for one compact `friendly_duel` memory event. Challenge, acceptance,
decline, start, and cancellation do not become durable duel memories. `RecordSharedEvent` writes only
to the current Deep Sim roster, so a nearby non-party opponent never causes a new persistent Deep Sim
profile or memory file.

## Deterministic validation

`tests/RUN_DETERMINISTIC_TESTS.ps1` compiles `DuelSocialSemantics.cs` into the existing standalone
regression executable. The duel tests cover authority contradictions, party/non-party identity,
social significance, deterministic templates, non-lethal grounding, history/loot restrictions,
structured+fallback deduplication, repeated completion suppression, and completion-only memory
qualification. The full in-game `/dsevents test` output also appends these duel semantics checks.
