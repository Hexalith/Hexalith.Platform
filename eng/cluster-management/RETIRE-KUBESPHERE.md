# Story 4.27: proposed native KubeSphere retirement

**No live removal is approved.** The current production allowlist is empty because ownership, required consumers and deletion effects remain unaccepted. [Qualification](QUALIFICATION.md) supplies exact native identities and protected namespace/PVC/PV bindings; its observations do not authorize Story 4.27 or clear Story 4.1.

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

1. Bind accepted 4.26 evidence to an exact 4.27 attempt. Administrator names operator, window/outage owners, incident commander, stop/recovery owner and affected workload-owner acknowledgements. Current signed 4.0 proofs, policy freshness/final-validation bindings, immutable off-node readback, external-etcd isolated restore and matching node recovery must independently pass. Earlier approval of the replacement or upgrade does not approve these removals.
2. Capture source health and exact protected namespaces, workloads/replicas, PVC/PV UIDs/bindings/reclaim policies, CNI/DNS/storage/ingress/identity dependencies and authenticated application outcomes. Independently read/decrypt encrypted configuration exports. Rehome required controllers/configuration/ownership before their reconciler is removed; retain unaccepted objects and stop on an unknown required consumer.
3. Retire individually approved console routes/extensions and admission/API-service dependencies in the rehearsed dependency order. Bind a separate exact expected-removal set to every phase and compare deletions/protected identities immediately after it; an unexpected extension-controller cleanup stops before core removal. Preserve public application/OIDC contracts. Complete each reviewed custom-resource finalizer/ownership lifecycle while its required controller still operates. Source WorkspaceTemplate/Workspace/namespace ownership and consumers must be explicitly dispositioned before any such deletion; this fixture conveys no orphaning, reownership or finalizer-edit approval. Unknown effects keep that action blocked. Confirm required native admission remains healthy before disabling any manager controller. Licensed KubeSphere application API writes must not be required.
4. Remove only reviewed core release resources using the UID/resourceVersion-bound native action sequence. Before each DELETE, compare the fresh object with its reviewed allowlist entry (UID and resourceVersion, or a reviewed content digest); stop and re-review on any difference. The current procedure enforces this ([reviewed-entry binding](#dependency-first-fixture-qualification-2026-10-02)); that revision has unit verification only. Handle each named finalizer/owner propagation decision separately. Namespace/PVC/PV and unrelated shared infrastructure remain protected. Compare observed deletions after **every phase** with the exact allowlist and original protected identities/bindings; unexpected deletion, drift, controller recreation or failed smoke stops the attempt.
5. Retain or separately retire each exact CRD/instance/RBAC/configuration artifact with its documented owner and disposition. A retained inert archive requires a named custodian and no live runtime/admission blocker. Verify retired release/controllers/routes are absent and remaining admission/DNS/CNI/storage/native API/private admin are healthy. Public-console denial and authenticated Keycloak/OpenBao/Memories/Forgejo outcomes must pass.
6. Sign acceptance with the actual change set, protected before/after identities/bindings, health/access results, allowlist/procedure digests and incident outcomes. Missing/failed/unsigned observations keep 4.27 incomplete.

## Stop, recovery and upgrade handoff

Drift, missing/inaccessible discovery, stale/unsigned recovery, unknown owner/consumer, unreviewed propagation, missing current authority, unexpected deletion, failed health or the window ending closes further mutation. Retain diagnostics and the intact source. Diagnose and recover existing intact services under the accountable recovery owner; retain every source PVC. A replacement target/cutover/fencing or old-point etcd/data recovery requires its separately approved decision. Do not improvise kubeadm downgrade, overwrite live volumes, remove arbitrary finalizers or restore an old etcd point into a running cluster. If an old point is used on an isolated approved target, reconcile retired controllers and current revocations before reopening authority.

After accepted retirement, create a fresh external-etcd point and matching node/configuration evidence. Revalidate all 4.0/4.1 currency, admission/operator compatibility, Forgejo consistency, storage and authenticated health gates. Preserve the historical [MAINTENANCE.md](../kubernetes-upgrade/MAINTENANCE.md), currently SHA-256 `834be8225ca05175e7cbf7c6d4ee35324401c196002002269223b9000a97d8b5`. Derive its successor from the actual remaining census and obtain approval of that exact successor digest/outage scope. This package does not overwrite the proposal, approve a hop or provision Rancher.

## Standalone procedure evidence

[Attempt 20261001t204952z-rehearsal](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t204952z-rehearsal/attempt.json) executes its exact recorded procedure SHA-256 `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0`. Its standalone synthetic native checks and exact cleanup pass; the prior byte mismatch is superseded for that revision. The [verification receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t205700z-current-procedure/verification.json) retains the executed bytes encrypted and binds the fixture-only allowlist and preservation assertions. Synthetic-recipient encryption is not independent Administrator custody proof. Actual core/extension uninstall was not repeated, and its finalizer/propagation/health/recovery blockers remain unchanged. No production removal action is approved.

**Superseded (2026-10-02 morning).** The fixture's schema-failure handling then changed the procedure SHA-256 to `9f84f36382efecec137f2dad936421c12e48e6afbbb956330470e12df169716b`. Malformed source identities now stop before fixture allocation; malformed successful Docker/native observations retain a sanitized failure receipt and continue exact cleanup. All 48 current unit tests pass. This revision has no full fixture execution; the historical attempt above proves its recorded prior bytes only. Native deletion and preservation behavior is unchanged, and the failed actual KubeSphere retirement remains incomplete.

## Dependency-first fixture qualification, 2026-10-02

`rehearse.py` now retires the actual `ks-core` 1.2.4 (application 4.2.1) fixture without Helm uninstall or vendor hooks. Before sending any request, it derives an exact scope and splits it into ordered, child-first phases. It records the allowlist and phase digests. Each phase reads every root fresh, compares it with the root's reviewed allowlist entry, and sends a native DELETE with the planned UID, the resourceVersion just read and Foreground propagation. It then waits for the declared expected set, waits again until two state polls 10 s apart match, and compares the result with the baseline. Any of the following stops the attempt:

- an unexpected removal or an incomplete one;
- a retired key that reappears;
- a change to a protected Namespace, PV, PVC or StorageClass, which now includes management labels.

Objects created during the attempt may disappear; they are reported separately.

**Reviewed-entry binding (review loop 2, unit verification only).** The executed `42e8e6ef…` run compared only the UID: each DELETE carried the resourceVersion read just before the request, so drift between review and DELETE went undetected. In that run, 13 of 178 requests carried a resourceVersion newer than the planned one: `User/admin`, `Extension/ks-console-embed`, `Repository/extensions-museum` and ten extension `Category` objects. The current procedure compares each fresh read with the root's reviewed allowlist entry, the baseline projection:

- A different UID stops the attempt (`uid-drift-before-native-delete`).
- An unchanged resourceVersion is sent as the DELETE precondition, so the server enforces the reviewed state.
- A changed resourceVersion is allowed only if the sanitized reviewed projection is identical apart from the resourceVersion. The projection covers owners, finalizers, management labels, Helm release, deletion state and references; an identical projection means status-only churn. Any other difference stops the attempt with `reviewed-entry-drift-before-native-delete`.
- A 409/Conflict retry re-reads the object and compares it with the reviewed entry again.

Each request record keeps the reviewed and sent resourceVersions, whether they differ, and the reviewed projection digest. Whether the 13 drifted roots above changed only status is unknown. A fixture rerun of the current bytes could therefore stop on a controller-driven change to a reviewed field, and that stop would need its own expected-effect review. **Production requirement:** keep this comparison and re-review on any stop.

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

Routes are never added to the scope by inference. If an Ingress or HTTPRoute outside the scope sends traffic to a retired Service, planning stops with `route-consumer-of-retired-service-outside-scope` until that route is closed or repointed under its own decision (review loop 2, unit verification only).

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

Allowlist SHA-256 is `8928485490cd4eb4aa883eb4739d7d25e5200fdac88236f8c875268a74ea3388`. This run proves only its recorded procedure digest `42e8e6ef…`. Review loop 2 then changed `rehearse.py`, first to the interim `1e723b75…` and now to SHA-256 `1ac666c06c93ecb619e737cce198204f8a8d4644a9e1382aa3e626b2a1d6d2f9`. The changes are the stale-route health check, the plan-time route-consumer refusal, the reviewed-entry DELETE binding, additional inventory kinds, broader fail-closed handling, labelled declarations, tool identities, the CRD-cache reset and synthetic-hold handling. The patched bytes have unit verification only, with no fixture rerun. [Cleanup](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t121114z-rehearsal/cleanup.json) proves node/network/volume/image-alias/credential absence.

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
| [`20261002t121114z`](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t121114z-rehearsal/kubesphere-retirement-result.json) | **Passed** all 16 executed phases, the final comparison and the health gate | Procedure bytes current at execution; later review changes are unit-verified only |

**Workspace propagation probe.** In each probe that ran (6 attempts), deleting a synthetic WorkspaceTemplate while controllers were running did not delete its member namespace. Instead, the controller removed the namespace's `kubesphere.io/workspace` label and `kubesphere.io/cascading-deletion` finalizer. So a controller-processed deletion of `system-workspace` would write to the labels and finalizers of `kube-system`, `default` and the other system namespaces. The rehearsed procedure avoids those protected-namespace writes: it deletes `system-workspace` only after the controller is gone, with one named finalizer intervention per object. The trade-off is that the system namespaces keep an inert `kubesphere.io/cascading-deletion` finalizer and workspace labels; 7 namespaces in production. Deleting such a namespace later needs a separate, named, per-namespace finalizer decision. Letting the controller unbind them is the alternative. That alternative writes protected-namespace metadata, needs owner approval, and was not rehearsed on `system-workspace` (which carries `kubesphere.io/protected-resource`).

**Production planning (proposal only).** The current planner **refuses** the committed [2026-10-01 census](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195141z-census/inventory.json) with `route-consumer-of-retired-service-outside-scope`: the public console Ingress below still routes to the retired `ks-console` Service. If that Ingress is excluded, for example after Story 4.2 closes it, the same scope of 476 objects splits into 17 phases. That partition includes the unrehearsed `application-store` phase: Repo `builtin-stable`, 27 Applications and 90 ApplicationVersions carrying `application.kubesphere.io/cleanup` finalizers. Without that phase the planner fails closed with `finalizer-after-controller-removal`. The census was projected before Secrets carried `serviceAccountReference`, so a fresh census is required before any production plan; token-Secret counterparts are currently under-counted. Some objects outside the scope would keep KubeSphere finalizers that become inert once controllers are removed:

- 20 license Secrets (`finalizers.kubesphere.io/cleanup`);
- `User/jpiquot` and `GlobalRoleBinding/jpiquot-platform-admin`;
- the app-store Category;
- `Cluster/host`;
- the 7 system-workspace namespaces.

Each of these needs an owner decision: deletion while controllers still run, or explicit inert retention.

These production objects are also outside the planned scope and need owner decisions:

- the public `Ingress/kubesphere-system/kubesphere-console`, routing `kube.hexalith.com` to `Service/ks-console` port 80;
- its cert-manager chain: `Certificate/kubesphere-console-letsencrypt`, `CertificateRequest/kubesphere-console-letsencrypt-1`, `Order/kubesphere-console-letsencrypt-1-200819670` and the `kubesphere-console-letsencrypt-tls` Secret;
- `Lease/kubesphere-system/ks-controller-manager-leader-election`.

The public route must be denied or removed **before** a production plan is computed. The planner refuses any out-of-scope Ingress or HTTPRoute that sends traffic to a retired Service, so the route is decided before any request rather than found by the post-retirement stale-route check. This overlaps Story 4.2's independent public-exposure closure. **Do not delete `User/jpiquot` or `GlobalRoleBinding/jpiquot-platform-admin`.** Garbage collection would remove the operator's only native cluster-admin binding (see [native authorization dependency](QUALIFICATION.md#native-authorization-dependency-2026-10-02)). The production allowlist stays empty until the owner decisions and an independent native administration path exist.

**Limits.** Fixture-only evidence, with:

- synthetic credentials;
- single-node Kubernetes v1.34.9;
- no production values, licenses, app store, operator user or `kubekey-system`;
- no authenticated application workload;
- no recovery test.

Licensed KubeSphere application APIs were not used; every request went to the native Kubernetes API. The probe's synthetic WorkspaceTemplate write is fixture setup, not part of the procedure. The [verification receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t125604z-dependency-first-verification/verification.json) binds the executed and current procedure digests.
