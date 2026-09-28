# Platform PRD update — 2026-09-28

Status: finalized; all validation findings and reviewer-gate findings resolved or routed to named owners.

The product owner requested an update driven by the [2026-09-28 validation report](../validation-report.md) (Fair: two High, three Medium). The update also synchronizes the PRD with later user-accepted architecture decisions. It preserves FR-1–FR-12, NFR-1–NFR-3, SM-1–SM-6 and SM-C1–SM-C5, the seven-module MVP, and every existing numeric rollout, backup and recovery target.

## Validation findings resolved

| Finding | Resolution |
| --- | --- |
| S1 (High): restoration revocation guarantees omitted accepted exceptions | FR-9, NFR-2, NFR-3 and SM-6 now state that production admission revocations always survive restoration. Two losses are accepted and reported: module-owned revocations after the cut, and identity-provider access removals not yet captured off the failed environment. Administrator reviews every lost-window exception before clearing the promotion stop. |
| S2 (High): the PRD blocked the approved release into degraded production | Release modes, FR-6, FR-7, FR-8 and SM-5 add one Administrator-approved attempt for empty or degraded production. It lifts only the observed stop and the healthy-baseline check; a later stop still blocks. The first deployment uses this path. |
| A1 (Medium): CI could exercise an older module artifact | FR-4 and SM-3 require CI candidate evidence to show the candidate's own builds of every module artifact, identified by content, and refuse other builds and uncommitted changes. Baseline-only runs are labelled. |
| A2 (Medium): attachment was undefined when the owner finishes | FR-4 and SM-3: an accepted attachment holds the owner's environment up to a hold limit. A developer stop or owner cancellation ends attached runs and records them as withdrawn. |
| S3 (Medium): the addendum described obsolete operations-writer and deputy controls | Addendum: two named writers, review triggered only beyond them, deputy recovery without repository write, and G2 deputy qualification. |

## Product-owner choices made during this update

1. **Sync basis:** carry the current architecture, including user-accepted decisions from its third 2026-09-28 run, whose confirmation review is pending. The PRD lists those rules under Release scope and re-checks them if that review changes them.
2. **S1 and S2 adopted** as accepted in the architecture.
3. **Attachment:** an accepted attachment holds the owner's environment. Any failed local run, owner or attached, retains the environment for debugging. CI always cleans up.
4. **Architecture drift:** seven further accepted architecture decisions carried into the PRD at capability level. They cover:
   - approved incompatible-release recovery;
   - the post-disaster reduced-recovery state;
   - the SM-4 grant expiry;
   - the human admission group;
   - stop triggers;
   - tenant-lifecycle limits on checks;
   - shared-infrastructure change control.

   Six lower-impact decisions went into the addendum.
5. **Stuck production (reviewer-gate High):** degraded production also covers production that a recorded incident, probe failure or failed pre-update health check shows is no longer working. The one-attempt approval then ships a fix. The architecture must adopt this before the first applicable production attempt.
6. **Deputy containment:** the stated limitation is that nobody can revoke production admission while Administrator is unavailable. The deputy contains incidents through the promotion stop and documented recovery.

## Downstream alignment and qualification

The PRD's downstream table routes these items:
- rules the architecture has not yet adopted: the FR-4 attachment and CI-evidence rules, and the FR-8/FR-9 degraded-definition and admission rules;
- the module-candidate definition;
- the probe, identity-capture-lag and admission-check bounds, and the shared-infrastructure currency check;
- reduced-recovery operating rules;
- tenant-lifecycle review;
- spec synchronization.

`_bmad-output/specs/spec-platform/SPEC.md` predates this update, and the [consistency review](review-consistency.md) lists its expected follow-up. Final document status does not establish runtime qualification.

## Verification

| Check | Result | Evidence |
| --- | --- | --- |
| Source extraction | S1–S3 grounded in user-accepted architecture decisions; 13 further decisions identified; no architecture decision exists for A1 or A2 | [Recovery/access extract](extract-recovery-access.md), [CI/attachment/drift extract](extract-ci-attachment-drift.md) |
| Decision history | Every entry of this update is captured or set aside | [Decision audit](memlog-audit.md) |
| Validation and architecture reconciliation | 5/5 findings resolved; 14 of 16 architecture items carried, 2 partial items then completed | [Validation](reconcile-validation.md), [architecture](reconcile-architecture.md) |
| Reviewer gate | One High confirmed by three reviewers and resolved by product-owner choice 5; Medium and Low clarifications applied | [Rubric](review-rubric.md), [recovery/access](review-recovery-access.md), [adversarial](review-adversarial.md), [consistency](review-consistency.md) |
| Recheck | 42 resolved, 11 partial, 4 routed, 2 declined with reason, 0 unresolved; follow-up fixes applied for all partial items and the new issues found | [Recheck](review-recheck.md) |
| Editorial structure and prose | Structure then prose passes on both documents: 30 PRD and 43 addendum fixes, plus 8 accepted clarifying proposals; 6 cross-section moves declined; no requirement, number, ID or anchor changed | [PRD structure](review-structure-prd.md), [PRD prose](review-prose-prd.md), [addendum structure](review-structure-addendum.md), [addendum prose](review-prose-addendum.md) |

Final mechanical checks passed: all 26 requirement and success-measure IDs are defined once and every reference resolves; 35 local links and heading anchors are valid; both documents have final frontmatter dated 2026-09-28; there are no unresolved placeholders or trailing whitespace. Results are in `document-checks.json`. This is document verification; production qualification remains subject to the stated gates.
