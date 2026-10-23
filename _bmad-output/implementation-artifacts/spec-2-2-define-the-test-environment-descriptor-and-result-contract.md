---
title: '2.2 Define the test environment descriptor and result contract'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '46a5b3a289892fc06a4db7afc0db8b765aed4a3b'
builds_baseline_commit: '2210aaaf54c779b8763dbf1e8767bdc1b422bc6d'
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

- [x] `docs/contracts/test-environment-v1.md` — document fields, population states, version rejection, outcome/hold semantics, shared descriptor/result bindings and downstream responsibilities; link from `README.md`.
- [x] `docs/contracts/schemas/hexalith.test-environment.v1.json` and `hexalith.test-result.v1.json` in that directory — publish self-contained Draft 2020-12 schemas with closed objects and typed reserved fields; define all policy handoff bindings without requiring future producers now.
- [x] `references/Hexalith.Builds/schemas/hexalith.test-environment.v1.json` and `hexalith.test-result.v1.json` in that directory — mirror canonical schema content for independent consumption.
- [x] `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentContractReader.cs` and `TestEnvironmentContractReadResult.cs` in that directory — implement descriptor/result reads returning detached validated JSON or diagnostics, explicit version/policy refusal and bounded strict parsing; never return partial documents or fetch remote schemas.
- [x] `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj` — embed mirrors; `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj` — include both packaged schemas; document consumption in Builds `README.md` while preserving its existing edits.
- [x] `references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs` and `references/Hexalith.Builds/test/fixtures/module/test-environment-v1/descriptor.json` and `result.json` there — test published/embedded agreement, endpoint consumption, every matrix row, four outcomes, hold boundaries and later population; samples confer no runtime qualification.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — synchronize only Story 2.2 as the workflow advances; preserve Epic 2 and unrelated statuses.

**Acceptance Criteria:**

- Given the published schemas, when a module author validates descriptor/result samples, then every Story 2.2 field and Story 2.1 handoff binding has a defined representation and lifecycle responsibility.
- Given unsupported descriptor/result versions, when the Builds consumer reads them, then it refuses and names supported versions.
- Given future candidate/loaded-identity values, when reserved fields are populated, then v1 still validates without granting evidence acceptance.
- Given terminal results, when consumers validate them, then precisely the four required outcomes are accepted independently of cleanup disposition.

### Review Findings

Code review pass 2 (2026-10-22). Layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor — none failed. Claims were reproduced with a probe against the built reader (`Hexalith.Builds.Tooling.dll` from 19:22, matching the reviewed source). During the review a concurrent session committed the reviewed files unchanged: Builds `66b9136`, plus the Platform gitlink.

