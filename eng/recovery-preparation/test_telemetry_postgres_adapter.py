#!/usr/bin/env python3
"""Synthetic PostgreSQL proposal/adversarial tests; no PG or backend process."""

from contextlib import redirect_stdout
from copy import deepcopy
import importlib.util
import io
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import unittest
from unittest.mock import patch


SOURCE = Path(__file__).absolute().with_name('telemetry_postgres_adapter.py')
spec = importlib.util.spec_from_file_location('telemetry_postgres_under_test', SOURCE)
a = importlib.util.module_from_spec(spec)
spec.loader.exec_module(a)
H, J, K = 'a' * 64, 'b' * 64, 'c' * 64


def resource(kind, name, uid, namespace='', rv='11'):
    return {'apiVersion': a.RESOURCE_TYPES[kind][0], 'kind': kind, 'namespace': namespace,
            'name': name, 'uid': uid, 'resourceVersion': rv}


def repin(value):
    value['sourcePinSha256'] = a.digest(a.canonical(value['source']))
    return value


def fixture():
    source = {
        'cluster': {'uid': 'synthetic-cluster-uid', 'endpoint': 'https://192.0.2.10:6443', 'caSha256': H},
        'namespace': resource('Namespace', 'synthetic-source', 'source-namespace-uid'),
        'statefulSet': resource('StatefulSet', 'synthetic-postgres', 'source-sts-uid', 'synthetic-source'),
        'pod': resource('Pod', 'synthetic-postgres-0', 'source-pod-uid', 'synthetic-source'),
        'container': 'postgres', 'image': 'example.invalid/postgres@sha256:' + H,
        'imageId': 'example.invalid/postgres@sha256:' + H, 'containerId': 'containerd://' + H,
        'restartCount': 0,
        'pvc': resource('PersistentVolumeClaim', 'synthetic-source-data', 'source-pvc-uid', 'synthetic-source'),
        'pv': resource('PersistentVolume', 'synthetic-source-volume', 'source-pv-uid'),
        'storageClass': resource('StorageClass', 'synthetic-delete-class', 'storage-class-uid'),
        'volumePath': {'pvUid': 'source-pv-uid', 'nodeUid': 'source-node-uid',
                       'path': '/var/openebs/local/source-data'},
        'database': 'synthetic_telemetry', 'user': 'synthetic_admin', 'peerOsUser': 'postgres',
        'socket': '/var/run/postgresql', 'port': 5432,
        'systemIdentifier': '1234567890123456789', 'timeline': 1, 'serverVersionNum': 180001,
        'schemas': ['public', 'synthetic_telemetry'],
        'tables': [{'schema': 'synthetic_telemetry', 'name': 'access_events'},
                   {'schema': 'synthetic_telemetry', 'name': 'access_rollups'}],
        'extensions': [{'name': 'plpgsql', 'schema': 'pg_catalog', 'version': '1.0'}],
        'roles': [{'name': 'postgres', 'canLogin': True, 'superuser': True},
                  {'name': 'synthetic_admin', 'canLogin': True, 'superuser': False},
                  {'name': 'pg_monitor', 'canLogin': False, 'superuser': False}],
        'configuration': [{'name': 'synthetic.conf', 'sha256': K, 'size': 200}],
        'tools': {key: {'path': '/usr/local/bin/' + key, 'sha256': H}
                  for key in ('psql', 'pg_dump', 'pg_restore')},
    }
    return repin({
        'kind': 'hexalith.telemetry-postgresql-proposal-input', 'schemaVersion': 1,
        'observedAt': '2026-10-23T12:00:00Z', 'parentProposalSha256': H, 'retainedDecisionsSha256': J,
        'evidence': {key: H for key in ('observationSha256', 'metadataSha256', 'storageSha256',
                                       'toolHashesSha256', 'identitySha256')},
        'source': source, 'sourcePinSha256': None,
        'worker': {'resource': resource('Deployment', 'synthetic-worker', 'worker-uid', 'synthetic-source'),
                   'previousReplicas': 1},
        'clock': {'resource': resource('Deployment', 'synthetic-clock', 'clock-uid', 'synthetic-source'),
                  'previousReplicas': 0},
        'target': {
            'cluster': deepcopy(source['cluster']),
            'namespace': resource('Namespace', 'synthetic-isolated', None, rv=None),
            'statefulSetName': 'synthetic-isolated-postgres', 'pvcName': 'synthetic-isolated-data',
            'secretName': 'synthetic-bootstrap', 'networkPolicyName': 'default-deny', 'container': 'postgres',
            'storageClass': {'resource': deepcopy(source['storageClass']), 'reclaimPolicy': 'Delete'},
            'storage': '1Gi', 'runAsUser': 999, 'runAsGroup': 999, 'fsGroup': 999,
            'resources': {'requests': {'cpu': '100m', 'memory': '128Mi'},
                          'limits': {'cpu': '1', 'memory': '512Mi'}},
            'socket': '/run/postgresql', 'port': 5432, 'bootstrapUser': 'postgres',
        },
        'transfer': {'store': 'synthetic-store', 'key': 'memories/synthetic/telemetry.dump.enc',
                     'writerPrincipal': 'synthetic-writer', 'validatorPrincipal': 'synthetic-validator',
                     'recipientSha256': K, 'retentionDays': 30, 'maximumProofAgeSeconds': 86400},
    })


def snapshot_binding(value):
    return {'proposalInputSha256': a.digest(a.canonical(value)), 'sourcePinSha256': value['sourcePinSha256'],
            'snapshotId': '00000003-0000001B-1', 'holderBackendPid': 1234, 'holderTransactionOpen': True,
            'preSnapshotTableCensus': deepcopy(value['source']['tables']),
            'snapshotTableCensus': deepcopy(value['source']['tables']),
            **{key: value['source'][key] for key in ('systemIdentifier', 'timeline', 'serverVersionNum')}}


def restore_binding(value):
    ns = value['target']['namespace']['name']
    return {
        'proposalInputSha256': a.digest(a.canonical(value)),
        'namespace': resource('Namespace', ns, 'target-namespace-uid'),
        'statefulSet': resource('StatefulSet', value['target']['statefulSetName'], 'target-sts-uid', ns),
        'pod': resource('Pod', value['target']['statefulSetName'] + '-0', 'target-pod-uid', ns),
        'pvc': resource('PersistentVolumeClaim', value['target']['pvcName'], 'target-pvc-uid', ns),
        'pv': resource('PersistentVolume', 'target-volume', 'target-pv-uid'),
        'volumePath': {'pvUid': 'target-pv-uid', 'nodeUid': 'target-node-uid',
                       'path': '/var/openebs/local/isolated-data'},
        'image': value['source']['image'], 'imageId': value['source']['imageId'],
        'container': value['target']['container'], 'containerId': 'containerd://' + K,
        'pvcVolumeName': 'target-volume', 'pvClaimUid': 'target-pvc-uid',
        'pvClaimName': value['target']['pvcName'], 'pvClaimNamespace': ns,
        'storageClassUid': value['target']['storageClass']['resource']['uid'], 'reclaimPolicy': 'Delete',
        'emptySchema': {key: [] for key in ('tables', 'sequences', 'functions', 'types', 'nondefaultSchemas')},
        'artifact': {'store': value['transfer']['store'], 'key': value['transfer']['key'],
                     'version': 'exact-immutable-version-1', 'sha256': H, 'size': 20,
                     'retainUntil': '2026-12-01T00:00:00Z'},
        'independentlyReadBack': {'store': value['transfer']['store'], 'key': value['transfer']['key'],
                                  'version': 'exact-immutable-version-1', 'sha256': H, 'size': 20,
                                  'validatorPrincipal': value['transfer']['validatorPrincipal'],
                                  'retentionMode': 'COMPLIANCE', 'retainUntil': '2026-12-01T00:00:00Z',
                                  'encryption': 'AES256'},
        'authenticatedCaptureRecord': {'recordSha256': K, 'artifactVersion': 'exact-immutable-version-1',
                                       'artifactSha256': H, 'plaintextSha256': J, 'plaintextSize': 40,
                                       'capturedAt': '2026-10-23T12:00:01Z', 'expiresAt': '2026-10-24T12:00:01Z'},
        'decryptedStdinSha256': J, 'decryptedStdinSize': 40, 'isolationAssessmentSha256': K,
    }


def database_semantics():
    return {'encoding': 'UTF8', 'localeProvider': 'c', 'collate': 'C', 'ctype': 'C', 'locale': None,
            'icuRules': None, 'collationVersion': None, 'actualCollationVersion': None, 'tablespace': 'pg_default'}


def default_collation():
    return {'schema': 'pg_catalog', 'name': 'default', 'provider': 'd', 'encoding': None, 'collate': None,
            'ctype': None, 'locale': None, 'icuRules': None, 'version': None, 'actualVersion': None,
            'deterministic': True}


def native_relation(relation, kind='r', owner='synthetic_admin', acl=None):
    return {**relation, 'kind': kind, 'owner': owner, 'acl': acl, 'persistence': 'p',
            'accessMethod': {'r': 'heap', 'i': 'btree', 'S': None}[kind],
            'options': None, 'toastOptions': None, 'tablespace': None}


def native_column(relation, position, column, typ='bigint', **changes):
    return {**relation, 'position': position, 'column': column, 'type': typ, 'notNull': False,
            'identity': '', 'generated': '', 'default': None, 'acl': None,
            'collation': default_collation() if typ == 'text' else None, 'storage': 'x' if typ == 'text' else 'p',
            'compression': '', 'statisticsTarget': None, 'options': None, 'dimensions': 0, **changes}


def slot(ordinal, position, column, index=False, expression=False, included=False):
    result = {'ordinal': ordinal, 'position': position, 'column': column, 'expression': expression}
    if index:
        result['included'] = included
    return result


def dependency_reference(table, position, column):
    return {**table, 'position': position, 'column': column}


def native_constraint(table, name, kind, columns, definition, dependencies=None, **changes):
    dependencies = [] if dependencies is None else dependencies
    return {**table, 'constraint': name, 'validated': True, 'definition': definition, 'kind': kind,
            'enforced': True, 'deferrable': False, 'initiallyDeferred': False, 'period': False,
            'columnCount': len(columns), 'columns': columns, 'referenceColumnCount': 0, 'referencedColumns': [],
            'referencedTable': None, 'supportingIndex': None, 'dependencyReferences': dependencies,
            'dependencyReferenceCount': len(dependencies), **changes}


