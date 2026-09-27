# PRD Quality Review — Hexalith Platform (prd-platform-2026-09-27)

## Overall verdict
The requirement shape suits a single-operator internal platform, and this is a strong capability spec. It states decisions as decisions and backs them with numeric thresholds. Missing evidence fails closed throughout FR-6, FR-7 and FR-9. The non-goals do real work, and one thesis runs through every feature: Platform orchestrates, and modules own behavior and evidence. The risk sits in two places. First, the recovery response model: the four-hour RTO counts detection and operator response, yet the PRD requires no outage detection, disclaims round-the-clock response, and adds a recovery deputy who is bound by nothing. Second, the post-finalization edits were made locally and not propagated. FR-2 now says McpCli is "declared directly" in the Platform workspace, while the addendum and the repository say it is a sibling repo. The agent-eligibility rule never reaches the McpCli glossary entry, SM-4, or the module-developer handoff.

## Decision-readiness — adequate
Most decisions are stated plainly, with what was given up:
- The testing model uses Option 3 with no generic mocking (§ MVP non-goals; addendum § Testing options).
- Rollback uses readiness plus module smoke checks, and traffic-metric triggers are explicitly deferred (§ MVP non-goals, § FR-7).
- Disaster recovery uses backups rather than a standby cluster. The PRD accepts the cost openly: "this accepts possible loss of up to one hour of committed data" (§ NFR-2).

The addendum keeps the rejected alternatives with real costs, not straw men. The PRD has no rhetorical open questions. Unresolved work is in a downstream table with owners and revisit conditions (§ Downstream decisions).

One tension has been smoothed over rather than decided: how quickly a human responds to a production failure. Three statements sit side by side without being reconciled. NFR-2's RTO runs from "service outage to verified restoration, including detection, operator response." The next paragraph says the targets do "not establish … round-the-clock response guarantee." FR-8 lets automatic promotion leave production in a "failed or unverified recovery" state that waits on Administrator, with no time bound at all.

### Findings
- **[high]** RTO counts detection and response, but neither is required or bounded (§ NFR-2 table and following paragraph; § FR-9; § SM-6)
  - The four-hour RTO clock starts at the outage and includes detection. No FR requires production outage detection or alerting. FR-8 explicitly puts post-verification failures "outside this automatic deployment-recovery policy".
  - Today the only detection path is incidental. FR-9's alert fires when the newest usable recovery point is "older than one hour", so detection could use up to a quarter of the RTO before anyone is notified. That notification goes through GitHub, which is not a paging channel.
  - The PRD also disclaims round-the-clock response. So it is undecided whether the four-hour target applies at 03:00 on a Sunday. That choice decides whether the team needs on-call coverage.
  - SM-6's scheduled exercises cannot test this: FR-9 folds "operator-response … assumptions" into the assessment rather than measuring them.
  - *Fix:* Decide the coverage model. One option: "RTO applies to outages starting within declared coverage hours; outside them, the clock starts at the next coverage window." Another: the RTO holds around the clock and the deputy gives the second responder. Then add a production-availability alert requirement, or put "outage detection" into the downstream table with an owner.
- **[medium]** A failed or unverified automatic recovery has no response bound, and promotion has no availability window (§ FR-8 consequences 3–4; § Vision)
  - Automatic promotion can fire whenever staging E2E passes. If the single rollback attempt fails, production stays down until Administrator intervenes.
  - That case is not a "major production server or storage failure requiring backup restoration". NFR-2 therefore does not apply, and no target covers it.
  - "Further automatic promotions stop pending intervention" does not say who resumes promotion or what evidence they need.
  - *Fix:* Either restrict automatic promotion to times when the recovery owner or deputy is available, or accept an unbounded failed-recovery outage explicitly as a trade-off. Also name the actor and the condition for resuming promotions.

## Substance over theater — strong
There is no NFR theater. NFR-1 to NFR-3 are product-specific, and each has a verification method: previous-version schema and event compatibility, RPO/RTO with a measurement column, and negative isolation tests using staging credentials. The Vision could not be swapped into another PRD. It names the ownership split, active-checkout debugging, explicit production-user declaration, and the E2E-gated automatic promotion. The addendum's option comparisons exist because the coaching surfaced real choices. There is no innovation section.

The one piece of furniture came from the post-finalization edit.

