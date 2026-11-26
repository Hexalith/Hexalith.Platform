---
title: '9.1 Decide the shared-infrastructure currency policy'
type: 'feature'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'a69e4b4b9ad374807829338856bb149fd55a198e'
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
- [x] `docs/contracts/shared-infrastructure-currency-policy-v1.md` -- publish the decisions, triggers, comparison and fail-closed rules, result-record fields (check ID, trigger, time, inventory digest, per-component deployed identity, pin, upstream sources and observation time, reason, pairing and overall result), approval-record naming rule, accountability, revisit conditions, downstream obligations (4.9 records per-component support/advisory sources and currency owners; 9.2, 9.6, 9.8) and an acceptance/refusal table -- gives 9.2 an enforceable contract.
- [x] `README.md` -- add a short section linking the policy beside "Staging policies" -- discoverability.
- [x] This spec and `sprint-status.yaml` -- record verification and status per the build workflow.

**Acceptance Criteria:**
- Given the profile inventory definition, when maintainers read the record, then it fixes checked components, cadence, allowed lag and the approved-attempt effect, and keeps the check at every production attempt and monthly drill.
- Given Story 9.2, when it implements the check, then each table row determines pass or fail and the automatic/approved outcome without further policy choices.

### Review Findings

Code review 2026-11-25 (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) over `a69e4b4b..2752ef60`, scoped to the policy, README, this spec, sprint-status and deferred-work. Verification Gap found no gaps.

- [x] [Review][Patch] Fix-deadline start when the deployed line's fix ships after another line's [docs/contracts/shared-infrastructure-currency-policy-v1.md:88-99] — resolved 2026-11-25, Administrator chose "deployed line governs": `fixReleasedAt` is the first upstream release on the deployed line carrying the fix. While that line has none, it is the first release on the earliest supported line carrying it. A later release on the same line, a repackaging or an inventory edit does not restart the clock. Example: line B fixes on day 0 and deployed line A backports on day 20, so the deadline is day 30 until the backport ships and day 50 after it. Reword lines 88-99 and add an acceptance row (blind-hunter+acceptance-auditor).
- [x] [Review][Patch] Story 9.8 obligation requires a passing automatic-attempt result before G3, which cannot exist [docs/contracts/shared-infrastructure-currency-policy-v1.md:216] — sequencing.md:9 says every pre-G3 release uses approved mode; epics Story 9.8 AC1 records G3 on the policy, rehearsals and 9.3 confirmations; spine G3 row 564 requires qualified checks. Reword: qualified enforcement plus 9.6 evidence before G3; each automatic attempt after G3 needs its own passing check.
- [x] [Review][Patch] Time-normalization rules are not uniform [docs/contracts/shared-infrastructure-currency-policy-v1.md:73,85,97] — the date-only "start of that date in UTC" rule covers only end of life, so a date-only fix release time leaves the one-second deadline row undecidable. The weekly "within 30 days" notice has no unit, unlike the 2,592,000-second security window.
- [x] [Review][Patch] The Story 9.7 suspension sentence binds a story whose triggers exclude currency-policy changes [docs/contracts/shared-infrastructure-currency-policy-v1.md:206-207] — epics 9.7 and spine G3 suspend only on verification, recovery or rollback-set policy changes. Limit the sentence to what 9.7 covers; v2 adoption already governs currency changes.
- [x] [Review][Patch] The "no published fix" row implies a vulnerability backstop that shared components lack [docs/contracts/shared-infrastructure-currency-policy-v1.md:239] — vulnerability policy v1 scans release-included images, not the node OS, etcd, Kubernetes binaries or the Rancher server. The approved column leaves the currency outcome unstated.
- [x] [Review][Patch] The effect of a monthly-drill result is unstated [docs/contracts/shared-infrastructure-currency-policy-v1.md:57] — weekly results explicitly add no block and set no stop; drill results say neither, and the acceptance table has no drill row.
- [x] [Review][Patch] A component instance with several observed identities (nodes, replicas) has no stated rule [docs/contracts/shared-infrastructure-currency-policy-v1.md:140-143] — one reported identity per component can hide a stale node or replica.
- [x] [Review][Patch] The last acceptance row records "Missing check", outside the pass/fail vocabulary [docs/contracts/shared-infrastructure-currency-policy-v1.md:245]
- [x] [Review][Patch] README says "monthly" for the monthly drill and omits the no-waiver automatic block [README.md:378-385]
- [x] [Review][Patch] The BH1 deferred entry is stale under staging evidence policy v2 [_bmad-output/implementation-artifacts/deferred-work.md:2361-2363] — spine row 337 (v2, 2026-11-19) requires a new idempotent command if one exists, otherwise a module-suite-named existing one. "Restore the mandatory new-idempotent-command rehearsal" would reintroduce a rule v2 removed.
- [x] [Review][Defer] The epic-9-context.md regeneration in fa293aa9 dropped more constraints than BH1-BH4 [_bmad-output/implementation-artifacts/epic-9-context.md:23-24,29] — deferred: agent-context fix. The story's own commit caused BH1-BH4. It also dropped `--rollback-on-failure` from the forbidden-rollback list and the Story 9.5 deputy-gap note. The currency bullet omits the weekly trigger and still says "Story 9.1 decides" rather than linking v1.

