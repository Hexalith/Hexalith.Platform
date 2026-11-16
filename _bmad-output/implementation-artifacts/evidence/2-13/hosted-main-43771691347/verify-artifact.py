"""Verify a downloaded artifact or recheck its retained authentic observations."""
from pathlib import Path
import argparse
import gzip
import hashlib
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

out = Path(__file__).resolve().parent
root = out.parents[4]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--retained', action='store_true', help='Recheck retained observations without downloading the original ZIP')
args = parser.parse_args()
run = json.loads((out / 'run.json').read_text())
artifact = json.loads((out / 'artifacts.json').read_text())['artifacts'][0]
revision = run['head_sha']
assert run['event'] == 'push' and run['head_branch'] == 'main'
assert run['status'] == 'completed' and run['conclusion'] == 'success'
assert artifact['workflow_run']['id'] == run['id']
assert artifact['workflow_run']['head_sha'] == revision
assert artifact['name'] == 'platform-tuple-' + str(run['id']) + '-' + str(run['run_attempt'])
jobs = json.loads((out / 'jobs.json').read_text())['jobs']
assert len(jobs) == 3
assert {j['name'] for j in jobs} == {'resolve-sources', 'tuple', 'tuple-verdict'}
assert all(j['status'] == 'completed' and j['conclusion'] == 'success' for j in jobs)
assert all(j['run_id'] == run['id'] for j in jobs)

# Validate the log before any evidence can be replaced, including on reruns.
log = out / 'tuple-job.log'
raw_log = log.read_bytes() if log.exists() else gzip.decompress((out / 'tuple-job.log.gz').read_bytes())
profiles = {'current42': 'tuple-ci-sdk42-current-suites.json',
            'base42': 'tuple-ci-sdk42-base-suites.json',
            'extensions': 'tuple-ci-extension-suites.json'}
profile_bytes = {suite: subprocess.check_output(['git', '-c', 'core.commitGraph=false', 'show', revision + ':eng/' + path], cwd=root)
                 for suite, path in profiles.items()}
if args.retained:
    receipt = json.loads((out / 'observations-receipt.json').read_text())
    retained_bytes = (out / receipt['file']).read_bytes()
    assert hashlib.sha256(retained_bytes).hexdigest() == receipt['sha256']
    assert receipt['artifactId'] == artifact['id']
    digest, archive_size = receipt['originalZipSha256'], receipt['originalZipBytes']
    assert archive_size == artifact['size_in_bytes']
    assert hashlib.sha256(raw_log).hexdigest() == json.loads((out / 'summary.json').read_text())['tupleJobLogSha256']
    with tarfile.open(fileobj=io.BytesIO(retained_bytes), mode='r:gz') as retained:
        observations = {}
        for member in retained.getmembers():
            assert member.isfile() and member.name not in observations
            raw = retained.extractfile(member).read()
            assert len(raw) == receipt['members'][member.name]['bytes']
            assert hashlib.sha256(raw).hexdigest() == receipt['members'][member.name]['sha256']
            observations[member.name] = raw
    assert set(observations) == set(receipt['members'])
    bundle = None
    names, read = list(observations), observations.__getitem__
else:
    archive = Path(os.environ['TMPDIR']) / 'hosted-artifact.zip'
    with urllib.request.urlopen(sys.stdin.readline().strip(), timeout=60) as response, archive.open('wb') as target:
        shutil.copyfileobj(response, target)
    with archive.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    archive_size = archive.stat().st_size
    assert archive_size == artifact['size_in_bytes']
    bundle = zipfile.ZipFile(archive)
    names, read = bundle.namelist(), bundle.read
