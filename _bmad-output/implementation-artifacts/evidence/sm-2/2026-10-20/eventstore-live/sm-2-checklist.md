# EventStore — live source-development evidence

Runtime proof: **PASS**. Verifier: Codex driving netcoredbg and real local processes. UTC: `2026-10-20T15:04:04.169328+00:00`. Module-owner acceptance and the human audit fields below remain pending.

The technical module runs from the Platform workspace.

| Evidence | Observed value |
| --- | --- |
| Tested module revision | `f6b545cd02632f662136d0b4b8e6952baa7afec3` (local qualification commit for domain modules) |
| Owner source base | `f6b545cd02632f662136d0b4b8e6952baa7afec3`; qualification changes only pins |
| Workspace | `/tmp/hexalith-sm2-20261020-zw8t88ea/Hexalith.Platform` |
| Direct Platform revision | `4f036ced45aa40700948c19949bb874881c960b5`; source mode; release tag `None` |
| Builds gitlink / release tag | `fff4a212180d8cd1c5c0759655a9bc4fb6963c45` / `v4.35.0` |
| Restored tool | `4.35.0+fff4a212180d8cd1c5c0759655a9bc4fb6963c45`; exact accepted pin |
| Environment / debugger identity | [environment.json](../environment.json) |
| Commands / working directories / exits | [commands.json](commands.json) |
| Committed inputs and disposable overlay | Platform snapshot at its recorded gitlinks |
| Edited environment / run identity | `b3caf6a6aff8be66a673ad539a160eed` |
| Source mapping | [edited-source-mapping.json](edited-source-mapping.json), SHA-256 `47570fa306533d3ba5a3ef2a920d630c48aa866892262425fe6e278b8c1d9e47` |
| Selected project resources | `eventstore` |
| Loaded build identities / Debug | [loaded-build-hashes.json](loaded-build-hashes.json); source/Debug mapping |
| Human source audit | Pending owner acceptance; [kit preflight gaps](../kit-preflight-gaps.md) preserve static failures |
| Human Dapr / ACL / mTLS audit | Pending owner acceptance; [edited-state.json](edited-state.json) records observed sidecar/control-plane readiness |
| Breakpoint | `/tmp/hexalith-sm2-20261020-zw8t88ea/Hexalith.Platform/references/Hexalith.EventStore/src/Hexalith.EventStore/Extensions/ServiceCollectionExtensions.cs:221`; [actual debugger transcript](debugger.txt) |
| Source edit and runtime effect | [source-edit.patch](source-edit.patch); observed `eventstore-sm2-20261020` |
| Original / edited source hashes | [result.json](result.json) |
| Direct gitlinks / nested references | [Platform direct-only references](../platform-direct-only-references.json); initialized nested count zero |
| Hosting inventory / retirement gates | Existing owner freeze/retirement policy retained; no hosting retirement performed |
| Readiness / deadline | `Ready`; effective deadline `600` seconds; [edited-state.json](edited-state.json) |
| Down / cleanup | Exact run-ID down returned exit `0`; runner asserted state and workspace removal; [commands.json](commands.json) |
| Source restoration | Byte-for-byte original restored; [source-restored.json](source-restored.json) |
| Story 3.11 / owner acceptance | Supports the evidence story; overall sign-off remains open |

- [x] Observed active-checkout breakpoint hit.
- [x] Observed source-edit runtime effect after rebuilding through the module tool.
- [x] Restored source bytes and completed exact-run cleanup.
- [x] Captured direct-only gitlinks and absent initialized nested references.
- [ ] Module owner accepted the source/Dapr audit and this evidence.

Scope: source/Debug development through `/ready` (server modules) or offline `modules` (McpCli). Business-flow qualification and source/package/deployed retirement parity remain separate gates.
