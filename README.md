# Deep Sims for Erenshor 0.7.0

Deep Sims adds an optional local-LLM social layer to Erenshor's existing SimPlayers. It observes verified game state, builds a small grounded context, and asks a local Ollama model for short MMO-style dialogue. It does not replace Sim AI and does not control gameplay.

## What it does

- Reads current party identity, classes, levels, guilds, assignments, zone, target, combat, HP/mana where available, deaths, kills, loot, encounters, and verified travel/camp events.
- Keeps bounded sidecar memory per Sim: outings, verified events, compact summaries, familiarity, rivalry tone, preferences, and Sim-to-Sim social history.
- Supports player-to-Sim whispers, party chat, Sim-to-Sim threads, autonomous idle chatter, event reactions, reunion lines, and concise post-combat/duel reactions.
- Grounds answers with a strict trust hierarchy: observed state, verified experience, remembered summaries, wiki/news, external real-world news, heard dialogue, then unknown.
- Rejects unsupported history, fabricated kills/loot/deaths, wrong classes/guilds/roles, assistant-style language, prompt leaks, rich-text injection, and stale or duplicate replies.
- Uses optional Erenshor wiki lookup, official-update lookup, and narrowly triggered external real-world news. External news is conversation-scoped and never becomes Erenshor fact or permanent memory.
- Provides deterministic diagnostics for sessions, encounters, performance, guards, seeds, memory, events, COOP state, and inference mode.

## Gameplay boundary

Erenshor remains authoritative for movement, pulls, attacks, spells, healing, targeting, grouping, loot, quests, equipment, and faction state. Deep Sims can describe or socially react to those events; the model cannot issue those actions. Memory remains sidecar data and never edits Erenshor save files. Ollama, wiki, news, and Deep Sims failures leave ordinary gameplay running.

## Co-op behavior

COOP is optional. When detected, host authority is required before Deep Sims starts. Remote humans are not classified as Sims, remote party chat is HEARD context rather than verified fact, and clients do not run competing Social Directors. Generated Sim speech is currently host-local when safe party-targeted broadcast cannot be proven.

## Configuration and major commands

The plugin is `forgetwhtuno.erenshor.deepsims`, version `0.7.0`. Important commands include:

```text
/aistatus              model and plugin status
/aitest                deterministic AI/integration smoke tests
/aimodel <model>       change Ollama model
/dwhisper <Sim> <text> force an AI whisper
/vwhisper <Sim> <text> request vanilla-style handling
/dssession             current/last/outing encounter state
/dsperf                performance and request diagnostics
/dsmemory [Sim]        bounded memory inspection
/dsforget <Sim> ...    forget eligible flavor/social data
/dsexport [full]       concise or detailed session export
/dsevents ...          verified event director controls
/dsseeds ...           autonomous seed diagnostics
/dsguardtest            grounding guard smoke tests
/dsinference ...       auto/CPU/GPU inference mode
/dsxnews <query>       explicit external-news lookup
/dsnewsources          external-news source status
/dscamp ...            Campmaster context integration
```

Exact command availability is reported by `/aistatus`; settings are in the generated BepInEx config. Network lookups and Ollama are optional.

## Memory and grounding

Generated dialogue and player claims remain HEARD. Verified combat, loot, death, zone, quest, duel, and party events can become compact EXPERIENCED/REMEMBERED records. The system never turns a single observed drop into an exclusive drop-table claim and never invents “again,” “last time,” or shared history without evidence.

## Related mods

- [Erenshor Practice Duels](https://github.com/forgetwhtuno/Erenshor-Duel): friendly, non-lethal virtual-health duels with local Sims.
- [Erenshor PvP](https://github.com/forgetwhtuno/Erenshor-PvP): standalone off-map Sim-profile PvP encounters.
- [Erenshor Follow](https://github.com/forgetwhtuno/ErenshorFollow): deterministic player follow, Sim-led travel, and expeditions.
- [Erenshor Party Tools](https://github.com/forgetwhtuno/Erenshor-PartyTools): ready checks, cosmetic rolls, friend availability, and a compact command panel.
- [Erenshor Campmaster](https://github.com/forgetwhtuno/Erenshor-Campmaster): read-only Hunt Camp and Relax social-context modes.
- [Erenshor Nemesis](https://github.com/forgetwhtuno/Erenshor-Nemesis): an optional persistent rival system that can use PvP results when PvP is installed.

All companion mods remain optional and communicate through narrow reflection/event surfaces where applicable. PvP sends only sanitized lifecycle facts to Deep Sims; Deep Sims may react socially but never controls PvP gameplay.

## Feature design drafts

Future-facing README/TODO packages for [PvP Events, the Deep Sims Companion System, and the Nemesis System](docs/feature-drafts/README.md) document proposed boundaries, data scaffolding, sequencing, and observable acceptance checks. They are design drafts, not implemented-feature claims.

## Credits and Inspiration

### Inspiration

- **[CustomSimFramework](https://github.com/PuzzelPiece/CustomSimFramework) by PuzzelPiece (TeamSaltyBois)** — knowing about this project helped inspire the social direction of Deep Sims. My interest was in deepening the SimPlayers already in my party with richer reactions, persistent memory, and familiarity, rather than adding new custom sims through content packs, which is what CustomSimFramework does. No code was used; this is an independent implementation.

### Compatibility / related projects

- **[Erenshor COOP](https://github.com/MizukiBelhi/ErenshorCoop) by MizukiBelhi** has been an important technical reference and compatibility target, particularly for distinguishing local Sims from remote/networked actors. I have also used a locally updated copy for recent-patch and Deep Sims compatibility testing. Erenshor COOP remains its own separate community project. No COOP code is included here; detection is reflection-only and the plugin works normally when COOP is absent.

## Development note

This project has been developed heavily with AI-assisted coding tools. The goal has been to build features I wanted to use in Erenshor, with development guided through design, testing, playtesting, audits, and iteration against the game. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.