**Rejected:**
- false — spec `done` vs sprint-status `review`: bmad-build convention; this review sets the final status.
- false — stale approval reused across attempts: the policy requires "its Administrator record", and the spine (Empty or degraded production) binds each Administrator record to one attempt.
- false — names every component as a blanket waiver: Decision 4 needs every failing component named; naming instances explicitly is not a generic acknowledgment, and Administrator holds the approved-mode authority.
- false — approval sequence unworkable: Administrator names components from the latest result; any newly failing component refuses the attempt until a new approval names it (table row).
- false — upstream fetch hangs under the lock: unavailable data fails closed with a recorded result; lock lifetime follows the existing attempt-ownership rules.
- false — passing check whose record write fails: the last acceptance row blocks or refuses any attempt without a completed recorded check.
- false — advisory scope: Decision 3 applies to every published advisory regardless of severity. Late disclosure is decided by "a later advisory observation … does not restart it". Withdrawn advisories are rare, and covering them adds rules (low).
- false — conflicting authoritative sources: 4.9 names one authoritative source per component; unverifiable evidence fails closed.
- false — BH5 untracked: the 9.2 spec at HEAD records 9.1 as complete (lines 60, 76, 93).
- false — deferred-work traceability: each entry carries `source_spec` and names the generated Epic 9 context; ledger headings vary.
- false — the triage "Verdict" column holds severities: high/medium/low is this workflow's verdict vocabulary.
- false — retained evidence unverifiable: gunzip of the `.gz` matches fa293aa9's raw `.diff` byte for byte (SHA-256 `eee1fb23…`).
- low — re-bind approvals to failure reasons: Decision 4 names components; the fix would edit the frozen decision.
- low — inventory discovery of unlisted components: adds a discovery requirement; shared changes run only through the inventory-pinned workflow, and revisit conditions cover gaps.
- low — early warning before a security deadline: Decision 2 fixes the notification triggers; adding one edits the spec.
- low — detecting a missed weekly run: new monitoring machinery for a result that blocks nothing; a revisit condition covers it.
- low — notification delivery retry or escalation: the delivery outcome is recorded; retry machinery is more than a direct fix.
- low — `checkedAt` clock source: adds a requirement; `checkedAt` is already the admission evaluation time, with start and completion recorded.
- low — roles held by different people: sole owner; unlikely.
- low — policy text has no commit anchor: a file cannot contain its own digest; the spec records it, and the closing commit is determinable.
- low — `review_loop_iteration` unchanged: spec bookkeeping; the fix edits the spec under review.

### Review Findings (delta review, 2026-11-26)

Code review 2026-11-26 (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) over `6046e6f1^..868b11cc`, the unreviewed 2026-11-25 patch delta, scoped to the policy, README, this spec and deferred-work. Verification Gap found no gaps.

