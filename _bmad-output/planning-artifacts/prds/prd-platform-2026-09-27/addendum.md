---
title: Hexalith Platform PRD — Supporting Context
status: draft
created: 2026-09-27
updated: 2026-09-27
---

# Supporting context

This document carries architecture and implementation context from the [brief addendum](../../briefs/brief-platform-2026-09-27/addendum.md), PRD coaching, and the [accepted Platform architecture](../../architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md). The architecture's [update gate](../../architecture/architecture-platform-2026-09-27/reviews/update-2026-09-27/gate-summary.md) passes document handoff; implementation and production qualification still need evidence. Repository observations below are historical observations from brief creation on 2026-09-27, not a fresh runtime inspection.

## Existing implementation context

The inspected AppHost contained an opt-in, Development-only Works preview using a sibling Works checkout and nested EventStore paths. Its composition included EventStore/admin/Operations, Works, and Dapr. That preview does not establish complete MVP integration or general module selection. The source addendum identifies Works as the rollback composition until its migration parity gate and assigns Agents wiring and live evidence to an existing Agents story.

The inspected Platform repository declared sixteen references: AI.Tools, Agents, Builds, ChatBot, Commons, Conversations, EventStore, Folders, FrontComposer, Memories, Parties, PolymorphicSerializations, Projects, Tenants, Timesheets, and Works. A reference declaration does not establish that a component is a running service or an integrated Platform capability. Immediate module declarations inspected for the brief did not include Platform, and the requested hosted Kubernetes/domain configuration was not found in the root implementation.

## McpCli context

The source identifies the sibling `../mcpcli` repository as McpCli's canonical location; it was absent from Platform's inspected root submodule declarations. McpCli supplies the `hexalith` .NET tool, discovers decorated Contracts libraries, and routes commands and queries through EventStore.

Its inspected README documented stdio MCP, deferred HTTP transport, and described an empty catalog with enrollment pending. These historical observations do not establish current runtime readiness or enrollment completeness. Accepted AD-11 selects a versioned McpCli running on the user or agent host with a common CLI/stdio MCP core. Hosted access uses the selected environment's authenticated EventStore gateway over HTTPS; stdio is the only MVP MCP surface. Other module or technical MCP hosts are not deployed or routed in Platform compositions until a later architecture decision admits them.

McpCli exposes module-defined commands and queries subject to the selected environment's permissions and agent eligibility. Offline Contracts inspection does not establish connected availability: execution also requires matching per-operation contract-schema digests from the selected gateway's committed catalog. Unknown or mismatched operations are non-executable. Profiles bind gateway, issuer and audience together; short-lived environment tokens go only to their issuing environment's gateway, with no fallback or implicit module enablement. Platform does not duplicate domain behavior.

AD-14 makes the authenticated client the calling surface. McpCli's CLI and MCP heads share one agent-capable surface; UI-only and confirmation-required operations require a confidential server-side UI client. The gateway derives actor and surface from authenticated evidence, reauthorizes every call and rejects actor spoofing. Synchronous cross-module steps use Keycloak token exchange; asynchronous steps use the EventStore-attested original actor restricted to its admitted task. Each public client needs a negative test for UI-only and confirmation-required operations. Connected discovery, token renewal, enrollment and these authorization rules remain implementation qualification, not existing capability claims.

## Workspace and build constraints

Only dependencies declared directly under `references/` by the active root repository may be initialized. Platform-root work uses Platform's declarations. Module-root work uses the module's direct declarations for Platform and any other dependencies; embedded Platform's own nested references remain uninitialized.

The source policy prohibits recursive or remote submodule updates and requires accidentally initialized nested submodules to be deinitialized. This is preserved policy context, not an instruction to modify the current working tree during PRD creation.

AD-4 resolves the active root once and uses the active module plus its directly declared Hexalith dependencies from source with Debug assets; other dependencies use packages at the Builds catalog version. CI/CD uses Release/NuGet artifacts. Missing required source, duplicate source/package identities and tool/submodule identity mismatches fail explicitly; sibling, ancestor, nested-Platform and package fallback are forbidden. In a module workspace the Platform submodule commit is the sole Platform identity: local mode runs that source, while the pinned CI tool must embed the same commit. Removing the historical preview's alternate paths remains implementation work.

### Module-owned configuration example

Each module developer defines that module's minimum dependencies. Domain modules require at least EventStore, Tenants, and Memories. The module repository contains a configuration file listing the servers to enable for development and testing; Platform consumes that selection.

In the product owner's Parties example, the Parties repository directly references EventStore, Tenants, Memories, and Platform as submodules under `parties/references`. Its configuration file selects the servers needed to develop and test Parties. This is a requested workspace arrangement, not a claim that the current Parties checkout already implements it.

The MVP uses one module-owned server list for development and integration testing. Platform owns the versioned declaration schema and validator; each module owns its declaration, including logical capabilities, readiness, startup-task lifecycle, interfaces, checks and recovery inventory. Current and previous schema majors are supported, and missing, duplicate or incompatible declarations fail validation. The first shared schema and its concrete files remain qualification work. Platform starts real configured services; lightweight test doubles belong in isolated module tests outside Platform.

