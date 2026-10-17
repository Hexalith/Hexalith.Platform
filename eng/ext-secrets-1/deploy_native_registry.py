#!/usr/bin/env python3
"""Install native Tenants/EventStore hosts with isolated persistence and credentials."""
import json
import base64
from pathlib import Path
import subprocess
import sys

from install_isolated_registry import Installer, NS, ROOT, canonical, env_secret, kube, kjson, sha
from provision_registry_identity import ISSUER


def install(installer):
    # Images must be preloaded during provisioning. No operator distribution
    # credential is retained in application imagePullSecrets.
    for app in ['eventstore', 'tenants']:
        for channel in ['app', 'dapr']:
            installer.secret('ext-secrets-1-' + app + '-' + channel + '-token', {
                'token': installer.secret_field('ext-secrets-1-registry-channels', app + '-' + channel)})
    from registry_redis import components
    for app in ['eventstore', 'tenants']:
        for component in components(app):
            installer.create(component)
    for app, caller, operations in [('tenants', 'eventstore', ['/process', '/project', '/query', '/replay-state', '/admin/operational-index-metadata']),
                                     ('eventstore', 'ext-secrets-1-inventory', ['/api/v1/streams/read'])]:
        installer.create({'apiVersion': 'dapr.io/v1alpha1', 'kind': 'Configuration', 'metadata': {'name': 'ext-secrets-1-' + app},
                          'spec': {'accessControl': {'defaultAction': 'deny', 'trustDomain': 'cluster.local', 'policies': [
                              {'appId': caller, 'namespace': NS, 'trustDomain': 'cluster.local', 'defaultAction': 'deny',
                               'operations': [{'name': operation, 'httpVerb': ['POST'], 'action': 'allow'} for operation in operations]}]}}})
    images = {}
    for app in ['eventstore', 'tenants']:
        tagged = 'registry.hexalith.com/ext-secrets-1-' + app + (':20261017-native-registry-v3' if app == 'eventstore' else ':20261017-native-registry')
        image = json.loads(subprocess.check_output(['docker', 'image', 'inspect', tagged]))[0]
        digest = next(name for name in image['RepoDigests'] if name.startswith('registry.hexalith.com/ext-secrets-1-' + app + '@'))
        images[app] = digest
        env = [{'name': name, 'value': value} for name, value in {
            'ASPNETCORE_ENVIRONMENT': 'Production', 'ASPNETCORE_HTTP_PORTS': '8080',
            'Authentication__JwtBearer__Authority': ISSUER, 'Authentication__JwtBearer__Issuer': ISSUER,
            'Authentication__JwtBearer__Audience': 'hexalith-eventstore', 'Authentication__JwtBearer__AllowedAlgorithms__0': 'RS256',
            'Authentication__JwtBearer__RequireHttpsMetadata': 'true', 'SSL_CERT_FILE': '/var/run/registry-idp-ca/ca.pem',
            'Authentication__WorkloadIssuer__Workload': app, 'Authentication__WorkloadIssuer__ClientId': app,
            'Authentication__Workload__Audience': app, 'Authentication__Workload__AllowedCallers__0': 'eventstore',
            'Authentication__DaprInternal__AllowedCallers__0': 'tenants', 'EventStore__DomainService__AppId': app,
            'Tenants__BootstrapGlobalAdminEnabled': 'false', 'Logging__LogLevel__Default': 'Warning',
            'EventStore__OpenApi__Enabled': 'false'}.items()]
        if app == 'eventstore':
            env += [{'name': name, 'value': value} for name, value in eventstore_component_environment().items()]
        env += [env_secret('APP_API_TOKEN', 'ext-secrets-1-' + app + '-app-token', 'token'),
                env_secret('DAPR_API_TOKEN', 'ext-secrets-1-' + app + '-dapr-token', 'token'),
                env_secret('Authentication__WorkloadIssuer__ClientSecret', 'ext-secrets-1-registry-oidc-' + app, 'client-secret')]
        annotations = {'dapr.io/enabled': 'true', 'dapr.io/app-id': app, 'dapr.io/app-port': '8080',
            'dapr.io/app-token-secret': 'ext-secrets-1-' + app + '-app-token', 'dapr.io/api-token-secret': 'ext-secrets-1-' + app + '-dapr-token',
            'dapr.io/config': 'ext-secrets-1-' + app, 'dapr.io/sidecar-cpu-request': '100m', 'dapr.io/sidecar-cpu-limit': '1',
            'dapr.io/sidecar-memory-request': '64Mi', 'dapr.io/sidecar-memory-limit': '256Mi', 'dapr.io/sidecar-run-as-non-root': 'true'}
        installer.deployment(app, digest, 8080, env, [], 1654, 'ext-secrets-1-registry',
             [{'name': 'idp-ca', 'configMap': {'name': 'ext-secrets-1-registry-idp-ca'}}],
             [{'name': 'idp-ca', 'mountPath': '/var/run/registry-idp-ca', 'readOnly': True}], annotations)
    record_provenance(installer)


