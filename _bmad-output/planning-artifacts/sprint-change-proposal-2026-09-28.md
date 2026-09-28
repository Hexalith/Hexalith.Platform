---
date: 2026-09-28
status: approved
mode: batch
scope: moderate
trigger: _bmad-output/planning-artifacts/implementation-readiness.md
decision: direct-adjustment
---

# Sprint Change Proposal — Implementation Readiness Corrections

## 1. Issue Summary

The 2026-09-28 implementation-readiness gate returned **CONCERNS**. The plan remains implementable, the MVP remains valid, and the G1 → G2 → G3 order remains correct, but `epics.md` contains 19 sequencing, coverage, sizing, and architecture-freshness findings (C-01–C-19).

The trigger is a planning-artifact consistency problem discovered before implementation, not an implementation failure or a strategic pivot. The evidence is the readiness report's trace from the finalized PRD, specification package, and architecture update 4 into individual stories. The most urgent corrections are:

- C-01 before Epic 2 accepts CI evidence;
- C-04 before the early independent track is scheduled;
- C-15 before the Kubernetes 1.34 end-of-life deadline on 2026-10-27;
- every other finding before its affected epic starts.

The change is limited to backlog artifacts. It does not change FR-1–FR-12, NFR-1–NFR-3, SM-1–SM-6, the MVP module set, or any architecture decision.

## 2. Change Analysis Checklist

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Triggering story | [N/A] | No implementation story triggered the change; the implementation-readiness gate did. |
| 1.2 Core problem | [x] | Planning decomposition is inconsistent with already-approved requirements and architecture. |
| 1.3 Evidence | [x] | C-01–C-19 cite exact source and `epics.md` locations. |
| 2.1 Current epic viability | [x] | Every affected epic remains viable after direct edits. |
| 2.2 Epic-level changes | [x] | Split oversized work, move or narrow forward-dependent criteria, and add missing stories/prerequisites. |
| 2.3 Remaining epics | [x] | Epics 2–10 reviewed; Epics 1, 11, and 12 require no substantive scope change. |
| 2.4 Obsolete/new epics | [N/A] | No epic is obsolete and no new epic is required. |
| 2.5 Order and priority | [x] | Early independent work and recovery-dependent stories are resequenced. |
| 3.1 PRD conflict | [x] | No PRD edit; the MVP remains achievable. |
| 3.2 Architecture conflict | [x] | Architecture is current; `epics.md` must be brought into alignment with update 4. |
| 3.3 UI/UX conflict | [N/A] | No UX contract exists and Platform exposes no product UI of its own. |
| 3.4 Other artifacts | [!] | `sprint-status.yaml` must be regenerated after approval and the `epics.md` edit. |
| 4.1 Direct adjustment | [x] Viable | Medium planning effort, low product risk, medium coordination risk. |
| 4.2 Rollback | [x] Not viable | No completed implementation must be reverted. |
| 4.3 MVP review | [x] Not required | No scope or goal reduction is justified. |
| 4.4 Recommended path | [x] | Direct adjustment within the existing epic structure. |
| 5.1–5.5 Proposal components | [x] | Issue, impacts, approach, action plan, and handoff are defined below. |
| 6.1 Checklist review | [x] | All applicable items are addressed. |
| 6.2 Proposal accuracy | [x] | Changes trace to C-01–C-19 and preserve source authority. |
| 6.3 User approval | [!] | Pending explicit approval. |
| 6.4 Sprint status update | [!] | Run sprint planning after the approved `epics.md` edit. |
| 6.5 Final handoff | [!] | Pending approval; proposed recipients are Product Owner and Developer. |

## 3. Impact Analysis

### Epic impact

