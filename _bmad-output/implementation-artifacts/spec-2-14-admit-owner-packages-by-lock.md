---
title: '2.14 Admit owner packages by lock file'
type: 'refactor'
created: '2026-11-19'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'f3f96b2ed5ee877597de66ce10bf11a4843629b7'
story_key: '2-14-pin-builds-and-owner-modules-by-released-version'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Composition admits owner packages (EventStore, Tenants, Memories, Parties, McpCli) through 18 hand-maintained DLL hash tables. Exact owner-version tuples select those tables, and project files carry exact-version conditions too. A new owner release is refused until C# changes. That blocks retiring the Builds Parties SDK 4.2 view, the HXR021 cause, by a version bump.

**Approach:** A generated, CI-enforced owner package lock supplies the expected package content. Composition checks each owner package it loads against the lock, and derives the expected DLL identities from the lock-verified archive. It records expected against loaded identities (AR-12). Version admission becomes a rule.

**Decisions (owner, 2026-11-19):**
- **Lock form:** a Platform-generated `eng/owner-packages.lock.json` holding `{id, version, contentHash}`, checked before any run effect. This follows the `builds-tool.lock.json` precedent, not native NuGet `packages.lock.json`. The story's "fails restore" is met as "fails before any run effect".
- **Repository scope:** Platform only. No Parties lock files and no Parties push in this slice.
- **Size:** keep one spec (about 2,900 tokens). The length is the Code Map's file inventory.

## Boundaries & Constraints

**Always:**
- Fail closed before any run effect, naming the package or assembly, when:
  - no lock entry exists;
  - the lock entry names another version;
  - the archive bytes differ from `contentHash`;
  - a loaded DLL differs from its archive member.
- Generate the lock the way the Builds tool lock is generated: `dotnet nuget verify --all` must pass and the hash must equal the nuget.org `packageHash`. Adopting a release means a version bump plus a regenerated lock.
- Run-built Parties bodies (the sealed Release owner, `currentOwnerArtifacts`) keep their run-bound authority; they are not locked.
- Keep candidate binding, declaration and topology checks, image digest binding and receipts.
- Turn the exact-version compile conditions in project files into version-range rules.
- Retire the cohort-keyed .NET 10 seed reader with the cohorts. No current run benefits from it: the 2.14 lifecycle run refused all four net10 images. Retiring the SDK 4.2 view removes its purpose.

**Never:**
- Hash, revision or owner release-version literals in `src/` for package admission.
- Retiring the SDK 4.2 view, changing pins or gitlinks, cutting a release, or pushing.
- `PlatformHostedReferenceMapping` (the Story 1.17 owner-approved hosted source tuple), and the redis/falkordb/postgres/daprd image digests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Current pins | Catalog versions with a regenerated lock | Admitted; evidence lists lock id, version, `contentHash`, member sha256 and loaded sha256 | N/A |
| New release | Real Tenants 8.0.1 / EventStore 5.2.0 packages with lock entries | Admitted with no C# edit | N/A |
| Unknown version | Catalog version without a lock entry | Refused, naming the package and version | Fail closed |
| Stale lock | Lock entry names another version | Refused | Fail closed |
| Tampered archive | `.nupkg` bytes differ from `contentHash` | Refused, naming the package | Fail closed |
| Substituted DLL | Loaded DLL differs from its archive member | Refused, naming the assembly | Fail closed |

</frozen-after-approval>

## Code Map

Aliases: C = `src/Hexalith.Platform.Composition`, B = `src/Hexalith.Platform.CatalogBuilder`, T = `tests/Hexalith.Platform.Composition.Tests`.

- **`B/PublishedCatalogArtifactCohort.cs`:**
  - 18 tables (:340-361), version selectors `For*Versions` (:194-259) and verifiers (:269-330).
  - It is linked into Composition (`Hexalith.Platform.Composition.csproj:68`) and copied into the generated reader (`C/PlatformRoutingCatalog.cs:69`). Its replacement keeps the link and copy pattern.
- **`C/PlatformPackageCatalogQualification.cs`:**
  - `RequirePackageCohort`/`Select*Cohort` (:86-140) refuse unknown tuples, and `VerifyPackageBytes` is at :94.
  - The McpCli `"2.0.0"` literal is at :123. `HasQualifiedSdkBytes` is used only by tests.
- **`C/PlatformRoutingCatalog.cs`:** :30-37 selects the cohort; :60-66 and :88 hold the net10 seed switch and the extraction; :94 has the reader request (`qualificationCohort`, `currentOwnerArtifacts`).
- **`B/Program.cs`:**
  - The cohort owner sets and verifiers are at :25-65, and the receipt's `loadedArtifacts` at :152-160.
  - Derive the owner set from the request's owners. `C/PlatformCatalogContractBinding.Resolve` already builds that set from the declared graph.
- **`C/PlatformCurrentOwnerProof.cs`:**
  - The cohort is at :32/:92, the SDK DLL checks at :143-147, the `project.assets.json` version scan at :178-200, and the receipt check at :260.
  - Isolated package folders: `build-packages` and `reader-packages` (:78-79, :107-128).
