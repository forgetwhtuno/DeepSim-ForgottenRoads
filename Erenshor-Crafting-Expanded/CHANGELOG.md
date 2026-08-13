# Changelog

## 0.1.1 - Source review pass

- Foraging scanner rewritten to exclude the local player hierarchy, other actor-owned renderers
  (`Character`/`NPC`/`SimPlayer`/`PlayerControl`), renderers outside the player's current loaded
  Unity scene, the mod's own clones, and particle/trail/line renderers; requires
  `MeshRenderer + MeshFilter + sharedMesh`; ranks small/medium environmental geometry ahead of
  giant world meshes; records rejection counts and per-candidate shader/material evidence.
  New pure-logic helper: `src/Foraging/ForagingScanPolicy.cs`.
- Visual-source resolution for forage nodes is now scene-bounded and fails closed on an
  ambiguous or cross-scene hierarchy path instead of using an unscoped `GameObject.Find`.
- Forage node hardening: single nearest-node gather input, `GameData.PlayerTyping` suppresses
  gather input, reward is granted before depletion, per-node tint via `MaterialPropertyBlock`
  only (never edits `sharedMaterial`), clone recursion capped by total transform count, and
  node/config values reject NaN/Infinity/invalid ranges.
- `ForgeStackQolPatch` now actually wires stack quality-of-life into `Smithing.Combine()`:
  moves only missing generic component units via repeated native `ItemIcon.QuickSmith()` calls,
  refuses to move anything when the forge already holds an unrelated or excess component, and
  excludes the three known special-combine template IDs (`31377423`, `2298018`, `2265228`) from
  auto-fill. Movement is detected from before/after slot state rather than assumed from a bool
  return.
- `CraftSuccessPatch` captures the recipe in a `Smithing.DoSuccess()` prefix and awards
  progression only in the postfix, after native success; `Smithing.Combine()` remains
  authoritative.
- Custom item registration hardened: a runtime ownership marker makes repeated
  registration/hot-reload more idempotent, database insertion is transactional/rollback-capable,
  a failed clone insertion destroys the orphan clone, and multi-quantity grants fail closed
  rather than partially granting and reporting success.
- Progression sidecar I/O now exposes a readable `LastError` instead of silently swallowing
  failures; `/craftdiag` reports it when present.
- Crafting commissions (disabled by default): the same recipe is no longer immediately
  re-offered after decline/completion until the forge reopens; the current Sim identity field is
  explicitly named `RuntimeKey` (scene-local, not persistent) rather than implying a stable ID.
- Partial Harmony `PatchAll` failure now unpatches itself and leaves the affected feature
  fail-closed instead of running with a partial patch set.
- UI: unpinned panel state now hides when its context ends (pin remains meaningful), and pointer
  protection covers the small Crafting toggle in addition to the main panel.
- Build/test scripts hardened: `BUILD.ps1` refuses an ambiguous BepInEx reference root instead of
  guessing; `INSTALL_TEST.ps1` records a target-bound, millisecond-timestamped backup session;
  `REMOVE_TEST.ps1` restores only the backup belonging to the exact selected target and marks a
  restored session so it cannot be replayed; `BUILD_AND_INSTALL.ps1` now delegates compilation to
  `BUILD.ps1`.
- Fixed a build break in this pass: `CraftingProgressionStore.LastError` used a C# 6
  auto-property initializer, which the project's legacy `csc.exe` toolchain (effectively C# 5)
  rejects; replaced with an explicit backing field.

## 0.1.0 - Initial development preview

- Forge quality-of-life groundwork, Smithing progression sidecar, a crafting-commission
  proof-of-concept, and an initial mod-owned Foraging system (vanilla Mining and Fishing
  untouched).
- Wild Herb custom item: registration, native inventory grant, stacking, zone leave/return, and a
  full game exit/restart with the same character — confirmed live by the human tester.
- `/craftdiag` diagnostic command.
