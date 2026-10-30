---
title: '3.1 Apply the candidate-selection policy to the runner contract'
type: 'feature'
created: '2026-10-30'
status: 'review'
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

## Verification

From Builds, build `test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj` with `dotnet build -c Debug -m:1 -nodeReuse:false`. Run the built xUnit assembly with `-class` selections for new candidate tests, contract reader, provisioning and result writers. Run `python3 -m unittest discover -s Github/governed-provenance/tests` for changed gate helpers. From Platform, run `python3 -m unittest discover -s tests/contracts -p test_test_environment_contract_mirrors.py`. All selected checks must pass; retain exact blockers separately from focused evidence.


### Implemented verification (2026-10-30)

- Required serialized Debug build passed with zero warnings/errors.
- New candidate policy/runner selections: 40 passed, zero failed/skipped/not-run.
- Contract reader, provisioning and result writer selections: 311 passed, zero failed/skipped/not-run.
- Governed-provenance Python suite: 67 passed. Platform contract mirrors: 1 passed.
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
