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
- [x] Helper above — blocking integration/finalization and exact-owner recovery.
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

## Implementation Notes

Local implementation is staged across Builds, Platform, EventStore and Parties. The full mapped Platform build passes; the Builds broad graph retains its pre-existing HXW005 FrontComposer source/package collision, with focused source diagnostics and exact reused-artifact hashes recorded separately. Published EventStore 3.128.4 lacks the migrated descriptor APIs, so caller activation and hosted qualification remain pending approved coordinated releases. See `evidence/2-8/README.md`, `verification.json` and `release-inputs.json`.

Coordinator final audit reran all 15 helper tests and 14 named CI lifecycle cases successfully and verified current release-source hashes and unchanged frozen intent. Both independent audit findings are resolved: constructor-time cancellation is forwarded once, and five executing CI cases observe committed withdrawal at the first AppHost stop. Final Builds coverage is 238 cases; Platform controls are 13. The remaining unchecked task is approved publication/reference activation and hosted qualification. Full story acceptance is not claimed.

## Spec Change Log

- 2026-10-31: use a versioned strict workflow entry point while preserving the legacy interface; frozen intent is unchanged.

## Review Triage Log

| Finding | Verdict | Action / evidence |
| --- | --- | --- |
| Signal during `Popen` can leave the newly created child uninterrupted | medium | Resolved: recheck cancellation after child assignment; constructor-time and real-subprocess signal controls pass (helper 15/15). |
| CI withdrawal ordering lacks an executing selected-suite/partial-startup stop observer | medium | Resolved: five executing service/partial-startup cases use a live AppHost stop observer; the full CI class passes 14/14. Source fixtures and simulated CI supply no hosted credit. |

## Verification

From B: build Module.Tests in Debug; run explicit xUnit classes with `-parallelMode none`, helper unittests and `pwsh -File Tools/test-domain-workflow-test-platforms.ps1`. Run individual Platform/reference projects. Record broad blockers. Hosted qualification requires approved publication/remote execution and revision-bound lifecycle/cleanup observations.

### Approved release phase (2026-10-31)

The user approved proceeding with the staged prerequisite releases and hosted qualification. This authorizes the necessary source commits, pushes/PRs, exact-source CI and release workflow dispatches in dependency order; existing protected publication, review, provenance and compatibility gates remain mandatory. The prior no-remote constraint applied to the completed local implementation phase. Preserve unrelated working changes and keep story status in progress until real hosted acceptance is recorded.

Remote reconciliation found EventStore main at `61e6fa853d81cf822d67d6abfd6a130bdbb14ce2` with green push CI and published SDK `3.129.0`; Tenants `6.0.0` is published, and Memories has an active release at `2649a946aa55cdd570c9beb15f9c91bc01703a26`. Reconcile these existing owner releases rather than starting duplicate release attempts. EventStore's SDK/helper prerequisite changes and later caller activation remain separate commit groups.

Historical release checkpoint (before current-main reconciliation and Builds merge): EventStore PR 378 and Builds PR 148 were open. The SDK prerequisite's full CI and published compatibility checks pass, but its required live-sidecar check fails six replay/source-guard cases also failing on the recorded main baseline. Merge and publication remain blocked. Builds' committed foundation passes 238 runtime cases and real archive-host qualification; incoming main reconciliation and required analyzer corrections are in progress. All 15 already-published Tenants/Memory family archives have valid signatures and exact version/source metadata. No accepted pin, registry credential or candidate acceptance has changed. Current revision-bound records and remaining qualification work are in [the release-phase handoff](evidence/2-8/release-phase/README.md).

Rollout compatibility decision (supersedes the mechanical legacy-pin proposal): preserve `domain-ci.yml` and add `domain-ci-v2.yml` from the staged strict workflow. Four legacy-pin PRs were prepared, but Folders and Conversations expose unrelated existing CI failures. A versioned entry point avoids making this story depend on those repairs. Existing callers keep their unchanged interface; their lanes grant no new integration or candidate acceptance. V2 binds its own exact identity, closure and executor helper and rejects legacy identity/digests. EventStore and Parties adopt an approved exact v2 source only after compatible releases. Preserve permissions, explicit pull-only secrets, required suites, package authority and all normal publication gates.

