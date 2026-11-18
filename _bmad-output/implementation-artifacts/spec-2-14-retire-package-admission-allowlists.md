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
