---
title: '4.27: Native client-certificate delivery'
type: 'feature'
epic: 4
story: 27
created: '2026-10-23'
status: 'in-progress'
baseline_commit: '693390a790d95a9bad9b1bd9cc0f0f3052f3fbef'
route: 'dispatch'
review_loop_iteration: 1
context:
  - '{project-root}/eng/recovery-preparation/RECOVERY-EXECUTION-BOUNDARY.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The retained native recovery client uses a certificate/key pair, but `NativeTLS` delivers bearer tokens only.

**Approach:** Add explicitly selected, custody-bound certificate delivery alongside bearer authentication, verified offline with public activation disabled.

## Boundaries & Constraints

**Always:** Pin profile, principal, audience, credential bytes and identity/custody hashes. Preserve exact CA trust, deadlines, native requests, source/target separation and cleanup. Load keys through sealed memory descriptors. Preserve historical evidence: 4.27 in-progress, 4.1 closed, Timesheets 3.6 open/FAIL.

**Never:** Access actual credentials/configuration/endpoints; allocate recovery attempts/policies; issue operational certificates; sign recovery documents; upload, recover, mutate infrastructure or activate execution. No profile sniffing, fallback, mixed authentication or persistent/logged keys. Synthetic fixtures confer no authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Client certificate | Bound PEM pair, explicit profile | Install exact pair; omit Authorization | Cleanup/deadline before success |
| Bearer | Bound bearer profile | Preserve wire behavior and audience selection | No fallback |
| Binding drift | Unknown/mixed profile, altered principal/audience/identity/custody/secret | Refuse before dispatch | Sanitized refusal |
| Invalid material | Malformed/mismatched pair or unsupported encrypted key | Refuse without prompt or lookup | Close all owned descriptors |
| Timeout/cancellation | Expiry/interrupt during loading, exchange or cleanup | No late success; preserve primary interrupt | Independent cleanup; no retry |
| Default invocation | Missing connector or public activation request | Refuse before credential/config/network access | Gate stays closed |

</frozen-after-approval>

## Code Map

- Paths below are under `eng/recovery-preparation/` unless stated otherwise.
- `recovery_execution_boundary.py`: extend `NativeBinding`/`NativeTLS`; reuse `Credential.descriptor`, `sealed`, `_close_owned`, `remaining`. Explicit profile defaults to bearer for compatibility; certificate/key envelope stays inside hashed secret bytes.
- `_execution_boundary_fixtures.py`, `test_recovery_execution_boundary.py`: reuse distinct clusters/credentials and inert composition.
- `RECOVERY-EXECUTION-BOUNDARY.md:73` documents the gap; `eng/cluster-management/NEXT-OPERATOR-ACTION.md:91` records the certificate profile. Read documentation only.
- Preserve historical transport/enrollment modules, common schemas and S3. Story 4.26 keeps actual qualification separate from fixtures.

## Tasks & Acceptance

**Execution:**
- [ ] `recovery_execution_boundary.py` — add bounded certificate parsing, sealed descriptor installation and header selection; retain mandatory connector injection and existing guards.
- [ ] `test_native_client_certificate.py` (new), `_execution_boundary_fixtures.py`, `test_recovery_execution_boundary.py` — cover every matrix row using static synthetic fixtures, real `SSLContext` loading and socket-free `MemoryBIO` peer verification; include missing/wrong-key controls, sealing, cancellation, descriptor preservation and source/target composition.
- [ ] `RECOVERY-EXECUTION-BOUNDARY.md`, `eng/cluster-management/NEXT-OPERATOR-ACTION.md`, `_bmad-output/implementation-artifacts/4-27-retire-kubesphere-without-changing-workload-data.md` — append results and remaining prerequisites.
- [ ] `_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/` (new) — record commands, results, matrix coverage and source hashes separately from historical evidence.

**Acceptance Criteria:**
- Given distinct credentials, when all four plans compose, then native exchanges preserve bound audiences, client identities and semantic guards.
- Given a no-op/wrong-certificate loader, when handshake tests run, then peer verification detects incorrect authentication.
- Given passing fixtures, when handoffs change, then credential/authority/runtime qualification, system/target/proof-consumer integration and actual recovery remain pending; gates stay closed.

## Implementation Notes

No intent gaps or irreversible effects; local code/tests/docs only. Clean starting tree on `main`, HEAD `693390a790d95a9bad9b1bd9cc0f0f3052f3fbef`. Capture this spec's baseline at implementation; preserve the parent baseline.

## Spec Change Log

### Review 1 — descriptor ownership re-derivation

Re-derive the implementation from the exact initial snapshots in `evidence/epic-4/4-27/native-client-certificate-delivery/sources/`; implementation files were returned to the preserved baseline before dispatch. Those snapshots and the initial receipts remain historical candidates. Do not replay historical recovery tools or replace prior packets.

The initial allocator returns a raw descriptor before its caller registers ownership; a return/opcode interruption leaks that descriptor. The shared closer can retry a descriptor number after a successful close and close a replacement opened by an interruption handler. A second cleanup-transition interrupt can also replace the original loading interrupt. Root reproduced all three using synthetic descriptors and no sockets. Keep ownership and the primary error available across allocation, installation, cleanup and exceptional returns. Register ownership within allocation before returning; use an owned close abstraction/state that cannot retry a released raw number. A loading interrupt must survive a later cleanup interrupt while all remaining owned resources receive cleanup attempts. Preserve existing callers and safe parent descriptors. These requirements add no public activation or credential acquisition.

