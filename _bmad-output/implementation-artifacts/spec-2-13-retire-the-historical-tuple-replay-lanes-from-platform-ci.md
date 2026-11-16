---
title: '2.13 Retire the historical tuple-replay lanes from Platform CI'
type: 'refactor'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
story_key: '2-13-retire-the-historical-tuple-replay-lanes-from-platform-ci'
baseline_commit: 'ddb1c0b141372a66d4a9d9c231d0041afbb7cd0c'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Historical tuple replay requires companion Platform commits and branches for every change. Merging those branches can restore obsolete pins, without providing qualification credit.

**Approach:** Execute source resolution and current-tuple checks on the candidate alone. Remove replay machinery, including the package-reader checkout hidden inside the current job, and update preparation instructions.

## Boundaries & Constraints

**Always:** Require both prerequisite jobs to succeed. Preserve pins, identity/integrity, runtime/output verification and zero-skip enforcement. Retire only historical-source-specific assertions. Preserve purpose/key/snapshot refusals, archive authenticity, signed-host handoff, reader integrity, readiness and model transport. Old packages may remain explicit fixture data without another Platform checkout or invented provenance. Record coverage changes.

**Never:** Create companion commits/refs, weaken image or runtime admission, equate reader authenticity with execution qualification, delete archived qualification/broker tags, modify `/pushall`, or implement Story 2.14's version-only pin redesign. Historical observations keep their original subjects.

## I/O & Edge-Case Matrix

| State | Expected behavior |
| --- | --- |
| PR, main push or explicit candidate dispatch | All Platform checkouts select only that candidate |
| Either prerequisite fails, cancels, skips or is absent | Combined verdict fails |
| Documentation-only or gitlink-only change | No historical descriptor update or extra ref |
| Changed owner, provenance, package body or runtime | Existing identity checks refuse |
| Old replay-only positive | Retired; no new image/runtime permission |

</frozen-after-approval>

## Code Map

Aliases: E = `eng`; T = `tests/Hexalith.Platform.Composition.Tests`; S = `tests/source-release`.

- `E/run_tuple_ci.py`: `collector_for` routes 450/478 current42 and all 97 base42 cases into signed4.47/NET10 replay; removing YAML jobs alone is insufficient.
- `E/current_tuple_source.py`: imports `PROVENANCE`, `commit`, duplicate-rejecting `decode` and `git` from the legacy module. Retain current owner/tag/blob checks.
- `T/PlatformPartiesPackageFixture.cs`: `PreparedFixtureRoot` authenticates another Platform revision. `currentArtifacts` is not a replacement for old image-positive tests: production deliberately refuses those newer image cohorts.
- Pins are repaired at HEAD; local qualification branches are absent. Preserve 105 archived qualification tags and both broker tags.

## Tasks & Acceptance

**Execution:**

- [x] `.github/workflows/tuple-ci.yml`, `S/tuple-workflow.test.mjs`, `S/source-release.test.mjs`: retain resolution, current execution and verdict only; remove historical inputs/outputs and Platform fetches; use candidate-only shallow checkouts. Test trigger coverage, checkout identity and the executable two-result verdict matrix.
- [x] `E/current_tuple_source.py`, `E/file_host_catalog.py`, `E/prepare_tuple_sources.py`, `E/test_current_tuple_source.py`: move shared helpers into current ownership; keep binding generation only; remove companion preparation/verification and test doc/gitlink changes without companions.
- [x] `E/{legacy_tuple_source,pre_extension_tuple_source,package_reader_source}.py`, corresponding `test_*.py`, and `E/{legacy-tuple-source,pre-extension-tuple-source}.json`: delete retired source modules, tests and descriptors.
- [x] `E/run_tuple_ci.py`, `E/tuple-ci-*.json`, `E/test_tuple_{collectors,runtime_context,sdk_suites}.py`: remove historical runtimes, reader workspace and fallback execution. Delete replay-only profiles; rebuild remaining current inventories with explicit positive counts and current collectors. Preserve extension controls and generic package-authenticity cases from base42; remove obsolete CLI selections and defaults.
- [x] `T/PlatformPartiesPackageFixture.cs`, `T/PlatformPackageCatalogReaderFixture.cs`, `T/PlatformQualificationPackageFixture.cs`, `T/Hexalith.Platform.Composition.Tests.csproj`: remove reader-workspace metadata and historical Platform roots. Use candidate/tool inputs and genuine fixture-owned declarations/packages; follow `T/PlatformPartiesProofFixture.cs` for local histories granting no publication authority.
- [x] `T/PlatformCurrentOwner{Package,Handoff,MemoriesTransport,Preparation}Tests.cs`, `T/PlatformPublishedPartiesMemoriesTransportTests.cs`, `T/PlatformParties{PackageEntry,ValidationHandoff,CatalogHandoff}Tests.cs`, `T/PlatformSdk42{PackageEntry,CurrentOwnerMemoriesTransport,PublishedPartiesMemoriesTransport,EventStorePackageTestCohort}Tests.cs`, `T/PlatformEventStorePackageTestCohortTests.cs`: adapt fixture callers while preserving substantive positive/negative behavior. Isolate static old-body checks from loaded current assemblies. Explain each replay-only selector removed.
- [x] `docs/current-tuple-preparation.md`, `docs/domain-module-enrollment.md`, `E/tuple-source-preparation.md`, `README.md`: replace L/M and reader-companion obligations with candidate-only preparation; align current descriptions with the selected binding and retain historical evidence labels.
- [x] `_bmad-output/implementation-artifacts/evidence/2-13/`: save coverage mapping, canonical revisions, before/after pin/tag inventories and checks. Remove any local `qualification/*` branches remaining at delivery after recording their targets. Verify an isolated merge leaves pins unchanged without invoking `/pushall`.

