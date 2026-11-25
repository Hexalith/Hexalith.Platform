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

- [ ] [Review][Decision] Fix-deadline start when the deployed line's fix ships after another line's — Decision 3 and policy lines 88-99 start the 30-day clock from "a release carrying its fix on the deployed line", fall back to "the earliest supported line carrying the fix", define `fixReleasedAt` as "that first applicable fixed release" and say "a later … fixed release does not restart it". If another supported line ships the fix on day 0 and the deployed line's backport ships on day 20, the deadline is either day 30 or day 50. Story 9.2 would have to choose, which breaks AC2 (blind-hunter+acceptance-auditor).
- [ ] [Review][Patch] Story 9.8 obligation requires a passing automatic-attempt result before G3, which cannot exist [docs/contracts/shared-infrastructure-currency-policy-v1.md:216] — sequencing.md:9 says every pre-G3 release uses approved mode; epics Story 9.8 AC1 records G3 on the policy, rehearsals and 9.3 confirmations; spine G3 row 564 requires qualified checks. Reword: qualified enforcement plus 9.6 evidence before G3; each automatic attempt after G3 needs its own passing check.
- [ ] [Review][Patch] Time-normalization rules are not uniform [docs/contracts/shared-infrastructure-currency-policy-v1.md:73,85,97] — the date-only "start of that date in UTC" rule covers only end of life, so a date-only fix release time leaves the one-second deadline row undecidable. The weekly "within 30 days" notice has no unit, unlike the 2,592,000-second security window.
- [ ] [Review][Patch] The Story 9.7 suspension sentence binds a story whose triggers exclude currency-policy changes [docs/contracts/shared-infrastructure-currency-policy-v1.md:206-207] — epics 9.7 and spine G3 suspend only on verification, recovery or rollback-set policy changes. Limit the sentence to what 9.7 covers; v2 adoption already governs currency changes.
- [ ] [Review][Patch] The "no published fix" row implies a vulnerability backstop that shared components lack [docs/contracts/shared-infrastructure-currency-policy-v1.md:239] — vulnerability policy v1 scans release-included images, not the node OS, etcd, Kubernetes binaries or the Rancher server. The approved column leaves the currency outcome unstated.
- [ ] [Review][Patch] The effect of a monthly-drill result is unstated [docs/contracts/shared-infrastructure-currency-policy-v1.md:57] — weekly results explicitly add no block and set no stop; drill results say neither, and the acceptance table has no drill row.
- [ ] [Review][Patch] A component instance with several observed identities (nodes, replicas) has no stated rule [docs/contracts/shared-infrastructure-currency-policy-v1.md:140-143] — one reported identity per component can hide a stale node or replica.
- [ ] [Review][Patch] The last acceptance row records "Missing check", outside the pass/fail vocabulary [docs/contracts/shared-infrastructure-currency-policy-v1.md:245]
- [ ] [Review][Patch] README says "monthly" for the monthly drill and omits the no-waiver automatic block [README.md:378-385]
- [ ] [Review][Patch] The BH1 deferred entry is stale under staging evidence policy v2 [_bmad-output/implementation-artifacts/deferred-work.md:2361-2363] — spine row 337 (v2, 2026-11-19) requires a new idempotent command if one exists, otherwise a module-suite-named existing one. "Restore the mandatory new-idempotent-command rehearsal" would reintroduce a rule v2 removed.
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

## Implementation Notes

- 2026-11-16: Resumed the approved spec after the user chose prerequisite-first sequencing for Story 9.2. Published the policy and linked it beside README's staging policies. No inventory, checker, infrastructure, pins, workflows or planning sources changed.
- Preserved the approved intent block byte-for-byte (SHA-256 `87f576e58e63b83ed84e1f342217998b19d120372bc692080e0683e54ab92cbc`). Added explicit deadline boundaries, evidence requirements, complete-failure-set naming and downstream ownership within those decisions.

- Final independent review: three direct policy corrections applied, four generated agent-context findings deferred as required by the review workflow, one spec-bookkeeping finding rejected. Edge-case review returned no findings; verification-gap review found no gaps.
- Final verification rechecked all seven policy links/anchors, README discovery, staged/worktree whitespace and unchanged approved intent. Policy content SHA-256: `eebd46aa30c58be1879aab899bec5874bfe8cf0ff7c42bfced794c28ca7d454a`.

- Retained the reviewed unified diff as `evidence/9-1/implementation-review-20261116.diff.gz`; gzip round-trip preserves exact patch bytes while avoiding whitespace warnings from unified-diff context lines. Final aggregate whitespace check passes.

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
