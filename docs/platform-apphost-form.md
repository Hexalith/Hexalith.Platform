Platform keeps `apphost.cs` as its thin file AppHost. It references
`src/Hexalith.Platform.Composition` with `#:project`. Agents retains its explicit
refusal, Works remains an exclusive opt-in preview, and the default branch calls
the same declaration model as the Builds runner.

`PlatformAppModel.Prepare` validates the whole declaration set and resolves all
enabled servers, full configuration keys, project or image identities, readiness
checks and executable task metadata before adding resources. Source composition
uses the command's single mapping and Debug projects. Package composition uses
Platform's owner snapshots, verifies their source provenance, and uses released
images at catalog versions. Builds.Module.EventStoreHost is the sole package-mode
gateway seed; its packaging contract remains in Builds.

Builds v2 plans carry their declaration selection, immutable mapping and the model
assembly built from the pinned Platform checkout. The Builds AppHost loads that
model and retains its stop, readiness and run-scoped UiHost protocols. Legacy v1
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
`DaprComponents/reference`. Component rebinding belongs to Story 1.7; extension
packages and startup deadline policy remain outside this change.

The shared model applies allocated Dapr listener ports and server-scoped tokens to
the actual generated CLI processes. Gateway configuration is rendered separately
from owner files so only selected server registrations remain. A new local Redis
store requires EventStore's existing operator writer-protocol activation: the
file model runs that bounded operation concurrently with startup, while signed
Builds plans retain the runner's operation. Neither path bypasses `/ready`.
