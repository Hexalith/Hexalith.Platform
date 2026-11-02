"""Check normal and optimized package validation against copied-bundle corruptions."""
from pathlib import Path
import copy
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

BUNDLE = Path(__file__).resolve().parent
ROOT = BUNDLE.parents[2]


def rehash(directory, relative):
    path = directory / 'artifact-inventory.json'
    inventory = json.loads(path.read_text())
    member = directory / relative
    for item in inventory['files']:
        if item['path'] == relative:
            item.update(sha256=hashlib.sha256(member.read_bytes()).hexdigest(), bytes=member.stat().st_size)
            path.write_text(json.dumps(inventory, indent=2) + '\n')
            return
    raise RuntimeError('Missing mutation member in inventory: ' + relative)


def mutate_xml(directory, kind):
    name = 'review-fix-verification/conversations-tests.xml'
    path = directory / name
    tree = ET.parse(path)
    assembly = tree.find('./assembly')
    if kind == 'extra-assembly':
        tree.getroot().append(copy.deepcopy(assembly))
    elif kind == 'case-not-run':
        assembly.find('./collection/test').set('result', 'NotRun')
    elif kind == 'aggregate-not-run':
        assembly.set('not-run', '1')
    elif kind == 'foreign-case-class':
        assembly.find('./collection/test').set('type', 'Foreign.Tests')
    elif kind == 'missing-case':
        collection = assembly.find('./collection')
        collection.remove(collection.find('./test'))
    tree.write(path, encoding='unicode')
    rehash(directory, name)


results = []
with tempfile.TemporaryDirectory(prefix='owner-verifier-copies-') as temporary:
    for name in ['clean', 'extra-uninventoried-file', 'missing-inventoried-file', 'extra-assembly',
                 'case-not-run', 'aggregate-not-run', 'foreign-case-class', 'missing-case',
                 'foreign-command-selection', 'runtime-manifest-substitution', 'source-manifest-substitution']:
        directory = Path(temporary) / name
        shutil.copytree(BUNDLE, directory)
        if name == 'extra-uninventoried-file':
            (directory / 'unexpected.txt').write_text('not inventoried\n')
        elif name == 'missing-inventoried-file':
            (directory / 'README.md').unlink()
        elif name in {'extra-assembly', 'case-not-run', 'aggregate-not-run', 'foreign-case-class', 'missing-case'}:
            mutate_xml(directory, name)
        elif name == 'foreign-command-selection':
            relative = 'review-fix-verification/commands.json'
            path = directory / relative
            commands = json.loads(path.read_text())
            command = next(item for item in commands if item['name'] == 'focused-tests')
            command['argv'][command['argv'].index('-class') + 1] = 'Foreign.Tests'
            path.write_text(json.dumps(commands, indent=2) + '\n')
            rehash(directory, relative)
        elif name in {'runtime-manifest-substitution', 'source-manifest-substitution'}:
            relative = 'review-fix-verification/' + ('relocated-runtime-after.json' if name.startswith('runtime') else 'after-source-build-inputs.json')
            path = directory / relative
            manifest = json.loads(path.read_text())
            manifest['files'][0]['sha256'] = '0' * 64
            path.write_text(json.dumps(manifest, indent=2) + '\n')
            rehash(directory, relative)
        for optimized in [False, True]:
            argv = [sys.executable] + (['-O'] if optimized else []) + [str(directory / 'verify-package.py'), '--bundle', str(directory), '--root', str(ROOT)]
            result = subprocess.run(argv, capture_output=True, text=True)
            expected_success = name == 'clean'
            if (result.returncode == 0) != expected_success:
                raise RuntimeError(f'Incorrect verifier outcome for {name}, optimized={optimized}: {result.stdout}{result.stderr}')
            results.append({'case': name, 'optimized': optimized, 'expectedSuccess': expected_success,
                            'exitCode': result.returncode, 'result': result.stdout.strip() if expected_success else result.stderr.strip()})
print(json.dumps({'status': 'Passed', 'sourceSelection': 'Package verifier only; no .NET/native/Live/OQ tests.', 'checks': results}, indent=2))
