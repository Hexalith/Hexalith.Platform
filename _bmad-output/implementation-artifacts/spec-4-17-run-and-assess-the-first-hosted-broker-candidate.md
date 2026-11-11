---
title: 'Story 4.17 slice: run and assess the first hosted broker candidate'
type: 'chore'
created: '2026-11-11'
status: 'in-progress'
route: 'dispatch'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
review_loop_iteration: 0
baseline_commit: '13aa784958dd634196c540713b3513e67af458ed'
context:
  - '{project-root}/references/Hexalith.EventStore/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="human-owned intent — user chose main publication and one hosted execution">

## Intent

**Problem:** Candidate 01 and its manual workflow are implemented, but unpublished. No hosted result exists; all five historical guarantees remain REJECTED.

**Approach:** Publish the existing candidate through the owner's chosen route, execute the fixed hosted workflow once, retain its evidence and independently assess the resulting packet. Produce a truthful first-stage result even when execution fails.

**Decision (2026-11-11):** The user answered "main" to the explicit publication/execution choices: publish the current main history, including the existing Story 2.8 commit, then execute the hosted candidate once. Preserve any independently published remote history through ordinary forward integration; do not force-push or bypass repository rules. This grants the specific remote publication and live dispatch authorization withheld by the prior implementation-only slice.

## Boundaries & Constraints

**Always:** Preserve candidate SHA-256 `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc`, EventStore `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213`, explicit `runtime-lock-1.19.1-platform-config-v1.json`, both brokers and all 24 vectors. Record the full actual execution revision separately from the JSON's declared, unauthenticated provenance. Recheck remote divergence and publication rules before mutation. Publication must place the workflow on the default branch and may trigger existing tuple/catalog CI and automatic source tag/GitHub release after their gates pass. Keep capture, integrity, technical qualification, production authority, cleanup and artifact delivery separate. Preserve both parent stories in-progress.

**Never:** Publish or dispatch before explicit approval; force-push or bypass repository rules; run infrastructure locally; change candidate, harness, locks or nested submodules; silently include unrelated changes; retry automatically; select a broker, grant an exception, or claim staging/production/Subscription acceptance. Unexpected publication divergence or required implementation changes return for assessment.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Behavior |
| --- | --- | --- |
| Publication ready | Approved scope and unchanged remote | Publish reviewed source through permitted controls; record exact revision |
| Publication blocked | Changed remote, unavailable gitlink, permission or rule refusal | Stop before dispatch; retain sanitized reason |
| Completed experiment | One run at published revision | Retain run/job identities, artifacts and separate actual exits |
| Technical rejection | Capture or technical failure | Retain all observed vectors; do not promote integrity to technical success |
| Interrupted or missing evidence | Cancellation, timeout, absent artifact or cleanup proof | Record incomplete; do not retry or invent results |
| Unexpected authority | Verifier success or unexpected failure | Preserve actual output; grant no production authority |

</frozen-after-approval>

## Code Map

- `.github/workflows/broker-candidate-qualification.yml`: reuse unchanged; manual Ubuntu 24.04 job, 40-minute bound, exact allocation/input guards and independent evaluations.
- `docs/implementation/story-4-17-broker-candidate-01{.json,.md,-ci.md}`: original input, admission provenance, dispatch/download commands and evidence limitations.
- Candidate commits `15926cac7ffe7bb91926617a586f34262beb0665` and `13aa784958dd634196c540713b3513e67af458ed`: existing reviewable implementation; intervening Story 2.8 is outside focused publication.
- `.github/workflows/{tuple-ci,catalog-builder,source-release}.yml`: existing publication side effects and independent source-release gates; do not weaken them to publish.
- EventStore `tools/validate-dapr-broker-qualification.py`: run three modes on retained packet, each with the separate original `--platform-config`; use pinned source unchanged.
- `tests/source-release/broker-candidate-workflow.test.mjs`: existing executable matrix controls; no new runner or test framework.

## Tasks & Acceptance

