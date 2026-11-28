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
- [x] `PlatformServerBindings.cs`, `PlatformDaprBindings.cs`, `PlatformLocalProfile.cs`, `Declarations/local-profile.json` — enroll the declared workloads. Preserve Folders' matched channel credentials, shared receipt key, separate read and scan proof keys, broker scopes for the inbound worker and dead-letter topics, `folders.events` override, and Memories index routing. Match the released Folders AppHost bindings.
- [x] `PlatformRoutingCatalogBootstrap.cs`, `PlatformCatalogContractBinding.cs`, `PlatformPackageCatalogQualification.cs`, `src/Hexalith.Platform.EventStoreHost/Program.cs` — enroll declared catalog owners and callers, keep package and reader identity checks, and map Folders safe denials to their metadata-only responses. Verify Projects' authorized Conversations advisory and membership calls.
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
- The final Release rerun passed all 73 selected Platform tests. It covers the exact Folders and Projects workload identities for Dapr grants, the existing Conversations source diagnostic and package boundary, and the generated realm configuration. The standalone Keycloak smoke test passed and its container was removed. A broader sweep timed out after 94 passes and one opt-in skip. [Final Platform rerun](../../evidence/story-3-6-platform/final-platform-verification.log) · [Broader run and Keycloak](../../evidence/story-3-6-platform/independent-verification.log)
- Pinned Builds `6.8.4` refused the real selector with `HXP020` and named Parties before assigning a run ID. Builds `6.8.5` reports source `deccbc3bcf4ed687ecf4d556ebe356fe57148777`, which predates the selector candidate. Builds still needs the operator relay and tenant identifier handoff. McpCli still requires source-mapped Contracts support. [Pinned invocation](../../evidence/story-3-6-platform/pinned-run.log) · [Builds release identity](../../evidence/story-3-6-platform/newer-builds-identity.log)
- The generated realm is selected for the complete Development profile. Default reference-composition adoption still needs workload principal bindings independent of the complete selector. Package startup refuses owner containers while the generated issuer is on host loopback. Completing package mode also requires the signed catalog and owner snapshots, released Projects extension and owner executable images. The live Folders delivery and denial path, authorized Projects advisory and membership calls, seven-module readiness, and exact-run teardown of the live environment await compatible inputs. Story 3.6 stays `in-progress`.
- Follow-up Platform wiring on 2026-11-28 (with pinned Builds `6.10.0+aebabdadadffa108801992253c7910f37437b115`): completed Projects channel and shared `HumanAuthorityReceipt` bindings (`PlatformLocalProfile.BindProjects` + `PlatformServerBindings`), added `projects` Dapr gateway rules (`/api/v1/commands`, `/api/v1/streams/read/workload`) and `folders -> memories` (`GET /api/search`), rendered and attached the `projects-conversation-start-setup` Dapr state store component, expanded `PlatformRoutingCatalogBootstrap` (`BoundStreamRead:AllowedDomains:projects`, `BoundOwnerQuery:AllowedQueries:projects` for Conversations, Folders, Tenants and Parties, and `GatewayQueryDomains` across all five query owners), granted `projects` `eventstore:stream-read` in `realm-contract.v1.json` and `AllowAnonymousDevelopment=false` in `PlatformLocalRealm`, deferred Conversations' six realm-supplied settings in `PlatformLocalProfile` when `UseGeneratedRealm` is active, and guarded direct-host `PlatformGatewayStartup` from reading a symmetric signing key under the generated realm. Independent 3-layer review (21 findings) applied patches for `"system"` tenant binding on `eventstore-bound-query-tenants`, `projects-conversation-start-setup` dynamic `AppId` scope and `"projects"` ownership, fail-closed `UseGeneratedRealm && !hasGateway`, whitespace receipt guard, `BindProjects` `AllowAnonymousDevelopment=false`, route `AppId` verification in `ResolveProjectsAllowedOwnerQueries`, and strengthened test coverage (factory-resolved `PlatformGatewayStartup` check, `HEXALITH_LOCAL_REALM_BINDINGS_EVENTSTORE`, YAML parsing, RSA key-pair match, missing-query refusal, and cross-platform exact-run teardown); deferred OIDC direct-host writer-protocol cutover and Projects manifest capability declaration to `deferred-work.md`. Post-patch verification passed 132/132 Release composition/realm/enrollment/catalog/extension tests (0 failed, 0 skipped), 381/381 Python eng tests, `builds_tuple.py`, `owner_packages_lock.py --check`, and `hexalith-module validate`. Running `hexalith-module run --manifest module/hexalith.module-manifest.v2.json` with Builds `6.10.0` built McpCli and the Platform model cleanly, then stopped at `--validate-platform` (`HXP020`) because Builds `6.10.0` `CompositionStartupTasks.Select` only schedules `per-start-verify` tasks (omitting Tenants' `once-per-environment-creation` operator task `create-synthetic-tenant`), and Builds `WriterProtocolCutover` still signs with the symmetric runner key rather than the generated realm. Exact-run `down --run-id` cleanly removed the failed run workspace and state. Story 3.6 and Slice 4 remain `in-progress` pending upstream Builds operator-task/OIDC cutover support and published Projects/owner package artifacts.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| Gap 1. Vacuous `PlatformGatewayStartup` check (`ImplementationType`) | High | Patch (Group A) | `PlatformAppModel.cs:633` uses a factory `ServiceDescriptor`, leaving `ImplementationType == null`; removing `!options.UseGeneratedRealm` still passes the test. |
| Gap 2. Expanded `GatewayQueryDomains` unverified in Keycloak bindings | Medium | Patch (Group B) | No test inspects `HEXALITH_LOCAL_REALM_BINDINGS_EVENTSTORE` on `platform-local-realm` for `folders`, `tenants`, and `parties` bound query grants. |
| Gap 3. Rendered `projects-conversation-start-setup.yaml` content unverified | Medium | Patch (Group C) | `AddDaprComponent` does not parse `LocalPath` at compose time, so missing or invalid YAML (`redisHost`, `keyPrefix`, `scopes`) passes the current test. |
| Gap 4. `RequireOwnerQuery` refusal path untested | Medium | Patch (Group D) | Deleting the `InvalidDataException` guard in `RequireOwnerQuery` still passes every test because only the complete happy-path catalog is tested. |
| Blind 1. Vacuous `PlatformGatewayStartup` assertion | High | Patch (Group A) | Same root cause as Gap 1: factory registration leaves `ImplementationType == null`. |
| Blind 2. Missing writer-protocol cutover for direct `UseGeneratedRealm` runs | Medium | Defer (Group E) | Builds 6.10.0 `CompositionWriterProtocolCutover.ActivateAsync` only supports symmetric HS256 signing and `realm-contract.v1.json` has no cutover client; requires coordinated Builds/realm OIDC cutover work. |
| Blind 3. `projects-conversation-start-setup` ownership, hardcoded scope, and `Capabilities` | Medium | Patch (Group F) + Defer | Fixed in Platform: use resolved `projects` `AppId` for `scopes` and attribute `PlatformResourceOwnerAnnotation` to `"projects"`. Deferred: exposing it in `PlatformDaprBindings.Capabilities` requires a Projects manifest capability declaration. |
| Blind 4. `UseGeneratedRealm` silently omits inputs without `eventstore` gateway | Medium | Patch (Group G) | `PlatformAppModel.cs:320` only creates the realm when `hasGateway` is true, so `UseGeneratedRealm && !hasGateway` would skip keys without `PlatformLocalRealm` running; fail closed before adding resources. |
| Blind 5. `AllowAnonymousDevelopment=false` only set in `PlatformLocalRealm` | Low | Patch (Group H) | `ProjectsAuthenticationOptions.AllowAnonymousDevelopment` defaults to `false`, and setting it in `PlatformLocalProfile.BindProjects` is a one-line explicit guard for non-realm `projects` compositions too. |
| Blind 6. `BindProjects` channel-token overwrite and missing failure/order tests | Low | Patch (Group I) | Per-run random channel-token generation matches `BindFolders`; add direct tests for missing `eventstore` refusal and `BindProjects`-before-`BindFolders` shared receipt. |
| Blind 7. Unverified `GatewayQueryDomains`, `BoundStreamRead` key pair, and `RequireOwnerQuery` route `AppId` | Medium | Patch (Groups B, D, J) | Verify `HEXALITH_LOCAL_REALM_BINDINGS_EVENTSTORE`, RSA key-pair match for `projects`, and route `AppId` + refusal tests for `RequireOwnerQuery`. |
| Blind 8. Temp workspace leak on early test assertion failure | Medium | Patch (Group K) | `renderedDirectory` was assigned 40 lines after `PlatformAppModel.Compose`, so earlier assertion failures bypass `finally` cleanup. |
| Blind 9. Missing-extension refusal not exercised by `CompleteCompositionRefusesMissingMappedOwnerOrExtensionBeforeAddingResources` | Low | Patch (Group L) | `EventStoreExtensionCompositionTests` covers extensions separately, but this test's name promised extension refusal; add an explicit missing-extension check here too. |
| Blind 10. POSIX-only `DockerCommand = "true"` and synthetic expiry test | Low | Patch (Group M) | Make `DockerCommand` cross-platform; `StartAsync` expiry requires `Hexalith.Builds.Module.Cli` (`ExecutingToolIdentity.Capture`) which is covered by Builds' own suite. |
| Blind 11. Unused `using Microsoft.Extensions.Hosting;` | Low | Patch (Group A) | Becomes actively used by the Group A fix calling `GetServices<IHostedService>()`. |
| Edge 1. Direct host `UseGeneratedRealm` leaves writer-protocol health check unhealthy | Medium | Defer (Group E) | Same root cause as Blind 2 (upstream Builds HS256-only `CompositionWriterProtocolCutover`). |
| Edge 2. `UseGeneratedRealm` without `eventstore` gateway skips auth settings | Medium | Patch (Group G) | Same root cause as Blind 4; fail closed in `PlatformAppModel.Compose` when `UseGeneratedRealm && !hasGateway`. |
| Edge 3. `tenants` bound query grant omits `"system"` tenant | High | Patch (Group N) | Verified in `ProjectOwnerBootstrapVerifier.cs:24`: `ResolveTenantOwnerAuthority` queries `tenants` under tenant `"system"`; include `"system"` in `eventstore:bound-tenant` for `audience == "tenants"`. |
| Edge 4. Whitespace-only receipt key accepted by `SharedHumanAuthorityReceiptKey` | Low | Patch (Group O) | `string.IsNullOrEmpty(receipt)` accepts whitespace; switch to `string.IsNullOrWhiteSpace(receipt)`. |
| Edge 5. Descendant `/api/v1/commands/*` grant would widen `projects` ACL | False | Reject | `PlatformDaprBindings.cs:102-107` grants `projects` only `/api/v1/commands`, `/api/v1/queries/owner-authority/workload`, and `/api/v1/streams/read/workload`; no `/api/v1/commands/*` descendant exists for `projects`, so widening never occurs. |
| Edge 6. Early assertion failure leaks temp workspace and secrets | Medium | Patch (Group K) | Same root cause as Blind 8; capture `renderedDirectory` immediately after `PlatformAppModel.Compose`. |

## Verification

- Use `run-with-temp` for disposable builds, test checkouts and live run data. Save sanitized evidence outside the temporary workspace and verify teardown by the exact run ID.
- `dotnet tool run hexalith-module -- run --manifest module/hexalith.module-manifest.v2.json` — expect declared readiness and a mapped McpCli build. Repeat with `--mode package` after the release inputs are available.
- Run the Platform Composition tests and the Builds tuple, owner-lock and intake checks. Capture the run descriptor, `HXI010`, `readiness.json`, loaded extension identities and the McpCli build evidence for live acceptance.
