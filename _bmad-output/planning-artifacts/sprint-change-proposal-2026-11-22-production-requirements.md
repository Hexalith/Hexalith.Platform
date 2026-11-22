# Sprint change proposal for production readiness

22 November 2026 · Owner approved and planning edits applied · Platform revision `b0124db4` with both approved November 22 planning corrections

## 1. Issue summary

The full sprint-planning gate found three remaining production requirements that block the tracker refresh. The tracker matches all 12 epics and 151 story IDs. The earlier November 22 re-gate passed for the issues addressed in its approved proposal. [Current readiness findings](implementation-readiness.md#november-22-full-planning-gate-follow-up) · [Earlier approved proposal](sprint-change-proposal-2026-11-22.md)

| Finding | Severity | Conflict |
| --- | --- | --- |
| IR-2026-11-22-4 | High | Story 6.22 authorizes a narrowly scoped production foundation bootstrap using verified native recovery evidence. PRD FR-10, its addendum and spec CAP-10 still state the complete-point rule without the exception. |
| IR-2026-11-22-5 | High | Story 6.7 depends on Story 6.9 proving the production store is reachable from the recovery executor. Story 8.9 commissions that executor once usable production points exist. The plan needs an earlier, testable reachability milestone on the selected host. |
| IR-2026-11-22-6 | Medium | AR-35 requires a complete recovery point after locking production for every approved retained-data attempt. Story 6.18 attaches that point to named restore and admits compatible repair at G1 without stating its point requirement. |

## 2. Impact analysis

| Area | Delivery impact |
| --- | --- |
| Epics 1–3, 11–12 | Local development, CI, toolchain, legacy retirement and post-MVP enrollment have independent acceptance criteria. |
| Epic 4 | Its accepted staging foundation remains the prerequisite for the first production foundation and supplies the working release and isolation checks during bootstrap. |
| Epics 5 and 6 | Story 5.11 supplies exact-release staging evidence. Stories 6.14 and 6.18 enforce the production point check for both approved retained-data branches. Stories 6.7 and 6.9 consume early recovery-host reachability. Story 6.22 retains the production-only bootstrap inventory and guard. Story 6.23 uses the existing first-empty G1 admission path. |
| Epics 7 and 8 | Story 8.1 supplies the selected off-site host early. Stories 8.4–8.7 supply complete recovery-point readiness before the first approved retained-data attempt. Story 8.9 commissions the executor after usable points and the other recovery inputs exist. Epic 7 access controls and the independent G2 drill still apply. |
| Epics 9 and 10 | SM-5 reuses admission refusal and recovery evidence. G3 automatic promotion continues under its existing release policy. |
| PRD, spec and architecture | FR-10 and CAP-10 need the approved bootstrap boundary. FR-8, CAP-7/CAP-8 and the architecture admission rules need compatible and named recovery to share the post-lock point requirement. AD-7 and the first-production sequencing need distinct acceptance evidence for store reachability and commissioning. |
| Operator evidence | Existing foundation and production attempt records carry the evidence and refusal reasons. The reachability proof uses a scoped store read. Backup capture and verification run under the existing production lock. These are pipeline and operations acceptance changes in the affected stories. |

The Platform epic inventory assigns product UI to modules and defines the operator surfaces through its FRs and ARs. The proposed changes affect the existing operator records and diagnostics. [UX Design Requirements](epics.md#ux-design-requirements)

## 3. Recommended approach

Apply a direct adjustment to the existing requirements and acceptance criteria. Pull forward the host-preparation portion of 8.1 before 6.9, and schedule the recovery-point work in 8.4–8.7 before any approved retained-data release needs it. The initial G1 installation and the full G2 commissioning and drill keep their existing scope. Rollback would reopen the already approved foundation and recovery decisions. An MVP scope change would leave first production and retained-data admission without agreed criteria.

Estimate half to one working day for the planning edits and re-gate after approval. Live scheduling depends on the off-site host and store connection, and on recovery-point production for retained-data repairs. The main delivery risk is an early failure that leaves data before a usable point or compatible baseline exists. In that case keep the stop and closed ingress, preserve data, and refer the incident to Administrator. Plan point generation early where repair availability matters. Story 8.9 commissioning is downstream of point generation.

## 4. Detailed changes

### A. Record the approved first-production foundation exception

**PRD FR-10 and CAP-10.** The PRD currently says, “Shared-infrastructure changes run as one controlled change covering both environments, after a complete recovery point.” CAP-10 repeats the same requirement, and the PRD addendum says “every shared change” follows it. [PRD FR-10](prds/prd-platform-2026-09-27/prd.md#fr-10-provide-isolated-staging-and-production-environments) · [PRD addendum](prds/prd-platform-2026-09-27/addendum.md#release-modes-and-production-entry) · [Spec CAP-10](../specs/spec-platform/acceptance-criteria.md#cap-10--isolated-staging-and-production-nfr-3)

**Proposed FR-10 wording**

> Ordinary shared-infrastructure changes run as one controlled change covering both environments under both locks after a complete recovery point. Re-run each environment’s working-release smoke checks and the applicable NFR-3 isolation checks. From G1, rehearse on a production-profile copy on prepared capacity before applying the change. An Administrator record for an urgent security patch applied in place can waive that rehearsal. The complete recovery point remains required for the patch.
>
> With the staging foundation accepted, the named first-production foundation procedure may run before a complete production recovery point exists, while production has neither workload data nor a working baseline. Administrator approves the exact production-only inventory, one named change owner and the recovery procedure. The bootstrap holds both environment locks, records one attempt per environment, and uses current, verified native recovery evidence for existing infrastructure. The shared-infrastructure identity creates the dedicated production namespaces, StorageClass and PriorityClass. Administrator establishes the reviewed new environment-identity grants before the environment-layer identity applies its resources. Existing staging or shared service objects, existing grants and cluster defaults are outside the inventory. Record staging smoke and isolation results and keep production user ingress closed. Record partial effects and require review of the observed state and exact remaining effects before resumption.

**Apply in the addendum, CAP-10 and Epic 6.** The Epic FR-10 inventory likewise says “after a complete recovery point” for all shared changes. Qualify each occurrence with “Ordinary shared-infrastructure changes require a complete recovery point. FR-10 permits the first-production foundation to use Story 6.22’s named, create-only procedure with both locks and current verified native recovery evidence.” Retain the applicable smoke, isolation, stop, rehearsal and forward-revert criteria. Epic 6 currently says “successful execution and forward-revert qualification wait until complete recovery points exist.” Replace that clause with “The named first-production foundation follows Story 6.22’s create-only bootstrap under both locks. Ordinary shared-infrastructure mutation and forward-revert qualification require complete recovery points.” Use FR-10 as the authority cited by the architecture release-tier exception and 6.22. [Epic FR inventory](epics.md#functional-requirements) · [Epic 6](epics.md#epic-6-deploy-to-production-with-verification-and-one-shot-recovery-g1-1) · [Story 6.22](epics.md#story-622-guard-shared-infrastructure-changes-with-one-controlled-procedure) · [Architecture release tiers](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#release-tiers)

**Acceptance.** The exact reviewed create-only inventory can proceed with both locks, approved native recovery evidence, accepted staging and the other 6.22 guards. Missing authorization, native evidence or either lock refuses that path. Existing objects and grants, a cluster default change, or a target with production workload data or a working baseline require the ordinary path. Verify staging smoke, isolation and closed production ingress before accepting the foundation.

### B. Prove store reachability before recovery executor commissioning

**Before.** Story 6.7 lists the store as a prerequisite. Story 6.9 says the recovery executor can “reach” it. Story 8.1 selects and hardens the host. Story 8.9 commissions it with usable points, the recovery contracts, off-site artifacts and custody available. [Story 6.7](epics.md#story-67-build-the-production-environment-layer) · [Story 6.9](epics.md#story-69-provide-the-production-attempt-lock-and-stop-store) · [Story 8.1](epics.md#story-81-procure-prepared-replacement-capacity-and-select-the-recovery-executor-host) · [Story 8.9](epics.md#story-89-fence-the-failed-environment-and-rebuild-it-on-prepared-capacity)

**Proposed Story 6.9 criterion**

> Given the production store is deployed outside the cluster and all executor hosts, and the dedicated off-site recovery host has been selected, reserved and hardened under Story 8.1, when the store is accepted for Story 6.7, then the production executor and off-site monitor demonstrate access under their existing scopes. From the selected off-site recovery host, use the intended network path, production store endpoint and TLS trust to authenticate with a short-lived identity restricted to reading a designated non-sensitive probe record. A successful read of that record proves recovery-host reachability. Retain the selected host identity, store endpoint, time, credential scope and result without secret material. Missing or failed proof blocks the foundation. Re-prove it if the host, endpoint, network path or trust changes before the foundation uses it. Keep the probe separate from live locks, attempt records, the promotion stop and the working pointer.

**Proposed changes at the dependency boundaries**

| Artifact and section | Current wording or scope | Proposed wording |
| --- | --- | --- |
| Epic 6, Story 6.7 prerequisites | “off-cluster attempt and lock store (6.9)” | “accepted off-cluster attempt and lock store, including the Story 6.9 reachability proof from the selected off-site recovery host” |
| Epic 8, Story 8.1 host criterion | The host is selected, reserved and hardened for later commissioning. | “Complete host selection, reservation, hardening and provisioning of the intended store network path before Story 6.9 acceptance. Record the selected host for the later Story 8.9 commissioning.” |
| Epic 8, Story 8.9 commissioning criterion | Commissioning uses the selected host, completed contracts, off-site artifacts, the store, usable points and custody. | “Given the selected Story 8.1 host, accepted Story 6.9 store connectivity, completed contracts and off-site artifacts, verified usable Story 8.7 recovery points and the custody release path, commission the executor with the existing pinned workflows and scoped recovery credentials. Recheck the store path with the commissioned identity and demonstrate the required record and epoch operations on isolated verification records.” |
| Architecture AD-7 and Owned work, AR-58, and spec Sequencing G1 and environment layer | The first production attempt needs a store “reachable from the production executor, recovery executor and monitor.” | “Before Story 6.7 consumes the production store, Story 6.9 records reachability from the production executor, off-site monitor and the designated Story 8.1 recovery host. An authenticated read of the designated probe record at the production endpoint from that host is sufficient for this milestone. Story 8.9 commissions the recovery executor after usable production recovery points and the other recovery inputs exist.” |

The early identity has read access to the probe record only. Story 8.9 retains the existing pinned off-site workflows, Administrator/deputy start authority, credential custody, and independence from GitHub and repository write access. [Architecture AD-7](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#ad-7--isolated-private-network-executors-adopted-amended) · [Spec G1](../specs/spec-platform/sequencing.md#g1--production-deployed-ingress-closed) · [Story 8.7](epics.md#story-87-record-usable-recovery-points-at-one-declared-cut)

**Acceptance.** A successful authenticated read from the selected off-site host satisfies the early requirement while Story 8.9 is still pending. A probe from a different host, a network-only check, a failed read or stale evidence after a topology change refuses foundation entry. Story 8.9 must still prove the commissioned authority and workflows once the recovery inputs exist. The Story 8.17 drill supplies the independent G2 evidence.

### C. Require a complete post-lock point for compatible G1 repair

**Before.** AR-35 says “An approved retained-data attempt starts only after a complete recovery point cut after the lock.” Story 6.18 includes the usable post-lock point only in the sentence about the named path. Story 6.14 precondition 9 checks compatibility or the named qualification. [AR-35](epics.md#additional-requirements) · [Story 6.14](epics.md#story-614-refuse-production-attempts-that-fail-a-precondition) · [Story 6.18](epics.md#story-618-run-administrator-approved-attempts-into-empty-or-degraded-production)

**Proposed Story 6.18 wording**

> For an Administrator-approved empty or degraded attempt, require exact-release fresh-install proof under Story 5.7 and the common admission checks. When application data remain, require valid compatibility evidence against the recorded baseline or a qualifying named restore. For either retained-data branch, including compatible repair at G1, take the production lock and cut a complete, verified FR-9 recovery point under that lock before any candidate rollout or workload mutation. Bind the point to the production instance, attempt and current lock epoch in the admission record. Refuse a missing, pre-lock, incomplete, unusable or mismatched point before mutation.
>
> At G1 use the compatible branch for retained data. After G1, the named branch additionally requires the separately qualified in-place restore capability and the exact candidate/baseline qualification under Story 8.16. A first empty installation with no retained application data uses fresh-install proof and the common gates. If a failed first installation leaves data without a working baseline, keep the stop and closed ingress and preserve the retained data. Refer the incident to Administrator if the existing retained-data gates cannot be met.

**Proposed Story 6.14 and AR-38 precondition 9 wording**

> For retained application data, require valid compatibility evidence against the recorded baseline or Administrator-approved mode with the separately qualified named data-restore recovery. In Administrator-approved mode, both retained-data branches require a complete, usable FR-9 point cut after taking the production lock and verified before workload mutation. Record the point and its binding to the instance, attempt and epoch before admitting the release. The existing G1 capability boundary applies.

**Apply in the other admission sources**

| Artifact and section | Current wording or scope | Proposed replacement or addition |
| --- | --- | --- |
| PRD FR-8, addendum, and Epic FR-8 inventory | Compatibility and named restore are alternatives, with the cut detailed in the incompatible branch. | “When application data remain, an Administrator-approved attempt requires valid baseline compatibility or a qualified named restore. In either case it requires a complete, usable recovery point under FR-9, cut after taking the production lock and verified before workload mutation.” |
| Architecture RRA, Release modes, Empty or degraded production and Production precondition 9 | The explicit post-lock requirement is attached to named recovery. | Move the common approved retained-data cut to precondition 9 using the Story 6.14 wording above. Reference it from both release-mode branches. Keep the named path’s rehearsal, exact qualification, authority, time bound and failure handling. |
| Platform spec, Constraints and CAP-7/CAP-8 | The point is attached to the named path. CAP-8 says fresh-install proof “applies only to a first installation.” | “Approved empty or degraded attempts require the exact candidate’s fresh-install proof. With retained data, they also require valid baseline compatibility or qualifying named restore. Either approved retained-data branch requires the complete, verified post-lock point before workload mutation. At G1 compatibility is the available retained-data branch.” Apply the same common-gate wording to the staging preparation summary in CAP-6 and Sequencing. |
| Spec Sequencing, architecture Owned work, AR-58 and Story 8.7 | The first degraded non-empty attempt gates compatibility or named recovery. Story 8.7 specifies the verified cut. | “Before the first approved retained-data attempt, make the Story 8.7 recovery-point capability available with its recovery inventory, backup chains and custody inputs from Stories 8.4–8.6. Cut and verify the attempt’s own point under its lock. Apply this dependency at admission, including compatible G1 repair.” |
| Epic 5, Story 5.9 closing criterion | “The named production path also requires the recovery point cut after taking the production lock.” | “Both approved retained-data production paths require the complete point cut after taking the production lock under Stories 6.14 and 6.18.” Story 5.11 supplies the staging evidence for that admission check. |

[PRD FR-8](prds/prd-platform-2026-09-27/prd.md#fr-8-restore-and-verify-the-previous-working-release) · [Architecture production preconditions](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#production-preconditions) · [Spec Constraints](../specs/spec-platform/SPEC.md#constraints) · [Spec CAP-8](../specs/spec-platform/acceptance-criteria.md#cap-8--verified-recovery-to-the-previous-working-release-nfr-1) · [Spec ordering](../specs/spec-platform/sequencing.md#other-ordering-rules)

**Acceptance.** An otherwise eligible compatible G1 repair with valid, exact staging evidence and a complete point cut under the current lock can proceed. Missing, pre-lock, incomplete, wrong-instance or wrong-epoch point evidence refuses before rollout, even with valid compatibility. A named path after G1 needs both its qualification and the same point check. A first empty installation without retained application data still uses its ordinary first-install criteria. A failed first installation with retained data and no qualifying baseline or point stays stopped with ingress closed and data preserved.

## 5. Implementation handoff

**Scope.** Moderate. The Product Owner and Developer coordinate the planning updates and the handoff between Epics 6 and 8. The plan retains the existing epic and story IDs.

| Owner | Handoff |
| --- | --- |
| Product Owner with Platform architecture | Record the approved 6.22 exception in FR-10 and CAP-10. Ratify the AD-7 sequencing and the FR-8 approved retained-data admission rule across the source documents. |
| Platform and Builds with Administrator | Specify the early 8.1 host evidence, the 6.9 scoped read and the acceptance dependency in 6.7. Implement and verify these in the existing operations stories. |
| Platform recovery maintainers with Administrator and module owners | Schedule 8.4–8.7 before the first approved retained-data attempt. Verify the per-attempt cut through 6.14/6.18. Commission 8.9 once its prerequisites exist and retain the 8.17 drill for G2. |
| Sprint owner | Apply the accepted edits and run the full planning gate. On PASS, verify the 12 epic and 151 story IDs and preserve the existing keys, statuses and action items when refreshing the tracker. |

Administrator approved this proposal on 22 November 2026. The planning edits and re-gate are complete. The PRD, spec, architecture and stories agree on all three rules. The delivery teams own the live bootstrap, admission and recovery proofs in the existing stories. [Readiness re-gate](implementation-readiness.md#november-22-re-gate-after-the-production-requirements-correction) · [Epic breakdown](epics.md) · [Sprint tracker](../implementation-artifacts/sprint-status.yaml)

## Workflow checklist

| Items | Status |
| --- | --- |
| 1.1–1.3, 2.1–2.5 | Done. Recorded the gate findings, mapped the delivery impact and defined prerequisite order. |
| 3.1–3.4 | Done. Identified source and operations changes. A Platform product UI specification is inapplicable. |
| 4.1–4.4 | Done. Direct adjustment is viable. Rollback and MVP reduction do not resolve the admission and commissioning rules. |
| 5.1–5.5, 6.1–6.2 | Done. Drafted the proposal, acceptance cases and ownership plan, and checked the source references. |
| 6.3, 6.5 | Done. Recorded owner approval and the planning and delivery handoff. |
| 6.4 | Done. Reconciled and refreshed the tracker after the planning re-gate. The 12 epic IDs, 151 story IDs and 19 action items were retained. |
