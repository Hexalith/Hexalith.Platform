---
title: Qualify Rancher and the management migration
type: story
epic: 4
story: 26
created: 2026-10-01
status: in-progress
route: dispatch
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/specs/spec-kubesphere-to-rancher/SPEC.md'
depends_on: []
---

# Story 4.26: Qualify Rancher and the management migration

As Administrator, I want an exact supported Rancher target, independent native access and an ownership-based migration plan, so that replacing management does not change workload data or silently expand authority.

**Repo:** Administrator operations and Platform planning. **Covers:** FR-9, FR-10, NFR-3, AR-59, AR-63. **Dependencies:** none for read-only preparation; current qualifying recovery evidence is required before any production mutation.

Acceptance criteria:

1. Record the live workload-cluster identity/topology, effective kubeadm/client versions, KubeSphere chart/images/configuration, extensions, CRDs and instances, owners/finalizers, API services, webhooks, RBAC, routes, namespaces and native persistent-state inventory. Retain sensitive exports encrypted outside Git.
2. Classify every KubeSphere-managed capability as unused/retired, native-owned or replaced. Identify all consumers and approved replacements. Unknown ownership, deletion effects or required functionality blocks retirement.
3. Select exact Rancher community chart/image identities, required dependency licenses and a security-current version pairing for the management cluster and target imported workload cluster. Retain dated version-specific matrices and release notes. Qualify the management installation Helm CLI and native recovery-tool identities separately from the application executor tool floor. No unqualified tag or claim that a generic import certifies manager hosting is accepted.
4. Specify the dedicated K3s VM's host, supported OS, CPU/RAM/storage budget, private DNS/TLS endpoint, route/firewall/CA requirements, independent backups and owner. Record costs, provisioning dependencies and single-node/shared-host limitations. Actual provisioning and console rollout may follow the workload upgrade.
5. Independently demonstrate authorized native Kubernetes administration and deny unauthorized/public access. Retain recovery kubeconfig/key custody independent of KubeSphere and Rancher. Do not substitute Rancher-proxy credentials for this path.
6. Map required operator accounts/roles to approved native/Rancher authority. Preserve the Administrator/deputy division, MFA requirement and least privilege; no automatic workspace-to-project, account-to-admin or staging-membership mapping is accepted. Define Administrator-approved management grant/revocation records and their independent off-site custody/complete lineage so restored authority can be reconciled after the cut; missing or conflicting authority fails closed.
7. Rehearse the version-specific uninstall against a representative isolated inventory. Produce an exact proposed resource allowlist, ownership/deletion-propagation result, dependency order, workload-preservation assertions and stop/recovery procedure. Demonstrate that licensed KubeSphere application writes are not needed to complete the qualified native retirement procedure.
8. Deliver a separately reviewable retirement runbook, revised upgrade ordering and 4.28 installation/access/backup plan. Qualification evidence alone neither authorizes live deletion nor clears Story 4.1. Story 4.26's retirement deliverable may complete while management-VM procurement remains scheduled for 4.28.

## Implementation handoff

Administrator approved the [course correction](../planning-artifacts/sprint-change-proposal-2026-10-01.md). Read-only native inspection, public artifact checks and isolated synthetic/vendor-chart fixtures have now executed under the [implementation spec](spec-4-26-qualify-rancher-and-the-management-migration.md). Use the [migration spec](../specs/spec-kubesphere-to-rancher/SPEC.md) and [operations handoff](../../eng/cluster-management/README.md). Retain exact identities, sanitized attempt records and encrypted private inventories; require acceptance evidence and applicable exact procedure approvals before mutation or completion. Production was not mutated; Story4.26, retirement and upgrade gates remain incomplete.

## Criterion evidence and remaining gates

All eight original criteria remain **incomplete**. Execution evidence distinguishes observations and unapproved proposals from accepted qualification.

