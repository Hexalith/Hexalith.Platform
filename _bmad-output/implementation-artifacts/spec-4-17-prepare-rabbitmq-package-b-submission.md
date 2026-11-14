---
title: 'Story 4.17 slice: prepare the RabbitMQ Package B submission packet'
type: 'chore'
created: '2026-11-14'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
baseline_commit: '50fb0462924a765e38d830e42af242ccfaf5982f'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — bounded local continuation of Story 4.17; publication requires separate approval">

## Intent

Prepare the reviewed Package B contribution for an explicit publication decision. The source
implementation and local checks are complete; the retained commit message is only a test
vehicle, and there is no exact upstream PR proposal. Package A remains open and unchanged.

Create a local packet with the proposed upstream destination, branch, exact PR title/body,
exact signed-off commit message and source identities. Reconstruct the reviewed source in a
disposable workspace, verify its tree and retain both the B-only patch and the complete diff
against current upstream main. Explain that an upstream PR targeting main includes A and B
while A is unmerged; a PR targeting A's branch in the fork would not be an upstream submission.
Offer waiting for A as the alternative, requiring a fresh base/diff approval after it changes.

This slice prepares a concrete proposal only. No fork, push, PR, comment, issue edit, persistent
Git mutation, broker, Dapr runtime or qualification execution. Preserve the reviewed source,
historical receipts, packet custody, Kafka stop rules, both parent in-progress statuses,
technical FAIL (20/24), five REJECTED/open guarantees, null selection and production FAIL.

</frozen-after-approval>

## Implementation Notes

- Intent gaps: none for local preparation; publication timing remains an explicit final owner
  choice. Irreversibles: none. Footprint: this spec, a new local submission-packet directory
  and one append-only intake section. No EventStore or sprint-status edits are needed.
- Loaded valid Epic 4 context and previous completed Story 4.15 continuity: preparation conveys
  no live acceptance and unknown operational inputs remain blockers.
- Reuse `docs/implementation/story-4-17-upstream/package-b-local-2026-11-14/` final patch,
  source/commit identities, coverage and check receipts. Its patch SHA-256 is
  `7d55faea09b30fe31228566601e72e04b2158bc64a65d9d45fb086c16a31e392`, base
  `96c3259b7c12a476edd208848153d3cf72a8c4f0`, expected tree
  `50f8ef2d3d3a240595d6088917f9d51afe04ec3c`. Reuse Package A's retained bundle.
- Read-only investigation verified upstream main `440b15349565bb15f7a88636477864e5a9f8cc72`,
  PR #4767 open at the recorded head, no discussion comments and six action_required workflows.
  Issue #4765 remains open. Deep investigation subagent independently confirmed the next step
  and prior-story continuity. Do not treat GitHub synthetic merge_commit_sha as a merge.
- Verify patch application/tree equality and exact diffs under `run-with-temp`; revalidate
  existing receipts in a disposable copy without overwriting them. No new behavior is added,
  so do not rerun Go tests. Check local links, manifest hashes, append-only history, frozen
  intent, protected inputs, parent statuses and `git diff --check`. Use one independent blind
  document reviewer. Publication requires approval of the completed packet; this local
  preparation spec may finish without completing Story 4.17 or committing repositories.
- Prepared the exact proposed title/body and signed-off B commit message. Preserve the
  original source coauthor; the public commit object is not created or confused with
  the old test subject. The upstream-main diff includes eight A+B files, 2786 insertions
  and 72 deletions; the B-only diff remains byte-identical to the reviewed patch.
- A `run-with-temp` job rebuilt the reviewed tree independently from each diff, checked
  whitespace and revalidated historical receipts in a disposable copy. All 15 required
  receipts, 18 negative controls and coverage checks passed; the recomputed historical
  summary was byte-identical. The job exited 0 and both scratch directories were removed.
