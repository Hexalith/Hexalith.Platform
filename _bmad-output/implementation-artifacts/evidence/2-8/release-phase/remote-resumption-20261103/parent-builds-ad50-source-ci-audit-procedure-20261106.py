"""Authenticate the actual incoming CI carrier, fixtures and logged unit totals."""
import datetime
import hashlib
import io
import json
import re
import subprocess
import zipfile
from pathlib import Path

EV = Path(__file__).resolve().parent
ROOT = Path('/home/administrator/.cache/hexalith/story-2-8/parent-builds-ad50-publication-20261106')
BARE = '/home/administrator/.cache/hexalith/story-2-8/parent-builds-ad50-read-20261106/source.git'
LOCAL = '/home/administrator/.cache/hexalith/story-2-8/builds-memories325-catalog-prerequisite-20261106/Hexalith.Builds'
SOURCE = 'ad50acf920d78ee16033bdc1cf26c34e8275cdcb'
PRIVATE = 'dea51b5c17cf3e20325b5726d116975485e0e967'
sha = lambda value: hashlib.sha256(value).hexdigest()
run = json.loads((ROOT / 'source-ci.json').read_bytes())
jobs = json.loads((ROOT / 'source-ci-jobs.json').read_bytes())['jobs']
assert run['id'] == 42142102328 and run['head_sha'] == SOURCE
assert run['run_attempt'] == 1 and run['event'] == 'push' and run['conclusion'] == 'success'
assert all(job['conclusion'] == ('skipped' if job['name'] == 'Dependency review' else 'success') for job in jobs)
archive = ROOT / 'actual-source-ci-qualification.zip'
assert sha(archive.read_bytes()) == 'f839cb6c176d65e702889ac8a9f1e63ed8b65f5993dacb3b1886fab6da7bb8d0'
with zipfile.ZipFile(archive) as outer:
    assert len(outer.namelist()) == len(set(outer.namelist()))
    inventory_raw = outer.read('g4-tool-package-inventory.json')
    inventory = json.loads(inventory_raw)
    assert inventory['version'] == '0.0.0-ci.726'
    with zipfile.ZipFile(io.BytesIO(outer.read('g4-tool-qualification-evidence.zip'))) as nested:
        producer = json.loads(nested.read('producer-run.json'))
        assert producer['sourceRevision'] == producer['GITHUB_WORKFLOW_SHA'] == SOURCE
        assert producer['GITHUB_RUN_ID'] == '42142102328' and producer['GITHUB_RUN_ATTEMPT'] == '1'
        assert producer['GITHUB_JOB'] == 'contracts' and producer['GITHUB_EVENT_NAME'] == 'push'
        assert producer['kind'] == 'ci' and producer['inventorySha256'] == sha(inventory_raw)
        assert nested.read('g4-tool-package-inventory.json') == inventory_raw
        payloads = []
        for entry in inventory['qualificationEvidence']:
            raw = outer.read(entry['file'])
            assert raw == nested.read(entry['file'])
            assert len(raw) == entry['sizeBytes'] and sha(raw) == entry['sha256'].lower()
            payloads.append({'file': entry['file'], 'bytes': len(raw), 'sha256': sha(raw)})
        assert len(payloads) == len({p['file'] for p in payloads}) == producer['qualificationEvidenceFiles'] == 55
    q = inventory['qualification']
    assert q['sourceTree'] == {'clean': True, 'revision': SOURCE}
    assert q['controls'] == {'mode': 'executed', 'result': 'passed'}
    assert q['packageBuild'] == {'mode': 'official', 'result': 'passed'}
    assert q['sourceValidation'] == {'mode': 'skipped', 'result': 'not-run'} and q['releaseEligible'] is False

