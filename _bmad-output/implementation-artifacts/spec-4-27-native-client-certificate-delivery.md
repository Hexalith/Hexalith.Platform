---
title: '4.27: Native client-certificate delivery'
type: 'feature'
epic: 4
story: 27
created: '2026-10-23'
status: 'done'
baseline_commit: '693390a790d95a9bad9b1bd9cc0f0f3052f3fbef'
route: 'dispatch'
review_loop_iteration: 5
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
- [x] `recovery_execution_boundary.py` — add bounded certificate parsing, sealed descriptor installation and header selection; retain mandatory connector injection and existing guards.
- [x] `test_native_client_certificate.py` (new), `_execution_boundary_fixtures.py`, `test_recovery_execution_boundary.py` — cover every matrix row using static synthetic fixtures, real `SSLContext` loading and socket-free `MemoryBIO` peer verification; include missing/wrong-key controls, sealing, cancellation, descriptor preservation and source/target composition.
- [x] `RECOVERY-EXECUTION-BOUNDARY.md`, `eng/cluster-management/NEXT-OPERATOR-ACTION.md`, `_bmad-output/implementation-artifacts/4-27-retire-kubesphere-without-changing-workload-data.md` — append results and remaining prerequisites.
- [x] `_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/` (new) — record commands, results, matrix coverage and source hashes separately from historical evidence.

**Acceptance Criteria:**
- Given distinct credentials, when all four plans compose, then native exchanges preserve bound audiences, client identities and semantic guards.
- Given a no-op/wrong-certificate loader, when handshake tests run, then peer verification detects incorrect authentication.
- Given passing fixtures, when handoffs change, then credential/authority/runtime qualification, system/target/proof-consumer integration and actual recovery remain pending; gates stay closed.

## Implementation Notes

No intent gaps or irreversible effects; local code/tests/docs only. Clean starting tree on `main`, HEAD `693390a790d95a9bad9b1bd9cc0f0f3052f3fbef`. Capture this spec's baseline at implementation; preserve the parent baseline.

Implementation dispatch coordination: the parent session already rendered and owns the bmad-build workflow. This subagent task is its Step 3 handoff; implement from this file without recursively rendering or re-entering that workflow.

## Spec Change Log

### Review 5 — exchange entry, current-error provenance and complete evidence checks

Re-derive from exact `native-client-certificate-delivery/review-4/sources/`, preserving that complete 331-test candidate and every earlier packet. The four implementation/test files are returned to the original baseline before dispatch. KEEP all 331 positive behaviors, all 55 focused test names, all 24 registration subcases, every prior ownership/EMFILE/caller-history/cleanup regression, original-byte PEM and real TLS/CA/wire/composition controls, frozen intent, historical prefixes and closed operational gates. Adapt structural injection selectors only while preserving each actual reached transition and assertion.

The parent independently reran the seven suites (331/0), receipt/parser controls and original sweeps (299 line, 2,309 opcode, 81 secondary). Those passing results do not cover the newly reproduced defects. Preserve `/tmp/bmad427-blind-probes.py`, `/tmp/bmad427-parent-guard-entry.py` and `/tmp/bmad427-independent-vhepcj6t/` as adverse/pre-correction material in a separate new `review-5/` packet. Do not relabel prior candidate success as current qualification.

