---
title: 'Story 4.17: submit RabbitMQ Package A as an upstream pull request'
type: 'chore'
created: '2026-11-14'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'a215204225396ee695756f934c2e8a172ba7f311'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The reviewed Package A patch for dapr/components-contrib #4764 (`contribution.patch`, hash in its `test-results.json`) exists only locally, so it cannot progress toward a supported Dapr release. Third-party PR #4766 (head `14bf2f783dff41ef2c927fe1aeb6999c113ffff3`) partially overlaps it, covering only the retry-wait `nil` return.

**Approach:** The owner selected "Submit Package A PR". Fork `dapr/components-contrib` to `jpiquot`. Apply the patch to official main with the decided changes below, re-run the recorded checks plus the upstream lint gate, and open one DCO-signed PR. The PR credits #4766 and offers to rebase or split. Record the result in Platform and EventStore.

**Owner decisions, review round 1 ("Fix both, then submit"):**
- Restore `Publish`'s warn/error logging that the patch deleted. Log a warning for each failed attempt and each reconnect wait, and an error at retry exhaustion, using the official base's messages. Returned errors are unchanged.
- Keep the transport abort during the confirmation phase. Investigation found it is required: amqp091 v1.14.0 `NotifyPublish` deadlocks the connection when listener capacity is below the outstanding publishings, and the adapter's capacity-1 listener relies on the abort to bound them. Per the owner's fallback, the PR body discloses that cancellation during exchange declaration, write or confirmation closes the connection's transport, so its subscriptions reconnect and unacknowledged deliveries are redelivered. The body offers a dedicated publisher connection or a drained listener as a follow-up.

## Boundaries & Constraints

**Always:** Do the work in a disposable `run-with-temp` checkout of official main. Proceed only while main is `440b15349565bb15f7a88636477864e5a9f8cc72` or contains no `pubsub/rabbitmq` change since then. Before any fork, push or PR, show the owner the exact PR title, PR body, commit message and source delta, then HALT for an explicit yes. Use one commit, signed off with the local git identity (`git commit -s`) and ending with the Co-Authored-By attribution. The PR body ends with the Claude Code attribution line. Reference the issue as "Partially addresses #4764" and list what the PR does not include: integration/certification tests, Dapr runtime mapping, Package B. Reference #4766 by number only, with no @mention. Keep both parent stories in-progress. Keep technical FAIL (20/24), five REJECTED/open guarantees, selection null and production FAIL.

**Never:** Change patch behavior beyond the decided logging restoration and behavior-neutral lint fixes. Use a closing keyword for #4764. Comment on, label or edit #4764, #4765 or #4766. Push Platform or EventStore. Run a broker, Dapr runtime or qualification. Adopt a runtime. Claim upstream acceptance or a delivery gate. Retry a failed submission with altered content without approval.

## I/O & Edge-Case Matrix

| Scenario | State | Expected | Handling |
|---|---|---|---|
| Clean base | main unchanged for `pubsub/rabbitmq` | Patch applies; the tree matches it | Main moved in rabbitmq, or apply fails → HALT |
| Lint finding | golangci-lint v2.10.1 flags a new issue in changed files | A mechanical, behavior-neutral fix, then a full re-check; the delta is shown at approval | A behavioral fix is needed → HALT |
| Restored logs | An attempt fails, a reconnect wait starts, or retries are exhausted | Warn on each failed attempt and reconnect wait, error at exhaustion; the same returned errors | — |
| #4766 merges first | Its change lands on main | — | HALT; a rebase would change the reviewed patch |
| Owner declines | No yes on the exact text | No fork or push; recorded as "prepared, not submitted" | — |
| Partial failure | Fork or push succeeds, then PR creation fails | Retain the partial state and error; no altered retry | Report |
| Readback | PR created | Title, body and head SHA match the approved bytes | Record any mismatch; no edit without approval |

</frozen-after-approval>

## Code Map

