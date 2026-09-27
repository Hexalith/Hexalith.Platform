---
id: SPEC-platform
companions:
  - ../../planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md
  - acceptance-criteria.md
  - success-measures.md
  - sequencing.md
  - glossary.md
  - brownfield.md
sources:
  - ../../planning-artifacts/prds/prd-platform-2026-09-27/prd.md
  - ../../planning-artifacts/prds/prd-platform-2026-09-27/addendum.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# Hexalith Platform

## Why

**Vision to realize, driven by a pain.** Each Hexalith module wires its own hosting today: domain AppHosts, per-module Dapr plumbing, file-existence source switches. The only root composition is an opt-in Works preview, and no hosted Kubernetes or domain configuration exists. As a result, developers cannot reliably run the whole system, or one module with only its dependencies. CI has no shared real-service environment, and there is no gated, recoverable path to production. Hexalith Platform gives the internal team one solution for development, testing, staging and production. Platform owns shared hosting, composition, promotion and recovery; modules keep their domain behavior. It serves developers, team members using hosted environments (including through McpCli), automated CI workflows, and Administrator, who is the production authority and recovery owner and is backed by a named recovery deputy. The designated Kubernetes installation and a shared Keycloak server already exist. The MVP proves the model on seven modules before the rest enroll through the same schema.

Mechanism authority is the adopted [architecture spine](../../planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md). This spec cites its decisions as AD-1..AD-15 and re-decides none of them.

## Capabilities

CAP-N maps one-to-one to PRD FR-N. Full testable consequences: [acceptance-criteria.md](acceptance-criteria.md).

- **CAP-1** — Complete local environment
  - **intent:** A developer starts the complete MVP environment locally through Platform to test and debug. It contains EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories plus their supporting components.
  - **success:** All seven modules report module-owned readiness together within the effective startup deadline, and CLI/MCP demonstrations execute enabled modules' agent-eligible operations (SM-1). AD-1, AD-9, AD-13.
- **CAP-2** — Debug the active module checkout
  - **intent:** A developer runs and debugs a domain module from its own workspace, which includes Platform as a direct submodule, executing the checkout being edited. EventStore, Memories and McpCli are debugged from source in the Platform workspace.
  - **success:** Breakpoints and source changes affect the running code in each domain-module workspace (Tenants, Parties, Folders, Projects), and for EventStore, Memories and McpCli in the Platform workspace. Only the active root's direct `references/` declarations are initialized. Local runs use project references and Debug; CI uses NuGet and Release (SM-2). AD-4.
- **CAP-3** — Developer-defined minimum environment
  - **intent:** A module developer declares the servers the module needs for development and integration testing, and Platform composes exactly that minimum environment as real services.
  - **success:** The Parties workspace runs the active Parties checkout with EventStore, Tenants and Memories as its declaration specifies, and no unrelated domain module (SM-2). AD-1, AD-4.
- **CAP-4** — Real-service integration test environments
  - **intent:** A developer or CI workflow provisions the declared environment and runs integration tests against real services. Tests wait for readiness, data is isolated, and retention and cleanup are limited to what the run owns.
  - **success:** Local Aspire and CI runs pass module-defined checks. Controlled failure, startup-timeout and cancellation scenarios show the agreed retention and cleanup, with diagnostics preserved and no other environment touched (SM-3). AD-5, AD-10.
- **CAP-5** — Isolated tests without Platform
  - **intent:** A module developer runs isolated tests from the module's test project without starting Platform, using lightweight doubles where needed.
  - **success:** Isolated tests pass with no Platform or domain server running. CI runs them before integration tests, and an isolated pass never satisfies integration acceptance (SM-3). AD-5.
- **CAP-6** — Critical-flow staging gate
  - **intent:** Platform promotes a release to production only after staging E2E tests pass for every module-declared critical flow, run against that exact release.
  - **success:** Every promoted release has complete passing staging evidence for that exact release. Missing, skipped, failed, stale or wrong-release results block promotion (SM-5; SM-C1 = 0). AD-2, AD-7, AD-13, AD-15.
- **CAP-7** — Production deployment verification
  - **intent:** Platform verifies that a production deployment reaches readiness and passes module-owned production-safe smoke checks before declaring it working, and detects failure.
  - **success:** Rehearsals fire each failure trigger: rollout not ready within 10 minutes, 60 seconds of continuous unavailability, two consecutive smoke failures, and a missing result. Only a clean verification window yields a working release (SM-5). AD-2, AD-3, AD-7.