## Testing options for module dependencies

The MVP uses Option 3: real Platform environments with focused tests outside Platform. The comparison preserves the rationale and alternatives, including the baseline of EventStore, Tenants, and Memories for domain-module environments.

### MVP approach

Use Option 3. Keep one module-owned server list as the initial development/integration baseline. Run inexpensive isolated tests first, then a focused real-service integration suite in CI. Start a test environment per suite or compatible batch, with isolated data. Preserve failed local environments for debugging until the developer explicitly stops and cleans them up. CI automatically cleans up after success, failure, or cancellation, including partial provisioning. Cleanup is scoped to the resources owned by the test environment. Preserve the confirmed local Debug/source and CI Release/NuGet distinction.

AD-5 and AD-10 settle hosting and lifecycle: the Platform runner provisions, waits for readiness, records ownership and cleans integration environments; fixtures consume its versioned descriptor. Local runs use Aspire and source assets; CI uses disposable Linux GitHub-hosted runners with Release/NuGet artifacts and no hosted-environment credentials. Technical-module AppHosts may support their own repository tests, but those runs are not Platform multi-module integration evidence.

Default to a fresh run-owned environment per suite or compatible batch with isolated ports, resources and catalog. Local success cleans automatically; local failure retains a listable environment with owner and age. The first terminal outcome decides retention or cleanup. Explicit attachment is allowed only to compatible local or CI run-owned environments after composition, artifact-mode, readiness and data-isolation checks; hosted attachment fails closed. Cleanup is idempotent, reports leftovers and never treats a shared AppHost path as ownership.

Do not add a generic Platform mocking subsystem or per-service real/fake switches for the MVP. If a concrete test later needs a substitute service, assess that specific case. Testing domain rules should not require launching Platform; claiming that a module integrates with its required services should require evidence from real-service tests.

### Option 1: Real services for all tests that exercise dependencies

Platform reads the Parties module configuration and starts Parties, EventStore, Tenants, Memories, and required infrastructure. Tests exercise actual service interactions against development or test data. Local runs use the confirmed source/Debug policy; CI uses the confirmed NuGet/Release policy.

Advantages: one understandable environment model, realistic integration evidence, and no separate fake implementations of core services. It can expose actual contract, tenant-boundary, serialization, persistence, and configuration problems when tests exercise those paths.

Costs: dependency startup, resource consumption, data setup/cleanup, and harder reproduction of particular failure modes. Running every small input permutation through the distributed environment makes feedback unnecessarily slow. Real services alone do not establish production parity or adequate test coverage.

Practical implementation: start the environment once per suite or compatible batch; wait for readiness rather than fixed delays; isolate test data so reuse does not couple tests. Never point a test environment at staging or production application data.

### Option 2: Allow selective test doubles

A test substitutes a controlled fake or stub for a dependency. For example, a focused Parties test could supply a known tenant lookup result or simulate a dependency timeout. In-process tests can replace collaborators at existing interfaces. A separately running Parties server needs an explicitly configurable substitute endpoint or application composition; substituting an object inside the test process does not replace a remote service.

Advantages: fast focused checks, controlled edge cases, and reduced local resource requirements. Useful for exercising a module's own response to dependency results or failures.

Costs: fake behavior can diverge from real services, giving misleading confidence about integration. Fake services need upkeep; arbitrary real/fake combinations multiply supported configurations. A substitute for EventStore cannot establish actual EventStore integration behavior. Extending Platform to supply generic fake services would add a second product responsibility.

Practical boundary if this option is selected: keep simple doubles in the owning module's tests and retain real-service checks for the interactions that matter. Avoid implementing complete fake EventStore, Tenants, or Memories servers solely to save startup time.

### Option 3: Real Platform environments with focused tests outside Platform

Use the developer-owned server list unchanged for interactive development and distributed integration tests. For Parties, that means actual Parties, EventStore, Tenants, Memories, and required infrastructure. Run unit tests and focused component tests directly from the module's test project, using lightweight doubles only when needed. Platform does not participate in those isolated tests.

Advantages: fast checks for module logic plus real integration evidence, while Platform keeps a simple responsibility: start configured real services. No server-substitution matrix or separate fake-service catalogue is required.

Costs: developers must distinguish isolated tests from distributed integration tests, and CI must run both. The real-service suite still needs readiness, data isolation, and cleanup. Failures across actual service boundaries still require targeted integration tests; passing isolated tests does not replace those checks.

### Supporting sources

