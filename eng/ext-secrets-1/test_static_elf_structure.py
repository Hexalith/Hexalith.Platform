"""Structural fixtures are never executed and establish no installed kernel/loader closure."""
import copy
import hashlib
import os
import struct
import tempfile
import unittest
from unittest.mock import patch

import launch_inventory as launch
import test_launch_inventory as fixture_launch


class StaticElfStructureTests(unittest.TestCase):
    def test_actual_validator_and_accepted_graph_reject_incoherent_native_maps(self):
        faults=('null-only','no-entry','nonexec-entry','bss-entry','truncated','offset-range','file-range','memory-range','address-overflow','bad-alignment','alignment-mismatch','overlapping-loads')
        for fault in faults:
            with self.subTest(fault=fault):
                body=fixture_launch.valid_static_elf()
                if fault=='null-only':struct.pack_into('<I',body,64,0)
                elif fault=='no-entry':struct.pack_into('<Q',body,24,0)
                elif fault=='nonexec-entry':struct.pack_into('<I',body,68,4)
                elif fault=='bss-entry':struct.pack_into('<Q',body,96,120)
                elif fault=='truncated':body=body[:100]
                elif fault=='offset-range':struct.pack_into('<Q',body,72,2**64-1)
                elif fault=='file-range':struct.pack_into('<Q',body,96,130)
                elif fault=='memory-range':struct.pack_into('<Q',body,104,128)
                elif fault=='address-overflow':struct.pack_into('<Q',body,80,2**64-100)
                elif fault=='bad-alignment':struct.pack_into('<Q',body,112,3)
                elif fault=='alignment-mismatch':struct.pack_into('<Q',body,80,0x400001)
                else:
                    body.extend(bytes(56));struct.pack_into('<H',body,56,2);body[120:176]=body[64:120]
                fixture=fixture_launch.LaunchInventoryTests();fixture.setUp()
                try:
                    path=fixture.root/'agents'/'executable';path.write_bytes(body)
                    with open(path,'rb') as source,self.assertRaises(ValueError):launch._static_elf(source.fileno())
                    manifest=copy.deepcopy(fixture.manifest)
                    for graph in manifest['resources']:
                        if graph['root']==str(path.parent):
                            next(pin for pin in graph['files'] if pin['path']==path.name).update(bytes=len(body),sha256=hashlib.sha256(body).hexdigest())
                    with patch.object(launch,'_immutable',return_value=None):self.assertTrue(launch.blockers(manifest,fixture.profile))
                finally:fixture.doCleanups()
        with tempfile.TemporaryFile() as file:
            file.write(fixture_launch.valid_static_elf());file.flush();launch._static_elf(file.fileno())
