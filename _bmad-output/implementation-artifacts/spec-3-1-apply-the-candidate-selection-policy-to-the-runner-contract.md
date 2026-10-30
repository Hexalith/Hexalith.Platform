---
title: '3.1 Apply the candidate-selection policy to the runner contract'
type: 'feature'
created: '2026-10-30'
status: 'in-review'
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

### Review Findings

Code review 2026-10-30 (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) over Builds `6494979..70ac3de` (excluding `evidence/`) and Platform `docs/contracts/test-environment-v1.md` since `2cbb8a68`. `B/` resolves to `references/Hexalith.Builds/`.

- [x] [Review][Patch] Opt-in candidate jobs break every `@main` `domain-ci.yml` caller (high, live; decision 2026-10-30: remove the candidate wiring from `domain-ci.yml`, keep `candidate-policy.yml` standalone, and push Builds `main`) [B/.github/workflows/domain-ci.yml:255] — fixed in Builds 74b8723 (pushed): restored the pre-story `domain-ci.yml` and added a regression test asserting no elevated permissions; README and Platform test-environment-v1.md updated. Provenance (74), prepare-module-build, publish-containers (152), actionlint and contract mirrors pass. — `candidate-policy` (B/.github/workflows/domain-ci.yml:255-264) and, in 70ac3de, `aspire-tests` (:973-977) request `id-token: write`/`actions: read`/`pull-requests: read`. GitHub checks nested-job permissions statically, before `if:`. Callers granting only `contents: read` (Commons, EventStore, Folders, Tenants, Conversations) fail at startup. Already live: Builds 1884a88 reached `origin/main` at 07:57:56Z, and since then Commons (41023618916) and EventStore (41023329031, 41023424649, 41032682037) end in `startup_failure` ("workflow file issue"). The fix needs an architecture choice: split candidate CI into a separate opt-in reusable workflow, require every caller to grant the scopes, or revert the domain-ci wiring for now.
- [x] [Review][Defer] CI never delivers the trusted gate configuration or token to candidate selection [B/.github/workflows/domain-ci.yml:1261] — deferred: decision 2026-10-30 adopts Builds-owned per-repository enrollment read by the approved workflow at its own `job.workflow_sha`, to be built with live issuer enrollment once the candidate CI wiring leaves `domain-ci.yml`. Finding detail: — `HEXALITH_CANDIDATE_GATE_CONFIG` is never provisioned by the approved workflow; "Recheck candidate acceptance" only checks it is non-empty (B/.github/workflows/domain-ci.yml:1261). The "Run Aspire hook" step (:1169) receives neither the config nor `HEXALITH_CANDIDATE_GITHUB_TOKEN`, so `test --candidate-request` refuses `issuer-not-enrolled`/`authentication-unavailable`. On `ubuntu-latest` the only remaining source is consumer-authored prepare/runner scripts writing `$GITHUB_ENV`. That lets the candidate choose `WorkflowSha`, `CallerWorkflowPath` and `CallerContentIdentity`, contradicting "Trusted workflow coordinates come from independent host configuration". The README also lacks an enrollment example and `ConsumerJobName` derivation. This needs a decision on the trust root, e.g. Builds-owned enrollment at the workflow SHA, or repository/organization `vars` read by the approved workflow.
- [x] [Review][Patch] Release decisions accept a `workflow_dispatch` of any ref (decision 2026-10-30: require the signed/dispatched ref to equal `refs/heads/main` in the issuer and the verifier) [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidateGateClient.cs:185] — neither the issuer (B/Github/governed-provenance/candidate_policy.py:97-101) nor the verifier (B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidateGateClient.cs:185) binds the dispatched ref, a release tag or the existing release authority. The only gate is approval on the consumer's own `candidate-policy` environment, whereas the policy says "existing release authority selects the exact committed release revision". This needs a decision on what qualifies as release authority: a protected `main`/tag ref, binding to the governed `domain-release.yml` run, or environment approval alone.
- [x] [Review][Patch] Successor-run check scans the full PR-run history and refuses in real repositories (high) [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidateGateClient.cs:178] — the unfiltered `runs?event=pull_request` listing with `per_page=100` returns about 1.3 MB per full page (measured on EventStore: 12,991 bytes/run), which exceeds the 1 MiB cap (:496) once a workflow has about 80 PR runs. EventStore has 397. `PagesAsync` also refuses permanently at a total of 1,000 or more (:444). Narrow the query to `created>=<gate run created_at>` with a smaller page size.
- [x] [Review][Patch] `aspire-tests` `always()` survives cancellation for every consumer [B/.github/workflows/domain-ci.yml:972] — GitHub re-evaluates running jobs on cancel, and `always()` keeps them running. Tenants and Conversations (`@main`, `cancel-in-progress: true`, Aspire tests) will keep superseded Aspire runs alive. Use `!cancelled()`.
- [x] [Review][Patch] `validate-result` reports `contract-invalid` instead of the named no-reuse condition (AC2) [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidatePolicyValidator.cs:62] — the v1 schemas constrain `reuseAllowed` to `const false` and the source lists to `maxItems: 0`, so a reuse-requesting result fails the reader first. `no-cross-decision-reuse` and `exact-policy-required` are only reachable from direct unit calls. Check the named policy conditions on the raw documents before mapping reader failure to `contract-invalid`, and add a public-command regression.
- [x] [Review][Patch] Deterministic candidate RunId is reused by repeated invocations in one job attempt [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:2329] — completed cleanup deletes run state (`StateStore.Delete`), so a second `test --candidate-request` passes the state-only duplicate guard and produces a second run under the same producing identity. Related fallbacks act on a RunId this invocation never issued:
  - the cancellation catches at :412 (after `runId = candidate.RunId` at :120) can tear it down;
  - `ownedRunId` can fall back to it (B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1178).

  Refuse when the candidate RunId's diagnostics directory already exists, gate both fallbacks on issued state, and document one candidate invocation per job attempt.
