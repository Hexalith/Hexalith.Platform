---
title: 'Execute the local F1 whole-request transport gate'
type: 'feature'
created: '2026-10-20'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: e9a142dc5c56cdb3c187f5eec0d3b3a7178a0058
context:
  - docs/implementation/ext-secrets-1-f1-2026-10-20/transport-profile.md
  - docs/implementation/ext-secrets-1-f1-2026-10-20/transport-profile.json
  - docs/implementation/ext-secrets-1-f1-2026-10-20/termination-protocol.md
  - docs/implementation/ext-secrets-1-f1-2026-10-20/f2-f3-prerequisites.md
---

<frozen-after-approval reason="user explicitly authorized implementation and synthetic local execution">

## Intent

Implement and execute the next local F1 transport gate for Agents Story 5.4: build exact Dapr 1.18.1 source plus the retained header correction, a test-only pluggable OutputBinding using retained protocols, and an instrumented synthetic sink. Drive the actual Dapr.Client SDK through daprd and UDS into that component and sink. Retain reviewable reproducible source, binaries, configurations, measurements and hashes.

## Boundaries & Constraints

Preserve Dapr-only production design, public bounds, atomic complete consumption, canonical bytes/signatures, analyzer fixes, existing tests and all unrelated dirty work. No production DI/provider registration or invoker activation. Wrap/unwrap, DependencyAvailable, LiveReady, KeyDeliveryEnabled, CompletionPermission and physical-completion claims remain closed/false. Fixtures grant no semantic custody, retention, termination or erasure authority.

Temporary local processes and synthetic material are authorized. No nested submodule initialization, deployment, provisioning, credentials, native providers, destruction or Live/F0/owner qualification. Public source/toolchain downloads are permitted to build pinned inputs. Use fresh detached source/artifact/evidence paths, retain failures, and clean up only owned temporary processes. No push or changes to acceptance registers. Original proposed packets remain historical evidence; record new executed results in a separate local packet.

## I/O & Edge-Case Matrix

