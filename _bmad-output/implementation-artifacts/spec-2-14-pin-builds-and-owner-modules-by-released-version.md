---
title: '2.14 Pin Builds by released version (Builds slice)'
type: 'refactor'
created: '2026-11-16'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
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
  - C `PlatformPartiesValidationContext.cs:13-36`: the hash pairs. Its sole caller is `PlatformPartiesRunProof.Validate:90`; pass it `options.Mapping.PlatformIdentity.ToolVersionRange`. The DLLs embed `6.0.5+<sha>`.
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
- [ ] `E/builds-tuple.json`, `E/builds_tuple.py`, `E/builds_publication.py` and their tests -- move to schema 2 `{schemaVersion, version, releaseTag}`.
  - The tag commit must equal the gitlink, and the catalog comes from the gitlink blob.
  - The release is checked by tag: published, with its `hexalith-release:` marker equal to the gitlink.
  - Observed IDs are evidence only.
- [ ] `E/builds-tool.lock.json`, a `lock` command, `E/prepare_builds_artifacts.py` and tests -- generate and enforce the lock as decided.
- [ ] `E/run_tuple_ci.py`, `E/verify-platform-pins.py`, `tuple-ci.yml`, and `tests/source-release/*.mjs` if steps change -- consume the new binding and lock, and drop the hash re-checks.
- [ ] `E/current_catalog_view.py` and the fixture-using tests -- drop the generation tables and version branches, read the catalog from the gitlink, and delete obsolete binding fixtures.
- [ ] C `PlatformPartiesValidationContext.cs`, `PlatformDeclarationSet.cs` -- apply the version rule and remove the pairs and the allow-list.
- [ ] The T files listed -- add the version-rule matrix, including missing or unparseable versions, and read pins from files.
- [ ] The docs listed -- point to the pin files, and label historical sections as historical.
- [ ] `_bmad-output/implementation-artifacts/evidence/2-14/` -- record a real Builds 6.0.6 adoption rehearsal in a scratch worktree: its diff and the passing Builds checks.

**Acceptance Criteria:**
- Given `src/`, `E/*.py` and the docs, when scanned for Builds 40/64-hex and release-version literals, then no current-pin identity remains.
- Given landing on main, when hosted tuple CI completes, then all three jobs are green with zero skips.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `python3 -B -m unittest discover -s eng -p 'test_*.py'` and `npm run test:source-release` -- expected: pass, zero skips.
- `python3 eng/run_tuple_ci.py --audit-only`, then the three profiles, running the Composition DLLs from `bin/Release/net11.0/` -- expected: positive counts, zero skips.
