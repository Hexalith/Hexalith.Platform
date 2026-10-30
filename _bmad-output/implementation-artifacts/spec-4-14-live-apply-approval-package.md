---
title: 'Story 4.14: Evidence-backed live-apply approval package'
type: 'chore'
created: '2026-10-29'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '965b00249b473d602a663f1f48be39424f996337'
context: []
---

<frozen-after-approval reason="human-owned intent — explicit preparation request">

## Intent

Prepare a reviewable Story 4.14 approval package from the offline foundation on main. Resolve every required field from cited repository facts, keep unsupported values null, and record an exact owner question for each missing field. Recheck 4.1, 4.2, 4.3, 4.27 and 4.28 acceptance against sprint status and evidence, distinguishing scoped evidence from full acceptance. Run the existing offline plan renderer, foundation tests and the bounded read-only native collector. Publish exact mutation commands only if their inputs are complete; otherwise retain the renderer refusal with target, effects, verification and forward-only recovery.

All implementation is local in a scratch worktree. Preserve unrelated Protection edits and all existing evidence, statuses and gitlinks; commit the package and fast-forward main without pushing. No cluster apply, API dry-run, Helm operation, CNI probe execution or other cluster write is authorized. The foundation stays module-agnostic. Story 4.14 stays in-progress. The consuming module's Story 3.6 remains in-progress and launch FAIL, independently of Platform acceptance. Story 4.15 is next and can only be prepared offline until 4.14 namespaces exist; implementing 4.15 is outside this package.

</frozen-after-approval>

## Implementation Notes

- Reuse `eng/staging-foundation/{inputs.example.json,render.py,apply-plan.json}` without inventing values or changing renderer behavior. Preserve `DEPENDENCY-DIRECTION.md` and original frozen story intent.
- New package artifacts live under `eng/staging-foundation/`; provenance is metadata keyed by dotted input path so ordinary input values remain directly consumable by the existing renderer. Observational/test receipts live under `_bmad-output/implementation-artifacts/evidence/epic-4/4-14/live-apply-approval-2026-10-29/`.
- Reuse the existing native observer without changing its query allowlist or credential handling. It performs bounded GET/auth introspection only and retains sanitized observations.
- No unresolved implementation choice: missing operational inputs are the requested output, not assumed answers. No irreversible operation is in scope. The scratch branch starts at the recorded baseline and will be checked against the preserved main working/index state before fast-forward.

- Completed the bounded approval preparation: 2 of 37 schema fields resolved from the live CoreDNS Service; 35 remain null with exact questions. Both final offline renderer invocations refused with exit 2 and no manifests. All 34 foundation tests passed after the review corrections.
- Read-only native observation revalidated target and certificate authentication, recorded absent target namespaces and unavailable Gateway APIs, and retained sanitized evidence without exporting credentials. Source bytes are bound; a separately dated post-collection local client measurement does not retrospectively attest the original client binary.
- The 4.2 scoped exposure/auth/GC results, original-scope 4.26 qualification and completed offline 4.27 certificate prerequisite are credited explicitly; all five complete dependency gates remain unaccepted.
- Main's unrelated Protection/security work evolved concurrently. Integration uses a fresh immediate before/after state snapshot and non-overlap check, preserving the latest state rather than restoring the initial snapshot. No status or gitlink is changed, no push is authorized.
- This spec is done only for the requested approval package. Explicit user instructions keep sprint Story 4.14 in-progress, so the workflow's normal review-status synchronization is not applied. Story 4.15 remains the next offline preparation; this package does not implement it.

## Review Triage Log

Independent context-free review returned all findings before triage. Each correction is documentation/provenance within this package; no live or renderer capability was added. Operational choices and inherited definition limitations remain explicit blockers for owner review, not silently completed work.

| Finding | Verdict | Disposition and evidence |
|---|---|---|
| R1: DNS timestamp used overall completion | low | Patched both `_provenance` records to the workload command's actual `observedAt`; retained `collectionCompletedAt` separately. Initial inputs preserved with their original hash. |
| R2: Investigation receipt incorrectly described as storing Platform source hashes | low | Patched INPUT-EVIDENCE.md to point to `_provenance` and gates.assessed.json; investigation.json holds separate external-context hashes. |
| R3: Offline 4.27 completion not credited in scoped evidence list | low | Patched the list using original receipt `scopedSpecStatus=done` and 336 tests; `acceptedForLiveApply=false` remains. |
| R4: Generated denials described without full coverage limits | medium | Main package now states missing arbitrary data workload/PVC and TLS ConfigMap writes and incomplete cross-layer coverage. Complete reviewed checks are required; no unknown principal or command is invented. |
| R5: Positive RBAC permission evidence omitted | medium | Added an exact additional owner question and required positive layer/effective-permission evidence before live approval. |
| R6: Staging-only StorageClass gap missing from primary handoff | medium | Main package and static preconditions now require reviewed enforcement/evidence for other class/default/static binding paths. Existing production-only quota is not claimed sufficient. |
| R7: Namespace/policy interval and partial recovery exposure | medium | Main package and preconditions require workload deployment authority withheld until both layers and complete isolation checks succeed, including partial recovery. |
| R8: New PriorityClass recovery name cannot be supplied to renderer | low | Recovery now explicitly requires a separately reviewed offline definition/renderer/reference update because the name is hardcoded. No immutable value update is proposed. |
| R9a: Observer source binding absent | false | native-collector.json already had `sources` with the unchanged observer SHA-256 before review completed. The claim was checked against the actual receipt. |
| R9b: Original client version absent | low | Original version remains honestly unrecorded. Added local post-collection version/binary metadata and timing limits outside the foundation; no new cluster call or backdated assertion. |
| R10: Future read-only commands did not reproduce TLS constraints | medium | Commands now pin server, verification and IP SAN; accompanying text requires intended CA/identity and no proxy/TLS override and distinguishes the sealed-context collector. |
| R11: Module-boundary test scope not specified | low | Main package identifies the four historical unit-test files and separate preparation validation of new artifacts, with a retained verification receipt. |

## Verification

- Final local plan/render attempts: expected exit 2, empty stdout, 35 missing fields, no manifest directory or API call. Earlier preparation attempts and their exact input/static-plan snapshots are retained.
- Foundation suite after corrections: 34 tests passed, zero failures or skips; see evidence `foundation-tests-reviewed.json` and stderr transcript.
- Package validation checks all required fields/questions, per-field source paths/pointers/hashes, dependency sources/statuses, package module independence, local links and native command allowlist. Evidence is retained under `evidence/epic-4/4-14/live-apply-approval-2026-10-29/` beneath the implementation-artifacts directory.
