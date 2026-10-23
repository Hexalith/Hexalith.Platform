# Bounded recovery preparation adapters

These adapters prepare and test request primitives for Story 4.27. They do not
implement a live driver or complete CNPG/WAL, OpenBao raft, paired Memories
physical-copy/drain, private OIDC, object transfer or target lifecycle
orchestration. A successful local result establishes consistency of an offline
proposal or synthetic protocol run. Every real-system acceptance field remains
`null`, and the operational gate stays `closed`.

The October 17 candidate and every earlier attempt keep their original dates,
bytes and meaning. This module allocates no recovery RID, selects no historical
executor, signs nothing and grants no permission. Current authentication, grant
lineage, provider protections, runtime observations and recovery acceptance are
separate prerequisites. Sole-owner documentary self-review does not establish
any of those facts. The existing destination, principal, key, roles, incident
channel and October 7–31 window must be retained in the separately authenticated
policy and private bundle. Nothing here opens 4.1 or completes Timesheets 3.6.

## Local CLI

All modes read only an exact SHA-256-bound local JSON input. Inputs must be
absolute paths, regular single-link files owned by the current user with mode
0600, under safe non-symlink directories. A mode-0700 output directory must
already exist. A sticky root-owned temporary-directory ancestor is allowed;
the input/output directory itself still has the required private custody.

```text
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/recovery_adapters.py proposal --input <exact-private-proposal.json> --sha256 <exact-sha256> --private-output <new-private-render.json>
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/recovery_adapters.py validate --input <exact-fully-bound-input.json> --sha256 <exact-sha256>
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/recovery_adapters.py render --input <exact-fully-bound-input.json> --sha256 <exact-sha256> --private-output <new-private-render.json>
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_recovery_adapters.py
```

The placeholders describe dependencies and are never executable commands in a
review bundle. Its actual commands must substitute exact private input paths
and hashes. No mode opens a credential, socket or subprocess, or invokes the
injected backend. `live`, `activate`, `execute` and their corresponding flags
are refused before any input access. Invalid arguments and all failures produce
a fixed sanitized result without input values, exception text or diagnostics.

Stdout is a neutral projection: local consistency, operation, request count,
input/scope and rendered-plan digests, missing tool roles and closed gates.
Concrete endpoints, resource identities, principal labels, object references,
request bodies and tool paths appear only in the private render. The private
render is create-only 0600; an existing path is refused and preserved. Encrypt
that private packet under the retained custody recipient before retaining raw
source/provider/custody material. This module supplies no encryption or upload
implementation. The CLI does not inspect a Page, provider or credential store.

## Proposal schema and honest missing bindings

`proposal` accepts current measured identities while future policy/signature,
artifact VersionIds, created target UIDs and qualified drivers are absent.
Its schema has exactly these fields:

| Fields | Value |
| --- | --- |
| `schemaVersion`, `policyId`, `observedAt` | Integer `1`, retained documentary policy identity, exact UTC observation timestamp |
| `sourceCluster`, `targetCluster` | Cluster object described below |
| `sourceNamespace` | Bound Namespace Resource |
| `targetNamespace` | Proposed Namespace Resource with both `uid` and `resourceVersion` null |
| `protectedSources` | Nonempty complete explicitly protected Resource list, including the source Namespace, paused workload and stores |
| `pausedWorkload`, `sourceStores`, `previousReplicas` | Bound Deployment, nonempty bound store Resource list, integer 1–64 recorded before pause |
| `watcher`, `watcherBefore`, `watcherDuring` | Bound controller Deployment and exact ordered WATCH_NAMESPACE lists; during equals before plus the proposed target name |
| `operatorPrincipal`, `validatorPrincipal`, `signerSha256` | Retained principal label, independently qualified validator label or null, public signer digest or null |
| `retainedDecisionsSha256` | Exact retained owner decisions digest; no authority is inferred |
| `tools` | Exactly `kubectl`, `artifactReader`, `signatureVerifier`, `stageRunner`; each Tool or null |
| `preparationTools` | Nonempty array of actual preparation Tool identities |

