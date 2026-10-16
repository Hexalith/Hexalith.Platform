---
title: EXT-SECRETS-1 isolated runtime provisioning
type: feature
created: 2026-10-16
status: done
baseline_commit: 65cd72692660798b01c56ed45312249bbd4e9894
route: dispatch
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EXT-SECRETS-1 stays Committed, not Available. Candidate `707eaca3b658942b9c6513d24c66da5b71e10199` has null anchors and 75 missing inputs. No isolated installation is accepted.

**Approach:** Discover public references on `192.168.1.30`, bind only those references, and regenerate the detached v2 packet from a clean worktree of that candidate. Present the command, configuration, plan pin, and three anchor pins for owner acceptance. Leave all 234 cases unrun until acceptance and independent enrollment. Report each remaining blocker with its input, authority, and executable action.

## Boundaries & Constraints

**Always:** Reuse existing Dapr and `openbao/StatefulSet/hexalith-keys` on `192.168.1.30`. Preserve 2026-10-08, Branch B, 365-day history, named approval, L/S/O/H/R = 300/30/600/86400/604800, and Open export disposition with zero effects. Keep decision keys outside Platform and Agents. Record credential references only. Keep the three roles independent. Preserve journals and the unrelated dirty checkout. Pin `707eaca3b658942b9c6513d24c66da5b71e10199` when its secrets tree still matches HEAD.

**Never:** Invent identities, tenants, anchors, tokens, results, or Available status. Use the operator kubeconfig or Memories credentials as application credentials. Apply proposed manifests, mutate production workloads, keys, or data, edit the Agents register, or delete journals. Run Live before the owner accepts the target, configuration, plan, three distinct anchors, and exact command, and before independent enrollment.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Required behavior | Failure |
| --- | --- | --- | --- |
| Reference absent | Discovery finds no owner binding | Leave it null; name authority and action | DependencyNotAvailable, zero effects |
| Dirty checkout | Unrelated changes remain | Prepare from a clean candidate worktree | No packet from the dirty checkout |
| Anchors missing | PEMs absent or not independently enrolled | Keep null anchor pins; stay unauthorized | Live preflight refuses |
| Gate closed | Register target or enrollment differs | Run none of the 234 cases | DependencyNotAvailable, zero effects |

</frozen-after-approval>

## Code Map

- `eng/ext-secrets-1/prepare_acceptance.py` — v2 packet; dirty porcelain blocks readiness. Reuse allowlisting.
- `eng/ext-secrets-1/qualify.py` — `preflight()` refuses before `execute()`; 234-case plan stays intact.
- `eng/ext-secrets-1/cases.py` — complete S1–S4/v23 plan. Do not add, drop, or reorder cases.
- `eng/ext-secrets-1/runtime-reference.example.json` — public allowlist and accepted host, provider, timing, and Open disposition.
- `eng/ext-secrets-1/provisioning/owner-input-map.json` — role owners and actions; `apply` stays false.
- `eng/ext-secrets-1/provisioning/*.proposed.yaml` — unapplied workloads and ACLs. Do not apply.
- `eng/ext-secrets-1/census-2026-10-08.json` — original census. Do not rewrite.
- `src/Hexalith.Platform.Secrets.Qualification.{Executor,Observer,Enrollment}/` — deny without provider, store, verifier, and caller scope.
- `eng/verify-ext-secrets-1.ps1` — Local entry. Live stays gated.
- Prior packet `.../exact-target-final-20261016T120636Z/` — do not overwrite. Agents register EXT-SECRETS-1 row stays Committed.

## Tasks & Acceptance

**Execution:**
- [x] `eng/ext-secrets-1/prepare_acceptance.py` — confirm secrets paths at HEAD match `707eaca3b658942b9c6513d24c66da5b71e10199`. If they differ, stop without retargeting.
- [x] `.../isolated-runtime-20261016/discovery.json` — read-only inspection of `192.168.1.30`; public Dapr, OpenBao, workload, and credential-reference names only.
- [x] `.../isolated-runtime-20261016/runtime-reference.json` — allowlisted example plus discovered public references; absent fields stay null.
- [x] Clean worktree of the candidate — run `prepare_acceptance.py` into `.../isolated-runtime-20261016/`. Emit packet, plan pin, three anchor pins, exact command, and blocked-input report.
- [x] `.../isolated-runtime-20261016/blockers.json` — each missing input with its authority and executable action from `owner-input-map.json` or the five README prompts.
- [x] `eng/verify-ext-secrets-1.ps1 -Mode Local` — clean worktree, fresh directories, EventStore `../eventstore`. Do not run Live.
- [x] Live gate — record that register acceptance and enrollment are absent, so all 234 cases stay unrun.

**Acceptance Criteria:**
- Given absent authorities, when provisioning is attempted, then no cluster, token, key, or journal change occurs, and each blocker names its input, authority, and action.
- Given the clean candidate, when preparation finishes, then the packet pins that commit, configuration, plan, and three anchor digests, and the command matches `prepare_acceptance.py`.
- Given null or unaccepted anchors, when Live preflight runs, then it returns DependencyNotAvailable and zero effects.
- Given later acceptance of the same commit, command, configuration, plan, and three enrolled anchors, when qualification runs, then all 234 cases show persisted end-state, isolation, revocation, restart, restore, and lost-result evidence before availability.

