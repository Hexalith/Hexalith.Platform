---
date: 2026-09-29
gate: CONCERNS
decision: Proceed; sprint-status.yaml regenerated against the edited epics.md (144 stories). Fix the open findings below before the affected work starts.
supersedes: 2026-09-28 gate (C-01–C-19), commit 19eeee8facaa0d98f4c83057d4124d796c9c28c4
inputs:
  - _bmad-output/planning-artifacts/epics.md (at 9360ee3)
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-28.md
  - _bmad-output/specs/spec-platform/ (SPEC.md, acceptance-criteria.md, sequencing.md, success-measures.md)
  - _bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md, addendum.md
  - _bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md (update 4)
---

# Implementation Readiness — Hexalith Platform (re-gate)

**Verdict: CONCERNS.** The edit that applied the 2026-09-28 sprint change proposal only partly landed. Most corrections reached the requirements inventory (AR-23, AR-28, AR-32, AR-33, AR-44), the epic notes and the External Prerequisites Register. Many never reached the stories' acceptance criteria: Stories 4.14, 5.7, 6.6, 6.7, 6.8, 8.4, 8.10, 8.13 and 9.5 are word-for-word unchanged. The edit also used a smaller renumbering than the proposal's §6/F5 laid out. Separate stories for capture-lag, reference suites, adapter conformance and executor commissioning were folded into existing stories, but the register and epic notes still read as if those stories exist.

**Current work is unaffected.** Stories 4.0–4.4 (the urgent Kubernetes track) are split and sequenced correctly (C-15), and the story files on disk for 4.0–4.3 match `epics.md`.

| Status | Findings |
| --- | --- |
| Resolved | C-02, C-03, C-05, C-06, C-09, C-15 |
| Partly fixed | C-01, C-04, C-07, C-08, C-10, C-12, C-14, C-18, C-19 |
| Still open | C-11, C-13, C-16, C-17 |
| New (from the edit) | N-01 – N-11 |

Line numbers refer to the current `epics.md`. Fix the findings with a direct edit or `bmad-correct-course`, then re-run `bmad-sprint-planning` to resync `sprint-status.yaml`.

## Fix before Epic 2 accepts CI evidence

| ID | Sev | Status | Remaining gap | Where | Suggested fix |
| --- | --- | --- | --- | --- | --- |
| C-01 | Medium | Partial | The policy is now decided in 2.1, but it reaches the runner contract only in 3.1 (Epic 3). Story 2.10 still marks the runner accepted "so that local and CI integration evidence can be accepted". Stories 2.8 and 2.10 are not limited to non-candidate (lifecycle) evidence. | L1543, L1570 | Narrow 2.10 to non-candidate evidence, or move 3.1 into Epic 2 after 2.2. |
| N-01 | Medium | New | Story 2.2 says Epic 3 only fills existing fields "without a version change". Story 3.1 writes the policy identifier, gated decision and reused-evidence lineage, which 2.2 never defines. | L1346, L1591 | Define those fields in 2.2, or let 3.1 version the contract. |

## Fix before the staging build (Story 4.5 onward)

