#!/usr/bin/env python3
"""Preload an exact isolated image; remove the provisioning-only credential and Pod.

Applications use IfNotPresent without operator pull credentials. Image eviction is
an explicit future provisioning dependency, never permission to retain an operator session.
"""
import base64
import json
from pathlib import Path
import re
import sys

from deploy_native_registry import distribution_auth
from install_isolated_registry import Installer, NS, canonical, kube, kjson, require_creation_identity, sha
from native_journal import durable_write


def exact_native_image(native, image):
    digest = image.rsplit('@', 1)[1]
    return isinstance(native, str) and (native == digest or native.endswith('@' + digest)
        or native in ('containerd://' + digest, 'docker://' + digest))


def observed_identity(kind, name):
    fields = kube('get', kind, name, '-n', NS, '--ignore-not-found', '-o',
                  'jsonpath={.metadata.uid}{" "}{.metadata.resourceVersion}').stdout.decode().split()
    if not fields:
        return None
    if len(fields) != 2:
        raise RuntimeError('preload-resource-observation-malformed')
    return {'uid': fields[0], 'resourceVersion': fields[1]}


def reconcile_completion(installer, image, name, suffix):
    """Retained create-return identities plus exact pull evidence and absence only.

    This never creates, pulls, deletes or fabricates a legacy identity. Even an
    original resource still present after an uncertain cleanup requires its owner.
    """
    evidence = Path(installer.evidence)
    identities_path = evidence / ('preload-creation-identities-' + suffix + '.json')
    pull_path = evidence / ('preloaded-image-' + suffix + '.json')
    journal_path = evidence / ('preload-journal-' + suffix + '.json')
    old_resources = getattr(installer, 'resources', [])
    original_started = any(item.get('name') == name or item.get('reference', '').endswith('/' + name) for item in old_resources)
    if not any(path.exists() for path in (identities_path, pull_path, journal_path)) and not original_started:
        return False
    if not identities_path.exists() or not pull_path.exists():
        raise RuntimeError('preload-original-create-identities-and-pull-evidence-required-no-repeat')
    identities = json.loads(identities_path.read_text())
    secret = require_creation_identity(identities.get('secret'))
    pod = require_creation_identity(identities.get('pod'))
    retained = json.loads(pull_path.read_text())
    if (retained.get('image') != image or not isinstance(retained.get('node'), str) or not retained['node']
        or not exact_native_image(retained.get('nativeImageId'), image)
        or retained.get('provisioningCredentialRetained') is not False or retained.get('credentialMountedInProcess') is not False):
        raise RuntimeError('preload-retained-exact-image-node-native-image-evidence-required')
    for kind, identity in (('secret', secret), ('pod', pod)):
        observed = observed_identity(kind, name)
        if observed is not None:
            if observed['uid'] != identity['uid'] or (kind == 'secret' and observed != identity):
                raise RuntimeError('preload-original-resource-replaced-no-cleanup')
            raise RuntimeError('preload-original-cleanup-not-confirmed-no-delete-repeat')
    completion = {'phase': 'CompletedCleanupReconciled', 'image': image, 'originalCreationIdentities': identities,
                  'node': retained['node'], 'nativeImageId': retained['nativeImageId'],
                  'podAbsent': True, 'secretAbsent': True, 'provisioningCredentialRetained': False,
                  'credentialMountedInProcess': False, 'creationRepeated': False, 'pullRepeated': False}
    durable_write(evidence / ('preload-completion-' + suffix + '.json'), completion)
    return True