**Acceptance Criteria:**

- Given a candidate, when preparation and CI execute, then no other Platform revision or qualification ref is required.
- Given retired replay machinery, when repository controls run, then remaining tuple, source-release and Composition controls pass with positive totals and no skips.
- Given landing on main, when hosted tuple CI completes, then resolution, current execution and their verdict are green; local audit alone cannot satisfy this criterion.

### Review Findings

Code review 2026-11-15 of `f55a641f` (parent `5b44f2d0`, code/docs only, evidence excluded). Layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor — all completed.

- [x] [Review][Patch] Delete orphaned legacy-only Composition test classes and record them (medium; decision resolved 2026-11-15: delete all three) [tests/Hexalith.Platform.Composition.Tests/PlatformPartiesMappedOwnerProofTests.cs:1] — `PlatformPartiesMappedOwnerProofTests` (3; asserts stale Parties gitlink `bb2783120c0e…` and would fail if executed), `PublishedCatalogArtifactCohortTests` (47; old 4.1.0/7.0.2/3.0.23 bodies) and `PlatformPackageCatalogReaderTests` (9; `RequirePublicContext` returns early under the current cohort) were selected only by the deleted `tuple-ci-legacy-suites.json`. Delete them and record each, with its reason, as a retired replay-only selector in `evidence/2-13/coverage-map.md` and `selector-count-mapping.tsv`. The reader-refusal patch below preserves the reader class's real value.
- [x] [Review][Patch] Hosted tuple CI fails at the landed commit: transport test relies on a machine-installed Dapr CLI (high; AC2/AC3) [tests/Hexalith.Platform.Composition.Tests/PlatformMemoriesContainerTransportTests.cs:55] — run 43757208901 `tuple` failed on current42 `test-0` with `Unable to locate the Dapr CLI`, so `tuple-verdict` failed. The hand-built graph never calls `builder.AddDapr(o => o.DaprPath = …)` as `PlatformAppModel.cs:278` does; local `/usr/local/bin/dapr` masked it. Because the runner stops at the first failing group, the remaining current42, base42 and extensions groups have never run on a hosted runner.
- [x] [Review][Patch] Reader pre-restore refusals lost all CI coverage (medium) [tests/Hexalith.Platform.Composition.Tests/PlatformReaderMutationControls.cs:20] — `LoadVerified` and the `validate-catalog.proj` exit/`HXW012` refusal in `PlatformPackageCatalogReader.Prepare` are exercised only by unselected `PlatformPackageCatalogReaderTests`. Add changed-snapshot, changed-pinned-catalog, missing-snapshot and changed-mapping variants to `AssertAsync`.
- [x] [Review][Patch] Duplicate-key refusal in current `decode` has no test (medium) [eng/test_current_tuple_source.py:1] — the only control was in deleted `test_legacy_tuple_source.py`; dropping `object_pairs_hook` keeps all 14 tests green. Add direct and real-Git `read_selection` duplicate-field refusals.
- [x] [Review][Patch] Retired fixture inputs and attributes left behind (low) [tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj:30] — `Fixtures/private-70525-provenance.json` is still embedded and in `.gitattributes:166`; `Fixtures/pre-extension-*.json` (`.gitattributes:118-119`) lost their only reader; `.gitattributes:106` names the renamed `PlatformSdk42CurrentOwnerMemoriesTransportTests.cs`.
- [x] [Review][Patch] `file_host_catalog.prepare` lost its only production caller but still runs as a CI control (low) [eng/run_tuple_ci.py:364] — the `CURRENT_PLATFORM_SHA` branch was its caller; `test_changed_apphost_cannot_use_another_current_sources_catalog` now only checks HEAD equality. Remove the module, its test and the control step.
- [x] [Review][Patch] Unreachable production branches in `ChangedFullEntryRefusesBeforeEffectsAsync` (low) [tests/Hexalith.Platform.Composition.Tests/PlatformPartiesPackageEntryTests.cs:43]
- [x] [Review][Patch] Ambiguous verdict sentence "Both prerequisites must succeed, including when another prerequisite fails, cancels or skips" (low) [README.md:13] — also in `docs/current-tuple-preparation.md` and `docs/domain-module-enrollment.md`.
- [x] [Review][Patch] Public preparation guide requires the personal `run-with-temp` wrapper (low) [eng/tuple-source-preparation.md:13]
- [x] [Review][Patch] Stale test title "exact five/six jobs" while asserting `[3, 6]` (low) [tests/source-release/source-release.test.mjs:45]
- [x] [Review][Patch] Stale "Copy and verify real release bytes" comment above the synthesized catalog (low) [tests/Hexalith.Platform.Composition.Tests/PlatformAppModelTests.cs:53]
- [x] [Review][Patch] base42 per-group count assertion dropped instead of updated to `[40, 51, 2]` (low) [eng/test_tuple_sdk_suites.py:84]
- [x] [Review][Patch] Vacuous `for-each-ref refs/heads/qualification/` assertions in fresh repos; compare full ref inventories before/after instead (low) [eng/test_current_tuple_source.py:214]
- [x] [Review][Patch] Reader `-p:CentralPackageTransitivePinningEnabled=true` argument has no test (low) [src/Hexalith.Platform.Composition/PlatformPackageCatalogReader.cs:62]
- [x] [Review][Patch] New Builds 6.0.5 AppHost/Tooling validation pair is not named in README/current-tuple-preparation like earlier pairs (low) [src/Hexalith.Platform.Composition/PlatformPartiesValidationContext.cs:36]
- [x] [Review][Patch] Always-true suite conditions and dead `file_catalog_sha256 is None` path in `run_tuple_ci.py` (low) [eng/run_tuple_ci.py:361]
- [x] [Review][Defer] `epic-2-context.md` rewrite drops Epic 2 constraints (hold 1,800 s/1–14,400 s, candidate policy v1, unsupported-version naming, Keycloak 26.6.4/realm import, cross-repository completion) [_bmad-output/implementation-artifacts/epic-2-context.md:22] — deferred: agent-context file; regenerate or restore from epics/architecture.
- [x] [Review][Defer] Companion branches `origin/fix/tuple-legacy-{ddb1c0b1,d386e360}-20261115` and `origin/fix/tuple-pre-extension-{ddb1c0b1,d386e360}-20261115` remain on origin [.github/workflows/tuple-ci.yml:1] — deferred: pre-existing remote refs; still the merge hazard this story targets; deletion is an owner remote decision.

