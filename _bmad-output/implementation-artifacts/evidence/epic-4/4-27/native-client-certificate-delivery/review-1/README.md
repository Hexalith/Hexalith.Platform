# Review-1 native client-certificate re-derivation

`verification.json` binds the corrected sources and seven exact commands in
`command-results.json`: 305 passing tests, zero skips. Logs retain the complete
results; `sources/` holds the four reviewed Python snapshots. All initial 297
cases and original test names remain covered; eight focused tests add ownership,
PEM, traditional-key and chain/count regressions. `matrix-coverage.md` maps the
contract and review corrections to the checks.

The parent `verification.json`, original `sources/`, `first-candidate/`, original
receipts and handoffs are historical candidates. They were not replaced or
modified. `initial-packet-sha256.json` records all original packet files before
this re-derivation. `handoff-prefixes.json` binds the full existing handoffs before
append-only updates. `preservation.json` and `preservation.log` record the explicit
checks made by the retained `verify-preservation.py` invocation from repository
root. Claims are limited to its checked scope; unrelated workspace edits remain
outside this packet.

`spec-dispatch-baseline.md` is the full review dispatch input;
`spec-verification-checkpoint.md` is the spec state bound by this receipt. The
original implementation receipt's spec hash is reproducible from the unchanged
parent `spec-implementation-checkpoint.md`. These are separate checkpoints and
do not reinterpret the original receipt as qualification of new code.

`original-parent-cancellation-probe.txt` retains the exact original parent
invocation byte-for-byte. That invocation targeted the initial helper's cleanup
line and is archival; its old source-line selector does not apply to the new
structure. `parent-probes.json` records the parent's reported original and current
observations. Current reproducibility is supplied by the allocator/registration/
close/primary-interrupt regressions in this exact command:

```bash
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_native_client_certificate.py
```

Every certificate and key is public synthetic fixture material. Newly embedded
synthetic root/intermediate and client material was prepared offline; temporary
issuer keys were discarded. The suite uses static fixtures, actual SSLContext
loading and MemoryBIO, forbids sockets, and acquires no real credentials or
configuration. No operational certificate was issued, no recovery document was
signed and no historical executor was replayed. No attempt/policy allocation,
upload, recovery or infrastructure mutation occurred.

Actual credential/custody/IAM/authority/client identity, live connector/TLS/runtime,
system/store/isolated-target/proof-consumer and actual recovery qualification
remain pending. Fixtures grant no authority. Public activation remains disabled;
4.27 stays in-progress, 4.1 closed and Timesheets 3.6 open/FAIL.
