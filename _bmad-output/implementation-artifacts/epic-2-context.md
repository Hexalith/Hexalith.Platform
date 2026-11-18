# Epic 2 Context: Test modules against real services, locally and in CI

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give developers and CI real-service test environments whose lifecycle belongs to the Platform runner (Builds' `hexalith-module`). Failed local environments stay for debugging, disposable CI environments always clean up, and package-mode integration blocks CI after isolated tests. The epic also keeps Platform's CI on the candidate's current tuple and makes Builds and owner-module adoption a released-version bump, without weakening candidate or lifecycle evidence.

## Stories

- Story 2.1: Decide the candidate and attachment policy
- Story 2.2: Define the test environment descriptor and result contract
- Story 2.3: Make local composition resources run-scoped
- Story 2.4: Provision a run-owned environment for a test suite
- Story 2.5: Apply outcome, retention and cleanup rules to local runs
- Story 2.6: Attach a run to a compatible environment
- Story 2.7: Withdraw attached runs and keep failure retention
- Story 2.8: Run CI integration on disposable runners with guaranteed cleanup
- Story 2.9: Migrate EventStore and Parties test fixtures to the environment descriptor
- Story 2.10: Qualify the runner lifecycle on the reference composition
- Story 2.11: Supervise attached runs and report the missing lifecycle producers
- Story 2.12: Provision a run-owned workload-binding authority for disposable test environments
- Story 2.13: Retire the historical tuple-replay lanes from Platform CI
- Story 2.14: Pin Builds and owner modules by released version

## Requirements & Constraints

- **Real services:** integration tests use real declared services. Doubles belong only in isolated tests, which run first without Platform. Passing isolated tests never satisfies integration acceptance. The reference composition is Parties with EventStore, Tenants and Memories.
- **Isolation:** each suite or compatible batch gets a fresh run-owned environment with run-scoped ports, volumes, catalog and data. A conflict fails fast and names the blocking run. Tests never touch staging or production data.
- **Readiness:** services report usable readiness and startup tasks succeed; a missing readiness definition is a configuration error. The default startup deadline is 10 minutes, excluding tests; an override must be finite and justified. Record the effective deadline, actual duration, any override and unready resources.
- **Outcomes:** record the first terminal outcome (passed, failed, cancelled or withdrawn) immediately. A withdrawn run is neither a pass nor a failure and never counts as evidence.
- **Local runs:** success cleans up once holds end. An owner or attached failure retains the resources until explicit cleanup, even after a later cancellation. Owner cancellation or an explicit stop withdraws attached runs before teardown. An Aspire environment the developer started separately keeps running.
- **CI:**
  - Disposable Linux GitHub-hosted runners with no hosted-environment credentials and only a read-only registry pull credential.
  - Attachment stays within one job, and attached runs end before cleanup, including on cancellation and partial startup.
  - The job's always-run cleanup step stops its owned runs and reports leftovers. That step is the cleanup record, and runner disposal is the backstop. No dedicated hosted scenario workflow is needed to prove it.
- **Cleanup:** idempotent, limited to owned resources, reports every leftover and is never recorded complete while resources remain. Redacted results and diagnostics survive teardown.
- **Lifecycle scenarios:** success, failure, startup timeout, cancellation, collision, owner finish or cancellation before an attachment ends, attached failure, hold expiry, and failure followed by cancellation. Each record binds the source revision, environment, result, startup duration, any override and diagnostics, and shows that no other environment was touched.
- **Evidence and specs:**
  - Evidence is a compact summary plus links to CI runs and artifacts, with no bulk logs or archives in git. Specs record decisions and current state.
  - Outside release CI, one re-run of a failure that does not reproduce is allowed; log the flaky test as a bug instead of adding gates.
- **Workload-binding trust:** generated trust exists only in disposable run-owned environments, bound to the run's caller cohort and tenant and expiring with it. Other enrollment stays with the realm owner. Evidence holds no credentials, grants or keys.

## Technical Decisions

- **Ownership:** Platform owns the single Aspire model, the lifecycle contracts and acceptance; Builds implements the runner and the reusable workflows. Fixtures read the versioned descriptor and never start an AppHost. A technical module's own-repository host is never Platform evidence. No DSL or environment service is added.
- **Modes:** local runs use active-root source in Debug; CI uses Release, NuGet packages and released images. Only direct `references/` submodules are initialized, with no fallback.
- **Descriptor and result:**
  - They bind run and owner identity, composition and mode, endpoints, catalog, synthetic tenant, expected and loaded identities, the candidate or baseline-only label, holds and outcome.
  - An unsupported contract version fails and names the supported versions.
  - Candidate artifacts are identified by content, not version label. A missing, substituted or uncommitted input fails closed, and a baseline-only run never qualifies.
- **Policy v1:**
  - Never reuse test results across acceptance decisions.
  - Attach only to compatible, ready local or CI run-owned environments. Refuse hosted targets, and refuse a finished owner unless its local environment is retained after a failure.
  - A hold keeps its original absolute expiry across retries. The runner stops attached work and access before owner teardown. A failure to stop stays visible and never extends a hold.
- **Campaigns:** pin inputs and run focused resource-free checks first. Historical evidence is re-verified only when a decision consumes it. Lifecycle cancellation is already proven by 2.8's hosted runs and Builds' lifecycle tests.
- **Platform tuple CI:** only source resolution and the current-tuple lane run, on the candidate commit, behind a fail-closed gate. A change needs no companion commits or extra refs.
- **Version pins:**
  - The Builds binding holds only the version and release tag, matching `.config/dotnet-tools.json`. NuGet lock files carry content hashes, and CI restores in locked mode.
  - Composition source has no release-identity literals, no declaration-hash or image-digest allowlists and no measured cohorts. Tool admission follows a version compatibility rule.
  - Each test project declares one package profile, which retires `platform-current-package`.
  - Adopting a release means a version bump plus regenerated lock files. Evidence still binds the candidate revision and compares the expected identities, taken from the lock files, with the loaded ones.

## UX & Interaction Patterns

Listings show owner, age, failed and attached runs, and absolute hold deadlines. Refusals state the incompatibility or name the blocking run.

## Cross-Story Dependencies

- The policy (2.1), descriptor (2.2) and run-scoped resources (2.3) come before fixture migration, attachment and CI acceptance.
- **2.8** is closed on its recorded hosted Parties runs. Do not dispatch more 2.8 runs or re-prove it on newer tuples. Its closure grants no candidate, SM-3 or tool acceptance.
- **2.9** is done; a regression gets a bounded owner fix.
- **2.11** supplies the lifecycle producers but grants no qualification.
- **Order 2.13 → 2.14 → 2.10.** 2.10 takes its tool acceptance and pins from 2.14's version rule, and its local scenarios can run first. Its CI part counts 2.8's runs plus Parties' normal CI through the shared CiCd workflow, so it also waits for CiCd story 21.
- **2.12** unblocks Folders Story 12.2. A run-owned enrollment is never staging evidence.
- **Epic 3** populates the loaded-identity and candidate fields under 2.1's policy and repeats 2.10's scenarios after any lifecycle change. **Epic 4** does not depend on 2.8 or 2.10.
