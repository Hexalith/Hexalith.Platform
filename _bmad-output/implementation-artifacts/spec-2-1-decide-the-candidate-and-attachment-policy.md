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
