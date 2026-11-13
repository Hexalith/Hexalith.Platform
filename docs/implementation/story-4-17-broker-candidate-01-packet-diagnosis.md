# Story 4.17 — candidate-01 attempt-2 packet diagnosis (2026-11-13)

Status: **offline diagnosis complete; two demonstrated defects fixed on an unpushed EventStore
branch; candidate-02 proposed, not authorized.** Platform 4.17 and EventStore 3.21 remain
**in-progress**. All five historical guarantees remain **REJECTED**. Broker selection is **null**
and production eligibility is **FAIL**. No vector, cleanup or authority result is credited.

Machine-readable records govern this summary. They are in
[`evidence/4-17/candidate-01-packet-diagnosis/`](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-packet-diagnosis/):
`vector-diagnosis.json`, `validation-results.json` and `candidate-02-proposal.json`. The
[candidate-02 proposal](story-4-17-broker-candidate-02-proposal.md) holds the next owner decision.

## Sources

| Item | Value |
| --- | --- |
| Packet | artifact 13995674927, `platform-broker-candidate-01-43333995506-1`, run [43333995506](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43333995506) |
| Packet source | Platform `94001fad925fdf7fa03a90b507b8f8e19074f9e3`, EventStore `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` |
| Platform baseline | `aa921804b5e3fcf698bf333f40ec50c390646692` (local main; `b1fe4cb5` and `aa921804` are unpushed) |
| EventStore base | `bb9e2b51fc8a609181077eba2ae900bcbe40745a` (Platform gitlink; broker tools byte-identical to `02a4d3b2`) |
| EventStore branch | `fix/3-21-candidate-01-verifier-and-pressure-retention`, tip `495dd24943031ec75e2822c6168ada3c8b9fdb82` (local, unpushed) |
| EventStore remote main | `f2062f7f7bd8b3dcae946ba209b933ad8e610ff2`. It advanced during this slice; a dry `git merge-tree` with the branch is clean, and no lock input changed |
| Pinned Dapr RabbitMQ component | `deploy/dapr/qualification/public-identities/1.19.1/components-pubsub-rabbitmq-rabbitmq.go`, SHA-256 `0ca12e7d…7632e` |

## Packet re-download

The artifact was downloaded again into a fresh scratch directory (`redownload_and_inspect.py`):

- 2,666,335 bytes with SHA-256 `906c0d943e7a0ee8393bb83b96c4c917477fe18820afe37fe6ba7274afe7aeb7`, matching both the expected value and the API digest;
- all 575 entries were inspected before extraction: no unsafe path, link, special file, encryption, duplicate, case collision or private transcript, and every CRC matched;
- every extracted file is byte-identical to the committed `artifact-extracted-digests.json` (575/575).

Nothing from the archive was executed or imported. The packet bytes stay outside Git. The
artifact expires on 2026-12-13T00:11:32Z.

## Diagnosis

`diagnose_vectors.py` recomputes each claim below from the packet's receipts, the producer's
retained sidecar logs and the Kafka container log, and asserts each one. Receipt numbers are
indexes into `packet/commands.json`.

| Vector | Failing predicate | Class | Cause | Key receipts |
| --- | --- | --- | --- | --- |
| rabbitmq/durable-quarantine | blocked: readback timeout | **Genuine broker behavior** (candidate-01 configuration) | The poison publish hit a publisher whose channel the previous vector's broker restart had closed. With a 500 ms timeout below the 2 s reconnect wait, it can never reconnect | 128 restart, 136 ready, 149 HTTP 500 at 1.90 s, 150–373 polls, log 1478 |
| rabbitmq/retention-expiry | blocked: readback timeout | **Genuine broker behavior** (candidate-01 configuration) | Same mechanism after failed-diversion's broker restart: both setup publishes returned 500 | 396 restart, 404 ready, 416 and 417 HTTP 500 at 1.90 s, 647 readback, log 1483 |
| rabbitmq/secret-rotation | `oldGenerationDenied` false | **Genuine broker behavior** (code-level) | The only call that could return ACCESS_REFUSED is the reconnect dial. Under candidate-01 it never runs, and under any setting its error is discarded | 718 delete_user, 720 users, 721 HTTP 500 "context deadline exceeded", log 1487 |
| rabbitmq/cancellation-backpressure | `acknowledgedLoss` 3, `negativeConfirmRejected` false | **Genuine broker behavior** (code-level) | The broker NACKed three publishes; the component logs the NACK and returns 204 | 742 policy, 743–750 all 204, 753 cancelled 599, 1051 readback, log 1492 (three NACKs) |
| rabbitmq/missing-bindings | unbound publish 204 | **Genuine broker behavior** (code-level) | Publishes use `mandatory=false`, so an unroutable message is confirmed | 1054 control, 1057 and 1061 absence measured, 1058 unbound 204 |
| kafka/cancellation-backpressure | `acknowledgedLoss` 2 | **Harness/timing defect** (fixed) | The harness's own 60 s topic retention deleted both acknowledged records before the consumer restarted | 1374–1375 204 at 00:09:59.33; log 1515 deletes segment 0 at 00:10:59.820; 1395 consumer at 00:11:04.29; earliest offset 0 (1385) → 5 (1453) |
| kafka/cancellation-backpressure | `negativeConfirmRejected` false | **Genuine broker behavior** | Produce quotas throttle and never reject: six 9.40 s timeouts, each later appended three times | 1373 quota, 1376–1381 HTTP 500 at 9.40 s, 1455 log end 20 = 2 + 6 × 3 |

