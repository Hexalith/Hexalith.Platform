"""Actual HTTP/channel/grant admission with disposable enrolled SQLite. No native/cohort phase."""
import hashlib
import socket
import sqlite3
import threading
import time
import unittest
from contextlib import closing
from unittest.mock import patch
from bounded_cohort_http import make_server
from joint_authority_service import CohortHttpApplication
from joint_live_phase_runner import canonical
import test_joint_live_phase_runner as runner_fixture
from cryptography.hazmat.primitives.asymmetric import ec


class AuthenticationDeadlineTests(unittest.TestCase):
    def test_actual_channel_and_current_grant_acquisition_keep_deadline_and_worker_ownership(self):
        for blocked in ('channel','grant','admitted-native'):
            with self.subTest(blocked=blocked):
                fixture=runner_fixture.JointRunnerTests();fixture.setUp();self.addCleanup(fixture.tearDown)
                bearer='fixture-authentication';method='fixture-readonly'
                with closing(sqlite3.connect(fixture.path)) as db,db:
                    db.execute('INSERT INTO workload_grants VALUES(?,?,?,0)',(hashlib.sha256(bearer.encode()).hexdigest(),canonical([method]),int(time.time())+300))
                entered=threading.Event();release=threading.Event();dispatched=[]
                def stall():
                    entered.set()
                    if not release.wait(4):raise AssertionError('fixture release missing')
                def channel(*args):
                    if blocked=='channel':stall()
                    return 'fixture-channel'
                import joint_authority_service as service
                original=service.existing_database;opens=[]
                def grant(*args,**kwargs):
                    opens.append(True)
                    if blocked=='grant' and len(opens)==1:stall()
                    return original(*args,**kwargs)
                def dispatch(*args):
                    dispatched.append(True)
                    if blocked=='admitted-native':stall()
                    return {'fixture':'authenticated-read'}
                app=CohortHttpApplication(fixture.path,ec.generate_private_key(ec.SECP256R1()),dispatch,'file:/fixture-channel',storage=fixture.storage)
                body=canonical({'protocol':'OQ-5.4-1.joint.v1','requestId':'fixture-authentication','method':method,'payload':{}})
                request=b'POST /joint/v1/'+method.encode()+b' HTTP/1.0\r\nContent-Length: '+str(len(body)).encode()+b'\r\ndapr-api-token: fixture-channel\r\nAuthorization: Bearer '+bearer.encode()+b'\r\n\r\n'+body
                with patch('qualify.read_mounted_credential',channel),patch.object(service,'existing_database',grant),make_server(app,'127.0.0.1',0,acquisition_timeout=.12,max_connections=1) as server:
                    worker=threading.Thread(target=server.serve_forever,kwargs={'poll_interval':.01},daemon=True);worker.start()
                    try:
                        with socket.create_connection(server.server_address,timeout=1) as client:
                            client.settimeout(1);client.sendall(request);self.assertTrue(entered.wait(1));time.sleep(.16)
                            self.assertFalse(server.slots.acquire(blocking=False))
                            with socket.create_connection(server.server_address,timeout=1) as surplus:
                                surplus.sendall(request)
                                try:self.assertEqual(b'',surplus.recv(1))
                                except ConnectionResetError:pass
                            if blocked!='admitted-native':
                                started=time.monotonic()
                                try:self.assertEqual(b'',client.recv(1024))
                                except ConnectionResetError:pass
                                self.assertLess(time.monotonic()-started,.5)
                                self.assertEqual([],dispatched)
                            release.set()
                            if blocked=='admitted-native':self.assertIn(b'200 OK',client.recv(4096))
                        until=time.monotonic()+2
                        while server.deadlines and time.monotonic()<until:time.sleep(.01)
                        acquired=False
                        while time.monotonic()<until and not acquired:
                            acquired=server.slots.acquire(blocking=False)
                            if not acquired:time.sleep(.01)
                        self.assertEqual({},server.deadlines);self.assertTrue(acquired);server.slots.release()
                        self.assertEqual([True] if blocked=='admitted-native' else [],dispatched)
                        with socket.create_connection(server.server_address,timeout=1) as next_client:
                            next_client.sendall(request);self.assertIn(b'200 OK',next_client.recv(4096))
                    finally:release.set();server.shutdown();worker.join(1)
