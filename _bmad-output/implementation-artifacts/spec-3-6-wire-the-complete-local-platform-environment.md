---
title: '3.6 slice 4: Wire the complete local Platform environment'
type: 'feature'
created: '2026-11-26'
status: 'in-progress'
route: 'dispatch'
baseline_commit: '332de5fd4fa84224dba5ffd830d38c8d38875747'
story_key: '3-6-run-all-seven-mvp-modules-together-locally'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem.** Platform lacks the complete local entry point and runtime bindings after the Folders, Projects and Builds preparation slices.

**Approach.** Use the Builds complete selector to run EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories from one command. Projects keeps Conversations as a required supporting service. One Platform gateway loads the declared extensions, and the runner builds McpCli under the run's mapping.

## Boundaries & Constraints

- Preserve individual-module, qualification, and opt-in behavior. Select one verified declaration graph and one mapping per run. Use mapped Debug projects in source mode and authenticated catalog artifacts in package mode. Keep McpCli run-scoped and stdio-only.
- Adopt the generated local realm for the Development AppHost and complete runner, as deferred in Story 3.4. Carry the synthetic tenant identifier and preserve the declared startup task. Story 3.7 owns McpCli's connected profile and discovery.
- Prepare Platform changes locally using candidate inputs with recorded identities. Final acceptance requires published owner artifacts and a signed Builds release. Keep publication and hosted changes outside this slice.

## I/O & Edge-Case Matrix

| Input | Expected |
| --- | --- |
| Complete selector | Seven MVP modules and Conversations selected once, one gateway, mapped McpCli artifact, all declared services and tasks ready. |
| Missing or mismatched owner, project, extension, catalog or image | Refuse the selection with the failing identity and input named. |
| Multiple declared deadlines | Largest finite override reported with owner and reason. Otherwise use the default. |
| Startup fails or expires | Record actual duration, name unready resources, retain diagnostics and clean up by run identity. |

</frozen-after-approval>

## Code Map

- `apphost.cs`, `PlatformDeclarationSet.cs` and `PlatformAppModel.ComposeRun` consume the runner graph. The completed Builds candidate exposes `CompositionDeclarationClosure.ComposeVerifiedModel` and reports `HXI010` plus `readiness.json`.
- Under `src/Hexalith.Platform.Composition/`, `PlatformServerBindings` locates owner projects and images. `PlatformDaprBindings` renders scoped components. `PlatformLocalProfile` binds owner inputs. `PlatformEventStoreExtensionBindings` builds the one gateway from declared extensions.
- `PlatformRoutingCatalogBootstrap`, `PlatformCatalogContractBinding` and `PlatformPackageCatalogQualification` gate readers, routes and the package run. `PlatformReleasedImages` verifies the image digests.
- `PlatformLocalRealm` and `Declarations/realm-contract.v1.json` render the Works preview realm today. Generalize the Works-specific gateway audiences and bind declared principals, including the Conversations access reader. `PlatformNativeTestPreparation` and Builds `CompositionTokenFactory` identify callers still using the runner signing key.
- The Builds selector is local commit `43206d060c33b3ee87050f57141162d84ae3d4bf`. Projects merged its extension in `4419c5598f9433e31e1d8ae727cc9238e6f67056`; the extension and package-mode executable still need release evidence. McpCli's `Build/ModuleAssemblyManifest.targets` needs source-mapped Contracts support. The Story 3.5 task handoff describes the required operator relay for `create-synthetic-tenant`.

## Tasks & Acceptance

**Execution**

