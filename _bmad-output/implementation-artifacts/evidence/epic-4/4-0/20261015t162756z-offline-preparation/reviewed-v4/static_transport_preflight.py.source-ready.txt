#!/usr/bin/env python3
"""Administrator-only local static transport inspection. No network or auth.

Preparation must not run this against the actual kubeconfig. The immediate
request is a protected local run of these exact reviewed bytes and binding.
No raw config, child stdout/stderr, exception text or secret reaches output.
"""

import argparse
import base64
from datetime import datetime, timezone
import fcntl
import hashlib
import json
import os
from pathlib import Path
import selectors
import signal
import stat
import subprocess
import time


BINDING = Path('/home/administrator/hexalith-recovery-evidence/4-0/20261015t162756z/static-transport-preflight-v4.json')
KUBECONFIG = Path('/home/administrator/.kube/hexalith-production')
EXPECTED_CONFIG_SHA256 = 'f00b691a5886ef76e6be0d6e3efe4030251147b7bba74bb0c58fa1944e0fe86c'
KUBECTL = Path('/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl')
EXPECTED_KUBECTL_SHA256 = '90b7b9058ffeb5c10710bb1c73f541eaf426fb1b4e87df435db669de9a2564a2'
PYTHON = Path('/usr/bin/python3.14')
SOURCE = Path(__file__).absolute()
CONTEXT = 'jpiquot@local'
ENDPOINT = 'https://192.168.1.30:6443'
MAX_CONFIG_BYTES = 1024 * 1024
MAX_OUTPUT_BYTES = 2 * MAX_CONFIG_BYTES
MAX_ERROR_BYTES = 65536
TIMEOUT_SECONDS = 10


class Refusal(ValueError):
    """A fixed sanitized reason; never construct one from child/input text."""


class SanitizedParser(argparse.ArgumentParser):
    """Never echo unrecognized arguments or supplied values on parse failure."""

    def error(self, message):
        raise Refusal('invalid local preflight invocation')


def require(ok, reason):
    if not ok:
        raise Refusal(reason)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def custody(path, private=False):
    require(path.is_absolute() and '..' not in path.parts, 'unsafe local path')
    for item in (*reversed(path.parents), path):
        info = item.lstat()
        require(not stat.S_ISLNK(info.st_mode) and info.st_uid in (0, os.getuid()), 'unsafe local custody')
        mode = stat.S_IMODE(info.st_mode)
        sticky_root = stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and mode & stat.S_ISVTX
        require(not mode & 0o022 or sticky_root, 'writable local custody')
    require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1, 'regular single-link input required')
    if private:
        require(info.st_uid == os.getuid() and stat.S_IMODE(info.st_mode) == 0o600,
                'private input must be owner-only0600')
    return info


def read_exact(path, expected, maximum, private=False):
    info = custody(path, private)
    require(info.st_size <= maximum, 'local input size limit')
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as stream:
        opened = os.fstat(stream.fileno())
        require((opened.st_dev, opened.st_ino) == (info.st_dev, info.st_ino), 'local input replaced')
        raw = stream.read(maximum + 1)
        end = os.fstat(stream.fileno())
    require((end.st_size, end.st_mtime_ns, end.st_ctime_ns)
            == (info.st_size, info.st_mtime_ns, info.st_ctime_ns), 'local input changed')
    require(len(raw) <= maximum and sha(raw) == expected, 'local input digest mismatch')
    return raw


def strict_json(raw):
    def pairs(items):
        result = {}
        for k, v in items:
            require(k not in result, 'duplicate metadata field')
            result[k] = v
        return result
    value = json.loads(raw, object_pairs_hook=pairs,
                       parse_float=lambda _: require(False, 'floating metadata refused'),
                       parse_constant=lambda _: require(False, 'nonfinite metadata refused'))
    require(isinstance(value, dict), 'metadata object required')
    return value