- [x] [Review][Patch] (decision resolved 2026-10-22: option a, recommendation adopted) Terminal outcome and cause are not coupled — `$defs.terminal` lets `outcome` and `cause` vary independently. `passed`+`hold-expired`, `passed`+`startup-timeout` and `withdrawn`+`tests-completed` all validate. `FourOutcomesRemainIndependentOfCleanup` asserts `withdrawn`/`cancelled` with `tests-completed`. The policy says a withdrawn run can never become a pass. Choose: an outcome→cause mapping (plus `withdrawn` ⇒ attached role), or document `cause` as advisory. (medium; blind+auditor+edge)
- [x] [Review][Patch] (decision resolved 2026-10-22: option a, recommendation adopted) Integer-typed fields accept spellings and magnitudes the reader and consumers misread — a hold of `1800e-4294967296` with `expiresAt == acceptedAt` validates: the schema sees 1800 and the reader's decimal sees 0, which bypasses the hold arithmetic check. `attemptSequence` `1e-4294967296` and `1e308` validate. Holds of `3e2`/`300.0` validate, then `JsonElement.GetInt32` throws on the validated document. Choose: the reader refuses integer fields that are not plain Int32, or document `GetDecimal` consumption and add schema maximums. (medium; edge+auditor)
- [x] [Review][Patch] (decision resolved 2026-10-22: option a, recommendation adopted) Endpoint URIs can carry credentials into retained results — `$defs.endpoint.uri` accepts `http://user:secret@host/?token=…`. Results outlive resource deletion, and the policy requires redacted results. Choose: forbid userinfo/query credentials (secrets travel on a separate channel), or require result producers to redact endpoints. (medium; blind)
- [x] [Review][Patch] (decision resolved 2026-10-22: option a, recommendation adopted) Run/hold identity relationships are undefined and unchecked — the attached `run` branch requires both `runId` and `attachedRunId` without defining how they differ. A hold with `environmentId: other-env`/`ownerRunId: other-owner` validates inside a `sample-environment` descriptor, and `role: owner` with `runId ≠ ownerRunId` validates. Choose: define and enforce the bindings, or define them in the doc only and leave enforcement to Story 2.6. (medium; blind+auditor+edge)
- [x] [Review][Patch] (decision resolved 2026-10-22: option a, recommendation adopted) `composition` and `catalog` cannot be `not-yet-populated` — `environment.required` forces them, so Epic 2 producers must invent identities. The doc (line 209) assigns catalog/composition identity to Epic 3, and Story 3.3 names the run-scoped catalog instance in the descriptor. Choose: add the reserved branch, or keep them required and correct the doc's ownership line. (medium; auditor)
- [x] [Review][Patch] (decision resolved 2026-10-22: option a, recommendation adopted) `termination.failures` and `cleanup.failures` have no defined referent — these are bare identity arrays that are never resolved, so Stories 2.5 and 2.7 will produce and consume them inconsistently. Choose: item/resource IDs that resolve in the document, or diagnostic IDs, or doc-only. (medium; blind)
- [x] [Review][Patch] Non-`expired` hold end at/after `expiresAt` is accepted [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentContractReader.cs:172] — a 300 s hold ending at 15:00 or exactly 12:05 with `terminal-outcome` validates. Expiry is exclusive and the hold ends at the earliest of outcome, expiry or withdrawal. (medium; blind+auditor+edge)
- [x] [Review][Patch] Scoped identity guard covers only resources, endpoints and diagnostics [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentContractReader.cs:263] — these all validate: duplicate startup `taskId` (passed and failed), duplicate readiness `resourceId`, readiness for a nonexistent resource, duplicate hold `originalAttachmentRequestId`, duplicate completion `testId`, duplicate termination item `id`. (medium; blind+edge)
- [x] [Review][Patch] Shared descriptor/result `$defs` parity is untested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:29] — about 1,400 lines are duplicated across the two schemas. They are identical today, but no test keeps them aligned. (medium; blind)
- [x] [Review][Patch] `not-overridden` startup budget other than 600 s is never tested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:221] (medium; verification)
- [x] [Review][Patch] `MismatchedTinyStartupOverridesAreRefused` never reaches the override comparison [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:379] (medium; verification)
- [x] [Review][Patch] No positive read of populated `termination`/`retention`/startup tasks/attached run, and the sample read never asserts `terminal.outcome` [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:319] (medium; verification+auditor+blind)
- [x] [Review][Patch] Nested qualification/decision `policyId` refusal is untested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:89] (medium; verification)
- [x] [Review][Patch] `pr-merge` decision without `pr` is never refused in tests [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:319] (medium; verification)
- [x] [Review][Patch] Omitted-hold 1,800 s schema default is untested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:204] (medium; verification)
- [x] [Review][Patch] HXT101/HXT102 `Message`/`Source` metadata-only and policy-naming claims are unasserted [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:61] (medium; verification)
- [x] [Review][Patch] CLI package contract check omits both new schemas [references/Hexalith.Builds/Tools/test-g4-tool-package-contracts.ps1:1026] (medium; verification+blind)
- [x] [Review][Patch] BOM-counted byte bound is untested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:286] (low; verification)
- [x] [Review][Patch] Wrongly typed `schema` version is untested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:61] — the reader already refuses `42` with HXT101. (low; blind)
- [x] [Review][Patch] Unrepresentable hold/deadline timestamps refuse as HXT103, but the doc table assigns them to HXT106 [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentContractReader.cs:125] (low; blind+edge)
- [x] [Review][Patch] Doc understates identity rules: leading/trailing whitespace is excluded and length is capped at 2,048 [docs/contracts/test-environment-v1.md:47] (low; blind)
- [x] [Review][Patch] Samples link points into the submodule path and 404s on GitHub [docs/contracts/test-environment-v1.md:13] (low; blind)
- [x] [Review][Patch] Builds README says samples leave unavailable producers `not-yet-populated`, but they populate fictional isolation/readiness/tasks/attachments [references/Hexalith.Builds/README.md:226] (low; auditor)
- [x] [Review][Defer] Non-seekable input HXT105 path is untested [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentContractReader.cs:74] — deferred: needs a platform-specific FIFO harness; the cost exceeds the protection in this change.
- [x] [Review][Defer] Platform mirror parity unittest is not run by any automated runner [tests/contracts/test_test_environment_contract_mirrors.py:1] — deferred: Platform has no Python test runner or CI workflow yet, and plain `discover -s tests` finds zero tests (no `__init__.py`). Wire it into the future contract verification script.

Rejected:

