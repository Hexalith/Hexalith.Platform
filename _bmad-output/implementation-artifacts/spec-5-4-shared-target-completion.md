---
title: 'Complete the coordinated EXT-SECRETS-1 and EXT-HOST-1 target'
type: 'feature'
created: '2026-10-25'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '90aef9ac3e813d9cae5b16d68ca5e326ce0fb599'
context:
  - '../../../agents/_bmad-output/planning-artifacts/external-dependency-register.md'
  - '../../../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-25.md'
  - '../../docs/implementation/story-5-4-shared-target-completion-2026-10-25/source-and-installation-audit.json'
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

## External Delivery Inputs (not implementation checkpoints)

1. Which existing non-AWS native installation/resources should the provider target? Supply the public installation bundle, independent authority/archive placement, enrolled public trust, scoped credential references and existing physical proof. No new experiment or spending is authorized. The prior owner form still has no selected installation.
2. What explicit coordinated Platform and protection integration dates should the completed replacement propose? The accepted October 8 date remains historical; missing installation readiness prevents deriving a credible replacement promise.

## Code Map

- `infrastructure/Hexalith.Platform.Custody.Dapr/{SemanticProvider,RecoverableLifecycle,KeyExchange,StatementVerifier}.cs`: reviewed lifecycle and signed authority rules; `Program.cs` currently selects the uninstalled AWS reference. The separate dirty non-AWS continuation is unreviewed and has an empty installed-provider registry.
- `src/Hexalith.Platform.Secrets.Runtime/`: reuse `CustodyLifecycleClient`, `DaprCustodyAuthorizerClient`, `CustodyOwnerAuthority`, `DaprCustodyKeyLifecycleProvider`, qualification protocol/host and Dapr credential transport.
- `src/Hexalith.Platform.Custody/`: reuse private capability capture, replicated spool and worker. `PrivateReplicatedSpoolAuthority` retains authority in process dictionaries; `ISecurityObservationRecorder` lacks a production implementation.
- `src/Hexalith.Platform.Composition/AgentsHostComposition.cs`: Development-only; AppHost DI does not configure child-process DI.
- `eng/ext-secrets-1/{qualify,cases}.py`: signed evidence, original journal and 234-case plan. `eng/ext-host-1/verify_full.py` proves thirteen component cells only.

## Tasks & Acceptance

**Execution:**
- [ ] `infrastructure/Hexalith.Platform.Custody.Dapr/` — implement the selected eligible native provider and independent authorizer/control/observer receiving services; require original controls and actual native closure evidence.
- [ ] `src/Hexalith.Platform.Secrets.Runtime/` — implement bounded authenticated registration/manifest original locators and current owner-call authority; compose lifecycle API/DI without root-byte disclosure.
- [ ] `src/Hexalith.Platform.Secrets.Qualification.{Executor,Observer,Enrollment}/Program.cs` — register independently authenticated role providers, durable originals, current trust and public verification.
- [ ] `src/Hexalith.Platform.Custody/` — implement durable independent spool authority and exact recorder; bind restricted replay, compromise subscriber and recovery worker in the owning process.
- [ ] `src/Hexalith.Platform.Composition/` and `src/Hexalith.Platform.Protection/` — complete production host composition and installed owner bindings while preserving consumer refusal gates and full H1–H4/v21/v23 scope.
- [ ] `eng/ext-secrets-1/joint_live_phase_runner.py` — coordinate four ordered phases with an exclusive retained attempt, signed chained results, exact uncertainty lookup and disjoint complete case partitions. Include all S1–S4/v23 cases and explicit recovery-case assignments.
- [ ] `eng/ext-secrets-1/qualify.py`, `eng/ext-host-1/verify_full.py` — route both owners through the same complete cohort result; retain separate component evidence.
- [ ] `AgentsDeploymentProfile.cs` and both `prepare_acceptance.py` scripts — replace literal date enforcement with strict calendar validation and equality to accepted register/packet/profile dates; bind full plans and actual trust bytes.
- [ ] `tests/` and `eng/ext-{secrets,host}-1/test_*.py` — test scope denial, restart/restore, exact recovery, date substitution, phase ordering, complete coverage and pair failure without live cohort effects.
- [ ] `docs/implementation/story-5-4-shared-target-completion-2026-10-25/` — retain reviewed source, installation gaps, actual hashes and both complete nine-field records/extensions at one canonical target; prepare executable commands for later acceptance.

