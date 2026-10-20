---
title: 'Define the recovery hook contract'
type: 'feature'
epic: 4
story: 10
created: '2026-10-07'
status: 'done'
baseline_commit: '332a7d8104e3f6c9aaa57cbc7f07afb5fa0859de'
route: 'dispatch'
review_loop_iteration: 0
story_key: '4-10-define-the-recovery-hook-contract'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 4.10 requires one versioned recovery-hook contract before release records bind it. Platform currently has no shared schema or invocation/result contract.

**Approach:** Publish a versioned JSON Schema, normative protocol documentation, and a local conformance validator with fixtures for declarations, invocations and results. Later releases and module implementations consume this contract.

## Boundaries & Constraints

**Always:** Represent the Platform cut and scoped, typed positions reached at or before it for `authoritative-restore`, `rebuild-only` and `live-authority-only`, explicitly including EventStore position and Memories register sequence. Preserve module ownership of position encodings; never compare unrelated counters. Declare dependencies, hook capabilities and whether reopening waits for replay.

Recovery mode closes user ingress, disables external-effect/destructive-retention workers and admits only the owning executor, recovery workloads and probes. Define admission/purge → backend-principal/dynamic-credential re-provisioning and rotation → rebuild ordering, integrity checks and external-effect reconciliation. Fence hooks execute separately in the recovery executor sandbox with authority-specific custodian material; ordinary hooks execute in their owning workload. In-place restore omits fencing and surviving-authority reissue.

Specify an internal recovery-only endpoint, this environment's recovery-hook principal, issuer-validated credentials bound to the current recovery-kind attempt and epoch, and current-epoch checks at task commit. DR requires a fresh issuer on replacement capacity. Recheck epoch on retries and commits; reject stale in-flight work. Results bind version, release, environment/instance, attempt, epoch, module, hook/task and cut, with achieved positions, status, integrity, reconciliation, replay and reopening eligibility. Missing implementations remain unqualified; incomplete or uncertain results cannot establish success. Reopening remains an executor/operator decision after all required gates.

Every release binds the exact contract version. Support every version referenced by rollback targets or retained recovery-point releases; retire a major only when no such reference remains. Retained fixtures contain credential references/bindings only, never credential values.

