---
title: '5.1 Decide the staging evidence policies'
type: 'feature'
created: '2026-10-30'
status: 'in-progress'
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

### Review Findings

Code review 2026-10-30 of commit `f8e4acd4` (policy, README, epic context, deferred ledger, sprint status). Layers: Blind Hunter, Edge Case Hunter, Verification Gap (no gaps), Acceptance Auditor (no hard AC violation). The user adopted all six recommended decisions on 2026-10-30, now recorded as patches, and left every patch as an action item. Line references are to `docs/contracts/staging-evidence-policy-v1.md` unless stated.

- [x] [Review][Patch] Reset replacement-job resumption contradicts the architecture (decision 2026-10-30: adopted (a)) [docs/contracts/staging-evidence-policy-v1.md:172] — drop replacement-job resumption for staging resets: executor loss interrupts and fails the reset, and Administrator or the deputy re-enters from the same point (architecture In-place recovery and Attempt ownership rows); rewrite the L278 example accordingly.
- [x] [Review][Patch] Re-entry before the prior lifetime passes is undefined (decision 2026-10-30: adopted (a)) [docs/contracts/staging-evidence-policy-v1.md:179] — a re-entry before the prior deadline inherits that deadline; a re-entry after it records entry plus 3,600 seconds (architecture Recovery-kind lifetime row); add an early re-entry example.
- [x] [Review][Patch] No rerun path after a cancelled, incomplete or interrupted latest attempt (decision 2026-10-30: adopted (a)) [docs/contracts/staging-evidence-policy-v1.md:89] — the Platform Maintainer may request a fresh attempt after expiry or any non-passing terminal outcome (failed, cancelled, incomplete or interrupted); still no periodic retry loop; update the L272 example.
- [x] [Review][Patch] Pending-decision creation and "changed targets" are undefined (decision 2026-10-30: adopted (a)) [docs/contracts/staging-evidence-policy-v1.md:97] — a retained candidate passing intake opens one trusted-gate-issued pending promotion decision bound to its release and policy; checked-input and baseline changes queue new attempts under it; only a different retained release is a changed target; switching to the approved retained-data path keeps the decision.
- [x] [Review][Patch] Reset trigger undefined with no baseline or unknown compatibility (decision 2026-10-30: adopted (a)) [docs/contracts/staging-evidence-policy-v1.md:150] — fail safe: without a production working baseline, or when baseline readability of the unadopted candidate's writes is not proven, treat them as unreadable and reset to the pre-attempt point before a later candidate starts.
- [x] [Review][Patch] Recovery point for a candidate with several attempts (decision 2026-10-30: adopted (a)) [docs/contracts/staging-evidence-policy-v1.md:152] — restore the earliest recovery point cut for that candidate since the last verified reset (before any of its writes); a same-candidate rerun after breaking or unproven-readability writes first runs a reset.
- [x] [Review][Patch] A superseded (non-adoptable) candidate stays pending and admissible [docs/contracts/staging-evidence-policy-v1.md:156] — non-adoptability appears only in the reset section, never in admission (L48-54), examples or the Story 5.11 row, and the earlier candidate keeps receiving automatic baseline-change triggers (ping-pong). Close its decision as superseded: refused at admission, no automatic triggers.
- [x] [Review][Patch] Timing example narrows the expiry-consistency rule [docs/contracts/staging-evidence-policy-v1.md:270] — "exceeds start plus 86,400 seconds" implies a shorter recorded expiry is accepted; L66/L74 refuse any inconsistent expiry. Say "differs from".
- [x] [Review][Patch] No default for a state matching no preparation row [docs/contracts/staging-evidence-policy-v1.md:202] — e.g. a baseline without compatibility and no Administrator approval. State that an unmatched state or missing applicable-row proof refuses admission.
- [x] [Review][Patch] Reset-expiry notification omits channel and deputy [docs/contracts/staging-evidence-policy-v1.md:164] — bind it to the architecture's single accepted notification path, which also mentions the recovery deputy who performs re-entry (Roles row).
- [x] [Review][Patch] Observation-gap outcome unstated [docs/contracts/staging-evidence-policy-v1.md:176] — say the gap leaves the reset unverified and Administrator or the deputy re-enters from the same point (In-place recovery row).
- [x] [Review][Patch] Removal/remap transition imprecise [docs/contracts/staging-evidence-policy-v1.md:127] — "a removed … flow can join the gate" is incoherent, "remap" is undefined (rename, split, check replacement, required to optional), and it is unclear whether both the latest accepted and production-baseline predecessors are compared. Define removal and remap fail-safe and require each predecessor transition.
- [x] [Review][Patch] Tenants review limited to "changes" [docs/contracts/staging-evidence-policy-v1.md:140] — Story 5.4 and the epic context require review of every tenant-lifecycle flow before it joins the gate.
- [x] [Review][Patch] Confirmation attributed to "the user" [docs/contracts/staging-evidence-policy-v1.md:5] — name the Platform Maintainer, `@jpiquot`, as the sibling candidate policy does.
- [x] [Review][Patch] README paragraph outside the Staging policies section [README.md:82] — move it under `## Staging policies` (added later by Story 5.2), beside the vulnerability policy.
- [x] [Review][Patch] Story 5.1 deferred entries filed under the spec-2-7 heading without target_story [_bmad-output/implementation-artifacts/deferred-work.md:1491] — add their own heading, plus `target_story` 3.3 and 5.2 for B7 and B8.
- [x] [Review][Patch] Latest-attempt rule doesn't say which attempt kinds count [docs/contracts/staging-evidence-policy-v1.md:122] — state that resets and named-recovery qualifications are recovery evidence that neither count as nor supersede the latest E2E evidence attempt.
- [x] [Review][Defer] Staging's own promotion-stop semantics conflict [docs/contracts/staging-evidence-policy-v1.md:189] — deferred, pre-existing: the architecture's Promotion stop row sets staging's stop on every non-working outcome and recovery entry, while the Staging gate row says a staging failure blocks only that candidate. The policy restates the frozen wording; resolving it belongs to the architecture or Stories 5.6/5.10.
- [x] [Review][Defer] Administrator reservation semantics undefined [docs/contracts/staging-evidence-policy-v1.md:156] — deferred, pre-existing architecture gap: open questions are whether a reserved candidate is still reset before the later attempt, how long a reservation lasts, and whether its evidence stays admissible. Story 5.10.
- [x] [Review][Defer] Empty/degraded fresh-install proof vs Story 5.7 [docs/contracts/staging-evidence-policy-v1.md:212] — deferred, pre-existing planning inconsistency: epics Story 5.7 says fresh-install proof "serves only a first installation", the architecture's Empty or degraded row also requires it for degraded production, and no consumer story qualifies that branch. The fix edits `epics.md`.
- [x] [Review][Defer] Epic-context pointer lost on regeneration [_bmad-output/implementation-artifacts/epic-5-context.md:45] — deferred: the file is compiled from planning artifacts, and `epics.md` Stories 5.3 and 5.6 cite only "the policy from Story 5.1". Add the policy identifier and link to `epics.md`.

