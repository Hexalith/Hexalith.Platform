# Story 4.17 — candidate 01 first hosted attempt

Assessed on 2026-11-11. The single approved hosted attempt
[43097161643, attempt 1](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43097161643)
ended in **`startup_failure` before any job was allocated**. GitHub reports an
unexpected service error and support request ID
`4ed78a69-de10-11f1-8d56-5705d8f2f5ff`. No broker capture ran. There are **zero jobs,
zero artifacts and 0/24 observed vectors**; packet verification and cleanup
assessment remain incomplete. No retry was performed.

## Published source and actual run binding

| Identity | Full value |
| --- | --- |
| Approved source baseline / workflow commit | `13aa784958dd634196c540713b3513e67af458ed` |
| Included Story 2.8 commit | `6d422033b959c249c149a4cb3785704e9eebe03c` |
| Candidate preparation commit | `15926cac7ffe7bb91926617a586f34262beb0665` |
| Final reviewed branch history | `9a829a40718311da3eeb20a9f4e9ba9bc2eea41b` |
| Externally published main merge / actual run head SHA | `94001fad925fdf7fa03a90b507b8f8e19074f9e3` |
| Actual dispatch ref | `refs/heads/chore/broker-candidate-01-hosted-94001fad` |
| Later independent main advance, preserved | `488723fd7efbccbab3bccdae3be4ad79b05a4994` |
| Required EventStore pin / clean isolated verifier source | `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` |
| Original candidate SHA-256 | `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc` |
| JSON's declared, unauthenticated Platform revision | `6d422033b959c249c149a4cb3785704e9eebe03c` |

[PR #40](https://github.com/Hexalith/Hexalith.Platform/pull/40) was observed merged
externally at `2026-11-11T20:32:48Z`. The reviews API returned an empty array;
no review or external bypass evidence is inferred. This slice did not repeat the
merge or bypass rules. The published merge's complete tree equals the reviewed
source tree. Earlier independent main changes were preserved through assessed
ordinary forward integration; the sole `.gitattributes` conflict retained both
additive rule blocks verbatim.

Main advanced again before dispatch and repinned EventStore to
`0c7b14856f2b730749caa8a552742a77d4882dc7`. The final guard stopped before any
mutation. Assessment authorized a dedicated reversible source branch at the already
published `94001fad` commit. It contains no new commit or changed experiment bytes;
current main and its newer gitlinks were preserved. GitHub's branch/tag dispatch
binding is documented in the [workflow API](https://docs.github.com/en/enterprise-cloud@latest/rest/actions/workflows).
The branch was verified at the exact full source before the single dispatch.
The GitHub connector refused ref creation with HTTP 403; the authenticated `gh`
CLI created the same assessed ref. See the [source-binding assessment](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/hosted-source-binding-assessment.json)
and [external-publication receipt](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/external-main-publication.json).

The original candidate, workflow and explicit
`runtime-lock-1.19.1-platform-config-v1.json` remain unchanged, with both brokers
and all 24 vectors. Admission validated the clean isolated EventStore pin and all
91 locked inputs. The JSON declaration is separate from GitHub's actual run-source
identity. Because no job was allocated, no runner checkout, bootstrap or worker
allocation observation exists; none is inferred from GitHub's run head SHA.

## Terminal outcome and missing evidence

| Dimension | Actual observation |
| --- | --- |
| Dispatch / attempt | Exactly **1 / 1**; no rerun |
| Run lifetime | Created `20:37:28Z`; terminal update `20:41:54Z`, 2026-11-11 |
| Terminal result | `startup_failure` |
| Jobs / check runs / pending deployments | **0 / 0 / 0** |
| Capture exit and argv | Absent; no job executed |
| Independent integrity exit and argv | Absent; no packet exists |
| Independent technical exit and argv | Absent; no packet exists |
| Independent production-authority exit and argv | Absent; no packet exists |
| Expected / observed vectors | **24 / 0**; all `NOT_EXECUTED` |
| Cleanup receipt / verified cleanup | Absent / not established |
| Expected artifact | `platform-broker-candidate-01-43097161643-1` |
| Artifact inventory / download | **0**; unavailable, no bytes or digest |
| Archive path inspection / extraction | Not applicable; no archive exists |
| Bounded technical success | Not established |
| Production eligibility / broker selection | **FAIL / null** |

The [run receipt](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/hosted-run.json),
[job inventory](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/hosted-jobs.json),
[artifact inventory](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/hosted-artifacts.json),
[actual outcome record](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/execution-status.json)
and [per-vector assessment](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/vector-assessment.json)
retain these separate results. The [independent-verifier assessment](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/independent-verifier-assessment.json)
records null argv and exits for all three modes: no verifier was invoked against
nonexistent evidence. Artifact absence prevents download, archive inspection and
packet evaluation; those steps remain incomplete.

The [provider diagnostic](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/startup-diagnostic.json)
retains GitHub's actual unexpected-error annotation and request ID. The CLI's generic
suggestion of a possible workflow-file issue is not a confirmed root cause.
Independent current-main tuple/catalog push runs were also queued in the retained
[snapshot](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/independent-main-runs.json);
this does not establish a shared cause or their eventual results. The provider's
suggested retry was not followed because the approved single attempt is consumed.

No raw credential, environment dump or private broker transcript was retained.
The [file manifest](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/file-manifest.json)
commits the retained metadata and publication evidence bytes. The earlier
[blocked-publication report](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/pre-hosted-status/story-4-17-broker-candidate-01-hosted.md)
is retained as historical context.

## Verification and remaining work

The previously integrated source passed `npm run test:source-release`: **82/82**,
including 53 executable broker controls. The actual published execution source has
an identical complete tree. Pinned commitlint **21.2.4** validated the authored Git
and PR messages before their mutations. Fresh checks confirmed the original digest,
unchanged workflow and gitlink, clean isolated pinned source, locked-input admission,
exact remote source ref and exactly one hosted attempt. Retained file hashes, links,
parent sprint values and `git diff --check` were checked. Offline controls do not
diagnose GitHub's startup failure or establish a broker guarantee.

All five [historical rejected guarantees](story-4-17-local-broker-dispositions-2026-11-10.md)
remain **REJECTED**: RabbitMQ rotation, pressure and missing binding; Kafka rotation
and pressure. Their dated dispositions are unchanged. Investigate the startup
failure before a separate decision on any further hosted attempt; no second run is
authorized by this slice. Packet/verifier, cleanup and artifact-delivery assessment
remain incomplete. Broader diagnosis, exact Kafka causality, broker selection,
exceptions, staging, hosted Subscription rendering and production acceptance remain
open. Platform 4.17 and EventStore 3.21 remain **in-progress**.
