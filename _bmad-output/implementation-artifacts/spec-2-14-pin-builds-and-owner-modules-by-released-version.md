---
title: '2.14 Pin Builds by released version (Builds slice)'
type: 'refactor'
created: '2026-11-16'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '7a78a6c44141d9be52b92fa687031f21c146e483'
story_key: '2-14-pin-builds-and-owner-modules-by-released-version'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `eng/builds-tuple.json` pins one Builds release with about 12 hand-maintained identities. `src/` compiles in Builds release identities: 22 AppHost/Tooling hash pairs and a version/commit allow-list. As a result, 26 of 35 Builds adoptions since 2026-11-01 also edited C# or docs.

**Approach:** Pin Builds by version and release tag only. A generated, CI-enforced lock supplies tool-package integrity. Admit the tool by the `hexalith-platform.json` range.

**Decisions (2026-11-16):**
- **Scope:** Builds slice only. Owner lock files, owner literals and AR-12 owner expectations are deferred (`deferred-work.md`).
- **Integrity:** a generated `eng/builds-tool.lock.json` holds `{id, version, contentHash}` (the NuGet SHA-512 base64 of the tool `.nupkg`). It is written only after `dotnet nuget verify --all` passes and the hash matches nuget.org `packageHash`. CI recomputes the hash.
- **Version rule:** the AppHost and Tooling informational versions must be equal and inside `toolVersionRange` `[pin, next major)`. Newer 6.x is admitted; older versions and 7.x are refused.

## Boundaries & Constraints

**Always:**
- Fail closed. Refuse when any of these hold:
  - the tool bytes differ from the lock;
  - the lock entry is missing or names another version;
  - the tag commit differs from the Builds gitlink;
  - the tools pin, the binding version and the range floor differ;
  - the AppHost/Tooling pair is split, out of range or unparseable.
- Keep `dotnet nuget verify --all`, the `--version` = `<version>+<gitlink>` check, the build-output byte comparison, the catalog read from the gitlink blob, candidate binding and zero-skip enforcement.
- Builds AR-12: expected = per-file sha256 of the lock-verified archive; loaded = built outputs; record both.
- Tests read pins from the pin files.
- Historical evidence keeps its original subjects.

**Never:**
- Hashes, revisions or release/run/artifact IDs in the binding.
- Builds release-identity literals in `src/`, in Python verifiers, or as current facts in docs.
- Owner-slice work.
- Landing 6.0.6, cutting a Builds release, or editing archived tags or `/pushall`.

A major generation or a behavior-changing release may still need code.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Adopt release | Version bump, gitlink move, lock command | Builds checks pass; diff is pins, gitlink and lock only | N/A |
| Tampered package | Tool bytes differ from contentHash | Preparation fails, naming the package | Fail closed |
| Stale lock | Version bumped, lock not rerun | Preparation fails on version mismatch | Fail closed |
| Tag drift | Tag commit differs from gitlink | `resolve-sources` fails | Fail closed |
| Versions | 6.0.5 or 6.1.0 / 6.0.3, 5.2.2 or 7.0.0 | Admitted / refused, naming the version | Fail closed |
| Split pair | AppHost differs from Tooling | Refused | Fail closed |

</frozen-after-approval>

## Code Map

Aliases: E = `eng`, C = `src/Hexalith.Platform.Composition`, T = `tests/Hexalith.Platform.Composition.Tests`.

- **Binding and tool checks:**
  - `E/builds_tuple.py:24-128`: binding fields, the tools/range floor and the gitlink/catalog check.
  - `E/builds_publication.py`: reuse its `git/ref/tags` lookup.
  - `E/prepare_builds_artifacts.py:43-91`: archive and Tooling sha256, signature, `--version`, per-file receipt.
