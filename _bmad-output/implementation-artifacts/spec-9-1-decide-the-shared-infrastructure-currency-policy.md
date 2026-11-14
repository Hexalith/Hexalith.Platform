---
title: '9.1 Decide the shared-infrastructure currency policy'
type: 'feature'
created: '2026-11-15'
status: 'draft'
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

**Always:** Run the check at every production attempt in both modes and at every monthly drill, always recording the result. Compare exact deployed identities with the profile inventory's pins, the pins with upstream support and security data resolved at check time, and the management/workload version pairing with the qualified pairing. A failed check blocks automatic promotion, names each out-of-date component and its reason, and sets no promotion stop. Fail closed: a missing or unresolvable deployed identity (for example a digest with no resolvable version), unavailable upstream data, deployed-versus-pin drift or an unqualified pairing each fail that component. Grant no waivers on the automatic path. Platform owns the policy with Administrator; Platform Maintainer `@jpiquot` is accountable. One direct recorded approval suffices.

**Never:** Create the profile inventory (Story 4.9), implement the check (9.2), rehearse it (9.6) or record G3 (9.8). Change infrastructure, pins, workflows, submodules, the staging evidence policy, the vulnerability policy or planning artifacts. Do not claim enforcement or current compliance.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Current | Every checked component deployed at its pin, pin supported and within allowed lag, pairing qualified | Pass, recorded with exact identities | N/A |
| Drift | Deployed identity differs from inventory pin | Component fails; automatic path blocked | Names component, deployed and pinned identities |
| End of life | Pin's release line past upstream end of life | Component fails | Names component and EOL date |
| Unknown | Version not resolvable, or upstream data unavailable | Component fails | Fail closed; no cached fallback |

</frozen-after-approval>

## Open Questions

1. **Checked inventory** — options: whole versioned shared facet plus environment-layer providers (OpenBao, PostgreSQL, FalkorDB, Redis) / shared facet only / Story 9.2's minimum (Kubernetes, Rancher, agents, management cluster, backup operator).
2. **Cadence beyond attempts and drills** — options: weekly scheduled check that notifies / daily scheduled check / none.
3. **Allowed lag** — options: support zero, security patch 30 days / security patch 14 days / tiered 7 days Critical or known-exploited, 30 days others.
4. **Effect on approved attempts** — options: proceed only if the approval names the failed components / blocked unless production is empty or degraded / no effect.

## Code Map

- `docs/contracts/staging-evidence-policy-v1.md` -- Platform policy precedent: identifier, published line, owner, numbered decisions, accountability/revisit, downstream adoption, acceptance/refusal examples. Reuse the shape; do not edit.
- `docs/contracts/image-and-dependency-vulnerability-policy-v1.md` -- 30-day Administrator exception and both-mode precedent; do not edit.
- `README.md:302-318` -- "Staging policies" section; add a sibling section linking the new policy.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:240,272,309,523,527` -- inventory currency rule, shared facet list and automatic-only block, release modes, G1 currency, G3 gate.
- `_bmad-output/specs/spec-platform/sequencing.md:9,210` -- approved mode replaces the currency trigger; bounds owed before G3.
- `_bmad-output/planning-artifacts/epics.md:2237-2262,3670-3696,4410-4446,4508-4540` -- Stories 4.9 (inventory, currency owners; backlog), 6.22 (urgent in-place patch), 8.17 (monthly drill), 9.1/9.2.
- `eng/cluster-management/RANCHER.md:15-35` -- dated Rancher 2.15.2/K3s 1.35.9 pins, SUSE support matrix, 24-hour recheck rule; informative only.
- `docs/implementation/story-5-4-coupled-delivery-2026-10-25/installation-evidence/infrastructure-observation.json` -- observed versions; Velero, Zot and Keycloak digest-only. Informative only.

## Tasks & Acceptance

**Execution:**
- [ ] `docs/contracts/shared-infrastructure-currency-policy-v1.md` -- publish the four decisions, check triggers, comparison and fail-closed rules, result-record fields (check ID, trigger, time, inventory digest, per-component deployed identity, pin, upstream sources and observation time, reason, pairing result, overall result), accountability, revisit conditions, downstream obligations and an acceptance/refusal table -- gives 9.2 an enforceable contract.
- [ ] `README.md` -- add a short section linking the policy beside "Staging policies" -- discoverability.
- [ ] This spec and `sprint-status.yaml` -- record verification and status per the build workflow.

**Acceptance Criteria:**
- Given the profile inventory definition, when maintainers read the record, then it fixes checked components, cadence, allowed lag and the approved-attempt effect, and keeps the check at every production attempt and monthly drill.
- Given Story 9.2, when it implements the check, then each table row determines pass or fail and the automatic/approved outcome without further policy choices.

## Implementation Notes

## Verification

**Commands:**
- `git diff --check` -- expected: no whitespace errors.

**Manual checks (if no CLI):**
- Every local link and anchor in the new document and README resolves; every table row matches the frozen decisions; no enforcement claim.
