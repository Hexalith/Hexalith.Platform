from pathlib import Path
import subprocess,json,os,datetime,hashlib,sys,xml.etree.ElementTree as ET
root=Path('/tmp/story32-default-source-mode-probe-_o2d3b9r/repository');sys.path.insert(0,str(root/'eng'))
from prepare_builds_artifacts import source_state
from run_tuple_ci import snapshot_composition_test_outputs
out=Path('/tmp/story32-default-source-mode-final-source48');assert not out.exists();out.mkdir()
assembly=root/'artifacts/final-cold-default-solution/bin/Hexalith.Platform.Composition.Tests/debug/Hexalith.Platform.Composition.Tests.dll'
r={'scope':'Actual plain default Debug/source extension and companion controls on the cold solution output; no global source, SDK, static restore or version overrides; no candidate or signed fixture credit.','source':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'before':source_state(),'outputBefore':snapshot_composition_test_outputs(assembly),'groups':[],'qualificationCredit':False,'candidateAcceptance':False}
env=os.environ.copy();env['NUGET_PACKAGES']='/tmp/story32-default-source-mode-final-builds/private-cache-solution';failed=False
for name,classes,total in [('extensions',['EventStoreExtensionCompositionTests'],36),('companions',['DomainEnrollmentKitTests','GatewayExtensionRegistrationTests','GatewaySeedSubscriptionTests'],12)]:
 args=['dotnet',str(assembly)]+[v for c in classes for v in ['-class','Hexalith.Platform.Composition.Tests.'+c]]+['-result-xml',str(out/(name+'.xml'))]
 with (out/(name+'.stdout')).open('w') as stdout,(out/(name+'.stderr')).open('w') as stderr:c=subprocess.run(args,cwd=root,env=env,stdout=stdout,stderr=stderr,timeout=1200)
 counts=ET.parse(out/(name+'.xml')).getroot().find('assembly').attrib;okay=c.returncode==0 and int(counts['total'])==total and int(counts['passed'])==total and all(int(counts.get(k,'0'))==0 for k in ['failed','errors','skipped','not-run']);r['groups'].append({'name':name,'command':args,'exitCode':c.returncode,'expected':total,'counts':counts,'passed':okay});failed|=not okay;print(name,counts,flush=True)
 (out/'receipt.json').write_text(json.dumps(r,indent=2)+'\n')
r['after']=source_state();r['outputAfter']=snapshot_composition_test_outputs(assembly);r['sourceUnchanged']=r['before']==r['after'];r['outputsUnchanged']=r['outputBefore']==r['outputAfter'];r['status']='passed' if not failed and r['sourceUnchanged'] and r['outputsUnchanged'] else 'failed';r['files']={f.name:{'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size} for f in sorted(out.iterdir()) if f.is_file() and f.name!='receipt.json'};(out/'receipt.json').write_text(json.dumps(r,indent=2)+'\n');print(out,r['status'],flush=True);raise SystemExit(0 if r['status']=='passed' else 1)
