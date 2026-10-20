# Story 1.11 evidence: run and debug McpCli from the Platform workspace

## Current status

Final, 2026-10-20 (review pass 5). [Release 4.34.0](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v4.34.0)
([release run 39396175115](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39396175115))
succeeded after human production approval. Platform pins tool `4.34.0`, and its
Builds gitlink is `5d03f1db9c38db34ecac7fe6048937478ffb82cc`, the `v4.34.0` tag commit.

- **AC1** passed with the released tool, run `0d8b26aad6e2cde1811cd47cfac54317`
  ([pinned-proof.json](pinned-proof.json), [pinned-proof.txt](pinned-proof.txt)):
  ready, the mapped run-scoped McpCli build, all four first-party PDBs, one
  function-breakpoint hit, offline `catalog_empty` exit 2, and `down` cleanup.
  The proof ran at Platform `9491501`, whose committed tool pin was still
  `4.33.1`; the `4.34.0` pin was an uncommitted working-tree edit at that time
  (`--version` reported `4.34.0+5d03f1d…`) and was committed in `a9ac664`.
- **Memories under the new pin** (review pass 5): `debug` on the Memories
  manifest with the pinned `4.34.0` reached ready in 93 s with every resource
  healthy and no McpCli build, and `down --run-id` removed the workspace
  ([memories-pinned-proof.txt](memories-pinned-proof.txt)). EventStore reached
  ready in the AC1 run itself.
- **Accepted range** (review pass 5): `hexalith-platform.json` raises the floor
  to `[4.34.0, 5.0.0-0)`, so `4.33.1`, which builds no McpCli, is no longer
  Platform-accepted.
- **Automatic checks:** the default Platform test verifies all four portable
  PDBs and usable sequence points ([review-pass4-tests.txt](review-pass4-tests.txt)).
  The Builds tests cover the McpCli build for both `run` and `debug`, and the
  deadline check after the build ([review-pass5-tests.txt](review-pass5-tests.txt)).

## Revisions

| Repository | Story revisions | At the pinned AC1 proof | Committed now (Platform `HEAD`) |
| --- | --- | --- | --- |
| Builds | `cf97810` `feat(tool): build a run-scoped McpCli for McpCli runs`, `24322f5` `fix(tool): name every McpCli HXW004 cause and its remedy`; released in `v4.34.0` (`5d03f1d`). Review pass 5 adds the test-only `b59e014` `test(tool): cover McpCli run builds and the post-build deadline` on local branch `test/1-11-review-pass5` (on `origin/main` `e40ed9c`, not pushed) | `5d03f1d` | `5d03f1d` (tag `v4.34.0`) |
| McpCli | `742f5b6` `feat(module): declare McpCli for Platform runs`, `d6176ac` `docs(module): name the released Platform McpCli tool`. Review pass 5 adds `97eee27` `docs(module): point to the Platform README for the current tool pin` on local branch `docs/1-11-review-pass5` (on `origin/main` `8ed3d91`, not pushed) | `b616db3` | `f1e8962` (a `/pushall` sync of nested gitlinks only; `git diff b616db3 f1e8962 -- src module` is empty) |
| Platform | `39be561`, `6664e2a`, `bb81fc9`, `fd7c7c1`, `a9ac664` (story paths; the `/pushall` syncs `c395e4a`, `a6d0d6d`, `a5fe856` move gitlinks) | `9491501` (tool pin `4.34.0` uncommitted) | review pass 5 changes are uncommitted |
| EventStore / Tenants / Memories | unchanged by this story | `c9f47dd` / `569e80e` / `c082f81` | `f6b545c` / `fc2d482` / `c082f81`; the Memories run used these |
| Sibling `../mcpcli` | not used | `4bb377a`, present, never built | — |

## History

The records below are earlier checkpoints, kept as written. They describe the
state at the time and are superseded by the sections above.