Rejected:

- E1/B10 (clock source and skew), low: attempts last far longer than plausible skew, so a "future" start never arises in practice and the 24-hour bound moves by seconds; a trusted-clock rule adds mechanism.
- E3/A7 (checked-input list), false: L81-83's general clause covers every input the gate checks, and the list says "include".
- E4/B3 (queue coalescing and ordering), low: efficiency only; old-input evidence is refused, and a newer issued attempt already blocks fallback (L109-111). Coalescing is a Story 5.6 implementation choice.
- B3 (intake trigger displaces a passed candidate), false: the architecture's Staging reset row is designed this way, and an Administrator reservation is the escape.
- E6 (admission check-to-commit race), low: a narrow window whose fix (compare-and-set admission) is Story 5.11 mechanism; the earlier triage (B4) rejected the same claim.
- E7/A11 (which lock epoch is bound), false: an interrupted release-kind attempt fails, and a replacement job only performs the remaining recovery, so no qualifying pass spans two epochs.
- E13 (executor dies before recording the deadline), false: the Timing row records kind and a pre-mutation lifetime at lock acquisition, and the monitor checks for stale attempts.
- E14/B9 (notification retry, escalation and repeats), low: the architecture's single notification path and dead-man workflow govern delivery; these rules would add mechanism.
- B9 (unreadable reset record), false: the architecture already stops for intervention on an unreadable record.
- E15 (reset ending early in failure, unnotified), false: the Roles row routes every failure and recovery notification to Administrator and the deputy, and non-working outcomes set staging's stop.
- E19/B7 (preparation rows overlap), false: row 2 requires compatibility evidence or the named recovery whenever data remain, matching the architecture; rows 1 and 2 never conflict.
- B11 (production synthetic-tenant provisioning), false: the epics traceability table assigns it to Epic 6.
- A12 (ownership wording differs from the spec), false: the executor/evaluator split matches the architecture and epic context.
- A13/B13 (qualitative revisit triggers; no recovery-point row), low, process-only: Story 5.1 fixes five rules, and the frozen decisions set those revisit conditions. Measurable thresholds or a sixth row would need new decisions.
- B14 (no binding to the policy text), low: git fixes v1's text, the review corrections landed before the first commit, and the sibling policy binds the identifier the same way.
- B15 (epic-context restatement may drift), low: the summary is accurate today, and the file says "Edit freely".

## Implementation Notes

- 2026-10-30: Added canonical policy `hexalith.platform.staging-evidence-policy.v1`, README discovery and Epic 5 adoption handoff. Records the approved five rules, exact input/decision bindings, boundary examples, ownership/revisit conditions and downstream qualification obligations.
- Parent inspected the full unified diff from baseline, including untracked files. Concurrent changes in other specs, the vulnerability policy, README and submodules are independent and preserved. Story 5.1 tracking is in progress; Epic 5 was already in progress.

- 2026-10-30 review follow-up: Applied all 17 adopted/open review patches. Clarified pending-decision creation, changed targets, supersession and terminal-outcome reruns; separated E2E attempts from recovery evidence; defined removal/remap and reviews against both applicable predecessors, including every tenant-lifecycle enrollment; made reset triggers and the earliest retained pre-write point fail safe; replaced reset-job resumption with authorized re-entry and inherited deadlines; bound notification to the accepted private-issue path and deputy; added default preparation refusal and qualification examples. Moved README discovery into Staging policies, synchronized the epic handoff, and routed the Story 5.1 implementation deferrals under their own heading with B7/B8 target stories. Frozen intent, planning artifacts, runtime code and independent submodule work remain unchanged.

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
