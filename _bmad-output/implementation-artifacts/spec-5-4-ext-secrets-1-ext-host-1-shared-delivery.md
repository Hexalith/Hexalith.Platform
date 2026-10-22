---
title: 'Shared Platform delivery for Agents Story 5.4'
type: 'feature'
created: '2026-10-22'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 9a43e1ad8678db4016cc78db63545984cee727c8
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
- [x] `src/Hexalith.Platform.Secrets.Runtime/CustodyBindingDaprCall.cs` and `src/Hexalith.Platform.Custody/PrivateCustodyBindingAuthority.cs` -- bind real ES256 and exact current-authority checks; deny when the credential is missing, changed, or stale -- removes fabricated signing literals.
- [x] `src/Hexalith.Platform.Secrets.Runtime/PrivateReplayCredentialBinding.cs` and `src/Hexalith.Platform.Secrets.Runtime/SecretsRuntimeServiceCollectionExtensions.cs` -- bind the private replay credential only as a constructor capture; install no registrar when the credential is absent or is only an endpoint -- keeps replay off public and Workflow paths.
- [x] `src/Hexalith.Platform.Custody/PrivateReplicatedSpoolAuthority.cs` and `src/Hexalith.Platform.Custody/PlatformPrivateCapabilityComposition.cs` -- authorize observe, drain, and aged recovery only for the supplied credential; keep the existing spool and worker -- a null authority still writes nothing.
- [x] `tests/Hexalith.Platform.Custody.Tests/PrivateStory54AuthorityTests.cs` and `tests/Hexalith.Platform.Secrets.Runtime.Tests/PrivateReplayCredentialBindingTests.cs` -- cover the matrix rows for signing, denial, and lost acknowledgement -- proves the private paths without a deployed authority.
- [x] `eng/ext-host-1/verify_full.py` and its tests -- store and recheck the evidence digest; exit 0 only for a complete fixture profile whose thirteen cells Passed -- makes the persisted pass checkable.
- [x] `docs/implementation/ext-story-5-4-shared-prerequisites-2026-10-22.md` -- list the 192.168.1.30 profile slots, credential references, null trust anchors, protection bindings, retained-history prerequisites, and the unmet OQ-5.4-1 gates -- prepares qualification without executing it.
- [x] One commit of this delivery, then both `prepare_acceptance.py` scripts into one fresh directory outside the checkout -- both packets pin that SHA -- returns the shared candidate.

**Acceptance Criteria:**
- Given the committed tree, when both packet files are read, then `candidateCommit` and the embedded `git rev-parse HEAD` pin are the same full SHA and `acceptedReplacementCommit` is null.
- Given the prerequisite document, when OQ-5.4-1 is inspected, then its command was not executed and the recorded outcome is `DependencyNotAvailable`.

## Implementation Notes

Commit `60472462e4b43f4d328260199b22655639d36dd3` binds supplied ES256 custody, current-authority, replay, and spool credentials. `CustodyBindingDaprCall.Build` returns null without that authority, and `DispatchAsync` still writes nothing. Full stores `evidenceDigest` and a same-key replay with changed evidence exits 1. Both packets are under `/tmp/ext-story-54-shared-60472462` and pin that SHA with `acceptedReplacementCommit` null. OQ-5.4-1 was not executed. Verification: custody authority tests 3 passed, secrets binding tests 8 passed, `eng/ext-host-1` 13 passed, `eng/ext-secrets-1` 216 passed. Review required the same-key replay to recheck the digest for every stored code and to read the thirteen Passed cells before returning 0. The current replay registrar and the recorded aged-recovery proof are now asserted. `references/Hexalith.Builds` stayed outside the commit.

## Spec Change Log

## Review Triage Log

