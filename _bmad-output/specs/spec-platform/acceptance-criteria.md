# Acceptance Criteria

Testable consequences for each capability, taken from PRD FR-N (CAP-N = FR-N) and NFR-1..NFR-3. PRD thresholds are kept verbatim. Where the spine makes a rule stricter, this file cites the spine clause instead of restating it. Record every demonstration as [success-measures.md](success-measures.md) requires.

Spine: `../../planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md` (RRA = its Release and Recovery Acceptance table).

## CAP-1 — Complete local environment

- EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories, plus their required supporting components, are available together for testing and debugging.
- Readiness follows the module-owned checks and bounded startup policy in CAP-4. The complete environment uses the largest declared startup override, reported as an override (spine Local tool and readiness).
- McpCli availability is shown by its local client connecting and executing declared agent-eligible operations. No hosted McpCli service is required.

## CAP-2 — Debug the active module checkout

- **Domain modules** (Tenants, Parties, Folders, Projects): source changes and breakpoints affect the running module from its own workspace checkout, with Platform as a direct submodule.
- **EventStore, Memories and McpCli:** they must be directly declared in Platform. Source changes and breakpoints affect the running code from their checkouts in the Platform workspace, built in Debug. A technical module's own workspace supports own-repository tests only (AD-4).
- Only the active root repository's direct `references/` declarations are initialized; nested submodules remain uninitialized. The Platform tool fails naming any initialized nested submodule (AD-4).
- The active module and directly declared Hexalith dependencies use source and Debug assets locally. Other dependencies use packages or released images at the Builds catalog version. CI/CD uses NuGet package references and Release assets.
- Missing required declared source, or a source and a package copy of one identity, fails with the dependency and path named. Nothing falls back to a sibling, ancestor, nested Platform or package (AD-4).
- The Platform tool is `hexalith-module`, pinned in `.config/dotnet-tools.json`; it alone selects the mode (AD-4 Mode selection).
- **Platform identity:** the workspace's `references/Hexalith.Platform` submodule commit, or HEAD in the Platform repository. It pins the Builds catalog commit and a tool-version range. Package mode refuses a mismatch; source mode warns (AD-4 Platform identity).

## CAP-3 — Developer-defined minimum environment

- The module developer controls the module's server list: one list for development and integration testing in the MVP.
- A domain module's minimum environment is EventStore, Tenants and Memories plus whatever else its developer declares.
- **Parties example:**
  - Platform runs the active Parties checkout with EventStore, Tenants and Memories, as the Parties declaration specifies. Unrelated domain modules are not required.
  - The Parties repository directly references EventStore, Tenants, Memories and Platform under `references/`. This is the requested arrangement; the Parties checkout does not implement it yet.
- EventStore, Memories and McpCli use module-owner-declared minimum compositions from the Platform workspace. Their technical roles need no invented domain dependencies and no separate module-workspace workflow.
- Dependencies follow the active-root direct-reference policy in CAP-2.
- Platform runs the declared servers as real services.
- Missing, duplicate or incompatible declarations fail validation (spine Module declaration).

## CAP-4 — Real-service integration tests, locally and in CI

**Composition and readiness**

- Parties integration tests run against real EventStore, Tenants and Memories, plus any other dependencies the Parties declaration selects.
- Local runs use Aspire with the module declaration and the active checkout. CI runs follow the Release/NuGet rule in CAP-2, in package mode (AD-4, AD-5).
- The runner (`hexalith-module`) owns the integration environment lifecycle. Tests consume its environment descriptor and never start their own AppHost. A technical module's own-repository tests may use its own fixture, but never count as Platform integration evidence (AD-10 Owner).
- CI uses disposable runners without staging or production credentials; its only retained-registry access is a read-only pull credential (AD-5).
- Tests start only after the selected module and its required dependencies report readiness and all required one-off startup tasks succeed. A running process alone is not readiness; a required service without usable readiness evidence is a configuration error.
- The default startup deadline is **10 minutes**, measured from the start request until all required resources are ready.
  - It excludes test execution and does not change production rollout deadlines.
  - The test configuration may override it with a justified finite value, and diagnostics show the effective value.
- A definite startup failure may fail the run before the deadline. A timeout names the resources that were not ready and preserves diagnostics.

**Run isolation and ownership**

- Test data is isolated: tests never depend on data left by other tests and never touch staging or production application data.
- Each suite or compatible batch gets a fresh run-owned environment, or fails naming the conflicting run.
- Explicit attach needs compatible composition, artifact mode, readiness and data isolation in a local or CI run-owned environment. Attaching to a hosted environment is refused.

