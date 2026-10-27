#!/usr/bin/env python3
"""Preload an exact isolated image; remove the provisioning-only credential and Pod.

Applications use IfNotPresent without operator pull credentials. Image eviction is
an explicit future provisioning dependency, never permission to retain an operator session.
"""
import base64
import json
import sys

from deploy_native_registry import distribution_auth
from install_isolated_registry import Installer, NS, canonical, kube, kjson, require_creation_identity, sha


def preload(installer, image):
    assert image.startswith('registry.hexalith.com/ext-secrets-1-') and '@sha256:' in image
    suffix = sha(image.encode())[:12]
    name = 'ext-secrets-1-preload-' + suffix
    secret_created = False
    pod_created = False
    secret_identity = None
    pod_uid = None
    try:
        created_secret = installer.create({'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': name}, 'type': 'kubernetes.io/dockerconfigjson',
                          'data': {'.dockerconfigjson': base64.b64encode(canonical({'auths': {'registry.hexalith.com': distribution_auth()}})).decode()}})
        secret_created = True
        secret_identity = require_creation_identity(created_secret)
        installer.write('preload-creation-identities-' + suffix + '.json', {'secret': secret_identity})
        created_pod = installer.create({'apiVersion': 'v1', 'kind': 'Pod', 'metadata': {'name': name}, 'spec': {
            'serviceAccountName': 'ext-secrets-1-registry', 'automountServiceAccountToken': False, 'restartPolicy': 'Never',
            'imagePullSecrets': [{'name': name}], 'securityContext': {'runAsNonRoot': True, 'runAsUser': 1654, 'seccompProfile': {'type': 'RuntimeDefault'}},
            'containers': [{'name': 'preload', 'image': image, 'imagePullPolicy': 'IfNotPresent', 'command': ['/bin/true'],
                            'securityContext': {'allowPrivilegeEscalation': False, 'capabilities': {'drop': ['ALL']}}}]}})
        pod_created = True
        pod_identity = require_creation_identity(created_pod)
        pod_uid = pod_identity['uid']
        installer.write('preload-creation-identities-' + suffix + '.json', {'secret': secret_identity, 'pod': pod_identity})
        kube('wait', 'pod/' + name, '-n', NS, '--for=jsonpath={.status.phase}=Succeeded', '--timeout=50s', timeout=55)
        pod = kjson('get', 'pod', name, '-n', NS)
        if pod['metadata']['uid'] != pod_uid:
            raise RuntimeError('preload-original-pod-replaced')
        installer.write('preloaded-image-' + suffix + '.json', {'image': image, 'node': pod['spec']['nodeName'],
                         'nativeImageId': pod['status']['containerStatuses'][0]['imageID'], 'provisioningCredentialRetained': False,
                         'credentialMountedInProcess': False})
    finally:
        cleanup_failed = False
        if pod_created:
            try:
                if not pod_uid:
                    raise RuntimeError('preload-original-pod-cleanup-unknown')
                kube('delete', '--raw', '/api/v1/namespaces/' + NS + '/pods/' + name, '-f', '-',
                     data=canonical({'apiVersion': 'v1', 'kind': 'DeleteOptions', 'preconditions': {'uid': pod_uid}}))
            except Exception:
                cleanup_failed = True
        if secret_created:
            try:
                if not secret_identity:
                    raise RuntimeError('preload-original-secret-cleanup-unknown')
                kube('delete', '--raw', '/api/v1/namespaces/' + NS + '/secrets/' + name, '-f', '-',
                     data=canonical({'apiVersion': 'v1', 'kind': 'DeleteOptions',
                                     'preconditions': secret_identity}))
            except Exception:
                cleanup_failed = True
        if cleanup_failed:
            raise RuntimeError('preload-owned-resource-cleanup-incomplete')


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    instance.created = json.loads((instance.evidence / 'created-resource-references.json').read_text())
    try:
        preload(instance, sys.argv[2])
        print('exact isolated image preloaded; provisioning Pod and operator credential removed')
    finally:
        instance.close()
