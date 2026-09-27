# Cross-Document Consistency Review — Hexalith Platform PRD

## Summary

The PRD is still a coherent source of truth after the post-final alignment edit. The three user decisions in memlog entry 71 (agent-eligible McpCli operations, domain-module versus Platform-workspace debugging, recovery deputy) are reflected in FR-2, FR-12, SM-1, SM-2, target users, glossary and the addendum McpCli context, and none of them contradicts a threshold, gate or fail-closed rule. The problems are incomplete propagation rather than contradictions. Some pre-alignment wording remains (the McpCli intro and glossary, the Module workspace glossary, and McpCli described as already "declared directly"). The recovery deputy and the agent-eligibility rule were added without any requirement, measure or owner behind them. The edit also leaves open how EventStore, Memories and McpCli get their minimum environments and integration tests. Separately, some user-accepted decisions recorded in the spine affect product behavior but never reached the PRD: the Administrator-approved release mode before G3, the Folders/Projects envelope override and the lost-window erasure exception. No finding is critical or high.

## Findings

### CON-1 — FR-12 section intro and McpCli glossary keep the pre-alignment "all commands and queries" wording
- **Severity:** medium
- **Check:** alignment-edit / memlog
- **Location:** PRD § Module operations through McpCli (intro), FR-12 body, Glossary "McpCli"  ↔  FR-12 second sentence; spine AD-11/AD-14; spec glossary "McpCli"
- **Evidence:** Intro: "McpCli provides CLI and MCP access to the commands and queries defined by each enabled module in local Aspire, staging, and production environments." Glossary: "McpCli: the CLI and MCP access component that exposes commands and queries defined by enabled modules…". FR-12: "A user can discover and invoke the commands and queries published by enabled modules … In the MVP, McpCli executes only operations that their owning module declares eligible for agent use". These restate the superseded memlog decision (line 52) with no qualifier. The spec glossary copied the stale wording ("the CLI and MCP tool that exposes enabled modules' commands and queries"), so the problem has already spread downstream. FR-12 also drops the spine's "step-up" category (AD-14: "operations not eligible for agent use, or requiring confirmation or step-up, are not executable through McpCli"). It also leaves discovery ambiguous: "discover and invoke" covers every published operation, while the spine separates "offline contract inspection" from "executable" (AD-11).
- **Classification:** PRD-internal residue; spine is right on the step-up and discovery semantics.
- **Fix:** In the PRD, add "agent-eligible" to the McpCli section intro and the glossary entry. In FR-12, say whether discovery lists operations that are not eligible (marked non-executable) or hides them, and add step-up to the list of refused categories. Mirror the glossary change in the spec's `glossary.md`.

### CON-2 — The agent-eligibility rule has no measure, no owner and no non-empty guard, so FR-12 acceptance can pass vacuously
- **Severity:** medium
- **Check:** alignment-edit / spec
- **Location:** PRD FR-12 TC 1, SM-1, SM-4, counter-metrics, Downstream table row 1  ↔  spec SPEC.md CAP-12 success, success-measures.md SM-1/SM-4; spine Owned work "Connected McpCli"
- **Evidence:** FR-12 adds a fail-closed rule ("An operation not declared eligible for agent use … is refused through McpCli"), but SM-1 only measures the positive case ("execute enabled modules' agent-eligible commands and queries") and SM-4 only covers environment isolation. No counter-metric covers a UI-only or confirmation-required operation that McpCli executed. Spec CAP-12 success claims "UI-only or confirmation-required operations are refused (SM-1, SM-4)", yet neither SM measures refusal. With a fail-closed default and nothing requiring any operation to be eligible, "enabled modules' agent-eligible commands" could be an empty set. FR-6, by contrast, requires "a non-empty critical-flow declaration". The spine's "prove … all required agent-eligible operations before FR-12 acceptance" never defines "required". Downstream row 1 assigns "public operations" to module developers but not their agent-eligibility declaration.
- **Classification:** Gap in both PRD and spec. The spine's AD-14 per-public-client negative test is the correct evidence.
- **Fix:** In the PRD, extend SM-1 or SM-4 with "and refusal of non-eligible, confirmation-required and step-up operations through McpCli (AD-14 negative test)". Consider a counter-metric: "zero non-eligible operations executed through McpCli". Say which operations each MVP module must declare eligible (or that each module declares at least one), and add "agent-use eligibility" to Downstream row 1. Update the spec's SM-1/SM-4 to match, or stop CAP-12 citing SMs that don't measure refusal.

