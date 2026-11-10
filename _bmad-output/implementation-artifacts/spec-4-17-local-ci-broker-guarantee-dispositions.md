---
title: 'Story 4.17 slice: local/CI dispositions of the five failed durable-broker guarantees'
type: 'chore'
created: '2026-11-10'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '34921e9710696bebf26c95bc81a68eab5ea6cdc8'
context: []
---

<frozen-after-approval reason="human-owned intent — owner scoped this 4.17 slice and chose recorded dispositions on 2026-11-10">

## Intent

**Problem:** On Dapr 1.19.1 (components-contrib 1.19.0), with RabbitMQ 4.3.6-management and Kafka 4.3.1, EventStore hosted runs `42532773702` and `42532776864` on `0a6725d51bb17a9eae28a24a53f3636bab9d48d7` fail five of the 24 durable-broker vectors: Kafka passes 10/12 and RabbitMQ 9/12. The failures are RabbitMQ rotation, pressure and missing binding, and Kafka rotation and pressure. Under EventStore's 2026-11-09 course correction (P2.F–H, P5.H), Platform owns concrete broker/component configuration and runs local/CI qualification. EventStore 3.21 only evaluates Platform's results.

**Approach:** Owner decision, 2026-11-10: record dispositions and run nothing. No config candidate can run without editing EventStore: for 1.19.1, `validate_profile` requires the profile `inputs` to match EventStore's checked-in lock, and that lock pins the bytes of the component YAMLs and of the harness that holds the broker settings. Platform records one disposition per guarantee, REJECTED with evidence: hosted observations, pinned upstream source, scenario predicates and upstream status. RabbitMQ pressure and missing binding are code-level, out of reach of configuration. The Kafka rotation candidate does not apply to mTLS. RabbitMQ rotation and Kafka pressure stay observed failures, because no config candidate can run under the pinned harness.

