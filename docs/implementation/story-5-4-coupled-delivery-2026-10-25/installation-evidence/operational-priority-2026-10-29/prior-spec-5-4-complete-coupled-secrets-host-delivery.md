---
title: 'Complete coupled Secrets and Host delivery for Agents 5.4'
type: 'feature'
created: '2026-10-25'
replanned: '2026-10-29'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '1b7a00381794bb7ecf40caf643c63c50758a81eb'
planning_baseline_commit: '965b00249b473d602a663f1f48be39424f996337'
context:
  - '../agents/_bmad-output/planning-artifacts/external-dependency-register.md'
  - '../agents/_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-28-mvp-custody.md'
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

1. **Scope:** retain approved DEC-5.4-MVP-CUSTODY-2026-10-28 operational-security priority (recommended; advanced custody/full acceptance deferred), or explicitly reopen full delivery with accountable non-AWS resources?
2. **Owner inputs:** supply public evidence references through `docs/implementation/story-5-4-coupled-delivery-2026-10-25/owner-decisions.json`, or keep delivery blocked? Required: preservation proof, provider facts, expired-profile recovery, backup/identity/decision/replay/spool evidence; reopened full scope also needs independent installations and physical originals.
3. **Dates:** supply coordinated owner-confirmed Platform/EventStore replacement dates, or keep full packets blocked? Historical dates/withdrawn estimates supply no replacement.

## Code Map

The [investigation](tests/story-5-4-coupled-delivery-2026-10-29-plan/investigation.md) maps symbols/tests; the [prior draft](tests/story-5-4-coupled-delivery-2026-10-29-plan/prior-spec.md) preserves history.

- `src/Hexalith.Platform.Secrets.Runtime/` — roles, signed authority, originals and private replay/spool composition exist; manifest publication/receiving-process adoption need closure.
- `src/Hexalith.Platform.Protection/QualificationOwnerDriverExecutable.cs` — recovery/export protocols refuse; FR-34/ERC source exists, installed qualification incomplete.
- `eng/ext-secrets-1/joint_live_phase_runner.py`, `coordinated_packets.py` and `src/Hexalith.Platform.Composition/AgentsDeploymentProfile.cs` — ordered recovery and explicit date/hash binding exist.

## Tasks & Acceptance

**Execution (full-delivery obligations; scope decision pending):**
- [x] Prepare installation planning/source investigation only.
- [ ] `owner-decisions.json` above — resolve scope and collect actual owner returns before dependent installation work.
- [ ] `src/Hexalith.Platform.Secrets.Runtime/` and qualification role programs — close original recovery/publication, authority/signing and independent role/store gaps.
- [ ] `src/Hexalith.Platform.Protection/` and Runtime owning-process composition — complete selected native drivers, actual FR-34 observer/engine, replay/spool/recorder and all host slots with accepted EventStore integration.
- [ ] `infrastructure/Hexalith.Platform.Custody.Dapr/`, `eng/ext-secrets-1/custody-deployment/` — if reopened, qualify non-AWS construction, independent resources/trust, ACLs and inventories.
- [ ] `tests/Hexalith.Platform.{Custody,Secrets.Runtime,Protection,Composition}.Tests/` and both verifier test directories — verify the matrix, ordering/tampering/loss, zero-effect denials and exact recovery in isolated fixtures.
- [ ] `docs/implementation/story-5-4-coupled-delivery-2026-10-25/` — after complete installation, prepare coordinated nine-field packets at one clean canonical Platform commit, actual hashes, owner dates and exact commands; execute none.

**Acceptance Criteria:**
- Given complete independently enrolled installations, when preparation runs, then both complete packets bind one canonical Platform commit, actual artifacts, explicit dates and executable commands.
- Given isolated recovery/order fixtures, when tests run, then phases cannot skip or relabel evidence and no accepted-set journal/cohort attempt is created.
- Given any missing prerequisite, when admission/preparation runs, then it refuses without promoting availability or replacing history.

## Implementation Notes

Planning only; frozen intent preserved. No runtime/installation/qualification operation ran. Prior notes/verification remain archived.

## Spec Change Log

- 2026-10-25 — Owner correction “AWS is not MVP”: removed AWS from active installation choices, resource/access requests, packaging/provisioning commands and tasks; withdrew AWS-derived date proposals. Retained historical evidence and all full-delivery/physical/independent-authority requirements.
- 2026-10-25 — Added the installation decision packet and required commands/owner returns using fresh read-only infrastructure/enrollment evidence. Narrowed backend/placement and date questions into concrete unaccepted choices; preserved the frozen intent, draft status, all implementation tasks, existing dependency records, timing, journals and OQ-5.4-1.
- 2026-10-29 — Refreshed source map/amendment; preserved frozen intent/history. Scope/owner returns remain unresolved.

## Review Triage Log

## Verification

Check frozen intent, revision, archived bytes and links; record concurrent staging. Use isolated suites; no Live/Full/native/cohort invocation.