### CON-3 — FR-2 says McpCli is already "declared directly" in the Platform workspace; the addendum still names a sibling path as canonical
- **Severity:** medium
- **Check:** alignment-edit
- **Location:** PRD FR-2 body  ↔  PRD addendum § Existing implementation context, § McpCli context; spine Owned work "Source/package adoption", AD-4; spec acceptance-criteria CAP-2
- **Evidence:** FR-2: "EventStore, Memories, and McpCli are run and debugged from source in the Platform workspace, where they are declared directly." Addendum: "The inspected Platform repository declared sixteen references: …" (McpCli is not among them). The McpCli context says "The source identifies the sibling `../mcpcli` repository as McpCli's canonical location; it was absent from Platform's inspected root submodule declarations." Spine AD-4 allows "no ancestor, sibling, recursive, nested-Platform or package fallback" and lists "canonical McpCli enrollment" as owned work. Spec CAP-2 AC correctly says "McpCli needs canonical McpCli enrollment in Platform's references first."
- **Classification:** The spec is right. The PRD states a target state as if it were current.
- **Fix:** In the PRD, change FR-2 to "…where they are, or for McpCli must become, direct `references/` declarations". Update the addendum McpCli context to separate the canonical *repository* from how it is resolved: McpCli must be added as a direct Platform reference, and the sibling path is not a resolution path. Extend Downstream row 3 ("McpCli enrollment…") to cover the Platform `references/` declaration.

### CON-4 — After the alignment, nothing says how EventStore, Memories and McpCli get minimum environments and Platform integration tests
- **Severity:** medium
- **Check:** alignment-edit / spine / spec
- **Location:** PRD FR-3, FR-4 TC 2, SM-3, section intro "Each module developer … supplies a module configuration"  ↔  spine AD-4, AD-10; spec SPEC.md Open Questions
- **Evidence:** The alignment edit scoped only *debugging*. FR-2: "EventStore, Memories, and McpCli are run and debugged from source in the Platform workspace". FR-3 and FR-4 still assume each module has its own configuration and workspace: FR-4 says "Local integration-test environments run through Aspire using the module configuration and the active source checkout", and SM-3 says "local Aspire and CI integration runs pass their module-defined checks". AD-10 says "EventStore.Testing(.Integration), module and McpCli tests consume its versioned environment descriptor and never start their own AppHost. A technical module's own-repository tests may start its AppHost … such runs are never Platform integration evidence". The spine also removes McpCli's Platform submodule. The spec's open question: "how do McpCli's CI integration tests get a Platform runner version? … AD-4's version check assumes a submodule." The same question applies to EventStore and Memories, which have no Platform submodule under Option A: which repository runs their Platform integration evidence, and where does their module configuration live?
- **Classification:** (c) genuine conflict needing a product decision (with an architecture follow-up)
- **Fix:** In the PRD, say for FR-3, FR-4 and SM-3 where EventStore, Memories and McpCli minimum environments and integration runs execute: the Platform workspace/CI, or their own repositories through a pinned Platform tool. In the spine, answer the spec's open question and extend it to cover EventStore and Memories. In the spec, widen the open question beyond McpCli.

