# Hexalith Platform architecture validation

19 November 2026 · Initiative spine · Commit `54d90be3b661591048640d3585a8eab4aea0e534`

**Changes required. Six high findings and three medium findings.** The platform architecture needs changes before the affected chart, publication and production-promotion gates can be qualified. The clearest implementation conflict is ownership of Dapr Components. The application chart renders eight that the architecture assigns to the environment.

## High findings

### V1 · Application deployment would own environment-managed Dapr Components

**High · Correct implementation and requalify.** AD-8 gives the environment identity sole write access to every Dapr Component. The maintained application chart renders those objects, and the saved normal manifest contains eight. An application deployment using the prescribed permissions would be refused. Giving it those permissions would let application upgrades or rollback rewrite forward-only infrastructure bindings.

**Action.** Keep AD-8 as the ownership rule. Move Component rendering to the environment layer and qualify each application chart mode with the intended deployment permissions.

[Spine, AD-8 L149](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L149) · [Release tiers L279](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L279) · [Application chart L7](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/eng/helm-qualification/chart/templates/dapr.yaml#L7) · [Recorded normal render L1586](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/implementation-artifacts/evidence/4-5/normal.yaml#L1586) · [Chart ownership description L45](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/eng/helm-qualification/README.md#L45) · [Chart test requiring Components L351](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/eng/helm-qualification/test_chart.py#L351)

### V2 · Production promotion omits the adopted vulnerability gate

**High · Align with adopted policy.** The spine’s production checklist can pass without scan evidence. Its handoff asks teams to choose a blocking severity. Platform already adopted a policy that blocks High, Critical and unresolved Unknown findings in both promotion modes, subject to a valid scoped exception. An otherwise passing release with an unexcepted High finding would receive opposite verdicts.

**Action.** Reference policy v1 in the common production preconditions. Give Story 5.5 responsibility for producing scan evidence and Story 5.11 responsibility for checking it at admission, including evidence age and exception state.

[Production checklist L343](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L343) · [Owned work L526](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L526) · [Adopted thresholds L45](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/image-and-dependency-vulnerability-policy-v1.md#L45) · [Exception and execution responsibilities L68](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/image-and-dependency-vulnerability-policy-v1.md#L68) · [Scan evidence age L94](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/image-and-dependency-vulnerability-policy-v1.md#L94)

### V3 · An incompatible release can reach approval without the required restore rehearsal

**High · Align with adopted policy.** The spine tells staging to prepare a rollback set whenever a production baseline exists. Its approved retained-data path asks the operator to name a recovery procedure. For that path, the adopted staging policy requires evidence of a rehearsed named restore and prohibits preparing an automatic rollback set. A written plan alone cannot prove recovery.

**Action.** Make staging select the applicable branch before qualification. For an approved retained-data release without valid compatibility evidence, require the full E2E pass and qualified restore, including authority, retained inputs, verification and treatment of post-cut changes. Retain the separate production post-lock recovery-point requirement.

[Staging gate L315](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L315) · [Approved release path L317](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L317) · [Production precondition 9 L353](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L353) · [Preparation branches L319](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/staging-evidence-policy-v1.md#L319) · [Story 5.9 obligation L376](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/staging-evidence-policy-v1.md#L376)

### V4 · A staging retry can start from data the candidate already changed

**High · Align with adopted policy.** The spine describes a reset before a later candidate and identifies a point at the start of the unadopted candidate’s attempt. The policy also requires a verified reset before retrying the same candidate after unreadable or unproven writes. A later retry’s snapshot may already contain those writes.

**Action.** Apply the reset to both a later candidate and a retry of the same one. Retain and restore the earliest pre-write point for that candidate since the last verified reset. Require successful verification before the queued attempt starts.

[Staging reset L316](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L316) · [In-place recovery L319](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L319) · [Reset and point selection L230](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/staging-evidence-policy-v1.md#L230) · [Same-candidate example L409](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/staging-evidence-policy-v1.md#L409)

### V5 · Staging can supersede a release already admitted to production

**High · Resolve an open design decision.** Production checks that candidate A is pending while it holds its own lock. Before A becomes the working baseline, staging can start B and mark A superseded. If A then succeeds, the gate also has to close A as completed. The two executors have no agreed transition for this interval. The Story 5.1 review already recorded this open decision.

**Action.** Have the owners of Stories 5.10 and 5.11 agree when admission reserves a decision and which signed transition wins. Exercise B starting before admission, after admission but before completion, and after a failed production attempt. Follow the policy versioning rule for any change to adopted semantics.

[Staging adoption L316](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L316) · [Per-environment locks L320](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L320) · [Admission check L58](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/staging-evidence-policy-v1.md#L58) · [Terminal states L152](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/staging-evidence-policy-v1.md#L152) · [Supersession rule L249](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/staging-evidence-policy-v1.md#L249) · [Existing deferral L138](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/implementation-artifacts/spec-5-1-decide-the-staging-evidence-policies.md#L138)

### V6 · Module image attestations are scheduled after their first required use

**High · Correct the prerequisite.** The handoff makes owner image attestation due at first staging promotion. The publication workflow must already verify each module image’s attestation at intake. An unattested module therefore blocks publication even if its team meets the date given in the handoff.

**Action.** Move the owner attestation prerequisite to first admission for publication. Keep the check that refuses a missing or mismatched attestation before the release record is written.

[Publication responsibilities L247](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L247) · [Owner handoff L525](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L525) · [Publication refusal criterion L2303](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/epics.md#L2303)

## Medium findings

### V7 · Chart qualification asks for evidence that exists only after publication

**Medium · Correct the qualification sequence.** The Aspire-to-Helm handoff bundles chart ownership, OCI attestation and retained-package rollback into one proof. Story 4.5 qualifies rendering and ownership before a published package exists. The epic assigns OCI evidence to publication and off-site package recovery to retention. Making all three part of the early proof creates a dependency on later work.

**Action.** Record the maintained-chart fallback. Split the handoff into chart qualification, verification of the published OCI attestation, and recovery of the retained package. Keep all three obligations at their owning gates.

[Chart fallback L77](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L77) · [Helm qualification handoff L519](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L519) · [Epic sequence L787](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/epics.md#L787) · [Story 4.5 exclusions L2232](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/epics.md#L2232) · [Measured fallback L9](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/eng/helm-qualification/README.md#L9) · [Proof boundary L153](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/eng/helm-qualification/README.md#L153) · [Recorded qualification result L4](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/implementation-artifacts/evidence/4-5/qualification.json#L4)

### V8 · Dependency-only releases cannot supply a newly introduced command

**Medium · Resolve a requirement conflict.** The PRD asks the rollback rehearsal to exercise a candidate-new idempotent command where applicable. The spine, staging policy and Story 5.8 require one unconditionally. A dependency-only release can preserve the entire command catalog and still need a compatibility rehearsal.

**Action.** Decide what evidence proves that no command was added, and which existing command should demonstrate baseline readability in that case. Align NFR-1, Story 5.8 and the versioned staging contract before applying the decision.

[Spine rehearsal L315](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L315) · [PRD NFR-1 L349](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md#L349) · [Spec acceptance criterion L174](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/specs/spec-platform/acceptance-criteria.md#L174) · [Policy rehearsal L321](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/docs/contracts/staging-evidence-policy-v1.md#L321) · [Story 5.8 L3058](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/epics.md#L3058)

### V9 · The stack table labels the September seed as current

**Medium · Correct the baseline references.** The stack table dates its source to September 27 but labels .NET SDK 10.0.401 and Aspire 13.5.4 as current. The checkout pins .NET SDK 11.0.100 and Aspire 17.0.0. The local tool manifest pins 6.3.2 for preparation. A developer using the table can select a different build environment from the one CI expects.

**Action.** Label the seed as historical and point readers to the current SDK, AppHost and tool manifests. Keep tool pinning, runner qualification and production acceptance as distinct claims.

[Stack table L384](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L384) · [Workspace SDK L3](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/global.json#L3) · [AppHost SDK L2](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/apphost.cs#L2) · [CLI requirement L299](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/README.md#L299) · [Prepared tool pin L6](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/.config/dotnet-tools.json#L6)

## Candidate qualification at this revision

The source-selection step used by tuple CI rejected this revision because the committed EventStore source binding differs from its submodule commit. Story 2.14 already records five dependency revisions requiring reconciliation and qualification before publishing another candidate. This is a separate release blocker and is excluded from the architecture finding count.

[CI source-selection step L42](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/.github/workflows/tuple-ci.yml#L42) · [Exact gitlink comparison L63](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/eng/current_tuple_source.py#L63) · [Story 2.14 audit L1717](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/epics.md#L1717)

## What passed

The November CI handoff distinguishes the accepted Story 2.8 lifecycle runs from the fresh run required for each merge or release decision. The spine also assigns outstanding Story 2.10 runner qualification to explicit owners. Those distinctions are consistent with candidate policy v1. [Qualification sequence L258](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L258) · [Policy handoff L509](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L509)

- BMAD structural lint passed with zero findings across AD-1 through AD-15. The capability map covers FR-1 through FR-12 and NFR-1 through NFR-3.
- All 26 local Markdown link occurrences in the spine resolved, including five heading fragments. All 18 frontmatter source paths exist. The document contains four Mermaid diagrams.
- Four independent reviews covered the architecture rubric, implementation and technology, cross-team compatibility, and release and recovery. Overlapping observations are counted once. High findings can change ownership or a publication, promotion or recovery decision. Medium findings affect qualification sequencing, applicability or reproducibility.
- This validation used the committed checkout and retained evidence. It ran the structural linter and the local CI source-selection check. Live deployments, recovery drills and the full integration suite were outside this run.

## Review evidence

- [Architecture rubric and PRD coverage](review-rubric.md)
- [Implementation and technology checks](review-reality.md)
- [Cross-team compatibility](review-adversarial.md)
- [Release and recovery](review-recovery.md)

[Spine at the reviewed revision](https://github.com/Hexalith/Hexalith.Platform/blob/54d90be3b661591048640d3585a8eab4aea0e534/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#L1) · [HTML report](validation-report.html)
