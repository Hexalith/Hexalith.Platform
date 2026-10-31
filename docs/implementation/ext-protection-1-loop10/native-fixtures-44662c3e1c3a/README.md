# Loop-10 native fixture continuation

Status: `in-progress`. Review counter 10, the original baselines, frozen intent and historical evidence are preserved. This directory is a new record; it supersedes no historical bytes. No accepted target, profile, integration date or availability is proposed. No Live/OQ/Agents, cluster installation, production OpenBao, Git mutation or remote operation is performed.

The fixture repairs are implemented. Verification exposes two source gaps: fresh shipped bootstrap refuses native KV-v2 defaults, and an observed native request/policy incompatibility returns HTTP 403 for the minimum-version configuration request; its underlying cause remains unproven. Production source remains unchanged. The final native suite executed all five unchanged cases and failed four; no new all-five pass is claimed. [verification.json](verification.json) binds the complete evidence.

## Fixture changes

`OpenBaoBootstrapIntegrationTests` now extracts the shipped `bootstrap.py` together with its adjacent `custody-policy.hcl`, passes the relay CA explicitly through `--ca-certificate`, and selects the actual shipped rendered per-tenant lifecycle policy for each tenant's runtime token. The token must contain exactly that one policy, without the default policy. Runtime owner/genesis/lifecycle calls use that tenant's token, and a foreign-tenant head read must be denied. A bounded public step marker preserves bootstrap refusal diagnostics without releasing its stderr, bearer or provider body.

`OpenBaoNativeCustodyFixture` runs the same shipped Python renderer. It issues one exclusive restricted token per tenant and routes each actual native operation through that tenant's real `OpenBaoErasureRootStore`. The five test bodies and all workload sizes remain unchanged. The existing policy helper additionally verifies that each runtime token is forbidden from the other tenant's head, archive and transit key. Runtime credentials are cleared when the fixture is disposed.

The native harness now checks two exact tenant/scope policy records per fixture, independently reproducing each policy hash with the shipped renderer. Deny-fragment-only, missing-tenant, foreign-scope, shared-policy and duplicate-tenant evidence is refused by offline regressions. No mock response or synthetic key replaces native OpenBao. Private eligibility remains the existing explicitly synthetic test authority.

## Evidence and execution boundaries

Required builds use serialized Debug/project references, `-m:1`, `-nodeReuse:false`, `GeneratePackageOnBuild=false`, `HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore`, physical `HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons`, and `HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src`. The process Commons setting matches that same physical path. Test DLLs execute directly from complete inventoried external runtimes.

The first required build failed on a concurrent duplicate local variable in `DelegatedOwnerQueryClient.cs`. The parent renamed only the second pattern variable, preserving the concurrent timeout logic. The failure log and exact before/after source binding are retained; subsequent fixture builds report zero warnings and errors. This is a concurrent build repair, separate from fixture behavior.

The first five-case native attempt overlapped a later test-only build and bootstrap diagnostic attempt. This violated the workflow's serialization requirement. Its timestamps, outcome and cleanup are retained without claiming serialized compliance. After the overlap was noticed, no further build/test started until that native attempt finished and its cleanup was verified. The final native verification did run serially after the diagnostic attempts and cleanup.

Raw outer-command logs are retained. The established native harness deliberately retains only closed test counts, safe failure markers/types/locations, policy hashes and public geometry from captured provider/test output, since raw assertion bodies may contain private material. Those closed observations are not relabelled as raw provider logs.

## Executed verification and remaining source gaps

Three required Debug/project-reference fixture builds pass with zero warnings and errors after the separately recorded concurrent build repair. Bootstrap/prepared shell tests pass 12/12, changed-harness checks pass 7/7, and the frozen initial DLL executes 42 transport/custody tests with zero failures/skips. These component results belong to their recorded runtime and do not replace native evidence.

The final native suite runs serially from `2026-10-31T06:35:07.846652+00:00` to `2026-10-31T07:14:10.763652+00:00`, including harness setup/cleanup. Native execution takes **2330.957 seconds**: **5 executed, 1 passed, 4 failed, 0 errors/skips/not-run**. The [case results](native-five-final.result.json) retain exact safe outcomes and source locations:

| Unchanged native case | Outcome |
|---|---|
| Tenant/context substitution, ACL denials, archive/history/index geometry | Passed |
| Pin race | Failed at line 86 |
| Lost trim acknowledgement / exact lookup | Failed at line 97 |
| Leaked old ciphertext trim / survivor | Failed at line 34 |
| Complete 1,000-target manifest vector | Failed at line 63 |

The complete 1,000-root registration loop reaches its manifest assertion; terminal consumption, ordered vector and lookup are not established. The passed context/history/index case retains the actual 9MiB state, a **9,439,440-byte head**, both history geometries and **256 owners/occurrences**. All ten final tenant-policy records reproduce exactly from the shipped renderer. These are narrow primitive results with synthetic private eligibility, not installed qualification.

All six owned native attempts remain separate:

| Attempt | Executed outcome | Cleanup |
|---|---|---|
| Bootstrap 01 | 1 failed; generic refusal before runtime tokens | Verified |
| Bootstrap 02 | 1 failed; `Bootstrap-head-config` | Verified |
| Initial five-case 01 | 5 executed / 4 failed; initial overlap deviation retained | Verified |
| Bootstrap 03 | 1 failed; exact fresh KV defaults captured | Verified |
| Narrow lost-trim diagnostic | 1 failed; rotate 200, config 403 twice, no trim | Verified |
| Final serialized five-case | 5 executed / 4 failed | Verified |

[Independent post-run checks](post-run-audit.json) and the [corrected parent audit](parent-external-runtime-audit-v2.json) confirm all six exact container IDs absent, all harness tmpfs cleanup complete, and all three 257-file runtime closures unchanged. The [serialization record](serialization.json) preserves four initial overlap intervals: failed build/harness unit, bootstrap 01/transport components, initial native five/build 03, and initial native five/bootstrap 02. The final native interval overlaps no owned build/test. No initial attempt is relabelled as serialized or passing.

The [source-gap trace](observed-source-gaps.json) is concrete:

1. Shipped bootstrap's existing `GET erc-heads/config` returns HTTP 200 with `max_versions=0` and `cas_required=false` immediately after it creates the KV-v2 mount. `ensure_config` writes only when data is absent and otherwise refuses the mismatch with required `{1,true}`. Bootstrap therefore stops before key/policy/head installation and its runtime-owner/genesis/lifecycle checks. The fixture did not seed configuration or bypass this stop. A source repair must handle fresh/default configuration while preserving partial-run matching/conflict refusal and readback.
2. Under the exact rendered tenant policy, native rotation returns 200, then the store's minimum-version configuration request returns 403 during both consumption and exact lookup. No trim POST occurs. The existing allowlists remain exactly 1..64; tenant isolation and export/backup/restore/deletion guards remain intact. The type-level cause and equivalence with direct production HTTP remain unestablished because this owned fixture forwards through the existing `bao` CLI transport. A source/transport compatibility repair and renewed native verification remain necessary; no policy bound or production behavior was changed here.

The final runtime has distinct DLL/PDB hashes from the initial graph; [final-freeze.json](final-freeze.json) lists every difference. Native-relevant production source bytes remain unchanged, while test-only diagnostics and unrelated concurrent source activity are explicitly inventoried. Whole binaries are not claimed equal. Broad source inventories record build context and do not expand task scope. The [parent preservation audit](parent-preservation/final-preservation-audit.json) confirms all 49 historical files unchanged, the original spec preserved as a prefix, frozen intent, status, original baselines and counter 10 unchanged. The only spec addition is the authorization for this continuation. The superseded parent runtime audit used a case-sensitive missing-container matcher; its false absence flags are preserved with the corrected v2 audit and do not indicate failed cleanup.

## Reproducible disposable prerequisites

