---
title: '4.27: Native client-certificate delivery'
type: 'feature'
epic: 4
story: 27
created: '2026-10-23'
status: 'in-progress'
baseline_commit: '693390a790d95a9bad9b1bd9cc0f0f3052f3fbef'
route: 'dispatch'
review_loop_iteration: 3
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

Implementation dispatch coordination: the parent session already rendered and owns the bmad-build workflow. This subagent task is its Step 3 handoff; implement from this file without recursively rendering or re-entering that workflow.

## Spec Change Log

### Review 3 — complete ownership handoffs and bounded cleanup

Re-derive from the exact `native-client-certificate-delivery/review-2/sources/` candidate, preserving that entire 315-test packet and all earlier packets. Implementation files are returned to the original baseline before dispatch. KEEP all 315 passing behaviors, 39 focused test names, 24 registration subcases, the new raw-result/reuse/fallback tests, fixture collection checks, byte/count controls, exact TLS identities/wire/CA assertions, disabled activation and null operational qualification. Keep frozen intent and all historical handoff prefixes unchanged.

Parent independently reproduced the review probes with `/usr/bin/python3.14 -I -S -B /tmp/bmad427-blind-probes.py`; source and results are available at `/tmp/bmad427-blind-probes.py` and `/tmp/bmad427-blind-probes.json`. Retain them with their original adverse results in a new `review-3/` packet; supply updated executable regressions as current evidence, rather than relabeling old invocations as passing.

- Acquire native streams into shared custody before a connector can return or its caller can be interrupted before STORE_ATTR. Define an explicit private owned-connector handoff; audit repository callers and adapt fixtures consistently. A known stream must already have an owner during acquisition and constructor/return interruption. Keep explicit connector injection and refusal before live work; never silently select a native connector or authentication fallback. Preserve bearer wire behavior. Actual connector/runtime qualification remains pending.
- Enforce the expected allocator identity at registration, not only during an earlier enumeration. A descriptor replaced after discovery and before registration must not be adopted or closed. Preserve raw transfer recovery, completed-close/reuse safety and all registration interruptions. Qualify the tested process-local ownership protocol, without claiming protection from arbitrary hostile descriptor-table mutation outside that protocol.
- Recovery must work or refuse without leaking if the memory allocation consumes the last available descriptor. Do not depend on opening an extra `/proc/self/fd` directory after the interrupted allocation. Arrange recovery resources before acquisition, with owned C-level release state, or use another bounded mechanism that retains exact identity. Cover isolated RLIMIT_NOFILE exhaustion, including at least the raw memfd-result transition; restore the child process's limit and preserve unrelated descriptors. No actual host or operational limit changes.
- Scope primary-error recovery to the current exchange. A caller's already handled KeyboardInterrupt in `__context__` must not replace a new loading interruption. Retain the original current-exchange interrupt through secondary recording/cleanup failures, including the existing double-interrupt regressions.
- Give owned streams bounded independent cleanup covering two interruptions inside close before release, plus the already tested entry transitions. Preserve the first current-exchange cancellation and attempt all remaining resources. Define and test safe idempotent release semantics: a close that completed before Python bookkeeping/cancellation must not retry a reused unrelated descriptor. Do not claim safety for arbitrary unsupported/non-idempotent third-party closers, infinitely repeated interruptions or kernel stalls.
- Restore fixture-owned cleanup when Native.exchange allocates its inert stream before context/profile validation; invalid profile/CA refusal must leave no open fixture stream.
- Extend the new content checker to compare recorded unittest totals/skips/outcomes against the actual bound transcripts, with an isolated coherent metadata/count negative control. Preserve the review-2 checker unchanged and document historical spec checkpoints separately from later workflow status/triage edits. Produce a new review-3 source/log/runtime/checkpoint packet, not edits to review-2. The unsigned receipt confers no external authenticity or operational authority.

All changes remain local code/tests/docs. No credential/configuration/endpoints, operational certificate issuance, recovery attempt/policy allocation, signing/upload/recovery, staging/commit/push or operational helper replay. Another shared-workspace operation recorded candidate changes at `56c783bcd005e3ce0c728dc72e440a696a91d034`; preserve that history and unrelated work. Preserve the original baseline. Original story/sprint 4.27 remain in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.

### Review 2 — raw allocation and outer cleanup

Re-derive from `evidence/epic-4/4-27/native-client-certificate-delivery/review-1/sources/`, preserving that complete 305-test candidate and its packet. The implementation files are again returned to the original baseline before dispatch. KEEP every prior positive behavior, test name and logged preservation requirement, including stateful owners, format/chain tests and all 24 registration subcases.

