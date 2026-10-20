---
title: '2.1 Decide the candidate and attachment policy'
type: 'feature'
created: '2026-10-20'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '6ab00b79054a58c72d5c7110ea69140d75d9d1ec'
story_key: '2-1-decide-the-candidate-and-attachment-policy'
context:
  - '_bmad-output/implementation-artifacts/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Publish one versioned Platform/Builds decision record for candidate evidence and attachment holds (Story 2.1, AR-16). Descriptor, runner and evidence consumers must share this policy before implementing attachment or accepting candidate evidence.

## Boundaries & Constraints

Fix candidate revision selection, its gated decision, unchanged-module evidence reuse, default/maximum attachment holds and shorter requests. Name an accountable owner and revisit trigger for each decision. Platform owns policy; Builds owns runner adoption. Use existing ownership without claiming unrecorded Builds approval.

Preserve AD-4/AD-5/AD-10: committed Release artifacts, expected/loaded content identities, catalog-pinned other modules, baseline-only labels, compatible isolated run-owned environments and same-job CI attachment. Withdrawal earns no evidence. Cancellation preserves local failure retention; CI cleans and reports leftovers. Owner finish refuses new attachments except local failure-retained environments.

Define a policy identifier and descriptor/result handoff for Stories 2.2, 2.6, 2.7 and 3.1: candidate, gated decision, reuse lineage, requested/effective hold and absolute expiry. Holds cannot renew indefinitely or survive owner withdrawal/CI job completion. Separate hold/startup/execution deadlines. Policy changes require version adoption and requalification.

Change documentation/tracking only; preserve independent edits. Exclude runner/schema/pin changes, remote publication and workload starts. Integration evidence remains a prerequisite; existing release authority and staging gates remain separate.

## Decisions

Confirmed by the user on 2026-10-20:

- Candidate evidence uses the committed PR merge result and gates merging that exact PR-head/base pair. Changed head/base requires rerunning. A different release revision requires its own exact-revision evidence; existing release gates remain separate.
- Version 1 prohibits evidence reuse across decisions, including unchanged modules. A new decision requires a new run; baseline-only evidence and missing identities never qualify.
- Attachment holds default to 30 minutes (1,800 seconds), with a maximum request of 4 hours (14,400 seconds). Allow shorter positive requests. Reject invalid/over-limit requests and forbid deadline extension. Measure the effective hold from attachment acceptance and record its absolute expiry.

</frozen-after-approval>

## Code Map

