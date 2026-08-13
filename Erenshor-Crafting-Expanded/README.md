# Erenshor Crafting Expanded

A horizontal-progression expansion to Erenshor's native crafting: forge quality-of-life, Smithing
progression, a crafting-commission proof-of-concept, and a new Foraging gathering system built on
mod-owned nodes (vanilla Mining and Fishing are untouched). See `docs/` for the full native-API
research this mod is built against, and the top-level implementation plan for scope boundaries.

## Status: Experimental / development (0.1.1)

- **Compiles cleanly and passes its full deterministic test suite** against the actual installed
  Erenshor/BepInEx assemblies (`BUILD.ps1` / `tests\RUN_TESTS.ps1`, 10/10 pure-logic test groups
  pass). Every Harmony patch target (`Smithing.Combine`, `Smithing.DoSuccess`,
  `ItemDatabase.Start`, `TypeText.CheckCommands`, `PlayerControl.LeftClick`,
  `csMouseOrbit.LateUpdate`) and the native member assumptions this mod relies on
  (`ItemDatabase.itemDict`/`ItemDB`/`ItemDBList`, `Smithing.Template`/`Components`/`FuelSource`,
  `Item.TemplateIngredients`/`TemplateRewards`, `ItemIcon.QuickSmith`, `PlayerControl.myTransform`,
  `GameData.PlayerTyping`) have been independently re-verified against the currently installed
  game assembly. This is a static/compile-time verification pass, not a live-game one.
- **Partially runtime verified.** Wild Herb custom-item registration, native inventory grant,
  stacking, zone leave/return, a full game exit/restart with the same character, and the
  `/craftdiag` inventory count have all been confirmed live by the human tester. See
  `docs/NATIVE_ITEM_REGISTRY_FINDINGS.md` for the evidence matrix. Bank/vendor/trade/Auction
  House behavior remains unverified and is not claimed safe.
- **Smithing stack QoL and the Foraging scanner rewrite still need a live regression pass.** The
  code keeps native `Smithing.Combine()` authoritative, captures the recipe before `DoSuccess()`
  and awards progression only in the postfix after native success, and wires stack QoL to the
  normal Craft/Combine path by repeatedly invoking native `ItemIcon.QuickSmith()` only for the
  exact missing generic recipe ingredients (the three known special-combine template IDs are
  excluded from auto-fill). None of this has been exercised in a running game yet — see
  `docs/FIRST_RUNTIME_TEST.md` for the checklist.
- **Foraging visual placement is still intentionally unauthored.** No real plant asset or fixed
  world coordinate is shipped yet; `EnablePoCNode = false` by default and invalid placeholder
  definitions are rejected. The rewritten scanner (`src/Foraging/ForagingScanPolicy.cs`) now
  excludes the local player hierarchy, other actor-owned renderers, other loaded Unity scenes,
  Crafting Expanded's own clones, effect renderers, and non-cloneable meshes before ranking
  environmental candidates, and source lookup is scene-bounded — but it still needs a live re-scan
  before a real node is authored.
- **Smithing progression is currently profile-wide**, stored in the mod's own
  `smithing-progress.json`. A stable local-player character identity has not yet been proven from
  the current game assembly, so the mod deliberately does not invent a per-character key. This
  is a known pre-release research item.
- **Crafting commissions remain an experimental PoC and are disabled by default.** Their current
  Sim runtime key is scene-local, not a persistent identity; active requests are invalidated on
  zoning. The final commission system still needs a verified recipe catalog and persistent Sim
  identity contract.
- Use `INSTALL_TEST.ps1` for a reversible test install. It records target-bound backup metadata
  so `REMOVE_TEST.ps1` cannot restore a backup belonging to another profile. The one-shot
  `BUILD_AND_INSTALL.ps1` intentionally has no backup and is not the preferred development path.

## Installation

