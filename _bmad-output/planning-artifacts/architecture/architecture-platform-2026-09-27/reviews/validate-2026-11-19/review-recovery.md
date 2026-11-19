# Release and recovery review

Reviewed the initiative architecture at Platform commit `54d90be3` on 19 November 2026. The release gate needs revision before Platform and Builds can implement it independently. Three high-severity findings affect admission or staging restore and one medium finding affects ordinary compatible releases.

In the citations below, “spine” refers to the initiative’s `ARCHITECTURE-SPINE.md`. “PRD” refers to `_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md`. Contract paths are relative to the repository root.

## High — The spine leaves an adopted scan threshold open

The spine tells Builds and Platform to set the threshold before first staging promotion. Its numbered production preconditions omit the vulnerability decision. Platform has already adopted a specific Builds-owned, immutable policy for both release modes. It blocks High and Critical findings including unfixed findings, and treats an unresolved Unknown as blocking, subject to the contract’s exact-occurrence exception rules. The module supplies a rationale and Administrator approves the exception. An approved release alone grants no exception. A promotion evaluator built from the spine could admit a release the Builds evaluator refuses. Evidence: spine lines 343–353 and 526; `docs/contracts/image-and-dependency-vulnerability-policy-v1.md` lines 3–10, 24–29, 45–54, 57–73 and 75–82.

**Disposition.** Reference the adopted policy in production admission and change the owned-work entry to implementation and qualification. Story 5.5 owns scanner and evidence enforcement. Adoption itself supplies no scan result.

## High — The staging gate gives the wrong rehearsal for an approved incompatible release

Consider a release over a known baseline with retained business data, a breaking schema change and an approved named data-recovery plan. The spine’s staging row prescribes an AD-15 rollback-set rehearsal when the baseline exists. The production preconditions permit the approved alternative to name a recovery, while the separate release-modes row specifies its checks and duration. The fixed staging contract expressly requires staging to rehearse the named data recovery with its authority, artifacts, procedure and verification and to prepare no automatic rollback set for this branch. An executor following the unconditional staging row can prepare the wrong artifact. An evaluator following the production alternative alone can accept a paper plan. Evidence: spine lines 229–232, 315–319, 348 and 353; `docs/contracts/staging-evidence-policy-v1.md` lines 313–336 and 374–378; PRD lines 181–183 and 240–243.

**Disposition.** Point the staging gate and production precondition 9 at the policy’s preparation table. Require qualifying evidence for the selected branch and distinguish rehearsal in staging from cutting the actual usable recovery point under the production lock.

## High — The staging reset rule omits retries of the same candidate

The spine describes a reset ahead of a later candidate when the baseline cannot read the earlier candidate’s writes. It identifies the recovery point as the one at the start of the unadopted candidate’s attempt. The staging policy also requires a reset ahead of a retry of the same candidate after breaking or unproven writes. It treats an absent baseline or unproven readability as unreadable and selects the earliest point for the candidate since the last verified reset. With two attempts from the same revision, the second attempt’s recovery point can already contain the first attempt’s writes. Rerunning from that state can invalidate both the evidence and the next reset. The qualification entry links the policy, but the operational rule would still lead a scheduler to miss those cases. Evidence: spine lines 315–319, 334 and 524; `docs/contracts/staging-evidence-policy-v1.md` lines 230–249, 296–303 and 373–377.

**Disposition.** Bind the staging reset trigger and recovery-point selector to policy v1 at the point of use. Qualify a same-candidate retry and a missing-readability proof with two distinct attempt identities and the same pre-write recovery point.

## Medium — A compatible candidate with no new idempotent command lacks a rehearsal case

NFR-1 requires exercise of a newly introduced idempotent command “where applicable.” Both the spine and policy require at least one candidate-new command for the baseline-compatible branch. The epic’s acceptance criterion repeats it. A UI-only patch can generate normal state and events and demonstrate that the baseline reads them, yet introduce no new command type. One team can mark that subcheck inapplicable and another can refuse the same compatible release. Evidence: PRD line 349; spine line 315; `docs/contracts/staging-evidence-policy-v1.md` lines 317–325; `_bmad-output/planning-artifacts/epics.md` lines 3056–3058.

**Disposition.** Ask the policy owners whether “new” means a newly introduced command type or a new command invocation. Define evidence for a release introducing no such type and retain the baseline-readability proof. If this changes the fixed v1 rule, publish and adopt a successor.

## Checks that agreed

- Publication binds the immutable package and images to the release record. Promotion and rollback select retained digests. Release records, environment-current values, and attempt records distinguish what transfers across environments from what each executor resolves locally. Spine lines 81–87 and 241; PRD lines 176–182.
- FR-8 application rollback retains data and current security authority and gets one recovery attempt. Disaster recovery has a separate path. Its replacement-capacity issuer, fence and credential reissue differ from an in-place restore, which retains live identity and key custody. Spine lines 94–97, 198–201, 319, 330 and 355–365; PRD lines 215–220 and 345–351; `docs/contracts/recovery-hook-v1.md` lines 132–157.
- The one-hour RPO and four-hour RTO are objectives. The RTO applies to outages beginning within declared coverage. Prepared replacement capacity and an isolated timed drill are G2 conditions, with a reduced-recovery posture after actual failover. The documents give these activities owners and gates. This review inspected policy and did not exercise infrastructure. Spine lines 332–339 and 533; PRD lines 283–286 and 353–365.
