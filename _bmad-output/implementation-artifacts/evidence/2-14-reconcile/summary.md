# Story 2.14 reconcile slice: verification summary

Spec: [spec-2-14-reconcile-the-five-dependency-revisions.md](../../spec-2-14-reconcile-the-five-dependency-revisions.md) (audit action 1).
These are local observations from 2026-11-20, made before landing. Hosted tuple CI on the landed commit is still required.

## Revisions

| Dependency | Before (`origin/main` `b8048739`) | After |
| --- | --- | --- |
| Builds | gitlink `99baf1db` (v6.4.1); pins 6.3.2; floor `[6.3.2, 7.0.0-0)`; catalog blob `29dcf83b` | gitlink `dc225a69` (v6.4.3); pins 6.4.3; floor `[6.4.3, 7.0.0-0)`; catalog blob `eca86204` |
| EventStore | binding `8a0e3613`; gitlink `2ec21f63` (v5.4.0) | binding `2ec21f63` |
| Parties | binding `6c34cfa9`; gitlink `fc509479` | binding `fc509479` |
| Tenants | binding `e1e21fe4`; gitlink `d04974cf` | binding `d04974cf` |
| Memories | binding `2b30558b`; gitlink `0856785c` (v4.1.0) | binding `0856785c` |

- Only the Builds gitlink moves. The binding changes only the four `sourceRevision` values; declaration SHA-256s and `packageProvenanceSha256` stay the same.
- `eng/builds-tool.lock.json` names `Hexalith.Builds.Module.Cli` 6.4.3. It was written after `dotnet nuget verify --all` passed and the hash matched nuget.org.
- `eng/owner-packages.lock.json` is unchanged: `--check` reports `current` for 18 packages against catalog blob `eca86204`.
- The Builds range `99baf1db..dc225a69` changes these net11 catalog defaults:

  | Package | Before | After |
  | --- | --- | --- |
  | CommunityToolkit Aspire Dapr | 17.0.0-beta.1052 | 17.0.0-beta.1078 (`apphost.cs` pins it since `0e922c8c`) |
  | EventStore | 5.3.1 | 5.4.0 |
  | Memories | 4.0.0 | 4.1.0 |
  | Conversations | 2.0.0 | 2.1.0 |
  | FrontComposer | 6.0.0 | 6.0.1 |

  The SDK 4.2 view versions are unchanged. The range also carries one code change, Builds `fix(release): keep isolated fixture project versions`.

## Commits

- `527c3fec` `test(ci)`: the source SDK guard in `eng/test_source_sdk_propagation.py` now accepts any parseable default of at least 4.1.0. Versions are read as in `tests/catalog-builder/verify_sdk_gates.py`. A new control refuses older and unparseable versions, which brings the file to 30 controls.
- `build(deps)`: the pins, the Builds gitlink, the two regenerated files, this summary and the sprint-status line.

## History

1. The first attempt used Builds v6.4.2 on `e899cf23`. It stopped at `source-sdk-propagation-controls` because the fixed list `("4.1.0", "4.2.0", "5.1.0", "5.2.0", "5.3.0")` refused the catalog default 5.3.1. That default has been in Builds since v6.3.3 (`e3ab0a85`). The owner chose the version-range guard.
2. With the guard, v6.4.2 passed all three suites on `e899cf23`. In the meantime `origin/main` gained `0e922c8c` (`apphost.cs` Dapr 1078) and `b8048739`. Only v6.4.3's catalog selects Dapr 1078, and `verify-platform-pins.py` requires the two to match, so the owner retargeted to v6.4.3.

## Commands

The worktree was on a detached HEAD with a clean tree, `MSBUILDDISABLENODEREUSE=1` and an isolated `NUGET_PACKAGES`. The step names are the ones in `tuple-ci.yml`.

| Check | Result |
| --- | --- |
| `eng/test_current_tuple_source.py`; `read_selection`, `verify_checkout` | 16 passed; pass |
| `eng/builds_tuple.py` = Builds checkout; `eng/builds_publication.py --revision HEAD` | `dc225a69`; v6.4.3 names `dc225a69`, release 451899736 |
| `eng/owner_packages_lock.py --check`; `eng/test_owner_packages_lock.py` | `current`, 18 packages; 11 passed |
| `npm run test:source-release`; `eng/test-extension-release/gate.test.mjs`; `eng/test-extension-release/run-controls.py` | 84; 14; 30 passed |
| `eng/test_tuple_sdk_suites.py`; `eng/test_builds_publication.py`; `eng/test_builds_tool_lock.py`; `eng/test_bound_builds.py` | 21; 13; 8; 12 passed |

## Tuple suites

The suites ran on `0f4281988e6ae759b4bb0ba5d948a85b98f6617a` with this command:

`NO_COLOR=1 TERM=dumb python3 eng/run_tuple_ci.py --evidence artifacts/2-14-reconcile/<suite> --suite <suite> --eventstore-sdk 4.2.0`

The CI guard was set locally (`GITHUB_ACTIONS=true CI=true PLATFORM_CANDIDATE_SHA=0f4281988e6ae759b4bb0ba5d948a85b98f6617a`), so these runs are local simulations. That commit later gained only this summary, the sprint-status line and one comment line in `eng/test_source_sdk_propagation.py`.

On the final stack, these were rerun: `read_selection`, `verify_checkout`, `owner_packages_lock.py --check` and `builds_publication.py`. The 30 source SDK controls were also rerun on their own against the v6.4.3 tool root, all passing at the 5.4.0 default.

"Groups" counts the test project selections in `tuple-ci-result.json` `tests`. `--eventstore-sdk 4.2.0` selects the SDK 4.2 package view that the collectors use. The source SDK controls separately read the net11 source default, 5.4.0.

| Suite | Groups | Executed | Passed | Failed, errors, skipped, not run |
| --- | --- | --- | --- | --- |
| current42 | 32 | 380 | 380 | 0 |
| base42 | 3 | 67 | 67 | 0 |
| extensions | 1 | 9 | 9 | 0 |

All 456 executions passed. Each run did these steps:
- the real `prepare_builds_artifacts.py --require-recorded`;
- `pin-alignment`;
- the 30 source SDK controls;
- a package-only restore and a Release build;
- the tests on `net11.0`.

The AR-12 hashes matched for Tooling and EventStoreHost, and the output snapshots did not change.

## Hosted tuple CI and release

The run links go here after landing:
- tuple CI (`resolve-sources`, `tuple`, `tuple-verdict`): pending;
- `source-release.yml`: pending. Main already carries the `fix` commits `0e922c8c` and `b8048739`, so a green tuple run releases v2.2.3, as the owner accepted on 2026-11-20.
