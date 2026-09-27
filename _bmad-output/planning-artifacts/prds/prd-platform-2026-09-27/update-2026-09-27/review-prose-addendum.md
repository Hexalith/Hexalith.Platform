# Addendum prose review — 2026-09-27

This document exists to help human Platform readers understand accepted decisions, assess their original trade-offs and find the implementation and qualification work that remains.

**Style guide:** Microsoft Writing Style Guide. The existing voice is direct, technical and explanatory. Preserve the explicit selected-option labels, module and requirement identifiers, contrast between historical observations and accepted requirements, and the full workings/advantages/costs/recommendations. The accepted structure changes are in place; this pass proposes only small clarity fixes within that structure.

## Findings

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| prose | **P-1 — §Recovery deputy authority, selected Option 1 costs:** “Releases remain stopped until Administrator returns; the deputy needs practiced access and must receive alerts.” | “Promotions remain stopped until Administrator resumes them; the deputy needs rehearsed recovery access and must receive alerts.” | Names the activity that remains stopped. The deputy can reopen service independently; the wording should not suggest serving releases stay unavailable until Administrator returns. Uses the existing promotion-resumption authority without changing it. |
| prose | **P-2 — §Selected approach and operational scope, opening:** “Use Option 2 with the one-hour potential ordinary-data loss target and validate the four-hour recovery target under the selected response coverage before production use.” | “Use frequent independent backups and a tested restore procedure (Option 2 below), with the one-hour potential ordinary-data loss target. Validate the four-hour recovery target under the selected response coverage before production use.” | After the structure change, this section precedes the option descriptions. Names the approach at the point of use and separates selection from qualification; preserves both targets and the coverage condition. |
| prose | **P-3 — §Workspace and build constraints:** “AD-4 resolves the active root once and uses the active module plus its directly declared Hexalith dependencies from source with Debug assets; other dependencies use packages at the Builds catalog version.” | “AD-4 resolves the active root once. The active module and Hexalith dependencies declared directly by that root use source and Debug assets; other dependencies use packages at the Builds catalog version.” | Replaces the ambiguous “its” with “that root,” matching the preceding active-root dependency rule. This avoids reading the sentence as permission to initialize dependencies declared by a nested module. |

## Exact word metrics

Measured with `uv run .agents/skills/bmad-review/scripts/word_metrics.py _bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/addendum.md` after the structure changes.

| Scope | Current exact words | Exact preview with all three fixes |
| --- | ---: | ---: |
| Entire addendum | 7,267 | 7,277 |
| Workspace and build constraints | 166 | 166 |
| Recovery deputy authority | 273 | 275 |
| Selected approach and operational scope | 513 | 521 |

The preview used the exact original/revised replacements above and the same metrics script. The ten additional words (**0.14%**) clarify references; no content is cut and no policy or option is changed. Other section counts remain unchanged. No length target applies.

Three proposed fixes; no further preference rewrites recommended. The original addendum was not edited, and the temporary metrics preview was removed.
