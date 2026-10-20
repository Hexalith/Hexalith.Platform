# F1 buffer accounting and release gate

This is an implementation prerequisite and proof strategy, not a claim that the pinned stack closes its buffers. No real key bytes were used. Local disposal of an owned application buffer cannot attest completion of another process, kernel socket, protobuf object or provider request.

## Exact delivery inventory

Before release, the independently governed component must durably admit the original operation/intent and a unique KeyUseLeaseId/DeliveryAttemptId for the exact recipient process and transport incarnation. The inventory records each actual producer, location, owner, lineage, generation, retention, copy outcome and completion proof. Every lower-level retry or delayed delivery is another holder under that admission, even when the application made one call. Disable configured automatic retries and still tolerate duplicates. Init/re-registration and application or sidecar restart preserve every unresolved old holder.

| Hop / copy | Concrete accounting requirement | Required completion evidence |
|---|---|---|
| Caller input and owned result | Mutable bounded owned storage; avoid raw keys in immutable strings, exceptions or evidence. Register application process/incarnation before receiving. | Authenticated lease-bound use completion and explicit zeroing of all owned buffers, or accepted exact process/host termination proof. |
| Dapr.Client / protobuf | Account for original ReadOnlyMemory, ByteString input copy, serialized frame and response-owned memory. The raw binding API preserves values but provides no secure buffer release contract. | An implemented audited adapter/runtime primitive that tracks and wipes every copy, or accepted isolation/termination of all relevant incarnations. Garbage collection/disposal alone is insufficient. |
| API channel, daprd, gRPC codecs and compression | Bind the actual sidecar incarnation and request attempt; enumerate serialization/decompression/pool copies and delayed receive/send. | Authenticated I/O quiescence plus buffer destruction; pool return and cancellation are insufficient. grpc-go may clear on later allocation, leaving dirty returned buffers meanwhile. |
| UDS and kernel queues | Restrict mounts/ACL to sidecar and component. Inventory pending sends/receives and OS/socket buffers, including old sockets after replacement. | Supported kernel/runtime completion or accepted termination boundary covering the old process and host resources. Socket close alone is not a demonstrated wipe. |
| Component / native SDK / TLS | Commit Prepared → SendCommitted once before send; retain uncertain result and opaque generation identity. Enumerate serializer, SDK, TLS and provider response buffers; no automatic resend. | ResultDurable with value-free or gated encrypted metadata, actual I/O drain and accepted zeroing/termination before Closed. |
| Diagnostic / host copies | Disable content/body logging, content tracing, core dumps, swap, hibernation, memory snapshots and diagnostic captures in every participant. | Actual installed policy/artifact evidence and independent observation; configuration intent is insufficient. |

## Fail-closed rules

A duplicate after release admission or an exact lookup returns status/proof only. Never retain or page raw D/X/K/C0 to make retries byte identical. A fresh legitimate use of the original root requires a new currently authorized key-use lease, never a replacement root. Freeze/erasure/lease closure prevents release; lease expiry and credential revocation do not close old I/O. A lost acknowledgement keeps the original SendCommitted intent and holders Unknown. No caller lookup starts another native send.

Physical B3/B5 closure must include every outstanding holder, not merely successful calls. Missing or unverifiable completion blocks irreversible receipts. Independent verification must distinguish actual destruction from gateway denial, missing keys, scheduled deletion and disabled credentials.

## Exact missing implementation

The inspected Dapr.Client 1.18.10 and Dapr 1.18.1 output-binding protocol do not expose a lease-bound cross-process buffer inventory, per-copy secure release or authenticated completion barrier. A component cannot retrospectively attest another sidecar/SDK process simply because Invoke succeeded. No qualifying instrumented stack or accepted termination protocol is supplied. This blocks key-bearing production binding even if the message limit is changed.

A separately approved stack/component change must implement and test those primitives, or the responsible owners must provide an accepted termination protocol covering every exact SDK/sidecar/component/host incarnation and late-delivery queue. F2/F3 must independently test that implementation with synthetic material, including loss immediately after send and process replacement while old buffers remain. This work adds no new runtime authority and grants no waiver.
