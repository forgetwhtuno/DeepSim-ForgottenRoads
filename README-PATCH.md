# Deep Sims 0.7.x social foundation patch

Target: `forgetwhtuno/DeepSim-erenshor`, branch `codex/factual-grounding-telemetry` (0.7.x development line).

## Apply

Copy this folder anywhere, open PowerShell in the root of your local `DeepSim-erenshor` checkout, then run:

```powershell
powershell -ExecutionPolicy Bypass -File <path-to-this-folder>\apply-social-foundation-0.7.ps1
```

The script validates all expected source anchors in memory before it writes any repository files. It then runs the repository's deterministic test suite unless `-SkipTests` is supplied.

After applying, run the normal game-linked build from the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1
```

## Intended behavior

- `/dssocial auto|llm|templates|off`
- `/dssocial quiet|normal|lively|status`
- one central autonomous budget for global/per-Sim/type cooldowns, recent semantic/message duplicate suppression, rolling message budget, player conversation pressure, combat state, priority, and social-moment ownership
- Auto uses deterministic templates for ritual events and after Ollama has entered its failure cooldown
- Templates makes no Ollama request for supported social expressions
- Off blocks autonomous chatter
- completed friendly-duel reactions are current-party spectator-only, exclude named duel participants, and are capped to one post-duel line
- COOP autonomous social authority remains host-only; no new network protocol or broad same-zone broadcast is introduced

## Deliberate limitation

Pre/during Practice Duel spectator chatter is not enabled in this patch. The inspected integration only establishes `ErenshorDuel.DuelController.Active`; it does not provide a verified participant identity contract. Without that, Deep Sims cannot safely exclude the duelists from the spectator pool. The completed `friendly_duel` event is already verified and can safely drive a post-duel reaction.

## Validation status in this environment

The patch was constructed against the exact 0.7.0 development sources read through the connected GitHub repository. This execution environment has no usable C#/.NET compiler and cannot reach GitHub from the local shell, so the repository's Windows `csc.exe` regression harness and the game-linked build could not be executed here. The apply script runs the deterministic suite on your Windows checkout and fails on the first regression.

In-game validation still needed:
- Auto after a real Ollama failure
- Templates mode under normal party chat and event load
- COOP host/client authority behavior
- one-line completed Practice Duel spectator reaction and participant exclusion
- native chat formatting/typing delay after deterministic templates
