Platform keeps `apphost.cs` as its thin file AppHost. It references
`src/Hexalith.Platform.Composition` with `#:project`. Agents retains its explicit
refusal, Works remains an exclusive opt-in preview, and the default branch calls
the same declaration model as the Builds runner.

`PlatformAppModel.Prepare` validates the whole declaration set and resolves all
enabled servers, full configuration keys, project or image identities, readiness
checks and executable task metadata before adding resources. Source composition
uses the command's single mapping and Debug projects. Package composition uses
Platform's owner snapshots and uses released images at catalog versions. Each
snapshot's provenance commit must equal the release tag commit selected by the
Builds catalog. The current v2 owner declarations were added after those releases,
so package composition refuses until the owners release matching declarations and
Platform updates its snapshots and release commit pins. Builds.Module.EventStoreHost
is the sole package-mode gateway seed; its packaging contract remains in Builds.

The file entry reads `Platform:ActiveModule` to choose a module; without it, the
six-server reference environment is selected. `Platform:DeclarationPath` adds an
active declaration. `Platform:SourceMapping:Path` and
`Platform:SourceMapping:Hash` select the verified source mapping, or the file entry
uses `HEXALITH_SOURCE_MAPPING_PATH` and `HEXALITH_SOURCE_MAPPING_HASH`. The optional
`Platform:EventStoreSeedProject` names the package gateway seed project. The file
entry requires a Development host environment and a local `dapr init` placement
and scheduler. Signed Builds plans supply their own run-scoped placement and
scheduler.

Builds v2 plans carry their declaration selection and content hash, immutable mapping and the model
assembly built from the pinned Platform checkout. The Builds AppHost loads that
model only while the declaration still matches the selected hash, and retains its stop, readiness and run-scoped UiHost protocols. Legacy v1
plans continue through the existing topology. A v2 declaration currently contains
no v1 qualification profile, so the runner never reports a readiness-only run as
a completed test profile.

A file AppHost has no generated `Projects` type that can be passed to
`DistributedApplicationTestingBuilder`. Test the ordinary C# model directly,
inspect its resource annotations and readiness checks, and smoke-test the actual
file entry separately. Do not claim a generated file-entry test-builder type.

Build `tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug`,
then run its `bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll` with `dotnet`
for the selection, binding and atomic-refusal matrix. This uses the xUnit executable
runner when the installed .NET SDK rejects the legacy VSTest target. Set
`HEXALITH_PLATFORM_LIVE_SMOKE=1` and select
`-method '*ReferenceServersReachDeclaredReadinessInTheFileEntryAsync'` to run the
Docker/Dapr file-entry smoke. Run
`eng/verify-agents-host.sh --mode LocalScaffold` for the file entry and pin check.
`eng/verify-platform-pins.py` names both versions when the SDK, EventStore.Aspire
or Dapr toolkit file pin differs from Builds.

Set `HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1` and `HEXALITH_DAPR_HOME` to a
catalog-pinned Dapr installation, then select `-class '*BuildsSourceStartupTests'`
to exercise the real Builds engine for EventStore and the full Tenants domain
environment. These tests check the single resolved mapping, selected resources,
run-scoped Dapr invocation readiness and complete teardown.

Shared local Dapr components retain their existing literal names under
`DaprComponents/reference`. EventStore's rate-limit configuration uses its
`configstore` component; Memories' secret and conversation components are attached
only to Memories sidecars. Component rebinding belongs to Story 1.7; extension
packages and startup deadline policy remain outside this change.

The shared model applies allocated Dapr listener ports and server-scoped tokens to
the actual generated CLI processes. Gateway configuration is rendered separately
from owner files so only selected server registrations remain. A new local Redis
store requires EventStore's existing operator writer-protocol activation: the
file model runs that bounded operation concurrently with startup, while signed
Builds plans retain the runner's operation. Neither path bypasses `/ready`.
