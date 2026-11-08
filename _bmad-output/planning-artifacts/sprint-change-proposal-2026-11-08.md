---
title: 'Bound Story 2.8 execution and separate reference qualification'
created: '2026-11-08'
status: 'approved-applied-to-planning'
scope: 'moderate'
mode: 'batch'
approval_basis: 'User explicitly invoked $bmad-correct-course and instructed: apply needed fixes, after the six-point diagnosis in this conversation.'
runtime_optimizations: 'specified; not implemented or measured by this correction'
---

# Sprint change proposal — Story 2.8 execution

## 1. Issue summary

Story 2.8's CI ownership/cleanup implementation has expanded into a multi-repository release and historical audit program. Repeated broad qualification, moving dependency/main inputs and poorly localized harness failures delay a finite completion decision. The product's cleanup and isolation requirements remain appropriate.

The user asked whether the story was overengineered and requested execution-time improvements, then explicitly directed this skill to apply the needed fixes. Batch execution and application of the previously described correction are authorized by that instruction; no additional approval is requested for these planning changes. This is not approval of a new evidence-reuse policy or a claim that runtime qualification passed.

Evidence inspected:

- [Original 2.8, 2.9 and 2.10 boundaries](epics.md#story-28-run-ci-integration-on-disposable-runners-with-guaranteed-cleanup).
- [Latest stop/save record](../implementation-artifacts/handoff-2-8-stop-save-20261108.md): interim original-baseline diff of 422,978,926 bytes / 5,973,132 lines, with historical source/HTML/CSS/policy reading queues.
- Active 2.8 spec before correction: 703,727 bytes / 1,812 lines. Its complete original bytes are [archived](../implementation-artifacts/evidence/2-8/course-correction-20261108/implementation-history-before-course-correction.md), with an exact [manifest](../implementation-artifacts/evidence/2-8/course-correction-20261108/history-manifest.json).
- Actual historical GitHub job timings, observed read-only on 2026-11-08:

| Parties manual matrix | Successful baseline | Failed qualification |
| --- | --- | --- |
| [42400061797](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42400061797) | 15m 05s | 1m 22s |
| [42408333976](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42408333976) | 17m 47s | 15m 16s |
| [42418257508](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42418257508) | 13m 04s | 12m 42s |

Baseline time totals 45m 56s; qualification totals 29m 20s. These are job durations, not total development time. The failures differed: missing Aspire runtime assets, cleanup-lock/reader-directory qualification, and stdin-stop acknowledgement. Generic failure typing made diagnosis slower. No total speedup percentage is established.

## 2. Impact analysis

**Epic 2:** Retain the epic, all acceptance criteria and all work already completed. Bound 2.8 to CI runner/workflow behavior and its original six edge-case rows. Preserve 2.9 as done. Give 2.10 an implementation spec and mark it ready-for-dev for the complete reference matrix and final accepted-tool enrollment.

**Other epics:** Epic 1's existing pins and accepted behavior remain intact. Epic 3 still requires its own candidate/loaded-artifact evidence and cannot infer acceptance from a baseline diagnostic. Epics 4–10 retain all staging, publication, production, recovery and MVP gates. Epics 11–12 retain their retirement/post-MVP scope. No epic is removed, renumbered or made obsolete; the overall sequence remains intact.

**PRD:** FR-4, FR-5, SM-3, SM-C4 and SM-C5 stay unchanged. Add an execution clarification explaining story boundaries, fixed campaign inputs and fresh acceptance decisions. No MVP scope or user journey changes.

**Architecture:** Add a CI qualification execution convention consistent with AD-4/5/10. No component, topology, API, schema, trust model, production mechanism or architecture diagram changes.

**Policy conflict found during correction:** Published [candidate-and-attachment policy v1](../../docs/contracts/candidate-and-attachment-policy-v1.md#evidence-reuse-and-lineage) prohibits cross-decision result reuse, even for identical bytes. The earlier suggestion to share a successful baseline is therefore limited to original-result inspection and non-accepting diagnostics. Reusing verified setup assets is allowed without treating them as test results. A broader test-result reuse mechanism is outside this correction and requires a versioned policy decision, adoption and requalification.

**UX:** No affected product screen, interaction, accessibility or wireframe was identified; no standalone Platform UX artifact is required for this CI-only correction.

**Technical delivery:** No runtime, CI pipeline, package pin, policy identifier or repository control is changed by this planning application. Concrete controller/preparation/parallel-lane tasks are assigned in 2.10; focused CI proof remains assigned to 2.8. Existing owner-repository required checks continue to govern any later source delivery.

## 3. Recommended approach

**Direct adjustment; moderate scope.** Apply the planning and execution contract now, preserving completed implementation and immutable evidence. Rollback is not selected because it would discard useful repairs without removing verification overhead. MVP reduction is not selected because the requirements remain achievable and justified.

Effort: low for this planning application; medium for the bounded controller/CI optimizations and remaining qualification, depending on observed failures. Risk: low for planning changes; medium for runtime qualification/cancellation. Timeline benefit begins by removing recursive archive reading and unnecessary version chasing. CI time savings require implementation and measurement. The approximately 30-minute saving originally suggested for baseline deduplication is conditional on a future permitted design; it is not an approved v1 result-reuse optimization.

## 4. Detailed changes applied

| Artifact/section | Before | After | Rationale |
| --- | --- | --- | --- |
| Story 2.8, execution scope | Whole reference adoption, successive publications, full manual matrix and original-baseline archive reading accumulate in one execution | Preserve the original frozen intent, tasks and six edge-case rows; explicitly map migration/full-reference work to its owning stories; define focused real CI completion | Finite implementation and proof boundary |
| Story 2.9, execution scope | Completed migration is repeatedly included in 2.8 resumption | Keep `done`; consume existing implementation; investigate demonstrated regressions separately | Preserve successful work |
| Story 2.10, scope/status | Backlog story with no bounded transferred-work handoff | New ready-for-dev spec owns broad reference matrix, final accepted-tool enrollment and optional bounded performance work | Account for every transferred obligation |
| PRD FR-5, clarification | No explicit execution guidance for separating implementation from complete qualification | Link correction; preserve FR/NFR/SM and v1 fresh-run rules | Synchronize product and sprint interpretation |
| Architecture conventions | No explicit campaign/audit execution guidance | Fixed campaign identities, targeted checks, verified setup cache, incremental audit and independent CI lanes | Optimize within existing invariants |
| Active 2.8 spec | 703,727-byte inline append-only execution history with superseded active scopes | Original 10,345-byte prefix unchanged plus compact authoritative current plan; history archived byte-for-byte | Reduce startup context and conflicting instructions |
| 2.8 audit protocol | Reconstruct/read the complete accumulated baseline, reopen historical context queues, and reread after additions | Enumerate complete changed-path inventory once; read active source/config/test/doc deltas; authenticate archives with complete manifests, hashes and original receipts; reopen affected gaps only | Preserve integrity without recursive reading |
| Historical handoffs | Old latest-main and archive-reading instructions can be mistaken for current authority | Clear supersession pointers; original historical bodies preserved | Make resumption deterministic |
| Sprint status | 2.8 in-progress, 2.9 done, 2.10 backlog | 2.8 in-progress, 2.9 done, 2.10 ready-for-dev | Reflect the applied backlog handoff honestly |

The active 2.8 spec explicitly overrides the original rendered Step 03's accumulated-history rereading requirement for this story. Keep the original baseline SHAs and full path coverage; change the audit representation and scope ownership. Installed/global skills and rendered workflow files are untouched. This is the user's authorized renegotiation of execution, not a silent change to frozen acceptance semantics.

## 5. Implementation handoff and success criteria

**Product owner/backlog:** The author applies the story boundaries and status annotations in this correction. No additional agent delegation is required.

**Platform/Builds/Parties developer:** Resume 2.8 from the compact active spec, record one fixed campaign inventory and verify only the remaining original CI rows. Apply the smallest demonstrated blocking fix with a focused regression. Make controller/CI changes in their owning repository through the normal development workflow. Execute 2.10's broad qualification and final tool enrollment separately.

**Acceptance owner:** Preserve real startup/execution cancellation, same-job attachment withdrawal before teardown, partial-start recovery, immutable first outcomes, exact zero leftovers and existing source/publication gates. Never count missing, required-skipped, failed, cancelled or withdrawn work as a pass. Preserve local service restrictions and all original budgets. Read-only inspection of old evidence does not create a new decision's pass.

Planning completion criteria:

- [x] Applied artifact changes and explicit old/new mapping.
- [x] Archived original spec exactly; preserve original prefix, baselines, policy, acceptance text and first outcomes.
- [x] Preserve 2.9 done and 2.8 in-progress; make 2.10 ready-for-dev with transferred tasks.
- [x] Record runtime implementation tasks and measurable timing fields without claiming they are implemented.
- [ ] Runtime developer closes 2.8's remaining real CI proof and review.
- [ ] Runtime developer implements justified CI/controller optimizations and measures their effect.
- [ ] Qualification owner closes 2.10 and final accepted-tool enrollment.

## Checklist and execution log

| Checklist section | Disposition |
| --- | --- |
| 1.1–1.3 Trigger/context/evidence | [x] Story 2.8, repeated long iterations, scope/audit growth and actual run timings identified |
| 2.1–2.5 Epic impact/order | [x] Epic 2 boundaries corrected; all other epics assessed; no epic addition/removal/reordering |
| 3.1 PRD | [x] Execution clarification applied; product acceptance preserved |
| 3.2 Architecture | [x] Execution convention applied within AD-4/5/10 |
| 3.3 UI/UX | [N/A] No affected product UI or flow |
| 3.4 Other artifacts | [x] Active spec, historical entry points, sprint status and transferred-work spec updated |
| 4.1 Direct adjustment | [x] Selected; low planning effort, medium runtime effort/risk |
| 4.2 Rollback | [N/A] Rejected; successful work is preserved |
| 4.3 MVP reduction | [N/A] Rejected; requirements unchanged |
| 4.4 Selected path | [x] Direct adjustment with finite proof and audit scopes |
| 5.1–5.5 Proposal/handoff | [x] Issue, impact, approach, artifact edits, MVP effects and owners recorded |
| 6.1–6.2 Completeness/consistency | [x] Every transferred obligation has an owner; runtime work remains explicitly open |
| 6.3 Approval | [x] User's explicit apply instruction authorizes the discussed correction; immutable v1 policy remains unchanged |
| 6.4 Sprint tracking | [x] Existing keys preserved; only 2.10 advances from backlog to ready-for-dev |
| 6.5 Handoff | [x] Developer and qualification-owner actions and success criteria specified above |

Execution: skill activated with empty prepend/append steps and persistent facts; config resolved for platform/Administrator/English/expert; trigger and authorization taken from the current conversation; batch mode selected to apply the user's instruction. Planning application is complete. Runtime qualification and source optimization are not claimed complete, and no service, publication, dispatch, cancellation or remote mutation is performed by this correction.

Validation: `git diff --check` passed. Direct Python checks verified the complete archive against the original Git blob and SHA-256, the unchanged 10,345-byte prefix/baselines, byte-identical acceptance sections for 2.8/2.9/2.10, preserved historical handoff bodies, and local link targets. PyYAML parsed sprint tracking and confirmed that its only status change is 2.10 from backlog to ready-for-dev. The active spec is 23,440 bytes, a 96.7% reduction in mandatory spec input. Runtime builds and service tests are outside this documentation/execution-contract change and supply no new acceptance evidence here.
