---
title: '5.1 Decide the staging evidence policies'
type: 'feature'
created: '2026-10-30'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '5-1-decide-the-staging-evidence-policies'
context:
  - '_bmad-output/implementation-artifacts/epic-5-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** FR-6/AR-57 require five agreed staging rules before accepting evidence; no staging policy or numeric age/reset limits exist.

**Approach:** Publish one versioned decision record fixing evidence age, attempt triggers, reruns, critical-flow removal/remap review and reset duration. Record each rule's rationale, owner and revisit condition, and downstream adoption obligations.

## Boundaries & Constraints

**Always:** Require complete passing real-service E2E for every included module's non-empty flows in both release modes. Bind evidence to exact retained release, suite, profile, configuration and applicable production baseline; preserve failed/incomplete attempts. Flow reductions need module-owner review beyond matching digests. Preserve serialization, per-attempt recovery points, separate resets and no-baseline/compatibility/named-recovery branches.

Reset lifetime expiry notifies without aborting restoration. Reset blocks later candidates until verified; staging failures/resets affect staging's stop only. Platform owns policy; Builds owns implementation, modules own flows. Claim no unrecorded owner approval.

**Never:** Change runners, schemas, workflows, pins, submodules, vulnerability policy or tenant implementation; start workloads, deploy, publish remotely or claim enforcement. Preserve independent work, including the authorized Builds checkout.

</frozen-after-approval>

## Open Questions

1. Initial policy choices: approve the following proposed set or specify replacements. These are proposals, not ratified decisions:
   - Evidence expires 24 hours (86,400 seconds) after attempt start, checked at promotion admission with an exclusive deadline; refuse missing/future timestamps or mismatches. Shorter age reduces drift but increases reruns; longer age allows delayed promotion.
   - Queue staging automatically for a retained candidate passing intake or a checked-input/baseline change. Platform Maintainer may request fresh attempts after expiry/failure; no periodic retry loop. Alternative: explicit requests only, giving more scheduling control.
   - Only the latest issued serialized attempt for a pending decision qualifies after a full passing rerun. Preserve earlier outcomes and rerun reason; no partial-pass aggregation or cross-decision reuse. Alternative: also require maintainer approval for every failed-attempt retry.
   - Removal/remap requires owning-module and Platform approval of before/after declaration/mapping digests, affected flows/checks and coverage rationale. Tenants also reviews lifecycle changes. Alternative: forbid removal/remap in v1, delaying suite evolution.
   - Reset lifetime is 60 minutes (3,600 seconds) from recovery entry; notify Administrator and Platform Maintainer on expiry without aborting. Resume/re-entry follow architectural timers. Shorter alerts sooner; longer tolerates slower restores.
   - Platform Maintainer `@jpiquot` is accountable; Builds implements, modules review flows, Administrator/EventStore qualify reset. Revisit respectively after freshness/cost issues, intake changes, rerun ambiguity, review gaps or restores exceeding the bound.

## Code Map

- `docs/contracts/candidate-and-attachment-policy-v1.md`: reuse policy structure; FR-4 does not decide staging semantics.
- `docs/contracts/staging-evidence-policy-v1.md`: new canonical record, policy ID `hexalith.platform.staging-evidence-policy.v1`.
- `README.md`: link beside existing policy guidance; distinguish publication from enforcement.
- `.github/CODEOWNERS`: existing accountable owner `@jpiquot`; reference only.
- `_bmad-output/planning-artifacts/epics.md`: Story 5.1 and consumers 5.3, 5.6–5.11. The architecture spine's staging gate/reset/lifetime rules are distilled above; reference only.

## Tasks & Acceptance

**Execution:**

- [ ] `docs/contracts/staging-evidence-policy-v1.md` — record approved rules, timing/identity boundaries, owners/revisit triggers, immutable version adoption, acceptance/refusal examples and downstream handoff.
- [ ] `README.md` — link policy and state downstream implementation/qualification obligations.
- [ ] `_bmad-output/implementation-artifacts/epic-5-context.md` — point downstream planning to the approved policy without claiming enforcement.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` — synchronize Story 5.1 and Epic 5 only under workflow rules.

**Acceptance Criteria:**

- Given FR-6, when published, then all five rules name concrete decisions, owners and revisit conditions.
- Given boundary-age, changed-input, rerun or reduced-coverage cases, when read, then acceptance/refusal and required reviews are unambiguous.
- Given reset expiry, when applied, then notify without aborting, retain lineage and prevent premature subsequent attempts.
- Given publication, when inspected, then policy identity/adoption/qualification are explicit without runtime or release-authority claims.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

Run scoped `git diff --check`; inspect new Markdown whitespace and links. Cross-check five rules, timing/identity boundaries, history, reviews and reset lifetime against Story 5.1/FR-6/architecture. No new tests or workloads. Verify preservation of unrelated changes.