| ID | Sev | Status | Remaining gap | Where | Suggested fix |
| --- | --- | --- | --- | --- | --- |
| C-18 | High | Partial | AR-23 and AR-32 now say a named shared-infrastructure/bootstrap workflow creates namespaces, environment-identity RBAC, StorageClasses and PriorityClasses. Stories 4.14 and 6.7 still have the environment-layer identity create them. No story builds that workflow before 6.22. Story 6.22 refuses any mutation without a complete recovery point, which exists only from 8.7. Moving the creation onto 6.22 as written would block 4.14 and 6.7 until Epic 8. | L404, L448, L2258–2265, L2993–2999, L3450–3454 | Add a staging-era bootstrap step (story or 4.14 criterion) run by the shared-infrastructure identity. Record the pre-G2 bootstrap exception to 6.22's recovery-point rule. |
| C-07a | Low | Partial | Story 4.14 needs keys under Administrator and deputy custody, but 6.3 (name the deputy) is not a prerequisite. | L2275 | Add the 6.3 prerequisite, or scope 4.14 to Administrator custody. |
| C-08 | Medium | Partial | Story 4.11 claims suite ownership and blocks publication. But no criterion requires writing or running the reference composition's declarations, E2E suites and smoke suites (Parties, EventStore, Tenants, Memories, McpCli). It is tagged only Builds/Platform, so module owners don't have to review it. Register row L678 still excludes the reference composition. | L2170–2198, L2176, L678 | Add criteria that deliver and run the suites, tagged with each module repository (proposal B6). |
| N-02 | Medium | New | Story 4.13 says the retained chart "can be applied without regeneration" but names no target. Staging is built in 4.14–4.20 and first deployed in 4.22. | L2244–2246 | Name a dry-run or scratch target, or move the check to 4.22. |
| N-03 | Low | New | The capture-lag bound decision now sits inside 4.9, which waits on EventStore's AD-26. A Platform decision is now blocked by another team for no reason. AR-59 is missing from 4.9's Covers line. The clause "names who is notified when it is exceeded" was lost. | L2121, L2142–2145 | Split the bound decision out of 4.9, or declare the dependency. Restore the notification clause and the AR-59 tag. |
| N-04 | Low | New | `epic-4-context.md` is stale. It gives the old titles for 4.9 and 4.11 and the urgent set as 4.0–4.3 (epics says 4.0–4.4). Its L63 says Helm qualification depends on publication and rollback, contradicting 4.5 (L2037). | epic-4-context.md L20, L22, L40, L63 | Refresh the context file. |

## Fix before the early independent track is scheduled

| ID | Sev | Status | Remaining gap | Where | Suggested fix |
| --- | --- | --- | --- | --- | --- |
| C-04a | Medium | Partial | 4.4 is now on the early track, but 6.3 and 6.4 raise issues in its notification repository without listing 4.4 as a prerequisite. | L2886, L2895, L2905 | Add the 4.4 prerequisite to 6.3 and 6.4. |

## Fix before Epic 5 starts

| ID | Sev | Status | Remaining gap | Where | Suggested fix |
| --- | --- | --- | --- | --- | --- |
| C-11 | Medium | Open | Story 5.7 still triggers only when production has "no working baseline". Stories 6.18, 5.11 and AR-34 keep the narrow scope. | L2704–2720, L3354, L2821, L460 | Widen to every approved empty or degraded attempt, keeping "sufficient alone only for first install" (spine:288). |
| C-10 | Medium | Partial | Stories 5.9 and 8.16 now qualify the named recovery, but neither covers preserving a later stop or the candidate-removal failure path. | L2745–2771, L4150–4158; sequencing.md:117 | Add both cases. |
| N-05 | Medium | New | Stories 5.9 and 8.16 both claim to qualify the named recovery. Story 5.11 treats 5.9 as the qualification. Stories 6.18 and 6.20 require a separate post-G1 qualification (8.16). Story 5.9 depends on production's first baseline (Epic 6) and can't be used before 8.16, but states neither. It also leaves out the revocation and Memories erasure continuity that 8.16 requires. | L2823, L3353, L3401–3403, L4153–4158 | Decide which story is the qualification. Make the other a rehearsal feeding it, and link the two. |
| N-06 | Low | New | Coverage map: AR-19 is still mapped to Epic 5 though no Epic 5 story covers it (the content moved to 4.8). AR-46 is in 5.11's criteria but not its Covers line. | L649, L2806 | Fix the map and the Covers line. |

## Fix before Epic 6 starts

| ID | Sev | Status | Remaining gap | Where | Suggested fix |
| --- | --- | --- | --- | --- | --- |
| C-16 | Medium | Open | Story 6.6 is identical to the old text. The four change windows appear only in prose and in 6.22's classification rule. | L2950–2981, L814, L3449 | Split 6.6 by change window. |
| C-19 | Low | Partial | Story 6.8's per-job credentials still lack the recovery-kind attempt's recovery-hook credential and read access to the recovery-point prefix. No story issues them for 8.16. AR-34 still says every staging attempt deploys the baseline and rehearses the rollback set. | L3025–3027, L460 | Align 6.8 and AR-34 with AR-33 and 5.6. |
| C-07b | Low | Partial | Stories 6.10 and 6.18 still require lost-window review unconditionally. Only AR-44 was amended, and neither story cites it. | L3099, L3337, L513 | Apply AR-44's condition in both stories. |

