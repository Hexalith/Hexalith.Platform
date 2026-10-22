# Story 2.3 verification

## Current qualification status

**Story 2.3 remains in review: final live qualification is incomplete.**

- [Normal Composition Debug build](final-build-composition-complete.log): **passed**, zero warnings/errors, with analyzers and project references enabled. [Ordinary suite against that build](final-ordinary-qualified.log): **141 passed**, zero failed, five gated skips (146 total), 76.840 seconds.
- [Builds Module normal Debug build](final-build-builds.log) passed, as did [22 relevant runtime tests](final-builds-runtime-tests.log). Independent lifecycle changes then arrived during verification: the [latest required build](latest-builds-lifecycle-blocker.log) now fails with **37 analyzer errors** in execution-budget, resource-claim, command-execution, startup-task and descriptor work. Those lifecycle policies are outside the frozen Story 2.3 intent and were preserved.
- [Occupied-port proof](final-occupied-port.log): **one passed**, no skip, 2.623 seconds. The foreign listener and marker survived candidate cleanup; task fixtures were removed.
- [Latest concurrent proof](final-live-qualified.log): executed, not skipped; fails **HXR027**, `cutover-timeout;health-HTTP-503`, before first-session readiness. [Startup diagnostics](live-concurrent-qualified/startup-failure.json) are retained. The expanded cross-run native-listener assertions remain unexecuted. The earlier complete 403.261-second concurrency pass remains historical evidence and does not qualify the final code state. No readiness or security check was weakened.
- [Latest file-entry smoke](final-file-entry-qualified.log): executed, not skipped; fails before AppHost resources start on the new Builds analyzer errors. The [previous file-entry attempt](final-file-entry.log) reached its owned host and reported the earlier NuGet conflict; its finally teardown passed.
- The earlier NU1506 blocker was repaired without changing package versions: the clean EventStore owner props now update existing cryptography pins, with conditional additions for older catalogs. [Both catalog evaluation cases passed](package-catalog-compatibility.json); [the exact owner compatibility diff](eventstore-package-compatibility.diff) is retained. No tool was published or repinned and no gitlink was staged by this workflow.
- [Cleanup audit](final-cleanup-audit.json): both failed concurrent attempts have no labelled containers, recorded project processes or AppHost PID left. No foreign-resource cleanup was performed.
- [Exact final source revisions/hashes](final-source-revisions.json), [workspace status](final-dirty-status.txt). The source tree continued changing independently during verification; passing results are tied to their recorded attempts.

To finish: let the independent Builds lifecycle implementation pass its normal build, collect the gateway `/health` result behind the HXR027 cutover timeout, then rerun normal builds, the expanded concurrent proof and file-entry smoke. Preserve existing source-identity, readiness and mTLS checks.

## Earlier implementation evidence

Local source verification only. No hosted data, package-mode CI, descriptor migration,
attachment/lifecycle policy or Works qualification is claimed.

- Ordinary Composition xUnit suite: **138 executed/discovered, 0 failures, 3 gated skips**, 33.549 seconds. The skipped cases are the two existing live security fixtures and the file-entry live smoke; the latter was executed separately and failed as recorded below. The subsequently added occupied-port test was executed separately.
- Focused final isolation matrix: **7 passed, 0 skipped**, including existing/dangling checkout symlinks, missing selected native runtime, private signed/file resources, actual native callbacks and retained failed-start diagnostics.
- Builds renderer plus existing runtime/path/plan suites: **22 passed, 0 skipped**; renderer class alone **4 passed**.
- Real concurrent proof: **1 passed, 0 skipped**, 403.261 seconds. See [the complete receipt set](live-concurrent-final/) and [runner output](live-concurrent-final.log). Both six-server sessions reached declared readiness together. The test checked disjoint persisted/bound ports, container IDs, writable mounts, SQLite discovery and certificate identities. Redis, Redis Stack and FalkorDB retained distinct values under the same key and wrote nonempty snapshots. After peer teardown, the survivor returned fresh declared readiness and all three values remained `run-1`. Both final cleanup receipts report success and no leftovers.
- Real occupied-port proof: **1 passed, 0 skipped**, 2.409 seconds. See [runner output](tests-occupied-port.log) and [diagnostics/cleanup](occupied-port/). A separately labelled Redis fixture occupied the Docker daemon's real publish boundary. Model-derived Sentry startup failed, diagnostics survived, candidate-only cleanup left the fixture's marker and PING intact, then fixture teardown removed only labelled task containers. Earlier WSL-socket experiments were nondeterministic across Docker Desktop's namespaces and are not passing evidence.

## Required build and live blockers

The normal individual Composition and Builds Module Debug builds were attempted with
`-m:1`. They encountered independently edited Builds ReleaseRecords/TestEnvironment/
CheckSuites analyzer failures. Logs are retained as `build-normal-initial.log` and
`build-builds-normal.log`. A later dependency build also failed in independently
changed EventStore.Contracts (`EventIdentityValidator.cs`: `Hexalith.Commons` missing);
see `build-source-dependency-blocker.log`.

