"""One effect-free tool-only compatibility check; no runtime execution or pin adoption."""
from pathlib import Path
helper=Path(__file__).with_name('check-coherent-caller.py').read_text()
prefix=helper.split('paths = [')[0].replace("coherent-caller-2'","ordinary-4482-compatibility'")
prefix=prefix.replace("BASE = '4eca9e8fbad451a602a9ec7d2d6384345b6b0e4e'","BASE = '11053e562a2cba0196354cec655c01677e2afd46'")
exec(compile(prefix,str(__file__),'exec'))
manifest=WORK/'.config/dotnet-tools.json';original=manifest.read_bytes();data=json.loads(original)
assert data['tools']['hexalith.builds.module.cli']['version']=='4.44.13'
data['tools']['hexalith.builds.module.cli']['version']='4.48.2';manifest.write_text(json.dumps(data,indent=2)+'\n')
archive=Path('/home/administrator/.nuget/packages/hexalith.builds.module.cli/4.48.2/hexalith.builds.module.cli.4.48.2.nupkg')
assert hashlib.sha256(archive.read_bytes()).hexdigest()=='9b7c50125f0ddfc953c1c3d892cddec5ba2fa1d8741bd9e82d77669b4e86ee92'
ENV['GITHUB_ENV']=str(Path(os.environ['TMPDIR'])/'github-env.txt')
code=run('prepare-build-only',['python3','-B','references/Hexalith.Builds/Github/prepare-module-build/prepare_module_build.py','--manifest','module/hexalith.module-manifest.v2.json'],required=False)
selected={}
if code==0:
 for line in Path(ENV['GITHUB_ENV']).read_text().splitlines():
  key,value=line.split('=',1);selected[key]=value
 mapping=Path(selected['CustomAfterMicrosoftCommonProps']).parent/'mapping.json'
 (OUT/'mapping.json').write_bytes(mapping.read_bytes())
 (OUT/'catalog-versions.json').write_bytes((mapping.parent/'catalog-versions.json').read_bytes())
run('executing-version',['dotnet','tool','run','hexalith-module','--','--version'],required=False)
record={'callerBase':BASE,'originalManifestVersion':'4.44.13','checkedManifestVersion':'4.48.2',
 'actualToolSource':'6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b','actualToolArchiveSha256':'9b7c50125f0ddfc953c1c3d892cddec5ba2fa1d8741bd9e82d77669b4e86ee92',
 'catalogPin':pins['references/Hexalith.Builds'],'platformPin':pins['references/Hexalith.Platform'],
 'prepareExitCode':code,'sourceTemporaryDirtyOnlyForToolManifest':True,'nativeDeclarationSha256':hashlib.sha256((WORK/'module/hexalith.module-manifest.v2.json').read_bytes()).hexdigest(),
 'resourcesStarted':False,'appHostExecuted':False,'newQualification':False,'candidateAcceptance':False,'toolAccepted':False,
 'pinnedRootsOverlaid':False,'runtimeEligibilityClaimed':False,'executedHelperForFutureProof':'not-yet-selected; must carry f8 separately from immutable catalog'}
(OUT/'result.json').write_text(json.dumps(record,indent=2)+'\n');print(json.dumps(record),flush=True)
