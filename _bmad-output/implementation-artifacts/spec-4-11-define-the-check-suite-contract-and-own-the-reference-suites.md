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

**Problem:** Executors lack a check-suite contract. Reference modules lack packaged smoke suites and complete flow-to-check mappings.

**Approach:** Builds supplies schemas, deterministic packaging and validation. Platform supplies owned suites and a registry linking declared flows/smoke surfaces to retained packages. Unresolved coverage blocks publication.

## Boundaries & Constraints

**Always:** Bind executable payload and descriptor with immutable SHA-256; specify nonrecursive digest computation, artifact layout and version selection. Declare stable check IDs, surfaces, finite timeouts and smoke cadences. Bind invocations/results to check, attempt, environment, served release, suite, profile and configuration; retain staging working-baseline identity and all attempts. Represent passed, failed, skipped, timed-out and incomplete; timeout fails, and only a complete matching pass satisfies a check.

Specify executor-sandbox execution with only the declared verification endpoint and short-lived synthetic-client tokens as authority-bearing inputs. Tokens never enter retained manifests, result records or diagnostics. The executor owns trusted release/profile/configuration context, authenticated served-release observation and deadlines; a suite cannot certify its own identity. Story 4.20 implements sandbox enforcement.

Platform-owned sources inherit owner `@jpiquot` from `.github/CODEOWNERS`. Preserve module ownership and existing `criticalFlows.e2eChecks` / `smokeSurfaces.suite` links. Cover Parties, EventStore, Tenants, Memories and McpCli; missing ownership, source, checks or mappings blocks publication. Production smokes use existing synthetic tenants, prefer reads and never grant administration, create/delete tenants or cause external effects. Staging lifecycle requires declared run-scoped synthetic tenants and authorization support.

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

1. **Tenants coverage conflicts with synthetic-only authority.** Declared tenant create/get and global-administrator list/set require current global-administrator authority in fixed `system` scope. Synthetic clients cannot have admin/cross-tenant grants; the staging synthetic-creation allowance is unimplemented. Denial tests cannot replace positive flows. Choose **A: preserve policy/declarations**, build compatible artifacts but retain publication blockers and leave 4.11 incomplete; or **B: expand scope to reconcile Tenants authorization/declarations first**, settling owner-approved policy before complete implementation.

## Code Map

- `references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json` — existing flow/smoke links; global all-module enforcement belongs to Story 5.3.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs` — reuse embedded JsonSchema.Net, duplicate-property rejection and deterministic diagnostics; no new dependency needed.
- `references/Hexalith.Parties/module/hexalith.module-manifest.v2.json` and Platform `src/Hexalith.Platform.Composition/Declarations/{eventstore,tenants,memories}.json` — current flow sources, empty smoke lists.
- Tenants `src/Hexalith.Tenants.Server/Aggregates/{TenantAggregate,GlobalAdministratorsAggregate}.cs` — persisted administrator requirements causing the conflict.
- `references/Hexalith.McpCli/src/Hexalith.McpCli/Hosting/HostFactory.cs` — retained CLI query and MCP stdio surfaces; no hosted HTTP server. Existing loopback conformance tests are not hosted suites.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.Builds/schemas/hexalith.check-{suite,invocation,result}.v1.json` and `docs/check-suite-contract-v1.md` — define closed versioned shapes, package layout, invocation restrictions, result semantics and consumer obligations.
- [ ] Builds `src/libraries/Hexalith.Builds.Tooling/CheckSuites/` and `Hexalith.Builds.Tooling.csproj` — implement schema/semantic validation, deterministic packaging, byte verification, identity comparison and reference coverage checks; embed schemas.
- [ ] Builds `src/libraries/Hexalith.Builds.Evidence.Cli/EvidenceCommandApplication.cs` — expose explicit package/validate entry points with stable diagnostics and exit codes; do not execute module code during validation.
- [ ] Platform `checks/reference/{registry.json,suites/*.json,runner.py,parties.py,eventstore.py,tenants.py,memories.py,mcpcli.py}` — implement synthetic-safe executable checks, owned declarations and complete source/check mappings to the extent authorized by the Tenants decision; never claim unresolved coverage.
- [ ] Builds `test/Hexalith.Builds.Evidence.Tests/CheckSuites/` and `test/fixtures/check-suites/`; Platform `checks/reference/test_reference_suites.py` — test the matrix, tampering, CLI refusals, token exclusion and controlled-endpoint runner outcomes.
- [ ] Platform `checks/reference/README.md` — document packaging/validation, source inventory, limits and Story 4.12's prepublication validation handoff.

**Acceptance Criteria:**
- Given a package, when independently validated, then executable bytes and declared checks match its digest.
- Given results and trusted context, when evaluated, then every identity and outcome is enforced.
- Given five reference modules, when coverage is validated, then unresolved ownership/source/check mappings block publication.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- Builds: `dotnet test test/Hexalith.Builds.Evidence.Tests/Hexalith.Builds.Evidence.Tests.csproj --configuration Debug` — contract, package and CLI tests pass; use focused built-assembly execution if environment-blocked.
- Platform: `python3 -m unittest discover -s checks/reference -p 'test_*.py'` — package/runner matrix passes without hosted credentials.
- Run reference packaging plus the coverage validator; expected eligibility depends on the explicit Tenants decision. Live staging execution belongs to its later executor/deployment story.
