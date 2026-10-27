"""Canonical provisioning and actual owner execution fixtures; no installed authority or qualification."""
import copy
import sqlite3
import tempfile
import unittest
from contextlib import closing
from pathlib import Path
from sqlite_storage_fixture import enrolled_storage
from test_qualify import Fixture
import qualify as q


class ProvisioningTests(unittest.TestCase):
    def test_canonical_schema_initializes_only_the_installer_row_and_runtime_requires_exact_singleton(self):
        with tempfile.TemporaryDirectory() as root:
            file=Path(root)/'authority.sqlite'
            with closing(sqlite3.connect(file)) as database:
                database.executescript(Path(__file__).with_name('joint-authority-schema.sql').read_text())
                self.assertEqual([],database.execute('SELECT * FROM database_storage_enrollment').fetchall())
            storage=enrolled_storage(file)
            with closing(storage.connect()) as database:self.assertEqual([(1,storage.enrollment['identity'])],database.execute('SELECT * FROM database_storage_enrollment').fetchall())
            with closing(sqlite3.connect(file)) as database,database:
                for row in ((2,'foreign'),(1,''),(1,' '),(1,None)):
                    with self.assertRaises(sqlite3.IntegrityError):database.execute('INSERT OR REPLACE INTO database_storage_enrollment VALUES(?,?)',row)
                database.execute('DELETE FROM database_storage_enrollment')
            with self.assertRaises(ValueError):storage.connect()
            with closing(sqlite3.connect(file)) as database,database:database.execute('INSERT INTO database_storage_enrollment VALUES(1,?)',('different',))
            with self.assertRaises(ValueError):storage.connect()
            with closing(sqlite3.connect(file)) as database,database:
                database.execute('DROP TABLE database_storage_enrollment');database.execute('CREATE TABLE database_storage_enrollment(slot,identity)')
                database.executemany('INSERT INTO database_storage_enrollment VALUES(?,?)',[(1,storage.enrollment['identity']),(2,'foreign')])
            with self.assertRaises(ValueError):storage.connect()


class PhasePermissionTests(unittest.TestCase):
    def test_actual_execute_acceptance_permission_is_exclusive_to_complete_unpartitioned_path(self):
        for selection in (None,[],[q.plan()[0].id],[case.id for case in q.plan()]):
            with self.subTest(selection='full-owner' if selection is None else len(selection)),tempfile.TemporaryDirectory() as root:
                fixture=Fixture(Path(root));result=q.execute(fixture.config,fixture.target,fixture.binding,fixture,case_ids=selection)
                self.assertEqual(selection is None,result['permitsOwnerAcceptanceForExactTarget'])
                self.assertEqual('QualificationPassed' if selection is None else 'PhaseQualificationPassed',result['code'])
                self.assertFalse(result['dependencyAvailable'])
