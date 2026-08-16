# Deep Sims 0.7.3 live test checklist

1. Send `dancer do you like being a windblade?`
   - Expect a personal answer, no `hang on, i'll check`, no wiki/news request.
   - Log: `turnType=Opinion` or `PersonalPreference`, `knowledgeNeed=None`, `retrievalDecision=social`.
2. Send `what are you guys reading tonight?`
   - Expect a social reply without lookup acknowledgement.
3. Send `any news about nasa?`
   - Expect immediate acknowledgement, external-news retrieval, then an answer to the original question.
4. Send `how do i get wolf meat?`
   - Expect immediate acknowledgement, Erenshor wiki path, then an acquisition answer.
5. Run `/dsbanter` with at least two active Deep Sims.
   - Expect two visible connected Sim lines. If not, expect one visible concise diagnostic containing `reason=`; never silence.
6. Reply to the second Sim line.
   - Expect same-thread continuity and no autonomous tail after the bounded turn budget.
7. Complete an Expedition with existing members, then add Astra.
   - Astra must not say she remembers that prior Expedition. She may state an independent, non-historical opinion about its location.
8. Check `lunaris.log`.
   - Confirm route diagnostics contain no player text/prompt content; confirm factual requests use `retrieve`, social requests use `social`.
