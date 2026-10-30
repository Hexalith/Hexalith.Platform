# Executed synthetic local F1 transport gate

The local SDK → patched daprd → pluggable UDS OutputBinding → instrumented sink path executed successfully. The [new machine packet](local-transport.json) retains the exact measured requests, every-hop protobuf/payload digests, decoded headers, full responses, metadata, attempts and incarnations. This is synthetic transport evidence. `DependencyAvailable=false`, `LiveReady=false`, `KeyDeliveryEnabled=false`, `CompletionPermission=false` and `PhysicalEvidence=false`. Wrap/unwrap and the production invoker remain closed. No semantic component, backend, native effect, credential, deployment or acceptance-register change occurred.

The retained header correction is a local candidate. Production support and acceptance remain unproved. The measurements below describe the fresh 2026-10-29 execution A after the review corrections. All 54 boundary probes and the actual SDK response case pass the stronger auditor; the earlier execution D remains retained historical evidence.

The [original transport packet](transport-profile.json) remains historical `ProposedUninstalled` evidence, unchanged. It was not relabelled. The [termination protocol](termination-protocol.md) and its real implementation prerequisites remain open.

## Exact executed artifacts

The persistent build root is `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-local-20261029-build-f`; the audited local execution is the fresh sibling `f1-local-20261029-execution-a`. `sealed-build.json`, `runtime-source-tree-sha256.json`, dependency-origin records, complete commands, opened executable image hashes tied to owned PID/start/terminal observations, lockfiles and configurations are retained there. The execution's `sha256.json` and `audit.json` bind all observations. Earlier attempts and failures remain retained; only the new execution A supports this packet.

* Runtime: exact Dapr `4cef924a0b1360c69d810deec8cd59bf853f6721`, Go 1.26.4, `stablecomponents`, components-contrib `72e6a52340620aa41c4deecc40cbc6f0007b9afe`, grpc-go 1.80.0. All 4,185 source files match the pinned archive except the retained local candidate one-line header correction. Runtime SHA-256: `241b88af7f853b388f01084d0b83591bf25590f944ee5a566146d8ce1c02577c`.
* Test-only fixture: SHA-256 `d12de8c763b9ea6665a61119bf08bddd290d93774d2199187ba557e4b3c717a3`. Imported generated protocol files are hashed, retained bindings/common source bytes match the pinned archive, and all 16 message schemas and both service schemas match their imported descriptors. Reflection discovery and the protected socket stem executed.
* Actual Dapr.Client 1.18.10 uses raw `BindingRequest.Data`, send cap 134,217,728, receive cap 1,048,575, empty service config and `MaxRetryAttempts=1`. The opt-in helper reuses the original fixture constructors in the existing test assembly. It adds no test case or production registration.

Go and .NET builds succeeded without compiler warnings. Runtime warnings about disabled mTLS, absent actors/scheduler and the absent application channel are retained; this loopback fixture is not a qualified host installation. Source/toolchain archive digests, independently checked public Go module content hash, exact patch/base/result hashes and generated/binary hashes are in the packet and retained independent reports.

The local source build leaves Dapr's injected release-version string `unknown`; its exact source revision, explicit build settings and binary digest identify this artifact.

The .NET source is the detached retained F1 revision `642b5bdbdf33ec1588eeef3b9a7e9f1a02aebc40` at `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-local-20261029-platform`, with the reviewed transport tooling and test helper overlaid. Every production/test .NET source from the original verifier snapshot matches except that helper. Later unrelated Platform qualification code requires APIs beyond the frozen EventStore pin; it is outside this historical F1 gate. Both dependencies have fresh clean detached checkouts at the exact required revisions. A parent `references/Hexalith.Builds` link supplies the pinned EventStore build layout without initializing nested submodules. The retained failed attempts document revision mismatch, Go environment normalization, dependency layout and the later-source API mismatch; none supports this packet.

