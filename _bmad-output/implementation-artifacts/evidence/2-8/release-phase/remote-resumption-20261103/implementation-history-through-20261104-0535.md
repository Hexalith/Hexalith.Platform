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

## Current Execution Handoff

The user authorized the complete story in stages, including normal source commits/pushes/PRs/merges, prerequisite publication and protected production review through the existing authorized role, registry reader delivery and real hosted/lifecycle qualification. Repeated “continue” preserves that authorization. Do not ask again for general permission. Preserve authentic source/review/publication gates; no bypass, fake review, self-approval, weakened assertions, unchanged-failure retries or candidate acceptance.

### Workflow and custody

Bmad-build rendered exactly once at `_bmad/render/bmad-build/platform-fcb6e4841134/df57414dae3b284c0fb7/`. Step03 is active; never rerender or enter step04 early. Baselines remain root03a099bba258c37a8f16c8b2e842ae842b1ad6d6 and Builds a22b55f0a4f6d2aaa3a5ccc505896064ac595364. Frozen SHA19044c59bdc8f93eee27bb766b6985fbc2f908f8e9764bebaeb72ecb908d9b0a and both unchecked reference tasks are unchanged. Before step04, stage/write/read the complete original-baseline unified diff including untracked files and audit every task, criterion and matrix row against actual passing execution. Prior large baseline diff is historical, not final.

One sequential implementation agent `/root/implement_story_2_8` remains available. Re-engage it only with the skill's exact sole-spec prompt after writing the scope here; no additional agents. Parent audits actual patches and hosted artifacts and controls remote qualification. Use isolated owner worktrees, root-declared direct dependencies only and no nested initialization. Preserve incoming/shared work; no reset/clean/force-push/pruning/amending. C# Debug normally; label Release/package qualification, build individual projects or execute their built xUnit assemblies, never solution-level dotnet test. Preserve analyzers/XML docs/one type per file/CRLF. Validate exact complete messages and titles with owning locked commitlint21.2.4 before mutation; no chore. Builds Node>=24.10; Parties cached Node24.22.1/npm11.19.1. No credentials/headers on argv/stdout/Git; custody remains private. Prefer connected GitHub tools; gh is fallback for binary/API mutations not supported by the connector. Both identities are jpiquot/id6775094; do not assume a bot or self-approve.

Evidence directory below is `evidence/2-8/release-phase/remote-resumption-20261103/` relative to this spec. Full earlier handoffs and first failures are preserved in immutable history archives linked by the evidence index, including the latest archive made with this consolidation. They are historical facts, not current dispatches or invented approval.

### Registry and public artifacts

Dedicated pull-only ci-reader is applied, measured and delivered to E/P as exactly HEXALITH_CI_REGISTRY_USERNAME/PASSWORD. Authenticated bodies match digests, anonymous/wrong reads401, create/update/delete403; existing auth/policy preserved, Zot Ready. Do not reprovision. `registry-live-checkpoint.json` and `memories3023-command-results-index.json` identify existing private600 custody without credential values. External kustomize source has not been found/reconciled.

Published catalog tuple currently selects EventStore4.0.4/source326bfef884543cdbcaf96de7f4c4ad378dbad6d7, Tenants7.0.0/source1b2c71b0e3cc5b16745f87aa7f1bc2c0ac8ffc27, Memories3.0.18/source66822bc5bc4858c824f17b7d093f5f0f676b03ea, Parties3.0.0/source639d6e98b496429eb0490d1f8feeec97bd1a3280, FrontComposer4.7.5/source07089dcbdad784a1d53037e9cea78d1d7becd8d6 and McpCli2.0.0. Earlier full official family proofs remain valid within their stated scope. NuGet repository signature checks use offline revocation, never an online claim. Raw signed ZIP SHA512 differs from signature content hash.

Shared Tenants.Contracts7 cache contains wrong-source0ad47c bytes; leave it untouched. Official cache `/home/administrator/.cache/hexalith/story-2-8/tenants7-official-nuget-20261103` has DLL SHA5676f656752395e149296ffbe9bc9d5e5e699e4bd6d4265df7de0f3442f05a40 and verified signature content hash TsYZitTvzdxx/fRjwRaaytAzO5//84YuS9AtRBLNUVWy+NHW09zGfTmd3pNMLp87g50rIxLxSmWXiSXjvcFZyQ==. Prior official FrontComposer served asset comparison and clean68 browser evidence do not prove source/reconciler parity.

### Current released Platform/Builds prerequisite

