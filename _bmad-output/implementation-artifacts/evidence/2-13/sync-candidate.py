"""Refresh the task-owned candidate checkout from final working bytes without another Platform source lane."""
from pathlib import Path
import subprocess,sys,shutil,json,hashlib
source=Path(sys.argv[1]);target=Path(sys.argv[2]);out=Path(sys.argv[3]);previous=subprocess.check_output(['git','rev-parse','HEAD'],cwd=target).decode().strip()
def git(root,*a):return subprocess.check_output(['git','-c','submodule.recurse=false',*a],cwd=root,stderr=subprocess.PIPE)
paths=set(git(source,'diff','--name-only','HEAD').decode().splitlines())
paths.update(p for p in git(source,'ls-files','--others','--exclude-standard','-z').decode().split('\0') if p and not p.startswith('_bmad-output/implementation-artifacts/evidence/'))
# Remove files deleted since an earlier local diagnostic snapshot, including untracked helper renames.
tracked=set(git(target,'ls-files','-z').decode().split('\0'))
for path in tracked:
 if path.startswith(('eng/','tests/Hexalith.Platform.Composition.Tests/')) and path and not (source/path).exists() and (target/path).is_file():
  (target/path).unlink()
for path in paths:
 src=source/path;dest=target/path
 if src.is_file():dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest)
 elif not src.exists() and dest.is_file():dest.unlink()
git(target,'add','-A');git(target,'-c','user.name=Candidate verification','-c','user.email=fixture@invalid','-c','commit.gpgsign=false','commit','-qm','refactor(ci): verify candidate-only tuple retirement')
revision=git(target,'rev-parse','HEAD').decode().strip();assert not git(target,'status','--porcelain').strip()
out.write_text(json.dumps({'previousDiagnosticRevision':previous,'candidateRevision':revision,'candidateTree':git(target,'rev-parse','HEAD^{tree}').decode().strip(),'historicalPlatformInputs':[],'sourceFiles':{p:hashlib.sha256((source/p).read_bytes()).hexdigest() for p in sorted(paths) if (source/p).is_file()},'deletedFiles':[p for p in sorted(paths) if not (source/p).exists()]},indent=2)+'\n')
out.with_suffix('.patch').write_bytes(git(target,'diff','--binary',previous,revision))
print(revision)