| Epic | Impact |
| --- | --- |
| Epic 1 | No substantive change. |
| Epic 2 | Candidate-selection policy moves here so CI evidence cannot precede it. |
| Epic 3 | Story 3.1 is removed after its policy is merged into Story 2.1; dependency prose is updated. |
| Epic 4 | Kubernetes backup/upgrade work is split; notification controls are pulled forward; capture-lag and reference-suite work are added; update-4 tier ownership is corrected. |
| Epic 5 | Staging branches are made baseline-aware; fresh-install proof is widened; stable McpCli publication is added. |
| Epic 6 | Infrastructure currency is split into change windows; recovery-point-dependent work is narrowed or moved; executor credentials and tier ownership are corrected. |
| Epic 7 | Backup-prefix isolation evidence moves to the point where the prefix exists. |
| Epic 8 | Procurement is separated from executor commissioning; recovery prerequisites are ordered; Memories work is disentangled; named data recovery is qualified here. |
| Epic 9 | SM-5 stop/deputy cases are completed. |
| Epic 10 | The full-composition DR drill records all SM-6 monitoring and delivery evidence. |
| Epics 11–12 | No substantive change. |

### Artifact impact

- **PRD:** no change.
- **Architecture spine:** no change. It is the authority the backlog must match.
- **Specification package:** no change. It already contains the missing ordering and acceptance rules.
- **UX:** not applicable.
- **`epics.md`:** edit the requirements inventory, external-prerequisite register, epic summaries, dependency flow, early-independent track, and affected stories.
- **`sprint-status.yaml`:** regenerate after the approved edit; do not hand-edit story keys.

### Technical and delivery impact

No production code, infrastructure, or deployed state changes as part of this proposal. The implementation backlog becomes more executable by:

- preventing evidence acceptance before governing policy exists;
- preventing recovery claims before complete recovery points and erasure continuity exist;
- exposing hard-date, security, and procurement work as genuinely independent work;
- moving shared-infrastructure resources to their architecture-owned tier;
- making stable McpCli publication and reference-suite ownership explicit;
- reducing oversized change windows.

## 4. Recommended Approach

Choose **Direct Adjustment**.

The source requirements and architecture are already coherent. The defects are in story decomposition, ordering, and coverage, so rollback or MVP reduction would add risk without solving the problem.

- **Planning effort:** Medium. One coordinated `epics.md` edit plus sprint-plan regeneration.
- **Implementation effort:** The same product scope, divided into more independently executable stories. Some work is pulled earlier; no capability is added.
- **Timeline impact:** Near-term sequencing changes. Kubernetes backup and upgrade work, repository controls, deputy nomination, monitoring, and capacity procurement start earlier. G1/G2/G3 targets do not change.
- **Change risk:** Medium, primarily story renumbering and dependency/reference updates.
- **Risk of no change:** High: invalid evidence, blocked stories, unqualified recovery paths, and update-4 ownership violations.

## 5. Detailed Change Proposals

The identifiers below refer to the current `epics.md` unless a proposal explicitly says “new”. Renumbering is mechanical and must update every internal reference, coverage-map entry, prerequisite entry, and regenerated sprint-status key.

### Proposal A — Move candidate policy ahead of CI evidence (C-01)

**Stories:** 2.1, 2.8, 2.10, 3.1, 3.9  
**Section:** policy and dependencies

**OLD**

- Story 2.1 decides only the attachment hold limit.
- Story 3.1 decides candidate revision, gated decision, and evidence reuse.
- Stories 2.8 and 2.10 accept CI integration evidence before Story 3.1.

**NEW**

- Rename Story 2.1 to **“Decide the candidate and attachment policy.”**
- Merge all Story 3.1 acceptance criteria into Story 2.1: candidate revision, gated merge/release decision, unchanged-module evidence reuse, default/maximum attachment holds, owners, and revisit conditions.
- State that Stories 2.8 and 2.10 cannot accept CI evidence until Story 2.1's decision is published in the runner/descriptor contract.
- Remove Story 3.1 and change Story 3.9's prerequisite from Story 3.1 to Story 2.1.
- Update Epic 2 and Epic 3 summaries and dependency-flow prose accordingly.

**Rationale:** Candidate identity and attachment are one shared AR-16 policy and must precede every evidence consumer.

### Proposal B — Split and reorder Epic 4 (C-04, C-05, C-06, C-07, C-08, C-15, C-18)

#### B1. Split pre-upgrade recovery proof from the Kubernetes upgrade (C-15)

**Story:** current 4.1  
**Section:** title, scope, acceptance criteria

**OLD**