Scope is the LOCAL/CI slice only. The full 24-vector denominator stays. Out of scope: staging acceptance criteria (they wait for Platform 4.14–4.16), broker selection (only the owner's AD-26 ratification selects), `production-profile.yaml`, Builds catalog/CLI/runner pins, any EventStore edit, Redis pub/sub, private Dapr builds and provider exceptions. No new evidence or proof model, and one review pass.

</frozen-after-approval>

## Implementation Notes

- Deliverable: `docs/implementation/story-4-17-local-broker-dispositions-2026-11-10.md`, a single Markdown record. It holds a per-guarantee table (vector, observation, basis, disposition, remedy route and owner), an evidence index, and a statement of what this slice does not claim. It follows the dated-record convention of `docs/implementation/`. There is no code, test, packet or schema.
- Evidence comes only from existing sources, cited by path, line or URL:
  - EventStore refresh spec: `_bmad-output/implementation-artifacts/spec-3-21-latest-stable-dapr-runtime-refresh.md:844-889`.
  - Retained source: `deploy/dapr/qualification/public-identities/1.19.1/components-pubsub-rabbitmq-rabbitmq.go`, sha256 `0ca12e7d…`, which equals upstream tag v1.19.0. Lines 282 and 290-299.
  - Scenario predicates: `tools/dapr_broker_qualification.py:228-233`.
  - Profile pin: `tools/dapr_broker_qualification.py:163-166`.
  - Kafka server properties hardcoded in `tools/qualify-dapr-brokers.py:444`; rotation scenario at `tools/qualify-dapr-brokers.py:711-764`.
  - Kafka KIP-368 scope.
  - Upstream components-contrib: NACK fix `78808be217cc4aea768acbedd0161ab9db04a0b4` (#4698, 2026-10-30) is on `main` and `release-1.19` but in no release. The latest release is v1.19.0; Dapr v1.19.1 embeds it. `mandatory=false` is unchanged on `main`.
- The sprint row `4-17-select-and-qualify-the-durable-production-broker` moves backlog → in-progress and stays there. This slice cannot satisfy the staging/selection/Subscription ACs.
- No push, no CI dispatch, no Docker or broker execution, no cross-repo edit.
- Implemented 2026-11-10. Files: the new record `docs/implementation/story-4-17-local-broker-dispositions-2026-11-10.md`, and `sprint-status.yaml`, where 4.17 moves backlog → in-progress (`epic-4` was already in-progress and `last_updated` already 2026-11-10). The owner committed the refreshed `epic-4-context.md` separately as `34921e97`.
- Surprise: the upstream NACK fix exists but is unreleased. components-contrib `release-1.19` holds `rabbitmq.go` byte-identical to `main` (sha256 `911f596b…`), and so contains #4698. Released v1.19.0 (`0ca12e7d…`) does not. The pressure disposition therefore records requalification after a patch release as its remedy route, owned by EventStore and Builds.
- Every cited harness line was checked at run commit `0a6725d51`. The only cited-input change between it and EventStore HEAD `b2cab541` is two release-metadata digests in the runtime lock (`aa16227bd`), which leaves every cited line unaffected.
- Review correction, superseding the upstream notes above: #4698 (`78808be2…`) is on `main` only. `release-1.19` carries its backport #4748 (`ac89e0966ca5d9ff4dce4ae2ef0045c33e2660ee`, 2026-11-06), so the two files are byte-identical without sharing a commit. The pressure route now names #4748. The profile-pin cite is `163-167`, and the observations are cited by section heading because line numbers drift.
- Sprint deviation: the finalize sync would set 4.17 to `review`. The row stays `in-progress`, with a comment, because the staging, selection and Subscription ACs remain unbuilt and this slice satisfies none of them.

## Review Triage Log

One Blind Hunter pass (floor N = 4; 12 findings). All 12 were verified against the files, GitHub's compare API or the KIP text, and none was false.

| # | Finding | Verdict | Route / evidence |
| --- | --- | --- | --- |
| 1 | Tool-call markup leaked into both new file tails; the spec lacked a final newline | high | patch: tails stripped; `grep` for the markup returns 0 |
| 2 | #4698 is not on `release-1.19`; backport #4748 is | medium | patch: the compare API returns `diverged`; `ac89e0966` reaches `release-1.19` with `behind_by` 0 |
| 3 | Kafka rotation had no route, and per-request ACL evaluation was ignored | medium | patch: basis marks it undiagnosed; route row added |
| 4 | The SASL_SSL claim was outdated (KIP-684, Kafka 2.8) | medium | patch: restated as the principal coming from SASL; source swapped |
| 5 | The "quoted" pass rules were partial paraphrases | medium | patch: full summarized rules, including the attributable `ErrorInfo` |
| 6 | Lock inventory and policy cites were incomplete or wrong | low | patch: 75 inputs; policy at harness 406-408 plus `dapr_broker_qualification.py:21-24`; cite ends at 167 |
| 7 | Disposition vocabulary undefined; negative claims unbounded | medium | patch: vocabulary section; untested candidates listed |
| 8 | EventStore caveats dropped | medium | patch: cold restart, non-universal loss, and the bound-only meaning of `acknowledgedLoss` restored |
| 9 | Follow-ups untracked; no link to EventStore's deferred entry | medium | patch: cross-reference added. Defer: Platform execution of candidates |
| 10 | Platform planning has no acceptance clause for this handoff | medium | defer: needs an `epics.md` planning edit. The record states the owner-directed scope |
| 11 | The `in-progress` row hid what 4.17 waits on | low | patch: sprint comment line |
| 12 | No repeatable verification commands | low | patch: Verification section |

## Verification

**Commands** (run 2026-11-10):
- `git -C ../eventstore show 0a6725d51:tools/dapr_broker_qualification.py | sed -n '21,24p;52,71p;163,167p;228,233p'` -- expected: the cited policy, denial rule, input pin and pass rules.
- `git -C ../eventstore show 0a6725d51:tools/qualify-dapr-brokers.py | sed -n '406,408p;444p;737p;742p;755p;758p;779p;797p'` -- expected: the cited harness lines.
- `git -C ../eventstore show 0a6725d51:deploy/dapr/qualification/public-identities/1.19.1/components-pubsub-rabbitmq-rabbitmq.go | sha256sum` -- expected: `0ca12e7d30f2f37ff87e1b64ee89c11a6d3b5953ce5e60e57e9176d06ac7632e`, equal to upstream `v1.19.0`.
- `git -C ../eventstore diff --stat 0a6725d51 HEAD -- tools/qualify-dapr-brokers.py tools/dapr_broker_qualification.py deploy/dapr/qualification` -- expected: only the runtime lock and two release-metadata JSON files.
- `curl -fsSL https://api.github.com/repos/dapr/components-contrib/compare/release-1.19...78808be217cc4aea768acbedd0161ab9db04a0b4` -- expected: `diverged`. The same compare from `ac89e0966ca5d9ff4dce4ae2ef0045c33e2660ee` gives `behind_by` 0.
- `curl -fsSL 'https://api.github.com/repos/dapr/components-contrib/releases?per_page=1'` -- expected: the latest release is `v1.19.0`.
- `git diff --check`, and `grep -cE '</?(content|invoke|parameter)'` on both new files -- expected: clean, and 0.