Current checkpoint: Builds PR 148 normally merged as `57255b502fdd546bb09851d04fc1830175d897f3` after all required checks and full hosted CI passed. Exact-source main CI is running. SDK PR 378 includes current owner main through a normal branch merge; refreshed full build passes and live integration remains pending. No package release or reference activation has occurred in this phase.

Publication verification finding: SDK PR 378's published-compatibility job in run `41236793296` reports green but its real test result is 49 passed, one failed, zero skipped. The unchanged historical `Published31282QualificationMatrixAsync` reports 74 failed/unverified observations among 239 historical package/source lanes; default Bash without pipefail masks dotnet failure through tee. Preserve the failure and existing expectations; no passing compatibility or release qualification is inferred from this job. See [the actual result](evidence/2-8/release-phase/eventstore-sdk/published-compatibility-actual-result-2ed.json). SDK publication remains held for valid exact-source/cohort qualification.

Main-source verification finding: Builds main push CI `41238944762` at `57255b502fdd546bb09851d04fc1830175d897f3` reports one failed module case (2,662 passed, five existing skips). Concurrent explicit down returned HXR037/HXT008 while startup was cancelling; the case expects success. The identical PR source had passed all 2,663 applicable cases. The case elapsed 1.385 seconds under its declared four-second overall stop bound (the five-second options value configures process stopping); outcome-lock contention is a hypothesis, not established timeout exhaustion. See [the exact failure](evidence/2-8/release-phase/builds-foundation/main-dotnet-572-failure.json). No unchanged rerun or publication is authorized as a workaround; retain and correct the failure.

Next sequential implementation step: diagnose the exact-main cleanup contention task in an isolated Builds worktree based on the verified v2 head `26ba857a74d75b184a0340a07f280dca3bb08874`. Retain deterministic reproduction and all refusal/withdrawal/first-outcome assertions. Prepare the smallest warranted fix as a separate commit on PR 149, with a final description covering its actual scope; normal complete checks remain mandatory. Do not merge or publish in this implementation handoff. The v2 producer preparation is complete, with 411 legacy assertions, 254 strict assertions, 95 provenance controls, 56 governed workflow controls, 11 preparation controls and 24 unchanged helper controls passing; Sonar and pinned commitlint are green and other exact-head CI is running. The four superseded compatibility PRs are closed unmerged. Reference caller adoption and package publication remain pending compatible released inputs and full hosted qualification.

SDK current-base gates update: all seven effective required PR 378 contexts now pass. Live-sidecar run `41236793296` has 304 passes, zero failures and four existing skips; its serialized execution control passes 1/1. Merge is held while pre-existing production release `41238371177` waits at source `484f4bd82fd2ef4b6c2653fdde57feeeb6d85a3c`, which excludes this SDK helper. The release is not dispatched by this phase. Source/workstream ownership was asked asynchronously; do not mutate main or assume that waiting publication selects our newly qualified source. Existing release approval remains applicable after source/cohort reconciliation, with all normal gates preserved.

Cleanup correction checkpoint: separate commit `46365cd94f8501f513f1cc413916ac24737d33e2` is pushed on Builds PR 149. Four original cases pass and two controlled lock cases fail on unchanged runtime; final conditional `waitForLock: canHoldAttachments` uses the same stop token and preserves the no-admission legacy path. Fresh Debug build has zero warnings/errors, and all 59 local lifecycle, 16 CI lifecycle and three legacy bookkeeping cases pass with zero skips. Root independently verified complete diffs, source/binary hashes and named XML results. Exact-head Sonar/Codacy/CodeQL/dependency/commitlint checks pass; full .NET/Python/package/catalog verification is running. No main merge, publication or reference adoption is claimed.

### Current-main reconciliation decision (supersedes the provisional bounded-wait fix)

Builds main advanced normally through PR 150 to `f6834a33967f8ec905145db14e1a13ffb5468d69`, adding a test-only correction for this same race. Its four original startup/down cases retry only the documented HXR037/HXT008 same-owner refusal after startup finalization, and two executing controls retain a live owned process during blocked metadata, then verify exact retry and immutable outcomes/expiry. Its production source is unchanged. This satisfies the frozen failure/refusal/retry contract. Preserve that incoming correction and its separate spec exactly.

The pragmatic final implementation adopts the existing retry contract and withdraws our provisional production waiting change. That avoids adding a new production timing behavior after the upstream fix has already addressed the mis-modeled assertion. The prior `46365cd94f8501f513f1cc413916ac24737d33e2` source and its 78-case evidence remain historical. Its complete hosted CI `41242828507` passed, but it is behind current main and must not be merged using a bypass.