## Fix before Epic 8 starts

| ID | Sev | Status | Remaining gap | Where | Suggested fix |
| --- | --- | --- | --- | --- | --- |
| C-17 | Medium | Open | Story 8.10, the first key-restore consumer, still lists only recovery hooks as prerequisites, not the tombstone/lineage work. Story 8.13 still bundles the Memories Redis → Dapr migration. Only register row L682 changed. | L3965, L4073–4076, L682 | Add the tombstone prerequisite to 8.10, and split out the Redis → Dapr gate. |
| C-14 | Low | Partial | Register L682 points to an "Epic 8 adapter-conformance story" that doesn't exist. No story qualifies the AR-60 adapter boundary. Story 8.4 still lacks "recovery owner, confirmed by Administrator". The override-records row was added (L686). | L682, L589, L3802, L3807 | Add the adapter-conformance story, or retarget the row to 8.13 with criteria. Add the confirmation to 8.4. |
| C-12 | Medium | Partial | Story 10.6 is fixed. The 8.17 drill record still lacks backup cadence and retention, availability and freshness monitoring, dead-man detection and GitHub delivery. The Epic 8 notes (L828) claim the drill has them. | L4186–4190, L828 | Add them to the 8.17 criteria. |
| C-04b | Medium | Partial / new dependency | Executor commissioning was folded into 8.9. It needs a pinned off-site copy of the workflows that 8.10–8.12 build, and its prerequisites (4.6, 4.13, 6.9, 8.5, 8.7) aren't named. AR-28 is tagged on 8.1, which now excludes commissioning, not on 8.9. | L3940–3945, L3930, L3732 | Move commissioning after 8.12 (proposal F2), name the prerequisites and move the AR-28 tag. |
| N-07 | Medium | New | Story 8.19 makes 8.16 a G2 condition. The source G2 rows don't include it; the source ties it to the first applicable degraded non-empty attempt. The proposal (F5) asked for adapter-boundary conformance in G2, not 8.16. | L4231; sequencing.md:52–55, :116; spine:494–495 | Reconcile with the source: drop 8.16 from G2, or record why it is added. |
| N-08 | Low | New | Story 8.16 has no Prerequisites field. It depends on 8.7, 8.10, 8.11 and 8.13, and on recovery-kind credentials that no story provides (see C-19). | L4128 | Add the prerequisites. |
| N-09 | Low | New | Coverage: AR-58 (first degraded non-empty attempt qualification) is covered by no story since the old 6.20 was replaced. The Epic 8 summary (L825) lists FR-9 and NFR-2 but not FR-8 and NFR-1, which the map (L626, L631) assigns via 8.16. | L579, L825 | Tag AR-58 on 8.16 or 5.9, and fix the summary. |

## Fix before Epic 9 starts

| ID | Sev | Status | Remaining gap | Where | Suggested fix |
| --- | --- | --- | --- | --- | --- |
| C-13 | Low | Open | Story 9.5 still rehearses only "the deputy cannot approve". The deputy limits on clearing a stop, admission and pre-G2 ingress, and the "resolves and recurs" case, appear only in the Epic 9 prose (L832). | L4351, L832 | Extend 9.5. |

## Minor references