- low — Cleanup `complete` with leftovers/failures, `not-retained` with IDs, and CI `failure-retained` all validate. These are runner lifecycle coherence rules that the doc assigns to Stories 2.5/2.8, the fix adds guards, and only a buggy runner produces them.
- low — Task `finishedAt` before `startedAt`, `passed` without `finishedAt`, `recordedAt` before the startup origin, and partial `not-declared` execution fields. These are observational chronology fields that no decision reads, and the fix adds guards.
- low — Candidate/decision/`reuseLineage` cross-field agreement is unchecked. The doc assigns this proof verification to Epic 3, and the reader grants no evidence.
- low — Top-level `diagnostics` has no `not-yet-populated` state. An empty list is a truthful producer statement, and reshaping it adds complexity.
- low — NBSP/U+3000/U+2028 identities pass as nonblank. Identities are runner-issued, and the fix needs a non-portable regex dialect change in four schema copies.
- low — Commit SHAs accept uppercase, and that is untested. Git emits lowercase, and narrowing the pattern is a guard.
- low — FIFO blocks before the `CanSeek` check. This was rejected previously; the fix needs platform-specific file-type detection.
- low — Cancellation during parse returns an accurate refusal instead of OCE. The window is microseconds.
- low — Refusal `field: "$"` and the `ToolPhase.Manifest` tag. The fixed location is documented, the Manifest phase means "parsing and validation", and no consumer routes by phase.
- low — Year `0000` in non-timing timestamps is accepted. The input is unlikely, and the fix is a guard.
- false — The mirror test fails on a clean checkout because the Builds schemas are untracked. Builds `66b9136` now commits both schemas, and the Platform gitlink points to it.
- false — CRLF/LF divergence breaks mirror parity. Neither repo sets eol attributes or `core.autocrlf` for these paths, so one checkout converts both identically.
- rejected — The "79 focused tests" claim is stale. The fix would edit the spec under review, and Final verification already records 119.

### Review Findings — pass 4 (2026-10-23)

Layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor — none failed. Scope: Story 2.2 files only. That is Platform `46a5b3a..HEAD` and Builds `2210aaaf..90d364d`. Story 2.4's `TestEnvironmentDescriptorWriter.cs`, its README section and unrelated csproj lines were excluded. The Builds mirror schemas were excluded because they are byte-identical to the Platform copies. The late-pass claim was reproduced against the built reader (`Hexalith.Builds.Tooling.dll` 2026-10-23 08:50, newer than reader commit `90d364d`): all six contradictory attached results returned `valid=True`, and the control returned HXT107.

- [x] [Review][Patch] An attached run's terminal outcome is not checked against its own hold [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentContractReader.cs:158] — all of these validate: `passed` recorded at 12:30 after the run's own hold expired at 12:05 (hold `ended/expired` or still `active`); `failed` after an `expired` end; `passed` after an `owner-cancelled` end; and `withdrawn`/`hold-expired` recorded at 12:01, before expiry or after a `terminal-outcome` end. The policy says a process reporting success after expiry cannot turn the withdrawn run into a pass. Fix for an attached result whose attachments are populated, using the current run's own hold: an ended hold's reason couples to the outcome (`terminal-outcome` ⇒ not `withdrawn`; `expired` ⇒ `withdrawn`/`hold-expired`; each withdrawal reason ⇒ `withdrawn` with the same cause); `hold-expired` ⇒ `recordedAt ≥ expiresAt`; a non-`withdrawn` outcome ⇒ `recordedAt < expiresAt` (exclusive expiry; the runner's record decides). Refuse with HXT107, update the doc's terminal/HXT107 text, and add negative tests for both contracts. (medium; auditor+blind+edge)
- [x] [Review][Patch] Refusal of a missing reserved binding or required result field is untested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:142] — if `decision` were dropped from `$defs/bindings.required`, or `cleanup`/`terminal` from the result root `required`, every test would stay green, because the reader never reads those objects. Add `<missing>` rows for `bindings.decision`, `cleanup` and `terminal` to `InvalidShapesNeverExposePartialDocumentsOrInputMetadata`. (medium; verification)
- [x] [Review][Patch] The `reuseLineage.sourceRunIds` must-be-empty rule is untested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:152] — only `sourceDecisionIds` and `reuseAllowed: true` are exercised, so removing `maxItems: 0` would make cross-run reuse representable with no test failing. Add a refusal row with a non-empty `sourceRunIds`. (medium; verification)
- [x] [Review][Patch] An invalid `contentIdentity` value is never refused in tests [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:142] — every fixture uses `Hash(char)`, so relaxing `^sha256:[a-f0-9]{64}$` would let version labels pass as content identities. Add refusal rows for a version label (`"1.2.3"`) and an uppercase-hex identity. (medium; verification)
- [x] [Review][Patch] Whitespace-padded identities and a blank override justification are untested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentContractReaderTests.cs:493] — the boundary theory uses only control characters, so dropping `\s` from the identity boundary classes would pass every test. Add `" sample"`/`"sample "` rows, and a `declared` override row whose `justification` is `" "`. (low; verification)
- [x] [Review][Patch] The doc never says which descriptor version a result snapshots [docs/contracts/test-environment-v1.md:71] — the policy's "descriptor/result contract versions" binding is met only implicitly, and the result's closed root cannot record it later without a v2. State that `hexalith.test-result.v1` always snapshots `hexalith.test-environment.v1` bindings, so a new descriptor version requires a new result version. (low; auditor)
- [x] [Review][Patch] The reader docs suggest a library that module tests can reference, but Tooling has `IsPackable=false` [docs/contracts/test-environment-v1.md:201] — the reader ships only inside the `hexalith-module` tool package. State that the reader is internal to the tool and that module fixtures validate descriptors against the published schemas. Make the same clarification in the Builds README consumption section. (low; blind)

Rejected (pass 4):