Root reproduced raw memfd/duplicate handles lost before STORE_FAST assigns the syscall result. Ownership must cover that underlying handoff too. Establish a unique local allocation identity in shared custody before creating the memory file, and recover only this allocator's exact matching memory descriptors after an interrupted result transfer. A duplicate shares that new memory-file identity. Never claim, close or retry unrelated/reused descriptors. This is cleanup of newly allocated process-local memory, not recovery-attempt allocation, service discovery or operational scope. Preserve the compatibility contract of legacy raw callers without claiming those historical paths gain this qualification.

Root also reproduced primary interruption replacement while `remember` initially assigns the error and at exchange's final cleanup entry, plus an open owned stream when cancellation skips the outer stream-close line. Guard the whole native lifetime through final cleanup. Preserve the original active interruption even if initial recording or later cleanup is interrupted; its exception context is available when a second interruption occurs during the first handler. Keep the shared owner/error state available to the outer guard. Attempt release of all known owned streams and descriptors, including interruption at cleanup entry. Add direct regressions for these demonstrated transitions and custody-backed duplication, write, chmod and sealing failures; do not replace prior tests with early refusals that never reach their injection sites.

Correct the certificate-count test by asserting the over-limit loader mock was not called; its raised assertion currently supplies the expected refusal after a mutated count guard. Add independent material-size refusal checks whose envelopes remain within the other bounds and assert no loader dispatch. Fixture collections must distinguish None from explicit empty inputs and reject mismatched lengths instead of silently using defaults or truncating zip.

Retain a new `review-2/` packet and append final handoffs. Include a content-based executable receipt checker for recorded source/snapshot/log hashes and frozen spec checkpoints; demonstrate coherent working/snapshot drift still fails the receipt binding. Explain the prior checker's precommit HEAD/index restriction without modifying it. Record Python patch version, interpreter identity, OpenSSL version and OS/release/architecture, omitting hostnames. Preserve unrelated concurrent Story 2.3/deferred-ledger edits and identify their exclusion from this scoped work. No new permission checkpoint or operational action is required.

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

### Review 2 — corrected candidate

| Finding | Verdict | Evidence / route |
| --- | --- | --- |
| B21 raw syscall result before assignment | medium | Root opcode probes at STORE_FAST low/fd each left one descriptor open. Register a recoverable allocator identity before the raw syscall; group with E21, bad_spec. |
| B22 interruption during primary recording | medium | Root loading KeyboardInterrupt followed by SystemExit at remember's assignment propagated the secondary. Preserve the active original context through recording; bad_spec. |
| B23 outer descriptor cleanup transition | medium | Root reproduced secondary SystemExit escaping the final exchange cleanup line despite stored primary. Group with E23; bad_spec. |
| B24 custody-backed internal failure coverage | low | Existing failure cases mainly cover the compatibility branch or preallocation refusal; add direct new-branch duplication/write/chmod/sealing regressions during re-derivation. |
| B25 isolated material-size bounds | low | Existing oversized envelope/certificate cases conflate bounds. Add direct limit-isolating, no-loader-dispatch assertions during re-derivation. |
| B26 fixture default/truncation | low | Explicit empty collections are replaced by bearer defaults and zip silently truncates unequal lengths. Directly distinguish None and check lengths. |
| B27 executable receipt hash verification | low | Prior checker verifies preservation/snapshot equality, while root separately checked receipt hashes. Add a complete content-based receipt checker so later reviewers can reproduce that check directly. |
| B28 precommit-only checker | low | It intentionally binds the preparation HEAD/index; that limitation was not explicit beside its invocation. Document the limitation and supply the new content checker without rewriting prior evidence. |
| B29 missing runtime environment metadata | low | Interpreter/OpenSSL patch versions and platform are absent from the packet, limiting reproduction of strict TLS/opcode behavior. Record these nonsensitive runtime facts. |
| B30 unrelated Story 2.3/deferred edits | false | These appeared concurrently after the clean task baseline and were preserved by both implementation agents. They have no certificate dependency and are excluded from this scope; the workflow diff includes the entire shared tree. |
| E21 raw syscall result transfer | medium | Root verified the same two one-descriptor leaks; group with B21, bad_spec. |
| E22 outer stream cleanup entry | medium | Root interrupted after a successful real MemoryBIO exchange and observed stream.closed=False. The structure predates the feature but is exposed on the new certificate path; include its ownership in the outer native guard, bad_spec. |
| E23 final cleanup replaces primary | medium | Root verified stored KeyboardInterrupt is replaced by final-cleanup SystemExit; group with B23, bad_spec. |
| V21 count test false positive | low | Pre-verified in-memory {0,15} to {0,16} mutant still passes because the mocked loader raises the Refusal-producing error. Add assert_not_called; direct test correction included in re-derivation. |

No finding is deferred. Internal ownership findings require coherent re-derivation; all direct test/evidence corrections are included. The approved frozen intent and closed operational gates remain unchanged.

### Review 3 — review-2 candidate

