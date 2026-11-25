---
title: '3.5 slice 1: Tenants synthetic tenant creation task'
type: 'feature'
created: '2026-11-25'
status: 'done'
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
  - the [Builds tuple and local tool pins](../../eng/tuple-source-preparation.md#adopting-a-builds-release) for the signed catalog that carries the new Tenants version
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
- [x] After the maintainer's push yes and the Tenants release: Platform pin, with the files in the Code Map.

**Acceptance Criteria:**
- Given the release is pinned in Platform, when `eng/test_current_tuple_source.py` and the Platform composition declaration tests run, then they pass with the task carried in the Tenants workload and its executable resolved.

### Review Findings

Code review on 2026-11-25 of Tenants PR #80 (`d58f24d8..583ea9e9`), Tenants `5a0441f0` and Platform `a4984b9d`. Blind Hunter, Edge Case Hunter, Verification Gap and Acceptance Auditor reviewed it. Tenants paths are relative to `references/Hexalith.Tenants`.

The patches were applied on 2026-11-25. The Tenants changes are local commit `e47047da` on `fix/synthetic-tenant-review-3-5`, based on `origin/main` `d34f353a`, in the worktree `/home/administrator/projects/hexalith/tenants-review-3-5`. It is in [Tenants PR #81](https://github.com/Hexalith/Hexalith.Tenants/pull/81), and it changes no released task or manifest bytes. Platform stays on v8.1.0. The task suite passed 11 of 11. Narrowing `{409, 422}` to `{409}` fails the new exit-2 case. A strict Release build in pinned NuGet mode reported zero warnings, and the Server suite passed 1,937 of 1,937. The Builds-slice hand-off in `deferred-work.md` now records the relay.

- [x] [Review][Patch] Run the task's fake-gateway suite in Tenants CI [references/Hexalith.Tenants/.github/workflows/ci.yml:96]. Only `scripts/run-product-unit-checks.sh` registers `tests/scripts/test_create_synthetic_tenant.py`, and only the `after-unit-tests` hook calls that script. Tenants CI delegates to the Builds `domain-ci.yml`, which never calls the hook. No CI job runs the eight cases.
- [x] [Review][Patch] Cover the task's refusal branches [references/Hexalith.Tenants/tests/scripts/test_create_synthetic_tenant.py:117]. No case sends a gateway 422 or a `Rejected` status carrying `SyntheticTenantIdInvalidRejection`, which should exit 2. No case sends a submission 401, refused `gatewayUrl` values, a missing `HEXALITH_TASK_INVOCATION_PATH`, an oversized body or a retryable terminal status. Narrowing `{409, 422}` to `{409}` still passes every test.
- [x] [Review][Patch] Record the operator relay in the Builds-slice hand-off [_bmad-output/implementation-artifacts/deferred-work.md:2706]. The entry only says to write the invocation file "with an operator authority". EventStore 5.6.0 admits the internal command only from a service-surface workload. The runner must relay it through `POST /api/v1/commands/bound/workload` with a signed command proof, and poll status with a separately scoped `tenants:read` JWT, as `docs/synthetic-tenant-task.md` states.
- [x] [Review][Patch] Assert that the synthetic marker survives index serialization [references/Hexalith.Tenants/src/Hexalith.Tenants.Server/Projections/TenantIndexEntry.cs:14]. The list-exclusion tests seed in-memory index objects. No test stores a synthetic `TenantCreated` projection, so an `IsSynthetic` that stops serializing would pass every test.
- [x] [Review][Patch] Document `CreateSyntheticTenant` in the event contract reference [references/Hexalith.Tenants/docs/event-contract-reference.md:300]. Only the inventory rows were added. Every other command also has a `####` section, plus table-of-contents, quick-reference and idempotency entries.
- [x] [Review][Patch] Restate the historical-archive refusal for 24-route archives [references/Hexalith.Tenants/docs/development-route-catalog.md:132]. The sentence about older 23-route archives was deleted, not updated. The document no longer says that 8.0.x 24-route archives do not qualify against the 25-route inventory.
- [x] [Review][Patch] Keep the task executable LF on every checkout [references/Hexalith.Tenants/.gitattributes:2]. `*.py` falls under `* text=auto`. A Windows `core.autocrlf` checkout gets a CRLF shebang and fails Platform's byte-parity check in `PackageModeResolvesReleasedSyntheticTenantExecutable`.
- [x] [Review][Defer] `CreateTenant` refuses an ID whose stream holds only rejections [references/Hexalith.Tenants/src/Hexalith.Tenants.Server/Aggregates/TenantAggregate.cs:36]. Deferred because it is pre-existing. `CreateTenant` tests `state is not null`, so after any rejected command on a never-created stream, an admin `CreateTenant` returns `TenantAlreadyExistsRejection`. A non-admin `CreateSyntheticTenant` attempt is one such command. The new handler tests `TenantId.Length > 0`.
- [x] [Review][Defer] Builds validates task executables against the Platform repository root [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/ManifestPathValidator.cs:23]. Deferred to the Builds owner. Composition resolves the executable under `Declarations/`, but validation walks up to the Platform `.git` root. That forced the root symlink `module/tasks/create-synthetic-tenant.py`. Each further owner task needs its own root alias, and two owners that declare the same relative task path cannot both be satisfied.
- [x] [Review][Defer] A non-string `rejectionType` exits 1 with no diagnostic [references/Hexalith.Tenants/module/tasks/create-synthetic-tenant.py:84]. Deferred because the fix changes the released task bytes. It belongs with the owner's task-hardening release.
- [x] [Review][Defer] A status 401 or 403 exits 1 instead of 3 [references/Hexalith.Tenants/module/tasks/create-synthetic-tenant.py:146]. Already recorded: "Harden Tenants task status polling and invocation URL validation".
- [x] [Review][Defer] Empty `?` or `#` delimiters pass URL validation [references/Hexalith.Tenants/module/tasks/create-synthetic-tenant.py:55]. Already recorded in the same entry.
- [x] [Review][Defer] No test replays a persisted `SyntheticTenantIdInvalidRejection` [references/Hexalith.Tenants/src/Hexalith.Tenants.Server/Aggregates/TenantState.cs:106]. Already recorded: "Exercise audit and singleton-index replay for the new pre-creation synthetic rejection".
- [x] [Review][Defer] The intake has no entry for the adopted EventStore 5.6.0 cohort [eng/module-intake.json]. Already recorded: "Reconcile EventStore and Memories intake selections".

**Rejected**

- false: the direct-gateway documentation misleads (Blind). The document points `gatewayUrl` at the Builds relay for a schema-3 gateway.
- false: one `headers` object cannot carry two credentials (Blind). The contract assigns the separately scoped status credential to the relay.
- false: status polls lack `X-Hexalith-Scope` (Edge). The relay adds the scope header when it polls EventStore.
- false: a global admin who is also a member cannot list the synthetic tenant (Blind, Edge and Acceptance, three findings). The frozen matrix requires synthetic tenants to be absent from a global admin's `ListTenants`. Member views, the non-admin branch and `get-user-tenants`, are unchanged.
- false: the marker does not reach `TenantSummary` or the client projections (Blind). No consumer in this slice or the deferred slices reads it there. Tokens will carry `eventstore:synthetic`.
- false: a rerun on a disabled synthetic tenant is a NoOp (Blind and Edge, two findings). The frozen matrix specifies a NoOp for an ID that is already synthetic.
- false: the executable copy has no hash binding (Blind). `PackageModeResolvesReleasedSyntheticTenantExecutable` compares bytes and mode with the gitlinked owner checkout, and that checkout is on the v8.1.0 tag.
- false: the fixtures hide a missing executable, copy from mixed sources and couple to a count (Blind and Edge, two findings). Builds validation fails and names a missing task path, parity is asserted, and a change in closure size fails visibly.
- false: the required suite profiles omit the new selectors (Acceptance). Both selectors run in required tuple CI (`.github/workflows/tuple-ci.yml:177-179`). The JSON profiles are fixed-total SDK 4.2 and extension lanes.
- low: the internal route is labelled `agent-eligible` (Acceptance). Internal ingress already bars agents, as it does for `BootstrapGlobalAdmin`. Relabelling changes catalog digests and needs a release.
- low: the spec says `done` while sprint status says `review` (Acceptance). This review sets both.
- low: a submission 409, 429 or 503 without a rejection type aborts (Edge). This is unlikely for one environment-creation command, and a retry loop adds branches. Polling 429 is already deferred.
- low: runner headers may include `Content-Length`, `Host` or case-duplicate names (Edge). The Builds runner writes these headers, and a guard adds complexity.
- low: SIGALRM can fire at the 120-second boundary (Edge). The window is microseconds, and a fix needs restructured alarm handling.
- low: a downgraded replica drops `IsSynthetic` (Edge). Mixed-version local and CI runs are unlikely, and schema versioning is a large change.

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
- The task's gateway contract was exercised with a fake server. The Builds runner needs an operator relay to sign the exact command message and operator principal for EventStore's bound workload submission. It must poll the normal status endpoint with a separately scoped operator JWT. The workload-only status URL returned by the bound submission requires a canonical public tenant and refuses `platform:tenants`. The existing bootstrap flow demonstrates the two transports. [Task contract](../../../tenants-synthetic-tenant-current-main/docs/synthetic-tenant-task.md), [bootstrap transport](../../src/Hexalith.Platform.Composition/PlatformNativeTestPreparation.cs:149)
- The [current-main worktree](../../../tenants-synthetic-tenant-current-main) applies the feature on `feat/synthetic-tenant-task-current-main` at local Tenants `origin/main` revision `d58f24d8d6909d7498ecec34955e7f17720c1ba0`. The [updated patch](../../../worktree-backups/tenants-synthetic-tenant-task-current-main.diff) preserves the SDK pin added upstream for the packed catalog consumer. The existing Tenants CI product check now runs the [eight fake-gateway cases](../../../tenants-synthetic-tenant-current-main/.synthetic-tenant-current-main-task-hook.log).
- Current-main strict Release builds in pinned NuGet package mode reported zero warnings or errors. The [Server](../../../tenants-synthetic-tenant-current-main/.synthetic-tenant-current-main-Server-pinned-source-test.log), [Contracts](../../../tenants-synthetic-tenant-current-main/.synthetic-tenant-current-main-Contracts-test.log), and [Testing](../../../tenants-synthetic-tenant-current-main/.synthetic-tenant-current-main-Testing-test.log) suites passed 1,936, 813, and 199 tests, respectively. The manifest validated against the exact Builds revision pinned by Tenants, and the task file is executable.
- Current-main Python discovery passed 173 of 174 tests. The existing NuGet v2 recovery fixture `test_official_v2_packages_are_existing_and_never_pushed_again` exceeded its readiness budget for `Fixture.Package4` during release verification. The fixture passed on the same candidate in a focused rerun. [Full Python log](../../../tenants-synthetic-tenant-current-main/.synthetic-tenant-current-main-python.log), [focused rerun](../../../tenants-synthetic-tenant-current-main/.synthetic-tenant-current-main-release-fixture-rerun.log)
- The [Tenants release workflow](../../../tenants-synthetic-tenant-current-main/.github/workflows/release.yml) now requires a manual dispatch from the current main tip after push CI passes for that exact revision and approval in the protected production environment. Platform's central package version comes from the signed Builds catalog, currently Builds 6.7.0 with Tenants 8.0.4. Adopting the new Tenants release also requires a signed Builds catalog release carrying its version. The release, the Platform pin, and Platform acceptance are pending.
- At adoption, update the [dependency-closure test](../../tests/Hexalith.Platform.Composition.Tests/PlatformAppModelTests.cs:98) to expect the task on both Tenants workload bindings. Package mappings without an owner path resolve the executable relative to the released declaration snapshot. The package-mode check must prove that the verified released executable is available there and that the declaration matches the release manifest byte for byte. [Executable resolution](../../references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDeclarationClosure.cs:48)
- Before the pin, `run-with-temp env PYTHONDONTWRITEBYTECODE=1 PYTHONPATH=eng python3 -m unittest eng/test_current_tuple_source.py` passed all 16 tests against the existing Platform tuple. A disposable .NET 11 probe built with zero warnings or errors and called `PlatformAppModel.Prepare` with the unreleased Tenants worktree as its mapped owner. Both `tenants` and `tenants-api` carried the exact task and resolved its executable under the candidate owner root. Acceptance against the published package mapping remains pending.
- The task contract now specifies the future Builds relay's submission and status permissions while preserving the four-field invocation. The updated Tenants patch includes this documentation and the executable's `100755` mode. [Task contract](../../../tenants-synthetic-tenant-current-main/docs/synthetic-tenant-task.md), [candidate patch](../../../worktree-backups/tenants-synthetic-tenant-task-current-main.diff)
- Fresh pinned checks passed 1,936 Server, 123 focused Contracts, 199 Testing, and eight Python task tests. The Server run covered creation, replay, collision, authorization, invalid IDs, global-list exclusion, member visibility, and legacy audit replay. [Server](../../../tenants-synthetic-tenant-current-main/.synthetic-tenant-audit-Server-test.log), [Contracts](../../../tenants-synthetic-tenant-current-main/.synthetic-tenant-audit-focused-Contracts-test.log), [Testing](../../../tenants-synthetic-tenant-current-main/.synthetic-tenant-audit-focused-Testing-test.log), [Python](../../../tenants-synthetic-tenant-current-main/.synthetic-tenant-audit-task.log)
- The package-mode fixture used the exact candidate manifest and executable with synthetic release provenance. Builds validation required `module/tasks/create-synthetic-tenant.py` beneath the Platform root even when the executable was present under `Declarations/`. A relative symlink from the Platform-root path to the executable under `Declarations/` satisfied both checks on Linux. The `tenants` and `tenants-api` workloads then each resolved the same executable with mode `100755`. Use this evidence to prepare the layout and fixture at adoption. Match both the manifest and script against the published Tenants Git tag. [Probe log](../../artifacts/story-3-5-package-task-probe-final.log)
- The maintainer approved committing the candidate, pushing the feature branch, and opening a PR. Tenants commit `583ea9e9354b57a1995cb0abffb3b08d99d1484e` is on `feat/synthetic-tenant-task-current-main` and targets `main` in [Tenants PR #80](https://github.com/Hexalith/Hexalith.Tenants/pull/80). The commit matches the verified candidate patch. [Commitlint result](../../artifacts/story-3-5-tenants-commitlint.log)
- Tenants PR #80 merged into `main` at `067ac3fec62b0a607bb512bd75cb1ca0fd780ae7`. The merge tree matches the verified feature tree. Release preflight requires successful push runs for that exact source. [CI](https://github.com/Hexalith/Hexalith.Tenants/actions/runs/45580392829), [Story Guards](https://github.com/Hexalith/Hexalith.Tenants/actions/runs/45580392249), [Source-reference integration](https://github.com/Hexalith/Hexalith.Tenants/actions/runs/45580392317)
- Published Builds `6.8.2` still sets `HexalithTenantsVersion` to `8.0.4`. Platform adoption needs the Tenants task release and a signed Builds catalog that carries it. [Builds package catalog](https://github.com/Hexalith/Hexalith.Builds/blob/v6.8.2/Props/Directory.Packages.props#L35)
- The release preflight requires all three push runs on the exact current `main` source. CI and Source-reference integration had passing jobs for `067ac3fec62b0a607bb512bd75cb1ca0fd780ae7`. All three required workflows, including Story Guards, still had queued jobs, and no release run existed for that source. The maintainer authorized proceeding without a separate human approval step. The connected GitHub account is the configured production reviewer and can use GitHub's per-run deployment review when the exact-source release reaches that gate. [Tenants release workflow](https://github.com/Hexalith/Hexalith.Tenants/blob/067ac3fec62b0a607bb512bd75cb1ca0fd780ae7/.github/workflows/release.yml), [latest Tenants release](https://github.com/Hexalith/Hexalith.Tenants/releases/tag/v8.0.4)
- The exact merge commit passed all 1,936 Server tests in a disposable checkout with its pinned Builds catalog and EventStore test inputs. The first checkout omitted the EventStore files required by seven existing documentation and configuration tests. The rerun included those pinned files and passed. The merged tree matches the candidate tree whose full Contracts and Testing suites passed above. A duplicate full Contracts run was stopped while release prerequisites remained pending. The eight task cases and 16 Platform baseline tuple unit tests also passed. [Merge verification](../../artifacts/story-3-5-merged-tenants-pinned-verification.log), [Platform baseline](../../artifacts/story-3-5-platform-baseline-tuple-tests.log)
- Tenants published `8.1.0` at `e4e28904dfb41ed19dd2885cc36b881aef167af2`. The tagged manifest declares `create-synthetic-tenant`, and the script is executable. All eight task tests and 1,936 Server tests passed from the tag. The adoption still requires a signed Builds catalog release with the Tenants 8.1.0 declaration identity. [Tenants release](https://github.com/Hexalith/Hexalith.Tenants/releases/tag/v8.1.0), [Server verification](../../artifacts/story-3-5-tenants-release-server-test.log)
- The initial Platform update selected Works source `386032af4450e838ce529aab64c9de3614399bce` and EventStore source `295b5a6ba584167f21cfef40787477b8814e32da`. At that point, Builds `6.8.3` at `62876129eaa035e78bd0324419231f07d43a07b8` was the latest signed release and selected EventStore `5.5.2`. Builds `main` at `eb81d1e00267b6b816c5fbd48e2c618965d6ec39` selected EventStore `5.6.0`, Commons `3.0.4`, and Tenants `8.0.4`. In Platform, 107 dependency and Works verification checks and four baseline composition closure cases passed. [Composition log](../../artifacts/story-3-5-latest-source-baseline-composition.log)
- Restoring against a candidate catalog with EventStore `5.6.0` and Tenants `8.1.0` required Commons `3.0.4` to satisfy the Tenants Contracts dependency on `Hexalith.Commons.UniqueIds`. With that update, the four Platform owner package graphs restored, and all 18 owner archives passed repository signature and NuGet registration hash checks. The candidate catalog exists only in the disposable verification job. [Candidate package verification](../../artifacts/story-3-5-candidate-package-closure.log)
- The Platform adoption patch copies the exact released Tenants manifest and executable. Twelve disposable composition tests passed, including task resolution for both Tenants workloads in source and package mode. The append-only intake patch verified all five NuGet packages and both image indexes. Both patches apply cleanly. Platform adoption and acceptance remain pending until the signed Builds release carries the Tenants declaration identity. [Adoption patch](../../../worktree-backups/story-3-5-platform-tenants-adoption.diff), [composition log](../../artifacts/story-3-5-platform-tenants-adoption-check.log), [intake patch](../../../worktree-backups/story-3-5-platform-tenants-intake.diff), [intake log](../../artifacts/story-3-5-platform-tenants-intake-check.log)
- After maintainer authorization, the Builds package workflow opened PR #208 for Tenants `8.1.0` and its release declaration. It merged at `a6abb7908ec38dcb3fbd99d69e7ed29ae25794d2`. CI passed for that exact source, and the release workflow published it as `6.8.4`. Platform pins that release, EventStore `5.6.0`, Tenants `8.1.0`, and Works source `7e691d4b95ab808047fc79f776d499bb7a5ef2d5`. The catalog selects Commons `3.0.4` and Works Contracts `1.0.2`. [Builds PR](https://github.com/Hexalith/Hexalith.Builds/pull/208), [merge CI](https://github.com/Hexalith/Hexalith.Builds/actions/runs/45627494323), [Builds release](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v6.8.4)
- In a disposable checkout of the Platform candidate, the exact signed Builds CLI, all 18 owner package entries, and the release declarations verified. All 132 Python checks and 19 composition cases passed. Both Tenants workload bindings resolve the byte-identical released executable in source and package mode. The tests used the published Builds `6.8.4` assemblies and the pinned public NuGet packages. The Timesheets plan retains its historical preparation inputs. [Published tuple verification](../../artifacts/story-3-5-final-published-tuple.log)
- Required tuple CI now selects the task parity and source closure tests. The existing isolated package fixture proves both Tenants workloads resolve the executable from its declaration snapshots with no Tenants checkout in the fixture. All six affected cases passed after these review changes. The final Works gitlink selects `92d4fe55663848a939cf9093e068f1cfea12a01c`, and Works Contracts remains at `1.0.2`. [Review change verification](../../artifacts/story-3-5-platform-adoption-review-fixes.log)
- The reviewed candidate passed all 166 Python checks and 30 composition cases, with zero skips. The composition run includes the updated required CI selectors and uses the published Builds `6.8.4` tool. It independently revalidated the source binding, package lock, release snapshots and append-only intake. [Reviewed candidate verification](../../artifacts/story-3-5-final-reviewed-tuple.log)

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| Blind 1. Submission correlation | Medium | Defer | v8.1.0 accepts any nonempty response correlation. EventStore defaults it to the sent message ID and can assign a separate execution ID. The owner must make the change before Platform can adopt new bytes. |
| Blind 2. Ambient proxy | Medium | Defer | The standard opener installs the default proxy handler. A configured HTTP proxy can receive operator headers on an HTTP gateway request. |
| Blind 3. Executable parity and mode | Medium | Patch | The tuple checks authenticate JSON. The executable test compares the released body, mode and alias, so require that test in CI. |
| Blind 4. Missing CI test selectors | Medium | Patch | The three tuple suites and the supplemental composition invocation omitted both new task assertions. |
| Blind 5. Package test has a source checkout | Low | Patch | The release byte comparison needs the owner checkout. The existing isolated package fixture can additionally prove that both workloads resolve the task with no owner checkout at its Platform root. |
| Blind 6. Other intake selections | Medium | Defer | The intake already selected EventStore 5.4.0 and Memories 4.1.0 independently of the package catalog. This slice adopts only the Tenants intake entry. |
| Blind 7. Native fixture can pass with zero tests | Medium | Defer | The G-1 probe accepts the test process exit code. xUnit reports zero tests and exits 0 for an unmatched class even with `-failSkips`. This predates the adoption. |
| Blind 8. Clean-host G-1 command | Low | Defer | G-1 uses `--pull never` with digest-pinned images and its run instructions omit the image preparation step. |
| Blind 9. G-1 signal cleanup | Medium | Defer | The wrapper sends SIGTERM to the process group. The Python probe has no handler that unwinds the Docker cleanup block. |
| Blind 10. Verification log retention | Low | Reject | The detailed log is retained locally outside the disposable job. The required declarations now have source-controlled CI selectors. The finding asks for an edit to this spec's evidence record. |
| Edge 1. Response identifies another command | Medium | Defer | The gateway's acknowledgement and status can agree with each other without the task confirming the submission correlation. This is the same Tenants owner follow-up as Blind 1. |
| Edge 2. Status rate limiting | Low | Defer | After an accepted command, HTTP 429 is raised immediately. A future owner update can retry within the existing deadline. |
| Edge 3. Status authorization exit | Low | Defer | Status HTTP 401 or 403 currently exits 1. The owner contract documents exit 3 for gateway authorization refusal. |
| Edge 4. Empty URL delimiters | Low | Defer | Empty query and fragment values pass the `urlsplit` value checks, and the original delimiter affects the appended command path. |
| Edge 5. Proxy receives operator credentials | Medium | Defer | Redirect refusal is separate from ambient proxy discovery. This is the same Tenants owner follow-up as Blind 2. |
| Edge 6. Package executable verification in the runner | Medium | Defer | Current composition resolves a path and checks existence. CI now verifies the released body. Verification before operator task execution belongs to the deferred Builds runner slice. |
| Edge 7. G-1 cleanup error masks failure | Low | Defer | A raise in the probe's `finally` block replaces the original qualification failure. This predates the adoption. |
| Edge 8. Per-call McpCli authorization context | Medium | Defer | The earlier Epic 3 context rewrite removed the explicit identity, surface and permission check at every server call. The fix belongs in generated agent context. |
| Verification 1. CI omits exact task release verification | Medium | Patch | The required selectors omitted the sole executable byte and mode comparison. Add it and the source task closure theory. |
| Verification 2. Redirect test survives mutation | Medium | Defer | Removing the custom redirect handler still passes all eight owner tests because the 307 POST fixture is already refused by default Python behavior. |
| Verification 3. Invalid-ID rejection projection replay | Medium | Defer | Owner tests cover command rejection and older pre-creation events. They omit a replay beginning with `SyntheticTenantIdInvalidRejection`. |
| Verification 4. Invalid Works preview manifest CI | Medium | Defer | The existing Story 3.4 preview refusal test is absent from the required selectors. This predates the adoption. |
