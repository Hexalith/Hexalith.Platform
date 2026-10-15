import json
from pathlib import Path
import subprocess

root = Path('/home/administrator/projects/hexalith/platform')
checks = [
    ('architecture-lint', ['python3', '.agents/skills/bmad-architecture/scripts/lint_spine.py', '--workspace', '_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27']),
    ('python-compile', ['python3', '-m', 'py_compile', 'eng/test-local-dapr-security.py', 'eng/verify-platform-pins.py']),
]
checks.append(('diff-check-.', ['git', 'diff', 'HEAD', '--check', '--', '.', ':(exclude)_bmad-output/implementation-artifacts/evidence/', ':(exclude)Hexalith.Platform.slnx', ':(exclude)eng/ext-secrets-1/']))
for name, directory in [('Hexalith.Builds', 'references/Hexalith.Builds'), ('Hexalith.EventStore', 'references/Hexalith.EventStore'), ('Hexalith.Memories', 'references/Hexalith.Memories'), ('Hexalith.Tenants', 'references/Hexalith.Tenants')]:
    checks.append(('diff-check-' + name, ['git', '-C', directory, 'diff', 'HEAD', '--check']))
checks.append(('cleanup', ['docker', 'ps', '--format', '{{.Names}}\t{{.Ports}}']))
for name, command in checks:
    path = Path('/tmp/story1-7-audit-' + name + '.log')
    with path.open('w') as output:
        result = subprocess.run(command, cwd=root, stdout=output, stderr=subprocess.STDOUT)
    print(json.dumps({'check': name, 'command': command, 'exitCode': result.returncode, 'log': str(path)}), flush=True)
    assert result.returncode == 0, path.read_text()
print('ALL FINAL READ-ONLY CHECKS PASSED', flush=True)