- false — Blind/Auditor: "a Story 2.4 section in the 2.2 contract doc contradicts it". The section came from concurrent Story 2.4 work in Platform `dcb01b1`, together with the `evidence/2-4` files. It documents 2.4 adoption within the stated "Stories 2.3–2.10 own … provisioning" split; review it under Story 2.4.
- false — Auditor: the same Story 2.4 section reported as out-of-scope provisioning content. Same refutation: it is not a Story 2.2 change.
- false — Edge (claim): "the producing-decision patch was not applied to the reader". R3-B10 approved a documentation patch only, and doc line 93 now names `producingDecisionId` → `decision.requestId`.
- low — Blind: decision/candidate/tool/reuseLineage cross-field references are unchecked. Same disposition as pass 2: proof verification belongs to Epic 3, the Story 2.4 producer leaves these `not-yet-populated`, and the fix adds guards.
- low — Edge: dangling `reuseLineage.producingDecisionId`/`producingRunId`. Same root and disposition as the cross-field references above.
- low — Blind: retention/cleanup resource IDs and `leftoverWorkIds` do not resolve against environment resources. Rejected as R3-B4/B5. Cleanup-owned resources may legitimately include volumes and networks that are not endpoint resources.
- low — Blind: dispositions contradict their lists (`complete` with leftovers, `not-retained` with IDs, CI `failure-retained`). These runner lifecycle coherence rules belong to Stories 2.5/2.8, as rejected in pass 2.
- low — Edge: cleanup `complete` with leftovers or unowned leftovers. Same disposition as above.
- low — Edge: termination `failures` and item `status` disagree. Same lifecycle-coherence disposition.
- low — Blind: non-hold timestamps are not parsed, so year `0000` passes and nothing is ordered. Rejected in pass 2: runner-issued input is unlikely, and the fix is a guard.
- low — Edge: year-`0000` `recordedAt`/`observedAt` makes a consumer's `DateTimeOffset.Parse` throw. Same disposition: unlikely runner output, and the fix is a guard.
- low — Blind: Draft 2020-12 `format` is annotation-only by default, and `\s`/`\d` differ between regex dialects. The doc already requires format-asserting validators, and the reader asserts formats. A fully portable pattern rewrite in four copies outweighs harm that is unlikely.
- low — Edge: a U+FEFF identity boundary passes .NET `\s` but fails ECMA-262. Runner-issued identities are unlikely to carry a BOM character, and this is the same dialect concern rejected in pass 2.
- low — Blind: refusals report `field: "$"` and a `Manifest` phase. Rejected in pass 2: the metadata-only design is deliberate, and no consumer routes by phase.
- low — Blind: the mirror unittest reads the working tree, raises a bare `FileNotFoundError` without the submodule, and has no meta-schema check; the ps1 check strips BOMs. Loud failures are acceptable, packaging copies bytes, and automation remains deferred.
- low — Blind: composition/catalog have no `state: populated`, `operatingSystem` is free-form, and counts semantics are loose. The first was the user's pass-2 option-a decision; the others are Epic 3 acceptance reconciliation.
- low — Blind: the samples are thin and the link points at `main`. The samples are illustrative, the tests cover the other shapes, and a branch link is the normal doc convention.
- low — Blind: `HasPlainIntegers` matches on property names. The verified name set equals every `type: integer` location in both schemas today, and closed v1 objects cannot gain fields without v2.
- low — Blind: a 1 MiB buffer is allocated per read, the static schema load fails as `TypeInitializationException`, and the directory/cancellation paths are untested. Reads happen about once per run, a packaging fault should fail loudly, and directories already map to HXT106.
- low — Edge: a FIFO path blocks before the `CanSeek` check. Rejected in passes 1 and 2, and the non-seekable test is already deferred: it needs platform-specific file-type detection.
- low — Edge: uppercase commit SHAs make ordinal comparisons fail. Rejected in pass 2: Git emits lowercase, and the failure is a loud refusal.
- low — Edge: readiness `ready` while a resource is failed or unobserved. The runner gates launch on its own observations, so this is a producer-coherence guard like the other rejected lifecycle rules.
- low — Edge: a startup task's `finishedAt` precedes `startedAt`. Rejected in pass 2 as observational chronology that no decision reads.
- low — Edge: `actualDurationSeconds` of `1e308` or a sub-tick value. The reader does no arithmetic on it, the producer is the runner, and a new bound is a guard.
- low — Edge: duplicate expected/loaded artifact entries. Rejected as R3-B2/B3: expected/loaded proof reconciliation belongs to Epic 3.

## Implementation Notes

- Published self-contained, closed Draft 2020-12 descriptor/result schemas and identical Builds mirrors. Shared bindings cover the complete policy handoff; reserved producers use typed `not-yet-populated` branches, with future candidate/baseline and loaded-identity branches available under v1.
- Added the offline `TestEnvironmentContractReader` and detached atomic read result. It rejects unsupported versions/policies, duplicate keys, malformed UTF-8/Unicode/JSON, unknown properties, excessive bytes/depth and inconsistent hold/deadline timing. Diagnostics use unused `HXT101`–`HXT107` and never echo input values, keys or paths.
- Embedded/package schema assets, documented Platform ownership and independent Builds consumption, and added diagnostic-only samples and 79 focused tests. Native handoff/internal records, services, tool/catalog pins and Git recording operations remain untouched by this story.
- Preserved concurrent unrelated Platform and Builds work, including other sprint statuses and additional schema resources in Tooling.csproj.

