# Current Work — Deep Sims

This file is meant to be handed to another AI (or a human) for a deeper audit. It records what is
actually confirmed versus what is only build/test-verified. Do not upgrade anything here to
"confirmed working" without a real live-game observation backing it.

## Current branch / SHA

- Branch: `agent/lunaris-native-deepsims`
- HEAD: `f75a54aba91400484315bb0d464b61830fe51415` — "Roleplay: central output guard,
  subjective-question routing, verified-identity cross reference"
- Open PR: [#2](https://github.com/forgetwhtuno/DeepSim-erenshor/pull/2) against `main`, **draft,
  unmerged**. Do not merge without an explicit instruction to do so in the moment.
- Parent commit `bc41870` — "Migrate Deep Sims to a native Lunaris plugin" (the BepInEx-to-Lunaris
  migration itself).

## Current live state

Confirmed working by direct observation of a real Lunaris session log this session:

- Native Lunaris load succeeds; the plugin's own "loaded" message appears (see the suite-wide
  duplicate-Awake question below — this message appeared *twice* per bootstrap, like every other
  native-Lunaris mod in the suite, not something specific to Deep Sims).
- Ollama connectivity works — real chat requests are sent and real replies come back
  (`qwen3.5:2b`/`qwen3.5:4b` observed in the log).
- Normal whole-party group conversation works: multiple Sims (Dancer, Phanty, Cyndara observed)
  produced generated lines, grounding rejected at least one ungrounded line correctly
  ("Rejected ungrounded group line from Phanty: unsupported loot/acquisition assertion"), and a
  retry-on-reject flow was observed working (a rejected first draft triggered a second Ollama
  call that then passed grounding).
- PvP integration: PvP's own encounter/combat had a good live result in the same session (see
  `Erenshor-Mod-Suite/docs/CURRENT_WORK.md`'s PvP entry) — Deep Sims' PvP-adjacent optional
  integration status specifically has not been isolated/confirmed beyond "nothing visibly broke."
- Follow integration status: not isolated/confirmed this session either way.

Not yet observed this session (absence of evidence, not evidence of absence):
- A clean unload/reload cycle.
- Multi-character (character-switch) behavior for anything Deep-Sims-owned.
- Sustained-session performance beyond the frame-hitch lines noted below.

## Current Roleplay investigation

This is the part most likely to need a deeper audit. Recorded here exactly, including the false
starts, so a reviewer can see the actual reasoning trail rather than just the final claim.

### Round 1 — `/dsroleplay on` appeared to do nothing

Live symptom: turning Roleplay on did not visibly change dialogue. Example observed line:
`"Dancer tells the group: i don't know that one heh"` after being asked about being a Windblade.

Investigation ruled out, with reasoning, before finding the real cause:
- **State round-trip**: `SocialPerspective.Parse`/`Describe` correctly round-trip `"Roleplay"` to
  the enum and back — verified by direct code read, not the bug.
- **Config sync**: `SyncSocialPerspectiveFromConfig()` reads the same in-memory
  `_settings.SocialPerspective` field the `/dsroleplay` command handler just wrote — not a
  disk-reread race, not the bug.
- **Main prompt path**: `PromptBuilder.BuildSystemPrompt` does branch correctly on
  `SocialPerspectiveState.RoleplayActive` and builds an entirely different identity block via
  `RoleplayPromptContract.BuildIdentityBlock` when active. This was confirmed **directly against a
  real Ollama request body captured in the live log** — the actual system prompt sent to the model
  said (verbatim, from the log): *"You are Phanty, the adventurer this Erenshor character is...
  Erenshor is your world, not a game... You are not a player controlling a character."* This part
  of the system was never broken.

Actual root cause found: `GroundPartyLineAsync`'s post-generation grounding-rejection fallback
called `SocialTemplates.RenderUnknownFactReply` — a hardcoded MMO-flavored filler template with
zero perspective awareness — whenever an LLM reply failed grounding. This fires routinely on
subjective questions (grounding has nothing to check an opinion against), so a live player asking
almost any opinion question would silently get the MMO-perspective fallback regardless of the
Roleplay-aware prompt that had just been sent. Fixed by adding `RoleplayFallback` (in
`src/RoleplayPerspective.cs`) and routing every fallback call site in `src/DeepSimsPlugin.cs`
through perspective-aware dispatch wrappers. Also found and fixed in the same pass: the existing
`RoleplayDeterministicTests` file existed but was never wired into any runnable command — now
runs via `/dsguardtest`.

