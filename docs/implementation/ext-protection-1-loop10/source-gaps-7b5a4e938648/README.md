# Loop-10 source-gap continuation

Status: `in-progress`; review counter 10 and both original baselines are unchanged. This packet records the authorized source-gap continuation only. Historical evidence is preserved. No accepted source target, profile, integration date or availability is proposed. No Live/OQ/Agents consumer, cluster installation, production OpenBao or Git mutation is performed.

The two source corrections are implemented. Bootstrap/prepared checks pass 12/12, harness checks pass 7/7, the required serialized Debug/project-reference build has zero warnings/errors, and the fresh frozen DLL passes 44 transport/custody checks without skips. Shipped bootstrap native integration passes with six exact owner genesis scopes and two real lifecycle actors. The unchanged five-case native suite is running; its result is pending and no new five-case pass is claimed here.

## Corrections and measured cause

`deploy/erasure-root/bootstrap.py` now recognizes the real KV-v2 defaults only when the current invocation has just created that mount. It writes the required `max_versions=1` / `cas_required=true`, then reads the configuration back before proceeding. Existing matching configuration resumes without writes; existing mismatches refuse before configuration mutation. Unexpected defaults on a newly created mount and mismatched readback also refuse. No fixture seeds the shipped bootstrap configuration.

The new bootstrap regression first reproduced the original `head-config` failure. The passing regression also proves exact resume, refusal of both pre-existing defaults and conflicting configuration, refusal of unexpected new-mount configuration, mandatory readback before key creation, retained key/policy/head conflict checks, CA-pin refusal before credential exposure, and default no-apply zero calls. The native bootstrap result independently records the actual initial `200` / `0` / `false` response followed by successful runtime owner validation. Its fixture retains that first bounded observation while permitting the second readback request.

The minimum-version 403 was isolated before changing production policy or transport. Two fresh verified owned OpenBao 2.6.2 probes compared the exact rendered policy and a candidate changing only allowlist literal types:

| Policy version literals | Request version values | `bao write` with JSON stdin | Direct HTTP to container loopback |
|---|---|---|---|
| Numeric | Numeric | 403 | 403 |
| Numeric | Strings | 403 | 403 |
| Quoted strings | Numeric | 403 | 403 |
| Quoted strings | Strings | Exit 0 | 200 |

The observed incompatibility belongs to both ACL literals and request value types. It also occurs through direct HTTP, excluding a CLI-only cause. The renderer now emits exactly the strings `"1"` through `"64"` for each minimum-version parameter. Production `OpenBaoErasureRootStore.TrimAsync` serializes matching invariant string values; its actual trim request and all other native request bodies retain their previous forms. No CLI transport code changed. The component regression exercises epochs 2 and 64. The native forbidden-operation helper additionally checks versions 0 and 65 remain denied; tenant boundaries and existing export/backup/restore/deletion guards are retained. The five native test bodies and workload sizes are unchanged.

The diagnosis result files retain the original observations. For `bao` successes their `httpStatus: 204` is a normalized CLI-success marker inferred from exit 0, not an observed wire status; the direct HTTP entries report observed wire statuses. No remote body, token or key is retained. Both diagnostic containers were removed and their tmpfs cleanup verified.

## Evidence and execution

Every build/test/native command has its own raw outer log and `*.command.json` interval with argv, working directory, environment references, exit code and log SHA-256. The established native harness captures provider/test output privately and exposes only closed counts, safe failures/locations, exact policy hashes, geometry and cleanup. Its bounded observations are not raw provider logs. Native operations run only after exact image/version, ownership and isolation verification. Private eligibility remains the existing explicitly synthetic test fixture.

`freeze.json`, `runtime-manifest.json` and `source-manifest.json` bind the complete 257-file runtime and 4,412-file source/build context at `/tmp/ext-protection-loop10-source-gaps-7b5a4e938648/`. The source inventory records build context and does not expand implementation scope. Required build settings are Debug, project references, `-m:1`, `-nodeReuse:false`, `GeneratePackageOnBuild=false`, `HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore`, physical `HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons`, and `HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src`. The process Commons setting matches that same physical path. DLLs run directly from the frozen runtime. Bootstrap integration and all five native cases run serially.

The real native scope uses the locally cached `quay.io/openbao/openbao:2.6.2@sha256:11fd73a2102cda9c55d5d881a8c3210303146a7ec1e8ac76f526e175c6d24641`, source revision `dd9c19c37a878cf4a81b18efb8d6f0599c7da923`, network `none`, no published ports, read-only root, UID/GID 65532, dropped capabilities, no-new-privileges, disabled Docker logs and tmpfs-only writable paths. Full native geometry remains 1,000 targets, actual 9MiB state, a head above 8MiB, both history geometries and 256 owners/occurrences. Runtime credentials use the actual shipped rendered tenant policies, without a shared wildcard or default policy. Root is bootstrap-only.

## Independent installation prerequisites

These remain missing/unestablished regardless of local native outcomes:

- A reviewed `accepted-profile.json` using `ext-protection-1-profile/v3`, binding both full accepted source revisions, unchanged 41-case plan, engine binary, accepted date and profile digest.
- Each role's complete `ext-protection-1-role-installation/v2` closure, trusted .NET 10 host/SDK apphost template, independently operated identities/private P-256 signers, endpoints and distinct bearer values in separate inert `env:EXT_PROTECTION_ROLE_[A-Z][A-Z0-9_]{0,63}` references.
- The same dedicated CA bytes in Secret `erasure-root-tls` member `ca.crt`, `ErasureRootInstallation.TrustedCaCertificatePem`, and bootstrap `--ca-certificate`, plus matching server certificate/private-key members and HTTPS identity.
- Canonical tenant/native-key mappings and independently selected exact occurrence-registry, deletion-consumption and lifecycle genesis scopes/digests; current per-tenant private phase/final/migration/retention grants and complete usable capability trust. Existing mismatching configuration or policy is refused; bootstrap does not silently rewrite an older installation.
- Independently qualified `erasure-root-no-backup` storage, actual daily/off-node/storage/node/Velero/operator exclusions, independent all-copy/restore/retirement authorities and evidence, and exact A1 store/enrollment/volume/native bindings. Disposable trim and labels do not qualify physical copies.
- The loop-7 signing-readiness backend `query-signing-availability`, credential-bound current public trust, and `DeletionSigningAvailabilityOperationHandler` with unrevoked `deletion-signing-owner` role, method `signing-readiness-permission`, scope `<tenant>|deletion-signing-availability`, and `QualificationBindings` key `deletion-signing-availability:` plus uppercase SHA256 of the default .NET JSON readiness request binding actual provider identity and installation digest.
- Independently enrolled source/control/fault cells, signed current observer read-only permission and physical component/backing proof, and exact authenticated persisted producer/copy bindings.
- For legacy 4MiB source chunks, both Dapr sidecar 8MiB response allowance (`--max-body-size 8Mi` or equivalent configuration) and the 8MiB SDK receive setting from `ErasureRootDaprTransport.Configure` at first client registration. New 1MiB framing fits default 4MiB transport.
- Linux cgroup v2, compatible CRI/container cgroup namespace, kubelet `memorySwap.swapBehavior: NoSwap`, and verified prepared startup limits `memory.max=1073741824` / `memory.swap.max=0`. Actual node/runtime settings and host-memory/system-daemon-swap erasure remain unproven.

R7-BH-7 and unopened review findings remain deferred. No new review cycle is opened. Complete independently installed 41-case and physical-copy qualification remain unestablished; production destruction remains independently gated.
