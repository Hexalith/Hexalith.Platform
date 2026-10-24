---
title: '2.4 Provision a run-owned environment for a test suite'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '543ea182bd83c813609c7df0491d1419b7f1f99e'
builds_baseline_commit: '66b9136a0b223925e51177a51eb48852e2f12787'
story_key: '2-4-provision-a-run-owned-environment-for-a-test-suite'
context:
  - '_bmad-output/implementation-artifacts/epic-2-context.md'
  - 'docs/contracts/test-environment-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Make `hexalith-module test` provision fresh real services for a declared suite or compatible batch, enforce readiness, and hand tests a validated Story 2.2 descriptor.

## Boundaries & Constraints

Reuse the active mapping, authoritative declaration closure, pinned catalog, isolated workspaces and Story 1.8 readiness/tasks. Preserve source/Debug and package/Release boundaries. Keep independent runs concurrent; refuse actual exclusive-resource conflicts with the blocking run ID. Never attach implicitly or reach hosted application data.

Startup defaults to 600 seconds from the environment request, with finite justified declaration overrides. Start execution only after readiness; batches have a finite combined bound and each entry's configured limit. Persist timing before launch; preserve immutable selection/source hashes and legacy evidence bindings. Adapt certificates and gates consistently.

Support one or repeated `--profile` values from the same manifest. Share one environment only when mode, graph and profile-specific resource/runtime requirements agree; execute entries sequentially. Refuse incompatibility before startup.

Supply absolute `HEXALITH_TEST_ENVIRONMENT_PATH` to a private, credential-free v1 descriptor. Bind real run/attempt/owner/environment identities, composition content, verified catalog commit/instance, synthetic isolation, observed endpoints, readiness and startup-task results. Validate before handoff. Keep unavailable proof `not-yet-populated`; validity grants no candidate acceptance.

Exclude attachment admission, lifecycle-policy redesign, CI orchestration, domain fixture migration, publication and pins. Preserve independent edits and applications; verification may stop only its owned resources.

## I/O & Edge-Case Matrix

| State | Behavior |
| --- | --- |
| Ready suite/compatible batch | Fresh environment; descriptor before child launch. |
| Invalid selection/incompatible batch | Refuse before allocating services. |
| Managed exclusive claim held | Fail promptly and name owner; independent allocations coexist. |
| Readiness/task failure or startup timeout | No test child; preserve bounded diagnostics and startup timing. |
| Missing catalog identity, invalid observation or descriptor | Refuse handoff; never invent values or fall back to legacy ABI. |

</frozen-after-approval>

## Code Map

Builds paths below are relative to `references/Hexalith.Builds/`. `R/` means `src/libraries/Hexalith.Builds.Tooling/Runtime/`; `T/` means `test/Hexalith.Builds.Module.Tests/`.

- `R/CompositionEngine.cs` enforces Story 1.8; `R/CompositionDeclarationClosure.cs` selects dependencies.
- `R/ModuleCommandExecutionService.cs` currently charges setup to execution. Keep `ModuleTestSelection` source/evidence bindings; add actual execution timing separately.
- AppHost `RunReadiness.cs` reports only EventStore/UI URLs; Platform server annotations supply ownership.
- `src/libraries/Hexalith.Builds.Tooling/Workspace/CatalogVersionMaterializer.cs` verifies the authoritative catalog snapshot/commit.

## Tasks & Acceptance

**Execution:**

- [x] `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, `R/ModuleCommandExecutionService.cs`, `R/ModuleTestSelection.cs` — accept suites/batches, validate compatibility and coordinate one fresh environment.
- [x] `R/CompositionStartupRequest.cs`, `R/ModuleTestExecutionBudget.cs`, `R/CompositionEngine.cs`, `R/CompositionRunPlan.cs`, `R/ModuleTestRunRecord.cs`, new `R/ModuleTestExecutionTiming.cs` — separate clocks; persist activation without renewing active timeouts.
- [x] New `R/CompositionResourceClaimStore.cs`, `R/CompositionPortAllocator.cs`, `R/CompositionRunStateStore.cs` — acquire exclusive runner claims atomically before launch; expose blocking ownership and release through existing ownership-limited teardown. Do not lock solely by manifest/composition.
- [x] `R/CompositionReadiness.cs`, `R/CompositionResourceReadiness.cs`, new `R/CompositionStartupTaskResult.cs`, `src/hosts/Hexalith.Builds.Module.AppHost/RunReadiness.cs`, `RunEndpoints.cs` there — capture ownership/endpoints and task results; never infer observed endpoints from proposed ports.
- [x] New `src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentDescriptorWriter.cs`, `R/NativeTestHandoff.cs`, `R/NativeTestExecutor.cs`, `R/CompositionSelectedTestGate.cs` — write/validate the descriptor and hand off its path; enforce actual execution deadlines and existing credential protections.
- [x] Platform `src/Hexalith.Platform.Composition/PlatformRetentionProfile.cs`, `tests/Hexalith.Platform.Composition.Tests/PlatformRetentionProfileTests.cs` — adapt pre-start certificate validity to the startup ceiling plus execution budget; retain actual gate expiry checks.
- [x] New `T/TestEnvironmentProvisioningTests.cs`, `T/CompositionResourceClaimStoreTests.cs`; existing `T/ModuleTestSelectionTests.cs`, `T/StartupCoordinationTests.cs`, `T/StartupReadinessTests.cs`, `T/NativeTestExecutorTests.cs` — cover every matrix row, descriptor reader acceptance, batch compatibility, delayed readiness preserving execution time, source drift and concurrent ownership.
- [x] New Platform `tests/Hexalith.Platform.Composition.Tests/PlatformTestEnvironmentProvisioningTests.cs` — gated real-service proof: declared suite consumes descriptor, writes/reads run-isolated state, and cannot read its peer's data.
- [x] Builds `README.md`, Platform `docs/contracts/test-environment-v1.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — document handoff, sync Story 2.4; retain `evidence/2-4/`.

**Acceptance Criteria:**

- Given declared suites, when the command runs, then readiness and required tasks pass before descriptor handoff and test execution.
- Given another run's conflicting claim, when starting without attach, then refusal names its owner without disturbing it.
- Given two fresh environments, when suites persist identical test keys, then each observes only its own synthetic data through its descriptor endpoints.
- Given a supported declared composition, when testing, then the v1 descriptor validates and `HXR003` never occurs; unsupported legacy ABI refusals remain.

## Implementation Notes

- Declared suites and compatible repeated profiles now share one freshly owned environment and execute sequentially. Selection and legacy evidence bindings stay immutable; actual execution timing is persisted separately after readiness and required tasks.
- Runner-managed TCP claims are acquired atomically, report the conflicting owner, and release only after ownership-limited teardown. Observations carry allocated endpoints, module ownership, readiness timestamps and actual required task results.
- The descriptor writer rechecks selected source, authoritative closure and verified catalog material, validates with the current Story 2.2 reader, and hands an absolute private credential-free path to native children. Unknown candidate/tool/artifact/suite proof stays unpopulated.
- Platform certificate validity covers the startup ceiling plus finite execution budgets. Selected source project builds use run-owned output directories to prevent shared output races.
- The gated technical fixture uses its real minimum dependency closure (Platform + EventStore and owned infrastructure), two concurrent suite commands, actual Redis persistence, descriptor-derived endpoints, and explicit same-ID cleanup. Native environment inheritance remains unchanged.


## Spec Change Log

- 2026-10-22: Implemented all tasks and moved to review. Frozen intent is unchanged. Retained the broader reference-domain startup failure independently from the passing minimum-closure provisioning proof.


## Review Triage Log

### Round-3 completion workflow review, 2026-10-23

All three independent layers completed before triage: ten Blind Hunter findings, one Edge Case Hunter finding, and no Verification Gap findings. Parent inspected the full-baseline inventory, cumulative provisioning runtime diff and the complete 374-line resumption source delta, then verified each new claim. The all-baseline patch contains independently delivered work and historical evidence; current attribution is recorded per finding.

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| Completion-B1: Terminal readiness FIFO | high | Defer. Parent reproduced the compiled-assembly hang after a 200 ms cancellation (still blocked at 1.2 s). The exact readiness.json read exists at Builds baseline 66b9136a0b223925e51177a51eb48852e2f12787; this is inherited metadata I/O, separate from the corrected startup-task/timing paths. |
| Completion-B2: Detached entry descendants | medium | Defer. The batch loop awaits each native command, but the unchanged baseline CompositionProcess.RunCoreAsync successful-exit path does not establish descendant quiescence when streams are detached. Run-scoped final cleanup remains; stronger per-entry process supervision is inherited lifecycle work and needs its own ownership/termination policy. |
| Completion-B3: Batch readiness snapshot | low | Reject. Carried: the prior round-2/round-3 disposition remains. Descriptors truthfully expose the original startup observedAt; re-observation between entries requires a new AppHost query mechanism and guards an uncommon service-breaking suite. |
| Completion-B4: Unknown resource owner | false | Reject. The production owner is selected only from the loaded declarations; server owners come from their loaded declaration and Memories ownership is null unless its declaration exists. Writer handoff consumes runner-held readiness, including across batch entries. No production path producing the alleged arbitrary nonempty owner was established; fabricated public API input is not that path. |
| Completion-B5: Observation chronology | false | Reject. Required tasks complete before the engine requests and accepts a matching post-task refreshId. RunReadiness stamps the actual observations, and the writer uses the retained session rather than child-rewritten metadata. The proposed inconsistent session is not produced by this causal readiness path; no false-ready production handoff was shown. |
| Completion-B6: Uncancelled task-results write | low | Reject. A stalled filesystem can delay the non-deadline bookkeeping write; normal atomic private-file writes do not demonstrate that condition. Its purpose is preserving expired-task evidence after the startup token cancels. Adding another allowance/exception path is more than a direct correction for this uncommon filesystem failure. |
| Completion-B7: Claim release exception | low | Reject. Carried: R3-B6 already records the same ReleaseOwned exception path and rejects the uncommon >2-second lock contention/deletion-failure guard under the low-finding rule. The code and claim are unchanged. |
| Completion-B8: Malformed durable claim | low | Reject. Carried: the round-3 rejection records the same ClaimedPorts failure. Claims publish complete documents atomically; damage or a foreign JSON file is required, and isolating those failures adds recovery guards. |
| Completion-B9: Archive parent replacement race | low | Reject. The static path policy rechecks ancestry before creation and rename, but cannot make pathname operations atomic against an actively racing child. That child already runs with the same filesystem privileges; this is an uncommon deliberate replacement, and directory-handle publication adds a new mechanism rather than a direct correction. |
| Completion-B10: Superseded evidence entry point | low | Patch. Confirmed the main evidence README still called 426/37 results final. The implementation agent directly corrected the opening to the verified 553/44 results and review-round-3 links, retaining parent-final as history. Link/count and whitespace checks passed; no runtime source changed. |
| Completion-E1: Terminal readiness FIFO | high | Defer. The edge layer independently identified the same synchronous readiness.json open as B1. Parent reproduction and baseline comparison verify both the hang and its pre-existing attribution; group only after recording this separate verdict. |