- Protect the earliest exchange-body transitions and lifetime-guard invocation for an already owned prepared stream. Parent C-owned probes interrupt placeholder allocation, the guard-call line and guard function entry; all currently leave the stream/file open. Retain independent cleanup and exact primary propagation even if metadata is unavailable or the helper never enters. Add direct no-dispatch, exact-descriptor and C-owned release regressions for each transition, and include a prepared-stream entry sweep/control alongside acquisition-created peers. The tested guarantee remains bounded; terminal interruptions after completed release remain separately classified.
- Select the current exchange's primary failure independently of stale exception-object history. A saved/reused loading KeyboardInterrupt whose pre-existing `__context__` contains an older handled KeyboardInterrupt currently propagates the older object when no handler is active at exchange entry. Track provenance within this exchange rather than treating the oldest historical context as current. Preserve existing secondary-interrupt and caller-handler tests; add this saved-context regression with exact object assertions and cleanup checks.
- Create a new receipt checker in `review-5/`; leave every historical checker/receipt unchanged. Parse all three bound cancellation-sweep results, require no findings/unraisable exceptions and reconcile target totals, terminal counts and verified release/propagation flags with receipt metadata. Add isolated hash-consistent adverse sweep and count/terminal mismatch controls. Require independently defined complete dependency-source and supporting-packet inventories; add coherent empty/omitted inventory controls and prove omitted dependencies cannot silently drift. Compare verbose executed test identities against each required suite's actual source test inventory; reject same-count plain-OK unrelated or repeated-test transcripts with coherently updated hashes and unchanged passing claims. These are reproducibility checks, not authenticity or operational authority.
- Add a socket-free real mutual TLS positive with both client/server contexts capped at TLS 1.2, asserting the negotiated version, original client identity and native header/body behavior. Preserve default negotiated-protocol tests.

Run all seven required suites after corrections, zero skips, plus updated finite cancellation controls and executable receipt self-tests. Bind new sources/snapshots/logs/runtime/checkpoints and current full HEAD separately from the original baseline. Append verified outcomes to the three handoffs. No actual credentials/configuration/endpoints, operational helpers, recovery allocation/signing/upload/recovery, activation, staging/commit/push or unrelated workspace edits. Original 4.27/sprint stay in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.

### Review 4 — protected lifetime entry and executable qualification controls

Re-derive from exact `native-client-certificate-delivery/review-3/sources/`, preserving that complete 325-test candidate and every earlier packet. The four source/test files are returned to the original baseline. KEEP all 325 positive behaviors and 49 focused test names, 24 registration subcases, owned connector construction and prepared handoffs, exact expected-inode adoption, pre-acquisition C-owned scan reservations, all isolated EMFILE/constructor-return tests, scoped caller-context handling, bounded idempotent stream cleanup, byte/count bounds, exact real TLS peer/wire/CA checks, original frozen intent and closed operational gates. Adapt structural injection selectors when code changes, but each preserved transition must still be reached and asserted, never replaced by an early refusal.

Parent verified both new resource leaks and the remaining cancellation behavior. New adversarial scripts/results are `/tmp/bmad427-single-trace.py`, `/tmp/bmad427-single-trace.json`, `/tmp/bmad427-line748.py`, `/tmp/bmad427-double-trace.py`, `/tmp/bmad427-double-trace.json`, `/tmp/bmad427-opcode-trace.py`, `/tmp/bmad427-opcode-trace.json`. Retain these original adverse records separately in new `review-4/` evidence; current regressions and updated sweeps provide new results, not relabeled historical successes.

