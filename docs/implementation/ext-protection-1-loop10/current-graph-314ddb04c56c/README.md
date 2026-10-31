# EXT-PROTECTION-1 loop-10 current-graph regression resume

Run `314ddb04c56c`, 2026-10-31. This is the run authorized by the spec section **Authorized loop-10 current-graph regression resume — 2026-10-31**. Status stays `in-progress`. Review counter 10, both original baselines (`231b0f2b…` / `a6224167…`) and the frozen intent are unchanged. The frozen-intent hash `96c592bd…` matches `../parent-audit.json`, and the Agents register hash is unchanged (`e29b7fc4…`). No accepted target, profile, date or availability is proposed. No Live/OQ, Agents consumer, cluster installation, production OpenBao, native OpenBao, Git mutation or remote operation took place. This run does not start a review or a correction loop.

## 1. Audit result: every mandatory correction is present, and no source was changed

`audit.py` reads the current working trees and checks method-scoped evidence: 20 corrections and 128 required source/test fragments. It exits nonzero if any fragment is missing. A negative control on a mutated copy of `OpenBaoErasureRootStore.cs` (DecryptAsync finally-zero removed, trim values made numeric) correctly reported `R10-BH-17` and source gap 2 as missing. The real trees pass with 20/20 present (`audit.json`, `audit.log.txt`).

| Correction | Production location (current bytes) | Regression coverage run here |
|---|---|---|
| R10-BH-1/R10-EC-7 resume, named stop step, zero-call default | `deploy/erasure-root/bootstrap.py` (`BootstrapStop`, per-step names, match-or-refuse) | `test_apply_installs_…` (lost ack after mount/key/policy/head, disjoint rewrites, mismatch refusals), `test_actual_cli_defaults_…` |
| R10-BH-2 pinned CA before the bearer | `bootstrap.py` `pinned_opener` / `ca-pin` step; store `ValidatePinnedCa` | `test_apply_installs_…` (unpinned → `ca-pin`, zero requests; wrong CA → zero tokens), `MissingCaPinRefusesBeforeASocket`, `MatchingPrivateCaReceivesTheTokenAndADifferentCaDoesNot` |
| R10-BH-3 + source gap 2: quoted `"1"`–`"64"` allow-lists | `bootstrap.py` `render_policy` | `test_rendered_policy_…` (exact list `["1"…"64"]`, no `[]`, no `"65"`) |
| Source gap 2: string trim values | `OpenBaoErasureRootStore.TrimAsync` (invariant strings; trim body numeric) | `TrimMinimumVersionsUseExactStringValuesAcceptedByTheBoundedNativePolicy` (epochs 2 and 64) |
| Source gap 1: KV-v2 new-mount defaults | `bootstrap.py` `ensure_config` (only when this run created the mount; mandatory readback) | `test_apply_installs_…` (GET/POST/GET, pre-existing defaults and conflicts refuse, fresh conflict and readback mismatch refuse) |
| R10-BH-5 `defaultMode: 0400`; 1Gi memory + zero swap | `statefulset.yaml`, `kubelet-noswap.fragment.yaml` | `test_prepared_startup_requires_exact_memory_and_zero_swap_before_openbao` (9 vectors) |
| R10-BH-7 readiness requires 2.6.2/initialized/unsealed | `statefulset.yaml` readiness shell | `test_actual_readiness_shell_refuses_wrong_native_health` (6 vectors) |
| R10-BH-8 listener 67108864 / 134217728 | `statefulset.yaml` | `test_rendered_policy_…` |
| R10-BH-13/R10-EC-3 null and non-JSON recovery → unavailable | `InteractionOccurrenceRegistryActor.ReadAsync` | `NullRecoveredSnapshotReturnsUnavailable`, `InvalidRecoveredSnapshotJsonReturnsUnavailable` |
| R10-BH-17 `DecryptAsync` body zeroed in `finally` | `OpenBaoErasureRootStore.DecryptAsync` | `DecryptRequestBodyIsZeroedAfterSuccessAndFailure` (success, 403, malformed JSON) |
| R10-VG-1 … VG-6 | — | `NewerCurrentOriginalRestoresExactRegistryState`, `InvalidRecoveredSnapshotJsonReturnsUnavailable`, `SealedSkeletonIsDetachedAndOversizeSkeletonIsRejected`, `CurrentOriginalReadForwardsOnlyToExactSingleEnrolledOwner`, `UnissuedProofAcceptsMatchingGuardAndRejectsMismatchedGuard`, `RejectedUnissuedCandidateRetainsIssuedOrdinal…HealthyProgress` |
| R11-VG-1 older stored snapshot replaced | — | `NewerCurrentOriginalReplacesOlderStoredRegistry` |
| R11-VG-2 journal rejects the unissued proof | — | `UnissuedProofAcceptsMatchingGuardAndRejectsMismatchedGuard` (`acceptBlockedProof = false` → false) |
| R11-VG-3 foreign-tenant owner reads | — | `AdmittedOriginalReadForwardsOnlyToExactSingleEnrolledOwner` / `CurrentOriginalReadForwardsOnlyToExactSingleEnrolledOwner` (`foreign-tenant`) |
| R11-BH-9 `TrimAsync`/`ReadKeyAsync` response buffers zeroed | `OpenBaoErasureRootStore` `finally` blocks | Source inspection only. The buffers are internal `SendAsync` copies, so a test cannot observe them without a production seam, and this run is not authorized to add one. |

