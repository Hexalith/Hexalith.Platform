"""Authenticate actual Memories CI reports; never dispatches, merges or starts services."""
import argparse
import datetime
import hashlib
import importlib.util
import json
import stat
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path, PurePosixPath

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--input', type=Path, required=True)
args = json.loads(parser.parse_args().input.read_bytes())
EV = Path('/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103')
REPO = 'Hexalith/Hexalith.Memories'
head, run_id = args['head'], args['run']
source = Path(args['sourceRoot'])
base = Path(args['cache'])
base.mkdir(exist_ok=True)
sha = lambda b: hashlib.sha256(b).hexdigest()

def api(path):
    return json.loads(subprocess.check_output(['gh', 'api', 'repos/' + REPO + '/' + path]))

run = api('actions/runs/' + str(run_id))
assert run['head_sha'] == head and run['path'] == '.github/workflows/ci.yml'
assert run['run_attempt'] == 1 and run['event'] == args['event']
assert run['repository']['full_name'] == REPO
assert run['status'] == 'completed' and run['conclusion'] == 'success'
commit = api('git/commits/' + head)
local_tree = subprocess.check_output(['git', '-C', str(source), 'rev-parse', 'HEAD^{tree}']).decode().strip()
assert commit['tree']['sha'] == local_tree == 'bb77de2d6f16d2c2922a4d55ca6606377d6d772e'
jobs = api(f'actions/runs/{run_id}/attempts/1/jobs?per_page=100')
assert jobs['total_count'] == len(jobs['jobs']) < 100
required_jobs = ['production-deployment-verification', 'access-telemetry-sql-contract', 'test-unit-contract', 'story-file-scope', 'integration-fast', 'source-debug', 'web-e2e-specimen', 'shared-ci / build-and-test', 'build']
for name in required_jobs:
    matches = [job for job in jobs['jobs'] if job['name'] == name]
    expected = 'success'
    if name == 'story-file-scope' and args['event'] == 'push' and run['head_branch'] == 'main':
        workflow = (source / '.github/workflows/ci.yml').read_text()
        assert "  story-file-scope:\n    name: story-file-scope\n    if: ${{ github.event_name != 'push' || github.ref_name != 'main' }}" in workflow
        expected = 'skipped'
    assert len(matches) == 1 and matches[0]['status'] == 'completed' and matches[0]['conclusion'] == expected, name
unit_job = next(job for job in jobs['jobs'] if job['name'] == 'test-unit-contract')
for name in ['Run coverage gate fixtures', 'Run Docker-free unit and contract tests', 'Validate scoped line coverage']:
    matches = [step for step in unit_job['steps'] if step['name'] == name]
    assert len(matches) == 1 and matches[0]['conclusion'] == 'success', name

artifacts = api(f'actions/runs/{run_id}/artifacts?per_page=100')
assert artifacts['total_count'] == len(artifacts['artifacts']) < 100
matches = [a for a in artifacts['artifacts'] if a['name'] == 'test-unit-contract-results']
assert len(matches) == 1
artifact = matches[0]
assert not artifact['expired'] and artifact['workflow_run']['id'] == run_id and artifact['workflow_run']['head_sha'] == head
archive = base / 'test-unit-contract-results.zip'
if not archive.exists():
    archive.write_bytes(subprocess.check_output(['gh', 'api', f'repos/{REPO}/actions/artifacts/{artifact["id"]}/zip']))
