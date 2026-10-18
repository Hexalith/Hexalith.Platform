---
title: '1.9 Ratify and pin the first Platform-accepted tool version'
type: 'chore'
epic: 1
story: 9
created: '2026-10-17'
status: 'in-review'
baseline_commit: 'b49d5c1cbfc79e94d92d6228e7d2b3c4f65b56f9'
builds_baseline_commit: 'c8c72dcab8e1b05520ff9445d9df105f084f9813'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Stories 1.1–1.8 are merged in Builds, but no `hexalith-module` version is Platform-accepted. Platform has no `.config/dotnet-tools.json`, and its `hexalith-platform.json` has no `toolVersionRange`, so package mode refuses with HXW010 and source mode warns. Builds `main` CI is also red, so the release gate cannot pass.

**Approach:** Turn Builds CI green, then release the next version (expected 4.33.0) through the approval-gated Release workflow. Platform then pins that exact version, sets the supported range, moves its Builds gitlink to the release-tagged commit, and records which versions are and are not accepted.

## Boundaries & Constraints

**Always:** Accept only a version that semantic-release actually published to NuGet.org. Platform's Builds gitlink must equal the commit the release tag points to, so the catalog commit and the released tool's embedded catalog snapshot are the same. Record as not accepted every published version below the accepted one: 4.20.0–4.27.4, plus the post-1.8 releases 4.29.1–4.32.0. `hexalith-platform.json` stays schema-valid; it holds only `schemaVersion` and `toolVersionRange`, and the prose record lives in docs. Builds owns the CI fix and its README sentence; Platform owns acceptance.

**Never:** Add the blocking CI tier (Story 2.8). Pin `hexalith-evidence`; its accepted Evidence-suite blocker stands, and Story 3.9 owns evidence. Edit other modules' tool manifests (Stories 1.12/1.13). Change the identity schema or reader, catalog versions, or release workflow gates. Weaken or skip the failing catalog test. Publish manually around semantic-release.

**Decisions (2026-10-17):**
- **Range.** `toolVersionRange` is `[<accepted>, 5.0.0)`. Later 4.x releases are accepted without a Platform edit. `.config/dotnet-tools.json` still pins the exact accepted version.
- **Release.** Claude dispatches `build-release.yml` once CI is green. The human approves the `production` environment gate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Accepted tool, Platform root | Restored pinned tool, HEAD carries the range and the gitlink | Identity resolves with no HXW010; catalog pin = release commit | N/A |
| Older tool | 4.32.0 or below run against Platform | Package mode refuses with HXW010; source mode warns | Names version, range and record |
| CI stays red after fix | Another failing step on the release candidate | Do not release; fix in Builds and re-run | Halt for a human if not attributable to this story |
| Version differs | semantic-release computes something other than 4.33.0 | Use the actually published version everywhere | N/A |

</frozen-after-approval>

## Code Map

Builds paths are under `references/Hexalith.Builds`.

