#!/usr/bin/env python3
"""Preload an exact isolated image; remove the provisioning-only credential and Pod.

Applications use IfNotPresent without operator pull credentials. Image eviction is
an explicit future provisioning dependency, never permission to retain an operator session.
"""
import base64
import json
import sys

from deploy_native_registry import distribution_auth
from install_isolated_registry import Installer, NS, canonical, kube, kjson, sha


def preload(installer, image):
    assert image.startswith('registry.hexalith.com/ext-secrets-1-') and '@sha256:' in image
    suffix = sha(image.encode())[:12]
    name = 'ext-secrets-1-preload-' + suffix
    secret_created = False
    pod_created = False
    secret_identity = None
    pod_uid = None
    try:
        installer.create({'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': name}, 'type': 'kubernetes.io/dockerconfigjson',
                          'data': {'.dockerconfigjson': base64.b64encode(canonical({'auths': {'registry.hexalith.com': distribution_auth()}})).decode()}})
        secret_created = True
        secret_identity = kube('get', 'secret', name, '-n', NS, '-o',
                               'jsonpath={.metadata.uid}{" "}{.metadata.resourceVersion}').stdout.decode().split()
        if len(secret_identity) != 2:
            raise RuntimeError('preload-original-secret-identity-unavailable')
        installer.create({'apiVersion': 'v1', 'kind': 'Pod', 'metadata': {'name': name}, 'spec': {
            'serviceAccountName': 'ext-secrets-1-registry', 'automountServiceAccountToken': False, 'restartPolicy': 'Never',
            'imagePullSecrets': [{'name': name}], 'securityContext': {'runAsNonRoot': True, 'runAsUser': 1654, 'seccompProfile': {'type': 'RuntimeDefault'}},
            'containers': [{'name': 'preload', 'image': image, 'imagePullPolicy': 'IfNotPresent', 'command': ['/bin/true'],
                            'securityContext': {'allowPrivilegeEscalation': False, 'capabilities': {'drop': ['ALL']}}}]}})
        pod_created = True
        pod_uid = kube('get', 'pod', name, '-n', NS, '-o', 'jsonpath={.metadata.uid}').stdout.decode().strip()
        if not pod_uid:
            raise RuntimeError('preload-original-pod-identity-unavailable')
        kube('wait', 'pod/' + name, '-n', NS, '--for=jsonpath={.status.phase}=Succeeded', '--timeout=50s', timeout=55)
        pod = kjson('get', 'pod', name, '-n', NS)
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
                if not secret_identity or len(secret_identity) != 2:
                    raise RuntimeError('preload-original-secret-cleanup-unknown')
                kube('delete', '--raw', '/api/v1/namespaces/' + NS + '/secrets/' + name, '-f', '-',
                     data=canonical({'apiVersion': 'v1', 'kind': 'DeleteOptions',
                                     'preconditions': {'uid': secret_identity[0], 'resourceVersion': secret_identity[1]}}))
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
