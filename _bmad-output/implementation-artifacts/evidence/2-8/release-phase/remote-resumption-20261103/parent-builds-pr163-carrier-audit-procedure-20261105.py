from pathlib import Path
import datetime
import hashlib
import io
import json
import subprocess
import zipfile

base = Path.home() / '.cache/hexalith/story-2-8/builds-pr163-first-ci-20261105'
ev = Path(__file__).resolve().parent
r = json.loads((ev / 'parent-builds-pr163-terminal-ci-and-carrier-acquisition-20261105.json').read_bytes())
sha = lambda body: hashlib.sha256(body).hexdigest()
root = Path.home() / '.cache/hexalith/story-2-8/builds-final-guard-sonar-20261105/Hexalith.Builds'
actual = '2b68f43d6df0f455f5b8390de7fd049ad04a1d4f'
head = '43adaa5d30437bec89f9485d414b1f92342b617c'
assert r['testedCommit']['sha'] == actual
assert r['testedCommit']['tree']['sha'] == r['headCommit']['tree']['sha'] == 'e94444c7163d835012542a4a5231578bb36e09c5'
assert r['run']['conclusion'] == 'failure'
archive = base / 'artifact13445943472.zip'
assert sha(archive.read_bytes()) == r['localArchive']['sha256']
with zipfile.ZipFile(archive) as z:
    assert len(z.namelist()) == len(set(z.namelist())) == 58
    ib = z.read('g4-tool-package-inventory.json')
    inventory = json.loads(ib)
    attempt = json.loads(z.read('producer-attempt.json'))
    q = inventory['qualification']
    assert attempt['GITHUB_SHA'] == actual and attempt['GITHUB_RUN_ID'] == '42090197080'
    assert attempt['GITHUB_RUN_ATTEMPT'] == '1' and attempt['producerStepOutcome'] == 'success'
    assert attempt['qualificationCredit'] is False
    assert attempt['GITHUB_WORKFLOW_REF'] == 'Hexalith/Hexalith.Builds/.github/workflows/ci.yml@refs/pull/163/merge'
    assert q['sourceTree'] == {'clean': True, 'revision': actual}
    assert q['sourceValidation'] == {'mode': 'skipped', 'result': 'not-run'}
    assert q['controls'] == {'mode': 'executed', 'result': 'passed'} and q['releaseEligible'] is False
    assert len(inventory['qualificationEvidence']) == 55
    with zipfile.ZipFile(io.BytesIO(z.read('g4-tool-qualification-evidence.zip'))) as inner:
        for entry in inventory['qualificationEvidence']:
            body = z.read(entry['file'])
            assert len(body) == entry['sizeBytes'] and sha(body) == entry['sha256'].lower()
            assert inner.read(entry['file']) == body
        assert inner.read('g4-tool-package-inventory.json') == ib
    files = q['fixtures']['files']
    for entry in files:
        path = 'test/fixtures/' + entry['file']
        body = subprocess.check_output(['git', '-C', str(root), 'cat-file', '--filters', '--path=' + path, head + ':' + path])
        assert len(body) == entry['sizeBytes'] and sha(body) == entry['sha256'].lower(), path
    assert len(files) == 299

log = subprocess.check_output(['gh', 'api', 'repos/Hexalith/Hexalith.Builds/actions/jobs/127465909461/logs'])
lp = base / 'contracts-job127465909461-first.log'
lp.write_bytes(log)
record = {
    'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'run': 42090197080, 'runConclusion': 'failure',
    'headSource': head, 'testedSyntheticSource': actual,
    'testedTree': r['testedCommit']['tree']['sha'], 'artifactId': 13445943472,
    'archiveSHA256': r['localArchive']['sha256'], 'producerAttempt': attempt,
    'qualificationPayloadsVerified': 55, 'actualSourceFixtureBodiesVerified': 299,
    'sourceFixtureProjection': 'actual owning git cat-file --filters from immutable reviewed source with the same tested tree',
    'inventorySHA256': sha(ib),
    'qualification': {k: v for k, v in q.items() if k != 'fixtures'},
    'contractsLog': {'path': str(lp), 'bytes': len(log), 'sha256': sha(log)},
    'publicAuthority': False, 'wholeRunAccepted': False,
    'interpretation': 'Contracts job succeeded and carrier matches the actual reviewed source tree; source validation skipped. Failed .NET gate remains blocking. No public release or native credit.',
}
output = ev / 'parent-builds-pr163-failed-ci-carrier-and-fixture-audit-20261105.json'
assert not output.exists()
output.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({k: record[k] for k in ['run', 'runConclusion', 'qualificationPayloadsVerified', 'actualSourceFixtureBodiesVerified', 'publicAuthority']}))