Current status, 2026-10-20: [release 4.34.0](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v4.34.0)
and [release run 39396175115](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39396175115)
succeeded after human production approval. Platform pins 4.34.0 and Builds
5d03f1db9c38db34ecac7fe6048937478ffb82cc, the release-tag commit. Tool restore
succeeds, and the pinned-tool debugger proof passed: ready, mapped run-scoped
McpCli build, all four runtime PDBs, one function-breakpoint hit, offline
`catalog_empty` exit 2, and complete `down` cleanup. See [pinned-proof.json](pinned-proof.json),
[the transcript](pinned-proof.txt), and [release-current.json](release-current.json).
The proof built McpCli b616db3092a9b1c5af196538a3de37a5c7f2d7ca; final gitlink
d6176acee1999965881e423b9c0e2e8a3725b5a8 adds only documentation/evidence, with
`src` and `module` unchanged. Both READMEs name the released version. The final default test also automatically verifies all four portable PDBs and usable source sequence points ([review-pass4-tests.txt](review-pass4-tests.txt)). Earlier
checkpoints below describe historical states.

Earlier checkpoint after production approval, 2026-10-20: the initial release
stopped before publication when Builds main advanced. Its replacement
39389191662 was cancelled when main advanced again, before another qualification
cycle. The latest source is 5d03f1db9c38db34ecac7fe6048937478ffb82cc, with
[exact-source CI queued](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39389674513).
Its file tree is identical to the previously green 6a38cb0f2e74977d7457fc59e6af79457ed8196a.
The maintainer has been asked to keep Builds main stable through the next
release. No package has been published; tool/tag pinning and pinned-tool AC1
remain open, with 4.33.1 still configured. See [release-retry.json](release-retry.json).
Earlier records below are historical.

Status, 2026-10-20 (review pass 2): the pre-release proof passed on 2026-10-19
with the locally packed tool
`4.34.0-local.1+d7c828a197d3bfb4762301a4d4c341a8c8701bfb`. That proof, the live
tests and the clone run predate the pass-1 review fixes, which were amended into
the Builds and McpCli commits (`d7c828a` became `cf97810`, `13568c0` became
`943b75c`). After pass 2, a live `debug` with the Builds CLI built from the
pass-2 fix (`review-pass2-debug.txt`) recorded the post-review `HXI010` text and
the README's function-breakpoint recipe. The orchestrator reruns AC1 with the
released and pinned tool after the `build-release.yml` production approval.
Until then, this directory holds no pinned-tool AC1 record.

Implementation revalidation on 2026-10-20 found the completed story changes
already committed. Platform `23a35074b10f999946ed5fdd621e4bb60af73de8` pins Builds `24322f56177b8fe86a78b2be7125f873c0a682bd` and McpCli `742f5b6ead66937f62025b73d77452e0f8126658`;
both submodule checkouts equal their locally recorded `origin/main` refs.
No remote query, push or release was performed during this revalidation.
The focused checks are recorded in `implementation-revalidation.txt`.


Release handoff, 2026-10-20: [Builds push CI](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39360743339)
passed at the reviewed commit. [Release run 39363818307](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39363818307)
passed its exact-source check and is waiting for the human's production approval.
No production approval was submitted by the agent. The tool/tag pin and pinned-tool
AC1 remain open. [release-handoff.json](release-handoff.json) records the canonical
revisions, CI results and gate state.

### Revisions during review passes 1-4

