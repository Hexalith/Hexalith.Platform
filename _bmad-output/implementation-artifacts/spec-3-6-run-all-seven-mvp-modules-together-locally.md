---
title: '3.6 slice 1: Folders joins a Platform composition'
type: 'feature'
created: '2026-11-25'
status: 'ready-for-dev'
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
- [ ] `src/Hexalith.Folders.Contracts` -- Move `FoldersCommandCatalog`, the organization ACL command model and the two declaration sources here, keeping namespaces and replacing any server-type reference with its literal name -- Gives Platform's catalog builder and the package one provider-free home.
- [ ] `src/Hexalith.Folders.EventStoreExtensions/` -- New packable, non-publishable library with the moved adapters, both assembly attributes and the `Create()` source -- This is the enrollable unit.
- [ ] `src/Hexalith.Folders.Server`, `src/Hexalith.Folders.EventStore`, `src/Hexalith.Folders.AppHost` -- Reference the package instead of linked files and keep one registration per host. Remove `EnableContainer` and `ContainerRepository` from the development host, and keep the AppHost resource -- Keeps Folders' own runs working.
- [ ] `module/hexalith.module-manifest.v2.json` -- Declare the `domain` and `workers` servers (app ids `folders` and `folders-workers`) with their Dapr roles, `/ready` readiness, dependencies and the `eventStoreExtensions` entry -- Lets Builds select Folders.
- [ ] `tools/release-packages.json`, `deploy/nuget/release-packages.yaml` -- Publish the new package -- Platform pins released packages.
- [ ] `tests/` -- Cover every matrix row and validate the manifest -- Proves the contract.

**Acceptance Criteria:**
- Given the released Folders version, when Platform reads its manifest and restores `Hexalith.Folders.EventStoreExtensions`, then both are available without a Folders source checkout.
- Given Folders' own AppHost, when it runs, then Folders commands keep their admission results.

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.slnx -c Debug -nodeReuse:false` -- expected: 0 warnings, 0 errors.
- Folders test projects for Contracts, EventStore, Server and AppHost, each invoked directly -- expected: all pass.
- `dotnet tool run hexalith-module -- validate --manifest module/hexalith.module-manifest.v2.json` (Builds 6.7.0) -- expected: valid.
- `python3 references/Hexalith.Builds/check.py` from a standalone clone before the push -- expected: success.

## Implementation Notes

## Spec Change Log

## Review Triage Log
