#!/usr/bin/env python3
"""Qualify the maintained application chart on the locked Dapr 1.19.2 inputs."""
import argparse
from collections import Counter
import hashlib
import json
import mmap
from pathlib import Path
import platform
import shutil
import signal
import sys
import tarfile
import xml.etree.ElementTree as ET

from qualify import CHART, HERE, ROOT, Qualification, documents, sha
from test_chart import campaign_graph_fixture, fixture_values, graph


LOCK = HERE / 'dapr-1.19.2.json'
CATALOG = 'Props/Directory.Packages.props'
BUILDS = ROOT / 'references/Hexalith.Builds'
BUILDS_RELEASE = ROOT / ('_bmad-output/implementation-artifacts/evidence/3-3/verification/'
                         'continuation-20261122/builds-662-published.json')
REFERENCE_MAPPING = ROOT / 'eng/story-1-17-main-reference-mapping.json'
WORKS_RUNTIME = ROOT / 'config/works-proof-toolchains/1.19.2.json'
TOOL_MANIFEST = ROOT / '.config/dotnet-tools.json'
APPLICATION_DAPR_KINDS = ('Configuration', 'Subscription')


def contents_sha(data):
    return hashlib.sha256(data).hexdigest()


def file_sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def archived_bundle(archive):
    with tarfile.open(archive, 'r:gz') as chart:
        entries = [m for m in chart.getmembers() if m.isfile() and m.name.startswith('dapr/crds/')]
        crds = {Path(m.name).name: chart.extractfile(m).read() for m in entries}
        if len(entries) != len(crds):
            raise ValueError('duplicate Dapr CRD in chart archive')
        metadata = documents(chart.extractfile('dapr/Chart.yaml').read())[0]
    return metadata, crds


def scheduler_rbac(objects):
    role, binding = (next(obj for obj in objects if obj['kind'] == kind
                          and obj['metadata']['name'] == 'dapr-scheduler-pods')
                     for kind in ('Role', 'RoleBinding'))
    if any(obj['metadata']['namespace'] != 'dapr-system' for obj in (role, binding)):
        raise AssertionError('Scheduler pod read grants must stay in the Dapr namespace')
    if role['rules'] != [{'apiGroups': [''], 'resources': ['pods'], 'verbs': ['get', 'list', 'watch']}]:
        raise AssertionError('unexpected Dapr 1.19 Scheduler Role rules')
    if binding['subjects'] != [{'kind': 'ServiceAccount', 'name': 'dapr-scheduler', 'namespace': 'dapr-system'}]:
        raise AssertionError('unexpected Dapr 1.19 Scheduler RoleBinding subjects')
    if binding['roleRef'] != {'apiGroup': 'rbac.authorization.k8s.io', 'kind': 'Role', 'name': 'dapr-scheduler-pods'}:
        raise AssertionError('unexpected Dapr 1.19 Scheduler RoleBinding reference')
    return {'role': role['rules'], 'binding': binding['subjects']}


def workflow_requires_schema(crd):
    version, = [v for v in crd['spec']['versions'] if v['name'] == 'v1alpha1' and v['served']]
    schema = version['schema']['openAPIV3Schema']['properties']['spec']['properties']
    rules = schema['rules']['items']['properties']
    return {kind: rules[kind]['items']['properties']['requires'] for kind in ('activities', 'workflows')}


