---
title: 'Complete coupled Secrets and Host delivery for Agents 5.4'
type: 'feature'
created: '2026-10-25'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '1b7a00381794bb7ecf40caf643c63c50758a81eb'
context:
  - '../agents/_bmad-output/planning-artifacts/external-dependency-register.md'
  - '../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-22.md'
  - 'tests/story-5-4-coupled-delivery-2026-10-25/investigation.md'
  - '../../docs/implementation/story-5-4-coupled-delivery-2026-10-25/installation-decision.md'
  - '../../docs/implementation/story-5-4-coupled-delivery-2026-10-25/installation-commands.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete the real JointLivePhaseRunner and installed production composition for EXT-SECRETS-1 and EXT-HOST-1. Implement current authority, signing/verification, private replay admission, replicated denial recording/recovery, protection and independent observation. Prepare complete coordinated replacement packets sharing one immutable Platform commit, explicit integration dates, executable commands and actual installed hashes. Source/component passes establish no availability.

## Boundaries & Constraints

**Always:** Host 192.168.1.30; Branch B; L/S/O/H/R=300/30/600/86400/604800. Reuse the reviewed Dapr-only custody foundation and ES256 primitives. Keep private credentials outside general DI/dispatch. Enroll independent executor/observer/enrollment trust and current/nonrollback authority outside the application restore cut. Preserve originals, uncertain outcomes and recovery identities.

**Never:** Run any OQ-5.4-1 phase or reserve its attempt during delivery. Modify accepted dependency records, consume Agents seams, self-approve replacement commitments, mint decision-approval evidence, substitute self-enrollment for independent trust, or claim physical erasure from fixtures/OpenBao deletion. Preserve existing journals and historical packets.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Authority | Current signed exact scope | Admit only the bound operation | Stale/revoked/substituted state denies before effects |
| Replay/spool | Duplicate, loss, restart, restore | Recover exact original; drain once under scoped ACL | Unknown remains pending; spool outage blocks ingress/readiness |
| Protection | Real engine and independent persisted observer | FR-34 at startup/every readiness; v21/v23 owner ordering | No-op, stale dispatch or incomplete closure denies |
| Cohort | Immutable accepted set | Future phases 0 protection, 1 secrets, 2 host/refusing consumers, 3 mutual | Preflight reserves nothing; changed bindings reject; recovery retains identity |
| Packets | Complete installed artifacts | Same full Platform commit; actual file/plan/anchor/image hashes | Missing installation cannot produce a complete packet |

</frozen-after-approval>

## Open Questions

1. **MVP constraint and installation D1:** the owner has ruled **AWS out of MVP**. Preserve existing Kubernetes/Dapr/OpenBao for its observed operational-secret scope. Choose MVP-1 (reuse an independently qualifying non-AWS semantic custody installation, with evidence) or MVP-2 (implement/qualify a non-AWS infrastructure backend behind the existing Dapr contract). No qualifying non-AWS installation was established; OpenBao/Raft revival remains a blocker for physical erasure. The old AWS accounts/regions/seven-key proposal and associated deployment work are superseded, not MVP prerequisites. See the [installation decision](../../docs/implementation/story-5-4-coupled-delivery-2026-10-25/installation-decision.md).
2. **Identity/access decisions D2–D8:** supply actual non-AWS custody/authority/archive host, appliance or service/store identities and read-only access; independent role compute/Dapr routing, named primary recovery administrator and different human deputy; independently enrolled public P-256 trust/current revocation and restricted credential references. The [owner return form](../../docs/implementation/story-5-4-coupled-delivery-2026-10-25/owner-decisions.json) names each missing implementation, resource, identity, access and physical-evidence return. No cloud account is presumed and no secret value is requested.
3. **Dates D9:** withdraw prior 2026-11-30 / 2026-12-03 / 2026-12-04 proposals because they were estimated around the excluded AWS route. Platform and EventStore owners must supply coordinated replacement dates based on the selected actual non-AWS provider's readiness/completion/recovery/qualification bounds and source/installation effort. Preserve accepted 2026-10-08 and EventStore's unaccepted 2026-11-02 as historical evidence until explicit complete replacement acceptance. Do not invent another provider interval or reserve an OQ attempt.

