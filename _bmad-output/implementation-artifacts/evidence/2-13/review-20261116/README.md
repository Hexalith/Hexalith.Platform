# 2026-11-16 code review patch verification

The second Story 2.13 code review covered the post-review delta `f55a641f..dffe523c`. It produced 4 decisions and
7 patches, and the user adopted every recommendation. This directory holds the local verification of all 11
resulting patches. [summary.json](summary.json) has the machine-readable totals.

The candidate was based on source `ead9ff0d`, whose tree is identical to `73fef1d0`, the last good tree before
companion merges reverted pins. It was verified as private candidate `8ebca209…` in a disposable copy. Its exact
patch is retained as [package/candidate.patch](package/candidate.patch).

| Control | Result | Evidence |
| --- | --- | --- |
| Composition, fresh signed-package build, no Dapr CLI on PATH | 38 groups, **588 executions** (current42 486, base42 93, extensions 9), zero skips | [package/composition-results.json](package/composition-results.json) |
| Changed groups (run first) | transport 1; listener group 5; restored cohort refusals 20; Platform-owned mapping 2 | same |
| Standalone tuple/source/publication Python | 170 | [standalone/results.json](standalone/results.json) |
| Source-release Node (trigger, checkout and verdict controls, with setup-dapr removed) | 83 | same |
| Extension-release Node / Python | 14 / 30 | same |
| Builds 6.0.5 signature, tuple graph audit, collection graph, source-SDK propagation | all exit 0 | [package/package-commands.json](package/package-commands.json) |

The coverage disposition of the three classes deleted on 2026-11-15 is corrected in
[coverage-correction.md](coverage-correction.md). It supersedes the hash-bound rows of `../coverage-map.md` and
`../selector-count-mapping.tsv`; those files are left byte-identical.

These are local observations only. They give no hosted main, image, native runtime or candidate acceptance. The
story stays non-done until a hosted tuple run of this candidate passes `resolve-sources`, `tuple` and
`tuple-verdict` with no Dapr CLI installed on the runner.