def eventstore_component_environment():
    return {'EventStore__CommandStatus__StateStoreName': 'eventstore-statestore',
            'EventStore__Projections__CheckpointStateStoreName': 'eventstore-statestore',
            'EventStore__Projections__ReadModelStateStoreName': 'eventstore-statestore',
            'EventStore__Publisher__PubSubName': 'eventstore-pubsub',
            'EventStore__ProjectionChanges__PubSubName': 'eventstore-pubsub',
            'EventStore__RateLimiting__ConfigurationStoreName': 'eventstore-configstore'}


def record_provenance(installer):
    """Record exact deployed images and reviewable composed source, including the probe repair."""
    tenants = Path('/home/administrator/projects/hexalith/tenants')
    native_host = tenants / 'references/Hexalith.EventStore/src/Hexalith.EventStore/Program.cs'
    deployments = kjson('get', 'deployments', '-n', NS)['items']
    images = {deployment['metadata']['name']: deployment['spec']['template']['spec']['containers'][0]['image']
              for deployment in deployments if deployment['metadata']['name'] in ['eventstore', 'tenants', 'inventory']}
    original = installer.evidence / 'native-host-provenance.json'
    if original.exists() and not (installer.evidence / 'native-host-initial-provenance.json').exists():
        (installer.evidence / 'native-host-initial-provenance.json').write_bytes(original.read_bytes())
    initial_path = installer.evidence / 'native-host-initial-provenance.json'
    initial = json.loads(initial_path.read_text()) if initial_path.exists() else {}
    composed = ROOT / 'src/Hexalith.Platform.TenantRegistry.Host'
    source_paths = [path for directory in ['src/Hexalith.Platform.TenantRegistry.Host', 'src/Hexalith.Platform.Secrets.Inventory']
                    for path in (ROOT / directory).rglob('*') if path.is_file() and path.suffix in ['.cs', '.csproj', '.props', '.json']
                    and not any(part in ('bin', 'obj') for part in path.parts)]
    installer.write('native-host-provenance.json', {'tenantsBuildCheckoutCommit': initial.get('tenantsSourceCommit'),
        'tenantsSourceCommitObserved': subprocess.check_output(['git', '-C', str(tenants), 'rev-parse', 'HEAD']).decode().strip(),
        'tenantsHostSha256': sha((tenants / 'src/Hexalith.Tenants/Program.cs').read_bytes()),
        'tenantsAggregateSha256': sha((tenants / 'src/Hexalith.Tenants.Server/Aggregates/TenantAggregate.cs').read_bytes()),
        'upstreamEventstoreSourceCommitObserved': subprocess.check_output(['git', '-C', str(tenants / 'references/Hexalith.EventStore'), 'rev-parse', 'HEAD']).decode().strip(),
        'upstreamEventstoreHostSha256': sha(native_host.read_bytes()),
        'composedEventstoreHostSource': str(composed / 'Program.cs'), 'composedEventstoreHostSha256': sha((composed / 'Program.cs').read_bytes()),
        'composedHostChange': 'The native placement-health HttpClient supplies its own DAPR_API_TOKEN; native health checks use the separately scoped EventStore component names. Native domain and command code is unchanged.',
        'reviewableSourceSha256': {str(path.relative_to(ROOT)): sha(path.read_bytes()) for path in source_paths},
        'eventstorePackageVersion': '3.124.0', 'eventstorePackageRepositoryCommit': '0737c0befce5338e294a789092a0e223e8264a04',
        'images': images, 'nativeDomainCodeChanged': False, 'stateStore': NS + '/StatefulSet/registry-state',
        'buildInstructions': str(ROOT / 'eng/ext-secrets-1/README.md') + '#isolated-native-registry-and-metadata-inventory',
        'independentSecretsTrustEstablished': False})


def distribution_auth():
    config = json.loads((Path.home() / '.docker/config.json').read_text())
    docker_auth = config['auths']['registry.hexalith.com']
    helper = config.get('credHelpers', {}).get('registry.hexalith.com') or config.get('credsStore')
    if not docker_auth.get('auth') and helper:
        result = subprocess.run(['docker-credential-' + helper, 'get'], input=b'registry.hexalith.com\n', capture_output=True, timeout=15)
        if result.returncode:
            raise RuntimeError('distribution-credential-helper-unavailable')
        credential = json.loads(result.stdout)
        docker_auth = {'auth': base64.b64encode((credential['Username'] + ':' + credential['Secret']).encode()).decode()}
    if not docker_auth.get('auth'):
        raise RuntimeError('distribution-pull-authentication-unavailable')
    return docker_auth


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    instance.created = json.loads((instance.evidence / 'created-resource-references.json').read_text())
    try:
        install(instance)
        print('native registry hosts installed with isolated credential references')
    except Exception as error:
        instance.write('native-host-installation-failure.json', {'exceptionType': type(error).__name__, 'reason': str(error) if isinstance(error, RuntimeError) else 'sanitized-installation-failure'})
        raise SystemExit(1) from None
    finally:
        instance.close()
