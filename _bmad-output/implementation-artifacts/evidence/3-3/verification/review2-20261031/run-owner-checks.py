import pathlib,subprocess,sys
here=pathlib.Path(__file__).resolve().parent
runner=here/'run-mapped.py'
checks=[
 ('eventstore-server','references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests','Hexalith.EventStore.Server.Tests',['*CatalogSchemaThreePolicyTests','*DevelopmentDomainRouteCatalogTests','*RouteCatalogTopologyIsolationTests','*RouteCatalogV3IngressTests']),
 ('eventstore-contracts','references/Hexalith.EventStore/tests/Hexalith.EventStore.Contracts.Tests','Hexalith.EventStore.Contracts.Tests',['*RouteCatalogV3Tests']),
 ('tenants','references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests','Hexalith.Tenants.Contracts.Tests',['*TenantCatalogTests']),
 ('parties','references/Hexalith.Parties/tests/Hexalith.Parties.Contracts.Tests','Hexalith.Parties.Contracts.Tests',['*PartiesCatalogTests']),
 ('memories','references/Hexalith.Memories/tests/Hexalith.Memories.EventStore.Tests','Hexalith.Memories.EventStore.Tests',['*MemoriesCatalogTests']),
]
for name,directory,assembly,classes in checks:
 command=['dotnet','build',directory+'/'+assembly+'.csproj','-c','Debug','-p:GeneratePackageOnBuild=false','-p:BuildProjectReferences=false','-m:1','-nodeReuse:false','--disable-build-servers']
 result=subprocess.call([sys.executable,str(runner),name+'-build-verified',*command])
 if result:sys.exit(result)
 command=['dotnet',directory+'/bin/Debug/net10.0/'+assembly+'.dll']
 for value in classes:command+=['-class',value]
 result=subprocess.call([sys.executable,str(runner),name+'-tests-verified',*command])
 if result:sys.exit(result)
