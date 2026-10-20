# Recovery hook contract 1.0.0

This is the normative, language-neutral wire contract for Platform recovery hooks.
The words MUST, MUST NOT and SHOULD express requirements. The
[JSON Schema](../../schemas/hexalith.recovery-hook.v1.json) uses Draft 2020-12 and
the exact `contractVersion` value `1.0.0`. Closed record shapes are intentional:
unknown fields, versions and duplicate JSON object keys MUST be refused.

The contract defines declarations, invocations and results. `context` and `chain`
are offline conformance sidecars, not new release-record or attempt-store formats.
This story supplies no hook implementations, credential issuer, attempt store,
atomic persistence, publication integration or live restoration. Schema validity,
including matching credential metadata, proves none of authentication, fencing,
module recovery qualification or runtime epoch enforcement.

## Release binding and retention

Every release record MUST bind the exact recovery contract version used by its
module declarations, requests and results. Consumers MUST select that version
explicitly; they MUST NOT silently select a latest version or fall back to another
major. Unknown versions fail closed. The endpoint major identifies the wire family;
`contractVersion` selects the exact contract within it. The schema for exactly
`1.0.0` is `schemas/hexalith.recovery-hook.v1.json`. Every later exact version
publishes its own schema as `schemas/hexalith.recovery-hook.<major>.<minor>.<patch>.json`
with a matching `$id`, and consumers select the schema by exact `contractVersion`.
A version and its schema become immutable when the first release record binds
them. After that, a changed contract needs a new exact version and its own
conformance evidence, even if its wire family stays the same.

The in-place entry point and pinned off-site recovery workflows MUST support every
version referenced by any rollback target or retained recovery point's release.
A major MAY retire only after no such release references it. Retiring one major
does not authorize deleting artifacts or evidence still required by retention.
Stories 4.6 and 4.12 own record encoding and publication binding; those consumers
are not implemented here. This document establishes their required input.

## Cut and positions

`cut.cutId` identifies one Platform-declared recovery cut. Its `positions` records
the boundary each recovery class reaches at or before that cut. Every cut MUST
include `authoritative-restore`, `rebuild-only` and `live-authority-only` positions,
an `eventstore-position` owned by `eventstore`, and a
`memories-register-sequence` owned by `memories`. The schema checks that coverage.
An omitted class or required position is not a zero or an implicit success.

Each position has a unique `positionId` and a typed descriptor: `ownerModule`,
`scope`, `kind`, `recoveryClass` and `encoding`. Its `value` is a module-owned,
opaque string. `relationToCut` is the owner's assertion `at-cut` or `before-cut`.
There is no universal counter, cross-module ordering, or conversion between the
EventStore position and Memories register sequence. Even equal decimal strings
in different scopes or encodings are unrelated. Each module MUST define and
qualify its encoding and its proof that its boundary is at or before the cut.
An offline checker cannot establish that proof from an opaque string.

Every position MUST have exactly one matching declaration `positionDefinitions`
entry under its owner. Duplicate IDs and duplicate typed scopes are refused.
All invocations and results bind the entire cut, including the exact positions;
comparing only `cutId` is insufficient. Position array order does not change cut
identity. Within a retry, arrays MUST retain their original representation.

`targetPositions` MUST contain exactly the cut positions owned by the invoked
module. Every result MUST report exactly those `achievedPositions`. A reported
position MUST preserve the target's full typed descriptor. `targetReached: true`
requires the exact target value and relation to the cut. A position is due from
the phase that reaches it: `authoritative-restore` and `live-authority-only`
positions from the module's first hook, `rebuild-only` positions from `rebuild`.
A successful result MUST have reached every due position. Before `rebuild`, a
result reports a `rebuild-only` position with the owner's current value and
`targetReached: false`. An owner MAY report an earlier value for a due position
only with `targetReached: false`; that outcome cannot succeed. The owner, rather
than this generic validator, verifies any earlier boundary. The cut's own target
may already be `before-cut`.

## Declarations and capabilities

A declaration binds `releaseId` (an immutable SHA-256 release identity), `moduleId`,
its owning `workloadId`, module `dependencies`, position definitions,
`reopenBeforeReplay`, the internal `endpoint`, `survivingAuthorities` and `hooks`.
Every module dependency MUST exist in the release declaration set. Module and
task dependency graphs MUST be acyclic. `moduleId`, `positionId` and `authorityId`
are unique across the release declaration set, so each surviving authority has
exactly one owning module, fence hook and reissue procedure. `hookId` is unique
within its module. `taskId` and `invocationId` are unique within the attempt; a
result is identified by its `invocationId`.