- **`C/PlatformCurrentOwnerObservation.cs`:** :33 is the mapped SDK DLL check; :61-62 are `sdkBodies`/`runtimeBodies`.
- **`C/PlatformPublishedEventStoreSeed.cs`:** retire it, including its literals.
- **Exact-version conditions:** `Hexalith.Platform.Composition.csproj:19-24,46,70`, `B/Hexalith.Platform.CatalogBuilder.csproj:3` and `src/Hexalith.Platform.EventStoreHost/Hexalith.Platform.EventStoreHost.csproj:19-25`. These are compile-time API constants, so the rules must keep today's results for current pins.
- **Precedent to reuse:**
  - `eng/builds_tool_lock.py` and `eng/builds-tool.lock.json`
  - `eng/test_builds_tool_lock.py`, run by `.github/workflows/tuple-ci.yml:96`
  - The tool's AR-12 expected/loaded handling in `eng/run_tuple_ci.py:122-141` and `eng/prepare_builds_artifacts.py:82-125`
- **Tests:**
  - 24 T files use cohort members. The heaviest are `PlatformCurrentPackageArtifactCohortTests`, `PlatformSdk420ArtifactCohortTests`, `PlatformCurrentReferenceCohortTests`, `PlatformSdk42PublishedPartiesCohortTests`, `PlatformPublishedPartiesCohortTests`, `PlatformEventStorePackageTestCohortTests`, `PlatformSdk42EventStorePackageTestCohortTests`, `PlatformPartiesPackageFixture` and `PlatformMcpCliCatalogSelectionTests`.
  - Five `tests/catalog-builder/verify_*.py` scripts send `qualificationCohort`.
  - `docs/implementation/current-package-cohort-20261112{,-memories27}.json` restates package hashes.
  - Keep the `eng/tuple-ci-*-suites.json` inventories accurate.

## Tasks & Acceptance

**Execution:**
- [x] `eng/owner_packages_lock.py`, `eng/owner-packages.lock.json`, `eng/test_owner_packages_lock.py`, `.github/workflows/tuple-ci.yml` -- generate `{id, version, contentHash}` for every owner package that the pinned catalog selects for Platform's package-mode surfaces, and check it in CI -- this is the single integrity source.
- [x] `B/PublishedCatalogArtifactCohort.cs` → a lock verifier (same link and copy) -- check the archive against `contentHash`, derive member sha256 and compare it with the loaded DLL -- this replaces the tables.
- [x] `C/PlatformPackageCatalogQualification.cs`, `C/PlatformRoutingCatalog.cs`, `B/Program.cs`, `C/PlatformCurrentOwnerProof.cs`, `C/PlatformCurrentOwnerObservation.cs`, `C/PlatformPublishedEventStoreSeed.cs` -- verify through the lock and take owner sets from the declared graph. Send the lock path instead of a cohort, record the AR-12 expected/loaded pairs, and retire the net10 seed path -- this removes the version tuples.
- [x] The three project files -- replace exact-version lists with range rules -- adopting a new release then needs no project edit.
- [x] T, `tests/catalog-builder/`, `docs/implementation/current-package-cohort-*.json` -- cover the matrix, including the real Tenants 8.0.1 / EventStore 5.2.0 row. Keep the substitution cases and delete the cohort-only tests and docs.
- [x] `eng/tuple-source-preparation.md`, `README.md`, `docs/package-catalog-qualification.md` -- add lock regeneration to the adoption procedure.

**Acceptance Criteria:**
- Given `src/` and the project files, when scanned for owner hash, revision and version literals used in package admission, then none remain (the Never exclusions apply).
- Given the current pins, when the three `eng/run_tuple_ci.py` suites run, then each passes with a positive count and zero skips.

## Design Notes

Work only in a new worktree, `/home/administrator/projects/hexalith/platform-2-14-locks`, on branch `feat/2-14-owner-package-lock` from local `main`. Other sessions' `/pushall` commits the main checkout's working tree. Do not commit, push, or touch module checkouts or remotes.

To find the expected identity of an assembly:
1. Map the assembly name to its package id and version (deps.json or `project.assets.json`).
2. Look up the lock entry.
3. Check the SHA-512 of the `.nupkg` in the package folder actually used.
4. Hash its `lib/<tfm>/<name>.dll` member.

## Verification

**Commands** (`export PATH="$HOME/.dotnet/tools:$PATH"`; build with `-nodeReuse:false`):
- `python3 eng/owner_packages_lock.py --check` -- expected: the lock matches the pinned catalog and nuget.org.
- `python3 -m unittest eng/test_owner_packages_lock.py eng/test_builds_tool_lock.py` -- expected: pass.
- `dotnet exec tests/Hexalith.Platform.Composition.Tests/bin/Release/net11.0/Hexalith.Platform.Composition.Tests.dll -class '*Lock*'` -- expected: the matrix rows pass with zero skips.
- `python3 eng/run_tuple_ci.py` for the base42, current42 and extensions suites, with `NO_COLOR=1 TERM=dumb` -- expected: positive counts and zero skips.

