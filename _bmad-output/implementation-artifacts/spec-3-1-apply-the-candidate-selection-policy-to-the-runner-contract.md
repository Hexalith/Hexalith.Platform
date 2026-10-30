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

### Resumed follow-up review triage (2026-10-30)

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| F1 | medium | patch: pre-issuance timeout passes null into ReportStartupTimeoutAsync, which then assigns a random producing identity and initializes it with CandidatePolicy. Preserve an unowned timeout rather than fabricate candidate state; add cancellation coverage. |
| F2 | medium | patch: TryReadAsync returns null for malformed existing state; diagnostics are then modified before the strict reader refuses. Check the state entry itself and preserve it with fresh-producing-run-required. |
| F3 | medium | patch: equal not-yet-populated startup deadlines bypass the measured timing checks in ValidateEvidence despite passed/ready evidence. Require populated startup timing for a passed result and test the exact substitution. Final qualification refusal remains mandatory. |
| F4 | medium | patch: the fixture returns at most one successor run, so no test verifies successful page-two traversal or later-page supersession. Shares its root cause with G1. |
| F5 | medium | patch: initial reservation checks precede the outcome lock, and simultaneous invocations have no regression. Verify exclusive producing identity and preservation through concurrent calls, including retained owner metadata after state cleanup. |
| F6 | medium | patch: duplicate matching JWKS IDs and malformed RSA parameters lack authentication refusal regressions. Pin the existing unique-key invariant and name malformed signing-key refusal consistently. |
| F7 | medium | patch: the authentication tests omit expired consumer tokens, future issuance, reversed expiry and the intentional archived-issuance distinction. Add isolated claim cases against the existing authentication contract. |
| F8 | medium | patch: changing only consumer ref also breaks its workflow_ref equality, so the allowed-ref guard is not independently tested. Substitute both claims coherently for PR and release cases. |
| F9 | low | patch: the issuer indexes merge_commit_sha before validating it, so an absent field raises KeyError. The direct optional-lookup correction preserves pr-binding-mismatch without adding policy surface. |
| F10 | low | patch: CreateToken generates an unused RSA key on valid paths; only the foreign-key fixture needs it. Conditionally create that fixture key to remove unnecessary cryptographic work. |
| G1 | medium | patch, pre-verified gap: all workflow-run responses have fewer than 30 entries, so reverting the continuation threshold to 100 survives the suite. Add a valid 30+1 page history and bind its resolved identities; grouped with F4. |

Edge-case review returned no additional findings. Review is scoped to the resumed Story 3.1 correction diff; unrelated concurrent composition and writer changes are preserved. The retained comprehensive Platform diff contains independent work since the historical baseline and is not claimed as Story 3.1 implementation.

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


### Final resumed review verification and remaining gate (2026-10-30)

All follow-up findings F1-F10 and G1 are corrected and verified; F4/G1 share the pagination coverage correction. Candidate issuance is serialized before diagnostic mutation, malformed prior state is preserved, and cancellation/timeout before issuance remains unowned. Passing evidence requires populated startup timing. Full-page successor, signing-key, coherent-ref and temporal-claim regressions pass; missing/null merge results share the named refusal.

- Final candidate selection: 156 passed. Parent contract/provisioning/result/engine/state/lifecycle/declaration selection: 430 passed. All 586 .NET cases have zero failures, errors, skips or not-run cases.
- Provenance Python: 86 passed. Contract mirror: 1 passed. Workflow lint and scoped whitespace checks pass. Tooling and focused test-project Debug builds pass with zero warnings/errors; the focused build uses already-built dependencies.
- The final exact required full Debug build was retried after review corrections and still fails with seven EventStoreHost API/package compatibility errors (Routing, UseEventStoreHttpHostSecurity, RequireEventStoreChannel, callback metadata accessibility and the related unused using). The earlier intermediate 17-error dependency observation remains archived separately. No dependency/catalog pin or host API repair was included.
- Final commands, named XML, six-row matrix audit, source fingerprints, reviewed diff and retained logs are in `references/Hexalith.Builds/evidence/3-1/review2-parent-verification.json`. The frozen approved intent is unchanged.
- A concurrently added diagnostic test received only mechanical analyzer-format corrections so the focused test project could compile; its behavior is preserved. This session performed no Git recording or remote operations.

