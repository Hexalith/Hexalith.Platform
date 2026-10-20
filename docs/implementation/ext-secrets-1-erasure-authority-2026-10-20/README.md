# Erasure-authority investigation evidence — 2026-10-20

Supports [the decision](../ext-secrets-1-erasure-authority-decision-2026-10-20.md). These are source/metadata/research observations, not physical qualification or an acceptance packet.

- `source-bindings.json`: exact reviewed Platform candidate, EventStore gitlink, inspected sibling revisions and input hashes. `git-blob` entries bind original immutable source, including the pre-update architecture spine; they do not hash the newly edited output. Observed-file entries bind retained public evidence bytes only.
- `infrastructure-observation.json`: exact read-only Kubernetes command arguments and selected metadata. No Secrets/configuration/credential values were requested. One observed node bounds the replica-independence claim.
- `version-observation.json`: publisher NuGet version-feed references, source SDK and local installed runtime observations. A current published version is not a successful build/compatibility test.
- `VALIDATION.md`: final documentation, source-binding and independent-review results; no model, native experiment, deployment or owner Live result is claimed.
- `SHA256SUMS`: portable checksums for this evidence directory and the final decision. Run `sha256sum --check SHA256SUMS` from this directory.

The retained negative control remains separately bound in the [2026-10-18 evidence](../ext-secrets-1-option-a-feasibility-2026-10-18/README.md). Ordinary Raft recovery demonstrated revival. Nothing in this bundle reverses that result or proves a new provider.

The first draft used a registered worktree. An unrelated concurrent `/pushall` removed that worktree twice and changed Platform main; drafting continued in a separate detached checkout rooted at the reviewed commit. No other session's branch/commit/work was undone. The final artifact set is documented in VALIDATION.md.

## Owner adoption and Dapr correction

The owner approved the recommendation, then required Dapr infrastructure abstraction with no application infrastructure dependencies. The current decision/spine/spec adopt that corrected boundary; no native-adapter exception or mandatory cloud stack is approved. `OWNER-APPROVAL.md` records the effective scope. `approval-basis/` retains the exact original proposed decision/spine and its historical manifest; those bytes are evidence, not current build instructions, and retain their original relative-link base. Do not run the pre-approval manifest against current files.

`dapr-boundary-research.json` and `dapr-bindings-v1.18.1.proto` bind the supported output-binding protocol. `review-20261020-dapr-*` reports are the current Dapr adoption reviews; earlier review reports retain the prior proposal's history. The current checksum manifest covers both historical input snapshots and final adopted outputs. No installed/qualified semantic custody component was discovered or created by this documentation work.
