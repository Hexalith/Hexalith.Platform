# Review-3 native client-certificate delivery evidence

This unsigned local fixture packet re-derives the exact review-2 source snapshots
under the amended dispatch spec. It adds no external authenticity, operational
authority, actual credential qualification, or recovery acceptance. All earlier
packets, including review-2's checker, snapshots, logs, and adverse parent probes,
remain byte-preserved. The parent baseline is
`693390a790d95a9bad9b1bd9cc0f0f3052f3fbef`; the shared-workspace history at
`56c783bcd005e3ce0c728dc72e440a696a91d034` is preserved.

`verification.json` binds the four current source snapshots, unchanged supporting
sources, exact seven-suite commands/logs, runtime metadata, historical packet
inventory, handoff prefixes, and dispatch/verification spec checkpoints. The
full spec checkpoints describe those historical moments. Later workflow
status/triage edits to the working spec can legitimately differ; the checker
independently requires the human-owned frozen intent to remain byte-exact.
The review-1 preservation checker remains a historical preparation-HEAD/index
check. The review-2 content checker is unchanged and retains its original
checkpoint and 315-test meaning.

Run from the repository root:

```bash
/usr/bin/python3.14 -I -S -B _bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-3/verify-receipt.py --self-test
```

The checker verifies source/snapshot/log/checkpoint hashes and parses the actual
bound unittest summaries to compare counts, skips and outcomes with the receipt
and command results. Isolated negative controls reject coherent working/snapshot
drift and coherent count/skip/outcome metadata drift, including updated duplicate
metadata and its recorded hash. No working source or historical packet is mutated
by these controls. The receipt is an unsigned reproducibility record, not a
cryptographic attestation from an external party.

The exact parent invocation `/usr/bin/python3.14 -I -S -B
/tmp/bmad427-blind-probes.py` and its original adverse results are retained as
`historical-bmad427-blind-probes.py` and `historical-bmad427-blind-probes.json`.
They describe the review-2 candidate and have not been relabeled or rerun as
passing. Current executable regressions are the 49-test certificate suite and
the other bound suite commands in `command-results.json`; their changed private
owned-connector API intentionally replaces raw resource-returning callbacks.

Repository callers were audited with `rg` for `NativeTLS`, connector definitions
and native exchanges. Only the explicit test/support callers use its exchange;
they now use `_OwnedConnector`, pre-owned inert streams, or
`custody.construct` placeholders registered before constructor acquisition. The
dormant native socket method is unselected. Raw callbacks refuse before
credential/context/network access. The private stream contract requires safe
partial construction and idempotent close; real descriptor release state must
be C-owned, as exercised by FileIO close/reuse. This does not qualify arbitrary
unsupported or non-idempotent third-party closers, infinitely repeated
interruptions, arbitrary hostile descriptor-table mutation outside the tested
process-local protocol, kernel stalls, or an actual connector/runtime.

Recovery scan iterators are C-owned and reserved only after their allocation
placeholder enters custody, before memory creation. Raw result recovery requires
no new directory descriptor under exhaustion. An isolated child lowers and
restores only its own RLIMIT_NOFILE, explicitly verifies one-free-fd reservation
failure and final-slot memory/duplicate transfer preconditions, and preserves
unrelated descriptor bytes/flags. No host or operational resource limit changes
occurred. Independent bounded release retains the first current-exchange
interrupt through two inside-close interruptions and all established entry
transitions; C-level release state prevents retrying a reused unrelated fd.

All tests use static public synthetic credentials and socket-free MemoryBIO
peers. Exact client DER/CA, original PEM/wire bytes, distinct source/target
principals/audiences, all four plans, bearer compatibility, semantic guards,
material/count bounds, registration transitions, closed-standard/fd 3
preservation, disabled public activation and null qualification remain covered.
No actual credentials/configuration/endpoints were accessed. No recovery attempt
or policy was allocated; no operational certificate issuance, signing, upload,
recovery, infrastructure mutation, staging, commit, push or historical helper
replay occurred. Unrelated Story 2.3/2.4, deferred-ledger, sprint-workflow and
submodule edits are excluded and preserved. Actual credential/authority/runtime,
system/target/proof-consumer integration and recovery remain pending;
4.27 stays in-progress, 4.1 closed, Timesheets 3.6 open/FAIL.