Status remains `in-review` because the required full-build gate is unresolved. The Build review step requires: "if verification fails and the failure cannot be fixed, HALT and escalate to the human." All authorized candidate-policy corrections are complete; host/package compatibility needs resolution before completion. Live enrollment, authentic tool qualification and Story 3.9 producers remain independent prerequisites.

### Review Findings — follow-up review (2026-10-30)

Code review 2026-10-30 (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) over the Story 3.1 files in Builds `70ac3de..bdd369c`: `74b8723`, `d080758`, and the F1–G1 corrections that a concurrent `/pushall` recorded in `bdd369c` during this review. Excludes `evidence/` and the concurrent Story 2.7 hunks. Also covers Platform `docs/contracts/test-environment-v1.md` since `66aafa69`. `B/` resolves to `references/Hexalith.Builds/`.

- [x] [Review][Defer] The required full Debug build still fails, for reasons outside Story 3.1 — deferred (decision 2026-10-30): accept the focused verification. The blocker belongs to the EventStoreHost security wiring added in `b8d1952`, which needs an EventStore release that provides those APIs, or a pin bump. `origin/main` still pins 3.128.4. Verification requires `dotnet build -c Debug -m:1 -nodeReuse:false` of the test project.
  - It fails with seven EventStoreHost errors: missing `Routing` namespaces, `UseEventStoreHttpHostSecurity`, `RequireEventStoreChannel`, an inaccessible `EventStoreActorCallbackEndpointMetadata`, and an unused using.
  - Builds `b8d1952` (source-mapped platform catalogs, not Story 3.1) introduced these calls. The pinned `Hexalith.EventStore` 3.128.4 does not provide them, and nothing since has touched the host or the pin.
  - All 586 Story 3.1 .NET results come from the focused `-p:BuildProjectReferences=false` build.
  - Choose one: accept the focused verification and defer the blocker to the work that owns it, keep 3.1 blocked, or repair the host here.
- [x] [Review][Patch] A passed result whose execution deadline was never populated skips the measured-duration check [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidatePolicyValidator.cs:145] — fixed: the validator now refuses unless execution is `populated` or `not-declared`. `PassingResultRefusesMatchingUnavailableTimingAsync` now covers both deadlines.
  - The F3 fix refuses only an unpopulated startup deadline. If both documents carry `execution: {"state":"not-yet-populated"}`, `ValidatePassingDuration` checks nothing.
  - Nothing else catches this: the reader has no rule linking a passed outcome to execution timing, and the contract reserves `not-declared` for undeclared budgets.
  - Fix: refuse a passed result whose execution state is neither `populated` nor `not-declared`, with `passing-duration-required`. Add the execution counterpart of `PassingResultRefusesMatchingUnavailableStartupTimingAsync`.
- [x] [Review][Patch] No test covers the command's owned-run fallback for an unissued candidate [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1178] — fixed: added the public-command case `unissued`. Mutation check: with the fallback restored, the command exits 0 with the prior owner's pass and the case fails.
  - Restoring `?? options.CandidatePolicy?.RunId` still passes every test.
  - With that fallback, a refusal before issuance, a cancellation while waiting on the lock, or a setup timeout would read the prior owner's committed `selected-test.json` and report its outcome as this invocation's.
  - Add a command-level case: seed a passing prior record and use a start override that returns no run ID. Assert there is no `runId`, the exit code is this invocation's own, and the prior bytes are unchanged.
- [x] [Review][Patch] AC1's release kind is never written through the writers or the public command [B/test/Hexalith.Builds.Module.Tests/CandidateRunnerTests.cs:123] — fixed: the writer round trip runs for both kinds, and the public command has a `startup-release` case. Both assert the decision kind and the presence or absence of `pr`.
  - Every accepted-startup command case and `DescriptorAndTerminalWritersPreserveSameIssuedCandidateAsync` use the PR-merge payload. Release decisions, which have no `pr` object, are exercised only at `ResolveAsync`.
  - Parameterize the writer round trip and the public `startup` case over both kinds.