After the individual verdicts, B1/E1 were grouped as one inherited metadata-read defect; B2 is separate inherited process supervision. Both are recorded in the deferred-work ledger. B10 was corrected directly in the evidence index. No intent gap, bad specification or new runtime patch remains. The parent independently reproduced the readiness FIFO hang and confirmed its exact read at the original Builds baseline; this limitation is not presented as fixed.

[Review dispositions](evidence/2-4/review-round-3/workflow-review.json), [parent FIFO reproduction](evidence/2-4/review-round-3/readiness-fifo-parent-reproduction.json), and [acceptance audit](evidence/2-4/review-round-3/parent-acceptance-audit.json) preserve the evidence. Runtime/test source hashes and frozen intent remain unchanged, so the existing green Builds, Platform and live invocations continue to bind the final implementation. The documentation-only review correction was checked by links, recorded counts and whitespace.

### Resumed workflow review, 2026-10-23

All three independent layers completed before triage. Each raw finding is recorded individually below, before grouping. The full baseline diff is retained at `/tmp/story24-full-afj6m61s.patch`; it includes unrelated committed work and evidence. Parent inspected the resumption source patch separately and independently validated the matrix, 540-case coverage union, actual archived live descriptors, full execution intervals and cleanup. All 58 recorded source fingerprints match.

| Finding | Verdict | Route and verified evidence |
| --- | --- | --- |
| R3-B1: ordinary closures lack a Platform infrastructure owner | false | The owner explicitly adopted actual declaration attribution with unavailable ownership refusing handoff. The frozen matrix requires refusal for invalid observations and forbids invented identities. Ordinary closures currently lack that required proof; the contract and qualification report state this limitation. Keep the resolved policy rather than infer a different owner or claim ordinary-closure qualification. |
| R3-B2: archive destination follows child-created parent links | high | Patch. `WriteSnapshotAsync` creates directories and renames through child-writable `native-tests/<entry-hash>` parents without checking links. Reject redirected destination ancestry using the existing owned-path policy; verify an outside sentinel stays unchanged. |
| R3-B3: new startup-task metadata input can block on a FIFO | high | Patch. `CopySelectedMetadata` adds `startup-task-results.json` then uses synchronous `File.ReadAllBytes`. The edge reviewer reproduced an indefinite open against the built assembly. Reuse the existing nonblocking held-handle primitive and finite metadata checks within the original cancellation bound. |
| R3-B4: earlier entry loses inspected functional observations | medium | Patch. `ReadObservationsAsync` deletes its input after returning safe bytes; `ArchiveSelectedAsync` writes only `ReportBytes`, and the next batch entry replaces the result. Retain the already-inspected `ObservationBytes` alongside each entry's report. |
| R3-B5: refused entry described as executed | low | Patch. The loop adds the name before source/descriptor validation and process startup. Correct the metadata-only wording to attempted/not attempted, with a refusal-before-launch assertion; no new launch-state API is needed. |
| R3-B6: claim release exceptions escape teardown | low | Reject; carried prior resolved low disposition. The store can throw after more than two seconds of transaction contention or deletion failure, as the earlier review recorded. This uncommon path requires another exception guard; it is not a direct correction under the workflow's low-finding rule. |
| R3-B7: contract reader opens a FIFO before checking seekability | medium | Defer. Verified `TestEnvironmentContractReader.Read` opens synchronously before its guard. This is the independently delivered Story 2.2 reader, already present before this resumption; its owner should reuse nonblocking open and test refusal. |
| R3-B8: native diagnostics redactor can retain short PEM body lines | medium | Defer. The concurrent redactor has no cross-line state, and an isolated short body line matches neither configuration nor opaque-value filters. The independent diagnostics owner must define/test multiline block suppression. |
| R3-B9: checkpoint loading has no aggregate payload budget | medium | Defer. The independent custody decoder bounds count, individual bytes and concurrent reads but retains all payload arrays, so valid large checkpoints can exhaust process memory. Production custody is outside the provisioning change. |
| R3-B10: native-result stream might exceed advertised length | maybe-false | Defer, medium if true, unverified. The custody method copies before checking the reported length, but the code alone does not establish that the production AWS response stream can exceed its HTTP content-length boundary. Verify the SDK/transport behavior or reproduce with a supported production provider before assigning confirmed harm. |
| R3-E1: startup-task FIFO blocks terminal recording | high | Patch; same verified root cause as R3-B3. The reviewer invoked the built private copy method with a FIFO and observed external timeout exit 124 after three seconds (`/tmp/story24-edge-fifo-kgeio3qm`). |
| R3-E2: Memories closure refuses descriptor handoff | false | Same refutation as R3-B1: the approved explicit-attribution policy intentionally refuses absent ownership proof. The source technical closure is qualified; ordinary closures are explicitly not qualified. No ownership fallback or declaration graph change is authorized by this finding. |
| R3-V1: successful package handoff has no test | medium | Defer. Trust the filed regression-gap evidence. The current authoritative `DefaultReleaseSnapshotHashes` is empty, so package closure validation refuses before a valid ready package session can exist. A positive fixture cannot supply current approved release authority without bypassing that guard or changing release/pin inputs, which frozen intent excludes. Add the positive consumer test when the owner releases and pins its v2 declarations; preserve package refusal and no package qualification claim now. |
| R3-V2: production diagnostics registration has no output assertion | medium | Defer. Trust the filed regression-gap evidence. Tests manually register capture while the actual composition test inspects arguments rather than emitted files. This subscriber registration is separately attributed concurrent diagnostics work, not the Story 2.4 implementation. |

Grouped patches: archive destination ownership (B2), bounded metadata capture (B3/E1), inspected per-entry observation custody (B4), and truthful attempt diagnostics (B5). No intent or specification change is required; fixes reuse existing ownership, inspection, cancellation and diagnostic mechanisms without adding public surface.

All four grouped patches are corrected and independently verified. The final parent invocation passed all 548 Builds cases, including five destination-link cases, real startup-metadata FIFO refusal, two-entry observation archives and both attempted-entry diagnostic cases. The complete Platform, schema and live checks were rerun afterward; see [final parent evidence](evidence/2-4/workflow-final/README.md). The six deferred findings are separately recorded; the approved ownership decision and frozen intent are unchanged.

All three independent layers completed before triage. Each finding was assessed individually; duplicate outcomes are grouped only after the verdicts below. Corrections use existing contracts, deadlines, metadata and refusal paths, with no new public API or changed frozen intent.

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| Blind 1: self-owned secondary port conflict | medium | Patch. Child allocation claims before `AllocateSecondaryPorts` excludes persisted unbound ports; no parent-process port history protects the child. Claim only the accepted non-overlapping allocation. |
| Blind 2: earlier entry native reports disappear | medium | Patch. Per-entry `native.trx` is written but omitted from durable metadata copying before workspace deletion. Retain every inspected report. |
| Blind 3: first entry stopping bound reused | medium | Patch. Compatibility allows distinct stop limits, but final processing always uses the initial `selected`; use the active entry's existing limit. |
| Blind 4: mutable/FIFO descriptor archiving | high | Patch. A child can replace its known descriptor path before uncancellable `File.Copy`; snapshot validated bounded bytes before launch and archive the snapshot. |
| Blind 5: non-600 unoverridden fallback | medium | Patch. `ReadinessTimeout` can reach the writer with `IsOverride=false`, where v1 requires 600. The approved policy requires a justified declaration override; refuse this unsupported handoff configuration before provisioning rather than fabricate justification. |
| Blind 6: interrupted claim publication | low | Patch. CreateNew exposes a partial owner document if the process ends during serialization, which owner-limited cleanup cannot decode. Atomic private temporary-file publication is a direct correction under the existing transaction. |
| Blind 7: incomplete batch legacy evidence | medium | Patch. The legacy artifact has one timing/report/count binding and cannot prove all entries; its validator also permits a list omitting the canonical selection. Full suite/candidate proof remains unavailable. Refuse legacy evidence export/validation for multi-entry batches before provisioning, and validate membership for represented selections; keep ordinary batch execution and diagnostic reports. |
| Blind 8: renewed post-processing timer | medium | Patch. The fresh five-second archival timer begins after report processing has used the stop budget, and exceptions replace the child outcome. Use the original remaining bound and preserve causal outcomes/stopping timestamps with evidence diagnostics. |
| Blind 9: unsupported CI refused after startup | medium | Patch. Missing actual CI metadata is discovered only by the writer after services start. Validate supported actual execution context before provisioning; do not invent metadata or add CI integrations. |
| Blind 10: cross-process claims unverified | medium | Patch. Existing contention uses tasks in one process, while the store's guarantee is across processes. Add a subprocess contention and interrupted-lock-holder check using existing production entry points. |
| Edge 1: self-owned secondary overlap | medium | Patch; same verified allocation ordering as Blind 1. |
| Edge 2: mixed stopping limits | medium | Patch; same active-entry bound defect as Blind 3. |
| Edge 3: per-entry report loss | medium | Patch; same omission as Blind 2. |
| Verification 1: activated gate deadline absent from tests | medium | Patch. Filed evidence verifies every direct gate test uses the legacy deadline; add delayed activation and controlled renewal/expiry assertions. |
| Verification 2: engine claim release absent from normal tests | medium | Patch. Filed evidence verifies engine cleanup tests seed no claims; assert successful release, peer preservation and retention after incomplete cleanup. |
| Parent live verification: shared provider restore collision | medium | Patch. Concurrent actual suite commands exposed shared composition-provider `obj/project.assets.json` despite separate final output directories. Added the existing `--artifacts-path` option under the run workspace; preserved assembly location, catalog, configuration and startup deadline. A real concurrent three-project regression passed both mode cases, followed by a passing fresh two-environment proof. |


