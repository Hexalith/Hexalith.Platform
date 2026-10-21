# Story 1.13 — domain-module enrollment kit and correction candidate

Status: **post-release corrections locally verified; new review/publication/pin acceptance pending**.
The earlier owner-approved candidate was published as `4.35.0` through the gated
Builds release workflow. The subsequent review reproduced enrollment defects in
real module workspaces; the local correction evidence is recorded below. The
existing pin remains `4.35.0` until the corrected candidate passes owner review
and the existing release gates. The kit leaves SM-2 for module-owner verification
after adoption; no live demonstration was performed or fabricated by this story.

## Final reviewed correction candidate — 2026-10-21

Exact local Builds source: `4841778b0b018ff017cd31805aafea5f4ae3c011`.
The three required review layers completed. All 11 surviving root-cause groups
were patched and tested; three low findings were rejected with individual reasons
in the story triage log. Nothing was deferred.

Final coordinator verification supersedes the pre-review outputs below:

- Complete Builds Module suite: **1,293/1,293 passed**, zero errors/failures/skips,
  486.110 seconds ([output](post-release-patch/builds-post-review-full-tests.txt)).
- Review patches: **173 adoption tests passed**, followed by **17 final path/CLI
  cases passed** after fixture cleanup ([adoption](post-release-patch/builds-review-fixes-tests.txt),
  [final cases](post-release-patch/builds-review-fixes-final-cases.txt)); the complete
  suite above also ran these final cases.
- Final normal Platform build: zero warnings/errors
  ([output](post-release-patch/platform-post-review-build.txt)); **5/5** kit tests
  pass against the actual published `4.35.0` pin
  ([output](post-release-patch/platform-post-review-kit-tests.txt)).
- Final real-Parties human/JSON runs both preserve Git status and all **6,035**
  tracked-file hashes; workspace enumeration passes across **474,480 bytes**.
  Both exit `1` for the real module's incomplete enrollment
  ([commands](post-release-patch/real-workspace-commands.json)).

The local source commit and its message passed pinned commitlint. The candidate
remains **unpublished**, and tool pin `4.35.0` does not contain these corrections.
Owner review, exact-source upstream CI, existing gated publication and fresh
Platform published-pin/range-floor/release-tag-gitlink acceptance remain pending.
Story status stays **in-review** until those explicit completion gates pass.

## Initial correction verification before required review — 2026-10-21

These corrections implement the approved patch findings in the story spec. They
have not established a new published or Platform-accepted release. No pin, support
floor or runtime dependency was changed by this local implementation. Builds owner
review, exact-source CI, gated publication, the actual published pin/release-tag
gitlink and fresh released-tool acceptance remain required for these corrections.

During this implementation an independent `/pushall` captured in-progress files:
Builds `1e87f193d7d8450233faa1e46a7a646edfaf5c79` and
`32af59cdaa2ae4786ab80c59433e17adccbba62b` and
`17d0c6ffec5fb3c8d8058e67cb85644460cb5256`, and Platform
`bc36d21d4b049911253beffaf45deb9bb806024e` and
`7b5548f0d308d27af9d5fda5b68c6b959c053185`. Those commits are concurrent
observations, not owner approval or release evidence. Review the final worktree
against the original story baselines below, including these captured edits; do not
use a HEAD-only diff. The post-release correction baseline is Builds
`fff4a212180d8cd1c5c0759655a9bc4fb6963c45` and Platform acceptance
`258269e3896c533d763a2fdb233ed9d4287272d5` (preserve subsequent unrelated work).

The final Builds Release build passes with **zero warnings/errors**
([output](post-release-patch/builds-build.txt)). All **188/188** focused adoption
and manifest-command cases pass with no skips
([output](post-release-patch/builds-focused-tests.txt)). The final complete serial
Module suite passes **1,235/1,235**, with zero errors, failures or skips in
441.130 seconds ([output](post-release-patch/builds-full-tests.txt)). The first
full run exposed one newly added helper-argument regression; bounding the match to
the actual argument fixed it. The subsequent focused and complete runs above
supersede that [initial result](post-release-patch/builds-full-before-argument-bound.txt).

