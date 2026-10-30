# EXT-PROTECTION-1 loop 9 handback

Status remains `in-progress`. This record does not accept a target, profile, date, or availability. Loop 7 candidates stay withdrawn. The native five-case workload was not run. No Live, OQ, Agents, cluster apply, or production OpenBao call was made.

## Corrections

- R9-BH-2: `ErasureRootInstallation.TrustedCaCertificatePem` is the dedicated erasure-root TLS CA. The production `OpenBaoErasureRootStore` constructor and the `hexalith-erasure-root` handler trust only that pin. A missing or unparseable pin throws before a socket opens. A matching local certificate receives the bearer. A different CA fails during the handshake, before the token is written.
- R9-BH-3: `bootstrap.py --apply` creates each transit key with `deletion_allowed: false`, installs one rendered policy per tenant and genesis scope, and reads that policy and each CAS-0 head back. A missing policy, a different policy body, or a head that does not match the written bytes returns `DependencyNotAvailable`. `--apply` off still makes zero native calls.
- R9-BH-5: each rendered policy names that tenant's head path, archive prefix, and transit key. `custody-policy.hcl` is the shared deny fragment for export, backup, restore, metadata, Raft snapshot, and `sys/raw`. It is not a production credential and does not grant `tenants/*` or `encrypt/*`.
- R9-BH-6: ingress on TCP 8200 is the `hexalith` namespace (`kubernetes.io/metadata.name`). TCP 8201 stays on the headless Service for raft and is admitted only from the erasure-root pod. The namespace carries restricted Pod Security labels.
- R9-BH-8: after `UnwrapAsync` commits a holder id, a failed decrypt, second authorization, or confirmation read zeroes any local plaintext and CAS-removes that holder when the caller never received the bytes. Cancellation does not roll the holder back. A later unwrap of the same id can succeed. A holder that was returned stays restrictive until `CompleteHolderAsync`.
- R9-BH-13: ERC JSON uses an ordinal `SortedDictionary<string, T>` converter. A non-ASCII key under a culture that reorders `Comparer<string>.Default` keeps the ordinal hash.
- R9-BH-1: `erasure-root.hcl` sets `disable_mlock = true`, and the container mounts an emptyDir at `/tmp`.
- R9-BH-10: `ProveNativeCustodyReadyAsync` decrypts the throwaway ciphertext, compares it with the probe, and zeroes both buffers. A substituted decrypt fails preflight with no head CAS.
- R9-EC-1: `UnissuedReplacementProof` returns false when `AttestationOrdinal` is `long.MaxValue`, before the successor add.
- R9-EC-2: `VerifyTrimAsync` uses `int.TryParse` and returns false for a non-integer key name.
- R9-EC-3 and R9-EC-4: `RewrapAsync` zeroes the request and response buffers in `finally`, and a null batch ciphertext throws `InvalidOperationException` before `StartsWith`.
- R9-EC-5: registry recovery returns null for `JsonException` as well as `InvalidOperationException`.
- R9-EC-8: `erc-read-rejected-issue` returns null when more than one guard attempt matches.
- R9-EC-10: a duplicate admission archive key makes `ReadAsync` return null.
- R9-VG-1: `VerifyBlockedReplacementAsync` accepts a matching `UnissuedProof` and rejects a mismatched guard.
- R9-VG-2: a live custody root that is zeroed without `Dispose` does not complete. After `Dispose`, completion is allowed.

Rejected and deferred rows were not changed. `PrivateOwnerOperationDeadline` was not changed.

## Commands and results

Debug project-reference builds used `-nodeReuse:false`, `UseHexalithProjectReferences=true`, `GeneratePackageOnBuild=false`, `HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore`, `HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons`, and `HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src`. The process `HexalithCommonsRoot` was the same Commons path.

- `tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj`: 0 warnings, 0 errors. This graph compiled `Hexalith.EventStore.Server`.
- `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj` with `BuildProjectReferences=false`: 0 warnings, 0 errors. An earlier full graph retry reported one `MSB3026` while another process held `Hexalith.EventStore.Contracts.dll`; the copy then succeeded. That warning is not from the changed source.

Test DLLs were executed with `dotnet test <dll> --no-build`:

- `Hexalith.Platform.Protection.Tests.dll` for the store, custody, rejected-issue, host, loop 3, loop 5, loop 7, and custody-occurrence classes: 99 passed.
- The same DLL for `FullyQualifiedName~ErasureRoot`: 258 passed, 6 skipped. The skips are the owned OpenBao and Dapr harness facts. This is not a new native pass.
- `Hexalith.EventStore.Server.Tests.dll` for `PublishedTrustReplacementAuthorityTests` and `InteractionOccurrenceRegistryActorTests`: 116 passed.
- `python3 eng/ext-protection-1/test_bootstrap.py`: 10 passed. The module entry point now follows every test class, so this command runs the readiness, enrollment, policy, and apply cases.

## Still missing

Full installed qualification remains external. That includes the 41-case installed run, physical-copy qualification, A1 HTTPS/storage/native/genesis inputs, no-backup and all-copy restore and retirement, and a real erasure-root-tls Secret. The CA pin, per-tenant policies, and namespace allowlist are prepared source. They were not applied to a cluster.

`disable_mlock = true` is in the prepared ERC config because this cycle requires it. The earlier secrets probe treated that field as obsolete and was not changed. No OpenBao 2.6.2 process was started here, so this record does not show whether the prepared pod accepts the field. The client namespace in the network policy is the documented `hexalith` namespace, not a live cluster inventory.
