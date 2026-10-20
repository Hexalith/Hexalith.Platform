# Retained primary-source F1 research

The complete immutable public-source bundle is retained at `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-20261020/research/`. It contains 32 downloaded primary artifacts, exact commit URLs/SHA-256 in `sources.json`, candidate validator bytes, an untruncated component inventory, `offline-check.py`, counterexamples and `SHA256SUMS`. The repository retains the structured findings, source manifest, counterexamples and exact imported protocol; no generated runtime/component artifact is implied.

Reproduce the source/hash and lower-bound byte witnesses without infrastructure access:

```bash
cd /home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-20261020/research
sha256sum --check SHA256SUMS
python3 offline-check.py
```

`collect.py` can refetch the public GitHub source URLs. It does not contact the accepted infrastructure host, read workload credentials or run native effects. The retained checks are source/encoding evidence; they do not run a Dapr sidecar or physical component.

Pins: Dapr v1.18.1 `4cef924a0b1360c69d810deec8cd59bf853f6721`; Dapr .NET SDK v1.18.10 `f3e09addc0572e40175f9683f4af5ddc40981ea5`; its runtime components-contrib v1.18.0 `72e6a52340620aa41c4deecc40cbc6f0007b9afe`; grpc-go v1.80.0 `397e45edaa68f8763773bbaaf539cf7894169cd2`. Full source identities, including protobuf and grpc-dotnet dependencies, are in the manifest.

The 4,284,001-byte witness is an illustrative vector of complete CustodyKeyObjectIdentity records with short tenant and control fields and two maximum 2048-byte identity fields. It is not the actual closed manifest DTO encoding, which uses existing ProtectionTarget triples. The decisive actual manifest request counterexamples come from the C# producer; those preserve the exact source request schema and measure its complete envelope and protobuf framing against the 4,194,304-byte default. The C# conformance producer separately measures the entire real request encoding, escaped-field lanes and SDK protobuf framing. Neither measurement authorizes a larger deployed limit or target splitting.
