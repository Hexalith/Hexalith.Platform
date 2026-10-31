#!/usr/bin/env python3
"""Qualify the application archive using only an invocation-owned kind cluster."""
import argparse
import copy
from datetime import datetime, timezone
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import stat
import subprocess
import sys
import tarfile
import tempfile
import uuid

from test_chart import CHART, HERE, documents, fixture_values, graph

ROOT = HERE.parent.parent
DEFAULT_EVIDENCE = ROOT / '_bmad-output/implementation-artifacts/evidence/4-5'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def secure_containers(pod):
    """Inspect all injected sidecars and init containers, including native sidecar placement."""
    spec = pod['spec']
    containers = spec.get('containers', []) + spec.get('initContainers', [])
    injected = [c for c in containers if c['name'] == 'daprd']
    if len(injected) != 1:
        raise AssertionError('actual Pod admission did not inject exactly one daprd')
    for c in containers:
        s = c.get('securityContext', {})
        if s.get('allowPrivilegeEscalation') is not False or 'ALL' not in s.get('capabilities', {}).get('drop', []):
            raise AssertionError('container is not restricted: ' + c['name'])
        if not s.get('runAsNonRoot', spec.get('securityContext', {}).get('runAsNonRoot', False)):
            raise AssertionError('container can run as root: ' + c['name'])
        if s.get('runAsUser', spec.get('securityContext', {}).get('runAsUser', 1)) == 0:
            raise AssertionError('root container: ' + c['name'])
        if s.get('seccompProfile', spec.get('securityContext', {}).get('seccompProfile', {})).get('type') not in ('RuntimeDefault', 'Localhost'):
            raise AssertionError('unconfined seccomp: ' + c['name'])
        if s.get('readOnlyRootFilesystem') is not True:
            raise AssertionError('writable container root filesystem: ' + c['name'])
    return [{'name': c['name'], 'securityContext': c.get('securityContext', {})} for c in containers]


