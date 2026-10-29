---
title: 'Story 4.14: Reassess and unblock the offline foundation approval package'
type: 'chore'
created: '2026-10-29'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '2a18cdd33fcb609a563011e9bfd4153f56445c63'
context: []
---

<frozen-after-approval reason="explicit user authorization for bounded offline preparation">

## Intent

Resolve foundation inputs only from cited repository evidence or explicit owner decisions, reassess dependencies and expose every remaining prerequisite in a concrete offline approval handoff. Where evidence cannot settle a choice, retain null and ask grouped questions. Render exact manifests and an ordered proposal only when actual inputs permit it; operational approval remains false.

## Boundaries & Constraints

Work in `/tmp/platform-story414-unblock-1RNMu2bO/platform`; its baseline is recorded above. The parent integrates a commit by fast-forwarding main without pushing, preserving unrelated edits, evidence, statuses and gitlinks. No cluster/network calls, API dry-runs, Helm execution, installation, credential issuance, key generation, unsealing, secret writes or recovery operations. Do not run native collectors. Keep Story 4.14 and consumer Story 3.6 in-progress, and consumer launch FAIL. Consumer facts never enter foundation definitions or acceptance gates.

All five dependencies remain unaccepted unless current authoritative evidence proves completion. Credit 4.2 exposure/registry/GC results, 4.27's offline certificate prerequisite and original-scope 4.26 qualification without advancing acceptance. 4.15 preparation is downstream, never a reverse foundation gate. The parent has asked three grouped owner questions; no answers exist yet. Unanswered operational choices are requested outputs, not implementation assumptions.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Existing cut | Two evidenced DNS fields; 35 nulls | Exact field inventory, grouped owner questions, no manifests | Renderer and plan exit 2, empty stdout, no output directory |
| Invalid evidence | Changed citation bytes or bad pointer | Offline verification refuses unsupported claims | Nonzero verification result; never rewrite history to pass |
| Scoped passes | 4.2 passes, 4.27 offline done, 4.26 qualified | Scoped credit with all five dependency acceptance flags false | Cannot become full acceptance or operational approval |

</frozen-after-approval>

## Code Map

- `eng/staging-foundation/{inputs.resolved.json,MISSING-INPUTS.md,INPUT-EVIDENCE.md,gates.assessed.json}`: current exact inputs, 35 individual questions, rejected substitutions and still-valid dependency citations.
- `eng/staging-foundation/{render.py,apply-plan.json,cni_probe.py}`: reuse refusal and stage order; do not execute generated argv. Known definition gaps include staging-only storage, complete RBAC, real DNS datapath and per-service TLS flows. Quantity syntax/order validation is limited, and empty storage parameters are currently refused.
- `eng/staging-secrets/{APPROVAL-PACKAGE.md,source-evidence.json,prepare.py}`: 4.15 pins six foundation files. Preserve every existing foundation file except README, and every secrets byte, so its valid historical citations remain usable. Put the new current assessment in additive artifacts and link from README.
- `_bmad-output/implementation-artifacts/evidence/epic-4/4-14/live-apply-approval-2026-10-29/`: retain the original observations and receipts verbatim. New evidence goes in `offline-unblocking-2026-10-29/` beside it.

## Tasks & Acceptance

**Execution:**
- [x] `eng/staging-foundation/unblocking.assessed.json` — add a source-bound current assessment with exact 37-field inventory, phase-separated prerequisites, scoped dependency reassessment and false approval/live acceptance flags.
- [x] `eng/staging-foundation/UNBLOCKING-REPORT.md` — record all grouped owner decisions, references to individual questions, definition gaps, later verification and ordered requirements for separately authorized live apply; explain why 4.15 supplies no new choices.
- [x] `eng/staging-foundation/README.md` — link the additive current handoff without invalidating 4.15's pinned sources.
- [x] `_bmad-output/implementation-artifacts/evidence/epic-4/4-14/offline-unblocking-2026-10-29/` — add a reproducible stdlib offline verifier and receipts/logs for source hashes/pointers, exact null set, approval/status/dependency boundaries, no-process/no-network behavior, all new module-agnostic artifacts, render/plan refusal and existing foundation/secrets suites.

**Acceptance Criteria:**
- Given the repository cut, when reassessed offline, then each field is resolved by valid provenance or remains null with an exact owner question; no example value is adopted.
- Given outstanding choices, when the package is generated, then all additional storage/RBAC/network prerequisites remain explicit and distinguish pre-render/pre-apply decisions from separately authorized live evidence.
- Given passing offline checks, when the handoff is read, then no claim implies operational or live acceptance, changed story status, consumer gate or execution authority.
- Given 4.15's supplied commit, when compared with main, then imported preparation and its later synthetic-test correction are preserved, as are its source pins and historical receipts.

