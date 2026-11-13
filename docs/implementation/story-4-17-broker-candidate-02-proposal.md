# Story 4.17 — broker candidate-02 proposal (2026-11-13)

Status: **proposed for owner approval; nothing pushed, tagged or dispatched.** Platform 4.17 and
EventStore 3.21 remain **in-progress**. All five historical guarantees remain **REJECTED**. Broker
selection stays **null** and production eligibility **FAIL**. Candidate-02 cannot produce bounded
technical success: four vectors fail for code-level or broker reasons that no admitted setting
reaches. What it can produce is the first independently verified packet for Platform's
configuration input.

[`candidate-02-proposal.json`](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-packet-diagnosis/candidate-02-proposal.json)
is authoritative. The evidence behind it is in the
[packet diagnosis](story-4-17-broker-candidate-01-packet-diagnosis.md).

## What changes

| Item | Candidate-01 (attempt 2) | Candidate-02 |
| --- | --- | --- |
| EventStore revision and Platform gitlink | `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` | `495dd24943031ec75e2822c6168ada3c8b9fdb82` (branch `fix/3-21-candidate-01-verifier-and-pressure-retention`; gitlink moves from `bb9e2b51…`) |
| Workflow `EVENTSTORE_REVISION` | `02a4d3b2…` | `495dd24943031ec75e2822c6168ada3c8b9fdb82` |
| Workflow `CONFIGURATION_SHA256` | `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc` | `b8601e3a6d4e8460d039573ca2f7e1728e52aa67e61fc0710d74cab5d072580b` |
| Configuration | `docs/implementation/story-4-17-broker-candidate-01.json` | [`story-4-17-broker-candidate-02.json`](story-4-17-broker-candidate-02.json), 367 bytes, declared Platform revision `aa921804b5e3fcf698bf333f40ec50c390646692` |
| Profile (same path) | SHA-256 `1c1da59176f78b9b60a87ba85b6076b8d215ff912840ecb52bdb7432497c587f`, 91 inputs | SHA-256 `eae60aeb3ad2734cb54aed7de4b4a4efb19b9b28f3f6ce2eab56fa89850875b6`, 94 inputs |
| Names | job `candidate-01`, artifact and temp `platform-broker-candidate-01-*` | job `candidate-02`, `platform-broker-candidate-02-*` |
| Dispatch tag | `qualification/broker-candidate-01/attempt-2-94001fad` | `qualification/broker-candidate-02/attempt-1-<SOURCE8>` |

`SOURCE8` is the first 8 hex digits of the published Platform source commit S. S carries the
gitlink, workflow and test changes beside this slice's committed JSON. S cannot exist before the
EventStore branch is pushed, because a gitlink may only record a pushed commit. The tag name is
therefore fixed when S is created and is never reused. No `qualification/broker-candidate-02/*`
tag exists today. Ruleset 24657570 blocks tag updates and deletions.

The profile changes 11 commitments: 8 changed inputs and 3 new archive copies. The JSON lists
every old and new hash. Four drifted inputs (net11.0 paths and a setup-dotnet line) are refreshed,
three changed tool and test files are refreshed, `archive.json` changes, and three historical
originals are newly archived.

Unchanged: runtime 1.19.1, CLI 1.19.1, SDK and components 1.19.0, every image digest, the chart,
both historical lock files, all 24 vectors and every pass rule. The workflow keeps its trigger,
runner, timeout, permissions, pinned actions, guards, phase codes, verifier calls and expected
rejection.

The exact Platform change is
[`candidate-02-platform-change.patch`](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-packet-diagnosis/candidate-02-platform-change.patch),
on base `aa921804`. Apply it with `git apply --index --unidiff-zero`; it uses zero-context hunks so
it carries no whitespace errors. Applied to a fresh worktree, it reproduces tree
`32f73b003465579b15183a761fd49d000dda1667` with gitlink `495dd249`. It passed the broker-workflow
controls (53/53) and the full source-release suite (82/82).

## Configuration choice

```json
{
  "schemaVersion": 1,
  "platformRevision": "aa921804b5e3fcf698bf333f40ec50c390646692",
  "platformPath": "docs/implementation/story-4-17-broker-candidate-02.json",
  "rabbitmq": { "reconnectWaitSeconds": 1, "outboundTimeoutMilliseconds": 3000 },
  "kafka": { "consumeRetryIntervalMilliseconds": 200, "group.initial.rebalance.delay.ms": 100 }
}
```

This listing is compacted. The committed file uses candidate-01's indent-2 layout, and the hash
above is of those committed bytes.

- **RabbitMQ returns to the historical defaults.** Candidate-01's 2 s reconnect wait with a
  500 ms timeout makes publisher reconnection unreachable. That caused the durable-quarantine and
  retention-expiry failures. No admitted value can surface ACCESS_REFUSED for rotation either.
  Offline admission confirms the rendered component equals the historical one.
