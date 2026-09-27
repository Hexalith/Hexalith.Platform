---
title: Hexalith Platform Product Requirements
status: final
created: 2026-09-27
updated: 2026-09-27
---

# PRD: Hexalith Platform

## Document purpose

This PRD defines the requirements for the team's internal Hexalith Platform. It builds on the [completed platform brief](../../briefs/brief-platform-2026-09-27/brief.md) and [brief addendum](../../briefs/brief-platform-2026-09-27/addendum.md), supplemented by product-owner decisions captured during PRD coaching. This revision reconciles the [2026-09-27 validation findings](validation-report.md) with the accepted [Platform architecture](../../architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md). Product requirements belong here; implementation decisions and supporting rationale belong in the [PRD addendum](addendum.md).

This document sets the product requirements for architecture and implementation planning. Implementation mechanisms and readiness evidence are assigned to the downstream owners listed at the end; the requirements do not establish deployed capability.

The [update summary](update-2026-09-27/update-summary.md) records the resolved findings, selected policies and final document checks.

## Vision

Hexalith Platform gives the team one common solution for deploying the environments needed for development, testing, staging, and production across Hexalith. Platform owns shared hosting, while modules retain their domain behavior.

The existing optional Works development preview does not supply the complete seven-module environment or reusable environments for individual-module work. Platform addresses that gap by giving developers and CI a consistent composition and carrying verified releases into isolated hosted environments.

Developers use Platform both to run the complete supported system on their own machines and to work on an individual module with only its required dependencies. Individual-module development must run the checkout being edited, so source changes and breakpoints affect the running code. Local tests and automated CI tests also obtain their required environments through Platform.

Staging and production run on the designated Kubernetes installation and may share supporting infrastructure while keeping application data and credentials isolated. Staging access does not grant production access: a user must be explicitly declared as a production user. Every production release must pass end-to-end tests for critical business flows in staging. Automatic promotion additionally requires demonstrated rollback compatibility and recovery readiness; an Administrator-approved path supports earlier or incompatible releases with an explicit recovery plan.

## Target users and jobs

- **Developers:** run, test, and debug the supported system locally. Tenants, Parties, Folders, and Projects use their module workspaces and minimum environments; EventStore, Memories, and McpCli are debugged from source in the Platform workspace.
- **Team members accessing hosted environments:** access the supported modules in staging or production, including access through McpCli.
- **Automated CI workflows:** obtain the environments required to execute automated tests.
- **Administrator (recovery owner):** receives deployment, recovery, and backup-failure notifications through GitHub, takes over when automatic recovery fails or cannot be verified, and follows the disaster recovery procedure when production data or infrastructure must be restored.
- **Recovery deputy:** a named person who receives recovery alerts, has independent recovery/key access, and can execute the documented recovery procedure, verify restoration and reopen service when Administrator is unavailable. Administrator alone authorizes resumption of promotions and production-user administration.

User journeys are represented by capability and acceptance scenarios because this PRD governs developer orchestration and module-provided interfaces; module business journeys remain owned by the modules.

## Confirmed MVP scope

The MVP covers EventStore, Tenants, Parties, Folders, Projects, McpCli, and Memories across local development/debugging, local tests, automated CI tests, staging, and production. It supports both a complete environment and the minimum environment needed for individual-module development. Platform's longer-term scope includes all Hexalith servers and components; the initial module set is not a permanent exclusion of the others.

The designated hosted Kubernetes installation is at `192.168.1.30`. Staging and production may share physical capacity and the existing identity provider; their application state and credentials remain isolated in separate environment instances as selected by the architecture. Its address does not establish cluster topology or availability. User access to production requires explicit production-user declaration.

### Release scope and production entry

A release identifies the complete intended composition of enrolled modules, including unchanged modules and their dependencies, together with retained application artifacts, compatible configuration and required checks. An enrolled module has supplied its module-owned declarations and qualification evidence for the target environment. Local or CI enrollment alone does not qualify a module for production. Incremental enrollment supports implementation; final MVP acceptance still requires all seven modules in the agreed environments.

Production promotion has two modes: **automatic**, after G3 and verified compatibility with the current working baseline; and **Administrator-approved**, with an authenticated approval record for a release before G3 or an incompatible release. Both require the same staging E2E gate, release identity, isolation, serialized changes, verification and recorded outcome. An incompatible release's approval must name its separately planned recovery procedure before production changes begin.

| Gate | Required evidence and permitted use |
| --- | --- |
| **G1 — Closed production deployment** | Supported cluster infrastructure and protected deployment/probe access. Production ingress remains closed to users, the production admission group is empty, and independent availability monitoring and GitHub notifications operate. |
| **G2 — Production user access** | Open user access only after isolation and access checks pass and the isolated recovery drill demonstrates FR-9/NFR-2, including Memories erasure continuity. Recovery access, capacity, notifications and response arrangements are verified before opening. |
| **G3 — Automatic promotion** | Enable automatic promotion only after SM-5 rehearsals demonstrate deployment verification, rollback, interruption handling and reporting. Administrator-approved releases and controlled rehearsals may precede G3; they do not bypass G2 for user access. |