Root HEAD is 728baa7c0f006d86e71d6c9b29527dabc8e16e49, genuine Platform v1.2.1/release433460382 published2026-11-04T02:58:23Z. It includes PR18's finite package profile and PR19's immutable repository@sha256 image preflight correction. Root records Builds6249a8c0a0480a4b8a8ba04fc214c6104c3c9045; canonical direct Builds is now clean at that exact source, synchronized without root HEAD/index or other dependency changes. [Synchronization receipt](evidence/2-8/release-phase/remote-resumption-20261103/parent-platform-728-canonical-builds-sync.json).

Builds v4.44.0/source6249 is genuine release433376662 and successful normal Release41736408981/attempt1. Parent verified both public signed CLI archives, exact reconstructed unsigned archive hashes against official inventory, both actual CLI --version results, all55 qualification payloads and295 tracked fixture bodies with explicit owning CRLF conversion. Module signed archive SHAbd454ebae17256eb27d67d197a18295e0153bc11f01b6f73e22defe0c21e53a9, Tooling SHAc85d688e71e17afd5f1a6b706d7cda896fe2b9f04852360c657c31d2c46b52d5, inventory SHA04f97587db775b4aa2df30f903d66dabe45ab282418b661b5c69b443e2ecd894. Catalog blobab01a410d9e3d5b626ae7feb8da6ec3c1a0af27b/SHA6013c6fc168131c36ab5473ddeb47951f8c926df23bc6a1bea24de9c8ef420ce. [Official audit](evidence/2-8/release-phase/remote-resumption-20261103/parent-builds-4440-official-audit.json), matching release/tag/producer API JSON receipts. Private cache `/home/administrator/.cache/hexalith/story-2-8/builds-4440-official-20261104`. Newer4.44.1/d39844a16ecf522c0d25efd0f53b31f7a3dfe423 is not Platform1.2.1's selected tuple; do not mix it in.

Actual Platform1.2.1 tuple41763592675/attempt1 at728 passed188 cases (83+2+24+44+7+28), all1,484 retained source-input hashes match immutable Git. Actual317 signed package member hashes/315 retained extracted members match. Actual CLI version is4.44.0+6249. Five SDK output hashes measured unchanged before/after tests match known official public bytes, but those output DLL/testassembly bodies are not retained. [Tuple audit](evidence/2-8/release-phase/remote-resumption-20261103/parent-platform-121-hosted-tuple-audit.json).

Catalog run41763558526/attempt1: allfour SDK3.130.0/1/2 and3.132.1 artifacts each pass15 cases; parent rehashed49 retained executable bodies each, compared source closure/catalog fields and actual receipt hashes. Absent307 bodies each remain manifest-only. All208 real MSBuild finite gate evaluations pass. [Catalog audit](evidence/2-8/release-phase/remote-resumption-20261103/parent-platform-121-sdk-catalog-audit.json), [retrieval](evidence/2-8/release-phase/remote-resumption-20261103/parent-platform-121-hosted-retrieval.json). Component candidateAcceptance fields are scoped historical fixture output, never Story2.8 candidate/native acceptance.

Released finite authority is in docs/package-catalog-qualification.md, PlatformPackageCatalogQualification.cs, PlatformQualificationOwnerProof.cs and PublishedCatalogArtifactCohort.cs. It admits only a fresh Development Linux/x64 EventStore package test with exactly additional owner tenants; exact E/T declarations, complete gateway+Tenants domain/API closure, actual signed SDK4.0.4/T7 bytes and private purpose-bound proof. Ordinary Compose cannot serialize that proof; other/default/domain/Parties cohorts and expanded-owner attachments remain refused. Preserve these guards until concrete new finite qualification exists. Builds4.44.0 already supports v2 integration-test-reference-owners, helper --owners and public test --with-owner=tenants. No helper feature is needed for this existing profile.

### EventStore proposal: source ready, push pending old run completion

Existing PR392: https://github.com/Hexalith/Hexalith.EventStore/pull/392. Worktree `/home/administrator/.cache/hexalith/story-2-8/eventstore-platform117-adoption-20261103/Hexalith.EventStore`, branch fix/adopt-platform-117. Remote/current proposal head f15c74b61938722a367b16060b42c3a6bcd203b6, observed main8a1aca637dad45d6c67806ae7c68508ef42b1206. New local clean commit **a0c65bec419138e79d763557c73573f894217f24** is not pushed yet.

