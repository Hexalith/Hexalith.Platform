# Story 1.13 pass-4 local corrections

Platform's recorded Builds gitlink and checkout are restored to the published
`v4.35.1` source `f8599224457dda99e7c0ae4870147dd51b464feb` in local Platform commit
`f97065cc31c2bdc1afd11c515298cc8f1a51380b`. The exact tool pin remains `4.35.1`, roll-forward disabled, with range
`[4.35.1, 5.0.0-0)`. The concurrent pointer-changing commit remains in history.
No remote branch was changed and no release was dispatched. The owner-directed
rollback remains in force.

The unpublished pass-3 candidate `af4bf9f2e4d3fd913c536a08bb0c9e8a54f9f260` and
integration `1ae50de72e0755a2a43a1b33764b6b87eecba154` are preserved on Builds branch
`fix/domain-enrollment-pass-4`, with new corrections developed in isolated worktree
`/tmp/hexalith-builds-enrollment-pass-4`. Pre-review local candidate commit is
`26317ab0060fc53dc842193729a503e144d142be`; that checkpoint was clean and its source
bytes match the pre-review tested hashes. Platform compiles the accepted checkout;
it does not consume those unpublished corrections.

The guide now describes the accepted tool's actual frozen-directory, source-scan
and asserted-probe behavior. It identifies unpublished corrections as pending the
next owner-approved release. The kit's offline pin/range test also compares the
recorded HEAD gitlink with the locally available `v<pin>` tag, without restoring
or invoking a tool. Historical v4.35.0 identity/procedure sections are labeled.

The isolated candidate additionally rejects split-segment sibling path construction
in executable `Path.Combine`, `os.path.join` and `Join-Path` calls; catches external
ancestor lookups in imports; recognizes nested generic Dapr calls and additional
shell/PowerShell existence forms. Regression cases cover those corrections plus
marker-file dependency guards, non-adjacent assertions, supported placeholders,
comments/string-only text, configured names and unrelated paths.

Verification commands and raw results:

- [Final candidate Release build](builds-build.txt): zero warnings/errors.
- [Affected candidate classes before the final shell-case control](builds-focused-before-shell-case-refinement.txt):
  411/411 passed, zero errors/failures/skips, 298.277 seconds. Final source adds a
  passing `test -n` string-test control and keeps file-test operators case-sensitive.
- [Platform Release build](platform-build.txt): zero warnings/errors.
- [Five Platform kit cases](platform-kit-tests.txt): all pass against actual HEAD
  `f97065cc31c2bdc1afd11c515298cc8f1a51380b` and the actual published pinned tool in both formats, including the
  offline tag/gitlink assertion.
- [Fresh published acceptance](released-acceptance.txt): NuGet.org-only restore
  into empty CLI-home/package caches; four human/JSON success/failure commands and
  two real-Parties commands preserve Git state and every file hash. Exact commands,
  version/source identity, package hash, outputs and inventories are in
  [released-package](released-package/released-commands.json). Reproduce with
  [accept-released.py.txt](accept-released.py.txt).
- [Pre-review candidate real-Parties check](real-module-check.txt): workspace and dependency
  rows pass; both formats preserve Git status and all 6,098 tracked hashes at Parties
  `63cd1279a82df47af3d16dd0ff9f002f3e4c86eb`. Expected overall exit `1` records
  incomplete enrollment. [Pre-review commands/hashes](pre-review-real-module/commands.json) and
  [reproduction](real-module-check.py.txt) bind this to the isolated binary.