> Story 4.1: Upgrade the cluster off Kubernetes 1.34 after verified backups

One story proves isolated restores for Keycloak PostgreSQL, OpenBao, and Memories and performs the in-place Kubernetes upgrade under the 2026-10-27 clock.

**NEW**

1. **Story 4.1 — Prove pre-upgrade backups and isolated restores**
   - Configure and capture recoverable copies for Keycloak PostgreSQL, OpenBao, and Memories.
   - Restore all three into isolated targets and record integrity and recovery evidence off-node.
   - Make the Kubernetes upgrade a blocked consumer of this evidence.
2. **Story 4.2 — Upgrade the cluster off Kubernetes 1.34**
   - Require Story 4.1.
   - Perform and verify the in-place upgrade and record actual versions and workload outcomes.

Renumber current Stories 4.2–4.25 to 4.3–4.26 before applying the further insertions below.

**Rationale:** Backup proof and cluster upgrade have different skills, failure modes, and completion evidence; both must be scheduled immediately.

#### B2. Make repository controls genuinely early (C-04)

**Story:** current 4.4, renumbered 4.5  
**Section:** ordering metadata

**OLD**

> Story 4.4: Apply publication and operations repository controls

It is not marked as part of the early independent track, although Stories 6.3 and 6.4 create notification issues in the repository it creates.

**NEW**

- Mark the renumbered Story 4.5 **Independent, pull forward**.
- Add it to the early-independent-track table.
- Add Story 4.5 as an explicit prerequisite of current Stories 6.3 and 6.4.

**Rationale:** Deputy delivery and monitor delivery cannot be tested before the notification repository exists.

#### B3. Narrow Aspire-to-Helm qualification to what exists at that point (C-07)

**Story:** current 4.5, renumbered 4.6  
**Section:** acceptance criteria

**OLD**

> the chart's OCI attestation verifies, and a rollback to the retained package succeeds without regeneration

Those artifacts and retention controls are created only by current Stories 4.12 and 4.13.

**NEW**

- Story 4.6 qualifies deterministic rendering, chart contents, admission, server-side ownership, and the AD-1 fallback trigger.
- Move OCI attestation verification into renumbered Story 4.13 (publication).
- Move retained-package restore/rollback from the off-site replica into renumbered Story 4.14 (retention).

**Rationale:** Export qualification must not claim provenance or rollback evidence before published retained artifacts exist.

#### B4. Put the identity capture-lag decision before staging realm event export (C-05)

**Stories:** 3.4, current 4.19, current 6.2  
**Section:** dependencies and policy

**OLD**

- Epic 4's declared Epic 3 exception omits Story 3.4.
- Current Story 4.19 exports identity events “within the declared bound.”
- Current Story 6.2 decides that bound later.

**NEW**

- Add Story 3.4 to Epic 4's allowed Epic 3 dependency set.
- Insert **new Story 4.20 — Decide the identity-event capture-lag bound**, after the Gateway story and before the staging-realm story.
- Move the capture-lag acceptance criteria from Story 6.2 into new Story 4.20, including owner, notification recipients, measurement point, and revisit condition.
- Renumber current Story 4.19 to 4.21 and current Stories 4.20–4.25 to 4.22–4.27.
- Story 6.2 retains the availability-probe stop bound and two-way admission-check cadence only.

**Rationale:** A producer cannot implement or qualify bounded export before the bound exists.

#### B5. Retime module image attestation (C-06)

**Story:** current 4.12, renumbered 4.13  
**Section:** prerequisites; External Prerequisites Register

**OLD**

> Image attestation ... Must precede: First staging promotion

The publication workflow already verifies the intake-pinned module attestations.

**NEW**

- Change the register boundary to **“Before first publication.”**
- Add EventStore and Memories attestation as prerequisites of renumbered Story 4.13.
- Keep vulnerability scanning and exception policy in Epic 5; distinguish it from producing/verifying attestations.

**Rationale:** The first release cannot be published with the required provenance until those attestations exist.

#### B6. Add owned reference-composition suites (C-08)

**Story:** new 4.28  
**Section:** new story

**OLD**

> Story 5.3 prerequisites: the reference composition's critical-flow and smoke declarations

