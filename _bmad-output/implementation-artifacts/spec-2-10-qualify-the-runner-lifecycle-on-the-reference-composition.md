---
title: '2.10 Qualify the runner lifecycle on the reference composition'
type: 'feature'
created: '2026-11-18'
status: 'in-progress'
route: 'dispatch'
baseline_commit: 'a2d746475d77202ddd31d09980857eba9bf6b157'
review_loop_iteration: 0
story_key: '2-10-qualify-the-runner-lifecycle-on-the-reference-composition'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The [2026-11-18 rescope](../planning-artifacts/sprint-change-proposal-2026-11-18.md) makes 2.10 a local run of the nine lifecycle scenarios on Parties, using the pinned released tool (Builds 6.3.0) and recording one compact summary per scenario. No 6.3.0 local run exists. The current controller is bound to 4.47.0 and cannot drive attached suites or collisions.

**Approach:** Add a small stdlib Python driver. It runs the released `hexalith-module` against Parties' existing `lifecycle-package` and `attached-lifecycle-package` fixtures through their `control.json`. Run all nine scenarios once and commit a compact summary. The CI part and the tool acceptance and pins stay open for Story 2.14 and CiCd story 21.

## Boundaries & Constraints

**Always:**
- **Inputs:**
  - Use Parties `8e3507e47a157875931b4ebcf34aff67fd3d5962`, the Platform gitlink, in a scratch git worktree.
  - Initialize only that worktree's nested `references/Hexalith.Builds`, at its gitlink `7afc4981e21bc2fcce687199a3fd2c61fe87336e` (v6.3.0), from local objects with no fetch.
  - Restore the tool from the worktree's `.config/dotnet-tools.json`. NuGet and registry pulls are allowed.
- **Package mode:** use package mode, as both profiles declare.
- **Environment:** leave `CI` and `GITHUB_ACTIONS` unset and fabricate no GitHub metadata.
- **Isolation:**
  - Snapshot non-owned containers and `list` entries before and after the run.
  - Act only on run ids the driver issued.
  - Every exit path runs `down` for every issued run.
- **Evidence:** one `summary.json` and a short README table, with no logs, archives, credentials or home paths.
- **Flaky failures:** one re-run is allowed for a failure that does not reproduce. Record the flaky test as a bug.

**Never:**
- Hosted workflows, dispatches, pushes or pin changes.
- Edits to Builds, Parties or Platform Composition source. On a product refusal or defect (e.g. HXP020), stop and report it. Never patch allowlists; 2.14 owns them.
- A startup-timeout override. Pause the owned AppHost instead and keep the default 600 s deadline.

**Decisions (2026-11-18):**
- Replace the 4.47.0 controller. Delete `local.py`, `test_local.py`, `prepare.py` and `test_prepare.py`, so the new driver is the only one; the history stays in git.
- Keep the full spec despite its size.

## I/O & Edge-Case Matrix

| Scenario | Drive | Expected |
|---|---|---|
| success | owner `pass` | `passed`; resources removed; no leftovers |
| failure | owner `fail` | `failed`, exit 4; `failure-retained` until `down`, then clean |
| startup timeout + partial | SIGSTOP the owned AppHost while `Starting` | HXR021 `startup-timeout` at effective 600 s, override `not-overridden`; actual duration and unready resources recorded; partial resources retained until `down` |
| cancellation | SIGINT to the owner process group during `wait` | `cancelled`, exit 130; clean, no leftovers |
| collision | second owner reuses the first owner's `--eventstore-port` | second refused with HXR031, which names the first run; first unaffected and passes |
| owner finish before attachment end | `attach --hold-seconds`, then owner `pass` | `passed` recorded at once; cleanup waits for `attach --end`; a new attach is refused with `owner-passed` |
| owner cancel before attachment end | attached suite (`--attach-to`) in `wait`, then SIGINT the owner | attached run `withdrawn` (`owner-cancelled`) before owner teardown; owner `cancelled` |
| attached failure + hold expiry | attached suite `fail`; a second attach with a short hold expires | attached `failed`, environment retained, owner untouched; expiry `withdrawn`/`hold-expired` with `expiresAt` unchanged; retry refused with HXR036 |
| failure then cancellation | attached `fail`, then SIGINT the owner | attached failure and retention unchanged; a second `--end` returns the first outcome |

