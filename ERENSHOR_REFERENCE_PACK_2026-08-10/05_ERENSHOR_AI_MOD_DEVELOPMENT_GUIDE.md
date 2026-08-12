# Erenshor AI-Assisted Mod Development Guide

**Research snapshot:** 2026-08-10  
**Purpose:** a durable set of rules for an AI coding agent working on Erenshor mods.

This file intentionally focuses on process and architecture rather than one feature.

---

## 1. First principle: do not hallucinate the game API

Erenshor does not expose a stable official mod SDK.

When asked to implement a feature:

```text
READ current repo instructions
  -> INSPECT current code
  -> ESTABLISH exact current game/member evidence
  -> MAKE smallest coherent change
  -> BUILD against installed assemblies
  -> TEST/DIAGNOSE
```

Never:
- invent a class;
- invent a field;
- invent an enum value;
- invent a slash command;
- invent a network API;
- assume an old wiki class name is current;
- assume a public mod built for an old patch still has correct signatures.

If evidence is missing, leave a clear compatibility TODO or runtime capability-off path.

---

## 2. Source trust order

For coding:

```text
1. current repository source
2. current installed Erenshor assemblies
3. current live diagnostics
4. current official patch notes
5. recent working public mod source
6. recent official wiki
7. old public source/wiki
8. inference
```

For player-facing lore/mechanics:

```text
1. observed current game
2. official developer announcement
3. recently maintained official wiki
4. older wiki
5. community guide
```

Do not silently merge contradictions.

---

## 3. Erenshor should own gameplay truth

For social/AI mods:

```text
Erenshor decides:
  movement
  pathfinding
  target legality
  attacks
  heals
  loot
  grouping
  roles
  quests
  saves
  item ownership
  faction
  combat result

mod decides:
  whether a verified event is interesting
  what UI to show
  what bounded memory to retain
  whether to request language generation

LLM decides:
  wording/social intent only
```

The model should not call game methods directly.

---

## 4. Separate fact provenance

Every fact in an AI context should have provenance.

Recommended categories:

```text
OBSERVED_NOW
VERIFIED_EVENT
DERIVED_MEMORY
OFFICIAL_KNOWLEDGE
WIKI_KNOWLEDGE
PLAYER_SAID
SIM_SAID
GENERATED
UNKNOWN
```

### Example

```text
OBSERVED_NOW:
  Phanty is in current subgroup.
  Phanty class = Arcanist.

VERIFIED_EVENT:
  Practice duel between player and Dancer ended.

PLAYER_SAID:
  "Phanty tanked this last night."

GENERATED:
  Dancer: "Phanty would probably tank it."
```

Only the first two can establish game history.

---

## 5. Do not turn dialogue into memory truth

Generated chat is evidence that a line was said, not evidence that its content happened.

Unsafe:
```text
LLM says "remember when we killed Brax?"
-> save "party killed Brax"
```

Safe:
```text
verified boss-death event says Brax died
-> save compact verified event
-> later LLM may reference it
```

---

## 6. Temporal language requires evidence

Guard phrases such as:
- again;
- last time;
- remember when;
- back here;
- another one;
- still;
- used to;
- always.

They often imply history.

If no verified supporting memory exists, rewrite:

```text
"Let's see how this one goes."
```

instead of:

```text
"Let's not wipe again."
```

---

## 7. Class/role grounding

Never conflate:
- class;
- possible role;
- assigned role;
- current behavior.

Example:

```text
Druid
  possible: healing / magic DPS
  assigned: unknown
  current observed action: casting heal
```

A single heal does not prove the role assignment. A class does not prove the role assignment.

---

## 8. Party and raid grounding

At v0.7:
- ordinary group logic exists;
- raids can contain three groups and 15 total characters.

A social/controller mod should carry:
- local subgroup;
- whole raid roster;
- party/raid membership;
- human vs local Sim vs remote human;
- role scope.