**Lifecycle**

| Outcome | Local | CI |
| --- | --- | --- |
| Tests pass | Clean up | Clean up |
| A test fails | Retain for debugging until the developer explicitly stops it | Clean up |
| Startup fails or times out | Retain the surviving resources and diagnostics | Clean up, including resources created before provisioning failed |
| Explicit cancellation | Stop the run and clean up only the resources it created; an Aspire environment started separately keeps running | Clean up |

- The run's first terminal outcome alone decides cleanup or retention; a later cancellation never erases an environment retained after failure. Retained environments are listed with owner and age.
- Cleanup removes only the run's own resources, never those of another active run or a hosted environment.
- Cleanup can be retried safely, removes resources rather than only metadata, and reports remaining owned resources. An incomplete cleanup is never recorded as complete.
- Test results and diagnostics stay available after cleanup; a live environment is never the only record of a failure.

## CAP-5 — Isolated tests without Platform

- Isolated tests run without starting Platform or the configured domain-module servers.
- CI runs isolated tests first and real-service integration tests afterwards. Passing isolated tests alone never satisfies a module's integration acceptance.
- Platform environments run their configured real services regardless of any test doubles used inside isolated tests.

## CAP-6 — Critical-flow E2E staging gate

**Declarations**

- Critical flows come from every enrolled module in the complete release, including unchanged modules. Each module owns its definitions; Platform keeps no central list. Infrastructure and tool modules declare their essential behavior, including McpCli candidate flows, rather than being exempted.
- Each included module supplies a non-empty critical-flow declaration and required E2E checks for every declared flow. Promotion is blocked by any of:
  - a missing or invalid declaration;
  - an empty check set;
  - an unresolved flow-to-test mapping.
- Removing or remapping a critical flow needs module-owner review, and changes to declarations or checked inputs need matching evidence (RRA Staging gate).

**Evidence**

- The required E2E tests exercise the candidate release deployed in staging, against real services.
- A failed, skipped or incomplete required test, or one with no completed passing result, blocks promotion; an absent result is not a pass.
- Isolated tests, integration tests or health checks alone never satisfy this gate.
- Passing results identify the served release, the production working baseline they were computed against, the check suite, runtime profile and configuration. Stale, mismatched or wrong-release evidence is refused. Evidence has a declared maximum age, and rerun evidence never hides an earlier failed or incomplete attempt (RRA Staging gate).
- The spine adds: a staging recovery point cut before each staging attempt; an upgrade from the production baseline to the candidate; an AD-15 rollback rehearsal with HotReload off, including an idempotent command new in the candidate; a fresh-install rehearsal when production has no working baseline; and the staging reset before a later candidate when an unadopted candidate's writes are unreadable by the baseline (RRA Staging gate, Staging reset).

**Release modes**

- The gate applies in both release modes (RRA Release modes).
- Automatic promotion additionally requires G3 and passing compatibility evidence against the current production working baseline (NFR-1). Modules supply change classifications; Platform supplies the combined staging rehearsal. Missing, failing, stale or wrong-baseline compatibility evidence blocks the automatic path.
- An authenticated Administrator approval permits a pre-G3 release or a separately planned incompatible release. It never substitutes for the staging gate, artifact provenance, environment isolation or production verification. If safe automatic rollback cannot be shown, the approval names the recovery procedure and its acceptance checks before the attempt.
- A single controlled attempt owns changes in each environment: application, configuration, routing, security and recovery changes. Shared-infrastructure changes coordinate both environments. Concurrent work never replaces the release being tested or invalidates its evidence (RRA Attempt ownership).

## CAP-7 — Production deployment verification

**Smoke suites**

- Each enrolled module provides its required smoke tests.
  - Read-only checks are preferred.
  - Necessary writes use dedicated synthetic identities and data, with no changes to real users and no external effects such as notifications. Production smoke suites never create or delete tenants (spine Synthetic identities).
  - Tests must not assume event records can be deleted afterwards.
- Staging E2E suites are never copied blindly into production.
- Production runs only the immutable check-suite digests bound in the release record that passed staging, without source builds, in the module-code sandbox (RRA Verification; AD-7).

**Before the update**

- Platform validates the readiness and smoke declarations of every included module. A missing or invalid declaration, or an empty required check set, blocks the update; finding no checks is not a pass.
- Platform identifies the working baseline, verifies it is healthy now, and checks required module release qualifications and current environment and security configuration. Recovery combines the previous application with compatible current environment values.
- Any missing precondition stops the attempt before mutation and notifies (spine Production preconditions 1–9). First deployments are handled under CAP-8.

