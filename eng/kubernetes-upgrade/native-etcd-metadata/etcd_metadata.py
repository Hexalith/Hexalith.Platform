"""Fixed native read-only external-etcd diagnostics; never snapshot/archive/upload."""
import copy
import hashlib
import importlib.util
import inspect
import ipaddress
import json
from pathlib import Path
import re
import time

BASE = Path(__file__).resolve().parent.parent / 'native-node-metadata/metadata.py'
spec = importlib.util.spec_from_file_location('qualified_node_metadata_transport', BASE)
transport = importlib.util.module_from_spec(spec)
spec.loader.exec_module(transport)


def attributes_profile(metadata, source_root='/source'):
    """Inspect only selected members through confined descriptor parents."""
    import os
    import stat
    root = os.open(source_root, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    unsupported = []
    try:
        for name, entry in metadata['entries'].items():
            parts = name.split('/')
            directory = os.dup(root)
            try:
                for part in parts[:-1]:
                    child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
                    os.close(directory);directory = child
                before = os.stat(parts[-1], dir_fd=directory, follow_symlinks=False)
                attrs = os.listxattr('/proc/self/fd/' + str(directory) + '/' + parts[-1], follow_symlinks=False)
                after = os.stat(parts[-1], dir_fd=directory, follow_symlinks=False)
                if any(getattr(before, k) != getattr(after, k) for k in (
                        'st_dev','st_ino','st_size','st_ctime_ns','st_mtime_ns','st_mode','st_uid','st_gid','st_nlink')):
                    raise ValueError('etcd-archive-profile-path-drift')
                if attrs or (stat.S_ISREG(before.st_mode) and before.st_nlink != 1):
                    unsupported.append({'path':name,'xattrNames':sorted(attrs),
                                        'regularFileLinkCount':before.st_nlink if stat.S_ISREG(before.st_mode) else None})
            finally:os.close(directory)
    finally:os.close(root)
    return {'allSelectedMembersInspected':True,'memberCount':len(metadata['entries']),
            'noAclXattrOrHardlinkProfileSupported':not unsupported,'unsupported':unsupported}


def single_certificate_file(raw):
    import re
    return (len(raw)<=65536 and raw.count(b'-----BEGIN CERTIFICATE-----') == 1
            and re.fullmatch(rb'\s*-----BEGIN CERTIFICATE-----\r?\n[A-Za-z0-9+/=\r\n]+-----END CERTIFICATE-----\s*',raw) is not None)


def probe(endpoint, bindings, valid_through, paths, mounts):
    """Executed only in the exact read-only source Pod; outputs no credential bytes."""
    import base64
    import json
    import ssl
    import socket
    import re
    import subprocess
    import time
    from pathlib import Path
    before = collect_metadata('/source', paths, mounts, require_security_profile=True)
    if any(before['entries'].get(path) != expected for path, expected in bindings.items()):
        raise ValueError('etcd-source-file-binding-drift')
    archive_profile = attributes_profile(before)
    ca, cert, key = ('/source/etc/kubernetes/pki/etcd/' + name for name in ('ca.crt', 'client.crt', 'client.key'))
    tls = ssl.create_default_context(cafile=ca)
    tls.minimum_version = ssl.TLSVersion.TLSv1_2
    tls.load_cert_chain(cert, key)  # Actual CA parsing and client key/certificate pairing.
    certificates = {}
    decoded = {}
    for role, path in [('ca', ca), ('client', cert)]:
        with open(path,'rb') as stream:raw = stream.read(65537)
        if not single_certificate_file(raw):
            raise ValueError('etcd-single-pem-certificate-profile-refused')
        info = ssl._ssl._test_decode_cert(path)
        if ssl.cert_time_to_seconds(info['notBefore']) > time.time() or ssl.cert_time_to_seconds(info['notAfter']) <= valid_through:
            raise ValueError('etcd-tls-lifetime-insufficient')
        certificates[role] = {'notBefore': info['notBefore'], 'notAfter': info['notAfter']}
        decoded[role] = info
        if role == 'ca':ca_der = ssl.PEM_cert_to_DER_cert(raw.decode('ascii'))
    if decoded['ca']['issuer'] != decoded['ca']['subject'] or decoded['client']['issuer'] != decoded['ca']['subject']:
        raise ValueError('etcd-direct-self-signed-ca-profile-refused')
    host,port = endpoint.removeprefix('https://').rsplit(':',1)
    with socket.create_connection((host,int(port)),timeout=5) as connection:
        with tls.wrap_socket(connection,server_hostname=host) as secure:
            info = secure.getpeercert()
            chain = secure.get_verified_chain()
            if len(chain) != 2 or chain[-1] != ca_der or chain[0] != secure.getpeercert(binary_form=True):
                raise ValueError('etcd-direct-ca-verified-chain-profile-refused')
            if (info['issuer'] != decoded['ca']['subject'] or ssl.cert_time_to_seconds(info['notBefore'])>time.time()
                    or ssl.cert_time_to_seconds(info['notAfter'])<=valid_through):
                raise ValueError('etcd-server-certificate-profile-or-lifetime-refused')
            certificates['server'] = {'notBefore':info['notBefore'],'notAfter':info['notAfter'],
                                      'verifiedHostnameAndCa':True,'tlsVersion':secure.version(),
                                      'verifiedChainCertificateCount':len(chain)}
    command = ['/source/usr/local/bin/etcdctl', '--endpoints=' + endpoint,
               '--cacert=' + ca, '--cert=' + cert, '--key=' + key,
               '--dial-timeout=5s', '--command-timeout=15s']
    observations = {}
    for name, suffix in [('health', ['endpoint', 'health', '-w', 'json']),
                         ('status', ['endpoint', 'status', '-w', 'json']),
                         ('members', ['member', 'list', '-w', 'json']),
                         ('alarms', ['alarm', 'list', '-w', 'json'])]:
        result = bounded_command(command + suffix, timeout=20, maximum=65536)
        if result.returncode != 0:
            raise ValueError('etcd-readonly-rpc-refused')
        observations[name] = json.loads(result.stdout)
    if collect_metadata('/source', paths, mounts, require_security_profile=True) != before:
        raise ValueError('etcd-source-file-drift-during-probe')
    return {'kind': 'external-etcd-readonly-metadata', 'endpoint': endpoint,
            'observations': observations, 'certificates': certificates,
            'nodeMetadata':before,'archiveProfile':archive_profile,
            'sourceFileBindingsVerifiedBeforeAndAfter': True, 'networkCommandCount': 4,
            'grpcRequestCountMeasured':False,
            'additionalVerifiedTlsHandshakes':1,
            'certificateFileProfile':'one PEM CA and client; self-issued trusted CA directly issues client and server',
            'sourceContentExported': False, 'snapshotTaken': False, 'archiveCreated': False,
            'processSecurityProfile': before['processSecurityProfile']}


def program(endpoint, bindings, valid_through):
    # The sole supported production TLS layout is established by fresh native
    # apiserver args and the qualified node metadata, not guessed from history.
    if not re.fullmatch(r'https://(?:[0-9]{1,3}\.){3}[0-9]{1,3}:[0-9]{1,5}', endpoint):
        raise ValueError('etcd-exact-endpoint-invalid')
    host, port = endpoint.removeprefix('https://').rsplit(':', 1)
    ipaddress.IPv4Address(host)
    if not 1 <= int(port) <= 65535:
        raise ValueError('etcd-exact-endpoint-invalid')
    expected_paths = {'etc/kubernetes/pki/etcd/ca.crt', 'etc/kubernetes/pki/etcd/client.crt',
                      'etc/kubernetes/pki/etcd/client.key', 'usr/local/bin/etcdctl'}
    if set(bindings) != expected_paths or type(valid_through) is not int or valid_through <= 0:
        raise ValueError('etcd-byte-bindings-invalid')
    for entry in bindings.values():
        if (set(entry) != {'uid', 'gid', 'mode', 'kind', 'bytes', 'sha256'} or entry['kind'] != 'file'
                or not re.fullmatch('[a-f0-9]{64}', entry['sha256'])
                or any(type(entry[k]) is not int or entry[k] < 0 for k in ('uid', 'gid', 'mode'))
                or entry['mode'] > 0o7777
                or type(entry['bytes']) is not int or not 0 < entry['bytes'] <= 128*1024*1024):
            raise ValueError('etcd-byte-bindings-invalid')
    if any(bindings[path]['bytes']>65536 for path in expected_paths if path.startswith('etc/')):
        raise ValueError('etcd-tls-file-profile-bound')
    return ('import os,selectors,signal,subprocess,time\n' + inspect.getsource(transport.collect_metadata)
            + '\n' + inspect.getsource(transport.bounded_command) + '\n' + inspect.getsource(attributes_profile)
            + '\n' + inspect.getsource(single_certificate_file)
            + '\n' + inspect.getsource(probe)
            + '\nimport json\nprint(json.dumps(probe(' + repr(endpoint) + ', ' + repr(bindings)
            + ', ' + repr(valid_through) + ', ' + repr(transport.PATHS + ('root/.ssh',))
            + ', ' + repr(tuple(p for _,p,_ in transport.MOUNTS) + ('/root',)) + '), sort_keys=True))\n')


def manifests(attempt, node, node_uid, namespace_uid, image, endpoint, bindings, valid_through):
    policy, pod = transport.manifests(attempt, node, node_uid, namespace_uid, image)
    for obj in (policy, pod):
        obj['metadata'] = copy.deepcopy(obj['metadata'])
        obj['metadata']['name'] = 's427-etcd-meta-' + attempt
        obj['metadata']['labels']['app.kubernetes.io/name'] = 's427-etcd-metadata'
    body = program(endpoint, bindings, valid_through)
    pod['spec']['containers'][0]['command'][-1] = body
    for obj in (policy, pod):
        obj['metadata']['annotations']['hexalith.io/program-sha256'] = hashlib.sha256(body.encode()).hexdigest()
    # Extra default-deny remains additive; only the fixed four CLI commands and their
    # exact TLS endpoint are claimed, never a complete network-fence guarantee.
    host, port = endpoint.removeprefix('https://').rsplit(':', 1)
    policy['spec']['egress'] = [{'to': [{'ipBlock': {'cidr': host + '/32'}}],
                                'ports': [{'protocol': 'TCP', 'port': int(port)}]}]
    pod['spec']['volumes'].append({'name':'root-ssh-parent','hostPath':{'path':'/root','type':'Directory'}})
    pod['spec']['containers'][0]['volumeMounts'].append({
        'name':'root-ssh-parent','mountPath':'/source/root','readOnly':True})
    policy['spec']['podSelector']={'matchLabels':copy.deepcopy(pod['metadata']['labels'])}
    return policy, pod


def assert_owned(observed, desired, uid):
    if desired['kind'] != 'NetworkPolicy':
        return transport.assert_owned(observed, desired, uid)
    if (observed.get('metadata', {}).get('uid') != uid
            or any(observed['metadata'].get(k) != desired['metadata'][k]
                   for k in ('name', 'namespace', 'labels', 'annotations'))
            or observed['metadata'].get('ownerReferences') or observed['metadata'].get('finalizers')):
        raise ValueError('etcd-owned-policy-identity-drift')
    actual = copy.deepcopy(observed.get('spec', {}))
    actual.setdefault('ingress', [])
    if actual != desired['spec']:
        raise ValueError('etcd-owned-policy-scope-drift')


def validate_observation(value, endpoint):
    if (value.get('kind') != 'external-etcd-readonly-metadata' or value.get('endpoint') != endpoint
            or value.get('sourceFileBindingsVerifiedBeforeAndAfter') is not True
            or value.get('networkCommandCount') != 4 or value.get('sourceContentExported') is not False
            or value.get('snapshotTaken') is not False or value.get('archiveCreated') is not False
            or value.get('archiveProfile',{}).get('allSelectedMembersInspected') is not True
            or value.get('archiveProfile',{}).get('memberCount') != len(value.get('nodeMetadata',{}).get('entries',{}))
            or value.get('processSecurityProfile') != {'onlyDacReadSearchEffectivePermittedAndBounded': True,
                'noNewPrivileges': True, 'rootFilesystemReadOnly': True}):
        raise ValueError('etcd-readonly-result-refused')
    obs = value['observations']
    if len(obs['health']) != 1 or obs['health'][0].get('endpoint') != endpoint or obs['health'][0].get('health') is not True:
        raise ValueError('etcd-health-refused')
    if len(obs['status']) != 1 or obs['status'][0].get('Endpoint') != endpoint:
        raise ValueError('etcd-status-refused')
    status = obs['status'][0]['Status']
    members = obs['members']['members']
    ids = [status['header'].get('cluster_id'), status['header'].get('member_id'), status.get('leader')]
    ids += [v.get('ID') for v in members]
    ids += [obs[name]['header'].get('cluster_id') for name in ('members','alarms')]
    if any(type(value) is not int or not 0 < value < 2**64 for value in ids):
        raise ValueError('etcd-native-uint64-identity-invalid')
    if (status.get('version') != '3.6.5' or status.get('errors') or obs['alarms'].get('alarms')
            or len(members) != 1 or members[0].get('isLearner', False)
            or members[0]['ID'] != status['header']['member_id'] or status['leader'] != members[0]['ID']
            or members[0].get('clientURLs') != [endpoint]
            or type(status.get('dbSize')) is not int or status['dbSize'] <= 0
            or type(status['header'].get('revision')) is not int or status['header']['revision'] <= 0
            or any(obs[name]['header']['cluster_id'] != status['header']['cluster_id'] for name in ('members', 'alarms'))):
        raise ValueError('etcd-member-status-or-alarm-refused')
    return {'memberId': str(status['header']['member_id']), 'clusterId': str(status['header']['cluster_id']),
            'memberName': members[0]['name'], 'peerUrls': members[0]['peerURLs'], 'endpoint': endpoint,
            'version': status['version'], 'revision': status['header']['revision'], 'databaseBytes': status['dbSize'],
            'databaseBytesInUse': status.get('dbSizeInUse'), 'certificates': value['certificates'],
            'nodeArchiveMemberCount':value['archiveProfile']['memberCount'],
            'nodeArchiveProfileSupported':value['archiveProfile'].get('noAclXattrOrHardlinkProfileSupported') is True,
            'unsupportedArchiveMembers':value['archiveProfile'].get('unsupported')}


def collect(args, objects, attempt, endpoint):
    client = transport.Client(args, attempt)
    owned, cleanup, value, summary = [], [], None, None
    original_error, unchanged, source_verified = None, False, False
    before = None
    try:
        before = client.protected_state()
        for path, uid in [('/api/v1/nodes/' + args.node, args.node_uid),
                          ('/api/v1/namespaces/kube-system', args.namespace_uid)]:
            if client.run('get', path)['metadata']['uid'] != uid:
                raise ValueError('etcd-source-identity-drift')
        for obj in objects:
            path = transport.resource_path(obj)
            if client.run('get', path, absent=True) is not None:
                raise ValueError('etcd-helper-name-exists')
            entry = [obj, None]
            owned.append(entry)
            created = client.run('create', path.rsplit('/', 1)[0], obj)
            entry[1] = created['metadata']['uid']
            assert_owned(created, obj, entry[1])
        pod, uid = owned[-1]
        deadline = time.monotonic() + 180
        while True:
            current = client.run('get', transport.resource_path(pod))
            assert_owned(current, pod, uid)
            if current.get('status', {}).get('phase') == 'Succeeded':
                break
            if current.get('status', {}).get('phase') == 'Failed' or time.monotonic() >= deadline:
                try:client.run('get', transport.resource_path(pod) + '/log')
                except Exception:pass
                raise ValueError('etcd-helper-failed-or-timeout')
            time.sleep(2)
        value = client.run('get', transport.resource_path(pod) + '/log')
        summary = validate_observation(value, endpoint)
        unchanged = client.protected_state() == before
        if not unchanged:raise ValueError('etcd-protected-identity-or-storage-drift')
        for path, uid in [('/api/v1/nodes/' + args.node, args.node_uid),
                          ('/api/v1/namespaces/kube-system', args.namespace_uid)]:
            if client.run('get', path)['metadata']['uid'] != uid:raise ValueError('etcd-source-identity-drift')
        source_verified = True
    except Exception as exc:
        original_error = type(exc).__name__ + (':' + str(exc) if isinstance(exc, ValueError) else '')
    finally:
        for obj, uid in reversed(owned):
            try:
                path = transport.resource_path(obj);current = client.run('get', path, absent=True)
                if current is not None:
                    if uid is None:
                        cleanup.append({'kind':obj['kind'],'uid':None,'absenceVerified':False,'reason':'unverified-create-response'});continue
                    assert_owned(current, obj, uid)
                    client.run('delete', path, {'apiVersion':'v1','kind':'DeleteOptions','preconditions':{
                        'uid':uid,'resourceVersion':current['metadata']['resourceVersion']},'propagationPolicy':'Foreground'})
                    deadline = time.monotonic() + 60
                    while client.run('get', path, absent=True) is not None:
                        if time.monotonic() >= deadline:raise ValueError('etcd-cleanup-timeout')
                        time.sleep(1)
                cleanup.append({'kind':obj['kind'],'uid':uid,'absenceVerified':True})
            except Exception:
                cleanup.append({'kind':obj['kind'],'uid':uid,'absenceVerified':False})
        try:attempt.encrypt('etcd-metadata-private-result', transport.canonical({'value':value,'error':original_error}), args.age,args.recipient)
        except Exception:original_error = original_error or 'etcd-result-custody-refused'
    return {'kind':'external-etcd-readonly-helper-result','error':original_error,'metadataObserved':summary is not None,
            'summary':summary,'namespacePvcPvIdentitiesAndBindingsUnchanged':unchanged,
            'sourceNodeAndNamespaceUidRevalidated':source_verified,'cleanup':cleanup,
            'cleanupPassed':bool(owned) and len(cleanup)==len(owned) and all(v['absenceVerified'] for v in cleanup),
            'helperCreationIntents':len(owned),'helperObjectsCreated':sum(uid is not None for _,uid in owned),
            'helperCreateOutcomesUnverified':sum(uid is None for _,uid in owned),'requests':client.requests,
            'snapshotTaken':False,'archiveCreated':False,'sourceRecoveryAccepted':False,'upgradeGate':'closed'}
