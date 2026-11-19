# Sprint change proposal — Correct the Platform PRD acceptance decisions

**19 November 2026 · Approved by the owner and applied · Minor scope**

## 1. Issue summary

The November 19 BMAD review graded revision `cb57dd4f` **Fair**, with three high, three medium, one low and zero critical findings. It reported 26 valid requirement and measure IDs and 25 resolving local link occurrences. The trigger was a document review. Three passages could reverse an acceptance decision. Four others could send an engineer to repeat work or use an older baseline. [Validation report](https://github.com/Hexalith/Hexalith.Platform/blob/main/_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/validate-2026-11-19/validation-report.md)

## 2. Impact analysis

| Consumers | Decision affected |
| --- | --- |
| Epic 4, particularly Stories 4.27, 4.1, 4.28 and 4.14 | Operators need the same before-staging dependency in the addendum, architecture and backlog. Story 4.26 covers advance qualification of the retirement plan. Story 4.28 covers the installed manager and recovery access. |
| Epics 6 and 9, particularly Stories 6.18 and 9.5 | Rehearsal authors must distinguish the ordinary pre-G3 approval from the empty/degraded exception and exercise both ways to clear a stop. The Epic 10 repeat of SM-5 consumes the same test. |
| Epics 2 and 3, particularly Stories 2.1, 2.10, 2.14, 3.1 and 3.9 | Runner qualification, attachment and candidate acceptance each need the evidence assigned to that decision. |
| Epics 4, 5, 6 and 9 | The release handoff must identify published policy, outstanding enforcement and outstanding operator measurements. |
| Module documentation owners | Folders, Projects and McpCli own their remaining upstream alignment. Recovery and deployment story authors can use the reconciled Platform spec. |

The changes fit the existing seven-module MVP, story order and G1–G3 gates. They affect planning text, execution instructions and rehearsal criteria. The work requires no runtime deployment, schema change, changes to a policy version, or UX design. [PRD](prds/prd-platform-2026-09-27/prd.md) · [Epics and stories](epics.md) · [Architecture spine](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md)

## 3. Recommended approach

Apply the seven edits together as a documentation change. Platform can review them in one pass with the existing policy owners. Estimated effort is half a day for editing, checking the decision examples and reviewing links. The principal risk is leaving a copied criterion in the backlog that still gives the old answer. Rollback would remove approved decisions, and an MVP replan would leave the wording conflicts unresolved.

## 4. Detailed edits and acceptance checks

### H1 · Withdraw the Kubernetes staging waiver

**Current.** The addendum's accepted-risk paragraph permits the pre-production support exception “until G1,” immediately before its account of the October 1 decision.

**Replacement.** “The October 1 decision superseded the pre-production Kubernetes support exception. Retire KubeSphere, complete and verify the supported workload Kubernetes upgrade, then qualify private Rancher management and independent native access. Staging may open only after those steps and its other prerequisites pass. A delayed upgrade or Rancher qualification delays staging.” Keep the profile inventory as the evidence of actual version currency.

**Check.** With KubeSphere retired and Kubernetes 1.34 past its October 27 end of life, staging stays closed. With the supported upgrade complete and Rancher qualification outstanding, staging still stays closed. Completion of Story 4.28 satisfies this dependency. G1 has its own evidence requirements. [Addendum](prds/prd-platform-2026-09-27/addendum.md) · [Approved October 1 sequence](sprint-change-proposal-2026-10-01.md)

### H2 · Test both Administrator decisions on the same healthy baseline

**Current.** SM-5 calls for “refusal of approval when production is neither empty nor degraded” and “clearance only by an Administrator record.” The SM-5 summary, Story 9.5 and the spec's SM-5 row repeat parts of this wording.

**Replacement.** “On a healthy working baseline, refuse an attempt invoking the empty/degraded exception. On that same baseline, admit an otherwise eligible pre-G3 release with authenticated Administrator approval for that attempt. Require the exact-release staging E2E pass, provenance, isolation, serialized change and production verification. For an incompatible release, also require FR-8's named recovery, checks, duration and post-lock recovery point.”

**Stop rehearsal.** Each trigger sets the durable stop. An authenticated Administrator clearance record may clear the observed stop after the FR-8 reviews. A verified successful Administrator-approved empty/degraded attempt may also clear only the stop observed for that approval. Approval alone leaves the stop in place. A later stop prevails. Failure removes the candidate workloads, retains data and sets a new stop. The monitor and deputy can never clear it, and the deputy cannot approve a release.

**Check.** Give the two healthy-baseline rehearsals identical passing inputs apart from the approval path. Expect refusal for the exception and acceptance for the ordinary pre-G3 attempt. A missing common gate blocks either path. Mirror the pair and both clearance routes in Story 9.5, the epic SM-5 summary and the spec measure. [PRD FR-6, FR-8, SM-5 and SM-C1](prds/prd-platform-2026-09-27/prd.md) · [Story 9.5](epics.md#story-95-rehearse-the-promotion-stop-and-approval-rules-sm-5-part-2) · [Spec measures](../specs/spec-platform/success-measures.md)

### H3 · Bind CI acceptance to the decision in candidate policy v1

**Current.** The PRD handoff asks Platform and Builds to choose “pull-request head or merge result,” evidence reuse and attachment limits. FR-4 identifies candidate artifacts but omits the published decision rule.

**Replacement.** Link candidate policy v1 from FR-4 and the CI handoff. “For a merge decision, test the committed merge-result SHA for the exact PR-head/base SHA pair and recheck those bindings at the gate. For a release, test the exact committed release revision. Each new merge or release decision requires a fresh qualifying run, including decisions with identical bytes or unchanged modules. Label baseline-only runs. Attachment holds default to 30 minutes from acceptance and have a four-hour maximum.” Assign Builds runner/workflow adoption and Platform tool acceptance and integration qualification. Point the corresponding architecture and spec handoff rows to the same published policy.

**Check.** Refuse head-only evidence, an earlier run after either SHA changes, and a PR run offered for a new release decision at the same SHA. A fresh passing run with matching decision, candidate and loaded artifact identities may qualify once the tool and integration gates pass. An over-limit attachment request is refused. [Candidate evidence and attachment policy v1](../../docs/contracts/candidate-and-attachment-policy-v1.md)

### M1 · Separate published hosted-release rules from outstanding evidence

**Current.** FR-6 asks Platform and Builds to define an evidence-age limit. The handoff groups published rules on freshness, reruns, coverage changes and currency with bounds that still require a decision.

| Proposed replacement in FR-6 and the relevant handoff rows | Remaining owner and check |
| --- | --- |
| Staging evidence expires 24 hours from serialized attempt start. Admission at the deadline fails. A new pending decision or changed checked inputs triggers a full attempt. For an eligible rerun, record the reason, retain every earlier result and use only the latest issued complete passing attempt for the pending decision. | Platform and Builds enforce and qualify the executor, history and admission evaluator. Demonstrate that a newer failed attempt blocks fallback to an earlier pass. |
| Removing or remapping required critical-flow coverage requires recorded approval by the owning module and Platform for the exact before/after inputs. Tenant-lifecycle changes also require Tenants review. | Module owners, Platform and Tenants supply the reviews. Retain the operator's synthetic-tenant provisioning and cleanup evidence. |
| Apply the published infrastructure inventory, per-production-attempt, monthly-drill and weekly checks, zero support lag and 30-day window for applicable published security fixes. Failure blocks automatic promotion. An approved attempt must name every failing component and pass all other preconditions. | Administrator and Platform supply actual inventory, qualification, check results and approvals. A generic acknowledgement or an unnamed failure refuses the attempt. |
| Keep the G1 monitoring bounds in a separate handoff row. Story 6.2 owns the availability-probe stop bound, how failures count while locked, and the two-way admission comparison cadence. Story 4.9 supplies the measured identity-event capture-lag bound. | Platform and Administrator record and qualify the outstanding bounds. No numeric value is inferred from the staging or currency policies. |

Link the applicable policy in each affected PRD row and in the corresponding downstream handoff. Keep the separate artifact, recovery, notification and verification obligations at their applicable gates. [Staging evidence policy v1](../../docs/contracts/staging-evidence-policy-v1.md) · [Shared-infrastructure currency policy v1](../../docs/contracts/shared-infrastructure-currency-policy-v1.md) · [Story 6.2](epics.md#story-62-decide-the-monitor-and-admission-check-bounds)

### M2 · Give Story 2.10 one current execution instruction

**Current.** The execution spec points to November 8 in frontmatter, still requires separate hosted cancellation workflows and a full final-input campaign, and ends with a November 15 statement retaining that scope.

**Replacement.** Point frontmatter, opening instructions, active task list and completion criteria to the November 18 scope. Mark the earlier dated instructions and handoffs as historical. “Run the local reference scenarios with the pinned released tool and record one compact result per scenario. For CI, count Story 2.8 runs 42521615234, 42526256372 and 42528046681 at their original Parties revision 926c1c3f and Builds 4.44.13. Add normal Parties CI success and failure through the shared CiCd workflow, including the always-run job-owned cleanup and leftover report. The local scenarios are independent of CiCd story 21. The current execution spec records the product defects blocking their live rerun. The CI portion depends on Story 2.14 and CiCd story 21. Tool acceptance and pins follow Story 2.14.” Remove dedicated hosted campaign and cancellation redispatch from the active checklist. Scope the PRD execution clarification's fresh-run sentence to new candidate merge or release decisions.

**Check.** A reader can use the current task list without dispatching a dedicated hosted workflow or relabelling an old run. Local scenario results, normal Parties CI evidence and accepted-tool qualification retain distinct completion criteria. [Approved November 18 correction](sprint-change-proposal-2026-11-18.md) · [Story 2.10 criteria](epics.md#story-210-qualify-the-runner-lifecycle-on-the-reference-composition) · [Execution spec](../implementation-artifacts/spec-2-10-qualify-the-runner-lifecycle-on-the-reference-composition.md)

### M3 · Close the completed spec-owner prerequisite

**Current.** The PRD handoff tells spec owners to synchronize acceptance wording before recovery/deployment stories can be finalized.

**Replacement.** “Platform completed spec reconciliation on October 1. Folders, Projects and McpCli documentation owners retain their respective upstream override records. Story authors use the reconciled spec. Implementation and release qualification remain with their named owners at the applicable gates.” Move the completed item out of the list of prerequisites and link the dated reconciliation.

**Check.** A recovery or deployment story can be finalized without requesting the October 1 spec work again. The upstream documentation items do not gate module enrollment. [Spec source-document alignment](../specs/spec-platform/sequencing.md#source-document-alignment)

### L1 · Identify the November amendment and the actual edit date

**Current.** The PRD frontmatter says November 8 and the revision summary points to September, although FR-4 and the execution clarification already incorporate November 18.

**Replacement.** In the introduction, link the November 18 amendment and name its two effects, disposable-runner cleanup evidence and the revised Story 2.10 qualification. Set `updated` to the actual day the approved edits are applied. For an edit on November 19, use `2026-11-19`. Record November 18 as the date of the preceding amendment.

**Check.** The frontmatter and revision summary distinguish the amendment date from the current document revision. [PRD revision history](prds/prd-platform-2026-09-27/prd.md) · [November 18 amendment](sprint-change-proposal-2026-11-18.md)

## 5. Implementation handoff

The owner approved all seven edits on November 19 with “I approve.” The Platform PRD, addendum, architecture handoff, spec sequencing, success measures, epic criteria and Story 2.10 execution spec incorporate the correction. Builds owns runner and workflow enforcement. Platform and the Administrator own their release gates. Story owners use the revised criteria when they qualify those capabilities.

**Verification on November 19.** All seven findings were checked against the revised passages and the governing decisions. All 26 original PRD requirement and measure IDs were preserved. After reconciliation with main, all 113 local Markdown link occurrences across the eight documents resolved, including explicit heading fragments. The PRD and addendum account for 40 of those links. Story headings and sprint status were preserved. Runtime qualification remains with the implementation stories.
