# Adversarial architecture review — update 2026-09-28, run 4

**Verdict: FAIL pending two narrow consistency corrections.** Counts: **0 Critical, 2 High, 0 Medium, 0 Low**. The updated artifact, attachment and promotion-stop rules close their direct divergence cases. Two baseline-dependent clauses still block the accepted empty/degraded deployment paths.

Reviewed the current 499-line `ARCHITECTURE-SPINE.md`, the accepted decisions at the tail of `.memlog.md`, the finalized Platform PRD FR-4/FR-8/FR-9, and the run-3 confirmation. Line references below refer to this draft. This review changes no source document or runtime state.

The configured adversarial lens asks whether two independently built components can follow their respective contract obligations and still fail to compose. In the findings below, one component produces valid inputs for an explicitly permitted path while the other enforces a general rule whose prerequisites that path cannot supply. These are contradictions between contract sections, rather than proposed resilience features.

Settled policies remain binding: degraded non-empty attempts require valid compatibility evidence or a named data-restore recovery; every failed approved empty/degraded attempt removes candidate workloads while keeping data; none has automatic rollback. Candidate revision, evidence reuse and finite hold policy remain one shared, explicitly gated Builds/Platform contract. This review requests no numeric defaults.

## ADV4-1 — The first empty deployment is still routed into nonexistent baseline evidence

**High · autofix from accepted first-deployment policy · high confidence.**

**Evidence.** Staging gate (L285) explicitly uses fresh-install proof when production has no working baseline. Empty or degraded production (L288) permits that approved attempt and removes its workloads on failure. PRD FR-8 explicitly classifies the first deployment as an approved empty-production attempt. However, Production precondition 9 (L317) unconditionally requires a rehearsal against production's working baseline and routes *missing* evidence into a named planned recovery. Release modes (L287) then unconditionally requires a complete post-lock recovery point for missing compatibility evidence. AD-15 Rollback set (L219) also requires a baseline package unless the approval names that recovery.

**Two independently built components.**

1. The staging evidence producer implements L285. For the first release `R1`, before any application data or baseline exists, it proves a fresh install, passes every enrolled module's exact-release E2E suite, and emits release/profile/suite identities. The approval author implements L288 and the PRD: it approves one empty-production attempt, accepts candidate removal on failure, and does not invent a preceding working application or recovery release.
2. The production precondition evaluator implements L307–317 and Release modes literally. No baseline rehearsal exists, so it requires a named data-restore recovery. The recovery-point writer implements L298: a usable point includes its compatible release/configuration identity. There is no previously working application release to restore, and no baseline package for AD-15.

The valid first-install input never satisfies the production consumer. Implementing the explicit empty path requires that one component silently invent an exception to the general text. A dummy baseline or an arbitrary named restore would change the accepted failure behavior and would not resolve the missing contract.

**Impact.** The first G1 deployment cannot start under the published preconditions, even with all intended first-install evidence and Administrator authorization present.

**Minimal correction.** In precondition 9 and Release modes, state the existing first-empty-bootstrap branch explicitly: when there is no working baseline and no retained application data requiring recovery, exact-release fresh-install proof satisfies this branch; no nonexistent baseline rehearsal or baseline data-restore plan is required. Its failure closes ingress and removes candidate workloads while keeping data, as already specified. Keep compatibility or named data restore mandatory for repair against retained data; the correction must not make “empty” or fresh-install proof a blanket bypass for retained state. Scope the rollback preparation and catalog clauses as described in ADV4-2 so they do not reintroduce the same dependency after preconditions pass.

**Decision disposition.** Apply as clarification of accepted first deployment. No new release waiver or recovery mode is requested. Do not change staging, provenance, isolation, verification, approval or stop requirements.

**Convergence check.** With no prior release/data, first-install staging evidence and the approval pass the release-mode gate; failure removes the candidate and leaves ingress closed. With retained application data, fresh-install evidence alone still cannot establish the chosen retained-data requirement.

## ADV4-2 — A compatible degraded retry requires baseline hosts that the previous failure removed

**High · autofix from accepted degraded policy · high confidence.**

**Evidence.** L288 permits a degraded non-empty attempt with valid baseline compatibility evidence and expressly removes candidate workloads on failure without automatically deploying the baseline. Automatic recovery (L294) excludes these attempts. Nevertheless, AD-15 (L219) exempts only a release with named recovery from rollback-set preparation, and Catalogs (L232) prescribes activation “in every environment” as preparation of candidate and rollback generations followed by ready-validation of the rollback generation **on the running baseline hosts**. The same Catalogs wording also fails to honor AD-15's existing exemption for releases with named recovery.

**Two independently built components.**

1. The approved-attempt executor follows L288. Production's recorded baseline is `B`. A previous approved degraded compatible attempt `C1` fails; the executor closes ingress, removes `C1` workloads, preserves data and records the new stop. It correctly leaves no application serving. Administrator subsequently approves `C2`, with all common evidence, fresh-install proof, valid compatibility evidence against `B`, the observed stop revision and cause dispositions. No named restore is required under the accepted compatibility-or-recovery choice.
2. The catalog activation component follows AD-15 and L232. Because `C2` has no named recovery, it prepares the rollback set and waits for ready-validation on running baseline hosts before allowing the candidate upgrade. There are no such hosts: component 1 was required to leave none serving. Deploying `B` automatically to obtain them would contradict the approved degraded failure path; treating the failed attempt as an automatic rollback opportunity would also contradict L294.

