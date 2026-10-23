---
title: '4.27: Implement the authenticated recovery execution boundary'
type: 'feature'
epic: 4
story: 27
created: '2026-10-23'
status: 'done'
route: 'dispatch'
baseline_commit: '858d02c05eb837a4ee85c5215430201f117dc24d'
review_loop_iteration: 1
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Revision-4 preparation lacks native execution, independent immutable readback and policy-authentication drivers.

**Approach:** Implement these drivers and test their composition with the existing runner using inert transports and local fixtures. Public live activation stays disabled; store orchestration and actual recovery remain separate prerequisites.

## Boundaries & Constraints

**Always:** Preserve existing sources/packets/signed originals and scoped destination, signer, validator, owner and maintenance decisions. Bind request/tool/dependency bytes and credential identities; enforce bounded I/O/deadlines and sanitized errors. Fixture success leaves qualification null, 4.27 in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.

**Never:** Access actual credentials/endpoints or perform recovery/infrastructure/signing/upload operations. Supply no public activation, ambient authentication, discovery, shell, redirect, retry or historical-executor fallback. Allocate no RID/policy, refresh no expiry and trust no authentication boolean.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior | Failure |
| --- | --- | --- | --- |
| Composition | Bound requests and inert drivers | Typed responses; authentication before dependent requests | No operational acceptance |
| Identity drift | Wrong tool, dependency, cluster, CA, principal or credential custody | Refuse before dispatch | Sanitized stop |
| Immutable input | Exact version, bytes, hash, length, AES256, COMPLIANCE and retention | Return only independently checked input | Unknown/mismatch stops |
| Signature | Original buffers, pinned signer/trust/namespace and exact policy scope | Cryptographic verification then scope/window checks | Changed bytes/scope stops |
| Partial I/O | Drip, overflow, stalled stdin, expiry or uncertain write | Enforce one deadline and release owned resources | No retry or compensation |
| Live request | Public activation/configuration selector | Refuse before configuration/credential access | Zero external requests |

</frozen-after-approval>

## Code Map

- `eng/recovery-preparation/recovery_adapters.py` -- unchanged types, runner, exact signature inputs and deadlines.
- `eng/recovery-preparation/native_https_transport.py`, `static_transport_preflight.py` -- reference stream/process/custody mechanics; preserve historical bindings.
- `eng/cluster-management/retire.py:SignedInputs` -- signature reference; never import its executor.
- [Revision-4 receipt](evidence/epic-4/4-27/20261023t085529z-s427-recovery-command-preparation/verification.json) -- completed preparation, preserved sources and remaining integration.

## Tasks & Acceptance

**Execution:**

- [x] `eng/recovery-preparation/recovery_execution_boundary.py` -- implement `exchange(Request, absolute_deadline)`, native TLS and bounded process protocols. Validate the bound plan, endpoint/CA/identity and tool/dependency bytes. Deliver sealed credential descriptors with fixed environment; bound partial I/O/cancellation, preserve uncertain effects and disable public activation.
- [x] `eng/recovery-preparation/immutable_readback_driver.py` -- implement an explicit injected validator/provider interface. Check raw metadata/bytes against version, identity, length/hash, AES256, COMPLIANCE, retention and principal separation. Unavailable concrete transport remains unresolved; never substitute writer credentials.
- [x] `eng/recovery-preparation/policy_authentication_driver.py` -- cryptographically verify original buffers with pinned trust/signer/namespace and bounded verifier. Check exact policy scope/dates/retained decisions before returning the existing authentication response; never reserialize signed input.
- [x] `eng/recovery-preparation/test_recovery_execution_boundary.py`, `test_immutable_readback_driver.py`, `test_policy_authentication_driver.py` -- cover every matrix row, runner ordering, distinct clusters/principals, real fixture-process bounds and stalled/partial I/O. Use synthetic credentials/public signatures; prohibit external requests.
- [x] `eng/recovery-preparation/RECOVERY-EXECUTION-BOUNDARY.md` -- document interfaces, trust/custody, safe fixture commands, cancellation limits and remaining provider/system/target/proof-consumer integration and actual qualifications.
- [x] `eng/cluster-management/NEXT-OPERATOR-ACTION.md` and `_bmad-output/implementation-artifacts/4-27-retire-kubesphere-without-changing-workload-data.md` -- append verified implementation/test outcomes with unchanged historical prefixes and gates. Bind new evidence separately from revision 4.

