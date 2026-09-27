# Adversarial Review — Hexalith Platform PRD

Reviewed 2026-09-27: `prd.md` and `addendum.md` (both `status: final`). Earlier review reports and `.memlog.md` were read only to avoid repeating findings that were already resolved. This is a requirements-contract review. It does not validate implementation or infrastructure.

## Summary

The PRD closes the obvious fail-open cases (empty flow sets, missing results, wrong-release evidence), but the unit those checks apply to, the "release", is never defined. The artifact that makes automatic rollback safe, previous-version compatibility evidence, has no gate, owner, or success measure. The isolation requirement covers people and application credentials but not the CI/CD, smoke-test, backup, and restore identities, which are the credentials most likely to cross environments. Recovery depends on one person reached through one channel, while the deputy added after finalization has no requirement behind them. Several success measures can go green on assumptions, empty catalogs, or self-declared inventories. As a build contract, the document lets a team pass every listed check while shipping a promotion gate that can be bypassed, a rollback that may not be safe, and DR evidence that is partly assumed.

## Findings

### ADV-1 — The gated unit ("release" / "included submodule") is undefined, so the gate can skip the modules a change breaks
- **Severity:** critical
- **Location:** § Staging validation and production promotion / FR-6, FR-7; § Downstream decisions ("release identity and promotion mechanism"); Glossary (no "release" or "submodule" entry)
- **Problem:** "The critical business flows used by the gate come from the submodules included in the release" and FR-7 validates declarations "for every included submodule". The PRD never says what a release is: a composite of all seven modules at pinned versions, or a per-module package. It also never says whether "included" means *deployed* or *changed*. "Submodule" is a Git term, and the addendum records that Platform declares **sixteen** references (Works, Agents, ChatBot, and others), not seven. "Release identity" is deferred to architecture, but the gate's meaning depends on it.
- **Consequence:** One reading gates an EventStore-only release on EventStore's flows alone, so a change that breaks Parties' critical flow is promoted. The opposite reading requires all sixteen Git submodules, including non-MVP ones with no flows, to supply declarations, which blocks promotion permanently. Both readings satisfy FR-6 as written.
- **Fix:** Define **Release** as the immutable set of artifacts (by digest) for every module deployed to the target environment, plus the versioned configuration. Require the gate to run the declared flows of **every enrolled module deployed in staging**, whether or not it changed. Replace "submodule" with "enrolled module" and define that term in the glossary.

### ADV-2 — NFR-1 compatibility is required but has no gate, no evidence owner, and no success measure
- **Severity:** critical
- **Location:** § NFR-1; FR-6; SM-5
- **Problem:** "Compatibility must be verified before automatic promotion. An incompatible change requires a separately planned release and recovery procedure. The architecture must define the compatibility evidence." FR-6's blocking conditions list declarations, E2E results, and release identity, but not compatibility evidence. Nobody is assigned to classify a change as incompatible. SM-5 checks only that data is preserved in a controlled rehearsal, not that each promoted release was verified compatible.
- **Consequence:** A release starts writing a new event format and fails at minute 3 of its verification window. FR-8 restores the previous code, which cannot read the events written in those 3 minutes. The restored release crashes, or silently misreads streams its smoke tests never touch, and FR-8 may still report success. Automatic rollback is safe only if this evidence exists, and nothing requires it to exist.
- **Fix:** Add an FR-6 consequence: "Each release carries per-module evidence that the recorded previous working release can read all schemas and event formats the new release writes. Missing, failed, or not-applicable-without-justification evidence blocks automatic promotion. A module declaring a change incompatible routes the release to the manual path (see ADV-5)." Extend SM-5 so one rehearsal uses a failing release that writes new-format data before rollback.

### ADV-3 — Isolation requirements omit the automation identities most likely to cross environments
- **Severity:** critical
- **Location:** § NFR-3; FR-10; FR-11; SM-4
- **Problem:** NFR-3 covers "staging application credentials" and "staging-only users". FR-11 covers users. None of the requirements mention the CI/CD deployer identity (which deploys *both* staging and production), the production smoke-test runner, backup jobs, restore tooling, or McpCli's hosted service identity. Every one of these needs production credentials.
- **Consequence:** A single GitHub workflow or runner holding both environments' deploy credentials satisfies every listed check. A staging or pull-request job, or a compromised dependency in it, can then deploy to or read production. SM-4 and SM-C2 stay green because they test only staging-only users and staging application credentials.
- **Fix:** Extend NFR-3: "Credentials that can deploy to, read, or write production (deployment, smoke-test, backup, restore, and service identities) are available only to the production promotion and recovery jobs. Staging, pull-request, and CI integration workflows cannot obtain them." Add matching negative checks to SM-4 and SM-C2.

