from pathlib import Path
import hashlib,json,subprocess
out=Path('/tmp/ext-protection-loop10-native-fixtures-44662c3e1c3a');verified=[];closures=[]
for result in sorted(out.glob('*.result.json')):
 v=json.loads(result.read_bytes());command=next(c['argv'] for c in v['commands'] if c['stage']=='cleanup-verify');p=subprocess.run(command,capture_output=True)
 absent=p.returncode!=0 and (b'no such object' in p.stderr.lower() or b'no such container' in p.stderr.lower())
 verified.append({'attempt':result.name,'argv':command,'exitCode':p.returncode,'stderr':p.stderr.decode(errors='replace').strip(),'containerAbsent':absent,'harnessCleanup':v['cleanup']})
 if not absent or v['cleanup'].get('complete') is not True or v['cleanup'].get('containerTmpfsUnmounted') is not True:raise SystemExit('Owned cleanup not verified')
for name in ('runtime','runtime-bootstrap-02','runtime-final'):
 directory=out/name;manifest=json.loads((directory/'runtime-manifest.json').read_bytes());entries=manifest['files'];listed={e['path'] for e in entries};observed={str(p.relative_to(directory)) for p in directory.rglob('*') if p.is_file() and p.name!='runtime-manifest.json'}
 if listed!=observed or any(hashlib.sha256((directory/e['path']).read_bytes()).hexdigest()!=e['sha256'] for e in entries):raise SystemExit('Runtime closure changed')
 closures.append({'runtime':str(directory),'filesVerified':len(entries),'manifestSha256':hashlib.sha256((directory/'runtime-manifest.json').read_bytes()).hexdigest(),'completeClosureUnchanged':True})
value={'ownedAttempts':verified,'runtimeClosures':closures};(out/'post-run-audit.json').write_text(json.dumps(value,indent=2)+'\n');print(json.dumps(value))
