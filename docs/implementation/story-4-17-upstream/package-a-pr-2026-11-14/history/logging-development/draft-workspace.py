"""Create the disposable source-editing checkout. Invoke with run-with-temp."""
import os
from pathlib import Path
import subprocess
import sys
out = Path(__file__).resolve().parent
source = Path(os.environ['TMPDIR']) / 'components-contrib'
subprocess.run(['git', 'clone', '--depth=1', '--branch=main', '--single-branch', 'https://github.com/dapr/components-contrib.git', str(source)], check=True)
base = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=source, text=True).strip()
if base != '440b15349565bb15f7a88636477864e5a9f8cc72':
 raise SystemExit('HALT: official main moved; use the guarded final workspace before proceeding')
for patch in (out.parent / 'package-a-local-2026-11-14/contribution.patch', out / 'lint-fix.patch'):
 subprocess.run(['git', 'apply', '--whitespace=error-all', str(patch)], cwd=source, check=True)
(out / 'workspace-path.txt').write_text(str(source) + '\n')
print('DRAFT_READY ' + str(source), flush=True)
for line in sys.stdin:
 if line.strip() == 'cleanup':
  break
print('DRAFT_RELEASED', flush=True)
