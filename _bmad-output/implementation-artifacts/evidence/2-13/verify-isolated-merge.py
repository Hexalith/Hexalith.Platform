"""Merge a single current candidate in a disposable repository and prove pins/tags do not move."""
from pathlib import Path
import subprocess,sys,hashlib,shutil,json,os
source=Path.cwd();target=Path(os.environ['TMPDIR'])/'merge';out=Path(sys.argv[1]).resolve()
def git(root,*a):return subprocess.check_output(['git','-c','submodule.recurse=false',*a],cwd=root,stderr=subprocess.PIPE)
base=git(source,'rev-parse','HEAD').decode().strip()
git(source,'clone','--shared','--no-checkout',str(source),str(target));git(target,'checkout','--detach',base)
subprocess.run(['git','apply','--binary','--whitespace=nowarn','-'],cwd=target,input=git(source,'diff','--binary','HEAD'),check=True,capture_output=True)
for path in git(source,'ls-files','--others','--exclude-standard','-z').decode().split('\0'):
 if not path or path.startswith('_bmad-output/implementation-artifacts/evidence/'):continue
 src=source/path
 if src.is_file():
  dst=target/path;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst)
git(target,'add','-A');git(target,'-c','user.name=Candidate merge verification','-c','user.email=fixture@invalid','-c','commit.gpgsign=false','commit','-qm','refactor(ci): retire historical tuple replay')
candidate=git(target,'rev-parse','HEAD').decode().strip();tree=git(target,'rev-parse','HEAD^{tree}').decode().strip()
git(target,'checkout','--detach',base)
files=['eng/builds-tuple.json','eng/current-tuple-source.json','.config/dotnet-tools.json','hexalith-platform.json','src/Hexalith.Platform.Composition/Declarations/provenance.json','apphost.cs']
def inventory():
 return {'files':{p:hashlib.sha256((target/p).read_bytes()).hexdigest() for p in files},'gitlinks':git(target,'ls-tree','HEAD:references').decode().splitlines(),'tags':git(target,'show-ref','--tags').decode().splitlines()}
before=inventory();git(target,'-c','user.name=Candidate merge verification','-c','user.email=fixture@invalid','-c','commit.gpgsign=false','merge','--no-ff',candidate,'-m','Merge candidate-only tuple CI retirement');after=inventory()
assert before==after
assert git(source,'for-each-ref','--format=%(refname) %(objectname)','refs/heads/qualification/').strip()==b''
out.write_text(json.dumps({'baseline':base,'diagnosticCandidate':candidate,'candidateTree':tree,'isolatedMerge':git(target,'rev-parse','HEAD').decode().strip(),'before':before,'after':after,'pinsUnchanged':True,'tagsUnchanged':True,'archivedQualificationTagCount':sum('refs/tags/archive/qualification' in t for t in before['tags']),'brokerTagCount':sum('refs/tags/qualification/broker-' in t for t in before['tags']),'localQualificationBranches':[],'remoteOperations':[],'pushallInvoked':False},indent=2)+'\n')