Each module MUST declare exactly one hook for each ordinary phase below. Multiple
fence hooks are permitted, one for each surviving authority. A hook capability
declares `implementation` as `implemented`, `missing` or `not-applicable`.
The last two require a non-secret `reasonCode`. A `missing` hook ALWAYS leaves
an attempt that requires it unqualified; in-place attempts do not require fence
hooks. An empty result set or a placeholder MUST NOT imply support.
`not-applicable` is an explicit owner declaration, consumed and qualified with
that release. Integrity, reconciliation and fence hooks MUST NOT be marked
`not-applicable`, and neither may the `rebuild` hook of a module that owns a
`rebuild-only` position. A reconciliation hook with no external operations can
report `externalEffects.status: not-applicable` with an evidence reference.

An authority entry binds `authorityId`, its `fenceHookId` and an owner-maintained
`reissueProcedureRef`. The fence hook MUST name that same authority. Those
references identify procedures and custody bindings, not credentials or a claim
that a procedure has run. Platform-owned authorities remain the responsibility
of their custodians; this module declaration set does not replace their gates.

## Recovery mode and ordered phases

Before any hook runs, the owning executor MUST establish recovery quarantine:
user ingress is `closed`, external-effect workers and destructive-retention
workers are `disabled`, and only the owning executor, named recovery workloads
and named probe sources are admitted. No user- or schedule-triggered work may run.
User-actor asynchronous work MUST remain blocked until the reconciled admission
projection is rebuilt. Ordinary workers MUST NOT gain recovery authority through
their normal application principal. The context `quarantine` fields describe
these independently verified controls; they do not install them.

The executor MUST use the following phase barriers. All applicable hooks in an
earlier phase MUST succeed before any later phase begins. Within a phase,
declared module dependencies run first. `dependsOn` MUST name every applicable
earlier-phase task and each direct dependency module's task in the same phase;
additional dependencies are permitted if acyclic. A dependency MUST have a
successful terminal result before the dependent invocation, including retries.
Missing implementations block qualification even if other hooks can be inspected.

| Phase | Required behavior and execution boundary |
| --- | --- |
| `fence` | DR only. Revoke the old instance's authority and prove old credentials fail. Execute the module-owned fence hook separately in the recovery executor sandbox. Custodians release material for exactly its `authorityId`; retain only `custodianMaterialRef`. The ordinary workload MUST NOT receive it. The old capacity remains isolated. |
| `admission-purge` | In the owning workload, reconcile admission and purge module-owned post-cut or restored transient state according to the owner protocol. Preserve erasure and revocation lineage. Do not replay user work before admission is safe. |
| `credentials` | In the owning workload, re-provision backend principals and module-owned dynamic-credential namespaces, then rotate restored credentials and their bindings. Only the owning module writes its dynamic namespaces. The phase precedes rebuild and does not rotate unrelated authorities. |
| `rebuild` | In the owning workload, rebuild or replay `rebuild-only` state from restored checkpoints to the declared target. Broker re-provisioning occurs before subscriptions are rendered and subscriber catch-up begins. External effects and destructive retention remain disabled. |
| `integrity` | In the owning workload, verify the restored/rebuilt state and owner invariants, cut coverage, erasure/lineage and cross-module consistency. Report explicit `passed`, `failed`, `unknown` or `not-run`, with evidence for passed/failed checks. |
| `external-effect-reconciliation` | In the owning workload, reconcile external-operation state, including uncertain and post-cut effects. Do not silently replay an unknown external effect. Return final module gate evidence for integrity, reconciliation and replay. |

The owning module's ordinary hooks run as recovery-scope tasks under its own
least-privilege principals. `execution.location` MUST be `owning-workload`,
`workloadId` MUST match the declaration and admitted recovery workload, and
`executorId` MUST identify the owning executor. A fence invocation instead MUST
use `recovery-executor-sandbox`, the matching `authorityId` and an
authority-specific `custodianMaterialRef`, with no workload identity. Credential
references alone do not establish that isolation; runtime consumers MUST enforce
it and qualify the hook's behavior.