- [x] [Review][Patch] Define which production attempts trigger the check [docs/contracts/shared-infrastructure-currency-policy-v1.md:56-58] — resolved from decision 2026-11-26: Administrator chose "release promotions". Define a production release attempt as one that deploys a release in automatic or Administrator-approved mode. Rollbacks, configuration-only changes, rotation rollouts, catalog activations, other approved manual changes, infrastructure-kind and recovery-kind attempts are outside this trigger; the weekly check still covers that infrastructure. Record the decision in the header. Finding: The patch review changed "every production attempt" (frozen Always, AC1, spine G3 row) to "every production release attempt" and never defined it. Spine release-kind attempts also cover rollbacks, configuration-only changes, rotation rollouts, catalog activations, realm-contract and operator-artifact changes and baseline re-deploys, and Administrator-approved manual workload or routing changes are approved-mode attempts. The record trigger is still `production-attempt`. Story 9.2 cannot tell which attempts are gated without a further policy choice (AC2). Medium (blind-hunter+acceptance-auditor+edge-case-hunter).
- [x] [Review][Patch] Fixes published only on unsupported lines fail closed [docs/contracts/shared-infrastructure-currency-policy-v1.md:123-124,280] — resolved from decision 2026-11-26: Administrator chose "no deadline, record". An advisory whose published fixes are all on lines unsupported at check time has no release-based deadline; record the advisory, those fixed releases and the reason, and make row 280 a pass with that record. Other cases where no deadline can be established (unknown timing, order or applicability) still fail closed. Record the decision in the header. Finding: Frozen Decision 3 starts a deadline only from a fix on the deployed line or the earliest supported line carrying it. When every published fix is on a line unsupported at check time, neither clause applies, as with row 281's no-fix pass. The patch fails it closed although nothing is unknown, and no compliant state exists until upstream fixes a supported line, so automatic promotion stays blocked indefinitely. Medium (acceptance-auditor).
- [x] [Review][Patch] Monthly-drill failures add no block without an Administrator decision [docs/contracts/shared-infrastructure-currency-policy-v1.md:86-87,221,290] — resolved from decision 2026-11-26: Administrator kept "no block". No rule change; list the 2026-11-26 decision in the header. Finding: The frozen Always rule says a failed check blocks automatic promotion; Decision 2 exempts only the weekly check. The 2026-11-25 patch chose "no block" for drill results. Each attempt's own current check still blocks stale infrastructure, so the gap is process-only. Low (acceptance-auditor).
- [x] [Review][Patch] README drops the no-enforcement disclaimer and reads as enforced [README.md:401-408] — Spec Never: "Do not claim enforcement or current compliance." The present-tense "The policy checks…" replaced the sentence saying publication establishes neither. Medium (blind-hunter+acceptance-auditor+edge-case-hunter).
- [x] [Review][Patch] The replacement-job epoch rule can gate recovery [docs/contracts/shared-infrastructure-currency-policy-v1.md:72-73] — Spine Attempt ownership lets a replacement job resume the same attempt only to perform its remaining single recovery. "A replacement job with a new epoch needs its own check" either describes a state admission never reaches or makes Story 9.2 gate a partly mutated production's recovery on a fresh check that fails closed on unavailable upstream data. Say the resumed recovery is not admission and is not gated; keep row 291's stale-epoch refusal. Medium (blind-hunter+edge-case-hunter).
- [x] [Review][Patch] The result-record paragraph omits the release-mode binding [docs/contracts/shared-infrastructure-currency-policy-v1.md:207-208] — Lines 70-71 bind attempt ID, release mode and epoch; the record paragraph binds only attempt and epoch (blind-hunter+edge-case-hunter+acceptance-auditor).
- [x] [Review][Patch] Row 276 says "earliest" while the rule says "oldest … in upstream release-line order" [docs/contracts/shared-infrastructure-currency-policy-v1.md:276] — "Oldest" is the literal reading of the frozen "earliest supported line"; only the example wording diverges (edge-case-hunter+blind-hunter+acceptance-auditor).
- [x] [Review][Patch] Month- or quarter-precision boundaries are undefined [docs/contracts/shared-infrastructure-currency-policy-v1.md:101-104] — Node OS sources often publish end of life by month only; extend the date-only start-of-period rule (edge-case-hunter).
- [x] [Review][Patch] The new deferred entry cites "BH1-BH4 above", labels the ledger does not carry [_bmad-output/implementation-artifacts/deferred-work.md:2741] (blind-hunter).
- [x] [Review][Patch] The BH1 deferred entry omits staging v2's refusal case [_bmad-output/implementation-artifacts/deferred-work.md:2362-2363] — Spine Staging preparation: an unknown delta, missing suitable witness or failed readback refuses automatic compatibility. A context restored from the entry would drop it (edge-case-hunter).
- [x] [Review][Patch] The header names an unlinked "AD-26 snapshot" [docs/contracts/shared-infrastructure-currency-policy-v1.md:6] — The only retained identity of the 2026-11-16 text (`eebd46aa…`) is the review-only AD-26 index at `2752ef60`; link it so readers can tell it from canonical v1 (blind-hunter).

