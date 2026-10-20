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

## Implementation Notes

- Published contract `1.0.0` with closed Draft 2020-12 declaration, invocation, result and offline sidecar shapes. Explicit workload bindings keep ordinary tasks separate from authority-specific sandbox fences.
- Added strict JSON loading, independent admission/commit contexts, scoped opaque-position checks, dependency/phase ordering, immutable retry bindings and fail-closed result/reopening checks.
- Locked `jsonschema` and its required RFC3339 validator; malformed dates and CLI errors produce stable diagnostics without reflected values. Lowercase RFC3339 markers and UTC offsets are normalized.
- Acceptance audit found and fixed missing date-format validation and a pending-replay prerequisite bypass before review. Review fixes preserve exact numeric/fractional-time comparisons, bind the prior issuer, tighten schema-only constraints and independently pin three existing guards. No frozen intent changed.
- All 74 tests ran and passed. Matrix coverage: conforming chain (`test_valid_chain`, `test_stable_success_output`); invalid shape/secret-safe refusals (`test_unknown_shape_and_unsupported_version`, `test_duplicate_json_keys_and_nonfinite_numbers`, `test_missing_cut_class_and_named_positions`, CLI diagnostic tests); authority mismatch (`test_independent_authority_mismatch`, `test_epoch_takeover_between_invocation_and_commit`, retry checks); incomplete recovery (`test_missing_implementation_is_unqualified`, failed integrity/unknown effect/replay tests and `test_matrix_outcomes`).

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

- `uv run --locked --project eng/recovery-contract python -m unittest discover -v -s eng/recovery-contract -p 'test_*.py'` — 74 tests passed (8.747s), including schema self-validation, all four matrix rows, semantic cases and CLI regressions.
- `git diff --check` — no whitespace errors.
