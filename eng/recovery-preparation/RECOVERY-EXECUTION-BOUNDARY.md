# Authenticated recovery execution boundary

The three new drivers implement the execution interfaces around the unchanged
[common adapter](RECOVERY-ADAPTERS.md). They have been exercised with synthetic
credentials, inert provider/native/system transports, real local fixture
processes and a pre-existing public OpenSSH signature. They establish no actual
recovery, provider, authority, target or operational qualification. Story 4.27
stays **in-progress**, 4.1 stays **closed**, and Timesheets 3.6 stays **open/FAIL**.
The revision-4 preparation packet and signed originals remain separate evidence.

## Interfaces and ordering

`RecoveryExecutionBoundary.exchange(Request, absolute_deadline)` accepts the
next exact request from `build_requests(binding)`. It permits the authentication
request's one specified materialization from the independently checked policy
and signature buffers. Every other field, including tool, endpoint, argv, body,
principal, environment, response cap and expected semantic check must match.
Mutable byte buffers refuse, and dispatch uses the boundary-owned immutable
expected request after matching so later caller changes cannot alter the wire.
Each exchange atomically claims the instance for its entire lifetime. Concurrent
or reentrant attempts refuse and poison the instance, so the active owner cannot
commit a second index advancement. Completed authentication is tracked separately
from the numeric request index and is required before every dependent dispatch.
A failure, including every escaping `BaseException`, poisons the instance; it
cannot retry the same request or skip to a later request. Ordinary failures become
one fixed refusal. Interrupts such as `KeyboardInterrupt` propagate after owned
cleanup. The unchanged runner retains create-only intents/results and writes its
sanitized stopped receipt for ordinary exceptions. An interrupt leaves existing
pending intents intact and produces no result or complete receipt for that call.
Partial effects remain uncertain; no compensating operation follows.

The first two requests use `ImmutableReadbackDriver`; the third uses
`PolicyAuthenticationDriver`. Native and system requests follow authentication.
The boundary also calls the unchanged common semantic checks, including exact
resource identities, isolated targets, fresh drains, fence observations and
owned-target cleanup guards. Tests compose capture, restore, resume and cleanup
with distinct source/target clusters and independent reader/operator principals.
Every successful fixture result retains null operational/recovery acceptance.

Only successful semantic checks may retain clean-drain or creation-fence
observations. Immediately before guarded inventory, replica PATCH and each owned
DELETE, the boundary rechecks the exact relevant observation after its own
credential/custody work. It enforces the required inventory/first-delete/namespace
fence adjacency. Future observations or ages beyond 30 seconds refuse. Delegated
deadlines are clamped to the caller's absolute limit, the request limit, policy
expiry and remaining prerequisite validity. Final dependency scans and all owned
driver cleanup precede the last deadline/window/semantic checks and state commit.
These checks apply to direct ordered calls as well as runner composition.

The caller supplies immutable `Tool` byte/length/path bindings for every source
and runtime dependency, explicitly acquired in-memory credentials and injected
transports. There is no configuration, credential or discovery loader. Use the
checked `load_source` function to compile exact source snapshots; sibling
bootstraps use stable no-follow source descriptors and ignore pyc caches.
Dispatch compares the loaded source fingerprints with the dependency inventory
and checks the inventoried files again. Symlinks, multiple file links, unsafe
ownership/modes, unstable reads, changed bytes and absent dependencies refuse.
The common adapter and all three new source modules must be inventoried.
No historical retirement/recovery executor is imported or selected.

## Credential custody and native TLS

A `CredentialBinding` pins principal, audience and hashes of original identity,
custody and secret bytes. `Credential.descriptor(binding)` checks exact records
and all hashes; a principal label or authentication boolean supplies no evidence.
Native audiences are exact cluster UIDs. An artifact reader's audience is the
exact artifact store. Operator/source, operator/target and independent validator
credentials are selected separately; writer credentials never replace a reader.
These records are caller-supplied custody bindings, not an implementation of IAM
identity qualification or acquisition. Those actual qualifications remain open.

