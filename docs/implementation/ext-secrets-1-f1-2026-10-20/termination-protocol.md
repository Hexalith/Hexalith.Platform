# F1 termination and sanitation prerequisite protocol

This packet defines required behavior for an implementation that does not exist. The [machine packet](termination-protocol.json) contains value-free synthetic identities and a closed-queue **model witness**, not installed inventory, an accepted termination profile or physical proof. No installed independent verifier is available. Therefore completion permission, key delivery, dependency availability and Live readiness remain false. Existing `CustodyBindingIntentRules` validate supplied booleans only; they do not implement this journal or prove those facts.

## Durable admission and copy lineage

Before any key-bearing byte leaves a holder, independently durable admission must bind the original tenant/operation/request digest, native IntentId, original root/generation, source/destination generation, KeyUseLeaseId, DeliveryAttemptId and exact authority incarnation. Add monotonically increasing admission sequence and inventory high-watermark, exact producer/recipient process incarnation, SDK/channel/sidecar/component/native transport incarnation, host/boot incarnation, artifact/profile/configuration hashes, copy parent/lineage, byte class/count bound, location/owner, queue identity and value-free evidence references. Do not persist key bytes, body traces or raw responses. Signed/private current authorization and a native expected-head comparison remain separate requirements. The journal and retained originals/public witnesses survive outside the termination boundary.

Capacity is reserved **before admission** for all admitted and potentially delayed copies: full request/checkpoint/original retention, native intents, holders, queue seals, termination/sanitation and independent evidence. Refuse new admission if finite measured capacity is unavailable; never drop old entries, evict originals or relabel an outstanding old process to make room. Count limits and the synthetic witness prove no capacity. No numeric retention or completion SLA is invented.

The minimum holder classes are:

| Class | Accounted copies and closure scope |
|---|---|
| caller-owned | Input/result/consumer arrays, serializer pools, every downstream recipient and original root use |
| sdk-protobuf | Dapr.Client ReadOnlyMemory, immutable ByteString copies, encoded request and response storage |
| grpc-dotnet | HTTP/2 serialization, compression if any, retry/replay buffers, channel send/receive queues |
| daprd-grpc-go | API decode, forwarded metadata, serialization, pooled buffers, binding/client retry and old sidecar instances |
| component | Generated protobuf, lifecycle implementation, queues, checkpoints, workers and every replacement incarnation |
| native-sdk-tls | Provider client request/result/TLS buffers, native intents and opaque uncertain source/destination generations |
| uds-kernel | Current and old UDS/TCP sockets, kernel buffers, pending sends/receives and DMA/transport copies where applicable |
| host-memory | Every process/host/VM/boot, RAM, allocator pages, swap/hibernation/core/snapshots and hypervisor copies |
| diagnostics | Logs, traces, exceptions, crash dumps, diagnostic agents, support/memory capture and their retained backups |
| delayed-queues | Retry, broker, scheduler, DLQ, offline producer and network delivery paths that can later emit original bytes |
| provider-internal | Registered service/operator/replica/recovery copies under independently accepted provider assurance |

An actual inventory may contain many entries per class and host. Adding a class never excuses the copy-level inventory. Unexpected copies become durable uncertainty and block release/closure. The synthetic packet retains both sidecar incarnations 1 and 2; replacement does not remove incarnation 1.

## Ordered completion states

