---
title: '4.26: Qualify Rancher and the management migration'
type: 'chore'
epic: 4
story: 26
created: '2026-10-01'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '98436a4cb884f8d11b4b5f3e6ae70e95ce687463'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/specs/spec-kubesphere-to-rancher/SPEC.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The approved replacement lacks an ownership census, retirement rehearsal and supported target. Installation tests do not establish safe removal.

**Approach:** Execute Story 4.26 qualification: collect protected native evidence, classify management dependencies, qualify exact Rancher/K3s/tool identities and independently usable access, rehearse native retirement in isolation, and deliver reviewable retirement and management plans.

**Decision:** Evaluate the Administrator's host `192.168.1.30` first; VM capability, capacity, endpoint and cost require qualification.

## Boundaries & Constraints

**Always:** Preserve cluster/namespace/workload/PVC identities, bindings and shared dependencies. Encrypt private exports outside Git; commit sanitized evidence. Revalidate skew, identity and currency. Preserve Administrator/deputy separation, MFA and independent grant/revocation lineage; missing authority fails closed. Distinguish observation, proposal, approval and acceptance.

**Never:** Mutate production, uninstall KubeSphere, grant roles, provision/install Rancher, upgrade Kubernetes, overwrite recovery evidence or the hashed upgrade proposal, introduce Fleet application writers, or import production credentials/data into a fixture. Namespace/CRD wildcards and blanket finalizer removal cannot constitute retirement scope. Qualification does not authorize 4.27 or open 4.1's gate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Census | Exact native context and compatible tools | Timestamped UID/version/ownership/storage census; encrypted private exports and sanitized digest ledger | Failed discovery or inaccessible API is an explicit coverage gap |
| Ownership | Catalog entries, installed controllers, consumers and finalizers | Distinguish available extensions from installations; classify each capability and deletion effect | Unknown owner/consumer/propagation blocks retirement acceptance |
| Target | Version-specific hosting/import matrices and stable releases | Exact separate management/workload/chart/image/tool/license identities | Unsupported, prerelease or security-unqualified identity remains unaccepted |
| Rehearsal | Isolated representative inventory and native procedure | Exact proposed allowlist, preservation assertions and cleanup evidence | Source reachability, drift or unexpected deletion stops the fixture |
| Authority | Native credentials and independent grant/revocation lineage | Authorized native access, unauthorized/public denial and scoped management-role proposal | Proxy-only access or missing/conflicting lineage fails closed |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md` — authoritative criteria.
- `eng/cluster-management/*.md` — existing contracts; add evidence-bound outputs.
- `eng/kubernetes-upgrade/prepare.py` — reuse private immutable-attempt/projection patterns; presence is not signature verification.
- `~/hexalith-upgrade-evidence/operations/story41-*.py` — adapt capture/fixture/recovery patterns; top-level actions prohibit direct imports.
- `~/hexalith-upgrade-evidence/tools/` — requalify retained kubectl/Helm/age/etcd identities.
- `~/hexalith-recovery-evidence/4-0/20261001t061620z/tools/` — reuse accepted signature/readback verification.
- `eng/kubernetes-upgrade/MAINTENANCE.md` — preserve digest; derive successor after retirement.

## Tasks & Acceptance

**Execution:**
- [x] `eng/cluster-management/qualify.py` — collect explicit-context read-only evidence: tools/runtime, chart/images/config, installed extensions, CRDs/instances, ownership/finalizers, admissions/API services, RBAC/routes/namespaces and persistent state; protect each attempt.
- [x] `eng/cluster-management/test_qualify.py` — test secret exclusion and failed discovery/ownership/pin/authority validation.
- [x] `_bmad-output/implementation-artifacts/evidence/epic-4/4-26/<attempt-id>/` — retain inventory, capability/disposition map, dated sources, current pins/licenses/tools and encrypted-export digests.
- [x] `eng/cluster-management/QUALIFICATION.md` — evidence native access/denial/custody, MFA/roles, independent authority lineage and fail-closed restoration; manufacture no grants/approvals.
- [x] `eng/cluster-management/rehearse.py` — rehearse isolated native uninstall with representative ownership/config and synthetic state; test propagation/finalizers, namespace/PVC preservation, license independence, stop/recovery and cleanup.
- [x] `eng/cluster-management/test_rehearse.py` — test source refusal, allowlist enforcement and unexpected deletion.
- [x] `eng/cluster-management/RETIRE-KUBESPHERE.md` — deliver ordered UID/resourceVersion-bound actions, allowlist/procedure digests, preservation assertions, currency/drift stops and 4.27 approval/recovery requirements.
- [x] `eng/cluster-management/RANCHER.md` and `README.md` — deliver concrete VM/OS/resource/storage/cost/private DNS/TLS/firewall/CA/backup/owner plan, scoped access and independent backup/restore tests; retain single-node/shared-host limitations and qualification → retirement → hop → Rancher → staging order.
- [x] `_bmad-output/implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md` and `sprint-status.yaml` — retain unmet criteria; complete only with mandatory evidence. Procurement may wait for 4.28.

**Acceptance Criteria:**
- Given qualification results, when assessed, then all eight criteria have traceable evidence or remain incomplete; proposals/checksums alone cannot pass.
- Given the proposed retirement scope, when reviewed, then exact ownership, native uninstall, propagation, workload preservation and recovery are demonstrated without licensed KubeSphere writes or production mutation.
- Given qualified plans, when handed to 4.28, then hosting/import support, tools/licenses, capacity, private/MFA access and independent recovery/authority are evidenced.

## Implementation Notes

Execution checkboxes above record implemented artifacts and executed bounded checks. They do **not** accept the original story criteria. The original story and sprint remain `in-progress`; its [eight-criterion ledger](4-26-qualify-rancher-and-the-management-migration.md#criterion-evidence-and-remaining-gates) lists the unmet evidence. No production mutation, grant, Rancher provisioning, upgrade, recovery overwrite or application Fleet writer occurred.

- Added `evidence.py` immutable owner-only private attempts/encrypt-before-write/projection digests, explicit native `qualify.py`, source-fenced `rehearse.py`, and twenty-one meaningful failure-boundary tests. Updated the qualification, retirement, Rancher and operations handoffs.
- `20261001t173902z-census`: Kubernetes1.34.9, direct `jpiquot@local` native TLS endpoint, 2,594 unique UIDs and zero API discovery/list failures; 23 namespaces, 27 PVCs, 28 PVs, 137 CRDs, eight mutating/twelve validating admission configurations. The superseded census with ten gaps is retained. Native authorized reads and credential-free CA-verified HTTP403 are observed; public denial, effective independent custody and maintenance authority remain unverified.
- `20261001t181200z-capability-review`: native Installed console plan confirmed separately from catalog; eight concrete capability cohorts propose replacements/retirement/native preservation with exact members, consumers and deletion-effect review. No approved disposition and no production removal actions.
- Dated source/review attempts retain Ranchercommunity2.15.2 chart/server/agent, K3s1.35.9+k3s1 and native workload1.35.9 candidates, separate hosting/import matrices, licenses, release bodies/digests and advisory ranges. Current official K3s release API at17:42:22Z reports `prerelease=false`; the earlier prerelease disagreement remains recorded. No separate channel-lag acceptance requirement was added.
- `20261001t181000z-public-verification`, `20261001t183042z-dependency-review`, `20261001t183444z-plan-render` and `20261001t184142z-dependency-images`: K3s/kubectl/kubeadm/kubelet published checksums match downloads; Helm archive and extracted binary match; fresh age/etcd archives and all retained binaries match. Backup/CRD charts match the version-specific index and render; proposed private-CA/single-replica Rancher values render. Concrete backup/kuberlr/server/shell and all eight bundled K3s registry manifest digests are retained. Relevant source licenses and66 Rancher advisory ranges are recorded. Signatures, transitive image-license/security and procedure/restore acceptance remain false.
- `20261001t180843z-rehearsal`: bounded synthetic native UID/resourceVersion/child-first/named-finalizer deletion, namespace/PVC/PV UID/binding/canary preservation and isolated cleanup passed. Zero-replica workload/synthetic finalizer limitations are explicit.
- `20261001t182403z-rehearsal`: actual core1.2.4/application4.2.1 and embedded-console1.2.0 all five deployments Ready; source API/etcd/SSH/HTTPS and external egress probes blocked; fresh credentials and different fixture UID. Native installed-plan deletion completed. Core `--no-hooks --wait --timeout90s` removed deployments but timed out, so retirement is unqualified. Cleanup removed node/network/volume, but its strict parser retained lowercase Docker not-found as unverified. The spelling fix preserves daemon/API-error refusal and its negative test passes. One diagnostic follow-up captures every served KubeSphere custom resource before/after without arbitrary finalizer edits.
- `20261001t184031z-rehearsal`: the single diagnostic follow-up also failed core uninstall, with604 before-state objects,317 expected retirements and437 after-state objects.151 expected retirements remain;17 chart CRs are Terminating with their exact finalizers (WorkspaceTemplate/system-workspace, custom ServiceAccount/ks-console, User/admin, five GlobalRoleBindings, seven GlobalRoles, Repository/extensions-museum, Extension/ks-console-embed). Controller removal preceded completion of those lifecycles.166 allowlisted objects disappeared, plus one unallowlisted completed extension Job Pod whose batch Job was omitted from the captured scope. Protected namespace/PV/PVC/synthetic-deployment metadata remained intact; post-core canary/authenticated application health is unverified. Cleanup passes explicit node/network/volume absence and fresh credential/image-alias removal. No finalizer stripping occurred.
- Final current helpers add Jobs/endpoints/storage-class snapshot coverage, refuse uncensused owners, and record/check an extension-specific expected set immediately after native InstallPlan deletion, before core removal. A negative test proves the later core allowlist cannot excuse an unexpected extension-phase core deletion. These guard changes have unit coverage only; the retained actual runtime remains failed/incomplete. Source ownership is unknown and this fixture approves no orphaning/reownership.
- [Closeout verification](evidence/epic-4/4-26/20261001t185518z-closeout/verification.json) binds executed and current procedure digests, exact allowlist/runbook hashes, retained failed evidence, local checksum/encryption/mode checks and subsequent explicit historical cleanup readback. The exact executed procedure bytes are retained encrypted from the matching interim staged snapshot. Twenty-one unit tests and staged/unstaged whitespace checks pass; off-node custody/signatures/authority and all original acceptance criteria remain unverified/incomplete.
- Host effective kubeadm/external-etcd/virtualization/reservations require the still-unanswered working SSH username/key location. Independent Administrator/deputy MFA/scopes, authenticated grant/revocation lineage, off-node export decryption/signatures and quarantined recovery remain externally dependent. No missing evidence is fabricated. VM placement/cost/DNS/CA decisions are unapproved; actual procurement/provisioning can wait for4.28.
- Historical `eng/kubernetes-upgrade/MAINTENANCE.md` SHA-256 remains `834be8225ca05175e7cbf7c6d4ee35324401c196002002269223b9000a97d8b5`. A post-retirement successor requires a fresh census/recovery point and its own approval.

The frozen matrix has named passing tests:

| Frozen row | Passing tests | Executed evidence/limit |
| --- | --- | --- |
| Census | `ProjectionTests.test_secret_and_configuration_values_never_enter_projection`; `ProjectionTests.test_terminating_custom_resource_projects_timestamp_and_named_finalizer_only`; `CensusTests.test_failed_discovery_is_not_an_empty_accepted_census`; `CensusTests.test_supported_skew_and_prerelease_fail`; `CustodyTests.test_private_attempts_immutable_and_no_recovery_overwrite`; `CustodyTests.test_symlink_evidence_custody_refused` | Completed native UID census; encrypted private export/header/digest/mode checks; independent custody/host coverage still incomplete |
| Ownership | `ProjectionTests.test_ownership_storage_and_installed_catalog_distinction`; `ProjectionTests.test_unknown_owner_consumer_and_deletion_effects_block_retirement`; `RehearsalTests.test_unknown_cascade_blocks_and_child_first_allows_explicit_scope` | Installed plan distinct from catalog; concrete unapproved capability map; unknown consumers/propagation block production scope |
| Target | `PinAuthorityTests.test_unqualified_tag_hosting_security_and_tools_fail_closed`; `PinAuthorityTests.test_github_prerelease_and_stale_review_rejected` | Exact dated candidates/matrices/artifact/tool/license records; no unsupported/prerelease/security-unqualified acceptance |
| Rehearsal | `RehearsalTests.test_source_endpoint_uid_reachability_and_credentials_refused`; `RehearsalTests.test_wildcards_missing_resources_protected_namespaces_and_claims_refused`; `RehearsalTests.test_named_finalizer_only_and_drift_stop`; `RehearsalTests.test_unexpected_deletion_incomplete_deletion_or_uid_binding_change_stops`; `RehearsalTests.test_daemon_failure_does_not_prove_cleanup_or_source_refusal`; `RehearsalTests.test_extension_phase_cannot_use_later_core_scope_to_allow_unexpected_deletion` | Synthetic preservation passes; real native core uninstall failure retained; source reachability, drift, unexpected phase deletion and Docker API errors fail closed |
| Authority | `CensusTests.test_proxy_credentials_are_refused_before_discovery`; `CensusTests.test_endpoint_userinfo_query_and_fragment_cannot_disclose_credentials`; `PinAuthorityTests.test_missing_gapped_conflicting_or_expanded_authority_fails_closed`; `PinAuthorityTests.test_post_cut_revocation_survives_source_loss` | Authorized native reads/credential-free403 observed; tests exercise metadata policy only, never authenticate signatures/grants or demonstrate actual MFA/restore |

Post-review verification supersedes the interim artifact hashes above without changing any immutable attempt:

- `20261001t195141z-census`: patched read-only collector captured2,593 unique UIDs, nine all-state Helm releases and283 encrypted requests. It adds27 previously omitted KubeSphere-marked identities to unresolved ownership review, including preserved namespaces/RBAC. It finalized `failed-closed` with70 missing-identity coverage entries: three ComponentStatus objects, one node/65 pod metric views and the built-in Calico profile. The four targeted read-only requests in `20261001t195456z-native-identities` retain the actual absent UID/resourceVersion metadata. These successful views are not approved deletion identities or malformed transport responses; do not infer complete projection from request success. All80 protected namespace/PV/PVC/storage-class identities and previously projected bindings/properties match the earlier observation; newly projected properties do not establish prior-state equality.
- `20261001t195601z-rehearsal`: fresh source-fenced standalone native fixture passes actual delete/recreate stale-UID and mutate-after-snapshot stale-resourceVersion cases. Each server response is409/Conflict and current full content/UID remains unchanged. Child-first/named synthetic finalizer removal, protected storage/lifecycle assertions and canary readback pass. Cleanup explicitly proves node/network/volume/owned-image-alias absence and fresh credential removal. No actual KubeSphere runtime rerun occurred; its finalizer/phase-propagation failure remains unqualified.
- Root verification passes all45 tests across `test_qualify.py`, `test_rehearse.py`, `test_evidence.py` and `test_cleanup.py`; new tests cover full collector pagination/nonpreferred CRDs, malformed responses/allocated preflight receipts, Helm states/pages, destination symlinks, Orphan propagation, protected-property drift, mutation ownership, bounded cleanup and native request/race behavior. Every frozen matrix row retains passing named coverage. The [post-review verification](evidence/epic-4/4-26/20261001t200342z-post-review/verification.json) binds current artifact/procedure digests and limits.
- Exact standalone execution used procedure SHA-256 `6277bf2ceb290e276405780016968eb83b0a3bd1e36694f35e311262faf94f19`; current SHA-256 `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0` adds only a three-line malformed-mount schema guard. Native deletion/preservation bytes are unchanged. Current-code tests and replay against retained synthetic Docker metadata pass; the full fixture was not rerun after this edit. The audit preserves both exact revisions and corrects the interpretation of an initially misnamed encrypted export without replacing it.
- All27 review findings have individual verdicts; G1–G17 corrections are implemented and verified. The implementation worker patched collector/rehearsal; root patched custody/cleanup after agreeing file ownership. G18 is [deferred](deferred-work.md) under the agent-context rule; the operations handoff restates the existing accepted4.2/4.3 staging prerequisites. No original criterion is accepted by these fixes.
- Build/story/sprint remain `in-progress`. Step05 completion/commit is pending operational acceptance: the workflow requires every acceptance criterion satisfied, while representative retirement, effective host/native maintenance access, owner decisions, authentic authority/MFA and independent custody/recovery remain unresolved. No frozen intent or recovery proof was modified.

Additional passing matrix coverage: Census — `CensusTests.test_collect_pagination_nonpreferred_crd_and_empty_discovery_coverage`, `CensusTests.test_all_helm_states_paginate_beyond_default_limit`, `CensusTests.test_malformed_successful_lists_are_coverage_failures`, `CensusTests.test_allocated_tool_and_shape_failures_finalize_closed_criteria`, all `DestinationTests`; Ownership — `ProjectionTests.test_explicit_management_keys_and_finalizers_classify_native_objects`, `ProjectionTests.test_secret_and_config_consumers_project_only_reference_identities`, `ProjectionTests.test_ingress_backends_and_gateway_listeners_preserve_sanitized_consumers`; Rehearsal — `RehearsalTests.test_orphan_propagation_retains_children_and_stops_nested_cascade`, `RehearsalTests.test_protected_storage_properties_finalizers_and_deletion_timestamps_cannot_drift`, all `FixtureBoundaryTests` and `CleanupTests`. Target/Authority tests above remain passing and their operational limitations remain unchanged.

## Spec Change Log

## Review Triage Log

All three layers reported before triage. Blind and edge reviewers were fresh context-free agents; the verification-gap layer reused the earlier source researcher because the runtime rejected fresh threads. No layer was skipped. The blind finding-floor calculation is review metadata, not a defect. Each finding below has a separate verdict before grouping; source/callers were traced for ordinary findings, and the two gap findings are pre-verified under the workflow.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B1 category symlink | high | `Attempt` checked only root symlinks; an owner-only category symlink can direct private writes into Git/recovery custody. | patch G1 |
| B2 management detection | medium | Classification checked label values only and omitted explicit KubeSphere label keys/finalizers, including preserved namespaces in the census. | patch G2 |
| B3 configuration consumers | medium | Pod projection reads Secret `name` instead of `secretName` and has no env/envFrom/projected/image-pull reference extraction. Consumers disappear from retirement review. | patch G3 |
| B4 route schemas | medium | Ingress service/default/path backends and Gateway listener hostnames do not use the HTTPRoute fields currently read. | patch G4 |
| B5 malformed lists | medium | Successfully parsed `{}` or non-list `items` breaks both list loops without failed coverage; non-object JSON can escape through attribute errors. | patch G5 |
| B6 tool preflight receipt | medium | Allocated attempt precedes subprocess version probes, which are outside the failure/finalization path; tool execution errors leave no published receipt. | patch G6 |
| B7 Orphan propagation | medium | Closure starts from every delete action irrespective of propagation, so an Orphan parent incorrectly requires retained children in removal scope. | patch G7 |
| B8 storage preservation | high | `assert_preserved` compares UID/binding only; projected PV reclaim policy/storage class can change without stopping retirement. | patch G8 |
| B9 early volume coverage | medium | Volumes are captured only late in startup; early failure leaves an empty list whose vacuous `all` reports verified absence. | patch G9 |
| B10 image cleanup | medium | Alias removal exits are recorded but ignored by `passed`, with no alias absence inspection. | patch G10 |
| V1 collector regression gap | medium | Neither collector test reaches pagination or non-preferred served CRD fallback; removal of either branch would retain passing tests. | patch G11 |
| V2 native DELETE regression gap | high | Helper drift tests never send native DELETE; happy-path execution does not prove that the server rejects stale UID/resourceVersion at the request boundary. | patch G12 |
| V3 malformed-list coverage | medium | Successful JSON decoding records observed coverage before malformed `items` silently truncates collection; confirmed in both loops. | patch G5 |
| E1 category symlink | high | Complete destination ancestry is unchecked, so root validation does not prevent category symlink redirection. | patch G1 |
| E2 queued protected deletion | high | A retained object can acquire `deletionTimestamp` with unchanged UID/binding and pass preservation, despite pending namespace/storage deletion. | patch G8 |
| E3 Secret volume | medium | Native Secret volumes use `secretName`; existing projection reads `name` and omits the mounted dependency. | patch G3 |
| E4 Ingress/Gateway | medium | Kind-specific route fields are not read, omitting backend/hostname dependencies. | patch G4 |
| E5 malformed-list coverage | medium | A code-zero JSON response lacking a list of objects currently stops collection with observed coverage. | patch G5 |
| E6 Helm states | medium | `helm list --all-namespaces` does not include all pending/uninstalling states without `--all`; those release exports are omitted. | patch G13 |
| E7 Helm page limit | medium | Retained Helm help confirms the default 256 limit; no pagination exists, so later release identities/exports disappear. | patch G13 |
| E8 side effect before encryption | medium | Network creation returns successfully before `run` encrypts output; encryption failure prevents the caller's ownership flag from being set and skips network cleanup. | patch G14 |
| E9 existing fixture alias | high | Generated tags are not checked for absence before tag/build, so unrelated local aliases can be overwritten and removed during cleanup. | patch G15 |
| E10 image cleanup | medium | Cleanup's success conjunction excludes image removal errors and alias readback. | patch G10 |
| E11 source capture race | medium | Inventory is read once for hashing and again for parsing; a concurrent edit can disconnect the fencing input from its recorded digest. | patch G16 |
| E12 unbounded cleanup inspection | medium | Docker absence inspections lack timeouts, preventing receipt/summary finalization if the daemon stalls. | patch G17 |
| E13 staging context gate deletion | medium | The refreshed agent context removed the explicit accepted exposure-closure/runner-relocation gate before staging; the new ordered sequence omits that condition although upstream requirements retain it. The fix edits agent context. | defer G18 |
| E14 protected-custody claim | high | Root-only symlink validation does not enforce the claim at the category destination where private attempts are created. | patch G1 |

Survivors group by exact root cause: G1=B1/E1/E14; G2=B2; G3=B3/E3; G4=B4/E4; G5=B5/V3/E5; G6=B6; G7=B7; G8=B8/E2 (incomplete preservation comparison); G9=B9; G10=B10/E10; G11=V1; G12=V2; G13=E6/E7 (incomplete Helm listing); G14=E8; G15=E9; G16=E11; G17=E12; G18=E13. G1–G17 are bounded implementation corrections/tests with no new public surface and demonstrated inputs; approved intent already settles the behavior. G18 is deferred under the workflow's agent-context rule. No intent-gap or bad-spec entry requires loopback. The original implementation worker was re-engaged with the required patch message.

Resolution: G1–G17 patched; all45 tests pass. Fresh native409/Conflict/preservation/cleanup evidence addresses G12 at the actual request boundary. Fresh census preserves missing identities as explicit closed coverage; it does not erase prior captures or accept retirement. G18 appended to deferred work; generated agent context was not edited during patching.

## Design Notes

Prefer a separate VM on existing safe capacity. If node1 is a guest, use a sibling VM on its hypervisor. A different physical host reduces correlated outages but adds cost/connectivity work. Neither provides HA. Propose 4 vCPU/16 GiB RAM/80 GiB SSD; CPU/RAM follows Rancher's [small-tier guidance](https://ranchermanager.docs.rancher.com/v2.14/getting-started/installation-and-upgrade/installation-requirements/), disk is estimated. Node1 reports 32 CPUs/~126 GiB RAM; spare capacity/virtualization remain unverified. Available SSH keys were rejected for root; the operator's working login is pending. No allocation/procurement is approved.

## Verification

- `python3 -m unittest discover -s eng/cluster-management -p 'test_*.py'` — meaningful evidence/isolation failure cases pass.
- `git diff --check` — clean whitespace.
- Inspect digests, custody, isolation/cleanup, criterion evidence and preserved identities; retain failures/limitations.

## Current standalone procedure follow-up

The [20261001t204952z-rehearsal result](evidence/epic-4/4-26/20261001t204952z-rehearsal/native-result.json) executes the exact current `rehearse.py` SHA-256 `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0`, including the malformed-mount guard added after the previous run. All six source/external refusal probes, real stale-UID/resourceVersion409/Conflict and unchanged-content assertions, child-first/named synthetic finalizer deletion, ten-before/eight-after exact preservation and canary checks pass. [Cleanup](evidence/epic-4/4-26/20261001t204952z-rehearsal/cleanup.json) explicitly proves node/network/owned-volume/image-alias and fresh-credential absence. Its 102 encrypted exports use the existing **synthetic fixture recipient**; its filename/comment establishes no Administrator custody, independent decryption or off-node acceptance. No production credentials/data were imported.

[Current verification](evidence/epic-4/4-26/20261001t205700z-current-procedure/verification.json) binds exact encrypted executed procedure bytes, the current artifacts, unit/local integrity checks and unaccepted limits. Historical attempts and frozen intent remain intact. Only the standalone current-byte execution limitation is superseded: actual KubeSphere runtime/uninstall was not rerun; finalizers, phase propagation, representative configuration/authenticated workload/recovery, effective host/access, MFA/authority and independent custody remain unresolved. All eight criteria and story/sprint remain `in-progress`; no gate opens.

## User-authorized access follow-up

The user supplied SSH username `jpiquot`, approved creation of evidence, confirmed the discovered local key and requested retrieval of the host key. [Conversation authorization](evidence/epic-4/4-26/20261001t205847z-native-access/authorization.json) is recorded without manufacturing authenticated grants, MFA or acceptance. [Native authority observations](evidence/epic-4/4-26/20261001t210315z-native-authority/native-access.json) verify the unchanged direct TLS cluster UID, authenticated `jpiquot`, eight sampled allowed SelfSubjectAccessReview decisions, private credential-free HTTP 403 and invalid-bearer HTTP 401. These checks changed no persistent resource or role and establish no distinct authenticated unauthorized/public denial, current signed lineage or independent custody.

The observed ED25519 host fingerprint is `SHA256:cC09u9n08cO/+GKfx4TL6xMuFHQUiQXm3UFaF3TWFGw`, retrieved on first use at the user's request. SSH requires the same exact key afterward; independent host trust remains unqualified. The local `hexalith-kube-c1` key and two discovered Windows identities did not authenticate. [Final diagnostics](evidence/epic-4/4-26/20261001t210432z-ssh-identity-check/host-access.json) explicitly confirm offering and rejection of both Windows public keys; earlier memory-file attempts make no offering claim. Temporary operational key copies were removed. Host facts/capacity remain unavailable without an authorized key. Sensitive command output uses the retained production census recipient, encrypted outside Git, with off-node decryption/signatures unverified. Frozen intent, historical evidence, procedure code and story/sprint statuses remain unchanged.

[Follow-up verification](evidence/epic-4/4-26/20261001t211014z-qualification-followup/verification.json) binds the current handoff after these additions, local evidence integrity, actual decrypted synthetic unit-test output and all five frozen matrix rows. The full baseline diff is retained in a unique system-temporary file; structured evidence was parsed and the current handoff diff inspected. All 45 executed tests passed without skips. This is implementation verification; operational acceptance remains incomplete and Build stays at Step 3.

## Resumed implementation verification

On 2026-10-02, bounded fixture-schema corrections reject non-object or non-string source identities before allocation and route malformed successful Docker/native responses through sanitized failure receipts, diagnostic refusal and cleanup/summary finalization. `FixtureBoundaryTests.test_malformed_source_identity_refused_before_fixture_allocation`, `FixtureBoundaryTests.test_malformed_successful_docker_image_schema_finalizes_closed` and `FixtureBoundaryTests.test_malformed_native_diagnostics_cannot_interrupt_failure_receipt_or_cleanup` pass with all prior cases: 48 executed tests without skips. `git diff --check` passes.

The procedure SHA-256 changes from `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0` to `9f84f36382efecec137f2dad936421c12e48e6afbbb956330470e12df169716b`. The new revision has unit verification only; no full fixture rerun followed it. The historical standalone attempt proves only its recorded prior procedure bytes, and the failed actual KubeSphere retirement/finalizer/phase-propagation evidence remains unchanged. No native deletion/preservation semantics, frozen intent, recovery evidence or hashed upgrade proposal changed. All eight original criteria and story/sprint remain `in-progress`; operational acceptance keeps Build at Step 3.

[Resume verification](evidence/epic-4/4-26/20261002t051917z-resume-verification/verification.json) records the root's independent 48-case run without skips, passing coverage for all five frozen matrix rows, inspection of the resumed diff, and local integrity of 31 prior projection attempts and 1,539 encrypted exports. It binds the current procedure and handoffs; local checks establish no independent decryption, signatures, authority or operational acceptance. The pending inputs are a working SSH identity and references to existing Administrator-approved capability/authority records; no response or approval is inferred.