A row whose expectation fails is recorded as `failed`, with its diagnostic ids. The run then continues with the remaining rows.

</frozen-after-approval>

## Code Map

- `eng/lifecycle-qualification/local.py` is the old controller, to be deleted. Before deleting it, reuse its ideas: run-id discovery from `g4-runs/*.json` (`discover`, line 466), the control-file write (line 503) and the `diagnostics/<runId>/selected-test.json` read (line 563).
- Parties `module/hexalith.module-manifest.v2.json` declares `tests[3]` `lifecycle-package` and `tests[4]` `attached-lifecycle-package`. Both use package mode with 300 s execution and 90 s stop limits. There is no `lifecycle.startup`, so the default 600 s applies.
- Parties `tests/Hexalith.Parties.Platform.LifecycleTests/LifecycleQualificationTests.cs:18-50`:
  - Reads `HEXALITH_G4_RUN_ID` and `HEXALITH_TEST_ENVIRONMENT_PATH`.
  - Requires `~/.local/share/hexalith-builds/ci-qualification/<runId>/control.json`, with `{"runId","action":"wait|pass|fail"}`, to exist when the test starts.
  - Writes `execution-ready.json` and polls the control file. The suite bound is 240 s.
- Parties `tests/Hexalith.Parties.Platform.AttachedLifecycleTests/AttachedLifecycleQualificationTests.cs:15-50`: its control file adds `ownerRunId`, `environmentId` and `originalAttachmentRequestId`.
- 6.3.0 scrubs `XDG_DATA_HOME` from children, so do not relocate state.
- Package preparation: `hexalith-module prepare-build --manifest module/hexalith.module-manifest.v2.json --mode package --workspace <W> --output json`. Then run `dotnet build <suite csproj> -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true` with the prepared imports.
- Builds 6.3.0 `ModuleCommandApplication.cs:224-238,391-421` defines the commands:
  - `test --attach-to/--hold-seconds/--attachment-request-id/--eventstore-port`
  - `attach --owner-run-id --composition-identity <sha256 of manifest bytes> --hold-seconds` and `attach --end --attached-run-id --outcome`
  - `down --run-id` and `list --output json`
  - Exit codes: 3 topology, 4 test, 130 cancelled.
  - Only SIGINT is cooperative (`ToolCommandHost.cs:20-53`).
- `~/.local/share/hexalith-builds/diagnostics/<runId>/test-result.json` holds:
  - `terminal.{outcome,cause,recordedAt,diagnosticIds}`
  - `bindings.deadlines.startup.{effectiveSeconds,actualDurationSeconds,override.state}`
  - `bindings.attachments.holds[]`, `retention`, `cleanup.{disposition,leftoverResourceIds}` and `bindings.tool.{version,contentIdentity}`
  - It does not record the source revision; the driver captures it.
- CI identities are counted, not re-run. They are the Story 2.8 hosted Parties runs [42521615234](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42521615234), [42526256372](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42526256372) and [42528046681](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42528046681).
- Earlier 2.10 history lives in this file's git history (`ed8e1048ddf83012a70e6fc7254e7dfe9c981025`) and in `evidence/2-10/`, which stays as committed.

## Tasks & Acceptance

**Execution:**
- [x] `eng/lifecycle-qualification/scenarios.py`: add a stdlib driver (about 300 lines) that runs all scenarios or a named subset and writes the summary. It must be repeatable for Epic 3's reruns.
- [x] `eng/lifecycle-qualification/test_scenarios.py`: unit-test the pure summary extraction and the row verdicts against sample `test-result.json` documents.
- [x] `eng/lifecycle-qualification/{local,test_local,prepare,test_prepare}.py`: delete them.
- [x] `eng/lifecycle-qualification/README.md`: rewrite it to cover the prerequisites and the run command.
- [ ] `_bmad-output/implementation-artifacts/evidence/2-10/local-6-3-0-20261118/`: run all nine rows live and write `summary.json` and `README.md`. **Blocked (2026-11-18):** two product defects, see Implementation Notes; no evidence directory was written.

