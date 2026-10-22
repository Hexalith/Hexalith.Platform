"""Failure regressions use local fixtures and never contact a Kubernetes cluster."""
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import subprocess
import json
import xml.etree.ElementTree as ET
import tempfile
import threading
import time
import unittest
from unittest.mock import patch

import qualify


class QualifierFailureTests(unittest.TestCase):
    @contextmanager
    def qualification(self):
        with tempfile.TemporaryDirectory(prefix='h45-failure-evidence-') as directory:
            q = qualify.Qualification(Path(directory))
            try:
                yield q
            finally:
                if q.temp.exists():
                    q.created = False
                    q.cleanup()

    def test_private_files_removed_when_cluster_delete_or_inventory_fails(self):
        for failure_call in (1, 2, 3):
            with self.subTest(failure_call=failure_call), self.qualification() as q:
                q.created = True
                q.kubeconfig.write_text('invocation-private-credential')
                calls = []
                def run(args, **kwargs):
                    calls.append(args)
                    if len(calls) == failure_call:
                        raise RuntimeError('demonstrated cleanup failure')
                    return subprocess.CompletedProcess(args, 0, '', '')
                with patch.object(q, 'run', side_effect=run):
                    with self.assertRaisesRegex(RuntimeError, 'demonstrated cleanup failure'):
                        q.cleanup()
                self.assertFalse(q.temp.exists())
                self.assertFalse(q.record['cleanup']['complete'])
                self.assertTrue(q.record['cleanup']['temporaryKubeconfigRemoved'])
                self.assertTrue(q.record['cleanup']['temporaryDirectoryRemoved'])
                self.assertIn('demonstrated cleanup failure', q.record['cleanup']['error'])
                self.assertEqual(calls[0][0:4], ['kind', 'delete', 'cluster', '--name'])
                self.assertEqual(calls[0][4], q.cluster)

    def test_cleanup_failure_stays_failed_in_durable_result(self):
        with self.qualification() as q:
            q.created = True
            q.kubeconfig.write_text('private')
            with patch.multiple(q, preflight=lambda: None, native_probe=lambda: None,
                                static_checks=lambda: None,
                                environment=lambda: None, admission_and_ssa=lambda: None,
                                verify_source=lambda: None), patch.object(q, 'create', lambda: None), patch.object(q, 'run', side_effect=RuntimeError('cannot delete owned cluster')):
                with self.assertRaisesRegex(RuntimeError, 'cannot delete owned cluster'):
                    q.execute()
            import json
            record = json.loads((q.evidence / 'qualification.json').read_text())
            self.assertEqual(record['status'], 'failed')
            self.assertFalse(record['cleanup']['complete'])
            self.assertTrue(record['cleanup']['temporaryKubeconfigRemoved'])

    def test_source_inventory_detects_added_deleted_and_changed_files(self):
        with self.qualification() as q, tempfile.TemporaryDirectory(prefix='h45-inventory-') as directory:
            root = Path(directory)
            chart = root / 'eng/helm-qualification/chart'
            chart.mkdir(parents=True)
            original = chart / 'original.yaml'
            for scenario in ('added', 'removed', 'modified'):
                with self.subTest(scenario=scenario):
                    original.write_text('before')
                    added = chart / 'new-template.yaml'
                    added.unlink(missing_ok=True)
                    with patch.multiple(qualify, ROOT=root, CHART=chart, HERE=chart.parent):
                        q.record['source'] = {'files': q.source_inventory()}
                        if scenario == 'added': added.write_text('new object')
                        elif scenario == 'removed': original.unlink()
                        else: original.write_text('after')
                        with self.assertRaisesRegex(AssertionError, 'source inventory or contents changed'):
                            q.verify_source()
                    expected = added if scenario == 'added' else original
                    self.assertEqual(q.record['sourceChangedDuringRun'][scenario], [str(expected.relative_to(root))])

    def test_source_inventory_refuses_authoritative_and_native_input_mutations(self):
        families = [*(f'references/Hexalith.{owner}/module/hexalith.module-manifest.v2.json' for owner in ('EventStore', 'Tenants', 'Memories')),
                    'DaprSelfHostedMtls.cs', 'Directory.Build.props', 'Directory.Build.targets',
                    'references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDeclarationClosure.cs',
                    'references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Workspace/WorkspaceRootResolver.cs',
                    'references/Hexalith.Builds/Tools/write-platform-version-catalog.ps1']
        with self.qualification() as q, tempfile.TemporaryDirectory(prefix='h45-source-closure-') as directory:
            root = Path(directory); chart = root / 'eng/helm-qualification/chart'; chart.mkdir(parents=True)
            with patch.multiple(qualify, ROOT=root, CHART=chart, HERE=chart.parent):
                for family in families:
                    path = root / family; path.parent.mkdir(parents=True, exist_ok=True)
                    for operation in ('modified', 'removed', 'added'):
                        with self.subTest(input=family, operation=operation):
                            path.write_text('before')
                            if operation == 'added': path.unlink()
                            q.record['source'] = {'files': q.source_inventory()}
                            if operation == 'removed': path.unlink()
                            else: path.write_text('after')
                            with self.assertRaisesRegex(AssertionError, 'source inventory or contents changed'):
                                q.verify_source()
                            self.assertIn(family, q.record['sourceChangedDuringRun'][operation])

    def test_native_build_input_closure_follows_projects_linked_sources_and_imports(self):
        with self.qualification() as q, tempfile.TemporaryDirectory(prefix='h45-build-closure-') as directory:
            root = Path(directory)
            entry = root / 'src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj'
            owner = root / 'owner/owner.csproj'
            linked = root / 'linked/DaprSelfHostedMtls.cs'
            resolver = owner.parent / 'Workspace/Resolver.cs'
            imported = root / 'build/owner-import.props'
            for path in (entry, owner, linked, resolver, imported):
                path.parent.mkdir(parents=True, exist_ok=True); path.write_text('source')
            def run(args, **kwargs):
                project = Path(args[2])
                self.assertEqual(q.env['DirectoryPackagesPropsPath'], str(root / 'eng/helm-qualification/source-packages.props'))
                if args[-1].startswith('-preprocess:'):
                    Path(args[-1].split(':', 1)[1]).write_text('<!--\n' + str(imported) + '\n-->')
                    return subprocess.CompletedProcess(args, 0, '', '')
                items = {'Compile': [{'FullPath': str(linked if project == entry else resolver)}],
                         'ProjectReference': [{'FullPath': str(owner)}] if project == entry else []}
                payload = {'Items': items, 'Properties': {'ProjectAssetsFile': str(project.parent / 'obj/project.assets.json')}}
                return subprocess.CompletedProcess(args, 0, json.dumps(payload), '')
            with patch.multiple(qualify, ROOT=root, HERE=root / 'eng/helm-qualification'), patch.object(q, 'run', side_effect=run):
                self.assertEqual(q.native_build_inputs(), {entry, owner, linked, resolver, imported})

    def test_resolved_kubernetes_integration_matches_both_assets_and_retains_hashes(self):
        for mismatch in (None, 'native', 'composition'):
            with self.subTest(mismatch=mismatch), self.qualification() as q, tempfile.TemporaryDirectory(prefix='h45-assets-') as directory:
                root = Path(directory); data = root / 'data'; q.env['XDG_DATA_HOME'] = str(data)
                native = data / 'dotnet/runfile/apphost-fixture/obj/project.assets.json'
                composition = root / 'composition-project.assets.json'; q.composition_assets = composition
                expected = q.tools['aspireKubernetes']; q.record['tools'] = {}
                for role, path in [('native', native), ('composition', composition)]:
                    path.parent.mkdir(parents=True, exist_ok=True)
                    version = '0.0.0' if role == mismatch else expected
                    path.write_text(json.dumps({'project': {'restore': {'projectPath': str(root / 'apphost.cs.csproj')}},
                                                'libraries': {'Aspire.Hosting.Kubernetes/' + version: {'sha512': 'fixture-package-hash'}}}))
                with patch.object(qualify, 'ROOT', root):
                    if mismatch:
                        with self.assertRaisesRegex(AssertionError, mismatch + ' resolved'):
                            q.resolved_kubernetes_integration()
                        self.assertNotIn('resolvedKubernetesIntegration', q.record['checks'])
                    else:
                        q.resolved_kubernetes_integration()
                        result = q.record['tools']['aspireKubernetes']
                        self.assertEqual(result['expectedVersion'], expected)
                        for role in ('native', 'composition'):
                            captured = result['resolved'][role]
                            self.assertEqual(captured['version'], expected)
                            self.assertEqual(captured['assetsSha256'], qualify.sha(q.evidence / captured['assetsFile']))
                            self.assertEqual(captured['packageSha512'], 'fixture-package-hash')

    def test_trickled_download_has_a_complete_transfer_deadline(self):
        class Trickle(BaseHTTPRequestHandler):
            def log_message(self, *args): pass
            def do_GET(self):
                self.send_response(200)
                self.end_headers()
                try:
                    for _ in range(100):
                        self.wfile.write(b'x')
                        self.wfile.flush()
                        time.sleep(0.05)
                except (BrokenPipeError, ConnectionResetError): pass
        server = ThreadingHTTPServer(('127.0.0.1', 0), Trickle)
        server.daemon_threads = True
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            with self.qualification() as q:
                q.DOWNLOAD_TIMEOUT_SECONDS = 0.25
                q.tools['trickle'] = {'url': f'http://127.0.0.1:{server.server_port}/data', 'sha256': 'never-completes'}
                started = time.monotonic()
                with self.assertRaisesRegex(RuntimeError, 'command failed|bounded command timed out'):
                    q.download('trickle', 'partial')
                self.assertLess(time.monotonic() - started, 2)
                self.assertNotIn('trickle', q.record.get('downloads', {}))
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=2)

    def test_source_package_boundary_keeps_catalog_pins_and_exposes_conflicts(self):
        boundary = qualify.HERE / 'source-packages.props'
        properties = ['-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false',
                      '-p:HexalithBuildsRoot=' + str(qualify.ROOT / 'references/Hexalith.Builds'),
                      '-p:HexalithEventStoreRoot=' + str(qualify.ROOT / 'references/Hexalith.EventStore'),
                      '-p:DirectoryPackagesPropsPath=' + str(boundary)]
        def evaluate(project):
            result = subprocess.run(['dotnet', 'msbuild', str(project), *properties, '-getItem:PackageVersion'],
                                    capture_output=True, text=True, check=True, timeout=30)
            return json.loads(result.stdout)['Items']['PackageVersion']
        catalog = ET.parse(qualify.ROOT / 'references/Hexalith.Builds/Props/Directory.Packages.props')
        names = ['System.Security.Cryptography.Pkcs', 'System.Security.Cryptography.Xml']
        pins = {name: next(e.attrib['Version'] for e in catalog.iter('PackageVersion') if e.attrib.get('Include') == name) for name in names}
        items = evaluate(qualify.ROOT / 'src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj')
        for name in names:
            self.assertEqual([i['Version'] for i in items if i['Identity'] == name], [pins[name]])
        commons = evaluate(qualify.ROOT / 'references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds/Hexalith.Commons.UniqueIds.csproj')
        self.assertTrue(any(i['Identity'] == 'ByteAether.Ulid' for i in commons), 'owner-specific package additions must survive')
        with tempfile.TemporaryDirectory(prefix='h45-package-conflict-') as directory:
            project = Path(directory) / 'conflict.proj'
            project.write_text('<Project><ItemGroup><PackageVersion Include="System.Security.Cryptography.Pkcs" Version="0.0.0" /></ItemGroup>'
                               + '<Import Project="' + str(qualify.ROOT / 'Directory.Packages.props') + '" />'
                               + '<Import Project="' + str(boundary) + '" /></Project>')
            versions = {i['Version'] for i in evaluate(project) if i['Identity'] == names[0]}
            self.assertEqual(versions, {'0.0.0', pins[names[0]]})

    def test_command_logs_are_visible_to_version_control(self):
        log = qualify.DEFAULT_EVIDENCE / 'qualification-command.log'
        result = subprocess.run(['git', 'check-ignore', '-q', str(log)], cwd=qualify.ROOT)
        self.assertEqual(result.returncode, 1, 'qualification logs must be version-control-visible')


if __name__ == '__main__':
    unittest.main()
