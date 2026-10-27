"""Disposable application construction with no production serving or enrollment."""
import threading
import time
import unittest
from unittest.mock import Mock,patch

import bounded_file_io as selected_io
import joint_authority_service as service


class CompleteStartupOwnerTests(unittest.TestCase):
    def test_role_specific_schema_observer_and_owner_construction_share_the_original_deadline(self):
        for role,stage in (('authority','CohortAuthorityStore'),('observer','PersistedPhaseObserver'),('owner','RetainedPhaseOwner')):
            with self.subTest(role=role):
                release=threading.Event();entered=threading.Event();slots=threading.BoundedSemaphore(1)
                config={'role':role,'ownerTrust':{},'authority':{},'observer':{},'observerProfile':{'ownerOriginalAnchorFile':'fixture-owner'},'binding':{},'databasePath':'fixture',
                        'phase':1,'executor':{},'daprEndpoint':'fixture','daprCredentialReference':'fixture','appChannelCredentialReference':'fixture'}
                def construct(*args,**kwargs):entered.set();release.wait();return Mock()
                with patch.object(selected_io,'_slots',slots),patch.object(service,'_load_installed_startup',return_value=(config,b'public',object())),patch('joint_live_phase_runner.DaprCohortRpc'),patch.object(service,'EnrolledSqliteStorage'),patch.object(service,'InstalledPhaseExecutor'),patch('phase_completion_pool.EnrolledPhaseCompletionPool'),patch('qualify.public_key_identity',side_effect=lambda raw:raw),patch.object(selected_io,'read',return_value=b'owner'),patch.object(service,stage,side_effect=construct),patch.object(service,'CohortHttpApplication') as app:
                    try:
                        with self.assertRaises(TimeoutError):selected_io.run(lambda:service._build_installed_application('fixture'),timeout=.05)
                        self.assertTrue(entered.is_set());self.assertFalse(slots.acquire(blocking=False));app.assert_not_called()
                    finally:release.set()
                    limit=time.monotonic()+2
                    while not slots.acquire(blocking=False):
                        self.assertLess(time.monotonic(),limit);time.sleep(.005)
                    slots.release();app.assert_not_called()

    def test_every_role_keeps_late_construction_in_one_total_owner_and_never_serves(self):
        for role in ('authority','observer','owner'):
            with self.subTest(role=role):
                released=threading.Event();entered=threading.Event();done=threading.Event();slots=threading.BoundedSemaphore(1)
                config={'role':role,'ownerTrust':{'0':{}},'daprEndpoint':'fixture','daprCredentialReference':'fixture'}
                def trust(*args):
                    entered.set();released.wait();selected_io.check();done.set()
                app=Mock()
                with patch.object(selected_io,'_slots',slots),patch.object(service,'_load_installed_startup',return_value=(config,b'public',object())) as loaded,patch('joint_live_phase_runner.DaprCohortRpc',side_effect=trust),patch.object(service,'CohortHttpApplication',app):
                    try:
                        with self.assertRaises(TimeoutError):selected_io.run(lambda:service._build_installed_application('fixture'),timeout=.05)
                        self.assertTrue(entered.is_set());loaded.assert_called_once_with('fixture');app.assert_not_called();self.assertFalse(slots.acquire(blocking=False))
                    finally:released.set()
                    limit=time.monotonic()+2
                    while not slots.acquire(blocking=False):
                        self.assertLess(time.monotonic(),limit);time.sleep(.005)
                    slots.release();self.assertFalse(done.is_set())

    def test_actual_main_uses_one_captured_profile_and_enters_serve_outside_acquisition(self):
        config={'role':'authority','ownerTrust':{},'databasePath':'fixture','appChannelCredentialReference':'fixture','listenHost':'127.0.0.1','listenPort':1}
        app=Mock();observed=[]
        app.serve.side_effect=lambda *args:observed.append(selected_io._deadline.get())
        with patch('sys.argv',['service','--installation','fixture']),patch.object(service,'_load_installed_startup',return_value=(config,b'public',object())) as loaded,patch.object(service,'CohortAuthorityStore') as store,patch.object(service,'EnrolledSqliteStorage'),patch.object(service,'CohortHttpApplication',return_value=app) as constructed:
            store.side_effect=lambda *args,**kwargs:(self.assertIsNotNone(selected_io._deadline.get()) or Mock())
            service.main();loaded.assert_called_once_with('fixture');constructed.assert_called_once();app.serve.assert_called_once_with('127.0.0.1',1)
        self.assertEqual([None],observed)

    def test_failure_after_profile_decode_does_not_construct_or_serve_http(self):
        config={'role':'authority','ownerTrust':{},'databasePath':'fixture'}
        with patch('sys.argv',['service','--installation','fixture']),patch.object(service,'_load_installed_startup',return_value=(config,b'public',object())),patch.object(service,'EnrolledSqliteStorage',side_effect=ValueError('fixture missing enrolled schema')),patch.object(service,'CohortHttpApplication') as app:
            with self.assertRaises(ValueError):service.main()
            app.assert_not_called()
