---
title: '2.8 Run CI integration on disposable runners with guaranteed cleanup'
type: 'feature'
created: '2026-10-31'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
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

- [ ] Verify the strict helper/workflow retains its declared redacted diagnostics and finalizes success, failure and partial startup through the durable exact-job owner receipt. Missing prerequisites must fail before allocation; cleanup failure/unknown discovery must remain non-success.
- [ ] Verify actual E/P caller execution uses the completed descriptor migration and preserves required isolated projects and blocking package-mode integration. Relevant existing implementation can be consumed; fresh candidate acceptance still requires its own exact-decision run.
- [ ] Build a finite row-to-test/result index for the six original matrix rows. Authenticate existing results for their original subject and record the remaining rows. Every required row must have an actual executed passing covering test; missing, filtered or required-skipped cases remain open.
- [ ] Complete focused real hosted proof of success/failure/partial-startup cleanup and same-job attachment withdrawal, plus separate real startup and execution cancellation runs with exact final owned inventories. Retain the ready-artifact-before-exact-cancel protocol, first outcomes and diagnostics. The three prior broad matrices remain failed; a focused result is explicitly labelled and cannot pass the full 2.10 matrix.
- [ ] Complete relevant owner-repository source checks and the required final current/real-legacy/five-SDK checks for the actual final source. Preserve all original controls and explicitly account for meaningful incoming added cases. Complete any genuinely required normal source publication once; do not create unrelated release prerequisites.
- [ ] Complete the finite audit below and enter Step 04. Story completion still needs the normal review; full reference qualification/tool acceptance stays with 2.10.

The original four acceptance criteria and six matrix rows above stay unchanged. Actual cancellation, zero leftovers and withdrawal before teardown are essential. This plan changes neither the result of an earlier attempt nor the policy's qualification requirements.

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

### Resume entry point

Read this active spec plus its frontmatter contexts, inspect the actual workspace and required original receipts, then perform the one-time input inventory. Do not restart old monitors, replay completed merges/cancellations, assume a run still active or resume a stale archive-reading queue. No runtime process or remote campaign was started by this course correction. The historical handoff is supporting evidence, not the current task list.
