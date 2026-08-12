# PvP Events — Design Draft

## Goal

Add repeatable PvP structures without replacing the existing arranged-match and ambush foundation. All modes reuse off-map profile selection, temporary proxy construction, containment, lethal resolution, protected-zone policy, rewards, cooldowns, and cleanup wherever their rules permit.

## Recommended scope

### 1. Declared Skirmish

Build this first. A party or guild sends a bounded challenge for a named zone and team size. The player explicitly accepts or refuses; the opposing side's agreement is simulated as part of the offer fiction. In a single-player game, “scheduled” should initially mean a short in-session staging countdown—not a real-world calendar or offline timer.

Reuse:

- `PvpTeamPlanner` for roster construction.
- `PvpPolicy` and protected-zone rules.
- `PvpTemporaryCloneFactory` and `PvpCombatContainment`.
- Existing reward/cooldown and semantic-event paths.

New state:

```text
DeclaredSkirmishOffer
- matchId
- opponentLeaderKey/name
- opponentGuildId
- requestedZone
- attackerCount
- defenderCount
- offeredUtc/unscaled expiry
- stagingSeconds
- status: offered | accepted | refused | cancelled | active | completed
```

The first release should require the player to already be in the requested eligible zone. Cross-zone travel invitations can come later.

### 2. Rivalry and ladder records

Current `PvpRecordService` stores aggregate config counters. A ladder needs bounded sidecar records keyed by stable Sim identity and guild ID rather than dozens of dynamic config entries.

```text
PvpRivalryRecord
- stableOpponentKey
- displayName
- guildId
- wins/losses/escapes
- arrangedWins/arrangedLosses
- skirmishWins/skirmishLosses
- lastResultUtc
- lastMatchId
```

Keep at most a configured number of opponent and guild records, preserving the most recent/highest-activity entries. The panel can show personal record, top rivals, and guild record without inventing a global multiplayer ranking.

### 3. King of the Hill

This is a real objective-mode extension, not merely a different encounter label. A non-protected configured zone contains a bounded objective radius. Presence accrues score while the hill is uncontested; combat remains lethal unless a later design explicitly changes it.

Required new behavior:

- Objective center/radius configuration per exact scene.
- Contesting rules for living attacker/defender actors.
- Score/timer loop and objective-based completion.
- Leash/escape handling when actors leave the event area.
- HUD state in the PvP panel.
- AI validation: native attackers currently chase combat targets, not abstract objectives.

Do not claim KOTH complete until native AI can enter/contest the radius reliably without LLM movement control.

### 4. Capture the Flag

Stretch goal. CTF needs verified world-object spawning, carry attachment, drop/return rules, interaction input, home zones, scoring, disconnect/death cleanup, and pathing behavior. No flag prefab or safe carry API is currently established, so implementation must begin with an API investigation and leave TODOs rather than guessing.

Prefer one deliberately configured play area over arbitrary-zone CTF in the first version.

## Proposed code scaffold

```text
PvpEventMode
PvpEventDefinition
PvpEventSession
PvpEventDirector
IPvpObjective
DeclaredSkirmishDirector
KothObjective
CtfObjective
PvpRivalryStore
PvpEventSemanticBridge
```

The event director owns lifecycle and objective state. Existing combat classes remain responsible for actors, damage containment, and lethal cleanup. Objective code cannot directly command Sim movement or attacks.

## UI and commands

Add an `EVENTS` view to the existing PvP panel only after the compact everyday view remains uncluttered.

Possible command surface:

```text
/epvp event status
/epvp event accept|refuse
/epvp skirmish [1-5]
/epvp ladder
/epvp koth status
```

Force/debug commands remain hidden behind the existing TEST setting.

## Opinion

Declared Skirmish plus rivalry records is the best value. It uses systems that already work and gives encounters identity and continuity. KOTH is worthwhile but should be treated as the first objective-engine project. CTF is disproportionately expensive and should not delay the first three.

