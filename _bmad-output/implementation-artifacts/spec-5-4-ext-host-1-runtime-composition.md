---
title: EXT-HOST-1 runtime composition for Agents Story 5.4
type: feature
created: 2026-10-17
status: in-review
route: dispatch
review_loop_iteration: 0
baseline_commit: b49d5c1cbfc79e94d92d6228e7d2b3c4f65b56f9
context:
  - /home/administrator/projects/hexalith/agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - docs/ext-host-1-agents-composition.md
  - references/Hexalith.Agents/_bmad-output/implementation-artifacts/tests/owner-delivery-2026-10-08/owner-inputs.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EXT-HOST-1 stays Committed at `f448db29493c1b30b0f18380188fe99b8b7606fe`. Agents enablement and Full verification refuse before any Agents resource exists. Replay, the replicated spool, its worker, and the denial recorder are unbound, so Story 5.4 has no deployment profile, tested candidate, or replacement packet.

**Approach:** Prepare Platform-owned composition of Agents with EventStore, Tenants, Parties, Conversations, and the existing private replay, spool, worker, and recorder. Bind current tenant, global administrator, human identity, and approver evidence from a reference-only profile. Verify signed operation, principal, and scope, then register issuer and nonce before target-command construction. A spool failure blocks that command. Lost acknowledgements recover once. The Full verifier persists ACL, isolation, outage, restart, and restore evidence and preserves protection and attestation. Return the profile, candidate, and detached packet. Full qualification waits for the required Parties and Conversations bindings.

**Decision:** Keep this spec whole. Bind credential slots only. The profile requires the spool backend, replica count, failure model, and replay, recorder, and worker references. This run installs nothing and mints nothing. Missing references stay empty, and the packet names each one. The verifier remains a runner over the existing custody and EventStore guard tests.

## Boundaries & Constraints

**Always:** Reuse `PlatformAppModel.ComposeEntry`, `AddPrivateTrustedEnvelopeVerifier`, `AddPrivateSecurityDenialRecorder`, `AddPrivateSecurityObservationDrainWorker`, `TrustedEnvelopeReplayVerifier`, `ReplicatedSecurityObservationSpool`, `ReplicatedSecurityObservationSpoolWorker`, `PlatformSecurityDenialRecorder`, and the FR-34 startup and readiness gates. Keep registrar, spool, and worker credentials as constructor captures. Register `(Issuer, DeliveryNonce)` only after cryptographic validation. Acknowledge a drain only on an exact `SecurityEventRecordReceipt`. Profile values are host `192.168.1.30`, date `2026-10-08`, Branch B, and L/S/O/H/R = 300/30/600/86400/604800. Without an accepted profile, Agents enablement keeps today's refusal sentence. Works and declaration lanes stay unchanged. Full mode with missing Parties or Conversations bindings writes blocked evidence and exits 1 with `DependencyNotAvailable: EXT-HOST-1`. `LocalScaffold` stays the Debug scaffold build. Packet schema `EXT-HOST-1.replacement-acceptance.v2` keeps `acceptedReplacementCommit` null and availability, live authorization, and production mutation false.

