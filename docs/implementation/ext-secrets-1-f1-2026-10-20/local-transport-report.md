# Executed synthetic local F1 transport gate

The local SDK → patched daprd → pluggable UDS OutputBinding → instrumented sink path executed successfully. The [new machine packet](local-transport.json) retains the exact measured requests, every-hop protobuf/payload digests, decoded headers, full responses, metadata, attempts and incarnations. This is synthetic transport evidence. `DependencyAvailable=false`, `LiveReady=false`, `KeyDeliveryEnabled=false`, `CompletionPermission=false` and `PhysicalEvidence=false`. Wrap/unwrap and the production invoker remain closed. No semantic component, backend, native effect, credential, deployment or acceptance-register change occurred.

The retained header correction is a local candidate. Production support and acceptance remain unproved. The measurements below describe retained execution D; the review corrections in the tooling require a fresh build, seal and execution before publishing replacement measurements.

The [original transport packet](transport-profile.json) remains historical `ProposedUninstalled` evidence, unchanged. It was not relabelled. The [termination protocol](termination-protocol.md) and its real implementation prerequisites remain open.

## Exact executed artifacts

The persistent build root is `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-local-20261020-a`; the audited local execution is the fresh sibling `f1-local-20261020-execution-d`. `sealed-build.json`, `runtime-source-tree-sha256.json`, dependency-origin records, complete commands, process image hashes from `/proc/PID/exe`, lockfiles and configurations are retained there. The execution's `sha256.json` and `audit.json` bind all observations. Earlier attempts and failures remain retained; only execution D supports this packet.

* Runtime: exact Dapr `4cef924a0b1360c69d810deec8cd59bf853f6721`, Go 1.26.4, `stablecomponents`, components-contrib `72e6a52340620aa41c4deecc40cbc6f0007b9afe`, grpc-go 1.80.0. All 4,185 source files match the pinned archive except the retained local candidate one-line header correction. Runtime SHA-256: `241b88af7f853b388f01084d0b83591bf25590f944ee5a566146d8ce1c02577c`.
* Test-only fixture: SHA-256 `17253dbca1caf71c7a8b92946388bccf7cee190ee800dc9e81ef707b88ac3d94`. Imported generated protocol files are hashed, retained bindings/common source bytes match the pinned archive, and all 16 message schemas and both service schemas match their imported descriptors. Reflection discovery and the protected socket stem executed.
* Actual Dapr.Client 1.18.10 uses raw `BindingRequest.Data`, send cap 134,217,728, receive cap 1,048,575, empty service config and `MaxRetryAttempts=1`. The opt-in helper reuses the original fixture constructors in the existing test assembly. It adds no test case or production registration.

Go and .NET builds succeeded without compiler warnings. Runtime warnings about disabled mTLS, absent actors/scheduler and the absent application channel are retained; this loopback fixture is not a qualified host installation. Source/toolchain archive digests, independently checked public Go module content hash, exact patch/base/result hashes and generated/binary hashes are in the packet and retained independent reports.

The local source build leaves Dapr's injected release-version string `unknown`; its exact source revision, explicit build settings and binary digest identify this artifact.

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

Full component responses include data, metadata and content type before `proto.Size(response)+5 < 1048576` is enforced. Actual independent component frames at 1,048,575 bytes pass; guards at 1,048,576 and 1,048,577 refuse before send. An actual unknown protobuf field is included in the rejected size/unknown measurement. Compressed-response selection is refused, no component compressor is registered, and every observed full response is uncompressed. The SDK-visible API response omits component content type, so its reconstructed size is not substituted for the independently measured complete component frame.

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

Fresh private socket directories use `0700`, sockets `0600`, and API listeners use loopback. Every owned process was stopped. Same-UID processes can still access these paths; no application-excluded mount, restricted production identity or accepted host/termination/sanitation boundary is proved. Shared root-disk exhaustion interrupted acquisition; owned module/compiler scratch was moved to `/dev/shm`. Persistent exact archives, source, final binaries, configurations, failures and evidence remain retained. Memory scratch is explicitly volatile and confers no retention assurance.

## Reproduction and next qualification

The [test-only tooling](../../../eng/ext-secrets-1/local-transport/README.md) provides hash-pinned detached build, source sealing, run and audit commands. Audit execution D with `eng/ext-secrets-1/audit_local_transport.py --build-root <retained-build-root> --evidence <retained-execution-d> --packet docs/implementation/ext-secrets-1-f1-2026-10-20/local-transport.json`. The complete fresh F1 verifier output is retained at `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-local-20261020-final-verification-b`; the complete Python suite log is `f1-local-20261020-a/final-python-suite.log`. Their reports are the authoritative validation record. The existing 238-case Runtime contract and historical proposed-packet audit remain required.

The [next qualification template](next-qualification.template.json) binds this exact transport packet/artifacts/evidence and leaves the actual semantic component, backend atomicity/retention/current authority, production identity/isolation, durable admission/lineage/seals/capacity, accepted termination/sanitation profile and independent verifier, physical all-copy/shared-cut/hold/survivor/restore proofs, and exact native resource/cost/cleanup authorization explicitly null. See [F2/F3 prerequisites](f2-f3-prerequisites.md). None of these synthetic fixtures opens a later qualification gate.
