---
title: 'Define the check-suite contract and own the reference suites'
type: 'feature'
epic: 4
story: 11
created: '2026-10-22'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-11-define-the-check-suite-contract-and-own-the-reference-suites'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Executors lack a shared check-suite package, invocation and result contract. The reference modules have no packaged production-safe smoke suites or complete flow-to-check mappings.

**Approach:** Builds supplies versioned schemas, deterministic packaging and offline validation through its existing tooling. Platform supplies owned reference suite sources and a validated registry linking module-declared flows and smoke surfaces to retained packages. Unresolved coverage blocks publication.

## Boundaries & Constraints

**Always:** Package executable checks with an immutable SHA-256 identity covering the descriptor and payload. Specify digest computation without self-reference, retained artifact layout and exact-version selection. Declare stable check IDs, logical surfaces, finite timeouts and finite smoke cadences. Bind invocations/results to check, attempt, environment, served release, suite, profile and configuration identities; preserve staging working-baseline identity and every attempt result. Represent passed, failed, skipped, timed-out and incomplete; timeout is failure, and only a complete matching pass satisfies a required check.

Specify executor-sandbox execution with only the declared verification endpoint and short-lived synthetic-client tokens as authority-bearing inputs. Tokens never enter retained manifests, result records or diagnostics. The executor owns trusted release/profile/configuration context, authenticated served-release observation and deadlines; a suite cannot certify its own identity. Story 4.20 implements sandbox enforcement.

Platform-owned sources inherit accountable owner `@jpiquot` from `.github/CODEOWNERS`. Preserve module ownership of critical-flow definitions and existing `criticalFlows.e2eChecks` / `smokeSurfaces.suite` fields. Cover Parties, EventStore, Tenants, Memories and McpCli; missing ownership, source, checks or mappings blocks reference publication. Production smokes use existing synthetic tenants, prefer reads and never grant administration, create/delete tenants or cause external effects. Staging tenant lifecycle requires declared run-scoped synthetic tenants and actual authorization support.

**Never:** Treat schema validity, local unit tests, health probes or specimen hosts as passing hosted E2E evidence. Silently omit privileged Tenants flows, invent surface endpoints, weaken authorization, mint hosted credentials, deploy environments or claim unfinished publication/sandbox integration exists. Preserve unrelated working-tree edits and the recovery-hook contract.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Valid package | Owned nonempty descriptor, payload and resolved mappings | Reproducible digest; schema and semantic validation pass | Stable report |
| Invalid package | Empty suite, duplicate IDs, unsafe paths, missing owner/source/check or altered bytes | Publication eligibility refused | Sanitized deterministic diagnostics |
| Result mismatch | Wrong release/digests/check, missing result or expired deadline | No passing evidence accepted | Explicit nonpassing outcome |
| Interrupted check | Skip, timeout, partial output or runner failure | Preserve original outcome; timeout fails | Nonzero execution result |
| Unsafe flow | Required flow needs authority absent from synthetic contract | Mapping remains unresolved and blocks publication | No fabricated replacement pass |

</frozen-after-approval>

## Open Questions

1. **Tenants authorization conflicts with required coverage.** Current declared `tenant-management` (create/get) and `global-administrator-management` (list/set) require current global-administrator authority, including access to fixed `system` scope. Synthetic clients must have no administrator or cross-tenant grants; the architecture's staging synthetic-creation allowance is not implemented. Denial checks do not prove these positive flows. Choose **A: preserve policy and declarations**, implement the contract and compatible suites while retaining explicit publication blockers and leaving Story 4.11 incomplete; or **B: expand the work to resolve the Tenants authorization/declaration design first**, with owner-approved scope and policy decisions before implementing complete coverage. Neither option silently marks incomplete mappings complete.

## Code Map

