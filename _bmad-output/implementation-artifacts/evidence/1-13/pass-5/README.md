# Story 1.13 pass-5 review patches

Pass 5 reviewed Platform `3d51fed..c65d87a` story paths and Builds
`5d03f1d..359fee2` story paths ([review input](review-input.diff.gz)). The owner chose
decision **P5-D1B**: accept the `/pushall` push of `359fee2` to Builds `main` and release
it. All eight patches are done: seven code and documentation patches, then the release
and pin after the owner's "I approve" ([release](#release-and-pin--2026-10-22)).

## Builds candidate

Local commit `154010b7640ba1da23e918f26bbad9917e4767ea` on branch
`fix/domain-enrollment-pass-5` was built on `359fee2` ([patch](builds-pass5.patch)).
It was rebased unchanged onto `b693d4a` as `558122e` and merged through PR #129 as
squash commit `abcb1f3`. It makes these changes:

- `HasDotnetPropertyOverride` now catches quoted `-property:` and `/property:` switches,
  and reserved properties inside a `;` list (`"-p:A=1;UseNuGetDeps=false"`).
- The split-segment sibling check now covers `Path.Join`, joined `"../Hexalith.*"` and
  `"../references/Hexalith.*"` segments, and `"../.."` segments.
- New regression rows cover padded and multi-line build paths, `@$` literals, `==`
  comparisons, the `then`/`do`/`exec` prefixes and the `/p:` and `/property:` switches.
  Controls keep in-module and non-reserved forms passing.
- The README now documents `validate --adoption`.

The commit message passed the pinned commitlint `21.2.4`
([record](commitlint-validation.json)). Source hashes are in
[source-sha256.txt](source-sha256.txt).

## Verification

- [Release build](builds-build.txt) and [CLI build](builds-cli-build.txt): zero
  warnings and zero errors.
- [Focused classes](builds-focused-tests.txt): `PlatformAdoptionValidationTests` and
  `PlatformManifestCommandTests` passed 468/468 with no skips.
- [Mutation check](mutation-check.txt): five targeted mutations made exactly the 12 new
  rows fail and nothing else. The mutations were removing `Trim()`, narrowing the `@$`
  literal, dropping `(?!=)`, dropping the `then|do|exec` prefixes and dropping `/p:` and
  `/property:`. The committed source was restored and rebuilt afterward.
- [Full serial suite](full-builds-tests.txt.gz) at the [recorded commit](full-builds-tests.commit):
  **1,515 total, 1,512 passed**, zero errors, failures or not-run cases, and three
  documented location skips. It took 693.262 s, with [exit 0](full-builds-tests.exitcode).
  The skips are `WorkspaceAppHostTests.PlatformPlanDelegatesToThePinnedModelAsync`,
  `WorkspaceMsBuildTests.RootDeclaredEventStoreCandidatesExcludeArchivedEvidenceAsync`
  and `WorkspaceMsBuildTests.RootDeclaredCommonsLibraryCandidateSelectsSourceAsync`. All
  three need the enclosing Platform checkout.
- [Real-module comparison](real-module-compare.json) ([script](real-module-compare.py.txt)):
  read-only JSON `validate --adoption` runs on nine Platform-referenced checkouts (Parties,
  Tenants, Folders, Projects, EventStore, Memories, Conversations, FrontComposer, McpCli).
  Rows from the `359fee2` binary and from the candidate are identical, and every Git
  status is unchanged. Parties still passes `workspace` and `dependency-selectors`.
  Correction: the script's informational `candidateFailing` list ends in `[:0]` and is
  always empty. The row comparison (`onlyBaseline`/`onlyCandidate`) does not use it.
  A [recheck](real-module-compare-parties-recheck.json) with released `4.35.2` at the
  compared Parties commit `61921f7c` confirms both rows pass.

The Platform doc patches change only `docs/domain-module-enrollment.md` §4 and the
evidence README. The kit tests read neither, so no Platform rebuild was needed at that
point.

## Release and pin — 2026-10-22

- Builds required checks passed on PR #129, which squash-merged as `abcb1f3`. The
  released `main` commit `4248b8c` also contains other sessions' EventStore `3.126.1`,
  Tenants `5.10.0` and fixture commits. [CI 39736790198](release/ci-result.json) passed
  on that exact commit.
- The owner approved the production gate. [Release run 39741376261](release/release-result.json)
  published `4.35.2`, and tag `v4.35.2` resolves to `4248b8c` ([tag](release/tag-target.json)).
- Platform pins `4.35.2` with range `[4.35.2, 5.0.0-0)`. The Builds gitlink is the tag
  commit. `README.md` and guide §2/§4 describe the released behavior.
- [Acceptance](release/accepted-identities.json) passed: four fresh NuGet-only
  commands, four read-only real-Parties commands, the Platform build and 5/5 kit
  cases. The current Parties gitlink is correctly reported for its new sibling catalog
  fallback (see [current state](../README.md#current-state--2026-10-22)).