No story or external-prerequisite row owns Parties, EventStore, Tenants, Memories, and McpCli reference suites.

**NEW**

Add **Story 4.28 — Author the reference composition's critical-flow, E2E, and smoke suites**.

- Repositories: Platform plus Parties, EventStore, Tenants, Memories, and McpCli owner review.
- Each module supplies non-empty critical-flow declarations, complete flow-to-test mappings, executable staging E2E suites, and production-safe smoke suites.
- McpCli supplies essential tool flows.
- The story uses the check-suite contract and blocks Story 5.3.
- The External Prerequisites Register continues to cover only modules outside the reference composition.

**Rationale:** Reference-composition work is explicitly Platform-accountable and cannot remain an unowned prerequisite.

#### B7. Align namespace and class ownership with architecture update 4 (C-18)

**Sections:** AR-23, AR-32; current Story 4.14 (renumbered 4.15)

**OLD**

> the environment-layer identity creates the staging namespaces, StorageClass and PriorityClass

**NEW**

- AR-23/AR-32 state that **namespaces, environment-identity RBAC, StorageClasses, and PriorityClasses belong to shared infrastructure**.
- Renumbered Story 4.15 uses a named staging-bootstrap/shared-infrastructure workflow and shared-infrastructure identity to create those objects.
- The environment-layer identity writes only inside the already-created data namespace and its allowed application-namespace environment objects.
- The application deploy identity remains unable to mutate either tier.
- Keep Story 6.3 on the early-independent track and make it an explicit prerequisite of renumbered Story 4.15 before encrypted-volume key custody is provisioned; Story 4.15 records independent custody by Administrator and the named deputy.

**Rationale:** This corrects architecture update 4's release-tier boundary and removes the undeclared dependency on deputy key custody.

### Proposal C — Make staging branches baseline-aware and publish stable McpCli (C-02, C-09, C-11, C-19)

#### C1. Declare the post-G1 closure boundary (C-02)

**Stories:** 5.6, 5.8, 5.9, current 6.23 (renumbered 6.25)  
**Section:** ordering notes and acceptance criteria

**OLD**

- Story 5.6 always deploys production's working baseline.
- Story 5.8 requires a recorded production baseline.
- Story 5.9's incompatible-reset branch also assumes one.
- The first baseline exists only after current Story 6.23.

**NEW**

- Story 5.6 branches explicitly:
  - no production baseline: use Story 5.7's fresh-install path;
  - a production baseline exists and automatic recovery is eligible: deploy it and rehearse the rollback set;
  - retained-data approved path without compatible evidence: qualify the named data recovery; do not prepare automatic rollback.
- State that Story 5.8 and the baseline-dependent branches of Stories 5.6 and 5.9 close only after renumbered Story 6.25 creates the first working production baseline and before the second production candidate is accepted.
- Update dependency prose so Epic 6 may start on Epic 5's first-install branch while the post-baseline branches remain open.

**Rationale:** The first release cannot be rehearsed over a production baseline that does not yet exist.

#### C2. Widen fresh-install proof without weakening retained-data safety (C-11)

**Story:** 5.7  
**Section:** title and acceptance criteria

**OLD**

> Rehearse a fresh install when production has no baseline

**NEW**

Rename to **“Rehearse candidate installation without the baseline for every approved empty or degraded attempt.”**

- Every approved empty or degraded attempt gets fresh-install/candidate-behavior proof.
- The proof is sufficient by itself only for the first installation with no retained application data.
- A retained-data repair additionally requires valid compatibility evidence or the named data-restore qualification.

**Rationale:** Degraded retained-data attempts also need proof that the candidate can install and run without relying on the failed baseline.

#### C3. Publish the stable McpCli after staging validation (C-09)

**Stories:** 4.12 and 5.10  
**Section:** publication lifecycle

**OLD**

- Publication builds a run/release candidate.
- No story publishes the stable `Hexalith.McpCli` version after validation.

**NEW**

