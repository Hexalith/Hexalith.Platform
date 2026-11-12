---
title: 'Synchronize and release the current Platform ecosystem'
type: 'chore'
created: '2026-11-12'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '3b1dddecbca214c94e3a7b1b65274f3827916178'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="user authorized maintenance and release with human validation bypassed">

## Intent

**Problem:** The user wants every change pushed, all root submodules synchronized, the latest .NET SDK and packages adopted, CI/CD failures repaired, a release created, and NuGet publication verified. The workspace is initially clean on main, uses 17 direct submodules, and has active remote CI/release work.

**Approach:** Adopt published upstream changes, verify current stable SDK and compatible package releases against authoritative registries, repair demonstrated CI failures without weakening checks, validate the final source and publish through the owning repositories' workflows. The user explicitly authorizes release and bypassing human validation; no additional skill approval checkpoints are required.

## Boundaries & Constraints

**Always:** Keep actual automated checks and source/package identity verification. Use Conventional Commits validated by each owning repository's pinned commitlint. Push submodule commits before parent pointers. Verify recorded commits exist on origin. Respect the shared catalog as the version authority. The latest stable SDK is 11.0.100 and is already installed and pinned throughout .NET repositories. Retain net10.0 target frameworks unless a demonstrated package upgrade requires a separately coherent migration; adopt latest compatible dependencies. Capture remaining incompatible major updates honestly.

**Never:** Initialize nested submodules, force push, discard existing work, expose credentials, fabricate qualification/publication evidence, or treat a source-only release as NuGet publication. This is software maintenance and publication, with no infrastructure deployment or product-acceptance authority.

</frozen-after-approval>

## Code Map

- `global.json`, `Directory.Build.props`, `Directory.Packages.props`: SDK 11.0.100, net10.0 targets, package import from the Builds catalog.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: shared authority; most packages already current. Framework-aligned packages stay on compatible 10.0 patches. Live registry differences are captured in `/tmp/platform-catalog-outdated.json`.
- `infrastructure/Directory.Packages.props`: AWS SDK packages have current compatible 4.1.0 releases.
- `package.json`, `package-lock.json`: npm registry reports conventional-changelog-conventionalcommits 10.4.1 replacing 9.3.1; validate release control tests after upgrading.
- `src/Hexalith.Platform.TenantRegistry.Host/Directory.Packages.props`: legacy Gateway 3.128.4 override versus published 4.2.0. Inspect consumers and compatibility before removing the override.
- `eng/tuple-source-preparation.md`, `eng/prepare_tuple_sources.py`: prepare exact current binding and same-code historical qualification commits. Updating parent source needs fresh historical metadata, including the package-reader source; preserve historical inputs and proofs.
- `eng/current-tuple-source.json`: owner revisions must match synchronized direct submodule commits.
- `.github/workflows/tuple-ci.yml`, `catalog-builder.yml`: exact-source automated qualifications before release.
- `.github/workflows/source-release.yml`: semantic-release creates a source tag and GitHub release; use an appropriate conventional fix/feature commit for maintained behavior changes.
- `.github/workflows/test-extension-release.yml`: manually dispatch version at exact released current main, publish Hexalith.Platform.TestExtension through configured NuGet OIDC. Production requires user approval; the user authorizes approving the run.
- `/tmp/platform-sync-audit-core.json`, `/tmp/platform-sync-audit-domain.json`: evolving read-only CI audits of synchronized ecosystem sources. Confirm every failure still applies at current main; other sessions may advance remotes.
- EventStore `tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs`: stale tool version 4.48.0 at line around 597 vs 4.48.2. SignalR.Tests has net10/11 Logging.Abstractions and DiagnosticSource conflicts.

