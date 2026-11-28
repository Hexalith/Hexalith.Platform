---
title: 'Story 2.14 view slice'
type: 'bugfix'
created: '2026-11-20'
status: 'superseded'
baseline_commit: '87c857dfc8567ebf9176cab7ba41d591ec299289'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Retire the SDK 4.2 views so Platform resolves the latest stable owner packages. Prove Debug/source Composition, package-mode operation digest readiness, and the Parties integration, lifecycle and attachment paths on the new tuple.

## Boundaries & Constraints

Use detached scratch worktrees for module changes and `run-with-temp` for disposable jobs. Approve each push and each dispatch separately. Scan the public Platform diff and use the maintainer's default GitHub login for Platform pushes. Preserve existing admission, release and CI checks. Keep one compact summary per slice. Limit Platform edits to adoption and the code, controls and documentation directly tied to retiring the views. If an owner changes behavior, stop and identify the test, owner and exact commit.

Retire the Timesheets selector with the remaining shared selectors. The current net11 Timesheets release overrides it and is outside the Platform package tuple. Its standalone version bump is independent.

</frozen-after-approval>

## Code Map

- Builds `Props/Directory.Packages.props` contains all four selectors. Corresponding checks are in `Tools/test-authoritative-package-catalog.ps1`, `Tools/test_evaluated_catalog_modes.py`, and Module.Tests `Workspace/CompositionCatalogPackageViewTests.cs` and `PackagedHostCatalogTests.cs`. `Tools/G4PackageQualification.functions.ps1` ties packaged-host qualification to the old selector. Keep frozen v1 schema evidence.
- Parties `Directory.Packages.props` applies EventStore 5.4.0 explicitly to two runtime projects. Contracts 4.0.0 still publishes minimum dependencies on EventStore 4.2.0 and McpCli 2.0.1. `.github/workflows/ci.yml` requires live `platform-package` on Platform v2.2.2. Builds `Workspace/PlatformIdentityResolver.cs` requires its paired Builds gitlink for Parties. The old Platform reader admits only the SDK 4.2 cohort.
- Builds `Runtime/CompositionDeclaration{Closure,Options}.cs` checks a release table currently ending at the old cohort. The view retirement must carry the new identities through authenticated declaration inputs without adding an allowlist.
- Platform `eng/current_catalog_view.py`, `eng/run_tuple_ci.py`, suite inventories, corresponding tests and the TestExtension fixture depend on SDK 4.2 selection. The current EventStore 5.4.0, Tenants 8.0.2 and Memories 4.1.0 release tags already carry declaration bytes identical to Platform's snapshots.

## Tasks & Acceptance

**Execution**

- [ ] In Builds, remove the Parties view and conditional use of it. Update the affected catalog tests and generated catalog metadata. Run existing Builds controls and Parties package-mode compile probes. Review, push after approval, and verify a release from the exact green source, with separate approval for a dispatch.
- [ ] In Parties, adopt the first Builds release in the tool/catalog pins and compile, test and inspect pack output on the current SDK. Keep historical compatibility jobs explicit. Prove the existing release gate on a matching released Platform/Builds pair before publishing.
- [ ] If that gate makes Parties-first circular, test a transition using published Parties 4.0.0 against current central pins. Require a green Platform tuple and the unchanged live Parties gate before relying on the transition. Report the exact failing test, owner and commit if behavior changes.
- [ ] In Builds, retire the Platform, packaged-host and Timesheets selectors and update their existing controls. Select the published Parties release in both family defaults and verify the real AppHost, EventStoreHost and UI host package graph. If a transition was needed, follow it with a final Parties pin. Verify tags, NuGet package dependencies and release identities before each adoption.
- [ ] In Platform, bump the Builds gitlink, tool manifest, version range and binding. Update release provenance and owner gitlinks as required, regenerate tool/owner locks and the current tuple binding. Move `tuple-ci.yml` and existing suite verification to the authenticated current SDK and adapt only controls tied to SDK 4.2. Preserve test coverage and verify Debug/source, locked Release, AC1 and matrix row 5. Scan the diff before seeking approval to land it.

**Acceptance criteria**

- Given the released catalog, when Platform and Parties resolve and build their active graphs, then the owner lock and tuple suites resolve the latest cohort and contain no SDK 4.2 selection.
- Given the mapped Parties declaration, when integration and lifecycle run live, then readiness passes, the result identifies the Release owner and immutable dependency images, and cleanup accounts for the owned resources. The attached suite compiles and runs under its declared profile.
- Given the landed Platform SHA, when hosted `tuple-ci.yml` completes, then source resolution, all three current suites and the verdict are green with zero skips.

## Implementation Notes

