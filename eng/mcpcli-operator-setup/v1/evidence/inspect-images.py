"""Read exact released images and inspect metadata/files without starting applications."""
from pathlib import Path
import hashlib
import io
import json
import os
import subprocess
import tarfile
from datetime import datetime, timezone

HERE = Path(__file__).resolve().parent
PRIVATE = Path('/tmp/mcpcli-417-v1-images-20261031')
CANDIDATES = {'eventstore':'registry.hexalith.com/eventstore@sha256:c14074f08cf990072912dd6284fc253a900f6a020a0cd11008195aa357528eed',
              'tenants':'registry.hexalith.com/tenants@sha256:2bc415a61466693bfa7e6c0df1c9aab319b0ef2e82c620897aa9faa1b58f75a5',
              'parties':'registry.hexalith.com/parties@sha256:85afc9e954146883da56edce78e2ddaf401a47b35241bdceed87dfc20217aceb'}


def main():
    PRIVATE.mkdir(mode=0o700)
    records=[]
    for name,reference in CANDIDATES.items():
        record={'name':name,'reference':reference,'observedAt':datetime.now(timezone.utc).isoformat(),'commands':[]}
        for phase,command in [('pull',['docker','pull','--platform','linux/amd64','--quiet',reference]),
                              ('save',['docker','image','save','--output',str(PRIVATE/(name+'.tar')),reference])]:
            result=subprocess.run(command,capture_output=True,timeout=180)
            record['commands'].append({'argv':command,'exitCode':result.returncode})
            if result.returncode:
                record['failurePhase']=phase;records.append(record)
                (HERE/'images.json').write_text(json.dumps(records,indent=2)+'\n')
                raise RuntimeError('Image '+phase+' failed for '+name)
        result=subprocess.run(['docker','image','inspect',reference],capture_output=True,timeout=20,check=True)
        inspected=json.loads(result.stdout)[0];config=inspected.get('Config',{});labels=config.get('Labels') or {}
        record.update(daemonImageId=inspected['Id'],manifestDigest=reference.split('@')[1],architecture=inspected.get('Architecture'),os=inspected.get('Os'),
            entrypoint=config.get('Entrypoint'),command=config.get('Cmd'),user=config.get('User'),workdir=config.get('WorkingDir'),
            environmentKeys=[e.split('=',1)[0] for e in config.get('Env',[])],
            labels={k:v for k,v in labels.items() if k.startswith('org.opencontainers.image.') or k.startswith('com.microsoft.')})
        files={};extract=PRIVATE/name;extract.mkdir(mode=0o700)
        with tarfile.open(PRIVATE/(name+'.tar'),'r:*') as outer:
            if 'manifest.json' not in outer.getnames():raise RuntimeError('Docker save manifest missing')
            image=json.load(outer.extractfile('manifest.json'))[0]
            record['configDigest']='sha256:'+hashlib.sha256(outer.extractfile(image['Config']).read()).hexdigest()
            for layer in image['Layers']:
                with tarfile.open(fileobj=io.BytesIO(outer.extractfile(layer).read()),mode='r:*') as archive:
                    for member in archive.getmembers():
                        path=Path(member.name.lstrip('./'))
                        if not member.isfile() or path.parts[:1]!=('app',) or '..' in path.parts:continue
                        if path.suffix not in ('.dll','.json','.pdb'):continue
                        raw=archive.extractfile(member).read();target=extract/path.relative_to('app');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(raw)
                        files[str(path.relative_to('app'))]=hashlib.sha256(raw).hexdigest()
        record['extractedDirectory']=str(extract);record['files']=files
        deps=list(extract.glob('*.deps.json'))
        record['dependencies']={}
        for dep in deps:
            record['dependencies'].update({k:v.get('type') for k,v in json.loads(dep.read_text()).get('libraries',{}).items() if k.startswith('Hexalith.')})
        markers=['BootstrapGlobalAdminEnabled','BootstrapGlobalAdminUserId','BootstrapGlobalAdmin','CreateTenant','AddUserToTenant','GetGlobalAdministrators',
                 'delivery-writer-protocol/activate','project/v2','EventStoreSidecarChannel','AppChannelToken','RequireEventStoreChannel','IsCanonicalUlid','PartyDetailProjectionQueryActor','MapActorsHandlers']
        record['markers']={marker:[p.name for p in extract.glob('*.dll') if marker.encode() in p.read_bytes() or marker.encode('utf-16le') in p.read_bytes()] for marker in markers}
        records.append(record);(HERE/'images.json').write_text(json.dumps(records,indent=2)+'\n')
        print(json.dumps({'image':name,'configDigest':record['configDigest'],'files':len(files),'entrypoint':record['entrypoint']}),flush=True)


if __name__=='__main__':main()