### ADV-4 — Gate contents are self-certified and can be weakened inside the release being gated
- **Severity:** high
- **Location:** FR-6 (second consequence); FR-7 (second consequence); SM-C1
- **Problem:** The only floor is "a non-empty critical-flow declaration and required E2E checks for every declared flow" and "empty required check sets block the update". If the declaration is read from the release under test, that release can remove the flow that is failing, or remap it to a trivial test. The gate then sees a valid, non-empty, fully passing set. Nothing compares declarations with the previous working release, and nothing proves that an E2E result actually exercised the staged services beyond the words "using the deployed real services".
- **Consequence:** A regression is promoted with no failed, skipped, missing, or wrong-release evidence, so SM-C1 records zero violations. The fail-closed fix from the earlier review only blocks the case that is easy to see.
- **Fix:** Require the gate to diff flow and smoke declarations against the previous working release. Any removed or remapped flow or check requires an explicit, recorded approval by Administrator (or deputy) for that release. Require each E2E result to carry evidence of the release identity it observed from the staged services.

### ADV-5 — No manual, hotfix, or resume path is specified, yet several requirements assume one
- **Severity:** high
- **Location:** SM-5 ("before enabling automatic production promotion"); FR-8 ("Further automatic promotions stop pending intervention"); FR-7 ("A pre-existing unhealthy production environment stops the update for investigation"); NFR-1 ("separately planned release")
- **Problem:** The PRD implies a mode in which automatic promotion is not enabled, a pause that someone lifts, a release path for incompatible changes, and a way to fix an already-unhealthy production. None of these is defined: who may deploy manually, what evidence is still required, how the pause is cleared and by whom, or whether a manually deployed release becomes a "working release".
- **Consequence:** Implementers will add an unrecorded manual workflow or `kubectl` bypass with no gate, no verification, and no working-release record. The next automatic deployment then has no valid rollback target. When production is unhealthy, FR-7 blocks the automatic path that would deliver the fix. SM-C1 does not say whether manual promotions count.
- **Fix:** Add an FR for manual promotion. It should name who may perform it; state which FR-6 and FR-7 checks still apply or are waived with a recorded justification; require that the result is recorded as a working release only after FR-7 verification; name who lifts the FR-8 pause and what evidence they record; and apply SM-C1 to every production promotion, manual or automatic.

### ADV-6 — A "working release" can never be demoted, so a known-bad release becomes the rollback target
- **Severity:** high
- **Location:** FR-8 (last consequence); Glossary "Working release"; FR-7
- **Problem:** A release becomes "working" once it passes the 5-minute window. "Failures found after the verification window are handled as operational incidents, outside this automatic deployment-recovery policy." Nothing pauses automatic promotion during such an incident, and nothing lets anyone mark a release as not working.
- **Consequence:** R2 passes its window and then breaks at minute 20. R3 is automatically promoted on top of it during the incident and fails. FR-8 rolls back to R2 and verifies it with R2's smoke suite, which already missed the defect, then reports "successful recovery" to a broken release. SM-C3 counts no violation.
- **Fix:** State that an open production incident pauses automatic promotion. Let the recovery owner or deputy revoke a release's working status, with the revocation recorded. Rollback-target selection must skip revoked releases, and report "no valid target" rather than guess.

### ADV-7 — The RTO includes operator response, but no response coverage exists and SM-6 accepts assumed values
- **Severity:** high
- **Location:** § NFR-2; FR-9 (restore exercise consequence); SM-6; addendum § Selected approach and operational scope
- **Problem:** The RTO covers "detection, operator response, replacement capacity, restore, and validation", yet "The targets do not establish an uptime percentage or round-the-clock response guarantee". The addendum also concedes that the target only "Fits an internal MVP with one named recovery owner **if that owner can respond**". Restore exercises are scheduled, so detection and response time are zero. FR-9 allows "operator-response and capacity-provisioning **assumptions** included in the RTO assessment."
- **Consequence:** SM-6 is satisfied by a measured restore time plus an assumed response time. A failure at the start of a weekend or holiday with one responder cannot meet 4 hours, but every exercise reports that it does. This contradiction is built into NFR-2.
- **Fix:** Choose one of two options. Either scope the RTO clock explicitly (for example, from owner acknowledgement, or during stated coverage hours), or define response coverage across owner and deputy. Require SM-6 to report measured restore time and assumed response time as separate figures, and require at least one exercise per year to start from an unannounced alert.

