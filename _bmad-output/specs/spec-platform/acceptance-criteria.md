# Acceptance Criteria

Testable consequences for each capability, taken from PRD FR-N (CAP-N = FR-N) and NFR-1..NFR-3. Where the spine makes a rule stricter, this file cites the spine clause instead of restating it. Record every demonstration as [success-measures.md](success-measures.md) requires.

Spine: `../../planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md` (RRA = its Release and Recovery Acceptance table).

## CAP-1 — Complete local environment

- EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories, plus their required supporting components, are available together for testing and debugging.
- Readiness follows the module-owned checks and startup policy in CAP-4. The complete environment uses the largest declared startup override (spine Local tool and readiness).

## CAP-2 — Debug the active module checkout

- **Domain modules** (Tenants, Parties, Folders, Projects): source changes and breakpoints affect the running module from its own workspace checkout, with Platform as a direct submodule.
- **EventStore, Memories and McpCli:** source changes and breakpoints affect the running code from their checkouts in the Platform workspace, where they are root-declared and built in Debug. McpCli needs canonical McpCli enrollment in Platform's references first ([sequencing.md](sequencing.md)).
- Only the active root repository's direct `references/` declarations are initialized; nested submodules remain uninitialized.
- Local development and testing use project references and Debug assets. CI/CD uses NuGet package references and Release assets.
- Missing required source fails with the dependency and path named. Nothing falls back to another copy (AD-4).
- The workspace's Platform submodule commit is the single Platform identity (AD-4).

## CAP-3 — Developer-defined minimum environment

- The module developer controls the module's server list: one list for development and integration testing in the MVP.
- A domain module's minimum environment is EventStore, Tenants and Memories plus whatever else its developer declares.
- **Parties example:**
  - Platform runs the active Parties checkout with EventStore, Tenants and Memories, as the Parties declaration specifies. Unrelated domain modules are not required.
  - The Parties repository directly references EventStore, Tenants, Memories and Platform under `references/`. This is the requested arrangement; the Parties checkout does not implement it yet.
- Platform runs the declared servers as real services.
- Missing, duplicate or incompatible declarations fail validation (spine Module declaration).

## CAP-4 — Real-service integration tests, locally and in CI

**Environment and readiness**

- Parties integration tests run against real EventStore, Tenants and Memories, plus any other dependencies the Parties declaration selects.
- Local runs use Aspire with the module declaration and the active checkout. CI runs follow the Release/NuGet rule in CAP-2.
- Tests start only after the selected module and its required dependencies report readiness and all required one-off startup tasks succeed.
- The default startup deadline is 10 minutes, measured from the start request until all required resources are ready.
  - It excludes test execution and does not change production deadlines.
  - The test configuration may override it with a justified finite value, and diagnostics show the effective value.
- A definite startup failure may fail the run before the deadline. A timeout names the resources that were not ready and preserves diagnostics.
- Test data is isolated: tests never depend on data left by other tests and never touch staging or production application data.
- One environment serves a test suite or a compatible batch.

**Lifecycle**

| Outcome | Local | CI |
| --- | --- | --- |
| Tests pass | Clean up (AD-10) | Clean up |
| A test fails | Retain for debugging until the developer explicitly stops it | Clean up |
| Startup fails or times out | Retain the surviving resources and diagnostics | Clean up, including partial provisioning |
| Explicit cancellation | Stop the run and clean up only the resources it created; an Aspire environment started separately keeps running | Clean up |

- Cleanup removes only the run's own resources, never those of another active run or a hosted environment.
- Test results and diagnostics stay available after cleanup; a live environment is never the only record of a failure.
- Test-result handling is separate from environment ownership, so normal fixture disposal must not undo the retention of failed local environments. Diagnostics are captured before cleanup.

## CAP-5 — Isolated tests without Platform

- Isolated tests run without starting Platform or the configured domain-module servers.
- CI runs isolated tests first and real-service integration tests afterwards. Passing isolated tests alone never satisfies a module's integration acceptance.
- Platform environments run their configured real services regardless of any test doubles used inside isolated tests.

## CAP-6 — Critical-flow E2E staging gate

