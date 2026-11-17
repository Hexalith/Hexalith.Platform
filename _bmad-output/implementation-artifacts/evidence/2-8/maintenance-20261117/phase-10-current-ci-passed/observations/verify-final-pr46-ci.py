"""Authenticate PR preflight outputs; no remote mutation or native acceptance."""
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import zipfile

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
OUT = AREA / 'pr46-final-ci-verified-01'
OUT.mkdir(exist_ok=False)
HEAD = '408481da06128ae50ad274d5dddcd4234d8b677d'
BASE = '11719c5cf51c2b2b080b89e0e301c2ef8dbacc46'
REPO = 'repos/Hexalith/Hexalith.Platform/'

def api(label, endpoint):
    result = subprocess.run(['gh', 'api', REPO + endpoint], capture_output=True, timeout=120)
    (OUT / (label + '.stderr')).write_bytes(result.stderr)
    assert result.returncode == 0, label
    (OUT / (label + '.json')).write_bytes(result.stdout)
    return json.loads(result.stdout)

record = {'classification': 'PR CI preflight only', 'candidateAcceptance': False,
          'nativeQualificationCredit': False, 'remoteMutations': False,
          'pr': 46, 'headSource': HEAD, 'baseSource': BASE}
try:
    pr = api('pr', 'pulls/46')
    assert pr['state'] == 'open' and not pr['merged']
    assert pr['head']['sha'] == HEAD and pr['base']['sha'] == BASE
    for label, run_id, workflow in [('tuple', 44166185074, '.github/workflows/tuple-ci.yml'),
                                    ('sdk', 44166185115, '.github/workflows/catalog-builder.yml')]:
        run = api(label + '-run', 'actions/runs/' + str(run_id))
        assert run['id'] == run_id and run['head_sha'] == HEAD and run['run_attempt'] == 1
        assert run['event'] == 'pull_request' and run['path'] == workflow
        assert run['status'] == 'completed' and run['conclusion'] == 'success'
        linked = [p for p in run['pull_requests'] if p['number'] == 46]
        assert len(linked) == 1 and linked[0]['head']['sha'] == HEAD and linked[0]['base']['sha'] == BASE
        jobs = api(label + '-jobs', 'actions/runs/' + str(run_id) + '/jobs?per_page=100')
        assert jobs['total_count'] == len(jobs['jobs'])
        assert all(j['status'] == 'completed' and j['conclusion'] == 'success' for j in jobs['jobs'])
        record[label] = {'runId': run_id, 'attempt': 1, 'jobs': [
            {'id': j['id'], 'name': j['name'], 'conclusion': j['conclusion']} for j in jobs['jobs']]}
    assert len(record['tuple']['jobs']) == 3
    assert len(record['sdk']['jobs']) == 6
    artifact = api('artifact', 'actions/artifacts/14369843406')
    assert artifact['workflow_run']['id'] == 44166185074
    assert artifact['workflow_run']['head_sha'] == HEAD
    assert not artifact['expired'] and artifact['name'] == 'platform-tuple-44166185074-1'
    expected_digest = 'sha256:7007cdc09afc7d815eddc37c7d1a84db91694bbf90bf5d4036ca970af1e3c18b'
    assert artifact['digest'] == expected_digest
    path = Path(os.environ['TMPDIR']) / 'tuple.zip'
    print('Downloading official tuple artifact', flush=True)
    with path.open('wb') as output:
        result = subprocess.run(['gh', 'api', REPO + 'actions/artifacts/14369843406/zip'],
                                stdout=output, stderr=subprocess.PIPE, timeout=240)
    (OUT / 'download.stderr').write_bytes(result.stderr)
    assert result.returncode == 0
    assert path.stat().st_size == artifact['size_in_bytes']
    digest = hashlib.file_digest(path.open('rb'), 'sha256').hexdigest()
    assert 'sha256:' + digest == expected_digest
    print('Official ZIP digest verified; validating member CRCs', flush=True)
    retained = []
    members = []
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        assert len(names) == len(set(names))
        for info in archive.infolist():
            name = info.filename
            p = PurePosixPath(name)
            assert not p.is_absolute() and '..' not in p.parts and '\\' not in name
            assert ((info.external_attr >> 16) & 0o170000) != 0o120000
            members.append({'name': name, 'crc': info.CRC, 'size': info.file_size})
        assert archive.testzip() is None
        for info in archive.infolist():
            name = info.filename
            if info.is_dir() or info.file_size > 4_000_000:
                continue
            if name.endswith(('tuple-ci-result.json', '.xml', 'bootstrap.json',
                              'source-sdk-propagation-controls.stdout',
                              'source-sdk-propagation-controls.stderr')):
                raw = archive.read(name)
                target = OUT / 'receipts' / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(raw)
                retained.append({'name': name, 'sha256': hashlib.sha256(raw).hexdigest(), 'size': len(raw)})
    (OUT / 'zip-member-inventory.json').write_text(json.dumps(members, indent=2) + '\n')
    assert len([r for r in retained if r['name'].endswith('tuple-ci-result.json')]) == 3
    record['artifact'] = {'id': artifact['id'], 'sha256': digest, 'size': artifact['size_in_bytes'],
                          'officialDigestAndCrcVerified': True, 'uniqueSafePaths': True, 'retained': retained}
    record['status'] = 'archive-authenticated; primary receipt audit pending'
    print(json.dumps({'status': record['status'], 'retained': len(retained), 'jobs': record['tuple']['jobs']}), flush=True)
except BaseException as error:
    record['status'] = 'failed'
    record['error'] = str(error)
    raise
finally:
    (OUT / 'result.json').write_text(json.dumps(record, indent=2) + '\n')
