"""Resource-free fixture-assisted controls; never canonical owner/hosted qualification."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile
import shutil
import unittest

ROOT = Path(__file__).resolve().parents[5]
PARTIES = ROOT / 'references/Hexalith.Parties'
BUILDS = ROOT / 'references/Hexalith.Builds'
PINNED_HELPER_SOURCE = 'b4703ec2247321f867f14cd9caca43fa00190feb'
HELPER_PATH = 'Github/run-module-integration/run_module_integration.py'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--pattern', default='test_*.py')
    parser.add_argument('--helper-source', default=PINNED_HELPER_SOURCE)
    args = parser.parse_args()
    if re.fullmatch(r'[0-9a-f]{40}', args.helper_source) is None:
        parser.error('--helper-source requires a full canonical commit identifier')
    source = subprocess.check_output(['git', '-c', 'core.commitGraph=false', 'rev-parse', '--verify', args.helper_source + '^{commit}'], cwd=BUILDS, text=True).strip()
    if source != args.helper_source:
        parser.error('--helper-source did not resolve to its exact commit')
    body = subprocess.check_output(['git', '-c', 'core.commitGraph=false', 'show', source + ':' + HELPER_PATH], cwd=BUILDS)
    with tempfile.TemporaryDirectory(prefix='story-2-10-helper-fixture-') as temporary:
        helper = Path(temporary) / HELPER_PATH
        helper.parent.mkdir(parents=True)
        helper.write_bytes(body)
        prepare_path = 'Github/prepare-module-build/prepare_module_build.py'
        prepare_body = subprocess.check_output(['git', '-c', 'core.commitGraph=false', 'show', source + ':' + prepare_path], cwd=BUILDS)
        prepare = Path(temporary) / prepare_path
        prepare.parent.mkdir(parents=True)
        prepare.write_bytes(prepare_body)
        print(json.dumps({'classification': 'fixture-assisted-resource-free-controls',
            'helperSource': source, 'helperPath': HELPER_PATH,
            'helperSha256': hashlib.sha256(body).hexdigest(),
            'preparePath': prepare_path, 'prepareSha256': hashlib.sha256(prepare_body).hexdigest(), 'canonicalOwnerChecks': False,
            'nestedSubmoduleInitialized': False, 'candidateAcceptance': False}), flush=True)
        fixture = Path(temporary) / 'parties'
        inputs = ['scripts/qualify-disposable-ci.py', 'scripts/observe-current-owner-startup.py',
                  'tests/ci-lifecycle', '.github/workflows', 'module/hexalith.module-manifest.v2.json',
                  'Hexalith.Parties.slnx', 'Hexalith.Parties.Standalone.slnx', '.config/dotnet-tools.json',
                  'tests/Hexalith.Parties.Platform.LifecycleTests', 'tests/Hexalith.Parties.Platform.IntegrationTests']
        identities = {}
        for name in inputs:
            source = PARTIES / name
            sources = list(source.rglob('*')) if source.is_dir() else [source]
            for source_file in sources:
                if not source_file.is_file() or '__pycache__' in source_file.parts or 'bin' in source_file.parts or 'obj' in source_file.parts:
                    continue
                relative = source_file.relative_to(PARTIES)
                destination = fixture / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source_file, destination)
                identities[str(relative)] = hashlib.sha256(source_file.read_bytes()).hexdigest()
        nested = fixture / 'references/Hexalith.Builds/Github'
        for source_file in (helper, prepare):
            destination = nested / source_file.relative_to(Path(temporary) / 'Github')
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source_file, destination)
        print(json.dumps({'fixtureInputSha256': identities}, sort_keys=True), flush=True)
        suite = unittest.defaultTestLoader.discover(str(fixture / 'tests/ci-lifecycle'), pattern=args.pattern)
        result = unittest.TextTestRunner(verbosity=2).run(suite)
        return 0 if result.wasSuccessful() else 1



if __name__ == '__main__':
    raise SystemExit(main())
