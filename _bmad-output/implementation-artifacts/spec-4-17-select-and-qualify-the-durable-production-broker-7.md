---
title: 'Story 4.17 slice: apply RabbitMQ-first remediation priority'
type: 'chore'
created: '2026-11-14'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
baseline_commit: '66c8b2bed82213ee4038da525eab5e81e78bbc13'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — owner directed apply recommended after the detailed tradeoff assessment">

## Intent

Apply the accepted remediation recommendation to the existing Platform and EventStore
handoffs: prioritize RabbitMQ Package A (publish-error propagation, cancellation and
reconnect) first, then Package B (opt-in mandatory publishing and correlated returns).
Retain the existing upstream issues and their acceptance requirements. This selects
engineering priority; it establishes no upstream implementation or supported delivery.

Keep Kafka pressure paused without a concrete supported Produce-rejection mechanism.
Keep rotation instrumentation paused without a supported completion/discrimination
witness or separately accepted correlation design. If Kafka is strategically required,
a pressure-contract discussion precedes further speculative tuning/instrumentation;
no predicate change is accepted here. Kafka re-entry remains independent of RabbitMQ.

Use normal planning checkpoints to inspect the existing issues and official releases;
create no monitoring service or scheduled automation. RabbitMQ execution re-entry needs
an official supported Dapr release embedding the required fixes, EventStore exact input
admission, Builds alignment and a separately accepted Platform full-matrix proposal.
A future focused upstream contribution needs its own concrete implementation scope.

Preserve all 24 vectors, predicates/time bounds, original work packages, packets, custody
obligations and authority labels. Both parents remain in-progress, five guarantees
REJECTED/open, technical FAIL (20/24), selection null and production eligibility FAIL.
This local priority update includes no broker run, runtime/profile/lock adoption,
deployment, storage change, new external message or Git publication. Only this bounded
priority-application slice may finish.

</frozen-after-approval>

## Implementation Notes

**Historical draft investigation, before the owner's priority decision:**

Intent gap: missing new supported input. Current footprint: this draft only; no new
callable interface. Future execution/adoption has separately gated external effects.
Started clean on main at the baseline. Loaded valid Epic 4 context and completed 4.15
continuity. An independent read-only investigator confirmed no overlooked local task;
another reconciliation would duplicate work.