- Shared catalog adoptable upgrades: HexalithCommonsVersion 2.30.11, HexalithPolymorphicSerializationsVersion 1.19.9, Microsoft.Build/Framework/Utilities.Core 18.12.2. Changing the published catalog requires Builds validation and publication before refreshing Platform publication identities. Evidence in `/tmp/platform-builds-package-audit.json`.
- McpCli `tests/Hexalith.McpCli.Core.Tests/InventoryCoverageTests.cs:526`: Windows directory read produces DirectoryNotFoundException rather than UnauthorizedAccessException. Preserve meaningful rejection and test the correct portable contract. Windows artifact `/tmp/mcpcli-windows-failure-audit`.
- Agents `test/Hexalith.Agents.Server.Tests/PackageInventoryTests.cs`: stale workflow SHA 163f3d in two assertions; `eng/consumer-executable-assets.json` pins FrontComposer 4.7.6 while resolved 5.0.0. Recompute actual assets rather than guessing hashes.
- Memories `tests/Hexalith.Memories.Cli.Tests/Ci/CiTestInventoryTests.cs:1118`: duplicate InlineData breaks build. Qualification gitlink/declaration identity mismatch must preserve evidence-backed admission rather than manufacture validation.
- Folders current manifest hash for `.github/workflows/ci.yml` is stale; inspect owning generator and regenerate hashes.
- Projects `tests/tools/test_current_g6_policy.py`: current CI lane lacks initialized Builds needed for g6_current.py. Initialize declared direct dependency before policy lane.
- Parties release candidate ledger lacks current Builds/Platform entries; investigate actual existing validation evidence and preserve real admission requirements.
- Tenants IntegrationTests framework10/package11 assembly conflicts require matching explicit package references or pruning configuration.
- ChatBot release recovery gets BootstrapGlobalAdmin 503 because catalog authority cannot admit; inspect full release failure before changing code.

## Tasks & Acceptance

**Execution:**
- [ ] SDK and dependency files -- verify latest stable/compatible versions and update actual outdated dependencies -- keep builds reproducible.
- [ ] Owning submodule CI files and tests -- repair confirmed current failures from audits and validate focused lanes -- restore meaningful automation.
- [ ] Root gitlinks and tuple bindings -- synchronize committed remote sources and regenerate honest historical qualifications -- retain exact-source controls.
- [ ] Release workflows -- push verified commits, monitor exact-source CI, create releases and approve pending release environments when needed -- carry out explicit publication request.
- [ ] Public NuGet archives -- verify expected versions and source metadata where available -- distinguish successful publication from release creation.

**Acceptance Criteria:**
- Given current registries, when dependency checks run, then SDK and adoptable compatible dependency versions are current, with any incompatible major versions identified.
- Given final synchronized sources, when owning CI runs, then required jobs pass without bypassing automated checks.
- Given released source, when publication runs, then a stable GitHub release exists and expected NuGet packages are publicly retrievable.
- Given completed synchronization, when git status and divergence checks run, then all content is pushed and the parent records remotely available root submodule commits.

## Implementation Notes

- Initial inspection found every root submodule clean on main. Fetched and fast-forwarded available origin/main changes non-recursively; the parent is now dirty only through intended gitlink updates and this specification.
- User explicitly requested bypassing human validation. Skill approval gates are superseded; automated validation remains required.
- Irreversible publication is explicitly authorized. Root coordinator handles commits, pushes, release orchestration, final source bindings and publication evidence after local implementation. Implement local fixes sequentially. Send the coordinator a message whenever an owning submodule is ready for commit, with changed files and validation commands/results, so publication can proceed during the remaining implementation. Do not stage final parent bindings against an unpublished Builds catalog. Avoid modifying the spec file during implementation; report notes to the coordinator. Other sessions may advance remote main; inspect local status and origin/main before changing files. Do not load repository skills from references.

## Spec Change Log

## Review Triage Log

## Verification

- `npm run test:source-release` and fixture release control tests -- existing refusal and publication checks pass.
- `python3 -B eng/test_current_tuple_source.py` plus historical/source preparation tests -- exact-source controls retained.
- Focused owning .NET project builds and native xUnit test invocation -- confirmed CI failures fixed with measured outputs.
- Exact current-source GitHub Actions jobs -- required CI and release jobs pass.
- NuGet flatcontainer requests and nuspec metadata -- expected stable archives published.