Do not call the whole raid "the party" internally if that creates ambiguity.

---

## 9. Actor authority check before every action feature

For movement/combat/action UI, require:
- known actor;
- local;
- correct actor category;
- current scene;
- alive;
- allowed membership;
- no conflicting state.

If COOP is installed, explicitly exclude remote humans unless the feature was designed and tested for them.

---

## 10. Chat must preserve vanilla gameplay commands

Group chat can drive Sim behavior.

Therefore:
- observe but do not consume command-bearing player lines;
- inject social output after vanilla has had its command path;
- preserve log/filter metadata;
- mark mod-generated output to avoid reprocessing.

A "better chat AI" that breaks `/group follow` is a regression.

---

## 11. Use a Social Director, not random chatter hooks

All autonomous speech should flow through one admission controller.

It should consider:
- event priority;
- global cooldown;
- per-Sim cooldown;
- per-event cooldown;
- recent player speech;
- current combat;
- semantic duplication;
- rolling message budget;
- current eligibility;
- authority/COOP host state.

Then one social moment produces at most one coherent thread.

This avoids five independent features all talking at once.

---

## 12. Templates are a first-class mode

Use deterministic responses for:
- ready checks;
- rolls;
- brief greetings;
- trivial acknowledgements;
- OOM/recovery;
- simple post-fight reactions;
- duel spectator reactions.

Use an LLM when:
- language variation matters;
- context is richer;
- several verified facts need synthesis;
- the player asks an open-ended question.

This reduces latency and hallucination risk.

---

## 13. External knowledge stays separate

Distinguish:
- game wiki;
- official game patch/news;
- real-world web/news;
- conversation.

A real-world news result should never become:
- Erenshor lore;
- Sim personal history;
- permanent relationship memory.

Give external context a TTL.

---

## 14. Never block Unity for AI

Required architecture:

```text
main thread:
  capture snapshot
  enqueue work

background:
  HTTP/model/RAG
  parse response

main thread:
  validate actor/session still relevant
  display line
```

Use:
- timeout;
- cancellation;
- bounded queue;
- failure cooldown;
- max concurrency.

If a request returns after the player zoned or party changed, revalidate before output.

---

## 15. Keep prompts bounded

Good prompt context is not "everything known."

Use:
- current question/event;
- current party identity;
- current encounter;
- last completed encounter if relevant;
- a few verified memories;
- a few conversation lines;
- relevant wiki snippet only when needed.

Do not serialize the whole session/history on every line.

---

## 16. Performance methodology

Measure separately:
- Unity-side snapshot time;
- queue delay;
- request wall time;
- provider/model load time;
- prompt evaluation;
- generation;
- main-thread frame hitches.

Do not say "the AI caused a hitch" just because the times overlap. Report correlation.

---

## 17. Sidecar failure should degrade gracefully

Recommended modes:

```text
Auto
  templates for trivial events
  LLM where useful if healthy
  templates if provider unavailable

LLM
  prefer LLM
  safe fallback on failure

Templates
  no inference requests

Off
  no autonomous social output
```

Core game and deterministic utility features must keep working if Ollama/model/service is gone.

---

## 18. Cross-mod integration should be optional and versioned

If Deep Sims wants Practice Duel events:
- detect whether Practice Duels is installed;
- bind a small read-only event/status surface;
- never require it for base startup;
- ignore unknown future versions safely.

If Follow wants a duel button:
- show it only when capability exists;
- do not compile the whole feature into a hard dependency unless necessary.

With Lunaris, prefer Aura IPC for new native integrations.

---

## 19. Game update response procedure

After every substantial Erenshor patch:

1. read official patch notes for subsystem refactors;
2. record current game/build;
3. rebuild against current assemblies;
4. verify every Harmony target;
5. run deterministic unit tests;
6. run smoke test:
   - login;
   - party;
   - combat;
   - zone;
   - disconnect;
