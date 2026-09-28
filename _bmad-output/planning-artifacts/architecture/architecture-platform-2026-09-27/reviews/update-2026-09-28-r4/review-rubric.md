# Update run 4 — good-spine rubric review

**Verdict: CONDITIONAL PASS — two consistency corrections remain; neither requires a new policy choice.** The accepted candidate-evidence, attachment, admission and degraded-production changes are enforceable and their remaining shared policies are gated before consumers use them. Two older general clauses still allow the release executor and record-store implementations to choose incompatible behavior.

**Counts:** 0 critical, 0 high, 2 medium, 0 low. **Actions:** 2 autofix; 0 discuss; 0 additional deferrals.

Reviewed 2026-09-28 against the full 499-line `../../ARCHITECTURE-SPINE.md`, SHA-256 `5e51d120fc7acc3f6fec709b1ea73e37970cdbd9992bd89fe6ee9b2fef5659d2`, status draft. Inputs: the full current Platform PRD, the update-4 memlog tail, applicable earlier decision entries, `confirm-r3.md`, and this run's clean `lint.json`. The reviewer gate's good-spine checklist governs this review. Source files were not edited; this report is the only write.

The user's latest decision is binding: degraded non-empty repair requires valid baseline compatibility evidence or a named data-restore recovery. Failure removes candidate workloads and retains data; this path never acquires automatic rollback. All previously accepted risks, run-3 G-1–G-5 decisions, module overrides and later-stop behavior remain binding.

## Findings

### RB4-1 — General baseline preparation and compatibility clauses still include paths that explicitly have no baseline rollback

**Medium · autofix · high confidence.** This is a scope correction to already accepted branches, not a request to reconsider recovery policy.

**Evidence:**

- AD-15, L219, requires the working baseline's rollback set unless an approved release names a separately planned recovery.
- Catalogs, L232, says activation “in every environment” prepares candidate and rollback generations and ready-validates the rollback generation “on the running baseline hosts”.
- Release modes, L287, routes missing compatibility evidence to a named recovery point; precondition 9, L317, requires compatibility evidence against production's working baseline without an empty-production qualifier.
- Empty or degraded production, L288, explicitly admits empty production, uses fresh-install proof, removes candidate workloads on failure and never automatically deploys the old baseline. The latest choice applies the compatibility-or-named-recovery requirement specifically to degraded **non-empty** production.
- The flowchart is correctly qualified at L263 as compatible deployment over a working baseline, but the Catalogs prose is not.
- The prior accepted C-11 decision in the memlog, L222, already gives empty/degraded production a remove-workloads-keep-data failure render. The update-4 decision at memlog L270 preserves that failure behavior while strengthening non-empty data compatibility.

**Concrete divergence:** The executor built from L232 and precondition 9 refuses the first G1 deployment because no running baseline or baseline-relative compatibility evidence exists. It then requests a data-restore plan whose recovery release is also absent. An executor built from L288 correctly accepts fresh-install proof and removes candidate workloads on failure. A degraded baseline with no running hosts exposes the same unqualified ready-validation requirement even when the newly required compatibility evidence exists. An approved named-recovery release is expressly exempt from the AD-15 rollback set, but the universal Catalogs sentence still asks it to prepare one.

**Minimal correction:** Define the applicability once and make the general clauses reference it:

1. Scope Catalogs' baseline-host ready-validation and rollback-generation preparation to the compatible deployment branch that uses the AD-15 rollback set. Empty/degraded and named-recovery attempts use their existing candidate and failure/recovery branches.
2. State explicitly that an empty-production attempt has no baseline-relative compatibility or baseline rollback preparation; its fresh-install evidence and remove-workloads failure behavior apply.
3. Preserve the latest user's requirement verbatim for degraded non-empty production: valid compatibility evidence or the named data-restore recovery. This evidence never enables automatic rollback on that path.

This does not waive provenance, staging E2E, verification, locking, admission or retained-data protections. It makes the general preparation wording agree with the accepted exceptional paths.

### RB4-2 — Promotion-stop records no longer explicitly bind their environment

**Medium · autofix · high confidence.** Restore the existing per-environment record decision; do not choose a new stop model.

**Evidence:**