**Failure triggers**

- Required services have not reached the intended release and become ready within **10 minutes of deployment start**.
- During the **five-minute verification window** after rollout readiness, a required service cannot serve traffic for **60 continuous seconds**.
- A required smoke test fails **twice consecutively**, with the second attempt **30 seconds after the first failure**.
- A required check has no completed passing result at the deadline. Skipped or missing results never pass.
- A single failed probe or container restart does not, by itself, trigger recovery.

**Verdict**

- Availability is sampled at least every **10 seconds**. Smoke checks have stable identities and run at window start and at a declared finite cadence. A check timeout fails the check. Results bind the intended served release; results from another release count as missing.
- A bounded grace may finish an in-flight retry but never turns a missing pass at the verification deadline into success.
- The release is working only if, at the end of the window, all required services are ready and the latest required smoke results pass, and no trigger has fired.
- Interruptions never reset elapsed deadlines or add recovery attempts. An observation gap invalidates verification. Uncertainty about release identity, ownership or records stops changes pending intervention. A bounded continuation of the same attempt may use only its remaining recovery allowance (RRA Timing and interruption).
- These values are initial policy defaults to validate with staging evidence. They govern deployment acceptance and are not availability guarantees.

## CAP-8 — Verified recovery to the previous working release (NFR-1)

**Recovery attempt**

- Platform makes one automatic recovery attempt to the recorded previous working application with compatible current environment configuration. It restores application components and routing changed by the attempt, including partially updated workloads, while keeping current data and security authority.
- If neither workloads nor routing changed, Platform keeps the existing release and reports the failed attempt. Reversing a first module enrollment removes application workloads, never durable data objects.
- Recovery has **10 minutes to restore readiness**, followed by the same **five-minute verification window** and thresholds as CAP-7.
  - It uses the restored release's recorded production-safe smoke suite, not checks for features that exist only in the failed release.
  - A completed rollback command alone does not prove recovery.
- Recovery is the AD-3 recovery render of the prepared AD-15 rollback set; Helm and controller-driven rollback are forbidden (AD-3). It touches only the application-package tier (spine Release tiers).
- Approved incompatible releases use their separately planned recovery, run as an in-place recovery (RRA Release modes).

**Reporting and stop conditions**

- Platform reports every deployment failure and recovery result, including success, through GitHub to Administrator and the recovery deputy. Notifications carry the release, environment, status and location of access-controlled diagnostic evidence.
- Every non-working production outcome and disaster recovery entry sets a durable promotion stop that survives process, executor or cluster restart. Only an authenticated Administrator record naming the reason and a verified current working release clears it. The deputy may perform documented recovery, verify restoration and reopen service, but cannot clear the stop or administer production-user access (RRA Promotion stop).
- If recovery fails, or cannot be verified because the cluster is unreachable, Platform reports it as failed or unverified for intervention. It never cycles through older releases.
- After a failed, unverified or interrupted recovery, Administrator or the deputy may start an in-place recovery that re-deploys the recorded working baseline under a new epoch, with the stop kept set (RRA In-place recovery).
- A first deployment has no previous working release. A failure stops it with user ingress closed and is reported without claiming a rollback (RRA Automatic recovery; Empty or degraded production).
- A manual recovery or disaster restore becomes the working baseline only after its required verification passes. Release decisions, approvals, attempt outcomes, diagnostics and the baseline stay retrievable independently of the failed environment (spine Binding classes and records).
- Failures found after the verification window are operational incidents, outside automatic recovery. An incident showing production is no longer working sets or keeps the promotion stop until Administrator records a verified baseline; it never triggers a search through older releases.

**NFR-1: data safety**

- Rollback preserves business data, event history, credential rotations, revocations and current security authority. It restores only the previous application and compatible current environment configuration, never shared infrastructure.
- Releases on the automatic path keep data schemas, newly written events and required routing and security state compatible with the previous working version. Module change classifications plus the staging candidate-to-baseline rehearsal prove the baseline reads candidate-written state and events, including a newly introduced idempotent command where applicable.
- Keys, secrets and routing entries needed by the recovery combination are not retired before the candidate becomes working (AD-15 Expand-only).
- Missing or invalid compatibility evidence blocks automatic promotion. An incompatible change needs the Administrator-approved release and a separately planned recovery (CAP-6).
- Disaster recovery (CAP-9) has its own data-loss targets; they never allow application rollback to rewind data.

