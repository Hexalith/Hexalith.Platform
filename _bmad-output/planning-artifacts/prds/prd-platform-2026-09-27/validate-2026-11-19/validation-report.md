# Validation report — Hexalith Platform

**BMAD grade: Fair** · 0 critical · 3 high · 3 medium · 1 low.

Reviewed 2026-11-19T13:11:28+01:00 at Platform revision `cb57dd4f`.

[Product requirements](../prd.md) · [PRD addendum](../addendum.md) · [BMAD quality rubric](../../../../../.agents/skills/bmad-prd/assets/prd-validation-checklist.md)

## Overall verdict

The seven-module scope, developer workflows and recovery objectives are well defined. The PRD is usable for planning. Correct three passages before using it to decide whether staging may open, whether an Administrator may approve a release on healthy production, or which build may qualify a pull request.

The November 18 changes are present in FR-4 and the execution clarification. Story 2.8’s recorded CI runs count toward runner lifecycle qualification under their original identities. A new candidate merge or release decision still needs its own qualifying run. The Story 2.10 execution spec contains older instructions that obscure this distinction.

[Platform brief](../../../briefs/brief-platform-2026-09-27/brief.md) · [September findings and resolutions](../update-2026-09-28/update-summary.md) · [November 18 approved correction](../../../sprint-change-proposal-2026-11-18.md)

## Dimension verdicts

| Dimension | Verdict | Assessment |
| --- | --- | --- |
| Decision-readiness | Adequate | The G1–G3 table distinguishes closed deployment, user access and automatic promotion. FR-8 names the people who can restore service, declare a stop and clear it. The addendum nevertheless presents a withdrawn staging exception in the present tense. |
| Substance over theater | Strong | FR-2 tests whether a breakpoint changes the running module. FR-4 tests actual loaded artifacts, bounded startup and resource ownership. NFR-2 defines the start and end of the recovery clock and explains who covers an outage that begins during a shift. These consequences let a reviewer reject misleading success claims. |
| Strategic coherence | Strong | The vision assigns shared hosting to Platform and business behavior to module owners. The Parties example in FR-3 shows what that boundary means in a developer's workspace. FR-6 carries it into release decisions by requiring each enrolled module's declared checks. SM-C1, SM-C4 and SM-C5 guard against passing a release with incomplete evidence, deleting another run's resources or hiding a slower startup with a longer timeout. |
| Done-ness clarity | Adequate | Every FR has testable consequences. FR-7 supplies a ten-minute rollout deadline and a five-minute verification window with concrete retry conditions. One SM-5 acceptance instruction is broad enough to reverse a valid result for an otherwise permitted approval. |
| Scope honesty | Strong | The seven-module MVP at prd.md:40–48 and the exclusions at lines 62–68 set clear boundaries. FR-9 names which acknowledged changes may be lost after a restore and gives Memories erasures stronger protection. NFR-2 conditions the four-hour recovery target on response coverage and replacement capacity. The September repository observations in addendum.md:10–24 are expressly dated. |
| Downstream usability | Adequate | The stable identifiers, glossary and named evidence owners support extraction into stories. The handoff table should identify which decisions are now published so that a team can tell policy ownership from implementation and qualification work. |
| Shape fit | Strong | Separate capability scenarios for developers, automated workflows, Administrator and the recovery deputy suit this internal platform. The PRD explains at line 36 why module business journeys stay with the modules. FR-4, FR-8 and FR-9 give role-specific acceptance scenarios where story writers need them, while the addendum preserves alternatives and their operating costs. |

## Findings by severity

### Critical (0)

No critical findings.

### High (3)

#### The addendum still permits staging on unsupported Kubernetes

*Decision-readiness*

The addendum says the pre-production support exception lasts “until G1.” The October 1 decision explicitly withdrew that waiver. On a delayed upgrade, the two instructions give opposite answers about starting staging after Kubernetes 1.34 reached end of life on October 27.

**Suggested change.** Mark the waiver superseded in the risk entry. State the accepted order there: retire KubeSphere, complete the supported Kubernetes upgrade, qualify private Rancher management, then start staging.

[Addendum, lines 370, 408 and 412](../addendum.md) · [PRD, line 414](../prd.md) · [October 1 decision, line 269](../../../sprint-change-proposal-2026-10-01.md) · [Architecture, lines 262 and 536](../../../architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md)

#### SM-5 can reject a permitted release on healthy production

*Done-ness clarity*

SM-5 calls for “refusal of approval when production is neither empty nor degraded.” FR-6 allows an Administrator to approve a pre-G3 release, or one requiring an explicit recovery plan, on a healthy baseline. SM-C1 limits its refusal to attempts invoking the degraded-production exception. SM-5’s adjacent stop-clearance wording also omits the verified approved attempt described in FR-8.

**Suggested change.** Test the two cases on the same healthy baseline. Refuse a request invoking the empty/degraded exception. Accept an otherwise eligible pre-G3 or incompatible release under its own approval rules. List both authorized ways to clear a stop, including the verified attempt that can clear only the stop the Administrator observed.