### ADV-8 — The recovery deputy has no requirements, which leaves Administrator as a single point of human failure
- **Severity:** high
- **Location:** § Target users (Recovery deputy); Glossary; FR-8; FR-9; SM-5; § Success measures preamble
- **Problem:** The deputy "backs Administrator for recovery and for custody of recovery keys", but the deputy was added after finalization with "No threshold, gate or other requirement changed" (memlog). FR-8 notifies only "the recovery owner, Administrator". FR-9 notifies only "Administrator", and SM-5 verifies "GitHub delivery to Administrator" only. The deputy is never named, has no notification, access, key-custody, or exercise obligation, and does not appear in the downstream table. Administrator is also the product owner, the owner of recovery evidence, co-owner of production-user administration, and the person who accepts that evidence, so the same person produces the evidence and accepts it.
- **Consequence:** If Administrator is unavailable, nobody learns about a failed rollback, stale backups, or paused promotions, and the recovery keys sit with one person. The deputy exists only in the glossary.
- **Fix:** Add FR consequences. Every FR-8 and FR-9 notification goes to owner and deputy. The deputy holds independent access to recovery and decryption material, and this is verified in a restore exercise. The deputy performs at least one restore exercise per defined period. Name the deputy in the downstream table, and verify delivery to the deputy in SM-5 and SM-6.

### ADV-9 — Backup-freshness monitoring fails open and alerts only after the RPO is already breached
- **Severity:** high
- **Location:** FR-9 (fifth consequence); SM-6
- **Problem:** "Platform notifies Administrator … when a backup job fails or the newest usable recovery point is older than **one hour**." There are three gaps. (a) If the monitor or its GitHub integration stops, silence looks like health; there is no heartbeat or dead-man's switch. (b) The alert threshold equals the RPO, so the alert means the target has already been missed. (c) "Usable" is not defined between monthly restores: job exit status, a checksum, and a verified restore are all possible readings.
- **Consequence:** Backups stop silently, or the chain becomes unrestorable, and the first discovery happens at the next monthly exercise or during the disaster itself.
- **Fix:** Alert when the monitor's heartbeat is missing, detected from outside the primary environment. Add a warning threshold below the RPO (for example, 45 minutes). Define the minimum usability evidence for each recovery point (for example, an integrity check of the base-plus-incremental chain) and state that only points passing it count toward recovery-point age.

### ADV-10 — The off-site copy is optional by grammar
- **Severity:** high
- **Location:** FR-9 (fourth consequence); § NFR-2 (last paragraph); § MVP non-goals
- **Problem:** "protection against whole-site loss **additionally requires** an off-site copy" and "Whole-site loss requires an independent recovery location". These sentences state what whole-site protection would need, not that the MVP must provide it. "Outside the primary server/storage failure domain" can be met by a second disk or NAS on the same LAN as `192.168.1.30`.
- **Consequence:** A fire, theft, or site-wide power or flooding event destroys production and every backup, yet FR-9 and SM-6 were satisfied.
- **Fix:** State explicitly whether whole-site loss is in MVP scope. If it is, require an off-site copy and include the off-site copy's age in freshness monitoring. If it is not, move it to § MVP non-goals as an accepted risk signed by Administrator.

### ADV-11 — Infrastructure modules and McpCli cannot meet per-module flow and smoke obligations as written
- **Severity:** high
- **Location:** FR-6 (second consequence); FR-7 (first consequence); FR-1; FR-10; Glossary "Critical business flow"; addendum § McpCli context
- **Problem:** Every included submodule must supply "a non-empty critical-flow declaration", and a critical flow is "a business operation that its owning module designates". EventStore, Memories, and McpCli do not own business operations. McpCli is "the `hexalith` .NET tool" with "stdio MCP, deferred HTTP transport". Yet FR-1 requires McpCli to be "available" with readiness checks, and FR-10 requires the whole MVP set to be "accessible in both environments". The PRD never says whether hosted McpCli is a deployed service or a client-side tool that talks to hosted EventStore.
- **Consequence:** Either promotion is blocked indefinitely, or teams invent vacuous "flows" for infrastructure modules to meet the non-empty rule. SM-4's hosted McpCli acceptance depends on a hosting decision nobody has made.
- **Fix:** Classify modules as domain, infrastructure, or client tool, and state the flow, smoke, readiness, and backup obligations for each class. For example, infrastructure modules are covered by the domain flows that exercise them. State whether McpCli is deployed in staging and production or is only a client.

