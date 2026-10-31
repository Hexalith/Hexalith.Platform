#!/usr/bin/env python3
"""Fixed metadata-only native helper; never archives, snapshots or uploads source bytes.

The helper has no network operations. Its NetworkPolicy is an additional scoped
restriction, not proof of a complete network fence or a recovery qualification.
"""
import argparse
import hashlib
import inspect
import ipaddress
import json
import os
from pathlib import Path
import re
import selectors
import signal
import stat
import subprocess
import sys
import time
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'eng/cluster-management'))
from evidence import Attempt, canonical, file_digest, write_new

MOUNTS = (
    ('etc', '/etc', 'Directory'),
    ('usr-local-bin', '/usr/local/bin', 'Directory'),
    ('usr-bin', '/usr/bin', 'Directory'),
    ('usr-local-sbin', '/usr/local/sbin', 'Directory'),
    ('usr-lib', '/usr/lib', 'Directory'),
    ('kubelet-config', '/var/lib/kubelet/config.yaml', 'File'),
    ('kubelet-flags', '/var/lib/kubelet/kubeadm-flags.env', 'File'),
    ('kubelet-instance', '/var/lib/kubelet/instance-config.yaml', 'File'),
    ('kubelet-pki', '/var/lib/kubelet/pki', 'Directory'),
    ('package-status', '/var/lib/dpkg/status', 'File'),
)
PATHS = (
    'etc/kubernetes', 'etc/containerd', 'etc/cni/net.d', 'etc/etcd.env',
    'etc/systemd/system/kubelet.service', 'etc/systemd/system/kubelet.service.d',
    'etc/systemd/system/containerd.service', 'etc/systemd/system/containerd.service.d',
    'etc/systemd/system/etcd.service', 'etc/systemd/system/etcd.service.d',
    'etc/default/kubelet', 'etc/ssl/etcd/ssl', 'etc/apt', 'etc/os-release',
    'etc/passwd', 'etc/group', 'etc/ssh', 'usr/lib/os-release',
    'var/lib/kubelet/config.yaml', 'var/lib/kubelet/kubeadm-flags.env',
    'var/lib/kubelet/instance-config.yaml', 'var/lib/kubelet/pki', 'var/lib/dpkg/status',
    'usr/local/bin/kubeadm', 'usr/local/bin/kubelet', 'usr/bin/kubelet',
    'usr/local/bin/kubectl', 'usr/local/bin/etcd', 'usr/local/bin/etcdctl',
    'usr/local/bin/etcdutl', 'usr/local/bin/containerd',
    'usr/local/bin/containerd-shim-runc-v2', 'usr/local/sbin/runc',
    'usr/local/bin/runc', 'usr/bin/runc', 'usr/local/bin/crictl', 'usr/local/bin/ctr',
)


