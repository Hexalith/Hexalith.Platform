---
title: EXT-SECRETS-1 runtime delivery for Agents Story 5.4
type: feature
created: 2026-10-17
status: done
route: dispatch
review_loop_iteration: 0
baseline_commit: 3e9cc4c7cf374e114e8a22f553eeaec8de3c8142
context:
  - /home/administrator/projects/hexalith/agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - eng/ext-secrets-1/README.md
  - /home/administrator/hexalith-secrets-evidence/ext-secrets-1/runtime-foundations-final-20261017-_bv3fzwb/HANDOFF.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EXT-SECRETS-1 stays Committed at `f448db29493c1b30b0f18380188fe99b8b7606fe`. The isolated registry and four HMAC key references are installed, but profile, signer, public trust/revocation, private replay, durable originals, role scopes, all-copy destruction, and nonrollback restore are unbound. No detached packet pins three independently enrolled public anchors, so Agents Story 5.4 cannot treat the seam as Available.

**Approach:** From a clean worktree of current `main` plus the unmerged foundations commits, bind the installed provider and inventory through the existing Custody, Secrets.Runtime, and qualification hosts. Preserve the S1–S4/v23 matrix and recovery journals, run Local verification, and emit a detached replacement packet. Live effects stay gated until that packet is accepted.

**Decision:** Keep this spec whole. Leave the three public-anchor hashes null and prepare a blocked packet. Keep the existing fail-closed lifecycle, journal, and qualification-host ports, with no new engine, registration, workload, or decision key. No destroy or restore runs. The packet names each missing reference.

## Boundaries & Constraints

**Always:** Reuse `AddDaprPlatformHmac`, `AddExplicitSecretsRuntime`, `AddPrivateReplay`, the three qualification hosts, `cases.py`, `RunJournal`, and `prepare_acceptance.py`. Keep host `192.168.1.30`, integration date 2026-10-08, Branch B, L/S/O/H/R = 300/30/600/86400/604800, and Open export disposition with zero effects. Decision private keys stay outside Platform and Agents. The candidate worktree is `main` `f0893dc1ced1da0bc219ef0992eb91b075638bf6` plus `8b00866f5cdcd83292510b0d2f3f2fbb18718760`, `538bcaf4f254012372ec355d493da3ed3573017e`, `6233d8d544fe4e41640fcbbd7de6305dcb96131c`, and `05507a22eaba12e285cfc251b64afb970a835ea8`. Leave the dirty `main` checkout's gitlinks and composition files untouched. Record credential references only. `dependencyAvailable` and `productionMutationAllowed` stay false. The packet keeps `acceptedReplacementCommit` null. Executor, observer, and enrollment identities, app IDs, and P-256 keys must differ.