### Findings
- **[medium]** The recovery deputy is a role that binds nothing (§ Target users line 30; § Glossary "Recovery deputy"; addendum § Disaster recovery, "Fits an internal MVP with one named recovery owner" and "a single recovery owner do not establish round-the-clock response coverage")
  - The deputy was added for "recovery and … custody of recovery keys". Yet no FR, NFR, SM, or downstream row mentions the deputy.
  - FR-8 and FR-9 notify only Administrator. FR-9's "required access/decryption material remain accessible independently" does not require a second custodian.
  - "A named person" is never named, and no row assigns naming the deputy before production use.
  - The addendum still reasons from a single recovery owner.
  - This matters because single-person key custody is exactly the disaster-recovery single point of failure the deputy was meant to remove.
  - *Fix:*
    - Add to FR-9: recovery access and decryption material are held by at least two custodians, namely Administrator and the recovery deputy.
    - Decide whether FR-8 and FR-9 notifications also reach the deputy.
    - Add "designate recovery deputy" to the Administrator row of the downstream table.
    - Update the two addendum sentences.

## Strategic coherence — adequate
The thesis is real and runs through the whole document: *Platform owns shared hosting, while modules retain their domain behavior* (§ Vision). The same split shows up everywhere modules own something:
- server lists (FR-3)
- readiness checks (FR-4)
- critical flows (FR-6)
- smoke suites (FR-7)
- authoritative-state inventories (FR-9)
- operation sets and their eligibility (FR-12)

The counter-metrics pair well with their SMs (SM-C1 to SM-C5), and SM-C5 honestly declines to invent a setup-time target. The MVP is platform-shaped, and its scope logic matches: seven modules proving the model, "not a permanent exclusion of the others".

What's missing is the *why*.

### Findings
- **[medium]** The brief's problem statement was dropped, and nothing measures whether the thesis worked (§ Vision; § Success measures)
  - The brief has a Problem section: the only root composition is an optional Works preview, and full-system composition and reusable module startup are missing. The PRD opens directly with the solution. Read on its own, it never says what is broken today or why the shared-orchestration bet beats per-module hosting.
  - All six SMs are acceptance demonstrations. None measures adoption, for example that each MVP module's local, test and CI path actually runs through Platform instead of its own wiring. Adoption is what would validate "one common solution".
  - *Fix:* Add a two- to three-sentence problem statement, taken from the brief, before the Vision. Add an adoption-shaped SM, or an explicit statement that bespoke per-module hosting is retained or retired for MVP modules.

## Done-ness clarity — adequate
FR-4 and FR-6 to FR-9 are unusually strong:
- Every trigger has a number: 10 minutes, a five-minute window, 60 continuous seconds, two failures 30 seconds apart, 30-minute cadence, 7- and 30-day retention, one-hour freshness.
- Absent evidence is treated as failure: "an absent test result is not a pass"; "discovering no checks is not a passing result".
- Ownership edges are testable: "Cleanup affects only the resources belonging to that test environment".

Story creation can work directly from these. The weak spots are a few undefined nouns and one gate condition the NFRs require but the FRs never enforce.

### Findings
- **[medium]** "Supported interfaces" is undefined, so FR-10's first consequence cannot be tested (§ FR-10 "accessible in both environments through its supported interfaces"; § FR-11 "any supported interface"; § SM-4 "expose the supported MVP interfaces")
  - Nothing lists which interfaces count per module: HTTP APIs through ingress, module UIs, EventStore endpoints, McpCli.
  - An engineer cannot tell when FR-10 is done, and the isolation checks in FR-11 and NFR-3 have no enumerated surface to cover.
  - *Fix:* Define "supported interface" in the Glossary as the module-declared external surfaces. Add module-declared interfaces to the module-developer row of the downstream table.
- **[medium]** NFR-1's compatibility check is not a condition of the FR-6 gate (§ NFR-1 "Compatibility must be verified before automatic promotion"; § FR-6; § SM-C1)
  - FR-6 lists only E2E evidence as gating, and every missing-E2E case fails closed.
  - Previous-version schema and event compatibility gets no gate consequence and no fail-closed rule. SM-C1 counts only E2E bypasses.
  - A release with missing compatibility evidence could satisfy FR-6 as written.
  - *Fix:* Add an FR-6 consequence: "Missing or failed previous-version compatibility evidence blocks automatic promotion; the release requires the separately planned procedure in NFR-1." Extend SM-C1 to cover compatibility evidence.
