"""Write the bundle artifact inventory and re-verify that no pre-existing loop-10 evidence byte changed. Usage: python3 -I finalize.py"""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

PLATFORM = Path('/home/administrator/projects/hexalith/platform')
LOOP10 = PLATFORM / 'docs/implementation/ext-protection-1-loop10'
BUNDLE = LOOP10 / 'current-graph-314ddb04c56c'
SCRIPTS = Path('/tmp/claude-1000/-home-administrator-projects-hexalith-platform/b949fb33-be78-47f5-84d2-3974588e5fdf/scratchpad/scripts-314ddb04c56c')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


shutil.copyfile(SCRIPTS / 'finalize.py', BUNDLE / 'finalize.py')
existing = [p for p in sorted(LOOP10.rglob('*')) if p.is_file() and not p.is_relative_to(BUNDLE)]
preservation = json.loads((BUNDLE / 'evidence-preservation.json').read_text())
assert len(existing) == preservation['existingFilesVerifiedUnchanged'], (len(existing), preservation)
tracked = subprocess.run(['git', '--no-optional-locks', '-C', str(PLATFORM), 'diff', '--quiet', 'HEAD', '--', str(LOOP10.relative_to(PLATFORM))])
assert tracked.returncode == 0, 'tracked loop-10 evidence differs from HEAD'
artifacts = [{'path': str(p.relative_to(BUNDLE)), 'bytes': p.stat().st_size, 'sha256': digest(p)}
             for p in sorted(BUNDLE.rglob('*')) if p.is_file() and p.name != 'artifact-inventory.json']
(BUNDLE / 'artifact-inventory.json').write_text(json.dumps(artifacts, indent=2) + '\n')
print(json.dumps({'bundleArtifacts': len(artifacts), 'existingFilesOutsideBundle': len(existing), 'trackedEvidenceMatchesHead': True}))
