# Review-4 native client-certificate delivery

This is unsigned, offline, synthetic fixture evidence. The seven named suites
pass **331 tests, zero skips** (55 + 45 + 12 + 13 + 42 + 94 + 70). Counts remain
receipt data; suite identity and the exact pinned command mapping are required.
The packet establishes no credential, authority, operational or recovery grant.
Public activation stays disabled; 4.27 stays in-progress, 4.1 closed and Timesheets
3.6 open/FAIL.

Run from the repository root:

```bash
/usr/bin/python3.14 -I -S -B _bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-4/verify-receipt.py --self-test
/usr/bin/python3.14 -I -S -B _bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-4/run-cancellation-sweeps.py
```

`command-results.json` and `verification.json` bind each of the seven actual
commands, full stdout/stderr transcripts, totals, zero skips and source/snapshot
hashes. `run-seven-suites.py` contains the exact seven commands used to capture
those logs; rerunning it replaces this packet's suite logs, so use an isolated
copy when preserving this exact historical invocation. `runtime-environment.json`
records the pinned interpreter identity, Python/OpenSSL patch versions and
OS/release/architecture without hostnames. `preparation-head.txt` records the
full preparation HEAD separately from the original baseline in the receipt.

`run-cancellation-sweeps.py` and `cancellation-sweeps.json` bind current finite
synthetic controls: 297 single-line targets, 2,298 opcode targets and 79 secondary
line targets, with no resource leaks, swallowed cancellations or unraisable
exceptions. The five secondary terminal exchange-propagation targets are listed
separately and verify all descriptor/peer release before injection. Those new
external cancellations do not promise a signal-atomic terminal return. Direct
focused tests also cover two skipped cleanup-invocation handoffs, combined lost
raw-result/recovery cancellation, partial trusted construction after C-owned
acquisition, custody constructor entry/partial/return and caller-history scope.
The guarantee is the finite tested process-local protocol. Arbitrary hostile
fd-table mutation, unsupported/non-idempotent closers, infinite cancellation,
kernel stalls and actual live connector/runtime remain unqualified.

The checker verifies all bound hashes, complete seven-suite inventory/commands,
actual plain-OK unittest summaries, totals, prior packet bytes, handoff prefixes,
historical spec checkpoints and retained test names. The self-test uses isolated
copies only. It rejects coherent working/snapshot drift, count/skip/outcome
metadata drift, suite omission and command drift. It also runs actual unittest
negative controls with the same bound test count: FAILED, skipped, expected
failure and unexpected success. Their transcript bytes and all referring hashes
are changed coherently, keeping original success claims unchanged, so a parser
hardcoded to OK/zero skips cannot pass this self-test. These intentionally adverse
parser controls are not passing qualification suites.

`spec-dispatch-baseline.md` and `spec-verification-checkpoint.md` bind exact full
historical spec bytes. Later workflow status/triage changes are separate; frozen
human intent remains byte-exact across all checkpoints. All 107 prior packet
files are bound in `prior-packet-sha256.json`. Review-3 and earlier checkers,
receipts, logs and source snapshots remain unchanged. Review-1's old preservation
checker intentionally requires its preparation HEAD/index and is a historical
precommit check; the new receipt checker requires only bound content.

Files named `historical-bmad427-*` are the exact original adverse parent
scripts/results against review-3. They are archived with their adverse results,
not relabeled as passing and not operational helper replays. The historical
line-748 probe retains its original hardcoded selector; the current executable
sweep adapts structurally to current source. The original review-3 sources remain
at `../review-3/sources/`; current snapshots are under `sources/`.

Remaining prerequisites are actual credential acquisition/custody and
IAM/authority/client-identity qualification, live connector/TLS/runtime review and
activation, full store/system and isolated-target orchestration including
telemetry PostgreSQL, proof-consumer integration, fresh exact attempt/policy/tool
bindings, separately scoped operational approval and actual recovery/resume/
cleanup with signed/non-dry/manual acceptance. No actual credentials,
configuration, endpoints, certificate issuance, recovery allocation, signing,
upload, recovery, infrastructure mutation, staging, commit or push occurred.
Unrelated Story 2.3/2.4, deferred-ledger, sprint-workflow and submodule work remains
preserved and excluded. This receipt provides reproducibility, no external
authenticity or operational authority.
