# F1 candidate whole-request transport profile

This is a pinned configuration/change packet for a missing implementation. It is **ProposedUninstalled**. `DependencyAvailable=false`, `LiveReady=false`, `KeyDeliveryEnabled=false` and `CompletionPermission=false`. No sidecar, UDS, semantic component, backend or physical path was executed. The production invoker still refuses above its 4 MiB default and refuses wrap/unwrap even when its supplied SDK client has larger channel settings. The [machine packet](transport-profile.json) and [termination packet](termination-protocol.json) grant no authority.

## Complete request bounds

The original owner identities, ordering, JSON request digests, canonical signed payloads, signatures and public bounds are unchanged. Private `Proof.HoldId`, `DispatchReceiptId` and `ProtectionReservationReceiptId` now require null or valid nonblank text of at most 2,048 UTF-16 units. This closes the previously unbounded private HoldId without restricting an owner field. Other proof/enrollment/control fields retain their existing private bounds. Encode and Decode both enforce the separate 64 MiB private request cap.

The following deliberately loose bounds account for the entire closed DTO, including private proof and Base64 fields:

* Manifest target data: `1000 * (3 * 2048 * 6 + 256)` bytes. Count all three owner target strings at the broader 2,048 UTF-16-unit limit, even though owner Capture imposes stricter UTF-8/tenant rules. The extra 256 bytes per target exceeds its JSON property/punctuation overhead.
* Every non-target textual occurrence together: at most `128 * 4096 * 6` bytes. The union's manifest/envelope/proof/enrollment has fewer than 128 such occurrences; 4,096 is the largest permitted private text bound. This over-counts short fixed strings and digests. The separately carried signed canonical capability cannot exceed that same bound before Base64, so add `4 * ceil((128 * 4096 * 6) / 3)`.
* Add 1 MiB for detached JWS (at most `16384 * 6` bytes), the 8,192-byte signature's Base64, numeric tokens, branch/property names, nulls and punctuation. These are far below that allowance. Thus the manifest data bound is **45,508,608 bytes**.
* A transition has at most 33,554,432 original target bytes. Its Base64 plus the same non-target text and 1 MiB structural allowance is **48,933,548 bytes**. The inherited 32 MiB pending-state limit is unchanged. Registration/lifecycle/page/current branches are smaller under their existing closed field bounds; no branch combines a transition and manifest.

Both complete data bounds fit 67,108,864 bytes. These are conservative sufficient bounds, not tight maxima, allocation/capacity proof, or a way to accept malformed proofs. There is no claim that merely imposing the decode cap repairs an unbounded schema.

The real pinned Dapr.Client serialization with a local `HttpMessageHandler` records:

| Synthetic original | JSON bytes | API protobuf bytes | gRPC bytes including five-byte prefix | SDK calls |
|---|---:|---:|---:|---:|
| Retained escaped 1,000-target owner request | 24,692,881 | 24,692,931 | 24,692,936 | 1 |
| 32 MiB transition | 44,740,912 | 44,740,962 | 44,740,967 | 1 |

The retained manifest JSON SHA-256 is `72D99146B808B66DBA54DE3D336E284E5F37E5E9D8605A8ABE85640F70EC5DBF`; its actual API protobuf SHA-256 is `50AB9B289AB78F2A6E2B70C77D6153E46D50A15A6F115DE685AFE377419CACCB`. Transition hashes are `0EFF8B317B90E1ED456DF4DDE1C8F7D229D8186639F47EBEE9DEFC0E50490D6C` and `B37D39DFDC6E5F9FB0CCE40B89CB9EE814F1E8D97124271067EFF5DBD9DDBC38`. The verifier requires these exact measurements, explicit channel settings and one SDK call per witness. A local handler proves serialization only; it cannot establish installed limits, retries, component semantics or physical erasure.

## Separate settings at every hop

Exact source revisions and per-file hashes are in [the retained manifest](research-source-manifest.json); the JSON packet binds that manifest's SHA-256. Dapr runtime is `4cef924a0b1360c69d810deec8cd59bf853f6721` (1.18.1), SDK is `f3e09addc0572e40175f9683f4af5ddc40981ea5` (1.18.10), grpc-go is `397e45edaa68f8763773bbaaf539cf7894169cd2` (1.80.0), and grpc-dotnet is `f22747c72676120b2bc69a1ba7c8bed995e29b1b` (2.80.0). Verification uses clean Agents sibling Builds `5d03f1db9c38db34ecac7fe6048937478ffb82cc` and EventStore `c9f47dd5ba72918445dfd24a674a5b2c310589bd`.

