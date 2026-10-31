"""Retain a complete Debug runtime and source/build context at a new path."""
from pathlib import Path
import datetime
import hashlib
import json
import shutil

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
EXTERNAL = Path('/tmp/ext-protection-loop10-source-gaps-7b5a4e938648')
EXTERNAL.mkdir()
runtime = EXTERNAL / 'runtime'
shutil.copytree(ROOT / 'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0', runtime)
entries = [{'path': str(path.relative_to(runtime)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
           for path in sorted(runtime.rglob('*')) if path.is_file()]
(runtime / 'runtime-manifest.json').write_text(json.dumps({'files': entries}, indent=2) + '\n')
source = []
locations = {'platform': ROOT, 'eventstore': ROOT.parent / 'eventstore',
             'commons': ROOT.parent / 'eventstore/references/Hexalith.Commons',
             'tenants': ROOT.parent / 'eventstore/references/Hexalith.Tenants',
             'builds': ROOT / 'references/Hexalith.Builds'}
allowed = {'.cs', '.csproj', '.props', '.targets', '.json', '.py', '.hcl', '.yaml', '.yml', '.sh'}
for name, place in locations.items():
    paths = []
    if name == 'platform':
        for folder in ('src', 'tests', 'eng/ext-protection-1', 'deploy/erasure-root'):
            paths.extend((place / folder).rglob('*'))
        paths.append(place / 'eng/ext-secrets-1/probe_raft_non_revival.py')
    else:
        paths.extend((place / 'src').rglob('*'))
        if name == 'eventstore':
            paths.extend((place / 'tests/Hexalith.EventStore.Server.Tests/Security').glob('*.cs'))
    paths.extend(place.glob('*'))
    for path in sorted(set(paths)):
        if not path.is_file() or path.is_symlink() or path.suffix not in allowed \
                or {'bin', 'obj', 'node_modules', '__pycache__', 'references'} & set(path.relative_to(place).parts):
            continue
        relative = path.relative_to(place)
        data = path.read_bytes()
        target = EXTERNAL / 'source' / name / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        source.append({'repository': name, 'path': str(relative), 'sha256': hashlib.sha256(data).hexdigest()})
(OUT / 'source-manifest.json').write_text(json.dumps({'roots': {key: str(value) for key, value in locations.items()},
    'frozenSourceRoot': str(EXTERNAL / 'source'), 'files': source}, indent=2) + '\n')
shutil.copyfile(runtime / 'runtime-manifest.json', OUT / 'runtime-manifest.json')
binding = {'capturedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'runtime': str(runtime),
    'runtimeFiles': len(entries), 'manifestSha256': hashlib.sha256((runtime / 'runtime-manifest.json').read_bytes()).hexdigest(),
    'assemblySha256': hashlib.sha256((runtime / 'Hexalith.Platform.Protection.Tests.dll').read_bytes()).hexdigest(),
    'sourceFiles': len(source), 'sourceManifestSha256': hashlib.sha256((OUT / 'source-manifest.json').read_bytes()).hexdigest()}
(OUT / 'freeze.json').write_text(json.dumps(binding, indent=2) + '\n')
print(json.dumps(binding))