| Repository | Revision | State |
| --- | --- | --- |
| Platform | `39be561` `feat(platform): run and debug McpCli from the Platform workspace` commits this story's paths, with Builds `cf97810` and McpCli `943b75c` gitlinks; `c395e4a` (a concurrent `/pushall`) moved the McpCli gitlink to `742f5b6` | review pass 2 committed in `6664e2a`; revalidation at `23a3507` records committed gitlinks Builds `24322f5` and McpCli `742f5b6` |
| Builds | `cf978100f7668de135eb8a2adcd535e21b26471b` `feat(tool): build a run-scoped McpCli for McpCli runs`, on `b435f2e3b3892e03f69322d24de6003b0baf7a7d` (the pre-review proofs ran at `d7c828a197d3bfb4762301a4d4c341a8c8701bfb`), then `24322f56177b8fe86a78b2be7125f873c0a682bd` `fix(tool): name every McpCli HXW004 cause and its remedy` (review pass 2; amends `fc5cfc0`, at which `review-pass2-debug.txt` and `review-pass2-tests.txt` ran; the amend changed README wording only) | checkout and locally recorded `origin/main` both `24322f5` at revalidation; committed in Platform, release and tag pin still pending |
| McpCli | `742f5b6ead66937f62025b73d77452e0f8126658` `feat(module): declare McpCli for Platform runs`, on `a51900b` | pushed to `origin/main`. It is `943b75ce85cbedc83b73f3b90277dcd3d4fae83b` (on `447085c42ec606f642240c4cae0415e7c2f97467`) rebased by the concurrent `/pushall`; `git range-diff` shows an identical patch, so the records that name `943b75c` apply to it. The pre-review proofs ran at `13568c0bc56fc42909c620847915b993483c594d`; only the README link differs |
| EventStore / Tenants / Memories | `682512c6` / `63b7b3d8` / `79e4ab1e` (pass 2: `b16be635` / `6b62b6b3` / `72a97345`) | unchanged, equal to the committed gitlinks |
| Sibling `../mcpcli` | `a51900b` (pass 2: `742f5b6`) | present during the run, never used |

During the 2026-10-19 runs, Platform `HEAD` committed Builds `4aaeed8`, so each
run reported `HXW009` (source mode warns). Another process staged the Platform
Builds gitlink, first at `b435f2e` and then at `d7c828a`; the pass-1 amend
replaced it with `cf97810`, which `39be561` commits. In pass 2 the staged Builds
gitlink (`fc5cfc0`, then `24322f5`) differs from the `cf97810` pin, so
`HXW009` appears again.

## Files