A Cluster is `{uid, endpoint, caSha256}`. The endpoint must use HTTPS, an exact
IPv4 address and explicit port; DNS, context aliases, insecure transport and
ambiguous same-endpoint cluster identities are refused. A Resource is
`{apiVersion, kind, namespace, name, uid, resourceVersion}`. Cluster-scoped
resources use an empty namespace. Bound Resources require exact nonempty UIDs
and numeric resourceVersions. Proposed Namespace identity is the only allowed
null identity. A Tool is `{path, sha256, size}` with an absolute canonical path
and exact byte digest/size. These references do not establish current executable
custody or runtime qualification.

The private proposal contains concrete known source GETs and the exact proposed
namespace GET requiring NotFound. Dependent capture/restore protocol shapes,
source replica PATCH and owned-target DELETE shape remain marked `blocked`.
Unknown future resourceVersions, target UIDs and artifact references stay null.
These shapes cannot be dispatched. `runnable` is false, all unresolved gate
values are null, and the proposal schema is deliberately rejected by
`parse_binding` and `run`. No new policy, signature, VersionId, tool hash or
recovery identity is fabricated to obtain a passing render. Observation time
records a fact; it does not refresh an expired recovery candidate.

## Fully bound request schema

`parse_binding` converts a separate closed JSON schema into immutable typed
`Binding`, `Cluster`, `Resource`, `Artifact`, `Tool` and `VolumePath` objects.
Direct dataclass instances are checked again when building requests or running.
The complete schema is exercised by the synthetic fixture in
[test_recovery_adapters.py](test_recovery_adapters.py). It uses these fields:

| Fields | Value |
| --- | --- |
| `schemaVersion`, `operation`, `policyId` | Integer `1`; `capture`, `restore`, `resume` or `cleanup`; retained policy identity |
| `validFrom`, `expiresAt`, `retainUntil` | UTC second timestamps; validity at most 24 hours; retention later than expiry |
| `operatorPrincipal`, `validatorPrincipal` | Explicit different credential principal labels |
| `signerSha256`, `retainedDecisionsSha256` | Independently pinned public signer and retained owner decisions digests |
| `sourceCluster`, `targetCluster`, `sourceNamespace`, `targetNamespace` | Typed exact source and target identities |
| `protectedSources`, `ownedTargets` | Exact distinct Resource inventories; protected and target UIDs/paths cannot overlap |
| `pausedWorkload`, `sourceStores`, `previousReplicas` | Explicit source Deployment, store identities and recorded replicas |
| `watcher`, `watcherBefore`, `watcherDuring` | Exact controller identity and ordered before/during watch lists |
| `watcherContainerIndex`, `watcherEnvironmentIndex` | Optional explicit integer positions, default 0; reads/tests must match WATCH_NAMESPACE exactly or refuse |
| `protectedPaths`, `targetPaths` | Exact source local-volume paths and target `{pvUid,nodeUid,path}` records |
| `policy`, `signature`, `restoreInput` | Immutable Artifact objects; restore input may be null for capture/cleanup |
| `tools` | Exactly the four Tool roles above; null renders an explicit missing-driver gate and prevents `run` |

An Artifact is `{store, key, version, sha256, size, retainUntil}`. Exact versions
are mandatory. Aliases such as latest/current/default/head, traversal, duplicate
JSON fields, nonfinite/floating numbers, booleans in integer slots, unknown
fields, malformed identities, endpoint ambiguity, hash and source/target drift
are refused. Namespaced protected objects must have their Namespace identity in
the protected set. Inventory completeness still requires independent current
observation; a caller-supplied list does not prove no other source exists.

Capture requires an absent proposed Namespace: target UID/resourceVersion are
null and owned target/path arrays are empty. An actual GET must return a matching
NotFound Status before the capture primitive. Restore, resume and cleanup
require the new independently observed Namespace UID/resourceVersion and owned
resources after separately guarded target creation. Restore/resume also require
an exact immutable recovery input. Target creation and source quiesce are not
implemented here. A proposed target name never substitutes for its future UID.

