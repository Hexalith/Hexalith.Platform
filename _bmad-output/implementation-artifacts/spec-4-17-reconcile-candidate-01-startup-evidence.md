---
title: 'Story 4.17: reconcile candidate-01 startup evidence and further execution proposal'
type: 'chore'
created: '2026-11-13'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
baseline_commit: 'd5928c8b106e1da4179e55dbe8390912b02a7e44'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Reconcile run 43097161643 attempt 1 against its original execution/verifier receipts and the later retained investigations. Establish only what evidence supports about the zero-job startup failure; correct a demonstrated Platform defect if one exists. Record exact commands, results and canonical revisions. Prepare one concrete further non-promoting candidate-01 execution without dispatching or borrowing consumed acceptance.

Preserve all existing receipts, candidate-01 configuration, EventStore source pin, qualification profile, runtime and dependency pins, all 24 vectors and the five historical REJECTED guarantees. Keep current-checkout drift and later packet/verifier defects separate from startup failure. Story 4.17 and EventStore 3.21 remain in-progress, selected broker null, production eligibility FAIL. No push, tag, dispatch, rerun, cancellation or lock refresh is authorized.

</frozen-after-approval>

## Implementation Notes

- Small evidence/documentation continuation; no intent gap or irreversible operation. Original worktree has unrelated Story 2.10 changes and a newer EventStore checkout; use a clean detached worktree at the canonical baseline and integrate only new files. Do not touch sprint status or gitlinks.
- Reuse retained evidence under `evidence/4-17/candidate-01-{hosted,startup-investigation,hosted-attempt-2,packet-diagnosis}` and the original workflow/tests. Add `candidate-01-startup-reconciliation/` for read-only observations, exact local command receipts, manifest and one fresh proposal; add a linked implementation report. Existing evidence stays byte-identical.
- Evidence supports provider disruption with queued-ref deletion unresolved; exact root cause remains unknown and no Platform code defect is demonstrated. The later attempt allocated with identical workflow bytes, so a deterministic workflow rejection is unsupported. Current EventStore checkout and the separately proposed candidate-02 changes cannot repair or explain a pre-allocation failure.
- Later candidate-01 execution 2 already consumed acceptance. Fresh workflow inventory also shows candidate-02 executing independently: record its source separately and require idle status at any future acceptance. Proposed execution 3 reuses the existing immutable source tag; no input/pin changes. Worker identity can only be observed after dispatch: require a new exclusive VM bound to NEW_RUN_ID/candidate-01/1 and its local socket/daemon; no allocation is claimed now.
- Verify original manifests and source hashes, run existing 53 workflow controls and actionlint, validate all 91 pinned inputs in an isolated EventStore archive, and enumerate current-checkout hash drift read-only. Do not claim that another unchanged capture fixes the known pinned-verifier refusal.

- Implemented only additive records and the continuation report. The independently compiled Epic 4 context was loaded for planning, then its generated change was discarded to keep unrelated files intact. No product code correction was demonstrated. The first local audit stopped when shared EventStore HEAD moved; its receipt and script are retained. A collector path error was likewise retained before the successful isolated audit.
- Verification: 155 historical manifest commitments match; all original execution identities match; original owner admission passes 91 inputs; archived session-start source has four exact original-profile drifts. Existing controls pass 53/53; actionlint 1.7.12 has no findings. Reviewed collector audit records 27 subprocesses and enforces identities/refusal shape; an existing-output probe fails without altering output bytes. The earlier 16-check audit remains retained.
- The single configured review layer, Blind Hunter, returned ten findings, all patched and checked. No additional review layer was configured and no finding was deferred. Explicit user constraints override moving the parent story to review: Platform 4.17, EventStore 3.21 and all selection/eligibility states remain unchanged.

## Review Triage Log

| Finding | Verdict / route | Evidence and correction |
| --- | --- | --- |
| R1 collector overwrites retained output | medium / patch | Existing destination was accepted. Require an explicit fresh output directory; negative probe returns FileExistsError with all prior hashes unchanged. |
| R2 configuration not bound before admission | medium / patch | Earlier audit recorded the working-tree hash only. Reviewed audit reads original JSON from the exact Git source, asserts its known SHA-256 and baseline byte equality, then admits those extracted bytes. |
| R3 workflow equality not enforced | medium / patch | Boolean was only recorded. Assert equality before applying baseline-control results to execution workflow bytes. |
| R4 arbitrary exit 1 accepted for drift | medium / patch | Earlier collector accepted any nonzero refusal. Assert all four expected paths, actual mismatches, 91 inputs, 24 vectors and exact refusal text. |
| R5 unrecorded Git subprocesses | medium / patch | Earlier ledger omitted identity/extraction reads. Every subprocess in the reviewed collector uses timestamped command receipts; report distinguishes earlier selected-check ledger. |
| R6 projected ruleset omits target | medium / patch | Fresh reviewed API observation retains target=tag, and proposal explicitly requires it with all-tag scope and no bypass. |
| R7 insufficient request timing | low / patch | Earlier snapshot has document time only. Retain it as such; add fresh per-call start/end UTC observations and per-subprocess timing without inventing old call times. |
| R8 new-run discovery ambiguous | medium / patch | Add pre-dispatch inventory, unique new run matching all identities/time, retained run URL, 30-minute discovery bound and ambiguity stop without retry. |
| R9 queued run could be orphaned | medium / patch | Accepted operator retains read-only tracking and retrieval through terminal, including late allocation; require accepted monitoring handoff if session ends. |
| R10 archive inspection lacks refusal rule | medium / patch | Explicitly refuse before extraction for unsafe/duplicate/colliding/link/special/encrypted/CRC-invalid/private members; retain unextracted bytes and rejection receipt; no credit. |
