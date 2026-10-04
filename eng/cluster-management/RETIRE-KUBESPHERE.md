# Story 4.27: proposed native KubeSphere retirement

**No live removal is approved.** The current production allowlist is empty because ownership, required consumers and deletion effects remain unaccepted. [Qualification](QUALIFICATION.md) supplies exact native identities and protected namespace/PVC/PV bindings; its observations do not authorize Story 4.27 or clear Story 4.1.

## Approved retirement decisions, 2026-10-03

**Public exposure first:** Story 4.2 closes `kube.hexalith.com` and proves public denial before 4.27 retirement. After manager removal, the `console-route` phase requests deletion of these exact `kubesphere-system` roots: Ingress `kubesphere-console`, Certificate `kubesphere-console-letsencrypt`, TLS Secret `kubesphere-console-letsencrypt-tls` and Lease `ks-controller-manager-leader-election`. CertificateRequest/Order descendants enter only through the current explicit owner closure, with their own reviewed identities. Preserve shared ingress/cert-manager and application/OIDC routes.

Keep **all seven namespaces**: `default`, `kube-node-lease`, `kube-public`, `kube-system`, `kubekey-system`, `kubesphere-controls-system`, `kubesphere-system`. The final `namespace-finalizers` phase runs with the manager absent and removes only metadata finalizer `kubesphere.io/cascading-deletion` through a UID/resourceVersion-bound native PUT. It issues no Namespace DELETE; native `spec.finalizers`, unrelated metadata finalizers, full labels, UIDs and storage remain unchanged. A terminating, owned, unnamed or drifted namespace is refused. This is no wildcard finalization or ownership rewrite.

| Inert archival objects | Custodian | Cleanup review date and scope |
| --- | --- | --- |
| The 20 exact license Secrets in the source census | Administrator | **2026-11-03 proposed review date**; no automatic deletion |
| `Cluster/host` | Administrator | Same proposed date; retain its inert finalizer and exact instance/CRD identity |
| The exact app-store Category in the source census | Administrator | Same proposed date; inspect remaining application consumers before later cleanup |

The date is explicitly **unapproved** in [the source-bound proposal](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t135922z-decided-retirement-plan/production-plan.json); retention is the approved choice, and any later deletion needs a separate exact review. `User/jpiquot` and its GlobalRoleBinding/native projection remain protected pending working independent administration and further scope decisions. No CRD is removed while it still contains retained instances or unresolved consumers.

The proposal contains **482 deletion objects, seven namespace interventions and 19 phases**. It binds the old census, retains its 70 identity-coverage gaps and records `sourceRevalidated: false` / `productionApproved: false`. It is not the executable production allowlist, which stays empty. Refresh source discovery, identities, token references, consumers and the full private content-drift check before exact 4.27 approval.

Before 4.27, perform a **fresh production etcd restore through Story 4.0's route**, with matching node recovery, off-node custody and current authority. The [historical node-archive confirmation](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t134235z-recovery-archive-confirmation/archive-confirmation.json) identifies a complementary **Story 4.1** archive; Story 4.0's retained manifest contains no node-archive reference. Historical recorded recovery and the synthetic restore below do not meet this fresh production prerequisite.

## Current retirement and rollback evidence

[The actual-chart run](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t135807z-rehearsal/kubesphere-retirement-result.json) passed 18 executed phases (the production application-store phase was absent): **353 exact removals**, zero unexpected/incomplete/recreated objects, **46 retained CRDs**, preserved protected identities/storage and clean post-retirement native health. The console phase removed six objects from four roots; the fixture's certificate APIs/owner chain are synthetic, so real cert-manager reconciliation remains unrehearsed. It exercised six native system namespaces. KubeSphere had already unbound its seven extra synthetic namespaces before the baseline; [the separate manager-free probe](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t145655z-seven-namespace-probe/seven-namespace-result.json) instead proves all seven named equivalents, seven UID/resourceVersion-bound PUTs, zero Namespace DELETEs and full metadata/storage/canary preservation.

Before retirement, the run captured a fixture etcd snapshot at revision **2769** with **983 keys**. It destroyed the retired node, bootstrapped a fresh fenced Docker node and restored its own encrypted synthetic inputs/snapshot with hash checking, a 1,000,000,000 revision bump and `--mark-compacted`. [Restore verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t135807z-rehearsal/rollback-restore-result.json) matches all **714 baseline object UIDs** and CRD UIDs, verifies new etcd cluster/member identities, native API readiness and five actually running/Ready KubeSphere deployments. Both retired-source and restored-target cleanup passed. This is single-node kind/stacked-etcd recovery with synthetic credentials/data; it does not qualify production external-etcd, authenticated business workloads or Rancher authority.