**Acceptance Criteria:**

- Given matching fixtures, when drivers serve the runner, then authentication precedes dependent requests and semantic guards remain effective.
- Given drift/timeout/unknown evidence, when its boundary is reached, then dispatch stops, receipts persist and acceptance remains unestablished.
- Given completed implementation, when reviewing its handoff, then implemented interfaces and remaining dependencies are explicit; no actual recovery is claimed.

## Implementation Notes

- The scoped continuation is done. The approved frozen constraint retains parent Story 4.27 and sprint in-progress, overriding the generic review-status sync for whole stories. All operational gates remain closed/pending as recorded.

- Final patched-source verification passes 233 cases (69 new + 164 unchanged), no skips. Exact emitted credentials and CA installation are additionally checked by killed in-memory mutants. The expired-policy negative case now refuses before verifier work; other original coverage remains. Both earlier candidate packets and the 157-file historical inventory remain unchanged. [Final verification](evidence/epic-4/4-27/20261023-recovery-execution-boundary/review-1/final/verification.json) records scope and remaining integrations; no operational qualification or parent-story completion is inferred.

- Review-1 corrections retain all original 38 test methods and add current regressions. Parent verification passes 225 cases (61 new + 164 unchanged), verifies all six matrix rows, and preserves the first candidate packet separately. [Current corrected-source evidence](evidence/epic-4/4-27/20261023-recovery-execution-boundary/review-1/verification.json) remains unsigned, offline and unqualified.

- Implemented the three dormant drivers, shared synthetic fixtures, adversarial suites and append-only handoff. Parent corrected free/occupied fd-3 handling and retained the initial evidence before refreshing final verification. Actual provider transport, runtime qualification and later recovery integration remain explicitly unresolved.

- Owner approved this continuation and implementation on 2026-10-23. Preserve the approved frozen block and baseline. Unrelated concurrent composition, submodule and tracking edits are outside this change.

## Spec Change Log

### Review 1 re-derivation — 2026-10-23

The reviewed new source is archived privately at `/tmp/s427-execution-boundary-gtrm5ukv/review-1-source/`; its manifest pins exact positive implementation bytes. Re-derive the three drivers, shared fixture, tests and documentation from that snapshot, retaining the complete correction set below. Earlier implementation receipts/logs remain historical, unqualified fixture results. Do not overwrite any baseline source or revision-4 packet.

KEEP: all 38 existing new tests and all 164 unchanged adapter cases; exact original signature buffers and the real positive/negative public OpenSSH vector; typed interfaces and policy materialization; native source/target and validator separation; sealed credentials, checked source loading/no pyc, fixed environment, no ambient discovery, no public activation, null operational acceptance, and every historical preservation bound. Keep the 157-file inventory and two original document prefixes. The approved frozen block and baseline are unchanged.

Re-derivation requirements:

- `recovery_execution_boundary.py` -- atomically claim each exchange. Concurrent and reentrant calls cannot dispatch the same request, advance the index twice or skip authentication. Require completed authentication before any dependent native/system dispatch, independently of the numeric index. Add deterministic barrier-based concurrency and reentrancy tests with inert drivers, including the demonstrated duplicate signature-readback/authentication bypass.
- Same file -- retain timestamps only from successfully checked clean-drain and creation-fence observations. Recheck the exact relevant prerequisite immediately before guarded inventory, replica PATCH and owned DELETE dispatch, after custody/credential work. Clamp the delegated deadline to remaining prerequisite validity as well as request/policy limits. Direct ordered calls and runner composition must both stop on stale/future observations; test the 31-second cases and aging during dependency scans.
- Same file -- every escaping cancellation/BaseException path poisons the boundary, including an interrupted uncertain write; preserve appropriate interrupt propagation and pending receipts. No retry/compensation/complete receipt follows. Add the demonstrated KeyboardInterrupt/retry regression.
- All drivers/process mechanics -- recheck absolute time and policy validity after final dependency scans and owned cleanup, before returning success or advancing state. Tests must expire the clock/window only during the final scan/close; retain honest limits for uninterruptible kernels and arbitrary injected functions.
- `BoundedProcess` -- allocate executable and input descriptors above stdio and credential destinations; preserve parent-owned descriptors and originally closed standard descriptors. Test each closed standard descriptor in an isolated child as well as the existing free/occupied fd-3 cases. Keep process-group ownership until signalling is complete; observe exit without reaping before signalling, then reap. Never signal a potentially recycled identifier. Test the ordering without killing unrelated processes.
- Same process mechanics -- attempt every owned stream/descriptor close and bounded reap even if signalling/wait/close fails. Sanitize reported cleanup errors, preserve cancellation semantics and prove absence of avoidable leaks with injected cleanup failures. No raw cleanup diagnostic may replace the fixed boundary error.
- `policy_authentication_driver.py` -- require `type(schemaVersion) is int` and version 1 on the original verified payload; add a coherently rebound boolean-version refusal.
- `NativeTLS` -- require bounded EOF after the declared body for this single-request connection-closing profile. Reject surplus bytes and expiry during EOF/close. Add exact emitted PATCH/DELETE body, content length/type, method/path/host and UID/resourceVersion precondition assertions; they must detect the empty-body mutation.
- `_execution_boundary_fixtures.py` and the new suites -- validate the complete stage envelope in both the real sealed-fd process and inert all-operation protocol: routing/request digest plus both distinct audiences, principals, identity/custody hashes and original synthetic secret bytes. The [] credential mutation and altered-record variants must fail.
- `RECOVERY-EXECUTION-BOUNDARY.md` and append-only handoffs -- document the corrected state/ownership/timing contract and actual new checks. Refresh separately bound evidence only after current-source tests pass. Do not promote earlier fixture success to operational qualification or claim missing provider/system/target/proof-consumer integration is complete.

## Review Triage Log

### Review 2 — corrected current source

All fifteen findings were classified independently before grouping. Parent inert probes are retained privately; verification gaps include executed mutation evidence.

