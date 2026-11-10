# Epic 2 Context: Test modules against real services, locally and in CI

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give developers and CI real-service integration environments that only the `hexalith-module` runner provisions, owns and cleans. Attachment uses finite holds with an honest `withdrawn` outcome. Local failures are kept for debugging, and CI always cleans up and reports leftovers. Isolated tests run first without Platform, and package-mode integration blocks CI. The epic proves this lifecycle on the Parties reference composition. It also publishes the candidate-and-attachment policy that Epic 3 consumes.

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

## Requirements & Constraints

- Environments run the declared minimum services for real; for Parties, these are EventStore, Tenants and Memories. Doubles exist only inside isolated tests, which run with no Platform or domain server and never satisfy integration acceptance.
- Each suite or compatible batch gets a fresh run-owned environment, with run-scoped ports, volumes, catalog and data. Otherwise it fails fast, naming the blocking run. Tests never reach hosted data.
- Readiness means module-defined readiness plus successful startup tasks; a running process does not count. Startup defaults to a 10-minute deadline, which excludes execution. An override must be finite, justified and shown. A timeout names the unready resources. Record the actual duration, the effective deadline and any override, and never present a raised timeout as an improvement.
- Record a run's first terminal outcome immediately: `passed`, `failed`, `cancelled` or `withdrawn`. A `withdrawn` run is neither a pass nor a failure and never counts as evidence.
- Locally, success cleans up after holds end. Any owner or attached test or startup failure keeps the surviving resources until explicit cleanup, even after a later cancellation or expiry. A stop or owner cancellation withdraws attached runs first, then removes only resources the owner created. A separately started Aspire environment keeps running.
- CI runs on disposable Linux GitHub-hosted runners and always cleans up, including after partial provisioning. A run is never counted clean while it still owns resources. Attachments stay within the same job and end before cleanup. CI holds no hosted credentials; its only registry access is a read-only pull.
- Cleanup is idempotent and limited to owned resources, and it removes resources rather than just metadata. It reports leftovers and is never recorded complete while anything remains. It never touches another environment, or one still serving an attachment within its hold. Results and redacted diagnostics survive cleanup.
- Qualification runs locally and in CI and covers these scenarios: success, failure, startup timeout, cancellation, collision, owner finish or cancel before an attachment ends, attached failure, hold expiry, and failure followed by cancellation. Each record holds the revision, environment, result, startup duration, any override and diagnostics, plus proof that no other environment was touched. Only runner-owned runs count.

## Technical Decisions

- **Ownership:** Platform owns the contracts and acceptance; Builds implements them in `hexalith-module` and the reusable workflows. Fixtures (EventStore.Testing(.Integration), module and McpCli tests) consume the versioned descriptor and never start an AppHost. A module's non-exported own-repository fixture is never Platform evidence. A shared AppHost path is not ownership. There is no custom DSL or environment service.
- **Modes:** locally, Aspire runs Debug sources from the active checkout; CI uses package mode (Release builds, NuGet boundaries, catalog-pinned images).
- **Contract:** binds these identities:
  - run, owner, environment and CI job;
  - composition and mode, endpoints, the run-scoped catalog and the synthetic tenant;
  - expected and loaded artifacts, and the candidate or `baseline-only` label;
  - holds, outcomes, the executing tool, termination, cleanup and diagnostics.

  An unsupported version fails, naming the supported versions. Epic 3's fields already exist, marked not yet populated, and a missing value never qualifies evidence.
- **Policy v1** (immutable): PR evidence binds the committed merge result to its exact head/base pair, and any change needs a new run. Evidence is never reused across decisions; reuse would need a new policy version, adoption and requalification. An unknown or mismatched policy ID fails closed. Local and baseline runs are diagnostic only.
- **Attachment:** attachment is explicit. The target must be a compatible local or CI run-owned environment, matching on composition, mode, identities, readiness and isolation. Hosted, developer-started or cross-job targets are refused with the reason. After the owner finishes, attachment is refused, except to a failure-retained local environment, until it is stopped or cleaned up. Cancelling an attached run ends only that run.
- **Holds:** an omitted hold defaults to 1,800 s. An explicit hold must be whole seconds from 1 to 14,400 and is applied exactly; any other value is rejected, never clamped. Record the request, acceptance time, effective seconds and absolute UTC expiry. Runner-issued lineage keeps the original expiry across retries.
- **Supervision:** the runner owns attached child work and per-attachment access. On expiry or withdrawal, it stops that work and ends access before owner teardown, recording `withdrawn` without changing the first outcome or the expiry. A stop failure is reported, and it neither extends the hold nor grants qualification.
- **Campaigns:** bind exact revisions, tool and catalog identities, digests and suite configuration. Each service job keeps its own cleanup proof, and cancellation cases use separate real workflow runs. Never carry one tool version's observations to another.

## UX & Interaction Patterns

Listings of retained and pending-cleanup environments show owner, age, failed and attached runs, and absolute hold deadlines. Refusals name the reason or the blocking run.

## Cross-Story Dependencies

- Story 2.1's policy and 2.2's contract come before attachment, fixture migration and CI evidence. Story 2.3's run-scoped resources underpin 2.4–2.8.
- Story 2.9 is done, and 2.8 consumes it, so do not redo the migration. A regression gets a bounded fix, reviewed by the EventStore or Parties owner.
- Story 2.8 is limited to the CI runner/workflow contract and its six edge-case rows, and it grants no candidate, lifecycle or tool acceptance.
- Story 2.10 is blocked. The 4.47.0 campaign left rows incomplete or unexecuted because producers were missing.
  - Story 2.11 adds those producers in the Builds runner, result contract and workflow.
  - It also makes the Parties caller select these cases: collision, CI startup timeout, failure then cancellation, owner cancellation before attachment end, and attached failure or expiry.
  - Releasing 2.11 grants no acceptance.
  - Once a Builds release carries 2.11, Story 2.10 runs one complete qualification on that fixed input set. Then it records the Platform-accepted tool and pins it in Platform and Parties.
- A cross-repository story is done only when it is merged upstream, released as a Platform-accepted version, pinned, and verified from the Platform workspace.
- Epic 3 populates the loaded-identity and candidate fields. The unnumbered EventStore 4.16 qualification work (PLAT-ES416-QH) reuses this runner and lifecycle without changing the scope of Stories 2.8–2.10.
