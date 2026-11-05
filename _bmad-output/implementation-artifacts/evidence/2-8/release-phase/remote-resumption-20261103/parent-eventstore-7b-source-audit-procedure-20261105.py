"""Independently audit the bounded EventStore fixture correction; no remote mutation."""
import collections
import datetime
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path('/home/administrator/.cache/hexalith/story-2-8/eventstore-current-ci-correction-20261105/Hexalith.EventStore')
EVIDENCE = ROOT.parent / 'correction-evidence'
OUTPUT = Path(__file__).with_name('parent-eventstore-7b-source-and-control-audit-20261105.json')

def sha(data):
    return hashlib.sha256(data).hexdigest()

def read(name):
    return json.loads((EVIDENCE / name).read_text())

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)

def tree(revision):
    return {record.split(b'\t', 1)[1].decode(): record.split(b'\t', 1)[0].decode()
            for record in git('ls-tree', '-rz', revision).split(b'\0') if record}

def main():
    checkpoint = read('eventstore-current-ci-correction-checkpoint.json')
    base, head = checkpoint['base'], checkpoint['head']
    assert head == '7b890438e50df663a2762ccc44c0bdd0a8214d7b'
    assert git('rev-parse', 'HEAD').decode().strip() == head
    assert not git('status', '--porcelain')
    assert git('rev-list', '--parents', '-n', '1', head).decode().split() == [head, base]
    index = read('eventstore-current-ci-correction-evidence-index.json')
    items = index['items']
    assert len(items) == len({item['path'] for item in items}) == index['files'] == 84
    total = 0
    for item in items:
        path = EVIDENCE / item['path']
        assert path.resolve().is_relative_to(EVIDENCE.resolve()) and not path.is_symlink()
        data = path.read_bytes()
        assert len(data) == item['bytes'] and sha(data) == item['sha256'], item['path']
        total += len(data)
    assert total == index['totalBytes'] == 29844237
    before, after = tree(base), tree(head)
    changed = sorted(name for name in before.keys() | after.keys() if before.get(name) != after.get(name))
    assert changed == sorted(checkpoint['changedPaths']) and len(changed) == 3
    patch = git('diff', '--binary', '--full-index', base, head)
    assert patch == (EVIDENCE / 'complete-owning-correction.patch').read_bytes()
    assert len(patch) == 11850 and sha(patch) == checkpoint['completePatch']['sha256']
    for item in read('tested-source-before-commit.json')['paths']:
        data = git('show', head + ':' + item['path'])
        assert sha(data) == item['sha256']
        assert after[item['path']].split()[-1] == item['gitBlob']
        assert (ROOT / item['path']).read_bytes().replace(b'\r\n', b'\n') == data.replace(b'\r\n', b'\n')
    outputs = read('tested-output-bodies-before-commit.json')
    assert len(outputs) == len({item['path'] for item in outputs}) == 1108
    for item in outputs:
        data = (ROOT / item['path']).read_bytes()
        assert len(data) == item['bytes'] and sha(data) == item['sha256'], item['path']
    for item in read('actual-external-tenant702-component-bodies.json'):
        assert sha(Path(item['authenticatedPackageBody']).read_bytes()) == item['sha256']
        assert sha(Path(item['actualOutputBody']).read_bytes()) == item['sha256']
    cases = read('actual-corrected-124-executed-cases.json')
    actual, groups = [], []
    for group in cases['groups']:
        data = (EVIDENCE / group['xml']).read_bytes()
        assert sha(data) == group['sha256']
        root = ET.fromstring(data)
        selected = list(root.iter('test'))
        assert len(selected) == group['cases']
        assert all(test.attrib['result'] == 'Pass' for test in selected)
        assert all(int(a.attrib.get('errors', 0)) == 0 for a in root.iter('assembly'))
        actual.extend(dict(test.attrib, group=group['group']) for test in selected)
        groups.append({'group': group['group'], 'actualPassed': len(selected), 'xmlSha256': sha(data)})
    assert len(actual) == len({test['id'] for test in actual}) == 124
    assert len({test['name'] for test in actual}) == 123
    assert {(t['id'], t['name'], t['result']) for t in actual} == {(t['id'], t['name'], t['result']) for t in cases['cases']}
    for name in ['unchanged-current-domain-exact-first.xml', 'unchanged-current-secrets-exact-first.xml']:
        selected = list(ET.parse(EVIDENCE / name).getroot().iter('test'))
        assert len(selected) == 1 and selected[0].attrib['result'] == 'Fail'
    causal = read('causal-denial-privacy-conclusion.json')
    assert causal['bothResponses503'] and causal['verifierCalls'] == 2
    assert causal['processorCalls'] == causal['queryHandlerCalls'] == 0
    assert causal['processLoggerWarningEnabled'] and not causal['queryLoggerWarningEnabled']
    assert causal['actualCapturedDenials'] == 1
    # All production, scanner, workflow, pin, and native contract blobs are equal by the complete tree comparison.
    message = (EVIDENCE / 'validated-commit-message.txt').read_bytes()
    assert git('show', '-s', '--format=%B', head).rstrip() == message.rstrip()
    assert sha(message) == read('owning-commitlint-exact-message.json')['messageSha256']
    assert json.loads((ROOT / 'node_modules/@commitlint/cli/package.json').read_text())['version'] == '21.2.4'
    check = subprocess.run(['node', 'node_modules/@commitlint/cli/cli.js', '--edit', str(EVIDENCE / 'validated-commit-message.txt'), '--config', 'commitlint.config.mjs', '--verbose'], cwd=ROOT, capture_output=True, text=True)
    assert check.returncode == 0
    audit = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'base': base, 'head': head,
             'clean': True, 'parent': base, 'changedPaths': changed, 'completePatchBytes': len(patch),
             'completePatchSha256': sha(patch), 'unchangedOtherTrackedObjects': len(before) - len(changed),
             'evidenceFilesRehashed': len(items), 'evidenceBytesRehashed': total,
             'indexSha256': sha((EVIDENCE / 'eventstore-current-ci-correction-evidence-index.json').read_bytes()),
             'actualOutputDllsRehashedUnchanged': len(outputs), 'actualPassedExecutions': len(actual),
             'distinctActualXunitIds': len({test['id'] for test in actual}), 'distinctDisplayNames': 123,
             'groups': groups, 'bothOriginalCausalRedCasesRetained': True, 'causalPrivacyObservation': causal,
             'exactFullMessageRevalidatedWithOwning21_2_4': True, 'commitlintOutput': check.stdout,
             'independentSourceAssessment': 'Three test files reconcile the existing protected-query diagnostic policy and runtime fixture canaries. Both 503s, two verifier identities, zero domain execution, process bounded denial and query diagnostic absence are asserted separately. Production policy and strict scanner are unchanged.',
             'nativeAcceptance': False, 'storyComplete': False}
    assert not OUTPUT.exists()
    OUTPUT.write_text(json.dumps(audit, indent=2) + '\n')
    print(json.dumps({k: audit[k] for k in ['head', 'evidenceFilesRehashed', 'actualOutputDllsRehashedUnchanged', 'actualPassedExecutions', 'distinctActualXunitIds', 'distinctDisplayNames', 'unchangedOtherTrackedObjects']}, indent=2))

if __name__ == '__main__':
    main()
