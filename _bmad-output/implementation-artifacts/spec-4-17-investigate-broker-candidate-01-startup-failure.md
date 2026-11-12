---
title: 'Story 4.17 slice: investigate broker candidate-01 pre-job startup failure'
type: 'chore'
created: '2026-11-13'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
review_loop_iteration: 0
baseline_commit: '0c97b496f3d9a4fc9a82c656ff1c159fba54325c'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The single authorized hosted attempt for broker candidate 01, GitHub run `43097161643` attempt 1 at `94001fad925fdf7fa03a90b507b8f8e19074f9e3`, ended `startup_failure` with zero jobs, zero artifacts and 0/24 observed vectors. Its root cause is unrecorded, and no further hosted attempt can be decided on without a diagnosis.

**Approach:** Use the retained receipts and read-only GitHub diagnostics to classify the failure as a workflow defect, a provider failure or an unresolved cause. Fix only demonstrated repository defects, verified by focused offline checks. Retain the new diagnostics as committed evidence. Prepare, without executing it, a concrete proposal for one further hosted attempt covering exact source, inputs, worker requirements and expected verification.

Do not dispatch, rerun or cancel any workflow run; the previous single-attempt authorization is consumed. Preserve the original attempt's records and the exact candidate, configuration and source identities. Platform 4.17 and EventStore 3.21 stay in-progress, all five rejected guarantees stay REJECTED, broker selection stays null and production eligibility stays FAIL. Record commands, results, full source revisions, remaining uncertainty and the next owner decision.

</frozen-after-approval>

## Implementation Notes

- Investigation findings (all GitHub calls were `GET`, user `gh` login; scratch copies under the session scratchpad):
  - Run `43097161643` is unchanged: attempt 1 only, `previous_attempt_url` null, `startup_failure`, `run_duration_ms` 266000, `billable` empty. It has 0 jobs (`filter=all`), 0 artifacts and logs return HTTP 404. It is the only run of workflow `415133929` (state `active`) and the repository's only `startup_failure`.
  - Provider window: six repository push runs (tuple-ci and catalog-builder, heads `94001fad`, `488723fd`, `fce6fd57`) created 20:32:49Z–20:44:07Z are still `queued` with zero jobs. The retained queued-run observation lists 6 of 6; an earlier ad-hoc query's `total_count` of 7 was not reproduced and is not retained. Runs created from 20:54:28Z were allocated (first job 20:58:05Z) using the same action pins. Catalog-builder's `cancel-in-progress` concurrency group did not cancel its orphaned queued runs. githubstatus incident `dvy0t3mvh1lg` (critical: Actions degraded availability) was posted at 20:48:12Z, mitigated at 21:07:24Z, and its backlog fully processed by 21:21:32Z. A minor Webhooks incident `1h2wbly1y72t` ran 20:22–20:26Z.
  - Newly observed confounder: the repository activity API shows `branch_creation` of `refs/heads/chore/broker-candidate-01-hosted-94001fad` at 20:37:25Z and its `branch_deletion` at **20:39:42Z** by the repository owner account. That was while the run was queued, 2m12s before its terminal update. The prior receipts do not record this deletion. Platform is a submodule of 15 local superprojects, and concurrent agent sessions were syncing them at 20:39Z. The committed `/pushall` skill prunes any remote branch whose tip is an ancestor of main, and `94001fad` is one. A bounded search of local Codex/Claude logs did not identify the deleting process.
  - No workflow defect demonstrated. The workflow blob is `9130ed4243e3961326e984e8fd22e49b70695aa4` at both `94001fad` and baseline. Actions policy is `allowed_actions: all` with `sha_pinning_required: false`. The same checkout/upload-artifact SHAs ran in catalog-builder at 20:54Z. `ubuntu-24.04` is a standard hosted label. The annotation is the generic service error, not a validation message. Validation failures terminate immediately rather than after 4m26s queued.
  - Classification: provider failure, with an unresolved exact mechanism. The ref deletion during a provider-delayed queue is a possible contributor. It is neither excluded nor proven.
- Proposal basis: tag ruleset `24657570` makes every tag non-updatable and non-deletable with no bypass. Ruleset `24657217` restricts non-`v*` tag creation to owner bypass. No workflow triggers on tags. Main's EventStore gitlink is now `bb9e2b51fc8a609181077eba2ae900bcbe40745a`, so main cannot be the source.
- Decisions: no repository code changes (no demonstrated defect). Do not modify `/pushall` (its causality is unproven and it is outside this scope). Retain the new diagnostics in a separate evidence directory with its own manifest, and leave the original attempt's evidence bytes unchanged. Add forward pointers to the existing reports without rewriting the dated content.
- Sprint sync to `in-progress`: no-op; 4.17 was already `in-progress`.
- Files added:
  - `docs/implementation/story-4-17-broker-candidate-01-startup-investigation.md`: the report.
  - `_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/`: 20 files, 131,956 bytes, plus `file-manifest.json`. It holds the 47 read-only receipts in `commands.json` (request IDs and raw-body digests), the run, workflow-inventory, runs-window, cross-repository, ref-lifecycle, policy, trigger-survey and provider-status snapshots, the offline outputs, `source-identities.json`, `local-log-search.json`, `classification.json`, `next-attempt-proposal.json`, the collector and `verify-manifest.py`.
  - The collector is retained in the evidence directory as `collect_startup_evidence.py`. It issues GET requests only and replaces logins and user ids with role labels.
