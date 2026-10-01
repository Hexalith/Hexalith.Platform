#!/usr/bin/env python3
"""Read-only native management census. Observations never authorize retirement."""
import argparse
import base64
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import ssl
import stat
import subprocess
import tempfile
import urllib.error
import urllib.parse
import urllib.request

from evidence import Attempt, canonical, digest, file_digest, now

IDENTITY = re.compile(r'[A-Za-z0-9_.:/@+\-=]+')
HEX = re.compile(r'[0-9a-f]{64}')
VERSION = re.compile(r'v?(\d+)\.(\d+)\.(\d+)(?:\+k3s\d+)?')
PROTECTED_KINDS = {'Namespace', 'PersistentVolume', 'PersistentVolumeClaim', 'StorageClass'}
MANAGEMENT_LABELS = ('app.kubernetes.io/managed-by', 'app.kubernetes.io/part-of',
                     'kubesphere.io/workspace', 'kubesphere.io/managed')


def safe(value):
    return value if isinstance(value, str) and len(value) <= 300 and IDENTITY.fullmatch(value) else None


def identities(values):
    return [safe(v) for v in values if safe(v)] if isinstance(values, list) else []


def reference(value):
    return {key: safe(value.get(key)) for key in ('apiVersion', 'kind', 'name', 'uid')}


def kubesphere_mark(value):
    domain = value.split('/', 1)[0] if isinstance(value, str) else ''
    return domain == 'kubesphere.io' or domain.endswith('.kubesphere.io')


def port_identity(value):
    return value if isinstance(value, int) and not isinstance(value, bool) and 0 < value <= 65535 else None


