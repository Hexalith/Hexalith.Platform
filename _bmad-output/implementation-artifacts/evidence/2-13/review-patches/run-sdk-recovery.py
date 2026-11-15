"""Repeat source-SDK metadata controls with their required prepared asset location."""
from pathlib import Path
import hashlib, json, os, signal, subprocess, sys, urllib.request, zipfile

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent / 'sdk-recovery'
out.mkdir()
scratch = Path(os.environ['TMPDIR'])
candidate = scratch / 'platform'
records = []

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def run(label, command, cwd):
    with (out / (label + '.log')).open('x') as log:
        child = subprocess.Popen(command, cwd=cwd, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            code = child.wait(timeout=1200)
        except subprocess.TimeoutExpired:
            os.killpg(child.pid, signal.SIGTERM)
            try:
                child.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(child.pid, signal.SIGKILL)
                child.wait()
            code = 'timeout'
    records.append({'label': label, 'command': command, 'cwd': str(cwd), 'exitCode': code, 'logSha256': sha(out / (label + '.log'))})
    (out / 'commands.json').write_text(json.dumps(records, indent=2) + '\n')
    print(label, code, flush=True)
    assert code == 0

os.environ.update(NUGET_PACKAGES=str(scratch / 'packages'), MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0', UseSharedCompilation='false')
run('prepare-candidate', [sys.executable, '-B', str(out.parent.parent / 'prepare-candidate.py'), str(root), str(candidate), str(out / 'candidate.json')], root)
manifest = json.loads((out.parent / 'candidate.json').read_bytes())['sourceFiles']
differences = {path: {'compositionCandidateSha256': digest, 'metadataCandidateSha256': sha(candidate / path)}
               for path, digest in manifest.items() if sha(candidate / path) != digest}
assert set(differences) == {'eng/test_current_tuple_source.py'}
(out / 'source-comparison.json').write_text(json.dumps(differences, indent=2) + '\n')
binding = json.loads((candidate / 'eng/builds-tuple.json').read_bytes())
publication = binding['publication']
version = publication['version']
archive = scratch / ('hexalith.builds.module.cli.' + version + '.nupkg')
with urllib.request.urlopen('https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/' + version + '/' + archive.name, timeout=90) as response:
    archive.write_bytes(response.read())
assert sha(archive) == publication['archiveSha256']
run('signature', ['dotnet', 'nuget', 'verify', str(archive), '--all'], candidate)
extracted = scratch / 'published'
with zipfile.ZipFile(archive) as package:
    for entry in package.infolist():
        if entry.is_dir():
            continue
        destination = (extracted / entry.filename).resolve()
        assert destination.is_relative_to(extracted) and not destination.exists()
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(package.read(entry))
tool = extracted / 'tools/net11.0/any'
assert sha(tool / 'Hexalith.Builds.Tooling.dll') == publication['toolingSha256']
properties = ['-p:UseHexalithProjectReferences=false', '-p:HexalithBuildsRoot=' + str(candidate / 'references/Hexalith.Builds'),
              '-p:HexalithBuildsPublishedToolRoot=' + str(tool), '-p:HexalithEventStoreVersion=4.2.0',
              '-p:HexalithTenantsVersion=7.0.7', '-p:HexalithMemoriesVersion=3.0.27', '-p:HexalithPartiesVersion=3.1.1', '-p:HexalithMcpCliVersion=2.0.1']
run('restore', ['dotnet', 'restore', 'Hexalith.Platform.slnx', '--artifacts-path', str(scratch / 'build'), *properties], candidate)
run('source-sdk', [sys.executable, '-B', 'eng/test_source_sdk_propagation.py', '--evidence', str(out / 'observations'), '--artifacts', str(scratch / 'build'), '--published-tool-root', str(tool)], candidate)
result = json.loads((out / 'observations/result.json').read_bytes())
assert result['passed'] and result['executed'] == 28 and result['skipped'] == 0
assert all(sha(candidate / path) == sha(root / path) for path in manifest)
summary = {'passed': True, 'executed': 28, 'skipped': 0, 'candidate': json.loads((out / 'candidate.json').read_bytes()),
           'productionAndCompositionSourceEqualTo565CaseCandidate': True, 'sourceFilesMatchFinalWorktree': True,
           'archiveSha256': sha(archive), 'hostedMainAcceptance': False,
           'diagnostic': 'The preceding source-SDK attempt lacked --artifacts and failed four asset-dependent evaluations; no product source changed.'}
(out / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