- Binding classes and records, L231, assigns writers to “promotion-stop set records” but no longer explicitly calls the stop per-environment, although it does explicitly scope the latest-working pointer to an environment.
- Promotion stop, L295, says it is set by “every non-working terminal outcome” and “every recovery entry”.
- Staging gate, L285, says a staging failure blocks only that candidate. The common Recovery sequence is also used for staging resets and says to set/keep the stop at L323.
- AD-8, L147, requires staging automation to be denied production records.
- The accepted memlog decisions are explicit: V-30, L187, calls the stop “a durable field of the per-environment record” and says a staging failure blocks its candidate only; C-35, L221, repeats “per-environment promotion stop and latest-working pointer with their own set/clear records”.

**Concrete divergence:** A record-store implementer can expose one global stop because the rendered spine names a single promotion stop and permits every executor to set it. A staging runner can then set the production promotion stop on its own failed E2E or reset, contrary to the candidate-only staging rule and staging's denied production-record authority. Another implementer keeps the accepted environment-qualified records. Both fit the current abbreviated writer list.

**Minimal correction:** Restore “per-environment” in Binding classes and records and require the environment identity on each stop set/clear record. In Promotion stop or Staging gate, state that staging outcomes and resets affect their staging/candidate state, not production's promotion-stop record. Production preconditions and Administrator clearance refer to the production record. Preserve the existing writers, revision/CAS behavior and accepted continuing-cause handling.

## Accepted changes confirmed

| Change | Result and evidence |
| --- | --- |
| Candidate content identity | AD-4 L102 fixes the candidate's entire consumed artifact set, including Contracts, extensions, services and run-scoped McpCli. AD-5 L112 joins expected identities to actually loaded identities, rejects substitution/uncommitted inputs and excludes baseline-only evidence. |
| Candidate policy ownership | L451 and L465 place revision selection, the gated merge/release decision, reuse and encoding behind one shared runner/descriptor contract before consumers implement acceptance. This is a legitimate deferral. |
| Bounded attachment holds | AD-10 L167–169 fixes immediate first terminal outcomes, any-local-failure retention, holds, finite expiry, withdrawn results, owner-finish refusal, cancellation ordering and same-job CI attachment. The descriptor and SM-3 proof gates are present at L451 and L464–465. |
| Degraded definition and compatibility choice | L288 and L483 carry the broader degraded definition and the latest compatibility-or-named-data-restore choice. Later stop sets cannot be cleared by an older approval. RB4-1 is a remaining general-clause scope correction. |
| Admission matching and clearance | L241 checks admission in both directions and notifies both recipients; L295 binds each cause, Administrator reviews, admission agreement and compare-and-set. Accepted continuing causes are not immediately recreated. |
| Deputy limits and reopening | L37, L123 and L329 preserve Administrator-only releases, stop clearance and admission administration; reopening restores the recorded ingress state and stays closed before G2. |
| Grants omitted by restore | Step 5 L327 lists post-cut grants for Administrator to reapply and prohibits recovery from adding them. In-place recovery instead preserves the live realm. |
| Shared-infrastructure failure | L250 names forward revert or DR, a non-working outcome, stop and degraded production, matching current PRD FR-10. |
| Run-3 CF3-1 | AD-3 L90, AD-12 L188 and Catalogs' recovery sentence L232 distinguish automatic recovery, baseline redeploy, data restore and the owning executor. RB4-1 concerns the earlier unqualified activation sentence. |
| Run-3 CF3-2 | L291 fixes lifetime anchors, preserves absolute recovery lifetimes, records the new epoch on same-attempt resumption and keeps consumed recovery allowance and clocks. |
| Run-3 CF3-3 | L290 distinguishes reserving an epoch for control actions from accepting takeover for operational mutation after fencing/draining. |
| Run-3 CF3-4 | L241 explicitly notifies Administrator and deputy on admission mismatch. |

## AD enforceability sweep