**Rejected (delta review):**
- false — fallback deadline moves both ways: the Administrator's 2026-11-25 "deployed line governs" decision moves the deadline to the deployed line's first fix (the day-30 to day-50 example); check-time eligibility follows the frozen "earliest supported line"; line 115 retains the selected fixed release.
- false — "any evidence" makes fix evidence optional: unknown fix status fails closed (line 125), so a fixed judgment needs evidence; "any" covers the unfixed case, where none exists.
- false — weekly notification content unconstrained: the spine Diagnostics row limits every notification's content; the policy uses that channel and does not override it.
- false — weekly target undefined without active production (blind-hunter, edge-case-hunter; 2 findings): decision 1 checks only shared and environment-layer infrastructure, which exists whether or not an application serves; empty production means no working baseline.
- false — component instances versus IDs: step 1 mirrors hosted-profile binding v2 (`expectedComponentIds` plus a source enumerating every actual instance); step 2 observes each instance from that independent set.
- false — Story 6.22 patch drift: drift fails by frozen design, unchanged from the 2026-11-16 text; re-pinning through Story 4.9 is the path back.
- false — Story 9.7 sentence is a no-op and currency v2 has no gate (blind-hunter, edge-case-hunter; 2 findings): the 2026-11-25 review limited the sentence on purpose; lines 239-241 and the 9.2 row require a new version and explicit adoption bound to the exact text with qualified cases.
- false — Story 9.8 gets an ongoing duty: Story 9.8 AC2 and AC3 own automatic promotion conditioned on a passing currency check.
- false — final status not recorded: this review sets it.
- false — README sentence and link formatting: neighbouring policy sections use the same trailing-link lines (README 389-393), and the sentence parses.
- false — new ledger entry lacks `owner:`: neither the sweep workflow nor the 2026-11-16 Story 9.1 entries use the field.
- false — BH1 entry rewritten in place: the 2026-11-25 review's patch directed that update.
- false — historical triage row still states the old rule: the patch review kept it on purpose; the ledger carries the current rule.
- false — recording becomes conditional: the qualifier applies only when the record store fails, and admission is then blocked or refused, so nothing proceeds unrecorded.
- false — extra profile IDs: "same set" is equality, and step 5 passes only when every required comparison passes.
- spec edit — second patch round unrecorded in Implementation Notes (blind-hunter, acceptance-auditor; 2 findings): all 14 patch-review patches are present at `868b11cc`; recording them edits the spec under review.
- spec edit — 2026-11-25 verification retains no script: the auditor re-verified the frozen-block hash, policy SHA-256 `864af1c6…`, the 25 cases and the links.
- spec edit — the approval-reuse rejection cites the wrong spine row: reuse stays within Decision 4, since any newly failing component refuses the attempt.
- low — result from another policy version or evaluator: the evaluator runs inside the production workflow and Story 9.2 adoption binds the exact text and evaluator (line 250); the fix adds verification requirements.
- low — unordered or tied release lines: line 123 already fails closed when no eligible deadline can be established; the fix adds Story 4.9 requirements.
- low — NotReady, cordoned or Pending instances: Story 9.2 implementation detail; the fix adds branch rules.
- low — weekly run during an in-flight attempt: the weekly result blocks nothing; the fix adds lock coordination.

### Review Findings (final delta review, 2026-11-26)

Code review 2026-11-26 (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) over `f37c42d0..d16f5fb6`, the 2026-11-26 patch pass, scoped to the policy, README, this spec, sprint-status and deferred-work. Verification Gap found no gaps. Line references are at `d16f5fb6`.

- [x] [Review][Patch] Rows 326 and 327 record "No production-attempt check", which is neither pass nor fail [docs/contracts/shared-infrastructure-currency-policy-v1.md:326-327] — AC2 requires every table row to determine pass or fail and the automatic/approved outcome. The 2026-11-25 review replaced "Missing check" for the same reason. Mark both result cells as not applicable, saying no production-attempt check runs or is recorded, and keep the scope outcome columns. Low (acceptance-auditor).
- [x] [Review][Patch] The calendar-period end-of-life rule covers only months and quarters [docs/contracts/shared-infrastructure-currency-policy-v1.md:123-126] — No rule covers a year-only or half-year value ("2027", "H1 2027"), so Story 9.2 would have to choose between start of period and unresolved (AC2). Extend the start-of-period rule to any calendar period published without a date (month, quarter, half-year or year). An unresolvable period still fails closed. Not adopted: a pass row one second before the period start, which line 113 already decides, and distance-dependent handling of unresolvable periods (rejected below). Low (blind-hunter+acceptance-auditor+edge-case-hunter).
- [x] [Review][Patch] The rewritten advisory sentence dropped the no-masking clause [docs/contracts/shared-infrastructure-currency-policy-v1.md:154-155] — The patch replaced "All applicable advisory deadlines must pass; one recently fixed advisory cannot hide another overdue fix" with "Every applicable advisory deadline must pass." The new sentence uses lowercase "must", outside the MUST convention, and can be read as the deadline having elapsed. Restore: each applicable advisory MUST meet its own deadline, and a fix for one advisory cannot hide another overdue fix. Low (blind-hunter+edge-case-hunter).
- [x] [Review][Defer] Planning sources and the Story 9.2 draft still use the trigger and drill wording from before the decision [docs/contracts/shared-infrastructure-currency-policy-v1.md:68-73] — deferred: the fix edits planning artifacts and another story's spec, which this story's Never rule forbids. Policy lines 70-73 say the 2026-11-26 decision defines the trigger for the PRD, architecture and sequencing. These sources are unchanged:
  - PRD: `prd.md:428` ("Check at each production attempt … Failure blocks automatic promotion")
  - Spine G3 row: `ARCHITECTURE-SPINE.md:564` ("Check each production attempt, monthly drill and weekly schedule. A failure blocks automatic promotion.")
  - Epics: `epics.md:448,4723,4728,4735`
  - Sequencing: `sequencing.md:79`
  - 9.2 draft spec: lines 17, 19 and 88 ("Preserve the original Story 9.2 production-attempt and monthly-drill acceptance criteria")

  No ledger entry routed their alignment. A 9.2 implementer starting from these sources may gate rollbacks or recovery, or block on drill failures. Medium (blind-hunter+acceptance-auditor).

