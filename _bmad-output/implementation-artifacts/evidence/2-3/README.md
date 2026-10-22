# Story 2.3 verification

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
