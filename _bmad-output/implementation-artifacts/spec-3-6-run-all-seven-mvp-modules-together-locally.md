---
title: '3.6 slice 1: Folders joins a Platform composition'
type: 'feature'
created: '2026-11-25'
status: 'in-progress'
baseline_commit: 'e416dfac2a6660efd0720fa1d278f8cda3fb329d'
route: 'dispatch'
story_key: '3-6-run-all-seven-mvp-modules-together-locally'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 needs Folders in the composed environment. Folders has no v2 module manifest, so Builds cannot select it. Its 16 idempotency-intent adapter classes are internal to `Hexalith.Folders.EventStore`, a Folders-built EventStore host whose `eventstore` image Story 3.2 refuses, so Platform's composed `eventstore` host cannot load them.

**Approach:** In Folders, move the adapters into a packable, provider-free `Hexalith.Folders.EventStoreExtensions` library that declares EventStore extension API v1. Make Folders' catalog declarations reachable from `Hexalith.Folders.Contracts`, author `module/hexalith.module-manifest.v2.json`, and stop building an `eventstore` image. Then release Folders.

**Decisions (2026-11-25):**
- Split Story 3.6. This spec is the Folders slice. The Projects, Builds and Platform slices follow, and each is recorded in deferred-work.
- Keep `Hexalith.Folders.EventStore` as a development-only host for Folders' own AppHost. It has no container image and consumes the new package.
- The manifest declares only the `domain` (Server) and `workers` servers. The UI joins after Folders Story 13.4 adds its health endpoints.
- Keep this spec whole, above the size target, so that Folders ships in one release.

## Boundaries & Constraints

**Always:**
- The package must not bring Dapr, Octokit, LibGit2Sharp or any other provider SDK into the composed host, directly or transitively. It therefore cannot reference the `Hexalith.Folders` domain assembly.
- Moved types keep their namespaces and names.
- One adapter inventory. Its route ids equal the declared command route ids, and each host registers it once.
- `[assembly: EventStoreExtension("folders", 1)]` matches the manifest entry, which has configuration inputs only: no secrets and no Dapr roles (HXP025–HXP028).
- The manifest validates with Builds 6.7.0's v2 schema, declares `identity.dependencies` `eventstore`, `tenants` and `memories`, uses the existing `/ready` endpoints and keeps `lifecycle.startup` at `{}`.
- Existing Folders behaviour, admission settings and tests keep passing.
- Work in a worktree branch from Folders `origin/main` (`fafdb951`), not the `1c8da626` checkout.
- Pushing Folders main and dispatching its release each need the maintainer's yes.

**Never:**
- Platform, Builds, Projects or McpCli changes (later slices).
- Enrollment-kit files (hosting inventory, handoff, SM-2 checklist), which belong to Story 3.11.
- Enrolling Folders' legacy MCP server or CLI.
- A module artifact or image named `eventstore`.
- Changing Folders command contracts or idempotency semantics.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected | Error Handling |
|---|---|---|---|
| Load | Test host calling `AddEventStoreExtensions` with the package and `EventStore:Extensions:Packages:0` = folders/1 | Loader admits it, registers every adapter, `eventstore-extensions` ready | — |
| Wrong major | Configured major 2 | Loader refuses, naming the package | Host not ready |
| Inventory drift | Adapter without a declared command, or the reverse | Folders test fails naming the route id | — |
| Provider leak | Package dependency closure | No `Dapr*`, Octokit or LibGit2Sharp asset | Test names the package |
| Image | Every Folders project | None sets `ContainerRepository=eventstore` | — |

</frozen-after-approval>

## Code Map

All paths are relative to `references/Hexalith.Folders` unless they start with `references/` or `src/`.