## Spec Change Log

## Review Triage Log


All three review layers returned. Each finding was checked before grouping. Corrections reuse existing contract fields and reader entry points; they add no new public API or producer responsibility. The full concurrent baseline diff is retained separately; independent review used the complete Story 2.2 implementation diff with unrelated concurrent changes and the claims file removed (the edge layer received claims separately).

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Blind 1: tiny unequal budgets underflow | medium | Reader converts positive declared decimals to zero and compares equality after loss; identical origin/expiry can then pass. Patch exact representable tick arithmetic and reject sub-tick/underflowing budgets. |
| Blind 2: identity edge controls | medium | The first/last `\S` bypass the middle class control exclusion; single control-character identifiers validate and reach endpoint/run consumers. Patch the existing identity constraint in all four schema copies. |
| Blind 3: FIFO blocks before seekability check | low | File.OpenRead can block on a named pipe before CanSeek; ordinary runner descriptors are local regular files. Reject this rare unsupported input concern: nonblocking special-file detection adds platform-specific machinery to a synchronous file reader. |
| Blind 4: leap-second inconsistency | low | Schema permits :60 in terminal time but DateTimeOffset arithmetic cannot represent it in deadlines. Patch the existing UTC constraint consistently to the reader's representable seconds 00–59 and document it. |
| Blind 5: duration without execution deadline | medium | The closed not-declared branch excludes the already-defined origin and actual-duration fields, preventing known execution observations without an invented timeout. Patch that branch to permit the existing timing fields independently of an expiry. |
| Blind 6: nonexistent cleanup attempt required | medium | A populated not-requested cleanup requires attemptId although no attempt exists. Patch the existing attempt requirement by disposition and remove invented attempts from samples/tests. |
| Blind 7: premature expiry end | medium | Reader only compares hold end with acceptance, so an expired reason before its bound expiry passes. Patch the demonstrated contradictory chronology check. |
| Blind 8: duplicate resource/endpoint identities | medium | Closed schemas permit duplicate resource IDs and endpoint names with distinct URIs; the reader has no uniqueness guard, leaving test endpoint selection ambiguous. Patch checks for these demonstrated duplicate states and document selection identities. |
| Blind 9: dangling diagnostic IDs | medium | terminal/readiness/task diagnosticIds can name no top-level classified diagnostic; schema/timing guards do not resolve them. Patch document-local resolution and unique diagnostic IDs, preserving opaque external reference contents. |
| Blind 10: canonical parity unprotected | medium | The automated test compares Builds disk/embedded assets only; canonical Platform edits are covered solely by the one-time manual comparison. Patch a narrow Platform-side parity test and execute it. |
| Edge 1: fractional deadline loses tick | medium | AddSeconds(double) can produce .5699999 for a valid .57-second interval. Same numeric precision root as Blind 1; patch exact integral tick computation with supported precision. |
| Edge 2: boundary control characters | medium | Verified identity pattern defect independently, including NUL at string edges. Same root and patch as Blind 2. |
| Verification 1: execution-expiry negative path untested | medium | Filed regression evidence shows only valid execution timing and negative startup/hold cases; removing execution arithmetic validation would leave these tests green. Patch descriptor/result negative execution-expiry cases. |
| Verification 2: end-before-acceptance path untested | medium | Filed regression evidence shows only a valid actual hold end; removing chronology validation would leave tests green. Patch both reader methods, including equality as the valid boundary. |


Review corrections were inspected individually in `/tmp/story-2-2-review-fixes.patch`. Applied every accepted patch: exact decimal/exponent-to-tick arithmetic; controls and leap-second constraints; existing deadline-free execution observations; conditional cleanup attempt identity; expired/end chronology; scoped resource/endpoint uniqueness; document-local classified diagnostic references; direct negative timing cases; and the Platform parity unittest. No intent gap, spec loopback or deferred work remains. Existing entry points, v1 identities and the frozen approved block are preserved.

## Verification

From Builds: `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1`, then invoke its built xUnit v3 assembly with `-class Hexalith.Builds.ModuleTool.Tests.TestEnvironmentContractReaderTests`. Run relevant existing schema/handoff regressions. Verify CLI package-item inclusion without publishing, canonical/mirror JSON equality, local documentation links and whitespace. No services are needed for contract checks.

### Executed verification (2026-10-22)