## Code Map

The investigation attachment maps exact files and missing services. Reuse `QualificationHost`, the Dapr lifecycle clients, `PlatformPrivateCapabilityComposition`, replicated spool/worker and existing protection providers. Qualification programs currently register no role providers; the spool authority uses process dictionaries; host AppHost DI does not configure child-process DI; Full remains component-only. No joint runner exists.

## Tasks & Acceptance

**Execution:**
- [x] Prepare the read-only installation decision packet, named proposed resource/role scopes, conditional command runbook, independently sourced public transport facts and exact D1–D9 owner returns. This completes planning only; no implementation/installation task below is closed.
- [ ] `src/Hexalith.Platform.Secrets.Runtime/` — implement durable original locators, authenticated current-authority/role sources and qualification providers/stores; retain complete original registrations/manifests.
- [ ] `src/Hexalith.Platform.Secrets.Qualification.{Executor,Observer,Enrollment}/Program.cs` — compose separate authenticated roles, scoped credentials, retained originals and independently enrolled signing/verification.
- [ ] `src/Hexalith.Platform.Custody/PlatformPrivateCapabilityComposition.cs` and `PrivateReplicatedSpoolAuthority.cs` — bind real current replay/spool authority, replicated storage, denial recorder and recovery worker in owning processes; preserve private access.
- [ ] `src/Hexalith.Platform.Protection/` and `src/Hexalith.Platform.Composition/AgentsHostComposition.cs` — compose the actual protection owner, persisted observer and FR-34 gates; complete every host slot and receiving custody API with accepted EventStore integration.
- [ ] `eng/ext-secrets-1/joint_live_phase_runner.py` (new), `qualify.py`, `eng/ext-host-1/verify_full.py` — add durable phase coordination and read-only preflight; keep component evidence distinct; test ordering, tampering, loss and exact recovery without a cohort invocation.
- [ ] `AgentsDeploymentProfile.cs`, both `prepare_acceptance.py` scripts and `eng/ext-secrets-1/custody-deployment/` — replace literal missed-date checks with accepted date/hash binding; install reviewed artifacts and independent enrollments using supplied resources, then verify actual hashes/ACLs/placement.
- [ ] Select/implement the actual non-AWS semantic custody infrastructure executable, closed profile, private credentials and deployment recipe; reuse provider-neutral lifecycle/ES256/original-recovery primitives. The current `infrastructure/Hexalith.Platform.Custody.Dapr` executable/profile/image preparer is AWS-specific reference source, not a configurable non-AWS MVP. Do not add its account/region/ARN/credential or regional-client work as MVP tasks. Complete actual independent stores, receiving-service/signer mounts and Dapr routing/identity for the selected backend without ambient credential fallback or unsafe retry.
- [ ] `tests/Hexalith.Platform.{Custody,Secrets.Runtime,Protection,Composition}.Tests/` and both verifier test directories — verify the matrix, zero-effect denials and all host consumer refusal assertions using isolated fixtures.
- [ ] `docs/implementation/story-5-4-coupled-delivery-2026-10-25/` (new) — retain installed inventories and coordinated nine-field replacement packets after one clean Platform commit; prepare exact commands without executing them.

**Acceptance Criteria:**
- Given complete independently enrolled installations, when packet preparation runs, then both complete packets pin one canonical Platform commit and the exact installed artifacts, explicit dates and executable commands.
- Given the delivered runner, when offline recovery/order tests run, then phases cannot skip, evidence cannot be relabeled, and no accepted-set journal or cohort attempt is created.
- Given any missing prerequisite, when production admission or packet validation runs, then it refuses without promoting availability or replacing historical evidence.

