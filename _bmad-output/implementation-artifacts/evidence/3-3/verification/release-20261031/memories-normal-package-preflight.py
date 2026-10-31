"""Run the owner's normal package-only gate against signed delivered SDK dependencies; no publication."""
from pathlib import Path
import os,subprocess,json,shutil
base=Path('/tmp/hexalith-story33-release-0_wjbwkj');root=base/'Hexalith.Memories';shim=base/'ordinary-compiler-tools/Memories';shim.mkdir(parents=True,exist_ok=True)
evidence=Path(__file__).parent
real=shutil.which('dotnet');props=str(base/'owner-release-catalogs/Memories/Props/Directory.Packages.props')
config=str(base/'ordinary.NuGet.Config')
globals=['-p:UseHexalithProjectReferences=false','-p:UseNuGetDeps=true','-p:Hexalith1BuildPackageProps='+props,'-p:HexalithBuildPackageVersions1='+props,'-p:RestoreConfigFile='+config,'-p:GeneratePackageOnBuild=false']
code='#!/usr/bin/env python3\nimport os,sys,json,datetime\nargs=sys.argv[1:]\n'
code+='if args and args[0] in ["build","pack","restore","msbuild"]:\n    args += '+repr(globals)+'\n'
code+='    if args[0] in ["build","pack"]: args += ["-m:1","-nr:false","--disable-build-servers"]\n'
code+='with open('+repr(str(evidence/'memories-stable-format-package-effective-commands.jsonl'))+',"a") as f: f.write(json.dumps({"argv":args,"cwd":os.getcwd()})+"\\n")\n'
code+='os.execv('+repr(real)+','+repr([real])+'+args)\n'
(shim/'dotnet').write_text(code);(shim/'dotnet').chmod(0o755)
env=dict(os.environ,PATH=str(shim)+os.pathsep+os.environ['PATH'],NUGET_PACKAGES=str(base/'ordinary-package-cache'),NUGET_HTTP_CACHE_PATH=str(base/'ordinary-http-cache'))
cmd=['pwsh','-NoLogo','-NoProfile','-File','tools/pack-release.ps1','-Version','0.0.0','-PackageOnly','-OutputDirectory',str(base/'release-candidate-packages/Memories/0.0.0')]
print(json.dumps({'argv':cmd,'ordinaryCache':env['NUGET_PACKAGES'],'originalPinnedCatalog':props,'publication':False}),flush=True)
r=subprocess.run(cmd,cwd=root,env=env,timeout=600);raise SystemExit(r.returncode)
