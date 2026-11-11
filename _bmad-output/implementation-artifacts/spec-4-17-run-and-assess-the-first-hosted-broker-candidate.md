---
title: 'Story 4.17 slice: run and assess the first hosted broker candidate'
type: 'chore'
created: '2026-11-11'
status: 'done'
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
- [x] Existing candidate files above — publish only approved history, validate authored Git/PR messages with pinned commitlint and verify remote gitlink availability. Record actual source/run binding; never infer publication from local refs.
- [x] `.github/workflows/broker-candidate-qualification.yml` — dispatch once at the approved published source; monitor terminal outcome and download its named artifact into a fresh destination before expiry. Record failure without unauthorized reruns.
- [x] `_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/` — retain sanitized run/artifact metadata, exact downloaded bytes/digests, verifier argv/exits and per-vector assessment. Keep downloaded artifacts outside existing source inventory; inspect archive paths before extraction and use the independently supplied original for verification. No raw credential/private-transcript publication.
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
- During preflight, independently published main `488723fd7efbccbab3bccdae3be4ad79b05a4994` repinned EventStore. Assessment authorized the reversible source branch `chore/broker-candidate-01-hosted-94001fad` at the already reviewed/published merge, preserving newer main. The one accepted attempt is [43097161643](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43097161643), attempt 1, actual head `94001fad925fdf7fa03a90b507b8f8e19074f9e3`.
- The bounded attempt finished as `startup_failure` before any job allocation. GitHub's diagnostic reports an unexpected error and request `4ed78a69-de10-11f1-8d56-5705d8f2f5ff`; root cause is unconfirmed. Zero jobs/check runs/artifacts and 0/24 observed vectors are retained. No capture or verifier ran, all actual argv/exits remain null, and no cleanup or delivery proof exists. Completed task checkboxes denote recording this expected failure branch, not obtaining a packet or completed qualification assessment. Packet download/verification is unavailable, so none was fabricated or attempted against missing evidence. No retry occurred.
- Parent independently verified all 78 manifest commitments (148,241 bytes), source-tree equality to the 82/82 tested reviewed source, unchanged candidate bytes and all missing-evidence distinctions. Parent stories stay in-progress, selection null and production eligibility FAIL. The consumed single-attempt slice can now undergo review; any later execution requires a new owner decision.

- Review scope follows this slice's owned report, tracking, context and evidence paths. Independently committed Keycloak/current-owner code and newer submodule gitlinks arrived after the preserved baseline; they are excluded from this slice's review/commit and remain untouched. The complete baseline diff is retained separately at `/tmp/platform-4-17-hosted-review-143nz1qk.diff`; the scoped review diff contains every owned tracked and untracked change.

## Spec Change Log

## Review Triage Log

All three independent layers completed before triage. Verification-gap returned no findings. Every finding below was checked against the retained bytes and source diff; no result authorizes another run or changes the observed startup failure.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| BH1 archived manifest lost its base | medium | 47 original paths are absent under the relocated directory; archive-first/parent fallback verifies the original commitments. Patch: document and provide repeatable verification of that preserved base without changing original evidence bytes. |
| BH2 archived report links broken | medium | The copied report retains eight links relative to docs/implementation. Patch navigation to the correct historical snapshots and shared immutable receipts. |
| BH3 empty inventories lack query identity | medium | Jobs, check-runs and pending-deployment JSON contain no repository/run/suite endpoint or successful-query receipt. Patch: bind each negative observation to its actual successful query. |
| BH4 current-named PR snapshot is stale | medium | Top-level pull-request.json still says OPEN/REVIEW_REQUIRED while external publication is observed merged. Patch: label historical snapshot and superseding receipt or retain a fresh current observation, preserving the prior bytes in the archive. |
| BH5 cached suite acceptance constraints omitted | medium | Generated cache omits explicit empty-suite/unmapped-flow refusal and missing-recovery-hook unqualification. Defer with the other generated agent-context omissions; no experiment code or current result depends on the omitted staging gates. |
| BH6 cached Kubernetes prerequisites omitted | medium | Current cache compresses named signed off-node restore proofs and fresh etcd prerequisite into unnamed per-hop gates. Defer generated agent-context correction. |
| BH7 cached exposure proof/OIDC constraints omitted | medium | Current cache omits external closure probes and preserving public OIDC. Defer generated agent-context correction. |
| BH8 cached ownership enforcement omitted | medium | Tier descriptions omit exactly-one-tier, denial of higher-tier application writes and environment union-of-records application. Defer generated agent-context correction. |
| BH9 cached executor boundaries omitted | medium | Cache omits takeover fencing and synthetic-token/verification-endpoint-only sandbox access. Defer generated agent-context correction. |
| BH10 cached staging identity boundaries omitted | medium | Cache omits surface-class clients, synthetic admin/cross-tenant exclusions and staging denial of Keycloak backup/restore. Defer generated agent-context correction. |
| EC1 archived report links broken | medium | Same verified relocation defect as BH2. Patch together while retaining all historical versus current distinctions. |

The cache findings share a lossy context-compilation cause and follow the workflow's explicit deferral route for agent-context edits. The generated cache was independently committed during this run and remains outside the current report-only corrections. Existing broker inputs, guards and full denominator remain unchanged.

Review completion: patched all five evidence/navigation findings (BH1–BH4 and EC1) with the implementation agent. Preserved the original historical manifest, documented its archive-first base, repaired all eight historical links, bound empty inventories to successful HTTP 200 GETs, and labeled the superseded PR snapshot. Parent verification also made the documented archive verifier independent of inherited Python optimization. Final verification passed all 79 current commitments (151,443 bytes), all 52 original archived commitments, 26 local links, query identities/statuses, the single-attempt/null-result invariants and whitespace. The new archival verifier passed with inherited PYTHONOPTIMIZE=2. Existing 82/82 controls apply to the byte-identical execution source; no source code changed or broad tests were repeated. Six cache findings were grouped into one explicit deferred agent-context correction.

Completion is scoped to the single attempt and its truthful infrastructure-failure report. Parent sprint synchronization to review is intentionally skipped because the approved frozen constraints preserve Platform 4.17 and EventStore 3.21 in-progress. Packet/verifier, cleanup and artifact-delivery assessment remain incomplete, all five historical rejections remain, and no additional attempt is authorized. No report push is performed by completion.

Controller commit-message preflight: pinned @commitlint/cli 21.2.4 accepted the exact full message in `/tmp/platform-4-17-hosted-commit-1eomegs3.txt`, exit 0, zero problems/warnings. Commit scope includes only reviewed report, evidence, spec and tracking paths; independently committed code and gitlinks are excluded.

## Verification

- `npm run test:source-release`: 82/82 passed during planning, including 53 executable broker controls; no live services.
- Recheck exact candidate digest, clean pinned EventStore source and reviewed publication diff before remote operations.
- Execute all three independent verifier modes after artifact retrieval; preserve nonzero outcomes. Production authority remains FAIL.
- `git diff --check`; inspect links, retained hashes and unchanged parent statuses.
