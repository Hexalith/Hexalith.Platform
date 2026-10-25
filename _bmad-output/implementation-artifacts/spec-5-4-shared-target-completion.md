---
title: 'Complete the coordinated EXT-SECRETS-1 and EXT-HOST-1 target'
type: 'feature'
created: '2026-10-25'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 1
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

Initial remote/current-source check 2026-10-25: fetched `origin` without changing the checkout. Initial local HEAD was `8b712a05cdae8880ccd9c7f7dd355ec2ffe6952b`; fetched `origin/main` was `90aef9ac3e813d9cae5b16d68ca5e326ce0fb599`. The existing baseline above is retained. No non-AWS continuation was present at that check; the Code Map's prior mention is historical investigation, not an implemented provider. Existing OpenBao/native registry/local transport assets may support provider implementation but do not prove all-copy physical erasure or eligibility. Select only operations supported by those assets and fail closed for uninstalled independent controls and physical evidence. Concurrent external commits have advanced the shared checkout during this work; final source evidence must record the then-current full revisions and working-tree hashes without presenting them as an accepted complete target.

Implementation decisions: strict `yyyy-MM-dd` real calendar dates must come from an explicitly accepted register/packet/profile context and agree across the coordinated set; no replacement date is inferred. Independent authority, control and observer services must use authenticated bounded request scopes, durable exact originals and externally enrolled current trust; unavailable dependencies deny before effects. Complete the source wiring even when real installations are absent, with an explicit installation ledger of all dependencies. Preserve every verbatim commitment field/extension from the accepted records, including S1–S4/v23, H1–H4/v21/v23, 365-day history and open Product gates. Packet preparation must refuse a complete output if installed inputs/dates are absent. If an immutable new source commit is unavailable, record working-tree source hashes separately and block the target field instead of inventing a commit.


Investigation only. No implementation task is closed. Full S1–S4/v23 and H1–H4/v21/v23, 365-day history and open Product gates remain binding. Existing staged Story 2.5 work and concurrent owner changes were not modified. Remote source was fetched; the audit records observed full revisions and public-file hashes. The frozen intent is preserved verbatim from the prior coupled-delivery draft. No approval, enrollment, cohort, experiment or spending occurred.

## Spec Change Log


### Pass 1 source re-derivation

Independent review exposed receiving-service omissions, false packet completeness, missing exact recovery and durability boundaries. All are within the existing frozen intent. Re-derive the affected paths in place: concurrent external commits and unrelated work make broad reversion unsafe. KEEP the existing signed identity checks, real EventStore recording port, complete case partitions, no-resend uncertainty behavior, private credential captures, explicit-date checks and all working source/tests. No accepted record or unrelated file may be reverted.

Required corrections:

- Implement receiving source for every method invoked by installed qualification, owner-call, and FR-34 clients. Bind actual runtime/native/protection and independently persisted read ports; do not substitute another unimplemented remote method or self-reported success. Native authorizer must supply current verified key-closure digest and exact key-admission statements for wrap/unwrap. Source applications must compose these handlers with explicit installed dependencies and deny missing inputs.
- Independently verify native host/protection evidence and case invariants; bind every secrets proof to the current target, cohort attempt, exact case/request, installed configuration and plan. Historical originals require retained public-key signature verification and original-time validity; current authorization remains fresh separately. No owner-provided flag/receipt shape is authoritative observation.
- Validate complete joint configuration structure and all cross-artifact/date/target/trust/phase-profile pins during packet admission. Preserve exact installed raw configuration bytes and clearly retain canonical semantic hashes separately where the legacy runtime protocol requires them. Reject malformed shapes with named blockers. Replace binding rows rather than append duplicates. Both exact commands must match their coordinated commands and pass the accepted joint configuration path.
- Impose a monotonic total deadline on cohort RPCs. Carry reserved/dispatched/unknown effect state into failure evidence; never claim zero after dispatch. Keep exact original replay payload/time across retry, allow proven unsent/rejected recovery, and keep uncertain submissions lookup-only.
- Store immutable spool transition originals separately with a bounded independent conditional head and completion capacity; keep restart/restore exactness and original grants. Publish immutable files atomically only after flush, and fsync containing directories before advancing authority. Never overwrite conflicting originals.
- Add meaningful receiving-path tests: restart with null phase result must only lookup; missing/changed retained payload and revoked workload must deny private proof issuance; full positive receiving handlers and negative substitutions; raw formatting, duplicate binding replacement, malformed inputs, cross-bound complete packet, old original signatures, actual observer evidence and bounded calls. Synchronize renamed reconciliation obligations and run the full isolated Python suite. No live owner/cohort/physical action is authorized.

## Review Triage Log


### Review pass 1

