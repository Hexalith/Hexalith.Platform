---
title: '2.10 Qualify the runner lifecycle on the reference composition'
type: 'feature'
created: '2026-11-18'
status: 'in-progress'
route: 'dispatch'
baseline_commit: 'a2d746475d77202ddd31d09980857eba9bf6b157'
review_loop_iteration: 0
story_key: '2-10-qualify-the-runner-lifecycle-on-the-reference-composition'
course_correction: '../planning-artifacts/sprint-change-proposal-2026-11-18.md'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The [2026-11-18 rescope](../planning-artifacts/sprint-change-proposal-2026-11-18.md) calls for a local run of the nine lifecycle scenarios on Parties and one compact summary per scenario. On 2026-11-27, after Platform `v2.9.0` (`a670a4c5`) and Parties `869bd043` adopted Builds `v6.9.1`, the owner renegotiated the inputs to restore the latest-released CLI rule and run live on Parties `869bd043` with Builds `v6.9.1` and Builds CLI `6.9.1`.

**Approach:** Maintain the stdlib Python driver (`eng/lifecycle-qualification/scenarios.py`), reading the `lifecycle-package` and `attached-lifecycle-package` suite entry paths from `module/hexalith.module-manifest.v2.json`. It runs the latest released `hexalith-module` (`6.9.1`) pinned by Parties against Parties' existing `lifecycle-package` and `attached-lifecycle-package` fixtures through their `control.json`. Confirm Docker on this host is quiescent before starting, run all nine scenarios live once with isolated Dapr home `~/.hexalith/dapr/cli-1.19.1-runtime-1.19.2`, verify zero residue and `foreign.unchanged=true` across all rows, and commit a compact summary. The CI part and the tool acceptance and pins stay open for Story 2.14 and CiCd story 21.

## Boundaries & Constraints

