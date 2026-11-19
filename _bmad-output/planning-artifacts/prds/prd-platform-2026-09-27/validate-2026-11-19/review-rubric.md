# PRD Quality Review — Hexalith Platform

## Overall verdict

The PRD gives developers observable acceptance tests for all seven modules and gives operators explicit release and recovery gates. Two passages can lead to opposite operational decisions about when staging may start and which production approvals to refuse. Several decisions still described as open already have published policies that downstream teams can use.

## Decision-readiness — adequate

The G1–G3 table distinguishes closed deployment, user access and automatic promotion. FR-8 names the people who can restore service, declare a stop and clear it. The addendum nevertheless presents a withdrawn staging exception in the present tense.

### Findings

- **[high]** The addendum still permits the withdrawn Kubernetes staging exception (§ addendum.md:408 and 412, prd.md:414, October 1 sprint change:269, ARCHITECTURE-SPINE.md:536) — “permits the recorded pre-production Kubernetes support exception only until G1” gives an operator grounds to start staging on Kubernetes 1.34 after its October 27 end of life. The approved correction explicitly makes a delayed upgrade block staging. *Fix:* Mark the old exception superseded and state the approved order in the risk entry itself. Retire KubeSphere, complete the supported Kubernetes upgrade, qualify Rancher, then enable staging.

## Substance over theater — strong

FR-2 tests whether a breakpoint changes the running module. FR-4 tests actual loaded artifacts, bounded startup and resource ownership. NFR-2 defines the start and end of the recovery clock and explains who covers an outage that begins during a shift. These consequences let a reviewer reject misleading success claims.

## Strategic coherence — strong

The vision assigns shared hosting to Platform and business behavior to module owners. The Parties example in FR-3 shows what that boundary means in a developer's workspace. FR-6 carries it into release decisions by requiring each enrolled module's declared checks. SM-C1, SM-C4 and SM-C5 guard against passing a release with incomplete evidence, deleting another run's resources or hiding a slower startup with a longer timeout.

## Done-ness clarity — adequate

Every FR has testable consequences. FR-7 supplies a ten-minute rollout deadline and a five-minute verification window with concrete retry conditions. One SM-5 acceptance instruction is broad enough to reverse a valid result for an otherwise permitted approval.

### Findings

- **[high]** SM-5 can reject a valid approval on a healthy baseline (§ prd.md:50, 181, 236, 385 and 394) — “refusal of approval when production is neither empty nor degraded” can be copied into a test for every Administrator-approved release. FR-6 explicitly permits pre-G3 releases and releases requiring a named recovery on a healthy baseline. SM-C1 states the narrower rule as “zero degraded-path approvals.” *Fix:* Scope the refusal explicitly to an attempt invoking the empty/degraded exception on a healthy baseline. Pair it with an allowed pre-G3 or named-recovery attempt on that same baseline when its common gates pass.

## Scope honesty — strong

The seven-module MVP at prd.md:40–48 and the exclusions at lines 62–68 set clear boundaries. FR-9 names which acknowledged changes may be lost after a restore and gives Memories erasures stronger protection. NFR-2 conditions the four-hour recovery target on response coverage and replacement capacity. The September repository observations in addendum.md:10–24 are expressly dated.

## Downstream usability — adequate

The stable identifiers, glossary and named evidence owners support extraction into stories. The handoff table should identify which decisions are now published so that a team can tell policy ownership from implementation and qualification work.

### Findings

- **[medium]** Published CI, staging and infrastructure policies still appear as decisions to make (§ prd.md:161, 179, 407–411, 415–416, docs/contracts/candidate-and-attachment-policy-v1.md:244–246, staging-evidence-policy-v1.md:74–78 and 100–137, shared-infrastructure-currency-policy-v1.md:113–134) — The handoff still says “Define the module candidate revision” and routes evidence freshness, reruns and the currency check's effect on approvals back to owners. Published policies already prescribe the committed PR merge result, fresh evidence per decision, 1,800-second default and 14,400-second maximum attachment holds, a 24-hour staging limit and an approval naming each failing infrastructure component. Reopening these choices could give two consumers different admission criteria. *Fix:* Link the three policy versions from the relevant rows. Mark their decisions published and retain the separate owner and deadline for adoption, enforcement and qualification.
- **[low]** The revision date predates an applied correction (§ prd.md:5, 148 and 161, November 18 sprint change:7 and 118–133) — The frontmatter says November 8. The file contains both approved November 18 edits, so a reader selecting a requirements snapshot by date could miss them. *Fix:* Set the revision date to November 18 or the date of the next accepted edit, and identify the November 18 change in the revision summary.

## Shape fit — strong

Separate capability scenarios for developers, automated workflows, Administrator and the recovery deputy suit this internal platform. The PRD explains at line 36 why module business journeys stay with the modules. FR-4, FR-8 and FR-9 give role-specific acceptance scenarios where story writers need them, while the addendum preserves alternatives and their operating costs.

## Mechanical notes

- All 26 FR, NFR, SM and counter-metric IDs have unique, contiguous definitions within their respective series. Every reference to one of these IDs in either reviewed document resolves to a definition in the PRD.
- All 25 local Markdown link occurrences in the PRD and addendum resolve, including their explicit fragments. External URLs were outside this check.
- Neither document contains an inline assumption or PM-note placeholder. The downstream table records owners and revisit conditions. Capability scenarios are the PRD's deliberate substitute for named user journeys.