For `disaster-recovery`, the recovery-hook issuer MUST be newly created on
prepared replacement capacity, with fresh keys. Restored credentials or issuer
keys MUST NOT authorize hooks. `issuer.origin` is `replacement-capacity`,
`keySource` is `fresh`, and the new `generationId` MUST differ from
`priorIssuerGenerationId`. Each surviving authority's custodian completes
replacement-instance credential reissue before reopening; a `pending` reissue
blocks reopening even when module tasks completed.

For `in-place`, close ingress and quiesce the current workloads while preserving
data objects. Retain the live realm and key custody, purge post-cut transient
state as prescribed by its owners, rotate credentials restored with module data,
and rebuild admission from the live realm. The issuer is the live
`current-environment` issuer. Fence hooks MUST NOT run, and
`survivingAuthorityReissue` MUST be `not-applicable`. This mode has no surviving
authority reissue, Keycloak/OpenBao restore, realm signing-key rotation,
environment-layer re-application, replacement cutover or credential handoff.
Declarations still retain fence capabilities for their DR consumers.

## Endpoint and authorization

The fixed request target is `POST /_internal/recovery/v1`, using the invocation
shape and JSON content type. An ordinary handler is served only by the owning
workload in recovery mode, on its private recovery route. It MUST NOT be exposed
on public ingress, ordinary operation discovery or McpCli. Fence handlers are
executed through the same message protocol inside the recovery executor sandbox;
they MUST NOT dispatch a fence task into the ordinary workload.

The runtime MUST authenticate a short-lived credential through its trusted issuer
over the private authenticated transport, normally an authorization header. The
retained JSON `authorization` object is a binding/reference record only. A raw
token, password, private key or custody material MUST NOT appear in any retained
request, result, fixture, log or diagnostic. `credentialRef` identifies the
out-of-band credential without granting access to it.

Only this environment instance's recovery-hook principal may invoke a hook. The
consumer MUST validate the issuer/signature, issuer generation, principal,
audience, `recovery-hook` scope, environment, instance, `attemptKind: recovery`,
current attempt ID, epoch and validity interval against independently trusted
state. Caller-supplied JSON claims MUST NOT replace authentication or a current
attempt-store read. The owning executor and workload identity MUST match the
quarantine allowlist. Credentials are valid from `issuedAt` inclusive until
`expiresAt` exclusive, and MUST still be valid at commit. Ordinary user,
deployment, stale-instance and restored-issuer credentials MUST be rejected.

Before admission and each retry, the consumer MUST recheck recovery mode, attempt
ownership, current epoch and its authorization. A stale in-flight task MUST be
cancelled or prevented from committing even if it authenticated before takeover.
The owner MUST also enforce any authority-specific fence needed for provider
effects; a metadata match or a preflight epoch check does not fence a mutation.

The terminal response body is the result shape. A conforming terminal result
uses HTTP 200 even for its explicit `failed`, `incomplete` or `unqualified`
outcome; transport success is not task success. Refuse malformed or unsupported
input with 400, failed authentication with 401, unauthorized callers or ordinary
mode with 403, stale context/epoch or conflicting retry identity with 409, and
unavailable current authority with 503. Refusal responses MUST use
`Content-Type: text/plain; charset=utf-8` and exactly the stable, non-secret reason
code as their body, with no surrounding JSON, trailing newline, reflected input or
credential details. For example, an epoch conflict can return HTTP 409 with that
content type and the exact body `stale-epoch`. Reason codes use the ID character
set, stay stable within an implementation and are diagnostics only; executors
MUST branch on the HTTP status alone. Runtime implementations MUST fail closed
when authoritative state is unavailable.

## Retry identity and atomic commit obligations

`taskId` identifies one logical module/hook task within the attempt; it MUST NOT
be reused for another hook. `invocationId` identifies a delivery and MUST be
unique. The initial delivery has `retry: 0` and no `retryOf`. Subsequent deliveries
increment retry by exactly one and name the immediately previous delivery.
The task's release, version, environment/instance, attempt/kind, epoch, cut,
module, hook, target positions, endpoint, dependencies and execution boundary are
immutable across retries. A new epoch starts new work under its current attempt;
it MUST NOT resume an old task by changing its identity in place.

The consumer MUST deduplicate an identical delivery and return its recorded
result without repeating its mutation. If the original execution is still in
flight, an identical duplicate MUST join that execution and wait for its terminal
result, never creating another task. After transport loss, the caller MAY resubmit
the identical delivery. The same delivery ID with a different
payload MUST be refused. A new delivery MAY retry an explicitly `failed` or
`incomplete` predecessor only after that predecessor is known to have ended;
it MUST NOT race an uncertain in-flight delivery. It MUST NOT retry a succeeded
or unqualified predecessor as new work. Renewed credential references and
validity intervals are allowed, with all current authority bindings revalidated.
Module implementations MUST make task effects idempotent or explicitly reconcile
them before a retry; transport failure is never evidence of no effect.