| File | Proves |
| --- | --- |
| [memories-pinned-proof.txt](memories-pinned-proof.txt) | Review pass 5: Memories `debug` with the pinned `4.34.0` reaches ready (run `736f7c00e9d2b10cc9de1bce2c8abc6f`, 93 s, every resource healthy, no McpCli build). `down --manifest` without `--run-id` gives the documented `HXR024` (an earlier Memories run exists); `down --run-id` removes the workspace and the run's containers |
| [review-pass5-tests.txt](review-pass5-tests.txt) | Review pass 5, Builds `b59e014`: focused `McpCliRunBuildTests` 31/31, full suite 1118 run with 0 failed and 3 skipped, and a reverted mutation check showing that each of the two new cases catches its regression |
| [tools/pdbdocs.cs](tools/pdbdocs.cs), [tools/ncdb_launch.py](tools/ncdb_launch.py) | The helpers `pinned-proof.txt` runs, copied byte-for-byte from that session's scratchpad: the PDB source-document lister, and the netcoredbg CLI driver that sets the breakpoint, runs, and records each hit and backtrace |
| [release-retry.json](release-retry.json), [release-handoff.json](release-handoff.json) | Release checkpoints before publication: the stopped first release and its replacement, then the exact-source handoff awaiting human approval |
| [builds-commitlint-pass5.txt](builds-commitlint-pass5.txt), [mcpcli-commitlint-pass5.txt](mcpcli-commitlint-pass5.txt), [checkpoint-commitlint.txt](checkpoint-commitlint.txt), [release-retry-commitlint.txt](release-retry-commitlint.txt), [completion-commitlint.txt](completion-commitlint.txt), [mcpcli-pin-commitlint.txt](mcpcli-pin-commitlint.txt) | Later commit messages pass the owning repository's pinned commitlint |
| [review-pass4-tests.txt](review-pass4-tests.txt) | Default automatic declaration test passes after verifying real McpCli portable PDB source documents and usable sequence points for all four first-party assemblies; private run workspace removed in finally |
| [pinned-proof.json](pinned-proof.json), [pinned-proof.txt](pinned-proof.txt) | Released pinned-tool AC1: readiness, run-scoped Debug DLL and mapped project, all four PDB source origins, CatalogService.cs:11 breakpoint hit, offline exit 2, clean checkout and down cleanup |
| [g4-tool-package-inventory.json](g4-tool-package-inventory.json) | Published release inventory retained byte-for-byte; its SHA-256 is recorded in release-current.json |
| [implementation-revalidation-current-source.txt](implementation-revalidation-current-source.txt) | Fresh Builds McpCli matrix 29/29 and Platform real declaration/casing/environment selection 1/1 at Builds `5d03f1db9c38db34ecac7fe6048937478ffb82cc` and McpCli `b616db3092a9b1c5af196538a3de37a5c7f2d7ca`; records the corrected source-mode and Tooling-version build commands and transient concurrent-checkout refusal |
| [release-current.json](release-current.json) | Current-source successful push CI, successful replacement release, human production approval, exact tool/tag identity and completed pinned-tool proof |
| [prerelease-proof.txt](prerelease-proof.txt) | AC1 with the pre-release tool: the inputs, `debug` reaching ready, `HXI010` naming the run-scoped Debug DLL, run id `493239030fb3522402ccfed7d355fca0`, the DLL and PDB hashes, the full document lists of the two PDBs inspected (`Hexalith.McpCli.pdb`, `Hexalith.McpCli.Core.pdb`; the pinned rerun covers all four first-party PDBs), the netcoredbg hit at `CatalogService.cs:12` while `modules` runs offline (`catalog_empty`, exit 2), a clean checkout, and `down` removing the workspace and the DLL |
| [prerelease-tool.txt](prerelease-tool.txt) | The pack from a clean scratch clone of `d7c828a`, the packaged-loader gate, the package hashes and the scratch tool install |
| [validation.txt](validation.txt) | AC2 before the release: the McpCli declaration validates with `4.33.1` (the pin at that time) and the pre-release tool; adding an `mcp` interface fails with `HXP002` and `HXP023` (plus `HXP024`, the unknown server). `pinned-proof.txt` repeats both checks with the released `4.34.0` |
| [transitive-pinning-build.txt](transitive-pinning-build.txt) | After review: a real McpCli Debug build with the fixed arguments (`-p:CentralPackageTransitivePinningEnabled=true`) succeeds with 0 warnings, restores with transitive pinning, and leaves the checkout clean |
| [unit-tests.txt](unit-tests.txt) | Builds `Hexalith.Builds.Module.Tests`: baseline 1071/1071 at `b435f2e`, change 1090/1090 with the 19 new `McpCliRunBuildTests` cases (pre-review; the review fixes add three cases, run as a focused class: 22/22) |
| [post-review-verification.txt](post-review-verification.txt) | After the review patches (orchestrator), at Builds `cf97810` and McpCli `943b75c`: <ul><li>Builds suite 1093/1093.</li><li>Full Platform Composition suite: 203 run, 11 failed, 7 skipped. Every failure is a member of the known 1.10 set, and none is new.</li><li>The gated live McpCli case passes 1/1 with the patched engine. The containers are back to the four baseline ones and the checkout is clean.</li></ul> |
| [live-tests.txt](live-tests.txt) | Platform gated live cases with the source-built engine: McpCli (`["eventstore"]`, tool path in the plan and in `HXI010`) and EventStore, 2/2 |
| [clone-identity.txt](clone-identity.txt) | AC4, **non-Platform evidence**: in a disposable McpCli clone, the pinned `4.33.1` run recorded Platform identity `2ac8e73c09fe631c5620723b54784d2e28e8941f`, equal to the clone's `references/Hexalith.Platform` gitlink, and `IsPlatformRepository: false` |
| [mcpcli-commitlint.txt](mcpcli-commitlint.txt), [builds-commitlint.txt](builds-commitlint.txt) | Both local commit messages pass the owning repository's pinned commitlint |
| [review-pass2-debug.txt](review-pass2-debug.txt) | Review pass 2, Builds CLI built from `fc5cfc0`, not the pinned tool: `debug` reaches ready (run `88ca6df720ede12bdf7df994f22f79b3`). `HXI010` names the run-scoped DLL and `references/Hexalith.McpCli/src/Hexalith.McpCli/Hexalith.McpCli.csproj`. The README's `break Hexalith.McpCli.Core.Catalog.CatalogService.ListModules` is reported `--pending`, then binds, and is hit (`CatalogService.cs:11`, called from `CliRunner.cs:330`) while `modules` returns `catalog_empty` with exit 2. The checkout stays clean, and `down` removes the workspace and the DLL. `test` builds nothing: without `--profile` it fails with `HXC001`, with one with `HXC002` |
| [review-pass2-tests.txt](review-pass2-tests.txt) | Review pass 2, Builds: focused `McpCliRunBuildTests` 29/29 (7 new catalog-drift cases) and the full suite 1100/1100 |
| [builds-commitlint-pass2.txt](builds-commitlint-pass2.txt) | The Builds review-pass-2 commit message passes Builds' pinned commitlint |
| [implementation-revalidation.txt](implementation-revalidation.txt) | Focused implementation checks at the committed Builds `24322f5` and McpCli `742f5b6`, with the release/pin handoff still open |