**Rejected (final delta review):**
- false — spec `done` vs sprint-status `review` (blind-hunter): this is the bmad-build convention, rejected in both earlier reviews, and this review sets the final status. `review_loop_iteration` is spec bookkeeping, so changing it is a spec edit.
- false — exclusion list omits environment-layer and profile changes and goes beyond the decision (blind-hunter): lines 69-70 apply the trigger only to release-promotion admission, so every other lock-holding change kind is already outside it. The extra listed kinds are not promotions; the "release promotions" decision excludes them, and the delta finding itself listed them.
- false — a promotion that bundles an excluded change kind loses its check (edge-case-hunter): "Separately admitted" (line 75) qualifies every listed kind, and lines 68-70 apply the trigger to the admission of every release promotion.
- low — attempt-kind classification and unknown kinds (blind-hunter): the spine Timing and interruption row records each attempt's kind at lock acquisition. A fail-closed default and an exemption record would add branches.
- false — scope of the Story 6.22 urgent patch is unclear (blind-hunter): the spine Shared infrastructure row makes the urgent in-place security patch a shared-infrastructure change, which line 78 excludes as infrastructure-kind. Lines 286-288 keep the check for each release attempt.
- spec edit — no notification for advisories without a deadline (blind-hunter): frozen Decision 2 fixes the notification triggers, and the 2026-11-26 decision chose "record" for the unsupported-only state.
- false — row 314 reverses an overdue failure, beyond the decision (blind-hunter): the decision covers lines "unsupported at check time", and line 135 re-evaluates eligibility at every check. Row 314 is the literal outcome of that rule, and it preserves the earlier failure.
- low — a boundary crossed between `checkedAt` and the first mutation (blind-hunter, edge-case-hunter; 2 findings): the gap is the admission-to-mutation interval under the lock. Re-evaluating at mutation adds machinery, and the 2026-11-25 BH6 patch already fixed the consumption point.
- false — an earlier matching pass can be reused when no boundary is crossed (edge-case-hunter): line 87 makes `checkedAt` this admission's evaluation time, and lines 92-94 refuse earlier results. Row 330 adds a case but permits nothing new.
- false — canonical v1 revision unrecorded, so d16f5fb6 could be pinned as v1 (blind-hunter, edge-case-hunter; 2 findings): lines 9 and 267-269 fix v1 at the revision that closes this final review, which this review produces. A file cannot carry its own commit (rejected on 2026-11-25).
- spec edit — two "final" policy hashes and an unnamed "previous revision" (blind-hunter; 2 findings): each sits in its own dated verification block.
- spec edit — "six routed patches" against eight Patch rows (blind-hunter, acceptance-auditor, edge-case-hunter, verification-gap; 4 findings): all eight build-review patches are in the policy. The three trigger-scope rows share lines 68-79, which leaves six distinct changes.
- spec edit — Implementation Notes omit the build-review round (blind-hunter).
- spec edit — the 2026-11-26 decisions are missing from the Spec Change Log (acceptance-auditor): they are recorded in the delta-review findings and in the policy header (lines 5-8).
- false — the README caveat is weaker than the finding it closes (blind-hunter): the README names Story 9.2 as the owner of enforcement and says runtime enforcement and current compliance require separate evidence. It claims neither.
- false — the AD-26 link is mutable and ambiguous (blind-hunter): the linked index calls itself a "Review-only source and file identity index" and records revision `2752ef60`, blob and SHA-256 `eebd46aa…` for the policy. It has not changed since it was created in `96383a27`.
- low — the Story 9.6 row does not require proving that recovery resumption is ungated (blind-hunter): the fix adds a rehearsal requirement, and Story 9.2 qualifies row 327.
- low — timestamps without a zone (edge-case-hunter): upstream sources publish zoned instants or dates, and lines 217-220 already fail unverifiable evidence closed. An explicit rule would add a branch.
- low — a fixed line that enters support more than 30 days after its fix fails immediately (edge-case-hunter): frozen Decision 3 measures from the upstream release, so the outcome is determinate. Changing the start would edit that decision.