The [verification receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t150550z-loop4-verification/verification.json) binds 117 passing tests, 17 detected guard mutations and every new export's double-recipient/decryption/digest checks. Exact executed script/module bytes are retained in [the source binding](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t142400z-executed-procedure-binding/procedure-binding.json). The full run used `evidence.py` `94b4e2d0…`; final `41ce544a…` adds direct-library readback refusal and ran in the namespace probe. The older `20261003t073627z` run bound only `rehearse.py`; imported modules were unbound. The earlier `20261003t134150z` tenant-sync failure and successful cleanup remain immutable; the corrected fixture waits for the native KubeSphere Service and requeues only its synthetic host object.

Production values, application-store/operator cohorts, real certificate reconciliation, complete content-drift enforcement, authenticated workload outcomes, current supported pins and signed independent authority/recovery acceptance remain incomplete. After accepted retirement, capture a fresh native recovery point for **1.35 → 1.36.5**, with 1.37 only after Rancher support. `MAINTENANCE.md` remains byte-for-byte unchanged.

## Version-specific procedure findings

The independently retrieved public `ks-core` 1.2.4 archive SHA-256 is `a5c87fe18477bacf9032eb9cf2968b73d73465aa3652b99eede865fe3ba7fbdd`; OCI manifest is `sha256:1c7ce4f56a22568ae6f21729e647585266ed27c24dae5545edd11b7ada7801c7`. Its application version matches observed KubeSphere 4.2.1. Preserve the chart and exact hook script digests with each rehearsal; matching a chart does not reproduce production values or installed extensions.

The chart has post-delete Jobs with cluster-admin bindings. Native `helm uninstall` with default hooks is outside the proposed safe scope until every script effect is reviewed. The core script deletes Jobs and RBAC through a cluster-wide `kubesphere.io/managed=true` selector. The CRD script removes system-workspace finalizers, rewrites labels/owner references/finalization for all selected managed namespaces, and strips finalizers from every instance of its selected CRDs before deleting them. These are concrete unaccepted deletion effects, not a safe UID-bound procedure.

The [actual-chart attempt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t182403z-rehearsal/summary.json) installed core 1.2.4/application 4.2.1 and embedded console 1.2.0 with fresh synthetic configuration. All five deployments became Ready. Native UID/resourceVersion-bound deletion of the Installed console InstallPlan completed while its controller remained running, without licensed KubeSphere application calls. Subsequent `helm uninstall ks-core --no-hooks --wait --timeout 90s` removed core deployments but returned `context deadline exceeded`; **the no-hooks retirement proposal is not qualified**. The original diagnostics omitted most KubeSphere custom instances. Preserve that failure; its cleanup parser conservatively reported the node unverified, and later explicit not-found readback corrects the observation without rewriting it.

The single [diagnostic follow-up](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t184031z-rehearsal/kubesphere-uninstall-observation.json) repeats that failure with 604 before-state objects and 317 exact expected retirements. Core deployments are absent; 151 expected retirements remain, including 17 chart custom resources with deletion requested at 18:49:15Z and finalizers still present after their controllers were removed:

| Terminating chart resources | Finalizer still present |
| --- | --- |
| WorkspaceTemplate/system-workspace | `kubesphere.io/cascading-deletion` |
| KubeSphere ServiceAccount/kubesphere-system/ks-console | `finalizers.kubesphere.io/serviceaccount` |
| User/admin | `finalizers.kubesphere.io/users` |
| Five GlobalRoleBindings; seven GlobalRoles | `finalizers.kubesphere.io/globalrolebindings`; `finalizers.kubesphere.io/globalroles` |
| Repository/extensions-museum | `kubesphere.io/repository-protection` |
| Extension/ks-console-embed | `kubesphere.io/extension-protection` |

