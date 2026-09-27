# PRD structure polish — 2026-09-27

This document exists to help the internal Platform team understand the agreed product scope, implement its requirements and qualify the local, CI and production outcomes.

**Structure model:** Strategic/Context (Pyramid). **Readers:** humans. **Style guide:** Microsoft Writing Style Guide. No length target was supplied. Policy, thresholds, stable IDs and user decisions are preserved. The document already puts purpose, vision, scope, release modes and entry gates before detailed requirements; the overall order serves its purpose.

## Exact measurement

Executed `uv run .agents/skills/bmad-review/scripts/word_metrics.py _bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md`.

- Document total: **7,204 words**.
- FR-4: **499 words** across 16 testable-consequence bullets.
- Release scope and production entry: **311 words**.
- Target users and jobs: **139 words**.
- Success measures: **448 words**; Counter-metrics: **181 words**.
- Downstream decisions and readiness evidence: **485 words**.
- Glossary: **719 words**.

Section counts exclude heading text; document total uses the script's full-text token convention. The same script's `word_count` function measured the movable journey sentence at 26 words and the proposed three group labels at 11 words.

## Findings

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| structure | FR-4, testable-consequence list, `prd.md:112–127`: 16 continuous bullets in a 499-word requirement. Run creation, readiness and completion rules are interleaved. | **MOVE and group within FR-4.** Keep every bullet verbatim. Add `**Composition and readiness**` before the first Parties bullet. Add `**Run isolation and ownership**` before “Test data is isolated…”. Move the complete “A new suite or compatible batch gets a fresh run-owned environment…” bullet immediately after that test-data bullet. Add `**Completion, cleanup and diagnostics**` before “The environment can serve a test suite or compatible batch…”. Leave the remaining completion/cleanup bullets in their existing order. | Three short labels and one bullet move let a human find startup, isolation or cleanup behavior without reading all 16 bullets. No changed semantics, IDs or thresholds. **Adds 11 words; saves 0.** FR-4 becomes 510 words under the same section-count convention. |
| structure | Final sentence of Downstream decisions, `prd.md:336`: “User journeys are represented by capability and acceptance scenarios because this PRD governs developer orchestration and module-provided interfaces; module business journeys remain owned by the modules.” | **MOVE** the exact 26-word sentence to a paragraph immediately after the Target users and jobs list (`prd.md:32`), before Confirmed MVP scope. Leave the uptime/latency commitment paragraph in Downstream decisions. | Explains the capability-led reading path where readers first encounter the user roles. Downstream then ends with service-target ownership rather than a late explanation of document shape. **0 net words**; Target users becomes 165 words and Downstream becomes 459 words. |
| structure | Release scope and production entry, 311 words, compared with more detailed FR-6/FR-8 and later glossary entries. | **PRESERVE** the scope definitions, two modes, G1/G2/G3 table and restricted pre-G2 synthetic qualification note before Features. | Apparent repetition provides essential scaffolding and prevents readers from confusing document readiness with user admission or automatic-promotion qualification. Cutting it would increase cross-reference burden. **0 words removed.** |
| structure | Success measures and Counter-metrics, 448 + 181 words, compared with FR/NFR acceptance detail. | **PRESERVE** the separate acceptance/evidence view and the counter-metrics. | Requirements define behavior; these sections define the combined demonstrations and failure signals. Their repetition supports different reader tasks and is not true redundancy. **0 words removed.** |

## Reduction summary

Four recommendations: two changes and two explicit preserves. Accepting all changes removes **0 words (0%)**, adds **11 words (0.15%)**, and produces **7,215 words**. There is no length target to meet. The small increase buys scanning structure; no requirement, example, accepted trade-off or human-oriented explanation is sacrificed for brevity. No additional structural change is recommended.

Only this report was written. The PRD was not edited. The preceding consistency report separately records verification that both low wording findings were fixed in the addendum.