Rejected:
- false — Historical README/guide sections say "The current producer": the intro labels them historical records with original subjects, as the spec requires.
- low — Candidate-only block repeated in three docs: maintenance only; consolidation is restructuring and 2.14 rewrites these sections.
- low — 565 executions vs 562 unique cases unstated: the figure is correctly labelled executions.
- low — `paths.Length.ShouldBe(6)` tautology and setup inside `assertRaisesRegex`: harmless.
- low — `AssertDerivedConsumerEnrichmentAsync` does not require enrichment: it is a bound by design; the catalog handoff theory independently needs warm ≠ cold through its proof-mismatch branch.
- low — Test names/summaries describing earlier positives: naming only.
- low — Nine reader mutations in one helper case: coverage kept, only granularity changed.
- low/maybe-false — Synthesized `PlatformAppModelTests` catalog: needed because the genuine catalog's generic default differs by design; single-word owners only; stale comment kept as a patch.
- low — Reader diagnostics may mask an exception: needs an I/O failure on the failure path; the guard is more than a direct correction.
- low — `source-releases.md` no longer says "unknown jobs": the doc names the exact three jobs; the gate is unchanged.
- false — `cr-at-eol` attributes instead of renormalizing: deliberate preservation of verified source bytes.
- low — `ExtensionSdkTest`/`SelectedSdkTest` identical, `sdk42` ignored: previously triaged (Blind-7).
- false — Single-choice `mode` in `prepare_tuple_sources.py`: keeps the documented `binding` CLI.
- false — `MatchesDeclaredTestServers` checks only Image/ProjectPath: it is a new additional refusal; the cohort/server/image switch still admits only exact tuples.
- false — Hard-coded transitive pinning overrides the owner: Platform `Directory.Packages.props`, Builds and Builds Tooling all set it true.
- low/maybe-false — Version/assets mutation re-run lacks pinning args: the asserted refusal is the explicit version/assets check.
- false — Cross-proof refusal never runs: the changed receipt now refuses unconditionally for both proofs.
- low/maybe-false — Guard build leaves new files under `source-mapping`: the subsequent proof `Matches` passes; not demonstrated.
- low — `PublishedSeed()` failure leaks the proof fixture: failure-path temp leak; the fix adds a guard.
- false — Absent SHA gives a traceback in `prepare_tuple_sources.py`: it still fails loudly and non-zero.
- false — Removed image-probe positives: spec I/O row retires old replay-only positives; current image cohorts refuse by design.
- low — Transport test bypasses production composition: Bind refuses current images first; `ActualDaprCliListenersAndTokensRemainServerScopedAsync` still composes the production path (prior Blind-3). The concrete defect is the Dapr CLI patch.
- low — Parties topology/runtime refusal theory deleted: its positive baseline was replay-only; restoring it needs new admission-layer factoring (prior Blind-11).
- false — Production authority changed outside the Code Map: the 6.0.5 pair matches `eng/builds-tuple.json` `toolingSha256` and 3-2 adoption605 evidence, and `MatchesDeclaredTestServers` only tightens admission. The documentation gap is kept as a patch.
- low — No end-to-end signed validation success: success needs image admission the spec retires; handoff consumption and five negative mutations remain.
- rejected — Spec `done` while AC3 is unmet: the fix would edit the spec; sprint status stays non-done.
- Given unchanged archived tags and current pins, when cleanup and merge verification finish, then both inventories are preserved.

### Review Findings — code review 2026-11-16 (post-review delta)

