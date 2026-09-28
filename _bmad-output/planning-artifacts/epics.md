---
stepsCompleted: [step-01-validate-prerequisites, step-02-design-epics, step-03-create-stories, step-04-final-validation]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md
  - _bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/addendum.md
  - _bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md
  - _bmad-output/specs/spec-platform/SPEC.md
  - _bmad-output/specs/spec-platform/acceptance-criteria.md
  - _bmad-output/specs/spec-platform/sequencing.md
  - _bmad-output/specs/spec-platform/success-measures.md
  - _bmad-output/specs/spec-platform/glossary.md
  - _bmad-output/specs/spec-platform/brownfield.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-27.md
  - _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-28.md
  - docs/ext-host-1-agents-composition.md
  - _bmad-output/planning-artifacts/briefs/brief-platform-2026-09-27/brief.md
  - _bmad-output/planning-artifacts/briefs/brief-platform-2026-09-27/addendum.md
---

# Hexalith Platform - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Hexalith Platform, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

**Source precedence.** The PRD holds the product requirements. The architecture spine (AD-1…AD-15 and its conventions) governs mechanism, and where it refines a PRD rule, the spine applies. The SPEC package is the preservation-validated contract. It maps CAP-N one-to-one to FR-N, and its acceptance criteria, sequencing and gates are authoritative for story acceptance. FR, NFR, SM, AD and CAP IDs are kept stable from their source documents. A requirement here states a target; it does not establish a deployed capability, a passed check or a production qualification.

## Requirements Inventory

### Functional Requirements

**FR-1: Start the complete local environment** (CAP-1). A developer can start the MVP module set and its required supporting components through Platform.
- EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories are available together for testing and debugging, using module-owned readiness checks and the FR-4 bounded startup policy.
- The complete environment uses the largest declared startup override, reported as an override.
- McpCli availability is shown by its local client connecting and executing declared agent-eligible operations; no hosted McpCli service is required.

**FR-2: Debug the active module checkout** (CAP-2). A developer can run and debug a domain module (Tenants, Parties, Folders, Projects) from that module's workspace, with Platform as a direct Git submodule, using only the components its module configuration specifies. EventStore, Memories and McpCli are directly declared in Platform and are run and debugged from source in the Platform workspace.
- Source changes and breakpoints affect the running module from the active checkout, including the EventStore, Memories and McpCli checkouts in the Platform workspace (Debug).
- Dependency initialization follows only the active root repository's direct `references/` declarations. Nested submodules remain uninitialized, and the Platform tool fails naming any nested submodule that has been initialized.
- The active module and directly declared Hexalith dependencies use source and Debug assets locally. Other dependencies use packages or released images at the Builds catalog version. CI/CD uses NuGet package references and Release assets.
- Missing required declared source, or both a source and a package copy of one identity, fails explicitly with the dependency and path named. There is no sibling, ancestor, nested-Platform or package fallback.
- The Platform tool (`hexalith-module`, pinned in `.config/dotnet-tools.json`) alone selects the mode. The Platform identity is the `references/Hexalith.Platform` submodule commit, or HEAD in the Platform repository. Package mode refuses identity, Builds-catalog or tool-range mismatches; source mode warns.

**FR-3: Run the developer-defined minimum environment** (CAP-3). A module developer defines in the module configuration the servers required for development and testing, and Platform composes exactly that minimum environment as real services.
- The module developer controls the server list; in the MVP there is one list for development and integration testing.
- A domain module's minimum environment includes EventStore, Tenants and Memories plus any additional dependencies its developer declares.
- Parties example: Platform runs the active Parties checkout with EventStore, Tenants and Memories as the Parties configuration specifies. Unrelated domain modules are not required. The Parties repository directly references EventStore, Tenants, Memories and Platform under `references/`.
- EventStore, Memories and McpCli use module-owner-declared minimum environments from the Platform workspace. They need no invented domain dependencies and no separate module-workspace workflow.
- Missing, duplicate or incompatible declarations fail validation.

**FR-4: Provision real-service integration tests locally and in CI** (CAP-4). A developer or automated CI workflow can provision the configured environment through Platform and execute integration tests against its real services.
- *Composition and readiness:*
  - Parties integration tests run against real EventStore, Tenants and Memories plus any selected dependencies.
  - Local runs use Aspire with the module configuration and active checkout. CI follows the Release/NuGet package-mode rule.
- *Candidate evidence:*
  - CI evidence for a module candidate shows the environment loaded the candidate's own builds of every artifact of that module, including extension packages. Artifacts are identified by content, not version label, and the environment reports the identities it actually loaded, including those inside the composed host and run-scoped McpCli.
  - Evidence with another build or revision, uncommitted local changes, a missing identity or a silently substituted cache is refused. A baseline-only run is labelled and cannot satisfy candidate acceptance.
- *Runner ownership:*
  - Platform (the `hexalith-module` runner) owns the environment lifecycle; tests consume its identified endpoints and composition descriptor and never start their own AppHost.
  - A technical module's own-repository tests may use its own fixture but do not count as Platform integration evidence.
  - CI uses disposable runners without staging or production credentials.
- *Readiness and startup:*
  - Test execution waits until the selected module and its required dependencies report module-defined readiness and required one-off startup tasks succeed. A running process is not readiness; a required service without usable readiness evidence is a configuration error.
  - The default overall environment-startup deadline is **10 minutes** from start request to all required resources ready. It excludes test execution and does not change production deadlines.
  - The module's test configuration may set a justified finite override, and diagnostics show the effective value.
  - A definite startup failure can fail early. A timeout names the unready resources and preserves diagnostics.
- *Run isolation and ownership:*
  - Test data is isolated, and tests never touch staging or production application data.
  - A new suite or compatible batch gets a fresh run-owned environment, or fails naming the conflicting run.
  - Explicit attachment requires compatible composition, artifact mode, artifact identities, readiness and data isolation in a local or CI run-owned environment. Hosted attachment is refused. New attachment after the owner finishes is refused unless the local environment was retained after failure.
- *Attachment holds:*
  - An accepted attachment holds automatic cleanup until every attached run ends or its finite hold limit, shown with the environment, expires. The owner's outcome is recorded when it occurs.
  - An attached run's cancellation never stops the owner's environment.
  - A run ended by hold expiry or withdrawal is recorded as withdrawn: neither pass nor failure, and never integration evidence.
- *Completion and cleanup:*
  - Local success cleans up automatically after holds end.
  - A failed local test or startup by the owner or any attached run retains surviving resources for debugging, listed with the failed run. Later cancellation must not erase failure retention, and the developer can explicitly stop and clean up.
  - Owner cancellation or an explicit developer stop first ends and reports attached runs as withdrawn, then cleans only the resources the owner created. A separately developer-started Aspire environment keeps running.
  - CI always cleans up after success, failure or cancellation, including partial provisioning. CI attachment is limited to the same CI job, and attached runs end before job cleanup.
  - Retained and pending-cleanup environments list their owner, age, failed runs and attached runs.
  - Cleanup touches only that environment's resources, never another active run or a hosted environment. It is safely retryable and reports leftovers, and incomplete cleanup is never recorded as complete.
  - Test results and diagnostics survive cleanup.

**FR-5: Keep isolated tests independent of Platform** (CAP-5). A module developer can run isolated tests directly from the module's test project, using lightweight test doubles where needed, without starting Platform.
- Isolated tests execute without starting Platform or the configured domain-module servers.
- CI runs isolated tests first and real-service integration tests afterwards. Passing isolated tests alone never satisfies integration acceptance.
- Platform environments always run configured real services regardless of test doubles inside isolated tests.

**FR-6: Gate production promotion on critical-flow E2E tests** (CAP-6). Platform requires passing staging E2E results for all designated critical flows before promoting a release to production, in either release mode.
- *Critical-flow declarations:*
  - Critical flows come from every enrolled module in the complete release, including unchanged modules. Each module owns its definitions, and Platform keeps no central list.
  - Infrastructure and tool modules declare their essential behavior, including McpCli candidate flows.
  - Each included module supplies a non-empty critical-flow declaration and required E2E checks for every flow. A missing or invalid declaration, an empty check set or an unresolved flow-to-test mapping blocks promotion.
  - Removing or remapping a critical flow needs module-owner review.
- *Staging E2E tests:*
  - Required E2E tests exercise the exact release being considered, deployed in staging against real services.
  - Staging E2E creates or deletes tenants only within a module-declared tenant-lifecycle critical flow, using run-scoped synthetic-marked tenants excluded from real-tenant views.
- *What counts as a pass:*
  - A failed, skipped or incomplete required test, or one without a completed passing result, blocks promotion; an absent result is not a pass.
  - Isolated tests, integration tests or health checks alone do not satisfy the gate.
  - Passing results identify the served release, the production working baseline, the check suite, runtime profile and configuration.
  - The same retained application artifacts are promoted. Stale, mismatched or wrong-release evidence is refused.
  - Evidence has a finite maximum age, defined and enforced by Platform and Builds before the gate is enabled. Rerun evidence never hides an earlier failed or incomplete attempt.
- *Release modes:*
  - Automatic promotion additionally requires G3, passing compatibility evidence against the current production working baseline (NFR-1) and a passing shared-infrastructure currency check. Modules supply change classifications; Platform supplies the combined staging rehearsal.
  - An authenticated Administrator approval permits a pre-G3 release, a release without valid compatibility evidence, or one attempt into empty or degraded production (FR-8). It never substitutes for the staging gate, artifact provenance, environment isolation or production verification.
- One controlled attempt at a time owns changes in each environment (application, configuration, routing, security, recovery). Concurrent work never replaces the release being tested or invalidates its evidence.

**FR-7: Verify production deployment and detect failure** (CAP-7). Platform verifies that the intended release becomes ready and performs required production-safe operations before declaring it a working release.
- *Smoke tests:*
  - Each enrolled module provides its required smoke tests. Read-only checks are preferred.
  - Necessary writes use the standing synthetic check identities and synthetic data, with no real-user changes or external effects. Smoke tests never create or delete tenants.
  - Staging E2E suites are not copied blindly into production.
- *Before the update:*
  - Platform validates readiness and smoke declarations for every included module. A missing or invalid declaration, or an empty required check set, blocks the update; discovering no checks is not a pass.
  - Platform identifies the working baseline, verifies it is healthy now, and checks module release qualifications and current environment and security configuration. Missing preconditions stop the attempt before mutation.
  - A failed health check also sets the promotion stop and makes production degraded. First deployment and empty or degraded production use the FR-8 approved path.
- *Failure triggers:*
  - Required services have not reached the intended release and become ready within **10 minutes of deployment start**.
  - During the **five-minute verification window** after rollout readiness, any required service is unable to serve traffic for **60 continuous seconds**.
  - A required smoke test fails **twice consecutively**, with the second attempt **30 seconds** after the first failure.
  - A required check has no completed passing result at the deadline; skipped or missing results never pass.
  - A single failed probe or container restart alone does not trigger rollback.
- *Sampling:* availability is sampled at least every **10 seconds**. Smoke checks have stable IDs and run at window start and then at a declared finite cadence. A timeout fails the check. Results bind the intended served release, and results from another release count as missing. A bounded grace may finish an in-flight retry but never turns a missing pass into success.
- *Verdict:* success only if required services are ready and the latest required smoke results pass at window end, with no trigger fired.
- *Interruptions:* they never reset deadlines or replenish recovery attempts. An observation gap invalidates verification. Uncertainty about release identity, ownership or records stops changes pending intervention.
- These thresholds are initial policy defaults to validate with staging evidence, not availability guarantees.

**FR-8: Restore and verify the previous working release** (CAP-8). For a failed compatible deployment over a working baseline, other than an approved empty or degraded attempt, Platform makes exactly one automatic recovery attempt to the recorded previous working application with compatible current environment configuration. FR-8 also defines the promotion stop and the Administrator-approved attempts.
- *Automatic recovery:*
  - It restores the application components and routing changed by the attempt, including partially updated workloads, while keeping current data and security authority.
  - If nothing changed, Platform retains the existing release and reports the failed attempt. Reversing a first module enrollment removes workloads, never durable data objects.
  - Recovery gets **10 minutes to restore readiness** plus the same five-minute verification and thresholds, using the restored release's recorded smoke suite. A rollback command alone is not success.
  - Failed or unverified recovery (for example, cluster unreachable) is reported for intervention, and Platform never cycles through older releases.
  - Every deployment failure and recovery result, including success, is reported through GitHub to Administrator and the named deputy, with release, environment, status and access-controlled diagnostics.
- *Promotion stop:*
  - A durable stop survives process, executor or cluster restart and suspends promotion. Promotion is also suspended during any recovery.
  - It is set by every non-working production outcome and recovery entry, a failed pre-update health check or over-bound probe failure, a recorded incident showing production is not working, any two-way mismatch between production admission and Administrator's records (checked on a declared cadence), or an Administrator or deputy declaration.
  - Monitoring may set the stop but never clear it, and setting it never starts a search through older releases. A continuing condition is recorded once.
  - Only an authenticated Administrator record clears the stop. The record names the reason, the observed stop state, a verified current working release and each cause as resolved or accepted, after reviewing lost-window exceptions and confirming admission matches its records. A later stop always prevails.
  - An accepted cause is not re-recorded until it resolves and recurs; any other condition persisting after the clear is recorded as a new cause.
- *Recovery deputy:* the deputy may perform documented recovery, verify restoration, reopen service (restoring only the recorded pre-incident ingress state; user ingress stays closed before G2) and declare a stop. The deputy cannot approve releases, clear the stop or administer production-user access.
- *Empty or degraded production:*
  - Empty means no working baseline. Degraded means the last outcome was non-working, or a recorded incident, over-bound probe failure or failed pre-update health check shows production is no longer working. The reduced-recovery state is neither, and no release attempt runs during any recovery.
  - Administrator alone approves one identified attempt, naming the reason and observed stop state. The approval lifts only that stop and the healthy-baseline check for that attempt.
  - When application data remain, the attempt needs valid compatibility evidence or a named data-restore recovery. A first installation without retained data uses fresh-install proof and the workload-removal failure path.
  - All FR-6 gates and FR-7 verification still apply, and staging proves the candidate works without the baseline.
  - A stop recorded after approval prevents the start. A stop recorded during the attempt lets it finish, but success then does not clear that stop.
  - Verified success becomes the working baseline and clears the observed stop. Failure removes the candidate's application, keeps data and sets a new stop.
- *First deployment:* this is an approved empty-production attempt. Failure stops with user ingress closed and is reported without claiming rollback.
- *Approved incompatible release:*
  - The approval names the recovery procedure, its acceptance checks and maximum duration. A usable recovery point is recorded after the attempt takes control, before it starts.
  - A non-working outcome leaves the stop set, user ingress closed and data kept, with the candidate removed. The approved recovery then restores data to that point; exceeding the maximum duration is reported but does not abort it.
  - The lost window is reported. FR-9's admission-revocation, Memories-erasure and reconciliation protections apply, and NFR-3 access outcomes are re-verified before ingress reopens.
- *Baseline and incidents:*
  - A manual recovery, approved attempt or disaster restore becomes the working baseline only after its verification passes.
  - Decisions, approvals, outcomes, diagnostics and the baseline remain retrievable independently of the failed environment.
  - Any other manual change to production workloads, routing or release configuration is an Administrator-approved attempt.
  - Failures found after the verification window are operational incidents.

