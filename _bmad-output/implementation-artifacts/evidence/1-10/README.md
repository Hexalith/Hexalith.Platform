# Story 1.10 evidence: run and debug EventStore and Memories from the Platform workspace

Collected 2026-10-18 on the WSL2 host, from the Platform root.

## Released pinned-tool proof (AC1)

Both owner manifests passed with `dotnet tool run hexalith-module -- debug`
from the Platform root, using the restored NuGet.org tool
`4.33.1+c6f56c011ce3a83cdf6100c322352daf125c6af9`
([restore/version output](released-tool-restore.txt)). Each run used the
package's `g4-host/Hexalith.Builds.Module.AppHost.dll`, the local Aspire
packages, and run-private AppHost and Aspire-store directories.

| Item | Canonical revision / version |
| --- | --- |
| Platform before both proofs | `5151bef5b68d6bd1f33d94f0a3a1b11d32dd48a6` |
| Builds checkout and release tag `v4.33.1` | `c6f56c011ce3a83cdf6100c322352daf125c6af9` |
| EventStore | `d55c6f7c11e9bce6f17d490a84c83f1561979d63` |
| Memories | `4f6b3b1265d15d208761e6419e5c825d3f39fea3` |
| Pinned tool | `hexalith.builds.module.cli` `4.33.1` |
| Debugger | netcoredbg `3.2.0-1` (`9744e1f`, MIT), scratchpad only |

| Module | Run id | Target launcher / app pid | Breakpoint and observed value |
| --- | --- | --- | --- |
| EventStore | `ce774342c896def4fd912cfa63ed0605` | `43628` / `52553` | `ServiceCollectionExtensions.cs:225`, `path = "/ready"` ([transcript](pinned-eventstore.txt)) |
| Memories | `bc73e2fbec937376a8efbb3a3dd32026` | `92843` / `2651` | `FalkorDbHealthCheck.cs:28`, `context.Registration.Name = "falkordb"` ([transcript](pinned-memories.txt)) |

The transcripts record `stopped, reason: breakpoint 1 hit`, the application
children under the reported launchers, source and assembly SHA-256 identities,
and the healthy selection. EventStore selected only the EventStore project;
Memories selected EventStore, Memories, telemetry and clock, plus Redis Stack
and FalkorDB, with no Tenants resources. Its other project launchers were
`76833`, `92836` and `92832`.

Both exact `down --run-id` commands returned `completed: Success`; each
workspace disappeared and no run-labelled containers or recorded project/app
processes remained. The four pre-existing Dapr containers remained running.
The temporary inotify limit was restored to `1048576`; the directly captured
UTC interval and exit codes are in [pinned-proof.json](pinned-proof.json).
Memories' triggered HTTP request disconnected as `down` stopped the server
after the breakpoint; readiness had already succeeded before attachment.

## Source live test revisions (AC2)

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
| .NET | SDK `10.0.402`, runtime `10.0.13` |
| Aspire CLI | `13.6.1` |
| Dapr home | `~/.hexalith/dapr/1.18.2`: CLI `1.18.0`, runtime `1.18.2` ([dapr-home.txt](dapr-home.txt)) |

The source-built diagnostic runs and the `4.33.1-local.1` prerelease proof below
are historical results. Each transcript retains the tree/tool identities it
originally recorded; they do not prove the current released/pinned acceptance.
The Builds implementation now also includes the process-architecture correction
and child-process assembly isolation reviewed after the prerelease proof.

## Acceptance criteria

| AC | Result |
| --- | --- |
| AC1: `debug` with the pinned tool reaches ready, a breakpoint is hit, `down` stops only that run | **Passed** with released and pinned `4.33.1` for both owner manifests ([EventStore](pinned-eventstore.txt), [Memories](pinned-memories.txt)). |
| AC2: gated live tests for EventStore, Tenants and Memories pass | **Passed**, 3/3 in 286.050 s at the reviewed Builds/catalog revision above ([live-tests.txt](live-tests.txt)). |
| AC3: each README scopes the own-repository AppHost to its tests | **Done**: Platform `README.md`, EventStore `README.md` and `CONTRIBUTING.md`, Memories `README.md`. |

## Historical 4.33.0 release blocker (resolved in 4.33.1)

The `4.33.0` package is not usable for any Platform v2 `run`/`debug` here:

1. `g4-host/Hexalith.Builds.Tooling.dll` is `1.0.0.0`, while the CLI compiles the Platform model against its
   own `4.33.0.0` copy. The AppHost loads `1.0.0.0` first, so loading the model fails (`0x80131040`).
   The intermediate Builds `6e8b71e` versions the hosts and adds a package check; it had not been released when this failure was recorded.
2. The packaged AppHost carries the CI runner's paths (`/home/runner/...` for the AppHost project, DCP and
   dashboard). With cause 1 bypassed in a scratchpad copy, Aspire stops on
   `UnauthorizedAccessException: Access to the path '/home/runner' is denied.` The `6e8b71e` check runs
   `--validate-platform` only, so it does not cover this.

Builds [CI 39138234609](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39138234609)
and the approved [Release 39139908836](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39139908836)
both passed at `c6f56c011ce3a83cdf6100c322352daf125c6af9`.
[Release v4.33.1](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v4.33.1)
was published on 2026-10-18; Platform now pins it and uses that tag's Builds
commit. The additional audit/catalog and formatting repairs were approved and
supplied by the owner. Local source/package checks are in
[builds-resumed-verification.txt](builds-resumed-verification.txt), and release,
approval and NuGet availability records are in [release-progress.json](release-progress.json).
The earlier failures in [release-blocker.txt](release-blocker.txt) and
[qualification-blocker.txt](qualification-blocker.txt) are historical.

## Builds fix pre-release proof (not the final AC1 evidence)

Builds local commit `5faaf88` (`fix(tool): launch the packaged AppHost with local Aspire paths`, on `4629ff2`)
resolves DCP and the dashboard from this machine's NuGet cache at the catalog Aspire version, gives the AppHost
directory and Aspire store run-private directories, and fails with `HXR016` when a package is missing. Packed as
`4.33.1-local.1` from a scratch clone whose baked paths were then made unusable (read-only, deleted NuGet view), and
installed into a scratch tool path, it ran `debug` from the Platform root to ready for both manifests, netcoredbg hit
`ServiceCollectionExtensions.cs:225` (`path = "/ready"`) and `FalkorDbHealthCheck.cs:28` (`"falkordb"`), and
`down --run-id` removed only each run. The same packed host without the overrides stops with
`UnauthorizedAccessException` ([prerelease-tool.txt](prerelease-tool.txt)).

## Historical source-built diagnostic proof (not AC1 evidence)

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
- Two early final-proof harness attempts reached ready and cleaned up, but stopped before debugger attachment: the harness initially compared sidecar IDs with project IDs and then shortened a dotted assembly name while hashing. Those scratch transcripts remain under `/tmp/bmad-1-10-pinned-eventstore-harness-attempt*.log`; neither is counted as breakpoint acceptance. The corrected proofs above both passed.
- Unit-test results and baselines: [unit-tests.txt](unit-tests.txt).