Code review 2026-11-16 of `f55a641f..dffe523c` (code/docs, evidence excluded; 27 files, +235/−796). Layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor — all completed. Verification Gap found no gaps in the changed behavior; its other findings were verified like the rest.

- [x] [Review][Decision] Hosted tuple CI installs a real Dapr CLI that no selected test needs (medium) — resolved 2026-11-16: remove the step; prove with a full local run with Dapr hidden from PATH, then the shared hosted run. `a506969c` added `dapr/setup-dapr@8d98…` (CLI 1.19.1) at `.github/workflows/tuple-ci.yml:118`, and `755de998` then made the transport test inject its own CLI. No other selected class publishes `BeforeStartEvent` (the only path that resolves the CLI). Hosted run 43771691347 ran with the CLI present, so it does not show the suites passing without one. The step would hide the next machine-Dapr dependency, which is the exact failure of run 43757208901, and it downloads an unhashed binary onto the qualification runner. Choose: remove the step and confirm with a hosted run, or keep it and record which tests need it.
- [x] [Review][Decision] Deleting the three orphan classes wholesale dropped cohort-independent coverage, and the coverage map misstates why (medium) — resolved 2026-11-16: restore the self-contained cases into a selected current class and correct the coverage map. `PlatformPartiesMappedOwnerProofTests`: only the first fact uses stale gitlink `bb2783…`. `PlatformOwnedMappingBindsItsActiveOwnerAndDirectCatalog` and `PlatformOwnedMappingRejectsChangedDirectOwnerGitlink` use the self-contained `PlatformPartiesProofFixture(platformOwned: true)`. No remaining test checks the Platform-owned drift refusal. `PublishedCatalogArtifactCohortTests`: `ExactVersionPairsDetermineOneClosedAuthority` (14 pure cases, including the `+metadata`, `7.0.2.0` and cross-pair refusals), `ForeignBodiesAndUnknownAuthoritiesRemainRefused` (5) and `FullVerificationRequiresAFullAuthority` (1) do not use the old bodies. Production `PublishedCatalogArtifactCohort.ForVersions` (`CatalogBuilder/PublishedCatalogArtifactCohort.cs:259`, called at `PlatformPackageCatalogQualification.cs:84`) still maps those pairs, and current tests check only null results. `PlatformPackageCatalogReaderTests`: `QualifiedTenantsModelForwardsDistinctReceiverTokensAsync` fails rather than returning early, which contradicts `coverage-map.md:31`. Choose: restore the cohort-independent facts into a selected current group (counts change, hosted rerun), or keep the deletion and correct `coverage-map.md:29-31` and `selector-count-mapping.tsv:40-42` to record the lost coverage.
- [x] [Review][Decision] Production Dapr listener/subscriber wiring has no CI coverage, and two earlier rejections rest on an unselected test (medium) — resolved 2026-11-16: select the listener test in current42. `ActualDaprCliListenersAndTokensRemainServerScopedAsync` (`PlatformAppModelTests.cs:167`) is not selected by any profile at HEAD, at `5b44f2d0` or at `ddb1c0b1`, and `git log -S` over `eng` finds no profile that ever named it. Before 2.13, four selected transport classes composed through production `Compose`. Their replacement, `PlatformMemoriesContainerTransportTests`, registers the subscriber itself. So `PlatformAppModel.cs:278` (`DaprPath`) and `:449` (`currentOwnerPackage` subscriber) are untested in CI, contrary to "Preserve … model transport". This invalidates the premises of the Blind-3 and Resume-Blind-2 rejections. Choose: select the listener test in current42 (463→464, measured, hosted rerun), or record the gap.
- [x] [Review][Decision] The companion-branch merge hazard recurred after landing, and four branches can still trigger it (medium) — resolved 2026-11-16: archive-tag the four commits, then delete the four remote branches (remote write confirmed separately). after `f55a641f`, `/pushall` merged `origin/fix/tuple-package-reader-{d386e360,ddb1c0b1}-20261115` (`d5e780c5`, `659ad39c`). That reverted the Builds/EventStore/Parties gitlinks, `eng/builds-tuple.json`, `provenance.json` and `.config/dotnet-tools.json` until `cb4aecc3` restored them. Those two branches are now ancestors of HEAD and harmless. `origin/fix/tuple-legacy-{ddb1c0b1,d386e360}-20261115` and `origin/fix/tuple-pre-extension-{ddb1c0b1,d386e360}-20261115` (`f0336579`, `55ddbc6e`, `da6b34e7`, `cff26b72`) are not ancestors. The 2026-11-15 deferred entry describes the hazard only as possible. Choose: delete the four remote branches now (targets recorded above; irreversible remote write), or keep them deferred and add the recurrence evidence to `deferred-work.md`.
- [x] [Review][Patch] Remove the unneeded setup-dapr step and prove the suites pass with no machine Dapr CLI (from D1) [.github/workflows/tuple-ci.yml:118]
- [x] [Review][Patch] Restore the cohort-independent cases (2 Platform-owned mapping facts; `ExactVersionPairsDetermineOneClosedAuthority`, `ForeignBodiesAndUnknownAuthoritiesRemainRefused`, `FullVerificationRequiresAFullAuthority`) into a selected current class, re-measure the inventory, and correct `coverage-map.md:29-31` and `selector-count-mapping.tsv:40-42` (from D2) [eng/tuple-ci-sdk42-current-suites.json:1]
- [x] [Review][Patch] Select `ActualDaprCliListenersAndTokensRemainServerScopedAsync` in the current42 model-boundary group and re-measure (from D3) [eng/tuple-ci-sdk42-current-suites.json:90]
- [x] [Review][Patch] Archive-tag `f0336579`, `55ddbc6e`, `da6b34e7`, `cff26b72` as `archive/tuple-{legacy,pre-extension}-{ddb1c0b1,d386e360}-20261115`, then delete the four `origin/fix/tuple-*` branches; record this in evidence (from D4; remote write) [_bmad-output/implementation-artifacts/deferred-work.md:2083]
- [x] [Review][Patch] Transport test never asserts that the toolkit used the injected Dapr CLI [tests/Hexalith.Platform.Composition.Tests/PlatformMemoriesContainerTransportTests.cs:56]
- [x] [Review][Patch] The always-true `args.suite` guard still wraps the only write of `authenticated-catalog.props`, which line 358 now reads unconditionally [eng/run_tuple_ci.py:285]
- [x] [Review][Patch] Stale whitespace attribute for the deleted `PlatformPublishedPartiesMemoriesTransportTests.cs` [.gitattributes:135]
- [x] [Review][Patch] `docs/domain-module-enrollment.md` has the same producer block but lacks the Builds 6.0.5 AppHost/Tooling validation pair that README and current-tuple-preparation carry, like its earlier pairs at lines 44/71 [docs/domain-module-enrollment.md:10]
- [x] [Review][Patch] Reworded verdict sentence overruns the ~80-column wrap in all three docs [README.md:15]
- [x] [Review][Patch] The preparation snippet continues with an empty `preparation_dir` if `mktemp -d` fails, so it writes to `/current-tuple-source.json` [eng/tuple-source-preparation.md:14]
- [x] [Review][Patch] The 2026-11-16 sprint-status comment is placed below the 2-13 entry, inside 2-14's comment block [_bmad-output/implementation-artifacts/sprint-status.yaml:105]

