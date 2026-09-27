# PRD structure review

This document exists to help the internal development, operations, and architecture team implement and verify the agreed Hexalith Platform scope without reopening product decisions.

**Model:** Strategic/Context (Pyramid). **Readers:** humans. **Style guide:** Microsoft Writing Style Guide. No extra standing directives or length cap apply.

The structure already supports lookup through stable requirement IDs, capability groups, and testable consequences. The highest-value changes bring scope boundaries forward and put the glossary where readers can consult it without interrupting the opening argument.

## Word metrics

Exact command: `uv run .agents/skills/bmad-review/scripts/word_metrics.py _bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md`.

Document total: **4,784 words**. Relevant section bodies: Glossary **505**, Confirmed MVP scope **100**, Target users and jobs **104**, MVP non-goals **86**, FR-4 **358**, Success measures **367**, Counter-metrics **157**. Section counts exclude their headings; the document total includes headings. Exact passage deltas below use the script's whitespace-token counting rule for these English passages.

## Findings

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| structure | Entire `## Glossary` section, currently between Target users and jobs and Confirmed MVP scope. The scope begins: “The MVP covers the seven-module set across local development/debugging, local tests, automated CI tests, staging, and production.” | **MOVE** the entire Glossary section unchanged to the end of the document, after Downstream decisions and readiness evidence. Replace the quoted scope sentence with: “The MVP covers EventStore, Tenants, Parties, Folders, Projects, McpCli, and Memories across local development/debugging, local tests, automated CI tests, staging, and production.” | Keeps the opening focused on purpose, users, and scope instead of requiring a 505-word terminology detour. Naming the seven modules at the scope boundary avoids a new forward-reference dependency. Move saves 0 words; sentence changes from 17 to 22 words, adding 5. All glossary definitions remain available. |
| structure | Entire `## MVP non-goals` section, currently after Cross-cutting non-functional requirements. | **MOVE** unchanged to immediately after Confirmed MVP scope, before Features. | The four explicit scope boundaries help readers interpret the requirements and belong beside included scope. Moves 86 body words; saves 0 words. |
| structure | Final paragraph of Target users and jobs: “The sources provide usage contexts rather than narrated user journeys; no detailed journeys have been inferred.” | **CUT** this paragraph. | The final paragraph under Downstream decisions already explains the capability/acceptance-scenario choice and preserves module ownership of business journeys. Removing this source-processing note keeps the user section focused on its jobs. Saves 16 words. |
| structure | Capability introductions, repeated numeric policy in FR-7/FR-8, and Success measures plus Counter-metrics. | **PRESERVE** these supporting summaries and repetitions. | Introductions orient human readers before detailed acceptance rules. The rollback section needs its own readiness and verification budgets; the success-measure sections explain ownership, timing, and evidence rather than merely copying requirements. Cutting them would force cross-reference chasing. Saves 0 words. |

## Result if accepted

Three change recommendations plus one explicit preservation finding. Net reduction: **11 words (0.23%)**, from **4,784 to 4,773 words**. No length target was supplied. The benefit is faster access to scope and its limits, rather than compression of the reviewed policies. The glossary move makes detailed definitions available on demand; explicitly naming the MVP modules in scope preserves the necessary opening context. No numeric policy, stable ID, ownership, or acceptance condition changes.