- Establish custody within the native lifetime guard. A prepared owned stream must close if `_DescriptorCustody.__init__` is interrupted at entry, during initialization or on return before the caller stores the result. Preserve safe ownership of the already-known prepared stream while custody metadata is partial or unassigned; no request may be dispatched. Add direct partial/return regressions with a C-owned synthetic descriptor where applicable.
- Remove unprotected cleanup and handler-entry transitions exposed by Python 3.14's exception table. A single cancellation at the initial stream-cleanup `try:` currently leaks a successfully authenticated peer; secondary cancellation at the fallback `except` header / `try:` currently bypasses active error custody. Restructure the lifetime coherently instead of adding another unprotected outer level. Verify exact primary interruption through acquisition/active cleanup, release of every known stream/input, no retry of effects and no late success. Qualify finite tested cancellation patterns; a new interrupt during terminal propagation after all cleanup is complete is a separate external cancellation, not a promised signal-atomic return boundary.
- Replace the cleanup identity `any(...)` generator with an explicit bounded loop. Parent reproduced KeyboardInterrupt becoming unraisable in generator-finalization opcodes while exchange returned success. Avoid that generator destructor path; check current success/cancellation sweeps for resource leaks and swallowed cancellations.
- Accept the demonstrated TLS-valid compact-base64 PEM and leading blank-line forms without normalizing installed bytes. Keep strict envelope fields, supported unencrypted labels, separate material/count/envelope limits, noninteractive loading and actual SSLContext key/certificate validation. Avoid ambiguous/unbounded regex backtracking when loosening whitespace/line wrapping. Add positive real-loader/peer and original-byte checks; retain malformed/encrypted/mismatch negative controls.
- Complete the direct qualification controls: combine a lost raw syscall result with a second interruption during scan/registration, asserting both injections and exact descriptor preservation; fail midway through a trusted owned stream constructor after a C-owned resource is acquired and prove partial cleanup. Retain all existing simpler transitions.
- Enforce the private acquire postconditions before native dispatch: its return must be None and the resulting stream must satisfy the owned-stream protocol. Parent demonstrated a wrapped acquire returning an object, or assigning an unsupported stream, currently reaches success. Refuse these protocol violations and retain ownership cleanup; add direct no-dispatch tests. This is no claim of safety for arbitrary hostile/unowned third-party resources.
- Extend the new receipt checker to require the seven expected named suites and their bound command mapping (counts remain data), reject expected-failure/non-passing summaries, and exercise actual hash-consistent FAILED, skipped and expected-failure transcript variants with unchanged success claims. Add a coherent suite-omission negative control. The prior self-test's metadata-only mutations do not catch a parser hardcoded to OK/zero skips. Preserve review-3 checker/receipts and put corrected checker and bound results only in review-4.

Run the required seven-suite verification after corrections, zero skips. Preserve all historical packets, source snapshots, prefixes, signing/recovery originals and unrelated workspace work. Record current full HEAD separately from the original baseline. No credential/configuration/endpoints, recovery allocation/signing/upload/recovery, activation, operational helper replay or Git staging/commit/push. Original 4.27/sprint remain in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.

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

### Review 4 — review-3 candidate

| Finding | Verdict | Evidence / route |
| --- | --- | --- |
| B41 unprotected initial cleanup try | medium | Parent reran the line-748 reproducer: a single KeyboardInterrupt leaves a successfully authenticated MemoryStream open. Python exception-table coverage needs coherent lifetime repair. bad_spec. |
| B42 fallback-entry and terminal cancellation | medium | Parent reran all 72 secondary targets: headers at 759/760 replace the current primary before fallback handling. Protect active cleanup/error custody, bad_spec. Terminal sites 783/784 occur after every resource is closed; signal atomicity after completed cleanup is explicitly outside the finite qualification. |
| B43 compact / leading-blank PEM | medium | Parent loaded both exact synthetic cert/key forms with real SSLContext successfully, while the current boundary refused. Direct bounded grammar/original-byte correction, patch included in re-derivation. |
| B44 generator-finalization cancellation | medium | Parent separately hit recover genexpr opcodes 108/110: KeyboardInterrupt went to sys.unraisablehook and exchange returned success. Replace the short-circuit generator with a direct loop; patch included. |
| B45 combined raw-result / recovery cancellation | low | Existing tests cover raw-result and registration faults separately; the three-spare-scan rationale lacks its combined direct control. Add the exact two-injection regression as direct qualification work. |
| B46 partially acquired stream constructor | low | Existing transitions stop before constructor acquisition or on normal return. Direct C-owned mid-construction failure coverage pins the newly documented close-on-partial contract; patch included. |
| B47 acquire postconditions | low | Parent exercised both wrapped return-object and unsupported-stream assignment; each reached status 200 despite the private protocol. Direct return/type postcondition checks plus no-dispatch tests, patch included. |
| B48 required suite inventory | low | Parent coherently removed telemetry-postgres and adjusted hashes/totals in an isolated copy; checker accepted the incomplete required run. Add the fixed seven-suite name/command mapping and omission control; patch included. |
| B49 expected-failure outcome details | low | Parent parser returned tests=1/skips=0/outcome=OK for an expected-failure transcript, counting a non-pass as passed. Reject non-passing details and test a bound transcript variant; patch included. |
| B50 child interpreter selection | low | Recorded commands, parent interpreter/hash and isolated children all use the explicitly qualified /usr/bin/python3.14. Mixed runtimes require departing from that pinned invocation. Reject broader cross-interpreter support/qualification; retain the measured command. |
| E41 custody constructor before guard | medium | Parent injected __init__ return with a prepared stream: original interrupt propagated but stream remained open. Entry/partial/return ownership repair is required, bad_spec. |
| V41 failed/skipped transcript controls | low | Pre-verified reviewer mutation hardcoding parsed OK/zero skips survived self-test and accepted adverse copied transcripts. Add actual FAILED/skipped hash-consistent log controls, direct patch included. |

