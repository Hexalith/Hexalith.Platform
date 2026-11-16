# Story 2.13 verification

[Hosted main acceptance](hosted-main-43771691347/README.md) records successful
`resolve-sources`, `tuple` and `tuple-verdict` jobs for run 43771691347, attempt 1,
at `755de998e91a11545826748615785f0e43833152`. Its retained observations reconcile
565 Composition executions across all 36 selected groups, with zero skips. This
satisfies AC3; it grants no separate image, runtime or release admission.

The latest [post-review supplement](post-review/README.md) records 19 affected C# cases and 14 current-source tests passing at `9b27493ed62183dd37c31894f2ac712da33bfed4`. The [independent audit](parent-post-review-audit.json) checks its XML, source/output identities and raw metadata. The [fresh parent controls](parent-post-review/summary.json) pass 174 standalone Python, 83 source-release Node and 44 extension-release checks, zero skips. Earlier full-run observations below retain their actual subjects; the complete 565-case inventory was not rerun on the post-review binary.

An [updated isolated merge](upstream-merge/summary.json) against observed upstream `5b44f2d058f9221d8b54782c377e09fec5786b65` keeps the retired descriptors deleted, preserves incoming release tooling/lockfile/assertions and passes all 83 source-release checks after fresh dependency installation. All 85 production/review source hashes, current pins and 17 gitlinks match. The original 152 tags remain unchanged; background fetches added public `v2.1.6`, which is preserved. Completion subsequently rebased the feature branch onto that main with the verified resolutions; [final code verification](final-code-verification.json) proves code and documentation match the tested merge. [Merged code patch](upstream-merge/patch-receipt.json) and diagnostic archives retain their exact source/observation identities.

Historical pre-landing observation: local implementation checks passed while
hosted main resolution, current execution and verdict were still outstanding.
The local observations below retain their original subjects and grant no
candidate, image or native acceptance.

Start with [verification-summary.json](verification-summary.json), [coverage-map.md](coverage-map.md) and [selector-count-mapping.tsv](selector-count-mapping.tsv). The continuing inventories reconcile **463 current42 + 93 base42 + 9 extension executions**, with zero skips. Standalone Python passed 173 executions, source-release Node 83, extension-release Node 14/Python 30, collection graph 1 and source-SDK propagation 28, all with zero skips. The solution package build produced 31 project outputs with zero warnings/errors.

[continuing-group-observations.tsv](continuing-group-observations.tsv) retains each group's actual revision and test assembly hash. The full strict run completed all groups at `2901a6d3dba223b190f19b4725f2ed3ba68618ab`; its single transport fixture failure was corrected and passed at `d4de27e9f227cc3f386b07eed9a869ce76bf9441`. Three strengthened guard groups passed all 108 affected cases at code snapshot `3b42a4daa394592d7ef6f65a6024506c5886300d`; 457 unchanged-control observations are explicitly reused. The final delivery snapshot `0552304d8fb2975da65de15ed56c17fea2ed78d1` adds only CRLF whitespace policy in `.gitattributes`; runtime source and output bytes remain those recorded for the verified code snapshot.

[canonical-revisions.json](canonical-revisions.json) binds source, producer/owner pins and actual output hashes. Authentic revision metadata changed DLL hashes between test-only snapshots; complete production source and all 2,260 method signatures/IL/exception regions with referenced metadata compare equal, and all five SDK/Tenants dependency hashes match. No revision metadata override was used.

[before-inventory.json](before-inventory.json), [after-inventory.json](after-inventory.json) and [isolated-merge-summary.json](isolated-merge-summary.json) preserve pins/gitlinks, 105 archived qualification tags and both broker tags. Local qualification branches were absent. The isolated merge matches delivery tree `b66bf4e7f839b9b596c9729f74bfd2e3c38d6816`; no remote mutation or `/pushall` occurred.

Bulk XML, stdout/stderr, assets, full receipts, source patches and earlier failure diagnostics are preserved in `verification-artifacts.tar.gz`. [archive-index.tsv](archive-index.tsv) gives every archived path, size and SHA-256; [archive-receipt.json](archive-receipt.json) binds the archive and index. Every archived file hash was verified before removing its expanded copy. Raw paths referenced by summaries refer to this archive and can be restored in this directory with:

```sh
tar -xzf verification-artifacts.tar.gz
```

Earlier failed runs remain diagnostic observations. The published-artifact receipt records artifact identity from its original dirty source observation; clean source/runtime observations are bound separately. The shared McpCli2.0.0 cache mismatch and independently signed official body are preserved without changing the allowlist. Disposable checkouts, feeds and build outputs used `run-with-temp`; their temporary paths are observation subjects and are removed after child work completes.

Parent staged audit: [parent-verification-audit.json](parent-verification-audit.json) independently checks archive integrity, final XML counts, runtime/test source hashes, matrix execution logs and live pin/tag preservation. After the recorded delivery snapshot, staging added the same CRLF whitespace rule for the new reader helper and filled TSV total-row disposition labels; these policy/evidence-only edits change no verified C# bytes.