## CAP-9 — Production backup and disaster recovery (NFR-2)

**Coverage**

- Backups cover the authoritative databases, files and configuration identified by each deployed module, plus Platform's retained releases and recovery records.
  - EventStore history alone is not assumed to be enough to restore every module.
  - Modules classify state as authoritative, rebuild-only or dependent on surviving live authority, and supply restoration, reconciliation and integrity checks. Reconstruction time counts toward the RTO.
- The recovery inventory includes identity, secret and other shared dependencies: Keycloak with its event export, each environment's OpenBao, production access configuration and revocation evidence. Each has a recovery owner, confirmed by Administrator, evidence that it survives or can be restored, and, for every authority that survives a failure, a fence-and-reissue owner and procedure (RRA Backup coverage and cadence).

**Cadence and storage**

- Backup or incremental-copy runs start every **30 minutes**.
- Retention keeps frequent recovery points for **seven days** and daily points for **30 days**, including the base backups and incremental chains they depend on.
- A usable recovery point is a complete, verified cross-module set at one declared cut, with compatible release and configuration identity and required security and erasure context. Integrity, complete chains and decryption are verified when it is recorded. RPO age is measured from the declared cut (RRA Recovery point and freshness).
- Recovery points are encrypted, immutable, restricted and off-site. Required artifacts, access and decryption material stay independently available to Administrator and the deputy, with tenant-key custody separate from ordinary data backups (AD-12 Key custody).
- Prepared replacement capacity is identified and exercised. Off-site backups alone do not prove whole-site recovery.

**Monitoring**

- Independent monitoring checks the newest complete recovery point at least every **15 minutes**, warns before its age reaches one hour, and notifies Administrator and the deputy through GitHub on backup failure or age over **one hour**.
- Production availability is probed at least every **five minutes**, from G1 onwards.
- Failure reporting survives loss of the primary environment; an independent hourly check detects monitor silence (spine Diagnostics and notification).

**Restore and reopen**

- Disaster recovery fences the failed environment and restores into quarantine, following the spine's Disaster recovery sequence. Replacement-capacity DR runs on the recovery executor, started by Administrator or the deputy (AD-7).
- Before reopening: rotate restored credentials, re-apply post-cut revocations, and verify compatible application and configuration versions, cross-module integrity, the restored release's smoke tests and all NFR-3 access outcomes, including denial of revoked principals. Re-enable backups and monitoring before reopening. Environment-creation and authority-population startup tasks never rerun during restore (spine Startup task lifecycle).
- Administrator or the deputy reopens; the promotion stop stays set until staging is re-established (spine Disaster recovery sequence step 7; RRA After DR).
- Memories recovery preserves every acknowledged erasure tombstone and never resurrects erased tenants or keys; unknown authority lineage fails closed. The one-hour RPO does not relax this (RRA Memories erasure continuity).
- For other modules, deletions, erasures, legal holds and module-owned revocations acknowledged only inside the lost RPO window may be lost. The recovery runner records them in the DR report before reopening; Administrator reviews them before clearing the promotion stop. Destructive retention and external-effect workers stay disabled until module-owned reconciliation completes (RRA Lost window and external effects).

**Exercises and targets**

- An isolated restore exercise runs **before G2, monthly thereafter, and after material storage or backup changes**. It assumes primary server and storage loss and proves that detection, worst-case response within declared coverage, replacement capacity, restore and verification meet the NFR-2 targets. It records recovered-data age, full elapsed time, coverage and every substituted dependency. Drills use isolated copies and never mutate live production or shared authority (RRA Disaster recovery evidence).
- **RPO at most one hour, continuously:** failure time minus the declared cut of the newest complete usable recovery point. This accepts losing up to one hour of committed data, subject to the erasure and security protections above.
- **RTO at most four hours for outages beginning within declared response coverage:** from service outage to verified restoration, including detection, operator response, replacement capacity, restore and validation. An incident starting within coverage stays covered when the scheduled coverage ends.
- Before G2, Administrator publishes response coverage with its time zone, primary and deputy responsibility and maximum acknowledgement delay, and identifies prepared replacement compute and storage. Deputy hours count toward coverage only after the G2 deputy proof (RRA Detection and response).
- Outside declared coverage no four-hour commitment is made. Every incident still records its full outage-to-restoration duration and coverage status; the clock never pauses or restarts.
- These are targets, not guarantees. If the evidence does not support one, add the missing capability or revisit the target with the recovery owner before opening production. There is no uptime percentage and no continuous response commitment. Whole-site loss is covered only when exercised replacement capacity is at an independent location.
- **Mechanism guidance:** use database-native backup and incremental or log-archive mechanisms suited to the actual storage engines. Improve those native capabilities before building a custom backup engine or custom replication for a tighter RPO. A raw volume or snapshot copy never counts toward a recovery point (AD-12 Model).

