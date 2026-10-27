"""Disposable real input/admission boundaries. No installed input or owner effect is used."""
import hashlib
import os
from pathlib import Path
import sqlite3
import tempfile
import threading
import time
import unittest
from concurrent.futures import ThreadPoolExecutor
from contextlib import closing
from unittest.mock import patch
import bounded_file_io as io
import qualify as q
import coordinated_packets as packets
from joint_live_phase_runner import run_installed_cohort, Blocked
import test_joint_live_phase_runner as runner_fixture
from test_joint_live_phase_runner import Owner, partitions
import test_coordinated_packets as packet_fixture


class PublicInputBoundaries(unittest.TestCase):
    def test_actual_public_reader_rejects_parent_links_types_size_change_and_private_inode(self):
        with tempfile.TemporaryDirectory() as root:
            root=Path(root);directory=root/'held';directory.mkdir();public=directory/'public';public.write_bytes(b'{"valid":true}')
            self.assertEqual({'valid':True},q.read_json(public))
            self.assertEqual(public.read_bytes(),q.bounded_public_file(os.path.relpath(public),100))
            alias=root/'alias';alias.symlink_to(directory,target_is_directory=True)
            for path in (alias/'public',directory,str(alias/'..'/'held'/'public')):
                with self.assertRaises((OSError,ValueError)):q.bounded_public_file(path,100)
            fifo=root/'fifo';os.mkfifo(fifo)
            with self.assertRaises(ValueError):q.bounded_public_file(fifo,100)
            with self.assertRaises(ValueError):q.bounded_public_file(public,2)
            info=public.stat()
            with self.assertRaisesRegex(ValueError,'configured credential'):q.bounded_public_file(public,100,forbidden_inodes={(info.st_dev,info.st_ino)})
            real_read=os.read;changed=False
            def growing(fd,size):
                nonlocal changed
                data=real_read(fd,size)
                if data and not changed:
                    changed=True;public.write_bytes(b'changed public content beyond original size')
                return data
            with patch.object(os,'read',side_effect=growing):
                with self.assertRaises(ValueError):q.read_json(public)

    def test_actual_admission_and_installed_owner_refuse_invalid_selected_paths_without_ports(self):
        with tempfile.TemporaryDirectory() as root:
            root=Path(root);real=root/'real';real.mkdir();(real/'input').write_bytes(b'{}');alias=root/'alias';alias.symlink_to(real,target_is_directory=True)
            fifo=root/'fifo';os.mkfifo(fifo);large=root/'large';large.write_bytes(b'x'*(1024*1024+1))
            for path in (alias/'input',fifo,large):
                self.assertTrue(packets.admission(path,real/'input','a'*40,True)[0])
                with patch('joint_live_phase_runner.DaprCohortRpc') as rpc:
                    with self.assertRaises(Blocked):run_installed_cohort(path,real/'input')
                    rpc.assert_not_called()

    def test_actual_public_acquisition_deadline_retains_bounded_worker_slots_until_cleanup(self):
        with tempfile.TemporaryDirectory() as root:
            path=Path(root)/'public';path.write_bytes(b'{}');release=threading.Event();all_entered=threading.Event();lock=threading.Lock();entered=0
            real_open=os.open;real_run=io.run
            def stalled(name,*args,**kwargs):
                nonlocal entered
                if name=='public':
                    with lock:
                        entered+=1
                        if entered==4:all_entered.set()
                    release.wait(3)
                return real_open(name,*args,**kwargs)
            try:
                with patch.object(os,'open',side_effect=stalled),ThreadPoolExecutor(max_workers=4) as pool:
                    calls=[pool.submit(lambda:real_run(lambda:q.read_json(path),timeout=.15)) for _ in range(4)]
                    self.assertTrue(all_entered.wait(1))
                    for call in calls:
                        with self.assertRaises(TimeoutError):call.result(1)
                    with self.assertRaises(TimeoutError):real_run(lambda:q.read_json(path),timeout=.03)
                    self.assertEqual(4,entered)
                    release.set()
                self.assertEqual({},real_run(lambda:q.read_json(path),timeout=1))
            finally:release.set()

    def test_actual_packet_and_installed_startup_bound_stalled_public_acquisition(self):
        with tempfile.TemporaryDirectory() as root:
            path=Path(root)/'public';path.write_bytes(b'{}');real_open=os.open;real_run=io.run
            for installed in (False,True):
                release=threading.Event();entered=threading.Event();finished=threading.Event()
                def stalled(name,*args,**kwargs):
                    if name=='public':
                        entered.set()
                        try:release.wait(2)
                        finally:finished.set()
                    return real_open(name,*args,**kwargs)
                try:
                    with patch.object(os,'open',side_effect=stalled),patch.object(io,'run',side_effect=lambda action,timeout=30:real_run(action,timeout=.05)),patch('joint_live_phase_runner.DaprCohortRpc') as rpc:
                        if installed:
                            with self.assertRaises(Blocked):run_installed_cohort(path,path)
                        else:self.assertEqual(['CoordinatedAdmissionInputDeadlineOrCapacity'],packets.admission(path,path,'a'*40,True)[0])
                        self.assertTrue(entered.is_set());self.assertFalse(finished.is_set());rpc.assert_not_called()
                        release.set();self.assertTrue(finished.wait(1))
                finally:release.set()

    def test_coordinated_admission_uses_one_total_input_budget(self):
        real_run=io.run
        with patch.object(io,'run',side_effect=lambda action,timeout=30:real_run(action,timeout=.03)),patch.object(packets,'_admission',side_effect=lambda *a:(time.sleep(.1),[],{},{})):
            missing,_,_=packets.admission('fixture','fixture','a'*40,True)
            self.assertEqual(['CoordinatedAdmissionInputDeadlineOrCapacity'],missing)
        # Allow the occupied worker to clean up; no attempt/owner was contacted.
        time.sleep(.11)


