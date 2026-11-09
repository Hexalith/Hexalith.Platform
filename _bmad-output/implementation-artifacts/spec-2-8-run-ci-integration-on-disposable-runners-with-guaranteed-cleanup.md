---
title: '2.8 Run CI integration on disposable runners with guaranteed cleanup'
type: 'feature'
created: '2026-10-31'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 2
baseline_commit: '03a099bba258c37a8f16c8b2e842ae842b1ad6d6'
builds_baseline_commit: 'a22b55f0a4f6d2aaa3a5ccc505896064ac595364'
story_key: '2-8-run-ci-integration-on-disposable-runners-with-guaranteed-cle'
context:
  - '{project-root}/docs/contracts/candidate-and-attachment-policy-v1.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.Builds/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** CI lacks runner-owned integration and unconditional cleanup; CI attachments are refused.

**Approach:** Run isolated tests before blocking package integration on disposable Linux GitHub-hosted runners; withdraw same-job attachments before teardown.

## Boundaries & Constraints

**Always:**
- Decision 2026-10-31: full story in stages, including package prerequisites/reference enrollment. Publication approval is separate; no remote implementation operations.
- Isolated tests pass without servers before provisioning. Integration requires declared suites, the exact pinned tool, Release/package dependencies and real services; missing prerequisites fail.
- Admit compatible active CI owners within the exact repository/run/attempt/job. Preserve compatibility, policy and expiry checks; reject completed owners.
- Persist ownership before allocation. After success, failure, cancellation or partial startup, bounded cleanup uses that receipt and an unconditional workflow finalizer.
- Close admission, record/report active attachments as withdrawn, then stop resources. Preserve first outcomes and original expiry. CI never retains failure resources or waits beyond job completion for holds.
- Cleanup is idempotent and ownership-limited. Unknown discovery or owned leftovers prevents completion. Preserve redacted results outside deleted workspaces.
- No staging/production provider credentials; registry access is pull-only. Preserve publication/provenance gates.
- Completion requires reference package CI and cancellation/partial-startup evidence.

**Never:** Change policy/schema semantics or local retention; claim candidate acceptance; implement deferred attached execution; publish or change accepted pins without release decisions.

## I/O & Edge-Case Matrix

| Scenario | Expected behavior |
| --- | --- |
| Isolated failure | No integration provisioning; blocking failure |
| Integration success/failure | Preserve outcome; withdraw active holds, then exact cleanup |
| Cancellation during startup/execution | Recover durable owner identity; withdraw, cleanup, retain diagnostics |
| Same-job/cross-job attachment | Admit compatible active owner / refuse before acceptance |
| Cleanup failure or unknown discovery | Non-success with explicit leftovers; retry permitted |
| Missing tool/suite/package preparation | Fail before acceptance; no legacy advisory fallback |

</frozen-after-approval>

## Code Map

B = `references/Hexalith.Builds`; R = `B/src/libraries/Hexalith.Builds.Tooling/Runtime`; T = `B/test/Hexalith.Builds.Module.Tests`; W = `B/.github/workflows`; E/P = `references/Hexalith.EventStore`/`references/Hexalith.Parties`.

- `R/CompositionAttachAdmission.cs`: retain `CheckContext`; remove blanket CI refusal.
- `R/CompositionEngine.cs`: `CanHoldAttachments`, session down and `DownOwnedCoreAsync` currently assume local attachments. Preserve partial-start cleanup and exact-resource verification.
- `R/CompositionAttachmentStore.cs`: extend `WithdrawAsync` to existing CI end reasons. `R/CompositionRunListing.cs`: restrict failure retention to local owners.
- `R/ModuleCommandExecutionService.cs`: add CI withdrawal without local hold waiting.
- `R/CompositionEngineOptions.cs`, new `R/CompositionCiOwnerReceipt.cs`, `B/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`: add `test --ci-owner-receipt`; persist exact job/owner identities before allocation, refuse an unwritable receipt.
- New `W/domain-ci-v2.yml`: move the staged strict integration contract to this versioned entry point; bind its self-identity and provenance path exactly to v2. Preserve `W/domain-ci.yml` byte-for-byte from merged foundation `57255b502fdd546bb09851d04fc1830175d897f3`. Gate provisioning on isolated results and reuse exact-tool/package preparation.
- New `B/Github/run-module-integration/run_module_integration.py`: execute and finalize via receipt; invoke exact `down --run-id`, validate job binding and propagate failures.
- `src/Hexalith.Platform.Composition/{PlatformNativeTestPreparation,PlatformAppModel,PlatformRoutingCatalogBootstrap,PlatformRoutingCatalog,PlatformTenantBootstrapAccess}.cs` and `Hexalith.Platform.Composition.csproj`: source guards/package refusals block native setup and catalog binding.
- `E/module/hexalith.module-manifest.v2.json`, new `E/tests/Hexalith.EventStore.Platform.IntegrationTests/`, E/P `.config/dotnet-tools.json` and `.github/workflows/ci.yml`: enroll descriptor-only reference integration. P declares `platform-package`, awaiting migrated E testing APIs.
- E/P solutions, P `scripts/render-platform-declaration.py`, B `Props/Directory.Packages.props`: preserve generated declarations and package authority; prepare compatible release inputs.

## Tasks & Acceptance

- [x] Receipt/CLI files above — preallocation recovery.
- [x] Attachment/engine/service/listing files above — job-bound admission and withdrawal before cleanup; preserve local semantics.
- [ ] Helper above — blocking integration/finalization and exact-owner recovery; retain its declared redacted content-addressed diagnostics (reopened after actual hosted omission).
- [x] Versioned workflow above — introduce strict v2 while preserving the legacy entry point. Implement in an isolated worktree from the merged foundation, verify and prepare a normal PR; do not activate callers or publish until exact compatible release inputs and gates pass. Remove unused legacy-only Aspire/performance/Dapr-version input declarations from v2 so GitHub rejects settings that no longer affect a job; the verified catalog owns the integration toolchain. Preserve explicit refusal of retired direct-integration/AppHost execution inputs. Remove obsolete Dapr arguments from E/P only during their later explicit v2 migration.
- [x] Platform composition files above and `tests/Hexalith.Platform.Composition.Tests/{PlatformNativeTestPreparationTests,PlatformAppModelTests}.cs` — enable/test package-native setup and catalog prerequisites.
- [ ] Reference files above — enroll real gateway/persistence and Parties suites; prepare compatible packages and publication handoff, then qualify hosted CI before completion.
- [x] `B/Tools/test-domain-workflow-test-platforms.ps1`, `W/{ci,build-release}.yml`, `B/.github/actionlint.yaml` — retain legacy validator coverage and add a small explicit v2 selection mode, run both modes in CI/release, and reuse only the existing narrow linter compatibility rule.
- [x] `B/Github/governed-provenance/governed_provenance.py` and affected `tests/{test_governed_provenance,test_candidate_policy,test_governed_release_workflow}.py`, `B/Github/prepare-module-build/tests/test_prepare_module_build.py` — add one closed v2-to-integration-helper binding, preserve legacy mapping and authorization, verify exact path/ref/SHA, stale-digest and missing/changed-helper refusals. Do not change the unenrolled C# candidate consumer.
- [ ] E `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs` and P `tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs` — update exact caller contract assertions to v2 when adopting the approved producer revision. Preserve all other owner gates and tests. P caller migration must retain execution of `tests/Hexalith.Parties.Tests`, `tests/Hexalith.Parties.Sample.Tests` and `tests/Hexalith.Parties.Ci.Tests`: move server-free projects into the isolated list, otherwise use declared suites. Removing the retired input must not silently drop these projects; verify actual execution.
- [x] `W/domain-ci-v2.md`, B README and CI/CD standards — document explicit v2 migration and keep the legacy documentation contract. Once the versioned replacement is verified and reviewable, close this story's four superseded unmerged compatibility-pin PRs with an explanation. Existing legacy lanes provide no Story 2.8 acceptance.
- [x] `R/CompositionEngine.cs` and `T/LocalRunLifecycleTests.cs` — diagnose and correct exact-main concurrent startup/down failure `ConcurrentDownCancelsStartingOwnerAsync(declared:true,beforeLaunch:false)`. Reproduce metadata-lock contention deterministically, preserve fail-closed withdrawal before stopping and immutable first outcomes, and verify exact cleanup retry. Correct a runtime defect or a mis-modeled fixture as evidence requires; do not weaken refusal assertions, add blanket CI retries, skip the case or extend production stopping budgets. Finish the separate v2 producer PR preparation first.
- [x] `T/CiRunLifecycleTests.cs`, existing `T/AttachAdmissionTests.cs`, `T/CompositionEngineTests.cs`, `T/ModuleRunnerReviewRegressionTests.cs` — matrix/local regressions, peers, retries and first outcomes.
- [x] New `B/Github/run-module-integration/tests/test_run_module_integration.py`, `B/Tools/test-domain-workflow-test-platforms.ps1`, `W/ci.yml` — executable controls.
- [x] `W/domain-ci.md`, `B/README.md`, `docs/contracts/test-environment-v1.md`, `_bmad-output/implementation-artifacts/evidence/2-8/README.md` — migration and evidence.

**Acceptance Criteria:**
- Given disposable Linux hosted CI, when isolated tests pass, then runner-owned package integration is required and advisory AppHost testing supplies no acceptance.
- Given any CI terminal path, when teardown starts, then active attachments have ended and verified completion requires zero owned leftovers.
- Given an attachment request, when job identity differs, then admission refuses; a compatible same-job request succeeds.
- Given CI permissions and inputs, when inspected, then no hosted provider credential or registry write access is available.

## Approved execution correction — 2026-11-08

The user explicitly invoked `$bmad-correct-course` and instructed **apply needed fixes** after the execution-time diagnosis. The [approved sprint correction](../planning-artifacts/sprint-change-proposal-2026-11-08.md) authorizes this compact execution plan. It supersedes all older ACTIVE/CURRENT handoffs, whole-reference scope additions, latest-main prerequisite chains and recursive archived-source reading queues. Preserve the original 10,345-byte prefix, frozen intent/edge-case matrix, baselines and historical outcomes. The changes below renegotiate execution and task ownership; they do not waive CI cleanup or policy requirements.

The complete previous 703,727-byte spec is [archived byte-for-byte](evidence/2-8/course-correction-20261108/implementation-history-before-course-correction.md), SHA-256 `1714ec845c2c24e66a4fc9fbe2f71c971d20305af9f1743f273942a57971e909`. Its [manifest](evidence/2-8/course-correction-20261108/history-manifest.json) records the original Git source/blob and preserved-prefix hash. Read the active spec and its frontmatter context on resumption; open historical sections only for a relevant unresolved input, proof gap or integrity discrepancy. Do not load the entire archive as implementation context.

### Authority and operating limits

