---
title: 'Next ten Platform deferred items'
type: 'bugfix'
created: '2026-11-24'
status: 'in-progress'
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

- [ ] Use the resolved status on exactly ten newly completed entries and retain their evidence and separate commits.
- [ ] Given the committed batch, when the solution, applicable repository suites and diff checks run, then the new requirements and behavior are verified.
- [ ] Given successful verification, when main is pushed, then the remote contains the full batch.

## Implementation Notes
