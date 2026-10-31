"""Compile-only downstream candidate preflight against unpublished uniquely versioned SDK bytes."""
from pathlib import Path
import os,subprocess,json
base=Path('/tmp/hexalith-story33-release-0_wjbwkj')
checks=[('Hexalith.Tenants','src/Hexalith.Tenants/Hexalith.Tenants.csproj'),('Hexalith.Tenants','src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj'),('Hexalith.Parties','src/Hexalith.Parties/Hexalith.Parties.csproj'),('Hexalith.Memories','src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj'),('Hexalith.Memories','src/Hexalith.Memories.DomainService/Hexalith.Memories.DomainService.csproj'),('Hexalith.Memories','src/Hexalith.Memories.AccessTelemetry/Hexalith.Memories.AccessTelemetry.csproj'),('Hexalith.Memories','src/Hexalith.Memories.AccessTelemetry.Clock/Hexalith.Memories.AccessTelemetry.Clock.csproj'),('Hexalith.Builds','src/hosts/Hexalith.Builds.Module.EventStoreHost/Hexalith.Builds.Module.EventStoreHost.csproj')]
env=dict(os.environ,NUGET_PACKAGES=str(base/'preflight-cache'))
props=str(base/'Hexalith.Builds/Props/Directory.Packages.props')
r={'purpose':__doc__,'published':False,'sdkVersion':'3.129.0-story33-preflight.1','cache':env['NUGET_PACKAGES'],'checks':[]}
try:
    for owner,project in checks:
        cmd=['dotnet','build',project,'-c','Release','-p:UseHexalithProjectReferences=false','-p:UseNuGetDeps=true','-p:HexalithEventStoreVersion='+r['sdkVersion'],'-p:HexalithBuildPackageVersions1='+props,'-p:Hexalith1BuildPackageProps='+props,'-p:RestoreConfigFile='+str(base/'preflight.NuGet.Config'),'-p:GeneratePackageOnBuild=false','-m:1','-nr:false','--disable-build-servers']
        print(json.dumps({'owner':owner,'command':cmd}),flush=True)
        p=subprocess.run(cmd,cwd=base/owner,env=env,timeout=300)
        r['checks'].append({'owner':owner,'project':project,'command':cmd,'exitCode':p.returncode})
        if p.returncode: raise RuntimeError(owner+' candidate compile failed: '+project)
    r['result']='passed'
finally:
    (Path(__file__).parent/'downstream-sdk-preflight.json').write_text(json.dumps(r,indent=2)+'\n')