assert 'sha256:' + digest == artifact['digest']
assert len(names) == len(set(names))
selected = {}
summary = {}
try:
    results = [n for n in names if n.endswith('/tuple-ci-result.json')]
    assert len(results) == 3
    for name in results:
        raw = read(name)
        result = json.loads(raw)
        suite = result['suite']
        assert suite in profiles and suite not in summary
        assert result['platformRevision'] == revision and result['status'] == 'passed'
        groups = json.loads(profile_bytes[suite])['groups']
        assert len(result['tests']) == len(groups)
        total = 0
        for number, (test, group) in enumerate(zip(result['tests'], groups)):
            assert test['currentSource'] == revision and test['platformSource'] == revision
            assert test['selectors'] == group['selectors']
            counts = test['counts']
            assert int(counts['passed']) == int(counts['total']) == group['requiredCount'] > 0
            assert all(int(counts.get(k, 0)) == 0 for k in ['failed', 'errors', 'skipped', 'not-run'])
            xml_name = str(Path(name).parent / ('required-' + str(number) + '.xml'))
            xml = read(xml_name)
            assemblies = ET.fromstring(xml).findall('assembly')
            assert len(assemblies) == 1
            assembly = assemblies[0]
            actual = assembly.attrib
            for k in ['total', 'passed', 'failed', 'errors', 'skipped', 'not-run']:
                assert int(actual.get(k, 0)) == int(counts.get(k, 0))
            cases = assembly.findall('.//test')
            assert len(cases) == group['requiredCount']
            represented = set()
            assert group['filter'] in ('-class', '-method')
            for case in cases:
                assert case.attrib['result'] == 'Pass'
                identity = case.attrib['type'] + '.' + case.attrib['method']
                assert case.attrib['type'] and case.attrib['method']
                assert case.attrib['name'] == identity or case.attrib['name'].startswith(identity + '(')
                selector = case.attrib['type'] if group['filter'] == '-class' else identity
                assert selector in group['selectors']
                represented.add(selector)
            assert represented == set(group['selectors'])
            selected[xml_name] = xml
            total += int(counts['passed'])
        selected[name] = raw
        summary[suite] = {'groups': len(groups), 'passed': total, 'skipped': 0, 'status': result['status'],
                          'inventoryPath': 'eng/' + profiles[suite], 'inventorySha256': hashlib.sha256(profile_bytes[suite]).hexdigest()}
    bootstraps = [n for n in names if n.endswith('/bootstrap.json')]
    assert len(bootstraps) == 1
    raw = read(bootstraps[0])
    bootstrap = json.loads(raw)
    assert bootstrap['context']['PLATFORM_CANDIDATE_SHA'] == revision
    assert bootstrap['context']['GITHUB_RUN_ID'] == str(run['id'])
    assert bootstrap['context']['GITHUB_RUN_ATTEMPT'] == str(run['run_attempt'])
    assert bootstrap['status'] == 'completed'
    assert all(v == 'success' for v in bootstrap['outcomes'].values())
    selected[bootstraps[0]] = raw
finally:
    if bundle is not None:
        bundle.close()
assert {k: v['passed'] for k, v in summary.items()} == {'current42': 463, 'base42': 93, 'extensions': 9}
assert sum(v['groups'] for v in summary.values()) == 36

result = {'passed': True, 'sourceRevision': revision, 'runId': run['id'],
          'runAttempt': run['run_attempt'], 'event': run['event'], 'branch': run['head_branch'],
          'runUrl': run['html_url'], 'jobs': {j['name']: j['conclusion'] for j in jobs},
          'artifactId': artifact['id'],
          'profiles': summary, 'compositionExecutions': sum(s['passed'] for s in summary.values()),
          'compositionGroups': 36, 'skipped': 0, 'hostedMainAcceptance': True,
          'tupleJobLogSha256': hashlib.sha256(raw_log).hexdigest(),
          'retainedArtifactMembers': len(selected), 'observationReceipt': 'observations-receipt.json'}
retained = out / 'verified-hosted-observations.tar.gz'
if not args.retained:
    with tarfile.open(retained, 'w:gz') as bundle:
        for name, raw in sorted(selected.items()):
            member = tarfile.TarInfo(name)
            member.size = len(raw)
            bundle.addfile(member, io.BytesIO(raw))
    members = {name: {'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()} for name, raw in selected.items()}
    receipt = {'artifactId': artifact['id'], 'originalZipSha256': digest,
               'originalZipBytes': archive_size, 'file': retained.name,
               'sha256': hashlib.sha256(retained.read_bytes()).hexdigest(), 'members': members}
    (out / 'observations-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    (out / 'tuple-job.log.gz').write_bytes(gzip.compress(raw_log, mtime=0))
    if log.exists():
        log.unlink()
(out / 'summary.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result, indent=2), flush=True)
