---
title: '5.1 Decide the staging evidence policies'
type: 'feature'
created: '2026-10-30'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '2cbb8a68d277bdd55e321972b8aa11ef015fc54e'
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

## Decisions

Confirmed by the user on 2026-10-30:

- Evidence expires 24 hours (86,400 seconds) after attempt start, checked at promotion admission with an exclusive deadline; refuse missing/future timestamps or mismatches.
- Queue staging automatically for a retained candidate passing intake or a checked-input/baseline change. Platform Maintainer may request fresh attempts after expiry/failure; no periodic retry loop.
- Only the latest issued serialized attempt for a pending decision qualifies after a full passing rerun. Preserve earlier outcomes and rerun reason; no partial-pass aggregation or cross-decision reuse.
- Removal/remap requires owning-module and Platform approval of before/after declaration/mapping digests, affected flows/checks and coverage rationale. Tenants also reviews lifecycle changes.
- Reset lifetime is 60 minutes (3,600 seconds) from recovery entry; notify Administrator and Platform Maintainer on expiry without aborting. Resume/re-entry follow architectural timers.
- Platform Maintainer `@jpiquot` is accountable; Builds implements, modules review flows, Administrator/EventStore qualify reset. Revisit respectively after freshness/cost issues, intake changes, rerun ambiguity, review gaps or restores exceeding the bound.

</frozen-after-approval>

## Code Map

- `docs/contracts/candidate-and-attachment-policy-v1.md`: reuse policy structure; FR-4 does not decide staging semantics.
- `docs/contracts/staging-evidence-policy-v1.md`: new canonical record, policy ID `hexalith.platform.staging-evidence-policy.v1`.
- `README.md`: link beside existing policy guidance; distinguish publication from enforcement.
- `.github/CODEOWNERS`: existing accountable owner `@jpiquot`; reference only.
- `_bmad-output/planning-artifacts/epics.md`: Story 5.1 and consumers 5.3, 5.6–5.11. The architecture spine's staging gate/reset/lifetime rules are distilled above; reference only.

## Tasks & Acceptance

**Execution:**

- [x] `docs/contracts/staging-evidence-policy-v1.md` — record approved rules, timing/identity boundaries, owners/revisit triggers, immutable version adoption, acceptance/refusal examples and downstream handoff.
- [x] `README.md` — link policy and state downstream implementation/qualification obligations.
- [x] `_bmad-output/implementation-artifacts/epic-5-context.md` — point downstream planning to the approved policy without claiming enforcement.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — synchronize Story 5.1 and Epic 5 only under workflow rules.

**Acceptance Criteria:**

- Given FR-6, when published, then all five rules name concrete decisions, owners and revisit conditions.
- Given boundary-age, changed-input, rerun or reduced-coverage cases, when read, then acceptance/refusal and required reviews are unambiguous.
- Given reset expiry, when applied, then notify without aborting, retain lineage and prevent premature subsequent attempts.
- Given publication, when inspected, then policy identity/adoption/qualification are explicit without runtime or release-authority claims.

## Implementation Notes

- 2026-10-30: Added canonical policy `hexalith.platform.staging-evidence-policy.v1`, README discovery and Epic 5 adoption handoff. Records the approved five rules, exact input/decision bindings, boundary examples, ownership/revisit conditions and downstream qualification obligations.
- Parent inspected the full unified diff from baseline, including untracked files. Concurrent changes in other specs, the vulnerability policy, README and submodules are independent and preserved. Story 5.1 tracking is in progress; Epic 5 was already in progress.

## Spec Change Log

## Review Triage Log

2026-10-30: All three independent layers completed before triage. Edge-case review returned no findings; verification-gap review found no gaps. Blind review returned the ten findings below. Each claim was checked against the policy, architecture and cited artifact before grouping or routing.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B1: Degraded/empty preparation omitted | medium | The policy restricts fresh-install proof to no-baseline/no-data; the architecture's Empty or degraded production rule also requires fresh-install proof for that approved path, alongside retained-data qualification. Patch the table and restrictive sentence to preserve the existing path. |
| B2: Authoritative before-review inputs | medium | The review section binds submitted before/after digests without explicitly tying the before inputs to accepted declarations. A validator needs the actual accepted transition to detect omitted removals. Patch this evidence clarification without adding an approval authority. |
| B3: Pending decision lifecycle | medium | The policy requires a pending decision but does not explicitly prevent reusing its identity after closure. Patch current authoritative pending-state verification and fresh identities for reopened/new requests, preserving the approved no-reuse rule. |
| B4: Admission race | false | The evidence boundary already requires the evaluator to compare current checked inputs/current production baseline at promotion admission, and the latest issued attempt alone may qualify. No implementation or mandated sequence of separate reads exists here; the proposed atomic store mechanism belongs to later implementation, not a demonstrated defect in this rule. |
| B5: Reset-expiry notification execution | medium | The policy names recipients but leaves notification execution implicit. The architecture assigns non-terminal maximum-lifetime notification to the independent off-site monitor. Patch that existing ownership so a dead executor does not become the notification source. |
| B6: Replacement-job resumption limits | medium | The reset section preserves timers and distinguishes interrupted recovery but omits explicit grace/takeover conditions on replacement-job resumption. Patch a direct architectural qualification; do not create a new recovery allowance. |
| B7: Story 3.3 service eligibility | medium | The independent, unfinished routing proposal names a service surface but specifies only UI/agent behavior. A future service gate could diverge. Defer to Story 3.3; routing eligibility is unrelated to the staging-policy goal and was not introduced by this story. |
| B8: Story 5.2 owner-policy delivery | medium | The independent vulnerability adoption document links an on-disk Builds policy absent from the recorded submodule commit, confirmed with git cat-file. Parent-only delivery cannot reproduce that owner document. Defer owner-record/revision delivery to Story 5.2; no vulnerability or submodule changes belong to Story 5.1. |
| B9: Spec/sprint state mismatch | false | Build step 4 explicitly sets spec in-review; sprint review synchronization occurs in presentation after review. The current in-progress sprint value is a workflow intermediate state, not a missed final transition; no spec correction is warranted. |
| B10: Unrelated loop-6 handoff reference | low | The independent protection delivery note points to loop-6 worktrees below without coordinates and retains older Current status labels. Defer that direct documentation correction to its owner; this story did not change the handoff. |

The original implementation agent applied B1, B2, B3, B5 and B6 as direct policy clarifications. Parent inspected each correction and repeated scoped verification successfully. B7, B8 and B10 were appended to the deferred ledger without modifying their independently owned artifacts; B4 and B9 were rejected with the evidence above. No unresolved finding remains for Story 5.1.

## Verification

Run scoped `git diff --check`; inspect new Markdown whitespace and links. Cross-check five rules, timing/identity boundaries, history, reviews and reset lifetime against Story 5.1/FR-6/architecture. No new tests or workloads. Verify preservation of unrelated changes.

- 2026-10-30: Parent independently confirmed 21 local links/fragments, final newlines and whitespace across policy/README/context, and the approved frozen block's unchanged SHA-256. Scoped `git diff --check` passed. All four tasks and acceptance criteria were checked against the diff and governing requirements; no matrix applies and no tests or workloads were run.
- 2026-10-30: After review corrections, parent repeated all 21 link/fragment, whitespace/newline and frozen-intent checks, inspected each of the five corrected passages, and reconfirmed the five concrete policy rules and all completed tasks. Scoped diff checks pass. Spec is done and Story 5.1 sprint tracking moves to review; Epic 5 remains in progress. Implementation/qualification of policy consumers remains downstream work.
