"""Qualify the exact new catalog and synchronize metadata with existing owner pins."""
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
OUT = AREA / 'platform630-source-04'
OUT.mkdir(exist_ok=False)
REPO = Path(os.environ['TMPDIR']) / 'Hexalith.Platform'
BASE = '11719c5cf51c2b2b080b89e0e301c2ef8dbacc46'
BRANCH = 'fix/story-2-8-builds-6-3'
PREVIOUS = json.loads((AREA / 'platform630-source-03/result.json').read_text())
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent) + ':' + os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RECORD = {'base': BASE, 'buildsVersion': '6.3.0', 'remoteMutations': [], 'commands': []}


def run(label, argv, cwd=REPO, payload=None, timeout=300, required=True):
    result = subprocess.run(argv, cwd=cwd, env=ENV, input=payload, capture_output=True, text=True, timeout=timeout)
    (OUT / (label + '.log')).write_text(result.stdout + result.stderr)
    RECORD['commands'].append({'label': label, 'exitCode': result.returncode})
    print(label, result.returncode, flush=True)
    if required and result.returncode:
        raise RuntimeError(label)
    return result


def git(label, *args):
    return run(label, ['git', '-c', 'submodule.recurse=false', *args]).stdout.strip()


def replace(path, before, after, count=1):
    text = path.read_text()
    assert text.count(before) == count, (path, before, text.count(before))
    path.write_text(text.replace(before, after))