**Execution:**
- [ ] Existing candidate files above — publish only approved history, validate authored Git/PR messages with pinned commitlint and verify remote gitlink availability. Record actual source/run binding; never infer publication from local refs.
- [ ] `.github/workflows/broker-candidate-qualification.yml` — dispatch once at the approved published source; monitor terminal outcome and download its named artifact into a fresh destination before expiry. Record failure without unauthorized reruns.
- [ ] `_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/` — retain sanitized run/artifact metadata, exact downloaded bytes/digests, verifier argv/exits and per-vector assessment. Keep downloaded artifacts outside existing source inventory; inspect archive paths before extraction and use the independently supplied original for verification. No raw credential/private-transcript publication.
- [x] `docs/implementation/story-4-17-broker-candidate-01-hosted.md` — report actual results, full source identities, evidence links and remaining failures. Update current CI handoff and narrowly scoped tracking notes without rewriting dated dispositions.
- [x] `_bmad-output/implementation-artifacts/{sprint-status.yaml,deferred-work.md}` and planning `epics.md` — record bounded outcome and unresolved next work; preserve parent statuses and original acceptance criteria.

**Acceptance Criteria:**
- Given an approved route, when publication and dispatch complete, then evidence identifies the actual source and exactly one hosted attempt.
- Given any matrix outcome, when assessed, then actual independent exits, vector coverage and cleanup limitations determine the report; missing evidence stays incomplete.
- Given this slice completes, when tracking is inspected, then broker selection remains null and broader diagnosis, staging and production acceptance remain open.

## Implementation Notes

- The explicit user choice supplies both publication-route approval and approval to continue this scoped spec; no duplicate confirmation is required. This specific authorization overrides the generic Step 3 no-remote-operations default for the named publication and single dispatch only.
- Remote main advanced independently to `09946e760b56a7a6ab9675503f2b018d33d45f2c` (Keycloak workload binding, 12 additive paths) after the initial observation at `b4b4eda9120e89a52afe6576f46bb7706860f3d1`. Inspect integration and rules before publication. Preserve its existing content; it is already published, outside this implementation's ownership. The original baseline remains unchanged. Current uncommitted files are this spec and the workflow-generated epic context only.
- Parent sprint story and epic already remain in-progress; synchronization is a no-op. Preserve their eventual parent statuses after this bounded slice.
- Published the approved three-commit history through required review PR [#40](https://github.com/Hexalith/Hexalith.Platform/pull/40), at review head `9a829a40718311da3eeb20a9f4e9ba9bc2eea41b`. Root main remains unchanged. Main ruleset 24657106 requires one approving review and code-owner review; no review is present, so default-branch integration and the single authorized dispatch remain pending. No bypass was used.
- A second independent remote advance to `3ad2f29ae50858ba665499568623b557725b9d93` conflicted only in additive `.gitattributes` blocks. Assessment authorized preserving both verbatim through ordinary forward integration; all independent source/test bytes and protected experiment bytes are unchanged. PR is now mergeable, with review required. Automatic tuple CI also reports historical-lane failures in the retained snapshot; no CI code was changed.
- Parent verification checked all 52 evidence-file size/hash commitments (100,154 bytes), the original candidate digest, the final 82/82 source-release test receipt, null run/verifier identities and unchanged parent sprint values. Local status report, tracking and evidence remain uncommitted and outside the published review branch. No hosted packet exists; execution and artifact-assessment tasks remain incomplete. Keep this spec in-progress and resume after the external review/integration gate; do not advance to final review/completion or consume another execution approval.
- Resume after the user's "approve": GitHub now reports PR #40 merged externally at `2026-11-11T20:32:48Z`, merge commit and current default main `94001fad925fdf7fa03a90b507b8f8e19074f9e3`. The review API still returns an empty list; record the observed external merge without inventing review or bypass evidence and do not repeat the merge. The latest 50 repository runs contain no candidate workflow. Proceed with the already approved single dispatch after exact published-source checks. Root checkout now has unrelated modified submodules (Builds, EventStore, Folders, FrontComposer, McpCli, Memories, Parties); preserve them and use an isolated pinned source for verifier work. Do not stage, commit or repin those changes.

## Spec Change Log

## Review Triage Log

## Verification

- `npm run test:source-release`: 82/82 passed during planning, including 53 executable broker controls; no live services.
- Recheck exact candidate digest, clean pinned EventStore source and reviewed publication diff before remote operations.
- Execute all three independent verifier modes after artifact retrieval; preserve nonzero outcomes. Production authority remains FAIL.
- `git diff --check`; inspect links, retained hashes and unchanged parent statuses.
