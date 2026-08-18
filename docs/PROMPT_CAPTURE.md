# Deep Sims local prompt capture (developer diagnostic)

Local-only instrumentation that records the exact LLM inference packets Deep Sims produces during
normal play, so alternative prompt designs can be compared offline against real traffic.

**This is a developer tool. It is OFF by default and must be turned on explicitly.**

Captured packets contain the real conversation and the complete prompt, because that is their whole
purpose. They never leave this machine, are never committed, and are never packaged.

## What it does not do

Instrumentation only. This workstream deliberately changed **no** prompting behavior: PromptBuilder,
system instructions, history roles, retrieval, classifier rules, classifier temperature, model
choice, reasoning routing, GroundingGuard, RoleplayGuard, memory selection, `num_ctx`, `temperature`
and `num_predict` are all untouched. The point is an unmodified production baseline.

## Enabling and disabling

In game (per session, does not persist):

```
/dspromptcapture on
/dspromptcapture status
/dspromptcapture off
```

Optionally label the next captured turn so a specimen is easy to find later:

```
/dspromptcapture mark windblade-opinion
```

Or persistently, in the Deep Sims config under the **Diagnostics** section:

| Setting | Default | Meaning |
| --- | --- | --- |
| `PromptCaptureEnabled` | `false` | Master switch. Leave off for ordinary play. |
| `PromptCaptureMaxFiles` | `100` | Maximum logical LLM requests per session. |
| `PromptCaptureIncludeClassifier` | `true` | Also capture the semantic classifier as its own linked packet. |

There is deliberately **no hotkey**.

## Where packets go

Relative to the Deep Sims data root (`<Erenshor>/plugins/config/DeepSims`):

```
DeepSims/
  Diagnostics/
    PromptCapture/
      session-<safe-id>/
        index.jsonl
        000001-request.json       <- semantic packet (what the builder produced)
        000001-request-raw.json   <- exact packet(s) sent to Ollama
        000001-result.json        <- what came back, and what the player saw
```

The session id is derived from UTC time only — never from character name, Steam id, or machine name.
The directory is created only when capture is actually switched on.

`/dspromptcapture status` reports a **relative** label only; absolute paths are never printed.

## Reading a capture

Start from `index.jsonl` — one compact line per logical request:

```json
{"requestId":43,"turnId":42,"stage":"direct_party_reply","source":"player_reply","speaker":"Dancer",
 "route":"PersonalPreference","knowledgeNeed":"None","retrievalUsed":false,"model":"qwen3.5:4b",
 "attempts":1,"grounding":"accepted","displayed":true,"interestingCases":["opinion_knowledge_override"],
 "requestFile":"000043-request.json","resultFile":"000043-result.json"}
```

Request and result files pair by the zero-padded request id: `000043-*`.

### The three files

**`-request.json` — semantic packet.** The structured state that produced the prompt: raw classifier
result vs effective route vs the deterministic corrections that fired; the bounded world/live-party
values that actually became prompt text; the session summary; only the memory and SoftPersona
selected for this prompt (plus candidate counts, never the rejected candidates); only the bounded
retrieval evidence handed to the model; the conversation thread; seed and Sim-to-Sim linkage; and the
resolved generation settings with the final `messages[]`.

**`-request-raw.json` — exact HTTP packet.** For every real HTTP call, the serialized body captured
immediately before submission, stored as a nested JSON object (not an escaped string) so it can be
replayed directly. Answers "what was actually sent", as distinct from "what the builder made".

**`-result.json` — outcome.** Per-attempt Ollama response and timings, then three deliberately
separate texts:

- `rawModelContent` — what the model said
- `postGuardContent` — after the roleplay guard
- `final.visibleText` — what the player actually saw

plus the grounding decision, its verbatim reason, and a stable `reasonCategory`.

### Retries and attempts

The retry ladder stays under **one logical request id**. Attempt kinds mirror the real branches in
`OllamaClient`: `primary`, `post_load_retry`, `expanded_budget`, `flattened_fallback`. The flattened
compatibility prompt is captured exactly as sent, which makes it directly usable as one of the
prompt architectures to test later.

### Classifier packets

When `PromptCaptureIncludeClassifier` is on, the classifier call is its own packet with
`stage=semantic_classifier`, recording the classifier `messages[]`, the exact request, the raw parsed
classification, the effective route, and which deterministic corrections changed it. The correction
names are the real ones from `SemanticTurnRouter`: `ApplyMeaningOverride` and `ApplyNoRetrievalRule`.

This is what lets you compare, for example, raw `TurnType=Opinion / KnowledgeNeed=GameWiki` against
effective `KnowledgeNeed=None`.

### Sim-to-Sim

A connected turn records `parentRequestId`, `conversationTurnIndex`, `previousSpeaker`,
`previousRawCandidate`, and `previousAcceptedVisibleText`. B is tied to the **accepted visible** line
— if A's model candidate was rejected and a deterministic opener became visible, B's
`previousAcceptedVisibleText` is that opener, while A's rejected candidate is still preserved in A's
own packet.

### Interesting-case tags

Diagnostic labels derived after the fact (they never change behavior): `opinion_with_retrieval`,
`opinion_knowledge_override`, `other_sim_preference`, `grounding_reject_topic_mismatch`,
`grounding_reject_loot_acquisition`, `grounding_reject_entity_relationship`,
`grounding_reject_kill_clear`, `connected_sim_banter`, `accepted_retrieval_answer`.

## Privacy

Normal logs get metadata only, never prompt text:

```
PromptCapture: request=17 source=player_reply speakerHash=1f2e3d4c route=Opinion messages=5 chars=1832 result=accepted
```

Packet files may contain the test conversation and full prompt. They must not contain — and the
writer redacts — absolute Windows/Unix user paths, `Authorization`/`Bearer`/api-key/token shapes.
Endpoints are recorded as a *kind* (`ollama_chat`, `ollama_chat_remote`) rather than a URL. No HTTP
headers, Steam ids, hostnames, usernames, save paths, save contents, unrelated config, bulk memory
files, or environment variables are ever recorded. Whole runtime objects are never serialized: only
values already copied at the request boundary are captured.

The external-news API key is never touched by capture; only the Ollama loopback packet is recorded.

## Safety and cost

- **Never breaks generation.** Every capture entry point is wrapped; a failure logs only the
  exception type plus the request id (`Prompt capture write failed request=43 error=IOException`) and
  the reply pipeline continues untouched.
- **Bounded.** At `PromptCaptureMaxFiles`, capture *stops and warns once* rather than deleting
  collected evidence. Already-captured packets are kept.
- **Off the main thread.** Values are copied at the request boundary; serialization and file writes
  happen on the thread pool, so no live mutable Unity object is touched from a background thread.

## Excluded from publication

`.gitignore` excludes the Diagnostics tree, and the suite's release packaging uses a strict file
allowlist (DLL + `INSTALL.md` + named docs), so capture output is structurally unpackageable. A guard
in `tests/RUN_DETERMINISTIC_TESTS.ps1` asserts both.
