---
title: 'Start the urgent Kubernetes upgrade track and split its backup gate'
type: 'chore'
created: '2026-09-28'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '7cbc4c528acfa4b784b4f6fed56a732380221304'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Epic 4’s date-bound cluster work is still in backlog while Kubernetes 1.34 reaches end of life on 2026-10-27. Story 4.1 also combines a hard pre-upgrade gate—off-node backups and isolated restore proofs for Keycloak PostgreSQL, OpenBao, and Memories—with the disruptive in-place upgrade, even though those proofs do not exist today.

**Approach:** Add Story 4.0 as the independently executable backup-and-restore prerequisite, retain the existing 4.1–4.3 identities, and activate Stories 4.0–4.3 immediately. Create implementation story files and sanitized discovery evidence so backup work, upgrade preparation, exposure closure, and runner relocation can proceed independently while the actual Kubernetes mutation remains blocked until Story 4.0 is done.

## Boundaries & Constraints

**Always:** Preserve the five pre-existing dirty submodules unchanged. Keep Story 4.1’s upgrade execution fail-closed on all three signed restore proofs. Preserve original Story 4.1–4.3 numbering by using numeric prerequisite Story 4.0. Treat live-cluster inspection as evidence, redact secret values, and distinguish observed state from completed acceptance. Mark Epic 4 and Stories 4.0–4.3 `in-progress` because work and discovery have begun.