## Design Notes

No intent gaps or irreversible changes. Dispatch spans runner, observations and handoff. Allocation probes are not reservations. Source qualification grants no package-mode CI or candidate qualification.

## Verification

Build Builds Module.Tests and Platform Composition.Tests individually in Debug with `-m:1`; invoke xUnit assemblies with focused `-class` filters, then affected regressions. Run schema parity and gated real-service proof. Record commands, full revisions, descriptor/timing/data and owned cleanup. Report blockers separately; model assertions cannot replace live evidence.


### Implementation verification, 2026-10-22

Evidence index: `evidence/2-4/README.md`; machine-readable results: `evidence/2-4/verification.json`.

- Builds Debug build: passed with zero warnings/errors. Serial affected regressions: **370/370 passed**. Final descriptor endpoint guard focused checks: **75/75 passed**. Production behavior did not change afterward.
- Platform Debug build: passed with zero warnings/errors; affected retention/build-isolation regressions: **29/29 passed**. Final fixture-only rebuild passed with one existing project-graph MSB3277 Redis assembly-version warning, retained in its log.
- Current Platform and Builds environment/result schemas are byte-identical, with hashes recorded in `schema-parity.json`.
- Gated real-service proof: **1/1 passed** in 101.923 seconds. Both fresh runs reached healthy readiness, passed the declared startup verifier, consumed validated descriptors, persisted identical keys, read their own values and could not see the peer's unique key. Actual execution received the full configured 120 seconds after approximately 96 seconds of startup in one run. Neither command produced `HXR003`.
- Final exact-owner audit covers all six run IDs from three attempts: no owned containers/processes, run workspaces, state files or JSON claims remain. The passing attempt's explicit cleanup receipts both succeeded on their first retry-safe call.
- Preserved failures: the six-server reference domain composition exited before healthy (`HXR025`); a fixture-only evidence transport mistake occurred after successful readiness/handoff; initial `HXR028` stop receipts were retained and same-ID retries succeeded. An eight-second startup-task fixture was made phase-specific with a sentinel and finite 15-second budget; the final serial regression run passed.

This is local source/Debug qualification of provisioning. Broader domain startup, package execution, CI, attachment admission and candidate acceptance are not claimed. Independent changes and all pins are preserved.

### Parent acceptance audit, 2026-10-22

The complete baseline diff, including concurrent work, is retained at `/tmp/story-2-4-full-6hmyocln.patch`. Parent inspected the complete Story 2.4 implementation diff at `/tmp/story-2-4-code-m9jjj68q.patch`; concurrent Story 2.2 schema/reader changes and shared documentation hunks remain separate. Approved frozen intent fingerprint is unchanged. All implementation handoff source hashes match.

| Matrix row | Executed coverage | Result |
| --- | --- | --- |
| Ready suite/batch | `ReadyBatchHandsValidatedDescriptorsToSequentialChildrenAfterStartupAsync`; live declared-suite proof | Passed in 75 focused checks and live 1/1. |
| Invalid/incompatible batch | `IncompatibleBatchNeverStartsAsync` runtime/duplicate/missing cases | Passed in 75 focused checks. |
| Managed exclusive claim | `ConflictNamesOwnerAndRollsBackOnlyTheRejectedAcquisition`, `ConcurrentAcquisitionsHaveExactlyOneWinnerAsync`, `ChildConflictPreservesTheBlockingOwnerAsync` | Passed in 75 focused checks. |
| Readiness/task failure or timeout | `StartupReadinessTests` and `StartupCoordinationTests`; retained live startup refusal | Passed in serial 370; failures never yield a ready session or child. |
| Missing catalog/observations/descriptor | Ten `MissingProofNeverLaunchesAChildAsync` cases | Passed in 75 focused checks. |

Parent independently parsed the actual logs: 370/370, 75/75, 29/29 and live 1/1, each with zero errors/failures/skips/not-run. Inspected both positive descriptors, UTC execution intervals, startup tasks, data receipts and successful explicit cleanup receipts. All six exact-owner cleanup audits are empty; no `HXR003` appears in either positive command. Machine-readable parent checks are in `evidence/2-4/parent-verification-audit.json`.


### Review corrections and final parent verification, 2026-10-22

All review findings routed to patch are corrected. No findings were deferred, and frozen intent remains unchanged. Secondary allocation claims only accepted non-overlapping ports; claim documents publish atomically. Native execution retains validated pre-launch descriptor snapshots and every inspected report within the original processing bound. Mixed batches use the active entry's stopping/cleanup limit. Unsupported fallback/CI configuration and multi-entry legacy evidence export refuse before provisioning; imported evidence checks canonical selection membership. Activated gate and exact-owner cleanup regressions now run normally.

Parent verification exposed one further real concurrency defect: composition provider builds shared source restore intermediates. The one-argument `PlatformManifestCommand.BuildModelAsync` correction places artifacts under each existing run workspace. `PlatformModelBuildIsolationTests.ConcurrentProviderBuildsOwnAllGraphOutputsAsync` executes two concurrent three-project builds in each mode and verifies private graph outputs, unchanged source directories, catalog and configuration. Failed live evidence is preserved at `evidence/2-4/parent-final/live/`.

Final evidence is indexed in `evidence/2-4/parent-final/README.md` and `verification.json`:

- Both individual Debug builds passed with zero warnings/errors.
- Builds affected regressions: **426 passed**, zero failures/errors/skips/not-run, 163.359 seconds. All new review regression cases executed and passed.
- Platform affected regressions: **37 passed**, zero failures/errors; one separate opt-in occupied-listener proof was skipped. Current schema mirror parity: **1 passed**.
- Fresh real-service qualification: **1 passed**, zero failures/errors/skips, 82.865 seconds. Runs `8629727f7f9688e67b86b7a06e4dcf9d` and `927a57797db4d90d6bb817fc7d1cdc8b` each observed six healthy resources and a passed required startup verifier. Startup took 75.54 and 77.53 seconds; each then received the full 120-second execution interval. Both persisted/read their own value and could not observe the peer's key. Neither returned `HXR003`.
- Parent independently validated both retained descriptors and inspected durable entry reports. Final source fingerprints match all **51** reviewed files. Approved frozen fingerprint remains `1f9629e6eaa57fac455129b09e94a9667293a9c5cb167d6403d239d0fbe5d852`.
- Read-only exact-owner resource scanning covers all **ten** run IDs from every attempt: no owned containers/processes, run workspaces, state files or JSON claims remain. Each final positive cleanup succeeded on its first explicit call.

This accepts local source/Debug provisioning for the supported technical closure. The retained broader reference-domain startup failure remains outside this positive runtime qualification; package-mode live execution, CI orchestration and candidate acceptance are not claimed. Concurrent work and pins were preserved. Full and Story 2.4 review diffs were rewritten after corrections at their original temporary paths.


## Code review round 2, 2026-10-23

Scope: platform `543ea182..2048aff` (story code paths, evidence excluded) and Builds `66b9136a..db5abf9`; 51 files, +2285/−98. Review mode `full` against this spec, `epic-2-context.md` and `docs/contracts/test-environment-v1.md`. All four layers (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) completed; 71 raw findings were verified individually, then grouped. The reviewed runtime code is unchanged at current Builds/Platform HEAD, so every location below is current. `B/` means `references/Hexalith.Builds/`.

### Review Findings

**Decision needed** — both resolved by the owner on 2026-10-23 and now tracked as patches.

- [x] [Review][Patch] Resource ownership falls back to the literal `platform` (decision resolved 2026-10-23: option (a), explicit attribution) — `RunReadiness.OwningModule` reflects on `PlatformServerAnnotation`; every resource without it (Redis, placement, scheduler, sidecars, module-declared resources) and any server whose annotation shape changes silently becomes `moduleId: "platform"`, so the writer's blank-owner refusal can never fire and the descriptor binds an owner no declaration supplied ("never invent values"). The `owner` refusal scenario changes `RunId`, not `ModuleId`. Options: (a) explicit attribution — PlatformAppModel annotates every composed resource with its owning module (servers → declaring module, runner infrastructure → the composition's actual platform declaration) and RunReadiness leaves `ModuleId` null otherwise so handoff refuses; (b) keep `platform` for infrastructure but refuse undecodable server annotations; (c) accept and document infrastructure attribution. Recommended (a). [B/src/hosts/Hexalith.Builds.Module.AppHost/RunReadiness.cs:458]
- [x] [Review][Patch] Closed v1 run-evidence schema widened in place (decision resolved 2026-10-23: option (a), document the forward-incompatible additive change; pinned validators move with the tool) — `selectedTestRun` and resource items (`additionalProperties: false`) gained `selections`, `executionTiming`, `moduleId`, `observedAt`, `endpoints`; every new artifact emits them, so validators embedding the previous v1 schema reject artifacts from this tool. Old artifacts still validate. Options: (a) accept as a forward-incompatible additive change and document that pinned validators move with the tool; (b) publish `hexalith.module-run-evidence.v2` for the new fields; (c) omit the new fields from exported evidence and keep them in the durable run record only. Recommended (a). [B/schemas/hexalith.module-run-evidence.v1.json:258]

