---
title: 'Update and release the synchronized workspace'
type: 'chore'
created: '2026-11-11'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '48ca87cf7e90c873cd922f00f7e442287236c621'
context:
  - '/home/administrator/projects/hexalith/platform/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '/home/administrator/projects/hexalith/platform/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="User explicitly authorized updates, fixes, pushes, releases and bypassing human validation in the invocation">

## Intent

**Problem:** The workspace is synchronized but its release inputs, dependencies, SDK selections and CI checks have drifted. Current Platform qualification fails before execution, and several module pipelines cannot publish current source.

**Approach:** Update the SDK and compatible package versions, repair actual CI failures, then publish the synchronized source and packages with automated verification. The user explicitly authorizes committing, pushing and releases, including passing human approval gates without another question. This authorization supersedes workflow checkpoints that request human validation.

## Boundaries & Constraints

**Always:** Preserve net10 consumer targets while selecting stable SDK11.0.100. Keep SDK10 runtime/reference packs available in CI. Preserve exact-source and artifact integrity checks, test assertions, historical regression cohorts, and honest acceptance labels. Validate exact commit messages with each owning repository's pinned commitlint before committing. Push submodule commits before the parent pointer commit. Verify package identity and actual NuGet availability after publication. Make changes sequentially within each dependency chain.

**Never:** Initialize nested submodules, force-push, fabricate passing results, remove automated qualification merely to release, print credentials, or claim native runtime acceptance from source/package checks. Existing compatible ABI holds require an explicit reason when a newer package is unavailable to net10 consumers. Do not rewrite frozen historical evidence.

</frozen-after-approval>

## Code Map

- Root `global.json` already selects SDK11.0.100, verified against Microsoft release metadata. All 17 root-declared repositories have clean main worktrees equal to fetched origin/main at baseline.
- Five old `references/Hexalith.{Agents,ChatBot,Conversations,Timesheets,Works}/global.json` files select10.0.403. Their direct setup-dotnet steps need runtime retention when upgraded.
- `references/Hexalith.Builds/Props/Directory.Packages.props` owns shared versions. Most are latest; Swashbuckle11 supports net10. ASP.NET OAuth11 and MSBuild18.12 packages require net11; preserve compatible net10 versions. Immutable11 has a documented assembly conflict hold.
- Root infrastructure AWS pins have patch updates; root js-yaml5.5.0 needs actual YAML controls. Timesheets local Aspire/MCP/OTel overrides lag behind the catalog. Package audit details are in `/tmp/platform-package-audit.json` and `/tmp/platform-npm-all-audit.json`.
- `/tmp/submodule-ci-errors.json`, `/tmp/submodule-ci-details.json`, `/tmp/submodule-release-audit.json` and `/tmp/Hexalith.*-job-*.log` contain current exact-SHA CI evidence. Shared failures include untagged Platform selections, Platform/Builds pin mismatch, stale catalog vulnerability and stale consumer executable baselines. Local failures include Builds Stage3, Works lint, Timesheets recursion, Projects prerequisite fixtures and McpCli validation.
- `.github/workflows/tuple-ci.yml`, `eng/builds-tuple.json`, `.config/dotnet-tools.json`, `hexalith-platform.json` bind a published Builds tuple. The gitlink0677db83 differs from recorded producer5ec17790. Latest published4.48.0 predates current package/SDK changes, so publication ordering matters.
- `eng/legacy_tuple_source.py` and `eng/pre_extension_tuple_source.py` reject stale historical companions correctly. Regenerate companions from final current source with only frozen tuple metadata/profile/provenance/owner gitlinks substituted; retain strict full-tree comparisons. Current Parties verification hardcodes an obsolete owner revision and must authenticate the selected committed owner/declaration without weakening historical checks.
- Root source releases use `source-release.yml` after both tuple and catalog CI. `test-extension-release.yml` publishes the sole Platform fixture package with configured Trusted Publishing. Production approval can be submitted under the user's explicit authorization. Folders run43112314702 was approved after successful CI; Memories run43106868671 was already publishing.

## Tasks & Acceptance