GitHub connector checks completed by 2026-11-14T08:31:54Z found
[4764](https://github.com/dapr/components-contrib/issues/4764) and
[4765](https://github.com/dapr/components-contrib/issues/4765) open, unchanged since
submission, with zero comments; separate comment reads were empty. Latest-release
endpoints returned [Dapr 1.19.1](https://github.com/dapr/dapr/releases/tag/v1.19.1)
(ID 429942238) and [components 1.19.0](https://github.com/dapr/components-contrib/releases/tag/v1.19.0)
(ID 420524535). These bounded observations provide no new supported-delivery/admission
evidence; they do not prove universal impossibility.

**Accepted priority application:**

- The owner's subsequent “apply recommended” resolves the previous Open Question by
  selecting the detailed recommendation. The previous prospective successor tasks
  are replaced by this bounded priority application; no missing technical input is invented.
  No intent gaps or irreversible effects remain in this document-only slice.
- Footprint: this spec, Platform's existing next-candidate intake, EventStore's existing
  evidence contract, its parent current pointer and one field in its existing deferred
  entry. Reuse existing work packages/issues; preserve their exact historical bytes.
  EventStore starts clean on main at cf803659c8bdb965d26b479fc6547a6be08801c7.
- Verify append-only history apart from explicit current pointers, unchanged frozen
  parent requirements, exact changed-file allowlists, immutable work packages/receipts,
  original 94-input/24-vector audit, statuses and changed-line whitespace. Use one blind
  reviewer; behavioral tests are unnecessary for this prose-only decision.
- User-supplied AGENTS instructions require run-with-temp for disposable verification
  jobs. Store scratch only below its TMPDIR and wait for child work before exit.
- Initial sprint sync is a no-op because the parent is already in-progress. Preserve
  that status through finalization under the accepted intent above. Keep this result
  local: the workflow disallows unrequested push, while owning Git instructions require
  publishing submodule changes before a superproject commit; do not trigger that
  publication prerequisite to satisfy the generic automatic-commit step.
- Applied the priority table and current pointers in both handoffs, updated the EventStore
  parent current summary, and appended one remediation_priority field to its existing
  deferred item. Original work packages and all earlier historical lines are retained.
- Initial verification passed under run-with-temp: the exact retained parent Bash audit
  verifies 94 locked inputs, protected/candidate bytes, frozen requirements, all 24 vectors,
  20 PASS/4 FAIL/five historical dispositions, original identities/authority/statuses and
  immutable links. The separate scope/history check passed: exactly four existing documents
  plus this accepted spec, valid local links, original work-package hash, unchanged heads,
  indexes and gitlink, and changed-line whitespace in both repositories. No behavior changed.
- Only the one-shot blind document-review layer is active; separate behavioral edge-case
  and verification-gap review layers are unnecessary for this document-only decision.
- Clarification of the historical GitHub observation: “unchanged since submission” refers
  to issue state and updated_at equalling the recorded creation timestamps, plus empty
  comment reads. It does not claim a fresh byte-for-byte title/body comparison. The source
  readbacks are in this build conversation; this slice performs no fresh remote-state check.
- Independent blind review returned four findings; all receive simple prose corrections.
  Labelled earlier draft notes as history, clarified the bounded issue observation,
  documented repeatable verification below, and assigned material release observations
  to the EventStore 3.21 maintainer in existing sprint planning and the reopened broker item.

- Final post-correction checks passed, including execution of the exact commands retained
  below. All 94 locked inputs, original vector/authority/status identities, protected
  historical bytes, new links/anchors and changed-file limits are preserved. All four
  review findings are corrected; none deferred. Only this local priority slice is done;
  both parent stories and all unsupported-delivery dependencies remain open. Heads,
  indexes and gitlink are unchanged; no commit, push, source adoption or broker run occurred.

Repeat the retained audit from the Platform root with the wrapper required by the user:

```bash
run-with-temp python3 -B - <<'PY'
from pathlib import Path
import re, subprocess
path = Path('references/Hexalith.EventStore/_bmad-output/implementation-artifacts/spec-3-21-production-dapr-runtime-pin-and-durable-broker-qualification.md')
blocks = re.findall(r'```bash\n(.*?)\n```', path.read_text(), re.S)
audit, = [block for block in blocks if 'PASS: 94 inputs;' in block]
subprocess.run(['bash', '-eu'], input=audit, text=True, check=True)
PY
git -c core.commitGraph=false diff --check
git -C references/Hexalith.EventStore -c core.commitGraph=false diff --check
git -c core.commitGraph=false diff --name-only --ignore-submodules=all
git -C references/Hexalith.EventStore -c core.commitGraph=false diff --name-only
```

The audit block is retained in the parent's “Reproduce the offline checks” paragraph;
expected output is its PASS line for 94 inputs and the original 24-vector outcomes.
The diff checks expect no whitespace errors and only the four existing documents named
above. The accepted spec is additionally untracked. The supplementary scope/history
assertions were one-off checks in the build conversation, not a maintained test fixture.
Inspect `git diff` in each owning repository to repeat the additions/current-pointer
history review. No qualification result is regenerated by these offline checks.

## Review Triage Log

| Finding | Verdict / route | Checked evidence and correction |
| --- | --- | --- |
| B1 draft-only notes look current | low / patch | The original investigation notes were retained verbatim but lacked chronology labels. Added explicit historical/current labels; the accepted intent and later footprint govern. |
| B2 verification lacks reproduction references | low / patch | The source audit exists in the EventStore parent, while supplementary assertions lived in this conversation. Added exact wrapper extraction and Git commands, expected coverage/output, audit location and limits of the one-off scope check. |
| B3 issue “unchanged” is ambiguous | medium / patch | The recorded reads establish issue state/timestamps and empty comments, not new full-body equality. Added that explicit limit and identified conversation readbacks; no new verification is claimed. |
| B4 manual follow-up lacks an accountable role/record | low / patch | Assigned checks to the EventStore 3.21 maintainer when the existing item is discussed in normal sprint planning; linked its existing reopened broker item and defined dated records for material changes, without new scheduling or repetitive no-change documents. |