Thus the compatibility-evidence branch is specified as valid but cannot reach its candidate deployment. This also occurs when the incident itself made the baseline hosts unavailable. Requiring a named restore merely to escape this step would collapse the user-selected “valid compatibility evidence **or** named recovery” into a different policy.

**Impact.** The documented repair route deadlocks after exactly the failure behavior the architecture requires. Implementers either refuse a permitted repair or restart the old application without the accepted authority/path.

**Minimal correction.** Scope AD-15's production rollback-set preparation and Catalogs' live-baseline ready-validation to deployment paths eligible for automatic recovery over a working baseline. Define the existing alternative activation branches by reference: approved empty/degraded attempts prepare and validate the candidate generation without requiring running baseline hosts; named data restores use the Recovery sequence's forward recovery generation. A degraded non-empty candidate still needs the chosen retained-data proof—valid compatibility evidence against its recorded baseline, including its staging rehearsal, or the named data-restore recovery. Preserve candidate validation and commit-at-readiness, forward retention, expand-only inputs and candidate removal on failure. Fresh-install proof supplements the retained-data requirement and never replaces it.

This correction also makes the Catalogs general rule honor the named-recovery exception that AD-15 already contains. It does not permit an automatic baseline deployment on the degraded path.

**Decision disposition.** Apply as a scope correction across AD-15 and Catalogs. No new user policy choice is required. The record/schema owners can encode the branches in their existing shared contract; no additional service or orchestration layer is needed.

**Convergence check.** Exercise a degraded retry with a recorded baseline, retained compatible state and zero application pods. The candidate can start after all relevant evidence and approval checks without starting the baseline first. Repeat with invalid compatibility evidence: it remains blocked unless the named data-restore recovery and post-lock usable point exist. Failure removes the candidate and never triggers automatic rollback.

## Counterexamples rejected because the current contract already forbids them

| Area | Attempted divergence | Binding rule that closes it |
| --- | --- | --- |
| Candidate artifact identity | A module test uses its candidate service but the composed host loads the catalog version of its extension package; another uses a cached package with the same version. | AD-4 Candidate mapping (L102) requires every consumed candidate artifact from the declared revision's own Release build. AD-5 Evidence (L112) compares expected and actually loaded content identities, including composed host and McpCli. Neither substitution is compliant. |
| Candidate revision and reuse | Separate consumers choose PR head versus merge result, or invent different unchanged-module reuse policies. | Owned work (L465) requires one shared published policy before acceptance/attachment consumers implement it. An independently invented consumer policy would violate the gate; the missing numeric/selection values are not a spine omission. |
| Owner success while attachment runs | Owner records success and cleanup deletes resources under an accepted attached run. | AD-10 Outcome and Attachments (L167–168) preserve the terminal owner result while the attachment holds resources until completion/deadline. Deletion during the hold violates the rule. |
| Attached failure races owner cancellation | A late cancellation changes an already recorded attached failure to withdrawn and erases local retention. | First terminal outcomes, any-local-failure retention and “cancellation preserves an already retained local failure” (L167–169) forbid it. An explicit developer cleanup remains the authorized removal path. |
| CI attachment crosses jobs | A second job holds an environment after the owning CI job ends. | AD-10 confines attachments to the same job and requires ending them before job cleanup. |
| Accepted continuing stop cause | Monitor re-adds an accepted unresolved cause immediately after clearance. | Promotion stop (L295) prohibits re-recording until resolution and recurrence; unaccepted continuing causes must be recorded anew. Cause encoding belongs to the common record/store contract. |
| Later stop competes with clear | A clear or approved attempt erases a set after the revision Administrator observed. | L288/L295 require revision-conditioned clearing and explicitly distinguish sets before start from sets during execution. A later set wins. |
| Admission mismatch accepted as a waiver | Approval marks a current mismatch “accepted” and proceeds with live admission different from signed records. | Administrator must confirm admission matches before approving/clearing; G1 acceptance (L484) explicitly proves refusal while it differs. Per-cause disposition cannot waive this review. |
| Fresh-install proof replaces degraded retained-data proof | A degraded release has fresh-install E2E evidence but no valid compatibility evidence or named recovery. | L288 and Owned work (L483) expressly refuse that case. ADV4-2 concerns execution of the evidence-valid branch, not relaxing this rule. |
| Baseline re-deploy bypasses an interrupted data restore | A deputy starts the ordinary baseline after partial data restoration. | In-place recovery (L289) requires data-restore re-entry from the same point; the accepted run-3 G-1 decision intentionally limits baseline re-deploy to failed automatic recovery or baseline re-deploy. That limit is not reopened here. |
| Recovery completion opens pre-G2 access or clears the stop | A deputy reopens arbitrary user access or resumes promotion after restoring service. | Roles, L295 and Recovery step 7 limit reopening to the pre-incident ingress state, preserve pre-G2 closure and keep the stop set. |

## Boundary of this verdict

The report establishes two semantic contradictions in the documented execution paths. It makes no claim that the runner, record store, recovery mechanism, deployed infrastructure or release gate has been qualified. Existing implementation gates and accepted risks remain in force. No technology-version assertions, numerical defaults or new authority rules are proposed.