def project_resource(obj):
    """Secret/ConfigMap data, arbitrary annotations, specs and RBAC rules never leave custody."""
    meta, spec = obj.get('metadata', {}), obj.get('spec', {})
    result = {'apiVersion': safe(obj.get('apiVersion')), 'kind': safe(obj.get('kind')),
              'name': safe(meta.get('name')), 'namespace': safe(meta.get('namespace')),
              'uid': safe(meta.get('uid')), 'resourceVersion': safe(meta.get('resourceVersion')),
              'deletionTimestamp': safe(meta.get('deletionTimestamp')),
              'owners': [reference(v) for v in meta.get('ownerReferences', [])],
              'finalizers': identities(meta.get('finalizers', [])),
              'managementLabels': {k: safe(v) for k, v in meta.get('labels', {}).items()
                                   if safe(k) and (k in MANAGEMENT_LABELS or kubesphere_mark(k))},
              'helmRelease': {k: safe(meta.get('annotations', {}).get('meta.helm.sh/' + k))
                              for k in ('release-name', 'release-namespace')
                              if safe(meta.get('annotations', {}).get('meta.helm.sh/' + k))}}
    kind = result['kind']
    if kind == 'Node':
        info = obj.get('status', {}).get('nodeInfo', {})
        result['runtime'] = {k: safe(info.get(k)) for k in ('kubeletVersion', 'kubeProxyVersion',
                            'containerRuntimeVersion', 'operatingSystem', 'architecture', 'kernelVersion')}
        result['capacity'] = {k: safe(obj.get('status', {}).get('capacity', {}).get(k))
                              for k in ('cpu', 'memory', 'ephemeral-storage', 'pods')}
    if kind in ('Deployment', 'StatefulSet', 'DaemonSet', 'Job', 'CronJob', 'Pod'):
        template = spec.get('jobTemplate', {}).get('spec', {}).get('template', {}) if kind == 'CronJob' else spec.get('template', {})
        pod = spec if kind == 'Pod' else template.get('spec', {})
        containers = [c for key in ('containers', 'initContainers', 'ephemeralContainers') for c in pod.get(key, [])]
        result['images'] = identities([c.get('image') for c in containers])
        result['imageIds'] = identities([c.get('imageID') for key in ('containerStatuses', 'initContainerStatuses')
                                        for c in obj.get('status', {}).get(key, [])])
        result['serviceAccount'] = safe(pod.get('serviceAccountName'))
        result['claims'] = identities([v.get('persistentVolumeClaim', {}).get('claimName') for v in pod.get('volumes', [])])
        secrets, configmaps = [], []
        for volume in pod.get('volumes', []):
            secrets.append(volume.get('secret', {}).get('secretName'))
            configmaps.append(volume.get('configMap', {}).get('name'))
            for source in volume.get('projected', {}).get('sources', []):
                secrets.append(source.get('secret', {}).get('name'))
                configmaps.append(source.get('configMap', {}).get('name'))
        for container in containers:
            for env in container.get('env', []):
                secrets.append(env.get('valueFrom', {}).get('secretKeyRef', {}).get('name'))
                configmaps.append(env.get('valueFrom', {}).get('configMapKeyRef', {}).get('name'))
            for env in container.get('envFrom', []):
                secrets.append(env.get('secretRef', {}).get('name'))
                configmaps.append(env.get('configMapRef', {}).get('name'))
        secrets.extend(v.get('name') for v in pod.get('imagePullSecrets', []))
        result['secretReferences'] = sorted(set(identities(secrets)))
        result['configMapReferences'] = sorted(set(identities(configmaps)))
        result['configReferences'] = sorted(set(result['secretReferences'] + result['configMapReferences']))
        if isinstance(spec.get('replicas'), int):
            result['replicas'] = spec['replicas']
    if kind == 'PersistentVolumeClaim':
        result['binding'] = {'volumeName': safe(spec.get('volumeName')), 'storageClass': safe(spec.get('storageClassName')),
                             'phase': safe(obj.get('status', {}).get('phase'))}
    if kind == 'PersistentVolume':
        result['binding'] = {k: safe(spec.get('claimRef', {}).get(k)) for k in ('name', 'namespace', 'uid')}
        result['storageClass'] = safe(spec.get('storageClassName'))
        result['reclaimPolicy'] = safe(spec.get('persistentVolumeReclaimPolicy'))
    if kind in ('PersistentVolume', 'PersistentVolumeClaim'):
        result['storagePropertiesSha256'] = digest(canonical(spec))
        result['volumeMode'] = safe(spec.get('volumeMode'))
        result['accessModes'] = identities(spec.get('accessModes', []))
    if kind == 'StorageClass':
        fields = ('provisioner', 'reclaimPolicy', 'volumeBindingMode', 'allowVolumeExpansion',
                  'mountOptions', 'parameters', 'allowedTopologies')
        result['storagePropertiesSha256'] = digest(canonical({k: obj.get(k) for k in fields}))
        result['provisioner'] = safe(obj.get('provisioner'))
        result['reclaimPolicy'] = safe(obj.get('reclaimPolicy'))
        result['volumeBindingMode'] = safe(obj.get('volumeBindingMode'))
    if kind == 'Namespace':
        result['namespaceFinalizers'] = identities(spec.get('finalizers', []))
    if kind in ('RoleBinding', 'ClusterRoleBinding'):
        result['roleRef'] = reference(obj.get('roleRef', {}))
        result['subjects'] = [{k: safe(s.get(k)) for k in ('kind', 'name', 'namespace')} for s in obj.get('subjects', [])]
    if kind == 'CustomResourceDefinition':
        result['customResource'] = {'group': safe(spec.get('group')), 'plural': safe(spec.get('names', {}).get('plural')),
                                    'scope': safe(spec.get('scope')),
                                    'versions': [{'name': safe(v.get('name')), 'served': v.get('served') is True,
                                                  'storage': v.get('storage') is True} for v in spec.get('versions', [])]}
    if kind in ('MutatingWebhookConfiguration', 'ValidatingWebhookConfiguration'):
        result['webhooks'] = [{'name': safe(v.get('name')), 'failurePolicy': safe(v.get('failurePolicy')),
                               'service': {k: safe(v.get('clientConfig', {}).get('service', {}).get(k))
                                           for k in ('namespace', 'name', 'path')},
                               'externalUrlConfigured': bool(v.get('clientConfig', {}).get('url')),
                               'rulesDigest': digest(canonical(v.get('rules', [])))} for v in obj.get('webhooks', [])]
    if kind == 'APIService':
        result['service'] = {k: safe(spec.get('service', {}).get(k)) for k in ('namespace', 'name')}
    if kind in ('Ingress', 'HTTPRoute', 'Gateway'):
        result['routeHosts'] = identities(spec.get('hostnames', []) + [v.get('host') for v in spec.get('rules', [])])
        result['backends'] = [{k: safe(v.get(k)) for k in ('name', 'namespace', 'kind')}
                              for rule in spec.get('rules', []) for v in rule.get('backendRefs', [])]
    if kind == 'Ingress':
        backends = [spec.get('defaultBackend', {})] + [p.get('backend', {}) for rule in spec.get('rules', [])
                    for p in rule.get('http', {}).get('paths', [])]
        result['backends'] = [{'kind': 'Service', 'name': safe(v['service'].get('name')),
                               'namespace': result['namespace'], 'port': port_identity(v['service'].get('port', {}).get('number')),
                               'portName': safe(v['service'].get('port', {}).get('name'))}
                              if 'service' in v else {k: safe(v.get('resource', {}).get(k))
                                                     for k in ('apiGroup', 'kind', 'name')} for v in backends if v]
        result['secretReferences'] = identities([v.get('secretName') for v in spec.get('tls', [])])
    if kind == 'Gateway':
        result['listeners'] = [{**{k: safe(v.get(k)) for k in ('name', 'protocol', 'hostname')},
                                'port': port_identity(v.get('port')),
                                'certificateReferences': [{k: safe(ref.get(k)) for k in ('group', 'kind', 'name', 'namespace')}
                                  for ref in v.get('tls', {}).get('certificateRefs', [])]} for v in spec.get('listeners', [])]
        result['routeHosts'] = identities([v.get('hostname') for v in spec.get('listeners', [])])
    # Catalog spec/config is private. Existence of an Extension is not installation proof.
    if kind in ('Extension', 'ExtensionVersion', 'InstallPlan', 'ClusterConfiguration'):
        result['extensionEvidenceClass'] = 'installation-plan' if kind == 'InstallPlan' else 'catalog-or-configuration'
    return result