class AcceptedInputs(unittest.TestCase):
    def test_actual_complete_packet_admission_rejects_duplicate_sections_and_all_standard_or_binding_rows(self):
        fixture=packet_fixture.CoordinatedPacketTests();fixture.setUp()
        try:
            original=fixture.fixture.register.read_text()
            for name in ('TargetVersionOrCommit','TargetIntegrationDate','AcceptedStatus','InstallationAuthorityBinding'):
                row=next(line for line in original.splitlines() if line.startswith('| `'+name+'` |'))
                changed=original.replace('### EXT-HOST-1','### EXT-HOST-1',1)
                section=packets.section(changed,'EXT-HOST-1')
                fixture.fixture.register.write_text(changed.replace(section,section+row+'\n'))
                self.assertTrue(packets.admission(fixture.context_path,fixture.fixture.register,fixture.fixture.target,True)[0],name)
            fixture.fixture.register.write_text(original+'\n'+packets.section(original,'EXT-HOST-1'))
            self.assertTrue(packets.admission(fixture.context_path,fixture.fixture.register,fixture.fixture.target,True)[0])
            fixture.fixture.register.write_text(original)
            self.assertEqual([],packets.admission(fixture.context_path,fixture.fixture.register,fixture.fixture.target,True)[0])
        finally:fixture.doCleanups()

    def test_actual_packet_context_rejects_duplicate_root_and_nested_properties(self):
        fixture=packet_fixture.CoordinatedPacketTests();fixture.setUp()
        try:
            raw=fixture.context_path.read_text()
            for changed in ('{"sourceCommit":"'+fixture.fixture.target+'",'+raw[1:],raw.replace('"path":','"path":"/fixture-substitution","path":',1)):
                fixture.context_path.write_text(changed)
                self.assertEqual(['ValidExplicitCoordinatedInstallationContext'],packets.admission(fixture.context_path,fixture.fixture.register,fixture.fixture.target,True)[0])
            fixture.context_path.write_text(raw)
            self.assertEqual([],packets.admission(fixture.context_path,fixture.fixture.register,fixture.fixture.target,True)[0])
        finally:fixture.doCleanups()

    def test_receipt_expiry_winning_during_owner_verification_inserts_no_attempt(self):
        fixture=runner_fixture.JointRunnerTests();fixture.setUp()
        try:
            for boundary in (1001,1002):
                ready=[]
                with patch('time.time',return_value=1000):
                    for phase in range(4):ready.append(Owner().preflight(fixture.binding,phase,partitions()[phase])['readiness'])
                for receipt in ready:receipt['payload']['validUntil']=1001
                clock=[1000]
                def verify(*args):clock[0]=boundary;return True
                fixture.store.verify_owner=verify
                with patch('time.time',side_effect=lambda:clock[0]):
                    with self.assertRaises(Blocked):fixture.authority.reserve(fixture.binding,'isolated-expired',ready)
                with closing(sqlite3.connect(fixture.path)) as database:self.assertEqual(0,database.execute('select count(*) from attempts').fetchone()[0])
        finally:fixture.tearDown()