All 12 findings have individual verdicts. The verification reviewer otherwise found the native paths covered; parent also reproduced B43/B44/B47/B48/B49 using inert/local fixture data. No finding is deferred. Ownership defects require the non-frozen coherent re-derivation above; frozen intent remains unchanged and all three candidate packets remain historical evidence.

### Review 5 — review-4 candidate

| Finding | Verdict | Evidence / route |
| --- | --- | --- |
| B51 stale saved exception context | medium | Parent reran the inert probe: an existing older context on the current loading KeyboardInterrupt replaces it outside an active caller handler (`propagatedOld=True`, `propagatedCurrent=False`). Current-exchange error provenance needs coherent repair; bad_spec. |
| B52 exchange body before guard | medium | Parent C-owned probe interrupts placeholder allocation and guard invocation; exact cancellation propagates while both prepared stream and file remain open. Protect initial exchange ownership; bad_spec, shared root with E51. |
| B53 unchecked adverse sweep outcome | medium | Parent isolated-copy probe inserted a descriptor-preservation failure and coherently rebound its log hashes; verifier still accepted zero-findings metadata. Parse/reconcile actual sweep outcomes in a new checker; bad_spec. |
| B54 dependency inventory omission | low | Parent reproduced acceptance with empty sourceSha256; imported dependencies outside the four snapshots then lack their promised bindings. Direct fixed-inventory/checker controls included in re-derivation. |
| B55 packet inventory omission | low | Parent reproduced acceptance with empty packetFileSha256; supporting checker/sweep/runtime files lose their advertised checks. Direct complete-inventory correction included in re-derivation. |
| B56 same-count unrelated test identities | low | unittest_summary checks totals/plain OK, with no executed-name comparison; replacing only verbose test labels preserves acceptance. Direct suite-identity comparison and a same-count negative control included in re-derivation. |
| B57 sweep invocation occurrences | low | Sweep intentionally records unique function/location targets, and direct tests independently reach both input allocations, all 24 registration subcases and later cleanup passes. No additional incorrect later-invocation behavior was demonstrated. Reject expanded occurrence-wide sweep machinery; add the specifically demonstrated prepared-entry control under B52. |
| B58 TLS 1.2 positive | low | Both peers permit TLS 1.2 but no test caps negotiation there; the minimum-protocol authentication path lacks a positive control. Include one direct real-peer qualification test in re-derivation. |
| B59 composition disposal test shape | low | The cited test now exercises actual startup before hosted-service resolution, so its single disposal assertion no longer exercises a resolved owner. The same file separately marks failure then explicitly disposes PlatformCompositionWorkspace, and PlatformRunIsolationReviewTests covers registered owned workspace disposal. No runtime failure is shown; reject an additional unrelated integration fixture as disproportionate to this narrow assertion gap. |
| B60 macOS containment claim | maybe-false | The one-line macOS comparison change belongs to concurrent committed composition work, not certificate delivery. No failing macOS path was supplied and this Linux run cannot settle Path.GetRelativePath behavior on the proposed filesystem configurations. A native macOS reproduction is needed; reject this unverified low-impact documentation/implementation suggestion. |
| E51 guard helper entry | medium | Parent injects at _guard_native call entry with a prepared io.FileIO-backed stream; the body/finally never runs, leaving its exact descriptor open. Same ownership defect as B52; bad_spec. |

