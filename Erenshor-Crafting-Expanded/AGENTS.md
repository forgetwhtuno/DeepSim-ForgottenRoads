# AGENTS.md — Erenshor Crafting Expanded

Instructions for AI/coding agents working in this repository. Read this before making changes.

## What this mod is

A horizontal-progression expansion to Erenshor's native crafting (BepInEx 5 plugin, .NET Framework 4.8, C# 5 effective language level via `csc`): forge quality-of-life, a Smithing progression sidecar, an experimental crafting-commission proof-of-concept (disabled by default), and a new mod-owned Foraging gathering system. Vanilla Mining and Fishing are intentionally untouched.

**Status is experimental**, not a finished release — read `README.md`'s status section and `docs/FIRST_RUNTIME_TEST.md` before claiming anything works. Foraging visual placement is intentionally unauthored (`EnablePoCNode = false` by default); Smithing progression is deliberately profile-wide, not per-character, because no stable local-player character identity has been proven from the current game assembly.

## Core design boundary

- `Smithing.Combine()` remains the authoritative native crafting method at all times. `ForgeStackQolPatch` only *moves component units into the forge* via repeated native `ItemIcon.QuickSmith()` calls for the exact missing generic ingredients — it never bypasses or duplicates native combine/reward logic, and it explicitly excludes the three known special-combine template IDs (`31377423`, `2298018`, `2265228`) from auto-fill.
- `CraftSuccessPatch` captures the recipe in a `Smithing.DoSuccess()` **prefix** and awards progression only in the **postfix**, after native success confirms. Never award progression from a prefix or from `Combine()`.
- Custom item registration (`src/Compatibility/GameItemRegistryApi.cs`) checks for id collisions before insertion and never overwrites an existing item; `ItemDB`/`ItemDBList` are kept in the same resize-and-rebuild relationship the native game uses (`ItemDBList` is a derived view, not independent storage).
- A partial Harmony `PatchAll` failure must unpatch itself and leave the affected feature fail-closed — never run with a silently partial patch set.
- Foraging nodes are mod-owned clones, never edits to native world objects. Tint uses `MaterialPropertyBlock` only, never `sharedMaterial`. Visual-source resolution is scene-bounded and must fail closed on an ambiguous or cross-scene hierarchy path.

## Forbidden

- Do not invent Erenshor/Unity API members or signatures. This mod is built entirely from IL disassembly of the actual installed `Assembly-CSharp.dll` (see `docs/NATIVE_*_FINDINGS.md`) — re-verify a member against the real installed assembly before relying on it, don't assume a name/shape from memory or another mod's source.
- Do not invent a per-character progression key. Research a genuinely stable local-player character identity across zone transitions, process restart, and multiple character slots before changing scope away from profile-wide progression; see the "Stable player identity research" note in `docs/` history.
- Do not add Cooking or additional forage resource types without being asked; current scope is Foraging only (mod-owned nodes), Mining/Fishing stay vanilla.
- Do not touch Bank/vendor/trade/Auction House code paths and claim them safe — they are explicitly unverified.
- Do not add or imply reuse of `cammaron/Arcanism` source — its license status is unresolved (no LICENSE file found); this mod's item-registration technique was independently reimplemented against this repo's own IL evidence, not copied. See `docs/ATTRIBUTION.md` before touching anything registration-related, and preserve that file's content/categories when adding new attribution entries.
- Do not commit `bin/`, `obj/`, `refs/`, `test-backups/`, compiled DLLs, or game/BepInEx assemblies. `.gitignore` already covers these.
- No secrets, personal file paths, tokens, or real names in source, docs, or commit messages.
- Do not commit or push changes unrelated to the task at hand.

## Important source files

- `src/ErenshorCraftingExpandedPlugin.cs` — `BaseUnityPlugin` entry point, `/craftdiag` command patch, UI input-protection patches (`PlayerControl.LeftClick`, `csMouseOrbit.LateUpdate`, adapted from this author's own `Erenshor-PvP`, see `docs/ATTRIBUTION.md`).
- `src/Crafting/CraftSuccessPatch.cs`, `src/Crafting/ForgeStackQolPatch.cs` — the two Smithing Harmony patches; see the design boundary above before touching either.
- `src/Crafting/CraftingProgressionStore.cs` — sidecar persistence, exposes `LastError`; never touches Erenshor's own save files.
- `src/Compatibility/GameCraftingApi.cs`, `GameForagingApi.cs`, `GameItemRegistryApi.cs`, `SimIdentityApi.cs` — the reflection/IL-evidence boundary against native game types.
- `src/Foraging/` — node catalog/controller/definitions and `ForagingScanPolicy.cs` (pure, testable candidate-ranking logic used by the live scanner).
- `src/Items/CustomItemRegistry.cs` — transactional custom item insertion.
- `src/Commissions/` — experimental, disabled-by-default PoC; do not enable by default.

## Build / test procedure

- Build-only compile check: `powershell -ExecutionPolicy Bypass -File .\BUILD.ps1` — auto-detects the Erenshor install and a BepInEx reference root, compiles to this mod's own `bin\`, and **never installs anything**. Refuses to guess between multiple BepInEx roots; pass `-BepInExRoot` explicitly if it does.
- Deterministic pure-logic tests: `powershell -ExecutionPolicy Bypass -File .\tests\RUN_TESTS.ps1` — standalone `csc` compile + run of the 10 pure-logic test groups, no game/BepInEx dependency.
- Test install (reversible, records a backup): `.\INSTALL_TEST.ps1`. Removal: `.\REMOVE_TEST.ps1`. `BUILD_AND_INSTALL.ps1` is a one-shot path with no backup — not the preferred development flow.
- The shipped build compiles with the legacy .NET Framework `csc.exe` (effectively **C# 5**) despite the `.csproj` claiming `LangVersion 7.3`. Avoid string interpolation, `nameof`, null-conditional operators, auto-property initializers, expression-bodied members, and inline `out` variables — this toolchain rejects all of them.
- Compile and run the deterministic tests before claiming a change works. Live in-game verification (see `docs/FIRST_RUNTIME_TEST.md`) is a separate step this environment cannot perform — never claim something is runtime-verified unless it actually was, by a human, in the running game.

## Compatibility boundaries

- Adapts UI/input-protection patterns from this author's own `Erenshor-PvP` (same author, Apache-2.0, independent repo) — see `docs/ATTRIBUTION.md` for exactly what was adapted versus independently reimplemented.
- No hard dependency on any other mod in the suite.