`NativeBinding` pins the full `Cluster`, original public CA bytes and exact
credential binding. The current native wire profile delivers explicit bearer
bytes. Client-certificate delivery remains a separate integration requirement
for source profiles that require it. `NativeTLS` builds a hostname-checking, CERT_REQUIRED,
strict-validation context from only the pinned CA. The dormant `_connect`
mechanics use a numeric IPv4 socket, the bound port and IP certificate validation;
there is no DNS, proxy, system CA loading, redirect, retry or discovery. Its
`exchange` requires an explicitly injected inert connector. The default refuses
before connecting. The native socket connector is never selected automatically.
Tests never connect sockets, including to loopback or the documentation-range
fixture addresses.

The HTTP exchange sends the exact native method/path/body/content type, partial
writes included. A single monotonic deadline covers each write, TLS operation,
header byte and body read. Headers and bodies are capped. Ambiguous lengths,
duplicated headers, redirects, transfer/content encodings, premature EOF,
oversized or dripping responses refuse. This single-request, connection-closing
profile requires bounded EOF after the declared body: unread surplus bytes or a
peer that keeps the connection open refuse. The owner closes its stream on success
or failure and checks the deadline after close before returning success. Timeout after a PATCH/DELETE has been sent leaves its remote effect
unknown; no local cancellation establishes that a server-side write was undone.

## Bounded process protocol

`BoundedProcess.run` executes the bound bytes from a sealed executable memfd.
The caller supplies exact argv; there is no shell, PATH lookup or fallback.
The working directory is `/`, the entire environment is `LANG=C.UTF-8`, stdin is
the original request body, stdout is the bounded protocol response and stderr
is counted then discarded. Credentials occupy a sealed descriptor at fd 3;
no secret is written to a file, argv, environment, receipt or diagnostic.
Every executable and input memfd is duplicated above fd 3 before use; temporary
low allocations are closed. The parent reserves credential destination fd 3 only
when it was originally closed and releases that reservation afterward. An occupied
parent fd 3 is preserved. Originally closed stdin/stdout/stderr remain closed in
the parent. Credential remapping therefore cannot overwrite the executable or
another input, even in a host with closed standard descriptors.

For system requests fd 3 contains an exact envelope with `requestSha256`,
`name`, `path`, `method` and `credentials`. Each credential contains principal,
audience, identity/custody hashes and base64 secret bytes. The routing fields
identify the stage without changing the existing argv or original stdin body.
The future system implementation must check the request hash and exact stage.
Both the real sealed-fd fixture process and inert all-operation protocol compare
the entire envelope, both distinct cluster audiences, principals, identity and
custody hashes, and original synthetic secret bytes. Empty credential lists and
altered routing/credential records fail these checks.
Only independently inventoried ELF executables or fixture scripts whose shebang
is exactly `#!/usr/bin/python3.14 -ISB` are accepted. Such scripts require the
separately pinned interpreter. These flags isolate imports, suppress site setup
and prevent bytecode writes. A complete qualified runtime dependency inventory
must be supplied separately for any real tool; no dependency discovery occurs.

The nonblocking selector drains stdout/stderr while sending bounded stdin under
one deadline. Overflow, stalled stdin, drips, failed writes, retained descendant
pipes and nonzero tool exits cannot produce successful protocol acceptance.
The process object is retained before constructor initialization, so launch-time
cancellation still leaves the boundary with its owned child's handles and PID.
Partially initialized objects are checked before signalling or closing resources.
Exit is observed with `waitid(..., WNOWAIT)` without reaping the owned leader.
The retained leader protects the process-group identifier during the final
custody scan. Group signalling completes before the first reap; no signal can
follow release of that ownership. Every owned stream/descriptor close and bounded
reap is attempted independently, even if another signal/wait/close fails. A failed
wrapper receives a bounded direct reap attempt, and failed owned closes receive
best-effort release attempts. Cleanup diagnostics become the same fixed boundary
error; cancellation keeps its original interrupt semantics. Final elapsed time is
checked after cleanup before any process success is returned.

