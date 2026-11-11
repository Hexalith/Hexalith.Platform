# Story 4.17 — candidate 01 publication and hosted-execution status

Assessed on 2026-11-11. **Blocked before hosted execution:** the approved history
is prepared in [PR #40](https://github.com/Hexalith/Hexalith.Platform/pull/40),
awaiting the repository's required approving review and code-owner review.
The owner has already approved main publication and exactly one hosted attempt.
No broker workflow was dispatched; no run, packet or artifact exists to assess.

## Publication and actual source identities

| Identity | Full value |
| --- | --- |
| Approved local source baseline | `13aa784958dd634196c540713b3513e67af458ed` |
| Included Story 2.8 commit | `6d422033b959c249c149a4cb3785704e9eebe03c` |
| Candidate preparation commit | `15926cac7ffe7bb91926617a586f34262beb0665` |
| Candidate workflow commit | `13aa784958dd634196c540713b3513e67af458ed` |
| Initially observed remote main | `09946e760b56a7a6ab9675503f2b018d33d45f2c` |
| Independently advanced remote main, preserved | `3ad2f29ae50858ba665499568623b557725b9d93` |
| Final review-branch source | `9a829a40718311da3eeb20a9f4e9ba9bc2eea41b` |
| Review branch | `chore/broker-candidate-01-publication` |
| Actual hosted execution revision | **Absent — no dispatch** |
| EventStore gitlink and clean verifier source | `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` |
| Original candidate SHA-256 | `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc` |
| JSON's declared, unauthenticated Platform revision | `6d422033b959c249c149a4cb3785704e9eebe03c` |

The JSON declaration does not authenticate execution. The original candidate,
workflow and explicit `runtime-lock-1.19.1-platform-config-v1.json` remain unchanged,
with both brokers and all 24 vectors. EventStore's pinned commit is an ancestor of
its observed published main. No harness, lock or nested submodule was changed.

Active main rules require a pull request with one approval and code-owner review.
The maintainer's technical bypass capability was not used. The protected default
branch has not received this workflow, so live dispatch remains gated on publication.
PR creation through the GitHub connector returned HTTP 403; the same validated PR
was created through the authenticated `gh` CLI.

The final PR snapshot is conflict-free (`MERGEABLE`) and `REVIEW_REQUIRED`, with
zero reviews. Automatic PR CI is separate from the broker experiment. Its
[pre-extension public tuple job](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43041857636/job/130595865450)
reported failure while other checks were still running. That result remains a
publication limitation requiring assessment; no CI implementation was changed
or gate weakened, and those checks establish no broker result.

Remote main advanced during publication preparation. Read-only assessment found
one conflict: both sides appended distinct preservation rules to `.gitattributes`.
The assessed normal forward merge retained both blocks verbatim and preserved all
independent remote source/test bytes. The three approved local commits retain
their identities. The spec and generated epic context were excluded from the review
branch. See the [divergence assessment](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/divergence-assessment.json)
and [final publication checks](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/final-publication-preflight.json).

## Evidence assessment

| Dimension | Observed result |
| --- | --- |
| Broker workflow dispatches by this slice | **0**; the approved single attempt remains unused |
| Run / attempt / job identity | Absent |
| Capture exit and argv | Not executed; absent |
| Independent integrity exit and argv | Not executed; no retained packet |
| Independent technical exit and argv | Not executed; no retained packet |
| Independent production-authority exit and argv | Not executed; no retained packet |
| Expected / observed vectors | **24 / 0**; every vector is `NOT_EXECUTED` |
| Cleanup receipt / verification | Absent / not assessed |
| Artifact metadata / exact bytes / digest | Absent; no artifact download attempted |
| Archive inspection and extraction | Not applicable; no archive exists |
| Bounded technical success | Not established |
| Production eligibility / broker selection | **FAIL / null** |

The [execution status](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/execution-status.json),
[run discovery](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/hosted-run-discovery.json),
[PR snapshot](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/pull-request.json)
and [per-vector assessment](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/vector-assessment.json)
retain the observed boundary. Missing exits are null; they are not synthetic failures
or successful evaluations. No raw credentials, environment dumps or private broker
transcripts were retained. The [file manifest](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/file-manifest.json)
commits the retained preflight and publication-evidence bytes.

All five [historical rejected guarantees](story-4-17-local-broker-dispositions-2026-11-10.md)
remain **REJECTED**: RabbitMQ rotation, pressure and missing binding; Kafka rotation
and pressure. The 24-vector assessment records absence of new observations and does
not rewrite those dated dispositions or award qualification credit.

## Verification and remaining work

The integrated review source passed `npm run test:source-release`: **82/82**,
including 53 executable broker controls. Pinned commitlint **21.2.4** accepted the
exact merge messages, approved history, PR title and full PR message. An initial
full-message footer line-length rejection was revised and revalidated before PR
creation. Final source checks preserved the original candidate digest, clean pinned
EventStore, all approved source bytes and independent main source/test bytes.
`git diff --check` passed. These are offline and publication checks.

Required review and permitted default-branch integration remain outstanding.
After publication, recheck the actual remote revision and dispatch the fixed workflow
exactly once under the existing approval. Retain its named artifact in a fresh
location before 30-day expiry, inspect archive paths before extraction, and run all
three independent verifier modes against the separate original configuration.
Actual exits, all observed vectors, cleanup proof and artifact delivery must determine
the resulting assessment. Missing evidence stays incomplete; no automatic rerun is
authorized. Main publication may trigger existing tuple/catalog CI and their gated
source tag/GitHub release; those independent gates remain unchanged.

Broader broker diagnosis, exact Kafka causality, selection, exceptions, staging,
hosted Subscription rendering and production acceptance remain open. Platform 4.17
and EventStore 3.21 remain **in-progress**.
