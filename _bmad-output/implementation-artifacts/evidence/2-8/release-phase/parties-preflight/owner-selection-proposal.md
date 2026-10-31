# Parties source-selection proposal

The user explicitly ratified these seven exact revisions: “Approve these seven existing revisions”. No ledger entry or source pointer has yet changed.

Retain the seven exact selections already committed in Parties main `4e2b2e8dae4835944adb55ccc8a86acbc470897e`, and record their source-selection approval under `jpiquot`. This supplies no test, package, release, deployment or runtime acceptance. Complete current-source CI and release gates remain mandatory.

| Dependency | Exact selected commit | Commit subject |
| --- | --- | --- |
| Hexalith.Builds | [81c88b34d09d34260b92d7f0928dbbc1c7446baa](https://github.com/Hexalith/Hexalith.Builds/commit/81c88b34d09d34260b92d7f0928dbbc1c7446baa) | build(g6): approve the SDK 10.0.403 and Dapr 1.19.0 tuple |
| Hexalith.EventStore | [6bcd40e3158d5b99e8d94cd300575768a6e479ab](https://github.com/Hexalith/Hexalith.EventStore/commit/6bcd40e3158d5b99e8d94cd300575768a6e479ab) | test: await owned subprocess termination |
| Hexalith.FrontComposer | [86e07babdf88329dffc60f0c4fb5b566a35063c4](https://github.com/Hexalith/Hexalith.FrontComposer/commit/86e07babdf88329dffc60f0c4fb5b566a35063c4) | docs: record qualified dependency transition |
| Hexalith.McpCli | [d7e21771079a2302e7a2e958dccdd12d94f56070](https://github.com/Hexalith/Hexalith.McpCli/commit/d7e21771079a2302e7a2e958dccdd12d94f56070) | docs: record the story 4.17 code review |
| Hexalith.Memories | [2649a946aa55cdd570c9beb15f9c91bc01703a26](https://github.com/Hexalith/Hexalith.Memories/commit/2649a946aa55cdd570c9beb15f9c91bc01703a26) | test: report owned workflow readiness failures |
| Hexalith.Platform | [82c9a31a0565c6dfd47750f5c67c087b487e6300](https://github.com/Hexalith/Hexalith.Platform/commit/82c9a31a0565c6dfd47750f5c67c087b487e6300) | build: preserve concurrent platform work |
| Hexalith.Tenants | [9e5a991d0ba304bac7b1a5809cd71797b47d78ed](https://github.com/Hexalith/Hexalith.Tenants/commit/9e5a991d0ba304bac7b1a5809cd71797b47d78ed) | build: preserve concurrent release alignment |

Builds supplies the selected package catalog (SDK 3.130.0, Tenants 6.0.0, Memories 3.0.13). Existing package-mode builds resolve external Hexalith dependencies from nuget.org; the other pointers retain the owner’s recorded diagnostic/source snapshot. The real Parties service and adapter compile in supported Release/package mode with zero warnings/errors. These two probes do not replace the complete CI suite.

The existing `.gitlink-signoff.tsv` requires a real release-owner handle for each `validated-advance`. Both the independent push-range check and release-floor check currently fail on these exact seven entries. A reset would be a separate deliberate dependency change requiring evaluation; it is not performed automatically.

If ratified, add only these exact seven rows with the actual approval date and source-selection-only explanation, through a normal Parties PR. Preserve all prior ledger rows and source pointers. Run the owning ledger gate, full CI and normal publication checks before any release.