Pre-review unfiltered serial verification passes **1,459 total / 1,456 passed**, zero
errors/failures/not-run cases, **three documented location skips**, in **786.275 seconds**.
[Raw output](full-builds-tests.txt) and [exit sentinel](full-builds-tests.exitcode)
record exit `0`. This covers the accepted pass-4 patch list and last shell-case control before the independent review corrections below. Three location-dependent integration cases require the
enclosing Platform checkout and are expected skips in this `/tmp` worktree:
`WorkspaceMsBuildTests.RootDeclaredEventStoreCandidatesExcludeArchivedEvidenceAsync`,
`WorkspaceMsBuildTests.RootDeclaredCommonsLibraryCandidateSelectsSourceAsync` and
`WorkspaceAppHostTests.PlatformPlanDelegatesToThePinnedModelAsync`.
They do not exercise the changed matcher or pass-4 matrix rows. A supplementary
symlink invocation still resolves the assembly's physical `/tmp` location and
records the same three skips in [enclosing-platform-context-tests.txt](enclosing-platform-context-tests.txt);
it is not passing coverage. No source or gate was weakened to remove the skips. The fresh published package
SHA-256 also matches the previously qualified signed package exactly. Both local
commit messages passed the owning repositories' pinned commitlint checks; exact
commands, versions and messages are in [commitlint-validation.json](commitlint-validation.json).
Exact command, from the isolated worktree:

```bash
dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj \
  -c Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=4.35.1 --no-restore
dotnet test/Hexalith.Builds.Module.Tests/bin/Release/net10.0/Hexalith.Builds.Module.Tests.dll \
  -parallel none -noLogo
```

[Source/artifact identities](source-identities.json), the
[pass-4 Builds delta](builds-pass4.patch) and compressed original-baseline
[Builds](builds-story-product.patch.gz)/[Platform](platform-story-product.patch.gz)
story product snapshots retain the review inputs.

The accepted published tool retains its documented asserted-probe false failure.
The local candidate is unpublished and needs a new owner decision before any
upstream integration/release. The final publication/pin task stays open. Human
SM-2, hosting retirement and other module adoption remain separate owner-led work.

Independent build review completed all three required layers. Its 16 findings are
classified individually in the story’s Review Triage Log. Five correction groups
remain local to the candidate: split direct-reference probe arguments, negated
shell tests, quoted dotnet overrides, named Dapr arguments after nested calls, and
structural failed-JSON assertions in Builds and Platform. Targeted regressions pass
157/157 Builds cases and 2/2 Platform cases; both Release builds have zero
warnings/errors. Final coordinator verification is complete on these source bytes; its output and
sentinel are under [review-fixes](review-fixes/). No new
publication, accepted pin or runtime dependency change is claimed.

Final reviewed local candidate: **750b1a0e739b73c48e31f3ee812d37ab7950d8c3** on
`fix/domain-enrollment-pass-4`; its working tree is clean and tested source bytes
are unchanged by the commit. Final unfiltered Builds verification passes
**1,486 total / 1,483 passed**, zero errors/failures/not-run cases, three documented
location skips, in **814.266 seconds**. See
[full output](review-fixes/full-builds-tests.txt),
[exit 0](review-fixes/full-builds-tests.exitcode) and
[final identities/hashes](review-fixes/source-identities.json). All five review
patch groups are fixed; none was newly deferred. The earlier 1,459-case result
is the pre-review checkpoint, superseded by this final run.

All [five final Platform kit cases](review-fixes/coordinator-platform-kit-tests.txt)
pass against the accepted published tool, including offline tag/gitlink identity
and structural failed-JSON assertions. Both final [real-Parties modes](review-fixes/real-module-check.txt)
preserve status and all 6,098 tracked hashes; workspace/dependency audits pass,
with expected overall failure for incomplete module enrollment. Current
[commands and hashes](real-module/commands.json) bind that rerun to the final
artifact hashes above. Fresh NuGet-only published acceptance remains valid.

The accepted identity stays 4.35.1 / `f8599224457dda99e7c0ae4870147dd51b464feb`.
New candidate publication and the final acceptance task remain stopped under
the retained owner rollback; the story remains **in-review**. No upstream branch,
release, tool pin, support floor or other module was changed by these corrections.

The saved Builds patch uses an exact-path Git attribute to preserve its raw bytes
and the blank-line context required by unified diff.
