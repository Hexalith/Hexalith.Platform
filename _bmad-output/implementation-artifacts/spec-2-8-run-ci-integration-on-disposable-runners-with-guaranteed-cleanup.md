---
title: '2.8 Run CI integration on disposable runners with guaranteed cleanup'
type: 'feature'
created: '2026-10-31'
status: 'done'
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
- [x] Complete relevant owner-repository source checks and the required final current/real-legacy/five-SDK checks for the actual final source. Preserve all original controls and explicitly account for meaningful incoming added cases. Complete any genuinely required normal source publication once; do not create unrelated release prerequisites.
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

- [x] **BS-4, withdraw before stop under signals: H, V2, Q, plus their tests.** A runner cancellation must never stop a composed resource before the tool has recorded attachment withdrawal.
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
  - *Owner amendment, 2026-11-09 (Step 03 verification; see Spec Change Log "Loop 2 amendment"). It replaces the two hosted runs above.* The v2 execution-phase run is not achievable as written. Parties v2 native tests are live for about 11 s after about 163 s of startup (CI 42479678863: readiness 12:32:02.0Z, terminal 12:32:13.5Z). Lengthening them changes the suite inputs, which the package profile refuses with HXP020 (attempt 42587081454). Builds v4.45.1 (`25de7a74`, which contains `bde7fd4f` with an unchanged helper and v2 workflow) is now released, so the remaining BS-4 hosted proof is:
    - **L rerun:** `cancel-execution` with an active same-job hold, on a Parties proof branch based on `4c0167b8`. The branch adopts v4.45.1 for both the pinned tool and the `references/Hexalith.Builds` gitlink. It must exercise the patched Q, the patched H finalizer and the loop-1 runtime. Apply the same three-timestamp rule and kernel-signal origin check as run 42587199919.
    - **V2 late-startup run:** a Parties proof caller of `domain-ci-v2.yml` at v4.45.1, pinning tool 4.45.1, with unchanged suite and test inputs. A kernel-signal observer is started from the caller's prepare script, not from a test change. Exactly one cancellation is sent, after the ready check: the "Runner-owned package integration" step is in progress and its age has reached a target chosen from retained startup timelines so that the AppHost, DCP and at least one owned container are live before readiness. Durable evidence must show that the helper signalled only the gated muxer and the tool, that no composed process was signalled from outside the runtime before the tool's own stop, and that the always() finalizer's exact down verified empty inventories. If the evidence shows no owned composed resource existed at signal time, the run counts as not achieved; one further run is allowed.
    - Execution-phase v2 ordering is covered by the L run, which applies the same tool-only rule to the same muxer-and-tool process structure, plus the local H process-level test.
    - Each run gets one audited cancellation, uses proof branches only (deleted afterwards), and is labelled focused and baseline-only. This is no pin change on `main`.
  - *Second owner amendment, 2026-11-09 (see Spec Change Log "Loop 2 amendment 2"). It replaces the v4.45.1 bindings above; both v4.45.1 runs were refused before any cancellation: L 42593229291 with HXW009/HXW007, v2 42593223538 with HXP020.*
    - **V2 late-startup run, rebound:** the same requirement as the v2 bullet above, except that the caller uses `domain-ci-v2.yml@bde7fd4f875ae147c0631b97c349ef9b73561ed4` with Parties' pinned tool 4.44.13. At `bde7fd4f` the helper and v2 workflow are byte-identical to v4.45.1, and its catalog pins Dapr 1.18.7/1.18.2, matching the Parties Platform v1.7.1 / Builds `3c80` tuple. The module manifest, suites and test projects stay byte-identical to `4c0167b8`. The `PlatformPartiesRunProof` check requires a known ordinary declaration hash and clean Parties, Platform and Builds checkouts, so the observer writes only under `$RUNNER_TEMP`. Exactly one cancellation per run, at a step age taken from the retained startup timelines (about 130 s); one further run is allowed if no owned composed resource was live at signal time.
    - **L on patched Builds, deferred:** run 42587199919 is accepted as BS-4's hosted controller (Q) proof, with its recorded scope (tool 4.44.13, helper `3c80`). Running L `cancel-execution` on a Builds release that carries the patch becomes a delivery gate in the post-review re-pin chain, after Platform pins such a release through L/M. It is not a Step 03 obligation.
    - BS-4 is complete when the rebound v2 run passes. Until the deferred L gate runs, the H finalizer's owned-process exclusion stays covered by the local process-level test plus that v2 run's finalizer.
- [x] **BS-5, timeout budget: V2, H, `B/Tools/test-domain-workflow-test-platforms.ps1`.** The helper's timeout path must fire and finish inside both the step and the job.
  - Derive the helper timeout from the time actually left in the job when the integration step starts. Also keep it below the step `timeout-minutes`. Reserve every bounded wait on the timeout path: the pre-launch `verify_tool`, `TIMEOUT_STOP_SECONDS` plus the post-kill wait, session termination, the finalize-time `verify_tool`, the exact down and the 4-minute finalizer step.
  - If the remaining time cannot hold the minimum helper timeout, refuse before allocation.
  - Keep `build-timeout-minutes`, its default of 45 and every other original budget unchanged (loop-1 KEEP).
  - The validator must compute the budget from the helper's own constants rather than hard-coded numbers.
- [x] **BS-2b, S Dapr digests and credentials.** Apply the BS-2 digest verification from L, before any extraction, to S's Dapr CLI and runtime archives. Also apply the PA-20 credential scoping to S: no `DOCKER_CONFIG` or registry config exported through `GITHUB_ENV`. Add executed refusal tests in the Parties suite.
- [x] **Patches (PB-1 to PB-16, from the Review Triage Log, iteration 1):**
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
- [x] **Verification (loop 2):**
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

### Execution record — 2026-11-09 review loop 2

This run executed the *Review loop 2 tasks* under the remote-operation authorization above. Remote actions:

- fast-forward pushes to Builds PR #177;
- one Parties fix-branch push;
- proof-branch pushes and dispatches, each followed by deleting the proof branch;
- exactly one audited cancellation request in the one run that reached its ready check.

This loop made no merges, `main` pushes, tags, publications or pin changes. Every result is `baseline-only`. The records are in [review-loop-2-20261109](evidence/2-8/review-loop-2-20261109/) and are summarized in the [evidence README](evidence/2-8/README.md).

- **External delivery, not by this loop:** an external `/pushall` under the owner account did both of the following.
  - It merged PR #177 into Builds `main` (`494c96a9`, 08:13Z).
  - It committed and pushed this Platform working tree as `6381fd6f` (08:39Z). That commit carries the loop-1 and PB-9 edits, and its tuple-CI preflight failed in the legacy lane.

  Both are outside this run's authority and are reported, not repaired.
- **Builds** `b411bafb` → `95b65d45` → `54970a7a` → `bde7fd4f`:
  - BS-4 tool-only signals and the finalizer's owned-process exclusion;
  - BS-5 derived budget;
  - PB-2 to PB-6, PB-12 to PB-15;
  - Codacy docstring and suppression fixes.

  Every PR check passes at `bde7fd4f`.
- **Parties** `2849c5b4` → `4c0167b8`: the Q part of BS-4, BS-2b, PB-1, PB-7, PB-8, PB-10, PB-11 and the L part of PB-15.
- **Platform:** PB-9 (the test observes the composed preparation through a read-only accessor), and PB-15 plus the BS-4 limit in the contract.
- **Local checks:**
  - 232 Python tests with 626 subtests (11 new helper controls);
  - the validator, legacy 411 and v2 302 assertions;
  - 351/351 focused xUnit cases;
  - Parties 95/95 and 46/46, with the pinned and the patched helper;
  - the Platform composition tests compile.

  The new controls fail against the previous sources, except the PB-3 escalation coverage.
- **BS-4 hosted proofs:**
  - Lifecycle cancel-execution [42587199919](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42587199919) passed the ordering rule. The admission-closing write (08:59:34.220Z) and the withdrawal commit (08:59:34.463Z) both came before the first composed-environment stop (08:59:34.662Z), and inventories ended empty. It exercised the controller with the pinned 4.44.13 tool and the `3c80` helper.
  - The v2 execution-cancellation proof was not achieved. Two attempts failed on HTTP 504 Dapr downloads, and one on HXP020 because the proof instrumentation changed the suite inputs. No cancellation was sent.
  - Two lifecycle setup attempts also failed before any owner existed: HXW009 under the Platform Builds pin, and HXT007 after a submodule overlay.
- **Still open:**
  - **BS-4 is complete under the second owner amendment.**
    - The two v4.45.1 runs (42593229291 and 42593223538) were refused before any cancellation: HXW009/HXW007 under the Platform pin `3c80`, and HXP020. They stay recorded failures.
    - The rebound v2 late-startup run first ran as 42596887673. That run was not achieved because the observer's records were lost before upload. The run itself behaved correctly.
    - The one authorized rerun, [42598774773](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42598774773), passed. It used `domain-ci-v2.yml@bde7fd4f` and tool 4.44.13, with the gitlinks and every suite and test input unchanged. The observer wrote only under `$RUNNER_TEMP` and streamed its records into the finalizer step's job log, where line counts and SHA-256 are verified.
    - At 10:17:01.949Z the helper sent SIGINT only to the gated muxer and the tool. At that moment the AppHost, eight DCP processes and four owned containers were live.
    - No signal from outside the runtime reached any composed process. The first outcome was `cancelled`/HXC130 at 10:17:01.967Z, the tool's own first stop came at 10:17:02.764Z, and the `always()` finalizer's exact down verified empty inventories.
    - Lifecycle run 42587199919 stands as the hosted controller proof.
    - Lifecycle `cancel-execution` on patched Builds is a deferred delivery gate in the post-review re-pin chain. Until it runs, the finalizer's owned-process exclusion is covered by the local process-level test and this v2 run's finalizer.
  - Step 04 should rule on one observation: the tool SIGKILLed its own native test host (08:59:34.138–.203Z) before the admission-closing and withdrawal marks.
  - PB-9 execution remains a gate for Platform L/M delivery.
  - Delivery waits for Step 04: Task 134, adoption of a Builds release with the digest file, Parties PR delivery, and a repair of the externally pushed Platform tuple.

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

### Loop 2 amendment — 2026-11-09 (Step 03 verification → BS-4 hosted-proof scope)

- **Trigger:** Step 03 verification of loop 2 found BS-4's v2 execution-phase hosted run unachievable as written. The native window lasts about 11 s after about 163 s of startup. Changing the suite inputs is refused (HXP020). Three attempts sent no cancellation: two hit HTTP 504 on Dapr downloads, one HXP020. The passing L run 42587199919 exercised the patched Q only, with tool 4.44.13 and helper `3c80`.
- **New fact:** an external session released Builds v4.45.1 from `main` `25de7a74` at 09:09:57Z. That `main` carries PR #177 (`bde7fd4f`), merged externally at 08:13Z. This removes the tool and gitlink mismatch that blocked the L rerun on patched Builds (HXW009, HXT007).
- **Owner decision:** adopt both recommendations. Rerun L `cancel-execution` on v4.45.1, and run one v2 late-startup cancellation on v4.45.1 with a kernel-signal observer and unchanged test inputs. Execution-phase v2 ordering is covered by L's identical tool-only rule plus the local H process-level test. The BS-4 task carries the exact amended requirement.
- **Known-bad states:** changing Parties suite or test inputs for a proof; more than one cancellation per run; a cancellation before the ready check; treating a run that had no live owned composed resource as the v2 proof.
- **KEEP:** everything in loop 2's KEEP list; run 42587199919 with its result and label; the three failed v2 attempts and the two failed L setup attempts, as recorded.

### Loop 2 amendment 2 — 2026-11-09 (Step 03 verification → BS-4 rebinding)

- **Trigger:** both amended v4.45.1 runs were refused before any cancellation.
  - L 42593229291 failed with HXW009/HXW007: the Parties Platform pin v1.7.1 requires Builds `3c80`, and no Platform release pins v4.45.1.
  - V2 42593223538 failed with HXP020 0.2 s after AppHost start. Platform v1.7.1 `PlatformPartiesRunProof` refuses a re-read plan that does not match, an unknown declaration hash, or skewed or dirty checkouts. The likely cause is tool 4.45.1 and Dapr 1.19.1 against the 4.44.13 / Dapr 1.18 tuple.
- **Owner decision:** rebind the v2 late-startup run to `domain-ci-v2.yml@bde7fd4f` with tool 4.44.13 (same helper and workflow bytes as v4.45.1, Dapr 1.18 catalog). Accept L run 42587199919 as the hosted controller proof, and defer L on patched Builds to a delivery gate in the post-review re-pin chain. The no-pin-change boundary stays in force.
- **Known-bad states:** writing proof output inside any checkout; changing the module manifest, suites or tests; treating the deferred L gate as satisfied before it runs.
- **KEEP:** everything above, including runs 42593229291 and 42593223538 as recorded failures before cancellation.

### Iteration 3 owner decision — 2026-11-11 (G1 timeout classification)

- **Trigger:** iteration-3 review AA3-1. PC-1 interrupts the tool with SIGINT on a helper timeout so that withdrawal precedes every composed stop. The tool treats that SIGINT as a cancellation: it records the owner `cancelled` and ends active holds as `ci-job-cancelled`.
- **Owner decision:** accept it as owner cancellation by the helper. This amends PA-1's second clause.
  - A helper timeout still returns 124, and the helper's `execution.json` records `timedOut: true, cancelled: false`.
  - The durable owner record and the hold end reasons are `cancelled` and `ci-job-cancelled`.
  - No runtime change. `domain-ci-v2.md` and the contract record the rule, and Story 2.10 reads `execution.json` to tell a helper timeout from a job cancellation.
- **Rejected alternatives:**
  - a tool-owned deadline (new CLI surface and a Builds release);
  - reverting to SIGTERM, which reintroduces BHB2-1.
- **KEEP:** everything in the loop-2 KEEP lists. Exit 124 and the helper's `timedOut` classification stay as they are. Withdrawal must still precede every composed stop except the limits recorded under G5.

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

### Iteration 2, 2026-11-09 (loop-2 fix-delta review)

Scope: the loop-2 fix delta only. Area B is Builds `b411bafb..bde7fd4f` (86 KB; the helper and v2 workflow are byte-identical in released v4.45.1). Area P is Parties `2849c5b4..4c0167b8` plus the Platform Story 2.8 paths `7fbe852f..HEAD` (63 KB). Three layers ran on each area: blind hunter (BHB2/BHP2), edge-case hunter (ECHB2/ECHP2) and verification gap (VGB2/VGP2). Claims were verified read-only against Builds `bde7fd4f`, Parties `4c0167b8` and Platform `HEAD`, with one mutation run on an exported helper tree. Rows matching an earlier row's location and claim are marked carried.

Route groups:
- **PC-n:** patches (no intent_gap or bad_spec entry, so no loopback).
- **DF:** defer.
- **RJ:** reject.

