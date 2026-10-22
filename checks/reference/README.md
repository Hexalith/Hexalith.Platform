# Platform reference check suites

Platform owns these sources under `.github/CODEOWNERS` (`@jpiquot`). The v1
contract and validator live in Builds:
[`check-suite-contract-v1.md`](../../references/Hexalith.Builds/docs/check-suite-contract-v1.md).
`registry.json` links declared critical flows and smoke surfaces to exact retained
suite versions. It leaves source declarations and authorization intact.

| Module | Source inventory | Compatible payload |
| --- | --- | --- |
| Parties | `references/Hexalith.Parties/module/hexalith.module-manifest.v2.json`; Contracts `CreateParty`, `GetParty` and persisted `PartyDetail` | Staging Gateway create → completed command status → persisted projection; existing synthetic party read smoke |
| EventStore | `src/Hexalith.Platform.Composition/Declarations/eventstore.json`; `Controllers/{Commands,CommandStatus,Queries}Controller.cs` and `SubmitQueryResponse` in EventStore source | Command submission/status using Parties, plus authenticated synthetic projection read |
| Tenants | `src/Hexalith.Platform.Composition/Declarations/tenants.json`; Contracts `GetGlobalAdministratorsQuery`, `CreateTenant`, `GetTenantQuery` | Supplementary non-administrator GET `/api/global-administrators` denial only; all required positive flows remain blocked |
| Memories | `src/Hexalith.Platform.Composition/Declarations/memories.json`; `MemoriesRoutes`, `IngestionInput`, `IngestionWorkflowStatus`, `SearchResult` | Run-scoped text/file ingest → completed workflow → matching syntactic search; existing synthetic case search smoke |
| McpCli | `references/Hexalith.McpCli/src/Hexalith.McpCli/Hosting/HostFactory.cs`; CLI README and Parties `GetParty` catalog contract | Retained `/candidate/hexalith` CLI read-only `parties.get-party` query through Gateway; this slice does not exercise MCP stdio and adds no HTTP MCP endpoint |

All smokes declare a 300-second cadence and 30-second timeout. Staging lifecycle
checks declare 120 seconds. Production checks use existing synthetic tenants,
read-only operations and no tenant/admin writes. Parties uses the Gateway to
exercise the internal `party-domain` surface. Endpoint bases are supplied by the
executor for the relevant module; routes come from source, never health probes.
Memories uses only the syntactic axis, inline synthetic content and an
executor-verified configuration that disables external effects/providers. Its
case must already exist in the declared run-scoped tenant. Staging tenant/case
provisioning and authorization are executor prerequisites; the suite never mints
credentials, grants administrator privileges or creates/deletes tenants.

Build and call the source tool (the source command is not a released tool pin):

```bash
dotnet build references/Hexalith.Builds/src/libraries/Hexalith.Builds.Evidence.Cli/Hexalith.Builds.Evidence.Cli.csproj --configuration Debug
```

Let `tool` denote `dotnet <absolute Builds path>/src/libraries/Hexalith.Builds.Evidence.Cli/bin/Debug/net10.0/Hexalith.Builds.Evidence.Cli.dll`
(use the built assembly name printed by the build). With a fresh retained output
root `/tmp/reference-suites`, invoke the tool for each module:

```text
check-suite package checks/reference/suites/parties.json --payload-root checks/reference --destination /tmp/reference-suites/parties/1.0.0 --output json
check-suite validate /tmp/reference-suites/parties/1.0.0 --output json
check-suite coverage checks/reference/registry.json --repository . --packages /tmp/reference-suites --output json
```

Repeat package/validate for `eventstore`, `tenants`, `memories` and `mcpcli`.
Every package must pass byte validation. Coverage must refuse publication with
HXS006 while the three owned Tenants blockers remain:

- `tenant-management`: synthetic lifecycle authorization is unavailable.
- `global-administrator-management`: positive administration coverage requires an owner decision; a denial check cannot replace it.
- `tenant-read-smoke`: safe synthetic tenant-read admission is unresolved.

The owned follow-up is recorded in
[`deferred-work.md`](../../_bmad-output/implementation-artifacts/deferred-work.md).
Story 4.11 stays `in-progress` until the mappings and required hosted evidence
are resolved. The scoped compatible implementation can be reviewed independently.

The executor first validates retained bytes, constructs the closed invocation,
mounts only the package/candidate/fixture context, supplies a short-lived
`HEXALITH_CHECK_TOKEN`, then runs `python3 payload/runner.py --invocation <path>`.
It independently authenticates the served release, enforces sandbox isolation and
deadlines, evaluates the result against its original invocation, and retains
every attempt. The runner's self-report cannot provide that authority. Story
4.20 implements the sandbox; no hosted credentials or environments are created
by this work.

```bash
python3 -m unittest discover -s checks/reference -p 'test_*.py'
python3 -O -m unittest discover -s checks/reference -p 'test_*.py'
```

These tests use controlled loopback HTTP and a mocked candidate transport. They
exercise response shapes, durable readback requirements and nonpassing/token
handling, and are never hosted E2E qualification. Schema validity, local tests,
specimen hosts and health probes do not satisfy hosted evidence.

Story 4.12 must run Builds package validation plus registry coverage before
publication, retain the exact package bytes, and bind their digests into signed
release records. No unfinished publication integration is claimed here. Preserve
the separate recovery-hook contract.
