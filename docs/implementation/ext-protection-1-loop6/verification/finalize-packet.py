import pathlib,json,hashlib,shutil,subprocess,datetime,xml.etree.ElementTree as ET,re
p=pathlib.Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop6-07bd8b0dea/platform');e=p.parent/'eventstore';r=pathlib.Path('/tmp/ext-protection-loop6-evidence');out=p/'docs/implementation/ext-protection-1-loop6';native=json.loads((r/'native-final-02.json').read_bytes());assert native['outcome']=='NativeCasesPassed' and native['cleanup']['complete']
assert native['nativeTestSummary']=={'total':5,'errors':0,'failed':0,'skipped':0,'notRun':0}
h=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
prior=json.loads((p/'docs/implementation/ext-protection-1-loop5/functional-source-inventory.json').read_bytes());selected={(v['repository'],v['path']) for v in prior};delta=[]
for name,base in [('platform',p),('eventstore',e)]:
 paths=set(subprocess.check_output(['git','diff','--name-only','HEAD'],cwd=base,text=True).splitlines())|set(subprocess.check_output(['git','ls-files','--others','--exclude-standard'],cwd=base,text=True).splitlines())
 for rel in sorted(paths):
  f=base/rel;k=p.parent/'preserved'/name/rel
  if not rel.startswith(('src/','tests/','eng/','deploy/','tools/')) or not f.is_file() or f.is_symlink() or '__pycache__' in f.parts:continue
  old=k.read_bytes() if k.is_file() else None
  if old!=f.read_bytes():delta.append({'repository':name,'path':rel,'sha256':h(f),'bytes':f.stat().st_size,'keepSha256':hashlib.sha256(old).hexdigest() if old is not None else None});selected.add((name,rel))
functional=[{'repository':name,'path':rel,'sha256':h((p if name=='platform' else e)/rel),'bytes':((p if name=='platform' else e)/rel).stat().st_size} for name,rel in sorted(selected) if ((p if name=='platform' else e)/rel).is_file()]
(out/'functional-source-inventory.json').write_text(json.dumps(functional,indent=2));(out/'loop6-delta-inventory.json').write_text(json.dumps(delta,indent=2))
x=json.loads((out/'verification.json').read_bytes());x.update(updatedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),nativeStatus='NativeCasesPassed',nativeEvidence='verification/native-final-02.json',nativeFreeze=json.loads((r/'final-freeze-07.json').read_bytes()),nativeSummary=native['nativeTestSummary'],nativeSeconds=native['nativeSuiteElapsedSeconds'],nativeGrowthGeometry=native['nativeGrowthGeometry'],nativeOccurrenceGeometry=native['nativeOccurrenceGeometry'],nativeCleanup=native['cleanup'],qualificationFreeze=json.loads((r/'final-freeze-04.json').read_bytes()),daprFreeze=json.loads((r/'final-freeze-05.json').read_bytes()),sourceEquivalence='verification/final-native-07-qualification-equivalence.json',functionalSourceFiles=len(functional),loop6DeltaFiles=len(delta))
x['dapr']['evidence']='verification/dapr-final-05.json';x['bootstrap']['evidence']='verification/bootstrap-final-02.json';x['bootstrap']['seconds']=json.loads((r/'bootstrap-final-02.json').read_bytes())['nativeSuiteElapsedSeconds'];x['retainedNativeFailures']=[{'path':'verification/native-final-01.json','outcome':'BlockedUnknown','failure':'BoundedNativeComponentSuiteUnavailable','timeoutSeconds':3600,'cleanup':json.loads((r/'native-final-01.json').read_bytes())['cleanup']}]
commands=x['commands'];d=next(c for c in commands if c['name'].startswith('dapr-final-'));d['name']='dapr-final-05';d['argv'][d['argv'].index('--assembly')+1]=x['daprFreeze']['root']+'/Hexalith.Platform.Protection.Tests.dll';d['argv'][-1]=str(r/'dapr-final-05.json');d['log']=str(r/'dapr-final-05.log');d['sha256']=h(pathlib.Path(d['log']))
for label,extra,result in [('native-final-02',[],'native-final-02.json'),('bootstrap-final-02',['--bootstrap'],'bootstrap-final-02.json'),('native-channel-precheck-02',['--diagnostic'],'native-channel-precheck-02.json')]:
 pin=x['nativeFreeze'];argv=['python3','eng/ext-protection-1/probe_erc_native.py']+extra+['--test-assembly',pin['root']+'/Hexalith.Platform.Protection.Tests.dll','--test-assembly-sha256',pin['assemblySha256'],'--runtime-manifest',pin['manifest'],'--runtime-manifest-sha256',pin['manifestSha256'],'--output',str(r/result)]
 commands.append({'name':label,'cwd':str(p),'argv':argv,'exitCode':0,'log':str(r/(label+'.log')),'sha256':h(r/(label+'.log'))})
