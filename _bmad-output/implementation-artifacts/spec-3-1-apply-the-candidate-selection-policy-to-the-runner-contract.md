---
title: '3.1 Apply the candidate-selection policy to the runner contract'
type: 'feature'
created: '2026-10-30'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '2cbb8a68d277bdd55e321972b8aa11ef015fc54e'
story_key: '3-1-apply-the-candidate-selection-policy-to-the-runner-contract'
context:
  - 'docs/contracts/candidate-and-attachment-policy-v1.md'
  - 'docs/contracts/test-environment-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Enforce Story 2.1's candidate policy in the Builds runner and its descriptor/result handoff, so consumers cannot select another revision, relabel evidence or choose a superseded passing attempt.

## Boundaries & Constraints

Use the exact published policy identifier and existing v1 representations. Authenticate decision issuance and current state independently of supplied JSON. Bind a PR's committed merge result to its exact repository/module/PR/head/base; release evidence targets its independently authorized exact revision. Recheck current bindings at acceptance. Retries retain the pending decision with distinct runs/attempts; reopened, retargeted or new requests need fresh decisions. The latest issued attempt governs, including pending, failed or cancelled successors.

Allow no cross-decision reuse, including unchanged modules. Preserve producing run/decision and explicitly empty reused-source lineage. Only runner-owned, disposable Linux GitHub-hosted package/Release CI can qualify. Local and baseline-only runs remain diagnostics. Full acceptance still requires authentic applicable tool qualification, matching expected/loaded artifact identities and complete required suites; unavailable proof refuses. Story 3.9 supplies loaded-artifact producers.

Preserve ongoing work, attachment semantics and first outcomes. Exclude publication, deployment, merging, credential provisioning, pin changes and changes to release authority. Do not reinterpret v1 schemas or claim live qualification from fixtures.

**Decision (2026-10-30):** The maintainer selected a GitHub-backed gate and instructed implementation to continue. An approved Builds reusable workflow issues decisions; authenticated GitHub observations establish request state, PR open/reopen epoch, exact merge parents and latest issued attempt. A release workflow dispatch creates a distinct request; retries retain it. Trusted workflow coordinates come from independent host configuration, never the request itself. Missing approval/authentication fails closed. Implement issuance and verification with local HTTP/workflow fixtures; live deployment and tool acceptance remain separate.

## I/O & Edge-Case Matrix

| State | Required behavior |
| --- | --- |
| Authenticated pending PR/release request | Select its exact committed candidate and bind its issued attempt. |
| Changed pair/revision, closed/completed request or fabricated issuer | Refuse with a named policy condition before provisioning/acceptance. |
| Earlier pass with a newer issued attempt | Refuse the older pass even if it finishes later. |
| Foreign lineage or producer mismatch | Refuse reuse; retain original evidence. |
| Local, baseline-only or missing identity/qualification/completion proof | Diagnostic only; refuse candidate acceptance. |
| Descriptor/result disagreement or partial startup | Preserve issued immutable bindings; never invent proof or relabel outcomes. |

</frozen-after-approval>

## Code Map

Paths below prefixed `B/` resolve to `references/Hexalith.Builds/`; `T/` resolves to `B/src/libraries/Hexalith.Builds.Tooling/`.

- `T/TestEnvironment/TestEnvironmentContractReader.cs`: strict offline reader, deliberately grants no candidate acceptance. Existing schemas already reserve every required binding.
- `T/Runtime/ModuleCommandExecutionService.cs`, `CompositionRunPlan.cs`, `ModuleTestRunRecord.cs`: pre-start selection, immutable plan and durable bindings. Qualification credit currently remains false.
- `T/TestEnvironment/TestEnvironmentDescriptorWriter.cs`, `TestResultWriter.cs`: candidate/decision/lineage unavailable; sequence hardcoded to 1, including partial-startup fallback.
- `B/Github/governed-provenance/`: exact workflow/blob provenance; `source_workflow_gates.py` supplies authenticated latest-run/refetch patterns, but no candidate request authority. Existing release signing has no enrolled candidate issuer.

## Tasks & Acceptance

**Execution:**