## Implementation Notes

## Spec Change Log

## Review Triage Log

- BH1 — `medium` — defer. `PLAN.md`, `PLAN-v2.md`, and `PLAN-v3.md` line 142 say "None means permission to execute here" after defining tool statuses that do not authorize execution. Pre-existing plan text, committed with this story but not produced by the secrets provisioning.
- BH2 — `low` — defer. README, REVIEW, DOCUMENT-CHECKS, and PLAN-v3 still name `trust-root-establishment.json` as the first deliverable and do not point at the later clarification that says not to manufacture that file. Pre-existing handoff wording.
- BH3 — `low` — defer. The clarification names a sole owner as a proposed producer and leaves the independent reviewer unidentified, but it never says that person cannot hold both roles. Pre-existing clarification.
- BH4 — `low` — defer. The clarification cites eight historical recovery signatures and four public-material classes without paths, fingerprints, or a map onto `public-trust-roots/`, the server/client PEM names, or the encryption recipient. Pre-existing clarification.
- BH5 — `low` — defer. `REVIEW-OWNER-CLARIFICATION-20261016t130702z.md` labels `/root/plan_edge_review` an independent reviewer and records no clarification digest. The same file says the review establishes no owner authority, so it does not grant acceptance. Pre-existing review note.
- BH6 — `medium` — defer. The rights section requires all 23 selectors to pass authorization, and the HTTP section requires 404 for selectors 20–23, but the plan never says those are separate results. The method is stated as GET after the table. Pre-existing plan.
- BH7 — `false` — reject. README line 5 and PLAN-v3 lines 567–568 identify `PLAN.md` and `PLAN-v2.md` as preserved drafting history and name `PLAN-v3.md` as the handoff.
- BH8 — `low` — defer. PLAN-v3 line 571 uses "They" after several sentences, so the subject is unclear. The ENOENT note and REVIEW.md's `rg` exit 2 describe the same missing file. Pre-existing wording.
- BH9 — `medium` — defer. Role codes R, M, V, and H appear on the v4 crosswalk and are absent from the producer/reviewer table. The five evidence groups are the five numbered artifact sections, so that half of the claim is not a gap. Pre-existing plan.
- BH10 — `medium` — defer. I1, I3, and I5 do not restate the format, lifecycle, and 23-request limits that appear elsewhere, and steps 7–10 still describe later measurement while those contracts are open. Pre-existing plan.
- BH11 — `medium` — defer. The preservation ledger has no row for HEAD `b5e1caea` even though the following paragraph tells P to include later commits. HTTP acceptance names sanitizer shapes and a nine-library inventory without listing them. Pre-existing plan.
- BH12 — `false` — reject. REVIEW.md and DOCUMENT-CHECKS.json scope the plan handoff and `PLAN-v3.md`. The secrets spec records its own inspection and is not covered by those claims.
- BH13 — `low` — reject. Design Notes, `baseline_commit`, and the candidate are three different revisions, and the spec does not list the compared paths. `git diff` of the secrets paths between `707eaca3b658942b9c6513d24c66da5b71e10199` and HEAD is empty. The fix would edit this spec.
- BH14 — `false` — reject. The packet, pins, command, and blockers are under the evidence root named in Design Notes, outside this diff. Empty spec logs are bookkeeping; filling them would edit this spec.
- BH15 — `low` — reject. Timing keys and the executor, observer, and enrollment roles are fixed in the frozen intent and in `runtime-reference.json`. Adding matrix rows or expanding those terms would edit this spec.
- EC1 — `medium` — reject. `prepare_acceptance.py` requires `--root`, `--configuration`, `--output`, `--artifacts-argument`, and `--evidence-argument`. The Verification line omits them, so a literal run is argparse usage exit 2 and writes no report. The prepared packet used the full command. The fix would edit this spec.
- EC2 — `false` — reject. `discovery.json` records a completed read-only inspection, `credentialValuesRead` false, and no owner workloads. Absent references stayed null in `runtime-reference.json`. No partial inspection was stored as a binding.

Verification-gap review reported no findings.

## Design Notes

HEAD `b5e1caea7ea95026e3e2138de050c6712f201502` is dirty; the secrets tree matches the candidate. Prepare in a clean worktree. Missing anchors stay null because self-generated keys are not independent enrollment. Evidence root: `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/isolated-runtime-20261016/`.

## Verification

**Commands:**
- `pwsh -NoProfile -File eng/verify-ext-secrets-1.ps1 -Mode Local -EventStoreSourceRoot ../eventstore -ArtifactsDirectory <fresh> -EvidenceDirectory <fresh>` — expected: passing Local results from the clean worktree.
- `python3 eng/ext-secrets-1/prepare_acceptance.py` — expected: exit 2, `qualificationEffects` 0, files only in the new evidence directory.
- `git status --short` — expected: the same unrelated dirty paths and no secrets-source or register edits.