- Continue the original rendered workflow at Step 03, with this story-specific execution amendment. Do not restart it or modify installed/global skills. The complete-path audit below replaces accumulated-history rereading in the rendered Step 03 for this story. Verify the effective 2.8 tasks below and all six original edge-case rows before Step 04; work explicitly assigned to 2.10 does not block the 2.8 task audit. Original transferred tasks remain visible above rather than being falsely checked complete.
- Baselines stay Platform `03a099bba258c37a8f16c8b2e842ae842b1ad6d6` and Builds `a22b55f0a4f6d2aaa3a5ccc505896064ac595364`. Use `git -c core.commitGraph=false` where needed. Preserve newer incoming/user work, all 17 root pins, all closed caches and first failures. No reset, clean, force, amend, fabricated review/identity, protected-main push, manual tag or policy/credential change.
- Only direct root-declared dependencies may be initialized, never nested/recursive ones. Work in the repository that owns a change. Preserve the existing locked commitlint and normal source/publication gates when making actual deliveries.
- Local execution remains limited to resource-free compilation and controlled filesystem/Git/process/transport fixtures, effect-free validation and necessary private MTP IPC. No local real AppHost/DCP/Dapr/Docker/services/listeners or service probes. Real service and cancellation evidence comes from authorized disposable hosted jobs.
- The planning correction launches no runtime campaign. On a later implementation continuation, use one sequential implementation role. Existing normal-delivery authorization remains recorded historically; exact-head review exceptions apply only to their named completed PRs and must not be replayed.
- Published `hexalith.platform.candidate-and-attachment-policy.v1` remains immutable: fresh evidence for every new acceptance decision, no cross-decision test-result reuse, no conversion of baseline/local/cancelled/withdrawn observations into candidate acceptance. Preserve original startup/execution/cleanup/hold budgets and immutable first outcomes.

### Scope and effective task ownership

| Work | Owner after correction | 2.8 treatment |
| --- | --- | --- |
| Durable CI owner receipt, job-bound admission, withdrawal, bounded exact cleanup, redacted diagnostics and strict v2 workflow | 2.8 | Required implementation and focused proof |
| E/P descriptor fixture migration and package/caller assertion migration | 2.9 | Already recorded done; consume the implementation and existing owner receipts; fix only demonstrated regressions |
| Real caller CI execution proving the 2.8 contract, including actual cancellation during startup/execution | 2.8 | Required; isolate these checks from the broad qualification matrix where necessary |
| Full manual matrix, metadata-lock retry/peer preservation, local retention/collision/expiry and full reference scenario table | 2.10 | Explicit transfer; no pass is inferred from transfer |
| Complete reference qualification and final Platform-accepted tool enrollment/pins | 2.10 | Remains open until its own gates pass |
| Candidate loaded-identity/decision acceptance | Epic 3, existing policy consumers | Remains independently gated; baseline lifecycle diagnostics provide no candidate acceptance |
| Newer unrelated releases/enrollment, automatic publisher refinements, archived HTML/CSS and whole historical policy interpretation | Their owning work or history | Not recurring prerequisites to 2.8; reopen only a demonstrated blocker or integrity issue |

The [2.10 implementation spec](spec-2-10-qualify-the-runner-lifecycle-on-the-reference-composition.md) retains every transferred qualification obligation. Story 2.9 stays done. Story 2.8 remains in-progress; this correction marks no runtime acceptance complete.

### Fixed campaign inputs and one-time reconciliation

1. Before the next real campaign, record one finite inventory: exact executor/caller/Platform/Builds source revisions; catalog and executing tool version/content; package/image digests; suite/configuration identities; policy; decision classification; required cases; workflow identity and budgets. Use actual sources and original receipts, not mutable branch labels or guessed next versions.
2. Canonical Platform at this correction is `5af626424e641dce8ed66bff06aed60d834706e3` (resolved from Git). Refresh its actual state before implementation. Preserve incoming Story 3.2 work and current root gitlinks; do not downgrade them to an older publication merely to reuse a checkpoint.
3. The latest historical handoff qualified genuine Builds `3c8096180ecf2aa539ac454815efbfceb8a92822` / 4.44.13 and Platform `0f4e27f1f78310bd731c10c19dc51bb1e0345d44` / 1.7.1. Parties `566b9bfcaff14639003414ee1b5db3e27c7c956a` had main CI native28/isolated15 proof. These are preserved original observations, not a command to replace newer pins or claim new acceptance.
4. EventStore `b0ea2fce1d03ab8b90c44415128e2e60c64cd614` had native9/ordinary proof but its full dedicated integration was cancelled. Incoming `b13271ba1d5ce4553372ee5e9edb91ff4ca7b0ce` adds Story 3.2 work. Keep its own evidence separate. Do not gate a baseline diagnostic against every new EventStore main workflow unless an actual dependency or owner delivery gate requires it. Never relabel the cancelled live suite as passed.
5. Select the required compatible inputs once after this inventory. A dependency update is justified by an actual blocker/security or compatibility requirement; record the changed input and rerun the affected checks. Preserve newer incoming work without automatically moving the qualification target. A changed merge/release decision still requires fresh qualifying evidence under policy v1.
6. Batch functional, declaration, test, documentation and final selection changes before the one final real legacy/current source cut. Preserve genuine ancestry and the existing eight-path metadata boundary. Do not generate new Q/L/M pairs, republish dependencies or restart full lanes for each evidence append. The actual final tested source, required checks and publication remain independently verified where applicable.

### Effective remaining 2.8 tasks

- [x] Verify the strict helper/workflow retains its declared redacted diagnostics and finalizes success, failure and partial startup through the durable exact-job owner receipt. Missing prerequisites must fail before allocation; cleanup failure/unknown discovery must remain non-success.
- [x] Verify actual E/P caller execution uses the completed descriptor migration and preserves required isolated projects and blocking package-mode integration. Relevant existing implementation can be consumed; fresh candidate acceptance still requires its own exact-decision run.
- [x] Build a finite row-to-test/result index for the six original matrix rows. Authenticate existing results for their original subject and record the remaining rows. Every required row must have an actual executed passing covering test; missing, filtered or required-skipped cases remain open.
- [x] Complete focused real hosted proof of success/failure/partial-startup cleanup and same-job attachment withdrawal, plus separate real startup and execution cancellation runs with exact final owned inventories. Retain the ready-artifact-before-exact-cancel protocol, first outcomes and diagnostics. The three prior broad matrices remain failed; a focused result is explicitly labelled and cannot pass the full 2.10 matrix.
- [ ] Complete relevant owner-repository source checks and the required final current/real-legacy/five-SDK checks for the actual final source. Preserve all original controls and explicitly account for meaningful incoming added cases. Complete any genuinely required normal source publication once; do not create unrelated release prerequisites.
- [x] Complete the finite audit below and enter Step 04. Story completion still needs the normal review; full reference qualification/tool acceptance stays with 2.10.

#### Review loop 1 tasks (2026-11-09; see Spec Change Log and Review Triage Log)

The owner chose a **targeted fix with no revert**. Implement these fixes on owner-repository branches: Builds from its current pin `27145e52`, Parties from `926c1c3f`, Platform in the canonical checkout's working tree. The existing remote-operation boundary still applies: no merges, no `main` pushes, no publication, tags or pin changes. B = `references/Hexalith.Builds`, H = `B/Github/run-module-integration/run_module_integration.py`, V2 = `B/.github/workflows/domain-ci-v2.yml`, Q = Parties `scripts/qualify-disposable-ci.py`, L = Parties `.github/workflows/ci-lifecycle-qualification.yml`.

- [x] **BS-1, signal delivery: V2, H, `B/Github/run-module-integration/tests/test_run_module_integration.py`.** Make cancellation reach the tool.
  - Run the helper as the step process (`exec`).
  - Start the tool in its own session or process group, and deliver interrupt and terminate signals to that whole group.
  - Fit the helper's own stop/finalize work inside the hosted runner's cancellation grace: SIGINT to the step process, SIGTERM after 7.5 s, kill after a further 2.5 s. Exact cleanup beyond that window belongs to the always() finalizer.
  - Give the integration step an explicit `timeout-minutes`, and derive the helper timeout below it so the helper's timeout path fires first.
- [x] **BS-1, finalizer: H, V2.** The always() finalizer must never report `not-allocated` or verified cleanup while a process started by this job's integration step may still allocate.
  - The helper records its process group under the job root.
  - Before deciding, the finalizer terminates and waits for any surviving recorded group, then re-reads the receipt. If the receipt now exists, it performs the exact `down`.
  - A live orphan must not be able to hold `provisioning.lock` against the finalizer.
  - Keep the exact-job receipt binding and receipt-first recovery unchanged.
- [x] **BS-1, regression tests:** process-level tests in which only the step shell is signalled, as the runner does. Cover the window before the receipt exists, an orphan holding the lock, and the timeout path.
- [x] **BS-1, hosted proof:** one real hosted cancellation of a v2 caller's integration step that runs the patched helper. Bind it to exact source revisions, and finish with an exact-owner finalizer result and empty owned inventories. Label it focused and baseline-only. It does not replace the Parties lifecycle runs already recorded.
- [x] **BS-2: V2, L.** Before extraction, verify the Dapr CLI and runtime archives (daprd, placement, scheduler) against SHA-256 digests pinned next to the verified catalog versions in Builds-owned data. Missing digests and digest mismatches must refuse before extraction. Use authentic upstream checksum values. EventStore's prior Dapr 1.18.4 checksum repair shows the source of record.
- [x] **BS-3: `B/test/Hexalith.Builds.Module.Tests/LocalRunLifecycleTests.cs`.** Make `ConcurrentDownCancelsStartingOwnerAsync` deterministic. Each data row forces exactly one path and asserts it: contended rows get HXR037 followed by an exact retry; uncontended rows succeed immediately. Keep the existing deterministic HXR037 tests and every refusal assertion.
- [x] **Patches, Builds helper and workflow (PA-1 to PA-9):**
  - PA-1, H: separate a timeout from a user or job cancellation, so a timeout returns 124 and is not finalized as `ci-job-cancelled`.
  - PA-2, H: write each `down` attempt to its own evidence record instead of overwriting `cleanup.json`.
  - PA-3, H: write execution and cleanup evidence, including the exit code and uncertainty, before parsing tool output. Handle non-JSON output and a `down` timeout as unverified cleanup, still retain diagnostics, and keep bounded redacted stderr.
  - PA-4, V2: validate retired or empty inputs in the first step; mark `unit-test-projects` required; skip the finalizer cleanly when the helper was never checked out.
  - PA-5, V2: run the coverage gate before registry login and integration.
  - PA-6, `domain-ci-v2.md` and `B/README.md`:
    - make the migration example complete, with `uses:` and the `secrets:` mapping;
    - call secrets secrets;
    - document that forks and Dependabot have no registry secrets;
    - add `npm ci` and the governed-provenance tests to the Verification block;
    - correct the v2 `governed-ci` description;
    - remove or correct the stale Memories 3.0.23 statement in the README.
  - PA-7, H: classify AttributeError from malformed nested JSON.
  - PA-8, H: make evidence writes atomic (temporary file, fsync, replace).
  - PA-9, `test_run_module_integration.py` and `B/test/Hexalith.Builds.Module.Tests/CiRunLifecycleTests.cs`: pin the cases left open by mutations that survived:
    - a zero exit with no receipt or a non-Success outcome must return 1;
    - `cleanup_verified` must check exit code, runId and outcome;
    - a foreign receipt `stateDirectory` must refuse;
    - the `finalize --job-status cancelled` CLI path must set `HEXALITH_CI_JOB_STATUS`, and `--owners` must be refused;
    - the second `down` must return Success with the same runId.