Verification Gap Reviewer found no gaps and independently passed all 55 focused cases and the receipt self-test. Every reported finding has its own verdict; nothing is deferred. Non-frozen implementation/evidence instructions are amended above and frozen intent stays exact.

### Review 6 — corrected review-5 candidate

| Finding | Verdict | Evidence / route |
| --- | --- | --- |
| B61 invalid-protocol generator finalization | medium | Parent reran the inert invalid-stream probe: GeneratorExit finalization receives KeyboardInterrupt, sys.unraisablehook records it and Refusal replaces the exact interrupt. Replace the existing bounded method-check generator with an equivalent explicit loop; same postconditions/state and no public surface, so direct patch with a regression. |
| B62 destructive replay instructions | low | README directly invokes run-seven-suites.py, whose own-directory log/result writes change timing bytes and invalidate the receipt. Correct the replay instructions to preserve bound evidence; direct documentation patch. |
| B63 omitted verification command records | low | Parent reproduced verificationCommands=[] being accepted while the recorded five commands disappear. Add the fixed existing command/log inventory and coherent omission control; direct metadata correction. |
| B64 omitted historical preservation inventory | low | Parent replaced the preservation inventory with an empty object, rebound its hash and corrupted a review-4 source; verifier accepted. Pin the complete preserved inventory independently and add this exact omission/corruption control; direct receipt correction. |
| B65 coherent sweep target reduction | low | Parent reproduced acceptance after coherently changing every recorded count and bound result to a smaller claimed run. The actual independent run reproduced all 307/2369/93/32 targets with no omissions. Proving arbitrary coherently rewritten execution histories would need another independently pinned trace enumeration or execution layer; reject that additional machinery for this rare unsigned-receipt scenario. Existing adverse-result and mismatched-count controls remain required. |
| B66 descriptor status flag snapshot | low | descriptors() includes fstat and F_GETFD but omits F_GETFL, so broad preservation assertions miss status-flag drift. Include the existing status flags in that snapshot; direct test-helper correction. |
| B67 peer ownership before readiness assertion | low | StartAsync normally attempts teardown on failed peer startup, but its returned RunId can still identify incomplete cleanup. The live test asserts readiness before adding that identity, losing its final retry when teardown is incomplete. Pre-existing concurrent Story 2.5 fixture; defer. |
| B68 independent final lifecycle cleanup | medium | The current live test's finally loop awaits down and asserts emptiness without per-owner error collection; one failure bypasses every later owner. Pre-existing concurrent Story 2.5 fixture; defer with E61. |
| B69 untyped peer container selection | medium | The scanner returns all containers labelled for a run; the live fixture calls redis-cli on the first ID without checking resource identity. The provisioning suite already selects resourceId=redis explicitly. Pre-existing concurrent Story 2.5 proof can fail on another container; defer. |
| B70 extra live cancellation variants | low | The scenario list contains four outcomes and no cancellation variants; no cancellation defect was demonstrated. Reject expanding this unrelated live fixture for an optional coverage suggestion. |
| E61 final cleanup abort | medium | The cited actual loop has no per-owner catch; an exception or leftover assertion skips later owners. Same pre-existing Story 2.5 defect as B68; defer together after preserving this separate verdict. |