## Implementation Notes

- 2026-11-16: Resumed the approved spec after the user chose prerequisite-first sequencing for Story 9.2. Published the policy and linked it beside README's staging policies. No inventory, checker, infrastructure, pins, workflows or planning sources changed.
- Preserved the approved intent block byte-for-byte (SHA-256 `87f576e58e63b83ed84e1f342217998b19d120372bc692080e0683e54ab92cbc`). Added explicit deadline boundaries, evidence requirements, complete-failure-set naming and downstream ownership within those decisions.

- Final independent review: three direct policy corrections applied, four generated agent-context findings deferred as required by the review workflow, one spec-bookkeeping finding rejected. Edge-case review returned no findings; verification-gap review found no gaps.
- Final verification rechecked all seven policy links/anchors, README discovery, staged/worktree whitespace and unchanged approved intent. Policy content SHA-256: `eebd46aa30c58be1879aab899bec5874bfe8cf0ff7c42bfced794c28ca7d454a`.

- Retained the reviewed unified diff as `evidence/9-1/implementation-review-20261116.diff.gz`; gzip round-trip preserves exact patch bytes while avoiding whitespace warnings from unified-diff context lines. Final aggregate whitespace check passes.

- 2026-11-25: Applied the ten direct review patches, including the Administrator's fixed-release timing decision. Updated the BH1 deferred entry to reflect both staging evidence policy v2 command-witness cases. Generated Epic 9 context corrections remain deferred.

- 2026-11-26: Applied all eleven delta-review patches. Recorded the Administrator's release-promotion scope, unsupported-only fix handling and monthly-drill disposition. Clarified recovery resumption, release-mode binding and UTC normalization. Restored the README's enforcement caveat, linked the historical AD-26 index and corrected the two deferred-work references. The prior 2026-11-25 patch-review delta is retained at `868b11cc8151c3521c152f29370fdb3cb5a760d3`.

- 2026-11-26 final delta review: applied the three low patches. Rows 326 and 327 now record "Not applicable". The start-of-period rule now covers any calendar period, and rows 305 and 306 add the year example and the generalized unresolved case. The advisory sentence restores the MUST wording and the no-masking clause. The Story 9.2 trigger and drill wording in the planning sources is deferred to `deferred-work.md`.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| BH1: new-idempotent-command rehearsal weakened | medium | Defer: generated Epic 9 agent context says “where applicable”; architecture staging gate requires at least one new idempotent command. Context-only correction is deferred under the review workflow's agent-context rule. |
| BH2: deputy ingress limits omitted | medium | Defer: generated Epic 9 context omits restoration only to recorded pre-incident state and closed user ingress before G2; architecture recovery rules retain both. Context-only correction deferred. |
| BH3: recovery timing accounting omitted | medium | Defer: context drops operator/provisioning time from recorded recovery duration; the architecture's recovery metrics retain it. Context-only correction deferred. |
| BH4: rollback set calls baseline “previous application” | medium | Defer: context wording can select a failed/interrupted previous attempt instead of the working baseline required by AD-15. Context-only correction deferred. |
| BH5: 9.2 draft code map records old prerequisite state | low | Reject: planning-bookkeeping finding whose correction edits a build spec, excluded by the review workflow. Current policy exists and 9.1 frontmatter is authoritative; selected-sequence progress will be recorded when completed. |
| BH6: check consumption time unspecified | medium | Patch: “before mutation” permits a queued earlier result to cross an input/deadline change. Clarify the existing production-admission point under the attempt lock/epoch, with current inputs and deadlines; no new age window or public API. |
| BH7: evaluator identity omitted from result fields | medium | Patch: downstream 9.2 adoption already requires evaluator identity, but the required record table omits it. Repeat that existing requirement in the record table so evidence retains the qualified evaluator. |
| P1: coverage text assumes two database deployments | medium | Patch: policy says Keycloak's database and production PostgreSQL are separate instances despite explicitly leaving topology to 4.9. Describe coverage of actual deployment instances without assuming separate physical deployments. |

