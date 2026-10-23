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
| `pausedWorkload`, `sourceStores`, `previousReplicas` | Bound Deployment, distinct source-namespace store UID/path identities of kind StatefulSet, Deployment or CNPG Cluster, integer 1–64 recorded before pause |
| `watcher`, `watcherBefore`, `watcherDuring` | Bound controller Deployment and exact ordered WATCH_NAMESPACE lists; during equals before for no change, or before plus the proposed target name for a scoped extension |
| `operatorPrincipal`, `validatorPrincipal`, `signerSha256` | Retained principal label, independently qualified validator label or null, public signer digest or null |
| `retainedDecisionsSha256` | Exact retained owner decisions digest; no authority is inferred |
| `tools` | Exactly `kubectl`, `artifactReader`, `signatureVerifier`, `stageRunner`; each Tool or null |
| `preparationTools` | Nonempty array of actual preparation Tool identities |

A Cluster is `{uid, endpoint, caSha256}`. The endpoint must use HTTPS, an exact
IPv4 address and explicit canonically spelled numeric port; leading zeros,
DNS, context aliases, insecure transport and
ambiguous same-endpoint cluster identities are refused. A Resource is
`{apiVersion, kind, namespace, name, uid, resourceVersion}`. Cluster-scoped
resources use an empty namespace. Bound Resources require exact nonempty UIDs
and numeric resourceVersions. Proposed Namespace identity is the only allowed
null identity. Namespace names are DNS labels of at most 63 characters, without
dots; other resource names retain the bounded DNS-subdomain profile. Duplicate
protected API paths are refused even if they carry different UIDs. A Tool is
`{path, sha256, size}` with an absolute canonical path and exact byte digest/size.
These references do not establish current executable
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

The common Memories proposal and stock telemetry PostgreSQL recovery require no
CNPG watch change: `watcherDuring` explicitly equals `watcherBefore`. Their
cleanup proposal discloses `restorationRequired: false`; cleanup independently
verifies the unchanged exact watcher scope without a watcher PATCH. The private
parent bundle carries the separate Keycloak-specific proposed namespace/watch
extension. A CNPG-scoped proposal may retain the existing before-plus-target
case, which discloses `restorationRequired: true` and requires exact restoration.
These are offline preparation choices and perform no controller operation.

## Fully bound request schema

`parse_binding` converts a separate closed JSON schema into immutable typed
`Binding`, `Cluster`, `Resource`, `Artifact`, `Tool`, `VolumePath` and
`CreationFence` objects.
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
| `pausedWorkload`, `sourceStores`, `previousReplicas` | Explicit source Deployment, distinct source-namespace StatefulSet/Deployment/CNPG Cluster UID/path identities and recorded replicas |
| `watcher`, `watcherBefore`, `watcherDuring` | Exact controller identity and ordered watch lists; during equals before or before plus the target namespace |
| `watcherContainerIndex`, `watcherEnvironmentIndex` | Optional explicit integer positions, default 0; reads/tests must match WATCH_NAMESPACE exactly or refuse |
| `protectedPaths`, `targetPaths` | Source and target `{pvUid,nodeUid,path}` records; each protected source PV and each owned target PV requires exactly one mapping |
| `creationFence` | Required external `CreationFence` reference for cleanup; required null for capture/restore/resume |
| `cleanupCensus` | Required nonempty fully bound Resource list for cleanup, including all owned deletion roots and generated children; required null for capture/restore/resume |
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

Every protected source PV and owned target PV requires one exact node UID/path
binding, with no missing or duplicate mappings. Source and target paths are
bounded to `/var/openebs/local/`. Target paths cannot equal, contain or be
contained by any protected source path. Bound source volume records accompany
every dependent stage protocol request (typed field names serialize as
`pv_uid`, `node_uid`, `path`, consistently with target volume records). Target
paths are checked independently for absence. Other storage providers and larger unqualified input profiles are
refused. Current capacity, extraction, images, stdin and mount layout remain
separate runtime gates.

A `CreationFence` is `{resource, namespaceUid, qualificationSha256}`. `resource`
is the exact cluster-scoped `admissionregistration.k8s.io/v1`
`ValidatingAdmissionPolicyBinding` Resource, including UID/resourceVersion;
`namespaceUid` must equal the owned target Namespace UID. The independently
assessed qualification digest must cover the referenced admission policy,
binding/dependencies, namespace selection, fail-closed behavior and sustained
creation prevention for that exact namespace UID throughout cleanup. Merely
observing a binding does not prove those properties. The future qualified
stage driver must independently observe the full enforced fence and return the
exact reference, namespace UID, `creation-blocked` observation and `observedAt`.
The adapter never creates, installs or qualifies a fence. A NetworkPolicy does
not fence API object creation and is refused as this reference. The fence
reference is never an owned target; cluster-scoped owned cleanup resources are
restricted to Namespace and PersistentVolume. Recognizing the admission
reference adds no admission-control deletion authority.

