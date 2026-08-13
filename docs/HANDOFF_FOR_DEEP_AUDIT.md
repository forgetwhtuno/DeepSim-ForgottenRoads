# Handoff for Deep Audit

Written for a technical reviewer (human or AI) picking this repo up cold. Points at the
authoritative sources rather than re-deriving them, and records this session's live evidence so
you don't have to re-run everything just to get back to where things currently stand.

## Architectural boundaries — read `AGENTS.md` first

This repo's root `AGENTS.md` (~1350 lines) is the authoritative architecture document. Section 1
("Non-Negotiable Architecture Rules") is load-bearing and predates this session — do not treat
anything below as superseding it:

- **1.1 Erenshor controls gameplay.** Deep Sims never decides combat, movement, loot, or party
  state. It observes and talks.
- **1.2 Facts and flavor must stay separate.** Only content explicitly labeled VERIFIED in a
  prompt is treated as ground truth by the model; everything else (prior chat, personality,
  preferences) is dialogue continuity only, never evidence a game event occurred.
- **1.3 Never fabricate personal history.** No invented past events, relationships, or biography.
- **1.4 Prefer actual experience over wiki knowledge** when both are available.
- **1.5 Silence is valid** — `NO_MESSAGE` is a correct, expected output, not a failure mode.
- **1.6 Sims are MMO players, not assistants** (in MMO perspective) — this session added a second,
  distinct identity contract for Roleplay perspective (see below); the two must never both appear
  in one prompt (enforced in `PromptBuilder.BuildSystemPrompt` by a single `if (roleplay) {...}
  else {...}` branch, not by layering).
- **1.7 Never alter Erenshor save files.** All mod-owned state lives under its own sidecar
  storage, never inside the game's own save data.

## Two distinct "voice" concepts — do not conflate them

- **Expression mode** (`SocialPolicy`/`ExpressionMode`): *how* a line is produced — LLM vs.
  deterministic template vs. off. Orthogonal to perspective.
- **Perspective** (`SocialPerspective`/`SocialPerspectiveState`, `src/RoleplayPerspective.cs`):
  *who* is speaking — an MMO player describing playing the game, or the in-world adventurer
  itself. `/dsroleplay on|off|status` controls this. This is what this session's investigation was
  about. The two axes are meant to be fully independent — Roleplay-mode dialogue can still come
  from either the LLM or the deterministic-template backend
  (`RoleplayExpressionRouter`), and the choice of backend is expression mode's decision, not
  perspective's.

## Current Lunaris lifecycle requirements

This plugin is a native Lunaris plugin (`[LunarisPlugin(...)]`/`[LunarisPermission(...)]`), not
BepInEx — migrated this session's predecessor work (parent commit `bc41870`). Standard lifecycle:
`Awake()` registers config and Harmony patches; `OnDestroy()` must fully unpatch Harmony and clear
any static/AppDomain-level event subscriptions (a prior fix in this migration specifically closed
a hot-reload event leak where `AppDomain.CurrentDomain.AssemblyLoad` was subscribed from a static
constructor with no unsubscribe — check for this anti-pattern anywhere new code adds a
process-wide event handler).

**Open question, not resolved this session**: a real live log showed every native-Lunaris mod in
the wider suite (not Deep Sims specifically — this is suite-wide) running its full `Awake()`
twice per process bootstrap with no `OnDestroy` between the two passes. See
`Erenshor-Mod-Suite/docs/CURRENT_WORK.md` for the full cross-suite evidence and reasoning (a
sibling repo, `ErenshorContracts`, now carries a temporary instance-identity diagnostic
specifically to resolve this). Deep Sims does not currently carry that diagnostic itself. If this
investigation later confirms two genuinely coexisting live instances, and if Deep Sims turns out
to be affected the same way (plausible, but not isolated/confirmed for this repo specifically),
the practical risk here would be double LLM requests, double social-budget consumption, or two
independent conversation-state machines racing — worth explicitly checking once the suite-wide
question is settled.

## Important files

- `src/PromptBuilder.cs` — all LLM system-prompt construction. `BuildSystemPrompt` is the shared
  core every other `Build*` method calls; the perspective branch lives here.