Every owned PV requires one exact node UID/path binding. Paths are bounded to
`/var/openebs/local/`, cannot overlap protected paths, and are checked independently
for absence. Other storage providers and larger unqualified input profiles are
refused. Current capacity, extraction, images, stdin and mount layout remain
separate runtime gates.

The new policy envelope must exactly contain:

```json
{"kind":"hexalith.recovery-adapter-policy","schemaVersion":1,"scopeSha256":"<exact scope digest>","validFrom":"<bound UTC>","expiresAt":"<bound UTC>","retainedDecisionsSha256":"<exact retained decisions digest>"}
```

`Binding.scope_sha256()` hashes the canonical complete typed scope excluding
only its own policy and signature references. It includes the operation,
source/target identity, tools, principals, retention, stop-relevant inventory
and recovery input. This adapter envelope does not replace the retained owner
policy. A future authenticated policy must bind both and preserve scoped owner
decisions. An existing historical signature cannot authenticate this new scope.

## Injected I/O and guarded transitions

`build_requests(binding)` produces deterministic immutable `Request` objects.
`private_plan(binding)` returns their full private representation. Native
requests contain an exact endpoint, API method/path, canonical stdin body and
expected semantic check. PATCH requests use `application/json-patch+json`.
Native argv remains null because no qualified native dispatcher is supplied.
External protocol argv is present only if its exact Tool is bound, and describes
a **required future interface**, not an available executable:

```text
<exact-qualified-tool> --recovery-protocol-v1 --request-stdin --credential-fd=3
```

No implementation of this interface is supplied. In a real proposal, unavailable
roles must be null rather than replaced by invented helper paths or tool hashes.
The future native/external driver must verify exact tool bytes/custody, interpreter
and dependency bytes, credential identity, pinned cluster UID/CA/endpoint and
fd delivery before dispatch. It must supply only the fixed environment, no
ambient kubeconfig, HOME, proxy, agent or shell resolution. No historical tool
is imported, run, copied into an executor role or selected through a new RID.

`run(binding, backend, receipts, now, monotonic)` calls only the injected
`backend.exchange(request, absolute_deadline)`. Synthetic tests provide every
backend observation. There is no built-in backend. A separately reviewed and
approved driver must enforce one absolute 15-second deadline per request,
maximum body/error bytes, no redirects, no retries and bounded argv/stdin.
The runner checks elapsed time after a returned call, but cannot interrupt a
hung injected Python function; enforced cancellation is a required driver gate.
JSON input/stdin/API replies are bounded to 2 MiB; exact recovery readback is
bounded to 64 MiB. Combined policy/signature bytes are bounded to 1 MiB so their
base64-authentication stdin stays below 2 MiB. Larger captures/transfers need a
separately qualified streaming interface. stderr is capped at 64 KiB by the
future driver; raw diagnostics never enter these receipts.

The transitions are:

1. Independently read policy and signature through the distinct validator
   principal using exact immutable versions. Check actual bytes, SHA-256,
   length, version and retention. Validate the exact policy scope/dates. Only
   then materialize the verifier's stdin from these exact readback buffers and
   request detached cryptographic verification against the pinned public signer.
   The rendered verifier request identifies this dependent materialization;
   the create-only runtime intent records its actual final stdin/request hash.
   Verification requires the matching policy, signature, signer and scope
   digests; an `approved`/`authenticated` boolean is refused. The future verifier
   must perform actual cryptography; matching response strings alone are never
   an operational qualification or a replacement for that trusted implementation.
2. Check exact protected source UID/resourceVersions before the stage. Capture
   also checks the proposed target is absent. Restore/resume bind every owned
   target UID/resourceVersion, independently read exact immutable recovery bytes,
   and require isolated identity, default-deny networking, no host mounts and
   no source mounts. Restore and verification must return the exact input digest,
   source UIDs and target UID. Unknown or failed semantic outcomes stop even if
   the process exited zero. Restore checks protected sources again afterward.
3. Resume first requires verified restore/isolation, healthy stores with observed
   changed runtime identity, a fresh clean drain and a still-paused exact source
   Deployment. Drain must have zero pending/in-flight/missing/duplicate work and
   an unchanged stream digest; age is checked again immediately before PATCH.
   JSON Patch tests UID, resourceVersion and replicas 0, then restores only the
   recorded previous replica count. A subsequent GET must show matching desired,
   updated, ready and available replicas and observed generation. A sent PATCH
   without that reconciliation remains uncertain and requires the incident owner.