| Given | When | Then |
|---|---|---|
| Every owner-legal 1,000-target manifest | Apply complete field-derived bounds and exercise retained escaped witness | One complete request reaches sink; exact JSON/API hashes and 24,692,931 API bytes retained; no target splitting. |
| Maximum 32 MiB transition | SDK invokes binding | Exact 44,740,962-byte API request reaches sink intact once. |
| Conservative bounds and request ceilings | Boundary/over-bound probes run | Exercise sufficient manifest/transition bounds, 64 MiB data and 128 MiB protobuf ceilings; reject excess at identified hop. |
| Incoming headers and API metadata | Forward through daprd | Measure decoded HTTP/2 list with 32-byte entry overhead at 65,535/65,536/65,537; enforce complete post-sanitization map including generated trace fields: 64 entries, 256/4096 key/value UTF-8 bytes, 65,536 aggregate. |
| Full component response | Include data, metadata, content type, unknowns/compression | Strict proto.Size+5 <1 MiB; measure independent full frames, reject unknowns/compression; test below/at/above cap. |
| Cancellation, late duplicate, lost result, reconnect | Transport fault occurs | Measure attempts/incarnations; retain original synthetic identity and uncertainty, recover status/lookup only; never claim completion/exactly-once. |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.Platform.Secrets.Runtime.Tests/CustodyBindingFeasibilityTests.cs`: exact manifest/transition constructions at lines 49–57; reuse fixture and contract without changing canonical bytes. `CustodyBindingDaprInvokerTests.CaptureCandidateAsync` uses pinned SDK raw BindingRequest; new test-only live-local driver can reside in same assembly to reuse internals. No production changes required.
- `docs/implementation/ext-secrets-1-f1-2026-10-20/protocol/`: exact bindings/common proto and SHA256SUMS. Upstream pinned generated Go types may be imported after proving descriptor/source equality. Reflection v1alpha and protected socket stem discovery required.
- Retained Dapr source pin `4cef924a0b1360c69d810deec8cd59bf853f6721`, components-contrib `72e6a52340620aa41c4deecc40cbc6f0007b9afe`, grpc-go 1.80.0. Exact header patch path/base/result hashes are in profile; independently validate/apply to disposable source. Dapr go.mod requires Go 1.26.4; Go/protoc absent on PATH at investigation (download local tools if needed).
- `eng/ext-secrets-1/verify_f1.py`: preserve 238-test contract, exact captures and warning-free audits. Clean dependency roots: `/home/administrator/projects/hexalith/agents/references/Hexalith.Builds` and sibling `Hexalith.EventStore`, exact revisions in profile.
- `eng/ext-secrets-1/check_f1_packets.py`: historical proposed packet checker must keep existing assertions; add separate executed-packet audit/tests rather than relabeling historical proposals.
- Existing research source bundle: `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-20261020/research/`. Work is a local follow-on to completed transport-completion spec, not Platform sprint story 5.4.

## Tasks & Acceptance

- [ ] `eng/ext-secrets-1/local-transport/` — reproducible pinned fixture component/sink, dependency lockfiles and build/run tooling; no production provider.
- [ ] Existing Runtime tests or test-only harness — reuse exact request witnesses and actual SDK, independent wire instrumentation, meaningful boundary/fault coverage with value-free observations.
- [ ] `eng/ext-secrets-1/` — local gate orchestration and fail-closed evidence audit with adversarial tests; exact source/patch/protocol/generated/binary/configuration hashes, effective retry settings and socket/process isolation recorded.
- [ ] New local transport report/machine packet in existing F1 docs directory — retain measured every-hop sizes/digests/metadata/decoded headers/full responses and faults, field-derived universal sufficiency argument distinguished from finite witnesses; all authority/completion flags false.
- [ ] Existing README and F2/F3 prerequisites — precise remaining actual semantic component/backend/termination prerequisites and next concrete qualification packet.
- [ ] Run warning-free pinned Dapr/fixture/.NET builds, F1 verifier and complete Python suite with fresh detached artifacts; independently inspect full scoped diff and exact retained evidence. Mark limitations honestly; fix failures without weakening requirements.

Acceptance: Given synthetic transport fixtures, when the gate completes, then the local transport path and sufficient bounds are evidenced, no semantic/provider/physical authority is inferred, and next qualification inputs are concrete and explicit.

## Implementation Notes

No unresolved intent gaps or irreversible operations. User authorization covers implementation/local synthetic execution; existing unrelated edits are preserved. Detached sources/artifacts avoid interfering with ongoing work. Do not amend the frozen scope to accommodate failures: report unsupported behavior and supply a qualified test-only enforcement seam where required.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence and disposition |
|---|---|---|
| B1 | medium / patch | Fresh builder writes build-manifest.json, while audit reads source/source-inputs.json; no supplied writer exists. Emit the consumed record and test the producer/consumer connection. |
| B2 | medium / patch | Pinned decoder discards unknown fields within metadata map entries before top-level GetUnknown. Validate raw nested wire before loss; independently report it and add a real nested-unknown probe. |
| B3 | medium / patch | Auditor compares current binaries with execution and packet with seal separately, permitting stale seal linkage. Require seal, retained artifact and executed image hashes to agree. |
| B4 | medium / patch | SDK seal omits app-local Runtime/EventStore/gRPC assemblies and runtimeconfig loaded by the driver. Bind and verify the complete app dependency directory. |
| B5 | medium / patch | Inherited Go flags/workspace/checksum exclusions can alter build inputs while source files remain pinned. Isolate effective build settings and retain them. |
| B6 | medium / patch | Negative probes accept any non-OK/empty status; unrelated connection loss can satisfy rejection. Require the observed intended status/reset and enforcement boundary. |
| B7 | medium / patch | SDK opaque requests and later Go probes share body digests. Component/sink digest-only matching can borrow Go evidence. Correlate the SDK case across forwarded metadata and sink. |
| B8 | medium / patch | all([]) and command iteration allow empty process/cleanup inventories. Require the complete expected named process inventory with matching PID/start/image/terminal observations. |
| B9 | medium / patch | Published independent evidence and detached verifier reports are referenced but not opened/hashed by check_packet. Authenticate those retained bindings and passing/closed outcomes. |
| B10 | medium / patch | Nonempty all-null sections permit omitted prerequisites; template artifact hashes are unchecked. Require exact prerequisite keys and executed artifact bindings. |
| E1 | medium / patch | Independently confirmed the same missing source-input manifest producer as B1; group by that root cause. |
| E2 | medium / patch | Independently confirmed stale seal/execution binary linkage as B3; group by that root cause. |
| E3 | low / reject | Malformed response-size control outside the fixed runner schedule can panic the fixture. The retained runner supplies only 1048575/1048576/1048577; extra input guards are not needed for ordinary use of this fixed gate. |
| E4 | low / reject | Unrepresentable padding can loop for arbitrary CLI values, but all retained exact 128MiB boundary constructions are representable and executed. A generic padding search extension is unnecessary for this fixed schedule. |
| E5 | medium / patch | Completed streams remain in the observer data map with slices backed by large allocations. The persistent component connection handles many large streams in this actual schedule; remove consumed buffers and ended stream state. |
| E6 | medium / patch | Same demonstrated wrong rejection-cause acceptance as B6; group by that root cause. |
| E7 | medium / patch | Short descriptor/probe processes can exit before /proc/PID/exe opens; this records null and causes a correct run to fail audit. Bind the exact executed file robustly for short-lived children. |
| V1 | medium / patch | Preverified gap: deleting eight accepted metadata sink arrivals and refreshing inventory still passes full audit. Require positive send sink evidence and an adversarial regression. |
| V2 | medium / patch | Preverified producer/consumer gap repeats B1; no new root cause. |
| R1 | medium / patch | Root reproduced acceptance of empty configuration hashes, advertised header limit=1 and derived manifest bound=1 in temporary packets with rebound template hash. Pin complete published configuration, header/metadata/sufficiency declarations. |
| R2 | medium / patch | Root independently reproduced omission of all but one termination prerequisite; same root cause as B10. |

## Verification

Use fresh external artifacts/evidence for every verifier attempt. Run `python3 eng/ext-secrets-1/verify_f1.py --builds-root /home/administrator/projects/hexalith/agents/references/Hexalith.Builds --eventstore-root /home/administrator/projects/hexalith/agents/references/Hexalith.EventStore --artifacts <fresh> --output <fresh>` and `python3 -m unittest discover -s eng/ext-secrets-1 -p 'test_*.py' -v`. Retain exact local transport commands, logs, source identities and results. No fixtures count as physical evidence.
