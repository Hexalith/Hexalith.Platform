---
title: 'Define the check-suite contract and own the reference suites'
type: 'feature'
epic: 4
story: 11
created: '2026-10-22'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-11-define-the-check-suite-contract-and-own-the-reference-suites'
baseline_commit: 'e39d59b9e7f93c5880eff423e24b8a0f274560ed'
builds_baseline_commit: '2210aaaf54c779b8763dbf1e8767bdc1b422bc6d'
story_completion: 'incomplete-until-tenants-coverage-resolved'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Executors lack a check-suite contract. Reference modules lack packaged smoke suites and complete flow-to-check mappings.

**Approach:** Builds supplies schemas, deterministic packaging and validation. Platform supplies owned suites and a registry linking declared flows/smoke surfaces to retained packages. Unresolved coverage blocks publication.

**Approved scope (2026-10-22):** Option A. Implement compatible artifacts while preserving authorization and critical-flow declarations. Record incompatible Tenants coverage as explicit publication blockers and an owned follow-up. This scoped build can complete; Story 4.11 remains in progress until that follow-up resolves. Denial checks never substitute for positive administrator or tenant-creation coverage.

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

## Code Map

- Builds `schemas/hexalith.module-manifest.v2.json` — existing links; Story 5.3 owns all-module enforcement.
- Builds Tooling `Manifest/PlatformManifestValidator.cs` — reuse embedded JsonSchema.Net, duplicate rejection and diagnostics.
- `references/Hexalith.Parties/module/hexalith.module-manifest.v2.json` and Platform `src/Hexalith.Platform.Composition/Declarations/{eventstore,tenants,memories}.json` — current flow sources, empty smoke lists.
- McpCli `src/Hexalith.McpCli/Hosting/HostFactory.cs` — CLI query/MCP stdio; no hosted HTTP server.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Builds/schemas/hexalith.check-{suite,invocation,result}.v1.json` and `docs/check-suite-contract-v1.md` — define closed versioned shapes, package layout, invocation restrictions, result semantics and consumer obligations.
- [x] Builds `src/libraries/Hexalith.Builds.Tooling/CheckSuites/` and `Hexalith.Builds.Tooling.csproj` — implement schema/semantic validation, deterministic packaging, byte verification, identity comparison and reference coverage checks; embed schemas.
- [x] Builds `src/libraries/Hexalith.Builds.Evidence.Cli/EvidenceCommandApplication.cs` — expose explicit package/validate entry points with stable diagnostics and exit codes; do not execute module code during validation.
- [x] Platform `checks/reference/{registry.json,suites/*.json,runner.py,parties.py,eventstore.py,tenants.py,memories.py,mcpcli.py}` — implement compatible checks and mappings using source-defined endpoints. Represent incompatible flows as blocked; separate package validity from publication eligibility.
- [x] Builds `test/Hexalith.Builds.Evidence.Tests/CheckSuites/` and `test/fixtures/check-suites/`; Platform `checks/reference/test_reference_suites.py` — test the matrix, tampering, CLI refusals, token exclusion and controlled-endpoint runner outcomes.
- [x] Platform `checks/reference/README.md` — document packaging/validation, source inventory, limits and Story 4.12's prepublication validation handoff.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` — append Tenants prerequisite: owner Platform `@jpiquot`, Tenants/EventStore review; synthetic lifecycle authorization, positive admin coverage decision, safe tenant-read admission. Resolved mappings and hosted evidence gate 4.11 completion.

**Acceptance Criteria:**
- Given a package, when independently validated, then executable bytes and declared checks match its digest.
- Given results and trusted context, when evaluated, then every identity and outcome is enforced.
- Given five reference modules, when coverage is validated, then unresolved ownership/source/check mappings block publication.

## Implementation Notes

- Approved Option A implemented in Builds and Platform; all seven scoped tasks complete. Tenants positive mappings remain blocked and sprint Story 4.11 stays in progress.
- Parent acceptance audit corrected UTC-Z parsing, normal projection lag, fresh-install null-baseline support and accurate CLI surface metadata.
- First-pass matrix coverage ran: immutable/tampered packages and missing ownership/mappings in CheckSuiteContractTests; independent identity/outcome admission cases; runner timeout/skip/failure/redirect/production guards; explicit Tenants blocker checks. Full Evidence assembly: 147 passed, no skips. Platform: 15 passed.

## Spec Change Log

## Review Triage Log


Independent review pass 1, 2026-10-22. All three layers reported before triage. Every surviving finding below is a direct correction or regression test for an existing contract rule; no public API or approved intent changes are required.

| Finding | Verdict | Route | Verified evidence |
|---|---|---|---|
| BH-1 | medium | patch | Suite semantics only restrict production writes; an existing-synthetic staging-write descriptor validates, and the runner permits it. Correct the existing tenancy guard. |
| BH-2 | medium | patch | JSON Schema permits mathematically integral `30.0`; EvaluateResult calls GetInt32, which throws for that representation. Read the validated integral value safely. |
| BH-3 | medium | patch | Date uses an optional K timezone and AssumeUniversal; naive timestamps pass C# while Python requires an offset. Require explicit zones. |
| BH-4 | low | reject | The owner parser does ignore path overrides/team syntax. Current Platform CODEOWNERS has one global @jpiquot owner and only same-owner overrides; the approved owner remains accurate. A general CODEOWNERS parser adds complexity for a policy absent from this reference composition. |
| BH-5 | low | patch | Package reads descriptorPath directly while payload paths are link-checked. Reuse confined-path checks for descriptor input; this is a direct consistency correction. |
| BH-6 | medium | patch | Package follows linked output ancestors and reports valid, while ValidatePackage immediately refuses that path. Check existing destination ancestry before writing. |
| BH-7 | medium | patch | IngestionWorkflowStatusMapper explicitly maps Completed plus MemoryUnitStatus.Failed into an ID and failureSummary; the payload checks neither and can pass. Require indexed, failure-free completion. |
| BH-8 | medium | patch | Search smoke only checks index/count metadata, so an empty result list passes. Require a real returned memory ID for the synthetic case. |
| BH-9 | medium | patch | communicate buffers candidate stdout before the 1 MiB check. Move the existing output bound into reading and preserve deadline/kill handling. |
| BH-10 | medium | patch | PartyDetail exposes type, active state and PersonDetails, but lifecycle only checks ID. Assert submitted synthetic values and reject corrupted readback. |
| VG-1 | medium | patch | Pre-verified: digest mutations affect only result and hit pair equality first; retained-package digest checks can disappear undetected. Add paired invocation/result mutations. |
| VG-2 | medium | patch | Pre-verified copied-source mutation: making run_candidate raise leaves 15 tests green because the method is mocked. Exercise the real method with a controlled executable. |
| VG-3 | medium | patch | Pre-verified copied-source mutation: removing correlated Memories search leaves all tests green. Add wrong/missing readback cases. |
| VG-4 | medium | patch | Pre-verified: coverage fixtures never introduce source-only drift. Add a new declared flow and a mismatched source smoke-suite link. |
| VG-other-1 | medium | patch (BH-1) | Controlled staging-write probe confirms an existing synthetic tenant receives the command; same missing tenancy guard as BH-1. |
| EH-1 | medium | patch (BH-1) | Same reproduced descriptor/runner tenancy gap; retain the independent finding. |
| EH-2 | low | reject (BH-4) | Same real parser limitation, but effective current ownership remains globally @jpiquot. Full path-policy support is unnecessary complexity for this scoped reference policy. |
| EH-3 | medium | patch | Ingestion status endpoint returns 404 for state not yet present/readable; completed() requires 200 immediately. Poll the expected 404 without hiding 403/500 failures. |
| EH-4 | medium | patch (BH-3) | Same naive-timestamp interoperability defect. |
| EH-5 | medium | patch | C# schema regex $ accepts a final LF; parent CLI independently accepted a newline-ending suite ID. Python fullmatch refuses it. Use absolute end assertions in schemas/path regex. |
| EH-6 | medium | patch (BH-6) | Same linked destination success followed by validation refusal. |
| EH-7 | medium | patch | CLI File.ReadAllBytes allocates before ValidateDocument applies the 4 MiB cap. Bound the existing document-read path before allocation. |

## Verification

- Builds: `dotnet test test/Hexalith.Builds.Evidence.Tests/Hexalith.Builds.Evidence.Tests.csproj --configuration Debug` — contract, package and CLI tests pass; use focused built-assembly execution if environment-blocked.
- Platform: `python3 -m unittest discover -s checks/reference -p 'test_*.py'` — package/runner matrix passes without hosted credentials.
- Reference packages validate; reference publication eligibility fails explicitly for retained coverage blockers. Hosted execution follows later. Leave sprint Story 4.11 `in-progress`.
