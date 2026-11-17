"""One finite genuine-source metadata guard check; never executes an AppHost or service."""
from pathlib import Path
helper = Path(__file__).with_name('check-coherent-caller.py').read_text()
prefix = helper.split('paths = [')[0].replace("coherent-caller-2'", "ordinary-runtime-guards-3'")
prefix = prefix.replace("BASE = '4eca9e8fbad451a602a9ec7d2d6384345b6b0e4e'", "BASE = '11053e562a2cba0196354cec655c01677e2afd46'")
exec(compile(prefix, str(__file__), 'exec'))
manifest = WORK / '.config/dotnet-tools.json'
data = json.loads(manifest.read_text())
assert data['tools']['hexalith.builds.module.cli']['version'] == '4.44.13'
data['tools']['hexalith.builds.module.cli']['version'] = '4.48.2'
manifest.write_text(json.dumps(data, indent=2) + '\n')
ENV['PATH'] = '/home/administrator/.nvm/versions/node/v24.22.1/bin:' + ENV['PATH']
run('pinned-npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-audit', '--no-fund'], timeout=300)
message = 'test(ci): bind the released lifecycle tool'
candidate = Path(os.environ['TMPDIR']) / 'candidate.txt'
candidate.write_text(message + '\n')
run('pinned-commitlint', ['node', 'node_modules/@commitlint/cli/cli.js', '--edit', str(candidate)])
run('stage-tool', ['git', 'add', '.config/dotnet-tools.json'])
run('commit-tool', ['git', '-c', 'user.name=Codex', '-c', 'user.email=codex@openai.com', '-c', 'commit.gpgsign=false', 'commit', '-F', str(candidate)])
head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=WORK, text=True).strip()
(OUT / 'temporary-source.json').write_text(json.dumps({'parent': BASE, 'commit': head, 'onlyChange': '.config/dotnet-tools.json', 'remoteOperations': False}, indent=2) + '\n')
ENV['GITHUB_ENV'] = str(Path(os.environ['TMPDIR']) / 'github-env.txt')
run('prepare-real-4482', ['python3', '-B', 'references/Hexalith.Builds/Github/prepare-module-build/prepare_module_build.py', '--manifest', 'module/hexalith.module-manifest.v2.json'])
prepared = dict(line.split('=', 1) for line in Path(ENV['GITHUB_ENV']).read_text().splitlines())
mapping = Path(prepared['CustomAfterMicrosoftCommonProps']).parent / 'mapping.json'
(OUT / 'mapping.json').write_bytes(mapping.read_bytes())
tool_root = Path('/home/administrator/.nuget/packages/hexalith.builds.module.cli/4.48.2/tools/net10.0/any')
assert hashlib.sha256((tool_root.parents[2] / 'hexalith.builds.module.cli.4.48.2.nupkg').read_bytes()).hexdigest() == '9b7c50125f0ddfc953c1c3d892cddec5ba2fa1d8741bd9e82d77669b4e86ee92'
project = Path(os.environ['TMPDIR']) / 'metadata-guard'
project.mkdir()
(project / 'global.json').write_text('{"sdk":{"version":"10.0.403","rollForward":"disable"}}\n')
(project / 'guard.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Reference Include="' + str(tool_root) + '/*.dll" /></ItemGroup></Project>\n')
(project / 'Program.cs').write_bytes(Path(__file__).with_name('ordinary-runtime-guard.cs').read_bytes())
(OUT / 'harness.cs').write_bytes((project / 'Program.cs').read_bytes())
run('build-metadata-guard', ['dotnet', 'build', '--configuration', 'Release', '--disable-build-servers', '-m:1'], cwd=project)
run('execute-metadata-guard', ['dotnet', str(project / 'bin/Release/net10.0/guard.dll'), str(mapping), str(OUT / 'guard-result.json'), str(tool_root)], cwd=WORK, timeout=1200, required=False)
run('final-source-cleanliness', ['git', 'status', '--porcelain', '--untracked-files=normal'])
raise SystemExit(RESULTS['execute-metadata-guard']['exitCode'])