- `docs/contracts/candidate-and-attachment-policy-v1.md`: canonical published decisions and adoption handoff; PR merge result, no cross-decision reuse, 1,800-second default/14,400-second maximum holds.
- `_bmad-output/implementation-artifacts/epic-2-context.md`: FR-4 requirements; policy values undecided.
- `README.md`: module-tool documentation and Platform Maintainer ownership; `.github/CODEOWNERS` assigns `@jpiquot`.
- `references/Hexalith.Builds/.github/workflows/domain-ci.yml`: candidate equals workflow SHA; event/caller determines revision.
- `references/Hexalith.Builds/.github/workflows/domain-release.md`: authenticated CI candidate; preserve release authority.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs`: attachment refused; retention/cleanup exist. Reference only.
- `references/Hexalith.Builds/README.md`: future runner-contract adoption location; reference only.

## Tasks & Acceptance

**Execution:**

- [x] `docs/contracts/candidate-and-attachment-policy-v1.md` — write the approved decisions, rationale, owners, revisit triggers, version rules, evidence/hold rejection examples and downstream adoption requirements.
- [x] `README.md` — link the canonical policy near module-tool guidance and distinguish policy publication from implemented runner support.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — synchronize Story 2.1 and Epic 2 as the workflow progresses; leave other statuses intact.

**Acceptance Criteria:**

- Given FR-4 candidate rules, when the record is published, then it selects one candidate revision rule, identifies the gated decision and fixes unchanged-module reuse eligibility and refusal conditions.
- Given attachment rules, when a consumer reads the record, then default/max hold, shorter-request behavior, timing origin, expiry, withdrawal and invalid-request behavior are unambiguous.
- Given either decision, when ownership is inspected, then an accountable owner and concrete revisit condition are recorded.
- Given downstream consumers, when they adopt the contract, then they must bind its policy identifier and required fields before accepting candidate evidence or attachment; documentation makes no current capability claim.

### Review Findings

Code review of commit `8b7a1fe` (2026-10-20): Blind Hunter, Edge Case Hunter, Verification Gap (no gaps) and Acceptance Auditor; 53 raw findings triaged into 15 entries and 24 rejections.

- [x] [Review][Decision] Retry versus new original attachment request — Lines 122–123 require a fresh run-owned environment for continued work after expiry, while lines 124–126 grant a separate original request its own hold on the same environment, guarded only by an unenforceable "MUST NOT disguise a retry" with no rule for recognising a retry that omits its lineage ID. Story 2.6 cannot implement the rule consistently, and while the owner runs a caller can obtain successive holds for the same work. Options: (A) a retry is any request presenting an existing `originalAttachmentRequestId` or a runner-issued attached-run identity of that lineage; any other request is a new original request admitted only under owner-state rules, and no request changes an existing lineage's expiry; (B) after a lineage expires, refuse further attachments to that environment from the same requester, so continued work needs a fresh environment; (C) leave to Story 2.6. Sources: blind-hunter+acceptance-auditor+edge-case-hunter. **Resolved 2026-10-20: option A adopted and applied.**
- [x] [Review][Decision] Which runs may produce candidate evidence — Lines 63–66 exclude only local Debug/source and `baseline-only` runs, so a local package-mode Release run, or one attached to a failure-retained local environment holding the failed run's data, can still satisfy candidate acceptance; FR-4 and AD-5 frame candidate evidence as runner-owned CI evidence. Options: (A) only runner-owned CI runs in package mode on disposable GitHub-hosted runners (owner or same-job attached run) qualify, and every local run is a diagnostic; (B) also allow local package-mode owner runs on fresh environments; (C) leave to Story 3.1. Sources: blind-hunter+edge-case-hunter. **Resolved 2026-10-20: option A adopted and applied.**
- [x] [Review][Decision] Which attempt qualifies when one decision has several runs — Lines 48–49 let retries of a pending decision produce distinct runs/attempts but never say whether the latest attempt governs, any pass qualifies or any failure blocks, so re-running until green is permitted and Story 3.1 consumers may select different runs. Options: (A) the latest completed attempt for the pending decision governs, an earlier pass cannot be chosen over a later failure, and concurrent attempts supersede older ones; (B) any failed attempt blocks the decision until a new decision; (C) leave to Story 3.1. Sources: blind-hunter+edge-case-hunter. **Resolved 2026-10-20: option A adopted and applied.**
- [x] [Review][Patch] Tool qualification gates attachment, deadlocking Stories 2.6 and 2.10 [docs/contracts/candidate-and-attachment-policy-v1.md:12] — Lines 12–13, 198, 243 and README:63 make integration qualification a prerequisite to accepting attachments, but Story 2.10 must exercise attachment scenarios before the tool is Platform-accepted and Story 2.6 must accept compatible attachments; scope the qualification prerequisite to evidence acceptance, while attachment requires adopting this policy identifier and passing its checks. Source: acceptance-auditor.
- [x] [Review][Patch] Define environment withdrawal and keep AD-10's failure-retained attachment exception [docs/contracts/candidate-and-attachment-policy-v1.md:151] — "withdrawn environment" (line 151) and "owner withdrawal" (line 163) are undefined; reading owner cancellation as withdrawal refuses attachment to a local environment retained after an attached failure, which AD-10 and the spec allow. State that withdrawal is owner cancellation, explicit developer stop/cleanup or CI job end; that it ends active attachments; and that a local environment retained after failure stays attachable until explicit stop or cleanup. Owner failure alone does not end attachments (line 129). Sources: blind-hunter+acceptance-auditor+edge-case-hunter.
- [x] [Review][Patch] Pin the hold-request value domain [docs/contracts/candidate-and-attachment-policy-v1.md:105] — AC2 requires unambiguous invalid-request behaviour, but the unit, fractional values and explicit null/empty versus omission are undefined, so runners may accept 0.5 or 1,799.9 seconds or default a null. State whole seconds from 1 to 14,400; only an absent request is omission; null, empty, fractional or otherwise non-integer values are malformed and rejected. Sources: blind-hunter+acceptance-auditor+edge-case-hunter.
- [x] [Review][Patch] Bind every policy consumer story [docs/contracts/candidate-and-attachment-policy-v1.md:20] — Lines 20–21 and 207–214 bind only Stories 2.2, 2.6, 2.7 and 3.1, while Story 2.5 (hold-deadline listings), 2.8 (same-job CI attachment and CI evidence), 2.10 (lifecycle qualification) and 3.9 (candidate evidence) also consume it; the 2026-09-28 sprint change proposal gates 2.8 and 2.10 on this policy. Source: blind-hunter.
- [x] [Review][Patch] State the expiry boundary and deciding clock [docs/contracts/candidate-and-attachment-policy-v1.md:109] — A terminal report at exactly `expiresAt` can be judged pass or withdrawn by different consumers; the sibling `p1-receipt-provider-v1.md` states "Expiry is exclusive". State expiry when runner UTC time reaches `expiresAt`, decided by the runner's record. Sources: blind-hunter+edge-case-hunter.
- [x] [Review][Patch] Capitalise lowercase normative "must" [docs/contracts/candidate-and-attachment-policy-v1.md:126] — Lines 126, 192 and 212 use lowercase "must" although lines 5–6 declare MUST/MUST NOT as the requirement keywords, so live-hold accounting, baseline labelling and Builds adoption read as non-normative. Sources: blind-hunter+edge-case-hunter.
- [x] [Review][Patch] Phrase CI requirements normatively [docs/contracts/candidate-and-attachment-policy-v1.md:181] — "Integration CI stays on … runs isolated tests first and then blocking … integration" reads as a current capability claim (AC4), but Builds `domain-ci.yml` still has the advisory `aspire-continue-on-error` tier and Story 2.8 is backlog. Source: acceptance-auditor.
- [x] [Review][Patch] Keep AD-10's idempotent cleanup [docs/contracts/candidate-and-attachment-policy-v1.md:179] — "Cleanup MUST be retryable" weakens AD-10 and Story 2.5's "idempotent" guarantee. Source: acceptance-auditor.
- [x] [Review][Patch] Align Builds ownership wording [docs/contracts/candidate-and-attachment-policy-v1.md:221] — "Builds owns evidence enforcement" can read as Builds owning evidence acceptance; epic-2-context records "Platform owns contracts and acceptance, Builds owns runner/workflow implementation". Source: acceptance-auditor.
- [x] [Review][Patch] Name who confirmed the decisions [docs/contracts/candidate-and-attachment-policy-v1.md:5] — "confirmed by the user" names no approver in a published record; name the Platform Maintainer, `@jpiquot`. Source: blind-hunter.
- [x] [Review][Patch] Move the README policy paragraph after the restore commands [README.md:58] — The paragraph splits "run the pinned tool through `dotnet tool run`" from "From the Platform root, restore and check the pinned tool with:"; place it after that code block, still within module-tool guidance. Source: blind-hunter.
- [x] [Review][Defer] Planning context does not point downstream stories at the published policy [_bmad-output/implementation-artifacts/epic-2-context.md:40] — deferred: edits planning/agent-context artifacts. epic-2-context says "planning does not already choose these values" with no link to the policy; epics.md Story 2.2's field list lacks `policyId`/decision/lineage/suite/tool bindings, and Stories 3.1/3.9 still carry pre-C-01 wording ("policy from Story 3.1", reuse "under recorded conditions"). Source: blind-hunter.

**Rejected:**

- `false` — Trusted gate has no owner and contradicts "changes no publication authority": line 211 assigns decision binding to Story 3.1; requiring a future gate changes no current authority.
- `false` — No merge recheck mechanism / base advances between recheck and merge: the policy unambiguously gates the exact pair; the mechanism is Story 3.1/Builds work and row 1's revisit trigger covers merge-queue or strategy binding failure.
- `low` — Squash/rebase lands a commit other than M: merge-commit and squash land M's tree when the rechecked base is unchanged; only divergent rebase replay differs, and the fix adds a new requirement.
- `false` — Runner crash leaves holds unenforced: absolute `expiresAt` and the MUST-terminate rule apply regardless of restarts; durability is runner implementation.
- `false` — Refusals need not name reasons: Stories 2.2, 2.6, 3.1 and 3.9 already require named reasons; the policy defines semantics.
- `low` — No content digest, v2 transition or retirement rule: git pins the content, policy changes are rare and the fix adds new mechanism.
- `false` — Accountability table covers only three decisions and leaves the Builds owner unnamed: the rows cover the story's three AR-16 decisions as AC3 requires; naming a Builds person would claim unrecorded ownership.
- `false` — Nothing recorded in Builds: the spec excludes runner, schema, pin and submodule changes; adoption is downstream.
- `low` — README has no contracts index: pre-existing absence, not introduced here.
- `false` — Overlapping hold-table rows: both rows yield 14,400 seconds; the explicit row documents the inclusive maximum.
- `low` — Two revisit triggers are not measurable: process-only; a fix needs invented thresholds.
- `false` — Spec `done` while sprint status is `review`: the build workflow leaves sprint tracking at review until code review completes.
- `false` — Retry with a different hold or after a rejected original: line 121 makes the first accepted expiry immutable; a rejected request has no hold to extend.
- `false` — Acceptance races owner completion or cleanup: the policy fixes the required outcome; serialisation is implementation.
- `low` — Startup override validation or missing execution deadline: restated FR-4 owned by Story 2.4 and unchanged here.
- `false` — Deadline breach outcome unmapped: Story 2.5 maps startup timeout to failure retention, consistent with line 168.
- `false` — GitHub "re-run failed jobs" reuses prior job results: no-reuse is cross-decision; same-decision retries are explicitly allowed.
- `false` — Inconclusive/not-run statuses reconcile as passes: lines 88–91 require every required test to complete and pass, reconciled with individual results.
- `false` — Multi-module or cross-module PRs: each domain module has its own repository; catalog pinning of other modules is AD-4 by design.
- `false` — Fork PR head repository unbound: the exact head SHA identifies content regardless of hosting repository.
- `false` — Concurrent attachments share data: lines 97–98 require data-isolation verification before acceptance; its definition belongs to Stories 2.2/2.6.
- `low` — Attachment requested before readiness: verification precedes acceptance; refuse-versus-wait is UX and the hold starts at acceptance either way.
- `low` — Unbounded cleanup retries: Story 2.5 implementation; the policy already forbids reporting incomplete cleanup as complete.
- `false` — Retained resources need a TTL: AD-10 and Story 2.7 keep retention until explicit developer cleanup.

## Implementation Notes

- Published `hexalith.platform.candidate-and-attachment-policy.v1` with committed merge-result/exact head-base decision binding, no cross-decision reuse, 1,800-second default and 14,400-second maximum immutable holds, downstream descriptor/result handoff, ownership and revisit conditions.
- Linked the canonical policy from module-tool guidance without asserting runner support or Builds approval. Story 2.1 is ready for review; Epic 2 remains in progress. No runner, schema, pin or submodule changes were made by this story.

## Spec Change Log

## Review Triage Log

Three independent review layers completed. Edge-case review returned no findings; verification-gap review found no gaps. Each blind-review finding was checked against the full policy and the downstream stories before classification. The corrections below clarify documentation within the approved decisions; they introduce no runner or schema surface.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| BH1: Release candidate scope | medium | The candidate section unconditionally requires a PR merge result, then requires evidence for a different release revision. A release consumer cannot apply both rules. Patch: scope the PR rule to PR merge decisions and state exact committed release-revision selection under existing release authority. |
| BH2: Decision identity lifecycle | medium | The handoff requires a stable decision identity but supplies no retry/reopen lifecycle, allowing Story 3.1 consumers to interpret a new request as an old decision. Patch: clarify decision authority, immutable bindings, retry producing-run identity and new-request/reopen semantics. |
| BH3: Attachment lineage comparison | medium | The policy forbids replacement run IDs for the same work but defines only “enough attachment lineage.” Story 2.6 cannot compare renewal attempts consistently. Patch: define comparison by runner-issued original attachment-request lineage, preserved across retries and reconnects. |
| BH4: Qualified runner binding | medium | Exact-version Builds adoption is a prerequisite, but the handoff does not bind an executing tool to its qualification. Story 2.2/3.1 consumers cannot verify that prerequisite from a result. Patch: make the existing qualification requirement explicit in the semantic handoff. |
| BH5: Executed suite binding | medium | A passed outcome and loaded artifacts alone do not identify which required suite/configuration completed. Story 3.1 could accept a different or empty test invocation. Patch: require declared/executed suite and configuration identity plus completion/count evidence, preserving existing integration gates. |
| BH6: Withdrawal stops work | medium | Withdrawal currently records an outcome and ends a hold without explicitly stopping the attached worker. Story 2.7 requires the run to end; an ongoing worker could affect remaining users. Patch: state that withdrawal ends attached work/access, preserves other runs and reports stopping leftovers. |

All six findings were corrected in the policy and inspected individually after re-engaging the original implementation agent. Release selection is scoped by decision kind; trusted request identity and runner-issued attachment lineage define comparisons; executing-tool qualification and suite completion are bound; withdrawal explicitly ends owned work and access. No findings were deferred, and the approved frozen decisions remain unchanged.

## Verification

Cross-check Story 2.1 and AD-10, README links and Markdown whitespace. Inspect missing identity, changed revision/base, disallowed reuse, shorter/max/invalid hold, expiry, withdrawal and prior failure. Documentation requires no new test suite or workload run.

- 2026-10-20: Cross-checked the policy against Story 2.1, FR-4 and AD-4/AD-5/AD-10; the acceptance/rejection table covers missing identities, changed head/base, disallowed reuse, shorter/maximum/invalid holds, expiry, withdrawal and prior failure.
- Passed `git diff --check` for the story's tracked documentation/tracking files and checked the new policy's whitespace. All four canonical-policy local links and the added README policy link resolve.
- Confirmed the frozen approved block is byte-for-byte unchanged, only Story 2.1's sprint status changes to review, and Epic 2 remains in progress. Concurrent independent README/code/submodule edits are preserved; their verification is outside this story. No test suite or workload was run.
- Parent verification after review corrections: all four policy links and the README policy link resolve; whitespace checks pass; the approved block matches its pre-implementation SHA-256. All four Story 2.1 acceptance criteria and each corrected review finding were manually checked against the final policy. Spec is done; sprint tracking remains review under the build workflow, with Epic 2 in progress. Runtime adoption and integration qualification remain downstream work.
- 2026-10-20 code review of `8b7a1fe`: D1–D3 resolved with option A (identity-based attachment retries, CI-only candidate evidence, latest attempt governs) and all 14 resulting patches applied to the policy and README; the deferred planning-context pointer is in `deferred-work.md`. Rechecked that the frozen approved decisions are unchanged, all four policy links and the README link resolve, and `git diff --check` passes. Documentation only; no test suite or workload was run.