## Implementation Notes

All four implementation tasks are complete for the available evidence cut. The three owner question groups were asked asynchronously and remain unanswered; no values were adopted. The offline verifier passed 8 handoff checks, 34 foundation tests and 48 secrets tests. Both actual-input entrypoints refuse with exit 2 and produce no manifests. All 69 protected file hashes and six downstream source pins match. The current handoff records staging-priority bypass paths, complete RBAC/escalation and observer separation, quantity validation and exact-target binding as unresolved definition requirements.

The owner has already authorized offline work and local integration. No extra spec approval is requested. Missing owner choices will remain pending in the package if no reply arrives; only their dependent rendering is withheld. The parent will record integration preservation separately and handle commit/fast-forward.


Final review corrections bind source/import validation before project code, exact protected and receipt inventories, saved false flags/outcomes, retained decision wording/history and all supported line citation formats. Receipt writes reject symlink escapes before touching any destination. The fourth pending owner group records the unresolved provider bootstrap/acceptance sequence; probe collision/ownership and finite cleanup remain blockers. All 11 actionable review findings were patched; no work was deferred. After recovery, parent re-read the patch and all 90 offline checks passed again. This scoped spec is done for the offline handoff only: all 35 inputs and all four owner groups remain unanswered, every live/dependency approval remains closed, and the parent Story 4.14 sprint status must remain in-progress under the explicit user instruction.

Administrative preservation and exact 4.15 Git comparisons are recorded in [integration preflight](evidence/workspace/4-14-offline-unblocking-2026-10-29/integration-preflight.json), outside foundation definitions and acceptance evidence. The supplied/imported feature commits differ in a synthetic-test line; current non-test preparation bytes match the supplied commit. No tree equivalence is claimed.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Disposition and evidence |
|---|---|---|
| BH1: Modules load before protected source/import checks | medium | Patch: renderer imports precede hash validation in setUpClass. Validate protected bytes and import boundaries before loading any project module. |
| BH2: Saved receipt flags/outcomes are unchecked | medium | Patch: default verification only loops artifactSha256; validate saved blocked flags, baseline and complete successful offline check outcomes before trusting it. |
| BH3: Saved artifact map can omit required paths | medium | Patch: require the independently specified current artifact set, so removing entries cannot bypass checks. |
| BH4: Empty upstream decisions pass all() | medium | Patch: bind the retained decision contract, including the three upstream 4.28 decisions, before receipt regeneration. |
| BH5: Requirement prose can become a waiver during regeneration | medium | Patch: bind the current structured decision/prerequisite contract to its reviewed fingerprint, and describe the verifier as a current-cut consistency check. Default read-only hashes already catch unrehash­ed edits. |
| BH6: Group questions can omit additional prerequisites | medium | Patch: include owner groups and additional definition requests in the retained contract fingerprint and mutation coverage. Individual 35 field questions are already source-checked. |
| BH7: Legacy lines citations are not checked | low | Patch: validate legacy integer/range line locations as well as lineStart/lineEnd; dependency sources routinely use this format. |
| BH8: Protected manifest may silently omit files | medium | Patch: bind the exact reviewed 69-file preservation manifest digest before importing project code, rather than trusting a mutable nonempty list. |
| BH9: Provider TLS proof versus downstream provisioning order | medium | Patch: expose an unresolved owner decision for bootstrap milestones/acceptance sequence, with authority withheld and no selected waiver. The user was asked asynchronously; no answer or new authorization exists. Shared/environment stage order stays explicit, while complete bootstrap order remains blocked. |
| BH10: Probe namespace collision/ownership requirements missing | medium | Patch: require namespace collision refusal, fresh attempt ownership/UID evidence and finite cleanup deadline before later approval. The unchanged generator is not approved for execution. |
| EC1: Receipt regeneration can follow an escaping symlink | medium | Patch: reject symlinked destinations and verify each destination remains directly in the additive evidence directory before any transcript/receipt write. |
| Verification gap reviewer | No findings | Traced the entrypoint and ran all 90 checks in the scratch worktree. |

## Verification

- `python3 -B -m unittest discover -s eng/staging-foundation -v`: existing 34 tests pass.
- `python3 -B -m unittest discover -s eng/staging-secrets -v`: existing 48 tests pass without changing cited sources.
- New offline verifier: checks the matrix and citations, including deliberately stale hash/pointer and false acceptance mutations; never calls the cluster.
- Actual-input render/plan: expected exit 2 and no manifests until owners resolve missing fields. Complete synthetic fixtures prove only existing offline behavior and are never published as target manifests.