**Always:**
- **Inputs:**
  - Use Parties `869bd04362f914a05a010c039227f668c8ad903f` (pulled into Platform's `references/Hexalith.Parties` submodule from `origin/main`, advancing the gitlink from `e86d212abd7788aa5b820450d88266eda99462e5` at Platform `v2.9.0` `a670a4c51d76f63f3142e38f02542a246e1926fb`) in a scratch git worktree.
  - Initialize only that worktree's nested `references/Hexalith.Builds`, at its committed catalog gitlink `91cb7cba990ffdf82930f9bf8cb68aef41baba76` (`v6.9.1`), from local objects with no fetch. Record the catalog revision separately from the CLI release.
  - Restore the latest released Builds CLI (`6.9.1`) from the worktree's `.config/dotnet-tools.json`. Confirm it is the latest published release before running. NuGet, registry, and verified Dapr toolchain pulls (using isolated `~/.hexalith/dapr/cli-1.19.1-runtime-1.19.2` home verified against `Props/dapr-toolchain-sha256.json` without modifying shell profiles) are allowed.
- **Package mode:** use package mode, as both profiles declare.
- **Environment:** leave `CI` and `GITHUB_ACTIONS` unset and fabricate no GitHub metadata. Confirm Docker on this host is quiescent (no other test suites mutating containers) before starting.
- **Isolation:**
  - Snapshot non-owned containers and `list` entries before and after the run.
  - Act only on run ids the driver issued.
  - Every exit path runs `down` for every issued run.
  - Verify zero residue and `foreign.unchanged=true` across all nine rows.
- **Evidence:** one `summary.json` and a short README table in `_bmad-output/implementation-artifacts/evidence/2-10/local-6-9-1-20261127/`, with no logs, archives, credentials or home paths.
- **Flaky failures:** one re-run is allowed for a failure that does not reproduce. Record the flaky test as a bug.

**Never:**
- Hosted workflows, dispatches, pushes or accepted-tool pin changes (advancing Platform's `references/Hexalith.Parties` submodule gitlink to `869bd04362f914a05a010c039227f668c8ad903f` for this renegotiated run is explicitly authorized).
- Edits to Builds, Parties or Platform Composition source. On a product refusal or defect (e.g. HXP020), stop and report it. Never patch allowlists; 2.14 owns them.
- A startup-timeout override. Pause the owned AppHost instead and keep the default 600 s deadline.

**Decisions (2026-11-18):**
- Replace the 4.47.0 controller. Delete `local.py`, `test_local.py`, `prepare.py` and `test_prepare.py`, so the new driver is the only one; the history stays in git.
- Keep the full spec despite its size.

**Decisions (2026-11-27):**
- Relax the 2026-11-21 latest-only CLI rule to run live on current Parties `ca637a01829c57d9163fb686a56bc82b094a10d9` with its signed pinned Builds CLI `6.5.1` and nested Builds catalog `055d7df5b49669d5b705ce163b0b2e187ecb9a50`.
- Read suite project paths from `module/hexalith.module-manifest.v2.json` in `eng/lifecycle-qualification/scenarios.py` instead of hardcoding `SUITES`.
- Provision an isolated Dapr `cli-1.19.1-runtime-1.19.2` home verified against `Props/dapr-toolchain-sha256.json` if absent, leaving shell profiles unchanged.

**Decisions (2026-11-27, renegotiation 2):**
- Pull `references/Hexalith.Parties` to `869bd04362f914a05a010c039227f668c8ad903f` (including the updated Platform submodule gitlink in this change) and run with its nested `references/Hexalith.Builds` `91cb7cba990ffdf82930f9bf8cb68aef41baba76` (`v6.9.1`) and Builds CLI `6.9.1`.
- Restore the latest-released CLI rule (`6.9.1` confirmed latest on NuGet) and write live evidence to `_bmad-output/implementation-artifacts/evidence/2-10/local-6-9-1-20261127/`.
- Confirm Docker quiescence before starting, execute all 9 scenarios live with isolated Dapr home `~/.hexalith/dapr/cli-1.19.1-runtime-1.19.2`, verify zero residue and `foreign.unchanged=true` across all rows, and proceed through Step 4 review.

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
- Parties `module/hexalith.module-manifest.v2.json` declares `lifecycle-package` and `attached-lifecycle-package`. Both use package mode with 300 s execution and 90 s stop limits. The empty `lifecycle.startup` object leaves the default 600 s in effect.
- Parties `tests/Hexalith.Parties.Platform.LifecycleTests/LifecycleQualificationTests.cs:18-50`:
  - Reads `HEXALITH_G4_RUN_ID` and `HEXALITH_TEST_ENVIRONMENT_PATH`.
  - Requires `~/.local/share/hexalith-builds/ci-qualification/<runId>/control.json`, with `{"runId","action":"wait|pass|fail"}`, to exist when the test starts.
  - Writes `execution-ready.json` and polls the control file. The suite bound is 240 s.
- Parties `tests/Hexalith.Parties.Platform.AttachedLifecycleTests/AttachedLifecycleQualificationTests.cs:15-50`: its control file adds `ownerRunId`, `environmentId` and `originalAttachmentRequestId`.
- 6.3.0 scrubs `XDG_DATA_HOME` from children, so do not relocate state.
- Package preparation: `hexalith-module prepare-build --manifest module/hexalith.module-manifest.v2.json --mode package --workspace <W> --output json`. Then run `dotnet build <suite csproj> -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true` with the prepared imports.
- Builds CLI 6.4.4 `ModuleCommandApplication.cs` defines the commands:
  - `test --attach-to/--hold-seconds/--attachment-request-id/--eventstore-port`
  - `attach --owner-run-id --composition-identity <sha256 of manifest bytes> --hold-seconds` and `attach --end --attached-run-id --outcome`
  - `down --run-id` and `list --output json`
  - Exit codes: 3 topology, 4 test, 130 cancelled.
  - Only SIGINT is cooperative (`ToolCommandHost.cs:20-53`).
- `~/.local/share/hexalith-builds/g4-runs/diagnostics/<runId>/test-result.json` holds:
  - `terminal.{outcome,cause,recordedAt,diagnosticIds}`
  - `bindings.deadlines.startup.{effectiveSeconds,actualDurationSeconds,override.state}`
  - `bindings.attachments.holds[]`, `retention`, `cleanup.{disposition,leftoverResourceIds}` and `bindings.tool.{version,contentIdentity}`
  - It does not record the source revision; the driver captures it.
- Story 2.8 supplies hosted lifecycle evidence under the original Parties revision `926c1c3f` and Builds 4.44.13. Its lifecycle, startup-cancellation and execution-cancellation runs, respectively, are [42521615234](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42521615234), [42526256372](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42526256372) and [42528046681](https://github.com/Hexalith/Hexalith.Parties/actions/runs/42528046681).
- Earlier 2.10 history lives in this file's git history (`ed8e1048ddf83012a70e6fc7254e7dfe9c981025`) and in `evidence/2-10/`, which stays as committed.

## Tasks & Acceptance

**Execution:**
- [x] `eng/lifecycle-qualification/scenarios.py`: add a stdlib driver (about 300 lines) that runs all scenarios or a named subset and writes the summary. It must be repeatable for Epic 3's reruns.
- [x] `eng/lifecycle-qualification/test_scenarios.py`: unit-test the pure summary extraction and the row verdicts against sample `test-result.json` documents.
- [x] `eng/lifecycle-qualification/{local,test_local,prepare,test_prepare}.py`: delete them.
- [x] `eng/lifecycle-qualification/README.md`: rewrite it to cover the prerequisites and the run command.
- [x] `eng/lifecycle-qualification/scenarios.py` and `eng/lifecycle-qualification/test_scenarios.py`: resolve `lifecycle-package` and `attached-lifecycle-package` suite project paths from `module/hexalith.module-manifest.v2.json` (handling Parties `ca637a01`'s renamed `Hexalith.Parties.Platform.AttachedLifecycleQualification.Tests.csproj`) and cover manifest-driven suite resolution in unit tests.
- [ ] `_bmad-output/implementation-artifacts/evidence/2-10/local-6-9-1-20261127/`: confirm Docker quiescence on this host, run all nine rows live on Parties `869bd04362f914a05a010c039227f668c8ad903f` with latest released Builds CLI `6.9.1` and nested Builds catalog `91cb7cba990ffdf82930f9bf8cb68aef41baba76` (`v6.9.1`) using isolated Dapr home `~/.hexalith/dapr/cli-1.19.1-runtime-1.19.2`, verify zero residue and `foreign.unchanged=true` across all rows, clean up the scratch worktree/work dir, and write `summary.json` and `README.md`.

**Acceptance Criteria:**
- **Given** the summary, **when** inspected, **then** each row holds:
  - the source revision, run and environment ids, tool version and content identity;
  - the first outcome and cause, the effective deadline, the actual startup duration and the override state;
  - the diagnostic ids, retention and cleanup disposition, and leftovers;
  - an unchanged foreign-resource check.
- **Given** any driver exit, **when** it finishes, **then** `list` shows no issued run and no new container remains.
- **Given** the summary README, **when** read, **then** it links the 2.8 runs and states that the CI part and the accepted-tool record wait for 2.14 and CiCd story 21.

**Remaining CI and tool qualification**

- [ ] After Story 2.14 and CiCd story 21, record one success and one failure in normal Parties CI through the shared CiCd workflow. In each run, verify that the always-run cleanup step stops the job's owned runs and reports leftovers. Remaining owned resources mean cleanup is incomplete.
- [ ] Once the local reference scenarios and the required CI evidence pass, record Platform tool acceptance and pin the released version in Platform and Parties under Story 2.14's version compatibility rule.

The original Story 2.8 runs supply both hosted cancellation cases. Each new candidate merge or release decision requires a fresh qualifying run under the [candidate evidence and attachment policy v1](../../docs/contracts/candidate-and-attachment-policy-v1.md).

## Implementation Notes

**2026-11-27: Builds `main` contains the source fix for `HXT006`.**

- Builds commit `028d886371c830b77bffd9bac379051492187a25` issues attached-suite tokens with the enrolled `g4-native-tests` subject and an attachment-specific `jti`. The JWT expiry is bounded by the attachment hold and the execution deadline. Tenants uses its existing membership check.
- The Builds access termination report and README now describe the shared subject, attachment-specific token ID, and credential scope. A disposable build of the Builds candidate with those changes passed all 67 `AttachedSuiteExecutionTests` and `CompositionRedactionTests` cases.
- Builds CLI `6.9.1` predates the fix. The live campaign still requires a published CLI that contains the fix, compatible pins in the reference composition, and a quiescent Docker host.

**2026-11-27: the driver now stops the campaign when an attached suite exits unsuccessfully before readiness.**

- The first failed row keeps the available diagnostics, runs cleanup, and marks the remaining rows `not-run`. Readiness timeouts while the process is still running and controlled failures after readiness retain their existing handling.
- All 24 driver unit tests passed. The pinned Platform, Builds, and Parties revisions still contain the `HXT006` authorization mismatch described below. Live qualification and Step 4 review remain pending until an approved tuple resolves the mismatch and Docker can stay quiescent throughout the run.

**2026-11-27 (renegotiation 2): rows 1–6 functionally verified on Parties `869bd043` / Platform `v2.9.0` / Builds `6.9.1`; rows 7–9 blocked by attached-subject Tenants authorization defect (`HXT006`).**

- **Verified inputs & package prep:** Confirmed `hexalith.builds.module.cli` `6.9.1` is the latest published version on NuGet. Restored CLI reported `6.9.1+91cb7cba990ffdf82930f9bf8cb68aef41baba76` (`toolContentIdentity: sha256:69782f5e263a37ccf845654b7442eaac9f00fe2ae9b15a432467f7b0ac4e1b4d`) against Parties `869bd04362f914a05a010c039227f668c8ad903f` and nested Builds `91cb7cba990ffdf82930f9bf8cb68aef41baba76` using isolated Dapr home `~/.hexalith/dapr/cli-1.19.1-runtime-1.19.2`. `prepare-build --mode package` (`mappingHash: cbc1751a10cbb20021f794f69ffd8e86426e7fae81bf038d08f7dd722ff3747e`) and Release builds of both suites (`Hexalith.EventStore.Testing` `5.6.0`) passed.
- **Driver fix (`startup-timeout`):** Live `startup-timeout` runs (`fd172733...`, `24c1038f...`) exposed a driver bug: `startup_timeout()` read only AppHost-written `diagnostics/<runId>/startup-failure.json`, which a `SIGSTOP`ped AppHost cannot write, missing runner-CLI-written `diagnostics/<runId>/summary.json` (`hexalith.g4-startup-report.v1`, which recorded `ruleId: HXR021` and all 22 unready `resources`). Updated `eng/lifecycle-qualification/scenarios.py` with `startup_failure_facts(failure, report)` to fall back to `summary.json` (projecting bounded resource names) and added unit tests in `eng/lifecycle-qualification/test_scenarios.py` (`22/22` passing).
- **Live results for rows 1–6 (all functional runner behaviors verified):**
  - `success` (`e54ea33e...`): passed (`passed` / `tests-completed`, 67.3 s startup, `not-retained -> complete`, 0 leftovers, `foreign.unchanged: true`).
  - `failure` (`8609d26f...`): passed (exit 4, `failed` / `test-failure`, `failure-retained` with 13 containers -> `down` -> `released -> complete`, 0 leftovers, `foreign.unchanged: true`).
  - `startup-timeout` (`fd172733...`, `24c1038f...`): all runner checks passed (`HXR021`, `startup-timeout`, effective 600 s, actual 600.1 s / 600.5 s, `not-overridden`, 4 partial containers retained until `down`, `firstOutcomeStable: true`, `released -> complete`, 0 leftovers, `foreign.unchanged: true`); failed only the pre-fix `unready-recorded` check before the `startup_failure_facts` fix.
  - `cancellation` (`8f40c7b2...`, `e6252d80...`): all functional checks passed on both attempts (exit 130, `cancelled` / `run-cancelled`, 0 containers at exit, `not-retained -> complete`, 0 leftovers); failed only `foreign-unchanged` due to concurrent host Docker activity.
  - `collision` (`7075c620...`/`8bfae2b1...`, `32c2533b...`/`1add935e...`): all functional checks passed on both attempts (peer refused with `HXR031` naming first run, first unaffected and passed, 0 leftovers); failed only `foreign-unchanged` due to concurrent host Docker activity.
  - `owner-finish-hold` (`05361fbf...`, `e214c32f...` in follow-up probe): all 8 functional checks passed (`holdAdmitted`, `passedWhileHeld`, `cleanupPending`, `refusal: ["HXR035", "owner-passed"]`, `holdEnded`, exit 0, `passed`, `complete`, 0 leftovers); failed only `foreign-unchanged` due to concurrent host Docker activity (the earlier campaign attempt `847b4084...` timed out at 600 s on `memories-access-telemetry-clock` under host load ~63).
- **Product defect blocking rows 7–9 (`owner-cancel-attached`, `attached-failure-expiry`, `failure-then-cancellation`):**
  - All 6 attached-suite runs (`72cfbebb...`, `305ab478...`, `f9baef36...`, `cd1e2926...`, `ae836ceb...`, `239c9a53...`) failed reproducibly in 3.1–6.9 s with exit 4 (`HXT006`, `test-failure`) before publishing `execution-ready.json`.
  - Root cause:
    1. `src/Hexalith.Platform.Composition/PlatformNativeTestPreparation.cs:168` enrolls only `"g4-native-tests"` as `TenantRole.TenantReader` on the synthetic tenant (`g4t-<ownerRunId>`).
    2. Builds `6.9.1` (`references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:480`) mints attached-suite bearer tokens with narrowed non-admin subject `"g4-attached-tests:" + attached.RunId`.
    3. Tenants `GetTenantQueryHandler` (`references/Hexalith.Tenants/src/Hexalith.Tenants/Queries/Handlers/GetTenantQueryHandler.cs:56`, `TenantQueryHandlerBase.cs:561-567`) authorizes `GET /api/tenants/{tenant}` strictly by tenant membership (`model.Members`) or global-admin membership, refusing `"g4-attached-tests:<attachedRunId>"` with `403 Forbidden`.
    4. Parties `AttachedLifecycleQualificationTests.ReadServicesAsync` (`references/Hexalith.Parties/tests/Hexalith.Parties.Platform.AttachedLifecycleTests/AttachedLifecycleQualificationTests.cs:25-27,53-59`) calls `GET /api/tenants/{tenant}` and `EnsureSuccessStatusCode()` before publishing `execution-ready.json`.
  - Per spec constraints, no Builds, Parties, Tenants, or Platform Composition source was modified.
- **Cleanup & host-isolation note:** Every issued run cleaned up completely (`listedIssuedRuns: []`, `issuedContainers: 0`, zero `hexalith.g4.run` Docker containers remaining). The scratch worktree, temporary work/probe directories, and partial evidence directory were removed; no partial evidence was committed. Although Docker was quiescent at campaign start, concurrent unrelated suites (FrontComposer Playwright, Memories/Tenants Testcontainers, CiCd) started mid-run, mutating host containers (`foreign.unchanged: false`) and driving host load to ~63. A clean 9/9 live run requires resolving the attached-subject / Tenants-authorization product defect and running with host Docker quiescence.

**2026-11-27: manifest-driven suite resolution and package prep passed on Parties `ca637a01` / CLI `6.5.1`; live startup blocked by `parties` `3.1.1` route-catalog disagreement (`HXR021`).**

- **Driver & tests:** `eng/lifecycle-qualification/scenarios.py` now resolves `lifecycle-package` and `attached-lifecycle-package` project paths from `module/hexalith.module-manifest.v2.json` (`manifest_suites`), replacing hardcoded `SUITES`. `eng/lifecycle-qualification/test_scenarios.py` adds 3 unit tests covering renamed/legacy manifests, invalid/unsafe entries, and `Driver.prepare()` invocation (`21/21` passing).
- **Local Dapr prerequisite:** Provisioned `~/.hexalith/dapr/cli-1.19.1-runtime-1.19.2` (`dapr` 1.19.1, `daprd`/`placement`/`scheduler` 1.19.2) with all four archive SHA-256 digests verified against `055d7df5:Props/dapr-toolchain-sha256.json`; shell profiles unchanged.
- **Package preparation & admission:** Restored CLI reported `6.5.1+055d7df5b49669d5b705ce163b0b2e187ecb9a50` (`toolContentIdentity: sha256:f64afd15624a32942c1b4757245b598dfc44992334e946d6abc66a3255ad9f10`). `prepare-build --mode package` (`mappingHash: 91ea4254...`) and Release builds of both `Hexalith.Parties.Platform.LifecycleTests.csproj` and `Hexalith.Parties.Platform.AttachedLifecycleQualification.Tests.csproj` passed. Owner admission succeeded without `HXP020`.
- **Product defect in startup (`HXR021`):** Row 1 (`success`, run `5f22692a00b8d328c4fc17cd6aa462ca`) exited with code 3 (`HXR021`, `startup-timeout`, `effectiveSeconds: 600`, `actualDurationSeconds: 600.37`, `not-overridden`) and stopped the remaining 8 rows as `not-run`. A focused diagnostic probe (`2dcc644c63fc55fd90692e913d71f7dd`) captured the `parties` container logs and reproduced the exact root cause:
  - Builds `055d7df5` (`v6.5.1`) pins `HexalithPartiesVersion = 3.1.1`, selecting `registry.hexalith.com/parties@sha256:205e374548d3cac33457551f9c6f7b5f213db804ca58ae185332522833fe83c8` (built on `.NET 10.0.403`).
  - Platform `v2.2.5` (`0aacf132`) / CLI `6.5.1` (`.NET 11.0.100`) proposes a `.NET 11` route catalog generation. Four seconds after container start, `DevelopmentRouteCatalogBootstrapWorker` throws `Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException: The installed owner operation schemas or eligibility differ from the proposed generation.` and stops the `parties` host (`BackgroundServiceExceptionBehavior=StopHost`, exit 0); readiness also reports `route-catalog` `Reason=catalog-disagreement`. Twenty-one seconds later the sibling containers are terminated (`exit=137`) and startup times out at 600 s.
  - Builds `v6.8.5` and `v6.9.0` advance `HexalithPartiesVersion` to `4.0.1` (alongside EventStore `5.6.0`, Memories `4.1.2`, Tenants `8.1.0`), which requires an updated Platform release + owner lock adopted by Parties before a passing live rerun.
- **Cleanup & host-isolation note:** Both runs (`5f22692a...` and `2dcc644c...`) cleaned up completely with zero issued containers and zero listed runs; the scratch worktree and temporary work directories were removed, and no `0/9` evidence directory was committed. Concurrent unrelated suites (`Hexalith.Memories.IntegrationTests`, `Hexalith.Tenants.IntegrationTests`) were mutating non-owned Docker containers on this host during `5f22692a` (`foreign.unchanged: false`); the future live rerun also requires Docker quiescence.

**The latest-release requirement blocked the campaign on 2026-11-22.**

- Builds CLI 6.6.1 is published and available through NuGet. The approved Parties revision `d8d599e611c6a2999780907369ba70febfd266c2` pins CLI 6.4.4 and catalog `dc225a6969473c21c3dad6bda8f1a18ad0aaae0d`. The catalog fix first shipped in Builds 6.5.0. [Builds 6.6.1 release](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v6.6.1), [Builds 6.5.0 release](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v6.5.0)
- The current Platform gitlink, Parties `3480c5025873bec368ed6ebd74337166e249b706`, fixes attached-suite catalog selection by moving the suite to a new project name. It still pins CLI 6.4.4. A new approved Parties revision that pins the latest released CLI is required for the live campaign. No live row ran and no live evidence was written.
- The driver now fails the affected checks when Docker or runner inventory cannot be verified, including after final cleanup. The 18 resource-free tests passed, including sample verdicts for all nine rows. Live qualification remains open.

**2026-11-21: latest released CLI 6.4.4 stopped in package preparation.**

- The restored executable reported `6.4.4+fe68718b6c074a6b500a7191e3554a1d39e17688`. `prepare-build --mode package` and the owner suite build passed. The attached suite failed with nine `CS1061` errors for `OwnerRunId`, `EnvironmentId`, `OriginalAttachmentRequestId` and `AttachmentExpiresAtUtc`. Package-mode MSBuild evaluation confirmed that the catalog at Parties' Builds gitlink selects `Hexalith.EventStore.Testing` 4.2.0.
- No scenario or owner run started. The Docker inventory, runner state files and normalized `list` entries matched before and after. The scratch worktree was removed. No lifecycle summary was produced. The earlier HXP020 refusal was outside this attempt because the CLI never reached startup.
- Builds source commit `3be0f781` retires the old catalog view. Release v6.4.4 and the catalog pinned by Parties still contain it. The live rerun needs a released and adopted catalog with the fix. CI evidence and the accepted-tool record remain open.
- The driver now stops all remaining rows on a product refusal. Owner and collision-peer discovery uses the launched process's open provisioning lock, its AppHost process group or its own terminal result. Failed cleanup calls remain eligible for retry. Resource-free tests cover these changes. Live behavior remains unverified until package preparation succeeds.

**2026-11-18: driver delivered, live campaign stopped at two product defects.**

- **Driver:** `scenarios.py` (stdlib), its unit tests and the README are in place, and the 4.47.0 controller is deleted. The driver launches the restored 6.3.0 entry assembly directly, so each owner tool leads its own process group. Run ids come from new `g4-runs` states bound to the manifest hash and to the driver's own AppHost process group. It is 733 lines, more than the ~300 estimate, because each of the nine rows has its own checks.
- **Defect 1, HXP020:** the owner `lifecycle-package` run is refused at startup with "The ordinary Parties package profile requires its trusted parent plan/key handoff and unchanged source/test inputs" (exit 3, `startup-failure`, after about 158 s). This reproduced on two owner runs, `76555c8ae12daa014a8d0bcd10ceec7a` and `460954eea8dba5b234d860a11f2b7116`. Both cleaned up completely with no leftovers, and foreign containers and `list` entries were unchanged. Story 2.14 owns this allowlist; nothing was patched.
- **Defect 2, attached suite does not compile in package mode:** Builds 6.3.0 `Props/Directory.Packages.props` lists `Hexalith.Parties.Platform.AttachedLifecycleTests` among the "measured retained SDK4.2 consumers" (`HexalithPartiesSdk42CatalogView`), so it resolves `Hexalith.EventStore.Testing` 4.2.0. Parties `AttachedLifecycleQualificationTests.cs` (since `e35b8c1e`) uses `TestEnvironmentDescriptor.OwnerRunId`, `EnvironmentId`, `OriginalAttachmentRequestId` and `AttachmentExpiresAtUtc`, which first ship in 5.0.0, so the build fails with CS1061 (9 errors). This blocks the three attached-suite rows. Builds `main` (`eae4531a`) still lists the project. The fix belongs to the Builds catalog or Parties and needs a release under 2.14's version rule.
- **Local prerequisites found:**
  - 6.3.0 selects Dapr CLI and runtime 1.19.1. The shell's `HEXALITH_DAPR_HOME` (1.18.2 and 1.18.7) is refused with HXR012. An isolated home was created at `~/.hexalith/dapr/cli-1.19.1-runtime-1.19.1` from the archives pinned in `Props/dapr-toolchain-sha256.json`; shell profiles are unchanged.
  - The tool's workspace resolver initializes the worktree's nested `references/Hexalith.Platform` itself, from GitHub, during `prepare-build` and `test`.
- **Rerun** after 2.14 and a catalog fix: follow `eng/lifecycle-qualification/README.md` with the latest released CLI and write the summary to the evidence directory named in the execution task.
- **Parked (2026-11-18, user decision):**
  - Verified: the current Parties declaration `dfb03b6e…` is absent from `IsOrdinaryDeclaration` (`src/Hexalith.Platform.Composition/PlatformPartiesPackageCatalogQualification.cs:21`). That method admits only `40cee6ce…` and `d9c193b0…`, and Story 2.14 removes it.
  - The attached-suite catalog defect is in `deferred-work.md`.
  - The work is committed locally without a push. Step 04 review is deferred to the live rerun, after 2.14 and a Builds release carrying the catalog fix. The status stays `in-progress`.

## Spec Change Log

- 2026-11-21: The owner directed the campaign to use only the latest released CLI. The inputs and evidence path now identify the 6.4.4 campaign. The earlier 6.3.0 defects remain historical observations.
- 2026-11-27: Owner renegotiated `<frozen-after-approval>` to run live on current Parties `ca637a01829c57d9163fb686a56bc82b094a10d9` with its signed pinned Builds CLI `6.5.1` and catalog `055d7df5b49669d5b705ce163b0b2e187ecb9a50` (relaxing latest-only because `6.8.5`+ trips HXP020 against Platform v2.2.5's owner lock). Added manifest-driven suite discovery in `scenarios.py`, isolated Dapr `cli-1.19.1-runtime-1.19.2` home provisioning, and evidence output path `evidence/2-10/local-6-5-1-20261127/`.
- 2026-11-27 (renegotiation 2): After Platform `v2.9.0` (`a670a4c5`) and Parties `869bd04362f914a05a010c039227f668c8ad903f` adopted Builds `v6.9.1`, the owner renegotiated `<frozen-after-approval>` to pull `references/Hexalith.Parties` to `869bd04362f914a05a010c039227f668c8ad903f` (including the updated Platform gitlink), restore the latest-released CLI rule (`6.9.1` with nested Builds catalog `91cb7cba990ffdf82930f9bf8cb68aef41baba76`), confirm Docker quiescence, run all nine rows live with isolated Dapr home `cli-1.19.1-runtime-1.19.2`, verify zero residue and `foreign.unchanged=true`, and write evidence to `evidence/2-10/local-6-9-1-20261127/`.

## Review Triage Log

## Verification

**Commands:**
- `python3 -B -m unittest discover -s eng/lifecycle-qualification -v` -- expected: all pass
- `python3 -B eng/lifecycle-qualification/scenarios.py --checkout <worktree> --evidence <dir>` -- expected: exit 0, nine rows `passed`
- `dotnet tool run hexalith-module list --output json` (in the worktree) -- expected: no issued run ids