try:
    run('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT), str(REPO)], ROOT)
    git('origin', 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Platform.git')
    git('recover', 'fetch', str(AREA / 'platform630-source-03/Platform.bundle'), 'refs/heads/' + BRANCH)
    git('checkout', 'checkout', '-b', BRANCH, PREVIOUS['source'])
    catalog = AREA / 'builds630-catalog.props'
    raw = catalog.read_bytes()
    assert hashlib.sha256(raw).hexdigest() == 'aa423407a51ca2ee6507c49c94effd9c3b2da144d24158a1223dd863550d22a4'
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == 'db85d885ae1b7478ee2d053a5b54b9a43bff9ae7'
    (REPO / 'eng/fixtures/builds-6.3.0-Directory.Packages.props').write_bytes(raw)
    (REPO / 'eng/fixtures/builds-6.3.0-binding.json').write_bytes((AREA / 'builds630-binding.json').read_bytes())
    attributes = REPO / '.gitattributes'
    attributes.write_text(attributes.read_text() + '\n# Preserve the authenticated Builds 6.3.0 catalog and publication fixture bytes.\neng/fixtures/builds-6.3.0-Directory.Packages.props text eol=lf\neng/fixtures/builds-6.3.0-binding.json text eol=lf\n')
    view = REPO / 'eng/current_catalog_view.py'
    replace(view, '\ndef catalog_profile(binding=None) -> str:',
        '\nGENERATION630_CATALOG = {\n    "version": "6.3.0",\n'
        '    "sourceRevision": "7afc4981e21bc2fcce687199a3fd2c61fe87336e",\n'
        '    "blob": "db85d885ae1b7478ee2d053a5b54b9a43bff9ae7",\n'
        '    "sha256": "aa423407a51ca2ee6507c49c94effd9c3b2da144d24158a1223dd863550d22a4",\n}\n\n\ndef catalog_profile(binding=None) -> str:')
    replace(view, '"6.0.5": GENERATION605_CATALOG}.get(', '"6.0.5": GENERATION605_CATALOG, "6.3.0": GENERATION630_CATALOG}.get(')
    replace(view, 'def verify_observations(observed: list[dict], named_sdk: str, other_sdk: str) -> None:',
        'def verify_observations(observed: list[dict], named_sdk: str, other_sdk: str, *,\n'
        '                        generic_openapi: tuple[str, str] = ("11.0.0", "3.11.0")) -> None:')
    replace(view, 'if current and expected_sdk == "4.2.0" else ("11.0.0", "3.11.0"))))',
        'if current and expected_sdk == "4.2.0" else generic_openapi)))')
    replace(view, 'def verify_current_owner_observations(observed: list[dict], *, generic_tenants: str = "7.0.7", generic_mcp: str = "2.0.1") -> None:',
        'def verify_current_owner_observations(observed: list[dict], *, generic_tenants: str = "7.0.7",\n'
        '                                      generic_mcp: str = "2.0.1", generic_sdk: str = "5.1.0",\n'
        '                                      generic_openapi: tuple[str, str] = ("11.0.0", "3.11.0")) -> None:')
    replace(view, 'sdk = "4.2.0" if named else "5.1.0"', 'sdk = "4.2.0" if named else generic_sdk')
    replace(view, '("10.0.15", "2.13.0") if named else ("11.0.0", "3.11.0"))))',
        '("10.0.15", "2.13.0") if named else generic_openapi)))')
    replace(view, '    observed = evaluate_views(raw)\n    names = [item["Identity"] for item in observed]\n',
        '    version = binding["publication"]["version"] if binding is not None else None\n'
        '    generic_sdk = "5.2.0" if version == "6.3.0" else "5.1.0"\n'
        '    generic_openapi = ("11.0.0", "3.11.1" if version == "6.3.0" else "3.11.0")\n'
        '    observed = evaluate_views(raw)\n    names = [item["Identity"] for item in observed]\n')
    replace(view, '"5.1.0" if profile in ("current-sdk51", "current-sdk42") else "5.0.0")',
        'generic_sdk if profile in ("current-sdk51", "current-sdk42") else "5.0.0", generic_openapi=generic_openapi)')
    replace(view, 'verify_observations(collecting, "4.2.0", "4.2.0")',
        'verify_observations(collecting, "4.2.0", "4.2.0", generic_openapi=generic_openapi)')
    replace(view, 'binding["publication"]["version"] in ("6.0.2", "6.0.3", "6.0.5")',
        'version in ("6.0.2", "6.0.3", "6.0.5", "6.3.0")')
    replace(view, 'generic_tenants = "7.0.7" if binding["publication"]["version"] == "6.0.2" else "8.0.0"',
        'generic_tenants = "8.0.1" if version == "6.3.0" else "7.0.7" if version == "6.0.2" else "8.0.0"')
    replace(view, 'generic_mcp = "3.0.0" if binding["publication"]["version"] == "6.0.5" else "2.0.1"',
        'generic_mcp = "3.0.0" if version in ("6.0.5", "6.3.0") else "2.0.1"')
    replace(view, 'verify_current_owner_observations(owner_observed, generic_tenants=generic_tenants, generic_mcp=generic_mcp)',
        'verify_current_owner_observations(owner_observed, generic_tenants=generic_tenants, generic_mcp=generic_mcp,\n'
        '                                          generic_sdk=generic_sdk, generic_openapi=generic_openapi)')
    replace(view, 'if binding["publication"]["version"] == "6.0.5":', 'if version in ("6.0.5", "6.3.0"):')
    replace(view, '"Exact Builds6 SDK5.1 default and finite automatic/explicit SDK4.2 collection views"',
        '"Exact Builds6 SDK" + generic_sdk + " default and finite automatic/explicit SDK4.2 collection views"')
    tests = REPO / 'eng/test_current_catalog_view.py'
    replace(tests, 'GENERATION605_CATALOG, CURRENT_OWNER_CONSUMERS', 'GENERATION605_CATALOG, GENERATION630_CATALOG, CURRENT_OWNER_CONSUMERS')
    replace(tests, '\ndef observe_producer_projects(',
        '\ndef builds630_catalog_body() -> bytes:\n'
        '    return catalog_fixture("builds-6.3.0-Directory.Packages.props", GENERATION630_CATALOG["sourceRevision"],\n'
        '        GENERATION630_CATALOG["blob"], GENERATION630_CATALOG["sha256"])\n\n\ndef observe_producer_projects(')
    new_tests = '''    def test_builds630_preserves_named_sdk42_and_measures_generic_sdk52_without_native_authority(self):
        receipt = self.verify(builds630_catalog_body(), generation6_binding("6.3.0"))
        self.assertEqual(receipt["catalogSha256"], GENERATION630_CATALOG["sha256"])
        for context, count in (("defaultEvaluations", 36), ("collectingEvaluations", 36), ("currentOwnerEvaluations", 7)):
            self.assertEqual(len(receipt[context]), count)
            for item in receipt[context]:
                named = item["Identity"] in (*PLATFORM_CONSUMERS, *CURRENT_OWNER_CONSUMERS)
                with self.subTest(context=context, project=item["Identity"]):
                    self.assertEqual(item["Sdk"], "4.2.0" if named or context == "collectingEvaluations" else "5.2.0")
                    pins = dict(pin.split("=", 1) for pin in item["Pins"].split("|"))
                    self.assertEqual([pins[name] for name in OPENAPI_PACKAGES],
                        ["10.0.15", "2.13.0"] if named else ["11.0.0", "3.11.1"])
                    self.assertEqual(item["CatalogTenantsDefault"], "7.0.7" if named else "8.0.1")
                    self.assertEqual(item["CatalogMemoriesDefault"], "3.0.27" if named else "4.0.0")
                    self.assertEqual(item["CatalogMcpDefault"], "2.0.1" if named else "3.0.0")
        self.assertFalse(receipt["candidateAcceptance"])
        self.assertFalse(receipt["runtimeAcceptance"])

    def test_builds630_refuses_neighbor_release_wrong_source_blob_and_raw_catalog(self):
        from current_catalog_view import catalog_profile
        for fault in ("source", "blob", "sha256", "version"):
            binding = generation6_binding("6.3.0")
            if fault == "source":
                binding["producer"]["sourceRevision"] = binding["publication"]["sourceRevision"] = "e" * 40
            elif fault == "version":
                binding["publication"].update(version="6.3.1", releaseTag="v6.3.1")
            else:
                binding["producer"]["catalog"][fault] = "e" * (40 if fault == "blob" else 64)
                if fault == "sha256": binding["publication"]["catalogSha256"] = "e" * 64
            manifest = json.loads(binding["publication"]["manifestJson"])
            manifest.update(sha=binding["producer"]["sourceRevision"], version=binding["publication"]["version"])
            for package in manifest["packages"]:
                package["path"] = "packages/" + package["id"] + "." + manifest["version"] + ".nupkg"
            encoded = json.dumps(manifest)
            binding["publication"].update(manifestJson=encoded, manifestSha256=hashlib.sha256(encoded.encode()).hexdigest())
            with self.subTest(fault=fault), self.assertRaisesRegex(ValueError, "exact qualified Builds6"):
                catalog_profile(binding)
        with self.assertRaisesRegex(ValueError, "exact producer binding"):
            self.verify(builds605_catalog_body(), generation6_binding("6.3.0"))

    def test_builds630_refuses_named_and_generic_package_family_drift_in_every_context(self):
        raw, binding = builds630_catalog_body(), generation6_binding("6.3.0")
        receipt = self.verify(raw, binding)
        contexts = ("defaultEvaluations", "collectingEvaluations", "currentOwnerEvaluations")
        for context in contexts:
            for index in (0, -1):
                for field in ("Sdk", "Pins", "CatalogTenantsDefault", "TenantsPins", "CatalogMemoriesDefault", "MemoriesPins", "CatalogMcpDefault", "McpPins"):
                    observations = [copy.deepcopy(receipt[name]) for name in contexts]
                    observations[contexts.index(context)][index][field] = "unqualified"
                    with self.subTest(context=context, index=index, field=field), \\
                            patch("current_catalog_view.evaluate_views", side_effect=observations), self.assertRaises(ValueError):
                        self.verify(raw, binding)

'''
    replace(tests, '    def test_exact_consumer_inventory_does_not_implicitly_admit_new_platform_projects(self):',
        new_tests + '    def test_exact_consumer_inventory_does_not_implicitly_admit_new_platform_projects(self):')
    # Acquire only the existing four owner pins for Git-body source binding; change no owner gitlink.
    for owner in ('EventStore', 'Parties', 'Tenants', 'Memories'):
        name = 'Hexalith.' + owner
        selected = git('owner-pin-' + owner, 'rev-parse', 'HEAD:references/' + name)
        target = REPO / 'references' / name
        run('owner-clone-' + owner, ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references' / name), str(target)])
        run('owner-checkout-' + owner, ['git', 'checkout', '--detach', selected], target)
    run('record-owner-binding', ['python3', '-B', '-c',
        'import json,sys; from pathlib import Path; sys.path.insert(0,"eng"); from current_tuple_source import create_binding; '
        'root=Path.cwd(); (root/"eng/current-tuple-source.json").write_text(json.dumps(create_binding(root,"HEAD"),indent=2)+"\\n")'])
    # Real MSBuild evaluations of the exact public catalog; no service is started.
    run('catalog-controls', ['python3', '-B', 'eng/test_current_catalog_view.py'], timeout=900)
    run('source-controls', ['python3', '-B', 'eng/test_current_tuple_source.py'])
    run('npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-fund'])
    run('signatures', ['npm', 'audit', 'signatures'])
    run('source-release-controls', ['npm', 'run', 'test:source-release'])
    message = 'fix(ci): qualify the exact Builds 6.3 catalog and refresh source metadata\n'
    (OUT / 'commit-message.txt').write_text(message)
    run('commitlint', [str(NODE), str(REPO / 'node_modules/@commitlint/cli/cli.js'), '--config', str(REPO / 'commitlint.config.mjs'), '--color=false'], payload=message)
    git('stage', 'add', '--', '.gitattributes', 'eng/current_catalog_view.py', 'eng/test_current_catalog_view.py',
        'eng/current-tuple-source.json', 'eng/fixtures/builds-6.3.0-Directory.Packages.props', 'eng/fixtures/builds-6.3.0-binding.json')
    git('diff-check', 'diff', '--cached', '--check')
    git('commit', 'commit', '-F', str(OUT / 'commit-message.txt'))
    RECORD.update(source=git('source', 'rev-parse', 'HEAD'), tree=git('tree', 'rev-parse', 'HEAD^{tree}'), branch=BRANCH)
    run('actual-source-closure', ['python3', '-B', '-c', 'import sys; from pathlib import Path; sys.path.insert(0,"eng"); '
        'from current_tuple_source import verify_checkout; import json; print(json.dumps(verify_checkout(Path.cwd(),"' + RECORD['source'] + '"),indent=2))'])
    git('source-diff', 'diff', '--binary', BASE, 'HEAD')
    git('bundle', 'bundle', 'create', str(OUT / 'Platform.bundle'), 'refs/heads/' + BRANCH, '^' + BASE)
    current = git('remote-before-push', 'ls-remote', '--heads', 'origin', BRANCH).split()[0]
    assert current == PREVIOUS['source']
    git('push', 'push', '--no-recurse-submodules', 'origin', 'HEAD:refs/heads/' + BRANCH)
    RECORD['remoteMutations'].append({'action': 'normal-fast-forward-update-pr46', 'sha': RECORD['source']})
    RECORD.update(status='updated-pr-awaiting-normal-ci', prUrl=PREVIOUS['prUrl'])
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    if REPO.exists():
        subprocess.run(['git', 'bundle', 'create', str(OUT / 'recoverable.bundle'), 'refs/heads/' + BRANCH, '^' + BASE], cwd=REPO, capture_output=True)
        with (OUT / 'recoverable.patch').open('w') as stream:
            subprocess.run(['git', 'diff', '--binary', PREVIOUS['source']], cwd=REPO, stdout=stream, stderr=subprocess.DEVNULL)
        for name in ('eng/current_catalog_view.py', 'eng/test_current_catalog_view.py', 'eng/current-tuple-source.json'):
            target = OUT / 'changed-files' / name
            target.parent.mkdir(parents=True, exist_ok=True)
            if (REPO / name).exists(): target.write_bytes((REPO / name).read_bytes())
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
