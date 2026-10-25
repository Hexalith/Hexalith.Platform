# Custody lifecycle foundation — local source verification

The public Dapr adapters, authenticated wire integration and unapplied deployment bundle are ready for independent review. Local validation passed **20 component scenarios, 118 targeted Runtime tests, 5 Protection tests and 5 offline checks**, with zero failures, skips or unrun tests in the final runs. Three fresh builds completed with zero warnings/errors. A Release component publish and hash-verified offline image context also passed.

This is source/component evidence. The independent private authorizer, control issuer, closure observer, owning-host composition and accepted EventStore source/package integration remain explicit owning-source work. Actual resources, credentials, independent placement/enrollment, installed profiles and physical qualification remain absent or unqualified. No installed provider, irreversible-erasure, accepted target or dependency availability is established. Production DI, native serving/worker operations, owner Live/Full commands, cohort attempts and consuming Agents seams were not activated.

## Delivered behavior

`CustodyLifecycleClient` wraps the supplied original root, unwraps to a disposable owned buffer, obtains value-free duplicate/loss status, records independently observed holder completion, pins/unpins and recovers exact originals. The credential-bound Dapr authorizer receives public metadata and the exact complete wire hash; it receives no root/KEK bytes and has no local signer fallback. Every envelope must match the caller's fresh attempt/challenge, exact identity/body digest and independently installed enrollment. `BindingService` adds a P-256 response proof bound to the attempt/challenge and complete response hash. Invalid or missing proofs discard and clear key results.

`DaprAtomicDeletionManifestProvider` implements the existing manifest port using the complete signed original request and reservation. It exposes Consumed only after complete bounded-page assembly, exact byte/hash/signature/vector/restore-barrier checks and fresh independently authorized page calls. Loss, corrupt pages and unavailable authority remain Unknown. No lookup starts a worker/native effect.

`DaprCustodyKeyLifecycleProvider` delegates interaction-root Destroy to its retained complete reserved manifest. Before projecting a target-triple receipt it authenticates the full original registration, including key/lifecycle/store/contract versions. Substituted full identities cannot inherit another root's receipt. `DaprCustodyKeyLifecycleAuthority` uses the independently enrolled owner-state port and authenticated original lookup instead of a local witness/journal. Its legacy record-revision hook stays denied.

The deployment bundle contains a publish-output Dockerfile, exact-hash offline context preparer, scoped Dapr registration/no-retry configuration, protected socket/workload and suspended one-original worker templates, plus a closed-input offline preflight. The owning custody app, daprd and pluggable component share the qualified pod/socket boundary; a local output binding cannot be addressed as a remote service. The component native sidecar must reach its exact protected socket before daprd discovery. References and templates grant no authority or qualification.

## Final executed commands and retained results

Every build selected `-p:UseHexalithProjectReferences=true -p:HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore -m:1` explicitly, and used a fresh external artifact directory:

| Check | Artifact directory / retained result |
| --- | --- |
| Component test project build | `/tmp/custody-lifecycle-final-component-20261025a`; `verification/final-component-build.log`, then test-only strengthening `final-component-build-followup.log` |
| Component DLL, default complete scenario list | `verification/final-component-tests-followup.log`: 20 passed, 0 failed; preceding green run retained separately |
| Runtime test project build | `/tmp/custody-lifecycle-final-runtime-20261025a`; `verification/final-runtime-build.log` |
| Runtime DLL selected custody classes | `verification/final-runtime-tests.log`: Total 118, Errors/Failed/Skipped/Not Run 0 |
| Protection test project build | `/tmp/custody-lifecycle-final-protection-20261025a`; `verification/final-protection-build.log` |
| Protection DLL, complete suite | `verification/final-protection-tests.log`: Total 5, Errors/Failed/Skipped/Not Run 0 |
| Offline deployment/image-input regressions | `verification/final-offline-tests-followup.log`: 5 passed |
| Uninstalled-input preflight | `verification/reviewed-offline-preflight.log`: expected exit 1/Blocked, one entry per missing input |
| Release component publish | `/tmp/custody-lifecycle-final-publish-20261025a/output`; `verification/final-reference-publish.log` |
| Exact publish-output image context | `/tmp/custody-lifecycle-final-image-context-20261025a`; `verification/final-image-context-followup.log`: 41 files, OfflineContextPrepared, imageBuilt=false |
| Template YAML parse / scoped diff / negative checksum | `verification/final-static-checks.log` |

Runtime selection was exactly `CustodyBindingContractTests`, `CustodyBindingDaprInvokerTests`, `CustodyBindingOriginalTests`, `CustodyKeyLifecycleBindingTests`, `CustodyKeyLifecycleGuardAdmissionTests` and `CustodyLifecyclePublicContractTests`. Each selected class belongs to `Hexalith.Platform.Secrets.Runtime.Tests`; xUnit reported no unrun or skipped test. The component runner now refuses unknown requested scenario names, preventing an empty/mistyped filter from appearing as a pass.

