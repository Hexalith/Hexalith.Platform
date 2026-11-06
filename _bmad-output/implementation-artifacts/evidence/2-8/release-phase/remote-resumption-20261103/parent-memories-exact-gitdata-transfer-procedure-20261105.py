import base64, datetime, hashlib, json, re, subprocess
from pathlib import Path
R = Path('/home/administrator/.cache/hexalith/story-2-8/memories-domain-coverage-docs-20261105/Hexalith.Memories')
OUT = Path('/home/administrator/.cache/hexalith/story-2-8/parent-memories-exact-gitdata-transfer-20261105')
OUT.mkdir(exist_ok=False)
EV = Path('/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103')
REPO = 'Hexalith/Hexalith.Memories'
BASE = 'a4d1baac8c732563b362f9af1d92633945134662'
MAIN = '859680b38a77f04cc8f81ab7a5fe7f40a617cc2a'
HEAD = '0da33b098a8e793da9de3135f6e351883eb70924'
BRANCH = 'fix/production-domain-intent-adapters'
state = {'startedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'scope': 'Transfer exact existing reviewed Git objects through the documented Git API after Git transport commit_refs failures. No source identity, author, date, content, force or policy change.', 'operations': [], 'branchUpdated': False}

def save():
    (OUT / 'transfer-state.json').write_text(json.dumps(state, indent=2) + '\n')

def git(*args):
    return subprocess.check_output(['git', '-C', str(R), *args])

def api(endpoint, method='GET', payload=None, label=None):
    label = label or 'operation-' + str(len(state['operations']))
    args = ['gh', 'api', '--method', method, 'repos/' + REPO + '/' + endpoint]
    if payload is not None:
        request = OUT / (label + '-request.json')
        request.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n')
        args += ['--input', str(request)]
    result = subprocess.run(args, capture_output=True, text=True)
    record = {'label': label, 'method': method, 'endpoint': endpoint, 'exitCode': result.returncode, 'stdout': json.loads(result.stdout) if result.stdout else None, 'stderr': result.stderr}
    (OUT / (label + '-response.json')).write_text(json.dumps(record, indent=2) + '\n')
    state['operations'].append({'label': label, 'method': method, 'endpoint': endpoint, 'exitCode': result.returncode})
    save()
    assert result.returncode == 0, label
    return record['stdout']

def assert_refs():
    branch = api('git/ref/heads/' + BRANCH)
    main = api('git/ref/heads/main')
    pull = api('pulls/66')
    assert branch['object']['sha'] == pull['head']['sha'] == BASE
    assert main['object']['sha'] == MAIN and pull['state'] == 'open'
    assert api('rules/branches/fix%2Fproduction-domain-intent-adapters') == []

assert git('rev-parse', 'HEAD').decode().strip() == HEAD
assert not git('status', '--porcelain').strip()
assert_refs()
blob_cache = set()

def identity(raw):
    match = re.fullmatch(r'(.*) <([^<>]+)> (\d+) ([+-])(\d{2})(\d{2})', raw)
    assert match
    name, email, seconds, sign, hours, minutes = match.groups()
    delta = datetime.timedelta(hours=int(hours), minutes=int(minutes))
    zone = datetime.timezone(delta if sign == '+' else -delta)
    return {'name': name, 'email': email, 'date': datetime.datetime.fromtimestamp(int(seconds), zone).isoformat()}

for revision in ['c0114baa97a08ea298288cdda12cb896f0a49788', HEAD]:
    raw = git('cat-file', 'commit', revision)
    headers, message = raw.split(b'\n\n', 1)
    fields = {}
    for line in headers.decode().splitlines():
        key, value = line.split(' ', 1)
        assert key in {'tree', 'parent', 'author', 'committer'}
        assert key not in fields
        fields[key] = value
    parent = fields['parent']
    msgfile = OUT / (revision + '-exact-existing-message.txt')
    msgfile.write_bytes(message)
    lint = subprocess.run(['node', 'node_modules/@commitlint/cli/cli.js', '--config', 'commitlint.config.mjs', '--edit', str(msgfile)], cwd=R, capture_output=True, text=True)
    assert lint.returncode == 0
    assert json.loads((R / 'node_modules/@commitlint/cli/package.json').read_bytes())['version'] == '21.2.4'
    changed = [x.decode() for x in git('diff', '--name-only', '-z', parent, revision).split(b'\0') if x]
    assert len(changed) == 3
    entries = []
    for path in changed:
        metadata, actual = git('ls-tree', '-z', revision, '--', path).rstrip(b'\0').split(b'\t')
        mode, kind, oid = metadata.decode().split()
        assert actual.decode() == path and kind == 'blob' and mode == '100644'
        body = git('cat-file', 'blob', oid)
        if oid not in blob_cache:
            result = api('git/blobs', 'POST', {'content': base64.b64encode(body).decode(), 'encoding': 'base64'}, 'blob-' + oid)
            assert result['sha'] == oid
            blob_cache.add(oid)
        entries.append({'path': path, 'mode': mode, 'type': kind, 'sha': oid})
    base_tree = git('rev-parse', parent + '^{tree}').decode().strip()
    tree = api('git/trees', 'POST', {'base_tree': base_tree, 'tree': entries}, 'tree-' + revision)
    assert tree['sha'] == fields['tree'], 'Tree differs: no ref may update'
    payload = {'message': message.decode(), 'tree': fields['tree'], 'parents': [parent], 'author': identity(fields['author']), 'committer': identity(fields['committer'])}
    commit = api('git/commits', 'POST', payload, 'commit-' + revision)
    assert commit['sha'] == revision, 'Commit serialization differs: no ref may update'
    assert commit['tree']['sha'] == fields['tree'] and [x['sha'] for x in commit['parents']] == [parent]
    state.setdefault('exactOriginalCommitsTransferred', []).append({'sha': revision, 'tree': fields['tree'], 'parent': parent, 'rawMessageSha256': hashlib.sha256(message).hexdigest(), 'exactOriginalAuthorAndCommitterMetadataCopied': True})
    save()
assert_refs()
result = api('git/refs/heads/' + BRANCH, 'PATCH', {'sha': HEAD, 'force': False}, 'normal-feature-fast-forward')
assert result['object']['sha'] == HEAD
state['branchUpdated'] = True
save()
branch = api('git/ref/heads/' + BRANCH)
main = api('git/ref/heads/main')
pull = api('pulls/66')
assert branch['object']['sha'] == pull['head']['sha'] == HEAD and main['object']['sha'] == MAIN
state.update({'completedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'independentPostUpdateRefAndPrVerified': True, 'mainUnchanged': MAIN, 'force': False, 'protectedMainPush': False})
save()
p = EV / 'parent-memories-0da33-exact-git-api-transfer-and-normal-ff-20261105.json'
assert not p.exists()
p.write_text(json.dumps(state, indent=2) + '\n')
print(json.dumps({'receipt': str(p), 'head': HEAD, 'sameOriginalCommitHashes': True, 'normalFeatureFastForward': True, 'mainUnchanged': MAIN}))