### Patch review, 2026-11-25

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| Blind Hunter: result omits the production lock epoch | medium | Patch. Architecture permits a replacement job to retain the attempt ID and advance the epoch. The result must distinguish the current admission from evidence produced under an earlier epoch. |
| Blind Hunter: currency result has no separate writer authentication or digest verification | low | Reject. The separate result protocol would add implementation requirements. The architecture already requires the authorized executor to sign the production attempt record. Story 9.2 owns the checker and its evidence integration. |
| Blind Hunter: fallback selection changes as supported lines change and is undefined when no eligible fix remains | medium | Patch. The fallback uses the set supported at check time and orders it by upstream release-line order. If published fixes exist but eligibility or timing cannot establish a deadline, the existing fail-closed rule applies. |
| Blind Hunter: a secure pin can hide the reasons an observed drifted version fails | medium | Patch. Comparison step 2 covers pins, while decision 3 requires the deployed version's security status. Retain both comparisons so a drifted node's additional reasons are visible. |
| Blind Hunter: expected scope is derived from the inventory under check | medium | Patch. The adopted hosted-profile v2 construction contract supplies a separate composing inventory. A currency check must establish its complete scope against that coverage evidence. |
| Blind Hunter: evidence cannot distinguish unknown support from known support without an announced end date | low | Patch. Decision 3 fails unknown support. The record must be able to represent authoritative confirmation of current support where a boundary has yet to be announced. |
| Blind Hunter: rollout timing permits entry through G1 before currency evidence exists | false | Reject. Decision 2 already requires a recorded check at every production attempt and monthly drill. Story 9.8 adds automatic-mode qualification and does not delay those obligations. |
| Blind Hunter: production trigger could be mistaken for a generic recovery precondition | low | Patch. The architecture distinguishes production release attempts from recovery and shared-infrastructure changes. These two promotion modes apply at release admission. |
| Blind Hunter: weekly target is unspecified | medium | Patch. Cadence alone permits a run against a different environment. The policy's production environment-layer scope identifies the active production deployment and the shared infrastructure that serves it. |
| Blind Hunter: the isolated drill needs a control-plane source for upstream observations | low | Reject. The check binds observations to the drill, while architecture applies egress denial to the restored workloads. The two are compatible. Placement of the upstream observer is Story 9.2 implementation work. |
| Blind Hunter: weekly notifications omit the established delivery path | low | Patch. The architecture's Diagnostics and notification row prescribes the private GitHub issue channel. Point to that existing path. |
| Blind Hunter: weekly qualification omits the instant outside the notice window | low | Patch. The passing examples exercise the inclusive endpoint only. Add a case at 2,592,001 seconds to keep the boundary testable in Story 9.2. |
| Blind Hunter: qualification omits preservation of an existing promotion stop | low | Patch. Decision 4 explicitly prohibits clearing a stop, while the 9.6 evidence row only says no stop is set. Qualify preservation of the observed revision. |
| Blind Hunter: spec and sprint review statuses differ during the run | false | Reject. Build moves the spec to in-review in Step 4 and synchronizes sprint status in Step 5. |
| Edge Case Hunter: only unsupported lines have published fixes | medium | Patch. The no-published-fix state does not apply to known releases. An applicable deadline cannot be established, so record the advisory and fail closed. |
| Edge Case Hunter: branch age and fix publication order can disagree | medium | Patch. Separate upstream line selection from first fixed release on the selected line and show the outcome when publication order differs. |
| Edge Case Hunter: observed drift can omit deployed support and advisory evidence | medium | Patch. Apply the existing comparisons to every observed deployed version as well as its pin. |
| Verification Gap: the archived review snapshot could be mistaken for the v1 freeze point | low | Patch. The AD-26 index identifies review-only material at `2752ef60cf821accbab1937b4ce94d584da3ea6a`. Explicitly identify the revision that closes final Story 9.1 review as the canonical freeze point. |
| Verification Gap: the BH1 triage entry describes the previous staging rehearsal rule | low | Reject. The triage entry records the November 16 review. The updated deferred-work entry carries the current v2 rule. Changing the historical triage row would edit this build's spec. |