| Finding | Verdict | Route | Evidence and disposition |
| --- | --- | --- | --- |
| Blind 2.1 | high | patch | Parent reproduced bytearray stdin matching the approved request, changing during credential selection, emitting replicas 9 instead of 2 and returning fixture success. Dispatch the boundary-owned immutable expected request after matching; add a mutable-input regression. |
| Blind 2.2 | low | reject | Parent reproduced late poison after the active call had completed its final validation: one valid result returned, index advanced once, stopped=True, and the next request refused. No duplicate dispatch or authentication bypass occurs. This overlap can linearize the completed call before the failed duplicate; additional commit/cancellation machinery is unnecessary for this late observation window. |
| Blind 2.3 | medium | patch | A parent-injected first final close failure in read_custody exposed raw OSError text and leaked file/directory descriptors. Independent cleanup and fixed-error handling must also cover the duplicated bootstrap readers. |
| Blind 2.4 | medium | patch | The parent observed an original KeyboardInterrupt replaced by a cleanup KeyboardInterrupt. Preserve the original escaping interrupt while attempting the owned provider close. |
| Blind 2.5 | medium | patch | A mock-only connect interruption left the owned socket open because _connect catches Exception only. Catch cancellation, close owned raw/TLS handles and preserve the original interrupt. |
| Blind 2.6 | low | patch; group with direct driver budgets | An already expired standalone policy correctly refused authentication but still invoked the verifier. Preflight the binding window as part of local resource-budget enforcement; strengthen the old negative case to zero verifier calls rather than dropping it. |
| Blind 2.7 | medium | patch | Parent spies observed standalone reader and verifier forwarding a 120-second caller deadline despite the 15-second request limit. Clamp each direct driver to caller/request/policy budgets; preserve final expiry checks. |
| Blind 2.8 | medium | defer | Confirmed in pre-existing concurrent C# changes: MarkFailed suppresses marker-write failure while Dispose uses marker existence alone, so an existing failed workspace may be deleted. This continuation did not change composition source; retain the user edits and defer the separate correction. |
| Blind 2.9 | medium | defer | Confirmed in pre-existing Works preview code: nearest nested AppHost markers can become the supposed Platform checkout root, leaving other paths inside the actual checkout unprotected. This is outside the driver change and predates it in the captured worktree. |
| Blind 2.10 | medium | patch documentation | A process group cannot contain a descendant after setsid. Current fixtures cover descendants retaining that group only. Document the cooperative non-daemonizing tool qualification requirement and the absence of whole-tree sandbox/cancellation enforcement; do not add infrastructure or claim detached descendants are controlled. |
| Edge 2.1 | low | reject | Same late poison observation as Blind 2.2. The active valid response had already passed final guards; only one call commits, and all later requests refuse. No second request or new mutation follows the poison, so a new commit-lock protocol would add complexity without fixing the demonstrated authentication/data invariants. |
| Edge 2.2 | medium | patch | Parent interrupted Popen._execute_child after launch and confirmed that constructor failure left a live owned child while BoundedProcess had child=None. Retain construction ownership before initialization so cancellation can kill/reap that exact child. |
| Edge 2.3 | medium | patch | Same dormant-connector cancellation cleanup failure as Blind 2.5. Native TLS mechanics are part of the approved implementation even though live activation remains disabled. |
| Verification 2.1 | medium | patch | Pre-verified forcing all native bearer values to synthetic passed all 61 suites. Add full source/target exchanges asserting the exact selected secrets in emitted Authorization headers. |
| Verification 2.2 | medium | patch | Pre-verified returning an empty CA store passed all 61 suites. Observe connector get_ca_certs(binary_form=True) against each cluster's pinned certificate, retaining the existing hostname/CERT_REQUIRED checks. |

Groups: native connector interruption (Blind 2.5 / Edge 2.3) and direct-driver resource budgets (Blind 2.6 / 2.7). The late-commit observations are individually rejected as low-impact overlapping completion, with no duplicate dispatch or skipped authentication. Other survivors remain distinct. Existing public signatures, frozen intent, original packets and tests remain preserved; the expired-policy negative case is strengthened to refuse before work under an expired binding. All current-driver corrections are localized private implementation/test changes with no new public surface. Two unrelated composition defects are deferred; no intent gap or new spec loopback is needed.

### Parent precheck after re-derivation

| Finding | Verdict | Route | Evidence and correction |
| --- | --- | --- | --- |
| Memfd construction cleanup | medium | patch | Parent injected the initial low-descriptor close failure and observed two leaked descriptors plus raw OSError text. Allocation/duplication/low-close now share protected cleanup; two regressions cover close/duplication failures and interrupts. |

### Review 1 — authenticated execution boundary

Each finding was classified before grouping. Parent inert probes confirm the implementation failures; verification-gap claims include executed mutation evidence. No production/provider request or actual credential was used.

