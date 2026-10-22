---
title: 'Shared Platform delivery for Agents Story 5.4'
type: 'feature'
created: '2026-10-22'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/docs/implementation/ext-secrets-1-erasure-authority-decision-2026-10-20.md'
  - '{project-root}/docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Agents Story 5.4 still has no shared Platform commit whose EXT-SECRETS-1 and EXT-HOST-1 packets can be accepted together. Custody requests still carry fabricated issuer, signature, and authority literals. Current-authority, private replay, and spool-authority checks deny because no private implementation is bound. Full can exit 0, but its evidence is not bound to the bytes that were written. OQ-5.4-1 cannot run: EXT-PROTECTION-1 is Uncommitted, the three public anchors are null, and the host profile slots are empty.

**Approach:** Finish the private custody, signing, current-authority, replay-credential, spool, and recovery paths so a supplied credential performs real ES256 and exact authorization, and a missing one writes nothing. Prepare the 192.168.1.30 profile, credential references, trust-anchor requests, protection bindings, and retained-history prerequisites without installing them. Give Full a passing path whose persisted evidence digest is checked on the attempt. Commit that delivery once, emit both detached packets pinned to that commit, and record the OQ-5.4-1 refusal. Do not execute the cohort.

## Boundaries & Constraints

**Always:** Reuse `DeletionBatchCapabilityCodec.Sign`/`Verify`, `DeletionCapabilitySigningActor`, `PrivateOwnerOperationAuthenticator`, `ReplicatedSecurityObservationSpool`, `ReplicatedSecurityObservationSpoolWorker`, `AddPrivateReplay`, `AddPrivateTrustedEnvelopeVerifier`, `AddPrivateSecurityDenialRecorder`, and `AddPrivateSecurityObservationDrainWorker`. Keep registrar, spool, worker, and signer credentials as private constructor captures. Host stays `192.168.1.30`, date `2026-10-08`, Branch B, and L/S/O/H/R = 300/30/600/86400/604800. A Full pass requires the thirteen exact `REQUIRED_CELLS` Passed and an attempt digest equal to the persisted evidence bytes. `dependencyAvailable`, `productionEngineRegistered`, `liveEffects`, and `acceptedReplacementCommit` stay false/null. `missingRuntimeInputs` stays `EXT-PROTECTION-1`, `trust-anchors`, `OQ-5.4-1`. Both packets pin one identical full SHA from `git rev-parse HEAD` after one commit of this delivery.