- Files appended without rewriting dated content: the hosted report (later-investigation section), the CI handoff (pointer), `sprint-status.yaml` (comment and `last_updated`), `epics.md` (4.17 note) and `deferred-work.md` (update on the open diagnosis entry, which stays open). `epic-4-context.md` was regenerated by step 1 because `epics.md` was newer.
- Checks:
  - `node --test tests/source-release/broker-candidate-workflow.test.mjs`: 53/53, exit 0. This used the baseline test file against the identical workflow bytes.
  - actionlint 1.7.12 on the baseline and the `94001fad` workflow bytes: exit 0, no findings. shellcheck and pyflakes integrations were not installed.
  - Candidate SHA-256 unchanged.
  - Original hosted manifest: 79/79 commitments, 151,443 bytes.
  - `git diff --check` clean; 38/38 local links resolve.
  - EventStore sprint-status `3-21` is still `in-progress` (read only).
- Surprise: the dispatch branch deletion. Because it is a confounder rather than a demonstrated defect, it is recorded rather than fixed. Changing `/pushall` stays a separate owner decision.
- Review patches:
  - The collector was re-run with pagination; the earlier single page had silently missed the 20:32:47Z main push.
  - Added the cross-repository and trigger-survey records, the ref-deletion leads, `verify-manifest.py`, and real timestamps and argv.
  - The confounder is reworded as one of four differences.
  - Added the queue-timeout, ruleset-recheck and governs rules to the proposal; the report is aligned to the JSON.
  - The login and scratch path are removed from this spec.
  - Restored the dropped AR-32 tiers, skipped-check, broker-disposition, Rancher and intake/export ordering rules in `epic-4-context.md`.
  - Added three deferred-work entries.
- Final checks:
  - New manifest: 20/20 commitments, 131,956 bytes.
  - Original manifest: 79/79, 151,443 bytes.
  - All JSON parses; 44/44 local links resolve; `git diff --check` is clean.
- Sprint sync to `review` was skipped: the frozen intent keeps Platform 4.17 `in-progress`, as in the previous 4.17 slice.

## Review Triage Log

Blind Hunter (one layer; no layers skipped) returned 17 findings. Each was checked against the files and against GitHub GET results.

| # | Finding | Verdict | Evidence and route |
| --- | --- | --- | --- |
| 1 | Confounder called the "only observed difference" | medium | It also differs in event type, the `ubuntu-24.04` label, and being a first run of a new workflow. Patched: listed as one of four differences. |
| 2 | Activity timeline incomplete | medium | Confirmed: the unpaginated 100-event page stopped at 20:32:49Z and missed the 20:32:47Z main push to 94001fad. Patched: paginated query, 251 events, completeness flag. |
| 3 | Deleting-process leads skipped | medium | `delete_branch_on_merge` true explains only the PR-head deletion. Pull requests with the dispatch branch as head: 0. Audit log: 404. Security log: no API. Patched into the lifecycle and log-search records. |
| 4 | Provider window lacks start and cross-repo check | medium | Confirmed gap. Added 43 stuck runs across six repositories (20:29:41Z–20:52:47Z) and the earlier completed bounds. Patched. |
| 5 | Validation-timing claim unsourced; CLI hint unaddressed | medium | Real. Patched: the claim moved to uncertainty and the hint is assessed explicitly. |
| 6 | No rule for a run that stays queued | medium | Real, given that 43 runs never left queued. Patched: 30-minute queue timeout, cancellation needs authorization, the run consumes the attempt. |
| 7 | No ruleset/tag re-check; no trigger receipt | medium | Real. Patched: go/no-go checks the rulesets before and after and tag non-existence; `workflow-trigger-survey.json` retained. |
| 8 | Report and JSON disagree | low | Real. Patched: JSON governs, report aligned, SCRATCH path matched. |
| 9 | Login and local path in spec | medium | Real: the public repo uses role labels. Patched. |
| 10 | Collector not retained; no request IDs or digests | medium | Real. Patched: collector retained; receipts carry `X-GitHub-Request-Id` and raw-body SHA-256. |
| 11 | Placeholder argv; rounded timestamps | low | Real. Patched: real verifier argv, actual timestamps, manifest and proposal timestamped. |
| 12 | 53/53 unqualified | low | Real: the baseline test file was used. Patched wording in offline-checks, report, ledger and spec. |
| 13 | Follow-ups untracked | medium | Real. Deferred: three new `deferred-work.md` entries (/pushall prune hazard, orphaned queued runs, GitHub Support and RCA). |
| 14 | "Do not block" unsupported | low | The broker workflow has no concurrency group or `workflow_run` dependency. Patched wording; missing sibling CI results folded into the orphaned-runs entry. |
| 15 | Epic context drops AR-32 tier content | medium | Confirmed against `epics.md:448-449`. Caused by this run's regeneration. Patched. |
| 16 | Epic context drops dependency rules | medium | Confirmed: intake (4.8), Rancher non-blocking and export-before-publication. Patched. |
| 17 | Epic context drops skipped-check and broker-disposition rules | medium | Confirmed against `epics.md:99` and the prior cache. Patched. |

