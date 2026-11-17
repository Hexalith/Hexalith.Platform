"""Validate one unaccepted historical catalog delta in disposable local Git only."""
from pathlib import Path
helper = Path(__file__).with_name('check-coherent-caller.py').read_text()
prefix = helper.split('paths = [')[0].replace("coherent-caller-2'", "historical-signalr-proposal'")
prefix = prefix.replace("BASE = '4eca9e8fbad451a602a9ec7d2d6384345b6b0e4e'", "BASE = '11053e562a2cba0196354cec655c01677e2afd46'")
exec(compile(prefix, str(__file__), 'exec'))
import difflib
proposal = ROOT / 'artifacts/2-8-continuation-20261117/ordinary-proof-2'
files = json.loads((proposal / 'projected-files.json').read_text())
for path, digest in files.items():
    content = (proposal / 'changed-files' / path).read_bytes()
    assert hashlib.sha256(content).hexdigest() == digest
    destination = WORK / path
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(content)
ENV['PATH'] = '/home/administrator/.nvm/versions/node/v24.22.1/bin:' + ENV['PATH']
catalog = WORK / 'references/Hexalith.Builds'
props = catalog / 'Props/Directory.Packages.props'
before = props.read_bytes()
old = b'<PackageVersion Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="10.0.14" />'
new = b'<PackageVersion Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="10.0.15" />'
assert before.count(old) == 1
props.write_bytes(before.replace(old, new))
(OUT / 'catalog-10.0.15.patch').write_text(''.join(difflib.unified_diff(before.decode().splitlines(True), props.read_text().splitlines(True), fromfile='a/Props/Directory.Packages.props', tofile='b/Props/Directory.Packages.props')))
message = Path(os.environ['TMPDIR']) / 'producer-message.txt'
message.write_text('fix(catalog): patch the redis signalr dependency\n')
run('producer-npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-audit', '--no-fund'], cwd=catalog)
run('producer-commitlint', ['node', 'node_modules/@commitlint/cli/cli.js', '--edit', str(message)], cwd=catalog)
run('producer-stage', ['git', 'add', 'Props/Directory.Packages.props'], cwd=catalog)
run('producer-diff-check', ['git', 'diff', '--cached', '--check'], cwd=catalog)
run('producer-local-commit', ['git', '-c', 'user.name=Codex', '-c', 'user.email=codex@openai.com', '-c', 'commit.gpgsign=false', 'commit', '-F', str(message)], cwd=catalog)
catalog_head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=catalog, text=True).strip()
run('caller-npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-audit', '--no-fund'])
message.write_text('test(ci): evaluate the historical catalog patch\n')
run('caller-commitlint', ['node', 'node_modules/@commitlint/cli/cli.js', '--edit', str(message)])
run('caller-stage', ['git', 'add', '--', *files, 'references/Hexalith.Builds'])
run('caller-local-commit', ['git', '-c', 'user.name=Codex', '-c', 'user.email=codex@openai.com', '-c', 'commit.gpgsign=false', 'commit', '-F', str(message)])
caller_head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=WORK, text=True).strip()
ENV['GITHUB_ENV'] = str(Path(os.environ['TMPDIR']) / 'github-env.txt')
run('prepare-proposed-catalog', ['python3', '-B', 'references/Hexalith.Builds/Github/prepare-module-build/prepare_module_build.py', '--manifest', 'module/hexalith.module-manifest.v2.json'], required=False)
if RESULTS['prepare-proposed-catalog']['exitCode'] == 0:
    prepared = dict(line.split('=', 1) for line in Path(ENV['GITHUB_ENV']).read_text().splitlines())
    ENV.update(prepared)
    mapping = Path(prepared['CustomAfterMicrosoftCommonProps']).parent / 'mapping.json'
    (OUT / 'proposed-mapping.json').write_bytes(mapping.read_bytes())
    run('actual-test-project-build', ['dotnet', 'build', 'tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj', '--configuration', 'Release', '--disable-build-servers', '-m:1', '-warnaserror', '-p:UseHexalithProjectReferences=false', '-p:UseNuGetDeps=true'], timeout=1200, required=False)
record = {'classification': 'local-unaccepted-producer-proposal-only', 'originalCatalog': pins['references/Hexalith.Builds'], 'localProposedCatalog': catalog_head,
    'originalCaller': BASE, 'localCaller': caller_head, 'platformUnchanged': pins['references/Hexalith.Platform'],
    'singleCatalogDelta': {'package': 'Microsoft.AspNetCore.SignalR.StackExchangeRedis', 'from': '10.0.14', 'to': '10.0.15'},
    'rootWorkspacePinsChanged': False, 'historicalProofEligibleClaimed': False, 'nativeExactIdentityAssertionsUpdated': False,
    'remoteOperations': False, 'publication': False, 'auditSuppression': False, 'buildClaimOnly': True, 'candidateAcceptance': False,
    'results': RESULTS}
(OUT / 'proposal-validation.json').write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps(record), flush=True)
