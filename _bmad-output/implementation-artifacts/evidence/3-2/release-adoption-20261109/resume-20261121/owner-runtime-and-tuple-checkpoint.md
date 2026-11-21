# Current tuple and Builds catalog runtime

Platform [main 91147a49](https://github.com/Hexalith/Hexalith.Platform/commit/91147a498410460ad20c3bc10c3ff8fed0c189da)
included equivalent AppHost pin corrections and passed the catalog workflow.
Its tuple reached the first required test, where genuine EventStore 5.4.0
conflicted with the fixture's static 4.2.0 expectation. The retained xUnit
receipt reports one executed and failed case.
[Catalog](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44893921388),
[tuple](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44893921517).

The current test fixtures now compare materialized owner selection to the
committed owner lock and independently verify the loaded SDK version and DLL
bodies against locked signed archives. The synthetic current MCP pin comes
from its locked version. The positive production model check compares restore
assets to the exact verified Server archive member. Negative controls change
the currently selected version and preserve the historic refused versions.
All existing selectors and case counts remain required.

The exact published Builds 6.5.0 tool passed the signature and Platform owner
lock checks. A standalone .NET 11 regression harness with no MSBuild runtime
assemblies then failed within the catalog materializer because the parent
resolved the MSBuild task type before starting its worker. The exact owner
fix `055d7df5b49669d5b705ce163b0b2e187ecb9a50` passed the same harness. The
child SDK materialized the actual Platform Composition and EventStoreHost
consumer views and the parent remained free of loaded MSBuild assemblies.
An exact-owner build reported zero warnings and errors and all 78 focused
materialization and model cases passed. The first diagnostic's inherited
NuGet cache setting caused four explicit cache-path failures, which the
unchanged cases passed in the normal test execution environment.
[Owner fix](https://github.com/Hexalith/Hexalith.Builds/commit/055d7df5b49669d5b705ce163b0b2e187ecb9a50).

The final local replay with signed Builds 6.5.0 executed all 456 mandatory
package component cases through their existing selectors. The current profile
passed 364 of 380, the owner profile passed 63 of 67, and the extension profile
passed 9 of 9. All 20 failures came from the same Builds runtime error. Each
job used `run-with-temp`, exactly recorded direct dependency revisions, and
the published tool. The wrapper waited for the selected tests before
cleaning the disposable repository, restore caches, and outputs. Raw local
XML, logs, patches and replay commands are retained in the durable
implementation worktree under `artifacts/story-3-2/`.

The normal prerequisite-aware Builds release was dispatched once for exact
owner main. Its signed release and the next Platform tuple still require
verification. The current Platform publisher PR also requires CODEOWNER
approval for normal merge.
[Owner CI](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44895427410),
[owner release](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44896050418),
[Platform PR](https://github.com/Hexalith/Hexalith.Platform/pull/53).

The maintainer-directed authenticated NuGet readback confirmed the active
fixture policy. Its package filter was narrowed to the exact
`Hexalith.Platform.TestExtension` ID. It authorizes new packages and versions
for `Hexalith/Hexalith.Platform`, workflow `test-extension-release.yml`, and
environment `production`. The original `release.yml` policy was preserved.
The fixture publisher awaits the exact current Platform source release.
Signed fixture publication, finite owner and host admission, and actual
packaged startup remain pending.