### ADV-12 — An empty McpCli catalog satisfies SM-1 and SM-4
- **Severity:** high
- **Location:** FR-12; SM-1; SM-4; addendum § McpCli context ("empty catalog with enrollment pending")
- **Problem:** "McpCli executes only operations that their owning module declares eligible for agent use", and SM-1 requires demonstrations that "execute enabled modules' agent-eligible commands and queries". If no module declares any operation eligible, which is the default and matches the observed empty catalog, the positive demonstrations are vacuously true. The fail-closed rule applied to FR-6 and FR-7 was not applied here. Only the *refusal* consequence can actually be tested.
- **Consequence:** McpCli can ship refusing every operation while SM-1 and SM-4 pass.
- **Fix:** Require each enrolled domain module to publish at least one agent-eligible query and one command, or a recorded exemption. SM-1 and SM-4 must name the operations demonstrated for each module in each environment.

### ADV-13 — No go-live gate; "before production use" is undefined and appears six times
- **Severity:** high
- **Location:** NFR-2; FR-9; SM-4; SM-6; § Downstream decisions; FR-8 (first-deployment consequence)
- **Problem:** Readiness evidence is repeatedly required "before production use", but nothing defines when production use begins (first deployment, first real tenant, public DNS, first production user), who declares it, or that these preconditions are checked together. FR-8 already handles a first *deployment*, so production can exist before "production use".
- **Consequence:** Production accumulates real data after its first deployment while the restore exercise, isolation checks, and GitHub delivery are still pending. Each "before production use" condition can be skipped on its own.
- **Fix:** Add a Production readiness gate FR. It lists the required evidence (SM-4, SM-5, SM-6, notification delivery to owner and deputy, and the identified replacement capacity) and requires a recorded Administrator sign-off. Production user access and DNS are enabled only after the gate passes.

### ADV-14 — Isolation runs one way only; restores and debugging can put production data on staging-reachable infrastructure
- **Severity:** high
- **Location:** FR-10 (third consequence); § NFR-3; FR-9 (restore exercise consequence)
- **Problem:** FR-10 forbids copying "staging data or credentials into production", and NFR-3 addresses staging access to production. Nothing forbids copying production data *into* staging, for example for debugging. FR-9's "isolated restore exercise" does not say what it is isolated from, where it runs, who can reach it, or when it is destroyed. The shared database server permitted by § Confirmed MVP scope is an obvious target for such a restore.
- **Consequence:** A monthly restore places production business data on shared infrastructure reachable with staging credentials, where staging-only users can read it. SM-4 still passes because its negative checks target production endpoints only.
- **Fix:** Add to NFR-3: "Production data, including backups and restored copies, exists only in locations with production access controls. Restore-exercise environments inherit those controls and are destroyed after evidence capture. Production data is never copied to staging." Add a matching SM-4 check.

### ADV-15 — Shared infrastructure isolates access, not capacity or faults; staging can take production down
- **Severity:** high
- **Location:** § Confirmed MVP scope; § Vision; § NFR-3; FR-7 (failure triggers)
- **Problem:** Staging and production "may share supporting services or capacity, such as a database server, only while their application data and credentials remain isolated." There is no resource, quota, or fault-isolation requirement. The staging E2E gate, restore exercises, and possibly CI (see ADV-26) run on the same cluster.
- **Consequence:** A staging E2E run, a runaway staging workload, or a restore exercise saturates the shared database during a production verification window. The result is a production outage, a false FR-7 rollback, or both, with the cause attributed to the release. Maintenance on the shared database takes down both environments at once, so the staging gate is also unavailable during production recovery.
- **Fix:** Require resource limits or quotas that protect production from staging on shared services. State whether CI and restore exercises may run on the production cluster. Require FR-7 diagnostics to record the state of shared dependencies so a shared-infrastructure fault is not reported as a release failure.