That six-path commit coherently adopts Builds4.44.0/6249 and Platform1.2.1/728, updates exact caller contracts/docs and selects integration-test-reference-owners: tenants. Production manifest SHA21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495 and every native/diagnostic file remain byte-identical. Actual direct Tenants is official1b2. Signed-tool preparation0, owning Contracts Release/package build0warnings/errors, five actual caller/harness controls5/5zero skips, released helper owner selection3/3, actualCLI version/unchanged manifest validation pass. Real local helper correctly refuses absent GitHub-hosted identity before receipt/evidence; no fake CI/service claim. Parent read fullpatch and actualTRX independently. [Agent checkpoint](evidence/2-8/release-phase/remote-resumption-20261103/eventstore-platform121-checkpoint.json), [parent audit](evidence/2-8/release-phase/remote-resumption-20261103/parent-eventstore-a0c-platform121-source-audit.json). Existing exact title/body drafts and locked-message preflight are retained.

Prior f15 CI41774344462/attempt1 is still in progress at05:19UTC. Its five independent jobs, including Contracts, passed. Main126391076880 passed the isolated gate and reached required native invocation at05:18:05UTC. The independently verified Contracts artifact has1,518executed/passed and2existingNotExecuted cases; Tenants source-mode has7/7passes. See parent-eventstore-f15-independent-artifact-audit.json. Native outcome remains pending. Parent is observing its actual terminal outcome **before pushing a0c**, to avoid superseding that concrete correction run. Agent has returned the bounded source work; no active E implementation task. Once terminal, parent may normally push the already prepared authorized commit/update existing PR metadata, preserving old outcome and fresh exact-source run identities. Never manually re-run/cancel to hide failure. No merge/approval/tag/publication yet.

Old8d CI41711963375 attempt1 was externally cancelled; externally rerun attempt2 failed isolated tests, native not allocated. Incoming owner main fixed actual Admin-health classification/token/governance expectations. f15 retained them and corrected the negative mutation fixture's valid baseline45; genuine5red→5green and13/95/274focused tests are retained. First failures/attempts are never replaced by later results. [Failed artifact audit](evidence/2-8/release-phase/remote-resumption-20261103/parent-reference-20261104-failed-artifact-audit.json), [f15 correction audit](evidence/2-8/release-phase/remote-resumption-20261103/parent-eventstore-f15-hosted-fix-source-audit.json).

The real native suite must pass BOTH named Facts GatewayReadinessAndOwnedPersistenceEndStateAsync (real authenticated gateway200 + owned Redis SETNX/GET) and GatewayRefusesAnonymousCommandStatusAsync (real401), not just seven metadata tests. Diagnostic writing happens only after real assertions, through actual surviving run descriptor/plan/mapping/receipt/context/current Git binding. Native execution has a scrubbed allowlist with no GitHub env; do not add one or artificially load SDK DLLs. Preserve private600/CreateNew/nonlinks/64KiB/64assembly bounds.

### Parties proposal and lifecycle evidence still missing

Existing PR61: https://github.com/Hexalith/Hexalith.Parties/pull/61, worktree `/home/administrator/.cache/hexalith/story-2-8/parties-platform117-adoption-20261103/Hexalith.Parties`, branch fix/adopt-platform-117, current headc10ce7e8f323ded3a69711160c5b814fb95f5904. Base7d3bc8879c39d32f34866df5098a0d369821db03; normal merge7a464e3d1d61458898183a930ec29949885ee50c preserves incoming main's own60→120 build timeout. Observed later main16bdab230d5906197cee718b25a789606976b707; preserve it when future work needs merging. Current proposed tuple is older4.43.6/725/Platform1.1.7/ed, both ordinary/manual v2 refs725.

Initial126 proposal's16 paths preserve all native functional assertions, declaration/controller/fixture bytes, prior48,457ledger byte prefix/387matrix rows and enrollment. New SDK diagnostic distinguishes already-loaded native process from copied composition output; neither is backend host loading. Source/control verification13scrubbed/69caller/3selection/32lifecycle/7declaration and three Release/package builds passed. Currentc10 adds only direct owner depth1 checkout after an actual10minute clone timeout; production budgets and all validation remain.

Actualc10 CI41718277171/attempt1 completed main failure. All15 isolated reports:6307total/6294executed+passed/0failed/13notExecuted. Allsix independent jobs passed. Corrected declaration126204481406 passed in203seconds under unchanged10minute limit: fourmanifestvalidate, generatorcheck, all7strict tests. [Hosted declaration audit](evidence/2-8/release-phase/remote-resumption-20261103/parent-parties-c10-declaration-hosted-audit.json). ActualUI126204481357 passed68clean/0retry/0skip, [UI audit](evidence/2-8/release-phase/remote-resumption-20261103/parent-parties-c10-ui-hosted-audit.json). Prior126 run's67clean+1flaky remains separate evidence.