def canary_records(value, target=False, sequences=True):
    """Synthetic structured observations, with explicit empty categories."""
    s = value['source']
    tables = deepcopy(s['tables'])
    state = [{'schema': s['schemas'][1], 'name': 'seq|quoted"line\nname',
              'lastValue': '42', 'isCalled': False}] if sequences else []
    census = [{'schema': row['schema'], 'name': row['name']} for row in state]
    roles = deepcopy(s['roles'])
    for role in roles:
        role.update({flag: role['name'] == 'postgres' for flag in a.ROLE_FLAGS
                     if flag not in ('canLogin', 'superuser')})
        role['inherit'] = True
        if role['name'] == s['user']:
            role.update(createDb=True, createRole=True)
        if target and role['name'] != 'postgres' and not role['name'].startswith('pg_'):
            role.update({flag: False for flag in a.ROLE_FLAGS})
    relations = [native_relation(table, owner=s['user'], acl=['a|b\nc=d/u']) for table in tables]
    columns = [native_column(table, position, column, 'text' if position == 1 else 'bigint')
               for table in tables for position, column in ((1, 'body|line\nname'), (2, 'id'))]
    if sequences:
        relations.append(native_relation(census[0], 'S', s['user']))
        columns.extend(native_column(census[0], position, column, typ, notNull=True)
                       for position, column, typ in ((1, 'last_value', 'bigint'), (2, 'log_cnt', 'bigint'),
                                                     (3, 'is_called', 'boolean')))
    return {
        'sessionIdentity': {'database': s['database'], 'user': 'postgres' if target else s['user'],
                            'serverVersionNum': s['serverVersionNum']},
        'tableCensus': tables,
        'schema': {
            'database': database_semantics(),
            'namespaces': [{'schema': name, 'owner': s['user'], 'acl': None} for name in s['schemas']],
            'relations': relations,
            'columns': columns,
        },
        'dataCounts': [{**table, 'rows': 2} for table in tables],
        'dataContents': [{**table, 'rows': 2, 'sha256': H, 'profileAccepted': True,
                          'rowBytesMaximum': 64, 'contentBytes': 128} for table in tables],
        'constraints': [native_constraint(tables[0], 'check|line\nname', 'c',
            [slot(1, 1, 'body|line\nname')], "CHECK (body <> 'first|second\nthird')",
            [dependency_reference(tables[0], 1, 'body|line\nname')])],
        'indexes': [],
        'sequences': {'definitions': [{**census[0], 'owner': s['user'], 'type': 'bigint', 'start': '1',
                                       'minimum': '1', 'maximum': '9223372036854775807', 'increment': '1',
                                       'cycle': False, 'cache': '1'}] if sequences else [],
                      'ownership': [{**census[0], 'tableSchema': tables[0]['schema'],
                                     'tableName': tables[0]['name'], 'column': 'id', 'dependency': 'a'}]
                                    if sequences else []},
        'sequenceCensus': census, 'sequenceStateBefore': deepcopy(state), 'sequenceStateAfter': deepcopy(state),
        'extensions': deepcopy(s['extensions']),
        'acls': [{'owner': s['user'], 'schema': None, 'type': 'r', 'acl': ['a|b\nc=d/u']}],
        'roleIsolation': {'roles': roles, 'memberships': []},
        'unsupportedObjects': {key: [] for key in a.UNSUPPORTED_CATEGORIES},
        'databaseActivation': {'database': s['database'], 'owner': 'postgres' if target else s['user'], 'acl': None},
    }


def canary_bytes(records):
    return b''.join(a.canonical({'category': key, 'payload': value}) + b'\n' for key, value in records.items())


def add_index(records, table_index=0, index_name='synthetic_access_index'):
    table = records['tableCensus'][table_index]
    index = {**table, 'name': index_name}
    records['schema']['relations'].append(native_relation(index, 'i'))
    records['schema']['columns'].append(native_column(index, 1, 'id'))
    records['indexes'].append({**table, 'index': index['name'],
        'definition': 'CREATE INDEX ' + index_name + ' ON synthetic_telemetry.' + table['name'] + ' USING btree (id)',
        'columnCount': 1, 'keyColumnCount': 1, 'columns': [slot(1, 2, 'id', True)],
        'dependencyReferences': [dependency_reference(table, 2, 'id')], 'dependencyReferenceCount': 1,
        'expressions': None, 'predicate': None})
    return records


def add_expression_index(records):
    add_index(records)
    index = records['indexes'][-1]
    table = records['tableCensus'][0]
    index.update(columns=[slot(1, 0, None, True, expression=True)], expressions='lower(body)',
                 predicate='body IS NOT NULL', dependencyReferences=[dependency_reference(table, 1, 'body|line\nname')])
    index_column = records['schema']['columns'][-1]
    index_column.update(column='lower', type='text', storage='x', collation=default_collation())
    return records


def add_foreign_key(records):
    add_index(records, 1, 'synthetic_parent_unique')
    table, referenced = records['tableCensus']
    supporting = {'schema': referenced['schema'], 'name': 'synthetic_parent_unique'}
    records['constraints'].append(native_constraint(referenced, 'parent_unique', 'u',
        [slot(1, 2, 'id')], 'UNIQUE (id)', [dependency_reference(referenced, 2, 'id')], supportingIndex=supporting))
    records['constraints'].append(native_constraint(table, 'child_foreign_key', 'f', [slot(1, 2, 'id')],
        'FOREIGN KEY (id) REFERENCES synthetic_telemetry.access_rollups(id)',
        [dependency_reference(table, 2, 'id'), dependency_reference(referenced, 2, 'id')],
        referenceColumnCount=1, referencedColumns=[slot(1, 2, 'id')], referencedTable=referenced, supportingIndex=supporting))
    return records


def with_identity(records):
    table = records['tableCensus'][0]
    column = next(row for row in records['schema']['columns']
                  if row['schema'] == table['schema'] and row['name'] == table['name'] and row['column'] == 'id')
    column.update(identity='a', notNull=True, default=None)
    records['sequences']['ownership'][0]['dependency'] = 'i'
    return records


def mutable_ids(value):
    if type(value) is dict:
        return {id(value)} | set().union(*(mutable_ids(item) for item in value.values()), set())
    if type(value) is list:
        return {id(value)} | set().union(*(mutable_ids(item) for item in value), set())
    return set()


