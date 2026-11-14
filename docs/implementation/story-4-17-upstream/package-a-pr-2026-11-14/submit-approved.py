"""Submit the exact owner-approved bundle; run inside run-with-temp.

This is the parent-session submission procedure. Every remote write is recorded
and preceded by a fresh identity observation. A failed stage stops the procedure.
No source commit is recreated, and no issue or existing PR is edited.
"""
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

OUT = Path(__file__).resolve().parent
SOURCE = Path(os.environ['TMPDIR']) / 'components-contrib'
RECEIPT = OUT / 'submission-receipt.json'
BRANCH = 'fix/rabbitmq-publish-errors-4764'
REF = 'refs/heads/' + BRANCH
OFFICIAL = 'https://github.com/dapr/components-contrib.git'
FORK = 'jpiquot/components-contrib'


def now():
    return datetime.now(timezone.utc).isoformat()


record = json.loads(RECEIPT.read_text())
if not record.get('approvedTexts') or not record.get('approvedUtc'):
    raise SystemExit('HALT: explicit approval is absent')
if record.get('remoteWrites'):
    raise SystemExit('HALT: an existing partial submission requires inspection')
record['submissionStartedUtc'] = now()
record['disposableWorkspace'] = str(SOURCE.parent)


def save():
    RECEIPT.write_text(json.dumps(record, indent=2, ensure_ascii=False) + '\n')


def run(argv, label, cwd=None, mutation=False):
    entry = {'stage': label, 'argv': argv, 'startedUtc': now(), 'status': 'ATTEMPTED'}
    if mutation:
        record['remoteWrites'].append(entry)
        save()
    result = subprocess.run(argv, cwd=cwd, capture_output=True)
    (OUT / (label + '.stdout.log')).write_bytes(result.stdout)
    (OUT / (label + '.stderr.log')).write_bytes(result.stderr)
    entry.update(endedUtc=now(), exitCode=result.returncode,
                 status='SUCCEEDED' if result.returncode == 0 else 'FAILED',
                 stdout=label + '.stdout.log', stderr=label + '.stderr.log')
    if mutation:
        save()
    if result.returncode:
        raise RuntimeError(label + ' failed, exit ' + str(result.returncode))
    return result.stdout


def authenticate(stage):
    raw = run(['gh', 'api', 'user'], 'auth-before-' + stage)
    response = json.loads(raw)
    observation = {'observedUtc': now(), 'login': response['login'],
                   'rawResponse': 'auth-before-' + stage + '.stdout.log'}
    (OUT / ('auth-before-' + stage + '.json')).write_text(json.dumps(observation, indent=2) + '\n')
    if response['login'] != 'jpiquot':
        raise RuntimeError('HALT: authenticated login is not jpiquot')


def assert_approved_bytes():
    for name, expected in record['approvedTexts'].items():
        data = (OUT / name).read_bytes()
        if len(data) != expected['bytes'] or hashlib.sha256(data).hexdigest() != expected['sha256']:
            raise RuntimeError('HALT: approved text changed: ' + name)
    for name, field in [('pr-delta.patch', 'prDeltaSha256'), ('package-a-pr.bundle', 'bundleSha256')]:
        if hashlib.sha256((OUT / name).read_bytes()).hexdigest() != record['approvedSource'][field]:
            raise RuntimeError('HALT: approved source changed: ' + name)


def preflight(stage):
    run([sys.executable, '-B', str(OUT / 'preflight.py'), '--source', str(SOURCE), '--bundle'],
        'preflight-' + stage)
    shutil.copy2(OUT / 'submission-preflight.json', OUT / ('submission-preflight-' + stage + '.json'))


