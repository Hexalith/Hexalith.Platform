# Sprint change proposal for the November 22 readiness findings

22 November 2026 · Owner approved and planning edits applied · Reviewed Platform revision `b0124db4`

## 1. Issue summary

The [readiness gate](implementation-readiness.md#november-22-2026-readiness-gate) found three conflicts in the production handoff. Story 6.7 places production foundation objects in the environment-layer apply, while the architecture assigns them to shared infrastructure and Story 6.22 requires a complete recovery point. Stories 5.7, 5.11 and 6.18 disagree on fresh-install proof for an approved repair. Story 8.19 requires candidate-specific named recovery at G2 even when no candidate needs it.

## 2. Impact analysis

| Area | Delivery impact |
| --- | --- |
| Epics 4 and 6 | Carry the staging foundation’s ownership sequence into production. Commission the production executor and off-cluster attempt store before first-production foundation apply so both environment locks can be used. Story 6.7 owns the foundation handoff and forward-only environment apply. |
| Epics 5, 6 and 9 | The same exact candidate must produce fresh-install proof for the approved empty or degraded path. When production retains application data, the applicable compatibility or named recovery rehearsal must also pass. Story 5.11 evaluates both under the existing admission rules. Reuse these cases for SM-5. |
| Epics 8, 9 and 10 | Keep the G2 recovery drill and standing controls. Bind the named restore to an applicable candidate and baseline before admission. Epic 9 and Epic 10 repeat the relevant recovery paths under their existing SM-5 and SM-6 requirements. |
| PRD, architecture, spec and UX | The PRD already states the release requirements. The architecture and spec need the first-production foundation sequence. The affected workflows produce operator evidence in existing attempt records. |

## 3. Recommended approach

Amend the existing stories and their source contracts together. The 12 epic and 151 story IDs remain valid. Rolling back the staging foundation would discard the recorded ownership sequence. Reducing MVP scope would leave first deployment and recovery without an agreed implementation path.

The documentation changes are small and the scope is moderate across three delivery handoffs. Implementation and live verification remain in the existing stories. The main operational risk is first-production work affecting a functioning staging environment. Limit the bootstrap to exact, reviewed production foundation objects and require both locks, current native recovery evidence and staging postchecks. The work requires an accepted staging foundation, the production executor and attempt store, and reviewed production objects and authority before scheduling a live change window.

## 4. Detailed changes

### A. Sequence the first production foundation

**Before.** Story 6.7 says production gets its namespaces and StorageClass when its environment-layer identity applies the staging definitions. The architecture assigns those resources to a shared-infrastructure workflow whose ordinary path requires a complete recovery point. Its pre-environment urgent maintenance procedure does not specify the production foundation after staging is operating. [Story 6.7](epics.md#story-67-build-the-production-environment-layer) · [Story 6.22](epics.md#story-622-guard-shared-infrastructure-changes-with-one-controlled-procedure) · [Release tiers](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#release-tiers)

**Proposed.** In the architecture release tiers, the spec’s environment-layer sequencing and Epic 6, put Stories 6.8 and 6.9 ahead of the first production foundation step in 6.7. With the staging foundation accepted, both locks held and one attempt record per environment, Administrator authorizes the exact first-production foundation inventory and names its owner and recovery procedure. Use current verified native recovery evidence for existing infrastructure. The shared-infrastructure identity creates the dedicated production namespaces, StorageClass and PriorityClass. Administrator establishes the reviewed environment-identity grants before the environment-layer writer applies the forward-only resources. Prove the environment and application identities cannot perform the shared writes. [Recorded staging ownership sequence](../../eng/staging-foundation/CONTINUATION-PACKAGE.md#ordered-owners-and-acceptance-boundaries)

In Story 6.22, constrain the pre-recovery-point path to first production foundation creation before any production workload data or working baseline exists. Scope the inventory to new production objects and their explicit grants. Refuse changes to existing staging or shared service objects, existing access grants and cluster defaults. Require staging smoke and isolation evidence, with user ingress closed in production. Record partial effects and refuse unreviewed resumption or drift. Ordinary shared-infrastructure mutation continues to require a complete recovery point under both locks. The G1 application deployment still follows Story 6.23.

**Accept when** the correct owner can apply each stage in an isolated rehearsal, either missing lock prevents mutation, staging remains working, and the limited bootstrap path refuses an existing-object change, an out-of-inventory write, or reuse after production has workload data or a working baseline.

### B. Produce and consume fresh-install proof on the same release

**Before.** Story 5.7 phrases its execution trigger as no working baseline. Story 6.18 limits fresh-install proof to the first installation without retained data. Story 5.11 and the PRD already require the proof for the approved empty or degraded exception. [Story 5.7](epics.md#story-57-rehearse-a-fresh-install-when-production-has-no-baseline) · [Story 6.18](epics.md#story-618-run-administrator-approved-attempts-into-empty-or-degraded-production) · [PRD staging validation](prds/prd-platform-2026-09-27/prd.md#staging-validation-and-production-promotion)

**Proposed.** Keep Story 5.7’s ID and title for tracking. Extend its acceptance criteria to require a clean staging install of the exact candidate for each approved empty or degraded exception, even if degraded production has a recorded baseline. Run the full required E2E suite and retain the fresh-install outcome under the exact release and evidence bindings. Keep the clean-install state distinct from the baseline-based rehearsal and apply existing serialized staging and reset rules. In Story 6.18, require 5.7 evidence on each approved empty or degraded path via 5.11 and 6.14. For retained data require valid compatibility, or, once available after G1, a separately qualified named restore and a usable production recovery point cut after the lock. A no-data first installation needs the fresh-install branch and the common admission checks.

**Accept when** a degraded case with retained data and no fresh proof refuses even with compatibility evidence, fresh proof alone refuses when data remain, and the complete fresh-plus-compatible case can pass with every other gate satisfied. The first empty installation uses fresh proof and the common gates. The named path also requires the staging restore rehearsal and the post-lock production recovery point. [Story 5.11](epics.md#story-511-evaluate-staging-evidence-for-promotion) · [Platform spec staging order](../specs/spec-platform/sequencing.md#other-ordering-rules)

### C. Apply candidate-specific named recovery at admission

**Before.** Story 8.19 lists all of Story 8.16 as mandatory at G2. Story 8.16 includes both in-place recovery capability and qualification tied to an incompatible candidate. The spec’s G2 list requires a passed disaster-recovery drill and standing recovery controls. It assigns candidate-dependent restore evidence to the applicable retained-data attempt. [Story 8.16](epics.md#story-816-qualify-in-place-data-restore-and-named-retained-data-recovery) · [Story 8.19](epics.md#story-819-open-production-to-users-g2) · [Spec G2 gate](../specs/spec-platform/sequencing.md#g2--users-admitted-ingress-open)

**Proposed.** Remove the bundled 8.16 criterion from 8.19 and state which evidence is checked at release admission. In 5.9 and 8.16, make the exact staged rehearsal an input to candidate qualification and state that the reusable in-place capability must be qualified after G1 before admitting any attempt that relies on it. Retain the independent 8.17 drill and every other G2 prerequisite in 8.19. Stories 5.11 and 6.14 continue to refuse an applicable candidate with missing, stale or wrong-baseline evidence.

**Accept when** a healthy working release can reach G2 with all documented G2 conditions and no candidate awaiting named recovery. The same system must refuse an applicable retained-data approval before mutation if the qualified capability or matching candidate evidence is missing. [Production entry and owned work](../specs/spec-platform/sequencing.md)

## 5. Handoff and completion

| Owner | Handoff and evidence |
| --- | --- |
| Platform Architect and Product Owner | Approve the bounded bootstrap design and gate wording. Reconcile the architecture spine, sequencing spec and epic criteria in one change. |
| Platform operations and Administrator | Implement the reviewed foundation stages in 6.7 and the guard in 6.22. Demonstrate both-lock sequencing, correct writer permissions, preservation of staging and refusal cases before accepting the foundation. |
| Platform and Builds release maintainers | Implement the fresh-install evidence branch and both-mode consumption at the existing staging and production gates. Record the applicable cases in the release evidence and SM-5 handoff. |
| Platform recovery maintainers and Administrator | Prove the reusable in-place mechanism, candidate-specific restore admission and independent G2 drill in their existing stories. |

The owner selected **Approve and apply** on 22 November 2026. The planning handoff is recorded in [the epic breakdown](epics.md), [the architecture spine](architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#release-tiers), [the sequencing spec](../specs/spec-platform/sequencing.md) and [the Epic 5 implementation context](../implementation-artifacts/epic-5-context.md). The approval covered these planning edits. The delivery owners retain the live implementation and acceptance work above.