| Boundary | Exact candidate setting / requirement | Evidence outstanding |
|---|---|---|
| SDK → API | `GrpcChannelOptions { MaxSendMessageSize = 134217728, MaxReceiveMessageSize = 1048575 }`; raw `BindingRequest.Data`, no typed byte-array JSON wrapping | Executed only against local handler. Install the exact SDK/channel artifact and measure effective headers and duplicates. |
| daprd API | `--max-body-size 128Mi --read-buffer-size 64Ki` | API source independently sets `MaxRecvMsgSize` and `MaxSendMsgSize`. Effective deployment/artifact and boundary measurements absent. |
| daprd → component | Stock pinned pluggable client has default send ceiling MaxInt32; its default **receive** ceiling is 4 MiB | No stock sidecar change is required for the 128 MiB request alone. Do not confuse client receive and send. Keep full component responses below 1 MiB; do not infer that API flags configure this client. |
| Component gRPC server | Proposed Go constructor: `grpc.NewServer(grpc.MaxRecvMsgSize(134217728), grpc.MaxSendMsgSize(1048575), grpc.MaxHeaderListSize(65536))` | No component source/artifact/server exists. Equivalent implementation must pin actual generated/imported protos, reflection, socket registration, ACL and app-excluded UDS mount. |
| Full responses | Before send, require `proto.Size(response) + 5 < 1048576`, including content type, metadata and any unknown fields. Closed profile rejects unknown fields and compression. Pages carry ≤256 KiB original bytes. | SDK-visible reconstruction discards some fields; component/server and independent full-wire enforcement remain required. The invoker rejects all visible response frames ≥1 MiB. Never page key bytes. |

Incoming gRPC headers are copied/sanitized into component metadata by `InvokeBinding`. Count these independently from API protobuf metadata. Bound the **post-sanitization complete** map, including generated tracing metadata: at most 64 entries, 256 UTF-8 bytes per key, 4,096 per value and 65,536 aggregate key/value bytes. Add 16 map/tag/varint bytes per entry: **66,560 component protobuf bytes**. An empty API metadata map does not eliminate this reserve. The full API includes binding name, operation and data fields; the component includes operation, data and the complete forwarded map. At the 64 MiB data cap both fit 128 MiB with ample reserve. The binding name retains its existing 2,048-unit bound. The default invoker now also checks the conservative component size before SDK send. It cannot attest enforcement of headers in a real channel.

The pinned header option has a units discrepancy: options parsing yields `64Ki = 65,536` bytes, while `pkg/api/grpc/server.go` shifts `ReadBufferSize << 10` again, deriving **67,108,864** gRPC header-list bytes. This is a retained source-arithmetic measurement, **not an observed deployed header limit**. The proposed [one-line units patch](dapr-1.18.1-header-units.proposed.patch) removes that second shift. Its exact base source hash and patch hash are bound in JSON; it is unapplied and has no built artifact digest. It is a separate header-bound correction, not a request-size prerequisite. Approval/support for that artifact, boundary tests at 65,535/65,536/65,537 decoded header-list bytes, HTTP/2 entry overhead, and enforcement of the smaller post-sanitization metadata budget remain outstanding. An independently supported ingress/interceptor may enforce those budgets instead, but its exact artifact/change and tests must replace the proposed patch in a separately approved packet. No production path is activated here.

Disable configured application, binding resiliency/proxy/component and native SDK retries for key/copy/destructive sends and independently inspect effective policies. Transparent/late duplicates still require durable original admission and SendCommitted with status/lookup-only recovery. One handler invocation is not exactly-once delivery evidence.

## Backend and qualification requirements

The absent backend must retain complete immutable requests/checkpoints and complete originals of at least 64 MiB per object, or demonstrate a supported equivalent immutable representation without changing the atomic semantic request. Prewrite and verify the full bytes/hashes/signatures, then use one linearizable conditional head/index transition for the **entire** manifest. Declare actual item, object, action, transaction, index and concurrency limits. A 1,000-target manifest must never become per-target consumption commits. Current-head comparisons serialize holds, reservations, arm and original publication; the native shared physical cut still needs independent proof.

No arbitrary TTL is supplied: permanent operation/allocation exclusions, original signatures and every referenced outcome/checkpoint/backup need owner-required retention, extension margins and independent recovery. Measure/reserve request/checkpoint/original bytes, native intents, holders, queue and completion capacity before admission. Existing public 1,000-registration/10,000-retained-request and pending-state bounds remain fixed; count bounds alone do not prove fit. A 128 MiB channel does not establish backend transaction support or adequate memory.

Next packet must supply an immutable semantic component, actual backend primitives/limits/retention and independence, installed bounded transport/header measurements, every holder and sanitation proof under the [termination protocol](termination-protocol.md), an installed independent verifier, and separate exact native resource/cost/cleanup authorization. All remain unproved. Local packet conformance and SDK capture cannot make EXT-SECRETS-1 Available or authorize F0, F2/F3, Live, owner acceptance, deployment or physical destruction.