- `src/Hexalith.Folders.EventStore/`: the 16 `*IdempotencyIntentAdapter.cs` classes plus `FolderAccessIdempotencyIntent`, `FoldersCanonicalIntentBuilder`, `FoldersCommandIntentDeclarations`, `FoldersCatalogAdapters` and `FoldersCatalogAdapterSource` (`[assembly: RouteCatalogAdapterSource]`, `Create()`). `Program.cs` is the full gateway host. The csproj sets `ContainerRepository=eventstore`.
- Domain coupling: `OrganizationAclIdempotencyIntentAdapter.cs:7` uses the organization ACL model in `src/Hexalith.Folders/Aggregates/Organization` (about 224 lines). `FoldersCommandIntentDeclarations.cs:4` uses `src/Hexalith.Folders/Routing/FoldersCommandCatalog.cs`, which needs only EventStore.Contracts. `Hexalith.Folders.csproj:18-25` brings Dapr.Client, Octokit and LibGit2Sharp.
- `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj:49-53` link-compiles the adapter files. `FolderServerCatalogAdapterSource.cs` calls `FoldersCatalogAdapters.Create()`.
- `src/Hexalith.Folders.Server/FoldersRuntimeCatalogDeclarations.cs:10` and `FoldersProjectionCatalogDeclarations.cs:5` carry `[RouteCatalogDeclarationSource]`. Platform's catalog builder looks on `Hexalith.Folders.Contracts` (`src/Hexalith.Platform.Composition/PlatformCatalogContractBinding.cs:12,42`).
- `src/Hexalith.Folders.AppHost/Program.cs:21-35` registers the Folders EventStore host as `eventstore`. `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs:10,20,22` holds the app ids and components.
- `src/Hexalith.Folders.ServiceDefaults/Extensions.cs:135-155` maps `/ready` for Server and Workers.
- Pattern to copy: `tests/fixtures/Hexalith.Platform.TestExtension/` (csproj, `PlatformTestExtensionAdapters.cs:37-63`, manifest l.17-30). Loader: `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Extensions/EventStoreExtensionLoader.cs`. Schema: `references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json` (`eventStoreExtension` l.184-221, `lifecycle` l.910-1180). Manifest shape: `references/Hexalith.Projects/module/hexalith.module-manifest.v2.json`.
- Release: semantic-release on `main`, with `release.yml` run by `workflow_dispatch` after green CI. Packages are listed in `tools/release-packages.json` and `deploy/nuget/release-packages.yaml`.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders.Contracts` -- Move `FoldersCommandCatalog`, the organization ACL command model and the two declaration sources here, keeping namespaces and replacing any server-type reference with its literal name -- Gives Platform's catalog builder and the package one provider-free home.
- [x] `src/Hexalith.Folders.EventStoreExtensions/` -- New packable, non-publishable library with the moved adapters, both assembly attributes and the `Create()` source -- This is the enrollable unit.
- [x] `src/Hexalith.Folders.Server`, `src/Hexalith.Folders.EventStore`, `src/Hexalith.Folders.AppHost` -- Reference the package instead of linked files and keep one registration per host. Remove `EnableContainer` and `ContainerRepository` from the development host, and keep the AppHost resource -- Keeps Folders' own runs working.
- [x] `module/hexalith.module-manifest.v2.json` -- Declare the `domain` and `workers` servers (app ids `folders` and `folders-workers`) with their Dapr roles, `/ready` readiness, dependencies and the `eventStoreExtensions` entry -- Lets Builds select Folders.
- [x] `tools/release-packages.json`, `deploy/nuget/release-packages.yaml` -- Publish the new package -- Platform pins released packages.
- [x] `tests/` -- Cover every matrix row and validate the manifest -- Proves the contract.

**Acceptance Criteria:**
- Given the released Folders version, when Platform reads its manifest and restores `Hexalith.Folders.EventStoreExtensions`, then both are available without a Folders source checkout.
- Given Folders' own AppHost, when it runs, then Folders commands keep their admission results.

### Review Findings

2026-11-26 release review of Folders `4f0ccfd7..20f5cf01` and Platform `22540801`. All four review layers reported. The four patch items, eleven deferred items, and one rejected finding below account for nineteen findings before deduplication.

- [x] [Review][Patch] **High. Keep the complete seven-module story in progress.** The tracker defines `review` as implementation complete. The Projects, Builds, and Platform slices are pending, and the story still needs a source-mapped McpCli build, one composed gateway with Folders adapters, and seven-module readiness evidence with the effective deadline and elapsed time. [Sprint tracker](sprint-status.yaml#L152) [Story 3.6](../planning-artifacts/epics.md#story-36-run-all-seven-mvp-modules-together-locally)
- [x] [Review][Patch] **Medium. Exercise the packed extension in the release gate.** The isolated consumer builds an empty class and rejects only unexpected dependencies. The existing loader test uses project references. A future package that omits its required Folders Contracts dependency could pass these checks. Load the packed candidate through EventStore and assert adapter registration and readiness. The published 2.1.0 package passed that probe. [Package consumer check](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/scripts/validate-consumer-package-references.py#L52)
- [x] [Review][Patch] **Medium. Extend the PD10 source inventory to cover the relocated project and development registration.** The explicit inventory lists the existing extension files, but the extension directory is outside the roots that detect future unlisted compile inputs. The changed development gateway registration file is also absent from the explicit inventory. [Coverage roots](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/scripts/pd10-v2-coverage-roots.txt#L1) [Source inventory](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/scripts/pd10-v2-story-owned-paths.txt#L214)
- [x] [Review][Patch] **Low. Test the declared worker readiness endpoint against its host.** The worker maps `/ready`, but the existing manifest and endpoint tests can both pass if the manifest drifts to a different path. Probe the endpoint read from the manifest. [Worker readiness declaration](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/module/hexalith.module-manifest.v2.json#L226)

- [x] [Review][Defer] **High. Wire a shared human-authority receipt key into the gateway and Folders domain host.** The existing AppHost provides it to both. The domain verifier rejects a missing or nonmatching key, and the command endpoint returns 401. The Platform slice owns the host binding and authenticated mutation proof. [AppHost binding](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/src/Hexalith.Folders.AppHost/Program.cs#L61) [Receipt verifier](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/src/Hexalith.Folders.Server/Authentication/HumanAuthorityReceiptVerifier.cs#L38)
- [x] [Review][Defer] **Medium. Bind app-channel and workload credentials for both Folders hosts.** The manifest inventories two secret keys, but the current Platform profile reads runtime extension inputs and has no Folders binding. The Platform slice owns the actual owner and sidecar configuration. [Manifest inventory](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/module/hexalith.module-manifest.v2.json#L203) [Platform profile binder](../../src/Hexalith.Platform.Composition/PlatformLocalProfile.cs#L29)
- [x] [Review][Defer] **Medium. Enroll native source read and scan proofs for the Folders domain and workers.** The Folders AppHost provisions distinct keys and gateway allowlists. The current composition bootstrap lacks that Folders setup. Verify both callers through the composed gateway in the Platform slice. [AppHost proof bindings](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/src/Hexalith.Folders.AppHost/Program.cs#L179)
- [x] [Review][Defer] **Medium. Preserve the Folders publisher topic override.** EventStore otherwise publishes to tenant-qualified topics, while the Folders workers subscribe to `folders.events`. Apply and verify the publisher binding in the Platform slice. [Existing topic override](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/src/Hexalith.Folders.Aspire/FoldersAspireModule.cs#L92)
- [x] [Review][Defer] **High. Support the Folders inbound worker pub/sub role.** Platform's capability switch throws before adding resources when it sees `worker-inbound-events`. The Platform slice needs a component on the publisher's broker with the existing subscription and dead-letter scopes. [Role resolution](../../src/Hexalith.Platform.Composition/PlatformDaprBindings.cs#L43) [Worker component](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/deploy/dapr/production/pubsub.workers.yaml)
- [x] [Review][Defer] **Medium. Route Folders producer events into the Memories index.** The existing AppHost maps `hexalith-folders` to `folders-index` and enables provisioning. The Platform slice needs that receiving binding and delivery proof in addition to the `memories-events` publisher. [Memories registration](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/src/Hexalith.Folders.Aspire/FoldersAspireModule.cs#L63)
- [x] [Review][Defer] **High. Preserve Folders' synchronous command denials at the composed gateway.** The Folders development gateway registers the handler for `DomainServiceSafeDenialException`. The Platform gateway delegates to the SDK's generic exception handling, which returns HTTP 500 for this unmapped exception. The Platform and EventStore owners need to preserve the intended 401, 403, 404, and 503 responses and their metadata-only shape. [Development gateway registration](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/src/Hexalith.Folders.EventStore/Program.cs#L22) [Platform gateway](../../src/Hexalith.Platform.EventStoreHost/Program.cs#L24)
- [x] [Review][Defer] **Medium. Complete the Folders AppHost admission comparison.** The candidate kept returning `projection_unavailable` through its deadline. The pre-integration control also returned `projection_unavailable` and later returned 202 where the test expected 404. The recorded comparison leaves the AppHost acceptance criterion open. Folders owns a successful acceptance run against a valid control. [Recorded runs](#implementation-notes)
- [x] [Review][Defer] **Medium. Prevent Builds release pin drift.** The preflight test supplies a literal SHA without comparing it to the workflow action reference, input, and environment value. The released pins agree. The earlier failure and the follow-up are already in the ledger. [Release workflow](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/.github/workflows/release.yml#L89) [Deferred work](deferred-work.md)
- [x] [Review][Defer] **Medium, unverified. Confirm the post-release NuGet policy scope.** The release notes record widening the policy to `Hexalith.Folders*`. The runbook requires the six exact IDs with new-version scope after first publication. A read-only check of the registered policy by its owner would establish the current scope. [Trusted publishing runbook](https://github.com/Hexalith/Hexalith.Folders/blob/v2.1.0/docs/operations/release-packages.md#L41)
- [x] [Review][Defer] **Medium. Resolve the standalone Builds checker gate.** The prescribed `check.py` run exited on the missing `.github/hexalith-ci.json` adoption file, which is absent at the release commit. Folders owns adoption and a passing rerun or an explicit change to the verification requirement. [Verification commands](#verification) [Recorded result](#implementation-notes)

**Rejected**

- **Maybe-false, low. Explicit projection reconciliation path.** The manifest omits `/project/v2/reconcile`. The current renderer emits `/project/*` from the declared parent and child, and no denied reconciliation was established. The composed projection test can verify reachability without adding a speculative rule. [ACL renderer](../../src/Hexalith.Platform.Composition/PlatformDaprBindings.cs#L207)

**Release verification.** All six NuGet 2.1.0 archives report repository commit `20f5cf01cdc5f27a46f3d4984d066d6ace5000ea`. The packaged manifest matched the tagged file byte for byte and Builds 6.7.0 validated it. A fresh NuGet-only restore of the extension found no provider SDKs. A standalone host using EventStore.Server 5.5.1 loaded 18 distinct adapters and reported the extension healthy. [Extension on NuGet](https://www.nuget.org/packages/Hexalith.Folders.EventStoreExtensions/2.1.0) [Release run](https://github.com/Hexalith/Hexalith.Folders/actions/runs/45780260331)

**Patch verification.** All four patches are applied locally. The Folders changes are in the standalone Folders checkout based on `20f5cf01`. The unpublished `2.1.1-review.1` release dry run validated six package and symbol pairs and four isolated consumers. The new loader registered all 18 declared routes and reported `Healthy`. The worker readiness test and all 21 PD10 tests passed. Negative checks rejected a missing Contracts dependency and an unlisted extension source file. Live Folders admission parity and the seven-module acceptance run remain open. [Release dry run](../../../folders-story-3-6-release-review-tests.log) [Worker and PD10 tests](../../../folders-story-3-6-worker-pd10-review-tests.log)

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.slnx -c Debug -nodeReuse:false` -- expected: 0 warnings, 0 errors.
- Folders test projects for Contracts, EventStore, Server and AppHost, each invoked directly -- expected: all pass.
- `dotnet tool run hexalith-module -- validate --manifest module/hexalith.module-manifest.v2.json` (Builds 6.7.0) -- expected: valid.
- `python3 references/Hexalith.Builds/check.py` from a standalone clone before the push -- expected: success.

## Implementation Notes

- Folders worktree: `/home/administrator/projects/hexalith/folders-story-3-6`, branch `feat/folders-eventstore-extension`, pinned source `fafdb951c9ca76d8c5f28385f54c83f1d6dc343d`. Use the umbrella workspace's declared AI.Tools baseline for this linked worktree. The local Folders `origin/main` reference has advanced beyond the pinned source.
- Run disposable verification jobs with `run-with-temp`, keeping temporary copies, package feeds, and build outputs under the private `$TMPDIR`. Keep useful logs outside it and wait for child work to finish.
- The extension ships the source manifest under `build/hexalith.module-manifest.v2.json` in its NuGet archive. A clean package-only restore confirmed the provider-free graph and byte equality. A separate package-only EventStore 5.5.1 host loaded all 18 route adapters and reported healthy. The EventStore suite passed 127 tests, including wrong-major rejection and the route inventory check. The image check passed in the focused Contracts run.
- The package-mode CI solution built with zero warnings and errors. Contracts passed all 776 tests, Server passed 1,509 with 11 skips, AppHost passed 176 with 16 skips, and Workers passed all 194. The AppHost catalog test verified all 18 command policies and 23 operation declarations. The isolated pinned-source AppHost reported healthy for EventStore, Folders domain, Workers, UI and Tenants. Memories exited with code 1. The live run checked readiness. The admission tests compare the existing command intent vectors and refusal behavior.
- A compiled comparison against the pinned Folders revision matched all 23 route declarations, request and result schemas, eligibility values, and digests. The image check now covers `src`, `samples`, and `tests` and rejects an injected `eventstore` image in both added directories. The updated Contracts suite passed 776 tests and EventStore passed 127, both with zero skips. The focused topology and release tooling suite passed 45 tests using `TMPDIR=/var/tmp run-with-temp` because `/tmp/.git` triggers the standalone checkout guard. [Route comparison](../../../folders-story-3-6-route-parity.log), [image guard](../../../folders-story-3-6-image-inventory.log), [Contracts rerun](../../../folders-story-3-6-contracts-final-resume.log), [EventStore rerun](../../../folders-story-3-6-admission-resume.log), [CI tooling](../../../folders-story-3-6-ci-gates-resume.log).
- The opt-in live AppHost test completed tenant bootstrap and the organization ACL baseline on the candidate. The first public create request returned HTTP 503 with `projection_unavailable` until the retry deadline. The pre-change control failed earlier in `AddUserToTenant`, with HTTP 500 `unavailable` after a Tenants invocation timed out. The comparison could not establish live admission parity. Both runs used umbrella Platform `e416dfac2a6660efd0720fa1d278f8cda3fb329d` because the Platform gitlink pinned by Folders is unavailable locally. [Candidate](../../../folders-story-3-6-live-admission-resume.log), [control](../../../folders-story-3-6-live-baseline-resume.log).
- The pinned-source solution built with explicit source mode and four warnings from dependency build output. Builds 6.7.0 validated the manifest. The standalone `python3 references/Hexalith.Builds/check.py` exited at the missing `.github/hexalith-ci.json` adoption file. The legacy release dry run produced and validated all six package and symbol pairs and three isolated consumers.
- Workers keep code-owned subscriptions for `deadletter.tenants.events` and `deadletter.folders.events`. The pinned v2 manifest cannot directly express their dotted physical names in the dead-letter inventory. The later composition slices need the isolated `worker-inbound-events` role and preservation of those subscriptions. The manifest declares the domain tenant-event subscription and shared `memories-events` publisher.
- The release inventory and workflow are prepared. Publication requires Folders main push approval, green CI on that revision, separate release dispatch approval, and NuGet trusted publishing authorization for the first version of the new package ID.
- A scratch three-way application against cached Folders `origin/main` at `a3a255ba2a1aecc8508acaf219c563d4e0b7a7bc` applied the product changes cleanly. The generated Story 12.2 PD10 source inventory conflicted and needs regeneration on the integrated source. [Integration rehearsal](../../../folders-story-3-6-integration-rehearsal.log).
- The maintainer approved the Folders `main` push on 2026-11-25. Integration on `4f0ccfd7b116770df699a700858a8c5e29a4e72c` preserved the newer ACL validator when relocating it to Contracts and regenerated the PD10 inventory. Commit `05247925ce512e877ca3c06eac9cbb2c0013ece3` passed commit lint, manifest validation, the Release build, 2,093 selected .NET tests, and 45 CI tooling tests before the push. The test selection excluded 27 cases by their existing opt-in rules. [Push and local verification](../../../folders-story-3-6-approved-main.log), [exact-source CI](https://github.com/Hexalith/Hexalith.Folders/actions/runs/45691332500).
- The live AppHost test ran on both commits with the exact Folders gitlinks, including Platform `3848af49f9afc91b174b4c716751fe49cb15dbba`. Both completed tenant setup and the organization ACL baseline, then saw `projection_unavailable` while probing the first public create without the grant. The pushed commit reached its retry deadline. The pre-integration commit eventually returned HTTP 202 where the test expects HTTP 404. Live admission parity remains unverified. [Pushed commit](../../../folders-story-3-6-main-live-admission.log), [pre-integration commit](../../../folders-story-3-6-preintegration-live-admission.log).
- The release dry run on `05247925ce512e877ca3c06eac9cbb2c0013ece3` validated six package and symbol pairs and three package-only consumers. Both blocking hosted CI jobs flagged the same two scaffold assertions for the new solution project and dependency edges. [Package dry run](../../../folders-story-3-6-main-release-dry-run.log), [hosted baseline failure](https://github.com/Hexalith/Hexalith.Folders/actions/runs/45691332500/job/139477260667).
- Follow-up commit `2499dd3a63c6aa14bead7062a58d7d092e658f02` updates the scaffold project and dependency expectations in one test file. All 149 tests in the affected suite and all 37 checks in the full local baseline gate passed before the push. The `ci.yml` push run succeeded on that exact commit, which is the verified Folders `main` tip. The build-and-test, specialized, end-to-end, and accessibility jobs passed. The optional Aspire and performance jobs were skipped. Release dispatch still needs separate maintainer approval. [Baseline gate](../../../folders-story-3-6-scaffold-correction.log), [push](../../../folders-story-3-6-scaffold-push.log), [successful exact-source CI](https://github.com/Hexalith/Hexalith.Folders/actions/runs/45697563675).
- Local evidence: [Contracts and build](../../../folders-story-3-6-contracts-confirmed.log), [Server](../../../folders-story-3-6-server-confirmed.log), [AppHost suite](../../../folders-story-3-6-apphost-suite.log), [EventStore and Workers](../../../folders-story-3-6-final-manifest-workers.log), [manifest and package closure](../../../folders-story-3-6-final-package-closure.log), [package-only host](../../../folders-story-3-6-package-only-host.log), [release dry run](../../../folders-story-3-6-release-dry-run.log), and [standalone checker](../../../folders-story-3-6-check.log).
- Code review on 2026-11-26 of Folders `4f0ccfd7b116770df699a700858a8c5e29a4e72c..2499dd3a63c6aa14bead7062a58d7d092e658f02` raised 28 findings (29 triage rows, because Blind 10 was split). Twelve were fixed by seven patches in Folders commit `20b7a0a82d1056030088e7e94cadcc2d416d4f73`, two were deferred, and fifteen were rejected (see the Review Triage Log). The patch lists the new package first in the release inventory, corrects the preflight message, and adds tests for handler identity, type forwarding and the manifest's pub/sub keys. The CI-mode baseline gate (`CI=true`) passed every category. The first run's full suites also passed: Contracts 781, Server 1,567 (11 not run), EventStore 127, AppHost 193 (10 skipped), Workers 204, CI tooling 76. One capacity case failed once under load and passed in both reruns. [Patched suites](../../../folders-story-3-6-review-patches.log), [CI-mode baseline and focused runs](../../../folders-story-3-6-review-patches-ci.log).
- `20b7a0a82d1056030088e7e94cadcc2d416d4f73` was pushed to Folders `main` with maintainer approval, and CI run 45720728611 passed. The maintainer widened the NuGet.org `folders-production` policy to `Hexalith.Folders*`. Release run 45723655711 failed before NuGet login, so nothing was published and no tag was created. A 2026-11-20 Dependabot bump had moved the `prepare-domain-release` action to Builds v6.1.0 (`a1e0c310b1f1a50d78e6132890fae6bcba769f6d`) but left `builds-execution-sha`, `HEXALITH_BUILDS_EXECUTION_SHA` and the preflight pin at v4.48.2. Fix commit `20f5cf01cdc5f27a46f3d4984d066d6ace5000ea` aligns them, and the CI tooling suite passed 76 tests in a disposable clone ([log](../../../folders-story-3-6-release-identity-fix.log)). It was pushed with approval, then rewound three minutes later. At 2026-11-26 01:30:51 +01:00, the standalone clone `~/projects/hexalith/folders` force-pushed its stale `main` (20b7a0a8) over it. On 2026-11-26 the maintainer approved the recommended recovery: that clone was fast-forwarded to `20f5cf01cdc5f27a46f3d4984d066d6ace5000ea` and the commit was pushed from it, so the clone's tracking state now matches `main`. CI run 45774604639 first failed one race test, `OperationalStatusPersistenceTests.IndependentWritersForTheSameDeliveryConvergeWithoutDoubleAdvance`, where both concurrent writers returned `operational_store_unavailable`. The commit changes no .NET code, and the rerun of the failed job passed. Release run 45780260331 then published v2.1.0, tagged at that commit. NuGet.org lists 2.1.0 for all six packages, including `Hexalith.Folders.EventStoreExtensions`. [Successful CI](https://github.com/Hexalith/Hexalith.Folders/actions/runs/45774604639), [release](https://github.com/Hexalith/Hexalith.Folders/actions/runs/45780260331).

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| Blind 1. New package ID can leave a partial publication | Medium | Patch (group A) | The registered policy still allows only new versions of existing packages, and the publish gate pushes in inventory order (`run-release-package-gates.ps1:274`). Contracts would be accepted before the new ID got a 403, the same failure as `v1.1.0`. The new ID now publishes first, and the docs say exactly how to change the NuGet.org policy. |
| Blind 2. Nothing compares the policy package list with the inventory | Low | Reject | The 5-package policy file was never checked against the inventory before this change either. It is a manual registration record, and a check would add a new gate for a process-only gap. |
| Blind 3. Release docs lost the "no wildcard" rule, and the authorization step is vague | Low | Patch (group A) | The diff dropped "; no wildcard" and left a bare link as a sentence. "Authorize that exact ID … in the protected workflow" names no concrete NuGet.org change. |
| Blind 4. The `Hexalith.Folders` package reads Contracts internals | Low | Reject | No project in the workspace outside Folders references the `Hexalith.Folders` package, and both packages release at the same version. A mismatch needs a deliberate split pin. The fix would add public surface or exact version pins. |
| Blind 5. No package validation baseline for the moved types | Low | Patch (group C) | Same root cause as Gap 3: deleting a forwarder still builds and passes. |
| Blind 6. Namespaces no longer match assemblies | False | Reject | The frozen constraint "Moved types keep their namespaces and names" requires this, and the payload file already explains the handler identity. The finding names no harm. |
| Blind 7. Handler identity literals have no drift check | Low | Patch (group B) | `FoldersRuntimeCatalogDeclarations.cs:29` replaced `typeof(FolderDomainProjectionHandler).FullName` with a literal. No test compares the two. |
| Blind 8. Contracts closure test weakened | False | Reject | `PackedContractsClosureTests` still asserts the exact dependency IDs and the restored library set. That set is what proves the package is provider-free, and a version pin adds nothing to it. |
| Blind 9. Memories publisher falls back to `pubsub` | Low | Reject | The fallback keeps Folders' own AppHost behaviour unchanged, and Platform binds every declared `configurationKey`. A guard would add branches for a binding miss that nothing shows can happen. |
| Blind 10a. `memories-events` is ambiguous between two `events` bindings | False | Reject | Platform resolves a component per role (`PlatformDaprBindings.cs:94`, `("pubsub", "events") => "pubsub"`), so both `events` bindings name the same component. |
| Blind 10b. Worker `tenants.events`/`folders.events` subscriptions and dead letters are not in the manifest | Medium | Defer | Builds 6.7.0's v2 schema cannot express their dotted dead-letter topic names. The manifest's `classification.reason` records this. The Platform composition slice must keep these subscriptions. |
| Blind 11. The manifest is never validated in CI | Low | Reject | Builds 6.7.0 validated it locally. Platform validates the manifest when it adopts the package. The maintainer has declined new CI proof gates without a planning requirement. |
| Blind 12. Production wiring for the extension is missing | Medium | Defer | Folders' production fragment never said which image the `hexalith-eventstore` deployment uses (`service-images.yaml` binds only server, workers and ui). The gateway that carries the package is Platform's composed host. |
| Blind 13. Dev host skips the loader, and registration is not idempotent | False | Reject | The spec requires one registration per host, and `AddFoldersIdempotencyIntentAdapters` has one call site. `FoldersEventStoreExtensionTests` covers the loader path. |
| Blind 14. Nothing proves the packed nupkg loads | False | Reject | A package-only EventStore 5.5.1 host loaded all 18 adapters and reported healthy ([log](../../../folders-story-3-6-package-only-host.log)). The wrong-major test is a spec matrix row. |
| Blind 15. Payload comment is wrong, and the compile constant is unexplained | Low | Patch | "Independently from Contracts" misdescribes the file. The csproj constant had no explanation. |
| Blind 16. Evidence YAML provenance not updated | False | Reject | `generate-pd10-v2-conformance-set.py` writes `generated_for_date` and the prior-set fields itself, and `GeneratorReproducesTheCommittedManifestByteForByte` proves the output. |
| Blind 17. Package count hard-coded in seven places | Low | Reject | The count of five was hard-coded in the same seven places before this change. Moving it to one place is a refactor with no defect behind it. |
| Edge 1. Partial publication of the new ID | Medium | Patch (group A) | Same root cause as Blind 1. |
| Edge 2. Preflight message says five | Low | Patch | `validate-publication-preflight.sh:37` still said "five" while the gate requires six. |
| Edge 3. Domain tenant subscription hard-codes `pubsub` | False | Reject | Platform names the `events` role's component `pubsub` (`PlatformDaprBindings.cs:94`), which matches `FoldersTenantEventSubscription.PubSubName`. |
| Edge 4. Memories key unset or padded | Low | Reject | Same reasoning as Blind 9. |
| Edge 5. Legacy projection literal can drift from its handler | Low | Patch (group B) | Same root cause as Blind 7. |
| Edge 6. Server still link-compiles the payload file | False | Reject | One source file compiles in both assemblies (`FOLDERS_CONTRACT_PAYLOADS`), and `ContractsExportTheActualInstalledCommandPayloadSchemas` compares the digests. The spec's "linked files" were the adapter files, which now come from the package. |
| Gap 1. Configured Memories component checked only on upsert | Low | Patch | The remove and soft-delete tests run with the default name. The configured-name test is now a theory that covers index, remove and soft-delete. |
| Gap 2. Manifest pubsub keys not tied to the keys the workers read | Low | Patch | Only `runtime.eventStoreExtensions` was read from the manifest. A new test feeds the manifest's workers keys into the real readers. |
| Gap 3. Type forwarders unchecked | Low | Patch (group C) | `ContractTypeForwardingTests` compares the forwarded set with the 17 exported contract types. |
| Gap other 1. Manifest never validated | Low | Reject | Same as Blind 11. |
| Gap other 2. Legacy projection handler literal | Low | Patch (group B) | Same root cause as Blind 7. |
