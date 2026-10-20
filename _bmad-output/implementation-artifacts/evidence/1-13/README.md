# Story 1.13 — local enrollment candidate

Status: local implementation and verification; **not released or Platform accepted**.
Builds owner review, gated upstream publication, released-pin acceptance and human
SM-2 remain pending. No SM-2 demonstration was performed or fabricated.

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

## Verification results

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