def management_resource(item):
    return bool((item.get('namespace') or '').startswith('kubesphere')
                or '.kubesphere.io' in (item.get('apiVersion') or '')
                or 'kubesphere.io' in (item.get('apiVersion') or '')
                or (item.get('helmRelease', {}).get('release-namespace') or '').startswith('kubesphere')
                or (item.get('customResource', {}).get('group') or '').endswith('kubesphere.io')
                or any(kubesphere_mark(k) for k in item.get('managementLabels', {}))
                or any(kubesphere_mark(v) for v in item.get('finalizers', []))
                or any('kubesphere' in str(v) for v in item.get('managementLabels', {}).values()))


def classify(inventory):
    by_uid = {v['uid']: v for v in inventory if v.get('uid')}
    entries = []
    for item in inventory:
        if not management_resource(item):
            continue
        consumers = [{'uid': v['uid'], 'kind': v['kind'], 'namespace': v['namespace'], 'name': v['name']}
                     for v in inventory if any(o.get('uid') == item['uid'] for o in v['owners'])]
        entries.append({'resource': {k: item[k] for k in ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion')},
                        'observation': item.get('extensionEvidenceClass', 'native-resource'),
                        'ownersResolved': all(o.get('uid') in by_uid for o in item['owners']),
                        'observedOwnedDependents': consumers, 'finalizers': item['finalizers'],
                        'disposition': 'unresolved', 'approvedReplacement': None,
                        'consumerCoverage': 'owner-references-only; configuration/RBAC/routes require review',
                        'deletionEffects': 'unverified', 'retirementAccepted': False})
    return entries


def validate_ownership(entries):
    errors = []
    if not entries:
        errors.append('management-inventory-empty')
    for e in entries:
        if e.get('disposition') not in ('unused-retired', 'native-owned', 'replaced'):
            errors.append('unresolved-disposition')
        if e.get('ownersResolved') is not True or e.get('consumerCoverage') != 'verified':
            errors.append('unresolved-owner-or-consumer')
        if e.get('deletionEffects') not in ('verified-preserve', 'verified-remove'):
            errors.append('unverified-propagation')
        if e.get('disposition') == 'replaced' and not e.get('approvedReplacement'):
            errors.append('missing-approved-replacement')
    return sorted(set(errors))


def validate_pins(record):
    errors = []
    for component in ('rancher', 'managementK3s', 'workloadKubernetes'):
        pin = record.get(component, {})
        if pin.get('prerelease') is not False:
            errors.append('prerelease-' + component)
        if not VERSION.fullmatch(pin.get('version', '')):
            errors.append('missing-or-prerelease-' + component)
        if not HEX.fullmatch(pin.get('artifactSha256', '')):
            errors.append('missing-artifact-digest-' + component)
    if not HEX.fullmatch(record.get('chart', {}).get('sha256', '')):
        errors.append('missing-chart-digest')
    if not record.get('images') or any(not re.fullmatch(r'.+@sha256:[0-9a-f]{64}', v.get('identity', '')) for v in record.get('images', [])):
        errors.append('unqualified-image-identity')
    for category in ('hostingMatrix', 'importMatrix', 'securityReview', 'licenses', 'managementTools'):
        proof = record.get(category, {})
        if proof.get('verified') is not True or not HEX.fullmatch(proof.get('evidenceSha256', '')):
            errors.append('unqualified-' + category)
    if record.get('hostingMatrix', {}).get('distro') != 'k3s':
        errors.append('generic-import-does-not-qualify-hosting')
    try:
        observed = datetime.fromisoformat(record['checkedAt'].replace('Z', '+00:00'))
        delta = datetime.now(timezone.utc) - observed
        if not observed.tzinfo or not 0 <= delta.total_seconds() <= 86400:
            errors.append('stale-or-future-pin-review')
    except (KeyError, ValueError, TypeError):
        errors.append('missing-pin-review-date')
    return sorted(set(errors))


def validate_authority(record):
    """Validate signed-verification outcomes and complete independent current authority lineage."""
    errors = []
    if record.get('nativeAccess') != 'verified' or record.get('nativePath') != 'direct':
        errors.append('native-access-unverified-or-proxy-only')
    for field in ('mfaVerified', 'unauthorizedDenied', 'publicDenied', 'independentCustodyReadback',
                  'administratorSignatureVerified', 'independentOffsiteLineageReadback', 'restoredRevocationDenied'):
        if record.get(field) is not True:
            errors.append('missing-' + field)
    events = record.get('events', [])
    previous = None
    scopes = {}
    for sequence, event in enumerate(events, 1):
        if (event.get('sequence') != sequence or event.get('previousSha256') != previous
                or event.get('signatureVerified') is not True or not HEX.fullmatch(event.get('sha256', ''))):
            errors.append('gapped-or-unverified-authority-lineage')
        if event.get('approvedBy') != 'Administrator' or event.get('action') not in ('grant', 'revoke'):
            errors.append('unapproved-authority-event')
        principal = event.get('principal')
        if event.get('action') == 'grant':
            scopes[principal] = event.get('scope')
            if principal == 'deputy' and event.get('scope') not in ('scoped-native-recovery', 'scoped-management-recovery'):
                errors.append('deputy-authority-expanded')
        else:
            scopes.pop(principal, None)
        previous = event.get('sha256')
    if not events or record.get('currentHeadSha256') != previous or record.get('restoredScopes') != scopes:
        errors.append('missing-or-conflicting-current-authority')
    return sorted(set(errors))


def compatible(client, server):
    c, s = VERSION.fullmatch(client), VERSION.fullmatch(server)
    return bool(c and s and c[1] == s[1] and abs(int(c[2]) - int(s[2])) <= 1)


def validate_native_endpoint(value):
    server = urllib.parse.urlparse(value)
    if (server.scheme != 'https' or not server.hostname or server.username or server.password
            or server.query or server.fragment or server.path not in ('', '/')):
        raise ValueError('native-path-must-be-direct-verified-tls')
    return server


class Capture:
    def __init__(self, args, attempt):
        self.args, self.attempt = args, attempt
        self.coverage, self.inventory = [], []
        self.collected_resource_types = set()
        self.counter = 0

    def command(self, name, argv, parse=True):
        self.counter += 1
        try:
            r = subprocess.run(argv, capture_output=True, timeout=45)
            raw, code = r.stdout, r.returncode
            self.attempt.encrypt(f'{self.counter:04d}-{name}', canonical({'exitCode': code,
                                  'stdoutBase64': base64.b64encode(raw).decode(),
                                  'stderrBase64': base64.b64encode(r.stderr).decode()}), self.args.age, self.args.recipient)
            data = json.loads(raw) if code == 0 and parse else raw
            state = 'observed' if code == 0 else 'failed'
        except subprocess.TimeoutExpired:
            data, code, state = None, None, 'timed-out'
        except (ValueError, OSError):
            data, code, state = None, None, 'invalid-or-inaccessible'
        self.coverage.append({'step': name, 'state': state, 'exitCode': code})
        return data if state == 'observed' else None

    def kube(self, name, args, parse=True):
        return self.command(name, [str(self.args.kubectl), '--kubeconfig', str(self.args.kubeconfig),
                                  '--context', self.args.context, '--request-timeout=30s', *args], parse)

    def raw(self, name, path):
        return self.kube(name, ['get', '--raw', path])

    def gap(self, step):
        self.coverage.append({'step': step, 'state': 'invalid-schema', 'exitCode': 0})

    def resources(self, data, step):
        if not isinstance(data, dict) or not isinstance(data.get('resources'), list):
            self.gap(step)
            return []
        valid = []
        for resource in data['resources']:
            if (not isinstance(resource, dict) or not safe(resource.get('name'))
                    or not safe(resource.get('kind')) or not isinstance(resource.get('verbs'), list)
                    or any(not isinstance(v, str) for v in resource['verbs'])):
                self.gap(step)
            else:
                valid.append(resource)
        return valid

    def list_resource(self, gv, prefix, resource, step):
        continuation, seen = '', set()
        for page in range(1, 101):
            query = '?limit=500' + ('&continue=' + urllib.parse.quote(continuation, safe='') if continuation else '')
            listing = self.raw(step + '-' + str(page), prefix + '/' + resource['name'] + query)
            if listing is None:
                return
            if (not isinstance(listing, dict) or not isinstance(listing.get('items'), list)
                    or not isinstance(listing.get('metadata', {}), dict)
                    or not isinstance(listing.get('metadata', {}).get('continue', ''), str)):
                self.gap(step)
                return
            valid_page = True
            for raw in listing['items']:
                try:
                    if not isinstance(raw, dict) or not isinstance(raw.get('metadata'), dict):
                        raise ValueError('malformed-resource')
                    obj = dict(raw, apiVersion=raw.get('apiVersion', gv), kind=raw.get('kind', resource['kind']))
                    meta = obj['metadata']
                    if (any(not safe(meta.get(k)) for k in ('name', 'uid', 'resourceVersion'))
                            or not safe(obj['apiVersion']) or not safe(obj['kind'])
                            or (meta.get('namespace') is not None and not safe(meta['namespace']))
                            or not isinstance(meta.get('ownerReferences', []), list)
                            or not isinstance(meta.get('finalizers', []), list)
                            or any(not safe(v) for v in meta.get('finalizers', []))
                            or any(not isinstance(owner, dict) or not safe(owner.get('uid'))
                                   for owner in meta.get('ownerReferences', []))):
                        raise ValueError('malformed-resource')
                    self.inventory.append(project_resource(obj))
                except (ValueError, TypeError, KeyError, AttributeError):
                    self.gap(step + '-object')
                    valid_page = False
            if valid_page:
                self.collected_resource_types.add((gv, resource['name']))
            continuation = listing.get('metadata', {}).get('continue', '')
            if not continuation:
                return
            if continuation in seen:
                self.gap(step + '-pagination')
                return
            seen.add(continuation)
        self.coverage.append({'step': step + '-pagination', 'state': 'failed', 'exitCode': None})

    def helm_releases(self):
        releases, offset, maximum = [], 0, 256
        for page in range(100):
            listing = self.command('helm-releases-' + str(page + 1), [str(self.args.helm), '--kubeconfig', str(self.args.kubeconfig),
                    '--kube-context', self.args.context, 'list', '--all-namespaces', '--all', '--max', str(maximum),
                    '--offset', str(offset), '-o', 'json'])
            if listing is None:
                return releases
            if (not isinstance(listing, list) or any(not isinstance(r, dict)
                    or any(not safe(r.get(k)) for k in ('name', 'namespace', 'chart', 'status')) for r in listing)):
                self.gap('helm-releases')
                return releases
            releases.extend(listing)
            if len(listing) < maximum:
                return releases
            offset += len(listing)
        self.coverage.append({'step': 'helm-pagination', 'state': 'failed', 'exitCode': None})
        return releases

    def collect(self):
        version = self.kube('versions', ['version', '-o', 'json'])
        if not version or not compatible(version.get('clientVersion', {}).get('gitVersion', ''),
                                         version.get('serverVersion', {}).get('gitVersion', '')):
            raise ValueError('kubectl-server-skew-unqualified')
        config = self.kube('native-config', ['config', 'view', '--minify', '--flatten', '--raw', '-o', 'json'])
        if not config or len(config.get('clusters', [])) != 1:
            raise ValueError('native-context-unresolved')
        native = config['clusters'][0]['cluster']
        server = validate_native_endpoint(native.get('server', ''))
        if (native.get('insecure-skip-tls-verify')
                or native.get('proxy-url') or not native.get('certificate-authority-data')):
            raise ValueError('native-path-must-be-direct-verified-tls')
        access = {'nativePath': 'direct', 'authorizedRead': False,
                  'unauthorizedDenied': False, 'publicDenied': False,
                  'custodyPermissionsRestricted': stat.S_IMODE(self.args.kubeconfig.stat().st_mode) & 0o077 == 0,
                  'effectiveCustodyAccepted': False,
                  'permissionObservation': 'resolved file POSIX mode only; independent custody/ACL verification pending',
                  'independentCustodyReadback': False, 'mfaVerified': False,
                  'managementGrantLineageVerified': False, 'acceptance': 'incomplete'}
        tls = ssl.create_default_context(cadata=base64.b64decode(native['certificate-authority-data']).decode())
        # Credential-free HTTPS request; only a TLS-authenticated 401/403 proves denial.
        try:
            urllib.request.urlopen(urllib.request.Request(native['server'].rstrip('/') + '/api/v1/namespaces'), context=tls, timeout=10).close()
        except urllib.error.HTTPError as error:
            access['unauthorizedDenied'] = error.code in (401, 403)
            access['unauthorizedHttpStatus'] = error.code
        except (urllib.error.URLError, TimeoutError, OSError):
            access['unauthorizedProbe'] = 'unreachable-or-unverified-tls; denial-not-proven'
        core = self.raw('core-discovery', '/api/v1')
        groups = self.raw('group-discovery', '/apis')
        if (not isinstance(core, dict) or not isinstance(groups, dict)
                or not isinstance(groups.get('groups'), list)):
            self.gap('api-discovery')
            raise ValueError('api-discovery-failed')
        discovery = [('v1', '/api/v1', core)]
        for group in groups.get('groups', []):
            preferred = group.get('preferredVersion') if isinstance(group, dict) else None
            gv = safe(preferred.get('groupVersion')) if isinstance(preferred, dict) else None
            if gv:
                data = self.raw('discovery-' + gv.replace('/', '-').replace('.', '-'), '/apis/' + gv)
                if data is not None:
                    discovery.append((gv, '/apis/' + gv, data))
            else:
                self.gap('group-preferred-version')
        for gv, prefix, data in discovery:
            for resource in self.resources(data, 'discovery-' + gv.replace('/', '-')):
                name = safe(resource.get('name'))
                if not name or '/' in name or 'list' not in resource.get('verbs', []):
                    continue
                self.list_resource(gv, prefix, resource, 'list-' + gv.replace('/', '-').replace('.', '-') + '-' + name)
        # A group's preferred version need not serve every installed CRD. Discover
        # the CRD's actual storage/served version before calling an empty list unused.
        for crd in [v for v in self.inventory if v['kind'] == 'CustomResourceDefinition']:
            cr = crd['customResource']
            served = [v for v in cr['versions'] if v['served']]
            preferred = next((v for v in served if v['storage']), served[0] if served else None)
            if preferred is None:
                self.coverage.append({'step': 'crd-no-served-version-' + crd['name'], 'state': 'failed', 'exitCode': None})
                continue
            gv = cr['group'] + '/' + preferred['name']
            observed = (gv, cr['plural']) in self.collected_resource_types
            if observed:
                continue
            data = self.raw('crd-discovery-' + gv.replace('/', '-').replace('.', '-'), '/apis/' + gv)
            resource = next((r for r in self.resources(data, 'crd-discovery-' + crd['name'])
                             if r['name'] == cr['plural'] and 'list' in r['verbs']), None)
            if resource is None:
                self.coverage.append({'step': 'crd-instance-discovery-' + crd['name'], 'state': 'failed', 'exitCode': None})
                continue
            discovery.append((gv, '/apis/' + gv, data))
            self.list_resource(gv, '/apis/' + gv, resource, 'crd-list-' + crd['name'].replace('.', '-'))
        # Some resources (notably Events) are exposed through multiple groups.
        # Keep one observation per native UID; every request remains encrypted.
        self.inventory = list({v['uid']: v for v in self.inventory if v.get('uid')}.values())
        access['authorizedRead'] = any(v['kind'] == 'Namespace' for v in self.inventory)
        releases = self.helm_releases()
        charts = []
        if isinstance(releases, list):
            for release in releases:
                charts.append({k: safe(release.get(k)) for k in ('name', 'namespace', 'chart', 'app_version', 'status')})
                if (release.get('namespace') or '').startswith('kubesphere'):
                    for verb in ('values', 'manifest', 'hooks'):
                        extra = ['--all', '-o', 'json'] if verb == 'values' else []
                        self.command('helm-' + verb + '-' + release['name'], [str(self.args.helm), '--kubeconfig', str(self.args.kubeconfig),
                                     '--kube-context', self.args.context, 'get', verb, release['name'], '-n', release['namespace'], *extra], parse=verb == 'values')
        # ConfigMap containing kubeadm configuration is encrypted in core collection.
        crds = [v for v in self.inventory if v['kind'] == 'CustomResourceDefinition']
        listed = {gv for gv, _, _ in discovery}
        for crd in crds:
            cr = crd['customResource']
            served = {cr['group'] + '/' + v['name'] for v in cr['versions'] if v['served']}
            if not served.intersection(listed):
                self.coverage.append({'step': 'crd-instance-discovery-' + crd['name'], 'state': 'failed', 'exitCode': None})
        self.attempt.record('inventory.json', {'schemaVersion': 1, 'capturedAt': now(), 'context': safe(self.args.context),
                         'nativeEndpoint': native['server'], 'sourceClusterUid': next((v['uid'] for v in self.inventory
                         if v['kind'] == 'Namespace' and v['name'] == 'kube-system'), None),
                         'clientVersion': safe(version['clientVersion']['gitVersion']),
                         'serverVersion': safe(version['serverVersion']['gitVersion']), 'helmReleases': charts,
                         'resources': self.inventory, 'coverage': self.coverage, 'pointInTimeAtomic': False,
                         'nodeSshKubeadmEtcdRuntime': 'not-collected; API node runtime and encrypted kubeadm ConfigMap only'})
        self.attempt.record('access.json', access)
        dispositions = classify(self.inventory)
        self.attempt.record('capabilities.json', {'entries': dispositions, 'validationErrors': validate_ownership(dispositions),
                            'catalogIsNotInstallation': True, 'accepted': False})
        self.attempt.record('retirement-allowlist.json', {'state': 'no-production-removals-approved', 'actions': [],
                            'protectedResources': [v for v in self.inventory if v['kind'] in PROTECTED_KINDS],
                            'reason': 'Ownership, consumers, installed-controller finalizers and propagation are unaccepted'})
        return access


def collect(args):
    # Check tools before allocating the attempt. Hashes prove identity, not release authenticity.
    for path in (args.kubectl, args.helm, args.age, args.kubeconfig):
        if not path.is_file():
            raise ValueError('missing-tool-or-native-kubeconfig')
    maintenance_digest = file_digest(args.project_root / 'eng/kubernetes-upgrade/MAINTENANCE.md')
    attempt = Attempt(args.project_root, args.evidence_root, args.attempt_id)
    attempt.record('attempt.json', {'schemaVersion': 1, 'story': '4.26', 'attemptId': args.attempt_id,
                    'capturedAt': now(), 'operator': safe(args.operator), 'context': safe(args.context),
                    'mode': 'read-only-observation', 'mutationAuthorized': False, 'signed': False,
                    'maintenanceProposalSha256': maintenance_digest})
    tools = []
    state = 'incomplete'
    capture = Capture(args, attempt)
    try:
        for name, path, command in [('kubectl', args.kubectl, ['version', '--client', '-o', 'json']),
                                   ('helm', args.helm, ['version', '--short']), ('age', args.age, ['--version'])]:
            r = subprocess.run([str(path), *command], capture_output=True, timeout=30)
            tools.append({'name': name, 'sha256': file_digest(path), 'versionOutputSha256': digest(r.stdout),
                          'versionExitCode': r.returncode, 'releaseAuthenticityVerified': False,
                          'qualifiedForManagement': False})
            if r.returncode:
                raise ValueError('tool-version-preflight-failed')
        capture.collect()
        if any(v['state'] != 'observed' for v in capture.coverage):
            state = 'failed-closed'
    except (ValueError, OSError, subprocess.TimeoutExpired, TypeError, KeyError, AttributeError):
        # Error details are withheld: external failures can contain secret/config values.
        state = 'failed-closed'
    attempt.record('tools.json', {'tools': tools, 'applicationExecutorFloorChanged': False})
    if state == 'failed-closed':
        attempt.record('capture-failure.json', {'state': state, 'coverage': capture.coverage,
                               'reason': 'tools, schema, discovery, identity, skew, encryption or access failed; inspect private diagnostics'})
    attempt.record('criteria.json', {'criteria': [{'criterion': n, 'state': state,
                     'reason': reason} for n, reason in enumerate([
                     'Native census is an observation; node/etcd/coverage/currency and independent export readback require acceptance',
                     'Capability dispositions and consumers/deletion effects require explicit owner review',
                     'Current stable charts/images/matrices/licenses/security/tool authenticity require acceptance',
                     'Host virtualization, spare capacity, endpoint and cost need qualification; procurement may follow',
                     'Read access and anonymous denial do not establish public denial, MFA or independent recovery custody',
                     'No Administrator-approved independent grant/revocation lineage supplied',
                     'Synthetic native rehearsal is separate and cannot establish production controller deletion effects',
                     'Runbooks are reviewable proposals; no retirement approval or hop gate is opened'], 1)],
                     'qualificationAccepted': False, 'retirementAuthorized': False, 'upgradeGate': 'closed'})
    attempt.finish()
    return attempt.directory, state


def parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--project-root', type=Path, default=Path(__file__).resolve().parents[2])
    p.add_argument('--evidence-root', type=Path, default=Path.home() / 'hexalith-management-evidence')
    p.add_argument('--attempt-id', default=datetime.now(timezone.utc).strftime('%Y%m%dt%H%M%Sz-census'))
    p.add_argument('--operator', required=True)
    p.add_argument('--context', required=True, help='Explicit native context; never the implicit current context')
    p.add_argument('--kubeconfig', type=Path, required=True)
    p.add_argument('--kubectl', type=Path, required=True)
    p.add_argument('--helm', type=Path, required=True)
    p.add_argument('--age', type=Path, required=True)
    p.add_argument('--recipient', required=True, help='Administrator-owned age/SSH public recipient; no private key')
    return p


def main():
    p = parser()
    try:
        directory, state = collect(p.parse_args())
    except (ValueError, OSError, subprocess.TimeoutExpired):
        p.exit(2, 'Qualification refused; inspect inputs and restricted custody. No production mutation.\n')
    print(f'Private attempt: {directory}\nQualification: {state}; all mutation gates closed')
    if state == 'failed-closed':
        raise SystemExit(2)


if __name__ == '__main__':
    main()
