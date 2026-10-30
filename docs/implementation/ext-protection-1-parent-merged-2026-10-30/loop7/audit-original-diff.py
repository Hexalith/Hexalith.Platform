from pathlib import Path
import json,re,hashlib,subprocess
sp=Path('/tmp/bmad-protection-audit-scope.json');s=json.loads(sp.read_text());r=s['rederive7'];diff=Path(s['diff']);data=diff.read_bytes();repo=None;entries=[];positions=[]
for match in re.finditer(rb'^Repository: ([^\n]+)\n|^diff --git (.+)\n',data,re.M):
 if match[1]:repo=match[1].decode()
 else:
  header=match[2].decode();path=header.split(' b/',1)[1].strip('"');positions.append((match.start(),repo,path))
for n,(start,repo,path) in enumerate(positions):
 end=positions[n+1][0] if n+1<len(positions) else len(data);entries.append(dict(repository=repo,path=path,bytes=end-start,sha256=hashlib.sha256(data[start:end]).hexdigest()))
candidates=json.loads(Path('/tmp/ext-protection-loop7-source-candidates.json').read_text());known={(s['roots'][repo],x['path']) for repo,v in candidates.items() for x in v['sourceFiles']};functional=[x for x in entries if x['path'].startswith(('src/','tests/','tools/','eng/','deploy/')) or x['path'] in ['.gitattributes','Hexalith.Platform.slnx','docs/implementation/ext-protection-1-verifier-protocol.md']];unknown=[x for x in functional if (x['repository'],x['path']) not in known];assert not unknown,unknown
checks={}
for repo,v in candidates.items():
 root=Path(s['roots'][repo])
 for x in v['sourceFiles']:
  actual=subprocess.check_output(['git','hash-object','--path='+x['path'],'--stdin'],cwd=root,input=(root/x['path']).read_bytes()).decode().strip();assert actual==x['blob'],(repo,x['path'])
 argv=['git','-c','core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol','diff','--check',s['baselines'][repo],'--',*s['paths'][repo]];result=subprocess.run(argv,cwd=root,capture_output=True,text=True);assert result.returncode==0,(repo,result.stdout,result.stderr)
 checks[repo]=dict(candidateSourceFiles=len(v['sourceFiles']),baselineDiffCheckExitCode=result.returncode,argv=argv,stdout=result.stdout,stderr=result.stderr)
spec=Path(s['roots']['platform'])/'_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md';text=spec.read_text();tasks=text[text.index('## Tasks & Acceptance'):text.index('## Implementation Notes')];assert tasks.count('- [x]')==7 and '- [ ]' not in tasks
block=re.search(r'<frozen-after-approval[\s\S]*?</frozen-after-approval>',text).group();assert hashlib.sha256(block.encode()).hexdigest()==r['frozenSha256']
audit=dict(diff=str(diff),sha256=hashlib.sha256(data).hexdigest(),bytes=len(data),originalBaselines=s['baselines'],diffBlocks=len(entries),functionalBlocks=len(functional),unexpectedFunctionalBlocks=unknown,allCandidateSourceBlobsStillMatch=True,checks=checks,whitespacePolicy='The initial default check treated required CRLF as trailing whitespace in existing Custody files. The final check recognizes CR as line-ending while retaining ordinary space/tab/blank-line checks, matching the repository source policy and child check; no executed source bytes changed.',taskCount=7,frozenUnchanged=True,functionalReadEvidence=r['parentFunctionalDeltaComplete'],parentProtocolDelta=r['parentProtocolDocumentation'],metadataOnlyAttributes=r['parentMetadataOnlyDelta'],untrackedIncluded=r['originalBaselineDiff']['repositories'],blocks=entries)
Path('/tmp/ext-protection-loop7-original-diff-audit.json').write_text(json.dumps(audit,indent=2)+'\n');r['parentOriginalDiffAudit']='/tmp/ext-protection-loop7-original-diff-audit.json';sp.write_text(json.dumps(s,indent=2)+'\n');print(json.dumps({k:v for k,v in audit.items() if k not in ['blocks','functionalReadEvidence','parentProtocolDelta','metadataOnlyAttributes','checks']},indent=2))
