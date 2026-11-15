# Focused post-review verification

The eight reviewed source/guide files are fixed. The original Story 2.13 archive and observations are preserved; this supplement records a separate source and output subject.

Tested candidate: `9b27493ed62183dd37c31894f2ac712da33bfed4`, tree `7bcf050c12f28d2cc8ae4ac8f01ce56009045c31`. [source-and-output-before.json](source-and-output-before.json) and [source-and-output-after.json](source-and-output-after.json) bind the actual file/DLL hashes and unchanged execution context. Production source bytes match all 77 files in the preserved full-run source receipt; authentic revision metadata changes the production DLL hash. No revision override was used.

[summary.json](summary.json) records **19/19 focused C# cases and 14/14 Python tests**, with zero skips. The single Composition test-project package build passed with zero warnings/errors. Broader verification was left to the parent workflow.

| Focused control | Passed | Observation |
| --- | ---: | --- |
| Fixture factories | 3 | Historical/current provenance and the separate generic/SDK42 factory paths |
| Entry theory | 11 | Actual Production `ComposeRun` refuses before configuration, after a measured Development baseline |
| Reader reuse | 1 | Deterministic identical-body reuse remains valid; both proofs reject the controlled changed-digest receipt |
| Catalog handoff | 2 | Independent pinned-catalog evaluation bounds consumer-cache changes; rematerialization still refuses |
| Signed handoff | 2 | Both genuine initial-key paths retain bounded cache derivation and immutable warm entries before image refusal |
| Current-source Python | 14 | Real `file://` depth-one Platform clone, inaccessible predecessor, independent owner clones and tag/blob checks |

All five C# XML files remain directly readable, alongside [results.json](results.json), the exact commands, source manifests and [review-fix.patch](review-fix.patch). [guide-checks.json](guide-checks.json) records the edited guide links, current three-job orchestration and 463/93/9 profiles. No selector totals or production/runtime sources changed.

The initial API-overload compile diagnostic and intermediate successful compile are retained separately; the passing focused run used the final snapshot above. Published Builds6.0.5 archive/signature authentication is summarized in [published-artifact-summary.json](published-artifact-summary.json). Its original raw metadata is stored losslessly as `published-artifact-identity.json.gz`; [raw-observations-index.tsv](raw-observations-index.tsv) and [raw-observations-receipt.json](raw-observations-receipt.json) bind both compressed and original bytes, verified equal after decompression.

The original archive SHA-256 remains `9a94144fba0085cde6f542c9407f7a306c1ac167c5a3de84ec1854e157667105`. Local checks confer no image/native acceptance. Hosted-main acceptance remains pending authorized landing and actual hosted execution.

After testing, the parent added an evidence-only `.gitattributes` rule for verbatim `post-review/*.patch` bytes. It changes no runtime source; the source and output identities above remain the actual tested subject.

Raw console/build logs, including the first compile diagnostic, are also preserved in `diagnostic-logs.tar.gz`; [diagnostic-logs-receipt.json](diagnostic-logs-receipt.json) indexes their exact hashes for portable review.