def preload(installer, image):
    if not re.fullmatch(r'registry\.hexalith\.com/ext-secrets-1-[a-z-]+@sha256:[0-9a-f]{64}', image):
        raise ValueError('exact-isolated-preload-image-required')
    suffix = sha(image.encode())[:12]
    name = 'ext-secrets-1-preload-' + suffix
    if reconcile_completion(installer, image, name, suffix):
        return
    journal_path = Path(installer.evidence) / ('preload-journal-' + suffix + '.json')
    journal = {'phase': 'SecretCreationStarted', 'image': image, 'name': name}
    durable_write(journal_path, journal, create_only=True)
    secret_created = False
    pod_created = False
    secret_identity = None
    pod_uid = None
    try:
        created_secret = installer.create({'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': name}, 'type': 'kubernetes.io/dockerconfigjson',
                          'data': {'.dockerconfigjson': base64.b64encode(canonical({'auths': {'registry.hexalith.com': distribution_auth()}})).decode()}})
        secret_created = True
        secret_identity = require_creation_identity(created_secret)
        durable_write(Path(installer.evidence) / ('preload-creation-identities-' + suffix + '.json'), {'secret': secret_identity})
        journal.update(phase='PodCreationStarted', secretIdentity=secret_identity)
        durable_write(journal_path, journal)
        created_pod = installer.create({'apiVersion': 'v1', 'kind': 'Pod', 'metadata': {'name': name}, 'spec': {
            'serviceAccountName': 'ext-secrets-1-registry', 'automountServiceAccountToken': False, 'restartPolicy': 'Never',
            'imagePullSecrets': [{'name': name}], 'securityContext': {'runAsNonRoot': True, 'runAsUser': 1654, 'seccompProfile': {'type': 'RuntimeDefault'}},
            'containers': [{'name': 'preload', 'image': image, 'imagePullPolicy': 'IfNotPresent', 'command': ['/bin/true'],
                            'securityContext': {'allowPrivilegeEscalation': False, 'capabilities': {'drop': ['ALL']}}}]}})
        pod_created = True
        pod_identity = require_creation_identity(created_pod)
        pod_uid = pod_identity['uid']
        durable_write(Path(installer.evidence) / ('preload-creation-identities-' + suffix + '.json'), {'secret': secret_identity, 'pod': pod_identity})
        journal.update(phase='PullStarted', podIdentity=pod_identity)
        durable_write(journal_path, journal)
        kube('wait', 'pod/' + name, '-n', NS, '--for=jsonpath={.status.phase}=Succeeded', '--timeout=50s', timeout=55)
        pod = kjson('get', 'pod', name, '-n', NS)
        if pod['metadata']['uid'] != pod_uid or pod.get('status', {}).get('phase') != 'Succeeded' or pod['spec']['containers'][0]['image'] != image:
            raise RuntimeError('preload-original-pod-replaced')
        native_id = pod['status']['containerStatuses'][0]['imageID']
        if not exact_native_image(native_id, image):
            raise RuntimeError('preload-native-image-digest-mismatch')
        durable_write(Path(installer.evidence) / ('preloaded-image-' + suffix + '.json'), {'image': image, 'node': pod['spec']['nodeName'],
                         'nativeImageId': native_id, 'provisioningCredentialRetained': False,
                         'credentialMountedInProcess': False})
    finally:
        cleanup_failed = False
        if pod_created:
            try:
                if not pod_uid:
                    raise RuntimeError('preload-original-pod-cleanup-unknown')
                journal['podCleanupPhase'] = 'Started'
                durable_write(journal_path, journal)
                kube('delete', '--raw', '/api/v1/namespaces/' + NS + '/pods/' + name, '-f', '-',
                     data=canonical({'apiVersion': 'v1', 'kind': 'DeleteOptions', 'preconditions': {'uid': pod_uid}}))
                journal['podCleanupPhase'] = 'Acknowledged'
                durable_write(journal_path, journal)
            except Exception:
                cleanup_failed = True
        if secret_created:
            try:
                if not secret_identity:
                    raise RuntimeError('preload-original-secret-cleanup-unknown')
                journal['secretCleanupPhase'] = 'Started'
                durable_write(journal_path, journal)
                kube('delete', '--raw', '/api/v1/namespaces/' + NS + '/secrets/' + name, '-f', '-',
                     data=canonical({'apiVersion': 'v1', 'kind': 'DeleteOptions',
                                     'preconditions': secret_identity}))
                journal['secretCleanupPhase'] = 'Acknowledged'
                durable_write(journal_path, journal)
            except Exception:
                cleanup_failed = True
        if cleanup_failed:
            raise RuntimeError('preload-owned-resource-cleanup-incomplete')
    # The create identities and pull evidence remain untouched during recovery.
    kube('wait', '--for=delete', 'pod/' + name, '-n', NS, '--timeout=30s', timeout=35)
    kube('wait', '--for=delete', 'secret/' + name, '-n', NS, '--timeout=10s', timeout=15)
    reconcile_completion(installer, image, name, suffix)


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    instance.created = json.loads((instance.evidence / 'created-resource-references.json').read_text())
    try:
        preload(instance, sys.argv[2])
        print('exact isolated image preloaded; provisioning Pod and operator credential removed')
    finally:
        instance.close()
