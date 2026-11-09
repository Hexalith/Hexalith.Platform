"""Run genuine historical metadata/source checkouts locally without spoofing hosted CI."""
from pathlib import Path
import argparse,subprocess,json,sys,hashlib,os,datetime,xml.etree.ElementTree as ET
ap=argparse.ArgumentParser();ap.add_argument('--label',choices=['L','N'],required=True);a=ap.parse_args()
p=Path('/tmp/story32-import-closure-candidate-6xkn9mrn/repository');freeze=Path('/tmp/story32-linear-tuple-freeze-dc1d66b1');descriptor=json.loads((freeze/'linear-source-receipt.json').read_text());source=descriptor[a.label];current=descriptor['M'];out=freeze/('actual-'+a.label);assert not out.exists();out.mkdir();root=out/'repository';evidence=out/'evidence';evidence.mkdir()
def run(argv,cwd=None,**kw):return subprocess.run(argv,cwd=cwd,check=True,**kw)
run(['git','clone','--shared','--no-checkout',str(p),str(root)]);run(['git','checkout','--detach',source],root);run(['git','remote','set-url','origin','https://github.com/Hexalith/Hexalith.Platform.git'],root)
modules=subprocess.check_output(['git','config','--file','.gitmodules','--get-regexp','^submodule\\..*\\.path$'],cwd=root,text=True).splitlines()
for line in modules:
 key,path=line.split(' ',1);url=subprocess.check_output(['git','config','--file','.gitmodules','--get',key.removesuffix('.path')+'.url'],cwd=root,text=True).strip();selected=subprocess.check_output(['git','rev-parse',source+':'+path],cwd=root,text=True).strip();owner=root/path;run(['git','clone','--shared','--no-checkout',str(p/path),str(owner)]);run(['git','checkout','--detach',selected],owner);run(['git','remote','set-url','origin',url],owner)
assert not subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True).strip()
sys.path.insert(0,str(root/'eng'))
from prepare_builds_artifacts import source_state
from legacy_tuple_source import verify_parties_checkout
from run_tuple_ci import validate_profile,verify_historical_catalog_pin,snapshot_composition_test_outputs,verify_test_sdk
if a.label=='L':
 from legacy_tuple_source import verify
 required=349
else:
 from pre_extension_tuple_source import verify
 required=412
comparison=verify(root,current,source);verify_parties_checkout(root,historical=a.label=='L');(evidence/'actual-source-comparison.json').write_text(json.dumps(comparison,indent=2)+'\n');verify_historical_catalog_pin(root/'references/Hexalith.Builds/Props/Directory.Packages.props')
args=['python3','-B','eng/prepare_builds_artifacts.py','--output',str(out/'published-identity'),'--require-recorded'];run(args,root)
identity=json.loads((out/'published-identity/artifact-identity.json').read_text());tool=identity['toolRoot'];artifacts=root/'artifacts'/('replay-'+a.label);env=os.environ.copy();env['NUGET_PACKAGES']='/tmp/story32-sdk42-upstream-catalog-package-cache'
build=['dotnet','build','tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj','-c','Release','-m:1','-nodeReuse:false','--disable-build-servers','--artifacts-path',str(artifacts),'-p:UseHexalithProjectReferences=false','-p:HexalithBuildsPublishedToolRoot='+tool]
with (evidence/'build.stdout').open('w') as stdout,(evidence/'build.stderr').open('w') as stderr:run(build,root,env=env,stdout=stdout,stderr=stderr)
metadata=['python3','-B','eng/test_source_sdk_propagation.py','--evidence',str(evidence/'source-sdk-metadata'),'--artifacts',str(artifacts),'--published-tool-root',tool]
with (evidence/'metadata.stdout').open('w') as stdout,(evidence/'metadata.stderr').open('w') as stderr:run(metadata,root,env=env,stdout=stdout,stderr=stderr)
assembly=artifacts/'bin/Hexalith.Platform.Composition.Tests/release/Hexalith.Platform.Composition.Tests.dll';profile=json.loads((root/'eng/tuple-ci-suites.json').read_bytes());assert validate_profile(profile)==required;verify_test_sdk(assembly,'4.1.0');state=source_state();assert not state['dirty'],state['status'];outputs=snapshot_composition_test_outputs(assembly)
r={'scope':'Actual committed same-code historical checkout, signed published owning tool, actual SDK4.1 package inventory; local controls only, no hosted/defaultSDK4.2/signed-fixture acceptance credit.','label':a.label,'source':source,'currentSource':current,'required':required,'sourceComparison':comparison,'buildCommand':build,'metadataCommand':metadata,'sourceBefore':state,'outputBefore':outputs,'groups':[],'status':'running','candidateAcceptance':False,'hostedQualification':False,'signedFixtureCredit':False}
def save():(evidence/'receipt.json').write_text(json.dumps(r,indent=2)+'\n')
save();failed=False
try:
 for i,g in enumerate(profile['groups']):
  xml=evidence/('group-'+str(i)+'.xml');argv=['dotnet',str(assembly)]+[arg for name in g['selectors'] for arg in [g['filter'],name]]+['-result-xml',str(xml)]
  with (evidence/('group-'+str(i)+'.stdout')).open('w') as stdout,(evidence/('group-'+str(i)+'.stderr')).open('w') as stderr:c=subprocess.run(argv,cwd=root,env=env,stdout=stdout,stderr=stderr,timeout=1200)
  counts=ET.parse(xml).getroot().find('assembly').attrib if xml.exists() else {};okay=c.returncode==0 and int(counts.get('total',0))==g['requiredCount'] and int(counts.get('passed',0))==g['requiredCount'] and all(int(counts.get(k,0))==0 for k in ['failed','errors','skipped','not-run']);failed|=not okay;r['groups'].append({'index':i,'purpose':g.get('purpose'),'command':argv,'expected':g['requiredCount'],'counts':counts,'exitCode':c.returncode,'passed':okay});save();print(a.label,'group',i,'actual',counts,'passed',okay,flush=True)
except BaseException as error:
 r['error']=repr(error);failed=True;raise
finally:
 r['sourceAfter']=source_state();r['outputAfter']=snapshot_composition_test_outputs(assembly);r['sourceUnchanged']=r['sourceBefore']==r['sourceAfter'];r['outputsUnchanged']=r['outputBefore']==r['outputAfter'];r['actualPass']=sum(int(x['counts'].get('passed',0)) for x in r['groups']);r['actualFail']=sum(int(x['counts'].get('failed',0)) for x in r['groups']);r['actualExecutions']=sum(int(x['counts'].get('total',0)) for x in r['groups']);r['status']='passed' if not failed and r['actualExecutions']==required and r['sourceUnchanged'] and r['outputsUnchanged'] else 'failed';r['files']={str(f.relative_to(evidence)):{'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size} for f in sorted(evidence.rglob('*')) if f.is_file() and f.name!='receipt.json'};save()
print(a.label,r['status'],r['actualPass'],r['actualFail'],evidence,flush=True);raise SystemExit(0 if r['status']=='passed' else 1)