- [x] Reconcile PR 149 normally with current main: validate the exact merge message with pinned commitlint, perform a normal branch merge preserving the incoming PR 150 spec and all test controls, and restore `R/CompositionEngine.cs` byte-for-byte/git-normalized from current main. Remove our two transient-lock waiting cases and their now-unused helper extraction; retain all incoming original-case retry assertions and ready-process refusal/retry controls. Retain our additional active-attachment metadata-lock refusal/exact-retry case only as useful coverage: rename it to describe immediate refusal, remove the artificial deadline-timing expectation, and preserve active holds, owned workspace/claims, first outcome/status/cause/time and absolute expiry assertions. Do not alter production cleanup, stopping allowances, retention, admission or withdrawal rules.
- [x] Verify the reconciled current test class plus existing CI lifecycle class and three legacy bookkeeping controls on one fresh same-source binary with complete simulated GitHub context. Preserve historical red/failed invocations separately; rerun no broader local suites absent a new failure. Confirm production tree, legacy workflow/docs and incoming spec are unchanged from new main; v2 producer source remains the reviewed source. Prepare normal commit/push and a final PR title/body/squash candidate around versioned integration and preserved retry semantics, all pinned-commitlint validated. Existing complete current-base hosted gates remain mandatory before parent merge. No merge, publication, registry provisioning or caller adoption in this implementation handoff.

See [the complete incoming delta](evidence/2-8/release-phase/cleanup-contention/incoming-main-f683.json). The frozen intent is unchanged. This supersession is a smaller implementation choice within its explicit retry-permitted matrix, rather than a policy or acceptance change.

### Next prerequisite preparation while current-head CI runs

The reconciled Builds producer source is `fb949ec9c41587279b1e153a3f9d00c7ad3cfc37` on main base `f6834a33967f8ec905145db14e1a13ffb5468d69`. All production source and incoming retry controls are preserved; local/CI/legacy controls pass 78/78. Parent monitors its normal hosted gates separately.

The existing SDK release completed externally as 3.130.0 from `484f4bd82fd2ef4b6c2653fdde57feeeb6d85a3c`; parent verified all 14 official archive identities, source commits and signatures. Parent normally merged SDK helper PR 378 as `1de42b706f52af0da6513f0b7663002606d17156`, preserving the exact reviewed tree/message. Exact new-main push CI is running; no helper-containing release is claimed or dispatched. The earlier waiting-release ownership question no longer blocks main reconciliation because the actual prior run completed. Separate technical catalog PR 380 remains its owner's active work and must not be edited here.

Next sequential agent task is a read-only Parties prerequisite preflight: inspect the current canonical `Hexalith.Parties` main, owning project/CI/release guidance and actual release inventory in an isolated worktree, preserving the shared dirty checkout. Determine whether the real Parties and `src/Hexalith.Parties.EventStoreExtensions/Hexalith.Parties.EventStoreExtensions.csproj` libraries build in supported Release/package mode against the now-official owner-selected SDK 3.130.0 and published Tenants/Memory inputs, with zero source fallback or accepted pin edits. Use only owning supported imports/properties and root-declared dependencies; never initialize nested submodules or materialize private SDK drafts. A narrow owning-library/adapter Release build is useful; do not invent future versions or publish artifacts. Inspect live main CI/protections and the actual gitlink-range ledger to identify any remaining normal release prerequisite. Preserve commands, evaluated package/source identity, exact source, failures and limits, then report the smallest concrete next action. Do not repair source/CI, commit, push, merge, dispatch release, approve environments, provision registry access or adopt callers during this read-only preflight. This gathers available context and does not change the approved publication authorization for later qualified steps.

Final producer reconciliation checkpoint: `fb949ec9c41587279b1e153a3f9d00c7ad3cfc37` preserves current main `f6834a33967f8ec905145db14e1a13ffb5468d69` with an identical production src tree and all incoming tests/spec. Only the reviewed 15 v2 producer files and one 49-line active-attachment refusal/retry control differ. Fresh resolved-tree build has zero warnings/errors; final 78-case XML and exact compiled tree/source/binary hashes are independently verified. Full new current-base hosted CI is pending; no provisional production wait is retained.

