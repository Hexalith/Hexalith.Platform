# Tenants — live source-development evidence

Runtime proof: **PASS**. Verifier: Codex driving netcoredbg and real local processes. UTC: `2026-10-20T15:50:05.203595+00:00`. Module-owner acceptance and the human audit fields below remain pending.

The owner workspace uses the recorded disposable current-tool/Platform overlay. Its complete kit adoption remains open; see [preflight gaps](../kit-preflight-gaps.md).

| Evidence | Observed value |
| --- | --- |
| Tested module revision | `f92546366fa60feb6173a7c76811fe19ea874d72` (local qualification commit for domain modules) |
| Owner source base | `9a94131a26d0c5d80d5540706e267ee123d54b68`; qualification changes only pins |
| Workspace | `/tmp/hexalith-sm2-20261020-zw8t88ea/Hexalith.Tenants` |
| Direct Platform revision | `4f036ced45aa40700948c19949bb874881c960b5`; source mode; release tag `None` |
| Builds gitlink / release tag | `fff4a212180d8cd1c5c0759655a9bc4fb6963c45` / `v4.35.0` |
| Restored tool | `4.35.0+fff4a212180d8cd1c5c0759655a9bc4fb6963c45`; exact accepted pin |
| Environment / debugger identity | [environment.json](../environment.json) |
| Commands / working directories / exits | [commands.json](commands.json) |
| Committed inputs and disposable overlay | [local qualification commit](../tenants-qualification/identity.json) and [patch](../tenants-qualification/qualification.patch) |
| Edited environment / run identity | `61b362eebf8f5be4fd3806ab35a47884` |
| Source mapping | [edited-source-mapping.json](edited-source-mapping.json), SHA-256 `c658558158a66aed3fec840e6b6f656a984dd79e78f9f85db4f0f3267ed3fe85` |
| Selected project resources | `eventstore`, `memories`, `memories-access-telemetry`, `memories-access-telemetry-clock`, `tenants`, `tenants-api` |
| Loaded build identities / Debug | [loaded-build-hashes.json](loaded-build-hashes.json); source/Debug mapping |
| Human source audit | Pending owner acceptance; [kit preflight gaps](../kit-preflight-gaps.md) preserve static failures |
| Human Dapr / ACL / mTLS audit | Pending owner acceptance; [edited-state.json](edited-state.json) records observed sidecar/control-plane readiness |
| Breakpoint | `/tmp/hexalith-sm2-20261020-zw8t88ea/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs:167`; [actual debugger transcript](debugger.txt) |
| Source edit and runtime effect | [source-edit.patch](source-edit.patch); observed `tenants-sm2-20261020` |
| Original / edited source hashes | [result.json](result.json) |
| Direct gitlinks / nested references | [direct-only-references.json](direct-only-references.json); initialized nested count zero |
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
