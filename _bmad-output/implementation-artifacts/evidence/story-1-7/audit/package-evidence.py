import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess

root = Path('/home/administrator/projects/hexalith/platform')
evidence = root / '_bmad-output/implementation-artifacts/evidence/story-1-7'
audit = evidence / 'audit'
audit.mkdir(exist_ok=True)

logs = ['build', 'conflict-build', 'conflict-tests', 'cli-package-tests', 'platform-tests', 'builds-build', 'builds-tests', 'declarations', 'security-live', 'builds-tenants-live', 'builds-tenants-apphost', 'architecture-lint', 'python-compile', 'cleanup', 'global-whitespace-check', 'final-checks']
for name in logs:
    shutil.copyfile(Path('/tmp/story1-7-audit-' + name + '.log'), audit / (name + '.txt'))
for directory in ['.', 'Hexalith.Builds', 'Hexalith.EventStore', 'Hexalith.Memories', 'Hexalith.Tenants']:
    name = 'diff-check-' + directory
    shutil.copyfile(Path('/tmp/story1-7-audit-' + name + '.log'), audit / (name + '.txt'))
shutil.copyfile('/tmp/story1-7-cli-subscriptions.json', audit / 'cli-subscriptions.json')
shutil.copyfile('/tmp/story1-7-dapr-api.log', audit / 'dapr-resources-path-api.txt')
shutil.copyfile('/tmp/story1-7-audit-verification.py', audit / 'attempted-sequence.py')
shutil.copyfile('/tmp/story1-7-audit-driver.log', audit / 'attempted-sequence.txt')
shutil.copyfile('/tmp/story1-7-audit-final-checks.py', audit / 'final-checks.py')
for source in Path('/tmp/story1-7-audit-security-evidence').rglob('*'):
    if source.is_file():
        relative = source.relative_to('/tmp/story1-7-audit-security-evidence')
        if relative.suffix == '.log':
            relative = relative.with_suffix('.txt')
        elif relative.name == 'redis-port':
            relative = relative.with_name('redis-port.txt')
        target = audit / 'security' / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)

def statistics(name):
    content = (audit / (name + '.txt')).read_text()
    match = re.search(r'Total:\s+(\d+), Errors:\s+(\d+), Failed:\s+(\d+), Skipped:\s+(\d+), Not Run:\s+(\d+), Time:\s+([\d.]+)s', content)
    assert match, name
    total, errors, failed, skipped, not_run = map(int, match.groups()[:5])
    assert errors == failed == not_run == 0, name
    return dict(total=total, passed=total-failed-skipped-not_run, failed=failed, skipped=skipped, seconds=float(match[6]))

current = {name: statistics(name) for name in ['cli-package-tests', 'platform-tests', 'conflict-tests', 'builds-tests', 'security-live', 'builds-tenants-live']}
assert current['platform-tests']['total'] == 118
assert current['builds-tests']['passed'] == 138
assert current['security-live']['passed'] == current['builds-tenants-live']['passed'] == 1
assert sum(line.startswith('PASS') for line in (audit / 'declarations.txt').read_text().splitlines()) == 41
baseline = evidence / 'pre-audit-results.json'
if not baseline.exists():
    shutil.copyfile(evidence / 'results.json', baseline)
old = json.loads(baseline.read_text())
reused = ['domainservice-tests', 'aspire-owner-tests', 'server-owner-tests', 'memories-eventstore-tests', 'memories-server-tests', 'tenants-owner-tests']
result = {
    'date': '2026-10-15',
    'phase': 'post-parent-audit focused verification',
    'tests': current,
    'declarationChecks': 41,
    'reusedOwnerRegressionResults': {name: old['tests'][name] for name in reused},
    'reuseProvenance': 'Initial handoff logs and pre-audit-results.json; affected owner source/test implementations unchanged by parent-audit corrections. Current owner manifests and Builds validator were separately rebuilt/validated.',
    'securityLaunch': 'Direct pinned daprd 1.18.2, fixture-only allocated Sentry transport; not Dapr CLI launch. Original/adapted resources, hashes, runtime path and arguments retained.',
    'cliResourceEvidence': 'Actual Aspire lifecycle-generated CLI arguments and scoped Subscription contents asserted by executed in-process test; separate from runtime fixture.',
    'projectionObservation': json.loads((audit / 'security/projection-observation.json').read_text()),
    'sourceSmoke': 'Current-code six-server real Builds source smoke; requires final readiness and owned workspace/scheduler-data cleanup assertions.',
    'otherSourceSmokes': 'Initial successful technical EventStore and sequential-prebuild file-entry results retained as pre-audit provenance; not rerun after parent audit.',
    'testOnlyFollowup': 'After the 118-test ordinary suite, two conflict cases were added and the complete 12-case invalid-binding theory executed. Production code unchanged; ordinary suite not repeated.',
    'whitespaceBoundary': 'Root Story source/docs excludes retained raw evidence and concurrent unrelated slnx/ext-secrets edits. Global whitespace refusal retained separately; no raw bytes or unrelated files rewritten.',
}
(evidence / 'results.json').write_text(json.dumps(result, indent=2) + '\n')

old_hashes = evidence / 'pre-audit-changed-files.sha256'
if not old_hashes.exists():
    shutil.copyfile(evidence / 'changed-files.sha256', old_hashes)
exclude = ('Hexalith.Platform.slnx', 'eng/ext-secrets-1/')
files = []
for repository in ['.', 'references/Hexalith.Builds', 'references/Hexalith.EventStore', 'references/Hexalith.Memories', 'references/Hexalith.Tenants']:
    names = subprocess.check_output(['git', '-C', str(root / repository), 'diff', 'HEAD', '--name-only', '--diff-filter=ACMRT'], text=True).splitlines()
    for name in names:
        if repository == '.' and (name.startswith('_bmad-output/implementation-artifacts/evidence/') or any(name == item or name.startswith(item) for item in exclude)):
            continue
        path = root / repository / name
        if path.is_file():
            files.append(path)
(evidence / 'changed-files.sha256').write_text(''.join(hashlib.sha256(path.read_bytes()).hexdigest() + '  ' + str(path.relative_to(root)) + '\n' for path in sorted(set(files))))
shutil.copyfile(__file__, audit / 'package-evidence.py')
print(json.dumps(result, indent=2))
