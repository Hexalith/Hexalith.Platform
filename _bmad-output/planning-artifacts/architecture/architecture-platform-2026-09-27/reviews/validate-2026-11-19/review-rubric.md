# Platform architecture rubric review

19 November 2026 · Initiative spine · Platform commit `54d90be3`

**Verdict.** Revise the staging and promotion rules before teams use the spine to implement those gates. The capability map covers all 12 functional and three non-functional requirements, but the following instructions can produce different acceptance outcomes from the adopted policies. Three findings are high and one is medium. References below use paths relative to the repository root. “Spine” refers to `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`.

## High · R1 · A promotion evaluator can miss the adopted image vulnerability decision

The owned-work row asks Builds and Platform to “set the severity that blocks promotion.” The production preconditions cover provenance and staging evidence but omit a check of the release’s image scans and current exception state. An attested image with a valid staging pass and an unresolved High finding would satisfy those two checks. Story 5.2 already fixed the policy for both release modes. High, Critical and unresolved Unknown findings block by default. A qualifying exception addresses a specific occurrence, and every image scan has its own 24-hour clock. Stories 5.5 and 5.11 own implementation and admission.

**Disposition.** Align the owned-work row and the production admission checklist with the adopted version. Keep the implementation and qualification obligations visible. For a release with an unexcepted High finding, verify that both modes refuse admission even when staging and provenance pass.

**Evidence.** Spine lines 343–353 and 526. `docs/contracts/image-and-dependency-vulnerability-policy-v1.md` lines 3–10, 45–73, 75–99 and 107–110.

## High · R2 · A staging retry can inherit incompatible candidate writes

The staging reset rule names a later candidate and the in-place recovery row refers to the point cut at the start of “the unadopted candidate’s attempt.” If candidate A writes incompatible state and retries, a team following that summary could omit the reset or restore the second attempt’s point, which already contains A’s writes. The adopted policy also treats unproven readability as unreadable. It requires a verified reset before either a rerun of A or a later candidate, from A’s earliest pre-write point since the last verified reset. The incorrect starting state compromises the evidence for the rerun.

**Disposition.** Reference the policy’s reset selection and retention rules where the spine defines the staging reset and the restore point. Use a same-candidate retry with two recorded points as the acceptance example. Only the earlier pre-write point is eligible.

**Evidence.** Spine lines 315–316 and 319. `docs/contracts/staging-evidence-policy-v1.md` lines 230–249 and 409. The capability map directs FR-6 implementers to both the Staging gate and Staging reset rows at spine line 467.

## High · R3 · The staging sequence omits the named-recovery rehearsal

The staging row describes a baseline upgrade and rollback-set rehearsal for a production working baseline. It gives one alternate sequence, a fresh installation when no baseline exists. The production modes and preconditions require a named recovery plan, its checks, duration and a usable post-lock point where retained data lack valid compatibility evidence. They do not direct the staging executor to rehearse that named recovery. Following the staging summary can result in evidence for the wrong failure path. The adopted staging policy instead requires an approved retained-data candidate on this path to demonstrate the exact named restore, authority, verification and lost-window treatment and to omit automatic rollback preparation.

**Disposition.** Reference the policy’s four preparation branches from the staging rule and production precondition 9. For an approved incompatible release with retained data, a passing E2E run and a documented but unrehearsed restore must refuse admission.

**Evidence.** Spine lines 229–232, 242, 315, 317–318 and 353. `docs/contracts/staging-evidence-policy-v1.md` lines 313–335. `_bmad-output/planning-artifacts/epics.md` line 794.

## Medium · R4 · The attestation handoff falls after its first consumer

The publication workflow must verify every intake-pinned module image attestation. The owned-work table schedules EventStore and Memories adoption for first staging promotion. The approved story order requires it before first publication. A team relying on the handoff date would reach publication without a required input and block there.

**Disposition.** Move the module image attestation handoff to first publication and cite the publication story’s refusal case. Preserve the separate scan and promotion gates.

**Evidence.** Spine lines 240, 247 and 525. `_bmad-output/planning-artifacts/epics.md` lines 400, 799, 2295 and 2306.

## Remaining rubric checks

- **Scope and ownership.** The paradigm and roles identify the composition owner, technical and domain owners, production authority and recovery deputy (spine lines 36–46). AD-1 through AD-15 span composition, source and release identity, isolated CI, authorization, mutation boundaries, retention and disaster recovery. Every current PRD requirement group has a decision mapping at spine lines 459–475.
- **Operations and deferral.** The architecture decides or assigns hosting, deployment, private management, runtime isolation, backup, recovery, and monitoring. The supported Kubernetes upgrade and private Rancher qualification explicitly precede staging (lines 266–272). Open acceptance of the reference runner, measured monitoring bounds and replacement capacity have owners and evidence gates (lines 263–264 and 527–535). Their outstanding qualification is expected.
- **Inheritance and currentness.** This is the initiative-level spine. The custody slice is a separate artifact. The Stack table dates its seed to September (lines 382–398). Current package adoption is defined separately by AD-4 and the November qualification rules. This review compared repository records and policies; it makes no declaration of live component support or production readiness. The independent reality check covers the individual technology pins.
- **Mechanical check.** The BMAD spine linter returned zero findings at this revision. Its structural checks do not compare decision text with later adopted policies.