### ADV-16 — Configuration, Platform, and shared-infrastructure changes bypass the gate
- **Severity:** medium
- **Location:** FR-6; FR-7; FR-10 (third consequence); addendum § Hosted architecture questions
- **Problem:** The gate applies to "a release". Nothing says that production-only configuration values, Keycloak client or role changes, ingress, Dapr component definitions, Platform's own manifests, or database-server upgrades are part of a release. Production configuration values are never exercised in staging, because the two environments intentionally differ.
- **Consequence:** The changes most likely to break production (configuration and infrastructure) skip FR-6 E2E, FR-7 verification, and FR-8 rollback.
- **Fix:** State that any change to production application configuration or Platform-managed manifests is a release subject to FR-6 through FR-8. Require a written procedure for shared-infrastructure changes, which affect both environments at once.

### ADV-17 — The enforcement point for agent eligibility and human confirmation is unspecified
- **Severity:** medium
- **Location:** FR-12 (intro and first consequence); FR-11 (first consequence); addendum § McpCli context
- **Problem:** Non-eligible operations are "refused through McpCli" and human-confirmation operations "remain available only through that UI". If the refusal is implemented in McpCli (a client-side .NET tool), an agent holding the user's token can call EventStore directly. FR-11 explicitly anticipates direct API access. The PRD also does not require recording whether a production command came from a human or an agent.
- **Consequence:** The human-confirmation safeguard becomes a client-side convenience that any direct call bypasses, and production audit cannot tell agent actions from human ones.
- **Fix:** State that eligibility and confirmation rules are enforced on the server side (EventStore or the module), and that production commands record the invoking principal and interface (UI, CLI, MCP, or API).

### ADV-18 — The cross-module recovery point and its age are undefined
- **Severity:** medium
- **Location:** § NFR-2 (RPO row); FR-9 (sixth consequence); Glossary "Recovery point"
- **Problem:** RPO is measured by "Age of the newest usable recovery point". With several stores backed up at different times, it is unclear which age counts. The glossary defines a recovery point as "a usable copy of authoritative application data", in the singular. FR-9 requires "data integrity across modules" but does not define a consistent set.
- **Consequence:** RPO is reported from the freshest store while the newest *consistent* restorable set is older. Alternatively, restores mix points in time (a Parties record references a Tenants record that the Tenants backup lacks).
- **Fix:** Define a recovery point as a cross-module consistent set, with RPO age equal to the age of its oldest member. Freshness monitoring (ADV-9) measures that value.

### ADV-19 — The backup inventory is self-declared and omits Platform-owned state
- **Severity:** medium
- **Location:** FR-9 (first consequence); § Downstream decisions (last row)
- **Problem:** Coverage is "identified by each deployed module". Platform's own state has no owner: working-release records, versioned configuration, gate and compatibility evidence, secrets, certificates, Dapr components, and Kubernetes objects. Nothing checks that module inventories are complete.
- **Consequence:** After disaster recovery, Platform cannot identify a previous working release, so FR-7 blocks every update. Alternatively, a store no module listed is lost, and the restore exercise still passes because smoke tests never touch it.
- **Fix:** Require Platform to inventory its own state. Require each restore exercise to reconcile the inventory against all stateful resources found in the cluster (volumes, databases, secrets) and fail on any that are unlisted.

### ADV-20 — Local, CI, and hosted environments have no common version or composition source
- **Severity:** medium
- **Location:** FR-2 (third consequence); FR-4 (third consequence); addendum § Workspace and build constraints; § Hosted architecture questions ("choose CI hosting")
- **Problem:** "Local development and testing use project references and Debug assets. CI/CD uses NuGet package references and Release assets." Nothing requires CI's NuGet versions to correspond to the submodule commits pinned under `references/`. Nothing requires the local Aspire, CI, and Kubernetes compositions to come from the same service list and configuration.
- **Consequence:** Local integration tests pass against EventStore commit X, CI tests against NuGet version Y, and staging deploys Z. The "integration acceptance" evidence describes none of the systems actually deployed.
- **Fix:** Define the authoritative dependency version for each release. Require CI and hosted package versions to match the pinned references, or record the mapping. Require all three compositions to come from one module-configuration source.