raw = archive.read_bytes()
assert len(raw) == artifact['size_in_bytes'] and 'sha256:' + sha(raw) == artifact['digest']
extracted = base / 'unit-contract-results'
extracted.mkdir(exist_ok=True)
members, reports, domain_cases = [], [], []
with zipfile.ZipFile(archive) as content:
    assert len(content.namelist()) == len(set(content.namelist()))
    assert sum(info.file_size for info in content.infolist()) < 128 * 1024 * 1024
    for info in content.infolist():
        name = info.filename
        path = PurePosixPath(name)
        assert not path.is_absolute() and '..' not in path.parts and '\\' not in name
        assert not stat.S_ISLNK(info.external_attr >> 16)
        if info.is_dir():
            continue
        body = content.read(info)
        target = extracted / name
        target.parent.mkdir(parents=True, exist_ok=True)
        assert not target.is_symlink()
        if target.exists():
            assert target.read_bytes() == body
        else:
            target.write_bytes(body)
        members.append({'name': name, 'bytes': len(body), 'sha256': sha(body)})
        if not name.endswith('.trx'):
            continue
        root = ET.fromstring(body)
        counters = [element.attrib for element in root.iter() if element.tag.rsplit('}', 1)[-1] == 'Counters']
        assert len(counters) == 1
        count = counters[0]
        assert all(int(count.get(key, '0')) == 0 for key in ['failed', 'error', 'timeout', 'aborted'])
        cases = [element.attrib for element in root.iter() if element.tag.rsplit('}', 1)[-1] == 'UnitTestResult']
        assert all(case['outcome'] in ['Passed', 'NotExecuted'] for case in cases)
        reports.append({'name': name, 'sha256': sha(body), 'counters': count, 'existingNotExecutedCases': [case for case in cases if case['outcome'] == 'NotExecuted']})
        if 'Hexalith.Memories.DomainService.Tests/' in name:
            domain_cases = cases
assert len(reports) == 8
assert sum(int(report['counters']['passed']) for report in reports) == 5415
assert sum(int(report['counters']['notExecuted']) for report in reports) == 1
assert len(domain_cases) == 13 and all(case['outcome'] == 'Passed' for case in domain_cases)
prior = json.loads((EV / 'parent-memories-a4d-authenticated-eight-reports-and-first-coverage-refusal-20261105.json').read_bytes())
assert sorted(case['testName'] for case in domain_cases) == sorted(case['testName'] for case in prior['actual_hosted_domain_cases'])
assert any(case['testName'].endswith('ProductionCatalogAdapterTests.OrdinaryHostReachesItsCompleteIntentAdapterFactory') for case in domain_cases)

spec = importlib.util.spec_from_file_location('parent_actual_coverage_validator', source / 'tools/validate-coverage.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
configuration = source / 'tests/tooling/coverage_gate/line-coverage-gate.json'
summary = module.validate_coverage(extracted, configuration, Path('/home/runner/work/Hexalith.Memories/Hexalith.Memories'))
assert summary.threshold_percent == 76.5 and summary.coverage_percent >= 76.5
assert summary.report_count == 8 and len(summary.assemblies) == 15
assert 'Hexalith.Memories.DomainService' in summary.assemblies
log = base / 'actual-unit-contract-job.log'
if not log.exists():
    log.write_bytes(subprocess.check_output(['gh', 'api', f'repos/{REPO}/actions/jobs/{unit_job["id"]}/logs']))
assert b'Ran 40 tests' in log.read_bytes()
result = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'head': head, 'run': run, 'actualSourceCommit': commit, 'actualSourceTreeMatchesReviewedFinalTree': local_tree, 'attemptJobs': jobs, 'artifact': artifact, 'archive': str(archive), 'archiveSha256': sha(raw), 'members': members, 'reports': reports, 'actualDomainCases': domain_cases, 'unitPasses': 5415, 'existingNotExecuted': 1, 'coverage': summary._asdict(), 'assemblies': {name: {'covered': coverage.covered_lines, 'valid': coverage.valid_lines} for name, coverage in summary.assemblies.items()}, 'actualUnitJobLog': str(log), 'actualUnitJobLogSha256': sha(log.read_bytes()), 'actualFortyCoverageFixtureMethodsObservedInJobLog': True, 'auditProcedureSha256': sha(Path(__file__).read_bytes()), 'scope': 'Actual successful owner CI/report/coverage authentication on exact reviewed source; no final E/P native, producer publication or Story acceptance.', 'nativeAcceptance': False, 'candidateAcceptance': False}
output = EV / args['outputName']
assert not output.exists()
output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'output': str(output), 'head': head, 'run': run_id, 'unitPasses': 5415, 'domainCases': len(domain_cases), 'covered': summary.covered_lines, 'valid': summary.valid_lines, 'coveragePercent': summary.coverage_percent, 'thresholdPercent': 76.5}))