**Never:** Implement module hooks, credential issuance, the attempt store, atomic persistence, publication workflows or live restoration. Modify submodules or imply that schema validation proves authentication, fencing, recovery qualification or runtime epoch enforcement.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Conforming chain | Supported declaration, invocation, result and expected context | Valid identities, cut/positions and phase results | Stable success output |
| Invalid shape | Unknown version/fields, duplicate keys/IDs or missing positions | Refuse conformance | Secret-safe diagnostic and nonzero exit |
| Authority mismatch | Wrong environment/attempt/epoch/mode or takeover before commit | Refuse invocation/commit; no success claim | Compare with independently supplied expected context |
| Incomplete recovery | Missing hook, failed integrity, unknown external effect or required replay unfinished | Unqualified/failed/incomplete; reopening blocked | Preserve explicit outcome |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/epics.md:2165` — Story 4.10/AR-43 requirements; 4.6/4.12 own later release integration.
- `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:334` — recovery sequence; version retention at 191, ownership at 476.
- `references/Hexalith.Builds/schemas/hexalith.module-manifest.v1.json` — reuse Draft 2020-12, fixed discriminator and closed shapes; read-only reference.
- `eng/` — existing Python tooling; no shared schema evaluator or release-record consumer exists.

## Tasks & Acceptance

**Execution:**
- [x] `schemas/hexalith.recovery-hook.v1.json` — define versioned declaration/invocation/result shapes and scoped cut positions; make required coverage machine-checkable.
- [x] `docs/contracts/recovery-hook-v1.md` — fix endpoint/request/result semantics, authorization and atomic commit obligations, phase/dependency ordering, retry identity, refusals, reopening and version lifecycle; distinguish fence execution.
- [x] `eng/recovery-contract/pyproject.toml` and `uv.lock` — declare and lock the JSON Schema validator dependency; avoid a custom schema engine.
- [x] `eng/recovery-contract/validate.py` — strictly load JSON, validate against the schema and apply cross-record/context semantic checks; expose a local CLI with stable, sanitized errors.
- [x] `eng/recovery-contract/test_validate.py` and `fixtures/*.json` — test every matrix row, mismatched identity/cut/result bindings, dependency cycles, unsupported versions, noncomparable positions, in-place/DR distinctions and replay exceptions. Simulate epoch change between invocation and commit using independent context; label fixtures synthetic.
- [x] `eng/recovery-contract/README.md` — document reproducible CLI commands and consumer responsibilities; link the normative contract and schema.

**Acceptance Criteria:**
- Given Story 4.10, when the artifacts are inspected, then every listed contract element has a normative definition and schema representation where applicable.
- Given a retained release version, when a consumer selects its contract, then exact version selection and retention rules are explicit without claiming absent release integration.
- Given the fixtures, when validation runs, then valid chains pass and every refusal/incomplete case produces the expected outcome without disclosing secrets or asserting live recovery.

### Review Findings

Second review, 2026-10-20 (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor). Baseline: 74/74 tests pass.

- [x] [Review][Decision] Recovery class is not mapped to the phase that reaches it — high (acceptance-auditor+blind-hunter). `targetPositions` must equal every cut position the module owns (validate.py:197), and a succeeded result requires every target reached (validate.py:248). So pre-rebuild hooks must claim `rebuild-only` targets they cannot have reached yet. In valid-chain.json, memories `admission-purge` (12:00:06) and `credentials` (12:00:12) report `memories-register` (rebuild-only) as `targetReached: true` before memories `rebuild` (12:00:18). An honest `targetReached: false` is refused (`success-with-unreached-position`), and `incomplete` blocks every later phase (`dependency-not-succeeded`). Separately, a module that owns a `rebuild-only` position may declare `rebuild: not-applicable` and still get complete+eligible, because nothing links classes to phases. Options: (A) per-class due phase: `authoritative-restore`/`live-authority-only` from the module's first hook on, `rebuild-only` from `rebuild` on; earlier success may report rebuild-only `targetReached: false`; owning a rebuild-only position forbids a `not-applicable` rebuild hook (doc, schema, validator, fixtures); (B) pre-rebuild invocations target only non-rebuild-only positions. Resolved 2026-10-20: option A adopted and applied.
- [x] [Review][Decision] Results before the gate phases claim gates that have not run — medium (acceptance-auditor+blind-hunter). Every result in valid-chain.json, including the fence at 12:00:02, reports integrity `passed`, effects `reconciled`, replay `complete` and `reopeningEligible: true` before any integrity or reconciliation hook runs. The published `result.json` example is that fence result. The validator accepts it because the doc only says earlier phases "may" report `not-run`. Options: (A) MUST `not-run` for gates whose phase has not executed (integrity before `integrity`, effects before reconciliation, replay before `rebuild`), and `reopeningEligible: true` only on reconciliation results; enforce in validator and schema; regenerate fixtures; (B) fixtures only, keeping "may"; (C) A plus a per-phase `phaseEvidenceRef` for fence/admission/credential proofs (new wire field). Resolved 2026-10-20: option A adopted and applied.
- [x] [Review][Decision] Exact-version schema location is undefined — medium (acceptance-auditor+blind-hunter). The doc requires explicit exact-version selection and immutable published schemas (recovery-hook-v1.md:18-24). But the only schema is major-named (`hexalith.recovery-hook.v1.json`, `$id` .../v1) with `const "1.0.0"`, and validate.py hard-codes `VERSION`/`SCHEMA`. A 1.0.1 would have to edit the "immutable" v1 file or invent a naming scheme, so 4.6/4.12 consumers would diverge. Options: (A) keep the v1 file as the immutable 1.0.0 schema; the doc names later exact versions `hexalith.recovery-hook.<version>.json` with matching `$id`; the validator selects the schema through a version map (the v1 path already appears in EventStore qualification file inventories); (B) rename now to `hexalith.recovery-hook.1.0.0.json` while nothing binds it. Resolved 2026-10-20: option A adopted (doc only).
- [x] [Review][Decision] ID uniqueness scopes are undocumented; two modules can declare one surviving authority — medium (edge-case-hunter+blind-hunter). The doc says IDs are unique "in their documented scope" (recovery-hook-v1.md:71-72), but no scope is documented and "result IDs" do not exist. The validator makes `positionId`/`moduleId` global but `authorityId`/`hookId` per declaration (validate.py:177). Two modules can therefore fence and reissue the same `authorityId` with different procedures, and custodians would release one authority's material to two sandboxes. Options: (A) `authorityId` unique across the declaration set (one owning module), and document every scope as implemented; (B) allow shared authorities and document per-module scope. Resolved 2026-10-20: option A adopted and applied.
- [x] [Review][Decision] Refusal reason codes have no registry — low (blind-hunter). Refusal bodies must be "exactly the stable, non-secret reason code" (recovery-hook-v1.md:183), but only `stale-epoch` is ever named, so module handlers will emit different codes for the same refusal. Options: (A) state that codes are owner-defined diagnostics and executors branch only on HTTP status; (B) add a normative registry mapping codes to 400/401/403/409/503. Resolved 2026-10-20: option A adopted and applied.
- [x] [Review][Patch] The schema leaves out single-record rules the validator enforces, so `--schema-only` and non-Python consumers accept forbidden records [schemas/hexalith.recovery-hook.v1.json:425] — medium (acceptance-auditor+edge-case-hunter+blind-hunter). Add `maxContains: 1` for each ordinary phase; forbid `not-applicable` for integrity/reconciliation/fence hooks; add result `if succeeded` gates (integrity not failed/unknown, effects not unknown) and the `reopeningEligible: true` preconditions, using whatever rules decisions 1-2 settle. In the README, state that `--schema-only` does not check intra-record ID uniqueness, which JSON Schema cannot express.
- [x] [Review][Patch] The doc says `missing` ALWAYS leaves the attempt unqualified, but the validator exempts in-place fences [docs/contracts/recovery-hook-v1.md:77] — medium (edge-case-hunter+verification-gap). An in-place chain with a `missing` fence returns complete+eligible (reproduced). The code matches the spec ("In-place restore omits fencing"). Correct the sentence to "unqualified for attempts that require that hook" and add a test pinning the exemption (validate.py:355-361).
- [x] [Review][Patch] Refusal guards and schema conditionals are unpinned by tests [eng/recovery-contract/test_validate.py:1] — medium (verification-gap+blind-hunter). Mutation runs left all 74 tests green when 37 of 73 guards or 66 of 72 schema constraints were removed, most then yielding complete+eligible. Add exact-code refusal tests for: required-hook-not-applicable / fence-coverage / fence-binding; position-coverage / release-binding / duplicate-position-scope / definition-owner / achieved-coverage; success-with-uncertain-integrity / external-effects-not-reconciled; unqualified aggregation, withheld final eligibility and CLI exit 3 for unqualified; duplicate-hook-task / invalid-retry-sequence / retry-after-terminal-outcome / retry-time-invalid / dependency-not-succeeded; invocation-context-time / result-time / context-time; executor-mismatch / ambiguous-quarantine-source / quarantine consts; the execution if/then/else; DR/in-place context conditionals; evidenceRef and reasonCode requirements; closedness of every record definition and the `scope` const. `endpoint-mismatch` and `result-cut-mismatch` are unreachable behind earlier equality checks; keep them as defense in depth.
- [x] [Review][Patch] Several tests exercise a different rule from the one they name, or accept any refusal [eng/recovery-contract/test_validate.py:395] — medium (verification-gap+blind-hunter+acceptance-auditor). `test_unsafe_reopening_claim` hits `rebuild-replay-incomplete`; retarget it to an admission-purge result with `integrity: not-run`. `test_changed_commit_issuer_and_principal` (:290) never changes the issuer; add issuer, audience, quarantine and cut subtests. `test_missing_cut_class_and_named_positions` (:197) is masked by `minItems: 3`; substitute kind/class instead and assert `invalid-shape`. `test_unknown_shape_and_unsupported_version` (:148), `test_duplicate_ids` (:205) and `test_unknown_fields_at_every_wire_boundary` (:189) should assert exact codes, and CLI refusals should assert empty stderr.
- [x] [Review][Patch] The timestamp def relies on `format: date-time` [schemas/hexalith.recovery-hook.v1.json:65] — low (edge-case-hunter). A trailing newline passes the schema (reproduced), and Draft 2020-12 treats `format` as annotation-only for other consumers. Add an RFC 3339 pattern ending `(?![\s\S])` that matches validate.py's timestamp regex.
- [x] [Review][Patch] The README attributes "the four matrix outcomes" to four fixtures that cover three rows (incomplete and failed share a row; the conforming row is valid-chain.json) [eng/recovery-contract/README.md:62] — low (blind-hunter). Reword the sentence.
- [x] [Review][Patch] Unused `$defs/token` [schemas/hexalith.recovery-hook.v1.json:30] — low (blind-hunter). Nothing references it; schema-to-type generators would emit a dead type. Delete it.

**Rejected:**
- false — `chain.synthetic` is optional and unenforced (acceptance-auditor, blind-hunter): it is required as a boolean; fixtures set `true`; standalone fixtures are labelled by the README and `synthetic-` values. `false` legitimately describes chains assembled from retained records, and reports always carry `runtimeQualified: false`.
- false — chain-level `reopeningEligible` is not labelled advisory (acceptance-auditor): recovery-hook-v1.md:258-264 says only the executor/operator reopens and that an offline validator MUST NOT, and the same report carries `runtimeQualified: false`.
- false — reissue may regress from complete to pending (edge-case-hunter): a pending commit context already forces `reopeningEligible: false` (validate.py:372); non-monotonic reissue is allowed (same verdict as the prior BH-5).
- false — `-h` exits 0 with non-JSON output (edge-case-hunter): this is standard help behaviour; no record is validated, and JSON-parsing callers fail loudly on the help text.
- false — the diff omits valid-chain.json, failed-chain.json and uv.lock (blind-hunter, verification-gap): this was the review's own scoping choice; the files are tracked at 6ab00b7 and reviewers checked them in the tree.
- low — the single outcome hides co-occurring failures (acceptance-auditor, sub-claim): the outcome is still non-success and ineligible; a per-task breakdown adds output surface for a diagnostics-only gain. (That finding's test-assertion sub-claim is in the patch above.)
- low — fence invocations for different authorities can share a `custodianMaterialRef` (edge-case-hunter): refs are opaque and custody is enforced by the custodian/runtime (same verdict as the prior BH-6); a cross-invocation guard adds complexity.
- low — an unresulted invocation dated after the commit snapshot is accepted as incomplete rather than refused (edge-case-hunter): the outcome is already non-success and ineligible; refusing would add a guard.
- low — `--schema-only` silently ignores `--context`/`--commit-context` (edge-case-hunter): the report still says `shape-valid`; this is rarely met, and the fix is a new guard.
- low — no maximum credential lifetime (blind-hunter): credentials are bound to attempt and epoch and rechecked at commit (recovery-hook-v1.md:171-176, validate.py:209-218), so lifetime only matters within one epoch; a bound needs a new context field.
- low — invoking missing hooks contradicts the doc; HTTP statuses for some refusals are undefined (blind-hunter): "Missing implementations are unqualified" describes the attempt outcome, which validate.py:364 produces; the executor owns ordering, so out-of-order or missing-hook calls are rare.
- low — the final reconciliation summary is not cross-checked against earlier tasks (blind-hunter): each earlier task must already succeed, so a contradicting summary cannot turn an ineligible chain eligible; the checks would add cross-task complexity.

## Implementation Notes

- Published contract `1.0.0` with closed Draft 2020-12 declaration, invocation, result and offline sidecar shapes. Explicit workload bindings keep ordinary tasks separate from authority-specific sandbox fences.
- Added strict JSON loading, independent admission/commit contexts, scoped opaque-position checks, dependency/phase ordering, immutable retry bindings and fail-closed result/reopening checks.
- Locked `jsonschema` and its required RFC3339 validator; malformed dates and CLI errors produce stable diagnostics without reflected values. Lowercase RFC3339 markers and UTC offsets are normalized.
- Acceptance audit found and fixed missing date-format validation and a pending-replay prerequisite bypass before review. Review fixes preserve exact numeric/fractional-time comparisons, bind the prior issuer, tighten schema-only constraints and independently pin three existing guards. No frozen intent changed.
- Second review (2026-10-20) applied 5 decisions and 7 patches: recovery-class due phases, phase-owned gates with reconciliation-only reopening claims, exact-version schema naming, one owning module per surviving authority, diagnostic-only refusal reason codes, single-record rules moved into the schema, the in-place fence exemption documented, and exact-code tests for every reachable refusal. A require-disabling mutation sweep kills 70 of 73 mutants; the 3 survivors are unreachable (`endpoint-mismatch`, `result-cut-mismatch`) or equivalent (`invalid-timestamp`).
- All 105 tests ran and passed. Matrix coverage: conforming chain (`test_valid_chain`, `test_stable_success_output`); invalid shape/secret-safe refusals (`test_unknown_shape_and_unsupported_version`, `test_duplicate_json_keys_and_nonfinite_numbers`, `test_missing_cut_class_and_named_positions`, CLI diagnostic tests); authority mismatch (`test_independent_authority_mismatch`, `test_epoch_takeover_between_invocation_and_commit`, retry checks); incomplete recovery (`test_missing_implementation_is_unqualified`, failed integrity/unknown effect/replay tests and `test_matrix_outcomes`).

## Spec Change Log

## Review Triage Log

All 12 accepted findings were resolved in 11 patch groups (BH-2/EH-1 share one root cause). BH-5 and BH-6 were rejected on the evidence below. Final verification: all 74 tests passed; no accepted findings remain open.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-1 | high | patch | Reproduced: JSON epoch `7.000000000000000001` rounds to `7.0` and a complete chain matches trusted epoch 7. Preserve exact numeric parsing in the existing loader. |
| BH-2 | high | patch | Reproduced: a dependent invocation at `.0000001Z` passes before prerequisite completion at `.0000009Z`. Preserve full RFC3339 fractions in the existing comparison helper. |
| BH-3 | medium | patch | Confirmed standalone declarations accept a final LF in both module ID and digest because regex `$` permits it. Correct the existing end anchors. |
| BH-4 | medium | patch | Reproduced: changing only commit-context `priorIssuerGenerationId` still reports complete and eligible. Include that existing DR identity in context equality. |
| BH-5 | false | reject | A reissue regression to pending already returns reopening eligibility false; reproduced against the fixture. The protocol does not require monotonic reissue state, and a fresh current pending state must block reopening. |
| BH-6 | false | reject | The invocation already binds authority ID together with the material reference, and validation checks the declared authority. A repeated opaque reference alone does not demonstrate access to the same material: resolution and actual authority-specific custody enforcement belong to the custodian/runtime, explicitly outside this offline validator's claim. No global reference-uniqueness rule exists. |
| BH-7 | medium | patch | Confirmed shape validation accepts `/public/recovery` while declarations and normative target require the fixed internal endpoint. Apply the existing constant to invocation shape. |
| BH-8 | medium | patch | Confirmed cut shape accepts an EventStore-kind position owned by Keys; owner enforcement currently occurs only in full-chain semantics despite the coverage claim. Add the two existing built-in owner constants to schema constraints. |
| BH-9 | medium | patch | The HTTP refusal paragraph gives status codes and a reason code but omits content type/body serialization, leaving downstream parsers inconsistent. Clarify the existing reason-code-only body as plain text with one example; add no response fields or endpoints. |
| BH-10 | medium | patch | The documented duplicate path references a recorded terminal result which does not yet exist during an in-flight execution. Clarify that an identical delivery joins that execution and waits for its terminal result without creating another task. |
| EH-1 | high | patch (BH-2) | Independently reproduced full-fraction truncation causing inverted phase/credential times to compare equal. Same parsing root cause as BH-2. |
| VG-1 | medium | patch | Pre-verified mutation leaves all 64 tests green when same-phase dependency enforcement is removed. Add an isolated missing same-phase edge case retaining every earlier-phase edge. |
| VG-2 | medium | patch | Pre-verified mutation leaves all 64 tests green when credential audience comparison is removed. Add a structurally valid audience mismatch with all other fields unchanged. |
| VG-3 | medium | patch | Pre-verified mutation leaves all 64 tests green when immutable retry comparison is removed. Add an individually valid retry dependency change that must refuse retry-identity-mismatch. |

## Design Notes

This adds a language-neutral wire contract rather than a module ABI. Credential metadata is descriptive; runtime consumers must authenticate it and obtain current epoch from authoritative state. Offline expected context exercises comparison rules, not store atomicity. Dependency setup and wire names are implementation choices; no external mutation is needed.

## Verification

- `uv run --locked --project eng/recovery-contract python -m unittest discover -v -s eng/recovery-contract -p 'test_*.py'` — 105 tests passed (14.9s), including schema self-validation, all four matrix rows, semantic cases and CLI regressions.
- `git diff --check -- schemas docs/contracts eng/recovery-contract` — no whitespace errors.