| ID | Verdict | Evidence |
| --- | --- | --- |
| B1 | false | `DispatchAsync` calls `Build(command)` and returns refused when the authority is absent. The code map keeps that path non-writing until a witness exists. Supplied keys sign through `Sign` and `Build(command, authority)`, which the tests call. |
| B2 | false | The spec says a proof, app id, or binding name is not authority. `AuthorizeAsync` checks the bound issuer, audience, tenant, revision, and request digest. The other interface methods keep their default deny. `attempt-1` is a delivery label, not the authorization decision. |
| B3 | false | Lookup and destroy build the same capability bytes, so the cached signature is the signature of those bytes. Guard ordinals stay placeholders because this delivery does not invent a protection witness. |
| B4 | false | The authorizer recomputes the fingerprint and requires the in-memory bind. The three public anchor files stay null. The SPKI hash is the supplied credential's id, not an enrolled anchor file. |
| B5 | low | A disposed key or two concurrent `Sign` calls can throw, and the PKCS#8 export is not zeroed. Callers use one authority on one thread. Locks and disposal add complexity for a path everyday use does not hit. Rejected. |
| B6 | low | `_credential` is not read after `Create`, and `file://` is treated differently from `file:/`. Host composition correctly keeps a null authority because the spec forbids minting a credential. Rejected. |
| B7 | false | Observe and drain in `LostSpoolAcknowledgementStaysPendingUntilTheWorkerRestarts` pass through this authority, so its digest matches `Prepare` for that path. `RecordRevisionAsync` returning false is the deprecated hook. The spool's `Exact` check still rejects an unrelated receipt before recovery completes. |
| B8 | medium | `AddPrivateReplay` installs a current binding, but the new test only calls `RegisterAsync` on `AsStale()`. A current binding that never calls the invoker would stay green. |
| B9 | high | Same-key replay returns stored `exitCode` 0 whenever `evidenceDigest` matches. Changing a failed attempt's `exitCode` from 1 to 0 returns success without reading the thirteen cells. |
| B10 | false | The test leaves the receipt null while lookup is `Unknown`, then one worker pass acknowledges it and `PhysicalAppends` stays 1. |
| B11 | false | `2026-10-08` is the required profile date. The prerequisite record states the command was not run and the outcome is `DependencyNotAvailable`. `baseline_commit` is the start commit; the notes name the delivery commit. |
| E1 | low | `IsP256` can throw if curve export fails outside `Create`'s catch. A bad key already fails creation. Rejected. |
| E2 | low | Concurrent `Sign` can throw on `_originals.Add`. The authority is not shared across threads. Rejected. |
| E3 | low | A null command throws on `command.Operation`. Callers pass a command. Rejected. |
| E4 | medium | `Build` checks only `targets[0].TenantId` against the authority. A later target can name another tenant. The previous `Build` also signed every target. Deferred as pre-existing. |
| E5 | medium | Enrollment records only the first identity's store. The previous `Build` did the same. Deferred as pre-existing. |
| E6 | low | `AuthorizeAsync` does not recompute `OriginalRequestDigest` after a caller mutates the lifecycle. `Build` stamps that digest before return. Rejected. |
| E7 | low | An IPv6 `host:port` without `://` can pass `IsEndpoint`. Replay still requires `IsReference`, and ordinary references are `file:` paths. Rejected. |
| E8 | low | `AuthorizeAsync` accepts a non-null Observe intent. The spool calls `ValidateIntent` before the authority. Rejected. |
| E9 | low | `AuthorizeOriginalLookupAsync` accepts a non-null lookup. The spool validates the lookup before calling it. Rejected. |
| E10 | low | `VerifyAgedOriginalRecoveryAsync` can return true for a `Recorded` proof whose receipt is not the original. `RecoverOriginalAsync` also requires `Exact` and returns null. Rejected as an authority-only gap the spool already closes. |
| E11 | low | `AddMinutes` near `DateTimeOffset.MaxValue` can throw. The fixture clock is not there. Rejected. |
| E12 | low | Concurrent admit can corrupt the maps. The worker loop is single-threaded and the authority is not registered for general dispatch. Rejected. |
| E13 | false | `AdmitTransitionAsync` does not hash `TargetBytes`, and `RecordTransitionAsync` does. A mismatched admit fails closed at record. The passing observe path uses `Prepare`, which sets both. |
| E14 | false | `CommitAsync` records a transition once. The passing observe path advances the anchor. A second record of that same transition is not how the spool commits. |
| E15 | false | A null `TargetBytes` throws inside `RecordTransitionAsync`. The spool catches that and returns no effect. |
| E16 | low | `_anchorRevision + 1` wraps at `long.MaxValue`. The anchor starts at 0. Rejected. |
| E17 | low | The spool copy of `IsEndpoint` has the same IPv6 gap as E7. Rejected. |
| E18 | low | A vanished evidence file makes `evidence_digest` throw instead of returning 1. The replay test changes bytes; it does not delete the file. Rejected. |
| E19 | high | Same defect as B9. A stored exit of 0 is replayed when the hash still matches, including after the exit field is edited. |
| E20 | medium | A non-zero same-key replay returns that code without comparing `evidenceDigest`. Replaced failure evidence stays a failure, but the digest is not checked. Same replay decision as B9. |
| E21 | high | Claim form of B9. A failed attempt can be replayed as success. |
| V1 | medium | Pre-verified. No test calls `RegisterAsync` or `LookupAsync` on the current binding `AddPrivateReplay` installs. |
| V2 | medium | Pre-verified. No test asserts `VerifyAgedOriginalRecoveryAsync` is true once the issued grant's proof is `Recorded` with a receipt. |

## Design Notes

Evidence authentication is the SHA-256 of the persisted Full evidence files, stored on `component-attempt.json` and recomputed before a same-key replay returns success. No witness key is enrolled, so the attempt must not carry a generated signature. Category evidence keeps the exact Passed cell names; a token inside a different test name is not a required cell.

## Verification

**Commands:**
- `dotnet test` on the custody and secrets-runtime test projects, filtered to the new authority, replay, spool, and signing tests -- expected: pass, with the denial cases asserting zero writes.
- `python3 -m unittest` for `eng/ext-host-1` and `eng/ext-secrets-1` -- expected: the fixture Full path exits 0 with a matching digest; Live and cohort tests still refuse.
- `git rev-parse HEAD` compared with both packet `candidateCommit` fields -- expected: equal, and `git status --porcelain` empty after the commit.