Rejected:
- Spec edit (rule) — Fourth AC now reads as a rejected item because the 2026-11-15 Review Findings block was inserted between AC3 and AC4 (blind, acceptance). Real, but the fix edits this spec.
- Spec edit (rule) — Task text still keeps `file_host_catalog.py`, and the Dapr workflow step and Spec Change Log entry are missing (blind).
- false / spec edit — Spec `done` vs sprint `review`: `review` is the correct state until this code review resolves. The superseded present-tense notes would need spec edits (blind, acceptance).
- Spec edit (rule) — "No new work was deferred" vs two 2026-11-15 deferred entries (blind).
- low — The setup-dapr step has no `id`, no outcome key and no workflow test (blind, edge). A setup failure skips preflight, so `PREFLIGHT_OUTCOME=skipped` fails the verdict closed. Adding it means a new outcome-schema field, and it is moot if the step is removed.
- low — No receipt link for the 6.0.5 pair and its AppHost hash is absent from `builds-tuple.json` (blind). Earlier pairs link receipts inconsistently, and the hash is bound at `PlatformPartiesValidationContext.cs:36`.
- low — current42 per-group counts are not pinned in `test_tuple_sdk_suites.py` (blind). `run_tuple_ci.py:437` already enforces each group's exact `requiredCount` against executed XML. Pre-existing.
- low — Ref-inventory checks ignore the worktree/index (blind) and owner repositories (edge). They cover the companion-ref hazard as patched; the extra scopes add assertions for a state nobody demonstrated.
- low — "Refuse before restore" assertions in `AssertRunAuthorityRefusal` cannot fail because `Prepare` never restores (edge, verification-gap). The deleted test was equally vacuous; proving `PlatformRoutingCatalog` ordering needs exercise through the caller, more than a direct correction.
- low — Tight 60-minute `timeout-minutes` (acceptance). Already raised to 120 by `17b1f688` on origin/main, outside the reviewed range. Local main (`dffe523c`) and origin/main have diverged by one commit each, so integrate before pushing.

Patch resolution (2026-11-16): all 11 patches are applied. Their [local verification](evidence/2-13/review-20261116/README.md) ran a fresh signed-package build with no Dapr CLI on PATH. It passed 38 groups and **588 executions** (current42 486, base42 93, extensions 9), plus 170 standalone Python, 83 source-release Node and 14/30 extension controls, all with zero skips. The changed groups ran first: transport 1, the listener group 5, restored cohort refusals 20 and Platform-owned mapping 2. [coverage-correction.md](evidence/2-13/review-20261116/coverage-correction.md) supersedes the hash-bound coverage rows without rewriting them.

The two synthetic profiles in `test_builds_tuple.py` and `test_builds_publication.py` had to follow the new 486 total. D4 resolved itself during the review: at about 08:35Z another session's `/pushall` merged all four remaining companion branches into origin/main (`cd0b1aa1`, `9dd73bea`, `3cd9a77b`, `5691d313`), reverting the Builds 4.45.0 pins again, and then pruned all six companion branches. With the user's approval, `ead9ff0d` restored the exact `73fef1d0` tree on top of origin/main. No archive tags were needed, because main's history now holds those commits. Hosted confirmation of the new inventory without a runner Dapr CLI is pending the next approved push.

