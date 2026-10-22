# Editorial structure review

This document exists to help expert Platform, EventStore, Parties and Agents owner decision makers select concrete custody alternatives and assign the remaining decisions blocking Story 5.4 delivery.

**Model:** Strategic/Context (Pyramid), with conceptual scaffolding for the two custody classes. **Reader:** humans. **Style:** Microsoft. No target length. Content and policy are sacrosanct; this review concerns organization only.

**Exact metrics:** `uv run .agents/skills/bmad-review/scripts/word_metrics.py docs/implementation/ext-protection-1-custody-design-2026-10-18.md` reports **3,853 words**. Sections: introduction 125; accepted choices/blockers 375; two custody classes 290; interaction construction 489; AWS profile 205; history expiry/alternatives 600; recovery/evidence retention 598; owner decisions 688; implementation/qualification evidence 423.

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| structure | §Remaining owner decisions — 688 words, currently reached after 2,682 preceding words | **MOVE** the full section immediately after §Two custody classes, one recovery contract. Keep the detailed interaction, history and recovery sections below it as supporting explanation. | Puts the actionable owner choices after 790 words of recommendation, accepted policy and conceptual scaffolding. The table already expands P1/R1/H1 recommendations sufficiently for expert scanning; supporting sections then substantiate the choices. Moves 688 words; saves 0 words. No content loss. |

**Summary:** One recommendation; estimated reduction **0 words (0%)** of 3,853. No length target to assess. The change preserves the diagram, conceptual table, alternatives, accepted-policy boundary and all evidence. Trade-off: readers encounter owner alternatives before the full technical rationale; that ordering suits this expert decision-making audience and retains the rationale immediately below.