The minimal fd mapping uses `preexec_fn` only for `dup2` and is qualified here in a
single-thread fixture harness. A real multithread host/runtime must separately
qualify its process-launch and cancellation mechanism. Process creation, regular
file reads and kernel-uninterruptible tasks cannot be made arbitrarily
preemptible by these Python interfaces; a SIGKILL attempt cannot prove immediate
kernel reclamation or reverse externally committed effects. The cleanup wait
is capped at 0.5 seconds and never targets unrelated process groups. Tools must
be separately qualified as non-daemonizing: descendants that detach with
`setsid()` can escape the owned process group. Whole-tree sandboxing and detached
descendant enforcement are not supplied by this boundary. Actual tool/runtime
qualification remains closed.

## Independent immutable readback

`ProviderBinding` fixes store, endpoint, CA, validator credential and distinct
writer principal/identity/custody/secret hashes. `ImmutableProvider.open_exact(artifact,
credential, deadline)` returns an owned handle with raw `metadata: bytes`,
`read(maximum, deadline)` and nonblocking `close()`. Every blocking provider call
must enforce the supplied absolute deadline internally; post-return checks
alone cannot interrupt an arbitrary injected Python function. Standalone readback
and authentication calls locally cap the caller deadline to the original request
budget and policy expiry, rechecking the policy window before provider/verifier
work. A concrete
provider transport is deliberately unresolved and the base implementation
refuses. No credential or transport is substituted when it is absent.

The driver compares exact raw metadata for store/key/version, length, hash,
`AES256`, `COMPLIANCE`, retention, provider endpoint/CA and reader principal/
identity/custody. Unknown/missing/duplicate fields refuse. It independently
counts and hashes the bytes with one-byte overflow/EOF checking; metadata
checksums or success booleans cannot replace this read. Retention must match
the exact artifact version and cover the bound retained decisions. The driver
closes the handle before returning the existing typed `Response`; short,
changed, overflowing, late or uncertain evidence never becomes recovery input.

## Policy authentication

`PolicyAuthenticationDriver` binds original public allowed-signers bytes,
trust SHA-256, exact signer identity and namespace `hexalith-recovery`. Its
narrow trust grammar permits one Ed25519 public key and one exact namespace;
patterns, multiple signers, additional options and changed trust refuse. The
SHA-256 of the SSH public-key wire bytes must match `binding.signer_sha256`.

The driver verifies exact policy/signature lengths and hashes, seals the trust
and signature buffers, then invokes only the bound verifier with:

```text
<bound-verifier> -Y verify -f <sealed-trust-fd> -I <pinned-signer> -n hexalith-recovery -s <sealed-signature-fd>
```

Original policy bytes are verifier stdin. They are never reserialized for
verification. Only an integer zero verifier exit permits subsequent exact
schema/scope, dates, retained-decision and current-window checks, then the
existing authentication response. `schemaVersion` must have exact Python `int`
type and value 1; a coherently rebound JSON boolean cannot authenticate. The last
runtime scan is followed by fresh absolute-time and policy-window checks. A caller's `authenticated` boolean is
rejected. The verifier receives no signing key and performs no signing.

## Safe fixture verification

Run from the repository root:

```bash
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_recovery_execution_boundary.py
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_immutable_readback_driver.py
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_policy_authentication_driver.py
```

All test credentials and provider observations are invented. Policy composition
uses an explicitly inert verifier to avoid manufacturing a new signed policy.
A separate real `BoundedProcess` test verifies the existing OpenSSH public
Ed25519 regression vector under its original `unittest` namespace, then refuses
changed message/signature/namespace/trust. It loads only the public key,
signature and message embedded in the test; it never generates a key or signs.
The three original public blob identifiers are recorded next to the fixture.
This tests actual positive cryptographic invocation/descriptor mechanics while
keeping the policy-composition result explicitly unqualified.

The [first-candidate implementation receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261023-recovery-execution-boundary/verification.json)
remains historical fixture evidence. The [separate review-1 receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261023-recovery-execution-boundary/review-1/verification.json)
records the preceding candidate's source and test logs as historical fixture
evidence. Current source requires a separate final verification receipt; neither
historical receipt modifies or requalifies revision 4. The old common and telemetry suites
are run unchanged (94 + 70 cases).

