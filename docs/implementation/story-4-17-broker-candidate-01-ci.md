# Story 4.17 — prepared manual CI for broker candidate 01

Prepared on 2026-11-11 under the owner's accepted bounded implementation scope.
The [workflow](../../.github/workflows/broker-candidate-qualification.yml) has been
implemented and verified offline. The first approved hosted attempt,
[43097161643](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43097161643),
ended in **`startup_failure` before any job was allocated**. No broker capture ran;
no artifact or packet exists. The [current hosted report and retained evidence](story-4-17-broker-candidate-01-hosted.md)
record the actual published source, single dispatch, provider diagnostic, 0/24
observed vectors and null verifier exits. The approved attempt is consumed;
no automatic retry is authorized. Platform 4.17 and EventStore 3.21 remain
**in-progress**; all five historical guarantees remain **REJECTED**. No broker is
selected, and staging and production qualification remain open.

This implements the local/CI configuration handoff for one candidate. Platform
owns the manual workflow, original configuration, hosted allocation and retained
execution receipts. EventStore owns the pinned producer, verifier, matrix and
interpretation of evidence. This establishes no general Platform runner/tool
acceptance, Builds integration, staging capability or production authority.

## Fixed inputs and hosted execution

The only trigger is `workflow_dispatch`, without inputs. One job uses a fresh
standard GitHub-hosted `ubuntu-24.04` Linux x64 VM, a 40-minute timeout and
`contents: read` permissions. The existing SHA-pinned checkout and artifact actions
are reused. The checked-out Platform revision must match the actual dispatch
revision; it is recorded separately from the JSON's older declared provenance.

| Input | Fixed value |
| --- | --- |
| Original configuration | [story-4-17-broker-candidate-01.json](story-4-17-broker-candidate-01.json) |
| Original SHA-256 | `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc` |
| Clean EventStore revision and gitlink | `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` |
| Explicit profile | `deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json` |
| Runtime / CLI / SDK / components | `1.19.1` / `1.19.1` / `1.19.0` / `1.19.0` |
| Matrix | Both `rabbitmq,kafka`; all 12 fixed scenarios per broker, 24 vectors |

Bootstrap evidence precedes checkout and setup. Source and runner guards reject
wrong revisions, changed hashes, dirty sources, unsupported OS/architecture,
missing image identity and reused destinations before installation or producer
execution. Only the root-declared EventStore submodule is initialized, without
recursion. The pinned profile validates all its source inputs before setup proceeds.

After those checks, the hosted job binds its run/job/attempt identity to the
observed canonical local Docker socket, socket device/inode and daemon ID. It
creates the exclusive allocation only inside that verified hosted VM. Remote or
conflicting Docker selection is rejected. EventStore's existing producer then owns
the allocation lock, repeated custody checks, resource cleanup and cleanup receipts.
The existing installer verifies the exact profile's isolated CLI/runtime downloads.
No local allocation file, local Docker execution or new runner framework is used.

The original JSON stays in the Platform checkout, outside the packet. Capture and
each of the three verifier modes receive that same independently supplied original
through `--platform-config`. The packet's copy is never substituted for it.
The [original preparation and handoff](story-4-17-broker-candidate-01.md) is preserved.

## Executed publication and single dispatch

The owner approved current-main publication, including Story 2.8, and one hosted
attempt. PR #40 was observed merged externally at full revision
`94001fad925fdf7fa03a90b507b8f8e19074f9e3`; its reviews API remained empty, so no
review or external bypass evidence is inferred. The workflow reached the default
branch before dispatch. A later independent main advance changed the EventStore
gitlink, and the final guard stopped before dispatching that source.

Assessment then authorized the dedicated source branch
`chore/broker-candidate-01-hosted-94001fad` at the already published exact-input
commit. Current main and all newer gitlinks were preserved. No candidate, workflow,
harness or lock changed and no new commit was created for the source binding.
The single executed command was:

```bash
gh workflow run broker-candidate-qualification.yml \
  --repo Hexalith/Hexalith.Platform \
  --ref chore/broker-candidate-01-hosted-94001fad
```

GitHub bound run `43097161643`, attempt 1, to the full actual source
`94001fad925fdf7fa03a90b507b8f8e19074f9e3`, separately from the JSON declaration.
It terminated with `startup_failure`, zero jobs and no artifact. The exact provider
diagnostic and support request ID are retained in the hosted report. No verification
mode was invoked without a packet. Any further attempt requires a separate decision;
do not rerun automatically.

The later [startup investigation](story-4-17-broker-candidate-01-startup-investigation.md)
classifies the failure as provider-side and finds no workflow defect. It records that
the dispatch branch was deleted while the run was queued. It proposes, but does not
authorize, one further attempt from an immutable tag at the same source.

[Hosted attempt 2](story-4-17-broker-candidate-01-hosted-attempt-2.md) then ran once from that tag (run 43333995506). The job was
allocated, all phases ran and the artifact uploaded. The pinned verifier rejected the
packet before vector assessment: Kafka topic creation's `--config` argument is
misread as a daprd launch. The workflow itself behaved as designed.

## Separate outcomes and expected failures

The workflow preserves raw exits separately for source setup, installation,
capture, integrity, technical qualification and production authority. A nonzero
producer exit cannot skip the three independent evaluations. If source admission
succeeded but installation failed, those evaluations still run and report the
missing packet. If source admission failed, they stay skipped and incomplete.

