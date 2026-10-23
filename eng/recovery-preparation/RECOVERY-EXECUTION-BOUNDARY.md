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
