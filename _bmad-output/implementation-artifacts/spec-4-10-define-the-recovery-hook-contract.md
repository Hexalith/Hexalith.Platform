---
title: 'Define the recovery hook contract'
type: 'feature'
epic: 4
story: 10
created: '2026-10-07'
status: 'ready-for-dev'
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
- [ ] `schemas/hexalith.recovery-hook.v1.json` — define versioned declaration/invocation/result shapes and scoped cut positions; make required coverage machine-checkable.
- [ ] `docs/contracts/recovery-hook-v1.md` — fix endpoint/request/result semantics, authorization and atomic commit obligations, phase/dependency ordering, retry identity, refusals, reopening and version lifecycle; distinguish fence execution.
- [ ] `eng/recovery-contract/pyproject.toml` and `uv.lock` — declare and lock the JSON Schema validator dependency; avoid a custom schema engine.
- [ ] `eng/recovery-contract/validate.py` — strictly load JSON, validate against the schema and apply cross-record/context semantic checks; expose a local CLI with stable, sanitized errors.
- [ ] `eng/recovery-contract/test_validate.py` and `fixtures/*.json` — test every matrix row, mismatched identity/cut/result bindings, dependency cycles, unsupported versions, noncomparable positions, in-place/DR distinctions and replay exceptions. Simulate epoch change between invocation and commit using independent context; label fixtures synthetic.
- [ ] `eng/recovery-contract/README.md` — document reproducible CLI commands and consumer responsibilities; link the normative contract and schema.

**Acceptance Criteria:**
- Given Story 4.10, when the artifacts are inspected, then every listed contract element has a normative definition and schema representation where applicable.
- Given a retained release version, when a consumer selects its contract, then exact version selection and retention rules are explicit without claiming absent release integration.
- Given the fixtures, when validation runs, then valid chains pass and every refusal/incomplete case produces the expected outcome without disclosing secrets or asserting live recovery.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

This adds a language-neutral wire contract rather than a module ABI. Credential metadata is descriptive; runtime consumers must authenticate it and obtain current epoch from authoritative state. Offline expected context exercises comparison rules, not store atomicity. Dependency setup and wire names are implementation choices; no external mutation is needed.

## Verification

- `uv run --project eng/recovery-contract python -m unittest discover -s eng/recovery-contract -p 'test_*.py'` — schema self-validation, semantic and CLI cases pass.
- `git diff --check` — no whitespace errors.