- [x] [Review][Patch] The README still says the consumer proof comes "currently" from `domain-ci.yml` [B/README.md:623] — fixed: the README now says no consumer workflow is enrolled yet, and that the opt-in consumer workflow will replace the verifier's `domain-ci.yml` reference.
  - The same section says `domain-ci.yml` has no candidate wiring and requests only `contents: read`, so no shipped workflow can mint the proof.
  - Say that no consumer workflow is enrolled yet, and that the opt-in consumer workflow will replace the verifier's `domain-ci.yml` reference.
- [x] [Review][Patch] The `domain-ci.yml` permission regression test only rejects four strings [B/Github/governed-provenance/tests/test_candidate_policy.py:155] — fixed: the test asserts exactly one top-level `permissions:` block granting only `contents: read`. Checked against in-memory variants: `packages: write`, top-level `write-all`, job-level `write-all` and job-level `checks: write` all fail it.
  - Adding `packages: write`, `checks: write`, `write-all` or any other job-level `permissions:` would bring back the caller `startup_failure` without failing the test.
  - Assert that the file has exactly one `permissions:` block and that it is `contents: read`.
- [x] [Review][Patch] The enrollment-format regression test misses the lowercase-hex rule for `CallerContentIdentity` [B/test/Hexalith.Builds.Module.Tests/CandidateRunnerTests.cs:736] — fixed: added a `sha256:` + uppercase-hex case. It fails when `!char.IsAsciiLetterUpper` is removed.
  - Only the bare 64-hex case runs, which exercises the prefix and length check. A `sha256:` prefix followed by uppercase hex is never tried, so removing `!char.IsAsciiLetterUpper` would go unnoticed.
  - Add that case.
- [x] [Review][Patch] The issuance substitution theory has no `job_workflow_ref` case [B/test/Hexalith.Builds.Module.Tests/CandidatePolicyTests.cs:319] — no change, refuted (`false`) during resolution: `SavedIssuanceNeverOverridesCurrentAuthorityAsync("issuer")` already isolates it. `FabricatedIssuer` signs an otherwise valid token whose only differing claim is `job_workflow_ref`, and the check at `CandidateGateClient.cs:440` is the only use of that claim. Removing it already fails that case.
  - The issuer-side `job_workflow_ref` equality is what separates issuer from consumer, and no isolated case tests it. The consumer side is covered by `wrong-workflow`.
  - Add an `InlineData`.
  - Two related points need no change: `alg: none` goes through the already-tested non-RS256 branch, and the JWK `use`/`alg` fields need no check while `kid`, `kty` and the header `alg` are pinned.
- [x] [Review][Defer] The consumer side is orphaned since the `domain-ci.yml` revert [B/src/libraries/Hexalith.Builds.Tooling/CandidatePolicy/CandidateGateClient.cs:440] — deferred: already recorded with the candidate CI trust-root item, which says the opt-in consumer workflow must replace the hard-coded `/domain-ci.yml@` consumer reference. That workflow must also run `candidate_workspace.py`; until then, its `governed_provenance.py` helper mapping does nothing.
- [x] [Review][Defer] "One candidate invocation per job attempt" is enforced only within one local state directory [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:2393] — deferred: pre-existing, because the producing RunId comes from the signed payload.
  - Matrix legs share `GITHUB_JOB`, and a second state directory on the same runner is never checked, so two invocations could both issue the same producing identity.
  - The opt-in consumer workflow must guarantee a single invocation (no matrix, a fixed state root) or bind the numeric GitHub job.
- [x] [Review][Defer] A setup timeout after candidate issuance may discard the issued RunId (medium, unverified) [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1260] — deferred:
  - `ReportSetupTimeoutAsync` always passes a null run ID, and the command then sets `ownedRunId = expired.RunId`. Before F1, this path invented a random owner instead.
  - To settle it: check whether `startupToken` can be cancelled while a session exists and `ExecutionTiming` is null — for example, batch entry two, after line 1217 resets it.

**Rejected:**

- Issuance-lock waits over 5 s escape as an `OperationCanceledException` and are reported as `HXT007` (Blind Hunter, Verification Gap, Edge Case Hunter): `low`.
  - Verified: `OpenOutcomeLockAsync`'s internal bound matches no `StartAsync` filter.
  - It needs duplicate invocations racing within one job attempt, the run still fails safe, and the fix adds a new catch path.