No failure is a verifier gap. The only verifier defect was the integrity false positive that
rejected the whole packet.

### RabbitMQ publisher reconnection (durable-quarantine, retention-expiry)

The pinned component's `Publish` (`rabbitmq.go:302-336`) handles a closed channel this way: it
logs "publisher is reconnecting in 2s", then waits for `ReconnectWait` *or the request context*,
and only after the wait calls `reconnect` (lines 321-328). Candidate-01's resiliency timeout of
500 ms always ends the context first. Each attempt therefore returns before reconnecting: 3 ×
500 ms plus 2 × 200 ms backoff gives 1.90 s, then HTTP 500 "context deadline exceeded". In logs
1478 and 1483, every publish produced a reconnect warning and none produced a "connected with
connectionCount" line. The guarantee under test was never exercised. The harness then waited
45 s for an effect that could not arrive.

This is real behavior of the candidate configuration, not a harness defect. Both vectors passed
with the default 1 s and 3000 ms in the 2026-11-10 hosted runs. Restarting the publisher after
each broker restart would hide a liveness regression rather than fix one, so the harness is not
changed. Its generic "bounded readiness/readback condition not met" message hides the setup
publish's 500. That is a diagnosability gap only and is left as is.

### RabbitMQ rotation, pressure and missing binding

- **Rotation.** The reconnect's error is discarded (`_ = r.reconnect(...)`, line 328). So
  ACCESS_REFUSED from a deleted user can never become the publish error. Candidate-01's two
  RabbitMQ knobs are therefore inapplicable to this guarantee. The stale response moved from the
  harness's 599 (2026-11-10) to Dapr's 500 "context deadline exceeded", which
  `attributable_denial` correctly rejects.
- **Pressure.** `publishSync` logs "did not receive confirmation of publishing" and returns `nil`
  (lines 290-299). The 2026-11-10 record lists the upstream fix as unreleased; that was not
  re-checked in this offline slice.
- **Missing binding.** The publish passes `mandatory=false` (line 282).

### Kafka pressure

The harness creates every topic with `retention.ms=60000` and `segment.ms=1000`. The pressure
vector keeps its consumer stopped through six throttled publishes plus native offset
diagnostics. The two acknowledged records (offsets 0-1) were written at 00:09:59.33. At
00:10:59.820 the Kafka log records "Deleting segment LogSegment(baseOffset=0, …,
largestRecordTimestamp=…599345) due to log retention time 60000ms breach". The consumer group had
no committed pressure offset (receipt 1371) and restarted at 00:11:04.29, so it began at the
advanced log start. The first copy of `fcb2d274…` sat in the same segment, which is why that ID
shows two receipts instead of three. The fixture, not backpressure, removed the records.

