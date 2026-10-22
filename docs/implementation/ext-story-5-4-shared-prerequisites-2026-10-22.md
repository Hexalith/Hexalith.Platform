# Story 5.4 shared delivery prerequisites

Date `2026-10-08`. Host `192.168.1.30`. Parties branch B. Timing L/S/O/H/R = 300/30/600/86400/604800. This record prepares the shared EXT-SECRETS-1 and EXT-HOST-1 delivery. It installs nothing, fills no anchor file, and mints no key.

`dependencyAvailable`, `productionEngineRegistered`, `liveEffects`, and `acceptedReplacementCommit` stay false or null. `missingRuntimeInputs` stays `EXT-PROTECTION-1`, `trust-anchors`, and `OQ-5.4-1`.

## OQ-5.4-1

The OQ-5.4-1 command was not executed. The recorded outcome is `DependencyNotAvailable`.

EXT-PROTECTION-1 remains Uncommitted. `eng/ext-secrets-1/qualify.py` still treats these implementations as Live blockers: `IndependentRoleAppChannelAndAuthenticatedCallerScopes`, `InstalledOwnerQualificationExecutor`, `IndependentPersistedStateObserver`, `IsolatedTargetEnrollmentAuthority`, `QualifiedAllCopyLifecycleAdapter`, `QualifiedNonrollbackRestoreAdapter`, `IndependentCurrentInventoryAuthority`, `IndependentDecisionSignerAndActorRoleAuthority`, `PrivateExactReplayRegistrarBinding`, and `PrivateAuthenticatedCompromiseSubscriberBinding`. `cohort_prerequisite_inputs` still appends `OQ-5.4-1.JointLivePhaseRunner`. Neither record is Available.

## Host profile slots

`eng/ext-host-1/prepare_acceptance.py` leaves these profile slots empty: `currentTenant`, `globalAdministrator`, `humanIdentity`, `approverEvidence`, `replay`, `recorder`, `worker`, `spoolBackend`, `failureModel`, `protectionEngine`, `protectionObserver`, `partiesBinding`, `conversationsBinding`, and `replicaCount`. Service slots `services.eventStore`, `services.tenants`, `services.parties`, `services.conversations`, and `services.agents` stay relative checkout paths, not installed bindings.

Owner action: supply the accepted profile references for `192.168.1.30` before any Full run against `/tmp/ext-host-1-full`. This delivery does not fill those slots.

## Credential references and trust anchors

`eng/ext-secrets-1/runtime-reference.example.json` and `eng/ext-secrets-1/provisioning/role-inputs.proposed.json` keep `publicAnchorFile` null for executor, observer, and enrollment. Their credential reference fields stay null, including signer, Dapr API, app-channel, caller-authenticator, and caller-scope references. `apply` stays false.

Owner action: enroll the three public anchors and the credential references outside this checkout. Do not mint production keys or fill those files in this delivery.

## Protection bindings

Full still names these binding gaps: `partiesBinding`, `conversationsBinding`, `protectionEngine`, and `protectionObserver`. They stay empty in the default host profile. EXT-PROTECTION-1 is not committed by this delivery.

## Retained history

`docs/implementation/ext-parties-1-platform-prerequisites-2026-10-18/` assigns T2–T6 to the symbolic name `PLAT-ACTOR-HISTORY-1`. Installation orders 4, 5, and 6 are the retained-history prerequisite work: authority and copy decisions, selected adapters and host admission, then durable inventory and recovery hooks. That name is not a register row and this delivery does not install a history backend.

Owner action: resolve those prerequisites before any history installation. Do not add `PLAT-ACTOR-HISTORY-1` to the Agents register from this record.
