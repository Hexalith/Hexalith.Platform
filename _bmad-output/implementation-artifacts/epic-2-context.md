# Epic 2 Context: Test modules against real services, locally and in CI

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give developers and CI real-service integration environments whose lifecycle belongs to the Platform runner (Builds' `hexalith-module`). Environments are run-scoped, readiness-gated and data-isolated. Attachment works under finite holds with an honest withdrawn outcome. Failed local environments are kept for debugging, and disposable CI environments always clean up and report leftovers. Isolated tests run first without Platform, and the package-mode integration tier blocks CI. The epic also does four more things:

- It publishes the one candidate-and-attachment policy that every evidence consumer follows.
- It gives disposable runs their own workload-binding authority.
- It keeps Platform CI on the candidate commit's current tuple.
- It turns adoption of a Builds or owner-module release into a version bump.

None of this may weaken lifecycle or candidate evidence. Candidate artifact-identity evidence follows in Epic 3.

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

- **Real services:** integration tests use real declared services. The reference composition is Parties with EventStore, Tenants and Memories. Doubles live only in isolated tests, and passing isolated tests never satisfies integration acceptance.
- **Isolation:** each suite or compatible batch gets a fresh run-owned environment with run-scoped ports, volumes, catalog and data. A conflict fails fast and names the blocking run. Tests never touch staging or production data.
- **Readiness:**
  - Declared readiness and startup tasks must succeed. A running process is not readiness, and a missing readiness definition is a configuration error.
  - The default startup deadline is 10 minutes, excluding tests. An override must be finite and justified.
  - Record the effective deadline, actual duration, override and unready resources. A longer timeout is never reported as faster startup.
- **Outcomes:** the first terminal outcome (passed, failed, cancelled or withdrawn) is recorded immediately and never overwritten. A withdrawn run is neither a pass nor a failure, and it never counts as evidence.
- **Attachment:**
  - Attachment is explicit and only to a ready local or CI run-owned environment. It must match on composition, mode, expected and loaded artifact identities, readiness and data isolation.
  - Hosted and separately started Aspire environments are refused. A finished owner is refused unless its local environment is retained after a failure.
  - CI attachment stays in the owner's job. Cancelling an attached run ends only that run.
- **Hold rules:**
  - A hold starts at acceptance. An omitted request gets **1,800 s**. An explicit request must be **whole seconds, 1–14,400**, and is honored exactly.
  - Zero, negative, fractional, null, empty, malformed, non-finite or over-limit requests are rejected before attachment, **never clamped or defaulted**.
  - Record `acceptedAt`, the request (omission stays distinguishable), `effectiveHoldSeconds` and `expiresAt` as an **absolute UTC timestamp**, and show it.
  - Expiry is exclusive and decided by the runner's UTC clock and record. The original expiry survives retries and reconnects, and a stopping failure never extends it. At expiry the attached run is withdrawn.
- **Local runs:**
  - Success cleans up after holds end.
  - Any owner or attached failure retains the environment until explicit cleanup, even after a later cancellation.
  - Owner cancellation or an explicit stop withdraws attached runs before teardown.
  - A separately started Aspire environment keeps running.
- **CI:**
  - CI runs on disposable Linux GitHub-hosted runners with no staging or production credentials. Its only registry access is a read-only pull credential.
  - Attached runs end before cleanup, including on cancellation and partial startup.
  - The always-run cleanup step and its leftover report are the cleanup record, and runner disposal is the backstop. No dedicated hosted scenario workflow is needed.
- **Cleanup:** idempotent and limited to owned resources. It removes resources, not just metadata, reports every leftover and is never complete while any remain. Redacted results and diagnostics survive teardown.
- **Lifecycle scenarios:**
  - The scenarios are success, failure, startup timeout, cancellation and collision; owner finish or cancellation before an attachment ends; attached failure; hold expiry; and failure followed by cancellation.
  - Each record binds the source revision, environment, result, actual startup duration, any override and diagnostics, and shows that no other environment was touched.
- **Evidence:**
  - Keep a compact summary plus CI run and artifact links, with no bulk logs or archives in git.
  - Historical evidence is re-verified only when a decision consumes it.
  - Outside release CI, a non-reproducing failure may be re-run once. Log the flaky test as a bug instead of adding gates.
- **Run-owned workload-binding authority:**
  - It runs as a disposable **Keycloak 26.6.4** with the provider, over HTTPS. It is configured **only by a startup realm import, with no admin user and no admin API call**.
  - It enrolls exactly the consumer-derived inventory for the run's final caller cohort and tenant, valid only for the run.
  - Owner-only manifest and secret files go in the run directory, and the run supplies `Folders:WorkloadEnrollment:ManifestPath` and `ManifestSha256`. The unchanged consumer verifies TLS, signer and probes before catalog capture.
  - TLS uses default certificate validation with **trust supplied at process launch**. Missing trust fails closed with a named precondition, and no bypass exists.
  - Persistent, shared, staging and production authorities, and foreign-run manifests, are refused.
  - Cleanup removes the container, manifest, secrets and certificates. Evidence is labelled `run-owned enrollment` and holds no credentials, grants or keys.

## Technical Decisions

- **Ownership:** Platform owns the single Aspire model, the lifecycle contracts and acceptance. Builds implements the runner and reusable workflows. Fixtures read the descriptor and never start an AppHost. A technical module's own-repository fixture is never Platform evidence. No DSL or environment service is added.
- **Modes:** local runs use active-root source in Debug. CI uses Release, NuGet packages and released images. The pinned tool alone sets the mode, and only direct `references/` submodules are initialized, with no fallback.
- **Descriptor and result contract:**
  - The contract is versioned. It binds run and owner identity, composition and mode, endpoints, the catalog, the synthetic tenant, expected and loaded identities, the candidate or baseline-only label, holds and outcome.
  - An **unsupported contract version fails, naming the supported versions**.
  - Epic 3 fields are defined and marked not-yet-populated. An unpopulated required field never qualifies.
- **Candidate policy v1:** the policy is the immutable `hexalith.platform.candidate-and-attachment-policy.v1` record in `docs/contracts/candidate-and-attachment-policy-v1.md`.
  - A merge decision tests the committed merge result **bound to the exact PR-head/base SHA pair**, rechecked at merge. A changed pair needs a fresh run, and head-only evidence never qualifies.
  - A release decision tests the exact committed release revision in a fresh run.
  - Results are never reused across decisions, even for identical bytes or unchanged modules. Only the latest attempt for a pending decision qualifies.
  - Candidate evidence comes only from runner-owned package-mode CI on disposable runners, either the owner run or a same-job attachment. Local and baseline-only runs are diagnostics only.
  - Evidence binds the declared and executed suite and configuration and a fully completed, non-empty required test set.
- **Platform tuple CI:** only source resolution and the current-tuple lane run, on the candidate commit, behind a fail-closed verdict. No companion commits or extra refs are needed.
- **Version pins (2026-11-18 version-rule design):**
  - The Builds binding holds only the version and tag. The tag resolves to the Builds gitlink. The binding version, the `.config/dotnet-tools.json` pin and the range floor must match.
  - AppHost and Tooling must report the same version within `[pin, next major)`. Newer same-major versions are admitted. Older versions, the next major, and split or unparseable pairs are refused. The tool package is verified against its generated lock.
  - Owner packages are checked against `eng/owner-packages.lock.json` (ID, version and content hash), and each loaded assembly against its archive member. CI restores in locked mode. Any mismatch fails before the run creates resources.
  - Composition has no release-identity literals, no declaration-hash or image-digest allowlists and no measured cohorts. One package profile per test project retires `platform-current-package`.
  - Adoption is a version bump plus regenerated locks, with no C#, verifier or doc edit. Evidence still binds the candidate revision, expected identities from lock-verified artifacts and measured loaded identities.

## UX & Interaction Patterns

Listings show owner, age, failed and attached runs, and absolute UTC hold deadlines. Refusals state the incompatibility, the blocking run or the missing precondition.

## Cross-Story Dependencies

- **Completion rule:** a story tagged with another repository is done only when it is merged upstream, released (a tool as a Platform-accepted version), pinned or declared in Platform, and verified from the Platform workspace.
- **Foundations:** 2.1, 2.2 and 2.3 precede attachment, fixture migration and evidence. Accepting attachments or CI evidence also needs Builds adoption, Platform tool acceptance and integration qualification.
- **Closed stories:**
  - 2.8 closed on recorded hosted Parties runs: the matrix on `566b9bfc` and the cancellations on `926c1c3f`, all on Builds 4.44.13. Don't re-prove them. That closure grants no candidate, SM-3 or tool acceptance.
  - 2.9 is done, and a regression gets a bounded owner fix.
  - 2.11 supplies producers but grants no qualification.
- **Order 2.13 → 2.14 → 2.10:**
  - 2.10's local scenarios run now on the pinned released tool.
  - Its CI part waits for 2.14 and CiCd story 21. It counts 2.8's runs plus normal Parties CI success and failure through the shared workflow.
  - Platform acceptance and the Platform and Parties pins follow 2.14's version rule.
- **2.14 audit follow-up (November 19 audit, action 1):**
  - Tuple preflight passed on `f3f96b2e`. The `707cf901` submodule sync then reintroduced drift in all five revisions (Builds, EventStore, Parties, Tenants, Memories), and `read_selection` stops at EventStore.
  - Before a new candidate is published, the release owner selects the five revisions. Platform aligns the submodules to the approved publications, or validates the upgrades and updates their bindings together.
  - On one corrected commit, pass `read_selection`, the Builds publication and gitlink checks, `verify_checkout`, and `current42`, `base42` and `extensions` with zero skips. Link the run to that commit.
  - The repair reopens neither 2.8 nor the frozen Builds brief.
- **2.12 boundaries:**
  - 2.12 unblocks Folders 12.2 and reuses the existing provider, consumer and `verify_component.py`.
  - The realm owner installs the provider and enrolls persistent, staging and production authorities. For Folders, that work is Story 10.1: Administrator enrolls per the runbook, and run-owned enrollment is never staging evidence.
  - The enrollment runbook names 2.12's path as the only generated-trust activation, limited to disposable run-owned environments. Its Folders 12.2 paragraph points there.
- **Later epics:** Stories 3.1 and 3.9 apply policy v1 and populate the loaded-identity and candidate fields. Story 3.12 repeats 2.10's scenarios after lifecycle changes. Epic 4 does not depend on 2.8 or 2.10.