Main126204481623 reached required native invocation but old Platformed threw HXP020's unconditional package refusal **before services**. Owner8ecbd1faf0d3155d53084c48474b6919, attempt15ff4eda68f3d4a721a0134320e23e51, environmentac27c04a5f272b664b589dc0c51e5fb2. Exact finalizer succeeded with empty inventories; it proves receipt-bound metadata recovery, not real service teardown or native acceptance. See failed artifact audit. No new P mutation during the current component task.

Manual lifecycle PR60 was externally merged, finalheade42448ab0a94f88d2f92436c8c023edf56f5d223/mergef1f33f8de655d9aa9307bafba128b0c7ea5c2570. Its18 workflow/controller/fixture paths are preserved in P. Do not reopen or duplicate it. Controller persists private atomic journal/source/mapping/context/receipt intent before launch, 30s pipe gate captures real PID/birth/group before releasing CLI, and SIGTERM/bookkeeping recovery must always finalize exact owner/siblings. Real fixture preserves canonical CreateNew observation, bounded240s hold and post-hold real persistence assertions. Startup signal proof uses live Starting; execution proof requires Ready + live process identity + original unexpired finite hold. First outcomes/time/expiry immutable; invalid phase proof still cleans but earns no credit.

Only after coherent real E/P native runs: sequential actual matrix/startup-cancel/execution-cancel. Before cancellation, download THAT exact run's readiness artifact, verify source/run/attempt/job/issued owner/current phase/original unexpired hold; retain actual normal cancel API request/response/UTC. Never cancel unrelated runs. Verify real pass/fail/cancel/partial startup, same-job admission/cross-job+completed refusal, withdrawal/report before first stop, unknown non-success/exact retry/unaffected peer real data, zero owned process/container/workspace/claim leftovers and no discovery uncertainty. No holds/retention past job completion. No manual dispatch yet; full Story2.8 matrix/native acceptance remains incomplete.

### Memories3.0.23: authenticated observations and causal question

Genuine release433463293/tagv3.0.23/source82c0aa0a7046d7bd91569ac1c9a38a3ed0386d0f, published2026-11-04T03:02:47Z. Normal Release41762624725 and exact-source CI41760685661 succeeded. Official evidence asset708018915 SHAb58e599aaf2c61a19256a6f97293b096ff832fe53d3a08c19e114e1ddc8768b8, setSHAb559cf12b2263f8d9a47852789eb6aa5882efd7531cbc342d7c412ba90e36ea1. Private carrier `/home/administrator/.cache/hexalith/story-2-8/memories-3023-release-20261104/evidence`; component cache `/home/administrator/.cache/hexalith/story-2-8/memories-3023-component-20261104`.

[Component checkpoint](evidence/2-8/release-phase/remote-resumption-20261103/memories3023-component-checkpoint.json) and16 supporting inventories verify123carrier files, allten public repository-signed packages, unsigned asset payload equality, five live image graphs/source82/version labels/both architecture configs/manifests,11Linuxamd64 compressed+rootfs layers and16static assemblies. Parent reverified all16inventory hashes. No services/source/pins/registry/PR mutations. Image owner DLLs differ from same-source public packages; no substitution/global cache replacement. Arm64 layers were not fetched.

Required default Memories closure includes server+domain+clock+telemetry; Mcp is separate. CurrentM snapshot SHA56f6dc8134d3f7cea06910e923a4736a450e00241e8e4a565ac9226897881840 is byte-identical to released82 manifest. Truthful future metadata adoption can select3.0.23/source82 without inventing declaration changes. Current catalog/provenance remains3.0.18/source668.

Actual ordinary images omit Hexalith.Memories.EventStoreExtensions; public signed3.0.23 extension exists with RouteCatalogAdapterSource/Create. Domain csproj admits it only in a non-publishing hosted-test package profile. Actual server/domain SDK DLLs are3.132.2+7c93a1234d14c66f5ee85ed48caaee1d30a671f3, outside current admitted4.0.4 byte cohort. **Positive static IL confirms Domain Main catalog registration and SDK finite receiver calls are present.**

