# Story 1.12 local verification commands

Run each owner project individually. No nested reference was initialized in Platform.

```bash
python3 references/Hexalith.Parties/scripts/render-platform-declaration.py --check
python3 -B -m unittest discover -s references/Hexalith.Parties/scripts -p test_platform_declaration.py -v
dotnet tool run hexalith-module -- validate --manifest references/Hexalith.Parties/module/hexalith.module-manifest.v2.json --manifest references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json --manifest references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json --manifest references/Hexalith.Memories/module/hexalith.module-manifest.v2.json --output json
# Broad gate; currently blocked by unrelated Builds/Works/Identity sources:
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj --configuration Release --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
# Focused composition compile:
dotnet build src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj --configuration Debug --no-restore -m:1 -p:BuildProjectReferences=false -p:BuildsToolingAssemblyPath=/home/administrator/.nuget/packages/hexalith.builds.module.cli/4.34.0/tools/net10.0/any/Hexalith.Builds.Tooling.dll -p:NuGetAudit=false
# Focused test compile isolates concurrent Story 1.13 DomainEnrollmentKitTests only.
# focused-lane.targets is copied verbatim to /tmp/1-12-focused-tests.targets.
dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj --configuration Debug --no-restore -m:1 -p:BuildProjectReferences=false -p:BuildsToolingAssemblyPath=/home/administrator/.nuget/packages/hexalith.builds.module.cli/4.34.0/tools/net10.0/any/Hexalith.Builds.Tooling.dll -p:CustomBeforeDirectoryBuildTargets=/tmp/1-12-focused-tests.targets -p:NuGetAudit=false
cp /home/administrator/.nuget/packages/hexalith.builds.module.cli/4.34.0/tools/net10.0/any/Hexalith.Builds.Tooling.dll tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll
dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class Hexalith.Platform.Composition.Tests.PartiesAdoptionTests
# In Parties (Platform submodule): inject only the already initialized direct catalog.
dotnet build tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj --configuration Release -m:1 -p:HexalithBuildsRoot=/home/administrator/projects/hexalith/platform/references/Hexalith.Builds -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
dotnet tests/Hexalith.Parties.Security.Tests/bin/Release/net10.0/Hexalith.Parties.Security.Tests.dll
# Same build flags for Hexalith.Parties.Tests and Hexalith.Parties.Ci.Tests, run individually.
# The independent clone has its direct catalog initialized and needs no catalog property.
```

`dotnet test` in Parties returned an MTP handshake failure without running tests;
the documented fallback of building then invoking the xUnit assembly was used.
The focused lane does not qualify the broad gate as passed.

## Independent source proof and handoff

- Clone: `/tmp/hexalith-story-1-12/Hexalith.Parties` (retained for local integration).
- Distracting source fixture: `/tmp/hexalith-story-1-12/Hexalith.EventStore`.
- Accepted isolated Dapr home: `/home/administrator/.hexalith/dapr/1.18.2`.
- Packaged Tooling: `/home/administrator/.nuget/packages/hexalith.builds.module.cli/4.34.0/tools/net10.0/any/Hexalith.Builds.Tooling.dll`.
- netcoredbg: `/tmp/claude-1000/-home-administrator-projects-hexalith-platform/d8ec5f37-4955-4368-a392-1396fff05b5c/scratchpad/tools/netcoredbg/netcoredbg`.
- `handoff-owner-state.json` records the owner merge/index state; preserve that
  state when importing the three local follow-up commits.

From the independent clone:

```bash
dotnet tool restore
HEXALITH_DAPR_HOME=/home/administrator/.hexalith/dapr/1.18.2 dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json
# Record run ID from the tool run-state, use launcher PID only to locate its actual service child.
# tools/attach_ready.py <netcoredbg> <child-pid> <absolute-middleware.cs> <bound-http-url>/ready
HEXALITH_DAPR_HOME=/home/administrator/.hexalith/dapr/1.18.2 dotnet tool run hexalith-module -- down --manifest module/hexalith.module-manifest.v2.json --run-id <exact-run-id>
```

The six CI fixture cases were prepared with individual `dotnet restore` of
Contracts.Tests, Client.Tests, AdminPortal.Tests, ConsumerPortal.Tests, UI.Tests,
then `dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj --configuration Release -m:1 -p:NuGetAudit=true -p:MinVerVersionOverride=1.0.0`.
Exact commands are logged in `ci-security-fixture-setup.txt`; rerun only
`Hexalith.Parties.Ci.Tests.SignalRSecurityFloorTests` to perform its own
`publish --no-restore --no-build` check.

Run `python3 -I _bmad-output/implementation-artifacts/evidence/1-12/tools/run_reference_checks.py`
from Platform for the reference ACL matrix using the accepted validator. This
focused adapter does not replace the separately blocked source-build gate.

## Platform-workspace live smoke

An independent Platform checkout at `platform-smoke-revisions.json` was created
from P1 using local direct-reference clones, with Parties/Tenants pins committed
before startup. All nested references stayed uninitialized. From that checkout:

```bash
dotnet tool restore
HEXALITH_DAPR_HOME=/home/administrator/.hexalith/dapr/1.18.2 dotnet tool run hexalith-module -- debug --manifest references/Hexalith.Parties/module/hexalith.module-manifest.v2.json
HEXALITH_DAPR_HOME=/home/administrator/.hexalith/dapr/1.18.2 dotnet tool run hexalith-module -- down --manifest references/Hexalith.Parties/module/hexalith.module-manifest.v2.json --run-id 4e6371d42e84ac10b33eb3ae43cbcd58
```

Result: Ready in 287.04 seconds, exactly seven healthy application servers;
state and workspace removed by exact down. Setup, pinned commitlint, revisions,
source mapping, direct-reference status, readiness and cleanup are committed.
Owner approval and upstream integration remain; the parent pin is included in the local review commit.

## Final review reruns

See `review-check-results.json` for exact coordinator commands, working directories and zero exits. The owner normal Release and composition normal Debug builds also pass; review build summaries retain their full-output hashes. No test/source exclusion was used in these final builds.
