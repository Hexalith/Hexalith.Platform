# Story 2.14 (Builds slice) verification

Local, pre-landing observations from 2026-11-18. Hosted tuple CI on main is still
required after landing. These local runs grant no hosted, image or runtime
acceptance.

- [Adoption rehearsal, Builds 6.3.0 → 6.3.1](rehearsal-6.3.0-to-6.3.1/README.md):
  the adoption diff is the pins, the gitlink and the lock only. At 6.3.1 only the
  rehearsal steps 03–10 ran, and they passed; the three tuple CI profiles did not
  run at 6.3.1. Tag drift, a stale lock and a tampered package refuse on real
  data. The rehearsal was not landed.
- [Current-pin scan](pin-scan.json) ([script](pin-scan.py)): 942 files in `src/`,
  `eng/*.py`, `README.md`, `docs/*.md` and `eng/*.md` contain no identity of the
  current pin. That covers the version, gitlink, catalog blob and SHA-256, archive
  SHA-256, lock `contentHash`, and the AppHost/Tooling/EventStore host member
  hashes. The only Builds tag commits left in `eng/*.py` are five historical
  fixture and control subjects in tests.

## Local tuple CI profiles

The three profiles ran with `eng/run_tuple_ci.py` in a clean throwaway worktree.
It held a detached snapshot of `feat/2-14-builds-version-pins` plus this
implementation, with all 18 gitlinks unchanged. The CI-only guard was satisfied
locally with `GITHUB_ACTIONS=true CI=true PLATFORM_CANDIDATE_SHA=<snapshot>`, so
these are local simulations. `resolve-sources` steps passed first: owner source
selection, `verify_checkout` and the Builds tag check.

| Profile | Groups | Executed | Passed | Failed, errors, skipped, not run |
| --- | --- | --- | --- | --- |
| current42 | 34 | 487 | 487 | 0 |
| base42 | 3 | 93 | 93 | 0 |
| extensions | 1 | 9 | 9 | 0 |

That is 589 executions, all passed. The before/after test-output snapshots were
unchanged in every profile. [local-profiles-summary.json](local-profiles-summary.json)
has the binding, lock, gitlink catalog identity and AR-12 identities of each run.

Each profile ran the real `prepare_builds_artifacts.py --require-recorded`. That
covers the lock, `dotnet nuget verify --all`, the tag and release marker,
`--version` = `6.3.0+7afc4981…` and the per-file receipt. The runs then did a
package-only restore and a Release build, and executed the Composition DLL from
`bin/Release/net11.0/`. AR-12 (`buildsArtifactIdentities` in each result):
the expected SHA-256 of the lock-verified archive member equals the loaded build
output, for both `Hexalith.Builds.Tooling.dll` and
`Hexalith.Builds.Module.EventStoreHost.dll`.

**Environment note.** The first current42 attempt failed group 11,
`PlatformCurrentReferenceCohortTests`. This machine's shared NuGet cache holds a
non-genuine `Hexalith.McpCli.Abstractions 2.0.0`, restored on 2026-11-02 from a
local `/tmp/story-2-14-packages` source. Its DLL SHA-256 is `8f639ef9…`; the
nuget.org package's is `fc59fc5b…`, which is what the source expects. The
profiles above therefore used an isolated, freshly restored `NUGET_PACKAGES`.
The shared cache was not modified. Hosted CI restores into a fresh cache.

## Other checks

- `python3 -B -m unittest discover -s eng -p 'test_*.py'`: 343 tests passed, zero
  skips. Baseline before the change: 376 tests with 2 errors, both from the
  CLI-only modules that now opt out of bare discovery. The drop is the deleted
  per-generation fixture controls (6.0.0 through 6.3.0 bindings, catalog tables
  and Builds-version branches) plus those two discovery errors.
- `npm run test:source-release`: 84 passed, zero skips.
- `python3 eng/run_tuple_ci.py --audit-only --suite current42`: `graph-verified`
  with 31 projects.