class DaprCandidate(Qualification):
    def _initialize(self):
        if any(self.evidence.iterdir()):
            raise ValueError('Dapr candidate evidence directory must be fresh')
        super()._initialize()
        self.env['HELM_DRIVER'] = 'secret'

    def steps(self):
        return (self.preflight, self.verify_dapr_inputs, self.static_checks,
                self.create, self.environment, self.admission_and_ssa)

    def source_inventory(self):
        paths = [*CHART.rglob('*'), *HERE.glob('*.py'), *HERE.glob('*.props'), LOCK, HERE / 'toolchain.json',
                 *list((ROOT / 'src/Hexalith.Platform.Composition').glob('*.cs')),
                 BUILDS / CATALOG, BUILDS_RELEASE, REFERENCE_MAPPING, ROOT / 'eng/builds-tuple.json', WORKS_RUNTIME,
                 ROOT / 'global.json', TOOL_MANIFEST, *self.builds_cli_files]
        return {str(p.relative_to(ROOT) if p.is_relative_to(ROOT) else p): sha(p)
                for p in sorted(set(paths)) if p.is_file()}

    def preflight(self):
        self.record['candidate'] = {'file': LOCK.name}
        data = LOCK.read_bytes()
        self.record['candidate']['sha256'] = contents_sha(data)
        self.tools = json.loads(data)
        if self.tools.get('schema') != 'hexalith.helm-qualification.dapr-candidate.v1':
            raise ValueError('unsupported Dapr candidate lock')
        self.record['candidate']['inputs'] = self.tools
        if sys.platform != 'linux' or platform.machine() != 'x86_64' or sys.version_info < (3, 12):
            raise RuntimeError('Dapr candidate requires Linux amd64 and Python 3.12 or newer')
        self.preflight_tools(extra=('skopeo', 'dotnet'))
        self.record['tools']['python'] = {'version': platform.python_version(), 'binarySha256': sha(sys.executable)}
        self.record_builds_cli()
        self.record['source'] = {
            'revision': self.run(['git', 'rev-parse', 'HEAD']).stdout.strip(),
            'statusPorcelain': self.run(['git', 'status', '--porcelain']).stdout,
            'submoduleStatus': self.run(['git', 'submodule', 'status']).stdout,
            'files': self.source_inventory(),
        }
        if self.record['source']['files'][str(LOCK.relative_to(ROOT))] != self.record['candidate']['sha256']:
            raise AssertionError('Dapr candidate lock changed during preflight')
        self.record['inputs'] = {'graphSha256': sha(CHART / 'reference-graph.json'),
                                 'nodeImage': self.tools['nodeImage']}
        self.verify_builds()
        reviewed = self.tools['referenceGraph']
        graph_path = CHART.relative_to(ROOT) / 'reference-graph.json'
        graph_at_review = self.run(['git', 'show', f'{reviewed["reviewedPlatformRevision"]}:{graph_path}']).stdout
        if {contents_sha(graph_at_review.encode()), self.record['inputs']['graphSha256']} != {reviewed['sha256']}:
            raise AssertionError('maintained chart graph differs from the reviewed reference graph')
        self.record['exportDecision'] = {'selected': 'maintained-chart', 'trigger': 'AD-1',
                                         'scope': 'Retained historical native publishing decision',
                                         'referenceGraph': reviewed}
        self.record['checks'].update(preflight=True, reviewedReferenceGraphBytes=True)

    def record_builds_cli(self):
        # Share the offline reader tests' actual sibling-first artifact selection.
        from test_campaign_lifecycle import CLI, TOOL_FRAMEWORK, TOOL_VERSION

        self.builds_cli_files = [CLI, CLI.with_suffix('.deps.json'), CLI.with_suffix('.runtimeconfig.json')]
        identities = {str(path): sha(path) for path in self.builds_cli_files}
        version = self.run(['dotnet', CLI, '--version']).stdout.strip()
        if not version:
            raise AssertionError('offline Builds Module CLI did not report its version')
        self.record['tools']['offlineBuildsModuleCli'] = {
            'reportedVersion': version, 'toolManifestVersion': TOOL_VERSION, 'targetFramework': TOOL_FRAMEWORK,
            'toolManifestSha256': sha(TOOL_MANIFEST), 'files': identities}

    def verify_builds(self):
        pin = self.tools['builds']
        released = json.loads(BUILDS_RELEASE.read_text())['release']
        bound = json.loads((ROOT / 'eng/builds-tuple.json').read_text())
        if (bound['version'] != pin['version'] or bound['releaseTag'] != pin['releaseTag']
                or released['releaseTag'] != pin['releaseTag'] or released['tagCommit'] != pin['revision']):
            raise AssertionError('released Builds identity differs from the Platform tuple')
        catalog = self.run(['git', '-C', BUILDS, 'show', f'{pin["revision"]}:{CATALOG}']).stdout
        if contents_sha(catalog.encode()) != pin['catalogSha256']:
            raise AssertionError('released Builds catalog differs from the candidate pin')
        local_catalog = (BUILDS / CATALOG).read_text()
        if local_catalog != catalog:
            raise AssertionError('checked out Builds catalog differs from the released catalog')
        props = ET.fromstring(catalog)
        sdks = {e.attrib['Include']: e.attrib['Version'] for e in props.findall('.//PackageVersion')
                if e.attrib.get('Include', '').startswith('Dapr.')}
        if (props.findtext('.//HexalithDaprRuntimeVersion') != self.tools['dapr']['version']
                or props.findtext('.//HexalithDaprCliVersion') != pin['daprCli']
                or not sdks or set(sdks.values()) != {pin['daprSdk']}):
            raise AssertionError('Dapr runtime, CLI or SDK differs from the released Builds catalog')
        self.record['checks']['releasedBuildsAlignment'] = {
            'releaseRecordSha256': sha(BUILDS_RELEASE), 'catalogSha256': pin['catalogSha256'],
            'runtime': self.tools['dapr']['version'], 'cli': pin['daprCli'], 'sdkPackages': sdks}

    def verify_dapr_inputs(self):
        dapr = self.tools['dapr']
        self.dapr_archive = self.download('dapr', 'dapr.tgz')
        index = self.temp / 'index.yaml'
        self.fetch(dapr['indexUrl'], index)
        entry = next(e for e in documents(index.read_text())[0]['entries']['dapr'] if e['version'] == dapr['version'])
        if (entry['digest'] != dapr['sha256'] or entry['appVersion'] != dapr['version']
                or dapr['url'] not in entry['urls']):
            raise AssertionError('Dapr chart checksum or version differs from its official index')
        metadata, crds = archived_bundle(self.dapr_archive)
        if (metadata['name'], metadata['version'], metadata['appVersion']) != ('dapr', dapr['version'], dapr['version']):
            raise AssertionError('Dapr chart metadata differs from its pinned runtime')
        shutil.copy2(self.dapr_archive, self.evidence / f'dapr-{dapr["version"]}.tgz')
        (self.evidence / 'dapr-index-entry.json').write_text(json.dumps(entry, indent=2, default=str) + '\n')
        self.verify_canonical_crds(dapr, crds)
        self.verify_dapr_source(dapr)
        self.verify_dapr_images(dapr)
        self.dapr_values = self.temp / 'dapr-values.json'
        self.dapr_values.write_text(json.dumps(dapr['values'], indent=2) + '\n')
        shutil.copy2(self.dapr_values, self.evidence / self.dapr_values.name)
        self.record['inputs']['daprValuesSha256'] = sha(self.dapr_values)
        rendered = self.run([self.helm, 'template', 'dapr', self.dapr_archive, '-n', 'dapr-system',
                             '-f', self.dapr_values]).stdout
        objects = documents(rendered)
        self.record['checks']['schedulerPodReadRbacRendered'] = scheduler_rbac(objects)
        self.record['checks']['daprRenderedImagePins'] = self.control_plane_images(objects)

    def verify_canonical_crds(self, dapr, crds):
        if {name: contents_sha(data) for name, data in crds.items()} != dapr['crds']:
            raise AssertionError('Dapr chart CRD bytes differ from the canonical pins')
        self.crds_dir = self.temp / 'dapr-crds'
        self.crds_dir.mkdir()
        crd_ids = []
        for filename, digest in dapr['crds'].items():
            url = f'{dapr["source"]["rawRoot"]}/charts/dapr/crds/{filename}'
            canonical = self.download('dapr-crd-' + filename, 'dapr-crds/' + filename,
                                      pin={'url': url, 'sha256': digest})
            crd_ids.append(documents(canonical.read_text())[0]['metadata']['name'])
        shutil.copytree(self.crds_dir, self.evidence / self.crds_dir.name)
        self.crd_ids = crd_ids
        starting = self.download('starting-dapr', 'starting-dapr.tgz', pin=dapr['startingRuntime'])
        _, old_crds = archived_bundle(starting)
        changed = sorted(name for name in crds.keys() | old_crds.keys() if crds.get(name) != old_crds.get(name))
        if changed != dapr['crdsChangedFromStartingRuntime']:
            raise AssertionError('Dapr CRD change set differs from the recorded upgrade requirements')
        self.record['checks']['canonicalDaprCrds'] = {'names': self.crd_ids, 'changedFromStartingRuntime': changed}

    def verify_dapr_source(self, dapr):
        dep = 'github.com/dapr/components-contrib'
        source, contrib = dapr['source'], dapr['componentsContrib']
        for project, identity in ((source, dapr), (contrib, contrib)):
            tag = 'refs/tags/v' + identity['version']
            output = self.run(['git', 'ls-remote', '--tags', project['repository'], tag, tag + '^{}']).stdout
            refs = {ref: rev for rev, ref in map(str.split, output.splitlines())}
            if refs.get(tag + '^{}', refs.get(tag)) != project['revision']:
                raise AssertionError('upstream Dapr tag differs from its pinned source revision')
        for filename, digest in (('go.mod', dapr['goModSha256']), ('go.sum', dapr['goSumSha256'])):
            self.download('dapr-' + filename, filename,
                          pin={'url': source['rawRoot'] + '/' + filename, 'sha256': digest})
        version = 'v' + contrib['version']
        if [dep, version] not in [line.split() for line in (self.temp / 'go.mod').read_text().splitlines()]:
            raise AssertionError('upstream runtime embeds a different components-contrib version')
        if f'{dep} {version} {contrib["goChecksum"]}' not in (self.temp / 'go.sum').read_text().splitlines():
            raise AssertionError('upstream components-contrib checksum differs from the candidate pin')

    def verify_dapr_images(self, dapr):
        self.image_config_digests = {}
        for name, image in dapr['images'].items():
            tag = image['releaseTag']
            raw_index = self.run(['skopeo', 'inspect', '--raw', 'docker://' + tag]).stdout
            if 'sha256:' + contents_sha(raw_index.encode()) != image['indexDigest']:
                raise AssertionError('Dapr image release tag differs from its pinned index: ' + name)
            manifest = [m['digest'] for m in json.loads(raw_index)['manifests']
                        if m['platform']['os'] == 'linux' and m['platform']['architecture'] == 'amd64']
            repository, digest = image['linuxAmd64'].split('@')
            if manifest != [digest] or tag != repository + ':' + dapr['version']:
                raise AssertionError('Dapr image Linux amd64 descriptor differs from pin: ' + name)
            raw_manifest = self.run(['skopeo', 'inspect', '--raw', 'docker://' + image['linuxAmd64']]).stdout
            if 'sha256:' + contents_sha(raw_manifest.encode()) != digest:
                raise AssertionError('Dapr image manifest bytes differ from pin: ' + name)
            self.image_config_digests[image['linuxAmd64']] = json.loads(raw_manifest)['config']['digest']
        self.record['checks']['officialDaprImageDigests'] = True
        self.verify_embedded_dependency(dapr)

    def verify_embedded_dependency(self, dapr):
        image = dapr['images']['daprd']['linuxAmd64']
        binary = self.temp / 'daprd'
        layout = self.temp / 'daprd-image'
        self.run(['skopeo', 'copy', '--preserve-digests', 'docker://' + image, 'dir:' + str(layout)], timeout=150)
        manifest_path = layout / 'manifest.json'
        manifest_digest = image.rsplit('@', 1)[1]
        if 'sha256:' + file_sha(manifest_path) != manifest_digest:
            raise AssertionError('copied daprd manifest differs from the pinned image')
        manifest = json.loads(manifest_path.read_bytes())
        layers = manifest['layers']
        for descriptor in [manifest['config'], *layers]:
            digest = descriptor['digest']
            algorithm, value = digest.split(':', 1)
            blob = layout / value
            if algorithm != 'sha256' or blob.stat().st_size != descriptor['size'] or file_sha(blob) != value:
                raise AssertionError('copied daprd blob differs from manifest: ' + digest)
        binary_layer = layers[-1]['digest']
        with tarfile.open(layout / binary_layer.split(':', 1)[1], 'r:*') as archive:
            members = [member for member in archive if member.name in ('daprd', './daprd')]
            if len(members) != 1 or not members[0].isfile():
                raise AssertionError('pinned daprd image has no unique regular daprd binary in its final layer')
            with archive.extractfile(members[0]) as stream, binary.open('wb') as destination:
                shutil.copyfileobj(stream, destination)
        works = json.loads(WORKS_RUNTIME.read_text())
        binary_sha = file_sha(binary)
        if (works['qualifiedDaprRuntime'], works['linuxAmd64DaprdSha256']) != (dapr['version'], binary_sha):
            raise AssertionError('daprd binary differs from the released runtime recorded for Works')
        contrib = dapr['componentsContrib']
        entries = [f'\ndep\tgithub.com/dapr/components-contrib\tv{contrib["version"]}\t{contrib["goChecksum"]}\n',
                   f'\nbuild\tvcs.revision={dapr["source"]["revision"]}\n']
        with binary.open('rb') as stream, mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as embedded:
            if any(embedded.find(entry.encode()) < 0 for entry in entries):
                raise AssertionError('daprd embedded Go source or components-contrib identity differs from pin')
        self.record['checks']['embeddedDaprSource'] = {
            'runtimeVersion': dapr['version'], 'runtimeRevision': dapr['source']['revision'],
            'daprdSha256': binary_sha, 'manifestDigest': manifest_digest, 'binaryLayerDigest': binary_layer,
            'componentsContrib': contrib}

    def control_plane_images(self, objects):
        images = self.tools['dapr']['images']
        expected = {image['linuxAmd64'] for name, image in images.items() if name != 'daprd'}
        counts = Counter()
        sidecars = []
        for obj in objects:
            if obj['kind'] in ('Deployment', 'StatefulSet'):
                spec = obj['spec']
                template = spec['template']['spec']
                for kind in ('containers', 'initContainers'):
                    for container in template.get(kind, []):
                        counts[container['image']] += spec.get('replicas', 1)
                        sidecars.extend(e['value'] for e in container.get('env', []) if e['name'] == 'SIDECAR_IMAGE')
        if set(counts) != expected or min(counts.values()) < 1 or sidecars != [images['daprd']['linuxAmd64']]:
            raise AssertionError('Dapr control-plane or injected sidecar configuration differs from pinned images')
        return {'controlPlane': sorted(counts), 'replicasByImage': dict(sorted(counts.items())), 'sidecar': sidecars[0]}

    def static_checks(self):
        super().static_checks()
        checks = self.record['checks']['applicationRenderModes'] = {}
        for mode in ('normal', 'recovery', 'remove-workloads'):
            outputs = {}
            for label, chart in (('source', CHART), ('archive', self.archive)):
                args = [self.helm, 'template', 'reference', chart, '-n', 'reference', '-f', self.values,
                        '--set', 'mode=' + mode]
                first = self.run(args).stdout
                if first != self.run(args).stdout:
                    raise AssertionError('application render is nondeterministic: ' + mode + ' ' + label)
                resources = documents(first)
                if any(obj['kind'] in ('Component', 'Secret', 'Gateway', 'StatefulSet', 'PersistentVolumeClaim')
                       for obj in resources):
                    raise AssertionError('application chart rendered an environment resource')
                if mode == 'normal' and label == 'archive':
                    self.application_dapr_names = self.application_dapr_inventory(resources)
                    self.record['checks']['applicationDaprInventory'] = {'expected': self.application_dapr_names}
                path = self.evidence / f'{mode}-{label}.yaml'
                path.write_text(first)
                outputs[label] = {'file': path.name, 'sha256': sha(path), 'components': 0}
            if outputs['source']['sha256'] != outputs['archive']['sha256']:
                raise AssertionError('application source and archive renders differ: ' + mode)
            checks[mode] = outputs

    def install_dapr(self):
        checks = self.record['checks']
        self.kube('apply', '--server-side', '--field-manager=h45-infrastructure', '-f', self.crds_dir)
        self.kube('wait', '--for=condition=Established', '--timeout=60s', *('crd/' + name for name in self.crd_ids))
        canonical = documents((self.crds_dir / 'workflowaccesspolicy.yaml').read_text())[0]
        expected = workflow_requires_schema(canonical)
        live = json.loads(self.kube('get', 'crd', 'workflowaccesspolicies.dapr.io', '-o', 'json').stdout)
        observed = workflow_requires_schema(live)
        if expected != observed or any(s['type'] != 'array' or s['items']['type'] != 'object' for s in observed.values()):
            raise AssertionError('live WorkflowAccessPolicy CRD differs at the 1.19 requires paths')
        path = self.evidence / 'installed-workflowaccesspolicy-schema.json'
        path.write_text(json.dumps(observed, indent=2) + '\n')
        checks['installedWorkflowAccessPolicySchema'] = {'file': path.name, 'sha256': sha(path)}
        checks['canonicalDaprCrds']['applyBeforeHelm'] = True
        self.helm_run('install', 'dapr', self.dapr_archive, '--namespace', 'dapr-system', '--create-namespace',
                      '--skip-crds', '-f', self.dapr_values, '--wait', '--timeout', '240s', timeout=270)
        objects = json.loads(self.kube('get', 'deployments,statefulsets,roles,rolebindings', '-n', 'dapr-system',
                                       '-o', 'json').stdout)['items']
        image_pins = self.control_plane_images(objects)
        if image_pins != checks['daprRenderedImagePins']:
            raise AssertionError('live Dapr control-plane image or replica count differs from the verified chart render')
        checks['daprLiveImagePins'] = image_pins
        rbac = scheduler_rbac(objects)
        account = json.loads(self.kube('get', 'serviceaccount', 'dapr-scheduler', '-n', 'dapr-system', '-o', 'json').stdout)
        uid = account['metadata']['uid']
        groups = ['system:serviceaccounts', 'system:serviceaccounts:dapr-system', 'system:authenticated']
        identity = ['--as=system:serviceaccount:dapr-system:dapr-scheduler', '--as-uid=' + uid,
                    *('--as-group=' + group for group in groups)]
        for verb in ('get', 'list', 'watch'):
            allowed = self.kube(*identity, 'auth', 'can-i', verb, 'pods', '-n', 'dapr-system')
            if allowed.stdout.strip() != 'yes':
                raise AssertionError('Scheduler Pod read not granted in dapr-system: ' + verb)
        denied = self.kube(*identity, 'auth', 'can-i', 'list', 'pods', '-n', 'default', ok=False)
        if denied.returncode != 1 or denied.stdout.strip() != 'no':
            raise AssertionError('Scheduler could read Pods outside dapr-system')
        checks['schedulerPodReadRbacLive'] = {**rbac, 'uid': uid, 'groups': groups, 'outsideNamespaceDenied': 'default'}

    def check_dapr_control_plane(self, pods):
        expected = self.record['checks']['daprLiveImagePins']['replicasByImage']
        counts = Counter()
        observed = {}
        for pod in pods:
            name = pod['metadata']['name']
            if name in observed:
                raise AssertionError('duplicate Dapr control-plane Pod: ' + name)
            observed[name] = {}
            for spec_key, status_key in (('containers', 'containerStatuses'), ('initContainers', 'initContainerStatuses')):
                desired = {c['name']: c['image'] for c in pod['spec'].get(spec_key, [])}
                statuses = pod['status'].get(status_key, [])
                if len(statuses) != len(desired) or {c['name'] for c in statuses} != desired.keys():
                    raise AssertionError('Dapr control-plane Pod is missing declared container status: ' + name)
                for container in statuses:
                    image = desired[container['name']]
                    reported = container['image']
                    image_id = container['imageID']
                    config = self.image_config_digests.get(image)
                    if (not container['ready'] or image not in expected or reported not in (image, config)
                            or image_id.rsplit('@', 1)[-1] not in (image.rsplit('@', 1)[-1], config)):
                        raise AssertionError('Dapr control-plane Pod failed ready image readback: ' + name)
                    counts[image] += 1
                    observed[name][container['name']] = {'image': image, 'statusImage': reported, 'imageID': image_id}
        if dict(counts) != expected:
            raise AssertionError('Dapr control-plane Pod count by image differs from the verified replica count')
        self.record['checks']['daprRunningImageIds'] = observed

    @staticmethod
    def application_dapr_inventory(objects):
        return {kind: sorted(obj['metadata']['name'] for obj in objects if obj['kind'] == kind)
                for kind in APPLICATION_DAPR_KINDS}

    def environment(self):
        super().environment()
        if len(self.components) != 8:
            raise AssertionError('reference environment must contain eight graph-derived Dapr Components')
        baseline = self.evidence / 'baseline-graph.json'
        baseline.write_text(json.dumps(campaign_graph_fixture(), indent=2) + '\n')
        baseline_values = self.evidence / 'baseline-values.json'
        baseline_values.write_text(json.dumps(fixture_values(campaign_graph_fixture()), indent=2) + '\n')
        candidate = self.evidence / 'candidate-graph.json'
        shutil.copy2(CHART / 'reference-graph.json', candidate)
        self.record['checks']['environmentComponentUnion'] = {
            'count': len(self.components), 'names': [component['metadata']['name'] for component in self.components],
            'baselineGraphSha256': sha(baseline), 'baselineValuesSha256': sha(baseline_values),
            'candidateGraphSha256': sha(candidate), 'candidateValuesSha256': sha(self.values)}
        self.retain_components('beforeInstall', self.component_baseline)

    def retain_components(self, phase, snapshot):
        path = self.evidence / f'component-ownership-{phase}.json'
        path.write_text(json.dumps(snapshot, indent=2, sort_keys=True) + '\n')
        self.record.setdefault('componentSnapshots', {})[phase] = {'file': path.name, 'sha256': sha(path)}

    def check_components(self, phase):
        self.retain_components(phase, super().check_components(phase))
        if phase in ('removeWorkloads', 'uninstall'):
            objects = json.loads(self.kube('get', 'configurations.dapr.io,subscriptions.dapr.io',
                                           '-n', 'reference', '-o', 'json').stdout)['items']
            observed = self.application_dapr_inventory(objects)
            expected = self.application_dapr_names if phase == 'removeWorkloads' else {kind: [] for kind in APPLICATION_DAPR_KINDS}
            if observed != expected:
                raise AssertionError('application Dapr Configuration or Subscription inventory differs during ' + phase)
            self.record['checks']['applicationDaprInventory'][phase] = observed

    def check_admitted_sidecar(self, pod):
        checked = super().check_admitted_sidecar(pod)
        containers = pod['spec'].get('containers', []) + pod['spec'].get('initContainers', [])
        daprd = next(c for c in containers if c['name'] == 'daprd')
        if daprd['image'] != self.tools['dapr']['images']['daprd']['linuxAmd64']:
            raise AssertionError('restricted admission injected an unexpected daprd image')
        next(c for c in checked if c['name'] == 'daprd')['image'] = daprd['image']
        return checked


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path, required=True, help='Fresh directory for this run’s durable evidence.')
    args = parser.parse_args()

    def interrupted(signum, frame):
        raise RuntimeError('qualification interrupted; cleaning owned resources')

    for signum in (signal.SIGTERM, signal.SIGINT):
        signal.signal(signum, interrupted)
    try:
        candidate = DaprCandidate(args.evidence)
        candidate.execute()
    except Exception as error:
        print('QUALIFICATION FAILED: ' + str(error), file=sys.stderr)
        return 1
    print('QUALIFICATION PASSED: ' + str(candidate.evidence / 'qualification.json'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
