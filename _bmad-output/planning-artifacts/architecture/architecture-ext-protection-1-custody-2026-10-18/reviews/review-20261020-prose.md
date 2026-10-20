# Prose review — 2026-10-20

This document exists to help Platform, security and operations owners approve or reject the proposed erasure construction and let implementers build the approved boundaries without changing existing custody contracts.

Reader type: humans. Microsoft style baseline. The preceding structure pass’s two moves have been applied; it marked no passages CUT. This dependent pass preserves the resulting structure and all technical choices.

The intentional voice is terse, technical and directive. Preserve exact contract/status names, decision/boundary IDs, key notation, numerical bounds, explicit Unknown results, and the distinction between recommendation and acceptance. The short caveats and repeated qualification gates serve the operational audience. No stylistic expansion of the protocol tables or diagrams is proposed.

Exact word metrics for this reviewed version: **4,896 words**, from `uv run .agents/skills/bmad-review/scripts/word_metrics.py docs/implementation/ext-secrets-1-erasure-authority-decision-2026-10-20.md`. No length target was supplied. Code blocks and structural markup were excluded from copy-editing.

No blocking prose issue. The following small fixes address ambiguity or awkward phrasing rather than author preference.

| Pass | Original Text | Revised Text | Changes |
| --- | --- | --- | --- |
| prose | §Exact owner approvals and provisioning returns: “No values/private keys belong in the return packet.” | “No secret values or private keys belong in the return packet.” | Clarifies that public account IDs, resource references and hashes requested immediately before this sentence are allowed values. Preserves the prohibition on secret material. |
| prose | §Authority model and transactions: “All validated facts bind the prior checkpoint hash; CAS failure recomputes from current state.” | “All validated facts bind the prior checkpoint hash. On CAS failure, recompute the proposed checkpoint from the current state.” | Gives the recomputation an explicit object; a failed comparison does not itself perform work. “Proposed checkpoint” also avoids suggesting that an original request identity is regenerated. |
| prose | §Exact components and new deployment/storage classes, new-service row: “Versions reality-checked; new service is unimplemented.” | “Published versions verified; the new service is unimplemented.” | Replaces an informal, vague verification phrase with the actual published-version claim. The following compatibility limitation remains intact. |
| prose | §Investigation and verified primitives: “This point observation is not health qualification.” | “This metadata snapshot does not qualify service health.” | Makes the subject and limitation immediately understandable to an operator. No new health assertion is introduced. |
| prose | §Private protocol fields and first-release bounds: “The container carries source-owner-authenticated guard/protection receipts; its assembler cannot mint ordering authority.” | “The container carries guard and protection receipts authenticated by their source owners; its assembler cannot mint ordering authority.” | Unpacks a dense compound without changing which owner authenticates each receipt or the assembler’s authority limit. |
| prose | §Private protocol fields and first-release bounds: “A 1,000-target backend test does not establish a 1,001-root actor.” | “A successful 1,000-target backend test does not qualify an actor with 1,001 registered roots.” | Replaces an unclear shorthand with the exact capacity distinction being made. The existing hard limit remains unchanged. |

Six local suggestions; no additional structural move or technical revision is requested. The prior structure pass proposed zero reduction, and this prose pass has no shortening target. Retain all owner approvals, failure conditions, experiment bounds and assurance classifications.