### Qualified producer merge and explicit registry-reader selection

Builds PR 149 normally squash-merged as `d8d0d70f26d79c95b544a64a74e329149711cb7d` after all exact current-base required checks and complete CI `41244914066` succeeded. Actual module result: 2,666 passed, zero failed, five existing skips; evidence tests 191/191; tooling integration one passed and 12 existing skips. Parent verified the exact reviewed tree, validated squash message, base, strict check app identities and preserved production/legacy/incoming-source bytes. Exact-main CI `41246834980` is now running; no Builds publication is claimed.

SDK helper main `1de42b706f52af0da6513f0b7663002606d17156` passed current-main CI `41244694290` (14,863 passed, zero failed, 49 existing skips across 20 actual test summaries) and commitlint `41244694281`. Parent dispatched helper prerequisite Release `41246774822` from that exact live main. Its existing exact-source preflight passed and its existing protected production review was normally approved under the user's release authorization, with `current_user_can_approve=true`, no bypass and no protection changes. Publication is running; do not invent its version. The separate previous release `41238371177` completed externally and was never reviewed, dispatched or modified by this story. The user explicitly confirmed that earlier run belongs to a separate workstream.

The user explicitly selected **Prepare a dedicated CI reader account**. Selected principal is `ci-reader`, registry `registry.hexalith.com`, read only, no administrator membership. Explicit credentials are limited to repository secrets `HEXALITH_CI_REGISTRY_USERNAME` and `HEXALITH_CI_REGISTRY_PASSWORD` in EventStore and Parties and their explicit v2 secret mappings. Existing readers/writers, anonymous refusal, retention, provider credentials and publisher credentials remain unchanged. Do not read or reuse the existing workload reader's private custody.

Next sequential implementation task, after completing the current Parties read-only report: prepare the smallest concrete registry account provisioning artifact for parent review; do not execute mutations in this preparation handoff. Use `eng/ci-registry-reader/README.md` plus one narrowly scoped operator script only if needed, and sanitized evidence under `evidence/2-8/release-phase/registry-reader/` relative to this spec. Read live config and projected deployment identity with explicit kubeconfig `/home/administrator/.kube/config`, context `jpiquot@local`, cluster kube-system UID `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`, namespace `registry-distribution`. Preserve the live ConfigMap `distribution-registry-config` UID `d00eeb4b-f507-440f-8328-fb838a7d5da3`, resourceVersion `27899151`, config SHA-256 `90805228cf4ac16752d3bb1ca15b47b92f8c24f1a572b5275f1e54c9eecb0317`. Detect drift rather than silently rebasing the plan. Read only metadata/key names for Secret `distribution-registry-htpasswd` during preparation. Do not expose authentication headers, passwords, bcrypt rows, existing secret values, private GitHub auth, or full responses containing them.

Prepare reproducible exact-source configuration and UID/resourceVersion/config-value-guarded changes before any apply: append one dedicated read-only `ci-reader` repository policy while retaining all existing configuration and writer/reader policies. The original applied kustomize source is outside local repositories and has not been located; document this and retain an explicit reproducible source/patch that can be carried into that original source before reapplication. Do not claim that the external source has been updated. Explain and bind the necessary targeted config reload: config.json is mounted with subPath and the current one-replica deployment uses Recreate; no unrelated restart, scaling, availability or policy change. Keep encrypted password-hash append and protected rollback material outside all Git worktrees in a new owner-only custody directory; plain credentials are generated cryptographically only for this account, never printed or passed on argv, and delivered to GitHub through stdin. Existing htpasswd rows must remain byte-identical. Preparation must not read them or create real credentials.

The reviewable procedure must separate preparation, exact revalidation, account/policy application, normal targeted reload, measurements and final credential delivery. A future execution must prove actual authenticated known-image manifest and blob reads with content hashes, anonymous/wrong-credential refusals, and create/update/delete denial using requests that cannot alter retained data even if permissions are unexpectedly broad. Record only sanitized statuses, source/resource identities and content hashes. Preserve failures; no synthetic proof, privilege broadening or candidate acceptance. Stage no unrelated work, commit/push no source, create no account/secret, perform no registry/cluster mutation, set no GitHub secret and dispatch no workflow in this preparation handoff. Parent continues independently with already approved ordinary release and merge coordination.

### Parties exact source-selection ratification

