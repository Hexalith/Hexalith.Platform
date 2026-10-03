---
title: '4.26: Qualify Rancher and the management migration'
type: 'chore'
epic: 4
story: 26
created: '2026-10-01'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 2
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

**Decision (2026-10-03, Administrator):** The repository is public. Security-weakness narratives and host-access specifics go to encrypted private custody for the Administrator, and Git keeps only a neutral pointer. This covers which key or account reaches which privilege, authentication or sudo weaknesses, credential file locations, modes and validity, and the remediation status of those weaknesses. Future published projections omit them. Pushed history and committed immutable evidence attempts are not rewritten. Kubernetes RBAC facts from the census are not covered and stay public: which user or ServiceAccount holds which role, including the `jpiquot` binding finding.

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
- `eng/cluster-management/evidence.py` `Attempt` (`encrypt`, `finish`) — hold redacted text in a new private attempt; only ciphertext identities and a neutral pointer are published.
- Redaction recipient: the Administrator signing public key, age tag `8XlNQg`, which was the original production census recipient (see `targetRecipient` in the `20261003t063011z-recipient-reencryption` manifest). Confirm `8XlNQg` in every new header. Never use `hexalith-kube-c1` or the fixture recipient.
- Redaction sweep: every `eng/cluster-management/*.md`, the story file, this spec's non-frozen body (including Review Triage Log rows B25, B33, B40 and B41) and this story's `deferred-work.md` entries. In QUALIFICATION.md the targets are mainly the access follow-ups, web-login clarification, authorized host observations, kubeadm credential paragraphs, access-risk section and recipient observation. Keep non-sensitive facts: criterion status, required decisions, credential revocability, and versions and topology already in the census.

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
- [x] Redaction (2026-10-03 decision) — move the matching passages from the swept files into one encrypted private attempt and replace each with a neutral pointer: attempt ID, ciphertext SHA-256 and a neutral reason such as "criterion 5 remains unmet". Leave committed evidence attempts and history unchanged.
- [x] Review loop 3 patch groups G48–G50, G52, G54 and G56–G70 — apply each as its Review loop 3 row states, with tests for code changes. G50 and G52 apply to the redacted or neutral text.
- [ ] Review loop 4 patch groups G72–G74 and G76–G83 — apply each as its Review loop 4 row states, with tests for code changes.

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

### Review loop 2 (2026-10-03)

