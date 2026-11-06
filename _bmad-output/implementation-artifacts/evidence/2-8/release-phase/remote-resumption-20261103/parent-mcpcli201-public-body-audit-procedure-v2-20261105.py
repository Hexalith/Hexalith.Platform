import ast
import datetime
import hashlib
import json
import os
import struct
import subprocess
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

EV = Path('_bmad-output/implementation-artifacts/evidence/2-8/release-phase/remote-resumption-20261103')
BASE = Path('/home/administrator/.cache/hexalith/story-2-8/builds-4445-incoming-20261105')
SOURCE = 'aa2845edbea23444a1d362060b8d0f5dc05a132e'
REPO = 'Hexalith/Hexalith.McpCli'
sha = lambda b: hashlib.sha256(b).hexdigest()

def api(path):
    return json.loads(subprocess.check_output(['gh', 'api', 'repos/' + REPO + '/' + path]))

# Reuse the already independently read, exact ZIP reconstruction implementation.
prior = ast.parse((EV / 'parent-builds-source-ci-official-audit-procedure-20261105.py').read_text())
function = next(n for n in prior.body if isinstance(n, ast.FunctionDef) and n.name == 'reconstruct_unsigned')
exec(compile(ast.Module(body=[function], type_ignores=[]), '<retained-zip-reconstruction>', 'exec'))

release = api('releases/435585219')
assert release['tag_name'] == 'v2.0.1' and not release['draft'] and not release['prerelease']
tag = api('git/ref/tags/v2.0.1')
obj = tag['object']
annotated = None
if obj['type'] == 'tag':
    annotated = api('git/tags/' + obj['sha'])
    assert annotated['tag'] == 'v2.0.1'
    obj = annotated['object']
assert obj == {'sha': SOURCE, 'type': 'commit', 'url': 'https://api.github.com/repos/' + REPO + '/git/commits/' + SOURCE}
runs = []
for run_id, path, event in [(42082617201, '.github/workflows/release.yml', 'workflow_dispatch'), (42072073153, '.github/workflows/ci.yml', 'push')]:
    run = api('actions/runs/' + str(run_id))
    assert run['head_sha'] == SOURCE and run['path'] == path and run['event'] == event
    assert run['status'] == 'completed' and run['conclusion'] == 'success'
    assert run['head_branch'] == 'main' and run['repository']['full_name'] == REPO
    runs.append(run)

rows = []
for pid in ['Hexalith.McpCli.Abstractions', 'Hexalith.McpCli']:
    package = BASE / (pid + '.2.0.1.nupkg')
    raw = package.read_bytes()
    asset = next(a for a in release['assets'] if a['name'] == package.name)
    unsigned = reconstruct_unsigned(raw)
    assert len(unsigned) == asset['size'] and 'sha256:' + sha(unsigned) == asset['digest']
    retained = BASE / ('official-unsigned-' + package.name)
    if not retained.exists():
        data = subprocess.check_output(['gh', 'api', '-H', 'Accept: application/octet-stream', 'repos/' + REPO + '/releases/assets/' + str(asset['id'])])
        assert len(data) == asset['size'] and 'sha256:' + sha(data) == asset['digest']
        retained.write_bytes(data)
    assert retained.read_bytes() == unsigned
    env = os.environ.copy()
    env.pop('NUGET_CERT_REVOCATION_MODE', None)
    env['DOTNET_CLI_HOME'] = str(BASE / 'dotnet-home')
    result = subprocess.run(['dotnet', 'nuget', 'verify', '--all', str(package)], env=env, capture_output=True, text=True)
    log = BASE / (pid + '-normal-repository-signature.txt')
    assert not log.exists()
    log.write_text(result.stdout + result.stderr)
    assert result.returncode == 0, log
    with zipfile.ZipFile(package) as signed, zipfile.ZipFile(retained) as official:
        meta = ET.fromstring(signed.read(pid + '.nuspec'))
        ns = {'n': meta.tag.split('}')[0].removeprefix('{')}
        md = meta.find('n:metadata', ns)
        assert md.findtext('n:id', namespaces=ns) == pid
        assert md.findtext('n:version', namespaces=ns) == '2.0.1'
        repository = md.find('n:repository', ns)
        assert repository.attrib['commit'] == SOURCE
        signed_names = set(signed.namelist()) - {'.signature.p7s'}
        assert signed_names == set(official.namelist())
        members = {}
        for name in sorted(signed_names):
            assert signed.read(name) == official.read(name)
            members[name] = sha(signed.read(name))
    rows.append({'id': pid, 'signedArchiveSha256': sha(raw), 'officialUnsignedArchiveSha256': sha(unsigned), 'officialAsset': asset, 'repository': repository.attrib, 'normalSignatureExitCode': result.returncode, 'normalSignatureLog': str(log), 'normalSignatureLogSha256': sha(log.read_bytes()), 'allNonSignatureMemberHashes': members})

out = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'source': SOURCE, 'version': '2.0.1', 'release': release, 'tag': tag, 'annotatedTag': annotated, 'successfulActualSourceRuns': runs, 'packages': rows, 'scope': 'Actual public package/release source and exact body authentication only; no service execution or native/Story acceptance.', 'candidateAcceptance': False, 'nativeAcceptance': False}
path = EV / 'parent-mcpcli201-public-body-independent-audit-20261105.json'
assert not path.exists()
path.write_text(json.dumps(out, indent=2) + '\n')
print(json.dumps({'output': str(path), 'source': SOURCE, 'packages': [{'id': row['id'], 'signatureExit': row['normalSignatureExitCode'], 'members': len(row['allNonSignatureMemberHashes'])} for row in rows]}))