- Issuance lock files are never deleted and keep the state root from being removed (Blind Hunter, Edge Case Hunter): `low`. They are 0-byte files on disposable runners, and deleting them would reintroduce a lock-file race.
- The concurrency and cancellation tests never prove that the waiter blocked on the issuance lock: `low`.
  - The Concurrent and WaitingDuplicate tests also pass against the pre-F5 code, so they do not prove the fix.
  - The race they target is improbable, and a distinguishing test needs a redesign.
- `main` is hard-coded rather than enrolled: `false`. The 2026-10-30 decision requires `refs/heads/main` in both issuer and verifier, and each dispatch is its own decision on the commit it dispatched.
- Redundant guards, and the `alg` check running after the JWKS fetch: `false`. No harm is named.
- Unexplained page size 30: `false`. `SuccessorLookupStartsAtGateCreationAsync` pins `per_page=30`, and the G1 test covers page continuation.
- The v1 contract was amended in place: `false`.
  - The edited section was added by this story (`66aafa69`) the same day, and it documents runner enforcement, not v1 representation.
  - The policy's "existing release authority" is the owner-approved `main` dispatch.
- The public-command theory has too many cases, and `starts.ShouldBe(0)` proves nothing for accepted startups: `low`. This is readability only, and splitting the theory is a restructure.
- `ValidateRawPolicy` duplicates the reader's parsing: `low`. It runs only after the strict reader has refused, and only to name the refusal.
- Prose line wrapping: `false`. No Markdown line-length tooling exists, and the rendered output is the same.
- The review diff could not be applied as one patch: `false`. That is how this review's diff was built, not a defect in the code.
- Task 4 is still ticked and the Spec Change Log is empty: rejected, because the fix would edit the spec under review.
- Consumer-proof temporal failures are named `issuer-authentication-required`: `low`.
  - The name is stable and the F7 tests assert it on purpose.
  - The consumer path cannot run live yet, and renaming would change the condition contract.
- The production engine now carries `DeclarationOptionsOverride`: `false`. It is an internal init-only property that only fixture tests set.
- A JWT header without `kid`/`alg`, or with missing claims, is reported as `authority-unavailable` (Edge Case Hunter, two findings): `low`.
  - Verified: `S` and `GetProperty` throw `KeyNotFoundException`, which the gate maps to `authority-unavailable`.
  - Only forged tokens reach this, the request still fails closed, and the fix adds a guard.
- An identity reserved after a failure before issuance has no result: `low`. This is the documented one-invocation-per-attempt rule, nothing is invented, and a re-run issues a fresh attempt.
- The prior owner's workspace exists without diagnostics: `false`. Diagnostics are created before the workspace, and selected-test runs keep them (`RefusePreparedRunAsync` always tears down through `FailAndTearDownAsync`).
- A populated deadline without `effectiveSeconds` skips the budget check: `false`. The schema requires `effectiveSeconds` for populated startup and execution deadlines, and the only production caller reads through the strict reader first.
- `DeclarationOptionsOverride` does not reach the writer's closure reload: `low`. This only limits test fixtures, and fixing it means more override plumbing.

### Follow-up review resolution (2026-10-30)

No Story 3.1 code changed after Builds `bdd369c`, so this pass resolved the open follow-up items without another review. The build-blocker decision is now a deferral. Six patches are applied, and one patch item was refuted, with no change made. The fixes are uncommitted on Builds `69701df`.

- The standalone Tooling build and the focused test-project Debug build (`-p:BuildProjectReferences=false`) both finished with 0 warnings and 0 errors.
- Candidate classes: 161 passed. Contract, provisioning, result, engine, state, lifecycle and declaration regressions: 432 passed. Neither run had failures, errors, skips or not-run cases.
- Provenance Python: 86 passed. Contract mirror: 1 passed. Scoped whitespace check: clean.
- Mutation check, run in a scratch worktree: each of the three new regression cases fails against its targeted mutation, and every other case still passes.
- The required full Debug build was not rerun. It is deferred above.
- Commands, XML results, the mutation run and source hashes are in `references/Hexalith.Builds/evidence/3-1/review3-verification.json`.
- Live issuer enrollment, authentic tool qualification and the Story 3.9 loaded-artifact producers are still independent prerequisites. Final acceptance still refuses unavailable qualification authentication.