- **[medium]** Cross-module data integrity after restore has no definition and no owner (§ FR-9 "Restoration verifies … data integrity across modules"; addendum "independently timed database snapshots do not by themselves prove a recoverable cross-module state")
  - Several stores are backed up independently every 30 minutes. The PRD requires cross-module consistency to be verified but never says what consistent means (a coordinated point, or an accepted skew with reconciliation), nor who provides the check.
  - The downstream table assigns "authoritative-state inventories" to modules, but not consistency checks.
  - *Fix:* Require each module to supply a post-restore integrity and consistency check against its EventStore history. State whether recovery points must be coordinated across modules. Add both to the downstream table.
- **[low]** Undefined terms remain in several FRs:
  - FR-2: "only the components required by the selected scenario". The module configuration defines one list, so there is no scenario to select.
  - FR-4: "compatible batch" and "useful diagnostic logs" (no minimum content, location, or retention).
  - FR-8: "preserves diagnostics" (same gap).
  - *Fix:* Replace "selected scenario" with "the module configuration". State a minimum diagnostic set and a retention period, or assign them to the downstream diagnostics row explicitly.

## Scope honesty — adequate
The non-goals are real and each closes something a reader might otherwise assume: a generic mocking subsystem, metric-based rollback, reversal of business data, a standby installation. The downstream table is honest that "the requirements do not establish deployed capability". There are no `[ASSUMPTION]` or `[NOTE FOR PM]` tags. The memlog shows that each adopted policy was confirmed by the user, so the absence is plausible. The open-items density suits a build-ready internal PRD. Two omissions are left for the reader to infer.

### Findings
- **[medium]** How a release reaches staging is neither required nor ruled out (§ FR-6 "exercise the release being considered for promotion in staging"; § FR-10)
  - The PRD specifies production deployment, verification and rollback in detail. It never says what puts a release into staging: merge, tag, or manual trigger.
  - It also doesn't say whether a failed staging deployment gets FR-7/FR-8 treatment, or whether failed staging E2E results just block promotion.
  - Readers must guess whether staging CD is in MVP scope.
  - *Fix:* Add a short FR for the staging deployment trigger and its failure handling, or add a non-goal saying staging deployment is manual or architecture-defined for the MVP.
- **[low]** The fate of existing hosting is not stated (addendum § Existing implementation context: Works preview as "the rollback composition until its migration parity gate"; the PRD is silent)
  - The PRD never says whether the MVP keeps, replaces, or ignores the root Works preview. Works is outside the MVP set.
  - It also never says whether MVP modules keep their own AppHosts.
  - *Fix:* Add one scope line or non-goal, for example: "The existing Works preview remains until its parity gate; MVP modules' own AppHosts are neither required nor removed by this PRD".

## Downstream usability — adequate
The Glossary is thorough, and all IDs are unique and contiguous:
- FR-1 to FR-12
- NFR-1 to NFR-3
- SM-1 to SM-6
- SM-C1 to SM-C5

Every FR, NFR and SM cross-reference resolves, as do all three addendum anchors. Sections refer to each other by ID rather than "see above", and each SM names the FRs it validates.

The damage comes from the post-finalization edits. They changed FR-2 and FR-12 without updating the addendum, the Glossary entries, SM-4, or the downstream table. A spec built from this PRD inherits the contradictions.

### Findings
- **[medium]** The Platform-workspace edit contradicts the addendum and the repository (§ FR-2 "EventStore, Memories, and McpCli are run and debugged from source in the Platform workspace, where they are declared directly"; § Glossary "Platform workspace"; addendum § McpCli context "sibling `../mcpcli` repository … absent from Platform's inspected root submodule declarations"; addendum § Existing implementation context, sixteen references with no McpCli)
  - `.gitmodules` confirms McpCli is not a Platform reference. The PRD states "declared directly" as a present fact.
  - Under the root-only rule in FR-2 and addendum § Workspace and build constraints, source-debugging McpCli in the Platform workspace requires adding it as a direct reference. That requirement is not stated and has no owner.
  - The edit also leaves unstated which composition runs when EventStore, Memories or McpCli is debugged from the Platform workspace. FR-3's minimum-environment mechanism is now tied only to domain-module workspaces (§ Individual-module development intro).
  - *Fix:* In FR-2, write "…from source in the Platform workspace, which declares each directly under `references/`". Update addendum § McpCli context to say the sibling location is superseded, or how it is reconciled. State whether Platform-workspace debugging uses the complete environment or a per-module configuration.