- Clarify in the publication story that it builds an unpublished McpCli candidate.
- Extend Story 5.10: after exact-release staging evidence passes, the Platform publication workflow alone publishes the stable `Hexalith.McpCli` version, versioned by McpCli base and Platform release, with the release-bound package hash; versions are never reused.
- McpCli's own pipeline remains limited to Abstractions and prerelease tool versions.

**Rationale:** This completes the architecture's build-and-release order and AR-46.

#### C4. Limit rollback-set preparation to automatic-eligible attempts (C-19)

**Sections:** AR-33, Stories 5.6 and 6.17 (renumbered 6.20)

**OLD**

> Before rollout, the rollback set ... is recorded, prepared and ready-validated.

**NEW**

> Before rollout **of an attempt eligible for automatic recovery**, the rollback set is recorded, prepared, and ready-validated. Approved empty/degraded and named-recovery attempts do not require production baseline-host validation or an automatic rollback generation.

Apply the same qualification to Story 5.6 and retain the existing correct wording in the automatic-recovery story.

**Rationale:** Architecture update 4 makes rollback-set preparation conditional on automatic-recovery eligibility.

### Proposal D — Split Epic 6 and remove premature recovery claims (C-03, C-07, C-16, C-18, C-19)

#### D1. Split infrastructure currency into bounded change windows (C-16)

**Story:** current 6.6  
**Section:** story decomposition

**OLD**

One story upgrades roughly 13 shared components across identity, data, networking, control-plane, registry, backup, and cluster-admin concerns.

**NEW**

Replace Story 6.6 with four stories:

1. **6.6 — Bring identity and secret infrastructure current:** OpenBao and Keycloak.
2. **6.7 — Bring shared data services current:** Redis 8 through the Memories digest set, CloudNativePG and both PostgreSQL instances, and FalkorDB.
3. **6.8 — Bring the platform control plane and networking current:** Dapr, Traefik, Calico, cert-manager, Gateway API CRDs, and the storage provisioner.
4. **6.9 — Bring artifact, backup, and administration tooling current:** Zot, Velero, and KubeSphere update/removal.

Each story records before/after versions, compatibility evidence, rollback/forward-revert plan, and one bounded change window. Renumber current Stories 6.7–6.19 to 6.10–6.22. Move current 6.20 as described below, and renumber current 6.21–6.24 to 6.23–6.26.

**Rationale:** The original story crosses too many owners and failure domains to implement or recover safely as one change.

#### D2. Keep G1 baseline redeploy, move data restore later (C-03, C-10)

**Stories:** current 6.19 (renumbered 6.22), current 6.20, new 8.16  
**Section:** scope and acceptance criteria

**OLD**

- Story 6.19 includes baseline redeploy and recovery-point data restore.
- Story 6.20 qualifies a named data restore before backup/recovery-point and Memories-continuity stories exist.

**NEW**

- Rename renumbered Story 6.22 to **“Re-deploy the working baseline in place as Administrator or deputy.”** Keep only the prepared rollback-set or recorded-baseline redeploy branch.
- Remove the data-restore branch from it.
- Remove current Story 6.20 from Epic 6.
- Add **new Story 8.16 — Qualify in-place data restore and named retained-data recovery**, after Stories 8.7 and 8.13.
- New Story 8.16 incorporates current Story 6.20 and the removed data-restore branch, and adds staging qualification of:
  - refusal without valid compatibility or a named recovery;
  - a usable post-lock recovery point;
  - candidate removal with data retained;
  - no automatic rollback preparation or execution;
  - later-stop precedence;
  - post-cut admission-revocation and Memories-erasure continuity;
  - recovery re-entry from the same point and NFR-3 re-verification.

**Rationale:** Data restore cannot be implemented or qualified before usable recovery points and erasure continuity exist, and none of it is required for G1.

#### D3. Separate guarded shared-workflow implementation from qualification (C-03)

**Story:** current 6.22 (renumbered 6.24)  
**Section:** title and acceptance criteria

**OLD**

> Change shared infrastructure through one controlled procedure

It claims successful execution before complete recovery points exist.

**NEW**

Rename to **“Implement the guarded shared-infrastructure workflow.”**