| Finding | Verdict | Evidence / route |
| --- | --- | --- |
| B31 connector return before stream registration | medium | Parent reran the retained opcode probe: connector returned a real MemoryBIO stream, STORE_ATTR cancellation left it open. Exposed by the newly qualified lifetime; owned acquisition is required. bad_spec; shared with E31. |
| B32 discovery/registration identity gap | medium | Parent reran the probe replacing the discovered memory fd before register; cleanup closed the unrelated replacement. Expected identity must reach adoption. bad_spec. |
| B33 descriptor exhaustion | medium | Isolated parent probe consumed the last fd with memfd_create; listdir failed EMFILE and fd 3 leaked without an owner. Recovery resources must precede allocation. bad_spec. |
| B34 caller exception context | medium | Parent probe called exchange within a handled caller KeyboardInterrupt; remember propagated that older object instead of the new loading interrupt. Bound context traversal to this exchange. bad_spec. |
| B35 two close interruptions | medium | Parent probe made the first two close calls interrupt before release; stream remained open. Cover this supported bounded two-interrupt pattern. bad_spec. |
| B36 non-idempotent close/reuse | medium | Parent probe released the raw fd before bookkeeping, opened a same-number replacement, then observed a retry close it. The trusted stream release protocol needs explicit idempotent ownership and qualification; arbitrary unsupported closers gain no safety claim. bad_spec. |
| B37 exact byte-limit positives | low | Exact-limit positive parser tests are absent, but current <= guards accept those lengths and no incorrect behavior was demonstrated. Reject optional fixture construction; existing independent oversized/no-loader controls remain. |
| B38 repeated complete fd scans | low | Successful allocations are rescanned. No timeout/late-success or everyday fixture failure was shown, and remaining(deadline) rejects late success; reject speculative state/optimization complexity. Exhaustion correctness remains separately accepted as B33. |
| B39 unrelated Story 2.3 revision evidence | false | The qualification README is explicitly dated 2026-10-23 and links exact revision/source receipts; subsequent pointer movement does not change that historical assertion. No current certificate qualification consumes it. |
| B40 log-summary metadata cross-check | low | The checker binds log hashes and duplicated JSON totals without parsing actual unittest summaries. Current logs match 315, but an accidental coherent count error would escape. Direct checker/test correction, patch included in re-derivation. |
| E31 connector result ownership | medium | Same independently reproduced open-stream outcome as B31; preserve its own finding row and resolve with owned acquisition. bad_spec. |
| E32 removed fixture finally | low | Original baseline Native.exchange closed its already-created stream in finally; the current TLS delegation loses that cleanup before connector entry on invalid profile/CA. Direct finally restoration, patch included in re-derivation. |

Verification Gap Reviewer found no gaps and independently ran all 39 focused tests successfully. All six blind probes were rerun by the parent; no findings are deferred. Non-frozen ownership instructions are amended above; lower direct patches are included in this coherent re-derivation. Frozen intent is unchanged.

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

### Review-1 re-derivation verification — 2026-10-23

- Re-derived the four implementation/test sources from the exact initial `sources/` snapshots. The initial packet and snapshots remain byte-preserved; the new corrected-source packet is `native-client-certificate-delivery/review-1/`.
- Persistent, registered descriptor owners now span allocator return/opcode, registration entry/append and owner-constructor returns. A C-level close owner prevents retrying a released raw number. Shared error custody preserves the original loading `KeyboardInterrupt` through later cleanup-transition `SystemExit`; all remaining owned inputs receive cleanup attempts. Direct regressions and independent parent probes pass with zero unreleased descriptors and safe replacement descriptors preserved.
- Real TLS/MemoryBIO regressions add traditional RSA and EC keys, a positive intermediate chain with missing-chain control, isolated 16/17-certificate count checks below the byte limits, byte-exact CRLF/no-terminal-newline loading, and explicit isolated fd 3 sentinel/flag preservation. All 297 initial test names/behaviors remain covered; the seven suites pass **305 tests, zero skips** (29 certificate + 45 boundary + 12 readback + 13 authentication + 42 S3 + 94 common + 70 telemetry).
- The historical spec receipt's SHA-256 remains reproducible from `spec-implementation-checkpoint.md`; this review captures its dispatch spec and final verification checkpoint separately. The original independent parent probe invocation is archived byte-for-byte and labeled as historical; current structured-ownership regressions and their exact suite command provide current reproducibility.
- Three handoffs append the results and remaining prerequisites. Public activation remains disabled; actual credential/authority/runtime, system/target/proof-consumer and recovery qualification remain pending. Frozen intent and parent baseline remain unchanged; **4.27 stays in-progress, 4.1 closed and Timesheets 3.6 open/FAIL**. No staging, commits, push or historical executor replay occurred.


### Review-2 re-derivation verification — 2026-10-24