| Transition | Mandatory durable evidence and refusal rule |
|---|---|
| Admitted → SendCommitted | Persist the exact original and complete lease/copy admission, then atomically commit one send attempt before sending. Uncertain send remains committed. No caller resend or new identity after restart. |
| SendCommitted → UseStopped | Current authority fences future key release and consumers stop use; retain original result status. A cancelled caller, expired lease or revoked credential alone is insufficient. |
| UseStopped → BoundarySealed | Seal every producer and queue with exact incarnation and last-admitted sequence. Reject new admission. Include SDK retry, sidecar, component/native, UDS/kernel and delayed-delivery queues. Record the exact boundary digest and inventory watermark. Missing/offline queues remain unsealed. |
| BoundarySealed → QuiescenceObserved | Independent observer reconciles every inventory entry and all queue sealed-through/observed-through watermarks. Observe no in-flight send, pending receive, scheduled/delayed delivery or unregistered descendant. A replacement cannot attest the old incarnation. Lost acknowledgement or inventory gap retains Unknown. |
| QuiescenceObserved → TerminationObserved | Independently witness the accepted isolation/termination action for each **original** process/transport/host incarnation. Prove no late producer/queue can revive or deliver the material. Keep the authority, originals and witnesses outside that action. |
| TerminationObserved → SanitizationVerified | Independently verify the accepted memory/media/provider sanitation profile for every registered copy, including kernel/host/hypervisor/diagnostic paths. Process death, socket close and pool return do not prove this step. No undocumented trusted-runtime assumption substitutes for accepted evidence. |
| SanitizationVerified → Closed | Installed independent verifier authenticates all preceding originals, complete inventory/watermarks, boundary and evidence against current independent public trust, all-copy closure and survivor/restore requirements. Only then may the separately qualified component close its original intent/lease. B3/B5 retain their own full physical/hold/survivor/restore gates. |

For buffers with a tested instrumented wipe primitive, evidence must prove exact buffer ownership, zeroing and quiescence for all descendants. For opaque framework/kernel/provider buffers, supply a separately accepted exact isolation/termination/sanitation profile. This may require an entire host/VM boundary and provider assurance; container restart alone cannot establish it. Identify exact supported action, observable postcondition, responsible owner, artifact/configuration and independent verifier. No such installed primitive/profile, action authorization or measured implementation is provided here.

## Recovery, verification and evidence

Cancellation, timeout, lost acknowledgement, process/sidecar/component restart, UDS re-registration and duplicate delivery preserve the original SendCommitted intent, old incarnations, copy lineage and inventory watermarks. Recovery reconciles status/lookup only; it cannot resend, replay cached key bytes, replace an authority, clear the old journal or reissue a root. A delayed holder appearing after sealing invalidates quiescence, reopens uncertainty under the same original, and blocks Closed and B3/B5 until fully reconciled. A tainted unknown staging generation cannot be adopted; its separately authorized physical retirement still needs independent proof. Missing current/adopted copy proof disqualifies completion.

The eventual signed value-free verifier input must bind protocol/profile revision, original request/intent/lease/delivery and authority, complete inventory and parent-lineage digest, exact process/transport/host incarnations, admitted/sealed/observed watermarks per queue, boundary digest, capacity reservation, ordered state evidence, termination and sanitation receipts, provider assurance, current independent trust and independently retained original/result references. Evidence signers and observation roles must be enrolled independently; the component cannot self-approve its own destruction. Application/observer access remains Dapr-only. Credentials, native endpoints and keys never enter the packet or application DI.

`eng/ext-secrets-1/check_f1_packets.py` rejects missing holder classes, dropped/replaced old incarnations, changed lineage/originals, stale watermarks, unsealed queues and synthetic availability/completion claims. Its positive result is **packet conformance only**. It cannot verify real signatures, hosts, termination, sanitation, native effects or physical erasure; it always reports completion permission false. The witness stops at synthetic QuiescenceObserved with termination/sanitation false. A fully populated synthetic witness still cannot pass a claim of Closed. Implement/install and independently qualify the genuine verifier before requesting permission to enable key delivery.

The next separately authorized packet must contain actual immutable participant/verifier artifacts, enrolled independent identities/trust, durable inventory/admission/seal implementation, measured capacity, exact supported termination/sanitation profile, effective diagnostics/host policies and independently observed duplicate/loss/restart/delay/old-host fault results. It must also contain complete backend all-copy/shared-cut/survivor/restore proof and explicit native resource/cost/cleanup authorization. Until those exist, EXT-SECRETS-1 stays Committed/unavailable and every key-bearing path stays refused.
