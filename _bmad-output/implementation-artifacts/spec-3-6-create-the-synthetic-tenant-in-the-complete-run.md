---
title: '3.6 slice 5: Create the synthetic tenant in the complete run'
type: 'feature'
created: '2026-11-28'
status: 'done'
route: 'dispatch'
baseline_commit: '1ae50cd1ce23016de5046adf81034e9ff0162d69'
story_key: '3-6-run-all-seven-mvp-modules-together-locally'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The complete selector stops at `--validate-platform` (`HXP020`) because Builds 6.10.0 never schedules Tenants' `once-per-environment-creation` task `create-synthetic-tenant`. The task's operator relay cannot live in the Builds runner. Under the generated realm the gateway accepts only realm-issued RS256 tokens, and the proof key, Dapr token and realm client secrets exist only in the AppHost. The writer-protocol cutover fails for the same reason, so EventStore never becomes healthy.

**Approach:** Builds schedules environment-creation tasks, records a private invocation path for each in the run plan and passes it as `HEXALITH_TASK_INVOCATION_PATH`. Builds also lets the model supply the cutover's operator token. Platform adds a local operator principal to the generated realm and uses it for the cutover. It also hosts a loopback relay in the AppHost. The relay bootstraps the operator as global administrator, then forwards the task's submit and status calls to EventStore's bound-workload and status endpoints. The composed gateway skips the erasure-root protection registration only in a runner-owned Development run, so the complete run can start. Native preparation creates the run tenant as a synthetic tenant, so the task is a no-op in Parties runs.

**Decisions (2026-11-28):**
- This slice covers the task scheduling, the realm-signed cutover and the AppHost relay together. The maintainer kept the full spec at about 2,700 tokens.
- Builds schedules every declared `once-per-environment-creation` task in every Platform composition. The relay uses realm operator tokens when the generated realm is active and runner-key tokens otherwise.
- Renegotiated after implementation (maintainer adopted all four recommendations): add a Development-only gateway protection exemption to this slice; let the creation token carry the global-administrator role; fix the Parties native preparation here; keep the Platform code on a protected candidate commit, not in the main checkout.

## Boundaries & Constraints

**Always:**
- Builds stays owner-agnostic, with no Tenants, realm or route knowledge. Runs without the generated realm keep the HS256 cutover. `per-start-verify` tasks behave as before.
- The invocation file lives in the private run workspace with owner-only permissions. It holds only a per-run relay credential, never a realm token or key, and is written before `readiness.json`. The relay listens only on loopback, accepts only that credential and serves only the task's two calls.
- The operator is a confidential client that exists only in the generated local realm instance, never in the shared realm contract. Each operator token carries exactly one grant (global administrator for the cutover, one permission for bootstrap or status), except the creation token, which carries `tenants:create` plus the global-administrator role that Tenants checks.
- Allow the bound `tenants` domain only for the commands the relay submits.
- The gateway skips `AddPlatformErasureRootProtection` only when the host environment is Development and `EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath` is set. Every other configuration still refuses with `EXT-PROTECTION-1`.
- Keep `PlatformDeclarationSet.ValidateCompleteRunner`, the preflight order, the descriptor's rule that every task passed, and the released Tenants task bytes unchanged.
- Implement Builds in an isolated worktree branch from `v6.10.0`, committed locally with its identity recorded. Live Platform runs use that candidate. Do not push, release or change anything hosted.
- Keep Platform code changes in the detached candidate worktree `../platform-story-3-6-slice-5`, protected by `refs/hexalith-candidates/story-3-6-slice-5`. Never put them in the main checkout or on a branch.

**Never:** Hand proof keys or realm secrets to the runner. Forward the task's credential upstream. Add the `eventstore:synthetic` claim (EventStore slice) or the McpCli profile (Story 3.7). Accept package mode, which needs released inputs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh complete run | Source mode, generated realm | Preflight passes. The cutover uses a realm operator token. The task exits 0, `startupTasks` reports it passed, and the created tenant is the descriptor's `syntheticTenantId`. | N/A |
| Run without the generated realm | Platform composition with Tenants | The HS256 cutover is unchanged. The task passes through the relay using runner-key tokens. | N/A |
| No invocation file | The model wrote none | The task exits 2. | `HXR021` names `tenants/create-synthetic-tenant`. Keep diagnostics and clean up by run ID. |
| Bad relay request | Wrong credential, or a path other than the two task calls | 401 or 404, and nothing is forwarded | N/A |
| Gateway refusal or rejection | 403, 409 or 422 | The relay returns the status and body unchanged, and the task exits 3. | `HXR021` names the task. |
| Operator token unavailable | Keycloak is not ready, or it refuses the client | Fails within the deadline and names the operator client. No HS256 fallback under the realm. | Named failure |

