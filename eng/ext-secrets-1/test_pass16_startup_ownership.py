import threading
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path
import hashlib
import bounded_file_io as selected_io
import joint_live_phase_runner as runner
from test_qualify import Fixture


class StartupAcknowledgementOwnershipTests(unittest.TestCase):
    def available(self,slots):
        count=0
        while slots.acquire(blocking=False):count+=1
        for _ in range(count):slots.release()
        return count

    def test_selected_file_prestart_lost_launch_ack_and_completed_race_keep_exact_capacity(self):
        for mode in ('unstarted','launched','completed'):
            with self.subTest(mode=mode):
                slots=threading.BoundedSemaphore(4);release=threading.Event();done=threading.Event();launched=[]
                start=threading.Thread.start
                def action():
                    if mode=='launched':release.wait(2)
                    done.set();return 'retained'
                def fail(worker):
                    if mode!='unstarted':
                        launched.append(worker);start(worker)
                        if mode=='completed':self.assertTrue(done.wait(1));worker.join(1)
                    raise RuntimeError('isolated lost startup acknowledgment')
                try:
                    with patch.object(selected_io,'_slots',slots),patch.object(threading.Thread,'start',fail):
                        with self.assertRaises(RuntimeError):selected_io.run(action)
                    self.assertEqual(3 if mode=='launched' else 4,self.available(slots))
                    # The stalled original plus three charged actual workers saturate four slots.
                    if mode=='launched':
                        held=[]
                        for _ in range(3):self.assertTrue(slots.acquire(blocking=False));held.append(True)
                        self.assertFalse(slots.acquire(blocking=False))
                        for _ in held:slots.release()
                finally:
                    release.set()
                    for worker in launched:worker.join(2)
                self.assertEqual(4,self.available(slots))
                with patch.object(selected_io,'_slots',slots):self.assertEqual('recovered',selected_io.run(lambda:'recovered'))

    def test_actual_rpc_prestart_lost_launch_ack_and_completed_race_keep_connection_and_charge(self):
        for mode in ('unstarted','launched','completed'):
            with self.subTest(mode=mode),tempfile.TemporaryDirectory() as directory:
                fixture=Fixture(directory);anchor=fixture.config['qualification']['executor']['publicAnchorFile']
                descriptor={'appId':'isolated-owner','credentialReference':'file:/fixture','publicAnchorFile':anchor,'publicAnchorSha256':hashlib.sha256(Path(anchor).read_bytes()).hexdigest()}
                rpc=runner.DaprCohortRpc(descriptor,'http://127.0.0.1:3500','file:/fixture')
                slots=threading.BoundedSemaphore(4);release=threading.Event();entered=threading.Event();closed=[];launched=[];start=threading.Thread.start
                class Connection:
                    sock=None
                    def __init__(self,*a,**kw):pass
                    def close(self):closed.append(True)
                    def connect(self):raise OSError('isolated completed transport')
                def mounted(*args):
                    entered.set()
                    if mode=='launched':release.wait(2)
                    return 'fixture'
                def fail(worker):
                    if mode!='unstarted':
                        launched.append(worker);start(worker);self.assertTrue(entered.wait(1))
                        if mode=='completed':worker.join(1);self.assertFalse(worker.is_alive())
                    raise RuntimeError('isolated lost startup acknowledgment')
                try:
                    with patch.object(runner,'_RPC_WORKERS',slots),patch('http.client.HTTPConnection',Connection),patch('qualify.read_mounted_credential',side_effect=mounted),patch.object(threading.Thread,'start',fail):
                        with self.assertRaises(RuntimeError):rpc.call('lookup',{'fixtureOnly':True})
                    self.assertEqual(3 if mode=='launched' else 4,self.available(slots))
                    self.assertEqual(0 if mode=='launched' else 1,len(closed))
                finally:
                    release.set()
                    for worker in launched:worker.join(2)
                self.assertEqual(4,self.available(slots));self.assertEqual(1,len(closed))
                with patch.object(runner,'_RPC_WORKERS',slots),patch('http.client.HTTPConnection',Connection),patch('qualify.read_mounted_credential',return_value='fixture'):
                    with self.assertRaises(OSError):rpc.call('lookup',{'fixtureOnly':True})
                self.assertEqual(4,self.available(slots))

    def test_os_launch_before_python_bootstrap_acknowledgement_retains_both_actual_paths(self):
        native_start=threading._start_joinable_thread;Thread=threading.Thread
        class LostAcknowledgement(RuntimeError):pass
        class InterruptedEvent:
            def __init__(self,event):self.event=event
            def is_set(self):return self.event.is_set()
            def set(self):return self.event.set()
            def wait(self,timeout=None):raise LostAcknowledgement('OS launch precedes Python bootstrap')
        for path in ('file','rpc'):
            with self.subTest(path=path),tempfile.TemporaryDirectory() as directory:
                fixture=Fixture(directory);anchor=fixture.config['qualification']['executor']['publicAnchorFile'];descriptor={'appId':'isolated-owner','credentialReference':'file:/fixture','publicAnchorFile':anchor,'publicAnchorSha256':hashlib.sha256(Path(anchor).read_bytes()).hexdigest()}
                rpc=runner.DaprCohortRpc(descriptor,'http://127.0.0.1:3500','file:/fixture');slots=threading.BoundedSemaphore(4);bootstrap=threading.Event();release=threading.Event();entered=threading.Event();created=[];closed=[]
                def delayed(function,**kwargs):
                    def run():
                        self.assertTrue(bootstrap.wait(2));function()
                    return native_start(run,**kwargs)
                def make(*args,**kwargs):
                    thread=Thread(*args,**kwargs);thread._started=InterruptedEvent(thread._started);created.append(thread);return thread
                def action():entered.set();self.assertTrue(release.wait(2))
                class Connection:
                    sock=None
                    def __init__(self,*args,**kwargs):pass
                    def close(self):closed.append(True)
                try:
                    with patch.object(threading,'_start_joinable_thread',delayed),patch.object(threading,'Thread',make),patch.object(selected_io,'_slots',slots),patch.object(runner,'_RPC_WORKERS',slots),patch('http.client.HTTPConnection',Connection),patch('qualify.read_mounted_credential',side_effect=AssertionError('cancelled RPC cannot read credentials')):
                        with self.assertRaises(LostAcknowledgement):
                            if path=='file':selected_io.run(action,timeout=2)
                            else:rpc.call('lookup',{'fixtureOnly':True})
                        self.assertIsNone(created[0].ident);self.assertEqual(3,self.available(slots));self.assertEqual([],closed)
                        bootstrap.set();self.assertTrue(created[0]._started.event.wait(1))
                        if path=='file':self.assertTrue(entered.wait(1));self.assertEqual(3,self.available(slots))
                        release.set();created[0].join(2);self.assertFalse(created[0].is_alive())
                    self.assertEqual(4,self.available(slots))
                    if path=='rpc':self.assertEqual(1,len(closed))
                finally:
                    bootstrap.set();release.set()
                    for thread in created:
                        if thread.ident is not None:thread.join(2)
