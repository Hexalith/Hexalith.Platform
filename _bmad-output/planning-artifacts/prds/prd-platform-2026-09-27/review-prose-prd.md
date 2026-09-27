# PRD prose review

This document exists to help the internal development, operations, and architecture team implement and verify the agreed Hexalith Platform scope without reopening product decisions.

**Readers:** humans. **Style guide:** Microsoft Writing Style Guide. The prose uses a deliberate formal requirements voice: present-tense capability statements, explicit obligations, stable IDs, and concise acceptance bullets. Preserve that voice, named ownership, policy thresholds, technical terminology, and the distinction between product targets and demonstrated capability. The preceding structure changes are already applied; no deleted passages are reviewed here.

## Word metrics

Exact command: `uv run .agents/skills/bmad-review/scripts/word_metrics.py _bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md`.

Current total: **4,773 words**. The FR-9 body contains **292 words**. The script returned per-heading counts for the entire current document. No arbitrary length target applies.

## Findings

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| prose | FR-9: “Failed backup jobs or a newest usable recovery point older than **one hour** generate GitHub notifications to Administrator.” | “Platform notifies Administrator through GitHub when a backup job fails or the newest usable recovery point is older than **one hour**.” | Removes the awkward indefinite article before “newest” and makes the actor and two alert conditions explicit. Preserves the recipient, channel, trigger alternatives, and one-hour threshold. |

One actionable finding. No other changes are needed for comprehension. This review proposes no requirement, policy, ownership, or scope changes.