- Implement both-lock ownership, named change owner, tier-restricted identity, one attempt per environment, and a hard refusal when no complete recovery point exists.
- Do not claim a successful shared change or forward-revert qualification in Epic 6.
- Qualify successful execution, smoke/NFR-3 reruns, and forward revert in Story 9.6, after Epic 8 supplies complete recovery points.

**Rationale:** The guard can be implemented at G1; successful execution evidence needs recovery infrastructure.

#### D4. Correct production tier ownership (C-18)

**Story:** current 6.7 (renumbered 6.10)  
**Section:** acceptance criteria

**OLD**

> the production environment-layer identity creates namespaces, StorageClass, and PriorityClass

**NEW**

- The named shared-infrastructure/bootstrap workflow creates production namespaces, environment-identity RBAC, StorageClass, and PriorityClass.
- The production environment-layer identity writes only the data namespace and its permitted application-namespace environment objects.
- The application deployment identity writes only the application-package tier.

**Rationale:** Same architecture-update-4 correction as staging.

#### D5. Complete recovery-kind executor credentials (C-19)

**Story:** current 6.8 (renumbered 6.11)  
**Section:** credentials acceptance criterion

**OLD**

> per-job production credentials: application deploy identity, environment-layer identity, synthetic smoke clients

**NEW**

For a recovery-kind attempt, also issue:

- a recovery-hook credential bound to that attempt and epoch;
- read access to that environment's recovery-point prefix;
- only the custodian-released decryption material required by that job;
- destruction/revocation of all three at job end.

**Rationale:** These are mandatory AD-7 credentials for in-place recovery.

#### D6. Make lost-window review conditional until recovery records exist (C-07)

**Stories:** current 6.10 and 6.18 (renumbered 6.13 and 6.21)  
**Section:** clearance/approval acceptance criteria

**OLD**

Both stories read as if a lost-window report always exists, although the report is produced during later recovery work.

**NEW**

State that Administrator reviews every recorded lost-window report applicable to the observed stop; absence is valid only when no recovery capable of producing a lost window has occurred. Once a named recovery or DR report exists, it is a mandatory input to clearance or approval.

**Rationale:** G1 first-install and baseline-redeploy paths must not depend on a report they cannot produce, while later recovery paths must never bypass it.

### Proposal E — Correct cross-epic isolation evidence (C-07)

**Stories:** 7.6, 8.5, 8.19 (renumbered drill story)  
**Section:** NFR-3 negative matrix

**OLD**

Story 7.6 tests staging access to production backup prefixes before Story 8.5 creates those prefixes.

**NEW**

- Story 7.6 covers all production targets that exist at G1 but explicitly defers the backup-prefix case.
- Story 8.5 creates per-environment-instance backup prefixes and their policies.
- Add the staging recovery-hook/prior-epoch denial and backup-prefix denial to the renumbered DR drill Story 8.19 and to the final NFR-3 evidence set.

**Rationale:** A refusal test is meaningful only after the protected target exists.

### Proposal F — Restructure Epic 8 around actual recovery prerequisites (C-03, C-04, C-10, C-12, C-14, C-17)

#### F1. Split procurement from executor commissioning (C-04)

**Story:** 8.1; new 8.15  
**Section:** scope and acceptance criteria

**OLD**

Story 8.1 is marked independent but combines capacity procurement with an integrated recovery executor that depends on records, artifacts, backups, recovery points, and recovery workflows.

**NEW**

- Story 8.1 covers only capacity/location procurement and selection of the recovery-executor host.
- Add **new Story 8.15 — Commission the off-site recovery executor** after Stories 8.9–8.12.
- Story 8.15 integrates the pinned off-site workflow copy, record store, registry replica, recovery-point prefix, per-job custody release, allowlists, credential rotation, and no-GitHub/no-operations-write start path.
- Explicit prerequisites: renumbered Stories 4.7 and 4.14, renumbered Story 6.12, Stories 8.5, 8.7, and 8.9–8.12.

**Rationale:** Procurement is parallelizable; end-to-end executor commissioning is not.

#### F2. Put the tombstone prerequisite on the first consumer and split adapter work (C-17, C-14)

**Stories:** 8.10, 8.13, new 8.14  
**Section:** prerequisites and scope

**OLD**

