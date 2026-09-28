---
title: 'Start the urgent Kubernetes upgrade track and split its backup gate'
type: 'chore'
created: '2026-09-28'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
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
- [ ] `_bmad-output/planning-artifacts/epics.md` -- split backup proofs into Story 4.0 and make Story 4.1 consume its completion evidence.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` and `epic-4-context.md` -- add and activate the urgent track consistently.
- [ ] `_bmad-output/implementation-artifacts/4-0-*.md` through `4-3-*.md` -- create implementation-ready story artifacts grounded in the live inventory and upstream constraints.
- [ ] `_bmad-output/implementation-artifacts/evidence/epic-4/initial-cluster-inventory.md` -- record the sanitized 2026-09-28 baseline and explicit blockers.

**Acceptance Criteria:**
- Given C-15, when Epic 4 is read, then backup/restore proof is an independent Story 4.0 and Story 4.1 cannot execute the upgrade until 4.0 is done.
- Given the sprint tracker, when parsed, then Epic 4 and Stories 4.0–4.3 are `in-progress`, their keys exactly match their story artifacts, and all unrelated statuses are unchanged.
- Given the story files, when an operator begins work, then each names concrete resources, ordered gates, evidence outputs, stop conditions, and the dependencies that require external infrastructure or an Administrator decision.
- Given the discovery evidence, when reviewed, then it records Kubernetes v1.34.9, zero CloudNativePG backup resources, node-local OpenBao snapshots, absent CSI snapshot APIs, public admin surfaces, anonymous Zot reads, and the privileged in-cluster runner without exposing credentials.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Story 4.0 avoids renumbering 25 existing Epic 4 stories and preserves the user’s reference to Stories 4.1–4.3. The split changes ownership, not safety: backup work starts independently, while the disruptive upgrade remains gated.

The live cluster is a single control-plane/worker node at v1.34.9 with external etcd configured at `192.168.1.30:2379`. Current supported Kubernetes branches are 1.35–1.37; kubeadm minor versions must be traversed sequentially. The target patch is therefore resolved and recorded immediately before the maintenance window rather than frozen in this planning change.

## Verification

**Commands:**
- `python3 -c "import yaml; yaml.safe_load(open('_bmad-output/implementation-artifacts/sprint-status.yaml'))"` -- sprint status parses.
- `rg '^### Story 4\.(0|1|2|3):' _bmad-output/planning-artifacts/epics.md` -- exactly the activated stories are present with stable identifiers.
- `rg '^status:|^route:' _bmad-output/implementation-artifacts/{4-0-*,4-1-*,4-2-*,4-3-*}.md` -- story files have valid workflow states.
- `git diff --check` -- changed text has no whitespace errors.
