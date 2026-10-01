---
review: rubric-and-prd-reconciliation
date: 2026-10-01
scope: approved Rancher course-correction slice
verdict: pass-after-fixes
unresolved_findings: 0
original_findings:
  high: 1
  medium: 1
canonical_artifacts_edited_by_reviewer: false
---

# Rancher update rubric and PRD reconciliation

The amended spine passes this scoped review after two narrow corrections. The approved management topology, deployment ownership, private access, native recovery independence, migration order and initial versus integrated recovery gates now agree. No actionable finding remains in the reviewed slice.

## Scope and evidence

Reviewed the complete approved `sprint-change-proposal-2026-10-01.md`, current Platform PRD/addendum, architecture spine and resumed memlog, new `spec-kubesphere-to-rancher/SPEC.md`, Stories 4.26–4.28, and relevant Platform spec sequencing/acceptance and operations handoff. Compared the spine's AD headings with the Git HEAD version. Re-read the author's corrections during this review.

This is a consistency review of the approved 2026-10-01 change. Unrelated existing architecture, historical brownfield observations and completed Story 4.0 proof are preserved. No canonical artifact was edited by this reviewer, and no cluster inspection, deletion, deployment or other live operation was performed. Mechanical lint was reported as passing by the caller; the reviewed spine also passes scoped `git diff --check`.

## Original findings and closure

### RB-R1 — High: the rendered spine omitted the full before-staging migration gate — closed

**Original gap.** The resumed memlog adopted `4.26 → 4.27 → 4.1 → 4.28 → staging`, and the approved proposal, PRD downstream-action row, addendum, migration spec and Story 4.28 require replacement-manager qualification before staging. The initial rendered spine only required the supported Kubernetes upgrade before staging and placed Rancher qualification in the G1 Infrastructure currency row. A builder following the spine alone could therefore begin staging after the native hop but before accepted Rancher access/initial restore evidence. Retirement before the hop and the independence of native upgrade from manager availability were also left to other artifacts.

**Disposition.** Autofix by distilling the already adopted ordering constraint into the Cluster management convention; no new policy decision or AD ID is needed.

**Verified closure.** The spine's Cluster management sequence paragraph now names 4.26 retirement qualification, 4.27 accepted retirement and recovery/preservation evidence, 4.1 fresh post-retirement inputs and all per-hop gates, and 4.28 private management/initial isolated restore before staging. It explicitly keeps public closure urgent and makes procurement/manager availability independent of native retirement or upgrade. The accepted-risk text continues to remove the former staging-on-1.34-after-EOL waiver. This agrees with the migration spec work-order table and Stories 4.26–4.28.

### RB-R2 — Medium: shared-infrastructure rules omitted the approved pre-lock operator procedure — closed

**Original gap.** The approved proposal's A2 rationale explicitly directs work before environment locks exist through an exact approved urgent operator procedure. The initial spine added Rancher/agents/management cluster to Shared infrastructure but retained an unconditional both-locks/executor path. These migration steps precede staging foundations, while staging lock/executor establishment and production attempt machinery are later owned work. Requiring that future machinery would silently add a dependency to the approved urgent native track.

**Disposition.** Autofix the Shared infrastructure change path with the limited approved pre-environment procedure. Retain the established lock/executor path once its environments exist.

**Verified closure.** The Shared infrastructure row now says that before staging/production and their locks exist, the urgent track uses its exact Administrator-approved owner-controlled procedure with current verified native recovery, preservation, access and per-hop gates. The ongoing both-locks path, G1 rehearsal requirement, forward-revert/DR responsibility and qualification checks remain. The clause supplies a bounded bootstrap path without waiving exact procedure approval or signed/fresh recovery evidence.

## PRD and approved-consequence reconciliation

| Requirement or consequence | Spine coverage | Result |
| --- | --- | --- |
| PRD management-platform scope; private maintained community manager, no vendor-activation dependency in required core functions | Rancher community convention; exact chart/image/license provenance and supported pairing held in shared profile qualification; PRD requirements remain inherited | Landed; actual selected release still requires 4.26 qualification |
| FR-9 management inventory, independent native/decryption access, fencing/reconciliation before reconnection | Cluster management recovery paragraph plus AD-12 and existing recovery inventory/fence/reissue rules | Landed |
| FR-10 manager/agents/management-cluster change and currency control | Shared infrastructure row and Infrastructure currency owned work | Landed |
| FR-10 application identities receive no management authority; NFR-3 covers UI/API/proxy/issued credentials | Existing AD-7/8 purpose-limited authority plus private manager convention, bounded agent permissions, unchanged application writers and affected NFR-3 enforcement; observable denial cases remain in 4.28 and CAP-10 | Landed |
| FR-11/deputy authority remains restricted; management grants do not derive from staging membership | Cluster management current-role reconciliation and scoped deputy/native access; existing AD-6 production-admission owner unchanged | Landed |
| Retirement before native hop; initial Rancher qualification before staging/G1; public closure remains independently urgent | New sequence paragraph and accepted-risk waiver removal | Landed after RB-R1 correction |
| Initial 4.28 management restore differs from integrated Epic 8 protection/RPO/RTO proof | New management recovery paragraph names initial access/isolated restore, later cadence/retention, fencing/revocation and measured RPO/RTO gates | Landed |
| Management-app backup does not replace workload data, workload external etcd or native management-cluster recovery | Explicit management recovery paragraph and independent native procedures | Landed |

## Good-spine rubric

- **Actual divergence points:** fixed the management/workload topology, imported-cluster distribution, release writer, Fleet exclusion, native recovery independence, principal/agent boundary, private exposure and migration order. Independent stories cannot select an incompatible manager location, writer or staging prerequisite.
- **Enforceable rules:** inventory, role/private-access tests, supported pairing, exact procedure gates, workload/PVC preservation, fencing and restored-current-authority tests have owners and acceptance evidence in 4.26–4.28 and the later referenced gates. Unqualified target identities or missing evidence remain incomplete work.
- **Operational envelope:** dedicated single-node K3s VM and generic kubeadm import are settled. Capacity, OS, placement/cost, exact endpoint/TLS/connectivity, authentication and precise version/permission inventory remain explicit 4.26 qualification inputs. Same-host placement adds no host/site resilience. This is no silent operational dimension.
- **Deferred work:** deferred execution pins and capacity choices cannot authorize mutation; their qualification and exact approval gates are explicit. No new version is advertised as a final execution pin. Retained primary-source version/fit evidence is a candidate baseline requiring current per-attempt revalidation.
- **Brownfield compatibility:** existing kubeadm identity/distribution, external-etcd procedures, workloads and native PVC/namespace identities remain authoritative. Retirement is ownership-based and does not presume a KubeSphere-to-Rancher schema conversion. The old console's removal and new manager deployment remain unevidenced backlog actions.
- **Stable decisions:** AD-1 through AD-15 IDs and titles exactly match the prior Git version. The management convention specializes those existing decisions without renumbering or weakening them. Existing application release, environment isolation and production admission boundaries remain binding.

The final verdict applies to planning coherence. It does not establish qualified pins, completed migration, passed staging/G1/G2/G3, or authorization for a live operational attempt.