| ID | Where | Fix |
| --- | --- | --- |
| N-10 | Story 3.9, L1791: "a candidate under the policy from Story 3.1". | Point to Story 2.1 (applied through 3.1's runner contract). |
| N-11 | Epic 3 AR list, L647: omits AR-14, which 3.1 now covers (L1576). | Add AR-14. |

## Resolved since 2026-09-28

- **C-02:** the closure boundary after 6.23 is declared at epic level. Story 5.6 no longer assumes a production baseline, and 5.9 branches to 5.7 when there is none.
- **C-03:** Stories 6.19, 6.20 and 6.22 no longer claim a recovery point. Data restore moved to 8.16, after 8.7 and 8.13.
- **C-05:** Epic 4 may depend on 3.4. The capture-lag bound is decided before 4.19 (but see N-03).
- **C-06:** AR-22 and the register now expect attestation from first publication. 4.8 and 4.12 verify it.
- **C-09:** Story 5.11 publishes stable `Hexalith.McpCli` versions after staging validation (tag gap in N-06).
- **C-15:** backup proofs (4.0) are split from the upgrade (4.1), with a mutation gate, and scheduled first.

## Findings carried forward and updated story references

The open policy decisions, human decisions, spike, embedded ratifications and stories blocked by the External Prerequisites Register appear in the [superseded September 28 report](https://github.com/Hexalith/Hexalith.Platform/blob/19eeee8facaa0d98f4c83057d4124d796c9c28c4/_bmad-output/planning-artifacts/implementation-readiness.md). Use the September 29 IDs and titles below when following its references.

| September 28 reference | September 29 reference |
| --- | --- |
| Candidate policy | 2.1, Decide the candidate and attachment policy |
| 5.9, staging reset | 5.10, Reset staging for unadopted incompatible candidates |
| 5.10, staging evidence evaluation | 5.11, Evaluate staging evidence for promotion |
| 6.19, in-place recovery | 6.19, Redeploy the working baseline in place as Administrator or the deputy |
| 6.20, approved incompatible release | 6.20, Enforce the G1 recovery boundary |
| 6.22, shared-infrastructure changes | 6.22, Guard shared-infrastructure changes with one controlled procedure |
| 8.16, isolated DR drill | 8.17, Run the isolated DR drill and prove RPO and RTO |
| 8.17, production telemetry retention | 8.18, Retain production telemetry off-site for incident analysis |
| 8.18, G2 user entry | 8.19, Open production to users (G2) |

The new 5.9 covers the staging rehearsal for a retained-data approved attempt. The new 8.16 covers in-place data restore and named retained-data recovery qualification (see N-05).

## November 22, 2026 readiness gate

**Verdict: FAIL.** Reviewed Platform revision `b0124db4` against the current PRD, architecture spine, Platform spec, epic breakdown and staging foundation handoff. The sprint tracker contains all 12 epic IDs and 151 story IDs. The following conflicts stopped the tracker refresh.

| ID | Severity | Finding | Sources |
| --- | --- | --- | --- |
| IR-2026-11-22-1 | High | Initial production setup needs a procedure for creating the production namespaces, identity RBAC, StorageClass and PriorityClass before a complete production recovery point can exist. Story 6.7 places these resources in the environment-layer apply. The architecture assigns them to shared infrastructure, and Story 6.22 requires a complete recovery point for its ordinary workflow. The staging handoff records separate owners and apply stages. | [Story 6.7](epics.md#story-67-build-the-production-environment-layer), [Story 6.22](epics.md#story-622-guard-shared-infrastructure-changes-with-one-controlled-procedure), [architecture release tiers](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#release-tiers), [staging foundation sequence](../../eng/staging-foundation/CONTINUATION-PACKAGE.md#ordered-owners-and-acceptance-boundaries) |
| IR-2026-11-22-2 | High | Story 5.11 requires fresh-install proof for an approved empty or degraded attempt, with compatibility or a qualified named restore required when data remain. Story 6.18 limits use of fresh-install proof to first installation without retained data. Story 5.7 also needs an explicit execution branch for a degraded target with an existing baseline. | [Story 5.7](epics.md#story-57-rehearse-a-fresh-install-when-production-has-no-baseline), [Story 5.11](epics.md#story-511-evaluate-staging-evidence-for-promotion), [Story 6.18](epics.md#story-618-run-administrator-approved-attempts-into-empty-or-degraded-production), [PRD staging validation](prds/prd-platform-2026-09-27/prd.md#staging-validation-and-production-promotion) |
| IR-2026-11-22-3 | High | Story 8.19 makes candidate-specific named-recovery qualification a universal G2 condition. The spec requires it for the first applicable degraded attempt with retained data and defines G2 through standing controls and an isolated disaster-recovery drill. A healthy first release may have no such candidate. | [Story 8.19](epics.md#story-819-open-production-to-users-g2), [Platform spec sequencing](../specs/spec-platform/sequencing.md#g2--users-admitted-ingress-open) |

The [November 22 sprint change proposal](sprint-change-proposal-2026-11-22.md) records the corrections, the owner’s approval and the cases assigned to implementation.

### November 22 re-gate after the approved course correction

**Verdict: PASS for the three November 22 planning findings.** The production foundation has a named owner, order and narrow pre-recovery-point procedure under both locks. Staging and production now require the same exact-release fresh-install proof for the approved empty or degraded path. G2 uses the independent disaster-recovery drill and standing controls, while admission of a candidate relying on named recovery requires its own qualification.

The [sprint tracker](../implementation-artifacts/sprint-status.yaml) was checked against 12 epics and 151 stories. The generator treated two existing story keys as new slugs and attempted to advance the expressly backlogged Story 4.28 because a scoped input file exists. The reconciled preview matched all statuses by numeric ID and all 19 action items. The refresh updated the tracker timestamp in place to retain the keys referenced by frozen specs and the evidence comments that the generator drops. This re-gate covers planning. Live bootstrap, release-admission and recovery evidence remain acceptance work for their respective stories.

### November 22 full planning gate follow-up

**Verdict at this gate: FAIL.** The tracker matched all 12 epic IDs and 151 story IDs. Three production requirements blocked that refresh. The preceding PASS applied to findings IR-2026-11-22-1 through IR-2026-11-22-3.

| ID | Severity | Finding | Sources |
| --- | --- | --- | --- |
| IR-2026-11-22-4 | High | FR-10 requires a complete recovery point for every shared-infrastructure change. Record the approved first-production foundation exception that Story 6.22 limits to a create-only procedure using current, verified native recovery evidence under both locks. | [PRD FR-10](prds/prd-platform-2026-09-27/prd.md#fr-10-provide-isolated-staging-and-production-environments) · [Story 6.22](epics.md#story-622-guard-shared-infrastructure-changes-with-one-controlled-procedure) |
| IR-2026-11-22-5 | High | Story 6.7 needs Story 6.9’s proof that the production attempt store is reachable from the recovery executor. Story 8.9 commissions the executor after usable production recovery points exist. Specify what Story 6.9 must verify from the selected recovery host before full commissioning. | [Story 6.7](epics.md#story-67-build-the-production-environment-layer) · [Story 6.9](epics.md#story-69-provide-the-production-attempt-lock-and-stop-store) · [Story 8.9](epics.md#story-89-fence-the-failed-environment-and-rebuild-it-on-prepared-capacity) |
| IR-2026-11-22-6 | Medium | AR-35 requires a complete post-lock point for every approved retained-data attempt. Story 6.18 makes the point explicit in its named-restore branch and leaves the compatible G1 repair requirement unstated. | [AR-35](epics.md#additional-requirements) · [Story 6.18](epics.md#story-618-run-administrator-approved-attempts-into-empty-or-degraded-production) |

The owner approved the edits and acceptance cases in the [production readiness sprint change proposal](sprint-change-proposal-2026-11-22-production-requirements.md) on 22 November 2026.

### November 22 re-gate after the production requirements correction

**Current verdict: PASS.** The re-gate resolves IR-2026-11-22-4 through IR-2026-11-22-6. The scope is planning readiness. Live acceptance evidence remains with the delivery stories.

| Finding | Verified planning rule |
| --- | --- |
| IR-2026-11-22-4 | FR-10 and CAP-10 record Story 6.22's approved create-only foundation exception before production has workload data or a working baseline. The exception uses both locks and current verified native recovery evidence. Ordinary shared changes require a complete recovery point. |
| IR-2026-11-22-5 | Story 8.1 prepares the designated off-site host in time for Story 6.9's authenticated, scoped read from the production store before Story 6.7. Story 8.9 commissions the executor after usable Story 8.7 recovery points and its other inputs exist. |
| IR-2026-11-22-6 | FR-8, AR-35, the architecture and spec, and Stories 6.14 and 6.18 require both approved retained-data branches to cut and verify a complete point under the production lock before workload mutation. Story 6.23 covers the first empty installation without retained data. A failed first installation that leaves data stays stopped if the retained-data gates cannot be met. |

The [sprint tracker](../implementation-artifacts/sprint-status.yaml) matches 12 epics and 151 story IDs. The generator preview derived different slugs for Stories 2.11 and 2.12 and treated the Story 4.28 input as a reason to advance its status. The reconciled preview matched all statuses by numeric ID and all 19 action items. The tracker refresh updated `last_updated` in place to preserve the existing keys, statuses, action items and evidence comments.