The owner approved the plan on 2026-11-20. The first implementation handoff covers the first Builds change through local verification. Use the detached worktree at `/home/administrator/projects/hexalith/builds-2-14-view`, based on `aaf27a2c9e1a91a8ea215bafaf6db3891a464fd8`. The Platform checkout, module gitlinks and publication steps follow only after the local first change and separate owner authorization. Leave commit preparation and publication to the parent. The next links depend on the published first release. For owning-repo guidance, the umbrella declares the AI.Tools root submodule, and its ordinary Hexalith instructions are available at `/home/administrator/projects/hexalith/platform/references/Hexalith.AI.Tools`. Do not mutate module checkouts under Platform.

### Builds first slice

Builds main `2c33b2d749a2f7b8c67a15130a815996f83a51a5` still failed `Assert-PackagedHostCatalogViews` for AppHost. Builds `874576d42b8e8668cce3f57bc1952175b0f19cfc` introduced the regression. The original candidate `e9e4cd501f44f76e764b906d16dbadaf1c3e334f` is preserved in `~/projects/hexalith/builds-2-14-view`.

The replacement is `eb298a4df75baf83d46c7514ea1f203a9f690b51` in the detached `~/projects/hexalith/builds-2-14-view-replacement` worktree. The audit binds catalog commit `3be0f781f37af47aa35e5eafdf0ae3bfa7f1878d`. The Platform, packaged-host and Timesheets selectors retain the 4.2 fallback. Parties and the shared default select EventStore 5.4.0. Commitlint 21.2.4 passed for both commits.

The packaged-host smoke check, 376 authoritative catalog probes, 17 packaged-host and frozen-v1 controls, 8 Python catalog tests and 48 focused .NET tests passed. The 331-package audit and all Builds CI catalog checks passed. The full Module.Tests run passed 3,165 tests with four context-dependent skips and zero failures. Parties `787aef2deed296b48677f01c9ace2d84e3f03cd5` built Contracts and AttachedLifecycleTests in Release package mode with zero warnings or errors. Restored assets confirm EventStore 5.4.0 for both projects and McpCli 3.0.0 for Contracts.

The owner approved `eb298a4df75baf83d46c7514ea1f203a9f690b51` and it was pushed to Builds main. The hosted public API check rejected the removal of `HexalithPartiesSdk42CatalogView` against the Builds v6.4.4 baseline. EventStore 5.4.0 remains the latest stable version for all 13 catalog packages on NuGet. [Failed public API job](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44762139869/job/136377143725)

Detached follow-up `6548f2486a0cb1618e99a503bce999a00ec857fa` documents the removal and carries its breaking-change marker. Commitlint, the public API check, the packaged-host check and audit validation passed locally. The pinned semantic-release analyzer classified the change as major. The owner approved the follow-up and it was pushed to Builds main. Hosted CI passed on that exact SHA, and Builds main was verified at the same commit. The owner separately approved release dispatch from the same revision. [Successful push CI](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44764079404) · [Approved release run](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44772694806)

The approved Builds release failed at `Check processed release security findings` on source `6548f2486a0cb1618e99a503bce999a00ec857fa`. Both CodeQL jobs reported the same four open high-severity Python alerts. Builds shared release tooling owns the gate. Builds `9e403c3b813a8d273057355201ed59d1f298757f` added Python to release scanning. Version calculation, package build and publication were skipped. [Release outcome](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44772694806)

- `Github/lib/publish_spike.py:241` at `b260185ebe0d0dd1ae9507b16f2b49b366ccd47c`. [Alert 1](https://github.com/Hexalith/Hexalith.Builds/security/code-scanning/1)
- `Github/lib/tests/test_release_draft.py:472` at `4dd100c62ee8d5dd2952cfaa5c2516245d5e1c1c`. [Alert 2](https://github.com/Hexalith/Hexalith.Builds/security/code-scanning/2)
- `Github/lib/tests/test_release_publish.py:174` at `9517bca108b07750e31b1af6df6cfde2bd691bc9`. [Alert 3](https://github.com/Hexalith/Hexalith.Builds/security/code-scanning/3)
- `Github/split-publication/tests/test_split_publication.py:658` at `daed31053919862fa7b65c94706a1d2c66d80701`. [Alert 4](https://github.com/Hexalith/Hexalith.Builds/security/code-scanning/4)

## Spec Change Log

- 2026-11-27: Marked `superseded` by `spec-2-14-close-out-version-only-pins-and-retire-sdk42-artifacts.md` after Builds 6.9.1, Parties 4.0.1, and Platform `v2.9.0` retired the SDK 4.2 catalog views and moved locks and tuple CI onto the unified catalog.

## Review Triage Log

## Verification

The disposable planning probe built Parties Contracts and AttachedLifecycleTests on the prospective catalog with zero warnings and errors. Check the complete catalog and package tests, the plain Debug Composition build and tests, and all three live profiles with `NO_COLOR=1 TERM=dumb`. Use `dotnet -nodeReuse:false` for Builds and run the existing PowerShell controls with `~/.dotnet/tools/pwsh`. Verify exact hosted run SHAs and preserve one compact summary.
