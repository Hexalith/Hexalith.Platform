---
title: '4.6 Encode release, attempt, qualification and Administrator records'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '6fbe5a024650382e25bac7d47ac919457e8d393c'
builds_baseline_commit: '2210aaaf54c779b8763dbf1e8767bdc1b422bc6d'
builds_implementation_commit: 'c28c5c255226f85ef9533894b722d1ad99816b5b'
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
| Supersession | Nonterminal predecessor plus verified takeover linked to it at the current higher CAS epoch | Only successor writer can terminalize predecessor as superseded | Missing/conflicting takeover evidence rejects |
| Administrator action | Executor, workflow, monitor or deputy key claims Administrator | Reject despite valid cryptographic signature | Fail closed on shared identity/key enrollment |

</frozen-after-approval>

## Code Map

Paths below are relative to `references/Hexalith.Builds`.

- `src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs`, `ModuleManifestLoader.cs`, `JsonDuplicatePropertyValidator.cs`: reuse strict parsing and embedded, offline JSON Schema patterns.
- `src/libraries/Hexalith.Builds.Tooling/RunEvidence/ModuleRunEvidenceWriter.cs`: deterministic UTF-8 encoding pattern; leave existing evidence contracts unchanged.
- `src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj`: existing shared assembly, central schema dependency and embedded resources; presently bundled with CLI packages, not independently published.
- `test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj`: existing xUnit/Shouldly project; no new package dependencies needed.
- `{project-root}/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`: authoritative fields, writers and takeover semantics. Existing development JWTs grant no hosted authority.

## Tasks & Acceptance

**Execution:**
- [x] `schemas/hexalith.release-records.v1.json` — define eight closed, individually identified versioned branches and their writer/signature envelopes; embed through the existing Tooling project file.
- [x] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/{Release,Attempt,Qualification,ProductionPromoted,ReleaseAvailable,LatestWorkingPointer,PromotionStop,Administrator}Record.cs` — add typed payloads, one type per file. Release binds artifact/host/catalog/McpCli/profile/contract/suite identities, sidecar patch, qualified sets and classifications. Attempt binds release, kind, environment-current values, epoch, timing/deadlines/grace, recovery allowance, observed stop and outcome. Other kinds bind their issuing attempt, release/profile/qualified sets or observed action/stop as applicable.
- [x] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/{SignedRecord,RecordTrustPolicy,ReleaseRecordCodec}.cs` — implement bounded strict decoding, deterministic signing bytes/digests and P-256/SHA-256 signatures using platform cryptography. Cover every accepted field; reject ambiguous JSON. Private keys remain caller-held; trust configuration contains public anchors and explicit scopes.
- [x] `src/libraries/Hexalith.Builds.Tooling/ReleaseRecords/RecordTransitionValidator.cs` — validate writer matrix, referenced verified records, immutable terminal state, linked takeover and monotonic latest-working moves. Keep verified inputs immutable; unverified DTOs cannot masquerade as verified context.
- [x] `test/Hexalith.Builds.Module.Tests/ReleaseRecords/{RecordSchema,RecordSignature,RecordTransition}Tests.cs` — test every matrix row and all eight kinds with real ephemeral keys, including key aliases, cross-environment replay, stale epochs, unauthorized stop clearance, post-verification mutation and deterministic encoding vectors.
- [x] `ReleaseRecords.md` — document schemas, fields, writer matrix, signing format, trust/custody assumptions and CAS integration examples; preserve the dirty root README.

**Acceptance Criteria:**
- Given each of the eight record kinds, when encoded and verified, then its versioned schema identifies its writer and only an independently authorized writer signature is accepted.
- Given an existing terminal attempt or a proposed supersession, when transition validation runs, then immutability and successor-writer authority are enforced against verified prior state.
- Given an Administrator record, when verified, then only an independently enrolled Administrator identity with exclusive declared custody is accepted.

## Implementation Notes

- Implemented the eight immutable payloads, embedded schema, canonical ES256 codec, independent trust policy and verified-only transition API in Builds; documented integration in `ReleaseRecords.md`.
- Parent inspected the complete story diff, including untracked source/tests, at `/tmp/story46-task-39luvk74.diff`. Full concurrent workspace diff retained separately at `/tmp/story46-full-8w40d008.diff`; unrelated changes are preserved.
- Matrix audit: all five rows have executed passing tests in `/tmp/story46-focused-tests.xml` (78 passed; zero failures, skips or unrun cases). Focused harness imports Builds warning gates and pinned package configuration and compiles original source/schema paths in separate library/test projects; build has zero warnings/errors.
- Regression coverage includes DR-only takeover writers and exact canonical terminal replay including the signature. Verification commands and source hashes are in `/tmp/story46-verification-commands.txt` and `/tmp/story46-source-sha256.txt`.
- Normal module-project build is currently blocked by concurrent `CheckSuites/CheckSuiteContract.cs` SA1143 diagnostics at lines 191 and 232; full existing-suite verification remains outstanding. Exact output: `/tmp/story46-normal-build-final.log`. No warning gate was weakened.

