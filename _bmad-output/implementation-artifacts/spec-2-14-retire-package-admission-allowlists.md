---
title: '2.14 Retire package admission allowlists'
type: 'refactor'
created: '2026-11-18'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '1ffa6013577fb0aace1cab93371db98f92829244'
story_key: '2-14-pin-builds-and-owner-modules-by-released-version'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Platform refuses valid package-mode runs when the Parties declaration or dependency images differ from compiled allowlists. Admit the mapped, declared package run using authenticated inputs and released dependency images, and consolidate the Parties integration project under `platform-package`.

## Boundaries & Constraints

- Use the committed owner declaration, actual test entry, trusted runner handoff, verified mapping and release snapshots. Preserve topology, runtime scope, readiness, Dapr transport and lifecycle checks. Builds still reports legitimate topology and manifest failures as HXP020.
- Build active Parties in Release against the mapped packages. Keep the source revision, built-artifact binding and native observations under the single integration profile. Lifecycle and attachment projects retain their own package profiles.
- Resolve images selected by the pinned catalog and registry binding to immutable digests before launch. Bind execution and observations to those digests. Preserve expected and loaded identities, existing package byte verification and candidate policy. A baseline-only run earns no candidate credit.
- This slice retires declaration and image allowlists and image cohorts. Owner NuGet lock files and replacement of package-only byte tables are deferred. Historical evidence retains its original subjects. CiCd story 21 owns workflow and controller retirement.

## I/O & Edge-Case Matrix

