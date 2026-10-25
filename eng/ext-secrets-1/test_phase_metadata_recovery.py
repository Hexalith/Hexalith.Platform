"""Installed executor lookup uses only its pinned metadata mode; no normal owner command is launched."""
import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch, MagicMock
from joint_authority_service import InstalledPhaseExecutor
from joint_live_phase_runner import digest, partitions, Blocked

class MetadataRecoveryTests(unittest.TestCase):
    def test_preflight_refuses_missing_or_substituted_metadata_mode_and_lookup_only_runs_that_mode(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory=Path(temporary);artifact=directory/'fixture-owner';artifact.write_bytes(b'fixture-executable-never-directly-run')
            descriptor={'phase':0,'cases':partitions()[0],'command':[str(artifact),'--phase-command'],'recoveryCommand':[str(artifact),'--phase-finalize'],
                'workingDirectory':str(directory),'environment':{},'completionTimeoutSeconds':30,
                'artifacts':[{'path':str(artifact),'sha256':hashlib.sha256(artifact.read_bytes()).hexdigest()}]}
            binding={'phaseExecutionProfileSha256':{'0':digest(descriptor)}};descriptor['bindingDigest']=digest(binding)
            request={'phase':0,'requestId':digest('fixture-request'),'cases':partitions()[0]}
            class Observer:
                finalized=False
                def call(self,method,value):
                    if method=='preflight-phase':return {'missingInputs':[]}
                    return {'state':'Passed' if self.finalized else 'Unknown','requestSha256':digest(request),'bindingDigest':digest(binding),'passedCases':request['cases'] if self.finalized else []}
            observer=Observer();executor=InstalledPhaseExecutor(descriptor,observer,binding)
            with patch('case_driver_inventory.blockers',return_value=[]):self.assertEqual([],executor.preflight())
            for invalid in (None,[str(artifact),'--phase-command'],['foreign','--phase-finalize']):
                bad={**descriptor,'recoveryCommand':invalid};badbinding={'phaseExecutionProfileSha256':{'0':digest({k:v for k,v in bad.items() if k!='bindingDigest'})}}
                with patch('case_driver_inventory.blockers',return_value=[]):self.assertIn('PinnedMetadataOnlyPhaseFinalizer',InstalledPhaseExecutor(bad,observer,badbinding).preflight())
            process=MagicMock();process.communicate.side_effect=lambda *args,**kwargs:setattr(observer,'finalized',True)
            process.__enter__.return_value=process
            with patch('subprocess.Popen',return_value=process) as launch:
                recovered=executor.lookup(request)
            self.assertEqual('Passed',recovered[0]);self.assertEqual([str(artifact),'--phase-finalize'],launch.call_args.args[0]);self.assertEqual(1,launch.call_count)
            # Exact immutable observer result now exists; a second lookup starts no process at all.
            with patch('subprocess.Popen',side_effect=AssertionError('normal command must never run')):self.assertEqual(recovered,executor.lookup(request))