**Acceptance Criteria:**
- Given an incomplete installation, when admission or packet preparation runs, then it refuses and names exact missing inputs without claiming installed trust or availability.
- Given complete independently enrolled inputs, when preparation runs, then both packets bind one target, explicit dates, exact commands and actual configuration/profile/plan/trust hashes.
- Given an accepted set in isolated runner tests, when a phase fails or becomes uncertain, then neither record passes and recovery retains the original identity without repeating an unknown effect.

## Implementation Notes

User authorization 2026-10-25: proceed with all reversible source implementation and isolated verification now; the planning checkpoint is overridden. Missing installation choices, enrolled inputs and integration dates remain external delivery blockers, not implementation gates. Preserve the frozen intent verbatim, existing staged Story 2.5 probes, and dirty `references/Hexalith.Builds` work. Do not stage, commit, push, execute a cohort/Live/Full owner command, reserve a live attempt, activate a consuming seam, install anything, conduct a physical experiment, spend money or mutate accepted records. Fixture-generated anchors/hashes/identities are restricted to isolated tests and must never be presented as installed qualification. Source completion and complete shared-target delivery must be reported separately; leave delivery blocked where installed evidence or owner dates are missing. Complete independent review after implementation.

Remote/current-source check 2026-10-25: fetched `origin` without changing the checkout. Current local HEAD is `8b712a0` (resolve the canonical full identifier directly from Git for evidence); `origin/main` is `90aef9ac3e813d9cae5b16d68ca5e326ce0fb599`. The existing baseline above is retained. No non-AWS continuation is present in this checkout; the Code Map's prior mention is historical investigation, not an implemented provider. Existing OpenBao/native registry/local transport assets may support provider implementation but do not prove all-copy physical erasure or eligibility. Select only operations supported by those assets and fail closed for uninstalled independent controls and physical evidence.

Implementation decisions: strict `yyyy-MM-dd` real calendar dates must come from an explicitly accepted register/packet/profile context and agree across the coordinated set; no replacement date is inferred. Independent authority, control and observer services must use authenticated bounded request scopes, durable exact originals and externally enrolled current trust; unavailable dependencies deny before effects. Complete the source wiring even when real installations are absent, with an explicit installation ledger of all dependencies. Preserve every verbatim commitment field/extension from the accepted records, including S1–S4/v23, H1–H4/v21/v23, 365-day history and open Product gates. Packet preparation must refuse a complete output if installed inputs/dates are absent. If an immutable new source commit is unavailable, record working-tree source hashes separately and block the target field instead of inventing a commit.


Investigation only. No implementation task is closed. Full S1–S4/v23 and H1–H4/v21/v23, 365-day history and open Product gates remain binding. Existing staged Story 2.5 work and concurrent owner changes were not modified. Remote source was fetched; the audit records observed full revisions and public-file hashes. The frozen intent is preserved verbatim from the prior coupled-delivery draft. No approval, enrollment, cohort, experiment or spending occurred.

## Spec Change Log

## Review Triage Log

## Verification

After implementation: focused custody/runtime/composition/protection builds and tests, Python verifier regressions, independent review and scoped diff checks. Use fresh fixture directories. Never invoke Live/Full owner entrypoints or physical probes as delivery tests. Before implementation, validate the audit's public hashes and accepted sections; those static checks establish no runtime qualification.
