---
title: 'EXT-SECRETS-1 authority administrator channel hardening (R23B-6)'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Address R23B-6 only. Authenticate and authorize administrator requests before acquiring their private channel token. Bound credential file bytes, total acquisition time and outstanding actual I/O, including after caller timeout or cancellation. Preserve query authentication, administrator permissions, the whole-directory projected Secret mount (including symlinks), and owner-managed replacement/rotation; reacquire the configured locator on each authorized request. Invalid mounts and saturated acquisition fail closed. Test unauthenticated and unauthorized requests, invalid and stalled mounts, cancellation, saturation, rotation and successful authenticated administration with actual JWT validation. Never expose credential values in diagnostics or evidence.

Preserve all sealed source23 evidence, existing baseline failures and unrelated work. Do not install, run Live/Full/cohort qualification or replay maintenance, edit accepted dependency records, stage, commit or push.

</frozen-after-approval>

## Implementation Notes

- Investigation: no intent gaps or irreversible operations; small footprint confined to authority HTTP, an internal channel reader, focused tests/fixture and local documentation. No sprint-status changes: this is an isolated deferred security correction, not completion of story 5.4.
- `AuthorityHttp.cs`: retain query middleware behavior and endpoint authorization policies; perform administrator-channel verification only inside the authorized administrator route group.
- New internal reader: 8192-byte maximum, a two-second acquisition budget, four immediately admitted workers and no queued work. Actual workers own slots through disposal/completion even after timeout or request cancellation. Linux nonblocking open follows projected Secret symlinks; descriptor metadata rejects nonregular and oversized mounts. Token buffers are transient and cleared; no file credential strings or diagnostics.
- `AuthorityHttpFixture.cs` and focused test file: retain real Kestrel and RS256 JWTs; use internal reader injection to deterministically stall opening/reading and prove cancellation and lingering-work limits. Real file/FIFO/symlink tests exercise production acquisition.
- Contract sources: `eng/ext-secrets-1/install_hmac_authority.py` whole-directory read-only Secret mount; `eng/ext-secrets-1/README.md` isolated authority and credential replacement sections. Strict no-follow runtime readers are incompatible with this mount and are not changed.
- Verification: baseline authority suite before edits; rebuild changed authority/test projects with existing dependency binaries if historical dependency sources prevent a fresh graph build; run complete authority suite and focused adversarial tests. Retain failed attempts separately and carry forward Runtime843/847 and Python466/467 historical failures without relabeling them.
- Production and final test builds passed with zero warnings/errors, using `--no-restore -p:BuildProjectReferences=false` and existing dependency binaries. This is not a fresh dependency-graph source rebuild.
- Starting retained Authority binary: 67/67; rebuilt current suite: 88/88, including all 67 existing cases. Seven JWT/permission refusal cases exercise both administrator endpoints with an absent real file and an acquisition counter; seven real invalid-mount cases include FIFO and device; remaining cases cover exact byte boundary, projected-link rotation, open/read stalls, no queuing after timeout, cancellation, no audit effect and recovery. Saturated acquisition leaves an authenticated query successful.
- HTTP cancellation coverage injects linked `RequestAborted` deterministically through the fixture; it does not claim native socket-disconnect timing. Direct cancellation tests retain all actual stalled worker slots until release. Worker faults are observed and owned byte/character buffers are cleared; no file credential strings are created.
- Local verification report and original diagnostic attempts retained at `/tmp/ext-secrets-admin-channel-_vb9r02d/verification.json`; final accepted test log is `authority-tests-4.log`. Earlier compile/test harness failures and an excluded stale-binary test run remain separately labeled.
- Preservation check: all 204 original files under the handoff evidence directory unchanged; sealed source23 and review-fix SHA256 values match the handoff; index unchanged. Concurrent story 2.5/deferred-ledger edits belong to another session and were not modified here. The historical Runtime843/847 (four baseline failures) and Python466/467 (one baseline failure) remain failed aggregates and were not rerun or relabeled. Both external dependencies remain unavailable.