- `references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json` — existing flow/smoke links; global all-module enforcement belongs to Story 5.3.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs` — reuse embedded JsonSchema.Net, duplicate-property rejection and deterministic diagnostics; no new dependency needed.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Evidence.Cli/EvidenceCommandApplication.cs` — extend existing tool with explicit suite commands; preserve current readiness/G4 dispatch.
- `references/Hexalith.Parties/module/hexalith.module-manifest.v2.json` and Platform `src/Hexalith.Platform.Composition/Declarations/{eventstore,tenants,memories}.json` — current flow sources, empty smoke lists.
- Parties `tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs`, EventStore `Controllers/{Commands,CommandStatus,Queries}Controller.cs`, Memories `Endpoints/{Ingestion,Search}Endpoints.cs` — request/assertion references; do not copy local provisioning or token minting.
- Tenants `src/Hexalith.Tenants.Server/Aggregates/{TenantAggregate,GlobalAdministratorsAggregate}.cs`, query handler `GetGlobalAdministratorsQueryHandler.cs`, and `TenantsGlobalAdministratorVerifier.cs` — persisted administrator checks causing the unresolved policy conflict.
- `references/Hexalith.McpCli/src/Hexalith.McpCli/Hosting/HostFactory.cs` — retained CLI query and MCP stdio surfaces; no hosted HTTP server. Existing loopback conformance tests are not hosted suites.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.Builds/schemas/hexalith.check-{suite,invocation,result}.v1.json` and `docs/check-suite-contract-v1.md` — define closed versioned shapes, package layout, invocation restrictions, result semantics and consumer obligations.
- [ ] Builds `src/libraries/Hexalith.Builds.Tooling/CheckSuites/` and `Hexalith.Builds.Tooling.csproj` — implement schema/semantic validation, deterministic packaging, byte verification, identity comparison and reference coverage checks; embed schemas.
- [ ] Builds `src/libraries/Hexalith.Builds.Evidence.Cli/EvidenceCommandApplication.cs` — expose explicit package/validate entry points with stable diagnostics and exit codes; do not execute module code during validation.
- [ ] Platform `checks/reference/{registry.json,suites/*.json,runner.py,parties.py,eventstore.py,tenants.py,memories.py,mcpcli.py}` — implement synthetic-safe executable checks, owned declarations and complete source/check mappings to the extent authorized by the Tenants decision; never claim unresolved coverage.
- [ ] Builds `test/Hexalith.Builds.Evidence.Tests/CheckSuites/` and `test/fixtures/check-suites/`; Platform `checks/reference/test_reference_suites.py` — cover every matrix row, package tampering, CLI refusal, retained-token exclusion and runner outcomes with controlled endpoint fixtures.
- [ ] Platform `checks/reference/README.md` and `docs/contracts/reference-check-suites.md` — publish reproducible packaging/validation commands, source inventory, limits and the mandatory prepublication validation handoff to Story 4.12.

**Acceptance Criteria:**
- Given a packaged suite, when independently validated, then executable bytes, stable IDs, surfaces and smoke cadences match its immutable digest.
- Given an invocation/result pair and trusted expected context, when evaluated, then the five outcomes and every identity binding are enforced without treating timeout as success.
- Given all five reference modules, when coverage is validated, then owner/source/check coverage is explicit and any unresolved mapping prevents publication eligibility.
- Given this story's artifacts, when consumed by later executors/publication, then contract validation is distinguished from sandbox enforcement and hosted qualification.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- Builds: `dotnet test test/Hexalith.Builds.Evidence.Tests/Hexalith.Builds.Evidence.Tests.csproj --configuration Debug` — contract, package and CLI tests pass; use focused built-assembly execution if environment-blocked.
- Platform: `python3 -m unittest discover -s checks/reference -p 'test_*.py'` — package/runner matrix passes without hosted credentials.
- Run reference packaging plus the coverage validator; expected eligibility depends on the explicit Tenants decision. Live staging execution belongs to its later executor/deployment story.
