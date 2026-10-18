# Story 1.10 evidence: run and debug EventStore and Memories from the Platform workspace

Collected 2026-10-18 on the WSL2 host, from the Platform root.

## Versions and commits

| Item | Value |
| --- | --- |
| Platform | `79935099e9b4c6fdcffe29f84d42b8c92b50fc2b` (baseline, working tree) |
| Pinned tool | `hexalith.builds.module.cli` `4.33.0+544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9` |
| Builds checkout | `6e8b71e` (`v4.33.0` + 2 commits), not changed |
| EventStore | baseline `c0c08d45`; story commits `a3b0c587` (fix), `2af29911` (docs), local only |
| Memories | baseline `fb39a0c5`; story commits `c16ea1a1` (fix), `5d77b0ec` (docs), local only |
| Aspire CLI | `13.6.1+3751e615ad660621f2a7aa4c390ef48e08a0eed8` |
| Docker | `29.8.2` |
| .NET | SDK `10.0.401`, runtime `10.0.12` |
| Dapr home | `~/.hexalith/dapr/1.18.2`: CLI `1.18.0`, runtime `1.18.2` ([dapr-home.txt](dapr-home.txt)) |
| Debugger | netcoredbg `3.2.0-1092` (MIT), scratchpad only |

Runs used the baseline commits plus this story's uncommitted changes, which were committed afterwards
without further edits. Only `EventStore.Aspire`/`Memories.Aspire` hosting helpers, their tests and docs
changed; none of the launched servers did.

## Acceptance criteria

| AC | Result |
| --- | --- |
| AC1: `debug` with the pinned tool reaches ready, a breakpoint is hit, `down` stops only that run | **Pending release.** The pinned `4.33.0` fails with `HXP020` ([pinned-tool.txt](pinned-tool.txt)). The Builds fix (local commit `5faaf88`), packed as `4.33.1-local.1`, passes for both manifests ([prerelease-tool.txt](prerelease-tool.txt)); the final proof needs the released, pinned tool. |
| AC2: gated live tests for EventStore, Tenants and Memories pass | **Passed**, 3/3 in 257.9 s ([live-tests.txt](live-tests.txt)). |
| AC3: each README scopes the own-repository AppHost to its tests | **Done**: Platform `README.md`, EventStore `README.md` and `CONTRIBUTING.md`, Memories `README.md`. |

## AC1 blocker (needs a Builds change, outside this story)

The `4.33.0` package is not usable for any Platform v2 `run`/`debug` here:

1. `g4-host/Hexalith.Builds.Tooling.dll` is `1.0.0.0`, while the CLI compiles the Platform model against its
   own `4.33.0.0` copy. The AppHost loads `1.0.0.0` first, so loading the model fails (`0x80131040`).
   Builds `6e8b71e` already versions the hosts and adds a package check, but it is an unreleased `build:` commit.
2. The packaged AppHost carries the CI runner's paths (`/home/runner/...` for the AppHost project, DCP and
   dashboard). With cause 1 bypassed in a scratchpad copy, Aspire stops on
   `UnauthorizedAccessException: Access to the path '/home/runner' is denied.` The `6e8b71e` check runs
   `--validate-platform` only, so it does not cover this.

A fixed Builds release and a Platform pin move are required. On 2026-10-18 the human chose to fix Builds in this story (next section).

## Builds fix pre-release proof (not the final AC1 evidence)

Builds local commit `5faaf88` (`fix(tool): launch the packaged AppHost with local Aspire paths`, on `4629ff2`)
resolves DCP and the dashboard from this machine's NuGet cache at the catalog Aspire version, gives the AppHost
directory and Aspire store run-private directories, and fails with `HXR016` when a package is missing. Packed as
`4.33.1-local.1` from a scratch clone whose baked paths were then made unusable (read-only, deleted NuGet view), and
installed into a scratch tool path, it ran `debug` from the Platform root to ready for both manifests, netcoredbg hit
`ServiceCollectionExtensions.cs:225` (`path = "/ready"`) and `FalkorDbHealthCheck.cs:28` (`"falkordb"`), and
`down --run-id` removed only each run. The same packed host without the overrides stops with
`UnauthorizedAccessException` ([prerelease-tool.txt](prerelease-tool.txt)).

## What works once the tool is fixed (diagnostic, not AC1 evidence)

The same `debug` and `down` commands, run with the Builds CLI built from the `references/Hexalith.Builds`
checkout, reached ready, and netcoredbg hit breakpoints in both checkouts:

| Module | Launcher pid | App pid | Breakpoint | Hit output |
| --- | --- | --- | --- | --- |
| EventStore | 86917 | 96522 | `references/Hexalith.EventStore/src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs:225` | `stopped, reason: breakpoint 1 hit ... ServiceCollectionExtensions.cs:225` |
| Memories | 47176 | 56393 | `references/Hexalith.Memories/src/Hexalith.Memories.Server/HealthChecks/FalkorDbHealthCheck.cs:28` | `stopped, reason: breakpoint 1 hit ... FalkorDbHealthCheck.cs:28`, `context.Registration.Name = "falkordb"` |

The Memories run also reported launchers 33685 (`eventstore`), 47168 (`memories-access-telemetry`) and
47170 (`memories-access-telemetry-clock`), each with its app process as a child. `down --run-id` returned
`completed: Success` for both runs and removed only that run's containers and processes
([diagnostic-eventstore.txt](diagnostic-eventstore.txt), [diagnostic-memories.txt](diagnostic-memories.txt)).

netcoredbg `detach` left the .NET 10.0.12 debuggee suspended in both runs, so the docs name VS Code and
tell developers to stay attached and finish with `down`.

## Environment notes

- The installed machine-wide Dapr (runtime `1.18.7`) was not used; the isolated home above was provisioned
  from official release assets with verified checksums.
- Two of three live-test class runs failed when Sentry exited with `no space left on device`: the host's
  inotify watch budget was exhausted (1,048,543 of 1,048,576), mostly by `fluentui-mcp` processes from other
  sessions. The third run passed. Concurrent runs also share fixed Dapr ports; run-scoping is Epic 2.
- Unit-test results and baselines: [unit-tests.txt](unit-tests.txt).
