---
title: '2.1 Decide the candidate and attachment policy'
type: 'feature'
created: '2026-10-20'
status: 'in-progress'
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

- `_bmad-output/implementation-artifacts/epic-2-context.md`: FR-4 requirements; policy values undecided.
- `README.md`: module-tool documentation and Platform Maintainer ownership; `.github/CODEOWNERS` assigns `@jpiquot`.
- `references/Hexalith.Builds/.github/workflows/domain-ci.yml`: candidate equals workflow SHA; event/caller determines revision.
- `references/Hexalith.Builds/.github/workflows/domain-release.md`: authenticated CI candidate; preserve release authority.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs`: attachment refused; retention/cleanup exist. Reference only.
- `references/Hexalith.Builds/README.md`: future runner-contract adoption location; reference only.

## Tasks & Acceptance

**Execution:**

- [ ] `docs/contracts/candidate-and-attachment-policy-v1.md` — write the approved decisions, rationale, owners, revisit triggers, version rules, evidence/hold rejection examples and downstream adoption requirements.
- [ ] `README.md` — link the canonical policy near module-tool guidance and distinguish policy publication from implemented runner support.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` — synchronize Story 2.1 and Epic 2 as the workflow progresses; leave other statuses intact.

**Acceptance Criteria:**

- Given FR-4 candidate rules, when the record is published, then it selects one candidate revision rule, identifies the gated decision and fixes unchanged-module reuse eligibility and refusal conditions.
- Given attachment rules, when a consumer reads the record, then default/max hold, shorter-request behavior, timing origin, expiry, withdrawal and invalid-request behavior are unambiguous.
- Given either decision, when ownership is inspected, then an accountable owner and concrete revisit condition are recorded.
- Given downstream consumers, when they adopt the contract, then they must bind its policy identifier and required fields before accepting candidate evidence or attachment; documentation makes no current capability claim.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

Cross-check Story 2.1 and AD-10, README links and Markdown whitespace. Inspect missing identity, changed revision/base, disallowed reuse, shorter/max/invalid hold, expiry, withdrawal and prior failure. Documentation requires no new test suite or workload run.