After initial G1 deployment, Administrator may admit a designated synthetic production test identity solely for SM-4 qualification. Ingress remains limited to executor/probe sources, no general users are admitted, and the test grant is revoked after the check. Recovery drills use isolated identity copies. This restricted verification step does not constitute G2 opening.

## MVP non-goals

- A generic Platform mocking subsystem, fake-service catalogue, or per-service real/fake configuration switches. Lightweight doubles belong in module-owned isolated tests.
- Automatic rollback based on production error-rate or latency baselines in the MVP; revisit when operating evidence supports useful thresholds.
- Standalone module workspaces for EventStore, Memories, or McpCli, and execution of UI-only or human-confirmation operations through McpCli. Their source debugging and permitted operations remain in scope through the specified surfaces.
- Automatic reversal of business data or event history during application rollback, or indefinite automatic switching between application releases.
- A standby production installation or automatic infrastructure failover as an MVP recovery requirement; the selected disaster recovery approach is independent backups and a tested restore procedure.

## Features

### Local system development

Developers can use Platform to run the complete environment on their own machines through Aspire for testing and debugging.

#### FR-1: Start the complete local environment

A developer can start the MVP module set and its required supporting components through Platform.

**Testable consequence:** a local verification demonstrates that EventStore, Tenants, Parties, Folders, Projects, McpCli, and Memories are available together for testing and debugging, using the module-owned readiness checks and bounded startup policy in FR-4. McpCli availability is demonstrated by its local client connecting and executing declared eligible operations; it does not require a hosted McpCli service.

### Individual-module development

A domain-module workspace includes Platform as a direct Git submodule and runs the module being edited with its minimum environment. Each module developer owns the minimum dependency definition and supplies a module configuration listing the servers to enable. Domain modules require at least EventStore, Tenants, and Memories.

#### FR-2: Debug the active module checkout

A developer can run and debug a domain module (Tenants, Parties, Folders, Projects) from that module's workspace using Platform and only the components specified by the module configuration. EventStore, Memories, and McpCli must be directly declared in Platform and run and debugged from source in that workspace; this is an enrollment requirement, not a claim that every declaration already exists.

**Testable consequences:**

- Source changes and breakpoints affect the running module from the active module checkout, including the EventStore, Memories, and McpCli checkouts in the Platform workspace.
- Dependency initialization follows only the active root repository's direct declarations under `references/`; nested submodules remain uninitialized.
- The active module and directly declared Hexalith dependencies use source and Debug assets locally; other dependencies use packages at the shared Builds catalog version. CI/CD uses NuGet package references and Release assets. Missing required declared source or duplicate source/package identities fail explicitly, without sibling, ancestor, nested-Platform or package fallback.

#### FR-3: Run the developer-defined minimum environment

A module developer can define the servers required for development and testing in the module configuration, and Platform uses that configuration to compose the minimum environment.

**Testable consequences:**

- The module developer controls the required server list for that module's development and testing.
- A domain module's minimum environment includes EventStore, Tenants, and Memories, together with any additional dependencies its developer declares.
- For Parties, Platform runs the active Parties checkout with EventStore, Tenants, and Memories according to the Parties module configuration; unrelated domain modules are not required.
- EventStore, Memories and McpCli use module-owner-declared minimum compositions from the Platform workspace. Their technical roles do not require inventing domain dependencies or a separate module-workspace workflow.
- Dependencies continue to follow the active root repository's direct-reference policy in FR-2.