- `docs/implementation/story-4-17-upstream/package-a-local-2026-11-14/` -- read-only source: `contribution.patch`, `README.md` (behavior, gaps), `review.md`, `test-results.json` (check argv and counts: 61/192 full unit, 220/680 focused race, 10/10 legacy race).
- Prior round, moved out of the tree: `/tmp/claude-1000/-home-administrator-projects-hexalith-platform/d00d5e14-440e-4b09-8b93-d61871423c09/scratchpad/prior-round/package-a-pr-2026-11-14-round2/`. Reuse its `workspace.py`, `checks.py`, `verify-subject.py`, `summarize-checks.py`, `lint-fix.patch` (verified behavior-neutral), PR texts and README as the starting point. Copy its evidence into the new directory under `history/round-2-unchanged-patch/`, labeled superseded.
- `docs/implementation/story-4-17-upstream/package-a-pr-2026-11-14/` -- recreate: scripts, identities, receipts/logs, `pr-delta.patch` (lint plus logging, relative to base plus `contribution.patch`), PR texts, bundle, `submission-receipt.json`, `README.md`.
- Upstream `pubsub/rabbitmq/rabbitmq.go`. The base `Publish` loop at `440b1534…` holds the three messages to restore: `publishing failed`, `publisher is reconnecting in %s ...` and `publishing attempt (%d/%d) failed`. In the patched `Publish`, failures flow through the `remember`/`result` closures and the `reconnect` branch, so log there without changing control flow. `publishSync`'s `abortOnCancel` stays as is.
- Upstream CI: golangci-lint `v2.10.1` with `only-new-issues`; its `.golangci.yml` build-tags are `certtests`, `conftests` and `metadata`, and no formatters are enabled. There is a mod-tidy diff; `make test` runs without `-race`.
- Records: `docs/implementation/story-4-17-next-candidate-intake.md` (a dated section plus one "Current handoff" sentence); EventStore `docs/implementation/story-3-21-broker-remediation-evidence-contract.md` (a dated section) and `_bmad-output/implementation-artifacts/deferred-work.md` (an `update:` line on the broker item with source_spec `spec-3-21-latest-stable-dapr-runtime-refresh.md`), local commit only; `_bmad-output/implementation-artifacts/sprint-status.yaml` (a dated 4.17 comment). gh is `jpiquot` with `repo` scope; no fork exists.

## Tasks & Acceptance

**Execution:**
- [x] Source delta -- Restore the three log calls, and add a focused unit test that captures logger output (dapr/kit `logger.SetOutput`) to assert them. Keep `lint-fix.patch`. Write `pr-delta.patch`.
- [x] Checks -- In held run-with-temp jobs: clone official main and record its identity, apply `contribution.patch` plus `pr-delta.patch`, and commit on `fix/rabbitmq-publish-errors-4764`. Run the recorded checks plus `go mod tidy -diff`, golangci-lint `--new-from-rev=<base>` and `gofmt -s -l`. `summarize-checks.py` must FAIL on a missing required receipt or on counts below the local baseline. Save a thin bundle with the head SHA and tree.
- [x] Submission procedure -- `README.md` steps and a preflight script. The preflight HALTs unless main is the base, or no change since the base touches `pubsub/rabbitmq`, `common/component/rabbitmq`, `go.mod`, `go.sum` or `.golangci.yml`, and unless #4766 is OPEN at head `14bf2f783dff41ef2c927fe1aeb6999c113ffff3`. It fetches the base by SHA before using the bundle. Readback parses the `gh` JSON and compares the UTF-8 bytes exactly.
- [x] PR texts -- Add the abort disclosure and follow-up offer. Name the conformance suite as not run. Keep the existing rules and limits.
- [ ] Approval gate (parent) -- Present the texts, delta and check results, then HALT.
- [ ] Submit (parent, after the yes) -- Run the preflight, fork, push the bundled commit, `gh pr create`, read back, write `submission-receipt.json`.
- [ ] Records (parent) -- PR identity, remaining gates and unchanged dispositions.

**Acceptance Criteria:**
- Given the submitted PR, when it is read back, then its head tree equals base plus `contribution.patch` plus `pr-delta.patch`. Its title and body bytes equal the approved files, and the commit carries a Signed-off-by line.
- Given the completed slice, when the records are inspected, then the delivery gate remains an official supported Dapr release followed by EventStore admission, Builds alignment and a separate Platform full-matrix proposal. All qualification dispositions are unchanged.