## Whole requests and sufficient bounds

| Retained owner request | JSON bytes | API protobuf / wire bytes | Component protobuf / wire bytes | SDK calls / sink arrivals |
|---|---:|---:|---:|---:|
| Escaped 1,000-target manifest | 24,692,881 | 24,692,931 / 24,692,936 | 24,693,271 / 24,693,276 | 1 / 1 |
| Maximum 32 MiB transition | 44,740,912 | 44,740,962 / 44,740,967 | 44,741,304 / 44,741,309 | 1 / 1 |

JSON/API hashes are exactly the retained profile hashes: manifest `72D99146…EC5DBF` / `50AB9B28…19CACCB`; transition `0EFF8B31…0490D6C` / `B37D39DF…D9DDBC38`. Full hashes and independently measured component frame hashes are retained in the machine packet. Every hop and sink sees the complete identical payload. No target splitting occurs. Component frame hashes are measurements of this run, including its generated tracing metadata; they are not promised to repeat across runs.

The universal sufficiency argument remains field-derived, conditional on the complete closed schema and enforced private metadata profile. It is not an exhaustive finite witness claim:

* Manifest: `1000*(3*2048*6+256) + 128*4096*6 + 4*ceil((128*4096*6)/3) + 1048576 = 45508608` data bytes.
* Transition: `4*ceil(33554432/3) + 128*4096*6 + 1048576 = 48933548` data bytes.
* Both fit the unchanged private 67,108,864-byte data cap. At that cap, the deliberately loose full API/component protobuf bounds are 67,181,835 / 67,175,688 bytes, including the entire 66,560-byte forwarded metadata reserve; both fit 134,217,728 bytes. No branch combines a manifest with a transition. Public owner bounds and canonical bytes/signatures are unchanged.

Opaque zero-filled probes exercise the sufficient manifest/transition sizes and data cap through the real SDK and downstream transport. These are size witnesses, not valid semantic manifests. Data at 67,108,863 and 67,108,864 reaches the sink; 67,108,865 is refused by the component before the sink. SDK protobuf probes at 134,217,727 and 134,217,728 produce complete API frames, then remain refused/Unknown above the data budget; 134,217,729 is rejected at SDK send. Separate negative Go probes deliberately permit a larger send to isolate the API/component receive ceilings: both accept at/below 128 MiB at their transport boundary and reject one byte above it. Direct component unknown-field padding is separately refused after transport admission. An API receive flag is not mistaken for the component client's receive/send configuration.

## Headers, metadata and complete responses

Independent HTTP/2 frame observers decode HPACK and count every header as `name bytes + value bytes + 32`. Raw probes ignore peer SETTINGS only for the deliberate over-limit case. At both API and component servers, 65,535 and 65,536 decoded bytes pass the header boundary; 65,537 receives `RST_STREAM FRAME_SIZE_ERROR` before component admission. Advertised SETTINGS is 65,536. API probes within the header limit can still be refused by the smaller metadata field budget; that is a distinct hop. A second independently written Python HTTP/2 probe reproduced the API boundary against the same runtime digest without using fixture observer/probe code. Its source, report, runtime log and owned cleanup are retained under the build root's `independent/` directory.

The test-only component enforces the complete received post-sanitization map before the sink: 64 entries, 256/4,096 UTF-8 bytes per key/value and 65,536 aggregate key/value bytes. This includes generated trace fields and forwarded incoming headers. SDK witness maps had six entries and 329/331 aggregate bytes despite empty API protobuf maps. Boundary and one-over probes cover every field/count/aggregate constraint both directly and through daprd; API probes adjust for the independently measured generated/forwarded baseline rather than assuming it absent. A conservative 16 protobuf overhead bytes per entry yields the 66,560-byte reserve.