## Review Triage Log

- R1, medium, patch: `Task.Run` places potentially indefinite native calls on shared pool workers. Four retained stalls can delay timer continuations and queries on a constrained pool. Replace admission-owned work with bounded dedicated threads and verify the stalled cases in a constrained child process.
- R2, medium, patch: whitespace/BOM-only files normalize to an empty token but currently return 401. Treat normalized empty credentials like other unusable mounts (503); add real-mount cases.
- R3, medium, patch: timeout tests bound responses only after waiting for worker entry and do not distinguish immediate refusal from waiting one acquisition timeout. Measure one total test interval and assert saturated reader tasks complete synchronously without admitting work.
- R4, medium, patch: no stalled-disposal case protects slot retention through stream disposal. Extend the controlled mount to block disposal and exercise timeout, saturation and recovery.
- R5, medium, patch: reused operation IDs can make a late erroneous mutation look like an idempotent recovery; stream-disposal completion precedes endpoint completion. Use unique refused IDs, a fixture server completion barrier and post-completion audit checks.
- R6, medium, patch: response-only assertions and disabled logging do not protect against late fault diagnostics. Capture fixture logs, inject synthetic late-failure sentinels and verify responses/logs omit them. No real credential values enter test diagnostics or evidence.

- Review patch verification supersedes the earlier 88-case result: complete Authority suite **93/93**, including all 67 original cases and 26 new cases. Final logs: `authority-tests-5.log`, `build-authority-2.log`, `build-tests-9.log`. Review-edit compilation attempts 7/8 remain retained as failed diagnostics; final builds have zero warnings/errors.
- Dedicated, admission-owned LongRunning tasks isolate filesystem stalls from shared HTTP/deadline workers. A separate child process forced and asserted a maximum of two shared pool workers: all three open/read/disposal stall cases passed, with live authenticated fixture queries and recovery. `constrained-pool-tests-1.log` retains the runner option deprecation notice and nonzero selection (3/3).
- Review patches add normalized-empty mount refusal (whitespace/BOM-only), a single elapsed timeout interval with stated scheduling tolerance, synchronously completed saturation refusal, unique original operation IDs, server request-completion barriers, post-completion audit absence, captured trace logs and synthetic late faults following both timeout and request cancellation. Sentinels remain in memory only; boolean assertions never print them.

- Final review follow-up confirmed R1–R5 and then R6 after the observation barrier adjustment. All six findings are resolved; none deferred. The late-fault test now waits for the actual fault-observation continuation, rather than a stream/open callback.
- Final accepted verification after that last adjustment: Authority **93/93**, constrained two-shared-worker process **3/3**; `authority-tests-6.log`, `constrained-pool-tests-2.log`, `build-authority-3.log`, `build-tests-10.log`. Complete review/build/preservation summary with source, dependency-binary and original-log hashes: `docs/implementation/ext-secrets-1-authority-admin-channel-hardening-2026-10-28.json`.
- External concurrency exception: after verification, another session staged this scope with story 2.5. This task attempted to unstage only its seven paths, but an active `.git/index.lock` prevented any change. The external session created `4e35f561b022caede7093f0c3f4301e3dcbd1e11` (`fix: story 2.5`) including the R23B-6 files. Its committed source matches the final tested bytes. No staging, commit or push command was run by this task. The external history and unrelated edits were preserved; final spec/evidence updates remain local.
- This spec is done for R23B-6 only. Broad story 5.4 and accepted dependency records retain their status and blockers; the historical deferred finding remains preserved with its contemporaneous evidence. No installation, qualification, consumer activation or maintenance occurred. All 204 original handoff evidence files are unchanged. Prior statements that HEAD/index were unchanged describe checks before the external session's Git actions; the final report explicitly records the exception.