- **Kafka is unchanged** (200 ms, 100 ms), so a verified `kafka/secret-rotation` result can be
  compared with attempt 2's unverified PASS.
- **Alternative (like-for-like):** keep candidate-01's RabbitMQ values. The file would have
  SHA-256 `26c8d9a18ac72bfbce2fbca4fd5562f041a69e811830a0f88ba098e9f5a5b444`. It isolates the
  EventStore fixes, but durable-quarantine and retention-expiry are expected to fail again.

## Route (each step needs the owner's go-ahead)

1. **EventStore.** Push the branch and merge it into `main` with a merge commit, never a rebase or
   squash, so that `495dd249` stays contained in main. The commits are `build:` and `docs:`, so
   they trigger no package release. Confirm `git merge-base --is-ancestor 495dd249 origin/main`,
   and check the activity API for a later `force_push`.
2. **Platform source S.** On a dedicated non-main branch from the published base, apply the patch
   (gitlink, workflow, test) beside the committed JSON and commit. Push only that branch; neither
   tuple CI nor catalog-builder triggers on a non-main push. Re-run the broker-workflow controls at
   S, and the candidate-02 offline admission against the pushed EventStore revision. Before
   pushing, scan the evidence, because Platform is public.
3. **Go/no-go**, as for attempt 2:
   - provider status;
   - rulesets 24657570 and 24657217 unchanged;
   - the tag absent;
   - the workflow idle;
   - the identities match;
   - no tag triggers.

   Then create the tag through the API at S and confirm that no run started.
4. **Dispatch exactly once:**

   ```bash
   gh workflow run broker-candidate-qualification.yml --repo Hexalith/Hexalith.Platform \
     --ref qualification/broker-candidate-02/attempt-1-<SOURCE8>
   ```

   No rerun, retry or cancellation, except the 30-minute no-job rule. The run counts as the
   attempt.
5. **Retrieve** the artifact before its 30-day expiry. Inspect it before extraction, and run the
   three verifier modes locally from an isolated `git archive` of `495dd249`.

## Worker requirements

- A fresh GitHub-hosted `ubuntu-24.04` VM, 40-minute timeout, `contents: read`.
- Guards: `RUNNER_ENVIRONMENT=github-hosted`, Linux/X64, `ImageOS=ubuntu24`, Ubuntu 24.04 x86_64,
  Python ≥ 3.11, OpenSSL, and a local unix-socket Docker daemon on linux/amd64 with no
  `DOCKER_HOST`, `DOCKER_CONTEXT` or TLS overrides.
- Sources: a clean Platform tree at the dispatch SHA. The gitlink, the EventStore HEAD and its clean
  tree must all equal `495dd249`, and the configuration SHA-256 must equal `b8601e3a…580b`.
- Allocation: an exclusive allocation created only inside that VM, bound to the run, job and
  attempt, the socket device/inode and the daemon ID. EventStore's producer then owns custody,
  cleanup and its receipts.

## Expected verification

| Phase | Expected |
| --- | --- |
| Capture | Exit 1, because some scenarios are not PASS; cleanup receipt complete |
| Integrity (`--allow-failures`) | **Exit 0.** The only integrity blocker the 02a4d3b2 verifier reported is fixed, and the fixed rule admitted the real attempt-2 packet (non-authoritative) |
| Technical | **Exit 1.** Certain to fail: `rabbitmq/cancellation-backpressure`, `rabbitmq/missing-bindings`, `rabbitmq/secret-rotation`, and `kafka/cancellation-backpressure` (`negativeConfirmRejected`). Predicted but unproven: `rabbitmq/durable-quarantine` and `rabbitmq/retention-expiry` PASS, and Kafka pressure `acknowledgedLoss` 0. `kafka/secret-rotation` is unknown |
| Authority (`--require-production-authority`) | **Exit 1**, empty stderr, stdout exactly: `FAIL: production eligibility FAIL: authenticated AD-26 Architecture/Platform records, Story 3.19 publication/profile binding, Kubernetes rehearsal, restore and Story 4.16 envelope proof are absent; this lane never issues authority` (one trailing newline) |
| Job | Fails, because `boundedTechnicalSuccess` is false whenever technical qualification fails |

A verified packet credits integrity, cleanup and each verified vector result. It selects no
broker and changes no rejected guarantee.

## Decision

- **Recommended:** approve candidate-02 as specified (route steps 1–5). It turns this slice's
  diagnoses into verified evidence: the RabbitMQ regressions, the Kafka retention artifact and the
  Kafka rotation result. It also supplies the verified baseline that EventStore 3.21 needs to
  decide the Kafka pressure fault and the RabbitMQ component gaps.
- **Alternatives:**
  - run the like-for-like JSON instead;
  - merge and push the EventStore fixes now, but defer any hosted attempt until EventStore 3.21
    decides the unreachable Kafka pressure rule and the RabbitMQ component dependencies.