- [Aspire testing overview](https://aspire.dev/testing/overview/): distributed tests exercise separately running resources and support application lifecycle management. In-process test dependency injection does not replace collaborators inside another process.
- [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0): distinguishes isolated tests from integration coverage and documents test-host service replacement. The options and recommendation above are project-specific analysis, not mandates from the documentation.

## Test readiness and local cancellation rationale

The MVP uses readiness option 3 and cancellation option 1 below. FR-4 in the [PRD](prd.md) defines the requirements; the alternatives preserve the rationale. Failed local environments remain available for debugging and CI environments are cleaned up after success, failure, or cancellation.

### Selected behavior

Use readiness option 3 and cancellation option 1. Preserve the distinction between failing tests and explicitly cancelling a run, while limiting cleanup to owned resources.

| Situation | Behavior |
| --- | --- |
| Local test succeeds | Clean up its run-owned environment. |
| Local test fails | Retain its environment for debugging; already agreed. |
| Local environment startup fails or times out | Retain surviving local resources and diagnostics for inspection. |
| Developer explicitly cancels a local test run | Stop the run and clean up resources it created. |
| Test explicitly attaches to a compatible local or CI environment | Cancelling the test does not stop an environment owned by another run or developer session. Hosted attachment is refused. |
| CI succeeds, fails, or is cancelled | Clean up run-owned resources, including partial provisioning; already agreed. |

The Platform runner owns lifecycle and applies the first terminal outcome; normal test-fixture disposal must not defeat retention of failed local environments. Capture diagnostic evidence before cleanup. Startup tasks declare lifecycle and authority: environment creation tasks never rerun on upgrade, rollback or restore, and hosted population or authority creation is operator-only.

### Readiness option 1: Central checks and a fixed timeout

Platform defines how all services are judged ready and applies one fixed startup deadline. A generic rule might check process state or a standard HTTP endpoint.

Advantages: consistent behavior and few configuration choices. Limitations: Platform can accumulate module-specific knowledge, and generic checks may miss initialization needed by a particular module. Process-running status alone is insufficient evidence that a service can accept test requests. An inflexible timeout can fail legitimate slower environments.

### Readiness option 2: Independent module checks and timeouts

Every module supplies readiness behavior and chooses its own waiting rules. Platform uses those declarations to decide when it can run tests.

Advantages: domain knowledge and startup expectations stay with the module developer; unusual initialization is supported. Limitations: timeout semantics and failure reports can diverge across modules. Without a shared bound for the selected environment, a failed dependency can keep the whole run waiting too long. More independent timers require more rules for how they combine.

### Readiness option 3: Module checks with a common startup policy

Each module defines readiness for its services. Platform waits for the selected module and its required dependencies to report readiness, and any required one-off startup tasks to complete successfully. Readiness means the service is initialized and able to handle the intended requests; full business-flow validation remains in integration/E2E tests.

Use a **10-minute overall environment-startup deadline** by default, measured from the request to start the environment until all required resources are ready. It covers provisioning/startup waiting, not subsequent test execution. Allow an explicit finite override in the module's test configuration for a justified slow-starting environment. Show the effective timeout in diagnostics. This test default does not modify production deployment deadlines.

If a required resource has no usable readiness definition, configuration fails rather than silently treating a running process as ready. A definite startup failure may fail the run before the deadline. A timeout reports which resources were not ready and retains diagnostic evidence. Local startup failures preserve the surviving test environment for inspection; CI startup failures clean up resources owned by that run.

Advantages: module ownership with consistent bounded behavior, using a small configuration surface. Limitations: readiness checks still require maintenance, and timeout overrides need justification rather than being used to hide failures. This is the selected option; no custom orchestration language or separate retry policy per dependency is needed for the MVP.

For Parties, Platform starts the configured Parties, EventStore, Tenants, and Memories services with their required infrastructure. Tests begin when the required readiness checks pass. If one required service never becomes ready, the run fails with that resource identified instead of proceeding with a misleading partial environment.

### Cancellation option 1: Stop resources owned by the cancelled run

Explicitly cancelling a local test run stops test execution and stops/cleans up the environment resources created by that run. An Aspire environment started separately by the developer is outside the run's cleanup ownership and remains running. Retain test results and useful logs independently of resource cleanup.

Advantages: a cancelled run does not leave its own background services consuming resources; the same ownership rule prevents disruption to other work. Limitations: the live state of a run-owned cancelled environment is lost, so reproducing it requires another run. Cancellation is deliberately different from a failed test result, which retains the local environment under the accepted policy.

### Cancellation option 2: Keep the environment after cancellation

Cancelling stops test execution but leaves its environment available until the developer explicitly stops it, just as for a failed local run.

Advantages: preserves live inspection/debugging state and supports rapid test reruns. Limitations: environments can accumulate, occupy ports, retain state that affects later work, and consume resources until explicitly cleaned up. The developer needs to remember which environments are still running.

### Official-source context

- [Aspire health checks](https://aspire.dev/fundamentals/health-checks/) distinguishes resource readiness checks from service health endpoints; without configured checks, orchestration can fall back to running state. The selected policy requires explicit suitable readiness evidence for required services.
- [Access resources in Aspire tests](https://aspire.dev/testing/accessing-resources/) distinguishes running from healthy and recommends bounded waits. The 10-minute value is a project choice, not an Aspire default.
- [Manage the AppHost in tests](https://aspire.dev/testing/manage-app-host/) explains that disposing a test-owned AppHost cleans up its resources. Preserving failed local environments requires deliberate lifecycle handling.
- [Advanced Aspire testing](https://aspire.dev/testing/advanced-scenarios/) documents restrictions on using the project-based testing builder with file-based AppHosts. Architecture must check the actual AppHost form and installed version when implementing the lifecycle; this PRD does not select a testing API or require an AppHost conversion.

## Production rollback policy and rationale

The MVP uses readiness plus submodule-owned production smoke tests and one verified application recovery attempt, except when an Administrator-approved incompatible release names a separately planned recovery. FR-7, FR-8, and NFR-1 in the [PRD](prd.md) define the requirements. Numeric values are initial policy defaults, not measured service guarantees; validate them with staging evidence before production rollout.

### Options and rationale

| Approach | How it works | Advantages | Limitations |
| --- | --- | --- | --- |
| Readiness alone | Restore the previous working release when the new workloads cannot become ready or remain unavailable. | Simple and inexpensive; detects startup and configuration failures. | Healthy endpoints can coexist with broken business operations. |
| Readiness plus module-owned smoke checks | After readiness, run a small production-safe check set selected by each submodule from its critical flows. Restore the previous working release on persistent availability or functional failures. | Checks both operability and essential behavior with predictable, bounded automation. | Modules must maintain safe checks and their test data; coverage remains smaller than staging E2E coverage. |
| Add traffic-metric triggers | Also compare error rates or latency against an established baseline. | Can detect regressions outside the smoke checks. | Requires meaningful traffic, baselines, and tuned thresholds; adds noise and operating work before those exist. |

Use readiness plus module-owned smoke checks for the MVP. Continue running the complete required critical-flow E2E suite in staging. Production smoke checks are a short additional verification and do not replace that staging gate. Defer automatic traffic-metric triggers until operating evidence justifies them.

### Accepted release identity and production entry

AD-1 and AD-2 select one Platform Aspire application model and one retained application Helm package with immutable image identities. Publish once, validate in staging, and deploy that retained artifact; never regenerate it during promotion or recovery. Data services, persistent state, shared infrastructure and credential rotation belong to a separately versioned, forward-only environment layer. A representative exporter qualification must prove the model works without a custom compiler, duplicate topology or recurring generated-file patches; otherwise the accepted fallback is a small maintained Helm chart with parity checks.

The release record binds immutable artifacts, composed-host identity, module contracts and checks, profile template and qualified environment-layer versions. Environment-current values include secret references, authorization/configuration versions and current key/catalog generations. Each attempt records the exact values used; the working baseline is the last verified working release and attempt. Staging evidence must identify the serving release, suite, profile, configuration and production baseline, and remain within its maximum age. Incomplete, stale or mismatched evidence cannot authorize automatic promotion.

There is one production deployment workflow with two modes. **Automatic** requires G3 and complete compatibility evidence. **Administrator-approved** uses an authenticated approval for pre-G3 releases and incompatible changes; an incompatible change names its separately planned recovery. Both retain the staging gate, serialized attempt ownership, artifact provenance, production verification and durable records. Administrator approval is not a waiver of failed critical-flow checks.

| Entry gate | Evidence required |
| --- | --- |
| G1: closed production deployment | Supported infrastructure and declared access paths; production ingress admits only executor/probe sources and the production admission group is empty. |
| G2: admit users and open ingress | Successful disaster recovery drill, including Memories tombstone and key continuity, with access/isolation checks. |
| G3: enable automatic promotion | SM-5 deployment-failure and recovery rehearsals plus qualified release/attempt controls. |

Between initial G1 deployment and G2 opening, Administrator may temporarily admit a designated synthetic test identity to prove SM-4 positive access. Ingress stays executor/probe-only, general users remain excluded, and the test grant is revoked after verification. Recovery drills use isolated identity copies. Architecture must carry this restricted qualification step alongside its initial empty-admission-group rule.

AD-15 requires a prepared recovery combination before rollout: the working application with current security/configuration and a forward catalog generation preserving its routes and all still-referenced idempotency/key entries. Staging rehearses candidate-to-baseline recovery, including a command introduced by the candidate and its written state/events. Retirement cannot invalidate that combination before the release is working. Breaking, missing or wrong-baseline evidence routes the release to Administrator-approved planning.

AD-2 retains release artifacts, records and supporting evidence outside the primary failure domain for at least the backup retention period or their lifetime as a rollback target, whichever is longer. Platform and Builds qualify this retention and the staging trigger/rerun evidence policy before relying on release evidence.

### Failure triggers and initial thresholds

1. **Rollout does not complete:** required workloads have not converged to the intended release and become ready within 10 minutes of deployment start. This is an overall deployment-workflow deadline, not a claim that a Kubernetes progress deadline measures total rollout duration.
2. **Persistent service unavailability:** during a five-minute verification window after rollout readiness, any required service cannot serve traffic for 60 continuous seconds. An isolated probe failure or one container restart is insufficient by itself.
3. **Business verification fails:** a required module smoke check fails twice consecutively, with the second attempt 30 seconds after the first failure. A required check with no completed passing verification result at the deadline also prevents deployment success and triggers recovery. The check runner must identify the intended release rather than accidentally verifying only an old replica.

Mark the release working only when required services are ready and the latest required smoke-test results are passing at the end of the verification window, with no failure trigger having fired. Missing test results never count as success. The recorded working baseline must be ready and pass its smoke suite now before an update begins. Recovery renders that retained application using compatible environment-current values. A pre-existing unhealthy production environment stops the update for investigation.

One lock covers every workload-affecting environment change, including configuration, rotations, catalog/profile changes and disaster recovery. Durable attempt timers never restart, observation gaps invalidate verification, and interruption never grants another recovery attempt. An unreadable record, unknown release or ambiguous ownership stops mutation for intervention. Attempt records, the working baseline and the promotion stop remain accessible outside both the cluster and executor host.

### Module responsibilities and production test data

Each submodule chooses and maintains its small production-safe smoke set. Prefer read-only checks; necessary writes use dedicated synthetic test data, with no real-user changes or external effects such as notifications. Do not assume event records can simply be deleted after a test. The full staging E2E suite may contain operations unsuitable for production and must not be copied blindly.

### Recovery and its limits

- Make one automatic recovery attempt to the recorded previous working application release with compatible environment-current values, including partially updated workloads and committed routing changes. If no release-owned object changed and no catalog generation was committed, retain the current release and report the failed attempt. Administrator-approved incompatible releases use their named recovery procedure.
- Verify recovery using readiness and the recorded production-safe smoke suite applicable to the restored release, under the same verification policy and failure thresholds. Checks for features introduced only in the failed release must not incorrectly fail its predecessor. Initial recovery budget: 10 minutes to restore readiness, followed by the same five-minute verification window. A successful rollback command alone is not proof of restored service.
- Every non-working production outcome and every disaster recovery entry sets the durable promotion stop. Retain diagnostics and report every deployment failure and recovery result. Only an authenticated Administrator record naming the reason and a verified current working release clears the stop; deputy restoration does not resume promotions. If recovery fails or the cluster is unreachable, report failed or unverified recovery; do not loop through releases.
- A first deployment has no previous working release. On failure, keep ingress closed, report the bootstrap failure and stop. Recovery from a failed first module enrollment removes its application workloads but preserves its data objects.
- Restore application code and compatible configuration without rewinding business data, event history, or credential rotations. The automatic deployment path requires compatibility with the previous working application version, including schemas and newly written event formats. An incompatible change needs a separately planned release/recovery procedure; application rollback alone cannot undo its data effects.
- These triggers govern the deployment and its verification window. Failures discovered later require operational incident handling; they do not trigger indefinite automatic version changes under this policy.

### GitHub notification integration

GitHub issues assigned to Administrator are the accepted channel for deployment, backup and recovery notifications. Notifications identify the affected release/environment and outcome and carry opaque references to access-controlled evidence; deployment logs and artifacts are private. The selected deputy policy below adds delivery to the named deputy without transferring promotion-resume authority. Account mappings, actual delivery and escalation remain qualification work. GitHub is the single accepted notification channel.

### Recovery deputy authority

**Selected: Option 1, the recommended split.** The named deputy receives recovery alerts, executes the documented recovery procedure through their own identity, verifies the restored service and reopens service after all recovery checks pass. Administrator alone clears the promotion stop. Restoring already authorized access is part of recovery; the choice does not delegate production-user admission or general identity administration.

| Option | How it works | Advantages | Costs and limits |
| --- | --- | --- | --- |
| 1: Deputy restores and reopens; Administrator resumes promotion — selected | Give the deputy only the permissions, key access and runbook needed for documented recovery and verification; leave promotion stopped for Administrator review. | Recovery can proceed when Administrator is unavailable while one person retains release-resume authority. | Releases remain stopped until Administrator returns; the deputy needs practiced access and must receive alerts. |
| 2: Deputy also resumes promotion after the same checks | Add permission for the deputy to record the verified working release and clear the stop under the same evidence requirements. | Removes the final dependency on Administrator after successful recovery. | Delegates broader release authority and requires a clear accountability and access-review rule. |

Implement Option 1 with ordinary named identities, narrowly scoped permissions and a rehearsed runbook, without a custom workflow system. **Architecture follow-up is required:** its current Administrator-only operations access and notification rules do not yet implement deputy recovery authority. Update those controls and prove independent deputy access before relying on deputy response coverage. This does not automatically grant operations-repository write access; if a second writer is introduced, the accepted GitHub Team control review applies.

### Implementation implications from official documentation

[Kubernetes Deployments](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/) reports stalled rollout conditions but does not itself implement automatic rollback. A Deployment rollback restores the Pod template; broader release configuration and application data recovery are separate concerns. Platform's deployment workflow must therefore own the recovery decision and verify the outcome.

[Kubernetes probes](https://kubernetes.io/docs/concepts/workloads/pods/probes/) distinguish startup, readiness, and liveness behavior. Use those signals for their intended purposes; transient readiness loss or a container restart should not automatically be treated as proof that the entire release must be reverted. The thresholds above are project policy defaults, not Kubernetes-prescribed values.

## Disaster recovery approach and alternatives

The MVP uses Option 2: frequent independent backups and a tested restore procedure, with production RPO at most one hour and RTO at most four hours within declared response coverage. FR-9 and NFR-2 in the [PRD](prd.md) define the requirements; Options 1 and 3 preserve alternatives and their trade-offs. The original brief did not establish topology, storage engines, data volume, spare capacity or existing backups. The accepted architecture now selects prepared replacement capacity and module-aware recovery; representative timed evidence is still required.

**Recovery time objective (RTO)** measures the original service outage to verified service restoration, including detection, operator response, capacity provision, data restoration and validation. The selected coverage policy below defines when the four-hour objective applies; starting a response shift never resets the outage clock. **Recovery point objective (RPO)** is the maximum target age of recoverable data relative to failure. Its one-hour target and backup monitoring apply continuously. Backup schedules alone establish neither objective.

Application rollback under FR-8 restores code while preserving data. Disaster recovery restores lost or damaged state from a recoverable copy and may lose changes beyond its recovery point; its accepted one-hour data-loss target is distinct from the data-preservation requirement for application rollback.

### Option 1: Daily backups and manual rebuild

Create application-consistent backups once per day and copy them outside the primary infrastructure. After a failure, Administrator provisions replacement capacity, redeploys a recorded application release, restores compatible data and configuration, and verifies service behavior.

Illustrative targets: up to 24 hours of lost data and restoration within 24 hours of the outage, if the backup and replacement capacity are available. A missed or unusable backup makes actual loss worse.

Advantages: few moving parts, low operating cost, and an understandable recovery procedure. Suitable where a day's work can be recreated or the data is disposable.

Disadvantages: potentially losing a full day's committed business changes; a lengthy manual restoration; no continuity during infrastructure loss. This is not the recommended default for production business records. Even this option needs off-primary copies and restore validation.

### Option 2: Frequent off-primary backups and a tested restore procedure

Keep a recoverable application-data copy outside the primary server/storage failure domain. Begin with backup or incremental-copy runs every 30 minutes, aiming for a newest completed, usable recovery point no older than one hour. Choose database-native backup mechanisms appropriate to the actual storage engines. Use existing incremental or log-archive capabilities where useful rather than writing a custom backup engine.

After failure, Administrator follows a short restore procedure to rebuild compatible application/configuration state, restore authoritative data, and run the approved production-safe smoke checks before reopening access.

Production targets: **RPO at most one hour continuously** and **RTO at most four hours from outage to verified recovery within declared response coverage**. The RTO includes operator response and replacement capacity; it is not a stopwatch started after those prerequisites become available. Full outage durations are recorded even outside response coverage.

Advantages: substantially less potential data loss than daily backups, no standby production cluster required, and a recovery process that can be tested independently. Fits an internal MVP with one named recovery owner if that owner can respond and recovery capacity is available.

Disadvantages: outages last while data is restored; backup duration, transfer capacity, and data volume can defeat the targets. Backup freshness must be monitored. The selected target explicitly accepts possible loss of up to one hour of committed data.

### Option 3: Redundant infrastructure and database failover, with backups retained

Run redundant application and stateful services across independent failure domains, with standby capacity and tested failover. Depending on the database, synchronous replication can protect acknowledged writes during supported node failures; asynchronous replication can lose changes that have not reached the standby. Continue independent backups for corruption, deletion, and losses affecting both copies.

Illustrative objective for a supported single-node failure: restoration within 15 minutes and little or no loss of acknowledged data, depending on replication semantics. This is a different failure scope from losing the whole installation. A whole-site outage still requires an independent recovery location or restoration from backups.

Advantages: shorter interruptions for failures covered by the redundancy design; less reliance on manual provisioning for those failures.

Disadvantages: more machines and storage, more failure modes, replication monitoring, failover testing, and safeguards against conflicting writers. Replication can also propagate bad changes. Extra application replicas on the same underlying server or storage do not provide independent failure protection. Not recommended for the MVP unless the acceptable outage/data-loss objectives require it.

### Selected approach and operational scope

Use Option 2 with the one-hour potential ordinary-data loss target and validate the four-hour recovery target under the selected response coverage before production use. The Memories erasure exception below is stronger than the ordinary-data RPO. GitHub notifications and named recovery operators do not establish round-the-clock coverage or an uptime guarantee.

Initial implementation scope:

- Each module identifies authoritative databases, files, and configuration needed for recovery. Include EventStore's durable history and the authoritative state of the other deployed modules; do not assume all state can be reconstructed from EventStore. Rebuild derived state only where a module supplies a verified procedure, and include that time in recovery measurements.
- Include required shared dependencies, particularly Keycloak, its admin/user revocation event export, each environment's OpenBao and production access configuration. Administrator confirms each dependency's recovery owner and surviving or restored availability. The accepted architecture requires a verified dependency recovery procedure; concrete backup tooling remains qualification work.
- Keep immutable encrypted completed recovery points off-site with restricted access and independently available access/decryption material and retained application artifacts. Tenant-key material has separate custody from ciphertext backups. Whole-site recovery also needs replacement capacity at an independent location.
- Start backups/incremental copies every 30 minutes, retain frequent recovery points for seven days and daily recovery points for 30 days, and measure the newest usable recovery point rather than treating a successful timer invocation as evidence. Retain any base backup and incremental chain needed to make those points recoverable.
- An off-site monitor checks production availability at least every five minutes, usable recovery-point freshness at least every 15 minutes and stalled deployment attempts. It warns before freshness exceeds one hour and raises GitHub issues for backup failure or a recovery point older than one hour; an hourly GitHub dead-man check detects a stale monitor heartbeat. Deliver recovery alerts to Administrator and the deputy under the selected authority policy.
- Record one declared recovery cut, compatible release/configuration, required identity and Memories lineage context and verified artifact integrity. A complete recovery set needs valid checksums, decryptable data and unbroken incremental chains. Independently timed snapshots do not prove mutually consistent module state; pruning cannot remove a set referenced by an open recovery.
- Validate restoration in isolation before G2, monthly thereafter and after material changes to storage or backup behavior. Assume primary server and storage are unavailable. Check integrity, the restored release's production-safe smoke tests, permitted production access, rejection of staging-only users/credentials and denial of revoked principals. Record real detection, declared worst-case response, capacity/transfer/replay time, recovered-data age and total outage-to-restoration duration. Drill substitutions are explicit; drills do not mutate live authority.
- Identify prepared replacement compute/storage, recovery executor access and key custody before G2. If representative evidence cannot support the four-hour target within declared coverage, provide the missing capability or explicitly revisit the target; do not label an unproven procedure qualified.

The original options used PostgreSQL native log archiving as an example, without selecting Platform storage. Actual environment-layer pins and module-qualified backup mechanisms belong to the accepted profile inventory. Improve native recovery capabilities before building custom replication or a multi-cluster system solely for a tighter RPO.

### Response coverage and the four-hour target

**Selected: Option 1, the recommended coverage policy.** Publish response coverage and maximum acknowledgement time and qualify the four-hour RTO within those arrangements. Every incident reports its original outage time, actual response time and full duration through verified restoration, including incidents outside coverage. Neither a shift start nor operator arrival resets the clock. The one-hour RPO, backups and freshness monitoring remain continuous.

| Option | How it works | Advantages | Costs and limits |
| --- | --- | --- | --- |
| 1: Four hours within declared response coverage — selected | Administrator and deputy define their actual coverage and worst-case response; drills include detection and that response in the four-hour budget. | Matches the accepted architecture and makes the response commitment testable without assuming continuous staffing. | Outages outside coverage can last longer; reports must expose that duration and coverage limits. |
| 2: Four hours at all times | Arrange continuous accountable response with escalation and independent operator access, and prove that detection, response and restoration fit the same budget at any time. | Provides one recovery commitment regardless of when failure occurs. | Needs continuous coverage and additional operating commitment; a notification alone cannot supply it. |

The recommendation adopted is Option 1 because this internal MVP has no accepted round-the-clock response commitment. Revisit it with Administrator if external adoption or observed incidents justify Option 2. Coverage hours, deputy access and acknowledgement bounds must be named before the first qualifying drill.

### Recovery authority, erasure and external effects

AD-12 separates authoritative restore, rebuild-only data and live authority. Memories Redis/FalkorDB projections and tenant-keyed coordination rebuild through authoritative replay; copied register data is not proof of live tombstone authority. Memories/EventStore must qualify the independent synchronous complete tombstone mirror and lineage protocol before G2. No acknowledged Memories tombstone, erased tenant or erased key may be resurrected under the ordinary one-hour RPO; unknown lineage fails closed. Key custody must make every earlier key backup unusable for an erased tenant while preserving recovery of other tenants.

The exercised order is: fence old writers and prove their credentials fail; recover the tenant-key store and reapply tombstone key destruction; restore authoritative data into quarantine; run module admission/purge before permitted replay; rotate restored credentials/signing keys and reapply post-cut revocations; verify integrity, release smokes and access; resume protection and monitoring before reopening. Compromise recovery also rotates the Dapr trust root and realm keys. Environment creation or authority-population startup tasks do not silently rerun during recovery.

For non-Memories erasures, deletions and legal holds acknowledged inside the lost window, the user accepted the ordinary RPO limitation. The recovery report states that window. Destructive retention and external-effect workers stay disabled until their module owner reconciles restored state and external-operation outcomes; unknown provider outcomes are never blindly repeated. This exception does not relax Memories continuity or other module functional and authorization rules.

### Source grounding

- [Kubernetes etcd backup guidance](https://kubernetes.io/docs/tasks/administer-cluster/configure-upgrade-etcd/) covers recovery of Kubernetes state. It does not establish backup coverage for application databases or persistent-volume contents; those must be inventoried separately.
- [Velero CSI snapshot guidance](https://github.com/vmware-tanzu/velero-plugin-for-csi#warning) explains why snapshots retained only on primary storage can be lost with that storage. This supports the independent-copy requirement, not selection of Velero or verification of the local storage provider.
- [PostgreSQL continuous archiving](https://www.postgresql.org/docs/current/continuous-archiving.html) illustrates recovery using base backups plus archived transaction logs; [standby replication](https://www.postgresql.org/docs/current/warm-standby.html) describes replication trade-offs. The selected recovery targets and retention periods are project requirements, not vendor guarantees.

## Hosted architecture questions

The [accepted architecture](../../architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md) resolves the earlier topology, identity, execution and recovery questions as follows. The designated Kubernetes installation remains `192.168.1.30`, with staging under `hexalith.com` and production under `tache.ai`. The architecture records the shared single-node kernel as a residual risk; the address alone was never evidence of topology or availability.

| Concern | Accepted decision |
| --- | --- |
| Shared hosting and module ownership | Platform owns the integrated composition and deployment; domain modules own behavior, UI, readiness, checks and declarations. One Platform-composed EventStore host per environment contains compatible module extension packages. Domain-module hosting is frozen until parity-based retirement; technical-module own-repository hosts remain valid. |
| Environment isolation | Staging and production have separate application and data namespaces, application data-service instances, OpenBao, volumes, credentials and external-provider tenancy. Shared capacity and shared Keycloak remain permitted. Application deployment authority cannot mutate the data namespace. |
| Identity and admission | Separate staging/production realms on the existing Keycloak server derive from one versioned contract. Administrator alone grants the named production admission group. Defaults, self-registration, first login, identity mapping and staging membership never confer production admission. Production realm changes are Administrator-only; the selected deputy recovery procedure restores existing authority without granting general admission administration. |
| Boundary enforcement | Default-deny network and Dapr policies, restricted pod security, environment-owned hostnames/certificates and least-privilege backend principals require negative tests from staging users, credentials and pods. Local development keys do not become hosted trust. |
| Build and deployment execution | Public Platform/Builds workflows publish and attest retained artifacts on disposable hosted runners. An Administrator-controlled private operations repository supplies allowlisted deployment workflows to separate staging and production executors; the production executor is outside the application cluster. Credentials are environment-scoped and never available to PR/test code. An off-site executor runs recovery. |
| GitHub plan and control boundary | Remain on GitHub Free with a single-writer operations repository and enforcement at the executor/target through allowlists and OIDC or executor-held credentials. Revisit GitHub Team controls when a second writer is introduced. The deputy access amendment must preserve this boundary or explicitly trigger that review. |
| Secrets and infrastructure | Environment-current secrets and authority advance independently of application rollback. OpenBao uses per-app bootstrap credentials with named renewal owners; tenant-key custody is separate. The environment profile inventories supported, security-current runtime/data-service versions and qualified artifact combinations. |
| Runtime integration | Dapr/shared SDKs provide infrastructure access, with named Memories FalkorDB, Redis search/vector and preflight-dedup adapter exceptions. Other direct Redis coordination is transitional and gates G2, not local/CI enrollment. New exceptions need recorded architecture-owner acceptance. |

Platform's MVP envelope governs the stricter Folders and Projects infrastructure clauses: one-hour ordinary-data RPO, four-hour disaster RTO under declared coverage, independent backup/restore, no HA or standby requirement and no uptime percentage. Projects retains its module process/task recovery behavior; its durable task engine is outside Platform MVP. Module behavior, authorization, erasure safeguards and release gates remain binding. Upstream documentation alignment is maintenance, not an additional enrollment gate.

### Remaining qualification and ownership

These are evidence and implementation prerequisites. Final PRD or architecture status does not satisfy them.

| Work | Owner | Required boundary |
| --- | --- | --- |
| Declaration schema/validator, environment descriptor, source mapping and lifecycle | Platform with module owners | Before affected module enrollment and fixture migration; demonstrate exact cleanup, retained-run inspection and package parity. |
| Composed host, extension API, catalog/metadata and authenticated McpCli | EventStore, McpCli and Platform | Before dependent enrollment and FR-12 acceptance; prove contract compatibility and surface/actor enforcement. |
| Shared runtime/profile and Aspire-to-Helm qualification | EventStore and Platform | Before hosted qualification; ratify catalog/secret instances, durable broker/profile, retained artifact behavior and exporter fallback criteria. |
| Realm, secrets, executor access and isolation | Platform, Administrator and module owners | Before hosted readiness; prove per-environment boundaries, negative tests and supported infrastructure. |
| Deputy operational access, alerts and rehearsed recovery | Administrator with Platform | Amend architecture controls and prove the deputy's own identity, minimum permissions, key custody, notification delivery and restoration/reopening authority before counting deputy coverage. Administrator alone resumes promotions. |
| Exposure/DNS/certificates, monitor and infrastructure currency | Administrator with dependency owners | Before G1; name access paths and owners and verify supported, patched infrastructure. |
| Prepared capacity, response coverage, key custody and recovery drill | Administrator/deputy with dependency owners | Before G2; identify location, custody, response/acknowledgement bounds and representative data size, and prove the original-outage RTO and continuous RPO. |
| Memories continuity and recovery conformance | Memories, EventStore and Platform | Before G2; prove tombstone lineage, erasure-safe key custody, replay and adapter boundaries. |
| Release/attempt controls and notification evidence | Platform, Builds and Administrator | Before G3; resolve EventStore retention/activation/lifecycle confirmations and prove provenance, one recovery attempt, concurrency/interruption controls and actual GitHub delivery. |

Accepted risks remain the shared-node failure domain, GitHub as the single notification path, GitHub Free controls, prerelease hosting/exporter dependencies, whole-site loss without independent replacement capacity, and non-Memories erasures inside the lost RPO window. The architecture permits the recorded pre-production Kubernetes support exception only until G1. Concrete version currency is checked through the profile inventory; historical stack observations do not establish a qualified production combination.

Prior external references in this addendum preserve the original options' technical context. This update reconciles accepted internal decisions; it does not reverify those external pages, installed versions or live infrastructure.
