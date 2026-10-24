Platform keeps `apphost.cs` as its thin file AppHost. It references
`src/Hexalith.Platform.Composition` with `#:project`. Agents retains its explicit
refusal, Works remains an exclusive opt-in preview, and the default branch calls
the same declaration model as the Builds runner.

The bootstrap resolves `HexalithBuildsRoot` once for its catalog and tooling
project. An existing sibling `Hexalith.Builds` checkout takes precedence, matching
the workspace catalog convention; otherwise it uses the repository's existing
`references/Hexalith.Builds` checkout. An explicit MSBuild property selects both;
relative overrides resolve against the Platform root for every consuming project.
Normalization runs after Builds applies `CustomAfterMicrosoftCommonProps`, so a
command-selected mapping controls the catalog, tooling and test seed together.
Missing dependencies fail without initializing nested submodules. The pin verifier
uses the same default precedence; pass `--catalog` for an explicit root override.
The runner's `BuildsToolingAssemblyPath` continues to select its own tooling binary.
File-AppHost SDK and package directives must still match the chosen Builds catalog.
Run `python3 -m unittest discover -s eng -p 'test_host_builds_resolution.py' -v`
and `python3 -m unittest discover -s eng -p 'test_verify_platform_pins.py' -v`
to verify bootstrap resolution and pin refusal without starting services.
This resolves build-time inputs only. Runtime package image-version lookup and
the opt-in Builds live smoke still have nested checkout assumptions; neither is
qualified for this sibling-only workspace by a passing bootstrap build. Normal
workspace admission still requires the selected Platform release identity,
Builds pin, tool range and declaration mapping before resources are added.

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
six-server reference environment is selected. `Platform:DeclarationPath` supplies
a declaration to validate; `Platform:ActiveModule` must name it to select it.
Without that selection, the six-server reference environment is selected. When no
source mapping path is supplied, the file entry resolves the mapping from the
explicit declaration's workspace, or anchors on `hexalith-platform.json` in the
AppHost directory when no declaration is supplied. Builds resolves the outermost
workspace and its direct references once; owner declarations come from that mapping,
without assuming references nested inside Platform. That workspace must map EventStore, Tenants and Memories
for the reference environment; missing owners refuse by identity before resources
are added.
`Platform:SourceMapping:Path` and
`Platform:SourceMapping:Hash` select the verified source mapping, or the file entry
uses `HEXALITH_SOURCE_MAPPING_PATH` and `HEXALITH_SOURCE_MAPPING_HASH`. The optional
`Platform:EventStoreSeedProject` names the package gateway seed project. The file entry requires a Development host environment. Platform supplies local mTLS Sentry, placement and scheduler at Builds catalog pins; sidecars use the same control-plane trust domain (`localhost`) and namespace (`default`); ACLs match self-hosted workload identities in `public`. Signed Builds plans retain runner lifecycle ownership and use the same authenticated control plane.

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
runner when the installed .NET SDK rejects the legacy VSTest target. The ordinary
suite builds the mapped Memories server in Debug before validating its owner
option types; no separately prebuilt owner DLL is required. Set
`HEXALITH_PLATFORM_LIVE_SMOKE=1` and select
`-method '*ReferenceServersReachDeclaredReadinessInTheFileEntryAsync'` to run the
Docker/Dapr file-entry smoke. Run
`eng/verify-agents-host.sh --mode LocalScaffold` for the file entry and pin check.
`eng/verify-platform-pins.py` names both versions when the SDK, EventStore.Aspire
or Dapr toolkit file pin differs from Builds.

For reproducible file-entry qualification, also set `HEXALITH_PLATFORM_PREBUILD_SOURCE=1`.
The gated test resolves one verified source mapping, builds each selected owner project
sequentially, then starts the real file entry with that mapping. Retain the build output
with `HEXALITH_PLATFORM_SOURCE_PREBUILD_LOG`. Cold default startup can contend for shared
owner build outputs when several project launches compile the same reference concurrently;
the sequential-prebuild result does not qualify that cold startup path.