</frozen-after-approval>

## Code Map

Builds paths are relative to the Builds repository root.

- `src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionStartupTasks.cs`: the scope filter is at 78-80, and the task environment at 186-189 carries only `HEXALITH_G4_RUN_ID`. `CompositionRunPlan.cs:129` sends `StartupTasks` to `plan.json`, which is where the model learns about tasks.
- `CompositionEngine.cs`: `Select` is called at 407, only for Platform compositions. Tasks run at 1854, after the AppHost writes `readiness.json`. A failure reports `HXR021` at 1872, and results are written at 1942. `TestEnvironmentDescriptorWriter.cs:103-110,168` builds `startupTasks`. `plan.TenantNamespace` feeds `syntheticTenantId`.
- `src/hosts/Hexalith.Builds.Module.AppHost/AppHostRunner.cs:117-130` runs the cutover alongside startup, and EventStore becomes healthy only after the cutover. `WriterProtocolCutover.cs` passes the HS256 key to `CompositionWriterProtocolCutover.cs:47-123`, which mints a global-administrator token through `CompositionTokenFactory.cs:41-90`.
- Builds tests are in `test/Hexalith.Builds.Module.Tests/`: `StartupReadinessTests`, `StartupCoordinationTests` and `CompositionStartupTaskOutputTests`. Use the fixture mutation `workspace.Change("modules.0.lifecycle.tasks.0.<field>", …)`.
- Platform, under `src/Hexalith.Platform.Composition/`:
  - `PlatformDeclarationSet.cs:56-79` holds the HXP020 check. Leave it unchanged.
  - In `PlatformAppModel.cs`, the realm is enabled at 113, validation runs at 205, `realm-instance.json` is written at 329-334, and the hosted-service registration pattern is at 620-640.
  - `PlatformLocalRealm.cs`: service accounts only and direct grants off (232-268), client secrets (479-486), RS256-only resource authentication (535-554). `Declarations/realm-contract.v1.json:134-160` is the `tenants` service client, the source of the workload assertion.
  - `PlatformRoutingCatalogBootstrap.cs:105-123` generates the proof keys and bound domains. Bootstrap access is set only for the Parties test.
  - `PlatformNativeTestPreparation.cs:224-287` and `PlatformNativeGatewayContract.cs:16-55` already send the bound submit through the Tenants sidecar Dapr invoke, along with `BootstrapGlobalAdmin` and the status poll. Reuse them as the relay's runner-key path.
- EventStore checks, under `references/Hexalith.EventStore/src/`:
  - `CommandsController.cs:96-140` requires the Dapr app channel, `sub = workload:{caller}`, an allowed domain, a proof checked against `BoundStreamRead:PublicKeys`, a human bearer whose `sub` equals `PrincipalId`, exactly one `tenants:create`, and `X-Hexalith-Scope` equal to the tenant.
  - `CommandStatusController.cs:252-293` requires exactly one `tenants:read`.
  - `RouteCatalogEligibilityBehavior.cs:37-47` admits internal commands only from the `service` surface.
- `src/Hexalith.Platform.EventStoreHost/Program.cs:45` calls `AddPlatformErasureRootProtection`, which throws `EXT-PROTECTION-1:ExplicitProtectionInstallationRequired` unless `EventStore:PayloadProtection:Enabled` is set (`src/Hexalith.Platform.Protection/ErasureRootHostComposition.cs:21-28`). Local composition sets Development at `PlatformAppModel.cs:743` and the catalog path at `PlatformRoutingCatalogBootstrap.cs:56`.
- `PlatformNativeTestPreparation.cs:165` submits `CreateTenant` for the run tenant. In Tenants, `TenantAggregate.cs:46-66` makes `CreateSyntheticTenant` a no-op for an existing synthetic tenant and rejects it for a regular one.
- Tenants contract: `references/Hexalith.Tenants/module/tasks/create-synthetic-tenant.py:33-164` and `docs/synthetic-tenant-task.md`. The task sends `POST {gatewayUrl}/api/v1/commands` and `GET {gatewayUrl}/api/v1/commands/status/{messageId}`. Loopback http is allowed, and redirects are refused.

## Tasks & Acceptance