**Patch**

- [x] [Review][Patch] Independent runs collide on port claims and fail with `HXR031` — `CompositionPortAllocator.Allocate` probes random free ports without consulting the claim store, and both claim sites fail the start without reallocating. A port claimed by another run but not yet (or never) bound looks free; with ~50–60 claimed ports per run in a ~20,000-port range, two concurrent or one retained peer collide on the order of 10–15% of starts, contradicting "independent allocations coexist". Exclude claimed ports and reallocate fresh allocations on conflict; keep `HXR031` for claims that cannot be reallocated. The live proof could not see this because both commands shared one in-process allocator. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:331; src/Hexalith.Platform.Composition/PlatformAppModel.cs:376]
- [x] [Review][Patch] Existing selected-suite report-safety regressions silently stop before the child runs — `ModuleRunnerReviewRegressionTests.Session()` lacks provisioning observations, so `ExecuteSelectedAsync` refuses with HXT009 before launch; `EveryExitInspectsEncodedReportsAsync` `utf16-failed`/`utf16-safe` fail and the other cases pass without a child (a 60 s `token-hang` passes in under a second). The class was outside the recorded 426-test list. Route `Session()` through `TestEnvironmentProvisioningFixture` and assert a child-ran sentinel. [B/test/Hexalith.Builds.Module.Tests/ModuleRunnerReviewRegressionTests.cs:562]
- [x] [Review][Patch] Batch combined budget omits inter-entry overhead — `Activate(Σ execution limits)` starts before entry 1 while stop, report processing (up to min(stop, 30) s), archival and descriptor writing between entries consume it; a later entry is cancelled (HXT007) before its own advertised `deadlines.execution.expiresAt`, `HEXALITH_G4_EXECUTION_DEADLINE_UTC`, token and gate expiry. Bound the batch by Σ(execution + stopping/processing allowances) so every entry receives its configured limit and advertised deadlines match enforcement. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1121]
- [x] [Review][Patch] Startup expiry during source mapping in the test path loses startup timing and uses `HXR025` — the inner `catch (OperationCanceledException)` maps it to a refusal with the AppHost "exited before healthy" code, bypassing `ReportSetupTimeoutAsync`, which run/debug use to emit `HXR021` and preserve the startup summary (matrix row 4). [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1147]
- [x] [Review][Patch] Terminal recording rereads `execution-timing.json` from the child-reachable run workspace — contradicts the stated "never reread child-writable entry files" invariant; when archival is skipped (exhausted processing budget) a child-left FIFO blocks `File.OpenRead` indefinitely, or forged timing is recorded. Use the runner's in-memory timing (session/native result) and fall back to the prior record. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:562]
- [x] [Review][Patch] A failed batch does not identify the failing entry or the entries never executed — the loop breaks on the first failure and the result carries only that entry's generic diagnostics; archives are keyed by hashed names. Add a metadata-only diagnostic naming executed, failed and not-executed entries, with a mid-batch failure regression. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1136]
- [x] [Review][Patch] Setup-phase `HXT007` handling is stale after moving setup to the startup clock — the "during setup" catch can no longer fire (the execution budget is unbounded until activation), and `SetupUsesInitialFiniteLifetimeAsync` still expects HXT007 within 3 s: `early-mapping` now waits the full 600 s and fails with HXR021. Delete the dead catch and rewrite the theory with a short `ReadinessTimeout` asserting the new startup classification. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:773; B/test/Hexalith.Builds.Module.Tests/ModuleRunnerReviewRegressionTests.cs:177]
- [x] [Review][Patch] Engine `HXR031` translation and primary-port claiming are untested — no test reads an AppHost HXR031 document through `ReadStartupFailuresAsync` or proves plan ports are claimed after ready and released after `down`; removing either passes the suite (AC2). Also closes the matching Story 2.3 deferred ledger item. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:331]
- [x] [Review][Patch] Startup task results never reach a writer test — the fixture always uses `StartupTasks = []`; dropping `StartupTaskResults = taskResults` would refuse every real suite with a per-start task while all tests pass. Assert the recorded `sample/verify` result in `StartupTaskReadyRequiresItsArgumentsAsync`. [B/test/Hexalith.Builds.Module.Tests/StartupReadinessTests.cs:288]
- [x] [Review][Patch] AppHost readiness endpoints, module ID and observation times are produced but never asserted — `HealthyReadinessWritesMetadataAndFinalProgressAsync` exercises the code with an allocated endpoint but checks none of the new fields; an annotation shaped like the real `PlatformServerBinding` (including a `-dapr-cli` sibling) is never resolved. [B/test/Hexalith.Builds.Module.Tests/Workspace/AppHostRunnerValidationTests.cs:285]
- [x] [Review][Patch] `ComposeRun` secondary-port claiming call site is untested — only the helper is called directly; reverting `PlatformAppModel.cs:138` passes every test. Also closes the matching Story 2.3 deferred ledger item. [tests/Hexalith.Platform.Composition.Tests/PlatformRunIsolationTests.cs:173]
- [x] [Review][Patch] Evidence validator execution-timing coherence is never exercised against tampering — deleting the `IsExecutionTimingCoherent` clause accepts an extended deadline; add deadline/effective-seconds/configuration-identity/start-before-request/credential-expiry mutations. [B/src/libraries/Hexalith.Builds.Tooling/RunEvidence/ModuleRunEvidenceArtifactValidator.cs:486]
- [x] [Review][Patch] Activated deadline environment values are indistinguishable from legacy values in tests — reverting to `selected.ExecutionDeadlineUtc`/`CredentialExpiryUtc` would tell children a deadline earlier by the whole startup time; assert env deadline == descriptor `expiresAt` and token `exp` in the delayed-start batch test. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestHandoff.cs:96]
- [x] [Review][Patch] Batch refusal branches are untested beyond runtime/duplicate/missing — mode mismatch, retention/non-retention mixing and combined-limit overflow can be removed without failures, losing the refuse-before-startup promise. [B/test/Hexalith.Builds.Module.Tests/TestEnvironmentProvisioningTests.cs:103]
- [x] [Review][Patch] The combined batch execution bound is never exercised — no `ModuleTestExecutionBudget` test proves no cancellation before `Activate`, cancellation after N s, and no renewal by later `Activate`/`Restrict`. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleTestExecutionBudget.cs:11]
- [x] [Review][Patch] The unsafe-endpoint refusal is never reached by tests — the `endpoint` scenario replaces the only EventStore endpoint, so it fails the EventStore match before `SafeEndpoint`; keep the EventStore endpoint and add hosted-host/userinfo/query endpoints so loopback, credential and query/fragment checks are what refuse. [B/test/Hexalith.Builds.Module.Tests/TestEnvironmentProvisioningTests.cs:160]
- [x] [Review][Patch] Explicit `down` wipes batch membership from the durable record — `Selections = _options.SelectedTests` overwrites prior entries with `[]` when a cleanup engine records the stopping outcome; keep `record.Selections` when the options carry none. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:562]
- [x] [Review][Patch] Stale `functional-observations.json` leaks between batch entries — only the report is deleted from the shared `native-tests` directory, so `File.Exists(observationPath)` makes a later entry consume the earlier entry's observations; delete it with the report. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:333]
- [x] [Review][Patch] Descriptor-writer refusal reasons collapse into one generic `HXT009` — the writer's constant messages (source drift, missing catalog, unsafe endpoint, tasks not passed) are discarded; carry the runner-authored message into the diagnostic. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:323]
- [x] [Review][Patch] Batch selection refusals share one message — missing, duplicate, incompatible and overflow selections, and the unrelated supplied-mapping mode mismatch, all report "Select compatible declared suites…"; use distinct constant messages. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1063]
- [x] [Review][Patch] Startup-task results are written with the deadline-bound token — when a task reaches the startup deadline the write throws, skipping the post-loop diagnostic that names the expired task (test commands now pass the startup token); write with a non-deadline bound. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1025]
- [x] [Review][Patch] `credentialExpiryUtc` parsing can escape evidence validation — `GetDateTimeOffset()` throws `FormatException` for a non-date string (format is not asserted by the schema evaluation) while `TryValidate` only catches `JsonException`; use `TryGetDateTimeOffset`. [B/src/libraries/Hexalith.Builds.Tooling/RunEvidence/ModuleRunEvidenceArtifactValidator.cs:492]
- [x] [Review][Patch] Cross-process claim test builds its child without the repository's isolated-build flags — add `-nodeReuse:false` and `-p:UseSharedCompilation=false` as `PlatformManifestCommand` does, avoiding lingering nodes and load-induced 60 s timeouts. [B/test/Hexalith.Builds.Module.Tests/CompositionResourceClaimStoreTests.cs:106]
- [x] [Review][Patch] Run-private project build test can pass vacuously — assert the composed builder contains at least one `ProjectResource` before checking `--artifacts-path`. [tests/Hexalith.Platform.Composition.Tests/PlatformRetentionBuildIsolationTests.cs:57]
- [x] [Review][Patch] Live proof never deletes its temporary workspace — `platform-suite-provisioning-*` (state, claims, suite-proof) remains after every run; delete it after the cleanup audit. [tests/Hexalith.Platform.Composition.Tests/PlatformTestEnvironmentProvisioningTests.cs:24]
- [x] [Review][Patch] Live proof validates suite re-serialized receipts instead of the runner's archived descriptor snapshots, and `persisted` is a hard-coded literal — validate `state/diagnostics/<run>/test-environment.json` and derive `persisted` from the `SAVE` reply. [tests/Hexalith.Platform.Composition.Tests/PlatformTestEnvironmentProvisioningTests.cs:65; eng/test-environment-provisioning/suite.py:82]
- [x] [Review][Patch] `PreStartValidityCeiling` batch-sum branch is untested — only the single-entry branch is asserted. [src/Hexalith.Platform.Composition/PlatformRetentionProfile.cs:242]
- [x] [Review][Patch] Run-private `--artifacts-path` for vstest/mtp selected children is untested — every selected-suite test uses `python3`; assert the argument via the `NativeDotnetCommand` stand-in. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:372]
- [x] [Review][Patch] Contract handoff section misdescribes the proof and carries a story-named heading — it says the gated test "runs two declared suites" (it runs one declared suite in two concurrent fresh environments), titles a versioned-contract section "Story 2.4 runner handoff", and leaves a double blank line before it. [docs/contracts/test-environment-v1.md:248]
- [x] [Review][Patch] Builds README handoff section breaks its wrapping and omits that top-level `test-environment.json`/`execution-timing.json` archives are overwritten per batch entry (per-entry archives are authoritative). [B/README.md:382]
- [x] [Review][Patch] Sprint tracking was not synced — `sprint-status.yaml` shows `review` while this spec said `done`; synced by this review's status update. [_bmad-output/implementation-artifacts/sprint-status.yaml:60]