- `src/RoleplayPerspective.cs` — perspective state, the MMO/Roleplay identity-block contract, the
  chat-texture stripping regex, and (as of this session) `RoleplayOutputGuard`, the central
  post-generation content guard for Roleplay mode.
- `src/DeepSimsPlugin.cs` — the plugin entry point and by far the largest file; owns command
  parsing, the group-chat/whisper/autonomous emission paths, and (as of this session) the
  perspective-aware dispatch wrappers and diagnostic logging around each emission path.
- `src/SocialFoundation.cs` — `PartyReplyIntentClassifier` and related intent/topic
  classification. The subjective-vs-factual routing bug fixed this session lived here.
- `src/GroundingGuard` (grep for the class, spread across relevant files) — the mechanism that
  rejects LLM output making unverified factual/event claims. Distinct from
  `RoleplayOutputGuard` — grounding checks truth claims regardless of perspective;
  `RoleplayOutputGuard` checks perspective-appropriate *voice*, regardless of truth.
- `src/RoleplayDeterministicTests.cs` — the Roleplay-specific regression suite, runnable live via
  `/dsguardtest` (this session fixed it being written but never wired to any command).
- `AGENTS.md` — the architecture rules above, plus a large priority/roadmap section (P0-P2) that
  predates this session and reflects a different planning cycle; treat the roadmap section as
  historical context, not a current task queue, unless told otherwise.

## Known fragile paths

- **Perspective/expression orthogonality.** It's easy to accidentally couple them when adding a
  new emission path — always check both "does this respect `SocialPerspectiveState.RoleplayActive`"
  and "does this respect the configured expression mode" independently.
- **Fallback/guard call sites are scattered by necessity** (group reply, group retry, whisper,
  `/dstalk`, autonomous, verified-event reply, template fallback all have their own code path)
  which is exactly why this session's central `RoleplayOutputGuard` still needed per-call-site
  wiring rather than being a single interception point. A new emission path added later needs to
  remember to call it — there is no compiler-enforced guarantee here. If you're auditing for
  regressions, grep for every `WriteChat(` call that shows Sim dialogue and confirm each one's
  candidate string passed through the guard while Roleplay is active.
- **The wiki/wiki-lookup path and the native-identity path are separate signal sources that need
  explicit cross-referencing** (this session's identity-cross-reference fix) — a wiki lookup
  answers "what is a Windblade," never "is this Sim a Windblade." Any new content path that
  touches class/identity facts needs to keep sourcing that from verified native Sim state
  (`SimSnapshot.ClassName`, confirmed this session to be sourced only from native `CharacterClass`
  reflection in `src/SimContextReader.cs`, never inferred from a name or a wiki result) rather
  than assuming a wiki result implies identity.

## What NOT to redesign

- Do not merge PR #2 without an explicit instruction to do so at that moment.
- Do not broadly rewrite Deep Sims to "improve" Roleplay quality by making output more
  flowery/theatrical — the user has been explicit twice this session that Roleplay ≠
  Shakespeare/fantasy theatrics; it means perspective/identity (MMO player vs. in-world
  adventurer), and correct style stays plain, modern, and short.
- Do not touch PvP combat/matchmaking from this repo (Deep Sims' PvP integration is
  read/observe-only by design — see `AGENTS.md` section 5-adjacent co-op/integration sections).
- Do not add a defensive duplicate-plugin-instance guard here (or anywhere in the suite) until the
  cross-suite investigation referenced above is actually resolved by a live run.

## Current live evidence (this session)

See `docs/CURRENT_WORK.md` for the full trail, including the two rounds of Roleplay
investigation, the exact bad lines observed, the exact fixes, and the exact diagnostic field
names now in the code (`roleplayGuardRan`/`roleplayGuardChanged`/`roleplayGuardRejected`). Short
version: Ollama connectivity, group chat, and grounding-rejection/retry all confirmed live-working;
the Roleplay-specific fixes described there are build/test-verified only, not yet live-retested.

## Questions still unanswered

See `docs/CURRENT_WORK.md`'s "Known open questions" and "Next deep audit questions" sections —
not duplicated here to avoid the two docs drifting apart. Read that file for the current list.
