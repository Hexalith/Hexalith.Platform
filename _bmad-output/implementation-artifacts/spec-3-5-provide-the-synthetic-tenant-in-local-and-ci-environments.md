---
title: '3.5 slice 1: Tenants synthetic tenant creation task'
type: 'feature'
created: '2026-11-25'
status: 'in-progress'
baseline_commit: 'f6e58f9c760bb2ad302b21e6e67add7db0489bb0'
route: 'dispatch'
story_key: '3-5-provide-the-synthetic-tenant-in-local-and-ci-environments'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.5 depends on Tenants' synthetic tenant aggregate and its idempotent creation task, and Tenants has neither (v8.0.4, main). `CreateTenant` rejects a rerun, nothing marks a tenant synthetic, and the global tenant list shows every tenant.

**Approach:** Add an idempotent, admin-only `CreateSyntheticTenant` command to Tenants. It sets an immutable synthetic marker at creation. Declare it as a `once-per-environment-creation` lifecycle task with a runnable executable, and exclude marked tenants from the global-admin tenant list. Then release Tenants and pin that release in Platform.

**Decisions (2026-11-25):**
- Split Story 3.5. This spec is the Tenants slice. The Builds task-execution slice, the EventStore claim slice and the Platform wiring slice follow, and each is recorded in deferred-work.
- Synthetic tokens will carry an EventStore-owned `eventstore:synthetic` claim, delivered in the EventStore slice and not here.
- Pushing Tenants main triggers its release, so it needs the maintainer's yes.

## Boundaries & Constraints

**Always:**
- Keep the existing behaviour of `CreateTenant`, `TenantIdentity.DefaultTenantId = "system"`, the `actor:globalAdmin` check and the existing routes.
- The marker is set only by `CreateSyntheticTenant` and never changes.
- Legacy `TenantCreated` events replay unchanged, including audit-replay certification.
- The task never logs tokens or headers. A rerun exits 0 without a new event.
- Platform's `Declarations/tenants.json` stays byte-identical to the released manifest.

**Never:**
- Builds runner changes, EventStore claims, realm synthetic clients or Platform descriptor wiring (later slices).
- Exclusion in member views. A synthetic member still sees its own tenant.
- An agent-callable or REST route for the new command.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected | Error Handling |
|---|---|---|---|
| Create | Global admin, canonical new id | `TenantCreated` with `IsSynthetic=true`, tenant active | — |
| Rerun | Same id, already synthetic | `DomainResult.NoOp()`, task exits 0 | — |
| Collision | Id exists as a real tenant | `TenantAlreadyExistsRejection` | Task exits 3, naming the tenant and the rejection |
| Not admin | No verified `actor:globalAdmin` | `GlobalAdminRequired` rejection | Task exits 3 |
| Invalid id | Fails `TenantCanonicalizer`, or `system` | `SyntheticTenantIdInvalidRejection` | The task exits 2 and sends no request |
| Global list | Global admin calls `ListTenants` | Synthetic tenants absent | — |
| Legacy event | `TenantCreated` with no flag | `IsSynthetic` null, replay certified | — |

</frozen-after-approval>

## Code Map

All Tenants paths are relative to `references/Hexalith.Tenants`. Work in a worktree branch off `origin/main` (54985ab1), not in the detached v8.0.4 checkout.

- `src/Hexalith.Tenants.Contracts/Commands/CreateTenant.cs` and `BootstrapGlobalAdmin.cs`: command shapes. Copy the bootstrap one, which has no `[RestRoute]` and no `[HexalithCommand]`.
- `src/Hexalith.Tenants.Server/Aggregates/TenantAggregate.cs`: handler examples at 29-38, `IsGlobalAdmin` at 324-326, `DomainResult.NoOp()` at 171. `TenantState.cs:17-24` is the Apply.
- `src/Hexalith.Tenants.Contracts/Events/TenantCreated.cs`: a positional record.
- `TenantProjectionHandler.cs:375-386` skips only nullable properties. That is why the flag is `bool?`.
- Index:
  - `TenantIndexEntry.cs:5`
  - `TenantIndexReadModel.cs:18-26`
  - `TenantIndexProjectionVersion.cs:32-38`
  - The global-admin branch of `ListTenantsQueryHandler.cs:68-69`
- Catalog:
  - `TenantCatalogDeclarations.cs`: the `Commands` list (70-83) and the internal flag (30-31).
  - `TenantIntentAdapters.cs:16`
  - `tools/catalog-package-consumer/approved-route-ids.txt`
  - The counts in `TenantCatalogTests.cs:31,62-67` and the list in `TenantIntentDeclarationTests.cs:14-26`.
- `module/hexalith.module-manifest.v2.json:536`: `lifecycle.tasks`, whose shape comes from the Builds schema `$defs.task`. Non-REST commands are not listed in the manifest's `operations`.
- Request shape: `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`, with `SubmitCommandAsync` at 329 and `GetCommandStatusAsync` at 268.
- Platform adoption: compare Platform commit 207a7932. The files are:
  - the gitlink
  - `Declarations/tenants.json` and `provenance.json`
  - `eng/current-tuple-source.json`
  - `eng/module-intake.json:84-130`
  - `eng/owner-packages.lock.json:80-95`
  - `eng/timesheets-deployment/plan.json:160`, if its hash changes
