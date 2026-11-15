"""Bind the final strengthened ref assertion to its fresh real-Git test run."""
from pathlib import Path
import hashlib, json, os, re, subprocess, sys

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent
path = root / 'eng/test_current_tuple_source.py'
def sha(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()
before = sha(path)
command = [sys.executable, '-B', 'eng/test_current_tuple_source.py']
with (out / 'current-source-final.log').open('x') as log:
    result = subprocess.run(command, cwd=root, stdout=log, stderr=subprocess.STDOUT, timeout=1200)
text = (out / 'current-source-final.log').read_text()
assert result.returncode == 0 and re.search(r'Ran 16 tests in ', text) and 'skipped=' not in text and '\nOK\n' in text
assert before == sha(path)
original = json.loads((out / 'candidate.json').read_bytes())
differences = {name: {'testedCandidateSha256': digest, 'finalSha256': sha(root / name)}
               for name, digest in original['sourceFiles'].items() if sha(root / name) != digest}
assert set(differences) == {'eng/test_current_tuple_source.py'}
summary = {'passed': True, 'executed': 16, 'skipped': 0, 'command': command, 'cwd': str(root),
           'sourceSha256': before, 'sourceUnchangedDuringExecution': True,
           'logSha256': sha(out / 'current-source-final.log'), 'differencesFromPackageCandidate': differences,
           'productionAndCompositionSourceUnchanged': True, 'metadataOnlyDelta': {
               'path': '.gitattributes', 'finalSha256': sha(root / '.gitattributes'),
               'reason': 'Remove the second obsolete transport-class whitespace attribute.'},
           'hostedMainAcceptance': False}
(out / 'final-source-summary.json').write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps(summary, indent=2))
