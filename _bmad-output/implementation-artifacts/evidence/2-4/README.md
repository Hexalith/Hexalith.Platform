# Story 2.4 implementation evidence

The runner provisions one fresh owned environment for a declared suite or compatible repeated-profile batch, waits for actual readiness and required startup tasks, persists separate execution activation, validates the credential-free v1 descriptor, and launches native children with its absolute path. Local source/Debug qualification is complete and Story 2.4 is ready for review.

## Verification results

| Check | Result | Evidence |
| --- | --- | --- |
| Builds Module.Tests Debug build, `-m:1` | 0 warnings, 0 errors | `builds-build-debug.log` |
| Serial affected Builds regressions | 370 passed, 0 failed, 115.462 s | `builds-affected-370.log` |
| Final descriptor endpoint guard focused checks | 75 passed, 0 failed, 7.978 s | `builds-focused-75.log` |
| Platform Composition.Tests Debug build, `-m:1` | 0 warnings, 0 errors | `platform-build-debug.log` |
| Platform retention and build isolation checks | 29 passed, 0 failed, 8.572 s | `platform-affected-29.log` |
| Final fixture rebuild | 0 errors; 1 existing graph MSB3277 Redis assembly conflict warning | `platform-build-live-fixture-final.log` |
| Two actual fresh declared suite commands | 1 live test passed, 0 failed, 101.923 s | `live-technical-attempt3/qualification.log` |
| Current Platform/Builds v1 schemas | Byte-identical environment/result files | `schema-parity.json` |
| Final exact-owner audit, all six attempted run IDs | No owned containers/processes, state, workspaces or claims | `final-cleanup-audit.json` |

These results describe the initial Story 2.4 handoff. Production code subsequently changed in response to parent review; focused results and the exact commands are recorded under `review-fixes/`. The parent owns the full regression run and fresh serial live proof for those corrections. No additional test suite was run concurrently with the initial live proof.

`verification.json` records results, limitations and each positive run's real identity, descriptor and execution timing. `final-source-revisions.json` records full current heads and tracked working-tree diff digests. Each live attempt also retains the full heads observed at its own start; independent concurrent work was preserved. The immutable selected manifest/entry hashes and observed catalog/composition identities are in each run's diagnostics.

## Commands and covered behavior

From `references/Hexalith.Builds`:

```sh
dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1 --no-restore
```

The affected serial xUnit assembly selection used `-parallelMode none` and covered `TestEnvironmentProvisioningTests`, `CompositionResourceClaimStoreTests`, `ModuleTestSelectionTests`, `NativeTestExecutorTests`, `StartupCoordinationTests`, `StartupReadinessTests`, `SelectedObservationTests`, `ModuleRunEvidenceSerializationTests`, `ModuleRunEvidenceWriterTests`, `ModuleCommandApplicationTests` and `TestEnvironmentContractReaderTests`. The final 75 focused checks covered provisioning, claims, selection and native execution after the writer's missing-observed-endpoint guard. Coverage includes delayed readiness preserving both sequential entry budgets, current selected readiness metadata, profile compatibility refusals before allocation, source/closure/catalog drift, missing observations and startup tasks, hosted endpoint refusal, reader validation, owner-bearing claim conflicts, rollback and peer-safe release.

From the Platform root:

```sh
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1
```

Platform affected checks used the xUnit assembly with `-parallelMode none`, retention methods and the explicit project-output/readiness methods. They cover startup-plus-execution certificate ceilings, original gate expiry enforcement, trusted TLS readiness and run-private selected source outputs.

The positive live invocation was:

```sh
env HEXALITH_PLATFORM_PROVISIONING_LIVE=1 \
  HEXALITH_DAPR_HOME=/tmp/platform-2-3-tool-inputs-y9ophakd \
  HEXALITH_PLATFORM_PROVISIONING_EVIDENCE=/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/2-4/live-technical-attempt3 \
  dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll \
  -class '*PlatformTestEnvironmentProvisioningTests'
```

The tool home contains the catalog-matching Dapr inputs already prepared for Story 2.3; no pins changed. This gated fixture starts two `ModuleCommandExecutionService.Test` commands against `eng/test-environment-provisioning/manifest.v2.json`, whose real minimum closure is Platform + EventStore and owned Redis/Dapr infrastructure. Both actual test children consume only the runner descriptor and declared legacy safe handoff variables. They persist the same Redis key with their own run ID, call `SAVE`, read their own value and assert the peer's unique key is absent. The descriptor-derived shared proof barrier is inside this test's private workspace; the arbitrary native environment allowlist was not expanded.

The positive runs were `717519ee8557410c70682ed47bab1303` and `fb10e0a7e55275b6a62b331610ad461f`. Both completed with `HXI005`, validated descriptors and required `platform/verify-run-owner` startup results. Their six observed resources were healthy before handoff. One run spent approximately 96 seconds starting and still received its complete 120-second execution interval. Actual command output contains no `HXR003`. Each descriptor reserves unavailable candidate/tool/artifact/suite proof as `not-yet-populated`, and each data receipt explicitly grants no qualification credit.

The positive attempt contains original fixture inputs, command results, descriptors, data receipts, per-entry timing/native reports, observed readiness/tasks, immutable selection/mapping/plan metadata and successful explicit owned cleanup receipts. `cleanup-audit.json` confirms no remaining JSON claims or run workspaces. The independent final read-only `CompositionRunResourceScanner.FindAsync` audit covers every run from all attempts.

## Retained failures and limits

`live-source-attempt1/` preserves the initial six-server reference composition. Real infrastructure and EventStore became healthy, but Memories or Tenants application resources finished before healthy and the readiness guard returned `HXR025`. No descriptor or native child was handed off. The separate diagnostic `source-memories-no-restore.log` reproduced a missing Commons namespace; a normal restored build passed in `source-memories-restored.log`. Those standalone builds are diagnostic evidence only and do not qualify that broader running composition. Its owned cleanup succeeded.

`live-technical-attempt2/` reached full readiness and valid descriptor handoff in both environments. The fixture then incorrectly expected a custom evidence environment variable to pass the existing native environment allowlist. The correction derives the private shared directory from the absolute descriptor path. The first explicit stop receipts reported `HXR028` (blocked stdin listener) despite verified empty resources; both original receipts remain, and ownership-limited same-ID idempotent retries subsequently succeeded with no claims/state/workspaces. No lifecycle behavior was relaxed to hide the initial diagnostic.

`builds-regression-initial-with-live-load.log`, `builds-serial-before-fixture-adjustment.log` and `startup-deadline-isolated.log` preserve the startup-task fixture investigation. Its original eight-second deadline could expire before the intended task phase under startup load. The final fixture creates a start sentinel, uses a finite 15-second test-only bound and proves task termination. The final serial 370-test run passed. Production's 600-second startup default is unchanged.

This evidence establishes local source/Debug provisioning and persisted isolation for the supported technical closure. It does not qualify the broader reference domain composition, package-mode execution, CI orchestration, attachment admission, domain fixture migration, tool/candidate acceptance, or publication. No version-control commits, pushes, or pin changes were made by this implementation task.