`cleanupCensus` separates the complete read-only object census from
`ownedTargets`, the existing direct-deletion roots. Both lists require distinct
UIDs and API paths, and each deletion root must appear with its exact identity
in the census. The census also recognizes Pod, ConfigMap, ServiceAccount,
ReplicaSet, ControllerRevision, Job, CronJob, Role, RoleBinding, Endpoints,
EndpointSlice, VolumeSnapshot, VolumeSnapshotContent, Lease and
PodDisruptionBudget. These additional kinds receive exact GETs and independent
absence checks only. They are refused in
`ownedTargets`; recognizing a kind adds no direct DELETE authority. Census
objects must belong to the target namespace, except its bound Namespace,
PersistentVolumes and generated VolumeSnapshotContents. Every census PV must
also be an owned deletion root, with the required exact node/path mapping and
independent path-absence check. Source UID/path
exclusions and watcher/fence identity exclusions apply to the census too.
Cluster controls and the external fence are never deletion roots.

The qualified stage driver's complete API discovery/census must include every
object in that target scope, including generated children and cluster-scoped
storage dependents. An unsupported kind, omitted object, extra object or changed
identity fails completeness before any deletion. The adapter compares the
observed inventory against the whole census, independent of list order, then
reads every bound census identity and later requires exact independent NotFound
for every census object. A driver must never filter unknown resources from its
observation to fit this bounded profile. More than 64 census objects or more than
256 total requests requires separate qualification and is refused here.

The proposal input schema remains unchanged: it contains no source paths or
fence identity or census. Its dependent cleanup shape has `creationFence: null`,
`cleanupCensus: null`, and its `qualifiedNamespaceCreationFence` unresolved gate
remains null. Typed complete source volume mappings, a complete cleanup census
and an independently qualified creation fence are
additional future fully bound input dependencies, never fabricated proposal
facts.

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
approved driver must enforce an absolute deadline for every request, clamped to
the smaller of its 15-second timeout and remaining policy validity at dispatch,
maximum body/error bytes, no redirects, no retries and bounded argv/stdin.
The runner checks elapsed time after a returned call, but cannot interrupt a
hung injected Python function; enforced cancellation is a required driver gate.
Local cancellation does not roll back a server-side write. Timeout or expired
validity after dispatch leaves the effect uncertain and requires independent
incident-owner assessment; the adapter issues no compensating operation.
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
   the process exited zero. Both capture and restore re-read every protected
   source UID/resourceVersion after their stages before primitive completion.
   Capture's separately approved controlled source changes are outside this
   primitive and cannot justify skipping its final preservation checks.
3. Resume first requires verified restore/isolation, healthy stores with observed
   changed runtime identity, a fresh clean drain and a still-paused exact source
   Deployment. Drain must have zero pending/in-flight/missing/duplicate work and
   an unchanged stream digest; age and policy validity are checked again after
   the create-only request intent is persisted, immediately before PATCH.
   JSON Patch tests UID, resourceVersion and replicas 0, then restores only the
   recorded previous replica count. A subsequent GET must show matching desired,
   updated, ready and available replicas and observed generation. A sent PATCH
   without that reconciliation remains uncertain and requires the incident owner.
4. Cleanup independently observes the bound external creation fence immediately
   before the complete census inventory, then checks every census identity
   and refreshes the same qualified fence immediately before the deletion loop.
   The census includes direct roots and generated children;
   only the separately authorized `ownedTargets` roots receive direct DELETEs.
   It requires another independent observation of the unchanged fence immediately
   before Namespace DELETE. Failed, unknown, changed or older-than-30-second
   fence observations stop the sequence. Freshness is checked again after intent
   persistence before inventory or each DELETE; no unrelated backend call may
   intervene between the fence and inventory, first DELETE or Namespace DELETE. The external
   qualification must establish continuous creation prevention between checks.
   Cleanup deletes only authorized roots with both UID and resourceVersion
   preconditions; Namespace is last. Native HTTP 200/202 DELETE acceptance may
   contain a standard metav1.Status or the matching resource. Every supplied
   name, UID, group and kind must match; a returned resource may have an updated
   resourceVersion while finalizers complete. Acceptance does not establish
   deletion. Cleanup then requires fresh exact
   NotFound reads for every census object and explicit same-node path-not-found
   observations. A 403, timeout, remaining PV or unobserved host path cannot mean
   absence. When the scope was extended, it restores the watcher with exact
   UID/resourceVersion/value tests. Equal before/during scopes omit the watcher
   PATCH. Both cases independently read back the original ordered scope and
   recheck sources; the no-change case also retains the exact resourceVersion.
   A watcher included in protectedSources must retain its UID/path and exact
   restored watch scope; its authorized PATCH may legitimately change its
   resourceVersion. All other protected source UID/resourceVersions remain exact.

