from pathlib import Path
from collections import Counter
import subprocess,json,sys,os,hashlib,datetime,xml.etree.ElementTree as ET
p=Path('/tmp/story32-import-closure-candidate-6xkn9mrn/repository');freeze=Path('/tmp/story32-linear-tuple-freeze-dc1d66b1');descriptor=json.loads((freeze/'linear-source-receipt.json').read_bytes());source=descriptor['M'];out=freeze/'actual-M';assert not out.exists();out.mkdir();root=out/'repository';evidence=out/'evidence';evidence.mkdir()
def run(args,cwd=None,**kwargs):return subprocess.run(args,cwd=cwd,check=True,**kwargs)
run(['git','clone','--shared','--no-checkout',str(p),str(root)]);run(['git','checkout','--detach',source],root);run(['git','remote','set-url','origin','https://github.com/Hexalith/Hexalith.Platform.git'],root)
for line in subprocess.check_output(['git','config','--file','.gitmodules','--get-regexp',r'^submodule\..*\.path$'],cwd=root,text=True).splitlines():
 key,path=line.split(' ',1);url=subprocess.check_output(['git','config','--file','.gitmodules','--get',key.removesuffix('.path')+'.url'],cwd=root,text=True).strip();selected=subprocess.check_output(['git','rev-parse','HEAD:'+path],cwd=root,text=True).strip();owner=root/path;run(['git','clone','--shared','--no-checkout',str(p/path),str(owner)]);run(['git','checkout','--detach',selected],owner);run(['git','remote','set-url','origin',url],owner)
sys.path.insert(0,str(root/'eng'))
from prepare_builds_artifacts import source_state
from run_tuple_ci import snapshot_composition_test_outputs,verify_test_sdk
before_build=source_state();assert not before_build['dirty']
run(['python3','-B','eng/prepare_builds_artifacts.py','--output',str(out/'published-identity'),'--require-recorded'],root)
identity=json.loads((out/'published-identity/artifact-identity.json').read_bytes());tool=identity['toolRoot'];artifacts=root/'artifacts/current42-import-closure';env=os.environ.copy();env['NUGET_PACKAGES']='/tmp/story32-sdk42-upstream-catalog-package-cache'
build=['dotnet','build','tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj','-c','Release','-m:1','-nodeReuse:false','--disable-build-servers','--artifacts-path',str(artifacts),'-p:UseHexalithProjectReferences=false','-p:HexalithBuildsPublishedToolRoot='+tool]
with (evidence/'build.stdout').open('w') as stdout,(evidence/'build.stderr').open('w') as stderr:run(build,root,env=env,stdout=stdout,stderr=stderr)
metadata=['python3','-B','eng/test_source_sdk_propagation.py','--evidence',str(evidence/'source-sdk-metadata'),'--artifacts',str(artifacts),'--published-tool-root',tool]
with (evidence/'metadata.stdout').open('w') as stdout,(evidence/'metadata.stderr').open('w') as stderr:run(metadata,root,env=env,stdout=stdout,stderr=stderr)
assembly=artifacts/'bin/Hexalith.Platform.Composition.Tests/release/Hexalith.Platform.Composition.Tests.dll';verify_test_sdk(assembly,'4.2.0');before=source_state();assert before==before_build;outputs=snapshot_composition_test_outputs(assembly)
r={'source':source,'scope':'Actual committed current default SDK4.2 package inventory with fresh official signed owning tool and separate exact-M owner checkouts. Local controls only; no hosted, signed fixture or native qualification credit.','buildCommand':build,'metadataCommand':metadata,'sourceBefore':before,'outputBefore':outputs,'groups':[],'candidateAcceptance':False,'hostedQualification':False,'signedFixtureCredit':False,'status':'running'}
def save():(evidence/'receipt.json').write_text(json.dumps(r,indent=2)+'\n')
save();failed=False
try:
 for label,filename,required in [('current','eng/tuple-ci-sdk42-current-suites.json',456),('base','eng/tuple-ci-sdk42-base-suites.json',97),('extensions','eng/tuple-ci-extension-suites.json',9)]:
  profile=json.loads((root/filename).read_bytes());assert sum(g['requiredCount'] for g in profile['groups'])==required
  for index,g in enumerate(profile['groups']):
   name=label+'-'+str(index);xml=evidence/(name+'.xml');args=['dotnet',str(assembly)]+[arg for selector in g['selectors'] for arg in [g['filter'],selector]]+['-result-xml',str(xml)]
   with (evidence/(name+'.stdout')).open('w') as stdout,(evidence/(name+'.stderr')).open('w') as stderr:c=subprocess.run(args,cwd=root,env=env,stdout=stdout,stderr=stderr,timeout=1200)
   counts=ET.parse(xml).getroot().find('assembly').attrib if xml.exists() else {};okay=c.returncode==0 and int(counts.get('total',0))==g['requiredCount'] and int(counts.get('passed',0))==g['requiredCount'] and all(int(counts.get(k,0))==0 for k in ['failed','errors','skipped','not-run']);failed|=not okay;r['groups'].append({'name':name,'expected':g['requiredCount'],'purpose':g['purpose'],'command':args,'exitCode':c.returncode,'counts':counts,'passed':okay});save();print(name,counts,'passed',okay,flush=True)
except BaseException as error:r['error']=repr(error);failed=True;raise
finally:
 ids=Counter((t.get('type'),t.get('method'),t.get('name')) for f in evidence.glob('*.xml') for t in ET.parse(f).getroot().iter('test'));r['executionCount']=sum(ids.values());r['uniqueCaseCount']=len(ids);r['duplicateCases']=[{'type':k[0],'method':k[1],'name':k[2],'executions':v} for k,v in sorted(ids.items()) if v!=1];r['sourceAfter']=source_state();r['outputAfter']=snapshot_composition_test_outputs(assembly);r['sourceUnchanged']=before==r['sourceAfter'];r['outputsUnchanged']=outputs==r['outputAfter'];r['status']='passed' if not failed and r['executionCount']==562 and r['uniqueCaseCount']==559 and len(r['duplicateCases'])==3 and all(x['executions']==2 for x in r['duplicateCases']) and r['sourceUnchanged'] and r['outputsUnchanged'] else 'failed';r['files']={str(f.relative_to(evidence)):{'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size} for f in sorted(evidence.rglob('*')) if f.is_file() and f.name!='receipt.json'};save()
print(evidence,r['status'],flush=True);raise SystemExit(0 if r['status']=='passed' else 1)
