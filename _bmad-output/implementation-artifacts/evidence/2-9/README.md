# Story 2.9 implementation evidence — incomplete acceptance

The descriptor consumer, exported fixture migration and Parties suite are implemented. Story 2.9
remains **in progress**: the real Parties flow and persisted-state acceptance have not run successfully.
No attachment, expiry-enforcement, package, candidate or tool qualification is granted.

## Source identity and scope

The spec baseline is `d5f16fb64f766eea4cf9dbc1a324ba3d46a1266a`. Initial submodule revisions were:

- EventStore: `6e0280bf489171326e2ce1ba3dde361712b7c977`.
- Parties: `784215a8fe91b4d5ae00eb65ab61d3b9507ca0c2`.
- Builds: `0b17f8022cdc23b8b242b000f5b2aa15580184ac`.

Another actor committed the shared workspace while this implementation agent was collecting evidence.
This agent did not stage, commit, merge, push or change pins. The captured canonical HEADs are EventStore
`28ccd32c95ab521755d12deee5d7ad16927e7c92` and Parties
`1d84af20837f388c1605085c54109bd4b537a998`. The first live run observed Platform
`490fd0b54d7e25da4ccaf7534408b0dca9e6b627`.

[changed-files.json](changed-files.json) lists the 32 EventStore and 12 Parties task-owned paths.
[source-snapshot.json](source-snapshot.json) records file hashes and canonical revisions.
[concurrent-baseline-delta.json](concurrent-baseline-delta.json) identifies other files changed since
the initial baselines. The `*.task.patch.gz` files contain task-only unified diffs;
`*.baseline-to-current.patch.gz` contain the complete baseline-to-current diffs. Decompress with
`gzip -dc` to inspect or apply the exact patch bytes. The broader diffs include concurrent work and
must not be attributed to this task.

The parent audit subsequently corrected `Parties/Directory.Packages.props`: an `Update` alone loses
the older catalog's missing Contracts.UI fallback. A conditional include now retains exactly one
family-pinned entry with either catalog. This post-commit correction is included in the source hashes
and patches; the snapshot records its working-tree status separately from the external HEADs.

The shared consumer embeds the authoritative schema and applies strict bounded UTF-8/JSON, duplicate,
version/policy, scoped-identity/reference, URI and hold/deadline checks before readiness, startup and
synthetic-isolation enforcement. Fixtures require matching private run/tenant/isolation identities;
endpoints use exact resource/module/name selection. Bounded clients prevent hosted endpoints,
redirects and cross-origin absolute-URI overrides. Disposal cancels requests, releases clients and
reports failures without taking runner lifecycle ownership. Attached execution explicitly refuses;
hold arithmetic remains validated without granting attachment qualification.

Both exported startup paths and their local prerequisite skips were removed. The private EventStore
owner fixtures and frozen Parties topology sources were preserved. Parties now declares source and
package suites, uses the real Tenants REST interface (which owns system-scope routing), the Memories
REST tenant interface and EventStore command/query interfaces, and independently reads persisted
aggregate metadata and every event envelope from its declared run-owned Redis endpoint. No suite
credentials, domain servers or doubles are created by the new fixture.

## Executed checks