Review covered the full diff since `baseline_commit`, with code and handoffs first and evidence last. All three layers ran as fresh context-free agents and none was skipped. No earlier row matched a new finding's location and claim, so none is carried. Each verdict below came from reading the cited code, the committed census or the evidence. Verification-gap findings V4–V10 arrive pre-verified.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B11 console route scope | medium | Census `Ingress/kubesphere-system/kubesphere-console` (kube.hexalith.com → `ks-console`), its Certificate/CertificateRequest/Order and the manager Lease are outside `retirement_scope`'s 476 objects. `post_retirement_checks` checks only webhook/APIService/CRD-conversion backends. | patch G19 |
| B12 dynamic registration census | medium | `management_resource` ignores webhook backend namespaces. `validator.license.kubesphere.io` (backend `kubesphere-system`) is in retirement scope but absent from the 596 census capability entries. | patch G20 |
| B13 review-time resourceVersion | medium | `native_retire` deletes with the freshly read resourceVersion and never compares the planned entry. Runbook step 4 promises reviewed UID/resourceVersion actions with drift stops, which the rehearsal does not exercise. | patch G21 |
| B14 published plaintext digests | medium | `Attempt.encrypt` stores `plaintextSha256` and `finish()` publishes it, so short or partly predictable exports can be confirmed offline. The 2026-10-03 manifest deliberately omits these digests. | patch G22 |
| B15 pin/authority validators | medium | `validate_authority` links declared `sha256`/`previousSha256` values without hashing event content, so an edited event keeps a passing chain. The unwired sub-claim is V12, the TypeError sub-claim is E23, and stale pin wording joins G27. | patch G29 |
| B16 fence address coverage | maybe-false | Probes reach only the source endpoint host and 1.1.1.1. Reachability of the Docker bridge/host or another source address was not observed. Settle by probing those addresses from inside a fixture. Refusal conflation is E17. | defer G46 |
| B17 hard-coded evidence | medium | `cleanup.json` always writes `unrelatedDockerObjectsChanged: false`, `failure.json` asserts "source unchanged", and `validate_isolation` checks literals set just before. These are declarations recorded as observations, contrary to the frozen observation/proposal distinction. | patch G25 |
| B18 tracked kinds | medium | `NATIVE_RESOURCES` omits leases, ingresses, network policies, cronjobs, controller revisions, PDBs, HPAs, quotas and limit ranges, so their removal cannot fail `assert_phase`. | patch G23 |
| B19 exception tuple/PyYAML | low | `yaml.YAMLError`, `tarfile.TarError`, `ImportError` or `StopIteration` skip `failure.json`/diagnostics and `main()` prints a traceback; PyYAML is undocumented. `finally` still cleans up, and broadening the except is a direct correction. | patch G24 |
| B20 rehearsal tool provenance | medium | `--kubectl` is required but unused. The copied Helm and the age, kind and docker identities are never hashed or versioned in rehearsal evidence. The image-ID cross-check sub-claim is low and unpatched. | patch G26 |
| B21 stale current statements | medium | RETIRE-KUBESPHERE.md lines 24/34 and README lines 25/29 describe superseded procedures as current. Story criterion 1 cites the pre-G5 zero-failure census. | patch G27 |
| B22 joined words/numbers | low | Handoffs contain "Ranchercommunity2.15.2", "All45" and "in4.28". Readers meet these constantly, and the fix is a direct correction. | patch G28 |
| B23 epic context deletions | medium | The rewritten `epic-4-context.md` drops the 4.3 runner, registry/profile and Traefik requirements that `epics.md` still states. The fix edits agent context. | defer G45 |
| B24 relayed approvals | low | The 2026-10-03 kubeadm and re-encryption records cite only a coordinator relay, unlike `20261001t205847z` authorization evidence. | patch G30 |
| B25 host-access risk | high | The evidence is held in private custody: `passage-13.age` of attempt [`20261003t103215z-custody-redaction`](evidence/epic-4/4-26/20261003t103215z-custody-redaction/encrypted-exports.json) (ciphertext SHA-256 `a31309d5081dd49a0016180578817775fdd425627ae8f7da56b8914f549a1773`; criterion 5 remains unmet). The docs framed it only as export custody, and nothing tracked remediation. | patch G31 |
| B26 spec/sprint metadata | low | The spec status/body mismatch would be fixed by editing this spec, so that part is rejected. `sprint-status.yaml` `last_updated` changed from `MM-DD-YYYY HH:MM` to an ISO date. | patch G34 |
| B27 unrelated changes | false | `apphost.cs`, submodule and 4.1 amendments arrived in separate user commits such as `e7aee88`. The baseline range spans them, but this story did not make them. | reject |
| B28 evidence size/index | low | The JSON size is a repository cost rather than a functional defect. The criterion ledger links current and superseded attempts, and restructuring is more than a direct correction. | reject |
| B29 kubelet eviction | medium | RANCHER.md preserves the sampled `evictionHard` with only memory/pid. A partially set map drops kubelet's nodefs/imagefs defaults, and the storage-reservation plan omits that risk. | patch G32 |
| B30 anonymous success | low | A successful credential-free request records nothing, so anonymous read is visible only as missing fields. | patch G33 |
| B31 publication tests | medium | Same untested `finish()` projection as V4. | patch G37 |
| B32 locale-dependent values | low | One immutable attempt records a French `stat` value and C-date strings. Their meaning is intact, the problem is rarely met, and re-capture is not a direct correction. | reject |
| E15 orphan inside cascade | low | The defect is real only for mixed Orphan/Foreground allowlists. Every current planner action is Foreground, and the fix adds a guard. | reject |
| E16 escaped exceptions | low | Same defect as B19. | patch G24 |
| E17 refused probe | low | A `/dev/tcp` refusal exits 1 and records `blocked`. The probed source ports are listening services, and distinguishing refusal adds branching. | reject |
| E18 fixed resource list | medium | Same defect as B18. | patch G23 |
| E19 route health gate | medium | Same root cause as B11. | patch G19 |
| E20 Traefik routes | low | IngressRoute backends are not projected, but the five census routes sit in unrelated namespaces and consumer coverage is already marked for review. A new projection branch is more than a direct correction. | reject |
| E21 StatefulSet claim templates | low | Only scaled-to-zero StatefulSets lose PVC linkage, and PVCs are protected kinds that never enter scope. | reject |
| E22 proxy environment | low | No proxy variables are set here and the recorded census path was direct. The fix adds a guard. | reject |
| E23 pin validator types | false | Malformed input raises loudly instead of passing, and no non-test caller supplies such records. | reject |
| E24 authority principals | false | Grants still require Administrator approval and verified signatures. Intent restricts only deputy scope, and an unmatched revoke cannot expand authority. | reject |
| E25 probe member finalizer | low | The passing probe recorded the member finalizer, and a `None` read fails loudly. The added guard is not a direct correction. | reject |
| E26 extension installation | low | All ten actual-chart attempts reproduced `Installed`, and `kubesphere-runtime.json` retains the flag. The proposed stop guards an unobserved state. | reject |
| E27 CRD list cache | low | `diagnostics()` can cache the custom-resource list before the retirement baseline. A one-line reset is a direct correction. | patch G35 |
| E28 foregroundDeletion race | low | A Foreground delete can transiently add `foregroundDeletion`, and the synthetic replace then sets finalizers to `[]` instead of removing only the named hold. | patch G36 |
| E29 completed manager pods | low | The gate fails closed on any manager-namespace Pod. No attempt observed such residue, and exempting completed Pods needs a projection change. | reject |
| E30 other InstallPlan | false | The production census holds only `ks-console-embed`, and another single plan would stop loudly on unexpected removal. | reject |
| E31 plaintext digest | medium | Same defect as B14. | patch G22 |
| E32 sprint timestamp | low | Same sprint-format defect as B26. | patch G34 |
| E33 Story 4.1 context bullet | medium | The diff confirms deletion of the post-upgrade requirement from agent context. The fix edits agent context. | defer G45 |
| E34 runner context bullet | medium | The diff confirms deletion of the runner relocation requirements. The fix edits agent context. | defer G45 |
| E35 registry/profile context | medium | The diff confirms deletion of the registry immutability and profile-template requirements. The fix edits agent context. | defer G45 |
| E36 recovery/license claims | low | Handoffs already state that no restore was tested. The only overclaim is this spec's task wording, which cannot be edited. | reject |
| V4 publication projection | medium | Pre-verified: copying all private files or disabling publication still passes all 60 tests. | patch G37 |
| V5 encryption refusal | medium | Pre-verified: disabling the age exit/header check passes all tests. | patch G38 |
| V6 access fields | medium | Pre-verified: forcing denial on any HTTP error or on unreachability passes all tests. | patch G39 |
| V7 failed-closed coverage | medium | Pre-verified: removing the non-observed coverage check passes all tests. | patch G40 |
| V8 retained CRD guard | medium | Pre-verified: disabling `retained-crd-changed` passes all tests. | patch G41 |
| V9 tenant-sync gate | low | Pre-verified: disabling the gate passes all tests. | patch G42 |
| V10 attempt id/custody | medium | Pre-verified: removing either refusal passes all tests. | patch G43 |
| V11 real home in test | medium | `test_private_attempts_immutable_and_no_recovery_overwrite` uses the real `Path.home()`. A regressed recovery check would create directories in the actual `~/hexalith-recovery-evidence`. | patch G44 |
| V12 uncalled validators | low | Validators are test-only policy models, and collected records already mark pins/authority unaccepted. Wiring them adds CLI surface. | reject |

Groups by shared root cause:

| Group | Members |
| --- | --- |
| G19 | B11, E19 (route consumers of retired Services) |
| G20 | B12 |
| G21 | B13 |
| G22 | B14, E31 |
| G23 | B18, E18 |
| G24 | B19, E16 |
| G25 | B17 |
| G26 | B20 |
| G27 | B21, plus the stale pin wording from B15 |
| G28 | B22 |
| G29 | B15 |
| G30 | B24 |
| G31 | B25 |
| G32 | B29 |
| G33 | B30 |
| G34 | B26, E32 |
| G35 | E27 |
| G36 | E28 |
| G37 | V4, B31 |
| G38–G44 | V5–V11, one each |
| G45 | B23, E33, E34, E35 |
| G46 | B16 |

No group needs an intent or spec change. G19–G44 are bounded code, test and handoff corrections that add no public surface, and approved intent already settles the behavior they need. G45 is deferred under the agent-context rule. G46 is deferred as unverified: medium if true, settled by probing the bridge/host and other source addresses from inside a fixture.

Resolution: G19–G44 are patched, and G45/G46 are appended to [deferred work](deferred-work.md). Two code corrections were completed during resolution:

- **G19.** Besides the stale-route health check, `dependency_plan` now refuses any out-of-scope Ingress or HTTPRoute that sends traffic to a retired Service (`route-consumer-of-retired-service-outside-scope`). On the committed census it refuses on `Ingress/kubesphere-system/kubesphere-console`. Excluding that route, it reproduces 476 objects in 17 phases.
- **G21.** `native_retire` compares each fresh read with the root's reviewed allowlist entry. A changed resourceVersion proceeds only when the sanitized reviewed projection is otherwise identical, and any other drift stops with `reviewed-entry-drift-before-native-delete`. The executed `42e8e6ef…` run carried a newer resourceVersion than planned for 13 of 178 requests, so a rerun may surface reviewed-field changes made by controllers.

