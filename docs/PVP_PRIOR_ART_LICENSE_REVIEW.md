# Erenshor PvP prior-art and license review

Research date: 2026-08-11. This is practical engineering-license research, not legal advice. It is deliberately conservative: a public repository or downloadable Thunderstore package is **not** treated as permission to copy code.

## Scope and local baseline

The checked local copies of `DeepSim-erenshor` and `Erenshor-Duel` both contain the Apache License 2.0 and notices naming `forgetwhtuno`. Practice Duels is therefore the preferred technical baseline, subject to its own game-version validation. Its current README documents: local same-scene Sim identity; COOP remote-human exclusion; virtual health/yield; interception of verified damage, healing, spell, pet, and effect routes; fail-closed third-party effects; and cleanup/restoration for health, effects, target, aggro, pets, autoattack, and temporary state.

This report did not add a dependency or copy any external code.

## Findings at a glance

| Project | Author | Source / version evidence | License and verified source | Reuse decision |
|---|---|---|---|---|
| Reckss PvP Mod | Recks / GitHub `Reckimus` | [Thunderstore package](https://thunderstore.io/c/erenshor/p/Recks/Reckss_PvP_Mod/) reports `Recks-Reckss_PvP_Mod-1.1.0`; public repo `main` was commit `15b7184d426270d803c9f8d19eebc254267beb23` (2025-05-02). | **NO LICENSE FOUND — DO NOT COPY.** Checked repository root: only `README.md` and `Reckss-PvP-Mod.dll`; no `LICENSE`, `COPYING`, license header, or package license was found. | D — study behavior and independently reproduce only. |
| Practice Duels / Erenshor-Duel (local baseline) | forgetwhtuno | Local `Erenshor-Duel`, README identifies 0.3.1. | Apache-2.0; local `Erenshor-Duel/LICENSE` and `NOTICE`. | A — same-owner project; reuse its established boundary intentionally, preserving Apache notices. |
| Duels | Raikou / `devRaikou` | [GitHub](https://github.com/devRaikou/Duels), release/source version `2.0.5`, inspected commit `f0e5d11fd477969d6ad12c3b60e38fb4f409b27f` (2026-03-06). | MIT; repository `LICENSE` says `Copyright (c) 2025 Raikou`. | A/B for isolated, genuinely useful generic code only. Prefer a clean internal C# implementation; do not import the Paper/Minecraft framework or bundled database stack. |
| Duels | Jungwoo Im / `Realizedd` | [GitHub project](https://github.com/Realizedd/Duels); [package metadata](https://mvnrepository.com/artifact/com.github.Realizedd.Duels/duels-api/3.5.0) identifies GPL-3.0. | GPL-3.0; package metadata (secondary confirmation). | D for ideas; do not copy/link into an Apache-2.0 PvP mod without a separate legal/architecture review. |
| double_elimination | `smwa` | [GitHub](https://github.com/smwa/double_elimination) | MIT; repository `LICENSE` shown by GitHub. | B for algorithmic reference only. It is Python and not a runtime dependency candidate. |

### License compatibility matrix

| Project | Copy/adapt? | Modify? | Redistribute? | Commercial use? | Attribution / notice | Source disclosure / copyleft | Apache-2.0 project safe? |
|---|---:|---:|---:|---:|---|---|---|
| Reckss PvP | No | No permission established | No permission established | No permission established | No license grant; retain no copied material | No established terms; copyrighted by default | No — independent implementation only. |
| Practice Duels | Yes | Yes | Yes | Yes | Preserve copyright, license, and existing NOTICE; mark modified files when applicable | No copyleft; Apache patent and notice terms apply | Yes. |
| devRaikou/Duels (MIT) | Yes | Yes | Yes | Yes | Include MIT copyright and permission text with substantial copied portions; cite source in third-party notices | No source disclosure or copyleft | Yes, if its notice is retained. |
| Realizedd/Duels (GPL-3.0) | Not for this project | Yes under GPL | Yes under GPL | Yes under GPL | GPL notices and license required | Strong copyleft for derivative/combined distribution; compliance scope is fact-sensitive | No practical fit for code/link reuse in an Apache-only mod. |
| smwa/double_elimination (MIT) | Yes | Yes | Yes | Yes | Retain MIT copyright and permission text for substantial code | No source disclosure or copyleft | Yes, but reimplementing a small bracket model is lower-risk. |

For any **new** candidate: Apache-2.0, MIT, BSD-2-Clause, and BSD-3-Clause are normally compatible when their required notices are retained. MPL-2.0 may be usable only if MPL-covered files remain under MPL, making it a poor fit for copied source in this small Apache-only mod. LGPL/GPL/AGPL and CC licenses require separate, precise review; do not treat them as Apache-compatible by default. “No license” means no copying.

## Erenshor-specific technical comparison

### Reckss PvP Mod

**What it does.** The package README says F5 toggles PvP, grouped SimPlayers cannot be attacked, and a Sim attacked by the player should abandon its prior logic and focus on killing the player. The package also notes that Sim-on-player behavior during NPC attacks still needed testing.

**How it appears to work.** The public source repository contains a compiled `Reckss-PvP-Mod.dll`, not source. Assembly metadata identifies Harmony patches for `Character.DamageMe`, `Character.MagicDamageMe`, and `SimPlayer.Update`, and references `Character._fromPlayer`, `SimPlayerGrouping`, `NPC.AggroOn`, `CurrentHP`, `inCombat`, and `NavMeshAgent.SetDestination`. This supports the narrow conclusion that it uses damage-path interception plus a Sim update loop; it does **not** establish exact current game signatures or complete behavior. In particular, the metadata alone cannot prove the exact `_fromPlayer` mutation, death/progression result, or all party/aggro edge cases.

**Useful idea.** Keep a single explicit player-vs-Sim eligibility gate before enabling hostile intent, and ensure the Sim is directed toward a real attacker rather than silently becoming hostile.

**Unsafe/problematic idea.** A global F5 hostility toggle with real damage/death has none of Practice Duels’ demonstrated containment, lifecycle state machine, COOP filtering, virtual health, interruption cancellation, or restoration guarantees. Patching only the two visible damage paths is insufficient: damage-over-time, pets, heals, status effects, assists, group actions, and cleanup can bypass it.

**Applicability.** Inspiration only. Do not copy, decompile into source, or use the binary as a dependency. Treat `Character.DamageMe`, `MagicDamageMe`, `_fromPlayer`, `NPC.AggroOn`, and `SimPlayer.Update` as historical research leads; verify every live signature and path against the installed `Assembly-CSharp.dll` before implementation.

### Practice Duels compared

Practice Duels already solves the safer version of the problem: it uses native Sim AI for combat while the mod owns only the boundary and virtual outcome. It explicitly does not permit real death, XP, loot, faction, item-loss, or save mutation. Its existing acceptance policies cover local identity (stable Sim tracking index before display-name fallback), active-zone locality, remote COOP-human exclusion, party state, health, and interruption cancellation.

For a future PvP mod, do not replace this with the Reckss approach. Extend the existing explicit match state and boundary only after each hook is revalidated against the currently installed game assembly.

## Reusable architecture lessons

| Area | Recommended independent implementation | Evidence / lesson | Avoid |
|---|---|---|---|
| Challenge flow | `Idle -> Offered -> Accepted -> Countdown -> Active -> Resolving -> Cleaned`; include expiry, refusal, cooldown, and one idempotent terminal cleanup path. | Practice Duels already has opt-in offers, 30-second expiry, diagnostics, and deterministic policy tests. | Global “PvP on” without per-match consent. |
| Combat containment | Keep virtual match HP separate from real progression. Admit only verified participant and pre-existing owned-pet damage/effects. Block or cancel unknown/outside effects. | Practice Duels’ virtual-health, effect, heal, pet, aggro, and restoration boundary. | Letting native death/loot/XP be the match result. |
| Sim hostility and aggro | Store match-local opponent/participant identities. Suppress party/bystander assist routes and restore target/aggro afterward. | Reckss demonstrates that hostility/aggro needs deliberate handling; Practice Duels supplies the safer containment. | Inferring identity from display name or `SimPlayer` component alone. |
| Lifecycle and cleanup | Cancel safely on zone change, distance break, camp state, real combat, participant loss, hostile interruption, error, or manual stop. Cleanup must be idempotent. | Practice Duels documents and tests these exit categories. | A one-shot reset that leaves autoattack, pet, status, target, or aggro residue. |
| Team PvP | Do not spawn/clone persistent Sims until a temporary-avatar lifecycle is verified. Model teams as explicit participant sets with average-level and composition policy. | Practice Duels’ self-tested future-team policy and its explicit refusal to use unverified spawn APIs. | Persistent-Sim duplication or party mutation as a shortcut. |
| Ranking | Start unranked. If ranked later, use sidecar per-mode ratings and update only on validated terminal results. Use expected-score ELO/Glicko-style math, provisional matches, and a rating floor. | `devRaikou/Duels` separates ranked/unranked and persists per-kit ELO, but its fixed +/-25 update is too simplistic to adopt unchanged. | Ranking practice cancellations, invalid matches, or unequally configured modes together. |
| Rewards / anti-farm | Sidecar marks only; cap daily rewards, suppress or sharply decay repeat-opponent rewards, require a minimum active-match duration/meaningful damage, and record cancellation reason. | Practice Duels already proposes opponent/composition cooldowns and participation credit. | XP, normal loot, item loss, or repeated-win mark farming. |
| Spectators | Spectator is an explicit non-participant role: no damage/heal/aggro contribution, no reward eligibility, explicit join/leave cleanup. | `devRaikou/Duels` tracks spectators separately and removes them at duel end. | Treating nearby Sims or remote COOP humans as spectators by accident. |
| UI | Native-styled challenge prompt with explicit Accept/Refuse, remaining expiry, opponent/mode, and protected-zone/eligibility reason. | Practice Duels menu and explicit invitation are the closest UX fit. | Hidden automatic challenge acceptance or an imported large GUI framework. |
| Social | Emit fact-only lifecycle events (`challenge`, `accepted`, `declined`, `started`, `completed`, `cancelled`); let Deep Sims react socially but never choose targets/actions/outcomes. | Existing Duel–Deep Sims event contract. | LLM-controlled PvP decisions or generated dialogue becoming result evidence. |

## Attribution and notices

Create `THIRD_PARTY_NOTICES.md` **only when third-party code or a third-party binary is actually distributed**. It should identify project, author/copyright, source URL, exact revision/version, license, files/components used, and required license text location.

* For local Apache-2.0 cross-project reuse: retain the Apache license, relevant copyright notices, and relevant NOTICE content; state materially modified files where required.
* For MIT code from `devRaikou/Duels` or `smwa/double_elimination`: include the original MIT copyright and permission notice with the copied substantial portion (prefer a full copy in `THIRD_PARTY_NOTICES.md` plus source-file attribution).
* Reckss needs **no third-party attribution entry for ideas alone**, but a voluntary README credit may say it helped identify the problem space. Do not imply permission or code reuse.
* Do not distribute Reckss’s DLL, its decompiled output, or copied fragments.
* Do not import `devRaikou/Duels`’ Paper, HikariCP, SQLite JDBC, MySQL, PlaceholderAPI, or LuckPerms dependency stack. A small BepInEx sidecar JSON store is safer and sufficient initially.

## Recommended decision record

1. Build future Erenshor PvP as an extension of the Apache-2.0 Practice Duels containment model, not an adaptation of Reckss.
2. Keep it unranked/no-reward until the native damage/effect/cleanup paths and COOP behavior have live validation.
3. Independently implement the small match state machine, repeat-opponent cooldown ledger, rating math, brackets, and UI. Those ideas are not proprietary to any one project and avoiding copied code keeps the distribution simple.
4. Do not add `THIRD_PARTY_NOTICES.md` yet unless code is actually imported. If a future MIT fragment is copied, add it in the same change.
5. Before any gameplay work, record installed-assembly evidence for every intended patch; prior public binaries and stale mod metadata are not authoritative.

## Source notes

* [Reckss PvP Mod on Thunderstore](https://thunderstore.io/c/erenshor/p/Recks/Reckss_PvP_Mod/) — package description, author, dependency/version label, and source link.
* [Reckimus/ErenshorPvP](https://github.com/Reckimus/ErenshorPvP) — public repository inspected at the commit stated above; no license file present.
* [devRaikou/Duels](https://github.com/devRaikou/Duels) — MIT license and architecture/source version noted above.
* [Realizedd/Duels](https://github.com/Realizedd/Duels) and [Maven package metadata](https://mvnrepository.com/artifact/com.github.Realizedd.Duels/duels-api/3.5.0) — GPL-3.0 compatibility warning.
* [smwa/double_elimination](https://github.com/smwa/double_elimination) — MIT bracket-algorithm reference.
