# Rubric and dependency-register reconciliation

Reviewed: 2026-10-22.

**Verdict: PASS with two nonblocking clarity fixes.** The update fixes the real feature-level divergence points without adopting a backend or reopening accepted policy. No critical or high findings. Open owner decisions remain implementation/admission gates, so none of the Deferred entries authorizes incompatible production choices.

## Inputs and scope

- Current custody `ARCHITECTURE-SPINE.md` and `docs/implementation/ext-protection-1-custody-design-2026-10-18.md`.
- Authoritative Agents `external-dependency-register.md`, including status semantics, October 10/18 acceptances, protection v21/v23 and OQ-5.4-1.
- Architecture reviewer-gate checklist; accepted October 20 erasure-authority decision/approval; production history lifecycle contract; history owner decision packet.
- Read-only semantic review. No deliverable edits, infrastructure actions, dependency executions or acceptance transitions.

## Findings

### R1 — Low: qualify the two distinct F1 namespaces

**Locations:** spine AD-23 versus AD-26/AD-28 and Deferred; custody design lines 99, 132 and 149.

The update uses `F1` for both the October 20 interaction Dapr/component feasibility lane and the Parties/EventStore actor-free successor-continuity decision. The design identifies the surrounding contexts, but the terse spine says both “F1 must measure that finite budget” and “F1's actor-free successor proof.” An owner could attach the interaction transport evidence to the history decision, or treat a history continuity acceptance as interaction feasibility.

**Disposition: autofix.** Preserve the existing IDs and qualify each reference as “interaction feasibility F1” or “history continuity F1,” linking each to its existing packet/source. Do likewise for `F2` where both the interaction experiment lane and history installation/qualification work are discussed. This is a reference clarification, not a new decision or gate.

### R2 — Low: state the existing shared-target cohort ordering where the handoff is summarized

**Locations:** custody design lines 22 and 133; spine final Deferred bullet.

The summary correctly preserves OQ-5.4-1 prerequisites and blocks execution, but it compresses the exact coordination invariant into “shared target/profile/joint runner.” The register additionally requires Secrets and Host to pin the same Platform commit and executes protection phase 0 before secrets/host phases 1–3; both records become Available only when the entire cohort passes. The source register remains authoritative, so there is no present authorization defect. Repeating this short invariant in the owner handoff would prevent independent delivery owners from preparing individually passing runs and expecting separate availability transitions.

**Disposition: autofix or retain as an explicit link to the complete cohort.** In the D01–D04/F2 delivery acceptance row, add the same immutable Platform target for Secrets/Host, ordered Phase 0–3 run and joint pass requirement. Do not copy or amend current target hashes, commands or status fields.

## Reconciliation: material requirements landed

| Register / accepted requirement | Assessment |
| --- | --- |
| Story 5.4 is accepted ready-for-dev; four direct records are Committed and unavailable | Correctly stated in design line 22; no claim that unavailable execution is now permitted. |
| Committed permits contract/package work, not consumer seam execution | Backend-neutral work is allowed; installed/physical/live/cohort paths remain gated. No availability declaration. |
| Current targets, integration date, commands and hashes cannot change by inference | Explicitly preserved; the doc retains the missed 2026-10-08 date and exact source authority rather than inventing a successor commitment. |
| EXT-PROTECTION-1 remains Uncommitted and must become Committed before the cohort | Correctly retained as a delivery prerequisite. |
| PLAT-ACTOR-HISTORY-1 is symbolic work | Explicitly identified as not an accepted register record; mapping or future registration is an owner decision. |
| Atomic complete manifest, permanent exact retry/lookup and all-or-none physical fate | B0–B5 and AD-19–AD-25 retain complete vector binding, one common physical cut, Unknown until independently verified B5, immutable originals and no per-target delete loop. |
| v21 online guard/dispatch and v23 protection-owner reserve/block/activation | Ownership/order boundaries remain distinct; no new cancellation owner or capability expiry path. |
| Restore cannot revive, original aliases/versions cannot reissue | Current independent authority, exclusion indexes, quarantined restore, old-incarnation fencing, target failure and surviving-control recovery are explicit. |
| Independent approval signer | Preserved outside Agents/platform-host custody and distinct from recovery/custody authority. |
| Bounded retained human binding | Original fixed 31,536,000-second exclusive expiry, separate purpose/unit/generations, profile-erasure distinction and no retry/rebind/revoke/restore renewal are explicit. |
| Exact history expiry includes failures/backups | Read cutoff and physical unrecoverability are separate. A late receipt proves the earlier cut or remains breach/pending; delayed KMS destruction is explicitly ineligible under current policy. |
| Owner selections remain precise and reviewable | C1/Q1–Q7/history F1/D01–D04/F2 give accountable roles, missing principal fields, alternatives and acceptance inputs. H3 is a policy-change alternative, not adopted. |
| Accepted Dapr boundary and unaccepted AWS construction | Both documents consistently preserve Dapr-only app infrastructure access and isolate candidate vendor internals. AWS is neither selected nor qualified. |

## Rubric assessment

- **Enforceable invariants:** Each AD has Binds/Prevents/Rule. Accepted obligations and recommended physical enforcement are distinguishable. Recommended AD-27 is a capability acceptance criterion, not a false claim that a verified implementation exists.
- **Level-below consistency:** Shared manifest fate, sole cancellation owner, exact original publication, per-unit history expiry and independent restore admission prevent the likely incompatible local implementations.
- **No unsafe deferral:** Backend choice, time/holder feasibility, identity mapping, source continuity and actual composition remain explicit blockers. Backend-neutral/offline work cannot satisfy the physical gate.
- **Brownfield and inherited constraints:** Existing six operations, original DEK/AAD/version semantics, actor capacities, Dapr boundary and same-surviving-authority first-release limitation are ratified. No parent AD is weakened in the reviewed update.
- **Operational envelope:** Independent failure/restore placement, Administrator/deputy access, fencing, total-loss fail-closed behavior, copy/work ownership, retention classification, throughput/admission headroom, evidence and bounded qualification lanes are decided or assigned to explicit owner selections.
- **Technology claims:** No new version, deployment or vendor is bound. The author labels versions as observed baselines and the history construction as unverified. Primary-source citations substantiate the candidate KMS delay/irreversibility distinction and Dapr extension boundary; no built-in complete custody component is claimed.

The remaining production blockers are real owner/feasibility inputs and are correctly left visible. The architecture update itself is ready for owner review after optional clarity fixes above.

## Author dispositions verified after revision

- **R1 — applied in the companion design.** Interaction-feasibility F0/F1/F2/F3, history continuity F1 and history installation/qualification F2 now have explicit namespaces in the handoff and table. The spine already distinguishes history F1 in AD-28; remaining bare F1 references in AD-23/AD-26 and Deferred retain context but could receive the same optional editorial qualification. No gate blocker remains.
- **R2 — applied.** The final handoff explicitly requires the same immutable Secrets/Host Platform commit, fixed Phase 0 protection / Phase 1 host-free secrets / Phase 2 host with consumer refusal / Phase 3 mutual-case order, and entire-cohort success. It states that any failure leaves both records non-Available and separate phase successes cannot advance either.

Post-disposition verdict remains **PASS**. No new critical/high findings from these changes.
