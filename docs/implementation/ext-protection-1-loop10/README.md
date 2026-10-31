# EXT-PROTECTION-1 loop 10 handback

Status remains `in-progress`. This record does not accept a target, profile, date, or availability. The native five-case workload was not run. No Live, OQ, Agents, cluster apply, or production OpenBao call was made. Loop 9 corrections stay. Rejected and unopened rows were not changed. `PrivateOwnerOperationDeadline` was not changed.

## Corrections

- R10-BH-1: `bootstrap.py --apply` reads each mount, key, policy, and CAS-0 head before writing it. A matching object is left in place and the run continues. A different object stops before that write. The failure JSON keeps `ERC_BOOTSTRAP_INPUT_OR_NATIVE_FAILURE` and adds `step` (`mount:`, `transit-key:`, `policy:`, `head:`, `head-config`, `health`, `ca-pin`, `credential`, or `enrollment`). `--apply` off still makes zero native calls.
- R10-BH-2: `--apply` requires `--ca-certificate`. The client builds a TLS context that trusts only that file. `X-Vault-Token` is not written when the certificate does not chain to the pin. A missing pin stops at `ca-pin` before any socket. A matching local CA still reaches `BootstrappedNotQualified`.
- R10-BH-3: each rendered per-tenant key-config stanza allows `min_decryption_version` and `min_encryption_version` of `[1]`, the version this bootstrap creates. The empty parameter lists are gone.
- R10-BH-5: the `erasure-root-tls` volume sets `defaultMode: 0400`. The container memory request is `1Gi`, equal to the memory limit, so the swap allowance under LimitedSwap is that same limit.
- R10-BH-7: the readiness command reads `sys/health` with the pinned CA and fails unless the JSON reports version `2.6.2`, `initialized` true, and `sealed` false.
- R10-BH-8: the prepared listener uses `max_request_size` 41943040 and `max_request_json_memory` 100663296. Their sum is under the 1Gi pod limit. Raft entry and transaction sizes are unchanged. The disposable native harness still uses the previous listener bounds.
- R10-BH-13 and R10-EC-3: registry recovery throws if the recovered snapshot deserializes to null, and that `ArgumentNullException` returns null together with `JsonException` and `InvalidOperationException`.
- R10-BH-17: `DecryptAsync` zeroes the request body in `finally`, including when the native call fails.
- R10-VG-1: `NewerCurrentOriginalRestoresExactRegistryState` restores a missing primary registry from a newer current original.
- R10-VG-2: `InvalidRecoveredSnapshotJsonReturnsUnavailable` and `NullRecoveredSnapshotReturnsUnavailable` cover non-JSON and null snapshot bytes.
- R10-VG-3: `SealedSkeletonIsDetachedAndOversizeSkeletonIsRejected` mutates the caller skeleton after retain and refuses a skeleton above 16777216 bytes without another write.
- R10-VG-4: `CurrentOriginalReadForwardsOnlyToExactSingleEnrolledOwner` covers exact, foreign, duplicate, and absent `ReadCurrentOriginalAsync` enrollment.
- R10-VG-5: `UnissuedProofAcceptsMatchingGuardAndRejectsMismatchedGuard` accepts a matching unissued proof on the real published-trust authority and rejects both a moved revision and a same-revision digest change.
- R10-VG-6: after the healthy `ReplaceAttestation`, `RejectedUnissuedReplacement` is null.

## Commands and results

Debug project-reference builds used `-nodeReuse:false`, `UseHexalithProjectReferences=true`, `GeneratePackageOnBuild=false`, `HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore`, `HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons`, and `HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src`. The process `HexalithCommonsRoot` was the same Commons path.

- `tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj`: 0 warnings, 0 errors.
- `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj` with `-m:1`: 0 warnings, 0 errors. Two earlier parallel builds reported `MSB3026` copy retries while the same outputs were being written; those copies then succeeded. The serialized rebuild is the zero-warning result.

Test DLLs were executed with `dotnet test <dll> --no-build`:

- `Hexalith.EventStore.Server.Tests.dll` for `InteractionOccurrenceRegistryActorTests`, `PublishedTrustReplacementAuthorityTests`, and `PublishedTrustOwnerStateAuthorityTests`: 146 passed.
- `Hexalith.Platform.Protection.Tests.dll` for `ErasureRootUnissuedReplacementTests`: 1 passed.
- `Hexalith.Platform.Protection.Tests.dll` for `OpenBaoErasureRootStoreTests`: 14 passed. Those tests do not call `DecryptAsync`.
- `python3 eng/ext-protection-1/test_bootstrap.py`: 10 passed.

## Still missing

Full installed qualification remains external. That includes the 41-case installed run, physical-copy qualification, A1 HTTPS/storage/native/genesis inputs, no-backup and all-copy restore and retirement, and a real erasure-root-tls Secret. The CA pin is a prepared CLI input. It was not applied to a cluster.

The key-config policy allows minimum versions 1 through 64 so the bootstrap epoch and later trims are permitted. The listener stays at 64MiB requests and 128MiB JSON, which already fit under the 1Gi limit. The memory request stays 256Mi and the limit stays 1Gi.
