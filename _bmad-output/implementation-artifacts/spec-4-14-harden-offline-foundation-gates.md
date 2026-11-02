---
title: 'Story 4.14: Harden the offline foundation gates'
type: 'chore'
created: '2026-11-02'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '127d0862986a96e89f22538041804924fd1dd057'
story_key: '4-14-build-staging-namespaces-storage-and-pod-and-network-isolati'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 4.14's offline package has validation and probe ownership gaps. Live acceptance is blocked by 35 unknown inputs, four owner decision groups, five unaccepted prerequisites and missing operational approval.

**Approach:** Harden offline preflight and CNI probe generation using repository facts. Make preflight the documented candidate entry point that validates before calling the source-pinned renderer. Preserve the renderer and historical receipts. Keep unresolved owner choices and live effects blocked.

**Approved scope:** Offline hardening only, per the user's 2026-11-02 instruction to apply the recommended path. Story 4.14 stays in-progress. Full live completion requires separate decisions, accepted dependencies and exact operational approval.

## Boundaries & Constraints

**Always:** Keep plans non-executable and approval/live acceptance false. Preserve six Story 4.15-pinned foundation files, historical evidence, gitlinks and unrelated work. Require real owner inputs. Use module-agnostic, standard-library offline checks.

**Never:** Call the cluster/network, run generated commands, mutate resources, issue credentials, select unevidenced policies, claim runtime proof or mark 4.14 done.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Complete candidate | Explicit values and valid relationships | Guarded render/plan output; live apply blocked | No execution |
| Invalid candidate | Malformed quantity, inverted request/limit or PVC bound, quota too small for one declared minimum | Field-specific refusal before candidate qualification | Nonzero exit; no manifests |
| Missing candidate | Current 35 null inputs | Exact missing fields retained; no inferred values | Nonzero exit; no manifests |
| Probe candidate | Unique attempt, digest image, finite deadline | Fresh namespace and UID-bound cleanup proposal | Collision or unverified ownership refuses |
| Probe result | Missing control, duplicate output or reachable denied peer | Inconclusive/fail, never passing CNI evidence | Cleanup remains guarded |

</frozen-after-approval>

## Code Map

- `eng/staging-foundation/render.py:70,293` — pinned renderer; preserve bytes. Quantity checks are weak and target check compares supplied strings.
- `eng/staging-foundation/cni_probe.py:58,94,117,132` — fixed namespace/label cleanup; add attempt identity and UID guard.
- `eng/staging-foundation/test_staging_foundation.py` — existing offline behavioral tests.
- `eng/staging-foundation/{UNBLOCKING-REPORT.md,README.md,unblocking.assessed.json}` — gaps, false flags and handoff.
- `eng/staging-secrets/source-evidence.json` — six historical foundation pins.
- `_bmad-output/planning-artifacts/epics.md:2272` and `_bmad-output/implementation-artifacts/sprint-status.yaml:103` — live criteria and in-progress story key.

## Tasks & Acceptance

**Execution:**
- [x] `eng/staging-foundation/preflight.py` — add a stdlib-only guarded `render`/`plan` entry point: validate Kubernetes quantity syntax and nonnegative values; require default requests ≤ default limits and PVC min ≤ max; reject a positive pod/PVC quota whose resource quota cannot fit one declared default request/minimum claim. Call the pinned renderer in process only on success. Refuse before writing on invalid or missing inputs; preserve blocked plan semantics.
- [x] `eng/staging-foundation/cni_probe.py` — require an attempt ID and finite cleanup deadline, generate a per-attempt namespace, emit collision and UID checks, and refuse a cleanup proposal without matching observed namespace identity and UID.
- [x] `eng/staging-foundation/test_staging_foundation.py` — cover valid/invalid candidates, malformed/duplicate probe results, collision/UID mismatch and no-process behavior.
- [x] `eng/staging-foundation/README.md` and a new current handoff under `eng/staging-foundation/` — document the guarded candidate path and remaining blockers without changing historical pins.

**Acceptance Criteria:**
- Given the current unresolved inputs, when guarded render or plan runs, then it refuses without writing manifests or invoking cluster tools.
- Given a complete synthetic fixture, when quantities and relationships are invalid, then each unsafe field is reported and no qualification is emitted.
- Given a generated probe proposal, when namespace ownership or UID cannot be verified, then no delete action is qualified.
- Given passing offline tests, when the package is reviewed, then approval and live acceptance remain false and Story 4.14 remains in-progress.

