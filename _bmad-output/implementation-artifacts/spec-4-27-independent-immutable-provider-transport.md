---
title: '4.27: Independent immutable S3 read transport'
type: 'feature'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
epic: 4
story: 27
baseline_commit: '95ee736dd43df51f328126ff48113e42b2bd4c6d'
context: []
---

<frozen-after-approval reason="user explicitly requested scoped specification and implementation">

## Intent

**Problem:** `ImmutableReadbackDriver` has an exact read contract but its provider interface refuses. Offline execution preparation needs a concrete independent provider implementation.

**Approach:** Implement a dormant, explicitly bound S3 HTTPS provider, exercising complete request/response mechanics through inert streams and synthetic credentials. Repository evidence identifies Scaleway Object Storage, `fr-par`, `https://s3.fr-par.scw.cloud`, and bucket `hexalith-recovery-points`; no production configuration is loaded or selected by default.

## Boundaries & Constraints

**Always:** Pin `ProviderBinding`, original CA bytes/hash, explicit region, reader identity/custody/secret and separate writer hashes. Retain the base refusing interface and default refusal before credentials/network without an injected connector. Independently check exact-version object metadata and `GetObjectRetention`. Preserve existing changes, historical evidence and public activation refusal. Story 4.27 stays in-progress, 4.1 closed, Timesheets 3.6 open/FAIL.

**Never:** Access real credentials/production endpoints; load environment/config/default credentials; allocate recovery attempts/policies; sign recovery documents; upload, mutate, recover, deploy or open operational gates. Only synthetic protocol authentication HMAC is exercised. No SDK defaults, proxy, redirects, retry, list/discovery, writer fallback or latest/null aliases.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Exact read | Bound validator, TLS CA, endpoint, version, matching GET and retention API | Owned metadata/read/close handle; verified length and SHA-256 at bounded EOF | Existing typed driver response only after cleanup |
| Binding drift | Wrong reader/writer/CA/store/endpoint, malformed secret envelope | No dispatch | Fixed refusal |
| Metadata drift | Wrong/missing version, length, encryption, lock, retention; API disagreement | Reject before exposing body | Close every owned stream |
| Hostile HTTP/XML | Duplicate/oversized headers, ambiguous length, encodings, redirect, malformed/DTD/duplicate XML | Bounded rejection, no fallback | Sanitized refusal |
| Body drift | Short/oversized/changed/nonbytes data, surplus after Content-Length | No successful EOF or driver result | Poison and release handle |
| Deadline/cleanup | Expiry during connector, writes, headers, retention/body/EOF/close; close failure or cancellation | One immutable absolute budget; no successful late result | Attempt cleanup independently; primary interrupt preserved |

</frozen-after-approval>

## Code Map

- `eng/recovery-preparation/immutable_readback_driver.py`: retain stable sibling bootstrap, `ProviderBinding`, base interface and driver. Add concrete `S3ImmutableProvider` here, keeping source inventory coverage without new source dependencies.
- `eng/recovery-preparation/recovery_execution_boundary.py`: reuse `Credential.descriptor`, deadline/refusal conventions and strict TLS patterns; preserve boundary and runner behavior.
- `eng/recovery-preparation/recovery_adapters.py`: reuse exact parsed `Artifact`, bounded sizes and timestamp/hash/schema helpers.
- `eng/recovery-preparation/_execution_boundary_fixtures.py`: synthetic credentials, public fixture CA, inert composition; no modifications required.
- `eng/kubernetes-upgrade/recovery_readiness.py:50` and `GUARDED-RECOVERY-PREPARATION.md:174`: both retention representations must match. `evidence/epic-4/recovery-destination-setup.md` establishes S3 destination and separate applications.
- Previous completed Story 4.26: retain historical outcomes, custody separation and independent authority/runtime qualifications. Epic-4 context cache is current.

## Tasks & Acceptance

**Execution:**
- [x] `eng/recovery-preparation/immutable_readback_driver.py` — add explicit provider/CA/region constructor and inert connector injection; strict hostname-verifying TLS using only pinned CA; validate all bindings before connector work.
- [x] Same file — implement read-only SigV4 path-style exact `versionId` GET plus `retention=&versionId=...`; narrow JSON secret `{accessKeyId, secretAccessKey}`; canonical encoding and region/service/date authentication. Retention XML and GET lock headers must match the artifact. Bounded HTTP framing, partial writes, streaming count/hash, terminal EOF and owned cleanup share one absolute deadline.
- [x] `eng/recovery-preparation/test_immutable_s3_provider.py` — cover every matrix row with inert streams and synthetic credentials, verify actual wire authentication against an independent oracle/vector, default refusal, TLS/credential separation, cancellation/cleanup and composition with existing driver for policy/signature/recovery input.
- [x] `eng/recovery-preparation/RECOVERY-EXECUTION-BOUNDARY.md`, `eng/cluster-management/NEXT-OPERATOR-ACTION.md`, story file — append current protocol/results/remaining integrations without rewriting historical checkpoints.
- [x] New `evidence/epic-4/4-27/20261023-immutable-provider-transport/` under implementation artifacts — record focused command/results, source hashes and offline limitations separately from historical evidence.

**Acceptance Criteria:**
- Given matching synthetic observations, when exact read completes, then both provider and driver verify streamed length/hash and all resources close before success.
- Given any matrix failure, when invoked, then no successful recovery input or sanitized success survives and no extra/fallback request occurs.
- Given the public entry point or absent connector, when invoked, then activation remains disabled without credential/config/network access.

