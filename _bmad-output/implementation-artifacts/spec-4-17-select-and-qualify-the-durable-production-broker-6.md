---
title: 'Story 4.17 slice: reconcile the completed broker dependency handoff'
type: 'chore'
created: '2026-11-14'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
baseline_commit: 'bad341e575ffc936ab8d36705e9bbd142f74604d'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — continuation of Story 4.17 under its existing offline boundaries">

## Intent

Reconcile Platform's completed packet preservation, actual RabbitMQ issue submissions and Kafka
source stop decisions into EventStore 3.21's current dependency tracking. Its latest handoff still
reports custody and filing as pending, risking duplicate work and an incorrect next-candidate intake.
Use existing immutable Platform receipts and decisions; preserve the dated preparation history and
the exact previously published work packages. Record the local reconciliation in Platform's intake.

This is factual documentation maintenance within the accepted offline continuation. Keep both
parents in-progress, all 24 vectors and predicates unchanged, five guarantees REJECTED/open,
technical FAIL (20/24), selection null and production eligibility FAIL. No broker execution,
reproduction recipe, new upstream message, storage operation, source/profile/lock change,
runtime adoption, Git revision/index/gitlink change or publication is part of this slice.

</frozen-after-approval>

## Implementation Notes

- Intent gaps: none; missing remedies remain documented dependencies. Irreversibles: none;
  read-only remote checks and local prose. Footprint: this spec, Platform's existing intake,
  EventStore's existing parent spec, remediation evidence contract and existing deferred entry.
  No new callable interface or runtime tests are needed. Use the one-shot route.
- Loaded valid Epic 4 context and completed Story 4.15 continuity: preserve evidence and explicit
  operational unknowns; offline preparation cannot supply live acceptance. Existing Subscription
  rendering already implements the declaration contract and needs no duplicate implementation.
- Platform starts clean on main at the baseline above. EventStore starts clean on main at
  `51dd68b86a690ffe9e11c565dccec49b2d401a94`; remote main is
  `325c3ddcf122d7b6dfc9f2a5190fa88ac4fa1394`. Its intervening commits leave the parent/contract
  unchanged and alter only unrelated deferred-ledger content. Do not update the checkout/gitlink
  or the separate sibling checkout. Snapshot all 38,496 tracked files plus both heads/indexes.
- Source map: immutable Platform baseline above contains `docs/implementation/story-4-17-custody/`
  receipt and post-review checks, `story-4-17-upstream/` plan/receipts, rotation-feasibility and
  controlled-association decisions. EventStore parent current pointer and appended reconciliation
  lead to `docs/implementation/story-3-21-broker-remediation-evidence-contract.md`; append the
  latest disposition to the existing reopened 3.21 item in `deferred-work.md`.
- Preserve the RabbitMQ published work package byte for byte at SHA-256
  `8f34e21a085fb7d63d8c63b7a8a3fe88a6897b6127224523d2d728229e563bbd`. It is a historical
  submission input; update current tracking through the parent/contract rather than rewriting it.
  The Kafka work package is also retained unchanged; the newer Platform stop decisions govern re-entry.
- Fresh connected GitHub reads show #4764/#4765 open with zero comments and unchanged submission
  timestamps. Official latest-release pages still resolve to Dapr v1.19.1/components v1.19.0.
  These are bounded current observations, not acceptance or delivery. Do not reopen Kafka source
  investigation without its concrete dependency.
- Verify the parent's retained stdlib-only offline Bash audit, original receipts/hashes/counts,
  immutable-link targets, new local links, append-only history, frozen parent blocks, both sprint
  files, allowlisted changes, whitespace and snapshot preservation. Use one independent blind
  document reviewer. No behavioral tests are justified for prose reconciliation.
- Parent status is already in-progress, so initial sprint synchronization is a no-op. The existing
  offline boundaries preserve parent statuses and Git state through finalization; only this slice
  may finish. They override the helper's generic final sprint-review transition and local commit.
- Implemented the current pointer and dated EventStore parent/contract reconciliation, appended
  `completed_handoff` to the existing deferred item and recorded the local-only update in Platform.
  The contract uses seven immutable Platform file links and the actual two public issue identities.
  Both original work packages, custody/publication receipts and dated preparation bytes are retained.