| Criterion | Executed evidence | Evidence still required |
| --- | --- | --- |
|1: Native census/custody | [Census](evidence/epic-4/4-26/20261001t173902z-census/inventory.json): Kubernetes1.34.9, 2,594 unique UIDs, zero API discovery/list failures, installed core/console releases, CRDs/instances/owners/finalizers/webhooks/RBAC/routes/storage; raw exports encrypted outside Git | Working operator SSH login for current effective kubeadm/external-etcd/host facts; independently readable off-node export custody/signatures; fresh pre-action drift comparison |
|2: Ownership/disposition | [Capability proposal](evidence/epic-4/4-26/20261001t181200z-capability-review/capability-dispositions.json) maps eight capability cohorts, exact observed members, consumers and deletion effects; confirms installed console plan separately from catalog | Owner-approved complete consumers/replacements, named shared dependencies and proven propagation/finalizer effects. Production allowlist remains empty |
|3: Supported target/tools/licenses | Dated separate hosting/import matrices; Ranchercommunity2.15.2 chart/server/agent identities, current K3s1.35.9+k3s1 API stable flag, native1.35.9 direction, [artifact checks](evidence/epic-4/4-26/20261001t181000z-public-verification/artifact-checks.json), [dependency checks](evidence/epic-4/4-26/20261001t183042z-dependency-review/dependency-checks.json), licenses and advisory ranges | Complete required image/transitive license/security review, authenticity and procedure/restore compatibility; revalidated exact OS/patch/import definitions. No candidate is accepted |
|4: Host/private VM/cost | [Rancher plan](../../eng/cluster-management/RANCHER.md): three placement choices, conditional recommendation, Ubuntu24.04/4vCPU/16GiB/80GiB proposal, private DNS/TLS/firewall/backups/owners/cost assumptions | Current hypervisor/virtualization/reservations/disk capacity and approved host/address/CA/cost/owner decisions. Provisioning remains scheduled for4.28 |
|5: Independent native administration | [Native access](evidence/epic-4/4-26/20261001t173902z-census/access.json): authorized direct CA-verified reads, credential-free HTTP403; resolved Linux kubeconfig owner mode0600 | Maintenance/recovery capability, distinct unauthorized authenticated and public denial tests, independent effective custody/readback/MFA. Symlink0777 is not public-access proof |
|6: Scoped authority/restoration | [Qualification plan](../../eng/cluster-management/QUALIFICATION.md): explicit Administrator/deputy split, no automatic mappings, cryptographic current lineage and post-cut revocation/source-loss restore procedure; negative metadata validation tests | Actual approved accounts/scopes/MFA and authenticated complete independent grant/revocation lineage; quarantined restore/current revocation denial. Validators do not authenticate grants |
|7: Representative native retirement | [Standalone native result](evidence/epic-4/4-26/20261001t180843z-rehearsal/native-result.json) passes bounded synthetic UID/RV/finalizer/preservation checks. Actual core/embedded console all Ready; native extension deletion completed. [Diagnostic follow-up](evidence/epic-4/4-26/20261001t184031z-rehearsal/kubesphere-uninstall-observation.json) records core deployments removed,151 expected retirements retained,17 Terminating CRs with finalizers, and an unallowlisted extension Job Pod removal; cleanup passes | Earlier exact custom-resource finalizer/ownership and phase-propagation review while controllers still operate; source ownership/consumer decisions, representative production configuration, authenticated workload/canary and independent recovery evidence. No-hooks procedure remains unqualified |
|8: Reviewable handoff/order | Separate [retirement](../../eng/cluster-management/RETIRE-KUBESPHERE.md), [Rancher/access/backup](../../eng/cluster-management/RANCHER.md), [README](../../eng/cluster-management/README.md) and this explicit criterion ledger; historical hashed upgrade proposal preserved | Review/acceptance of the evidence-bound deliverables and applicable current recovery/procedure decisions. Qualification does not authorize4.27 or clear4.1 |

## Post-review follow-up

The fresh standalone run and current procedure have distinct recorded digests: the current revision adds only a three-line malformed-mount guard after the run began. Exact executed bytes are retained encrypted, native deletion/preservation code is unchanged, and current-code tests plus replay of the saved synthetic mount metadata pass. A full fixture rerun after that edit is not claimed.

All45 unit tests pass after the custody, consumer/discovery, propagation/preservation and cleanup corrections. The [fresh read-only census](evidence/epic-4/4-26/20261001t195141z-census/inventory.json) captures2,593 unique UIDs and nine all-state Helm releases, adds27 KubeSphere-marked identities to unresolved ownership review, and explicitly closes on70 missing UID/resourceVersion observations. [Targeted native metadata](evidence/epic-4/4-26/20261001t195456z-native-identities/identity-observations.json) records the successful status/metrics/default-Calico views behind those entries; they are not native deletion identities. All80 protected identities and previously projected binding/storage properties match the earlier capture; new fields have no earlier comparison proof.

