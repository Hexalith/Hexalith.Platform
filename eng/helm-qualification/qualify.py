#!/usr/bin/env python3
"""Qualify the application archive using only an invocation-owned kind cluster."""
import argparse
import copy
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
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
        self.evidence = evidence
        self.evidence.mkdir(parents=True, exist_ok=True)
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
        self.tools = json.loads((HERE / 'toolchain.json').read_text())
        self.helm = os.environ.get('HELM_BIN') or shutil.which('helm')

    def run(self, args, *, data=None, ok=True, timeout=120):
        args = [str(a) for a in args]
        try:
            result = subprocess.run(args, input=data, capture_output=True, text=True, env=self.env,
                                    cwd=ROOT, timeout=timeout)
        except subprocess.TimeoutExpired as error:
            self.record['commands'].append({'argv': args, 'timeoutSeconds': timeout, 'timedOut': True})
            raise RuntimeError('bounded command timed out: ' + args[0]) from error
        index = len(self.record['commands'])
        log = self.evidence / f'{index:03d}-{Path(args[0]).name}.log'
        log.write_text(result.stdout + result.stderr)
        self.record['commands'].append({'argv': args, 'returncode': result.returncode,
                                      'log': log.name, 'logSha256': sha(log),
                                      'stdinSha256': hashlib.sha256((data or '').encode()).hexdigest() if data is not None else None})
        if ok and result.returncode:
            raise RuntimeError(f'command failed ({result.returncode}): {args[0]} — {result.stderr[-1500:]}')
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
        if not self.helm: raise RuntimeError('Helm 4.2.0+ required; set HELM_BIN')
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
                                 'files': self.source_inventory()}
        self.record['inputs'] = {'graphSha256': sha(CHART / 'reference-graph.json'), 'nodeImage': self.tools['nodeImage'],
                                 'sourcePackageProps': str(HERE / 'source-packages.props')}
        self.record['checks']['preflight'] = True

    def source_inventory(self):
        paths = list(CHART.rglob('*')) + list((ROOT / 'src/Hexalith.Platform.Composition').glob('*.cs'))
        paths += list((ROOT / 'src/Hexalith.Platform.Composition').glob('*.csproj'))
        paths += list(HERE.glob('*.py')) + list(HERE.glob('*.props'))
        paths += list((ROOT / 'references').glob('*/Directory.Packages.props'))
        paths += [ROOT / 'tests/Hexalith.Platform.Composition.Tests/PlatformHostedPublishingTests.cs',
                  HERE / 'README.md', ROOT / 'apphost.cs', HERE / 'toolchain.json',
                  ROOT / 'references/Hexalith.Builds/Props/Directory.Packages.props',
                  ROOT / 'Directory.Packages.props', ROOT / 'references/Hexalith.EventStore/Directory.Packages.props']
        paths += list((ROOT / 'src/Hexalith.Platform.Composition/Declarations').glob('*.json'))
        paths += [ROOT / 'references/Hexalith.Parties/module/hexalith.module-manifest.v2.json']
        return {str(p.relative_to(ROOT)): sha(p) for p in sorted(set(paths)) if p.is_file()}

    def verify_source(self):
        before = self.record['source']['files']
        after = self.source_inventory()
        changes = {'added': sorted(after.keys() - before.keys()),
                   'removed': sorted(before.keys() - after.keys()),
                   'modified': sorted(p for p in before.keys() & after.keys() if before[p] != after[p])}
        self.record['sourceChangedDuringRun'] = changes
        if any(changes.values()):
            raise AssertionError('source inventory or contents changed during qualification; rerun against stable inputs')

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
        self.run([sys.executable, '-m', 'unittest', 'discover', '-s', str(HERE), '-p', 'test_*.py', '-v'])
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
                   'onlyInvocationOwnedResourcesDeleted': True, 'temporaryKubeconfigRemoved': False}
        self.record['cleanup'] = cleanup
        try:
            if self.created:
                self.run(['kind', 'delete', 'cluster', '--name', self.cluster, '--kubeconfig', self.kubeconfig], timeout=120)
                clusters = self.run(['kind', 'get', 'clusters']).stdout.splitlines()
                if self.cluster in clusters: raise AssertionError('owned cluster remains after cleanup')
                self.record['remainingClusters'] = clusters
                if not set(self.record.get('preexistingClusters', [])).issubset(clusters):
                    raise AssertionError('a preexisting cluster disappeared during qualification')
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
            try: self.cleanup()
            except Exception as error:
                self.record['status'] = 'failed'; self.record['cleanup'].update(complete=False, error=str(error)); failure = failure or error
            self.record['finished'] = datetime.now(timezone.utc).isoformat()
            (self.evidence / 'qualification.json').write_text(json.dumps(self.record, indent=2) + '\n')
        if failure: raise failure


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path, default=DEFAULT_EVIDENCE)
    args = parser.parse_args()
    def interrupted(signum, frame):
        raise InterruptedError('qualification interrupted; cleaning owned resources')
    signal.signal(signal.SIGTERM, interrupted)
    qualification = Qualification(args.evidence.resolve())
    try: qualification.execute()
    except Exception as error:
        print('QUALIFICATION FAILED: ' + str(error), file=sys.stderr); return 1
    print('QUALIFICATION PASSED: ' + str(qualification.evidence / 'qualification.json')); return 0


if __name__ == '__main__':
    sys.exit(main())