| ID | Claim | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| BHB2-1 | Helper timeout sends SIGTERM first, which ends the real tool without withdrawal | high | PC-1 | `ToolCommandHost.RunWithConsoleCancellationAsync` registers only `PosixSignal.SIGINT` and `CancelKeyPress`; nothing handles SIGTERM, so the runtime default ends the tool. `StopSignal` stops a nonpersistent AppHost when it loses stdin (`PersistAfterParentExit` is false for CI `test`), so composed resources stop before any withdrawal, and only the later exact down records it. The timeout, `wait` and `muxer-apphost` fakes treat SIGTERM like SIGINT and hide this. The spec never prescribed SIGTERM here. |
| VGB2-2 | Timeout stop of tool-only targets is untested | medium | PC-1 | Pre-verified: restoring `signal_groups` in `stop_after_timeout` keeps 65/65 green. |
| BHB2-12 | No timeout test with an owned AppHost; no muxer-dies-first test; no reparented untagged descendant test | low | PC-1, PC-2, PC-4 | Same gaps as VGB2-2, VGB2-1 and VGB2-3. |
| BHB2-2 | Leader gone but tool alive: the no-receipt branch can record verified `not-allocated` after the tool writes its receipt | medium | PC-2 | `tool_processes` returns `{}` without the leader (`:635-637`). `finalize` checks `receipt.exists()`, stops `session_processes`, then writes `not-allocated` without re-reading the receipt. Loop 1 terminated the whole session before reading it. Reachable only when the muxer dies before the tool. |
| ECHB2-1 | Same as BHB2-2 | medium | PC-2 | Same evidence as BHB2-2. |
| ECHB2-18 | Same as BHB2-2 (claim form) | medium | PC-2 | Same evidence as BHB2-2. |
| VGB2-1 | No-receipt sweep with a surviving tool is untested | medium | PC-2 | Pre-verified: deleting the `session_processes` stop keeps 65/65 green. |
| VGB2-6 | `verify_tool` raises before `exact_down`, so a missing tool writes no `tool-unavailable` record, retains nothing, and turns timeout 124 into 1 | medium | PC-3 | Pre-verified probe: an unpatched `finalize` with no `dotnet` raised `FileNotFoundError` from `verify_tool` (`:752`) and left the evidence empty. PB-13 required unverified cleanup with diagnostics retained. |
| ECHB2-5 | Same as VGB2-6 | medium | PC-3 | Same evidence as VGB2-6. |
| ECHB2-17 | Same as VGB2-6 (claim form) | medium | PC-3 | Same evidence as VGB2-6. |
| VGB2-5 | Missing-tool test patches `verify_tool` away | medium | PC-3 | Pre-verified; the test patches both calls. |
| VGB2-3 | Protection of owned descendants is untested | medium | PC-4 | Pre-verified: replacing the parent walk in `protected()` keeps 65/65 green. |
| VGB2-4 | Only a failure of the first cleanup-record write is tested | low | PC-5 | Pre-verified: ignoring the final write's result keeps 65/65 green. |
| BHB2-4 | A timed-out down kills only its process group; the post-kill `communicate()` is unbounded | low | PC-6 | `run_down` uses `killpg(child.pid)`, then `communicate()` with no timeout. A descendant in another group holding the pipes blocks it. The docs say "session". |
| ECHB2-3 | Same as BHB2-4 | low | PC-6 | Same evidence as BHB2-4. |
| BHB2-9 | camelCase secret keys (`dbPassword`, `clientSecret`, `accessToken`) survive redaction | low | PC-7 | `(?<![a-z])` under `(?i)` rejects any letter before the keyword. Direct fix: drop the left boundary. |
| ECHB2-6 | Same as BHB2-9 | low | PC-7 | Same evidence as BHB2-9. |
| BHB2-8 | Validator pins helper source text; "Job start is recorded first" compares positions in the whole file | low | PC-8 | Ordering part: a non-checkout step inserted before the input step passes. Direct fix: assert the job's first step. The source-pinning part is rejected: the spec's "derive from the helper's constants" is met, and the alternative is a redesign. |
| ECHB2-10 | Same as BHB2-8 ordering part | low | PC-8 | Same evidence as BHB2-8. |
| BHB2-11 | New tests prove less than their names | low | PC-9, PC-10 | The v2-step test asserts only upper bounds (PC-9). `CancelAfter(5 s)` plus `IsCancellationRequested.ShouldBeFalse()` is load-sensitive (PC-10). The `StoppingTimestamp` range part is rejected: PB-6 asked only for the assertion. |
| ECHB2-13 | Same as BHB2-11 `CancelAfter` part | low | PC-10 | Same evidence as BHB2-11. |
| ECHB2-12 | Escalation test asserts SIGTERM below 0.6 s on two runner signals | low | PC-11 | Load-sensitive under the known cross-session load. Direct fix: larger patched constants. |
| ECHB2-11 | Exhausted-budget process test does not prove the budget refusal | low | PC-12 | Mutation: with the refusals in `derived_timeout` removed, the test still passes (it fails for missing preparation environment); only the unit test fails. |
| ECHB2-16 | `recorded_session_members` has no callers | low | PC-13 | `git grep` finds only its definition. |
| BHB2-13 | `domain-ci-v2.md`: "never signalled by the helper" contradicts the finalizer bullets; a glued sentence and an unwrapped line | low | PC-14 | Direct doc fix. |
| BHB2-3 | Nothing guards against composed processes being direct children of the leader | low | RJ | Hosted run 42598774773 traced the real layout (muxer leads, tool is its child, AppHost is the tool's child). A guard adds complexity. |
| BHB2-5 | `signal_processes` catches only `ProcessLookupError` | false | RJ | Same reasoning as ECHB-10: no privileged process runs in the tool session, and kill(2) allows a sender with the target's real uid. |
| ECHB2-2 | Same as BHB2-5 | false | RJ | Same as BHB2-5. |
| BHB2-6 | Job budget does not reserve the always() steps after the finalizer | low | RJ | It only bites when every bounded wait hits its maximum, and on a disposable runner. BS-5's reserve list is met. The fix adds a budget input. |
| ECHB2-9 | Same as BHB2-6 | low | RJ | Same as BHB2-6. |
| BHB2-7 | Validator digest scenarios degrade when CLI and runtime share a version | low | RJ | At `bde7fd4f` the versions differ (1.18.2/1.18.7). On the 1.19.1 catalog the scenario still refuses. The fix needs JSON editing in PowerShell. |
| BHB2-10 | `invocation.json` omits the derivation inputs | low | RJ | Not required. New fields would be refused by the Parties verifiers (PB-1). |
| BHB2-14 | Dependabot guidance omits exposure and mapping | low | RJ | The reader is pull-only, and explicit caller mapping is already documented. |
| BHB2-15 | `MINIMUM_TIMEOUT_SECONDS = 60` is unrealistic | low | RJ | The spec leaves the minimum open; a too-short run ends as a classified timeout. |
| ECHB2-4 | Invalid UTF-8 inside valid JSON can be recorded as verified | false | RJ | Replacement characters cannot satisfy `cleanup_verified`'s run identity and empty-inventory checks; a corrupted message string changes no verified field. |
| ECHB2-7 | The fixed 60 s setup allowance can be exceeded | low | RJ | "Set up job" took about 1 s in the recorded hosted runs, and the 30 s margin remains. |
| ECHB2-8 | Fractional or over-360 `build-timeout-minutes` | low | RJ | A fractional value fails closed in argparse before allocation. Above 360, the step bound dominates. |
| ECHB2-14 | An interrupted `run_down` orphans its down | low | RJ | The orphan is the exact cleanup itself. Any competing down fails closed on the lock, and the job is already being cancelled. |
| ECHB2-15 | A refused receipt leaves non-tool session processes running | false | RJ | Refusing destructive commands for a foreign or malformed receipt is the documented behavior, and the result is non-success. |
| BHP2-16 | PB-9 test cannot pass, and its "before resources" check is vacuous | medium | PC-15 | `Compose` reads `RunPlan.DaprConfigPath` (`PlatformAppModel.cs:194`). Every passing `fixture.Compose` caller writes it first, plus the `daprd` marker and DCP paths (`PlatformPublishedPartiesMemoriesTransportTests.cs:20-36`); this test writes none. `ExpectedHosts` is the whole participant cohort (`PlatformRoutingCatalogBootstrap.cs:28`), which the source catalog test pins as six (`PlatformAppModelCatalogReviewTests.cs:98`), not three. `builder.Resources.ShouldBeEmpty()` runs in the options callback, before binding. Loop 1's version also never set up the config. |
| ECHP2-7 | Same as BHP2-16 (participant set) | medium | PC-15 | Same evidence as BHP2-16. |
| ECHP2-8 | Same as BHP2-16 (Dapr config) | medium | PC-15 | Same evidence as BHP2-16. |
| ECHP2-12 | Same as BHP2-16 (claim form) | medium | PC-15 | Same evidence as BHP2-16. |
| BHP2-3 | Contract list swallows the following paragraph | low | PC-16 | `test-environment-v1.md:390-392`: no blank line, so the verification rule renders inside the "With a receipt" bullet. |
| BHP2-4 | Stale "`withdrawn` remains reserved for future attached runs" | low | PC-16 | `test-environment-v1.md:410` (Story 2.5 text) now contradicts `owner-withdrawn` and the CI hold rules. |
| BHP2-14 | Partial-startup test patches an attribute of the lazy `HELPER`, forcing the real import | low | PC-17 | Reproduced by two layers: `FileNotFoundError` without the submodule. Direct fix: patch `Q.HELPER` itself. The other test-strength parts are rejected as negligible. |
| ECHP2-9 | Same as BHP2-14 | low | PC-17 | Same evidence as BHP2-14. |
| VGP2-1 | Timed invocation with a raw layout is not refused by any test | low | PC-18 | Pre-verified: removing `and not timed` keeps 29/29 green. |
| VGP2-2 | The reader-file writer heredoc is never executed | low | PC-19 | Pre-verified: two heredoc mutations keep 95/95 green. |
| ECHP2-5 | A resumed partial-startup AppHost can keep provisioning while the down runs | maybe-false | DF | Medium if true (late resources could escape discovery). Settle by confirming that `down` stops run-tagged processes before its final discovery, or by the deferred L matrix on patched Builds. |
| BHP2-1 | S and L cannot pass at the pinned Builds `3c80` | false | RJ | Fail-closed by design until the delivery-chain repin (BS-2b; execution records). |
| ECHP2-1 | Same as BHP2-1 | false | RJ | Same as BHP2-1. |
| VGP2-3 | Same as BHP2-1; tests use synthetic Props | false | RJ | Same as BHP2-1. Synthetic catalogs exercise the step logic by design. |
| BHP2-2 | Contract describes Builds behavior the Platform pin lacks | false | RJ | carried BHP-9: ships with the Builds repin in L/M delivery. |
| BHP2-5 | Contract omits the evidence layout | low | RJ | Documented in `domain-ci-v2.md`. |
| BHP2-6 | `invocation_accepted` refuses `qualificationOwners` | low | RJ | PB-1 accepts only `timeoutSeconds`, and ordinary profiles forbid qualification owners (`PlatformPartiesRunProof`). |
| BHP2-7 | Per-record runId check misreports an `allocation-unverified` record; `jobStatus` unchecked | low | RJ | Fails closed on abnormal evidence; the message is the only harm. |
| BHP2-8 | Wrapped-execution checks omit `launched`/`signals`; no `timedOut` test | low | RJ | The `cancelled`, `timedOut` and integer exit-code checks already require a normal completion. |
| BHP2-9 | `process_signal` can deliver to nobody | low | RJ | Q signals only after readiness, when the tool runs. A lost signal fails closed on the required cancelled outcome. The fix adds a guard. |
| ECHP2-2 | Same as BHP2-9 | low | RJ | Same as BHP2-9. |
| BHP2-10 | Partial startup no longer exercises the whole-tree kill | low | RJ | Hosted matrix 42521615234 keeps that evidence, and BS-4 mandates tool-only signalling for Q. |
| BHP2-11 | `tool_processes` duplicated in Q and H | low | RJ | Q cannot use H's version until Parties pins a Builds release that has it. |
| BHP2-12 | Digest code duplicated in L and S | low | RJ | Refactor only. |
| BHP2-13 | S no longer validates the reader credential | low | RJ | Diagnostics-only workflow; a bad credential still shows as a pull failure. Validation would add a step. |
| ECHP2-10 | Same as BHP2-13 | low | RJ | Same as BHP2-13. |
| BHP2-15 | Missing refusal cases in the digest readers | low | RJ | Each fails closed on Builds-owned pinned data. |
| BHP2-17 | `ExpectedCatalogHosts` doc omits the app-id values | low | RJ | Negligible. |
| ECHP2-3 | PID reuse between the birth check and `os.kill` | low | RJ | Microsecond window; `pidfd` adds complexity. |
| ECHP2-4 | `PermissionError` reading `/proc/<pid>/stat` | false | RJ | Hosted runners do not mount `/proc` with `hidepid`, and `stat` is world-readable. |
| ECHP2-6 | Dict equality treats `True` as `1` | low | RJ | Records come only from the helper; forging needs write access to the evidence. |
| ECHP2-11 | Partial-startup SIGKILL spares untagged tool children | low | RJ | Disposable runner; nothing they hold blocks the exact down. |
| OBS2-1 | The tool killed its own native test host before admission closed (run 42587199919) | false | RJ | Orchestrator ruling requested by Step 03. The test host is the owner's test execution, not a composed resource or an attachment. BS-4's known-bad list governs signals from H and Q, which reached only the muxer and tool. |
| OBS2-2 | V2 exports `DOCKER_CONFIG` through `GITHUB_ENV` | false | RJ | Orchestrator ruling. The export follows every caller-supplied step, and an always() step removes the config. The loop-2 known-bad wording targets L and S, where test code runs after the export. |

**Outcome:** no intent_gap or bad_spec entry, so there is no loopback and `review_loop_iteration` stays 2. Nineteen patch groups (PC-1 to PC-19) go to the implementation agent; ECHP2-5 is deferred.

**Patches applied, 2026-11-09:** the implementation agent applied PC-1 to PC-19.
- Builds `194e87da` is on the new branch `fix/ci-v2-review-fixes-bde7fd4f`, because `fix/ci-v2-cancellation-and-cleanup-27145e52` was merged into `main` and deleted externally. No PR is open.
- Parties `11053e56` is on `fix/lifecycle-cancel-signal-566b9bfc`.
- The Platform edits are in the working tree.

The orchestrator re-verified at those revisions, in fresh worktrees:

| Check | Result |
| --- | --- |
| Builds helper suite | 68/68 |
| Builds `prepare-module-build` | 16/16, after a locked `npm ci` |
| Builds governed provenance | 95/95 |
| Workflow validator | legacy 411 and v2 302 assertions |
| Seven focused xUnit classes | 351/351 |
| Parties ci-lifecycle | 97/97, with the pinned Builds helper |
| Parties integration scripts | 46/46, with the pinned Builds helper |
| Platform composition tests | compile with 0 errors |

PC-15 (the PB-9 test) still executes only in Platform L/M delivery, which will also prove that its image-probe check runs before the first resource is added. Step 04 is complete.

### Iteration 3, 2026-11-11 (PC-1 to PC-19 fix-delta review)

Scope: only the iteration-2 patch delta.
- Builds `bde7fd4f..194e87da`: the helper, its tests, the validator, `CiRunLifecycleTests.cs` and `domain-ci-v2.md`. This delta is merged into `main` by `b5eb25c6`.
- Parties `4c0167b8..11053e56`, now on `main`.
- Platform `5cf3e8cb`: `PlatformAppModelTests.cs`, `test-environment-v1.md` and `deferred-work.md`.
- Excluded: the later Story 2.11 helper edits (Builds `f118c244`).

The combined diff is 640 lines (45 KB). Four layers ran: blind hunter (BH3), edge-case hunter (ECH3), verification gap (VG3) and acceptance auditor (AA3). The orchestrator verified claims read-only against Builds `194e87da`, Parties `11053e56` and Platform `HEAD`. It also ran two scratch-copy checks:
- **PC-18 mutation:** removing `and not timed` now fails its test.
- **Regex comparison:** the current `SECRET_TEXT` against the patch proposed in G4.

Verdict routes: **D** (decision needed), **P** (patch), **RJ** (reject).

| ID | Claim | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| AA3-1 | Helper timeout is now recorded as a cancellation | medium | D: G1 | On SIGINT, `ModuleCommandExecutionService.cs:1400-1412` sets `jobCancelled`. It then calls `RecordOwnerCancellationAsync`, which records `Cancelled` (`CompositionEngine.cs:616`), and withdraws holds as `ci-job-cancelled`. The later exact down also selects `ci-job-cancelled` (`:1428`). This contradicts PA-1 ("not finalized as `ci-job-cancelled`") and the contract (`test-environment-v1.md:362`, "cancelled only for an actual cancellation"). Only `execution.json` (`timedOut`) and exit 124 still say "timeout". |
| BH3-6 | `exact_down`'s `except OSError` lost its only test | medium | P: G2 | The rewritten missing-tool test empties `PATH`, so `verify_tool` fails first. Mutation (VG3): replacing the branch with `return 0` keeps 68/68 green. |
| AA3-2 | Same as BH3-6 | medium | P: G2 | Same mutation evidence. |
| ECH3-15 | Same as BH3-6 (deletion form) | medium | P: G2 | Same mutation evidence. |
| VG3-1 | Same as BH3-6 (pre-verified gap) | medium | P: G2 | Same mutation evidence. |
| BH3-7 | No test kills a tool that ignores SIGINT after a helper timeout | medium | P: G3 | Both timeout fakes exit on the first SIGINT, and no test patches `TIMEOUT_STOP_SECONDS`. Mutation (VG3): an unbounded `child.wait()` keeps 68/68 green. |
| VG3-2 | Same as BH3-7 | medium | P: G3 | Same mutation evidence. |
| BH3-1 | Keyword-first camelCase keys (`secretKey`, `passwordHash`, `tokenValue`) are not redacted | medium | P: G4 | Reproduced: the case-insensitive `(?![a-z])` rejects a following capital. Values shorter than 40 characters survive in retained excerpts. |
| AA3-6 | Same as BH3-1 | medium | P: G4 | Same reproduction. |
| ECH3-9 | Same as BH3-1 | medium | P: G4 | Same reproduction. |
| ECH3-16 | The commit claims camelCase keys are redacted | medium | P: G4 | Same reproduction. |
| VG3-O1 | Dropping the left boundary redacts `CancellationToken` stack frames | low | P: G4 | Reproduced: `CancellationToken cancellationToken) in /src/CompositionEngine.cs:line 626` becomes `Cancellation<redacted>`, which drops file and line. Same root cause as BH3-1: the boundaries cannot see camelCase. |
| AA3-3 | Docs say only the runner's kill can precede withdrawal | medium | P: G5 | The delta itself says SIGTERM ends the tool before withdrawal (`run_module_integration.py:954`). Yet on cancellation the helper sends SIGTERM at 6 s (at once on a second runner signal), kills at 8.5 s, and kills at 120 s after a timeout. `cancellation_settled` (`:929`) and `test-environment-v1.md:377-381` claim only the runner's kill can precede withdrawal. BS-4 requires the limit to be recorded honestly. |
| BH3-3 | `finalize`'s `verify_tool` guard catches fewer errors than the module | low | P: G6 | `document.get` on a non-object manifest raises `AttributeError`, which escapes with no `tool-unavailable` record and no retention. Elsewhere the module catches the six-type tuple. The mislabel half (a pin mismatch recorded as `tool-unavailable`) is rejected: it needs a tool change mid-job, and a new status adds a branch. |
| AA3-7 | Same as BH3-3 | low | P: G6 | Same evidence. |
| ECH3-3 | Same as BH3-3 | low | P: G6 | Same evidence. |
| BH3-4 | `run_down`'s second timeout drops stderr it already read | low | P: G7 | Reproduced on Python 3.14.4: the second `TimeoutExpired.stderr` still holds `b'down stalled: lock held\n'`, but the code replaces it with `b""`. The zombie and pipe part is negligible, because the helper exits soon after. |
| ECH3-5 | Same as BH3-4 | low | P: G7 | Same reproduction. |
| BH3-5 | `run_down` docstring is stale, and the lock guarantee no longer holds | low | P: G8 | The docstring at `:771` still says "kills that whole session". The lock half is false: `provisioning.lock` is the tool's own .NET `FileStream` lock (`CompositionEngine.cs:198`), opened close-on-exec. The muxer and the tool are in the killed group, so no exec'd descendant can hold it. |
| AA3-9 | Same docstring as BH3-5; PC-15 not executed; spec status | low | P: G8 | Docstring as above. PC-15 is already a recorded L/M gate. Correcting the spec status would edit the spec, so that part is rejected. |
| BH3-11 | Contract's CI recovery section not updated for this delta | low | P: G9 | `test-environment-v1.md:386-390` lacks the post-sweep receipt re-check, `tool-unavailable` and the timeout SIGINT, all of which `domain-ci-v2.md` now describes. |
| BH3-12 | `domain-ci-v2.md` controls list lacks the three new controls | low | P: G9 | `:255-264` lists none of: timeout-before-composed-stop, muxer-orphan late receipt, timed-out down with an escaped descendant. The "stray paragraph" half is false: Markdown joins lines `263-264` into one sentence. |
| AA3-4 | Contract still says the helper never signals composed processes | low | P: G9 | The sentence at `:377` sits next to "Without a receipt, it stops every other recorded-session process". This is PC-14's fix missing from the Platform copy. |
| BH3-10 | PC-16 `withdrawn` text conflicts with the runtime | low | P: G10 | Owner cancellation records `cancelled`, so a later attach is refused as `owner-cancelled`, not `owner-withdrawn` (`CompositionAttachAdmission.cs:342`). The text also omits expiry, which the policy lists as a cause of `withdrawn`. |
| AA3-5 | Same as BH3-10 | low | P: G10 | Same evidence. |
| ECH3-18 | Same as BH3-10 | low | P: G10 | Same evidence. |
| BH3-8 | PC-9 test effectively asserts the step finishes in under 5 s | low | P: G11 | With `started = now - 300` the job term binds and falls one-for-one with wall time, so `latest - earliest` is the step's duration. The bracket alone already pins the exact value. |
| ECH3-14 | Same as BH3-8 | low | P: G11 | Same evidence. |
| ECH3-10 | 3 s helper timeout can fire before the fake installs its SIGINT handler | low | P: G12 | The `muxer-apphost` fake starts four Python processes, then installs its handler (`:159`). Under load, a default SIGINT would kill it before withdrawal. |
| ECH3-11 | The new tests read `int()` from a marker that may still be empty | low | P: G13 | `wait_for` checks only that the path exists (`:282-284`), and `write_text` creates the file before it writes. |
| ECH3-13 | The descendant test can pass without exercising the new branch | low | P: G13 | Nothing asserts the escaped descendant was spawned. An empty marker in `finally` raises `ValueError`. Same root cause as ECH3-11: marker synchronization. |
| BH3-15 | PB-9 test no longer ties the probe to the bound servers | low | P: G14 | Probes iterate the servers after `PlatformPackageCatalogQualification.Bind` (`PlatformAppModel.cs:149-166`), but the test does not check the probed image. The sibling `PlatformPublishedPartiesMemoriesTransportTests.cs:46` asserts `PublishedImages.ShouldContain(...)`. The `daprd.exe` half is false: these package tests run on Linux, and the sibling uses `daprd`. |
| BH3-16 | Failed-cleanup-record test: misnamed helper, final evidence unchecked | low | P: G15 | `first_write_fails` now fails write 1 or 3. For write 3, nothing asserts that `cleanup-1.json` is left `completed` and unverified. |
| BH3-17 | Late-orphan subtest does not check the down's run id | low | P: G16 | `down.assert_called_once()` alone does not prove the exact down used the late receipt's owner. |
| BH3-2 | No-receipt sweep can signal composed processes of a receipt written late | low | RJ | Reachable only if the muxer died first and the tool writes its receipt and launches composed processes between the check and the first SIGINT. No attachment can be active before readiness. The fix re-evaluates the selection every round. |
| AA3-8 | Same as BH3-2 | low | RJ | Same reasoning. |
| ECH3-1 | Same as BH3-2 | low | RJ | Same reasoning. |
| ECH3-2 | A partial or foreign late receipt raises without a record | false | RJ | Carried ECHB2-15: refusing a malformed or foreign receipt before destructive commands is documented, and the result is non-success. |
| ECH3-4 | A descendant that escaped the group keeps `provisioning.lock` | false | RJ | The lock is the tool's close-on-exec `FileStream` (see BH3-5). |
| ECH3-17 | Doc: "no orphaned down keeps `provisioning.lock`" | false | RJ | Same as ECH3-4. |
| ECH3-6 | Muxer exits before the tool finishes its SIGINT withdrawal | maybe-false | RJ | Low if true: withdrawal takes under 1 s, and the finalizer sweep starts with SIGINT and allows 5 s. Settle by confirming that the dotnet CLI muxer ignores SIGINT while its child runs, as the `muxer-apphost` fake models. |
| ECH3-7 | Runner cancellation during the 120 s timeout stop | low | RJ | The handler still forwards: the second runner signal sends SIGTERM at once and the tool exits. The fix adds a loop branch for a window of a few seconds. |
| ECH3-8 | A suite stop timeout above 120 s is cut | low | RJ | Withdrawal comes before the tool's stops, and the exact down follows the kill. |
| ECH3-12 | Forked test child leaks if an assertion fails | low | RJ | Only on a failing run; the fix adds cleanup code. |
| BH3-9 | `CancelAfter` 60 s no longer guards the 5 s production allowance | low | RJ | PC-10 deliberately traded a load-sensitive wall-clock check for stability. The functional assertions (status `Failed`, `owner-failed` refusal) remain. A deterministic guard would be a redesign. |
| BH3-13 | Mocking all of `Q.HELPER` loses checking | false | RJ | The previous patch asserted nothing about `retain_artifacts` either. `partial_startup` uses only that attribute (AA3). |
| BH3-14 | Reader tests lack leak, pre-existing-path and symlink cases | low | RJ | The step prints nothing. `mkdir` and `O_EXCL` fail closed. `RUNNER_TEMP` is a real path on hosted runners. These would be new cases beyond PC-19. |
| OBS3-1 | `FINALIZE_RESERVE_SECONDS` omits the new 10 s post-kill wait and the third termination pass | low | RJ | The 30 s margin absorbs both: the finalizer's worst case is 235 s against a 240 s step. A fix would change the derived step and finalizer budgets (the same class as BHB2-6). |

**Outcome:** 1 decision-needed group (G1), 15 patch groups (G2 to G16) and no defers. 14 findings are rejected, plus four partial claims inside kept findings (listed below).

### Iteration 4, 2026-11-18 (closure delta review)

Scope, per the 2026-11-18 course correction:
- Builds `7afc4981..37e1a600`: the attach fallback, lifecycle-hold forwarding and their tests.
- Platform `8069fcdd..339af849`: the current-owner image admission and the restored Builds gitlink.
- The combined diff is 14 files (44.6 KB).

Three layers ran: blind hunter (BH4), edge-case hunter (ECH4) and verification gap (VG4). The orchestrator confirmed that Builds `main` CI runs [44260597208](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44260597208) and [44264165103](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44264165103) fail on one test, `IssuedOwnerStateAdmitsSameJobWithoutProcessGitHubVariablesAsync(process: "scrubbed")` at `AttachAdmissionTests.cs:210`, with a `cross-job` refusal. It also confirmed that `CheckContext` catches only `InvalidDataException` and that `AdmitAsync` calls it at line 81 without a guard.

| ID | Claim | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| BH4-7 | Issued-handoff tests depend on the ambient CI and handoff variables | high | P: G1 | Builds `main` CI fails the `scrubbed` case on hosted runners: the real GitHub identity wins, so the result is `cross-job`. |
| VG4-1 | Same as BH4-7 (pre-verified gap; the foreign-job test passes vacuously on CI) | high | P: G1 | On hosted CI the process identity decides, so the issued path is never exercised. |
| VG4-O1 | Builds `main` CI is red because of this test | high | P: G1 | Runs 44260597208 and 44264165103, 1 failure out of 3,166 tests. |
| ECH4-11 | Same as BH4-7 | high | P: G1 | Same evidence. |
| ECH4-13 | The unverifiable test leaves the ambient `HEXALITH_G4_*` handoff variables in place | low | P: G1 | Same root cause: ambient handoff variables change which branch runs. |
| ECH4-14 | The `NativeTestExecutorTests` negative hold assertion depends on an ambient `HEXALITH_CI_LIFECYCLE_HOLD` | low | P: G1 | Same root cause; the fix is to clear the variable before asserting. |
| BH4-6 | `AttachAdmissionTests` changes process-wide CI variables while running in parallel | medium | P: G2 | The class has no `[Collection(nameof(PlatformManifestTestGrouping))]`; every other class that sets CI variables is in that serial collection. |
| ECH4-12 | Same as BH4-6 | medium | P: G2 | Same evidence. |
| VG4-O2 | Same as BH4-6 | medium | P: G2 | Same evidence. |
| BH4-8 | No negative tests for the issued-state guards or for process-first precedence | medium | P: G3 | Same as VG4-2. |
| VG4-2 | Same as BH4-8 (pre-verified gap) | medium | P: G3 | Removing the `DeepEquals` check, the `VerifyIssuedContext` call or the role check, or returning issued state first, keeps every test green. |
| VG4-3 | The reader is never checked against the real plan and descriptor writers (pre-verified gap) | medium | P: G4 | `WriteHandoff` builds both files by hand. No more hosted proof runs are planned, so nothing else would catch drift. |
| BH4-1 | Non-`InvalidDataException` errors escape admission | low | P: G5 | A `JsonException`, `IOException`, `UnauthorizedAccessException` or `FormatException` from `GetInt32` escapes through `AdmitAsync:81`. The fix only widens a catch filter and uses `TryGetInt32`. |
| ECH4-1 | Same as BH4-1 | low | P: G5 | Same evidence. |
| ECH4-2 | A fault in an issued file aborts a complete-CI admission | low | P: G5 | Same root cause: only those exception types escape; `InvalidDataException` is already caught. |
| ECH4-7 | A fractional `workflowAttempt` throws `FormatException` | low | P: G5 | Same root cause. |
| ECH4-17 | A local admission can now throw | low | P: G5 | Same root cause. |
| BH4-2 | The fallback identity is not bound to the requested owner or store | low | RJ | Forging it requires writing consistent owner files on the job's own disposable VM, where every process is already in the same job. `VerifyIssuedContext` requires `github-hosted`. Binding would add parameters and could refuse a legitimate same-job attach to another owner. |
| ECH4-5 | Same as BH4-2 | low | RJ | Same evidence. |
| VG4-O3 | Same as BH4-2 | low | RJ | Same evidence. |
| BH4-3a | The catch branch accepts an issued `local` context for incomplete CI | false | RJ | In that case a verified handoff exists, so "incomplete CI without a handoff refuses" still holds. The context can only match a local owner. |
| ECH4-3 | Same as BH4-3a | false | RJ | Same evidence. |
| BH4-3b | The issued-read error is discarded, giving `cross-context` instead of `unverifiable` | low | RJ | It affects only the refusal reason. Surfacing it would refuse local attaches that succeed today. |
| ECH4-4 | Same as BH4-3b | low | RJ | Same evidence. |
| BH4-4 | `VerifyExecutingTool` skips a one-sided tool identity | low | RJ | It is reachable only with a partial or forged file. Tightening it would change refusal semantics without evidence that both writers always record the identity. |
| ECH4-8 | Same as BH4-4 | low | RJ | Same evidence. |
| BH4-5 | The reader re-implements validation (descriptor schema not checked, duplicate helpers, size checked before an unbounded read) | low | RJ | No reachable harm was shown; reusing the existing code would be a refactor. |
| ECH4-9 | A file can change between the checks and `ReadAllBytes` | low | RJ | The owner writes these files once, before the child starts. A bounded stream read is more than a direct correction. |
| ECH4-6 | A relative `resourceClaimStateDirectory` resolves against the working directory | low | RJ | The engine writes the absolute `_options.StateDirectory`; only a forged plan could be relative. The fix would add a guard. |
| ECH4-10 | Linked ancestor directories reject a genuine handoff | low | RJ | The issued path matters only for CI owners on GitHub-hosted Linux, where workspace paths have no linked ancestors. Other Builds readers apply the same rule. |
| BH4-9 | Nothing records which source supplied the attacher identity | low | RJ | The spec does not require it, and recording it would add public surface. |
| BH4-10 | The `HEXALITH_CI_LIFECYCLE_HOLD` constant has loose edges | low | RJ | Cosmetic. The exact `"1"` contract matches the Parties controller. |
| BH4-11 | The Platform change lost its refusal coverage | low | RJ | The new `!current` guard and the remaining refusals belong to the allowlist that Story 2.14 removes, so new tests would be thrown away. |
| BH4-12 | Two Platform tests got weaker | low | RJ | Same allowlist; Story 2.14 rewrites these tests. |
| ECH4-16 | The `platform-components` absence assertion was dropped | low | RJ | That validation now succeeds, so the refusal-path assertion no longer applies. Story 2.14 rewrites this test. |
| ECH4-15 | `HashSet` hides duplicate image probes | low | RJ | Duplicate probes are harmless, and the set equality still proves all six digests. |
| BH4-13 | `AssertCurrentOwnerMemories327Images` checks less than its summary claims | low | RJ | Every caller already asserts the Parties and EventStore `ProjectPath` (for example `PlatformCurrentOwnerPackageTests.cs:488-489`). |
| BH4-14 | The allowlist comment is not traceable | low | RJ | Cosmetic; Story 2.14 removes the allowlist. |
| BH4-15 | The diff does not identify the Builds commit or its release status | false | RJ | The scope above names `37e1a600`, and the closure record states it is unreleased. |

**Outcome:** five patch groups (G1 to G5), no defers and no loopback. 20 findings are rejected.

**Patches applied:** all five patch groups are in Builds `58e8a03fda27b5919760052cfa6da85e37eaa361`, on local branch `fix/attach-admission-test-isolation` from `origin/main` `eae4531a`. It is not pushed. The production change is limited to the widened catch filter and `TryGetInt32` in `TestEnvironmentDescriptorWriter.cs`.

Orchestrator check:
- In a clean environment, `AttachAdmissionTests` plus `NativeTestExecutorTests` pass 215/215.
- With a complete hosted-like GitHub identity set, `AttachAdmissionTests` passes 145/145. This is the condition that turned Builds `main` red.

The implementer's mutation checks fail on each guard named in G3 and on the G5 catch. Reverting only `TryGetInt32` stays green, because the widened catch already refuses that case.

### Iteration 5, 2026-11-18 (G1–G5 fix-delta review)

Scope: Builds `eae4531a..58e8a03f` only (3 files, 15.4 KB). Four layers ran: blind hunter (BH5), edge-case hunter (ECH5), verification gap (VG5) and acceptance auditor (AA5). The orchestrator re-ran `AttachAdmissionTests` plus `NativeTestExecutorTests` from the fresh `bin/Debug/net11.0` build: 215/215 both in a clean environment and with a hosted-like GitHub identity and `HEXALITH_CI_LIFECYCLE_HOLD=1`. On .NET 10.0.15, `Path.GetFullPath` throws `ArgumentException` for a path containing NUL.

| ID | Claim | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| AA5-1 | G4 is half done: `test-environment.json` is still hand-built, so the real descriptor writer never feeds the reader | medium | P: H1 | Only `plan.json` comes from `CompositionDocumentStore.Serialize`. VG5 found no other test that reaches `ResolveAttacherContext` from a written descriptor. A writer-side rename of `bindings.run.*` or `bindings.environment.executionContext` would break the Parties same-job attach with every test green, and no more hosted runs are planned. |
| AA5-3 | `ArgumentException` still escapes admission | low | P: H2 | A NUL in the plan's `resourceClaimStateDirectory` reaches `Path.GetFullPath` in `ReadDocument`. The filter does not list `ArgumentException`, and `CheckContext` catches only `InvalidDataException`. The issued read runs before the process check, so it also crashes complete-CI attachers. Only a forged plan reaches it, but G5's stated goal is unmet. |
| BH5-1 | Same as AA5-3 | low | P: H2 | Same evidence. |
| ECH5-1 | Same as AA5-3 | low | P: H2 | Same evidence. |
| ECH5-5 | The "unreadable metadata refuses" claim holds only for an incomplete process | low | P: H2 | Its NUL half is AA5-3. Its scrubbed-process half is BH5-2 (rejected). With a complete process identity, the process identity is used, which is correct. |
| AA5-4 | No test pins `TryGetInt32` on its own | low | P: H3 | The fractional row stays green with `GetInt32` restored, because the filter wraps `FormatException`. |
| BH5-5 | `FormatException` in the filter is dead | low | P: H3 | `TryGetInt32` removed the only `FormatException` source in the issued read. Deleting the clause makes the fractional row pin `TryGetInt32`. |
| ECH5-3 | Same as AA5-4 | low | P: H3 | Same evidence. |
| VG5-1 | The `IOException`/`UnauthorizedAccessException` arms are untested (pre-verified gap) | low | P: H4 | Narrowing the filter to `InvalidDataException or JsonException` keeps every test green. A descriptor held with `FileShare.None` then escapes `AdmitAsync`. |
| BH5-6 | Same as VG5-1, plus no test of a complete process identity with a malformed handoff | low | P: H4 | The IO half is VG5-1. The complete-process half is rejected: `CompleteProcessIdentityPrecedesIssuedOwnerStateAsync` already pins process-first precedence, and the remaining mutation is unlikely. |
| BH5-7 | The theory summary claims every issued-state guard | low | P: H5 | Its 7 rows cover the guards G3 named, the plan attempt binding and malformed JSON. About 13 other reader guards have no row. |
| ECH5-4 | Same as BH5-7 | low | P: H5 | Same evidence. |
| AA5-2 | The self-hosted row records a `runnerKind` the writer never produces | low | P: H6 | `ExecutionContext()` records `other` for any runner that is not `github-hosted`. |
| BH5-10a | `handoff` in `IncompleteProcessWithoutIssuedHandoffIsUnverifiableAsync` removes the handoff | low | P: H7 | The name says the opposite of what the line does. |
| BH5-8 | Joining the serial collection takes `AttachAdmissionTests` out of parallel execution | low | D: D1 | No assembly-level parallel setting exists, so the class ran in parallel before. It takes 78.8 s alone (145 cases), now added to every full run's serial phase. Only 5 methods change process variables. |
| AA5-5 | Builds `main` stays red until this commit lands | medium | D: D2 | Runs 44260597208 and 44264165103 fail on the test this commit fixes. Delivery needs owner approval, because Builds `main` pushes trigger releases. |
| BH5-2 | A scrubbed process drops `issuedError` and refuses as `cross-context` | low | RJ | Repeats BH4-3b: only the refusal reason is affected, and a CI owner still refuses. |
| ECH5-2 | Same as BH5-2 | low | RJ | Same evidence. |
| BH5-3 | The refusal message hides the cause | low | RJ | `CheckContext` is unchanged, and the refusal is correct. Surfacing the cause needs a new message design for rare failures. |
| BH5-4 | The mutation rows cannot tell which guard fired | low | RJ | Unmutated controls prove both fixtures read: same-job `incomplete` admits, and the foreign fixture gives `cross-job` under `scrubbed`. Each row then changes one field. |
| BH5-9 | `DefaultAttachContextMatchesProcessAsync` depends on ambient handoff variables | low | RJ | Untouched by this diff. It fails only with scrubbed GitHub variables plus an inherited G4 handoff, which Builds CI never has. |
| BH5-10b | `ScrubbedGitHub` and `IncompleteGitHub` repeat the variable names | low | RJ | It drifts only if `ExecutionContext()` reads a new variable, and the fix adds a shared list. |
| BH5-10c | The fractional row hand-copies the `Ci()` shape | low | RJ | It drifts only if `Ci()` gains a field, and the fix adds a parameter. |
| BH5-11 | Only `"1"` is tested for `HEXALITH_CI_LIFECYCLE_HOLD` | low | RJ | Outside this delta; same area as the rejected BH4-10. |

**Outcome:** two decisions (D1, D2), seven patch groups (H1–H7), no defers and no loopback. Eight findings are rejected, plus half of BH5-6.

### Review Findings

- [x] [Review][Decision] G1 (resolved 2026-11-11: accept as owner cancellation; see the iteration-3 owner decision): A helper timeout is now recorded as a cancellation.
  - PC-1 sends SIGINT on a helper timeout so that withdrawal comes before any composed stop.
  - The tool treats SIGINT as a job cancellation: the owner is recorded `cancelled` and holds end as `ci-job-cancelled`. This contradicts PA-1 and `test-environment-v1.md:362`.
  - The options are:
    - accept and document it as owner cancellation by the helper (this amends the PA-1 wording);
    - have the tool own the deadline, recording a failed outcome and `ci-job-completed` before it stops;
    - revert to SIGTERM, which reintroduces BHB2-1.
- [x] [Review][Patch] G2: Restore an `exact_down` launch-failure subtest. Patch `verify_tool`, make `run_down` raise `FileNotFoundError`, then assert exit 1, a `tool-unavailable` record that is unverified, and a retained `test-result.json` [Hexalith.Builds/Github/run-module-integration/tests/test_run_module_integration.py:1407]
- [x] [Review][Patch] G3: Test the helper-timeout kill. Use `ignore-all` with `TIMEOUT_STOP_SECONDS` patched to about 1 s, then assert 124, `processExitCode == -SIGKILL` and only SIGINT in the logged signals [Hexalith.Builds/Github/run-module-integration/tests/test_run_module_integration.py:937]
- [x] [Review][Patch] G4: Make `SECRET_TEXT` camelCase-aware: `(?i)(?<!cancellation)(…)s?(?-i:(?![a-z]))[^\n]*`. Extend the test with keyword-first keys and a `CancellationToken` stack frame that must keep its file and line [Hexalith.Builds/Github/run-module-integration/run_module_integration.py:70]
- [x] [Review][Patch] G5: State the withdrawal-ordering limit honestly. The helper's SIGTERM at 6 s (or at a second runner signal), its kill at 8.5 s and the 120 s timeout kill can end a tool that has not yet recorded withdrawal. Fix the `cancellation_settled` comment, `domain-ci-v2.md` and the contract [Hexalith.Builds/Github/run-module-integration/run_module_integration.py:929]
- [x] [Review][Patch] G6: Catch the module's full exception tuple around `verify_tool` in `finalize` [Hexalith.Builds/Github/run-module-integration/run_module_integration.py:749]
- [x] [Review][Patch] G7: Keep the accumulated `stdout`/`stderr` of the second `TimeoutExpired` in `run_down` [Hexalith.Builds/Github/run-module-integration/run_module_integration.py:788]
- [x] [Review][Patch] G8: Fix the `run_down` docstring to say "kills its process group" [Hexalith.Builds/Github/run-module-integration/run_module_integration.py:771]
- [x] [Review][Patch] G9: Sync the docs with this delta.
  - Contract: the post-sweep receipt re-check, `tool-unavailable` and the timeout SIGINT, and drop "never signalled by the helper".
  - `domain-ci-v2.md`: list the three new controls.

  [Hexalith.Platform/docs/contracts/test-environment-v1.md:377]
- [x] [Review][Patch] G10: Correct the `withdrawn` sentence.
  - `withdrawn` ends attached runs and holds on expiry or environment withdrawal.
  - An attach to an owner whose environment was withdrawn by owner cancellation is refused as `owner-cancelled`.
  - `owner-withdrawn` maps an owner terminal recorded as `withdrawn`.

  [Hexalith.Platform/docs/contracts/test-environment-v1.md:411]
- [x] [Review][Patch] G11: Delete the `assertLess(latest - earliest, 5)` timing assertion [Hexalith.Builds/Github/run-module-integration/tests/test_run_module_integration.py:1474]
- [x] [Review][Patch] G12: Raise the new timeout test's helper timeout from 3 s to 10 s [Hexalith.Builds/Github/run-module-integration/tests/test_run_module_integration.py:1488]
- [x] [Review][Patch] G13: Wait for non-empty marker content. Assert the escaped descendant was spawned, and guard `int()` in `finally` [Hexalith.Builds/Github/run-module-integration/tests/test_run_module_integration.py:1502]
- [x] [Review][Patch] G14: Assert that each probed image is a published catalog image, as the sibling transport test does [Hexalith.Platform/tests/Hexalith.Platform.Composition.Tests/PlatformAppModelTests.cs:620]
- [x] [Review][Patch] G15: Rename `first_write_fails`. For the third-write case, assert that `cleanup-1.json` is left `completed` with `verified: false` [Hexalith.Builds/Github/run-module-integration/tests/test_run_module_integration.py:1382]
- [x] [Review][Patch] G16: In the late-orphan subtest, assert that the down's `--run-id` is the late receipt's owner run [Hexalith.Builds/Github/run-module-integration/tests/test_run_module_integration.py:1502]

**Rejected (appendix):**
- BH3-2, AA3-8, ECH3-1 (low): narrow pre-readiness race; the fix re-evaluates every round.
- ECH3-2 (false): documented fail-closed refusal (ECHB2-15).
- ECH3-4, ECH3-17 (false): the lock descriptor is close-on-exec and held by the killed tool.
- ECH3-6 (maybe-false, low if true): settle the muxer's SIGINT handling.
- ECH3-7 (low): the handler still forwards SIGTERM; a narrow window.
- ECH3-8 (low): withdrawal comes first and the exact down follows.
- ECH3-12 (low): failing-run hygiene only.
- BH3-9 (low): PC-10's deliberate stability trade.
- BH3-13 (false): nothing was asserted before.
- BH3-14 (low): fail-closed and beyond PC-19.
- OBS3-1 (low): absorbed by the margin; the fix changes budgets.
- The `tool-unavailable` mislabel half of BH3-3, the stray-paragraph half of BH3-12, the `daprd.exe` half of BH3-15 and the spec-status half of AA3-9: rejected as noted in their rows.

**Patches applied, 2026-11-11:** G1 was resolved by owner decision (doc only), and G2 to G16 were all applied. Nothing has been pushed, merged or released.
- **Builds:** commit `f8dba239` (commitlint-validated) on local branch `fix/2-8-review-iter3-fixes`, cut from `main` `e588605c` (which contains `194e87da`), unpushed:
  - `run_module_integration.py`: G4, G5 comment, G6, G7, G8 and the G1 docstring.
  - `test_run_module_integration.py`: G2, G3, G4, G11, G12, G13, G15 and G16, plus controls for G6 and G7.
  - `domain-ci-v2.md`: G1, G5 and G9.
- **Platform working tree:** `test-environment-v1.md` (G1, G5, G9, G10) and `PlatformAppModelTests.cs` (G14).

| Check | Result |
| --- | --- |
| Builds helper suite | 71/71 |
| Builds governed provenance | passed |
| Workflow validator at current `main` | legacy 312 and v2 206 assertions (the restructured validator; no validator file changed) |
| Platform composition tests (`-nodeReuse:false`) | compile with 0 warnings and 0 errors, fresh DLL |
| xUnit | not rerun; no C# source changed |

The G2, G3 (kill dropped, and SIGTERM on timeout), G4 (iteration-2 regex), G6 and G7 mutants each fail at least one test; G2 and G6 do so through the missing-tool test. G14 runs only in Platform L/M delivery, like PC-15.

**Iteration 5 (2026-11-18), Builds `58e8a03f`:**

- [x] [Review][Decision] D1 (resolved 2026-11-18: keep serial; no change): Keep `AttachAdmissionTests` serial, or split it — Joining the serial collection fixed the variable race (G2), but it adds the class's 78.8 s to every full run's serial phase. Only 5 methods change process variables. The options are to keep it (consistent with the 26 classes already in the collection), to move those 5 methods into a small serial class, or to defer to Story 2.10's feedback-time scope.
- [x] [Review][Decision] D2 (resolved 2026-11-18: deliver through a PR after the patches; merge needs a separate owner yes): Deliver the fix to Builds `main` — Builds `main` CI stays red until this commit lands. Pushing to `main` triggers a release, so delivery needs owner approval.
- [x] [Review][Patch] H1: Add one same-job admission test whose `test-environment.json` is written by `TestEnvironmentDescriptorWriter.WriteAsync` under a complete `job-a` GitHub identity, then admit under `ScrubbedGitHub()` [test/Hexalith.Builds.Module.Tests/AttachAdmissionTests.cs:1938]
- [x] [Review][Patch] H2: Add `ArgumentException` to the issued-read filter, and add a `state-directory-nul` row [src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentDescriptorWriter.cs:280]
- [x] [Review][Patch] H3: Delete the dead `FormatException` clause, so the fractional row pins `TryGetInt32` [src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentDescriptorWriter.cs:280]
- [x] [Review][Patch] H4: Add a `descriptor-locked` row that holds the descriptor with `FileShare.None` during admission [test/Hexalith.Builds.Module.Tests/AttachAdmissionTests.cs:241]
- [x] [Review][Patch] H5: Narrow the theory summary to the guards it actually covers [test/Hexalith.Builds.Module.Tests/AttachAdmissionTests.cs:241]
- [x] [Review][Patch] H6: Use `runnerKind: "other"`, the value the writer records, in the self-hosted row [test/Hexalith.Builds.Module.Tests/AttachAdmissionTests.cs:258]
- [x] [Review][Patch] H7: Rename the handoff-clearing variable to `noHandoff` [test/Hexalith.Builds.Module.Tests/AttachAdmissionTests.cs:235]

**Rejected (iteration 5 appendix):** BH5-2 and ECH5-2 (low; repeats BH4-3b, refusal reason only), BH5-3 (low; unchanged correct refusal), BH5-4 (low; unmutated controls prove both fixtures), BH5-9 (low; untouched, and Builds CI never reaches it), BH5-10b and BH5-10c (low; speculative drift, and the fix adds surface), BH5-11 (low; outside the delta, BH4-10), and the complete-process half of BH5-6 (low; precedence is already pinned).

**Iteration 5 patches applied, 2026-11-18:** Builds `main` had meanwhile gained `88d93a29`, another session's minimal scrubbed-case fix. The fix branch was rebased onto `origin/main` `db7bb717`, which left a duplicate `ScrubbedGitHub()` that was then removed. H1–H7 were folded into one commit, `13e6e795` (`fix(runtime): harden issued-handoff attach admission`, commitlint-valid). The pre-rebase commit is kept as local ref `backup/58e8a03f`.

- **Delivery:** branch `fix/attach-admission-test-isolation` pushed; [Builds PR #191](https://github.com/Hexalith/Hexalith.Builds/pull/191) opened. It is not merged, because the merge triggers a Builds release and needs a separate owner yes.
- **Checks:**
  - Analyzer-on build: 0 warnings, 0 errors.
  - `AttachAdmissionTests` + `NativeTestExecutorTests`: 218/218 clean and 218/218 hosted-like (complete GitHub identity plus `HEXALITH_CI_LIFECYCLE_HOLD=1`).
  - Mutants: dropping `ArgumentException` fails `state-directory-nul`; restoring `GetInt32` fails `fractional-attempt`; dropping `IOException` fails `descriptor-locked`; a reader that rejects the real writer's `attemptSequence` fails only `WrittenOwnerHandoffAdmitsScrubbedSameJobAttacherAsync`.

### Demonstrated owner-source gate blocker — 2026-11-11 continuation

Builds draft [PR #181](https://github.com/Hexalith/Hexalith.Builds/pull/181) carries exact iteration-3 commit `f8dba23982fb1599a37c543b41439dce52c0dd19`. Its branch push and PR creation are within the existing hosted-proof authorization; no merge, publication or root pin change occurred. The GitHub connector refused PR creation with HTTP 403; the existing repository CLI connection created it after confirming that no PR already existed.

The owner-source gate has a demonstrated prerequisite failure already present at base `e588605ccf2c6577a2490e1a3fc512f8ffdc3960`: Builds CI run `43006363999`, job `130474898734`, fails four `SelectedRoutingCatalogSnapshotTests.CapturedRoutingProofRequiresTheValidatedOwnedInstanceAsync` rows. `SelectedRoutingCatalogSnapshot.ValidateDescriptor` now requires `ownerRunId`, `runId` and `role`, but that test's `CreateDescriptor` writes only `environmentId`. The positive rows (`valid`, `near-limit-valid`, `schema-properties`) and the cancellation row fail before reaching their intended assertions. This is an actual owner delivery-gate blocker, not a new release or enrollment prerequisite.

- [x] In Builds `test/Hexalith.Builds.Module.Tests/SelectedRoutingCatalogSnapshotTests.cs`, supply the real plan's owner/run identities and the `owner` role in `CreateDescriptor`, preserving every mutation and refusal assertion. Keep production validation unchanged. Run the existing complete class using resource-free filesystem fixtures only; verify all positive, cancellation and negative rows execute and pass. Commit the minimal fixture correction on the same review branch with the locked commitlint policy, and retain the original failed source outcome.
- [x] Inspect the resulting PR source checks at their exact head. Final Platform tuple execution and the patched-release lifecycle gate remain open; no merge/publication or main pin-change authority is added.

### Implementation continuation — 2026-11-11 focused verification and hosted selection

The [local verification receipt](evidence/2-8/verification-20261111/results.json) records exact sources, commands, results, file hashes and remaining gates. The implementation role made no remote changes. All 17 root gitlinks are preserved, and these checks provide no hosted, candidate or tool-acceptance credit.

- Builds fixture correction: `b74b467ac5ce5d9f9fc440f43e279b7093b539de`, parent `f8dba23982fb1599a37c543b41439dce52c0dd19`, on `fix/2-8-review-iter3-fixes`. Only `CreateDescriptor` changes: `ownerRunId` and `runId` use `plan.RunId`, and `role` is `owner`. Production validation and every existing mutation/refusal assertion are unchanged. Locked commitlint 21.2.4 passed before the commit. The original failed source outcome above remains failed.
- The [complete routing-snapshot class](evidence/2-8/verification-20261111/routing-snapshot-tests.xml) passed **34/34**, with zero failures, errors, skips or not-run cases: all 32 theory rows and both facts, including the three positive rows and cancellation. The class uses temporary files, directories, symlinks, JSON, cryptographic values and cancellation only. Its `Process` is never started; the readiness URI is never contacted; `ArchiveSelectedAsync` writes snapshots only.
- PA-15/PB-9/PC-15/G14 now has an executable hosted selection. The current workflow actually runs `eng/tuple-ci-sdk42-current-suites.json`, so the required package-binding method is added there. Its exact required total increases **456 → 457**; `eng/run_tuple_ci.py` and the existing suite-selection control agree. All previous cases and all historical/other profiles remain unchanged. The control also refuses a same-count substitution of the required method. This enrolls the test; its actual execution remains a Platform L/M gate.

| Focused check | Observed result |
| --- | --- |
| Builds helper at `f8dba239` | 71/71 |
| Builds governed provenance | 90 tests and 105 subtests passed |
| Builds governed-release controls | 34 tests and 270 subtests passed |
| Workflow validator | legacy 312 and v2 206 assertions passed |
| Tuple suite-selection controls | 8/8 |
| Real-legacy/pre-extension source fixture controls | 20/20 and 9/9 |
| Builds module-test project compilation | 0 warnings, 0 errors |
| Complete routing-snapshot class | 34/34 |
| Platform package-mode Release compilation | 0 warnings, 0 errors |

The initial Platform Debug `--no-restore` compilation failed with CS1704 because cached source assets mixed EventStore 4.1.0 and 4.2.0 in Tenants.Contracts and Memories.DomainService. Its [original log](evidence/2-8/verification-20261111/platform-composition-build.log) is retained. The focused package-mode Release fallback then compiled the composition test project successfully; it changes no dependency or gate and grants compilation credit only.

The exact-head PR source checks, normal review/delivery, actual package-binding test execution, final current/real-legacy/five-SDK L/M checks and the patched-release lifecycle cancellation gate remain open. Story 2.8 stays in progress; Story 2.10 qualification and candidate acceptance remain independently gated.

### Supplemental delivery-delta review — 2026-11-11

Three context-free reviewers examined only the new current42 selection and the routing-snapshot fixture correction: `/tmp/story-2-8-new-delivery-delta-xpj1vvdw.diff`, 4,902 bytes, SHA-256 `573cdc1e7d3a8b7ffa05a674a71e159ccb39e3bf48b1a12ffe2aef269c340e06`. The edge-case reviewer returned no findings; the verification-gap reviewer found no gaps. The blind reviewer calculated a finding floor of three and returned the three claims below. This is an incremental review under the approved audit amendment; it neither reopens prior triage nor grants final hosted acceptance.

| ID | Claim | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| SD-BH-1 | The Builds fixture change has no owning commit or updated Platform pin, so a fresh Platform checkout retains it unfixed. | false | reject | The owning commit is `b74b467ac5ce5d9f9fc440f43e279b7093b539de`, pushed to Builds PR #181 and tested 34/34. Root pins intentionally remain unchanged until the separately recorded delivery boundary. Updating main pins during this proof phase would violate that boundary; this scoped review diff is not a final enrollment proposal. |
| SD-BH-2 | The repaired fixture supplies valid owner/run/role values without negative cases for each of those fields. | low | defer | Those direct owner-descriptor mutation cases are absent in this class and predate this fixture repair; the production checks came from incoming attached-lifecycle work `bfda07eea7dcc8ef0a6da0fa14643aa70eebec78`. `NativeTestExecutorTests.AttachedRoutingSnapshotsRequireExactOwnerAsync` already covers a foreign attached owner, but not every owner-descriptor field. The repair restores existing positive/cancellation cases without weakening a guard. Further mutation coverage belongs to the owning identity-validation work. |
| SD-BH-3 | The current profile omits `UnqualifiedPackageCatalogCompositionRefusesBeforeContainersOrSidecarsAreAdded`. | low | defer | This method was absent before the new positive binding selector, and this change removes no negative selection. The profile already selects `PlatformPackageCatalogEntryPointTests` (four invalid-binding cases assert zero configuration/probes/resources) and `QualifiedPackageCatalogUnavailableImageRefusesBeforeAddingResources`. Selecting the additional direct-Compose refusal is a pre-existing coverage improvement, recorded separately. |

The two coverage improvements are appended to `deferred-work.md`. There is no new blocking finding or patch. Exact-head source CI and every previously recorded delivery gate remain required.

### Delivery checkpoint — 2026-11-11

Builds draft [PR #181](https://github.com/Hexalith/Hexalith.Builds/pull/181) is now at `b74b467ac5ce5d9f9fc440f43e279b7093b539de`. Both exact full commit messages passed the owning locked commitlint CLI before their branch pushes. Hosted source CI is [run 43014505006](https://github.com/Hexalith/Hexalith.Builds/actions/runs/43014505006); its outcomes must be read at this exact head before delivery. The earlier `f8dba239` source run is superseded by a genuine fixture change, not retried unchanged, and supplies no final-source credit.

The [parent incremental audit](evidence/2-8/verification-20261111/parent-audit.json) independently checks the 34-case primary XML, the eight tuple controls, all 16 verification bodies, both older review manifests, the archived history and all original frozen/task/acceptance bytes. It records the whole-prefix hash check discrepancy and its resolution (the mutable workflow counter and approved replacement handoff heading); no historical body was rewritten. All 17 root pins remain unchanged. Two feature-branch pushes, draft PR creation and one PR body update were performed; there were no main pushes, merges, tags, publications, manual workflow dispatches or cancellation requests.

The recorded **hosted proof, no merges** boundary remains in force. Finish the PR source checks and report the concrete result before any new normal delivery/publication authorization. Keep this story `in-progress`: the enrolled package-binding test has compiled but still must execute in final L/M, the final exact-source tuple/SDK gates remain open, and the patched-release lifecycle cancellation gate remains separate. Do not mark Step 05 or Story 2.8 complete from local checks or PR source checks alone.

**Checkpoint completed, 2026-11-11 12:07 UTC:** all ten jobs in source CI `43014505006` attempt 1 passed for exact PR head `b74b467ac5ce5d9f9fc440f43e279b7093b539de`, including Release/tool qualification and catalog authority. The [source-check receipt](evidence/2-8/verification-20261111/pr181-source-checks.json) records every job and check; the auxiliary CodeQL status is neutral, while the actual CodeQL analysis passed. The [primary ordinary-source .NET summary](evidence/2-8/verification-20261111/hosted-source-dotnet.json) records 3,052 module passes, zero failures and four contextual skips identical to the base. Its disabled live cases are not reference or lifecycle credit. The [six-row continuation index](evidence/2-8/verification-20261111/row-index-continuation.json) maps the touched regressions without changing any earlier subject or acceptance classification.

The [normal-merge proposal](evidence/2-8/verification-20261111/normal-merge-proposal.json) and its exact full commit message are prepared and commitlint-validated; owner approval is still absent. Git confirmed Builds main advanced independently to `5050f01a74cd6ba22954030c852a097cffb2b5b8` through publisher-only work. Those changes are preserved, the PR head stays fixed, and any actual merge needs fresh main CI before separately permitted publication. The [continuation manifest](evidence/2-8/verification-20261111/continuation-manifest.json) binds the completed evidence. No mutation is queued or running. Resume from the owner delivery decision for PR #181, preserving all remaining final Platform and patched-release lifecycle gates.

### Owner merge request and observed completion — 2026-11-11

The user replied **“merge to main”** to the concrete PR #181 merge question. Fresh inspection found that an external `/pushall` had already merged it at **12:18:43 UTC**, actor `jpiquot`, and GitHub now marks PR #181 merged/closed. No ready transition, merge request, main push or publication was replayed by this turn.

Direct Git fetch and ancestry checks verified actual main merge `213aab34be1ce97783a51ebeaf3d2d0c5dab6dd7`, tree `586cd76f23e5155992760186017dd85441516080`, with ordered parents `5050f01a74cd6ba22954030c852a097cffb2b5b8` and `b74b467ac5ce5d9f9fc440f43e279b7093b539de`. Both the iteration-3 fixes and fixture repair are ancestors. The actual full merge message is the external `/pushall` message, not the earlier prepared PR merge message; the owning locked commitlint CLI accepted its exact Git bytes. The [merge receipt](evidence/2-8/verification-20261111/observed-main-merge.json) and [separate manifest](evidence/2-8/verification-20261111/merge-observation-manifest.json) preserve that distinction.

Fresh main [CI 43019233527](https://github.com/Hexalith/Hexalith.Builds/actions/runs/43019233527) at this exact merge was **in progress** when observed; main commitlint `43019233552` passed. The earlier successful PR CI remains scoped to its own head and does not pass this main run. Root Platform pins and pending changes remain intact. The merge request is fulfilled; Story 2.8 stays in progress pending main CI and the previously recorded publication/final Platform/lifecycle gates. The merge request adds no source-publication authorization. Do not replay PR #181 delivery or start a release from an assumed version.


### Delivery reconciliation and producer boundary — 2026-11-17

The [finite continuation evidence](evidence/2-8/verification-20261117/README.md)
closes the effective final-source task at its actual delivered subject,
Platform `32643bde39b4da0e4538f8388e225727f82122f6` / 2.0.0: tuple
`43391842440`, all six SDK catalog jobs in `43391842426`, and publisher
`43396965519` succeeded. The original named PA-15/PB-9/PC-15/G14 case actually
passed in the retained primary XML collected at
`b05616cc57c16b152a364d5a4d0382782b99332f` with .NET 10/tool 4.47.0. Relevant
production bytes match the delivered source; later Story 2.13 retirement and
the separate current-root tuple failure remain explicitly attributed. Builds
merge CI `43019233527` remains cancelled; later genuine delivered descendants
supply their own source/publication receipts. No earlier failure is relabelled.

Five focused patched-release attempts (`44051836412`, `44053412339`,
`44062720071`, `44066243616`, `44073347507`) remain failed, each with zero
cancellation requests. The final attempt passed all 15 isolated projects,
then refused the ordinary package handoff before native execution. Both exact
cleanup attempts verified the same durable owner with empty inventories; this
supplies no cancellation or three-timestamp ordering credit. The task-owned
final proof ref was verified at
`bccab615f9e3d1a92e3bbf648ee89e78a925670b` and deleted. The novel bounded
MSBuild-worker isolation fix and actual direct/Python worker regression remain
as a separate [two-path owner proposal](evidence/2-8/verification-20261117/owner-ready-fix/proposal.json)
against `8abfd266eecb5a636ee5498f944304669c18aa41`; normal owner review/delivery
is pending and no current-owner qualification is inferred from its apply check.

One finite genuine older holds-only subject,
`11053e562a2cba0196354cec655c01677e2afd46`, passed both real ordinary
parent-handoff/cohort metadata guards with released CLI 4.48.2 and separate
executor `6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b`. Catalog
`3c8096180ecf2aa539ac454815efbfceb8a92822`, Platform
`0f4e27f1f78310bd731c10c19dc51bb1e0345d44`, actual native/declaration bytes,
15 isolated projects and original 60/45-minute budgets stay intact. The
[prepared nine-path projection](evidence/2-8/verification-20261117/ordinary-proof-4/projection.diff)
authenticates its executor before controller imports. Controller 97, released
helper 75, observer 3 and owning workflow class 70 controls passed; no local
AppHost, builder, service or probe was executed. Original harness failures and
unexecuted blocked cases remain recorded.

The actual test-project build now fails `NU1903` for SignalR Redis 10.0.14.
The [concrete 10.0.15 producer patch and local validation](evidence/2-8/verification-20261117/producer-boundary.md)
correctly reach `HXW009`: the unchanged Platform source pins the original
catalog, so a substituted catalog cannot claim its identity. The recommended
owner decision is a bounded supported-4.x catalog maintenance and matching
Platform source/publication path with all normal gates, followed by a genuinely
refrozen caller. No version/tag is guessed. Audit and pin guards remain enabled; further
dispatches remain stopped at this producer decision. Proper current-cohort lifecycle
registration is a separate, broader capability/qualification alternative.

The [finite task/row audit](evidence/2-8/verification-20261117/finite-task-and-row-audit.json)
preserves all six original matrix rows and their original result subjects.
The focused released-tool execution cancellation, durable admission-close /
withdrawal / first composed-stop ordering with kernel signal origins, and
normal story review remain open. Status remains **in-progress**; Step 04/05,
Story 2.10 qualification and candidate/tool acceptance are not completed.
The unrelated incoming root commit
`4f88df0ec98885425db5ddb20a3a3bb34a93d046` is preserved without assigning it
previous test outcomes. All 18 current root gitlinks and the frozen block are
unchanged. All disposable jobs/monitors finished; no cancellation or remote
mutation is queued.

### Owner approval — 2026-11-17 maintenance continuation

The user replied **“approve”** to the concrete request to proceed with the
[catalog-maintenance and matching Platform release path](evidence/2-8/verification-20261117/producer-boundary.md)
through its normal gates. This authorizes the bounded supported-4.x catalog
security maintenance and corresponding Platform source/handoff/publication work,
including necessary owner branches, PRs, normal delivery and publication after
their required checks. It supersedes the previous no-publication boundary only
for this approved path; do not ask again for that same authorization.

Resume Step 03 of the already-rendered build workflow. Preserve the original
baselines, frozen intent, all prior outcomes and the shared root's 18 current
pins. Resolve the maintenance lane and version from the owning release policy,
actual refs and publisher capabilities; do not invent a tag or bypass normal
source, package, provenance, audit or publication checks. Prepare and review the
concrete changes before delivery. The one-pin SignalR patch remains the starting
change, with matching genuine catalog/Platform identities required before a
refrozen caller can execute. Record every new source and publication receipt.

After the producer inputs exist, resume the admitted holds-only focused proof
with its original suites, budgets, ready-artifact verification and one normal
cancellation, followed by exact cleanup and the three-timestamp/kernel-origin
audit. No current-cohort lifecycle registration, Story 2.10 qualification,
candidate/tool acceptance or shared-main accepted-pin adoption is authorized by
this maintenance decision. Story 2.8 remains in-progress until the existing
implementation, runtime and normal review gates actually complete.

### Maintenance preparation — 2026-11-17

The [reviewed local proposals and verification](evidence/2-8/maintenance-20261117/phase-01-local/parent-pre-delivery-review.json)
retain schema 1's signed archive, executable, source, catalog and inventory
binding. The actual supported maintenance policy names Builds `4.44.x` from
`3c8096180ecf2aa539ac454815efbfceb8a92822` and Platform `1.7.x` from
`0f4e27f1f78310bd731c10c19dc51bb1e0345d44`. Its owner proposal is
`3c285357c9eadec74a3516d649c8197fe1719052`; no delivery is claimed yet.

Actual restore controls show that Redis 10.0.15 also requires Options and
DependencyInjection.Abstractions 10.0.15. Those three changes are the complete
verified minimal catalog closure. The locked analyzers derive 4.44.14 and
1.7.2 from genuine committed proposal ranges; no tag was created. The Platform
proposal still retains its original tuple data until genuine new publication
identities exist.

The 12 Builds guard, 22 Platform guard, six independent public-route/recheck
and 17 owner write/environment controls passed. The first complete owner global
invocation failed because its API token used the audit-only variable and its
disposable clone retained a local origin. Both normal API checks and all 17
correct-origin controls subsequently passed at the unchanged source; the
original aggregate failure remains recorded. Other global subchecks and
retained tool suites passed. This is local verification, not hosted source CI,
publication, runtime cancellation or story acceptance.

All 18 shared root pins/checkouts and the frozen intent remain unchanged.
Continue normal owner delivery and exact-source producer gates under the
existing approval, then refreeze the focused proof. Step 04/05 and runtime
ordering acceptance remain open.


### Maintenance owner delivery and producer corrections — 2026-11-17

The [phase 02 checkpoint](evidence/2-8/maintenance-20261117/phase-02-owner-and-ci/checkpoint.json)
records the normal CiCd PR #1 merge at
`93eb23114d4ca41b92c3dccf21c8e1baa9c5870e`, preserving the independent incoming
AI.Tools change. Both reviewed-source CI `44100791669` and actual merge push CI
`44106588862` passed. Frozen owner policy
`3c285357c9eadec74a3516d649c8197fe1719052` is a genuine ancestor. The normal
protected-environment API added only the named `4.44.x` branch; existing
review/protection settings remained byte-bound and unchanged. Normal NuGet
Trusted Publishing is still required; no policy/key fallback was installed.

Builds PR #188 now names `3aafeda8ed2b2befa0f49c78fe0b5c88c79d7f1c`.
Its first hosted source run `44110224042` remains failed. The normal audit
generator refreshed only the dotnet-10 family, preserving all 155 other
families and every non-dotnet-10 package observation. The three actual pins
remain the complete verified Redis/Options/DI.Abstractions closure. Two
branch-list assertions now include the approved maintenance lane. Four actual
MSBuild failures were independently reproduced: build correctly refused the
first conflicting source flag/root with HXW006, while the old assertion
expected Configuration despite the CLI forwarding it globally. The corrected
four cases executed and passed, retaining configuration/evaluation assertions
and failure requirements. An intervening SA1515 failure is retained, then
corrected with the required blank line. Locked full-message validation,
dependency signatures and version analysis passed; computed version remains
4.44.14. Exact corrected-head PR CI `44117921876` is pending.

Builds default-branch registration PR #187 adds the actionless manual
registration declaration. Its prior exact-head run `44109440725` failed two
publication fixtures that were already failing on its genuine main base.
The [focused fixture repair](evidence/2-8/maintenance-20261117/phase-02-owner-and-ci/registration-fixtures-02/source-delta.log)
retains the authenticated original payload maps and producer artifact identity,
then asserts the actual public payload map and signed archive hash. Both
previously failing tests passed, with no production-publication change.
The reviewed/commitlint-validated continuation is
`6c622123470b65cefbfc2326aeeee34a89b597a2`; its fresh normal PR CI is pending.
Superseded automatic cancellations are not test passes or user cancellation
requests.

Platform support proposal remains
`ad32547462a746945631c35cb994bcdb8f53d0ed`, with all 42 retained/new release
controls passed and original tuple data retained until a genuine Builds
publication exists. Platform registration PR #43 has a one-review/code-owner
repository rule and the independently observed current-root tuple failure.
Do not bypass either or adopt current pins to repair this maintenance path.
Its actual necessity will be resolved through the genuine qualified
maintenance-ref dispatch; the workflow filename already exists on default main.

The phase 02 manifest binds 260 files (SHA-256
`3c88b15044ee3f6f6a7b668ca82368b1561b99b11d6a3d0723d5b59af78fdfbc`).
Incoming root `24f8ebeba25a36e043942e7f1a76583724b27fef`, all 18 root
pins/checkouts and the original frozen intent are preserved. There are no new
tags/releases, no source-publication or runtime-ordering acceptance, and no new
cancellation requests. Step 03 remains in progress.


### Builds maintenance merge and runner-capacity boundary — 2026-11-17

The [phase 03 checkpoint](evidence/2-8/maintenance-20261117/phase-03-builds-source-merge/checkpoint.json)
records all seven exact PR-source CI jobs passed in run `44117921876`, then one
normal merge of PR #188 at `51d56ec722e0e66c8957f2f7502015f2c9b7c5c1`.
Its actual parents, reviewed tree and complete actual message were independently
verified. The GitHub-formed message omits the proposed terminal newline; both
forms were commitlint-validated before the request, and the actual Git object
message was validated afterwards. The merged-source push CI `44121371373` is
still required and in progress. CodeQL, Bugbot, security and Sonar passed;
auxiliary Codacy remains action-required with no annotations and is not
relabelled passed.

The ordinary .NET job executed 2,856 module tests, 191 evidence tests and one
integration test successfully. Five contextual module and 12 opt-in/live
integration skips remain skips and supply no lifecycle credit. The primary PR
qualification artifact's official digest/CRC were verified. It belongs to the
synthetic PR merge checkout `e0c74dd62258e0114eac75466231910ddaad3338`, records
source validation skipped/not-run and releaseEligible:false. It is validation
only; normal actual-version publication must produce its own genuine inventory.

Builds registration run `44118275946` now passes both Python lanes and catalog
contracts. Its macOS job `134163397914` executed zero steps: GitHub records that
the hosted arm64 runner was never acquired because of capacity constraints.
The old publisher registry entry remains deleted, so normal default-branch
registration is genuinely required. A concrete one-job normal retry exception
was requested from the user because the repository instruction forbids
unchanged-source retries. That exception is pending; no retry was requested
from GitHub, no source change was fabricated, and the cancelled job is not a
pass. Continue independent merged-source checks while waiting. There are no
new tags/releases or lifecycle cancellation requests. Step 03 remains open.


**Runner-capacity exception approved:** the user selected **“Retry the macOS job
once (Recommended)”** for run `44118275946`, job `134163397914`, at unchanged
source `6c622123470b65cefbfc2326aeeee34a89b597a2`. This authorizes one normal
job retry after the current run finishes. Preserve the never-acquired original
failure; no second retry, test skip, fabricated source change or runtime/gate
waiver is authorized. Do not ask again for this specific exception.


### Registration delivered; maintenance carrier correction — 2026-11-17

The [phase 04 checkpoint](evidence/2-8/maintenance-20261117/phase-04-registration-and-carrier/checkpoint.json)
records the one explicitly approved normal macOS retry. It acquired a runner,
executed its actual steps and passed as job `134183570648`. Run `44118275946`
completed successfully at attempt 2. The original never-acquired job remains
cancelled; normal GitHub reuse of unchanged-source successful jobs is recorded
separately. This specific exception is spent; no second retry is authorized.

PR #187 merged normally at `7afc4981e21bc2fcce687199a3fd2c61fe87336e`.
A first operator preflight made zero merge requests because it incorrectly
assumed current main used ci.yml. Reading its genuine source and registered
workflow established builds-ci.yml / `411848291`, while the supported old lane
uses ci.yml / `320718206`. The corrected preflight, actual merge tree, parents
and complete actual message passed verification. The legacy publisher registry
`152656352` is now active. Its new default-main CI `44125038640` is separate
from the supported maintenance source and is not relabelled complete.

Maintenance push CI `44121371373` remains failed: .NET, catalog, Python and
CodeQL passed; complete package qualification reported producer success, then
carrier retention rejected the new maintenance workflow ref. The failed
artifact's official ZIP digest/CRC and original producer identity were
verified. All recorded identity fields match the actual source/job/run; the
old carrier whitelist alone omitted refs/heads/4.44.x. No publication was
dispatched from this failed source gate.

The bounded two-path correction `4a7e0d22e79aeb26f73fd4b147cd4edece8e4f26`
is in [Builds PR #189](https://github.com/Hexalith/Hexalith.Builds/pull/189).
It admits only the exact named maintenance branch and constrains maintenance
release carriers to stable 4.44 versions, preserving every other identity,
source, eligibility, byte/hash, path and atomic-write check. All 16 carrier
methods, including the real CI/release archive/verify round trips and strict
negative cases, passed; all 15 source-gate controls passed. Locked dependency
signatures/full commit message and actual version analysis passed; derived
version remains 4.44.14. Exact-head PR CI `44125853269` is pending; normal
merge and fresh merged-source CI remain necessary before publication. The
remaining executed publisher path was checked for branch restrictions; the
original old carrier omission is now explicitly covered.

All 18 shared root pins/checkouts and frozen intent are unchanged. There are
no new tags/releases, publication dispatches, lifecycle cancellation requests,
source acceptance, current-cohort registration or Story 2.10 qualification.
Continue Step 03 through the existing maintenance authorization; do not ask
again for the already-spent single macOS retry or the approved publication path.


### Source CI complete; NuGet maintenance policy and one retry — 2026-11-17

The [phase 05 checkpoint](evidence/2-8/maintenance-20261117/phase-05-source-ci-and-publishing-policy/checkpoint.json)
records normal PR #189 merge `979cfd3e2c4145be516fbd833dacfc6b8e8fdce5`,
all seven PR CI jobs passed in `44125853269`, and its genuine push CI
`44129245136` attempt 1 successful. Six jobs passed; the contextual dependency
review skip stays a skip. Primary successful job logs and the official
qualification ZIP digest/CRC bind the actual maintenance source, workflow, run
and attempt. Registration main push CI `44125038640` also passed.

Normal publication run `44133307523` passed both source checks but its first
release job failed at NuGet OIDC: the existing policy expected builds-release.yml
and hexalith-release, while this approved legacy lane uses build-release.yml and
production. Create Release was skipped; no publication is inferred from that
attempt. The user explicitly replied **“add it yourself”** to the exact additional
NuGet policy and single normal release retry request, then signed in through
the requested Windows browser.

One additional policy, `Hexalith.Builds 4.44 maintenance` / `27467`, was created
through the normal authenticated UI as creator jpiquot. It grants the same
Hexalith owner, Hexalith.Builds* glob and push-new-packages scope, restricted to
Hexalith/Hexalith.Builds, build-release.yml and production. The existing policy
`26029` was independently compared and is unchanged. No API key fallback,
credential change or environment protection change was made.

Exactly one approved normal retry of failed job `134216202450` was requested.
Run `44133307523` attempt 2 has passed its real source gates and NuGet
authentication; Create Release is in progress. Its normal production review
was approved through the existing required reviewer jpiquot, preserving all
protection settings. The retry exception is spent; do not issue another retry.
Only the actual completed publisher and public signed archive/inventory can
authorize truthful Platform tuple fields.

Platform support source `33d28ad2f3cac02de45652743707f73a8fd3d9f7` corrects the
retained legacy test's obsolete main-only text assumption using real committed
source selection. Its ten original controls now pass, with other assertions
preserved. Tuple values are still original pending genuine publication. The
current maintenance code differs from the old legacy source, so final source
preparation must create a genuine same-code legacy commit, restoring only the
existing eight allowed metadata paths from authenticated legacy source
3cd036f78ccdfeb72521122a65f1a3aef0bff9da. Preserve its 349 controls/profile,
original tool/catalog/provenance and qualificationCredit:false; keep the current
412-case profile unchanged. Never widen the metadata allowance or claim an
unchanged old legacy source is same-code.

The phase 05 manifest binds 170 files (SHA-256
`56ccee298fc3df7dd133d86c39e24d6439541065107304afb20381ec3080121a`). All 18 shared root pins/checkouts and the frozen intent
remain unchanged. Source publication, current/legacy and SDK qualifications,
and the final real hosted cancellation-order proof remain open. No new
lifecycle cancellation request, current-cohort registration, Story 2.10
qualification or candidate acceptance is recorded. Continue Step 03.


### Supported Builds publication verified — 2026-11-17

The [phase 06 checkpoint](evidence/2-8/maintenance-20261117/phase-06-published-builds/checkpoint.json)
records run `44133307523` attempt 2 completed successfully. Both actual jobs
passed, including release job `134250485020`, its protected source recheck,
NuGet OIDC login and Create Release. The one authorized retry is spent. The
normal publisher derived and published [v4.44.14](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v4.44.14)
at source `979cfd3e2c4145be516fbd833dacfc6b8e8fdce5`; no tag or version was
manually overridden. Its delay was normal NuGet repository signing and public
blob availability, observed through the normal live Actions page.

Independent verification bound the official attempt-2 artifact digest, outer
and inner CRCs, complete evidence member hashes, actual producer source/run/job/
workflow identity, release eligibility and genuine tag source. The official
release inventory bytes match the qualified carrier and release asset digest:
`e114c68e45fd6b23785370f3849efdca70411e603aee87da76dff7a07ad37119`. The independently downloaded public NuGet archive
passed `dotnet nuget verify --all`; its SHA-256 is
`4bd1338627c7283d5fc98db9b374b932fb946f3b0c14b3ed97b4cea604273a73`. The extracted CLI reports exactly
`4.44.14+979cfd3e2c4145be516fbd833dacfc6b8e8fdce5`. Actual Tooling SHA-256 is
`fcb860995228e4ae332cc6474341e6f8f34d714bb6f71488804dc34fc9933e63`, AppHost assembly SHA-256 is
`c261d02d4ab2582c2a427361afb157552660b58365654abb39c6669210bf73f4`. Catalog blob `a82d3ad3e745ea7432f60c892c0c66395daaa8ef` has raw SHA-256
`908d6acf84f3fac00c14942652b7b6d35fc93094e5ce41f87061db44bdaf3295` and genuine catalog-origin revision
`7b95a68fa0657135cee1103defc59089defa93ff`. These are publication/artifact facts,
not a new candidate or caller/native qualification.

The phase 06 manifest binds 68 files (SHA-256
`4ea2403357b0432a219b498b3377085e656a8df687f3bcc8a51510f8c283ef44`). Matching Platform current/legacy preparation is now authorized
by the real publication data. Its existing 412 current cases, all 349 original
legacy controls, unchanged eight-path same-code boundary and all five SDK
gates remain required, followed by normal source publication and the final
hosted cancellation-order proof. No lifecycle cancellation request, current
cohort registration or Story 2.10 qualification is inferred. Step 03 remains open.


### Matching Platform current and genuine legacy sources — 2026-11-17

The [phase 07 checkpoint](evidence/2-8/maintenance-20261117/phase-07-platform-maintenance-source/checkpoint.json)
records the actual observed Builds 4.44.14 binding in Platform current source
`e1e8ccf5f51211fd5bd80259fbfd851b83513100`. The genuine same-code legacy source is
`3aa95205d54fc6daba229f38655a024ae88132f1`. It restores exactly the original eight allowed
metadata paths from 3cd036f78ccdfeb72521122a65f1a3aef0bff9da while retaining
identical new code, tests, helpers and documentation. The production verifier
ran at the actual legacy HEAD against the actual current Git commit and passed.
Its original 349 controls/profile/tool/catalog/provenance remain unchanged,
qualificationCredit:false and no binary-equivalence claim. The current profile
is byte-identical to the supported base and still requires 412 cases.

All 19 retained tuple controls, ten retained legacy controls and 42 source-
release controls passed. The normal `eng/prepare_builds_artifacts.py` with
--require-recorded passed on the clean committed current source and genuine
Builds source, independently checking the public archive's signature, exact
source/version, Tooling hash and catalog binding. Dependency signatures, all
complete commit messages and locked real range analysis passed; the derived
prospective source version is 1.7.2, with no tag/release created. The complete
17-path source diff was read by the parent.

A normal 1.7.x branch was created at authenticated supported base
0f4e27f1f78310bd731c10c19dc51bb1e0345d44. The genuine legacy branch was pushed
before the current source, then [Platform PR #44](https://github.com/Hexalith/Hexalith.Platform/pull/44) opened
against 1.7.x. Exact PR-source hosted tuple CI `44147787827` and catalog/SDK CI
`44147787643` are pending; current 412 cases, all 349 legacy controls and all
five SDK jobs must actually run and pass. These local controls grant none of
that hosted qualification. No source-publication dispatch or lifecycle
cancellation request was sent, and no existing branch protection was changed.

The phase 07 manifest binds 81 files (SHA-256
`3a37ef613dfb5af6505887976d702c65e4505fa8357e0849ff3eadd177672408`). Continue through normal PR qualification/merge, fresh exact
merged-source CI, normal registered source publication and the final genuine
hosted cancellation-order proof. The default-main registration PR #43 remains
unmerged until its necessity is resolved by the actual intended dispatch.
Step 03 remains in progress.


### User correction: supported Platform 2.x and latest Builds 6.x — 2026-11-17

The [phase 08 checkpoint](evidence/2-8/maintenance-20261117/phase-08-superseded-route/checkpoint.json)
records Platform PR #44's five SDK jobs passed, while tuple CI `44147787827`
failed during restore with NU1903 for Aspire.Hosting 13.6.4. Required tuple
suites never executed; its legacy job was skipped. The official failed artifact
digest/CRC and primary restore output were verified. The earlier producer logs
already contained the Aspire warning; the parent acknowledged that the initial
three-package maintenance patch was too narrow for Platform's restore gate.

The user first selected a security-patched legacy dependency baseline, retaining
all 349 tests and keeping audit enabled. A local, explicit v2 descriptor/four-
package floor proposal passed 14 legacy controls and 21 tuple/MSBuild controls.
The initial metadata-condition implementation failed real MSBuild evaluation;
the implementation was corrected without changing expected behavior. A precise
existing-CRLF attribute resolved the final whitespace check. None of this
obsolete projection was delivered to supported current Platform source.

An independent two-path Aspire13.6.5 catalog proposal
`f934342d541f9802ce5a987a65c60af868ec6d5c` passed normal catalog/audit checks
and 90 catalog scenarios with other package pins and audit families preserved.
Its first local finish failed commitlint for disallowed chore type; the corrected
full build message passed. It opened normal Builds PR #190, but was never
merged or published by this task.

**The user's latest instructions supersede this route:** “we publish only
2.x.x versions. 1.7.x is obsolete ans we do not want to maintain it” and
“use latest builds 6.x.x”. Stop all further 1.7.x/4.44.x maintenance work.
Use supported current Platform2.x with the observed latest stable Builds6.3.0
at `7afc4981e21bc2fcce687199a3fd2c61fe87336e`. Current Platform's existing
NET11/6.x path selects6.0.5; update its genuine publication binding, exact tool
pin/range, Builds gitlink and current documentation together. The current2.x
pipeline already retired L/M and349/412 obligations through Story2.13; do not
carry the obsolete maintenance projection into it. The original Story2.8
behavioral goal, actual verification and final native cancellation/cleanup proof
remain required. The frozen text stays unchanged; these explicit user changes
control the effective supported versions and route.

Cleanup discovered PRs #43 and #44 had already merged externally as
`11719c5cf51c2b2b080b89e0e301c2ef8dbacc46` and
`571c6c7b20eb6c28b5d9ec67de1f90f40537ce9c`, respectively. Those histories
are preserved. The shared root advanced externally to11719c5; all18 root
pins/checkouts and pending work remain intact. Unmerged Builds PR #190 was
closed and its CI `44154568897` cancelled through one ordinary administrative
request. This is separate from the lifecycle proof, whose cancellation request
count remains zero. No published tag/package/release was removed.

The phase08 manifest binds 83 files (SHA-256
`804242d6c01db0da88b296701c34b5de13634212e1bde2d7e548314393e5b4e2`). Further work is in artifacts/2-8-current-20261117 and the supported
2.x/6.x flow. No old maintenance merge/publish operator may be resumed.
Step03 remains in progress.


### Current Platform2.x adopts genuine Builds6.3.0 — 2026-11-17

The user's supported2.x/latest6.x decision is now concrete in [Platform PR #46](https://github.com/Hexalith/Hexalith.Platform/pull/46), final source `408481da06128ae50ad274d5dddcd4234d8b677d` against main `11719c5cf51c2b2b080b89e0e301c2ef8dbacc46`. It binds the normal signed Builds6.3.0 release/source `7afc4981e21bc2fcce687199a3fd2c61fe87336e`, exact tool pin/range, raw catalog and shared publication handoff together. Only the intended Builds gitlink changes; the other17 direct pins stay fixed. Current-only486+93+9 and six SDK gates remain mandatory. No obsolete1.7/4.44 operator is resumed.

Independent normal-release, artifact-digest/CRC, public inventory, repository signature, payload, executable/source, Tooling and exact catalog checks passed. The real signed validation AppHost is `3050b20b0037ab62470e4bb42a7e08512181b6abbbc02bb12de17b0f1e75f0a1`, paired with Tooling `48448ba24ceb2d265ad4b641e6587c79547167c0563954c662807824fdd6be27`. An earlier collector label contained the distinct EventStoreHost hash `e71345052a6b300eceab35b39026338fa0ebccb1ae5d2a2f6ed4a4a6d22feb40`; the signed member map and corrected host-identity record distinguish them. No signed archive or payload digest was relaxed.

Normal recorded artifact preparation exposed the producer's CRLF checkout versus exact Git-byte guard. It now materializes the existing bound producer privately, authenticates all exact bytes, rechecks that same private checkout before receipt/export and cleans it on exit. The normal checkout and its attributes remain unchanged. Twelve real Git acquisition/refusal controls,22 tuple and16 publication controls passed; the real public normal command also passed on final source. Main's existing four owner gitlinks had advanced with a stale source binding: regenerate only that binding from their genuine Git bodies, preserving identical canonical declaration hashes and package provenance. Local actual owner/release-tag closure and hosted source resolution passed. The Bugbot claim that the new binding mismatches those already-advanced pins is contradicted by that primary evidence; no source guard is waived.

Thirty catalog controls independently verify the exact6.3.0 raw body: finite named Platform/Parties contexts retain SDK4.2/OpenAPI10.0.15/2.13.0 and companions, while generic SDK5.2/OpenAPI11.0.0/3.11.1/Tenants8.0.1 remains separate. Exact source/range and signed host/Tooling admissions extend only to the observed tuple. The compiled owning project builds with zero warnings;17 current artifact and8 signed handoff cases passed with zero skips on source `44c0c647781543f774bfce9bb17d80cfba471ccf`. Source07 only adds the new catalog's5.2 selection to the source-metadata setup guard; all29 existing real metadata/restore controls pass, zero skips. Earlier SDK source `b5c3114a5a9b42adfd42668bcf1fcd2964d625fc` passed all six hosted SDK jobs in44162565674 but its tuple ran zero source-propagation cases because that finite setup guard still excluded5.2. Retain the failed run and authenticated diagnostics rather than giving them passing credit. No expected production assertion or mandatory count was weakened.

The source06 operator's post-push prUrl bookkeeping error was reconciled from actual PR head and both complete passing XML reports; the successful push was never replayed. Source05's missing nested direct Builds checkout is retained as a failed local setup observation; the correctly prepared source06 supplies the genuine25-case passing evidence.

Fresh final-source tuple [44166185074](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44166185074) and six-SDK [44166185115](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44166185115) are pending at this checkpoint. Main requires one approving code-owner review (`.github/CODEOWNERS`: jpiquot); no rule bypass, forged review, main merge or new Platform publication occurred. The original hosted cancellation-order/cleanup proof remains open, with zero lifecycle-proof cancellation requests. Local jobs used `run-with-temp`, completed their child work and cleaned disposable checkouts; no real local services, AppHost runtime, DCP, Dapr, Docker, listener or live probe was started. Artifact/source checks grant no image, native or candidate acceptance.

Evidence: [current6.3 checkpoint](evidence/2-8/maintenance-20261117/phase-09-current-builds-6-3/checkpoint.json), [file manifest](evidence/2-8/maintenance-20261117/phase-09-current-builds-6-3/manifest.json). Manifest SHA-256 `c6b4ebf9a4112b024976e04730cca99c0744742a6ddd01da1a04fe8391aea462`.


### Supported current focused proof: exact EventStore package-test purpose — 2026-11-17

The latest2.x/6.x decision requires an eligible current execution path for the remaining focused2.8 proof. Existing private EventStore `platform-package` eligibility already authenticates its exact declaration/entry,120-second execution and90-second stop budgets, package mode, actual parent proof, SDK4.2.0/Tenants7.0.7 public bodies and only the EventStore/Tenants topology. Its native binding currently has no Tenants7.0.7 image rows. This is a demonstrated execution prerequisite, separate from complete2.10 reference qualification.

A separate metadata-only diagnostic ref `be7149f6b4097b617ec1c5f287e68fe9cd4d9390` from genuine Parties main `c9c21fc3e48a999db575a361562707a79f1663cf` preserved every product gitlink and changed only the manual diagnostic workflow plus its collector/offline controls. The released Builds6.3 shared commit validator applied the owning module's exact policy; commitlint, npm signature verification, seven offline security/digest controls and actionlint passed. One normal workflow dispatch [44174015501](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44174015501), job134360420143, completed its read-only hosted registry collection successfully. No service was provisioned or native proof attempted. Original artifact14368338901 digest/CRC and exact source/run/attempt/job observations are authenticated: SHA256 `c8ed0a948bbbbca2f0d5df5042255da7783cad06f4d87d2fa58101a0e6633379`. All six observed image labels match the already recorded Tenants7.0.7 and Memories3.0.27 public release revisions. Metadata facts supply no runtime, image-cohort or candidate acceptance by themselves.

Prepare only the two exact Linux/amd64 Tenants7.0.7 image bindings for the existing EventStore package-test purpose: domain `590bc04ecc3301daef22b318d029b486edd8b40b550a592a32461223966a9635`; api `2a72f0e36a0cfeef9b7e26731315049248fa6f2be164547de102b12481105b65`. Preserve every original parent/source/entry/declaration/package/topology/budget check, the preassigned-digest refusal and the ordinary/serialized-proof refusals. Independently hash the original manifest bytes in a small regression fixture and verify both resulting bindings through the existing40-case guard class. Keep the current full Parties native cohorts unadmitted; do not expand the complete2.10 qualification scope.

After normal supported2.x source checks, owner review and publication, prepare one genuine EventStore caller for the remaining focused cancellation observation. Use its existing declared120/90 package suite and released6.3 executor; preserve production budgets and the240-second raw hold request. Any targeted execution hold must follow real gateway/readiness/persisted-state assertions and remain bounded by the declared suite. Use public testing packages in package mode and retain actual isolated-project execution. Record one finite exact-source/package/image/suite/workflow inventory before native dispatch; retain the original ready-before-one-cancel, first-outcome, withdrawal-before-stop, kernel-event and exact empty-inventory/diagnostic audit. This introduces no new policy schema, attached native execution, reference qualification or accepted-tool enrollment. Root owner gitlinks stay unchanged; helper/workflow diagnostics remain a genuine2.8 obligation.


### Final supported-current PR qualification and corrected native route — 2026-11-17

Final [PR #46](https://github.com/Hexalith/Hexalith.Platform/pull/46) source `408481da06128ae50ad274d5dddcd4234d8b677d` passed hosted tuple [44166185074](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44166185074) and all six SDK jobs in [44166185115](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44166185115), attempt1. Independently authenticated artifact14369843406 has SHA256 `7007cdc09afc7d815eddc37c7d1a84db91694bbf90bf5d4036ca970af1e3c18b`, valid CRCs and unique safe members. Complete primary receipts and all38 required XML groups match the exact committed profiles: current486, base93, extensions9; all588 passed with zero failed, skipped, errors or not-run. The sole current collector used genuine signed Builds6.3.0/source7af and actual NET11, with unchanged output snapshots and clean exact source. The actual executed PR virtual merge is `e6869d11be7e636ae2717ea77e504225eb83f5dd`, parents11719/408481; its Git tree `f8370e59ff5b20b75a681a581a21869f9e8b32c3` equals the PR head tree. Do not label the virtual merge as a literal checkout of408481. These remain PR preflight observations, with no native or candidate acceptance.

The first local artifact extractor authenticated the ZIP but then failed its receipt-count assertion because its4MB retention cap omitted the genuine6MB receipts. That failure is preserved. The corrected extractor retrieved the same official archive, retained all original receipts and passed the independent per-case/source/tool audit. No hosted job was retried. GitHub's latest-release observation still reports Builds6.3.0. Platform remains supported2.x only; the obsolete1.7/4.44 route stays superseded.

The preceding EventStore native-route proposal is **disqualified and must not be delivered**. The actual released Builds6.3.0 `CompositionAttachAdmission` refuses `QualificationOwners` with HXR033/qualification-owner-expansion: “Expanded qualification owners do not support attachment or reuse.” The EventStore package purpose needs that expansion, so it cannot supply this required active-hold observation under unchanged policy. Its local-only proposal `174bebdaf4ec4c7e454fdd7746f7ca1afe1d3c45` was never pushed;39/40 controls passed and the original signed-body failure remains failed. Preserve its sources/logs and the independently useful six hosted image facts. Do not change the published attachment policy or infer native eligibility from those metadata. A focused current Parties alternative remains investigation only; ordinary/public/non-CI full current cohorts remain unadmitted. No native grant or caller has been delivered, and no native dispatch or lifecycle cancellation request was sent on this supported route.

The concrete normal merge payload for PR46/head408481 passed the owning locked commitlint and npm signature checks. GitHub rule [Platform main reviewed changes](https://github.com/Hexalith/Hexalith.Platform/rules/24657106) requires one code-owner approval; sole owner jpiquot is also the PR author and cannot self-review. Its existing configured user bypass is available, but no fresh exception is approved or used. Earlier exact-head exceptions cannot be replayed. Request approval only for this exact completed PR/head; retain all normal fresh main tuple/six-SDK and supported2.x source-publication checks after merge. No protected-main push, manual tag, forged review, rule/credential change, main merge or new Platform publication occurred. The remaining original cancellation-order/cleanup proof stays open; Step03 remains in progress.

Evidence: [phase10 checkpoint](evidence/2-8/maintenance-20261117/phase-10-current-ci-passed/checkpoint.json), [primary receipt audit](evidence/2-8/maintenance-20261117/phase-10-current-ci-passed/observations/pr46-final-ci-verified-02/primary-receipt-audit.json), [specific review-exception request](evidence/2-8/maintenance-20261117/phase-10-current-ci-passed/owner-review-exception-request.json), [manifest](evidence/2-8/maintenance-20261117/phase-10-current-ci-passed/manifest.json). Manifest SHA256 `29cb085851f98944d3d48c40a6193cd9917922e5e59a6a62651f6c5c1d993d59`. Earlier phases and the frozen2526-byte block remain unchanged.


### Owner-approved Builds 6.3.0 merge and main CI — 2026-11-17

The owner replied “approve” to the specific PR46 code-owner review exception.
After verifying GitHub still reported the prepared head and base and all nine
required PR jobs passed, the configured jpiquot bypass was used for that exact
merge. [PR #46](https://github.com/Hexalith/Hexalith.Platform/pull/46) merged
as `a18e5f0e65b9cf733795f7fa134c5bdb2fd0858b`. The merge parents are the approved base
11719c5c and head408481da. Its tree equals the audited virtual-merge tree.
The GitHub ruleset continues to require code-owner approval for later PRs.

On the actual source pushed to main, [all six SDK jobs](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44189252435)
passed. The [tuple run](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44189252348)
passed source resolution and is executing the release build and tests. The
automatic [first source-release check](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44190151361)
published an authenticated deferred receipt with reason `counterpart-pending`.
Its publish job was skipped. GitHub invokes the same gate again on tuple
completion. Latest published Platform at this checkpoint is v2.1.10.

The released Builds6.3.0 helper passed all75 resource-free controls from source
7afc4981, including tool-only signal forwarding, the exact down and first-outcome
retention. The first two scratch runs omitted the prepare helper and then its
test fixtures. Their setup failures are retained along with the complete
passing run. The tests emitted a Python subprocess resource warning, and its
reported process had exited before the job returned.

Parties main c9c21fc3 has the bounded native persistence fixture and marker, but
its registered manual lifecycle workflow calls an absent controller script.
The lifecycle fixture uses the ordinary package entry. The current private
profile only authorizes the distinct integration-project entry, and its exact
current image cohort remains refused before native setup. Builds6.3 clears
GitHub job variables from the AppHost process environment, so a future hosted
admission must derive job context from the issued state and bind it to the
authenticated plan and executor. The released EventStore expanded-owner route
remains unable to admit a hold. Story2.8's real execution-cancellation ordering
and exact cleanup proof remain open, with zero current native dispatches or
lifecycle cancellation requests.

[Phase11 checkpoint](evidence/2-8/maintenance-20261117/phase-11-approved-main-merge/checkpoint.json)
and [manifest](evidence/2-8/maintenance-20261117/phase-11-approved-main-merge/manifest.json)
preserve the merge, run observations and proof boundary. The manifest covers
39 files with SHA256 `b69e67834e4d8be2759b16c38f334fd6ff9cd5f8e81c5f31d8899095fc153b1f`. The frozen acceptance text retains its
original digest.

### Local current-owner execution path — 2026-11-18

This Step 03 continuation implemented the phase-11 local prerequisites for the
remaining focused current-owner proof. No push, dispatch, merge, publication,
tag, pin change on `main`, native dispatch or lifecycle cancellation request
occurred. Results are local and `baseline-only`.

Uncommitted owner-repository work:

- Builds `7afc4981` working tree: `ResolveAttacherContext()` admits same-job
  issued owner state when process GitHub variables are scrubbed or incomplete;
  `NativeTestHandoff` forwards `HEXALITH_CI_LIFECYCLE_HOLD=1`. Cross-job issued
  state and incomplete CI without handoff still refuse.
- Platform: `CurrentOwnerMemories327Name` (Tenants 7.0.7 / Memories 3.0.27)
  binds only with `CurrentOwnerProof`. Ordinary/public 7.0.7 and
  `CurrentOwnerCurrentName` stay refused. HEAD still records Builds `a327bc5c`;
  the worktree checkout is `7afc4981` with additional uncommitted attach
  fallback.
- Parties `8abfd266` working tree: restored the lifecycle controller and
  ci-lifecycle suite. Execution-cancellation owners launch
  `platform-current-package`, set the hold flag before `execvp`, and wait on
  `execution-ready.json` after real persistence. `start-cancellation --phase
  execution` now takes a 240 s same-job CLI `attach()` hold named
  `execution-hold` and does not launch `attached-lifecycle-package`.

Independent resource-free recheck (no AppHost, Dapr, Docker or live probe):

- Builds issued-state / incomplete-process / handoff / HXR033: 7 passed.
- Parties `CiLifecycleHoldTests`: 2 passed.
- Parties `ExecutionCancellationMainTests` + `LaunchContractTests`: 6 passed.
- Platform `OrdinaryFixtureUsesOnlyItsActualMeasuredOwnerAndImageCohort`: 2 passed.

Still open, so Step 04 is not entered:

- Hosted execution-cancellation ordering, withdrawal-before-stop and exact
  empty-inventory proof on the supported 2.x / 6.x current-owner path.
- Current-owner Platform tests that resolve the workspace against a clean
  signed 6.3.0 pin; they were not executed on this dirty Builds tree.
- EventStore expanded-owner remains HXR033. No candidate or tool acceptance.

### Hosted proof blocked before dispatch — 2026-11-18

The user chose **hosted proof**. The [campaign inventory](evidence/2-8/hosted-proof-20261118/campaign-inventory.json)
was recorded first. No workflow dispatch, cancellation request, proof-branch
push, merge, tag or publication occurred.

A Parties `cancel-execution` run cannot reach a Ready owner with an active hold
on the required current-owner path from published identities:

- Latest published Platform is still `v2.1.10` (`215ae18c`). It refuses
  `CurrentOwnerMemories327Name`. Builds 6.3.0 Platform `a18e5f0e` passed tuple
  CI `44189252348` and source-release verify `44190151361`, then skipped
  publish and has no SemVer tag (HXW011 for any Parties caller).
- Image admission exists only in the uncommitted Platform working tree. A
  proof-branch Platform gitlink would be untagged and refused by HXW011 before
  allocation. Tags and publication remain outside this hosted-proof boundary.
- Parties `origin/main` `c9c21fc3` pins untagged Platform `1a5666ed`, tool
  5.2.2 (HXW010 against the 2.x range), and still calls the absent
  `scripts/qualify-disposable-ci.py`. Local controller restoration sits on
  `8abfd266`, behind that main.

The remaining proof still requires a tagged Platform release that admits the
measured current-owner images, then a Parties proof branch that adopts that
tuple, the restored controller, and one ready-then-cancel run. That publication
is a separate authorization from this hosted-proof session.

### Publication then proof authorized — 2026-11-18

The user chose **publish then proof**. Isolated worktree
`/tmp/hexalith-platform-current-owner-images` from `origin/main` `8069fcdd`
produced two commits on `fix/current-owner-image-admission`:

- `95bc1f99638ff20c0b6185a00a098066b60235c8` restores the signed Builds 6.3.0
  gitlink `7afc4981e21bc2fcce687199a3fd2c61fe87336e`. Main `8069fcdd` had
  recorded ancestor `a327bc5c`, so tuple initialization failed against
  `eng/builds-tuple.json`.
- `339af849b82dc82b1a3416931a7fff73d0ab2942` admits `CurrentOwnerMemories327`
  linux/amd64 Tenants 7.0.7 / Memories 3.0.27 images only under
  `CurrentOwnerProof`. Ordinary/public 7.0.7 and `CurrentOwnerCurrentName`
  stay refused.

[PR #47](https://github.com/Hexalith/Hexalith.Platform/pull/47) merged as
`bb8c7915ae67e427f3629427550c96ca1e74bc14` (parents `8069fcdd` / `339af849`)
using the configured code-owner bypass after PR tuple
[44211008988](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44211008988)
and all six catalog jobs in
[44211009070](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44211009070)
passed on head `339af849`. Fresh main tuple/catalog are
[44220196201](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44220196201)
and
[44220196096](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44220196096).
Main tuple [44220196201](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44220196201)
and catalog [44220196096](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44220196096)
passed. Source-release
[44228001525](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44228001525)
published tag `v2.1.11` at `bb8c7915` with Builds gitlink `7afc4981`.

Parties proof branch
`proof/story-2-8-current-owner-cancel-execution-20261118` (`0d64a977`) pins that
tuple, restores the controller, and holds after persistence via the controller
wait file. Restore blockers and isolated-test overlays are on that branch.
[44241094448](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44241094448)
passed isolated tests and allocated a disposable owner, then refused ordinary
`platform-package` integration with HXP020 (trusted parent plan/key handoff).
Cleanup completed with empty leftovers. Qualification never started. Zero
cancellation requests. The CiCd-owned lifecycle workflow still runs ordinary
`platform-package` in baseline; that gate cannot be changed from Parties.

### Proof-branch profile switch authorized — 2026-11-18

Diagnosis: Platform `v2.1.11` admits the Parties declaration
`dfb03b6e397944c4f24737f4ac9278fba033399a0efa30a80447c9112709efee` (identical on
Parties `origin/main`, `c9c21fc3`, `8abfd266` and the proof branch) only as
`PlatformCurrentOwnerProof.CurrentDeclarationHash` through the declared
`platform-current-package` profile. `PlatformPartiesPackageCatalogQualification.IsOrdinaryDeclaration`
refuses it for ordinary `platform-package`, which is why the baseline job of
`ci-lifecycle-qualification.yml` in run 44241094448 failed with HXP020. That file
lies in the CiCd story 21 owned perimeter.

The user chose **proof-branch switch**. Permitted for this Step 03 continuation:

- On Parties branch `proof/story-2-8-current-owner-cancel-execution-20261118`
  only, change the baseline `integration-test-profiles` in
  `.github/workflows/ci-lifecycle-qualification.yml` from `platform-package` to
  `platform-current-package`. Any further proof-branch-only correction needed
  for that same run is allowed if it stays within this branch and its
  workflow/controller, and it must be recorded.
- Run the lifecycle controller unit tests locally, push that branch, dispatch
  `scenario=cancel-execution`, and send exactly one normal cancellation after
  the ready artifact (`execution-ready.json`) check. Record exact final owned
  inventories and the campaign input change before dispatch.

Not permitted: merges, pushes to any `main`, tags, publications, pin changes on
`main`, or CiCd perimeter changes outside that proof branch. If the
current-owner path hits another gate, stop and report it rather than widening
scope. Results remain `baseline-only`: no candidate or tool acceptance, and
no Story 2.10 matrix pass.

### Proof-branch switch executed; dispatch not achieved — 2026-11-18

This continuation used the authorization above: one proof-branch push, one
dispatch and no cancellation request. There were no merges, `main` pushes,
tags, publications or pin changes. Records are in
[hosted-proof-20261118](evidence/2-8/hosted-proof-20261118/). The result is
`baseline-only`.

External events, reported and not repaired:

- At 07:41Z an external `/pushall` merged the earlier proof branch at
  `0d64a977` into Parties `main` as `8e3507e4`, then deleted the remote branch.
- Between 07:52Z and 07:54Z an external session removed worktree
  `/tmp/hexalith-parties-current-owner-proof` and the local proof branch. That
  discarded this task's first uncommitted copy of the edits. The edits were
  reapplied in a standalone scratch clone and match the lost copy (same four
  file SHA-256).

Proof commit `063400a55718303f86464959f4769961410660d8` on parent `0d64a977`
([patch](evidence/2-8/hosted-proof-20261118/proof-diff-063400a5.patch),
[input change](evidence/2-8/hosted-proof-20261118/campaign-input-change-1.json),
recorded before dispatch):

- **The authorized switch:** the lifecycle baseline now sets
  `integration-test-profiles: platform-current-package`. The ordinary `ci.yml`
  keeps `platform-package`.
- **Controller correction for the same run:** the controller's own source
  showed a gap, so it was fixed before dispatch rather than found on a hosted
  run. With the current profile in CI,
  `NativeAssemblyObservations.Write` calls
  `NativeDiagnosticBinding.VerifyCallerArtifact`, which reads
  `TestResults/module-integration/invocation.json`. Only the v2 helper writes
  that file, and the qualification job never runs the helper. The native test
  would therefore throw before `CiLifecycleHold` publishes
  `execution-ready.json`. The fix: before launching the execution owner,
  `declare_current_invocation()` writes that exact-job, sole-profile
  `baseline-only` invocation through the pinned helper's `write_evidence`.
  The job also retains `TestResults/module-integration`.
- **Controller tests:**
  - the boundary assertion now expects the current profile;
  - one new control fails against the parent controller.
- **Local checks:**
  - lifecycle controller suite 188/188 (the parent had 187);
  - actionlint passed;
  - commitlint 21.2.4 accepted the commit with the Parties config.

  No local service was started.

Run [44262928117](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44262928117)
([dispatch](evidence/2-8/hosted-proof-20261118/dispatch-7.json),
[result](evidence/2-8/hosted-proof-20261118/dispatch-7-result.json)) **failed
and is not achieved**:

- One of 15 isolated projects failed: `Hexalith.Parties.Ci.Tests`, 420/421
  passed. The failing case was
  `AspireRuntimePreparationTests.CatalogApiAvailabilityDoesNotSelectNativeHosts(version: "4.2.0", source: False, available: True, hostsAvailable: True)`.
  Its `dotnet msbuild -getProperty` evaluation exited 1 with empty output.
- The same row passed in run 44241094448 on unchanged test and MSBuild
  sources. This failure is recorded as nondeterministic and unrelated, and it
  is not relabelled passed.
- "Require isolated success before integration" refused provisioning, and the
  finalizer verified `not-allocated`. This is a fresh observation of matrix row
  1.
- The qualification job was skipped, so no owner, hold or cancellation
  existed.
- The one-request operator verified nothing and sent nothing. The lifecycle
  cancellation request count stays zero.
- The remote proof branch was deleted after the run so that `/pushall` cannot
  merge it into `main`. The commit can be re-pushed unchanged.

**Open — owner decision needed.** Repository CI guidance allows a re-run only
after a source change, so a retry of the unchanged `063400a5` needs an explicit
one-time exception, like the earlier macOS job retry. The choice is between:

- re-pushing the same commit with one fresh `cancel-execution` dispatch and the
  same one-request protocol;
- separately correcting the nondeterministic owner test in its own work.

The current-owner integration, the controller's invocation fix, the
withdraw-before-stop ordering and the empty-inventory proof all remain
unexercised on hosted runners. Step 04 is not entered.

### Closure by course correction — 2026-11-18

The user approved [sprint-change-proposal-2026-11-18.md](../planning-artifacts/sprint-change-proposal-2026-11-18.md).
By owner decision, the acceptance criteria are met by hosted Parties runs
42521615234 (matrix), 42526256372 (startup cancellation) and 42528046681
(execution cancellation) on Parties `926c1c3f` / Builds 4.44.13. Re-proving on
the newest tuple is not required. All remaining hosted-proof work in this spec
is dropped: the HXP020 route, the proof branch, Parties commit `063400a5` and
its scratch clone. No more 2.8 dispatches.

Step 04 reviews only the remaining delta:

- Builds `37e1a600` (on `main` through `/pushall`, unreleased): the same-job
  attach fallback for scrubbed GitHub job variables, `HEXALITH_CI_LIFECYCLE_HOLD`
  forwarding, `TestEnvironmentDescriptorWriter` and their tests.
- Platform `339af849` (released in v2.1.11): `CurrentOwnerMemories327` image
  admission under `CurrentOwnerProof`. Story 2.14 later removes this allowlist.

Story 2.10 (rescoped) and Story 2.14 (extended) own the follow-up work.