## Implementation Notes

- Added a guarded candidate CLI that validates unresolved inputs and resource quantities before calling the unchanged historical renderer. Extreme exponents and values above Kubernetes' quantity magnitude bound refuse cleanly.
- Generated probe artifacts now use a per-attempt namespace. Creation requires an observed absence; cleanup proposals require a matching namespace UID, labels and deadline and carry a UID precondition. No generated action is executable.
- All six Story 4.15 foundation pins still match. The current resolved input cut refuses with 35 missing fields, exit 2, no stdout and no manifest directory. The 42 offline tests pass; sprint Story 4.14 remains in-progress.
- Review corrections removed creation and DELETE action templates, distinguished binary `Ei` from exponent notation, enforced mill CPU precision, and covered memory ordering and cleanup deadline mismatches. The final offline suite passes 44 tests; the 35-field refusal remains unchanged. Live proof gaps were recorded in deferred work.
- This scoped spec is done. The sprint story remains `in-progress` under the approved offline-only boundary; the workflow's normal `review` synchronization is intentionally not applied to the broader live story.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| BH-1 CPU sub-millicore | medium | patch | `quantity("0.5m", "cpu")` passes although Kubernetes rejects CPU precision finer than 1m; add a direct guard. |
| BH-2 plan manifest binding | medium | defer | The unchanged source-pinned `render.build_apply_plan` accepts any manifest directory; this known exact-byte/target binding gap predates this offline validation scope. |
| BH-3 apply collision race | medium | patch | New `creation_proposal` emits `kubectl apply`, which may update a namespace created after the check; withhold the action template. |
| BH-4 unrelated manifest path | medium | patch | New `creation_proposal` emits an argv for an unchecked file; withholding the action template removes that unsafe proposal. |
| BH-5 stale absence observation | low | patch | A supplied `exists:false` map has no target/time provenance; state explicitly that it is unverified and qualifies no action. |
| BH-6 cleanup namespace contents | medium | patch | New DELETE template could delete other resources in a matching namespace; withhold DELETE until a later live inventory and authorization. |
| BH-7 denied-peer false pass | high | defer | Existing probe treats any denied connection failure as isolation, including DNS/endpoint failure; runtime CNI proof needs a separate positive peer control. |
| BH-8 arbitrary log replay | medium | defer | Existing evaluator accepts caller-supplied text without client Pod UID/source proof; live evidence collection remains unimplemented. |
| BH-9 DNS label syntax | medium | defer | Existing probe validates only nonempty selector strings, so invalid Kubernetes labels can render; selector policy qualification requires the actual DNS path. |
| BH-10 deadline/write race | low | patch | New CLI validates deadline again after writing files; compute the sequence before writes. |
| EH-1 namespace collision | medium | patch | Same verified `kubectl apply` race as BH-3; keep this independent review row. |
| EH-2 unrelated manifest | medium | patch | Same verified unchecked manifest argv as BH-4; keep this independent review row. |
| EH-3 reused attempt/log | medium | defer | Caller can reuse an ID after deletion and replay an old log; source Pod UID binding belongs to the unimplemented live proof path. |
| EH-4 deadline/write race | low | patch | Same verified second-validation race as BH-10; keep this independent review row. |
| VG-1 memory ordering test | medium | patch | Removing memory request/limit comparison leaves relevant tests green; add a behavioral refusal case. |
| VG-2 deadline mismatch test | medium | patch | Removing namespace deadline comparison leaves relevant tests green; add a mismatched-annotation case. |
| VG-3 Ei quantity | medium | patch | `quantity("1Ei", "storage")` enters exponent parsing and refuses a valid binary suffix; disambiguate the branches. |

## Verification

**Commands:**
- `python3 -B -m unittest discover -s eng/staging-foundation -v` — expected: offline suite passes without process/network calls.
- `python3 -B eng/staging-foundation/preflight.py render --inputs eng/staging-foundation/inputs.resolved.json --output /tmp/staging-foundation-refusal` — expected: refusal on unresolved fields, no manifest output.