Verification Gap Reviewer found no gaps and independently passed all 59 focused cases and the review-5 receipt self-test. The parent independently passed 335/0, all six frozen matrix rows, all receipt/parser controls and the full finite sweeps with unchanged source hashes. Scoped survivors are direct corrections to demonstrated existing states, so no re-derivation or review-loop increment is required. Preserve the entire 335-test candidate before patches; current corrected-source verification must be bound separately. Frozen intent and operational gates remain unchanged.

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


### Review-4 re-derivation verification — 2026-10-24

- Re-derived the four implementation/test sources from exact review-3 snapshots. All 107 historical packet files, all 325 prior behaviors, all 49 focused names, all 24 registration subcases, frozen intent and handoff prefixes remain preserved. The original adverse single/double/opcode scripts/results are archived unchanged in `native-client-certificate-delivery/review-4/`; current executable sweeps are separate. Original baseline `693390a790d95a9bad9b1bd9cc0f0f3052f3fbef` and preparation HEAD `56c783bcd005e3ce0c728dc72e440a696a91d034` remain separately recorded.
- One lifetime guard now covers custody constructor entry/partial/return and three independent cleanup-invocation passes. Exact current-primary cancellation survives the tested acquisition/active-cleanup transitions, every known prepared/current stream and input is released, and effects are never retried. Explicit loops avoid cleanup-generator finalization. Direct controls combine lost raw results with second scan/registration cancellation, fail trusted construction after C-owned acquisition, and reject non-None acquire results or unsupported streams before dispatch. Caller-handled cancellation/ordinary exceptions neither replace new acquisition/loading interruptions nor poison successful exchanges.
- Linear bounded PEM framing accepts compact-base64 and leading-blank forms without normalizing installed bytes; real loader/exact-peer positives and malformed/encrypted/mismatch, byte/count, CA/wire, source/target composition, descriptor preservation and deadline controls remain. The seven suites pass **331 tests, zero skips** (55 certificate + 45 boundary + 12 readback + 13 authentication + 42 S3 + 94 common + 70 telemetry).
- Current sweeps cover **299 single-line, 2,309 opcode and 81 secondary-line targets**, with no leaks, swallowed cancellations or unraisable exceptions. Five terminal secondary targets are separately recorded only after peer/descriptor release is proved; a new interrupt after completed cleanup is a separate external cancellation, with no signal-atomic return promise. Unsupported/non-idempotent third-party closers, arbitrary hostile descriptor mutation, infinitely repeated interruptions and kernel stalls remain outside qualification.
- The new content checker requires all seven suite names and pinned command mapping, parses actual bound summaries and rejects non-passing details. Its isolated controls run real same-count unittest FAILED/skipped/expected-failure/unexpected-success transcripts, coherently bind their bytes/hashes while preserving original success claims, and require rejection; coherent suite omission, command drift, metadata drift and source/snapshot drift also reject. Review-3 and earlier checkers remain unchanged. Runtime/interpreter/OpenSSL and OS/release/architecture omit hostnames; full spec checkpoints remain separate from later workflow status edits. The unsigned receipt confers no authenticity or authority.
- Three handoffs append results and remaining prerequisites. Actual credential/authority/runtime, system/target/proof-consumer integration and recovery remain pending. Public activation stays disabled; **4.27 stays in-progress, 4.1 closed and Timesheets 3.6 open/FAIL**. Unrelated concurrent work is preserved and excluded. This offline pass accessed no actual credential/configuration/endpoints and performed no allocation/signing/upload/recovery, staging/commit/push or operational helper replay.

Shared-workspace provenance: preparation HEAD remains
`56c783bcd005e3ce0c728dc72e440a696a91d034`; a concurrent operation recorded
`b2ff52312b01db65aef3e8b60fb56681ae97d70a` (`fix: story 4.27`) during this pass.
That history is preserved and the observed current HEAD is separately bound in
`review-4/current-head.txt`; this pass performed no Git staging/commit/push.


