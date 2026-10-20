---
title: EXT-SECRETS-1 approved custody continuation
type: feature
created: 2026-10-20
status: done
delivery_status: source-verified-runtime-blocked
live_qualification: not-run-missing-installed-provider-and-authorities
route: dispatch
baseline_commit: bb81fc9e98533246cfeed43ffcce08866a2a57f6
review_loop_iteration: 1
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
- [x] Commit only isolated continuation work, generate detached immutable candidate packet via existing tool, include its exact command/configuration/plan/anchor pins and missing acceptance/runtime inputs. Do not install or accept it.

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

The first detached candidate packet was prepared from clean canonical commit b1e03da74b714683b2b281d076985a8e938f0769. Preparation returned DependencyNotAvailable with packetPrepared=true, zero effects and all runtime/acceptance blockers; no Live command was executed. Final source after independent review will receive a new immutable packet, preserving this preview.


## Review correction plan — iteration 1

The review identified insufficient modeling detail outside frozen intent. Re-derive only the model, its tests, model documentation and Local model-audit changes. Existing reconciliation/template/source packets remain preserved; root regenerates their model hashes and final candidate after verification. Prior implementation is retained in canonical commit b1e03da74b714683b2b281d076985a8e938f0769 and the external review diff.

KEEP complete identities and existing manifest digest/order validation; no provider/DI/network/keys; independent forward head and originals; source/successor/alternate/tainted path graph; shared cut; bounded explicit exploration; honest physical/transport assumptions; original1/1000 vectors and actor limits; all67 accounting and unavailable/acceptance gates.

Re-derive the first three tasks with these concrete requirements:

- Require expected current revisions for effect mutations, including omitted revisions from restored application callers. Explicitly test stale application revision after restore, then independently current same-authority reconciliation. Lookup remains read-only.
- Retain original survivor identities and permanent interaction allocation exclusions independently of mutable paths. Invariants reject omitted/changed survivors, whole selected-set loss before B4, mismatched cut count/boundary and any premature terminal result. Negative controls must exercise all these failures with accurate traces.
- Distinguish guard dispatch from B0 protection reservation. Selected effective/unknown holds before dispatch block the original; a post-dispatch selected pin fails even before custody receives B0. Model held survivors separately by complete identity so they remain recoverable without blocking selected erasure. Exact release cannot erase unrelated holds; holds/control mutations invalidate stale revision comparisons.
- Parameterize root allocation with a complete supplied identity. Reject previously allocated/erased interaction under changed alias/version/lifecycle/store, including after application restore, through retained exclusions. Generate only valid bounded model identities; test a valid 2048-byte source ObjectId. Preserve maximum1000 total registrations and permit a genuinely fresh survivor under successor.
- Represent one original native intent's Prepared/SendCommitted/ResultDurable/Closed and original producer/delivery identity within finite bounds. A delayed first staging delivery can arrive after freeze/sidecar loss and taints its destination; actual original completion/holder closure and permanent retirement are required before arm. Unknown send is never resent; duplicate delivery cannot mint another attempt. These remain symbolic, value-free physical premises.
- Make crash, lost acknowledgment, old application snapshot and sidecar-loss schedules identify the actual injected fault. Loss of sidecar after old buffers are already closed is safe but must still record the lost incarnation. For every boundary, assert observed lookup status AND returned original bytes before/after authority loss/recovery/restart/restore. An admitted unfinished original is Unknown, never NotStarted; publication returns byte-identical original bytes.
- Local audit removes its own prior model report before execution, requires the specific evidence-producing test to have run and passed, then hashes the fresh report. Source tests must demonstrate those guards; do not just require another arbitrary test in the class.

## Review Triage Log — iteration 1

| ID | Verdict | Evidence and route |
|---|---|---|
| B1 | medium | Omitted expectedHead bypasses comparison after RestoreApplication; model permits stale cut. Re-derive required current-revision semantics (bad_spec). |
| B2 | medium | Paths is sole survivor inventory; deleting all controls makes All checks vacuous. Retain independent membership (bad_spec). |
| B3 | medium | All selected paths None before B4 bypasses both partial and post-cut predicates. Strengthen independent invariants (bad_spec). |
| B4 | medium | Single tenant-wide hold cannot represent held-survivor lane; per-identity holds required by approved F0 design (bad_spec). |
| B5 | medium | BeforeB0 conflates pre-dispatch and already-dispatched intervals, allowing a later conflicting pin. Distinguish owner dispatch (bad_spec). |
| B6 | medium | One monotone uncertain flag/unconditional delayed-send refusal omits delayed first staging delivery. Model finite original native-intent phases (bad_spec). |
| B7 | medium | Crash/lost-response share the same branch and four sidecar schedules inject no lost incarnation. Make reported fault schedules concrete (bad_spec). |
| B8 | medium | Always-refused reissue action cannot test ordinary allocation under changed alias/version; retain exclusions and parameterize allocation (bad_spec). |
| B9 | low | Unsafe survivor trace says skip copy but mutation deletes already-prepared successor. Correct trace during re-derivation (patch, superseded by bad_spec). |
| B10 | medium | Local report path can contain old evidence and only arbitrary class execution is required. Require fresh report/specific producing method (patch, superseded by bad_spec). |
| E1 | medium | 2048-byte valid ObjectId gains19 bytes at allocation and violates existing custody validation. Test bounded identity generation (patch, superseded by bad_spec). |
| V1 | medium | Pre-verified: no test asserts returned observation bytes or unfinished Unknown. Verify actual lookup result at every boundary (patch, superseded by bad_spec). |

