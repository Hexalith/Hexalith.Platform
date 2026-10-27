#!/usr/bin/env python3
"""Create-only native registry infrastructure. Credentials never leave memory/cluster Secrets.

This installs an isolated registry, not independent EXT-SECRETS-1 trust. It cannot
accept a profile, issue a decision, write a provider key, or run qualification.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
from datetime import datetime, timezone

KUBE = ['kubectl', '--kubeconfig', '/home/administrator/.kube/hexalith-production', '--request-timeout=30s']
NS = 'ext-secrets-1-isolated'
ROOT = Path(__file__).resolve().parents[2]
REGISTER = ROOT.parent / 'agents/_bmad-output/planning-artifacts/external-dependency-register.md'
BAO_CA = '/openbao/userconfig/openbao-server-tls/ca.crt'
BAO_NAME = 'hexalith-keys.openbao.svc.cluster.local'
IDP_HOST = 'registry-idp.ext-secrets-1-isolated.svc.cluster.local'
TLS_POLICY = 'ext-secrets-1-registry-pki'
TLS_ROLE = 'ext-secrets-1-registry'


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True).encode()


def creation_identity(output):
    fields = output.decode().split()
    if len(fields) != 2 or not all(fields):
        raise RuntimeError('original-create-response-identity-unavailable')
    return {'uid': fields[0], 'resourceVersion': fields[1]}


def sha(value):
    return hashlib.sha256(value).hexdigest()


def command(args, data=None, allowed=(0,), timeout=90):
    result = subprocess.run(args, input=data, capture_output=True, timeout=timeout)
    if result.returncode not in allowed:
        # Never forward raw provider/credential output, including subprocess exceptions.
        raise RuntimeError('command-failed:' + args[0] + ':' + str(result.returncode))
    return result


def kube(*args, data=None, allowed=(0,), timeout=90):
    return command(KUBE + list(args), data, allowed, timeout)


def kjson(*args):
    return json.loads(kube(*args, '-o', 'json').stdout)


def reference(value):
    metadata = value['metadata']
    return '/'.join((value['kind'], metadata.get('namespace', '_cluster'), metadata['name']))


def snapshot():
    kinds = ('namespaces,deployments.apps,statefulsets.apps,daemonsets.apps,services,configmaps,'
             'serviceaccounts,persistentvolumeclaims,persistentvolumes,networkpolicies.networking.k8s.io,'
             'roles.rbac.authorization.k8s.io,rolebindings.rbac.authorization.k8s.io,'
             'clusterroles.rbac.authorization.k8s.io,clusterrolebindings.rbac.authorization.k8s.io,'
             'components.dapr.io,configurations.dapr.io,resiliencies.dapr.io,subscriptions.dapr.io,'
             'clusters.postgresql.cnpg.io,issuers.cert-manager.io,clusterissuers.cert-manager.io,certificates.cert-manager.io')
    result = {}
    for value in kjson('get', kinds, '-A')['items']:
        metadata = value['metadata']
        material = {k: v for k, v in value.items() if k not in ('metadata', 'status', 'kind', 'apiVersion')}
        material['metadata'] = {k: metadata[k] for k in ('labels', 'annotations') if k in metadata}
        result[reference(value)] = {'uid': metadata['uid'], 'specSha256': sha(canonical(material))}
    for line in kube('get', 'secrets', '-A', '--no-headers', '-o',
                     'custom-columns=NS:.metadata.namespace,NAME:.metadata.name,UID:.metadata.uid,RV:.metadata.resourceVersion').stdout.decode().splitlines():
        namespace, name, uid, rv = line.split()
        result['Secret/' + namespace + '/' + name] = {'uid': uid, 'resourceVersion': rv}
    return result


class Installer:
    def __init__(self, evidence):
        self.evidence = Path(evidence)
        self.created = []
        self.token = bytearray(base64.b64decode(kube('get', 'secret', 'openbao-operator-credentials', '-n', 'openbao',
                                                    '-o', 'jsonpath={.data.operator-token}').stdout, validate=True))

    def close(self):
        self.token[:] = b'\0' * len(self.token)

    def write(self, name, value):
        (self.evidence / name).write_bytes(json.dumps(value, sort_keys=True, indent=2).encode() + b'\n')

    def bao(self, *args, data=None, allowed=(0,)):
        script = ('IFS= read -r BAO_TOKEN || exit 65; export BAO_TOKEN; '
                  'export BAO_ADDR=https://127.0.0.1:8200 BAO_CACERT=' + BAO_CA +
                  ' BAO_TLS_SERVER_NAME=' + BAO_NAME + ' BAO_SKIP_VERIFY=false; exec bao "$@"')
        return kube('exec', '-i', '-n', 'openbao', 'hexalith-keys-0', '--', 'sh', '-c', script, 'sh', *args,
                    data=bytes(self.token) + b'\n' + (data or b''), allowed=allowed)

    def openbao_snapshot(self):
        mounts = json.loads(self.bao('secrets', 'list', '-detailed', '-format=json').stdout)
        auth = json.loads(self.bao('auth', 'list', '-detailed', '-format=json').stdout)
        names = json.loads(self.bao('policy', 'list', '-format=json').stdout)
        policies = {name: 'builtin-root' if name == 'root' else sha(self.bao('policy', 'read', name).stdout) for name in names}
        return {'mounts': {name: sha(canonical(value)) for name, value in mounts.items()},
                'authMounts': {name: sha(canonical(value)) for name, value in auth.items()}, 'policies': policies}

    def create(self, obj):
        metadata = obj.setdefault('metadata', {})
        if obj['kind'] != 'Namespace':
            metadata.setdefault('namespace', NS)
        args = ['get', obj['kind'], metadata['name'], '--ignore-not-found', '-o', 'name']
        if 'namespace' in metadata:
            args += ['-n', metadata['namespace']]
        if kube(*args).stdout.strip():
            raise RuntimeError('create-name-already-exists:' + obj['kind'] + '/' + metadata['name'])
        # Even Secret creation uses name output only; full Secret objects are never read back.
        result = kube('create', '-f', '-', '-o', 'jsonpath={.metadata.uid}{" "}{.metadata.resourceVersion}', data=canonical(obj))
        identity = creation_identity(result.stdout)
        self.created.append(reference(obj))
        self.write('created-resource-references.json', self.created)
        return identity

    def secret(self, name, fields):
        return self.create({'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': name}, 'type': 'Opaque',
                     'data': {key: base64.b64encode(value.encode()).decode() for key, value in fields.items()}})

    def secret_field(self, name, field):
        return base64.b64decode(kube('get', 'secret', name, '-n', NS, '-o', 'jsonpath={.data.' + field + '}').stdout, validate=True).decode()

    def install_idp(self):
        self.create({'apiVersion': 'networking.k8s.io/v1', 'kind': 'NetworkPolicy', 'metadata': {'name': 'ext-secrets-1-registry-isolation'},
                     'spec': {'podSelector': {}, 'policyTypes': ['Ingress', 'Egress'],
                              'ingress': [{'from': [{'podSelector': {}}, {'namespaceSelector': {'matchLabels': {'kubernetes.io/metadata.name': 'dapr-system'}}}]}],
                              'egress': [{'to': [{'podSelector': {}}]},
                                         {'to': [{'namespaceSelector': {'matchLabels': {'kubernetes.io/metadata.name': 'dapr-system'}}}]},
                                         {'to': [{'namespaceSelector': {'matchLabels': {'kubernetes.io/metadata.name': 'kube-system'}}}], 'ports': [{'port': 53, 'protocol': 'UDP'}, {'port': 53, 'protocol': 'TCP'}]},
                                         {'to': [{'ipBlock': {'cidr': '169.254.25.10/32'}}], 'ports': [{'port': 53, 'protocol': 'UDP'}, {'port': 53, 'protocol': 'TCP'}]},
                                         {'to': [{'namespaceSelector': {'matchLabels': {'kubernetes.io/metadata.name': 'openbao'}}}], 'ports': [{'port': 8200, 'protocol': 'TCP'}]}]}})
        env = [env_secret('KC_DB_USERNAME', 'ext-secrets-1-registry-idp-db', 'username'),
               env_secret('KC_DB_PASSWORD', 'ext-secrets-1-registry-idp-db', 'password'),
               env_secret('KC_BOOTSTRAP_ADMIN_CLIENT_ID', 'ext-secrets-1-registry-idp-bootstrap', 'client-id'),
               env_secret('KC_BOOTSTRAP_ADMIN_CLIENT_SECRET', 'ext-secrets-1-registry-idp-bootstrap', 'client-secret')]
        env += [{'name': name, 'value': value} for name, value in {
            'KC_DB': 'postgres', 'KC_DB_URL': 'jdbc:postgresql://registry-idp-db:5432/registry_idp',
            'KC_HOSTNAME': 'https://' + IDP_HOST + ':8443', 'KC_HOSTNAME_STRICT': 'true', 'KC_HTTP_ENABLED': 'false',
            'KC_HTTPS_CERTIFICATE_FILE': '/var/run/registry-idp-tls/tls.crt', 'KC_HTTPS_CERTIFICATE_KEY_FILE': '/var/run/registry-idp-tls/tls.key',
            'KC_HEALTH_ENABLED': 'true', 'KC_METRICS_ENABLED': 'false', 'KC_LOG_LEVEL': 'warn',
            'KC_CACHE': 'local', 'JAVA_OPTS_KC_HEAP': '-Xms256m -Xmx512m'}.items()]
        self.deployment('registry-idp', 'quay.io/keycloak/keycloak@sha256:82a77884f3af238beab1e7afd63b5f530e1b5c0590bd7aa60b40a40463e29b2c',
                        8443, env, ['start'], 1000, 'ext-secrets-1-registry-idp',
                        [{'name': 'tls', 'secret': {'secretName': 'ext-secrets-1-registry-idp-tls', 'defaultMode': 288}}],
                        [{'name': 'tls', 'mountPath': '/var/run/registry-idp-tls', 'readOnly': True}],
                        resources={'requests': {'cpu': '200m', 'memory': '512Mi'}, 'limits': {'cpu': '2', 'memory': '1Gi'}})

    def deployment(self, name, image, port, env, args, uid, service_account, volumes=None, mounts=None, annotations=None, resources=None):
        self.create({'apiVersion': 'v1', 'kind': 'Service', 'metadata': {'name': name},
                     'spec': {'selector': {'app': name}, 'ports': [{'port': port, 'targetPort': port}]}})
        deployment = {'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': {'name': name},
                     'spec': {'replicas': 1, 'selector': {'matchLabels': {'app': name}},
                              'template': {'metadata': {'labels': {'app': name}, 'annotations': annotations or {}},
                               'spec': {'automountServiceAccountToken': False, 'serviceAccountName': service_account,
                                'securityContext': {'runAsNonRoot': True, 'runAsUser': uid, 'runAsGroup': uid, 'fsGroup': uid, 'seccompProfile': {'type': 'RuntimeDefault'}},
                                'containers': [{'name': name, 'image': image, 'imagePullPolicy': 'IfNotPresent', 'args': args, 'env': env, 'ports': [{'containerPort': port}],
                                  'securityContext': {'allowPrivilegeEscalation': False, 'capabilities': {'drop': ['ALL']}},
                                  'resources': resources or {'requests': {'cpu': '100m', 'memory': '128Mi'}, 'limits': {'cpu': '1', 'memory': '512Mi'}},
                                  'readinessProbe': ({'httpGet': {'path': '/ready' if name in ('eventstore', 'tenants') else '/alive', 'port': port}, 'initialDelaySeconds': 5, 'periodSeconds': 5, 'timeoutSeconds': 5} if annotations else {'tcpSocket': {'port': port}, 'initialDelaySeconds': 10, 'periodSeconds': 5}),
                                  'volumeMounts': [{'name': 'tmp', 'mountPath': '/tmp'}] + (mounts or [])}],
                                'volumes': [{'name': 'tmp', 'emptyDir': {}}] + (volumes or [])}}}}
        if annotations:
            self.restricted_sidecar(deployment, name)
        self.create(deployment)

    def restricted_sidecar(self, deployment, app):
        # The installed 1.18.1 injector omits drop=ALL. Its public control-plane
        # template is reused explicitly without changing global injector settings.
        # No existing application's credential values, Secret refs or SA token
        # values are copied: refs are replaced and projected tokens are newly issued.
        pod = next(value for value in kjson('get', 'pods', '-n', 'hexalith-memories')['items']
                   if value['metadata'].get('annotations', {}).get('dapr.io/app-id') == 'memories')
        sidecar = json.loads(json.dumps(next(value for value in pod['spec']['containers'] if value['name'] == 'daprd')))
        args = sidecar['args']
        annotations = deployment['spec']['template']['metadata']['annotations']
        for flag, value in [('--app-id', annotations['dapr.io/app-id']), ('--config', annotations['dapr.io/config']), ('--log-level', 'warn')]:
            args[args.index(flag) + 1] = value
        for env in sidecar['env']:
            if env['name'] == 'NAMESPACE':
                env['value'] = NS
            elif env['name'] in ('DAPR_API_TOKEN', 'APP_API_TOKEN'):
                assert 'value' not in env
                env['valueFrom']['secretKeyRef']['name'] = annotations['dapr.io/api-token-secret' if env['name'] == 'DAPR_API_TOKEN' else 'dapr.io/app-token-secret']
            else:
                assert 'secretKeyRef' not in env.get('valueFrom', {})
        sidecar.setdefault('securityContext', {})['capabilities'] = {'drop': ['ALL']}
        sidecar['resources'] = {'requests': {'cpu': '100m', 'memory': '64Mi'}, 'limits': {'cpu': '1', 'memory': '256Mi'}}
        wanted = {value['name'] for value in sidecar.get('volumeMounts', [])}
        selected = [value for value in pod['spec']['volumes'] if value['name'] in wanted]
        assert selected and all('projected' in value and all('secret' not in source for source in value['projected']['sources']) for value in selected)
        spec = deployment['spec']['template']['spec']
        spec['containers'] = [container for container in spec['containers'] if container['name'] != 'daprd'] + [sidecar]
        existing = {volume['name'] for volume in spec['volumes']}
        spec['volumes'] += [value for value in selected if value['name'] not in existing]

    def infrastructure(self):
        # Check original identities, rather than creating replacements for the four foundations.
        old = json.loads((Path('/home/administrator/hexalith-secrets-evidence/ext-secrets-1/provider-inventory-installation-20261017T072855253627Z') / 'installation-references.json').read_text())
        for kind, name in [('Namespace', NS), ('ServiceAccount', 'ext-secrets-1-custody'), ('ConfigMap', 'ext-secrets-1-openbao-ca')]:
            obj = kjson('get', kind, name, *([] if kind == 'Namespace' else ['-n', NS]))
            assert any(item.get('uid') == obj['metadata']['uid'] for item in old['installedResources'])
        mounts = json.loads(self.bao('secrets', 'list', '-detailed', '-format=json').stdout)
        assert mounts['ext-secrets-1-isolated/']['uuid'] == old['installedResources'][3]['uuid']
        self.write('preservation-before.json', snapshot())
        self.write('openbao-preservation-before.json', self.openbao_snapshot())
        self.write('settled-before.json', {'agentsRegisterSha256': sha(REGISTER.read_bytes()), 'foundationReferences': old['installedResources']})

        for name in ['ext-secrets-1-registry', 'ext-secrets-1-registry-idp', 'ext-secrets-1-registry-pki', 'ext-secrets-1-inventory']:
            self.create({'apiVersion': 'v1', 'kind': 'ServiceAccount', 'metadata': {'name': name}, 'automountServiceAccountToken': False})
        # A new role uses the existing PKI issuer. Existing roles, CA keys and issuer objects stay unchanged.
        role_names = json.loads(self.bao('list', '-format=json', 'pki-int/roles').stdout)
        policy_names = json.loads(self.bao('policy', 'list', '-format=json').stdout)
        auth_roles = json.loads(self.bao('list', '-format=json', 'auth/kubernetes/role').stdout)
        assert TLS_ROLE not in role_names and TLS_POLICY not in policy_names and TLS_POLICY not in auth_roles
        role = {'allowed_domains': [IDP_HOST], 'allow_bare_domains': True, 'allow_subdomains': False,
                'allow_localhost': False, 'allow_ip_sans': False, 'max_ttl': '720h', 'key_type': 'rsa', 'key_bits': 2048}
        self.bao('write', 'pki-int/roles/' + TLS_ROLE, '-', data=canonical(role))
        policy = 'path "pki-int/sign/' + TLS_ROLE + '" { capabilities = ["update"] }\n'
        self.bao('policy', 'write', TLS_POLICY, '-', data=policy.encode())
        self.bao('write', 'auth/kubernetes/role/' + TLS_POLICY, '-', data=canonical({
            'bound_service_account_names': ['ext-secrets-1-registry-pki'], 'bound_service_account_namespaces': [NS],
            'audience': 'vault://' + NS + '/ext-secrets-1-registry-pki', 'token_policies': [TLS_POLICY], 'token_ttl': '5m', 'token_max_ttl': '10m'}))
        self.create({'apiVersion': 'cert-manager.io/v1', 'kind': 'Issuer', 'metadata': {'name': 'ext-secrets-1-registry-pki'},
                     'spec': {'vault': {'server': 'https://' + BAO_NAME + ':8200', 'path': 'pki-int/sign/' + TLS_ROLE,
                              'caBundle': base64.b64encode(kube('exec', '-n', 'openbao', 'hexalith-keys-0', '--', 'cat', BAO_CA).stdout).decode(),
                              'auth': {'kubernetes': {'role': TLS_POLICY, 'mountPath': '/v1/auth/kubernetes',
                                       'serviceAccountRef': {'name': 'ext-secrets-1-registry-pki'}}}}}})
        self.create({'apiVersion': 'rbac.authorization.k8s.io/v1', 'kind': 'Role', 'metadata': {'name': 'ext-secrets-1-registry-pki-tokenrequest'},
                     'rules': [{'apiGroups': [''], 'resources': ['serviceaccounts/token'], 'resourceNames': ['ext-secrets-1-registry-pki'], 'verbs': ['create']}]})
        self.create({'apiVersion': 'rbac.authorization.k8s.io/v1', 'kind': 'RoleBinding', 'metadata': {'name': 'ext-secrets-1-registry-pki-tokenrequest'},
                     'roleRef': {'apiGroup': 'rbac.authorization.k8s.io', 'kind': 'Role', 'name': 'ext-secrets-1-registry-pki-tokenrequest'},
                     'subjects': [{'kind': 'ServiceAccount', 'name': 'cert-manager', 'namespace': 'cert-manager'}]})
        self.create({'apiVersion': 'cert-manager.io/v1', 'kind': 'Certificate', 'metadata': {'name': 'ext-secrets-1-registry-idp'},
                     'spec': {'secretName': 'ext-secrets-1-registry-idp-tls', 'duration': '720h', 'renewBefore': '168h',
                              'commonName': IDP_HOST, 'dnsNames': [IDP_HOST], 'issuerRef': {'name': 'ext-secrets-1-registry-pki', 'kind': 'Issuer'},
                              'privateKey': {'algorithm': 'RSA', 'size': 2048, 'rotationPolicy': 'Always'}}})
        self.secret('ext-secrets-1-registry-idp-db', {'username': 'registry_idp', 'password': secrets.token_urlsafe(36)})
        self.secret('ext-secrets-1-registry-idp-bootstrap', {'client-id': 'ext-secrets-1-registry-bootstrap', 'client-secret': secrets.token_urlsafe(48)})
        self.secret('ext-secrets-1-registry-channels', {name: secrets.token_urlsafe(48) for name in ['eventstore-app', 'eventstore-dapr', 'tenants-app', 'tenants-dapr', 'inventory-app', 'inventory-dapr']})
        redis_fields = {name: secrets.token_urlsafe(36) for name in ['eventstore', 'tenants', 'inventory']}
        self.secret('ext-secrets-1-registry-state-credentials', redis_fields)
        from registry_redis import native_acl, INTERNAL_REPLAY_ACL
        acl = INTERNAL_REPLAY_ACL + '\n' + '\n'.join(native_acl(name, password) for name, password in redis_fields.items()) + '\n'
        # Redis ACL/passwords live only in the intended Secret, mounted read-only by Redis.
        self.secret('ext-secrets-1-registry-redis-acl', {'users.acl': acl})
        self.create({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'ext-secrets-1-registry-redis'},
                     'data': {'redis.conf': 'bind 0.0.0.0\nprotected-mode yes\nport 6379\nappendonly yes\nappendfsync always\ndir /data\naclfile /etc/redis-acl/users.acl\n'}})
        postgres_image = 'docker.io/library/postgres:18.6-trixie@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722'
        redis_image = 'redis/redis-stack-server:7.4.0-v8@sha256:798ab84d9f266936b034ab11c4d04a2b8e4b441884c5aa7d17ac951eefdf742a'
        self.stateful('registry-idp-db', postgres_image, 5432, 999, [
            env_secret('POSTGRES_USER', 'ext-secrets-1-registry-idp-db', 'username'),
            env_secret('POSTGRES_PASSWORD', 'ext-secrets-1-registry-idp-db', 'password'),
            {'name': 'POSTGRES_DB', 'value': 'registry_idp'}, {'name': 'PGDATA', 'value': '/var/lib/postgresql/data/pgdata'}],
            '/var/lib/postgresql/data', ['/usr/local/bin/docker-entrypoint.sh', 'postgres', '-c', 'log_statement=none', '-c', 'log_min_error_statement=panic'])
        self.stateful('registry-state', redis_image, 6379, 1000, [], '/data', ['redis-server', '/etc/redis-config/redis.conf'],
                      [{'name': 'config', 'configMap': {'name': 'ext-secrets-1-registry-redis'}}, {'name': 'acl', 'secret': {'secretName': 'ext-secrets-1-registry-redis-acl'}}],
                      [{'name': 'config', 'mountPath': '/etc/redis-config', 'readOnly': True}, {'name': 'acl', 'mountPath': '/etc/redis-acl', 'readOnly': True}])

    def stateful(self, name, image, port, uid, env, storage_path, command_args, volumes=None, mounts=None):
        self.create({'apiVersion': 'v1', 'kind': 'Service', 'metadata': {'name': name}, 'spec': {'selector': {'app': name}, 'ports': [{'port': port, 'targetPort': port}]}})
        container = {'name': name, 'image': image, 'command': command_args, 'env': env, 'ports': [{'containerPort': port}],
                     'securityContext': {'allowPrivilegeEscalation': False, 'capabilities': {'drop': ['ALL']}},
                     'volumeMounts': [{'name': 'data', 'mountPath': storage_path}, {'name': 'tmp', 'mountPath': '/tmp'}, {'name': 'run', 'mountPath': '/var/run/postgresql'}] + (mounts or []),
                     'resources': {'requests': {'cpu': '100m', 'memory': '128Mi'}, 'limits': {'cpu': '1', 'memory': '1Gi'}},
                     'readinessProbe': {'tcpSocket': {'port': port}, 'initialDelaySeconds': 5, 'periodSeconds': 5}}
        self.create({'apiVersion': 'apps/v1', 'kind': 'StatefulSet', 'metadata': {'name': name},
                     'spec': {'serviceName': name, 'replicas': 1, 'selector': {'matchLabels': {'app': name}},
                              'template': {'metadata': {'labels': {'app': name}}, 'spec': {'serviceAccountName': 'ext-secrets-1-registry',
                                'automountServiceAccountToken': False,
                                'securityContext': {'runAsNonRoot': True, 'runAsUser': uid, 'runAsGroup': uid, 'fsGroup': uid, 'seccompProfile': {'type': 'RuntimeDefault'}},
                                'containers': [container], 'volumes': [{'name': 'tmp', 'emptyDir': {}}, {'name': 'run', 'emptyDir': {}}] + (volumes or [])}},
                              'volumeClaimTemplates': [{'metadata': {'name': 'data'}, 'spec': {'accessModes': ['ReadWriteOnce'], 'storageClassName': 'openebs-hostpath-retain', 'resources': {'requests': {'storage': '2Gi'}}}}]}})


def env_secret(name, secret, key):
    return {'name': name, 'valueFrom': {'secretKeyRef': {'name': secret, 'key': key}}}


def main():
    os.umask(0o077)
    evidence = Path('/home/administrator/hexalith-secrets-evidence/ext-secrets-1') / ('native-registry-inventory-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'))
    evidence.mkdir(exist_ok=False)
    print('evidence=' + str(evidence), flush=True)
    installer = Installer(evidence)
    try:
        installer.infrastructure()
        installer.write('infrastructure-installation.json', {'status': 'in-progress', 'qualificationPassed': False, 'dependencyAvailable': False,
                        'contractOperations': 0, 'providerKeyWrites': 0, 'independentSecretsTrustEstablished': False,
                        'createdReferences': installer.created})
    except Exception as error:
        installer.write('installation-failure.json', {'exceptionType': type(error).__name__, 'reason': str(error) if isinstance(error, (RuntimeError, AssertionError)) else 'sanitized-installation-failure'})
        print('installation-incomplete; see sanitized evidence', flush=True)
        return 1
    finally:
        installer.close()
    return 0


if __name__ == '__main__':
    sys.exit(main())