### ADV-21 — Module-root composition is undefined for servers with no direct declaration, and for embedded Platform version skew
- **Severity:** medium
- **Location:** FR-2 (second consequence); FR-3; addendum § Workspace and build constraints
- **Problem:** A module configuration may list any server ("any additional dependencies its developer declares"), but "Dependency initialization follows only the active root repository's direct declarations under `references/`; nested submodules remain uninitialized." Suppose Parties' configuration enables Folders without declaring it under `parties/references/`. Platform could fail, fetch a package or container, or initialize a nested submodule (which is prohibited). Separately, the Platform version embedded in a module may expect module versions that differ from the module's own pinned EventStore.
- **Consequence:** Implementations will differ, and resolving the problem by initializing nested submodules would violate the stated policy.
- **Fix:** State that a listed server with no direct declaration is a configuration error, or name the permitted fallback. Require a compatibility check between the embedded Platform version and the module's declared references.

### ADV-22 — GitHub as the only channel: possible exposure and unconfirmed receipt
- **Severity:** medium
- **Location:** FR-8 (third consequence); FR-9 (fifth consequence); SM-5 ("Verify GitHub delivery to Administrator"); addendum § GitHub notification integration
- **Problem:** Notifications carry release, environment, recovery status, and "where to find diagnostic evidence", and the repository visibility is not constrained. Delivery is verified once; there is no acknowledgement, re-notification, or escalation, and a GitHub outage is not considered.
- **Consequence:** Production diagnostics end up visible to anyone who can read the repository, and a delivered notification lands in a filtered inbox. Promotions stay paused, or a failed recovery goes unhandled, indefinitely.
- **Fix:** Post production notifications and diagnostics only to locations restricted to production-authorized people. Require acknowledgement of critical notifications, with re-notification and escalation to the deputy within a stated time.

### ADV-23 — Verification-window mechanics can be read two ways
- **Severity:** medium
- **Location:** FR-7 (fourth to sixth consequences); FR-8 (second consequence)
- **Problem:** Three points are open. (a) "unable to serve traffic for 60 continuous seconds" does not say whether this is measured by probes or by real requests, which matters for a low-traffic internal system. (b) "the latest required smoke-test results are passing at the end of the verification window" does not set a cadence, so a single run at the start of the window satisfies it. (c) "A required check without a completed passing verification result **at the deadline**" does not say whether the deadline is the 10-minute rollout deadline or the end of the window.
- **Consequence:** Two compliant implementations detect different failures. A suite run only at the start of the window misses a defect that appears at minute 3.
- **Fix:** Name the availability signal. Require smoke runs at least at the start and end of the window, or at a stated interval. Name the deadline.

### ADV-24 — Reruns, concurrency, and ordering of staging promotions are unspecified
- **Severity:** medium
- **Location:** FR-6 (fourth and sixth consequences); SM-C1
- **Problem:** "Any required E2E test that fails … prevents automatic promotion" does not say whether a rerun that later passes clears the failure, or whether SM-C1 ("zero releases promoted with failed … evidence") counts it. Nothing serializes staging deployment and E2E runs, and nothing prevents promoting an older release after a newer one.
- **Consequence:** Flaky tests get rerun until they pass. E2E for release A runs against a partially deployed release B. A late-finishing older release downgrades production.
- **Fix:** Define the rerun policy and how SM-C1 counts reruns. Serialize staging deployment and E2E per release. Forbid promoting a release older than the current working release.

### ADV-25 — "Zero" counter-metrics cannot be observed without retained, auditable records
- **Severity:** medium
- **Location:** § Success measures preamble; SM-C1–SM-C3
- **Problem:** "Record the release or source revision, environment, result, and supporting diagnostics with each demonstration" covers demonstrations, not every promotion or recovery decision. No retention, immutability, or periodic audit is required. SM-C2 is limited to "the staging-only negative checks".
- **Consequence:** "Zero bypassed validations" and "zero false recovery successes" mean "none noticed". A violation is found only by accident.
- **Fix:** Require an append-only record for every promotion and recovery (release ID, declaration digest, results, decision, actor), retained for a stated period. Compute SM-C1 and SM-C3 from that record on a stated schedule.

