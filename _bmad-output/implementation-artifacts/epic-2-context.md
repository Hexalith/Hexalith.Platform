# Epic 2 Context: Test modules against real services, locally and in CI

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Provide trustworthy local and CI integration testing through fresh, runner-owned environments containing the module's declared real services. Runs must have isolated resources and data, bounded readiness, explicit attachment ownership, useful failure retention and verifiable cleanup. Isolated tests remain independent of Platform; CI integration becomes a blocking package-mode tier. This epic establishes lifecycle and shared policy; candidate artifact-identity implementation follows when the composed host and run-scoped McpCli can report loaded identities.

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

- Use real services from the module-owned minimum configuration. Parties' reference environment includes EventStore, Tenants and Memories. Test doubles belong only inside isolated tests, which run directly without Platform or domain servers.
- Provision a fresh environment per suite or compatible batch by default. Scope ports, volumes, resources, catalog instance and test data to the run; otherwise fail promptly naming the conflicting run. Never access staging or production data.
- Wait for module-defined readiness and successful required startup tasks. A running process is insufficient; missing usable readiness is a configuration error. The default startup deadline is ten minutes from the environment request, excluding test execution. Apply only justified finite declaration overrides; record the effective deadline and actual duration. Definite failures may end startup early; timeouts name unready resources and preserve diagnostics.
- Accept explicit attachment only to compatible local or CI run-owned environments, checking composition, mode, artifact identities, readiness and data isolation. Refuse hosted targets and new attachments after owner completion, except local environments retained after failure. CI attachments stay within one CI job.
- Record each run's first terminal outcome immediately. Successful local runs clean up after attachment holds end. Any owner or attached local test/startup failure retains surviving resources for debugging; subsequent cancellation cannot erase that retention. Explicit developer cleanup can remove it.
- An accepted attachment delays automatic cleanup until it ends or its finite hold expires. Attached-run cancellation ends only that run. Expiry or environment withdrawal yields `withdrawn`, neither pass nor failure and never integration evidence. Owner cancellation or explicit stop withdraws active attachments before cleanup; CI job completion or cancellation also ends attachments first.
- CI uses disposable Linux GitHub-hosted runners: isolated tests first, then blocking real-service integration in package mode. CI always cleans owned resources after success, failure, cancellation or partial startup. It receives no staging/production provider credentials; retained-registry access is read-only pull.
- Cleanup is retryable, removes only the environment's owned resources and reports leftovers. Never mark incomplete cleanup complete or stop a separately developer-started Aspire environment. Preserve classified, redacted results and diagnostics after resources disappear.
- Qualify success, failure, timeout, cancellation, collision and attachment orderings on the Parties reference composition, including attached failure, hold expiry and failure followed by cancellation. Evidence records revision, environment, result, duration, overrides and diagnostics. Acceptance requires zero cross-environment deletion, premature cleanup during valid holds or falsely completed CI cleanup.

## Technical Decisions

Platform defines composition and contract semantics; Builds implements the runner as `hexalith-module` and supplies reusable CI workflows. The runner alone provisions, waits for readiness, records ownership and cleans multi-module environments. Fixtures consume its descriptor and never start a second AppHost. Technical modules may retain non-exported own-repository fixture paths, whose runs do not count as Platform integration evidence. Do not introduce a custom DSL or environment service.

The versioned descriptor carries run/owner identity, composition identity and mode, resource endpoints, run-scoped catalog name, synthetic tenant identifier, expected/loaded artifact identities, candidate revision or baseline-only label, and attachment holds. Results distinguish `passed`, `failed`, `cancelled` and `withdrawn`. Unsupported descriptor versions fail explicitly. Candidate fields are reserved before their producers exist so later population needs no contract-version change.

One shared decision must fix the candidate revision rule (PR head or merge result), the merge/release decision evidence gates, unchanged-module evidence reuse, finite default and maximum attachment holds, and shorter-hold requests. Each decision needs an owner and revisit condition. Publish it for inclusion in the runner/descriptor contract before any attachment or candidate-evidence consumer implements policy; planning does not already choose these values.

Local source runs use the active root's direct dependencies and Debug assets; CI package mode uses Release and NuGet boundaries. Candidate acceptance requires every consumed candidate-module artifact to come from that committed revision's own build, while other modules remain catalog-pinned. Content identities, including packages in the composed host and run-scoped McpCli, must match expected identities. Missing/substituted identities, other revisions and uncommitted inputs are refused; labelled baseline-only runs cannot satisfy candidate acceptance.

## UX & Interaction Patterns

Retained and pending-cleanup environment listings show owner, age, failed runs, attached runs and finite hold deadlines. Collision, compatibility, readiness and cleanup failures identify the blocking run, resource or reason. Developers can inspect retained failures and explicitly stop or clean up without affecting independently started environments.

## Cross-Story Dependencies

The shared policy precedes descriptor consumers and attachment implementation. The descriptor precedes provisioning and fixture migration; run isolation and the existing readiness declarations underpin lifecycle qualification. Platform owns contracts and acceptance, Builds owns runner/workflow implementation, and EventStore/Parties owners review fixture adoption. Record and pin the Platform-accepted lifecycle tool version in Platform and Parties. Epic 3 consumes this policy and descriptor to implement candidate identity reporting and evidence acceptance; lifecycle qualification alone does not establish candidate-artifact acceptance.