def run_view(raw):
    """Pass only sealed in-memory bytes to a fixed non-authenticating command."""
    fd = os.memfd_create('hexalith-static-kubeconfig', os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
    try:
        os.fchmod(fd, 0o600)
        with os.fdopen(os.dup(fd), 'wb') as stream:
            stream.write(raw)
        os.lseek(fd, 0, os.SEEK_SET)
        seals = fcntl.F_SEAL_WRITE | fcntl.F_SEAL_GROW | fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_SEAL
        fcntl.fcntl(fd, fcntl.F_ADD_SEALS, seals)
        require(fcntl.fcntl(fd, fcntl.F_GET_SEALS) & seals == seals, 'in-memory config seal failed')
        argv = [str(KUBECTL), '--kubeconfig', f'/proc/self/fd/{fd}', '--context', CONTEXT,
                'config', 'view', '--raw', '-o', 'json']
        child = subprocess.Popen(argv, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                                 stderr=subprocess.PIPE, env={'LANG': 'C.UTF-8'},
                                 pass_fds=(fd,), start_new_session=True, shell=False,
                                 cwd='/')
        try:
            stdout = bounded_output(child, time.monotonic() + TIMEOUT_SECONDS)
        except (Refusal, OSError, subprocess.TimeoutExpired):
            try:
                os.killpg(child.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            child.wait()
            raise Refusal('local config parser exceeded bounds or refused') from None
        finally:
            child.stdout.close()
            child.stderr.close()
        require(child.returncode == 0, 'local config parser refused')
        return stdout
    finally:
        os.close(fd)


def bounded_output(child, deadline):
    """Drain capped pipes under one deadline; never buffer past either cap."""
    output = bytearray()
    error_bytes = 0
    with selectors.DefaultSelector() as selector:
        for stream, label in ((child.stdout, 'stdout'), (child.stderr, 'stderr')):
            os.set_blocking(stream.fileno(), False)
            selector.register(stream, selectors.EVENT_READ, label)
        while selector.get_map():
            remaining = deadline - time.monotonic()
            require(remaining > 0, 'local config parser timed out')
            for key, _ in selector.select(min(remaining, 0.1)):
                used = len(output) if key.data == 'stdout' else error_bytes
                limit = MAX_OUTPUT_BYTES if key.data == 'stdout' else MAX_ERROR_BYTES
                chunk = os.read(key.fd, min(65536, limit - used + 1))
                if not chunk:
                    selector.unregister(key.fileobj)
                    continue
                require(used + len(chunk) <= limit, 'local parser output limit')
                if key.data == 'stdout':
                    output.extend(chunk)
                else:
                    error_bytes += len(chunk)
        remaining = deadline - time.monotonic()
        require(remaining > 0, 'local config parser timed out')
        child.wait(timeout=remaining)
    return bytes(output)


def validate_schema(config):
    require(set(config) <= {'apiVersion', 'kind', 'preferences', 'clusters', 'contexts', 'users', 'current-context'}
            and config.get('apiVersion') == 'v1' and config.get('kind') == 'Config'
            and config.get('current-context') == CONTEXT and config.get('preferences') in ({}, None),
            'static kubeconfig root or selected context mismatch')

    def named(items, field):
        require(isinstance(items, list) and 0 < len(items) <= 32, 'bounded named static entries required')
        result = {}
        for item in items:
            require(isinstance(item, dict) and set(item) == {'name', field}
                    and isinstance(item['name'], str) and item['name'] and item['name'] not in result
                    and isinstance(item[field], dict), 'static named entry schema refused')
            result[item['name']] = item[field]
        return result

    contexts = named(config['contexts'], 'context')
    clusters = named(config['clusters'], 'cluster')
    users = named(config['users'], 'user')
    for context in contexts.values():
        require(set(context) <= {'cluster', 'user', 'namespace'} and set(context) >= {'cluster', 'user'}
                and context['cluster'] in clusters and context['user'] in users, 'static context reference refused')
    selected = contexts[CONTEXT]
    for cluster in clusters.values():
        require(set(cluster) == {'server', 'certificate-authority-data'}
                and isinstance(cluster['server'], str) and cluster['server'].startswith('https://')
                and isinstance(cluster['certificate-authority-data'], str), 'TLS/static cluster schema refused')
        ca = base64.b64decode(cluster['certificate-authority-data'], validate=True)
        require(b'-----BEGIN CERTIFICATE-----' in ca and b'PRIVATE KEY' not in ca, 'public CA data required')
    require(clusters[selected['cluster']]['server'] == ENDPOINT, 'selected source endpoint mismatch')
    for user in users.values():
        if set(user) == {'token'}:
            require(isinstance(user['token'], str) and 0 < len(user['token']) <= 65536,
                    'static bearer token schema refused')
        else:
            require(set(user) == {'client-certificate-data', 'client-key-data'}, 'nonstatic credential schema refused')
            for field in user:
                require(isinstance(user[field], str) and bool(base64.b64decode(user[field], validate=True)),
                        'inline static credential data required')
    return {'staticTransportSchema': 'pass', 'operationalGate': 'closed',
            'authenticationPerformed': False, 'apiCalls': 0, 'authorityAccepted': False,
            'providerOrIamQualified': False, 'captureOrExecutionAuthorized': False}


def preflight(binding):
    require(set(binding) == {'kind', 'schemaVersion', 'kubeconfig', 'kubectl', 'interpreter', 'source', 'limits'},
            'preflight binding fields mismatch')
    require(binding['kind'] == 'hexalith.local-static-transport-preflight' and binding['schemaVersion'] == 1,
            'preflight binding kind mismatch')
    require(binding['kubeconfig'] == {'path': str(KUBECONFIG), 'sha256': EXPECTED_CONFIG_SHA256,
            'context': CONTEXT, 'endpoint': ENDPOINT}, 'fixed config binding mismatch')
    require(binding['limits'] == {'maximumConfigBytes': MAX_CONFIG_BYTES,
            'maximumOutputBytes': MAX_OUTPUT_BYTES, 'timeoutSeconds': TIMEOUT_SECONDS,
            'networkCallsAllowed': False, 'authenticationAllowed': False}, 'preflight limits changed')
    for name, path in (('source', SOURCE), ('interpreter', PYTHON), ('kubectl', KUBECTL)):
        item = binding[name]
        require(set(item) == {'path', 'sha256', 'bytes'} and item['path'] == str(path), 'fixed local tool path mismatch')
        raw = read_exact(path, item['sha256'], 128 * 1024 * 1024)
        require(len(raw) == item['bytes'], 'fixed local tool size mismatch')
    require(binding['kubectl']['sha256'] == EXPECTED_KUBECTL_SHA256, 'kubectl pinned bytes mismatch')
    raw = read_exact(KUBECONFIG, EXPECTED_CONFIG_SHA256, MAX_CONFIG_BYTES, private=True)
    # The raw input is never written to disk, inherited by a shell or printed.
    result = validate_schema(strict_json(run_view(raw)))
    read_exact(KUBECTL, EXPECTED_KUBECTL_SHA256, 128 * 1024 * 1024)
    return result


def main():
    parser = SanitizedParser(description=__doc__)
    parser.add_argument('--binding-sha256', required=True, help='exact independently reviewed fixed binding digest')
    try:
        args = parser.parse_args()
        parent = BINDING.parent.lstat()
        require(parent.st_uid == os.getuid() and stat.S_IMODE(parent.st_mode) == 0o700,
                'fresh binding parent must be owner-only0700')
        binding_raw = read_exact(BINDING, args.binding_sha256, 65536, private=True)
        binding = strict_json(binding_raw)
        result = preflight(binding)
        result['observedAt'] = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')
        result['inputs'] = {'bindingSha256': sha(binding_raw),
            'kubeconfigSha256': binding['kubeconfig']['sha256'],
            'sourceSha256': binding['source']['sha256'],
            'kubectlSha256': binding['kubectl']['sha256'],
            'interpreterSha256': binding['interpreter']['sha256']}
        code = 0
    except (Refusal, OSError, ValueError, TypeError, KeyError, AttributeError, IndexError, RecursionError, OverflowError):
        result = {'staticTransportSchema': 'refused', 'operationalGate': 'closed',
                  'authenticationPerformed': False, 'apiCalls': 0, 'authorityAccepted': False,
                  'reason': 'local static transport preflight refused; no raw diagnostics are emitted'}
        code = 1
    print(json.dumps(result, sort_keys=True))
    return code


if __name__ == '__main__':
    raise SystemExit(main())
