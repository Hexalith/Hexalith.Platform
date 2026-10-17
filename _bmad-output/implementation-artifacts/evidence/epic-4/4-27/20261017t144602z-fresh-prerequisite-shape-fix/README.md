# Review fixes without changing the prepared candidate

The offline preparer/verifier now requires JSON objects before root or nested
object access. Manifest arrays/scalars, malformed manifest `files`, payload
arrays, malformed owner subobjects and malformed local-record entries produce
CLI exit 1 with the bounded refusal JSON and empty stderr. The documented
`--owner-policy` now names its absolute owning checkout path.

Only the focused `test_fresh_prerequisite.py` suite ran: **14 passed, no failures
or skips**. Its added malformed-root/nested cases call the CLI entry point and
check exit 1, exact refusal output and no traceback. The documentation's absolute
owner-policy path exists; whitespace and final-newline checks pass. The exact
new source hashes and results are in [source-test-relation.json](source-test-relation.json).

The existing fresh packet remains RID `20261017t143202z`, prepared
2026-10-17T14:32:02Z, exclusive expiry 2026-10-18T14:32:02Z, original manifest
SHA-256 `d14efe6e8e82d983187416392ccc77207b446827f149caf3b45ed4b1ae90939a`.
Its original source/test record and consistency pass remain historical. These
three corrected source files have different hashes from their original packet
bindings, so that pass does not certify this corrected source revision. This
separate relation discloses the drift; it does not amend or reseal the packet.

No existing packet bytes, dates, manifest, original receipt or historical
verifier were changed. The existing packet verifier and old refused verifier
were not executed, and no additional recovery candidate was allocated.
Original verifier exit 1 remains preserved. No full preservation sweep or
current packet-integrity certification is claimed. The operational gate remains
closed, and there is no live operation or Timesheets acceptance.