- [x] `module/hexalith.module-manifest.v2.json`, `apphost.cs`, `README.md` — add the serverless Platform selector and document the complete `run`, `debug` and exact-run `down` commands. Pass one verified closure into composition.
- [ ] `PlatformServerBindings.cs`, `PlatformDaprBindings.cs`, `PlatformLocalProfile.cs`, `Declarations/local-profile.json` — enroll the declared workloads. Preserve Folders' matched channel credentials, shared receipt key, separate read and scan proof keys, broker scopes for the inbound worker and dead-letter topics, `folders.events` override, and Memories index routing. Match the released Folders AppHost bindings.
- [ ] `PlatformRoutingCatalogBootstrap.cs`, `PlatformCatalogContractBinding.cs`, `PlatformPackageCatalogQualification.cs`, `src/Hexalith.Platform.EventStoreHost/Program.cs` — enroll declared catalog owners and callers, keep package and reader identity checks, and map Folders safe denials to their metadata-only responses. Verify Projects' authorized Conversations advisory and membership calls.
- [ ] `PlatformLocalRealm.cs`, `PlatformAppModel.cs`, `PlatformNativeTestPreparation.cs`, `Declarations/realm-contract.v1.json` — bind the generated realm's issuer, audiences and grants across the gateway and selected clients. Coordinate any runner-side token acquisition change with Builds.
- [ ] `eng/builds-tuple.json`, `.config/dotnet-tools.json`, `hexalith-platform.json`, `eng/owner_packages_lock.py`, `eng/module-intake.json`, `src/Hexalith.Platform.CatalogBuilder/OwnerPackageLock.cs`, `Declarations/provenance.json` — adopt authenticated releases, update gitlinks and generate locks and snapshots per `eng/tuple-source-preparation.md`. Require released executable identities for package startup.
- [ ] `tests/Hexalith.Platform.Composition.Tests/` — extend declaration, realm, catalog and lifecycle tests for the matrix. Capture an isolated complete run and verify exact-run teardown.

**Acceptance Criteria**

- Given the compatible pins, when the Platform manifest runs in source mode, then all declared workloads reach readiness within the effective deadline and a run-scoped McpCli builds from the mapped Contracts. The runner reports the deadline, its override status and the measured duration.
- Given the ready environment, when an authorized Folders command reaches the composed gateway, then the declared adapter runs and the worker and Memories consumers receive their intended deliveries. Safe denials preserve their status and response shape.
- Given authenticated package inputs, when the complete selector runs in package mode, then the same graph uses locked Release artifacts. Missing inputs fail with the named identity.

## Implementation Notes

- The selector and README are present. The existing `apphost.cs` remains the thin entry point and calls `PlatformAppModel.ComposeEntry`. The complete runner uses the declaration closure passed through the Builds model call. It requires one scheduled Tenants operator task with the declared executable and arguments before adding resources.
- The local model binds Folders and Projects to the composed gateway, preserves Folders' owner-specific subscription and proof settings, and selects local realm principals from the declaration graph. The gateway registers the metadata-only denial handler before EventStore's global exception handler. Package preflight verifies owner package bytes and requires the released image digest for each image-bound workload.
- The Release build and 64 focused Platform tests passed. The exact Builds candidate `43206d060c33b3ee87050f57141162d84ae3d4bf` passed 21 fixture tests for complete selection, override and timeout diagnostics, mapped fixture Contracts, and exact-run teardown. All 77 Python identity checks, the module intake check, and the current authenticated owner-lock and Builds-lock checks passed. [Platform tests](../../evidence/story-3-6-platform/composition-tests.log) · [Builds fixture tests](../../evidence/story-3-6-platform/builds-candidate-focused.log) · [Identity checks](../../evidence/story-3-6-platform/identity-tests.log)
- Pinned Builds `6.8.4` refused the real selector with `HXP020` and named Parties before assigning a run ID. Builds `6.8.5` reports source `deccbc3bcf4ed687ecf4d556ebe356fe57148777`, which predates the selector candidate. Builds still needs the operator relay and tenant identifier handoff. McpCli still requires source-mapped Contracts support. [Pinned invocation](../../evidence/story-3-6-platform/pinned-run.log) · [Builds release identity](../../evidence/story-3-6-platform/newer-builds-identity.log)
- Completing the package path requires the signed catalog and owner snapshots, released Projects extension and owner executable images, and a generated issuer that owner containers can reach. The current issuer listens on the host loopback. The live Folders delivery and denial path, authorized Projects advisory and membership calls, seven-module readiness, and exact-run teardown of the live environment await compatible inputs. Story 3.6 stays `in-progress`.

## Spec Change Log

## Review Triage Log

## Verification

- Use `run-with-temp` for disposable builds, test checkouts and live run data. Save sanitized evidence outside the temporary workspace and verify teardown by the exact run ID.
- `dotnet tool run hexalith-module -- run --manifest module/hexalith.module-manifest.v2.json` — expect declared readiness and a mapped McpCli build. Repeat with `--mode package` after the release inputs are available.
- Run the Platform Composition tests and the Builds tuple, owner-lock and intake checks. Capture the run descriptor, `HXI010`, `readiness.json`, loaded extension identities and the McpCli build evidence for live acceptance.