**Never:** Do not run `kubeadm upgrade`, drain or reboot the node, delete or replace PVCs, expose credentials, claim a successful restore from backup-job success alone, close an administrative route without a usable Administrator path, disable registry anonymous reads before consumers have verified credentials, or remove the in-cluster runner before an external runner passes jobs without cluster access.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Story split | C-15 and existing Story 4.1 | Story 4.0 owns all three backup/restore proofs; 4.1 depends on 4.0 and owns only upgrade/preflight/post-check work | Any missing proof keeps the upgrade gate closed |
| Existing identities | Current Stories 4.1–4.3 and downstream cross-references | Their numbers and sprint keys remain stable | Do not renumber the remainder of Epic 4 |
| Live inventory | Cluster context `jpiquot@local` | Sanitized, timestamped evidence records observed versions, backup gaps, exposures, and runner placement | Omit Secret data and label incomplete observations explicitly |
| Sprint activation | Epic 4 and Stories 4.0–4.3 | All become `in-progress`; later stories remain unchanged | YAML must retain every existing key and parse successfully |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/epics.md` -- authoritative Epic 4 story definitions; add 4.0 and narrow 4.1 without renumbering later stories.
- `_bmad-output/planning-artifacts/implementation-readiness.md:48` -- C-15 source finding; leave historical finding intact and record its disposition in the story artifacts.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- add the 4.0 key and activate Epic 4 plus 4.0–4.3.
- `_bmad-output/implementation-artifacts/epic-4-context.md` -- compiled context; align the story list and dependency wording with the split.
- `_bmad-output/implementation-artifacts/4-0-*.md` through `4-3-*.md` -- new implementation stories with actionable tasks, hard gates, evidence requirements, and current observations.
- `_bmad-output/implementation-artifacts/evidence/epic-4/initial-cluster-inventory.md` -- sanitized baseline from read-only Kubernetes and external negative probes.
- `references/Hexalith.Memories/docs/operations/backup-restore.md` and `tools/verify-backup-recovery.py` -- reuse the module-owned Redis/FalkorDB backup and verification contracts; do not invent a competing recovery test.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/planning-artifacts/epics.md` -- split backup proofs into Story 4.0 and make Story 4.1 consume its completion evidence.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` and `epic-4-context.md` -- add and activate the urgent track consistently.
- [x] `_bmad-output/implementation-artifacts/4-0-*.md` through `4-3-*.md` -- create implementation-ready story artifacts grounded in the live inventory and upstream constraints.
- [x] `_bmad-output/implementation-artifacts/evidence/epic-4/initial-cluster-inventory.md` -- record the sanitized 2026-09-28 baseline and explicit blockers.

**Acceptance Criteria:**
- Given C-15, when Epic 4 is read, then backup/restore proof is an independent Story 4.0 and Story 4.1 cannot execute the upgrade until 4.0 is done.
- Given the sprint tracker, when parsed, then Epic 4 and Stories 4.0–4.3 are `in-progress`, their keys exactly match their story artifacts, and all unrelated statuses are unchanged.
- Given the story files, when an operator begins work, then each names concrete resources, ordered gates, evidence outputs, stop conditions, and the dependencies that require external infrastructure or an Administrator decision.
- Given the discovery evidence, when reviewed, then it records Kubernetes v1.34.9, zero CloudNativePG backup resources, node-local OpenBao snapshots, absent CSI snapshot APIs, public admin surfaces, anonymous Zot reads, and the privileged in-cluster runner without exposing credentials.

## Implementation Notes

- Added Story 4.0 as the sole owner of the Keycloak PostgreSQL, OpenBao and Memories off-node backup and isolated-restore proofs. Story 4.1 now validates its signed aggregate gate before any cluster mutation.
- Activated Epic 4 and Stories 4.0–4.3 without changing later story identities or unrelated sprint states.
- Captured read-only observations at `2026-09-28T12:51:19Z` from context `jpiquot@local`. No Secret objects or data were queried and no live resource was changed.
- The story artifacts distinguish completed discovery from open operational acceptance and name Administrator/infrastructure decisions, ordered tasks, evidence outputs and fail-closed stop conditions.

## Spec Change Log

- 2026-09-28: Implemented the approved urgent-track split, activation, story artifacts and sanitized initial inventory.

## Review Triage Log

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| BH-01 | medium | `epics.md:2` does erase the completed workflow markers, but `git blame` attributes it to concurrent `/pushall` commit `63af0c72`, not this urgent-track implementation. Defer as pre-existing/concurrent work. |
| BH-02 | false | The opening worktree evidence showed five already-dirty submodules; `/pushall` subsequently recorded their existing checked-out commits plus FrontComposer in the parent repository. This implementation did not edit or move the protected submodule worktrees, so the claimed preservation violation does not occur. Reject. |
| BH-03 | false | No repository dispatcher consumes `depends_on`/`blocks`, and Story 4.14 has no implementation artifact yet. The authoritative Epic 4 prose already places Stories 4.0–4.3 before staging; there is no demonstrated metadata bypass to fix. Reject. |
| BH-04 | medium | `epics.md:611` still accepts staging on 1.34 after the EOL date while Story 4.1 requires the upgrade before staging. The conflicting AR-64 text predates the baseline and was not changed by this story. Defer for planning reconciliation. |
| BH-05 | medium | Story 4.0's singular `artifactSha256` does not explicitly bind every CNPG, WAL, Redis, FalkorDB and logical-export object. Patch the proof contract to bind a signed manifest and every object checksum. |
| BH-06 | medium | Story 4.0 requires second-operator validation but its evidence schema omits validator identity, time, result and signature. Patch the gate evidence contract. |
| BH-07 | high | The proof schema cannot demonstrate the required encryption, immutability, retention and off-node failure domain even though those properties gate a destructive upgrade. Patch the schema and Story 4.1 validation. |
| BH-08 | medium | `capturedAt` and evidence expiry do not prove the recovery point satisfies its approved RPO. Patch each proof with the data cutoff, approved RPO and compliance result. |
| BH-09 | high | New namespaces/storage alone do not prove restore isolation from live identity, network, DNS or credentials, and cleanup is not executed. Patch concrete negative-isolation checks and cleanup evidence. |
| BH-10 | medium | The Memories flow pauses intake but has no successful resume, backlog/in-flight reconciliation or post-resume health requirement. Patch the ordered workflow and evidence. |
| BH-11 | medium | Counts plus one Keycloak smoke test can miss row/content corruption. Patch non-secret source/restored integrity invariants or checksums. |
| BH-12 | high | Story 4.1 accepts an external-etcd snapshot and procedure without integrity verification or a current isolated-restore proof. Patch the mutation gate and evidence requirements. |
| BH-13 | high | External etcd alone does not cover kubeadm configuration, static-pod manifests or cluster PKI needed to reconstruct the sole control plane. Patch a protected node/control-plane recovery bundle and restore procedure. |
| BH-14 | high | Kubeadm planning does not establish compatibility for Calico, CoreDNS, kube-proxy, OpenEBS, containerd, webhooks, operators or removed APIs. Patch per-hop compatibility gates. |
| BH-15 | medium | Existing mounted PVC health can pass while dynamic provisioning is broken. Patch a disposable provision/mount/write/restart/read/delete storage test before accepting each hop. |
| BH-16 | medium | Story 4.2 permits KubeSphere removal, but its acceptance criterion unconditionally requires console usability. Patch the criterion to accept either proven absence with CLI administration or Administrator-only console access. |
| BH-17 | medium | A Zot authentication change can break publication writers, replication and administrative clients even though only readers are inventoried. Patch least-privilege write/replication inventory and regression checks. |
| BH-18 | high | A retained set sourced only from approved release records can omit live/rollback images and multi-architecture child manifests. Patch reachability from current workloads, rollback targets and manifest lists. |
| BH-19 | low | A real GC run that deletes nothing does not violate the story's safety goal when the disposable-repository rehearsal already proves deletion behavior; requiring sacrificial live-registry content adds operational risk for negligible benefit. Reject. |
| EC-01 | high | A same-name CNPG cluster can be recreated between inventory and backup; without rechecking the UID the proof can bind the wrong incarnation. Patch source-UID validation at backup creation and proof signing. |
| EC-02 | medium | OpenBao verification names source inventory but never requires a snapshot-bound source UID, raft index and canary/count capture. Patch the capture and comparison evidence. |
| EC-03 | high | Restore targets holding production data can remain after proof completion because only a cleanup owner is named. Patch destruction, residual-storage checks and signed cleanup completion. |
| EC-04 | high | Partial quiescence, cordon or drain is not an explicit stop condition before kubeadm runs. Patch terminal-success evidence and abort behavior for every pre-mutation operation. |
| EC-05 | medium | Story 4.2 records ingress UIDs/config digests but never rechecks them before mutation, allowing stale plans to overwrite concurrent changes. Patch optimistic-concurrency validation. |
| EC-06 | medium | A credential proof can be satisfied from a local image cache without Zot authentication. Patch forced uncached pulls correlated with registry audit evidence. |
| EC-07 | medium | A consumer added after inventory can lose pulls at anonymous-read cutover. Patch a change freeze or immediate pre-cutover re-inventory and proof. |
| EC-08 | high | Registry writes between retained-set signing and GC can strand or delete new/in-flight content. Patch a write lock or generation-bound atomic snapshot. |
| EC-09 | high | A recreated/reconfigured runner deployment can be revoked or deleted using stale source evidence. Patch UID/config revalidation before credential revocation and deletion. |
| EC-10 | high | Deleting a runner PVC can leave credential-bearing cache data on a retained PV. Patch reclaim-policy inspection, secure erasure and residual PV/storage evidence. |
| EC-11 | medium | Denying only the named endpoint/CIDRs can leave aliases, proxies or another address family reachable. Patch complete endpoint enumeration and per-family negative probes. |
| EC-12 | medium | The concurrent AR-28 edit promises end-of-job credential cleanup without an abnormal-termination revoker or lease guarantee, which can retain recovery authority after a crashed job. Defer because the edit is unrelated to this story. |
| EC-13 | medium | The concurrent AR-32 tier list leaves RBAC, ServiceAccounts, CRDs, PDBs and admission objects unclassified despite requiring every object to have one tier. Defer because the edit is unrelated to this story. |
| EC-14 | false | AR-35 and FR-8 already define Administrator/deputy-started in-place recovery and deliberately exclude empty/degraded or named-recovery attempts from automatic rollback; absence of an automatic rollback generation is the accepted mode, not an unhandled path. Reject. |
| EC-15 | medium | This independently confirms BH-01: `stepsCompleted: []` can make a completed epic plan appear unvalidated, but commit provenance shows the change came from concurrent `/pushall` work. Defer with BH-01. |
| VG-01 | medium | The verification-gap reviewer demonstrated that Tenants canonical URLs can fall through to `FcModuleLandingPage` while direct-render and alias tests still pass. The change lives inside the concurrently advanced Tenants submodule, not this story. Defer a router-level regression test. |

## Design Notes

Story 4.0 avoids renumbering 25 existing Epic 4 stories and preserves the user’s reference to Stories 4.1–4.3. The split changes ownership, not safety: backup work starts independently, while the disruptive upgrade remains gated.

The live cluster is a single control-plane/worker node at v1.34.9 with external etcd configured at `192.168.1.30:2379`. Current supported Kubernetes branches are 1.35–1.37; kubeadm minor versions must be traversed sequentially. The target patch is therefore resolved and recorded immediately before the maintenance window rather than frozen in this planning change.

## Verification

**Commands:**
- `python3 -c "import yaml; yaml.safe_load(open('_bmad-output/implementation-artifacts/sprint-status.yaml'))"` -- sprint status parses.
- `rg '^### Story 4\.(0|1|2|3):' _bmad-output/planning-artifacts/epics.md` -- exactly the activated stories are present with stable identifiers.
- `rg '^status:|^route:' _bmad-output/implementation-artifacts/{4-0-*,4-1-*,4-2-*,4-3-*}.md` -- story files have valid workflow states.
- `git diff --check` -- changed text has no whitespace errors.
