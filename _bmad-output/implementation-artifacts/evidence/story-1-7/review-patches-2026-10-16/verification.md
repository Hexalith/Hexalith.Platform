# Story 1.7 review patch verification — 2026-10-16

The four outstanding patch findings required test coverage; production behavior is unchanged. The assertions cover exact Tenants and Memories receiver caller/path/verb grants, deny defaults and trust identities; declarative subscription mode in every source, package and signed-run server environment; owned scheduler bind mounts and the named-volume fallback; and environment-selected or run-plan-selected Dapr homes in sidecar options and actual toolkit-generated CLI commands/arguments. The signed-plan case sets a different ambient home to verify precedence. Paths include spaces.

| Check | Result | Evidence |
| --- | --- | --- |
| Platform composition test-project Debug build | Passed, zero warnings/errors | [build](build.txt) |
| Focused new/extended assertions | 12 passed, zero failed/skipped | [focused tests](focused.txt) |
| Full Platform suite | 142 total: 137 passed, zero failed, five gated skips; 33.570 s | [suite](suite.txt) |
| Platform solution Debug build | Passed, zero warnings/errors | [solution](solution.txt) |
| Reference declarations | 41 PASS checks | [declarations](declarations.txt) |
| Canonical hosting helper gate | Six passed | [helper gate](helper-gate.txt) |
| Architecture lint | Passed, zero findings | [architecture](architecture.txt) |
| File-entry LocalScaffold | Passed | [scaffold](scaffold.txt) |

These are model and callback assertions. The toolkit's real `BeforeStartEvent` creates the CLI executable resources, whose executable path and `--runtime-path` arguments are inspected without launching Dapr. The implementation subagent did not rerun live tests or affected owner regressions for this test-only change. The parent subsequently reran the direct pinned-runtime security fixture as part of the matrix audit below. Live enforcement, subscription delivery and cleanup retain the historical evidence and limitations in [the story verification record](../verification.md); package/image release prerequisites, Redis wildcard delivery, concurrent-run isolation and deferred review findings remain unchanged.

An initial focused run evaluated the new application-environment assertion after `BeforeStartEvent`, when toolkit-added callbacks need allocated endpoints. It failed with an unallocated `eventstore-dapr-cli` HTTP endpoint. The assertion now runs before that event; generated CLI checks still run afterward. The retained focused and full logs are the subsequent passing runs.

The spec's existing baseline remains `c3af1f2c9e7b0d1b003a1bd5978545f664e38167`. During verification a concurrent repository update advanced HEAD to `6020c583c5644952a11312715020ce2b050c0218` (`chore(platform): record offline qualification and composition tests`), observed through `git rev-parse HEAD` and `git log -1 --format=%s`; it already includes the four changed test files. This implementation agent did not stage or commit files or modify recovery work. [The retained source state](source-state.json) records those commands and the test-source SHA-256 values.

Commands ran from the Platform root:

```bash
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1 --nologo
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -parallelMode none -method '*TenantsAndMemoriesReceiversHaveExactlyTheirDeclaredInvocationGrants' -method '*ControlPlaneWithoutOwnedSchedulerDataUsesThePreservedNamedVolume' -method '*SelectedDaprHomeConfiguresTheActualCliAndEverySidecarRuntimeAsync' -method '*GraphHasSixDeclaredServersAndNoOptionalOrLegacyHosts' -method '*ActualDaprCliListenersAndTokensRemainServerScopedAsync' -method '*PackageCompositionAddsCatalogContainersWithDeclaredSidecarsAndScopedBindingsAsync' -method '*SignedCompositionKeepsRunDiscoveryPortsSharedStateAndApiCredentialsAsync'
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -parallelMode none
dotnet build Hexalith.Platform.slnx -c Debug -m:1 --nologo
python3 scripts/test-reference-module-declarations.py
python3 eng/test_verify_platform_pins.py
python3 .agents/skills/bmad-architecture/scripts/lint_spine.py --workspace _bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27
bash eng/verify-agents-host.sh --mode LocalScaffold --artifacts-path /tmp/story-1-7-review-patches-scaffold-artifacts
```


## Parent matrix audit

The parent read the test patch diff and the implementation's component, scope, ACL, subscription and runtime wiring diff against the preserved baseline. All six execution tasks remain complete. The hosting-helper check passed six cases, and verification records distinguish model/callback assertions from live enforcement and release/image qualification.

Fresh matrix run: **17 passed, zero failures/skips** in 8.105 s. [Raw matrix output](matrix.txt) and [live fixture result](security/result.log) retain the result. The live fixture directly uses the pinned `daprd`, with its previously documented fixture Sentry port adaptation; it does not qualify CLI launch, package images or Redis projection wildcard delivery.

| Matrix row | Passing coverage |
| --- | --- |
| Local invocation | `LiveLocalDaprEnforcesInvocationAndDeclarativeDeadLetterDeliveryAsync`: mTLS, permitted callers, seven denied cases and cleanup |
| Capability | `ActualOwnerOptionsShareResolvedNamesAndTelemetryStateIsIsolated`; 13 `InvalidDaprBindingsReturnSourceAndFieldWithoutDeclarations` cases: assigned configuration names, component scopes, telemetry isolation and atomic diagnostic refusal |
| Topic | `PublisherInventoriesAloneCreateNoSubscriptions`; `ExplicitSubscriptionRendersDeliveryAndDeclaredDeadLetterWithoutPublisherSubscriptions`; invalid-binding cases and the live fixture: explicit receiving routes, pubsub binding, scoped delivery and dead-letter handling |

The parent executed:

```bash
HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1 \
HEXALITH_DAPR_HOME=/tmp/story-1-6-builds-toolchain \
HEXALITH_PLATFORM_SECURITY_EVIDENCE=<absolute path to security directory beside this file> \
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll \
  -parallelMode none \
  -method '*LiveLocalDaprEnforcesInvocationAndDeclarativeDeadLetterDeliveryAsync' \
  -method '*PublisherInventoriesAloneCreateNoSubscriptions' \
  -method '*ExplicitSubscriptionRendersDeliveryAndDeclaredDeadLetterWithoutPublisherSubscriptions' \
  -method '*InvalidDaprBindingsReturnSourceAndFieldWithoutDeclarations' \
  -method '*ActualOwnerOptionsShareResolvedNamesAndTelemetryStateIsIsolated'
```