Full component responses include data, metadata and content type before `proto.Size(response)+5 < 1048576` is enforced. Actual independent component frames at 1,048,575 bytes pass, including an actual Dapr.Client receive of 1,048,526 data bytes with matching component/API payload hashes; guards at 1,048,576 and 1,048,577 refuse before send. An actual unknown protobuf field is included in the rejected size/unknown measurement. Compressed-response selection is refused, no component compressor is registered, and every observed full response is uncompressed. The SDK-visible API response omits component content type, so its reconstructed size is not substituted for the independently measured complete component frame.

Stock pinned daprd accepts/decompresses gzip **API requests**. The configured SDK path sends uncompressed requests, and the component rejects actual gzip requests before admission. The packet preserves this measured distinction; it claims no API ingress compression prohibition or production enforcement seam.

## Faults, retries and isolation

| Synthetic original fault | Sink send attempts | Recovery | Original producer / recovery component |
|---|---:|---|---|
| Cancellation after independently observed admission | 1 | Status only; original digest/count returned | component-1 / component-1 |
| Delayed duplicate after cancellation | 2 | Status only; same original digest/identity | component-1 / component-1 |
| Lost result after sink arrival | 1 | Status only; original digest/count returned | component-1 / component-1 |
| Component/UDS reconnect | 1 | Status only after replacement | component-1 / component-2 |

Each original has one actual SDK call. The late duplicate is intentional fault injection; it is not hidden by a one-call claim. Exact original identities, payload hashes, monotonic attempt counts, cancellation times, SDK/channel/process identities and both socket/process incarnations remain visible. Successful status lookup returns the original producer and count; it never resends the original as a new identity. Cancellation/lost results remain Unknown. There is no exactly-once or physical-completion claim.

The loaded binding policy explicitly sets zero outbound retries; SDK, stock pluggable-client source and synthetic HTTP POST behavior are retained separately. Transparent/late transport duplicates are not excluded. The sink's digest/count map is in process memory. It is not durable admission, SendCommitted, inventory, lineage, sealed queues, capacity reservation or a semantic recovery implementation.

Fresh private socket directories use `0700`, sockets `0600`, and API listeners use loopback. Every owned process was stopped. Same-UID processes can still access these paths; no application-excluded mount, restricted production identity or accepted host/termination/sanitation boundary is proved. Historical acquisition failures remain retained. This run keeps its complete pinned archives, module/compiler caches, sources, final binaries, configurations and evidence under fresh persistent paths.

## Reproduction and next qualification

The [test-only tooling](../../../eng/ext-secrets-1/local-transport/README.md) provides hash-pinned detached build, source sealing, run and audit commands. Audit execution A with `eng/ext-secrets-1/audit_local_transport.py --build-root /home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-local-20261029-build-f --evidence /home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-local-20261029-execution-a --packet docs/implementation/ext-secrets-1-f1-2026-10-20/local-transport.json`. The complete fresh F1 verifier output is retained at `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-local-20261029-verification-c`; the complete Python suite log is `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-local-20261029-python-c.log`. Their reports are the authoritative validation record. The existing 238-case Runtime contract and historical proposed-packet audit passed. The complete Python suite passes 472 tests; the Go fixture regressions pass as well. Eleven additional mutations of actual retained evidence are refused, including changed runtime resources/limits/endpoints, unmeasured request sizes, deleted accepted sink arrivals and substituted original payloads; the hashed independent inventory retains that adversarial report. Retained XML and the complete discovered Python test inventory are independently rechecked by the packet auditor.

The [next qualification template](next-qualification.template.json) binds this exact transport packet/artifacts/evidence and leaves the actual semantic component, backend atomicity/retention/current authority, production identity/isolation, durable admission/lineage/seals/capacity, accepted termination/sanitation profile and independent verifier, physical all-copy/shared-cut/hold/survivor/restore proofs, and exact native resource/cost/cleanup authorization explicitly null. See [F2/F3 prerequisites](f2-f3-prerequisites.md). None of these synthetic fixtures opens a later qualification gate.