- The shared Builds Debug build was attempted as specified. It is blocked by concurrent `CheckSuites`/`ReleaseRecords` compilation/analyzer errors, unrelated to Story 2.2. The latest shared retry has no Story 2.2 errors; see `/tmp/story-2-2-shared-build-final.log` (38 concurrent-work errors). No build gate was weakened and no concurrent source was removed or edited.
- Isolated verification used `git archive 2210aaaf54c779b8763dbf1e8767bdc1b422bc6d | tar -x -C /tmp/story-2-2-builds-verify`, then copied the eight Story 2.2 Builds files listed in `/tmp/story-2-2-builds-verify/story-2-2-copied-files.txt`, byte-compared them and added only the two Story 2.2 schema entries to the archived baseline project files. This created no branch/index changes and excluded concurrent code solely in that temporary snapshot.
- In that snapshot, `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1 -p:MinVerVersionOverride=1.0.0` passed with zero warnings/errors. The version override supplies metadata in the archive without Git history; it changes no dependency or tool pin. Log: `/tmp/story-2-2-isolated-build.log`.
- `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class Hexalith.Builds.ModuleTool.Tests.TestEnvironmentContractReaderTests` passed all 79 tests, zero failures/skips. Log: `/tmp/story-2-2-contract-tests.log`.
- The same assembly with `-class Hexalith.Builds.ModuleTool.Tests.PlatformManifestValidationTests -class Hexalith.Builds.ModuleTool.Tests.NativeTestExecutorTests -class Hexalith.Builds.ModuleTool.Tests.NativeTestReportLoaderTests` passed all 180 relevant existing regressions, zero failures/skips. Log: `/tmp/story-2-2-regression-tests.log`.
- All verified Story 2.2 code, schemas, tests and fixtures still byte-match the shared tree. Both canonical/mirror pairs match byte-for-byte; schema references are local and objects closed. Evaluated CLI `None` items include both schemas with `Pack=true` and `PackagePath=tools/net10.0/any/schemas/`, without publishing. New local documentation links and whitespace checks pass. No services were needed or started.

- Parent verification: independently reran all 79 contract tests with XML results at `/tmp/story-2-2-parent-contract-tests.xml`; all passed, zero skips. Audited every matrix row against executed tests: detached samples; version refusals; policy/shape/strict JSON/size/depth refusals; unpopulated and future-populated bindings; invalid outcomes and hold requests. Confirmed the verified task files still match the shared tree, canonical/mirror schemas and shared schema definitions agree, and the frozen approved block is unchanged (SHA-256 `95765eb38845f04298b2cbb115c0e4912a8998d4188c885f9e04fff2f0f609b8`). Full baseline diff, including concurrent changes, is staged as `/tmp/story-2-2-full-t0an20sq.patch`; Story 2.2 review scope is `/tmp/story-2-2-task.patch`.


### Final parent verification after review corrections (2026-10-22)

- The earlier concurrent shared-build blocker is resolved. From `references/Hexalith.Builds`, `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` passed with **zero warnings and zero errors**. Log: `/tmp/story-2-2-shared-parent-final-build.log`.
- Against that integrated shared build, `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class Hexalith.Builds.ModuleTool.Tests.TestEnvironmentContractReaderTests -class Hexalith.Builds.ModuleTool.Tests.PlatformManifestValidationTests -class Hexalith.Builds.ModuleTool.Tests.NativeTestExecutorTests -class Hexalith.Builds.ModuleTool.Tests.NativeTestReportLoaderTests -result-xml /tmp/story-2-2-shared-final-tests.xml` passed **299 tests**: 119 contract cases and 180 existing regressions, zero failures/skips/not-run. Independently parsed XML confirms all counts and passes. The same final 299 tests also passed in the isolated snapshot.
- `python3 -m unittest discover -s tests/contracts -p test_test_environment_contract_mirrors.py -v` passed the canonical/mirror parity unittest. Both schema pairs and all shared definitions match; updated objects remain closed and references local. New-file whitespace, tracked diff whitespace and contract documentation links pass.
- Rechecked every matrix row against executed contract tests, including all review timing/identity/reference corrections; all tasks remain complete. Refreshed `/tmp/story-2-2-review-tay5xwek.patch` for all Story 2.2 implementation changes and retained the full concurrent baseline diff separately. No services, remote operations, commits, releases or pin changes were performed by this story.

- Completion honors the approved frozen exclusion of Git recording operations: changes remain uncommitted, overriding the generated presentation step's default local-commit action.


### Review pass 2 corrections and verification (2026-10-22)

