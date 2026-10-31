# EXT-PROTECTION-1 loop 10 resume handback

This is the 2026-10-31 resume of the authorized loop-10 correction cycle. Status stays `in-progress`; counter 10, original baselines and frozen intent are unchanged. No target, profile, date or availability is accepted. No Git mutation, Live/OQ/Agents execution, cluster operation or production OpenBao call occurred. The native five-case suite and installed 41-case command were not run. Existing Review 11 changes were preserved; this resume does not start another review or correction loop.

The contradictory initial handback and verification bytes are retained unchanged as [initial-handback.md](initial-handback.md) and [initial-verification.json](initial-verification.json). They are superseded by this record and do not describe fresh execution.

## Corrections and preserved loop-10 behavior

- R10-BH-1: bootstrap now reads the OpenBao mount inventory. The pinned 2.6.2 [mount handlers](https://github.com/openbao/openbao/blob/v2.6.2/vault/logical_system.go#L866) show why an absent single-mount read must not be assumed to return 404. The HTTPS fixture models that protocol. Fresh tests lose acknowledgements after mount/key/policy/head commits and resume without rewriting committed objects. Exact matches continue; conflicts stop before changing the object, with a named step. Default no-apply makes zero calls.
- R10-BH-2: the existing explicit `--ca-certificate` pin is preserved. Fresh local HTTPS checks prove matching-CA apply, missing-pin refusal without a request and wrong-CA refusal before any bearer is received. Production store CA tests also pass.
- R10-BH-3: both per-tenant key-config minimum-version allow-lists remain exactly 1 through 64. Empty lists and version 1 alone are not the shipped setting.
- R10-BH-5: TLS Secret `defaultMode: 0400` stays. The new prepared kubelet fragment selects `NoSwap`; startup refuses before OpenBao unless fixed cgroup v2 files report `memory.max=1073741824` and `memory.swap.max=0`. The memory request remains 256Mi and limit 1Gi. Kubernetes has no Docker-style combined memory-and-swap Pod field; equal requests/limits are not used as proof. Nine shell vectors cover exact, wrong, unlimited and missing limits. Path substitution exists only in the test copy.
- R10-BH-7: existing pinned-CA readiness requires version 2.6.2, initialized true and sealed false. Six executions of the actual shell cover healthy, wrong version, uninitialized, sealed, missing field and failed native command.
- R10-BH-8: listener bounds remain 67108864 request bytes and 134217728 JSON-memory bytes. Their 192MiB sum fits under 1Gi. These are not the lower contradictory values in the initial handback.
- R10-BH-13/R10-EC-3: recovered null/non-JSON snapshots and argument-null refusal remain unavailable. The complete registry class passes, including newer current-original restore, exact older-primary replacement, detached skeleton bytes and oversize rejection.
- R10-BH-17: existing `DecryptAsync` finally-zero stays. Three new tests inspect the actual HTTP body backing buffer after success, native 403 and malformed response JSON. The body is nonzero during send and zero afterward; the healthy returned root stays exact. The test handler can omit its usual string buffering to expose that original stream.
- R10-VG-1 through VG-6: all filed registry/current-original/published-trust/unissued/healthy-successor tests remain and pass. The real authority accepts the matching unissued proof and rejects moved guard revision or changed digest; the healthy issue clears `RejectedUnissuedReplacement`.

## Fresh verification

Both serialized Debug project-reference builds used `-nodeReuse:false`, `-m:1`, `UseHexalithProjectReferences=true`, `GeneratePackageOnBuild=false`, `HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore`, physical `HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons`, and `HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src`. Process `HexalithCommonsRoot` used the same physical path. Both builds report zero warnings and errors.

Complete output directories were copied to fresh external runtime `/tmp/ext-protection-loop10-resume-27fe4f343c1a/runtime/` and SHA-256 inventoried before execution. Tests ran directly with `dotnet <test.dll> -class ...`, serialized, without project-level `dotnet test`.

| Fresh check | Result |
|---|---|
| Bootstrap and prepared shell tests | 12 passed |
| Platform DLL: transport, custody, disposed holder, unissued replacement, loop-8 admission and receiving | 74 passed, zero errors/failures/skips |
| EventStore DLL: registry, published-trust replacement/current-owner/deletion authority and governance guard | 268 passed, zero errors/failures/skips |

The bootstrap output contains the existing redirect-test HTTPError cleanup `ResourceWarning`. xUnit reports newer spellings for `-parallel` and `-xml`. These notices remain in raw output, separately from the clean builds. No failed execution was hidden or relabelled.

[Fresh evidence](ext-protection-loop10-resume-27fe4f343c1a/commands.json) and [verification.json](verification.json) retain exact argv/cwd/exit codes, raw logs, XML, selected source bytes/hashes and complete runtime inventories. Historical matrix/native results belong to their original graphs; focused execution does not repeat or qualify them.

The [independent audit](parent-audit.json) verifies the bundled evidence, 28 selected source snapshots, both complete tested runtimes, unchanged frozen intent/register/baselines/counter and verbatim initial handback. Concurrent repository HEAD movements are observations only; they are not accepted targets.

## Remaining source gaps and installed prerequisites

Confirmed existing integration-fixture gaps remain outside this focused resume: `OpenBaoBootstrapIntegrationTests` omits `--ca-certificate`, extracts no adjacent `custody-policy.hcl`, and issues its runtime token from the now deny-only shared policy. `OpenBaoNativeCustodyFixture` also uses that deny-only fragment for runtime tokens. Reconcile those fixtures with per-tenant rendered policies before claiming a fresh native bootstrap or five-case pass. They were not run here; historical passes are not attributed to this graph.

The pre-existing R7-BH-7 generic subprocess defer also remains: `subprocess.run(capture_output=True)` can accumulate both pipes before the verifier checks stdout, without a stderr bound. This unchanged baseline boundary was expressly outside this correction cycle.

Review 11 unopened/maybe-false rows remain unverified evidence, not confirmed defects or completed work. Existing direct patches already present were preserved. No new review or counter change occurred.

The following independently supplied inputs are still required; unknown values remain unset:

- A reviewed `accepted-profile.json` using `ext-protection-1-profile/v3`, binding both full accepted source revisions, the unchanged 41-case plan and exact engine binary hash, with the accepted date/profile digest. No values are proposed here.
- A complete `ext-protection-1-role-installation/v2` closure for each shipped role: apphost, entry/dependency/runtime configuration, selected managed/native/resource assets, accepted role configuration and public anchor. Supply the trusted .NET 10 host/SDK apphost template, independently operated role identities/private signers, and distinct bearer values in distinct inert `env:EXT_PROTECTION_ROLE_[A-Z][A-Z0-9_]{0,63}` references.
- The same actual dedicated CA bytes in Secret `erasure-root-tls` member `ca.crt`, production `ErasureRootInstallation.TrustedCaCertificatePem`, and the file passed to bootstrap `--ca-certificate`. Supply the matching certificate/private-key Secret members and actual HTTPS identity. No Secret or certificate was installed.
- The exact reviewed canonical tenant-to-native-key map and independently selected occurrence-registry, deletion-consumption and lifecycle genesis scope/digest map. Supply current per-tenant private phase/final/migration/retention grants and usable capability trust; bootstrap must not guess genesis or normalize a different enrollment.
- The independently qualified `erasure-root-no-backup` storage class and actual exclusions for daily/off-node/storage/node/Velero/operator copies, with independent all-copy, restore and retirement authorities/evidence. Supply the actual A1 native/store/enrollment/volume bindings. Live-native trim and snapshot labels do not qualify physical copies.
- The [loop-7 signing readiness installation](../ext-protection-1-loop7/README.md): credential-bound backend `query-signing-availability`, exact current public trust response, and `DeletionSigningAvailabilityOperationHandler` with an unrevoked `deletion-signing-owner` role, method `signing-readiness-permission`, scope `<tenant>|deletion-signing-availability`, and `QualificationBindings` key `deletion-signing-availability:` plus uppercase SHA256 of the default .NET JSON readiness-request bytes. That request binds the actual provider identity and installation digest.
- Independently enrolled source/control/fault cells, current signed observer read-only permission and physical component/backing proof, exact persisted producer/copy bindings, and independent role endpoints/credentials.
- If legacy 4MiB source chunks remain, both the independently installed Dapr sidecar 8MiB response allowance (`--max-body-size 8Mi` or corresponding configuration) and the 8MiB SDK receive setting from `ErasureRootDaprTransport.Configure` at the first client registration. New 1MiB frames fit the default 4MiB transport; no sidecar change was installed.

Installed 41-case and full physical-copy qualification remain unestablished; production destruction remains independently gated.

The new exact node prerequisite is Linux cgroup v2, a compatible CRI runtime and container cgroup namespace exposing its own limits at the fixed paths, with kubelet `memorySwap.swapBehavior: NoSwap`. [Kubernetes](https://kubernetes.io/docs/concepts/cluster-administration/swap-memory-management/) documents runtime enforcement of NoSwap; the [kernel](https://www.kernel.org/doc/html/latest/admin-guide/cgroup-v2.html#memory-interface-files) defines separate memory and swap limits. Their 1Gi-plus-zero mapping supplies the requested combined 1Gi ceiling. The fragment was not installed and actual node/runtime settings remain unverified. This does not establish host-memory or system-daemon-swap erasure. See [prepared deployment prerequisites](../../../deploy/erasure-root/README.md).
