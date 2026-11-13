# Epic 2 Context: Test modules against real services, locally and in CI

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give developers and CI disposable real-service integration environments whose provisioning, readiness, ownership and cleanup belong to the runner. Runs isolate data and resources, support explicit finite attachment, preserve local failures for debugging, and always clean CI resources. Isolated tests run first without Platform; package-mode integration blocks CI. Disposable workload-binding authorities let real-provider tests obtain enrollment for their actual callers and tenant.

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

## Requirements & Constraints

- Integration uses real declared services; Parties requires EventStore, Tenants and Memories. Doubles belong only to isolated tests, which need no Platform or domain servers and cannot satisfy integration acceptance.
- Fresh environments isolate ports, volumes, catalogs and data per suite or compatible batch; conflicts fail fast and name the blocking run. No test reaches hosted application data.
- Readiness requires module-defined usable readiness and successful startup tasks. The default startup deadline is ten minutes, excluding test execution. Any override is finite, justified and visible. Results preserve actual duration, effective deadline, override and unready-resource diagnostics; raising a timeout is not an improvement.
- Record the first terminal outcome immediately: passed, failed, cancelled or withdrawn. Withdrawal is neither pass nor failure and never qualifies integration evidence.
- Local success cleans after attachments end. Any local startup or test failure retains surviving resources until explicit cleanup, even after later cancellation. Owner cancellation or explicit stop first withdraws attached runs, then removes only owned resources; separately developer-started environments remain running.
- Disposable Linux GitHub-hosted CI runs isolated tests before blocking package-mode integration. It has no staging or production credentials; registry access is read-only pull. Cleanup covers success, failure, cancellation and partial provisioning. Same-job attachments end before cleanup.
- Cleanup is idempotent, removes actual owned resources and reports every leftover; incomplete cleanup is never complete. Redacted diagnostics and results survive teardown.
- Lifecycle qualification covers success, failure, startup timeout, cancellation, collision, owner finish/cancel before attachment ends, attached failure, expiry and failure followed by cancellation, locally and in CI. Each observation binds revision, environment, result, startup duration, override, diagnostics and proof that other environments were preserved.
- Disposable workload-binding enrollment is limited to the final run caller cohort and synthetic tenant, with validity bounded by the run. Generated trust is accepted only for disposable run-owned environments; persistent/shared, staging and production authorities remain realm-owner responsibilities. Evidence is labeled run-owned enrollment and retains no credentials, grants or keys.

## Technical Decisions

- Platform owns lifecycle contracts and acceptance; Builds implements the runner and reusable workflows. Shared integration fixtures consume a versioned descriptor and never start an AppHost. A technical module's private own-repository fixture does not supply Platform evidence. No new DSL or environment service is introduced.
- Local mode uses Debug sources from the active checkout. CI uses Release/NuGet package boundaries and catalog-pinned images.
- Descriptor/result identities bind run, owner, environment, CI job, composition, mode, endpoints, catalog, tenant, expected/loaded artifacts, candidate or baseline-only label, holds, tool, termination, cleanup and diagnostics. Unsupported contract versions fail naming supported versions. Missing candidate/loaded identity fields cannot qualify evidence.
- Candidate policy v1 binds PR evidence to the committed merge result and exact head/base pair. Changed inputs require a new run; decisions do not reuse evidence. Unknown or mismatched policy identifiers fail closed. Local and baseline-only runs are diagnostic.
- Attachment is explicit and requires compatible identities, composition, mode, readiness and isolation. Hosted, developer-started and cross-job targets are refused. Finished owners refuse new attachments except failure-retained local environments. Cancelling an attachment ends only that run.
- Holds default to 1,800 seconds; explicit whole seconds from 1 through 14,400 apply exactly, without clamping. Record acceptance and absolute UTC expiry; retries preserve original expiry. The runner owns attached child work and access, stopping both before teardown on withdrawal/expiry. Stop failures remain visible and cannot extend holds or qualify withdrawn work.
- Campaigns bind exact revisions, executing tool and catalog identities, artifact digests and suite configuration. Each service job retains its own cleanup proof; cancellation cases require separate real workflow runs. Never carry observations between tool versions. Story 2.12 enrollment evidence does not replace these campaign requirements.
- Disposable workload-binding authority uses Keycloak 26.6.4 with the existing provider and startup realm import, without an admin user/API. Derive enrollment inventory from the consumer's own rule. Owner-only manifest and secret files supply consumer configuration; its existing TLS, signer and probe verification precedes catalog capture. Default certificate validation uses trust supplied at process launch; missing trust fails with a named precondition. Cleanup removes owned authority, enrollment secrets and certificates.

## UX & Interaction Patterns

Retained and pending-cleanup listings show owner, age, failed/attached runs and absolute hold deadlines. Refusals explain incompatibility or identify the blocking run. Diagnostics show effective startup limits and cleanup leftovers.

## Cross-Story Dependencies

- Candidate/attachment policy and descriptor contracts precede attachment, fixture migration and CI evidence. Run-scoped resources underpin lifecycle implementation.
- Story 2.9 remains complete; demonstrated regressions receive bounded owner fixes. Story 2.8 supplies its six focused CI proofs; it does not grant full lifecycle, candidate or tool acceptance.
- Story 2.11 supplies missing attached-work, termination, timing and Parties caller producers. Story 2.10 then qualifies one fixed released input set and records the Platform-accepted tool and pins; old-version observations retain their original subjects.
- Cross-repository completion requires upstream merge, release, Platform acceptance/pins and workspace verification where required by the story.
- Story 2.12 unblocks Folders 12.2 strict real-provider acceptance. Persistent and staging enrollment, including Folders 10.1, remains outside the disposable producer's authority. The workload-binding enrollment runbook documents the generated-trust activation boundary.
- Epic 3 adds candidate loaded-identity evidence. EventStore append qualification reuses the same lifecycle without widening Stories 2.8–2.10.
