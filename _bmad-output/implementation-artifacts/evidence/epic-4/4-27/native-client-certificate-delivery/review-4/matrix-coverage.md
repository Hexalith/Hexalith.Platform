# Review-4 matrix and qualification controls

All 49 review-3 focused test names and all 24 registration subcases are retained,
as are the other 276 suite cases. The complete historical matrix is unchanged at
`../review-3/matrix-coverage.md`. Six new focused cases extend it to 55:

| Matrix / transition | Current executable control |
| --- | --- |
| TLS-valid compact base64 and leading blanks, original installed bytes and exact authenticated peer | `test_compact_base64_and_leading_blank_lines_authenticate_and_install_original_bytes` (three real-loader/peer variants) |
| Prepared C-owned stream survives custody constructor entry, partial initialization and return interruption | `test_custody_constructor_entry_partial_and_return_interruptions_close_prepared_c_owned_stream` (three no-dispatch/no-credential transitions) |
| Trusted stream constructor fails after acquiring a C-owned descriptor | `test_trusted_stream_constructor_failure_after_c_owned_acquisition_releases_partial_stream` (ordinary failure and exact cancellation) |
| Lost memfd/duplicate result and a second scan/registration interruption | `test_lost_raw_result_and_second_scan_or_registration_interrupt_preserve_exact_descriptors` (eight combinations; both injections asserted, exact descriptor/flag snapshot and unrelated bytes preserved) |
| Private acquire returns non-None or installs an unsupported/missing/incomplete stream | `test_private_acquire_postconditions_refuse_before_dispatch_and_close_all_known_streams` (four violations, no dispatch, prepared/current streams closed) |
| Both first and second cleanup-invocation handoffs interrupt with an authenticated peer and a live memory input | `test_two_cleanup_invocation_interruptions_preserve_peer_and_input_ownership` (exact primary, no retry, all resources released) |

Existing cleanup-entry tests are adapted to the explicit protected passes,
including the first inline cleanup call and second descriptor/stream release
transitions. Each preserved injection is reached and asserted, not replaced by
an early refusal. The caller-history regression now additionally interrupts the
first boundary assignment and connector setup inside handled caller cancellation and proves real TLS
success inside handled KeyboardInterrupt and ValueError contexts.

All original matrix rows still run: exact certificate pair with no Authorization;
bearer compatibility and exact audience; profile/principal/audience/identity/
custody/secret drift; malformed/missing/mismatched/encrypted material; bounds and
noninteractive/sealed loading; deadline/cancellation at acquisition/exchange/
cleanup; default/public activation refusal before live access. Exact DER/CA and
wire assertions, no-op/wrong-loader controls, source/target separation, semantic
stop conditions, original native bodies and null operational acceptance remain.

The current executable sweep separately covers 299 line, 2,309 opcode and 81
secondary-line targets, with zero leaks, swallowed cancellations or unraisable
exceptions. Five terminal external interruptions are classified only after exact
descriptor and peer release is proved. There is no guarantee for infinitely
repeated interruption, unsupported closers, hostile descriptor mutation, kernel
stalls or a signal-atomic return after cleanup.

The receipt self-test generates real same-count unittest FAILED, skipped,
expected-failure and unexpected-success transcripts. Hashes bind those altered
logs coherently while unchanged metadata still claims success; all refuse.
Coherent seven-suite omission, command drift, metadata drift and source/snapshot
drift also refuse. The seven required positive suites alone pass 331 tests with
zero skips. This packet supplies reproducibility only and no operational grant.