### ADV-26 — CI orphan cleanup and CI hosting location are unaddressed
- **Severity:** medium
- **Location:** FR-4 (tenth and eleventh consequences); SM-C4; addendum § Hosted architecture questions
- **Problem:** "CI automatically cleans up … after success, failure, or cancellation". A crashed runner, a killed job after timeout, or a lost runner fits none of these cases, and no orphan sweep is required. CI hosting is deferred, and nothing prevents it from running on the production cluster at `192.168.1.30`. SM-C4 counts only "cancelled/finished" runs.
- **Consequence:** Orphaned CI environments build up, possibly on the cluster that hosts production, and SM-C4 stays at zero.
- **Fix:** Require ownership labels with a time-to-live and a periodic orphan sweep. State whether CI may run on the production cluster.

### ADV-27 — Local identity and permissions are undefined
- **Severity:** medium
- **Location:** FR-12 (intro and third consequence); FR-1; SM-1; addendum § Hosted architecture questions
- **Problem:** McpCli access in local Aspire is "subject to the user's permissions in the selected environment". The only identity provider named is the shared Keycloak "on the Kubernetes installation", which also hosts production. The PRD does not say whether local environments use it, a local identity provider, or no authentication.
- **Consequence:** Either developer laptops depend on, and hold credentials for, the production-hosting identity server, or a local no-auth mode hides permission bugs that SM-1 cannot catch.
- **Fix:** State the local identity source and seed permissions. State that local environments never use production realms, clients, or credentials.

### ADV-28 — Production smoke writes have no containment
- **Severity:** medium
- **Location:** FR-7 (first consequence); addendum § Module responsibilities and production test data
- **Problem:** "necessary writes use dedicated synthetic data without real-user changes or external effects". Yet "Do not assume event records can simply be deleted". There is no dedicated synthetic tenant, no exclusion from business queries, and no bound on growth. Every deployment and rollback writes permanent production events that end up in backups. The smoke runner's production identity is also undefined (see ADV-3).
- **Consequence:** Synthetic records appear in production reporting and tenant data, and the smoke identity has broader production write access than it needs.
- **Fix:** Require a dedicated synthetic tenant or partition, marked and excluded from business views. The smoke identity is authorized only within it.

### ADV-29 — Personal-data obligations are an unstated assumption
- **Severity:** medium
- **Location:** § NFR-1 ("preserve … event history"); FR-9 (retention); general
- **Problem:** The MVP includes Parties, and production (`tache.ai`) will hold business records in immutable event history, with 30-day backup retention. The PRD never classifies production data or says whether erasure or retention obligations apply.
- **Consequence:** The architecture spine fixed immutable history and backup retention without considering erasure. If obligations apply, retrofitting them into event-sourced history and backup chains is expensive.
- **Fix:** Add a data-classification statement for production. If personal data is in scope, add a requirement, or an explicit deferral with an owner, covering erasure in event history and backups.

### ADV-30 — Production-user declaration is unaudited, and the Keycloak admin plane is shared
- **Severity:** medium
- **Location:** FR-11; § NFR-3 (second paragraph); addendum § Hosted architecture questions
- **Problem:** Declaration is the only control on production access, yet "production-user administration process are architecture choices". The PRD does not say who may declare users, require an audit record, periodic review, or revocation latency, or cover staging identity administrators. In one shared Keycloak, a staging-scoped administrator may be able to grant production roles, and NFR-3 tests only staging *users* and *application credentials*.
- **Consequence:** Production access grows without review, and staging administration becomes a path to production authorization that no check covers.
- **Fix:** Require that declarations are made by named approvers, are auditable, and take effect on revocation within a stated time. Require that staging identity administrators cannot modify production authorization, and add a negative test to SM-4.

### ADV-31 — Silent scope in the addendum: Works and Agents
- **Severity:** medium
- **Location:** addendum § Existing implementation context; PRD § Confirmed MVP scope
- **Problem:** "The source addendum identifies Works as the rollback composition until its migration parity gate and assigns Agents wiring and live evidence to an existing Agents story." Neither Works nor Agents is in the MVP set, but the existing AppHost composes Works, and the repository history shows an Agents host being scaffolded. The PRD does not say whether Platform must keep these compositions working, or whether they are subject to FR-6 through FR-9.
- **Consequence:** An implementer either deletes the Works preview and breaks a declared rollback path, or carries modules outside the scope through the gate, backups, and access rules with no requirements.
- **Fix:** State in § MVP non-goals or § Confirmed MVP scope whether the Works and Agents compositions are maintained, frozen, or removed. If maintained, state which FRs apply to them.

