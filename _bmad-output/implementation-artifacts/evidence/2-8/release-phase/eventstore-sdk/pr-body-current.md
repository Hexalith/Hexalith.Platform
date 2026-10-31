Native module integration suites need exact synthetic event-stream observations through
their run-owned Redis endpoint. Add the shared SDK reader, reject foreign tenants before
opening persistence and use the centrally pinned Redis dependency.

The branch includes current owner main 484f4bd82fd2ef4b6c2653fdde57feeeb6d85a3c through
a normal merge. Head 2ed2f03346d11c925104f4bfc31ac747182a3dc8 differs from that base in
exactly three SDK files; all other owner files are preserved.

Validation: zero-warning Release build and all 15 descriptor controls pass. Refreshed
full CI run 41236793431 passes. The required live-sidecar check in run 41236793296
passes with 304 tests passed, zero failed and four existing skips. Its serialized
execution metadata control also passes 1/1. All seven current-base required contexts
remain mandatory before merge.

The published-compatibility job in run 41236793296 reports success, but its actual test
result is 49 passed, one failed and zero skipped. Published31282QualificationMatrixAsync
reports 74 failed/unverified observations among 239 historical package/source lanes.
The unchanged integration workflow pipes dotnet to tee under default bash without
pipefail, masking the test exit. This is not passing package qualification evidence.
The historical matrix and workflow are unchanged by these three SDK files. No failure
has been waived and no test expectation or protection changed.

SDK publication remains held for exact source/cohort reconciliation and valid release
qualification. Caller activation, workflow/tool pins, manifests and solution enrollment
remain separate steps. This PR grants no hosted topology or candidate acceptance.