## Implementation Notes

Intent gaps: none. Irreversibles: none; offline local changes only. Footprint: one provider module, one focused suite, appended handoffs and separate verification. User authorized preservation of the pre-existing dirty tree and specification followed by implementation; no additional approval checkpoint is needed.

Implemented in the inventoried provider module with 32 focused tests; 233 existing tests also pass (265 total, zero skips). Parent inspection added configuration snapshot, UTF-8/declaration validation and post-hash deadline regressions. Exact historical file and handoff-prefix checks pass. The unrelated Builds submodule changed concurrently; this task made no writes there and records that drift separately. Every matrix row has passing coverage in the focused suite.

Final corrections pass 42 provider tests plus all 233 existing tests (275 total, zero skips); complete final transcripts live in the separate review-1 packet. The exact-trust-anchor test detects an in-memory wrong-CA substitution. All eleven retained review items are resolved, one optional evidence extension was rejected, and no findings were deferred. The scoped implementation spec is done; the enclosing Story 4.27 and sprint remain in-progress by explicit user instruction.

## Spec Change Log

Review pass 1 adds direct implementation/test corrections without changing frozen intent: dispatch binding after callbacks, cancellation-safe ownership, standalone retention expiry, exact TLS trust-anchor assertions and boundary coverage. Keep inert defaults, exact wire authentication, both retention APIs, independently verified streaming, historical evidence and closed operational gates. Initial 265-test evidence/source snapshots remain separate from corrected-source verification.

## Review Triage Log

| Finding | Verdict | Evidence / route |
| --- | --- | --- |
| B1 driver callback binding drift | medium | The dispatch clock callback can change the concrete provider endpoint after the early check. Snapshot provider/binding and validate immediately before dispatch; patch. |
| B2 subclass binding-check bypass | medium | Exact-type check omits inherited S3 implementations. Include inherited providers in the same dispatch binding guard; patch with B1. |
| B3 expired standalone retention | medium | Matching metadata with expired retention can reach verified EOF outside driver composition. Refuse expired retention and clamp the standalone deadline to remaining retention; patch. |
| B4 malformed CA with matching digest untested | low | Digest mismatch cases do not exercise PEM parsing; add a coherent malformed-CA refusal case as a direct test correction; patch. |
| B5 successful body above 65536 bytes untested | medium | Existing small chunks do not establish full-size reads across the advertised cap. Add a large multichunk success test; patch. |
| B6 hostile second-response recv untested | low | Shared framing is tested on the first response, but independent release of both streams deserves explicit second-response cases; patch. |
| B7 shorter per-read deadline untested | medium | The current min is correct, but replacing it with the original deadline would evade existing tests. Cover shorter live and expired caller deadlines; patch. |
| B8 exclusive handle ownership undocumented | low | The driver uses one sequential owner; state that requirement in the private handle contract, avoiding an unnecessary concurrent API; direct documentation patch. |
| B9 vector derivation command not retained | low | Literal target/date/scope/secret, fixed result and independent wire parser already make the vector reproducible. No demonstrated incorrect result; reject optional extra derivation artifact. |
| B10 focused transcript absent | low | The initial record truthfully summarizes observed tool output. The required post-fix verification will capture a complete log; patch evidence then. |
| V1 TLS CA identity verification gap | medium | Reviewer substituted the other fixture CA in memory and all 32 tests still passed. Assert exact independently decoded DER trust anchors; patch. |
| R1 cancellation before release loses ownership | medium | Parent line-level synthetic interrupt before EOF close produced closed=false, retained-owner=false, closeCalls=0. Retain ownership through cleanup and handoff; add line-interruption regressions for open/read/close; patch. |

## Design Notes

Connector contract: `(SSLContext, exact_endpoint, absolute_deadline) -> owned stream` with bounded `settimeout/send/recv` and nonblocking `close`. It must enforce the deadline inside connection/TLS work and own any resources it cannot return. There is no automatically selected live connector. Each subsequent read clamps to the original deadline; later caller deadlines cannot extend it. Provider handles are terminal after failure/close. Failed closes get bounded best-effort release but still refuse success.

S3 has no mandatory stored SHA-256 metadata: normalized `sha256` is the pinned expected hash, verified independently from bytes at EOF by provider and driver; never describe it as a provider checksum. Optional provider hash metadata must not replace body verification. Retention timestamps may normalize zero fractional seconds to the artifact's exact UTC instant. Do not require an undocumented retention-response version header; check it if present and bind the exact query. No plaintext spool or whole-object provider buffering.

Protocol references: [Scaleway supported APIs](https://www.scaleway.com/en/docs/object-storage/api-cli/using-api-call-list/), [S3 GetObject](https://docs.aws.amazon.com/AmazonS3/latest/API/API_GetObject.html), [GetObjectRetention](https://docs.aws.amazon.com/AmazonS3/latest/API/API_GetObjectRetention.html), [SigV4](https://docs.aws.amazon.com/IAM/latest/UserGuide/reference_sigv-create-signed-request.html). Public documentation reads are separate from prohibited production endpoint access.

## Verification

Run `/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_immutable_s3_provider.py`, existing readback/boundary/authentication suites and unchanged common/telemetry suites. Require no skips, no external socket activity, no fabricated operational acceptance. Inspect public activation and append-only preservation separately.