- **[medium]** The FR-12 agent-eligibility edit was not propagated:
  - The section intro (§ Module operations through McpCli), the Glossary "McpCli" entry, and SM-4 ("McpCli operations") still say McpCli exposes all commands and queries defined by enabled modules. SM-1 alone says "agent-eligible".
  - No SM covers the new refusal consequence.
  - The module-developer row of the downstream table lists "public operations" but not their agent-eligibility declarations.
  - "Agent use" is undefined. Applied to the CLI, it means a *human* CLI user is refused confirmation-required operations. That trade-off is real but unacknowledged.
  - "Discover" is ambiguous: are ineligible operations listed?
  - "Remain available only through that UI" places a requirement on module UIs that the PRD otherwise never mentions.
  - *Fix:*
    - Add an "Agent-eligible operation" Glossary entry, and carry the restriction into the McpCli entry, the section intro and SM-4.
    - Add eligibility declarations to the module-developer downstream row.
    - Add a negative McpCli check to SM-4.
    - State whether ineligible operations are listed, and acknowledge that the restriction applies to human CLI use too.
- **[medium]** "Release" is the unit of FR-6 to FR-8 but is never defined (§ FR-6 "submodules included in the release"; § FR-8 "partially updated workloads"; Glossary defines only "Working release")
  - Nothing says whether a release is the whole seven-module composition at fixed versions or a subset of changed modules. That choice drives gate scope, what "previous working release" means, and partial rollback.
  - The downstream table defers "release identity" to architecture, but release *scope* is a product decision.
  - *Fix:* Add a Glossary entry. For example: "Release: the versioned set of module builds and configuration promoted together; the gate covers every module included in it."
- **[low]** Several terms drift:
  - "Submodule" and "module" are used interchangeably (§ FR-6, § FR-7, § Glossary "Critical business flow", "Smoke test"), even though McpCli is not a Git submodule of Platform.
  - The Glossary "Module workspace" entry was not narrowed to match FR-2's "domain-module workspace".
  - The "Domain module" entry defines the term only by its dependencies and has no member list. For Tenants, "at least EventStore, Tenants, and Memories in addition to the module being worked on" is self-referential.
  - "Administrator" appears in the Glossary only inside "Recovery owner".
  - *Fix:* Use "module" consistently. List the domain modules in the Glossary entry. Add an "Administrator" entry.

## Shape fit — strong
The internal, single-operator product is correctly written as a capability spec. Testable consequences carry the acceptance weight, and the SMs are operational acceptance demonstrations, which fits this shape. The decision to skip named user journeys is justified in the document itself: "this PRD governs developer orchestration and module-provided interfaces; module business journeys remain owned by the modules" (§ Downstream decisions, final paragraph). UJs here would add overhead without information. As a chain-top PRD, it keeps traceability through stable IDs and SM-to-FR mapping, which is the right investment. The addendum carries the rationale depth, which keeps the PRD lean. No findings.

## Mechanical notes
- **IDs:** FR, NFR, SM and SM-C sequences are contiguous and unique, and every referenced ID exists. Addendum anchors `#module-owned-configuration-example`, `#mcpcli-context` and `#hosted-architecture-questions` resolve. Brief links resolve to `briefs/brief-platform-2026-09-27/brief.md` and its `addendum.md`.
- **Assumptions Index:** there are no inline `[ASSUMPTION]` tags and no index, so the roundtrip holds trivially.
- **Placement:** the UJ-substitution rationale sits at the end of § Downstream decisions (line 293). It would read better next to § Target users and jobs, where a reader looks for journeys.
- **Status after edits:** frontmatter still says `status: final` with an unchanged `updated` date after the post-finalization edits. The memlog's last entry says "No threshold, gate or other requirement changed". In fact FR-12 gained a new restriction and refusal consequence, and FR-2 changed where three modules are debugged. Downstream owners relying on "final" should know requirements changed after the finalize-pass review.
- **Regression check against the prior finalize-pass review** (checked after the judgments above were written):
  - No previously resolved finding has regressed. FR-6 still fails closed on missing or empty declarations, and FR-7 still validates declarations before updating production. SM-5 still requires a successful rollback plus separate failure-reporting evidence. FR-8 still uses the restored release's smoke suite. FR-9 still covers Keycloak and access restore checks. The successful local-run cleanup default is still assigned in the downstream table.
  - The prior recovery/access review accepted the "no round-the-clock coverage" disclosure as sufficient. This review disagrees and rates it high, because detection is counted in the RTO but no requirement produces it.
  - The Platform-workspace, agent-eligibility and deputy findings above are new. They come from post-finalization edits that the prior review never saw.