The second predicate is a genuine broker property. A produce byte-rate quota delays responses and
never rejects. Each over-quota publish ran 3 × 3 s Dapr timeouts plus 2 × 200 ms = 9.40 s and
returned 500. All three attempts were nevertheless appended later (log end 20 = 2 + 6 × 3). The
only Kafka rejection the pass rule accepts, "kafka server: The throttling quota has been
exceeded", is not produced by this fault. The vector therefore fails after the harness fix too.
Whether this guarantee is attainable on Kafka at all is an EventStore 3.21 question (see
[remaining uncertainty](#remaining-uncertainty)).

## Fixes (EventStore branch, unpushed)

| Commit | Change |
| --- | --- |
| `a19bad9de1851b5ebfb22689dbe429c890150a13` | **Lock inventory.** `d5589b53f` (net11.0 paths) and `15c8ccd52` (setup-dotnet line) drifted four platform-config-v1 inputs and three historical 1.19.1 inputs. At `bb9e2b51` the workflow's owner-admission `validate_profile` rejects the profile, and the discovery suite fails (96 OK, 76 failures, 22 errors). The exact historical bytes are archived from `af9735b2e6fbcfc868051eee8d88ce40110cfefc` and `07f5ef132deb17d45d7209357f8ac89ef39bc557`, and the Platform lock is refreshed. Historical lock files are unchanged |
| `78bc35aa57e1e33c376847fb2902e69565cce16c` | **Verifier.** `--config` is exempt only inside the producer's exact Kafka admin grammar: `docker exec` into the measured Kafka container, an `/opt/kafka/bin/kafka-*.sh` tool, the measured listener and the admin credentials. `--app-id` and `--resources-path` still identify a sidecar there. Every other receipt keeps full identification. The unchanged topic setup moved into `Run.kafka_topics` so the controls drive the producer's real grammar |
| `c4b90c42c72cf7e841d45c22ce35b1c5f87872a6` | **Harness.** `qualification.backpressure` is created with `retention.ms=-1`. Other topics, the fault, the bounds and the pass rule are unchanged |
| `495dd24943031ec75e2822c6168ada3c8b9fdb82` | Docs: the 3.21 input spec records the corrections |

New controls in `tools/tests/test_dapr_platform_broker_configuration.py`:

- a real-shaped Kafka topic-creation receipt, emitted by the producer's own `kafka_topics()`, passes integrity;
- unmeasured executables, and sidecar flags placed in the wrong container, path, listener or credentials, still fail with "sidecar executable lacks the measured Dapr preflight binding";
- duplicated and `=`-form flags still fail with "ambiguous or incomplete sidecar configuration";
- the pressure topic is the only one without time-based expiry.

Scratch-copy mutations prove the controls bite (`mutation-sensitivity.json`):

- the pinned 02a4d3b2 rule fails the positive control;
- skipping every `docker exec` fails 10 subtests;
- an exemption without the container binding fails 2 subtests;
- restoring the 60 s retention fails the retention control.

**Non-authoritative check** (`diagnostic-fixed-identification.json`). The fixed file was placed
in an isolated copy of 02a4d3b2, with the trusted root pointed at the unpatched archive. Against
the real packet:

- integrity: exit 0, PASS;
- technical: exit 1, naming exactly the six vectors above;
- authority: exit 1, with byte-for-byte the rejection the workflow requires.

This credits nothing.

## Validation

Recorded with argv, exit and log SHA-256 in `validation-results.json`; logs stay outside Git.

| Command | Where | Result |
| --- | --- | --- |
| `python3 -B -m unittest discover -v -s tools/tests -p 'test_dapr*qualification.py'` | EventStore `bb9e2b51` | exit 1: 156 tests, 76 failures, 22 errors (pre-existing drift) |
| same | clean archive of `495dd249` | exit 0: 160 tests OK |
| `… -p test_dapr_platform_broker_configuration.py` | clean archive of `495dd249` | exit 0: 32 tests OK |
| `… -p test_qualify_internal_actor_admission.py` | clean archive of `495dd249` | exit 0: 11 tests OK |
| `candidate_02_admission.py` (owner admission and producer constructor, no subprocess or socket) | clean archive of `495dd249` | exit 0: profile PASS (94 inputs), producer admission PASS |
| `node --test tests/source-release/broker-candidate-workflow.test.mjs` | Platform `aa921804` | exit 0: 53/53 |
| same, with `candidate-02-platform-change.patch` | detached scratch worktree | exit 0: 53/53 |
| `npm run test:source-release`, with the patch | detached scratch worktree | exit 0: 82/82 |
| `git diff --check`; `scripts/check-committed-files.sh --staged`; pinned commitlint 21.2.4 | EventStore branch | all passed for each commit |

No C#, project or workflow file changed in EventStore, so no .NET build ran. No Docker, broker,
CI, dispatch, tag or push occurred.

## Remaining uncertainty

- Every diagnosis comes from a packet whose integrity is verified only non-authoritatively. The 18 producer PASS results, including historically rejected `kafka/secret-rotation`, stay unverified.
- Restoring the RabbitMQ defaults is predicted to bring back durable-quarantine and retention-expiry; that is not proven. Likewise, the retention fix is predicted to give Kafka pressure `acknowledgedLoss` 0 within the 10 s bound.
- The Kafka pressure pass rule accepts only a rejection that a produce byte-rate quota never emits. That vector cannot pass with this fault. Changing the fault or the guarantee is EventStore 3.21's decision. Relaxing the rule was out of scope and was not done.
- RabbitMQ rotation needs a component change: the reconnect error must surface, or the design must change, for example a cold restart with old credentials. RabbitMQ pressure and missing binding keep their upstream dependencies. Upstream release status was not re-checked.
- Why `kafka/secret-rotation` passed in attempt 2 is unexplained. It is not attributed to candidate-01's Kafka knobs.
- The branch starts at `bb9e2b51` as instructed. EventStore main is now `f2062f7f`. The merge is clean, but whatever merge commit is created must keep `495dd249` reachable from main.
- Attempt 1's residual cause (provider incident or ref deletion) is unchanged.

## Next owner decision

See the [candidate-02 proposal](story-4-17-broker-candidate-02-proposal.md). Approving it means
pushing the EventStore branch and merging it, creating the Platform source commit and tag, and
dispatching one attempt. Each step is listed there with the expected verifier outcome.

## Commands

The retained scripts are in the evidence directory. Each one takes paths as arguments and runs
with `python3 -I`:

- `redownload_and_inspect.py`: download, digest check, inspection, extraction;
- `dump_receipts.py`: compact receipt view;
- `diagnose_vectors.py`: the six-vector diagnosis;
- `refresh_qualification_inventory.py`: archive and Platform lock refresh;
- `mutation_sensitivity.py`: control sensitivity;
- `diagnostic_fixed_identification.py`: the non-authoritative check;
- `candidate_02_admission.py`: offline admission;
- `finalize_evidence.py`: record compilation.

`file-manifest.json` commits every retained file. Check it with
`python3 -I ../candidate-01-startup-investigation/verify-manifest.py file-manifest.json`.