## CAP-10 — Isolated staging and production (NFR-3)

- The MVP module set is reachable in both environments through its supported interfaces; in production, only from G2 onwards. Each module declares its external surfaces, required authorization and exposure class; a disabled or private surface is not advertised.
- McpCli runs on the caller's host and reaches the selected environment through its permitted gateway.
- Staging and production use separate application data and credentials, even where a supporting service or its capacity is shared.
- Environment configuration and promotion preserve these boundaries. Deploying a release never copies staging data or credentials into production.
- Staging workload or automation authority cannot claim production hostnames or administer production state. Resource limits protect production capacity from staging exhaustion; shared hardware does not provide node or site resilience.
- **NFR-3:**
  - Deployment, smoke-test, backup, recovery and identity-management identities have only their declared environment and purpose permissions. CI receives no hosted-environment credentials.
  - Enforcement holds at direct service, data, routing and administration boundaries, including requests that bypass a UI. Restored production copies stay restricted production data.
  - Verification rejects staging-only users, application credentials, automation credentials and pods attempting production operations, data or secret access, routing claims or identity administration, including direct requests and restored copies. It also proves permitted access for an explicitly authorized production user and denial after revocation. The spine's full negative matrix is AD-8 Negative tests.
  - Repeat the relevant checks after access changes and restoration.

## CAP-11 — Explicit production-user access

- A staging-only user cannot reach production data or operations through any supported interface, including direct API, CLI and MCP access.
- Declaring someone a production user grants only their assigned production permissions, not unrestricted access.
- A user authorized in both environments uses each with that environment's permissions.
- Staging membership and permissions never automatically create production membership or permissions, including during promotion.
- Only Administrator grants and revokes production admission, through an authenticated, auditable action. Self-registration, first login, identity mappings and staging administration cannot grant it. Recovery authority, including the deputy's, never grants production-user administration (AD-6 Admission, Administration).
- Authenticating with the shared Keycloak alone never grants production access.
- Synthetic actors are admitted only through the synthetic-admission group, limited to the synthetic tenant; the gateway never treats a synthetic flag as admission. The G1 SM-4 temporary grant is the only synthetic entry to the human production-admission group ([sequencing.md](sequencing.md#g1--production-deployed-ingress-closed); spine Synthetic identities).

## CAP-12 — Module operations through McpCli

- McpCli executes only operations their module declares agent-eligible. UI-only and confirmation-required operations are refused through McpCli, even for a user permitted to perform them elsewhere; they remain available only through the module UI (AD-14).
- The available operations come from the enabled modules' definitions. Platform keeps no business implementations of its own.
- Offline contract inspection does not establish executable availability. Connected discovery must match the selected environment's enabled operation definitions and contract versions; unknown or mismatched contracts are non-executable (AD-11 Availability).
- CLI and MCP access work against local Aspire and hosted staging and production, routing through EventStore (the gateway) in the selected environment.
- An invocation runs only in the selected environment, and that environment enforces permissions for both commands and queries. Choosing an environment or interface grants nothing extra.
- A staging-only user cannot run production operations through McpCli. A production user can run only the operations their production permissions allow.
- Operations of a disabled module are unavailable. Invoking one does not enable the module or route the call to another environment.
- Server-side checks enforce authenticated caller identity, calling surface and current permissions on every invocation. Caller-supplied actor or surface values grant nothing; a public client's token used directly against the gateway cannot bypass UI-only or confirmation restrictions (AD-14 Surface, Actor and workload).
- Each client in the surface map has a negative test proving its tokens are denied operations outside its class, directly and through one cross-module hop (AD-14 Negative tests).
- Acceptance names the enrolled operations actually demonstrated, proves allowed invocation through CLI and MCP, and proves refusal of ineligible, disabled, mismatched-contract and unauthorized operations. An empty executable catalog cannot satisfy the positive demonstration.
- In local and CI modes the Platform tool builds a run-scoped, never-published McpCli. Only the Platform publication workflow publishes stable `Hexalith.McpCli` versions, after staging validation (AD-11 Tool).