### Review-5 re-derivation verification — 2026-10-24

- Re-derived the four implementation/test sources from exact review-4 snapshots, preserving every prior packet, all 331 prior behaviors, all 55 focused names, all 24 registration subcases, historical handoff prefixes and frozen intent. Original parent probes and the complete independent pre-correction run remain byte-preserved in the new `native-client-certificate-delivery/review-5/` packet.
- The first exchange-body transition now begins the protected lifetime. Prepared C-owned stream release survives placeholder allocation, guard invocation and guard function-entry interruption even without metadata. Current-call traceback provenance excludes stale saved exception context and recognizes the same active caller exception re-raised by loading. Direct regressions assert exact primary objects, no credential/context/native dispatch where applicable, descriptor identity/flag preservation and C-owned release.
- Socket-free real mutual TLS capped at TLS 1.2 asserts both negotiated versions, original client identity and exact native PATCH headers/body. Default protocol negotiation and all earlier PEM/CA/wire/composition/deadline/cleanup controls remain. The seven required suites pass **335 tests, zero skips** (59 certificate plus 45 boundary, 12 readback, 13 authentication, 42 S3, 94 common and 70 telemetry).
- Updated finite controls pass **307 line, 2,369 opcode, 93 secondary-line and 32 prepared-entry targets**, with no findings or unraisable exceptions. One final cleanup-invocation target is classified only after descriptor/peer release is verified; a new cancellation after completed release remains an external cancellation, with no signal-atomic return promise. Unsupported closers, arbitrary hostile descriptor mutation, indefinitely repeated interruptions, kernel stalls and actual connector/runtime remain unqualified.
- The new receipt checker reconciles actual bound sweep outcomes/counts/terminal release/propagation flags, requires independent complete dependency-source/supporting-packet inventories and compares verbose executed identities with all seven actual source test inventories. Executable isolated controls reject hash-consistent adverse sweeps, count/terminal mismatches, empty/omitted inventories, omitted dependency drift and same-count plain-OK unrelated/repeated transcripts. Actual FAILED/skipped/expected-failure/unexpected-success controls retain required test identities, preserving an independent hardcoded-parser mutation control. Historical checkers/receipts remain unchanged. Full spec checkpoints, current full HEAD, source/snapshot/log and nonsensitive runtime bytes are separately bound. This unsigned packet confers reproducibility only, not authenticity or authority.
- Three handoffs append verified outcomes and remaining prerequisites. Actual credential/authority/runtime, system/target/proof-consumer integration and recovery remain pending. Public activation stays disabled; **4.27 stays in-progress, 4.1 closed and Timesheets 3.6 open/FAIL**. Unrelated concurrent work is preserved and excluded. No actual credentials/configuration/endpoints, operational helper replay, recovery allocation/signing/upload/recovery, staging/commit/push or infrastructure mutation occurred.


### Final direct-patch verification — 2026-10-24

- All scoped review survivors are corrected. Independent verification passes **336 tests, zero skips**, all six frozen matrix rows, all finite sweeps (307 line / 2,341 opcode / 93 secondary / 32 prepared-entry), receipt self-tests and both parser/invalid-protocol mutation controls. The invalid-protocol mutation is an intentionally adverse run and is not counted among passing qualification suites.
- The exact 335-test candidate, 215 pinned historical files, original adverse probes, every prior test name/behavior and frozen intent are preserved. Full final command transcripts/source hashes and review outcomes are retained in [independent final evidence](evidence/epic-4/4-27/native-client-certificate-final-verification-20261024/verification.json).
- Three pre-existing concurrent Story 2.5 lifecycle findings are deferred; none blocks this native-certificate scope. The parent 4.27 story/sprint remains in-progress, 4.1 closed and Timesheets 3.6 open/FAIL. Actual operational prerequisites remain pending and public activation stays disabled. The scoped no-staging/commit/push instruction remains in force.
