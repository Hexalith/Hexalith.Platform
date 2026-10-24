# Review-2 matrix and preservation coverage

All fixtures use static public synthetic material, real `SSLContext` loading,
socket-free MemoryBIO peers and injected inert connectors. No sockets or actual
credential/configuration access is permitted. Every review-1 test name remains.

| Spec scenario / finding | Direct executable coverage in `test_native_client_certificate.py` |
| --- | --- |
| Exact certificate delivery | `test_exact_certificate_and_key_authenticate_real_peer_without_authorization`, sealed-byte checks, exact DER/CA assertions, no-op/wrong-loader controls |
| Bearer compatibility / mixed selection | default/explicit bearer exact wire test; source certificate/target bearer composition |
| Binding/profile drift | principal/audience/identity/custody/secret/CA drift and unknown/mixed/unselected profile tests |
| Invalid material | malformed/missing/mismatched/encrypted/duplicate PEM cases; CRLF/no-terminal-newline positives; traditional RSA/EC positives; valid chain and missing-intermediate control |
| Isolated count and size bounds | 16/17 certificate count below byte bounds with `load.assert_not_called()`; independent certificate/key sizes with envelope below 65536, valid PEM grammar and <=16 certificates; envelope whitespace crosses only envelope bound |
| B21/E21 raw syscall handoff | `test_raw_memfd_and_duplicate_store_interrupts_recover_both_input_allocations`: STORE_FAST low/fd after CALL, occurrences 1 and 2; exact unknown descriptor precondition and identical primary interrupt |
| Unrelated/reused descriptor preservation | `test_raw_result_recovery_preserves_unrelated_similar_and_reused_descriptors`: low/fd for both occurrences, replacement `/dev/null`, similarly named memfd and pinned same-name/different-inode duplicate control; prior completed-close reuse test |
| All 24 registration transitions | unchanged `test_registration_entry_append_and_constructor_returns_retain_every_input`: 6 sites x 4 input descriptors; all former allocator-return/opcode tests retained |
| B22 initial recording | `test_primary_loading_interrupt_survives_interruption_at_first_error_assignment`: secondary SystemExit before first `self.error` assignment preserves the loading KeyboardInterrupt through exception context |
| B23/E23 final cleanup | inner and fallback descriptor cleanup entry regressions retain original loading interrupt; exact primary object identity and descriptor snapshots |
| E22 outer stream cleanup | real MemoryBIO success interrupted at inner close entry; second regression interrupts inner and fallback close entries with a real open peer plus a still-owned synthetic input; both close, original interrupt preserved |
| B24 actual allocator failures | `test_custody_backed_duplicate_write_chmod_and_sealing_failures_reach_owned_inputs`: duplicate/write/chmod/F_ADD_SEALS/F_GET_SEALS x first/second input x ordinary/cancellation = 20 injection subcases; each confirms it reaches the unique custody-backed memfd |
| Deadline/cancellation/cleanup | prior loading/cleanup expiry, independent descriptor close failures, interrupted exchange/send/recv/stream close, occupied fd 3 bytes/flags and closed standard descriptors |
| B26 fixture defaults/truncation | `test_fixture_collections_distinguish_none_empty_and_mismatched_lengths`: None defaults succeed; explicit empty, short and long profile/secret/CA collections refuse; direct fixture CA count checks |
| All four operations | exact source/target peer DER and CA, audience/identity custody, native method/path/body/preconditions, semantic guards and null acceptance remain covered by original composition and boundary suites |
| Default / public invocation | missing connector and public activation refuse before credential/context/configuration/network access |
| B27/B28 receipt/checker | `verify-receipt.py --self-test`: source/snapshot/log/checkpoint hashes, frozen intent, historical hashes, handoff prefixes, test-name preservation; isolated coherent source+snapshot drift fails receipt hash; no HEAD/index requirement |
| B29 runtime reproduction | `runtime-environment.json`: Python patch/build/interpreter path and SHA-256, OpenSSL, OS/release/architecture/distribution, no hostname |
| B30 unrelated edits | Source inventory excludes Story 2.3, deferred ledger and submodules; implementation and receipt checks do not write them |

The two-interrupt regressions qualify the exercised loading/recording/cleanup
transitions. They do not prove reclamation under indefinitely repeated async
exceptions or kernel stalls. The source-custody reader, process driver and
legacy raw `sealed` callers retain their old compatibility contracts and are
not claimed to gain custody-backed allocator qualification.

The new packet's `prior-packet-sha256.json` covers all 55 prior native-certificate
packet files, including review-1, the complete 305-test source snapshots and the
original independent parent probe invocation. The parent original baseline is
retained as `693390a790d95a9bad9b1bd9cc0f0f3052f3fbef`; new verification is
content based and does not require current HEAD to equal that historical commit.