The [safe diagnostic](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t184031z-rehearsal/kubesphere-diagnostic-state-111.json) distinguishes Terminating from retained/unattempted objects using deletion timestamps. Protected namespace/PV/PVC/synthetic-deployment UIDs and bindings remain intact, with no authenticated application or post-core canary claim. Offline exact-set comparison also finds one unallowlisted completed extension-install Job Pod removed; its batch Job was omitted from that captured scope. This is a separate coverage/propagation blocker. The tooling at that time (2026-10-01) added Jobs/endpoints, refused uncensused owners and verified a separate extension-phase expected set immediately after InstallPlan deletion, before any core uninstall. That no-hooks procedure is superseded by the [dependency-first qualification](#dependency-first-fixture-qualification-2026-10-02).

The executed [fixture allowlist](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t184031z-rehearsal/kubesphere-retirement-allowlist.json) SHA-256 is `fa04fd13ad22f73d096a2c714e161848fca6ebe7b75a38f7f95bb5fc671c8a12`; its executed fixture procedure SHA-256 is `d4fd9a0977d3e5fc8d40de9bc5e0f54830210c3455ea2b2f8218bd061d17a3eb`. [Cleanup](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t184031z-rehearsal/cleanup.json) passes explicit node/network/volume absence, fresh-credential removal and exact image-alias removal. The [closeout](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t185518z-closeout/verification.json) binds the current runbook/procedure/allowlist digests, local evidence checks and limits. These are fixture-only digests, not an approved production scope.

The separate [synthetic native result](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t180843z-rehearsal/native-result.json) passes exact UID/resourceVersion deletion, child-first scope, a named synthetic finalizer, PV/PVC identity/binding preservation and synthetic canary readback. Its deployment has zero replicas and its finalizer is synthetic. It cannot substitute for complete actual extension/core/CRD propagation, production-value/consumer fidelity or authenticated workload/recovery qualification.

After review, a [fresh standalone fixture](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195601z-rehearsal/native-result.json) also verifies actual [stale UID](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195601z-rehearsal/native-precondition-stale-uid.json) and [stale resourceVersion](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195601z-rehearsal/native-precondition-stale-resource-version.json) after synthetic delete/recreate and update races. Both native requests return 409/Conflict and leave current full content/UID intact. Protected storage/lifecycle/canary assertions and explicit node/network/volume/owned-image-alias cleanup pass. This verifies the patched safeguards without rerunning or accepting the failed actual KubeSphere retirement; see the [current verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t200342z-post-review/verification.json).

## Exact proposed action ledger

**Superseded (2026-10-01).** The post-review standalone run used procedure SHA-256 `6277bf2ceb290e276405780016968eb83b0a3bd1e36694f35e311262faf94f19`. The then-current revision `a78aa790…` added only a three-line malformed-mount guard, and its own standalone run is recorded below. The current procedure and its evidence are in the [dependency-first section](#dependency-first-fixture-qualification-2026-10-02).

### Retirement choices and recommendation

| Choice | How it works | Pros | Cons and observed limit |
| --- | --- | --- | --- |
| Vendor Helm uninstall with hooks | Helm removes the release, then vendor Jobs reconcile selected RBAC, CRDs, finalizers and namespace ownership | Uses the packaged cleanup and can complete controller-independent cleanup | The inspected scripts use broad selectors and finalizer/namespace rewrites; their effects are not bounded to reviewed UIDs. It is unqualified for the preserved production inventory |
| Helm uninstall with `--no-hooks` alone | Helm removes chart resources and waits, leaving vendor cleanup Jobs disabled | Avoids the broad hook mutations and is easy to repeat in a disposable fixture | Both actual-chart attempts timed out: required controllers were gone while 17 chart custom resources still needed finalizer reconciliation. It also leaves controller-created resources and propagation decisions unresolved |
| Dependency-first native retirement | While controllers still run, finish each approved extension/custom-resource lifecycle and any required ownership handoff; then remove exact reviewed core objects through native UID/resourceVersion-bound actions | Makes the deletion scope, phase results and preserved namespaces/storage reviewable; uses the native API without a licensed application-write dependency | Requires owner/consumer decisions. Controller-driven side effects need their own expected sets. The [2026-10-02 fixture](#dependency-first-fixture-qualification-2026-10-02) rehearses it for the actual chart; production-only cohorts remain unrehearsed |

**Recommendation:** Use the dependency-first native sequence, now rehearsed in the disposable fixture (see below), as the 4.27 procedure basis once source ownership/consumer decisions are supplied. Keep the existing controllers alive until the approved dependent lifecycles finish. Reuse the small phase ledger and preservation checks; do not build a migration framework. The two failed attempts rule out treating `--no-hooks` alone as the final procedure, and the vendor hooks require explicit effect review before any use. The current production removal set remains empty.

Before populating production actions, record each API group/version/resource, kind, namespace/name, **UID and resourceVersion**, owner/writer, required consumers, approved disposition/replacement, phase/order, propagation policy, expected descendants and preservation assertion. Every propagated deletion is an explicit allowlist member. Include chart hooks and release Secrets as named decisions. Record the canonical JSON allowlist SHA-256 and procedure/document SHA-256; bind review and approval to both and to the current attempt.

For an individually approved deletion, use Kubernetes `DeleteOptions` with `preconditions.uid` and `preconditions.resourceVersion`, plus the approved propagation policy. A read/check followed by an unrestricted Helm batch is not atomic UID enforcement. Derive exact native API paths from current discovery. Revalidate each phase's resourceVersion against a fresh observation while preserving the original UID; retain each new phase as a new record. No wildcard namespace/CRD deletion, all-resource selector or blanket finalizer stripping is retirement scope. Any finalizer intervention names one object, one finalizer, the observed blocking dependency and a separate approved action. Do not delete a CRD before every instance and its consumer/propagation decision is accounted for.

## Ordered phases for review

1. Bind accepted 4.26 evidence to an exact 4.27 attempt. Administrator names operator, window/outage owners, incident commander, stop/recovery owner and affected workload-owner acknowledgements. Current signed 4.0 proofs, policy freshness/final-validation bindings, immutable off-node readback, a **fresh production etcd restore through Story 4.0's route**, and matching node recovery must independently pass before 4.27. Earlier approval of the replacement or upgrade does not approve these removals.
2. Capture source health and exact protected namespaces, workloads/replicas, PVC/PV UIDs/bindings/reclaim policies, CNI/DNS/storage/ingress/identity dependencies and authenticated application outcomes. Independently read/decrypt encrypted configuration exports. Rehome required controllers/configuration/ownership before their reconciler is removed; retain unaccepted objects and stop on an unknown required consumer.
3. Retire individually approved console routes/extensions and admission/API-service dependencies in the rehearsed dependency order. Bind a separate exact expected-removal set to every phase and compare deletions/protected identities immediately after it; an unexpected extension-controller cleanup stops before core removal. Preserve public application/OIDC contracts. Complete each reviewed custom-resource finalizer/ownership lifecycle while its required controller still operates. Source WorkspaceTemplate/Workspace/namespace ownership and consumers must be explicitly dispositioned before any such deletion; this fixture conveys no orphaning, reownership or finalizer-edit approval. Unknown effects keep that action blocked. Confirm required native admission remains healthy before disabling any manager controller. Licensed KubeSphere application API writes must not be required.
4. Remove only reviewed core release resources using the UID/resourceVersion-bound native action sequence. Before each DELETE, compare the fresh object with its reviewed allowlist entry (UID and resourceVersion, or a reviewed content digest); stop and re-review on any difference. The fixture procedure enforces only part of this ([reviewed-entry binding](#dependency-first-fixture-qualification-2026-10-02)). It stops on a changed UID or on any difference in the sanitized reviewed projection, but a changed resourceVersion with an unchanged projection proceeds, so a change confined to spec, data, RBAC rules or non-management labels and annotations is not detected. Fixture attempt `20261003t073627z` executed that partial check. A 4.27 executor must also compare a reviewed content digest held in private custody, or require the reviewed resourceVersion unchanged. Handle each named finalizer/owner propagation decision separately. Namespace/PVC/PV and unrelated shared infrastructure remain protected. Compare observed deletions after **every phase** with the exact allowlist and original protected identities/bindings; unexpected deletion, drift, controller recreation or failed smoke stops the attempt.
5. Retain or separately retire each exact CRD/instance/RBAC/configuration artifact with its documented owner and disposition. A retained inert archive requires a named custodian and no live runtime/admission blocker. Verify retired release/controllers/routes are absent and remaining admission/DNS/CNI/storage/native API/private admin are healthy. Public-console denial and authenticated Keycloak/OpenBao/Memories/Forgejo outcomes must pass.
6. Sign acceptance with the actual change set, protected before/after identities/bindings, health/access results, allowlist/procedure digests and incident outcomes. Missing/failed/unsigned observations keep 4.27 incomplete.

## Stop, recovery and upgrade handoff

Drift, missing/inaccessible discovery, stale/unsigned recovery, unknown owner/consumer, unreviewed propagation, missing current authority, unexpected deletion, failed health or the window ending closes further mutation. Retain diagnostics and the intact source. Diagnose and recover existing intact services under the accountable recovery owner; retain every source PVC. A replacement target/cutover/fencing or old-point etcd/data recovery requires its separately approved decision. Do not improvise kubeadm downgrade, overwrite live volumes, remove arbitrary finalizers or restore an old etcd point into a running cluster. If an old point is used on an isolated approved target, reconcile retired controllers and current revocations before reopening authority.

After accepted retirement, create a fresh external-etcd point and matching node/configuration evidence. Revalidate all 4.0/4.1 currency, admission/operator compatibility, Forgejo consistency, storage and authenticated health gates. Preserve the historical [MAINTENANCE.md](../kubernetes-upgrade/MAINTENANCE.md), currently SHA-256 `834be8225ca05175e7cbf7c6d4ee35324401c196002002269223b9000a97d8b5`. Target **Kubernetes 1.36.5 through a qualified 1.35 patch**; a later 1.37 requires Rancher support and fresh qualification. Derive its successor from the actual remaining census and obtain approval of that exact successor digest/outage scope. This package does not overwrite the proposal, approve a hop or provision Rancher.

## Standalone procedure evidence

[Attempt 20261001t204952z-rehearsal](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t204952z-rehearsal/attempt.json) executes its exact recorded procedure SHA-256 `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0`. Its standalone synthetic native checks and exact cleanup pass; the prior byte mismatch is superseded for that revision. The [verification receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t205700z-current-procedure/verification.json) retains the executed bytes encrypted and binds the fixture-only allowlist and preservation assertions. Synthetic-recipient encryption is not independent Administrator custody proof. Actual core/extension uninstall was not repeated, and its finalizer/propagation/health/recovery blockers remain unchanged. No production removal action is approved.

**Superseded (2026-10-02 morning).** The fixture's schema-failure handling then changed the procedure SHA-256 to `9f84f36382efecec137f2dad936421c12e48e6afbbb956330470e12df169716b`. Malformed source identities now stop before fixture allocation; malformed successful Docker/native observations retain a sanitized failure receipt and continue exact cleanup. All 48 current unit tests pass. This revision has no full fixture execution; the historical attempt above proves its recorded prior bytes only. Native deletion and preservation behavior is unchanged, and the failed actual KubeSphere retirement remains incomplete.

## Dependency-first fixture qualification, 2026-10-02

`rehearse.py` now retires the actual `ks-core` 1.2.4 (application 4.2.1) fixture without Helm uninstall or vendor hooks. Before sending any request, it derives an exact scope and splits it into ordered, child-first phases. It records the allowlist and phase digests. Each phase reads every root fresh, compares it with the root's reviewed allowlist entry, and sends a native DELETE with the planned UID, the resourceVersion just read and Foreground propagation. It then waits for the declared expected set, waits again until two state polls 10 s apart match, and compares the result with the baseline. Any of the following stops the attempt:

- an unexpected removal or an incomplete one;
- a retired key that reappears;
- a change to a protected Namespace, PV, PVC or StorageClass, which now includes management labels.

Objects created during the attempt may disappear; they are reported separately.

**Reviewed-entry binding (review loop 2).** The executed `42e8e6ef…` run compared only the UID: each DELETE carried the resourceVersion read just before the request, so drift between review and DELETE went undetected. In that run, 13 of 178 requests carried a resourceVersion newer than the planned one: `User/admin`, `Extension/ks-console-embed`, `Repository/extensions-museum` and ten extension `Category` objects. The current procedure compares each fresh read with the root's reviewed allowlist entry, the baseline projection:

- A different UID stops the attempt (`uid-drift-before-native-delete`).
- An unchanged resourceVersion is sent as the DELETE precondition, so the server enforces the reviewed state.
- A changed resourceVersion is allowed only if the sanitized reviewed projection is identical apart from the resourceVersion. The projection covers owners, finalizers, management labels, Helm release, deletion state and references. It omits spec, data, RBAC rules, status and non-management labels and annotations, so an identical projection does not prove status-only churn: a change confined to those fields passes (review loop 3, G49). Any projected difference stops the attempt with `reviewed-entry-drift-before-native-delete`.
- A 409/Conflict retry re-reads the object and compares it with the reviewed entry again.

Each request record keeps the reviewed and sent resourceVersions, whether they differ, and the reviewed projection digest. In the [current-procedure rerun](#current-procedure-fixture-rerun-2026-10-03), the same 13 roots carried newer resourceVersions and each one passed the reviewed-projection comparison. So between review and DELETE, the controllers changed only fields outside the sanitized projection; whether those were status, spec or data fields was not observed. No projected field changed and nothing stopped. **Production requirement:** keep this comparison, add a reviewed content digest held in private custody (or require an unchanged resourceVersion) so that spec, data and rules drift also stops, and re-review on any stop.

**Scope.** The scope is built from these exact sources. Protected kinds never enter it:

- Helm-managed `ks-core`/`ks-console-embed` objects and their release records.
- The controller-created `Workspace/system-workspace`.
- The installed extension's leftover helm-executor RBAC and ServiceAccount.
- Webhook and APIService registrations whose backend Service is retired. This covers the dynamic, non-Helm `validator.license.kubesphere.io`, which nothing removes with its backend.
- The ownerReference closure of all of the above.
- Explicit counterparts that controllers remove with their source object:
  - the same-name Endpoints of a deleted Service;
  - service-account token Secrets, linked by annotation;
  - IAM projections linked by `iam.kubesphere.io/workspacerolebinding-ref`, `iam.kubesphere.io/user-ref` and `kubesphere.io/username`.

Routes are never added to the scope by inference. Loop 4 adds only the four explicitly approved console residue identities described above; other consumers still stop planning. If an Ingress or HTTPRoute outside the scope sends traffic to a retired Service, planning stops with `route-consumer-of-retired-service-outside-scope` until that route is closed or repointed under its own decision (review loop 2). The fixture has no out-of-scope route, so the rerun shows only that the check does not misfire; the refusal path itself has unit verification only.

| # | Phase | KubeSphere controllers | Exact fixture removals | Observed controller or native effect |
| --- | --- | --- | --- | --- |
| 1 | installed-extension | running | 12 | One InstallPlan request. The controller uninstalls the embedded console release, including its owned install Job/Pods |
| 2 | global-role-bindings | running | 6 | Owned RBAC ClusterRoleBinding garbage-collected |
| 3 | workspace-role-bindings | running | 7 | Controller removes 6 label-linked RoleBindings in the system namespaces |
| 4 | workspace-roles | running | 8 | Owned `kubesphere:iam:system-workspace:*` ClusterRoles garbage-collected |
| 5 | kubesphere-cluster-role-bindings | running | 2 | The user-linked IAM binding and its owned RBAC projection, removed before their user |
| 6 | users | running | 2 | Controller deletes `Secret/kubeconfig-admin` |
| 7 | global-roles | running | 7 | Finalizers completed by controller |
| 8 | kubesphere-service-accounts | running | 2 | Controller deletes the annotation-linked token Secret |
| 9 | catalog-extension | running | 2 | Embedded-console Extension/ExtensionVersion |
| 10 | extension-repository | running | 125 | Controller completes `extension-protection` finalizers on 52 Extensions and 72 versions |
| 11 | application-store | running | not present | Production-only; the egress-fenced fixture cannot sync applications, so this phase is **unrehearsed** |
| 12 | admission | running | 14 | Helm webhook configurations, removed before the backends they call |
| 13 | controllers-and-services | removed in this phase | 25 | Four Deployments with their ReplicaSets/Pods, Services, Endpoints and EndpointSlices |
| 14 | reconciled-admission | absent | 1 | Dynamic `validator.license.kubesphere.io`. A running manager recreates it, so it is retired afterwards; allowed only because every webhook in it is `failurePolicy: Ignore` (APIServices refused) |
| 15 | remaining-release-objects | absent | 131 | 129 native requests; no finalizers allowed after controller removal |
| 16 | release-records | absent | 1 | Helm release Secret; Helm no longer tracks the release |
| 17 | system-workspace-finalizers | absent | 2 | DELETE, then one named `kubesphere.io/cascading-deletion` removal per object |

**Result.** [Attempt `20261002t121114z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t121114z-rehearsal/kubesphere-retirement-result.json) executed procedure SHA-256 `42e8e6efc64ed6e383509ca461556d566ccf836473bbad208e6031ed47580b7c`, the `rehearse.py` bytes current at execution, and passed. Tenant sync took 3 s. From 660 baseline objects it made 347 exact removals in 16 executed phases; the fixture has no application store. There were zero unexpected, incomplete, recreated or transient removals. Every protected namespace/PV/PVC/StorageClass identity, binding, label and finalizer is unchanged, the synthetic canary is intact, and all 43 retained CRDs are unchanged, including their resourceVersions. Post-retirement health found no stale webhook/APIService/CRD-conversion reference and no manager workload or pod. A native ConfigMap write/delete and a new-namespace create/delete completed without any KubeSphere label or finalizer. The only finalizer interventions removed `kubesphere.io/cascading-deletion` once each from `WorkspaceTemplate/system-workspace` and `Workspace/system-workspace`. 26 KubeSphere-marked objects remain:

- six system namespaces, with labels and an inert `kubesphere.io/cascading-deletion` finalizer;
- `Cluster/host`, with an inert `finalizer.cluster.kubesphere.io`;
- 15 license-quota Secrets;
- the native `default` ServiceAccounts and `kube-root-ca.crt` ConfigMaps in the two manager namespaces.

Allowlist SHA-256 is `8928485490cd4eb4aa883eb4739d7d25e5200fdac88236f8c875268a74ea3388`. This run proves only its recorded procedure digest `42e8e6ef…`. Review loop 2 then changed `rehearse.py`, first to the interim `1e723b75…` and then to SHA-256 `1ac666c06c93ecb619e737cce198204f8a8d4644a9e1382aa3e626b2a1d6d2f9`. The changes are the stale-route health check, the plan-time route-consumer refusal, the reviewed-entry DELETE binding, additional inventory kinds, broader fail-closed handling, labelled declarations, tool identities, the CRD-cache reset and synthetic-hold handling. Attempt `20261003t073627z` [executed those bytes](#current-procedure-fixture-rerun-2026-10-03). [Cleanup](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t121114z-rehearsal/cleanup.json) proves node/network/volume/image-alias/credential absence.

**Attempt chain.** Every attempt is retained, failed ones included, and cleanup passed in each:

| Attempt | Outcome | Correction |
| --- | --- | --- |
| [`20261002t093116z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t093116z-rehearsal/summary.json) | Failed closed: `kind create` exceeded its 180 s bound before any KubeSphere step | Bound raised to 300 s |
| [`20261002t093503z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t093503z-rehearsal/retirement-phase-03-workspace-role-bindings.json) | Phases 1–2 passed; phase 3 stopped on 6 label-linked RoleBindings | Workspace-binding link added |
| [`20261002t095302z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t095302z-rehearsal/retirement-phase-06-users.json) | Phases 1–5 passed; phase 6 stopped on `kubeconfig-admin` | Username and token-annotation links added |
| [`20261002t101306z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t101306z-rehearsal/failure.json) | Probe Workspace not materialized within 60 s | Wait raised to 180 s; refuse before creating the member namespace |
| [`20261002t102420z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t102420z-rehearsal/failure.json) | Phases 1–12 passed; the tool's own manager-absence gate fired at the start of phase 13 | Gate defect fixed (`managerAbsentRequired`) |
| [`20261002t104904z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t104904z-rehearsal/post-retirement-native-health.json) | All 16 phases and the final preservation comparison passed; the health gate found the stale dynamic license webhook | Backend-linked admission rule added |
| [`20261002t113254z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t113254z-rehearsal/failure.json) | KubeSphere never materialized `Workspace/system-workspace` (tenant sync), so the probe was refused | Representativeness gate: wait up to 300 s for tenant sync, otherwise fail before the probe |
| [`20261002t114621z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t114621z-rehearsal/retirement-phase-12-admission.json) | Tenant sync in 3 s; phases 1–10 passed; the controller **recreated** the license webhook deleted in phase 12 | `reconciled-admission` phase after controller removal, with an `Ignore`-only guard |
| [`20261002t121114z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t121114z-rehearsal/kubesphere-retirement-result.json) | **Passed** all 16 executed phases, the final comparison and the health gate | Procedure bytes current at execution; review loop 2 followed |
| [`20261003t073627z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t073627z-rehearsal/kubesphere-retirement-result.json) | **Passed** with the review loop 2 procedure `1ac666c0…`: 347 exact removals in 16 phases; 13 changed resourceVersions all passed the reviewed-projection comparison | Procedure bytes current at execution; no code change before the run; review loop 3 followed |

**Workspace propagation probe.** In each probe that ran (6 attempts), deleting a synthetic WorkspaceTemplate while controllers were running did not delete its member namespace. Instead, the controller removed the namespace's `kubesphere.io/workspace` label and `kubesphere.io/cascading-deletion` finalizer. So a controller-processed deletion of `system-workspace` would write to the labels and finalizers of `kube-system`, `default` and the other system namespaces. The rehearsed procedure avoids those protected-namespace writes: it deletes `system-workspace` only after the controller is gone, with one named finalizer intervention per object. Before the 2026-10-03 decision, the trade-off was that the system namespaces kept an inert `kubesphere.io/cascading-deletion` finalizer and workspace labels; 7 namespaces in production. Deleting such a namespace later needs a separate, named, per-namespace finalizer decision. Letting the controller unbind them is the alternative. That alternative writes protected-namespace metadata, needs owner approval, and was not rehearsed on `system-workspace` (which carries `kubesphere.io/protected-resource`).

**Historical production planning (review loop 2; superseded by the approved decisions above).** That planner **refused** the committed [2026-10-01 census](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195141z-census/inventory.json) with `route-consumer-of-retired-service-outside-scope`: the public console Ingress below still routes to the retired `ks-console` Service. If that Ingress is excluded, for example after Story 4.2 closes it, the same scope of 476 objects splits into 17 phases. That partition includes the unrehearsed `application-store` phase: Repo `builtin-stable`, 27 Applications and 90 ApplicationVersions carrying `application.kubesphere.io/cleanup` finalizers. Without that phase the planner fails closed with `finalizer-after-controller-removal`. The census was projected before Secrets carried `serviceAccountReference`, so a fresh census is required before any production plan; token-Secret counterparts are currently under-counted. Some objects outside the scope would keep KubeSphere finalizers that become inert once controllers are removed:

- 20 license Secrets (`finalizers.kubesphere.io/cleanup`);
- `User/jpiquot` and `GlobalRoleBinding/jpiquot-platform-admin`;
- the app-store Category;
- `Cluster/host`;
- the 7 system-workspace namespaces.

The subsequent 2026-10-03 decision selects named namespace intervention and inert archive retention above. Operator-user and other consumer/ownership effects remain unaccepted.

These production objects are also outside the planned scope and need owner decisions:

- the public `Ingress/kubesphere-system/kubesphere-console`, routing `kube.hexalith.com` to `Service/ks-console` port 80;
- its cert-manager chain: `Certificate/kubesphere-console-letsencrypt`, `CertificateRequest/kubesphere-console-letsencrypt-1`, `Order/kubesphere-console-letsencrypt-1-200819670` and the `kubesphere-console-letsencrypt-tls` Secret;
- `Lease/kubesphere-system/ks-controller-manager-leader-election`.

The public route must be closed under 4.2 **before production retirement**. The current derived plan includes its exact decided residue; a derived plan does not prove exposure closure. The planner refuses any out-of-scope Ingress or HTTPRoute that sends traffic to a retired Service, so the route is decided before any request rather than found by the post-retirement stale-route check. This overlaps Story 4.2's independent public-exposure closure. **Do not delete `User/jpiquot` or `GlobalRoleBinding/jpiquot-platform-admin`.** Garbage collection would remove the operator's only native cluster-admin binding (see [native authorization dependency](QUALIFICATION.md#native-authorization-dependency-2026-10-02)). The production allowlist stays empty until the owner decisions and an independent native administration path exist.

**Limits.** Fixture-only evidence, with:

- synthetic credentials;
- single-node Kubernetes v1.34.9;
- no production values, licenses, app store, operator user or `kubekey-system`;
- no authenticated application workload;
- no recovery test.

Licensed KubeSphere application APIs were not used; every request went to the native Kubernetes API. The probe's synthetic WorkspaceTemplate write is fixture setup, not part of the procedure. The [verification receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t125604z-dependency-first-verification/verification.json) binds the executed and current procedure digests.

## Current-procedure fixture rerun, 2026-10-03

With Administrator approval, [attempt `20261003t073627z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t073627z-rehearsal/kubesphere-retirement-result.json) reran the fixture with the then-current `rehearse.py`, SHA-256 `1ac666c06c93ecb619e737cce198204f8a8d4644a9e1382aa3e626b2a1d6d2f9`. The SHA-256 was confirmed before the start and is recorded in the attempt. The other inputs are the same as for `20261002t121114z`:

- the sanitized 2026-10-01 census (`84c44e0a…`);
- node image `sha256:8a9be59e…`;
- chart `a5c87fe1…`;
- retained kubectl, Helm and age with SHA-256 `90b7b905…`, `7a319dee…` and `eb7dd1b5…`;
- the synthetic fixture recipient.

The run **passed** and needed no code change:

- All six source and egress probes were blocked, and the fixture cluster UID differs from the source.
- Stale-UID and stale-resourceVersion requests returned 409/Conflict with content unchanged.
- All five KubeSphere deployments became Ready, the console InstallPlan reproduced `Installed`, and tenant sync took 3 s.
- The workspace probe again unbound its member namespace without deleting it.

The baseline has 667 objects. That is 7 more than before, because the added inventory kinds now include 5 Leases and 2 ControllerRevisions. None of them is in scope. The scope, at 347 objects in 16 executed phases, and every per-phase count in the [phase table](#dependency-first-fixture-qualification-2026-10-02) are unchanged. Allowlist SHA-256 is `76b32cf39e60ff05ce407c844e90e281acfe3134628f3b528c8ffa7970b3b22c`.

The procedure had 179 root actions:

- 1 was `absent-before-request`: `ExtensionVersion/ks-console-embed-1.2.0`, already removed by its controller.
- 178 DELETEs were accepted with no conflict retries.
- Of those, 165 carried their reviewed resourceVersion unchanged.
- The other 13 carried a newer resourceVersion, and every one passed the reviewed-projection comparison:
  - `User/admin` (1345→2704);
  - `Extension/ks-console-embed` (1657→2613);
  - `Repository/extensions-museum` (1760→3397);
  - the ten extension Categories `ai-machine-learning`, `computing`, `database`, `deepseek`, `dev-tools`, `integration-delivery`, `networking`, `observability`, `security` and `storage`.

There were no unexpected, incomplete, recreated or transient removals:

- All 13 protected Namespace/PV/PVC/StorageClass identities are unchanged, and the synthetic canary is intact.
- All 43 CRDs are unchanged, including their resourceVersions.
- The post-retirement gate found no stale admission, APIService, CRD-conversion or route reference and no manager runtime or residue. A new namespace got no KubeSphere label or finalizer, and its deletion completed.
- The only finalizer interventions were the two named `system-workspace` removals.

27 KubeSphere-marked objects remain: the 26 listed in the 2026-10-02 result plus the now-inventoried `Lease/kubesphere-system/ks-controller-manager-leader-election`.

[Cleanup](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t073627z-rehearsal/cleanup.json) passed:

- The node, network, single owned volume, both image aliases and the fresh kubeconfig are absent, and `kind delete` exited 0.
- An independent readback after the run confirms each absence and shows no remaining kind cluster or `s426` container or network.
- All 639 private exports are encrypted to the synthetic fixture recipient (tag `bHCI3g`). The private attempt directory is mode 0700 with 0600 files, and both `SHA256SUMS` files verify. The published export record carries no plaintext digests.

The 2026-10-02 fixture limits still apply, and no production allowlist or acceptance follows from this run.

## Review loop 3 changes, 2026-10-03

Review loop 3 changed `rehearse.py` to SHA-256 `4862637dc063d06742392e21c98262d16127b4a1b055c56621ecb1299adf98a5`. The procedure now hashes the PATH-resolved kind and docker binaries, records the operator only when it is a safe identity, and records the digests of the imported `evidence.py` and `qualify.py` in `attempt.json`. The reviewed-entry comparison is described accurately above; its behavior is unchanged. Native deletion, phase, finalizer and preservation behavior is unchanged. These bytes have unit verification only, and no fixture rerun followed. New tests cover the fixture start's isolation gates, the synthetic run's checks, phase settling and absence waits, the final retirement comparison, the chart digest and core readiness refusals, and cleanup's network and credential criteria.