- `Tools/test-platform-version-catalog.ps1:185-202`: the baseline tooling rebuild copies only `schemas/hexalith.module-manifest.v2.json`. `Hexalith.Builds.Tooling.csproj:29` also embeds `schemas/hexalith.module-run-evidence.v1.json`, so CI runs 39042931334 and 39050116291 fail with CS1566. Copy every schema the csproj embeds; derive them from the csproj rather than hard-coding a second list. Run 39055666717 (`c8c72dc`) was still in progress at planning time.
- `.github/workflows/build-release.yml`: `workflow_dispatch` only. `Tools/verify-g4-release-source.sh` requires a green push CI at the exact `main` SHA, and the `production` environment requires reviewer `jpiquot`. semantic-release creates the tag on that SHA, with no release commit. Commits since `v4.32.0` contain `feat`, so the expected version is 4.33.0.
- `README.md:326-329` (Builds): "Versions up to and including `4.27.4` are not Platform-accepted… Story 1.9 owns the first Platform tool acceptance." Before the release, reword it: Platform's `hexalith-platform.json` range is authoritative, and versions up to and including 4.32.0 are not accepted. Do this before the release so the tagged commit carries it without naming its own version.
- `src/libraries/Hexalith.Builds.Tooling/Workspace/PlatformIdentityResolver.cs:264-275`: the reader rejects unknown fields and invalid NuGet ranges. HXW010 is the range check. Reuse it as-is.
- `{project-root}/hexalith-platform.json`: currently `{"schemaVersion": 1}`. Add `toolVersionRange`.
- `{project-root}/.config/dotnet-tools.json` (new): `hexalith.builds.module.cli` → exact version, `commands: ["hexalith-module"]`, `rollForward: false`. Follow the format in `references/Hexalith.Parties/.config/dotnet-tools.json`.
- `{project-root}/references/Hexalith.Builds` gitlink: currently `c8c72dc`. Move it to the release-tag commit.
- `{project-root}/README.md`: add a short "Module tool" section with the accepted version, the range, the catalog commit, the not-accepted versions, and how to restore.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds/Tools/test-platform-version-catalog.ps1` -- copy every csproj-embedded schema into the rebuild copy -- unblocks the release CI.
- [x] `references/Hexalith.Builds/README.md` -- reword the acceptance sentence as above -- the release commit carries the correct record.
- [x] Builds (local only): run the catalog test locally until all cases pass, then make one local commit, `fix(ci): copy embedded schemas into catalog rebuild test`, with the two files above. **Stop here.** The implementation agent never pushes or runs remote operations.
- [x] *(Orchestrator, remote)*: push Builds `main` and wait for green push CI. Dispatch `build-release.yml` for human gate approval, wait for NuGet.org to list the version, and fetch the tag -- produces the accepted version.
- [x] `{project-root}/.config/dotnet-tools.json`, `hexalith-platform.json`, Builds gitlink, `README.md` -- after the release only: pin, set the range, move the gitlink to the tag commit, and record the versions -- the Platform acceptance. Concurrent Story 5.4 work has uncommitted Platform files (`global.json`, `src/Hexalith.Platform.Composition/*`, `eng/*`, tests). Never modify, stage or revert them; stage only this story's paths.

**Acceptance Criteria:**
- Given the release, when it completes, then the NuGet listing contains the version and the Builds tag points to the Platform gitlink commit.
- Given the Platform repository at the new HEAD, when `dotnet tool restore` runs, then `hexalith-module` at the accepted version is restored, and the identity resolution reports no HXW009/HXW010.
- Given the Platform README, when read, then it names the first accepted version, the range, the catalog commit, and the not-accepted versions 4.20.0–4.27.4 and 4.29.1–4.32.0.

## Implementation Notes

- 2026-10-17, Builds local commit `254aed4` (`fix(ci): copy embedded schemas into catalog rebuild test`, parent `c8c72dc`, not pushed). The rebuild step now evaluates `Hexalith.Builds.Tooling.csproj` with `dotnet msbuild -getItem:EmbeddedResource` and copies every evaluated resource outside the tooling directory to the same Builds-relative path in the isolated copy; it refuses a resource outside the Builds checkout or missing on disk. The hard-coded manifest-v2 copy and the pre-created `schemas` directory were removed. `pwsh -NoProfile -File Tools/test-platform-version-catalog.ps1` passed locally: 96 scenarios, exit 0. Commit message validated with the repository's commitlint (`@commitlint/cli` 21.2.3).
- 2026-10-18 resumed verification: Builds `main` is at `544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`, which contains the CI fix. The catalog test again passed all 96 scenarios with exit 0. The local `origin/main` tracking ref points to that commit, but no release tag matches it in the local checkout. Platform remains unpinned, so release and acceptance criteria are still open.
- 2026-10-18 release verification: Builds `main` and green push CI [39061726126](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39061726126) both use `544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`. The existing approval-gated [release run 39063443489](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39063443489) completed successfully. NuGet.org lists `hexalith.builds.module.cli` 4.33.0, and fetched tag `v4.33.0` points to that same commit. The Platform HEAD gitlink already pointed there, so no gitlink edit was necessary.
- Platform commit `59f6c25990afc5439070046ed3d3e77f671d9f4a` adds the exact 4.33.0 local tool pin, range `[4.33.0, 5.0.0)`, and acceptance record. `dotnet tool restore --no-http-cache` restored the package after the first plain restore encountered stale NuGet metadata; a subsequent plain restore passed. The tool reports `4.33.0+544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`.
- Package mode against the EventStore reference manifest passed Platform identity and stopped later at unrelated declaration provenance `HXP020`; `down` completed successfully. An isolated 4.32.0 tool refused package mode with HXW010 and warned in source mode with HXW010 before source mode stopped at missing `HEXALITH_DAPR_HOME` (`HXR011`). The temporary older tool was uninstalled.
- 2026-10-18 independent re-check on top of `59f6c25`: the README also states that a tool outside the range is refused with `HXW010` in package mode and warned in source mode. At the local HEAD carrying that README sentence, `dotnet tool restore` restored 4.33.0 and `--version` printed `4.33.0+544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`. `run --mode package --manifest src/Hexalith.Platform.Composition/Declarations/eventstore.json --output json` exited 3 with only `HXP020` (EventStore declaration provenance 3.120.0 vs catalog 3.124.0), with no HXW009/HXW010 and no identity warnings. The run resolves the workspace mapping, including Platform identity, before it loads the declaration closure. `down` on the same manifest exited 0 (`HXI001`). `git -C references/Hexalith.Builds describe --exact-match --tags HEAD` printed `v4.33.0`, and the HEAD gitlink is `544fd2e`.

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `pwsh -NoProfile -File Tools/test-platform-version-catalog.ps1` (in Builds) -- expected: all cases PASS.
- `gh run list --workflow ci.yml --branch main --limit 1` (Builds) -- expected: success on the release SHA.
- `curl -s https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/index.json` -- expected: lists the accepted version.
- `dotnet tool restore && dotnet tool run hexalith-module --version` (Platform root) -- expected: the accepted version.
- From the Platform root, run `dotnet tool run hexalith-module run --mode package` on a reference declaration, then `down` -- expected: no HXW009/HXW010 identity findings. A later non-identity failure is acceptable and recorded.
- `git -C references/Hexalith.Builds describe --exact-match --tags HEAD` -- expected: `v<accepted>`.

**Observed 2026-10-18:** Catalog test 96/96 pass; push CI and release workflow success; NuGet listing includes 4.33.0; `dotnet tool restore` success; `dotnet tool run hexalith-module --version` reports 4.33.0 with the tag commit; accepted package-mode invocation has no HXW009/HXW010; 4.32.0 package mode returns HXW010 and source mode warns HXW010; `down` succeeds; Builds HEAD has exact tag `v4.33.0` and matches the Platform HEAD gitlink.