# Retain the successful broad run's actual runtime pin; native-only test transport changes were separately executed.
x['finalNativeTransportBuild']='verification/build-36-native-channel-assertions.log';x['qualificationRuntimeSourceLimit']='Complete510 test run used graph04-matched runtime; later native-only test transport/diagnostic changes and Dapr-only hash assertions have separate exact executed graphs. All production source files are byte-identical.'
(out/'verification.json').write_text(json.dumps(x,indent=2));(out/'final-commands.json').write_text(json.dumps(commands,indent=2))
readme=(out/'README.md').read_text();a=readme.index('Native-final-01 froze before');b=readme.index('\n\n## Missing installed inputs',a)
readme=readme[:a]+f'''Native-final-02 passes all five cases with zero failures/errors/skips in {native['nativeSuiteElapsedSeconds']} seconds. It executes the unchanged full 1,000 targets, actual 9 MiB state, >8 MiB native head, both history geometries, 256 native owners/occurrences and all shipped restricted-policy denials. Container absence and tmpfs unmount are verified. The exact native geometry is recorded in `verification.json` and the original native result.

Native-final-01 exhausted its unchanged 3,600-second bound and remains BlockedUnknown with verified cleanup. Its timeout, earlier failures and all frozen graphs remain unchanged. The test-only throughput correction keeps one owned docker-exec stream while each HTTP request still executes the real bao API command under that request's stdin-only credential. Request/response framing preserves separate stdout/stderr and native status; cancellation breaks the channel without reconnect/redispatch. The precheck executes real success, all eight shipped ACL denials, wrong-context error, absence and invalid-token denial followed by valid-token success. No HOME assignment or token file is introduced. Per-request serialization still permits caller-level multi-request CAS interleaving; the unchanged native pin race executes in the full suite.

The final native graph is07, broad qualification runtime04 and exact-state Dapr runtime05. All production sources are byte-identical across those graphs. Differences are confined to the three native transport/diagnostic test files and Dapr test assertions, explicitly inventoried. No whole-binary equality is claimed. Fresh shipped-bootstrap02 passes on07 with verified cleanup. A flat frozen-runtime whole-suite attempt exposed five existing repository-relative Python helper discovery failures; it is retained. The final full510 run uses the required repository layout after all257 runtime files were verified byte-identical to graph04.
''' +readme[b:];(out/'README.md').write_text(readme)
notice=f'''\n\n### Loop-6 isolated local implementation evidence — 2026-10-30\n\nThe [loop-6 handback](../../docs/implementation/ext-protection-1-loop6/README.md) records all twelve authorized corrections, exact source/runtime overlays and retained failures. Final local checks pass Protection510 total/502 executed/eight explicit owned-integration skips, EventStore444, payload898, contracts14, lifecycle42/revocation26, full Secrets build/185 affected tests, offline116 without skips, harness13 and Source41/liveExecuted:false; required Debug/project-reference/-nodeReuse:false host/three-role/analyzer builds are clean. Fresh native-final-02 passes all five unchanged cases in {native['nativeSuiteElapsedSeconds']}s with complete1000 targets, actual9MiB state, >8MiB head, history/index geometry, shipped restricted policies and verified container absence/tmpfs unmount. Separate actual Dapr four-phase exact9/32MiB/legacy/snapshot-loss coverage, shipped Python bootstrap and native transport success/denial/error prechecks pass with owned cleanup. The original3600s native-final-01 failure remains BlockedUnknown, never relabelled. Status stays in-progress for parent audit/review; original baselines/frozen intent/register are unchanged. No immutable candidate/date/profile/availability is proposed. Installed41/full physical-copy qualification remain external and destruction independently gated. No Live/OQ/Agents, cluster/provisioning, production data or Git mutation/remote operation occurred.\n'''
spec=p/'_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md';old=spec.read_bytes();start=old.index(b'<frozen-after-approval');end=old.index(b'</frozen-after-approval>')+len(b'</frozen-after-approval>');assert hashlib.sha256(old[start:end]).hexdigest()=='87282445c06d0588eed81ca970ec21f961a596ba2b2b8fc61d77c32d2c244940';assert '### Loop-6 isolated local implementation evidence' not in spec.read_text();spec.write_bytes(old+notice.encode())
owner=e/'docs/implementation/ext-protection-1-owner-acceptance.md';owner_notice=notice.replace('[loop-6 handback](../../docs/implementation/ext-protection-1-loop6/README.md)','[loop-6 handback](../../../platform/docs/implementation/ext-protection-1-loop6/README.md)');assert '### Loop-6 isolated local implementation evidence' not in owner.read_text();owner.write_bytes(owner.read_bytes()+owner_notice.encode())
# Exact retained attempt inventory: raw artifacts remain authoritative, including all failures.
entries=[]
for f in sorted(r.rglob('*')):
 if not f.is_file():continue
 v={'path':str(f.relative_to(r)),'bytes':f.stat().st_size,'sha256':h(f)}
 if f.suffix=='.log':
  t=f.read_text(errors='replace');match=re.search(r'Total:\s*(\d+),\s*Errors:\s*(\d+),\s*Failed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Not Run:\s*(\d+)',t)
  if match:v['xunitCounts']=list(map(int,match.groups()))
  if 'Build succeeded.' in t:v['build']='Passed'
  elif 'Build FAILED.' in t:v['build']='Failed'
 if f.suffix=='.json':
  try:
   z=json.loads(f.read_bytes())
   if isinstance(z,dict) and 'outcome' in z:v['outcome']=z['outcome']
  except ValueError:pass
 entries.append(v)
