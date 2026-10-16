---
title: EXT-SECRETS-1 isolated runtime provisioning
type: feature
created: 2026-10-16
status: in-progress
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
- [ ] `eng/ext-secrets-1/prepare_acceptance.py` — confirm secrets paths at HEAD match `707eaca3b658942b9c6513d24c66da5b71e10199`. If they differ, stop without retargeting.
- [ ] `.../isolated-runtime-20261016/discovery.json` — read-only inspection of `192.168.1.30`; public Dapr, OpenBao, workload, and credential-reference names only.
- [ ] `.../isolated-runtime-20261016/runtime-reference.json` — allowlisted example plus discovered public references; absent fields stay null.
- [ ] Clean worktree of the candidate — run `prepare_acceptance.py` into `.../isolated-runtime-20261016/`. Emit packet, plan pin, three anchor pins, exact command, and blocked-input report.
- [ ] `.../isolated-runtime-20261016/blockers.json` — each missing input with its authority and executable action from `owner-input-map.json` or the five README prompts.
- [ ] `eng/verify-ext-secrets-1.ps1 -Mode Local` — clean worktree, fresh directories, EventStore `../eventstore`. Do not run Live.
- [ ] Live gate — record that register acceptance and enrollment are absent, so all 234 cases stay unrun.

**Acceptance Criteria:**
- Given absent authorities, when provisioning is attempted, then no cluster, token, key, or journal change occurs, and each blocker names its input, authority, and action.
- Given the clean candidate, when preparation finishes, then the packet pins that commit, configuration, plan, and three anchor digests, and the command matches `prepare_acceptance.py`.
- Given null or unaccepted anchors, when Live preflight runs, then it returns DependencyNotAvailable and zero effects.
- Given later acceptance of the same commit, command, configuration, plan, and three enrolled anchors, when qualification runs, then all 234 cases show persisted end-state, isolation, revocation, restart, restore, and lost-result evidence before availability.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

HEAD `b5e1caea7ea95026e3e2138de050c6712f201502` is dirty; the secrets tree matches the candidate. Prepare in a clean worktree. Missing anchors stay null because self-generated keys are not independent enrollment. Evidence root: `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/isolated-runtime-20261016/`.

## Verification

**Commands:**
- `pwsh -NoProfile -File eng/verify-ext-secrets-1.ps1 -Mode Local -EventStoreSourceRoot ../eventstore -ArtifactsDirectory <fresh> -EvidenceDirectory <fresh>` — expected: passing Local results from the clean worktree.
- `python3 eng/ext-secrets-1/prepare_acceptance.py` — expected: exit 2, `qualificationEffects` 0, files only in the new evidence directory.
- `git status --short` — expected: the same unrelated dirty paths and no secrets-source or register edits.
