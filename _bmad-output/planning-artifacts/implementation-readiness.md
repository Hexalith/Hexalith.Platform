---
date: 2026-09-28
gate: CONCERNS
decision: Proceed; sprint-status.yaml generated. Fix findings in epics.md before the affected work starts.
inputs:
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/specs/spec-platform/ (SPEC.md, acceptance-criteria.md, sequencing.md, success-measures.md)
  - _bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md, addendum.md
  - _bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md (update 4)
---

# Implementation Readiness — Hexalith Platform

**Verdict: CONCERNS.** The plan is implementable and sound overall. Every FR-1..12, NFR-1..3, SM-1..6, SM-C1..C5 and AR-1..AR-63 traces to stories whose acceptance criteria carry the source thresholds, and AR-64 is accepted risk by design. No story adds scope that the sources don't support. The G1 → G2 → G3 order holds, the version pins agree across documents, and the final CAP-8/SM-5 spec refinement is reflected in Stories 6.18, 6.20 and 9.5.

The findings below are gaps in sequencing, coverage or freshness. Fix them in `epics.md` with `bmad-correct-course` or a direct edit before the affected work starts, then re-run `bmad-sprint-planning` to refresh `sprint-status.yaml`.

**Fix before:** Epic 2 starts (C-01) and the early independent track is scheduled (C-04, C-15). Fix the others before their epic starts.

## Sequencing — undeclared forward dependencies

| ID | Sev | Finding | Where | Suggested fix |
| --- | --- | --- | --- | --- |
| C-01 | Medium | Stories 2.8 and 2.10 accept CI integration evidence (2.10 marks the runner Platform-accepted "so that local and CI integration evidence can be accepted"). The candidate policy they depend on (PR head or merge result, the gated decision, evidence reuse) is decided only in Story 3.1. The spec requires it first. | SPEC.md:103; sequencing.md:99, :205; spine:470-471; epics.md:1520, :1541 | Move 3.1 into Epic 2 next to 2.1, or narrow 2.10's claim to non-candidate evidence. |
| C-02 | Medium | Story 5.8 needs "a candidate over production's working baseline" and evidence that "names the recorded production baseline". The baseline branches of 5.6 and 5.9 need it too. The first production baseline exists only at Story 6.23. | epics.md:2648-2668; 6.23 at epics.md:3367 | State that 5.8 closes after 6.23, or move it to follow 6.23. |
| C-03 | Medium | Stories 6.19 (data-restore branch), 6.20 and 6.22 need a complete recovery point. Backups and usable recovery points are built in 8.5 and 8.7. Story 6.20 also needs Memories erasure continuity (8.13 plus the tombstone prerequisite). None of these is required for G1. | epics.md:3262, :3289, :3306, :3340, :3352 | Move them after 8.7 and 8.13, or split off the recovery-point-dependent criteria. |
| C-04 | Medium | The early independent track isn't fully independent. 6.3 (test issue) and 6.4 (monitor) raise issues in the notification repository that Story 4.4 creates. For 8.1, only procurement is independent: its host wiring needs 4.6, 4.13, 6.9, 8.5, 8.7 and 8.9–8.12. | epics.md:712, :1949, :2789, :2793, :3640-3644 | Pull 4.4 (or its notification-repository part) forward with 6.3 and 6.4. Split 8.1 into procurement and executor-host wiring. |
| C-05 | Low | Story 4.19 uses the realm-contract instance and generation from 3.4, which is outside the declared Epic 4 exception (only 3.2 and 3.3). It also requires event export "within the declared bound", which 6.2 decides later. | epics.md:2314, :2338; epic list dependency flow item 4 | Add 3.4 to the Epic 4 exception, and move the capture-lag bound decision ahead of 4.19. |
| C-06 | Low | Story 4.12 verifies module image attestations, but the register only expects EventStore and Memories attestation by Epic 5. The first staging deployment (4.22) is therefore blocked on it. | epics.md:2140; External Prerequisites Register | Retime the register row to Epic 4 and first publication. |
| C-07 | Low | Minor cross-epic needs: 4.5 needs OCI attestation and rollback built in 4.12 and 4.13; 4.14 needs deputy key custody from 6.3; 7.6 tests backup-prefix access that 8.5 creates; 6.10 and 6.18 use "lost-window", which is defined only in 8.12. | epics.md:1980, :2198, :3564, :2993, :3231 | Add the notes, or reorder where it's cheap. |

## Coverage gaps