**Do not equate image adapter omission with a proven runtime dependency.** The earlier checkpoint calls it a blocker, but parent found a narrower architectural question: unchanged PlatformRoutingCatalog generates the adapter from public package output and gives its paths to the gateway; the SDK7c93 DevelopmentDomainRouteCatalog source supplies empty domain admission, reads compiled owner contracts/installed targets, and does not invoke adapter factories. Domain image absence alone may be expected. Parent actual SDK Add method decompilation/IL confirms bootstrap delegation but has not yet inspected its async capture lambda. SDK3.132.2 source DevelopmentDomainRouteCatalog is byte-identical to current SDK source at the inspected method file. Published package reader/receiver compatibility still needs proof. Preserve original observations and correct only causal interpretation with evidence; do not force an owner release or speculative SDK migration from a version string. Existing finite admission remains closed.

### Next task for the same sequential implementation agent

Parent prerequisite audit completed during this component task: current Builds main d39844a16ecf522c0d25efd0f53b31f7a3dfe423 selects FrontComposer4.7.6 (other E/T/M/P versions unchanged). Genuine FCv4.7.6/sourceb73040064778943bbbfb0b42d3bd4e66b24129d0/release433368526 was produced by successful normal Release41744125726 and exact-source push CI41743236543. Parent verified all eight public repository signatures offline, actual source/version metadata, all149unsigned release/public payload members and the retained release manifests. [Official package audit](evidence/2-8/release-phase/remote-resumption-20261103/parent-frontcomposer-476-official-package-audit.json). The sealed manifest's pending publish status is a pre-publication observation; actual public signed bodies were independently fetched. Its aggregate6333producer tests were reported, not independently re-counted from TRX. No served asset/runtime/UI parity or unsupported attestation claim. This authenticates a possible later coherent current-main catalog adoption; it does not change the current Platform1.2.1/Builds4.44.0/FC4.7.5 tuple or authorize pin edits in this read-only task.

Perform a bounded **component qualification of the existing signed Memories3.0.23 and Parties3.0.0 full-cohort prerequisites**, resolving whether any actual owner publication change is necessary before proposing it. This is read-only for production source, accepted pins, registry and PRs. Parent continues the old EventStore CI and owns eventual push of the prepared a0c commit.

1. Trace the actual adapter/receiver dependency through unchanged Platform728 catalog reader/bootstrap, actual public SDK4.0.4 gateway and actual image SDK3.132.2 receiver. Inspect the compiled async capture path and relevant exact source/API where useful with dotnet-inspect. Determine precisely which process consumes the extension factory and whether the ordinary domain image requires that assembly. Do not repeat the prior image omission inference as a proven blocker.
2. Using isolated task caches and the existing authentic published artifacts, run meaningful compiled catalog/intent-factory comparisons for Memories3.0.23 under the actual public SDK4.0.4 reader. Use the unchanged released-source reader's existing component interface; do not modify its production finite cohort guards or manufacture an authenticated run proof. If a component request leaves qualificationCohort unset because it is not an admitted production profile, explicitly label that output **unadmitted component evidence**, with authorityRegistered/nativeAcceptance/candidateAcceptance false. Capture actual resolved package assets, loaded DLL identities/hashes, actual commands/exit codes and catalog byte/digest/route/policy comparisons. Distinguish public package and image owner bodies and preserve first failures. Check semantic compatibility using real compiled declarations/factories, not version strings or fabricated JSON equality.
3. Investigate the actual existing Parties3.0.0 published backend closure with the same care: official tag/source/public signed package family and required live image manifest/config/source linkage/layer/application bytes; catalog registration and real finite receiver. Reuse authenticated cached evidence when it already proves these facts, but independently verify relevant retained hashes and current immutable registry bodies with existing reader custody. Do not assume P has the same source-provenance or extension requirement as M. Preserve all required Tenants/Memories/Party services, declarations and real Party fixture operations. Test a compiled combined owner catalog only where public artifacts and resolved SDKs make it meaningful.
4. If all existing bytes qualify at component scope, report the minimum finite authenticated normal Builds catalog/Platform provenance/admission extension and later real-runtime qualification needed for full Parties and attachment semantics. If an actual API/artifact/declaration/runtime-protocol gap is demonstrated, identify exact owner source/path/body/failing behavior and the minimum normal prerequisite. Do not patch owner source, publish/retag images, drop required owners, enable a test-only/source backend, relax schema/policy, remove guards, start a full backend/service topology, fabricate CI identity or claim real native/parity/candidate acceptance in this task.

Write compact readable checkpoint, actual command/results/hash inventories, first failures and explicit limits in the shared evidence directory; large binaries/inspection output remain private. No additional agents, E/P source mutation, new publication, accepted pin changes, manual lifecycle dispatch or step04. Keep the agent available for the later concrete implementation/review handoff.
