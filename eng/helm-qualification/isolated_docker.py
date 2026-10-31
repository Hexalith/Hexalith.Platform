"""Render an offline rootless-engine proposal; never inspect or change a host.

Unresolved @TOKENS@ and null observations are intentional admission blockers.
This module uses only local file operations, not Qualification or its transports.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import stat


ACCOUNT = 'hexalith-calibration'
HOME_PATH = '/home/' + ACCOUNT
SCRATCH = HOME_PATH + '/engine-scratch'
RUNTIME = '/run/user/@UID@/' + ACCOUNT
BIN = HOME_PATH + '/tools/@TOOLSET@/bin'
UNIT = 'hexalith-calibration-docker.service'
GIB = 1024 ** 3


def encoded(value):
    """Return deterministic readable JSON bytes, without time or local-path data."""
    return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode('utf-8')


def proposal():
    """Describe ceilings and missing observations without issuing run authority."""
    return {
        'schema': 'hexalith.platform.isolated-docker-proposal/v1',
        'purpose': 'offline-host-setup-proposal',
        'host': {'name': 'node1', 'address': '192.168.1.30'},
        'account': {'name': ACCOUNT, 'home': HOME_PATH, 'uid': None, 'gid': None,
            'subuid': None, 'subgid': None},
        'paths': {'socket': 'unix://' + RUNTIME + '/docker.sock',
            'data': SCRATCH + '/docker', 'exec': RUNTIME + '/exec',
            'pid': RUNTIME + '/dockerd.pid', 'rootlessKit': RUNTIME + '/rootlesskit',
            'managedContainerdData': SCRATCH + '/docker/containerd/daemon',
            'managedContainerdExec': RUNTIME + '/exec/containerd',
            'tools': BIN, 'scratch': SCRATCH},
        'ceilings': {'workloadCpuMilli': 10250, 'workloadMemoryBytes': 19 * GIB,
            'postgresCpuMilli': 2000, 'postgresMemoryBytes': 4 * GIB,
            'engineAllowanceCpuMilli': 2000, 'engineAllowanceMemoryBytes': 4 * GIB,
            'userSliceCpuMilli': 12250, 'userSliceMemoryBytes': 23 * GIB,
            'userSliceSwapBytes': 0, 'userSliceTasks': 32768,
            'containerLogFiles': 3,
            'containerLogFileBytes': 10 * 1024 ** 2,
            'additionalScratchBytes': 24 * GIB, 'scratchMinimumFreePermille': 200},
        'observations': {key: None for key in (
            'hostExecutionTransport', 'newHostChangeApproval', 'protectedBefore', 'protectedAfter',
            'accountAndSubordinateIdNonoverlap', 'verifiedToolVersionsAndHashes',
            'userNamespacesAndHelpers', 'nativeOverlayBacking', 'cgroupV2AndDelegation',
            'kernelAndAppArmorPrerequisites', 'rootlessKindPrerequisites',
            'netHeadroomAndCompetingReservations', 'enforcedScratchQuotaAndFreeMargin',
            'enforcedIoLimits', 'privateNetworkRanges', 'effectiveAncestorLimits',
            'rootlessEngineIdentityAndOwnership', 'emptyEngine',
            'localCappedRunnerRouting', 'existingCalibrationPlanOperationalBindings')},
        'authority': {'hostChangesAuthorized': False, 'launchReady': False,
            'calibrationApproval': 'existing-accepted-unused', 'calibrationAttemptsConsumed': 0,
            'hostCapture': 'consumed-do-not-repeat', 'qualificationCredit': False,
            'productionCredit': False, 'a41Closed': False},
    }


def payloads():
    """Render deliberately uninstalled templates and the owner operational recipe."""
    service = f'''# PROPOSAL ONLY. Resolve tokens and obtain exact new-host-change approval first.
[Unit]
Description=Platform isolated calibration rootless Docker

[Service]
Type=notify
NotifyAccess=all
Environment="PATH={BIN}:/usr/bin:/bin:/usr/sbin:/sbin"
Environment="DOCKERD={BIN}/dockerd"
Environment="HOME={HOME_PATH}"
Environment="XDG_RUNTIME_DIR=%t"
Environment="XDG_CONFIG_HOME={HOME_PATH}/.config"
Environment="XDG_CACHE_HOME={SCRATCH}/cache"
Environment="TMPDIR={SCRATCH}/tmp"
Environment="DOCKER_CONFIG={HOME_PATH}/.docker"
Environment="DOCKERD_ROOTLESS_ROOTLESSKIT_STATE_DIR=%t/{ACCOUNT}/rootlesskit"
Environment="DOCKERD_ROOTLESS_ROOTLESSKIT_NET=slirp4netns"
Environment="DOCKERD_ROOTLESS_ROOTLESSKIT_PORT_DRIVER=builtin"
Environment="DOCKERD_ROOTLESS_ROOTLESSKIT_DISABLE_HOST_LOOPBACK=true"
Environment="DOCKERD_ROOTLESS_ROOTLESSKIT_DETACH_NETNS=false"
Environment="DOCKERD_ROOTLESS_ROOTLESSKIT_SLIRP4NETNS_SANDBOX=true"
Environment="DOCKERD_ROOTLESS_ROOTLESSKIT_SLIRP4NETNS_SECCOMP=true"
UnsetEnvironment=DOCKER_HOST DOCKER_CONTEXT DOCKER_TLS_VERIFY DOCKER_CERT_PATH CONTAINERD_ADDRESS CONTAINERD_NAMESPACE _DOCKERD_ROOTLESS_CHILD DOCKERD_ROOTLESS_ROOTLESSKIT_FLAGS HTTP_PROXY HTTPS_PROXY ALL_PROXY http_proxy https_proxy all_proxy
RuntimeDirectory={ACCOUNT}
RuntimeDirectoryMode=0700
UMask=0077
ExecStart={BIN}/dockerd-rootless.sh --config-file={HOME_PATH}/.config/hexalith-calibration/daemon.json --host=unix://%t/{ACCOUNT}/docker.sock --data-root={SCRATCH}/docker --exec-root=%t/{ACCOUNT}/exec --pidfile=%t/{ACCOUNT}/dockerd.pid
Delegate=yes
KillMode=mixed
TimeoutStartSec=120
TimeoutStopSec=120
Restart=no
# Container logs rotate below; daemon stdout/stderr are discarded (no retained console claim).
StandardOutput=null
StandardError=null
'''
    slice_config = f'''# Install ONLY for the resolved dedicated UID, never user-.slice.d.
[Slice]
MemoryAccounting=yes
TasksAccounting=yes
IOAccounting=yes
CPUQuota=1225%
CPUQuotaPeriodSec=100ms
MemoryMax={23 * GIB}
MemorySwapMax=0
TasksMax=32768
# All four concrete device limits need custodian approval and effective readback.
IOReadBandwidthMax=@IO_DEVICE@ @READ_BYTES_PER_SECOND@
IOWriteBandwidthMax=@IO_DEVICE@ @WRITE_BYTES_PER_SECOND@
IOReadIOPSMax=@IO_DEVICE@ @READ_IOPS@
IOWriteIOPSMax=@IO_DEVICE@ @WRITE_IOPS@
'''
    daemon = {'storage-driver': 'overlay2', 'features': {'containerd-snapshotter': False},
        'exec-opts': ['native.cgroupdriver=systemd'], 'default-cgroupns-mode': 'private',
        'userland-proxy-path': BIN + '/docker-proxy', 'log-driver': 'json-file',
        'log-opts': {'max-size': '10m', 'max-file': '3'},
        'ip': '127.0.0.1', 'bip': '@BRIDGE_CIDR@',
        'default-address-pools': [{'base': '@DOCKER_POOL_CIDR@', 'size': 24}],
        'default-network-opts': {'bridge': {'com.docker.network.bridge.host_binding_ipv4': '127.0.0.1'}},
        'live-restore': False}
    return {
        'proposal.json': encoded(proposal()),
        'user/' + UNIT + '.in': service.encode('utf-8'),
        'system/user-@UID@.slice.d/50-calibration.conf.in': slice_config.encode('utf-8'),
        'system/user@@UID@.service.d/50-calibration-delegate.conf.in':
            b'# Dedicated UID instance only; do not change user@.service globally.\n[Service]\nDelegate=cpu cpuset io memory pids\n',
        'config/daemon.json.in': encoded(daemon),
        'config/docker-client.json': encoded({'auths': {}}),
        'OPERATIONS.md': Path(__file__).with_name('ISOLATED-DOCKER.md').read_bytes(),
    }


def _directory(path):
    """Open one real directory without following a final symlink."""
    return os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)


def render(output):
    """Render privately even under an unusual caller umask; restore it on exit."""
    previous = os.umask(0o077)
    try:
        return _render(output)
    finally:
        os.umask(previous)


def _render(output):
    """Create only a new private bundle, with anchored no-follow file writes.

    Every parent must already exist and be a real directory. The immediate parent
    must be privately owned by this user; no ancestors or prior bytes are changed.
    A partial failure retains the new partial bundle for explicit inspection.
    """
    path = Path(output)
    if not path.is_absolute() or any(p in ('.', '..') for p in str(output).split('/')):
        raise ValueError('output-must-be-absolute-without-dot-components')
    # Walk each directory via its open parent so symlink ancestors cannot redirect writes.
    fd = _directory('/')
    try:
        for component in path.parts[1:-1]:
            next_fd = os.open(component, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=fd)
            os.close(fd)
            fd = next_fd
        parent = os.fstat(fd)
        if parent.st_uid != os.geteuid() or stat.S_IMODE(parent.st_mode) & 0o077:
            raise ValueError('output-parent-must-be-private-and-owned')
        if not path.name:
            raise ValueError('fresh-output-required')
        # mkdir is exclusive, including a dangling symlink or an existing empty directory.
        files = payloads()
        os.mkdir(path.name, 0o700, dir_fd=fd)
        root_fd = os.open(path.name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=fd)
        try:
            os.fchmod(root_fd, 0o700)
            directories = {'.': root_fd}
            try:
                inventory = {}
                for name, content in sorted(files.items()):
                    relative = Path(name)
                    current = '.'
                    for part in relative.parts[:-1]:
                        child = part if current == '.' else current + '/' + part
                        if child not in directories:
                            os.mkdir(part, 0o700, dir_fd=directories[current])
                            directories[child] = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                                dir_fd=directories[current])
                            os.fchmod(directories[child], 0o700)
                        current = child
                    _write(directories[current], relative.name, content)
                    inventory[name] = {'sha256': hashlib.sha256(content).hexdigest(), 'sizeBytes': len(content), 'mode': '0600'}
                receipt = {'schema': 'hexalith.platform.isolated-docker-bundle/v1',
                    'complete': True, 'hostContact': False, 'files': inventory,
                    'receiptBinding': 'receipt.json excludes itself; pin its raw SHA256 externally',
                    'generatorSha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
                # Persist all payload bytes and directory entries before publishing
                # a final complete receipt. A failed receipt fsync leaves only a
                # private pending file, never a final success marker.
                _write(root_fd, '.receipt.json.pending', encoded(receipt))
                for directory_fd in reversed(tuple(directories.values())):
                    os.fsync(directory_fd)
                os.fsync(fd)
                os.link('.receipt.json.pending', 'receipt.json', src_dir_fd=root_fd,
                    dst_dir_fd=root_fd, follow_symlinks=False)
                try:
                    os.fsync(root_fd)
                except OSError:
                    os.unlink('receipt.json', dir_fd=root_fd)
                    raise
                os.unlink('.receipt.json.pending', dir_fd=root_fd)
                return receipt
            finally:
                for name, directory_fd in directories.items():
                    if name != '.':
                        os.close(directory_fd)
        finally:
            os.close(root_fd)
    finally:
        os.close(fd)


def _write(parent_fd, name, content):
    """Write a new private regular file without clobber or symlink following."""
    fd = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600, dir_fd=parent_fd)
    with os.fdopen(fd, 'wb') as stream:
        os.fchmod(stream.fileno(), 0o600)
        stream.write(content)
        stream.flush()
        os.fsync(stream.fileno())


def main(argv=None):
    """Expose only local offline rendering; no install or lifecycle commands."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, help='fresh absolute path beneath an existing private directory')
    args = parser.parse_args(argv)
    try:
        receipt = render(args.output)
    except (OSError, ValueError) as exc:
        parser.exit(1, 'isolated-docker-preparation-refused: ' + type(exc).__name__ + '\n')
    print(json.dumps({'schema': receipt['schema'], 'files': len(receipt['files']),
        'hostContact': False, 'launchReady': False}, sort_keys=True))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
