# Addendum structure review — 2026-09-27

This document exists to help Platform product, architecture and implementation readers understand accepted decisions, compare the original options and locate remaining qualification work.

**Reader:** humans. **Style guide:** Microsoft Writing Style Guide. **Model:** Reference/Database for the topic-based supporting context, with Explanation scaffolding inside technical sections and the selected decision before alternatives within each decision record. No length target or hooks apply. Content and accepted decisions are preserved; these are proposals only.

The addendum is a useful reference with detailed option mechanics, advantages, costs and recommendations that the user expressly requested. Two rearrangements improve navigation without deleting that depth. No general shortening is recommended.

## Findings

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| structure | **S-1:** §Disaster recovery approach and alternatives places the three options (**130 + 220 + 168 = 518 words**) before §Selected approach and operational scope (**513**), §Response coverage and the four-hour target (**246**) and §Recovery authority, erasure and external effects (**221**). | **MOVE** those three selected-contract sections immediately after the chapter's introductory definition/context. Use this order: introduction → selected approach → response coverage → recovery authority/erasure → Options 1, 2 and 3 → source grounding. Keep all sections and their text. | A reader finds the governing recovery contract, continuous RPO, coverage rule and erasure exception before the historical alternatives. The opening already identifies Option 2, so the options remain understandable when read later. **Word impact: 0 removed/added; 980 measured section-content words relocated.** |
| structure | **S-2:** §Accepted release identity and production entry contains **477 words** covering retained artifacts, evidence identity, two release modes, G1–G3, synthetic test admission, AD-15 recovery compatibility and retention. §Recovery and its limits contains **300 words**. | **MOVE** to three clearer locations: (1) rename the original section **Retained release identity and evidence**, keeping its first two paragraphs and the AD-2 retention paragraph; (2) add the following sibling section **Production modes and entry gates**, containing the release-mode paragraph, gate table and restricted synthetic-admission paragraph; (3) move the AD-15 recovery-combination paragraph to the start of existing **Recovery and its limits**. | The reader can find artifact identity, entry authority and rollback compatibility independently. Exact preview content counts: retained identity **224**, modes/gates **190**, recovery limits **363**; all **777** original section-content words are preserved. **Word impact: 0 content removed; total document +5 words from heading structure, measured by the same script.** |
| structure | **P-1:** The dependency-testing, readiness, cancellation, rollback, disaster-recovery, deputy-authority and response-coverage comparisons repeat some selected conclusions. | **PRESERVE** their workings, advantages, costs, rejected alternatives and selected recommendations. | The apparent repetition supplies the rationale requested by the user and lets readers assess the trade-offs. Replacing it with bare links or a selected-policy list would remove contributed content. **Word impact: 0; no cuts proposed.** |
| structure | **P-2:** The introduction/historical implementation observations, accepted hosted-decision table and final qualification table separate observations, chosen mechanisms and unproved capability. | **PRESERVE** that distinction and the local/source links. | These sections stop readers treating historical repository evidence, document finality or accepted architecture as a production pass. The owner/gate table is useful random-access reference. **Word impact: 0; no cuts proposed.** |

## Exact word metrics

Command: `uv run .agents/skills/bmad-review/scripts/word_metrics.py _bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/addendum.md`.

**Current total: 7,264 words.** Counts below are the script's direct per-heading section counts; parent rows do not include child sections. The document total is reported independently by the same script.

| Heading | Exact section words |
| --- | ---: |
| (preamble) | 15 |
| Supporting context | 48 |
| Existing implementation context | 129 |
| McpCli context | 286 |
| Workspace and build constraints | 166 |
| Module-owned configuration example | 163 |
| Testing options for module dependencies | 31 |
| MVP approach | 274 |
| Option 1: Real services for all tests that exercise dependencies | 147 |
| Option 2: Allow selective test doubles | 172 |
| Option 3: Real Platform environments with focused tests outside Platform | 124 |
| Supporting sources | 55 |
| Test readiness and local cancellation rationale | 41 |
| Selected behavior | 197 |
| Readiness option 1: Central checks and a fixed timeout | 71 |
| Readiness option 2: Independent module checks and timeouts | 76 |
| Readiness option 3: Module checks with a common startup policy | 268 |
| Cancellation option 1: Stop resources owned by the cancelled run | 103 |
| Cancellation option 2: Keep the environment after cancellation | 61 |
| Official-source context | 124 |
| Production rollback policy and rationale | 54 |
| Options and rationale | 204 |
| Accepted release identity and production entry | 477 |
| Failure triggers and initial thresholds | 267 |
| Module responsibilities and production test data | 59 |
| Recovery and its limits | 300 |
| GitHub notification integration | 66 |
| Recovery deputy authority | 273 |
| Implementation implications from official documentation | 89 |
| Disaster recovery approach and alternatives | 195 |
| Option 1: Daily backups and manual rebuild | 130 |
| Option 2: Frequent off-primary backups and a tested restore procedure | 220 |
| Option 3: Redundant infrastructure and database failover, with backups retained | 168 |
| Selected approach and operational scope | 513 |
| Response coverage and the four-hour target | 246 |
| Recovery authority, erasure and external effects | 221 |
| Source grounding | 93 |
| Hosted architecture questions | 491 |
| Remaining qualification and ownership | 407 |

## Recommendation summary

Two proposed structural changes and two explicit preserve findings. Applying S-1 and S-2 to a temporary preview, then running the same metrics script, produced **7,269 words**: **5 additional words (0.07%)**, with no body-content reduction. The source addendum was unchanged and the temporary preview was removed.

There is no length target to meet. The only trade-off is one additional heading; it improves navigation within a dense release section. No option mechanics, rationale, recommendation, historical observation, source link, acceptance decision or qualification owner is removed.

Before this pass, the rubric review's R-1 was rechecked and a resolution note appended: shared release controls are required before the first applicable production attempt, including approved pre-G3 attempts; SM-5 separately gates G3.
