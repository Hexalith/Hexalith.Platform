# Identity-event capture-lag measurement proposal

Status: **preparation proposal; no numeric bound or measured result is approved**.

The user selected a measured proposal while applying the Story 9.2 prerequisite
recommendations on 2026-11-16. This document prepares that work for Platform 4.9;
it does not change the approved AD-26 package or authorize an infrastructure run.

## Ownership and current inputs

[Story 4.9](../../_bmad-output/planning-artifacts/epics.md#story-49-ratify-the-hosted-runtime-and-identity-event-contracts)
owns the finite maximum, measurement point, accountable owner and revisit
condition. Platform with Administrator decides the policy. Proposed operational
owner: Platform Maintainer `@jpiquot`; actual acceptance must record ownership.
[Story 4.19](../../_bmad-output/planning-artifacts/epics.md#story-419-generate-the-staging-realm-and-register-staging-clients)
owns the staging realm and bounded off-cluster event export; no exporter is
supplied by this proposal.

Focused source inspection found no implemented admin/user-event exporter,
durable export checkpoint or capture-lag telemetry in the current Platform
`src/` and `eng/`. Existing private OIDC/recovery tools establish different
facts. Their observation timestamps do not measure event occurrence or capture.
Stories 4.9, 4.19 and the relevant Epic 8 work remain backlog.

Before measurements, the execution plan must identify the exact source realm,
event configuration, exporter/version, off-primary sink and failure-domain
placement, durability/readback/checkpoint mechanism and supported source event
identities. It must name representative load, fault and execution budgets,
retention, cleanup deadline and target-scoped credentials. These are inputs to
future qualification, not defaults selected here.

## Proposed timing contract

Measure each uniquely correlated relevant admin/user event from the source's
documented authoritative occurrence/commit point to independently observed
durable capture outside the primary failure domain. End only after the sink's
documented durable-commit condition is satisfied, independent readback proves
the event contents, and export checkpoint/reconciliation proves older expected
events were not skipped. An exporter send, successful HTTP response or
newest-event timestamp alone is insufficient.

If source commit time is not observable, a controlled exercise may instead
measure the conservative interval from before request submission through durable
readback. Label that interval and its limits explicitly; do not silently present
it as measured source-commit lag. Correlation and coverage must match the actual
export protocol; this proposal invents no globally ordered Keycloak sequence.

Retain the source event identity/time, client request/ack brackets, applicable
queue/retry observations, sink commit/readback and checkpoint evidence, UTC
clock sources and uncertainty, and same-process monotonic elapsed time with
boot/process identities. Unknown clock comparability or missing coverage makes
the measurement inconclusive. Negative or inconsistent intervals cannot pass.
Use existing clock evidence and instrumentation where sufficient; add no new
clock service for this exercise.

Track the oldest known uncaptured event throughout backlog, retry and restart.
Restarting the process or retrying delivery does not reset an event's age.
Quiet periods and heartbeat success cannot advance a checkpoint across missing
events. Report unavailable observations as unavailable rather than zero lag.

## Small qualification matrix

| Case | Exercise on isolated targets | Required observation |
| --- | --- | --- |
| Normal | Generate correlated relevant admin and user events | Every expected event is durably captured; timing and checkpoint agree |
| Backlog | Apply owner-declared representative load | Queue/oldest-event age and complete catch-up; no skipped older events |
| Retry | Introduce a bounded transient delivery failure | Original ages survive retry; duplicates/reordering do not lose events or falsely advance progress |
| Restart | Restart the run-owned exporter around commit/checkpoint boundaries | Recovery replays safely and retains the complete expected set and original timing |
| Unavailable | Make a named source/exporter/sink observation unavailable | Explicit inconclusive/failure record; no stale or zero-lag pass; complete catch-up after recovery |

Restarting the identity provider or sink is unnecessary for the initial exercise;
any later such case needs an isolated target and an explicit execution plan.
Percentiles are useful diagnostics, but neither an average nor a percentile
establishes a maximum capture-lag policy.

## From observations to a policy decision

Retain sanitized raw observations, exact identities and commands, completeness
checks, clock uncertainty, interruptions and cleanup results in the future
run's evidence directory. Do not discard failed or inconclusive cases when a
later run succeeds.

Platform proposes a finite maximum using the observed end-to-end upper bounds,
operational headroom and the acceptable loss exposure; Administrator records
the decision with its measurement point, owner and revisit conditions. The
decision must explicitly state which observed fault envelope it covers. Missing
delivery or timing evidence cannot be compensated for by selecting a larger
number. Until this decision exists, no numeric compliance claim is available.

Revisit after source/exporter/sink or durability/placement changes, changed load
assumptions, clock uncertainty, a missed event, or a breach. A breach notifies
Administrator and the deputy under the existing policy. This proposal creates
no promotion-stop setter or clearance rule.

The accepted loss window concerns **non-admission identity-provider removals**.
Signed admission authority and recovery reconciliation retain their independent
revocation rules. Neither the currency policy's 30-day window nor a recovery
monitor cadence supplies this capture-lag limit.

## Execution and cleanup boundary

Use a synthetic isolated realm and run-owned sink prefix with realm-scoped
writer credentials and an independent read-only observer. Restrict faults to
those resources; never reuse incident recovery credentials or production realm
targets. Record only public identities and credential references in retained
results. Keep sensitive material and disposable jobs beneath the private
`TMPDIR` supplied by `run-with-temp`, and preserve useful sanitized logs outside
it. Wait for child work and verify removal of run-owned identities, objects and
processes within the declared cleanup bound.

This proposal can be prepared alongside EventStore's schema construction.
Measurements depend on the actual export path and an executable isolated plan;
the final policy belongs in
`docs/contracts/identity-event-capture-lag-v1.md` after decision. Preparing this
document completes none of the inventory, exporter, drill or currency-check
acceptance criteria.