**Execution:**
- [ ] Five submodule SDK files and applicable workflow setup steps: select SDK11 and retain net10 runtime availability.
- [ ] Owning package manifests/catalogs: update compatible stable dependencies and lockfiles, document necessary compatibility holds, run affected controls.
- [ ] Owning CI tooling/tests: fix root causes shown by current CI logs, preserving assertions and source provenance requirements. Include supported shared-workflow pins, release runtime retention, exact npm inventory assertions and current production closure expectations.
- [ ] Builds publication and Platform tuple files: bind a real current published producer, then repair historical same-code companions and current owner verification. Independently measure finite707/326 owner artifacts, retain historical cohorts, authenticate current declaration bytes, and protect Git commit/blob/checkout checks with behavioral controls.
- [ ] Owning repositories: validate commits, push changes and verify remote availability; commit parent pointers only after dependent pushes.
- [ ] Release workflows: create the qualified root release, pass authorized human gates, publish packages, and verify exact versions from public NuGet metadata/archive.

**Acceptance Criteria:**
- Given a current worktree, when synchronization finishes, then every recorded submodule revision exists on its remote and all worktrees are clean.
- Given live dependencies, when updates finish, then all SDK selections use the latest stable SDK and package upgrades either pass relevant checks or have a precise compatibility constraint recorded.
- Given updated source, when current CI executes, then required checks succeed without fabricated qualification or weakened assertions.
- Given successful automated qualification, when publishing completes, then the release identifies the exact source and each expected package version is available on NuGet.

## Implementation Notes

The user waived human checkpoints in the original instruction. Automated review and validation remain required. Implementation may proceed in dependency order and report concrete intermediate blockers to the orchestrating agent so that release prerequisites can be resolved before further work. Read each owning AGENTS.md before edits; do not load skills under references.


Local verification completed: root source-release controls pass82/82; extension gate14/14 and archive30/30. Tuple binding19/19, SDK profile8/8, legacy20/20, pre-extension12/12, current source14/14 and isolated-reader5/5 controls pass. Three targeted mutation checks detect removal of owner HEAD, actual release declaration blob and commit-type guards. Infrastructure AWS and package compatibility scenarios pass. Root Composition.Tests Release build has zero warnings/errors; the complete published-pair cross-product controls pass4/4. The retained C# cohort/production checks pass101/101 with no skips and the genuine current public/private reader passes23/23. npm verifies497 registry signatures and129 attestations. Package pruning is disabled at the root so centrally selected Extensions11 assemblies remain consistent across libraries and ASP.NET hosts.

Builds producer163f3d0deac6b0db5ec4bdfcc0934b12e6adb46a passed exact-source hosted CI43114507351 and release43119860909. Both signed4.48.1 NuGet tools, release inventory, catalog blob and paired AppHost/Tooling bodies are independently verified and bound atomically. Memories3.0.27 has all10 expected public packages; Folders1.4.0 has all5. The Dapr1.19.1 runtime and CLI release archives match their official published checksums; extracted executable versions and runtime SHA256 are measured separately. Real Works toolchain qualification,7 toolchain controls and72 host controls pass. Full R1–R11 Works parity remains a behavioral CI prerequisite, not inferred from artifact hashes.

Submodule SDK selections in Agents, ChatBot, Conversations, Timesheets and Works use11.0.100 with explicit net10 runtime retention. Additional fixes cover SDK11 native test invocation, Python3.14 tooling, current dependency locks and source/package release markers. At source preparation, several module checks still require the forthcoming qualified Platform tag; final module pins, hosted CI and final source/package publication remain execution tasks and are recorded outside this immutable source snapshot until completed.

## Spec Change Log

Automated review identified remaining dependency-order finalization and stale finite cohort/test data. Clarified the non-frozen execution tasks to include those concrete repairs. KEEP all historical entries and counts, the source/package identity separation, passing infrastructure changes, SDK updates, verified commit/publication evidence and explicit acceptance limitations.

## Review Triage Log


