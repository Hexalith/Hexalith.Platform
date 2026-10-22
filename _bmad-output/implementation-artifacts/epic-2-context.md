# Epic 2 Context: Test modules against real services, locally and in CI

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Provide isolated real-service testing with runner ownership, bounded readiness/attachments, local failure retention and verifiable cleanup. Isolated tests remain independent; package-mode integration blocks CI. Epic 3 adds candidate selection/loaded-artifact proof.

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

## Requirements & Constraints

- Run the declared minimum real services; Parties requires EventStore, Tenants and Memories. Doubles stay inside isolated tests, which run without Platform or domain servers.
- Scope resources, ports, volumes, catalog and data per suite/batch, or identify the blocking run. Never reach hosted data.
- Require readiness and successful startup tasks. Startup defaults to ten minutes from environment request, excluding execution; finite overrides need justification. Record effective deadlines/origins and durations. Startup, execution and hold deadlines are separate.
- CI uses disposable Linux GitHub-hosted runners: isolated tests first, blocking package-mode integration second. No hosted provider credentials; registry access is read-only pull. Always withdraw attachments before job cleanup, including cancellation/partial startup.
- Record first outcomes immediately: `passed`, `failed`, `cancelled`, `withdrawn`. Local success waits for holds; any local owner/attached test/startup failure retains surviving resources until explicit cleanup, despite later cancellation/expiry.
- Cleanup is idempotent, retryable and ownership-limited; report leftovers honestly. Preserve classified, redacted diagnostics/results after deletion.
- Qualify success, failure, timeout, cancellation, collision and attachment orderings, including owner finish/cancel first, attached failure/expiry and failure then cancellation; record revision, environment, outcome, duration, overrides and diagnostics.

## Technical Decisions

Platform owns contracts/acceptance; Builds implements `hexalith-module`/workflows. Only the runner manages environments; fixtures consume descriptors without AppHosts. Own-repository fixtures produce no Platform evidence. Use one Aspire model/mapping: Debug source assets locally, Release/NuGet boundaries and catalog dependencies in package mode.

Use `hexalith.platform.candidate-and-attachment-policy.v1` from `docs/contracts/candidate-and-attachment-policy-v1.md`, owned by `@jpiquot`. Publication grants no Builds adoption. Missing/unknown/mismatched policy IDs fail closed; changes require versioning/adoption/requalification.

Versioned contracts bind run/owner/environment/CI identities; composition/mode/endpoints/catalog/tenant; readiness/startup; expected/loaded artifacts; executing tool/qualification; candidate/baseline classification; decision/no-reuse lineage; suites/configuration/completion; holds/deadlines/outcomes, work/access termination, cleanup/retention/diagnostics. Unsupported versions name supported versions. Reserve candidate/loaded fields for later population without version changes; missing values cannot qualify evidence.

PR evidence binds committed merge results to exact head/base decisions; releases need fresh runs. Gates authenticate decisions; only latest attempts qualify. No cross-decision reuse. Candidate artifacts use that revision's Release build; dependencies stay catalog-pinned. Acceptance requires content identities, qualification, complete required coverage and runner-owned package-mode CI. Local/baseline runs remain diagnostic.

Attachments require compatible composition/mode/identities/readiness/isolation; reject hosted, developer-started or cross-job environments. Completed owners refuse requests except ready local failure-retained environments, attachable after cancellation until explicit stop/cleanup.

Omitted holds mean 1,800 seconds; explicit whole seconds 1–14,400 are accepted exactly; invalid values fail without clamping/defaulting. Record original request/omission, acceptance UTC time, effective seconds and absolute expiry. Runner-issued lineage preserves expiry across reconnects/retries; fresh requests need admission. Expiry/withdrawal ends active work/access as `withdrawn`, never evidence. Attached cancellation affects only itself. Preserve first outcomes/failure retention; report and retry failed stopping without extending holds.

## UX & Interaction Patterns

Listings show owner, age, failed/attached runs, absolute deadlines and pending cleanup. Refusals explain blockers. Cleanup preserves separately started environments.

## Cross-Story Dependencies

Contracts precede provisioning/fixture migration; isolation/readiness underpin qualification. EventStore/Parties owners review adoption. Pin the qualified lifecycle tool in Platform/Parties. Attachments need runner policy adoption; candidate acceptance also needs Platform acceptance/integration qualification. Epic 3 populates reserved fields; lifecycle qualification alone grants no candidate acceptance.
