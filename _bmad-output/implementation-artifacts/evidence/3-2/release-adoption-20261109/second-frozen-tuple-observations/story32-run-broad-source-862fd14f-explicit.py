"""Full bounded source inventory against retained history; no hosted or signed fixture acceptance."""
from pathlib import Path
import subprocess,os,signal,time,json,hashlib,sys,tempfile,xml.etree.ElementTree as ET,datetime
root=Path('/tmp/story-3-2-broad-20261109-_z_wy84q/current');sys.path.insert(0,str(root/'eng'))
from prepare_builds_artifacts import source_state
from run_tuple_ci import snapshot_composition_test_outputs
out=Path(tempfile.mkdtemp(prefix='story32-broad-source-862fd14f-explicit-'));Path('/tmp/story32-broad-source-862fd14f-explicit-locator.txt').write_text(str(out)+'\n');source=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip();assert source=='862fd14f17bf48432c76993afd668b5a067e887d';before=source_state();assert not before['dirty'];artifacts=root/'artifacts/story32-broad-source-862fd14f-explicit-source';dll=artifacts/'bin/Hexalith.Platform.Composition.Tests/debug/Hexalith.Platform.Composition.Tests.dll';tool='/tmp/story32-builds447-local-qualified-artifacts/published/tools/net10.0/any';env=os.environ.copy();env['NUGET_PACKAGES']='/tmp/story32-sdk42-upstream-catalog-package-cache';r={'recordedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':source,'scope':'Full local Debug/source inventory and retained-baseline comparison only. No hosted, default-cohort or signed-fixture qualification credit.','before':before,'commands':[],'classes':[],'candidateAcceptance':False,'hostedQualification':False,'signedFixtureCredit':False,'status':'running'}
def save():(out/'receipt.json').write_text(json.dumps(r,indent=2)+'\n')
def run(argv,name,limit):
 started=time.monotonic();log=out/name
 with log.open('w') as output:
  child=subprocess.Popen(argv,cwd=root,env=env,stdout=output,stderr=subprocess.STDOUT,start_new_session=True)
  try:code=child.wait(timeout=limit);timed=False
  except subprocess.TimeoutExpired:
   timed=True;os.killpg(child.pid,signal.SIGTERM)
   try:child.wait(timeout=10)
   except subprocess.TimeoutExpired:os.killpg(child.pid,signal.SIGKILL);child.wait()
   code=child.returncode
 record={'command':argv,'timeoutSeconds':limit,'exitCode':code,'timedOut':timed,'seconds':round(time.monotonic()-started,3),'log':name,'sha256':hashlib.sha256(log.read_bytes()).hexdigest()};r['commands'].append(record);save();return record
outputs=None
try:
 restore=run(['dotnet','tool','restore'],'tool-restore.log',180)
 if restore['exitCode']!=0:raise RuntimeError('Pinned tool restore failed; see actual log.')
 build=run(['dotnet','build','tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj','-c','Debug','-m:1','-nodeReuse:false','--disable-build-servers','--artifacts-path',str(artifacts),'-p:UseHexalithProjectReferences=true','-p:HexalithBuildsPublishedToolRoot='+tool],'build.log',1200)
 if build['exitCode']!=0:raise RuntimeError('Fresh normal source build failed; see actual log.')
 outputs=snapshot_composition_test_outputs(dll);r['outputBefore']=outputs
 discover=run(['dotnet','exec',str(dll),'-list','classes/json','-noColor'],'classes.json',60)
 if discover['exitCode']!=0:raise RuntimeError('Full class discovery failed.')
 raw=(out/'classes.json').read_text();classes=json.loads(raw[raw.index('['):raw.rfind(']')+1]);r['discoveredClasses']=classes;save();print('discovered',len(classes),flush=True)
 for index,cls in enumerate(classes):
  name=f'{index:03d}-'+cls.rsplit('.',1)[-1];xml=out/(name+'.xml');limit=3600 if cls.endswith('PlatformAppModelTests') else 900 if cls.endswith(('EventStoreExtensionCompositionTests','PlatformRoutingCatalogIntegrationTests')) else 360
  result=run(['dotnet','exec',str(dll),'-class',cls,'-parallelMode','none','-maxThreads','1','-noColor','-longRunning','60','-result-xml',str(xml)],name+'.log',limit);result=dict(result,**{'class':cls,'xml':xml.name,'xmlPresent':xml.exists()})
  if xml.exists():
   assemblies=ET.parse(xml).getroot().findall('assembly');result['counts']={key:sum(int(a.get(key,'0')) for a in assemblies) for key in ['total','passed','failed','errors','skipped','not-run']}
  r['classes'].append(result);save();print(index+1,len(classes),cls.rsplit('.',1)[-1],result.get('counts',result),flush=True)
 tests=[];combined=ET.Element('assemblies')
 for record in r['classes']:
  if record['xmlPresent']:
   for assembly in ET.parse(out/record['xml']).getroot().findall('assembly'):
    combined.append(assembly)
    for case in assembly.iter('test'):tests.append({'class':case.get('type'),'method':case.get('method'),'name':case.get('name'),'result':case.get('result'),'time':case.get('time'),'failure':case.findtext('failure/message')})
 ET.ElementTree(combined).write(out/'combined.xml',encoding='utf-8',xml_declaration=True);(out/'tests.json').write_text(json.dumps(tests,indent=2)+'\n');r['counts']={key:sum(c.get('counts',{}).get(key,0) for c in r['classes']) for key in ['total','passed','failed','errors','skipped','not-run']};r['fullInventoryExecuted']=len(classes)==len(r['classes']) and all(c['xmlPresent'] and not c['timedOut'] for c in r['classes']);r['status']='completed-local-observation' if r['fullInventoryExecuted'] else 'incomplete-local-observation'
except BaseException as error:
 r['status']='failed-or-incomplete';r['error']=repr(error);raise
finally:
 r['after']=source_state();r['sourceUnchanged']=before==r['after']
 if outputs is not None:r['outputAfter']=snapshot_composition_test_outputs(dll);r['outputsUnchanged']=outputs==r['outputAfter']
 r['completedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat();r['files']={f.name:{'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size} for f in sorted(out.iterdir()) if f.is_file() and f.name!='receipt.json'};save();print(out,r['status'],r.get('counts'),flush=True)