This is a BepInEx 5 plugin. Requires BepInEx installed and Erenshor already launched modded once
(so a BepInEx profile exists).

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD.ps1
powershell -ExecutionPolicy Bypass -File .\INSTALL_TEST.ps1
```

`BUILD.ps1` auto-detects your Erenshor install and BepInEx reference root and compiles to this
mod's own `bin\`; it never installs anything. `INSTALL_TEST.ps1` performs the actual install and
records a target-bound, timestamped backup so `REMOVE_TEST.ps1` can cleanly restore whatever was
there before. `BUILD_AND_INSTALL.ps1` is a one-shot alternative with no backup — prefer the
test/remove pair above during development.

## Configuration

Settings live in the generated BepInEx config file after first launch.

| Section | Key | Default | What it does |
|---|---|---|---|
| General | EnableMod | true | Master switch. When false, no Harmony behavior beyond native crafting runs. |
| Crafting | CraftHotkey | None (unbound) | Key that performs one Craft action while the forge window is open and chat isn't focused. |
| Commissions | EnableCraftingRequests | false | Experimental PoC: let local Sims offer a single crafting commission. |
| UI | ShowCraftingToggle | true | Show the small Crafting toggle button when crafting context is relevant. |
| UI | PersistWindowPosition | true | Remember the Crafting panel's dragged position between sessions. |
| Foraging | EnableForaging | true | Enable the Foraging subsystem (registry, diagnostics). Does not by itself spawn a node. |
| Foraging | EnablePoCNode | false | Spawn the authored node once it has real survey-verified position/visual data. Leave off. |
| Foraging | ForageKey | G | Gather an in-range, available Foraging node. |
| Foraging | ForagingInteractionRange | 3.5 | Max distance (world units) to gather a node. Mod-owned value; no native constant found. |
| Foraging.Dev | ForagingScanRadius | 12 | Radius `/craftdiag forage scan` searches. Development diagnostic only. |
| Foraging.Dev | ForagingDebugRespawnSeconds | 0 | If > 0, overrides node respawn time for fast iteration testing. Leave 0 for normal play. |
| Foraging.Dev | AllowDebugPlaceholderVisual | false | Development-only placeholder-sphere fallback. Leave off. |

## Commands

- `/craftdiag` — concise diagnostic summary (mod state, persistence errors, forage survey pointer).
- `/craftdiag giveherb` — development helper to grant a Wild Herb for testing.
- `/craftdiag forage pos` — reports the player's current position/scene for authoring a node location.
- `/craftdiag forage scan [filter]` — scans nearby renderers as forage-node visual-source candidates; see `docs/FORAGING_ASSET_SURVEY.md`.

## Compatibility

- **Required:** BepInEx 5, a matching installed Erenshor build (the mod compiles against and
  disassembles the currently installed `Assembly-CSharp.dll` — see `docs/` for the exact findings).
- **Optional integration:** none currently. Adapts UI/input-protection *code patterns* from this
  author's own `Erenshor-PvP` (see Acknowledgements) but has no runtime dependency on it or any
  other mod in the suite.
- Vanilla Mining and Fishing are untouched by design; this mod only adds a new, separate Foraging
  system.

## Known limitations

See the Status section above for the full detail. In short: Bank/vendor/trade/Auction House
interaction with custom items is unverified; Foraging has no authored node location yet; Smithing
progression is profile-wide, not per-character; crafting commissions are an off-by-default PoC.

## Troubleshooting

- If the mod doesn't load: verify BepInEx itself loads, verify `ErenshorCraftingExpanded.dll` is
  under the active profile's `BepInEx\plugins\`, and check the BepInEx console/log for a Harmony
  patch-target error — a missing target fails closed with a log line rather than silently running
  partial features.
- Rebuild against the current `Assembly-CSharp.dll` after any Erenshor update; native method/field
  shapes are re-verified each pass but can change with a patch.
- Run `/craftdiag` for a quick state summary, including any progression `persistenceError`.

## Development / build information

This project has been developed with substantial AI-assisted coding, guided through design,
testing, playtesting, and audits against the actual installed game assembly. Bug reports, code
review, corrections, and contributions from experienced Erenshor modders are welcome.

Build/test procedure and architecture boundaries for contributors (human or AI) are documented in
[`AGENTS.md`](AGENTS.md).

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by
the game's developer.

## License

Apache License 2.0 — see [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE).

## Acknowledgements

Crafting Expanded builds on research and examples from the Erenshor modding community.

- **Arcanism — cammaron**
  Referenced as an existing, currently-maintained Erenshor implementation of custom item
  creation and `ItemDatabase` registration. No source was copied (no license file is published
  in that repository); the registration technique was independently reimplemented against this
  build's own disassembled `Assembly-CSharp.dll`.

- **Erenshor-PvP — forgetwhtuno**
  This mod's own sibling project (same author, Apache-2.0-licensed, independent checkout in this
  development environment). Its draggable/toggleable IMGUI panel and world-input-passthrough
  prevention (`PlayerControl.LeftClick` / `csMouseOrbit.LateUpdate` Harmony prefixes) were
  adapted for Crafting Expanded's own panel.

- **Erenshor-Party-Tools — forgetwhtuno**
  Same author/license as above. Established the panel offset-anchoring convention `Erenshor-PvP`
  (and in turn this mod) follows, and its `RUN_TESTS.ps1` shape is the template for this mod's
  own pure-logic test runner.

Detailed technical attribution, including exactly which files were inspected and what was
copied versus independently reimplemented, is in [`docs/ATTRIBUTION.md`](docs/ATTRIBUTION.md).

No endorsement by any credited project or author is implied.
