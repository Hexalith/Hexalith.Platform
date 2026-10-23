# Current-tree Story 2.3 qualification — 2026-10-23

Both required live gates pass on the current source: the two-run proof passes in 312.595 seconds and the direct file-entry smoke in 142.294 seconds (wall time). Each executes one test, with zero failures/skips. [Commands, environments and results](results.json), [concurrent output](tests-concurrent.log), [file-entry output](tests-file-entry.log), [source before](source-before.json), [source after](source-after.json) and [tool versions/hashes](tools.json) bind the results to the tested tree. Source hashes and staged gitlinks remained unchanged.

Both six-server sessions reach fresh declared readiness while they coexist ([run 0](concurrent/concurrent-readiness-0.json), [run 1](concurrent/concurrent-readiness-1.json)). Each inventories six project HTTP endpoints and 24 native sidecar listeners. The executed proof checks disjoint ports/container IDs, separate discovery/certificates/writable storage, distinct persisted values under identical keys in all three stores, then fresh [survivor readiness](concurrent/survivor-readiness.json) and retained values after stopping its peer. [Persisted values](concurrent/persisted-values.txt) and the [cleanup audit](concurrent/cleanup-audit.json) retain the observations. Both owned sessions report successful cleanup; no containers with either run label remain. The file-entry smoke also asserts successful owned stop.

Normal Composition and Builds Module test/AppHost builds pass without warnings/errors. The broader Platform/Timesheets suite passes 230 cases (236 total, six gated live skips), the Builds runtime subset passes 27, and the bounded occupied-port proof passes one without skips. See the [build/ordinary/occupied sequence](../current-tree-20261023T205721Z/results.json), [Platform suite](../current-tree-20261023T205721Z/tests-platform-ordinary.log), [Builds subset](../current-tree-20261023T205721Z/tests-builds-runtime.log), [occupied proof](../current-tree-20261023T205721Z/tests-occupied-port.log) and [focused patch verification](../final-state-patches-20261023/README.md). The latter also records the passing normal Composition build and 23 focused cases.

## Failure diagnosed and corrected

The initial [concurrent attempt](../current-tree-20261023T205721Z/concurrent/startup-failure.json) failed HXR025 when Tenants exited. Its owned cleanup succeeded with no leftovers. The first [file-entry attempt](../current-tree-20261023T210329Z/tests-file-entry.log) exposed four CS0618 build errors from obsolete EventStore failure-name aliases. The clean Tenants owner checkout received eight reference-name substitutions across two handlers and existing audit assertions; their wire values are identical. Both normal Debug/source owner builds pass without warnings/errors, with 285 authority/audit query cases and 119 audit persistence integration cases passing without skips. See [compatibility correction and verification](../final-state-patches-20261023/README.md#current-tenants-source-compatibility). Both live gates above were then rerun and passed. No package selection or build/readiness/security gate changed.

## Application inventory limitation

The separate Tenants AppHost is running in both inventories, but its AppHost/CLI PIDs changed during this qualification. The wrapper therefore returned exit 1 for its strict identity-preservation audit even though both test processes returned 0; this is not represented as a passing unchanged-PID assertion. See the [raw audit](audit.json) and [application observation](application-observation.json). This workflow's lifecycle calls target only the Platform file AppHost or its owned Builds sessions; no call targeted the separate Tenants AppHost. The inventories alone do not establish why its process IDs changed.

## Executed matrix coverage

| Matrix row | Executed passing coverage |
| --- | --- |
| Identical selections | Ordinary `IdenticalSelectionsOwnPortsCredentialsDiscoveryAndStorageAsync` (signed/file) plus the two real Builds sessions above. |
| Explicit certificate parent | Ordinary checkout/dangling-link/linked-ancestor/mapped-owner/private-child refusal and registered-service cleanup tests; focused Works checkout and default/external disposal cases. |
| Selected runtime unavailable | Ordinary `MissingSelectedNativeRuntimeRefusesStartupAndPreservesOwnedDiagnosticsAsync`; selected/fallback runtime cases also pass. |
| Startup failure or occupied port | Ordinary subscriber failure-retention tests plus the executed Docker occupied-port proof, preserving its separately owned marker/listener through candidate cleanup. |

The macOS-specific containment branch was not executed on this Linux host. Works preview live qualification and the previously recorded independent lifecycle/provider findings remain deferred. No gitlinks were staged or remote operations performed.

## Workflow review

The blind, edge-case and verification-gap layers completed. All fourteen findings have [individual verdicts and evidence](review-triage.json); no new local run-isolation patch remains. Existing dispositions were carried, and the alleged proxyless allocation failure is disproved by the [executable probe](../proxyless-sidecar-check-20261023/run.log). Seven independent owner entries were appended to the deferred ledger. They include a high Custody unpin authorization defect, unverified native-stream/component-version questions, and retention/claim verification issues. These independent code paths do not execute in the qualified local reference runs.

## Completion and owner revision

Story 2.3 is `done` in the spec and `review` in sprint tracking. The local root commit retains the exact Tenants compatibility patch and tested owner hashes. Those three Tenants files remain uncommitted so its HEAD continues matching the root's pinned gitlink; advancing that owner without the forbidden gitlink update would break the qualified source mapping. Independent Story 2.4 edits are excluded from staging.
