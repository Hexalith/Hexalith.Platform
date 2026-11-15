"""Check the final ref/pin inventory and a private merge, without touching source refs."""
from pathlib import Path
import hashlib, json, os, subprocess

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent
scratch = Path(os.environ['TMPDIR'])
paths = ['eng/builds-tuple.json', 'eng/current-tuple-source.json', '.config/dotnet-tools.json',
         'hexalith-platform.json', 'src/Hexalith.Platform.Composition/Declarations/provenance.json', 'apphost.cs']

def git(directory, *arguments, data=None):
    return subprocess.run(['git', '-c', 'core.commitGraph=false', '-c', 'submodule.recurse=false', *arguments],
                          cwd=directory, input=data, check=True, capture_output=True).stdout

def inventory():
    return {'platformRevision': git(root, 'rev-parse', 'HEAD').decode().strip(),
            'files': {path: hashlib.sha256((root / path).read_bytes()).hexdigest() for path in paths},
            'gitlinks': git(root, 'ls-tree', 'HEAD:references').decode().splitlines(),
            'refs': git(root, 'for-each-ref', '--format=%(objectname) %(refname)').decode().splitlines(),
            'tags': git(root, 'for-each-ref', '--format=%(objectname) %(refname)', 'refs/tags/').decode().splitlines(),
            'qualificationBranches': git(root, 'for-each-ref', '--format=%(objectname) %(refname)', 'refs/heads/qualification/').decode().splitlines()}

before = inventory()
(out / 'before-inventory.json').write_text(json.dumps(before, indent=2) + '\n')
assert not before['qualificationBranches']
original = json.loads((out.parent / 'before-inventory.json').read_bytes())
assert before['files'] == original['files']
assert before['gitlinks'] == original['allGitlinks']
assert set(original['tags']).issubset(before['tags'])
assert sum(' refs/tags/archive/qualification' in tag for tag in before['tags']) == 105
assert sum(' refs/tags/qualification/broker-' in tag for tag in before['tags']) == 2

# Build and merge the candidate only in the disposable repository.
private = scratch / 'merge'
git(root, 'clone', '--shared', '--no-checkout', str(root), str(private))
git(private, 'checkout', '--detach', before['platformRevision'])
patch = git(root, 'diff', '--binary', 'HEAD')
git(private, 'apply', '--index', '--binary', '--whitespace=nowarn', '-', data=patch)
git(private, '-c', 'user.name=Candidate verification', '-c', 'user.email=fixture@invalid', '-c', 'commit.gpgsign=false',
    'commit', '-qm', 'test(ci): private final review patch merge subject')
candidate = git(private, 'rev-parse', 'HEAD').decode().strip()
git(private, 'checkout', '--detach', before['platformRevision'])
git(private, '-c', 'user.name=Candidate verification', '-c', 'user.email=fixture@invalid', '-c', 'commit.gpgsign=false',
    'merge', '--no-ff', '--no-edit', candidate)
merged = git(private, 'rev-parse', 'HEAD').decode().strip()
merged_files = {path: hashlib.sha256(git(private, 'show', merged + ':' + path)).hexdigest() for path in paths}
assert merged_files == before['files']
assert git(private, 'ls-tree', merged + ':references').decode().splitlines() == before['gitlinks']
after = inventory()
assert before == after
(out / 'after-inventory.json').write_text(json.dumps(after, indent=2) + '\n')
summary = {'passed': True, 'sourceRevision': before['platformRevision'], 'privateCandidateRevision': candidate,
           'privateMergeRevision': merged, 'privateMergeTree': git(private, 'rev-parse', 'HEAD^{tree}').decode().strip(),
           'pinsAndGitlinksUnchanged': True, 'rootRefInventoryUnchanged': True, 'originalTagsPreserved': len(original['tags']),
           'archivedQualificationTags': 105, 'brokerTags': 2, 'localQualificationBranches': [],
           'remoteOperations': [], 'pushallInvoked': False, 'hostedMainAcceptance': False}
(out / 'integrity-summary.json').write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps(summary, indent=2))