# The actual public source tree and closed private checkout have identical fixture
# Git objects. Read those already-present blobs without modifying the closed root.
remote_tree = subprocess.check_output(['git', '--git-dir', BARE, 'ls-tree', '-r', SOURCE, 'test/fixtures'])
local_tree = subprocess.check_output(['git', '-C', LOCAL, 'ls-tree', '-r', PRIVATE, 'test/fixtures'])
assert remote_tree == local_tree
entries = q['fixtures']['files']
paths = ['test/fixtures/' + entry['file'] for entry in entries]
request = ''.join(PRIVATE + ':' + path + '\n' for path in paths).encode()
stream = io.BytesIO(subprocess.check_output(['git', '-C', LOCAL, 'cat-file', '--batch'], input=request))
attrs = subprocess.check_output(['git', '-C', LOCAL, 'check-attr', '--source=' + PRIVATE, 'eol', '--', *paths]).decode().splitlines()
eol = {line.rsplit(': ', 2)[0]: line.rsplit(': ', 1)[1] for line in attrs}
fixtures = []
for path, entry in zip(paths, entries, strict=True):
    header = stream.readline().decode().split()
    assert len(header) == 3 and header[1] == 'blob'
    raw = stream.read(int(header[2]))
    assert stream.read(1) == b'\n'
    conversion = 'Exact raw Git blob'
    if sha(raw) != entry['sha256'].lower():
        assert eol[path] == 'crlf', path
        raw = raw.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
        conversion = 'Actual Git attribute eol=crlf checkout'
    assert sha(raw) == entry['sha256'].lower() and len(raw) == entry['sizeBytes'], path
    fixtures.append({'path': path, 'sha256': sha(raw), 'bytes': len(raw), 'conversion': conversion})
assert not stream.read() and len(fixtures) == 301
declared = set(paths)
tracked = {line.split('\t', 1)[1] for line in remote_tree.decode().splitlines()}

log = (ROOT / 'actual-source-dotnet-test-job.log').read_text()
plain = re.sub(r'\x1b\[[0-9;]*[A-Za-z]', '', log)
summaries = []
for block in plain.split('Test run summary: Passed!')[1:]:
    fields = {}
    for key in ['total', 'failed', 'succeeded', 'skipped']:
        match = re.search(r'\b' + key + r':\s*(\d+)', block)
        assert match, key
        fields[key] = int(match.group(1))
    assert fields['failed'] == 0 and fields['succeeded'] + fields['skipped'] == fields['total']
    summaries.append(fields)
assert summaries == [{'total': 2827, 'failed': 0, 'succeeded': 2822, 'skipped': 5},
                     {'total': 191, 'failed': 0, 'succeeded': 191, 'skipped': 0},
                     {'total': 13, 'failed': 0, 'succeeded': 1, 'skipped': 12}]
record = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'run': run, 'jobs': jobs,
          'source': SOURCE, 'producer': producer, 'artifactId': 13468808318,
          'artifactSha256': sha(archive.read_bytes()), 'inventorySha256': sha(inventory_raw),
          'qualificationPayloadsVerified': payloads, 'declaredFixtureBodiesVerified': fixtures,
          'unlistedTrackedFixturePaths': sorted(tracked - declared), 'actualDotnetTotals': summaries,
          'ciCarrierQualification': q, 'publicPublicationAuthority': False,
          'normalPublisherAlreadyExists': 42144370420,
          'parentDispatchOrReviewOrPolicyMutationSent': False,
          'limits': ['Private CI version is not a public accepted tool.',
                     'CI package carrier explicitly skips source validation and is release-ineligible; separate real unit job totals are recorded.',
                     'Normal release must execute its own source validation and yield genuine signed public packages.',
                     'Synthetic source-release fixture is not a native service outcome.',
                     'The whole-suite log retains counts; no extra per-case XML or native success is invented.'],
          'nativeAcceptance': False, 'candidateAcceptance': False}
output = EV / 'parent-builds-ad50-main-ci-carrier-and-real-source-tests-audit-20261106.json'
assert not output.exists()
output.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({'audit': str(output), 'payloads': len(payloads), 'fixtures': len(fixtures), 'dotnet': summaries}))
