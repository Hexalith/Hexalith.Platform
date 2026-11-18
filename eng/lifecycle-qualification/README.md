# Local lifecycle scenarios

`scenarios.py` runs the Story 2.10 lifecycle scenarios locally on the reference
composition (Parties) with the released `hexalith-module` that Parties pins. It
drives Parties' committed `lifecycle-package` and `attached-lifecycle-package`
fixtures through their `control.json` files and writes one compact
`summary.json` plus a `README.md` table. Epic 3 reruns use the same command.

It replaces the 4.47.0 controller (`local.py`, `prepare.py` and their tests),
which remains in git history.

## Prerequisites

- Linux with Docker running and able to pull the module images from
  `registry.hexalith.com`, the .NET SDK from Parties' `global.json`, Python 3
  and `pwsh` (looked up in `~/.dotnet/tools`).
- The toolchain that the pinned Builds selects, which the tool probes before it
  allocates anything:
  - `HEXALITH_DAPR_HOME` names an isolated Dapr home: `tools/dapr` and
    `.dapr/bin/{daprd,placement,scheduler}` at the `HexalithDaprCliVersion`
    and `HexalithDaprRuntimeVersion` of the nested Builds
    `Props/Directory.Packages.props` (1.19.1 for Builds 6.3.0). Builds CI
    downloads those archives and checks them against
    `Props/dapr-toolchain-sha256.json`; do the same locally. Otherwise the
    owner stops with HXR012 or HXR013.
  - The Aspire CLI on `PATH` matches `Aspire.AppHost.Sdk` (13.6.5 for 6.3.0).
- A scratch worktree of Parties at the Platform gitlink, with only its nested
  `references/Hexalith.Builds` initialized at its own gitlink, from local
  objects and without a fetch:

  ```bash
  parties=$(git rev-parse HEAD:references/Hexalith.Parties)
  git -C references/Hexalith.Parties worktree add --detach /path/to/parties "$parties"
  git -C /path/to/parties -c protocol.file.allow=always \
    -c submodule.Hexalith.Builds.url="$(git rev-parse --absolute-git-dir)/modules/references/Hexalith.Builds" \
    submodule update --init -- references/Hexalith.Builds
  ```

  The released tool's workspace resolver then initializes the other nested
  references it maps (for example `references/Hexalith.Platform`) from their
  recorded remotes during `prepare-build` and `test`.

- Leave `CI` and `GITHUB_ACTIONS` unset. The driver also strips them, and every
  `GITHUB_*`, `RUNNER_*` and `XDG_DATA_HOME` value, from the tool's environment.
  Runner state stays in `~/.local/share/hexalith-builds`.

## Run

```bash
python3 -B -m unittest discover -s eng/lifecycle-qualification -v
HEXALITH_DAPR_HOME=/path/to/dapr-home \
python3 -B eng/lifecycle-qualification/scenarios.py --checkout /path/to/parties \
  --evidence _bmad-output/implementation-artifacts/evidence/<story>/<run>/
```

- `--scenario NAME` (repeatable) runs a subset. The names are `success`,
  `failure`, `startup-timeout`, `cancellation`, `collision`,
  `owner-finish-hold`, `owner-cancel-attached`, `attached-failure-expiry` and
  `failure-then-cancellation`.
- `--work DIR` holds the private logs and prepared imports. It defaults to a
  sibling of the checkout and is never evidence.
- `--skip-prepare` reuses an earlier package preparation.

The full campaign takes about an hour, mostly owner startups and the
startup-timeout row's 600 s default deadline. The exit code is 0 only when every
selected row passed and no issued run or container remains.

## What it does

1. **Preflight.** Requires a clean checkout with Builds at its gitlink, runs
   `dotnet tool restore` and launches the restored entry assembly directly, so
   each owner tool leads its own process group.
2. **Package preparation.** Runs `prepare-build --mode package` and builds both
   suites in Release with the prepared imports, before any service starts. A
   suite that does not build is a product defect: the driver stops and names
   the suite and its compiler error codes.
3. **Rows.** Each row snapshots non-owned containers and `list` entries before
   and after itself. It finds its run ids from new `g4-runs/*.json` states
   bound to this manifest and to its own AppHost process group. It acts only on
   those ids. Signals are SIGINT, which is the only cooperative one, plus
   SIGSTOP/SIGCONT of the owned starting AppHost. No timeout is overridden.
4. **Cleanup.** Every exit path, including SIGINT, SIGTERM and SIGHUP, stops the
   launched processes and runs `down` for every issued owner. The summary
   records any issued run that `list` still shows, and any container left.

A row whose expectations fail is recorded as `failed` with its failed checks
and rule ids, and the campaign continues. A row that fails is re-run once; if
the re-run passes, the row is marked `flaky` and keeps the first attempt, and
the flaky test is recorded as a bug. An owner that exits before execution (a
product refusal such as HXP020, or a missing prerequisite such as HXR012) stops
the campaign, and the remaining rows are recorded as `not-run`.

## Evidence

`summary.json` records, for each run of each row:

- the source revision, run, attempt and environment ids, and the tool version
  and content identity;
- the first outcome and cause, the effective startup deadline, the actual
  startup duration and the override state;
- the rule and diagnostic ids, the retention at exit and after cleanup, the
  cleanup disposition and the leftovers;
- the row's foreign-resource check.

No logs, archives, credentials or absolute paths are written. The CI part of
Story 2.10 is not run here: it counts Story 2.8's hosted runs and, after Story
2.14 and CiCd story 21, Parties' normal CI.
