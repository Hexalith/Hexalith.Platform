"""Only local owner entrypoint refusal paths execute; no authority transport, attempt reserve or owner phase runs."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

import qualify as q

spec=importlib.util.spec_from_file_location('host_uncertainty_verifier',Path(__file__).parents[1]/'ext-host-1/verify_full.py')
host=importlib.util.module_from_spec(spec);spec.loader.exec_module(host)


class OuterUncertaintyTests(unittest.TestCase):
    def test_both_actual_owner_entrypoints_preserve_unknown_for_missing_malformed_and_changed_checkout_inputs(self):
        for fault in ('missing','malformed','checkout'):
            with self.subTest(fault=fault), tempfile.TemporaryDirectory() as temp:
                directory=Path(temp);root=directory/'platform';root.mkdir();configuration=directory/'joint.json'
                profile=directory/'profile.json';profile.write_bytes(b'{"fixture":true}')
                if fault=='malformed': configuration.write_text('{')
                elif fault=='checkout': configuration.write_text(json.dumps({'binding':{'platformCommit':'a'*40,'profileSha256':hashlib.sha256(profile.read_bytes()).hexdigest()}}))
                args=['qualify.py','--root',str(root),'--caller',str(root),'--eventstore-argument','../eventstore','--artifacts-argument',str(directory/'artifacts'),'--evidence-argument',str(directory/'evidence')]
                with patch.dict(os.environ,{'PLATFORM_JOINT_COHORT_CONFIGURATION':str(configuration)}),patch.object(sys,'argv',args),patch('qualify.git',return_value='wrong-checkout'):
                    self.assertEqual(2,q.main())
                    self.assertEqual(1,host.verify(root,directory/'host',profile))
                secret=q.read_json(next((directory/'evidence').glob('attempt-*/live-evidence.json')))
                hosted=q.read_json(directory/'host/joint-cohort-failure.json')
                for report in (secret,hosted):
                    self.assertEqual('Unknown',report['qualificationEffects'])
                    self.assertEqual('Unknown',report['effectState'])
                    self.assertEqual('LookupRequired',report['attemptState'])
                    self.assertTrue(report['effectsStarted'])

    def test_both_owner_failure_documents_preserve_reservation_loss_uncertainty_and_candidate_identity(self):
        import joint_live_phase_runner as joint
        from test_joint_live_phase_runner import JointRunnerTests, PHASES
        fixture=JointRunnerTests();fixture.setUp()
        try:
            reserve=fixture.authority.reserve
            def lose(binding,attempt,ready):
                reserve(binding,"competing-fixture-reservation",ready)
                raise TimeoutError("fixture reserve acknowledgment lost")
            fixture.authority.reserve=lose
            try: fixture.runner().run()
            except joint.Blocked as error: failure=error
            else: self.fail("fixture reserve loss should be uncertain")
            with tempfile.TemporaryDirectory() as temp:
                directory=Path(temp);root=directory/"platform";root.mkdir();profile=directory/"profile.json";profile.write_bytes(b'{"fixture":true}')
                configuration=directory/"joint.json";configuration.write_text(json.dumps({"binding":{"profileSha256":hashlib.sha256(profile.read_bytes()).hexdigest()}}))
                args=["qualify.py","--root",str(root),"--caller",str(root),"--eventstore-argument","../eventstore","--artifacts-argument",str(directory/"artifacts"),"--evidence-argument",str(directory/"evidence")]
                with patch.dict(os.environ,{"PLATFORM_JOINT_COHORT_CONFIGURATION":str(configuration)}),patch.object(sys,"argv",args),patch.object(joint,"run_installed_cohort",side_effect=failure):
                    self.assertEqual(2,q.main());self.assertEqual(1,host.verify(root,directory/"host",profile))
                for report in (q.read_json(next((directory/"evidence").glob("attempt-*/live-evidence.json"))),q.read_json(directory/"host/joint-cohort-failure.json")):
                    self.assertEqual("Unknown",report["qualificationEffects"]);self.assertEqual("Unknown",report["effectState"])
                    self.assertEqual("ReservationUnknown",report["attemptState"]);self.assertEqual(failure.attempt_id,report["attemptId"])
                    self.assertTrue(report["effectsStarted"])
        finally: fixture.tearDown()


if __name__=='__main__': unittest.main()