## Implementation Notes

Implementation agent scope: the first four Execution tasks only, all local. Step-03 forbids remote writes; read-only clones and downloads are fine. The parent session runs the approval gate, submission and records after review.

## Spec Change Log

- Review round 1 (ECH1/BH11, ECH2, ECH14 → intent_gap). The owner chose "Fix both, then submit". Investigation of amqp091 v1.14.0 `NotifyPublish` showed the confirmation-phase abort is required, so the owner's fallback (keep and disclose) applies; only the logging is restored. The frozen decisions, matrix row, Code Map, Tasks, AC and Verification were amended, and the process patches (BH2/ECH6, BH3/ECH5, BH4/ECH7, BH10, BH6, BH12) were folded into the tasks. Known-bad state avoided: a public PR silently deleting retry/reconnect logs and not disclosing that one caller's cancellation disrupts the connection's consumers. KEEP: the verified lint delta; the text rules ("Partially addresses", no @mention, attribution last, no private markers); the fresh-clone bundle verification; the 14-receipt check set and its counts; the round-1 and round-2 evidence as labeled history.

## Review Triage Log

Round 1 (diff: Platform artifacts plus upstream subject `a8c884bc298d076a08dac03cf56805971c239cb8`; Blind Hunter BH, Edge Case Hunter ECH, Verification Gap VG). All layers reported before triage.