### CON-5 — The "Module workspace" glossary entry is not scoped to domain modules
- **Severity:** low
- **Check:** alignment-edit
- **Location:** PRD Glossary "Module workspace"  ↔  PRD FR-2, SM-2, Glossary "Platform workspace"; spec glossary "Module workspace"
- **Evidence:** PRD: "Module workspace: the individual module repository from which a developer edits, runs, tests, and debugs that module." FR-2 now limits module-workspace debugging to "a domain module (Tenants, Parties, Folders, Projects)". The spec glossary is already correct: "In the MVP, the domain modules are the ones debugged this way."
- **Classification:** The spec is right.
- **Fix:** In the PRD, add the spec's qualifier to the glossary entry.

### CON-6 — The recovery deputy has no requirement, notification path, authority or downstream owner, and every operative recovery rule is still Administrator-only
- **Severity:** medium
- **Check:** alignment-edit / spine
- **Location:** PRD Target users "Recovery deputy", Glossary; FR-8 TC 3, FR-9 body, SM preamble, Downstream table; addendum § Disaster recovery Option 2 and Selected approach  ↔  spine Design Paradigm, AD-6, AD-7, Diagnostics and notification, RRA Automatic recovery, Secrets
- **Evidence:** The PRD adds "Recovery deputy: a named person who backs Administrator for recovery and for custody of recovery keys". Every operative clause, however, still names Administrator alone:
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
- **Classification:** (c) genuine conflict needing a product decision about the deputy's scope
- **Fix:** In the PRD, decide whether the deputy only holds keys or can also receive notifications, run the DR procedure and clear the promotion stop. State that in the Target users entry and in FR-8/FR-9. Add a Downstream row: "Name the recovery deputy and provision deputy access | Administrator | Before hosted readiness". If the deputy's scope is broadened, update the spine's notification path, operations-repository access (this triggers AD-7's "second person" GitHub Team clause) and promotion-stop authority to match.

### CON-7 — The Administrator-approved release mode and the G3 delay on automatic promotion are not in the PRD
- **Severity:** medium
- **Check:** spine / memlog / brief
- **Location:** PRD Vision, FR-6, NFR-1, SM-5  ↔  spine RRA "Release modes", "Production entry gates"; spec SPEC.md G3 constraint, spec .memlog A4
- **Evidence:** PRD Vision: "A release must pass end-to-end tests … before automatic production promotion". The brief: "After a release passes staging checks, Platform automatically deploys it to production." Spine: "Administrator-approved replaces only those two triggers with an authenticated Administrator record; it serves pre-G3 releases, SM-5 fault rehearsals on a staged release before G2, and incompatible releases". Spec: "Before G3, every production release uses the Administrator-approved mode." Spec memlog A4: "GitHub issues to Administrator, the Administrator-approved release mode and the recovery deputy are accepted as elaborations of the PRD". The PRD memlog (entry 71) records only the deputy from A4.
- **Classification:** (b) The PRD should be updated to match an accepted decision.
- **Fix:** In the PRD, add a sentence to FR-6 or NFR-1: until automatic promotion is enabled (SM-5), and for incompatible releases, production releases need an explicit Administrator approval. The staging gate, verification and one-attempt recovery still apply. Append a memlog entry recording all of A4 (approved mode and GitHub issues as the notification mechanism) and the A2 mapping ("before production use" = G2, "before enabling automatic production promotion" = G3).

### CON-8 — A 4-hour RTO that includes operator response conflicts with having no round-the-clock response; the spine reads it two ways
- **Severity:** medium
- **Check:** spine
- **Location:** PRD NFR-2 table and closing paragraph, FR-9 TC 7; addendum § Selected approach  ↔  spine RRA "Detection and response", "Disaster recovery evidence"; spec CAP-9 AC
- **Evidence:** PRD: RTO "At most four hours | Service outage to verified restoration, including detection, operator response, replacement capacity…". The addendum adds that "it is not a four-hour stopwatch started after those prerequisites happen to become available". But the PRD also says "The targets do not establish … round-the-clock response guarantee." The spine's Detection row says "The recovery procedure declares response arrangements, such as staffed hours and a maximum acknowledgement time; the RTO is assessed within them", which can be read as pausing the clock outside staffed hours. Its DR evidence row "applies the declared worst-case response" and requires "outage-to-verified-service RTO at most four hours", which is the strict reading. The spec inherits both readings: "RTO ≤ 4 h … including … operator response" and "no 24/7 response commitment".
- **Classification:** (c) genuine conflict needing a product decision
- **Fix:** In the PRD (NFR-2), state one of two options. Either the 4-hour RTO holds for an outage at any time, which implies a maximum acknowledgement time that fits inside it, or it holds only for outages that start within the declared response hours and otherwise the target is revisited. Then change the spine's Detection row to match; "assessed within them" should not stay ambiguous.

### CON-9 — "Local uses project references" is absolute in the PRD but AD-4 uses packages for dependencies not declared at the root
- **Severity:** low
- **Check:** spine
- **Location:** PRD FR-2 TC 3 ("Local development and testing use project references and Debug assets")  ↔  spine AD-4; spec SPEC.md Constraints "Active-root dependencies"
- **Evidence:** AD-4: "Root-declared Hexalith dependencies build from source in Debug; all others resolve as packages at the Builds catalog version". The spec restates the PRD: "Local builds use project references and Debug".
- **Classification:** (a) spine refinement consistent with the PRD, though the PRD wording is too broad.
- **Fix:** In the PRD, qualify FR-2 TC 3 as "root-declared Hexalith dependencies use project references and Debug locally; other dependencies resolve as packages". Apply the same qualifier to the spec constraint.

### CON-10 — The PRD's shared "database server" example is excluded by the adopted AD-8
- **Severity:** low
- **Check:** spine / spec
- **Location:** PRD § Confirmed MVP scope, § Hosted environments intro  ↔  spine AD-8; spine .memlog ("User selected option 1 … separate application data-service instances"); spec SPEC.md Constraints "Hosting"
- **Evidence:** PRD: "Staging and production may share supporting services or capacity, such as a database server, only while their application data and credentials remain isolated." Spine AD-8: "Staging and production each have an application namespace and a separate data namespace holding their own data-service instances, OpenBao instance…". The spec understates this: "Supporting infrastructure may be shared only if application data, credentials and namespaces stay per environment" (it omits data-service instances).
- **Classification:** (a) The spine uses the PRD's permission more narrowly, and the spec is less precise than the spine.
- **Fix:** Optional PRD note that architecture chose per-environment data-service instances, with shared Keycloak, the Dapr control plane, ingress and node capacity. Required spec fix: add "data-service and OpenBao instances" to the Hosting constraint.

### CON-11 — Two product-level decisions made in the spine are missing from the PRD
- **Severity:** low
- **Check:** spine
- **Location:** PRD FR-9 intro ("Each module identifies its authoritative state and recovery needs"), NFR-2  ↔  spine § Source Precedence; RRA "Lost window and external effects"
- **Evidence:**
  - Spine: "The Platform MVP envelope … governs the stricter infrastructure clauses of Folders … and Projects … by explicit user authority." A reader of the PRD would take module "recovery needs" (such as Folders' RPO of 5 minutes or less) as binding on Platform.
  - Spine: "Non-Memories erasures, deletions and legal holds acknowledged after the recovery cut are an accepted RPO exception". This means erased or deleted data can come back after a restore. That is a privacy consequence, not just data loss, and NFR-2 says nothing about it.
- **Classification:** (b) The PRD should be updated to match accepted decisions.
- **Fix:** In the PRD, add a sentence to FR-9 saying module recovery needs are met within the NFR-2 envelope and stricter module infrastructure targets do not bind Platform in the MVP. Add a sentence to NFR-2 saying erasures, deletions and legal holds inside the lost window are an accepted exception that the DR report states.

### CON-12 — The PRD downstream table and addendum still list as open some items the final spine has decided
- **Severity:** low
- **Check:** spine
- **Location:** PRD Downstream table rows 2, 4, 5; addendum § Hosted architecture questions  ↔  spine AD-5, AD-6, AD-10, Hosted interfaces
- **Evidence:** The PRD lists "local Aspire lifecycle (including the successful local-run cleanup default)" as pending, while spine AD-10 says "Local success cleans". The addendum says "Realm, client, role/group, and account layouts have not been selected" and "It must also choose CI hosting", while AD-6 (separate realms, production group) and AD-5 (GitHub-hosted runners) have decided them.
- **Classification:** (a) The spine resolves items the PRD handed downstream. This is not a conflict, but the PRD's statements have gone stale.
- **Fix:** In the PRD, add one line to the Downstream section saying the architecture spine (final 2026-09-27) resolves the architecture rows, with a link, and stop describing the addendum's hosted questions as current. Don't restate the mechanisms.

### CON-13 — The spec moves SM-4 to "during G2 opening", and SPEC.md contradicts its own sequencing file
- **Severity:** low
- **Check:** spec
- **Location:** PRD SM-4 ("before production use")  ↔  spec success-measures.md SM-4 ("Completes during the G2 opening order"), SPEC.md G2 constraint ("SM-4 and SM-6 are its entry evidence"), sequencing.md G2 ("SM-6 before opening; SM-4 completes during the opening order")
- **Evidence:** In the spec's opening order, the first user is admitted and ingress opened before the SM-4 positive check runs on the live realm. SPEC.md still calls SM-4 G2 *entry* evidence.
- **Classification:** The spec's sequencing.md is right (user-accepted A3). Its SPEC.md wording is inconsistent, and the PRD's "before production use" is broadly consistent with A3 if the first admitted test user doesn't count as production use.
- **Fix:** In the spec, change SPEC.md's G2 constraint to "SM-6 is entry evidence; SM-4 completes in the opening order before anyone other than the first user is admitted". Optionally add a clause to PRD SM-4 matching that.

### CON-14 — Spec brownfield says EventStore and Memories should add a Platform submodule, contrary to Option A
- **Severity:** low
- **Check:** spec
- **Location:** spec brownfield.md § Module repositories  ↔  spec sequencing.md "Before SM-2"; PRD FR-2
- **Evidence:** brownfield.md: "None of EventStore, Memories, Parties, Tenants, Folders or Projects declares Platform as a submodule yet. Adding these direct declarations is owned work in the spine." sequencing.md: "Each domain module (Tenants, Parties, Folders, Projects) adds Platform as a direct submodule". PRD FR-2 puts EventStore and Memories in the Platform workspace.
- **Classification:** The spec's sequencing.md and the PRD are right; brownfield.md is stale.
- **Fix:** In the spec, limit brownfield.md's "adding these direct declarations" to the four domain modules, and keep the six-module list as an observation only.

### CON-15 — McpCli is required to "become ready" even though the spine treats it as a tool on the user's host
- **Severity:** low
- **Check:** alignment-edit / spine / spec
- **Location:** PRD FR-1 TC, SM-1 ("all seven MVP modules … become ready")  ↔  spine Design Paradigm ("Tool: McpCli"), flowchart ("McpCli on user or agent host"), AD-11; spec CAP-1 ("All seven modules report module-owned readiness")
- **Evidence:** Readiness is defined for services ("Each module defines readiness for its services"). McpCli is a stdio or CLI process with no hosted workload.
- **Classification:** (a) The spine refines this and the PRD/spec never adjusted. Needs a definition.
- **Fix:** In the PRD and spec, define what "ready" means for McpCli in SM-1 and CAP-1. For example: built through the AD-4 mapping, and its connected-mode metadata/digest check against the local gateway succeeds.

## Verified consistent

- **Memlog fidelity:** every decision still in force (entries 7–66) is in the PRD or addendum. This covers the seven-module scope; Option 3 testing with no mocking subsystem; one module-owned server list; the 10-minute test startup deadline with finite overrides; failed-local retention, cancellation ownership and CI cleanup; the module-owned E2E gate that fails closed; the rollback thresholds (10 min / 5 min / 60 s / 2 failures 30 s apart) and the single recovery attempt; GitHub notifications to Administrator; DR Option 2 (RPO 1 h, RTO 4 h, 30-minute runs, 7-day/30-day retention, 1-hour freshness alert, drills before production, monthly and after changes); permission to share infrastructure; reuse of the existing Keycloak; explicit production users; capability scenarios with no uptime target; and the deferred successful-local cleanup default. The only superseded decision still restated is memlog 52 (see CON-1).
- **Entry 71 alignment:** FR-2, the Individual-module intro, SM-2, FR-12 body and TC 1, SM-1, target users (deputy), and the glossary entries "Platform workspace" and "Recovery deputy" all match the decision. The addendum McpCli context matches FR-12. No threshold, gate or fail-closed rule changed. FR-1–FR-12, NFR-1–NFR-3, SM-1–SM-6 and SM-C1–SM-C5 are still unique and contiguous.
- **PRD ↔ spine thresholds:** the spine's Release and Recovery Acceptance section reproduces every PRD number verbatim. That includes rollout, verification, unavailability, smoke-retry and recovery budgets, backup cadence and retention, the freshness alert, RPO/RTO and the 10-minute test deadline. The spine's capability map already says FR-12 acceptance "covers agent-eligible operations", so it is consistent with the post-final PRD.
- **Spine refinements consistent with the PRD (a):** AD-2 exact-release digests plus the maximum evidence age; smoke sampling every 10 s or less and the bounded grace; the durable promotion stop cleared by Administrator; AD-6 separate realms with an Administrator-granted production group; AD-8 negative tests from staging pods, Dapr and hostnames; the off-site monitor (probe every 5 minutes or less, freshness every 15 minutes or less) plus the dead-man check; always off-site, immutable copies; Keycloak and OpenBao in the recovery inventory; the first failed deployment keeps ingress closed; the complete environment uses the largest declared override; McpCli stdio on the user's host, calling the gateway over HTTPS with OIDC, satisfies "MCP access to hosted environments".
- **Spine scope beyond the PRD (d), acceptable:** the AD-9 Dapr boundary, the AD-13 composed host, the AD-15 rollback sets, G1 needing a supported Kubernetes minor, G2 needing Memories tombstone continuity, and ratification of EventStore AD-26 before production profile proof. They add schedule dependencies to the PRD's "before production use" but don't contradict it.
- **PRD ↔ spec:** CAP-1–CAP-12 match FR-1–FR-12 on thresholds, actors, module lists and fail-closed rules: empty or invalid declarations block, absent results are not passes, only one recovery attempt, and cleanup stays within owned resources. Spec additions are cited spine refinements (stale evidence, ingress only after G2, Administrator admission). SM-1–SM-3, SM-5, SM-6 and SM-C1–SM-C5 match the PRD wording; the only difference is SM-4 timing (CON-13). The spec's non-goals trace to PRD non-goals, FR-8 text or spine AD-11/Source Precedence.
- **Brief → PRD:** the five brief outcomes map to SM-1–SM-5, and SM-6 adds the DR outcome from coaching. The brief's IP, domains, data/credential separation, automatic promotion and rollback, root-only submodules and Debug/Release rules are all kept. The two narrowings, module-repository debugging for technical modules and McpCli limited to agent-eligible operations, are backed by memlog entry 71. The brief's tentative integration-tester and operator personas were reconciled on purpose (reconcile-brief-addendum.md). The Hexalith.Builds relationship is carried in the addendum and the spine.