- Applied all 23 accepted patch findings, including the six recorded option-a decisions. Terminal causes now map to their outcomes and withdrawal requires an attached role; integer fields require plain Int32 spellings; retained endpoint snapshots exclude userinfo, queries and fragments. The reader enforces owner/attached aliases and hold environment/owner bindings, non-expired hold ends before exclusive expiry, scoped producer identities and readiness resource references. Composition/catalog identities can explicitly remain unavailable. Termination failures resolve item IDs across owned-work/access lists; cleanup failures resolve IDs in that cleanup's owned-resource list, with explanatory diagnostic IDs kept separate.
- Added direct coverage for shared descriptor/result definitions, all cause/outcome combinations, integer spellings/magnitudes and boundaries, endpoint secrecy, run/hold references, unavailable composition/catalog, failure references, producer scopes, default/override timing, positive operational population, nested policies, missing PR bindings, BOM-counted byte limits and stable version/policy metadata. Corrected identity/timestamp diagnostics, the cross-repository sample link and the Builds sample description. The CLI package-contract script now checks both packaged and embedded test schemas for exact source agreement.
- The required shared-tree command `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` is currently blocked by **37 errors in concurrent runtime/descriptor-writer work**, with none reported in the Story 2.2 reader. No unrelated sources or gates were changed. Log: `/tmp/story-2-2-review2-build.log`.
- Archived committed Builds baseline `66b9136a0b223925e51177a51eb48852e2f12787` into `/tmp/story-2-2-review2-verify.lg14d0of`, copied the six modified Builds story files and byte-compared them to the shared tree. Its Debug test-project build with `-m:1 -p:MinVerVersionOverride=1.0.0` passed with **zero warnings/errors**. The override provides archive version metadata without changing any dependency/tool pin. Log: `/tmp/story-2-2-review2-isolated-build.log`.
- Ran the built xUnit assembly with `-class Hexalith.Builds.ModuleTool.Tests.TestEnvironmentContractReaderTests`: **193 tests passed**, zero failures/skips/not-run. The same assembly with `PlatformManifestValidationTests`, `NativeTestExecutorTests` and `NativeTestReportLoaderTests` passed **180 existing regressions**. XML: `/tmp/story-2-2-review2-contract-tests.xml` and `/tmp/story-2-2-review2-regression-tests.xml`.
- Canonical/mirror Python unittest passed. Shared definitions match, objects remain closed and references local; contract documentation links and scoped whitespace checks pass. Evaluated CLI package items include both schemas with `Pack=true` and the existing schema package path. Executed the updated package-check blocks offline against synthetic ZIPs: exact content passes and each missing/tampered schema is refused; the compiled Tooling embedded schemas also match. Probe log: `/tmp/story-2-2-review2-package-probe.log`. The full lifecycle package script was not run because it exercises downstream resources outside this story.
- The approved frozen block remains byte-identical (SHA-256 `95765eb38845f04298b2cbb115c0e4912a8998d4188c885f9e04fff2f0f609b8`). Only Story 2.2 advances to review. The two explicitly deferred findings remain deferred; no services, Git recording operations, releases, tool/catalog pins or unrelated edits were performed.

### Resumed independent review triage (2026-10-22)

All three layers completed against the full Story 2.2 implementation diff at `/tmp/story-2-2-final-review-9dwec7i9.patch`; the full concurrent baseline diff is retained at `/tmp/story-2-2-complete-baseline-ieze4cti.patch`. Edge-case review returned no findings. Findings below were graded separately before routing.

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| R3-B1: one attached run can have conflicting hold lineages | medium | `HasUniqueIds` checks request IDs only; distinct requests with the same attached-run identity can return contradictory expiries. The immutable policy already says that issued attached-run identity resolves to its original request. Patch the existing hold uniqueness check and directly test the conflicting records. |
| R3-B2: contradictory expected artifact claims | low | The typed representation accepts repeated artifact/module entries with different hashes. The reader grants no evidence and the approved intent excludes evidence acceptance; expected/loaded proof reconciliation is explicitly assigned to Epic 3. Reject this additional producer-proof guard within the offline contract reader. |
| R3-B3: contradictory loaded artifact claims | low | The typed observations likewise accept differing hashes for the same consumer. These remain producer claims and do not pass an evidence gate here; validating loaded proof is excluded downstream work. Reject the additional tuple-key/proof guard. |
| R3-B4: dangling termination leftover IDs | low | `leftoverWorkIds` is represented but only the separately approved failure IDs are resolved locally. The reader performs no stopping or cleanup; item/leftover lifecycle reconciliation belongs to the excluded runner work, as with the previously rejected cleanup coherence guards. Reject this new guard for a buggy future producer. |
| R3-B5: dangling cleanup leftover IDs | low | `leftoverResourceIds` can name an absent owned item, but no retry or deletion uses these records in this reader. Ownership-limited cleanup reconciliation remains downstream lifecycle work. Reject this additional guard, consistent with the earlier cleanup-coherence disposition. |
| R3-B6: equal run and attempt strings | low | Equality is accepted despite the prose describing distinct attempt identity. These are runner-issued, separately named fields and no current consumer conflates their namespaces; a new inequality guard addresses an unlikely producer defect without demonstrated consumer harm. Reject the guard. |
| R3-B7: unusable HTTP authority or port | medium | The URI schema accepts `http://:1234/` and `http://127.0.0.1:999999/`, which cannot be consumed as .NET endpoints. A direct System.Uri probe refuses both and accepts normal HTTP, IPv6, gRPC and custom-scheme examples. Patch existing endpoint validation to require a representable absolute URI and test both contracts. |
| R3-B8: installed package does not call the new reader | low | Package checks verify the exact compiled Tooling schemas; existing installed manifest validation exercises the same Json.Schema dependency, and the reader only reads its supplied input and embedded assets. No package-only reader failure is demonstrated. Reject the additional outside-checkout harness for this covered dependency path. |
| R3-B9: canonical mirror test lacks automation | low | carried — same location and claim as the explicitly deferred mirror-automation finding and existing deferred-work entry. Preserve its prior defer route; do not patch or append it again. |
| R3-B10: producing decision field target is implicit | low | The sole decision identity is `decision.requestId` and the populated fixture already uses it for `reuseLineage.producingDecisionId`. Patch documentation to name that existing relationship directly. |
| R3-V1: result-only diagnostic references lack negative tests | medium | Accepted filed regression evidence: restricting recursive resolution to `bindings` would leave the negative readiness test green while accepting dangling terminal/termination/cleanup references. Patch contract tests with direct atomic HXT104 cases at those result locations. |