All builds used Debug and `-nodeReuse:false`; final builds also used `-m:1`. EventStore source builds
used `-p:UseHexalithProjectReferences=true`. Parties used the exact environment returned by the module
tool's `prepare-build --manifest references/Hexalith.Parties/module/hexalith.module-manifest.v2.json
--workspace <private-workspace> --mode source --output json`.

| Check | Observed result |
| --- | --- |
| EventStore Testing.Tests build and full DLL `-class '*'` | Build passed; 189 tests, zero failures/skips. |
| EventStore Testing.Integration.Tests build and full DLL `-class '*'` | Build passed; 51 tests, zero failures/skips. |
| Parties Platform project build through prepared source imports | Passed, zero warnings/errors. |
| `python3 scripts/test_platform_declaration.py` from Parties | 7 tests passed. |
| Parties Platform DLL `-class '*PartyEnvironmentTests'` with descriptor variable removed | Expected refusal: one failed test, zero skips; `HXT106`, no startup. |
| Schema and package boundaries | All three schema copies byte-identical; no direct Aspire hosting or non-packable Tooling dependency. |
| Private EventStore IntegrationTests project build | Blocked by unchanged `EventStore/ConcurrencyConflictIntegrationTests.cs:286`, CS7036 against the current `SubmitCommandHandler` constructor. Narrow owner tests could not be rebuilt. |

[canonical-checks/checks.json](canonical-checks/checks.json) and its compressed stdout archives record the current-HEAD
reproduction. [final-checks/checks.json](final-checks/checks.json) preserves the preceding checks,
including the private-owner compilation failure. [schema-and-package-boundaries.json](schema-and-package-boundaries.json)
records the schema hashes and project dependency declarations.
[matrix-test-checks.json](matrix-test-checks.json) records 34 reader and 7 fixture tests rerun with
verbose output, including every matrix case by name; all passed without skips. The parent
[implementation-audit.json](implementation-audit.json) maps coverage and outstanding acceptance.
[catalog-fallback-check.json](catalog-fallback-check.json) records one Contracts.UI 4.7.1 entry with
both the published Builds 4.38.3 catalog and the current-source catalog, plus the clean Parties rebuild.
[stdout-archives.json](stdout-archives.json) records the SHA-256 of every compressed log and its
original stdout bytes, including all broad failures. Raw `.log` files are ignored locally; the
corresponding `.log.gz` archives retain the decisive output. Decompress with `gzip -dc`. [missing-handoff.log.gz](missing-handoff.log.gz)
retains the required fail-without-skip result. Installed/published package acceptance was not attempted:
it requires authoritative publications of the migrated testing API, and Debug artifacts grant none.

The complete isolated Parties project inventory was built/run sequentially without descriptor or
private handoff variables. The broad lane is **not green**. Exact commands and exit codes are in
[parties-isolated/checks.json](parties-isolated/checks.json); logs, counts and failing method names are
retained in [isolated-test-observations.json](isolated-test-observations.json). These are unresolved
checks in unchanged compatibility/package/fitness areas, not asserted baseline reproductions:

- Aspire model checks require the absent nested EventStore checkout (7 failures).
- Contracts package fixtures require nested Commons and invoke Release; Client package fixtures invoke
  Release and hit the source mapping's Debug guard. No guard was weakened or nested checkout initialized.
- CI checks require nested Builds/history and other owner receipts (80 failures).
- Parties.Tests initially failed compilation at the now-required QueryRouter catalog argument. Its
  isolated paging test now supplies an explicit test-only validated catalog lease. The host's HTTP
  security startup validator is preserved. Its older gateway factory still removes a now-required
  `ICommandActivityTracker`; the gateway run has 64 failures out of 66. Other classes have 32 failures
  and four skips out of 2,745, including owner-receipt/checkout and readiness expectations.

Focused Contracts checks excluding their package namespace pass 194/194; focused Client checks
excluding their package namespace pass 343/343. Authentication, Server, Security, UI, both portals,
Mcp, Picker and Sample project runs pass. Projections has no failures and one existing optional skip.
These focused results do not replace the failing broad lane. Doubles remain confined to isolated tests.

## Real declared source attempt and cleanup

Executed from Platform:

```text
dotnet references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/bin/Debug/net10.0/Hexalith.Builds.Module.Cli.dll test --manifest references/Hexalith.Parties/module/hexalith.module-manifest.v2.json --profile platform-source --mode source --output json
```

Run `e3ea18e127cc57e7a43ad7c9c386131e` provisioned fresh Redis, Memories backing stores and mTLS
control-plane containers, and started real EventStore. The runner refused startup with `HXR027`.
[The exact command result](first-run-command.json), [startup failure](first-run-startup-failure.json),
[readiness observation](first-run-readiness-progress.json), [EventStore readiness](first-run-eventstore-ready.json)
and [terminal result](first-run-test-result.json) are retained. No descriptor was produced and no suite
was launched, so there are no successful persistence or dependency observations to retain.

The concrete unavailable catalog producer is distinct from fixture migration: EventStore's
`route-catalog` check reports that no activated catalog agrees with its profile/inventory/authentication/
topology. Its projection-delivery writer protocol remains inactive because the existing authorized
runner cutover waits for every other dependency check to be healthy. The sidecar, placement, state
store, pub/sub and config-store checks were healthy. No approved run-scoped catalog producer is
available in the current draft Story 3.3; readiness was not bypassed.

Synthetic tenant provisioning is a second unavailable producer. The current runner supplies a
synthetic identifier, not a persisted Tenants/Memories record. Tenants `CreateTenant` requires global
administrator authority, which the existing suite credential does not grant. The new flow requires
those real records and must remain unqualified until the owner-side provisioning from Story 3.5 is
available; the fixture does not seed replacements or mint credentials. Separately, source inspection
suggests the selected suite token's `domains=parties` differs from the Party contract's `party` domain.
That possible mismatch is **inferred**, not reproduced: execution never reached the fixture.

Exact cleanup was executed with `down --run-id e3ea18e127cc57e7a43ad7c9c386131e --output json`.
[first-run-cleanup.json](first-run-cleanup.json) reports success with empty remaining resources.
The pre-existing Docker resources `fluent-ui-blazor-mcp`, `dapr_placement`, `dapr_scheduler`,
`dapr_redis` and `dapr_zipkin` remained. The initial live candidate reported tool version
`1.0.0-20261030070621`, outside Platform's accepted range; it provides diagnostic execution only.

The agreed `attached-test-execution-and-expiry-enforcement` follow-up remains open. None of these
checks proves attached execution, live expiry termination, package-mode CI, candidate acceptance or
successful real Parties acceptance.