The module configuration defines one server list for development and integration testing in the MVP. Platform runs these servers as real services. The repository configuration-file example is preserved in the [addendum](addendum.md#module-owned-configuration-example).

### Local and CI testing

Module developers run isolated tests without starting Platform. Local integration-test environments use Aspire to run Platform's configured real services. Platform also supplies real-service environments for CI integration tests. Failed local test environments remain available for debugging; CI environments are automatically cleaned up after completion or cancellation. Passing isolated tests does not replace evidence that the module works with its required services.

#### FR-4: Provision real-service integration tests locally and in CI

A developer or automated CI workflow can provision the configured environment through Platform and execute integration tests against its real services.

**Testable consequences:**

**Composition and readiness**

- Parties integration tests run against real EventStore, Tenants, and Memories, plus any additional dependencies selected in the Parties module configuration.
- Local integration-test environments run through Aspire using the module configuration and the active source checkout.
- The configured environment supports both local and automated CI integration runs, following the local Debug/project-reference and CI Release/NuGet rules in FR-2.
- Platform owns the integration environment lifecycle; tests consume its identified endpoints and composition. A technical module's independent repository tests may use its own fixture, but do not count as Platform integration evidence. CI environments use disposable runners without staging or production credentials.
- Each module defines readiness for its services. Test execution waits until the selected module and its required dependencies report readiness and any required one-off startup tasks complete successfully. A running process alone does not establish readiness; a required service without usable readiness evidence is a configuration error.
- The default overall environment-startup deadline is **10 minutes**, from the request to start the environment until required resources are ready. The module's test configuration may explicitly override it with a justified finite timeout; diagnostics show the effective value. This deadline excludes test execution and does not change production rollout deadlines.
- Definite startup failure can fail the run before its deadline. A timeout identifies resources that were not ready and preserves diagnostic evidence.

**Run isolation and ownership**

- Test data is isolated so tests do not depend on data left by other tests or affect staging or production application data.
- A new suite or compatible batch gets a fresh run-owned environment by default, or fails with the conflicting run identified. Explicit attachment requires compatible composition, artifact mode, readiness and data isolation in a local or CI run-owned environment; attachment to hosted environments is refused.

**Completion, cleanup and diagnostics**

- The environment can serve a test suite or compatible batch. A failed local test or startup attempt leaves surviving environment resources available for debugging; the developer can explicitly stop and clean up the retained environment.
- A successful local run cleans up automatically. The run's first terminal outcome determines cleanup or retention; later cancellation must not erase an environment retained after failure. Retained environments show their owner and age.
- Explicit cancellation of a local test run stops that run and cleans up resources it created. An Aspire environment started separately by the developer remains running.
- CI automatically cleans up its test environment after success, failure, or cancellation, including resources created before provisioning failed.
- Cleanup affects only the resources belonging to that test environment; it must not remove resources belonging to another active run or a hosted environment.
- Cleanup can be retried safely and reports remaining owned resources; an incomplete cleanup is not recorded as complete.
- Test results and useful diagnostic logs remain available after environment cleanup; keeping live resources is not the only means of diagnosing a failure.

#### FR-5: Keep isolated tests independent of Platform

A module developer can run isolated tests directly from the module's test project, using lightweight test doubles where needed, without starting Platform.

**Testable consequences:**

- Isolated tests can execute without starting the configured domain-module servers.
- CI runs isolated tests first and real-service integration tests afterward; passing isolated tests alone does not satisfy the module's integration acceptance.
- Platform development and integration environments continue to run their configured real services regardless of test doubles used inside isolated tests.

### Staging validation and production promotion

Each enrolled module defines its own critical business flows or essential service/tool behavior. Platform uses those module-defined flows to gate every production promotion, including Administrator-approved releases. The release includes unchanged enrolled modules, so changing one module does not omit the others' required checks.

#### FR-6: Gate production promotion on critical-flow E2E tests

Platform must require passing staging E2E results for all designated critical flows before promoting a release to production in either release mode.

**Testable consequences:**

- The critical flows used by the gate come from every enrolled module in the complete release; each module owns its definitions. Infrastructure and tool modules declare their essential behavior, including McpCli candidate flows, rather than being silently exempted for lacking business journeys.
- Each included module supplies a non-empty critical-flow declaration and required E2E checks for every declared flow. Missing or invalid declarations, empty check sets, and unresolved flow-to-test mappings block promotion.
- The required E2E tests exercise the release being considered for promotion in staging, using the deployed real services.
- Any required E2E test that fails, is skipped, is incomplete, or has no completed passing result prevents promotion; an absent test result is not a pass.
- Passing isolated tests, integration tests, or service health checks alone does not satisfy this E2E requirement.
- Passing results identify the served release, required check suite, runtime profile and configuration. The same retained application artifacts are promoted; stale, mismatched or wrong-release evidence is refused. Platform and Builds define and enforce a finite evidence-age limit before enabling the gate.
- Automatic promotion additionally requires G3 and passing compatibility evidence against the current production working baseline under NFR-1. Modules supply change classifications; Platform supplies the combined staging rehearsal. Missing, failing, stale or wrong-baseline compatibility evidence blocks the automatic path.
- An authenticated Administrator approval permits a pre-G3 release or a separately planned incompatible release. It never substitutes for the staging gate, artifact provenance, environment isolation or production verification. If safe automatic rollback cannot be demonstrated, approval names the recovery procedure and its acceptance checks before the attempt.
- A single controlled attempt owns changes in each environment, including application, configuration, routing, security and recovery changes. Shared-infrastructure changes coordinate both environments; concurrent work must not replace the release being tested or invalidate its evidence.

The concrete flow lists belong to the modules and must be supplied when their release checks are integrated with Platform. Changes to declarations or checked inputs require matching evidence. They are not enumerated centrally in this PRD.

### Production verification and recovery

Platform verifies production with readiness checks and a short smoke-test suite owned by the enrolled modules. These checks supplement the full critical-flow E2E gate in staging. If a compatible deployment fails, Platform makes one automatic attempt to restore the previous working application and verifies the result. An approved incompatible release follows its separately recorded recovery procedure.

#### FR-7: Verify production deployment and detect failure

Platform must verify that the intended release becomes ready and performs the required production-safe operations before declaring it a working release.

**Testable consequences:**

- Each enrolled module provides its required smoke tests. Read-only checks are preferred; necessary writes use dedicated synthetic identities and data without real-user changes or external effects such as notifications.
- Before updating production, Platform validates the required readiness and smoke-check declarations for every included module. Missing or invalid declarations and empty required check sets block the update; discovering no checks is not a passing result.
- Before updating production, Platform identifies the working baseline, verifies that it is healthy now, and checks required module release qualifications and current environment/security configuration. Recovery must combine the previous application with compatible current environment values. Missing preconditions stop the attempt before mutation. First-deployment handling is defined in FR-8.
- Deployment fails if required services have not reached the intended release and become ready within **10 minutes of deployment start**.
- After rollout readiness, Platform starts a **five-minute verification window**. Any required service unable to serve traffic for **60 continuous seconds** during this window triggers recovery. A single failed probe or container restart does not by itself trigger rollback.
- A required smoke test failing **twice consecutively**, with the second attempt **30 seconds after the first failure**, triggers recovery. A required check without a completed passing verification result at the deadline also fails deployment; skipped or missing results do not pass.
- Availability is sampled at least every **10 seconds**; smoke checks have stable identities and run at the window start and a declared finite cadence. A check timeout fails the check. Results bind the intended served release. A bounded grace may finish an in-flight retry but cannot convert a missing pass at the verification deadline into success.
- Verification targets the intended release. Platform declares success only when required services are ready and the latest required smoke-test results are passing at the end of the verification window, with no failure trigger having fired.
- Interruptions do not reset elapsed deadlines or replenish recovery attempts. A gap in observation invalidates verification; uncertainty about release identity, ownership or records stops changes pending intervention. A bounded continuation of the same attempt may use only its remaining recovery allowance.

These are initial policy defaults to validate with staging evidence. They govern deployment acceptance; they are not measured availability guarantees.

#### FR-8: Restore and verify the previous working release

When FR-7 detects a failed compatible production deployment, Platform must make one automatic recovery attempt to the recorded previous working application with compatible current environment configuration. Approved incompatible releases use the separately planned recovery required by FR-6.

**Testable consequences:**

- Recovery restores application components and routing changed by the attempt, including partially updated workloads, to the previous working behavior while retaining current data and security authority. If neither workloads nor routing changed, Platform retains the existing release and reports the failed attempt. Reversing a first module enrollment removes application workloads, never durable data objects.
- Recovery has **10 minutes to restore readiness**, followed by the same **five-minute verification window** and failure thresholds used for deployment verification. It uses the recorded production-safe smoke suite applicable to the restored release, rather than checks for features introduced only in the failed release. Completing a rollback command alone does not establish recovery success.
- Platform reports every deployment failure and recovery result, including successful recovery, through GitHub to Administrator and the named deputy. Notifications identify the release, environment, status and access-controlled diagnostic evidence. Delivery and the deputy's independent recovery access are demonstrated before G2.
- Every non-working production outcome and disaster recovery entry sets a durable promotion stop that survives process, executor or cluster restart. Only an authenticated Administrator record naming the reason and verified current working release clears it. The deputy may perform documented recovery, verify restoration and reopen service, but cannot clear the promotion stop or administer production-user access.
- If the recovery attempt fails or its outcome cannot be verified because the cluster is unreachable, Platform reports failed or unverified recovery to the recovery owner for intervention. It does not cycle through older releases automatically.
- A first deployment has no previous working release; failure stops that deployment with user ingress closed and is reported without claiming a successful rollback.
- A manual recovery or disaster restore becomes the working baseline only after its required verification passes. Release decisions, approvals, attempt outcomes, diagnostics and the baseline remain retrievable independently of the failed environment.
- Failures found after the verification window are operational incidents, outside automatic deployment recovery. An incident establishing that production is no longer working sets or retains the promotion stop until Administrator records a verified baseline; it does not trigger an automatic search through older releases.

### Production backup and disaster recovery

Platform supports frequent independent backups and an exercised restore procedure. Each module identifies its authoritative state and recovery needs; the recovery owner restores a compatible application and data state after major production server or storage failure.

#### FR-9: Back up and restore production state

The recovery owner can restore production from usable recovery points outside the primary server/storage failure domain and verify the restored service against the targets in NFR-2.

**Testable consequences:**

- Backup coverage includes the authoritative databases, files, and configuration identified by each deployed module, plus Platform's retained releases and recovery records. EventStore history alone is not assumed sufficient to restore every module. Modules classify state as authoritative, rebuild-only or dependent on surviving live authority, and supply the required restoration, reconciliation and integrity checks.
- The recovery inventory includes required identity, secret and other shared dependencies, including Keycloak, production access configuration and revocation evidence. Each dependency has an identified recovery owner and evidence that it remains available or can be restored within the recovery procedure.
- Backup or incremental-copy runs start every **30 minutes**. Retention provides frequent recovery points for **seven days** and daily recovery points for **30 days**, including the base backups and incremental data needed to restore them.
- A usable recovery point is a complete, verified cross-module recovery set at one declared cut, with compatible release/configuration identity and required security and erasure context. Integrity, complete incremental chains and decryption are verified when the point is recorded. Independently timed snapshots or successful jobs alone do not establish a usable point; RPO age is measured from the declared cut.
- Recovery points are encrypted, immutable, restricted and off-site. Required artifacts, access and decryption material remain independently available to Administrator and the deputy, with tenant-key custody separated from ordinary data backups. Prepared replacement capacity is identified and exercised. Off-site backups alone do not prove whole-site recovery capability.
- Independent monitoring checks the newest complete recovery point at least every **15 minutes**, warns before its age reaches one hour and notifies Administrator and the deputy through GitHub on backup failure or age over **one hour**. Production availability is probed at least every **five minutes**, from G1 onward. Failure reporting survives loss of the primary environment; an independent hourly check detects monitor silence.
- Disaster recovery fences the failed environment and restores into quarantine. Before reopening, rotate restored credentials, reapply post-cut revocations, and verify compatible application/configuration versions, cross-module integrity, restored-release smoke tests and all NFR-3 access outcomes, including denial of revoked principals. Re-enable backups and monitoring before reopening service.
- Memories recovery must preserve every acknowledged erasure tombstone and prevent resurrection of erased tenants or keys; unknown authority lineage fails closed. The ordinary one-hour RPO does not relax this guarantee. For other modules, deletions, erasures or legal holds acknowledged only inside the lost RPO window may be lost under the accepted MVP recovery envelope; report that window and complete module-owned reconciliation before resuming destructive retention or external effects.
- An isolated restore exercise runs **before G2, monthly thereafter, and after material storage or backup changes**. It assumes primary server and storage loss and proves that detection, worst-case response within declared coverage, replacement capacity, restore and verification meet the NFR-2 targets. It records recovered-data age, full elapsed time, coverage and every substituted dependency. Drills use isolated copies and cannot mutate live production or shared authority.

### Hosted environments and user access

Platform provides staging and production on the designated Kubernetes installation and integrates with the existing shared Keycloak server for security. Shared physical capacity and identity infrastructure must preserve separate environment state, credentials and authority; the same person may use both environments only with the required authorization for each.

#### FR-10: Provide isolated staging and production environments

Platform must host the supported MVP modules in staging under `hexalith.com` and production under `tache.ai` on the designated installation at `192.168.1.30`.

**Testable consequences:**

- The MVP module set is accessible in both environments through its supported interfaces. Each module declares its external surfaces, required authorization and exposure classification; a disabled or private surface is not advertised as available. McpCli runs on the caller's host and reaches the selected environment through its permitted gateway.
- Staging and production use separate application data and credentials even where a supporting service or its capacity is shared.
- Environment configuration and release promotion preserve those boundaries; deploying an application release does not copy staging data or credentials into production.
- Staging workload or automation authority cannot claim production hostnames or administer production state. Resource limits protect production capacity against staging exhaustion; shared hardware does not establish node or site resilience.

#### FR-11: Require explicit production-user access

A staging user must be denied production access unless explicitly declared as a production user.

**Testable consequences:**

- A user authorized only in staging cannot access production data or operations through any supported interface, including direct API, CLI, or MCP access.
- Explicit production-user declaration permits access according to the user's assigned production permissions; it does not imply unrestricted permissions.
- A user authorized in both environments can use each with the permissions applicable there.
- Staging membership and permissions do not automatically create production membership or permissions, including during release promotion.
- Administrator alone grants and revokes explicit production admission through an authenticated, auditable action. Self-registration, first login, identity mappings and staging administration cannot grant it. Recovery authority does not grant production-user administration.
- Access integrates with the existing common Keycloak server. Authentication through that shared provider alone does not grant production access; explicit production-user authorization is still required.

### Module operations through McpCli

McpCli provides CLI and MCP access to the agent-eligible commands and queries defined by each enabled module in local Aspire, staging, and production environments. This restriction applies equally to human CLI callers and agents. Modules retain ownership of their operations and domain behavior; Platform supplies the environment and integration.

#### FR-12: Access enabled-module operations in the selected environment

A user can discover and invoke the commands and queries published by enabled modules through McpCli, subject to the user's permissions in the selected environment. In the MVP, McpCli executes only operations that their owning module declares eligible for agent use; operations restricted to a module UI or requiring human confirmation remain available only through that UI.

**Testable consequences:**

- An operation not declared eligible for agent use, or one requiring human confirmation, is refused through McpCli, even for a user permitted to perform it elsewhere.
- The available operation set comes from the enabled modules' definitions; Platform does not maintain separate business implementations of those operations.
- Offline contract inspection does not establish executable availability. Connected discovery must agree with the selected environment's enabled operation definitions and contract versions; unknown or mismatched contracts are non-executable.
- CLI and MCP access work against local Aspire environments and hosted staging/production environments, routing operations through EventStore in the selected environment.
- An invocation executes only against the selected environment. User permissions are enforced there for both commands and queries; choosing an environment or interface does not grant additional access.
- A staging-only user cannot execute production operations through McpCli. An explicitly authorized production user can execute only the operations permitted by that user's production permissions.
- Operations from a disabled module are unavailable in that environment; invoking one does not implicitly enable the module or route to another environment.
- Server-side checks enforce authenticated caller identity, calling surface and current permissions on every invocation. Caller-supplied actor or surface values cannot grant authority; using a public client's token for a direct gateway call must not bypass UI-only or human-confirmation restrictions.
- Acceptance names the enrolled operations actually demonstrated, proves allowed invocation through CLI and MCP, and proves refusal of ineligible, disabled, mismatched-contract and unauthorized operations. An empty executable catalog cannot satisfy the positive demonstration.

The accepted architecture uses `Hexalith.McpCli` and the selected environment's EventStore gateway; McpCli stdio is the MVP MCP surface and its CLI head is the Hexalith-owned command-line surface. Proprietary module and technical-module MCP servers, plug-ins, and CLIs are obsolete migration sources, including EventStore Admin tools. Platform does not add or publish another proprietary Hexalith MCP/CLI surface. A legacy surface is retired after its owner-approved operation inventory, replacement or withdrawal decision, authorization, compatibility, and acceptance evidence pass. Enrollment, authentication and readiness qualification remain implementation work under the [McpCli context](addendum.md#mcpcli-context).

## Cross-cutting non-functional requirements

### NFR-1: Preserve data through application rollback

Application rollback must preserve business data, event history, credential rotations, revocations and current security authority. It restores the previous application and compatible current environment configuration without rewinding those resources or shared infrastructure.

Releases using automatic rollback must keep data schemas, newly written events and required routing/security state compatible with the previous working application version. Module change classifications and a staging candidate-to-baseline rehearsal must prove the baseline reads candidate-written state and events, including a newly introduced idempotent command where applicable. Keys, secrets and routing entries needed by that recovery combination cannot be retired before the candidate becomes working. Missing or invalid compatibility evidence blocks automatic promotion; an incompatible change requires the Administrator-approved release and separately planned recovery procedure in FR-6.

The accepted architecture defines the retained artifacts, forward routing generation and evidence binding; implementation must qualify them before use. Disaster recovery from backups has the separate data-loss and recovery-time targets in NFR-2, with FR-9 security and erasure protections; those targets do not permit application rollback to rewind business data.

### NFR-2: Bound production data loss and disaster recovery time

For major production server or storage failure requiring backup restoration:

| Objective | Target | Measurement |
| --- | --- | --- |
| RPO | At most **one hour**, continuously | Failure time minus the declared cut of the newest complete usable recovery point; accepts up to one hour of committed-data loss subject to FR-9 erasure and security protections. |
| RTO | At most **four hours** for outages beginning within declared response coverage | Service outage to verified restoration, including detection, operator response, replacement capacity, restore, and validation. An incident beginning within coverage remains covered when the scheduled coverage ends. |

These are accepted product targets, not demonstrated infrastructure guarantees. Before G2, Administrator publishes response coverage with its time zone, primary/deputy responsibility and maximum acknowledgement delay, and identifies prepared replacement compute/storage. A restore exercise must include the declared worst-case response and prove the targets. If evidence does not support a target, address the missing capability or explicitly revisit the target with the recovery owner before opening production.

Outside declared response coverage, no four-hour recovery commitment is made. Every incident still records full outage-to-restoration duration and coverage status; the clock never pauses or restarts when coverage begins. Backup protection, the one-hour RPO and monitoring remain continuous. These targets establish neither an uptime percentage nor continuous response coverage. Whole-site loss is covered only when exercised replacement capacity is at an independent location.

The Platform MVP recovery envelope governs stricter infrastructure RPO/RTO or availability expectations in Folders and Projects. It does not waive module behavior, authorization, erasure protections or release gates; module-owned process/task recovery remains distinct from platform disaster recovery.

### NFR-3: Enforce isolation on shared infrastructure

Sharing infrastructure must not give staging users, workloads, pods or automation access to production data, credentials or administrative authority. Deployment, smoke-test, backup, recovery and identity-management identities have only their declared environment and purpose permissions; CI receives no hosted-environment credentials. Access enforcement holds at direct service, data, routing and administration boundaries. Restored production copies remain restricted production data even in a recovery environment.

Verify rejection of staging-only users, application credentials, automation credentials and pods attempting production operations, data/secret access, routing claims or identity administration, including direct requests and restored copies. Also prove permitted access for an explicitly authorized production user and denial after revocation. The existing Keycloak server remains the identity provider; accepted environment and identity mechanisms are recorded in the [addendum](addendum.md#hosted-architecture-questions). Repeat the relevant checks after access changes and restoration.

## Success measures

These measures operationalize the five brief outcomes and the disaster recovery outcome added during coaching. They are acceptance targets; this PRD does not claim they have been achieved. Platform owns combined acceptance evidence, module developers supply their readiness/operation/critical-flow checks, and Administrator owns recovery evidence. Record the release or source revision, environment, result, and supporting diagnostics with each demonstration.

- **SM-1 — Complete local environment:** all seven MVP modules and required supporting resources become ready under the effective FR-4 startup deadline; CLI and MCP demonstrations execute enabled modules' agent-eligible commands and queries with the expected results. Validates FR-1 and FR-12 at MVP acceptance.
- **SM-2 — Module workspace development:** each domain-module workspace (Tenants, Parties, Folders, Projects) demonstrates its developer-defined minimum composition and a breakpoint or source change in the active checkout affecting the running module; EventStore, Memories, and McpCli demonstrate the same source-debugging outcome from the Platform workspace. Only direct root-declared dependencies are initialized. Validates FR-2 and FR-3 at MVP acceptance.
- **SM-3 — Test execution and lifecycle:** every MVP module demonstrates its declared minimum composition and required local Aspire/CI integration checks from its FR-2 workspace. Success, failure, startup-timeout, cancellation, collision and attachment scenarios demonstrate the FR-4 ownership/retention/cleanup rules, leftover reporting and accessible diagnostics without affecting another environment. Validates FR-4 and FR-5 at MVP acceptance and after lifecycle changes.
- **SM-4 — Hosted access and isolation:** staging and production expose declared supported interfaces and named agent-eligible McpCli operations; allowed operations succeed and every FR-11/FR-12/NFR-3 refusal check passes for users, workloads and automation. A controlled explicitly admitted test user supplies positive production evidence before general user admission. Validates FR-10 through FR-12 and NFR-3 before G2, after access changes and during recovery verification.
- **SM-5 — Promotion and recovery:** both release modes enforce exact-release staging E2E evidence for the complete enrolled composition; automatic promotion rejects invalid compatibility evidence and requires a candidate-to-current-baseline rehearsal. Controlled deployment failures exercise FR-7 and verify one FR-8 recovery within its budgets, preserving NFR-1 state. Separate scenarios cover interruption, routing-only changes, first deployment, failed/unverified recovery, durable promotion stop and Administrator-only resumption. Verify GitHub delivery and deputy recovery access; an incompatible approved release demonstrates its separately planned recovery acceptance. Validates FR-6 through FR-8 and NFR-1 before G3 and after recovery-policy changes.
- **SM-6 — Recoverable production data:** an isolated drill proves complete recovery sets, continuous RPO at most one hour and RTO at most four hours under declared coverage, including worst-case response, capacity, security, erasure, data and smoke-test checks. Verify backup cadence/retention, independent availability/freshness monitoring, monitor-silence detection and GitHub delivery to Administrator and deputy. Validates FR-9 and NFR-2 before G2, monthly, and after material backup/storage changes. Record full outage duration and coverage status; monitor recovery-point age between exercises.

### Counter-metrics

- **SM-C1 — Bypassed validation:** zero releases in either mode promoted with failed, skipped, missing, incomplete, stale or mismatched E2E evidence; zero automatic promotions without valid compatibility evidence for the current baseline and G3 qualification. Approval cannot waive common gates. Counterbalances SM-5.
- **SM-C2 — Unauthorized cross-environment access:** zero successful production operations, data/secret access or administrative changes in the staging-only user, workload and automation negative checks, including restored copies. Shared capacity savings must preserve isolation. Counterbalances SM-4.
- **SM-C3 — False recovery success:** zero recoveries reported successful without the required readiness, smoke-test, and data checks. Shorter recorded recovery time must not omit operator/provisioning time or hide data loss. Counterbalances SM-5 and SM-6.
- **SM-C4 — Destructive or incomplete cleanup:** zero deletions of another environment's resources, and no cancelled/finished CI run counted as cleaned up while its owned resources remain. Counterbalances SM-3.
- **SM-C5 — Hidden startup delay:** record actual startup duration and explicit timeout overrides alongside readiness results. Increasing a timeout must not be reported as improved startup performance. Counterbalances SM-1 and SM-3; no separate setup-time improvement target has been established.

## Downstream decisions and readiness evidence

The product rules above are settled. The accepted architecture defines the mechanisms summarized in the addendum; its passing handoff review establishes document readiness. The following implementation and qualification work remains. None of these documents establishes a passed production gate.

| Work | Owner | Revisit condition |
| --- | --- | --- |
| Module composition/readiness declarations, supported surfaces and agent eligibility, critical-flow/smoke suites, authoritative/rebuild/erasure inventories and integrity hooks | Each module developer | Before qualification for each target environment; preserve module product and release gates |
| Policy for reviewing removals or remapping of required critical-flow checks, with evidence identifying changed declarations | Module owners with Platform | Before release-gate qualification; matching digests alone do not establish that reduced coverage is acceptable |
| Staging execution triggers and rules for accepting rerun evidence without hiding failed or incomplete attempts | Platform with Builds | Before accepting the first staging release evidence; retain attempt history and enforce FR-6 evidence identity and validity |
| Implement and qualify the selected root-source mapping, declaration/export contracts, Platform runner ownership and disposable CI lifecycle | Platform with Builds and module maintainers | Before accepting local/CI composition and testing evidence |
| Qualify McpCli enrollment and selected-environment contract matching, surface/actor authorization and named positive/refusal demonstrations | Platform, McpCli and EventStore maintainers | Before FR-12/SM-1/SM-4 acceptance |
| Qualify environment-specific identity/state, automation permissions, network isolation and explicit production admission | Platform with Administrator and module maintainers | Before G2 and after access changes; controlled test admission precedes general user opening |
| Supported infrastructure, ingress/DNS/certificates, executor/probe access, independent monitoring and verified GitHub delivery | Administrator with Platform implementation | Before G1; retain evidence of actual configuration and versions |
| Retained artifacts, evidence-age policy, attempt serialization, compatibility rehearsal, interruption/recovery bounds, notification delivery and promotion-stop clearance | Platform with Builds, EventStore and Administrator | Before applicable production attempts; SM-5 qualification before G3 |
| Complete recovery inventory, independent backups/artifacts/keys, named deputy access, declared response hours/time zone/acknowledgement bound, prepared capacity and representative recovery volume | Administrator with deputy and dependency owners | Before G2; demonstrate in the timed drill and repeat after material changes; whole-site coverage requires independent capacity |
| Memories tombstone/key continuity and module-specific recovery reconciliation | Memories/EventStore with other module owners and Platform | Before G2 and whenever recovery mechanisms change |
| Align architecture operational access and notification recipients with the selected deputy role; synchronize spec/acceptance wording for both release modes, G1–G3 and RTO coverage | Platform architecture/spec owners with Administrator | Before recovery/deployment stories are finalized; this PRD's later explicit decisions govern any stale downstream wording |

No numeric uptime percentage or additional latency/throughput target is established. Revisit with Administrator if external adoption or operating evidence requires a stronger service commitment.

## Glossary

- **Platform:** the shared Hexalith environment-provisioning and hosting solution defined by this PRD.
- **Module:** a Hexalith component whose domain behavior remains in its own repository.
- **Domain module:** a module that provides domain behavior and requires at least EventStore, Tenants, and Memories in its minimum environment; Parties is one example.
- **MVP module set:** EventStore, Tenants, Parties, Folders, Projects, McpCli, and Memories.
- **Complete environment:** the MVP module set together with the supporting components required to run it.
- **Module workspace:** the active Tenants, Parties, Folders or Projects repository used for that domain module's Platform development and testing.
- **Platform workspace:** the Platform repository used as the active root repository; EventStore, Memories, and McpCli are run and debugged from source there.
- **Minimum environment:** the components the module developer defines as required for development or testing. For domain modules, these include at least EventStore, Tenants, and Memories in addition to the module being worked on.
- **Module configuration:** the configuration maintained by the module developer that lists the servers Platform must enable for development and testing.
- **Enabled module:** a module whose services are enabled in the selected environment.
- **Enrolled module:** a module with the declarations and qualification evidence needed for inclusion in a Platform composition at the relevant stage; repository presence alone is insufficient.
- **Supported interface:** a module-declared external surface with explicit exposure and authorization rules that Platform qualifies for its selected environment.
- **Agent-eligible operation:** a module-declared command or query permitted through McpCli; UI-only or human-confirmation operations remain ineligible even for a human CLI caller.
- **McpCli:** the caller-hosted CLI and stdio MCP client exposing enabled modules' agent-eligible operations through EventStore, with selected-environment contract matching and authorization.
- **Isolated test:** a module-owned unit or focused component test that runs without Platform and may use lightweight test doubles.
- **Integration test:** a test that exercises the module with its configured real services through a Platform environment.
- **Critical business flow:** a module-owned business operation or essential service/tool behavior designated for mandatory E2E verification before production promotion.
- **E2E test:** an end-to-end test that verifies a business flow through the deployed services involved in delivering its outcome.
- **Smoke test:** a short production-safe check selected and maintained by an enrolled module to verify essential behavior after deployment or rollback.
- **Release:** an identified complete composition of enrolled modules, retained application artifacts, compatible configuration and required check suites, including unchanged modules and dependencies.
- **Working release:** an identified application release with compatible versioned configuration that has passed the required production readiness and smoke-test verification.
- **Working baseline:** the retained release identity and verified attempt record for the current working application; recovery combines its application artifacts with compatible current environment authority.
- **Promotion stop:** the durable block on further production promotion after a non-working outcome or disaster recovery; only Administrator may clear it after verified recovery.
- **Verification window:** the five-minute period after rollout readiness during which Platform checks production availability and smoke-test results before declaring deployment or recovery successful.
- **Recovery owner:** Administrator, the product owner, who receives deployment-failure and recovery-result notifications through GitHub and intervenes when automatic recovery fails or remains unverified.
- **Recovery deputy:** the named person with independent recovery/key access who receives alerts and can restore, verify and reopen service; cannot resume promotions or administer production-user admission.
- **Recovery point:** a complete verified set of module and shared-dependency recovery artifacts at one declared cut, with compatible release/configuration and required security/erasure context.
- **RPO (recovery point objective):** the maximum target age of recoverable data at failure, expressing the potential loss of recently committed data.
- **RTO (recovery time objective):** the maximum target duration from service outage to verified restoration, including detection, operator response, replacement capacity, data restoration, and validation.
- **Response coverage:** the published hours and time zone in which the primary/deputy commit to responding within a declared acknowledgement bound; determines four-hour RTO applicability without pausing the outage clock.
- **Active root repository:** the repository from which work is being performed and whose direct submodule declarations govern dependency initialization.
- **Staging:** the hosted environment published under `hexalith.com`, where checks gate production promotion.
- **Production:** the hosted environment published under `tache.ai`.
- **Production user:** a user explicitly declared as authorized to access production. Being a staging user alone does not confer this status or production permissions.