**FR-9: Back up and restore production state** (CAP-9). Administrator or the deputy can restore production from usable recovery points outside the primary server/storage failure domain and verify the restored service against NFR-2.
- *Coverage:*
  - Coverage includes each deployed module's authoritative databases, files and configuration plus Platform's retained releases and recovery records. EventStore history alone is not assumed sufficient.
  - Modules classify state as authoritative, rebuild-only or live-authority-only, and supply restoration, reconciliation and integrity checks.
  - The recovery inventory includes identity, secret and shared dependencies (Keycloak with its event export, each environment's OpenBao, production access configuration, revocation evidence). Each has an identified recovery owner, evidence of availability or restorability, and, for surviving authorities, a fence-and-reissue owner.
- *Cadence and retention:* backup or incremental runs start every **30 minutes**. Frequent points are retained **seven days** and daily points **30 days**, with complete base and incremental chains.
- *Usable recovery point:*
  - A complete, verified cross-module set at one declared cut, with compatible release and configuration identity, required security and erasure context, and identity-provider changes exported within the declared lag bound.
  - Integrity, chains and decryption are verified when the point is recorded. Independently timed snapshots are not a usable point. RPO age is measured from the cut.
  - Tenant-key backups cover every referenced non-erased key generation. A missing generation is accepted only with a current tombstone plus known-lineage proof.
- *Storage and capacity:*
  - Points are encrypted, immutable, restricted and off-site. Access and decryption material is independently available to Administrator and the deputy, with tenant-key custody separate from data backups.
  - Prepared replacement capacity is identified and exercised; off-site backups alone do not prove whole-site recovery.
- *Monitoring:*
  - Independent monitoring checks the newest complete point at least every **15 minutes**, warns before it reaches one hour of age, and notifies Administrator and the deputy through GitHub on backup failure or age over **one hour**.
  - Availability is probed at least every **five minutes** from G1.
  - Failure reporting survives loss of the primary environment, and an independent hourly check detects monitor silence.
- *DR steps before reopening:* fence the failed environment and restore into quarantine, then:
  - rotate restored credentials;
  - reconcile production admission to Administrator's records;
  - reapply other revocations captured off-environment;
  - verify compatible versions, cross-module integrity, restored-release smokes and all NFR-3 access outcomes;
  - re-enable backups and monitoring.
- *Admission reconciliation:* it only removes access. It denies every revoked principal, including a revocation made just before failure. Recovery never grants admission, and post-cut grants are listed for Administrator to re-apply.
- *Accepted revocation losses:* two are accepted — module-owned authorization revocations acknowledged after the cut, and identity-provider access removals other than admission inside the declared, monitored capture-lag bound. A breach of that bound notifies Administrator and the deputy.
- *Memories:* recovery preserves every acknowledged erasure tombstone and never resurrects erased tenants or keys; unknown lineage fails closed, regardless of the one-hour RPO. Non-Memories deletions, erasures and legal holds inside the lost window may be lost.
- *Recovery report:* it records every lost-window exception before reopening, and Administrator's review of it gates clearing the stop, not reopening. Destructive-retention and external-effect workers stay disabled until module reconciliation completes.
- *Restore exercises:* an isolated restore exercise runs **before G2, monthly, and after material storage, backup or recovery-mechanism changes**. It assumes primary server and storage loss and proves detection, worst-case response, capacity, restore and verification against NFR-2. It records data age, elapsed time, coverage and every substituted dependency, and never mutates live production.
- *Reduced-recovery state:* after a verified disaster restore, production is in a recorded reduced-recovery state until new capacity is identified, the exercise repeats and Administrator records the return to G2 conditions. User access may continue, but the four-hour RTO is not committed for a further failure. The stop stays set until staging is re-established, and there is no emergency release path.

**FR-10: Provide isolated staging and production environments** (CAP-10). Platform hosts the MVP modules in staging under `hexalith.com` and production under `tache.ai` on the designated installation at `192.168.1.30`.
- *Interfaces:* the MVP module set is accessible in both environments through supported interfaces (production from G2). Each module declares its external surfaces, required authorization and exposure class, and a disabled or private surface is not advertised. McpCli runs on the caller's host and reaches the selected environment through its permitted gateway.
- *Separation:*
  - Staging and production use separate application data and credentials, even where a supporting service or its capacity is shared. Deploying a release never copies staging data or credentials into production.
  - Staging workload or automation authority cannot claim production hostnames or administer production state.
  - Resource limits protect production capacity from staging exhaustion; shared hardware does not provide node or site resilience.
- *Shared-infrastructure changes:*
  - They run as one controlled change covering both environments, after a complete recovery point, and re-run each environment's served-release smoke checks and the NFR-3 isolation checks.
  - From G1, each change is first rehearsed on a production-profile copy on prepared capacity. An urgent security patch may instead be applied in place with an Administrator record.
  - A failed verification is non-working: it sets the stop and degrades production, and Administrator recovers by forward revert.
- *Currency check:* a shared-infrastructure currency check compares deployed shared components with the supported, security-current profile inventory. Failure blocks automatic promotion.

**FR-11: Require explicit production-user access** (CAP-11). A staging user is denied production access unless explicitly declared a production user.
- A staging-only user cannot access production data or operations through any supported interface, including direct API, CLI and MCP.
- Production-user declaration permits only the user's assigned production permissions. A user authorized in both environments uses each with that environment's permissions.
- Staging membership and permissions never automatically create production membership or permissions, including during promotion.
- Administrator alone grants and revokes production admission, through an authenticated, auditable action. Self-registration, first login, identity mappings, staging administration and recovery authority cannot grant it.
- *Admission records:* every grant or revocation is a signed, create-only Administrator record chained to its predecessor, retained off the primary failure domain. Live membership is compared with the records in both directions on a declared cadence; a mismatch notifies and sets the stop. Reconciliation fails closed on a chain gap.
- Access integrates with the existing shared Keycloak server; authentication through it alone never grants production access.
- Synthetic actors are admitted only through the synthetic-admission group, limited to the synthetic tenant. The G1 SM-4 temporary grant is the only synthetic entry to the human admission group.

**FR-12: Access enabled-module operations in the selected environment** (CAP-12). A user can discover and invoke the commands and queries published by enabled modules through McpCli (CLI and stdio MCP), subject to their permissions in the selected environment. McpCli executes only operations their module declares agent-eligible.
- *Refusals and sources:*
  - UI-only and confirmation-required operations are refused through McpCli, even for users permitted elsewhere.
  - Available operations come from enabled modules' definitions; Platform keeps no business implementations.
- *Availability:*
  - Offline contract inspection does not establish availability. Connected discovery must match the selected environment's enabled operation definitions and contract-schema digests; unknown or mismatched contracts are non-executable.
  - CLI and MCP work against local Aspire and hosted staging and production, routed through the selected environment's EventStore gateway.
  - An invocation executes only in the selected environment, which enforces permissions for commands and queries; choosing an environment or interface grants nothing extra. A staging-only user cannot execute production operations.
  - Disabled-module operations are unavailable, and invoking one never enables the module or reroutes the call.
- *Server-side enforcement:*
  - Server-side checks enforce authenticated caller identity, calling surface and current permissions on every invocation.
  - Caller-supplied actor or surface values grant nothing. A public client's token used directly against the gateway cannot bypass UI-only or confirmation restrictions.
- *Acceptance:*
  - It names the enrolled operations actually demonstrated, proves allowed CLI and MCP invocation, and proves refusal of ineligible, disabled, mismatched-contract and unauthorized operations. An empty executable catalog cannot satisfy it.
  - Each client in the surface map has a negative test, directly and through one cross-module hop.
- *Legacy surfaces:*
  - Proprietary module and technical-module MCP servers, plug-ins and CLIs (including EventStore Admin.Cli and Admin.Mcp) are obsolete migration sources, and Platform adds or publishes no other proprietary MCP/CLI surface.
  - A legacy surface retires only after its owner-approved operation inventory, replacement or withdrawal, authorization, compatibility and acceptance evidence.

### NonFunctional Requirements

**NFR-1: Preserve data through application rollback.**
- Application rollback preserves business data, event history, credential rotations, revocations and current security authority. It restores only the previous application and compatible current environment configuration, never rewinding those resources or shared infrastructure.
- *Compatibility on the automatic path:*
  - Automatic-rollback releases keep data schemas, newly written events and required routing and security state compatible with the previous working version.
  - Module change classifications plus a staging candidate-to-baseline rehearsal prove the baseline reads candidate-written state and events, including a newly introduced idempotent command.
  - Keys, secrets and routing entries needed by the recovery combination are not retired before the candidate becomes working (AD-15 expand-only).
  - Missing or invalid compatibility evidence blocks automatic promotion. An incompatible change requires the Administrator-approved release with a separately planned recovery.
- DR data-loss targets (NFR-2) never permit application rollback to rewind data. The data restore after a failed approved incompatible release is an explicitly approved recovery, not application rollback.

**NFR-2: Bound production data loss and disaster recovery time** (major server/storage failure requiring backup restore).
- *RPO* at most **one hour**, continuously: failure time minus the declared cut of the newest complete usable recovery point, subject to FR-9 erasure and admission protections.
- *RTO* at most **four hours** for outages beginning within declared response coverage, measured from outage to verified restoration including detection, operator response, replacement capacity, restore and validation. An incident starting within coverage stays covered when coverage ends.
- *Before G2:*
  - Administrator publishes response coverage (time zone, primary and deputy responsibility, maximum acknowledgement delay) and identifies prepared replacement compute and storage. Deputy hours count only after the G2 deputy proof.
  - A restore exercise includes the declared worst-case response and proves the targets. If the evidence fails, add capability or revisit the target before opening production.
- *Outside coverage:* no four-hour commitment applies, nor during the reduced-recovery state. Every incident records its full duration and coverage status, and the clock never pauses. Backups, RPO and monitoring stay continuous.
- There is no uptime percentage, latency or throughput target. Whole-site loss is covered only with exercised capacity at an independent location.
- *Precedence:* the Platform MVP envelope takes precedence over stricter infrastructure RPO, RTO and availability clauses in Folders and Projects, without waiving module behavior, authorization, erasure protections or release gates.

**NFR-3: Enforce isolation on shared infrastructure.**
- Sharing infrastructure must not give staging users, workloads, pods or automation access to production data, credentials or administrative authority.
- Deployment, smoke-test, backup, recovery and identity-management identities hold only their declared environment and purpose permissions. CI receives no hosted-environment credentials.
- Enforcement holds at direct service, data, routing and administration boundaries. Restored production copies remain restricted production data.
- *Verification:*
  - Negative checks reject staging-only users, application credentials, automation credentials and pods attempting production operations, data or secret access, routing claims or identity administration, including direct requests and restored copies (the full matrix is AD-8 Negative tests).
  - Positive checks prove access for an explicitly authorized production user, and denial after admission revocation, including after restoration when the revocation was made just before the failure.
  - Checks repeat after access changes, shared-infrastructure changes and restoration.

**Success measures (acceptance targets, recorded with release or source revision, expected and loaded artifact identities, environment, result and diagnostics):**
- **SM-1: Complete local environment.** All seven modules ready under the effective startup deadline; CLI and MCP execute enabled modules' agent-eligible commands and queries. Validates FR-1 and FR-12; MVP acceptance.
- **SM-2: Module workspace development.** Each domain-module workspace demonstrates its minimum environment and a breakpoint or source change in the active checkout. EventStore, Memories and McpCli do the same from the Platform workspace. Only direct root-declared dependencies are initialized. Validates FR-2 and FR-3.
- **SM-3: Test execution and lifecycle.** Every MVP module demonstrates its minimum environment and local and CI integration checks.
  - Lifecycle scenarios: success, failure, startup-timeout, cancellation and collision.
  - Attachment scenarios: owner finish or cancel before attachment end, attached failure, hold expiry, and failure followed by cancellation.
  - Leftover reporting and diagnostics are shown.
  - Candidate evidence: the candidate's identity is shown; substituted and uncommitted inputs are refused, and a baseline-only run is labelled.
  - Validates FR-4 and FR-5. Only runner-owned runs count.
- **SM-4: Hosted access and isolation.** Declared interfaces and named McpCli operations are shown in both environments. Allowed operations succeed and every FR-11, FR-12 and NFR-3 refusal passes. The SM-4 admission test identity supplies positive production evidence, and its grant expires by G2 with a denial check. Runs before G2, after access changes (including the first G2 admission) and during recovery verification.
- **SM-5: Promotion and recovery rehearsals.** Validates FR-6 to FR-8, FR-10 shared-infrastructure rules and NFR-1, before G3 and after recovery-policy changes. Rehearsals demonstrate:
  - exact-release E2E evidence in both modes;
  - rejection of invalid compatibility evidence and the candidate-to-baseline rehearsal;
  - every FR-7 trigger;
  - one FR-8 recovery within budget with state preserved;
  - interruption, routing-only change, first deployment, and failed or unverified recovery;
  - every stop trigger with Administrator-only clearance, per-cause re-recording and the reviews before clearing;
  - approved degraded attempts: clearing only the observed stop, later-stop precedence, candidate removal on failure, refusal when production is neither empty nor degraded, and the deputy unable to approve;
  - a failed approved incompatible release with its recovery preserving a post-cut admission revocation and a Memories erasure;
  - shared-infrastructure forward revert and a currency-check block;
  - GitHub delivery and deputy recovery access.
- **SM-6: Recoverable production data.** An isolated drill proves:
  - complete recovery sets, RPO ≤ 1 h and RTO ≤ 4 h under coverage;
  - worst-case response, capacity, security, erasure-safe key, data, smoke and access checks;
  - denial of an admission revoked just before failure;
  - lost-window categories, grants to re-apply, and the reduced-recovery state with the stop set;
  - backup cadence and retention, independent monitoring, monitor-silence detection and GitHub delivery.
  Validates FR-9 and NFR-2 before G2, monthly, and after material changes.

**Counter-metrics:**
- **SM-C1: Bypassed validation.** Must be zero: promotions with failed, skipped, missing, incomplete, stale or mismatched E2E evidence; automatic promotions without valid compatibility evidence or G3; degraded-path approvals when production is neither empty nor degraded; stop clearances without an Administrator record or verified eligible attempt.
- **SM-C2: Unauthorized cross-environment access.** Must be zero successful production operations, data or secret access, or admin changes in the staging-only negative checks, including restored copies.
- **SM-C3: False recovery success.** Must be zero recoveries reported successful without readiness, smoke and data checks. Recorded recovery time never omits operator or provisioning time.
- **SM-C4: Destructive or incomplete cleanup.** Must be zero deletions of another environment's resources and zero cleanups of an environment still serving an attached run within its hold. A CI run is never counted as clean while its owned resources remain.
- **SM-C5: Hidden startup delay.** Record actual startup duration and timeout overrides. Raising a timeout is never reported as improvement.

### Additional Requirements

> **Starter template: none.** This is brownfield. There is no greenfield scaffold, so Epic 1 Story 1 is not a project-initialization story. The root `apphost.cs` is a file-based Aspire host on SDK 13.5.4, `global.json` pins .NET SDK 10.0.401, and the opt-in, Development-only Works preview stays the rollback composition until Works passes its parity gate. The first shared artifacts are the Platform-accepted `hexalith-module` tool version and the next-major module declaration schema; every consumer waits on them.

**Baseline and stack**
- AR-1: *Stack pins (existing, no upgrade requested):*
  - .NET SDK 10.0.401 (latestPatch).
  - Aspire AppHost SDK, Docker and Redis hosting 13.5.4; the Aspire CLI must equal the AppHost SDK, checked by the Platform tool.
  - CommunityToolkit Aspire Dapr 13.5.1-beta.757.
  - Hexalith.EventStore.Aspire 3.106.0; catalog 3.109.0 needs toolkit beta.767 or later, so align them together.
  - Aspire.Hosting.Kubernetes 13.5.4-preview.1.26464.4; Aspire.Hosting.Keycloak 13.5.3-preview (catalog 13.5.4-preview).
  - Helm 4.2.0 floor, with the exact version in the profile inventory and equal on every executor.
  - Dapr runtime 1.18, with the sidecar patch pinned per release.
  - Keycloak, OpenBao, data-service, broker, Traefik, Gateway API CRD, cert-manager, Calico, Zot and Velero pins belong to the profile inventory.
- AR-2: *Brownfield facts to resolve:*
  - Platform declares 17 `references/`, but a declaration is not a running service.
  - No root hosted Kubernetes or domain configuration and no Platform runtime workflow exist.
  - `DaprSelfHostedMtls.cs` fixes ports 50001, 51005 and 51006 and the scheduler volume `hexalith-platform-dapr-scheduler`, so concurrent runs are unsafe.
  - File-based AppHosts cannot use `DistributedApplicationTestingBuilder` through generated `Projects` types.
  - Builds: `hexalith-module` and Evidence.Cli 4.20.0–4.27.4 are on NuGet but not Platform-accepted, and the README calls them unpublished. `SupportedPlatformPins.cs` hard-codes EventStore, Dapr and FrontComposer versions. `CompositionRunState` is metadata-only, and `HXR003` remains open on the unqualified descriptor path.
  - No MVP module declares Platform as a submodule yet. McpCli declares `references/Hexalith.Platform`.
  - EventStore `RepositoryProjectPaths` probes sibling and ancestor paths, and Parties `Directory.Build.props` uses file-existence switches.
  - `Folders.EventStore` publishes an image named `eventstore` with 13 in-process adapters.

**Composition, tooling and declarations**
- AR-3 (AD-1): *One composition model:*
  - One Platform-owned Aspire application model consumes module declarations through ordinary C# helpers, and the Platform tool launches it.
  - No other AppHost composes multi-module environments, and no AppHost runs in production.
  - In staging and production the retained Platform package is the only deployment definition. Module deploy assets are declaration or conformance inputs, never a second writer.
- AR-4 (AD-1 Chart): *Chart rules:*
  - A shared Platform helper adds Dapr sidecar annotations and Pod-Security-compliant security contexts.
  - It emits HTTPRoutes whose parent reference is the environment's Gateway, namespaced. The chart contains no Gateway and no Secret object.
  - Hosted data services are non-secret endpoint parameters; credentials come through Dapr secret stores or `secretKeyRef`.
  - Render modes (normal, recovery, remove-workloads) are chart values.
  - Fall back to a small maintained Helm chart with parity checks if export needs per-module Dapr hand-modelling, a custom compiler, recurring generated-file patches or a duplicate topology.
- AR-5 (AD-4): *Active-root source identity:*
  - Resolve the active root once into one mapping consumed by build and launch.
  - Initialize only the active root's direct `references/*`, never recursively, and fail naming any initialized nested submodule. A source and a package copy of one identity fail the build.
  - Source mode builds root-declared dependencies in Debug; everything else uses NuGet packages or released images at the catalog version, never as a fallback.
  - Package mode builds the Platform composition from the Platform submodule at a release tag (HEAD in Platform), the module under test in Release against NuGet, dependency services from released images, and the composed host from packages.
  - Candidate mapping: the candidate's own Release artifacts replace its catalog artifacts in the one mapping used by the composed host, run-scoped McpCli and tests. No cache may silently supply another build.
  - Only the pinned tool sets the mode.
  - Platform identity checks: package mode refuses a composition, Builds-catalog or tool-range mismatch; source mode warns. An untagged or dirty submodule is allowed only in source mode.
- AR-6 (Local tool and readiness): the tool provides stable `run`, `down`, `test` and `debug` commands. Startup follows the FR-4 deadline and override rules, and the tool records actual startup duration, readiness and effective deadline.
- AR-7 (AD-13): *Composed EventStore host:*
  - Each environment runs exactly one `eventstore` app, which is the gateway. Platform composes it from the EventStore server plus enrolled modules' extension packages against the EventStore-owned versioned extension API. Hosted environments run the Platform-published image `platform/eventstore`.
  - Extension packages declare no Dapr roles, reference no provider SDK or Dapr client, and declare no secret, except the named AD-9 exceptions.
  - The build fails on an unsupported extension-API major (current and previous are supported) or a duplicate registration. Modules never publish images named `eventstore`.
  - The composed image's release-available and production-promoted records are Platform-issued and bind the server package and extension digests.
- AR-8 (Module declaration): *Declaration schema:*
  - Platform owns the semantics and validator; Builds implements them in the next major of `hexalith.module-manifest` (v1 is never accepted for enrollment). Platform accepts the current and previous Platform majors, and missing, duplicate or incompatible declarations fail.
  - Identity fields: stable module, app and resource identities, and the enabled server list.
  - Runtime fields: Dapr capabilities by logical role with recovery class and configuration key; extension packages and their inputs; resources, limits, replicas (default 1) and volumes.
  - Surface fields: logical interfaces and route prefixes with exposure class (public, internal-only, disabled), required surface class and authorization, inbound callers and operations.
  - Integration fields: topics with dead-letter policy, logical secrets and dynamic namespaces, identity needs, egress destinations, external providers, and external-effect and destructive-retention workers with their disable control.
  - Lifecycle fields: readiness and startup tasks with scope and authority; recovery and fence hooks with reopen-before-replay; startup override with justification; critical flows and smoke suites with their surfaces; change classification; recovery inventory.
  - Platform assigns component names, namespaces, FQDNs and scopes. Module-authoritative files such as the Parties ACL become declaration sources.
- AR-9 (Startup task lifecycle): scopes are per-start verify (idempotent), once-per-environment creation, recovery (in dependency order) and operator-only. Creation tasks never rerun on upgrade, rollback, restore or DR. Population- or authority-creating tasks are operator-only in hosted environments.
- AR-10 (AD-9): *Dapr boundary:*
  - Use Dapr through shared SDKs by default. The only exceptions are the Memories FalkorDB graph, the Memories Redis search/vector indexes and the Memories SET NX preflight-dedup row. Other Memories direct Redis coordination is transitional until G2. A new exception needs Administrator acceptance recorded in the spine.
  - No `const` or attribute literal names a component; names bind through declared configuration keys.
  - Declared topics use declarative Subscription objects rendered by Platform, with no programmatic subscriptions in compositions.
- AR-11 (Catalogs): *Routing catalog:*
  - One deterministic Platform generator builds each composition's catalog generations from declarations and Contracts, plus the committed idempotency and key entries as non-executable retention entries.
  - Each route entry carries the contract-schema digest and surface eligibility.
  - Hosted catalogs live in `deploy/dapr/eventstore-routing-catalog.json`; local and CI catalogs are run-scoped.
  - The gateway enforces eligibility, and its metadata endpoint lists only executable entries. There is no second hand-maintained catalog.
  - Candidate and rollback generations are prepared and ready-validated per the activation rules.

**Test environments and CI**
- AR-12 (AD-5): *CI integration:*
  - Reuse Builds workflows on disposable Linux GitHub-hosted runners. Isolated tests run first, then the runner's package-mode integration tier, which is blocking.
  - CI has no staging or production credentials; its only registry access is a read-only pull credential.
  - The run descriptor and results bind the candidate module and revision, the expected content identities and the actually loaded identities, including those inside the composed host and McpCli. Builds owns the encoding.
- AR-13 (AD-10): the runner alone provisions, readiness-waits, records ownership of and cleans multi-module environments. EventStore.Testing(.Integration), module and McpCli tests consume its versioned environment descriptor, and a shared AppHost path alone is not ownership. Lifecycle rules are as in FR-4.
- AR-14: *Test environment descriptor and result contract* (Platform-defined, implemented in `hexalith-module`): run and owner identity, expected and loaded artifact identities, candidate revision or baseline-only label, attachment holds and withdrawn outcomes. It precedes fixture migration and any candidate or attachment evidence.
- AR-15: *Runner qualification* (Builds with Platform):
  - Implement collision failure, deadline and override, immediate terminal outcomes, failure retention, listing, artifact-compatible attachment with finite holds and withdrawal, owner-finish refusal, same-job CI attachment, cleanup reporting leftovers, CI partial-start cleanup and the descriptor. Resolve `HXR003`.
  - Reconcile fixed Dapr ports and volumes, dapr-init Redis, the fixture lock, fixed-port Keycloak and the shared certificate directory.
  - There is no custom DSL or environment service.
- AR-16: *Candidate and attachment policy* (Platform with Builds, before any CI evidence is accepted): one shared candidate-revision rule (PR head or merge result), the merge or release decision the evidence gates, unchanged-module evidence reuse and a finite attachment hold limit, all published in the runner contract before consumers implement them.

**Release artifacts, provenance and records**
- AR-17 (AD-2): *Retained release artifacts:*
  - The publication workflow publishes one immutable application Helm package identified by OCI manifest digest, plus immutable image digests including `platform/eventstore`.
  - The release record binds every release-invariant value: module artifacts and digests, composed-host identity, catalog route digest, McpCli package hash, profile template digest, qualified environment-layer and shared sets, Dapr sidecar patch, realm-contract version, recovery-hook contract version, change classifications and check-suite digests.
  - Executors `helm upgrade` by digest and never regenerate with `aspire publish` or `aspire deploy`.
  - Artifacts are retained off the primary failure domain for the backup retention period or their life as a rollback target, whichever is longer. Helm history is never the record.
- AR-18 (Workflows and provenance): *Publication and repository controls:*
  - The public Platform publication workflow runs on hosted runners through SHA-pinned Builds reusable workflows, attests the package, composed image and McpCli, verifies intake-pinned module image attestations and writes the release record. The governed closure of `uses:` is SHA- or digest-pinned.
  - `main` has a ruleset: pull request with code-owner review, no force-push or deletion, Administrator-only bypass. CODEOWNERS assigns `.github/workflows/**` and CODEOWNERS itself to Administrator. A tag ruleset forbids update and delete, and Builds bypass is Administrator-only.
  - `registry.hexalith.com` has anonymous read disabled, per-environment pull credentials, create-only writers, and garbage collection that never removes a retained digest, plus an off-site replica with a separate writer. `registry.tache.ai` is not a retained store.
- AR-19 (Module intake): a versioned intake manifest in the Platform repository pins package and image digests, image-attestation identities and evidence references. It uses the single definition of *breaking*, and the effective classification is the maximum over the chain from the working baseline.
- AR-20 (Binding classes and records):
  - Values are release-invariant, environment-current or attempt-bound.
  - Records are accepted only when signed by their writer: release, attempt, qualification, production-promoted, release-available, latest-working pointer, promotion-stop and Administrator records.
  - Records live in one CAS-capable store outside the target cluster and executor hosts. The working baseline is the release record plus the attempt record the latest-working pointer names.
- AR-21: *Shared contracts:*
  - Record encoding (Builds): release, attempt, qualification and Administrator records.
  - Attempt lock, record and promotion-stop store (Platform with Builds).
  - Check-suite invocation and result contract (Builds).
  All three precede the first staging deployment.
- AR-22: *Image supply chain:* module image attestation (Builds `domain-release`, adopted by EventStore and Memories) precedes the first publication because the publication workflow verifies the intake-pinned attestations. The image and dependency vulnerability policy (scan at intake, a blocking severity, recorded exceptions) precedes the first staging promotion.

**Hosted environments, identity and isolation**
- AR-23 (AD-8): *Environment separation:*
  - Each environment has an application namespace and a separate data namespace holding its data services, OpenBao, PVs, Gateway and listener Certificates.
  - A named shared-infrastructure/bootstrap workflow and shared-infrastructure identity create the namespaces, environment-identity RBAC, StorageClasses and PriorityClasses.
  - The environment-layer identity writes inside the already-created data namespace plus the allowed application-namespace Components, HTTPEndpoints, MCPServers, NetworkPolicies, quotas and bootstrap Secrets. It does not create or mutate shared-infrastructure objects. The per-job application deploy identity holds none of these permissions.
  - Each environment uses its own StorageClass, with quotas denying the other's, and least-privilege per-module principals.
  - Pod Security is `restricted`, with default-deny ingress and egress on an enforcing CNI. Production gets a higher PriorityClass; staging runs under quotas.
  - Dapr has one trust domain per environment, deny-by-default Configurations and HotReload off, with WorkflowAccessPolicies scoped by app ID.
  - The full AD-8 negative-test matrix applies.
- AR-24 (Hosted interfaces): *Gateway and naming:*
  - One FQDN pattern applies; internal and disabled interfaces are neither routed nor advertised, and module artifacts contain no environment FQDN.
  - Each environment has a Gateway API Gateway on Traefik in its data namespace. Listeners carry only exact FQDNs, the HTTPS listener admits routes only from the environment's app namespace, and the port-80 listener also admits ACME solver routes.
  - User-ingress admission (open, or executor and probe only) is environment-layer state changed only within an attempt.
  - HTTPRoutes are restricted to hostnames, parent reference, path matches and same-namespace backends. The application deploy identity cannot create Ingress, `traefik.io` or `hub.traefik.io` objects.
  - Nothing is rendered on `nginx-public`.
  - Reserved names are rejected in staging, staging Certificates use only the staging HTTP-01 Issuer, and staging holds no DNS-01 credential.
  - Each DNS zone has one named owner. Executors verify through a declared internal endpoint.
- AR-25 (AD-6): *Identity realms and admission:*
  - Separate staging and production realms on the shared Keycloak, all generated from one versioned, value-free realm contract. EventStore owns claims; Platform owns the instance (surface classes, client-to-surface map, token exchange, admission groups, event settings, refresh lifetimes, synthetic tenant ID).
  - Admission records are signed, create-only and chained, and the recovery authority for admission. EventStore's admission projection is live-authority-only and fails closed after restore until rebuilt.
  - No automation holds master or cross-realm admin. Admin API, console and master realm are reachable only from a declared Administrator path. Admin and user events are exported off-cluster within a declared bound.
  - Administrator and deputy authorities use phishing-resistant MFA, and break-glass use alerts.
  - Public clients never get `offline_access`.
- AR-26 (AD-14): *Calling surfaces:*
  - Surface classes are `ui` (confidential), `agent` (public) and `service` (confidential). Each surface is a distinct client per environment, and the surface is derived from `azp` plus attested chain context, never from headers or audience.
  - Synchronous cross-module chains use Keycloak token exchange, downscoped. Asynchronous steps use the EventStore-attested original actor and re-check admission.
  - Raw user-token forwarding is forbidden, and there are per-client negative tests.
- AR-27 (Synthetic identities):
  - Each environment has one synthetic tenant identifier, with the Tenants-owned idempotent creation task (operator-only in hosted environments).
  - Synthetic clients are flagged and hold no admin or cross-tenant grant. There is a standing synthetic-admission group.
  - The staging-only tenant-lifecycle admission clause is rejected by the validator in production. The immutable synthetic marker is accepted only on staging or run-owned creation by a synthetic client.
  - Administrator provisions production synthetic credentials, held only by the production executor (and by the recovery executor for one DR job).
- AR-28 (AD-7): *Executors and triggers:*
  - Staging and production each have a dedicated self-hosted executor on a separate host; the production executor sits outside the application cluster. An off-site recovery executor handles replacement-capacity recovery. No executor shares a host with a CI runner or another executor.
  - Credentials are per-job, revocable and epoch-bound, issued through OIDC claim checks or held by the executor, never as repository secrets. A recovery-kind job additionally receives a recovery-hook credential bound to its attempt and epoch, read access to that environment instance's recovery-point prefix, and only the custodian-released decryption material needed for that job; all three are destroyed or revoked at job end.
  - Module-supplied code runs in a sandbox without job credentials.
  - Jobs come from a private operations repository whose only writers are the named writers. Executors enforce allowlists and verify signed Administrator records.
  - Administrator or the deputy can start in-place recovery on the production executor and replacement-capacity recovery on the recovery executor.
- AR-29 (Secrets): follows EventStore AD-24 — Dapr `secretstores.hashicorp.vault` against the environment's OpenBao, with per-app tokens mounted per pod and a pod restart on rotation. The value-free `deploy/dapr/openbao-secret-contract.yaml` has an environment dimension and module-owned dynamic namespaces. Required generations gate readiness.
- AR-30 (Data protection): node-level volume encryption, with keys under Administrator and deputy custody, and native TLS on data services where supported (CloudNativePG included).
- AR-31 (Production profile): *Profile and qualification:*
  - The EventStore-ratified template is paired with a profile inventory (environment and shared facets plus the deploy toolchain) under a profile digest.
  - Qualification records are issued per attempt, and the production-promoted record is renewed on an environment-layer or shared change.
  - The Dapr skew rule applies.
  - Security currency is checked at each production attempt and monthly drill, and every expiring credential has a named renewal owner.
- AR-32 (Release tiers):
  - Tiers are the application package, the environment layer (per environment, forward-only) and shared infrastructure (both locks, rehearsed from G1), plus what sits outside every release. Every object belongs to exactly one tier.
  - Namespaces, environment-identity RBAC, StorageClasses and PriorityClasses are shared infrastructure. Data services, broker, OpenBao, tenant-key store, volumes, Gateway and Certificates, Dapr Components/HTTPEndpoints/MCPServers, NetworkPolicies, quotas, limits and bootstrap Secrets are environment-layer objects. Workloads, services, HTTPRoutes, Dapr Configurations, WorkflowAccessPolicies, Subscriptions and Resiliency belong to the application package.
  - Dapr activation restarts consuming sidecars on a resource change.
  - Environment-layer objects are rendered from the union of baseline and candidate and applied by their own attempt before the release attempt.
  - Definitions live in the operations repository, with a pinned off-site copy.
  - Keycloak is production-critical: staging has no backup or restore access to it.

**Release and recovery mechanics**
- AR-33 (AD-3/AD-15): *Rollback sets:*
  - Application rollback touches only the application-package tier. Automatic recovery renders the prepared rollback set through a Helm upgrade. Helm `--rollback-on-failure`, `helm rollback` and controller-driven rollback are forbidden.
  - Before rollout of an attempt eligible for automatic recovery, the rollback set (baseline package with environment-current values plus the rollback generation) is recorded, prepared and ready-validated. Approved empty/degraded and named-recovery attempts do not require production baseline-host validation or an automatic rollback generation.
  - Every forward-only input stays expand-only until the candidate is working, and contraction happens in a later attempt.
- AR-34 (RRA Staging gate / Staging reset):
  - Each staging attempt cuts a staging recovery point, deploys the production baseline, upgrades to the candidate, runs E2E and rehearses the rollback set with HotReload off. When production has no baseline, it rehearses a fresh install instead.
  - A failed rehearsal is breaking evidence, not a staging failure.
  - Evidence has a maximum age, and reruns never hide failures.
  - Staging reset: an unadopted candidate whose writes the baseline cannot read is reset by in-place data restore before a later candidate's attempt, and the staging realm regenerates.
- AR-35 (Release modes, Empty or degraded production, In-place recovery):
  - These follow the FR-8 rules. An approved retained-data attempt starts only after a complete recovery point cut after the lock.
  - In-place recovery is either a baseline re-deploy or a data restore, runs on the production executor under a new epoch, and is started by Administrator or the deputy with the stop kept set.
- AR-36 (Attempt ownership / Timing and interruption):
  - One per-environment lock with a monotonic epoch covers every workload-affecting change; shared-infrastructure changes hold both locks.
  - Takeover is accepted only after every affected authority is fenced or drained.
  - Attempt kinds (release, infrastructure, recovery) record their grace, deadlines and maximum lifetimes. Resumption never restarts timers, and interruption outcomes follow the kind.
  - The off-site monitor notifies on stale non-terminal records.
- AR-37 (Rollout / Verification / Automatic recovery):
  - The production-promoted record is written before the Helm upgrade and invalidated on a non-working outcome.
  - Production runs only the immutable check-suite digests from the release record, in the sandbox.
  - Thresholds follow FR-7 and FR-8.
- AR-38 (Production preconditions 1–9) — no mutation until all hold:
  1. Lock, epoch and a clear stop.
  2. The release-mode trigger.
  3. Provenance.
  4. Exact-release staging evidence within its maximum age.
  5. EventStore server package and composed image release-available.
  6. Equal profile digests, versions within the qualified sets, the required realm-contract version, and the environment layer applied.
  7. Every included module enrolled for production with valid, non-empty readiness and smoke declarations.
  8. Production healthy now.
  9. Retained-data compatibility evidence, or approved mode with a named data restore.
- AR-39 (Production entry gates): *G1, G2 and G3:*
  - G1: supported Kubernetes minor; production deployed with ingress closed (executor and probe only) and an empty human admission group; GitHub delivery working; probe running.
  - Between G1 and G2: the SM-4 temporary synthetic grant, which expires by G2, is revoked and is followed by a denial check.
  - G2: the AD-12 drill passes, including Memories continuity; SM-4 passes; the G1 grant's revocation and denial check exist; recovery access, capacity, deputy alerts and coverage are verified.
  - G2 opening order: admit the first user, re-verify SM-4 on the live realm, then admit others. A failure aborts and removes the user.
  - G3: after SM-5 and EventStore confirmations. Policy changes suspend it until the affected rehearsals repeat. Before G3, every release is Administrator-approved.
- AR-40 (Backup coverage / Recovery point / Detection / DR evidence):
  - The inventory covers module state, the Keycloak DB with its event export, and each OpenBao, each with a recovery owner explicitly confirmed by Administrator and recorded with the entry, a recovery class and a fence-and-reissue owner.
  - A backup unit holds one recovery class, and prefixes are per environment instance.
  - Rotated key generations are retained as long as data backups.
  - Recovery-point integrity metadata is kept off-site, and pruning skips referenced points.
  - The DR drill applies worst-case response, times every substitution (an unbounded substitution fails), times rebuild-only replay at representative size, rehearses real fence steps on staging before G2, and uses ephemeral, egress-denied restores. A real DR aborts any drill.
- AR-41 (AD-12): *Recovery model and custody:*
  - One active production environment, restored onto prepared capacity. Recovery units use native database backup or module-allowlisted file backup; raw volume or snapshot copies never count.
  - Tenant keys are held in a separate custody class, and erasure makes earlier key backups unusable for the erased tenant.
  - The recovery tooling supports every recovery-hook contract version still referenced.
  - Administrator and the deputy unseal OpenBao manually within coverage, custodian use alerts, and root tokens are revoked at recovery end.
- AR-42 (Recovery sequence): *Seven steps, each with an in-place form:*
  1. Fence surviving authorities, or quiesce in place.
  2. Rebuild the environment on prepared capacity, with Keycloak and OpenBao restored into quarantine and release provenance verified.
  3. Restore keys and re-apply tombstone destruction.
  4. Restore data into quarantine, start the release in recovery mode on a forward catalog generation, run epoch-bound module recovery hooks in dependency order, re-provision the broker and catch up subscribers.
  5. Rotate credentials, reconcile admission, re-apply post-cut revocations and rebuild the admission projection.
  6. Verify, re-enable workers and record the lost window.
  7. Reopen: resume monitoring, cut over DNS and certificates, hand credentials to the production executor and restore the pre-incident ingress state, with the stop kept set.
  Recovery mode keeps user ingress closed and external-effect and destructive-retention workers disabled, with quarantine admitting only recovery actors.
- AR-43: *Recovery hook contract* (Platform-owned, before the first staging deployment; the first drill proves it): cut identity and per-class positions, quarantine admission, purge, re-provisioning and rotation, fence hooks, rebuild, integrity, external-effect reconciliation, the reopen-before-replay declaration, recovery mode, an internal endpoint with attempt- and epoch-bound credentials, epoch-bound task commits and the result shape.
- AR-44 (Lost window / After DR): these follow FR-9, NFR-2 and the reduced-recovery posture. Administrator reviews every recorded lost-window report applicable to the observed stop; absence is valid only when no recovery capable of producing a lost window has occurred. Once a named-recovery or DR report exists, it is a mandatory input to clearance or approval. The reduced-recovery operating policy (automatic-promotion resumption, drill cadence while capacity is consumed, staging re-establishment) is fixed before G2.

**Diagnostics and notification**
- AR-45 (Diagnostics and notification):
  - Telemetry sinks are per environment, rendered by Platform, and modules never declare a sink.
  - Attempt diagnostics and incident telemetry ship off-site with declared retention. Evidence is bound to run or release, environment, artifacts and configuration, and redacted diagnostics survive cleanup.
  - The single accepted notification path is GitHub issues in a private notification repository, raised through an issues-only credential, assigned to Administrator and mentioning the deputy.
  - The off-site monitor runs the probe and the recovery-point, event-export, stale-attempt, seal-state, volume-headroom and expiry checks, plus the two-way admission comparison against signed records.
  - An hourly dead-man workflow runs on an off-hour minute, and the monitor and dead-man workflow watch each other.

**McpCli and legacy surfaces**
- AR-46 (AD-11 Tool/Availability/Profiles):
  - The publication workflow alone publishes stable `Hexalith.McpCli` versions, after staging validation, versioned by base and Platform release. McpCli's own pipeline publishes Abstractions and prerelease tools only. In source and package modes the tool builds a run-scoped, never-published McpCli.
  - In connected mode an operation is executable only when the gateway metadata serves it with a matching digest and McpCli-admitting eligibility; McpCli uses the served eligibility.
  - A profile binds one environment's gateway, issuer, audience and token, with no mixing or fallback. Hosted tokens are short-lived OIDC tokens.
  - The actor is derived from the token; actor flags are refused, and the gateway rejects mismatched actor properties.
  - Refresh material lives only in the OS credential store.
- AR-47 (AD-11 Legacy/New surfaces; sprint change proposal):
  - Legacy module and technical-module MCP hosts, plug-ins and CLIs, including EventStore Admin.Cli and Admin.Mcp, are frozen. They are never enrolled, deployed, routed, mapped or issued a realm client in any Platform composition, and declaration validation rejects an enrolled host that maps an MCP endpoint.
  - Temporary compatibility use happens only outside compositions, under a named migration record.
  - No new proprietary surface is admitted. McpCli HTTP or other transports need a Platform AD under AD-14.
  - EventStore Admin.Server and Admin.UI remain a confidential UI surface.
  - Platform's share of the migration: the canonical `references/Hexalith.McpCli` link, the no-new-surface policy, composition and route removal at retirement, and release composition and candidate evidence.
  - The McpCli migration inventory schema (McpCli with EventStore) precedes any retirement; inventories and cutover run under McpCli Epic 5.
- AR-48 (Connected McpCli owned work): amend McpCli's availability, token, actor, profile-source, source-mapped Contracts manifest and publication contracts. Migrate its test AppHost to the AD-10 descriptor, with no new `Parties.Aspire`. Prove digest compatibility, served eligibility, freshness and token renewal before FR-12 acceptance.

**Migration, coexistence and extensions**
- AR-49 (Migration and coexistence):
  - Builds.Module.AppHost launches the AD-1 model, and Builds.Module.UiHost is only a run-scoped host. Builds.Module.EventStoreHost becomes the AD-13 seed or is removed.
  - Domain-module AppHosts are frozen with no new cross-module wiring. New hosting helpers are refused until the canonical ServiceDefaults and Aspire Dapr package set is recorded. FrontComposer.AppHost is sample-only.
  - Producers are adopted before consumers are retired. Legacy hosting retires only after source, package and deployed parity plus EventStore AD-22 authority.
- AR-50 (Module adoption):
  - Add direct declarations to every workspace, including `references/Hexalith.Platform`, and canonical McpCli enrollment.
  - Remove sibling, nested and package-fallback helpers and file-existence selectors.
  - Move the Works lane off hard-coded sibling paths.
  - Align EventStore.Aspire and the Dapr toolkit to the catalog together.
  - Remove the Works-preview HS256 key when the local realm lands, and update the README's Aspire CLI floor.
  - Apply AD-9 naming and subscriptions and reconcile EventStore AD-24's `openbao`.
  - Inventory each module's hosting projects.
- AR-51 (EXT-HOST-1 Agents host composition): *outside MVP acceptance, enrolls through the same declaration schema.*
  - Platform owns the host composition for the Agents DomainService and UI with EventStore, Conversations, Parties, Tenants, provider and safety adapters, Dapr Workflow, secrets, health, identity and telemetry. This replaces Agents-owned AppHost, Aspire and ServiceDefaults.
  - Contract: clean-checkout composition through Platform's Aspire AppHost without Agents-owned hosting.
  - Current verification command: `./eng/verify-agents-host.sh` (the script exists).
  - Full wiring and Level 4/5 evidence belong to Agents Story 5.6. The Works lane also enrolls through the same schema, and this spine supersedes Works AD-20.
- AR-52 (Source precedence):
  - The Platform MVP recovery envelope governs the stricter Folders (I-10, I-11, EXT-ES-RECOVERY) and Projects (AD-28, G-1) infrastructure clauses without blocking their enrollment. Projects AD-30 may not demand HA or RPO-0 evidence.
  - Platform overrides McpCli AD-4, AD-10, AD-13, AD-14, AD-16 and AD-17, and AD-14 overrides Projects AD-30's MCP confirmation.
  - Recording these upstream is alignment work, not an enrollment block.

**Owned work by gate (cross-repository; each lands before the gate or consumer it controls)**
- AR-53: *First Platform-accepted tool version* (Builds with Platform):
  - Remove the hard-coded version constants so the catalog is the one version authority, and fix the README.
  - Builds.Module.AppHost launches the Platform model.
  - Implement AD-4 package mode, the identity checks, direct-only initialization and the debug entry point.
  - Confirm the AppHost form and testing-builder compatibility.
  - Make the runner CI tier blocking and retire the advisory module-AppHost tier.
- AR-54: *First local enrollment* (Platform with EventStore and Commons): ratify the local mTLS, per-receiver ACL and scoped-component convention, and record the canonical ServiceDefaults and Aspire Dapr package set.
- AR-55: *First publication* (Administrator): apply the `main` ruleset, CODEOWNERS, tag ruleset and Builds bypass; set the organization base permission to read or none (today it is write); create the operations and notification repositories with only the named writers.
- AR-56: *First staging deployment* rows:
  - Composed host: Folders adapters are packaged before Folders joins.
  - Aspire-to-Helm qualification with the representative Parties+EventStore+Tenants+Memories composition. Aspire 13.5.4 `AddGateway` materializes its own Gateway, so work around it. Prove deterministic rendering, chart contents, server-side dry-run against `restricted`, no Secret in the chart, digest-pinned images via values, Helm SSA ownership and the fallback trigger. Publication separately proves OCI attestation, and retained-artifact qualification separately proves restore/rollback from the off-site replica.
  - Shared runtime and profile: ratify the profile; create the catalog and secret-contract instances; select and qualify the durable production broker (Redis pub/sub excluded) with retention, dead-letter and backlog recovery; qualify Dapr activation with one policy-only and one HTTPEndpoint-only update.
  - Secrets, identity, network and transport: Keycloak inventory, realm contract and event export, token-exchange clients, per-environment OpenBao and tenant-key store, per-app token mounts, splitting the shared `openbao-runtime-bootstrap` token (expires 2027-07-19), the executor sandbox, CNI enforcement, drop-all-capabilities, volume encryption and TLS, Gateway API CRDs with the Traefik provider, cert-manager per-environment `gatewayHTTPRoute` issuers, staging HTTP-01, trust domains and every negative isolation case.
  - Staging data restore and takeover fencing.
  - Telemetry sink.
- AR-57: *First staging evidence* policy: triggers, rerun acceptance, evidence maximum age, the per-attempt staging recovery point, the staging-reset maximum duration and critical-flow remap review. Tenants reviews tenant-lifecycle flows, leftover synthetic-tenant cleanup or reporting, and production synthetic-tenant provisioning.
- AR-58: *First production attempt* (Platform, Builds, Administrator): the production store instance reachable from the production executor, recovery executor and monitor; provenance; interruption, epoch and timing; takeover fencing with revocable per-job credentials and a Kubernetes drain; revision-conditioned stop clears; one recovery; the in-place recovery entry point; signed records; executor runner updates and version monitoring; stale-attempt detection. *First degraded non-empty attempt:* the compatibility or named-recovery qualification.
- AR-59: *G1 rows:*
  - Monitor and admission bounds (Platform with Administrator): probe-stop bound including behavior during a locked attempt, identity-event capture lag, two-way admission cadence, mismatch notification, and refusal to clear or approve while admission differs from its records.
  - Exposure, DNS and certificates (Administrator): exposure paths, the ingress-closure mechanism, zone owners, ACME credentials, the internal verification endpoint, registry controls and replica, monitor host, notification repository, dead-man workflow, actual issue delivery, and removal of the public Keycloak admin, master-realm and KubeSphere console routes, verified by an external negative probe.
  - Infrastructure currency: move Kubernetes off 1.34 (end of life 2026-10-27); patch OpenBao; move Redis Stack 7.4 to Redis 8; bring Keycloak, CNPG, PostgreSQL, FalkorDB, Dapr, Traefik, Calico, cert-manager, Gateway API, storage provisioner, Zot and Velero current; update or remove KubeSphere; name renewal owners.
  - Forgejo runner relocation: move the privileged runner off the node and off any executor host.
- AR-60: *G2 rows:*
  - Recovery capacity and coverage: capacity and location, published coverage, the recovery executor with custody, a pinned off-site workflow copy, fence-and-reissue owners, the deputy's identity, permissions, keys, alerts and rehearsal, Keycloak DB backup with revocation-before-export proof, budget and data sizes.
  - Reduced-recovery operating policy.
  - Memories continuity: tombstone and key continuity, tenant-key store, per-tenant principals and operator artifact, tombstone-mirror fence hook and recovery evidence.
  - Memories adapter conformance: qualify the named AD-9 adapter boundary and migrate remaining direct Redis coordination to Dapr.
  - Telemetry retention and off-site durability.
- AR-61: *G3 row:* shared-infrastructure currency policy (inventory, cadence, allowed lag, effect on approved attempts). *Triggers, outside the MVP:* GitHub Team controls once anyone beyond the named writers gains write; additional McpCli transports, step-up, mocks and traffic-metric rollback; broker change, standby, regions, scale and universal in-transit TLS.
- AR-62: *First shared versions:* dependencies owned outside Platform that module adoption waits for.
  - EventStore extension API with supported majors.
  - AD-13 ratification and composed-subject issuer registration.
  - Routing catalog schema with digests, eligibility and retention entries (EventStore.Contracts).
  - Gateway metadata endpoint.
  - Original-actor and originating-surface attestation (EventStore AD-29 extension).
  - Admission predicate and projection with the staging lifecycle clause.
  - Realm contract (EventStore claims, Platform instance).
  - Profile template and binding split (EventStore with Platform).
- AR-63: *Observed infrastructure to classify or remediate before reuse:*
  - A single Ready node on v1.34.9 with OpenEBS local hostpath storage.
  - An existing `hexalith-memories` namespace, and no staging/production namespace pair.
  - One shared OpenBao (daily raft snapshot, coverage unverified).
  - The Keycloak 26.7.4 PostgreSQL cluster, with no CNPG backups.
  - Traefik v3.7.13 serving `nginx-public`, no Gateway API CRDs, and cert-manager v1.21.2 solving through `nginx-public`.
  - Zot 2.1.20 with anonymous read and GC deleting untagged manifests.
  - A Dapr injector that does not drop capabilities.
  - The `forgejo-runner` namespace at Pod Security `privileged`.
  - The only Velero schedule is `forgejo-hourly`.
- AR-64 (Accepted risks — constraints, not work): a single node and shared kernel with no HA; in-place Kubernetes-minor upgrades taking both environments down; staging on 1.34 after 2026-10-27 until G1; GitHub Free with two named writers; GitHub as the single notification path; prerelease Aspire, Dapr toolkit and Keycloak pins; plaintext Redis and FalkorDB inside the data namespace; manual unseal outside coverage; whole-site loss without independent capacity; the accepted lost-window revocations and erasures.

### UX Design Requirements

No UX design contract exists. None was found under `ux-designs/`, and there is no legacy `*ux*` document. Platform exposes no product UI of its own: module UIs, including confidential `ui`-class surfaces for UI-only and confirmation-required operations, remain module-owned. Operator-facing surfaces are the `hexalith-module` run, down, test and debug commands and their diagnostics and listing (AR-6, FR-4), McpCli CLI and stdio MCP (FR-12, AR-46), and GitHub-issue notifications (AR-45). Those are covered by their FR and AR entries, so there are no UX-DRs.

### FR Coverage Map

- FR-1: Epic 3 - Complete seven-module local environment, ready within the effective deadline
- FR-2: Epic 1 - Debug the active checkout; direct-only references; source/package mapping and Platform identity (proven on the reference composition); Epic 3 - SM-2 evidence across all four domain workspaces
- FR-3: Epic 1 - Module-declared minimum environment composed as real services
- FR-4: Epic 2 - Run-owned environment lifecycle: readiness, isolation, attachment, retention and cleanup, local and CI; Epic 3 - candidate artifact-identity evidence, including identities loaded inside the composed host and run-scoped McpCli
- FR-5: Epic 2 - Isolated tests first, without Platform
- FR-6: Epic 5 - Exact-release critical-flow staging gate in both modes; Epic 9 - automatic-mode compatibility and currency conditions; Epic 10 - critical flows of the full seven-module release
- FR-7: Epic 6 - Production readiness, verification window and failure triggers; Epic 10 - Folders and Projects smoke suites in production verification
- FR-8: Epic 6 - One automatic compatible recovery, promotion stop, approved first-install path and baseline re-deploy; Epic 8 - in-place data restore, named retained-data recovery, DR and reduced-recovery state; Epic 9 - SM-5 rehearsal of every rule
- FR-9: Epic 8 - Backups, recovery points, monitoring, DR sequence and drills; Epic 10 - drill repeated with Folders and Projects state
- FR-10: Epic 4 - Isolated staging on `hexalith.com`; Epic 6 - production on `tache.ai` and shared-infrastructure change procedure; Epic 9 - currency check blocking automatic promotion; Epic 10 - Folders and Projects hosted in both environments
- FR-11: Epic 7 - Explicit Administrator-granted production admission with records and reconciliation
- FR-12: Epic 3 - Local McpCli CLI/MCP operations; Epic 4 - staging operations; Epic 7 - production operations and refusals; Epic 10 - Folders and Projects operations in hosted environments; Epic 11 - legacy MCP/CLI retirement
- NFR-1: Epic 5 - baseline-aware compatibility evidence and automatic-eligible rollback-set rehearsal; Epic 6 - data-preserving automatic recovery render; Epic 8 - named retained-data recovery qualification; Epic 9 - automatic-path enforcement
- NFR-2: Epic 8 - RPO/RTO targets, coverage and drills
- NFR-3: Epic 4 - staging-side controls and early closure of public admin exposure; Epic 7 - production negative and positive matrix; Epic 8 - re-verification after restore

**Success measures:**
- SM-1: Epic 3.
- SM-2: Epic 1 (mechanism), Epic 3 (evidence across the set).
- SM-3: Epic 2 (mechanism), Epic 3 (evidence across the set).
- SM-4: Epic 7 (reference composition), re-verified in Epic 8 at G2 opening; Epic 10 (all seven modules).
- SM-5: Epic 9 (reference composition), with the shared rehearsal harness and early approved-mode fault rehearsals in Epic 6; Epic 10 (all seven modules).
- SM-6: Epic 8; Epic 10 (repeat drill with all seven modules).
- Counter-metrics: SM-C1 Epics 5, 9, 10 · SM-C2 Epic 7 · SM-C3 Epics 6, 8 · SM-C4 Epic 2 · SM-C5 Epics 2, 3.

**Additional requirements:**
- Epic 1: AR-1–AR-6, AR-8–AR-10, AR-49, AR-50 (reference-composition adoption), AR-53, AR-54, plus the zero-extension baseline of AR-7.
- Epic 2: AR-2 (fixed ports and volumes), AR-12 (CI tiers), AR-13, AR-14, AR-15, AR-16 (the complete candidate and attachment policy).
- Epic 3: AR-7, AR-11, AR-12 (evidence binding), AR-25–AR-27 (local instances), AR-46–AR-48, AR-62; it consumes the AR-16 policy published by Epic 2.
- Epic 4: AR-17–AR-32, AR-43, AR-45, AR-55, AR-56, AR-59 (Kubernetes currency, Forgejo relocation, public-exposure removal and the capture-lag decision, pulled forward), AR-63; AR-22 module attestation before publication.
- Epic 5: AR-19, AR-22 (vulnerability policy), AR-33, AR-34, AR-46 (stable publication after staging validation), AR-57.
- Epic 6: AR-33, AR-35 (release modes and baseline re-deploy, not data restore), AR-36–AR-39, AR-45, AR-58, AR-59 (remaining G1 rows).
- Epic 7: AR-25–AR-27, AR-39 (SM-4 grant).
- Epic 8: AR-28 (commissioned recovery executor), AR-35 (in-place data restore and named recovery), AR-40–AR-44, AR-60.
- Epic 9: AR-61, AR-58 (G3 confirmations).
- Epic 10: AR-7 (hosted Folders extension packages), AR-52, AR-56 (Folders adapters before Folders joins).
- Epic 11: AR-47, AR-49, AR-50 (retirement).
- Epic 12: AR-51.
- AR-62 (other teams' first shared versions) is consumed by Stories 3.2, 3.3, 3.7, 4.9 and 9.3, and tracked in the External Prerequisites Register.
- AR-64 (accepted risks) is a constraint on all epics, not a unit of work, so no story covers it.

### External Prerequisites Register

Deliverables owned outside the Platform-accountable scope, listed so their owners can plan in parallel. A consuming story names its prerequisite and cannot close until the prerequisite is delivered; deferral never turns absent evidence into a pass.

| Prerequisite | Owner | Consumed by | Must precede |
| --- | --- | --- | --- |
| Extension API with supported majors | EventStore | Epic 3 (composed host with extensions); Epic 10 (Folders hosted) | AD-13 composed host |
| AD-13 ratification and composed-subject issuer registration | EventStore | Epic 4 (composed image release-available); Epic 6 (precondition 5) | Composed host release-available |
| Routing catalog schema with per-operation digests, surface eligibility and retention entries | EventStore.Contracts | Epic 3 (catalog generator, McpCli discovery); Epic 6 (rollback generation) | Catalog generator |
| Gateway metadata endpoint | EventStore | Epic 3 (McpCli connected mode) | FR-12 acceptance |
| Original-actor and originating-surface attestation (EventStore AD-29 extension) | EventStore | Epic 3 (cross-module steps); Epic 7 | First asynchronous or confirmed cross-module step; FR-12 acceptance |
| Admission predicate and projection, including the staging lifecycle clause | EventStore | Epic 4 (staging predicate); Epic 7 (projection); Epic 8 (rebuild after restore) | First hosted enrollment |
| Realm-contract claim names and semantics | EventStore (Platform owns the instance) | Epic 3 (local realm); Epic 4 (staging realm) | Local realm |
| Profile template and per-release binding split (EventStore AD-26) | EventStore with Platform | Epic 4 | First staging deployment |
| G3 confirmations: AD-15 retention entries, catalog activation order, expected-generation commit, recovery-point forward generation, production-promoted invalidation and renewal | EventStore | Epic 9 | G3 |
| Contract amendments (availability, token, actor, profile source, source-mapped Contracts manifest, publication) and test-AppHost migration to the AD-10 descriptor | McpCli | Epic 3 | FR-12 acceptance |
| Migration inventory schema, generic administration and resource contract; McpCli Epic 5 inventories and cutover | McpCli with EventStore | Epic 11 | Any legacy MCP/CLI retirement |
| Module adoption: direct `references/Hexalith.Platform`, declaration, fallback removal (Tenants, Folders, Projects) | Module owners | Epic 3 (evidence stories); Epic 10 | SM-2 and SM-3 evidence across the set |
| Readiness, surface and agent-eligibility declarations; critical-flow declarations and E2E suites; production-safe smoke suites (modules outside the reference composition) | Module owners | Epics 3, 10 | Enrollment in each environment |
| Recovery-hook and fence-hook implementations; authoritative, rebuild-only and erasure inventories; integrity checks | Module owners | Epic 5 (staging reset); Epic 8 (DR) | First staging reset; first AD-12 drill |
| Synthetic tenant aggregate and creation task; tenant-lifecycle flow review; leftover synthetic-tenant cleanup or reporting | Tenants | Epic 3 (local); Epic 5 (staging E2E); Epic 6 (production synthetic tenant) | Release gate; first production verification |
| Tombstone mirror and lineage protocol, tenant-key store, per-tenant principals, operator artifact and tombstone-mirror fence hook | Memories with EventStore | Epic 8 key restoration and erasure-continuity stories | Before the first key-restore consumer; G2 |
| Qualify the Memories AD-9 adapter boundary and migrate remaining direct Redis coordination to Dapr | Memories with EventStore | Epic 8 adapter-conformance story; G2 opening | G2 |
| Idempotency-intent adapters packaged as extension packages | Folders | Epic 3 (local complete environment); Epic 10 (hosted) | Folders joining a composition |
| Image attestation through Builds `domain-release` or an equivalent attested path | EventStore, Memories | Epic 4 publication workflow | First publication |
| Record Platform's accepted Folders, Projects and McpCli overrides in their upstream architecture and PRD documents | Folders, Projects and McpCli documentation owners | Recovery and deployment story finalization; final MVP evidence | Before affected recovery/deployment stories are finalized; not an enrollment block |
| Agents Story 5.6: full wiring and Level 4/5 evidence | Agents | Epic 12 | EXT-HOST-1 status `Available` |

## Epic List

**Backlog scope: Platform-accountable.**
- Stories cover work in the Platform repository, Administrator operational work, and Builds `hexalith-module` work that implements Platform-owned semantics. Each story is tagged with the repository or owner that implements it.
- Exception: the reference composition's own module work is in scope. This covers Parties adoption and the EventStore, Tenants and Memories declarations the reference composition needs. These stories are tagged with the module's repository and need that module owner's review, because Epic 1 cannot close without them.
- Other EventStore, McpCli and module-owned deliverables appear as named prerequisites on the stories that consume them (see the External Prerequisites Register). Platform integration and verification stories prove those deliverables work in the composition.
- **Cross-repository definition of done:** a story tagged with another repository is done only when its change is merged upstream, released (for tools, as a version Platform accepts), pinned or declared in Platform, and verified from the Platform workspace.
- **Policy decisions are stories.** Every policy the sources leave to "fix before…" is an explicit story at the start of the epic that first uses it, and it produces a decision record. No consumer chooses its own policy.
- Epics 11 and 12 do not block MVP acceptance.

**Dependency flow:**
1. Epic 1 builds the foundation.
2. Epic 2 builds on Epic 1: run ownership comes before scale.
3. Epic 3 builds on Epics 1 and 2. Epic 2's published candidate-and-attachment policy precedes every CI-evidence consumer. Inside Epic 3, the composed host and catalog stories come first, then the local realm and McpCli connected mode, then candidate evidence, then the SM-1, SM-2 and SM-3 evidence stories.
4. Epic 4 needs Epic 1 plus the composed-host, catalog and local-realm foundations of Epic 3. It does not wait for McpCli connected mode (only its staging McpCli story does) or for the Epic 3 evidence stories. It needs the recovery hook *contract* only; module hook implementations are prerequisites of Epics 5 and 8.
5. Epic 5 builds on Epic 4. Its no-production-baseline branch is complete enough to enable the first G1 deployment; after Epic 6 creates the first working production baseline, Epic 5 closes its baseline-dependent rehearsal branches before a second production candidate is accepted.
6. Epics 6 → 7 → 8 → 9 follow the strict G1 → G2 → G3 gate order. Epic 6 implements baseline re-deploy and a guarded shared-infrastructure workflow; Epic 8 supplies the recovery points needed to qualify in-place data restore, named retained-data recovery and successful shared-infrastructure execution.
7. Epic 10's staging enrollment stories can start once Epic 5 exists, and its production enrollment stories once Epic 6 exists. Its MVP acceptance story closes after Epic 9.
8. Epic 11 needs Epics 3 and 4 plus the parity evidence from McpCli and the modules.
9. Epic 12 needs Epics 1–3.

Each epic delivers a usable outcome for the production state that exists when it runs. Epic 5's first-install staging gate is complete before Epic 6; its baseline-dependent qualification is state-contingent and returns after Epic 6 creates the first baseline, rather than blocking that first deployment. Otherwise, epic order is dependency order, not calendar order.

**Early independent track.** These stories depend on no earlier epic but are date-bound or slow to arrange. Each is tagged *independent, pull forward* so sprint planning schedules it at once:

| Story | Epic | Why it moves |
| --- | --- | --- |
| Prove off-node backups and isolated restores for Keycloak PostgreSQL, OpenBao and Memories | 4 | The Kubernetes upgrade is blocked until these proofs pass, and both need immediate scheduling. |
| Upgrade the cluster off Kubernetes 1.34 (end of life 2026-10-27), in place | 4 | Hard external date. Preparation can overlap the backup work, but cluster mutation waits for the verified restore evidence. |
| Close the public Keycloak admin, master-realm and KubeSphere console routes, verified by an external negative probe; disable anonymous registry reads once existing consumers hold pull credentials | 4 | This exposure is live today. Nothing is gained by waiting for G1. |
| Move the privileged Forgejo runner off the cluster node and off any executor host | 4 | Must happen before staging holds real data. |
| Apply publication and operations repository controls | 4 | Publication and notification consumers need these repositories and controls before they can produce accepted evidence. |
| Off-site monitor host, private notification repository and dead-man workflow | 6 | G1 needs working delivery, and this host must be arranged before then. |
| Name the recovery deputy: identity, phishing-resistant MFA, minimum permissions | 6 | A human decision. G1 notifications must reach the deputy, and G2 cannot pass without the deputy. |
| Procure prepared replacement capacity and its location, and select the off-site recovery-executor host | 8 | Procurement lead time; integrated executor commissioning waits for the recovery contracts, artifacts, records and recovery points. |

**Pattern: prove the mechanism on the reference composition, then collect evidence across all modules.** Every mechanism epic, local or hosted, closes when the reference composition demonstrates the mechanism. The reference composition is Parties with EventStore, Tenants and Memories, plus McpCli, and locally also the technical modules from the Platform workspace. A release covers only the modules enrolled so far, and production precondition 7 checks only those. The PRD states that incremental enrollment supports implementation while final MVP acceptance needs all seven modules.

Evidence for success measures that span every module is collected in dedicated closing stories:
- SM-1, SM-2 and SM-3 at the end of Epic 3;
- SM-4, SM-5 and SM-6 across all seven modules in Epic 10.

Each evidence story records the release or source revision, loaded identities, environment, result and diagnostics, and names its module prerequisites. A module that has not adopted leaves MVP acceptance explicitly open.

### Epic 1: Develop a module against its minimum environment
The Parties developer runs and debugs their checkout from the Parties workspace, with Platform as a direct submodule. They get exactly the servers the Parties declaration lists (EventStore, Tenants and Memories) as real services. EventStore, Memories and McpCli developers do the same from the Platform workspace. Breakpoints and source edits reach the running code, only direct references are initialized, and failures name the missing dependency.
- **FRs covered:** FR-2, FR-3 · **Evidence:** SM-2 mechanism, on the reference composition
- **Scope:**
  - The mechanism is proven on Parties plus the three technical modules.
  - In scope, tagged with each module's repository and reviewed by its owner:
    - Parties adoption: a direct `references/Hexalith.Platform` reference, its declaration, and removal of the file-existence switches in `Directory.Build.props`;
    - the EventStore, Tenants and Memories declarations the reference composition needs.
  - Tenants, Folders and Projects workspace adoption is module-owned. Platform supplies the declaration template, validator and adoption guide, and verifies each adoption when its owner delivers.
  - SM-2 across all four domain workspaces closes in Epic 3.
- **Notes:**
  - The epic opens with the first shared artifacts, a Platform-accepted `hexalith-module` version (Builds) and the next major of the declaration schema with its validator.
  - The Platform Aspire model becomes the primary composition, and the Works preview stays the rollback composition.
  - Startup diagnostics must help, not just name: a developer reading a timeout sees which resource is unready and why.

### Epic 2: Test modules against real services, locally and in CI
Developers and CI provision run-owned, real-service environments through the runner, with:
- the versioned environment descriptor, run-scoped ports, resources and volumes (no fixed-port collisions), readiness waits and isolated data;
- a fast failure naming a colliding run;
- safe attachment with finite holds and a withdrawn outcome;
- local environments retained on failure, and CI environments always cleaned up, including after partial startup, with leftovers reported.

Isolated tests run first, without Platform, and the package-mode integration tier blocks CI.
- **FRs covered:** FR-4 (lifecycle, readiness, isolation, attachment, retention, cleanup), FR-5 · **Evidence:** SM-3 mechanism, SM-C4, SM-C5
- **Policy decision (opens the epic):** one shared candidate-and-attachment policy fixes the candidate revision, the merge or release decision its evidence gates, reuse of accepted evidence for unchanged modules, and default and maximum attachment holds with owners and revisit conditions. It is published in the runner and descriptor contract before any consumer implements attachment or accepts CI evidence.
- **Notes:** candidate artifact-identity implementation and evidence follow in Epic 3, once the composed host and run-scoped McpCli exist to report what they loaded; those consumers use Epic 2's policy rather than choosing their own.

### Epic 3: Run the complete Hexalith system locally with agent access
A developer starts all seven MVP modules together with one command, and they become ready within the effective deadline. The environment runs through:
- the Platform-composed `eventstore` host, with Folders' adapters as extension packages;
- a run-scoped catalog;
- a local realm generated from the realm contract;
- the synthetic tenant.

The local McpCli CLI and stdio MCP discover and execute enabled modules' agent-eligible operations, and refuse ineligible, disabled, mismatched and unauthorized ones. CI candidate evidence proves exactly which candidate artifacts were loaded, including inside the composed host and run-scoped McpCli. Baseline-only runs are labelled, and substituted or uncommitted inputs are refused. The epic closes with the local MVP evidence stories for SM-1, SM-2 and SM-3 across all seven modules.
- **FRs covered:** FR-1, FR-4 (candidate artifact-identity evidence), FR-12 (local), plus evidence for FR-2, FR-3 and FR-5 across the set · **Evidence:** SM-1, SM-2, SM-3
- **Policy dependency:** Epic 2's candidate-and-attachment policy must be published in the runner and descriptor contract before this epic accepts CI candidate evidence.
- **Story order:** composed host and catalog → local realm and McpCli connected mode → candidate evidence → SM evidence stories. Epic 4 depends on the composed-host, catalog and local-realm foundations, not on this epic's closing evidence stories.
- **Prerequisites:** see the register. The main ones are EventStore's extension API, catalog schema, gateway metadata endpoint and actor attestation; the realm-contract claims; McpCli's contract amendments; Folders' adapter packages; and, for the evidence stories, adoption by Tenants, Folders and Projects.

### Epic 4: Publish retained releases and run the isolated staging environment
The publication workflow produces an attested, immutable release: application Helm package, composed image, McpCli candidate and release record. The staging executor deploys it by digest into an isolated staging environment on `hexalith.com`. There, team members use the reference composition's supported interfaces and McpCli with staging permissions. Staging cannot claim production names or authority. Folders and Projects join in Epic 10.
- **FRs covered:** FR-10 (staging), FR-12 (staging), NFR-3 (staging-side controls)
- **Early independent stories** (see the Early independent track):
  - Prove off-node backups and isolated restores for Keycloak PostgreSQL, OpenBao and Memories, then perform the blocked in-place Kubernetes upgrade as a separate change window.
  - Close the public admin exposure and anonymous registry reads, issuing pull credentials to existing consumers first.
  - Relocate the Forgejo runner.
  - Apply publication and operations repository controls before notification or publication consumers need them.
- **Notes:**
  - The Aspire-to-Helm qualification comes early and proves deterministic rendering, chart contents, admission and ownership; OCI attestation is proven by publication, and retained-package restore from the off-site replica by retention. The AD-1 fallback to a maintained Helm chart applies if needed.
  - The record encoding, the recovery hook contract and the check-suite contract precede the first publication, because the release record binds their versions.
  - The identity-event capture-lag bound is decided before the staging realm implements bounded event export.
  - The reference composition's critical-flow, E2E and smoke suites have explicit Platform-accountable ownership in this epic and block staging-gate validation.
  - The staging in-place data restore needs a deployed release, so it is proven on the first staging deployment, which holds only synthetic data, before Epic 5 accepts any candidate evidence. Module hook implementations are not needed here.

### Epic 5: Gate every release on exact-release staging evidence
Each candidate is proven in staging against that exact release. Every enrolled module's non-empty critical flows pass. With no production baseline, staging proves installation and candidate behavior. With a production baseline and automatic-recovery eligibility, it upgrades from that baseline and rehearses the rollback set. A retained-data approved path without compatible evidence qualifies its named data recovery and does not prepare automatic rollback. Evidence is bound to the release, has a maximum age and never hides failures. Missing, stale or wrong evidence blocks promotion in both release modes. Unadopted incompatible candidates are reset before the next one. After staging validation passes, the Platform publication workflow alone publishes the stable McpCli version.
- **FRs covered:** FR-6, NFR-1 (compatibility evidence)
- **Policy decisions (open the epic):**
  - evidence maximum age;
  - staging triggers and rerun acceptance that never hides failed attempts;
  - review policy for removing or remapping critical flows;
  - staging-reset maximum duration;
  - the image and dependency vulnerability severity that blocks promotion, and how exceptions are recorded.
- **Conditional closure:** the no-baseline branch enables the first G1 deployment. Baseline-dependent rehearsal branches close after Epic 6 establishes the first working production baseline and before a second production candidate is accepted.
- **Prerequisites:** Epic 4's owned reference-composition suites; module recovery-hook implementations (staging reset); EventStore and Memories image attestation before publication. The Builds check-suite contract lands in Epic 4, before the first publication.

### Epic 6: Deploy to production with verification and one-shot recovery (G1)
Administrator deploys a staged release to `tache.ai` through the Administrator-approved path, with user ingress closed. Platform verifies it against the readiness deadline, the five-minute window and the smoke triggers. On an eligible compatible failure it recovers exactly once automatically, without rewinding data. It supports in-place baseline re-deploy but does not yet claim recovery-point data restore. It keeps a durable per-cause promotion stop that only Administrator clears, and reports every outcome through GitHub to Administrator and the deputy. G1 is reached with the off-site monitor, exposure controls and current infrastructure in place.
- **FRs covered:** FR-7, FR-8, FR-10 (production and shared-infrastructure changes), NFR-1 · **Evidence:** G1; early SM-5 fault rehearsals in approved mode
- **Story 6.1: rehearsal-injection architecture spike.**
  - None of the PRD, spine, spec or addendum says how an SM-5 fault rehearsal makes a staged release fail production verification. The release-invariant record cannot carry the fault, or it would be a different release from the one staging approved.
  - The spike produces an AD amendment through the architecture workflow. Its exit criteria are:
    - a named injection point for each FR-7 trigger (rollout not ready, 60 s unavailability, two consecutive smoke failures, missing result);
    - how an injected fault is classified within the locked attempt;
    - the attempt-record fields that mark an attempt as a rehearsal;
    - rehearsal-labelled GitHub notifications, so the deputy never receives a real alert for a planned fault;
    - injection only inside an Administrator-approved rehearsal attempt, with no standing credential, and recorded in the attempt record.
  - Epic 6 then builds one shared rehearsal harness, which Epics 8, 9 and 10 reuse.
- **Policy decisions (before G1):** the availability-probe stop bound, including how probe failures during a locked attempt count, and the two-way admission-check cadence. Epic 4 has already fixed the identity-event capture-lag bound before staging event export.
- **Early independent stories:** naming the recovery deputy; the off-site monitor host, notification delivery and dead-man workflow.
- **G1 access mechanisms:** the production realm, signed admission records and two-way drift detection are built here, because the standing synthetic check identities and the G1 admission-bounds row need them.
- **Bounded change windows:** infrastructure currency is divided into identity/secrets, shared data services, control-plane/networking, and artifact/backup/administration tooling so each change has a recoverable scope.
- **Shared-infrastructure boundary:** this epic implements the both-lock guarded workflow and its refusal without a complete recovery point. Successful execution and forward-revert qualification wait for Epic 8 recovery points and close in Epic 9.

### Epic 7: Control production access explicitly
Only users that Administrator explicitly admits reach production, with exactly their production permissions, through the API, CLI and MCP. Staging users, workloads, automation and pods are refused at every boundary. Admission is signed, chained and reconciled in both directions. The temporary synthetic grant supplies the SM-4 positive evidence, and its revocation is followed by a denial check.
- **FRs covered:** FR-11, FR-12 (production), NFR-3 (production matrix) · **Evidence:** SM-4 (reference composition), SM-C2
- **Notes:** G1 needs the production realm, the signed admission-record chain and two-way drift detection, so Epic 6 builds them. This epic proves them for human admission, the SM-4 grant and the full isolation matrix.
- **Deferred matrix target:** backup-prefix denial is added only after Epic 8 creates the per-environment-instance backup prefixes; every other G1-existing target is exercised here.

### Epic 8: Recover production from disaster and open it to users (G2)
Administrator or the deputy restores production on prepared capacity within one hour of data loss and four hours of recovery for outages during declared coverage. Recovery never resurrects Memories erasures or revoked admission. Backups run every 30 minutes under independent monitoring. The epic qualifies in-place data restore and named retained-data recovery only after usable recovery points and Memories continuity exist, then completes the isolated drill and opens G2 in the prescribed order.
- **FRs covered:** FR-9, NFR-2 · **Evidence:** SM-6, SM-C3, G2
- **Early independent story:** procure prepared replacement capacity and its location and select the off-site recovery-executor host. Full executor commissioning waits for the retained artifacts, record store, recovery-point prefix, recovery workflows and custody release path. The deputy is named in Epic 6; this epic proves the deputy's key custody and rehearsed restore.
- **Policy decision:** the reduced-recovery operating policy — whether automatic promotion may resume before the return to G2 conditions, drill cadence while replacement capacity is consumed, and how staging is re-established.
- **Notes:** the drill re-verifies backup cadence and retention, availability and recovery-point monitoring, monitor/dead-man silence detection in both directions, actual GitHub delivery, backup-prefix isolation and prior-epoch recovery-hook denial. It reuses the Epic 6 rehearsal harness and its rehearsal-labelled notifications.
- **Prerequisites:** the Memories tombstone mirror, lineage, tenant-key and fence foundations precede their first key-restore consumer. Erasure continuity and the AD-9 adapter-boundary migration are separate G2 qualifications. Module recovery-hook implementations and Administrator-confirmed recovery inventories also precede the drill.

### Epic 9: Promote releases to production automatically (G3)
Releases with valid compatibility evidence against the current baseline, and a passing shared-infrastructure currency check, promote automatically. The SM-5 rehearsals prove every failure trigger and every recovery, stop and approval rule, including all deputy prohibitions and both accepted/unaccepted cause recurrence rules. They also qualify successful shared-infrastructure execution and forward revert after Epic 8 supplies complete recovery points. A policy change suspends automatic promotion until the affected rehearsals repeat.
- **FRs covered:** FR-6 (automatic mode), FR-8, FR-10 (currency check), NFR-1 · **Evidence:** SM-5 (reference composition), SM-C1
- **Policy decision (opens the epic):** the shared-infrastructure currency policy — checked inventory, cadence, allowed lag and effect on approved attempts.
- **Notes:** the SM-5 suite runs on the Epic 6 rehearsal harness; it adds no harness of its own.
- **Prerequisites:** EventStore's G3 confirmations.

### Epic 10: Complete the MVP across every environment
Folders and Projects join staging and production through the same gates. Folders' idempotency-intent adapters run as composed-host extension packages. Each module brings its declarations, critical flows, smoke suites, recovery inventories and hooks, hosted surfaces and McpCli operations. The full seven-module release then passes:
- the staging gate;
- production verification;
- the access matrix;
- a repeat DR drill;
- the SM-5 rehearsals.

The MVP is accepted with all seven modules through G1, G2 and G3.
- **FRs covered:** FR-6, FR-7, FR-9, FR-10 and FR-12 for the full set · **Evidence:** SM-4, SM-5 and SM-6 across all seven modules; MVP acceptance
- **Notes:**
  - Staging enrollment stories can start once Epic 5 exists, and production enrollment once Epic 6 exists.
  - Enrolling a module into production is a release attempt: if a first enrollment fails, its workloads are removed but its data objects are kept.
  - The MVP acceptance story closes after Epic 9.
  - The full-composition DR drill repeats every SM-6 operating-control check: backup cadence and retention, independent availability and freshness monitoring, bidirectional monitor-silence detection, and actual GitHub delivery to Administrator and deputy.
  - The Platform MVP recovery envelope takes precedence over the stricter infrastructure clauses in Folders and Projects (AR-52).
- **Prerequisites:** Folders' adapter packages; Folders and Projects declarations, critical flows, smoke suites, recovery hooks and inventories.

### Epic 11: Retire legacy hosting and module-specific MCP/CLI surfaces *(not MVP-blocking)*
Developers and operators end with one supported hosting path and one CLI/MCP surface. Frozen domain AppHosts and the Works preview retire after parity. Legacy MCP/CLI sources leave every composition and route after their AD-11 evidence passes.
- **FRs covered:** FR-12 (legacy clause)
- **Prerequisites:** McpCli Epic 5 inventories and cutover; each module's parity evidence; EventStore AD-22 removal authority.

### Epic 12: Enroll the Agents host composition (EXT-HOST-1) *(post-MVP)*
Agents runs through Platform's composition with no Agents-owned AppHost, Aspire or ServiceDefaults, and `./eng/verify-agents-host.sh` passes.
- **Covers:** AR-51
- **Prerequisites:** Agents Story 5.6 for full wiring and Level 4/5 evidence.

## Epic 1: Develop a module against its minimum environment

The Parties developer runs and debugs their checkout from the Parties workspace, with Platform as a direct submodule, against exactly the servers the Parties declaration lists (EventStore, Tenants and Memories) as real services. EventStore, Memories and McpCli developers do the same from the Platform workspace. Breakpoints and source edits reach the running code, only direct references are initialized, and failures name the missing dependency. The mechanism is proven on the reference composition; SM-2 across all four domain workspaces closes in Epic 3.

### Story 1.1: Validate module declarations against the Platform declaration schema

As a module developer,
I want a versioned Platform declaration schema and a validator that explains exactly what is wrong,
So that I declare my module's servers, runtime needs and lifecycle once and Platform composes from it.

**Repo:** Hexalith.Builds (schema, validator); Platform owns the semantics · **Covers:** FR-3, AR-8, AR-9, AR-10, AR-47 (validator rule)

**Acceptance Criteria:**

**Given** the next major of `hexalith.module-manifest` published in Builds `schemas/`
**When** a declaration identifies itself with that `schema` value
**Then** the validator accepts the Identity, Runtime, Surfaces, Integration and Lifecycle field groups from the spine's Module declaration convention, and rejects unknown properties, naming the field path
**And** the full field structure is defined now: later epics add stage-specific validation rules, never a new major

**Given** a declaration that uses `hexalith.module-manifest.v1`
**When** it is validated for Platform enrollment
**Then** validation fails, stating that v1 is not accepted and naming the required major

**Given** a declaration with a missing required field, a duplicate module, app or resource identity, or an unsupported major
**When** it is validated
**Then** each problem is reported with file, field and reason, and no partial composition is produced
**And** the validator's supported list holds the current and previous Platform majors

**Given** a required server without a readiness declaration, or a startup override without a finite value and a justification
**When** validated for local enrollment
**Then** it fails as a configuration error

**Given** a startup task
**When** validated
**Then** its scope is one of per-start verify, once-per-environment creation, recovery or operator-only, and its authority class is declared

**Given** a Dapr capability
**When** validated
**Then** it declares a logical role, a recovery class (authoritative-restore, rebuild-only or live-authority-only) and the configuration key that carries its bound component name
**And** a literal component name is rejected

**Given** an enrolled host declaration that maps an MCP endpoint
**When** validated
**Then** it is rejected (AD-11)

### Story 1.2: Make the Builds catalog the single version authority

As a Platform maintainer,
I want every platform component version to come from the Builds catalog,
So that no hard-coded constant can silently disagree with what a composition runs.

**Repo:** Hexalith.Builds · **Covers:** AR-1, AR-53 (version constants, README)

**Acceptance Criteria:**

**Given** `SupportedPlatformPins.cs` and `CompositionToolchainPins.cs`
**When** this story lands
**Then** the hard-coded EventStore, Dapr runtime, Dapr SDK and FrontComposer versions are gone, and every consumer reads the catalog
**And** a search of the tooling source finds none of those version literals

**Given** the catalog's EventStore.Aspire and CommunityToolkit Aspire Dapr hosting pins
**When** either changes
**Then** the two move together, and a mismatched pair fails validation naming both (EventStore.Aspire 3.109.0 needs toolkit beta.767 or later)

**Given** the Aspire CLI and AppHost SDK versions
**When** the tool starts
**Then** it fails if they differ, naming both versions

**Given** the Builds README
**When** updated
**Then** it no longer says the tools are unpublished, and states that versions up to and including 4.27.4 are not Platform-accepted

### Story 1.3: Resolve one active-root source mapping

As a developer,
I want the tool to resolve my workspace into one mapping of which dependencies build from source and which come as packages,
So that the code I edit is the code that runs, and nothing silently substitutes another copy.

**Repo:** Hexalith.Builds · **Covers:** FR-2, AR-5 (mapping, source mode, mode selection)

**Acceptance Criteria:**

**Given** an active workspace root
**When** the tool resolves it
**Then** it computes a single mapping, once, of the active module plus its directly declared `references/*` dependencies, and both build and launch use that mapping

**Given** the active root's direct `references/*` submodules
**When** the tool initializes dependencies
**Then** it initializes only those, never recursively and never with a remote update
**And** if any nested submodule is already initialized, it fails naming its path and telling the developer to deinitialize it

**Given** a root-declared Hexalith dependency in source mode
**When** building
**Then** it builds from its checkout in Debug
**And** every other dependency resolves as a NuGet package or released image at the catalog version

**Given** a required declared source path that is missing
**When** resolving
**Then** the tool fails naming the dependency and path
**And** it attempts no ancestor, sibling, recursive, nested-Platform or package fallback

**Given** both a source copy and a package copy of the same identity
**When** building
**Then** the build fails naming the identity and both origins

**Given** a workspace where optional files exist or are missing
**When** the mode is selected
**Then** file existence never determines the mode; only the pinned tool's mode property does

### Story 1.4: Enforce the Platform identity and support package mode

As a module maintainer,
I want the tool to bind my workspace to one Platform identity and to build the CI-style package mode,
So that local and CI runs use the Platform composition, catalog and tool I actually pinned.

**Repo:** Hexalith.Builds · **Covers:** FR-2 (the CI/CD rule), AR-5 (Platform identity, package mode)

**Acceptance Criteria:**

**Given** a workspace other than the Platform repository
**When** the tool runs
**Then** it requires a direct `references/Hexalith.Platform` and uses that submodule's commit as the Platform identity
**And** in the Platform repository, the identity is HEAD

**Given** a Platform identity
**When** it is resolved
**Then** it yields the pinned Builds catalog commit and the supported tool-version range

**Given** package mode
**When** any of these holds:
- the composition commit differs from the submodule HEAD;
- the Builds submodule differs from the pinned catalog;
- the tool is outside the supported range;
- the Platform submodule is untagged or dirty

**Then** the run is refused, naming the mismatch
**And** in source mode the same conditions produce only warnings

**Given** package mode outside the Platform repository
**When** building
**Then** the Platform composition builds from the Platform submodule at its release tag
**And** the module under test builds in Release against NuGet library packages at the catalog version
**And** dependency services run from their released images at the catalog version

**Given** package mode inside the Platform repository
**When** building
**Then** the composition builds from HEAD

### Story 1.5: Declare EventStore, Memories and Tenants for the reference composition

As the EventStore, Memories or Tenants owner,
I want my module declared through the Platform schema,
So that Platform can compose it as a real service for the reference composition and for the modules that depend on it.

**Repo:** Hexalith.EventStore, Hexalith.Memories, Hexalith.Tenants (owner review) · **Covers:** FR-3, AR-8, AR-9, AR-10

**Acceptance Criteria:**

**Given** the EventStore, Memories and Tenants declarations
**When** validated
**Then** each declares its app and resource identities, its logical Dapr roles with their configuration keys, and its readiness checks and startup tasks with their scopes

**Given** Memories' declaration
**When** validated
**Then** the FalkorDB graph, the Redis search/vector indexes and the SET NX preflight-dedup row are its only declared provider-SDK exceptions (AD-9)
**And** its remaining direct Redis coordination is marked transitional, which does not block local enrollment

**Given** the EventStore and Memories minimum environments
**When** declared
**Then** they contain no invented domain dependency

**Given** Tenants' existing AppHost
**When** this story lands
**Then** that AppHost is frozen, with no new cross-module wiring
**And** Tenants adopting Platform in its own workspace remains module-owned work, outside this story

### Story 1.6: Compose environments from declarations with one Platform Aspire model

As a developer,
I want Platform's single Aspire application model to compose exactly the servers a declaration enables,
So that every environment comes from one definition instead of per-module AppHosts.

**Repo:** Hexalith.Platform (model); Hexalith.Builds (Module.AppHost, EventStoreHost, UiHost) · **Covers:** FR-3, AR-3, AR-7 (with no extension packages), AR-49, AR-50 (EventStore.Aspire and toolkit alignment)

**Acceptance Criteria:**

**Given** a domain module's declaration
**When** the Platform model composes it
**Then** it adds the declared servers plus EventStore, Tenants and Memories, as real services from their declarations (Story 1.5), through ordinary C# helpers that consume the declarations
**And** unrelated modules are absent

**Given** a composition that includes EventStore, Tenants and Memories
**When** it is built
**Then** each resource carries its declared readiness checks and startup tasks, so the runner can wait on module-defined readiness

**Given** a composition that includes EventStore
**When** it starts
**Then** exactly one `eventstore` app runs
**And** Platform composes it from the EventStore server, with no extension packages, through the source mapping from Story 1.3
**And** extension packages are added in Epic 3

**Given** Builds.Module.AppHost
**When** it runs
**Then** it launches the Platform model instead of composing its own environment
**And** Builds.Module.EventStoreHost either becomes the starting point for the composed host or is removed
**And** Builds.Module.UiHost stays a run-scoped runner host, never a hosted workload

**Given** the existing Works preview
**When** `Platform:Works:Enabled` is not set
**Then** Works is not composed
**And** when it is set, the preview behaves as it does today, remaining the rollback composition

**Given** the AppHost form (the file-based `apphost.cs` or a project)
**When** this story completes
**Then** the chosen form and its compatibility with the Aspire testing builder are recorded, because a file-based AppHost cannot use `DistributedApplicationTestingBuilder` through generated `Projects` types

**Given** the Platform AppHost's EventStore.Aspire and Dapr toolkit pins
**When** the model builds
**Then** they match the catalog's pair (from Story 1.2)

### Story 1.7: Ratify local Dapr conventions and bind component names

As a module developer,
I want Platform to own local Dapr wiring (mTLS, access control, component names and subscriptions),
So that my module stops carrying Dapr plumbing and behaves the same in every composition.

**Repo:** Hexalith.Platform, consulting EventStore and Commons · **Covers:** AR-10, AR-54, AR-49 (refusing new hosting helpers)

**Acceptance Criteria:**

**Given** a local composition
**When** the Dapr sidecars start
**Then** local mTLS, a per-receiver access-control policy and scoped components follow the ratified convention
**And** the convention is recorded in the architecture through the architecture workflow

**Given** a declared Dapr capability
**When** Platform renders the components
**Then** Platform assigns the component name and passes it through the configuration key the declaration names
**And** no module needs a literal component name inside a Platform composition

**Given** declared topics
**When** composed
**Then** Platform renders declarative Subscription objects with the bound component name and the dead-letter policy

**Given** the canonical ServiceDefaults and Aspire Dapr package set
**When** it is recorded in the spine
**Then** hosting helpers built on that set are accepted
**And** until it is recorded, new hosting helpers are refused

### Story 1.8: Run and debug with readiness-aware startup and useful diagnostics

As a developer,
I want `hexalith-module run`, `debug` and `down` to wait for real readiness and explain failures,
So that I debug a working environment, and a failure tells me what's wrong instead of dumping logs.

**Repo:** Hexalith.Builds · **Covers:** FR-2 (debugging), AR-6, the FR-4 readiness rules for local runs, SM-C5

**Acceptance Criteria:**

**Given** a declared composition
**When** I run `hexalith-module debug`
**Then** the active module and root-declared Hexalith dependencies start from source in Debug, a debugger can attach, and breakpoints in the active checkout are hit

**Given** startup is in progress
**When** the tool waits
**Then** it waits until every required resource reports its module-defined readiness and every required startup task succeeds
**And** a running process alone never counts as ready

**Given** the default 10-minute deadline, measured from the start request
**When** a module declares a finite override with a justification
**Then** the effective deadline appears in the diagnostics
**And** the complete environment uses the largest declared override, reported as an override

**Given** the deadline expires, or startup fails definitively before it
**When** the tool reports
**Then** it names each unready resource with its last readiness result or error, the effective deadline and where the preserved diagnostics are, not a raw log dump

**Given** any run
**When** startup completes
**Then** the actual startup duration, the readiness results and the effective deadline are recorded

**Given** `hexalith-module down`
**When** invoked
**Then** it stops only the environment this tool started

### Story 1.9: Ratify and pin the first Platform-accepted tool version

As a Platform maintainer,
I want a released `hexalith-module` version that Platform formally accepts and pins,
So that module workspaces can depend on it.

**Repo:** Hexalith.Builds, Hexalith.Platform · **Covers:** AR-53 (first accepted version; the blocking CI tier follows in Story 2.8)

**Acceptance Criteria:**

**Given** the Builds changes from Stories 1.1–1.8 merged
**When** the next tool version is released to NuGet
**Then** it is recorded as the first Platform-accepted version
**And** versions up to and including 4.27.4 are recorded as not accepted

**Given** the Platform repository
**When** the tool is pinned
**Then** `.config/dotnet-tools.json` pins the accepted version
**And** the Platform identity records the supported tool range and the Builds catalog commit

### Story 1.10: Run and debug EventStore and Memories from the Platform workspace

As an EventStore or Memories developer,
I want to run and debug my module from source in the Platform workspace against its declared minimum environment,
So that my breakpoints and edits affect the running module without a separate module-workspace workflow.

**Repo:** Hexalith.EventStore, Hexalith.Memories (owner review), Hexalith.Platform · **Covers:** FR-2, FR-3, AR-50

**Acceptance Criteria:**

**Given** EventStore's module-owned declaration from Story 1.5
**When** `hexalith-module debug` runs from the Platform workspace
**Then** the checkout under `references/Hexalith.EventStore` runs in Debug against its declared minimum environment, and a breakpoint there is hit
**And** the same holds for Memories under `references/Hexalith.Memories`

**Given** EventStore's `RepositoryProjectPaths`
**When** this story lands
**Then** its probing of current, ancestor and sibling paths is removed, and resolution goes only through the source mapping

**Given** a technical module's own-repository AppHost
**When** it is used
**Then** it serves only that repository's own tests and never counts as Platform evidence

### Story 1.11: Run and debug McpCli from the Platform workspace

As a McpCli developer,
I want to run and debug McpCli from source in the Platform workspace with its declared minimum environment,
So that McpCli work uses the same composition as every other module.

**Repo:** Hexalith.McpCli (owner review), Hexalith.Platform · **Covers:** FR-2, FR-3, AR-46 (the run-scoped build)

**Acceptance Criteria:**

**Given** `references/Hexalith.McpCli` in Platform
**When** `hexalith-module debug` targets McpCli
**Then** the checkout builds in Debug as a run-scoped, never-published tool
**And** a breakpoint is hit while it runs an offline contract inspection
**And** connected execution against the gateway is added in Epic 3

**Given** McpCli's own `references/Hexalith.Platform`
**When** the tool runs in the McpCli repository
**Then** that submodule is McpCli's Platform identity
**And** Platform evidence still comes only from the Platform workspace

**Given** the sibling `../mcpcli` checkout on disk
**When** resolving
**Then** it is never used

**Given** McpCli's declaration
**When** validated
**Then** it declares its minimum environment, and it declares no MCP endpoint host because stdio is the only MVP MCP surface

### Story 1.12: Adopt Parties as the reference domain module

As the Parties developer,
I want to run and debug Parties from its own workspace with Platform as a direct submodule,
So that I work against real EventStore, Tenants and Memories, running only the servers Parties declares.

**Repo:** Hexalith.Parties (owner review) · **Covers:** FR-2, FR-3, SM-2 (proven on the reference composition), AR-50

**Acceptance Criteria:**

**Given** the Parties repository
**When** adopted
**Then** it declares `references/Hexalith.Platform`, `references/Hexalith.EventStore`, `references/Hexalith.Tenants` and `references/Hexalith.Memories` directly, and pins the accepted tool in `.config/dotnet-tools.json`

**Given** the Parties declaration
**When** composed
**Then** Platform runs the active Parties checkout with EventStore, Tenants and Memories, and no unrelated domain module
**And** the Parties ACL file is a declaration source

**Given** Parties' `Directory.Build.props`
**When** this story lands
**Then** its per-dependency file-existence source switches are removed, and only the tool selects the mode
**And** Parties reads component names through configuration keys, and its declared topics use declarative subscriptions

**Given** `hexalith-module debug` in the Parties workspace
**When** a breakpoint is set and a source edit is made in Parties
**Then** the breakpoint is hit and the edit affects the running service
**And** Platform's own nested references stay uninitialized
**And** this is recorded as SM-2 evidence for the reference composition, with source revision, environment, result and diagnostics

### Story 1.13: Publish the domain-module enrollment kit

As a Tenants, Folders or Projects owner,
I want a template, a guide and a validation command for adopting Platform,
So that my team adopts on its own schedule and Platform verifies the result consistently.

**Repo:** Hexalith.Platform · **Covers:** AR-8, AR-49, AR-50 (hosting inventory)

**Acceptance Criteria:**

**Given** the kit
**When** a module owner follows it
**Then** it covers:
- adding direct `references/` entries;
- writing the declaration from the template;
- pinning the accepted tool;
- removing sibling, nested and package-fallback helpers and file-existence selectors;
- binding component names through configuration keys;
- using declarative subscriptions

**Given** a module workspace
**When** `hexalith-module` validates it
**Then** each adoption requirement is reported as pass or fail, with the file and reason

**Given** a completed adoption
**When** Platform verifies it
**Then** a checklist records the SM-2 demonstration: a breakpoint and an edit in the active checkout, and direct-only initialization
**And** that record feeds the SM-2 evidence story in Epic 3

**Given** the module's AppHost, Aspire and ServiceDefaults projects
**When** they are recorded with the kit's hosting-inventory template
**Then** each is marked frozen, with a retirement plan that follows the Migration and coexistence rules

## Epic 2: Test modules against real services, locally and in CI

Developers and CI provision run-owned, real-service environments through the runner: run-scoped resources with no fixed-port collisions, readiness waits, isolated data, safe attachment with finite holds and a withdrawn outcome, local retention on failure, and CI environments that always clean up with leftovers reported. Isolated tests run first without Platform, and the package-mode integration tier blocks CI. Candidate artifact-identity evidence follows in Epic 3.

### Story 2.1: Decide the candidate and attachment policy

As a Platform maintainer,
I want one shared policy for candidate evidence and finite attachment holds,
So that no consumer invents its own candidate-selection or attachment rules.

**Repo:** Hexalith.Platform with Hexalith.Builds · **Covers:** AR-16 (candidate and attachment policy)

**Acceptance Criteria:**

**Given** the FR-4 candidate-evidence rules
**When** this story completes
**Then** the decision record fixes whether the candidate revision is the pull-request head or merge result, which merge or release decision its evidence gates, and when accepted evidence for an unchanged module may be reused

**Given** the FR-4 attachment rules
**When** the same decision record is completed
**Then** it fixes a finite default hold limit, whether a run may request a shorter hold, and the maximum any run may request

**Given** the candidate and attachment decisions
**When** ownership is recorded
**Then** each decision names its owner and the condition for revisiting it

**Given** the decision
**When** it is published
**Then** it is recorded for inclusion in the runner and descriptor contract before any consumer implements attachment or accepts candidate evidence

### Story 2.2: Define the test environment descriptor and result contract

As a module test author,
I want a versioned environment descriptor and result contract from the runner,
So that my tests consume endpoints and composition identity instead of starting their own AppHost.

**Repo:** Hexalith.Platform (contract), Hexalith.Builds (implementation in `hexalith-module`) · **Covers:** FR-4, AR-13, AR-14

**Acceptance Criteria:**

**Given** the contract
**When** it is published as a versioned schema
**Then** the descriptor carries:
- run and owner identity;
- composition identity and mode;
- per-resource endpoints;
- the run-scoped catalog instance name;
- the synthetic tenant identifier field;
- expected and loaded artifact-identity fields;
- the candidate-revision or baseline-only label;
- attachment holds

**And** the result contract carries each run's terminal outcome, one of passed, failed, cancelled or withdrawn

**Given** a descriptor from an unsupported contract version
**When** a consumer reads it
**Then** it fails, naming the supported versions

**Given** fields that Epic 3 populates (loaded identities, candidate label)
**When** this story lands
**Then** those fields are defined and marked not-yet-populated, so Epic 3 adds values without a version change

### Story 2.3: Make local composition resources run-scoped

As a developer,
I want every port, volume and shared resource of a composition to be scoped to its run,
So that two runs on one machine never collide.

**Repo:** Hexalith.Platform, Hexalith.Builds · **Covers:** AR-2, AR-15 (reconcile fixed resources)

**Acceptance Criteria:**

**Given** `DaprSelfHostedMtls.cs`
**When** this story lands
**Then** the fixed host ports 50001, 51005 and 51006 and the scheduler volume `hexalith-platform-dapr-scheduler` are allocated per run

**Given** the dapr-init Redis, the fixture lock, fixed-port Keycloak and the shared certificate directory
**When** a run starts
**Then** each is run-scoped or explicitly shared read-only, with the choice recorded

**Given** two runs of the reference composition started on one machine
**When** both reach readiness
**Then** neither uses the other's ports, volumes or data

### Story 2.4: Provision a run-owned environment for a test suite

As a developer,
I want `hexalith-module test` to provision a fresh run-owned environment for my suite and hand my tests its descriptor,
So that integration tests run against real services with isolated data.

**Repo:** Hexalith.Builds · **Covers:** FR-4 (composition and readiness, run isolation), AR-13, AR-15 (`HXR003`)

**Acceptance Criteria:**

**Given** a suite or compatible batch
**When** `hexalith-module test` runs
**Then** the runner provisions a fresh run-owned environment from the module declaration and active checkout, waits for readiness under the rules from Story 1.8, then supplies the descriptor to the tests

**Given** another run already holds a conflicting environment
**When** a new run starts without an explicit attach
**Then** it fails fast, naming the blocking run

**Given** the environment's data
**When** tests run
**Then** they neither depend on data left by other runs nor reach staging or production application data

**Given** the unqualified descriptor path that reports `HXR003` today
**When** this story lands
**Then** the runner uses the qualified descriptor from Story 2.2, and `HXR003` no longer occurs for declared compositions

### Story 2.5: Apply outcome, retention and cleanup rules to local runs

As a developer,
I want a passing run to clean up and a failing run to leave its environment for me,
So that I can debug failures without environments piling up.

**Repo:** Hexalith.Builds · **Covers:** FR-4 (completion, cleanup and diagnostics), AR-13, SM-C4

**Acceptance Criteria:**

**Given** a local run
**When** it reaches its first terminal outcome
**Then** that outcome is recorded immediately
**And** on success the environment cleans up automatically
**And** on a test failure, or a startup failure or timeout, the surviving resources are retained

**Given** retained and pending-cleanup environments
**When** listed
**Then** each shows its owner, age and failed runs

**Given** an explicit developer stop or cleanup
**When** invoked on a retained environment
**Then** only the resources that environment owns are removed
**And** an Aspire environment the developer started separately keeps running

**Given** a cleanup
**When** it runs or is retried
**Then** it is idempotent, removes resources rather than just metadata, and reports every owned resource left behind
**And** an incomplete cleanup is never recorded as complete

**Given** any finished run
**When** its environment is gone
**Then** its test results and classified, redacted diagnostics remain available

### Story 2.6: Attach a run to a compatible environment

As a developer,
I want to attach a test run to an existing compatible environment under a finite hold,
So that I can reuse a running environment safely without it vanishing mid-run.

**Repo:** Hexalith.Builds · **Covers:** FR-4 (run isolation and ownership), AR-13, AR-16

**Acceptance Criteria:**

**Given** an explicit attach request
**When** the target is a local or CI run-owned environment with compatible composition, mode, artifact identities, readiness and data isolation
**Then** the attachment is accepted and recorded with its hold deadline, shown with the environment

**Given** an attach request to a hosted environment, or to an incompatible one
**When** evaluated
**Then** it is refused with the reason

**Given** an owner run that has finished
**When** a new attach is requested
**Then** it is refused, unless the local environment is retained after a failure

**Given** an accepted attachment
**When** the owner finishes
**Then** automatic cleanup waits until every attached run ends or its hold deadline passes

**Given** an attached run that is cancelled
**When** it ends
**Then** only that run ends, and the owner's environment keeps running

### Story 2.7: Withdraw attached runs and keep failure retention

As a developer,
I want attached runs ended cleanly and honestly when their environment goes away,
So that no withdrawn run is ever mistaken for a pass or a failure.

**Repo:** Hexalith.Builds · **Covers:** FR-4 (withdrawn outcome, retention), AR-13, SM-C4

**Acceptance Criteria:**

**Given** an attached run that reaches its hold limit
**When** the limit expires
**Then** the run ends and is recorded as withdrawn: neither a pass nor a failure, and never valid integration evidence

**Given** owner cancellation or an explicit developer stop
**When** attached runs are still active
**Then** they are ended and reported as withdrawn before any cleanup starts

**Given** a local attached run that fails while the owner passes
**When** the owner finishes
**Then** the environment is retained under the failure rule and listed with the failed run

**Given** an environment retained after a failure
**When** a later cancellation happens
**Then** the retention remains until an explicit developer cleanup

### Story 2.8: Run CI integration on disposable runners with guaranteed cleanup

As a module maintainer,
I want CI to run isolated tests first and then package-mode integration in a run-owned environment that always cleans up,
So that CI evidence is trustworthy and CI never leaks resources or touches hosted credentials.

**Repo:** Hexalith.Builds (reusable workflows, runner) · **Covers:** FR-4 (CI rules), FR-5, AR-12, AR-53 (blocking CI tier), SM-C4

**Acceptance Criteria:**

**Given** a module's CI
**When** it runs on a disposable Linux GitHub-hosted runner
**Then** isolated tests run first, without Platform or any domain server, followed by real-service integration through the runner in package mode as the blocking tier
**And** passing isolated tests alone never satisfies integration acceptance
**And** the advisory module-AppHost tier is retired

**Given** a CI run that succeeds, fails or is cancelled, including during provisioning
**When** it ends
**Then** every resource it owns is cleaned up
**And** a run is never counted as cleaned up while any of its owned resources remain

**Given** a CI attachment
**When** requested
**Then** it is limited to runs in the same CI job
**And** attached runs end before the job's cleanup, including on job cancellation

**Given** CI credentials
**When** inspected
**Then** CI holds no staging or production provider credentials, and its only registry access is a read-only pull credential

### Story 2.9: Migrate EventStore and Parties test fixtures to the environment descriptor

As an EventStore or Parties test author,
I want my integration fixtures to consume the runner's descriptor,
So that my tests run in Platform-owned environments instead of starting their own AppHost.

**Repo:** Hexalith.EventStore, Hexalith.Parties (owner review) · **Covers:** FR-4, FR-5, AR-13

**Acceptance Criteria:**

**Given** `EventStore.Testing` and `EventStore.Testing.Integration`
**When** used in a Platform composition
**Then** they read the descriptor from Story 2.2 and never start an AppHost
**And** the non-exported own-repository fixture path stays available only for EventStore's own tests

**Given** the Parties integration tests
**When** run through `hexalith-module test`
**Then** they run against real EventStore, Tenants and Memories from the descriptor

**Given** the Parties isolated tests
**When** run from the test project
**Then** they pass with no Platform and no domain server started, using lightweight doubles only inside the tests

### Story 2.10: Qualify the runner lifecycle on the reference composition

As a Platform maintainer,
I want the runner lifecycle proven by recorded scenarios on the reference composition,
So that local and CI integration evidence can be accepted.

**Repo:** Hexalith.Platform, Hexalith.Builds · **Covers:** FR-4, FR-5, SM-3 (proven on the reference composition), SM-C4, SM-C5

**Acceptance Criteria:**

**Given** the Parties reference composition
**When** scenarios run locally and in CI
**Then** each of these is recorded with its expected ownership, retention, cleanup and leftover reporting:
- success, failure, startup timeout, cancellation and collision;
- owner finish before attachment end, and owner cancellation before attachment end;
- attached-run failure and hold expiry;
- failure followed by cancellation

**Given** each scenario
**When** recorded
**Then** the record holds the source revision, environment, result, actual startup duration, any timeout override and diagnostics
**And** no other environment's resources were touched

**Given** the lifecycle implementation
**When** released
**Then** the tool version that carries it is recorded as Platform-accepted and pinned in Platform and in Parties

## Epic 3: Run the complete Hexalith system locally with agent access

A developer starts all seven MVP modules together with one command. They run through the Platform-composed `eventstore` host with extension packages, a run-scoped catalog, a local realm generated from the realm contract and the synthetic tenant. The local McpCli CLI and stdio MCP discover and execute agent-eligible operations and refuse everything else. CI candidate evidence proves exactly which artifacts were loaded. The epic closes with the SM-1, SM-2 and SM-3 evidence stories across all seven modules.

### Story 3.1: Apply the candidate-selection policy to the runner contract

As a Platform maintainer,
I want the runner and descriptor contract to enforce the approved candidate-selection policy,
So that every module produces and consumes candidate evidence consistently.

**Repo:** Hexalith.Platform with Hexalith.Builds · **Covers:** AR-14, AR-16 (candidate-policy implementation)

**Acceptance Criteria:**

**Given** the decision record from Story 2.1
**When** the runner selects a candidate
**Then** it selects exactly the declared pull-request head or merge result and records the merge or release decision that the evidence gates

**Given** accepted evidence for an unchanged module
**When** a consumer requests reuse
**Then** the runner accepts it only under the policy's recorded reuse conditions and records the source evidence and target decision
**And** it rejects reuse when any declared condition is not met, naming the condition

**Given** a candidate descriptor and result
**When** they are written
**Then** they carry the policy identifier, candidate revision, gated decision and any reused-evidence lineage defined by the versioned contract

**Given** a consumer that attempts to select or accept a candidate by another rule
**When** the contract is validated
**Then** validation fails and names the policy mismatch

### Story 3.2: Compose the eventstore host with enrolled extension packages

As a module developer with EventStore extensions,
I want Platform to compose my extension packages into the one `eventstore` host,
So that my module's semantics run in the shared gateway without a module-built `eventstore` image.

**Repo:** Hexalith.Platform, Hexalith.Builds · **Covers:** FR-1, AR-7, AR-62 · **Prerequisites:** EventStore extension API with supported majors

**Acceptance Criteria:**

**Given** enrolled modules that declare extension packages
**When** the composition builds in source or package mode
**Then** Platform assembles one `eventstore` host from the EventStore server plus those packages, through the source mapping

**Given** an extension package targeting an extension-API major that the server does not support, or two packages registering the same extension
**When** building
**Then** the build fails, naming the module and the major or duplicate

**Given** an extension package that declares Dapr roles, references a provider SDK or Dapr client, or declares a secret
**When** validated
**Then** it is rejected, unless it belongs to a named AD-9 exception with its own least-privilege secret

**Given** a module artifact published under the image name `eventstore`
**When** validated
**Then** it is rejected

**Given** a Platform-owned test extension package built against the supported extension-API major
**When** composed
**Then** the host starts and reports readiness with that extension loaded
**And** Folders' idempotency-intent adapters, the MVP's real consumer, are proven in Story 3.6 locally and Story 10.1 hosted, so neither this story nor Epic 4 waits on Folders

### Story 3.3: Generate the run-scoped routing catalog

As a developer,
I want Platform to generate the routing catalog deterministically from declarations and Contracts,
So that the gateway routes and restricts operations from one source, with no hand-maintained list.

**Repo:** Hexalith.Platform · **Covers:** FR-12, AR-11, AR-62 · **Prerequisites:** EventStore.Contracts routing catalog schema

**Acceptance Criteria:**

**Given** enrolled declarations and Contracts
**When** the generator runs twice on the same inputs
**Then** it produces identical generations
**And** each route entry carries the operation's contract-schema digest and its surface eligibility (agent-eligible, UI-only, confirmation-required or step-up), both covered by that digest

**Given** a local or CI run
**When** it composes
**Then** the catalog instance is run-scoped and named in the environment descriptor

**Given** a supplied committed generation
**When** a new generation is generated from it
**Then** the committed idempotency and key entries carry over as non-executable retention entries

**Given** the gateway running the active generation
**When** an operation is invoked
**Then** eligibility is enforced from that generation
**And** the metadata endpoint lists only executable entries

### Story 3.4: Generate the local realm from the realm contract

As a developer,
I want the local identity realm generated from the same versioned realm contract that staging and production use,
So that local identity behaves like hosted identity and no development key becomes trust.

**Repo:** Hexalith.Platform · **Covers:** AR-25, AR-26, AR-27, AR-50 (HS256 removal) · **Prerequisites:** realm-contract claim names and semantics from EventStore

**Acceptance Criteria:**

**Given** the value-free, versioned realm contract
**When** the local realm is generated
**Then** it contains the surface classes `ui`, `agent` and `service`, a distinct client per calling surface mapped to exactly one class, the token-exchange permissions and preconditions, the admission groups, the refresh-token settings and the synthetic tenant identifier

**Given** the public McpCli client
**When** it is generated
**Then** it is an `agent`-class public client without `offline_access`
**And** full scope is disabled, with audiences and roles mapped explicitly

**Given** the Works preview
**When** the local realm lands
**Then** the preview's HS256 key is removed

### Story 3.5: Provide the synthetic tenant in local and CI environments

As a test author,
I want every run-owned environment to have the synthetic tenant ready and identified,
So that tests and smoke-style checks write only synthetic data that modules exclude from real views.

**Repo:** Hexalith.Platform · **Covers:** AR-27 · **Prerequisites:** Tenants' synthetic tenant aggregate and idempotent creation task

**Acceptance Criteria:**

**Given** a run-owned environment
**When** it starts
**Then** Tenants' creation task, keyed by the Platform-owned synthetic tenant identifier, runs once for the environment and succeeds idempotently
**And** the identifier appears in the descriptor and the realm-contract instance

**Given** synthetic actor and workload clients
**When** they obtain tokens
**Then** the tokens are flagged synthetic and carry no admin or cross-tenant grant

**Given** a module's real-tenant views and aggregates
**When** the synthetic tenant exists
**Then** it is excluded by its identifier

### Story 3.6: Run all seven MVP modules together locally

As a developer,
I want one command to start the complete MVP environment,
So that I can test and debug the whole supported system on my machine.

**Repo:** Hexalith.Platform · **Covers:** FR-1, AR-6 · **Prerequisites:** Folders and Projects declarations and adoption; Folders' extension packages

**Acceptance Criteria:**

**Given** the Platform workspace
**When** the complete environment is requested
**Then** EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories start with their required supporting components, through one composed `eventstore` host that includes Folders' adapters
**And** a run-scoped McpCli is built through the source mapping

**Given** module startup overrides
**When** the complete environment starts
**Then** the effective deadline is the largest declared override, reported as an override

**Given** readiness
**When** all seven report module-defined readiness within the effective deadline
**Then** the environment is declared ready, and the actual duration is recorded
**And** otherwise the unready resources are named, as in Story 1.8

### Story 3.7: Discover enabled operations through connected McpCli

As a developer or agent,
I want McpCli to discover exactly the operations the selected environment can execute,
So that I never see or call an operation that environment cannot run.

**Repo:** Hexalith.Platform (integration), Hexalith.McpCli (owner review) · **Covers:** FR-12, AR-46, AR-48, AR-62 · **Prerequisites:** EventStore gateway metadata endpoint; McpCli contract amendments

**Acceptance Criteria:**

**Given** a McpCli profile for the local environment
**When** it is created
**Then** it binds one gateway, issuer, audience and token source, with no mixing or fallback between environments

**Given** connected mode
**When** McpCli discovers operations
**Then** an operation is executable only if the gateway metadata serves it as executable, with a contract-schema digest matching McpCli's Contracts and an eligibility that admits McpCli
**And** McpCli uses the served eligibility, not its bundled copy

**Given** an unknown operation, or one whose contract digest does not match
**When** it is discovered
**Then** it is shown as non-executable
**And** offline contract inspection never marks anything as executable

**Given** a disabled module
**When** discovery runs
**Then** its operations are unavailable, and discovery never enables the module or routes to another environment

### Story 3.8: Execute and refuse operations through local McpCli CLI and MCP

As a developer or agent,
I want CLI and stdio MCP invocations to execute permitted agent-eligible operations and refuse everything else,
So that McpCli is a safe, consistent route to module operations.

**Repo:** Hexalith.Platform, Hexalith.McpCli (owner review) · **Covers:** FR-12, AR-26, AR-46 · **Prerequisites:** EventStore original-actor and originating-surface attestation (for cross-module steps)

**Acceptance Criteria:**

**Given** an agent-eligible command or query the user is permitted to run
**When** it is invoked through the CLI head and through the stdio MCP head
**Then** it executes through the local gateway with the expected result

**Given** a UI-only or confirmation-required operation, an operation of a disabled module, a mismatched contract, or an operation outside the user's permissions
**When** it is invoked through either head
**Then** it is refused, even for a user permitted to perform it elsewhere

**Given** an actor or surface value supplied by a flag, an environment variable or a profile
**When** an invocation runs
**Then** McpCli refuses the value, and the gateway derives actor and surface from the authenticated token
**And** a Contracts-declared actor property that differs from the token subject is rejected

**Given** each client in the surface map
**When** its tokens are used outside its class, directly or through one cross-module hop
**Then** the call is denied, including a token carrying a confidential client's audience and an exchanged token replayed to an internal host

### Story 3.9: Prove candidate artifact identity in CI evidence

As a module maintainer,
I want CI evidence to prove exactly which build of my candidate the environment loaded,
So that candidate acceptance can never pass on another build, revision or uncommitted change.

**Repo:** Hexalith.Builds, Hexalith.Platform · **Covers:** FR-4 (candidate evidence), AR-5 (candidate mapping), AR-12 (evidence binding)

**Acceptance Criteria:**

**Given** a candidate under the policy from Story 3.1
**When** CI builds it
**Then** every artifact of the candidate module comes from its own Release build of the declared committed revision: Contracts, server and extension packages, services and tools
**And** those artifacts replace that module's catalog artifacts in the one mapping used by the composed host, the run-scoped McpCli and the tests, while other modules stay catalog-pinned

**Given** a completed run
**When** its evidence is written
**Then** it binds the candidate, its revision and the expected content identities to the identities the environment actually loaded, including those inside the composed host and the run-scoped McpCli

**Given** evidence with a missing identity, an artifact from another build or revision, an uncommitted input or a cache-substituted package
**When** it is evaluated for candidate acceptance
**Then** it is refused, naming the offending artifact

**Given** a run with no candidate
**When** it is recorded
**Then** it is labelled baseline-only and cannot satisfy candidate acceptance

### Story 3.10: Record SM-1 evidence for the complete local environment

As a Platform maintainer,
I want SM-1 demonstrated and recorded for all seven modules,
So that local MVP acceptance rests on evidence, not claims.

**Repo:** Hexalith.Platform · **Covers:** SM-1, FR-1, FR-12, SM-C5

**Acceptance Criteria:**

**Given** the complete local environment
**When** it starts
**Then** all seven modules and their required supporting resources become ready under the effective deadline
**And** the record holds the actual duration, any override, the source revision, loaded identities and diagnostics

**Given** each enabled module that declares agent-eligible operations
**When** named commands and queries are executed through the CLI and through MCP
**Then** each returns the expected result
**And** the record names every demonstrated operation (an empty catalog cannot satisfy this)

**Given** refusal cases
**When** they are run
**Then** ineligible, disabled-module, mismatched-contract and unauthorized invocations are refused and recorded

### Story 3.11: Record SM-2 evidence across every workspace

As a Platform maintainer,
I want SM-2 demonstrated in each domain-module workspace and for each technical module,
So that module-workspace development is proven for the whole MVP set.

**Repo:** Hexalith.Platform · **Covers:** SM-2, FR-2, FR-3 · **Prerequisites:** Tenants, Folders and Projects adoption using the kit from Story 1.13

**Acceptance Criteria:**

**Given** each of the Tenants, Parties, Folders and Projects workspaces
**When** its minimum environment runs through `hexalith-module debug`
**Then** a breakpoint and a source edit in the active checkout affect the running module
**And** only direct root-declared dependencies are initialized

**Given** EventStore, Memories and McpCli in the Platform workspace
**When** debugged
**Then** they show the same source-debugging outcome

**Given** any module that has not adopted
**When** this story is evaluated
**Then** SM-2 is recorded as open for that module, never as passed

### Story 3.12: Record SM-3 evidence across every MVP module

As a Platform maintainer,
I want SM-3 demonstrated for every MVP module, locally and in CI,
So that test execution and lifecycle are proven for the whole MVP set.

**Repo:** Hexalith.Platform · **Covers:** SM-3, FR-4, FR-5, SM-C4 · **Prerequisites:** module adoption; McpCli test-AppHost migration to the descriptor

**Acceptance Criteria:**

**Given** each MVP module
**When** its declared minimum environment and its required local and CI integration checks run from its workspace
**Then** they pass through runner-owned environments
**And** isolated tests run first without Platform

**Given** CI runs for module candidates
**When** recorded
**Then** each identifies the candidate revision and the expected and loaded artifacts
**And** substituted or uncommitted inputs are refused, and a baseline-only run is labelled

**Given** the lifecycle scenarios from Story 2.10
**When** they repeat after any lifecycle change
**Then** the results are recorded again

**Given** any module that has not adopted
**When** this story is evaluated
**Then** SM-3 is recorded as open for that module

## Epic 4: Publish retained releases and run the isolated staging environment

The publication workflow produces an attested, immutable release: application Helm package, composed image, McpCli candidate and release record. The staging executor deploys it by digest into an isolated staging environment on `hexalith.com`. There, team members use the reference composition's supported interfaces and McpCli with staging permissions, and staging cannot claim production names or authority. Stories 4.0–4.4 are urgent, date-bound or security-driven work, so sprint planning schedules them first. Stories 4.0, 4.2, 4.3 and 4.4 can execute independently; Story 4.1 preparation may run in parallel, but no cluster upgrade may begin until Story 4.0 is done.

### Story 4.0: Prove off-node backups and isolated restores

As Administrator,
I want restorable off-node backups for Keycloak PostgreSQL, shared OpenBao and `hexalith-memories`,
So that the disruptive cluster upgrade has independently proven recovery points instead of relying on node-local data or backup-job success.

**Repo:** Administrator operations · **Covers:** AR-59 (upgrade prerequisite), AR-63 · *Independent, pull forward; prerequisite for Story 4.1 upgrade execution*

**Acceptance Criteria:**

**Given** the existing Keycloak PostgreSQL cluster, shared OpenBao raft data and `hexalith-memories` Redis/FalkorDB data
**When** Story 4.0 completes
**Then** each system has an encrypted, immutable recovery point stored off the cluster node
**And** each recovery point has been restored into an isolated target and verified against its source inventory without replacing a live PVC

**Given** the three isolated restore rehearsals
**When** their evidence is assembled
**Then** the signed proofs name the recovery-point identifiers, off-node object identities and checksums, isolated targets, verification results, timestamps and accountable operator
**And** backup-job success without successful isolated restore verification is not accepted as proof

**Given** any missing, stale, unsigned or failed proof
**When** Story 4.1 reaches its mutation gate
**Then** the Kubernetes upgrade remains blocked

### Story 4.1: Upgrade the cluster off Kubernetes 1.34 after verified backups

As Administrator,
I want the designated cluster on a supported Kubernetes minor before staging exists,
So that the in-place upgrade, which takes every workload down, happens while nothing depends on it and before 1.34 reaches end of life on 2026-10-27.

**Repo:** Administrator operations · **Covers:** AR-59 (infrastructure currency: Kubernetes), AR-63 · *Independent, pull forward*

**Acceptance Criteria:**

**Given** Story 4.0's signed restore proofs for Keycloak PostgreSQL, shared OpenBao and `hexalith-memories`
**When** the upgrade mutation is requested
**Then** Story 4.0 is `done` and all three proofs pass freshness, signature, recovery-point and off-node-storage validation
**And** a missing, stale, unsigned or failed proof keeps the upgrade blocked

**Given** the single-node topology and external etcd endpoint
**When** the maintenance window is prepared
**Then** kubeadm and workload preflights, the sequential minor-version path, the current target patch, the outage approval and rollback/stop conditions are recorded
**And** preparation does not count as authorization to mutate the cluster

**Given** the single-node cluster on v1.34.9
**When** it is upgraded in place
**Then** it runs a supported minor at its current patch, and the actual versions are recorded as evidence

**Given** the workloads that existed before the upgrade (Keycloak, OpenBao, Memories, Forgejo)
**When** the upgrade completes
**Then** each is verified healthy, or restored from its backup, and the outcome is recorded

### Story 4.2: Close public admin exposure and anonymous registry reads

As Administrator,
I want the Keycloak admin and master-realm routes, the KubeSphere console and anonymous registry reads closed now,
So that live administrative exposure doesn't persist while staging is built beside it.

**Repo:** Administrator operations · **Covers:** AR-59 (exposure), AR-18 (registry controls), AR-25 (admin path), AR-63 · *Independent, pull forward*

**Acceptance Criteria:**

**Given** the public Keycloak admin and master-realm routes and the KubeSphere console at `kube.hexalith.com`
**When** this story completes
**Then** they are reachable only from the declared Administrator path
**And** an external negative probe proves they are unreachable from the internet

**Given** the Zot registry at `registry.hexalith.com`
**When** anonymous read is disabled
**Then** every existing consumer already holds a pull credential, and none of them breaks
**And** an anonymous pull is proven to be refused

**Given** registry garbage collection
**When** reconfigured
**Then** it can no longer delete a retained digest

### Story 4.3: Relocate the privileged Forgejo runner

As Administrator,
I want the privileged Forgejo runner moved off the cluster node,
So that no privileged CI workload shares a host with staging, production or any executor.

**Repo:** Administrator operations · **Covers:** AR-59 (Forgejo relocation), AR-28 · *Independent, pull forward*

**Acceptance Criteria:**

**Given** the `forgejo-runner` namespace at Pod Security `privileged`
**When** this story completes
**Then** the runner runs on a separate machine or VM that is shared with no executor, and the namespace is removed from the cluster

**Given** the relocated runner
**When** Forgejo jobs run
**Then** they succeed with no access to the designated cluster's API or namespaces

### Story 4.4: Apply publication and operations repository controls

As Administrator,
I want the Platform repository, the tags and the organization locked down, and the operations and notification repositories created with only the named writers,
So that what executors run and what publication signs cannot be changed casually.

**Repo:** Administrator operations (GitHub) · **Covers:** AR-18, AR-28 (triggers), AR-45 (notification repository), AR-55 · *Independent, pull forward*

**Acceptance Criteria:**

**Given** Platform `main`
**When** its ruleset is applied
**Then** it requires a pull request with code-owner review and forbids force-push and deletion, with Administrator-only bypass
**And** CODEOWNERS assigns `.github/workflows/**` and `.github/CODEOWNERS` to Administrator

**Given** release tags
**When** the tag ruleset is applied
**Then** update and deletion are forbidden, and only the publication workflow or Administrator may create tags
**And** the Builds ruleset bypass is Administrator-only

**Given** the Hexalith organization
**When** configured
**Then** its base repository permission is read or none, no longer write

**Given** the private operations repository and the private notification repository
**When** created
**Then** their only writers are Administrator and the second organization owner
**And** workflows run with read-only repository permissions, and no PAT, App or deploy key outside the named writers' devices can write

### Story 4.5: Qualify the Aspire-to-Helm export with the reference composition

As a Platform maintainer,
I want the Platform Aspire model exported to a qualified Helm chart for Parties, EventStore, Tenants and Memories,
So that hosted deployment has one definition, or we trigger the maintained-chart fallback early.

**Repo:** Hexalith.Platform · **Covers:** AR-3, AR-4, AR-56 (Aspire-to-Helm qualification)

**Acceptance Criteria:**

**Given** the reference composition
**When** exported
**Then** the chart has Dapr sidecar annotations and Pod-Security-compliant security contexts added by one shared helper, and HTTPRoutes whose parent reference names the environment's Gateway with its namespace
**And** the chart contains no Gateway, even though Aspire 13.5.4 `AddGateway` would materialize one

**Given** the rendered chart
**When** inspected
**Then** it contains no Secret object and no secret value, hosted data services appear only as non-secret endpoint parameters, and images are digest-pinned through values
**And** the normal, recovery and remove-workloads render modes are chart values

**Given** a server-side admission dry-run against Pod Security `restricted`
**When** run
**Then** it passes with HotReload off

**Given** the packaged chart
**When** verified before publication exists
**Then** Helm server-side-apply ownership works and two renders from the same declared inputs and mode produce identical manifests
**And** OCI-attestation and off-site-restoration checks are explicitly excluded until published and retained artifacts exist, so this qualification has no future-story dependency

**Given** export needs per-module Dapr hand-modelling, a custom compiler, recurring generated-file patches or a duplicate topology
**When** that is found
**Then** the AD-1 fallback to a small maintained Helm chart with parity checks is recorded, and it replaces the exporter for later stories

### Story 4.6: Encode release, attempt, qualification and Administrator records

As a Platform maintainer,
I want the record types encoded once, with signatures bound to their writers,
So that deployment accepts a record only from the writer entitled to issue it.

**Repo:** Hexalith.Builds · **Covers:** AR-20, AR-21 (record encoding)

**Acceptance Criteria:**

**Given** the release, attempt, qualification, production-promoted, release-available, latest-working-pointer, promotion-stop and Administrator record kinds
**When** encoded
**Then** each has a versioned schema and names its writer
**And** a record is accepted only when signed by that writer, never on the strength of store access

**Given** an attempt record
**When** it becomes terminal
**Then** it is immutable
**And** a superseded attempt's terminal record may be written only by the writer of the attempt that took it over

**Given** an Administrator record
**When** verified
**Then** its signature must come from an Administrator-held identity that no executor, workflow token, monitor or deputy holds

### Story 4.7: Provide the staging attempt, lock and promotion-stop store

As a Platform maintainer,
I want a compare-and-set store outside the cluster and the executor hosts for staging's lock, epoch, attempt records and stop,
So that staging attempts are serialized and their records survive the environment.

**Repo:** Hexalith.Platform with Hexalith.Builds · **Covers:** AR-20, AR-21 (store), AR-36

**Acceptance Criteria:**

**Given** the store
**When** deployed
**Then** it is hosted outside the target cluster and every executor host, and it supports compare-and-set
**And** it holds one lock per environment with a monotonic epoch

**Given** two concurrent staging attempts
**When** both try to take the lock
**Then** exactly one succeeds
**And** every mutation checks the current epoch before it commits

**Given** a staging promotion-stop record
**When** written
**Then** it is bound to staging only, and it never sets or clears production's stop

### Story 4.8: Maintain the module intake manifest

As a Platform maintainer,
I want a versioned intake manifest that pins each module release and computes its effective change classification,
So that every release knows exactly which module artifacts it contains and how breaking they are.

**Repo:** Hexalith.Platform · **Covers:** AR-19, AR-22 (attestation before publication) · **Prerequisites:** EventStore and Memories image attestation through Builds `domain-release` or an equivalent attested path

**Acceptance Criteria:**

**Given** a module release entering Platform
**When** it is added to the intake manifest
**Then** the manifest pins its package and image digests, its image-attestation identity and its release-evidence references

**Given** a module image pinned in the intake manifest
**When** it is admitted for publication
**Then** its attestation is verified and names the module repository, workflow, protected ref and pinned Builds workflow ref recorded in the manifest
**And** a missing or mismatched attestation refuses the module release before publication

**Given** a chain of module releases between production's working baseline and a candidate
**When** the classification is computed
**Then** the effective classification is the maximum over the chain (breaking, additive or none) under the single definition of breaking
**And** the manifest records the chain and the working baseline it was computed against

### Story 4.9: Ratify the hosted runtime and identity-event contracts

As a Platform maintainer,
I want the profile, hosted catalog, secret contract and identity-event capture bound created and ratified,
So that staging and production share one qualified runtime definition with bounded admission-event propagation.

**Repo:** Hexalith.Platform, with EventStore · **Covers:** AR-11, AR-29, AR-31, AR-56 (shared runtime and profile), AR-62 · **Prerequisites:** the EventStore profile template and per-release binding split (EventStore AD-26)

**Acceptance Criteria:**

**Given** the EventStore-ratified profile template
**When** the profile inventory is created
**Then** it pins the environment-layer and shared-infrastructure providers and topology, including the Dapr control plane with sidecar drop-all-capabilities, and separately pins the deploy toolchain (including the exact Helm version)
**And** the profile digest covers the template plus those pins, and nothing else

**Given** the hosted catalog
**When** created
**Then** `deploy/dapr/eventstore-routing-catalog.json` is committed and generated by the generator from Story 3.3

**Given** the secret contract
**When** created
**Then** `deploy/dapr/openbao-secret-contract.yaml` is value-free, has an environment dimension, and lists static entries and module-owned dynamic namespaces with their only writers

**Given** each time-bound certificate, credential, token and domain registration in the inventory
**When** recorded
**Then** it has a named renewal owner

**Given** hosted admission and identity events
**When** this story completes
**Then** a decision record fixes a finite maximum capture lag, its measurement point, its owner and the condition for revisiting it
**And** staging event export and later admission reconciliation use that bound rather than choosing their own

### Story 4.10: Define the recovery hook contract

As a Platform maintainer,
I want the recovery hook contract defined once and versioned,
So that module recovery has one contract before the first release binds its version.

**Repo:** Hexalith.Platform · **Covers:** AR-43

**Acceptance Criteria:**

**Given** the contract
**When** published
**Then** it specifies:
- cut identity, with the position each recovery class reaches (including the EventStore position and the Memories register sequence);
- recovery mode and quarantine admission;
- purge, re-provisioning and rotation hooks, fence hooks, rebuild, integrity and external-effect reconciliation;
- the reopen-before-replay declaration;
- an internal endpoint with an attempt- and epoch-bound credential;
- epoch-bound task commits;
- the result shape

**And** it carries a version that each release record binds

### Story 4.11: Define the check-suite contract and own the reference suites

As a module owner,
I want one contract for invoking E2E and smoke suites, with explicit Platform ownership of the reference-composition suites,
So that checks run identically on staging and production executors and no required reference flow is left ownerless.

**Repo:** Hexalith.Builds (contract), Hexalith.Platform (reference suites) · **Covers:** FR-6, FR-7, AR-21 (check-suite contract, before the first staging deployment)

**Acceptance Criteria:**

**Given** a check suite
**When** it is packaged
**Then** it has an immutable digest, stable check IDs, a declared surface for each check and a declared cadence for smoke checks

**Given** the invocation contract
**When** published
**Then** it specifies that a suite runs in the executor's module-code sandbox and receives only the declared verification endpoint and short-lived synthetic-client tokens
**And** the staging executor enforces this in Story 4.20

**Given** each check result
**When** reported
**Then** it records passed, failed, skipped, timed out or incomplete, together with the served release identity, the suite digest, the profile digest and the configuration digest
**And** a timeout counts as a failure

**Given** the reference composition and McpCli
**When** this story completes
**Then** Platform records the accountable owner and source location for each non-empty critical-flow suite and each production-safe smoke suite required for Parties, EventStore, Tenants, Memories and McpCli
**And** every mapped check is packaged through this contract and passes its schema validation before the first reference release is published
**And** an absent owner, empty suite or unresolved flow-to-check mapping blocks publication

### Story 4.12: Publish retained releases with provenance

As a Platform maintainer,
I want one publication workflow to build, attest and retain each release,
So that staging and production deploy exactly what was published, and nothing is ever regenerated.

**Repo:** Hexalith.Platform (publication workflow), Hexalith.Builds (reusable workflows) · **Covers:** AR-17, AR-18, AR-46 (McpCli candidate) · **Prerequisites:** EventStore AD-13 ratification and composed-subject issuer registration

**Acceptance Criteria:**

**Given** the public publication workflow on disposable hosted runners
**When** it runs from the intake manifest
**Then** it builds the application Helm package, the composed `platform/eventstore` image and the McpCli candidate
**And** it attests each one, verifies the module image attestations pinned in the manifest, and writes the signed release record binding every release-invariant value
**And** the full `uses:` closure is SHA- or digest-pinned and checked by the Builds governed closure

**Given** the release artifacts
**When** published
**Then** the package is identified by its OCI manifest digest and every image by its digest, and none can be overwritten

**Given** an attestation
**When** verified
**Then** the predicate names the signer workflow and digest, the source repository and protected ref, and a Build Config URI equal to the publication workflow path

### Story 4.13: Retain artifacts off the primary failure domain

As Administrator,
I want retained artifacts, records and evidence replicated off-site with create-only writers,
So that any rollback target or recovery point's release can be restored after losing the primary site.

**Repo:** Administrator operations, Hexalith.Platform · **Covers:** AR-17 (retention), AR-18 (registry, replica)

**Acceptance Criteria:**

**Given** `registry.hexalith.com`
**When** configured for retained artifacts
**Then** every writer to a retained repository can create but not update or delete
**And** each environment has its own pull credential
**And** `registry.tache.ai` is never used as a retained store

**Given** the off-site replica
**When** artifacts are published
**Then** they are replicated with their attestations by a separate writer

**Given** a retained Helm package, composed image, McpCli artifact, release record and attestation set in the off-site replica
**When** the primary artifact store is treated as unavailable
**Then** each artifact is retrieved by its recorded digest, every attestation verifies, and the retained chart can be applied without regeneration

**Given** the retention period
**When** pruning runs
**Then** no package, digest, record or evidence is removed while it is inside the backup retention period or still a rollback target, whichever lasts longer

### Story 4.14: Build staging namespaces, storage and pod and network isolation

As a Platform maintainer,
I want staging's application and data namespaces with enforced pod, network and storage isolation,
So that staging workloads are confined before any release runs there.

**Repo:** Hexalith.Platform (operations repository definitions) · **Covers:** FR-10, NFR-3, AR-23, AR-30, AR-32

**Acceptance Criteria:**

**Given** staging
**When** its environment layer is applied by the environment-layer identity
**Then** it has an application namespace and a separate data namespace holding staging's own data-service instances and persistent volumes

**Given** both namespaces
**When** inspected
**Then** Pod Security `restricted` is enforced, default-deny ingress and egress NetworkPolicies are applied, and a test proves the CNI enforces them
**And** staging runs under quotas and limits, with a lower PriorityClass than production will have

**Given** storage
**When** a claim is made
**Then** it binds only through staging's StorageClass
**And** quotas deny staging the production StorageClass
**And** volumes use node-level encryption, with keys under Administrator and deputy custody

**Given** data services
**When** connected
**Then** they use native TLS where the provider supports it, including CloudNativePG

**Given** the per-job application deploy identity
**When** inspected
**Then** it cannot write the data namespace or any environment-layer object

### Story 4.15: Provide staging secrets through a per-environment OpenBao and tenant-key store

As a Platform maintainer,
I want staging to have its own OpenBao and tenant-key store with per-app tokens,
So that staging secrets are separate, scoped and renewable.

**Repo:** Hexalith.Platform, Administrator operations · **Covers:** AR-29, AR-41 (key custody), AR-56 (secrets)

**Acceptance Criteria:**

**Given** staging's data namespace
**When** secrets are provisioned
**Then** staging has its own OpenBao instance and a separate tenant-key store that is excluded from ordinary OpenBao snapshots

**Given** each app
**When** its sidecar starts
**Then** it reads secrets through Dapr `secretstores.hashicorp.vault` with a per-app token mounted per pod
**And** a token rotation restarts the pod

**Given** the existing shared OpenBao and its `openbao-runtime-bootstrap` token
**When** classified
**Then** the shared token is split per app, and each token has a named renewal owner who renews it before its 2027-07-19 expiry

**Given** a required secret generation
**When** it is missing
**Then** readiness fails

### Story 4.16: Apply the staging Dapr trust domain and runtime policy

As a Platform maintainer,
I want staging's Dapr runtime locked to its own trust domain with deny-by-default policy,
So that no staging caller can reach apps outside its declared permissions.

**Repo:** Hexalith.Platform · **Covers:** AR-23 (Dapr), AR-32 (Dapr activation), AR-56 (drop-all-capabilities, Dapr activation qualification)

**Acceptance Criteria:**

**Given** staging
**When** Dapr is configured
**Then** it has its own trust domain, hosted Configurations deny by default, callers are allowed by trust domain, namespace and app ID, and HotReload is off
**And** every app that hosts workflows has at least one scoped WorkflowAccessPolicy

**Given** the sidecar injector
**When** configured
**Then** sidecars drop all capabilities

**Given** one policy-only update and one HTTPEndpoint-only update
**When** each is applied within an attempt
**Then** every consuming sidecar restarts before readiness or qualification passes

### Story 4.17: Select and qualify the durable production broker

As a Platform maintainer,
I want a durable broker selected and qualified for hosted environments,
So that event delivery survives failures, with retention, dead-lettering and backlog recovery.

**Repo:** Hexalith.Platform with EventStore · **Covers:** AR-56 (broker)

**Acceptance Criteria:**

**Given** the broker candidates
**When** one is selected
**Then** it is not Redis pub/sub (excluded by EventStore AD-26), and the choice is recorded in the profile inventory

**Given** the selected broker in staging
**When** qualified
**Then** at-least-once unordered delivery, message identity, deduplication, dead-letter capture, delivery retention and backlog recovery are each demonstrated

**Given** a declared topic
**When** composed in a hosted environment
**Then** Platform renders its Subscription with the bound component and dead-letter policy

### Story 4.18: Serve staging through its own Gateway, hostnames and certificates

As a team member,
I want staging reachable only at its own `hexalith.com` names, through its own Gateway and certificates,
So that staging can never claim production names or bypass ingress.

**Repo:** Hexalith.Platform, Administrator operations (DNS) · **Covers:** FR-10, AR-24, AR-56 (Gateway API, cert-manager)

**Acceptance Criteria:**

**Given** the cluster
**When** Gateway support is installed
**Then** Gateway API CRDs are installed at a version the Traefik pin supports, with the Traefik Gateway provider, and cert-manager has per-environment `gatewayHTTPRoute` issuers in place of the `nginx-public` solvers

**Given** staging's Gateway in its data namespace
**When** configured
**Then** its listeners carry only staging's exact FQDNs (no wildcards over a shared zone)
**And** the HTTPS listener admits routes only from staging's application namespace, and the port-80 listener also admits ACME solver routes

**Given** a staging listener, route, Certificate or CertificateRequest that names a reserved or out-of-pattern host, or references a ClusterIssuer
**When** submitted
**Then** the release validator and admission reject it
**And** staging holds no DNS-01 credential and uses only the staging HTTP-01 Issuer

**Given** the application deploy identity
**When** it tries to create an Ingress, `traefik.io` or `hub.traefik.io` object
**Then** it is denied
**And** nothing is rendered on the `nginx-public` class

**Given** user-ingress admission
**When** it changes
**Then** it changes only within an attempt, as environment-layer state on the Gateway
**And** executors verify through a declared internal endpoint

### Story 4.19: Generate the staging realm and register staging clients

As a team member,
I want a staging realm generated from the realm contract, with its own clients and admission rules,
So that staging identity is separate from production and consistent with local.

**Repo:** Hexalith.Platform · **Covers:** AR-25, AR-26, AR-27 · **Prerequisites:** EventStore admission predicate with the staging lifecycle clause

**Acceptance Criteria:**

**Given** the shared Keycloak server
**When** the staging realm is generated by the staging-only management client
**Then** it matches the realm-contract version bound in the release record, applied forward-only before the attempt that needs it

**Given** the staging realm
**When** inspected
**Then** it has distinct clients per surface mapped to one class, explicit token-exchange permissions, the admission groups, synthetic clients flagged with no admin or cross-tenant grant, and the staging-only tenant-lifecycle clause enabled by a staging realm-contract value

**Given** Keycloak administration
**When** exercised from staging
**Then** staging has no write access beyond its realm and no backup or restore access to the Keycloak server or database

**Given** admin and user events
**When** they occur
**Then** they are exported off-cluster within the declared bound

### Story 4.20: Run the staging executor and the module-code sandbox

As a Platform maintainer,
I want a dedicated staging executor that runs only allowlisted workflows with per-job credentials and sandboxes module code,
So that no repository event or module code can reach deployment authority.

**Repo:** Hexalith.Platform (operations repository), Administrator operations · **Covers:** AR-28, AR-56 (staging takeover fencing)

**Acceptance Criteria:**

**Given** the staging executor
**When** provisioned
**Then** it is a dedicated self-hosted Linux machine or VM, shared with no CI runner or other executor

**Given** a job
**When** it requests credentials
**Then** it gets, through GitHub OIDC claim checks, per-job credentials only for staging: the application deploy identity, the environment-layer identity and synthetic smoke clients
**And** every credential that can mutate a lock-covered authority is revocable at its issuer and issued only to the job holding the current epoch
**And** no deployment credential is a repository secret

**Given** a job whose repository, workflow ref, ref or event is not on the allowlist
**When** it arrives
**Then** the executor refuses it

**Given** module-supplied smoke, E2E or fence code
**When** it runs
**Then** it runs as a separate OS user or container with no access to job credentials, workspace, job token or container runtime, and receives only short-lived synthetic-client tokens and the declared verification endpoint

**Given** a takeover of a staging attempt
**When** a new epoch is reserved
**Then** the new owner fences each affected authority (an epoch check, or revocation of the prior job's credentials plus its declared drain) before any mutation

### Story 4.21: Send staging telemetry to a per-environment sink

As a Platform maintainer,
I want staging telemetry routed to its own sink, with exporter and egress rendered by Platform,
So that diagnostics are available per environment and production data is never mixed in.

**Repo:** Hexalith.Platform · **Covers:** AR-45 (telemetry sink), AR-56

**Acceptance Criteria:**

**Given** staging workloads
**When** they emit telemetry
**Then** it goes to a staging sink through exporter configuration and egress rendered by Platform, and modules never declare a sink

**Given** attempt-window diagnostics
**When** an attempt ends
**Then** they are bound to the release, environment, artifacts and effective configuration, classified and redacted, and kept after cleanup

### Story 4.22: Deploy the reference release to staging

As a team member,
I want the reference release deployed to staging by digest and reachable at `hexalith.com`,
So that I can use Parties, EventStore, Tenants and Memories in a hosted environment.

**Repo:** Hexalith.Platform (operations repository) · **Covers:** FR-10 (staging), AR-17, AR-32, AR-37

**Acceptance Criteria:**

**Given** a published release
**When** a staging deployment is requested
**Then** an environment-layer attempt first applies, forward-only, the objects rendered from the union of the baseline's and the candidate's release records
**And** a release attempt then takes the lock and a new epoch and runs `helm upgrade` on the retained package by digest, never with `aspire publish` or `aspire deploy`

**Given** the release attempt
**When** it runs
**Then** every required workload reaches the intended release and readiness within 10 minutes of t0, and the attempt record captures the environment-current values used

**Given** a team member with staging permissions
**When** they use the reference composition's declared public interfaces at staging FQDNs
**Then** the interfaces work
**And** internal-only and disabled interfaces are neither routed nor advertised

### Story 4.23: Restore staging data in place

As a Platform maintainer,
I want a staging in-place data restore that invokes the recovery hook contract,
So that staging can return to an earlier recovery point before any candidate evidence depends on it.

**Repo:** Hexalith.Platform (operations repository) · **Covers:** AR-42 (in-place forms), AR-56 (staging data restore)

*Ordering note:* AR-56 lists the staging data restore among the first-staging-deployment rows. A restore needs a deployed release to act on, so it is proven on the first staging deployment, which holds only synthetic data, and before Epic 5 accepts any candidate evidence.

**Acceptance Criteria:**

**Given** the reference release deployed to staging (Story 4.22)
**When** a staging recovery point is cut
**Then** it records the cut identity and the position each recovery class reaches, as the contract from Story 4.10 defines

**Given** a staging in-place data restore to that point
**When** started on the staging executor as its own attempt
**Then** it keeps staging's stop set, closes user ingress to executor and probe sources, removes the release's workloads while keeping data objects, and discards post-cut broker backlog, dead letters, consumer offsets and Scheduler jobs
**And** it then restores data and starts the release in recovery mode with the chart's recovery render

**Given** a module without hook implementations
**When** the restore runs
**Then** the restore reports that module as unqualified instead of claiming success

### Story 4.24: Use McpCli against staging

As a team member,
I want McpCli to work against staging with short-lived staging tokens,
So that I can run permitted module operations in staging from my own machine.

**Repo:** Hexalith.Platform, Hexalith.McpCli (owner review) · **Covers:** FR-12 (staging), AR-46

**Acceptance Criteria:**

**Given** a staging profile
**When** created
**Then** it binds the staging gateway, issuer and audience
**And** it uses short-lived per-environment OIDC tokens sent only to the staging gateway, with refresh material only in the OS credential store and no `offline_access`

**Given** a staging user
**When** they invoke agent-eligible operations through the CLI and MCP
**Then** the operations run only in staging with that user's staging permissions
**And** ineligible, disabled, mismatched and unauthorized operations are refused, as they are locally

**Given** the gateway
**When** it handles any call
**Then** it re-authorizes the call and never logs or persists bearer tokens

### Story 4.25: Prove staging-side isolation controls

As Administrator,
I want staging's own isolation controls proven before production exists,
So that the NFR-3 matrix starts from a verified staging boundary.

**Repo:** Hexalith.Platform · **Covers:** NFR-3 (staging side), AR-23, AR-24

**Acceptance Criteria:**

**Given** staging workloads, pods and automation identities
**When** they attempt to write outside their namespaces, bind another StorageClass, claim reserved or out-of-pattern hostnames, or create Ingress or Traefik objects
**Then** each attempt is denied and recorded

**Given** the staging management client and staging admins
**When** they attempt administration outside the staging realm
**Then** they are denied

**Given** CI credentials
**When** inspected
**Then** they include no staging or production provider credentials

**Given** these results
**When** recorded
**Then** they become the staging-side part of the NFR-3 matrix that Epic 7 completes against production

## Epic 5: Gate every release on exact-release staging evidence

Each candidate is proven in staging against that exact release. Every enrolled module's non-empty critical flows must pass. With no production baseline, staging proves installation and candidate behavior. With a production baseline and automatic-recovery eligibility, it upgrades from that baseline and rehearses the rollback set. A retained-data approved path without compatible evidence qualifies its named data recovery and does not prepare automatic rollback. Evidence is bound to the release, has a maximum age and never hides failures. Missing, stale or wrong evidence blocks promotion in both release modes. Unadopted incompatible candidates are reset before the next one. The no-baseline branch enables the first G1 deployment; baseline-dependent branches close after Epic 6 establishes the first working production baseline and before a second production candidate is accepted. After staging validation passes, only the Platform publication workflow publishes a stable McpCli version.

### Story 5.1: Decide the staging evidence policies

As a Platform maintainer,
I want the staging evidence policies decided and recorded,
So that the gate enforces one agreed rule set and nobody improvises it.

**Repo:** Hexalith.Platform with Hexalith.Builds and module owners · **Covers:** FR-6, AR-57

**Acceptance Criteria:**

**Given** the FR-6 evidence rules
**When** this story completes
**Then** a decision record fixes:
- the evidence maximum age;
- what triggers a staging attempt;
- how rerun evidence is accepted without hiding an earlier failed or incomplete attempt;
- the review policy for removing or remapping a critical flow (matching digests alone never show that reduced coverage is acceptable);
- the declared maximum duration of a staging reset

**And** each rule names its owner and the condition for revisiting it

### Story 5.2: Decide the image and dependency vulnerability policy

As a Platform maintainer,
I want a vulnerability severity that blocks promotion and a way to record exceptions,
So that known-vulnerable images never reach production unnoticed.

**Repo:** Hexalith.Builds with Hexalith.Platform · **Covers:** AR-22

**Acceptance Criteria:**

**Given** module and composed images
**When** this story completes
**Then** a decision record fixes the scanner, the blocking severity, and how an exception is approved, recorded and expired

### Story 5.3: Validate critical-flow and smoke declarations for every included module

As a Platform maintainer,
I want the declaration validator to enforce complete critical-flow and smoke declarations for every module in a release,
So that no module slips through the gate by declaring nothing.

**Repo:** Hexalith.Builds (validator), Hexalith.Platform · **Covers:** FR-6, FR-7, AR-8 (lifecycle validation) · **Prerequisites:** the owned reference suites from Story 4.11

**Acceptance Criteria:**

**Given** a release
**When** it is validated for staging
**Then** every included module, changed or unchanged, has a non-empty critical-flow declaration with a required E2E check for every flow
**And** infrastructure and tool modules, including McpCli's candidate flows, declare their essential behavior rather than being exempt

**Given** a missing or invalid declaration, an empty check set or an unresolved flow-to-test mapping
**When** validated
**Then** the release is refused, naming the module and flow

**Given** a critical flow that is removed or remapped
**When** validated
**Then** the change needs a recorded module-owner review under the policy from Story 5.1

**Given** smoke declarations
**When** validated
**Then** a missing, invalid or empty required smoke set blocks the release

**Given** a module's production smoke suite
**When** validated
**Then** its checks are read-only where possible, any necessary write uses only the standing synthetic check identities and synthetic data with no real-user change or external effect, and no check creates or deletes a tenant
**And** it is declared as a smoke suite in its own right, and a staging E2E suite is never accepted in its place

### Story 5.4: Review tenant-lifecycle flows and handle leftover synthetic tenants

As the Tenants owner,
I want tenant-lifecycle critical flows reviewed and leftover synthetic staging tenants cleaned up or reported,
So that staging E2E can create and delete tenants safely without polluting real-tenant views.

**Repo:** Hexalith.Tenants (owner review), Hexalith.Platform · **Covers:** FR-6, AR-27, AR-57

**Acceptance Criteria:**

**Given** a module-declared tenant-lifecycle critical flow
**When** reviewed by the Tenants owner with the owning module's owner
**Then** the review is recorded before the flow joins the release gate

**Given** a staging E2E run
**When** it creates or deletes tenants
**Then** it does so only inside such a flow, only with run-scoped tenants carrying the immutable synthetic exclusion marker, and only under the staging-only admission clause
**And** modules exclude only tenants whose attested creation names a synthetic client

**Given** synthetic tenants left over after a staging attempt
**When** the attempt ends
**Then** they are cleaned up, or listed in the attempt report with their creating run

### Story 5.5: Scan module and composed images against the vulnerability policy

As a Platform maintainer,
I want every module and composed image scanned before promotion,
So that only images below the accepted vulnerability threshold reach production.

**Repo:** Hexalith.Builds, Hexalith.Platform · **Covers:** AR-22

**Acceptance Criteria:**

**Given** module and composed images
**When** scanned
**Then** a finding at or above the blocking severity from Story 5.2 blocks promotion unless a recorded, unexpired exception covers it
**And** the scan result names every image digest and scanner-database version and is bound to the release evidence

### Story 5.6: Run exact-release staging E2E in a serialized staging attempt

As Administrator,
I want every candidate's required E2E checks run in staging against that exact release,
So that production only ever receives releases proven end to end.

**Repo:** Hexalith.Platform (operations repository) · **Covers:** FR-6, AR-34, SM-C1

**Acceptance Criteria:**

**Given** a candidate whose intake artifacts and declarations are validated
**When** a staging attempt starts
**Then** it takes the staging lock and epoch, cuts a staging recovery point before the candidate writes state, and deploys the exact candidate within one serialized attempt
**And** the attempt record names the preparation state used, without assuming that a production baseline exists

**Given** the deployed candidate
**When** E2E runs
**Then** every required check of every declared flow of every included module runs against the deployed real services
**And** the results record the serving release, the production working baseline they were computed against, and the suite, profile and configuration digests

**Given** any required check that failed, was skipped, is incomplete or has no completed passing result
**When** the evidence is recorded
**Then** the candidate is marked not promotable
**And** a staging failure blocks only that candidate

**Given** a rerun
**When** it passes
**Then** the record still shows every earlier failed or incomplete attempt, as the policy from Story 5.1 requires

### Story 5.7: Rehearse a fresh install when production has no baseline

As Administrator,
I want staging to prove the candidate installs and works from empty when production has no working baseline,
So that a first production installation rests on real evidence.

**Repo:** Hexalith.Platform (operations repository) · **Covers:** FR-6, FR-8 (first deployment), AR-34

**Acceptance Criteria:**

**Given** production has no working baseline
**When** a staging attempt runs
**Then** it rehearses a fresh install of the candidate, with no nonexistent baseline host and no rollback set, and runs every required E2E check

**Given** the fresh-install evidence
**When** recorded
**Then** it is labelled as fresh-install proof, which serves only a first installation with no retained application data

### Story 5.8: Rehearse the rollback set and record compatibility evidence

As Administrator,
I want staging to prove that production's baseline can read everything the candidate writes,
So that automatic rollback is only ever used when it is safe.

**Repo:** Hexalith.Platform (operations repository) · **Covers:** NFR-1, FR-6, AR-33, AR-34

**Acceptance Criteria:**

**Given** a candidate over production's working baseline
**When** the staging attempt rehearses the AD-15 rollback set with HotReload off
**Then** it runs at least one idempotent command that is new in the candidate, and verifies that the baseline reads the state and events the candidate wrote

**Given** the rehearsal result and the effective change classification from the intake manifest
**When** recorded
**Then** the compatibility evidence names the recorded production baseline and its prepared rollback set
**And** a failed rehearsal is recorded as breaking evidence, not as a staging failure

**Given** staging retirements of catalog entries, keys or secrets
**When** applied
**Then** they stay expand-only relative to production's working baseline

### Story 5.9: Qualify the named recovery for a retained-data approved attempt

As Administrator,
I want an incompatible candidate's named data recovery rehearsed in staging,
So that an approved retained-data production attempt has a proven recovery without pretending automatic rollback is safe.

**Repo:** Hexalith.Platform (operations repository) · **Covers:** FR-6, FR-8, NFR-1, AR-34

**Acceptance Criteria:**

**Given** production has a working baseline with retained application data and the candidate lacks valid compatibility evidence against it or is classified breaking
**When** the approved retained-data path is qualified
**Then** the qualification names the retained candidate and baseline artifacts, the recovery point, the exact data-restore procedure, the required authority and the expected lost-window treatment
**And** it states explicitly that no automatic rollback set is prepared

**Given** the named recovery rehearsal
**When** staging starts from the working baseline, upgrades to the exact candidate and writes representative candidate state
**Then** the recorded recovery procedure restores the declared data point and deploys the named retained application and compatible current environment configuration
**And** readiness, smoke, data-integrity and applicable access checks pass against the recovered state

**Given** a missing plan, unavailable retained artifact or recovery point, failed restore, or failed verification
**When** the qualification is evaluated
**Then** the retained-data approved path is not qualified and the reason is recorded

**Given** production has no working baseline or retained application data
**When** staging selects its preparation branch
**Then** it uses the fresh-install proof from Story 5.7 instead of this path

### Story 5.10: Reset staging for unadopted incompatible candidates

As a Platform maintainer,
I want staging returned to a baseline-readable state before a new candidate when an unadopted candidate wrote incompatible data,
So that each candidate's evidence starts from a clean, representative staging.

**Repo:** Hexalith.Platform (operations repository) · **Covers:** FR-6, AR-34 (staging reset), AR-42 (in-place forms) · **Prerequisites:** module recovery-hook implementations for the reference composition

**Acceptance Criteria:**

**Given** a later candidate's staging attempt starts
**When** an earlier candidate has not been adopted and no Administrator record reserves it
**Then** the earlier candidate is no longer adoptable

**Given** that unadopted candidate wrote state that production's working baseline cannot read
**When** the later attempt is requested
**Then** staging first runs an in-place data restore, as its own attempt, to the recovery point cut at the start of the unadopted candidate's attempt
**And** the staging realm is regenerated from the realm contract, and the later candidate's environment-layer attempt removes objects declared only by the reset candidate

**Given** an additive unadopted candidate
**When** a later attempt is requested
**Then** no reset runs

**Given** recovery-point pruning
**When** it runs
**Then** it keeps any point that a pending staging reset references

### Story 5.11: Evaluate staging evidence for promotion

As Administrator,
I want one evaluator that decides, from recorded evidence, whether a release may be promoted,
So that the production workflow never promotes on missing, stale or mismatched evidence.

**Repo:** Hexalith.Platform · **Covers:** FR-6, SM-C1, AR-38 (precondition 4)

**Acceptance Criteria:**

**Given** a release and its staging evidence
**When** evaluated
**Then** the evaluator passes only if every required check passed for that exact release, with matching suite, profile and configuration digests, and the evidence is within its maximum age

**Given** evidence that is stale, belongs to another release, or has mismatched digests
**When** evaluated
**Then** it is refused, and the reason is recorded

**Given** the release modes
**When** evaluated
**Then** both require the same evidence
**And** a first installation requires the fresh-install proof from Story 5.7
**And** the automatic path requires valid compatibility evidence and a rehearsed rollback set from Story 5.8 against the current baseline
**And** an approved retained-data attempt without compatibility evidence requires the named recovery qualification from Story 5.9

**Given** a release that contains a McpCli candidate
**When** all applicable staging evidence passes
**Then** only the Platform publication workflow publishes the attested candidate as the stable McpCli version
**And** failed, stale or mismatched evidence publishes no stable version

## Epic 6: Deploy to production with verification and one-shot recovery (G1)

Administrator deploys a staged release to `tache.ai` through the Administrator-approved path, with user ingress closed. Platform verifies it against the readiness deadline, the five-minute window and the smoke triggers. On an eligible compatible failure it recovers exactly once automatically without rewinding data. G1 supports an in-place baseline redeploy but does not claim recovery-point data restore or named retained-data recovery. It keeps a durable per-cause promotion stop that only Administrator clears and reports every outcome through GitHub to Administrator and the deputy. G1 is reached with the off-site monitor, exposure controls, current infrastructure, the production realm and admission records in place. Shared-infrastructure changes gain a guarded, both-lock procedure here, but successful execution and forward-revert qualification wait until complete recovery points exist.

### Story 6.1: Decide how rehearsal faults are injected

As Administrator,
I want an architecture decision on how SM-5 fault rehearsals make a staged release fail production verification,
So that rehearsals prove the real mechanism without changing the release that staging approved.

**Repo:** Hexalith.Platform (architecture workflow) · **Covers:** FR-7, SM-5, AR-37 · *Architecture spike*

**Acceptance Criteria:**

**Given** that none of the PRD, spine, spec or addendum defines rehearsal fault injection
**When** the spike completes
**Then** an AD amendment is recorded through the architecture workflow, naming an injection point for each FR-7 trigger: rollout not ready, 60 s unavailability, two consecutive smoke failures, and a missing result
**And** no injection changes any release-invariant value

**Given** the amendment
**When** reviewed
**Then** it states how an injected fault is classified within the locked attempt, and which attempt-record fields mark an attempt as a rehearsal

**Given** rehearsal notifications
**When** specified
**Then** GitHub issues for rehearsals are labelled as rehearsals, so the deputy never receives a real alert for a planned fault

**Given** injection authority
**When** specified
**Then** injection is possible only inside an Administrator-approved rehearsal attempt, holds no standing credential, and is recorded in the attempt record

### Story 6.2: Decide the monitor and admission-check bounds

As Administrator,
I want the availability-probe stop bound and admission-check cadence decided,
So that monitoring sets the promotion stop by agreed rules, starting at G1.

**Repo:** Hexalith.Platform with Administrator · **Covers:** FR-8, FR-9, AR-59 (monitor and admission bounds)

**Acceptance Criteria:**

**Given** the FR-7 thresholds
**When** this story completes
**Then** a decision record fixes the availability-probe failure bound that sets the stop, consistent with those thresholds, including how probe failures during a locked attempt count

**Given** production admission
**When** decided
**Then** the record fixes the cadence of the two-way comparison between live admission and signed admission records
**And** the comparison consumes the identity-event capture-lag bound ratified in Story 4.9

### Story 6.3: Name the recovery deputy

As Administrator,
I want a named recovery deputy with their own identity, phishing-resistant MFA and minimum permissions,
So that every notification from G1 onward reaches a second responder.

**Repo:** Administrator operations · **Covers:** FR-8, AR-25 (MFA), AR-45 · *Independent, pull forward*

**Acceptance Criteria:**

**Given** the deputy
**When** named
**Then** their identity, phishing-resistant MFA and minimum permissions are recorded
**And** they have no write access to the operations repository, and no right to approve releases, clear the stop or administer production admission

**Given** the notification repository
**When** a test issue is raised
**Then** it is assigned to Administrator and mentions the deputy, and both confirm receipt

### Story 6.4: Stand up the off-site monitor, GitHub notification delivery and the dead-man check

As Administrator,
I want an off-site monitor that raises GitHub issues and a dead-man check that watches it,
So that failures are reported even when the primary site is down, and a silent monitor is noticed.

**Repo:** Administrator operations, Hexalith.Platform · **Covers:** FR-9 (monitoring), AR-45 · *Independent, pull forward*

**Acceptance Criteria:**

**Given** the off-site monitor host
**When** running
**Then** it is outside the primary failure domain and raises issues in the private notification repository through an issues-only credential, assigned to Administrator and mentioning the deputy
**And** each issue carries environment, release identity, outcome status and opaque references to access-controlled evidence

**Given** the hourly dead-man workflow, scheduled on an off-hour minute
**When** the monitor's heartbeat goes stale
**Then** it alerts
**And** the monitor alerts when the dead-man run is stale beyond two scheduled intervals

**Given** actual delivery
**When** tested end to end
**Then** Administrator and the deputy both receive the test issue, and the delivery is recorded as G1 evidence

### Story 6.5: Complete G1 exposure, DNS and certificate controls

As Administrator,
I want production's exposure path, DNS, certificates and verification endpoint in place,
So that production can be deployed with user ingress closed and verified privately.

**Repo:** Administrator operations · **Covers:** AR-24, AR-59 (exposure, DNS and certificates)

**Acceptance Criteria:**

**Given** production
**When** exposure is configured
**Then** the public exposure path and the ingress-closure mechanism are defined, so production hostnames can admit only executor and probe sources

**Given** each DNS zone
**When** configured
**Then** it has one named owner
**And** production ACME credentials exist only for production, and the production realm issuer uses a production-controlled name

**Given** executors
**When** verifying
**Then** they use a declared internal verification endpoint with the same authentication as public access

**Given** the retained registry
**When** reviewed
**Then** its failure domain and its per-environment pull-credential mechanism are recorded

### Story 6.6: Bring shared infrastructure current for G1

As Administrator,
I want every shared component on a supported, security-current version recorded in the profile inventory,
So that production starts on infrastructure that is not already behind.

**Repo:** Administrator operations · **Covers:** AR-31, AR-59 (infrastructure currency), AR-63

**Acceptance Criteria:**

**Given** the inventory
**When** this story completes
**Then** these are at supported, security-current versions:
- OpenBao, patched;
- Redis 8, replacing Redis Stack 7.4 through the Memories digest set;
- Keycloak, on a supported minor;
- CloudNativePG and both PostgreSQL instances;
- FalkorDB;
- the Dapr control-plane patch;
- Traefik, Calico, cert-manager and the Gateway API CRDs;
- the storage provisioner;
- Zot and Velero

**And** the actual versions are recorded as evidence

**Given** KubeSphere
**When** reviewed
**Then** it is either current or removed

**Given** every expiring certificate and credential
**When** inventoried
**Then** it has a renewal owner and a monitor lead time

### Story 6.7: Build the production environment layer

As a Platform maintainer,
I want production's namespaces, storage, secrets, Dapr runtime and Gateway built from the same definitions as staging,
So that production is isolated by the same verified controls.

**Repo:** Hexalith.Platform (operations repository) · **Covers:** FR-10 (production), NFR-3, AR-23, AR-24, AR-29, AR-30, AR-32

**Acceptance Criteria:**

**Given** the staging environment-layer definitions from Stories 4.14–4.18
**When** applied to production by the production environment-layer identity
**Then** production gets its own application and data namespaces, data-service instances, OpenBao and tenant-key store, volumes, StorageClass, Dapr trust domain, Gateway and listener Certificates at `tache.ai` FQDNs

**Given** production workloads
**When** scheduled
**Then** they have a higher PriorityClass than staging

**Given** staging resources
**When** staging exhausts its quota
**Then** production capacity is protected by resource limits

**Given** user ingress
**When** the environment layer is applied
**Then** it admits only executor and probe sources

### Story 6.8: Run the production executor outside the application cluster

As Administrator,
I want a production-only executor outside the cluster with per-job credentials and an operator recovery entry point,
So that production changes run only from allowlisted workflows, or from Administrator or deputy recovery.

**Repo:** Hexalith.Platform (operations repository), Administrator operations · **Covers:** AR-28, AR-58 (executor updates and monitoring)

**Acceptance Criteria:**

**Given** the production executor
**When** provisioned
**Then** it runs outside the application cluster on a dedicated host, runs only production jobs, and shares its host with no CI runner or other executor

**Given** a production job
**When** it obtains credentials
**Then** they are per-job and production-only (the application deploy identity, the environment-layer identity, synthetic smoke clients), revocable at their issuer, and issued only to the holder of the current epoch
**And** a per-job cluster-scoped identity is issued only to named shared-infrastructure workflows

**Given** an in-place recovery started by Administrator or the deputy
**When** they use the allowlisted recovery entry point
**Then** it runs under their own MFA identity, without write access to the operations repository

**Given** an Administrator record
**When** a job depends on it
**Then** the executor verifies its signature before acting

**Given** the executor's runner software
**When** a new version is released
**Then** updates are applied and versions are monitored

### Story 6.9: Provide the production attempt, lock and stop store

As Administrator,
I want production's lock, epoch, attempt records and stop in a compare-and-set store that the production executor, the recovery executor and the monitor can all reach,
So that production control survives loss of the cluster or the executor.

**Repo:** Hexalith.Platform with Hexalith.Builds · **Covers:** AR-20, AR-36, AR-58

**Acceptance Criteria:**

**Given** the production store
**When** deployed
**Then** it is outside the cluster and every executor host, and the production executor, the recovery executor and the off-site monitor can each reach it

**Given** production records
**When** written
**Then** each is accepted only with its writer's signature, and terminal attempt records are immutable

**Given** the latest-working pointer
**When** moved
**Then** only the writer of a working terminal attempt with a higher epoch may move it

### Story 6.10: Keep a durable per-cause promotion stop

As Administrator,
I want a durable promotion stop that records each cause and that only I can clear,
So that promotions never resume while production is unsafe.

**Repo:** Hexalith.Platform · **Covers:** FR-8 (promotion stop), FR-9 (availability probe), AR-39, AR-45 (probe)

**Acceptance Criteria:**

**Given** production exists
**When** the off-site monitor from Story 6.4 probes its availability, at least every five minutes, through the declared internal endpoint
**Then** each result is recorded, and failures beyond the bound from Story 6.2 set the stop as a probe cause

**Given** production
**When** any of these occurs:
- a non-working terminal outcome, or any recovery entry;
- a failed pre-update health check;
- a probe failure beyond the bound from Story 6.2;
- a recorded incident establishing production is not working;
- an Administrator or deputy declaration

**Then** the stop is set with a new cause, and its monotonic revision advances
**And** it survives process, executor and cluster restart
**And** promotion is also suspended during any recovery

**Given** a condition that continues
**When** it is still present
**Then** it is recorded once until it resolves

**Given** the monitor
**When** it sets the stop
**Then** it can never clear it, and setting the stop never starts a search through older releases

**Given** a clear
**When** requested
**Then** it is accepted only as a signed Administrator record naming the reason, the observed revision, a verified current working attempt and each cause as resolved or accepted
**And** the record shows that Administrator reviewed the lost-window exceptions before clearing
**And** it is applied by compare-and-set only if the revision is unchanged, so a later cause always prevails

**Given** a cause marked accepted
**When** it persists after the clear
**Then** it is not re-recorded until it resolves and recurs
**And** any other condition still present after the clear is recorded as a new cause

**Given** the deputy
**When** they try to clear the stop
**Then** they are refused

### Story 6.11: Generate the production realm and record admission grants

As Administrator,
I want the production realm generated from the realm contract, and every admission grant or revocation recorded as a signed, chained record before it is applied,
So that admission records, not a realm snapshot, are the authority for who may enter production.

**Repo:** Hexalith.Platform, Administrator operations · **Covers:** FR-11, AR-25, AR-27 · **Prerequisites:** the Tenants operator-only creation task for the production synthetic tenant

**Acceptance Criteria:**

**Given** the realm contract
**When** Administrator applies it to production
**Then** the production realm exists with the human production-admission group empty, a standing synthetic-admission group limited to the synthetic tenant, and distinct clients per surface
**And** the staging lifecycle clause is rejected by the validator

**Given** a grant or revocation of human or synthetic admission
**When** made
**Then** it is first written as a signed Administrator record that is create-only and names its predecessor's digest, then applied in Keycloak
**And** it is retained off the primary failure domain while its grant is current, and until every recovery point cut before its revocation has expired

**Given** a gap in the record chain
**When** reconciliation runs
**Then** it stops for intervention

**Given** the production synthetic tenant and credentials
**When** provisioned
**Then** Administrator provisions them through the operator-only task, and only the production executor holds the synthetic credentials

### Story 6.12: Detect admission drift in both directions

As Administrator,
I want the monitor to compare live production admission with the signed records in both directions,
So that an unrecorded grant or an unapplied revocation stops promotion immediately.

**Repo:** Hexalith.Platform · **Covers:** FR-8, FR-11, AR-39, AR-45, AR-59 (admission bounds)

**Acceptance Criteria:**

**Given** the cadence from Story 6.2
**When** the off-site monitor runs
**Then** it compares live production admission with the signed records, detecting both an unapplied record and an unrecorded membership change
**And** it matches every exported admission-group change to a signed record

**Given** a mismatch
**When** detected
**Then** Administrator and the deputy are notified and the promotion stop is set

**Given** a mismatch that is still present
**When** Administrator tries to clear the stop or approve an attempt
**Then** the action is refused until admission matches the records

### Story 6.13: Report every attempt outcome through GitHub

As Administrator or the recovery deputy,
I want every deployment failure and recovery result reported to both of us,
So that no outcome, including a successful recovery, goes unnoticed.

**Repo:** Hexalith.Platform · **Covers:** FR-8 (reporting), AR-45

**Acceptance Criteria:**

**Given** any deployment failure or recovery result, including success
**When** an attempt reaches its outcome
**Then** a GitHub issue identifies the release, environment, status and opaque references to access-controlled diagnostics, assigned to Administrator and mentioning the deputy

**Given** attempt-window diagnostics
**When** the attempt ends
**Then** they ship off the primary failure domain with a declared minimum retention
**And** deployment logs and artifacts stay private

### Story 6.14: Refuse production attempts that fail a precondition

As Administrator,
I want production attempts to mutate nothing until every precondition holds,
So that a missing condition stops the attempt safely and tells me why.

**Repo:** Hexalith.Platform · **Covers:** FR-7 (pre-update checks), AR-38, SM-C1

**Acceptance Criteria:**

**Given** a production release attempt
**When** it starts
**Then** it checks, in order, before any mutation:
1. the lock and a new epoch are held, and the stop is clear;
2. the release-mode trigger is met;
3. provenance matches;
4. exact-release staging evidence is within its maximum age (the evaluator from Story 5.11);
5. the EventStore server package and the composed image are release-available;
6. profile digests are equal, versions fall within the qualified sets, the realm-contract version is present, and the environment layer is applied;
7. every included module is enrolled for production, with valid, non-empty readiness and smoke declarations;
8. the working baseline is ready and its smoke suite passes now;
9. retained-data compatibility evidence exists; an approved retained-data attempt without it is available only when a named data-restore recovery has been separately qualified, which is not a G1 capability

**Given** any precondition fails
**When** it is detected
**Then** the attempt stops without mutation and notifies, naming the precondition

**Given** the health check in precondition 8 fails
**When** detected
**Then** the stop is also set and production is recorded as degraded

### Story 6.15: Bound every attempt's timing and fence takeovers

As Administrator,
I want every attempt to keep its deadlines through interruptions and every takeover to fence the previous owner,
So that no restart grants extra time or a second recovery, and no stale job can mutate production.

**Repo:** Hexalith.Platform with Hexalith.Builds · **Covers:** FR-7 (interruptions), AR-36, AR-58

**Acceptance Criteria:**

**Given** an attempt
**When** it acquires the lock
**Then** it records its kind (release, infrastructure or recovery), a grace no longer than the check timeout plus 30 seconds, and its deadlines under its kind's rules

**Given** a replacement job resuming the same attempt
**When** it takes over
**Then** it inherits the kind, timers, grace and consumed recovery allowance, and records the new epoch
**And** timers never restart, and an observation gap invalidates the verification window

**Given** any takeover
**When** a new epoch is reserved
**Then** mutation waits until every affected authority is fenced by an epoch or generation check, or the prior job's credentials are revoked at their issuer and its declared maximum in-flight duration has passed
**And** otherwise the attempt stops for intervention

**Given** a record/cluster disagreement, an unknown actual release or lock owner, or an unreadable record
**When** detected
**Then** changes stop for intervention without mutation

**Given** a record that stays non-terminal past its maximum lifetime
**When** the monitor checks
**Then** it notifies

### Story 6.16: Roll out and verify a production release

As Administrator,
I want production rollouts verified by readiness and module smoke checks over a five-minute window,
So that a release is declared working only when it actually works.

**Repo:** Hexalith.Platform · **Covers:** FR-7, AR-37

**Acceptance Criteria:**

**Given** a release attempt past its preconditions
**When** it rolls out
**Then** it writes the production-promoted record bound to the active profile digest before the Helm upgrade by digest, and records t0 at the first mutation

**Given** the rollout
**When** required workloads have not reached the intended release and readiness by t0 + 10 minutes
**Then** the deployment fails

**Given** readiness is reached
**When** the five-minute verification window runs
**Then** availability is sampled at least every 10 seconds
**And** smoke checks from the immutable suite digests bound in the release record run in the sandbox at window start and then at their declared cadence

**Given** any of these during the window:
- 60 continuous seconds of unavailability of a required service;
- the same smoke check failing twice consecutively, with the second attempt 30 seconds after the first failure;
- a required check with no completed passing result at the deadline

**When** it occurs
**Then** the deployment fails
**And** a single failed probe or restart alone does not fail it, and results from another release count as missing

**Given** window end
**When** readiness and the latest required smoke results hold and no trigger fired
**Then** the attempt is recorded working, the candidate generation is committed, and the latest-working pointer moves

**Given** a failure found after the verification window ends
**When** recorded
**Then** it is an operational incident, not a failed verification, and it starts no automatic recovery
**And** an incident showing production is not working sets the stop, as Story 6.10 requires

### Story 6.17: Recover once automatically from a failed compatible deployment

As Administrator,
I want exactly one automatic recovery to the previous working release when a compatible deployment fails,
So that production returns to working behavior without rewinding data or looping through old releases.

**Repo:** Hexalith.Platform · **Covers:** FR-8 (automatic recovery), NFR-1, AR-33, AR-37

**Acceptance Criteria:**

**Given** an attempt eligible for automatic recovery
**When** it prepares
**Then** before rollout it records the rollback set (the baseline package with environment-current values plus the rollback generation) and prepares and ready-validates it on the running baseline hosts

**Given** keys, secrets, catalog entries and routing that the rollback set needs
**When** the candidate is not yet working
**Then** none of them is retired, and any contraction waits for a later attempt (AD-15 expand-only)

**Given** a failed compatible deployment over a working baseline
**When** recovery runs
**Then** Platform renders the rollback set with a Helm upgrade, never `helm rollback`, `--rollback-on-failure` or controller-driven rollback, and restores partially changed workloads and routing while keeping data and current security authority
**And** it commits the rollback generation at readiness

**Given** the recovery
**When** verified
**Then** readiness must return within 10 minutes, followed by the same five-minute verification using the restored release's recorded smoke suite

**Given** no workload changed and no generation was committed
**When** the attempt fails
**Then** Platform retains the existing release and reports the failed attempt

**Given** a failed first enrollment of a module
**When** reversed
**Then** its workloads are removed, never its data objects

**Given** the recovery fails or cannot be verified
**When** that happens
**Then** it is reported as failed or unverified for intervention, the stop is set, and no older release is tried
**And** the production-promoted record is invalidated

### Story 6.18: Run Administrator-approved attempts into empty or degraded production

As Administrator,
I want to approve one identified attempt into empty or degraded production, including the first deployment,
So that production can be installed or repaired without weakening any other gate.

**Repo:** Hexalith.Platform · **Covers:** FR-8 (approved attempts, first deployment), AR-35

**Acceptance Criteria:**

**Given** production that is empty (no working baseline) or degraded (last outcome non-working, or a recorded incident, over-bound probe failure or failed pre-update health check)
**When** Administrator records an approval
**Then** it names one attempt, its reason, the observed stop revision and a disposition for each cause, after the lost-window and admission-match reviews
**And** it lifts only that stop and precondition 8, for that attempt only

**Given** production that is neither empty nor degraded, including the reduced-recovery state
**When** approval is requested
**Then** it is refused
**And** a deputy's approval is always refused

**Given** a stop recorded after the approval
**When** the attempt has not started
**Then** the attempt does not start
**And** if the stop is recorded during the attempt, the attempt may finish, but its success does not clear that stop

**Given** retained application data
**When** evaluated
**Then** G1 requires valid compatibility evidence against the recorded baseline
**And** a named data-restore recovery cannot authorize the attempt until that recovery capability is separately qualified after G1
**And** only a first installation with no retained data uses the fresh-install proof from Story 5.7

**Given** the approved attempt
**When** verified working
**Then** it becomes the working baseline and clears the observed stop by compare-and-set

**Given** the approved attempt
**When** it fails
**Then** user ingress closes, the candidate's workloads are removed while data objects are kept, a new stop is set, and no baseline is redeployed automatically
**And** a failed first deployment is reported without claiming a rollback

**Given** any other manual change to production workloads, routing or release configuration
**When** requested
**Then** it runs only as an Administrator-approved attempt under the lock, and is refused otherwise

### Story 6.19: Redeploy the working baseline in place as Administrator or the deputy

As Administrator or the recovery deputy,
I want to redeploy the recorded working baseline in place,
So that I can restore service without a release when automatic recovery fails.

**Repo:** Hexalith.Platform (operations repository) · **Covers:** FR-8 (manual baseline redeploy), AR-35

**Acceptance Criteria:**

**Given** a failed, unverified or interrupted automatic recovery or baseline redeploy
**When** Administrator or the deputy starts a baseline redeploy on the production executor
**Then** it runs under a new epoch with the stop kept set, rendering the prepared rollback set, or, when none was prepared, the recorded working baseline with environment-current values

**Given** the baseline redeploy
**When** verified
**Then** it uses the same verification as automatic recovery, and it becomes the working baseline only after that verification passes

**Given** a recovery-kind attempt
**When** its maximum lifetime passes
**Then** Administrator and the deputy are notified, and the recovery is not aborted

### Story 6.20: Enforce the G1 recovery boundary

As Administrator,
I want G1 to refuse any release or recovery that requires an unqualified data restore,
So that baseline redeploy is never mistaken for recovery-point restoration.

**Repo:** Hexalith.Platform · **Covers:** FR-8, NFR-1, AR-35

**Acceptance Criteria:**

**Given** a candidate with retained application data and missing, stale, wrong-baseline, failing or breaking compatibility evidence
**When** a production attempt is requested at G1
**Then** it is refused even with Administrator approval, and the refusal states that a qualified named data-restore recovery is unavailable

**Given** a request to restore a production recovery point at G1
**When** the production executor evaluates it
**Then** it is refused without mutating workloads, data, routing or the working pointer
**And** the refusal does not affect the separately supported baseline-redeploy path

**Given** a failed compatible deployment or baseline redeploy
**When** recovery is selected
**Then** only the data-preserving rollback-set or baseline-redeploy mechanisms are offered
**And** neither mechanism rewinds application data or current security authority

### Story 6.21: Build the shared rehearsal harness

As Administrator,
I want one harness that injects rehearsal faults as the Story 6.1 decision specifies,
So that every SM-5 rehearsal and DR drill uses the same trusted mechanism.

**Repo:** Hexalith.Platform · **Covers:** SM-5, FR-7, FR-8

**Acceptance Criteria:**

**Given** the AD amendment from Story 6.1
**When** the harness is built
**Then** it can inject each FR-7 trigger at its named injection point inside an Administrator-approved rehearsal attempt, and never outside one

**Given** a rehearsal attempt
**When** it runs
**Then** the attempt record marks it as a rehearsal, notifications are labelled as rehearsals, and the injection holds no standing credential

**Given** harness use
**When** audited
**Then** every injection appears in its attempt record, with the trigger and its time

### Story 6.22: Guard shared-infrastructure changes with one controlled procedure

As Administrator,
I want every shared-infrastructure change run as one controlled change covering both environments,
So that shared changes never silently break either environment's isolation or working release.

**Repo:** Hexalith.Platform (operations repository), Administrator operations · **Covers:** FR-10 (shared-infrastructure changes), AR-32

**Acceptance Criteria:**

**Given** a shared-infrastructure change
**When** the procedure prepares it
**Then** it classifies the bounded change window as identity and secrets, shared data services, control-plane and networking, or artifact, backup and administration tooling
**And** it requires both environment locks, one named change owner, a complete recovery point, and one attempt record per environment on that environment's executor

**Given** no complete recovery point is available
**When** a shared-infrastructure mutation is requested
**Then** the procedure refuses it before mutation and records the missing prerequisite

**Given** a dry-run of the guarded procedure at G1
**When** its control flow is verified
**Then** it proves both-lock acquisition, attempt recording, smoke and NFR-3 test selection, failure classification, stop setting and the forward-revert entry point without claiming successful infrastructure mutation or forward-revert qualification

**Given** a change after G1
**When** requested
**Then** it is refused unless it has a complete recovery point and has first been rehearsed on a production-profile copy on prepared capacity
**And** the only exception is an urgent security patch applied in place with an Administrator record

### Story 6.23: Deploy the reference release to production with ingress closed and reach G1

As Administrator,
I want the reference release deployed to production as an approved first empty installation, with user ingress closed,
So that production exists, is monitored and is verified before anyone is admitted.

**Repo:** Hexalith.Platform, Administrator operations · **Covers:** FR-10 (production), FR-7, FR-8, AR-39 (G1)

**Acceptance Criteria:**

**Given** a release with fresh-install staging proof
**When** Administrator approves the first empty installation
**Then** the attempt deploys it into production with ingress admitting only executor and probe sources, the human production-admission group empty, and smoke checks run by the standing synthetic check identities
**And** a failure removes candidate workloads, keeps any created data, keeps ingress closed and is reported without claiming a rollback

**Given** the verified deployment
**When** G1 is evaluated
**Then** each of these is recorded:
- a supported Kubernetes minor;
- working GitHub delivery;
- the off-site probe running at least every five minutes;
- the G1 exposure, currency, Forgejo and bound decisions in place

**Given** the G1 NFR-3 checks
**When** run
**Then** staging pods cannot reach production services, sidecars or data, and a staging release that declares a production hostname is rejected

### Story 6.24: Run the pre-G2 fault rehearsals in approved mode

As Administrator,
I want the deployment and recovery failure paths rehearsed on production before G2,
So that production's safety mechanisms are proven while no user can be affected.

**Repo:** Hexalith.Platform · **Covers:** SM-5 (early rehearsals), FR-7, FR-8, SM-C3

**Acceptance Criteria:**

**Given** a staged release and the harness
**When** approved-mode rehearsals run in production before G2
**Then** each FR-7 trigger fires and fails its deployment
**And** one automatic recovery completes within its budgets, with data preserved

**Given** interrupted attempts
**When** rehearsed
**Then** deadlines do not reset, no recovery attempt is added, and a replacement job resumes only within its grace

**Given** a routing-only change and a failed or unverified recovery
**When** rehearsed
**Then** each follows its defined path, and the stop and notifications are recorded

**Given** these rehearsals
**When** recorded
**Then** they count as early SM-5 evidence, and none is reported as a successful recovery without its readiness, smoke and data checks

## Epic 7: Control production access explicitly

Only users that Administrator explicitly admits reach production, with exactly their production permissions, through the API, CLI and MCP. Staging users, workloads, automation and pods are refused at every boundary. The admission-record chain and drift detection built in Epic 6 are proven here for human admission. The temporary synthetic grant supplies the SM-4 positive evidence, and its revocation is followed by a denial check. Backup-prefix denial is recorded as deferred until Epic 8 creates the per-environment-instance prefixes; every target that exists at G1 is exercised here.

### Story 7.1: Enforce production admission at every boundary

As a production user,
I want production to admit me only through my explicit production admission, and only with my production permissions,
So that staging access or shared authentication never becomes production access.

**Repo:** Hexalith.Platform, with EventStore and module owners · **Covers:** FR-11, NFR-3, AR-25, AR-26 · **Prerequisites:** the EventStore admission predicate

**Acceptance Criteria:**

**Given** a user authorized only in staging
**When** they call production through a direct API, the CLI or MCP
**Then** every call is denied, for both data and operations

**Given** an admitted production user
**When** they call production
**Then** they get exactly their assigned production permissions, never unrestricted access
**And** a user authorized in both environments uses each with that environment's permissions

**Given** a release promotion
**When** it completes
**Then** no staging membership or permission has been copied into production

**Given** a token authenticated by the shared Keycloak without production admission, or a token carrying the synthetic flag
**When** presented to the production gateway
**Then** neither authentication nor the flag grants admission

### Story 7.2: Administer production admission as Administrator only

As Administrator,
I want production admission granted and revoked only by me, through an authenticated and auditable action,
So that no other path can make someone a production user.

**Repo:** Hexalith.Platform, Administrator operations · **Covers:** FR-11, AR-25

**Acceptance Criteria:**

**Given** a grant or revocation
**When** Administrator performs it
**Then** it follows the signed-record-then-apply flow from Story 6.11, and it is auditable

**Given** self-registration, first login, identity-provider mapping, staging administration or recovery authority, including the deputy's
**When** any is used to add a production user
**Then** the grant does not happen, and the attempt is recorded

**Given** application principals used with McpCli, UIs or agents
**When** inspected
**Then** none calls the Keycloak admin API, and agent-capable tokens carry no realm-management roles and no admin-API audience

### Story 7.3: Re-check admission for asynchronous cross-module steps

As a production user,
I want asynchronous steps done on my behalf to re-check my current admission,
So that revoking my admission also stops work already queued under my name.

**Repo:** Hexalith.Platform, with EventStore and module owners · **Covers:** FR-11, AR-26 · **Prerequisites:** the EventStore admission projection and original-actor attestation

**Acceptance Criteria:**

**Given** an asynchronous task step with an attested original actor
**When** it executes
**Then** it re-checks that actor's current admission against EventStore's admission projection, and runs only operations bound to that task

**Given** the projection is unknown, older than its declared maximum staleness, or shows the admission revoked
**When** the step checks
**Then** it fails closed

**Given** a synchronous cross-module step
**When** it calls another module
**Then** it uses Keycloak standard token exchange by the calling module's confidential client, downscoped to the target's declared operations
**And** raw user-token forwarding is refused

### Story 7.4: Prove the SM-4 positive case with the temporary synthetic grant

As Administrator,
I want to admit one designated synthetic identity temporarily, to prove admitted access works, and then revoke it,
So that SM-4 has positive production evidence before any real user is admitted.

**Repo:** Hexalith.Platform, Administrator operations · **Covers:** FR-11, SM-4, AR-39 (SM-4 grant)

**Acceptance Criteria:**

**Given** production after the G1 deployment
**When** Administrator grants the designated SM-4 identity membership in the human production-admission group
**Then** the signed record carries an expiry no later than G2, and the identity's permissions are limited to the synthetic tenant
**And** it is exercised only from executor and probe sources while ingress stays closed

**Given** the grant is active
**When** the identity invokes permitted operations
**Then** they succeed, and the positive evidence is recorded

**Given** the check is complete
**When** Administrator revokes the grant
**Then** a revocation record is written and a denial check proves that the identity is refused
**And** this is recorded without opening G2

### Story 7.5: Prove calling-surface restrictions in production

As Administrator,
I want every client in production's surface map proven unable to act outside its surface class,
So that agent-held tokens can never run UI-only or confirmation-required operations.

**Repo:** Hexalith.Platform · **Covers:** FR-12, AR-26

**Acceptance Criteria:**

**Given** each client in the production surface map
**When** its tokens call operations outside its class, directly and through one cross-module hop
**Then** each call is denied

**Given** a token carrying a confidential client's correct target audience but originating from an `agent` or `service` client
**When** presented
**Then** it is denied

**Given** a token exchanged for one target
**When** replayed to another internal host
**Then** it is denied

**Given** a confirmation-required cross-module chain
**When** EventStore's originating-surface attestation is not available
**Then** service clients may call only agent-eligible operations, so the chain is unavailable, never unsafe

### Story 7.6: Run the NFR-3 production isolation matrix

As Administrator,
I want every staging principal, workload and automation identity proven unable to reach production,
So that shared infrastructure never leaks production data or authority.

**Repo:** Hexalith.Platform · **Covers:** NFR-3, FR-10, SM-C2, AR-23 (negative tests)

**Acceptance Criteria:**

**Given** staging users, workloads, credentials, automation identities and pods
**When** they attempt any of these:
- production data services, secrets or OpenBao;
- application ports, sidecars, actors or workflows, including a staging app ID equal to a production one;
- volume or PersistentVolume binding;
- production hostnames or certificates;
- identity administration, including the staging management client and staging admins against the production realm, and McpCli tokens against the admin API;
- automation targets: production namespaces, environment-layer objects, registry writes, records, evidence, and the internal verification and recovery endpoints, including the staging recovery-hook principal and a prior-epoch credential

**Then** every attempt is denied
**And** the record shows zero successes

**Given** the staging-side results from Story 4.25
**When** combined
**Then** the full AD-8 matrix is recorded

**Given** an access change or a shared-infrastructure change
**When** it completes
**Then** the affected checks repeat

**Given** production backup prefixes do not yet exist
**When** the G1 matrix is recorded
**Then** backup-prefix denial is marked deferred rather than passed
**And** every other target that exists at G1 must pass

### Story 7.7: Use McpCli against production

As a production user,
I want McpCli to run my permitted operations in production and nothing else,
So that I can use agent access in production safely.

**Repo:** Hexalith.Platform, Hexalith.McpCli (owner review) · **Covers:** FR-12 (production), AR-46

**Acceptance Criteria:**

**Given** a production profile
**When** created
**Then** it binds the production gateway, issuer and audience, and sends tokens only to the production gateway

**Given** an admitted production user
**When** they invoke agent-eligible operations through the CLI and MCP
**Then** only operations their production permissions allow succeed
**And** ineligible, disabled-module, mismatched-contract and unauthorized operations are refused

**Given** a staging-only user
**When** they invoke any production operation through McpCli
**Then** it is refused

### Story 7.8: Record SM-4 evidence for the reference composition

As Administrator,
I want SM-4 recorded for the reference composition in both environments,
So that G2 has its access and isolation evidence.

**Repo:** Hexalith.Platform · **Covers:** SM-4, SM-C2, FR-10, FR-11, FR-12, NFR-3

**Acceptance Criteria:**

**Given** staging and production
**When** SM-4 runs
**Then** the declared supported interfaces and named agent-eligible McpCli operations are shown, allowed operations succeed, and every currently applicable FR-11, FR-12 and NFR-3 refusal check passes for users, workloads and automation
**And** the not-yet-created backup-prefix target remains explicitly deferred, never silently passed

**Given** the SM-4 record
**When** stored
**Then** it includes the synthetic grant's positive evidence, its revocation record and its denial check, with the release, environment, result and diagnostics

## Epic 8: Recover production from disaster and open it to users (G2)

Administrator or the deputy restores production on prepared capacity within one hour of data loss and four hours of recovery for outages that begin within declared response coverage. Recovery never resurrects Memories erasures or revoked admission. Backups run every 30 minutes under independent monitoring. After usable recovery points and Memories continuity exist, this epic qualifies in-place data restore and named retained-data recovery, completes the isolated monthly drill, closes the deferred backup-prefix isolation target, and opens G2 in the prescribed order.

### Story 8.1: Procure prepared replacement capacity and select the recovery executor host

As Administrator,
I want replacement compute and storage procured and an off-site recovery executor host selected,
So that a disaster restore has somewhere to go before it is ever needed.

**Repo:** Administrator operations · **Covers:** FR-9, NFR-2, AR-28 (recovery executor), AR-60 · *Independent, pull forward*

**Acceptance Criteria:**

**Given** production's representative data sizes
**When** capacity is procured
**Then** prepared replacement compute and storage are identified, with their location and capacity budget recorded
**And** whole-site coverage is claimed only if that location is independent of the primary site

**Given** the recovery executor host
**When** selected and reserved off-site
**Then** it is dedicated to replacement-capacity recovery and shared with no CI runner or other executor
**And** its base host is hardened, but no integrated recovery authority is claimed until the recovery contracts, artifacts, records, recovery points and custody release path exist

**Given** the procurement record
**When** completed
**Then** it names the capacity owner, location, readiness lead time, selected host and the prerequisites for later executor commissioning

### Story 8.2: Decide the reduced-recovery operating policy

As Administrator,
I want the reduced-recovery operating rules decided,
So that production after a disaster restore runs by known rules instead of improvisation.

**Repo:** Hexalith.Platform with Administrator · **Covers:** FR-9, AR-44, AR-60

**Acceptance Criteria:**

**Given** the reduced-recovery state after a verified disaster restore
**When** this story completes
**Then** a decision record fixes:
- whether automatic promotion may resume before the return to G2 conditions;
- the restore-exercise cadence while replacement capacity is consumed;
- how staging is re-established

**And** G2 is not met until this record exists

### Story 8.3: Publish response coverage and record incident timing

As Administrator,
I want response coverage published and every incident timed from the original outage,
So that the four-hour RTO applies exactly where we committed to it, and outages are never under-reported.

**Repo:** Administrator operations, Hexalith.Platform · **Covers:** NFR-2, AR-40 (detection and response)

**Acceptance Criteria:**

**Given** the recovery procedure
**When** coverage is published before the first qualifying drill
**Then** it states the coverage hours and time zone, the primary and deputy responsibility and the maximum acknowledgement time
**And** deputy hours count toward coverage only once the deputy's recovery capability has been proven

**Given** an incident
**When** recorded
**Then** it holds the original outage time, the actual response time, the coverage status and the full outage-to-verified-restoration duration
**And** the clock never pauses or restarts
**And** an outage that begins within coverage stays covered when the coverage window ends

### Story 8.4: Build the recovery inventory

As Administrator,
I want a complete recovery inventory, with an owner, a class and a fence procedure for everything production depends on,
So that no required state or authority is missed when production must be restored.

**Repo:** Hexalith.Platform, Administrator operations · **Covers:** FR-9, AR-40 · **Prerequisites:** module authoritative, rebuild-only and erasure inventories with integrity checks

**Acceptance Criteria:**

**Given** each deployed module
**When** inventoried
**Then** its authoritative databases, files and configuration are listed with a recovery class (authoritative-restore, rebuild-only or live-authority-only) and a recovery owner
**And** EventStore history alone is never assumed to restore another module

**Given** shared dependencies
**When** inventoried
**Then** the Keycloak database with its event export, each environment's OpenBao, the production access configuration and the revocation evidence are listed with owners, and evidence that each survives or can be restored

**Given** each authority that survives a failure
**When** inventoried
**Then** it has a named fence-and-reissue owner and procedure

**Given** a backup unit
**When** defined
**Then** it holds exactly one recovery class
**And** it contains rebuild-only state only when its restore discards that state as the state's owner prescribes

### Story 8.5: Back up production every 30 minutes with complete chains

As Administrator,
I want native backups every 30 minutes, retained immutably off-site,
So that a usable copy of production is never older than the RPO allows.

**Repo:** Hexalith.Platform, Administrator operations · **Covers:** FR-9, NFR-2, AR-40, AR-41

**Acceptance Criteria:**

**Given** each inventoried unit
**When** backed up
**Then** it uses native database backup or a module-allowlisted file-and-metadata backup, and a raw volume or snapshot copy never counts
**And** CloudNativePG backups and the Keycloak database backup are configured

**Given** the schedule
**When** running
**Then** backup or incremental runs start every 30 minutes
**And** frequent points are kept for seven days and daily points for 30 days, including the base backups and incremental chains they depend on

**Given** stored copies
**When** inspected
**Then** they are encrypted, immutable, restricted and off-site under per-environment-instance prefixes, with access and decryption material held independently of the primary failure domain
**And** rotated key generations are kept at least as long as the data backups

### Story 8.6: Keep tenant keys in separate custody

As Administrator,
I want tenant keys backed up in their own custody class, never alongside ciphertext backups,
So that erasing a tenant makes every earlier copy of its key unusable, while other tenants stay recoverable.

**Repo:** Hexalith.Platform, with Memories · **Covers:** FR-9, AR-41 (key custody)

**Acceptance Criteria:**

**Given** the tenant-key store
**When** backed up
**Then** it is excluded from ordinary OpenBao snapshots and backed up as a separate custody class, never held with ciphertext backups

**Given** a tenant erasure
**When** it is acknowledged
**Then** every earlier key backup becomes unusable for the erased tenant
**And** a fresh key backup keeps the other tenants recoverable

**Given** a recovery point
**When** it is recorded
**Then** its tenant-key backup covers every non-erased key generation referenced at or before the cut

### Story 8.7: Record usable recovery points at one declared cut

As Administrator,
I want each recovery point recorded as one verified cross-module set at a declared cut,
So that "we have backups" means "we can restore a consistent production".

**Repo:** Hexalith.Platform · **Covers:** FR-9, AR-40, AR-43 (cut identity)

**Acceptance Criteria:**

**Given** a backup run
**When** a recovery point is recorded
**Then** it holds:
- every inventory artifact at one declared cut, with the position each recovery class reaches;
- the compatible release and configuration identity and the profile digest;
- the Memories register population and sequence;
- tenant-key coverage;
- Keycloak event-export lag within its bound

**Given** the recorded point
**When** verified
**Then** its checksums, unbroken chains and test decryption with independent keys are verified when it is written, and recorded in off-site metadata
**And** independently timed snapshots or successful jobs alone never make a point usable

**Given** a generation that is missing without explanation, or unknown key lineage
**When** detected
**Then** the point is unusable

**Given** pruning
**When** it runs
**Then** it skips any point referenced by an open recovery, a pending named recovery or an unadopted staging candidate's reset

### Story 8.8: Monitor recovery-point freshness and coverage off-site

As Administrator or the recovery deputy,
I want the off-site monitor to watch recovery-point freshness, key coverage and platform health signals,
So that we learn about a backup gap before we need the backup.

**Repo:** Hexalith.Platform · **Covers:** FR-9 (monitoring), AR-40, AR-45

**Acceptance Criteria:**

**Given** the newest complete recovery point
**When** the monitor checks, at least every 15 minutes
**Then** it reports the point's age measured from its cut, warns before that age reaches one hour, and notifies Administrator and the deputy on a backup failure or an age over one hour

**Given** tenant-key coverage
**When** the monitor checks
**Then** it verifies coverage, including the erasure proof, without holding key material, and fails the point closed on an unexplained loss

**Given** Keycloak event export
**When** its lag exceeds the bound from Story 4.9
**Then** Administrator and the deputy are notified

**Given** seal state, volume headroom and expiries
**When** checked
**Then** problems are notified, each expiry is warned about a declared lead time ahead, and upcoming expiries are listed at every drill

### Story 8.9: Fence the failed environment and rebuild it on prepared capacity

As Administrator or the recovery deputy,
I want disaster recovery to start by fencing the old instance and rebuilding the environment on prepared capacity,
So that the failed site can never write again and the restore starts from a trusted base.

**Repo:** Hexalith.Platform (recovery workflows) · **Covers:** FR-9, AR-42 (steps 1–2), AR-41 (unseal)

**Acceptance Criteria:**

**Given** a disaster recovery started by Administrator or the deputy on the recovery executor
**When** step 1 runs
**Then** the promotion stop is set
**And** each surviving authority's fence-and-reissue owner revokes the old instance's credentials, through a sandboxed module fence hook or a Platform-owned procedure, and proves they fail
**And** the old node stays isolated from network and DNS

**Given** the selected recovery executor host from Story 8.1 and the completed recovery contracts, off-site artifacts, record store, usable recovery points and custody release path
**When** the executor is commissioned
**Then** a pinned off-site copy of the workflows and environment definitions runs with neither operations-repository write access nor GitHub
**And** its standing credentials cover only the prepared capacity, its record-signing identity, write access to the attempt, lock and stop store, and read-only access to recovery points and the off-site registry replica

**Given** step 2
**When** it runs
**Then** the environment layer and shared infrastructure are reproduced on prepared capacity from the off-site definitions at the recovery point's digest
**And** Keycloak and the environment's OpenBao are restored into quarantine under custody, with the staging realm disabled
**And** the recovery point's release is staged from the off-site replica after its provenance is verified

**Given** OpenBao
**When** it needs unsealing
**Then** Administrator or the deputy unseals it manually as custodians, any custodian use alerts, and any root token is revoked at recovery end

**Given** the real fence steps
**When** prepared for G2
**Then** they have been rehearsed on staging

### Story 8.10: Restore keys and data into quarantine with module recovery hooks

As Administrator or the recovery deputy,
I want keys and data restored into quarantine and each module's recovery hooks run in order,
So that restored state is consistent, erasures stay erased, and nothing user-triggered runs early.

**Repo:** Hexalith.Platform · **Covers:** FR-9, AR-42 (steps 3–4) · **Prerequisites:** module recovery-hook implementations

**Acceptance Criteria:**

**Given** step 3
**When** keys are restored
**Then** the tenant-key store is restored and tombstone key destruction is re-applied from the surviving tombstone mirror, read through its fence-and-reissue owner
**And** a missing generation is accepted only with current tombstone and known-lineage proof, and unknown lineage stops the recovery

**Given** step 4
**When** data is restored
**Then** module data is restored into quarantine with no application workloads running
**And** the recovery point's release then starts by digest in recovery mode, on a forward catalog generation that keeps later idempotency and key entries as non-executable retention entries

**Given** recovery mode
**When** active
**Then** user ingress is closed, external-effect and destructive-retention workers are disabled, and quarantine admits only the owning executor, the recovery workloads and probe sources

**Given** module recovery hooks
**When** invoked in declared dependency order (admission and purge, re-provisioning and rotation, then rebuild-only replay)
**Then** each runs inside its module's workload through the recovery-mode endpoint, with a credential bound to the current attempt and epoch from an issuer created on the prepared capacity
**And** it commits only while its epoch is current

**Given** the broker
**When** step 4 completes
**Then** the broker is re-provisioned empty, Subscriptions are rendered, and subscribers catch up from their restored checkpoints

### Story 8.11: Rotate credentials and reconcile production admission after restore

As Administrator,
I want restored credentials rotated and admission reconciled to my signed records,
So that a restore never brings back revoked access or grants anything new.

**Repo:** Hexalith.Platform · **Covers:** FR-9, FR-11, AR-42 (step 5)

**Acceptance Criteria:**

**Given** step 5
**When** it runs
**Then** every restored credential and application signing key is rotated, apart from module-owned dynamic namespaces, which their hooks rotate
**And** realm signing keys are rotated unless the old issuer is proven unavailable
**And** a compromise-driven restore also rotates the Dapr trust root and realm keys without that exception

**Given** restored admission membership
**When** reconciled against the admission records
**Then** every principal without a current grant, or with a later revocation, is removed, including a revocation made just before the failure
**And** grants recorded after the cut are listed for Administrator to re-apply, and are never added by recovery

**Given** admin and user revocations recorded after the cut
**When** step 5 completes
**Then** they are re-applied
**And** old credentials are proven to fail against the restored instances
**And** EventStore's admission projection is rebuilt from the reconciled realm before any user, scheduled or asynchronous work runs

### Story 8.12: Verify, record the lost window and reopen service

As Administrator or the recovery deputy,
I want the restored environment verified, the lost window recorded, and service reopened only to its pre-incident ingress state,
So that users come back to a verified production and every known loss is on record.

**Repo:** Hexalith.Platform · **Covers:** FR-9, NFR-3, AR-42 (steps 6–7), AR-44

**Acceptance Criteria:**

**Given** step 6
**When** verification runs, with user ingress still closed
**Then** it checks compatible releases, cross-module integrity, restored-release smokes, the SM-4 access outcomes, denial of every principal whose latest admission record is a revocation (at the gateway and through an asynchronous re-check), and new-credential success with old-credential failure at every surviving authority
**And** each module's workers are re-enabled only once its hook reports its external-operation state reconciled

**Given** the lost window
**When** recorded before reopening
**Then** the DR report lists each accepted category and its window:
- module-owned authorization revocations after the cut;
- identity-provider access removals beyond the export frontier;
- non-Memories deletions, erasures and legal holds

**Given** step 7
**When** it runs
**Then** protection and monitoring resume, DNS and certificates cut over, fresh synthetic and deployment credentials are handed to the production executor, its allowlist is re-provisioned, and both are recorded
**And** Administrator or the deputy restores only the pre-incident ingress state recorded in the attempt, keeping user ingress closed before G2
**And** the promotion stop stays set

### Story 8.13: Preserve Memories erasure continuity through recovery

As a Memories tenant,
I want recovery to keep every erasure I was told had happened,
So that my erased data or keys never come back after a restore.

**Repo:** Hexalith.Platform, with Memories and EventStore · **Covers:** FR-9, AR-40 (Memories erasure continuity), AR-60 (Memories conformance) · **Prerequisites:** the Memories tombstone mirror and lineage protocol, tenant-key store, per-tenant principals, operator artifact and tombstone-mirror fence hook

**Acceptance Criteria:**

**Given** a Memories erasure acknowledged before the failure, including one inside the RPO window
**When** production is restored
**Then** the tombstone is preserved, and neither the erased tenant nor its keys are resurrected

**Given** Memories' Redis and FalkorDB projections and tenant-keyed coordination
**When** restored
**Then** they are rebuilt through authoritative replay, never from copies
**And** a copied register is never treated as live authority

**Given** unknown authority lineage
**When** detected
**Then** the recovery fails closed

**Given** Memories partitions captured in backups
**When** inspected
**Then** they are protected by EventStore payload protection

**Given** the remaining direct Redis coordination in Memories
**When** G2 is evaluated
**Then** it has migrated to Dapr, as the AD-9 transition requires

### Story 8.14: Prove the deputy's recovery capability

As Administrator,
I want the deputy proven able to restore and reopen production on their own,
So that recovery doesn't depend on me being available.

**Repo:** Administrator operations · **Covers:** FR-8, FR-9, AR-60 (deputy proof)

**Acceptance Criteria:**

**Given** the deputy named in Story 6.3
**When** proven
**Then** their key custody (unseal and recovery keys, decryption material) is independent of Administrator's and of the primary failure domain

**Given** a rehearsal
**When** the deputy runs the documented restore and reopen alone
**Then** it succeeds under their own MFA identity, with no operations-repository write access and no dependency on GitHub

**Given** the proof
**When** recorded
**Then** the deputy's hours start to count toward response coverage

### Story 8.15: Operate the reduced-recovery state after a disaster restore

As Administrator,
I want production after a verified disaster restore to run in a recorded reduced-recovery state until full recovery capability returns,
So that everyone knows the four-hour RTO is suspended and releases stay stopped.

**Repo:** Hexalith.Platform · **Covers:** FR-9, AR-44

**Acceptance Criteria:**

**Given** a verified disaster restore
**When** it completes
**Then** the DR attempt record becomes the working baseline, and production enters a recorded reduced-recovery state, which is not degraded production

**Given** the reduced-recovery state
**When** active
**Then** user access may continue
**And** incidents record the state, and no four-hour RTO is committed for a further failure that needs replacement capacity

**Given** the promotion stop
**When** in the reduced-recovery state
**Then** it stays set until Administrator re-establishes staging under the policy from Story 8.2
**And** no emergency release path exists, including for application security fixes

**Given** new prepared capacity, a repeat drill recorded as a material change, and an Administrator record of return to G2 conditions
**When** all three exist
**Then** the reduced-recovery state ends

### Story 8.16: Qualify in-place data restore and named retained-data recovery

As Administrator or the recovery deputy,
I want production recovery-point restoration and the named retained-data release path qualified,
So that incompatible releases can recover safely without pretending baseline redeploy or automatic rollback restores data.

**Repo:** Hexalith.Platform (operations and recovery workflows) · **Covers:** FR-8, FR-9, NFR-1, AR-35, AR-42

**Acceptance Criteria:**

**Given** a usable production recovery point, retained artifacts and qualified Memories erasure continuity
**When** Administrator or the deputy starts an in-place data restore on the production executor
**Then** it takes a new epoch, keeps the promotion stop set, closes user ingress to executor and probe sources, removes application workloads while keeping data objects, and discards post-cut broker backlog, dead letters, consumer offsets and Scheduler jobs
**And** it restores keys and data, runs the previous recovery hooks in dependency order, rotates credentials, reconciles admission and verifies readiness, smoke, integrity and access before any ingress state is restored

**Given** an interrupted or failed in-place data restore
**When** it is resumed
**Then** it re-enters as a data restore from the same recorded point under a new epoch
**And** exceeding its recorded maximum duration notifies Administrator and the deputy without aborting the recovery

**Given** a candidate without valid compatibility evidence against the working baseline
**When** its approved retained-data path is qualified
**Then** the qualification binds the staged rehearsal, named recovery procedure, recovery-point requirements, retained artifacts, maximum duration and acceptance checks to that candidate
**And** it prepares no automatic rollback and grants no authority beyond one Administrator-approved attempt

**Given** the named recovery rehearsal
**When** a post-cut admission revocation and a Memories erasure are introduced
**Then** both survive restoration, the lost window is reported and the NFR-3 access outcomes pass before the qualification is accepted

**Given** the qualification is missing, stale, bound to another candidate or baseline, or any recovery check fails
**When** an approved retained-data production attempt is evaluated
**Then** it is refused before mutation

### Story 8.17: Run the isolated DR drill and prove RPO and RTO

As Administrator,
I want an isolated drill that proves production can be recovered within the RPO and RTO targets,
So that G2 rests on measured recovery, not intent.

**Repo:** Hexalith.Platform, Administrator operations · **Covers:** FR-9, NFR-2, NFR-3 (restored copies), SM-6, SM-C2, SM-C3, AR-40 (DR evidence)

**Acceptance Criteria:**

**Given** a drill that assumes the primary server and storage are lost
**When** it runs with the declared worst-case response
**Then** it measures detection (the probe interval plus the measured probe-to-issue latency), response, capacity, transfer, replay at representative size, restore and validation
**And** it proves an RPO of at most one hour and an outage-to-verified-service RTO of at most four hours

**Given** substituted steps (fence against drill-scoped copies, Keycloak restored or generated from the realm contract with the export applied, DNS and certificate cutover, mirror copies)
**When** used
**Then** each records a measured or bounded duration that is added to the RTO
**And** an unbounded substitution fails the RTO proof

**Given** the drill environment
**When** used
**Then** it never mutates live production or shared authority, its restores are ephemeral and egress-denied, and its copies are destroyed with verification within a stated bound
**And** a real DR entry aborts the drill and reclaims the capacity

**Given** the drill's restored copies
**When** staging users, workloads, credentials and automation attempt to reach them
**Then** every attempt is denied, and the copies are handled as restricted production data

**Given** the production backup prefixes created in this epic
**When** staging credentials and automation try to list, read, write or delete them
**Then** every attempt is denied and recorded, closing the deferred backup-prefix target from Story 7.6

**Given** the drill record
**When** stored
**Then** it lists the recovered-data age, coverage status, full elapsed time, every substitution, every lost-window category, the grants to re-apply, the reduced-recovery state with the stop set, and denial of an admission revoked just before the failure, including a revocation lost before event export
**And** the drill repeats monthly and after material storage, backup or recovery-mechanism changes

### Story 8.18: Retain production telemetry off-site for incident analysis

As Administrator,
I want production telemetry retained off the primary failure domain,
So that incidents can be analysed even after losing the primary site.

**Repo:** Hexalith.Platform · **Covers:** AR-45 (telemetry durability), AR-56

**Acceptance Criteria:**

**Given** the production telemetry sink
**When** configured
**Then** the telemetry needed for incident analysis ships off the primary failure domain with a declared minimum retention
**And** production data is readable only by production principals

### Story 8.19: Open production to users (G2)

As Administrator,
I want production opened to users only after every G2 condition is met, and in a controlled order,
So that the first real users arrive at a recoverable, isolated and verified production.

**Repo:** Hexalith.Platform, Administrator operations · **Covers:** FR-10, FR-11, SM-4, SM-6, AR-39 (G2)

**Acceptance Criteria:**

**Given** G2 evaluation
**When** checked
**Then** each of these is present:
- a passed DR drill, including Memories tombstone and key continuity;
- Memories conformance, including the migration of its remaining direct Redis coordination to Dapr (Story 8.13);
- SM-4 evidence, with the G1 grant's revocation record and denial check;
- verified recovery access, capacity, deputy alert delivery and response coverage;
- qualified in-place data restore and named retained-data recovery (Story 8.16);
- the reduced-recovery policy record;
- telemetry durability

**And** G2 does not open if any one is missing

**Given** the conditions are met
**When** Administrator admits the first user
**Then** SM-4 is re-verified on the live production realm with that user, and staging users and staging-issued tokens are proven rejected through public ingress

**Given** the first-user SM-4 re-verification passes
**When** further users are admitted
**Then** each goes through the Administrator admission flow

**Given** the first-user SM-4 re-verification fails
**When** detected
**Then** G2 aborts, and the admitted user is removed

## Epic 9: Promote releases to production automatically (G3)

Releases with valid compatibility evidence against the current baseline, and a passing shared-infrastructure currency check, promote automatically. The SM-5 rehearsals, run on the shared harness from Epic 6, prove every failure trigger and every recovery, stop and approval rule. With complete recovery points now available, they also qualify successful shared-infrastructure execution and forward revert. Any policy change suspends automatic promotion until the affected rehearsals repeat.

### Story 9.1: Decide the shared-infrastructure currency policy

As Administrator,
I want the currency check's inventory, cadence, allowed lag and effect on approved attempts decided,
So that "security-current" has a precise, enforceable meaning before automatic promotion.

**Repo:** Hexalith.Platform with Administrator · **Covers:** FR-10 (currency check), AR-61

**Acceptance Criteria:**

**Given** the profile inventory
**When** this story completes
**Then** a decision record fixes the checked components, the check cadence, the allowed lag behind supported, security-current versions, and whether a failed check also affects Administrator-approved attempts
**And** the check still runs at every production attempt and every monthly drill

### Story 9.2: Run the shared-infrastructure currency check

As Administrator,
I want deployed shared components compared with the inventory at every production attempt and every drill,
So that automatic promotion never runs on stale or unpatched infrastructure.

**Repo:** Hexalith.Platform · **Covers:** FR-10, AR-31, AR-61

**Acceptance Criteria:**

**Given** a production attempt or a monthly drill
**When** the check runs
**Then** it compares each deployed shared component with the supported, security-current version in the inventory under the policy from Story 9.1, and records the result

**Given** a failed check
**When** an automatic promotion is evaluated
**Then** the automatic path is blocked, naming the components that are out of date
**And** approved attempts follow the policy from Story 9.1

### Story 9.3: Verify EventStore's G3 confirmations

As Administrator,
I want EventStore's catalog and record behaviours that automatic rollback relies on confirmed and verified,
So that automatic recovery can depend on them.

**Repo:** Hexalith.Platform, with EventStore · **Covers:** AR-58 (G3 confirmations), AR-62, NFR-1 · **Prerequisites:** EventStore G3 confirmations

**Acceptance Criteria:**

**Given** EventStore's confirmations of AD-15 retention entries, catalog activation order, expected-generation catalog commit, the recovery-point forward generation, and production-promoted record invalidation and renewal
**When** received
**Then** each is verified by a staging test whose result is recorded
**And** G3 cannot be recorded while any is missing

### Story 9.4: Rehearse deployment verification and one-shot recovery (SM-5, part 1)

As Administrator,
I want every deployment-failure and recovery path rehearsed,
So that automatic promotion relies on proven behaviour.

**Repo:** Hexalith.Platform · **Covers:** SM-5, FR-7, FR-8, NFR-1, SM-C3

**Acceptance Criteria:**

**Given** the shared harness
**When** rehearsals run in approved mode
**Then** each FR-7 trigger fires: rollout not ready by 10 minutes, 60 s unavailability, two consecutive smoke failures, and a missing result

**Given** a failed compatible deployment
**When** recovery runs
**Then** one recovery completes within its 10-minute and five-minute budgets
**And** business data, event history, credential rotations and revocations are proven unchanged

**Given** an interruption, a routing-only change, a first deployment, and a failed or unverified recovery
**When** each is rehearsed
**Then** each follows its defined path, and none is recorded as a successful recovery without its readiness, smoke and data checks

### Story 9.5: Rehearse the promotion-stop and approval rules (SM-5, part 2)

As Administrator,
I want every stop trigger and every approval rule rehearsed,
So that promotion can never resume, or be approved, when it shouldn't.

**Repo:** Hexalith.Platform · **Covers:** SM-5, FR-8, SM-C1

**Acceptance Criteria:**

**Given** each stop trigger
**When** rehearsed
**Then** it sets a durable stop that the monitor never clears, and only a signed Administrator record clears it

**Given** a clear
**When** rehearsed
**Then** Administrator's lost-window and admission reviews happen first
**And** a persisting condition that is not marked accepted is re-recorded after the clear

**Given** approved degraded attempts
**When** rehearsed
**Then** each of these is shown:
- an approval clears only the stop it observed;
- a later stop prevails over the clear;
- a failed degraded attempt removes the candidate and sets a new stop;
- approval is refused when production is neither empty nor degraded;
- the deputy cannot approve

### Story 9.6: Rehearse release-mode, retained-data and infrastructure rules (SM-5, part 3)

As Administrator,
I want the release-mode, retained-data and shared-infrastructure rules rehearsed,
So that automatic promotion and its alternatives each behave as specified.

**Repo:** Hexalith.Platform · **Covers:** SM-5, FR-6, FR-8, FR-10, NFR-1

**Acceptance Criteria:**

**Given** both release modes
**When** rehearsed
**Then** each requires exact-release staging E2E evidence for the complete enrolled composition
**And** automatic promotion rejects invalid compatibility evidence and requires a candidate-to-current-baseline rehearsal

**Given** a failed approved incompatible release
**When** rehearsed
**Then** user ingress closes and the planned recovery completes
**And** an admission revocation and a Memories erasure acknowledged during the attempt are both preserved

**Given** a failed shared-infrastructure verification and a failed currency check
**When** rehearsed
**Then** the first sets the stop and recovers by forward revert, and the second blocks automatic promotion

**Given** the guarded shared-infrastructure procedure, a complete recovery point and prepared production-profile capacity
**When** its success and failure paths are qualified
**Then** a bounded change executes under both environment locks, both served releases pass smoke and NFR-3 verification, and the successful attempts are recorded
**And** an injected verification failure is recovered by a recorded forward revert that restores both environments' verified state

**Given** GitHub delivery and the deputy's recovery access
**When** rehearsed
**Then** both are demonstrated

### Story 9.7: Suspend automatic promotion when policy changes

As Administrator,
I want any change to verification, recovery or rollback-set policy to suspend automatic promotion until the affected rehearsals repeat,
So that automation never runs on unproven rules.

**Repo:** Hexalith.Platform · **Covers:** FR-6, AR-39 (G3 suspension), SM-C1

**Acceptance Criteria:**

**Given** a change to FR-7 or FR-8 thresholds or budgets, recovery procedures or rollback-set rules
**When** recorded
**Then** automatic promotion is suspended
**And** Platform and Administrator record which SM-5 rehearsals the change affects, including whether a change to module readiness or smoke declarations affects any

**Given** the affected rehearsals
**When** they have all passed again
**Then** automatic promotion resumes, with the record linking the change to the rehearsal evidence

### Story 9.8: Reach G3 and enable automatic promotion

As Administrator,
I want automatic promotion enabled only after SM-5 passes on the reference composition,
So that releases reach production automatically on proven safety.

**Repo:** Hexalith.Platform · **Covers:** FR-6 (automatic mode), SM-5, SM-C1, AR-39 (G3)

**Acceptance Criteria:**

**Given** the SM-5 rehearsals from Stories 9.4–9.6, the EventStore confirmations from Story 9.3 and the currency policy from Story 9.1
**When** all are recorded
**Then** Administrator records G3

**Given** G3
**When** a release has complete compatibility evidence against the current production baseline and the currency check passes
**Then** the production workflow promotes it automatically, with the same staging gate, lock, provenance, verification and records as approved mode

**Given** missing, failing, stale or wrong-baseline compatibility evidence, or a failed currency check
**When** automatic promotion is evaluated
**Then** the automatic path is refused
**And** Administrator approval still requires the applicable fresh-install, compatible-baseline or qualified named-recovery path and never bypasses staging evidence

**Given** the promotion history
**When** audited
**Then** there are zero automatic promotions without valid compatibility evidence and G3

## Epic 10: Complete the MVP across every environment

Folders and Projects join staging and production through the same gates. Folders' idempotency-intent adapters run as composed-host extension packages. The full seven-module release passes the staging gate, production verification, the access matrix, a repeat DR drill and the SM-5 rehearsals. The MVP is accepted with all seven modules through G1, G2 and G3.

### Story 10.1: Enroll Folders into staging

As a Folders user,
I want Folders available in staging through the same gates as the reference modules,
So that Folders is proven in a hosted environment before production.

**Repo:** Hexalith.Platform, Hexalith.Folders (owner review) · **Covers:** FR-6, FR-10, FR-12, AR-7, AR-52, AR-56 · **Prerequisites:** Folders' adapters as extension packages; Folders declarations, critical flows, E2E and smoke suites, surfaces, recovery inventory and hooks

**Acceptance Criteria:**

**Given** Folders' hosted declaration
**When** validated for staging
**Then** its surfaces, exposure classes, agent eligibility, critical flows with E2E checks, smoke suites, recovery inventory and hooks all pass validation

**Given** the composed `platform/eventstore` image
**When** published with Folders enrolled
**Then** it includes Folders' idempotency-intent adapters as extension packages
**And** no Folders artifact publishes an image named `eventstore`

**Given** a staging attempt of the release with Folders
**When** it runs
**Then** Folders' critical flows pass for that exact release, and its agent-eligible operations work through McpCli in staging

**Given** Folders' stricter infrastructure clauses (I-10, I-11, EXT-ES-RECOVERY)
**When** enrollment is evaluated
**Then** the Platform MVP recovery envelope takes precedence, and they block nothing
**And** Folders' module behavior, authorization and release gates still apply

### Story 10.2: Enroll Projects into staging

As a Projects user,
I want Projects available in staging through the same gates,
So that Projects is proven in a hosted environment before production.

**Repo:** Hexalith.Platform, Hexalith.Projects (owner review) · **Covers:** FR-6, FR-10, FR-12, AR-26, AR-52 · **Prerequisites:** Projects declarations, critical flows, E2E and smoke suites, surfaces, recovery inventory and hooks

**Acceptance Criteria:**

**Given** Projects' hosted declaration
**When** validated for staging
**Then** its surfaces, exposure classes, agent eligibility, critical flows with E2E checks, smoke suites, recovery inventory and hooks all pass validation

**Given** Projects operations that need human confirmation
**When** invoked through McpCli
**Then** they are refused, and remain available only through the confidential Projects UI (AD-14 takes precedence over Projects AD-30)

**Given** a staging attempt of the release with Projects
**When** it runs
**Then** Projects' critical flows pass for that exact release

**Given** Projects' AD-28 availability, RPO-0, and G-1 durable task engine clauses
**When** enrollment is evaluated
**Then** the Platform MVP envelope takes precedence, and Projects AD-30 gates may not demand HA or RPO-0 evidence

### Story 10.3: Promote the full seven-module release to production

As Administrator,
I want the release containing all seven modules promoted to production, first-enrolling Folders and Projects there,
So that production serves the complete MVP.

**Repo:** Hexalith.Platform · **Covers:** FR-6, FR-7, FR-8, FR-10

**Acceptance Criteria:**

**Given** the full release with complete staging evidence
**When** promoted in the mode the current gate allows
**Then** every precondition holds for all seven modules, and Folders' and Projects' smoke suites take part in the verification window

**Given** a failed first enrollment of Folders or Projects
**When** recovered
**Then** its workloads are removed, and its data objects are kept

**Given** Folders' and Projects' creation tasks
**When** they are first enrolled into production
**Then** they run once per environment, and never on upgrade, rollback or restore

### Story 10.4: Re-verify SM-4 across all seven modules

As Administrator,
I want SM-4 re-verified with Folders and Projects included,
So that access and isolation are proven for the complete MVP.

**Repo:** Hexalith.Platform · **Covers:** SM-4, SM-C2, FR-10, FR-11, FR-12, NFR-3

**Acceptance Criteria:**

**Given** staging and production with all seven modules
**When** SM-4 runs
**Then** every module's declared interfaces and named agent-eligible McpCli operations are shown, allowed operations succeed, and every FR-11, FR-12 and NFR-3 refusal passes, including the surface-class negative tests for the new clients

### Story 10.5: Repeat the affected SM-5 rehearsals for the full composition

As Administrator,
I want the SM-5 rehearsals affected by Folders' and Projects' readiness and smoke declarations repeated,
So that automatic promotion stays proven for the complete release.

**Repo:** Hexalith.Platform · **Covers:** SM-5, SM-C1, FR-6

**Acceptance Criteria:**

**Given** the new modules' readiness and smoke declarations
**When** assessed under the rule from Story 9.7
**Then** the affected rehearsals are listed, and automatic promotion is suspended until they pass again

**Given** the repeated rehearsals
**When** they pass
**Then** SM-5 is recorded for the full composition

### Story 10.6: Repeat the DR drill with all seven modules

As Administrator,
I want a DR drill that restores Folders' and Projects' state along with the rest,
So that the one-hour RPO and four-hour RTO are proven for the complete MVP.

**Repo:** Hexalith.Platform, Administrator operations · **Covers:** SM-6, FR-9, NFR-2

**Acceptance Criteria:**

**Given** enrolling Folders and Projects is a material change
**When** the drill repeats
**Then** it restores their authoritative state with their integrity and reconciliation checks, and runs their restored-release smokes
**And** it proves RPO of at most one hour and RTO of at most four hours at representative size, including their state

**Given** the drill record
**When** stored
**Then** it lists every lost-window category, including theirs

**Given** the full-composition drill
**When** its operating controls are evaluated
**Then** it re-verifies backup cadence and retention, independent availability and recovery-point freshness monitoring, monitor-silence and dead-man-silence detection in both directions, and actual GitHub delivery to Administrator and the deputy
**And** any failed operating-control check fails the full-composition SM-6 result

### Story 10.7: Record MVP acceptance

As Administrator,
I want MVP acceptance recorded against every success measure and counter-metric,
So that "done" means demonstrated, with any open item stated honestly.

**Repo:** Hexalith.Platform · **Covers:** SM-1–SM-6, SM-C1–SM-C5

**Acceptance Criteria:**

**Given** SM-1 to SM-6
**When** acceptance is evaluated
**Then** each is linked to its recorded demonstration, with release or source revision, loaded identities, environment, result and diagnostics

**Given** SM-C1 to SM-C4
**When** evaluated
**Then** each shows zero violations, and SM-C5 shows the recorded startup durations and overrides

**Given** the success signal
**When** demonstrated
**Then** it shows:
- Parties debugged from its own workspace against real services;
- a release proven in staging reaching `tache.ai` through G1, G2 and G3;
- a rehearsed failure rolled back once with data intact;
- a drill meeting the RPO and RTO

**Given** a missing module adoption or demonstration
**When** found
**Then** acceptance is recorded as open, naming the gap, and never as passed

## Epic 11: Retire legacy hosting and module-specific MCP/CLI surfaces *(not MVP-blocking)*

Developers and operators end with one supported hosting path and one CLI/MCP surface. Frozen domain AppHosts and the Works preview retire after parity. Legacy MCP/CLI sources leave every composition and route after their AD-11 evidence passes.

### Story 11.1: Enforce the legacy-surface freeze in every composition

As a Platform maintainer,
I want no legacy MCP/CLI surface enrolled, deployed, routed, mapped or issued a realm client in any Platform composition,
So that McpCli stays the only Hexalith-owned CLI/MCP surface while migration proceeds.

**Repo:** Hexalith.Platform · **Covers:** FR-12 (legacy clause), AR-47

**Acceptance Criteria:**

**Given** legacy module and technical-module MCP hosts, plug-ins and CLIs, including EventStore Admin.Cli and Admin.Mcp
**When** any composition renders, locally, in CI or hosted
**Then** none is enrolled, deployed, routed or mapped, and no realm client is issued to one

**Given** a new proprietary MCP/CLI host or a new McpCli transport
**When** proposed for a composition
**Then** it is refused until a Platform AD admits it under AD-14

**Given** a temporary compatibility use of a legacy surface
**When** needed
**Then** it happens only outside every Platform composition, under a named migration record with a removal gate

**Given** EventStore Admin.Server and Admin.UI
**When** composed
**Then** they remain a confidential UI surface
**And** confirmation-required admin operations are available only through that UI, or are withdrawn

### Story 11.2: Remove a retired legacy MCP/CLI surface from compositions and routes

As a Platform maintainer,
I want each legacy surface removed from Platform once its retirement evidence passes,
So that obsolete surfaces disappear without losing an approved capability.

**Repo:** Hexalith.Platform · **Covers:** FR-12 (legacy clause), AR-47 · **Prerequisites:** the McpCli migration inventory schema; McpCli Epic 5 inventory and cutover for that source; EventStore AD-22 consumer-removal authority where it applies

**Acceptance Criteria:**

**Given** a legacy source
**When** its retirement is evaluated
**Then** removal proceeds only if every one of these is present:
- an owner-approved operation inventory;
- a McpCli replacement or an approved withdrawal for each operation;
- AD-14 authorization, audit, structured-output and exit-code parity;
- compatibility and acceptance evidence;
- AD-22 authority where it applies

**Given** the evidence passes
**When** removal happens
**Then** any remaining Platform references, routes, realm entries and documentation for that source are removed, and the removal is verified in every environment

**Given** missing evidence for any operation
**When** evaluated
**Then** removal is refused, naming the operation

### Story 11.3: Retire a frozen domain-module AppHost after parity

As a module owner,
I want my module's frozen AppHost, Aspire and ServiceDefaults projects retired once Platform demonstrably replaces them,
So that there is one hosting path and no second composition to maintain.

**Repo:** Hexalith.Platform, the owning module (owner review) · **Covers:** AR-49, AR-50

**Acceptance Criteria:**

**Given** a domain module's hosting inventory from Story 1.13
**When** retirement is evaluated
**Then** it proceeds only with source, package and deployed parity evidence, plus the applicable EventStore AD-22 exact-subject consumer-removal authority

**Given** producers and consumers
**When** retiring
**Then** producers are adopted before any consumer is retired

**Given** the retirement
**When** complete
**Then** the module's hosting projects are removed, and its development and CI run only through Platform

### Story 11.4: Move the Works lane onto the declaration schema and retire the Works preview

As a Works developer,
I want Works composed through the same declaration schema without hard-coded sibling paths,
So that the Works preview can retire and Platform has one composition model.

**Repo:** Hexalith.Platform, Hexalith.Works (owner review) · **Covers:** AR-49, AR-50

**Acceptance Criteria:**

**Given** the Works lane in `apphost.cs`
**When** migrated
**Then** it resolves Works through the source mapping from direct declarations, with no hard-coded sibling `../works` path

**Given** Works' migration parity gate
**When** it passes
**Then** the opt-in Works preview is removed, and this spine's ownership split supersedes Works AD-20

## Epic 12: Enroll the Agents host composition (EXT-HOST-1) *(post-MVP)*

Agents runs through Platform's composition with no Agents-owned AppHost, Aspire or ServiceDefaults, and `./eng/verify-agents-host.sh` passes.

### Story 12.1: Declare Agents and its dependencies through the Platform schema

As an Agents developer,
I want the Agents DomainService and UI, and their dependencies, declared through the Platform declaration schema,
So that Platform composes Agents like any other module.

**Repo:** Hexalith.Agents (owner review), Hexalith.Platform · **Covers:** AR-51 · **Prerequisites:** Agents Story 5.6

**Acceptance Criteria:**

**Given** the Agents declaration
**When** validated
**Then** it declares the Agents DomainService and UI together with EventStore, Conversations, Parties and Tenants, the provider and safety adapters, Dapr Workflow with scoped WorkflowAccessPolicies, and its secrets, health, identity and telemetry needs

**Given** Agents' AppHost, Aspire and ServiceDefaults projects
**When** this story lands
**Then** they are frozen, and inventoried for retirement

### Story 12.2: Compose Agents from a clean checkout and pass the EXT-HOST-1 verification

As an Agents developer,
I want Platform to compose Agents from a clean checkout with no Agents-owned hosting,
So that EXT-HOST-1 moves from Committed to Available.

**Repo:** Hexalith.Platform · **Covers:** AR-51

**Acceptance Criteria:**

**Given** a clean checkout
**When** Platform's Aspire model composes Agents
**Then** it starts with no Agents-owned AppHost, Aspire or ServiceDefaults, and `./eng/verify-agents-host.sh` passes

**Given** the Level 4 and Level 5 evidence from Agents Story 5.6
**When** recorded
**Then** the EXT-HOST-1 register status moves from `Committed` to `Available`