- [x] [Review][Patch] Authentication and binding refusals have no negative tests [B/test/Hexalith.Builds.Module.Tests/CandidatePolicyTests.cs] — no test covers any of these:
  - a foreign signing key, a tampered signature, `alg`/`iss`/`job_workflow_sha`/`environment` mismatches, a stale consumer proof, or payload bytes edited after signing (`issuer-authentication-required`);
  - single-claim consumer-proof or context mismatches (`consumer-authentication-required`, `same-ci-job-required`);
  - caller workflow bytes, path or ref mismatches (`issuer-workflow-mismatch`);
  - duplicate JSON keys.
- [x] [Review][Patch] No public `test --candidate-request` test reaches startup with an accepted candidate [B/test/Hexalith.Builds.Module.Tests/CandidateRunnerTests.cs] — every public candidate test refuses before startup. Dropping `options with { CandidatePolicy = candidate }` (ModuleCommandExecutionService.cs:1155) or the issued state's `CandidatePolicy` (:1185) would not fail any test. Assert the result under `binding.RunId` with the issued bindings.
- [x] [Review][Patch] `fresh-producing-run-required` is untested [B/test/Hexalith.Builds.Module.Tests/CandidateRunnerTests.cs] — pre-write state for the binding's RunId, then assert the refusal, that the owner's state and workspace are untouched, and that the public command reports `HXP301`.
- [x] [Review][Patch] `baseline-only` labelling of runs without a candidate request is not pinned [B/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentDescriptorWriter.cs:126] — add descriptor and partial-startup result assertions. The reader accepts both labels, so a writer reverting to `unavailable` goes unnoticed.
- [x] [Review][Patch] Python issuer payload contract and refusal paths are unpinned [B/Github/governed-provenance/tests/test_candidate_policy.py] — no assertion covers `repository`, `moduleId`, `pr.id` or `pr.mergeResultCommit`, which the C# runtime consumes. Missing refusal cases: no-reviewer environment, unapproved review, reopen after the run, attempt mismatch, incomplete pagination, oversized response. `candidate_workspace.py` is missing its symlink, foreign-origin and idempotency cases.
- [x] [Review][Patch] `ValidatePassingDuration` silently skips a populated deadline without `actualDurationSeconds` [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidatePolicyValidator.cs:339] — the schema leaves it optional, so a passed result with no measured duration bypasses the over-budget check. Require it when the deadline is populated.
- [x] [Review][Patch] `ValidateStartupDeclarations` returns early when only one side is populated [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidatePolicyValidator.cs:287] — a result can introduce tasks the descriptor never declared. Refuse when populated states differ.
- [x] [Review][Patch] Unknown JWKS `kid` or a non-RSA key is reported as `authority-unavailable` [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidateGateClient.cs:413] — `Single(...)` and the missing `n` throw, so a forged token reads as a transient outage. Map both to `issuer-authentication-required`.
- [x] [Review][Patch] A null `merge_commit_sha` produces an unnamed traceback [B/Github/governed-provenance/candidate_policy.py:81] — the issuer fetches `/git/commits/None` and fails with `HTTPError` instead of a named refusal. Raise `pr-binding-mismatch` when it is absent.
- [x] [Review][Patch] `ValidateEnrollment` accepts identity formats that can never match [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidateGateClient.cs:373] — it accepts an uppercase `WorkflowSha` and a bare-hex `CallerContentIdentity` that never equal GitHub's lowercase SHA or `Hash()`'s `sha256:` form, which yields misleading mismatch refusals. Require lowercase hex and `^sha256:[0-9a-f]{64}$`.
- [x] [Review][Patch] README candidate section sits under "License" [B/README.md:1090] — the `###` section is appended after `## License` (:1084) with a double blank line. Move it into the CLI or reusable-workflow documentation.
- [x] [Review][Defer] Non-candidate expected artifacts are not checked against Builds catalog pins [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidatePolicyValidator.cs:237] — deferred: AD-4 dependency identity proof belongs with the Story 3.9 loaded-artifact producers. The final refusal currently prevents acceptance.
- [x] [Review][Defer] A candidate artifact loaded by only one consumer kind still passes [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidatePolicyValidator.cs:237] — deferred (medium, unverified): v1 expected items carry no consumer binding. To settle it, Story 3.9 must define which consumers must load which candidate artifacts.