- Platform checks: `PlatformServerBindings.cs:118-124` requires the executable under the owner root.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Tenants.Contracts/Commands/CreateSyntheticTenant.cs`, `Events/TenantCreated.cs`, `Events/Rejections/SyntheticTenantIdInvalidRejection.cs`: add the command (`create-synthetic-tenant`, domain `tenants`), add the trailing `bool? IsSynthetic = null`, and add the rejection.
- [x] `TenantAggregate.cs`, `TenantState.cs`: the handler, in the matrix order (admin, then id, then state). `IsSynthetic` is applied from the event.
- [x] `TenantIndexEntry.cs`, `TenantIndexReadModel.cs`, `TenantIndexProjectionVersion.cs`, `ListTenantsQueryHandler.cs`: project the flag and exclude it in the global-admin branch. Hash the flag only when it is true, so existing versions stay stable.
- [x] Catalog files and tests: register the route as internal with permission `create`, then update the approved ids and counts.
- [x] `module/tasks/create-synthetic-tenant.py` (Python 3 stdlib, `#!/usr/bin/env python3`, mode 100755): read the JSON file named by `HEXALITH_TASK_INVOCATION_PATH` (`syntheticTenantId`, `gatewayUrl`, `commandTenant`, `headers`). Submit the command, poll its status for up to 120 s, and exit 0/1/2/3 as the matrix says. Document the contract in `docs/`.
- [x] `module/hexalith.module-manifest.v2.json`: add the task `{create-synthetic-tenant, once-per-environment-creation, operator, module/tasks/create-synthetic-tenant.py, []}`.
- [x] Tests:
  - `TenantAggregateTests`: every aggregate row, plus `CreateTenant` on a synthetic id.
  - Index and query tests: exclusion and the member branch.
  - The legacy-event replay test.
  - `tests/scripts/test_create_synthetic_tenant.py`: a fake gateway on `http.server`. Cover the request shape, exit codes 0/2/3/1, a timeout, and that no secret appears in the output.
- [ ] After the maintainer's push yes and the Tenants release: Platform pin, with the files in the Code Map.

**Acceptance Criteria:**
- Given the release is pinned in Platform, when `eng/test_current_tuple_source.py` and the Platform composition declaration tests run, then they pass with the task carried in the Tenants workload and its executable resolved.

## Design Notes

The authority is admin-only, because creation is population-creating and marked operator-only in hosted environments. The future synthetic clients never hold it. The invocation file keeps the runner's catalog-binding headers and the bearer token out of argv and out of the Tenants code. The Builds slice writes that file.

## Verification

**Commands:**
- `dotnet build tests/<P>.csproj -c Release -warnaserror -m:1 -nr:false -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true`, then `dotnet test tests/<P>.csproj --no-build -c Release` for Server, Contracts and Testing. Expected: all pass.
- `python3 -m unittest discover -s tests/scripts`. Expected: all pass.
- Platform: `python3 -m unittest eng/test_current_tuple_source.py`. Expected: passes after the pin.

## Implementation Notes

- The implementation is in the Tenants `feat/synthetic-tenant-task` worktree at baseline `54985ab11ade150d5fa908be179cf2a5a407353d`. The [patch](../../../worktree-backups/tenants-synthetic-tenant-task.diff) includes the executable mode and new files. The name of a synthetic tenant defaults to its identifier.
- Strict Release builds in pinned NuGet package mode reported zero warnings or errors. All 1,928 [Server](../../../tenants-synthetic-tenant-task/.synthetic-tenant-final-Server-test.log), 813 [Contracts](../../../tenants-synthetic-tenant-task/.synthetic-tenant-final-Contracts-test.log), and 199 [Testing](../../../tenants-synthetic-tenant-task/.synthetic-tenant-final-Testing-test.log) tests passed. The [Python suite](../../../tenants-synthetic-tenant-task/.synthetic-tenant-python.log) passed all 173 tests, including eight fake-gateway cases for the request shape, no-op completion, rejection diagnostics, timeout, and credential handling. The owner manifest validated against the pinned Builds v2 schema.
- The task's gateway contract was exercised with a fake server. EventStore schema 3 admits internal commands through authenticated workload transport. A direct bound request requires the exact command message ID and operator principal in the signed body, which the four-field invocation does not provide. The existing `PlatformNativeTestPreparation.SubmitBootstrapAsync` path demonstrates the operator transport the runner needs for the live task. See the [task contract](../../../tenants-synthetic-tenant-task/docs/synthetic-tenant-task.md).
- Tenants main has advanced past the spec baseline. A release requires integrating and verifying the change on the current Tenants main. Maintainer approval, the Tenants release, the Platform pin, and Platform acceptance are pending.

## Spec Change Log

## Review Triage Log