- Final verification after review fixes: focused build is clean and 130/130 cases pass; the ordinary Debug module-project build has zero warnings/errors and the complete module suite passes 1789/1789, with zero failures or skips. The earlier concurrent blockers are resolved; one missing blank line was added to the renderer test without changing its statements.
- All 13 accepted review findings were patched across 11 root-cause entries, with no public API expansion. Parent inspected the patch delta and audited executed coverage again. Preserved initial phase progress, single automatic recovery (including resumption), fixed recovery notification lifetime and staging in-place restore.
- Retained evidence: [verification packet](evidence/story-4-6/verification.json), [focused per-case XML](evidence/story-4-6/focused-tests.xml), [module suite output](evidence/story-4-6/module-tests.log), and source hashes with the exact focused fallback harness. Hash audit confirms the 26 captured source/configuration inputs still match.

- Local Builds implementation commit: `c28c5c255226f85ef9533894b722d1ad99816b5b`. Only story-owned files and its schema-resource addition were staged; concurrent shared-file work was preserved. Both exact commit messages passed each repository’s pinned commitlint configuration. Full module verification used the combined working tree recorded in the packet; no remote operation was performed.

## Spec Change Log

## Review Triage Log

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| BH-01 | medium | The fixed-fields comparison masks rollout, verification and maximum-lifetime updates; the same-epoch branch has no established-deadline guard. Patch local transition checks and regression tests, preserving legitimate new phase deadlines and absolute recovery lifetime. |
| BH-02 | medium | Codec timing checks omit phase-before-entry and future-outage rejection, and ordinary phase bounds can exceed the declared lifetime. Patch elementary chronology; recovery lifetime remains a notification bound and must not become an abort limit. |
| BH-03 | false | Administrator actions always bind a signed action and target; an optional attempt reference supplies existing context. No operation is executed here, and the documented downstream entry/eligibility checks must bind newly entered recovery/takeover operations. The probe shows representable signed metadata, not an unauthorized operation or entitlement bypass. |
| BH-04 | false | The signed envelope subject must equal the action target. A target can name a release, environment or recovery rather than the referenced attempt ID, so forcing equality would conflate distinct fields; no caller executing an unrelated operation was demonstrated. |
| BH-05 | medium | Clearance checks current stop identity/revision but never reads CauseDispositions. Patch rejection of a missing or invalid disposition for the supplied current cause; downstream store retains responsibility for all outstanding cause history. |
| BH-06 | low | Staging in-place restore is required by Stories 4.23/5.10, so the code correctly admits its staging executor. The writer-table wording is ambiguous: directly clarify target-environment ownership, with production ownership when the target is production. |
| BH-07 | medium | The dollar end anchors admit a final newline in identifiers, digest strings and signature text; base64 decoding then accepts it. Patch whole-string matching and add signed-input regressions. |
| BH-08 | medium | NamedRecovery.MaximumDurationSeconds is Int32 while its schema maximum is 9007199254740991. Patch the schema maximum to Int32.MaxValue and verify the published/typed boundary; grouped with EC-03. |
| BH-09 | medium | Sign calls EnsureP256/SignData outside any expected-failure translation, contradicting its documented metadata-only RecordValidationException contract. Patch known invalid signing-key failures without changing null-argument behavior or exposing crypto exception content. |
| BH-10 | medium | Digest and enum mutations use schema-invalid values, so null verification alone does not establish signature coverage. Patch schema-valid independent mutations and assert invalid-signature; grouped with VG-01. |
| EC-01 | medium | The exact-envelope early return precedes currentEpoch validation for in-progress attempts. Patch active replay to require the current epoch while retaining terminal exact-replay no-ops. |
| EC-02 | medium | The identifier regex first/last non-whitespace classes allow NUL even though the interior class excludes controls. Patch the endpoint classes and test leading/trailing control characters; distinct from the end-anchor defect. |
| EC-03 | medium | The schema permits a named duration above Int32.MaxValue but typed decoding cannot represent it. Same verified contract mismatch as BH-08; one schema-bound patch. |
| VG-01 | medium | Pre-verified broken-verification gap: applicationPackageDigest tampering is currently schema-invalid, masking a missing signature binding. Patch the signature test with valid digest replacements and the exact invalid-signature result; grouped with BH-10. |
| VG-02 | medium | Pre-verified regression gap: ValidateReleaseAvailable has only a valid working-staging invocation. Add independent negative cases for outcome, environment, exact references and composed image, asserting invalid-transition. |

## Design Notes

One schema resource contains eight branches with distinct versioned schema identifiers. Keep Platform records separate from Builds package-release receipts. Reuse Tooling without extending the two-package publication pipeline. Signature and transition checks alone cannot establish a committed store write.

## Verification

From the owning Builds repository:

- `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` — build with existing warning gates.
- `dotnet test test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug --no-build` — existing and new cases pass; use built-assembly xUnit class filters for focused triage.
- `git diff --check -- schemas src/libraries/Hexalith.Builds.Tooling/ReleaseRecords src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj test/Hexalith.Builds.Module.Tests/ReleaseRecords ReleaseRecords.md` — changed files have no whitespace errors.