**Execution:**
- [x] `CompositionStartupTasks.cs`, `CompositionStartupTask`, `CompositionRunPlan.cs` (Builds): schedule `once-per-environment-creation` and `per-start-verify` tasks in declaration order. Record a private invocation path for each one and pass it as `HEXALITH_TASK_INVOCATION_PATH`.
- [x] `AppHostRunner.cs`, `WriterProtocolCutover.cs`, `CompositionWriterProtocolCutover.cs` (Builds): use an operator token source registered by the model when one exists. Otherwise use the HS256 key.
- [x] Builds tests: scheduling, invocation path and environment, the fallback and the model token source, and an unchanged `per-start-verify` path.
- [x] `PlatformLocalRealm.cs`, `PlatformAppModel.cs`: render the operator client, implement the token source and register it for the cutover.
- [x] New `PlatformOperatorTaskRelay.cs`, plus `PlatformAppModel.cs` and `PlatformRoutingCatalogBootstrap.cs`: write the invocation file, host the relay, bootstrap the operator and forward the two calls with realm or runner-key tokens.
- [x] `tests/Hexalith.Platform.Composition.Tests/`: cover the matrix against a fake gateway. Check realm rendering and that the invocation file holds no secret.
- [x] `src/Hexalith.Platform.EventStoreHost/Program.cs` plus a host test: apply the Development-only protection exemption. Prove that a non-Development host, or one without the development catalog path, still refuses.
- [x] `PlatformNativeTestPreparation.cs` plus its tests: create the run tenant with `CreateSyntheticTenant`, so the scheduled task is a no-op in Parties runs.
- [x] Live run: run the complete selector in source mode with the Builds candidate. Record the evidence and tear down by exact run ID.

**Acceptance Criteria:**
- Given the Builds candidate and the Platform changes, when the complete selector runs in source mode, then preflight passes and the composed gateway starts. Slice 4's acceptance now owns reaching readiness with the realm cutover and `tenants/create-synthetic-tenant` passed.
- Given a Tenants composition without the generated realm, when it runs in source mode with the Builds candidate, then it reaches readiness with `tenants/create-synthetic-tenant` passed through the relay and the descriptor naming the synthetic tenant.
- Given a real Keycloak serving the generated realm, when the operator requests a grant, then the token carries exactly that grant, and a wrong operator secret is refused with `platform-operator` named.
- Given the existing Platform composition and Builds suites, when they run, then they pass apart from the expected new task reporting.

## Implementation Notes