- Critical flows come from the submodules included in the release, and each submodule owns its flow definitions. Platform keeps no central list; each module supplies its list when its release checks are integrated.
- Each included submodule supplies a non-empty critical-flow declaration and required E2E checks for every declared flow. Promotion is blocked by any of:
  - a missing or invalid declaration;
  - an empty check set;
  - an unresolved flow-to-test mapping.
- The required E2E tests exercise the candidate release deployed in staging, against real services.
- A failed or skipped required test, or one with no completed passing result, blocks promotion; an absent result is not a pass.
- Isolated tests, integration tests or health checks alone never satisfy this gate.
- Passing results must belong to the release being promoted; results from an earlier release do not count.
- The spine adds (RRA Staging gate):
  - an upgrade from the production baseline to the candidate;
  - an AD-15 rollback rehearsal;
  - the McpCli candidate's flows;
  - a maximum evidence age;
  - bound profile and configuration digests.

## CAP-7 — Production deployment verification

**Smoke suites**

- Each submodule provides its required smoke tests.
  - Read-only checks are preferred.
  - Necessary writes use dedicated synthetic data, with no changes to real users and no external effects such as notifications.
  - Tests must not assume event records can be deleted afterwards.
- Staging E2E suites are never copied blindly into production.

**Before the update**

- Platform validates the readiness and smoke declarations of every included submodule. A missing or invalid declaration, or an empty required check set, blocks the update; finding no checks is not a pass.
- Platform identifies the previous working release and its compatible versioned configuration.
- If production is already unhealthy, the update stops for investigation. First deployments are handled under CAP-8.

**Failure triggers**

- Required services have not reached the intended release and become ready within 10 minutes of deployment start.
- During the 5-minute verification window after rollout readiness, a required service cannot serve traffic for 60 continuous seconds.
- A required smoke test fails twice in a row, with the second attempt 30 seconds after the first failure.
- A required check has no completed passing result at the deadline. Skipped or missing results never pass.
- A single failed probe or container restart does not, by itself, trigger recovery.

**Verdict**

- Verification targets the intended release, not an old replica.
- The release is working only if, at the end of the window, all required services are ready and the latest required smoke results pass, and no trigger has fired.
- These values are initial policy defaults to be validated with staging evidence. They govern deployment acceptance and are not availability guarantees.
- The spine adds sampling every 10 seconds or less, stable smoke IDs, a check cadence, a bounded grace period, and interruption rules (RRA Verification, Interruption).

## CAP-8 — Verified recovery to the previous working release (NFR-1)

**Recovery attempt**

- Platform makes one automatic recovery attempt to the recorded previous working release and its compatible configuration, including any partially updated workloads. If no workloads changed, it keeps the existing release and reports the failed attempt.
- Recovery has 10 minutes to restore readiness, followed by the same 5-minute window and thresholds as CAP-7.
  - It uses the restored release's recorded production-safe smoke suite, not checks for features that exist only in the failed release.
  - A completed rollback command alone does not prove recovery.

**Reporting and stop conditions**

- Platform reports the deployment failure and the recovery result, and preserves diagnostics.
- It notifies Administrator through GitHub with the release, environment, recovery status and location of diagnostic evidence.
- Further automatic promotions stop until Administrator intervenes.
- If recovery fails, or cannot be verified because the cluster is unreachable, Platform reports it as failed or unverified. It never cycles through older releases.
- A first deployment has no previous working release. A failure stops it and is reported without claiming a rollback; ingress stays closed (RRA Automatic recovery).
- Failures found after the verification window are operational incidents, outside this policy.

**NFR-1: data safety**

- Rollback preserves business data, event history and credential rotations. It restores only application code and compatible versioned configuration.
- Releases on the automatic path keep data schemas and newly written event formats compatible with the previous working version. This compatibility is verified before automatic promotion.
- An incompatible change needs a separately planned release and recovery procedure (Administrator-approved mode).
- Disaster recovery (CAP-9) has its own data-loss targets; they never allow application rollback to rewind data.

## CAP-9 — Production backup and disaster recovery (NFR-2)

**Coverage**

- Backups cover the authoritative databases, files and configuration identified by each deployed module.
  - EventStore history alone is not assumed to be enough to restore every module.
  - Any reconstruction of derived state needs a verified procedure from the module, and its duration counts toward the RTO.