try:
    save()
    assert_approved_bytes()
    run(['git', 'clone', '--quiet', '--depth=1', '--branch=main', '--single-branch', OFFICIAL, str(SOURCE)],
        'submission-clone')
    if (OUT / 'submission-preflight.json').exists():
        shutil.copy2(OUT / 'submission-preflight.json', OUT / 'history/pre-submission-preflight.json')
    preflight('before-write')
    print('Preflight passed; exact source and receipt gates verified.', flush=True)
    run(['git', 'fetch', '--quiet', str(OUT / 'package-a-pr.bundle'), REF + ':' + REF],
        'submission-fetch-bundle', SOURCE)
    head = run(['git', 'rev-parse', REF], 'submission-head', SOURCE).decode().strip()
    tree = run(['git', 'rev-parse', REF + '^{tree}'], 'submission-tree', SOURCE).decode().strip()
    parent = run(['git', 'show', '-s', '--format=%P', REF], 'submission-parent', SOURCE).decode().strip()
    raw = run(['git', 'cat-file', 'commit', REF], 'submission-commit', SOURCE)
    message = raw.split(b'\n\n', 1)[1]
    if (head != record['head'] or tree != record['tree'] or parent != record['base']
            or message != (OUT / 'commit-message.txt').read_bytes()):
        raise RuntimeError('HALT: bundled commit identity differs')
    assert_approved_bytes()
    authenticate('fork')
    run(['gh', 'repo', 'fork', 'dapr/components-contrib', '--clone=false'], 'fork', mutation=True)
    fork = json.loads(run(['gh', 'api', 'repos/' + FORK], 'fork-readback'))
    if not (fork['full_name'] == FORK and fork['fork']
            and fork['parent']['full_name'] == 'dapr/components-contrib'):
        raise RuntimeError('HALT: created repository is not the intended fork')
    record['fork'] = {'repository': fork['full_name'], 'url': fork['html_url'], 'id': fork['id'],
                      'parent': fork['parent']['full_name']}
    save()
    print('Fork created and verified.', flush=True)
    authenticate('push')
    run(['git', '-c', 'credential.helper=', '-c', 'credential.helper=!gh auth git-credential',
         'push', 'https://github.com/' + FORK + '.git', REF + ':' + REF],
        'push', SOURCE, mutation=True)
    branch = json.loads(run(['gh', 'api', 'repos/' + FORK + '/git/ref/heads/' + BRANCH], 'branch-readback'))
    if branch['object']['sha'] != head:
        raise RuntimeError('HALT: pushed branch SHA differs')
    record['pushedHead'] = branch['object']['sha']
    save()
    print('Exact tested commit pushed and verified.', flush=True)
    preflight('before-pr')
    authenticate('pr')
    assert_approved_bytes()
    url = run(['gh', 'pr', 'create', '-R', 'dapr/components-contrib', '--base', 'main',
               '--head', 'jpiquot:' + BRANCH, '--title', (OUT / 'pr-title.txt').read_bytes().decode('utf-8'),
               '--body-file', str(OUT / 'pr-body.md')], 'create-pr', mutation=True).decode().strip()
    record['pullRequest'] = {'url': url}
    record['status'] = 'submitted; readback pending'
    save()
    print('Created ' + url, flush=True)
    run([sys.executable, '-B', str(OUT / 'readback.py'), url], 'submitted-pr-readback')
    readback = json.loads((OUT / 'pr-readback.json').read_text())
    record['readback'] = readback
    metadata = json.loads(run(['gh', 'pr', 'view', url, '-R', 'dapr/components-contrib', '--json',
                               'number,url,state,baseRefName,headRefName,headRefOid,headRepositoryOwner,headRepository,isCrossRepository,isDraft'],
                              'submitted-pr-metadata'))
    commit = json.loads(run(['gh', 'api', 'repos/dapr/components-contrib/git/commits/' + head],
                           'submitted-commit-readback'))
    if not (metadata['baseRefName'] == 'main' and metadata['headRefName'] == BRANCH
            and metadata['headRefOid'] == head and metadata['headRepositoryOwner']['login'] == 'jpiquot'
            and commit['tree']['sha'] == tree and [p['sha'] for p in commit['parents']] == [record['base']]
            and commit['message'] == message.decode().rstrip('\n')):
        raise RuntimeError('HALT: submitted PR or commit metadata differs')
    record['pullRequest'] = metadata
    record['remoteTreeVerified'] = True
    record['status'] = 'submitted; exact readback verified'
    record['submittedUtc'] = now()
    save()
    print('PR title/body bytes, head, tree, parent and sign-off verified.', flush=True)
except Exception as error:
    record['status'] = 'halted; inspect retained submission state'
    record['haltedUtc'] = now()
    record['error'] = str(error)
    save()
    print(str(error), file=sys.stderr, flush=True)
    raise SystemExit(3)
