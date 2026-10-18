# Story 1.10 evidence: run and debug EventStore and Memories from the Platform workspace

Collected 2026-10-18 on the WSL2 host, from the Platform root.

## Versions and commits

The source live tests use these revisions, verified unchanged at the start and
end of the run. Binary SHA-256 identities and UTC timestamps are in
[live-tests.txt](live-tests.txt). A concurrent, uncommitted Builds audit refresh
was preserved; it does not affect source declarations or catalog selections.

| Item | Value |
| --- | --- |
| Platform | `5f18593639832d1245577dd93ec07687517d3acb` |
| Builds | `332b2c144defba546fe0a09626a4a72f2340e22f` |
| EventStore | `d55c6f7c11e9bce6f17d490a84c83f1561979d63` |
| Memories | `d0c5d8ca96b06e02acdda88f255b33288642a7f7` |
| Pinned tool | `hexalith.builds.module.cli` `4.33.0`; release/pin update pending |
| .NET | SDK `10.0.402`, runtime `10.0.13` |
| Aspire CLI | `13.6.1` |
| Dapr home | `~/.hexalith/dapr/1.18.2`: CLI `1.18.0`, runtime `1.18.2` ([dapr-home.txt](dapr-home.txt)) |
| Debugger used in historical proofs | netcoredbg `3.2.0-1092` (MIT), scratchpad only |

The source-built diagnostic runs and the `4.33.1-local.1` prerelease proof below
are historical results. Each transcript retains the tree/tool identities it
originally recorded; they do not prove the current released/pinned acceptance.
The Builds implementation now also includes the process-architecture correction
and child-process assembly isolation reviewed after the prerelease proof.

## Acceptance criteria

| AC | Result |
| --- | --- |
| AC1: `debug` with the pinned tool reaches ready, a breakpoint is hit, `down` stops only that run | **Pending release.** The pinned `4.33.0` fails with `HXP020` ([pinned-tool.txt](pinned-tool.txt)). The Builds fix (local commit `5faaf88`), packed as `4.33.1-local.1`, passes for both manifests ([prerelease-tool.txt](prerelease-tool.txt)); the final proof needs the released, pinned tool. |
| AC2: gated live tests for EventStore, Tenants and Memories pass | **Passed**, 3/3 in 286.050 s at the reviewed Builds/catalog revision above ([live-tests.txt](live-tests.txt)). |
| AC3: each README scopes the own-repository AppHost to its tests | **Done**: Platform `README.md`, EventStore `README.md` and `CONTRIBUTING.md`, Memories `README.md`. |

## AC1 release blocker

The `4.33.0` package is not usable for any Platform v2 `run`/`debug` here:

1. `g4-host/Hexalith.Builds.Tooling.dll` is `1.0.0.0`, while the CLI compiles the Platform model against its
   own `4.33.0.0` copy. The AppHost loads `1.0.0.0` first, so loading the model fails (`0x80131040`).
   Builds `6e8b71e` already versions the hosts and adds a package check, but it is an unreleased `build:` commit.
2. The packaged AppHost carries the CI runner's paths (`/home/runner/...` for the AppHost project, DCP and
   dashboard). With cause 1 bypassed in a scratchpad copy, Aspire stops on
   `UnauthorizedAccessException: Access to the path '/home/runner' is denied.` The `6e8b71e` check runs
   `--validate-platform` only, so it does not cover this.

A fixed Builds release and a Platform pin move are required. On 2026-10-18 the human chose to fix the two packaged-host defects in this story (next section). The additional catalog/audit repair and formatting-only qualification correction were approved and supplied by the owner. CI [39138234609](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39138234609) passed at `c6f56c011ce3a83cdf6100c322352daf125c6af9`. Approved Release [39139908836](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39139908836) has passed its source/package qualification and is creating the release. See [builds-resumed-verification.txt](builds-resumed-verification.txt) for the local source and package checks. The older failures in [release-blocker.txt](release-blocker.txt) and [qualification-blocker.txt](qualification-blocker.txt) are retained as historical evidence.

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
- The historical live-test timeline is inconsistent and is retained only as
  [superseded evidence](live-tests-historical.txt). The resumed run captures UTC
  start/end times directly: the first attempt failed with Sentry Exited at the
  saturated watch limit, then all three cases passed with temporary watch headroom.
  The original host limit was restored. Runs still share fixed Dapr ports; run-scoping is Epic 2.
- Unit-test results and baselines: [unit-tests.txt](unit-tests.txt).
