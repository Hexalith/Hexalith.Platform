# Native client-certificate delivery: review-2 source receipt

This is unsigned offline fixture evidence. It confers no recovery, IAM, runtime,
client-identity or operational qualification. Story 4.27 remains in-progress,
4.1 closed and Timesheets 3.6 open/FAIL. Public activation remains disabled.

The four implementation sources were re-derived from the exact `../review-1/sources/`
305-test candidate. All 55 prior packet files, including review-1 and initial
snapshots/logs/checkpoints, are hash-inventoried and preserved. `sources/` binds
the final four implementation/test files separately. Three existing handoffs
were extended; their complete dispatch prefixes are independently hash-bound.
The frozen approved spec block and original parent baseline remain unchanged.

The seven commands in `command-results.json` passed 315 tests with zero skips:
39 certificate, 45 boundary, 12 immutable readback, 13 authentication, 42 S3,
94 common adapter and 70 telemetry. The focused command is:

```bash
/usr/bin/python3.14 -I -S -B eng/recovery-preparation/test_native_client_certificate.py
```

The 39 focused methods retain all 29 review-1 methods and all 24 registration
subcases. Ten new methods cover raw syscall assignment for both inputs,
unrelated/reused descriptors, interruption at initial recording and both cleanup
paths, real MemoryBIO stream release, 20 allocator failure injections, independent
size/count controls and fixture collection validation. Full coverage mapping is
in `matrix-coverage.md`; Python/OpenSSL/platform metadata without hostnames is in
`runtime-environment.json`. The two-interrupt guarantee is limited to the tested
transitions, with no claim of immunity to indefinitely repeated async exceptions.
Legacy raw callers retain compatibility without gaining new custody qualification.

Run the complete content-bound checker from the repository root:

```bash
/usr/bin/python3.14 -I -S -B _bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-2/verify-receipt.py --self-test
```

It checks receipt-recorded source/snapshot/log hashes, current handoffs and their
preserved prefixes, prior packets, exact spec checkpoints, frozen intent and all
review-1 test names. Its isolated copy first passes unchanged, then changes both
a working source and its snapshot identically and confirms that receipt content
binding rejects the drift. Working sources and historical packets are never
mutated by this self-test. This is content validation of an unsigned receipt,
not an authenticated signature or grant.

The older `../review-1/verify-preservation.py` is intentionally untouched. It
requires HEAD exactly `693390a790d95a9bad9b1bd9cc0f0f3052f3fbef`, compares the
index to that commit, and expects its preparation snapshots/current spec. It was
a precommit preservation check for that candidate. Later commits or a new
review candidate invalidate those preconditions; rerunning it is not the correct
way to verify current receipt hashes. This packet's checker does not inspect or
require Git HEAD or index, and uses the recorded content bindings instead.

`parent-raw-transfer-probe.py` and `parent-raw-transfer-result.json` preserve the
parent's independent four-case opcode audit byte-for-byte. Its original command
was `/usr/bin/python3.14 -I -S -B /tmp/bmad-427-parent-raw-transfer-probe.py > /tmp/bmad-427-parent-raw-transfer-result.json`, exit 0. The packet-path reproducible invocation is:

```bash
/usr/bin/python3.14 -I -S -B _bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-2/parent-raw-transfer-probe.py
```

All four parent injections fired, preserved the original KeyboardInterrupt and
full descriptor snapshots, and left no new descriptors. The focused suite
provides current independently executable equivalents. The earlier independent
probe invocation remains archived in the unchanged review-1 packet.

Unrelated concurrent Story 2.3, deferred-ledger and submodule edits are excluded
from this source inventory and were not edited, staged or committed here.
No actual credential/configuration/endpoint access, operational certificate
issuance, recovery allocation, recovery-document signing, upload, recovery,
infrastructure mutation or historical executor replay occurred. Actual
credential/custody/authority/client identity and live connector/TLS/runtime
qualification, full system/target/proof-consumer integration (including telemetry
PostgreSQL), fresh exact attempt/policy/tool bindings and separately scoped
actual recovery/resume/cleanup plus signed/non-dry/manual acceptance remain open.