class Qualification:
    DOWNLOAD_TIMEOUT_SECONDS = 60

    def __init__(self, evidence):
        self.evidence = evidence.resolve()
        self.evidence.mkdir(parents=True, exist_ok=True)
        self._evidence_lock = os.open(self.evidence, os.O_RDONLY | os.O_DIRECTORY)
        try:
            fcntl.flock(self._evidence_lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            self.close()
            raise RuntimeError('qualification evidence directory is already in use: ' + str(self.evidence)) from error
        try:
            self._initialize()
        except BaseException:
            self.close()
            raise

    def close(self):
        if self._evidence_lock is not None:
            os.close(self._evidence_lock)
            self._evidence_lock = None

    def _initialize(self):
        # The directory lock is held before discarding the previous invocation.
        names = ('qualification.json', 'normal.yaml', 'fixture-values.json',
                 'native-output', 'native-rendered.yaml')
        for path in self.evidence.iterdir():
            if (path.name in names or re.fullmatch(r'[0-9]+-.*[.]log', path.name)
                    or re.fullmatch(r'admitted-.*[.]json', path.name)
                    or re.fullmatch(r'resolved-.*-project[.]assets[.]json', path.name)
                    or re.fullmatch(r'hexalith-reference-.*[.]tgz', path.name)):
                if path.is_dir() and not path.is_symlink():
                    shutil.rmtree(path)
                else:
                    path.unlink()
        self.record = {'schema': 'hexalith.helm-qualification.v1', 'started': datetime.now(timezone.utc).isoformat(),
                       'status': 'failed', 'commands': [], 'checks': {}, 'cleanup': {},
                       'exclusions': ['OCI publication and attestation', 'off-site restoration', 'staging deployment',
                                      'application functional readiness with retained images', 'future environment policy qualification']}
        self.temp = Path(tempfile.mkdtemp(prefix='h45-'))
        self.cluster = 'h45-' + uuid.uuid4().hex[:12]
        self.kubeconfig = self.temp / 'kubeconfig'
        self.context = 'kind-' + self.cluster
        self.created = False
        self.env = dict(os.environ)
        self.env.pop('KUBECONFIG', None)
        self.env['KUBECONFIG'] = str(self.kubeconfig)
        self.env['MSBUILDDISABLENODEREUSE'] = '1'
        self.env['HELM_BIN'] = str(self.temp / 'linux-amd64/helm')
        self.tools = json.loads((HERE / 'toolchain.json').read_text())
        self.helm = self.env['HELM_BIN']

    def run(self, args, *, data=None, ok=True, timeout=120, sensitive=False,
            maximum_output_bytes=8 * 1024 * 1024, heartbeat=None, inherit_process_group=False,
            stdout_file=None, data_file=None):
        """Drain bounded pipes and always terminate/reap the owned process group."""
        import ctypes
        import selectors
        import time
        args = [str(a) for a in args]
        output = {'stdout': bytearray(), 'stderr': bytearray()}
        streamed_bytes = 0
        sink = None
        def private_file(path, flags):
            path = Path(path)
            parent = path.parent.stat()
            if not (path.is_absolute() and path == path.resolve() and not path.is_symlink()
                    and stat.S_IMODE(parent.st_mode) == 0o700 and parent.st_uid == os.geteuid()):
                raise ValueError('private binary command custody required')
            fd = os.open(path, flags | os.O_NOFOLLOW | os.O_NONBLOCK, 0o600)
            info = os.fstat(fd)
            if not (stat.S_ISREG(info.st_mode) and stat.S_IMODE(info.st_mode) == 0o600
                    and info.st_uid == os.geteuid() and info.st_nlink == 1):
                os.close(fd)
                raise ValueError('private binary command file required')
            return fd
        if stdout_file is not None or data_file is not None:
            sensitive = True
        timed_out = overflow = False
        failure = None
        # Linux qualification adopts orphaned descendants so group cleanup can
        # reap them, including a writer whose original parent already exited.
        libc = ctypes.CDLL(None, use_errno=True)
        old_subreaper = ctypes.c_int()
        if libc.prctl(37, ctypes.byref(old_subreaper), 0, 0, 0) != 0 or libc.prctl(36, 1, 0, 0, 0) != 0:
            raise RuntimeError('process-group supervision unavailable')
        process = None
        try:
            if stdout_file is not None:
                sink = os.fdopen(private_file(stdout_file, os.O_WRONLY | os.O_CREAT | os.O_EXCL), 'wb')
            if data_file is not None and data is not None:
                raise ValueError('one command input required')
            with (os.fdopen(private_file(data_file, os.O_RDONLY), 'rb') if data_file is not None
                    else tempfile.TemporaryFile()) as stdin_file, selectors.DefaultSelector() as selector:
                if data is not None:
                    stdin_file.write(data.encode() if isinstance(data, str) else data)
                    stdin_file.seek(0)
                process = subprocess.Popen(args, stdin=stdin_file if data is not None or data_file is not None else subprocess.DEVNULL,
                    stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=self.env, cwd=ROOT, start_new_session=not inherit_process_group)
                for name in output:
                    stream = getattr(process, name)
                    os.set_blocking(stream.fileno(), False)
                    selector.register(stream, selectors.EVENT_READ, name)
                end = time.monotonic() + timeout
                next_heartbeat = time.monotonic()
                try:
                    while selector.get_map():
                        if heartbeat is not None and time.monotonic() >= next_heartbeat:
                            heartbeat()
                            next_heartbeat = time.monotonic() + 5
                        # A leader's successful exit does not authorize a detached
                        # descendant to continue writing or running indefinitely.
                        if process.poll() is not None:
                            try:
                                process.kill() if inherit_process_group else os.killpg(process.pid, signal.SIGKILL)
                            except ProcessLookupError:
                                pass
                        if time.monotonic() >= end:
                            timed_out = True
                            break
                        for key, _ in selector.select(min(0.05, max(0, end - time.monotonic()))):
                            remaining = maximum_output_bytes - sum(map(len, output.values())) - streamed_bytes
                            chunk = os.read(key.fileobj.fileno(), min(16384, remaining + 1))
                            if not chunk:
                                selector.unregister(key.fileobj)
                            elif len(chunk) > remaining:
                                overflow = True
                                break
                            elif key.data == 'stdout' and sink is not None:
                                sink.write(chunk)
                                streamed_bytes += len(chunk)
                            else:
                                output[key.data].extend(chunk)
                        if overflow:
                            break
                    if not timed_out and not overflow:
                        try:
                            process.wait(timeout=max(0.001, end - time.monotonic()))
                        except subprocess.TimeoutExpired:
                            timed_out = True
                except BaseException as error:
                    failure = error
                finally:
                    # killpg is unconditional, even when poll() has reaped the
                    # leader between cancellation and this terminal path.
                    try:
                        process.kill() if inherit_process_group else os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    process.wait()
                    process.stdout.close(); process.stderr.close()
                    reap_end = time.monotonic() + 1
                    while time.monotonic() < reap_end:
                        try:
                            child, _ = os.waitpid(-process.pid, os.WNOHANG)
                        except ChildProcessError:
                            break
                        if child == 0:
                            time.sleep(0.005)
        finally:
            active_error = sys.exc_info()[0] is not None
            try:
                if sink is not None:
                    try:
                        sink.flush(); os.fsync(sink.fileno())
                    finally:
                        sink.close()
            except OSError:
                if not active_error:
                    raise
            finally:
                libc.prctl(36, old_subreaper.value, 0, 0, 0)
        result = subprocess.CompletedProcess(args, process.returncode,
            output['stdout'].decode('utf-8', errors='replace'), output['stderr'].decode('utf-8', errors='replace'))
        index = self.record.get('commandCount', len(self.record['commands']))
        log = self.evidence / f'{index:03d}-{Path(args[0]).name}.log'
        code = ('cancelled' if isinstance(failure, (KeyboardInterrupt, InterruptedError)) else 'process-io' if failure else
                'timeout' if timed_out else 'output-limit' if overflow else 'exit-code' if result.returncode else 'completed')
        with os.fdopen(os.open(log, os.O_WRONLY | os.O_CREAT | os.O_TRUNC | os.O_NOFOLLOW, 0o600), 'w') as stream:
            stream.write(json.dumps({'outcome': code, 'returncode': result.returncode}) + '\n' if sensitive else result.stdout + result.stderr)
        self.record['commands'].append({'argv': [Path(args[0]).name, '[restricted arguments omitted]'] if sensitive else args,
            'returncode': result.returncode, 'outcome': code, 'timedOut': timed_out, 'outputLimitExceeded': overflow,
            'timeoutSeconds': timeout, 'capturedBytes': sum(map(len, output.values())) + streamed_bytes, 'processGroupTerminated': True,
            'log': log.name, 'logSha256': sha(log),
            'stdinSha256': None if sensitive or data is None else hashlib.sha256(data.encode() if isinstance(data, str) else data).hexdigest()})
        if failure:
            raise failure
        if timed_out or overflow:
            raise RuntimeError('bounded command timed out' if timed_out else 'bounded command refused: output-limit')
        if ok and result.returncode:
            raise RuntimeError(f'command failed ({result.returncode}): {Path(args[0]).name}' + ('' if sensitive else ' — ' + result.stderr[-1500:]))
        return result

    def kube(self, *args, **kwargs):
        return self.run(['kubectl', '--kubeconfig', self.kubeconfig, '--context', self.context, *args], **kwargs)

    def pod_kube(self, *args, **kwargs):
        identity = self.admission_identity
        return self.kube('--as=' + identity['username'], '--as-uid=' + identity['uid'],
                         '--as-group=system:serviceaccounts', '--as-group=system:serviceaccounts:kube-system',
                         '--as-group=system:authenticated', *args, **kwargs)

    def helm_run(self, *args, **kwargs):
        return self.run([self.helm, '--kubeconfig', self.kubeconfig, '--kube-context', self.context, *args], **kwargs)

    def download(self, key, name):
        pin = self.tools[key]; path = self.temp / name
        # curl bounds the complete transfer, including trickled response bodies.
        self.run(['curl', '--fail', '--location', '--silent', '--show-error',
                  '--max-time', str(self.DOWNLOAD_TIMEOUT_SECONDS), '--output', path, pin['url']],
                 timeout=self.DOWNLOAD_TIMEOUT_SECONDS + 5)
        if sha(path) != pin['sha256']:
            raise RuntimeError('download checksum mismatch: ' + key)
        self.record.setdefault('downloads', {})[key] = {'url': pin['url'], 'sha256': sha(path)}
        return path

    def preflight(self):
        for tool in ('docker', 'kind', 'kubectl', 'dotnet', 'aspire', 'curl'):
            if not shutil.which(tool): raise RuntimeError('required tool is missing: ' + tool)
        archive = self.download('helm', 'helm.tar.gz')
        with tarfile.open(archive, 'r:gz') as distribution:
            member = distribution.getmember('linux-amd64/helm')
            if not member.isfile(): raise RuntimeError('pinned Helm archive has no regular binary')
            distribution.extract(member, self.temp, filter='data')
        versions = {'helm': self.run([self.helm, 'version', '--short']).stdout.strip(),
                    'kind': self.run(['kind', 'version']).stdout.strip(),
                    'kubectl': json.loads(self.run(['kubectl', 'version', '--client', '-o', 'json']).stdout)['clientVersion']['gitVersion'],
                    'aspire': self.run(['aspire', '--version']).stdout.strip(),
                    'dotnet': self.run(['dotnet', '--version']).stdout.strip(),
                    'docker': self.run(['docker', 'info', '--format', '{{.ServerVersion}}']).stdout.strip(),
                    'curl': self.run(['curl', '--version']).stdout.splitlines()[0]}
        if not versions['helm'].startswith('v' + self.tools['helm']['version'] + '+'):
            raise RuntimeError('Helm differs from the verified toolchain pin')
        if 'v' + self.tools['kind'] + ' ' not in versions['kind']:
            raise RuntimeError('kind differs from pin')
        if not versions['aspire'].startswith(self.tools['aspire'] + '+'):
            raise RuntimeError('Aspire differs from pin')
        catalog = (ROOT / 'references/Hexalith.Builds/Props/Directory.Packages.props').read_text()
        if f'<HexalithDaprRuntimeVersion>{self.tools["dapr"]["version"]}</HexalithDaprRuntimeVersion>' not in catalog:
            raise RuntimeError('Dapr patch is not the Builds catalog pin')
        self.record['tools'] = {k: {'version': v, 'binarySha256': sha(self.helm if k == 'helm' else shutil.which(k))}
                                for k, v in versions.items()}
        self.record['source'] = {'revision': self.run(['git', 'rev-parse', 'HEAD']).stdout.strip(),
                                 'statusPorcelain': self.run(['git', 'status', '--porcelain']).stdout,
                                 'submoduleStatus': self.run(['git', 'submodule', 'status']).stdout,
                                 'files': self.source_inventory()}
        self.record['inputs'] = {'graphSha256': sha(CHART / 'reference-graph.json'), 'nodeImage': self.tools['nodeImage'],
                                 'sourcePackageProps': str(HERE / 'source-packages.props')}
        self.record['checks']['preflight'] = True

    def native_build_inputs(self):
        """Evaluate the source project graph and its actual compile/resource/import inputs."""
        entry = ROOT / 'src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj'
        pending, seen, inputs = [entry], set(), set()
        environment = dict(self.env)
        self.env.update(UseHexalithProjectReferences='true', UseNuGetDeps='false', RunAnalyzers='false',
                        DirectoryPackagesPropsPath=str(HERE / 'source-packages.props'))
        try:
            while pending:
                project = pending.pop().resolve()
                if project in seen or not project.is_file():
                    continue
                seen.add(project); inputs.add(project)
                prefix = ['dotnet', 'msbuild', project]
                evaluated = json.loads(self.run(prefix + ['-getItem:Compile,EmbeddedResource,AdditionalFiles,Content,None,ProjectReference',
                                                          '-getProperty:ProjectAssetsFile'], timeout=30).stdout)
                if project == entry.resolve():
                    self.composition_assets = Path(evaluated['Properties']['ProjectAssetsFile'])
                for kind, items in evaluated['Items'].items():
                    for item in items:
                        path = Path(item.get('FullPath') or project.parent / item['Identity']).resolve()
                        if kind == 'ProjectReference': pending.append(path)
                        elif not (path.is_relative_to(project.parent) and
                                  any(part in ('bin', 'obj') for part in path.relative_to(project.parent).parts)):
                            inputs.add(path)
                # MSBuildAllProjects omits modern SDK imports; preprocessing lists resolved imports.
                expanded = self.temp / ('imports-' + hashlib.sha256(str(project).encode()).hexdigest()[:16] + '.xml')
                self.run(prefix + ['-preprocess:' + str(expanded)], timeout=30)
                for line in expanded.read_text(encoding='utf-8-sig').splitlines():
                    path = Path(line.strip())
                    if path.is_absolute() and path.suffix in ('.props', '.targets', '.proj', '.csproj') and path.is_file():
                        inputs.add(path)
        finally:
            self.env = environment
        # Generated build outputs are derived from the recorded project/import inputs.
        return inputs

    def source_inventory(self):
        paths = list(CHART.rglob('*')) + list((ROOT / 'src/Hexalith.Platform.Composition').glob('*.cs'))
        paths += list((ROOT / 'src/Hexalith.Platform.Composition').glob('*.csproj'))
        paths += list(HERE.glob('*.py')) + list(HERE.glob('*.props'))
        paths += list(ROOT.glob('*.props')) + list(ROOT.glob('*.targets'))
        paths += list((ROOT / 'references').glob('*/Directory.Packages.props'))
        paths += [ROOT / 'tests/Hexalith.Platform.Composition.Tests/PlatformHostedPublishingTests.cs',
                  HERE / 'README.md', ROOT / 'apphost.cs', HERE / 'toolchain.json', ROOT / 'global.json',
                  ROOT / 'nuget.config', ROOT / 'DaprSelfHostedMtls.cs',
                  ROOT / 'references/Hexalith.Builds/Props/Directory.Packages.props',
                  ROOT / 'references/Hexalith.Builds/Tools/write-platform-version-catalog.ps1']
        paths += list((ROOT / 'src/Hexalith.Platform.Composition/Declarations').glob('*.json'))
        for owner in ('EventStore', 'Tenants', 'Memories', 'Parties'):
            paths += list((ROOT / 'references' / ('Hexalith.' + owner) / 'module').rglob('*.json'))
        tooling = ROOT / 'references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling'
        paths += [p for p in tooling.rglob('*') if p.suffix in ('.cs', '.csproj', '.props', '.targets', '.json')
                  and not any(part in ('bin', 'obj') for part in p.relative_to(tooling).parts)]
        paths += list(self.native_build_inputs())
        return {(str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(p)): sha(p)
                for p in sorted(set(paths)) if p.is_file()}

    def verify_source(self):
        before = self.record['source']['files']
        after = self.source_inventory()
        changes = {'added': sorted(after.keys() - before.keys()),
                   'removed': sorted(before.keys() - after.keys()),
                   'modified': sorted(p for p in before.keys() & after.keys() if before[p] != after[p])}
        self.record['sourceChangedDuringRun'] = changes
        if any(changes.values()):
            raise AssertionError('source inventory or contents changed during qualification; rerun against stable inputs')

    def resolved_kubernetes_integration(self):
        """Retain resolved assets from the completed native build, not only requested pins."""
        data_home = Path(self.env.get('XDG_DATA_HOME', Path.home() / '.local/share'))
        candidates = []
        for path in (data_home / 'dotnet/runfile').glob('apphost-*/obj/project.assets.json'):
            assets = json.loads(path.read_text())
            if assets.get('project', {}).get('restore', {}).get('projectPath') == str(ROOT / 'apphost.cs.csproj'):
                candidates.append(path)
        if len(candidates) != 1:
            raise AssertionError('native resolved-package assets are missing or ambiguous')
        paths = {'native': candidates[0],
                 'composition': getattr(self, 'composition_assets', ROOT / 'src/Hexalith.Platform.Composition/obj/project.assets.json')}
        expected = self.tools['aspireKubernetes']
        evidence = {'package': 'Aspire.Hosting.Kubernetes', 'version': expected, 'expectedVersion': expected, 'resolved': {}}
        for role, path in paths.items():
            assets = json.loads(path.read_text())
            packages = [(name, value) for name, value in assets['libraries'].items()
                        if name.split('/')[0].lower() == 'aspire.hosting.kubernetes']
            if len(packages) != 1 or packages[0][0].split('/', 1)[1] != expected:
                raise AssertionError(role + ' resolved Aspire.Hosting.Kubernetes differs from the existing toolchain pin')
            name, package = packages[0]
            retained = self.evidence / ('resolved-' + role + '-project.assets.json')
            shutil.copy2(path, retained)
            evidence['resolved'][role] = {'version': name.split('/', 1)[1], 'assetsFile': retained.name,
                                          'assetsSha256': sha(retained), 'packageSha512': package.get('sha512')}
        self.record['tools']['aspireKubernetes'] = evidence
        self.record['checks']['resolvedKubernetesIntegration'] = True

    def native_probe(self):
        output = self.temp / 'native-output'
        metadata = self.temp / 'native-graph.json'
        previous = dict(self.env)
        self.env.update(Platform__Hosted__Enabled='true', Platform__Hosted__GraphPath=str(metadata),
                        UseHexalithProjectReferences='true', UseNuGetDeps='false', RunAnalyzers='false',
                        DirectoryPackagesPropsPath=str(HERE / 'source-packages.props'))
        try:
            self.run(['aspire', 'publish', '--apphost', ROOT / 'apphost.cs', '--output-path', output,
                      '--non-interactive', '--include-exception-details'], timeout=240)
        finally:
            self.env = previous
        self.resolved_kubernetes_integration()
        if json.loads(metadata.read_text()) != graph():
            raise AssertionError('packaged graph metadata differs from the current native publishing graph')
        rendered = self.run([self.helm, 'template', 'native', output, '--namespace', 'reference']).stdout
        objects = documents(rendered)
        missing = []
        for workload in graph()['workloads']:
            deployment = next((d for d in objects if d['kind'] == 'Deployment' and d['metadata'].get('labels', {}).get('app.kubernetes.io/component') == workload['resourceId']), None)
            if deployment is None: raise AssertionError('incomplete native probe: missing workload')
            annotations = deployment['spec']['template'].get('metadata', {}).get('annotations', {})
            if annotations.get('dapr.io/app-id') != workload['appId'] or annotations.get('dapr.io/enabled') != 'true':
                missing.append(workload['identity'])
        if len(missing) != len(graph()['workloads']):
            raise AssertionError('recorded native Dapr fallback trigger is no longer reproduced; reassess AD-1')
        native_directory = self.evidence / 'native-output'
        if native_directory.exists(): shutil.rmtree(native_directory)
        shutil.copytree(output, native_directory)
        (self.evidence / 'native-rendered.yaml').write_text(rendered)
        self.record['exportDecision'] = {'selected': 'maintained-chart', 'trigger': 'AD-1: hand-modeling module Dapr plumbing',
            'nativePublishSucceeded': True, 'nativeDaprAnnotationsMissingFor': missing,
            'reason': 'WithDaprSidecar annotations are absent from every generated Pod template; preserving them requires a separately modeled Kubernetes Dapr path.',
            'generatedFiles': {str(p.relative_to(native_directory)): sha(p) for p in sorted(native_directory.rglob('*')) if p.is_file()},
            'renderedSha256': sha(self.evidence / 'native-rendered.yaml')}
        self.record['checks']['nativeProbeAndGraphParity'] = True

    def static_checks(self):
        self.run([sys.executable, '-m', 'unittest', 'discover', '-s', str(HERE), '-p', 'test_*.py', '-v'], timeout=600)
        self.record['checks']['chartMatrix'] = True
        values = self.temp / 'values.json'; values.write_text(json.dumps(fixture_values(), sort_keys=True))
        self.values = values
        self.record['inputs']['valuesSha256'] = sha(values)
        (self.evidence / 'fixture-values.json').write_bytes(values.read_bytes())
        self.run([self.helm, 'package', CHART, '--destination', self.temp])
        self.archive = next(self.temp.glob('hexalith-reference-*.tgz'))
        shutil.copy2(self.archive, self.evidence / self.archive.name)
        self.record['package'] = {'file': self.archive.name, 'sha256': sha(self.archive)}

    def create(self):
        existing = self.run(['kind', 'get', 'clusters']).stdout.splitlines()
        self.record['preexistingClusters'] = existing
        if self.cluster in existing: raise RuntimeError('generated kind target already exists')
        self.created = True  # The invocation owns attempted creation and any partial node left behind.
        self.run(['kind', 'create', 'cluster', '--name', self.cluster, '--image', self.tools['nodeImage'],
                  '--kubeconfig', self.kubeconfig, '--wait', '180s'], timeout=240)
        config = json.loads(self.run(['kubectl', '--kubeconfig', self.kubeconfig, 'config', 'view', '-o', 'json']).stdout)
        if len(config['clusters']) != 1 or len(config['contexts']) != 1 or config['contexts'][0]['name'] != self.context:
            raise AssertionError('kubeconfig does not contain only the owned cluster')
        endpoint = config['clusters'][0]['cluster']['server']
        if not re.fullmatch(r'https://127\.0\.0\.1:\d+', endpoint): raise AssertionError('owned endpoint is not loopback')
        uid = json.loads(self.kube('get', 'namespace', 'kube-system', '-o', 'json').stdout)['metadata']['uid']
        self.record['cluster'] = {'name': self.cluster, 'context': self.context, 'uid': uid, 'endpoint': endpoint, 'ambientKubeconfigUsed': False}
        gateway = self.download('gateway', 'gateway.yaml')
        self.kube('apply', '--server-side', '--field-manager=h45-environment', '-f', gateway)
        dapr = self.download('dapr', 'dapr.tgz')
        self.helm_run('install', 'dapr', dapr, '--namespace', 'dapr-system', '--create-namespace', '--wait', '--timeout', '180s',
                      '--set', 'global.ha.enabled=false', '--set', 'global.scheduler.enabled=false',
                      '--set', 'dapr_placement.volumeclaims.enabled=false',
                      '--set', 'dapr_sidecar_injector.sidecarRunAsNonRoot=true',
                      '--set', 'dapr_sidecar_injector.sidecarRunAsUser=1654',
                      '--set', 'dapr_sidecar_injector.sidecarReadOnlyRootFilesystem=true',
                      '--set', 'dapr_sidecar_injector.sidecarDropALLCapabilities=true', timeout=240)
        self.record['daprControlPlane'] = json.loads(self.kube('get', 'pods', '-n', 'dapr-system', '-o', 'json').stdout)
        self.record['checks']['ownedClusterAndPinnedDapr'] = True

    def environment(self):
        fixtures = [
            {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 'reference', 'labels': {
                'pod-security.kubernetes.io/enforce': 'restricted', 'pod-security.kubernetes.io/enforce-version': 'v1.35',
                'pod-security.kubernetes.io/audit': 'restricted', 'pod-security.kubernetes.io/warn': 'restricted'}}},
            {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 'environment'}},
            {'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': 'fixture-credential', 'namespace': 'reference'},
             'stringData': {'token': 'invocation-owned-synthetic-token'}},
            {'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': 'fixture-registry', 'namespace': 'reference'},
             'type': 'kubernetes.io/dockerconfigjson', 'stringData': {'.dockerconfigjson': '{"auths":{}}'}},
            {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'fixture-components', 'namespace': 'reference'}, 'data': {'fixture': 'environment-owned'}},
            {'apiVersion': 'v1', 'kind': 'PersistentVolumeClaim', 'metadata': {'name': 'fixture-storage', 'namespace': 'environment'},
             'spec': {'accessModes': ['ReadWriteOnce'], 'resources': {'requests': {'storage': '1Mi'}}}},
            {'apiVersion': 'gateway.networking.k8s.io/v1', 'kind': 'GatewayClass', 'metadata': {'name': 'fixture'},
             'spec': {'controllerName': 'fixture.invalid/unmanaged'}},
            {'apiVersion': 'gateway.networking.k8s.io/v1', 'kind': 'Gateway', 'metadata': {'name': 'fixture-gateway', 'namespace': 'environment'},
             'spec': {'gatewayClassName': 'fixture', 'listeners': [{'name': 'https', 'port': 443, 'protocol': 'HTTPS',
                 'tls': {'mode': 'Terminate', 'certificateRefs': [{'name': 'fixture-certificate'}]},
                 'allowedRoutes': {'namespaces': {'from': 'All'}}}]}},
        ]
        # Distinct app/API fixtures make crossed token references observable.
        token_names = {binding[k]['name'] for binding in fixture_values()['bindings'].values() for k in ('appToken', 'apiToken')}
        fixtures.extend({'apiVersion': 'v1', 'kind': 'Secret',
                         'metadata': {'name': name, 'namespace': 'reference'},
                         'stringData': {'token': 'invocation-owned-' + name}} for name in sorted(token_names))
        fixtures.append({'apiVersion': 'dapr.io/v1alpha1', 'kind': 'Component',
                         'metadata': {'name': 'environment-preserved', 'namespace': 'reference'},
                         'spec': {'type': 'secretstores.kubernetes', 'version': 'v1', 'metadata': []}, 'scopes': ['environment-owner']})
        controller = json.loads(self.kube('get', 'serviceaccount', 'replicaset-controller', '-n', 'kube-system', '-o', 'json').stdout)
        self.admission_identity = {'username': 'system:serviceaccount:kube-system:replicaset-controller', 'uid': controller['metadata']['uid']}
        self.record['admissionIdentity'] = json.loads(self.pod_kube('auth', 'whoami', '-o', 'json').stdout)
        self.fixtures = fixtures
        self.kube('apply', '--server-side', '--field-manager=h45-environment', '-f', '-', data='\n---\n'.join(json.dumps(v) for v in fixtures))
        self.fixture_uids = [self.fixture_identity(v) for v in fixtures]

    def fixture_identity(self, obj):
        args = ['get', obj['kind'], obj['metadata']['name'], '-o', 'json', '--show-managed-fields=true']
        if obj['metadata'].get('namespace'): args += ['-n', obj['metadata']['namespace']]
        current = json.loads(self.kube(*args).stdout)
        if any(m.get('manager') == 'helm' for m in current['metadata'].get('managedFields', [])):
            raise AssertionError('Helm owns an environment fixture')
        return {'kind': obj['kind'], 'name': obj['metadata']['name'], 'namespace': obj['metadata'].get('namespace'), 'uid': current['metadata']['uid']}

    def admission_and_ssa(self):
        # Use archive for the complete lifecycle. No readiness claim is made for synthetic image bindings.
        self.helm_run('install', 'reference', self.archive, '-n', 'reference', '-f', self.values, '--server-side=true', '--wait=hookOnly', '--timeout', '60s')
        rendered = self.helm_run('template', 'reference', self.archive, '-n', 'reference', '-f', self.values).stdout
        rendered_path = self.evidence / 'normal.yaml'; rendered_path.write_text(rendered)
        self.record['inputs']['renderedSha256'] = sha(rendered_path)
        deployments = [d for d in documents(rendered) if d['kind'] == 'Deployment']
        admitted = []
        for d in deployments:
            template = copy.deepcopy(d['spec']['template'])
            pod = {'apiVersion': 'v1', 'kind': 'Pod', 'metadata': {**template['metadata'], 'name': 'dry-' + d['metadata']['name'], 'namespace': 'reference'}, 'spec': template['spec']}
            response = json.loads(self.pod_kube('create', '--dry-run=server', '-f', '-', '-o', 'json', data=json.dumps(pod)).stdout)
            admitted.append({'workload': d['metadata']['name'], 'containers': secure_containers(response)})
            (self.evidence / ('admitted-' + d['metadata']['name'] + '.json')).write_text(json.dumps(response, indent=2))
        self.record['checks']['podAdmission'] = admitted
        print('PASSED all Pod templates admitted with compliant injected containers', flush=True)
        unsafe = {'apiVersion': 'v1', 'kind': 'Pod', 'metadata': {'name': 'unsafe-control', 'namespace': 'reference'},
                  'spec': {'containers': [{'name': 'unsafe', 'image': 'fixture.invalid/unsafe:latest', 'securityContext': {'privileged': True}}]}}
        denied = self.pod_kube('create', '--dry-run=server', '-f', '-', data=json.dumps(unsafe), ok=False)
        if denied.returncode == 0 or 'violates PodSecurity' not in denied.stderr:
            raise AssertionError('unsafe control was not rejected by restricted admission')
        self.record['checks']['unsafePodRejected'] = True
        print('PASSED unsafe Pod rejected under the same admission identity', flush=True)
        owners = []
        for d in deployments:
            live = json.loads(self.kube('get', 'deployment', d['metadata']['name'], '-n', 'reference', '-o', 'json', '--show-managed-fields=true').stdout)
            fields = [m for m in live['metadata']['managedFields'] if m.get('manager') == 'helm' and m.get('operation') == 'Apply']
            if not fields: raise AssertionError('Helm Apply ownership is missing')
            owners.append({'workload': d['metadata']['name'], 'managedFields': fields})
        self.record['checks']['helmApplyOwnership'] = owners
        print('PASSED Helm Apply field ownership verified', flush=True)
        self.helm_run('upgrade', 'reference', self.archive, '-n', 'reference', '-f', self.values,
                      '--server-side=true', '--wait=hookOnly', '--set', 'mode=recovery')
        live = json.loads(self.kube('get', 'deployments', '-n', 'reference', '-o', 'json').stdout)
        if any(d['spec']['replicas'] != 0 for d in live['items']): raise AssertionError('recovery did not quiesce workloads')
        if json.loads(self.kube('get', 'httproutes', '-n', 'reference', '-o', 'json').stdout)['items']:
            raise AssertionError('recovery retained public routes')
        self.record['checks']['recoveryQuiescesAndClosesRoutes'] = True
        print('PASSED recovery quiesced every workload and closed public routes', flush=True)
        # A second writer deliberately takes one field. Only this synthetic qualification fixture uses force.
        name = deployments[0]['metadata']['name']
        rival = {'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': {'name': name, 'namespace': 'reference'}, 'spec': {'replicas': 2}}
        self.kube('apply', '--server-side', '--field-manager=h45-rival', '--force-conflicts', '-f', '-', data=json.dumps(rival))
        refused = self.helm_run('upgrade', 'reference', self.archive, '-n', 'reference', '-f', self.values,
                                '--server-side=true', '--wait=hookOnly', '--set', 'mode=recovery', ok=False)
        message = refused.stderr + refused.stdout
        if refused.returncode == 0 or 'conflict' not in message.lower() or 'h45-rival' not in message:
            raise AssertionError('unforced Helm SSA conflict refusal was not demonstrated')
        unchanged = json.loads(self.kube('get', 'deployment', name, '-n', 'reference', '-o', 'json').stdout)
        if unchanged['spec']['replicas'] != 2: raise AssertionError('conflicting Helm upgrade changed rival-owned field')
        self.record['checks']['unforcedConflictRefusal'] = True
        print('PASSED unforced Helm SSA conflict refusal verified', flush=True)
        self.helm_run('upgrade', 'reference', self.archive, '-n', 'reference', '-f', self.values,
                      '--server-side=true', '--wait=hookOnly', '--set', 'mode=remove-workloads')
        for kind in ('deployments', 'services', 'httproutes', 'pods'):
            # Controllers may take a bounded interval to remove dependent Pods.
            if kind == 'pods':
                self.kube('wait', '--for=delete', 'pod', '--all', '-n', 'reference', '--timeout=90s')
            if json.loads(self.kube('get', kind, '-n', 'reference', '-o', 'json').stdout)['items']:
                raise AssertionError('workload removal left ' + kind)
        if self.fixture_uids != [self.fixture_identity(v) for v in self.fixtures]:
            raise AssertionError('workload removal changed environment fixtures')
        self.record['checks']['removalPreservesEnvironment'] = self.fixture_uids
        print('PASSED workload removal preserved separately owned environment fixtures', flush=True)
        self.helm_run('uninstall', 'reference', '-n', 'reference', '--wait', '--timeout', '60s')
        if self.fixture_uids != [self.fixture_identity(v) for v in self.fixtures]:
            raise AssertionError('uninstall changed environment fixtures')
        self.record['checks']['uninstallPreservesEnvironment'] = True
        print('PASSED uninstall preserved separately owned environment fixtures', flush=True)

    def cleanup(self):
        cleanup = {'complete': False, 'ownedCluster': self.cluster,
                   'onlyInvocationOwnedResourcesDeleted': False, 'temporaryKubeconfigRemoved': False}
        self.record['cleanup'] = cleanup
        try:
            if self.created:
                self.run(['kind', 'delete', 'cluster', '--name', self.cluster, '--kubeconfig', self.kubeconfig], timeout=120)
                clusters = self.run(['kind', 'get', 'clusters']).stdout.splitlines()
                if self.cluster in clusters: raise AssertionError('owned cluster remains after cleanup')
                self.record['remainingClusters'] = clusters
                if not set(self.record.get('preexistingClusters', [])).issubset(clusters):
                    raise AssertionError('a preexisting cluster disappeared during qualification')
                cleanup['onlyInvocationOwnedResourcesDeleted'] = True
                nodes = self.run(['docker', 'ps', '-a', '--filter', 'label=io.x-k8s.kind.cluster=' + self.cluster, '--format', '{{.ID}}']).stdout.strip()
                if nodes: raise AssertionError('owned kind nodes remain after cleanup')
            cleanup['clusterRemovalVerified'] = True
        except Exception as error:
            cleanup['error'] = str(error)
            raise
        finally:
            # Private credentials must be discarded even when cluster deletion/inventory fails.
            try:
                shutil.rmtree(self.temp)
            finally:
                cleanup['temporaryKubeconfigRemoved'] = not self.kubeconfig.exists()
                cleanup['temporaryDirectoryRemoved'] = not self.temp.exists()
            cleanup['complete'] = cleanup.get('clusterRemovalVerified', False) and cleanup['temporaryDirectoryRemoved']

    def execute(self):
        failure = None
        try:
            for step in (self.preflight, self.native_probe, self.static_checks, self.create, self.environment, self.admission_and_ssa):
                print('RUNNING ' + step.__name__, flush=True)
                step()
                print('PASSED ' + step.__name__, flush=True)
            self.verify_source()
            self.record['status'] = 'passed' 
        except Exception as error:
            failure = error
            self.record['failure'] = f'{type(error).__name__}: {error}'
        finally:
            try:
                try: self.cleanup()
                except Exception as error:
                    self.record['status'] = 'failed'; self.record['cleanup'].update(complete=False, error=str(error)); failure = failure or error
                self.record['finished'] = datetime.now(timezone.utc).isoformat()
                (self.evidence / 'qualification.json').write_text(json.dumps(self.record, indent=2) + '\n')
            finally:
                self.close()
        if failure: raise failure


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path, default=DEFAULT_EVIDENCE)
    parser.add_argument('--calibration', choices=('prepare', 'inspect', 'launch', 'monitor', 'stop', 'remove'), help='Independent calibration, with separate plan/authority/reservations; no campaign credit.')
    parser.add_argument('--calibration-state', type=Path)
    parser.add_argument('--calibration-plan', type=Path)
    parser.add_argument('--campaign', choices=('prepare', 'inspect', 'launch', 'monitor', 'stop', 'remove'))
    parser.add_argument('--campaign-state', type=Path)
    parser.add_argument('--campaign-plan', type=Path)
    parser.add_argument('--approved-plan-sha256')
    parser.add_argument('--campaign-capacity', action='store_true', help='Offline finite Memories sizing/admission only; does not instantiate the synthetic qualifier.')
    parser.add_argument('--campaign-profile')
    parser.add_argument('--approved-campaign-sha256')
    parser.add_argument('--current-capacity-receipt')
    parser.add_argument('--approved-capacity-sha256')
    parser.add_argument('--original-admission')
    parser.add_argument('--approved-original-admission-sha256')
    parser.add_argument('--admission-issuer')
    parser.add_argument('--admission-key')
    parser.add_argument('--approved-admission-key-sha256')
    args = parser.parse_args()
    if args.calibration:
        if args.campaign or args.campaign_capacity or args.calibration_state is None or args.campaign_state or args.campaign_plan:
            parser.error('--calibration requires --calibration-state and cannot select campaign or fixture inputs')
        import independent_calibration
        forwarded = [args.calibration, '--state', str(args.calibration_state)]
        for name, value in (('--plan', args.calibration_plan), ('--approved-plan-sha256', args.approved_plan_sha256)):
            if value is not None:
                forwarded.extend((name, str(value)))
        return independent_calibration.main(forwarded)
    if args.calibration_state or args.calibration_plan:
        parser.error('calibration inputs require --calibration')
    if args.campaign:
        if args.campaign_capacity or args.campaign_state is None:
            parser.error('--campaign requires --campaign-state and cannot select the fixture/capacity route')
        import campaign_lifecycle
        forwarded = [args.campaign, '--state', str(args.campaign_state)]
        for name, value in (('--plan', args.campaign_plan), ('--approved-plan-sha256', args.approved_plan_sha256)):
            if value is not None:
                forwarded.extend((name, str(value)))
        return campaign_lifecycle.main(forwarded)
    if any((args.campaign_state, args.campaign_plan, args.approved_plan_sha256)):
        parser.error('lifecycle inputs require --campaign')
    if args.campaign_capacity:
        import campaign_capacity
        forwarded = []
        for flag, value in (('--profile', args.campaign_profile), ('--approved-sha256', args.approved_campaign_sha256),
                            ('--current-capacity-receipt', args.current_capacity_receipt), ('--approved-capacity-sha256', args.approved_capacity_sha256),
                            ('--original-admission', args.original_admission), ('--approved-original-admission-sha256', args.approved_original_admission_sha256),
                            ('--admission-issuer', args.admission_issuer), ('--admission-key', args.admission_key), ('--approved-admission-key-sha256', args.approved_admission_key_sha256)):
            if value is not None:
                forwarded.extend((flag, value))
        return campaign_capacity.main(forwarded)
    if any((args.campaign_profile, args.approved_campaign_sha256, args.current_capacity_receipt, args.approved_capacity_sha256,
            args.original_admission, args.approved_original_admission_sha256, args.admission_issuer, args.admission_key, args.approved_admission_key_sha256)):
        parser.error('campaign inputs require --campaign-capacity; the synthetic qualifier is not a full campaign launcher')
    def interrupted(signum, frame):
        # InterruptedError is swallowed as an interrupted system call by selectors.
        raise RuntimeError('qualification interrupted; cleaning owned resources')
    signal.signal(signal.SIGTERM, interrupted)
    try:
        qualification = Qualification(args.evidence.resolve())
        qualification.execute()
    except Exception as error:
        print('QUALIFICATION FAILED: ' + str(error), file=sys.stderr); return 1
    print('QUALIFICATION PASSED: ' + str(qualification.evidence / 'qualification.json')); return 0


if __name__ == '__main__':
    sys.exit(main())