**Deferred**

- [x] [Review][Defer] `RunEndpoints.Observe` throws for a healthy resource with an unallocated endpoint on every run path, including failure and progress reporting [B/src/hosts/Hexalith.Builds.Module.AppHost/RunEndpoints.cs:63] — deferred: maybe-false, medium if true. Settle by confirming whether Aspire 13.6 can report a resource `Healthy` before all its endpoints are allocated (for example proxyless or sidecar endpoints); if it can, return only allocated endpoints from `Observe` and refuse unallocated ones only in the descriptor writer.

**Rejected**

- `false` — Orphaned claims after a crash before the first state write: `down --run-id <named owner>` scans, deletes the workspace and releases claims even when no state file exists (`CompositionEngine.DownAsync` → `ReleaseOwned`).
- `false` — Same-owner re-acquisition self-conflict: no production path re-acquires claims for an existing run; `--validate-platform` composes with claims disabled.
- `low` — Readiness is not re-observed between batch entries: each descriptor reports the truthful startup `observedAt`; an earlier entry breaking services is uncommon and re-observation needs a new AppHost query mechanism.
- `false` — Timing persisted before descriptor validation: activation actually occurred, `actualDurationSeconds` stays null and HXT009 records that no child launched.
- `false` — No live batch proof and trivially distinct environment/catalog IDs: the required gated proof covers declared-suite isolation only, and distinct runner-issued identities are the asserted property.
- `low` — `suite.py` rendezvous through the runner layout with an 80 s wait: opt-in gated proof that fails loudly; a cleaner rendezvous needs a new handoff channel.
- `low` — Live-proof evidence-directory reuse, prebuilt AppHost assumption, missing `HEXALITH_DAPR_HOME`, and cleanup assertions masking the primary failure: opt-in test, loud failures, fixes add guards.
- `false` — CI environment-variable mutation races: `PlatformManifestTestGrouping` uses `DisableParallelization = true`, so the collection runs alone.
- `false` — Widened certificate/capability lifetime and no aggregate cap: the spec directs pre-start validity to the startup ceiling plus execution budget with gate expiry retained; `SelectBatch` bounds the combined limit.
- `false` — Trivial startup verifier: the fixture task exists to exercise the required-task gate, and its pass is truthfully observed.
- `low` — Plan `EnvironmentId`/`AttemptId` initializers regenerate for pre-change `plan.json`: only legacy plan files read by a new tool; `down` does not use them.
- `false` — Fabricated `attemptSequence`/`attemptId`/catalog `instanceId`: the runner is the issuer of run-scoped identities, there are no retries, and the catalog commit comes from the verified snapshot.
- `false` — Unpinned samples link: the hunk is Story 2.2's concurrent contract edit, not Story 2.4 code.
- `false` — Persisted selection rewritten during batches: each `ModuleTestSelection` stays immutable and the full ordered list is retained; only the active-entry pointer changes.
- `false` — Story 2.2 hunks inside the diff: concurrent committed Story 2.2 work in the shared contract file.
- `low` — `ReleaseOwned` throwing inside teardown: requires >2 s transaction-lock contention or delete failure; fix adds a guard.
- `low` — Corrupt claim files blocking as owner `unknown` and the resulting HXR022 text: claims publish atomically; recovery needs new reclamation handling.
- `low` — Startup overrides with more than seven fractional digits pass pre-provisioning: valid but unrealistic manifest values; fix adds a guard.
- `low` — Failed mapping probe applying the 600 s fallback despite a larger dependency override: rare first-run condition; fix restructures startup-token ownership.
- `low` — Activation more than 1 s after the startup deadline outrunning the certificate ceiling: readiness-to-activation is milliseconds; fix adds a guard.
- `low` — Cross-process claim test's 2 s interruption window: kill-and-exit is far below the window; fix adds a test-only parameter.

Outcome: both decisions adopted as recommended. All 32 patch action items are now complete, in addition to the prior sprint-status correction; implementation is ready for the next parent-owned review.


### Round-2 implementation and verification, 2026-10-23

All round-2 Patch items are complete; frozen intent remains unchanged. Port allocation excludes durable unbound peer claims and retries claim races while preserving existing allocator callers. Explicit resource ownership comes from actual declarations, with a useful refusal when absent. Batch bounds cover execution and stopping/processing allowances; terminal recording uses runner-held timing and preserves membership. Startup timeouts retain HXR021 timing/task diagnostics. Native handoff clears stale observations, reports constant refusal reasons and identifies executed, failed and unexecuted entries. The approved additive closed-v1 evidence-schema compatibility change is documented.

Evidence: [round-2 index](evidence/2-4/review-round-2/README.md), [verification](evidence/2-4/review-round-2/verification.json), [patch mapping](evidence/2-4/review-round-2/patch-to-verification.json), and [independent parent audit](evidence/2-4/review-round-2/parent-acceptance-audit.json).

- Both individual Debug builds passed with zero warnings/errors. Builds verifies all **540** cases through the explicit union of a 539/540 invocation and the corrected 49/49 review-class rerun. The original failure and exact coverage union are retained; no single 540-case pass is claimed. Final handoff/startup checks passed 41/41 and 2/2.
- Platform affected checks passed **41**, with one separate opt-in occupied-listener proof skipped. Current descriptor/result schema mirrors remain byte-identical.
- Fresh technical real-service proof passed **1/1** in 101.140 seconds. Runs `0d85ae12fe7f68f5b60a7a3e8a8664be` and `eb2c5693981cc789b98e7c3bf490c78f` each observed six healthy resources and a passed required verifier, received the full 120-second execution interval after 89.60/95.30 seconds of startup, persisted/read isolated identical keys, and could not see their peer's key. Runner-archived descriptors validate, native/timing observations are retained, and neither command emitted HXR003.
- Both exact-owner cleanups succeeded on their first explicit call. No proof-owned containers/processes, JSON claims, run workspaces or temporary fixture workspaces remain. Earlier ambient-CLI and incomplete-Dapr prerequisite refusals are retained separately; the passing proof reused the approved private 13.6.3 CLI and complete 1.18.7 runtime without changing pins or the global installation.
- All 58 retained source fingerprints and the approved frozen fingerprint are recorded. A narrow independent diagnostics-test CS8198 repair is separately attributed in the owned-hunk map; concurrent recovery/diagnostics functionality is preserved.

Qualification requires an actual selected Platform declaration for shared-infrastructure ownership. The technical fixture includes it; ordinary EventStore/Tenants/Memories closures currently do not and therefore refuse descriptor handoff. Source/Debug technical provisioning is qualified, with no package-mode live, CI, attachment or candidate acceptance claim. The initial independent Tenants AppHost was absent at final read-only inspection; its external lifecycle cause is unconfirmed, and no lifecycle action targeted it. The index records that observation without attributing it to proof cleanup. The next workflow review remains parent-owned.

### Final workflow review and parent verification, 2026-10-23

The resumed workflow review is complete. All 32 prior Patch items and four newly grouped corrections are implemented. Native archives enforce owned, link-free destinations and retain inspected observations per entry. Terminal metadata uses nonblocking bounded reads within the original stopping token. Batch diagnostics now say attempted/not attempted rather than imply a refused child executed. Frozen intent remains unchanged.

Evidence: [final index](evidence/2-4/workflow-final/README.md), [machine-readable verification](evidence/2-4/workflow-final/verification.json), [exact commands](evidence/2-4/workflow-final/commands.json), and [59 final source fingerprints](evidence/2-4/workflow-final/source-fingerprints.json).

