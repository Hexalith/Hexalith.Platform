# RabbitMQ Package A — submitted upstream pull request

Prepared November 14, 2026 for [dapr/components-contrib #4764](https://github.com/dapr/components-contrib/issues/4764).

**Status: submitted, exact readback verified.** The owner approved the presented artifacts with “do”. [PR #4767](https://github.com/dapr/components-contrib/pull/4767) is OPEN from the verified `jpiquot/components-contrib` fork at the exact tested commit below. [submission-receipt.json](submission-receipt.json) retains the fork/push/create stages and exact title/body/head/tree/parent verification. No existing issue or PR was commented on, labeled or edited. Platform and EventStore were not pushed; no broker, Dapr runtime or qualification ran.

## Subject

| Item | Value |
|---|---|
| Official base | `440b15349565bb15f7a88636477864e5a9f8cc72` (main unchanged at the recorded checks) |
| Branch | `fix/rabbitmq-publish-errors-4764` |
| Head | `96c3259b7c12a476edd208848153d3cf72a8c4f0` (one commit, sole parent = base) |
| Tree | `055ab8a8a908eacb09828847232c94ed1c74b07f` |
| Reviewed contribution | [contribution.patch](../package-a-local-2026-11-14/contribution.patch), SHA-256 `ad71546ef66169c02b6b61f926b313c48d5f251e1a52e99c4129a44059dfa3b7`, applied unchanged |
| Current delta | [pr-delta.patch](pr-delta.patch), SHA-256 `bcc083c9e55c66d1e1994674443b1ad5cbfb09bbe2b68eae31e5f80c8c7c92ab`, relative to base plus contribution.patch |
| Thin bundle | [package-a-pr.bundle](package-a-pr.bundle), SHA-256 `2b76da6c491da467e3f1ac714cbb9de2f8e0d6e773e0ffe3f0dfa3efaddb9c51` |

[commit-identity.json](commit-identity.json) records the author, local git identity sign-off, final Co-Authored-By line, clean tree and exact five changed `pubsub/rabbitmq` files. [bundle.json](bundle.json) records its head, tree and base prerequisite. [source-identity.json](source-identity.json) records the read-only official clone. [submission-identity.json](submission-identity.json) records the preparation-time observation of `gh` authenticated as `jpiquot` with `repo` scope and no existing `jpiquot/components-contrib` fork.

## Approved source delta

The reviewed contribution is unchanged. [pr-delta.patch](pr-delta.patch) contains the retained [lint-fix.patch](lint-fix.patch) plus [logging-restoration.patch](logging-restoration.patch):

- Each failed publish or reconnect attempt warns with the official base's `publishing attempt (%d/%d) failed` message, including the attempt number and original error.
- Each reconnect wait warns with the official base's `publisher is reconnecting in %s ...` message.
- Retry exhaustion logs the official base's `publishing failed` message with the same error returned to the caller.
- `TestPublishPublicFailureLogging` captures `dapr/kit` logger JSON using `SetOutput`. Three cases assert message text, levels and counts for retry exhaustion, reconnect exhaustion and reconnect-failure exhaustion, while asserting the returned error identity.

Returned errors and control flow are unchanged. `publishSync`'s confirmation-phase transport abort remains as reviewed. Relative to the prior linted subject, production edits are limited to the Publish logging calls and passing the attempt number to its existing error-recording closure. The retained lint delta is byte-identical to the previous round: shadow assignments, the equivalent integer-range loop, constant errors, equivalent byte concatenation and switch syntax, and explained bounded-conversion/test-TLS lint suppressions. No new behavioral fix was needed for lint.

The body explicitly discloses that cancellation during exchange declaration, write or confirmation closes the shared transport, so subscriptions reconnect and unacknowledged deliveries are redelivered. It explains why the capacity-1 confirmation listener requires the abort and offers a dedicated publisher connection or a drained listener as a follow-up. This is an implementation consequence, not real-broker recovery evidence; those tests remain unexecuted.

## Current checks

[test-results.json](test-results.json) is **PASS**, with all 14 required receipts. Every receipt binds the final head above and a clean tree before and after. [toolchain-identity.json](toolchain-identity.json) records Go 1.26.10 verified against the go.dev archive SHA-256 and golangci-lint v2.10.1 verified against its release checksums.

| Check | Result |
|---|---|
| `git diff --stat <base> HEAD` | only five `pubsub/rabbitmq` files; 1780 insertions, 71 deletions |
| `gofmt -s -l` on all five files; `git diff --check`; `go vet ./pubsub/rabbitmq` | PASS, empty output |
| `golangci-lint run --new-from-rev=<base> ./pubsub/rabbitmq/...`, default and unlimited counts | PASS, `0 issues.`; CI build tags `certtests`, `conftests`, `metadata` from .golangci.yml |
| Full unit suite | PASS: 62 top-level / 196 test and subtest pass events, no skips or failures (baseline 61/192) |
| Focused public Publish and Init, `-race -count=10` | PASS: 230 top-level / 720 pass events, 72 unique names, no skips or failures (baseline 220/680) |
| Legacy reconnect fixture, `-race -count=10` | PASS: 10/10, no skips or failures |
| Integration tag with `-run '^$'` | PASS; compile only, no broker tests executed |
| Root `go mod tidy -diff` | PASS, no diff |
| Supplementary root `-compat=1.20 -diff`, certification module tidy and compatibility tidy | PASS, no diff; all are mandatory receipts in this prepared subject |

[summarize-checks.py](summarize-checks.py) checks log hashes, clean subject identity, all 14 receipt names, exact argv from [required-checks.jsonl](required-checks.jsonl), and the recorded local baseline count floors. It returns FAIL and a nonzero exit if a receipt is absent, its command or flags differ, or a suite drops below its floor. [verify-procedure.py](verify-procedure.py) verified every missing-receipt case at both summary and bundle boundaries, mislabeled commands/race flags, undersized runs, all five independently specified guard paths in moved shallow Git clones, base retrieval, forbidden PR states, exact UTF-8 mismatches and durable command/parse failures using disposable fixtures and mocked gh responses: **PASS**, recorded in [procedure-verification.json](procedure-verification.json).

[verify-subject.py](verify-subject.py) verified the bundle in a fresh disposable official clone, fetched its base by SHA, reconstructed base plus contribution.patch plus pr-delta.patch and matched head, tree, sole parent, message bytes, author sign-off and attribution: **PASS** in [subject-verification.json](subject-verification.json). It independently reconstructed the same tree from contribution.patch plus the retained lint-fix.patch plus logging-restoration.patch. PR text rules, exclusions, shared-transport disclosure, SDK limits, rebase/split offer and absence of private markers also passed. After the review fixes, the parent revalidated all 14 check receipts, passed all 89 procedure assertions, and reran this fresh-clone source and text verification successfully. The tested source commit and bundle remain unchanged. Parent outputs are retained in [parent-receipt-verification.out](parent-receipt-verification.out), [parent-procedure-verification.out](parent-procedure-verification.out), and [parent-subject-verification.out](parent-subject-verification.out). The previous raw verifier output is preserved in [history/pre-review-subject-verification.out](history/pre-review-subject-verification.out).

[preflight.py](preflight.py) only reads upstream. It HALTs unless main equals the reviewed base or its diff touches none of `pubsub/rabbitmq`, `common/component/rabbitmq`, `go.mod`, `go.sum` or `.golangci.yml`. It requires #4766 to be OPEN, unmerged and at `14bf2f783dff41ef2c927fe1aeb6999c113ffff3`. It always fetches the reviewed base SHA before using the bundle. The bundle gate recomputes the offline receipt summary in private disposable storage, preserving the original receipts, logs and saved summary; a stale saved PASS cannot bypass a missing receipt. The preparation observation in [submission-preflight.json](submission-preflight.json) is PASS; the parent must rerun it immediately before any submission after approval.

## Approved texts

| File | SHA-256 | Bytes |
|---|---|---|
| [pr-title.txt](pr-title.txt) | `abfdb386c22e168de2145d1ced5a5c11c284d9d7bb32db115480c8e0ab1426d8` | 70 |
| [pr-body.md](pr-body.md) | `5bfe6aa62faee98dfc501eea99553602f580d4a40470719721d93378c3b80581` | 7433 |
| [commit-message.txt](commit-message.txt) | `9ab7d05a5bc6c9fc8e214ac1b6437747c4b90c084361fd035c076ece8143fc55` | 1517 |

Title and body have no trailing newline. The body ends with the Claude Code attribution and uses `Partially addresses #4764`. It references #4766 by number only, credits its overlapping fix and offers to rebase or split. Its checklist says no metadata or configuration changes and points to the cancellation/subscription effects disclosed above. Its exclusions name integration, certification and conformance suite execution, Dapr runtime mapping, and mandatory publishing/correlated returns (#4765). Neither upstream acceptance nor a delivery gate is claimed.

## Reproduce local preparation

All disposable sources, tools, caches and build outputs live beneath `run-with-temp`'s private TMPDIR. Keep these evidence files outside it. In one terminal run `run-with-temp python3 -B workspace.py` from this directory and wait for WORKSPACE_READY. In another run `run-with-temp python3 -B checks.py < required-checks.jsonl`; EOF releases and removes the tool/cache job. Then run `python3 -B summarize-checks.py`, send `bundle` to the held workspace, run `run-with-temp python3 -B verify-subject.py` and `run-with-temp python3 -B verify-procedure.py`, and send `cleanup` to the workspace. Wait for every job to finish. A regenerated commit can have a different timestamp and SHA; it requires renewed text/source approval before submission. Do not overwrite approved evidence during submission.

Large logs are stored as deterministic gzip with uncompressed and stored hashes in their receipts. The root .gitignore excludes `.log` files, so the parent must explicitly include the useful logs with `git add -f` when recording this directory. [workspace-lifecycle.json](workspace-lifecycle.json) records completed jobs and removal of their temporary directories.

## Parent submission procedure — completed after explicit approval

1. Present the exact [title](pr-title.txt), [body](pr-body.md), [commit message](commit-message.txt), source delta and checks above; HALT for the owner's explicit yes. A decline remains **prepared, not submitted**.
2. In a new held `run-with-temp` job, clone official main read-only beneath TMPDIR. Before **each** remote write (fork, push and PR creation), run a fresh read-only `gh api user`, parse the response as JSON and require `login` to equal `jpiquot`; otherwise HALT. Retain the raw response and UTC observation time separately for each stage (for example `auth-before-fork.json`, `auth-before-push.json` and `auth-before-pr.json`). The preparation-time identity receipt is historical evidence and cannot satisfy these checks. Run `python3 -B <this-directory>/preflight.py --source <clone> --bundle` immediately before the first remote write. It fetches the base by SHA even if main has moved, then verifies the bundle and local result gates. Retain this result as `submission-preflight-before-write.json` before another run overwrites the working receipt. Any failed guard means HALT.
3. Fetch `refs/heads/fix/rabbitmq-publish-errors-4764:refs/heads/fix/rabbitmq-publish-errors-4764` from this directory's bundle into the clone. Confirm the ref equals `96c3259b7c12a476edd208848153d3cf72a8c4f0`, its tree equals `055ab8a8a908eacb09828847232c94ed1c74b07f`, its sole parent is the recorded base, and the commit message and approved text/delta hashes still match. Use the bundled commit without recreating it.
4. Run `gh repo fork dapr/components-contrib --clone=false`, then push only that exact ref to `https://github.com/jpiquot/components-contrib.git` with no force. Capture success or failure after each step in submission-receipt.json. Do not alter or retry a failed submission with changed content without approval.
5. After fork and push, rerun the **same** `preflight.py --source <clone> --bundle` guard immediately before PR creation, within `run-with-temp`, and retain its result as `submission-preflight-before-pr.json` alongside the earlier result. Recheck authenticated `jpiquot` for this write as described above. If the guard fails, preserve the successful fork/push and failure receipt, then HALT. Create the PR using the title decoded from the approved UTF-8 file and `--body-file` for the body. A structured Python call preserves the exact title and avoids shell interpolation:

```python
from pathlib import Path
import subprocess
out = Path('/absolute/path/to/this/evidence/directory')
result = subprocess.run([
    'gh', 'pr', 'create', '-R', 'dapr/components-contrib', '--base', 'main',
    '--head', 'jpiquot:fix/rabbitmq-publish-errors-4764',
    '--title', (out / 'pr-title.txt').read_bytes().decode('utf-8'),
    '--body-file', str(out / 'pr-body.md'),
], capture_output=True, text=True, check=True)
```

6. Run `python3 -B <this-directory>/readback.py <PR-number-or-URL>`. It calls `gh pr view --json title,body,headRefOid`, parses JSON and compares each string re-encoded as UTF-8 with the approved file bytes and the head SHA. It never uses `--jq`, whose appended newline would create a false mismatch. Preserve raw stdout, stderr and computed readback hashes. The helper records the attempted PR identity before executing gh and writes COMMAND_FAILED or INVALID_RESPONSE with the captured bytes if the command or JSON/schema parse fails. Record any mismatch without editing. If fork or push succeeded but PR creation failed, retain that partial state and error; no changed retry. Update submission-receipt.json with the fork, branch, head, PR number/URL and readback result.
7. Keep the held job until any child work finishes, then clean up. The parent records the result in the Platform intake, EventStore contract/deferred-work and sprint status, with local commits only; do not push Platform or EventStore. Do not comment on, label or edit #4764, #4765 or #4766.

## Superseded evidence and unchanged dispositions

[history/round-2-unchanged-patch/](history/round-2-unchanged-patch/) preserves the prior preparation and its round-1 evidence unchanged. It is superseded by the current head and root receipts. Round 1 did not have the two certification-module tidy checks that were added in round 2; its dirty-tree lint-development logs are diagnostics. [history/logging-development/](history/logging-development/) preserves this round's pre-seal diagnostics, not authoritative final-subject checks.

Both parent stories remain **in-progress**. Technical qualification stays **FAIL (20/24)**, all five historical guarantees **REJECTED/open**, broker selection **null**, production eligibility **FAIL**. The delivery gate remains an official supported Dapr release embedding the required fixes, followed by EventStore admission, Builds alignment and a separate Platform full-matrix proposal. Package B and the independent Kafka stop rules remain open.