- Platform candidate `42abf5b2652012ed54ca78363e9e22fcc03dc9d8` (review fixes on `7e56ebeacfecff1bb67b59300b2603d7ab8780df`, on `e6c4dd627cc82252e9590d29884032157f342e80`) is a detached commit in `../platform-story-3-6-slice-5`, kept by `refs/hexalith-candidates/story-3-6-slice-5`. All 18 submodule worktrees there match the gitlinks. The main checkout holds no code changes.
- Gateway exemption: `PlatformGatewayProtection` skips `AddPlatformErasureRootProtection` only for a Development host with the development catalog path. It also never skips when `EventStore:PayloadProtection:Enabled` is true, so the signed-fixture qualification still validates its installation. `PlatformGatewayProtectionTests` proves the exemption, the non-Development, missing and blank catalog-path refusals, and that enabled protection is still validated.
- Parties: native preparation creates the run tenant with `CreateSyntheticTenant` through the bound Tenants channel, then runs `AddUserToTenant` and Memories provisioning unchanged. `OperatorIdentity` names `CreateSyntheticTenant` with the same `tenants:create` grant. The bound channel admits `global-administrators,tenants` for both native preparation and the relay. The contract checks prove the bound creation is accepted with the verified administrator flag.
- Builds candidate `c428fd475eace9d92f30087157ecec94cda050f9` (review fixes on `4c70470360a4732aa027d586dd09adfdae1692b9`) is a detached commit on `v6.10.0` in `../builds-story-3-6-slice-5`, kept by `refs/hexalith-candidates/story-3-6-slice-5`. It is not pushed. Builds schedules both startup scopes in declaration order. Each environment-creation task records `<workspace>/task-invocations/<module>.<task>.json` in `plan.json`, and the runner creates that directory with mode 700. Only that path is passed as `HEXALITH_TASK_INVOCATION_PATH`. A model can register `ICompositionOperatorTokenSource` for the cutover. Without one, the cutover keeps the HS256 key. With one, it never falls back, and a refusal or deadline reports only `operator=<name>` through `HXR027`.
- Platform renders the instance-only confidential client `platform-operator` in the generated realm. It has one optional scope per grant, and `PlatformOperatorTaskBinding` derives those grants from the catalog-declared bootstrap and synthetic-tenant commands. `PlatformRealmOperatorTokenSource` serves the cutover. `PlatformOperatorTaskRelay` writes the 600-mode invocation, which holds only the relay credential, and serves the two task calls on 127.0.0.1. It bootstraps the operator once and returns gateway answers unchanged. Under the realm it uses realm tokens and the Tenants realm assertion. Otherwise it uses runner-key tokens. The bound `tenants` channel admits only `global-administrators,tenants`.
- The creation token carries `tenants:create` plus the global-administrator role, as the amended grant rule allows.
- Verification: 921 focused Builds tests passed. The 20 new Platform tests and the opt-in Keycloak 26.6.4 operator check passed. The Platform regression classes ran 310 tests: 2 failed for reasons this slice does not touch, and 4 opt-in tests were skipped. A live Tenants run with runner-key tokens reached `HXI010`, and `tenants/create-synthetic-tenant` passed. [Evidence](../../evidence/story-3-6-platform/slice-5-verification.log)
- Follow-up verification: 48 focused tests and 326 of 332 regression tests passed. The 4 skips are opt-in tests. Two tests failed. `TechnicalSuiteAttributesInfrastructureToItsActualPlatformDeclarationAsync` fails the same way at baseline `1ae50cd1` with published Builds 6.10.0, so it predates this slice. `FullPackageFixtureKeepsGatewaySeedAndContainerSubscriptionBindingsAsync` fails at baseline only on a different guard, so it is not proven pre-existing. It passes on the candidate with a packed candidate tool, so it fails only with the source-built tool layout. [Evidence](../../evidence/story-3-6-platform/slice-5-verification.log)
- Live complete run `27cc3137f6a435a8d6384d268a02f381`: the gateway now starts. A new blocker outside this slice remains. Memories and Projects exit during the development catalog bootstrap with `Loaded topology disagrees with the candidate`. Memories reports `undeclared-acl-operation` and Projects reports `acl-grant-has-no-finite-authenticated-receiver`, both from `RouteCatalogTopologyValidator`. The catalog never activates, so the cutover and the task never run, and the run ends with `HXR021` at the 600 s deadline. The run was torn down by exact run ID. The live-run task and AC1 stay open.

- Review patches (iteration 0, 13 patches): Builds `c428fd475eace9d92f30087157ecec94cda050f9` and Platform `42abf5b2652012ed54ca78363e9e22fcc03dc9d8`, both detached and kept by `refs/hexalith-candidates/story-3-6-slice-5`. Final verification on 2026-11-28 used a tool repacked from `c428fd47` (`-p:Version=6.11.0-candidate.1`) as `HexalithBuildsPublishedToolRoot`. Six Builds classes ran 116 tests: all passed, none skipped. Twelve Platform Composition classes ran 383 tests: 377 passed, and the 5 skips are opt-in live tests. The one failure is `TechnicalSuiteAttributesInfrastructureToItsActualPlatformDeclarationAsync`, which fails the same way at baseline. With `HEXALITH_PLATFORM_LIVE_SMOKE=1`, the real-Keycloak operator test passed. The complete live run was not repeated, because its readiness now belongs to slice 4.

## Spec Change Log

- 2026-11-28, after implementation. Trigger: the complete run failed with HXR025 because the composed gateway requires a production erasure-root installation (`EXT-PROTECTION-1`). Tenants also checks admin only through the role claim, and Parties native preparation creates the run tenant as a regular tenant. The maintainer renegotiated the intent and adopted all four recommendations. Amended: Approach, Decisions, the grant rule, the protection-exemption and candidate-worktree constraints, the Code Map and two new tasks. Avoids: a complete run that can never start locally, a creation command Tenants always rejects, and Parties runs failing with `TenantAlreadyExistsRejection` once Builds is released. KEEP: the relay in the AppHost, the per-run relay credential, the HS256 path without the realm, and the released Tenants task bytes.

- 2026-11-28, after the follow-up run. Trigger: complete run `27cc3137f6a435a8d6384d268a02f381` started the gateway, but then Memories and Projects failed catalog topology validation (`undeclared-acl-operation`, `acl-grant-has-no-finite-authenticated-receiver`). The failing ACL grants come from slice 4's wiring in `1ae50cd1`. The maintainer moved readiness of the complete run to slice 4's acceptance. Amended: the acceptance criteria now cover what this slice proves (the gateway starts, the Tenants run passes the task through the relay, and the real-realm operator check), and the live-run task is checked. Avoids: widening this slice a third time into slice-4 wiring and owner manifests. KEEP: every implementation decision above. Slice 4 must prove the realm cutover and the task end to end.