class PlannerTests(unittest.TestCase):
    def test_current_proposal_deterministic_no_live_io_all_runtime_values_null(self):
        value = fixture()
        with patch.object(subprocess, 'Popen', side_effect=AssertionError('live process')), \
                patch.object(socket, 'socket', side_effect=AssertionError('live socket')), \
                patch.object(os, 'open', side_effect=AssertionError('unexpected local read')):
            first = a.render_proposal(value)
            second = a.render_proposal(value)
        self.assertEqual(a.canonical(first), a.canonical(second))
        self.assertEqual(first['backendRequests'], 0)
        self.assertFalse(first['runnable'])
        for key in ('snapshotBinding', 'targetRuntimeBinding', 'artifact', 'creationFence',
                    'operationalQualification', 'policyAuthentication', 'recoveryAcceptance',
                    'sourceResumed', 'targetCleanupComplete', 'uploadOrAbortImplementation'):
            self.assertIsNone(first[key], key)
        self.assertTrue(all(x is None for x in first['unresolvedGates'].values()))
        self.assertIsNone(first['capture']['argv'])
        self.assertIsNone(first['capture']['snapshotId'])
        self.assertIsNone(first['capture']['dumpArgvTemplate'][-1])
        self.assertIsNone(first['restore']['argv'])
        self.assertIsNone(first['restore']['stdin'])
        self.assertIsNone(first['restore']['bootstrapCredentials'])
        self.assertIsNone(first['restore']['authenticatedCaptureRecord'])
        self.assertIsNone(first['restore']['independentlyReadBack'])
        first['source']['database'] = 'changed'
        self.assertEqual(value['source']['database'], 'synthetic_telemetry')

    def test_unsafe_source_endpoint_image_tool_or_identity_drift_refused(self):
        for key, change in (
            ('endpoint', lambda v: v['source']['cluster'].update(endpoint='https://127.0.0.1:06443')),
            ('uid', lambda v: v['source']['pod'].update(uid='new-uid')),
            ('image', lambda v: v['source'].update(image='example.invalid/postgres@sha256:' + K)),
            ('toolhash', lambda v: v['source']['tools']['pg_dump'].update(sha256=J)),
            ('native', lambda v: v['source'].update(systemIdentifier='9')),
        ):
            with self.subTest(key=key):
                v = fixture()
                change(v)
                with self.assertRaisesRegex(a.Refusal, '^' + a.FAILURE + '$'):
                    a.render_proposal(v)

    def test_closed_schema_rejects_missing_tool_table_extra_secret_or_duplicate_json(self):
        for change in (
            lambda v: v.update(password='do-not-echo-secret'),
            lambda v: v['source']['tools'].pop('pg_dump'),
            lambda v: v['source']['tools'].update(pg_dump=None),
            lambda v: v['source']['tables'].pop(),
            lambda v: v['source']['tables'].append(deepcopy(v['source']['tables'][0])),
            lambda v: v['source']['roles'][0].update(password='do-not-echo-secret'),
            lambda v: v['source'].update(serverVersionNum=170001),
        ):
            v = fixture()
            change(v)
            repin(v)
            with self.assertRaises(a.Refusal):
                a.render_proposal(v)
        with self.assertRaises(a.c.Refusal):
            a.strict_json(b'{"kind":1,"kind":2}')
        for raw in (b'{"x":1.2}', b'{"x":NaN}'):
            with self.assertRaises(a.c.Refusal):
                a.strict_json(raw)

    def test_injection_connection_string_aliases_and_unsafe_paths_refused(self):
        for bad in ('postgresql://evil/db', 'host=evil', '-dother', 'x; SELECT 1',
                    'x\n\\! id', 'x$(id)', 'x`id`', "x'", 'latest', 'current'):
            for key in ('database', 'user', 'peerOsUser'):
                v = fixture()
                v['source'][key] = bad
                repin(v)
                with self.subTest(key=key, bad=bad), self.assertRaises(a.Refusal):
                    a.render_proposal(v)
        for bad in ('/tmp/../psql', '/tmp/current/psql', '/tmp/psql;id', 'psql', '/tmp/pg_dump'):
            v = fixture()
            v['source']['tools']['psql']['path'] = bad
            repin(v)
            with self.assertRaises(a.Refusal):
                a.render_proposal(v)
        for bad in ('memories/latest/a', 'memories/../a', 'other/a', 'memories/current/a'):
            v = fixture()
            v['transfer']['key'] = bad
            with self.assertRaises(a.Refusal):
                a.render_proposal(v)

    def test_capture_exported_snapshot_lifetime_canaries_and_whole_database(self):
        v = fixture()
        p = a.render_proposal(v)
        capture = p['capture']
        holder = capture['snapshotHolder']
        self.assertIn('REPEATABLE READ READ ONLY', holder['initialStdin'])
        self.assertIn('pg_export_snapshot()', holder['initialStdin'])
        self.assertNotIn('COMMIT', holder['initialStdin'])
        self.assertNotIn('--command', holder['argv'])
        self.assertIn('without-EOF', holder['stdinProtocol'])
        self.assertEqual(holder['releaseStdin'], 'ROLLBACK;\n')
        self.assertIn('sequence-state-is-not-MVCC', capture['sequenceLimit'])
        commands = a.snapshot_commands(v, snapshot_binding(v))
        self.assertIn('--snapshot=00000003-0000001B-1', commands['dumpArgv'])
        self.assertIn('--format=custom', commands['dumpArgv'])
        self.assertIn('--file=-', commands['dumpArgv'])
        self.assertNotIn('--schema', ' '.join(commands['dumpArgv']))
        self.assertNotIn('--table', ' '.join(commands['dumpArgv']))
        sql = commands['canaryStdin']
        self.assertLess(sql.index('SET TRANSACTION SNAPSHOT'), sql.index('current_database()'))
        for table in v['source']['tables']:
            self.assertIn('FROM "' + table['schema'] + '"."' + table['name'] + '"', sql)
        for keyword in ('pg_attribute', 'count(*)', 'pg_sequences', 'pg_constraint',
                        'pg_extension', 'relacl', 'nspacl', 'datacl', 'pg_default_acl', 'pg_roles'):
            self.assertIn(keyword, sql)
        self.assertNotIn('rolpassword', sql)
        self.assertNotIn('pg_authid', sql)
        self.assertFalse(commands['executionAuthorized'])
        self.assertIn('withhold-sequenceStateAfter-and-ROLLBACK-until-dump-completes', commands['canaryStdinProtocol'])

    def test_snapshot_constructor_refuses_missing_binding_injection_and_native_drift(self):
        v = fixture()
        for change in (
            lambda b: b.update(snapshotId=None),
            lambda b: b.update(snapshotId='00000003-0000001B-1\';SELECT 1;--'),
            lambda b: b.update(snapshotId='latest'),
            lambda b: b.update(snapshotId='00000003-0000001B-0'),
            lambda b: b.update(snapshotId='00000003-0000001B-4294967296'),
            lambda b: b.update(holderTransactionOpen=False),
            lambda b: b.update(holderBackendPid=0),
            lambda b: b.update(systemIdentifier='9'),
            lambda b: b.update(timeline=True),
            lambda b: b.update(serverVersionNum=180002),
            lambda b: b.update(sourcePinSha256=K),
            lambda b: b.update(proposalInputSha256=K),
            lambda b: b['preSnapshotTableCensus'].append({'schema': 'public', 'name': 'unexpected'}),
            lambda b: b['snapshotTableCensus'].append({'schema': 'public', 'name': 'unexpected'}),
            lambda b: b['snapshotTableCensus'].pop(),
            lambda b: b['preSnapshotTableCensus'][0].update(name='other_table'),
        ):
            b = snapshot_binding(v)
            change(b)
            with self.assertRaises(a.Refusal):
                a.snapshot_commands(v, b)
        with self.assertRaises(a.Refusal):
            a.snapshot_commands(v, None)

    def test_canary_comparisons_use_portable_names_and_separate_expected_role_session_differences(self):
        contract = a.render_proposal(fixture())['canaryContract']
        self.assertFalse(contract['wholeTranscriptEquality'])
        self.assertEqual(set(contract['expectedDifferences']),
                         {'sessionIdentity', 'roleIsolation', 'databaseActivation'})
        self.assertTrue({'schema', 'dataCounts', 'sequences', 'constraints', 'extensions', 'acls', 'tableCensus'} <=
                        set(contract['requiredEqualCategories']))
        self.assertFalse(set(contract['requiredEqualCategories']) & set(contract['expectedDifferences']))
        sql = contract['sqlByCategory']
        self.assertIn('n.nspname AS schema', sql['acls'])
        self.assertIn('LEFT JOIN pg_namespace', sql['acls'])
        for key in contract['requiredEqualCategories']:
            # Catalog OIDs may occur in JOIN/WHERE/function inputs, but never
            # as compared fields; resolve the owner/schema/type to names first.
            for select in sql[key].split('SELECT ')[1:]:
                projected = select.split(' FROM ')[0]
                columns, column, depth = [], '', 0
                for char in projected + ',':
                    if char == ',' and depth == 0:
                        columns.append(column.strip())
                        column = ''
                    else:
                        column += char
                        depth += (char == '(') - (char == ')')
                for column in columns:
                    self.assertNotRegex(column, r'^(?:[a-z]+\.)?(?:oid|defaclnamespace)$')
        self.assertIn('NOLOGIN-NOSUPERUSER', contract['expectedDifferences']['roleIsolation'])
        self.assertIn('stop-and-rebind', contract['unexpectedOrMissingTableAction'])

    def test_every_pg_command_has_explicit_clean_peer_connection_and_no_ambient_password(self):
        v = fixture()
        p = a.render_proposal(v)
        argvs = [p['capture']['dumpArgvTemplate'], p['capture']['snapshotHolder']['argv'],
                 p['capture']['canaryArgv'], p['restore']['argvTemplate'], p['restore']['canaryArgv']]
        for argv in argvs:
            self.assertEqual(argv[:2], ['/usr/bin/env', '-i'])
            for flag in ('PGPASSFILE=/dev/null', 'PGSERVICEFILE=/dev/null', '--no-password',
                         '--dbname=synthetic_telemetry'):
                self.assertIn(flag, argv)
            self.assertTrue(any(x.startswith('--host=/') for x in argv if x is not None))
            self.assertTrue(any(x.startswith('--username=') for x in argv if x is not None))
            joined = ' '.join(x for x in argv if x is not None)
            self.assertNotIn('PGPASSWORD', joined)
            self.assertNotIn('PGSERVICE=', joined)
            self.assertNotIn('HOME=', joined)

    def test_isolated_manifests_new_delete_pvc_nonroot_no_service_or_source_mounts(self):
        v = fixture()
        p = a.render_proposal(v)
        manifests = p['restore']['manifestTemplates']
        self.assertEqual([m['kind'] for m in manifests],
                         ['Namespace', 'NetworkPolicy', 'PersistentVolumeClaim', 'Secret', 'StatefulSet'])
        network = manifests[1]['spec']
        self.assertEqual(network, {'podSelector': {}, 'policyTypes': ['Ingress', 'Egress'],
                                   'ingress': [], 'egress': []})
        self.assertNotIn('volumeName', manifests[2]['spec'])
        self.assertIsNone(manifests[3]['data']['bootstrap-password'])
        pod = manifests[4]['spec']['template']['spec']
        self.assertFalse(pod['automountServiceAccountToken'])
        self.assertTrue(pod['securityContext']['runAsNonRoot'])
        self.assertGreater(pod['securityContext']['runAsUser'], 0)
        self.assertEqual(pod['containers'][0]['image'], v['source']['image'])
        for vol in pod['volumes']:
            self.assertNotIn('hostPath', vol)
            if 'persistentVolumeClaim' in vol:
                self.assertEqual(vol['persistentVolumeClaim']['claimName'], v['target']['pvcName'])
        self.assertIn('listen_addresses=', pod['containers'][0]['args'])
        self.assertFalse(p['restore']['manifestTemplatesApplyable'])

    def test_target_source_overlap_invented_target_ids_and_unsafe_startup_inputs_refused(self):
        for change in (
            lambda v: v['target'].update(namespace=deepcopy(v['source']['namespace'])),
            lambda v: v['target']['namespace'].update(uid='invented', resourceVersion='1'),
            lambda v: v['target'].update(pvcName=v['source']['pvc']['name']),
            lambda v: v['target'].update(statefulSetName=v['source']['statefulSet']['name']),
            lambda v: v['target']['storageClass'].update(reclaimPolicy='Retain'),
            lambda v: v['target'].update(runAsUser=0),
            lambda v: v['target'].update(bootstrapUser='source_admin'),
            lambda v: v['target'].update(hostPath='/source'),
            lambda v: v['transfer'].update(validatorPrincipal=v['transfer']['writerPrincipal']),
        ):
            v = fixture()
            change(v)
            with self.assertRaises(a.Refusal):
                a.render_proposal(v)

    def test_source_image_digest_agrees_and_target_socket_mounts_cannot_shadow_data_credentials_or_tmp(self):
        v = fixture()
        v['source']['imageId'] = 'docker-pullable://example.invalid/postgres@sha256:' + H
        repin(v)
        a.render_proposal(v)
        v['source']['imageId'] = 'docker-pullable://example.invalid/postgres@sha256:' + J
        repin(v)
        with self.assertRaises(a.Refusal):
            a.render_proposal(v)
        for bad in ('/tmp', '/tmp/socket', '/var', '/var/lib', '/var/lib/postgresql',
                    '/var/lib/postgresql/socket', '/run', '/run/credentials', '/run/credentials/socket'):
            v = fixture()
            v['target']['socket'] = bad
            with self.subTest(socket=bad), self.assertRaises(a.Refusal):
                a.render_proposal(v)

    def test_roles_preserve_recorded_facts_but_target_application_roles_are_nologin(self):
        p = a.render_proposal(fixture())
        self.assertEqual(p['restore']['sourceRoles'], fixture()['source']['roles'])
        self.assertIn('CREATE ROLE "synthetic_admin" WITH NOLOGIN NOSUPERUSER', p['restore']['targetRoleSql'])
        self.assertNotIn('CREATE ROLE "postgres"', p['restore']['targetRoleSql'])
        self.assertNotIn('CREATE ROLE "pg_monitor"', p['restore']['targetRoleSql'])
        self.assertNotIn('PASSWORD', p['restore']['targetRoleSql'])
        self.assertIn('no-activation-authority', p['restore']['roleDifference'])

    def test_restore_constructor_exact_immutable_readback_stdin_and_safe_flags(self):
        v = fixture()
        b = restore_binding(v)
        with patch.object(os, 'open', side_effect=AssertionError('no input reads')), \
                patch.object(subprocess, 'Popen', side_effect=AssertionError('no process')), \
                patch.object(socket, 'socket', side_effect=AssertionError('no socket')):
            command = a.restore_command(v, b)
        argv = command['argv']
        for flag in ('--format=custom', '--exit-on-error', '--single-transaction', '--host=/run/postgresql',
                     '--username=postgres', '--dbname=synthetic_telemetry'):
            self.assertIn(flag, argv)
        for flag in ('--clean', '--create', '--no-owner', '--no-acl', '--jobs', '--file'):
            self.assertFalse(any(x == flag or x.startswith(flag + '=') for x in argv))
        self.assertEqual(command['stdinContract']['immutableArtifact'], b['artifact'])
        self.assertEqual(command['stdinContract']['sha256'], J)
        self.assertFalse(command['executionAuthorized'])

    def test_restore_refuses_unknown_or_reused_uids_empty_schema_image_storage_and_artifact_drift(self):
        v = fixture()
        changes = [
            lambda b: b['namespace'].update(uid=None),
            lambda b: b['pvc'].update(uid=None),
            lambda b: b['namespace'].update(uid=v['source']['namespace']['uid']),
            lambda b: b['pod'].update(uid=v['source']['pod']['uid']),
            lambda b: b['pvc'].update(uid=v['source']['pvc']['uid']),
            lambda b: b['pv'].update(uid=v['source']['pv']['uid']),
            lambda b: b['emptySchema'].update(tables=['existing']),
            lambda b: b.update(emptySchema=None),
            lambda b: b.update(image='example.invalid/postgres@sha256:' + K),
            lambda b: b.update(imageId='example.invalid/postgres@sha256:' + K),
            lambda b: b.update(pvcVolumeName=v['source']['pv']['name']),
            lambda b: b.update(pvClaimUid='wrong'),
            lambda b: b.update(pvClaimNamespace='synthetic-source'),
            lambda b: b.update(storageClassUid='wrong'),
            lambda b: b.update(reclaimPolicy='Retain'),
            lambda b: b['independentlyReadBack'].update(version='different-version'),
            lambda b: b['independentlyReadBack'].update(sha256=K),
            lambda b: b['independentlyReadBack'].update(validatorPrincipal='synthetic-writer'),
            lambda b: b['artifact'].update(version='latest'),
            lambda b: b['artifact'].update(key='other/key'),
            lambda b: b.update(decryptedStdinSha256=None),
            lambda b: b.update(decryptedStdinSha256=K),
            lambda b: b.update(decryptedStdinSize=41),
            lambda b: b['authenticatedCaptureRecord'].update(plaintextSha256=K),
            lambda b: b['authenticatedCaptureRecord'].update(plaintextSize=41),
            lambda b: b['authenticatedCaptureRecord'].update(artifactSha256=K),
            lambda b: b['authenticatedCaptureRecord'].update(artifactVersion='different-version'),
        ]
        for i, change in enumerate(changes):
            b = restore_binding(v)
            change(b)
            with self.subTest(case=i), self.assertRaises(a.Refusal):
                a.restore_command(v, b)

    def test_restore_equal_ancestor_descendant_source_path_or_unknown_mapping_refused(self):
        v = fixture()
        for path in ('/var/openebs/local/source-data', '/var/openebs/local/source-data/child',
                     '/var/openebs/local'):
            b = restore_binding(v)
            b['volumePath']['path'] = path
            with self.assertRaises(a.Refusal):
                a.restore_command(v, b)
        v['source']['volumePath'] = None
        repin(v)
        self.assertIsNone(a.render_proposal(v)['source']['volumePath'])
        with self.assertRaises(a.Refusal):
            a.restore_command(v, restore_binding(v))

    def test_pause_resume_exact_uid_rv_prior_count_clock_and_parent_prerequisites(self):
        p = a.render_proposal(fixture())
        for label, before, after in (('pause', 1, 0), ('resume', 0, 1)):
            shape = p[label]
            self.assertEqual(shape['contentType'], 'application/json-patch+json')
            self.assertEqual(shape['body'][0], {'op': 'test', 'path': '/metadata/uid', 'value': 'worker-uid'})
            self.assertEqual(shape['body'][1]['path'], '/metadata/resourceVersion')
            self.assertEqual(shape['body'][2], {'op': 'test', 'path': '/spec/replicas', 'value': before})
            self.assertEqual(shape['body'][3], {'op': 'replace', 'path': '/spec/replicas', 'value': after})
        self.assertEqual(p['pause']['body'][1]['value'], '11')
        self.assertIsNone(p['resume']['body'][1]['value'])
        self.assertEqual(p['resume']['preserveClock']['previousReplicas'], 0)
        self.assertIn('parent-MCP-and-paired-stores-restored-and-verified', p['resume']['requiredBeforePatch'])
        self.assertIn('PG18-restored-and-canaries-agree', p['resume']['requiredBeforePatch'])
        self.assertIsNone(p['resume']['reconciliation'])
        for key, bad in (('worker', 2), ('worker', True), ('clock', 1)):
            v = fixture()
            v[key]['previousReplicas'] = bad
            with self.assertRaises(a.Refusal):
                a.render_proposal(v)

    def test_cleanup_all_future_owned_uids_rvs_fence_paths_null_no_source_writes(self):
        p = a.render_proposal(fixture())
        cleanup = p['cleanup']
        for key in ('creationFence', 'ownedVolumePaths', 'inventory', 'complete'):
            self.assertIsNone(cleanup[key])
        self.assertEqual(cleanup['deleteOptionsTemplate']['preconditions'], {'uid': None, 'resourceVersion': None})
        self.assertEqual(cleanup['sourceWrites'], [])
        for target in cleanup['ownedTargets']:
            self.assertIsNone(target['uid'])
            self.assertIsNone(target['resourceVersion'])
            self.assertNotEqual(target['namespace'], fixture()['source']['namespace']['name'])
        self.assertIn('namespace-last', cleanup['order'])
        self.assertIn('same-node-path-absence', cleanup['order'])

    def test_every_public_contract_has_no_mutable_input_alias_in_either_direction(self):
        value = fixture()
        snapshot, restore = snapshot_binding(value), restore_binding(value)
        for result in (a.validate(value), a.render_proposal(value), a.snapshot_commands(value, snapshot),
                       a.restore_command(value, restore)):
            self.assertFalse(mutable_ids(result) & (mutable_ids(value) | mutable_ids(snapshot) | mutable_ids(restore)))
        result = a.restore_command(value, restore)
        result['stdinContract']['immutableArtifact']['sha256'] = K
        self.assertEqual(restore['artifact']['sha256'], H)
        result = a.restore_command(value, restore)
        restore['artifact']['sha256'] = K
        self.assertEqual(result['stdinContract']['immutableArtifact']['sha256'], H)
        plan = a.render_proposal(value)
        value['source']['roles'][0]['name'] = 'changed_caller'
        self.assertEqual(plan['restore']['sourceRoles'][0]['name'], 'postgres')
        plan['targetProposal']['resources']['requests']['cpu'] = '2'
        self.assertEqual(value['target']['resources']['requests']['cpu'], '100m')

    def test_retained_transfer_policy_is_closed_and_exact(self):
        for key, bad in (('retentionDays', 29), ('retentionDays', True), ('retentionDays', '30'),
                         ('maximumProofAgeSeconds', 86399), ('maximumProofAgeSeconds', None)):
            value = fixture()
            value['transfer'][key] = bad
            with self.subTest(key=key, bad=bad), self.assertRaises(a.Refusal):
                a.render_proposal(value)
        for key in ('retentionDays', 'maximumProofAgeSeconds'):
            value = fixture()
            del value['transfer'][key]
            with self.assertRaises(a.Refusal):
                a.render_proposal(value)

    def test_retention_requires_post_observation_capture_exact_expiry_and_independent_compliance(self):
        value = fixture()
        changes = (
            lambda b: b['authenticatedCaptureRecord'].update(capturedAt='2000-01-01T00:00:00Z',
                                                             expiresAt='2000-01-02T00:00:00Z'),
            lambda b: b['authenticatedCaptureRecord'].update(capturedAt='2026-10-23T11:59:59Z',
                                                             expiresAt='2026-10-24T11:59:59Z'),
            lambda b: b['authenticatedCaptureRecord'].update(expiresAt='2026-10-24T12:00:00Z'),
            lambda b: b['authenticatedCaptureRecord'].update(expiresAt='2026-10-24T12:00:02Z'),
            lambda b: b['authenticatedCaptureRecord'].pop('capturedAt'),
            lambda b: b['authenticatedCaptureRecord'].pop('expiresAt'),
            lambda b: b['artifact'].update(retainUntil='2000-01-01T00:00:00Z'),
            lambda b: b['independentlyReadBack'].update(retainUntil='2000-01-01T00:00:00Z'),
            lambda b: b['independentlyReadBack'].update(retentionMode='GOVERNANCE'),
            lambda b: b['independentlyReadBack'].update(retentionMode=None),
            lambda b: b['independentlyReadBack'].update(encryption='aws:kms'),
            lambda b: b['independentlyReadBack'].update(store='other-store'),
            lambda b: b['independentlyReadBack'].update(key='memories/other'),
            lambda b: b['independentlyReadBack'].pop('retainUntil'),
        )
        for i, change in enumerate(changes):
            binding = restore_binding(value)
            change(binding)
            with self.subTest(case=i), self.assertRaises(a.Refusal):
                a.restore_command(value, binding)
        binding = restore_binding(value)
        binding['artifact']['retainUntil'] = binding['independentlyReadBack']['retainUntil'] = '2026-11-22T12:00:00Z'
        with self.assertRaises(a.Refusal):
            a.restore_command(value, binding)
        binding['artifact']['retainUntil'] = binding['independentlyReadBack']['retainUntil'] = '2026-11-22T12:00:01Z'
        self.assertFalse(a.restore_command(value, binding)['executionAuthorized'])
        binding['artifact']['retainUntil'] = binding['independentlyReadBack']['retainUntil'] = '2000-01-01T00:00:00Z'
        with self.assertRaises(a.Refusal):
            a.restore_command(value, binding)
        binding = restore_binding(value)
        binding['artifact']['size'], binding['independentlyReadBack']['size'] = 1, True
        with self.assertRaises(a.Refusal):
            a.restore_command(value, binding)

    def test_cpu_and_memory_dimensions_normalized_and_impossible_requests_refused(self):
        for kind, request, limit in (('cpu', '1000m', '500m'), ('cpu', '2', '1500m'),
                                     ('memory', '1Gi', '512Mi'), ('memory', '1025Mi', '1Gi')):
            value = fixture()
            value['target']['resources']['requests'][kind] = request
            value['target']['resources']['limits'][kind] = limit
            with self.subTest(kind=kind, request=request, limit=limit), self.assertRaises(a.Refusal):
                a.render_proposal(value)
        for kind, bad in (('cpu', '1Mi'), ('cpu', '1Gi'), ('cpu', '0.5'), ('cpu', '0'),
                          ('cpu', '001m'), ('cpu', '1e3'), ('memory', '1000m'), ('memory', '1'),
                          ('memory', '1Ti'), ('memory', '1G'), ('memory', '0Mi'), ('memory', '-1Mi')):
            value = fixture()
            value['target']['resources']['requests'][kind] = bad
            with self.subTest(kind=kind, bad=bad), self.assertRaises(a.Refusal):
                a.render_proposal(value)
        value = fixture()
        value['target']['resources'] = {'requests': {'cpu': '1', 'memory': '1Gi'},
                                         'limits': {'cpu': '1000m', 'memory': '1024Mi'}}
        plan = a.render_proposal(value)
        resources = plan['restore']['manifestTemplates'][-1]['spec']['template']['spec']['containers'][0]['resources']
        self.assertEqual(resources['requests'], resources['limits'])
        self.assertEqual(resources['requests'], {'cpu': '1000m', 'memory': '1024Mi'})

    def test_content_digest_sql_profile_and_stable_representations_are_explicit(self):
        plan = a.render_proposal(fixture())
        sql = plan['capture']['canarySqlBody']
        for fragment in ("timezone = 'UTC'", "datestyle = 'ISO, YMD'", "intervalstyle = 'iso_8601'",
                         "bytea_output = 'hex'", 'extra_float_digits = 3', 'row_to_json(r)::text',
                         'sha256(convert_to(', 'octet_length(convert_to(row_json', 'LIMIT 100001',
                         'ORDER BY row_json COLLATE "C"', 'profileAccepted', "statement_timeout = '15s'"):
            self.assertIn(fragment, sql)
        self.assertEqual(sql.count('row_to_json(r)::text'), 2)
        self.assertNotIn('DISTINCT row_json', sql)
        self.assertNotIn('CREATE EXTENSION', sql)
        self.assertIsNone(plan['canaryContract']['contentHashProfile']['qualification'])
        self.assertIsNone(plan['canaryContract']['agreement'])

    def test_sequence_direct_samples_quote_catalog_names_and_remain_read_only(self):
        sections = a.render_proposal(fixture())['canaryContract']['sqlByCategory']
        for category in ('sequenceStateBefore', 'sequenceStateAfter'):
            sql = sections[category]
            self.assertIn('last_value::text', sql)
            self.assertIn('is_called AS "isCalled"', sql)
            self.assertIn('FROM %I.%I', sql)
            self.assertIn('format(', sql)
            self.assertIn('\\gexec', sql)
            self.assertIn('WHERE false', sql)
            for mutation in ('CHECKPOINT', 'nextval', 'setval', 'INSERT', 'UPDATE', 'DELETE', 'CREATE'):
                self.assertNotIn(mutation, sql)

    def test_structured_canaries_compare_separate_categories_and_deliberate_differences_without_io(self):
        value = fixture()
        source, target = canary_records(value), canary_records(value, target=True)
        with patch.object(os, 'open', side_effect=AssertionError('no reads')), \
                patch.object(socket, 'socket', side_effect=AssertionError('no socket')), \
                patch.object(subprocess, 'Popen', side_effect=AssertionError('no process')):
            result = a.compare_canaries(value, canary_bytes(source), canary_bytes(target))
        self.assertTrue(result['consistent'])
        self.assertFalse(result['executionAuthorized'])
        self.assertIsNone(result['recoveryAcceptance'])
        self.assertIsNone(result['operationalQualification'])
        self.assertIn('dataContents', result['equalCategories'])
        self.assertEqual(set(result['expectedDifferenceCategories']), set(a.DIFFERENT_CATEGORIES))

    def test_equal_counts_with_changed_content_hash_cannot_pass(self):
        value = fixture()
        source, target = canary_records(value), canary_records(value, target=True)
        target['dataContents'][0]['sha256'] = J
        self.assertEqual(source['dataCounts'], target['dataCounts'])
        with self.assertRaises(a.Refusal):
            a.compare_canaries(value, canary_bytes(source), canary_bytes(target))

    def test_data_hash_checks_missing_duplicate_profile_and_count_inconsistency_refused(self):
        value = fixture()
        changes = (
            lambda r: r['dataContents'].pop(),
            lambda r: r['dataContents'].__setitem__(1, deepcopy(r['dataContents'][0])),
            lambda r: r['dataContents'][0].update(sha256=None),
            lambda r: r['dataContents'][0].update(profileAccepted=False),
            lambda r: r['dataContents'][0].update(rows=100001),
            lambda r: r['dataContents'][0].update(rows=3),
            lambda r: r['dataContents'][0].update(rowBytesMaximum=65537),
            lambda r: r['dataContents'][0].update(contentBytes=16777217),
            lambda r: r['dataCounts'][0].update(rows=True),
        )
        for i, change in enumerate(changes):
            records = canary_records(value)
            change(records)
            with self.subTest(case=i), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        records = canary_records(value)
        for category in ('dataCounts', 'dataContents'):
            for row in records[category]:
                row['rows'] = 0
                if category == 'dataContents':
                    row.update(rowBytesMaximum=0, contentBytes=0, sha256=a.digest(b''))
        a.parse_canaries(value, canary_bytes(records))

    def test_sequence_census_requires_both_stable_samples_including_explicit_zero(self):
        value = fixture()
        for change in (
            lambda r: r.pop('sequenceCensus'),
            lambda r: r.pop('sequenceStateBefore'),
            lambda r: r.pop('sequenceStateAfter'),
            lambda r: r.update(sequenceStateAfter=[]),
            lambda r: r['sequenceStateBefore'].append(deepcopy(r['sequenceStateBefore'][0])),
            lambda r: r['sequenceStateAfter'][0].update(isCalled=True),
            lambda r: r['sequenceStateAfter'][0].update(lastValue='43'),
            lambda r: r['sequenceCensus'][0].update(name='other'),
            lambda r: r['sequences'].update(definitions=[]),
        ):
            records = canary_records(value)
            change(records)
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        source, target = canary_records(value, sequences=False), canary_records(value, target=True, sequences=False)
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        target.pop('sequenceStateBefore')
        with self.assertRaises(a.Refusal):
            a.compare_canaries(value, canary_bytes(source), canary_bytes(target))

    def test_structured_separators_newlines_and_null_are_unambiguous_and_metadata_changes_refused(self):
        value = fixture()
        source, target = canary_records(value), canary_records(value, target=True)
        raw = canary_bytes(source)
        parsed = a.parse_canaries(value, raw)
        self.assertEqual(parsed['constraints'][0]['constraint'], 'check|line\nname')
        self.assertIsNone(parsed['schema']['columns'][0]['default'])
        self.assertEqual(parsed['acls'][0]['schema'], None)
        for change in (
            lambda r: r['constraints'][0].update(definition="CHECK (body <> 'first\nsecond|third')"),
            lambda r: r['schema']['columns'][0].update(default='null'),
            lambda r: r['schema']['namespaces'][0].update(acl=[]),
            lambda r: r['acls'][0].update(schema=''),
            lambda r: r['schema']['relations'][0].update(acl=['a\nb|c=d/u']),
        ):
            changed = deepcopy(target)
            change(changed)
            with self.assertRaises(a.Refusal):
                a.compare_canaries(value, raw, canary_bytes(changed))
        # Catalog ordering alone is not a changed observation.
        target['dataCounts'].reverse()
        target['dataContents'].reverse()
        target['schema']['columns'].reverse()
        self.assertTrue(a.compare_canaries(value, raw, canary_bytes(target))['consistent'])

    def test_tagged_parser_refuses_category_spoofing_missing_duplicate_and_raw_tuples(self):
        value = fixture()
        source = canary_records(value)
        good = canary_bytes(source)
        lines = good.splitlines(keepends=True)
        first = json.loads(lines[0])
        spoofs = [good + lines[0], b''.join(lines[1:]), b'a|b|NULL\n' + b''.join(lines[1:]),
                  a.canonical({**first, 'category': 'unrecognized'}) + b'\n' + b''.join(lines[1:]),
                  a.canonical({**first, 'category': 'tableCensus'}) + b'\n' + b''.join(lines[1:]),
                  a.canonical({**first, 'payload': {'category': 'tableCensus', 'payload': []}}) + b'\n' + b''.join(lines[1:]),
                  b'{"category":"sessionIdentity","category":"tableCensus","payload":[]}\n' + b''.join(lines[1:]),
                  good.replace(b'"payload":', b'"extra":0,"payload":', 1)]
        for i, raw in enumerate(spoofs):
            with self.subTest(case=i), self.assertRaises(a.Refusal):
                a.parse_canaries(value, raw)
        source['constraints'].append(deepcopy(source['constraints'][0]))
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(source))

    def test_canary_parser_is_bounded_data_only_and_rejects_unexpected_activation(self):
        value = fixture()
        raw = canary_bytes(canary_records(value))
        for bad in (b' ' * (a.c.MAX_JSON + 1), b'\n' + raw, raw + b'\n',
                    raw.replace(b'"synthetic_telemetry"', b'"x\\u0000y"', 1)):
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, bad)
        for change in (
            lambda r: r['roleIsolation']['roles'][1].update(canLogin=True),
            lambda r: r['sessionIdentity'].update(user=value['source']['user']),
            lambda r: r['databaseActivation'].update(owner=value['source']['user']),
            lambda r: r['databaseActivation'].update(acl=[]),
        ):
            target = canary_records(value, target=True)
            change(target)
            with self.assertRaises(a.Refusal):
                a.compare_canaries(value, raw, canary_bytes(target))

    def test_native_json_content_sql_never_normalizes_json_fields_through_jsonb(self):
        plan = a.render_proposal(fixture())
        sql = plan['canaryContract']['sqlByCategory']['dataContents']
        self.assertEqual(sql.count('row_to_json(r)::text AS row_json'), 2)
        for normalization in ('to_jsonb(', 'jsonb_', '::jsonb'):
            self.assertNotIn(normalization, sql)
        self.assertIn('row_to_json', plan['canaryContract']['contentHashProfile']['algorithm'])
        # Native JSON duplicates/whitespace are distinct input representations.
        # The pure comparator must not normalize a supplied data digest either.
        source, target = canary_records(fixture()), canary_records(fixture(), target=True)
        source['dataContents'][0]['sha256'] = a.digest(b'{"payload":{"x":1,"x":2}}')
        target['dataContents'][0]['sha256'] = a.digest(b'{"payload":{"x":2}}')
        with self.assertRaises(a.Refusal):
            a.compare_canaries(fixture(), canary_bytes(source), canary_bytes(target))

    def test_schema_profile_requires_complete_bound_table_relations_and_columns(self):
        value = fixture()
        changes = (
            lambda r: r['schema'].update(relations=[], columns=[]),
            lambda r: r['schema']['relations'].pop(0),
            lambda r: r['schema'].update(columns=[x for x in r['schema']['columns']
                                                  if x['name'] != value['source']['tables'][0]['name']]),
            lambda r: r['schema']['columns'][0].update(name='unknown_relation'),
            lambda r: r['schema']['relations'][0].update(schema='unknown_schema'),
            lambda r: r['schema']['columns'][0].update(schema='unknown_schema'),
            lambda r: r['schema']['relations'][0].update(name='unknown_table'),
            lambda r: r['schema']['namespaces'].pop(),
            lambda r: r['schema']['namespaces'].append(deepcopy(r['schema']['namespaces'][0])),
            lambda r: r['schema']['columns'].append(deepcopy(r['schema']['columns'][0])),
            lambda r: r['schema']['columns'][1].update(column=r['schema']['columns'][0]['column']),
            lambda r: r['schema']['columns'][0].update(position=0),
            lambda r: r['schema']['columns'][0].update(position=1601),
            lambda r: r['schema']['columns'][0].update(type=''),
            lambda r: r['schema']['columns'][0].update(identity='invalid'),
            lambda r: r['schema']['columns'][0].update(generated='invalid'),
            lambda r: r['schema']['columns'][0].update(identity='a', generated='s'),
            lambda r: r['schema']['relations'][0].update(owner='unobserved_role'),
        )
        for i, change in enumerate(changes):
            records = canary_records(value)
            change(records)
            with self.subTest(case=i), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))

    def test_schema_profile_rejects_unsupported_relation_kinds_and_orphan_metadata(self):
        value = fixture()
        for kind in ('v', 'm', 'f', 'p', 'I', 'c', 't', ''):
            records = canary_records(value)
            records['schema']['relations'][0]['kind'] = kind
            with self.subTest(kind=kind), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        for change in (
            lambda r: r['constraints'][0].update(name='unknown_table'),
            lambda r: r['sequences']['ownership'][0].update(tableName='unknown_table'),
            lambda r: r['sequences']['ownership'][0].update(tableSchema='unknown_schema'),
            lambda r: r['sequences']['ownership'][0].update(column='unknown_column'),
            lambda r: r['sequences']['ownership'][0].update(dependency='n'),
            lambda r: r['sequenceCensus'][0].update(schema='unknown_schema'),
            lambda r: r['schema']['relations'][-1].update(kind='r'),
            lambda r: r['schema']['columns'].append({**r['schema']['columns'][0], 'name': 'orphan'}),
        ):
            records = canary_records(value)
            change(records)
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))

    def test_supported_index_catalog_must_match_relations_columns_and_bound_tables(self):
        value = fixture()
        source, target = add_index(canary_records(value)), add_index(canary_records(value, target=True))
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        for change in (
            lambda r: r['indexes'][0].update(name='unknown_table'),
            lambda r: r['indexes'][0].update(index='missing_relation'),
            lambda r: r['indexes'].clear(),
            lambda r: r['schema']['relations'].pop(),
            lambda r: r['schema']['columns'].pop(),
            lambda r: r['indexes'].append({**r['indexes'][0], 'name': r['tableCensus'][1]['name']}),
        ):
            changed = deepcopy(target)
            change(changed)
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(changed))

    def test_unsupported_object_census_is_explicit_complete_and_empty_before_agreement(self):
        value = fixture()
        for category in a.UNSUPPORTED_CATEGORIES:
            source = canary_records(value)
            source['unsupportedObjects'][category] = [{'schema': 'synthetic_telemetry', 'name': 'unhandled'}]
            with self.subTest(category=category), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(source))
            # Matching unsupported objects on both sides never qualifies them.
            with self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(source), canary_bytes(source))
            source = canary_records(value)
            del source['unsupportedObjects'][category]
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(source))
        for replacement in (None, [], {'routines': []}):
            source = canary_records(value)
            source['unsupportedObjects'] = replacement
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(source))
        source = canary_records(value)
        source.pop('unsupportedObjects')
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(source))

    def test_unsupported_object_sql_is_read_only_and_extensions_only_have_pinned_identity_coverage(self):
        value = fixture()
        sql = a.render_proposal(value)['canaryContract']['sqlByCategory']['unsupportedObjects']
        for catalog in ('pg_proc', 'pg_depend', 'pg_trigger', 'pg_policy', 'pg_type', 'pg_operator',
                        'pg_opclass', 'pg_opfamily', 'pg_collation', 'pg_conversion', 'pg_ts_config',
                        'pg_rewrite', 'pg_inherits', 'pg_event_trigger', 'pg_foreign_server',
                        'pg_language', 'pg_publication', 'pg_subscription', 'pg_largeobject_metadata', 'pg_cast',
                        'pg_am', 'pg_transform'):
            self.assertIn(catalog, sql)
        for fragment in ('NOT t.tgisinternal', 'c.relrowsecurity', 'c.relforcerowsecurity', "ed.deptype='e'",
                         "ed.refclassid='pg_extension'::regclass", 'base.typarray=t.oid'):
            self.assertIn(fragment, sql)
        for forbidden in ('CREATE ', 'ALTER ', 'DROP ', 'INSERT ', 'UPDATE ', 'DELETE ', 'pg_get_functiondef',
                          'subconninfo', 'rolpassword'):
            self.assertNotIn(forbidden, sql)
        value['source']['extensions'].append({'name': 'pgstattuple', 'schema': 'public', 'version': '1.5'})
        repin(value)
        source, target = canary_records(value), canary_records(value, target=True)
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        self.assertIn('no-routine-body-equality', a.render_proposal(value)['canaryContract']['supportedSchemaProfile']['extensionCoverage'])
        value['source']['extensions'].append({'name': 'unknown_extension', 'schema': 'public', 'version': '1.0'})
        repin(value)
        with self.assertRaises(a.Refusal):
            a.render_proposal(value)

    def test_sequence_numeric_definitions_refuse_malformed_and_impossible_native_profiles(self):
        value = fixture()
        cases = [('type', 'numeric'), ('minimum', 'x'), ('minimum', '+1'), ('minimum', '01'),
                 ('minimum', '-0'), ('minimum', '1.0'), ('minimum', '1e3'), ('minimum', ' 1'),
                 ('minimum', '-9223372036854775809'), ('maximum', '9223372036854775808'),
                 ('minimum', '9223372036854775807'), ('maximum', '1'), ('maximum', '0'),
                 ('start', '0'), ('start', '9223372036854775808'), ('start', 'bad'),
                 ('increment', '0'), ('increment', '9223372036854775808'), ('increment', '1.5'),
                 ('cache', '0'), ('cache', '-1'), ('cache', '9223372036854775808'), ('cache', 'bad')]
        for key, bad in cases:
            records = canary_records(value)
            records['sequences']['definitions'][0][key] = bad
            with self.subTest(key=key, bad=bad), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        for typ, minimum, maximum in (('smallint', '-32768', '32767'), ('integer', '-2147483648', '2147483647'),
                                       ('bigint', '-9223372036854775808', '9223372036854775807')):
            records = canary_records(value)
            definition = records['sequences']['definitions'][0]
            definition.update(type=typ, minimum=minimum, maximum=maximum)
            a.parse_canaries(value, canary_bytes(records))
            for field, bad in (('minimum', str(int(minimum) - 1)), ('maximum', str(int(maximum) + 1)),
                               ('start', str(int(maximum) + 1))):
                altered = deepcopy(records)
                altered['sequences']['definitions'][0][field] = bad
                with self.subTest(typ=typ, field=field), self.assertRaises(a.Refusal):
                    a.parse_canaries(value, canary_bytes(altered))

    def test_sequence_samples_fit_definition_bounds_and_stable_target_differences_refuse(self):
        value = fixture()
        for last in ('0', '-1', '101', '-0', '01', 'abc', '1.0', '9223372036854775808'):
            source = canary_records(value)
            source['sequences']['definitions'][0].update(maximum='100')
            for category in ('sequenceStateBefore', 'sequenceStateAfter'):
                source[category][0]['lastValue'] = last
            with self.subTest(last=last), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(source))
        source = canary_records(value)
        source['sequences']['definitions'][0].update(minimum='-100', maximum='-1', start='-1', increment='-1')
        for category in ('sequenceStateBefore', 'sequenceStateAfter'):
            source[category][0]['lastValue'] = '-42'
        a.parse_canaries(value, canary_bytes(source))
        source = canary_records(value)
        for change in ({'lastValue': '43'}, {'isCalled': True}):
            target = canary_records(value, target=True)
            for category in ('sequenceStateBefore', 'sequenceStateAfter'):
                target[category][0].update(change)
            # Each target transcript is internally stable and individually valid.
            a.parse_canaries(value, canary_bytes(target))
            with self.subTest(change=change), self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(source), canary_bytes(target))

    def test_content_statistics_require_feasible_nonempty_row_representations(self):
        value = fixture()
        for maximum, total in ((1, 16000000), (1, 2), (0, 1), (64, 16000000), (64, 64), (64, 65), (2, 5)):
            records = canary_records(value)
            records['dataContents'][0].update(rowBytesMaximum=maximum, contentBytes=total)
            with self.subTest(maximum=maximum, total=total), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        records = canary_records(value)
        records['dataContents'][0].update(rowBytesMaximum=64, contentBytes=66)
        a.parse_canaries(value, canary_bytes(records))
        records['dataContents'][0].update(rowBytesMaximum=64, contentBytes=128)
        a.parse_canaries(value, canary_bytes(records))
        for category in ('dataCounts', 'dataContents'):
            records[category][0]['rows'] = 0
        records['dataContents'][0].update(rowBytesMaximum=0, contentBytes=0, sha256=H)
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))

    def test_custom_target_role_all_authority_flags_and_inheritance_must_be_false(self):
        value = fixture()
        source = canary_records(value)
        self.assertTrue(source['roleIsolation']['roles'][1]['createDb'])
        self.assertTrue(source['roleIsolation']['roles'][1]['createRole'])
        for flag in a.ROLE_FLAGS:
            target = canary_records(value, target=True)
            target['roleIsolation']['roles'][1][flag] = True
            with self.subTest(flag=flag), self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(source), canary_bytes(target))
        for flag in a.ROLE_FLAGS:
            target = canary_records(value, target=True)
            del target['roleIsolation']['roles'][1][flag]
            with self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(source), canary_bytes(target))
        sql = a.render_proposal(value)['restore']['targetRoleSql']
        for restriction in ('NOLOGIN', 'NOSUPERUSER', 'NOCREATEDB', 'NOCREATEROLE', 'NOREPLICATION',
                            'NOBYPASSRLS', 'NOINHERIT'):
            self.assertIn(restriction, sql)
        authority_sql = a.render_proposal(value)['canaryContract']['sqlByCategory']['roleIsolation']
        for catalog_field in ('rolcreatedb', 'rolcreaterole', 'rolreplication', 'rolbypassrls', 'rolinherit',
                              'pg_auth_members', 'admin_option', 'inherit_option', 'set_option'):
            self.assertIn(catalog_field, authority_sql)

    def test_custom_membership_semantics_and_target_activation_grants_always_refuse(self):
        value = fixture()
        for membership in (
            {'role': 'pg_monitor', 'member': 'synthetic_admin', 'grantor': 'postgres', 'admin': True, 'inherit': True, 'set': True},
            {'role': 'synthetic_admin', 'member': 'postgres', 'grantor': 'postgres', 'admin': False, 'inherit': False, 'set': False},
            {'role': 'pg_monitor', 'member': 'postgres', 'grantor': 'synthetic_admin', 'admin': False, 'inherit': True, 'set': True},
        ):
            for side in ('source', 'target', 'both'):
                source, target = canary_records(value), canary_records(value, target=True)
                if side in ('source', 'both'):
                    source['roleIsolation']['memberships'] = [membership]
                if side in ('target', 'both'):
                    target['roleIsolation']['memberships'] = [membership]
                with self.subTest(side=side, membership=membership), self.assertRaises(a.Refusal):
                    a.compare_canaries(value, canary_bytes(source), canary_bytes(target))

    def test_builtin_membership_profile_matches_exactly_with_explicit_bootstrap_exception(self):
        value = fixture()
        value['source']['roles'].append({'name': 'pg_read_all_settings', 'canLogin': False, 'superuser': False})
        repin(value)
        source, target = canary_records(value), canary_records(value, target=True)
        membership = {'role': 'pg_read_all_settings', 'member': 'pg_monitor', 'grantor': 'postgres',
                      'admin': False, 'inherit': True, 'set': True}
        source['roleIsolation']['memberships'] = [deepcopy(membership)]
        target['roleIsolation']['memberships'] = [deepcopy(membership)]
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        for change in (
            lambda r: r['roleIsolation']['memberships'].clear(),
            lambda r: r['roleIsolation']['memberships'][0].update(admin=True),
            lambda r: r['roleIsolation']['memberships'][0].update(inherit=False),
            lambda r: r['roleIsolation']['memberships'][0].update(set=False),
            lambda r: r['roleIsolation']['memberships'][0].update(member='unknown_role'),
            lambda r: r['roleIsolation']['memberships'].append(deepcopy(membership)),
            lambda r: r['roleIsolation']['roles'][0].update(replication=False),
            lambda r: r['roleIsolation']['roles'][2].update(createDb=True),
            lambda r: r['roleIsolation']['roles'][2].update(inherit=False),
        ):
            altered = deepcopy(target)
            change(altered)
            with self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(source), canary_bytes(altered))

    def test_cleanup_owned_roots_align_with_common_and_generated_pod_is_census_only(self):
        plan = a.render_proposal(fixture())
        cleanup = plan['cleanup']
        for root in cleanup['ownedTargets']:
            self.assertIn((root['apiVersion'], root['kind']), a.c.DELETE_ROOT_KINDS)
            self.assertNotEqual(root['kind'], 'Pod')
            self.assertIsNone(root['uid'])
            self.assertIsNone(root['resourceVersion'])
        self.assertEqual(sum(row['kind'] == 'Pod' for row in cleanup['cleanupCensusTemplate']), 1)
        pod = next(row for row in cleanup['cleanupCensusTemplate'] if row['kind'] == 'Pod')
        self.assertNotIn((pod['apiVersion'], pod['kind']), a.c.DELETE_ROOT_KINDS)
        self.assertEqual(pod['name'], fixture()['target']['statefulSetName'] + '-0')
        self.assertTrue(all(row in cleanup['cleanupCensusTemplate'] for row in cleanup['ownedTargets']))
        for key in ('cleanupCensus', 'targetPaths', 'creationFence', 'inventory', 'ownedVolumePaths', 'complete'):
            self.assertIsNone(cleanup[key])
        for row in cleanup['cleanupCensusTemplate']:
            self.assertIsNone(row['uid'])
            self.assertIsNone(row['resourceVersion'])

    def test_portable_database_semantics_are_required_and_compared_without_current_fact_invention(self):
        value = fixture()
        source = canary_records(value)
        for key, change in (
            ('encoding', {'encoding': 'LATIN1'}), ('collate', {'collate': 'POSIX'}),
            ('ctype', {'ctype': 'POSIX'}), ('locale', {'localeProvider': 'b', 'locale': 'C.UTF-8'}),
            ('icuRules', {'localeProvider': 'i', 'locale': 'und', 'icuRules': '&a < b'}),
        ):
            target = canary_records(value, target=True)
            target['schema']['database'].update(change)
            a.parse_canaries(value, canary_bytes(target))
            with self.subTest(key=key), self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(source), canary_bytes(target))
        target = canary_records(value, target=True)
        target['schema']['database'].update(collationVersion='version-2', actualCollationVersion='version-2')
        for column in target['schema']['columns']:
            if column['collation'] is not None:
                column['collation']['actualVersion'] = 'version-2'
        a.parse_canaries(value, canary_bytes(target))
        with self.assertRaises(a.Refusal):
            a.compare_canaries(value, canary_bytes(source), canary_bytes(target))
        for key in database_semantics():
            changed = deepcopy(source)
            del changed['schema']['database'][key]
            with self.subTest(missing=key), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(changed))
        self.assertNotIn('localeProvider', value['source'])

    def test_column_acl_collation_and_storage_semantics_are_compared_as_structured_metadata(self):
        value = fixture()
        source = canary_records(value)
        for change in (
            {'acl': ['synthetic_admin=r/postgres']}, {'acl': []}, {'storage': 'm'}, {'compression': 'p'},
            {'statisticsTarget': 42},
            {'collation': {**default_collation(), 'name': 'C', 'provider': 'c', 'collate': 'C', 'ctype': 'C'}},
        ):
            target = canary_records(value, target=True)
            target['schema']['columns'][0].update(change)
            a.parse_canaries(value, canary_bytes(target))
            with self.subTest(change=change), self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(source), canary_bytes(target))
        # The portable identity alone is insufficient: provider semantics are
        # independently compared, including nondeterminism and version metadata.
        base = {**default_collation(), 'name': 'C', 'provider': 'c', 'collate': 'C', 'ctype': 'C'}
        source['schema']['columns'][0]['collation'] = deepcopy(base)
        for change in ({'encoding': 'UTF8'}, {'collate': 'POSIX'}, {'ctype': 'POSIX'},
                       {'version': 'version-1', 'actualVersion': 'version-1'}, {'deterministic': False},
                       {'provider': 'i', 'locale': 'und', 'collate': None, 'ctype': None, 'icuRules': '&a < b'}):
            target = canary_records(value, target=True)
            target['schema']['columns'][0]['collation'] = {**base, **change}
            a.parse_canaries(value, canary_bytes(target))
            with self.subTest(change=change), self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(source), canary_bytes(target))

    def test_locale_and_column_metadata_missing_or_incoherent_values_refuse(self):
        value = fixture()
        for change in (
            lambda r: r['schema']['columns'][0].pop('acl'),
            lambda r: r['schema']['columns'][0].pop('collation'),
            lambda r: r['schema']['columns'][0].update(collation=None),
            lambda r: r['schema']['columns'][0]['collation'].update(actualVersion='drift'),
            lambda r: r['schema']['database'].update(localeProvider='unsupported'),
            lambda r: r['schema']['database'].update(collationVersion='old', actualCollationVersion='new'),
            lambda r: r['schema']['database'].update(locale='unsupported-libc-locale'),
            lambda r: r['schema']['columns'][0].update(storage='invalid'),
            lambda r: r['schema']['columns'][0].update(compression='invalid'),
            lambda r: r['schema']['columns'][0].update(statisticsTarget=True),
            lambda r: r['schema']['columns'][0].update(statisticsTarget=10001),
            lambda r: r['schema']['columns'][0].update(options=['unsupported=value']),
        ):
            records = canary_records(value)
            change(records)
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))

    def test_relation_logged_default_storage_profile_refuses_unlogged_temp_and_unsupported_options(self):
        value = fixture()
        for kind in ('r', 'i', 'S'):
            for persistence in ('u', 't', ''):
                records = add_index(canary_records(value))
                relation = next(row for row in records['schema']['relations'] if row['kind'] == kind)
                relation['persistence'] = persistence
                with self.subTest(kind=kind, persistence=persistence), self.assertRaises(a.Refusal):
                    a.parse_canaries(value, canary_bytes(records))
        for change in ({'accessMethod': 'custom'}, {'accessMethod': None}, {'options': ['fillfactor=80']},
                       {'toastOptions': ['autovacuum_enabled=false']}, {'tablespace': 'custom_source_space'}):
            records = canary_records(value)
            records['schema']['relations'][0].update(change)
            with self.subTest(change=change), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        records = canary_records(value)
        records['schema']['database']['tablespace'] = 'custom_database_space'
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))

    def test_extended_statistics_labels_tablespaces_and_settings_census_refuses_without_values(self):
        value = fixture()
        for kind in ('extended-statistics', 'security-label', 'shared-security-label',
                     'relation-tablespace', 'database-tablespace', 'database-role-settings'):
            records = canary_records(value)
            records['unsupportedObjects']['otherObjects'] = [{'kind': kind, 'schema': None, 'name': 'synthetic'}]
            with self.subTest(kind=kind), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        sql = a.render_proposal(value)['canaryContract']['sqlByCategory']['unsupportedObjects']
        for catalog in ('pg_statistic_ext', 'pg_tablespace', 'pg_seclabel', 'pg_shseclabel', 'pg_db_role_setting'):
            self.assertIn(catalog, sql)
        for secret_or_path in ('setconfig', 's.label', 'pg_tablespace_location', 'subconninfo'):
            self.assertNotIn(secret_or_path, sql)

    def test_native_index_expression_and_include_slots_have_portable_exact_references(self):
        value = fixture()
        source, target = add_expression_index(canary_records(value)), add_expression_index(canary_records(value, target=True))
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        # A constant expression legitimately has no column dependency.
        for records in (source, target):
            records['indexes'][0].update(expressions='1', predicate=None, dependencyReferences=[], dependencyReferenceCount=0)
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        source, target = add_index(canary_records(value)), add_index(canary_records(value, target=True))
        for records in (source, target):
            index = records['indexes'][0]
            index['columnCount'] = 2
            index['columns'].append(slot(2, 1, 'body|line\nname', True, included=True))
            index['dependencyReferences'].append(dependency_reference(records['tableCensus'][0], 1, 'body|line\nname'))
            index['dependencyReferenceCount'] = 2
            records['schema']['columns'].append(native_column({'schema': index['schema'], 'name': index['index']},
                                                             2, 'body|line\nname', 'text'))
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])

    def test_index_reference_missing_duplicate_orphan_and_expression_contradictions_refuse(self):
        value = fixture()
        for change in (
            lambda r: r['indexes'][0]['columns'].clear(),
            lambda r: r['indexes'][0]['columns'].append(deepcopy(r['indexes'][0]['columns'][0])),
            lambda r: r['indexes'][0]['columns'][0].update(position=99),
            lambda r: r['indexes'][0]['columns'][0].update(column='missing'),
            lambda r: r['indexes'][0]['columns'][0].update(included=True),
            lambda r: r['indexes'][0]['columns'][0].update(expression=True),
            lambda r: r['indexes'][0]['dependencyReferences'].clear(),
            lambda r: r['indexes'][0]['dependencyReferences'][0].update(position=99),
            lambda r: r['indexes'][0]['dependencyReferences'][0].update(name=r['tableCensus'][1]['name']),
            lambda r: r['indexes'][0]['dependencyReferences'].append(deepcopy(r['indexes'][0]['dependencyReferences'][0])),
            lambda r: r['indexes'][0].update(keyColumnCount=2),
            lambda r: r['indexes'][0].update(expressions='unexpected-expression'),
            lambda r: r['indexes'][0].update(predicate=''),
        ):
            records = add_index(canary_records(value))
            change(records)
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        records = add_expression_index(canary_records(value))
        records['indexes'][0]['expressions'] = None
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))

    def test_foreign_key_referenced_table_columns_and_supporting_index_are_cross_checked(self):
        value = fixture()
        source, target = add_foreign_key(canary_records(value)), add_foreign_key(canary_records(value, target=True))
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        for change in (
            lambda c: c.update(referencedTable=None), lambda c: c['referencedTable'].update(name='missing'),
            lambda c: c['referencedColumns'].clear(), lambda c: c['referencedColumns'][0].update(column='missing'),
            lambda c: c['referencedColumns'][0].update(position=1), lambda c: c['columns'][0].update(position=99),
            lambda c: c['columns'].clear(), lambda c: c.update(supportingIndex=None),
            lambda c: c['supportingIndex'].update(name='missing'), lambda c: c.update(referenceColumnCount=0),
            lambda c: c['dependencyReferences'].pop(), lambda c: c['dependencyReferences'][0].update(position=99),
            lambda c: c['dependencyReferences'].append(deepcopy(c['dependencyReferences'][0])),
        ):
            altered = deepcopy(target)
            change(altered['constraints'][-1])
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(altered))
        # A plain check constraint also cannot lose its native column census.
        records = canary_records(value)
        records['constraints'][0]['columns'].clear()
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))

    def test_identity_columns_require_not_null_and_exactly_one_internally_owned_sequence(self):
        value = fixture()
        source, target = with_identity(canary_records(value)), with_identity(canary_records(value, target=True))
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        identity = lambda records: next(row for row in records['schema']['columns'] if row['identity'])
        for change in (
            lambda r: identity(r).update(notNull=False), lambda r: identity(r).update(default='1'),
            lambda r: identity(r).update(generated='s', default='1'),
            lambda r: r['sequences']['ownership'].clear(),
            lambda r: r['sequences']['ownership'][0].update(dependency='a'),
            lambda r: r['sequences']['ownership'][0].update(column='body|line\nname'),
            lambda r: identity(r).update(identity=''),
            lambda r: identity(r).update(type='integer'),
        ):
            records = deepcopy(source)
            change(records)
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        records = deepcopy(source)
        # A distinct, fully described second sequence cannot also own one identity.
        for category in ('sequenceCensus', 'sequenceStateBefore', 'sequenceStateAfter'):
            records[category].append({**records[category][0], 'name': 'second_sequence'})
        for key in ('definitions', 'ownership'):
            records['sequences'][key].append({**records['sequences'][key][0], 'name': 'second_sequence'})
        sequence = source['sequenceCensus'][0]
        records['schema']['relations'].append(native_relation({'schema': sequence['schema'], 'name': 'second_sequence'}, 'S'))
        records['schema']['columns'].extend({**column, 'name': 'second_sequence'}
            for column in source['schema']['columns'] if column['name'] == sequence['name'])
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))

    def test_generated_columns_require_expression_and_remain_distinct_from_identity_and_serial(self):
        value = fixture()
        source, target = canary_records(value), canary_records(value, target=True)
        for records in (source, target):
            records['schema']['columns'][0].update(generated='s', default='upper(body)')
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        for expression in (None, '', '   '):
            changed = deepcopy(source)
            changed['schema']['columns'][0]['default'] = expression
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(changed))
        records = with_identity(canary_records(value))
        col = next(row for row in records['schema']['columns'] if row['identity'])
        col.update(generated='v', default='1')
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))
        records = canary_records(value)
        records['schema']['columns'][1].update(generated='s', default='1')
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))

    def test_native_sequence_relation_has_exact_three_columns_with_fixed_properties(self):
        value = fixture()
        for change in (
            lambda rows: rows.pop(1), lambda rows: rows[1].update(column='wrong'),
            lambda rows: rows[1].update(type='integer'), lambda rows: rows[2].update(notNull=False),
            lambda rows: rows[1].update(position=4), lambda rows: rows[0].update(identity='a'),
            lambda rows: rows[1].update(generated='s', default='1'), lambda rows: rows[2].update(default='false'),
            lambda rows: rows.append({**rows[0], 'position': 4, 'column': 'extra'}),
        ):
            records = canary_records(value)
            name = records['sequenceCensus'][0]['name']
            columns = [row for row in records['schema']['columns'] if row['name'] == name]
            change(columns)
            records['schema']['columns'] = [row for row in records['schema']['columns'] if row['name'] != name] + columns
            with self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))

    def test_smallint_sequence_increment_is_independent_nonzero_int64(self):
        value = fixture()
        source, target = canary_records(value), canary_records(value, target=True)
        for records in (source, target):
            records['sequences']['definitions'][0].update(type='smallint', minimum='-32768', maximum='32767', increment='100000')
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        for increment in ('0', '9223372036854775808', '-9223372036854775809'):
            changed = deepcopy(source)
            changed['sequences']['definitions'][0]['increment'] = increment
            with self.subTest(increment=increment), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(changed))

    def test_source_builtin_authority_guard_rejects_independent_runtime_flag_drift(self):
        value = fixture()
        target = canary_records(value, target=True)
        for flag, bad in (('createDb', True), ('createRole', True), ('replication', True), ('bypassRls', True), ('inherit', False)):
            source = canary_records(value)
            source['roleIsolation']['roles'][2][flag] = bad
            a.parse_canaries(value, canary_bytes(source))
            with self.subTest(flag=flag), self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(source), canary_bytes(target))

    def test_source_custom_role_login_and_superuser_projection_are_independently_pinned(self):
        value = fixture()
        target = canary_records(value, target=True)
        for flag, bad in (('canLogin', False), ('superuser', True)):
            source = canary_records(value)
            source['roleIsolation']['roles'][1][flag] = bad
            a.parse_canaries(value, canary_bytes(source))
            with self.subTest(flag=flag), self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(source), canary_bytes(target))

    def test_new_catalog_sql_observes_portable_native_metadata_without_oids_secrets_or_execution(self):
        sql = a.render_proposal(fixture())['canaryContract']['sqlByCategory']
        for field in ('datlocprovider', 'datcollate', 'datctype', 'datlocale', 'daticurules', 'datcollversion',
                      'pg_database_collation_actual_version', 'a.attacl', 'a.attcollation', 'collprovider', 'colllocale',
                      'pg_collation_actual_version', 'attstorage', 'attcompression', 'attstattarget', 'relpersistence', 'reloptions'):
            self.assertIn(field, sql['schema'])
        for field in ('pg_index', 'indkey', 'indnatts', 'indnkeyatts', 'indexprs', 'indpred', 'pg_depend', 'WITH ORDINALITY'):
            self.assertIn(field, sql['indexes'])
        for field in ('conkey', 'confkey', 'confrelid', 'conindid', 'pg_depend', 'referencedColumns', 'dependencyReferences'):
            self.assertIn(field, sql['constraints'])
        for forbidden in ('rolpassword', 'pg_authid', 'pg_tablespace_location', 'subconninfo', 'setconfig', 'pg_get_functiondef'):
            self.assertNotIn(forbidden, '\n'.join(sql.values()))

    def test_unsupported_native_index_and_trigger_states_are_explicit_refusals(self):
        value = fixture()
        sql = a.render_proposal(value)['canaryContract']['sqlByCategory']['unsupportedObjects']
        for kind, native_condition in (
            ('unusable-index', 'NOT i.indisvalid OR NOT i.indisready OR NOT i.indislive'),
            ('nondefault-replica-identity', "c.relreplident <> 'd'"),
            ('replica-identity-index', 'i.indisreplident'),
            ('cluster-selected-index', 'i.indisclustered'),
            ('nonstandard-internal-constraint-trigger', "t.tgisinternal AND t.tgconstraint<>0 AND t.tgenabled <> 'O'"),
            ('operator-class-key-storage', 'op.opckeytype<>0'),
        ):
            self.assertIn(kind, sql)
            self.assertIn(native_condition, sql)
            records = canary_records(value)
            records['unsupportedObjects']['otherObjects'] = [{'kind': kind, 'schema': 'synthetic_telemetry', 'name': 'native-object'}]
            with self.subTest(kind=kind), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))

    def test_check_slots_match_column_dependency_census_including_zero_column_checks(self):
        value = fixture()
        for change in (
            lambda row: row.update(columns=[], columnCount=0),
            lambda row: row.update(dependencyReferences=[], dependencyReferenceCount=0),
            lambda row: row.update(columns=row['columns'] + [{**row['columns'][0], 'ordinal': 2}], columnCount=2),
        ):
            records = canary_records(value)
            change(records['constraints'][0])
            # Identical malformed transcripts must refuse as well.
            with self.assertRaises(a.Refusal):
                a.compare_canaries(value, canary_bytes(records), canary_bytes(records))
        source, target = canary_records(value), canary_records(value, target=True)
        for records in (source, target):
            records['constraints'][0].update(columns=[], columnCount=0, dependencyReferences=[], dependencyReferenceCount=0,
                                             definition='CHECK (true)')
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])

    def test_direct_index_column_base_type_matches_and_typmods_can_differ(self):
        value = fixture()
        records = add_index(canary_records(value))
        records['schema']['columns'][-1]['type'] = 'boolean'
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))
        source, target = add_index(canary_records(value)), add_index(canary_records(value, target=True))
        for records in (source, target):
            table = records['tableCensus'][0]
            column = next(row for row in records['schema']['columns'] if row['name'] == table['name'] and row['column'] == 'id')
            column.update(type='character varying(24)', collation=default_collation(), storage='x')
            records['schema']['columns'][-1].update(type='character varying', collation=default_collation(), storage='x')
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])

    def test_index_columns_cannot_declare_identity_or_generated_properties(self):
        value = fixture()
        for changes in ({'identity': 'a', 'notNull': True}, {'generated': 's', 'default': '1'}):
            records = add_index(canary_records(value))
            records['schema']['columns'][-1].update(changes)
            with self.subTest(changes=changes), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))

    def test_builtin_scalar_and_array_collation_presence_and_catalog_scope(self):
        value = fixture()
        for typ in ('text', 'text[]', 'name', 'name[]', 'character varying(24)[]', 'character(8)[]'):
            records = canary_records(value)
            records['schema']['columns'][0].update(type=typ, collation=default_collation())
            a.parse_canaries(value, canary_bytes(records))
            records['schema']['columns'][0]['collation'] = None
            with self.subTest(missing=typ), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        for typ in ('bigint', 'bigint[]', 'boolean', 'uuid', 'numeric(10,2)', 'jsonb[]'):
            records = canary_records(value)
            records['schema']['columns'][0].update(type=typ, collation=None)
            a.parse_canaries(value, canary_bytes(records))
            records['schema']['columns'][0]['collation'] = default_collation()
            with self.subTest(extra=typ), self.assertRaises(a.Refusal):
                a.parse_canaries(value, canary_bytes(records))
        records = canary_records(value)
        records['schema']['columns'][0]['collation'] = {
            **default_collation(), 'schema': 'synthetic_telemetry', 'name': 'custom_c', 'provider': 'c', 'collate': 'C', 'ctype': 'C'}
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))

    def test_constraint_supporting_index_must_belong_to_exact_existing_table(self):
        value = fixture()
        records = add_index(add_foreign_key(canary_records(value)), 0, 'wrong_table_support')
        # The existing wrong-table index has the same key position and base
        # type, so only the native table-ownership guard distinguishes it.
        records['constraints'][-1]['supportingIndex'] = {'schema': records['tableCensus'][0]['schema'], 'name': 'wrong_table_support'}
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))

    def test_constraint_deferred_flag_requires_deferrable_with_valid_positive_modes(self):
        value = fixture()
        for initially_deferred in (False, True):
            source, target = add_foreign_key(canary_records(value)), add_foreign_key(canary_records(value, target=True))
            for records in (source, target):
                records['constraints'][-1].update(deferrable=True, initiallyDeferred=initially_deferred)
            self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        records = add_foreign_key(canary_records(value))
        records['constraints'][-1].update(deferrable=False, initiallyDeferred=True)
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(records))

    def test_native_not_null_constraint_requires_one_not_null_column(self):
        value = fixture()
        source, target = canary_records(value), canary_records(value, target=True)
        for records in (source, target):
            table = records['tableCensus'][0]
            next(row for row in records['schema']['columns'] if row['name'] == table['name'] and row['column'] == 'id')['notNull'] = True
            records['constraints'].append(native_constraint(table, 'native_not_null', 'n', [slot(1, 2, 'id')], 'NOT NULL id',
                                                          [dependency_reference(table, 2, 'id')]))
        self.assertTrue(a.compare_canaries(value, canary_bytes(source), canary_bytes(target))['consistent'])
        changed = deepcopy(source)
        changed['schema']['columns'][1]['notNull'] = False
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(changed))
        changed = deepcopy(source)
        constraint = changed['constraints'][-1]
        constraint['columns'].append(slot(2, 1, 'body|line\nname'))
        constraint['columnCount'] = 2
        constraint['dependencyReferences'].append(dependency_reference(changed['tableCensus'][0], 1, 'body|line\nname'))
        constraint['dependencyReferenceCount'] = 2
        changed['schema']['columns'][0]['notNull'] = True
        with self.assertRaises(a.Refusal):
            a.parse_canaries(value, canary_bytes(changed))


class CliTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.root.chmod(0o700)

    def tearDown(self):
        self.temporary.cleanup()

    def write(self, name, raw):
        path = self.root / name
        path.write_bytes(raw)
        path.chmod(0o600)
        return path

    def invoke(self, argv):
        output = io.StringIO()
        with redirect_stdout(output):
            code = a.main(argv)
        return code, json.loads(output.getvalue())

    def test_cli_bounded_exact_input_neutral_summary_and_private_create_only_output(self):
        v = fixture()
        raw = a.canonical(v)
        source = self.write('input.json', raw)
        target = self.root / 'private-plan.json'
        args = ['proposal', '--input', str(source), '--sha256', a.digest(raw), '--private-output', str(target)]
        with patch.object(subprocess, 'Popen', side_effect=AssertionError('no process')), \
                patch.object(socket, 'socket', side_effect=AssertionError('no socket')):
            code, summary = self.invoke(args)
        self.assertEqual(code, 0)
        self.assertEqual(summary['backendRequests'], 0)
        self.assertFalse(summary['runnable'])
        self.assertEqual(summary['privatePlanSha256'], a.digest(target.read_bytes()))
        self.assertEqual(target.stat().st_mode & 0o777, 0o600)
        for secret in ('synthetic-source', 'synthetic_telemetry', '192.0.2.10', 'synthetic_admin',
                       'source-namespace-uid', 'synthetic-writer', 'memories/'):
            self.assertNotIn(secret, json.dumps(summary))
        before = target.read_bytes()
        code, summary = self.invoke(args)
        self.assertEqual(code, 1)
        self.assertEqual(summary['result'], a.FAILURE)
        self.assertEqual(target.read_bytes(), before)

    def test_cli_activation_unknown_arguments_refused_before_input_or_credentials(self):
        for activation in ('execute', 'capture', 'restore', 'resume', 'cleanup', '--live', '--activate',
                           '--execute', '--unknown', '--help'):
            with patch.object(a.c, 'read_exact_local', side_effect=AssertionError('no local read')):
                code, summary = self.invoke([activation, '--input', '/not/a/credential', '--sha256', H])
            self.assertEqual(code, 1)
            self.assertEqual(summary['result'], a.FAILURE)
            self.assertEqual(summary['backendRequests'], 0)

    def test_cli_hash_permissions_symlink_hardlink_relative_and_oversize_refused(self):
        raw = a.canonical(fixture())
        source = self.write('input.json', raw)
        def call(path=source, sha=None):
            return self.invoke(['proposal', '--input', str(path), '--sha256', sha or a.digest(raw)])[0]
        self.assertEqual(call(sha=K), 1)
        source.chmod(0o644)
        self.assertEqual(call(), 1)
        source.chmod(0o600)
        linked = self.root / 'linked.json'
        linked.symlink_to(source)
        self.assertEqual(call(linked), 1)
        linked.unlink()
        os.link(source, linked)
        self.assertEqual(call(), 1)
        linked.unlink()
        self.assertEqual(call(Path('input.json')), 1)
        source.write_bytes(b' ' * (a.c.MAX_JSON + 1))
        self.assertEqual(call(sha=a.digest(source.read_bytes())), 1)

    def test_cli_failure_does_not_echo_raw_private_input(self):
        value = fixture()
        value['source']['database'] = "secret-sentinel' host=evil"
        repin(value)
        raw = a.canonical(value)
        source = self.write('input.json', raw)
        code, result = self.invoke(['proposal', '--input', str(source), '--sha256', a.digest(raw)])
        self.assertEqual(code, 1)
        self.assertEqual(result['result'], a.FAILURE)
        self.assertNotIn('secret-sentinel', json.dumps(result))


if __name__ == '__main__':
    unittest.main(verbosity=2)