- **CI:** `E/run_tuple_ci.py:51-59,270-365,403` re-checks the binding hashes. Keep its `:459-463` comparison. Also `E/verify-platform-pins.py:24-29` and `tuple-ci.yml:41-48,99-112`.
- **Catalog view:** `E/current_catalog_view.py:40-100,279-300` holds the `GENERATION60x_CATALOG` tables and the `"6.0.5"` branches. Owner-version literals stay, but stop keying them on the Builds version. `E/fixtures/builds-6.0.*` feed four `E/test_*` modules.
- **Admission (C#):**
  - C `PlatformPartiesValidationContext.cs:13-36`: the hash pairs. Its sole caller is `PlatformPartiesRunProof.Validate` (about line 91); pass it `options.Mapping.PlatformIdentity.ToolVersionRange`. The DLLs embed `<version>+<sha>` (now `6.3.0+7afc4981…`).
  - C `PlatformDeclarationSet.cs:36-53`: the allow-list. Keep its structural range checks.
- **T literals:**
  - `PlatformOrdinaryDeclarationCompatibilityTests.cs:43-121`
  - `PlatformCurrentOwnerHandoffTests.cs:22-27`
  - `PlatformPartiesValidationHandoffTests.cs:20`
  - `PlatformPublishedValidationRuntime.cs:37-136`
  - `PlatformMcpCliCatalogSelectionTests.cs:133`
  - `PlatformCurrentPackageArtifactCohortTests.cs:246-340`
- **T binding readers:** `PlatformProductionModelClosureTests.cs:22-33`, `PlatformPartiesPackageFixture.cs:109`, `PlatformPackageCatalogReaderFixture.cs:28`.
- **Docs:** `README.md:3-14,328`, `docs/current-tuple-preparation.md:3-14`, `docs/domain-module-enrollment.md:3-14`, `E/tuple-source-preparation.md:184-240`.

## Tasks & Acceptance

**Execution:**
- [x] `E/builds-tuple.json`, `E/builds_tuple.py`, `E/builds_publication.py` and their tests -- move to schema 2 `{schemaVersion, version, releaseTag}`.
  - The tag commit must equal the gitlink, and the catalog comes from the gitlink blob.
  - The release is checked by tag: published, with its `hexalith-release:` marker equal to the gitlink.
  - Observed IDs are evidence only.
- [x] `E/builds-tool.lock.json`, a `lock` command, `E/prepare_builds_artifacts.py` and tests -- generate and enforce the lock as decided.
- [x] `E/run_tuple_ci.py`, `E/verify-platform-pins.py`, `tuple-ci.yml`, and `tests/source-release/*.mjs` if steps change -- consume the new binding and lock, and drop the hash re-checks.
- [x] `E/current_catalog_view.py` and the fixture-using tests -- drop the generation tables and version branches, read the catalog from the gitlink, and delete obsolete binding fixtures.
- [x] C `PlatformPartiesValidationContext.cs`, `PlatformDeclarationSet.cs` -- apply the version rule and remove the pairs and the allow-list.
- [x] The T files listed -- add the version-rule matrix, including missing or unparseable versions, and read pins from files.
- [x] The docs listed -- point to the pin files, and label historical sections as historical.
- [x] `_bmad-output/implementation-artifacts/evidence/2-14/` -- record a real Builds 6.3.0 → 6.3.1 adoption rehearsal (v6.3.1 = `db7bb7179244ce33483231687486381305c7c3db`) in a separate scratch worktree: its diff and the passing Builds checks. Do not land it.

**Acceptance Criteria:**
- Given `src/`, `E/*.py` and the docs, when scanned for Builds 40/64-hex and release-version literals, then no current-pin identity remains.
- Given landing on main, when hosted tuple CI completes, then all three jobs are green with zero skips.

## Implementation Notes

**Where to work (2026-11-18):**
- Work only in the worktree `/home/administrator/projects/hexalith/platform-2-14`, on local branch `feat/2-14-builds-version-pins`. Never edit `/home/administrator/projects/hexalith/platform`: other sessions' `/pushall` commits that checkout's working tree.
- Its submodules are initialized at their gitlinks. Commit `7a78a6c4` restored the five pinned gitlinks (Builds `7afc4981` = v6.3.0; the EventStore, Parties, Tenants and Memories bindings), so `current_tuple_source.verify_checkout` and the Builds gitlink check pass here. Keep those five gitlinks unchanged. Do not commit, push, or touch module checkouts or remotes.
- For the 6.3.1 rehearsal, create a second throwaway worktree from this branch, and remove it afterwards.

**Code drift since planning:** the pin moved from 6.0.5 to 6.3.0, so Code Map line numbers are approximate. The same patterns now also carry 6.3.0 entries, for example `GENERATION630_CATALOG` and the `("6.3.0", "7afc4981…")` allow-list row. The matrix versions (6.0.5, 6.1.0 / 6.0.3, 5.2.2, 7.0.0) are rule-relative examples. Test them against the explicit range `[6.0.5, 7.0.0-0)`. Separately, test the current pins read from the pin files.

**Environment facts:**
- `export PATH="$HOME/.dotnet/tools:$PATH"` first. `pwsh` lives there, and Builds-referencing builds fail with MSB3073 exit 127 without it.
- Build with `-nodeReuse:false`. Shared MSBuild nodes hang under concurrent sessions. After a build, check the DLL timestamp.
- `dotnet test` on the slnx runs zero tests. Run each test DLL with `dotnet exec <explicit bin/<Config>/<tfm>/X.Tests.dll>`, using xUnit v3 `-class`/`-method` filters. Do not take the first DLL `find` returns: stale `spec14-*` copies exist.
- `verify_component.py --platform-assembly` and similar summary parsers need `NO_COLOR=1 TERM=dumb`.
- Preserve each file's line endings. Several `.cs` files use CRLF.

## Spec Change Log

- **2026-11-18 (owner decisions at resume):**
  - The pin is now Builds 6.3.0, and 6.3.1 was published 2026-11-18. The rehearsal therefore targets 6.3.0 → 6.3.1, and the frozen "Never: Landing 6.0.6" reads as "never land the rehearsal release".
  - Landing restores the five pinned gitlinks that `/pushall` moved (`7a78a6c4`), so hosted tuple CI can go green.
  - The 2026-11-18 extension of the epic story (HXP020 declaration and image allowlists, retiring `platform-current-package`) stays out of this Builds slice. It is planned next as a second 2.14 spec (`deferred-work.md`).

## Review Triage Log

Review pass 1 (2026-11-18). Layers: Blind Hunter (B), Edge Case Hunter (E), Verification Gap (V). Groups share one root cause.

| ID | Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| V1 | The AR-12 expected/loaded block in `run_tuple_ci.py` (receipt recheck, `buildsArtifactIdentities`, member refusal) is never run by a test | medium | patch P1 | Every `run_tuple_ci.main()` test uses `--audit-only`, which returns before the block; deleting the record or the refusal fails nothing. |
| B3 | The version-rule matrix has no prerelease cases; the README lost the "prereleases inside the range" sentence | low | patch P2 | `PairRefusal` admits in-range prereleases (NuGet `Satisfies`), but `7.0.0-x` and `<floor>-x` are untested, and the sentence went with the 6.3.0 literal. |
| E13 | The `dotnet nuget verify … --all` argv assertion was deleted | low | patch P3 | No remaining test fails if `verify_signature` drops `--all`. |
| E11 | The ambient `HexalithBuildsRoot=unverified-root` override was deleted from the preflight test | low | patch P4 | The base test patched it before asserting `-p:HexalithBuildsRoot=<sibling>`; the rewrite asserts the property without the ambient override. |
| E9 | The `verify-agents-host.sh` catalog-digest heredoc reads git without `inspection_environment()` | low | patch P5 | The old code read a static digest; the new git reads inherit an ambient `GIT_DIR`/`GIT_WORK_TREE`. |
| B9, B14 | The evidence README says "every Builds check passes at 6.3.1" and does not explain the 376 → 343 unit-test drop | low | patch P6 | At 6.3.1 only steps 03–10 ran, not the three profiles. The drop is the deleted generation-fixture controls, but nothing says so. (The `load_tests` opt-out is harmless: the script entry uses `loadTestsFromTestCase` and requires `testsRun == 1`.) |
| B10 | The adoption procedure never says to commit before the checks | low | patch P7 | `recorded_gitlink`, `PlatformTestPins.BuildsGitlink` and `--require-recorded` all read `HEAD`, so an uncommitted bump is checked against the old gitlink and refuses as tag drift. |
| B12 | Sections under "Historical preparation records" still say "The current producer is…" | low | patch P8 | `README.md:62,86,113,266` and `docs/current-tuple-preparation.md:62,86,113,448`. |
| B19 | The same ~30-line preparation block is copied into three documents | low | patch P9 | README, `docs/current-tuple-preparation.md` and `docs/domain-module-enrollment.md`; `eng/tuple-source-preparation.md#adopting-a-builds-release` is already canonical. |
| B11 | Works' `scripts/tests/test_platform_builds_preparation.py` copies Platform's `builds_tuple.py` and uses schema 1 and `validate_recorded_source` | medium | defer D1 | Confirmed at lines 35, 50, 53 and 59. It fails at Works' next Platform reference bump, and the fix lives in Works. |
| B18 | The shared NuGet cache holds a non-genuine `Hexalith.McpCli.Abstractions 2.0.0` | medium | defer D2 | Pre-existing local machine state, found by the profile runs; local package-mode evidence that uses the shared cache may be affected. |
| B1 | The pair rule ignores the `+commit` suffix, so a matched foreign-commit pair is admitted | false | reject | The frozen decision admits AppHost/Tooling by equal version inside the range. Integrity comes from the lock, the build-output comparison and `--version = <version>+<gitlink>`. |
| B2, V2 | `IsCurrentPublishedValidation` now throws, and its caller path is untested for refusals | low | reject | Throwing refuses, which is the "naming the version" matrix outcome. The rule is fully covered through `PairRefusal`; a caller test needs a forged published AppHost, which the harness refuses to launch. |
| B4 | `PlatformDeclarationSet` no longer validates `CatalogCommit` | false | reject | `PlatformPartiesRunProof.cs:107-108` refuses a `CatalogCommit` that differs from the Builds checkout and the gitlink. |
| B5, E7 | `generic_defaults` refuses only a generic SDK equal to the named view | false | reject | When a generic default equals the named value, every consumer resolves the same version, so no wrong package can pass. |
| B6 | The lock command does not tie the package to the tag or the gitlink | false | reject | That is the frozen lock decision. `resolve-sources` checks tag == gitlink, and preparation checks the marker and `--version`. |
| B7, E1 | `_get` decompresses gzip before applying its bound | low | reject | A maintainer runs it against nuget.org over TLS; a truncated body still fails closed (EOFError), and the fix adds code. |
| B8, E15 | `eng/lifecycle-qualification` still names Builds 6.3.0, and `pin-scan.py` does not recurse | false | reject | The lines state Builds 6.3.0 behavior (its Dapr/Aspire versions, XDG scrubbing), which stays true after adoption. They are outside the AC scan scope. |
| B13 | `local-profiles-summary.json` is flattened, with no generator | low | reject | Cosmetic hand summary of the run outputs. |
| B15 | Bookkeeping: Verification and Triage sections empty, status mismatch | false | reject | Verification lists its commands, this log is now filled, and sprint status syncs at completion. |
| B16 | `run_tuple_ci.py` reads the gitlink with `rev-parse`, not `recorded_gitlink` | false | reject | `verify_catalog` refuses unless the checkout HEAD equals that value, and `resolve-sources` validates it through `recorded_gitlink`. |
| B17 | `verify-platform-pins.py --producer-binding` only validates the schema | low | reject | It still refuses a malformed binding; removing it touches callers for no named harm. |
| E2 | `schemaVersion` `true`/`2.0` is accepted | false | reject | No downstream effect; every other field is validated exactly. |
| E3 | `rollForward: true` would let the tool roll forward to an unlocked version | false | reject | `rollForward` selects the .NET runtime, not the tool version, and the file pins `false`. |
| E4 | `builds_publication.py --revision` mixes another revision's gitlink with the working-tree pins | low | reject | CI checks out exactly the candidate; this is local misuse only, and the fix adds code. |
| E5 | An audit run that reuses a caller receipt has no tag, marker or `--version` check | false | reject | That reuse path is audit-only diagnostics, refuses outside audit, and never had a tag check. |
| E6 | Archive bytes may change between the lock check, the signature check and extraction | false | reject | The output is a new run-owned evidence directory (pre-existing pattern); no actor was shown. |
| E8 | A prerelease or commit-less pair is admitted | false | reject | The frozen rule admits any equal pair inside the range; P2 adds the prerelease matrix. |
| E10 | `test_tuple_collection_graph --catalog` can credit a catalog to the wrong gitlink | low | reject | CI passes the gitlink catalog it just verified; this is a local diagnostic. |
| E12 | The producer-project and explicit SDK5.1 observations were removed | low | reject | The old test was keyed on deleted fixture generics (EventStore 5.1.0); a version-agnostic rewrite would be new test design. |
| E14 | The CRLF-presence assertion was removed | low | reject | Checkout EOL depends on the environment; line-ending tamper cases remain in `test_current_tuple_source.py`. |
| V3 | The `verify-agents-host.sh` heredoc is stubbed in routine tests | low | reject | Local-only script that fails safe at use; `recorded_gitlink` and `verify_catalog` are tested directly. |

## Verification

**Commands:**
- `python3 -B -m unittest discover -s eng -p 'test_*.py'` and `npm run test:source-release` -- expected: pass, zero skips.
- `python3 eng/run_tuple_ci.py --audit-only`, then the three profiles, running the Composition DLLs from `bin/Release/net11.0/` -- expected: positive counts, zero skips.
