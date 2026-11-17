"""Prepare an unpublished security-patch proposal; never changes shared checkouts."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-maintenance-20261117'
OUT = AREA / 'builds-aspire-security-02'
OUT.mkdir(exist_ok=False)
REPO = Path(os.environ['TMPDIR']) / 'Hexalith.Builds'
BASE = '979cfd3e2c4145be516fbd833dacfc6b8e8fdce5'
OBSERVED_PATCH_SOURCE = '6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b'
BRANCH = 'fix/story-2-8-aspire-maintenance'
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent) + ':' + os.environ['PATH'])
RECORD = {'base': BASE, 'remoteMutations': False, 'legacyPolicyChanged': False,
          'classification': 'unpublished catalog security-patch proposal only', 'commands': []}


def run(label, args, cwd=REPO, payload=None, timeout=300):
    result = subprocess.run(args, cwd=cwd, env=ENV, input=payload, capture_output=True, text=True, timeout=timeout)
    (OUT / (label + '.log')).write_text(result.stdout + result.stderr)
    RECORD['commands'].append({'label': label, 'exitCode': result.returncode})
    print(label, result.returncode, flush=True)
    if result.returncode:
        raise RuntimeError(label)
    return result.stdout.strip()


def git(label, *args):
    return run(label, ['git', '-c', 'submodule.recurse=false', *args])


def commit(label, message):
    (OUT / (label + '-message.txt')).write_text(message)
    run(label + '-commitlint', [str(NODE), str(REPO / 'node_modules/@commitlint/cli/cli.js'),
        '--config', str(REPO / 'commitlint.config.mjs'), '--color=false'], payload=message)
    git(label + '-diff-check', 'diff', '--cached', '--check')
    git(label + '-commit', 'commit', '-F', str(OUT / (label + '-message.txt')))
    return git(label + '-source', 'rev-parse', 'HEAD')


try:
    run('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(REPO)], ROOT)
    git('origin', 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Builds.git')
    git('fetch-base', 'fetch', '--no-recurse-submodules', 'origin', 'refs/heads/4.44.x', 'refs/tags/v4.44.14:refs/tags/v4.44.14')
    assert git('live-base', 'rev-parse', 'FETCH_HEAD') == BASE
    git('checkout', 'checkout', '-b', BRANCH, BASE)
    patch = git('existing-patched-catalog', 'show', OBSERVED_PATCH_SOURCE + ':Props/Directory.Packages.props')
    prior = (REPO / 'Props/Directory.Packages.props').read_bytes()
    text = prior.decode()
    replacements = []
    stable = re.compile(r'(<PackageVersion Include="(Aspire\.[^"]+)" Version=")([^"]+)("\s*/>)')
    known = {m.group(2): m.group(3) for m in stable.finditer(patch)}
    def replace(match):
        name, old = match.group(2), match.group(3)
        assert old.startswith('13.6.4') and known[name].startswith('13.6.5')
        replacements.append({'package': name, 'old': old, 'new': known[name]})
        return match.group(1) + known[name] + match.group(4)
    changed = stable.sub(replace, text)
    old_sdk = '<HexalithAspireAppHostSdkVersion>13.6.4</HexalithAspireAppHostSdkVersion>'
    assert changed.count(old_sdk) == 1
    changed = changed.replace(old_sdk, old_sdk.replace('13.6.4', '13.6.5'))
    assert len(replacements) == 13
    (REPO / 'Props/Directory.Packages.props').write_bytes(changed.encode())
    old_versions = {e.attrib['Include']: e.attrib['Version'] for e in ET.fromstring(prior).iter('PackageVersion')}
    new_versions = {e.attrib['Include']: e.attrib['Version'] for e in ET.fromstring(changed).iter('PackageVersion')}
    assert set(old_versions) == set(new_versions)
    assert {k for k in old_versions if old_versions[k] != new_versions[k]} == {r['package'] for r in replacements}
    shutil.copyfile(REPO / 'Tools/package-version-audit.json', OUT / 'prior-audit.json')
    run('npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-fund'])
    run('signatures', ['npm', 'audit', 'signatures'])
    git('stage-catalog', 'add', '--', 'Props/Directory.Packages.props')
    catalog_commit = commit('catalog', 'fix(deps): patch the 13.6 Aspire hosting cohort\n')
    run('bounded-aspire-audit', ['pwsh', '-NoProfile', '-File', 'Tools/audit-central-package-versions.ps1',
        '-ChangedFamily', 'aspire', '-PriorAuditPath', str(OUT / 'prior-audit.json'),
        '-OutputPath', str(REPO / 'Tools/package-version-audit.json')], timeout=600)
    old = json.loads((OUT / 'prior-audit.json').read_text())
    new = json.loads((REPO / 'Tools/package-version-audit.json').read_text())
    assert new['snapshot']['refreshedFamilies'] == ['aspire']
    old_packages = {p['id']: p for p in old['packages']}
    assert all(p == old_packages[p['id']] for p in new['packages'] if p['family'] != 'aspire')
    run('normal-audit-validator', ['pwsh', '-NoProfile', '-File', 'Tools/validate-package-version-audit.ps1'])
    prior_result = json.loads((AREA / 'builds-aspire-security-01/result.json').read_text())
    prior_commands = {c['label']: c['exitCode'] for c in prior_result['commands']}
    assert prior_commands['normal-platform-catalog-validator'] == prior_commands['catalog-controls'] == 0
    assert prior_result['base'] == BASE
    # Only the operator output path and disallowed commit type changed. Catalog
    # bytes still derive from the same immutable base and existing patch source.
    RECORD['unchangedCatalogControlsReused'] = {
        'normalPlatformCatalogValidator': 'builds-aspire-security-01/normal-platform-catalog-validator.log',
        'catalogControls': 'builds-aspire-security-01/catalog-controls.log',
        'sameBase': BASE, 'samePatchSource': OBSERVED_PATCH_SOURCE,
        'catalogRawSha256': hashlib.sha256(subprocess.check_output(['git','show','HEAD:Props/Directory.Packages.props'],cwd=REPO)).hexdigest(),
        'noPublishedCiOrAcceptanceCredit': True}

    git('stage-audit', 'add', '--', 'Tools/package-version-audit.json')
    current = commit('audit', 'build(deps): record the bounded Aspire security audit\n')
    run('version-analysis', [str(NODE), str(AREA / 'analyze-maintenance-version.mjs'), str(REPO), BASE,
        '4.44.x', str(OUT / 'version.json'), ''])
    git('complete-diff', 'diff', '--binary', BASE, current)
    git('bundle', 'bundle', 'create', str(OUT / 'Builds.bundle'), 'refs/heads/' + BRANCH, '^' + BASE)
    shutil.copyfile(REPO / 'Tools/package-version-audit.json', OUT / 'package-version-audit.json')
    RECORD.update(status='verified-local-security-proposal', catalogCommit=catalog_commit, source=current,
        branch=BRANCH, replacements=replacements, appHostSdk={'old': '13.6.4', 'new': '13.6.5'},
        otherPackagePinsUnchanged=True, otherAuditFamiliesUnchanged=True,
        catalogRawSha256=hashlib.sha256(subprocess.check_output(['git', 'show', 'HEAD:Props/Directory.Packages.props'], cwd=REPO)).hexdigest())
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    if REPO.exists():
        artifact = REPO / 'Tools/package-version-audit.json'
        if artifact.is_file(): shutil.copyfile(artifact, OUT / 'package-version-audit.json')
        subprocess.run(['git','bundle','create',str(OUT/'recoverable.bundle'),'refs/heads/'+BRANCH,'^'+BASE],cwd=REPO,capture_output=True)
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