| ID | Sev | Finding | Where | Suggested fix |
| --- | --- | --- | --- | --- |
| C-08 | Medium | No story writes the reference composition's critical-flow declarations, E2E suites and production smoke suites (Parties, EventStore, Tenants, Memories). Story 5.3 lists them as a prerequisite, but the register covers only "modules outside the reference composition", and the reference composition's module work is in Platform scope. | epics.md:2534; register row "Readiness, surface … smoke suites" | Add a story in Epic 4 or 5, tagged with each module repository, or move it into the register with an owner. |
| C-09 | Medium | No story makes the Platform publication workflow publish stable `Hexalith.McpCli` versions after staging validation. Stories build only the run-scoped tool and the McpCli candidate. | acceptance-criteria.md:263; sequencing.md:162; AR-46 | Extend 4.12 or 5.10, or add a story. |
| C-10 | Medium | No story has staging qualify the named data-restore recovery (refusal, candidate removal, no automatic rollback, continuity) before the first applicable retained-data attempt. Story 6.20 only names the procedure, and the only rehearsal is 9.6 at G3. | sequencing.md:117, :173, :211 | Add staging qualification criteria to 5.x or 6.20. |
| C-11 | Medium | Every approved empty or degraded attempt needs staging to prove the candidate installs and works without the baseline. Story 5.7 triggers only when "production has no working baseline", so degraded-but-retained-data attempts get no fresh-install proof. (Fresh-install proof is sufficient on its own only for a first installation; retained-data repair also needs precondition 9.) | prd.md:232; spine:288; SPEC.md:87; epics.md:2646, :3247 | Widen 5.7's trigger to every empty or degraded approved attempt, keeping "sufficient alone only for first install". |
| C-12 | Medium | The SM-6 evidence stories don't re-verify backup cadence and retention, independent availability and freshness monitoring, monitor-silence detection, or GitHub delivery to both people in the drill record. | success-measures.md:15; Stories 8.16, 10.6 | Add these to the 8.16 and 10.6 drill-record criteria. |
| C-13 | Low | SM-5 evidence (9.5) rehearses one of the four deputy limits (approval). The other three (clear, admission, pre-G2 ingress) and the "accepted cause resolves and recurs" case are not rehearsed. | success-measures.md:14; acceptance-criteria.md:156-157 | Extend 9.5. |
| C-14 | Low | Smaller gaps: the G2 Memories adapter-boundary conformance is missing from 8.13, 8.18 and the register (sequencing.md:54). Recording the Folders, Projects and McpCli override records upstream is untracked (sequencing.md:126, :227). "Recovery owner, confirmed by Administrator" is missing from 8.4 (acceptance-criteria.md:186). | as cited | Add the criteria or register rows. |

## Story size and entanglement

| ID | Sev | Finding | Where | Suggested fix |
| --- | --- | --- | --- | --- |
| C-15 | Medium | Story 4.1 bundles backup and isolated-restore proofs for three systems (no CloudNativePG backups exist today) with the in-place upgrade, on a hard clock: Kubernetes 1.34 reaches end of life on 2026-10-27. | epics.md:1860 | Split the backup proofs from the upgrade and schedule both immediately. |
| C-16 | Medium | Story 6.6 upgrades about 13 shared components in one story, including Redis Stack 7.4 → Redis 8 through the Memories digest set (another team), CloudNativePG with two PostgreSQL instances, Keycloak, and KubeSphere removal. | epics.md:2844-2858 | Split by component or by change window. |
| C-17 | Low | Stories 8.10 and 8.13 depend on each other: 8.10 re-applies key destruction from the tombstone mirror, a prerequisite listed only on 8.13, and 8.13 can be proven only through 8.10's restore. 8.13 also bundles the unrelated Memories Redis → Dapr migration gate. Story 4.18 is borderline oversized. | epics.md:3862, :3965, :2280 | Put the tombstone prerequisite on 8.10, and split out the Redis → Dapr gate. |

## Out of date after architecture update 4

| ID | Sev | Finding | Where | Suggested fix |
| --- | --- | --- | --- | --- |
| C-18 | Medium | Stories 4.14 and 6.7 have the environment-layer identity create namespaces, the StorageClass and the PriorityClass. Update 4 moved namespaces, environment-identity RBAC, PriorityClasses and StorageClasses to the shared-infrastructure tier, changed only through the named shared-infrastructure workflow. The environment-layer identity writes only into the data namespace. AR-32 doesn't reflect the tier move. | spine:142, :250; epics.md:445, :2184-2197, :2887-2893 | Re-home the namespace, StorageClass and PriorityClass creation under the shared-infrastructure workflow (6.22 or a staging-era equivalent). |
| C-19 | Low | AR-33 says the rollback set is always prepared and ready-validated. The final rule applies this only to attempts eligible for automatic recovery (6.17 is correct). Story 6.8's per-job credentials are missing the recovery-kind attempt's recovery-hook credential and the read access to the recovery-point prefix. Story 5.6 deploys production's baseline for every staging attempt, but the spec limits this to automatic-eligible attempts. | epics.md:454, :2920, :2614; spine:132, :219; acceptance-criteria.md:149; sequencing.md:171-173 | Align the wording. |

## Open decisions (by design, not findings)

Policy decisions: 2.1 attachment hold limit; 3.1 candidate-selection policy; 5.1 staging evidence policies; 5.2 vulnerability policy; 6.2 monitor and admission bounds; 8.2 reduced-recovery operating policy; 9.1 shared-infrastructure currency policy. The human decision is 6.3 (name the recovery deputy). The architecture spike is 6.1 (rehearsal-fault injection; 6.21 and every SM-5 rehearsal depend on it). Stories with embedded ratifications: 1.6 (AppHost form), 1.7 (local Dapr convention), 4.5 (Aspire-to-Helm or AD-1 fallback), 4.17 (durable broker).

## External prerequisites — stories that cannot close without other teams

Epics 1 and 2 have no External Prerequisites Register blockers, but several stories need other teams to review and merge under the cross-repository definition of done: 1.5, 1.9, 1.10, 1.11, 1.12, 2.9 and 2.10. Register-blocked stories:
- Epic 3: 3.2–3.8 and 3.10–3.12. Story 3.3 also needs the gateway metadata endpoint, which is untagged.
- Epic 4: 4.9, 4.12 (see C-06), 4.19, 4.24.
- Epic 5: 5.3 (see C-08), 5.4, 5.5, 5.9.
- Epic 6: 6.11, 6.14.
- Epic 7: 7.1, 7.3.
- Epic 8: 8.4, 8.6, 8.9–8.11 (untagged) and 8.13.
- Later epics: 9.3, 10.1, 10.2, 11.2, 11.3, 12.1, 12.2.