- New packet artifacts and hashes are bound by `proposal.json`; `source-verification.json`
  and `upstream-observation.json` distinguish source verification from new test execution.
  A later approved submission must create/capture the public commit, recheck exact inputs
  and branch/PR absence, then retain partial results and verify exact readbacks.
- Independent blind review returned ten findings; checked the actual packet and source
  claims, then patched all ten within local preparation. Retained `verify.py` repeats
  source/receipt verification with command output hashes, actual tree outputs and named
  assertions. A read-only GitHub API fallback supplies workflow head fields omitted by
  the connector; all six runs match A. Public immutable receipt readback matches the
  local Git blob and the repository is public. No upstream write or new behavioral test.
- Final verification passed 11 reconstruction/receipt assertions, all 17 manifest bindings,
  44 local link targets, 399 historical input/prefix comparisons, unchanged frozen intent,
  unchanged canonical heads/indexes and clean EventStore. Both scratch jobs finished and
  their directories were removed; `git diff --check` passed. All ten review findings were
  patched, with nothing deferred. The receipt summary uses `.txt` so it survives normal
  staging; no new packet artifact is ignored.
- Only this local preparation slice is done. Its bounded intent preserves both parent
  in-progress statuses and forbids persistent Git mutations, so generic one-shot parent
  review/commit instructions do not apply. No commit, push or public write was performed.
  The explicit next owner decision is whether to publish the exact proposed A+B upstream
  PR or wait for A and prepare a fresh base-bound packet.
- The owner subsequently answered **yes**, authorizing the exact proposed A+B remote
  publication. The original proposal and all 17 approval-bound artifacts remain unchanged.
  Published branch `jpiquot:fix/rabbitmq-mandatory-returns-4765` with create-only absence
  lease and opened upstream PR https://github.com/dapr/components-contrib/pull/4768.
  Public B commit `c6ba13793f181461afb3c74c9300f5876879861f` has sole parent A and
  the reviewed tree. Remote raw commit/message, identities, incremental diff and exact
  PR title/body/base/head/scope all match. Connector PR creation returned integration
  403; after confirming absence, the authenticated GitHub CLI completed the same request.
  `publication/submission-receipt.json` records approval, partial outcomes and readbacks;
  `SUBMISSION.md` is the current publication status. Both scratch jobs finished and were
  removed. No Platform/EventStore commit/push, existing upstream edits or runtime run.

## Review Triage Log

| Finding | Verdict | Evidence and resolution |
|---|---|---|
| BH1 verification reproducibility | low | Original commands omitted stdout/assertions and receipt command. Retain and execute `verify.py`; its new report records each command, tree and assertion. |
| BH2 workflow head binding | low | Connector results omit head fields despite exact commit query. Retain query parameters and direct API readback with all six exact head SHAs. |
| BH3 author/committer identities | low | Signoff specified the signer but the manifest omitted author/committer. Bind both to the existing local identity and require remote readback. |
| BH4 intake prefix rule | low | The baseline required undocumented prefix handling. Move the intake to an explicit append-only record with byte length and prefix digest. |
| BH5 integration wording | low | The added test executes against a broker when selected; only its local validation was compile-only. Correct PR and commit wording. |
| BH6 delivery guarantee | medium | PR wording asserted at-least-once delivery without broker/runtime evidence. State duplicate-publication risk and no proven end-to-end guarantee. |
| BH7 pinned SDK references | low | Ordering assertion lacked its immutable source. Link the verified v1.14.0 commit's content assembly, return dispatch and notification timeout. |
| BH8 accessible evidence | medium | Upstream reviewers could not find detailed results from the PR text. Link the already-public immutable packet, exact argv and negative-control harness; verify published summary blob. |
| BH9 atomic branch creation | low | Preflight absence alone allowed a concurrent ref collision. Specify atomic create-ref or an explicit absence lease, with no update on collision. |
| BH10 squash-merge scope | low | A merge alone need not narrow GitHub's comparison after squash. Require dependency resolution, checked rebase and fresh approval before branch updates. |
