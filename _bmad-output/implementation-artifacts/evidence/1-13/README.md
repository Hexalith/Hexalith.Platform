# Story 1.13 — local enrollment candidate

Status: local implementation and verification; **not released or Platform accepted**.
Builds owner review, gated upstream publication and released-pin acceptance remain
pending. The kit leaves SM-2 for module-owner verification after adoption; no live
demonstration was performed or fabricated by this story.

## Canonical identities

- Original Platform story baseline: `3d51fed1f3d99acf36ef690f150136593298bd95`.
- Original Builds implementation baseline / accepted release tag `v4.34.0`:
  `5d03f1db9c38db34ecac7fe6048937478ffb82cc`.
- During local implementation, an independent `/pushall` operation committed
  in-progress files to Builds `1c560aa3ad0407717ef0e2ce5fb5e48f0ffd32a1`.
  Review the **original baseline through the final working tree**, including both
  that commit and subsequent uncommitted corrections. This implementation session
  did not commit, push, publish or change remote state.
- Accepted published tool pin remains `4.34.0`; supported range remains
  `[4.34.0, 5.0.0-0)`. A concurrent gitlink change is not a new release acceptance.

## Local commands

From `references/Hexalith.Builds`:

```bash
dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj \
  -c Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=4.34.0 --no-restore
dotnet test/Hexalith.Builds.Module.Tests/bin/Release/net10.0/Hexalith.Builds.Module.Tests.dll \
  -class '*PlatformAdoptionValidationTests' -class '*PlatformManifestCommandTests' -noLogo
```