The [fresh standalone fixture](evidence/epic-4/4-26/20261001t195601z-rehearsal/native-result.json) passes real stale-UID and stale-resourceVersion409/Conflict rejection, unchanged current content, child-first/named synthetic finalizer deletion, protected storage/canary and explicit node/network/volume/image-alias/credential cleanup. It does not repeat the actual KubeSphere runtime; the17 Terminating CRs and unreviewed phase deletion remain blockers. The [post-review verification](evidence/epic-4/4-26/20261001t200342z-post-review/verification.json) binds current artifact/procedure digests and limits. One generated agent-context gate repair is [deferred](deferred-work.md); the operations handoff preserves the existing accepted4.2/4.3 requirement before staging. All eight original criteria and this story remain incomplete.

## Current standalone procedure follow-up

The [new standalone rehearsal](evidence/epic-4/4-26/20261001t204952z-rehearsal/native-result.json) now executes the exact current procedure, including the later malformed-mount guard. Source refusal, both native409/Conflict races with unchanged content, child-first/named synthetic finalizer deletion, protected storage/canary and [exact cleanup](evidence/epic-4/4-26/20261001t204952z-rehearsal/cleanup.json) pass. [Verification](evidence/epic-4/4-26/20261001t205700z-current-procedure/verification.json) retains exact encrypted procedure bytes and digest attribution. Its 102 encrypted fixture exports use the existing synthetic fixture recipient, with no Administrator/off-node custody or independent decryption claim. This supersedes only the earlier standalone execution mismatch. Actual KubeSphere retirement, representative configuration/health/recovery, host/access/authority decisions and all eight original criteria remain incomplete; story/sprint remain `in-progress`.

## User-authorized access evidence

The user's [approval to create evidence](evidence/epic-4/4-26/20261001t205847z-native-access/authorization.json) is retained separately from grants and acceptance. [Native checks](evidence/epic-4/4-26/20261001t210315z-native-authority/native-access.json) authenticate `jpiquot` against the same direct TLS cluster, observe eight allowed native policy decisions, and retain private-network credential-free HTTP 403 and invalid-bearer HTTP 401. No persistent resource or role changed. Distinct authenticated unauthorized/public denial, MFA, independent custody and current signed grant/revocation lineage remain unverified.

The host's public ED25519 key was retrieved at the user's request. The supplied local identity and two discovered Windows identities fail SSH authentication as `jpiquot`; [diagnostics](evidence/epic-4/4-26/20261001t210432z-ssh-identity-check/host-access.json) confirm that both Windows public keys were offered and rejected. No host facts or capacity were inferred. Sensitive outputs use the retained production evidence recipient outside Git; no independent custody acceptance is claimed. All eight criteria and story/sprint remain `in-progress`.

[Follow-up verification](evidence/epic-4/4-26/20261001t211014z-qualification-followup/verification.json) binds the updated handoff and verifies local manifests/ciphertexts, the actual 45 passing unit cases without skips, and the frozen matrix. Build completion remains pending operational acceptance; these records do not clear retirement or upgrade gates.

## Resumed implementation verification

The 2026-10-02 implementation closes a fixture evidence gap: malformed source identities stop before allocation, and malformed successful Docker/native schemas now retain sanitized failure receipts while cleanup and summary finalization continue. Three new regression cases pass with the prior 45, for 48 executed tests without skips. Current `rehearse.py` SHA-256 is `9f84f36382efecec137f2dad936421c12e48e6afbbb956330470e12df169716b`; these changes have unit verification only. The historical standalone run proves its recorded `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0` bytes, and no full fixture rerun or actual KubeSphere retirement follows this edit. All eight original criteria remain incomplete; story/sprint remain `in-progress`.

[Resume verification](evidence/epic-4/4-26/20261002t051917z-resume-verification/verification.json) binds the current artifacts, independent 48-case implementation check and local evidence integrity. Working host access, approved ownership/authority, target qualification, representative retirement and independent custody/recovery remain required under the criterion ledger above.
