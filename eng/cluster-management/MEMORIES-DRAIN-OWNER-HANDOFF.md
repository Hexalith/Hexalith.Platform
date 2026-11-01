# Memories drain blocker: handoff to the publisher owner

Recovery RID `20261101t131121z` is stopped. Its failed drain is preserved, and
the separately accepted intake-only recovery completed once. The next action is
to identify the operator responsible for the unrelated EventStore workload and
obtain that owner's disposition of it. Releasing that workload does not authorize
replaying the failed drain or continuing recovery.

This is a local documentary handoff, with no live reads or operational dispatch.
The latest source/publisher observation below is historical, at
**2026-11-01T17:21:29Z**. Current availability, ownership and writer state must be
observed again before any separately approved operation.

## Recorded state and exact blocker

The [intake-restoration checkpoint](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261101t172332z-s427-intact-source-intake-restored/verification.json)
records Memories intake/worker/clock **2/1/0 ready**, Memories API 2-ready and
Redis, FalkorDB and telemetry PostgreSQL each 1-ready. Only intake was restored;
source stores and worker were unchanged. Both exact Keycloak/OpenBao isolated
targets and the CNPG watch extension remain held.

| Object | Recorded identity |
| --- | --- |
| Unowned namespace | `mcpcli-417-20261101-00c6a713`, UID `2f5b7d0a-4988-4380-a026-7b1e1c39855e` |
| Controller | Job `eventstore`, UID `4b8f43cb-7875-43a6-a4a8-0c5f007879a6` |
| Publisher | Pod `eventstore-nkkwj`, UID `d888a907-5fb9-4684-9b52-a6f827582884` |

At the last observation both `eventstore` and `daprd` containers were
Running/Ready. The earlier bounded [publisher observation](../../../../../hexalith-retirement-investigation-evidence/20261031t160723z-s427-operational-resume/continuation-20261101t140315z/memories-drain-publisher-incident/unowned-publisher-observation.json)
at **17:03:59Z** records Job parallelism 1, `suspend=false`, namespace-local
`pubsub.redis` and `state.redis` components and the `task-isolation` policy name.
Those observations establish neither permission to alter the Job nor proof that
its writes cannot affect the recovery source.

Both original drain samples counted one publisher. The accepted `mem.py`
counts **all Pod objects in every namespace** matching
`dapr.io/app-id=eventstore`; both samples must count zero. Suspending a Job,
observing empty streams, or observing a terminal container alone does not prove
that predicate. The observed Pod was active, so the recorded failure is not a
terminal-Pod false positive. No selector exclusion or acceptance relaxation is
proposed.

The unchanged failed quiescence SHA-256 is
`540dd7ef385d3ed5f43bd0ef5b227adaa1443e8c3f191ffa413814bb87247fd3`, with
`drainPass=false` and `intakePaused=false`. The intake-restoration result SHA-256
is `ea3f8b51c0cf89b9ef4187eb067b6f83f35fe5b98ae577f1bc6b91ea7ed2e595`.
It records `backupAccepted=false`, `operationalContinuationAuthorized=false` and
`externalJobMutationAuthorized=false`.

## Required response from the publisher owner

The Administrator must identify the operator responsible for this exact Job;
that ownership is currently unestablished in this recovery evidence. Obtain the
owner's disposition: it must remain, or the owner releases it under their existing
authority. Record the actual release outcome, its time and these exact object
identities. Preserve workload data and its existing custody requirements.

This handoff supplies no stop/delete command or mutation authority for that
workload. Namespace, PVC/PV, component, policy and unrelated Job/Pod changes stay
outside this recovery attempt. If the publisher must remain, record that decision
and keep recovery stopped. If its owner independently releases it under their
own authority, later native reads must still prove the complete unchanged global
publisher predicate; an owner statement alone cannot pass drain.

## Separate conditions for another recovery continuation

After the owner disposition, prepare a concrete incident proposal before asking
the Administrator to approve recovery operations. That proposal must:

1. Preserve every original signed input, point clock, failed/passed intent and
   result, both accepted corrections, failed quiescence and the single completed
   intake-only restoration. Never replay the stopped dispatcher or replace its
   canonical pre/census/pause/drain records.
2. Define a distinct create-only journal and fresh source/census/pause/drain
   records with explicit consumers and exact driver/tool/procedure hashes.
   Intake is restored, so the old pause and failed drain cannot describe a new
   outage. If the existing signed interfaces cannot express this without
   overwriting records, keep the gate closed and prepare an exact successor
   policy for its real signing/readback barrier.
3. Revalidate current source identity, complete desired content, storage and
   health; all 85 protected bindings; both held targets' exact namespace,
   workload and PVC/PV UIDs; watcher ownership; current writer/caller freeze;
   capacity, time budget, policy signatures and independent immutable readbacks.
   Preserve the original Keycloak cutoff **12:47:52Z**, durability **12:48:05Z**,
   RPO **18 seconds** and all original point/native/TLS age limits.
4. Prove the unchanged global publisher census is zero before a new pause, then
   require the original two drain samples with the **65-second wait**, every original
   equality check and every original zero-backlog/in-flight/writer condition.
   A failed, unknown or stale result stops before stores or writers are paused.
5. Bind the exact new outage, owner, remaining stages, source/intake resume,
   target retention/cleanup, failure stops and proof/manifest provenance to an
   actual separate incident decision. Any target cleanup needs its own reviewed
   exact ownership/lifecycle and absence checks. No cleanup branch is executable
   merely because this handoff names it.

The current attempt ends exclusively at **2026-11-01T23:00:00Z**. The broader
scheduling window does not extend it or any source point/proof/TLS horizon.
If fresh gates cannot pass within the remaining budget, retain the stopped
attempt and prepare a separately reviewed successor rather than extending dates.
After a later successful actual restore and clean drain, restore intact
source/intake immediately before waiting for Administrator-only signatures.
Authentic signed records/readbacks, non-dry validation and honest current M1/M2
remain required; no recovery acceptance is supplied here.

The [local reconciliation receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261101t180645z-s427-drain-owner-handoff/verification.json)
verifies retained bytes and the stopped journal only. It supplies no fresh
provider/source observation or owner/incident approval. Story **4.27 remains
in-progress**, **4.1 closed**, and **Timesheets 3.6 open/FAIL**. Fresh accepted
recovery and the separate native/node/etcd and exact retirement plan/approval/arm
remain prerequisites; no retirement, upgrade, Rancher or deployment is authorized
by this handoff.
