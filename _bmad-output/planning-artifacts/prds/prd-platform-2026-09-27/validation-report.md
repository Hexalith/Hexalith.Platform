# Validation Report — Hexalith Platform

- **PRD:** `_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md`
- **Rubric:** `.claude/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Run at:** 2026-09-27T21:54:47+02:00
- **Grade:** Fair

## Overall verdict

The requirement shape suits a single-operator internal platform, and this is a strong capability spec. It states decisions as decisions and backs them with numeric thresholds. Missing evidence fails closed throughout FR-6, FR-7 and FR-9. The non-goals do real work, and one thesis runs through every feature: Platform orchestrates, and modules own behavior and evidence. The risk sits in two places. First, the recovery response model: the four-hour RTO counts detection and operator response, yet the PRD requires no outage detection, disclaims round-the-clock response, and adds a recovery deputy who is bound by nothing. Second, the post-finalization edits were made locally and not propagated. FR-2 now says McpCli is "declared directly" in the Platform workspace, while the addendum and the repository say it is a sibling repo. The agent-eligibility rule never reaches the McpCli glossary entry, SM-4, or the module-developer handoff.

The adversarial and consistency reviewers extend that picture without contradicting it. The adversarial reviewer (38 findings: 3 critical, 12 high, 18 medium, 5 low) reads the PRD as a build contract. It finds places where a team could pass every listed check and still ship an unsafe system: the gated unit is undefined; rollback safety rests on compatibility evidence that nothing requires; automation and CI identities sit outside the isolation requirement; gate contents are self-certified; a working release can never be demoted; backup-freshness monitoring fails open; and there is no single go-live gate. The consistency reviewer (15 findings: 7 medium, 8 low, none critical or high) checked the PRD against the memlog, the architecture spine, the spec and the brief. It found every threshold, gate and fail-closed rule consistent. Its problems are incomplete propagation of the post-final alignment edit, plus decisions accepted in the spine that never reached the PRD: the Administrator-approved release mode before G3, the Folders/Projects envelope override, and the lost-window erasure exception. The reviewers converge on the themes listed here. Where all three flag the same gap, it is the most reliable signal in this report.

Grade rationale. By the rubric, the grade is Fair because no dimension is thin or broken and there is one high finding. The adversarial reviewer rated three findings critical (ADV-1, ADV-2, ADV-3). The orchestrator checked them against the PRD text. ADV-1 is confirmed: the Glossary defines only “Working release”, while FR-6 gates on “submodules included in the release”. ADV-2 overstates the gap, because NFR-1 already says “Compatibility must be verified before automatic promotion”; the real gap is that this check is missing from FR-6’s gate conditions and SM-5’s evidence and has no owner. ADV-3 is confirmed as a gap, because NFR-3 covers only “staging application credentials” and leaves out automation and CI credentials. All three were treated as high-impact rather than critical for grading. If the reader accepts the adversarial severities, the grade becomes Poor.

### Convergent themes

- **“Release” is undefined, so the promotion gate has no fixed scope** — Rubric (Downstream usability: “Release” never defined; low: “submodule”/“module” drift); ADV-1, ADV-16 — Gate scope, the “previous working release” and partial rollback all depend on what a release contains; one reading promotes a change that breaks an unchanged module, another blocks promotion permanently.
- **NFR-1 compatibility evidence is not wired into FR-6 or SM-5** — Rubric (Done-ness clarity: NFR-1 check not a condition of the FR-6 gate); ADV-2, ADV-5; CON-7 — Automatic rollback is safe only if the previous release can read what the new one wrote; with no gate condition, owner or measure, missing evidence does not block promotion, and the “separately planned release” for incompatible changes (the spine’s Administrator-approved mode) is not in the PRD.
- **Manual, approved and resume paths are undefined** — Rubric (Decision-readiness: failed/unverified recovery has no response bound or resume actor); ADV-5, ADV-6; CON-7 — The PRD assumes a promotion pause, an incompatible-change path and pre-G3 releases, but never says who deploys manually, who lifts the FR-8 stop, or how a bad working release is revoked, so implementers will add an ungated bypass.
- **The recovery deputy has no requirement behind it; key custody and notification stay single-person** — Rubric (Substance over theater); ADV-8, ADV-22; CON-6 — The deputy was added to remove the recovery single point of failure, but FR-8, FR-9, SM-5 and the downstream table still notify and empower Administrator alone.
- **The RTO counts detection and operator response, but no detection requirement and no 24/7 response exist** — Rubric (Decision-readiness, high); ADV-7, ADV-9; CON-8 — Whether the four-hour target holds at 03:00 on a Sunday decides on-call coverage; scheduled exercises and an alert that fires only once the RPO is breached cannot measure it, and the spine reads the target two ways.
- **The McpCli agent-eligible rule is not propagated and can be satisfied by an empty set** — Rubric (Downstream usability: FR-12 edit not propagated); ADV-12, ADV-17; CON-1, CON-2 — The section intro, Glossary and SM-4 still promise all commands and queries, no measure covers refusal, and McpCli could ship refusing every operation while SM-1 passes.
- **FR-2 says McpCli is “declared directly” in the Platform workspace, but it is a sibling repository** — Rubric (Downstream usability: Platform-workspace edit; low: “Module workspace” not narrowed); CON-3, CON-4, CON-5 — A target state is written as current fact; the direct `references/` declaration has no owner, and nothing says where EventStore, Memories and McpCli minimum environments and integration evidence run.
- **McpCli’s hosted form and readiness are undefined** — ADV-11; CON-15 — FR-1, FR-10 and SM-1 require McpCli to become ready and be accessible in hosted environments, yet it is a client tool on the user’s host, and per-module flow and smoke obligations do not fit it.
- **“Supported interfaces” is undefined** — Rubric (Done-ness clarity); ADV-32 — FR-10 cannot be tested, and the isolation checks in FR-11, NFR-3 and SM-4 have no enumerated surface to cover.
- **The cross-module recovery point and restore integrity are undefined** — Rubric (Done-ness clarity: cross-module data integrity); ADV-18, ADV-19 — With stores backed up independently, RPO can be reported from the freshest store while the newest consistent set is older, and nobody owns the consistency check or Platform’s own state.
- **“Before production use” has no single go-live gate** — ADV-13; CON-7, CON-13 — Each of the six preconditions can be skipped on its own; the spec maps the phrase to G2 but the PRD never records that mapping, and SM-4 timing already differs between the PRD and the spec.
- **Erasure and deletion versus backup restore are unaddressed** — ADV-29; CON-11 — The spine accepts that erasures and deletions in the lost window can reappear after a restore, a privacy consequence that NFR-2 never states.
- **The fate of existing hosting (Works preview, Agents) is unstated** — Rubric (Scope honesty, low); ADV-31 — An implementer may delete the Works rollback composition, or carry non-MVP modules through the gate, backups and access rules with no requirements.
- **The path into staging is unspecified** — Rubric (Scope honesty: how a release reaches staging); ADV-24 — Neither the staging trigger nor serialization of staging deployments and E2E runs is stated, so E2E evidence can describe a partially deployed or older release.

## Dimension verdicts

- Decision-readiness — adequate
- Substance over theater — strong
- Strategic coherence — adequate
- Done-ness clarity — adequate
- Scope honesty — adequate
- Downstream usability — adequate
- Shape fit — strong

## Findings by severity

### Critical (3)

*These three findings are listed as the adversarial reviewer rated them. The orchestrator checked them against the PRD text and treated them as high-impact rather than critical for grading; see the Overall verdict (grade rationale).*

**[Adversarial ADV-1]** — The gated unit ("release" / "included submodule") is undefined, so the gate can skip the modules a change breaks (§ Staging validation and production promotion / FR-6, FR-7; § Downstream decisions ("release identity and promotion mechanism"); Glossary (no "release" or "submodule" entry))

Problem: "The critical business flows used by the gate come from the submodules included in the release" and FR-7 validates declarations "for every included submodule". The PRD never says what a release is: a composite of all seven modules at pinned versions, or a per-module package. It also never says whether "included" means *deployed* or *changed*. "Submodule" is a Git term, and the addendum records that Platform declares **sixteen** references (Works, Agents, ChatBot, and others), not seven. "Release identity" is deferred to architecture, but the gate's meaning depends on it.

Consequence: One reading gates an EventStore-only release on EventStore's flows alone, so a change that breaks Parties' critical flow is promoted. The opposite reading requires all sixteen Git submodules, including non-MVP ones with no flows, to supply declarations, which blocks promotion permanently. Both readings satisfy FR-6 as written.

Fix: Define **Release** as the immutable set of artifacts (by digest) for every module deployed to the target environment, plus the versioned configuration. Require the gate to run the declared flows of **every enrolled module deployed in staging**, whether or not it changed. Replace "submodule" with "enrolled module" and define that term in the glossary.

**[Adversarial ADV-2]** — NFR-1 compatibility is required but has no gate, no evidence owner, and no success measure (§ NFR-1; FR-6; SM-5)

Problem: "Compatibility must be verified before automatic promotion. An incompatible change requires a separately planned release and recovery procedure. The architecture must define the compatibility evidence." FR-6's blocking conditions list declarations, E2E results, and release identity, but not compatibility evidence. Nobody is assigned to classify a change as incompatible. SM-5 checks only that data is preserved in a controlled rehearsal, not that each promoted release was verified compatible.

Consequence: A release starts writing a new event format and fails at minute 3 of its verification window. FR-8 restores the previous code, which cannot read the events written in those 3 minutes. The restored release crashes, or silently misreads streams its smoke tests never touch, and FR-8 may still report success. Automatic rollback is safe only if this evidence exists, and nothing requires it to exist.

Fix: Add an FR-6 consequence: "Each release carries per-module evidence that the recorded previous working release can read all schemas and event formats the new release writes. Missing, failed, or not-applicable-without-justification evidence blocks automatic promotion. A module declaring a change incompatible routes the release to the manual path (see ADV-5)." Extend SM-5 so one rehearsal uses a failing release that writes new-format data before rollback.

**[Adversarial ADV-3]** — Isolation requirements omit the automation identities most likely to cross environments (§ NFR-3; FR-10; FR-11; SM-4)

Problem: NFR-3 covers "staging application credentials" and "staging-only users". FR-11 covers users. None of the requirements mention the CI/CD deployer identity (which deploys *both* staging and production), the production smoke-test runner, backup jobs, restore tooling, or McpCli's hosted service identity. Every one of these needs production credentials.

Consequence: A single GitHub workflow or runner holding both environments' deploy credentials satisfies every listed check. A staging or pull-request job, or a compromised dependency in it, can then deploy to or read production. SM-4 and SM-C2 stay green because they test only staging-only users and staging application credentials.

Fix: Extend NFR-3: "Credentials that can deploy to, read, or write production (deployment, smoke-test, backup, restore, and service identities) are available only to the production promotion and recovery jobs. Staging, pull-request, and CI integration workflows cannot obtain them." Add matching negative checks to SM-4 and SM-C2.

### High (13)

**[Rubric — Decision-readiness]** — RTO counts detection and response, but neither is required or bounded (§ NFR-2 table and following paragraph; § FR-9; § SM-6)

- The four-hour RTO clock starts at the outage and includes detection. No FR requires production outage detection or alerting. FR-8 explicitly puts post-verification failures "outside this automatic deployment-recovery policy".
- Today the only detection path is incidental. FR-9's alert fires when the newest usable recovery point is "older than one hour", so detection could use up to a quarter of the RTO before anyone is notified. That notification goes through GitHub, which is not a paging channel.
- The PRD also disclaims round-the-clock response. So it is undecided whether the four-hour target applies at 03:00 on a Sunday. That choice decides whether the team needs on-call coverage.
- SM-6's scheduled exercises cannot test this: FR-9 folds "operator-response … assumptions" into the assessment rather than measuring them.

Fix: Decide the coverage model. One option: "RTO applies to outages starting within declared coverage hours; outside them, the clock starts at the next coverage window." Another: the RTO holds around the clock and the deputy gives the second responder. Then add a production-availability alert requirement, or put "outage detection" into the downstream table with an owner.

**[Adversarial ADV-4]** — Gate contents are self-certified and can be weakened inside the release being gated (§ FR-6 (second consequence); FR-7 (second consequence); SM-C1)

Problem: The only floor is "a non-empty critical-flow declaration and required E2E checks for every declared flow" and "empty required check sets block the update". If the declaration is read from the release under test, that release can remove the flow that is failing, or remap it to a trivial test. The gate then sees a valid, non-empty, fully passing set. Nothing compares declarations with the previous working release, and nothing proves that an E2E result actually exercised the staged services beyond the words "using the deployed real services".

Consequence: A regression is promoted with no failed, skipped, missing, or wrong-release evidence, so SM-C1 records zero violations. The fail-closed fix from the earlier review only blocks the case that is easy to see.

Fix: Require the gate to diff flow and smoke declarations against the previous working release. Any removed or remapped flow or check requires an explicit, recorded approval by Administrator (or deputy) for that release. Require each E2E result to carry evidence of the release identity it observed from the staged services.

**[Adversarial ADV-5]** — No manual, hotfix, or resume path is specified, yet several requirements assume one (§ SM-5 ("before enabling automatic production promotion"); FR-8 ("Further automatic promotions stop pending intervention"); FR-7 ("A pre-existing unhealthy production environment stops the update for investigation"); NFR-1 ("separately planned release"))

Problem: The PRD implies a mode in which automatic promotion is not enabled, a pause that someone lifts, a release path for incompatible changes, and a way to fix an already-unhealthy production. None of these is defined: who may deploy manually, what evidence is still required, how the pause is cleared and by whom, or whether a manually deployed release becomes a "working release".

Consequence: Implementers will add an unrecorded manual workflow or `kubectl` bypass with no gate, no verification, and no working-release record. The next automatic deployment then has no valid rollback target. When production is unhealthy, FR-7 blocks the automatic path that would deliver the fix. SM-C1 does not say whether manual promotions count.

Fix: Add an FR for manual promotion. It should name who may perform it; state which FR-6 and FR-7 checks still apply or are waived with a recorded justification; require that the result is recorded as a working release only after FR-7 verification; name who lifts the FR-8 pause and what evidence they record; and apply SM-C1 to every production promotion, manual or automatic.

**[Adversarial ADV-6]** — A "working release" can never be demoted, so a known-bad release becomes the rollback target (§ FR-8 (last consequence); Glossary "Working release"; FR-7)

Problem: A release becomes "working" once it passes the 5-minute window. "Failures found after the verification window are handled as operational incidents, outside this automatic deployment-recovery policy." Nothing pauses automatic promotion during such an incident, and nothing lets anyone mark a release as not working.

Consequence: R2 passes its window and then breaks at minute 20. R3 is automatically promoted on top of it during the incident and fails. FR-8 rolls back to R2 and verifies it with R2's smoke suite, which already missed the defect, then reports "successful recovery" to a broken release. SM-C3 counts no violation.

Fix: State that an open production incident pauses automatic promotion. Let the recovery owner or deputy revoke a release's working status, with the revocation recorded. Rollback-target selection must skip revoked releases, and report "no valid target" rather than guess.

**[Adversarial ADV-7]** — The RTO includes operator response, but no response coverage exists and SM-6 accepts assumed values (§ NFR-2; FR-9 (restore exercise consequence); SM-6; addendum § Selected approach and operational scope)

Problem: The RTO covers "detection, operator response, replacement capacity, restore, and validation", yet "The targets do not establish an uptime percentage or round-the-clock response guarantee". The addendum also concedes that the target only "Fits an internal MVP with one named recovery owner **if that owner can respond**". Restore exercises are scheduled, so detection and response time are zero. FR-9 allows "operator-response and capacity-provisioning **assumptions** included in the RTO assessment."

Consequence: SM-6 is satisfied by a measured restore time plus an assumed response time. A failure at the start of a weekend or holiday with one responder cannot meet 4 hours, but every exercise reports that it does. This contradiction is built into NFR-2.

Fix: Choose one of two options. Either scope the RTO clock explicitly (for example, from owner acknowledgement, or during stated coverage hours), or define response coverage across owner and deputy. Require SM-6 to report measured restore time and assumed response time as separate figures, and require at least one exercise per year to start from an unannounced alert.

**[Adversarial ADV-8]** — The recovery deputy has no requirements, which leaves Administrator as a single point of human failure (§ Target users (Recovery deputy); Glossary; FR-8; FR-9; SM-5; § Success measures preamble)

Problem: The deputy "backs Administrator for recovery and for custody of recovery keys", but the deputy was added after finalization with "No threshold, gate or other requirement changed" (memlog). FR-8 notifies only "the recovery owner, Administrator". FR-9 notifies only "Administrator", and SM-5 verifies "GitHub delivery to Administrator" only. The deputy is never named, has no notification, access, key-custody, or exercise obligation, and does not appear in the downstream table. Administrator is also the product owner, the owner of recovery evidence, co-owner of production-user administration, and the person who accepts that evidence, so the same person produces the evidence and accepts it.

Consequence: If Administrator is unavailable, nobody learns about a failed rollback, stale backups, or paused promotions, and the recovery keys sit with one person. The deputy exists only in the glossary.

Fix: Add FR consequences. Every FR-8 and FR-9 notification goes to owner and deputy. The deputy holds independent access to recovery and decryption material, and this is verified in a restore exercise. The deputy performs at least one restore exercise per defined period. Name the deputy in the downstream table, and verify delivery to the deputy in SM-5 and SM-6.

**[Adversarial ADV-9]** — Backup-freshness monitoring fails open and alerts only after the RPO is already breached (§ FR-9 (fifth consequence); SM-6)

Problem: "Platform notifies Administrator … when a backup job fails or the newest usable recovery point is older than **one hour**." There are three gaps. (a) If the monitor or its GitHub integration stops, silence looks like health; there is no heartbeat or dead-man's switch. (b) The alert threshold equals the RPO, so the alert means the target has already been missed. (c) "Usable" is not defined between monthly restores: job exit status, a checksum, and a verified restore are all possible readings.

Consequence: Backups stop silently, or the chain becomes unrestorable, and the first discovery happens at the next monthly exercise or during the disaster itself.

Fix: Alert when the monitor's heartbeat is missing, detected from outside the primary environment. Add a warning threshold below the RPO (for example, 45 minutes). Define the minimum usability evidence for each recovery point (for example, an integrity check of the base-plus-incremental chain) and state that only points passing it count toward recovery-point age.

**[Adversarial ADV-10]** — The off-site copy is optional by grammar (FR-9 (fourth consequence); § NFR-2 (last paragraph); § MVP non-goals)

Problem: "protection against whole-site loss **additionally requires** an off-site copy" and "Whole-site loss requires an independent recovery location". These sentences state what whole-site protection would need, not that the MVP must provide it. "Outside the primary server/storage failure domain" can be met by a second disk or NAS on the same LAN as `192.168.1.30`.

Consequence: A fire, theft, or site-wide power or flooding event destroys production and every backup, yet FR-9 and SM-6 were satisfied.

Fix: State explicitly whether whole-site loss is in MVP scope. If it is, require an off-site copy and include the off-site copy's age in freshness monitoring. If it is not, move it to § MVP non-goals as an accepted risk signed by Administrator.

**[Adversarial ADV-11]** — Infrastructure modules and McpCli cannot meet per-module flow and smoke obligations as written (FR-6 (second consequence); FR-7 (first consequence); FR-1; FR-10; Glossary "Critical business flow"; addendum § McpCli context)

Problem: Every included submodule must supply "a non-empty critical-flow declaration", and a critical flow is "a business operation that its owning module designates". EventStore, Memories, and McpCli do not own business operations. McpCli is "the `hexalith` .NET tool" with "stdio MCP, deferred HTTP transport". Yet FR-1 requires McpCli to be "available" with readiness checks, and FR-10 requires the whole MVP set to be "accessible in both environments". The PRD never says whether hosted McpCli is a deployed service or a client-side tool that talks to hosted EventStore.

Consequence: Either promotion is blocked indefinitely, or teams invent vacuous "flows" for infrastructure modules to meet the non-empty rule. SM-4's hosted McpCli acceptance depends on a hosting decision nobody has made.

Fix: Classify modules as domain, infrastructure, or client tool, and state the flow, smoke, readiness, and backup obligations for each class. For example, infrastructure modules are covered by the domain flows that exercise them. State whether McpCli is deployed in staging and production or is only a client.

**[Adversarial ADV-12]** — An empty McpCli catalog satisfies SM-1 and SM-4 (FR-12; SM-1; SM-4; addendum § McpCli context ("empty catalog with enrollment pending"))

Problem: "McpCli executes only operations that their owning module declares eligible for agent use", and SM-1 requires demonstrations that "execute enabled modules' agent-eligible commands and queries". If no module declares any operation eligible, which is the default and matches the observed empty catalog, the positive demonstrations are vacuously true. The fail-closed rule applied to FR-6 and FR-7 was not applied here. Only the *refusal* consequence can actually be tested.

Consequence: McpCli can ship refusing every operation while SM-1 and SM-4 pass.

Fix: Require each enrolled domain module to publish at least one agent-eligible query and one command, or a recorded exemption. SM-1 and SM-4 must name the operations demonstrated for each module in each environment.

**[Adversarial ADV-13]** — No go-live gate; "before production use" is undefined and appears six times (NFR-2; FR-9; SM-4; SM-6; § Downstream decisions; FR-8 (first-deployment consequence))

Problem: Readiness evidence is repeatedly required "before production use", but nothing defines when production use begins (first deployment, first real tenant, public DNS, first production user), who declares it, or that these preconditions are checked together. FR-8 already handles a first *deployment*, so production can exist before "production use".

Consequence: Production accumulates real data after its first deployment while the restore exercise, isolation checks, and GitHub delivery are still pending. Each "before production use" condition can be skipped on its own.

Fix: Add a Production readiness gate FR. It lists the required evidence (SM-4, SM-5, SM-6, notification delivery to owner and deputy, and the identified replacement capacity) and requires a recorded Administrator sign-off. Production user access and DNS are enabled only after the gate passes.

**[Adversarial ADV-14]** — Isolation runs one way only; restores and debugging can put production data on staging-reachable infrastructure (FR-10 (third consequence); § NFR-3; FR-9 (restore exercise consequence))

Problem: FR-10 forbids copying "staging data or credentials into production", and NFR-3 addresses staging access to production. Nothing forbids copying production data *into* staging, for example for debugging. FR-9's "isolated restore exercise" does not say what it is isolated from, where it runs, who can reach it, or when it is destroyed. The shared database server permitted by § Confirmed MVP scope is an obvious target for such a restore.

Consequence: A monthly restore places production business data on shared infrastructure reachable with staging credentials, where staging-only users can read it. SM-4 still passes because its negative checks target production endpoints only.

Fix: Add to NFR-3: "Production data, including backups and restored copies, exists only in locations with production access controls. Restore-exercise environments inherit those controls and are destroyed after evidence capture. Production data is never copied to staging." Add a matching SM-4 check.

**[Adversarial ADV-15]** — Shared infrastructure isolates access, not capacity or faults; staging can take production down (§ Confirmed MVP scope; § Vision; § NFR-3; FR-7 (failure triggers))

Problem: Staging and production "may share supporting services or capacity, such as a database server, only while their application data and credentials remain isolated." There is no resource, quota, or fault-isolation requirement. The staging E2E gate, restore exercises, and possibly CI (see ADV-26) run on the same cluster.

Consequence: A staging E2E run, a runaway staging workload, or a restore exercise saturates the shared database during a production verification window. The result is a production outage, a false FR-7 rollback, or both, with the cause attributed to the release. Maintenance on the shared database takes down both environments at once, so the staging gate is also unavailable during production recovery.

Fix: Require resource limits or quotas that protect production from staging on shared services. State whether CI and restore exercises may run on the production cluster. Require FR-7 diagnostics to record the state of shared dependencies so a shared-infrastructure fault is not reported as a release failure.

### Medium (35)

**[Rubric — Decision-readiness]** — A failed or unverified automatic recovery has no response bound, and promotion has no availability window (§ FR-8 consequences 3–4; § Vision)

- Automatic promotion can fire whenever staging E2E passes. If the single rollback attempt fails, production stays down until Administrator intervenes.
- That case is not a "major production server or storage failure requiring backup restoration". NFR-2 therefore does not apply, and no target covers it.
- "Further automatic promotions stop pending intervention" does not say who resumes promotion or what evidence they need.

Fix: Either restrict automatic promotion to times when the recovery owner or deputy is available, or accept an unbounded failed-recovery outage explicitly as a trade-off. Also name the actor and the condition for resuming promotions.

**[Rubric — Substance over theater]** — The recovery deputy is a role that binds nothing (§ Target users line 30; § Glossary "Recovery deputy"; addendum § Disaster recovery, "Fits an internal MVP with one named recovery owner" and "a single recovery owner do not establish round-the-clock response coverage")

- The deputy was added for "recovery and … custody of recovery keys". Yet no FR, NFR, SM, or downstream row mentions the deputy.
- FR-8 and FR-9 notify only Administrator. FR-9's "required access/decryption material remain accessible independently" does not require a second custodian.
- "A named person" is never named, and no row assigns naming the deputy before production use.
- The addendum still reasons from a single recovery owner.
- This matters because single-person key custody is exactly the disaster-recovery single point of failure the deputy was meant to remove.

Fix:
- Add to FR-9: recovery access and decryption material are held by at least two custodians, namely Administrator and the recovery deputy.
- Decide whether FR-8 and FR-9 notifications also reach the deputy.
- Add "designate recovery deputy" to the Administrator row of the downstream table.
- Update the two addendum sentences.

**[Rubric — Strategic coherence]** — The brief's problem statement was dropped, and nothing measures whether the thesis worked (§ Vision; § Success measures)

- The brief has a Problem section: the only root composition is an optional Works preview, and full-system composition and reusable module startup are missing. The PRD opens directly with the solution. Read on its own, it never says what is broken today or why the shared-orchestration bet beats per-module hosting.
- All six SMs are acceptance demonstrations. None measures adoption, for example that each MVP module's local, test and CI path actually runs through Platform instead of its own wiring. Adoption is what would validate "one common solution".

Fix: Add a two- to three-sentence problem statement, taken from the brief, before the Vision. Add an adoption-shaped SM, or an explicit statement that bespoke per-module hosting is retained or retired for MVP modules.

**[Rubric — Done-ness clarity]** — "Supported interfaces" is undefined, so FR-10's first consequence cannot be tested (§ FR-10 "accessible in both environments through its supported interfaces"; § FR-11 "any supported interface"; § SM-4 "expose the supported MVP interfaces")

- Nothing lists which interfaces count per module: HTTP APIs through ingress, module UIs, EventStore endpoints, McpCli.
- An engineer cannot tell when FR-10 is done, and the isolation checks in FR-11 and NFR-3 have no enumerated surface to cover.

Fix: Define "supported interface" in the Glossary as the module-declared external surfaces. Add module-declared interfaces to the module-developer row of the downstream table.

**[Rubric — Done-ness clarity]** — NFR-1's compatibility check is not a condition of the FR-6 gate (§ NFR-1 "Compatibility must be verified before automatic promotion"; § FR-6; § SM-C1)

- FR-6 lists only E2E evidence as gating, and every missing-E2E case fails closed.
- Previous-version schema and event compatibility gets no gate consequence and no fail-closed rule. SM-C1 counts only E2E bypasses.
- A release with missing compatibility evidence could satisfy FR-6 as written.

Fix: Add an FR-6 consequence: "Missing or failed previous-version compatibility evidence blocks automatic promotion; the release requires the separately planned procedure in NFR-1." Extend SM-C1 to cover compatibility evidence.

**[Rubric — Done-ness clarity]** — Cross-module data integrity after restore has no definition and no owner (§ FR-9 "Restoration verifies … data integrity across modules"; addendum "independently timed database snapshots do not by themselves prove a recoverable cross-module state")

- Several stores are backed up independently every 30 minutes. The PRD requires cross-module consistency to be verified but never says what consistent means (a coordinated point, or an accepted skew with reconciliation), nor who provides the check.
- The downstream table assigns "authoritative-state inventories" to modules, but not consistency checks.

Fix: Require each module to supply a post-restore integrity and consistency check against its EventStore history. State whether recovery points must be coordinated across modules. Add both to the downstream table.

**[Rubric — Scope honesty]** — How a release reaches staging is neither required nor ruled out (§ FR-6 "exercise the release being considered for promotion in staging"; § FR-10)

- The PRD specifies production deployment, verification and rollback in detail. It never says what puts a release into staging: merge, tag, or manual trigger.
- It also doesn't say whether a failed staging deployment gets FR-7/FR-8 treatment, or whether failed staging E2E results just block promotion.
- Readers must guess whether staging CD is in MVP scope.

Fix: Add a short FR for the staging deployment trigger and its failure handling, or add a non-goal saying staging deployment is manual or architecture-defined for the MVP.

**[Rubric — Downstream usability]** — The Platform-workspace edit contradicts the addendum and the repository (§ FR-2 "EventStore, Memories, and McpCli are run and debugged from source in the Platform workspace, where they are declared directly"; § Glossary "Platform workspace"; addendum § McpCli context "sibling `../mcpcli` repository … absent from Platform's inspected root submodule declarations"; addendum § Existing implementation context, sixteen references with no McpCli)

- `.gitmodules` confirms McpCli is not a Platform reference. The PRD states "declared directly" as a present fact.
- Under the root-only rule in FR-2 and addendum § Workspace and build constraints, source-debugging McpCli in the Platform workspace requires adding it as a direct reference. That requirement is not stated and has no owner.
- The edit also leaves unstated which composition runs when EventStore, Memories or McpCli is debugged from the Platform workspace. FR-3's minimum-environment mechanism is now tied only to domain-module workspaces (§ Individual-module development intro).

Fix: In FR-2, write "…from source in the Platform workspace, which declares each directly under `references/`". Update addendum § McpCli context to say the sibling location is superseded, or how it is reconciled. State whether Platform-workspace debugging uses the complete environment or a per-module configuration.

**[Rubric — Downstream usability]** — The FR-12 agent-eligibility edit was not propagated (§ Module operations through McpCli (intro); § Glossary "McpCli"; § SM-1; § SM-4; § Downstream decisions)

- The section intro (§ Module operations through McpCli), the Glossary "McpCli" entry, and SM-4 ("McpCli operations") still say McpCli exposes all commands and queries defined by enabled modules. SM-1 alone says "agent-eligible".
- No SM covers the new refusal consequence.
- The module-developer row of the downstream table lists "public operations" but not their agent-eligibility declarations.
- "Agent use" is undefined. Applied to the CLI, it means a *human* CLI user is refused confirmation-required operations. That trade-off is real but unacknowledged.
- "Discover" is ambiguous: are ineligible operations listed?
- "Remain available only through that UI" places a requirement on module UIs that the PRD otherwise never mentions.

Fix:
- Add an "Agent-eligible operation" Glossary entry, and carry the restriction into the McpCli entry, the section intro and SM-4.
- Add eligibility declarations to the module-developer downstream row.
- Add a negative McpCli check to SM-4.
- State whether ineligible operations are listed, and acknowledge that the restriction applies to human CLI use too.

**[Rubric — Downstream usability]** — "Release" is the unit of FR-6 to FR-8 but is never defined (§ FR-6 "submodules included in the release"; § FR-8 "partially updated workloads"; Glossary defines only "Working release")

- Nothing says whether a release is the whole seven-module composition at fixed versions or a subset of changed modules. That choice drives gate scope, what "previous working release" means, and partial rollback.
- The downstream table defers "release identity" to architecture, but release *scope* is a product decision.

Fix: Add a Glossary entry. For example: "Release: the versioned set of module builds and configuration promoted together; the gate covers every module included in it."

**[Adversarial ADV-16]** — Configuration, Platform, and shared-infrastructure changes bypass the gate (FR-6; FR-7; FR-10 (third consequence); addendum § Hosted architecture questions)

Problem: The gate applies to "a release". Nothing says that production-only configuration values, Keycloak client or role changes, ingress, Dapr component definitions, Platform's own manifests, or database-server upgrades are part of a release. Production configuration values are never exercised in staging, because the two environments intentionally differ.

Consequence: The changes most likely to break production (configuration and infrastructure) skip FR-6 E2E, FR-7 verification, and FR-8 rollback.

Fix: State that any change to production application configuration or Platform-managed manifests is a release subject to FR-6 through FR-8. Require a written procedure for shared-infrastructure changes, which affect both environments at once.

**[Adversarial ADV-17]** — The enforcement point for agent eligibility and human confirmation is unspecified (FR-12 (intro and first consequence); FR-11 (first consequence); addendum § McpCli context)

Problem: Non-eligible operations are "refused through McpCli" and human-confirmation operations "remain available only through that UI". If the refusal is implemented in McpCli (a client-side .NET tool), an agent holding the user's token can call EventStore directly. FR-11 explicitly anticipates direct API access. The PRD also does not require recording whether a production command came from a human or an agent.

Consequence: The human-confirmation safeguard becomes a client-side convenience that any direct call bypasses, and production audit cannot tell agent actions from human ones.

Fix: State that eligibility and confirmation rules are enforced on the server side (EventStore or the module), and that production commands record the invoking principal and interface (UI, CLI, MCP, or API).

**[Adversarial ADV-18]** — The cross-module recovery point and its age are undefined (§ NFR-2 (RPO row); FR-9 (sixth consequence); Glossary "Recovery point")

Problem: RPO is measured by "Age of the newest usable recovery point". With several stores backed up at different times, it is unclear which age counts. The glossary defines a recovery point as "a usable copy of authoritative application data", in the singular. FR-9 requires "data integrity across modules" but does not define a consistent set.

Consequence: RPO is reported from the freshest store while the newest *consistent* restorable set is older. Alternatively, restores mix points in time (a Parties record references a Tenants record that the Tenants backup lacks).

Fix: Define a recovery point as a cross-module consistent set, with RPO age equal to the age of its oldest member. Freshness monitoring (ADV-9) measures that value.

**[Adversarial ADV-19]** — The backup inventory is self-declared and omits Platform-owned state (FR-9 (first consequence); § Downstream decisions (last row))

Problem: Coverage is "identified by each deployed module". Platform's own state has no owner: working-release records, versioned configuration, gate and compatibility evidence, secrets, certificates, Dapr components, and Kubernetes objects. Nothing checks that module inventories are complete.

Consequence: After disaster recovery, Platform cannot identify a previous working release, so FR-7 blocks every update. Alternatively, a store no module listed is lost, and the restore exercise still passes because smoke tests never touch it.

Fix: Require Platform to inventory its own state. Require each restore exercise to reconcile the inventory against all stateful resources found in the cluster (volumes, databases, secrets) and fail on any that are unlisted.

**[Adversarial ADV-20]** — Local, CI, and hosted environments have no common version or composition source (FR-2 (third consequence); FR-4 (third consequence); addendum § Workspace and build constraints; § Hosted architecture questions ("choose CI hosting"))

Problem: "Local development and testing use project references and Debug assets. CI/CD uses NuGet package references and Release assets." Nothing requires CI's NuGet versions to correspond to the submodule commits pinned under `references/`. Nothing requires the local Aspire, CI, and Kubernetes compositions to come from the same service list and configuration.

Consequence: Local integration tests pass against EventStore commit X, CI tests against NuGet version Y, and staging deploys Z. The "integration acceptance" evidence describes none of the systems actually deployed.

Fix: Define the authoritative dependency version for each release. Require CI and hosted package versions to match the pinned references, or record the mapping. Require all three compositions to come from one module-configuration source.

**[Adversarial ADV-21]** — Module-root composition is undefined for servers with no direct declaration, and for embedded Platform version skew (FR-2 (second consequence); FR-3; addendum § Workspace and build constraints)

Problem: A module configuration may list any server ("any additional dependencies its developer declares"), but "Dependency initialization follows only the active root repository's direct declarations under `references/`; nested submodules remain uninitialized." Suppose Parties' configuration enables Folders without declaring it under `parties/references/`. Platform could fail, fetch a package or container, or initialize a nested submodule (which is prohibited). Separately, the Platform version embedded in a module may expect module versions that differ from the module's own pinned EventStore.

Consequence: Implementations will differ, and resolving the problem by initializing nested submodules would violate the stated policy.

Fix: State that a listed server with no direct declaration is a configuration error, or name the permitted fallback. Require a compatibility check between the embedded Platform version and the module's declared references.

**[Adversarial ADV-22]** — GitHub as the only channel: possible exposure and unconfirmed receipt (FR-8 (third consequence); FR-9 (fifth consequence); SM-5 ("Verify GitHub delivery to Administrator"); addendum § GitHub notification integration)

Problem: Notifications carry release, environment, recovery status, and "where to find diagnostic evidence", and the repository visibility is not constrained. Delivery is verified once; there is no acknowledgement, re-notification, or escalation, and a GitHub outage is not considered.

Consequence: Production diagnostics end up visible to anyone who can read the repository, and a delivered notification lands in a filtered inbox. Promotions stay paused, or a failed recovery goes unhandled, indefinitely.

Fix: Post production notifications and diagnostics only to locations restricted to production-authorized people. Require acknowledgement of critical notifications, with re-notification and escalation to the deputy within a stated time.

**[Adversarial ADV-23]** — Verification-window mechanics can be read two ways (§ FR-7 (fourth to sixth consequences); FR-8 (second consequence))

Problem: Three points are open. (a) "unable to serve traffic for 60 continuous seconds" does not say whether this is measured by probes or by real requests, which matters for a low-traffic internal system. (b) "the latest required smoke-test results are passing at the end of the verification window" does not set a cadence, so a single run at the start of the window satisfies it. (c) "A required check without a completed passing verification result **at the deadline**" does not say whether the deadline is the 10-minute rollout deadline or the end of the window.

Consequence: Two compliant implementations detect different failures. A suite run only at the start of the window misses a defect that appears at minute 3.

Fix: Name the availability signal. Require smoke runs at least at the start and end of the window, or at a stated interval. Name the deadline.

**[Adversarial ADV-24]** — Reruns, concurrency, and ordering of staging promotions are unspecified (§ FR-6 (fourth and sixth consequences); SM-C1)

Problem: "Any required E2E test that fails … prevents automatic promotion" does not say whether a rerun that later passes clears the failure, or whether SM-C1 ("zero releases promoted with failed … evidence") counts it. Nothing serializes staging deployment and E2E runs, and nothing prevents promoting an older release after a newer one.

Consequence: Flaky tests get rerun until they pass. E2E for release A runs against a partially deployed release B. A late-finishing older release downgrades production.

Fix: Define the rerun policy and how SM-C1 counts reruns. Serialize staging deployment and E2E per release. Forbid promoting a release older than the current working release.

**[Adversarial ADV-25]** — "Zero" counter-metrics cannot be observed without retained, auditable records (§ Success measures preamble; SM-C1–SM-C3)

Problem: "Record the release or source revision, environment, result, and supporting diagnostics with each demonstration" covers demonstrations, not every promotion or recovery decision. No retention, immutability, or periodic audit is required. SM-C2 is limited to "the staging-only negative checks".

Consequence: "Zero bypassed validations" and "zero false recovery successes" mean "none noticed". A violation is found only by accident.

Fix: Require an append-only record for every promotion and recovery (release ID, declaration digest, results, decision, actor), retained for a stated period. Compute SM-C1 and SM-C3 from that record on a stated schedule.

**[Adversarial ADV-26]** — CI orphan cleanup and CI hosting location are unaddressed (FR-4 (tenth and eleventh consequences); SM-C4; addendum § Hosted architecture questions)

Problem: "CI automatically cleans up … after success, failure, or cancellation". A crashed runner, a killed job after timeout, or a lost runner fits none of these cases, and no orphan sweep is required. CI hosting is deferred, and nothing prevents it from running on the production cluster at `192.168.1.30`. SM-C4 counts only "cancelled/finished" runs.

Consequence: Orphaned CI environments build up, possibly on the cluster that hosts production, and SM-C4 stays at zero.

Fix: Require ownership labels with a time-to-live and a periodic orphan sweep. State whether CI may run on the production cluster.

**[Adversarial ADV-27]** — Local identity and permissions are undefined (FR-12 (intro and third consequence); FR-1; SM-1; addendum § Hosted architecture questions)

Problem: McpCli access in local Aspire is "subject to the user's permissions in the selected environment". The only identity provider named is the shared Keycloak "on the Kubernetes installation", which also hosts production. The PRD does not say whether local environments use it, a local identity provider, or no authentication.

Consequence: Either developer laptops depend on, and hold credentials for, the production-hosting identity server, or a local no-auth mode hides permission bugs that SM-1 cannot catch.

Fix: State the local identity source and seed permissions. State that local environments never use production realms, clients, or credentials.

**[Adversarial ADV-28]** — Production smoke writes have no containment (FR-7 (first consequence); addendum § Module responsibilities and production test data)

Problem: "necessary writes use dedicated synthetic data without real-user changes or external effects". Yet "Do not assume event records can simply be deleted". There is no dedicated synthetic tenant, no exclusion from business queries, and no bound on growth. Every deployment and rollback writes permanent production events that end up in backups. The smoke runner's production identity is also undefined (see ADV-3).

Consequence: Synthetic records appear in production reporting and tenant data, and the smoke identity has broader production write access than it needs.

Fix: Require a dedicated synthetic tenant or partition, marked and excluded from business views. The smoke identity is authorized only within it.

**[Adversarial ADV-29]** — Personal-data obligations are an unstated assumption (§ NFR-1 ("preserve … event history"); FR-9 (retention); general)

Problem: The MVP includes Parties, and production (`tache.ai`) will hold business records in immutable event history, with 30-day backup retention. The PRD never classifies production data or says whether erasure or retention obligations apply.

Consequence: The architecture spine fixed immutable history and backup retention without considering erasure. If obligations apply, retrofitting them into event-sourced history and backup chains is expensive.

Fix: Add a data-classification statement for production. If personal data is in scope, add a requirement, or an explicit deferral with an owner, covering erasure in event history and backups.

**[Adversarial ADV-30]** — Production-user declaration is unaudited, and the Keycloak admin plane is shared (FR-11; § NFR-3 (second paragraph); addendum § Hosted architecture questions)

Problem: Declaration is the only control on production access, yet "production-user administration process are architecture choices". The PRD does not say who may declare users, require an audit record, periodic review, or revocation latency, or cover staging identity administrators. In one shared Keycloak, a staging-scoped administrator may be able to grant production roles, and NFR-3 tests only staging *users* and *application credentials*.

Consequence: Production access grows without review, and staging administration becomes a path to production authorization that no check covers.

Fix: Require that declarations are made by named approvers, are auditable, and take effect on revocation within a stated time. Require that staging identity administrators cannot modify production authorization, and add a negative test to SM-4.

**[Adversarial ADV-31]** — Silent scope in the addendum: Works and Agents (addendum § Existing implementation context; PRD § Confirmed MVP scope)

Problem: "The source addendum identifies Works as the rollback composition until its migration parity gate and assigns Agents wiring and live evidence to an existing Agents story." Neither Works nor Agents is in the MVP set, but the existing AppHost composes Works, and the repository history shows an Agents host being scaffolded. The PRD does not say whether Platform must keep these compositions working, or whether they are subject to FR-6 through FR-9.

Consequence: An implementer either deletes the Works preview and breaks a declared rollback path, or carries modules outside the scope through the gate, backups, and access rules with no requirements.

Fix: State in § MVP non-goals or § Confirmed MVP scope whether the Works and Agents compositions are maintained, frozen, or removed. If maintained, state which FRs apply to them.

**[Adversarial ADV-32]** — The hosted network exposure model and "supported interfaces" are undefined (FR-10 (intro and first consequence); § Confirmed MVP scope)

Problem: "The MVP module set is accessible in both environments through its supported interfaces", published under public domains (`hexalith.com`, `tache.ai`) on a private RFC1918 address. The PRD does not say whether the environments are internet-exposed or reachable only over the LAN or VPN, and does not list which interfaces each module supports.

Consequence: The security posture (exposure of Keycloak, TLS, rate limiting) and the scope of SM-4 acceptance are left to the implementer.

Fix: State the exposure model. Require each module to declare its supported interfaces at enrollment, with a missing declaration treated as a failure.

**[Adversarial ADV-33]** — Preserving credential rotations conflicts with restoring versioned configuration (§ NFR-1 (first paragraph); FR-8 (first consequence); Glossary "Recovery point")

Problem: Rollback must preserve "credential rotations" while restoring "compatible versioned configuration". A recovery point includes "configuration needed to restore service". The PRD does not define the boundary between configuration and secrets.

Consequence: Rollback reverts secret references to rotated-out credentials, or a disaster-recovery restore brings back credentials that were revoked after the recovery point.

Fix: State that secrets are excluded from versioned configuration and are never restored by rollback. Disaster-recovery restores must reapply current credentials and revocations before reopening service.

**[Consistency CON-1]** — FR-12 section intro and McpCli glossary keep the pre-alignment "all commands and queries" wording (PRD § Module operations through McpCli (intro), FR-12 body, Glossary "McpCli"  ↔  FR-12 second sentence; spine AD-11/AD-14; spec glossary "McpCli")

Check: alignment-edit / memlog

Evidence: Intro: "McpCli provides CLI and MCP access to the commands and queries defined by each enabled module in local Aspire, staging, and production environments." Glossary: "McpCli: the CLI and MCP access component that exposes commands and queries defined by enabled modules…". FR-12: "A user can discover and invoke the commands and queries published by enabled modules … In the MVP, McpCli executes only operations that their owning module declares eligible for agent use". These restate the superseded memlog decision (line 52) with no qualifier. The spec glossary copied the stale wording ("the CLI and MCP tool that exposes enabled modules' commands and queries"), so the problem has already spread downstream. FR-12 also drops the spine's "step-up" category (AD-14: "operations not eligible for agent use, or requiring confirmation or step-up, are not executable through McpCli"). It also leaves discovery ambiguous: "discover and invoke" covers every published operation, while the spine separates "offline contract inspection" from "executable" (AD-11).

Classification: PRD-internal residue; spine is right on the step-up and discovery semantics.

Fix: In the PRD, add "agent-eligible" to the McpCli section intro and the glossary entry. In FR-12, say whether discovery lists operations that are not eligible (marked non-executable) or hides them, and add step-up to the list of refused categories. Mirror the glossary change in the spec's `glossary.md`.

**[Consistency CON-2]** — The agent-eligibility rule has no measure, no owner and no non-empty guard, so FR-12 acceptance can pass vacuously (§ PRD FR-12 TC 1, SM-1, SM-4, counter-metrics, Downstream table row 1  ↔  spec SPEC.md CAP-12 success, success-measures.md SM-1/SM-4; spine Owned work "Connected McpCli")

Check: alignment-edit / spec

Evidence: FR-12 adds a fail-closed rule ("An operation not declared eligible for agent use … is refused through McpCli"), but SM-1 only measures the positive case ("execute enabled modules' agent-eligible commands and queries") and SM-4 only covers environment isolation. No counter-metric covers a UI-only or confirmation-required operation that McpCli executed. Spec CAP-12 success claims "UI-only or confirmation-required operations are refused (SM-1, SM-4)", yet neither SM measures refusal. With a fail-closed default and nothing requiring any operation to be eligible, "enabled modules' agent-eligible commands" could be an empty set. FR-6, by contrast, requires "a non-empty critical-flow declaration". The spine's "prove … all required agent-eligible operations before FR-12 acceptance" never defines "required". Downstream row 1 assigns "public operations" to module developers but not their agent-eligibility declaration.

Classification: Gap in both PRD and spec. The spine's AD-14 per-public-client negative test is the correct evidence.

Fix: In the PRD, extend SM-1 or SM-4 with "and refusal of non-eligible, confirmation-required and step-up operations through McpCli (AD-14 negative test)". Consider a counter-metric: "zero non-eligible operations executed through McpCli". Say which operations each MVP module must declare eligible (or that each module declares at least one), and add "agent-use eligibility" to Downstream row 1. Update the spec's SM-1/SM-4 to match, or stop CAP-12 citing SMs that don't measure refusal.

**[Consistency CON-3]** — FR-2 says McpCli is already "declared directly" in the Platform workspace; the addendum still names a sibling path as canonical (PRD FR-2 body  ↔  PRD addendum § Existing implementation context, § McpCli context; spine Owned work "Source/package adoption", AD-4; spec acceptance-criteria CAP-2)

Check: alignment-edit

Evidence: FR-2: "EventStore, Memories, and McpCli are run and debugged from source in the Platform workspace, where they are declared directly." Addendum: "The inspected Platform repository declared sixteen references: …" (McpCli is not among them). The McpCli context says "The source identifies the sibling `../mcpcli` repository as McpCli's canonical location; it was absent from Platform's inspected root submodule declarations." Spine AD-4 allows "no ancestor, sibling, recursive, nested-Platform or package fallback" and lists "canonical McpCli enrollment" as owned work. Spec CAP-2 AC correctly says "McpCli needs canonical McpCli enrollment in Platform's references first."

Classification: The spec is right. The PRD states a target state as if it were current.

Fix: In the PRD, change FR-2 to "…where they are, or for McpCli must become, direct `references/` declarations". Update the addendum McpCli context to separate the canonical *repository* from how it is resolved: McpCli must be added as a direct Platform reference, and the sibling path is not a resolution path. Extend Downstream row 3 ("McpCli enrollment…") to cover the Platform `references/` declaration.

**[Consistency CON-4]** — After the alignment, nothing says how EventStore, Memories and McpCli get minimum environments and Platform integration tests (§ PRD FR-3, FR-4 TC 2, SM-3, section intro "Each module developer … supplies a module configuration"  ↔  spine AD-4, AD-10; spec SPEC.md Open Questions)

Check: alignment-edit / spine / spec

Evidence: The alignment edit scoped only *debugging*. FR-2: "EventStore, Memories, and McpCli are run and debugged from source in the Platform workspace". FR-3 and FR-4 still assume each module has its own configuration and workspace: FR-4 says "Local integration-test environments run through Aspire using the module configuration and the active source checkout", and SM-3 says "local Aspire and CI integration runs pass their module-defined checks". AD-10 says "EventStore.Testing(.Integration), module and McpCli tests consume its versioned environment descriptor and never start their own AppHost. A technical module's own-repository tests may start its AppHost … such runs are never Platform integration evidence". The spine also removes McpCli's Platform submodule. The spec's open question: "how do McpCli's CI integration tests get a Platform runner version? … AD-4's version check assumes a submodule." The same question applies to EventStore and Memories, which have no Platform submodule under Option A: which repository runs their Platform integration evidence, and where does their module configuration live?

Classification: (c) genuine conflict needing a product decision (with an architecture follow-up)

Fix: In the PRD, say for FR-3, FR-4 and SM-3 where EventStore, Memories and McpCli minimum environments and integration runs execute: the Platform workspace/CI, or their own repositories through a pinned Platform tool. In the spine, answer the spec's open question and extend it to cover EventStore and Memories. In the spec, widen the open question beyond McpCli.

**[Consistency CON-6]** — The recovery deputy has no requirement, notification path, authority or downstream owner, and every operative recovery rule is still Administrator-only (PRD Target users "Recovery deputy", Glossary; FR-8 TC 3, FR-9 body, SM preamble, Downstream table; addendum § Disaster recovery Option 2 and Selected approach  ↔  spine Design Paradigm, AD-6, AD-7, Diagnostics and notification, RRA Automatic recovery, Secrets)

Check: alignment-edit / spine

Evidence: The PRD adds "Recovery deputy: a named person who backs Administrator for recovery and for custody of recovery keys". Every operative clause, however, still names Administrator alone:

- FR-8: "notifies the recovery owner, Administrator, through GitHub"
- FR-9: "The recovery owner can restore production…"
- SM preamble: "Administrator owns recovery evidence"
- addendum: "After failure, Administrator follows a short restore procedure…"
- addendum: "GitHub notifications and a single recovery owner do not establish round-the-clock response coverage"

The spine's operative rules are Administrator-only too:

- "GitHub issues assigned to Administrator are the single accepted notification path"
- "Deployment workflows live in a private operations repository writable only by Administrator"
- "only an authenticated Administrator record … clears" the promotion stop
- "production realm changes are Administrator-only"

The spine gives the deputy only one concrete role: "Administrator and the recovery deputy are the unseal and recovery-key custodians." If Administrator is unavailable, the deputy is not notified and cannot run recovery workflows, so the "backs Administrator for recovery" role has no effect on RTO response. Naming the deputy is a spine Deferred item ("Administrator MFA, break-glass and recovery deputy") but has no PRD downstream row.

Classification: (c) genuine conflict needing a product decision about the deputy's scope

Fix: In the PRD, decide whether the deputy only holds keys or can also receive notifications, run the DR procedure and clear the promotion stop. State that in the Target users entry and in FR-8/FR-9. Add a Downstream row: "Name the recovery deputy and provision deputy access | Administrator | Before hosted readiness". If the deputy's scope is broadened, update the spine's notification path, operations-repository access (this triggers AD-7's "second person" GitHub Team clause) and promotion-stop authority to match.

**[Consistency CON-7]** — The Administrator-approved release mode and the G3 delay on automatic promotion are not in the PRD (§ PRD Vision, FR-6, NFR-1, SM-5  ↔  spine RRA "Release modes", "Production entry gates"; spec SPEC.md G3 constraint, spec .memlog A4)

Check: spine / memlog / brief

Evidence: PRD Vision: "A release must pass end-to-end tests … before automatic production promotion". The brief: "After a release passes staging checks, Platform automatically deploys it to production." Spine: "Administrator-approved replaces only those two triggers with an authenticated Administrator record; it serves pre-G3 releases, SM-5 fault rehearsals on a staged release before G2, and incompatible releases". Spec: "Before G3, every production release uses the Administrator-approved mode." Spec memlog A4: "GitHub issues to Administrator, the Administrator-approved release mode and the recovery deputy are accepted as elaborations of the PRD". The PRD memlog (entry 71) records only the deputy from A4.

Classification: (b) The PRD should be updated to match an accepted decision.

Fix: In the PRD, add a sentence to FR-6 or NFR-1: until automatic promotion is enabled (SM-5), and for incompatible releases, production releases need an explicit Administrator approval. The staging gate, verification and one-attempt recovery still apply. Append a memlog entry recording all of A4 (approved mode and GitHub issues as the notification mechanism) and the A2 mapping ("before production use" = G2, "before enabling automatic production promotion" = G3).

**[Consistency CON-8]** — A 4-hour RTO that includes operator response conflicts with having no round-the-clock response; the spine reads it two ways (PRD NFR-2 table and closing paragraph, FR-9 TC 7; addendum § Selected approach  ↔  spine RRA "Detection and response", "Disaster recovery evidence"; spec CAP-9 AC)

Check: spine

Evidence: PRD: RTO "At most four hours | Service outage to verified restoration, including detection, operator response, replacement capacity…". The addendum adds that "it is not a four-hour stopwatch started after those prerequisites happen to become available". But the PRD also says "The targets do not establish … round-the-clock response guarantee." The spine's Detection row says "The recovery procedure declares response arrangements, such as staffed hours and a maximum acknowledgement time; the RTO is assessed within them", which can be read as pausing the clock outside staffed hours. Its DR evidence row "applies the declared worst-case response" and requires "outage-to-verified-service RTO at most four hours", which is the strict reading. The spec inherits both readings: "RTO ≤ 4 h … including … operator response" and "no 24/7 response commitment".

Classification: (c) genuine conflict needing a product decision

Fix: In the PRD (NFR-2), state one of two options. Either the 4-hour RTO holds for an outage at any time, which implies a maximum acknowledgement time that fits inside it, or it holds only for outages that start within the declared response hours and otherwise the target is revisited. Then change the spine's Detection row to match; "assessed within them" should not stay ambiguous.

### Low (16)

**[Rubric — Done-ness clarity]** — Undefined terms remain in several FRs (§ FR-2; § FR-4; § FR-8)

- FR-2: "only the components required by the selected scenario". The module configuration defines one list, so there is no scenario to select.
- FR-4: "compatible batch" and "useful diagnostic logs" (no minimum content, location, or retention).
- FR-8: "preserves diagnostics" (same gap).

Fix: Replace "selected scenario" with "the module configuration". State a minimum diagnostic set and a retention period, or assign them to the downstream diagnostics row explicitly.

**[Rubric — Scope honesty]** — The fate of existing hosting is not stated (addendum § Existing implementation context: Works preview as "the rollback composition until its migration parity gate"; the PRD is silent)

- The PRD never says whether the MVP keeps, replaces, or ignores the root Works preview. Works is outside the MVP set.
- It also never says whether MVP modules keep their own AppHosts.

Fix: Add one scope line or non-goal, for example: "The existing Works preview remains until its parity gate; MVP modules' own AppHosts are neither required nor removed by this PRD".

**[Rubric — Downstream usability]** — Several terms drift (§ FR-6; § FR-7; § Glossary)

- "Submodule" and "module" are used interchangeably (§ FR-6, § FR-7, § Glossary "Critical business flow", "Smoke test"), even though McpCli is not a Git submodule of Platform.
- The Glossary "Module workspace" entry was not narrowed to match FR-2's "domain-module workspace".
- The "Domain module" entry defines the term only by its dependencies and has no member list. For Tenants, "at least EventStore, Tenants, and Memories in addition to the module being worked on" is self-referential.
- "Administrator" appears in the Glossary only inside "Recovery owner".

Fix: Use "module" consistently. List the domain modules in the Glossary entry. Add an "Administrator" entry.

**[Adversarial ADV-34]** — The mandatory baseline contradicts "developer controls the list" (§ FR-3 (first and second consequences))

Problem: "The module developer controls the required server list" conflicts with "A domain module's minimum environment includes EventStore, Tenants, and Memories". The PRD does not say what happens when a configuration omits a baseline server.

Consequence: One implementation adds the server silently, another rejects the configuration, so developers see different behavior.

Fix: Specify one behavior. Rejecting the configuration with a clear error is the fail-closed choice.

**[Adversarial ADV-35]** — Success measures are one-shot and scoped to a single example (§ SM-1; SM-2; SM-3; FR-4 (first consequence))

Problem: SM-1 and SM-2 are validated "at MVP acceptance" only. SM-3 does not say which modules' integration runs must pass, and FR-4's only concrete example is Parties.

Consequence: A demonstration that covers only Parties satisfies SM-3, and the complete local environment degrades unnoticed after acceptance.

Fix: Make SM-3 apply to each domain module, and to EventStore, Memories, and McpCli from the Platform workspace. Add a scheduled CI job that starts the complete environment.

**[Adversarial ADV-36]** — The startup-deadline override and its start boundary are loose (§ FR-4 (fifth consequence); SM-C5)

Problem: The override needs only a "justified finite timeout", but the justification is not recorded or reviewed, and "finite" allows 24 hours. The PRD does not say whether the time from "the request to start the environment" includes build, restore, and image pulls.

Consequence: SM-C5 records overrides but cannot stop them from masking slow startups, and timings are not comparable between local and CI runs.

Fix: Store the justification with the override, set a maximum, and define which steps count toward the startup time.

**[Adversarial ADV-37]** — Behavior of the next run while a failed local environment is retained is undefined (FR-4 (eighth consequence); addendum § Selected behavior)

Problem: "A failed local test or startup attempt leaves surviving environment resources available for debugging." The PRD does not say what the next run does while that environment holds ports, containers, or data: fail, reuse it (which breaks FR-4 data isolation), or start in parallel.

Consequence: Retained environments collide with new runs, or tests silently reuse stale state.

Fix: Specify the next-run behavior, for example refusing to start and naming the retained environment and how to clean it up.

**[Adversarial ADV-38]** — Deployment disruption is unbounded and unstated (FR-7; FR-8; § Downstream decisions (closing paragraph))

Problem: The PRD does not say whether a normal deployment may interrupt service. A failed deployment can leave production degraded for up to 10 + 5 + 10 + 5 = 30 minutes before a human is involved, and this worst case is never stated as accepted.

Consequence: A recreate-style deployment that interrupts service on every release satisfies the PRD, and users see repeated outages that no measure records.

Fix: State whether deployments must avoid service interruption, and record the 30-minute worst case as an accepted limit (or tighten it).

**[Consistency CON-5]** — The "Module workspace" glossary entry is not scoped to domain modules (§ PRD Glossary "Module workspace"  ↔  PRD FR-2, SM-2, Glossary "Platform workspace"; spec glossary "Module workspace")

Check: alignment-edit

Evidence: PRD: "Module workspace: the individual module repository from which a developer edits, runs, tests, and debugs that module." FR-2 now limits module-workspace debugging to "a domain module (Tenants, Parties, Folders, Projects)". The spec glossary is already correct: "In the MVP, the domain modules are the ones debugged this way."

Classification: The spec is right.

Fix: In the PRD, add the spec's qualifier to the glossary entry.

**[Consistency CON-9]** — "Local uses project references" is absolute in the PRD but AD-4 uses packages for dependencies not declared at the root (§ PRD FR-2 TC 3 ("Local development and testing use project references and Debug assets")  ↔  spine AD-4; spec SPEC.md Constraints "Active-root dependencies")

Check: spine

Evidence: AD-4: "Root-declared Hexalith dependencies build from source in Debug; all others resolve as packages at the Builds catalog version". The spec restates the PRD: "Local builds use project references and Debug".

Classification: (a) spine refinement consistent with the PRD, though the PRD wording is too broad.

Fix: In the PRD, qualify FR-2 TC 3 as "root-declared Hexalith dependencies use project references and Debug locally; other dependencies resolve as packages". Apply the same qualifier to the spec constraint.

**[Consistency CON-10]** — The PRD's shared "database server" example is excluded by the adopted AD-8 (PRD § Confirmed MVP scope, § Hosted environments intro  ↔  spine AD-8; spine .memlog ("User selected option 1 … separate application data-service instances"); spec SPEC.md Constraints "Hosting")

Check: spine / spec

Evidence: PRD: "Staging and production may share supporting services or capacity, such as a database server, only while their application data and credentials remain isolated." Spine AD-8: "Staging and production each have an application namespace and a separate data namespace holding their own data-service instances, OpenBao instance…". The spec understates this: "Supporting infrastructure may be shared only if application data, credentials and namespaces stay per environment" (it omits data-service instances).

Classification: (a) The spine uses the PRD's permission more narrowly, and the spec is less precise than the spine.

Fix: Optional PRD note that architecture chose per-environment data-service instances, with shared Keycloak, the Dapr control plane, ingress and node capacity. Required spec fix: add "data-service and OpenBao instances" to the Hosting constraint.

**[Consistency CON-11]** — Two product-level decisions made in the spine are missing from the PRD (PRD FR-9 intro ("Each module identifies its authoritative state and recovery needs"), NFR-2  ↔  spine § Source Precedence; RRA "Lost window and external effects")

Check: spine

Evidence:

- Spine: "The Platform MVP envelope … governs the stricter infrastructure clauses of Folders … and Projects … by explicit user authority." A reader of the PRD would take module "recovery needs" (such as Folders' RPO of 5 minutes or less) as binding on Platform.
- Spine: "Non-Memories erasures, deletions and legal holds acknowledged after the recovery cut are an accepted RPO exception". This means erased or deleted data can come back after a restore. That is a privacy consequence, not just data loss, and NFR-2 says nothing about it.

Classification: (b) The PRD should be updated to match accepted decisions.

Fix: In the PRD, add a sentence to FR-9 saying module recovery needs are met within the NFR-2 envelope and stricter module infrastructure targets do not bind Platform in the MVP. Add a sentence to NFR-2 saying erasures, deletions and legal holds inside the lost window are an accepted exception that the DR report states.

**[Consistency CON-12]** — The PRD downstream table and addendum still list as open some items the final spine has decided (PRD Downstream table rows 2, 4, 5; addendum § Hosted architecture questions  ↔  spine AD-5, AD-6, AD-10, Hosted interfaces)

Check: spine

Evidence: The PRD lists "local Aspire lifecycle (including the successful local-run cleanup default)" as pending, while spine AD-10 says "Local success cleans". The addendum says "Realm, client, role/group, and account layouts have not been selected" and "It must also choose CI hosting", while AD-6 (separate realms, production group) and AD-5 (GitHub-hosted runners) have decided them.

Classification: (a) The spine resolves items the PRD handed downstream. This is not a conflict, but the PRD's statements have gone stale.

Fix: In the PRD, add one line to the Downstream section saying the architecture spine (final 2026-09-27) resolves the architecture rows, with a link, and stop describing the addendum's hosted questions as current. Don't restate the mechanisms.

**[Consistency CON-13]** — The spec moves SM-4 to "during G2 opening", and SPEC.md contradicts its own sequencing file (§ PRD SM-4 ("before production use")  ↔  spec success-measures.md SM-4 ("Completes during the G2 opening order"), SPEC.md G2 constraint ("SM-4 and SM-6 are its entry evidence"), sequencing.md G2 ("SM-6 before opening; SM-4 completes during the opening order"))

Check: spec

Evidence: In the spec's opening order, the first user is admitted and ingress opened before the SM-4 positive check runs on the live realm. SPEC.md still calls SM-4 G2 *entry* evidence.

Classification: The spec's sequencing.md is right (user-accepted A3). Its SPEC.md wording is inconsistent, and the PRD's "before production use" is broadly consistent with A3 if the first admitted test user doesn't count as production use.

Fix: In the spec, change SPEC.md's G2 constraint to "SM-6 is entry evidence; SM-4 completes in the opening order before anyone other than the first user is admitted". Optionally add a clause to PRD SM-4 matching that.

**[Consistency CON-14]** — Spec brownfield says EventStore and Memories should add a Platform submodule, contrary to Option A (spec brownfield.md § Module repositories  ↔  spec sequencing.md "Before SM-2"; PRD FR-2)

Check: spec

Evidence: brownfield.md: "None of EventStore, Memories, Parties, Tenants, Folders or Projects declares Platform as a submodule yet. Adding these direct declarations is owned work in the spine." sequencing.md: "Each domain module (Tenants, Parties, Folders, Projects) adds Platform as a direct submodule". PRD FR-2 puts EventStore and Memories in the Platform workspace.

Classification: The spec's sequencing.md and the PRD are right; brownfield.md is stale.

Fix: In the spec, limit brownfield.md's "adding these direct declarations" to the four domain modules, and keep the six-module list as an observation only.

**[Consistency CON-15]** — McpCli is required to "become ready" even though the spine treats it as a tool on the user's host (§ PRD FR-1 TC, SM-1 ("all seven MVP modules … become ready")  ↔  spine Design Paradigm ("Tool: McpCli"), flowchart ("McpCli on user or agent host"), AD-11; spec CAP-1 ("All seven modules report module-owned readiness"))

Check: alignment-edit / spine / spec

Evidence: Readiness is defined for services ("Each module defines readiness for its services"). McpCli is a stdio or CLI process with no hosted workload.

Classification: (a) The spine refines this and the PRD/spec never adjusted. Needs a definition.

Fix: In the PRD and spec, define what "ready" means for McpCli in SM-1 and CAP-1. For example: built through the AD-4 mapping, and its connected-mode metadata/digest check against the local gateway succeeds.

## Mechanical notes

- **IDs:** FR, NFR, SM and SM-C sequences are contiguous and unique, and every referenced ID exists. Addendum anchors `#module-owned-configuration-example`, `#mcpcli-context` and `#hosted-architecture-questions` resolve. Brief links resolve to `briefs/brief-platform-2026-09-27/brief.md` and its `addendum.md`.
- **Assumptions Index:** there are no inline `[ASSUMPTION]` tags and no index, so the roundtrip holds trivially.
- **Placement:** the UJ-substitution rationale sits at the end of § Downstream decisions (line 293). It would read better next to § Target users and jobs, where a reader looks for journeys.
- **Status after edits:** frontmatter still says `status: final` with an unchanged `updated` date after the post-finalization edits. The memlog's last entry says "No threshold, gate or other requirement changed". In fact FR-12 gained a new restriction and refusal consequence, and FR-2 changed where three modules are debugged. Downstream owners relying on "final" should know requirements changed after the finalize-pass review.
- **Regression check against the prior finalize-pass review** (checked after the judgments above were written):
  - No previously resolved finding has regressed. FR-6 still fails closed on missing or empty declarations, and FR-7 still validates declarations before updating production. SM-5 still requires a successful rollback plus separate failure-reporting evidence. FR-8 still uses the restored release's smoke suite. FR-9 still covers Keycloak and access restore checks. The successful local-run cleanup default is still assigned in the downstream table.
  - The prior recovery/access review accepted the "no round-the-clock coverage" disclosure as sufficient. This review disagrees and rates it high, because detection is counted in the RTO but no requirement produces it.
  - The Platform-workspace, agent-eligibility and deputy findings above are new. They come from post-finalization edits that the prior review never saw.

## Reviewer files

- `review-rubric.md`
- `review-adversarial-general.md`
- `review-consistency.md`
- Earlier finalize-pass files also exist in the workspace: `finalize-review-rubric.md`, `review-recovery-access.md`, `review-structure-*.md`, `review-prose-*.md`, `review-resolutions.md`. They were resolved during finalization and were not part of this run.