## Implementation Notes

The [installation packet](../../docs/implementation/story-5-4-coupled-delivery-2026-10-25/installation-decision.md) and [conditional command runbook](../../docs/implementation/story-5-4-coupled-delivery-2026-10-25/installation-commands.md) are required build inputs alongside the unchanged investigation. Proposed names are not installed identities. AWS is outside MVP; the reference executable, its two-tenant/five-epoch/seven-native-key profile and provider-specific preflight remain historical reference evidence. Implement/select and measure the actual non-AWS provider against all accepted limits and proof requirements; do not inherit reference resource counts as MVP requirements. Authority/currentness services need their own actual independently administered store bundle.

Fresh read-only observations on 2026-10-25 used kubeconfig `/home/administrator/.kube/hexalith-production`, context `jpiquot@local`, endpoint `https://192.168.1.30:6443`. The sole node `node1` maps to host 192.168.1.30; all inspected OpenBao and isolated-registry pods are on it. Both storage classes use local hostpath; neither three same-node replicas nor Retain reclaim policy proves independent recovery. No named custody/authorizer/qualification-role workload or lifecycle binding appeared in the inspected cluster. This establishes scoped cluster state, not global absence.

The retained actual runtime reference SHA-256 `31c28a9d1dd21eed8ca4d0c421088cb946b262c624ba461d0795cab92cd1feb3` still has 51 null fields and no role-anchor paths. The host profile still has 14 unfilled slots; its five service paths are checkouts. Protection accepted-profile and Conversations live-manifest remain absent. The earlier AWS local-access observation is historical and creates no MVP account/resource requirement. No installed hash is inferred for missing files. The observed public local Dapr CA and token audience are transport discovery, not independent qualification enrollment; their precise provenance/hashes are retained in the packet.

Source dispositions remain open for private authorizer, durable control issuer, independent closure observer, custody receiving API/composition, complete registration and reserved-manifest original locators, EventStore accepted source/package integration, actual role providers/original stores, current replay/spool authority and real FR-34 engine/observer. Credential references cannot close missing implementations. EventStore's G1–G7 remain open/BlockedNotSubmitted. Existing Open export and history-policy obligations are unchanged.

## Spec Change Log

- 2026-10-25 — Owner correction “AWS is not MVP”: removed AWS from active installation choices, resource/access requests, packaging/provisioning commands and tasks; withdrew AWS-derived date proposals. Retained historical evidence and all full-delivery/physical/independent-authority requirements.
- 2026-10-25 — Added the installation decision packet and required commands/owner returns using fresh read-only infrastructure/enrollment evidence. Narrowed backend/placement and date questions into concrete unaccepted choices; preserved the frozen intent, draft status, all implementation tasks, existing dependency records, timing, journals and OQ-5.4-1.

## Review Triage Log

## Verification

Original planning verification: fifteen public-metadata requests exited 0; follow-up public CA/token metadata retained separately. Offline `custody-deployment/preflight.py` exited 1 with 68 unique missing inputs and all installed/physical/availability/runnable flags false. See the packet's retained evidence, checksum manifest and preservation verification. No Live/Full entry point, qualification enrollment, worker, native probe, attempt reservation, deployment or dependency mutation was executed. This planning result does not satisfy implementation acceptance.

Run targeted dotnet suites and Python verifier/deployment tests against fresh isolated fixture directories. Check independent enrollment signatures, currentness, actual installed hashes and private ACLs through read-only installation checks. Retain command/exit/hash evidence. Do not run owner Live/Full entry points, native destructive probes, consuming Agents paths or OQ phases as verification of this delivery.

MVP correction verification: see `verification-mvp-no-aws.json` in the packet evidence directory. Current documentation and command syntax were checked without rerunning infrastructure/preflight/qualification. Earlier 68-field refusal describes the excluded reference runtime, not a mandatory non-AWS MVP input list.