The four surviving corrections are patches to existing entry points and representations; each has a demonstrated state or direct documentation correction, adds no public surface, and requires no new intent decision. The approved frozen block remains unchanged.

### Final verification after resumed review (2026-10-22)

- Applied all four surviving review patches: unique attached-run identities across hold lineages, absolute endpoint representability, the explicit producing-decision reference, and negative result-only diagnostic tests. The focused isolated Debug rebuild passed with zero warnings/errors; all 12 added cases passed. Logs: `/tmp/story-2-2-review3-targeted-build.log` and `/tmp/story-2-2-review3-targeted-tests.xml`.
- Parent verification ran the complete contract and related regression selection against the final isolated Debug assembly: **385 passed**, zero errors/failures/skips/not-run. This includes **205 contract cases**, **145 manifest cases**, **16 native-executor cases**, and **19 native-report cases**. XML: `/tmp/story-2-2-parent-final-tests.xml`; output: `/tmp/story-2-2-parent-final-tests.log`. Every acceptance-matrix row and all final review regressions have executed passing coverage.
- Parent verification confirmed all changed Builds code, tests, schemas and package-check script still byte-match the tested snapshot. A separately added Story 2.4 README section was preserved. Canonical/mirror parity unittest and scoped whitespace checks pass; package/embedded-schema evidence from the previous pass remains applicable because those assets and checks are unchanged.
- Retried the required shared-tree Debug test-project build. Tooling and CLI now compile, but **two concurrent AppHost errors** remain: `AppHostRunner.cs:36` and `:37` report CS0103 for an undefined `plan`. This is the exact remaining integrated-build limitation, superseding the earlier 37-error count. Command: `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1`; log: `/tmp/story-2-2-parent-final-integrated-build.log`. No unrelated code or build gate was changed.
- All review findings have a recorded disposition. The two previously approved deferrals remain deferred. The frozen intent SHA-256 remains `95765eb38845f04298b2cbb115c0e4912a8998d4188c885f9e04fff2f0f609b8`. Completion preserves the approved exclusion of Git recording operations; no commit or push was created. The spec is done and the sprint story remains review, as the presentation step requires.

### Review pass 4 corrections and verification (2026-10-23)

- Applied all 7 pass-4 patches. `HasConsistentAttachedOutcome` refuses an attached result with populated attachments (HXT107) in two cases. First, when `recordedAt ≥ expiresAt` for the run's own hold disagrees with `cause == hold-expired`. Second, when an ended own hold's reason disagrees with the outcome: `terminal-outcome` ⇒ not `withdrawn`; `expired` ⇒ `hold-expired`; each withdrawal reason ⇒ the same cause. Owner outcomes stay independent of other runs' holds. Added test rows for a missing `bindings.decision`/`cleanup`/`terminal`, a non-empty `sourceRunIds`, version-label and uppercase `contentIdentity`, whitespace-padded identities and a blank override justification. Two existing tests now use a cause consistent with the hold: the withdrawn row in `FourOutcomesRemainIndependentOfCleanup` uses `owner-cancelled`, and the valid `hold-expired` case is recorded at expiry. The contract doc now covers the attached-outcome check, the HXT103/HXT107 rows, descriptor/result version pairing and tool-internal reader packaging; the Builds README mirrors the packaging note. Schemas are unchanged.
- From Builds: `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1 -nodeReuse:false` passed on the shared tree with **zero warnings and zero errors**. The first two attempts failed on analyzer rules S3358 and IDE0046 in the new method; restructuring the ternary fixed both, and no rules were suppressed. Fresh DLLs: Tooling 10:22:20, tests 10:22:26.
- The built assembly with `-class` `TestEnvironmentContractReaderTests`, `PlatformManifestValidationTests`, `NativeTestExecutorTests` and `NativeTestReportLoaderTests` passed **410 tests**: 230 contract cases (205 plus the 25 new ones), 145 manifest, 16 native-executor and 19 native-report cases, with zero errors/failures/skips/not-run. `TestEnvironmentProvisioningTests` passed 28. Re-running the auditor's six late-pass probes against the new reader: all are refused with HXT107, the control fixture is valid, and the at-expiry `terminal-outcome` control is still HXT107.
- Platform canonical/mirror parity unittest passes; `git diff --check` is clean in both repositories. The frozen intent SHA-256 remains `95765eb38845f04298b2cbb115c0e4912a8998d4188c885f9e04fff2f0f609b8`. No commits, pushes, services, releases or pin changes were made; the concurrent Story 4.1 Platform changes were left untouched.
