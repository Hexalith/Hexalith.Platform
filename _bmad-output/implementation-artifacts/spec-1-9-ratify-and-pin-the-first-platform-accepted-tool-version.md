---
title: '1.9 Ratify and pin the first Platform-accepted tool version'
type: 'chore'
epic: 1
story: 9
created: '2026-10-17'
status: 'done'
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
- **Range.** `toolVersionRange` is `[<accepted>, 5.0.0-0)`. Later 4.x releases are accepted without a Platform edit, including 4.x prereleases; every 5.0 prerelease is excluded (human decision 2026-10-18, superseding `[<accepted>, 5.0.0)`). `.config/dotnet-tools.json` still pins the exact accepted version.
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

- blind: Agents UI is added as an Aspire project although its Razor SDK project has no executable output — `medium` — verified in `Hexalith.Agents.UI.csproj`; rejected for 1.9 because this is pre-existing Story 5.4 composition work, outside tool acceptance.
- blind: normal Agents composition passes null private capabilities — `medium` — `TryCompose` calls `Compose(..., null)`; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: AppHost private service registrations do not reach the launched Agents server — `medium` — `BindPrivateCapabilities` registers on the AppHost service collection; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: `AgentsHostComposition.AdmitAsync` has no production caller — `medium` — the Agents server still registers `UnavailableAgentTrustedCommandAdmission`; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: admission checks spool readiness but does not persist an observation before command construction — `medium` — the method only calls `IsReadyAsync` around verification; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: Agents server lacks an EventStore base URL binding — `medium` — the composition sets no `Agents__EventStore__BaseUrl`, while the server requires the configuration key; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: public `Compose` checks only four credential slots — `medium` — other required profile fields can be empty through that public entry; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: an opaque short string may be an inline credential — `maybe-false` — current validation rejects known secret markers but a reference syntax contract is needed to distinguish opaque references from tokens; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: Full verifier shell does not pass a profile — `medium` — `eng/verify-agents-host.sh` invokes `verify_full.py` without `--profile`; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: category and overall Full evidence can disagree about blocked status — `medium` — category `blocked` is `bool(gaps)` while `blocked.json` is always true; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: TRX failures can count as a successful suite — `medium` — `run_suite` records outcomes but tests only process exit and presence of result nodes; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: category evidence can contain no matched tests — `medium` — `write_evidence` writes empty `matchedTests` without a guard; rejected for 1.9 as pre-existing Story 5.4 work.
- blind: packet writer accepts a clean unrelated Git repository — `low` — `prepare` checks cleanliness and SHA format but no Platform repository identity; rejected for 1.9 as pre-existing Story 5.4 work.
- edge: Boolean timing values can pass as integers — `false` — Python equality cannot equate `True` or `False` to any accepted timing value, all of which exceed 1.
- edge: direct `Compose` can receive empty tenant or protection fields — `medium` — its four-slot guard does not validate those fields; rejected for 1.9 as pre-existing Story 5.4 work.
- edge: enabling Agents with Works or Timesheets skips the latter lane — `medium` — `ComposeEntry` returns immediately after successful Agents composition; rejected for 1.9 as pre-existing Story 5.4 work.
- edge: four present bindings can mask other missing profile references in Full evidence — `medium` — `binding_gaps` checks only four keys; rejected for 1.9 as pre-existing Story 5.4 work.
- edge: missing `dotnet` escapes Full verification before evidence is written — `medium` — `run_suite` catches timeout but not `OSError`; rejected for 1.9 as pre-existing Story 5.4 work.
- edge: failing suites can leave category evidence marked unblocked — `medium` — category status ignores suite exit codes; rejected for 1.9 as pre-existing Story 5.4 work.
- verification-gap: EXT-HOST-1 Python tests are absent from the host verification command — `medium` — the shell lists only two older unittest modules, while the reviewer ran the five new tests directly; rejected for 1.9 as pre-existing Story 5.4 work.
- verification-gap: Full evidence tests do not assert category matches — `medium` — the existing test asserts effects and gaps only, and its fixture supplies one ACL-named result; rejected for 1.9 as pre-existing Story 5.4 work.
- verification-gap: no repeatable Platform check compares the pinned package and accepted range — `medium` — Builds tests use synthetic records and the current pair was verified manually; deferred to Story 2.8 because 1.9 explicitly excludes the blocking CI tier.
Claude review pass 2026-10-18. Story-only diff: Platform `.config`, `hexalith-platform.json`, `README.md`, Builds gitlink, and Builds `544fd2e`.