**Rejected:**

- Opted-in CI is always red, and acceptance cannot be rechecked at merge time: the spec mandates refusal while proof is unavailable, and merging is excluded. Fixing this would edit the spec or policy.
- Self-approval is not prevented: `false`. The policy requires authenticated approval, not a distinct reviewer, and on a sole-maintainer project `prevent_self_review` would make every decision unapprovable.
- The release path lacks a newer-dispatch check: `false`. Each dispatch is its own decision, and attempts are bounded by `run_attempt`.
- Every `HXP301` maps to `UsageOrManifest`, including outages: `low`. The gate is not live, and remapping would add a conditional exit-code mapping.
- `workflow_helpers` is hard-coded and untested: `false`. The directory helper scan already binds both helpers in the real `domain-ci.yml` closure, as the Verification Gap layer checked.
- `assert` in the inline issuer step: `false`. It runs `python3 -` without `-O`, and the reusable job environment is not caller-controlled.
- Mixed `epoch` value types: `false`. Issuer and verifier compare it only as an opaque string.
- The env-mutating candidate test can affect parallel classes: `maybe-false`, low at most. `AttachAdmissionTests` mostly inject explicit contexts.
- The hosted-runner label is hard-coded to `ubuntu-latest`: `low`. A mismatch refuses with a named condition, and the label ships at the same Builds revision as the workflow.
- The three consumer kinds are hard-coded: `false`. The policy names the composed host, McpCli and tests.
- `download-artifact` v4.3.0 vs `upload-artifact` v7.0.2: `low`. There is no demonstrated incompatibility, and changing pins needs governed action approval.
- Style nits: `false`. `.editorconfig` sets `insert_final_newline = false`. Exposing internals to the CLI, the fully qualified names and `[7..]` cause no named harm.
- Malformed review diff and absent evidence: `false`. This was an artifact of how the review diff was built; evidence was excluded on purpose, and Platform 66aafa69 records the pointer bump.
- The request identity is per workflow run, not per PR epoch: `false`. The spec decision makes the latest registered PR workflow run the request.
- "Re-run failed jobs" cannot reuse the issuance: `low`. It fails closed, and a named pre-download check would add a step.
- The clean-checkout check ignores gitignored files: `false`. A disposable checkout only gains ignored files from committed workflow steps and build outputs, and `--ignored` would refuse every package build.
- The result writer does not compare `run.runId`/`attemptId` with the issued ones: `false`. Every writer path takes them from the same issued state, and `ValidateEvidence` refuses a mismatch.
- Sprint status (`review`) vs spec status (`done`): fixing it edits the spec under review. Sprint tracking is synced at review completion.
- An earlier attempt's rejected review blocks an approved rerun: `low`. It fails closed, and the approvals API has no attempt field to filter on.
- A null `HttpClient` in the public constructor: `false`. No caller can pass one, and a loud failure on unreachable input is correct.
- JWKS key rotation and the absence of a JWKS cache: `low`. Validation runs minutes after issuance within the same job.