| Input | Outcome |
| --- | --- |
| Current committed Parties declaration under `platform-package` | Admitted through the real runner proof and runs the mapped Release build |
| Changed declaration, entry, mapping, or wrong owner outside the verified handoff | Refused before image probing or resource creation |
| Catalog-selected released dependency image with a new digest | Launch binds the resolved digest and records the observed identity |
| Missing or invalid image resolution, substituted image or package bytes | Refused at the corresponding verification boundary with the affected artifact named |
| Previously declared duplicate package profile | Removed from live declaration and callers. Distinct lifecycle and attachment entries still work |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Composition/PlatformPartiesRunProof.cs` already binds clean Git, plan/key, mapping, manifest and entry bytes. `PlatformPartiesPackageCatalogQualification.cs` and `PlatformPackageCatalogQualification.cs` duplicate hardcoded snapshot and image checks. Preserve the Builds declaration closure and EventStore owner proof.
- `PlatformCurrentOwnerProof.cs`, `PlatformCurrentOwnerObservation.cs` and `PlatformPackageCatalogReader.cs` prepare the owner Release run, seal its inputs and bind the reader. `PlatformRoutingCatalog.cs` and `src/Hexalith.Platform.CatalogBuilder/Program.cs` consume reader evidence and verify the loaded package bodies.
- `PlatformServerBindings.cs` resolves catalog versions and owner repositories. `PlatformAppModel.cs` probes images and binds digest-qualified launch. `PlatformDaprEndpointSubscriber.cs` takes a flag currently derived from the selected cohort.
- In Parties, `scripts/render-platform-declaration.py` owns `module/hexalith.module-manifest.v2.json`. Ordinary CI already selects `platform-package`. Live native observation consumers still branch on the retired purpose and old release identities.

## Tasks & Acceptance

**Execution**

- [x] In `src/Hexalith.Platform.Composition/Platform{PartiesRunProof,PartiesPackageCatalogQualification,PackageCatalogQualification}.cs`, replace static declaration admission with exact run-bound inputs and structural checks for Parties and EventStore. Separate package byte verification from declaration and image admission.
- [x] In `src/Hexalith.Platform.Composition/Platform{AppModel,ServerBindings,CurrentOwnerProof,CurrentOwnerObservation,PackageCatalogReader,RoutingCatalog,DaprEndpointSubscriber}.cs`, select Release preparation through the authenticated Parties package run, preserve the catalog and artifact seal, resolve and use immutable dependency digests, and select Dapr transport from the actual graph. Update the reader request contract in `src/Hexalith.Platform.CatalogBuilder/Program.cs` if it carries those bindings.
- [x] In the Parties repository, update the declaration generator, generated manifest, `scripts/test_platform_declaration.py`, and the live selectors in `scripts/{qualify-disposable-ci,observe-current-owner-startup}.py` and `tests/Hexalith.Parties.Platform.IntegrationTests/{NativeDiagnosticBinding,PartyEnvironmentTests,PublicRuntimeImageObservations}.cs`. Preserve persisted-state assertions, cancellation holds and cleanup. Remove active references to the duplicate profile.
- [x] In `tests/Hexalith.Platform.Composition.Tests`, use `PlatformPartiesPackageEntryTests.cs`, `PlatformCurrentOwner{Purpose,Handoff,Package}Tests.cs`, `PlatformEventStorePackageTestCohortTests.cs` and `PlatformMemoriesContainerTransportTests.cs` to cover the matrix and retain artifact substitution cases. Align `eng/tuple-ci-*-suites.json` and update current instructions in `README.md` and `docs/package-catalog-qualification.md`.

**Acceptance criteria**

- Given the real committed Parties declaration and matching tuple, when the declared integration profile runs, then the old allowlist no longer produces HXP020, the run reports the actual Release owner and immutable dependency images, and cleanup reports the owned resources.
- Given the Parties manifest, when package suites are grouped by project entry, then each group has one profile and the lifecycle and attached suites remain selectable.
- Given a changed declaration or dependency image accepted through the source and release bindings, when package admission runs, then it needs no hand-maintained declaration or image identity in Composition.

### Review Findings

Code review 2026-11-18 of Platform `1ffa6013..ab9c0b43` plus Parties `39ecdc0f..bfd63514`. The Parties content landed on Parties `main` as `6c34cfa9`, which `92c9005b` pins.

- [x] [Review][Decision→Patch: adapt the verifier to the 2.14 evidence shape (owner, 2026-11-18)] Parties evidence verifier rejects the new `platform-package` evidence. Parties `ci.yml` runs `verify-public-package-integration.py` on every push and pull request once `ci` passes. Concurrent commit Parties `5e5cb7b3` restored that verifier, and it calls `scripts/public_runtime_observation_contract.py:199-201`, which still requires `currentOwner is None` and a `publicRuntime` block. The 2.14 producer, `PartyEnvironmentTests.cs:131-132`, now emits a non-null `currentOwner` and `dependencyImages`. Also stale: `scripts/runtime_observation_contract.py:20` (`PROFILE = "platform-current-package"`, `tenantsVersion == "7.0.5"`) and the case IDs in `verify-public-package-integration.py:36-41` for the renamed `InvocationPreservesOrdinaryBatches…` method. The first green Parties integration run will fail at the verifier. Sources: blind+edge+verification+auditor.
- [ ] [Review][Decision→Patch: land the Platform patches, let Platform release, then repin Parties' Platform gitlink together with the verifier change; every push needs owner confirmation (owner, 2026-11-18)] Parties `main` is ahead of its Platform pin. Parties `6c34cfa9` carries the 2.14 declaration (sha256 `14e6bfe2…`) and the `released-images.json` reader, but its `references/Hexalith.Platform` gitlink is `c5f5eab3`. That revision still has the `IsOrdinaryDeclaration` allowlist (`40cee6ce…`/`d9c193b0…`) and no `PlatformReleasedImages`. No Platform tag contains `ab9c0b43` yet. Parties CI for `6c34cfa9` is queued and will be refused before the native suite. The spec required "the coordinated Platform release" first. Builds gitlink/pin disagreement at `ab9c0b43` (gitlink `v6.3.2`, pin `6.3.0`) was already reconciled by `92c9005b`. Sources: auditor + triage.
- [x] [Review][Patch] Catalog-builder verifiers assert the retired refusal text. `PublishedCatalogArtifactCohort.Verify` now says "differs from its verified published bytes", but four live verifiers still require "exact signed public artifact bytes". Hosted `catalog-builder.yml` run 44385370471 on `92c9005b` fails (4.1.0 and 4.2.0 legs), which blocks "Qualified Platform source release" run 44386404316. `verify_current_public_reader.py:149` and `verify_sdk420_public_parties.py:124` carry the same assertion [tests/catalog-builder/verify_public_owner_cohorts.py:123]
- [x] [Review][Patch] Native binding misses the controller-file lifecycle hold. Use `CiLifecycleHold.IsRequested` instead of re-reading `HEXALITH_CI_LIFECYCLE_HOLD`. Builds 6.3.x does not forward the variable, so the hold is signalled by the control file and the binding wrongly routes to `module-integration` and requires `invocation.json` [references/Hexalith.Parties/tests/Hexalith.Parties.Platform.IntegrationTests/NativeDiagnosticBinding.cs:99]
- [x] [Review][Patch] Run `PlatformReleasedImages.PrepareAsync` and its `released-images.json` under test, and assert the module-container `hexalith.platform.resource` label. Use a stub `docker` on PATH for `ps`/`container inspect`, then feed the written receipt to `PlatformCurrentOwnerObservation` [src/Hexalith.Platform.Composition/PlatformReleasedImages.cs:63]
- [x] [Review][Patch] Make `UncommittedDeclarationCannotAcquireParentProof` reach `RequireCommittedFile`. Rehash the appended declaration into the plan before `Create`, and add an uncommitted-entry variant. Today it refuses on the earlier hash check [tests/Hexalith.Platform.Composition.Tests/PlatformOrdinaryDeclarationCompatibilityTests.cs:39]
- [x] [Review][Patch] Add refusal tests for the Parties `PublicRuntimeImageObservations.Read` reader. Cover a foreign run/attempt/environment/mapping, `qualificationCredit: true`, a duplicate resource, a catalog-repository mismatch, a substituted observed reference or ID, and an empty list. `PublicRuntimeImageObservationTests` exercise only the test-local historical copy [references/Hexalith.Parties/tests/Hexalith.Parties.Platform.IntegrationTests/PublicRuntimeImageObservations.cs:9]
- [x] [Review][Patch] Pin the new Compose guard, "requires its authenticated and sealed Release owner". Count image probes and assert the message for the `archive` and `runtime-body` faults [tests/Hexalith.Platform.Composition.Tests/PlatformCurrentOwnerPackageTests.cs:115]
- [x] [Review][Patch] Add a `platform-package` row with the lifecycle hold set, asserting the `ci-lifecycle` artifact route and the skipped journal check [references/Hexalith.Parties/tests/Hexalith.Parties.Platform.IntegrationTests/NativeDiagnosticBindingTests.cs:107]
- [x] [Review][Patch] Name the artifact in the remaining generic package refusals: the deps.json version checks for EventStore, Tenants, Memories and McpCli, the nuspec repository check, and a missing embedded `lib/` entry (matrix row 4) [src/Hexalith.Platform.Composition/PlatformCurrentOwnerProof.cs:166]
- [x] [Review][Patch] Correct two test names or summaries that still claim allowlist protection. `CurrentDeclarationCannotReplayIntoHistoricalSourceAuthority` now only refuses a zero hash. `UncommittedDeclarationCannotAcquireParentProof` says "the compatibility set remains finite" [tests/Hexalith.Platform.Composition.Tests/PlatformCurrentPackageArtifactCohortTests.cs:130]
- [x] [Review][Defer] AC1 and matrix row 5 are unproven live [artifacts/story-2-14-package-admission/summary.md:45]. Deferred: the pre-existing operation-digest readiness mismatch (HXR021, reproduced with the former image bytes) stops `platform-package` and lifecycle before the native suite. The attached project fails CS1061 against `Hexalith.EventStore.Testing` 4.2.0.
- [x] [Review][Defer] The Memories container gateway rewrite to `DAPR_HTTP_ENDPOINT` now applies to every graph with a Memories container and EventStore, not only measured Parties cohorts [src/Hexalith.Platform.Composition/PlatformDaprEndpointSubscriber.cs:45]. Deferred, medium, unverified. A package run of a non-Parties graph, or the historical FullParties 3.0.23 graph, reaching Memories→gateway commands would settle it.
- [x] [Review][Defer] `memories-vectors` (`redis/redis-stack:latest`) and `memories-graph` (`falkordb/falkordb:latest`) stay on mutable tags without a retention profile [src/Hexalith.Platform.Composition/PlatformAppModel.cs]. Deferred: pre-existing; these images are not catalog-selected.

Applied 2026-11-18 in Platform `ce8179d6` and `75dbd151`, not pushed:
- `PrepareAsync` now inspects containers through the injectable `ImageProbeRunner`. `PreparedRunRecordsTheLoadedDependencyIdentityAsync` (6 cases) checks the receipt and the run/resource label filters. The producer-side label is not asserted, because no test composes a full package graph.
- The committed-file control uses `--skip-worktree`. The clean-status check already refuses plain edits, so `RequireCommittedFile` matters only for bytes that status hides.
- Local runs: the 20 new or changed fixture-free cases pass, and the eng tuple tests pass (63). The 26 `PlatformPartiesPackageFixture` cases (including the sealed-owner guard) cannot run on current `main` here, because the cohort SDK bytes and the published seed are unavailable; hosted tuple CI covers them.
- Inventories: current42 is now 479. Hosted tuple CI on `92c9005b` timed out in `PlatformCurrentOwnerHandoffTests` (it already took 918 s of the 1200 s limit before 2.14), so that group is split into three method groups.

- The Parties patches (hold detection, reader refusal tests, hold-routing rows, and the adapted verifier) are in a local Parties commit `cd65a7e6`, built on Parties `5c293f09` in an isolated clone and not pushed. The verifier now validates the `currentOwner` Release owner and `dependencyImages` without pinned image or package digests, and `NATIVE_CASES` has 46 entries. Results: the integration-verifier unit tests pass (68), the integration test project compiles in Release, and 44/44 of its local cases pass. The live `PartyEnvironmentTests` did not run locally. Two assumptions still need a real hosted artifact: that native SDK bodies equal the owner's `sdkBodies`, and that the composition output contains `Hexalith.Parties.Contracts`.

#### Rejected

- false: Platform fixtures still declare `platform-current-package`. `parties-current*.json` model the published declarations, and the active fixture `parties-active-declaration.json` lacks the retired profile.
- false: an incomplete `released-images.json` passes. The producer writes every resolved server, and `Resolve` sets a digest or throws for each image-backed server, so no partial receipt is reachable without tampering with the run workspace.
- low: the loopback-registry `--insecure` probe is bypassed for run-bound images. No current project defines `HEXALITH_PLATFORM_LOCAL_PACKAGE_QUALIFICATION`, and the fix is a new transport branch.
- false: `LastIndexOf(':')` breaks on untagged or port-only images. `PlatformServerBindings.cs:102` always composes `repository + ":" + version`.
- low: the `PrepareAsync` poll uses `docker ps` without `--all` and waits until cancellation (blind, edge, auditor, verification). Preparations run sequentially under the run token, and the runner already reports exited resources as HXR021 with names. A state-aware loop is more than a direct fix.
- false: with no catalog, the receipt lands where readers look elsewhere. No reader is registered without a catalog.
- low: two matching containers are reported as "loaded a different artifact". This needs a leftover same-run container; the run is still refused.
- false: the `DAPR_HTTP_ENDPOINT` indexer throws `KeyNotFoundException`. The same callback ran in the measured current-owner runs, and Toolkit sets the variable for sidecar containers.
- low: non-git mappings skip the committed-file and composition-identity checks (blind, edge, auditor). The EventStore plan is still runner-signed (`PlatformQualificationOwnerProof`), the bytes must match the plan hashes, and everyday runs use git checkouts.
- low: some error messages are imprecise (`VerifyCurrentSdkFile` wording, the cohort refusal listing only EventStore and Tenants, `MatchesTestSource` swallowing causes). They are diagnostic only.
- low: evidence omits the index digest, cohort and McpCli version. The expected and loaded manifest and config digests and the plan are recorded; the cohort is derived from them.
- low: the parent proof is built twice and `Matches` spawns extra git processes. `rev-parse HEAD:path` and `hash-object` are cheap next to the status scan already bounded on `main`.
- false: `PlatformServerBindings` trusts the proof's presence. `PartiesRunProof` is internal and created only after `Validate`, and the Compose guard and `Bind` re-check `Matches` before any resource.
- low: `HasQualifiedSdkBytes` and `HistoricalPublicRuntimeImageObservations` are left only for tests. Removal means refactoring several fixtures; the historical copy is labelled as such.
- false: the docs overstate the trust model. They state that the catalog-selected tag is resolved from the registry at launch, which is the spec's design.
- false: AC3 is only partially met because new versions need a byte table. The spec explicitly defers replacing package-only byte tables, and AC3 concerns declaration and image identity.
- low: a mixed `platform-package,lifecycle-package` batch takes the Release-owner path. Parties accepts that batch on purpose, its CI selects only `platform-package`, and refusing it needs a new guard.
- low: a prior attempt's container with the same run ID could be recorded. Two matches already refuse, and a stale container cannot pass the digest comparison with a different image.
- false: a duplicate EventStore source entry makes `SingleOrDefault` escape as `InvalidOperationException`. No reachable verified mapping was shown with duplicates; it still fails loudly.
- false: an `Assembly.Load` failure escapes `Bind`. These are package references of Composition and are always deployed with it.

## Implementation Notes

- Keep product edits in the owning checkouts. Use temporary snapshots for clean-Git verification, with the aligned tuple only in the verification workspace. Do not push or change shared pins.
- Tags are resolved with Docker Buildx against the release registry. The runner verifies each selected manifest, launches by digest and compares the owned container's loaded identity with the authenticated manifest or configuration digest.
- A separate repository sync recorded the Parties edits as `bfd63514579b7e9a273875ec421685a9f45702e6` during verification. Parties' local commit needs the coordinated Platform release and CiCd story 21. The legacy workflow already references a verifier absent from its committed tree.

## Spec Change Log

## Review Triage Log

## Verification

- Use `run-with-temp` for disposable clean checkouts and isolated NuGet cache. The current `main` rejects its owner gitlinks. The matching Platform baseline is `157d24e70fbd595e3301276b93b6c3720b631b9b`. Keep the binding checks and regenerate the Parties binding when adopting the reviewed owner revision.
- Run the Parties renderer `--check` and declaration tests, focused Composition controls, and all three `eng/run_tuple_ci.py` suites with positive counts and zero skips. Smoke the real `platform-package` and lifecycle attachment paths against the matching tuple. Record exact source, expected and loaded identities, and cleanup in a compact summary.
- [Verification summary](../../artifacts/story-2-14-package-admission/summary.md) records exact source revisions, expected and observed identities, selectors and outcomes. The disposable collector snapshots aligned six file AppHost directives to the pinned Builds catalog.
- All three local tuple collectors passed with zero skips. Current42 ran 471 tests in one invocation, base42 ran 72, and extensions ran nine. The Parties renderer, declaration tests and focused native binding checks passed.
- The live integration passed the old HXP020 gate and launched the mapped Release owner. Tenants and Memories reported operation digests that differ from the run-scoped catalog. The resolved images match the six previous immutable references. Both smoke runs were cleaned up with zero leftovers.
- The attachment project still fails with CS1061 against the catalog-selected `Hexalith.EventStore.Testing` 4.2.0. The package lacks the attachment lineage and expiry API the tests use. Live readiness and attachment verification keep this spec in progress.