- Story 8.10 re-applies tombstone key destruction but does not name the tombstone mirror/lineage prerequisite.
- Story 8.13 both proves erasure continuity and migrates unrelated Redis coordination to Dapr.

**NEW**

- Add the Memories tombstone mirror, lineage protocol, tenant-key store, operator artifact, and fence hook as explicit prerequisites of Story 8.10.
- Narrow Story 8.13 to erasure/tombstone/key continuity and recovery evidence.
- Add **new Story 8.14 — Qualify the Memories adapter boundary and migrate remaining Redis coordination to Dapr**.
- Add new Story 8.14 to the G2 prerequisites in the External Prerequisites Register and the G2-opening story.

**Rationale:** This removes a circular dependency and separates data-safety evidence from an adapter migration gate.

#### F3. Require Administrator-confirmed recovery ownership (C-14)

**Story:** 8.4  
**Section:** recovery inventory acceptance criteria

**OLD**

> each item has a recovery owner

**NEW**

> each item has a recovery owner explicitly confirmed by Administrator, with the confirmation recorded alongside the inventory entry

**Rationale:** This preserves the acceptance-criteria source requirement.

#### F4. Complete SM-6 drill evidence (C-12)

**Stories:** current 8.16 (renumbered 8.19), 10.6  
**Section:** drill record acceptance criteria

**OLD**

The drill records RPO/RTO, losses, access, and substitutions but does not explicitly re-verify all SM-6 operating controls.

**NEW**

Both drill stories also record verification of:

- 30-minute backup cadence and seven-day/frequent plus 30-day/daily retention;
- independent production-availability and recovery-point-freshness monitoring;
- monitor-silence/dead-man detection in both directions;
- actual GitHub delivery to Administrator and the deputy.

**Rationale:** These are explicit SM-6 acceptance targets and must be repeated for the full composition.

#### F5. Epic 8 final story order

Keep Stories 8.1–8.13 with the scope changes above, then use:

| Final ID | Story |
| --- | --- |
| 8.14 | Qualify the Memories adapter boundary and migrate remaining Redis coordination to Dapr (new) |
| 8.15 | Commission the off-site recovery executor (new) |
| 8.16 | Qualify in-place data restore and named retained-data recovery (moved/split from 6.19–6.20) |
| 8.17 | Prove the deputy's recovery capability (current 8.14) |
| 8.18 | Operate the reduced-recovery state (current 8.15) |
| 8.19 | Run the isolated DR drill and prove RPO/RTO (current 8.16) |
| 8.20 | Retain production telemetry off-site (current 8.17) |
| 8.21 | Open production to users / G2 (current 8.18) |

Update Story 8.21 to require Story 8.14's adapter-boundary conformance as well as Story 8.13's erasure continuity.

### Proposal G — Complete SM-5 stop and deputy coverage (C-13)

**Story:** 9.5  
**Section:** acceptance criteria

**OLD**

- Rehearses the deputy's inability to approve.
- Re-records a persistent cause that was not accepted.

**NEW**

Add rehearsals proving the deputy cannot:

- approve a release;
- clear a promotion stop;
- grant or revoke production admission;
- reopen user ingress before G2.

Also add the accepted-cause lifecycle: after a cause marked accepted resolves and later recurs, it is recorded as a new cause; while it remains continuously present, it is not duplicated.

**Rationale:** SM-5 explicitly covers all four deputy limits and both cause recurrence rules.

### Proposal H — Track source-document overrides (C-14)

**Section:** External Prerequisites Register; source-document alignment

**OLD**

The source-alignment prose says Folders, Projects, and McpCli override records remain upstream work, but the prerequisite register has no row for them.

**NEW**

Add a register row:

| Prerequisite | Owner | Consumed by | Must precede |
| --- | --- | --- | --- |
| Record Platform's accepted Folders, Projects, and McpCli overrides in their upstream architecture/PRD documents | Folders, Projects, McpCli documentation owners | Recovery and deployment story finalization; final MVP evidence | Before affected recovery/deployment stories are finalized; not an enrollment block |

**Rationale:** The architecture explicitly owns this alignment work; tracking it does not turn it into an enrollment gate.