## Review Triage Log

Iteration 0 (2026-11-28): blind hunter, edge-case hunter and verification-gap layers; diff Platform 1ae50cd1..7e56ebea plus Builds v6.10.0..4c704703.

| Finding | Claim | Verdict | Route | Evidence |
|---|---|---|---|---|
| V1 verification-gap | AppHost cutover wrapper lookup of ICompositionOperatorTokenSource has no test | medium | patch | Pre-verified: only direct ActivateAsync callers and custom activate: hooks are tested. Dropping the lookup keeps every test green. |
| V2 + B17 | No default test composes the realm-credential relay branch used by complete runs | medium | patch | Pre-verified: the OperatorTask test covers the runner branch only. The complete-environment test has no RunPlan, so SelectTask returns null. |
| V3 verification-gap | Relay tests only check that the forwarded workload assertion exists | medium | patch | Pre-verified: PlatformOperatorTaskRelayTests.cs:85 uses ShouldContainKey only, and the fake upstream accepts any request. |
| V4 verification-gap | PlatformOperatorUpstream.Resolve has no automated test | medium | defer | Pre-verified, filed disposition defer: the repo verifies application-model endpoint resolution only through live runs, and the manual Tenants run 2e84cc31 covered it. |
| V5 verification-gap | Native preparation switch to CreateSyntheticTenant is never executed by a test or run | medium | defer | Pre-verified, filed disposition defer: native preparation is verified only by live Parties runs. Record one with the Builds candidate before releasing Builds. |
| V6 verification-gap | No test boots the real gateway host on the Development exemption path | medium | patch | Pre-verified: every WebApplicationFactory harness sets PayloadProtection:Enabled=true and no catalog path, so reverting Program.cs:45 stays green. |
| V7 verification-gap | Test for the inherited HEXALITH_TASK_INVOCATION_PATH scrub cannot fail | low | patch | Pre-verified: the test process never sets the variable, so "unset" is printed with or without the Remove at CompositionStartupTasks.cs:224. |
| B10 + E6 + V-other | Cutover status stays operator-token-unavailable after the token was issued | low | patch | CompositionWriterProtocolCutover.cs:103 sets lastStatus before the token request and never resets it. A hang in ActivateProtocolAsync then throws cutover-timeout;operator-token-unavailable. |
| B2 + E5 | Operator client gets every relay grant even when no task is scheduled; the inline comment is wrong | low | patch | PlatformAppModel.cs passes catalog.OperatorTask.Grants whenever the catalog declares both commands, independent of SelectTask. Direct one-line fix. |
| E1 | ResponseHeadersRead bypasses the relay 64 KiB upstream body bound | low | patch | RelayAsync uses ResponseHeadersRead, and then ReadAsByteArrayAsync, which MaxResponseContentBufferSize does not limit. Fix is to drop the option. |
| E3 | DisposeAsync deletes task.InvocationPath even when PrepareAsync refused it | low | patch | DisposeAsync deletes the raw plan path without the RequireInvocationPath validation. Delete only the path validated in PrepareAsync. |
| B15 | Submit accepts message ids that the status route then 404s forever | low | patch | TaskCommand admits any id of 1-128 characters, but the status route admits only [A-Za-z0-9-]. Validate the same set on submit. |
| E11 | A null StartupTasks in a deserialized plan throws NullReferenceException in PlatformRoutingCatalogBootstrap | low | patch | options.RunPlan?.StartupTasks.Any(...) has no null fallback, while SelectTask uses ?? []. Direct correction. |
| E4 + B9 sub-point | Relay binding skips the internal-ingress check for catalogs other than schema v3 | low | patch | PlatformOperatorTaskBinding.Operation checks ingress only when SchemaVersion == 3, while native preparation refuses non-v3 catalogs. Require v3. |
| B18 | Native preparation names a message id tenantId | low | patch | PlatformNativeTestPreparation.cs: SubmitBoundAsync returns the message id passed to WaitForCommittedAsync. Rename it. |
| V-other | Platform candidate cannot build at its own Builds gitlink (v6.10.0) | medium | defer | The intent forbids releasing Builds. The adoption commit must release Builds, repin the gitlink and tool version, then land the Platform candidate. |
| B14 sub-point | Realm workload assertion carries no tenant or domain binding claims | maybe-false (medium if true) | defer | Unverified: whether the gateway accepts an unbound realm assertion for the bound submit. Slice 4 complete run (moved AC) settles it. |
| B1 | AlreadyBootstrapped is accepted without checking the bootstrapped subject | maybe-false (low if true) | reject | Runner-key subjects match: both are g4-operator-<runId> (PlatformNativeTestPreparation.cs:130, PlatformAppModel.cs:361). A mismatch needs a realm run with Parties native preparation, and it would fail loudly with HXR021. |
| B3 + E8 + E13 | Builds without HEXALITH_PLATFORM_CANONICAL_SCOPES half-wire the task, and still use CreateTenant | false | reject | The symbol is defined for project references or EventStore >= 4.0.0 (Composition csproj:21), and the pinned EventStore is 5.x. The non-canonical build is unreachable. |
| B4 | Runner does not pre-check that the invocation document exists | false | reject | The frozen matrix defines this exact behaviour: the task exits 2 and HXR021 names it. CompositionEnvironmentTaskTests covers it. |
| B5 + E9 | Creation tasks are not ordered before verification tasks | false | reject | The spec mandates declaration order for both scopes. |
| B6 | Descriptor lacks task scope; per-start test name claims an unchanged plan shape | low | reject | The spec keeps the descriptor rule unchanged. plan.json is runner-internal. The fix adds surface. |
| B7 | Relay credential and listener live for the whole run | low | reject | Loopback only, per-run credential, and only the exact idempotent task command. Closing it adds lifecycle branches. |
| B8 | Readback check weaker than native preparation, and cached | low | reject | The catalog commits once per run, and the relay is used once. Revalidation adds complexity for an unlikely re-commit. |
| B9 | Status read permission derived by naming convention | false | reject | EventStore hard-codes the same mapping (CommandStatusController.cs:276-277: platform:tenants => tenants:read). The resolution-style inconsistency is cosmetic. |
| B11 | Protection bypass is silent and broader than documented in builds without CATALOG_SDK | low | reject | Gated to Development plus the catalog path as the maintainer approved. Logging before Build() needs new plumbing. |
| B12 + E12 | Relaxed Tenants issuer lookup weakens fail-fast on the Parties path | low | reject | Parties runs always carry the issuer (the indexer previously succeeded). A guard adds branches for an unlikely case. |
| B13 | Invocation document has no version or shared contract | false | reject | The format is owned by the released Tenants task contract (docs/synthetic-tenant-task.md). The spec keeps Builds owner-agnostic. |
| B14 | Relay mints fresh credentials on every call | low | reject | A few local token requests per task run. Caching adds complexity. |
| B16 | RequireExactGrants ignores aud, iss and exp | low | reject | The gateway validates them anyway. An extra guard for an unlikely mapper regression. |
| E2 | A retryable terminal or missing bootstrap status polls until the 60 s bound | low | reject | Fails loudly with 502 within the bound. Resubmission logic adds branches. |
| E7 | Another module's creation task gets an invocation path nobody writes | low | reject | Only Tenants declares one, and it fails loudly with HXR021. Refusing unknown tasks up front adds a guard. |
| E10 | An oversized 2xx token response is retried as an unavailable realm | low | reject | Keycloak tokens are far below 64 KiB. Telling the cases apart adds branches. |
| E14 | A manually configured Development gateway also skips protection | false | reject | This is exactly the gate the maintainer approved on 2026-11-28 (Development plus the catalog path). |

## Design Notes

The relay belongs in the AppHost, because every secret the bound submission needs already exists there. The runner gets a per-run relay credential and nothing more. The model-supplied token source keeps Builds generic and leaves non-realm runs on today's HS256 path.

## Verification

**Commands:**
- Build the Builds tests in Release, then run `dotnet <Hexalith.Builds.Module.Tests.dll> -class <Class>` per class. Expected: the selected classes pass. Combining `-class` and `-method` selects zero tests.
- Build Platform in Release with `-nodeReuse:false`, then `dotnet exec` the Composition tests DLL with focused `-class` filters. Expected: everything passes, with no skips.
- Run `run-with-temp dotnet run --project <worktree>/src/libraries/Hexalith.Builds.Module.Cli -- run --manifest module/hexalith.module-manifest.v2.json`, then `down --run-id <id>`. Expected: `HXI010`, then exact teardown.
