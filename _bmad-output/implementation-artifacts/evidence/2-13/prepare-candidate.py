"""Create one clean local candidate snapshot and direct owner copies; never select a historical Platform input."""
from pathlib import Path
import subprocess,sys,shutil,json
source=Path(sys.argv[1]);target=Path(sys.argv[2]);out=Path(sys.argv[3])
def git(root,*args):return subprocess.check_output(['git','-c','submodule.recurse=false',*args],cwd=root,stderr=subprocess.PIPE)
git(source,'clone','--shared','--no-checkout',str(source),str(target))
git(target,'checkout','--detach',git(source,'rev-parse','HEAD').decode().strip())
patch=git(source,'diff','--binary','HEAD')
subprocess.run(['git','apply','--binary','--whitespace=nowarn','-'],cwd=target,input=patch,check=True,capture_output=True)
for path in git(source,'ls-files','--others','--exclude-standard','-z').decode().split('\0'):
 if not path or path.startswith('_bmad-output/implementation-artifacts/evidence/'):continue
 src=source/path
 if src.is_file():
  dest=target/path;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dest)
# Clone only direct gitlinks; no nested submodule initialization or network fetch.
for line in git(source,'ls-tree','HEAD:references').decode().splitlines():
 metadata,name=line.split('\t');mode,kind,revision=metadata.split()
 if mode!='160000':continue
 owner=source/'references'/name;dest=target/'references'/name
 git(source,'clone','--shared','--no-checkout',str(owner),str(dest));git(dest,'checkout','--detach',revision)
git(target,'add','-A')
git(target,'-c','user.name=Candidate verification','-c','user.email=fixture@invalid','-c','commit.gpgsign=false','commit','-qm','refactor(ci): retire historical tuple replay')
revision=git(target,'rev-parse','HEAD').decode().strip()
assert not git(target,'status','--porcelain').strip()
out.write_text(json.dumps({'sourceRevision':git(source,'rev-parse','HEAD').decode().strip(),'diagnosticCandidateRevision':revision,'candidateTree':git(target,'rev-parse','HEAD^{tree}').decode().strip(),'historicalPlatformInputs':[],'directOwnersOnly':True},indent=2)+'\n')
print(target)