- Both individual Debug builds passed with zero warnings/errors.
- One full Builds affected invocation passed **548/548**, with zero failures/errors/skips/not-run, in 194.231 seconds. Every frozen matrix row and every new workflow-review regression executed and passed. The earlier 540-case coverage union and transient fixture lookup failures remain historical evidence, superseded by this complete green invocation.
- Platform passed **41** affected cases with one separate opt-in occupied-listener skip; schema mirrors remain byte-identical.
- Fresh real-service proof passed **1/1** in 68.917 seconds. Runs `929bdb18cf8400a07ea154e00f3a9028` and `eaba106ee9884e9e9d819913a4fe115f` each observed six healthy resources and a passed startup verifier, received the full 120-second interval after 63.75/57.37 seconds of startup, persisted/read isolated identical keys and could not observe their peer's key. No HXR003 occurred.
- Parent independently validated actual runner-archived descriptors, timing and data, inspected successful cleanup receipts, and scanned exact Docker labels and process environment identities. No proof-owned containers/processes remain; claims/workspaces are empty and the fixture directory is deleted.
- All 59 final source fingerprints match; the approved frozen fingerprint remains `1f9629e6eaa57fac455129b09e94a9667293a9c5cb167d6403d239d0fbe5d852`. Six findings concerning independently delivered or release-dependent work were deferred. Workflow completion records local commits in Builds and Platform; no push, package/tool/catalog pin change or unrelated lifecycle operation is performed.

The explicit Platform-declaration prerequisite and local source/Debug qualification limits above still apply. No general ordinary-closure, package-mode live, CI, attachment or candidate qualification is claimed.


## Code review round 3, 2026-10-23

Scope: Platform `543ea182..ae932d0` and Builds `66b9136a..8c9caa7` (current gitlink), restricted to the 58 story source files recorded in `evidence/2-4/workflow-final/source-fingerprints.json` (evidence excluded; `PlatformDaprDiagnosticsTests.cs` excluded because all of its hunks come from Story 4.27); 5,261 diff lines, +3,235/−183. Shared files still carry concurrent Story 2.2/2.3/4.27 hunks that are not judged here. Mode `full` against this spec, `epic-2-context.md` and `docs/contracts/test-environment-v1.md`. All four layers completed (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor); 53 raw findings were verified individually, then grouped. `B/` means `references/Hexalith.Builds/`.

### Review Findings

**Decision needed**

- [x] [Review][Decision] (resolved 2026-10-23: option 1, the selected module's declaration owns shared infrastructure; now tracked as the first patch below) Ordinary closures can never receive a descriptor: shared infrastructure ownership requires a declaration whose `moduleId` is `platform`, and only the synthetic qualification fixture declares one — `PlatformAppModel` gives Redis, sentry, placement and scheduler an owner only when the closure contains a module with ID `platform` (`src/Hexalith.Platform.Composition/PlatformAppModel.cs:347`); otherwise the owner is null and `TestEnvironmentDescriptorWriter` refuses with HXT009. A repository-wide search finds `"moduleId": "platform"` only in `eng/test-environment-provisioning/manifest.v2.json`. At baseline `66b9136a`, a v2 selected suite reached `NativeTestExecutor` after a ready start. Now Memories `functional-retention`, the only real declared suite, spends the full startup and is then refused. Stories 2.9 and 2.10 also need real closures to receive descriptors. The refusal is found only at handoff, after services start, never before provisioning (Blind Hunter). The round-2 decision (a) says "runner infrastructure → the composition's actual platform declaration". In this project's vocabulary a "Platform declaration" is any module declaration that conforms to the Platform declaration schema (Story 1.1), so the implementation's reading — a module literally named `platform` — may not be what the owner intended. Options: (1) attribute shared runner infrastructure to the composition's active selected-module declaration, so real closures qualify; update the contract paragraph and the Tenants null-owner test; (2) keep the `platform` requirement, add a pre-provisioning refusal now, and track a real Platform module declaration auto-included in every closure as follow-up work; (3) accept as documented and record the Memories regression as deferred.

**Patch**

- [x] [Review][Patch] Attribute shared runner infrastructure to the selected module's declaration (decision option 1) — in `PlatformAppModel`, take the infrastructure owner from `options.ActiveModule`, falling back to the selected suite's module, instead of looking for a module named `platform`; servers and Memories stores keep their declaring owners. Update the Tenants test that expects `null`, rewrite the contract paragraph that says ordinary EventStore/Tenants/Memories closures refuse handoff, and verify that a Memories closure gets an owner on every resource. [src/Hexalith.Platform.Composition/PlatformAppModel.cs:347]
- [x] [Review][Patch] Platform owner annotation crosses assemblies by type-name reflection, and the only non-gated test uses a Builds-local stand-in type — renaming the Platform record or its `ModuleId` property silently makes every resource unowned, so every real handoff refuses, while all normal tests stay green. Add a Platform test that runs the same name-plus-property lookup on resources composed by `ComposeRun` and asserts their owners. [B/src/hosts/Hexalith.Builds.Module.AppHost/RunReadiness.cs:458]
- [x] [Review][Patch] The engine-computed `CompositionContentIdentity` is never verified — every handoff test receives it from `TestEnvironmentProvisioningFixture`, which computes it with the writer's own function. Deleting or mis-computing the engine assignment passes every test but refuses every real suite with "Composition or suite sources changed before handoff." Assert it from a ready fake-AppHost start with `SelectedTest` set. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:327]
- [x] [Review][Patch] The repeated `--profile` refusal outside a v2 `test` is untested — dropping it lets v1 `test --profile a --profile b` run only `a` and report success. Add CLI cases for `run` and v1 `test` that expect HXC002 and start nothing. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:141]
- [x] [Review][Patch] The terminal record's `Selection` for later batch entries is unasserted — reverting to `state?.SelectedTest ?? _options.SelectedTest` would record entry `first` next to entry `second`'s timing. Extend `TerminalTimingNeverReadsChildFileAndDownKeepsMembershipAsync` with state holding `first` and options holding `second`. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:539]
- [x] [Review][Patch] v1 `test` setup now uses the startup clock, but no test covers it — `MappingConsumesOriginalDefaultDeadlineAsync` covers run/debug only. If the startup request were restricted to v2, a stalled v1 `test` setup would hang without bound, because the budget no longer cancels itself before `Activate`. Add a `("test", "v1")` row that asserts a bounded HXR021. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:89]
- [x] [Review][Patch] The engine's selected-suite startup-timeout branch (HXR021 with preserved summary) is never reached — the only `SelectedExecutionOwnerToken` test expires inside readiness waiting. Add a `SelectedTest` start whose startup token cancels during `PlatformModelBuildOverride`, and assert HXR021 plus `summary.json`. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:393]
- [x] [Review][Patch] Memories store ownership is never asserted — if the `memories-vectors`/`memories-graph`/`memories-retention-postgresql` branch is deleted, those stores fall to the infrastructure owner and all tests still pass. Assert owner `memories` in `IdenticalSelectionsOwnPortsCredentialsDiscoveryAndStorageAsync`. [tests/Hexalith.Platform.Composition.Tests/PlatformRunIsolationTests.cs:81]
- [x] [Review][Patch] The versioned contract points readers at a process-artifact path ("Evidence belongs under `_bmad-output/implementation-artifacts/evidence/2-4/`"), following the same rule as the round-2 removal of the story-named heading. Evidence directories can be pruned, as the Builds archive removal shows. Drop the sentence. The `tree/main` samples link in the same file comes from Story 2.2 and is out of scope. [docs/contracts/test-environment-v1.md:287]

**Deferred**

- [x] [Review][Defer] `RunEndpoints.Observe` throws for a healthy resource with an unallocated endpoint on every run path [B/src/hosts/Hexalith.Builds.Module.AppHost/RunEndpoints.cs:64] — deferred: maybe-false, medium if true; re-raised by three layers this round and already in the round-2 ledger. New corroboration toward false: the Story 2.3 round-3 live check found that Aspire 13.6.3 `DcpExecutor.PrepareServices` allocates every proxyless endpoint port before resources start. Settle it by observing the reference composition's progress snapshots under run/debug, or by confirming that no 13.6 resource type can report `Healthy` with an unallocated endpoint.

**Rejected**

