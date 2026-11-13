# Story 4.17 — candidate-01 startup evidence reconciliation

Rechecked on 2026-11-13 from Platform
`d5928c8b106e1da4179e55dbe8390912b02a7e44`. **The exact cause of run
[43097161643, attempt 1](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43097161643)
remains unresolved. Provider disruption is strongly indicated; dispatch-ref deletion
while queued remains a possible contributor. No Platform workflow defect is
demonstrated, so there is no evidenced Platform code correction to make.**

Fresh attempt-specific API reads confirm `startup_failure`, zero jobs, zero artifacts
and no prior attempt. The original receipts still record **0/24 observed vectors**,
null capture/verifier argv and exits, and incomplete packet/cleanup assessment.
The [assessment](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-reconciliation/assessment.json)
and [new evidence manifest](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-reconciliation/file-manifest.json)
record this continuation. No dispatch, rerun, cancellation, push or ref change occurred.

## Cause and evidence limits

The retained [startup investigation](story-4-17-broker-candidate-01-startup-investigation.md)
found 43 zero-job queued runs across six repositories during the same window. GitHub's
Actions incident `dvy0t3mvh1lg` was posted at 20:48:12Z, after this run's terminal
update at 20:41:54Z. This supports correlation with a provider disruption; it does
not prove this run's exact failure mechanism. GitHub supplied only an unexpected
service-error annotation and support request `4ed78a69-de10-11f1-8d56-5705d8f2f5ff`.
The CLI's generic workflow-file hint supplies no additional diagnosis.

Repository activity records the dispatch branch's deletion at 20:39:42Z while the
run was queued. The deleting process and whether deletion caused termination are
unknown. Do not attribute deletion to `/pushall` merely because its pruning rule
could delete a merged branch. No change to that procedure is justified by this
investigation. An exact provider-side explanation still requires GitHub Support;
this continuation sends no support message.

The separately accepted [candidate-01 execution 2](story-4-17-broker-candidate-01-hosted-attempt-2.md),
run `43333995506`, allocated in five seconds using identical source, workflow bytes,
label and event. This contradicts a deterministic rejection of those workflow bytes.
It does not distinguish provider disruption from ref deletion or exclude transient
registration/service problems. That execution's acceptance is also consumed.

## Preserved execution identities

| Item | Exact identity |
| --- | --- |
| Platform execution source | `94001fad925fdf7fa03a90b507b8f8e19074f9e3` |
| Workflow blob | `9130ed4243e3961326e984e8fd22e49b70695aa4` |
| EventStore source pin | `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` |
| Candidate-01 configuration SHA-256 | `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc` |
| Explicit profile | `deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json` |
| Profile SHA-256 | `1c1da59176f78b9b60a87ba85b6076b8d215ff912840ecb52bdb7432497c587f` |
| Profile inputs / vectors | 91 / 24 (both brokers, 12 scenarios each) |

The configuration's declared Platform revision remains the unauthenticated
`6d422033b959c249c149a4cb3785704e9eebe03c`; it is separate from GitHub's source binding.
All original evidence files are unchanged. Manifest rechecks verified **155/155
commitments (868,243 bytes)** across the original hosted, startup investigation,
execution-2 and packet-diagnosis evidence; the original hosted subset is **79/79
(151,443 bytes)**. This verifies retained bytes, not broker guarantees.

## Separate EventStore concerns

At session start, the shared checkout was
`f2062f7f7bd8b3dcae946ba209b933ad8e610ff2`; Platform's baseline gitlink is
`bb9e2b51fc8a609181077eba2ae900bcbe40745a`. Neither is attempt 1's source.
An isolated archive of the session-start checkout has four mismatched inputs against
the unchanged original profile: the runtime-qualification workflow,
`qualify-internal-actor-admission.py`, `run-dapr-runtime-lane.py` and the actor-admission
test. Original-profile admission refuses with exit 1. Its full expected/actual hashes
are retained in `local-checks-final/admission-session-start-checkout.stdout`.
An isolated archive of **02a4d3b2** admits all 91 inputs and the original JSON, exit 0.
No historical lock, profile, source pin or runtime/dependency pin was refreshed here.

Concurrent work advanced the shared checkout during the first audit. Its final
consistency assertion refused the result; all logs and the exact script version are
retained under `local-checks-01`. A collector path error on the next invocation is
retained under `local-checks-02`. The final successful audit uses immutable archives
for both admissions; no shared checkout supplies admission bytes. It observed the
shared checkout at `d457ecd51a17b8ecc0c678b02d19d7ee92c13141` at start and end.
This is another session's state, not a pin update made here.

Execution 2's pinned verifier also has a separately demonstrated false positive:
Kafka topic creation's `docker exec ... --config ...` is interpreted as a daprd
launch. All three verifier modes refuse before vector assessment. That defect is
in EventStore and cannot explain a failure before any job allocation. Its 24 producer
observations and cleanup receipt remain uncredited. No verifier or packet is run
again in this continuation.