**Never:** Edit the Agents register. Mint production keys or fill executor, observer, or enrollment anchor files. Treat OpenBao HMAC, ordinary Raft restore, or `CustodySharedCutModel` as a lifecycle provider. Register the signing actor, raw registrar, spool, or worker for general DI. Add an Agents AppHost, Aspire, or ServiceDefaults project. Run Live, Full against `/tmp/ext-host-1-full`, or any OQ-5.4-1 phase. Mark either record Available. Push. Invent `PLAT-ACTOR-HISTORY-1` as a register row or install a history backend.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Supplied signing credential | Ephemeral P-256 key and matching current authority | Detached ES256 verifies; exact retry returns the original | Changed key, issuer, audience, or tenant writes nothing |
| Authority absent | Null credential, endpoint-only reference, or stale revision | No signature, replay append, or spool write | Deny; lookup stays Unknown |
| Lost spool acknowledgement | Append succeeds, acknowledgement is lost, worker restarts | Original stays pending, then one acknowledgement | Unknown proof is not success |
| Full pass | Complete fixture profile; thirteen cells Passed | Exit 0; evidence files exist; attempt digest matches those bytes | `dependencyAvailable` false |
| Full refusal | Missing profile, secret profile, or cell gap | Exit 1; prior attempt kept | No second suite for a different complete key |
| Packets | Clean commit of this delivery | Both `candidateCommit` values and both commands pin that SHA | `acceptedReplacementCommit` null |
| Cohort | Protection Uncommitted; anchors null; profile slots empty | Refusal record only | Command is not executed |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Secrets.Runtime/CustodyBindingDaprCall.cs` `Build` (lines 47–70) fabricates `issuer`, `audience`, `original-detached-signature`, and `authority`. Replace those literals with the supplied command and current-authority proof. Keep the digest and sort checks. `DispatchAsync` stays non-writing until a real witness exists.
- `src/Hexalith.Platform.Custody/DeletionBatchCapabilityCodec.cs` `Sign`/`Verify` is the only ES256 primitive to call. Do not mint a key inside it.
- `src/Hexalith.Platform.Custody/DeletionCapabilitySigningActor.cs` keeps null authority as `Unavailable`. Do not call `AddActor`.
- `src/Hexalith.Platform.Secrets.Runtime/CustodyBindingContract.cs` `ICustodyBindingCurrentAuthority` (lines 245–255) defaults deny. Add one private implementation that checks the supplied credential fingerprint over the exact request. Do not treat app id, binding name, or `ControlAdmissionProof` as authority.
- `src/Hexalith.Platform.Custody/PrivateOwnerOperationAuthenticator.cs` is the existing machine-and-grant check. Reuse it; do not give it an HTTP route.
- `src/Hexalith.Platform.Secrets.Runtime/DaprCapabilityAdapters.cs` `DaprReplayRegistrarAdapter` is transport only. Bind the private replay credential beside `AddPrivateReplay` (`SecretsRuntimeServiceCollectionExtensions.cs`). An absent credential or an endpoint used as a credential installs no registrar.
- `src/Hexalith.Platform.Custody/IReplicatedSecuritySpoolAuthority.cs` has no production class. `ReplicatedSecurityObservationSpool.CurrentAsync` already denies a null authority. Add the private authority and keep the worker on `AddPrivateSecurityObservationDrainWorker` only.
- `src/Hexalith.Platform.Custody/PlatformPrivateCapabilityComposition.cs` stays the only registration path. Do not resolve the raw registrar, spool, or worker from general DI.
- `eng/ext-host-1/verify_full.py` `verify` (lines 342–389) can return 0, but `write_attempt` stores no evidence digest and category files still match name tokens. Bind the digest the way `retain_prior_evidence` hashes files. Same-key replay must recheck it. Keep `REFUSAL`, `BINDING_GAPS`, and `MISSING_RUNTIME_INPUTS`.
- `eng/ext-host-1/prepare_acceptance.py` and `eng/ext-secrets-1/prepare_acceptance.py` already pin a clean HEAD and force `acceptedReplacementCommit` null. Run both; do not weaken their gates. `qualify.py` `IMPLEMENTATIONS` and `OQ-5.4-1.JointLivePhaseRunner` stay mandatory Live blockers.
- `eng/ext-secrets-1/provisioning/role-inputs.proposed.json` and `runtime-reference.example.json` keep `publicAnchorFile` null. The prerequisite document names each empty slot and its owner action.
- `docs/implementation/ext-parties-1-platform-prerequisites-2026-10-18/` assigns retained history to symbolic `PLAT-ACTOR-HISTORY-1`. Prepare that prerequisite list. Do not implement the backend.
- `_bmad-output/implementation-artifacts/spec-ext-secrets-1-story-5-4-completion.md` already split endpoint and credential references. Do not reopen those review fixes.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Platform.Secrets.Runtime/CustodyBindingDaprCall.cs` and `src/Hexalith.Platform.Custody/PrivateCustodyBindingAuthority.cs` -- bind real ES256 and exact current-authority checks; deny when the credential is missing, changed, or stale -- removes fabricated signing literals.
- [ ] `src/Hexalith.Platform.Secrets.Runtime/PrivateReplayCredentialBinding.cs` and `src/Hexalith.Platform.Secrets.Runtime/SecretsRuntimeServiceCollectionExtensions.cs` -- bind the private replay credential only as a constructor capture; install no registrar when the credential is absent or is only an endpoint -- keeps replay off public and Workflow paths.
- [ ] `src/Hexalith.Platform.Custody/PrivateReplicatedSpoolAuthority.cs` and `src/Hexalith.Platform.Custody/PlatformPrivateCapabilityComposition.cs` -- authorize observe, drain, and aged recovery only for the supplied credential; keep the existing spool and worker -- a null authority still writes nothing.
- [ ] `tests/Hexalith.Platform.Custody.Tests/PrivateStory54AuthorityTests.cs` and `tests/Hexalith.Platform.Secrets.Runtime.Tests/PrivateReplayCredentialBindingTests.cs` -- cover the matrix rows for signing, denial, and lost acknowledgement -- proves the private paths without a deployed authority.
- [ ] `eng/ext-host-1/verify_full.py` and its tests -- store and recheck the evidence digest; exit 0 only for a complete fixture profile whose thirteen cells Passed -- makes the persisted pass checkable.
- [ ] `docs/implementation/ext-story-5-4-shared-prerequisites-2026-10-22.md` -- list the 192.168.1.30 profile slots, credential references, null trust anchors, protection bindings, retained-history prerequisites, and the unmet OQ-5.4-1 gates -- prepares qualification without executing it.
- [ ] One commit of this delivery, then both `prepare_acceptance.py` scripts into one fresh directory outside the checkout -- both packets pin that SHA -- returns the shared candidate.

**Acceptance Criteria:**
- Given the committed tree, when both packet files are read, then `candidateCommit` and the embedded `git rev-parse HEAD` pin are the same full SHA and `acceptedReplacementCommit` is null.
- Given the prerequisite document, when OQ-5.4-1 is inspected, then its command was not executed and the recorded outcome is `DependencyNotAvailable`.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Evidence authentication is the SHA-256 of the persisted Full evidence files, stored on `component-attempt.json` and recomputed before a same-key replay returns success. No witness key is enrolled, so the attempt must not carry a generated signature. Category evidence keeps the exact Passed cell names; a token inside a different test name is not a required cell.

## Verification

**Commands:**
- `dotnet test` on the custody and secrets-runtime test projects, filtered to the new authority, replay, spool, and signing tests -- expected: pass, with the denial cases asserting zero writes.
- `python3 -m unittest` for `eng/ext-host-1` and `eng/ext-secrets-1` -- expected: the fixture Full path exits 0 with a matching digest; Live and cohort tests still refuse.
- `git rev-parse HEAD` compared with both packet `candidateCommit` fields -- expected: equal, and `git status --porcelain` empty after the commit.