| Finding | Verdict | Verified evidence | Route |
| --- | --- | --- | --- |
| BH1 | high | Installed qualification adapters call executor/observer/enrollment operations without receiving handlers; actual source handlers and executable composition are required. | bad_spec — re-derive affected path in place |
| BH2 | high | custody-owner-call has no receiving handler; current owner authority cannot function. | bad_spec — re-derive affected path in place |
| BH3 | high | FR-34 engine/reader/canary methods have no receiving handlers; startup/readiness cannot complete. | bad_spec — re-derive affected path in place |
| BH4 | high | Non-secrets observer validates receipt shape only, permitting fabricated owner evidence without authoritative reads or case invariants. | bad_spec — re-derive affected path in place |
| BH5 | high | q.verify_signed applies a 30-second current window to immutable historical phase originals, preventing delayed recovery. | bad_spec — re-derive affected path in place |
| BH6 | high | Packet admission does not validate joint configuration binding against target/dates/artifact/trust pins; invalid fixture binding prepares a complete packet. | bad_spec — re-derive affected path in place |
| BH7 | medium | Preparation compares raw installed configuration hash with canonical parsed serialization, rejecting valid reformatted files. | bad_spec — re-derive affected path in place |
| BH8 | medium | Replacement records append duplicate joint/host binding rows; consumers select obsolete first rows on subsequent replacement. | bad_spec — re-derive affected path in place |
| BH9 | high | urllib inactivity timeout does not impose an overall monotonic request deadline. | bad_spec — re-derive affected path in place |
| BH10 | high | Joint qualifier failures retain effects_started=false even after reserved/dispatched phases. | bad_spec — re-derive affected path in place |
| BH11 | high | Replay original is retained before proof/submission; unsent originals cannot progress and later timestamps change identity. | bad_spec — re-derive affected path in place |
| BH12 | high | Spool authority retains every complete transition snapshot twice in a capped single head; quadratic growth lacks archive/completion reserve. | bad_spec — re-derive affected path in place |
| BH13 | high | Immutable archive writes publish final paths before completion and omit parent directory fsync. | bad_spec — re-derive affected path in place |
| EC1 | high | Confirmed BH13: interrupted final-locator writes can expose partial originals and break exact recovery. | bad_spec — re-derive affected path in place |
| EC2 | high | Secrets original request is not cross-bound to current platform target and retained cohort attempt; other signed case results can be reused. | bad_spec — re-derive affected path in place |
| EC3 | high | Confirmed BH5: immutable proofs are incorrectly subject to current 30-second freshness. | bad_spec — re-derive affected path in place |
| EC4 | high | Confirmed BH4: fabricated host/protection receipts pass independent observer shape checks. | bad_spec — re-derive affected path in place |
| EC5 | high | Confirmed BH10: post-dispatch Blocked results report zero effects. | bad_spec — re-derive affected path in place |
| EC6 | medium | Malformed context/nested arrays raise AttributeError instead of a named blocked-input report. | bad_spec — re-derive affected path in place |
| EC7 | medium | Malformed accepted packet fields/types raise KeyNotFoundException or InvalidOperationException outside TryCompose refusal guards. | bad_spec — re-derive affected path in place |
| EC8 | medium | Confirmed BH7: raw-byte enrollment and canonical configuration hashes diverge. | bad_spec — re-derive affected path in place |
| EC9 | high | Native authorizer prepares null KeyClosureProfileDigest and omits custody-key-admission metadata; every wrap/unwrap is refused. | bad_spec — re-derive affected path in place |
| EC10 | high | Confirmed BH2: custody-owner-call receiver is absent. | bad_spec — re-derive affected path in place |
| EC11 | high | Confirmed BH3: FR-34 receiving operations are absent. | bad_spec — re-derive affected path in place |
| VG1 | high | Pre-verified regression gap: mutating RetainedPhaseOwner to repeat execution after unknown original leaves all ten coordinator tests passing; add actual receiving restart/lookup test. | patch |
| VG2 | high | Pre-verified regression gap: exact retained-original private command authorization has no exercised receiving/proof-source test; port tests substitute the proof source. | patch |
| VG3 | medium | Renamed cohort blocker is not synchronized with reconciliation obligations, causing a false RegisterConditionMetNotLiveVerified classification and four isolated-suite failures. | patch |

### Additional source verification during pass 1 correction

| Finding | Verdict | Verified evidence | Route |
| --- | --- | --- | --- |
| R1A1 | high | OpenBao Transit context supplies derivation rather than the required AEAD associated data; the adapter must pin derived/non-derived metadata and bind the canonical authority, tenant, purpose and epoch on encryption, decryption and migration. | bad_spec — re-derive affected adapter in place |
| R1A2 | high | Spool current/readiness/authorization could succeed without usable immutable archive capacity, and filesystem-root capacity did not measure the enrolled archive mount. | bad_spec — require actual archive admission and completion reserve |
| R1A3 | high | An unchanged native event stream alone did not prove a dispatched command was not recorded; queued/uncertain transport status must remain unknown, with exact status and a confirming native reread for terminal rejection. | bad_spec — preserve uncertainty and native proof ordering |
| R1A4 | high | Recovery reused an expired grant indefinitely; renewal must follow definitive exact-original native proof and current authorization, preserve every old signed grant, and keep bounded current state. | bad_spec — renew and archive grants without changing the original |
| R1A5 | high | The generic recorder Originals dictionary had a lifetime 10,000-item ceiling in one conditional head; immutable indexed originals and a bounded CAS root are required for continued operation. | bad_spec — retain complete immutable originals behind a bounded index |
| R1A6 | high | Resubmitting a definitively rejected command with the same transport identity only replays its terminal SDK result; recovery needs a retained grant-bound transport identity while preserving the logical original and keeping unknown attempts lookup-only. | bad_spec — retain deterministic recovery transport identities |
| R1A7 | medium | Rewriting relative public-anchor locators during detached preparation changes enrolled semantic bytes; preserve exact raw bytes and refuse unsupported relative installed locators explicitly. | bad_spec — preserve raw enrollment and require original absolute locators |

These corrections are source requirements within the existing frozen intent. Isolated regression passes establish no installed admission, physical property, accepted date or dependency availability. The source inventory and installation ledger must distinguish unfinished ordinary implementation from genuinely unselected external protocols.

## Verification

After implementation: focused custody/runtime/composition/protection builds and tests, Python verifier regressions, independent review and scoped diff checks. Use fresh fixture directories. Never invoke Live/Full owner entrypoints or physical probes as delivery tests. Before implementation, validate the audit's public hashes and accepted sections; those static checks establish no runtime qualification.
