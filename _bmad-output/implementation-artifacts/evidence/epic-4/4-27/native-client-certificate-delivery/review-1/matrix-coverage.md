# Review-1 matrix and acceptance coverage

The frozen spec and its review corrections are the implementation contract.
All seven commands and log hashes are in `command-results.json`; 305 tests pass
with zero skips. All 21 initial focused test names and all 45 initial boundary
test names remain, as do the five unchanged supporting suites (231 cases).

| Requirement | Verification |
| --- | --- |
| Explicit bound certificate pair, original bytes, no Authorization | `test_exact_certificate_and_key_authenticate_real_peer_without_authorization`, `test_sealed_descriptor_paths_exact_bytes_permissions_and_noninteractive_loader` |
| Compatibility bearer profile and exact audience/wire bytes | `test_bearer_default_and_explicit_profile_preserve_wire_and_audience`; original full source/target bearer and CA assertions |
| Unknown/mixed/unselected authentication, no fallback | `test_unknown_mixed_and_unselected_profiles_refuse_without_connector` |
| Principal/audience/identity/custody/secret/CA drift refuses before dispatch | `test_principal_audience_identity_custody_secret_and_ca_drift_refuse_before_dispatch` |
| Strict envelope, missing/malformed/mismatched/encrypted or oversized keys | `test_malformed_missing_mismatched_encrypted_duplicate_and_oversized_material_refuse`; refusal password callback tested without lookup or prompt |
| Real peer identity, wrong/no-op loader, exact CA and host | original `test_noop_loader_cannot_pass_peer_authentication`, `test_wrong_certificate_loader_cannot_pass_peer_authentication`, `test_wrong_ca_and_wrong_hostname_are_detected_by_real_tls`; all original exact DER/CA checks retained |
| Loading/sealing/cleanup expiry; no late success | `test_loading_expiry_and_descriptor_cleanup_expiry_prevent_connector`, `test_exchange_and_stream_cleanup_expiry_or_interrupt_never_return_late_success` |
| Independent cleanup and original interruption, no retry | original loading/sealing/cleanup/stream interrupt tests; original first-installation-cleanup-line regression |
| Allocation owns inputs before allocator return or RETURN_VALUE opcode | `test_allocator_return_and_return_opcode_interrupts_retain_registered_ownership`, each certificate/key allocation |
| Registration cannot lose an input at entry, append or owner constructor return | `test_registration_entry_append_and_constructor_returns_retain_every_input`, six transitions across each of four low/high allocations |
| Completed close cannot retry a released descriptor number | `test_successful_owned_close_never_retries_a_reused_descriptor_number`, including an explicitly reopened same-number unrelated descriptor and repeated cleanup |
| Loading KeyboardInterrupt survives later cleanup-transition SystemExit | `test_loading_interrupt_survives_second_cleanup_transition_interrupt`, zero remaining descriptors and no connector calls |
| Conventional PEM CRLF/no-terminal-newline accepted without byte changes | `test_valid_crlf_and_missing_terminal_newlines_install_original_bytes`, all four formatting combinations, real loading and handshake |
| Traditional EC and RSA keys; valid intermediate chain | `test_traditional_ec_key_authenticates_real_peer`, `test_traditional_rsa_key_and_valid_intermediate_chain_authenticate_real_peer`, exact leaf DER and missing-intermediate negative control |
| Certificate count bounded independently of material/envelope sizes | `test_certificate_count_limit_isolated_below_all_byte_limits`, positive 16 and refused 17 blocks, each below both byte caps |
| Occupied fd 3 retains bytes and flags; closed standard fds remain closed | explicit isolated fd 3 precondition/sentinel/descriptor and status flags; original three isolated closed-stdio checks |
| Missing connector/public activation refuses before any credential/config/network access | `test_default_connector_and_public_activation_refuse_before_credential_context_or_network`; sockets forbidden throughout focused tests |
| Capture, restore, resume, cleanup preserve bound audiences and distinct source/target client identities and native requests | original real MemoryBIO four-plan composition asserts exact client DER, exact CA trust, audience, principal, method/path/body and stream cleanup |
| Mixed source-certificate/target-bearer selection remains explicit | original independent-profile composition case |
| Semantic stops and preconditions stay enforced | original certificate stale/future drain and every fence/delete guard; all 45 boundary cases pass |
| No fixture authority; actual qualifications remain pending | all four composed results retain null operationalQualification and recoveryAcceptance; default public gate closed; append-only handoffs retain historical story states |

Suite counts are certificate 29, boundary 45, readback 12, authentication 13,
S3 42, common 94 and telemetry 70. The initial candidate's 297-test receipt and
its four source snapshots are byte-preserved. Historical spec receipt and the
archived independent parent probe retain their original scope; current-source
qualification is limited to these unsigned offline fixtures.