## Implementation Notes

Completion (2026-11-16): all implementation tasks, acceptance criteria and matrix rows are verified. Hosted main AC3 and the resumed three-layer review are complete, including all five accepted evidence/documentation corrections and the independent parent verifier checks. Spec status is `done`; sprint tracking is `review` per the build workflow. Earlier pending statements are historical records. No new work was deferred and no remote writes were performed.

Hosted main acceptance verified (2026-11-16): after the user's read-only remote authorization, run `43771691347`, attempt 1, proved a successful main push of the exact local candidate `755de998e91a11545826748615785f0e43833152`. All three required jobs (`resolve-sources`, `tuple`, `tuple-verdict`) succeeded. [Verified hosted evidence](evidence/2-13/hosted-main-43771691347/summary.json) reconciles the GitHub-digested artifact, candidate/bootstrap identity, all 36 selected XML groups and all 565 Composition executions (463/93/9), with zero skips. AC3 is satisfied; earlier pending-acceptance statements retain their original observation times. The workflow now proceeds through step-04 review. No remote writes were performed.

Resumed candidate recheck (2026-11-16): at `755de998e91a11545826748615785f0e43833152`, no implementation changes were needed. [Fresh evidence](evidence/2-13/candidate-recheck-755de998/README.md) records 170 standalone Python, 83 source-release Node, 14 extension Node and 30 extension Python checks, zero skips, plus a successful signed-archive/project-graph audit from a depth-one Platform checkout containing one commit. Production and Composition source hashes still match the original 565-execution candidate; those observations retain their original subject. The parent independently checked all fourteen fresh command logs and archived their raw output with member hashes. Pins, gitlinks and refs remain unchanged. AC3 is still unverified; this run stops at the step-03 acceptance gate pending authorization for read-only hosted CI inspection under the workflow's explicit no-remote rule. Spec and sprint status remain `in-progress`.

Review patch follow-up (2026-11-15): all sixteen Review/Patch findings above are resolved. [Fresh verification](evidence/2-13/review-patches/README.md) records all 565 Composition executions (463/93/9), 170 standalone Python, 83 source-release Node, 14 extension Node, 30 extension Python, the one collection-graph case and all 28 source-SDK controls, with zero skips. The transport passes with no machine Dapr CLI on PATH, and the selected reader executes the four pre-restore authority refusals plus its transitive-pinning argument control. Final source hashes and the 16-case real-Git supplement bind the stronger ref-generation assertion; production/Composition bytes match the full-run candidate. An initial source-SDK harness invocation lacked its artifact-path argument; its four asset-location diagnostics and corrected passing run are preserved separately. The isolated merge preserves pins/gitlinks and every original tag, with no local qualification branches or repository remote operations. AC3 remains unmet pending authorized landing and actual hosted main resolution/current/verdict success; story and sprint status remain non-done. Earlier implementation observations below retain their original subjects.

The [independent parent audit](evidence/2-13/review-patches/parent-audit.json) checked all 329 archived member hashes, all 36 XML groups against their selected inventories, 1,599 recorded source hashes, unchanged outputs/pins/refs and frozen intent. The resumed build remains at the step-03 acceptance gate because AC3 is unmet; the workflow's no-remote rule prevents obtaining hosted main acceptance in this run.

Workflow execution constraints: no push or remote operations; implementation proceeds sequentially. Hosted main verification remains required after an authorized landing and cannot be claimed from local checks.

Local implementation verification passed: current-only workflow/source publisher gates; retired source modules, descriptors, frozen profiles and NET10 switch; current fixture/collector execution; complete coverage mapping and candidate-only instructions. Necessary compatibility corrections preserve central transitive pinning in copied readers, authenticate the signed Builds 6.0.5 host/Tooling pair, and expose the existing authenticated graph checks internally without granting image admission.

[Verification](evidence/2-13/verification-summary.json) reconciles 565 Composition executions, 173 standalone Python, 83 source-release Node, 14 extension Node plus 30 Python, graph 1 and source-SDK 28: positive totals and zero skips. The 31-project package build has zero warnings/errors. Test-only corrections retain their actual snapshot/output identities and explicitly reuse unchanged controls. All private jobs finished and their workspace was removed.

[Parent audit](evidence/2-13/parent-verification-audit.json) independently checked every archived member hash, all 36 final XML groups, all 186 runtime/test source files, the trigger/verdict and real-Git matrix logs, and live pins/gitlinks/tags. Parent staging added one further CRLF attribute for the new reader helper and explicit TSV total labels; these policy/evidence-only edits change no verified C# or runtime source. Hosted main acceptance is pending the conditional landing phase and is not claimed by local verification or review.

