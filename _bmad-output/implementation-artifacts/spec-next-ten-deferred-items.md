---
title: 'Next ten Platform deferred items'
type: 'bugfix'
created: '2026-11-24'
status: 'in-review'
route: 'dispatch'
baseline_commit: '32fafb5823c141f5ba1ec9d0880d76a9454600ca'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Take the next ten actionable entries from the Platform deferred-work ledger in its listed order. Implement each in a separate commit, with applicable tests and a recorded resolution. Record older items that are stale or blocked, then verify the full build and test suite and push the completed work directly to main.

</frozen-after-approval>

## Code Map

- The oldest entries in [the ledger](deferred-work.md) refer to the Epic 4 planning documents, recovery evidence under external custody, and the Platform EventStore host and identity services.
- The architecture spine and canonical epics own the recovery credential and tier requirements. Epic 4's generated context carries approved constraints forward.
- The EventStore seed host is covered by the Composition tests. Identity admission runs behind `AddPlatformIdentity` and has an existing Identity test project.

## Tasks & Acceptance

- [x] Use the done status on exactly ten newly completed entries and retain their evidence and separate commits.
- [ ] Given the committed batch, when the solution, applicable repository suites and diff checks run, then the new requirements and behavior are verified.
- [ ] Given successful verification, when main is pushed, then the remote contains the full batch.

## Implementation Notes

- The ledger has no priority field. This pass used its listed order. Each new implementation commit records its own resolution. Obsolete entries carry a superseded status, and blocked entries stay open with the required input recorded.
- The Release build used the published Builds 6.7.0 tool, verified against its NuGet repository signature, package lock and executable revision. The older published 6.6.2 pin had diverged from the recorded Builds gitlink and omitted the v2 API used by AttemptStore. The repair regenerated the tool lock, owner package lock, declarations and candidate provenance from the released catalog.
- The G87 HTTP test runs under the Production environment with a synthetic HTTPS authority. Requests at the inherited limit reach body processing, an oversized request receives 413, and Dapr actor routes carry their prior bounded limit for larger serialized envelopes.
- Verification found a stale offline planning evidence path, an overly broad Platform-only package-version declaration and two test fixture issues. The repairs restore the historical planning bytes, scope the S3 pin to AttemptStore, exercise the real .NET 11 HTTP callback, and synchronize the validation-worker test before advancing its clock.
- The historical public cohort uses the EventStore declaration bytes from its recorded owner revision. The current cohort uses the upgraded declaration. Both are checked against the corresponding provenance.
- The ledger records ten completions, seven superseded entries, and five blocked entries. It retains 516 open or partially resolved entries, including the five blocked entries.

## Verification

- The complete Release solution built against the published 6.7.0 tool. The current tuple and signed package locks verified. The repository's Python, Node and Go suites and the standalone catalog checks passed after correcting their tool and fixture inputs.
- The five gateway host tests, 165 Identity tests, 28 package-catalog regressions, 185 Works tests, and the corrected Custody rollover case passed. Works used the sibling checkout selected for EventStore 5.5.1.
- The full-suite gate remains open. The current-owner Composition cases exceeded preparation deadlines. The Secrets Runtime and Protection runs also reported failures in time-bounded archive and receiving operations. The shared verification host had more than 100 runnable tasks for 14 CPUs during these failures.

## Review Triage Log

| Finding | Verdict and action |
| --- | --- |
| Standing access bypassed recovery-point leases | High. Removed standing reads from AD-7 and Story 8.9 and required the lease before reading the recovery point. |
| An executor could retain a reusable decryption key across epoch loss | High. Custody now mediates decryption under the active lease without exporting the key. |
| Two clusters could share a resource identity tuple | Medium. The tier inventory includes cluster identity. Explicit persistent-resource ownership takes precedence for generated resources. |
| Broad RBAC ownership in the Epic 4 context conflicted with AR-32 | Medium. The context carries the shared-grant rule and the capability rule for namespaced resources. |
| The restored runner evidence omitted credential absence | Medium. Story 4.3 context requires both credential absence and network denial. |
| The ingress summary omitted the data namespace and direct Ingress and Traefik refusal | Medium. The context now includes both controls. |
| Canonical 4.27 dependencies and the context could omit separate recovery or closure evidence | High. The operations handoff, canonical epic and context now agree on accepted console closure, current signed 4.0 proof and the fresh 4.1 fenced restore. |
| Verified Rancher identity could differ from the input ultimately applied | Medium. The runbook rechecks the exact install and import inputs against the verification record. |
| A 1 MiB global default could reject the larger Dapr actor envelope | High. Preserved the bounded actor callback limit and verified its endpoint metadata. |
| TestServer options alone could miss a deployed HTTP regression | Medium. The Production loopback test exercises the real Kestrel boundary, including the exception-handler response. |
| The gateway tests treated missing and invalid operator proof as one condition | Medium. The matrix now exercises both, separately from an unlisted provenance issuer. |
| Gateway tests did not detect removal of the target activity check | Medium. The matrix includes an inactive target with a current revision. |
| The hosted tuple omitted the Identity regression cases | Medium. The workflow runs the Identity tests selected for the gateway and enrollment paths after the three Release tuple suites. |
| Default diff checking rejected CRLF Identity changes | Low. Added the existing per-file whitespace rule for preserved line endings. |
| The readiness crosswalk did not identify the old story numbers | Low. The crosswalk distinguishes the merged candidate and hold policy from the new runner-contract story. |
| The separate Tenant Registry host has the old request-size configuration | Medium. Existing ledger entry R5-B5 retains the Tenant Registry's separate release and verification work. |
