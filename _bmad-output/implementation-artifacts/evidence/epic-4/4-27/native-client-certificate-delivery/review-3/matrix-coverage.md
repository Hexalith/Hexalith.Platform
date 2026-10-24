# Current review-3 matrix and ownership coverage

The 39 focused review-2 test names, all 24 registration subcases, and the other
276 suite cases are retained. The complete previous matrix remains immutable at
`../review-2/matrix-coverage.md`. Ten new focused tests extend that coverage:

| Matrix / ownership transition | Current executable regression |
| --- | --- |
| Default/raw connector refusal before credential/context access | `test_private_connector_refuses_raw_factories_before_credential_or_context_access` |
| Explicit certificate acquisition; resource-free placeholder before constructor, real MemoryBIO constructor return, construct/connector returns | `test_owned_connector_constructor_and_return_interruptions_close_registered_real_peer` (five injected transitions) |
| Prepared stream already in shared custody before STORE_ATTR; cancellation releases it | `test_prepared_stream_handoff_store_interrupt_preserves_shared_owner` |
| Discovery/adoption identity drift; replacement must remain open and unchanged | `test_recovery_registration_rechecks_expected_identity_before_adopting_replacement` |
| Exhausted descriptor table: one-free-fd reservation refusal, raw last-slot memfd transfer, duplicate EMFILE and last-slot duplicate transfer | `test_preacquired_recovery_scans_cover_isolated_descriptor_exhaustion_and_raw_results` (four isolated child modes, limit restored and unrelated sentinel preserved) |
| Recovery resources already registered before reservation or constructor return interruption | `test_recovery_reservation_constructor_and_return_interruptions_close_registered_scans` (three injected transitions) |
| Caller history must not replace current exchange loading cancellation | `test_caller_handled_interrupt_does_not_replace_current_loading_interrupt` |
| Two interruptions before stream release, first interrupt retained, remaining memory input released | `test_two_interruptions_inside_stream_close_preserve_first_and_release_all_inputs` |
| C-owned idempotent stream release followed by cancellation before Python bookkeeping; unrelated same-number fd survives | `test_idempotent_c_owned_stream_close_preserves_reused_unrelated_descriptor` |
| Fixture allocates stream before unknown profile / invalid CA; both refusals close it | `test_fixture_native_profile_and_ca_refusals_close_preallocated_stream` |

All original matrix rows continue to run: certificate exact-pair installation and
Authorization omission; bearer byte/audience selection; binding drift and mixed
profiles; malformed/missing/mismatched/encrypted material; loading/exchange/
cleanup deadline and interrupt handling; default/public activation refusal.
Positive peer controls still detect no-op/wrong-key loaders through real mutual
TLS and compare exact DER/CA. Four-plan source/target composition retains native
wire bodies, semantic gates, distinct identities and null recovery acceptance.

The separate executable receipt checker verifies the actual log summary against
bound counts/skips/outcomes and demonstrates isolated coherent source/snapshot
and count/skip/outcome drift rejection. Original adverse probes remain historical
with their adverse results. Current success makes no operational qualification.
