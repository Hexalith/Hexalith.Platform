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
    try:
        installer.create({'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': name}, 'type': 'kubernetes.io/dockerconfigjson',
                          'data': {'.dockerconfigjson': base64.b64encode(canonical({'auths': {'registry.hexalith.com': distribution_auth()}})).decode()}})
        secret_created = True
        installer.create({'apiVersion': 'v1', 'kind': 'Pod', 'metadata': {'name': name}, 'spec': {
            'serviceAccountName': 'ext-secrets-1-registry', 'automountServiceAccountToken': False, 'restartPolicy': 'Never',
            'imagePullSecrets': [{'name': name}], 'securityContext': {'runAsNonRoot': True, 'runAsUser': 1654, 'seccompProfile': {'type': 'RuntimeDefault'}},
            'containers': [{'name': 'preload', 'image': image, 'imagePullPolicy': 'IfNotPresent', 'command': ['/bin/true'],
                            'securityContext': {'allowPrivilegeEscalation': False, 'capabilities': {'drop': ['ALL']}}}]}})
        pod_created = True
        kube('wait', 'pod/' + name, '-n', NS, '--for=jsonpath={.status.phase}=Succeeded', '--timeout=50s', timeout=55)
        pod = kjson('get', 'pod', name, '-n', NS)
        installer.write('preloaded-image-' + suffix + '.json', {'image': image, 'node': pod['spec']['nodeName'],
                         'nativeImageId': pod['status']['containerStatuses'][0]['imageID'], 'provisioningCredentialRetained': False,
                         'credentialMountedInProcess': False})
    finally:
        if pod_created:
            kube('delete', 'pod', name, '-n', NS, '--wait=false')
        if secret_created:
            kube('delete', 'secret', name, '-n', NS)


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    instance.created = json.loads((instance.evidence / 'created-resource-references.json').read_text())
    try:
        preload(instance, sys.argv[2])
        print('exact isolated image preloaded; provisioning Pod and operator credential removed')
    finally:
        instance.close()