### Build review of the 2026-11-26 patch

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| Blind Hunter — Story 9.1 criteria retain the original attempt wording | medium | Reject as an edit to this build's spec. The criteria preserve the original requirement, and decision 2 of v1 records the Administrator's later definition of the production trigger. |
| Blind Hunter — G3 and Story 9.2 use broader attempt wording | medium | Patch. The architecture's G3 row and Story 9.2 can be read to include changes with no release promotion. Identify decision 2 as the governing interpretation and carry it into the downstream obligations. |
| Blind Hunter — Promotion steps can be mistaken for exempt changes | medium | Patch. An ordinary promotion commits the candidate's catalog generation. Apply the exclusions to separately admitted operations and qualify the combined case. |
| Blind Hunter — Unsupported-only outcomes omit per-line support evidence | medium | Patch. The record requires dated support observations for deployed and pinned versions. Retain the same evidence for every fixed release line counted toward the unsupported-only exception. |
| Blind Hunter — A quarter can be ambiguous without a calendar | low | Patch. Resolve the period from the authoritative source and apply the existing fail-closed rule when it cannot be mapped to a UTC instant. |
| Blind Hunter — Qualification omits stale results whose binding still matches | medium | Patch. A check can expire when a boundary is crossed with the same attempt, mode and epoch. Qualify the refusal independently of binding mismatches. |
| Blind Hunter — Qualification omits a fallback line leaving support after its deadline | low | Patch. The eligible fix set can change at end of life. Qualify the current no-deadline result and retention of the earlier failure. |
| Edge Case Hunter — Original acceptance and release admission scope differ | medium | Patch. The November 26 decision defines the original production-attempt wording for the currency check. Apply that definition to the implementation and qualification handoff. |
| Verification Gap — Earlier PRD and G3 wording can gate rollback | medium | Patch. The PRD and architecture use broader wording than the recorded decision. Scope the handoff to release promotions and retain the separately admitted recovery case. |

## Verification

**Commands:**
- `git diff --check` -- expected: no whitespace errors.

**Manual checks (if no CLI):**
- Every local link and anchor in the new document and README resolves; every table row matches the frozen decisions; no enforcement claim.

**Executed verification (2026-11-16):**
- `git diff --check` and an independent new-policy whitespace inspection: passed.
- Disposable local-link/anchor audit: all seven policy links and anchors resolve; README's new policy link resolves.
- Manual matrix audit against the published acceptance/refusal table: Current, Drift/unknown, Lag, Approved/named and Approved/unnamed all match the frozen decisions. Exact support/security boundaries, unresolved digest versions, unknown coverage and weekly notification cases are also explicit.
- These are documentation checks. Executable evaluation and runtime enforcement are Story 9.2 obligations; no behavioral implementation test is claimed.

**Executed verification (2026-11-25):**

- `git diff --check` passed. The frozen intent block matches `6046e6f15d220b84a12488178cd1114778e7e81b` byte for byte. The final patch preserved the review triage rows byte for byte.
- The final disposable documentation audit verified all 25 acceptance and refusal cases and all ten affected local links and anchors, including the README and deferred-work links. It checked date-only UTC normalization, the weekly notice boundary at 2,592,000 and 2,592,001 seconds, and the deadline arithmetic for a deployed-line backport and competing fallback lines.
- The final policy content SHA-256 is `864af1c63e7844b8ad62ecdc159b018a750290f7fda369d4a79f814bf71fafc2`.

**Executed verification (2026-11-26):**

- `git diff --check` passed. The frozen intent block matches the previous revision byte for byte (SHA-256 `87f576e58e63b83ed84e1f342217998b19d120372bc692080e0683e54ab92cbc`).
- A disposable documentation audit checked all 30 acceptance and refusal cases and 12 local links and anchors. It verified the AD-26 snapshot against its recorded revision, Git blob, byte count and SHA-256. It also checked UTC month and quarter boundaries, the inclusive weekly notice window, and the security deadline boundaries and release-line examples.
- The policy content SHA-256 before the build review was `deb446f6cef311562fbd962088cc3eff888ebfae2804e365d6531da814061de4`.
- The three build reviewers examined the current Story 9.1 delta from `f37c42d04d4347c1bf50208511c82997485d25b9`. The original baseline also spans unrelated changes. All six routed patches passed a final disposable documentation audit of 34 cases, 12 links and anchors, the UTC boundaries, the historical AD-26 identity and the unchanged frozen intent. Scoped `git diff --check` passed.
- The final policy content SHA-256 is `dc8016f977b8a8a732d20a43d90aedbc786b5d5a04171087c8528c678c21f1a9`. Verification covers the policy text and references. Story 9.2 owns runtime implementation and verification.
- Final delta review of `f37c42d0..d16f5fb6`: after the three patches, a disposable audit found the following:
  - All 34 acceptance and refusal rows have a result cell that starts with Pass, Fail or Not applicable.
  - All 11 local links and anchors resolve: the policy, the README section and the new ledger entry.
  - The policy has no lowercase "must".
  - The frozen intent block matches `d16f5fb6` byte for byte (SHA-256 `87f576e58e63b83ed84e1f342217998b19d120372bc692080e0683e54ab92cbc`).
  - Scoped `git diff --check` passed.
  - The policy content SHA-256 at the close of the final Story 9.1 review is `c75a8b0b85ee1aa73615e5906dc0d3aa8a5601743fefd9de4b6eb1d3b817a4a9`.
