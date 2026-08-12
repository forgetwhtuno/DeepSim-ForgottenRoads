# Deep Sims — Campmaster Relax social integration

This patch treats Campmaster's explicit Relax state as a social-context modifier,
not as gameplay authority.

- Campmaster `IsRelaxActive` is consumed by the existing optional reflection bridge.
- Relax suppresses legacy Hunt Camp social semantics while active.
- Quiet Relax opportunities use distinct semantic topic keys instead of one repeated
  `camp_idle` fingerprint, avoiding the old five-minute duplicate lockout.
- Cadence follows the existing `/dssocial` activity preset:
  - Quiet: roughly 120–240 seconds after the first opportunity.
  - Normal: roughly 45–120 seconds.
  - Lively: roughly 25–60 seconds.
- One admitted Relax opportunity may use the existing bounded Sim-to-Sim thread
  machinery; it does not create a second scheduler.
- The existing central SocialBudget remains authoritative. It receives a Relax-only
  budget profile so a short downtime thread does not consume the whole ten-minute
  ambient budget.
- Topic seeds cover preferences, classes/roles, zone atmosphere, gear aesthetics,
  adventure pace, harmless off-topic chat, verified outing facts, and verified
  memory. They never create gameplay facts.
- Templates mode and Ollama-failure fallback have safe Relax topic lines.

The patch does not make the LLM control movement, combat, pulls, roles, healing,
loot, travel, or Campmaster state.