Platform's normal composition test project builds with **zero warnings/errors**
([output](post-release-patch/platform-build.txt)) after a fresh Release restore
using **only the existing local NuGet cache**
([output](post-release-patch/platform-release-restore.txt)). The initial
[stale-assets build](post-release-patch/platform-stale-assets-build.txt) is
superseded. All **5/5 kit cases** pass against the actual pinned published
`4.35.0` tool via `dotnet tool run`, including the exact pin/range-floor and
roll-forward assertions ([output](post-release-patch/platform-kit-tests.txt)).
The CLI tests parse stdout independently from stderr and customize the pending
inventory owner/consumer fields. To reproduce from Platform:

```bash
dotnet tool restore
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj \
  -c Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=4.35.0 --no-restore
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Release/net10.0/Hexalith.Platform.Composition.Tests.dll \
  -class '*DomainEnrollmentKitTests' -parallelMode none -noLogo
```

The local `MinVerVersionOverride=4.35.0` identifies build artifacts only; it does
not publish or accept these corrections as version `4.35.0`. The released CLI
assertion uses the restored published package separately from the source validator.
These pre-review outputs were superseded by the final reviewed checks above. Scoped `git diff --check`
passes in both owning repositories.

The current candidate's [source/artifact identities](post-release-patch/candidate-identities.json)
include exact hashes and original-story/post-release review patch hashes. The
[Builds story diff](post-release-patch/builds-original-story.patch) and
[Platform story diff](post-release-patch/platform-original-story.patch) retain the
original-baseline view. The narrower correction diffs are
[Builds](post-release-patch/builds-post-release.patch) and
[Platform](post-release-patch/platform-post-release.patch). Their retained context
whitespace is intentional; the local evidence attributes preserve their bytes.

The corrected local CLI was also exercised read-only in the real Parties module
checkout at `6d9e3417f7abcd03e66a04de5084909c4b6c6115`. Its tracked-path output
is **474,480 bytes**, and the workspace requirement passes. Human and JSON runs
both preserve Git status and SHA-256 hashes of **6,035 tracked files**. Both exit
`1`, correctly leaving adoption incomplete: this nested checkout has uninitialized
required direct references, no hosting inventory and legacy Dapr patterns; the
bounded dependency audit also identifies a dependency-directory probe in a fitness
test. No other module was enrolled or edited, and no initialization was performed.
The source scans finish without the former enumeration or oversized evidence
failures. See [exact commands and results](post-release-patch/real-workspace-commands.json),
[human output](post-release-patch/parties-human.stdout.txt),
[JSON output](post-release-patch/parties-json.stdout.txt) and
[reproduction script](post-release-patch/verify-real-workspace.py.txt). The retained
before/after inventories bind the read-only check to each invocation.

The documentation command check copies templates only when absent and merges only
the accepted tool entry while retaining an existing `aspirate` `9.1.0` pin. See
[check script](post-release-patch/check-guide.py.txt) and
[output](post-release-patch/guide-commands.txt).

## Canonical identities

- Original Platform story baseline: `3d51fed1f3d99acf36ef690f150136593298bd95`.
- Original Builds implementation baseline / accepted release tag `v4.34.0`:
  `5d03f1db9c38db34ecac7fe6048937478ffb82cc`.
- During local implementation, an independent `/pushall` operation committed
  in-progress files to Builds `1c560aa3ad0407717ef0e2ce5fb5e48f0ffd32a1`.
  The reviewed product diff covers the **original baseline through the final
  candidate**, including that commit and subsequent corrections. Owner-authorized
  release operations are recorded below.
- Accepted published tool pin: `4.35.0`, with `rollForward: false`.
- Supported range: `[4.35.0, 5.0.0-0)`.
- Previously accepted Builds gitlink (2026-10-20) and `v4.35.0` release tag:
  `fff4a212180d8cd1c5c0759655a9bc4fb6963c45`.
- [Canonical release and acceptance identities](accepted-release-identities.json).

## Prepublication local commands (historical)

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

## Release acceptance procedure (steps 1–5 completed)

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

## Prior release verification — 2026-10-20

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

## Owner-approved candidate and gated release