| ID | Verdict | Evidence and route |
|---|---|---|
| ECH1/BH11 | high | `publishSync` arms `abortOnCancel(ctx, r.connection)` until its deferred stop, which covers the confirmation wait. Subscriptions consume on the same `r.channel`/connection (`ensureSubscription`). One caller's timeout during confirmation therefore aborts the transport for all consumers, although `WaitContext` already honors ctx. The transport test's "confirmation" stage asserts this abort deliberately, and the PR body does not state the shared-connection consequence. Fixing it changes reviewed patch behavior, which the frozen Never forbids → **intent_gap**. |
| ECH2 | medium | Same root cause as ECH1. In the confirm branch, a cancellation between ACK and the deferred `stopAbort` aborts the healthy connection after Publish returns nil. The no-confirm branch already stops first. Grouped with ECH1 → **intent_gap**. |
| ECH14 | medium | The patch deletes Publish's `Errorf "publishing failed"`, `Warnf "publisher is reconnecting"` and `Warnf "publishing attempt (%d/%d) failed"`, with no replacement. `reconnect` logs only Info lines, so retry and reconnect failures lose warn/error visibility. Restoring them changes the patch → **intent_gap**. |
| ECH3 | medium | `authorizationError` matches any `amqp.Error` with Code 403. The SDK's generic `ErrCredentials` (Server=false), which a transient SASL-stage drop can produce, becomes terminal; before, it was retried. This Package A design choice is already disclosed in the PR body's known limits. It is not caused by this slice → **defer**. |
| BH2/ECH6 | medium | The `workspace.py` preflight halts only on MERGED, and README step 2 rechecks only "unmerged". The body's #4766 relation text is stale if that PR's head moves or it closes → **patch** (the submission preflight requires OPEN and head `14bf2f783dff41ef2c927fe1aeb6999c113ffff3`). |
| BH3/ECH5 | medium | The base guard diffs only `pubsub/rabbitmq`. A main move in `go.mod`, `go.sum`, `common/component/rabbitmq` or `.golangci.yml` would pass unchecked → **patch** (widen the submission guard paths). |
| BH4/ECH7 | medium | If main moved, the depth-1 clone in README step 3 lacks the bundle prerequisite `440b1534…`, so bundle verify/fetch fails at submission → **patch** (fetch the base by SHA before using the bundle). |
| BH10 | low | Readback extraction is unspecified. `gh --jq` appends a newline, which would produce a false mismatch under the no-edit rule → **patch** (document a JSON-parse, UTF-8 exact-compare readback). |
| BH6 | low | The body's not-run list names integration and certification tests but not the conformance suite, which also was not run. The compile-risk part is false: no exported symbol changes, and only `NewRabbitMQ` is used outside the package (VG confirmed) → **patch** (body text). |
| BH12 | low | `test-results.json`'s round1 note says "the same checks", but round 1 lacked the two certification tidy checks. The README table places the supplementary compat check with the required tidy row → **patch** (README wording). |
| BH1/ECH16 | low | The AC says "base plus the patch", while the tree also contains the lint delta. That delta is authorized by the frozen Lint-finding row and verified byte-identical. The fix is a spec edit → rejected. |
| BH5 | false | The upstream `.golangci.yml` build-tags are `certtests`, `conftests` and `metadata`, not `integration_test`, so the local lint equals CI's. The integration-tag compile also ran go test's vet subset. |
| BH7/VG1/ECH10 | low | `summarize-checks.py` skips missing receipts and has no count floors (VG reproduced this). The committed run has all 14 receipts, and VG verified the counts against the local package. Unlikely in use, and the fix adds guards → rejected (VG's filed disposition was defer). |
| BH8/ECH9 | low | The marker check is case-sensitive. A case-insensitive grep of the title, body and commit for hexalith/administrator/platform/eventstore/qualification/candidate and `/tmp/`/`/home/` finds nothing → rejected. |
| BH9 | low | Local paths in receipts already appear in published Package A artifacts (for example `checks-environment.json`, `*-command.txt`). They are not secrets and match existing practice → rejected. |
| BH13 | low | Round-1 scripts were not preserved. The `lint-fix-dev*` receipts record `cleanTreeBefore: false` and sit in `round-1-reviewed-patch/` → rejected (an intermediate record, honestly labeled). |
| BH14 | maybe-false | `.golangci.yml` enables no formatters, so `gofmt -s` was not checked on the final tree; no local Go toolchain exists to settle it. If true it would be low → rejected; run `gofmt -s -l` in the next check run. |
| ECH4 | false | Production `dial` never returns (nil, nil); only a hypothetical injected test dialer could, and it would fail loudly. |
| ECH8 | low | A `verify-subject.py` crash on a moved main fails loudly; the earlier PASS truthfully records the earlier check → rejected. |
| ECH11 | low | No urlopen/subprocess timeouts; a hang is visible and the run completed → rejected. |
| ECH12 | false | The host has no GOFLAGS/GOPROXY/GOSUMDB/GOPRIVATE environment and no user go env file, so the receipts are unaffected. |
| ECH13 | low | 1 s cancellation budgets; upstream `make test` runs without `-race`. Low flake likelihood, and the fix changes patch tests → rejected. |
| ECH15 | low | The unexported `rabbitMQ` is built only via `NewRabbitMQ` and test helpers, which initialize `channelMutex` → rejected. |
| VG2 | low | `verify-subject.py` requires #4765 in the body; this matters only if the owner drops that reference at approval, and then the rule must change → rejected (handle at the gate). |

### Preparation review round 2

All three layers reported before triage. Subject: `96c3259b7c12a476edd208848153d3cf72a8c4f0`; diff includes the restored logging and preserved historical evidence. Individual verdicts precede grouping.

| ID | Verdict | Evidence and route |
|---|---|---|
| B2-1 | medium | The summarizer records argv without comparing it to the required command manifest. A mislabeled successful command can satisfy a required gate; the reviewer reproduced vet replaced by true. **patch**, grouped with E2-1: require exact command arrays. |
| B2-2 | low | Removing the new logging test events while recomputing log hashes still clears the deliberately retained baseline floors. Actual full-unit and focused-race logs contain the new test and all three subtests, and bind the immutable subject containing them. Reject: a further test-name guard addresses manipulated evidence outside ordinary use and adds guard complexity. |
| B2-3 | medium | Submission preflight trusts saved summary PASS values, so deleting a required receipt afterward is not detected there. **patch**: recompute the offline receipt gates in disposable storage before accepting them, without overwriting original evidence. |
| B2-4 | false | The helper compares current file bytes, but its documented caller must first confirm the approved text/delta hashes (README submission step 3); altered local text would halt before creation. The pending receipt intentionally has approvedTexts null until the explicit yes. No approval is inferred now; preserve the caller check. |
| B2-5 | low | A deliberately supplied different PR with the same head/text could pass the helper. The documented caller uses the just-created PR identity, fixes the target repository, and saves its returned identity. Reject additional identity guards for this unlikely caller misuse. |
| B2-6 | low | The GitHub login observation is from preparation and may become stale before fork. **patch**: directly document a fresh jpiquot identity check immediately before writes. |
| B2-7 | low | The overlapping PR may change between the first preflight and PR creation. **patch**: rerun the same upstream guard immediately before creation and retain the result; reduce this unavoidable external-state race without claiming atomicity. |
| B2-8 | medium | Readback records nothing when the gh command or JSON parsing fails. Those failures can occur after successful creation and must leave durable evidence. **patch**: retain attempted identity, raw output/error and failure status; exercise the failure paths with disposable mocked responses. |
| B2-9 | low | The PR checklist rationale says no documented behavior change although the body describes changed cancellation and subscription effects. **patch**: replace that rationale with the concrete lack of metadata/configuration changes and reference the disclosed effects. |
| B2-10 | low | The logger test uses repeated failures; mixed conclusive/transport error selection already has passing component tests, and the error log formats the exact same local err returned at exhaustion. Reject an additional low-value fixture: it adds setup for unchanged selection behavior. |
| E2-1 | medium | Same demonstrated command-label gap as B2-1: required receipt names do not establish that the required command ran. **patch**, grouped with B2-1; no separate fix. |
| VG2-1 | medium | Pre-verified broken verification: removing go.mod from GUARD_PATHS also removes its generated fixture case, leaving verification green. **patch**: independently specify all five paths and exercise check_upstream against disposable Git changes, including moved shallow-main base retrieval. |

All surviving fixes concern local preparation guards, tests or wording. No change to the approved upstream source behavior, commit, bundle, qualification dispositions or remote-write boundary is required.

## Design Notes

"Partially addresses" keeps #4764 open for the work this PR omits. The #4766 number reference already cross-links, so the PR needs no @mention.

## Verification

**Commands:**
- Held-job checks -- expected: all exit 0; full unit at least 61/192 and focused race at least 680 pass events, with no skips; 0 new lint issues; empty `gofmt -s -l`; no tidy diff.
- `git diff --stat <base> HEAD` -- expected: only `pubsub/rabbitmq` files.
- The preflight, then `gh pr view <n> --json title,body,headRefOid` parsed as JSON -- expected: the approved bytes and the pushed SHA.

## Current preparation verification

The first four tasks are complete on signed-off upstream subject `96c3259b7c12a476edd208848153d3cf72a8c4f0`. All 14 receipts pass (unit 62/196, focused race 230/720, legacy race 10/10); fresh-clone tree/message verification and disposable preflight/readback/receipt-failure fixtures pass. All implementation jobs exited and cleaned up. See [prepared package](../../docs/implementation/story-4-17-upstream/package-a-pr-2026-11-14/README.md).

The clean-base, lint, restored-logging, overlapping-PR and readback matrix paths have passing checks. Owner decline and partial remote failure remain parent procedures at the explicit approval boundary: the receipt says prepared, not submitted with no remote writes; submission has not occurred. The submission acceptance criterion and final cross-repository records remain pending. The preparation review precedes that required checkpoint.

### Preparation review outcome

All seven surviving review corrections (grouping B2-1/E2-1) are complete: exact command matching; fresh disposable receipt recomputation; durable command/parse failure receipts; fresh identity and pre-create upstream observations in the submission procedure; accurate PR checklist wording; and independent five-path shallow-Git tests. Parent verification passed all 14 receipt checks, 89 procedure assertions and fresh official-clone source/message/text verification. No new finding was deferred. Existing round-1 deferrals and unchanged delivery gates remain.

Approval checkpoint: the exact prepared title, body, commit message and delta are presented for an explicit owner yes. No fork, push or PR has occurred. Submission and final Platform/EventStore records remain incomplete; this spec remains in-review and the parent sprint story remains in-progress until those tasks are fulfilled. The workflow completion/status step does not apply before this explicit human checkpoint.