**Acceptance Criteria:**
- **Given** the summary, **when** inspected, **then** each row holds:
  - the source revision, run and environment ids, tool version and content identity;
  - the first outcome and cause, the effective deadline, the actual startup duration and the override state;
  - the diagnostic ids, retention and cleanup disposition, and leftovers;
  - an unchanged foreign-resource check.
- **Given** any driver exit, **when** it finishes, **then** `list` shows no issued run and no new container remains.
- **Given** the summary README, **when** read, **then** it links the 2.8 runs and states that the CI part and the accepted-tool record wait for 2.14 and CiCd story 21.

## Implementation Notes

**2026-11-18: driver delivered, live campaign stopped at two product defects.**

- **Driver:** `scenarios.py` (stdlib), its unit tests and the README are in place, and the 4.47.0 controller is deleted. The driver launches the restored 6.3.0 entry assembly directly, so each owner tool leads its own process group. Run ids come from new `g4-runs` states bound to the manifest hash and to the driver's own AppHost process group. It is 733 lines, more than the ~300 estimate, because each of the nine rows has its own checks.
- **Defect 1, HXP020:** the owner `lifecycle-package` run is refused at startup with "The ordinary Parties package profile requires its trusted parent plan/key handoff and unchanged source/test inputs" (exit 3, `startup-failure`, after about 158 s). This reproduced on two owner runs, `76555c8ae12daa014a8d0bcd10ceec7a` and `460954eea8dba5b234d860a11f2b7116`. Both cleaned up completely with no leftovers, and foreign containers and `list` entries were unchanged. Story 2.14 owns this allowlist; nothing was patched.
- **Defect 2, attached suite does not compile in package mode:** Builds 6.3.0 `Props/Directory.Packages.props` lists `Hexalith.Parties.Platform.AttachedLifecycleTests` among the "measured retained SDK4.2 consumers" (`HexalithPartiesSdk42CatalogView`), so it resolves `Hexalith.EventStore.Testing` 4.2.0. Parties `AttachedLifecycleQualificationTests.cs` (since `e35b8c1e`) uses `TestEnvironmentDescriptor.OwnerRunId`, `EnvironmentId`, `OriginalAttachmentRequestId` and `AttachmentExpiresAtUtc`, which first ship in 5.0.0, so the build fails with CS1061 (9 errors). This blocks the three attached-suite rows. Builds `main` (`eae4531a`) still lists the project. The fix belongs to the Builds catalog or Parties and needs a release under 2.14's version rule.
- **Local prerequisites found:**
  - 6.3.0 selects Dapr CLI and runtime 1.19.1. The shell's `HEXALITH_DAPR_HOME` (1.18.2 and 1.18.7) is refused with HXR012. An isolated home was created at `~/.hexalith/dapr/cli-1.19.1-runtime-1.19.1` from the archives pinned in `Props/dapr-toolchain-sha256.json`; shell profiles are unchanged.
  - The tool's workspace resolver initializes the worktree's nested `references/Hexalith.Platform` itself, from GitHub, during `prepare-build` and `test`.
- **Rerun** after 2.14 and a catalog fix: follow `eng/lifecycle-qualification/README.md` and write the summary to the evidence directory above.
- **Parked (2026-11-18, user decision):**
  - Verified: the current Parties declaration `dfb03b6e…` is absent from `IsOrdinaryDeclaration` (`src/Hexalith.Platform.Composition/PlatformPartiesPackageCatalogQualification.cs:21`). That method admits only `40cee6ce…` and `d9c193b0…`, and Story 2.14 removes it.
  - The attached-suite catalog defect is in `deferred-work.md`.
  - The work is committed locally without a push. Step 04 review is deferred to the live rerun, after 2.14 and a Builds release carrying the catalog fix. The status stays `in-progress`.

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `python3 -B -m unittest discover -s eng/lifecycle-qualification -v` -- expected: all pass
- `python3 -B eng/lifecycle-qualification/scenarios.py --checkout <worktree> --evidence <dir>` -- expected: exit 0, nine rows `passed`
- `dotnet tool run hexalith-module list --output json` (in the worktree) -- expected: no issued run ids