(out/'external-attempt-inventory.json').write_text(json.dumps(entries,indent=2))
proof=out/'verification';proof.mkdir(exist_ok=True)
for f in sorted(r.rglob('*')):
 if f.is_file():
  dest=proof/f.relative_to(r);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dest);assert h(f)==h(dest)
artifacts=[{'path':str(f.relative_to(out)),'bytes':f.stat().st_size,'sha256':h(f)} for f in sorted(out.rglob('*')) if f.is_file() and f.name not in ['artifact-inventory.json','SHA256SUMS']];(out/'artifact-inventory.json').write_text(json.dumps(artifacts,indent=2));(out/'SHA256SUMS').write_text(''.join(v['sha256']+'  '+v['path']+'\n' for v in artifacts))
mirrors=json.loads((r/'persistent-mirrors.json').read_bytes());dst=pathlib.Path(mirrors['root'])/'evidence';shutil.copytree(r,dst);packet=pathlib.Path(mirrors['root'])/'packet';shutil.copytree(out,packet)
for base,copy in [(r,dst),(out,packet)]:
 for f in base.rglob('*'):
  if f.is_file():assert h(f)==h(copy/f.relative_to(base))
print(json.dumps({'status':'in-progress','native':native['outcome'],'functionalFiles':len(functional),'deltaFiles':len(delta),'packetArtifacts':len(artifacts),'persistentRoot':mirrors['root']}))