- Re-derived the four implementation/test files from the complete review-1 snapshots, preserving all 305 prior tests and all 24 registration subcases. The historical packets and checkpoints remain byte-preserved; new evidence is retained in `native-client-certificate-delivery/review-2/`.
- Unique shared allocator identities precede raw memory-file creation. Exact-identity recovery covers memfd/duplicate syscall results interrupted before local assignment, for both certificate/key allocations; inode-aware owners distinguish new allocations reusing old raw numbers and preserve unrelated/reused descriptors. Legacy raw callers keep their prior compatibility contract without gaining this qualification.
- Shared stream/error custody spans the whole exchange and guarded fallback cleanup. Original interruptions survive a second interruption during initial recording, either descriptor-cleanup entry or both stream-cleanup entries; all known owned inputs and the real MemoryBIO peer receive release attempts. These direct tests establish the specified two-interrupt patterns, not immunity to indefinitely repeated asynchronous cancellation.
- Added direct custody-backed duplicate/write/chmod/sealing failure regressions, independent material-size refusals with envelopes within other bounds, an explicit no-loader assertion for certificate-count refusal, and None/empty/length checks for fixture collections. All seven suites pass **315 tests, zero skips** (39 certificate + 45 boundary + 12 readback + 13 authentication + 42 S3 + 94 common + 70 telemetry).
- A new executable content-based receipt checker verifies recorded source/snapshot/log and frozen-spec checkpoint hashes. Its isolated mutation test changes both working source and snapshot coherently and confirms rejection against the receipt binding. The original review-1 checker remains unchanged and is explicitly documented as preparation-HEAD/index dependent. Runtime metadata records Python 3.14.4, exact interpreter identity, OpenSSL 3.5.5 and Linux/release/architecture without hostnames.
- Three handoffs append results and remaining prerequisites. Concurrent unrelated Story 2.3, deferred-ledger and submodule edits remain excluded and preserved. No staging, commits, push or historical executor replay occurred. Frozen intent and parent baseline remain unchanged; actual credential/authority/runtime, system/target/proof-consumer and recovery qualifications remain pending. Public activation stays disabled; **4.27 stays in-progress, 4.1 closed and Timesheets 3.6 open/FAIL**.


### Review-3 re-derivation verification — 2026-10-24

- Re-derived the four implementation/test files from exact review-2 snapshots; all historical packets, checkpoints, handoff prefixes and the parent's six original adverse probe results remain byte-preserved. The new packet is `native-client-certificate-delivery/review-3/`. The original baseline and shared-workspace history are preserved.
- Explicit private owned connectors register stream placeholders before constructor acquisition or retain prepared streams before handoff. Raw callbacks refuse before credential/context access; all repository callers were audited and adapted. Safe partial construction and idempotent C-owned release are required. Direct regressions cover constructor/connector returns, prepared STORE_ATTR cancellation, two inside-close interruptions and completed-close/reused-fd safety.
- Allocation placeholders enter custody before C-owned scan reservation and memory-file creation. Exact expected identity reaches adoption. Isolated child RLIMIT_NOFILE cases cover one-free-fd reservation failure, raw memfd/duplicate last-slot transitions and duplication EMFILE, restoring the child limit and preserving unrelated descriptors. Constructor/reservation/return interruptions also retain ownership. The tested process-local protocol does not claim protection from arbitrary hostile descriptor-table mutation.
- Current-exchange exception custody excludes already handled caller context and retains the original interrupt through secondary recording/cleanup failures. Fixture-owned finally cleanup closes already-created streams on invalid profile/CA refusal. All 315 prior behaviors, 39 focused names and 24 registration subcases remain. The seven suites pass **325 tests, zero skips** (49 certificate + 45 boundary + 12 readback + 13 authentication + 42 S3 + 94 common + 70 telemetry).
- The new content checker additionally parses bound unittest counts/skips/outcomes and rejects isolated coherent metadata drift, while retaining the coherent source/snapshot control. Review-2's checker is unchanged. Historical full spec checkpoints are separately bound from later workflow status/triage edits; frozen intent remains exact. The unsigned receipt provides reproducibility only, with no external authenticity or operational authority. Runtime metadata records exact interpreter/Python/OpenSSL and OS/release/architecture without hostnames.
- Three handoffs append the results and remaining prerequisites. Actual credential/authority/runtime, system/target/proof-consumer integration and recovery remain pending. Unsupported/non-idempotent third-party closers, infinitely repeated interruptions and kernel stalls remain unqualified. Public activation stays disabled; **4.27 stays in-progress, 4.1 closed and Timesheets 3.6 open/FAIL**. Unrelated concurrent Story 2.3/2.4, deferred-ledger, sprint-workflow and submodule edits are preserved and excluded. No staging, commit, push or historical executor replay occurred.