Set `HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1` and `HEXALITH_DAPR_HOME` to a
catalog-pinned Dapr installation (CLI and runtime). `HEXALITH_ASPIRE_CLI` may name an isolated catalog-matching Aspire executable. Then select `-class '*BuildsSourceStartupTests'`
to exercise the real Builds engine for EventStore and the full Tenants domain
environment. These tests check the single resolved mapping, selected resources,
declared application readiness, authenticated sidecar outbound readiness and complete teardown. The runner grants no invocation access to readiness routes. The separate gated `LiveLocalDaprEnforcesInvocationAndDeclarativeDeadLetterDeliveryAsync` fixture proves allowed/denied invocation and scoped subscription/dead-letter delivery.

Tenants API dependency readiness uses the resolved gateway HTTP health endpoint through
`EventStore:GatewayReadiness:BaseAddress`. It still probes the gateway's `/ready` result;
this requires no invocation permission. Without that option, isolated Tenants retains
its existing Dapr readiness path.

Local Dapr conventions are ratified in architecture AD-9. The canonical hosting set is
`Hexalith.EventStore.Aspire`, `Hexalith.EventStore.ServiceDefaults` and
`CommunityToolkit.Aspire.Hosting.Dapr`; Commons provides supporting primitives.
New hosting helpers must fit this recorded set, and Builds owns every version.
Evaluate admission with `python3 eng/verify-platform-pins.py --hosting-helper PACKAGE`.
Optional v2 capability `servers` are owner server IDs. An explicit topic
`subscription` supplies its `server` and delivery `route`; optional `topic` names the
physical Dapr topic or pattern, defaulting to the inventory name. `deadLetter.topic`
references a same-role inventory name; its physical target must be concrete, unique
and distinct from the source. A receiver, resolved pubsub component and physical
topic identify one subscription; duplicate identities are refused. Publisher
inventories create no subscriptions. Interface `invocationRules` supply `caller`, `path` and
`verbs`; missing rules grant no invocation. Platform resolves names once, injects
actual owner configuration keys, scopes components to selected consumers, renders
one deny-default mTLS policy per receiver and disables programmatic subscription
discovery in declarative mode. The actual CLI resource paths include a dedicated
subscription directory, with the receiver scopes retained. Shared state readers and isolated telemetry state
are preserved. Hosted policy remains later work; run resource isolation applies to signed runner plans and the direct file AppHost.

The shared model applies allocated Dapr listener ports and server-scoped tokens to
the actual generated CLI processes. Gateway configuration is rendered separately
from owner files so only selected server registrations remain. A new local Redis
store requires EventStore's existing operator writer-protocol activation: the
file model runs that bounded operation concurrently with startup, while signed
Builds plans retain the runner's operation. Neither path bypasses `/ready`.

The reference owner declarations explicitly retain Memories `memories-events` delivery
at `/events/ingest` and EventStore projection notification pattern
`*.*.projection-changed` at `/projections/changed`. The EventStore gateway grants
`tenants-api` POST access to `/api/v1/commands` and `/api/v1/queries`, as used by the
owner gateway client. It grants no readiness invocation permission. The existing
projection pattern does not establish Redis wildcard fan-out: the pinned Redis
component reads a stream using the requested topic value, so concrete projection
notification delivery is a separate provider limitation, not a qualified result of
this story's rendering or startup tests.

Local reference runs use run-owned ports, private issuer credentials, SQLite name
resolution, secrets, Redis data, scheduler data, and Memories vector/graph data.
Sentry/placement/scheduler gRPC endpoints bind directly to the allocated host
ports, including under `aspire start --isolated`. This keeps native sidecar
addresses and scheduler advertisements aligned with the actual listeners.
Container target ports and mTLS trust domains remain unchanged. Every receiver
retains deny-default ACLs and selected component scopes.
Allocated host addresses reach both rendered policy and the actual native `daprd`
command. Native startup avoids the Dapr CLI's fixed Sentry-address behavior.
Selected binaries and source templates are read-only shared inputs. Ordinary
file-entry and runner `debug`/`run` launches use shared developer build outputs in
the checkout (`bin`, `obj` and configured `artifacts` paths), which can be updated
by compilation and contend across launches. Runner test suites put project outputs
under `<run-workspace>/project-builds/<resource>`; the retention experiment keeps
its existing owner `artifacts/retention/<run-id>/build/<resource>` placement.
Build outputs are inputs to the composition; run-owned runtime state is listed above.
Allocation probes release sockets before startup and are not cross-process
reservations; bind conflicts fail and must leave foreign resources alone.
A second concurrent direct file-entry run needs `--isolated` (`aspire run --isolated`
or `aspire start --isolated`): `apphost.run.json` fixes Aspire's own dashboard, OTLP
and resource-service ports.

