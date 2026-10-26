"""Disposable byte inventories only; fixture binaries are never executable qualification."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from execution_profile import validate_execution_inventory, validate_execution_profile


def fixture_profile(directory, phase=0, cases=None):
    directory=Path(directory);app=directory/'app';runtime=directory/'runtime';app.mkdir(parents=True);runtime.mkdir()
    dll=app/'Hexalith.Platform.Secrets.Qualification.Executor.dll';dll.write_bytes(b'MZfixture-only')
    (app/'Hexalith.Platform.Secrets.Qualification.Executor.deps.json').write_text('{}')
    (app/'Hexalith.Platform.Secrets.Qualification.Executor.runtimeconfig.json').write_text('{}')
    installation=app/'installation.json';installation.write_text('{}')
    launcher=runtime/'dotnet';launcher.write_bytes(b'\x7fELFfixture-only')
    return {'phase':phase,'cases':cases or [],'command':[str(launcher),str(dll),'--phase-command'],
        'recoveryCommand':[str(launcher),str(dll),'--phase-finalize'],'workingDirectory':str(app),
        'environment':{'PLATFORM_JOINT_PHASE_COMMAND_INSTALLATION':str(installation)},'completionTimeoutSeconds':30,
        'dependencyClosure':{'schema':'Platform.InstalledDotnetExecutionClosure.v1','entryAssembly':str(dll),
            'installationFile':str(installation),'directories':[str(app),str(runtime)],'configurationFiles':[str(installation)]},
        'artifacts':[{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for root in (app,runtime) for p in root.iterdir()]}


class ExecutionProfileTests(unittest.TestCase):
    def test_complete_census_rejects_loaded_dependency_and_configuration_changes_with_same_launcher(self):
        with tempfile.TemporaryDirectory() as temporary:
            profile=fixture_profile(temporary);self.assertEqual([],validate_execution_inventory(profile))
            for suffix in ('.dll','.deps.json','.runtimeconfig.json','installation.json'):
                path=next(Path(value['path']) for value in profile['artifacts'] if value['path'].endswith(suffix))
                before=path.read_bytes();path.write_bytes(before+b' ')
                self.assertTrue(validate_execution_inventory(profile),suffix);path.write_bytes(before)
            unknown=Path(profile['workingDirectory'])/'unexpected.dll';unknown.write_bytes(b'MZforeign')
            self.assertTrue(validate_execution_inventory(profile));unknown.unlink()
            for name in ('DOTNET_STARTUP_HOOKS','DOTNET_ADDITIONAL_DEPS','LD_PRELOAD','PYTHONPATH'):
                changed=deepcopy(profile);changed['environment'][name]='/foreign/code'
                self.assertTrue(validate_execution_inventory(changed))
            changed=deepcopy(profile);changed['dependencyClosure']['directories'].pop()
            self.assertTrue(validate_execution_inventory(changed))

    def test_full_profile_requires_independent_native_image_measurement_even_with_owner_completeness_flags(self):
        with tempfile.TemporaryDirectory() as temporary:
            profile=fixture_profile(temporary)
            self.assertEqual([],validate_execution_inventory(profile))
            expected=['External.IndependentlyVerifiedInstalledImageAndNativeLoaderMeasurementProtocol']
            self.assertEqual(expected,validate_execution_profile(profile))
            profile['dependencyClosure']['complete']=True;profile['dependencyClosure']['independentlyVerified']=True
            self.assertEqual(expected,validate_execution_profile(profile))

    def test_private_mounts_remain_current_authority_inputs_and_are_never_public_hash_artifacts(self):
        with tempfile.TemporaryDirectory() as temporary:
            profile=fixture_profile(temporary);private=Path(temporary)/'private-key.pem';private.write_text('fixture-private-mount')
            installation=Path(profile['dependencyClosure']['installationFile'])
            installation.write_text(json.dumps({'OwnerSigningKeyFile':str(private),'ReadCredentials':{'RsaPkcs8Base64File':str(private),'AssertionFiles':{'source':str(private)}}}))
            for value in profile['artifacts']:
                if value['path']==str(installation):value['sha256']=hashlib.sha256(installation.read_bytes()).hexdigest()
            self.assertEqual([],validate_execution_inventory(profile));private.write_text('fixture-rotated-under-independent-current-authority')
            self.assertEqual([],validate_execution_inventory(profile))
            profile['artifacts'].append({'path':str(private),'sha256':hashlib.sha256(private.read_bytes()).hexdigest()})
            self.assertTrue(validate_execution_inventory(profile))

    def test_binary_hashing_accepts_large_bounded_files_and_generic_loader_is_refused(self):
        with tempfile.TemporaryDirectory() as temporary:
            profile=fixture_profile(temporary);binary=Path(profile['dependencyClosure']['entryAssembly']);binary.write_bytes(b'MZ'+b'x'*(2*1024*1024))
            for value in profile['artifacts']:
                if value['path']==str(binary):value['sha256']=hashlib.sha256(binary.read_bytes()).hexdigest()
            self.assertEqual([],validate_execution_inventory(profile))
            profile['command']=['/usr/bin/python3',str(binary),'--phase-command']
            profile['recoveryCommand']=['/usr/bin/python3',str(binary),'--phase-finalize']
            self.assertTrue(validate_execution_inventory(profile))
