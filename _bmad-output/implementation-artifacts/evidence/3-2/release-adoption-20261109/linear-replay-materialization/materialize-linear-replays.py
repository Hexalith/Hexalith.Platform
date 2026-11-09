"""Materialize actual linear L/N/M metadata only after the finalized code/evidence commit."""
from pathlib import Path
if not __debug__:
 raise RuntimeError("This preservation assembler refuses Python optimization.")
import argparse,hashlib,json,subprocess,datetime
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);a=ap.parse_args();root=a.root.resolve();out=a.output.resolve();assert not out.exists();assert root not in out.parents and out != root;assert not subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True).strip()
def git(*args):return subprocess.check_output(['git',*args],cwd=root)
def raw(source,path):return git('show',source+':'+path)
base=git('rev-parse','HEAD').decode().strip();branch=git('branch','--show-current').decode().strip();assert branch
replay_refs={label:'docs/story-3-2-replay-'+label.lower()+'-20261109' for label in ['L','N']}
for ref in replay_refs.values():
 assert subprocess.run(['git','show-ref','--verify','--quiet','refs/heads/'+ref],cwd=root).returncode==1, 'Replay ref already exists: '+ref
for name in ['Builds','EventStore']:
 assert not subprocess.check_output(['git','status','--porcelain'],cwd=root/'references'/('Hexalith.'+name),text=True).strip()
for profile,count in [('eng/tuple-ci-sdk42-current-suites.json',456),('eng/tuple-ci-sdk42-base-suites.json',97),('eng/tuple-ci-extension-suites.json',9)]:
 assert sum(g['requiredCount'] for g in json.loads(raw(base,profile))['groups'])==count
assert hashlib.sha256(raw(base,'eng/tuple-ci-sdk42-current-suites.json')).digest()==hashlib.sha256(raw('081178aa5cc01f5dc736ba0d1980aa80fedc6fe1','eng/tuple-ci-suites.json')).digest()
out.mkdir(parents=True)
files=['.config/dotnet-tools.json','hexalith-platform.json','eng/builds-tuple.json','eng/tuple-ci-suites.json','src/Hexalith.Platform.Composition/Declarations/provenance.json'];current={f:raw(base,f) for f in files};old='d4c4b46916c773ebe16190100bffffa2db31f219';pre='27a1638e0e7d66b672708507d95a44fe31050718';legacy={f:raw(old,f) for f in files};previous={f:raw(pre,f) for f in files}
assert hashlib.sha256(legacy['eng/tuple-ci-suites.json']).hexdigest()=='61689f1598dac43a9fa28dcf5319008059e0fea0f1018209b1a8e79e4f63d7b0';assert hashlib.sha256(previous['eng/tuple-ci-suites.json']).hexdigest()=='3643ab2f60f255bd0d0b9a91452e1f7060b5f5b69bf0fef0a51cf8336a526c6c';assert hashlib.sha256(legacy[files[-1]]).hexdigest()=='3e9b04109c5ddb34a0ac6801f5cdb36e1cbc102c65bfccb54b77c4d408091a61';assert hashlib.sha256(previous[files[-1]]).hexdigest()=='3931a6ff47adf0edd4b6d19430294f6ebed2c334b50655528e751eee049fc846'
assert json.loads(current['eng/builds-tuple.json'])['publication']['version']=='4.47.0';assert git('rev-parse',base+':references/Hexalith.Parties').decode().strip()=='ebe1739d70a4e76447938b0bb8c53db508bf6019'
report={'recordedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'preparationSource':base,'branch':branch,'partiesSource':'ebe1739d70a4e76447938b0bb8c53db508bf6019','assemblerSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'requiredCurrentExecutions':562,'requiredUniqueCurrentCases':559,'historicalRequiredCounts':{'L':349,'N':412},'requiredHostedJobs':{'tuple':5,'catalog':6},'phases':[],'qualificationCredit':False}
def checkpoint(): (out/'linear-source-receipt.json').write_text(json.dumps(report,indent=2)+'\n')
def select(name,metadata,builds,eventstore,legacy_source=None,previous_source=None):
 for f,b in metadata.items():(root/f).write_bytes(b)
 for name,source in [('Builds',builds),('EventStore',eventstore)]:
  subprocess.run(['git','checkout','--detach',source],cwd=root/'references'/('Hexalith.'+name),check=True)
 for path,value in [('eng/legacy-tuple-source.json',legacy_source),('eng/pre-extension-tuple-source.json',previous_source)]:
  if value:
   d=json.loads(raw(base,path));d['sourceRevision']=value;(root/path).write_text(json.dumps(d,indent=2)+'\n')
 subprocess.run(['git','add','--',*files,'references/Hexalith.Builds','references/Hexalith.EventStore','eng/legacy-tuple-source.json','eng/pre-extension-tuple-source.json'],cwd=root,check=True)
def record(label,message):
 m=out/(label+'-commit.txt');m.write_text(message+'\n');subprocess.run(['node','node_modules/@commitlint/cli/cli.js','--edit',str(m)],cwd=root,check=True);subprocess.run(['git','commit','--file',str(m)],cwd=root,check=True);source=git('rev-parse','HEAD').decode().strip();report['phases'].append({'label':label,'source':source});checkpoint();return source
checkpoint()
select('L',legacy,'fc2122240a39f03628c04fa8433e5eece90b8b4c','cab8363c2b6e2c0588efc8ca9c2e09fab0006841');L=record('L','build(tuple): record the same-code legacy source')
select('N',previous,'27145e52ffab5623198ba5efdab5cdb71aa42d28','cab8363c2b6e2c0588efc8ca9c2e09fab0006841',legacy_source=L);N=record('N','build(tuple): record the same-code pre-extension source')
select('M',current,'5ec1779040606be89306129a7d2b245258c9acd2','aa16227bd07f0564a3026d619a5abd973e02656f',legacy_source=L,previous_source=N);M=record('M','build(tuple): select the measured current qualification source')
for label,source in [('L',L),('N',N)]:
 name=replay_refs[label];subprocess.run(['git','branch',name,source],cwd=root,check=True);report[label+'Ref']=name
allowed=set(files+['eng/legacy-tuple-source.json','eng/pre-extension-tuple-source.json','references/Hexalith.Builds','references/Hexalith.EventStore'])
def code_tree(source):
 return b'\0'.join(row for row in git('ls-tree','-r','-z',source).split(b'\0') if row and row.split(b'\t',1)[1].decode() not in allowed)
for source in [L,N,M]:
 assert code_tree(source)==code_tree(base), 'Non-metadata code/test/helper/docs/gitlink tree changed.'
 assert git('rev-parse',source+':references/Hexalith.Parties').decode().strip()==report['partiesSource']
report['nonMetadataTreeSha256']=hashlib.sha256(code_tree(base)).hexdigest();report['nonMetadataTreesEqual']=True
assert git('rev-parse',M+'^').decode().strip()==N and git('rev-parse',N+'^').decode().strip()==L;assert not git('status','--porcelain').decode().strip();report.update(L=L,N=N,M=M,status='materialized-real-linear-sources-awaiting-actual-checkout-verification');checkpoint();print(json.dumps(report,indent=2))
