from pathlib import Path
import json,hashlib,shutil,difflib,subprocess
base=Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop5-290f9f5f3d');platform=base/'platform';eventstore=base/'eventstore';evidence=Path('/tmp/ext-protection-loop5-evidence');packet=platform/'docs/implementation/ext-protection-1-loop5';packet.mkdir(parents=True,exist_ok=True)
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
keep=json.loads((base/'keep-inventory.json').read_text());scope={(r['repository'],r['path']) for r in keep if not r['path'].startswith(('docs/','_bmad-output/'))}
for directory in ['src/Hexalith.Platform.Protection','tests/Hexalith.Platform.Protection.Tests','eng/ext-protection-1']:
 for p in (platform/directory).glob('*'):
  rel=p.relative_to(platform)
  if p.is_file() and not p.is_symlink() and p.name.startswith(('ErasureRoot','ProtectionQualification','OpenBao','OwnedErasureRoot','IErasureRoot','DaprErasureRoot')):scope.add(('platform',str(rel)))
for path in ['src/Hexalith.Platform.Custody/IDeletionCapabilityRevocationAuthenticator.cs','src/Hexalith.Platform.Custody/DeletionCapabilityRevocationSubscriber.cs','src/Hexalith.Platform.Secrets.Runtime/DaprCapabilityAdapters.cs']:scope.add(('platform',path))
rows=[];delta=[];patch=[]
for repo,rel in sorted(scope):
 p=base/repo/rel
 if not p.is_file():continue
 original=base/'preserved'/repo/rel
 row={'repository':repo,'path':rel,'sha256':sha(p),'bytes':p.stat().st_size};rows.append(row)
 if original.is_file():
  before=original.read_bytes();previous=sha(original);basis='loop-5 KEEP'
 else:
  baseline='231b0f2b57e96984a919ac1cdce97cc36df18542' if repo=='platform' else 'a62241679854fa9c359c0f552114a42ca0f6a638'
  captured=subprocess.run(['git','show',baseline+':'+rel],cwd=base/repo,capture_output=True,check=False)
  before=captured.stdout if captured.returncode==0 else b'';previous=hashlib.sha256(before).hexdigest() if before else None;basis='Original baseline '+baseline if captured.returncode==0 else 'New in loop 5'
 if before is None or before!=p.read_bytes():
  delta.append(dict(row,priorSha256=previous,comparisonBasis=basis))
  if before is not None:
   try:patch.extend(difflib.unified_diff(before.decode().splitlines(True),p.read_bytes().decode().splitlines(True),fromfile='KEEP/'+repo+'/'+rel,tofile='loop5/'+repo+'/'+rel))
   except UnicodeDecodeError:pass
(packet/'functional-source-inventory.json').write_text(json.dumps(rows,indent=2)+'\n')
(packet/'loop5-delta-inventory.json').write_text(json.dumps(delta,indent=2)+'\n')
(packet/'loop5-keep-delta.patch').write_text(''.join(patch))
verify=packet/'verification-tools';verify.mkdir(exist_ok=True)
for name in ['ext-protection-loop5-regressions.py','ext-protection-loop5-regressions-v6.py','ext-protection-loop5-freeze-v2.py','ext-protection-loop5-freeze-v3.py','ext-protection-loop5-freeze-v4.py','ext-protection-loop5-freeze-v5.py','ext-protection-loop5-freeze-v6.py','ext-protection-loop5-equivalence.py','ext-protection-loop5-freeze-v7.py','ext-protection-loop5-freeze-v8.py','ext-protection-loop5-regressions-v7.py','ext-protection-loop5-regressions-v8.py','ext-protection-loop5-equivalence-v7-v8.py','ext-protection-loop5-preserve-v8.py','ext-protection-loop5-seal-audit.py']:
 p=Path('/tmp')/name
 if p.exists():shutil.copy2(p,verify/name)
comparison=verify/'metadata-compare';comparison.mkdir(exist_ok=True)
for name in ['Compare.csproj','Program.cs']:shutil.copy2(Path('/tmp/ext-protection-loop5-metadata-compare')/name,comparison/name)
# Every raw attempt stays external and is hash-inventoried; the finalized packet copies these exact files after native completion.
raw=[]
for p in sorted(evidence.rglob('*')):
 if p.is_file():raw.append({'path':str(p.relative_to(evidence)),'sha256':sha(p),'bytes':p.stat().st_size})
(packet/'external-attempt-inventory.json').write_text(json.dumps(raw,indent=2)+'\n')
print(json.dumps({'functionalSourceFiles':len(rows),'loop5Deltas':len(delta),'externalAttemptFiles':len(raw)}))
