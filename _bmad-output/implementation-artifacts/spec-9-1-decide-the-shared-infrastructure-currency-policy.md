---
title: '9.1 Decide the shared-infrastructure currency policy'
type: 'feature'
created: '2026-11-15'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
story_key: '9-1-decide-the-shared-infrastructure-currency-policy'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-9-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** FR-10/AR-61 and G3 require a currency check before automatic promotion. No record yet says which components it checks, how often, how far a component may lag behind supported, security-current releases, or what a failed check does to Administrator-approved attempts, so "security-current" cannot be enforced.

**Approach:** Publish one versioned Platform decision record, `hexalith.platform.shared-infrastructure-currency-policy.v1`, that fixes those four points, with fail-closed evaluation rules, result-record fields, ownership, revisit conditions and the downstream obligations of Stories 4.9, 9.2, 9.6 and 9.8.

## Boundaries & Constraints

**Always:** Run the check at every production attempt in both modes and at every monthly drill, always recording the result. Compare exact deployed identities with inventory pins, pins with upstream support and security data resolved at check time, and the management/workload pairing with the qualified pairing. A failed check blocks automatic promotion with no waiver, names each failing component and reason, and sets no promotion stop. Fail closed, including for digest-only components without a resolvable version. Platform owns the policy with Administrator; Platform Maintainer `@jpiquot` is accountable. One direct recorded approval suffices.

**Never:** Create the profile inventory (Story 4.9), implement the check (9.2), rehearse it (9.6) or record G3 (9.8). Change infrastructure, pins, workflows, submodules, the staging evidence policy, the vulnerability policy or planning artifacts. Do not claim enforcement or current compliance.

## Decisions

Confirmed by the user on 2026-11-15:

1. **Inventory:** every versioned shared-facet component (Kubernetes with node OS, containerd and etcd; Rancher server, required agents and CRDs; management cluster; backup operator; CNI; Traefik and Gateway API CRDs; cert-manager; storage provisioner; operators; Dapr control plane; Keycloak and its database) plus production's environment-layer providers (OpenBao, PostgreSQL, FalkorDB, Redis). Configuration-only objects (covered by the profile digest) and the deploy toolchain are excluded.
2. **Cadence:** beyond every production attempt and monthly drill, a weekly scheduled check records its result and notifies Administrator on failure or when an upstream end of life falls within 30 days. It sets no stop and adds no block.
3. **Allowed lag:** zero for support, so a component fails from its upstream end-of-life date or when it falls outside the supported matrix or the qualified pairing. A release fixing a published advisory for the deployed line, or the earliest supported line carrying the fix, must be deployed within 30 days of its upstream release. Non-security updates have no limit while supported.
4. **Approved attempts:** the check runs and is recorded. A failed result lets the attempt start only when the Administrator record names every failing component. An unnamed failing component refuses the attempt until a new approval.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Current | All components at pin, supported, within lag, pairing qualified | Pass, recorded | N/A |
| Drift / unknown | Deployed differs from pin, version unresolvable or upstream data unavailable | Component fails; automatic blocked | Fail closed; names component; no cached fallback |
| Lag | Security fix released 30+ days ago, not deployed | Component fails | Names advisory and fixed release |
| Approved, named | Failed check; record names every failing component | Attempt may start | Result recorded |
| Approved, unnamed | Failed check includes an unnamed component | Attempt refused | New approval required |

</frozen-after-approval>

## Code Map

- `docs/contracts/staging-evidence-policy-v1.md` -- shape precedent (identifier, owner, numbered decisions, accountability/revisit, downstream adoption, examples); do not edit. `image-and-dependency-vulnerability-policy-v1.md` is the 30-day precedent.
- `README.md:302-318` -- "Staging policies" section; add a sibling section linking the new policy.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:240,272,309,523,527` -- inventory currency rule, shared facet list and automatic-only block, release modes, G1 currency, G3 gate.
- `_bmad-output/specs/spec-platform/sequencing.md:9,210` -- approved mode replaces the currency trigger; bounds owed before G3.
- `_bmad-output/planning-artifacts/epics.md:2237-2262,3670-3696,4410-4446,4508-4540` -- Stories 4.9 (inventory, currency owners; backlog), 6.22 (urgent in-place patch), 8.17 (monthly drill), 9.1/9.2.
- `eng/cluster-management/RANCHER.md:15-35`, `docs/implementation/story-5-4-coupled-delivery-2026-10-25/installation-evidence/infrastructure-observation.json` -- dated Rancher/K3s pins, support matrix, observed versions (Velero, Zot, Keycloak digest-only); informative only, cite no compliance.

## Tasks & Acceptance

**Execution:**
- [ ] `docs/contracts/shared-infrastructure-currency-policy-v1.md` -- publish the decisions, triggers, comparison and fail-closed rules, result-record fields (check ID, trigger, time, inventory digest, per-component deployed identity, pin, upstream sources and observation time, reason, pairing and overall result), approval-record naming rule, accountability, revisit conditions, downstream obligations (4.9 records per-component support/advisory sources and currency owners; 9.2, 9.6, 9.8) and an acceptance/refusal table -- gives 9.2 an enforceable contract.
- [ ] `README.md` -- add a short section linking the policy beside "Staging policies" -- discoverability.
- [ ] This spec and `sprint-status.yaml` -- record verification and status per the build workflow.

**Acceptance Criteria:**
- Given the profile inventory definition, when maintainers read the record, then it fixes checked components, cadence, allowed lag and the approved-attempt effect, and keeps the check at every production attempt and monthly drill.
- Given Story 9.2, when it implements the check, then each table row determines pass or fail and the automatic/approved outcome without further policy choices.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `git diff --check` -- expected: no whitespace errors.

**Manual checks (if no CLI):**
- Every local link and anchor in the new document and README resolves; every table row matches the frozen decisions; no enforcement claim.
