"""Actual loopback child cleanup and closed command input checks."""
from copy import deepcopy
import importlib.util
import os
from pathlib import Path
import socket
import subprocess
import unittest

spec=importlib.util.spec_from_file_location('isolation',Path(__file__).with_name('telemetry_isolation.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)


class Probe(unittest.TestCase):
    def test_real_loopback_positive_and_negative_leave_no_listener_or_child_group(self):
        with socket.socket() as sock:
            sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
        child=subprocess.Popen(m.argv([{'name':'synthetic-closed','address':'127.0.0.1','port':port}]),
                               stdout=subprocess.PIPE,stderr=subprocess.PIPE,start_new_session=True)
        try:
            out,err=child.communicate(timeout=60)
            self.assertEqual(child.returncode,0,err.decode(errors='replace'))
            self.assertEqual(dict(line.split('|') for line in out.decode().splitlines()),
                             {'control-loopback':'open','synthetic-closed':'blocked'})
            with socket.socket() as check:check.bind(('127.0.0.1',15443))
            with self.assertRaises(ProcessLookupError):os.killpg(child.pid,0)
        finally:
            if child.poll() is None:os.killpg(child.pid,9);child.wait(5)
            child.stdout.close();child.stderr.close()

    def test_peer_injection_and_nonliteral_or_oversized_scope_refuse_without_dispatch(self):
        valid=[{'name':'synthetic','address':'192.0.2.1','port':5432}]
        for key,bad in [('name','test;arbitrary'),('address','192.0.2.1;arbitrary'),
                        ('address','example.invalid'),('address','::1'),('port',True),('port',65536)]:
            value=deepcopy(valid);value[0][key]=bad
            with self.subTest(key=key,bad=bad),self.assertRaises(ValueError):m.argv(value)
        for value in ([],valid*17,None):
            with self.assertRaises(ValueError):m.argv(value)

    def test_unexpected_existing_loopback_listener_refuses_without_removing_it(self):
        with socket.socket() as sock:
            sock.bind(('127.0.0.1',15443));sock.listen()
            result=subprocess.run(m.argv([{'name':'synthetic','address':'127.0.0.1','port':1}]),
                                  capture_output=True,timeout=5)
            self.assertEqual(result.returncode,63)
            self.assertEqual(sock.getsockname()[1],15443)

if __name__=='__main__':unittest.main()