- The recovery inventory includes shared dependencies: the existing Keycloak and the production access configuration. Each has a recovery owner, confirmed by Administrator, and evidence that it survives or can be restored.

**Cadence and storage**

- Backup or incremental-copy runs start every 30 minutes.
- Retention keeps frequent recovery points for 7 days and daily points for 30 days, including the base backups and incremental chains they depend on.
- Recovery points, and the access and decryption material they need, stay reachable without the failed primary infrastructure.
- Backups are encrypted and access-restricted. Surviving whole-site loss also requires a copy at an independent location.

**Monitoring**

- Platform notifies Administrator through GitHub when a backup job fails or the newest usable recovery point is more than 1 h old.
- Monitoring measures the age of the newest usable recovery point; a scheduled or completed job is not proof of recoverability.
- Reporting keeps working while the primary environment is down.

**Restore verification**

Before service reopens, the restore verifies:

- compatible application and configuration versions;
- data integrity across modules (independently timed snapshots do not prove a coherent state);
- the restored release's module-owned smoke tests;
- access for an explicitly authorized production user, and denial for staging-only users and staging application credentials.

**Exercises and targets**

- An isolated restore exercise runs before production use (G2), monthly, and after material storage or backup changes. It records the age of the recovered data and the restore time, including operator-response and capacity-provisioning assumptions.
- **RPO ≤ 1 h:** the age of the newest usable recovery point at the time of failure. This accepts losing up to 1 h of committed data.
- **RTO ≤ 4 h:** from outage to verified restoration, including detection, operator response, replacement capacity, restore and validation.
- The recovery procedure identifies where replacement compute and storage come from and the response arrangements.
- These are targets, not guarantees. If the evidence does not support one, add the missing capability or revisit the target with Administrator.
- There is no uptime percentage and no 24/7 response commitment.
- **Mechanism guidance:** use database-native backup and incremental or log-archive mechanisms suited to the actual storage engines. Improve those native capabilities before building a custom backup engine or custom replication for a tighter RPO. The spine treats native backup as a seed; blanket volume restore is ruled out.
- The spine adds the DR sequence, the recovery-point definition, Memories erasure continuity and the lost-window exception (RRA rows).

## CAP-10 — Isolated staging and production (NFR-3)

- The MVP module set is reachable in both environments through its supported interfaces; in production, only from G2 onwards.
- Staging and production use separate application data and credentials, even where a supporting service or its capacity is shared.
- Environment configuration and promotion preserve these boundaries. Deploying a release never copies staging data or credentials into production.
- **NFR-3:**
  - Staging application credentials cannot reach production data or credentials.
  - Enforcement holds at service and data boundaries, including direct requests that bypass a UI.
  - Verification combines attempts as staging-only users and with staging application credentials with a positive production-user test. The spine adds negative tests from staging pods and for Dapr and hostnames (AD-8).

## CAP-11 — Explicit production-user access

- A staging-only user cannot reach production data or operations through any supported interface, including direct API, CLI and MCP access.
- Declaring someone a production user grants only their assigned production permissions, not unrestricted access.
- A user authorized in both environments uses each with that environment's permissions.
- Staging membership and permissions never automatically create production membership or permissions, including during promotion.
- Authenticating with the shared Keycloak alone never grants production access.

## CAP-12 — Module operations through McpCli

- The available operations come from the enabled modules' definitions. Platform keeps no business implementations of its own.
- CLI and MCP access work against local Aspire and hosted staging and production, routing through EventStore (the gateway) in the selected environment.
- An invocation runs only in the selected environment, and that environment enforces permissions for both commands and queries. Choosing an environment or interface grants nothing extra.
- A staging-only user cannot run production operations through McpCli. A production user can run only the operations their production permissions allow.
- Operations of a disabled module are unavailable. Invoking one does not enable the module or route the call to another environment.
- McpCli executes only operations their module declares agent-eligible. UI-only and confirmation-required operations are refused through McpCli, even for a user permitted to perform them elsewhere; they remain available only through the module UI (AD-14).
- Each public client has a negative test proving the restricted operations are refused (AD-14).