Byte-level regression check: all 28 task files selected by the previous resume match the latest loop-10 graphs. Six match `source-gaps-7b5a4e938648/implementation-after/` and 22 match `ext-protection-loop10-resume-27fe4f343c1a/selected-source/`. Nothing was missing or regressed, so no production or test source was changed and no additional class set was required. The exact tested bytes are in `selected-source/` and `selected-source.json`.

## 2. Build and runtime

Both builds are serialized Debug project-reference builds using `-nodeReuse:false -m:1 -p:UseHexalithProjectReferences=true -p:GeneratePackageOnBuild=false`, `HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore`, physical `HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons` (also set as the process variable), and `HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src`. `--no-incremental` was added so that every project output is actually rebuilt and its timestamp can prove freshness. An up-to-date incremental build would otherwise keep old timestamps.

| Build | Result | Freshness (`build-freshness.json`) |
|---|---|---|
| `tests/Hexalith.Platform.Protection.Tests` | exit 0, 0 warnings, 0 errors, 12.46 s | 11/11 project outputs written after build start; test-output copies match |
| `tests/Hexalith.EventStore.Server.Tests` | exit 0, 0 warnings, 0 errors, 24.68 s | 18/18 outputs fresh; the McpCli analyzer is correctly not copy-local |

The predicted and actual build closures match (23 projects, `build-closure.json`). Each complete output directory was copied before any execution to the external runtime `/tmp/claude-1000/-home-administrator-projects-hexalith-platform/b949fb33-be78-47f5-84d2-3974588e5fdf/scratchpad/ext-protection-loop10-current-graph-314ddb04c56c/runtime/{platform,eventstore}`. The copies were inventoried with SHA-256 and verified against their sources: Platform has 257 files (manifest `ae941268…`) and EventStore has 692 files (manifest `3ea48d8e…`). Both were rehashed unchanged after execution (`post-run-integrity.json`).

## 3. Fresh results (`commands.json`: argv, cwd, recorded environment, UTC interval, exit code, log SHA-256)

| Check | Result |
|---|---|
| Bootstrap and prepared shell tests (`python3 -m unittest discover -s eng/ext-protection-1 -p test_bootstrap.py -v`) | 12/12 OK, exit 0 |
| Native-harness offline unit tests (`test_probe_erc_native.py`, Docker mocked; additional) | 7/7 OK, exit 0 |
| Platform DLL, direct, `-parallel none`, classes OpenBaoErasureRootStore / ErasureRootCustody / CustodyOccurrenceProvider / ErasureRootUnissuedReplacement / ErasureRootLoop8Admission / ErasureRootReceiving | 76 total, 76 passed, 0 failed/errors/skipped |
| EventStore DLL, direct, classes InteractionOccurrenceRegistryActor / PublishedTrustReplacementAuthority / PublishedTrustOwnerStateAuthority / PublishedTrustDeletionConsumptionAuthority / GovernanceScopeGuard | 268 total, 268 passed, 0 failed/errors/skipped |

The Platform count is 76 rather than the resume's 74 because the source-gap continuation added the two `TrimMinimumVersions…` theory rows. Notices left in the raw output are the existing redirect-test `HTTPError` `ResourceWarning`, and xUnit's deprecation notices for `-parallel` and `-xml`. No failure occurred, and none was relabelled. Bootstrap native integration and the five-case native suite were **not** rerun. Their passes belong to the `source-gaps-7b5a4e938648` frozen graph and are not attributed to this graph.

## 4. Concurrent unrelated changes (recorded only)