Every protected, target, paused-workload, resume and watcher identity response
must have absent or null `metadata.deletionTimestamp`. A non-null timestamp
means the object is terminating and stops the primitive. Only a native DELETE
acceptance resource response may carry a valid terminating timestamp; it still
requires the later independent exact absence check.

Native response fixtures follow the primary Kubernetes v0.34 sources:
[metav1.Status/StatusDetails](https://github.com/kubernetes/apimachinery/blob/v0.34.0/pkg/apis/meta/v1/types.go#L692),
[NewNotFound](https://github.com/kubernetes/apimachinery/blob/v0.34.0/pkg/api/errors/errors.go#L133),
[DELETE handler](https://github.com/kubernetes/apiserver/blob/v0.34.0/pkg/endpoints/handlers/delete.go#L163)
and [registry finalizeDelete](https://github.com/kubernetes/apiserver/blob/v0.34.0/pkg/registry/generic/registry/store.go#L1287).
Status optional metadata, message, code and details are validated by their
native shapes. Grouped NotFound requires the exact requested API group,
resource plural and name, allowing standard optional Status fields; wrong
groups, malformed responses, forbidden results and timeouts still refuse.

`receipts.create(key, bytes)` must be create-only. `LocalReceipts` implements
0700-directory/0600-file no-follow descriptor custody, exclusive creation and
fsync. A run writes an intent before any backend call and an intent/result per
request; only response hashes and fixed semantic states are recorded. Existing
records are preserved. Reusing a scope/operation refuses before another backend
request. Failure retains partial effects, pending intents and earlier results.
No automatic retry, rollback, resume, target cleanup or multipart abort follows.
Policy validity is rechecked after every request-intent write immediately before
dispatch, so a slow persistence operation cannot carry a request past expiry.

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
| Unsafe input | `test_expired_future_and_24_hour_overrun_refuse_before_backend`, `test_source_volume_mapping_is_typed_complete_and_one_to_one`, `test_target_volume_equal_ancestor_and_descendant_source_paths_refuse`, `test_source_stores_reject_namespace_duplicate_and_unrelated_resources`, `test_noncanonical_numeric_ports_refuse_before_endpoint_identity_comparison` |
| Capture/restore boundary | `test_authentication_consumes_the_exact_independently_verified_readback_bytes`, `test_immutable_restore_input_corruption_stops_before_restore`, `test_process_success_does_not_override_semantic_failure`, `test_restore_isolation_source_mount_host_mount_and_network_failures_stop` |
| Source resume | `test_resume_dirty_stale_future_or_changed_stream_drain_leaves_pause`, `test_slow_resume_intent_cannot_dispatch_after_clean_drain_ages`, `test_resume_only_patches_recorded_identity_zero_and_prior_replicas`, `test_resume_requires_actual_reconciliation_after_patch` |
| Target cleanup | `test_failed_unknown_stale_or_changed_fence_stops_before_destructive_request`, `test_creation_fence_and_other_cluster_controls_cannot_be_owned_targets`, `test_native_delete_accepts_v034_status_200_202_and_resource_shapes`, `test_grouped_native_notfound_accepts_standard_optional_fields`, `test_cleanup_protected_watcher_allows_restored_rv_and_requires_exact_original_scope`, `test_final_source_drift_after_restore_and_cleanup_prevents_completion` |

The revised suite preserves all 76 revision-2 test methods and adds adversarial
regressions for the complete review correction set. Full-census tests include
every newly recognized generated kind, independent child identity/absence,
unknown/extra/missing inventory and direct-deletion-scope refusal. Terminating
identity and capture post-stage drift tests require failure without completion.
Distinct source/target cluster fixtures cover all four operations, including
target GET/DELETE and source preservation, replica and watcher routes; they fail
if target GETs are forced to the source endpoint. Near-expiry assertions require
backend deadlines within remaining policy validity. Slow-intent tests establish
the post-persistence policy/drain/fence checks. Post-restore and post-cleanup
source UID/resourceVersion drift must fail without a completion receipt; those
regressions fail if all final source-preservation requests are removed.