The local tools observed in this run are .NET SDK `10.0.403`, MSBuild `18.9.15`, .NET/ASP.NET host `10.0.14`, Python `3.14.4`, and Docker client/server `29.8.2`. Docker is addressed only through `unix:///var/run/docker.sock`. The exact locally cached image is `quay.io/openbao/openbao:2.6.2@sha256:11fd73a2102cda9c55d5d881a8c3210303146a7ec1e8ac76f526e175c6d24641`; image labels and the actual native version command bind source revision `dd9c19c37a878cf4a81b18efb8d6f0599c7da923`. No image was installed or pulled.

The harness verifies ownership and isolation before native operations: a fresh run-specific label/container, no network or published ports, read-only root, non-root UID/GID 65532, dropped capabilities, no-new-privileges, disabled Docker logging and tmpfs-only writable paths. The existing disposable geometry remains: 2GiB memory with combined memory/swap 2GiB, 1536MiB data tmpfs, bounded native requests/JSON/Raft, the complete 1,000-target manifest, actual 9MiB state, a head above 8MiB, both history geometries and 256 owners/occurrences. Owned removal verifies both container absence and tmpfs unmount. These settings establish only this disposable test scope.

## Independently supplied installation prerequisites

Unknown values remain unset. Fixture results do not establish the following inputs or authorize the owner command:

- A reviewed `accepted-profile.json` using `ext-protection-1-profile/v3`, binding both full accepted source revisions, the unchanged 41-case plan and exact engine binary hash, with the accepted date/profile digest.
- A complete `ext-protection-1-role-installation/v2` closure for each shipped role: apphost, entry/dependency/runtime configuration, selected managed/native/resource assets, accepted role configuration and public anchor. Supply the trusted .NET 10 host/SDK apphost template, independently operated role identities/private signers, and distinct bearer values in distinct inert `env:EXT_PROTECTION_ROLE_[A-Z][A-Z0-9_]{0,63}` references.
- The same dedicated CA bytes in Secret `erasure-root-tls` member `ca.crt`, production `ErasureRootInstallation.TrustedCaCertificatePem`, and bootstrap `--ca-certificate`, with matching server certificate/private-key Secret members and the actual HTTPS identity. Nothing was installed.
- The reviewed canonical tenant-to-native-key map and independently selected occurrence-registry, deletion-consumption and lifecycle genesis scope/digest map, plus current per-tenant private phase/final/migration/retention grants and complete usable capability trust. Bootstrap must not guess genesis or normalize a different enrollment.
- The independently qualified `erasure-root-no-backup` storage class and actual daily/off-node/storage/node/Velero/operator exclusions, independent all-copy/restore/retirement authorities and evidence, and actual A1 native/store/enrollment/volume bindings. Live-native trim and snapshot labels do not qualify physical copies.
- The loop-7 signing-readiness installation: credential-bound backend `query-signing-availability`, exact current public trust response, and `DeletionSigningAvailabilityOperationHandler` with an unrevoked `deletion-signing-owner` role, method `signing-readiness-permission`, scope `<tenant>|deletion-signing-availability`, and `QualificationBindings` key `deletion-signing-availability:` plus uppercase SHA256 of the default .NET JSON readiness-request bytes binding the actual provider identity and installation digest.
- Independently enrolled source/control/fault cells, current signed observer read-only permission and physical component/backing proof, exact persisted producer/copy bindings, and independent role endpoints/credentials.
- For remaining legacy 4MiB source chunks, both the independently installed Dapr sidecar 8MiB response allowance (`--max-body-size 8Mi` or corresponding configuration) and the 8MiB SDK receive setting from `ErasureRootDaprTransport.Configure` at first client registration. New 1MiB frames fit default 4MiB transport. Nothing was installed.
- Linux cgroup v2, a compatible CRI runtime and container cgroup namespace exposing its own limits at the fixed paths, with kubelet `memorySwap.swapBehavior: NoSwap`. Prepared startup requires `memory.max=1073741824` and `memory.swap.max=0`; actual node/runtime settings remain unverified, and host-memory/system-daemon-swap erasure remains unproven.

R7-BH-7's generic subprocess-capture defer and unopened review rows remain deferred. No new review cycle is opened. The complete independently installed 41-case run and physical-copy qualification remain unestablished, and production destruction stays independently gated.