- `false` — v1 contract changed without a version bump (integer spellings, outcome/cause coupling, `not-yet-populated` identities, HXT106→HXT103): these hunks come from Story 2.2 commits (`dcb01b1`, `01aede5`) on the contract that story owns, made while it was being finalized; none is Story 2.4 code.
- `low` — Closed run-evidence v1 schema widened in place: the owner adopted option (a) on 2026-10-23, and the Builds README documents it.
- `false` — Schema accepts any number of `selections` while the validator refuses more than one: refusing multi-entry evidence is the round-1 decision (batches refuse `--evidence` before provisioning); the extra coherence checks are redundant, not wrong.
- `false` — Memories store names hard-coded in ownership: Platform itself composes those stores only when `hasMemories`, so attributing them to the declared `memories` module is truthful; the missing assertion is a patch above.
- `low` — `memories/functional-retention` special case in batch compatibility: it reflects the only profile-specific resource requirement that exists today (the retention profile); generalizing it is new design.
- `low` — A malformed claim file breaks every `ClaimedPorts` call, and `ReleaseOwned` is unguarded in teardown: claims publish atomically, so corruption needs disk damage or a foreign file; the fix adds guards (teardown part carried from round 2).
- `low` — Claims orphaned by a crash before the first state write: `down --run-id` releases them; the leak needs repeated crashes inside a millisecond window, and reclamation is new handling.
- `false` — Batch teardown uses the last entry's stop limit: the contract documents active-entry stopping, and the shared environment is the same one that entry would get when run alone.
- `false` — `PreStartValidityCeiling` single-entry/600 s/null branches (two layers): the Platform test path always populates `SelectedTests` and `StartupDeadlineUtc`, so those fallbacks are unreachable there; validity beyond credential expiry is the spec-directed pre-start ceiling, with the gate deadline retained.
- `false` — Live proof data is tautological: `suite.py` writes `peerKeyVisible: false` only after `GET` of the peer key asserted `None`; masking in `finally` was rejected as low in round 2.
- `low` — `suite.py` layout rendezvous, fixed 80 s wait, and persistence shown by `SAVE`: an opt-in gated proof that fails loudly (carried from round 2); the AC's "persist" means stored in the owned store, not durable across restarts.
- `false` — Startup verifier proves nothing: the fixture task exists to exercise the required-task gate (carried from round 2).
- `false` — Regression assertions removed without replacement: the stop-bound timing check became a durable-state assertion, with a rationale about OS process-tree termination; whether the setup-timeout workspace is still absent is unverified and would only be low.
- `low` — Batch children can write runner-trusted workspace files: plan and readiness come from the in-memory session, and tampering with the catalog snapshot only causes a fail-closed HXT009 (as the batch test shows).
- `false` — Invented catalog `instanceId`/`attemptSequence`/plan identities: the runner issues run-scoped identities, there are no retries, and the catalog commit comes from the verified snapshot (carried from round 2).
- `low` — Content identities hash default serializer output: deterministic for a given tool version; drift needs a code change, and canonicalization is new design.
- `false` — Live proof reuses a stale `suite-proof` directory: each run creates a fresh temporary workspace and deletes it after the cleanup audit (round-2 patch).
- `low` — Peers ready more than 80 s apart: observed startups differ by under 7 s; this is an opt-in proof (carried from round 2).
- `false` — Descriptor endpoint without a port: observed endpoints are Aspire-allocated URLs that always carry their port.
- `false` — 64th allocation conflict reported as HXR031 with an unreachable final throw (two layers): HXR031 for claims that cannot be reallocated is the round-2 decision, and the trailing throw is required for compilation.
- `low` — Lock contention over 2 s surfaces as a generic failure with no owner (two layers): the lock guards only millisecond file operations; the fix adds an exception type.
- `low` — An I/O failure while creating the temporary claim is misreported as a conflict: needs ENOSPC-type failure while a peer holds the path; the fix restructures classification.
- `low` — Owner `unknown` → HXR022 text: carried from round 2.
- `low` — Readiness is not re-observed between batch entries: carried from round 2.
- `low` — A symlinked `native-tests` lets entry N delete files outside the workspace: the child already runs as the same user with the same write access, so no boundary is crossed; an accidental link is implausible.
- `low` — I/O failures before the handoff `try` become HXT007 instead of HXT009: both refuse before launch and neither claims a pass; the classification difference is negligible.
- `low` — Cancellation mid-batch loses the HXI002 summary: the result is already a withdrawal/HXT007; the fix adds try/finally.
- `false` — The descriptor re-reads CI variables instead of the runner's override: `ContinuousIntegrationOverride` is an internal test hook, and the production expressions are identical.
- `low` — Evidence-directory reuse in the live proof: carried from round 2.
- `low` — Story 2.3 commit claims the certificate parent is validated before claims and writes: the claim belongs to Story 2.3's wording; a late refusal still tears down and releases owned claims.
- `low` — Any manifest declaring `moduleId: platform` receives infrastructure ownership: only the fixture does; it becomes moot under decision options 1 or 2, and a canonical pin needs a real declaration first.
- `low` — `startup-task-results.json` is copied from the child-writable workspace at terminal recording: forging it requires test code to tamper with runner files; round 2 already bounds the read; replacing it with in-memory serialization is more than a direct correction.
- `low` — Composition identity is not cross-checked against composed resources: identity is computed from the closure that drives composition, and the descriptor lists the actually observed resources; resource-set matching is new design.
- `low` — Spec `status: done` while sprint status says `review`: settled by this review's status sync.

Outcome: decision resolved as option 1 (selected-module infrastructure ownership). The user chose to keep all 9 patch findings as action items; the story returns to `in-progress`. One finding is deferred, which repeats the existing round-2 ledger item; 39 were rejected.


### Round-3 implementation and verification, 2026-10-23

All nine approved patch actions are complete. Shared runner infrastructure resolves the active selected module against the authoritative declaration closure, falling back to the selected suite's module. Servers and Memories stores retain their declaring owners. Current documentation and the unavailable-owner diagnostic reflect that policy; the versioned contract no longer points at process artifacts. The added regressions bind actual Platform annotations, engine-produced composition identity, repeated-profile refusal, later-entry terminal selection, v1 startup timing, selected model-build startup expiry and store ownership.

Evidence: [round-3 index](evidence/2-4/review-round-3/README.md), [verification](evidence/2-4/review-round-3/verification.json), [patch mapping](evidence/2-4/review-round-3/patch-to-verification.json), and [independent parent acceptance](evidence/2-4/review-round-3/parent-acceptance-audit.json).

- Both individual Debug builds passed with zero warnings/errors. Focused Builds checks passed 11/11. The final full affected Builds invocation passed **553/553**, with zero failures/errors/skips/not-run, in 223.934 seconds. Initial temporary-ancestor fixture refusals and their unchanged 49/49 isolated rerun are retained; the final invocation used a private temporary directory without weakening any guard.
- Platform passed **44** affected checks, with one separate opt-in occupied-listener skip and no failures/errors. All three real annotation-lookup cases passed, including selected-suite fallback. Environment/result schema mirrors remain byte-identical.
- Fresh real-service proof passed **1/1** in 98.121 seconds. Runs `2f8a837fd855653bfee1186fbe8c65e6` and `fa089a284b28d8794b801465c48b905f` each observed six healthy resources and a passed required verifier before handoff, received a full 120-second execution interval after 81.49/90.83 seconds of startup, persisted/read isolated identical keys and could not observe their peer's unique key. Actual runner archives validate and neither command emitted HXR003.
- Both explicit same-owner cleanups succeeded on their first call. Exact Docker-label and process-environment scans are empty; claims/workspaces are empty and the fixture temporary directory is deleted. Independent applications were not targeted.
- The final binding contains **60 Story 2.4 source files** and separately records three concurrent diagnostics files. Portable-PDB document checksums and matching binary/PDB identities verify their compiled bytes without claiming ownership or broader independent-story qualification. The approved frozen fingerprint remains `1f9629e6eaa57fac455129b09e94a9667293a9c5cb167d6403d239d0fbe5d852`.

No approved implementation action remains incomplete. The previously deferred allocation observation remains deferred. Ordinary Tenants/Memories ownership is model-verified; live qualification remains the source/Debug technical fixture, with no package, CI, attachment or candidate acceptance claim. Concurrent changes and pins are preserved. Workflow review and final status synchronization remain parent-owned.


## Code review round 4, 2026-10-23

Scope: Platform `543ea182..693390a` and Builds `66b9136a..37d98f6` (current gitlink), restricted to the 60 Story 2.4 source files in `evidence/2-4/review-round-3/source-fingerprints.json`. All 60 fingerprints match HEAD. Evidence and the three concurrent diagnostics files are excluded. The diff is 5,475 lines, +3,384/−187. Shared files still carry concurrent Story 2.2/2.3/4.27 hunks, which are not judged here. Mode `full` against this spec, `epic-2-context.md` and `docs/contracts/test-environment-v1.md`. All four layers completed: Blind Hunter, Edge Case Hunter, Verification Gap and Acceptance Auditor. Each of the 38 raw findings was verified individually, then grouped. The Acceptance Auditor found no AC or I/O-matrix violation. `B/` means `references/Hexalith.Builds/`.

### Review Findings

**Patch**

- [x] [Review][Patch] A retained batch's later `down` uses the active entry's stop limit, but no test checks this. `RecordTestOutcomeAsync` rewrites state with `SelectedTest = record.Selection`, and `DownAsync` builds `TeardownBound(state?.SelectedTest)` from it. `TerminalTimingNeverReadsChildFileAndDownKeepsMembershipAsync` overwrites the state it just recorded with its own copy, and the batch tests never write run state. If the assignment is dropped, a retained failed batch is torn down within entry 1's stop limit and every test still passes. Read the state back before overwriting it and assert `SelectedTest == selections[1]`. [B/test/Hexalith.Builds.Module.Tests/TestEnvironmentProvisioningReviewTests.cs:216; B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:591]
- [x] [Review][Patch] Two evidence-validator timing checks are not pinned by any mutation: `effectiveSeconds == executionTimeoutSeconds` and `startedAtUtc <= terminalAtUtc`. The existing `execution-seconds` mutation is already rejected by the deadline-difference clause. Without the first check, a forged artifact with a consistently enlarged start/deadline/credential triple would validate. Add two `ConflictingSelectedEvidenceIsRejected` cases: one consistently enlarged budget, and one start after terminal. [B/test/Hexalith.Builds.Module.Tests/ModuleRunnerReviewRegressionTests.cs:526; B/src/libraries/Hexalith.Builds.Tooling/RunEvidence/ModuleRunEvidenceArtifactValidator.cs:490]
- [x] [Review][Patch] Retained execution-timing and startup-task archives are only asserted by the opt-in live proof. These cover the pre-launch `execution-timing.json`, the root and per-entry archived timing, `ActualDurationSeconds`, and the regular `startup-task-results.json` copy. Dropping any of them passes every normal test. Extend `ReadyBatchHandsValidatedDescriptorsToSequentialChildrenAfterStartupAsync` to compare both archived timing files with the descriptor's `originAt`/`expiresAt` and to assert a non-null duration. Have `runner.py` assert that the pre-launch timing file exists. Add a regular-file companion to `TerminalMetadataRejectsStartupTaskFifoWithinOriginalBoundAsync`. [B/test/Hexalith.Builds.Module.Tests/TestEnvironmentProvisioningTests.cs:30; B/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:258; B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:777]
- [x] [Review][Patch] The Builds README says "A conflict returns `HXR031` naming the blocking run", but both production `Acquire` sites reallocate on conflict. `HXR031` appears only when 64 consecutive allocations conflict. Reword it to say that conflicting fresh allocations are retried and `HXR031` names the blocking run only when no independent allocation can be claimed. [B/README.md:441]

**Deferred**

