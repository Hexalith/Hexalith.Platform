from sqlite_storage_fixture import enrolled_storage
import uuid
"""Disposable pass13 receiving/acquisition fixtures only; no installed qualification."""
import base64
import copy
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import tempfile
import threading
import unittest
from unittest.mock import patch
from contextlib import closing
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
import bounded_file_io as io
import joint_authority_service as service
from joint_live_phase_runner import canonical, digest, partitions, PHASES, Blocked
import test_joint_live_phase_runner as runner_tests
from test_execution_profile import fixture_profile
from execution_profile import validate_execution_inventory


class PartitionAdmissionTests(unittest.TestCase):
    def setUp(self):
        runner_tests.JointRunnerTests.setUp(self)
        self.keys=[ec.generate_private_key(ec.SECP256R1()) for _ in PHASES]
        def verify(phase,envelope):
            try:
                self.keys[phase].public_key().verify(base64.b64decode(envelope['signature'],validate=True),canonical(envelope['payload']),ec.ECDSA(hashes.SHA256()))
                return True
            except Exception:return False
        self.store.verify_owner=verify
        self.authority.verify=lambda request,result:verify(request['phase'],result)
        def reserve(binding,attempt,readiness=None):
            ready=readiness or [self.owners[name].preflight(binding,phase,partitions()[phase])['readiness'] for phase,name in enumerate(PHASES)]
            return self.store.dispatch('reserve',{'binding':binding,'attemptId':str(uuid.uuid5(uuid.NAMESPACE_URL,attempt)) if len(attempt)!=36 else attempt,'ownerReadiness':ready})
        self.authority.reserve=reserve
        for phase,name in enumerate(PHASES):
            owner=self.owners[name];execute=owner.execute;preflight=owner.preflight
            def signed_execute(request,execute=execute,phase=phase):
                result=execute(request);result['signature']=self.sign(phase,result['payload'])['signature'];return result
            def signed_preflight(binding,number,cases,preflight=preflight,phase=phase):
                result=preflight(binding,number,cases);result['readiness']=self.sign(phase,result['readiness']['payload']);return result
            owner.execute=signed_execute;owner.preflight=signed_preflight
    def sign(self,phase,payload):
        return {'payload':payload,'signature':base64.b64encode(self.keys[phase].sign(canonical(payload),ec.ECDSA(hashes.SHA256()))).decode()}
    tearDown=runner_tests.JointRunnerTests.tearDown
    runner=runner_tests.JointRunnerTests.runner
    def test_signed_bad_publication_and_legacy_predecessor_never_admit_or_dispatch_successor(self):
        attempt=self.authority.reserve(self.binding,'fixture-case-partition')['attemptId']
        request={'protocol':'OQ-5.4-1.joint.v1','attemptId':attempt,'bindingDigest':digest(self.binding),'phase':0,'owner':PHASES[0],'previousSha256':digest(self.binding),'cases':partitions()[0]}
        request['requestId']=digest([attempt,request['bindingDigest'],0,request['previousSha256']]);self.authority.retain(request)
        base=self.sign(0,request|{'state':'Passed','passedCases':request['cases']})
        variants=[{'passedCases':[]},{'passedCases':request['cases']+['extra']},{'passedCases':request['cases']+request['cases'][:1]}, {'failedCases':[request['cases'][0]]},{'unknownCases':['extra']},{'missingCases':['extra']}]
        for changed in variants:
            bad=self.sign(0,base['payload']|changed)
            with self.assertRaises(Blocked):self.authority.publish(request,bad)
            with closing(sqlite3.connect(self.path)) as db, db: db.execute('INSERT INTO results VALUES(?,?)',(request['requestId'],canonical(bad)))
            following={'protocol':request['protocol'],'attemptId':attempt,'bindingDigest':request['bindingDigest'],'phase':1,'owner':PHASES[1],'previousSha256':digest(bad),'cases':partitions()[1]}
            following['requestId']=digest([attempt,request['bindingDigest'],1,digest(bad)])
            for method in ('retain','phase-admission'):
                with self.assertRaises(Blocked):self.store.dispatch(method,{'request':following})
            self.owners[PHASES[0]].originals[request['requestId']]=bad
            with self.assertRaises(Blocked):self.runner().run()
            self.assertEqual([],self.owners[PHASES[1]].executions)
            with closing(sqlite3.connect(self.path)) as db, db:db.execute('DELETE FROM results')
        self.authority.publish(request,base)
        self.owners[PHASES[0]].originals[request['requestId']]=base
        self.assertTrue(self.runner().run()['cohortPassed'])


class SelectedBoundaries(unittest.TestCase):
    def test_bounded_complete_directory_census_through_actual_inventory(self):
        with tempfile.TemporaryDirectory() as temp:
            profile=fixture_profile(temp);root=Path(profile['workingDirectory']);current=root
            for i in range(65):current=current/str(i);current.mkdir()
            self.assertTrue(validate_execution_inventory(profile))
            current.rmdir();self.assertEqual([],validate_execution_inventory(profile))
            self.assertEqual(set(x['path'] for x in profile['artifacts'] if str(root) in x['path']),io.census(str(root)))
            with patch.object(io.os,'scandir',wraps=io.os.scandir):
                with self.assertRaisesRegex(ValueError,'Entries'):io.census(str(root),maximum=4)
    def test_thread_start_failures_release_only_unstarted_slots(self):
        for _ in range(8):
            with patch.object(threading.Thread,'start',side_effect=RuntimeError('injected start failure')):
                with self.assertRaises(RuntimeError):io.run(lambda:True,.05)
        self.assertTrue(io.run(lambda:True,.1))
    def test_enrollment_checks_exactly_the_parsed_startup_snapshot(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);key=ec.generate_private_key(ec.SECP256R1());public=root/'public';private=root/'private';installation=root/'installation';database=root/'authority.sqlite'
            public.write_bytes(key.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo));private.write_bytes(key.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))
            config={'publicAnchorFile':str(public),'signingKeyFile':str(private),'databasePath':str(database),'role':'authority','scope':'enrolled'};raw=canonical(config);installation.write_bytes(raw)
            with closing(sqlite3.connect(database)) as db,db:
                db.execute('CREATE TABLE service_enrollment(role,public_anchor_sha256,profile_sha256)');db.execute('INSERT INTO service_enrollment VALUES(?,?,?)',('authority',hashlib.sha256(public.read_bytes()).hexdigest(),hashlib.sha256(raw).hexdigest()))
            config['storageEnrollment']=enrolled_storage(database).enrollment;raw=canonical(config);installation.write_bytes(raw)
            with closing(sqlite3.connect(database)) as db,db:db.execute('UPDATE service_enrollment SET profile_sha256=?',(hashlib.sha256(raw).hexdigest(),))
            real=io.read;reads=[]
            def read(path,maximum,*args,**kwargs):
                if str(path)==str(installation):
                    reads.append(path)
                    return canonical(config|{'scope':'substituted'}) if len(reads)==1 else raw
                return real(path,maximum,*args,**kwargs)
            with patch.object(io,'read',side_effect=read):
                with self.assertRaises(Blocked):service._load_installed_startup(str(installation))
            self.assertEqual(1,len(reads));self.assertEqual(config,service._load_installed_startup(str(installation))[0])