| Finding | Verdict / route | Evidence and resolution |
| --- | --- | --- |
| Blind1: unsupported reusable-workflow SDK inputs | high / patch | Exact pinned caller blobs omit the input; four hosted runs report startup_failure. Align callers and their recorded producer pins. |
| Blind2: current source binding absent | high / patch | New current-lane verification requires the committed binding. Generate it from the final staged tree before pushing root. |
| Blind3: producer/publication identity stale | high / patch | Recorded5ec17790/4.47 differs from selected163f3d0d. Complete the already planned producer publication and atomic binding update. |
| Blind4: historical companions stale | high / patch | Strict comparison correctly refuses changed helpers and workflows. Regenerate immutable companion commits after final source preparation. |
| Blind5: current published artifact cohorts lag catalog | high / patch | Exact selectors only accept705/325 while selected catalog uses707/326. Add independently measured finite package identities and retain historical entries. |
| Blind6: private current-owner declaration lag | high / patch | Runtime constant d9c193b0 differs from the selected committed dfb03b6e declaration. Bind the actual current declaration and update affected controls without native acceptance claims. |
| Blind7: production closure assertion lag | high / patch | Required test hardcodes Server705 while current dependency selection is707. Verify the signed707 archive and test its actual production output. |
| Blind8: Memories npm graph assertions lag | high / patch | Manifest changed but C# inventory assertions still require old override versions. Update the exact assertions and execute the owning test. |
| Blind9: release runtime installation incomplete | medium / patch | Old Agents/Works release implementations install only global SDK11. Pin supported helpers and explicitly retain SDK10. |
| Blind10: tree object accepted as release commit | high / patch | A real isolated Git tree-tag fixture satisfies the current generic peel. Require commit peeling and reject tree tags. |
| Edge1: unsupported reusable input | high / patch | Independently confirms Blind1 against all four actual pinned blobs; same repair. |
| Edge2: binding absent | high / patch | Independently confirms Blind2; final committed binding remains mandatory. |
| Edge3: cold release runner lacks net10 | medium / patch | Independently confirms Blind9's supported runner boundary; explicit install required. |
| Verification1: initialized owner HEAD check lacks regression control | medium / patch | Reviewer removed the revision guard and all39 controls still passed. Add unchanged-manifest/wrong-HEAD rejection. |
| Verification2: actual release blob digest lacks regression control | medium / patch | Reviewer removed the blob guard and all39 controls still passed. Add internally consistent forged snapshot/provenance/binding against the real release blob. |
| Verification-other1: binding absent | high / patch | Same concrete absent-file failure as Blind2; final preparation will supply it. |

| FinalBlind1: current binding absent | high / patch | carried Blind2: generate exact committed owner/package source binding from final staged objects before push. |
| FinalBlind2: reader source selector absent | high / patch | The new current/base collector requires an explicit reader SHA; final descriptor preparation records the published third companion. |
| FinalBlind3: historical companions stale | high / patch | carried Blind4: regenerate all three immutable companions after source patches, and validate strict source equivalence before push. |
| FinalBlind4: prepared fixture rejects descriptor changes | high / patch | Fixture allowed paths omit the two bounded descriptors that necessarily change after companion creation. Permit these paths and authenticate their actual committed selections with the production source verifier. |
| FinalBlind5: complete current private preparation untested | medium / patch | Existing retained integration cases prove705/25/310 only. Replace a redundant selector control with real current707/26/311 source proof and reader preparation, stopping before resource allocation. |
| FinalBlind6: image-authority control stops at helper | medium / patch | Production selection can return the new public reader purpose, so a helper-only assertion does not prove refusal. Explicitly refuse both new current purposes in image binding and test the actual entry after genuine current preparation. |
| FinalBlind7: non-SDK package release metadata not checked | medium / patch | Current reader verifier records owner nuspec identities without comparing them to retained independent measurements. Authenticate exact archive and source metadata for each measured package. |
| FinalBlind8: Works behavior evidence outstanding | high / patch | This is required execution finalization, not artifact-derived acceptance: full R1–R11 parity must pass for the exact new Platform source/runtime before completion; current evidence explicitly claims executable identity only. |
| FinalBlind9: retained workspace lifecycle leaks | medium / patch | Two current/base invocations retain complete owner checkout trees past test completion. Scope the task-owned directories and clean up after output/identity evidence is captured, including failure paths. |
| FinalBlind10: duplicated historical wording | low / patch | Directly correct the repeated word in the source-release documentation. |
| FinalVerification1: no valid current private EventStore request | medium / patch | Valid single-Tenants requests never execute the new Program switch arm; add a real executable success with23 routes/operations/declarations,12 idempotency entries and six exact receipt bodies. |

## Verification

- Run affected repository build/test commands with package references and Release settings for CI parity; execute test projects individually.
- Run `python3 -B eng/test_builds_tuple.py`, `python3 -B eng/test_legacy_tuple_source.py`, `python3 -B eng/test_pre_extension_tuple_source.py`, `python3 -B eng/test_tuple_sdk_suites.py` and `npm run test:source-release` after root qualification changes.
- Inspect latest exact-source GitHub Actions jobs and failed logs after every pushed fix. Check NuGet flat-container indexes and release archive identities after publication.