- Initial verification: the exact retained parent Bash audit exits 0 (94 locked inputs, original
  24-vector 20/4/five-historical outcomes, execution/configuration identities, authority and statuses).
  `python3 -B /tmp/bmad417-handoff-27yd_1b9/verify_handoff.py` also exits 0: 38,492 protected tracked
  files unchanged, exactly four existing documents changed plus this spec, unchanged heads/indexes/
  gitlinks, preserved history/frozen requirements, seven immutable and six new local links, exact
  custody/publication commitments. Both owning repositories pass `git diff --check`.
- The temporary audit initially expected eight new immutable links; enumeration showed seven and
  its counting assertion was corrected. No document, source or evidence change was needed for that
  test expectation. The independent blind reviewer is evaluating the actual documentation diff.
- Independent blind review completed with five findings, all checked and patched below; no work
  was deferred. Added the existing manual Administrator checkpoint and its eighth immutable
  evidence link, kept Kafka admission independent of RabbitMQ delivery, and added a current intake
  pointer. The historical intake text remains byte-identical after removing that explicit pointer;
  it is no longer a contiguous prefix. This is the same documented reconciliation goal and footprint.
  Removed fresh issue/release-state assertions from the deliverable instead of creating a new
  observation artifact; only the retained actual submissions are asserted there.
- Final post-correction verification passes: the retained audit again verifies all 94 locked
  inputs, original 24-vector 20/4/five-historical results, frozen requirements and execution/
  authority/status identities. The snapshot audit again verifies 38,492 unchanged tracked files,
  exactly four existing document changes plus this spec, unchanged heads/indexes/gitlinks and
  sprint files, original work-package/body/custody commitments, preserved history, eight immutable
  file links and six new local links. Both repositories pass changed-line whitespace checks.
  All five review findings are corrected; none deferred. Only this reconciliation slice is done.
  Both parent stories remain in-progress; no broker run, remote mutation, staging, commit or push.
- Subsequent publication authorization: the user's “push akk changes” means push all changes and
  explicitly authorizes the Git mutations excluded from the completed local build slice above.
  Inspected/fetched all 17 root-declared submodules without recursive updates; only EventStore
  had content changes, and every other recorded HEAD was already reachable on its fetched remote.
- Publication preflight: both exact message files passed each owning repository's commitlint
  21.2.4 with zero problems/warnings. The initial Platform preflight was blocked by a full disk
  before its message file existed; the owned temporary verification baseline was preserved on
  the memory filesystem, freeing space, then both real nonempty messages were validated again.
  No user data or unrelated checkout was removed or changed.
- EventStore's documentation-only commit rebased cleanly onto remote main
  `889d42845e048567663ba6b373bff881cdd3021b`; reviewed parent/contract bytes and the new ledger
  disposition were checked after rebase. The retained 94-input/24-vector audit passed again.
  Published EventStore `cf803659c8bdb965d26b479fc6547a6be08801c7` and verified that exact remote
  main revision before staging its Platform gitlink. GitHub reported “Bypassed rule violations
  for refs/heads/main”: pull-request requirement and seven expected required status checks.
  Preparation-time local-only/Git-preservation statements above remain the original build record.

## Review Triage Log

| Finding | Verdict / route | Checked evidence and correction |
| --- | --- | --- |
| B1 historical acceptance criteria | medium / patch | The new banner grouped accepted criteria with historical preparation statuses. The November 13 approval keeps those criteria binding; the banner now says so explicitly. |
| B2 fresh upstream observations lack retained provenance | low / patch | Existing immutable submission receipts establish creation/content, not this session's later open/comment/latest-release observations. Removed those additional claims from the deliverable. Prior source/release observations remain explicitly dated history. |
| B3 continuing custody checkpoint omitted | medium / patch | The published custody post-review record requires manual read-only review by December 13, 2026 Europe/Paris before the first minimum ends. Added the immutable checkpoint link/date, ongoing hold and validation limits to the contract, carried the date into the parent, and appended the obligation to the existing ledger disposition. No automation or cloud operation is created. |
| B4 combined admission gate | medium / patch | The new paragraph placed EventStore admission after upstream delivery without scoping that to RabbitMQ. The existing contract permits independent workstreams. Added a separate Kafka concrete-input/admission path and limited the upstream-release gate to RabbitMQ. |
| B5 opening intake status lacks current pointer | low / patch | The opening prepared status predated the completed custody/submission sections. Added a nearby pointer to the local EventStore reconciliation and explicitly retained binding criteria and outstanding successor dependencies. Original dated text is preserved. |
