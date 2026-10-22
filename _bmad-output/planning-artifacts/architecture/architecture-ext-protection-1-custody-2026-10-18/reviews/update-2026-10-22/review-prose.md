# Prose review — 2026-10-22

This document exists to help expert Platform, EventStore, Parties and Agents owners choose the remaining custody alternatives blocking Story 5.4.

Reader: humans. Style guide: Microsoft. Preserve the direct technical voice, stable decision and contract identifiers, explicit approval boundaries, compact comparison tables, diagrams, and repeated distinctions between recommendations, accepted policy and qualified implementation. Content and architecture choices are outside this editorial review.

The structure pass selected a decision-first progression and moved Remaining owner decisions immediately after Two custody classes, one recovery contract. That move is already applied; there were no CUT or MERGE findings.

`word_metrics.py` measured 3,853 words: introduction 125; Accepted choices and current blockers 375; Two custody classes, one recovery contract 290; Remaining owner decisions 688; Interaction keys 489; AWS profile 205; Human-binding history 600; Independent authority, restore and evidence retention 598; Evidence needed before handoff 423.

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| prose | at `now >= ExpiresAt`, it denies even if a worker has not persisted Expired. | at `now >= ExpiresAt`, deny release even if a worker has not persisted Expired. | In Human-binding history, authorization requirement: replaces the unclear “it” with the action required at the boundary. |
| prose | Neither is qualified; otherwise retain refusal. | Neither is qualified; retain production admission refusal until one qualifies. | In the Q6 P1/P2 paragraph: removes the ambiguous “otherwise” and states when refusal can end. |
| prose | Under both alternatives, an expiry-time response loss uses the original operation/proof, never a new full receipt or a recreated identity key. | Under both alternatives, recover a response lost at expiry from the original operation/proof; never create a new full receipt or recreate an identity key. | In the Q3/Q7 R1/R2 paragraph: makes recovery the action instead of making response loss the actor. |

Three prose recommendations. No content cuts or merges are proposed. The already applied structure move saves 0 words (0% of the measured 3,853); no length target was supplied. These sentence-level fixes retain the technical distinctions and do not trade away reader guidance.