Accept ordinary valid PEM CRLF and missing-terminal-newline forms without changing the original bytes installed. Keep strict envelope fields, material/count bounds and encrypted-key refusal. Add real loading/handshake coverage for traditional RSA and EC keys, a valid certificate chain and an independently isolated over-limit count case. Make the fd 3 precondition explicit in its isolated fixture, retaining its bytes/flags.

KEEP all 297 existing passing behaviors/tests, exact source/target peer DER and CA checks, no-op/wrong-loader controls, all four plans, native wire/preconditions, bearer compatibility, sealed memory custody, semantic stops, null qualifications and disabled activation. Preserve the approved frozen block, parent status and historical handoff prefixes. Retain a new corrected-source packet and append updated handoffs; do not overwrite the initial candidate. Keep the initial spec hash reproducible using `spec-implementation-checkpoint.md`, and retain the independent probe invocation with new evidence.

## Review Triage Log

### Review 1 — three independent layers

| Finding | Verdict | Evidence / route |
| --- | --- | --- |
| B1 allocator return before registration | medium | Root return-event probe leaked one sealed descriptor before append; register inside allocation and retain ownership across returns. Group with E1/V1; bad_spec. |
| B2 completed-close retry | medium | Root interrupted after close, opened a replacement with the same number, and observed the helper close that unrelated descriptor. Correct owned close semantics; bad_spec. |
| B3 valid PEM formatting | medium | Real SSLContext accepted CRLF/no-terminal-newline pairs while the boundary refused before dispatch. Preserve exact valid PEM forms; direct correction included in re-derivation. |
| B4 spec receipt hash | low | Review status legitimately changed after the initial receipt. Exact original bytes matching its hash are now retained in spec-implementation-checkpoint.md; preserve the receipt and bind final state separately. Direct evidence correction. |
| B5 traditional key positive coverage | low | Tests use EC PKCS#8 only while the parser/docs support traditional RSA/EC. Add direct real-loader regressions during re-derivation. |
| B6 chain/count coverage | low | The oversized repeated-chain case does not isolate the count limit, and no positive chain exists. Add direct chain and count cases during re-derivation. |
| B7 same-CA alternative negative control | low | Existing real-peer and composition checks independently assert exact DER, and the wrong-issuer control proves loader replacement fails. A new same-CA credential family adds fixture complexity without a demonstrated surviving defect; reject optional extension. |
| B8 implicit fd 3 precondition | low | Descriptor snapshots check all existing descriptors, but the fixture does not explicitly arrange fd 3 and its contents. Make the precondition explicit as a direct test correction. |
| B9 reverse mixed profile coverage | low | Both-certificate plans already exercise real TLS at both endpoints; mixed selection exercises source certificate/target bearer only. No incorrect selection was demonstrated; reject extra branch/fixture complexity. |
| B10 independent probe retention | low | Original report truthfully identifies parent tool-transcript provenance; preserve the exact executable invocation with corrected evidence for reproducibility. Direct evidence correction. |
| E1 allocator return before registration | medium | Root reproduced the same leaked descriptor on a sealed return event; group with B1/V1, bad_spec. |
| E2 primary interrupt replaced | medium | Root injected loading KeyboardInterrupt followed by cleanup-transition SystemExit; the latter escaped. Preserve the shared primary error through cleanup; bad_spec. |
| V1 allocator transfer Other finding | medium | Verified alongside B1: outer and helper lists are both empty at interrupted transfer; group with B1/E1, bad_spec. |

No findings are deferred. Ownership entries require coherent internal re-derivation; the remaining direct corrections are included in that pass. Frozen intent is unchanged and no new operational approval is inferred.

## Verification

- `/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_native_client_certificate.py` — all matrix rows pass; no skips/sockets/actual credentials.
- Run existing boundary/authentication/readback/S3 suites; broaden only for changed contracts or failures.
- `git diff --check`; verify preserved evidence and handoff prefixes. No staging, commits, push or historical executor replay.


### Implementation verification — 2026-10-23

- Captured the original spec bytes in `evidence/epic-4/4-27/native-client-certificate-delivery/spec-baseline.md`; SHA-256 `f72434a4c132f84aa4da71cba698b701cd27e4ff3ecb4431002f9daae07afb1a`. Parent baseline `693390a790d95a9bad9b1bd9cc0f0f3052f3fbef` remains unchanged.
- Corrected-source seven-suite verification passes 297 tests, zero skips: certificate 21, boundary 45, readback 12, policy authentication 13, S3 42, common adapter 94 and telemetry 70. Full commands, transcripts, source hashes and matrix coverage are in the separate native-client-certificate-delivery packet.
- Parent independent cancellation audit first observed two unreleased sealed descriptors when interrupting the first installation-cleanup line. Exchange ownership now persists through that transition; the focused regression passes and the repeated parent probe reports zero unreleased descriptors with the primary interrupt preserved. The initial 296-test logs remain under `first-candidate/` and do not qualify corrected code.
- Handoff prefixes and tracked historical evidence are byte-preserved; frozen intent, historical executor/enrollment modules, common schemas, S3 and parent baseline remain unchanged. No staging, commits, push or historical executor replay occurred.
- Fixture results confer no operational authority. All actual credential/authority/runtime, store/system/target/proof-consumer and recovery qualifications remain pending. Public activation stays disabled; 4.27 stays in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.
