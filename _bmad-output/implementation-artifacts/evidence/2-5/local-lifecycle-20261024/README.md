# Story 2.5 local lifecycle verification

The parent verified **534 Builds tests passed** and **one real-service test passed**
with no failures, errors, skips or unexecuted tests. Both individual Debug builds
passed without warnings or errors. The live test exercised success, test failure,
execution timeout and required-startup-task failure in 278.032 seconds.

[Commands](commands.json), [verification](verification.json),
[parent acceptance audit](parent-acceptance-audit.json) and
[parsed artifact audit](parsed-audit.json) retain the observations. The
[matrix audit](matrix-audit.json) names executed passing tests for all seven rows.
[Builds XML](builds-tests.xml) and [live XML](platform-live-tests.xml) preserve the
actual test results.

Every live scenario validates its v1 result before and after cleanup, preserves
the first outcome/cause/time, verifies the independent peer's Redis sentinel and
records cleanup retries. [Final live cleanup](live/cleanup-audit.json) is empty.
The [parent resource audit](parent-owned-resource-audit.json) checks exact owned
container labels and process environment tags across every retained attempt.
The [source identities](source-content-identities.json) are unchanged from the
snapshot taken before the final live run. Concurrent recovery work is recorded
separately in the [diff audit](parent-diff-audit.json).

The [review dispositions](workflow-review.json) and `review-corrections/` preserve the correction history. The final result retains the original terminal/binding data; cleanup and retention observation timestamps may advance on idempotent calls.

The retained earlier attempts are historical evidence:

- `live-attempt-1-cli-mismatch/`: Aspire 13.6.1 refused before allocation; the
  current catalog requires 13.6.3. Final verification used a temporary 13.6.3 CLI.
- `live-attempt-2-cleanup-retry/`: resources were removed, but the initial proof
  incorrectly required every cleanup attempt to pass. The final proof retains
  first failures and verifies bounded retries and a further idempotent cleanup.
- `live-attempt-3-startup-cause/`: resource behavior passed, but the offline audit
  rejected a task failure misclassified as startup timeout under shared `HXR021`.
  [The rejection](pre-cause-audit.json) and its log are preserved. The corrected
  producer records the runner-observed cause, and both producer and live tests
  assert the distinction.

Package lifecycle state coverage passes. Full package runtime qualification remains
unavailable because authoritative release declaration hashes are unpopulated.
These observations grant no candidate, CI or publication acceptance. Workflow
review is complete: all 21 individual findings are corrected, with no deferrals.
