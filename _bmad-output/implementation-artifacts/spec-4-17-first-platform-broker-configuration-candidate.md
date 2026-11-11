---
title: 'Story 4.17 slice: first Platform broker configuration candidate'
type: 'chore'
created: '2026-11-11'
status: 'done'
route: 'oneshot'
story_key: '4-17-select-and-qualify-the-durable-production-broker'
review_loop_iteration: 0
baseline_commit: '6d422033b959c249c149a4cb3785704e9eebe03c'
context: []
---

<frozen-after-approval reason="human-owned intent — owner explicitly scoped this offline 4.17 candidate slice">

## Intent

**Problem:** Platform's 2026-11-10 dispositions left five durable-broker guarantees REJECTED. EventStore now provides a bounded schema-1 configuration input, but Platform has no concrete candidate or validated execution handoff.

**Approach:** Repin the EventStore gitlink to the pushed `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` implementation. Add one Platform-owned JSON containing only admitted settings, explain which rejected guarantee each value probes, validate it with EventStore's parser and producer admission without Docker, and record the exact command, result, SHA-256 and future invocation with `runtime-lock-1.19.1-platform-config-v1.json`.

These values are experiments, never proven remedies. Retain the dated dispositions without re-deriving them. Run no infrastructure, CI or live qualification; stop and ask before any live run. Do not change EventStore's harness, locks, bounds or nested submodules. Do not select a broker, grant exceptions, or close Platform 4.17 or EventStore 3.21. RabbitMQ pressure and missing binding remain REJECTED pending the separate upstream fixes (#4748 unreleased in the retained dispositions; mandatory/return handling).

</frozen-after-approval>

## Implementation Notes

- Planning facts: no unresolved intent gaps, no irreversible operations; five-file footprint including this spec. All checks use Python's standard library and temporary admission output. No new executable entry point or Platform runner capability.
- Add `docs/implementation/story-4-17-broker-candidate-01.json`: schema 1, declared Platform baseline `6d422033b959c249c149a4cb3785704e9eebe03c`, exact repository-relative path, RabbitMQ reconnect 2 seconds/outbound timeout 500 ms, Kafka consume retry 200 ms/initial rebalance delay 100 ms. Both Kafka settings probe consumer timing for pressure and, secondarily, rotation diagnostics; neither changes publisher identity or ACL enforcement.
- Add `docs/implementation/story-4-17-broker-candidate-01.md`: hypotheses, provenance limitations (the JSON is newly added after its declared baseline), exact offline validation and results, pushed-revision evidence, and future producer/verifier commands requiring separate live authorization, supported allocated worker and verified tools. Capture integrity, technical guarantees and production authority remain separate.
- Reuse `load_configuration`, `provenance`, `validate_profile` and the producer's `Run` constructor from the pinned EventStore implementation for offline admission only; never call its infrastructure preparation, main or finish. Remove temporary admission/private files. Record schema-3 admission as a check, never a qualification packet.
- Update `.gitattributes` to preserve the candidate JSON bytes across checkouts, following existing hash-bound input conventions. Repin only `references/Hexalith.EventStore`; keep its working tree clean and do not initialize nested references.
- Context: valid cached `epic-4-context.md`; Story 4.15's completed offline slice preserves the prepared-versus-live boundary. Existing 4.17 disposition spec is complete only for its dated slice; parent sprint key remains `in-progress`. EventStore README, schema-1 parser and completed input-enabling spec define the exact input and execution handoff. The full 24-vector denominator, five rejected guarantees, null broker selection and FAIL production eligibility remain.
- Implemented the five-file footprint. EventStore's gitlink is the exact pushed input-enabling commit; no EventStore source, lock or nested gitlink was edited. The candidate's SHA-256 is `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc`.
- Offline validation (exact command and stdout in the handoff): exit 0; real schema-1 parser and producer admission passed against 91 pinned inputs; schema-3 metadata and exact configuration/provenance checked; missing-configuration and historical-profile pairings rejected before output. No subprocess/socket calls; all temporary files removed. No live method invoked.
- Documentation checks: four Bash blocks pass `bash -n`; all local links resolve; new-file whitespace is clean. Candidate JSON has `text: unset`, preserving its measured bytes. `git diff --check` passes.
- Commit-message preflight: pinned `@commitlint/cli@21.2.4`, `node_modules/.bin/commitlint --edit /tmp/platform-4-17-commit-e4h41nzu.txt --verbose`, exit 0, zero problems/warnings for the exact local commit message. No push is authorized or performed.
- Parent status handling: the user's explicit instruction not to close 4.17 overrides the workflow's final parent advancement to review. The parent stays `in-progress`; only this bounded candidate spec may become `done`. EventStore 3.21 remains `in-progress` in its untouched source.
- Review corrections: assert the expected candidate hash before admission and future execution; force Python assertions on; guard setup and verify the future EventStore revision/clean tree; capture producer and verifier statuses independently. All five review findings were patched with no deferred work.
- Final validation: extracted and executed the exact documented offline command under inherited `PYTHONOPTIMIZE=2`; exit 0, stdout byte-for-byte equal to the recorded result. The final three Bash blocks pass syntax checks, all local links resolve, and whitespace checks pass. The live block was parsed only, never executed. The configuration SHA-256 is unchanged.

## Review Triage Log

| Finding | Verdict | Evidence and resolution |
| --- | --- | --- |
| Setup failures could fall through to execution | medium | The original commands lacked fail-fast setup. The final live handoff is a `set -eu` subshell; failed directory/revision/clean-tree/hash/fresh-path checks or installation stop it before producer execution. Patched. |
| Inherited errexit could skip later evaluations | medium | A nonzero producer/technical verifier could end the original command sequence. Each evaluation now captures its own status through `||`, all four codes are printed, and the final status preserves failure. Patched. |
| Another schema-valid file could pass without matching candidate 01 | medium | The original check printed the supplied digest without comparing it to the recorded candidate. The final offline command raises on an expected-hash mismatch; the future handoff runs `sha256sum --check` before setup. Patched. |
| Future execution could use a different trusted checkout | medium | Profile validation trusts its owning checkout rather than the historical documentation. The future handoff now requires exact EventStore revision `02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` and a clean working tree. Patched. |
| PYTHONOPTIMIZE could disable validation assertions | medium | The original `python3 -B` honored inherited optimization. The final command sets `PYTHONOPTIMIZE=0` and checks `__debug__`; it passed with inherited optimization set to 2. Patched. |