| Finding | Verdict | Route | Evidence and disposition |
| --- | --- | --- | --- |
| Blind 1 | high | bad_spec | Parent inert probes accepted resume PATCH and cleanup DELETE after their validated drain/fence observations aged 31 seconds. The boundary must retain and recheck these prerequisites after its own custody work. |
| Blind 2 | medium | bad_spec | KeyboardInterrupt during an ordered PATCH left stopped=False and permitted the same PATCH to be retried. Poison escaping cancellation paths while preserving interrupt propagation. |
| Blind 3 | medium | bad_spec | No atomic request claim protects the index. The reported duplicate PATCH is consistent with the verified concurrent readback race; serialize or refuse concurrent/reentrant exchanges. |
| Blind 4 | medium | bad_spec | Parent delayed the final dependency scan past the absolute deadline and exchange still returned 200. Apply final time/window checks after custody and owned cleanup. |
| Blind 5 | medium | patch; retain through re-derivation | Parent authenticated fixture policy schemaVersion=true because Python dictionary equality treats True as 1. Require the exact integer schema type. |
| Blind 6 | low | reject | Parent confirmed fabricated sys.modules annotations can select a cached object. This requires deliberate in-process module fabrication under private names; the caller already controls executable Python. It is outside ordinary use, and a provenance registry adds complexity without an independent in-process trust root. |
| Blind 7 | medium | patch; retain through re-derivation | Parent accepted a Content-Length-framed body with unread surplus bytes. Require bounded EOF for the declared single-request connection-closing protocol. |
| Blind 8 | medium | bad_spec | An isolated child with stdin closed refused an otherwise valid composition: low descriptor allocation can place the executable at the credential destination. Allocate executable/input descriptors above all reserved destinations. |
| Blind 9 | medium | bad_spec | An injected killpg PermissionError escaped its raw diagnostic and left five owned descriptors open. Attempt every cleanup independently and sanitize cleanup failure. |
| Blind 10 | medium | bad_spec | Parent observed group signalling after the leader had already been reaped; no ownership protection remains against identifier reuse during the final scan. Retain ownership until signalling is complete, then reap. |
| Edge 1 | high | bad_spec | Two concurrent signature-readback calls both succeeded, advanced index 1 to 3 and permitted native dispatch with zero verifier calls. This independently demonstrates an authentication bypass from the same request-claim race as Blind 3. |
| Edge 2 | high | bad_spec | Independently reported stale-drain PATCH is confirmed by the parent 31-second probe. Same drain-freshness root cause as Blind 1. |
| Edge 3 | high | bad_spec | Independently reported stale-fence DELETE is confirmed by the parent cleanup probe. Same fence-freshness root cause as Blind 1. |
| Edge 4 | medium | patch; retain through re-derivation | Same surplus native-response acceptance as Blind 7; parent reproduced unread EXTRA_RESPONSE_BYTES. |
| Edge 5 | medium | patch; retain through re-derivation | Same schemaVersion=true acceptance as Blind 5; parent fixture verification reached a successful authentication response. |
| Edge 6 | medium | bad_spec | Same post-scan late success as Blind 4. The parent advanced the monotonic clock only at the final inventory check and observed successful return. |
| Edge 7 | medium | bad_spec | Same reaped-leader signal ordering as Blind 10. Code and the parent event probe show child.returncode already populated before killpg. |
| Verification 1 | medium | patch; retain through re-derivation | Pre-verified mutation replacing every native body with empty bytes passed all 20 boundary tests. Add exact PATCH/DELETE wire body, length, content-type and precondition assertions. |
| Verification 2 | medium | patch; retain through re-derivation | Pre-verified mutation replacing the stage credentials list with [] passed all 20 boundary tests. Validate the complete real fd-3 envelope and all-operation inert envelope against bound synthetic credentials. |

Groups are freshness (Blind 1, Edge 2/3), atomic request ownership (Blind 3, Edge 1), final timing (Blind 4, Edge 6), schema type (Blind 5, Edge 5), framing (Blind 7, Edge 4), and process ownership (Blind 10, Edge 7). Cancellation, descriptor allocation, cleanup and both verification gaps remain distinct. Blind 6 is rejected under the low-impact complexity rule. No finding is deferred. Required non-frozen contract corrections trigger re-derivation; smaller patch findings are retained in that correction set.

## Verification

- Run the three new suites with `/usr/bin/python3.14 -I -S -B`; no skips/external requests.
- Run common and telemetry suites once after integration; preserve all 164 cases and existing source bytes.
- Audit matrix coverage, run `git diff --check` and compare preserved sources/packets to a pre-implementation hash inventory.