- blind: released Builds README:283-285 still says the Platform record has no tool range — `low` — confirmed at `544fd2e`; patch (direct wording correction in Builds).
- blind/edge: Platform README "Publication alone does not grant acceptance" contradicts the 4.x range — `low` — later 4.x releases are accepted by range with no Platform edit; patch (reword).
- blind/edge: `[4.33.0, 5.0.0)` admits `5.0.0-preview.1` and 4.x prereleases — `medium` — NuGet `Satisfies` confirmed by the reviewer; intent_gap, resolved by the human as `[4.33.0, 5.0.0-0)` (frozen decision updated); patch the record and README.
- edge: exact `[4.33.0]` instead of the 4.x range — `false` — contradicts the human range decision.
- blind: gitlink move landed in `3c9fb64 fix: cicd` with Story 5.4 files, before release approval — `low` — process-only traceability; the end state equals the tag commit, and the fix would rewrite pushed history; rejected.
- blind: catalog commits `650de63`/`eeab884` were brought in without a Platform build — `false` — they only add an unused `Hexalith.McpCli.Abstractions` PackageVersion, which cannot change existing Platform restores.
- blind: HXP020 — Platform's EventStore declaration provenance is 3.120.0 vs catalog 3.124.0 — `medium` — pre-existing catalog/declaration drift, not caused by 1.9; defer.
- blind/edge: Platform has no `v<SemVer>` tag, so module-workspace package mode still gets HXW011 — `medium` — `git tag` and `ls-remote --tags` are empty; Platform release tagging is outside this intent; defer.
- blind: README hard-codes the catalog commit with no upgrade rule — `low` — required by the AC; folded into the README patch as a one-sentence pin/gitlink/tag rule.
- blind: README check commands state no expected output or identity check — `low` — docs polish with no defect; rejected.
- blind: stale NuGet HTTP cache restore hint missing — `low` — a transient local cache state, not everyday use; rejected.
- blind: README does not say which tool the range covers — `false` — README names `hexalith-module`/`hexalith.builds.module.cli`, and HXW010 checks only the running module CLI.
- blind: not-accepted list omits 4.27.5/4.28.0/4.29.0 and versions below 4.20.0 — `low` — those tags were never published to NuGet.org, but the wording should say every version below 4.33.0; folded into the README patch.
- blind/edge: the script fix covers only EmbeddedResource, not other external inputs — `false` — no other external item exists today; the remaining copies are explicit `Exec` inputs; speculative.
- blind/edge: resource evaluation context differs from the isolated build — `false` — the csproj declares the resources unconditionally; the real-checkout evaluation can only add extra copies, never drop a required one.
- blind: unclear errors when there are no resources or stdout is not JSON — `low` — loud failure in an unreached state; rejected.
- edge: EmbeddedResource under the tooling `bin/`/`obj/` is never copied — `false` — the only such item, `$(PlatformVersionCatalogPath)`, is generated by the isolated build itself.
- verification-gap: no automated pin/range consistency check — `medium` — pre-verified; defer (Story 2.8, already logged).
- verification-gap other: Platform `hexalith.builds.module.cli` pin is absent from Builds `package-version-exceptions.json` — `maybe-false` — only a workspace-mode scan including Platform would report it, and that scan crashes earlier on Platform today; settled by fixing that crash and rescanning; if real, only `low`; rejected.

## Verification

**Commands:**
- `pwsh -NoProfile -File Tools/test-platform-version-catalog.ps1` (in Builds) -- expected: all cases PASS.
- `gh run list --workflow ci.yml --branch main --limit 1` (Builds) -- expected: success on the release SHA.
- `curl -s https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/index.json` -- expected: lists the accepted version.
- `dotnet tool restore && dotnet tool run hexalith-module --version` (Platform root) -- expected: the accepted version.
- From the Platform root, run `dotnet tool run hexalith-module run --mode package` on a reference declaration, then `down` -- expected: no HXW009/HXW010 identity findings. A later non-identity failure is acceptable and recorded.
- `git -C references/Hexalith.Builds describe --exact-match --tags HEAD` -- expected: `v<accepted>`.

**Observed 2026-10-18:** Catalog test 96/96 pass; push CI and release workflow success; NuGet listing includes 4.33.0; `dotnet tool restore` success; `dotnet tool run hexalith-module --version` reports 4.33.0 with the tag commit; accepted package-mode invocation has no HXW009/HXW010; 4.32.0 package mode returns HXW010 and source mode warns HXW010; `down` succeeds; Builds HEAD has exact tag `v4.33.0` and matches the Platform HEAD gitlink.