All 76 tests pass without skips, and `git diff --check` is clean. Disabling any single guard from G19–G23, G25, G26, G29, G33 or G35–G43 makes at least one test fail. Final `rehearse.py` SHA-256 `1ac666c06c93ecb619e737cce198204f8a8d4644a9e1382aa3e626b2a1d6d2f9` supersedes the interim `1e723b75…` cited in the dependency-first follow-up. These bytes were later executed in the [current-procedure fixture rerun](#current-procedure-fixture-rerun-2026-10-03). No production read or mutation, grant, Rancher action or frozen-intent change occurred, and all eight criteria remain incomplete.

### Review loop 3 (2026-10-03)

The review covered the full diff since `baseline_commit`, with code and handoffs first and evidence last. All three layers ran as fresh context-free agents, and none was skipped. A finding whose location and claim match an earlier row, where the code still reads as that row describes, is marked `carried`; it keeps the earlier verdict and route and is not patched or deferred again. Verification-gap findings V13–V23 arrive pre-verified. Every other verdict comes from reading the cited code, the committed census, the local Docker state or the repository settings.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B33 public repository exposure | high | `gh repo view` reports `Hexalith/Hexalith.Platform` as `PUBLIC`. The 2026-10-03 commit `c3c473a` is pushed (`origin/main` = HEAD). It publishes access-weakness and credential observations, now in the same private passage (`passage-13.age`), and census inventories with Secret names, hostnames and image versions. The frozen intent says to commit sanitized evidence but does not say whether security-weakness narratives and production maps may go into a public repository. | intent_gap G47 |
| B34 historical plaintext digests | low | Pre-G22 attempts still publish `plaintextSha256`. The guessable short outputs (`whoami`, `stat`, sudo exit) are already stated in the projections and prose, so confirming them reveals nothing new. High-entropy exports cannot be brute-forced, and scrubbing immutable attempts is not a direct correction. | reject |
| B35 recipient unrecorded | medium | `Attempt.encrypt` and `finish()` record no recipient stanza, so Git readers cannot check the `8XlNQg`/`yYjuKA`/`bHCI3g` tags the handoffs cite. The 12 wrong-recipient attempts show the state occurs. Recording each export's age stanza type and tag adds no surface. Pinning an expected recipient would add a parameter, so that sub-claim is rejected. | patch G48 |
| B36 drift-guard overclaim | medium | `project_resource` excludes specs, data, rules and non-management labels and annotations. RETIRE-KUBESPHERE.md line 85 still says "an identical projection means status-only churn", and step 4 (line 55) says "stop and re-review on any difference". The code tolerates a changed resourceVersion whenever projected fields match. | patch G49 |
| B37 in-run allowlist | false | Fixture UIDs differ from production by design, so a fixture cannot bind an externally reviewed production allowlist. A 4.27 executor is excluded because the intent says qualification does not authorize 4.27. | reject |
| B38 fence address coverage | carried | Same claim as B16: probes reach only the source host and 1.1.1.1. `Fixture.start` still reads as B16 describes. maybe-false; settle by probing the bridge/host and other addresses from inside a fixture. | defer G46 (carried) |
| B39 authorization evidence | low | `givenBefore: 2026-10-03T06:28:00Z` shows the answers preceded the actions, and the relay provenance is explicit. Fixture-only reruns need no production authorization record, and the handoffs cite the approval. Recording the earlier passphrase request would need the Administrator to restate it, which is not a direct correction. | reject |
| B40 action-1 overclaim | medium | QUALIFICATION.md overclaimed completion of a remediation action and kept contradicting statements in the same section; the specifics are in the same private passage (`passage-13.age`). | patch G50 |
| B41 remediation untracked | medium | Remediation actions are listed only in prose, and the conditions behind them predate this story; the specifics are in the same private passage (`passage-13.age`). | defer G51 |
| B42 kubeadm credential revocation | medium | The criterion-5 section offers `admin.conf` without saying that a shared `kubeadm:cluster-admins` certificate cannot be revoked short of CA rotation or removing a binding kubeadm relies on. That bears on the intent's independent revocation lineage. The alternative, an unowned binding for an approved identity, is listed but not contrasted on revocability. | patch G52 |
| B43 Kubernetes 1.34 end of life | medium | Kubernetes 1.34 reaches end of life on 2026-10-27. All eight 4.26 criteria are open and 4.27 has not started, and nothing records a critical path or fallback. The schedule belongs to 4.1, which predates this story. | defer G53 |
| B44 stale handoff text | low | QUALIFICATION.md line 3 links the "criterion ledger" to the superseded `20261001t173902z-census/criteria.json`. The story says "Each correction has unit tests", which is untrue for the doc-only G27, G28, G30–G32 and G34. Each fix is a direct correction. The sub-claim about an "operator's working SSH login" sentence is false because it no longer exists. | patch G54 |
| B45 spec stale text and metadata | low | The run-together tokens, "no fixture rerun" wording and frontmatter are in this build's spec. | reject (spec edit) |
| B46 further context deletions | medium | The `epic-4-context.md` rewrite also drops off-cluster record storage, distinct staging promotion-stop state, separate-writer off-site replication and "signed" release records, beyond G45's list. The fix edits agent context. The deferred-work overlap and path-style sub-claim would modify existing entries, so it is rejected. | defer G55 |
| B47 eviction thresholds | false | RANCHER.md line 88 says the proposal reproduces the observed disk-eviction gap and requires an explicit disk-eviction decision before relying on the reservation. Choosing thresholds is that owner decision. | reject |
| B48 stale Rancher wording | low | RANCHER.md line 55 still leads with "when node1 is a guest", although node1 is observed bare metal. Line 37 pairs "split-horizon" with "no public ... record". Both are direct corrections, part of the same incomplete stale-text sweep as B44. The v2.14 link is in the spec, so that part is rejected. | patch G54 |
| B49 kind and docker binaries | low | `tool_identities` hashes only the version output of PATH `kind` and `docker`. Hashing the resolved binaries is a direct correction. | patch G56 |
| B50 vendor images by local tag | carried | Same as B20's image-ID cross-check sub-claim, recorded low and unpatched. The code is unchanged. | reject (carried) |
| B51 locale-dependent values | low | Same kind as B32, in new immutable attempts. The meaning is intact, and re-capture is not a direct correction. | reject |
| B52 unrelated changes | carried | Same as B27. `apphost.cs` and the submodule bumps are separate user commits such as `73421308`. | reject (carried) |
| E37 identity-less virtual items | false | ComponentStatus, metrics views and the built-in Calico profile cannot satisfy a UID census. Recording them as explicit closed coverage matches the frozen Census row, and targeted metadata for all 70 is retained. Accepting them is a reviewer decision, not a collector defect. | reject |
| E38 HTTPException in anonymous probe | false | Only a non-HTTP or broken TLS peer at the native API port raises `http.client.HTTPException`. That was never shown, and the failure is loud. | reject |
| E39 proxy environment | carried | Same as E22, and the code is unchanged. | reject (carried) |
| E40 non-preferred non-CRD versions | maybe-false | `collect()` lists only each group's preferred version, adding non-preferred versions only for CRDs. Whether any production group serves a resource only in a non-preferred version is unobserved. Medium if true; settle by comparing per-version discovery on production. | defer G71 |
| E41 Traefik routes | carried | Same as E20: the five census IngressRoutes sit in unrelated namespaces. | reject (carried) |
| E42 dotted Helm release name | false | None of the nine census releases contains a dot, and an unshown dotted name fails loudly on `invalid-export-name`. | reject |
| E43 pin validator types | carried | Same as E23. | reject (carried) |
| E44 naive `checkedAt` | low | Subtracting a naive datetime raises before the `tzinfo` check, so the guard never runs and the error code reads `missing-pin-review-date`. It still fails closed, and the reorder is a direct correction. | patch G68 |
| E45 malformed authority event | false | A non-dict event or non-string digest raises loudly, as E23 recorded, and no non-test caller supplies such records. | reject |
| E46 revoke without grant | carried | Same as E24. | reject (carried) |
| E47 published attempt collision | low | Collision needs the same timestamped attempt ID and category under a different evidence root. A pre-check adds a guard. | reject |
| E48 ancestor symlink race | low | Needs a concurrent local attacker with write access to the custody path. An `openat` rewrite is not a direct correction. | reject |
| E49 unsanitized operator | low | `rehearse.py` records `args.operator` raw, while `qualify.py` applies `safe()`. Importing and applying `safe` is a direct correction. | patch G69 |
| E50 network/cluster creation race | low | Names derive from the unique attempt ID and are checked absent just before. A concurrent same-named creation is unlikely, and the fix adds ID tracking. | reject |
| E51 base tag sole reference | false | The retained node image `sha256:8a9be59e…` also carries `hexalith-s426-node:v1.34.9` and survived ten runs, so removing the base alias only untags. | reject |
| E52 refused probe | carried | Same as E17. | reject (carried) |
| E53 hard-coded isolation endpoint | carried | Same as B17's "`validate_isolation` checks literals set just before"; the endpoint literal is unchanged. | patch G25 (carried; not re-patched) |
| E54 undrained save stderr | low | `docker save` writes little to stderr, and a stall ends in a loud timeout. | reject |
| E55 probe observe timeout | carried | Same as E25. | reject (carried) |
| E56 synthetic replace race | low | A changed resourceVersion makes the replace fail loudly with 409. A retry loop adds logic. | reject |
| E57 release-name namespace | low | The census has no duplicate release names across namespaces, and the plan is reviewed exactly. The namespace condition adds a guard. | reject |
| E58 fixed resource list | low | G23 added the observed missing kinds. Discovery-driven listing is more than a direct correction, and the fixture holds no other cascade targets. | reject |
| E59 repeatable attempt ID | false | The sentence before the command says to supply a new attempt ID each time, and refusing reuse is intended. | reject |
| E60 reviewed-entry drift claim | medium | Same root cause as B36: changes to spec, data or RBAC rules after review pass the projection comparison, while the handoffs promise drift stops. | patch G49 |
| E61 procedure digest scope | medium | `rehearse.py` imports `evidence` and `qualify`, including `project_resource`, but `attempt.json` binds only `scriptSha256`. Recording those module digests adds no surface. | patch G70 |
| V13 fixture isolation gate | medium | Pre-verified: deleting `validate_isolation`, the attached-network check or the IP-literal guard in `start()` passes all 76 tests. | patch G57 |
| V14 synthetic `execute()` assertions | medium | Pre-verified: removing the canary check, the final `assert_preserved`, the hold check or per-action revalidation passes all tests. | patch G58 |
| V15 final phase and settle gates | medium | Pre-verified: removing the final `assert_phase`, the settle/`wait_absent` checks or the `did-not-settle` raises passes all tests. | patch G59 |
| V16 health-gate branches | medium | Pre-verified: removing the stale-APIService line or the new-namespace finalizer term passes all tests. | patch G60 |
| V17 census native-path refusals | medium | Pre-verified: removing the TLS/proxy/CA refusal, the skew check or the single-cluster check passes all tests. | patch G61 |
| V18 census raw encryption | medium | Pre-verified: making `Capture.command` skip `encrypt` passes all tests. | patch G62 |
| V19 CRD instance-discovery gaps | medium | Pre-verified: removing either CRD coverage append passes all tests. | patch G63 |
| V20 `native_read` mapping | medium | Pre-verified: an unconditional `return None` passes all tests. | patch G64 |
| V21 cleanup pass criteria | medium | Pre-verified: dropping `networkAbsent`, dropping `freshCredentialFileAbsent` or removing the `failed-cleanup` state passes all tests. | patch G65 |
| V22 chart digest and readiness | medium | Pre-verified: removing the chart-digest or readiness refusal passes all tests. | patch G66 |
| V23 upgrade-evidence root | medium | Pre-verified: removing `hexalith-upgrade-evidence` from the refused roots passes all tests. | patch G67 |
| V24 protected-propagation guard | false | Propagation into protected kinds is still stopped by the earlier per-action and closure checks, so the redundant guard causes no missing protection. | reject |
| V25 literal success records | false | The records are written only after their guards. The real gap is untested guards, which V13–V15 cover. | reject |

Groups: G47=B33; G48=B35; G49=B36, E60; G50=B40; G51=B41; G52=B42; G53=B43; G54=B44, B48; G55=B46; G56=B49; G57–G67=V13–V23, one each; G68=E44; G69=E49; G70=E61; G71=E40.

Routing: G47 is an intent gap, which triggers a loopback under the cascade, so the patch and defer entries wait for its resolution. `review_loop_iteration` is incremented. The pushed public history cannot be undone by reverting the local tree, and the commits are the user's own, so no revert or remote change was made. The intent gap goes to the Administrator for a decision.

Resolution: the Administrator's 2026-10-03 decision settles G47. The redaction and G48–G50, G52, G54 and G56–G70 are applied, and G51, G53, G55 and G71 are appended to [deferred work](deferred-work.md), G51 as a neutral pointer.

- **Redaction.** 41 fragments from QUALIFICATION.md, README.md, RANCHER.md, the story and this spec's non-frozen body, including rows B25, B33, B40 and B41, moved verbatim into 15 encrypted passages of the private attempt [`20261003t103215z-custody-redaction`](evidence/epic-4/4-26/20261003t103215z-custody-redaction/redaction-index.json). The fragments cover which key or account reaches which privilege, authentication and sudo weaknesses, credential file locations, modes and validity, and remediation status. Each passage is encrypted with the retained age 1.3.2 to the Administrator signing key. All 15 headers carry `-> ssh-ed25519 8XlNQg`, and the published `encrypted-exports.json` records that stanza for each export. The private directory is 0700 with 0600 files, both `SHA256SUMS` files verify, and no plaintext digest is published. Every moved passage is replaced by a pointer that names the attempt, export and ciphertext SHA-256 with a neutral reason. Row B25's title is also neutralized; its substance is in passages P07 and P13. RETIRE-KUBESPHERE.md and this story's existing deferred-work entries held no matching passage. Committed evidence attempts and Git history are unchanged. The agent cannot decrypt the passages, because the Administrator identity needs an interactive passphrase, so Administrator readback is pending.
- **G50 and G52.** G50 is applied to the private copy of the access assessment: the passage carries a corrected text in which the passphrase step does not complete the remediation action. G52 is applied to the neutral text: QUALIFICATION.md contrasts the shared kubeadm group certificate, revocable only by CA rotation or by deleting the binding kubeadm relies on, with an unowned per-identity binding, revocable by deleting that binding.
- **Frozen decision in code.** Following "future published projections omit them", `qualify.py` writes the native kubeconfig file-mode observation only to the encrypted export `native-credential-custody`; the published `access.json` names the export.
- **G54 note.** QUALIFICATION.md still held the "operator's working SSH login" sentence that row B44 called absent; it is corrected with the other stale text.
- **Verification.** 97 tests pass without skips, and `git diff --check` is clean. A scratch mutation run removed each guard that V13–V23 named, plus the new G48, G56, G68–G70 and custody guards, one at a time: all 39 removals fail at least one test. Final SHA-256: `rehearse.py` `4862637dc063d06742392e21c98262d16127b4a1b055c56621ecb1299adf98a5`, `qualify.py` `86a7d65c8248f8cda197f7656712a1d45413cd54bba9fdef6c56de98c97ca17a`, `evidence.py` `96d25efa03cdeb59f865712b148243799d5ad813a828000c02a972e998a59024`. These bytes have unit verification only, with no census or fixture rerun. No production read or mutation, grant, Rancher action, recovery overwrite or change to `MAINTENANCE.md` occurred. All eight criteria and story/sprint remain `in-progress`.

### Review loop 4 (2026-10-03)

The review covered the full diff since `baseline_commit` after the redaction and the loop 3 corrections. All three layers ran as fresh context-free agents, and none was skipped. The carry rule is the same as in loop 3. Verification-gap findings V26–V33 arrive pre-verified.

| Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- |
| B53 unrecorded intent amendment | low | The story's loop 3 section says no "frozen-intent change occurred", but the 2026-10-03 decision was added to the frozen block. Rewording the story is a direct correction. The Spec Change Log sub-claim would edit this spec, so it is rejected. | patch G72 |
| B54 spec frontmatter | low | Editing it would edit this build's spec. | reject (spec edit) |
| B55 stale spec body text | carried | Same claim as B45. | reject (carried) |
| B56 published credential path | low | QUALIFICATION.md line 49 still shows the actual native context and kubeconfig file path in the example command. The decision explicitly covers credential file locations. Placeholders are a direct correction. | patch G73 |
| B57 residual account-to-privilege text | intent gap | QUALIFICATION.md line 120 says the `jpiquot@local` certificate "carries no group, so that binding is its only cluster-admin grant". The kubeadm group path is also stated publicly. The decision covers "which key or account reaches which privilege", while the approved Code Map keeps facts "already in the census". The census itself publishes these bindings. | intent_gap G75 |
| B58 census RBAC projections | intent gap | `project_resource` publishes every RoleBinding and ClusterRoleBinding `roleRef` and `subjects` into `inventory.json`. Read literally, the decision's "future published projections omit them" covers account-to-privilege mappings, yet the frozen Census and Ownership rows rely on RBAC projection. Only the Administrator can settle which reading applies. | intent_gap G75 |
| B59 anonymous access fields | low | `access.json` would publish `anonymousReadAllowed: true` only if credential-free reads succeeded. The observed result is 401/403, kubeadm defaults deny `system:anonymous`, and the operator reviews a census before committing it. Withholding the field conditionally adds a branch for an unobserved state. | reject |
| B60 recipient not enforced | carried | Same claim as B35's rejected pinning sub-claim. | reject (carried) |
| B61 census procedure binding | medium | `qualify.py` `collect()` writes no script or module digests, because G70 covered only `rehearse.py`. The `20261003t073627z` rerun handoffs do not say that its imported-module bytes are unbound. Recording the digests and adding that disclosure are direct corrections. | patch G74 |
| B62 terminated manager pods | false | `post_retirement_checks` exempts only Succeeded or Failed Pods, which run nothing. The stricter per-phase gate is the fail-closed behavior E29 recorded. | reject |
| B63 remediation untracked | carried | Same claim as B41. | defer G51 (carried) |
| B64 per-fragment redaction index | low | The published index belongs to an immutable attempt. The pushed history already gives a fragment-level map through `git diff c3c473a`, and adding a private per-fragment index is more than a direct correction. | reject |
| B65 Kubernetes 1.34 end of life | carried | Same claim as B43. | defer G53 (carried) |
| B66 context deletions | carried | Same claim as B23 and B46. | defer G45/G55 (carried) |
| E62 proxy environment | carried | Same as E22. | reject (carried) |
| E63 refused probe | carried | Same as E17. | reject (carried) |
| E64 multi-owner dependents | false | Over-approximating the closure only demands more explicit members, or ends in a loud wait timeout. It never deletes an unreviewed object, and no in-scope multi-owner dependent was shown. | reject |
| E65 per-phase manager Pod gate | carried | Same as E29. | reject (carried) |
| E66 later-phase path discovery | false | Roots come from the same fixture's inventory and discovered kinds. An unshown missing kind fails loudly. | reject |
| E67 retained CRD conversion to a retired Service | low | The final health gate checks retained CRD conversion backends, and the current-procedure rerun found none stale. Planning earlier adds logic. | reject |
| E68 transient errors while polling | low | A transient read error fails the attempt loudly and stays fail-closed. A retry adds branches. | reject |
| E69 ServiceAccount reference domain | low | A mismatched token-Secret expectation stops on a wait or an unexpected deletion. The rerun completed with exact removals. | reject |
| E70 probe member finalizer | carried | Same as E25. | reject (carried) |
| E71 null `checkedAt` | carried | Same class as E23: malformed test-only input raises loudly. | reject (carried) |
| E72 null validator fields | carried | Same as E23. | reject (carried) |
| E73 revoke without grant | carried | Same as E24. | reject (carried) |
| E74 published attempt collision | carried | Same as E47. | reject (carried) |
| E75 partial publication directory | low | Needs a write failure partway through publication. A staged rename is more than a direct correction. | reject |
| E76 system ancestor symlink | false | This host's custody path has no symlinked ancestor, and an unshown symlinked host fails loudly. | reject |
| E77 ancestor symlink race | carried | Same as E48. | reject (carried) |
| E78 network/cluster creation race | carried | Same as E50. | reject (carried) |
| E79 build cache residue | low | Build cache is not a named fixture object that cleanup claims to remove. Pruning adds logic. | reject |
| E80 census RBAC claim | intent gap | Same root cause as B58. | intent_gap G75 |
| E81 anonymous-read claim | low | Same as B59. | reject |
| V26 projected Helm annotations | medium | Pre-verified: dropping the `helmRelease` projection passes all 97 tests. Replaying the committed census shrinks the refused 476-object scope to an accepted 38-object plan. | patch G76 |
| V27 fixture custom-resource listing | medium | Pre-verified: omitting the served KubeSphere custom resources passes all tests. Replaying the rerun baseline shrinks 347 objects in 16 phases to 62 in 5. | patch G77 |
| V28 health gate and probe invocation | medium | Pre-verified: removing the `post_retirement_checks` or probe call still records a pass. | patch G78 |
| V29 probe outcomes | medium | Pre-verified: misclassifying a deleted or terminating member namespace passes all tests. | patch G79 |
| V30 fixture command exit check | medium | Pre-verified: removing the refusal of disallowed exit codes passes all tests. | patch G80 |
| V31 owner resolution record | medium | Pre-verified: a constant `ownersResolved`, an empty dependent list or a missing empty-inventory error passes all tests. | patch G81 |
| V32 management classification terms | medium | Pre-verified: removing the CRD-group or Helm release-namespace term passes all tests. On the committed census that drops 41 CRDs and one binding. | patch G82 |
| V33 census coverage branches and Helm exports | medium | Pre-verified: removing the malformed owner, finalizer and namespace, preferred-version or Helm-entry gap branches, or skipping KubeSphere `helm get` exports, passes all tests. | patch G83 |

Groups: G72=B53; G73=B56; G74=B61; G75=B57, B58, E80; G76–G83=V26–V33, one each.

Routing: G75 is an intent gap, so the patch entries wait for its resolution and survive the loopback. `review_loop_iteration` is incremented. No revert was made, because the verified code changes do not depend on how G75 is resolved and the user has not asked to discard them.

## Design Notes

Prefer a separate VM on existing safe capacity. If node1 is a guest, use a sibling VM on its hypervisor. A different physical host reduces correlated outages but adds cost/connectivity work. Neither provides HA. Propose 4 vCPU/16 GiB RAM/80 GiB SSD; CPU/RAM follows Rancher's [small-tier guidance](https://ranchermanager.docs.rancher.com/v2.14/getting-started/installation-and-upgrade/installation-requirements/), disk is estimated. Current authenticated observations report node1 as bare metal with 32 logical CPUs/~126 GiB RAM and existing AMD-V/KVM/QEMU/libvirt. Use the [concrete reservation proposal](../../eng/cluster-management/RANCHER.md#observed-host-and-reservation-proposal) to assess contention and additional host headroom; safe guest allocation, disk performance and owner decisions remain unaccepted. No allocation/procurement is approved.

## Verification

- `python3 -m unittest discover -s eng/cluster-management -p 'test_*.py'` — meaningful evidence/isolation failure cases pass.
- `git diff --check` — clean whitespace.
- Inspect digests, custody, isolation/cleanup, criterion evidence and preserved identities; retain failures/limitations.

## Current standalone procedure follow-up

The [20261001t204952z-rehearsal result](evidence/epic-4/4-26/20261001t204952z-rehearsal/native-result.json) executes the exact current `rehearse.py` SHA-256 `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0`, including the malformed-mount guard added after the previous run. All six source/external refusal probes, real stale-UID/resourceVersion409/Conflict and unchanged-content assertions, child-first/named synthetic finalizer deletion, ten-before/eight-after exact preservation and canary checks pass. [Cleanup](evidence/epic-4/4-26/20261001t204952z-rehearsal/cleanup.json) explicitly proves node/network/owned-volume/image-alias and fresh-credential absence. Its 102 encrypted exports use the existing **synthetic fixture recipient**; its filename/comment establishes no Administrator custody, independent decryption or off-node acceptance. No production credentials/data were imported.

[Current verification](evidence/epic-4/4-26/20261001t205700z-current-procedure/verification.json) binds exact encrypted executed procedure bytes, the current artifacts, unit/local integrity checks and unaccepted limits. Historical attempts and frozen intent remain intact. Only the standalone current-byte execution limitation is superseded: actual KubeSphere runtime/uninstall was not rerun; finalizers, phase propagation, representative configuration/authenticated workload/recovery, effective host/access, MFA/authority and independent custody remain unresolved. All eight criteria and story/sprint remain `in-progress`; no gate opens.

## User-authorized access follow-up

The user approved creation of evidence. [Conversation authorization](evidence/epic-4/4-26/20261001t205847z-native-access/authorization.json) is recorded without manufacturing authenticated grants, MFA or acceptance. [Native authority observations](evidence/epic-4/4-26/20261001t210315z-native-authority/native-access.json) verify the unchanged direct TLS cluster UID, authenticated native access, private credential-free HTTP 403 and invalid-bearer HTTP 401. The principal's sampled permissions and the SSH inputs are held in private custody: `passage-14.age` of attempt [`20261003t103215z-custody-redaction`](evidence/epic-4/4-26/20261003t103215z-custody-redaction/encrypted-exports.json) (ciphertext SHA-256 `b47f8e24efb4754db4e1cdec6bccc559342786218742a1bec123d11d2d2018a4`; criterion 5 remains unmet). These checks changed no persistent resource or role and establish no distinct authenticated unauthorized/public denial, current signed lineage or independent custody.

Host-access trials from 2026-10-01 are in the same private passage (`passage-14.age`). Host facts/capacity remained unavailable at that point. Sensitive command output uses the retained production census recipient, encrypted outside Git, with off-node decryption/signatures unverified. Frozen intent, historical evidence, procedure code and story/sprint statuses remain unchanged.

[Follow-up verification](evidence/epic-4/4-26/20261001t211014z-qualification-followup/verification.json) binds the current handoff after these additions, local evidence integrity, actual decrypted synthetic unit-test output and all five frozen matrix rows. The full baseline diff is retained in a unique system-temporary file; structured evidence was parsed and the current handoff diff inspected. All 45 executed tests passed without skips. This is implementation verification; operational acceptance remains incomplete and Build stays at Step 3.

## Resumed implementation verification

On 2026-10-02, bounded fixture-schema corrections reject non-object or non-string source identities before allocation and route malformed successful Docker/native responses through sanitized failure receipts, diagnostic refusal and cleanup/summary finalization. `FixtureBoundaryTests.test_malformed_source_identity_refused_before_fixture_allocation`, `FixtureBoundaryTests.test_malformed_successful_docker_image_schema_finalizes_closed` and `FixtureBoundaryTests.test_malformed_native_diagnostics_cannot_interrupt_failure_receipt_or_cleanup` pass with all prior cases: 48 executed tests without skips. `git diff --check` passes.

The procedure SHA-256 changes from `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0` to `9f84f36382efecec137f2dad936421c12e48e6afbbb956330470e12df169716b`. The new revision has unit verification only; no full fixture rerun followed it. The historical standalone attempt proves only its recorded prior procedure bytes, and the failed actual KubeSphere retirement/finalizer/phase-propagation evidence remains unchanged. No native deletion/preservation semantics, frozen intent, recovery evidence or hashed upgrade proposal changed. All eight original criteria and story/sprint remain `in-progress`; operational acceptance keeps Build at Step 3.

[Resume verification](evidence/epic-4/4-26/20261002t051917z-resume-verification/verification.json) records the root's independent 48-case run without skips, passing coverage for all five frozen matrix rows, inspection of the resumed diff, and local integrity of 31 prior projection attempts and 1,539 encrypted exports. It binds the current procedure and handoffs; local checks establish no independent decryption, signatures, authority or operational acceptance. The pending inputs are a working SSH identity and references to existing Administrator-approved capability/authority records; no response or approval is inferred.

Host-account clarifications from 2026-10-02 are in the same private passage (`passage-14.age`). They accept no MFA, role, custody or retirement criterion and do not alter historical evidence.

The [20261002t054119z targeted native read](evidence/epic-4/4-26/20261002t054119z-native-node-read/native-node-observation.json) independently confirms the same cluster UID, node `node1` reporting Ubuntu 24.04.3 LTS/kubelet v1.34.9/32 CPUs/~126 GiB RAM, and kubeadm configuration with one external-etcd endpoint. Raw outputs are encrypted outside Git. This uses the existing native credential and verifies no host account, effective kubeadm/etcd binary, virtualization, reserved capacity, MFA or independent custody. No persistent source resource or role changed; all eight criteria remain incomplete.

Further host-account SSH trials on 2026-10-02 are in the same private passage (`passage-14.age`). No account/key/role grant or production change occurred, and all original criteria remain incomplete.

An operator-supplied host-login report followed; it is in the same private passage (`passage-14.age`) and supersedes only the unknown operator host-account input; effective host/virtualization/capacity facts, approved management authority/MFA and independent custody/retirement acceptance remain incomplete.

## Independently authenticated host follow-up

Authorized read-only host access then became available; its specifics are in the same private passage (`passage-14.age`). OS/native/web identities are not automatically mapped to approved management roles.

[Host facts](evidence/epic-4/4-26/20261002t072401z-host-facts/host-observation.json) and [service/process bindings](evidence/epic-4/4-26/20261002t072631z-host-services/host-services.json) establish installed kubeadm/running kubelet1.34.9, running host containerd2.3.3 and external-etcd3.6.5 with exact binary digests. A second container-scoped containerd has different bytes despite the same version. [Native datastore reads](evidence/epic-4/4-26/20261002t072944z-native-datastore-host/datastore-host-observation.json) confirm client/peer certificate authentication, one current member and no sampled status errors/alarms. They take no snapshot, perform no health-write probe and test no restore.

Node1 reports bare metal, AMD-V/KVM, installed QEMU8.2.2/libvirt10.0.0, ~125.66GiB total/~113.84GiB available RAM and ~791.16GiB available root space. Libvirt is inactive with listening activation sockets, so live daemon queries were omitted; no running QEMU or offline system guest definition was observed. [Current node budget](evidence/epic-4/4-26/20261002t073151z-node-resource-budget/node-resource-budget.json) revalidates source/node UIDs and Ready/no-pressure conditions, with allocatable31.6CPUs/~118.89GiB, requests12.3CPUs/20,702MiB and limits49.7CPUs/80,484MiB. The [reviewable reservation proposal](../../eng/cluster-management/RANCHER.md#observed-host-and-reservation-proposal) adds4500m/18GiB/100GiB for the guest and estimated overhead, subject to measured contention/owner approval and separately scoped implementation. Host SSH/binary/topology facts are now observed; safe reservations, disk health/IOPS, private endpoint/cost/owners, approved capabilities/authority/MFA, target qualification, representative retirement and independent custody/recovery remain unresolved.

All raw outputs are encrypted outside Git before writes, with only allowlisted projections published. Agent commands were read-only and started no services. Historical failures, current procedure bytes, frozen intent, protected recovery/upgrade artifacts and story/sprint statuses remain intact. Build remains at Step3 because original operational criteria are incomplete.

## Dependency-first retirement follow-up

On 2026-10-02 `rehearse.py` replaced its no-hooks Helm path with the runbook's recommended dependency-first native retirement:

- An exact scope is planned into 17 ordered child-first phases before any request is sent.
- Every root gets a fresh UID/resourceVersion-bound DELETE with Foreground propagation.
- KubeSphere controllers complete their lifecycles before the manager is removed.
- Reconciled registrations, remaining release objects and release records follow controller removal.
- `system-workspace` objects receive one named finalizer intervention each.
- Each phase is compared exactly after settling, and a post-retirement native health gate runs at the end.

`qualify.py` additionally projects a Secret's service-account reference and a CRD's conversion service. 60 tests pass without skips.

Nine isolated attempts executed. [`20261002t121114z-rehearsal`](evidence/epic-4/4-26/20261002t121114z-rehearsal/kubesphere-retirement-result.json) passed with procedure SHA-256 `42e8e6efc64ed6e383509ca461556d566ccf836473bbad208e6031ed47580b7c`, the bytes current at execution. Review loop 2 changed `rehearse.py` to SHA-256 `1e723b7579f5bddc8c7e6fa5dac21952ae75cb8243971e4115cfa657fba6d92a`; those patched bytes have unit verification only, with no fixture rerun. It recorded 660 baseline objects and 347 exact removals in 16 executed phases, with zero unexpected/incomplete/recreated objects. Protected namespaces/PV/PVC/StorageClasses and 43 CRDs were unchanged, post-retirement health was clean and cleanup was verified. Each earlier stop is retained with its correction: kind timeout, label-linked RoleBindings, user kubeconfig/token Secrets, the probe wait, an own-gate defect, the stale license webhook, missing tenant sync and the manager recreating that webhook. The synthetic probe shows that controller-processed workspace deletion unbinds member namespaces (label and finalizer removed) without deleting them.

Analysis of committed evidence only:

- The census-derived production plan proposes 476 objects in 17 phases and includes an unrehearsed application-store phase.
- License Secrets, `User/jpiquot`, its GlobalRoleBinding, `Cluster/host`, an app Category and 7 namespaces would retain inert KubeSphere finalizers.
- The only `jpiquot` cluster-admin binding is ownerReferenced by `GlobalRoleBinding/jpiquot-platform-admin`, so native authorization is not KubeSphere-independent.
- 12 qualification attempts from `20261002t051917z` through `20261002t074902z` used a non-Administrator recipient; the custody finding is held in private custody: `passage-15.age` of attempt [`20261003t103215z-custody-redaction`](evidence/epic-4/4-26/20261003t103215z-custody-redaction/encrypted-exports.json) (ciphertext SHA-256 `c82e112058320bf6bfad7553eb02a8e11b40524316dfdcfe5798a9cf1e816403`; criteria 1 and 5 remain incomplete).

These findings need Administrator decisions. No production mutation, grant, Rancher action, recovery overwrite or hashed upgrade-proposal change occurred, and frozen intent is unchanged. The [receipt](evidence/epic-4/4-26/20261002t125604z-dependency-first-verification/verification.json) binds the executed and current procedure digests. All eight criteria and story/sprint remain `in-progress`; Build stays at Step 3.

## Administrator-approved custody follow-ups

On 2026-10-03, two Administrator-approved follow-ups ran.

- **Native admin credential (A).** A read-only host check observed a kubeadm administrator credential in group `kubeadm:cluster-admins`. The census shows the `kubeadm:cluster-admins` and `cluster-admin` (`system:masters`) bindings have no owners, so this path does not depend on KubeSphere. Its location, custody and validity, and the diagnosis of the expiry tooling, are in the same private passage (`passage-15.age`).
  - The credential was not used, copied or changed.
  - Criterion 5 stays unmet until the Administrator decides the holder and encrypted off-node copy and working use is proven.
- **Re-encryption (B).** The 33 private exports of the 12 attempts `20261002t051917z` to `20261002t074902z` were stream-re-encrypted to the Administrator recipient (`8XlNQg`) without writing plaintext to disk ([manifest](evidence/epic-4/4-26/20261003t063011z-recipient-reencryption/reencryption-manifest.json)). File counts match, every new header carries `8XlNQg`, every plaintext matched its original record, and the originals are unchanged.
  - Administrator readback, the decision on the original ciphertexts and the remaining custody actions in the same private passage (`passage-15.age`) are pending.

Frozen intent, `rehearse.py`, `MAINTENANCE.md` and sprint status are unchanged. No commit was made.

## Current-procedure fixture rerun (2026-10-03)

The Administrator approved one rerun of the dependency-first rehearsal in an isolated kind fixture with the current procedure.

- **Inputs.** The procedure SHA-256 `1ac666c06c93ecb619e737cce198204f8a8d4644a9e1382aa3e626b2a1d6d2f9` was confirmed before the start and recorded as executed. Every other input was copied from the recorded `20261002t121114z` invocation:
  - the committed sanitized census `20261001t195141z` (`84c44e0a…`);
  - node image `sha256:8a9be59e…`;
  - public chart `a5c87fe1…`;
  - retained kubectl `90b7b905…`, Helm `7a319dee…` and age `eb7dd1b5…`;
  - the existing synthetic fixture recipient (tag `bHCI3g`).

  No production credential or data entered the fixture, and nothing outside the new fixture was mutated.
- **Outcome.** [`20261003t073627z-rehearsal`](evidence/epic-4/4-26/20261003t073627z-rehearsal/kubesphere-retirement-result.json) **passed**, and no code change was needed.
  - Source and egress refusal: 6/6 probes blocked.
  - Native stale-UID and stale-resourceVersion requests returned 409/Conflict.
  - All five deployments were Ready, the InstallPlan reproduced `Installed` and tenant sync took 3 s.
  - The baseline has 667 objects, of which 347 were removed exactly in 16 executed phases. Allowlist SHA-256 is `76b32cf39e60ff05ce407c844e90e281acfe3134628f3b528c8ffa7970b3b22c`.
- **Reviewed-entry binding.** There were 179 root actions: 1 `absent-before-request` (`ExtensionVersion/ks-console-embed-1.2.0`) and 178 accepted DELETEs with no conflict retries.
  - 165 DELETEs carried their reviewed resourceVersion.
  - 13 saw a changed resourceVersion, and all 13 passed the reviewed-projection comparison, so no reviewed field had drifted. They are `User/admin` 1345→2704, `Extension/ks-console-embed` 1657→2613, `Repository/extensions-museum` 1760→3397 and ten extension Categories (`ai-machine-learning`, `computing`, `database`, `deepseek`, `dev-tools`, `integration-delivery`, `networking`, `observability`, `security`, `storage`).
- **Route refusal.** The route-consumer check did not fire, because the fixture has no out-of-scope route. Its refusal path has unit verification only.
- **Preservation.**
  - There were zero unexpected, incomplete, recreated or transient removals.
  - The 13 protected identities, the synthetic canary and all 43 CRDs with their resourceVersions are unchanged.
  - The post-retirement gate found no stale admission, APIService, CRD-conversion or route reference and no manager runtime, and the new-namespace lifecycle was clean.
  - The only finalizer interventions were the two named `system-workspace` removals.
  - 27 KubeSphere-marked objects remain: the earlier 26 plus the newly inventoried manager Lease.
- **Cleanup and custody.**
  - [Cleanup](evidence/epic-4/4-26/20261003t073627z-rehearsal/cleanup.json) passed: node, network, owned volume, both image aliases and the fresh kubeconfig are absent, and `kind delete` exited 0.
  - An independent readback found no kind cluster or `s426` container or network.
  - All 639 exports are encrypted to the synthetic fixture recipient, with 0700/0600 modes, and both `SHA256SUMS` files verify. The published export record omits plaintext digests.

76 unit tests pass and `git diff --check` is clean. Frozen intent, `MAINTENANCE.md` (`834be822…`), recovery evidence and sprint status are unchanged, and nothing was committed. This is fixture evidence only. Source ownership and consumer decisions, the production-only cohorts, production values, authenticated workload and recovery evidence and the other criteria's gates remain. All eight criteria and story/sprint remain `in-progress`.
