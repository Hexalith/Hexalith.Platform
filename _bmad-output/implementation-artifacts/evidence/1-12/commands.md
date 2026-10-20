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