The user explicitly answered **Approve these seven existing revisions** to the concrete [owner-selection proposal](evidence/2-8/release-phase/parties-preflight/owner-selection-proposal.md). This is release-owner `jpiquot` source-selection ratification for exactly these already-recorded main pointers: Builds `81c88b34d09d34260b92d7f0928dbbc1c7446baa`, EventStore `6bcd40e3158d5b99e8d94cd300575768a6e479ab`, FrontComposer `86e07babdf88329dffc60f0c4fb5b566a35063c4`, McpCli `d7e21771079a2302e7a2e958dccdd12d94f56070`, Memories `2649a946aa55cdd570c9beb15f9c91bc01703a26`, Platform `82c9a31a0565c6dfd47750f5c67c087b487e6300`, Tenants `9e5a991d0ba304bac7b1a5809cd71797b47d78ed`. It supplies no CI/test/package/release/runtime acceptance. No additional owner approval is needed for recording these seven exact selections.

After the registry preparation handoff is complete and parent has reviewed it, the next sequential source task is the Parties ledger correction in an isolated worktree on current canonical main. Inspect guidance and `.gitlink-signoff.tsv` format; verify current main still records all seven exact gitlinks, and preserve every existing row and source pointer. Append seven source-selection-only `validated-advance` rows with canonical SHA, actual `jpiquot` ratification and actual approval date, citing the concrete approval/evidence in a comment or short owning document. No fabricated test/parity/owner-release statement. Re-execute exact push-range and release-floor controls, then prepare the smallest normal commit/PR with owning pinned commitlint validation for every exact message/title. Do not alter source, catalog, workflow gates or tests to make the ledger pass. Full normal CI must run on the correction; parent may perform authorized normal remote operations after exact review. Preserve shared dirty source and never initialize nested submodules. This paragraph queues the next task and does not expand the currently active registry preparation handoff.

Registry execution sequencing: the active SDK prerequisite release is publishing a container through the same registry. The parent must wait for that known publication to finish before applying the necessary targeted registry reload, and recheck known active publisher state immediately before mutation. Do not interrupt the active publication. This does not change the preparation-only active handoff.

SDK helper release checkpoint: `41246774822` completed successfully as official `3.130.1` at exact `1de42b706f52af0da6513f0b7663002606d17156`. Parent verified the resolved release tag, artifact publication identity, five raw OCI index/manifest/config hashes, both revision/version labels, and both platform smoke/cleanup results. Image index is `sha256:cbaed729ae671eff9ecf69b40c8f646e0a27aeae62fe0481ce6be98cfa840c86`. Exact-main live-sidecar run also passed 304/0/four existing skips plus serialization 1/1. Initial official NuGet flat-container requests returned 404 and index lists had not yet propagated; no passing official archive/signature claim or consumer adoption is made until retrieval verifies. Known SDK publication no longer blocks the proposed registry reload; recheck actual active publishers at execution.

Reference cohort preparation remains a later explicit task. Current Builds catalog defaults are SDK3.129.0/Tenants5.11.2/Memories3.0.13/Parties2.0.0; Parties source overrides SDK3.130.0/Tenants6.0.0 through its supported owner props. A producer merge or isolated source build is not combined cohort acceptance. Preserve catalog authority and verify actual published compatible owner versions/declarations/images before updating accepted inputs or selecting the final tool release. Avoid a redundant tool publication against a knowingly incomplete reference cohort: parent may await the actual Parties adapter publication and perform the smallest necessary catalog/declaration adoption before dispatching the final tool release. Never invent future package versions or import the separate unpublished technical PR380.

### Operator-requested execution pause

The operator explicitly paused this session and its child work until the separate Parties publication in coordination thread `01a19065-cca3-7051-801f-fa2e62875924` finishes. Preserve all source, commits, branches, staged/untracked changes and evidence. No further edits/builds in shared Parties checkouts or commits/pushes/merges/releases to EventStore, Tenants or Builds. Do not cancel other sessions’ workflows or change protections. Finish only necessary owned-resource cleanup and wait for explicit resume; do not resume on a timer. This instruction supersedes the queued implementation handoffs while paused. Story remains in progress; checkpoint: [paused state](evidence/2-8/release-phase/paused-checkpoint.json). No registry account, policy, Kubernetes Secret or GitHub secret was created or changed by this session.
