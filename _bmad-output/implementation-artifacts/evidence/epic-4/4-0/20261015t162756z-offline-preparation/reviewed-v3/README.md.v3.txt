# Offline Story 4.0 packet verification

`verify_packet.py` reads local files and hashes only. It has no operational mode,
imports no legacy tool, authenticates no approval, and always reports
`operationalGate: closed`. Exit 0 establishes consistent unsigned preparation;
it establishes neither current recovery nor permission to measure or execute.

Use the packet path and independently retained `packet.json` SHA-256 from the
specific sanitized preparation receipt:

```text
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/verify_packet.py --packet <private-RID-directory> --manifest packet-v3.json --sha256 <reviewed-packet-v3-sha256>
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_verify_packet.py
```

The verifier requires exact create-only private layout, directories 0700/files
0600, no symlinks or path escape, an exact checksum inventory, unchanged original
and frozen historical sources, current local binary hashes, retained policy
decisions, attempt-scoped proposed targets, named null measurements and closed
operational stages. Historical observations and acceptance stay historical.
Originals, public trust bytes and binaries are hashed without running them.
The system Docker file is measured through its canonical non-symlink path, with
its group-writable custody explicitly blocked for execution.

The packet's checksum is an integrity anchor supplied by the reviewer, not an
authentication mechanism. The verifier does not verify cryptography, inspect a
provider, load authentication credentials, decrypt material, issue approval,
create infrastructure, sign, publish, or execute a stage. Preparation remains
structurally valid after its candidate policy expires; the policy's unchanged
24-hour ceiling remains binding for any later separately approved use.

The explicit `packet-v3.json` revision is the active review input for RID
`20261015t162756z`. Its `README-v3.md`, `bindings-v3.json`, bound
`operation-plan-v3.json`, `NEXT-OPERATIONAL-APPROVAL-v3.md` and `SHA256SUMS-v3`
preserve all v1/v2 files and validate their complete original payload maps.
There is no latest-revision discovery or automatic fallback. The original
policy, RID and preparation/expiry times are unchanged. Every semantic read is
checked against its inventoried digest; the complete active layout, custody and
hash inventory is rechecked before success. Concrete historical expectations
are reconstructed from inventoried original source records and must match.

The immediate proposal is only an Administrator-run protected local
`static_transport_preflight.py` check of an exact fixed config digest,
context/endpoint/TLS/static credential schema. It uses only a bound local
`kubectl config view --raw -o json` over a sealed mode-0600 in-memory input and
cleared child environment. No raw output is emitted. Preparation must not run
it on the actual config. Its synthetic tests read only synthetic input.
API/provider/IAM lanes remain BLOCKED until static transport, exact executors,
runtime and authority are separately qualified and approved. Future selectors
are inert reference in `FUTURE-MEASUREMENTS-v3.md`; no approval is inherited.

There are no generic live adapters. Frozen sources are `.txt` files only and
must remain reference-only. Old policy/preflight/dry/selftest entry points can
have side effects; their names do not make them safe to run. Current source,
provider, authority, image and capacity measurements require the packet's exact
next approval, followed by revised reviewed execution/signing approval.