- [x] `T/CandidatePolicy/CandidateGateClient.cs`, `CandidatePolicyValidator.cs` — implement authenticated GitHub issuance/current-state lookup and separate candidate selection/acceptance checks; keep one type per file and refuse unavailable state. Bind reopened PR epochs and distinct release-dispatch requests, with immutable retries and issuer-ordered attempts.
- [x] `B/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, `T/Runtime/ModuleInvocationCommand.cs`, `ModuleCommandExecutionService.cs`, `CompositionRunPlan.cs` — wire explicit candidate-request input and policy validation before service startup, plus an explicit result-validation command with fresh authority rechecks.
- [x] `T/TestEnvironment/TestEnvironmentDescriptorWriter.cs`, `TestResultWriter.cs` — populate selected candidate/decision/no-reuse bindings and issued sequence; preserve them in terminal and partial-startup snapshots.
- [x] `B/.github/workflows/domain-ci.yml`, `B/.github/workflows/candidate-policy.yml`, `B/Github/governed-provenance/` — integrate reusable gate issuance/verification and trusted workflow transport without granting publication authority. Do not treat unsigned saved observations as authentication.
- [x] `B/test/Hexalith.Builds.Module.Tests/CandidatePolicyTests.cs`, `CandidateRunnerTests.cs` — cover every matrix row, authenticated current-state changes, attempt races and public command/writer integration; preserve reader/provisioning/result regressions.
- [x] `docs/contracts/test-environment-v1.md`, `B/README.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — document enforcement, remaining qualification prerequisites and accurate story progress.

**Acceptance Criteria:**

- Given Story 2.1's policy, when either candidate kind is selected, then the runner records exactly its authorized revision and gated decision.
- Given unchanged-module evidence, when reuse is requested, then the runner rejects it and names the violated no-reuse condition.
- Given a descriptor and result, when written or compared, then policy, candidate, decision, producing attempt and lineage remain consistent.
- Given a consumer using another selection/acceptance rule, when validated, then it fails with a stable named condition.

## Implementation Notes

- Builds baseline: `0b17f8022cdc23b8b242b000f5b2aa15580184ac`. Preserve concurrent work; no Git recording operations are requested.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B1 | high | patch: a copied signed issuance plus forged CI environment passes VerifyExecutionAsync; its only process evidence is caller metadata and API job existence. Reuse existing OIDC verification for a fresh consumer challenge under the enrolled Builds workflow revision; no new public authority or configuration is needed. |
| B2 | medium | patch: candidate state is issued before prerequisite probes, whose direct Failed returns leave no first result; both session-null recovery paths only read a committed result. Route those failures through the existing recording path. |
| B3 | medium | patch: the release pending guard uses the first run response; the final successful completed response is accepted. Apply the same pending-state guard to the final response. |
| B4 | medium | patch: absent/null WorkflowRef and CallerWorkflowPath reach StartsWith after deserialization and escape as NullReferenceException. Validate existing configuration fields before dereference. |
| B5 | medium | patch: ResolveAsync classifies transport cancellation/IO but VerifyExecutionAsync does not; unrequested timeout is reported as cancellation and stream IO may escape. Reuse the same cancellation-versus-authority classification. |
| B6 | medium | patch: expected coverage compares artifactId alone while loaded matching uses module/content; different-module aliases can omit candidate content. Compare the same complete tuple and refuse ambiguous expected entries. |
| B7 | medium | patch: startupTasks is absent from cross-document immutable comparisons and required flags can disappear. Preserve task IDs/requirements while permitting observation progress. |
| B8 | medium | patch: neither immutable-field comparison nor the attached-role check compares original hold acceptance/expiry fields. Preserve prior hold identity/request/acceptance/expiry, allowing valid end progress and later new holds. |
| B9 | false | ValidateEvidence deliberately compares representations; its sole production caller ValidateAcceptanceAsync always refuses tool-qualification-authentication-unavailable. No altered claimed suite can gain candidate acceptance. Authenticated execution/artifact qualification remains an explicit independent prerequisite; matching JSON is never promoted to authority. |
| B10 | medium | patch: workflow runs can truncate at the documented search ceiling; a short terminal page currently grants completeness. Refuse ambiguous ceiling/total-count observations in the existing pagination helper. |
| B11 | medium | patch: the newly added clean check sees workflow-owned .hexalith/builds-execution as untracked when consumers lack an ignore rule. Establish an exact workflow-owned output exclusion without exempting arbitrary candidate inputs or modifying tracked ignore files. |
| B12 | medium | patch: passed-result validation ignores actualDurationSeconds although the reader permits over-budget failed observations. Reject over-budget startup/execution durations for a passed result while preserving failure records. |
| E1 | medium | patch, grouped with B4: edge reviewer reproduced {} enrollment causing unhandled NullReferenceException instead of HXP301 JSON. |
| E2 | medium | patch, grouped with B6: distinct modules sharing artifactId satisfy the incomplete expected coverage test without loading candidate content. |
| E3 | medium | patch, grouped with B3: final release completion is not subjected to the initial pending-state guard. |
| V1 | medium | patch: filed pre-verified gap; the only public candidate test rejects stale authority before the clean/exact Git check. Add dirty and wrong-revision public command cases with zero startup calls. |
| V2 | medium | patch: projection emits only the root workflow although traversal records nested workflows. The isolated fixture changes nested run/environment bytes without changing the reported digest. Include nested workflow blobs through the existing digest-bearing source representation and add the regression. |
| P1 | medium | Parent expanded lifecycle verification exposed one reflection call still using the old ExecutePlatformAsync arity. Added the absent-candidate null argument; the timeout/retention expectations are preserved. Final rerun pending. |