**Never:** Add Agents AppHost, Aspire, or ServiceDefaults projects. Register raw credentials for general DI. Install a no-op protection wrapper, a new database, a fake spool, or real spool storage. Mint replay, recorder, or worker credentials. Approve human exact-Conversation or class/time-range scope. Edit the Agents register or copy secret bytes into git, logs, or evidence. Use the operator kubeconfig or OpenBao server secrets as application credentials. Change the secrets matrix or packet. Weaken the two existing Agents-refusal tests. Treat this run as Available or as Parties or Conversations qualification. Reimplement the EventStore governance guard.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Profile absent | Agents enabled, no accepted profile | Existing EXT-HOST-1 refusal before resources | No command and no spool write |
| Spool unavailable | Valid signed envelope; observe null or spool not ready | No target command; `SecurityAuditUnavailable` | Readiness blocks tenant-access and audit |
| Lost acknowledgement | Append succeeds, acknowledgement lost, worker restarts | Original stays pending; one security event | Unknown acknowledgement is not success |
| Binding absent | Parties or Conversations reference missing | Persisted blocked ACL, isolation, outage, restart, and restore evidence | Exit 1 `DependencyNotAvailable: EXT-HOST-1`, zero effects |
| Credential slots empty | Spool or replay, recorder, or worker reference missing | Profile validation fails; no resource is installed | Packet names each missing reference; zero cluster calls |
| Packet | Clean candidate, qualification blocked | Detached v2 packet with profile hash, plan hash, candidate, and exact command | `acceptedReplacementCommit` null; zero live effects |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Composition/PlatformAppModel.cs` — `ComposeEntry` lines 22–31. Keep the refusal when the profile is absent.
- `src/Hexalith.Platform.Composition/WorksPreviewModel.cs` — copy the opt-in sibling-source failure behavior. Do not change Works or Identity.
- `src/Hexalith.Platform.Composition/WorksProductionProfile.cs` — copy the reference-only validator shape.
- `src/Hexalith.Platform.Composition/Declarations/tenants.json` — bind `Tenants:BootstrapGlobalAdminEnabled` and `Tenants:BootstrapGlobalAdminUserId` from the profile.
- `src/Hexalith.Platform.Custody/PlatformPrivateCapabilityComposition.cs` — call the three private Add methods. Do not register raw objects.
- `src/Hexalith.Platform.Custody/TrustedEnvelopeReplayVerifier.cs`, `ReplicatedSecurityObservationSpool.cs`, `ReplicatedSecurityObservationSpoolWorker.cs`, `PlatformSecurityDenialRecorder.cs` — reuse verify, observe, drain, and readiness. Do not reorder replay or acknowledgement.
- `src/Hexalith.Platform.Custody/Fr34ProtectionGate.cs`, `Fr34ProtectionStartupGate.cs`, `Fr34ProtectionReadinessCheck.cs` — preserve startup and every-readiness attestation. No production engine is registered.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Security/GovernanceScopeGuardOwner.cs` — existing H3/H4/v21/v23 guard. The verifier runs its tests; it does not reimplement the guard.
- `eng/verify-agents-host.sh` — Full fails at line 45. LocalScaffold builds `apphost.cs`. Lines 39–42 forbid Agents hosting projects.
- `tests/Hexalith.Platform.Composition.Tests/TimesheetsHostBindingTests.cs:252` and `tests/Hexalith.Platform.Works.Tests/WorksPreviewModelTests.cs:41` — keep both refusals.
- `tests/Hexalith.Platform.Custody.Tests` — `ReplicatedSecurityObservationSpoolTests`, `ReplicatedSecurityObservationSpoolWorkerTests`, `TrustedEnvelopeReplayVerifierTests`, `PlatformSecurityDenialRecorderTests`, `Fr34ProtectionGateTests` are the persisted H1/H2 evidence source.
- Sibling roots `../eventstore`, `../tenants`, `../parties`, `../conversations`, `../agents` — missing checkout fails composition. Do not edit the Agents register.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Platform.Composition/AgentsDeploymentProfile.cs` — validate the reference-only profile: host, date, timing, five sibling services, current tenant, global administrator, human identity, approver evidence, replay, recorder, worker, spool backend, replica count, failure model, and protection engine/observer. Reject secrets and unknown keys.
- [x] `src/Hexalith.Platform.Composition/AgentsHostComposition.cs` — compose those services and the private capabilities. Verify signed operation, principal, and scope, then register issuer/nonce, before target-command construction. Block construction when the spool is unavailable.
- [x] `src/Hexalith.Platform.Composition/PlatformAppModel.cs` — enter that composition only with a valid profile; otherwise keep the current refusal.
- [x] `tests/Hexalith.Platform.Composition.Tests/AgentsHostCompositionTests.cs` — cover absent profile, spool failure, and lost acknowledgement.
- [x] `eng/ext-host-1/verify_full.py` and `eng/verify-agents-host.sh` — Full persists H1–H4/v21/v23 ACL, isolation, outage, restart, and restore evidence from the custody and EventStore guard tests. Missing Parties, Conversations, or protection bindings exit 1 with the existing sentence and zero effects. LocalScaffold stays unchanged.
- [x] `eng/ext-host-1/prepare_acceptance.py` — write the profile, candidate commit, and detached v2 packet outside the checkout.
- [x] Credential binding — require the spool and private replay, recorder, and worker reference slots. Install nothing. Name every empty slot in the packet.

**Acceptance Criteria:**
- Given Agents enabled and no accepted profile, when `ComposeEntry` runs, then it throws the existing EXT-HOST-1 sentence and adds no Agents resource.
- Given a valid profile and signed operation, when admission runs, then principal and scope are verified and issuer/nonce registration finishes before target-command construction.
- Given spool unavailability or a lost acknowledgement, when denial and drain run, then no target command is built and recovery appends one security event.
- Given missing Parties or Conversations bindings, when Full finishes, then persisted evidence names the gap and the process exits 1 with `DependencyNotAvailable: EXT-HOST-1`.
- Given a clean candidate, when preparation finishes, then the profile, candidate, and exact command are in `EXT-HOST-1.replacement-acceptance.v2` and `acceptedReplacementCommit` is null.

## Implementation Notes

Agents composition is opt-in through `Platform:Agents:DeploymentProfile`. An enabled flag without a valid profile still throws the existing EXT-HOST-1 sentence and adds no resource. Empty spool, replay, recorder, and worker slots fail validation and are named by the detached packet. This run installs no store and mints no credentials.

`AdmitAsync` checks spool readiness, then `TrustedEnvelopeReplayVerifier.VerifyAsync`, then readiness again, and only then builds the target command. `SignedAdmissionRegistersIssuerBeforeTargetCommand` covers that order. Lost acknowledgement keeps one append and a null receipt.

Full mode runs the custody and `GovernanceScopeGuardTests` suites, writes `acl.json`, `isolation.json`, `outage.json`, `restart.json`, `restore.json`, and `blocked.json`, and exits 1. Evidence from the implementation run is under `/tmp/ext-host-1-full/evidence` with custody 129 and guard 71 passing, `effects` 0, and gaps `partiesBinding`, `conversationsBinding`, `protectionEngine`, and `protectionObserver`.

`prepare_acceptance.py` on a clean temporary candidate wrote `EXT-HOST-1.replacement-acceptance.v2` with `acceptedReplacementCommit` null, 14 missing references, and zero effects. The dirty platform checkout is not a packet source.

SDK 10 `dotnet test` needs `global.json` `test.runner` `Microsoft.Testing.Platform`, `OutputType` `Exe` on the composition and Works test projects, and `--report-xunit-trx`. `--nologo` makes that runner report zero tests.

## Spec Change Log

## Review Triage Log

## Design Notes

Agents composition stays opt-in. Enablement without an accepted profile still refuses, so Works, Timesheets, and declaration hosts stay stable. Full verification records H1–H4/v21/v23 evidence while Parties and Conversations are unbound; that result stays blocked and keeps the existing refusal sentence. The packet proposes the candidate and does not authorize live effects.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj --filter AgentsHostCompositionTests` -- expected: pass.
- `dotnet test tests/Hexalith.Platform.Works.Tests/Hexalith.Platform.Works.Tests.csproj --filter AgentsCompositionStillRefusesUnavailableQualification` -- expected: pass.
- `bash eng/verify-agents-host.sh --mode LocalScaffold --artifacts-path /tmp/ext-host-1-local-scaffold` -- expected: exit 0.
- `bash eng/verify-agents-host.sh --mode Full --artifacts-path /tmp/ext-host-1-full` -- expected: exit 1, `DependencyNotAvailable: EXT-HOST-1`, blocked evidence, zero live effects.
- `python3 eng/ext-host-1/prepare_acceptance.py` with a clean candidate and a fresh directory outside the checkout -- expected: v2 packet, null `acceptedReplacementCommit`, zero effects.