- **CAP-8** — Verified recovery to the previous working release
  - **intent:** After a failed production deployment, Platform makes one automatic, verified recovery to the recorded previous working release without rewinding data.
  - **success:** A rehearsed rollback restores readiness within 10 minutes and passes the 5-minute verification using the restored release's smoke checks, with data intact. Failed or unverified recovery and first-deployment failures are reported accurately, with no second attempt. Administrator is notified through GitHub (SM-5; SM-C3 = 0). AD-2, AD-3, AD-15.
- **CAP-9** — Production backup and disaster recovery
  - **intent:** The recovery owner restores production onto prepared capacity from recovery points kept outside the primary failure domain, and verifies it before reopening.
  - **success:** An isolated drill proves RPO ≤ 1 h and RTO ≤ 4 h from outage to verified service, including integrity, smoke and access checks. Backup cadence, retention, and GitHub failure and freshness alerts are verified (SM-6). AD-7, AD-12.
- **CAP-10** — Isolated staging and production
  - **intent:** Platform hosts the MVP modules in staging (`hexalith.com`) and production (`tache.ai`) on the designated installation, with separate application data and credentials.
  - **success:** Both environments expose the MVP interfaces. Negative tests run as staging users, with staging credentials and from staging pods reach no production data, secrets, ports or hostnames (SM-4; SM-C2 = 0). AD-6, AD-8.
- **CAP-11** — Explicit production-user access
  - **intent:** Production access requires explicit admission by Administrator; staging authorization never confers it.
  - **success:** Staging-only users are denied production data and operations through the API, CLI and MCP. An authorized production user receives exactly their production permissions (SM-4; SM-C2 = 0). AD-6, AD-14.
- **CAP-12** — Module operations through McpCli
  - **intent:** A user discovers and invokes enabled modules' agent-eligible commands and queries through the McpCli CLI or MCP, in a selected local, staging or production environment and under that environment's permissions.
  - **success:** Operations execute only in the selected environment, through its gateway. Operations of disabled modules, unauthorized operations, and UI-only or confirmation-required operations are refused (SM-1, SM-4). AD-11, AD-13, AD-14.

## Constraints

- **Spine precedence.** The spine governs mechanism. Where it refines a PRD rule, the refinement applies. AD changes go through bmad-architecture, never through this spec.
- **Ownership.** Platform owns shared hosting, composition, promotion and recovery. Modules own domain behavior, server lists, readiness semantics, public operations, critical flows, smoke suites and authoritative-state inventories. Platform implements no domain behavior and no second orchestration framework (AD-1).
- **Single composition model.** One Platform-owned Aspire model composes every environment. No AppHost runs in production. The retained application Helm package is the only staging and production deployment definition (AD-1, AD-2).
- **Active-root dependencies.** Only the active root repository's direct `references/` declarations are initialized. No recursive or remote submodule updates; nested submodules stay uninitialized, and any initialized by accident are deinitialized. No ancestor, sibling or package fallback. Local builds use project references and Debug; CI/CD uses NuGet packages and Release (AD-4, AD-5).
- **Real services only.** Platform environments run only the configured real services. Test doubles exist only in module-owned isolated tests.
- **Readiness.** Each module defines readiness for its services. A running process is not readiness, and a required service without usable readiness evidence is a configuration error. The default test startup deadline is 10 minutes; only a justified finite override, shown in diagnostics, may change it.
- **Run-owned test environments (AD-10).**
  - Local: success cleans up; a test or startup failure retains the environment; explicit cancellation cleans up only the resources that run created.
  - CI: always cleans up, including after partial provisioning.
  - Cleanup never touches another run or a hosted environment, and results and diagnostics survive it.
