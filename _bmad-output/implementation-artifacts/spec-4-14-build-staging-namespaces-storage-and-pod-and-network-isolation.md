---
title: 'Story 4.14: Staging namespaces, storage and pod and network isolation'
type: 'feature'
created: '2026-10-29'
status: 'done'
baseline_commit: '251bc9465459b9c28ce1d1738bfbb0525fb07293'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/eng/timesheets-deployment/platform-managed-operations.json'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Timesheets Story 3.6 needs `hexalith-staging` and `hexalith-staging-data`, but no isolation definitions exist (both namespaces NotFound) and the gating dependencies 4.1, 4.2, 4.3, 4.27 (in-progress) and 4.28 (backlog) have no accepted evidence.

**Approach:** Add offline, tested operations-repo definitions under `eng/staging-foundation/` — layered manifests, a Calico enforcement probe, and a blocked apply/verify/recovery package with exact commands. Nothing is applied to the cluster. Story 4.14 stays in-progress; Timesheets 3.6 stays in-progress and launch FAIL.

## Boundaries & Constraints

**Always:** Split objects by identity per AR-32: namespaces, StorageClass and PriorityClass in the shared-infrastructure layer; quotas, LimitRanges, NetworkPolicies, PVC rules and data-service TLS in the environment layer. Pod Security `restricted` (enforce) on both namespaces; default-deny ingress+egress; staging PriorityClass lower than the production one; ResourceQuota denies the production StorageClass. Unknown facts (provisioner, encryption attestation, custody holders, production StorageClass name, deploy-identity names) are required inputs the generator refuses to default. Dry-run/read-only only. Preserve historical evidence and gitlinks.

**Never:** Any live mutation, secret value, invented input, helm rollback, change to existing evidence or submodule pointers, edits to the uncommitted Protection work, or marking 4.14, 4.1–4.3, 4.27, 4.28 or Timesheets 3.6 done/passed.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Complete inputs | Valid `inputs.json` | Deterministic manifests for both layers | N/A |
| Missing/placeholder input | Absent provisioner, encryption attestation, custody or prod StorageClass | Render refused naming each field | Non-zero exit, no output |
| Wrong target | Context/UID differs from `jpiquot@local` / `b5860674-…` | Apply plan refuses | Non-zero exit |
| CNI probe | Probe pod reaches a denied peer | Probe fails the check | Report result, cleanup argv emitted |

</frozen-after-approval>

## Code Map

- `eng/timesheets-deployment/{prepare.py,plan.json,test_prepare.py}` -- style model (stdlib python, unittest); `plan.json`/`prepare.py` list 4-14 as backlog — keep that until acceptance.
- `eng/timesheets-deployment/environment-chart/templates/prerequisites.yaml` -- assumes `hexalith-staging`; its NetworkPolicy must stay compatible with default-deny.
- `eng/timesheets-deployment/platform-managed-operations.json` -- blocked proposal naming both namespaces; do not edit approval flags.
- `eng/cluster-management/{QUALIFICATION.md,STAGING-PREREQUISITES.md}` -- Calico CNI, dependency gates.
- `deploy/erasure-root/network-policy.yaml` -- default-deny pattern to reuse.

## Tasks & Acceptance

**Execution:**
- [x] `eng/staging-foundation/inputs.example.json` -- required-input schema with null placeholders -- no fabricated facts
- [x] `eng/staging-foundation/render.py` -- build shared-infra and environment-layer manifests from inputs -- refuses missing inputs
- [x] `eng/staging-foundation/cni_probe.py` -- Calico enforcement probe argv/manifests (allowed vs denied connection) -- AC "test proves the CNI enforces"
- [x] `eng/staging-foundation/apply-plan.json` + `README.md` -- BLOCKED package: target, exact dry-run/apply argv per identity, effects, read-only verification, qualified forward recovery, per-job identity `auth can-i` denial checks
- [x] `eng/staging-foundation/test_*.py` -- unit tests for I/O matrix and policy invariants
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` and story file -- 4-14 `in-progress` with blocker note only

**Acceptance Criteria:**
- Given complete inputs, when rendered, then both namespaces carry PSA restricted, default-deny policies, quotas/limits, lower PriorityClass and production-StorageClass quota denial.
- Given the apply plan, when inspected, then `approvalGranted=false`, no executable dispatcher, and unmet dependencies listed.
- Given the repo, when tests run, then they pass and no cluster call is made.

## Implementation Notes

- 2026-10-29: offline package landed in `eng/staging-foundation/` (inputs schema, renderer, CNI probe, blocked apply plan, README, 22 unit tests). Nothing applied; no cluster call.
- Blocker: 4.1, 4.2, 4.3, 4.27 in-progress and 4.28 backlog lack accepted evidence; no owner approval; real inputs (provisioner, encryption attestation, custody holders, production StorageClass, identities, quotas) not supplied. 4.14 stays in-progress; Timesheets 3.6 stays in-progress, launch FAIL.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| Missing/invalid inputs JSON gives traceback (edge, gap) | medium | patch | main catches only InputError; matrix promises refusal exit 2 |
| Validation gaps: dict holders, tlsPort range, non-string parameters, duplicates, single-pass reporting | medium | patch | confirmed in validate; invalid manifests would pass offline gate |
| Quota not scoped to staging PriorityClass | medium | patch | staging pod could use production class |
| Denial checks lack update/patch and cross-identity | medium | patch | AR-32 split never verified |
| Target guard compares CLI strings; no live UID read; dry-run ordering | medium | patch | plan carries no re-read argv |
| CNI probe: no readiness, no writable /tmp, loose regex, no control, unguarded cleanup | high | patch | false CNI-enforces pass possible |
| Tests: argv content, quota/LimitRange/PSA/ConfigMap values, validate branches, cni main, behavioural no-cluster-call | medium | patch | gap reviewer demonstrated mutations passing |
| PriorityClass immutability undocumented | low | patch | README direct correction |
| Data-TLS policies span all pods/ports, ignore per-service labels | medium | defer | needs per-service pod labels, a factual input not yet known |
| Data namespace default-deny blocks replication peers | medium | defer | needs data-service topology fact; unverified |
| Quota denies only production class; other unencrypted classes unblocked | medium | defer | needs admission policy (VAP/Kyverno) decision |
| Timesheets boundary compatibility only grepped | low | defer | needs a policy evaluator |
| Timesheets-boundary / default-deny workload egress (Gateway, Dapr, OpenBao) | medium | defer | owned by 4.15/4.16/4.18 |
| Impersonation --as form (serviceaccount) | low | false | identities are caller-provided strings; documented as inputs |
| Atomic writes / stale output dir | low | false | rejected: complexity exceeds everyday risk |
| Spec/sprint inconsistencies, tasks unchecked, 21 vs 22 tests, StorageClass-less static PV | low | false | process-only or workflow-managed; tests counted 22 by unittest |
| Deterministic ordering of jobs | low | false | caller-ordered input is deterministic for equal input |

## Verification

**Commands:**
- `python3 -B -m unittest discover -s eng/staging-foundation -v` -- expected: all pass
- `python3 -B -m unittest discover -s eng/timesheets-deployment -v` -- expected: unchanged pass
