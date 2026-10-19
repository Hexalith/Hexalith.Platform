# Story 1.11 evidence: run and debug McpCli from the Platform workspace

Status, 2026-10-19: the pre-release proof passed with the locally packed tool
`4.34.0-local.1+d7c828a197d3bfb4762301a4d4c341a8c8701bfb`. That proof, the live
tests and the clone run predate the review fixes, which were amended into the
Builds and McpCli commits (`d7c828a` became `cf97810`, `13568c0` became
`943b75c`). The orchestrator
reruns AC1 with the released and pinned tool after review, push and the
`build-release.yml` production approval. Until then, this directory holds no
pinned-tool AC1 record.

## Revisions

| Repository | Revision | State |
| --- | --- | --- |
| Platform | `f0e33167fcdb5f539da159c54f7505b72a65c9b2` plus this story's working tree | uncommitted; McpCli gitlink staged at `943b75c` |
| Builds | `cf978100f7668de135eb8a2adcd535e21b26471b` `feat(tool): build a run-scoped McpCli for McpCli runs`, on `origin/main` `b435f2e3b3892e03f69322d24de6003b0baf7a7d` (the pre-review proofs ran at `d7c828a197d3bfb4762301a4d4c341a8c8701bfb`) | local commit, not pushed |
| McpCli | `943b75ce85cbedc83b73f3b90277dcd3d4fae83b` `feat(module): declare McpCli for Platform runs`, on `447085c42ec606f642240c4cae0415e7c2f97467` (the pre-review proofs ran at `13568c0bc56fc42909c620847915b993483c594d`; only the README link differs) | local commit, not pushed |
| EventStore / Tenants / Memories | `682512c6` / `63b7b3d8` / `79e4ab1e` | unchanged, equal to the committed gitlinks |
| Sibling `../mcpcli` | `a51900b` | present during the run, never used |

Platform `HEAD` still commits Builds `4aaeed8`, so every run reports `HXW009`
(source mode warns). During this work another process staged the Platform
Builds gitlink, first at `b435f2e` and then at `d7c828a`, which the review
amend has since replaced with `cf97810`. This story did not stage it; see the
spec's Implementation Notes.

## Files

| File | Proves |
| --- | --- |
| [prerelease-proof.txt](prerelease-proof.txt) | AC1 with the pre-release tool: the inputs, `debug` reaching ready, `HXI010` naming the run-scoped Debug DLL, run id `493239030fb3522402ccfed7d355fca0`, the DLL and PDB hashes, the full document lists of the two PDBs inspected (`Hexalith.McpCli.pdb`, `Hexalith.McpCli.Core.pdb`; the pinned rerun covers all four first-party PDBs), the netcoredbg hit at `CatalogService.cs:12` while `modules` runs offline (`catalog_empty`, exit 2), a clean checkout, and `down` removing the workspace and the DLL |
| [prerelease-tool.txt](prerelease-tool.txt) | The pack from a clean scratch clone of `d7c828a`, the packaged-loader gate, the package hashes and the scratch tool install |
| [validation.txt](validation.txt) | AC2: the McpCli declaration validates with the pinned `4.33.1` and the pre-release tool; adding an `mcp` interface fails with `HXP002` and `HXP023` (plus `HXP024`, the unknown server) |
| [transitive-pinning-build.txt](transitive-pinning-build.txt) | After review: a real McpCli Debug build with the fixed arguments (`-p:CentralPackageTransitivePinningEnabled=true`) succeeds with 0 warnings, restores with transitive pinning, and leaves the checkout clean |
| [unit-tests.txt](unit-tests.txt) | Builds `Hexalith.Builds.Module.Tests`: baseline 1071/1071 at `b435f2e`, change 1090/1090 with the 19 new `McpCliRunBuildTests` cases (pre-review; the review fixes add three cases, run as a focused class: 22/22) |
| [post-review-verification.txt](post-review-verification.txt) | After the review patches (orchestrator), at Builds `cf97810` and McpCli `943b75c`: <ul><li>Builds suite 1093/1093.</li><li>Full Platform Composition suite: 203 run, 11 failed, 7 skipped. Every failure is a member of the known 1.10 set, and none is new.</li><li>The gated live McpCli case passes 1/1 with the patched engine. The containers are back to the four baseline ones and the checkout is clean.</li></ul> |
| [live-tests.txt](live-tests.txt) | Platform gated live cases with the source-built engine: McpCli (`["eventstore"]`, tool path in the plan and in `HXI010`) and EventStore, 2/2 |
| [clone-identity.txt](clone-identity.txt) | AC4, **non-Platform evidence**: in a disposable McpCli clone, the pinned `4.33.1` run recorded Platform identity `2ac8e73c09fe631c5620723b54784d2e28e8941f`, equal to the clone's `references/Hexalith.Platform` gitlink, and `IsPlatformRepository: false` |
| [mcpcli-commitlint.txt](mcpcli-commitlint.txt), [builds-commitlint.txt](builds-commitlint.txt) | Both local commit messages pass the owning repository's pinned commitlint |

## Acceptance criteria

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
- **AC2.** See `validation.txt` and the `McpCliShapedDeclarationValidatesAndRejectsAnMcpInterface`
  unit cases.
- **AC3 (sibling).** The sibling `../mcpcli` existed during the run and in the
  unit fixtures. The tool resolves McpCli only through the mapping entry behind
  `HexalithMcpCliRoot`. The two inspected PDBs' document lists show the build came from
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