The corrected suites add deterministic barrier races/reentrancy, independent
completed-auth enforcement, 31-second/future/unclean observations, aging during
custody, prerequisite deadline clamping, interrupted uncertain writes, expiry
only during final scans/EOF/owned close, exact native PATCH/DELETE wire bodies and
preconditions, complete credential envelopes, isolated hosts with each standard
descriptor closed, signal-before-reap event assertions, and injected cleanup
failures with descriptor/reap checks. These are local fixture checks, not runtime
or operational qualification.

## Remaining integration and actual qualifications

A concrete independent provider transport, actual credential custody/IAM and
authority/grant lineage, complete tool/runtime dependency qualification,
full store/system orchestration (including telemetry PostgreSQL), isolated
capture/restore target drivers, current target ownership/resource identities,
proof-consumer integration and signed/non-dry/manual acceptance remain open.
Fresh exact attempt/policy/tool bindings and separately scoped operational
approval remain prerequisites to any real action; this implementation allocates
no recovery RID or policy and changes no expiry or retained owner decision.
Actual recovery, source pause/resume, target cleanup and preservation acceptance
have not occurred. Public invocation always returns a sanitized refusal with
zero backend requests before configuration or credential access.

## Independent S3 transport implementation — 2026-10-23

This continuation addresses the concrete provider gap identified above. The
scoped [implementation spec](../../_bmad-output/implementation-artifacts/spec-4-27-independent-immutable-provider-transport.md)
records the resolved protocol and offline boundary. Historical fixture receipts,
revision 4 and the preceding handoff text retain their original meaning.

