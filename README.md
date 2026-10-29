# Hexalith.Platform

Platform-owned Aspire host for Hexalith domain modules.

This repository is the **EXT-HOST-1** delivery target for Hexalith Agents: it owns
production-like composition of the Agents DomainService and FrontComposer UI with
EventStore, Conversations, Parties, Tenants, Provider and safety adapters, Dapr
Workflow, secrets, health, identity, telemetry, and evidence ingress.

Domain modules such as [Hexalith.Agents](https://github.com/Hexalith/Hexalith.Agents)
must **not** ship module-owned `AppHost`, `Aspire`, or `ServiceDefaults` projects.
Those hosting concerns live here.

## Status

| Milestone | State |
| --- | --- |
| Repository + empty Aspire AppHost scaffold | Current |
| Works development topology | Opt-in preview; AD-20 parity gate open |
| Agents DomainService / UI composition wiring | Planned (Agents Story 5.6) |
| Live Level 4/5 compatibility evidence | Planned (`Available` gate) |

## Requirements

- .NET SDK `10.0.402` or later (`latestPatch` roll-forward)
- Aspire CLI `13.6.4` exactly for the prepared tool below. The module tool compares `aspire --version` with
  the catalog-selected `Aspire.AppHost.Sdk` and refuses any other version with
  `HXR015`.

## Module tool

From the Platform root, restore and check its exact local tool pin:

```bash
dotnet tool restore
dotnet tool run hexalith-module --version
```

The local tool manifest prepares `hexalith.builds.module.cli` `4.38.2`, and
`hexalith-platform.json` prepares the matching floor `[4.38.2, 5.0.0-0)`.
This is a local preparation candidate. Platform integration qualification,
recording the exact Builds gitlink, publication of the resulting Platform identity
and consumer adoption remain pending. The historical accepted `4.35.2` evidence
below does not qualify this candidate for `run`, `debug` or package composition.

[Builds release `v4.38.2`](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v4.38.2)
identifies exact source `2574bf2f3da5367d8801f0d7661d66155e5d90b0`. Its official
qualification inventory and signed NuGet archive were verified for Story 6.4.
The corresponding Platform Builds gitlink patch is prepared separately and remains
unapplied. This release preserves the current Projects catalog, including
Conversations `1.3.1` and EventStore `3.128.4`; its embedded runtime catalog selects
Aspire `13.6.4`, Dapr CLI `1.18.2` and Dapr runtime `1.18.7`. The direct file AppHost
also selects Aspire `13.6.4`; that version agreement alone is not live integration
acceptance. No runtime prerequisite was installed or deployment performed.

The configured range permits later 4.x versions, including prereleases, and excludes
5.0 prereleases. Each accepted tool carries its own catalog; matching the range
alone does not establish integration qualification. The exact tool pin and Builds
gitlink must identify the same published release before acceptance.
See the [Story 1.13 evidence](_bmad-output/implementation-artifacts/evidence/1-13/README.md)
for owner approval, gated publication and fresh released-tool acceptance. The
[Story 1.11 evidence](_bmad-output/implementation-artifacts/evidence/1-11/README.md)
records McpCli, EventStore and Memories verification with `4.34.0`; the
[Story 1.10 evidence](_bmad-output/implementation-artifacts/evidence/1-10/README.md)
records the earlier EventStore and Memories proof with `4.33.1`.

Versions below the prepared range, including `4.38.1`, are refused by its range
check once that Platform identity is recorded. Version `4.34.0` first built McpCli; `4.33.1` composed only
servers. The earlier `4.33.0` also had a mismatched packaged Tooling identity and build-machine paths.
From `4.31.0`, a tool outside the range is refused with `HXW010` in package mode
and warned in source mode. Older tools do not check the range, so run the pinned
tool through `dotnet tool run`.

The canonical [candidate evidence and attachment policy v1](docs/contracts/candidate-and-attachment-policy-v1.md)
fixes committed PR merge-result evidence, prohibits reuse across decisions and
sets finite attachment holds. This publishes Platform policy for downstream
descriptor and runner adoption; it does not establish implemented support or
change the accepted tool pin. Accepting attachments under it requires Builds
adoption; accepting evidence also requires a Platform-accepted, integration-qualified tool.

The versioned [test environment and terminal result contracts](docs/contracts/test-environment-v1.md)
publish strict, offline-readable schemas for runner endpoints, composition identity,
reserved candidate proof, attachment holds and the four terminal outcomes. Builds
ships the schemas and consumer reader; lifecycle and evidence adoption remain
downstream responsibilities.

## Parties reference domain

The qualified independent `Hexalith.Parties` checkout pins module tool `4.34.0`
against its own direct Platform gitlink `17179e5`, whose accepted range is
`[4.34.0, 5.0.0-0)`. That historical qualification does not change this checkout's
current minimum above. From that Parties checkout:

```bash
dotnet tool restore
dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json
```

Parties declares EventStore, Tenants and Memories, giving seven ready servers
including `parties`; source and Debug bindings use only the tool-selected direct
references. Platform routes the singular `party` domain to `parties/process`,
binds the existing state/pubsub keys and renders the declarative tenant subscription.
Rich Parties search stays disabled. Legacy domain AppHost, Aspire, UI/MCP and sample
hosts remain frozen outside this qualification. Owner review and upstream
integration are complete; the [Story 1.12 evidence](_bmad-output/implementation-artifacts/evidence/1-12/README.md)
records that qualification and its exact revisions. Story 1.12 predates the
enrollment kit: Parties is not enrolled under `validate --adoption`. Its current
checkout still has programmatic subscription discovery, literal Dapr component
names, a missing hosting inventory and the older pin. Those changes belong to
Parties' owner-led enrollment; see the
[Story 1.13 findings](_bmad-output/implementation-artifacts/spec-1-13-publish-the-domain-module-enrollment-kit.md#review-findings).

## Domain-module enrollment

Use the [enrollment guide](docs/domain-module-enrollment.md) and
[starter templates](templates/domain-module-enrollment/) for owner-led declaration,
direct-only initialization, source mapping, Dapr conventions, hosting inventory
and human SM-2 evidence. The accepted `4.35.2` tool provides read-only
`validate --adoption` reports in human and JSON formats. Static reports leave
SM-2 for the module owner to demonstrate. See
[Story 1.13 evidence](_bmad-output/implementation-artifacts/evidence/1-13/README.md).

## Run and debug a technical module

EventStore, Memories and McpCli run and debug from this workspace, never from
their own repository. Each command takes the owner manifest in the module's
checkout under `references/`; the tool loads every owner manifest itself, so
never pass the copies in `src/Hexalith.Platform.Composition/Declarations/` (that
duplicates a module and fails with `HXP003`/`HXP020`).

| Module | Manifest | Platform runs |
| --- | --- | --- |
| EventStore | `references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json` | `eventstore` |
| Memories | `references/Hexalith.Memories/module/hexalith.module-manifest.v2.json` | `memories`, `memories-access-telemetry-clock`, `memories-access-telemetry`, `eventstore`, plus Redis Stack (`memories-vectors`) and FalkorDB (`memories-graph`); no Tenants |
| McpCli | `references/Hexalith.McpCli/module/hexalith.module-manifest.v2.json` | `eventstore` (the gateway McpCli targets), plus a run-scoped Debug build of `references/Hexalith.McpCli`; McpCli declares no server or interface |

### Prerequisites

- The pinned tool (`dotnet tool restore`), the .NET SDK and Aspire CLI
  `13.6.4` listed above, and a running Docker engine. This candidate's integration
  qualification remains pending.
- The direct submodules `references/Hexalith.Builds`, `references/Hexalith.EventStore`,
  `references/Hexalith.Tenants` and `references/Hexalith.Memories`, initialized
  without `--recursive`. The tool reads all three declaration owners even for
  EventStore alone; the selected resources remain those in the table above.
  McpCli also needs `references/Hexalith.McpCli`, with its own nested
  `references/*` left uninitialized (`HXW003` refuses any initialized nested
  submodule).
  Each checkout's `HEAD` must equal the gitlink staged in this
  repository. After a local commit inside a submodule, stage it with
  `git add references/<Repository>` or the tool refuses the run.
- The local Aspire orchestration and dashboard packages at `13.6.4`. From this
  workspace, run `dotnet restore apphost.cs` once before the first `run` or `debug`.
  Tool restore alone does not install these SDK packages. If NuGet uses a custom
  `globalPackagesFolder`, set `NUGET_PACKAGES` to that absolute folder for both
  restore and `debug`; the tool otherwise checks `~/.nuget/packages` and reports
  `HXR016` when the required packages are missing.
- `HEXALITH_DAPR_HOME`: an absolute, isolated Dapr home with Dapr CLI `1.18.2`
  at `tools/dapr` and runtime `1.18.7` at `.dapr/bin/{daprd,placement,scheduler}`.
  A machine-wide `dapr init` does not qualify. On Linux x64 (including WSL2),
  prepare one from the official release assets; other platforms need the
  matching `<os>_<arch>` assets of the same releases. The subshell stops at the
  first failed download or checksum, removes its downloads and leaves your shell
  where it was:

  ```bash
  (
    set -euo pipefail
    dapr_home="$HOME/.hexalith/dapr/1.18.7"
    mkdir -p "$dapr_home/tools" "$dapr_home/.dapr/bin"
    downloads="$(mktemp -d)"
    trap 'rm -rf "$downloads"' EXIT
    cd "$downloads"
    base=https://github.com/dapr
    for asset in cli/releases/download/v1.18.2/dapr_linux_amd64.tar.gz \
        dapr/releases/download/v1.18.7/daprd_linux_amd64.tar.gz \
        dapr/releases/download/v1.18.7/placement_linux_amd64.tar.gz \
        dapr/releases/download/v1.18.7/scheduler_linux_amd64.tar.gz; do
      curl -fsSLO "$base/$asset"
      curl -fsSLO "$base/$asset.sha256"
    done
    sha256sum -c ./*.sha256
    tar -xzf dapr_linux_amd64.tar.gz -C "$dapr_home/tools"
    for binary in daprd placement scheduler; do
      tar -xzf "${binary}_linux_amd64.tar.gz" -C "$dapr_home/.dapr/bin"
    done
    DAPR_RUNTIME_PATH="$dapr_home" "$dapr_home/tools/dapr" --version   # CLI 1.18.2, runtime 1.18.7
  )
  ```

### Commands

From the Platform root:

```bash
export HEXALITH_DAPR_HOME="$HOME/.hexalith/dapr/1.18.2"

# EventStore alone
dotnet tool run hexalith-module -- debug --manifest references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json
dotnet tool run hexalith-module -- down --manifest references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json

# Memories with its EventStore dependency
dotnet tool run hexalith-module -- debug --manifest references/Hexalith.Memories/module/hexalith.module-manifest.v2.json
dotnet tool run hexalith-module -- down --manifest references/Hexalith.Memories/module/hexalith.module-manifest.v2.json

# McpCli with the EventStore gateway it targets
dotnet tool run hexalith-module -- debug --manifest references/Hexalith.McpCli/module/hexalith.module-manifest.v2.json
dotnet tool run hexalith-module -- down --manifest references/Hexalith.McpCli/module/hexalith.module-manifest.v2.json
```

`debug` builds the active module and its root-declared dependencies from their
`references/` checkouts in Debug, waits for each server's declared `/ready`
endpoint (10-minute default deadline), and reports one process id per project.
`run` starts the same composition without reporting process ids. `down` stops
only the resources tagged with that run. Each signed runner plan and direct file
AppHost allocates its own Sentry, placement, scheduler and sidecar listeners;
persisted runner ports are honored. Each run owns its Redis, scheduler data,
Memories vector/graph data, issuer credentials, secrets and SQLite discovery registry
outside the checkout. Allocation probes release their sockets before startup;
a competing bind fails startup instead of selecting shared resources. A second
concurrent direct file-entry run needs `aspire run --isolated` (or
`aspire start --isolated`) because `apphost.run.json` fixes Aspire's own
dashboard, OTLP and resource-service ports.

Set `HEXALITH_DAPR_HOME` to an installation containing the catalog-pinned CLI at
`tools/dapr` and native runtime at `.dapr/bin/daprd` (plus placement/scheduler for
Builds prerequisite checks). Installed binaries and component templates can be
shared read-only. Ordinary file-entry and runner `debug`/`run` project launches
use the checkout's shared developer build outputs (`bin`, `obj` and configured
`artifacts` paths); concurrent builds can contend there. Runner test suites select
private project outputs under their run workspace. Build outputs are developer
inputs to composition; the runtime data and credentials listed above are run-owned.
The file entry also accepts the user's installed native runtime
when no home is selected. An unavailable selected runtime fails startup.
`Dapr:Mtls:CertificateDirectory` selects an external parent for a private child per
run; checkout paths, including symlinks into the checkout, are rejected.

Reference compositions start their own Redis, never consume dapr-init Redis or
acquire exported fixture locks, and instantiate no Keycloak. Legacy technical
own-repository fixtures are outside Platform integration evidence; descriptor
migration is Story 2.9. Works remains an opt-in development preview with its existing
qualification scope. See [concurrency verification](docs/platform-apphost-form.md)
for the gated two-session proof and its current evidence limits.

McpCli is a short-lived CLI, not a server. For the McpCli manifest, `run` and
`debug` also build `references/Hexalith.McpCli/src/Hexalith.McpCli`
in Debug into the run workspace, using the run's pinned Builds catalog. `test`
needs `--profile` naming a declared test; the McpCli declaration declares none,
so `test` refuses it (`HXC002`) and builds nothing. The
build never packs or publishes the tool and never writes the checkout's `bin`
or `obj`. When the environment is ready, an `HXI010` diagnostic with field
`mcpcli` names the built tool,
`${XDG_DATA_HOME:-$HOME/.local/share}/hexalith-builds/g4-workspaces/<runId>/tools/mcpcli/bin/Hexalith.McpCli/debug/Hexalith.McpCli.dll`,
and the checkout project it was built from; `down` deletes it with the run
workspace. A missing checkout or tool project fails with `HXW004`, and a failed
build with `HXR020`, before any resource starts. Both name McpCli and the
expected project path; no other copy, such as a sibling `../mcpcli` checkout,
is used. Package mode builds no McpCli; package-mode McpCli arrives in Epic 3.
The started EventStore gateway is the target of connected execution, which also
arrives in Epic 3; the debugger recipe below uses offline verbs only. The build
first shipped in `4.34.0`; use the current accepted range above. The earlier `4.33.1`
composes only servers and builds no McpCli; it is outside the range, so it is
warned (`HXW010`) in source mode and refused in package mode.

`debug` does not print the run id. It is the file name of the run state
`<runId>.json` under `${XDG_DATA_HOME:-$HOME/.local/share}/hexalith-builds/g4-runs/`
on Linux. Inspect the state files and select the one whose `projectProcessIds`
contains the launcher id reported by this `debug` invocation. Confirm its
`manifestHash` matches `sha256sum <manifest>` (ignoring hexadecimal letter case),
then use its `runId`. File modification time alone cannot identify your run.
When an earlier run used the same manifest, `down --manifest` refuses with
`HXR024`; pass the id as `down --manifest <manifest> --run-id <runId>`.
An explicit `--run-id` chooses that run directly; the tool does not cross-check
its manifest, so verify both fields before stopping it.

### Attach a debugger

The reported process id is the `dotnet run` launcher, not the application. The
application is the launcher's child process:

```bash
ps --ppid <launcher-pid> -o pid,args
```

Attach to the child whose command is the module's
`bin/Debug/net10.0/<Assembly>` executable, for example with VS Code
("Attach to a .NET process"). Breakpoints bind to the sources in
`references/<Repository>`. Stay attached until you finish, then stop the run
with `down`.

### Launch McpCli under a debugger

McpCli exits after each command, so launch the reported DLL under the debugger
instead of attaching. Offline discovery verbs (`modules`, `operations`,
`describe`) need no gateway URL. With [netcoredbg](https://github.com/Samsung/netcoredbg)
(MIT; verified with 3.2.0; download it outside the repository):

```bash
tool="<path reported by HXI010>"
netcoredbg --interpreter=cli -- dotnet "$tool" modules
# at the ncdb prompt:
#   break Hexalith.McpCli.Core.Catalog.CatalogService.ListModules
#   run
```

netcoredbg first reports the breakpoint as `--pending`, because the McpCli
assemblies are not loaded yet; it binds once `Hexalith.McpCli.Core` loads, and
`run` then stops in `ListModules`. A `file:line` breakpoint, such as the
absolute path of `CatalogService.cs` under `references/Hexalith.McpCli`, works
the same way.

In VS Code, use a `coreclr` launch configuration whose `program` is the
reported DLL and whose `args` are the McpCli arguments. Breakpoints bind to the
sources in `references/Hexalith.McpCli`. Until modules are enrolled, `modules`
returns `catalog_empty` with exit code `2`. The run-id directory in the
reported path is the `runId` for `down --run-id`.

### Own-repository AppHosts

A technical module's own AppHost, such as `src/Hexalith.Memories.AppHost` or
`src/Hexalith.EventStore.AppHost`, serves only that repository's own tests. A
run of it is never evidence that the module runs in the Platform composition;
only the commands above, from this workspace, are. McpCli has no AppHost: its
own `references/Hexalith.Platform` and any run from its repository root serve
only its own tests, and such a run is never Platform evidence.

## Verify Agents host contract (clean checkout)

From a clean clone of this repository:

```bash
./eng/verify-agents-host.sh
```

Expected: Release build of the platform AppHost succeeds, and the repository
declares itself as the Agents platform host without requiring any Agents-owned
hosting projects.

## Local run

```bash
aspire run
```

The Works migration topology is available when the sibling `works` checkout is
present. Enable it explicitly for local development:

```bash
Platform__Works__Enabled=true aspire run
```

The Works topology is a development preview while the AD-20 R1–R11 parity and
rollback gate remains open. The existing Works AppHost remains the supported
rollback composition until that gate passes.

## Owner

Platform Maintainer (Hexalith).
