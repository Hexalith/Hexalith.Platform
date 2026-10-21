# Synthetic local F1 transport gate

This directory builds an exact detached Dapr 1.18.1 runtime with the retained one-line header-units correction and a test-only Go OutputBinding, HTTP sink, HTTP/2 observers and negative probe driver. It provides no production provider, semantic custody authority, backend, credential, native operation or completion verifier. All material is synthetic. The production invoker stays closed.

`build.py` requires fresh external output, downloads hash-pinned Dapr and Go 1.26.4 archives, verifies retained protocol/source equality, and builds with the explicit `stablecomponents` tag. `source/source-inputs.json` is emitted by the builder and consumed by the auditor. Inherited Go overrides are removed; `GOENV=off`, `GOWORK=off`, empty `GOFLAGS`, local toolchain selection and public checksum verification are explicit. `go-build-environment.json` retains the effective settings and is sealed. Dependencies remain pinned by the runtime's exact `go.mod`/`go.sum` and the fixture's retained lockfiles. No submodule initialization occurs. Use the clean dependency roots and revisions in the transport profile:

```bash
python3 eng/ext-secrets-1/local-transport/build.py \
  --output /absolute/fresh/f1-build \
  --builds-root /home/administrator/projects/hexalith/agents/references/Hexalith.Builds \
  --eventstore-root /home/administrator/projects/hexalith/agents/references/Hexalith.EventStore
python3 eng/ext-secrets-1/local-transport/seal_build.py \
  --build-root /absolute/fresh/f1-build \
  --assembly /absolute/fresh/f1-build/dotnet-artifacts/bin/Hexalith.Platform.Secrets.Runtime.Tests/debug/Hexalith.Platform.Secrets.Runtime.Tests.dll
python3 eng/ext-secrets-1/local-transport/run.py \
  --build-root /absolute/fresh/f1-build \
  --assembly /absolute/fresh/f1-build/dotnet-artifacts/bin/Hexalith.Platform.Secrets.Runtime.Tests/debug/Hexalith.Platform.Secrets.Runtime.Tests.dll \
  --output /absolute/fresh/f1-execution
python3 eng/ext-secrets-1/audit_local_transport.py \
  --build-root /absolute/fresh/f1-build --evidence /absolute/fresh/f1-execution
```

The .NET assembly remains the existing 238-test assembly. Its existing evidence producer calls an opt-in helper only when `EXT_SECRETS_1_LOCAL_TRANSPORT` names the generated local configuration. The helper invokes actual Dapr.Client 1.18.10 with raw `BindingRequest.Data`, bounded channel settings and no configured retry policy. It reuses the original manifest/transition fixture construction. Opaque zero-filled boundary probes are explicitly separate from owner-legal manifests. The original verifier's historical handler-only evidence and proposed packets are preserved.

The runtime receives `--max-body-size 128Mi --read-buffer-size 64Ki`. A scoped outbound resiliency policy has zero retries. The pluggable component uses the imported pinned generated types, reflection discovery and a protected socket stem. It independently enforces the 64 MiB data budget, the complete received metadata map, recursive raw-wire unknown-field rejection before protobuf map decoding can discard entry fields, and `proto.Size(response)+5 < 1 MiB`, including response content type and metadata. No component gRPC compressor/decompressor is registered. Actual gzip request probes measure stock API acceptance/decompression and component refusal separately; compressed-response selection is refused before send. The configured SDK path is uncompressed.

Observers relay HTTP/2 frames on the actual loopback API and UDS component connections, preserve HPACK state and count every decoded header with its 32-byte per-entry overhead. They hash complete independent protobuf frames and payloads, retain sizes and value-free metadata summaries, and never write request bodies or raw responses. They reject unsupported padded/priority header forms rather than silently altering them. Raw negative header probes deliberately ignore advertised SETTINGS to measure server rejection. Go negative protobuf probes allow a send size above 128 MiB to isolate the receiver's ceiling; the SDK has its own independent at/below/above probes.

Every process has its own retained PID/start time/command and incarnation. The runner hashes an opened executable FD and executes that same inode, including short-lived descriptors/probes. It retains the FD through the owned child wait and records its terminal hash/inode; it does not race `/proc/PID/exe` after exit. The SDK seal covers every app-local file, including Runtime/EventStore/gRPC assemblies, apphost, deps and runtimeconfig. Cleanup stops only the runner's process objects. Sockets use a fresh short private `/tmp` directory, directories `0700` and sockets `0600`. A same-UID application can still access these paths; no app-excluded mount, process identity isolation, accepted host sanitation or production isolation is proved. Replacement preserves the old incarnation in observations. The synthetic sink keeps attempt counts only in its running memory; this is transport fault instrumentation, not a durable semantic admission implementation.

When long public TLS downloads stall, `range_download.fetch` can retain validated 1 MiB HTTP ranges and assemble an unchanged module archive. Retain all failed attempts and range manifests, then require `go mod download` to authenticate that archive against pinned `go.sum` before building. It grants no exemption from dependency checks.

Run the complete F1 verifier separately with fresh external artifacts/evidence, and run `python3 -m unittest discover -s eng/ext-secrets-1 -p 'test_*.py' -v`. The executed audit is additional to `check_f1_packets.py`; it never relabels historical proposed packets or opens wrap/unwrap, dependency availability, Live, key delivery, completion or physical authority.

Review correction verification is intentionally focused:

```bash
python3 -m unittest discover -s eng/ext-secrets-1 -p test_audit_local_transport.py -v
# In a detached fixture source copy, with github.com/dapr/dapr replaced by the
# exact pinned runtime source and the pinned Go toolchain/cache:
go test -mod=readonly -run 'Test(NestedMapUnknown|ObserverNested|ConsumedStream)' -v .
```

The updated runner records 54 named boundary probes, including nested-map unknown requests at both hops and encoded nested-map response refusals. Stock daprd discards unknown API request fields before forwarding; that behavior is measured explicitly. The fixture component refuses raw unknown fields before decode. Focused regression tests additionally send malformed request and response bytes through real HTTP/2 frames to verify the independent observer detects nested unknowns.

Keep previous build/execution roots immutable and use fresh roots for the commands above. After the parent completes the separate full verifier and Python suite, publishing a replacement packet additionally requires `verification.f1ReportSha256`, `verification.f1AssemblySha256` and `verification.pythonSuiteSha256`, and `build.goBuildEnvironmentSha256`. Bind the verifier report, its actual detached assembly, and the retained Python log; update all measurements/configuration hashes from the new audit. Retain and hash the independent report inventory under the new build root; if the runtime digest changes, independently remeasure the new runtime. Update the next template last so its packet/runtime/fixture/evidence bindings match exactly. The historical packet and execution D do not satisfy the stronger new auditor and remain retained historical evidence.