- Builds implementation/corrections: `b6642fecff8617e1765d835e71ebde316e3f71a5`.
- Final additional-regression candidate: `fff4a212180d8cd1c5c0759655a9bc4fb6963c45`.
- [Complete product diff](reviewed-change.patch) includes story changes from the
  original Platform and Builds baselines, excluding concurrent unrelated work.
- Exact commit messages passed the pinned owning-repository commitlint CLIs
  ([Builds](builds-commitlint.txt), [regression](regression-commitlint.txt),
  [Platform](platform-commitlint.txt)).

The owner explicitly approved this exact candidate and its gated release at
2026-10-20T12:57:36Z: “Approve candidate and release”
([record](owner-approval.json)). Builds main was pushed to the approved candidate.
GitHub accepted the owner-account push using its configured branch-rule bypass;
the release source, CI, qualification and production environment gates remain
unchanged. No guard configuration was changed or waived.

Exact-source [CI 39426403061](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39426403061)
passed ([machine result](ci-result.json), [suite summaries](ci-test-summaries.txt)):
Module 1,188 total / 1,185 passed / 3 skipped; Evidence 107/107 passed;
Tooling integration 13 total / 1 passed / 12 skipped. The G-4 package qualification
and Python/CodeQL checks passed. The skipped tests require their documented
external context; they were not converted into passes.

The official [release run 39431435493](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39431435493)
uses the approved full source SHA. Its exact-current-main/green-push-CI source
guard passed. The recorded owner authorization was applied through the ordinary
`production` environment approval API under the configured reviewer
([approval response](production-gate-approval.json)). The workflow completed
successfully and [published `v4.35.0`](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v4.35.0)
at 2026-10-20T14:14:32Z. See [workflow result](release-result.json),
[release metadata](published-release.json),
[qualified package inventory](published-package-inventory.json) and
[publisher's remote verification](release-publication-hashes.txt).

## Prior v4.35.0 Platform acceptance — 2026-10-20

Platform now pins `4.35.0`, disables roll-forward, sets the range floor to `4.35.0`
and retains upper bound `5.0.0-0`. The exact Builds release-tag commit is the
Platform gitlink. The runtime catalog, SDK pin and AppHost topology are unchanged
from `v4.34.0`; this story upgrades no runtime dependency.

A fresh [Platform restore](platform-released-tool-restore.txt) from NuGet.org reports
[`4.35.0+fff4a212180d8cd1c5c0759655a9bc4fb6963c45`](platform-released-tool-version.txt).
The acceptance harness uses an empty `DOTNET_CLI_HOME`, an empty package cache,
an explicit `globalPackagesFolder` and NuGet.org as its sole source. It downloads
the public package, records its SHA-256 and runs all four documented tool commands:
human/JSON success exits `0`, missing inventory exits `1`, and Git state plus
every workspace file hash remain unchanged. Retained before/after inventories
bind the read-only assertions to every invocation. See
[commands and hashes](released-package/released-commands.json),
[restore](released-package/tool-restore.txt),
[version](released-package/tool-version.txt) and
[reproduction script](released-package/accept-released.py.txt).
An initial harness attempt stopped before validation because the existing .NET
tool cache was reused; explicit CLI-home and package-folder isolation corrected
the harness. The successful run above used an empty cache.

NuGet repository signing changes the archive hash. The fresh download's signed
SHA-256 matches the public hash verified by the official publisher. The publisher
also verified every uncompressed payload entry against the qualified unsigned
package, excluding only the repository signature. The release inventory binds
that qualified package to the exact clean approved source. Both hashes are
preserved in [acceptance identities](accepted-release-identities.json).

All **4/4 Platform kit tests** passed against the official released CLI
([test output](platform-released-kit-tests.txt)). The earlier normal Platform
build passed with zero warnings/errors; the exact-source upstream CI ran
**1,188 Module tests: 1,185 passed, zero failed, 3 context-dependent skips**.
All 17 review findings were corrected; none were deferred. The final acceptance
commit message passed the pinned Platform commitlint CLI
([validation](acceptance-commitlint.txt)).

No other module was adopted or retired. Module owners still perform their real
active-checkout breakpoint/edit and direct-only SM-2 demonstration and record it
in the pending checklist for Story 3.11. Static acceptance supplies no retirement
permission or live SM-2 evidence.

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