[PRD, lines 50, 181, 230, 235–241, 384–385 and 394](../prd.md) · [Acceptance criteria, line 104](../../../../specs/spec-platform/acceptance-criteria.md)

#### The CI handoff reopens the choice of commit that qualifies a merge

*Downstream usability*

The handoff asks owners to define “pull-request head or merge result,” evidence reuse and attachment limits. Policy v1 already requires the committed merge result for the exact head/base pair. A passing head-only build could otherwise be accepted for an untested merge. A distinct release decision requires fresh evidence even for identical bytes. The attachment hold defaults to 30 minutes and has a four-hour maximum.

**Suggested change.** Link policy v1 from FR-4 and the handoff row. Record these choices as settled and leave tool adoption, enforcement and integration qualification with Platform and Builds.

[PRD, lines 125–126, 137, 161, 411 and 420](../prd.md) · [Candidate policy v1, lines 28–43, 79–84 and 244–246](../../../../../docs/contracts/candidate-and-attachment-policy-v1.md)

### Medium (3)

#### The handoff still asks owners to define published hosted-release rules

*Downstream usability*

The PRD routes staging reruns, evidence age, changes to required checks and infrastructure currency back to owners. The staging policy sets a 24-hour maximum from attempt start, defines reruns and specifies who approves coverage changes. The infrastructure policy defines cadence and allowed lag and requires an approved attempt to name each failing component.

**Suggested change.** Link the two policies in the applicable handoff rows. Retain outstanding implementation, testing and operator evidence against each policy and identify any genuinely open bound separately.

[PRD, lines 179, 407–409 and 415–416](../prd.md) · [Staging policy v1, lines 76–96, 100–137 and 186–204](../../../../../docs/contracts/staging-evidence-policy-v1.md) · [Infrastructure currency policy v1, lines 50–134](../../../../../docs/contracts/shared-infrastructure-currency-policy-v1.md)

#### Story 2.10 still directs engineers to repeat superseded hosted work

*Downstream usability*

FR-4 and the approved epic accept the always-run cleanup record on disposable hosted runners. The Story 2.10 execution spec still prescribes separate cancellation workflows and full qualification on the final fixed inputs. An engineer could rerun a dedicated campaign even after the owner accepted Story 2.8’s original hosted cancellation results.

**Suggested change.** Point the execution spec to the November 18 scope. Keep the historical runs under their original identities. Run reference scenarios locally and use normal Parties CI success and failure with cleanup under the declared dependencies. Reserve the fresh-run rule for new candidate merge or release decisions.

[PRD, lines 145–149 and 161](../prd.md) · [Story 2.10 execution spec, lines 46, 68, 72 and 634–636](../../../../implementation-artifacts/spec-2-10-qualify-the-runner-lifecycle-on-the-reference-composition.md) · [Epics, lines 1503 and 1560–1567](../../../epics.md) · [November 18 decision, lines 37–41 and 99–108](../../../sprint-change-proposal-2026-11-18.md)

#### The handoff lists completed specification work as a prerequisite

*Downstream usability*

The PRD tells spec owners to synchronize recovery and deployment wording before finalizing stories. The spec package records completion of that reconciliation on October 1. Module documentation owners still hold separate work for Folders, Projects and McpCli. Reading the PRD alone could unnecessarily hold a story.

**Suggested change.** Replace the spec-owner prerequisite with a link to the completed reconciliation. Identify the remaining upstream documentation work and preserve the separate implementation and qualification gates.

[PRD, line 422](../prd.md) · [Specification sequencing, lines 221–227](../../../../specs/spec-platform/sequencing.md)

### Low (1)

#### The revision date hides the November 18 amendment

*Downstream usability*

The frontmatter dates this revision November 8. The hosted cleanup rule and the explicit reference to the rescoped Story 2.10 reflect the approved November 18 edits. Someone selecting the PRD by its date could pick the wrong baseline.

**Suggested change.** Date the existing content through November 18 and link the November amendment in the revision summary. Use the actual edit date for any subsequent update.

[PRD, lines 5, 12, 16, 148 and 161](../prd.md) · [November 18 decision, lines 7 and 118–133](../../../sprint-change-proposal-2026-11-18.md)

## Mechanical notes

- The PRD defines 12 functional requirements, 3 non-functional requirements, 6 success measures and 5 counter-metrics. All 26 IDs are unique and contiguous within their series. All three release gates are defined.
- All 25 local Markdown link occurrences in the PRD and addendum resolve, including their explicit heading fragments. Neither document contains an inline assumption tag, PM-note placeholder, TODO or TBD.
- The PRD explains why capability scenarios stand in for user journeys. The original seven-module scope and the split between module and Platform workspaces are accounted for. The September update records the resolution of its five findings.
- This review covers requirements and their handoff at repository revision cb57dd4f. Production readiness, deployed configuration and live CI outcomes require their own evidence.

## Reviewer files

[BMAD rubric review](review-rubric.md) · [Current plan alignment review](review-plan-alignment.md)
