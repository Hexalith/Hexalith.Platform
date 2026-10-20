---
title: EXT-SECRETS-1 approved custody continuation
type: feature
created: 2026-10-20
status: in-progress
route: dispatch
baseline_commit: bb81fc9e98533246cfeed43ffcce08866a2a57f6
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="explicit continuation instruction; inherits approved custody work and Dapr constraint">

## Intent

Continue the approved EXT-SECRETS-1 custody work for Agents Story 5.4. Implement the next feasible source behavior, account for every historical qualification gap, prepare concrete provisioning artifacts where installed inputs are unavailable, and supply an immutable replacement target/command packet for acceptance before any new Live attempt.

## Boundaries & Constraints

Use existing Platform custody contracts and the approved Dapr-only erasure/recovery design. Preserve date 2026-10-08, host 192.168.1.30, L/S/O/H/R=300/30/600/86400/604800, Open overlapping-export disposition and independent approval private keys outside Platform/Agents. Preserve unrelated work with an isolated worktree. Record credential references only. Never infer a qualified provider from OpenBao health, return an irreversible receipt from a model, reset original journals, run the exhausted cohort, amend the Agents register or declare Available from local tests. Physical providers, independent runtime authorities and qualification roles are missing; report them explicitly.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Required result |
|---|---|---|
| Shared cut | Exact original complete manifest, prepared survivors, closed holders, retired alternate epochs | All selected paths cut together; surviving controls remain reachable |
| Incomplete preparation | Effective/unknown pin, tainted epoch, unresolved native send or Dapr holder | Arm/cut/publication blocked |
| Loss/restart/restore | B0–B5 interruption or old application snapshot | Preserve independent forward head and original; lookup starts no new effect |
| Duplicate/reissue | Changed manifest or delayed duplicate/key release after freeze | Conflict/block, never allocate replacement original or revive target |
| Missing installation | Historical 67-input report plus present source/configuration | Exact accounting, reference-only requests, zero qualification effects |

</frozen-after-approval>

## Code Map

- Current approved scope: `_bmad-output/specs/spec-ext-secrets-1-interaction-custody/{SPEC.md,implementation-plan.md,custody-binding-contract.md}`; next unit I1 offline feasibility, bounded I2 research. Adopted decision: `docs/implementation/ext-secrets-1-erasure-authority-decision-2026-10-20.md`, B0–B5 and F0.
- `src/Hexalith.Platform.Custody/CustodyKeyObjectIdentity.cs`, `CustodyKeyLifecycleActor.cs`: reuse complete identities and existing anchored original semantics. No new lifecycle provider/DI registration.
- `eng/ext-secrets-1/reconcile_inputs.py`, `prepare_acceptance.py`, `qualify.py`, `cases.py`: existing exact67, source-only packet generator and complete 234-case Live client. Preserve all acceptance/journal gates.
- `docs/implementation/ext-secrets-1-completion-2026-10-19/`: historical handoff; five supplied references are not current installation evidence.

## Tasks & Acceptance

**Execution:**
- [x] Add an internal, value-free offline feasibility model under `src/Hexalith.Platform.Custody`, with focused `CustodySharedCutModelTests` in the existing custody test project. It is explicitly a model, never a provider or DI service. Model exact original manifest identity, target/survivor epoch paths, B0–B5, whole-tenant freeze, effective/unknown holds, uncertain native sends/holders, tainted/alternate epochs, current authority loss, byte-identical terminal originals and nonrollback head across old application restore. No network, keys, credentials, provider SDK or synthetic Live provider.
- [x] Add bounded small-state exploration with concrete counterexample traces, crash/loss/restore at each boundary, delayed duplicate delivery/sidecar loss, changed originals, root reissue, rotation and explicit 1/1,000 target vectors. Include unsafe negative controls demonstrating partial-cut, survivor-loss or false completion detection. Explain modeled assumptions and unproved physical/transport properties.
- [x] Register the model class in `eng/verify-ext-secrets-1.ps1` Local audit. Run focused source checks and the complete Local gate with explicit existing clean EventStore/Builds inputs and unique external evidence outputs. No Live/cohort command.
- [x] Update reconciliation actions for the approved Dapr-only decision and concrete missing component/current-authority/holder-closure proof. Preserve every original 67 name and report new requirements separately. Regenerate fresh reference-only ledger/requests and handoff with exact owners/actions, hashes, source test results and incomplete runtime status.
- [ ] Commit only isolated continuation work, generate detached immutable candidate packet via existing tool, include its exact command/configuration/plan/anchor pins and missing acceptance/runtime inputs. Do not install or accept it.

**Acceptance Criteria:**
- Given modeled successful preparation, when shared cut/publication completes, then all selected targets and no survivor lose paths and the exact original vector survives lookup/restart/restore.
- Given unresolved copies, holders, current authority or holds, when a destructive transition is requested, then it is refused with a counterexample trace for intentionally unsafe alternatives.
- Given missing installed providers/independent anchors, when a replacement packet is prepared, then it remains unavailable and names the exact remaining inputs without executing qualification.

## Implementation Notes

This continues the approved `spec-ext-secrets-1-story-5-4-completion.md`; its source tasks were complete but runtime work remains in progress. The subsequent approved Dapr-only design explicitly selects I1 as the next implementable unit. Do not represent a model as implemented S1–S4/v23 Live behavior. The concrete physical/component feasibility and independent role implementations remain blockers, not operator-only configuration. Implementation subagent handles the first three tasks only; root completes reconciliation and packet tasks sequentially after it returns.

## Spec Change Log

## Review Triage Log

## Verification

Use `HexalithBuildsRoot=/tmp/ext-secrets-1-builds-20261019`, EventStore `/tmp/ext-secrets-1-eventstore-20261019`, and unique artifact/evidence paths. Build existing custody tests, execute `CustodySharedCutModelTests` with XML, then run complete Local gate. Python reconciliation and packet tests must pass. Persist content-free model traces and state-count/depth bounds. No native experiment or owner Live execution is authorized by this spec.

Continuation source validation: 737 custody and 132 runtime tests passed; post-provisioning Python suite 155 passed. Builds are warning/error free. Model: 740 states / 19,116 transitions / depth16, 28 boundary fault schedules and five unsafe controls. Historical67 ledger retained;53 required runtime fields remain missing;25 supplemental Dapr component/proof references remain null. No Live/cohort effects. Overall runtime outcome remains incomplete.