**Never:** Edit the Agents register. Reorder, add, or drop cases in `cases.py` or `contract-case-matrix.txt`. Delete or rewrite recovery journals or prior evidence directories. Run Live or any of the 234 cases before the owner accepts the candidate, exact command, configuration, plan, and three enrolled anchors. Self-generate anchors and call them independently enrolled. Use operator or Memories credentials as application credentials. Treat ordinary HMAC KV destroy as all-copy proof. Add a database or substitute provider. Commit the unrelated dirty `main` files. Deploy a workload whose profile, trust, revocation, or anchor inputs are still null.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Installed provider | Foundations evidence has registry, HMAC credential, and tenant A/B | Public references enter the candidate configuration | Secret bytes stay out of git, logs, and evidence |
| Anchor or backend absent | PEM or qualified lifecycle reference missing | Detached packet stays blocked and names the input, owner, and action | Exit 2, zero effects |
| Dirty checkout | Unrelated `main` edits remain | Preparation runs only in the clean candidate worktree | No packet from the dirty checkout |
| Live before acceptance | Register target or enrollment differs | None of the 234 cases run | DependencyNotAvailable, zero effects |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Custody/DaprPlatformHmacBinding.cs` — reuse `AddDaprPlatformHmac`. Do not change wipe timing or key handling.
- `src/Hexalith.Platform.Secrets.Runtime/SecretsRuntimeServiceCollectionExtensions.cs` — `AddExplicitSecretsRuntime` and `AddPrivateReplay` register clients only when references and the 1–30 second timeout are valid. Do not give them defaults.
- `src/Hexalith.Platform.Secrets.Runtime/DaprHmacAuthorityAdapter.cs`, `DaprInventoryAuthorityAdapter.cs`, `DaprDecisionAuthorityAdapter.cs`, `DaprSigningProviderAdapter.cs`, `DaprSigningTrustAdapter.cs`, `DaprRevocationAuthenticatorAdapter.cs`, `DaprReplayRegistrarAdapter.cs` — reuse. Leave the deferred absence, echo, and revision defects unchanged.
- `src/Hexalith.Platform.Secrets.Runtime/QualificationHost.cs` — `IQualificationExecutorProvider`, `IQualificationObserverProvider`, `IQualificationEnrollmentProvider`, `IQualificationOriginalStore`, and `QualificationDenial.Evaluate` deny when the provider, store, verifier, all-copy reference, nonrollback journal, or accepted commit is missing.
- `src/Hexalith.Platform.Secrets.Runtime/QualificationHttpHost.cs` — `IQualificationCallerAuthenticator` null denies before a provider call.
- `src/Hexalith.Platform.Secrets.Qualification.{Executor,Observer,Enrollment}/Program.cs` — hosts call `GetService` and have no production registration.
- `src/Hexalith.Platform.Custody/ICustodyKeyLifecycleProvider.cs`, `ICustodyKeyLifecycleAuthority.cs`, `IFr34ProtectionCanaryEngine.cs` — ports only. No production backend is registered.
- `src/Hexalith.Platform.Secrets.Inventory/` and `src/Hexalith.Platform.TenantRegistry.Host/` — present only on the foundations commits. Reuse the installed host; do not replace it.
- `eng/ext-secrets-1/cases.py` — `plan()` is the 234-case S1–S4/v23 matrix. Do not change order. `contract-case-matrix.txt` is the frozen review list.
- `eng/ext-secrets-1/qualify.py` — `RunJournal` under `original-run-journal/` and `preflight()` refuse effects before an accepted target, command, configuration, plan, and three PEM anchors.
- `eng/ext-secrets-1/prepare_acceptance.py` — writes the v2 packet only from a clean candidate. Dirty porcelain writes `blocked-inputs.json` only.
- `eng/ext-secrets-1/provisioning/owner-input-map.json` — `apply` stays false.
- Evidence `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/runtime-foundations-final-20261017-_bv3fzwb/` — installed public refs and `remaining-inputs.json`. Do not overwrite. Do not copy credential files into git.
- Agents `external-dependency-register.md` EXT-SECRETS-1 row — Committed. Do not edit.

## Tasks & Acceptance

**Execution:**
- [x] Clean worktree — start from `f0893dc1ced1da0bc219ef0992eb91b075638bf6` and include the four foundations commits. Leave the dirty `main` checkout untouched.
- [x] Candidate `runtime-reference.json` — copy installed public provider, inventory, tenant, host, timing, and Open-disposition fields from the foundations evidence. Leave every unresolved field null.
- [x] `src/Hexalith.Platform.Secrets.Qualification.Executor/Program.cs`, `../Observer/Program.cs`, `../Enrollment/Program.cs` — confirm they stay unregistered. Do not add a deny-only authenticator, store, or verifier.
- [x] `src/Hexalith.Platform.Custody/ICustodyKeyLifecycleProvider.cs`, `ICustodyKeyLifecycleAuthority.cs`, `IFr34ProtectionCanaryEngine.cs` — confirm no production backend is added. No destroy or restore runs.
- [x] `eng/ext-secrets-1/cases.py`, `contract-case-matrix.txt`, and existing `original-run-journal/` directories — confirm unchanged.
- [x] `eng/verify-ext-secrets-1.ps1 -Mode Local` — fresh artifact and evidence directories, EventStore source `../eventstore`.
- [x] `eng/ext-secrets-1/prepare_acceptance.py` — run from the clean candidate into a new detached directory. Emit the exact command and the configuration and plan hashes. Leave the three anchor hashes null.
- [x] Live gate — record that the Agents register was not edited and that no contract case ran.

**Acceptance Criteria:**
- Given the installed registry and HMAC references, when the candidate configuration is written, then those public references are bound and no credential value is stored.
- Given a missing anchor or qualified lifecycle backend, when preparation finishes, then the packet stays blocked, names that input, and reports zero effects.
- Given the clean candidate, when preparation finishes, then the packet schema is `EXT-SECRETS-1.replacement-acceptance.v2`, `acceptedReplacementCommit` is null, and the exact command matches `prepare_acceptance.py`.
- Given any result, when verification finishes, then Local mode passed, the 234-case matrix and journals are intact, and Live was not run.

## Implementation Notes

Clean candidate worktree: `/home/administrator/projects/hexalith/platform-ext-secrets-1-runtime-delivery-20261017` at `9b73d777590a58f847e90d3658b09efd4185be63` (`f0893dc` merged with `05507a2`, which contains `8b00866`, `538bcaf`, `6233d8d`, and `05507a2`). A fresh merge-tree of those parents still conflicts in `deferred-work.md`; the existing merge commit was reused and no source file was edited. The candidate `references/Hexalith.Builds` gitlink `920389059d0fa4c0485709500d147873c4baaadb` was checked out only in that worktree so Local verification could import its props.

Public configuration: `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/runtime-delivery-20261017-9b73d77/configuration/runtime-reference.json` (`configurationSha256` `83baee70e7beaf350b543e658f703ac32b3cd0c3f23299862ae342b886988970`). Installed provider, HMAC credential reference, tenant inventory, tenants A/B, host, timing, and Open disposition are copied from the foundations evidence. All 51 unresolved leaves, including the three `publicAnchorFile` values and `acceptedReplacementCommit`, stay null. No credential value was copied. The foundations evidence directory was not overwritten.

Qualification hosts still resolve executor, observer, enrollment, original store, caller authenticator, and proof verifier with `GetService` and register none of them. No production `ICustodyKeyLifecycleProvider`, `ICustodyKeyLifecycleAuthority`, or `IFr34ProtectionCanaryEngine` backend was added. `owner-input-map.json` keeps `apply: false`. No destroy or restore ran.

`cases.py` SHA-256 `9998f8d519443af219c1f44fc5f03a15571d38e621811b326a96ee40c0f0ca3c` and `contract-case-matrix.txt` SHA-256 `85797cb09dc16ffbb8ffa3f1bbbab8883b0c9ac66d88e682f23c9f7a30ba8401` are unchanged. `plan()` still yields 234 cases. No `original-run-journal/` directory was present and none was created.

Local verification passed from the candidate: 709 custody tests and 107 runtime tests, `LiveReady: false`, `DependencyAvailable: false`. Evidence: `/tmp/ext-secrets-1-local-20261017-9b73d77b-evidence`. Artifacts: `/tmp/ext-secrets-1-local-20261017-9b73d77b-artifacts`. EventStore source: `/home/administrator/projects/hexalith/eventstore`. `python3 -m unittest discover -s eng/ext-secrets-1 -p 'test_*.py' -v` passed 104 tests. These are offline client checks, not the 234 live contract cases.

Detached packet: `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/runtime-delivery-20261017-9b73d77/detached-packet`. Exit 2, `packetPrepared: true`, `qualificationEffects: 0`. Schema `EXT-SECRETS-1.replacement-acceptance.v2`. `candidateCommit` `9b73d777590a58f847e90d3658b09efd4185be63`. `acceptedReplacementCommit` null. `dependencyAvailable` and `productionMutationAllowed` false. `planSha256` `16dd493ff0414fea13be95f137d372e54088881550665aa3239a5814a9e862ed`. `executorAnchorSha256`, `observerAnchorSha256`, and `enrollmentAnchorSha256` are null. `blocked-inputs.json` names each missing reference, including the three anchors, profile/trust/revocation, private replay, all-copy lifecycle, and nonrollback restore. `exact-command.sh` is the command emitted by `prepare_acceptance.py`; it was not executed. Live artifact and evidence directories were not created.

Live gate: `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/runtime-delivery-20261017-9b73d77/live-gate.json`. The Agents register SHA-256 remained `6d443170159303bdd39da62b377cb29501ef2071b7d2c01a1f2f77191a984a45`. `EXT-SECRETS-1` stays `Committed`. No contract case ran.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH1 | false | reject | `baseline_commit` is the main snapshot taken before this run. The frozen candidate is `9b73d777590a58f847e90d3658b09efd4185be63`, and Local verification plus the packet were produced there. |
| BH2 | false | reject | The candidate worktree still pins Builds `920389059d0fa4c0485709500d147873c4baaadb`. The gitlink and composition edits are commit `1753153`, Story 1.8, which landed after that snapshot. |
| BH3 | false | reject | The frozen Decision and the tasks require the qualification hosts to stay unregistered. The public runtime reference binds the installed provider and inventory; adding a registration would violate that decision. |
| BH4 | false | reject | The frozen Decision leaves the three anchor hashes null until independent enrollment. Generating distinct keys here is forbidden, and the packet records the null hashes. |
| BH5 | false | reject | The candidate has no `original-run-journal/` directory, so none was rewritten. `blocked-inputs.json` already names `runtime.nonrollbackJournalReference`. |
| BH6 | false | reject | `blocked-inputs.json` names the signer fields and all three `callerScopeAuthorityReference` values. Durable-original names stay on the owner-input map and on `QualificationHost`'s denial path. A longer note would only edit this spec. |
| BH7 | false | reject | The copied configuration keeps host `192.168.1.30`, timing `300/30/600/86400/604800`, and Open disposition. Integration date and Branch B remain the README and register constraints. Restating them in the notes would edit this spec. |
| BH8 | false | reject | This delivery is Agents Story 5.4, not the platform sprint row `5-4-review-tenant-lifecycle-flows-and-handle-leftover-synthetic`. Inventory on later main does not change the pinned candidate. |
| BH9 | low | defer | Story 1.8's spec says `done` while `sprint-status.yaml` still says `review` for `1-8-run-and-debug-with-readiness-aware-startup-and-useful-diagno`. This secrets delivery did not write that row. |
| BH10 | low | reject | `AssertContainerImage` still requires the image name and SHA256. Aspire clears the floating tag when a digest is set. `PlatformAppModel` already refuses a non-Development retention compose; another refusal test is not a direct correction. |
| BH11 | medium | defer | Grouped with EH1 and EH2. `File.ReadAllText` at `PlatformPostgresqlHealthCheck.cs:26` ignores the 5-second token, and `UnauthorizedAccessException` is outside the Unhealthy filter. Story 1.8 changed that line; this delivery did not. |
| BH12 | false | reject | Draining startup output to `Stream.Null` is the bounded-memory behavior. The untested `IOException` branch is VG1, not a second defect. |
| EH1 | medium | defer | `PlatformPostgresqlHealthCheck.CheckHealthAsync` reads the authority PEM with synchronous `File.ReadAllText` after arming a 5-second token. A blocked read is not cancelled by that token. Not changed by this delivery. |
| EH2 | medium | defer | The catch filter admits `IOException`, `SocketException`, `AuthenticationException`, and `CryptographicException` only. `UnauthorizedAccessException` from an unreadable or directory path leaves `CheckHealthAsync`. Not changed by this delivery. |
| VG1 | medium | defer | Pre-verified. `DrainAsync` swallows `IOException` and `RunOwnedAsync` can still return the process exit code; no Builds test makes that discarded-stream read throw. The filed disposition is defer. |

## Design Notes

The accepted register target and the replacement candidate are different commits. `prepare_acceptance.py` pins the candidate SHA and leaves acceptance to a later register edit. A tracked template or a packet inside the checkout does not authorize Live. `QualificationDenial` still returns 503 until all-copy, nonrollback, and `acceptedReplacementCommit` are present, so registering a host cannot start an effect by itself.

## Verification

**Commands:**
- `pwsh -NoProfile -File eng/verify-ext-secrets-1.ps1 -Mode Local -EventStoreSourceRoot ../eventstore -ArtifactsDirectory /tmp/ext-secrets-1-local-UNIQUE-artifacts -EvidenceDirectory /tmp/ext-secrets-1-local-UNIQUE-evidence` — expected: Local source checks pass and the command states that full EXT-SECRETS-1 remains unavailable.
- `python3 -m unittest discover -s eng/ext-secrets-1 -p 'test_*.py' -v` — expected: pass, with the 234-case matrix intact.
- `python3 eng/ext-secrets-1/prepare_acceptance.py --root <clean-candidate> --configuration <public-runtime-reference.json> --output <fresh-detached-directory> --artifacts-argument <fresh-live-artifacts> --evidence-argument <fresh-live-evidence>` — expected: detached packet or blocked report, zero effects, exit 0 only when no required input is missing.

**Parent verification (2026-10-17):** The candidate worktree is clean at `9b73d777590a58f847e90d3658b09efd4185be63` and contains `f0893dc`, `8b00866`, `538bcaf`, `6233d8d`, and `05507a2`. The dirty `main` checkout was not used. Custody XML reports 709 passed, 0 failed, 0 skipped. Runtime XML reports 107 passed, 0 failed, 0 skipped. The 104 offline client tests include the matrix rows: secret-bearing configuration is refused without retaining values (`test_rejected_configuration_only_writes_sanitized_zero_effect_report`, `test_preflight_rejects_secret_bearing_configuration_without_exposing_values`); a missing anchor is named with no transport (`test_missing_reference_and_key_are_reported_without_transport`); a dirty checkout writes only `blocked-inputs.json` with zero effects (`test_dirty_checkout_yields_only_blocked_report`); an unaccepted or mismatched register target denies Live with zero calls (`test_unaccepted_packet_denies_live_compatibility_with_zero_dapr_calls`, `test_mismatched_commit_string_denies_live_compatibility_with_zero_dapr_calls`, `test_arbitrary_live_command_cannot_authorize_effects`). The candidate configuration matches the foundations public reference. The blocked report has exit-path `DependencyNotAvailable`, `qualificationEffects` 0, and the three null anchors. Preserved `remaining-inputs.json` still names the owner and action for each anchor and lifecycle reference. Live artifact directories were not created, and the Agents register hash is unchanged.
