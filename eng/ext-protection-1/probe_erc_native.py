#!/usr/bin/env python3
"""Narrow actual ERC integration in one owned disposable OpenBao 2.6.2 container.

No Live/Agents/cohort path is invoked. The exact image must already be local;
networking, persistent mounts and container logging remain disabled. Native
credentials/payloads never enter argv or retained evidence.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import tempfile

ROOT = Path(__file__).resolve().parents[2]
BASE_FILE = ROOT / 'eng/ext-secrets-1/probe_raft_non_revival.py'
SPEC = importlib.util.spec_from_file_location('erc_disposable_base', BASE_FILE)
base = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(base)
CASES = ('TrimRefusesLeakedOldCiphertextAndRetainsUnrelatedRoot',
         'ThousandTargetManifestReturnsCompleteOrderedOriginalVector',
         'PinRaceOrdersBeforeManifestOrLosesToTheCompleteManifest',
         'LostTrimAcknowledgementRecoversOnlyOriginalNativeEpochAndReceipt',
         'NativeTenantAndCompleteContextSubstitutionCannotDecrypt')
ERC_CONFIG = base.CONFIG.replace('  tls_disable = true\n', '  tls_disable = true\n  max_request_size = 67108864\n  max_request_json_memory = 134217728\n  max_request_json_strings = 1000000\n').replace(
    '  node_id = "disposable-negative-control"\n', '  node_id = "disposable-negative-control"\n  max_entry_size = 67108864\n  max_transaction_size = 67108864\n')


class ErcProbe(base.Probe):
    def __init__(self, assembly, assembly_sha256, runner=base.run_command, diagnostic=False, case=None, runtime_manifest=None, runtime_manifest_sha256=None, bootstrap=False):
        super().__init__(base.IMAGE, runner=runner)
        self.assembly, self.assembly_sha256 = Path(assembly), assembly_sha256
        self.runtime_manifest = Path(runtime_manifest) if runtime_manifest else None
        self.runtime_manifest_sha256 = runtime_manifest_sha256
        self.bootstrap = bootstrap
        self.diagnostic = diagnostic
        if case is not None and case not in CASES:
            raise ValueError('Closed native case required')
        self.case = case
        self.result['shippedBootstrapExecuted'] = bootstrap
        self.result.update(schema='EXT-PROTECTION-1.erc-native-integration.v1',
                           scope='Owned disposable native ERC; actual production custody/store with synthetic private eligibility only',
                           productionQualified=False, fullCopyInventoryQualified=False,
                           liveExecuted=False, agentsExecuted=False, cases=list(CASES),
                           testAssemblySha256=assembly_sha256,
                           harnessSourceSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                           isolationSourceSha256=hashlib.sha256(BASE_FILE.read_bytes()).hexdigest())

    def execute(self):
        expected = ROOT / 'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0/Hexalith.Platform.Protection.Tests.dll'
        frozen_runtime = False
        if self.runtime_manifest is not None:
            if not self.runtime_manifest.is_absolute() or self.runtime_manifest.is_symlink() or not self.runtime_manifest.is_file() or hashlib.sha256(self.runtime_manifest.read_bytes()).hexdigest() != self.runtime_manifest_sha256:
                raise base.Blocked('PinnedFrozenRuntimeManifestRequired')
            manifest = json.loads(self.runtime_manifest.read_bytes())
            root = self.runtime_manifest.parent
            if self.assembly.parent.resolve() != root.resolve() or set(manifest) != {'files'} or not manifest['files']:
                raise base.Blocked('PinnedFrozenRuntimeManifestRequired')
            for entry in manifest['files']:
                path = root / entry['path']
                if path.is_symlink() or not path.is_file() or '..' in Path(entry['path']).parts or path.is_absolute() and not path.is_relative_to(root) or hashlib.sha256(path.read_bytes()).hexdigest() != entry['sha256']:
                    raise base.Blocked('FrozenRuntimeChanged')
            if self.assembly.name not in {entry['path'] for entry in manifest['files']}:
                raise base.Blocked('FrozenAssemblyMissing')
            listed = set()
            for entry in manifest['files']:
                relative = Path(entry['path'])
                if relative.is_absolute() or '..' in relative.parts:
                    raise base.Blocked('FrozenRuntimeChanged')
                listed.add(relative.as_posix())
            observed = set()
            for path in root.rglob('*'):
                if path.is_symlink():
                    raise base.Blocked('FrozenRuntimeClosureIncomplete')
                if not path.is_file() or path.resolve() == self.runtime_manifest.resolve():
                    continue
                observed.add(path.relative_to(root).as_posix())
            if observed != listed:
                raise base.Blocked('FrozenRuntimeClosureIncomplete')
            self.result['frozenRuntimeManifestSha256'] = self.runtime_manifest_sha256
            frozen_runtime = True
        if not frozen_runtime and self.assembly.resolve() != expected.resolve() or self.assembly.is_symlink() or not self.assembly.is_file() \
                or not re.fullmatch('[0-9a-f]{64}', self.assembly_sha256) \
                or hashlib.sha256(self.assembly.read_bytes()).hexdigest() != self.assembly_sha256:
            raise base.Blocked('PinnedOwnedDebugNativeAssemblyRequired')
        inspected = base.document(self.command('image-inspect', ['image', 'inspect', self.image]).stdout)
        if not isinstance(inspected, list) or len(inspected) != 1:
            raise base.Blocked('ImageIdentityUnverified')
        image = inspected[0]
        revision = image.get('Config', {}).get('Labels', {}).get('org.opencontainers.image.revision')
        if not isinstance(revision, str) or not re.fullmatch('[0-9a-f]{40}', revision) \
                or self.image.split('@')[0].split(':')[0] + '@' + self.image.split('@')[1] not in image.get('RepoDigests', []) \
                or set(image.get('Config', {}).get('Volumes', {})) != {'/openbao/file', '/openbao/logs'}:
            raise base.Blocked('ImageIdentityUnverified')
        self.mark('image-identity', localImageId=image.get('Id'), sourceRevision=revision)
        self.creation_attempted = True
        arguments = base.create_args(self.image, self.run_id, self.name)
        arguments = [value.replace(base.CONFIG, ERC_CONFIG) for value in arguments]
        self.result['serverConfigurationSha256'] = hashlib.sha256(ERC_CONFIG.encode()).hexdigest()
        self.result['nativeBounds'] = {'raftMaxEntryBytes': 67108864, 'raftMaxTransactionBytes': 67108864, 'listenerMaxRequestBytes': 67108864,
                                       'listenerMaxJsonMemoryBytes': 134217728, 'listenerMaxJsonStrings': 1000000,
                                       'clientMaxHeadBytes': 31457280, 'clientMaxResponseBytes': 33554432,
                                       'legacyArchiveChunkBytes': 4194304, 'sharedArchiveMinChunkBytes': 16384, 'sharedArchiveMaxChunkBytes': 131072, 'sharedArchiveMaxChunks': 3073, 'archiveOriginalMaxBytes': 50331648, 'dataTmpfsBytes': 1610612736, 'containerMemoryBytes': 2147483648}
        # Bounded disposable space accommodates the real 1000-target growing-head native case.
        arguments = [value.replace('size=128m', 'size=1536m') if value.startswith('/openbao/file:') else value for value in arguments]
        self.container_id = self.command('container-create', arguments).stdout.decode('ascii').strip()
        if not re.fullmatch('[0-9a-f]{64}', self.container_id):
            self.container_id = None
            raise base.Blocked('ContainerIdentityUnverified')
        value = base.document(self.command('container-inspect', ['inspect', self.container_id]).stdout)[0]
        base.require_isolation(value, self.run_id, self.container_id)
        self.mark('container-isolation', network='none', storage='tmpfs', publishedPorts=False, persistentVolumes=False, logging='none')
        self.command('container-start', ['start', self.container_id])
        version = self.bao('native-version', 'version').stdout.decode('ascii').strip()
        match = re.match(r'^OpenBao v([^ ]+) \(([0-9a-f]{40})\)(?:,|$)', version)
        if not match or match.group(1) != base.VERSION or match.group(2) != revision:
            raise base.Blocked('NativeVersionMismatch')
        self.mark('native-version', version=base.VERSION, sourceRevision=revision)
        self.await_seal('startup-raft', False)
        initialized = base.document(self.bao('initialize', 'operator', 'init', '-format=json', '-key-shares=1', '-key-threshold=1').stdout)
        keys, token = initialized.get('unseal_keys_b64'), initialized.get('root_token')
        if not isinstance(keys, list) or len(keys) != 1 or not keys[0] or not isinstance(token, str) or not token:
            raise base.Blocked('InitializationUnverified')
        self.token, self.unseal_key = token, keys[0]
        self.await_seal('unsealed', True)
        self.leader('raft-leader')
        environment = dict(os.environ, ERC_NATIVE_DISPOSABLE_CONTAINER=self.container_id,
                           ERC_NATIVE_DISPOSABLE_NONCE=self.run_id, ERC_NATIVE_DISPOSABLE_TOKEN=self.token)
        command = ['dotnet', str(self.assembly), '-class', 'Hexalith.Platform.Protection.Tests.OpenBaoErasureRootIntegrationTests', '-noColor']
        if self.bootstrap:
            command[3] = 'Hexalith.Platform.Protection.Tests.OpenBaoBootstrapIntegrationTests'
        elif self.diagnostic:
            command[3] = 'Hexalith.Platform.Protection.Tests.OpenBaoNativeTransportDiagnostics'
        elif self.case:
            command += ['-method', '*.' + self.case]
        self.result['commands'].append({'stage': 'native-component-suite', 'argv': command, 'credentials': 'private-environment-omitted', 'timeoutSeconds': 3600})
        try:
            started = time.monotonic()
            with tempfile.TemporaryDirectory(prefix='erc-native-diagnostic-') as diagnostic_directory:
                environment['ERC_NATIVE_DIAGNOSTIC_DIRECTORY'] = diagnostic_directory
                completed = subprocess.run(command, env=environment, capture_output=True, timeout=3600, check=False)
                runtime_policies = []
                for policy_path in sorted(Path(diagnostic_directory).glob('policy-*.json')):
                    if policy_path.is_symlink() or policy_path.stat().st_size > 2048:
                        raise base.Blocked('RuntimePolicyEvidenceInvalid')
                    policy = json.loads(policy_path.read_bytes())
                    if set(policy) != {'shippedPolicySha256', 'renderedPolicySha256', 'transitMount', 'headMount', 'runtimePolicyName', 'rootUsedOnlyForBootstrap'} or not all(re.fullmatch('[0-9a-f]{64}', policy[key]) for key in ('shippedPolicySha256', 'renderedPolicySha256')) or policy['rootUsedOnlyForBootstrap'] is not True or not all(re.fullmatch('[A-Za-z0-9_-]{1,128}', policy[key]) for key in ('transitMount', 'headMount', 'runtimePolicyName')):
                        raise base.Blocked('RuntimePolicyEvidenceInvalid')
                    runtime_policies.append(policy)
                self.result['runtimePolicies'] = runtime_policies
                metadata = Path(diagnostic_directory) / 'native-stage.json'
                if metadata.is_file() and not metadata.is_symlink() and metadata.stat().st_size <= 4096:
                    safe = json.loads(metadata.read_bytes())
                    if set(safe) == {'stage', 'exceptionType', 'location', 'stages'} and (safe['stage'] in ('NativeFailure', 'RootWrapped') or re.fullmatch(r'RootWrapIndex[0-9]{1,3}', safe['stage'])) \
                            and (safe['exceptionType'] is None or re.fullmatch(r'[A-Za-z0-9_.]{1,200}', safe['exceptionType'])) \
                            and (safe['location'] is None or re.fullmatch(r'OpenBao(?:DisposableNativeHandler|NativeCustodyFixture|NativeTransportDiagnostics)\.cs:[0-9]{1,6}', safe['location'])) \
                            and isinstance(safe['stages'], list) and len(safe['stages']) <= 30 \
                            and all(re.fullmatch(r'(?:GET|POST):[A-Za-z0-9-]{1,40}:[1-5][0-9]{2}', value) for value in safe['stages']):
                        self.result['safeMetadataChannel'] = safe
                elif self.diagnostic:
                    self.result['safeMetadataChannel'] = {'stage': 'NativeTestBodyNotEntered'}
            self.result['nativeSuiteElapsedSeconds'] = round(time.monotonic() - started, 3)
        except (OSError, subprocess.TimeoutExpired):
            raise base.Blocked('BoundedNativeComponentSuiteUnavailable') from None
        # Raw test/provider output may contain private assertion inputs; retain only the closed numerical summary.
        summary = re.search(rb'Total:\s*(\d+),\s*Errors:\s*(\d+),\s*Failed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Not Run:\s*(\d+)', completed.stdout)
        counts = list(map(int, summary.groups())) if summary else None
        text = (completed.stdout + b'\n' + completed.stderr).decode('utf-8', errors='replace')
        self.result['safeFailureStages'] = sorted(set(re.findall(r'ERC_NATIVE_STAGE:[A-Za-z0-9,:-]+', text)))[:10]
        self.result['safeFailureLocations'] = sorted(set(re.findall(r'(?:OpenBaoDisposableNativeHandler|OpenBaoNativeCustodyFixture|OpenBaoNativeTransportDiagnostics|OpenBaoErasureRootIntegrationTests)\.cs(?:\([0-9]+,0\)|:line\s+[0-9]+)', text)))[:20]
        self.result['safeFailureTypes'] = sorted(set(re.findall(r'\b[A-Za-z][A-Za-z0-9_.]*Exception\b', text)))[:10]
        measured = []; occurrence_measured = []
        for line in text.splitlines():
            try:
                value = json.loads(line)
            except (ValueError, TypeError):
                continue
            fields = {'nativeHistoryGeometry', 'transitions', 'archiveWriteBytes', 'archiveWrites', 'supportedBytes', 'nativeHeadBytes', 'restrictedRuntimePolicy'}
            if isinstance(value, dict) and set(value) == fields and value['restrictedRuntimePolicy'] is True and all(type(value[key]) is int and 0 < value[key] < 2 ** 53 for key in fields - {'restrictedRuntimePolicy'}):
                measured.append(value)
            occurrence_fields = {'nativeOccurrences', 'nativeOwners', 'archiveWriteBytes', 'archiveWrites', 'nativeHeadBytes', 'lookupReadBound', 'restrictedRuntimePolicy'}
            if isinstance(value, dict) and set(value) == occurrence_fields and value['restrictedRuntimePolicy'] is True \
                    and all(type(value[key]) is int and 0 < value[key] < 2 ** 53 for key in occurrence_fields - {'restrictedRuntimePolicy'}):
                occurrence_measured.append(value)
        self.result['nativeOccurrenceGeometry'] = occurrence_measured
        self.result['nativeGrowthGeometry'] = measured

        self.result['nativeTestSummary'] = dict(zip(('total', 'errors', 'failed', 'skipped', 'notRun'), counts)) if counts else None
        if completed.returncode != 0 or counts != [1 if self.diagnostic or self.case or self.bootstrap else 5, 0, 0, 0, 0]:
            raise base.Blocked('NativeComponentCasesDidNotAllPass')
        if not self.diagnostic and not self.bootstrap and self.case in (None, 'NativeTenantAndCompleteContextSubstitutionCannotDecrypt') and (len(measured) != 2 or any(value['nativeHeadBytes'] <= 8 * 1024 * 1024 for value in measured)):
            raise base.Blocked('NativeHeadAndScalableHistoryGeometryMissing')
        if not self.diagnostic and not self.bootstrap and self.case in (None, 'NativeTenantAndCompleteContextSubstitutionCannotDecrypt') and (len(occurrence_measured) != 1
                or occurrence_measured[0]['nativeOccurrences'] != 256 or occurrence_measured[0]['nativeOwners'] != 256
                or occurrence_measured[0]['lookupReadBound'] != 16):
            raise base.Blocked('NativeAuthoritativeOccurrenceGeometryMissing')
        if len(self.result.get('runtimePolicies', [])) != (1 if self.diagnostic or self.case or self.bootstrap else 5):
            raise base.Blocked('ExclusiveRestrictedRuntimePolicyEvidenceMissing')
        self.mark('native-component-cases', count=1 if self.diagnostic or self.case or self.bootstrap else 5, actualCustodyStore=True, privateEligibility='ExplicitSyntheticTestFixture')

    def run(self):
        try:
            self.execute()
        except base.Blocked as error:
            self.result['failure'] = str(error)
        except (KeyboardInterrupt, Exception):
            self.result['failure'] = 'InterruptedOrUnexpectedNativeFailure'
        finally:
            self.token = self.unseal_key = ''
            try:
                self.cleanup()
            except (base.Blocked, KeyboardInterrupt, Exception):
                self.result['cleanup'] = {'complete': False, 'failure': 'OwnedCleanupUnverified'}
        passed = self.result['failure'] is None and self.result['cleanup'].get('complete') is True \
            and self.result['observations'].get('native-component-cases', {}).get('verified') is True
        self.result['phase'] = 'Completed'
        self.result['outcome'] = ('ShippedBootstrapRuntimeGenesisPassed' if self.bootstrap else 'NativeBootstrapPassed' if self.diagnostic else 'NativeCasePassed' if self.case else 'NativeCasesPassed') if passed else 'BlockedUnknown'
        return self.result, 0 if passed else 1


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--test-assembly', required=True)
    parser.add_argument('--test-assembly-sha256', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--runtime-manifest')
    parser.add_argument('--runtime-manifest-sha256')
    parser.add_argument('--diagnostic', action='store_true')
    parser.add_argument('--bootstrap', action='store_true')
    parser.add_argument('--case', choices=CASES)
    arguments = parser.parse_args(argv)
    output = Path(arguments.output)
    if not output.is_absolute() or output.exists() or not output.parent.is_dir() or output.parent.is_symlink():
        raise SystemExit('A new absolute evidence file in an existing local directory is required')
    if arguments.diagnostic and arguments.case:
        parser.error('Select a capacity diagnostic or one native case')
    result, code = ErcProbe(arguments.test_assembly, arguments.test_assembly_sha256, diagnostic=arguments.diagnostic, case=arguments.case, runtime_manifest=arguments.runtime_manifest, runtime_manifest_sha256=arguments.runtime_manifest_sha256, bootstrap=arguments.bootstrap).run()
    descriptor = os.open(output, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(descriptor, 'w') as stream:
        json.dump(result, stream, sort_keys=True, separators=(',', ':'))
        stream.write('\n')
    print(json.dumps({'outcome': result['outcome'], 'evidence': str(output), 'productionQualified': False, 'liveExecuted': False}))
    return code


if __name__ == '__main__':
    sys.exit(main())