| AD | Rubric result |
| --- | --- |
| AD-1 | Enforceable: one composition, one exporter/helper, retained render modes, concrete fallback trigger. |
| AD-2 | Enforceable: digest identity, release-record binding, provenance, retention and no regeneration. |
| AD-3 | Enforceable: unique tiers, application-only rollback, explicit recovery routing and Dapr resource changes included in changed-workload detection. |
| AD-4 | Enforceable: one active-root mapping, modes, Platform/catalog/tool identities and candidate substitutions are explicit. Shared encoding is gated. |
| AD-5 | Enforceable: CI isolation, blocking integration tier and loaded-artifact evidence are explicit. |
| AD-6 | Enforceable: realm/admission authority, chained durable records, recovery limits and credential boundaries are specified. |
| AD-7 | Enforceable target: executor boundaries, recovery-point access, per-job custody and epoch-bound revocable credentials have shared rules and qualification gates. No claim of implemented fencing. |
| AD-8 | Enforceable: namespace, data, Dapr and network isolation plus the expanded negative matrix. RB4-2 restores the record convention needed by its staging-denial rule. |
| AD-9 | Enforceable: default Dapr boundary, closed exception list, logical names and declarative subscriptions. |
| AD-10 | Enforceable: ownership, outcome, hold and withdrawal state transitions now agree with the PRD. |
| AD-11 | Enforceable: static tool identity, served eligibility, actor/profile rules, publication and legacy-surface retirement. |
| AD-12 | Enforceable target: recovery ownership, classes, key custody, hook-version window and manual unseal are explicit; qualification remains gated. |
| AD-13 | Enforceable: composed-host subject, extension boundary, supported-major rule and artifact ownership. |
| AD-14 | Enforceable: client-derived surface, attested origin, actor rules and negative cases. EventStore attestation remains a consumer gate. |
| AD-15 | The rollback-set and expand-only rules prevent the intended divergence; exceptional branch applicability needs RB4-1. |

## Other rubric checks

**One level down and deferrals.** The document assigns publication, runner, executor, module, EventStore and operations boundaries explicitly. Shared declaration, descriptor, realm, record, hook and catalog contracts must precede their consumers. Candidate policy and attachment limits are fixed before evidence acceptance or attachment implementation; monitor/admission bounds before G1; reduced-recovery policy before G2; automatic-promotion currency policy before G3. None is a missing architecture input merely because a numerical value remains unselected. Consumers are expressly forbidden to choose their own policies. No additional deferred item needs promotion into an AD in this review.

**PRD coverage.** FR-1–FR-12 and NFR-1–NFR-3 remain mapped. The updated FR-4/SM-3 rules and FR-8/FR-9/FR-10 corrections landed. No recovery budget, staging gate, admission rule or accepted data-loss boundary was weakened. The PRD's adoption/confirmation status and the stale spec are already assigned source-alignment work before deployment/recovery stories are finalized; that is not a new finding.

**Breadth at initiative altitude.** Composition, runtime boundaries, state mutation, contracts, source/package identity, CI, hosted identity and isolation, provider binding, deployment, release state, operations, monitoring, recovery, migration and ownership are decided or explicitly gated. The operational envelope is substantial and not silent. There is no inherited parent-spine override concealed as a local choice; module overrides are named in Source precedence.

**Technology evidence.** No new technology version is selected by update 4. The Stack remains existing seed with explicit prerelease and qualification treatment. The current update evidence records the local SDK pins and known Builds implementation gaps; this review does not reclassify those as implemented target capability. The revised candidate contract is consistent with Kubernetes' distinction between mutable tags and immutable image digests ([official image documentation](https://kubernetes.io/docs/concepts/containers/images/#image-names)). GitHub documents different commit identities for PR merge-result and head testing, supporting the explicit shared candidate-revision policy gate ([official workflow-event documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#pull_request)). Official sources were opened on 2026-09-28. No upgrade recommendation or new technology-currentness finding is made.

**Brownfield truth and readiness.** The Works-only preview, unaccepted Builds tool versions, metadata-only state and `HXR003` gap remain visible. The spine describes its target contract and expressly disclaims production qualification. No runtime or live production evidence was created or inferred by this review.

**Mechanical checks.** The supplied update-4 linter has zero findings. The parent independently confirmed 15 preserved AD IDs and 10 resolving local links. Those mechanical results do not replace the semantic findings above.

## Decision boundary

No new user decision is requested. RB4-1 follows accepted first-install/degraded/named-recovery branches and the latest non-empty compatibility choice. RB4-2 restores the accepted per-environment stop-record scope. Remaining implementation proof and shared policy settings stay with their existing owners and gates. All accepted risks remain unchanged.