- [x] **Patches, Builds runtime (PA-10 to PA-14):**
  - PA-10, `ModuleCommandExecutionService.cs`: record `Cancelled` only for real cancellations.
  - PA-11, `CompositionRunSession.cs` and `CompositionEngine.cs`: delete the dead `LocalExecution` property and its false doc comment.
  - PA-12, `ModuleCommandExecutionService.cs`: link the CI withdrawal token to the remaining stop allowance.
  - PA-13, `CompositionEngine.cs` `FailAndTearDownAsync`: when a CI withdrawal fails, return a non-success result. Keep the cancelled status where it applies, include HXR023, and list the observed remaining resources.
  - PA-14: add the missing runtime tests:
    - engine-level `--ci-owner-receipt` wiring, with the receipt written before state and workspace;
    - the receipt's raw JSON keys, checked against what H reads;
    - the `HEXALITH_CI_JOB_STATUS` bridge through `CompositionCommandOptions.Create`;
    - `list` retention for CI owners;
    - a partial startup whose CI withdrawal fails: the AppHost stays alive, HXT008 is reported, no `EndedAtUtc` is written, and a later `down` completes.
- [x] **Patches, Platform and Parties (PA-15 to PA-23):** implemented and verified against the diff on 2026-11-09. The PA-15 test compiles but has not executed; its execution stays open under *Verification*.
  - PA-15, `tests/Hexalith.Platform.Composition.Tests/PlatformAppModelTests.cs`: replace the vacuous binding test with one that calls `Bind` and `Compose`. Assert that exactly one `PlatformNativeTestPreparation` is registered in package mode, with `RoutingCatalogObservationsRequired` true.
  - PA-16, Parties `ci.yml` and L: run `python3 -B -m unittest discover -s tests/ci-lifecycle` in push/PR CI and first in the qualification job.
  - PA-17, Parties `tests/ci-lifecycle/test_qualification_controller.py`: add real `withdrawn()` cases. Refuse `expired`, a null outcome, `endedAt >= expiresAt` and changed lineage; accept `ci-job-completed` and `ci-job-cancelled`.
  - PA-18, Q: install the wait-cancellation signal handlers before constructing `Qualification`.
  - PA-19, Q: require HXR037 or HXR023 for an injected lock refusal.
  - PA-20, L: set `persist-credentials: false`, and scope `DOCKER_CONFIG` to the steps that need it.
  - PA-21, Q: handle `remove-reader` without importing the Builds helper.
  - PA-22, `docs/contracts/test-environment-v1.md`: mark HXR035 `owner-ci-holds-unsupported` as retired; document `HEXALITH_CI_JOB_STATUS` and that failed jobs end holds as `ci-job-completed`.
  - PA-23, L: check explicitly for a missing catalog property.
- [ ] **Verification:**
  - Run the focused Builds xUnit lifecycle classes, the helper and provenance Python suites, and both workflow-validator modes.
  - Run the Parties ci-lifecycle suite and the Platform composition tests that cover PA-15.
  - Every changed behavior needs a test that runs and passes.
  - Re-run the six-row index for the rows these fixes touch.
  - Hand the change set back to Step 04, which reviews only the fix delta.
  - *Status, 2026-11-09:* all done except executing PA-15. Running it needs loopback listeners, which are forbidden locally, so it is a gate for Platform L/M delivery: admit it to `eng/tuple-ci-suites.json` and run it there. The orchestrator independently re-checked the 54 helper tests, both validator modes, 85 Parties ci-lifecycle and 43 integration-script tests, the Dapr digests against GitHub's asset digests, both hosted runs, all 11 checks on PR #177 and the six-row index.

The original four acceptance criteria and six matrix rows above stay unchanged. Actual cancellation, zero leftovers and withdrawal before teardown are essential. This plan changes neither the result of an earlier attempt nor the policy's qualification requirements.

#### Review loop 2 tasks (2026-11-09; see Spec Change Log loop 2 and Review Triage Log iteration 1)