Immediately at each task commit, the runtime MUST re-read the authoritative
current attempt and epoch and check all immutable bindings. The authoritative
mutation, durable task completion/deduplication record and epoch condition MUST
share an atomic transaction or an equivalent authority-enforced fencing
mechanism. A prior read followed by an unguarded write is insufficient. A failed
guard MUST discard/reject the stale completion; the task MUST NOT establish
success or publish an accepted result. Cross-authority work requires each
authority's own fencing and reconciliation; this contract does not create a
distributed transaction. The attempt-store and hook owners must implement and
prove those obligations. The offline validator implements none of them.

## Results and reopening

A result MUST bind `contractVersion`, `releaseId`, environment, instance, attempt,
recovery kind, epoch, mode, complete cut, module, hook, task and invocation, plus
`completedAt`. It MUST include achieved positions, explicit `status`, integrity,
external-effect reconciliation, replay state and `reopeningEligible`.

`status` is `succeeded`, `failed`, `incomplete` or `unqualified`. Non-success
requires a stable `reasonCode` and `reopeningEligible: false`. Missing
implementations are unqualified. Missing results and unfinished required work
are incomplete. Failed integrity and unknown integrity/external-operation states
MUST NOT establish success. Each gate belongs to a phase: replay to `rebuild`,
integrity to `integrity` and external effects to `external-effect-reconciliation`.
A result from an earlier phase MUST report that gate `not-run`, because its hook
has not observed it. A successful integrity hook requires
`integrity.status: passed`, and a successful reconciliation hook requires
`externalEffects.status: reconciled` or evidenced `not-applicable`.

`replay.status` is `complete`, `pending`, `not-run` or `not-applicable`. Complete,
pending and not-applicable states require evidence references. Successful rebuild
and reconciliation hooks MUST report replay state. A hook cannot succeed with
replay pending unless its release declaration explicitly permits
`reopenBeforeReplay: true`. The module's final reconciliation result carries this
exception and requires every declared target reached, integrity passed, external
effects reconciled and documented owner evidence that pending replay is safe after
reopening. It does not waive erasure, admission, external-effect or other gates.
With the declaration false, unfinished required replay remains incomplete and
blocks reopening.

`reopeningEligible: true` is an advisory module gate that only the module's final
reconciliation result may set: it requires succeeded status, all targets reached,
passed integrity, reconciled or evidenced not-applicable external effects, and
complete/not-applicable replay or the explicit pending replay exception. That
result MUST summarize those gates for the module. It MUST NOT contradict earlier task evidence or hide an
unresolved failure. Evidence references identify owner-maintained proof, not
proof verified by JSON Schema. A failed attempt stays failed unless an explicitly
permitted retry resolves that task and produces fresh gate evidence.

Only the executor and authorized operator decide whether to reopen after ALL
required module and Platform gates, cross-module checks, smokes, current
revocation/admission reconciliation, credential rotation and any DR fencing and
reissue proofs are accepted. Restore only the recorded prior ingress state;
pre-G2 production ingress stays closed and the promotion stop stays set. A
successful hook, HTTP response or offline validator MUST NOT reopen ingress,
enable workers or declare live recovery qualified. Workers resume only after
their external-operation state is reconciled and the owning executor accepts it.

## Offline conformance boundary

The [local validator](../../eng/recovery-contract/README.md) validates the schema
and cross-record rules against separately supplied invocation and commit
contexts. The context records the expected release, cut, environment/instance,
attempt, epoch, mode, principal, audience, issuer, quarantine and reissue state.
Runtime consumers MUST obtain those expectations from trusted current state.
Deriving the expected context from a submitted invocation defeats the comparison.

Both contexts MUST agree on identity, epoch, issuer and quarantine; commit time
must not precede admission time. Reissue may advance from pending to complete.
A changed commit epoch refuses the whole chain, simulating stale in-flight work.
This is a comparison test, not an implementation of a lock, credential issuer,
authoritative timestamp or atomic compare-and-set store. Complete offline chains
remain `runtimeQualified: false`. Incomplete, failed and unqualified outcomes are
preserved and never produce reopening eligibility.