Correction iteration1 source verified:35 focused /744 custody /132 runtime /161 Python passing, zero failures/skips/unrun, warning/error-free builds. Exploration5025 states /131598 transitions /depth20,32 concrete fault schedules,13 unsafe controls. Correction manifest retained independently; updated ledger still accounts for original67 and53 missing runtime fields. The earlier counts above are historical; use correction1 final evidence.


## Review Triage Log — correction1

| ID | Verdict | Evidence and route |
|---|---|---|
| C1 | medium | RestartApplication refreshes ApplicationHead even during authority outage, contrary to its current-read claim. Patch process restart to leave revision stale without authority; test outage and later reconciliation. |
| C2 | medium | Compiled armed state with all selected paths changed to Successor passes invariants; Prepared later refuses cut but safety checker is incomplete. Add sole-Source armed invariant/control (patch). |
| C3 | medium | Compiled B4 survivor Source-only passes checker despite source cut. Add post-cut successor-only survivor invariant/control (patch). |
| C4 | medium | Invalid B5 without restore barrier still observes ModelPublished despite false-completion invariant. Fail observation closed and assert unsafe-control outputs (patch). |
| C5 | false | Rotate is expressly routine KEK/signing control rotation preserving original AAD/paths; erasure-epoch movement is PrepareSurvivors/Arm, not Rotate. Graph-affecting routine rotation would violate the adopted hierarchy; model claims no physical key/provider implementation. |
| C6 | false | The report/docs explicitly bound the model to one original native intent/delivery. Zero-intent admission and arbitrary manifests are outside this finite model; no production provider uses it to require manufacturing a send. No zero-intent success claim is made. |
| C7 | medium | AllocateRoot boundary guard forbids all pre-freeze allocation, making the specific freeze regression vacuous. Permit the one bounded new allocation under current Source before B1 and current Successor after B3; retain freeze/exclusion/limit refusal (patch). |
| C8 | medium | Fresh marker report can be paired with old XML because helper only deletes report and fixture pre-creates XML. Delete own prior XML, require test-run production of both, add missing-fresh-XML regression (patch). |
| C9 | medium | Ack-loss marker identifies boundary but schedules retry DuplicateExecute only after reconciliation. Assert exact original duplicate before reconciliation, retain original operation/boundary/revision in report (patch). |
| C10 | low | Counterexample traces name a valid baseline but omit its executed action/revision sequence. Emit actual successful baseline actions, mutation, violations and observed outcome (patch). |
| V2 | medium | Pre-verified replacement denial is masked by authority-unavailable guard and absent from exploration. Add available-authority prepared/published/restored exact-denial assertions (patch). |
| E2 | false | Edge-case layer returned no findings after corrected identity/allocation behavior. No unresolved edge-case report. |

All prior12 findings are closed by corrected source/direct tests; the preceding744/132/161 counts cover that correction. Remaining patches add no public API, physical guarantee or deployment. Preserve all previous successful scope/invariants and runtime/acceptance blockers. Root regenerates evidence counts and final candidate only after these patches verify.


Final source closure: all12 first-pass findings were corrected. Correction1 C1–C4/C7–C10 and V2 are corrected and independently confirmed closed; C5/C6 were refuted against the explicitly bounded model, and their dispositions accepted by the reviewer. No unresolved/deferred source findings. Final root complete Local gate passed747 custody /132 runtime /162 Python;38 focused model and7 audit tests separately passed. Builds warning/error free; model9515 states /252180 transitions /depth20 /32 fault schedules /15 unsafe controls. Earlier counts in this append-only history are superseded by final evidence.

This source continuation is done; EXT-SECRETS-1 overall and Agents5.4 Live remain incomplete and unavailable. Physical component/provider, independent enrolled runtime authorities/roles and all53 missing runtime references plus25 supplemental component/proof references remain blockers. Original Story5.4 completion spec and Agents register/status are unchanged. Final detached immutable packet is generated after this local source commit; any subsequent source/runtime/trust change requires new pins and acceptance. No Live/cohort command or journal reset occurred.
