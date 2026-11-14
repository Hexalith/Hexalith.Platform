# RabbitMQ Package A — prepared upstream pull request

Prepared November 14, 2026 for [dapr/components-contrib #4764](https://github.com/dapr/components-contrib/issues/4764). This directory holds a locally committed, signed-off PR branch with the reviewed [Package A patch](../package-a-local-2026-11-14/contribution.patch), its upstream check results and the proposed PR texts.

**Status: prepared, not submitted.** The texts below await the owner's explicit approval. No fork, push, pull request, comment, label or issue edit has been made. Platform and EventStore were not pushed. No broker, Dapr runtime or qualification ran.

## Subject

| Item | Value |
|---|---|
| Official base | [`440b15349565bb15f7a88636477864e5a9f8cc72`](https://github.com/dapr/components-contrib/commit/440b15349565bb15f7a88636477864e5a9f8cc72) (main, unchanged at preflight and at final verification) |
| Branch | `fix/rabbitmq-publish-errors-4764` |
| Head commit | `a8c884bc298d076a08dac03cf56805971c239cb8` (one commit, parent = base) |
| Head tree | `5db36a5e9a200719a0b98058899025794fc29bfc` |
| Reviewed patch | `contribution.patch`, SHA-256 `ad71546ef66169c02b6b61f926b313c48d5f251e1a52e99c4129a44059dfa3b7`, applied unchanged (tree `b56fc35c…` alone) |
| Lint delta | [lint-fix.patch](lint-fix.patch), SHA-256 `fbc29c4f645fb5f7d23e7144f2d261b3e158f0d3d9b2815063b341617231c32f` |
| Thin bundle | [package-a-pr.bundle](package-a-pr.bundle) (`<base>..fix/rabbitmq-publish-errors-4764`), SHA-256 `6847f3a74829e1ef4b3b566f8c1166bcbe7c1351ac01b7c2e874210157ed09da` |

The commit changes only the five `pubsub/rabbitmq` files of the reviewed patch. Its message is [commit-message.txt](commit-message.txt). It is signed off with the local git identity and ends with the Co-Authored-By attribution. [commit-identity.json](commit-identity.json) and [bundle.json](bundle.json) record the identities. [preflight.json](preflight.json) shows main equal to the reviewed base. It also shows #4766 still open at head `14bf2f783dff41ef2c927fe1aeb6999c113ffff3`.

## Lint delta: shown for approval

The first full round ([round-1-reviewed-patch/](round-1-reviewed-patch/)) ran every check on base plus the unchanged reviewed patch. All checks passed except golangci-lint v2.10.1 `--new-from-rev`. It reported 13 findings with the CI default limits and 17 with unlimited counts. A further `prealloc` finding appeared after the first fixes. [lint-fix.patch](lint-fix.patch) resolves them mechanically, with no behavior change:

- `rabbitmq.go`: `if err := ctx.Err()` → `if err = ctx.Err()` (govet shadow; the outer `err` is reassigned before any later read). `for attempt := 0; attempt < publishMaxRetries; attempt++` → `for attempt := range publishMaxRetries` (intrange; untyped constant 3, loop variable unmodified).
- `rabbitmq_publish_transport_test.go`: seven constant `fmt.Errorf` → `errors.New` (perfsprint; same text). Two appended byte literals become an equivalent `slices.Concat` (prealloc; same bytes). Two shadowing `:=` become `=`. Explained `//nolint:gosec` comments cover three bounded length conversions (G115) and the loopback test peer's `InsecureSkipVerify` (G402). That `tls.Config` is hoisted to a local so the suppression covers one line. This follows the existing upstream `//nolint:gosec // reason` test convention.
- `rabbitmq_publish_test.go`: one `if`/`else if` chain on `stage` becomes an equivalent `switch` (staticcheck QF1003).

No behavioral fix was needed. The committed tree therefore equals the official base plus `contribution.patch` plus `lint-fix.patch`. [subject-verification.json](subject-verification.json) shows the head-minus-reviewed tree diff is byte-identical to `lint-fix.patch`. Round-1 development logs (`lint-fix-dev*`) ran on a dirty working tree and are diagnostics only. Workspace attempt 1 (`workspace-attempt-1-*`) is also kept: a script defect in porcelain-path parsing halted it before any commit.

## Round-2 checks (authoritative)

Run in held `run-with-temp` jobs ([workspace.py](workspace.py), [checks.py](checks.py)). Go 1.26.10 was verified against the go.dev index SHA-256. golangci-lint 2.10.1 (built with go1.26.0) was verified against its release checksums file. [toolchain-identity.json](toolchain-identity.json) records both. Every receipt shows the subject head `a8c884bc…` and a clean tree before and after. [summarize-checks.py](summarize-checks.py) re-hashes every log into [test-results.json](test-results.json): **PASS**.

| Check | Result |
|---|---|
| `git diff --stat <base> HEAD` | only the five `pubsub/rabbitmq` files; 1698 insertions, 71 deletions |
| `gofmt -l` (five files), `git diff --check <base> HEAD`, `go vet ./pubsub/rabbitmq` | PASS, empty output |
| `golangci-lint run --new-from-rev=<base> ./pubsub/rabbitmq/...` (default and unlimited counts) | PASS, `0 issues.` |
| `go test -json -count=1 -timeout=120s ./pubsub/rabbitmq` | PASS: 61 top-level, 192 including subtests; no skips or failures |
| Focused `-race -count=10 -run '^(TestPublishPublic\|TestInit)'` | PASS: 220 top-level / 680 pass events, 68 unique names |
| Legacy `-race -count=10 -run '^TestMustReconnectOnAMQPError$'` | PASS: 10/10 |
| `go test -tags=integration_test -run '^$'` | PASS; compile only, no broker |
| `go mod tidy -diff` and `-compat=1.20 -diff` (root) | PASS; no diff |
| Supplementary: `go -C tests/certification mod tidy -diff` (and `-compat=1.20`) | PASS; no diff (mirrors upstream `make modtidy-all check-mod-diff` for the module importing `pubsub/rabbitmq`) |

Test counts equal the [reviewed local results](../package-a-local-2026-11-14/test-results.json). Logs over 1 MiB are stored as deterministic gzip, and receipts keep both hashes. Note: `.gitignore` excludes `*.log`, so committing this directory needs `git add -f`, as was done for the local package.

[verify-subject.py](verify-subject.py) took a fresh read-only official clone and verified the bundle against it. It fetched the branch and matched head, tree and sole parent. It rebuilt the tree from base, the reviewed patch and the lint delta, and compared the commit message bytes and sign-off. It also checked the PR texts: Claude Code attribution last, "Partially addresses #4764", no closing keyword, no @mention, #4766 by number only, exclusions and SDK limits present, and no private markers. Result: **PASS** ([verify-subject.out](verify-subject.out)). All jobs exited 0 and their disposable directories were removed ([workspace-lifecycle.json](workspace-lifecycle.json)).

## Texts for approval

| File | SHA-256 | Bytes |
|---|---|---|
| [pr-title.txt](pr-title.txt) | `abfdb386c22e168de2145d1ced5a5c11c284d9d7bb32db115480c8e0ab1426d8` | 70 |
| [pr-body.md](pr-body.md) | `18d7eb607cbdefc1b41199ae99e25b2daab1b010c76d95f877a2b26881d54f37` | 6485 |
| [commit-message.txt](commit-message.txt) | `3c6d4c1c3030c810dd829539dfeaaddef628b695ecb6dd64e4e0b53e90f1a34d` | 1362 |

Title and body have no trailing newline, so readback can compare exact bytes. The body follows the upstream template (Description, Issue reference, Checklist) with these sections:

- behavior summary and tests
- `amqp091-go` limits: `ErrCredentials` and close-ok ordering
- exclusions: broker integration/certification tests, Dapr runtime mapping, and Package B as #4765
- the #4766 relation with the rebase/split offer

It references #4765 by number as the public identifier for Package B; drop that reference at approval if it is unwanted.

## Remaining parent steps

1. Present the three texts, the lint delta and these results, then HALT for an explicit yes. A decline is recorded as "prepared, not submitted".
2. Immediately before forking, re-check that main is unchanged for `pubsub/rabbitmq` and #4766 is unmerged; otherwise HALT.
3. In a fresh `run-with-temp` official clone (`--depth=1 --branch=main`), run `git bundle verify package-a-pr.bundle`. Fetch `refs/heads/fix/rabbitmq-publish-errors-4764` from the bundle and confirm `a8c884bc298d076a08dac03cf56805971c239cb8`. Then run `gh repo fork dapr/components-contrib --clone=false` and push that exact ref to the fork. Run `gh pr create -R dapr/components-contrib --base main --head jpiquot:fix/rabbitmq-publish-errors-4764 --title "$(cat pr-title.txt)" --body-file pr-body.md`.
4. Read back `gh pr view <n> --json title,body,headRefOid`, then write `submission-receipt.json` with the fork, branch, head SHA, PR number/URL and readback hashes. Record any mismatch without editing; on partial failure keep the state and error, with no altered retry.
5. Update the intake document, the EventStore records and sprint-status.

## Unchanged dispositions

A submitted PR is not upstream acceptance or a delivery gate. The gate remains an official supported Dapr release embedding the required component fixes. EventStore 3.21 admission follows, then Builds alignment and a separate Platform full-matrix proposal. Package B and Kafka stay open. Both parent stories remain **in-progress**. Technical qualification stays **FAIL (20/24)** with all five historical guarantees **REJECTED/open**. Broker selection stays **null** and production eligibility **FAIL**.