## Acceptance criteria

- **AC1 (released pinned tool).** Passed with 4.34.0, run `0d8b26aad6e2cde1811cd47cfac54317`; see [pinned-proof.json](pinned-proof.json) and the transcript. The following pre-release record is retained as history.
- **AC1 (pre-release).** From the Platform root with `HEXALITH_DAPR_HOME` set,
  `debug` on the McpCli manifest reached ready in 67 s with `eventstore` as the
  only project. `HXI010` (field `mcpcli`) named
  `~/.local/share/hexalith-builds/g4-workspaces/493239030fb3522402ccfed7d355fca0/tools/mcpcli/bin/Hexalith.McpCli/debug/Hexalith.McpCli.dll`.
  The two PDBs inspected (`Hexalith.McpCli.pdb`, `Hexalith.McpCli.Core.pdb`)
  record only sources under `references/Hexalith.McpCli/src` and generated
  files under the run workspace's `obj`. netcoredbg launched it with
  `modules` and stopped at `CatalogService.ListModules()` line 12, called from
  `CliRunner.cs:330`. After a step, `access.Catalog` was `null`, and the
  command returned `catalog_empty` with exit code 2, as accepted by the spec.
  `git -C references/Hexalith.McpCli status --short --ignored` was empty
  before and after. `down --run-id` succeeded and removed the workspace and
  the DLL. Only the four baseline Dapr containers remained from this run.
- **AC2.** With the released `4.34.0`, `pinned-proof.txt` ("Pinned declaration
  validation and MCP rejection") validates the declaration and refuses an `mcp`
  interface (`HXP002`, `HXP023`). See also `validation.txt` (before the release)
  and the `McpCliShapedDeclarationValidatesAndRejectsAnMcpInterface` unit cases.
- **AC3 (sibling).** The sibling `../mcpcli` existed during the run and in the
  unit fixtures. The tool resolves McpCli only through the mapping entry behind
  `HexalithMcpCliRoot`. The pinned run's four first-party PDB document lists show the build came from
  `references/Hexalith.McpCli`, and the real-build unit test proves a sibling
  is neither built nor used as a fallback.
- **AC4.** See `clone-identity.txt`. The run is McpCli own-repository evidence
  and is never counted as Platform evidence.
- **AC5.** The Platform README ("Run and debug a technical module",
  "Launch McpCli under a debugger", "Own-repository AppHosts") and the McpCli
  README ("Run and debug from the Platform workspace") scope McpCli's
  own-repository runs to its own builds and tests.

## Environment

- The isolated Dapr home is `~/.hexalith/dapr/1.18.2` (CLI 1.18.0, runtime
  1.18.2), as in Story 1.10. The Aspire CLI is 13.6.1, the SDK 10.0.402 and the
  runtime .NET 10.0.13.
- A concurrent session repeatedly ran Works smoke tests that hold the fixed Dapr
  control-plane ports (Sentry `50001`, placement `51005`, scheduler `51006`).
  Each live run here first waited until those ports had been free for 45 s and
  no Works test or verifier process was running.