### Review follow-up implementation and verification (2026-10-30)

All 16 remaining patch findings are implemented. The gate restricts release dispatches to signed/API-observed `main`, narrows successor history to the issued run's creation time with 30-item pages, preserves named authentication/enrollment refusals and canonical enrollment identities, and validates exact caller workflow refs. Public validation names raw no-reuse/exact-policy violations before generic schema refusal, requires measured populated passing deadlines and preserves startup declaration population. Candidate invocations reserve their producing identity through retained diagnostics and only act on state issued by the current invocation. The cancellation predicate, baseline labels, issuer/workspace refusal coverage and CLI documentation placement are corrected.

- Standalone Tooling Debug build and the focused test-project Debug build passed with zero warnings/errors. All 132 candidate tests and 429 contract/provisioning/result/engine/state/lifecycle/declaration regressions passed (561 total; zero failures, errors, skips or not-run cases).
- Provenance Python: 86 passed. Platform contract mirror: 1 passed. Both changed workflows passed actionlint; scoped Builds and Platform whitespace checks passed.
- The exact required full test-project build remains independently blocked by existing EventStoreHost/package API mismatches (7 errors: missing Routing APIs, UseEventStoreHttpHostSecurity, RequireEventStoreChannel and inaccessible EventStoreActorCallbackEndpointMetadata). The focused fallback uses `-p:BuildProjectReferences=false` with already-built unaffected project dependencies; no package/catalog pin or build gate changed.
- Public command tests cover real engine issuance and terminal bindings under the selected RunId, prior state/workspace preservation, diagnostics-only reuse refusal after active-state deletion, and the ready-session issued-state handoff. An internal fixture-only `CompositionEngineOptions.DeclarationOptionsOverride` supplies explicit offline release declarations from the existing package fixture helper. Production defaults remain unenrolled; this adds no public configuration or live acceptance authority.
- Exact commands, named XML, all six matrix-row mappings, source/artifact hashes, initial fixture failures and the full-build blocker are retained in `references/Hexalith.Builds/evidence/3-1/review-followup-verification.json` and adjacent `review-followup-*` files.

The approved intent and `in-progress` tracking status are preserved for parent review. This implementation session performed no Git recording operations. Concurrent workspace activity recorded the implementation and evidence during final verification; the retention manifest fingerprints only the explicit story files and records the observed Builds head. Live issuer enrollment, authentic tool qualification and Story 3.9 loaded-artifact producers remain independent prerequisites; final acceptance still refuses unavailable qualification authentication.