## Review Triage Log

Iteration 1 (2026-11-19). Layers: blind (B), edge-case (E), verification-gap (V; gaps pre-verified).

| Entry | Findings | Verdict | Route | Evidence |
|---|---|---|---|---|
| P1 | B1, E13 | high | patch | `tests/source-release/source-release.test.mjs:60-63` still finds the deleted `verify_public_owner_cohorts.py`/`verify_sdk420_public_parties.py` steps, so `historical.if` throws and `npm run test:source-release` fails in tuple CI. |
| P2 | B9, E6 | high | patch | `sdkBodies` became name→object over every mapped owner body; Parties `scripts/runtime_observation_contract.py:323` requires `sdkBodies == SDK` (the four name→hex bodies). |
| P3 | B12, E2, E16, V-other-1 | medium | patch | `OwnerPackageLock.VerifyReceipt` never reopens `archive`/`archiveMember` or checks the loaded file's assembly name, and `VerifyReader` no longer requires every `ObservedSdk` record, so a self-consistent forged or partial receipt passes. |
| P4 | B3, E17, V-other-2 | medium | patch | `VerifyPackageBytes` checks a fixed name list; `Hexalith.EventStore.Aspire` and `Admin.Abstractions`, loaded by Composition, are never checked, contrary to the intent and the three docs. All owner packages in the test host graph are locked. |
| P5 | B2 | medium | patch | `owner_packages_lock.py` reads both gitlinks from `HEAD` (`recorded_gitlink`, `git ls-tree HEAD`), so the documented "regenerate, then commit" order refuses or locks the old catalog. |
| P6 | V1, B8, E19 | medium | patch | No test admits a 5.2.0/8.0.1 catalog through `RequireLockedVersions`; a reintroduced version tuple in `RequireLockedPackages` would pass every test. |
| P7 | V2 | medium | patch | No test reaches `Bind`'s `VerifyPackageBytes` refusal; deleting that call passes every test. |
| P8 | V3 | medium | patch | `ReadMappedOwnerPackages` refusals (digest mismatch, missing `ObservedSdk` mapping) are untested. |
| P9 | V4 | medium | patch | The non-integration Parties branch of `RequireLockedPackages`/`VerifyPackageBytes` is never exercised; inverting its predicate passes every test. |
| P10 | B5 | medium | patch | `SURFACES` restates the project files' owner references with no check; a new reference leaves the lock incomplete and "regenerate the lock" cannot fix it. |
| P11 | B11 | medium | patch | `ReleasedDeclarationPreservesTheOrdinarySourceAndPurposeBoundary` (attempt replay, appended declaration) was not cohort-only but was deleted with both cohort files; `publishedDeclaration: true` is now dead. |
| P12 | E5, B15 | low | patch | `PlatformCurrentOwnerProof` seals `Hash(owners.Path)` instead of the admitted `owners.Sha256`. |
| P13 | E1 | low | patch | `ReaderPackageFolders(build)` runs after `catalog-pending` is created and outside the `try`, so a failure leaks the directory. |
| P14 | E10 | low | patch | `owner_packages_lock.py` main catches only `ValueError`; OSError/subprocess failures print raw tracebacks. |
| P15 | E11 | low | patch | `require_current` names no package when only the entry order differs. |
| R1 | B4 | low | reject | The EventStore host was never runtime-checked (pre-existing); the docs claim only that its packages are locked; a host check is a new path. |
| R2 | B5 (net11 restore) | low | reject | `--check` reproduces the lock for the current Parties Release closure. |
| R3 | B6, E8 | low | reject | Every catalog-builder matrix SDK (3.130.0-2, 3.132.1, 4.1.0, 4.2.0) keeps its constants; the spec licenses range rules; prerelease selection is not a catalog input. |
| R4 | B7 | false | reject | The spec derives the reader owner set from the request, which Composition writes from `PlatformCatalogContractBinding.Resolve`. |
| R5 | E15 | low | reject | Composition writes exactly the two run-bound keys; an absent key makes that body lock-verified and fail closed. |
| R6 | B10, E12 | low | reject | The weak refusal asserts sit on branches that Linux x64 CI does not take; the refusal message varies with the platform guard. The duplicated helper is cosmetic. |
| R7 | B11 (older genuine body) | low | reject | `verify_owner_package_lock_reader.py` `older-genuine-tenants-body` covers it in catalog-builder CI. |
| R8 | B13 | false | reject | The embedded `$(NuGetPackageRoot)` is NuGet's resolved global packages folder (it honours nuget.config and `RestorePackagesPath`). |
| R9 | B14 | low | reject | Every caller passes a single-target deps.json. |
| R10 | B15 (copies, prefixes) | low | reject | No divergence today; no named caller breaks. |
| R11 | E3, E4, E7, E9, E14 | low | reject | Generated deps.json files, ids from the same restore graph, five owner families only, clean CI checkouts with fail-closed refusals, and `VerifyDirectory` checking every reader owner DLL before load. |
| R12 | E18 | low | reject | The reader receipt and the current-owner proof carry the AR-12 records; a Composition record sink would be new surface. |