| Observation | Interpretation and final job result |
| --- | --- |
| Capture or technical exit is nonzero | Technical failure; final job fails even if integrity accepts truthful negative evidence |
| Missing/invalid packet or integrity failure | No integrity or qualification credit; diagnostics retained; final job fails |
| Capture, integrity and technical checks succeed, cleanup is verified complete, authority returns the exact pinned expected rejection | Bounded technical success; production eligibility **FAIL**, selection **null** |
| Authority succeeds unexpectedly, exits differently, or emits a different rejection | No bounded success; final job fails |
| Setup failure, skipped/cancelled step, absent exit, missing cleanup, failed summary or failed upload | Incomplete or failed evidence; final job fails |

The production-authority exit is expected to be 1 with the pinned verifier's exact
message naming the absent AD-26 records, Story 3.19 binding, Kubernetes rehearsal,
restore and Story 4.16 proof. Recognizing this rejection never masks a capture,
integrity, technical, setup, cleanup or artifact failure. `--allow-failures` checks
truthful capture integrity; it grants no rejected guarantee a PASS. A successful
bounded job would establish only this candidate's first-stage technical result,
subject to evidence assessment. It would select no broker and change no parent
story, staging, Subscription-rendering or production acceptance.

## Artifact retention and interruption limits

Summary generation and artifact upload each use `always()` independently. The
artifact is named `platform-broker-candidate-01-RUN_ID-RUN_ATTEMPT`, retained for
30 days, and available from a run's Actions page when upload succeeds. This first attempt
produced no artifact, so no download occurred. For an available artifact, download it
before expiry, for example:

```bash
gh run download RUN_ID --repo Hexalith/Hexalith.Platform \
  --name platform-broker-candidate-01-RUN_ID-RUN_ATTEMPT \
  --dir FRESH_EVIDENCE_DIRECTORY
```

It contains bootstrap runner/image context, observed allocation/source identities,
the separate original JSON, actual outcomes, command argv, and SHA-256/byte-size
commitments for transcripts. The producer's sanitized packet is retained unchanged,
including its command, custody, before/after inventory and cleanup receipts. Invalid
packets are evidence to diagnose, never qualification proof. Packets containing
links or special files are omitted; their missing retention prevents bounded success.

Before their admission comparisons, the workflow retains safe actual Platform and
EventStore revisions, the EventStore gitlink, and the configuration SHA-256 as
`*.observed` files and in `outcomes.json`'s `observations`. These typed observations
survive rejection; malformed identities become the fixed `invalid` marker.
Rejected configuration bytes are never retained. The separate original JSON is
copied into evidence only after all source, configuration and worker guards pass.

Each shell step records its last fixed phase code alongside its raw exit, and
`outcomes.json` and the summary report those phases. For example,
`platform-status-query` or `eventstore-status-query` identifies a failed Git status
command, `configuration-hash-match` identifies a configuration hash mismatch,
`eventstore-initialize` identifies submodule setup failure, and `tool-installation`
identifies installer failure. Successful steps record `complete`. These codes
identify the failed check without publishing arbitrary command diagnostics.

Evidence-processing failures are isolated and recorded as fixed
`evidenceProcessingErrors` codes, such as `authority-decode`, `packet-projection`
or `summary-write`. Reporting continues with available outcomes and remaining
summary/output attempts. Any such failure makes the report and final job fail,
withholds bounded technical and verified-cleanup credit, and preserves the already
created evidence root for the independent upload attempt. The upload can fall back
to the bootstrap root if report outputs are unavailable.

Raw stdout/stderr and command transcripts remain in a private VM directory outside
the packet inventory and are explicitly excluded from upload. Their exact exits,
hashes and sizes survive in the artifact; arbitrary diagnostic text is not published.
Private supervisor/credential trees, environment dumps and downloaded tools are
excluded. Raw private transcripts disappear with the disposable VM.

Cancellation or hard termination can interrupt EventStore cleanup, status writing,
summary generation or artifact upload. `always()` is an attempt, not a guarantee
that the hosted service allows those steps to finish. A cleanup receipt claiming
completion receives no verified-cleanup credit without successful integrity
verification. Absent statuses, missing receipts or unavailable artifacts stay
incomplete and require assessment before any new execution decision.

## Offline verification

[Executable workflow controls](../../tests/source-release/broker-candidate-workflow.test.mjs)
parse the actual YAML and execute its shell/Python blocks in disposable fixtures.
They fake the external pinned-owner APIs, Git, hosted OS/Docker boundaries,
installation, capture and verifier processes; the workflow's admission calls,
guards and reporting logic execute unchanged. Fixtures require no initialized
submodules, so the existing source-release CI order remains supported.
They cover runner/source/hash/freshness refusal, complete invocation pairing,
independent evaluations, technical and integrity failures, broken evidence, bounded
success, setup/cancellation, cleanup and independent summary/artifact conditions.
No service, tool download, Docker operation or live qualification is run.

```bash
node --test tests/source-release/broker-candidate-workflow.test.mjs
npm run test:source-release
git diff --check
```

The controls also parse the workflow and check every Bash block with `bash -n`.
These offline results validate the prepared workflow's behavior, not a live runner,
broker guarantee, staging deployment or production selection.