def collect_metadata(root, paths, mount_paths, require_readonly=True):
    """Hash only fixed paths without following links or retaining file contents."""
    import hashlib
    import json
    import os
    import stat
    import time
    from pathlib import Path
    deadline = time.monotonic() + 90
    root = Path(root)
    for path in mount_paths:
        if require_readonly and not os.statvfs(root / path.lstrip('/')).f_flag & os.ST_RDONLY:
            raise ValueError('source-mount-not-readonly')
    entries, absent, total = {}, [], 0

    def visit(parent, name, rel, depth=0):
        nonlocal total
        if time.monotonic() >= deadline or depth > 24 or len(entries) >= 8192:
            raise ValueError('metadata-bound')
        before = os.stat(name, dir_fd=parent, follow_symlinks=False)
        item = {'uid': before.st_uid, 'gid': before.st_gid,
                'mode': stat.S_IMODE(before.st_mode)}
        if stat.S_ISREG(before.st_mode):
            if before.st_size > 128 * 1024 * 1024:
                raise ValueError('metadata-file-bound')
            fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=parent)
            try:
                opened = os.fstat(fd)
                if (before.st_dev, before.st_ino) != (opened.st_dev, opened.st_ino):
                    raise ValueError('metadata-file-replaced')
                digest, count = hashlib.sha256(), 0
                while True:
                    if time.monotonic() >= deadline:
                        raise ValueError('metadata-deadline')
                    chunk = os.read(fd, 1048576)
                    if not chunk:
                        break
                    count += len(chunk)
                    total += len(chunk)
                    if count > 128 * 1024 * 1024 or total > 512 * 1024 * 1024:
                        raise ValueError('metadata-total-bound')
                    digest.update(chunk)
                end = os.fstat(fd)
            finally:
                os.close(fd)
            fields = ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns', 'st_mode', 'st_uid', 'st_gid')
            if count != before.st_size or any(getattr(before, k) != getattr(end, k) for k in fields):
                raise ValueError('metadata-file-drift')
            item.update(kind='file', bytes=count, sha256=digest.hexdigest())
        elif stat.S_ISDIR(before.st_mode):
            item['kind'] = 'directory'
        elif stat.S_ISLNK(before.st_mode):
            item.update(kind='symlink', target=os.readlink(name, dir_fd=parent))
        else:
            raise ValueError('metadata-special-file-refused')
        entries[rel] = item
        if item['kind'] == 'directory':
            directory = os.open(name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent)
            try:
                opened = os.fstat(directory)
                if (before.st_dev, before.st_ino) != (opened.st_dev, opened.st_ino):
                    raise ValueError('metadata-directory-replaced')
                for child in sorted(os.listdir(directory)):
                    visit(directory, child, rel + '/' + child, depth + 1)
                after = os.fstat(directory)
                if (before.st_dev, before.st_ino, before.st_mtime_ns, before.st_ctime_ns) != (
                        after.st_dev, after.st_ino, after.st_mtime_ns, after.st_ctime_ns):
                    raise ValueError('metadata-directory-drift')
            finally:
                os.close(directory)
        final = os.stat(name, dir_fd=parent, follow_symlinks=False)
        if any(getattr(before, k) != getattr(final, k) for k in
               ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns', 'st_mode', 'st_uid', 'st_gid')):
            raise ValueError('metadata-path-drift')

    root_fd = os.open(root, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        for rel in paths:
            if rel.startswith('/') or '..' in Path(rel).parts or not Path(rel).parts:
                raise ValueError('metadata-path-refused')
            directory = os.dup(root_fd)
            try:
                parts = Path(rel).parts
                try:
                    for part in parts[:-1]:
                        child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
                        os.close(directory)
                        directory = child
                except FileNotFoundError:
                    absent.append(rel)
                    continue
                try:
                    os.stat(parts[-1], dir_fd=directory, follow_symlinks=False)
                except FileNotFoundError:
                    absent.append(rel)
                    continue
                try:
                    visit(directory, parts[-1], rel)
                except FileNotFoundError:
                    raise ValueError('metadata-path-disappeared') from None
            finally:
                os.close(directory)
    finally:
        os.close(root_fd)
    result = {'kind': 'node-file-metadata-only', 'entries': entries, 'absentOptionalPaths': absent,
              'hashedBytes': total, 'sourceContentExported': False, 'networkOperations': 0,
              'bindMountedFileMetadataIsNotHostSymlinkProof': True}
    if len(json.dumps(result).encode()) > 4 * 1024 * 1024:
        raise ValueError('metadata-response-bound')
    return result


def program():
    return (inspect.getsource(collect_metadata) + '\nimport json\nprint(json.dumps(collect_metadata('
            + repr('/source') + ', ' + repr(PATHS) + ', '
            + repr(tuple(p for _, p, _ in MOUNTS)) + '), sort_keys=True))\n')


def manifests(attempt, node, node_uid, namespace_uid, image):
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]{0,35}', attempt):
        raise ValueError('metadata-attempt-invalid')
    if not re.fullmatch(r'[a-z0-9][a-z0-9.-]{0,252}', node):
        raise ValueError('metadata-node-invalid')
    if not all(re.fullmatch(r'[a-f0-9-]{36}', v) for v in (node_uid, namespace_uid)):
        raise ValueError('metadata-source-uid-invalid')
    if not re.fullmatch(r'(?:docker.io/)?library/python@sha256:[a-f0-9]{64}', image):
        raise ValueError('metadata-image-pin-invalid')
    name = 's427-node-meta-' + attempt
    labels = {'app.kubernetes.io/name': 's427-node-metadata', 'hexalith.io/metadata-attempt': attempt}
    annotations = {'hexalith.io/source-node-uid': node_uid, 'hexalith.io/source-namespace-uid': namespace_uid,
                   'hexalith.io/program-sha256': hashlib.sha256(program().encode()).hexdigest()}
    metadata = {'name': name, 'namespace': 'kube-system', 'labels': labels, 'annotations': annotations}
    policy = {'apiVersion': 'networking.k8s.io/v1', 'kind': 'NetworkPolicy', 'metadata': metadata,
              'spec': {'podSelector': {'matchLabels': labels}, 'policyTypes': ['Ingress', 'Egress'],
                       'ingress': [], 'egress': []}}
    pod = {'apiVersion': 'v1', 'kind': 'Pod', 'metadata': metadata,
           'spec': {'nodeName': node, 'restartPolicy': 'Never', 'activeDeadlineSeconds': 150,
                    'terminationGracePeriodSeconds': 5, 'automountServiceAccountToken': False,
                    'hostNetwork': False, 'hostPID': False, 'hostIPC': False,
                    'enableServiceLinks': False,
                    'securityContext': {'runAsUser': 0, 'runAsGroup': 0,
                                        'seccompProfile': {'type': 'RuntimeDefault'}},
                    'containers': [{'name': 'metadata', 'image': image, 'imagePullPolicy': 'IfNotPresent',
                                    'command': ['/usr/local/bin/python3', '-I', '-S', '-B', '-c', program()],
                                    'securityContext': {'privileged': False, 'readOnlyRootFilesystem': True,
                                                        'allowPrivilegeEscalation': False,
                                                        'capabilities': {'drop': ['ALL']}},
                                    'resources': {'requests': {'cpu': '25m', 'memory': '64Mi'},
                                                  'limits': {'cpu': '500m', 'memory': '256Mi'}},
                                    'volumeMounts': [{'name': name, 'mountPath': '/source' + path, 'readOnly': True}
                                                     for name, path, _ in MOUNTS]}],
                    'volumes': [{'name': name, 'hostPath': {'path': path, 'type': kind}}
                                for name, path, kind in MOUNTS]}}
    return policy, pod


def assert_owned(observed, desired, uid):
    if observed.get('metadata', {}).get('uid') != uid:
        raise ValueError('metadata-owned-uid-drift')
    for field in ('name', 'namespace', 'labels'):
        if observed['metadata'].get(field) != desired['metadata'][field]:
            raise ValueError('metadata-owned-shape-drift')
    annotations = dict(observed['metadata'].get('annotations', {}))
    if desired['kind'] == 'Pod':
        for key in ('cni.projectcalico.org/containerID', 'cni.projectcalico.org/podIP', 'cni.projectcalico.org/podIPs'):
            if key not in annotations:
                continue
            value = annotations.pop(key)
            try:
                if key.endswith('/containerID'):
                    if not re.fullmatch('[a-f0-9]{64}', value):
                        raise ValueError()
                else:
                    if not value or any(ipaddress.ip_interface(v).network.prefixlen not in (32, 128)
                                        for v in value.split(',')):
                        raise ValueError()
            except (ValueError, TypeError, AttributeError):
                raise ValueError('metadata-cni-status-annotation-invalid') from None
    if annotations != desired['metadata']['annotations']:
        raise ValueError('metadata-owned-shape-drift')
    if observed['metadata'].get('ownerReferences') or observed['metadata'].get('finalizers'):
        raise ValueError('metadata-owner-or-finalizer-unapproved')
    spec = observed.get('spec', {})
    if desired['kind'] == 'NetworkPolicy':
        if (set(spec) - {'podSelector', 'policyTypes', 'ingress', 'egress'}
                or spec.get('podSelector') != desired['spec']['podSelector']
                or spec.get('policyTypes') != desired['spec']['policyTypes']
                or spec.get('ingress', []) != [] or spec.get('egress', []) != []):
            raise ValueError('metadata-network-policy-drift')
    else:
        defaults = {'serviceAccountName': 'default', 'serviceAccount': 'default', 'dnsPolicy': 'ClusterFirst',
                    'schedulerName': 'default-scheduler', 'priority': 0, 'preemptionPolicy': 'PreemptLowerPriority'}
        if set(spec) - set(desired['spec']) - set(defaults) - {'tolerations'}:
            raise ValueError('metadata-unreviewed-pod-field')
        if any(spec[key] != value for key, value in defaults.items() if key in spec):
            raise ValueError('metadata-pod-default-drift')
        tolerations = spec.get('tolerations', [])
        if (not isinstance(tolerations, list) or len(tolerations) > 2
                or len({v.get('key') for v in tolerations}) != len(tolerations)
                or any(v not in [{'key': key, 'operator': 'Exists', 'effect': 'NoExecute', 'tolerationSeconds': 300}
                                 for key in ('node.kubernetes.io/not-ready', 'node.kubernetes.io/unreachable')]
                       for v in tolerations)):
            raise ValueError('metadata-pod-toleration-drift')
        for field in ('nodeName', 'restartPolicy', 'activeDeadlineSeconds', 'terminationGracePeriodSeconds',
                      'automountServiceAccountToken', 'enableServiceLinks', 'volumes'):
            if spec.get(field) != desired['spec'][field]:
                raise ValueError('metadata-pod-spec-drift')
        if any(spec.get(field, False) for field in ('hostNetwork', 'hostPID', 'hostIPC')):
            raise ValueError('metadata-host-namespace-refused')
        if spec.get('initContainers') or spec.get('ephemeralContainers') or len(spec.get('containers', [])) != 1:
            raise ValueError('metadata-added-container-refused')
        container = spec['containers'][0]
        defaults = {'terminationMessagePath': '/dev/termination-log', 'terminationMessagePolicy': 'File'}
        if (set(container) - set(desired['spec']['containers'][0]) - set(defaults)
                or any(container[key] != value for key, value in defaults.items() if key in container)):
            raise ValueError('metadata-unreviewed-container-field')
        for field, expected in desired['spec']['containers'][0].items():
            if container.get(field) != expected:
                raise ValueError('metadata-container-drift')
        if container.get('env') or container.get('envFrom'):
            raise ValueError('metadata-added-environment-refused')
        if spec.get('securityContext') != desired['spec']['securityContext']:
            raise ValueError('metadata-pod-security-drift')


def resource_path(obj):
    if obj['kind'] == 'Pod':
        return '/api/v1/namespaces/kube-system/pods/' + obj['metadata']['name']
    return '/apis/networking.k8s.io/v1/namespaces/kube-system/networkpolicies/' + obj['metadata']['name']


def bounded_command(argv, data=None, timeout=30, maximum=4 * 1024 * 1024):
    """Own and cap all three pipes under one deadline, including stdin delivery."""
    data = data or b''
    if len(data) > 1048576:
        raise ValueError('metadata-command-input-bound')
    deadline, output, errors, sent = time.monotonic() + timeout, bytearray(), bytearray(), 0
    child = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                             start_new_session=True, env={'LANG': 'C.UTF-8'})
    try:
        with selectors.DefaultSelector() as selected:
            for stream, label in ((child.stdout, 'stdout'), (child.stderr, 'stderr')):
                os.set_blocking(stream.fileno(), False)
                selected.register(stream, selectors.EVENT_READ, label)
            if data:
                os.set_blocking(child.stdin.fileno(), False)
                selected.register(child.stdin, selectors.EVENT_WRITE, 'stdin')
            else:
                child.stdin.close()
            while selected.get_map():
                if time.monotonic() >= deadline:
                    raise ValueError('metadata-command-deadline')
                for key, _ in selected.select(min(.1, max(0, deadline - time.monotonic()))):
                    if key.data == 'stdin':
                        sent += os.write(key.fd, data[sent:sent + 65536])
                        if sent == len(data):
                            selected.unregister(key.fileobj)
                            key.fileobj.close()
                        continue
                    target, limit = (output, maximum) if key.data == 'stdout' else (errors, 65536)
                    chunk = os.read(key.fd, min(65536, limit - len(target) + 1))
                    if not chunk:
                        selected.unregister(key.fileobj)
                        continue
                    target.extend(chunk)
                    if len(target) > limit:
                        raise ValueError('metadata-command-output-bound')
            child.wait(timeout=max(.001, deadline - time.monotonic()))
        return subprocess.CompletedProcess(argv, child.returncode, bytes(output), bytes(errors))
    except BaseException:
        try:
            os.killpg(child.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        child.wait(timeout=5)
        raise
    finally:
        for stream in (child.stdin, child.stdout, child.stderr):
            stream.close()


class Client:
    def __init__(self, args, attempt):
        self.args, self.attempt, self.sequence, self.requests = args, attempt, 0, []
        self.bindings = {p: file_digest(p) for p in (args.kubectl, args.kubeconfig)}
        argv = [str(args.kubectl), '--kubeconfig', str(args.kubeconfig), '--context', args.context,
                'config', 'view', '--minify', '--raw', '-o', 'json']
        result = bounded_command(argv, timeout=15, maximum=1048576)
        self.attempt.encrypt('metadata-offline-config', canonical({
            'exitCode': result.returncode, 'stdoutBase64': __import__('base64').b64encode(result.stdout).decode(),
            'stderrBase64': __import__('base64').b64encode(result.stderr).decode()}), args.age, args.recipient)
        if result.returncode or len(result.stdout) > 1048576:
            raise ValueError('metadata-static-native-config-unavailable')
        config = json.loads(result.stdout)
        if len(config.get('users', [])) != 1 or len(config.get('clusters', [])) != 1:
            raise ValueError('metadata-static-native-config-refused')
        user = config['users'][0].get('user', {})
        cluster = config['clusters'][0].get('cluster', {})
        endpoint = __import__('urllib.parse', fromlist=['urlparse']).urlparse(cluster.get('server', ''))
        if (set(user) != {'client-certificate-data', 'client-key-data'}
                or set(cluster) != {'server', 'certificate-authority-data'}
                or endpoint.scheme != 'https' or not endpoint.hostname or endpoint.username or endpoint.password
                or endpoint.query or endpoint.fragment or endpoint.path not in ('', '/')):
            raise ValueError('metadata-static-direct-native-client-required')

    def run(self, verb, path, body=None, absent=False):
        if verb not in ('get', 'create', 'delete') or not path.startswith(('/api/', '/apis/')):
            raise ValueError('metadata-command-refused')
        if any(file_digest(p) != digest for p, digest in self.bindings.items()):
            raise ValueError('metadata-client-input-drift')
        self.sequence += 1
        argv = [str(self.args.kubectl), '--kubeconfig', str(self.args.kubeconfig), '--context', self.args.context,
                '--request-timeout=20s', verb, '--raw', path]
        if body is not None:
            argv += ['-f', '-']
        self.attempt.encrypt(f'metadata-{self.sequence:03d}-intent', canonical({'argv': argv, 'body': body}),
                             self.args.age, self.args.recipient)
        request = {'verb': verb, 'path': path, 'exitCode': None, 'dispatched': True}
        self.requests.append(request)
        result = bounded_command(argv, canonical(body) if body is not None else None)
        request['exitCode'] = result.returncode
        self.attempt.encrypt(f'metadata-{self.sequence:03d}-result', canonical({
            'exitCode': result.returncode, 'stdoutBase64': __import__('base64').b64encode(result.stdout).decode(),
            'stderrBase64': __import__('base64').b64encode(result.stderr).decode()}), self.args.age, self.args.recipient)
        if result.returncode != 0:
            lines = result.stderr.decode(errors='replace').strip().splitlines()
            if (absent and result.returncode == 1 and len(lines) == 1
                    and lines[0].startswith('Error from server (NotFound): ')
                    and '"' + path.rsplit('/', 1)[1] + '"' in lines[0]):
                return None
            raise ValueError('metadata-native-command-refused')
        if len(result.stdout) > 4 * 1024 * 1024:
            raise ValueError('metadata-native-output-bound')
        if path.endswith('/log'):
            return json.loads(result.stdout)
        return json.loads(result.stdout)

    def protected_state(self):
        values = []
        for plural in ('namespaces', 'persistentvolumeclaims', 'persistentvolumes'):
            data = self.run('get', '/api/v1/' + plural + '?limit=500')
            if not isinstance(data.get('items'), list) or data.get('metadata', {}).get('continue'):
                raise ValueError('metadata-protected-census-incomplete')
            rows = []
            for obj in data['items']:
                md, spec = obj['metadata'], obj.get('spec', {})
                rows.append({'namespace': md.get('namespace'), 'name': md['name'], 'uid': md['uid'],
                             'volumeName': spec.get('volumeName'), 'claimRef': spec.get('claimRef')})
            values.append(sorted(rows, key=lambda row: (row['namespace'] or '', row['name'])))
        return values


def collect(args, manifests_, attempt):
    client = Client(args, attempt)
    owned, outcome, cleanup, before, preserved, identity_verified = [], None, [], None, False, False
    error = None
    try:
        before = client.protected_state()
        for path, uid in [('/api/v1/nodes/' + args.node, args.node_uid),
                          ('/api/v1/namespaces/kube-system', args.namespace_uid)]:
            if client.run('get', path)['metadata']['uid'] != uid:
                raise ValueError('metadata-source-identity-drift')
        for obj in manifests_:
            path = resource_path(obj)
            if client.run('get', path, absent=True) is not None:
                raise ValueError('metadata-helper-name-not-absent')
            ownership = [obj, None]
            owned.append(ownership)  # A lost response never disappears from the cleanup outcome.
            created = client.run('create', path.rsplit('/', 1)[0], obj)
            ownership[1] = created['metadata']['uid']
            assert_owned(created, obj, created['metadata']['uid'])
        pod, uid = owned[-1]
        deadline = time.monotonic() + 180
        while True:
            current = client.run('get', resource_path(pod))
            assert_owned(current, pod, uid)
            phase = current.get('status', {}).get('phase')
            if phase == 'Succeeded':
                break
            if phase == 'Failed' or time.monotonic() >= deadline:
                client.run('get', resource_path(pod) + '/log')
                raise ValueError('metadata-helper-failed-or-timeout')
            time.sleep(2)
        outcome = client.run('get', resource_path(pod) + '/log')
        if (outcome.get('kind') != 'node-file-metadata-only' or outcome.get('sourceContentExported') is not False
                or outcome.get('networkOperations') != 0 or not outcome.get('entries')):
            raise ValueError('metadata-helper-result-refused')
        preserved = client.protected_state() == before
        if not preserved:
            raise ValueError('metadata-protected-identity-or-binding-drift')
        for path, uid in [('/api/v1/nodes/' + args.node, args.node_uid),
                          ('/api/v1/namespaces/kube-system', args.namespace_uid)]:
            if client.run('get', path)['metadata']['uid'] != uid:
                raise ValueError('metadata-source-identity-drift')
        identity_verified = True
    except Exception as exc:
        error = type(exc).__name__ + (':' + str(exc) if isinstance(exc, ValueError) else '')
    finally:
        for obj, uid in reversed(owned):
            try:
                path = resource_path(obj)
                current = client.run('get', path, absent=True)
                if current is not None:
                    if uid is None:
                        cleanup.append({'kind': obj['kind'], 'uid': None, 'ownershipVerified': False,
                                        'absenceVerified': False, 'reason': 'unverified-create-response'})
                        continue
                    assert_owned(current, obj, uid)
                    client.run('delete', path, {'apiVersion': 'v1', 'kind': 'DeleteOptions',
                        'preconditions': {'uid': uid, 'resourceVersion': current['metadata']['resourceVersion']},
                        'propagationPolicy': 'Foreground'})
                    deadline = time.monotonic() + 60
                    while client.run('get', path, absent=True) is not None:
                        if time.monotonic() >= deadline:
                            raise ValueError('metadata-cleanup-timeout')
                        time.sleep(1)
                cleanup.append({'kind': obj['kind'], 'uid': uid, 'absenceVerified': True})
            except Exception:
                cleanup.append({'kind': obj['kind'], 'uid': uid, 'absenceVerified': False})
        try:
            attempt.encrypt('node-metadata-private-result', canonical({'metadata': outcome, 'error': error}),
                            args.age, args.recipient)
        except Exception:
            error = error or 'metadata-result-custody-refused'
    return {'kind': 'node-metadata-helper-result', 'error': error, 'metadataObserved': outcome is not None,
            'namespacePvcPvIdentitiesAndBindingsUnchanged': preserved,
            'sourceNodeAndNamespaceUidRevalidated': identity_verified,
            'fileCount': len(outcome.get('entries', {})) if outcome else 0,
            'cleanup': cleanup, 'cleanupPassed': len(cleanup) == len(owned) and all(v['absenceVerified'] for v in cleanup),
            'helperCreationIntents': len(owned), 'helperObjectsCreated': sum(uid is not None for _, uid in owned),
            'helperCreateOutcomesUnverified': sum(uid is None for _, uid in owned), 'requests': client.requests,
            'sourceContentArchived': False, 'snapshotTaken': False, 'uploadsPerformed': False,
            'networkPolicyIsCompleteFence': False, 'sourceRecoveryAccepted': False,
            'productionRetirementAuthorized': False, 'upgradeGate': 'closed'}


def parser():
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('attempt-id', 'node', 'node-uid', 'namespace-uid', 'image-pin', 'context', 'recipient', 'readback-recipient'):
        p.add_argument('--' + name, required=True)
    for name in ('kubeconfig', 'kubectl', 'age', 'readback-identity', 'evidence-root'):
        p.add_argument('--' + name, required=True, type=Path)
    p.add_argument('--execute-metadata-attempt')
    return p


def main():
    args = parser().parse_args()
    for path in (args.kubeconfig, args.readback_identity):
        info = path.lstat()
        if not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid() or stat.S_IMODE(info.st_mode) != 0o600:
            raise SystemExit('metadata-private-input-custody-refused')
    objects = manifests(args.attempt_id, args.node, args.node_uid, args.namespace_uid, args.image_pin)
    attempt = Attempt(ROOT, args.evidence_root, args.attempt_id, 'retirement',
                      readback_recipient=args.readback_recipient, readback_identity=args.readback_identity)
    attempt.record('metadata-proposal.json', {'kind': 'node-metadata-only-proposal', 'manifests': objects,
        'sourceSha256': file_digest(Path(__file__)), 'kubeconfigSha256': file_digest(args.kubeconfig),
        'kubectlSha256': file_digest(args.kubectl), 'privateContentCaptureAuthorized': False,
        'scope': 'Temporary own Pod/NetworkPolicy only; fixed metadata process; no complete network fence claim'})
    if args.execute_metadata_attempt is not None:
        if args.execute_metadata_attempt != args.attempt_id:
            raise SystemExit('metadata-explicit-attempt-mismatch')
        result = collect(args, objects, attempt)
        attempt.record('metadata-result.json', result)
    else:
        result = {'metadataObserved': False, 'cleanupPassed': False, 'proposalOnly': True}
    attempt.finish(publish=False)
    print(json.dumps({'privateDirectory': str(attempt.directory), **result}, sort_keys=True))
    if args.execute_metadata_attempt and (not result['metadataObserved'] or not result['cleanupPassed'] or result['error']):
        raise SystemExit(1)


if __name__ == '__main__':
    main()
