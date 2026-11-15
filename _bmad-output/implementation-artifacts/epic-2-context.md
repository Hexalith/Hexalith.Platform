# Epic 2 Context: Test modules against real services, locally and in CI

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give developers and CI real-service test environments whose provisioning, readiness, isolation and cleanup belong to the Platform runner. Preserve failed local environments for debugging, clean disposable CI environments, and make package-mode integration a blocking tier after isolated tests. Keep Platform's own CI focused on its candidate's current tuple, while preserving trustworthy source and artifact identity evidence.

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

- Integration uses real declared services; doubles belong only in isolated tests. Passing isolated tests never satisfies integration acceptance. Parties is the reference composition with EventStore, Tenants and Memories.
- Fresh environments isolate ports, volumes, catalogs and data per suite or compatible batch. Conflicts fail fast and name the blocking run. Tests never reach staging or production application data.
- Readiness means usable service readiness and successful required startup tasks. Missing readiness definitions fail configuration. The default startup deadline is ten minutes, excluding test execution; finite overrides need recorded justification. Preserve effective deadline, actual startup duration, overrides and unready-resource diagnostics.
- Record the first terminal outcome immediately: passed, failed, cancelled or withdrawn. Withdrawal is neither pass nor failure and cannot qualify integration evidence.
- Local success cleans after attachments end. Any local test/startup failure retains surviving resources until explicit cleanup, including after later cancellation. Owner cancellation or explicit stop ends attached runs before owned-resource teardown. Separately developer-started environments remain running.
- CI uses disposable Linux GitHub-hosted runners, isolated tests first, then blocking package-mode integration. It has no hosted provider credentials; registry access is read-only pull. Same-job attachments end before cleanup, including cancellation and partial startup.
- Cleanup is idempotent, removes actual owned resources and reports every leftover. Incomplete cleanup cannot be recorded complete. Classified, redacted results and diagnostics survive teardown.
- Qualification proves ownership, retention and cleanup across success, failure, startup timeout, cancellation, collision, owner finish/cancel before attachments end, attached failure, expiry and failure followed by cancellation. Every observation binds its source, environment, result, timing, diagnostics and preservation of other environments.
- Generated workload-binding trust is limited to disposable run-owned environments, their final caller cohort and synthetic tenant, with validity bounded by the run. Persistent/shared, staging and production enrollment remains realm-owner work. Evidence retains no credentials, grants or keys.

## Technical Decisions

- Platform owns one Aspire application model, lifecycle contracts and acceptance; Builds implements `hexalith-module` and reusable workflows. Shared fixtures consume the versioned descriptor rather than starting AppHosts. Technical modules' private own-repository hosts do not supply Platform evidence. No new DSL or environment service is introduced.
- Local mode uses active-root Debug sources. CI uses Release/NuGet boundaries and released dependency images. Initialize only directly declared root submodules, never recursively; no sibling, ancestor or package fallback replaces required source.
- Descriptors/results bind run/owner identity, composition/mode, endpoints, catalog, tenant, expected and loaded artifact identities, candidate or baseline-only label, holds, outcome and diagnostics. Candidate evidence must match the declared committed revision across every consumed candidate artifact, including composed-host packages and McpCli. Missing, substituted or uncommitted inputs fail closed; baseline diagnostics do not qualify candidates.
- Follow the shared candidate-and-attachment policy. Do not reuse results across acceptance decisions. Attach only to compatible, ready, isolated local/CI run-owned environments with matching artifact identities; refuse hosted and cross-job targets. Finished owners accept no new attachments except failure-retained local environments.
- Finite attachment holds preserve their original absolute expiry across retries. The runner owns attached work and access, stopping both before owner teardown on withdrawal. Stop failures remain visible and cannot extend holds or qualify withdrawn work.
- Campaigns bind exact source, tool/catalog, artifact and suite/configuration identities. Each service job retains its own cleanup proof; cancellation cases use separate real workflow runs. Old observations retain their original subjects.
- Platform tuple CI uses only source resolution and the current tuple on the candidate commit, with a fail-closed combined gate. Historical replay supplies no acceptance credit and no longer requires companion revisions or extra refs. Archived qualification and broker-candidate tags remain preserved.
- Release-version adoption and lock-file content integrity must retain expected-versus-loaded evidence. Simplifying pins does not weaken candidate identity or lifecycle acceptance.

## UX & Interaction Patterns

Retained/pending-cleanup listings show owner, age and failed/attached runs. Hold deadlines and cleanup leftovers remain visible. Refusals explain incompatibility or identify the blocking run; diagnostics identify unready resources and effective startup limits.

## Cross-Story Dependencies

- Candidate policy, descriptor contracts and resource isolation precede fixture migration, attachment and CI acceptance.
- Story 2.9 remains complete; demonstrated regressions receive bounded owner fixes. Story 2.8 supplies focused CI proofs without granting full lifecycle or tool acceptance. Story 2.11 supplies missing producers; Story 2.10 qualifies a fixed released input set before accepting and pinning the tool.
- Story 2.12 unblocks Folders 12.2 real-provider acceptance without replacing persistent/staging enrollment.
- Story 2.13 precedes version-pin simplification in 2.14 and remaining Platform adoption in 3.2. Historical records remain evidence for their original inputs; remaining lifecycle and candidate qualification stay required.