The exact source/build commands follow the forms in the deployment README. Component, Runtime and Protection DLLs were run directly from the directories above. Offline commands were `python3 -m unittest discover -s eng/ext-secrets-1/custody-deployment -p 'test_*.py' -v`, `preflight.py inputs.uninstalled.json --output <new-ledger>` and `prepare-image.py` with the retained publish inventory and its exact SHA-256. The image-context input inventory SHA-256 is `7650fcbb0189630d6a1e310810ab8ebed5c83885f72c59f921937c74be57b6b8`.

`source-inventory.json` records source/artifact/evidence file hashes and observed source-graph revisions. `publish-inventory.json` covers every emitted publish file. The image context embeds that exact inventory. These identify local bytes; they do not authenticate an installation. All `.log` evidence must be explicitly retained when publishing this bundle because the repository ignores log files by default.

## Matrix audit

All component scenarios below are in the default list and passed in the final run.

| Frozen matrix behavior | Executed coverage |
| --- | --- |
| Actual root flow and complete holder admission/completion | `public-lifecycle-real-sdk-authorizer-key-cleanup-and-refusals`, `ephemeral-key-delivery-duplicate-and-holder-completion`; actual SDK → BindingService → semantic provider, eight holder roles, exact original root, local zeroing, public JSON serialization denial and missing-completion refusal |
| Lost response / duplicate delivery remains value-free, no release/root recreation | Public lifecycle loss/duplicate assertions, `lost-wrap-result-recovers-without-native-resend`, `registration-only-original-lookup-and-substitution-denial` |
| Hold/stale/unavailable/substituted identity denies before effect | Public lifecycle signed stale/bad-signature/refusal checks, full-identity Destroy substitutions, `hold-pin-release-and-complete-manifest-reservation`, `signed-pre-cycle-hold-reservation-denial`, `missing-qualification-and-enrollment-close-effects` |
| Shared complete multi-target manifest, survivor, no partial terminal claim | `shared-cut-independent-closure-and-exact-original-pages` now reserves the two-target manifest through the public provider, traverses real B1–B5 with a survivor, and verifies per-key delegation plus subset-manifest refusal; hold-on-second-target leaves the head/effect count unchanged |
| Exact signed original vector/page recovery after response loss | Same shared-cut scenario: public recovery of exact ordered receipts, corrupt and lost page refusal, successful later original lookup without a new cut, retained preparation acknowledgement-loss recovery; `maximum-manifest-transition-immutable-index-roundtrip` retains 1,000-target/corrupt-object coverage |
| Restore/missing genesis never supplies authority | `single-head-cas-and-app-restore-exclusions`, `independent-owner-genesis-and-consecutive-transitions`, `public-owner-adapter-real-sdk-signed-genesis-and-transition-wire`, `eventstore-published-owner-bridge-exact-framing-and-recovery` |
| Missing installation/closure names inputs and supplies no availability | Five offline regressions reject missing/empty/extra/wrong-type sections, unpinned images, wrong hashes, missing G1 evidence and replacement output; complete fixture presence remains InputsPresentUnverified, installed/physical/available/runnable false |

Doubles establish code behavior only. Independent physical deletion, cloud durability, hidden-buffer closure and installed transport policy are not inferred from these assertions.

## Remaining source, installation and physical-proof dispositions

The deduplicated [installation ledger](installation-input-ledger-reviewed.json), [explicit reference-profile/copy/holder member list](reference-profile-inputs.json), [member-list explanation](REFERENCE-PROFILE-INPUTS.md) and [separate source dispositions](source-dispositions.json) name every gap's owner/action. The EventStore owning-source integration is assigned to the EventStore Maintainer. The private authorizer/control issuer/observer have missing owning-service implementations as well as missing installations; an opaque reference cannot close either category. The receiving host composition remains source work and no production DI is installed.

Local Protection builds used current sibling source for its owner-state port. The installed package graph predates that seam; package-only and attempted locked publish failures are retained as intermediate evidence. The Dockerfile therefore takes the exact reviewed publish-output bytes rather than claiming a broken package-only build route. No sibling source was edited. Generated source-reference restore changes to the two custody package-lock files were restored to their current committed bytes. Concurrent unrelated schema/docs/submodule work and the separate EventStore owner-bridge fixture compatibility edit were preserved and are outside this mutation scope.

The OpenBao negative control remains SHA-256 `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`, outcome `ObservedRevival`, `g1Qualified:false`. Historical semantic-provider evidence remains unchanged. No scheduled deletion, tombstone, wrapper, missing key or local fixture upgrades G1. The AWS internals remain an uninstalled infrastructure reference, with no mandatory backend adoption. Open export disposition, history policy, dependency acceptance, integration dates and timing remain unchanged.

Intermediate failures/logs are retained separately and are not final passes: early development compile failures, the pre-rebuild filtered run that did not include the new scenario, source/package graph failures and the initial image-context rejection of generated documentation/config assets. The corrected final logs above supersede them without deleting history. Required independent workflow review is owned by the parent build workflow and is recorded separately.