[Post-review verification](evidence/2-13/post-review/README.md) passed all 19 affected C# cases and 14 current-source Python tests at `9b27493ed62183dd37c31894f2ac712da33bfed4` (tree `7bcf050c12f28d2cc8ae4ac8f01ce56009045c31`). It strengthens the actual Production caller, cold/warm snapshot derivation and both changed-receipt proof refusals, removes dead fixture inputs, corrects both guides, and proves depth-one Platform selection with independent owner/tag/blob checks. All 77 production/build source files remain identical to the preserved full run. The [independent parent audit](evidence/2-13/parent-post-review-audit.json) checked the five XML groups, eight edited source/guide hashes, actual output stability and compressed raw metadata. The [parent control pass](evidence/2-13/parent-post-review/summary.json) freshly passed 174 standalone Python, 83 source-release Node, 14 extension Node and 30 extension Python checks, zero skips. The original 565-case observations retain their original subjects; only the affected cases were rerun after review.

Background fetches advanced `origin/main` to `5b44f2d058f9221d8b54782c377e09fec5786b65` and exposed public tag `v2.1.6`. All original 152 tags, including 105 archive and both broker tags, remain unchanged; the additional tag is preserved. The [updated isolated merge proof](evidence/2-13/upstream-merge/summary.json) resolved exactly the two descriptor modify/delete conflicts by keeping their retirement, preserved incoming release tooling/lockfile/assertions, matched all 85 production/review source hashes and passed all 83 source-release checks after fresh `npm ci`. Its actual private merge is `ef81f17c21f3593de4c16fa3e0ceef0ba1e6acf5`. Completion then rebased the local feature branch onto that observed main with the same two resolutions. [Final code verification](evidence/2-13/final-code-verification.json) proves every code/documentation path outside generated metadata matches the tested merge; the original captured baseline remains unchanged. No remote mutation or `/pushall` was invoked. Hosted main acceptance remains outstanding.

The local build workflow is complete. Sprint status remains `review` until an authorized landing and actual hosted main resolution/current/verdict acceptance; spec `done` does not claim that conditional hosted result.

## Spec Change Log

## Review Triage Log

All three independent layers completed before triage. The verification-gap layer reported no gaps. Each finding below was assessed individually before grouping.

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| Blind-1 | medium | The Production branch calls `PlatformCurrentOwnerProof.Create(..., false)` directly and constructs an unused Production builder. It therefore misses incorrect environment propagation in `ComposeRun`; patch the actual caller and require refusal before its configuration callback/effects. The reviewer's withdrawn well-formed-key objection is not a finding: an ordinary initial key has no prior anchor. |
| Blind-2 | low | The two EventStore mutation callbacks exercise the actual production `ValidateTestInputs` method with positive baselines and distinct sentinels, but do not independently isolate removal of its call from `Bind`. `Compose` still calls `Bind` before probes and `Bind` calls this layer before body/image checks. Reject this additional caller-isolation request: current unqualified images already refuse, and isolating a deeper admitted caller would require new instrumentation or an image-positive fixture, beyond a direct correction for a path developers cannot execute today. |
| Blind-3 | low | The transport component deliberately registers the real subscriber itself; it cannot detect a change to the current-package registration argument. Current full package composition refuses in `Bind` before reaching that registration, while `ActualDaprCliListenersAndTokensRemainServerScopedAsync` still composes the production source path and asserts actual registration. Reject additional current-image caller coverage: establishing that admitted path requires new qualification/instrumentation, beyond a direct correction for an unavailable execution path. |
| Blind-4 | low | The deleted old-image positive asserted a composed native preparation and its constant `RoutingCatalogObservationsRequired` property. Current native preparation has separate executable controls; `CatalogBootstrapBindsActualKeysPoliciesFilesAndCohortStartWaitsAsync` still composes the source path and checks all six participants. Isolating package registration would require an admitted package-image path; reject this negligible residual coverage request because the fix adds more than a direct correction to an unavailable path. |
| Blind-5 | medium | Both handoff tests preserve post-generation bytes/proof identity but exclude the catalog snapshot from their immutable-input comparisons without structurally bounding its cold-to-warm changes. Patch with a shared assertion allowing only genuine derived consumer cache entries and the correctly recomputed snapshot hash. |
| Blind-6 | low | Deterministic bodies legitimately let the original proof authenticate the repeated receipt, so that branch must remain. The existing controlled changed digest checks only the replacement proof; patch the same negative receipt to also require original-proof refusal, providing unconditional stale-measurement coverage without inventing body drift. |
| Blind-7 | low | `sdk42` became unused and its private705/25 comments are now misleading; patch by removing the dead argument and correcting comments. The two generic/SDK42 suite variants already selected the same SDK42 fixture at baseline (`SelectedSdkTest` delegated to `ExtensionSdkTest`), so broad suite deduplication is not a new defect and is not part of this patch. |
| Blind-8 | medium | The linked operational `docs/source-releases.md` still says five required tuple jobs, historical 412/349 lanes and obsolete totals. Patch its current operational description to three candidate-only jobs and the measured continuing profiles. |
| Blind-9 | medium | `docs/package-catalog-qualification.md` still presents deleted `PlatformPartiesPackageReaderTests` and `eng/tuple-ci-suites.json` as current unconditional positive instructions. Patch with a clear historical label and links to the continuing controls/preparation guide. |
| Blind-10 | medium | Workflow controls assert depth-one checkout syntax, but the recorded complete candidate execution used a shared full-history clone. Patch a real depth-one current-source test that proves the sole Platform commit suffices while preserving independent owner release/tag verification; no alternate Platform revision may become available. |
| Blind-11 | low | The retired old-image topology test included explicit non-Linux/ARM64 variants; the production runtime guard is unchanged and the tuple collector still enforces actual current runtime/output identity. A current package-image positive is unavailable, so simply reusing those variants would pass on the later image refusal. Reject this additional isolated runtime-layer request: meaningful coverage needs new instrumentation/admission-layer factoring, beyond a direct correction on platforms this tuple does not execute. |
| Edge-1 | medium | The qualification guide unconditionally points readers at a deleted replay inventory/class. This independently confirms Blind-9; patch the same stale historical/current documentation boundary. |

