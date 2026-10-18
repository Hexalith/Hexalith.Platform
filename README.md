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

- .NET SDK `10.0.401` or later (`latestPatch` roll-forward)
- Aspire CLI `13.6.1` exactly. The module tool compares `aspire --version` with
  the catalog-selected `Aspire.AppHost.Sdk` and refuses any other version with
  `HXR015`.

## Module tool

The first Platform-accepted `hexalith-module` version is `4.33.0`, published as
`hexalith.builds.module.cli`. The local tool manifest pins that exact version.
`hexalith-platform.json` accepts the range `[4.33.0, 5.0.0-0)`: later 4.x
releases, prereleases included, are accepted without a Platform edit, and 5.0
prereleases are excluded. The catalog commit is
`544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`, matching both the Builds gitlink
and release tag `v4.33.0`. Only the pinned version is guaranteed to embed that
catalog. Another accepted 4.x tool brings its own embedded Dapr, Redis and
AppHost SDK pins, and nothing compares them with the catalog yet. When the
exact pin moves, the Builds gitlink moves to that version's release-tag commit.

Every version below `4.33.0` is not Platform-accepted, including the published
`4.20.0–4.27.4` and `4.29.1–4.32.0` cohorts. From `4.31.0`, a tool outside the
range is refused with `HXW010` in package mode and warned in source mode.
Older tools do not check the range, so run the pinned tool through
`dotnet tool run`.

From the Platform root, restore and check the pinned tool with:

```bash
dotnet tool restore
dotnet tool run hexalith-module --version
```

## Run and debug a technical module

EventStore and Memories run and debug from this workspace, never from their own
repository. Each command takes the owner manifest in the module's checkout under
`references/`; the tool loads every owner manifest itself, so never pass the
copies in `src/Hexalith.Platform.Composition/Declarations/` (that duplicates a
module and fails with `HXP003`/`HXP020`).

| Module | Manifest | Platform runs |
| --- | --- | --- |
| EventStore | `references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json` | `eventstore` |
| Memories | `references/Hexalith.Memories/module/hexalith.module-manifest.v2.json` | `memories`, `memories-access-telemetry-clock`, `memories-access-telemetry`, `eventstore`, plus Redis Stack (`memories-vectors`) and FalkorDB (`memories-graph`); no Tenants |

### Prerequisites

- The pinned tool (`dotnet tool restore`), the .NET SDK and Aspire CLI
  `13.6.1` listed above, and a running Docker engine.
- The direct submodules `references/Hexalith.Builds`, `references/Hexalith.EventStore`
  and, for Memories, `references/Hexalith.Memories`, initialized without
  `--recursive`. Each checkout's `HEAD` must equal the gitlink staged in this
  repository. After a local commit inside a submodule, stage it with
  `git add references/<Repository>` or the tool refuses the run.
- `HEXALITH_DAPR_HOME`: an absolute, isolated Dapr home with Dapr CLI `1.18.0`
  at `tools/dapr` and runtime `1.18.2` at `.dapr/bin/{daprd,placement,scheduler}`.
  A machine-wide `dapr init` does not qualify. On Linux x64 (including WSL2),
  prepare one from the official release assets; other platforms need the
  matching `<os>_<arch>` assets of the same releases. The subshell stops at the
  first failed download or checksum and leaves your shell where it was:

  ```bash
  (
    set -euo pipefail
    home="$HOME/.hexalith/dapr/1.18.2"
    mkdir -p "$home/tools" "$home/.dapr/bin"
    cd "$(mktemp -d)"
    base=https://github.com/dapr
    for asset in cli/releases/download/v1.18.0/dapr_linux_amd64.tar.gz \
        dapr/releases/download/v1.18.2/daprd_linux_amd64.tar.gz \
        dapr/releases/download/v1.18.2/placement_linux_amd64.tar.gz \
        dapr/releases/download/v1.18.2/scheduler_linux_amd64.tar.gz; do
      curl -fsSLO "$base/$asset"
      curl -fsSLO "$base/$asset.sha256"
    done
    sha256sum -c ./*.sha256
    tar -xzf dapr_linux_amd64.tar.gz -C "$home/tools"
    for binary in daprd placement scheduler; do
      tar -xzf "${binary}_linux_amd64.tar.gz" -C "$home/.dapr/bin"
    done
    DAPR_RUNTIME_PATH="$home" "$home/tools/dapr" --version   # CLI 1.18.0, runtime 1.18.2
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
```

`debug` builds the active module and its root-declared dependencies from their
`references/` checkouts in Debug, waits for each server's declared `/ready`
endpoint (10-minute default deadline), and reports one process id per project.
`run` starts the same composition without reporting process ids. `down` stops
only the resources tagged with that run. Runs share fixed Dapr control-plane
ports (Sentry `50001`, placement `51005`, scheduler `51006`), so start one at a
time.

`debug` does not print the run id. It is the file name of the run state
`<runId>.json` under `${XDG_DATA_HOME:-$HOME/.local/share}/hexalith-builds/g4-runs/`
on Linux; the newest run is
`ls -t "${XDG_DATA_HOME:-$HOME/.local/share}"/hexalith-builds/g4-runs/*.json | head -1`.
When an earlier run used the same manifest, `down --manifest` refuses with
`HXR024`; pass the id as `down --manifest <manifest> --run-id <runId>`.

Known issue with the pinned `4.33.0`: `run` and `debug` stop with `HXP020`
("Could not load file or assembly 'Hexalith.Builds.Tooling, Version=4.33.0.0'").
The package's AppHost carries `Hexalith.Builds.Tooling` `1.0.0.0` and
build-machine paths, so these commands need a fixed Builds release. See
`_bmad-output/implementation-artifacts/evidence/1-10/README.md`.

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

### Own-repository AppHosts

A technical module's own AppHost, such as `src/Hexalith.Memories.AppHost` or
`src/Hexalith.EventStore.AppHost`, serves only that repository's own tests. A
run of it is never evidence that the module runs in the Platform composition;
only the commands above, from this workspace, are.

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