The observed HEADs are Platform `82c9a31a0565c6dfd47750f5c67c087b487e6300` and EventStore `61e6fa853d81cf822d67d6abfd6a130bdbb14ce2`. They were the same before and after the run, and they are observations, not targets. No source in the build closure changed during the run: 3,694 files were rehashed with 0 changed and 0 added. Since the source-gaps native freeze, 39 frozen files have changed through concurrent work (`source-delta-since-native-freeze.json`), and none of them is a loop-10 task file. Within this build graph these are:

- Custody `DeletionCapabilitySigningActor` null guards and the Platform `Directory.Build.props` Tenants/Memories sibling roots (commit `3e303eee`).
- EventStore routing/catalog changes in Server `ServiceCollectionExtensions`/`QueryRouter`/`RunScopedRouteCatalogGateway`, Client, Contracts and ServiceDefaults (for example commit `5e3c2f0e6`).

The rest are Composition/CatalogBuilder, Admin/Aspire and Builds tooling. Uncommitted EventStore Sharding test work lies outside both graphs. Because of these upstream assembly changes, 22 of the 257 Platform runtime files differ from the native freeze, while 235 are identical (`runtime-comparison.json`). No whole-binary equality is claimed. The selected classes do not exercise the concurrent signing-readiness guard.

All 284 pre-existing files under `ext-protection-1-loop10/` are byte-identical before and after packaging, and the tracked evidence matches HEAD (`evidence-preservation.json`). Raw command lines from other sessions are summarized, not bundled: 16 build processes were observed and none used the shared Platform or EventStore checkout.

## 5. Remaining source gaps and installed-qualification prerequisites

The audited corrections have no remaining observed source gap. The following items stay open:

- R7-BH-7 (generic `subprocess.run(capture_output=True)` with no stderr bound) remains deferred and unchanged.
- The Review 11 maybe-false rows R11-BH-1/2/3/5/6/7/8, R11-EC-1/3/6/7 remain deferred and unverified.
- R11-BH-9 is implemented without a direct regression test (see the table in section 1).

Independent prerequisites that are still missing are unchanged from `../source-gaps-7b5a4e938648/README.md`:

- A reviewed `ext-protection-1-profile/v3` `accepted-profile.json`. It must bind both accepted revisions, the unchanged 41-case plan, the engine binary, the date and the profile digest.
- A complete `ext-protection-1-role-installation/v2` closure for each role, with the trusted .NET 10 host/apphost template, independently operated identities and private P-256 signers, endpoints, and distinct bearers in inert `env:EXT_PROTECTION_ROLE_[A-Z][A-Z0-9_]{0,63}` references.
- The same dedicated CA bytes in Secret `erasure-root-tls` `ca.crt`, in `ErasureRootInstallation.TrustedCaCertificatePem` and in bootstrap `--ca-certificate`, plus the matching server certificate/key and HTTPS identity.
- The canonical tenant/native-key map and independently selected occurrence-registry, deletion-consumption and lifecycle genesis scopes/digests, together with current per-tenant private grants and usable capability trust.
- An independently qualified `erasure-root-no-backup` storage class, plus daily, off-node, storage, node, Velero and operator copy exclusions with independent all-copy, restore and retirement authorities and evidence. This also requires the actual A1 store, enrollment, volume and native bindings.
- The loop-7 signing-readiness backend (`query-signing-availability`, `DeletionSigningAvailabilityOperationHandler` with the `deletion-signing-owner` role, method `signing-readiness-permission`, scope `<tenant>|deletion-signing-availability`, and the matching `QualificationBindings` key).
- Enrolled source, control and fault cells; a signed observer read-only permission with physical backing proof; and exact producer/copy bindings.
- For legacy 4MiB source chunks, the Dapr sidecar 8MiB response allowance together with the SDK 8MiB receive setting.
- Linux cgroup v2 with a compatible CRI/container cgroup namespace and kubelet `memorySwap.swapBehavior: NoSwap`, with startup verifying `memory.max=1073741824` and `memory.swap.max=0`.

The installed 41-case qualification and the physical-copy qualification remain unestablished, and production destruction remains independently gated.

## Files

`audit.py`, `run.py` and `package.py` are the exact scripts. Raw logs are `*.log.txt` (byte-identical to the external logs, with hashes in `commands.json`). xUnit XML is in `platform-tests.xml` and `eventstore-tests.xml`. The remaining files are `verification.json`, `audit.json`, `build-freshness.json`, `build-closure.json`, the two `*-runtime.json` inventories, `source-before.json` (the 3,694-file closure inventory taken before the builds), `post-run-integrity.json`, `git-before.json`/`git-after.json`, `runtime-comparison.json`, `source-delta-since-native-freeze.json`, `evidence-preservation.json`, `selected-source/` and `artifact-inventory.json`. The external `source-after.json` is bound by hash in `verification.json`.
