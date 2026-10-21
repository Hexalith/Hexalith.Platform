# Pass-3 local corrections — 2026-10-21

All 18 accepted pass-3 patch findings are implemented locally. The source and artifact hashes, exact commands and outputs are recorded in [verification.json](verification.json). No other module was edited or enrolled, and no new publication or accepted pin is claimed. The accepted tool remains 4.35.1 with the existing release-tag gitlink.

Builds Release build passes with zero warnings/errors. The adoption and manifest-command classes passed 325/325 before the final optional-file guard refinement. The final 15-case custom-property group, including that new legitimate guard, passed afterward. The coordinator then found a false failure on Parties' HTTPS-validation regex; the leaf-property correction below supersedes that candidate. That delegated full run ended without a final summary or exit status and is incomplete; the final coordinator run below supersedes it. The earlier deliberately interrupted broad run is retained separately with its cancellation reason.

Platform required a Release restore to replace stale assets. Its final normal Release build passes with zero warnings/errors, and all five kit cases pass through the actual published 4.35.1 binary. The test now checks both the exact version and its full source commit against Platform HEAD's recorded Builds gitlink.

[Both real-Parties formats](real-module-commands.json) preserved Git status and all 6,035 tracked-file hashes. Workspace enumeration passes across 474,480 tracked-path bytes, and asserted prerequisite probes no longer fail dependency selection. Exit 1 remains expected for incomplete enrollment: uninitialized required references, old pin, missing inventory, programmatic discovery and literal component defaults/source names. These are separate from the corrected false failure. Parties' historical Story 1.12 qualification predates the kit.

Owner review of this new candidate, exact-source upstream CI, gated publication and fresh released-pin/range/gitlink acceptance remain pending. Human SM-2 and retirement authority remain module-owner gates. The saved [Builds product diff](builds-product.diff) is local source; it grants no release authority.


## Coordinator correction: distinguish leaf values from paths

The earlier real-Parties output falsely failed `Directory.Build.targets`: `_ContainerProvenanceHttpsUrlPattern` starts with `\Ahttps://…`, which unconditional slash normalization made look rooted. Leaf-value path resolution now first requires an explicit filesystem path form. The exact real regex, HTTPS repository URL and container-image values pass; direct, sibling, ancestor, Windows-style sibling and rooted dependency paths retain failing controls. Reserved tool-property assignments remain forbidden.

The final Builds build and normal Platform build pass with zero warnings/errors. All 21 custom-property cases pass, and all five published-pin Platform kit cases pass after the correction. The repeated real-Parties human/JSON checks now explicitly require and obtain `dependency-selectors` PASS, while preserving Git state and every tracked-file hash. The before-correction snapshots are retained under `real-module-before-leaf-correction-*`.

The previous full run was deliberately stopped at the coordinator's request, with 774 cases reached; its cancellation exception is retained in `full-builds-tests-before-leaf-correction-interrupted.txt`. That delegated run did not retain a completion summary or exit code and is not passing evidence. Source/artifact hashes and exact new commands are updated in `verification.json`. No release identity changed.

## Final reviewed local candidate

Builds candidate `af4bf9f2e4d3fd913c536a08bb0c9e8a54f9f260` contains all 18 accepted pass-3 fixes, the coordinator's leaf-value correction and all eight surviving post-review correction/regression groups. All three required review layers completed; every finding has an individual verdict in the story's triage log. No new finding was deferred. The existing Parties production-code deferral remains owner-led work.

Final coordinator verification passed **1,420/1,420 Builds cases**, with zero errors, failures, skips or not-run cases in 563.736 seconds ([full output](post-review-full-builds-tests.txt), [exit code](post-review-full-builds-tests.exitcode)). Both Release builds have zero warnings/errors. The affected patch checkpoint passed 168 cases, then the final Dapr refinement passed all 56 cases after its analyzer correction; the full suite ran the complete final source. All **5/5 Platform kit cases** pass through the actual published 4.35.1 binary, including full source metadata versus the committed release gitlink.

The final local CLI passes both workspace and dependency-selection audits on real Parties; human/JSON runs preserve Git status and all **6,035 tracked-file hashes**. Overall exit 1 records the module's remaining enrollment work. See [final real-module evidence](post-review-real-module-commands.json), [commands and source/artifact hashes](coordinator-verification.json), [matrix audit](coordinator-matrix-audit.json) and [candidate identity](reviewed-builds-candidate.json). The final source hashes remained unchanged throughout full verification.

The earlier stale-assets failure, deliberately cancelled runs, delegated incomplete run and analyzer failure are retained as historical diagnostics, not passes. The final coordinator outputs supersede them. Owner review of this exact new candidate, exact-source upstream CI, existing gated publication and fresh published-pin/range/tag/gitlink acceptance remain pending. Platform retains published 4.35.1 and its exact release gitlink. The spec stays **in-review** and sprint status **review**; the frozen completion constraint takes precedence over the generic workflow's automatic done status. No push or remote mutation occurred. Human SM-2 and retirement authority remain separate module-owner gates.