The owner adopted all recommendations: a **targeted fix with no revert**. Continue on the same branches: Builds `fix/ci-v2-cancellation-and-cleanup-27145e52` (PR #177, still draft) from `b411bafb`, Parties `fix/lifecycle-cancel-signal-566b9bfc` from `2849c5b4`, and Platform in the canonical working tree. The remote-operation boundary is unchanged: no merges, no `main` pushes, no publication, tags or pin changes. Abbreviations are as in loop 1, plus S = Parties `.github/workflows/current-owner-startup-diagnostics.yml` and the observer `scripts/observe-current-owner-startup.py`.

- [ ] **BS-4, withdraw before stop under signals: H, V2, Q, plus their tests.** A runner cancellation must never stop a composed resource before the tool has recorded attachment withdrawal.
  - The first interrupt goes only to the tool's own processes: the gate/muxer session leader and the tool process it runs. It must not reach the AppHost, DCP, the native test host or any other composed process. Both H's `ToolSession` and Q's `process_signal` currently `killpg` the whole group, and both must change.
  - Escalation inside the runner grace targets the tool first. Signal the whole recorded session only once the tool has exited, or at the final in-grace step. The runner's own hard kill after its grace is outside our control. After it, the always() finalizer's exact down still withdraws before stopping what remains, so record that limit honestly in `domain-ci-v2.md` and the contract.
  - Finalizer: when a receipt exists, stop only the surviving *tool* processes before deciding. Those are processes that could still allocate or hold `provisioning.lock`. Owned composed processes that the owner state records, such as the AppHost after a refused CI withdrawal (PA-13), must be left for the exact down, which withdraws first. Without a receipt, every surviving recorded-session process is still terminated before any `not-allocated` decision. The BS-1 guarantees stand.
  - Tests:
    - a process-level helper test whose fake tool starts a fake "AppHost" child in its group and records when it is first signalled. The tool records "withdrawn" before stopping it, and the test asserts that no signal reaches the AppHost before the withdrawal mark;
    - the same assertion for Q;
    - a finalizer test in which a receipt exists, the tool has exited and an owned AppHost survives. The exact down runs while the AppHost is still alive, and nothing signals the AppHost first.
  - Hosted proof, two focused runs, each with exactly one audited cancellation request after its ready check:
    - a v2 caller cancelled during *execution* while a native test is live;
    - L's `cancel-execution` scenario, with an active same-job hold, on a proof branch whose Builds submodule points to the patched Builds commit. Proof branches only; this is no pin change on `main`.

    For each run, take three timestamps from durable evidence and require the first two before the third: the owner's admission-closing status write, the attachment withdrawal commit, and the first composed-resource stop. Use, for example, owned-container `docker events` and the AppHost's own signal observation. Bind both runs to exact source revisions and label them focused and baseline-only.

    The 2026-11-08 Parties execution-cancellation run keeps its result and label, but its withdraw-before-stop ordering is now recorded as unproven.
- [ ] **BS-5, timeout budget: V2, H, `B/Tools/test-domain-workflow-test-platforms.ps1`.** The helper's timeout path must fire and finish inside both the step and the job.
  - Derive the helper timeout from the time actually left in the job when the integration step starts. Also keep it below the step `timeout-minutes`. Reserve every bounded wait on the timeout path: the pre-launch `verify_tool`, `TIMEOUT_STOP_SECONDS` plus the post-kill wait, session termination, the finalize-time `verify_tool`, the exact down and the 4-minute finalizer step.
  - If the remaining time cannot hold the minimum helper timeout, refuse before allocation.
  - Keep `build-timeout-minutes`, its default of 45 and every other original budget unchanged (loop-1 KEEP).
  - The validator must compute the budget from the helper's own constants rather than hard-coded numbers.
- [ ] **BS-2b, S Dapr digests and credentials.** Apply the BS-2 digest verification from L, before any extraction, to S's Dapr CLI and runtime archives. Also apply the PA-20 credential scoping to S: no `DOCKER_CONFIG` or registry config exported through `GITHUB_ENV`. Add executed refusal tests in the Parties suite.
- [ ] **Patches (PB-1 to PB-16, from the Review Triage Log, iteration 1):**
  - PB-1, Parties `scripts/verify-public-package-integration.py` and `scripts/verify-current-owner-integration.py`: accept the new helper's integer `timeoutSeconds` in `invocation.json`, and nothing else new. Add tests built on real new-helper output.
  - PB-2, H `exact_down` and `finalize`: a failure to write a cleanup record never prevents the exact down. Run it, then record on a best-effort basis, and keep the result non-success if recording failed.
  - PB-3, helper tests: cover the cancellation escalation with a tool that ignores SIGINT and SIGTERM (patched small constants), and the second-signal SIGTERM.
  - PB-4, ps1 validator: build the `pinned` digest fixture from the real `Props/Directory.Packages.props` Dapr versions.
  - PB-5, helper tests: cover the PID-reuse guard (different `startTime`) and the `bootId` mismatch, so that an unrelated session is never signalled.
  - PB-6, `CiRunLifecycleTests`:
    - observe the `RecordOwnerStatusAsync(Failed)` branch with stop allowance remaining: the owner is `Failed`, and a later attach is refused as `owner-failed`;
    - assert `StoppingTimestamp` on the refused partial-startup result.
  - PB-7, Parties tests: run L's install-step body under `bash -e` with `curl`, `tar` and `dotnet` stubbed, covering verified, CLI mismatch, runtime mismatch and runtime missing, plus a swapped digest variable. Assert there is no `tar` call for a refused archive. Do the same for S.
  - PB-8, Parties tests: a process-level test that S's observer step receives the runner's SIGINT sent only to the step shell.
  - PB-9, Platform `PlatformAppModelTests`: PA-15 must observe the participants and preparation that the composition produced, not recompute them. Execution stays a gate for Platform L/M delivery.
  - PB-10, Q: bind `withdrawal-lock-refusal` to HXR037 and `provisioning-lock-refusal` to HXR023.
  - PB-11, Parties `integration_phases`:
    - refuse mixed layouts;
    - bind the wrapper's and every cleanup record's `executionContext`, and the records' `runId`, to the verified context and owner;
    - require an integer zero `processExitCode`.
  - PB-12, H `stderr_record`: redact before slicing, and match secret keywords joined by `_` or `-`.
  - PB-13, H `exact_down`: decode down output with replacement, and classify a missing tool (OSError) as unverified cleanup, still retaining diagnostics.
  - PB-14, H `exact_down`: run the down in its own session and kill the whole group on timeout, so no orphaned down can hold `provisioning.lock`.
  - PB-15, docs:
    - `domain-ci-v2.md`: Dependabot runs can use Dependabot secrets;
    - L: correct the reader-file comment;
    - `docs/contracts/test-environment-v1.md`: list the exact current HXR035 reason strings.
  - PB-16: covered by BS-2b.
- [ ] **Verification (loop 2):**
  - Run the focused Builds xUnit lifecycle classes, the helper and provenance Python suites, both validator modes, the Parties ci-lifecycle suite and its integration-script tests, and compile the Platform composition tests.
  - Every changed behavior needs an executed passing test.
  - Re-run the six-row index for the touched rows.
  - Hand the loop-2 delta back to Step 04.

### Incremental audit with complete coverage

- Enumerate all changed paths since each original baseline once, including untracked paths relevant to the story. Record active executable source, configuration, tests, active documentation, immutable historical evidence and unrelated incoming work separately. No path is silently omitted.
- Read the full active source/configuration/test/documentation deltas. Map incoming work to its source/owner rather than reimplementing it. Reuse an authenticated prior review receipt for unchanged bytes at the original reviewed identity; review new or changed behavior and relevant context. An archive label cannot exempt active consumed configuration, code or fixtures.
- Verify immutable historical evidence mechanically through complete manifests, hashes and original receipts. Preserve every body and first outcome. If proof is missing, an archive changed, or a consumed input's meaning is unresolved, investigate that affected scope before using it. Otherwise do not reopen the old HTML/CSS/whole-policy queues, rerender historical bodies or read the multi-million-line aggregate diff into model context.
- Retain compact audit output: reviewed active paths/input identities, archive manifest identities, check results, row-to-result mapping and unresolved items. Exclude the generated audit's own content from a recursive obligation to audit that audit; still record its identity and provenance.
- After a change, update the path inventory and review the delta from the last authenticated review. Rerun tests when changed behavior, relevant consumed inputs or a new acceptance decision justify them. Merely rereading an original immutable result never makes it a new candidate result.

### Feedback time and optional implementation optimizations

Use focused process/parser/controller regressions and an early fresh-runner prerequisite smoke check before a full native campaign or producer release. Emit stable safe failure codes/checkpoints and bounded redacted diagnostic fields; never arbitrary exception text, credentials or raw service logs. The original generic ValueError, missing runtime cache and process acknowledgement failures are concrete regression targets.

Performance work is defined in 2.10 and must be implemented/tested in its owning repository. Verified setup/download caching and independent current/legacy CI lanes are candidates; the five-SDK matrix already runs in parallel. Preserve existing required check contexts, tests and the fail-closed final verdict. Test-result baseline sharing across acceptance decisions is excluded by policy v1 and is not enabled here. Optimization work is not a prerequisite if the existing workflow can correctly finish the required proof.

Record setup/build/test/startup/cleanup/audit durations separately for the next campaign. The earlier three runs spent 45m56s in baseline jobs and 29m20s in qualification; these are historical measurements, not an approved shortcut or a promised speedup.

### Remote-operation authorization — 2026-11-08 resumption

The user chose **hosted proof, no merges** for this Step 03 run. Permitted: workflow dispatches, one exact normal cancellation per cancellation run (after the ready-artifact check), branch pushes and PRs needed for focused hosted proof and owner-repository source checks. Not permitted until Step 04 review finishes: merges or pushes to any module or Platform `main` (they trigger releases), source publication, tags, or pin changes on `main`. Stop at that boundary and report rather than merging.

### Execution record — 2026-11-08 focused hosted proof

Executed under the remote-operation authorization above: four Parties `ci-lifecycle-qualification.yml` dispatches, exactly one audited cancellation request in each cancellation run, and two Parties branch pushes. There were no merges, `main` pushes, PRs, tags, publications or pin changes. Records live in [focused-hosted-proof-20261108](evidence/2-8/focused-hosted-proof-20261108/) and are summarized in the [evidence README](evidence/2-8/README.md). Every result is `baseline-only`: it is not candidate evidence, not Platform tool acceptance and not a Story 2.10 full-matrix pass.

- [Fixed inventory](evidence/2-8/focused-hosted-proof-20261108/campaign-inventory.json), recorded before the first dispatch. Caller Parties `566b9bfc`, executor Builds `3c809618`/v4.44.13 (runner sources byte-identical to the current pin 4.45.0), tool `036d32fb…`, catalog `c66fb47d…`, policy v1 and unchanged budgets. [One recorded input change](evidence/2-8/focused-hosted-proof-20261108/campaign-input-change-1.json) applies to the cancellation scenarios only.
- Matrix [42521615234](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42521615234) on `566b9bfc` passed every step: passing, failing, retry-with-peer and partial startup. Cross-job and completed-owner requests were refused with HXR033. Same-job holds were admitted, and their withdrawal was recorded as `ci-job-completed` or `ci-job-cancelled` before the original expiry. Real lock refusals (HXR037/HXR023) were non-success and left resources in place; the hold was withdrawn while 13 containers were live, and the exact retry then succeeded. After a partial-startup SIGKILL, the owner kept its `cancelled` first outcome. All five owners ended with empty inventories and retained their declared redacted content-addressed diagnostics.
- Startup cancellation [42524190110](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42524190110) on `566b9bfc` **failed and stays retained**. It cleaned its exact owner (19 resources, empty inventories), but the runner's step signal never reached the controller behind `bash -e`. The fix is Parties `926c1c3f` (`exec` for the wait step, plus a process-level regression that fails before the fix), on branch `fix/lifecycle-cancel-signal-566b9bfc`, unmerged.
- Startup cancellation [42526256372](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42526256372) on `926c1c3f` passed. The ready age at the request was 2.47 s. The signal-time snapshot showed `Starting` with no outcome, and the first outcome was `cancelled`/HXC130 after the signal. The finalizer and the exact retry both ended with empty inventories.
- Execution cancellation [42528046681](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42528046681) on `926c1c3f` passed. The ready age at the request was 3.70 s. At signal time the owner was `Ready`, native execution was live and the 240 s hold was active. The first outcome was `cancelled`, and the hold was withdrawn as `ci-job-cancelled` before its expiry. Cleanup completed for 22 resources with no leftovers.
- Caller proof: on Parties `566b9bfc` and `926c1c3f`, every baseline ran 15 isolated projects (6,393 passed, 14 pre-existing NotExecuted), including Parties.Tests, Sample.Tests and Ci.Tests, plus native 28/28. EventStore `b0ea2fce` main CI 42492835374 passed native 9/9 with retained diagnostics and empty inventories. Two external main runs failed HXW011 at prerequisite preparation and finalized `not-allocated`: Parties 42516570737 and EventStore 42519687843.
- Local resource-free checks at Builds `27145e52` all passed: 329/329 xUnit cases in the six lifecycle classes, 181 Python tests with 546 subtests, and the workflow validator (legacy 411, v2 254 assertions). The [six-row index](evidence/2-8/focused-hosted-proof-20261108/row-index.json) marks every frozen matrix row covered by executed passing tests. The [path audit](evidence/2-8/focused-hosted-proof-20261108/path-audit.json) covers every changed path since both baselines. It reuses the parent350 receipt for unchanged blobs and maps the 29 newer active paths to incoming Story 3.2/Builds 4.45.0 work. Story 2.8 composition guards are byte-unchanged, and 926 of 931 evidence manifest bindings match; the five exceptions are the living README.
- Remaining-task status. Platform main `7fbe852f` passed tuple CI 42520045812 (current 412/412, legacy 349/349 against `d4c4b469`) and all five SDK lanes in 42520045879; Builds CI passed for `3c809618` and `27145e52`. Two items stay open until Step 04 review permits delivery. First, a normal Parties PR for `926c1c3f`; Parties main `42b76cd6` currently fails its own CI (HXW011) after an external `/pushall` moved its Platform gitlink to untagged `e39a9a25`. Second, Platform delivery of this evidence through the L/M protocol, with its fresh tuple checks. The finite audit is complete and Step 04 entry belongs to the workflow.

### Execution record — 2026-11-09 review loop 1

This run executed the *Review loop 1 tasks* under the remote-operation authorization above. Remote actions were limited to:

- fast-forward pushes to Builds draft PR #177;
- one Parties fix-branch push;
- two proof-branch pushes, each with exactly one audited cancellation request, and each proof branch deleted after its run.

There were no merges, `main` pushes, tags, publications or pin changes. Every result is `baseline-only`. The records are in [review-loop-1-20261109](evidence/2-8/review-loop-1-20261109/) and are summarized in the [evidence README](evidence/2-8/README.md).

- **Builds** `27145e52` → `a51f9876` (fixes) → `4caa1e02` and `b411bafb` (behavior-preserving Codacy refactor and docstrings). Everything is on `fix/ci-v2-cancellation-and-cleanup-27145e52`, PR #177, which is a draft. Every PR check passes at `b411bafb`, Codacy included.
- **Parties** `926c1c3f` → `2849c5b4` (PA-16 to PA-21, PA-23, and the L half of BS-2), on `fix/lifecycle-cancel-signal-566b9bfc`. Its evidence readers now accept both the old and the new helper layouts.
- **Platform working tree:** the PA-15 test and the PA-22 contract text.
- **BS-1 hosted proof:** Parties runs 42539638836 (Builds `a51f9876`) and 42542502127 (`b411bafb`). In each:
  - the step-only runner signal reached the tool session, and the tool recorded `cancelled`/HXC130;
  - the helper returned 130 without a down;
  - the `always()` finalizer's exact down, run with job status `cancelled`, verified empty inventories;
  - the declared diagnostics were retained, and Dapr archives were verified before extraction.

  Both requests landed during startup, before any owned resource existed. The owned-resource cancellation runs of 2026-11-08 still cover that path.
- **Local checks.** Resource-free checks passed at `a51f9876` and again after the refactor:
  - 350/350 cases in the seven focused xUnit classes;
  - the whole assembly, 2,912 passed with 5 pre-existing skips, at `a51f9876`;
  - 221 Python tests with 607 subtests;
  - the validator, legacy 411 and v2 285 assertions;
  - Parties ci-lifecycle 85/85 and integration scripts 43/43 with both helpers;
  - the split tests, 5 deterministic repeats;
  - red controls that fail against the previous sources.

  The loop-1 row index marks every touched row covered. The loop-1 path audit lists the fix delta for Step 04.
- **Still open:**
  - **PA-15 has not executed.** A package composition needs loopback listeners, which are forbidden locally, and the hash-pinned `eng/tuple-ci-suites.json` does not select it. It must be admitted and run in Platform L/M delivery, so the PA-15 to PA-23 group and *Verification* stay unticked.
  - **L refuses until Parties adopts a Builds release that carries `Props/dapr-toolchain-sha256.json`.** This is fail-closed by design.
  - **Task 134 and normal PR delivery** of Builds, Parties and Platform wait for Step 04. Parties `main` is still red from external commits: `f2570a7a` (CI 42532583143) now fails HXW010 because its tool, 4.44.13, is below the Platform range `[4.45.0, 5.0.0-0)`. A `/pushall` must not merge either fix branch.

### Resume entry point

Read this active spec plus its frontmatter contexts, inspect the actual workspace and required original receipts, then perform the one-time input inventory. Do not restart old monitors, replay completed merges/cancellations, assume a run still active or resume a stale archive-reading queue. No runtime process or remote campaign was started by this course correction. The historical handoff is supporting evidence, not the current task list.

## Spec Change Log

### Loop 1 — 2026-11-09 (Step 04 review → targeted loopback)

- **Triggering findings:** three bad_spec groups and 23 patch groups in the Review Triage Log.
  - BS-1 (high; BH2-C3, ECH2-X3, ECH2-X2, BH2-C2, ECH2-X6): the v2 helper runs as a non-`exec` child of the step's bash. The hosted runner signals only the step process, so the helper and tool never see cancellation, and the helper's timeout exceeds the job timeout. As a result the always() finalizer can report `not-allocated` success, or leave cleanup pending, while an orphaned helper or tool still allocates or holds `provisioning.lock`.
  - BS-2 (medium): Dapr archives are extracted without digest verification.
  - BS-3 (medium): the contention test accepts either path.
- **Amended:** the "Review loop 1 tasks" block under *Effective remaining 2.8 tasks*. The frozen intent, the I/O matrix, the original tasks, the baselines and all historical outcomes are unchanged.
- **Owner decision:** targeted fix with no revert. The literal bad_spec rule would revert code that is already merged and released across Builds, Platform and Parties, so it was not applied. The existing no-merge remote boundary still holds.
- **Known-bad states to avoid:**
  - a helper that is not the step process, or a tool outside a signalled process group;
  - in-process stop budgets longer than the runner's grace period;
  - a helper timeout above the step or job timeout;
  - a finalizer that decides `not-allocated` or verified while a job-started process may still be alive;
  - in-place, truncate-then-write evidence;
  - extracting toolchain archives before digest verification;
  - tests that pass on either of two outcomes.
- **KEEP:**
  - receipt-first exact-job recovery, and the receipt's repository/run/attempt/job binding (X7 is by design);
  - fail-closed withdrawal before any resource stops;
  - immutable first outcomes;
  - redacted, content-addressed diagnostics retention;
  - the Parties `exec` wait-step fix `926c1c3f` and its process-level regression;
  - the ready-artifact-before-exact-cancel protocol;
  - every recorded hosted result, its label and its failed status where it failed;
  - the local no-services restriction;
  - every helper attribute that Q uses. If the helper's surface changes, update Q in the same Parties branch and prove it with the Parties ci-lifecycle suite;
  - the legacy `domain-ci.yml` contract;
  - all original budgets other than the explicitly derived integration-step timeout.

### Loop 2 — 2026-11-09 (iteration-1 fix-delta review → targeted loopback)

- **Triggering findings:** three bad_spec groups and 16 patch groups in the Review Triage Log, iteration 1.
  - BS-4 (high; ECHB-14, ECHB-3): BS-1 told the helper to "deliver interrupt and terminate signals to that whole group". That group also holds the AppHost, DCP and the native test host. The AppHost uses the default host lifetime, so it stops on SIGINT, and composed resources can stop before the tool records withdrawal. Q's `process_signal` does the same. The BS-1 finalizer also terminates an owned surviving AppHost before the exact down can withdraw.
  - BS-5 (medium; BHB-1, ECHB-2, BHB-2, ECHB-1, VGB-2): the derived helper timeout ignored the job timeout and several bounded waits.
  - BS-2b (medium; BHP-2): BS-2 and PA-20 named only V2 and L, not the sibling startup-diagnostics workflow S.
- **Amended:** added the "Review loop 2 tasks" block. Loop-1 BS-1's whole-group wording is superseded by BS-4 for the first interrupt and for finalizer termination of owned composed processes. Everything else in BS-1 stands. The frozen intent, the I/O matrix, earlier tasks, baselines and every historical outcome are unchanged, except that one ordering claim is relabelled unproven (below).
- **Owner decision:** adopt all recommendations. That means a targeted fix with no revert, a tool-only first interrupt for both H and Q, finalizer exact down before owned composed processes are terminated, the BS-5 and BS-2b fixes, PB-1 to PB-16, and focused hosted execution-cancellation proofs. The no-merge remote boundary still applies.
- **Known-bad states to avoid:**
  - any signal reaching a composed process (AppHost, DCP, test host, sidecar) before the tool has recorded withdrawal, except the runner's own hard kill after its grace;
  - a finalizer that terminates owned composed processes of a receipt-holding owner before its exact down;
  - a helper timeout that cannot finish inside the job's remaining time;
  - raising or changing `build-timeout-minutes` or any other original budget;
  - digest-free Dapr extraction or `GITHUB_ENV` credential export in any 2.8 workflow;
  - helper output that the Parties verifiers refuse;
  - a cleanup-record write that can prevent an exact down.
- **KEEP:**
  - everything in loop 1's KEEP list;
  - the gate that releases the tool only after its durable process record;
  - the per-attempt `cleanup-<n>.json` records and atomic evidence writes;
  - classifying timeouts separately from cancellations;
  - pinned digests in `Props/dapr-toolchain-sha256.json`;
  - the early input validation and coverage-gate ordering;
  - the deterministic BS-3 rows;
  - the PA-10 to PA-14 runtime fixes;
  - Parties PA-16 to PA-23;
  - the two loop-1 hosted v2 proofs, with their labels;
  - the 2026-11-08 Parties runs, with their results and labels. Their withdraw-before-stop ordering is now recorded as unproven, because Q signalled the whole group.

## Review Triage Log

Step 04, iteration 0, 2026-11-09. Scope: the Story 2.8 active code since the original baselines, in three diffs. Area 1 is Builds runtime/CLI/tests (238 KB), Area 2 is Builds CI workflows/helper/provenance/docs (267 KB), and Area 3 is Platform composition plus the Parties lifecycle controller through `926c1c3f` (362 KB). Historical evidence and unrelated incoming work were audited mechanically under the approved correction, not reviewed. Layers: blind hunter (BH), edge-case hunter (ECH) and verification gap (VG), one of each per area. Claims were verified read-only against Builds `27145e52`, Platform `7fbe852f` and Parties `926c1c3f`.

Attribution: Story 2.8 code is Builds `57255b50`, `d8d0d70f` and `f6834a33`, Platform `68b785b6`, and the Parties lifecycle controller commits `9270f5d9`…`926c1c3f`. Other origins are other or incoming work and route to defer or reject.

Route groups:
- **BS-1:** v2 in-process cancellation and timeout handling never reaches the tool.
- **BS-2:** Dapr toolchain downloads are not digest-verified.
- **BS-3:** the contention test accepts either path.
- **PA-n:** patches.
- **DF:** defer.
- **RJ:** reject.

| ID | Claim | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| BH2-C3 | v2 integration step lacks `exec`; helper never receives cancel | high | BS-1 | The runner sends SIGINT only to the step bash, then SIGTERM and kill after about 10 s (actions/runner ProcessInvoker), and bash does not forward the signal. Parties job 128867933643 shows this. If the cancel lands before the receipt, the finalizer reports `not-allocated` (exit 0) while the orphan can still allocate. If the orphan holds `provisioning.lock`, `down` gives up after 5 s. |
| ECH2-X3 | Same as C3 | high | BS-1 | Same evidence as C3. |
| ECH2-X2 | SIGINT/kill reach only the `dotnet` muxer | medium | BS-1 | No process group. The muxer's ProcessReaper cancels SIGINT and forwards only SIGTERM, so a kill orphans the tool. |
| BH2-C2 | Helper 3600 s timeout exceeds the job timeout | medium | BS-1 | There is no step `timeout-minutes` and the default `build-timeout-minutes` is 45. The job kill then takes the C3 path. |
| ECH2-X6 | Same as C2 | medium | BS-1 | Same evidence as C2. |
| BH2-C10 | v2 Dapr CLI/runtime downloads unverified | medium | BS-2 | `domain-ci-v2.yml:628-633` runs curl and tar with no digest check. Aspire.Cli is NuGet-signature-verified. |
| ECH2-X12 | Same as C10 | medium | BS-2 | Same evidence as C10. |
| BH3-P20 | Parties lifecycle Dapr downloads unverified | medium | BS-2 | `ci-lifecycle-qualification.yml:112-116`, origin `9270f5d9`. |
| ECH3-Y9 | Same as P20 | medium | BS-2 | Same evidence as P20. |
| BH1-R15 | `ConcurrentDownCancelsStartingOwnerAsync` accepts success or HXR037+retry | medium | BS-3 | `LocalRunLifecycleTests.cs:910-918`, origin `f6834a33`. The spec required deterministic contention without weakening refusal assertions. |
| ECH1-E15 | Same as R15 | medium | BS-3 | Same evidence as R15. |
| BH2-C1 | Timeout is finalized as a cancellation (exit 130, `ci-job-cancelled`) | medium | PA-1 | `stop()` sets `cancelled`, and `stop_timed_out_child` calls it (`run_module_integration.py:469,484,509,513`). Confirmed by a scratch simulation. |
| ECH2-X1 | Same as C1 | medium | PA-1 | Same evidence as C1. |
| BH2-C4 | Second `down` overwrites the first `cleanup.json` | low | PA-2 | `write_evidence` truncates (`:206-213`). The docs' "no overwrite" covers only diagnostics. |
| BH2-C5 | Evidence lost on non-JSON output or a `down` timeout; stderr dropped | medium | PA-3 | `finalize` parses before writing or retaining (`:420-433`). A non-JSON `test` loses only `execution.json`. |
| ECH2-X4 | Same as C5 (`down` path) | medium | PA-3 | Same evidence as C5. |
| ECH2-X5 | Same as C5 (`child` path) | medium | PA-3 | Same evidence as C5. |
| BH2-C9 | Late input validation; finalizer fails on the missing helper | low | PA-4 | Gate at `domain-ci-v2.yml:582-601` runs after build. A failure before checkout adds an exit-2 failure. `unit-test-projects` is `required: false`. |
| ECH2-X13 | Same as C9 (finalizer) | low | PA-4 | Same evidence as C9. |
| BH2-C12 | Coverage gate runs after provisioning | low | PA-5 | `domain-ci-v2.yml:694` comes after `:636-664`. Moving the step is a direct correction. |
| BH2-C13 | v2 migration example lacks `uses:`/`secrets:`; secrets called "inputs" | low | PA-6 | `domain-ci-v2.md:15-25,109`. The owners input is documented in the README, so the "undocumented" part is false. |
| BH2-C14 | Fork/Dependabot always fail v2; undocumented | low | PA-6 | No Dependabot secrets exist, and the login fails on empty credentials. |
| BH2-C15 | Verification block omits `npm ci` and governed-provenance tests | low | PA-6 | `domain-ci-v2.md:150-155`. |
| ECH2-X18 | Same as C15 | low | PA-6 | Same evidence as C15. |
| BH2-C17 | README names Memories 3.0.23 while Props selects 3.0.25 | low | PA-6 | `README.md:591-592`. Text from `ad54fc75`, gone stale. |
| BH2-C20 | v2 `governed-ci` description copied from v1 | low | PA-6 | `domain-ci-v2.yml:101-107`. |
| ECH2-X8 | AttributeError on non-object nested JSON escapes classification | low | PA-7 | `main` catch at `:573`. Cleanup still runs through `finally`. Adding the exception is a direct fix. |
| ECH2-X15 | Non-atomic evidence write | low | PA-8 | Truncate then write in place (`:206-213`). Concurrent writers are possible with the C3 orphan. |
| VG2-2 | Zero child exit without receipt or Success not pinned | medium | PA-9 | Mutation survived all 43 helper tests (filed). |
| VG2-3 | `cleanup_verified` exit-code/runId/outcome conditions not pinned | medium | PA-9 | Mutation survived (filed). |
| VG2-4 | Receipt `stateDirectory` binding never exercised | medium | PA-9 | Mutation survived (filed). |
| VG2-5 | `finalize --job-status` CLI path never runs | medium | PA-9 | Mutation survived (filed). |
| VG2-6 | Second `down` result Success/runId not asserted | low | PA-9 | `CiRunLifecycleTests.cs:150` (filed). |
| BH1-R1 | CI branch writes `Cancelled` for passed/failed runs with incomplete cleanup | low | PA-10 | `ModuleCommandExecutionService.cs:1399` → `CompositionEngine.cs:624-627`. Visible in raw state and admission only. |
| ECH1-E6 | Same as R1 | low | PA-10 | Same evidence as R1. |
| BH1-R12 | `CompositionRunSession.LocalExecution` dead; its doc comment is false | low | PA-11 | No production reader after `CanHoldAttachments` changed. Delete it. |
| ECH1-E16 | Same as R12 | low | PA-11 | Same evidence as R12. |
| ECH1-E7 | CI withdrawal 5 s token not linked to the stop budget | low | PA-12 | `ModuleCommandExecutionService.cs:1398`. Overrun is at most 5 s, against the spec's "preserve budgets". |
| ECH1-E8 | Failed CI withdrawal in FailAndTearDownAsync returns "failed" with a Cancelled exit and omits leftovers/HXR023 | medium | PA-13 | `CompositionEngine.cs:2004`. The AppHost is left running with no remaining resources reported. Matrix row "non-success with explicit leftovers". |
| VG1-2 | `--ci-owner-receipt` wiring and receipt-before-allocation never exercised | medium | PA-14 | Removing the wiring breaks no test (filed). |
| VG1-3 | Receipt JSON key contract unchecked across C#/Python | medium | PA-14 | A rename passes both suites (filed). |
| VG1-4 | `HEXALITH_CI_JOB_STATUS` → `CiJobCancelled` bridge untested | medium | PA-14 | The test injects the flag directly (filed). |
| VG1-5 | `list` retention for CI owners never asserted | low | PA-14 | Deleting `CompositionRunListing.cs:128` breaks no test (filed). |
| VG1-6 | CI partial-startup failed-withdrawal branch untested | medium | PA-14 | `CompositionEngine.cs:1992-2006` (filed). |
| BH3-P12 | `PackageCatalogParticipantsBindBeforeContainersOrSidecarsAreAdded` is vacuous | medium | PA-15 | Calls only `ResolveParticipants` and asserts on a fresh builder (`PlatformAppModelTests.cs:119-130`), origin `68b785b6`. |
| ECH3-Y16 | Same as P12 (task claim) | medium | PA-15 | Same evidence as P12. |
| VG3-4 | Package-mode native-preparation registration unasserted | medium | PA-15 | Restoring the `#if` keeps package suites green (filed). |
| BH3-P18 | Parties ci-lifecycle unit tests run in no workflow | medium | PA-16 | No `unittest discover -s tests/ci-lifecycle` anywhere. Includes the `exec` regression test. |
| VG3-6 | Same as P18 | medium | PA-16 | Filed with the same evidence. |
| VG3-7 | Controller `withdrawn()` mocked in every test | medium | PA-17 | `test_cleanup_matrix.py:142`, `test_qualification_controller.py:155-170` (filed). |
| ECH3-Y1 | Signal before `wait-cancellation` installs handlers is lost | medium | PA-18 | Handlers installed after `Qualification()` init, which includes `verify_tool` (`:947`→`:963`). Run 42526256372 had about 2.4 s of margin. Fails closed but flaky. |
| ECH3-Y4 | `down(expected=False)` credits any unverified result as the injected lock refusal | medium | PA-19 | `qualify-disposable-ci.py:665-666` never checks HXR037. |
| BH3-P21 | Checkout persists credentials; `DOCKER_CONFIG` exported through `GITHUB_ENV` | low | PA-20 | `yml:57-59,136`. Token is `contents: read`. Direct fix. |
| BH3-P22 | `remove-reader` crashes if the helper import fails, leaving the credential file | low | PA-21 | Module-load `exec_module` (`:24-27`) runs before `main()`. |
| ECH3-Y10 | Same as P22 | low | PA-21 | Same evidence as P22. |
| BH3-P27 | Contract silently drops HXR035; `HEXALITH_CI_JOB_STATUS` and the failed-job end reason undocumented | low | PA-22 | `test-environment-v1.md`. The HXR033 part is false (already documented at `:330-332`). |
| BH3-P29 | Toolchain step raises AttributeError on a missing element | low | PA-23 | `yml:78-80`. Fails closed. Direct None check. |
| VG2-1 | Legacy `allowed-not-runnable-tests` never executed by tests | medium | DF | Origin `cce084c4` (Works allowlist), not Story 2.8. |
| ECH1-E5 | `VerifyModelCommonProps` InvalidDataException escapes into generic HXR020 | low | DF | Origin `e9b74ab2`. Direct catch fix. |
| ECH1-E10 | Routing bundle archived and fsynced three times inside the stop budget | medium | DF | Origin `4d892075` (routing catalog evidence). |
| VG1-1 | Engine forwarding of `--with-owner` untested end to end | medium | DF | Origin `0b18cbde`, not in the 2.8 Code Map (filed patch, routed by attribution). |
| BH1-R22 | Leftover debug `WriteLine`; expanded-owner tests assert only the message | low | DF | Origins `26b9c1d1`/`0b18cbde`. |
| BH1-R21 | Static-vs-const and missing final newline conventions | low | DF | Origin `c354a044`. Cosmetic. |
| BH3-P8 | `artifactSha256` hashes the `.csproj` and is null for images | low | DF | Origin `74abb092`. No consumer checks it. |
| ECH3-Y14 | Same as P8 | low | DF | Same evidence as P8. |
| BH3-P9 | Probe failure message names the tag, not the digest reference | low | DF | Origin `4c7641b2`. The crash part is false (only exact tagged images get digests). |
| ECH3-Y13 | Malformed image reference crash | false | RJ | `ImageDigest` is assigned only to exact tagged `registry.hexalith.com/...:x.y.z` images. |
| BH3-P10 | Stale "mapped routing catalog owner" message on the package lane | low | DF | Origin `01948735`. |
| BH3-P11 | Current-owner `--no-build`/`--artifacts-path` arguments untested | low | DF | Origin `a04df42f`. Other branches are tested. |
| BH3-P13 | Test temp tree created before `try`; vacuous pre-Compose assertion | low | DF | Origins `74abb092`/`87896d57`. |
| BH3-P15 | Windows `return` reports a pass | low | DF | Origin `74abb092`. No Windows CI. |
| BH3-P16 | Comment typos | low | DF | Not 2.8. Cosmetic. |
| BH3-P6 | Several new Platform refusals untested | medium | DF | Same gaps as VG3-1/VG3-2, origin `74abb092`. |
| VG3-1 | Retained selected-closure refusal has no negative test | medium | DF | Origin `74abb092` (filed patch, routed by attribution). |
| VG3-2 | File-level link and size bounds untested | medium | DF | Origin `74abb092`. |
| VG3-3 | Module container digest pin unasserted | medium | DF | Origin `4c7641b2`. |
| VG3-5 | Canonical-scope gateway call sites untested | medium | DF | Filed as defer. |
| ECH2-X20 | v2 accepts any mapped registry credential; pull-only unverified | maybe-false | DF | Unverified, high if true. Settle it by inspecting registry.hexalith.com robot permissions for the mapped reader secrets. |
| ECH3-Y17 | Same for the Parties reader | maybe-false | DF | Same as X20. |
| BH2-C6 | v2 lacks the NotRunnable allowlist | low | RJ | No v2 caller needs it. The gap was opened by `cce084c4`. Fixing it needs a new input. |
| VG2-o1 | Same as C6 | low | RJ | Same as C6. |
| BH2-C8 | TRX validator duplicated six times | low | RJ | Developer-only. Extraction is non-trivial. |
| BH2-C11 | Fixed v2 artifact names collide | low | RJ | No current multi-call caller. Re-runs are fine. Fix is non-trivial. |
| ECH2-X10 | Same as C11 | low | RJ | Same as C11. |
| BH2-C16 | Platform tag fetch has no timeout or limit | low | RJ | Origin `eca2c60f`/`0b8c3f20`. Rare stall. The fix adds a guard. |
| ECH2-X14 | Same as C16 | low | RJ | Same as C16. |
| ECH2-X11 | Duplicate unit-test basenames overwrite TRX/coverage | low | RJ | Pre-baseline pattern, rare. The fix adds a guard. |
| BH1-R2 | Three withdrawal end-reason rules diverge | low | RJ | Narrow failed-withdrawal path; first withdrawal wins. Fix is non-trivial. |
| ECH1-E9 | Same as R2 | low | RJ | Same as R2. |
| BH1-R4 | Redundant `--with-owner` marks the run expanded | low | RJ | Origin `0b18cbde`. Caller misconfiguration only. The fix adds a guard. |
| BH1-R13 | Tests reflect on private methods | low | RJ | Breakage would fail tests loudly. A hook is non-trivial. |
| BH1-R16 | Off-Linux skips cover every case | low | RJ | All CI is Linux. A split is non-trivial. |
| BH1-R17 | McpCli build keeps the unfiltered argument | maybe-false | RJ | Origin `e9b74ab2`. Low if true. Needs a McpCli diverging-catalog build to settle. |
| BH1-R19 | Win32 kind label; duplicated formatting | low | RJ | Cosmetic. A rename changes the enum surface. |
| ECH1-E2 | Receipt path not prevalidated; generic HXR020 | low | RJ | Misconfiguration only; fails closed. Fix is non-trivial. |
| ECH1-E3 | Startup timeout plus receipt write failure lets IOException escape | low | RJ | Narrow double fault before allocation. Fix is non-trivial. |
| ECH1-E4 | `--with-owner` Bind refusal after allocation | low | RJ | Origin `0b18cbde`; fails closed. The fix adds a precheck. |
| ECH1-E14 | Matrix legs share `GITHUB_JOB` on a shared self-hosted root | low | RJ | Intent targets disposable GitHub-hosted runners, which never share a filesystem. |
| ECH2-X9 | Same as E14 | low | RJ | Same as E14. |
| ECH1-E17 | Self-hosted CI owners now admit holds | low | RJ | Intent targets disposable hosted runners. |
| BH3-P1 | LF→CRLF churn on two composition files | low | RJ | Origin `1a1efb25`. Reverting would churn again; repository convention keeps CRLF. |
| BH3-P14 | Polling checks bundled into an unrelated test | low | RJ | Timing is a lower bound (not flaky). A split is non-trivial. |
| BH3-P26 | Negative admission probe varies only the job | low | RJ | Builds unit tests cover repository/run/attempt. Adding hosted probes is non-trivial. |
| ECH3-Y12 | Non-string closure ids raise unclassified exceptions | low | RJ | Needs tampering of an owner-only file. Fails closed as HXR027. |
| BH1-R3 | CI detection duplicated | false | RJ | No divergence in detection demonstrated. |
| BH1-R5 | `MatchesOwner` skips `CreatedAtUtc` | false | RJ | The only caller makes fresh attempt/environment ids, so a match is the identical owner. |
| BH1-R6 | Receipt lock races | false | RJ | No same-owner concurrent production retry. `Path.Exists` (lstat) catches a link. |
| BH1-R8 | Owner tag reuses the JWT key | false | RJ | Different key bytes and message formats. Validators start only after the plan is verified. |
| BH1-R9 | Grants not canonicalized | false | RJ | Every input is already canonical. |
| ECH1-E13 | Spaces in grant values | false | RJ | Validated kebab and namespace inputs only. |
| BH1-R10 | Null `CatalogCommit` compares equal | false | RJ | The writer refuses a null pin; the reader binds it to `PlatformIdentity`. |
| BH1-R11 | `CredentialName` misses header names | false | RJ | The producer writes no headers, and its fields are checked against an exact list. |
| BH1-R14 | Local stopping-budget coverage lost | false | RJ | The deleted test already ran in CI mode; its assertions are kept. |
| BH1-R18 | dotnet env allowlist inconsistent | false | RJ | Empty is forwarded only when the parent's value is empty; every SDK child is started as `dotnet`. |
| BH1-R20 | Engine constructor can throw for down/list/attach | false | RJ | Owners are set only for `test` and normalized before any engine is built. |
| BH1-R23 | Release hash tables have no automated check | false | RJ | `DirectCatalogDeclarationTests` covers them, and runtime refuses any mismatch. |
| ECH1-E1 | Null descriptor path throws | false | RJ | The production caller always passes the assembly location. |
| ECH1-E11 | `$(Prop)` edge cases | false | RJ | Every property is literal, and the caller refuses null as unpinned. |
| ECH1-E12 | Symlinked root lets the receipt land in the run workspace | false | RJ | The workspace is a random runId subdirectory created after the receipt. |
| ECH1-E18 | Module catalog not checked against the pin | false | RJ | `PlatformIdentityResolver.cs:181-191` HXW009, fatal in package mode. |
| BH2-C18 | Release evidence upload only warns | false | RJ | An `always()` step always creates `producer-attempt.json`. |
| BH2-C19 | Empty CI run id silently drops the binding | false | RJ | `jq -e` fails the step first. |
| ECH2-X7 | Receipt alone authorizes `down` | false | RJ | Intended receipt-first recovery, bound to the exact repository/run/attempt/job. |
| ECH2-X16 | Duplicate suite names: last wins | false | RJ | The tool refuses an ambiguous or mode-mismatched selection before allocation. |
| ECH2-X17 | Repeated allowlisted name fails | false | RJ | Facts appear once per TRX; a collision would only come from a theory and is intentional. |
| ECH2-X19 | Release dropped the validator | false | RJ | Release requires exact-SHA push CI, which runs both modes. |
| BH3-P2 | Unlisted version silently builds legacy branches | false | RJ | Runtime `Bind` refuses non-cohort assemblies. |
| ECH3-Y15 | Same as P2 | false | RJ | Same as P2. |
| BH3-P3 | Unbounded polling with a null deadline | false | RJ | The caller's token is bounded at 600 s (`AppHostRunner.cs:113-126`). |
| BH3-P4 | `ReadRequiredAsync` waits forever | false | RJ | Same startup-bounded token. |
| BH3-P5 | TOCTOU / shrinking file | false | RJ | Inputs are written before start or atomically moved, and never rewritten. |
| BH3-P7 | Unguarded `readbacks["eventstore"]` | false | RJ | `eventstore` is always a participant when any are. |
| BH3-P17 | Operator precedence | false | RJ | The grouping matches the intended semantics. |
| BH3-P19 | Review diff incomplete | false | RJ | Observation about review input, not a code defect. |
| BH3-P23 | Private helper attributes; escaping exceptions | false | RJ | All attributes exist at the pinned helper; the inputs cannot be None. |
| ECH3-Y3 | Same as P23 | false | RJ | Same as P23. |
| BH3-P24 | 5 s launch wait | false | RJ | The child needs about 0.02 s; the hosted matrix launched all owners. |
| ECH3-Y6 | Same as P24 | false | RJ | Same as P24. |
| BH3-P25 | 120 s operator window undocumented | false | RJ | `docs/ci-lifecycle-qualification.md:56,96`. |
| BH3-P28 | Journal not fsynced | false | RJ | The atomic replace suffices between processes on an ephemeral VM. |
| ECH3-Y2 | AppHost left SIGSTOPped after an exception | false | RJ | No realistic trigger, and the always() finalizer resumes the AppHost. |
| ECH3-Y5 | Finalizer never signals live launches | false | RJ | Owners exit after `down`; the hosted finalizer recorded no failures. |
| ECH3-Y7 | Peer timeout during the retry steps | false | RJ | The sequence used 29 s of the 240 s bound. |
| ECH3-Y8 | Raw tool stdout in evidence | false | RJ | The tool result is a metadata-only contract, and the marker fields are fixed. |
| ECH3-Y11 | Single metadata GET without retry | false | RJ | It runs after readiness with a 1000-permit limiter. |
| ECH3-Y18 | APP_API_TOKEN negative assertion removed | false | RJ | Replaced by equality and inequality assertions at `PlatformAppModelTests.cs:497-498`. |

**Outcome:** three bad_spec groups (BS-1 high, BS-2 and BS-3 medium). The cascade requires a loopback to Step 03. The owner chose a targeted fix with no revert (Spec Change Log, loop 1). The defer entries are appended to `deferred-work.md`.

### Iteration 1, 2026-11-09 (fix-delta review)

Scope: the loop-1 fix delta only. Area B is Builds `27145e52..b411bafb` (166 KB). Area P is Parties `926c1c3f..2849c5b4` plus the Platform working-tree PA-15/PA-22 edits (53 KB). Three layers ran on each area: blind hunter (BHB/BHP), edge-case hunter (ECHB/ECHP) and verification gap (VGB/VGP). No iteration-0 row repeats at the same location with the same claim, so nothing is carried. Where an earlier row's reasoning applies, it is cited.

Route groups:
- **BS-4:** cancellation and finalizer signals reach composed processes before withdrawal.
- **BS-5:** the timeout budget is incomplete.
- **BS-2b:** the sibling startup-diagnostics workflow keeps unverified Dapr archives.
- **PB-n:** patches.
- **RJ:** reject.

| ID | Claim | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| ECHB-14 | Whole-group SIGINT reaches the AppHost/DCP, so resources stop with or before withdrawal | high | BS-4 | `ToolSession.forward`/`cancellation_settled` call `killpg` on the tool group. The tool starts the AppHost with plain `Process.Start` (`CompositionProcess.CreateStartInfo`), with no setsid or setpgid anywhere in `Runtime/`. The AppHost has no SIGINT handling of its own beyond the stdin `StopSignal`, so the default host lifetime stops the app on SIGINT. DCP shares the group. Parties Q `process_signal` uses the same `killpg`, so run 42528046681 did not prove that withdrawal came before the first stop. |
| ECHB-3 | Finalizer kills a surviving AppHost before the exact down withdraws | medium | BS-4 | `recorded_tool_stopped` terminates every session member before `exact_down`. On the PA-13 path (CI withdrawal refused, AppHost deliberately alive), resources therefore stop before withdrawal is recorded. |
| BHB-1 | Default job timeout (45) is below prepare (31) + integration (40) + finalizer (4) | medium | BS-5 | `domain-ci-v2.yml:191,357,734,744`. In the hosted proof, setup and isolated tests took about 8 minutes, so with the default the job cancellation fires before the helper's 35-minute timeout path completes. |
| ECHB-2 | Same as BHB-1 | medium | BS-5 | Same evidence as BHB-1. |
| BHB-2 | Step budget omits `verify_tool` ×2 and session-termination waits; worst case ~2455 s > 2400 s | medium | BS-5 | `TIMEOUT_STOP_SECONDS` 120 plus 10, the finalize default 150, two 30 s `verify_tool` probes and 3×5 s. The ps1 check hard-codes `+120+150`. |
| ECHB-1 | Same as BHB-2 | medium | BS-5 | Same evidence as BHB-2. |
| VGB-2 | Same as BHB-2 (filed) | medium | BS-5 | Pre-verified gap. |
| BHP-2 | `current-owner-startup-diagnostics.yml` still extracts Dapr archives without digests | medium | BS-2b | `yml:73-77`, origin `8936e4e5` (2.8 window). BS-2 named only V2 and L. |
| ECHP-1 | Parties verifiers refuse every new-helper evidence set: `invocation.json` gains `timeoutSeconds` | medium | PB-1 | `verify-public-package-integration.py:90-91` and `verify-current-owner-integration.py:164` require exact equality. The helper now writes `timeoutSeconds`, and Parties `ci.yml:52-78` runs the public verifier. The claim that the readers accept both layouts is therefore false. |
| BHB-4 | Failure to write the "attempted" cleanup record skips the exact down | medium | PB-2 | `exact_down` writes before `subprocess.run`. ENOSPC or an evidence-path refusal raises, and no down runs in the step or the finalizer. |
| ECHB-4 | Same as BHB-4 | medium | PB-2 | Same evidence as BHB-4. |
| VGB-1 | Cancellation escalation (6 s SIGTERM, 8.5 s SIGKILL, second signal) unexercised | medium | PB-3 | Every fake tool exits on the first SIGINT (filed). |
| BHB-11 | Same gap; also `bootId`, the 16 MiB stdout cap and the 99-record limit untested | medium | PB-3 | The escalation part is covered under VGB-1. `bootId` goes with PB-5. The size and limit parts are low and rejected: unreachable in practice. |
| VGB-3 | Digest file not tied to the real catalog versions | medium | PB-4 | The ps1 `pinned` fixture hard-codes 1.18.2/1.18.7 (filed). |
| BHB-12 | Same as VGB-3; no regeneration script | medium | PB-4 | The script part is low and rejected. |
| VGB-6 | PID-reuse guard (`startTime`, `bootId`) untested | medium | PB-5 | Deleting the guard breaks no test (filed). |
| VGB-4 | `RecordOwnerStatusAsync(Failed)` branch never observed | medium | PB-6 | The only reaching test has a spent allowance (filed). |
| VGB-5 | Refused partial-startup `StoppingTimestamp` unasserted | medium | PB-6 | Dropping the line keeps the test green (filed). |
| VGP-1 | L `verify_archive` checked only as text; a fail-open edit passes all tests | medium | PB-7 | Mutations survived 85/85 (filed). |
| BHP-16 | Digest tests miss a swapped variable and other cases | medium | PB-7 | The swapped-variable case is the same gap as VGP-1. The missing `*)` is low and rejected: a wrong digest refuses. |
| VGP-2 | Observer step `exec` not locked in by any test | medium | PB-8 | Removing `exec` keeps 85/85 green (filed). |
| BHP-14 | Same as VGP-2 | medium | PB-8 | Same evidence as VGP-2. |
| VGP-4 | PA-15 never runs and its participant checks recompute rather than observe | medium | PB-9 | Not selected in `eng/tuple-ci-suites.json`, and `RoutingCatalogObservationsRequired` is constant `true` (filed). |
| BHP-11 | Same as VGP-4 | medium | PB-9 | Same evidence as VGP-4. |
| BHP-5 | `LOCK_REFUSALS` accepts HXR023 (generic cleanup-incomplete) for either injected lock | low | PB-10 | HXR023 is also emitted for leftovers and unverified scans. Bind `withdrawal-lock-refusal` to HXR037 and `provisioning-lock-refusal` to HXR023, as the hosted matrix observed. |
| ECHP-6 | Same as BHP-5 | low | PB-10 | Same evidence as BHP-5. |
| BHP-6 | `integration_phases` accepts mixed layouts, contrary to its docstring | low | PB-11 | Only the cleanup half is layout-checked. |
| ECHP-2 | Same as BHP-6 | low | PB-11 | Same evidence as BHP-6. |
| BHP-7 | Wrapper and cleanup records never bound to the job context and owner | low | PB-11 | `integration_phases` gets neither `context` nor `owner`. |
| ECHP-4 | Same as BHP-7 | low | PB-11 | Same evidence as BHP-7. |
| ECHP-3 | `processExitCode` false/0.0 passes `== 0` | low | PB-11 | Python equality. Requires an `int` type check. |
| BHB-7 | Redaction runs after the 4096-character slice, so a split secret survives | low | PB-12 | `stderr_record` slices, then substitutes. Swapping the order is a direct fix. |
| ECHB-6 | Same as BHB-7 | low | PB-12 | Same evidence as BHB-7. |
| ECHB-7 | `\b` boundary misses `_PASSWORD=`/`_TOKEN=` | low | PB-12 | `_` is a word character. Use a letter lookaround. |
| BHB-8 | Non-UTF-8 down output or a missing `dotnet` escapes as an exception; record stays "attempted" and retention is skipped | low | PB-13 | `subprocess.run(text=True)` decodes strictly. Use `errors="replace"` and classify OSError. |
| ECHB-5 | Same as BHB-8 | low | PB-13 | Same evidence as BHB-8. |
| BHB-3 | A timed-out exact down orphans its tool, which can hold `provisioning.lock` | low | PB-14 | `subprocess.run(timeout=)` kills only the muxer. This contradicts BS-1's no-orphan-lock rule. Run down in its own session and kill the group on timeout. |
| BHB-14 | Doc says Dependabot runs cannot pass; Dependabot secrets can be configured | low | PB-15 | `domain-ci-v2.md` fork/Dependabot paragraph. Direct doc fix. The fail-fast part is rejected: it adds a guard. |
| BHP-4 | Comment says the fixed-path reader file is reachable only from owner steps | low | PB-15 | Any later step can read `${{ runner.temp }}/…reader`. Correct the comment. |
| BHP-10 | Retired-HXR035 paragraph lists reasons inexactly | low | PB-15 | The runtime emits `owner-starting`/`-passed`/`-failed`/`-cancelled`/`-withdrawn`/`-finished`/`-cleaning`/`-cleaned` (`CompositionAttachAdmission.cs:305-353`). |
| BHP-3 | Sibling workflow exports `DOCKER_CONFIG` through `GITHUB_ENV` | low | PB-16 | `current-owner-startup-diagnostics.yml:96`. Same fix as PA-20. |
| BHB-5 | A malformed or foreign process record blocks cleanup permanently | false | RJ | The record is written once, atomically, under the job-private `RUNNER_TEMP`. A killed writer leaves only the temporary name. No reachable trigger. |
| BHB-6 | Stderr bytes/SHA-256 describe a decoded 16 MiB prefix | low | RJ | Needs stderr over 16 MiB or non-UTF-8 output from the .NET tool. The fix adds streaming hashing. |
| ECHB-8 | Same as BHB-6 | low | RJ | Same as BHB-6. |
| BHB-9 | A cancellation during the helper's own post-run down is ignored | low | RJ | The always() finalizer repeats the exact down, and the "attempted" record is accurate. The fix needs an interruptible down. |
| ECHB-11 | Cancellation during the 120 s post-timeout stop loses `execution.json` | low | RJ | A double fault. The finalizer still cleans. |
| VGB-o1 | Same as BHB-9 | low | RJ | Same as BHB-9. |
| BHB-10 | Descendants that setsid escape session tracking | false | RJ | The receipt is persisted before any allocation (`EngineWritesCiOwnerReceiptBeforeStateAndWorkspaceAsync`). Before it exists, only the in-session tool runs, and exact down stops inventoried processes. |
| ECHB-9 | Same as BHB-10 | false | RJ | Same as BHB-10. |
| BHB-13 | Aspire.Cli is not digest-pinned | low | RJ | Exact immutable nuget.org version, signature-verified by the SDK. A pin needs trusted-signer configuration. |
| BHB-15 | Declared, uncontended concurrent down no longer tested | low | RJ | That row races outcome recording, which was the original flake. A deterministic row needs a new sequencing hook. |
| BHB-16 | Budget theory rows no longer differ | low | RJ | In-allowance withdrawal is covered by `ExecutingCiOwnerReportsWithdrawalBeforeFirstComposedResourceStopsAsync`. |
| BHB-17a | Receipt-key test only searches helper text | low | RJ | The hosted v2 runs read the real C# receipt with the helper. A cross-runtime test is non-trivial. |
| VGB-o2 | Same as BHB-17a | low | RJ | Same as BHB-17a. |
| BHB-17b | `<=` timestamps cannot prove receipt-first | false | RJ | The unwritable-receipt row proves that no state or workspace exists when the receipt fails. |
| BHB-17c | Tests reflect on private methods | low | RJ | Same reasoning as BH1-R13. |
| BHB-18 | Redundant `module-manifest != ''` conditions | false | RJ | No bad outcome. The inputs are required and validated first. |
| BHB-19 | No directory fsync after rename | false | RJ | Same reasoning as BH3-P28: readers share the ephemeral VM. |
| ECHB-10 | `killpg` PermissionError escapes | false | RJ | No privileged process runs in the tool session, so the trigger is not reachable. |
| ECHB-12 | Missing-terminal owners withdraw as `ci-job-cancelled` | false | RJ | Matches the PA-22 contract. The first withdrawal wins (BH1-R2). |
| ECHB-13 | Removed `finally: finalize` skips the in-step down on exceptions | low | RJ | The always() finalizer covers exception paths. An unreleased gate never runs the tool. |
| VGB-7 | Parties observer reads old shapes | false | RJ | Parties `2849c5b4` adds `helper_phase_documents`. The reviewer read the canonical checkout at `6f8cd78d`. |
| BHP-1 | L cannot run at the current Builds pin | false | RJ | Intended fail-closed state, recorded in the spec. Delivery repins first. |
| ECHP-8 | Same as BHP-1 | false | RJ | Same as BHP-1. |
| BHP-8 | Observer ignores wrapper status fields | low | RJ | Diagnostic-only observer. The fix adds branches. |
| ECHP-5 | Same as BHP-8 | low | RJ | Same as BHP-8. |
| BHP-9 | Platform contract describes unreleased helper behavior | false | RJ | The edit is uncommitted and ships with the Builds repin in L/M delivery. The evidence layout is documented in `domain-ci-v2.md`. |
| ECHP-10 | Same as BHP-9 | false | RJ | Same as BHP-9. |
| BHP-12 | PA-15 test is no longer hermetic | low | RJ | PA-15 required Bind and Compose, which need the package fixture. |
| BHP-13 | A late-handler regression crashes the test runner | low | RJ | The suite still fails, so the regression is detected. A guard adds complexity. |
| ECHP-9 | Unconditional SIGTERM in an observer test can crash the runner | low | RJ | Same as BHP-13. |
| BHP-15 | Current-owner verifier lacks a per-attempt test | low | RJ | No workflow calls it. The shared `integration_phases` is tested through the public verifier. |
| VGP-3 | Same as BHP-15 (filed defer) | low | RJ | This story's code, low, no caller. |
| ECHP-7 | Signal before handlers during interpreter startup | low | RJ | Fails closed and the finalizer cleans. The fix needs a new readiness marker in the cancel protocol. |

**Outcome:** three bad_spec groups (BS-4 high, BS-5 and BS-2b medium), so the cascade loops back to Step 03. The PB-1 to PB-16 patches are carried into that loop's tasks. Nothing was deferred. `review_loop_iteration` becomes 2. How to re-derive is the owner's decision.