The retained [destination evidence](../../_bmad-output/implementation-artifacts/evidence/epic-4/recovery-destination-setup.md)
identifies Scaleway S3 Object Storage in `fr-par` and the dedicated recovery
bucket. The existing [readiness contract](../kubernetes-upgrade/recovery_readiness.py)
and [guarded procedure](../kubernetes-upgrade/GUARDED-RECOVERY-PREPARATION.md)
require object metadata and a separate retention API result to agree for the
same immutable version. The implementation uses path-style `GetObject` with
`versionId`, followed by `GetObjectRetention` with that same exact `versionId`.
Both are read-only GETs; neither discovers nor substitutes the current version.
These operations are supported by [Scaleway's API](https://www.scaleway.com/en/docs/object-storage/api-cli/using-api-call-list/)
and follow the [S3 retention protocol](https://docs.aws.amazon.com/AmazonS3/latest/API/API_GetObjectRetention.html).

`S3ImmutableProvider` takes the exact `ProviderBinding`, original public CA
bytes, explicit region and an explicitly injected connector. The connector
receives a hostname-checking, `CERT_REQUIRED` TLS context containing only the
pinned CA, the bound endpoint and one absolute monotonic deadline. It owns any
connection/TLS resources it cannot return and must enforce that deadline within
each blocking call. No live connector, default credential chain or public
activation selector is supplied. Without a connector, `open_exact` refuses
before credential inspection or network access.

The independently bound reader credential's opaque secret bytes must contain
exact JSON fields `accessKeyId` and `secretAccessKey`; the existing credential
descriptor checks bind the entire envelope to its identity, custody and store
audience. Writer credentials are unavailable to the transport. S3 request
authentication uses region/service/date-bound [SigV4](https://docs.aws.amazon.com/IAM/latest/UserGuide/reference_sigv-create-signed-request.html).
Only invented credentials authenticate inert fixture requests; no recovery
policy, signature, acceptance document or production request is created.

The provider checks exact object-version headers, length, `AES256`, `COMPLIANCE`
and both retention representations before exposing body bytes. Header/XML
framing is bounded. Streaming checks actual byte count and SHA-256, and requires
bounded terminal EOF after the declared body. The normalized metadata's
`sha256` is the bound expected hash, not a claimed S3 checksum: actual bytes must
match it at EOF, and `ImmutableReadbackDriver` independently hashes those bytes
again. Later reads can shorten the original deadline but cannot extend it.
Owned streams close before successful completion; cleanup failure refuses
success and cancellation retains its primary interrupt semantics.

Remaining integrations are explicit credential acquisition/custody and current
IAM/grant-lineage qualification; separately reviewed live connection/TLS/runtime
qualification and activation; applicable native credential delivery; full
store/system and isolated-target orchestration, including telemetry PostgreSQL;
proof-consumer integration; fresh exact attempt/policy/tool bindings; and actual
recovery, resume, cleanup and signed/non-dry/manual acceptance. The concrete
offline transport does not establish any of these qualifications. Story 4.27
remains **in-progress**, 4.1 **closed**, Timesheets 3.6 **open/FAIL**, and all
public operational gates remain closed.


## Reviewed immutable-provider verification — 2026-10-23

The [corrected-source verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261023-immutable-provider-transport/review-1/verification.json) records **275 passing tests, zero
skips**: 42 provider cases and 233 existing readback/boundary/authentication/
common/telemetry cases. Full command transcripts and source hashes are retained.
The initial 265-test candidate packet and both exact candidate source snapshots
remain separate and unchanged.

Review corrections cover dispatch binding after callbacks and inherited S3
providers, persistent stream ownership through cancellation and handoff,
standalone retention expiry/deadline clamping, exact TLS trust-anchor bytes,
large streamed objects and hostile second responses. An in-memory wrong-CA
substitution is now positively detected. All retained review items were resolved;
no work was deferred by this review. Remaining live credential/authority/runtime,
system/target/proof-consumer and actual recovery integrations stay as listed
above. Public activation remains disabled; **4.27 stays in-progress, 4.1 closed,
and Timesheets 3.6 open/FAIL**.


## Native client-certificate delivery — 2026-10-23

The [scoped native-certificate spec](../../_bmad-output/implementation-artifacts/spec-4-27-native-client-certificate-delivery.md)
and [separate corrected-source verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/verification.json)
address the certificate-delivery gap recorded above. `NativeBinding` now pins an
explicit `authentication_profile`: `bearer` remains the compatibility default;
`client-certificate` must be selected explicitly. Certificate credentials contain
an exact JSON envelope with only `certificatePem` and `privateKeyPem` string
fields, inside the existing secret/identity/custody hash bindings. There is no
profile discovery, fallback or combined authentication.

`NativeTLS` loads the exact bounded PEM pair into the real TLS context using
sealed, close-on-exec memory descriptors. It accepts unencrypted PKCS#8, RSA or
EC PEM private keys; encrypted keys refuse without lookup or a password prompt.
Certificate authentication omits Authorization entirely. Bearer wire bytes and
exact per-cluster credential audiences remain unchanged. The exact CA trust,
hostname checks, native methods/paths/bodies, one absolute deadline and existing
semantic guards still apply. Exchange retains descriptor ownership through
installation cleanup; loading/cleanup interruption closes all owned inputs and
preserves the primary interrupt. Cleanup and elapsed-time checks precede success.
The explicit inert connector remains mandatory, and all public selectors refuse
before credential/configuration/network access.

The focused suite passes 21 static synthetic cases. Real `SSLContext` loading
and socket-free `MemoryBIO` mutual TLS verify the exact source/target client
certificates, including positive detection of no-op and wrong-certificate
loaders. All four plans compose with distinct identities, cluster audiences,
original native mutation bodies and null operational/recovery acceptance.
Additional cases cover mixed profiles, binding drift, malformed/missing/mismatched
or encrypted keys, sealing, fd 3 and closed-standard-descriptor preservation,
timeouts, cancellation and independent cleanup. A line-interruption regression
covers the parent audit's first-cleanup-line ownership finding. The seven suites
pass 297 tests, zero skips; commands, logs, matrix coverage and source hashes are
retained separately from all historical evidence.

Actual credential acquisition/custody, IAM/authority lineage and client identity
qualification; separately reviewed live connector/TLS/runtime qualification and
activation; complete store/system and isolated-target orchestration, including
telemetry PostgreSQL; proof-consumer integration; fresh exact attempt/policy/tool
bindings; and actual recovery/resume/cleanup with signed/non-dry/manual acceptance
remain prerequisites. Synthetic certificate fixtures confer no authority. No
actual credentials/configuration/endpoints, recovery-attempt allocation,
operational certificate issuance, recovery-document signing, upload, recovery or
infrastructure mutation occurred. Public operational gates remain closed;
**4.27 stays in-progress, 4.1 closed and Timesheets 3.6 open/FAIL**. Earlier handoff
prefixes, revision 4, signed originals and historical evidence remain unchanged.


## Reviewed native client-certificate ownership — 2026-10-23

The [review-1 corrected-source verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-1/verification.json) records **305 passing tests, zero skips**: 29 certificate cases
and the 276 existing boundary/readback/authentication/S3/common/telemetry cases.
The initial 297-test packet, four exact candidate snapshots, first-candidate
transcripts and original spec receipt remain unchanged historical evidence.
The original spec receipt hash is reproducible from its retained
`spec-implementation-checkpoint.md`; the new packet binds the reviewed state
separately and retains the exact original parent probe invocation as an archive.

Certificate inputs now register persistent owners inside allocation, before
returning to installation. Registration itself retains ownership across entry,
append and constructor-return interruptions. C-level descriptor close state
prevents a completed close from retrying a reused descriptor number. Shared
failure state preserves an original loading interrupt through a later cleanup
transition interrupt, while the remaining owners receive cleanup attempts.
Independent parent probes and direct regressions verify these boundaries.

Real socket-free mutual TLS also covers traditional RSA and EC keys, a valid
intermediate certificate chain, and an isolated count-limit refusal below the
byte limits. Valid CRLF and missing-terminal-newline PEM forms retain their
original installed bytes. The isolated fd 3 fixture explicitly checks its
precondition, sentinel bytes and descriptor/status flags. All prior exact peer
DER/CA, no-op/wrong-loader, four-plan, native-wire/precondition, bearer, semantic
stop and null-qualification checks remain passing.

Actual credential/custody/IAM/authority and client identity qualification,
live connector/TLS/runtime qualification and activation, complete store/system
and isolated-target orchestration including telemetry PostgreSQL, proof-consumer
integration, fresh exact attempt/policy/tool bindings, and actual recovery,
resume/cleanup plus signed/non-dry/manual acceptance remain pending. Synthetic
fixtures confer no authority. No actual credentials/configuration/endpoints,
recovery-attempt allocation, operational certificate issuance, recovery-document
signing, upload, recovery or infrastructure mutation occurred. Public activation
and operational gates remain closed; **4.27 stays in-progress, 4.1 closed and
Timesheets 3.6 open/FAIL**. Earlier handoff prefixes and evidence are preserved.


## Review-2 native certificate allocation and cleanup — 2026-10-24

The [review-2 content-bound verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-2/verification.json) records **315 passing
tests, zero skips**: 39 certificate cases and the 276 existing boundary/readback/
authentication/S3/common/telemetry cases. All 305 review-1 tests and every one of
the 24 registration subcases remain covered. Both historical candidate packets,
source snapshots, logs and spec receipt checkpoints retain their original bytes.

Each custody-backed allocator now registers a unique process-local memory-file
identity before the raw memfd syscall. Recovery of an interrupted syscall result
matches that exact memory-file identity, including its pinned inode once known;
closed owners from an earlier allocation cannot suppress a new allocation that
reuses their descriptor number. Stateful close ownership prevents re-closing a
released raw number. Direct opcode regressions cover memfd and duplicate results
before assignment for both certificate/key inputs, while unrelated, similarly
named and reused descriptors remain open and unchanged. Legacy raw allocator
callers retain their compatibility behavior without inheriting this custody
qualification.

The whole native exchange retains stream/error custody through final cleanup.
The original interruption survives a second interruption during initial error
recording, inner cleanup or the guarded outer fallback. Direct real-MemoryBIO
regressions also interrupt both stream-close entries with an open peer and an
owned memory input. Independent release attempts complete before any success;
the tested guarantee covers these two-interrupt patterns, not indefinitely
repeated asynchronous exceptions. New regressions reach custody-backed duplicate,
write, chmod and seal failures, isolate material/count bounds with no loader
dispatch, and reject empty/mismatched fixture collections instead of defaulting
or truncating them.

The executable receipt checker verifies recorded source, snapshot, log and spec
checkpoint hashes by content, without requiring a particular Git HEAD or index.
Its isolated self-test proves that matching changes to working source and its
snapshot still fail the receipt's source hash. The prior review-1 preservation
checker is unchanged: it deliberately requires the preparation HEAD and an
unchanged index, so it is a historical precommit check, not a later content-only
receipt verifier. Python patch/interpreter identity, OpenSSL and OS/release/
architecture are recorded without hostnames. Unrelated concurrent Story 2.3,
deferred-ledger and submodule edits are outside this scoped implementation and
were preserved.

Actual credential/custody/IAM/authority and client identity qualification,
live connector/TLS/runtime qualification and activation, complete store/system
and isolated-target orchestration including telemetry PostgreSQL, proof-consumer
integration, fresh exact attempt/policy/tool bindings, and actual recovery,
resume/cleanup plus signed/non-dry/manual acceptance remain pending. Synthetic
fixtures confer no authority. No actual credentials/configuration/endpoints,
recovery-attempt allocation, operational certificate issuance, recovery-document
signing, upload, recovery or infrastructure mutation occurred. Public activation
and operational gates remain closed; **4.27 stays in-progress, 4.1 closed and
Timesheets 3.6 open/FAIL**. Earlier handoff prefixes and evidence are preserved.


## Review-3 native certificate handoffs and bounded release — 2026-10-24

The [review-3 content-bound verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-3/verification.json) records **325 passing tests,
zero skips**: 49 certificate cases plus the existing 276 boundary/readback/
authentication/S3/common/telemetry cases. All 315 review-2 behaviors, the 39 prior
focused test names and all 24 registration subcases remain. Historical packets,
including the review-2 checker and original adverse parent probes, are preserved.
The original baseline and shared-workspace history remain unchanged.

Native exchange now requires an explicitly supplied private owned connector.
Trusted connectors register a resource-free stream placeholder in shared custody
before constructor acquisition; prepared inert streams already have a shared
owner before transfer. Raw resource-returning callbacks refuse before credential
or context work. Safe partial construction and idempotent release are part of
that private contract; real descriptor release state must be C-owned. Two close
interruptions before release still receive an independent third attempt. Current
exchange cancellation survives secondary recording/cleanup errors, while an
already handled caller interruption cannot replace it. A completed C-level close
never retries a reused unrelated descriptor. Fixture-owned cleanup also closes
preallocated streams on invalid profile or CA refusal.

Memory allocation registers its placeholder before reserving C-owned recovery
scans and before creating a memory file. Recovery rechecks the expected allocator
identity at descriptor adoption and needs no new directory descriptor after raw
allocation. Isolated child tests exercise reservation failure and memfd/duplicate
raw-result transitions at the actual last descriptor slot, restore the child's
RLIMIT_NOFILE and preserve unrelated descriptor bytes/flags. This qualifies the
tested bounded process-local protocol; arbitrary hostile descriptor-table
mutation, unsupported/non-idempotent third-party closers, indefinitely repeated
interruptions, kernel stalls and actual connector/runtime remain unqualified.

The new checker binds sources/snapshots/logs and historical spec checkpoints by
content, then parses actual unittest totals/skips/outcomes. Isolated coherent
source/snapshot and count/skip/outcome metadata controls fail independently.
Historical spec checkpoints remain separate from later workflow status/triage
edits; frozen human intent is still compared exactly. The unsigned packet confers
no external authenticity or operational authority. Runtime/interpreter/OpenSSL
and OS/release/architecture are recorded without hostnames. Unrelated concurrent
Story 2.3/2.4, deferred-ledger, sprint-workflow and submodule edits are excluded.

Actual credential/custody/IAM/authority and client identity qualification,
live connector/TLS/runtime qualification and activation, complete store/system
and isolated-target orchestration including telemetry PostgreSQL, proof-consumer
integration, fresh exact attempt/policy/tool bindings, and actual recovery,
resume/cleanup plus signed/non-dry/manual acceptance remain pending. Synthetic
fixtures confer no authority. No actual credentials/configuration/endpoints,
recovery-attempt allocation, operational certificate issuance, recovery-document
signing, upload, recovery or infrastructure mutation occurred. Public activation
and operational gates remain closed; **4.27 stays in-progress, 4.1 closed and
Timesheets 3.6 open/FAIL**. Earlier handoff prefixes and evidence are preserved.


## Review-4 native certificate lifetime and evidence controls — 2026-10-24

The [review-4 content-bound verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-4/verification.json) records **331 passing tests,
zero skips**: 55 certificate cases plus the existing 276 boundary/readback/
authentication/S3/common/telemetry cases. All 325 review-3 behaviors, all 49 prior
focused names and the 24 registration subcases remain. Every earlier packet,
including the review-3 checker and original adverse trace results, is preserved.
The original baseline and full current preparation HEAD are recorded separately.

The native lifetime guard starts before custody initialization and retains the
prepared stream through constructor entry, partial initialization and return.
Three independent idempotent cleanup passes protect their invocation handoffs
and active stream/input cleanup through the tested finite cancellation patterns.
Direct controls cover two cleanup-invocation interruptions, a lost raw syscall
result followed by scan/registration interruption, and a trusted constructor
failing after acquiring a C-owned descriptor. Caller exception history neither
replaces current cancellation nor turns a successful exchange into refusal.
Acquire must return None and produce a supported owned stream before dispatch;
protocol violations release every known prepared/current stream. The cleanup
identity check uses an explicit bounded loop instead of a generator.

Linear bounded PEM framing accepts TLS-valid compact base64 and leading blank
lines, preserving original installed bytes. Real SSLContext loading and exact
MemoryBIO peer/CA/wire checks remain, along with malformed/encrypted/mismatched
material refusals and separate envelope/material/count limits. Current sweeps
cover 299 line targets, 2,309 opcode targets and 81 secondary-cancellation targets:
no leaks, swallowed cancellations or unraisable exceptions. Five secondary
terminal-propagation sites are classified separately only after descriptor and
peer release is verified; a new external interrupt there has no signal-atomic
primary-return guarantee. Arbitrary hostile descriptor mutation, unsupported
closers, indefinitely repeated interruptions and kernel stalls remain unqualified.

The new checker requires all seven named suites and their exact pinned command
mapping, while keeping counts as recorded data. It rejects non-passing summaries.
Its isolated self-test runs actual unittest failure, skip, expected-failure and
unexpected-success transcripts with the same bound count and unchanged success
claims, updates all referencing hashes coherently, and requires refusal. Coherent
suite omission, command drift, source/snapshot drift and metadata drift also
refuse. Historical checkers/receipts stay unchanged; the unsigned packet supplies
reproducibility only, with no external authenticity or operational authority.

Actual credential/custody/IAM/authority and client identity qualification,
live connector/TLS/runtime qualification and activation, complete store/system
and isolated-target orchestration including telemetry PostgreSQL, proof-consumer
integration, fresh exact attempt/policy/tool bindings, and actual recovery,
resume/cleanup plus signed/non-dry/manual acceptance remain pending. Synthetic
fixtures confer no authority. No actual credentials/configuration/endpoints,
recovery-attempt allocation, operational certificate issuance, recovery-document
signing, upload, recovery or infrastructure mutation occurred. Public activation
and operational gates remain closed; **4.27 stays in-progress, 4.1 closed and
Timesheets 3.6 open/FAIL**. Earlier handoff prefixes and evidence are preserved;
unrelated Story 2.3/2.4, deferred-ledger, sprint-workflow and submodule work is
excluded. This offline pass performed no staging, commit, push or operational helper replay.

Shared-workspace provenance: preparation HEAD remains
`56c783bcd005e3ce0c728dc72e440a696a91d034`; a concurrent operation recorded
`b2ff52312b01db65aef3e8b60fb56681ae97d70a` (`fix: story 4.27`) during this pass.
That history is preserved and the observed current HEAD is separately bound in
`review-4/current-head.txt`; this pass performed no Git staging/commit/push.