**This round was build/test-verified only; not live-retested before Round 2's evidence arrived.**

### Round 2 — guard wasn't actually protecting output

A further live pass (after Round 1's fix was installed) produced a diagnostic log line per
generated message: `perspective=Roleplay|MMO expression=LLM|Template source=... 
roleplayPromptApplied=True|False roleplayGuardApplied=True|False`. Live evidence:

```
perspective=Roleplay roleplayPromptApplied=True roleplayGuardApplied=False
```

repeated on several ACCEPTED, player-visible lines that clearly contained MMO/internet-chat
texture Roleplay is supposed to prevent:

- `"nice to see you online again"`
- `"are your eyes painted on or playing NES? lmao"`
- `"heh yooo Brinon! aloha"`
- `"lmao maybe we're just too quiet to hear our own footsteps? :D"`
- `"Hey pal, nice to see you online again! Hit me up if you wanna hang."`
- `"It's quiet in Hidden... heh :D just peace for now lol"`

Root cause: `roleplayGuardApplied=False` wasn't ambiguous logging noise, it was accurate — no
roleplay-specific content guard ran *at all* on the group-reply, whisper,
vanilla-Sim-continuation, verified-event, or conversation-continuation paths. Only the narrow
ambient/autonomous path (`ApplyRoleplayAutonomousGuard`) ran any check, and even that guard's
vocabulary never covered plain chat texture like "lol"/"heh"/":D"/"online" — it was built to catch
stage-direction/self-narration, a different problem.

Fix: one central `RoleplayOutputGuard.Enforce(candidate, speakerName, out changed, out rejected)`
in `src/RoleplayPerspective.cs`. Two tiers: texture (the existing `ChatTexture` regex, extended)
is stripped in place so the sentence survives; core out-of-world vocabulary (`game`, `server`,
`session`, `online`, `dps`, `hit me up`, etc. — see `RoleplayOutputGuard.RejectCoreWords`/
`RejectCorePhrases`, kept as data so the list is easy to extend) is treated as unfixable and
rejects the whole candidate, since deleting a word can't fix a sentence whose entire *claim* is
out-of-world. Wired into `QueueGroupMessage` (the single funnel every group-visible line passes
through regardless of producer), the whisper display block, and the final display boundary as a
safety net.

Also found and fixed in the same pass: `PartyReplyIntentClassifier.Classify` checked the generic
factual-lookup heuristic *before* the existing "what do you think"/opinion check, so
`"dancer what do you think about being a windblade?"` (mentions a class name + a question word)
tripped the factual-lookup branch and got routed into wiki-relationship grounding an opinion can
never satisfy — collapsing to the unknown-fact fallback (`"No idea, honestly."`) even once
identity was verified. Fixed by reordering the existing checks, not adding new logic. Also added
an explicit verified-class-vs-asked-class cross-reference line to the prompt's identity block, so
the model is told authoritatively whether the speaker actually is the class being asked about
rather than only being shown a wiki definition of the class in the abstract.

**`git diff --check`, privacy scan, and the full deterministic test suite (222 pre-existing + all
new regression cases covering the exact lines above) all pass. This is still only build/test
evidence.**

### DO NOT claim this works in-game until it is live retested.

Nobody has run the game with the Round 2 fix installed as of this writing. The diagnostic log
lines described above should make the next live pass conclusive either way.

## Known open questions

- **Final Roleplay output quality after the new central guard** — not yet observed live.
- **Verified Sim identity/class grounding** — the fix is in; whether it produces good subjective
  answers in practice (not just "doesn't crash and isn't obviously wrong") needs a real pass.
- **Template/LLM parity** — whether the deterministic-template Roleplay backend
  (`RoleplayExpressionRouter`, used when the LLM path is unavailable/disabled) produces output of
  comparable quality to the now-guarded LLM path. Not compared side by side this session.
- **Unload/reload pending-request behavior** — not exercised this session.
- **Duplicate Lunaris plugin instance** — see
  `Erenshor-Mod-Suite/docs/CURRENT_WORK.md` for the full cross-suite evidence. Deep Sims is one of
  the affected mods (its own "loaded" line also appeared twice in the same log with no
  `OnDestroy` between), but Deep Sims does not currently carry the instance-identity diagnostic
  that `ErenshorContracts` has — if this needs isolating specifically for Deep Sims (e.g. to check
  whether a duplicate instance could cause double LLM requests/double social-budget consumption),
  that diagnostic would need to be added here too. Not done yet.
- **Performance/frame hitches** — several `[DeepSims Perf] frame hitch` lines were observed in the
  log (155ms-3685ms, at various points including right after plugin bootstrap and during a PvP
  encounter). Not investigated this session; unclear whether these are Deep-Sims-caused, another
  mod, or general Unity/Lunaris bootstrap cost. Worth a deeper look if hitching is a live
  complaint.
- **Suite UI integration plan** — Deep Sims is slated for a Hub tab in Phase 2 of the suite-wide
  UI work (see `Erenshor-Mod-Suite/docs/UI_DESIGN.md`/`docs/CURRENT_WORK.md`). Not started. The
  intended field list for that tab (Ollama/model status, social mode, activity preset,
  roleplay mode, current party members, memory/session status) is recorded there, not duplicated
  here.

## Next live tests (exact commands)

1. `/dsroleplay on`
2. `/dsroleplay status` — expect `perspective=Roleplay`.
3. Group chat: `"dancer what do you think about being a windblade?"` (or whichever Sim's verified
   class you want to test against) — expect an in-world, class-grounded opinion (something like
   *"It's suited me fine, I like getting in close"* if the verified class matches what was asked;
   a natural correction if it doesn't; "I don't know" only if the class genuinely can't be read,
   which should be rare) — NOT `"No idea, honestly."`, NOT MMO-flavored filler with "lol"/"online"/
   emoticons.
4. `/dstalk Dancer what do you think about being a windblade?` (or the equivalent unprompted-line
   command) for the same check via a different path.
5. `/dsroleplay off`, repeat steps 3-4, confirm dialogue reverts to ordinary MMO-player framing.
6. Watch the Lunaris log for the `RoleplayDiag` diagnostic lines throughout — confirmed present in
   `src/DeepSimsPlugin.cs` as `roleplayGuardRan=True|False roleplayGuardChanged=True|False
   roleplayGuardRejected=True|False`, replacing the old single ambiguous `roleplayGuardApplied`
   field. `roleplayGuardRan` should now read `True` on every Roleplay-mode emission (group,
   whisper, dstalk, autonomous) once this fix is live; `roleplayGuardChanged`/`Rejected` show
   whether that run actually altered or rejected the candidate.
7. Separately: run `/dsguardtest` to execute the wired-in deterministic Roleplay regression suite
   live and confirm it reports all-pass in the actual running game, not just in isolation.

## Next deep audit questions

- Is `RoleplayOutputGuard`'s reject list (`game`, `server`, `session`, `online`, `dps`, `player`,
  etc.) too aggressive for legitimate in-world dialogue that happens to need one of those words in
  a non-meta sense? Word-boundary matching was used deliberately to reduce false positives, but
  this hasn't been stress-tested against a large volume of real generated dialogue.
- Does the texture-stripping half of the guard (as opposed to the full-reject half) ever produce
  an awkward or grammatically broken sentence when it removes a trailing "lol"/":D"? Worth
  sampling real output.
- Is there a cleaner architectural place for perspective-aware output enforcement than dispatch
  wrappers scattered at each call site — e.g. could `QueueGroupMessage` alone be made the single
  mandatory gate for literally every group-visible line, removing the need for the whisper/other
  call-site-specific wiring? (Whisper doesn't currently route through `QueueGroupMessage` for
  structural reasons — worth understanding whether that's a real architectural boundary or
  incidental.)
- The `PartyReplyIntentClassifier` reorder fixed one specific misclassification
  ("what do you think about being a windblade") — are there other phrasings of the same
  subjective-about-a-verified-fact pattern that still misclassify?