Supplemental Debug builds passed with `RunAnalyzers=false`. Composition test builds
used the external `CustomAfterMicrosoftCommonTargets` file shown below to exclude
only the independent `PlatformHostedPublishingTests.cs` API compile error present
at that time. Final library/test compiles used existing dependency outputs through
`BuildProjectReferences=false`. These checks supplement the required gate; they do
not turn its failures into a pass.

```xml
<Project>
  <Target Name="ExcludeIndependentHostedPublishingTestForIsolationEvidence" BeforeTargets="CoreCompile" Condition="'$(MSBuildProjectName)' == 'Hexalith.Platform.Composition.Tests'">
    <ItemGroup><Compile Remove="PlatformHostedPublishingTests.cs" /></ItemGroup>
  </Target>
</Project>
```

The existing file-entry smoke was explicitly executed with the catalog-matching
Aspire/Dapr tools and `--isolated`. It failed before starting resources because
independent Custody compilation could not find EventStore contract types including
`DeletionBatchCapabilityV1` and governance/deletion types. See `live-file-entry.log`.
No existing application was stopped. File-entry graph/callback tests pass, but live
file-entry qualification remains blocked separately from the passing Builds proof.

## Commands

```bash
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1 --nologo
dotnet build references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1 --nologo
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class '*PlatformAppModelTests'
dotnet references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class '*CompositionDaprComponentRendererTests' -class '*CompositionPortAllocatorTests' -class '*CompositionRunPlanFactoryTests' -class '*CompositionAspireHostPathsTests' -class '*CompositionWorkspaceTests'
HEXALITH_PLATFORM_CONCURRENT_LIVE=1 HEXALITH_DAPR_HOME=<tool-input-directory.txt> HEXALITH_PLATFORM_CONCURRENT_EVIDENCE=<this-directory>/live-concurrent-final dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class '*PlatformConcurrentRunTests'
HEXALITH_PLATFORM_OCCUPIED_PORT_LIVE=1 dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -method '*OccupiedSentryPortReportsBindingFailureAndPreservesForeignListenerAsync'
HEXALITH_PLATFORM_LIVE_SMOKE=1 HEXALITH_DAPR_HOME=<tool-input-directory.txt> dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -method '*ReferenceServersReachDeclaredReadinessInTheFileEntryAsync'
```

## Revisions, privacy and cleanup

The workspace advanced independently during implementation. Exact full revisions
are in each live attempt's `revisions.txt`, with later root revisions/dirty status
recorded alongside this index. No gitlinks were staged or identity checks relaxed.
The default installed home had only `daprd`; the first attempt correctly failed
`HXR013`. Cached catalog `daprio/dapr:1.18.7` supplied placement/scheduler binaries
to a task-owned external tool directory; no catalog/tool pins changed and no tools
were published. See `versions.txt`, `tool-input-directory.txt` and
`tool-preparation.log`. Extraction containers were removed.

Attempt 2 reached both readiness receipts but failed in its inspection harness
because full Docker inspect exceeded bounded output capture. Its incomplete full
inventory was discarded. The final harness records only ID/image/mount/port fields;
no environment values or private-key material is retained. Certificate identities
are compared by hashes in memory. CLI observations omit environment fields and
login URLs. For CLI diagnostics only, a project marker was written inside each
owned run's private AppHost directory so Aspire could select the existing process;
no checkout or application model changed.

Final cleanup removed both owned run workspaces and labelled resources. The
pre-existing Tenants AppHost remains running; see `after-applications.json`.


## Review corrections

This directory explicitly retains its redacted `.log` receipts through its local
`.gitignore`, so the linked passing and failing evidence is visible in review.
The original concurrent pass captured persisted and Docker-bound ports. Review
adds metadata-only receipts for every actual native HTTP, gRPC, internal gRPC and
metrics listener; the next full verification must collect that expanded inventory.
Scoped review checks cover diagnostic retention through real service disposal,
all mapped checkout exclusions, certificate cleanup ownership, aggregate cleanup
failures, native-listener inventory and the existing Works qualification adapter.
They do not expand the Works live qualification claim.


Review-fix verification: [13 focused C# cases passed](review-focused-tests.log)
with no failures or skips (31.470 seconds), including registered-service failure
disposal, shared certificate-parent cleanup, mapped owner/symlink exclusions,
every-owned-session cleanup, listener inventory, and Works callback-time Sentry
resolution. [Eight adapter cases passed](review-adapter-tests.log), including the
exact bound native endpoint and foreign-endpoint refusal. The supplemental
[library](review-library-build.log) and [test](review-tests-build.log) builds passed
with `--no-restore -p:BuildProjectReferences=false -p:RunAnalyzers=false`; each
reported zero warnings/errors. Full normal-build, ordinary-suite, and live/file-entry
rechecks remain assigned to the parent verification pass.
