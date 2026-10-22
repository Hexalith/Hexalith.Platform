---
title: '4.6 Encode release, attempt, qualification and Administrator records'
type: 'feature'
created: '2026-10-22'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-6-encode-release-attempt-qualification-and-administrator-recor'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform lacks shared signed operational records. Store access or self-declared identity must never authorize deployment decisions.

**Approach:** Implement eight versioned record contracts, strict encoding, signature verification and state-transition validation in Hexalith.Builds, following the Platform architecture's writer matrix.

## Boundaries & Constraints

**Always:** Bind schema, kind, record ID, writer identity/key ID, subject, environment where applicable, timestamps and payload in the signature. Resolve keys and writer permissions from independently supplied trust configuration; reject unknown, revoked, wrongly scoped or overlapping Administrator/non-Administrator identities and public keys. Administrator custody is a trusted enrollment assertion, never inferred from a record. Preserve all existing edits.

Release and release-available records belong to publication; attempts belong to their environment executor (production executor for in-place recovery; recovery executor for replacement-capacity DR). Qualification and production-promoted records belong to their issuing attempt's writer. Latest-working advances only through a working terminal attempt's writer, in the same environment, at a higher epoch. Executors, monitor, Administrator and deputy may set environment-bound promotion stops; clearance requires an Administrator record.

**Never:** Implement storage, locks, deployment, workflow publication, production identities or key provisioning. Do not change package pins or create another CLI. Store integration and lifecycle eligibility remain downstream responsibilities; transition checks must run within the later store's CAS operation.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Error handling |
| --- | --- | --- | --- |
| Authentic record | Valid schema, signature and entitled writer | Return verified typed record and content digest | No store-access shortcut |
| Forgery or confusion | Tampering, wrong writer/environment/key, duplicate/unknown fields, unsupported kind/version/algorithm | Reject before returning trusted data | Stable metadata-only failure |
| Terminal attempt | Previously verified terminal record; proposed changed record | Reject every mutation, including writer replacement | Exact replay may be a no-op |
| Supersession | Nonterminal predecessor plus verified higher-epoch takeover linked to it | Only successor writer can terminalize predecessor as superseded | Missing/conflicting takeover evidence rejects |
| Administrator action | Executor, workflow, monitor or deputy key claims Administrator | Reject despite valid cryptographic signature | Fail closed on shared identity/key enrollment |

</frozen-after-approval>

## Code Map

Paths below are relative to `references/Hexalith.Builds`.

- `src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs`, `ModuleManifestLoader.cs`, `JsonDuplicatePropertyValidator.cs`: reuse strict parsing and embedded, offline JSON Schema patterns.
- `src/libraries/Hexalith.Builds.Tooling/RunEvidence/ModuleRunEvidenceWriter.cs`: deterministic UTF-8 encoding pattern; leave existing evidence contracts unchanged.
- `src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj`: existing shared assembly, central schema dependency and embedded resources; presently bundled with CLI packages, not independently published.
- `test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj`: existing xUnit/Shouldly project; no new package dependencies needed.
- Platform `ARCHITECTURE-SPINE.md`, “Binding classes and records” and “Attempt ownership”: authoritative field classes, writers and takeover semantics. Previous completed 4.0 spec supplies continuity, not record schemas. Existing development JWTs grant no hosted authority.

## Tasks & Acceptance

**Execution:**
- [ ] `schemas/hexalith.release-records.v1.json` — define eight closed, individually identified versioned branches and their writer/signature envelopes; embed through the existing Tooling project file.
- [ ] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/{Release,Attempt,Qualification,ProductionPromoted,ReleaseAvailable,LatestWorkingPointer,PromotionStop,Administrator}Record.cs` — add typed payloads, one type per file. Release binds artifact/host/catalog/McpCli/profile/contract/suite identities, sidecar patch, qualified sets and classifications. Attempt binds release, kind, environment-current values, epoch, timing/deadlines/grace, recovery allowance, observed stop and outcome. Other kinds bind their issuing attempt, release/profile/qualified sets or observed action/stop as applicable.
- [ ] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/{SignedRecord,RecordTrustPolicy,ReleaseRecordCodec}.cs` — implement bounded strict decoding, deterministic signing bytes/digests and P-256/SHA-256 signatures using platform cryptography. Cover every accepted field; reject ambiguous JSON. Private keys remain caller-held; trust configuration contains public anchors and explicit scopes.
- [ ] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs` — validate writer matrix, referenced verified records, immutable terminal state, linked takeover and monotonic latest-working moves. Keep verified inputs immutable; unverified DTOs cannot masquerade as verified context.
- [ ] `test/Hexalith.Builds.Module.Tests/ReleaseRecords/{RecordSchema,RecordSignature,RecordTransition}Tests.cs` — test every matrix row and all eight kinds with real ephemeral keys, including key aliases, cross-environment replay, stale epochs, unauthorized stop clearance, post-verification mutation and deterministic encoding vectors.
- [ ] `ReleaseRecords.md` — document schemas, fields, writer matrix, signing format, trust/custody assumptions and CAS integration examples; preserve the dirty root README.

**Acceptance Criteria:**
- Given each of the eight record kinds, when encoded and verified, then its versioned schema identifies its writer and only an independently authorized writer signature is accepted.
- Given an existing terminal attempt or a proposed supersession, when transition validation runs, then immutability and successor-writer authority are enforced against verified prior state.
- Given an Administrator record, when verified, then only an independently enrolled Administrator identity with exclusive declared custody is accepted.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

One schema resource may contain eight discriminated branches with distinct schema identifiers; each kind remains independently versioned. Keep these Platform records separate from existing Builds package-release receipts. Reuse Tooling rather than extending the two-package publication pipeline. Signature validity and state-transition authority are separate checks; neither alone establishes a committed store write.

## Verification

From the owning Builds repository:

- `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` — build with existing warning gates.
- `dotnet test test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug --no-build` — existing and new cases pass; use built-assembly xUnit class filters for focused triage.
- `git diff --check -- schemas src/libraries/Hexalith.Builds.Tooling/ReleaseRecords src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj test/Hexalith.Builds.Module.Tests/ReleaseRecords ReleaseRecords.md` — changed files have no whitespace errors.
