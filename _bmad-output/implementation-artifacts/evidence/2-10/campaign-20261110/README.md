# Story 2.10 live observations — 2026-11-10

Story 2.10 remains **in-progress**. The [verification manifest](verification.json)
records completed work and the remaining gates. No candidate or tool is accepted,
and no accepted-tool pin changes are made.

The fixed caller is Parties `c9eb1fe37373b2b25d3759a4ac8f33ac46754e17`, using
the genuine signed 4.47.0 tool and Builds catalog/helper
`5ec1779040606be89306129a7d2b245258c9acd2`. Local preflight authenticates the
official package and every installed carrier file, the original producer mapping
and its five imports. Toolchain pins are Aspire 13.6.4 and Dapr CLI/runtime
1.19.1; [runtime preparation](runtime-preparation.json) verifies every Dapr archive
against the selected committed catalog. Shell configuration is unchanged.

The fresh [hosted matrix](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42727219072)
passed its baseline and all four original matrix cases. Its
[audit](hosted-matrix-audit.json) authenticates the downloaded artifact digest,
both exact lock refusals, cleanup retry, unchanged active peer and clean
finalization of five issued owners. This is a pass of the existing four-case
matrix, not the complete required eight-group table.

Separate fresh [startup cancellation](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42737966490)
and [execution cancellation](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42740407192)
each passed a fresh baseline. The parent verified the ready artifact's exact
source/run/job/owner and the real waiting step before issuing one audited GitHub
cancellation request. Their [startup](cancel-startup-observation.json) and
[execution](cancel-execution-observation.json) records preserve the actual signal
snapshot, first outcome, independent finalizer and exact cleanup retry. Startup
was `Starting` without execution/terminal outcome. Execution had live native work
and an active hold; withdrawal kept its expiry and preceded it. Both workflow
conclusions remain `cancelled` and grant no passed integration evidence.

The [local index](local-observation-index.json) and
[required-scenario ledger](required-scenarios.json) preserve each actual diagnostic
subject/controller hash. They do not claim that all cases passed under the newest
controller or reuse evidence for another decision.

| Required group | Local observation | CI observation |
| --- | --- | --- |
| Success and failure | Passed | Passed |
| Startup timeout and partial startup | Incomplete: interrupted-startup duration unavailable | Incomplete: timeout unexecuted; partial duration unavailable |
| Cancellation and collision | Incomplete: collision unproven | Incomplete: collision unproven |
| Owner finish before attachment end | Passed | Passed |
| Owner cancellation before attachment end | Incomplete: attached work/access producer unavailable | Incomplete: attached work/access producer unavailable |
| Attached failure and hold expiry | Incomplete: only hold metadata is executable | Unexecuted |
| Failure followed by cancellation | Passed | Unexecuted |
| Metadata locks, exact retry and peer | Passed | Passed |

These are lifecycle observations: **7 passed, 7 incomplete, 2 unexecuted**.
Global executing-tool/termination bindings, the final compatible Platform source
cut and complete fixed-set qualification also remain open. The explicitly split
`attached-test-execution-and-expiry-enforcement` follow-up is an unresolved
prerequisite, as recorded in the [deferred ledger](../../../deferred-work.md).
It was not waived by fixture migration or these new runs.

Earlier verifier attempts remain unchanged and failed. They exposed internal
`completed` versus public `passed`, passive expiry projection versus a raw-store
write, and inspection of an issued tool child during exit. Focused controls,
actual metadata replay and fresh affected probes verify the corrections. The
final [controller](../../../../../eng/lifecycle-qualification/local.py) has **33
passing focused tests**, authenticates exact mapping/package inputs, reaps its
issued child before inspection, and rejects unreadable live processes. Its
final [peer probe](local-attempt-4/observations.json) verifies both lock stages,
idempotent exact cleanup and the peer's real persistence recheck. The corrected
[expiry probe](local-attempt-3/observations.json) preserves raw snapshots and the
public projection separately; it supplies no supervised attached-work proof.

The [local scope audit](local-scope-audit.json) finds no inspected owned containers,
workspaces or active state files across all 17 local owners; all ten unrelated
containers preserve identity/status. The second attempt's inherited old cleanup
flag is explicitly not treated as independent process proof. The separate first
attempt [bounded process audit](local-attempt-1-bounded-process-audit.json) and
later corrected probes retain their own limits and subjects.

[Stage times](hosted-matrix-stage-times.json) and per-owner startup/execution
observations remain separate; the original startup deadline is 600 seconds
without an override. Missing actual durations stay missing. No production budget
was increased, no savings are claimed, and the optional verified CI setup-cache
optimization remains deferred. Raw logs, private endpoint credentials, descriptors
and private operational state are excluded from this retained directory.
