---
title: '4.27: Native client-certificate delivery'
type: 'feature'
epic: 4
story: 27
created: '2026-10-23'
status: 'in-progress'
baseline_commit: '693390a790d95a9bad9b1bd9cc0f0f3052f3fbef'
route: 'dispatch'
review_loop_iteration: 0
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

## Review Triage Log

## Verification

- `/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_native_client_certificate.py` — all matrix rows pass; no skips/sockets/actual credentials.
- Run existing boundary/authentication/readback/S3 suites; broaden only for changed contracts or failures.
- `git diff --check`; verify preserved evidence and handoff prefixes. No staging, commits, push or historical executor replay.
