# Native client-certificate offline coverage

The frozen spec is the sole implementation contract. Its original bytes are
retained in `spec-baseline.md`; the parent baseline commit remains unchanged.
`verification.json` binds the corrected source snapshots and seven exact command
transcripts: 297 passing tests, zero skips. All certificate/key bytes are static
public synthetic fixtures; the focused suite forbids all socket allocation.

| Frozen matrix row / acceptance | Focused verification |
| --- | --- |
| Exact bound PEM pair, explicit certificate profile, no Authorization, deadline and cleanup before success | `test_exact_certificate_and_key_authenticate_real_peer_without_authorization`; `test_sealed_descriptor_paths_exact_bytes_permissions_and_noninteractive_loader` |
| Default and explicit bearer behavior, unchanged original HTTP wire and cluster audience | `test_bearer_default_and_explicit_profile_preserve_wire_and_audience`; existing `test_full_source_target_exchanges_use_selected_bearers_and_only_the_pinned_ca` |
| Unknown/mixed profile, no profile sniffing or fallback | `test_unknown_mixed_and_unselected_profiles_refuse_without_connector`; exact two-field envelope rejects added bearer fields |
| Principal, audience, identity, custody, secret and CA binding drift | `test_principal_audience_identity_custody_secret_and_ca_drift_refuse_before_dispatch` |
| Malformed/missing/mismatched/unsupported encrypted keys; duplicate/extra envelope fields; bounded material | `test_malformed_missing_mismatched_encrypted_duplicate_and_oversized_material_refuse`; no password lookup or prompt |
| Real key/certificate installation and sealed, nonpersistent descriptor bytes | `test_sealed_descriptor_paths_exact_bytes_permissions_and_noninteractive_loader`; real SSLContext parsing and mTLS peer identity checks |
| Missing/wrong certificate loader control | `test_noop_loader_cannot_pass_peer_authentication`; `test_wrong_certificate_loader_cannot_pass_peer_authentication`; both detect failed authentication through real MemoryBIO peer verification |
| Wrong CA and wrong host | `test_wrong_ca_and_wrong_hostname_are_detected_by_real_tls`; exact CA DER inventory assertion on every composed native connection |
| Expiry during sealing/loading/descriptor cleanup | `test_loading_expiry_and_descriptor_cleanup_expiry_prevent_connector` |
| Loading interrupt and independent owned-descriptor cleanup | `test_loading_interrupt_preserved_while_every_owned_descriptor_closes_independently`; `test_sealing_failure_closes_first_descriptor_and_cancellation_never_dispatches`; `test_cleanup_failure_refuses_and_closes_both_descriptors` |
| First-cleanup-line cancellation transition | `test_interrupt_on_first_installation_cleanup_line_retains_outer_descriptor_ownership`; parent independent probe first found two unreleased fds, then verified zero after correction; see `parent-cancellation-audit.json` |
| Exchange/stream-cleanup timeout or cancellation; primary interrupt; no late success/retry | `test_exchange_interrupt_preserves_primary_and_closes_stream_without_retry`; `test_exchange_and_stream_cleanup_expiry_or_interrupt_never_return_late_success`; existing uncertain PATCH/poisoning tests |
| Missing connector and public activation default deny before credential/config/network access | `test_default_connector_and_public_activation_refuse_before_credential_context_or_network`; all focused sockets forbidden |
| Parent descriptor preservation | `test_occupied_parent_fd3_and_its_flags_remain_unchanged`; `test_loading_preserves_originally_closed_standard_descriptors`; original standard fds 0/1/2 tested in separate isolated hosts |
| Capture/restore/resume/cleanup with separate source and target credentials | `test_all_four_plans_preserve_distinct_certificate_identities_audiences_native_requests_and_guards`; real mutual TLS encrypts/decrypts all native HTTP fixture bytes, checks exact peer DER, principal, audience, method/path/body and pinned trust |
| Independently selected source certificate and target bearer | `test_source_certificate_target_bearer_are_explicit_independent_selections`; each exchange has one selected profile |
| Existing semantic guards preserved for certificate plans | boundary `test_certificate_profiles_keep_stale_and_future_drain_and_fence_guards_closed`: stale/future clean drain, inventory fence and each owned delete refuse without dispatch; all original semantic/cleanup/request guards remain passing |
| Fixtures leave operational qualification and actual recovery pending | all four plan results retain null operationalQualification/recoveryAcceptance; three append-only handoffs preserve 4.27 in-progress, 4.1 closed, Timesheets 3.6 open/FAIL and disabled activation |

The seven exact commands use `/usr/bin/python3.14 -I -S -B` and respectively run
`test_native_client_certificate.py` (21), `test_recovery_execution_boundary.py`
(45), `test_immutable_readback_driver.py` (12),
`test_policy_authentication_driver.py` (13), `test_immutable_s3_provider.py` (42),
`test_recovery_adapters.py` (94) and `test_telemetry_postgres_adapter.py` (70).
The initial 296-test transcripts remain separate under `first-candidate/`.

No actual credential/configuration/endpoint access, recovery-attempt allocation,
operational certificate issuance, recovery-document signing, upload, recovery or
infrastructure mutation was performed. Actual custody/IAM/authority/client
identity, live TLS/runtime, system/store/target/proof-consumer and recovery
qualifications remain pending. Static synthetic CA keys used only while preparing
the embedded fixtures were discarded; only public fixture client/server keys
remain in test code. They confer no authority.
