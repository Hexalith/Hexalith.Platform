---
title: '2.2 Define the test environment descriptor and result contract'
type: 'feature'
created: '2026-10-22'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '2-2-define-the-test-environment-descriptor-and-result-contract'
context:
  - '_bmad-output/implementation-artifacts/epic-2-context.md'
  - 'docs/contracts/candidate-and-attachment-policy-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Publish versioned test-environment and terminal-result contracts so module tests can consume runner endpoints and composition identity. Platform owns their schemas and semantics; Builds implements an offline consumer reader and ships the schemas with `hexalith-module`.

## Boundaries & Constraints

Define `hexalith.test-environment.v1` and `hexalith.test-result.v1`, adopting the exact Story 2.1 policy identifier. Carry run/attempt, owner/environment and CI identities; composition/mode, per-resource endpoints, catalog commit/instance, synthetic tenant/isolation, readiness/startup tasks, expected/loaded artifact identities and attachment holds. Reserve structured tool qualification, candidate classification/revision, gated decision, no-reuse lineage, declared/executed suite/configuration/completion, independent deadlines, termination, retention/cleanup and diagnostics bindings from the published policy.

Unavailable producer values must be explicitly `not-yet-populated`, with typed reserved fields and no invented identity or qualification. Epic 3 can populate them, including candidate or `baseline-only` labels, without changing v1. Contract validity alone grants no candidate evidence or attachment permission. Results record the first terminal outcome/time; only `passed`, `failed`, `cancelled`, `withdrawn` are valid.

Validate strict JSON and schema shapes, exact policy and supported contract versions. Preserve absent hold requests versus explicit values: omitted means 1,800 seconds; explicit whole seconds 1–14,400 are valid; null/fractional/invalid requests fail. Define immutable acceptance/expiry and actual end fields with UTC timestamps.

Preserve existing edits, as authorized. Exclude provisioning, native handoff migration, attachment lifecycle/evidence acceptance, services, releases, tool/catalog pins and Git recording operations; those remain downstream work.

## I/O & Edge-Case Matrix

| Input/state | Expected behavior |
| --- | --- |
| Complete v1 descriptor/result | Reader exposes validated identity/endpoints/outcome without starting resources. |
| Unsupported or absent schema version | Refuse; explicitly name supported versions for that document kind. |
| Missing/unknown policy, required field, unknown property, duplicate key, malformed/oversized/deep JSON | Refuse atomically with stable metadata-only diagnostics. |
| Reserved candidate/loaded fields unavailable | Accept explicit unpopulated shape; never infer candidate eligibility. |
| Reserved fields populated later | Accept defined candidate/baseline shapes under the same version. |
| Invalid outcome or hold request | Refuse; never default malformed requests or translate legacy terminal statuses. |

</frozen-after-approval>

## Code Map

Paths below are workspace-relative; Builds changes belong to its own repository.

- `docs/contracts/candidate-and-attachment-policy-v1.md`: authoritative handoff and hold semantics; Story 2.1 is done.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs`: embedded JsonSchema.Net, offline registry, bounded strict parsing and diagnostics patterns; reuse duplicate detection from `JsonDuplicatePropertyValidator.cs`.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestHandoff.cs` and `ModuleTestRunRecord.cs`: existing transport/internal records; preserve. Executable module descriptors are a separate ABI.
- Builds `PlatformManifestValidationTests.cs`: schema/reader agreement pattern. Tooling embeds schemas; CLI explicitly includes schema package assets.

## Tasks & Acceptance

**Execution:**

- [ ] `docs/contracts/test-environment-v1.md` — document fields, population states, version rejection, outcome/hold semantics, shared descriptor/result bindings and downstream responsibilities; link from `README.md`.
- [ ] `docs/contracts/schemas/hexalith.test-environment.v1.json` and `hexalith.test-result.v1.json` in that directory — publish self-contained Draft 2020-12 schemas with closed objects and typed reserved fields; define all policy handoff bindings without requiring future producers now.
- [ ] `references/Hexalith.Builds/schemas/hexalith.test-environment.v1.json` and `hexalith.test-result.v1.json` in that directory — mirror canonical schema content for independent consumption.
- [ ] `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentContractReader.cs` and `TestEnvironmentContractReadResult.cs` in that directory — implement descriptor/result reads returning detached validated JSON or diagnostics, explicit version/policy refusal and bounded strict parsing; never return partial documents or fetch remote schemas.
- [ ] `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj` — embed mirrors; `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj` — include both packaged schemas; document consumption in Builds `README.md` while preserving its existing edits.
- [ ] `references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs` and `references/Hexalith.Builds/test/fixtures/module/test-environment-v1/descriptor.json` and `result.json` there — test published/embedded agreement, endpoint consumption, every matrix row, four outcomes, hold boundaries and later population; samples confer no runtime qualification.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` — synchronize only Story 2.2 as the workflow advances; preserve Epic 2 and unrelated statuses.

**Acceptance Criteria:**

- Given the published schemas, when a module author validates descriptor/result samples, then every Story 2.2 field and Story 2.1 handoff binding has a defined representation and lifecycle responsibility.
- Given unsupported descriptor/result versions, when the Builds consumer reads them, then it refuses and names supported versions.
- Given future candidate/loaded-identity values, when reserved fields are populated, then v1 still validates without granting evidence acceptance.
- Given terminal results, when consumers validate them, then precisely the four required outcomes are accepted independently of cleanup disposition.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

From Builds: `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1`, then invoke its built xUnit v3 assembly with `-class Hexalith.Builds.ModuleTool.Tests.TestEnvironmentContractReaderTests`. Run relevant existing schema/handoff regressions. Verify CLI package-item inclusion without publishing, canonical/mirror JSON equality, local documentation links and whitespace. No services are needed for contract checks.