Fresh workflow inventory additionally observed independent candidate-02 run
`43386723091` at source `4d66ff5584e4f62239551bb02311649334171a84`, first active,
then completed with failure. Its changed inputs, packet, acceptance and results
are outside this investigation; they do not replace candidate-01's identities.

The later review tightened the evidence collector and proposal only; it did not
identify a Platform startup defect. The successful reviewed audit and fresh
per-request observations are in `local-checks-reviewed/` and
`github-reads-reviewed.json`. Earlier audit receipts remain unchanged.

## One further execution proposal — no acceptance established

The [concrete proposal](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-reconciliation/next-execution-proposal.json)
defines **candidate-01 execution 3**, a new run at GitHub attempt 1. It reuses the
existing immutable tag `qualification/broker-candidate-01/attempt-2-94001fad` solely
as an exact source reference. It creates no tag and carries no dispatch inputs.
The tag's historical name supplies no execution permission. Fresh reads confirm its
source and the active update/deletion restrictions of ruleset `24657570`.

The proposal preserves every identity above and requires a **new exclusive
GitHub-hosted `ubuntu-24.04` VM** for job `candidate-01`. Its allocation must bind
`NEW_RUN_ID-candidate-01-1`, a local Docker socket, daemon ID and socket device/inode.
Record the actual job, runner and image identities after allocation. No worker is
allocated now, and execution 2's worker cannot be reused. The existing workflow
already enforces these guards, a 40-minute timeout and read-only contents permission.

Separate execution acceptance must bind the committed proposal and its SHA-256,
one dispatch, the exact inputs, the worker contract, artifact retrieval, three
independent pinned verifier modes and the 30-minute no-job handling. All active
candidate executions must be terminal, the provider healthy, and source/tag/policy
checks fresh before execution. No automatic retry is permitted. Without explicit
cancellation acceptance, a run still unallocated at 30 minutes is left queued and
reported; its single dispatch scope remains consumed.

Before dispatch, the accepted operator must retain a timestamped run inventory.
After the single dispatch, exactly one new run must match workflow, source, ref,
event, attempt and dispatch time. An ambiguous match stops for owner resolution;
it never permits another dispatch. A queued run remains the operator's monitoring
and retrieval responsibility if it allocates later, with an accepted handoff if
the session cannot continue. Archive inspection refuses the entire archive before
extraction on any unsafe member; the proposal lists the exact refusal conditions.

This repeat has limited value: it can observe startup and capture again. The
unchanged verifier is expected to repeat its sidecar-binding refusal if a comparable
packet reaches Kafka topic creation. It cannot promise verified vector results,
verified cleanup, the workflow's exact expected authority rejection or bounded
technical success. Retain every vector and actual phase result; no PASS is assumed.

## Commands and verification

All provider requests in this continuation were `GET`. The connector supported
attempt/job/artifact/ref/ruleset reads; it rejected the workflow-run-list URL with
`INVALID_ARGUMENT`. The authenticated CLI supplied that read-only inventory instead.
`github-reads.json` records the earlier connector arguments and projected responses;
its one timestamp is document time. `github-reads-reviewed.json` adds fresh per-call
start/end UTC observations, including the ruleset's explicit `target: tag`.
`local-checks-reviewed/local-commands.json` records every subprocess argv, cwd,
start/end UTC, exit and stdout/stderr digest, including Git identity reads and the
exact `gh api --method GET ... --jq ...` fallback. The earlier
`local-checks-final/local-commands.json` records 16 selected child checks.

The reviewed local audit invocation was:

```bash
python3 -I _bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-reconciliation/recheck_local.py \
  /home/administrator/projects/hexalith/platform/references/Hexalith.EventStore \
  /home/administrator/projects/hexalith/platform/node_modules \
  _bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-reconciliation/local-checks-reviewed
```

It completed 27 recorded subprocess checks. Existing workflow controls passed **53/53**;
actionlint **1.7.12** found no issue on either exact execution bytes or identical
baseline bytes (shellcheck/pyflakes integrations explicitly disabled). Both archive
admission results and all manifest checks matched the expectations above.
`git diff --check` passed. These are offline workflow/admission checks, not 24 broker
executions. No new broker vector ran.

The collector now refuses an existing output destination, so a rerun must name a
fresh directory. A negative probe confirmed refusal without altering prior output
bytes. It extracts configuration directly from the execution revision and asserts
its SHA-256, requires identical workflow bytes, and asserts the four exact drift
paths plus the specific admission refusal. `verification-final.json` retains the
non-overwrite probe and final consistency checks.

Platform 4.17 and EventStore 3.21 remain **in-progress**. All five historical failed
guarantees remain **REJECTED**, selected broker **null**, production eligibility
**FAIL**. No new execution is authorized by this report or its proposal.
