# Offline Story 4.0 packet verification

`verify_packet.py` reads local files and hashes only. It has no operational mode,
imports no legacy tool, authenticates no approval, and always reports
`operationalGate: closed`. Exit 0 establishes consistent unsigned preparation;
it establishes neither current recovery nor permission to measure or execute.

Use the packet path and independently retained `packet.json` SHA-256 from the
specific sanitized preparation receipt:

```text
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/verify_packet.py --packet <private-RID-directory> --sha256 <reviewed-packet-sha256>
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

There are no generic live adapters. Frozen sources are `.txt` files only and
must remain reference-only. Old policy/preflight/dry/selftest entry points can
have side effects; their names do not make them safe to run. Current source,
provider, authority, image and capacity measurements require the packet's exact
next approval, followed by revised reviewed execution/signing approval.