After individual verdicts, Blind-9 and Edge-1 share one stale-guide root cause. The remaining accepted entries are independent trivial patches with no public surface or new runtime authority. Low caller-isolation requests are rejected under the review's low-finding filter; no intent gap, bad spec or deferred implementation issue remains.

All accepted patches are applied and their focused checks passed (19 C# cases and the expanded 14-test real-Git module, zero skips). Parent verification additionally passed the complete 174-test standalone Python set, 83 source-release checks and 44 extension-release checks. Source/output hashes and all five focused XML groups were independently audited. Post-test changes are evidence, notes and the scoped verbatim-patch whitespace policy only.

### Resumed review, 2026-11-16

All three layers completed before triage: Edge Case Hunter returned no findings and Verification Gap Reviewer found no gaps. Blind Hunter's ten findings were assessed individually; the arithmetic line is not a finding.

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| Resume-Blind-1 | low | Carried: same enrichment claim previously rejected for the existing bound-by-design helper. The helper limits allowed cache additions; separate handoff assertions require cold/warm proof divergence. Keep the existing rejection; no repeated patch. |
| Resume-Blind-2 | low | Carried: same component-versus-production caller claim as Blind-3. Current unqualified image admission deliberately refuses before that wiring; the production source-path listener test remains selected. Keep the existing rejection of added admission-layer instrumentation. |
| Resume-Blind-3 | low | The new hosted verifier reads continuing profiles from the worktree, so a later historical recheck can consult different inventories. Patch: read the profile blobs at the recorded full revision and retain their hashes. |
| Resume-Blind-4 | low | The fetched records do belong to the selected run, but the verifier does not independently enforce job run IDs, artifact run/attempt name and bootstrap attempt identity. Patch these direct provenance checks. |
| Resume-Blind-5 | low | The authenticated artifact and runner already bind selectors, but the new verifier independently checks only XML aggregates. Patch the direct XML reconciliation to require passing test elements matching the declared class/method selectors, including representation of every selector. |
| Resume-Blind-6 | low | The documented and executed invocation is ordinary `python3 -B`; optimized execution is not used by any caller. Reject adding a new invocation-mode guard for this one-off evidence script under the low-finding filter. It adds a branch for a state absent from normal use. |
| Resume-Blind-7 | low | The verifier removes its raw tuple log after compression, then a repeat requires the removed input after beginning output replacement. Patch: read either retained log form and validate all inputs before replacing outputs. |
| Resume-Blind-8 | low | Carried: the previously recorded PublishedSeed failure-path proof leak is unchanged, and all selected fixture runs passed. Keep the existing rejection of additional failure-path fixture handling; no repeated patch. |
| Resume-Blind-9 | low | The archived one-off `execute-final-inventories.py` has parent-only timeout semantics, but no current CI/preparation caller uses it and no timed-out descendant is present in the recorded observations. Reject new process-group lifecycle branches in a historical harness under the low-finding filter; all jobs in this resumed run finished inside their wrapper before cleanup. |
| Resume-Blind-10 | low | The evidence index still presents the old pending-hosted state as current and lacks the new successful run link. Patch its current status and label the earlier pending statement historically. |

The survivors are independent direct evidence/documentation corrections (3, 4, 5, 7, 10), with no intent gap, bad spec or production change. They route to patch, without changing prior observation subjects or remote state.

All five accepted corrections are applied. The verifier passed authentic retained observations and twenty focused controls without downloads; an [independent parent audit](evidence/2-13/hosted-main-43771691347/parent-audit.json) additionally passed the authentic drifted-worktree recheck and four provenance/XML/log refusals, preserving evidence outputs on every refusal. Production code and selected inventories remain identical to hosted candidate `755de998e91a11545826748615785f0e43833152`; all 297 fresh control observations and the complete hosted 565-execution result remain applicable. No broader runtime rerun was needed for evidence/documentation-only corrections.

## Verification

Use `run-with-temp`; keep disposable copies/feeds/outputs under `$TMPDIR`, useful logs outside it, and await children.

Run remaining `E/test_*.py` tuple/source/publication/catalog/restore controls through their documented entry points; `npm run test:source-release`; extension-release controls; fresh Composition package builds and direct xUnit DLL execution for remaining inventories. Verify current tuple locally with `run_tuple_ci.py --audit-only`, then actual hosted execution. Test verdict failures/cancellation/missing results and absence of alternate Platform checkout/fetch paths. Record exact commands and no-skip counts.

Planning baseline: 82 source-release and 55 Python tuple tests passed. Collection-graph failed under generic discovery, then passed through its required `eng/test_tuple_collection_graph.py --evidence ...` entry point. Pre-implementation logs: `evidence/2-13/planning/`.