4. Cleanup checks every target identity and an exact complete owned inventory
   before any deletion. It deletes only inventoried resources with both UID and
   resourceVersion preconditions; Namespace is last. It then requires fresh exact
   NotFound reads for every target and explicit same-node path-not-found
   observations. A 403, timeout, remaining PV or unobserved host path cannot mean
   absence. It restores the watcher with exact UID/resourceVersion/value tests,
   independently reads back its original ordered scope, and rechecks sources.

`receipts.create(key, bytes)` must be create-only. `LocalReceipts` implements
0700-directory/0600-file no-follow descriptor custody, exclusive creation and
fsync. A run writes an intent before any backend call and an intent/result per
request; only response hashes and fixed semantic states are recorded. Existing
records are preserved. Reusing a scope/operation refuses before another backend
request. Failure retains partial effects, pending intents and earlier results.
No automatic retry, rollback, resume, target cleanup or multipart abort follows.

## Stops, recovery and qualification limits

A resume failure returns `stopped-pause-reconciliation-required`. Until the
incident owner independently determines actual replica/store/drain state,
retain the pause or treat a possibly applied PATCH as uncertain. Do not guess
that source replicas were restored, or automatically issue a compensating patch.
A cleanup failure returns `stopped-cleanup-incomplete`; retain exact remaining
owned identities, storage/path evidence and watcher state for a separately
reviewed recovery action. Inventory/identity drift requires a new factual
assessment rather than changing earlier receipts or relaxing preconditions.

An object writer without `ObjectStorageObjectsDelete` may be unable to abort a
multipart upload even when bucket policy allows that API. No provider grant,
upload, abort or multipart-cleanup adapter is supplied. Retain partial upload
identity and failure evidence. An already configured one-day abort lifecycle
is later observable provider behavior, never synchronous cleanup success.
Do not infer immutable readback, object retention, legal hold, grant lineage or
provider protection from a successful command or a writer's self-report.

The primitive completion states end in `-unqualified`. `operational_qualification`,
`recovery_acceptance`, `source_resumed` and `target_cleanup_complete` always remain
null, including all passing tests. Signed policy authentication, off-node
custody/readback, exact qualified store orchestration, images/capacity/stdin,
source-resume and target-cleanup runtime acceptance require actual independently
assessed observations from a separately approved driver. The preparation bundle
can be reviewed technically while those operational gates remain closed.

The synthetic suite covers every frozen matrix row:

| Matrix row | Representative executed tests |
| --- | --- |
| Offline plan | `test_offline_plan_is_deterministic_and_never_calls_backend`, `test_proposal_cli_only_local_and_public_projection_is_neutral`, `test_live_activation_refused_before_any_local_read` |
| Unsafe input | `test_expired_future_and_24_hour_overrun_refuse_before_backend`, `test_source_target_name_uid_and_path_collision_refuse`, `test_identity_uid_and_resource_version_drift_stop` |
| Capture/restore boundary | `test_authentication_consumes_the_exact_independently_verified_readback_bytes`, `test_immutable_restore_input_corruption_stops_before_restore`, `test_process_success_does_not_override_semantic_failure`, `test_restore_isolation_source_mount_host_mount_and_network_failures_stop` |
| Source resume | `test_resume_dirty_stale_future_or_changed_stream_drain_leaves_pause`, `test_drain_that_ages_before_patch_is_rechecked`, `test_resume_only_patches_recorded_identity_zero_and_prior_replicas`, `test_resume_requires_actual_reconciliation_after_patch` |
| Target cleanup | `test_cleanup_deletes_only_owned_uids_rv_and_namespace_last`, `test_cleanup_403_timeout_and_remaining_resource_are_not_absence`, `test_cleanup_remaining_path_and_wrong_node_are_not_absence`, `test_cleanup_watcher_restoration_tests_exact_uid_rv_and_original_scope` |