`MinVerVersionOverride=4.34.0` identifies local build outputs only; it is not a
published release. Local candidate command, from a disposable enrolled module Git
workspace (replace the assembly location with this checkout's absolute path):

```bash
dotnet /absolute/path/to/Hexalith.Builds.Module.Cli/bin/Release/net10.0/Hexalith.Builds.Module.Cli.dll \
  validate --manifest module/hexalith.module-manifest.v2.json --adoption
dotnet /absolute/path/to/Hexalith.Builds.Module.Cli/bin/Release/net10.0/Hexalith.Builds.Module.Cli.dll \
  validate --manifest module/hexalith.module-manifest.v2.json --adoption --output json
```

From Platform:

```bash
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj \
  -c Release -m:1 -p:NuGetAudit=false --no-restore
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Release/net10.0/Hexalith.Platform.Composition.Tests.dll \
  -class '*DomainEnrollmentKitTests' -noLogo
```

The passing workspace and deliberately failing input cases must report all nine
static requirements with file/reason. Expected failing adoption exit: `1`.
Read-only assertions compare Git status and file inventories before and after.
Human source review covers unsupported constructs; static rows leave SM-2
separately unverified. Focused and owning-project results are recorded below.

## Required release and acceptance handoff

1. Obtain Builds owner review of the complete baseline-to-worktree candidate.
2. Follow the existing Builds source/CI/package qualification and environment
   approval gates. Merge and publish through that pipeline; do not bypass it.
3. Verify the actual public NuGet version and canonical release-tag commit.
4. Pin that actual version in Platform `.config/dotnet-tools.json`, raise the
   `hexalith-platform.json` floor to it, retain upper bound `5.0.0-0`, and set the
   Builds gitlink to the exact release-tag commit. Update README acceptance facts.
5. Restore freshly and repeat both released pinned adoption output modes on
   passing/failing disposable module workspaces. Preserve exact commands, outputs,
   exits and unchanged-workspace evidence here, with canonical revisions and owner
   review/release links.
6. Module owners perform and record the real active-checkout breakpoint/edit and
   direct-only/nested demonstration using the pending template for Story 3.11.

Hosting inventory completeness grants no retirement authority. Every applicable
producer-adoption, owner-reviewed source/package/deployed parity and EventStore
AD-22 exact-subject consumer-removal gate remains independently required.

## Post-review verification

All 17 findings from three independent review passes were corrected. The targeted
regressions passed **65/65**, with zero build warnings/errors
([build](review-patch-build.txt), [tests](review-patch-tests.txt)). They cover
ordinary paths/comments, mixed-case dependency identities, self references,
frozen-host dependency checks and existence, MSBuild selectors/imports, declared
Dapr configuration keys in JSON/YAML/YML, qualified attributes, runnable tool
commands and committed Platform pin divergence. No review finding was deferred.
Three additional generic `PubSubName` format regressions passed separately on both
implementation and coordinator runs ([tests](generic-config-coordinator-tests.txt)).

Final verification uses detached Builds source `d7720206bcdde417a93a3514c903d107617107e7`
plus the reviewed corrections, isolated from concurrent workspace mutations.
[Source and package hashes](verified-source.json) bind that candidate to the live
story files. The stable Builds Release build passed with zero warnings/errors
([output](builds-stable-build.txt)).

The normal Platform composition test project also builds with zero warnings/errors
after a fresh restore and passes **4/4** kit tests
([build](platform-restored-build.txt), [tests](platform-normal-tests.txt)). Earlier
`--no-restore` failures below were stale restore state, not an established product
blocker. Artifacts must remain under the Platform workspace for its existing test
root-discovery convention.

The tool was packed and installed locally as `4.34.1-enrollment.20261020`
([pack](candidate-pack.txt), [install](candidate-install.txt)). This is a private
verification package, not a public version or accepted Platform pin; local build
metadata remains `4.34.0`. In a disposable fixture, `dotnet tool restore` and all
four actual `dotnet tool run hexalith-module -- validate --adoption` commands
passed: human/JSON success exits `0`, missing-inventory exits `1`, and Git state
plus every workspace file hash remain unchanged. See
[commands](packaged-local/candidate-commands.json),
[restore](packaged-local/tool-restore.txt),
[reproduction](packaged-local/accept-packaged.py.txt) and retained outputs.

The final serial Builds suite ran **1,185 tests: 1,182 passed, 0 failed, 3 skipped**
([output](builds-stable-serial-tests.txt)). The skips require an enclosing Platform
checkout with its real dependencies, which the detached verification snapshot
does not have. The three final generic-configuration cases were then verified
separately; production code is identical across both runs.

The earlier stable parallel Builds suite ran **1,185 tests**, with **1 cleanup timing
failure and 3 context-dependent skips** ([output](builds-stable-parallel-tests.txt)).
The failing cleanup method passed all **4/4** cases on a separate serial retry
([output](builds-cleanup-retry.txt)) and the final full serial run.
The initial 99 fixture failures below did not recur in the stable snapshot.

## Reviewed candidate awaiting owner approval

- Builds implementation/corrections: `b6642fecff8617e1765d835e71ebde316e3f71a5`.
- Final additional-regression candidate: `fff4a212180d8cd1c5c0759655a9bc4fb6963c45`.
- [Complete product diff](reviewed-change.patch) includes story changes from the
  original Platform and Builds baselines, excluding concurrent unrelated work.
- Exact commit messages passed the pinned owning-repository commitlint CLIs
  ([Builds](builds-commitlint.txt), [regression](regression-commitlint.txt),
  [Platform](platform-commitlint.txt)).

The session created local commits only. Owner approval, upstream release and fresh
released-tool Platform acceptance are still required before changing the accepted
pin/range or claiming Story 1.13 complete. The active pin remains `4.34.0`.

## Initial implementation verification (historical)

- Builds focused Release build: **PASS**, zero warnings/errors
  ([build output](builds-focused-build.txt)).
- Builds adoption/manifest-command classes: **97 tests, 0 failures**
  ([test output](builds-focused-tests.txt)).
- Focused Platform lane compiling the actual `DomainEnrollmentKitTests.cs` against
  the same candidate Tooling DLL: **4 tests, 0 failures**, zero build warnings/errors
  ([build](platform-focused-build.txt), [tests](platform-focused-tests.txt)).
  Its disposable project uses the existing pinned packages from the local NuGet
  cache; [project](focused-project.csproj.txt) and
  [empty central-import boundary](focused-Directory.Packages.props.txt) are retained.
  Run it with `dotnet artifacts/enrollment-focused/bin/Release/net10.0/EnrollmentFocused.dll -class '*DomainEnrollmentKitTests' -noLogo`.
- Direct local candidate CLI: **4 commands passed**. Passing human/JSON commands
  exit `0`; deliberately wrong/missing tool pin exits `1` in both formats.
  [Exact commands/exits and unchanged-state results](candidate-commands.json),
  [passing human](candidate-pass-human.txt), [passing JSON](candidate-pass-json.txt),
  [failing human](candidate-fail-human.txt), [failing JSON](candidate-fail-json.txt).
  Every file hash and Git status stayed unchanged for every invocation; before/after
  Git outputs and stderr are retained beside these files. The disposable workspace
  was removed afterward. [Local reproduction script](accept-candidate.py.txt).
- Normal Platform composition test-project Release build: **BLOCKED**, 7 errors
  and 4 warnings in existing Works.Runtime/Identity dependencies: resolved
  EventStore assemblies lack DomainService, Authentication, Authorization and
  related types. [Exact broad build output](platform-normal-project-build.txt).
  The focused lane above does not clear this broad gate.
- Full Builds Module project: **1,138 tests, 99 failures**, 0 errors/skips
  ([full output](builds-module-full.txt)); no adoption or manifest-command failure.
  Concurrent non-story work changed legacy v1 loader/schema/fixture pins and G6
  qualification inputs while this suite ran. Do not infer release qualification
  from the focused pass or alter those unrelated inputs to hide broad failures.
- `git diff --check` passed in both owning repositories.

Final observed Builds HEAD: `fbd5abd07115a90ce77a94da1e9db4990ccabad3`,
plus uncommitted final adoption corrections. Between the original Builds baseline
and this HEAD, concurrent changes also affect G6 policy/approval records,
package-qualification scripts, legacy v1 loader/schema and fixtures/tests. The story
scope is the adoption result/validator/audit/inventory types, CLI/diagnostic wiring,
adoption fixture/test class and adoption cases in `PlatformManifestCommandTests`.
Use that scope to avoid attributing or staging unrelated concurrent work.