7. test exact feature paths;
8. test cleanup/unload if Lunaris;
9. update compatibility docs.

The March 2026 chat rewrite and July 2026 raid/AH/save changes demonstrate why this is necessary.

---

## 20. Do not regress working safety boundaries

When expanding a feature, preserve the strongest established constraint.

Example, Practice Duels:
- adding nearby non-party Sims should not loosen third-party isolation;
- a broader challenge target set must still positively exclude remote humans;
- keep virtual health/restoration;
- keep hostile cancellation.

Example, Follow:
- adding route intelligence should not turn into teleportation;
- global destination knowledge should not bypass local NavMesh proof.

Example, Deep Sims:
- richer memory should not let generated dialogue become verified history;
- more autonomous banter should not bypass the central budget.

---

## 21. Prefer feature flags/capabilities to version guesses

Bad:
```text
if gameVersion >= 0.7:
  call NewMethod()
```

Better:
```text
if compatibility.TryBindNewMethod(out handler):
  capability = true
else:
  capability = false
```

Version strings help diagnostics, but actual member shape is what matters for a runtime mod.

---

## 22. Build/install scripts should fail safe

A build script should:
- locate game/profile;
- validate required DLLs;
- compile;
- stop on errors;
- copy only the intended output;
- avoid overwriting source or arbitrary files;
- print exact target path/version.

A patch/apply script should:
- verify source blocks;
- refuse partial mutation if assumptions fail;
- make a backup or use git;
- be idempotent where possible.

---

## 23. Test deterministic core without the game

Move logic out of MonoBehaviours where possible.

Unit-test:
- event admission;
- cooldowns;
- memory provenance;
- temporal-history guard;
- duplicate suppression;
- relationship math;
- duel virtual-health math;
- target eligibility using plain DTOs;
- command parsing;
- serialized sidecar data migrations.

Keep Unity/game access behind adapters.

---

## 24. In-game diagnostics are part of the product

When something fails, users should be able to produce:
- plugin version;
- detected capabilities;
- feature state;
- last rejection/cancel reason;
- last relevant actor;
- current zone;
- optional integration status.

This shortens debugging dramatically.

---

## 25. Security/privacy for local-AI/network mods

Do not commit:
- API keys;
- personal memory exports;
- private endpoints;
- auth tokens.

Default local services to:
- localhost;
- no unauthenticated LAN binding unless explicitly enabled;
- limited endpoints;
- bounded input size.

Do not send chat/history to a cloud provider without a clear user setting.

---

## 26. Recommended AI coding-agent prompt fragment

Use this with future tasks:

> Work from the current repository and current installed Erenshor assemblies, not remembered API names. Inspect relevant code before modifying it. Erenshor internals are not a stable public API: do not invent members, enum values, commands, network capabilities, or game behavior. Prefer existing proven accessors/hooks. Keep gameplay authoritative in Erenshor; AI may observe and express, not directly control gameplay unless the requested deterministic feature explicitly requires it. Preserve current safety boundaries, COOP remote-human exclusions, save/zone lifecycle safety, and cleanup. Add an observable acceptance check and diagnostics for new compatibility assumptions.

---

## 27. Representative sources

Game/current behavior:
- https://store.steampowered.com/app/2382520/Erenshor/
- https://steamcommunity.com/app/2382520/allnews/
- https://erenshor.wiki.gg/

Modding:
- https://docs.bepinex.dev/
- https://github.com/BepInEx/HarmonyX
- https://github.com/MizukiBelhi/Lunaris
- https://mizukibelhi.github.io/Lunaris-Docs/

Architecture examples:
- https://github.com/MizukiBelhi/ErenshorCoop
- https://github.com/Brumdail/ErenshorQoL
- https://github.com/aepod/ErenshorLLM
- https://github.com/forgetwhtuno/DeepSim-erenshor
- https://github.com/forgetwhtuno/ErenshorFollow
- https://github.com/forgetwhtuno/Erenshor-Duel