## 6. Consolidated Renumbering and Reference Updates

Apply renumbering atomically so no intermediate document contains ambiguous IDs.

### Epic 4

- 4.1 becomes backup/restore proof.
- New 4.2 is the Kubernetes upgrade.
- Current 4.2–4.18 become 4.3–4.19.
- New 4.20 is the capture-lag decision.
- Current 4.19–4.25 become 4.21–4.27.
- New 4.28 owns reference-composition critical-flow/E2E/smoke suites.

### Epic 6

- 6.1–6.5 stay unchanged except Story 6.2 loses the capture-lag decision.
- Current 6.6 becomes four Stories 6.6–6.9.
- Current 6.7–6.19 become 6.10–6.22.
- Current 6.20 moves into new Story 8.16.
- Current 6.21–6.24 become 6.23–6.26.

### Epic 8

- 8.1–8.13 retain their IDs with scoped edits.
- New Stories 8.14–8.16 are inserted as listed in Proposal F5.
- Current 8.14–8.18 become 8.17–8.21.

Every prose reference, dependency-flow item, FR/AR coverage-map entry, External Prerequisites Register consumer, and story prerequisite must use the final IDs. Re-run `bmad-sprint-planning` so `sprint-status.yaml` is regenerated from the corrected epic document.

## 7. Implementation Handoff

### Scope classification

**Moderate.** The work reorganizes the backlog and changes story boundaries and IDs. It does not require product or architecture replanning.

### Recipients and responsibilities

- **Product Owner / planning owner**
  - approve the story splits, moves, and final ordering;
  - confirm that the direct-adjustment path preserves priority;
  - accept the conditional Epic 5 post-baseline closure.
- **Developer agent**
  - apply the approved edits to `epics.md` atomically;
  - update all references and prerequisite rows;
  - validate that every C-01–C-19 finding is closed by the edited text.
- **Sprint-planning workflow**
  - regenerate `sprint-status.yaml` after the epic edit;
  - verify story keys and dependencies after renumbering.
- **Administrator / external owners**
  - start the early independent work: pre-upgrade restore proof, repository controls, deputy nomination, monitor/notification setup, and replacement-capacity procurement.

### Success criteria

The correction is complete when:

1. Every readiness finding C-01–C-19 maps to an explicit edited passage in `epics.md`.
2. No story accepts evidence before its policy or contract exists.
3. No recovery story claims usable recovery-point, data-restore, or erasure-continuity evidence before Epic 8 creates it.
4. Shared-infrastructure and environment-layer ownership matches architecture update 4.
5. Reference-composition suites and stable McpCli publication have named owners and stories.
6. Oversized Stories 4.1 and 6.6 are split into independently verifiable work.
7. `sprint-status.yaml` is regenerated without stale story IDs.
8. A repeated implementation-readiness review reports no remaining C-01–C-19 concern.

## 8. Decision Requested

**Decision:** Approved by Administrator on 2026-09-28.

Approval authorizes the `epics.md` correction and subsequent sprint-plan regeneration. No source requirements, architecture decisions, code, or deployed systems are changed by this approval.

## 9. Workflow Execution Log

- **Issue addressed:** Implementation-readiness findings C-01–C-19.
- **Review mode:** Batch.
- **Approved approach:** Direct adjustment within the existing epic structure.
- **Scope classification:** Moderate.
- **Artifacts modified by this workflow:** This Sprint Change Proposal only.
- **Artifacts authorized for the implementation handoff:** `epics.md`, followed by regenerated `sprint-status.yaml`.
- **Unaffected source artifacts:** PRD, architecture spine, specification package, and UX.
- **Routed to:** Product Owner / planning owner and Developer agent.
- **Product Owner responsibility:** Maintain priority, approve the final story boundaries, and accept the conditional Epic 5 post-baseline closure.
- **Developer responsibility:** Apply the approved story edits and renumbering atomically, update every reference and prerequisite, and verify closure of C-01–C-19.
- **Sprint-planning responsibility:** Regenerate and validate `sprint-status.yaml` after `epics.md` is corrected.
- **Handoff status:** Complete; implementation has not yet been performed.