### ADV-32 — The hosted network exposure model and "supported interfaces" are undefined
- **Severity:** medium
- **Location:** FR-10 (intro and first consequence); § Confirmed MVP scope
- **Problem:** "The MVP module set is accessible in both environments through its supported interfaces", published under public domains (`hexalith.com`, `tache.ai`) on a private RFC1918 address. The PRD does not say whether the environments are internet-exposed or reachable only over the LAN or VPN, and does not list which interfaces each module supports.
- **Consequence:** The security posture (exposure of Keycloak, TLS, rate limiting) and the scope of SM-4 acceptance are left to the implementer.
- **Fix:** State the exposure model. Require each module to declare its supported interfaces at enrollment, with a missing declaration treated as a failure.

### ADV-33 — Preserving credential rotations conflicts with restoring versioned configuration
- **Severity:** medium
- **Location:** § NFR-1 (first paragraph); FR-8 (first consequence); Glossary "Recovery point"
- **Problem:** Rollback must preserve "credential rotations" while restoring "compatible versioned configuration". A recovery point includes "configuration needed to restore service". The PRD does not define the boundary between configuration and secrets.
- **Consequence:** Rollback reverts secret references to rotated-out credentials, or a disaster-recovery restore brings back credentials that were revoked after the recovery point.
- **Fix:** State that secrets are excluded from versioned configuration and are never restored by rollback. Disaster-recovery restores must reapply current credentials and revocations before reopening service.

### ADV-34 — The mandatory baseline contradicts "developer controls the list"
- **Severity:** low
- **Location:** FR-3 (first and second consequences)
- **Problem:** "The module developer controls the required server list" conflicts with "A domain module's minimum environment includes EventStore, Tenants, and Memories". The PRD does not say what happens when a configuration omits a baseline server.
- **Consequence:** One implementation adds the server silently, another rejects the configuration, so developers see different behavior.
- **Fix:** Specify one behavior. Rejecting the configuration with a clear error is the fail-closed choice.

### ADV-35 — Success measures are one-shot and scoped to a single example
- **Severity:** low
- **Location:** SM-1; SM-2; SM-3; FR-4 (first consequence)
- **Problem:** SM-1 and SM-2 are validated "at MVP acceptance" only. SM-3 does not say which modules' integration runs must pass, and FR-4's only concrete example is Parties.
- **Consequence:** A demonstration that covers only Parties satisfies SM-3, and the complete local environment degrades unnoticed after acceptance.
- **Fix:** Make SM-3 apply to each domain module, and to EventStore, Memories, and McpCli from the Platform workspace. Add a scheduled CI job that starts the complete environment.

### ADV-36 — The startup-deadline override and its start boundary are loose
- **Severity:** low
- **Location:** FR-4 (fifth consequence); SM-C5
- **Problem:** The override needs only a "justified finite timeout", but the justification is not recorded or reviewed, and "finite" allows 24 hours. The PRD does not say whether the time from "the request to start the environment" includes build, restore, and image pulls.
- **Consequence:** SM-C5 records overrides but cannot stop them from masking slow startups, and timings are not comparable between local and CI runs.
- **Fix:** Store the justification with the override, set a maximum, and define which steps count toward the startup time.

### ADV-37 — Behavior of the next run while a failed local environment is retained is undefined
- **Severity:** low
- **Location:** FR-4 (eighth consequence); addendum § Selected behavior
- **Problem:** "A failed local test or startup attempt leaves surviving environment resources available for debugging." The PRD does not say what the next run does while that environment holds ports, containers, or data: fail, reuse it (which breaks FR-4 data isolation), or start in parallel.
- **Consequence:** Retained environments collide with new runs, or tests silently reuse stale state.
- **Fix:** Specify the next-run behavior, for example refusing to start and naming the retained environment and how to clean it up.

### ADV-38 — Deployment disruption is unbounded and unstated
- **Severity:** low
- **Location:** FR-7; FR-8; § Downstream decisions (closing paragraph)
- **Problem:** The PRD does not say whether a normal deployment may interrupt service. A failed deployment can leave production degraded for up to 10 + 5 + 10 + 5 = 30 minutes before a human is involved, and this worst case is never stated as accepted.
- **Consequence:** A recreate-style deployment that interrupts service on every release satisfies the PRD, and users see repeated outages that no measure records.
- **Fix:** State whether deployments must avoid service interruption, and record the 30-minute worst case as an accepted limit (or tighten it).
