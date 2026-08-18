# Deep Sims 0.7.3 social repair findings

## Live evidence

Newest `lunaris.log` (2026-08-15 22:30 local) confirmed the reported regressions:

- Dancer's Windblade question was classified `intent=Opinion` but logged `retrievalUsed=True`.
- `/dsbanter` emitted `source=dstalk` for Phanty, then a topic-mismatch grounding rejection with no visible fallback.
- Dancer and Cyndara generated independent `SocialBanter` lines, which did not establish a connected exchange.

## Repair

- Semantic social types now include personal preference, social question, reaction, and humor. Opinion/preference/social/humor/greeting routes clear `KnowledgeNeed` and `SearchQuery` after parsing, so a class name cannot reopen a lookup.
- `ResolveRoutedKnowledgeAsync` now exits before even experience retrieval when `KnowledgeNeed=None`; lookup acknowledgement remains limited to real factual routes.
- The direct route logs bounded diagnostics: turn type, knowledge need, retrieval decision, and retrieval reason. It does not log prompt or private message content.
- Thread continuation re-snapshots the visible party conversation after the first line displays. Sim B therefore receives the final visible Sim A line, not merely a line that was scheduled and later rejected.
- `/dsbanter` no longer enters the `dstalk`/ambient-seed route. It uses a bounded connected two-line diagnostic exchange; any queue failure produces a visible `DeepSims: Banter could not be generated` reason.
- Session seeds now capture witnesses from participants at event time and expose eligible speakers. A newly joined Sim cannot use the event as first-person history.

## Preserved

- Immediate lookup acknowledgement, async retrieval, factual answer generation, wiki/news split, current-party grounding, and LivePartyFacts membership revalidation are unchanged.
- Existing strict grounding still applies to world facts. The relaxation is only the decision not to retrieve for harmless personal/social turns.
- Existing bounded hidden reflection remains non-visible and cannot establish verified history from chat.

## Acceptance caveat

The deterministic suite verifies routing and thread inputs. Live acceptance still requires observing a party exchange where Sim B visibly answers the exact displayed Sim A line; do not mark social conversation complete from unit coverage alone.