- **Hosting.** The designated Kubernetes installation is `192.168.1.30`, with staging on `hexalith.com` and production on `tache.ai`. Supporting infrastructure may be shared only if application data, credentials and namespaces stay per environment (AD-8).
- **Identity.** The existing shared Keycloak hosts separate staging and production realms. Production admission is membership in a group that only Administrator grants: never by default, by promotion, or by copying staging membership (AD-6, AD-14).
- **Same release digests.** Promotion ships the same retained package and image digests that passed staging. Evidence is bound to the exact release, and absent evidence is never a pass (AD-2).
- **Production verification defaults.** Rollout must reach readiness within 10 minutes, followed by a 5-minute verification window. The deployment fails on 60 continuous seconds of unavailability, two consecutive smoke failures 30 seconds apart, or a missing result. One failed probe or one restart does not fail it. These defaults must be validated in staging; they are not availability guarantees.
- **One recovery attempt.** Recovery makes exactly one automatic attempt to the recorded working baseline: 10 minutes to readiness, then the same 5-minute verification. It never cycles through older releases. Every non-working outcome sets a durable promotion stop that only Administrator clears (spine Release and Recovery Acceptance).
- **NFR-1 rollback data safety.** Application rollback never rewinds business data, event history, credential rotations or key generations. Automatic releases must keep schemas and event formats compatible with the previous version. Incompatible releases use the Administrator-approved mode with a planned recovery (AD-3, AD-15).
- **NFR-2 recovery bounds.** RPO ≤ 1 h. RTO ≤ 4 h from outage to verified service, including detection, operator response and capacity. Backups run every 30 minutes, keeping frequent recovery points for 7 days and daily points for 30 days. Copies are encrypted, stored outside the primary failure domain, and have independent access and decryption keys. An alert fires when the newest usable recovery point is more than 1 h old. These are targets that have not yet been demonstrated (AD-12).
- **NFR-3 isolation.** Isolation holds at service and data boundaries, including direct requests that bypass UIs. It is verified by negative tests run as staging users, with staging credentials and from staging pods, plus a positive production-user test (AD-8, AD-14).
- **Dapr boundary.** Dapr is the runtime infrastructure boundary. Only the named AD-9 exceptions (Memories FalkorDB graph, Memories Redis search/vector, the Memories SET NX preflight row) use provider SDKs. A new exception needs Administrator acceptance recorded in the spine (AD-9).
- **Executor isolation.** Staging, test or PR code never runs where production credentials are or were held. Deployments run on isolated per-environment executors from the private operations repository (AD-7).
- **Notifications.** GitHub issues assigned to Administrator are the only notification path. Failure and freshness reporting keeps working while the primary environment is down.
- **MCP surface.** McpCli stdio is the only MVP MCP surface. McpCli executes only agent-eligible operations, and no module MCP host is deployed or routed (AD-11, AD-14).
- **G1.** Production is first deployed with ingress closed, admitting only executor and probe sources, with an empty admission group, on a supported Kubernetes minor ([sequencing.md](sequencing.md)).
- **G2.** Production users are admitted and ingress opens only after the AD-12 drill passes, including Memories tombstone continuity. The first admitted user completes SM-4 on the live production realm before anyone else is admitted. The PRD's "before production use" means G2: SM-4 and SM-6 are its entry evidence.
- **G3.** Automatic promotion is enabled only after the SM-5 rehearsals pass; this is the PRD's "before enabling automatic production promotion". Before G3, every production release uses the Administrator-approved mode.
- **First shared versions.** Module adoption epics wait for the First shared versions artifacts they consume. Each artifact exists before the item it must precede ([sequencing.md](sequencing.md)).
- **Source precedence.** The Platform MVP recovery envelope governs the stricter infrastructure clauses of Folders and Projects without blocking their enrollment. Module behavior, authorization, data-safety and release gates are never waived.
- **Evidence.** Deferring work assigns it; it never turns absent evidence into a pass. A document marked final does not establish deployed capability.

## Non-goals

- A generic Platform mocking subsystem, fake-service catalogue, or per-service real/fake switches. If a concrete test later needs a substitute service, that case is assessed on its own.
- Rollback triggered by traffic metrics (error rate, latency) in the MVP.
- Automatically reversing business data or event history on rollback, switching releases indefinitely, or automatically handling failures found after the verification window; those are operational incidents.
- A standby production installation, automatic infrastructure failover, HA or warm standby, multiple regions, broker changes or scaling work without a measured requirement.
- A numeric uptime percentage, latency or throughput targets, 24/7 response, or a cost target.
- MCP surfaces beyond McpCli stdio (hosted HTTP MCP, Memories.Mcp, Parties.Mcp, FrontComposer MCP, EventStore.Admin.Mcp), and step-up or confirmation flows through McpCli.
- Platform maintaining a central list of critical flows or smoke suites, or its own business implementations of module operations.
- Including servers outside the MVP set (e.g. Agents, ChatBot, Conversations, Timesheets, Works) in MVP acceptance. They stay in longer-term scope and will enroll through the same declaration schema.
- The Projects G-1 durable task engine; the HA, RPO-0 or 15-minute RTO infrastructure guarantees in Folders and Projects documents; a custom orchestration DSL, environment service or per-dependency retry policy.

## Success signal

- A developer debugs Parties from its own workspace against real EventStore, Tenants and Memories. A release that passed staging E2E for that exact release reaches `tache.ai` through G1, G2 and G3. A rehearsed failure rolls back once with data intact, and a drill restores production within a 1 h RPO and 4 h RTO.
- Each SM-1..SM-6 demonstration is recorded with release, environment, result and diagnostics, and SM-C1..SM-C4 stay at zero ([success-measures.md](success-measures.md)).

## Open Questions

- **For bmad-architecture:** once McpCli's Platform submodule is removed, how do McpCli's CI integration tests get a Platform runner version? AD-10 requires McpCli tests to use the Platform runner, but AD-4's version check assumes a submodule. This affects CAP-4 and SM-3 for McpCli only.