- [x] [Review][Defer] `RunEndpoints.Observe` throws for a healthy resource with an unallocated endpoint. This can replace the progress, `HXR025` and readiness documents on every run path. [B/src/hosts/Hexalith.Builds.Module.AppHost/RunEndpoints.cs:56] — deferred: three layers re-raised it again (Blind, Edge, Verification other). It is the same maybe-false (medium if true) item as the round-2 and round-3 ledger entries, and the same settling evidence applies.
- [x] [Review][Defer] No test covers refusing a healthy resource with an unallocated endpoint. [B/src/hosts/Hexalith.Builds.Module.AppHost/RunEndpoints.cs:56] — deferred: the gap is real (medium), but the correct assertion depends on the open `RunEndpoints.Observe` item. If Aspire can report such a resource as healthy, the throw moves to the descriptor writer and a readiness-level test would pin the wrong layer. Add the regression once that item is settled.
- [x] [Review][Defer] The retention composition's `PlatformDaprDiagnostics` registration (directory, application IDs, secret values) is never asserted through `ComposeRun`. [src/Hexalith.Platform.Composition/PlatformAppModel.cs:364] — deferred: this is Story 4.27 work (`858d02c`) and the same item as the round-2 R3-V2 ledger entry.

**Rejected**

- `false` — B1, CI detection ignores `ContinuousIntegrationOverride` and refuses non-GitHub CI. Refusing incomplete actual CI metadata before provisioning is the round-1 approved behaviour. The writer and the runner evaluate the same production expressions, and the override is an internal test hook (carried from round 3).
- `low` — B2, `jobId` comes from `GITHUB_JOB`, so matrix legs share it. Nothing in this story consumes it; attachment is Story 2.6. Disposable runners and loopback-only endpoints keep legs apart, and a per-leg identity needs new metadata design. Revisit in Stories 2.6/2.8.
- `false` — B3, the CI-context test mutates process environment. `PlatformManifestTestGrouping` is `DisableParallelization = true`, so the collection runs alone (carried from round 2).
- `low` — B4/E10, the live-proof `finally` masks the primary failure and leaks the workspace. Carried from rounds 2 and 3.
- `low` — B5/B6/E9, `suite.py` layout rendezvous, the fixed 80 s wait, and assertion exits without a TRX. Opt-in gated proof that fails loudly (carried).
- `low` — B7, opt-in live proof, prebuilt AppHost assumption, no dirty-tree marker. Carried from round 2.
- `false` — B8, v1 contract semantics changed in place. Those hunks are Story 2.2's. The closed run-evidence widening is the owner's round-2 decision (a), documented in the Builds README.
- `false` — B9, the schema, validator and runner disagree on `selections`. Multi-entry runs refuse `--evidence` before provisioning (`ModuleCommandExecutionService.cs:1041`). Batch `selected-test.json` files are durable run records that are never presented to the evidence validator (carried from round 3).
- `low` — B10, the `memories/functional-retention` special case in batch compatibility. Carried from round 3.
- `false` — B12/E8, cross-repo reflection lookup and owner fallback for other modules' stores. `ComposeRunOwnersSurviveTheBuildsAnnotationLookupAsync` runs the same name/property lookup on composed resources, so a rename fails it. Platform composes only servers, sidecars, shared infrastructure and the three Memories stores (`PlatformAppModel.cs:347-357`), so no other declared store can fall through.
- `false` — B13, `CompositionResourceClaimConflictException` deriving from `IOException` could be swallowed. No `IOException` handler precedes the specific catches (`CompositionEngine.cs:418`, `AppHostRunner.cs:77`), and the allocator loops catch the specific type. The other B13 parts are carried: owner `unknown` → HXR022 and the unguarded `ClaimedPorts` JSON are low; the unreachable final throw is false and already in the Story 2.3 ledger.
- `false` — E3, a 64th conflict is reported as HXR031 and the final throw is unreachable. HXR031 for claims that cannot be reallocated is the round-2 decision (carried).
- `low` — B14, later batch entries recheck child-writable catalog material. Tampering fails closed with HXT009 (carried from round 3).
- `low` — E6, a FIFO planted at `source-mapping/catalog-versions.json` blocks a later entry's handoff. It requires a same-user test child to sabotage a runner-internal path, and such a child can already stop or kill the runner directly, so no boundary is crossed. The manifest and declaration rechecks have the same synchronous reads, so a fix is not a direct correction. This follows the round-3 symlinked `native-tests` reasoning, which also bears on the deferred readiness FIFO item.
- `low` — E7, an invalid descriptor re-read inside `NativeTestHandoff.Create` maps to HXT007 rather than HXT009. It needs a concurrent writer between the runner's validated write and its re-read, and both outcomes refuse before launch (carried classification note).
- `low` — B15, regression assertions loosened. The setup-timeout workspace check is carried as low. The stop-bound change is a durable-state assertion (carried as false). The 20→30 s bound is load tolerance. The flakiness of the one-second batch fixture is maybe-false, and would only be low.
- `false` — B16, the contract handoff section omits variables and the loopback restriction. `HEXALITH_G4_*` are the pre-existing native-test variables, not descriptor contract. The contract accepts any absolute URI while the local producer deliberately narrows to loopback. The samples link is Story 2.2's.
- `false` — B17, the certificate ceiling omits stop seconds for a single entry. Single declared suites also populate `SelectedTests` (count 1), so the execution-plus-stop sum applies. The fallback branches are unreachable from the Platform path (carried from round 3).
- `low` — E2, `ReleaseOwned` can throw inside teardown. Carried from rounds 2 and 3.
- `low` — E4, a malformed claim file breaks `ClaimedPorts`. Carried.
- `low` — E5, readiness is not re-observed between batch entries. Carried.
- `low` — E11, the Story 2.3 claim that the certificate parent is validated before claims and writes. Carried from round 3, as Story 2.3 wording; a late refusal still tears down and releases claims.
- `low` — A2, the live fixture's `moduleId: platform` cannot distinguish the old ownership rule from the new one. The limitation is already disclosed. The real annotation lookup is bound for non-platform owners by the Platform model test, and renaming would require regenerating gated evidence.
- `reject` — A3, the `NativeTestExecutorTests.cs` task row names an untouched file. The fix would edit the spec under review; the coverage exists in the provisioning test classes.
- `reject` — A4, spec and sprint status disagree. Settled by this review's status sync.

Outcome: 0 decision-needed, 4 patch, 3 defer (all extending existing ledger items), 29 raw findings rejected.

### Round-4 implementation and verification, 2026-10-24

All four patches are applied. They are test-only and documentation-only, with no production code change.
- **Persisted batch selection.** `TerminalTimingNeverReadsChildFileAndDownKeepsMembershipAsync` now reads the recorded run state back instead of overwriting it. It asserts that `SelectedTest` and `TestRun.Selection` hold the last entry and that its 4-second stop limit is the one later `down` will use.
- **Validator timing clauses.** `ConflictingSelectedEvidenceIsRejected` gains `execution-enlarged`, a consistently enlarged effective/deadline/credential triple, and `execution-after-terminal`, a coherent clock that starts one second after terminal.
- **Archived timing and startup-task files.**
  - The shared `runner.py` fixture asserts that the pre-launch `execution-timing.json` exists, has no duration yet, and matches the descriptor's effective seconds, configuration identity and `originAt`.
  - `ReadyBatchHandsValidatedDescriptorsToSequentialChildrenAfterStartupAsync` checks each archived entry's timing against the child-observed start. It also checks the one-second bound and a measured duration of at least 0.15 s, and that the durable root copy equals the last entry's copy.
  - `BatchArchivesEachEntriesInspectedFunctionalObservationsAsync` checks that the workspace root copy equals the durable root copy.
  - The new `TerminalMetadataRetainsRegularStartupTaskResultsAsync` covers the regular-file `startup-task-results.json` copy.
- **README.** The Builds README now says that conflicting claims are reallocated for up to 64 attempts, and that only exhausted attempts fail with `HXR031` naming the last blocking run.

Verification:
- The individual Debug build of `Hexalith.Builds.Module.Tests` passed with zero warnings and zero errors, analyzers enabled.
- `TestEnvironmentProvisioningTests` and `ModuleRunnerReviewRegressionTests` passed **101/101**.
- **Mutation checks.** Every new assertion was checked against a temporary production mutation, built with analyzers disabled; the sources were then restored and their SHA-256 values re-verified. Each mutation failed exactly its intended test, with no collateral failures. The seven mutations:
  - drop `SelectedTest` from the state write;
  - drop the `EffectiveSeconds` clause;
  - drop the `StartedAtUtc <= TerminalAtUtc` clause;
  - zero `ActualDurationSeconds`;
  - drop the durable-root timing write;
  - drop the workspace-root timing write;
  - drop the `startup-task-results.json` copy entry.

  An eighth mutation dropped the pre-launch timing write. The first child then failed the fixture's new assertion before writing its report (`HXT002`).
- **Full assembly.** One full run of the assembly, before the final workspace-timing assertion was added, reported 2005/2015 passing. The 10 failures all came from classes this round did not touch: 8 `WorkspaceCommandModeTests`, 1 `McpCliRunBuildTests` deadline case and 1 `ReadinessDeadlineObservationSharesStoppingBudgetAsync(timeout)`. The run took 615 s under concurrent host load. Rerun in isolation against the final build, they passed 53/53 and 5/5. They are recorded as load-sensitive, not as regressions.
- **Concurrent commit.** A concurrent `/pushall` committed the four edited files, as they stood then, to Builds `main` as `dd3b4cd` at 03:57:50; Platform `56c783b` restages that gitlink. That commit contains no production source change. The later workspace-timing assertion in `TestEnvironmentProvisioningReviewTests.cs` remains an uncommitted Builds working-tree change.

No approved patch remains open. The three round-4 deferrals remain in the ledger.