## Verification

From Builds, build `test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj` with `dotnet build -c Debug -m:1 -nodeReuse:false`. Run the built xUnit assembly with `-class` selections for new candidate tests, contract reader, provisioning and result writers. Run `python3 -m unittest discover -s Github/governed-provenance/tests` for changed gate helpers. From Platform, run `python3 -m unittest discover -s tests/contracts -p test_test_environment_contract_mirrors.py`. All selected checks must pass; retain exact blockers separately from focused evidence.


### Implemented verification (2026-10-30)

- Required serialized Debug build passed with zero warnings/errors.
- New candidate policy/runner selections: 50 passed, zero failed/skipped/not-run.
- Contract reader, provisioning and result writer selections: 311 passed, zero failed/skipped/not-run.
- Governed-provenance Python suite: 69 passed. Platform contract mirrors: 1 passed.
- Both changed workflows passed `actionlint`; Builds and scoped Platform `git diff --check` passed.
- Named xUnit XML, exact commands, matrix-to-test mapping and logs are retained in
  `references/Hexalith.Builds/evidence/3-1/verification.json` and adjacent files.

These are local signed HTTP/workflow fixtures and runner regressions. No live
issuer, tool adoption, Platform acceptance or integration qualification is enrolled.
Final acceptance refuses missing or unauthenticated proof, including complete JSON
qualification references without an independent accepted qualification authority;
Story 3.9 still supplies loaded-artifact producers. Publication/deployment, release
authority, tool/catalog pins and first-outcome/attachment semantics are unchanged.
Implementation is ready for independent review.


Production-shape follow-up: PR workflow-run API `head_sha` binds H, while signed
GitHub OIDC `sha`/`ref`/`event_name` bind the executed committed merge result M;
caller workflow bytes are read at signed `workflow_sha`. Hosted Linux verification
uses the approved workflow's authenticated `ubuntu-latest` requested job label and
GitHub Actions runner group, with self-hosted/non-Linux refusals. The affected Debug
build, 50 named candidate tests and 69 provenance tests passed again. Earlier
reader/provisioning/result, mirror and workflow-lint evidence is retained unchanged.


### Review disposition (2026-10-30)

All 17 findings have individual verdicts above. E1/E2/E3 share the B4/B6/B3 root causes. The surviving corrections use existing entry points, policy fields, OIDC trust anchors and provenance representations; they add no public surface and address demonstrated states. They are patches under the workflow criteria. B9 is rejected because the advertised authentication bypass is prevented by the unconditional final qualification refusal. Preserve that refusal and every passing issuance, lineage, writer and GitHub response-shape test.

### Verification artifact retention

The owning Builds repository now admits only Story 3.1 command logs through its ignore rules and preserves evidence bytes through Git attributes, so the checked-in manifest can retain its referenced outputs without line-ending conversion. The first expanded run (497 passed, one reflection arity failure) remains archived as parent-tests-initial; the final rerun follows the call-site correction.

### Final parent verification and completion (2026-10-30)

- The final Debug build has zero warnings/errors. All 498 selected .NET tests passed, including all 86 candidate cases and the extended engine/state/lifecycle regressions; zero failures, errors, skips or not-run cases. The initial reflection failure is fixed without changing its timeout/retention expectations.
- The complete provenance Python suite passed 74 tests; schema mirror and workflow lint passed. Both scoped whitespace checks passed. All six matrix rows resolve to executed passing named XML cases.
- All 13 grouped review corrections and the additional parent call-site correction are applied and verified. One claim was refuted; nothing was deferred. Fresh consumer proof, terminal early failures, metadata immutability and nested closure hashing have explicit regressions.
- Source fingerprints, named XML, commands, retained logs and artifact digests are in Builds evidence/3-1/parent-verification.json, referenced by verification.json. The approved intent fingerprint is unchanged.
- The completion workflow requires scoped local Git commits; these record only this story and preserve unrelated staged work. No push or live enrollment is performed. Live issuer enrollment, authentic tool qualification and Story 3.9 artifact producers remain independent acceptance prerequisites.

Recorded Builds local revision: `70ac3de6328157547096e025140c11144b1cd86f`.