`Dapr:Mtls:CertificateDirectory` is an external parent, with one private child per
run. Paths resolving through symlinks into Platform or a mapped owner checkout fail
before resources are added. Missing native runtime reports its selected path and refuses
startup. A failed composition that writes `composition-failure.txt` retains its
private file-entry workspace at `<system-temp>/hexalith-platform/<random-run-id>/`,
including rendered configuration, discovery, data, secrets and default issuer
credentials. The selected sidecar configuration path identifies that workspace.
A failed Works preview start likewise retains
`<system-temp>/hexalith-works-preview/<random-run-id>/`, including its default issuer
credentials and scheduler data.
After its owned AppHost has stopped and diagnostics have been inspected, remove
only that run directory (for example, `rm -rf -- /tmp/hexalith-platform/<random-run-id>`
when the system temporary directory is `/tmp`). An explicitly configured certificate
parent holds a separate private issuer child; normal host disposal removes that child,
while forced-termination cleanup remains runner lifecycle work. Explicit runner
teardown and file-host disposal clean only owned children and data.

Earlier versions used the shared issuer directory `~/.dapr/certs/hexalith-platform`
and the Docker volume `hexalith-platform-dapr-scheduler`. Neither is used any more.
After stopping any older AppHost, remove them manually if they are no longer needed
(`docker volume rm hexalith-platform-dapr-scheduler`). A
`Dapr:Mtls:CertificateDirectory` value that still names the old issuer directory is
now treated as a parent: each run creates a new private issuer child beneath it.
Reference runs create Redis themselves; they do not use dapr-init Redis, exported
fixture locks or Keycloak. Legacy technical module fixtures remain outside Platform
integration evidence; descriptor migration is Story 2.9. Works uses the revised
shared allocation helper and remains a development preview.

Build each test project in Debug with `-m:1`, then run the xUnit executable:

```bash
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1
dotnet build references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1
dotnet build references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/Hexalith.Builds.Module.AppHost.csproj -c Debug -m:1
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class '*PlatformAppModelTests'
HEXALITH_PLATFORM_CONCURRENT_LIVE=1 HEXALITH_DAPR_HOME=/absolute/pinned-dapr-home dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class '*PlatformConcurrentRunTests'
```

The concurrent proof creates two Builds sessions selecting Tenants' six-server
reference closure. It refreshes declared readiness for both sessions while they coexist,
then checks disjoint listeners/container IDs,
private discovery and certificate identities, different persisted values under
identical keys in Redis/vector/graph stores, and fresh declared survivor readiness
after peer teardown. It retains revisions, startup/readiness and resource inventories,
persisted values and cleanup results under
`_bmad-output/implementation-artifacts/evidence/2-3/` (override with
`HEXALITH_PLATFORM_CONCURRENT_EVIDENCE`). It requires Docker, catalog-pinned Aspire,
Dapr CLI/native runtime, a built Debug Builds AppHost, and a source mapping whose
checkout revisions match the staged root gitlinks. Identity mismatches are live
blockers; no model-only or skipped result qualifies concurrent readiness. Do not
stage independent gitlinks to manufacture a passing mapping.

For the bounded occupied-listener matrix case, set
`HEXALITH_PLATFORM_OCCUPIED_PORT_LIVE=1` and select
`-method '*OccupiedSentryPortReportsBindingFailureAndPreservesForeignListenerAsync'`.
It holds a separately labelled Redis listener, attempts the model's actual Sentry
Docker binding, records the refusal, then verifies the listener and its marker
survive candidate-only cleanup. Fixture teardown removes both task-created
containers only after checking their separate run labels. This proves refusal at
the Docker publish boundary and preservation of the separately owned listener;
it does not exercise an AppHost startup failure or force a normal allocator to
select an occupied port. Real allocation probes skip occupied ports and release
their sockets before binding. The proof writes raw Docker output and cleanup logs
to the fixed `_bmad-output/implementation-artifacts/evidence/2-3/occupied-port/`
directory; `HEXALITH_PLATFORM_CONCURRENT_EVIDENCE` does not redirect these files.
