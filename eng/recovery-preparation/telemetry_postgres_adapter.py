#!/usr/bin/env python3
"""Deterministic PostgreSQL 18 proposals. No dispatcher or credential loader.

Every argv/SQL/manifest returned here is private, unexecuted proposal material.
The pure runtime constructors check binding consistency, never authenticate it.
"""

import os
from datetime import timedelta
from pathlib import Path
import re
import stat
import sys
import types


COMMON_SOURCE_SHA256 = 'c9330eacfe3ad174920d0def0ea1112fd75463dac52556f8ee6d92dfe67662e0'
FAILURE = 'telemetry-postgresql-proposal-refused'


def _common():
    # Compile only these exact sibling bytes, never resolve a helper via sys.path.
    # This small bootstrap also checks ancestors before executing shared custody.
    import hashlib
    path = Path(__file__).absolute().with_name('recovery_adapters.py')
    directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    fd = None
    try:
        for part in path.parent.parts[1:]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC,
                            dir_fd=directory)
            os.close(directory)
            directory = child
            info = os.fstat(directory)
            if info.st_uid not in (0, os.getuid()) or (info.st_mode & 0o022 and
                    not (info.st_uid == 0 and info.st_mode & stat.S_ISVTX)):
                raise ValueError(FAILURE)
        fd = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC,
                     dir_fd=directory)
        before = os.fstat(fd)
        if not (stat.S_ISREG(before.st_mode) and before.st_nlink == 1 and
                before.st_uid in (0, os.getuid()) and not before.st_mode & 0o022 and
                0 < before.st_size <= 262144):
            raise ValueError(FAILURE)
        raw = bytearray()
        while len(raw) <= 262144:
            part = os.read(fd, min(65536, 262145 - len(raw)))
            if not part:
                break
            raw.extend(part)
        after = os.fstat(fd)
        if (len(raw) != before.st_size or any(getattr(before, k) != getattr(after, k) for k in
                ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns', 'st_mode', 'st_nlink', 'st_uid')) or
                hashlib.sha256(raw).hexdigest() != COMMON_SOURCE_SHA256):
            raise ValueError(FAILURE)
        module = types.ModuleType('_telemetry_exact_recovery_primitives')
        sys.modules[module.__name__] = module
        exec(compile(bytes(raw), str(path), 'exec', dont_inherit=True), module.__dict__)
        return module
    finally:
        if fd is not None:
            os.close(fd)
        os.close(directory)


try:
    c = _common()
except Exception:
    if __name__ == '__main__':
        print('{"backendRequests":0,"operationalGate":"closed","result":"' + FAILURE + '"}')
        sys.exit(1)
    raise ValueError(FAILURE) from None

canonical, digest, strict_json = c.canonical, c.digest, c.strict_json


class Refusal(ValueError):
    """A fixed failure that never contains raw private input."""


def require(condition):
    if not condition:
        raise Refusal(FAILURE)


def _safe(function):
    def checked(*args, **kwargs):
        try:
            # Every public contract owns every nested JSON value, even when a
            # future constructor adds a caller-supplied receipt to its output.
            return strict_json(canonical(function(*args, **kwargs)))
        except Exception:
            raise Refusal(FAILURE) from None
    return checked


def fields(value, expected):
    c.fields(value, expected.split() if type(expected) is str else expected)


def identifier(value):
    # A deliberately narrow PostgreSQL identifier profile also rules out URI/
    # conninfo overrides in --dbname, option injection and psql meta-commands.
    c.text(value, 63, r'[A-Za-z_][A-Za-z0-9_]*')
    return value


def quote(value):
    return '"' + identifier(value) + '"'


def literal(value):
    identifier(value)
    return "'" + value + "'"


def name(value):
    return c.text(value, 63, r'[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?')


def path(value):
    c.absolute_path(value)
    require(re.fullmatch(r'/[A-Za-z0-9_./\-]+', value) is not None)
    require(all(part.lower() not in c.ALIASES for part in Path(value).parts[1:]))
    return value


def image(value):
    require(type(value) is str and re.fullmatch(
        r'[a-z0-9][a-z0-9._/:\-]{0,254}@sha256:[0-9a-f]{64}', value) is not None)
    return value


def image_id(value):
    require(type(value) is str)
    candidate = value.removeprefix('docker-pullable://')
    image(candidate)
    return value


RESOURCE_TYPES = {
    'Namespace': ('v1', 'namespaces', False),
    'Pod': ('v1', 'pods', True),
    'PersistentVolumeClaim': ('v1', 'persistentvolumeclaims', True),
    'PersistentVolume': ('v1', 'persistentvolumes', False),
    'StorageClass': ('storage.k8s.io/v1', 'storageclasses', False),
    'StatefulSet': ('apps/v1', 'statefulsets', True),
    'Deployment': ('apps/v1', 'deployments', True),
    'Secret': ('v1', 'secrets', True),
    'NetworkPolicy': ('networking.k8s.io/v1', 'networkpolicies', True),
}


def resource(value, kind, namespace=None, proposed=False):
    fields(value, 'apiVersion kind namespace name uid resourceVersion')
    api, _, namespaced = RESOURCE_TYPES[kind]
    require(value['kind'] == kind and value['apiVersion'] == api)
    name(value['name'])
    require(value['namespace'] == (namespace if namespaced else ''))
    if proposed:
        require(value['uid'] is None and value['resourceVersion'] is None)
    else:
        c.text(value['uid'])
        c.text(value['resourceVersion'], 64, r'[1-9][0-9]*')
    return value


def api_path(value):
    api, plural, namespaced = RESOURCE_TYPES[value['kind']]
    prefix = '/api/v1' if api == 'v1' else '/apis/' + api
    return prefix + ('/namespaces/' + value['namespace'] if namespaced else '') + '/' + plural + '/' + value['name']


def volume(value, pv_uid):
    fields(value, 'pvUid nodeUid path')
    require(c.VolumePath.parse(value).pv_uid == pv_uid)
    return value


def quantity(value, kind):
    patterns = {'storage': r'([1-9][0-9]{0,5})(Mi|Gi|Ti)',
                'cpu': r'([1-9][0-9]{0,5})(m?)',
                'memory': r'([1-9][0-9]{0,5})(Mi|Gi)'}
    require(type(value) is str)
    match = re.fullmatch(patterns[kind], value)
    require(match is not None)
    factors = {'': 1000, 'm': 1, 'Mi': 1024**2, 'Gi': 1024**3, 'Ti': 1024**4}
    return int(match[1]) * factors[match[2]]


def _normalized_resources(resources):
    return {key: {'cpu': str(quantity(item['cpu'], 'cpu')) + 'm',
                  'memory': str(quantity(item['memory'], 'memory') // 1024**2) + 'Mi'}
            for key, item in resources.items()}


@_safe
def validate(value):
    fields(value, 'kind schemaVersion observedAt parentProposalSha256 retainedDecisionsSha256 evidence '
                  'source sourcePinSha256 worker clock target transfer')
    require(value['kind'] == 'hexalith.telemetry-postgresql-proposal-input' and
            type(value['schemaVersion']) is int and value['schemaVersion'] == 1)
    c.timestamp(value['observedAt'])
    c.sha(value['parentProposalSha256'])
    c.sha(value['retainedDecisionsSha256'])
    fields(value['evidence'], 'observationSha256 metadataSha256 storageSha256 toolHashesSha256 identitySha256')
    for item in value['evidence'].values():
        c.sha(item)
    s = value['source']
    fields(s, 'cluster namespace statefulSet pod container image imageId containerId restartCount '
              'pvc pv storageClass volumePath database user peerOsUser socket port systemIdentifier '
              'timeline serverVersionNum schemas tables extensions roles configuration tools')
    require(c.sha(value['sourcePinSha256']) == digest(canonical(s)))
    c.Cluster.parse(s['cluster'])
    resource(s['namespace'], 'Namespace')
    ns = s['namespace']['name']
    for key, kind in (('statefulSet', 'StatefulSet'), ('pod', 'Pod'),
                      ('pvc', 'PersistentVolumeClaim'), ('pv', 'PersistentVolume'), ('storageClass', 'StorageClass')):
        resource(s[key], kind, ns)
    require(len({s[k]['uid'] for k in ('namespace', 'statefulSet', 'pod', 'pvc', 'pv', 'storageClass')}) == 6)
    name(s['container'])
    image(s['image'])
    image_id(s['imageId'])
    require(s['image'].rsplit('@sha256:', 1)[1] == s['imageId'].rsplit('@sha256:', 1)[1])
    c.text(s['containerId'], 128, r'(?:containerd|docker)://[0-9a-f]{64}')
    c.integer(s['restartCount'], 0, 1000000)
    if s['volumePath'] is not None:
        volume(s['volumePath'], s['pv']['uid'])
    for key in ('database', 'user', 'peerOsUser'):
        identifier(s[key])
    path(s['socket'])
    c.integer(s['port'], 1, 65535)
    c.text(s['systemIdentifier'], 20, r'[1-9][0-9]*')
    require(int(s['systemIdentifier']) <= 2**64 - 1)
    c.integer(s['timeline'], 1, 2**32 - 1)
    c.integer(s['serverVersionNum'], 180000, 189999)
    require(type(s['schemas']) is list and len(s['schemas']) == 2)
    require(len(set(identifier(x) for x in s['schemas'])) == 2)
    require(type(s['tables']) is list and len(s['tables']) == 2)
    for table in s['tables']:
        fields(table, 'schema name')
        identifier(table['name'])
        require(identifier(table['schema']) in s['schemas'])
    require(len({(t['schema'], t['name']) for t in s['tables']}) == 2)
    require(type(s['extensions']) is list and 0 < len(s['extensions']) <= 32)
    for extension in s['extensions']:
        fields(extension, 'name schema version')
        identifier(extension['name'])
        identifier(extension['schema'])
        c.text(extension['version'], 32, r'[0-9][0-9.\-]*')
        require(extension['name'] in ('plpgsql', 'pgstattuple'))
    require(len({x['name'] for x in s['extensions']}) == len(s['extensions']))
    require('plpgsql' in {x['name'] for x in s['extensions']})
    require(type(s['roles']) is list and 0 < len(s['roles']) <= 128)
    for role in s['roles']:
        fields(role, 'name canLogin superuser')
        identifier(role['name'])
        require(type(role['canLogin']) is bool and type(role['superuser']) is bool)
    require(len({r['name'] for r in s['roles']}) == len(s['roles']) and
            s['user'] in {r['name'] for r in s['roles']})
    require(type(s['configuration']) is list and 0 < len(s['configuration']) <= 16)
    for item in s['configuration']:
        fields(item, 'name sha256 size')
        c.text(item['name'], 128, r'[A-Za-z0-9][A-Za-z0-9_.\-]*')
        c.sha(item['sha256'])
        c.integer(item['size'], 1, 1048576)
    require(len({x['name'] for x in s['configuration']}) == len(s['configuration']))
    fields(s['tools'], 'psql pg_dump pg_restore')
    for key, tool in s['tools'].items():
        fields(tool, 'path sha256')
        require(Path(path(tool['path'])).name == key)
        c.sha(tool['sha256'])
    require(len({t['path'] for t in s['tools'].values()}) == 3)
    for key, replicas in (('worker', 1), ('clock', 0)):
        fields(value[key], 'resource previousReplicas')
        resource(value[key]['resource'], 'Deployment', ns)
        require(type(value[key]['previousReplicas']) is int and value[key]['previousReplicas'] == replicas)
    require(value['worker']['resource']['uid'] != value['clock']['resource']['uid'] and
            api_path(value['worker']['resource']) != api_path(value['clock']['resource']))
    t = value['target']
    fields(t, 'cluster namespace statefulSetName pvcName secretName networkPolicyName container '
              'storageClass storage runAsUser runAsGroup fsGroup resources socket port bootstrapUser')
    target_cluster, source_cluster = c.Cluster.parse(t['cluster']), c.Cluster.parse(s['cluster'])
    require(target_cluster == source_cluster if target_cluster.uid == source_cluster.uid else
            target_cluster.endpoint != source_cluster.endpoint)
    resource(t['namespace'], 'Namespace', proposed=True)
    require(t['namespace']['name'] != ns)
    for key in ('statefulSetName', 'pvcName', 'secretName', 'networkPolicyName', 'container'):
        name(t[key])
    require(t['pvcName'] != s['pvc']['name'] and t['statefulSetName'] != s['statefulSet']['name'])
    fields(t['storageClass'], 'resource reclaimPolicy')
    resource(t['storageClass']['resource'], 'StorageClass')
    require(t['storageClass']['reclaimPolicy'] == 'Delete')
    quantity(t['storage'], 'storage')
    for key in ('runAsUser', 'runAsGroup', 'fsGroup'):
        c.integer(t[key], 1, 2147483647)
    fields(t['resources'], 'requests limits')
    for limits in t['resources'].values():
        fields(limits, 'cpu memory')
        quantity(limits['cpu'], 'cpu')
        quantity(limits['memory'], 'memory')
    for kind in ('cpu', 'memory'):
        require(quantity(t['resources']['requests'][kind], kind) <=
                quantity(t['resources']['limits'][kind], kind))
    path(t['socket'])
    socket_path = Path(t['socket'])
    for reserved in ('/tmp', '/var/lib/postgresql', '/run/credentials'):
        reserved_path = Path(reserved)
        require(not (socket_path == reserved_path or socket_path in reserved_path.parents or
                     reserved_path in socket_path.parents))
    c.integer(t['port'], 1, 65535)
    # The isolated stock-image OS postgres peer is separate from source auth.
    require(t['bootstrapUser'] == 'postgres')
    transfer = value['transfer']
    fields(transfer, 'store key writerPrincipal validatorPrincipal recipientSha256 '
                     'retentionDays maximumProofAgeSeconds')
    require(type(transfer['retentionDays']) is int and transfer['retentionDays'] == 30 and
            type(transfer['maximumProofAgeSeconds']) is int and transfer['maximumProofAgeSeconds'] == 86400)
    c.text(transfer['store'])
    c.text(transfer['writerPrincipal'])
    if transfer['validatorPrincipal'] is not None:
        require(c.text(transfer['validatorPrincipal']) != transfer['writerPrincipal'])
    if transfer['recipientSha256'] is not None:
        c.sha(transfer['recipientSha256'])
    require(type(transfer['key']) is str and transfer['key'].startswith('memories/') and
            len(transfer['key']) > len('memories/'))
    c.Artifact.parse({'store': transfer['store'], 'key': transfer['key'], 'version': 'unexecuted-proposal',
                      'sha256': '0' * 64, 'size': 1, 'retainUntil': '2100-01-01T00:00:00Z'})
    require(len(canonical(value)) <= c.MAX_JSON)
    return value


def _argv(value, role, target=False):
    s, t = value['source'], value['target']
    connection = t if target else s
    user = t['bootstrapUser'] if target else s['user']
    result = ['/usr/bin/env', '-i', 'LANG=C', 'LC_ALL=C', 'PGCLIENTENCODING=UTF8',
              'PGPASSFILE=/dev/null', 'PGSERVICEFILE=/dev/null', 'PGCONNECT_TIMEOUT=5',
              'PGAPPNAME=telemetry-recovery-proposal', s['tools'][role]['path'],
              '--host=' + connection['socket'], '--port=' + str(connection['port']),
              '--username=' + user, '--dbname=' + s['database'], '--no-password']
    if role == 'psql':
        result.extend(['--no-psqlrc', '--quiet', '--no-align', '--tuples-only', '--set=ON_ERROR_STOP=1'])
    return result


MAX_CANARY_ROWS = 100000
MIN_CANARY_ROW_BYTES = 2  # Even an empty JSON row object occupies two bytes.
MAX_CANARY_ROW_BYTES = 65536
MAX_CANARY_CONTENT_BYTES = 16 * 1024 * 1024
MAX_CATALOG_ROWS = 4096
EQUAL_CATEGORIES = ('tableCensus schema dataCounts dataContents constraints indexes sequences '
                    'sequenceCensus sequenceStateBefore sequenceStateAfter extensions acls unsupportedObjects').split()
DIFFERENT_CATEGORIES = 'sessionIdentity roleIsolation databaseActivation'.split()
CANARY_CATEGORIES = EQUAL_CATEGORIES + DIFFERENT_CATEGORIES
UNSUPPORTED_CATEGORIES = 'routines triggers rowSecurity policies types otherObjects'.split()
ROLE_FLAGS = 'canLogin superuser createDb createRole replication bypassRls inherit'.split()
SEQUENCE_TYPES = {'smallint': (-(2**15), 2**15 - 1), 'integer': (-(2**31), 2**31 - 1),
                  'bigint': (-(2**63), 2**63 - 1)}


def _array_sql(query):
    # Each subquery specifies stable ordering. JSON escaping keeps embedded
    # separators/newlines distinct; an absent ACL is JSON null, not an empty ACL.
    return "(SELECT COALESCE(json_agg(row_to_json(q)), '[]'::json) FROM (" + query + ') q)'


def _record_sql(category, payload):
    require(category in CANARY_CATEGORIES)
    return "SELECT json_build_object('category', '" + category + "', 'payload', " + payload + ')::text;'


def _sequence_state_sql(category):
    # Catalog-derived names can contain quotes/newlines. %I quotes identifiers,
    # %L quotes payload literals, and the outer %L quotes the category. gexec
    # runs only generated SELECTs, never mutations, functions or shell commands.
    empty = ('SELECT NULL::text AS schema, NULL::text AS name, NULL::text AS "lastValue", '
             'NULL::boolean AS "isCalled" WHERE false')
    template = ("SELECT json_build_object('category', %L, 'payload', "
                "COALESCE(json_agg(row_to_json(q) ORDER BY schema COLLATE \"C\", name COLLATE \"C\"), "
                "'[]'::json))::text FROM (%s) q;")
    inner = ('SELECT %L::text AS schema, %L::text AS name, last_value::text AS "lastValue", '
             'is_called AS "isCalled" FROM %I.%I')
    def sql_string(text):
        return "'" + text.replace("'", "''") + "'"
    return ('SELECT format(' + sql_string(template) + ', ' + sql_string(category) + ', '
            'COALESCE(string_agg(format(' + sql_string(inner) + ', n.nspname, c.relname, '
            'n.nspname, c.relname), \' UNION ALL \' ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C"), '
            + sql_string(empty) + ')) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace '
            "WHERE c.relkind='S' AND n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'\n\\gexec")


def _unsupported_objects_sql():
    """Only census identifiers; no routine bodies, trigger execution or secrets."""
    namespace = "n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'"
    def not_extension(catalog, oid):
        return ("NOT EXISTS (SELECT 1 FROM pg_depend ed WHERE ed.classid='" + catalog +
                "'::regclass AND ed.objid=" + oid + " AND ed.refclassid='pg_extension'::regclass "
                "AND ed.deptype='e')")
    routines = _array_sql('SELECT n.nspname AS schema, p.proname AS name, '
        'pg_get_function_identity_arguments(p.oid) AS arguments, p.prokind AS kind FROM pg_proc p '
        'JOIN pg_namespace n ON n.oid=p.pronamespace WHERE ' + namespace + ' AND ' +
        not_extension('pg_proc', 'p.oid') + ' ORDER BY n.nspname COLLATE "C", p.proname COLLATE "C", 3')
    triggers = _array_sql('SELECT n.nspname AS schema, c.relname AS name, t.tgname AS trigger '
        'FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid '
        'JOIN pg_namespace n ON n.oid=c.relnamespace WHERE NOT t.tgisinternal AND ' + namespace +
        ' ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C", t.tgname COLLATE "C"')
    rls = _array_sql('SELECT n.nspname AS schema, c.relname AS name, c.relrowsecurity AS enabled, '
        'c.relforcerowsecurity AS forced FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace '
        'WHERE (c.relrowsecurity OR c.relforcerowsecurity) AND ' + namespace +
        ' ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C"')
    policies = _array_sql('SELECT n.nspname AS schema, c.relname AS name, p.polname AS policy '
        'FROM pg_policy p JOIN pg_class c ON c.oid=p.polrelid '
        'JOIN pg_namespace n ON n.oid=c.relnamespace WHERE ' + namespace +
        ' ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C", p.polname COLLATE "C"')
    # Automatic table row types and automatic array types are already described
    # by their base relation/type. Standalone composites/domains/enums/ranges/
    # base types are unsupported. Extension members have only pinned extension
    # identity/version coverage, never a claim of function/type body equality.
    types = _array_sql('SELECT n.nspname AS schema, t.typname AS name, t.typtype AS kind '
        'FROM pg_type t JOIN pg_namespace n ON n.oid=t.typnamespace '
        'LEFT JOIN pg_class c ON c.oid=t.typrelid WHERE ' + namespace +
        " AND (t.typtype <> 'c' OR c.relkind='c' OR c.oid IS NULL) "
        'AND NOT EXISTS (SELECT 1 FROM pg_type base WHERE base.typarray=t.oid) AND ' +
        not_extension('pg_type', 't.oid') + ' ORDER BY n.nspname COLLATE "C", t.typname COLLATE "C"')
    others = []
    for catalog, column, namespace_column, object_kind in (
        ('pg_operator', 'oprname', 'oprnamespace', 'operator'),
        ('pg_opclass', 'opcname', 'opcnamespace', 'operator-class'),
        ('pg_opfamily', 'opfname', 'opfnamespace', 'operator-family'),
        ('pg_collation', 'collname', 'collnamespace', 'collation'),
        ('pg_conversion', 'conname', 'connamespace', 'conversion'),
        ('pg_ts_config', 'cfgname', 'cfgnamespace', 'text-search-configuration'),
        ('pg_ts_dict', 'dictname', 'dictnamespace', 'text-search-dictionary'),
        ('pg_ts_parser', 'prsname', 'prsnamespace', 'text-search-parser'),
        ('pg_ts_template', 'tmplname', 'tmplnamespace', 'text-search-template'),
    ):
        others.append("SELECT '" + object_kind + "'::text AS kind, n.nspname::text AS schema, "
            'o.' + column + '::text AS name FROM ' + catalog + ' o JOIN pg_namespace n ON n.oid=o.' +
            namespace_column + ' WHERE ' + namespace + ' AND ' + not_extension(catalog, 'o.oid'))
    others.extend([
        "SELECT 'unusable-index'::text AS kind, n.nspname::text AS schema, ic.relname::text AS name "
        'FROM pg_index i JOIN pg_class c ON c.oid=i.indrelid JOIN pg_class ic ON ic.oid=i.indexrelid '
        'JOIN pg_namespace n ON n.oid=c.relnamespace '
        'WHERE (NOT i.indisvalid OR NOT i.indisready OR NOT i.indislive) AND ' + namespace,
        "SELECT 'nondefault-replica-identity'::text AS kind, n.nspname::text AS schema, c.relname::text AS name "
        'FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace '
        "WHERE c.relkind IN ('r','p') AND c.relreplident <> 'd' AND " + namespace,
        "SELECT 'replica-identity-index'::text AS kind, n.nspname::text AS schema, ic.relname::text AS name "
        'FROM pg_index i JOIN pg_class c ON c.oid=i.indrelid JOIN pg_class ic ON ic.oid=i.indexrelid '
        'JOIN pg_namespace n ON n.oid=c.relnamespace WHERE i.indisreplident AND ' + namespace,
        "SELECT 'cluster-selected-index'::text AS kind, n.nspname::text AS schema, ic.relname::text AS name "
        'FROM pg_index i JOIN pg_class c ON c.oid=i.indrelid JOIN pg_class ic ON ic.oid=i.indexrelid '
        'JOIN pg_namespace n ON n.oid=c.relnamespace WHERE i.indisclustered AND ' + namespace,
        "SELECT 'nonstandard-internal-constraint-trigger'::text AS kind, n.nspname::text AS schema, t.tgname::text AS name "
        'FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace '
        "WHERE t.tgisinternal AND t.tgconstraint<>0 AND t.tgenabled <> 'O' AND " + namespace,
        "SELECT 'operator-class-key-storage'::text AS kind, n.nspname::text AS schema, ic.relname::text AS name "
        'FROM pg_index i JOIN pg_class c ON c.oid=i.indrelid JOIN pg_class ic ON ic.oid=i.indexrelid '
        'JOIN pg_namespace n ON n.oid=c.relnamespace '
        'JOIN LATERAL unnest(i.indclass::oid[]) AS key(opclass) ON true '
        'JOIN pg_opclass op ON op.oid=key.opclass WHERE op.opckeytype<>0 AND ' + namespace,
        "SELECT 'rule'::text AS kind, n.nspname::text AS schema, r.rulename::text AS name "
        'FROM pg_rewrite r JOIN pg_class c ON c.oid=r.ev_class '
        'JOIN pg_namespace n ON n.oid=c.relnamespace WHERE ' + namespace,
        "SELECT 'inheritance'::text AS kind, n.nspname::text AS schema, c.relname::text AS name "
        'FROM pg_inherits i JOIN pg_class c ON c.oid=i.inhrelid '
        'JOIN pg_namespace n ON n.oid=c.relnamespace WHERE ' + namespace,
        "SELECT 'large-object'::text AS kind, NULL::text AS schema, 'present'::text AS name "
        'WHERE EXISTS (SELECT 1 FROM pg_largeobject_metadata)',
    ])
    for catalog, column, object_kind in (
        ('pg_event_trigger', 'evtname', 'event-trigger'), ('pg_publication', 'pubname', 'publication'),
        ('pg_subscription', 'subname', 'subscription'), ('pg_foreign_server', 'srvname', 'foreign-server'),
        ('pg_foreign_data_wrapper', 'fdwname', 'foreign-data-wrapper'), ('pg_language', 'lanname', 'language'),
    ):
        extra = " AND o.lanname NOT IN ('internal','c','sql')" if catalog == 'pg_language' else ''
        # pg_subscription is shared: inspect only this database, never conninfo.
        if catalog == 'pg_subscription':
            extra += ' AND EXISTS (SELECT 1 FROM pg_database d WHERE d.oid=o.subdbid AND d.datname=current_database())'
        others.append("SELECT '" + object_kind + "'::text AS kind, NULL::text AS schema, o." + column +
            '::text AS name FROM ' + catalog + ' o WHERE ' + not_extension(catalog, 'o.oid') + extra)
    others.append("SELECT 'cast'::text AS kind, NULL::text AS schema, "
        "format_type(c.castsource,NULL) || '->' || format_type(c.casttarget,NULL) AS name "
        'FROM pg_cast c JOIN pg_type src ON src.oid=c.castsource JOIN pg_type dst ON dst.oid=c.casttarget '
        'JOIN pg_namespace sn ON sn.oid=src.typnamespace JOIN pg_namespace dn ON dn.oid=dst.typnamespace '
        "WHERE (c.oid >= 16384 OR sn.nspname !~ '^pg_' OR dn.nspname !~ '^pg_') AND " +
        not_extension('pg_cast', 'c.oid'))
    others.append("SELECT 'access-method'::text AS kind, NULL::text AS schema, o.amname::text AS name "
                  'FROM pg_am o WHERE o.oid >= 16384 AND ' + not_extension('pg_am', 'o.oid'))
    others.append("SELECT 'transform'::text AS kind, NULL::text AS schema, "
                  "format_type(o.trftype,NULL) || '/' || l.lanname AS name FROM pg_transform o "
                  'JOIN pg_language l ON l.oid=o.trflang WHERE ' + not_extension('pg_transform', 'o.oid'))
    others.extend([
        "SELECT 'extended-statistics'::text AS kind, n.nspname::text AS schema, s.stxname::text AS name "
        'FROM pg_statistic_ext s JOIN pg_namespace n ON n.oid=s.stxnamespace WHERE ' + namespace,
        "SELECT 'relation-tablespace'::text AS kind, n.nspname::text AS schema, "
        "c.relname::text || '/' || t.spcname AS name FROM pg_class c "
        'JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_tablespace t ON t.oid=c.reltablespace '
        "WHERE t.spcname <> 'pg_default' AND " + namespace,
        "SELECT 'database-tablespace'::text AS kind, NULL::text AS schema, t.spcname::text AS name "
        'FROM pg_database d JOIN pg_tablespace t ON t.oid=d.dattablespace '
        "WHERE d.datname=current_database() AND t.spcname <> 'pg_default'",
        "SELECT 'security-label'::text AS kind, NULL::text AS schema, s.provider::text AS name FROM pg_seclabel s",
        "SELECT 'shared-security-label'::text AS kind, NULL::text AS schema, s.provider::text AS name FROM pg_shseclabel s",
        "SELECT 'database-role-settings'::text AS kind, NULL::text AS schema, "
        "COALESCE(d.datname::text,'all-databases') || '/' || COALESCE(r.rolname::text,'all-roles') AS name "
        'FROM pg_db_role_setting s LEFT JOIN pg_database d ON d.oid=s.setdatabase '
        'LEFT JOIN pg_roles r ON r.oid=s.setrole WHERE s.setdatabase=0 OR d.datname=current_database()',
    ])
    payloads = {'routines': routines, 'triggers': triggers, 'rowSecurity': rls, 'policies': policies,
                'types': types, 'otherObjects': _array_sql('SELECT * FROM (' + ' UNION ALL '.join(others) +
                    ') objects ORDER BY kind COLLATE "C", schema COLLATE "C" NULLS FIRST, name COLLATE "C"')}
    return 'json_build_object(' + ', '.join("'" + key + "', " + payloads[key] for key in UNSUPPORTED_CATEGORIES) + ')'


def _column_slots_sql(array, relation, index=False):
    return _array_sql('SELECT key.ordinality::integer AS ordinal, key.position AS position, '
        'a.attname AS column, (key.position=0) AS expression' +
        (', (key.ordinality > i.indnkeyatts) AS included' if index else '') +
        ' FROM unnest(' + array + ') WITH ORDINALITY AS key(position, ordinality) '
        'LEFT JOIN pg_attribute a ON a.attrelid=' + relation +
        ' AND a.attnum=key.position AND NOT a.attisdropped ORDER BY key.ordinality')


def _dependency_references_sql(catalog, object_id):
    # Keep unresolved/dropped catalog references visible as nulls so the pure
    # consumer refuses them. Names/positions, never catalog OIDs, are compared.
    return _array_sql('SELECT DISTINCT n.nspname AS schema, c.relname AS name, '
        'd.refobjsubid AS position, a.attname AS column FROM pg_depend d '
        'LEFT JOIN pg_class c ON c.oid=d.refobjid LEFT JOIN pg_namespace n ON n.oid=c.relnamespace '
        'LEFT JOIN pg_attribute a ON a.attrelid=d.refobjid AND a.attnum=d.refobjsubid AND NOT a.attisdropped '
        "WHERE d.classid='" + catalog + "'::regclass AND d.objid=" + object_id +
        " AND d.refclassid='pg_class'::regclass AND d.refobjsubid<>0 ORDER BY 1,2,3,4")


def _canary_sections(value):
    s = value['source']
    user_namespace = "n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'"
    table_queries, content_queries = [], []
    for table in s['tables']:
        labels = literal(table['schema']) + ' AS schema, ' + literal(table['name']) + ' AS name, '
        relation = quote(table['schema']) + '.' + quote(table['name'])
        table_queries.append('SELECT ' + labels + 'count(*) AS rows FROM ' + relation)
        # A materialized bounded prefix detects over-profile tables. Length-
        # prefixed UTF8 row JSON, ordered with C collation, preserves duplicates
        # without delimiter ambiguity. No extension or raw row output is used.
        content_queries.append('WITH rows AS MATERIALIZED (SELECT row_to_json(r)::text AS row_json FROM ' +
            relation + ' r LIMIT ' + str(MAX_CANARY_ROWS + 1) + '), stats AS (SELECT count(*) AS rows, '
            "COALESCE(max(octet_length(convert_to(row_json, 'UTF8'))), 0) AS maximum, "
            "COALESCE(sum(octet_length(convert_to(row_json, 'UTF8'))), 0)::bigint AS bytes FROM rows) "
            'SELECT ' + labels + 'stats.rows, stats.maximum AS "rowBytesMaximum", '
            'stats.bytes AS "contentBytes", (stats.rows <= ' + str(MAX_CANARY_ROWS) +
            ' AND stats.maximum <= ' + str(MAX_CANARY_ROW_BYTES) + ' AND stats.bytes <= ' +
            str(MAX_CANARY_CONTENT_BYTES) + ') AS "profileAccepted", CASE WHEN stats.rows <= ' +
            str(MAX_CANARY_ROWS) + ' AND stats.maximum <= ' + str(MAX_CANARY_ROW_BYTES) +
            ' AND stats.bytes <= ' + str(MAX_CANARY_CONTENT_BYTES) + ' THEN '
            "(SELECT encode(sha256(convert_to(COALESCE(string_agg("
            "octet_length(convert_to(row_json, 'UTF8'))::text || ':' || row_json, '' "
            'ORDER BY row_json COLLATE "C"), \'\'), \'UTF8\')), \'hex\') FROM rows) '
            'ELSE NULL END AS sha256 FROM stats')
    namespaces = _array_sql('SELECT n.nspname AS schema, pg_get_userbyid(n.nspowner) AS owner, '
                           'n.nspacl AS acl FROM pg_namespace n WHERE ' + user_namespace +
                           ' ORDER BY n.nspname COLLATE "C"')
    database = "(SELECT json_build_object('encoding', pg_encoding_to_char(d.encoding), "
    database += "'localeProvider', d.datlocprovider, 'collate', d.datcollate, 'ctype', d.datctype, "
    database += "'locale', d.datlocale, 'icuRules', d.daticurules, 'collationVersion', d.datcollversion, "
    database += "'actualCollationVersion', pg_database_collation_actual_version(d.oid), 'tablespace', t.spcname) "
    database += 'FROM pg_database d JOIN pg_tablespace t ON t.oid=d.dattablespace WHERE d.datname=current_database())'
    relations = _array_sql('SELECT n.nspname AS schema, c.relname AS name, c.relkind AS kind, '
        'pg_get_userbyid(c.relowner) AS owner, c.relacl AS acl, c.relpersistence AS persistence, '
        'am.amname AS "accessMethod", c.reloptions AS options, toast.reloptions AS "toastOptions", '
        'ts.spcname AS tablespace FROM pg_class c '
        'JOIN pg_namespace n ON n.oid=c.relnamespace LEFT JOIN pg_am am ON am.oid=c.relam '
        'LEFT JOIN pg_class toast ON toast.oid=c.reltoastrelid '
        'LEFT JOIN pg_tablespace ts ON ts.oid=c.reltablespace WHERE ' + user_namespace +
        ' ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C"')
    collation = ("CASE WHEN co.oid IS NULL THEN NULL ELSE json_build_object('schema', cn.nspname, "
        "'name', co.collname, 'provider', co.collprovider, 'encoding', "
        "CASE WHEN co.collencoding=-1 THEN NULL ELSE pg_encoding_to_char(co.collencoding) END, "
        "'collate', co.collcollate, 'ctype', co.collctype, 'locale', co.colllocale, "
        "'icuRules', co.collicurules, 'version', co.collversion, 'actualVersion', "
        "CASE WHEN co.collprovider='d' THEN (SELECT pg_database_collation_actual_version(db.oid) "
        "FROM pg_database db WHERE db.datname=current_database()) ELSE pg_collation_actual_version(co.oid) END, "
        "'deterministic', co.collisdeterministic) END")
    columns = _array_sql('SELECT n.nspname AS schema, c.relname AS name, a.attnum AS position, '
        'a.attname AS column, format_type(a.atttypid,a.atttypmod) AS type, a.attnotnull AS "notNull", '
        'a.attidentity AS identity, a.attgenerated AS generated, pg_get_expr(d.adbin,d.adrelid) AS "default", '
        'a.attacl AS acl, ' + collation + ' AS collation, a.attstorage AS storage, '
        'a.attcompression::text AS compression, a.attstattarget AS "statisticsTarget", '
        'a.attoptions AS options, a.attndims AS dimensions '
        'FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid '
        'JOIN pg_namespace n ON n.oid=c.relnamespace '
        'LEFT JOIN pg_collation co ON co.oid=a.attcollation LEFT JOIN pg_namespace cn ON cn.oid=co.collnamespace '
        'LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum '
        'WHERE a.attnum>0 AND NOT a.attisdropped AND ' + user_namespace +
        ' ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C", a.attnum')
    definitions = _array_sql('SELECT schemaname AS schema, sequencename AS name, sequenceowner AS owner, '
        'data_type AS type, start_value::text AS start, min_value::text AS minimum, '
        'max_value::text AS maximum, increment_by::text AS increment, cycle, cache_size::text AS cache '
        "FROM pg_sequences WHERE schemaname !~ '^pg_' AND schemaname <> 'information_schema' "
        'ORDER BY schemaname COLLATE "C", sequencename COLLATE "C"')
    ownership = _array_sql('SELECT qn.nspname AS schema, q.relname AS name, n.nspname AS "tableSchema", '
        'c.relname AS "tableName", a.attname AS column, d.deptype AS dependency FROM pg_depend d '
        "JOIN pg_class q ON q.oid=d.objid AND q.relkind='S' "
        'JOIN pg_namespace qn ON qn.oid=q.relnamespace JOIN pg_class c ON c.oid=d.refobjid '
        'JOIN pg_namespace n ON n.oid=c.relnamespace '
        'JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=d.refobjsubid '
        "WHERE qn.nspname !~ '^pg_' AND qn.nspname <> 'information_schema' "
        'ORDER BY qn.nspname COLLATE "C", q.relname COLLATE "C", n.nspname COLLATE "C", '
        'c.relname COLLATE "C", a.attname COLLATE "C"')
    index_columns = _column_slots_sql('i.indkey::smallint[]', 'i.indrelid', True)
    index_references = _dependency_references_sql('pg_class', 'i.indexrelid')
    indexes = _array_sql('SELECT n.nspname AS schema, c.relname AS name, ic.relname AS index, '
        'pg_get_indexdef(i.indexrelid) AS definition, i.indnatts AS "columnCount", '
        'i.indnkeyatts AS "keyColumnCount", ' + index_columns + ' AS columns, ' + index_references +
        ' AS "dependencyReferences", json_array_length(' + index_references + ') AS "dependencyReferenceCount", '
        'pg_get_expr(i.indexprs,i.indrelid,true) AS expressions, pg_get_expr(i.indpred,i.indrelid,true) AS predicate '
        'FROM pg_index i JOIN pg_class c ON c.oid=i.indrelid JOIN pg_namespace n ON n.oid=c.relnamespace '
        'JOIN pg_class ic ON ic.oid=i.indexrelid WHERE ' + user_namespace +
        ' ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C", ic.relname COLLATE "C"')
    constraint_columns = _column_slots_sql('k.conkey', 'k.conrelid')
    referenced_columns = _column_slots_sql('k.confkey', 'k.confrelid')
    constraint_references = _dependency_references_sql('pg_constraint', 'k.oid')
    constraints = _array_sql('SELECT n.nspname AS schema, c.relname AS name, k.conname AS constraint, '
        'k.convalidated AS validated, pg_get_constraintdef(k.oid,true) AS definition, k.contype AS kind, '
        'k.conenforced AS enforced, k.condeferrable AS deferrable, k.condeferred AS "initiallyDeferred", '
        'k.conperiod AS period, COALESCE(cardinality(k.conkey),0) AS "columnCount", ' + constraint_columns +
        ' AS columns, COALESCE(cardinality(k.confkey),0) AS "referenceColumnCount", ' + referenced_columns +
        ' AS "referencedColumns", CASE WHEN k.confrelid=0 THEN NULL ELSE '
        "json_build_object('schema', rn.nspname, 'name', rc.relname) END AS \"referencedTable\", "
        'CASE WHEN k.conindid=0 THEN NULL ELSE '
        "json_build_object('schema', ixn.nspname, 'name', ix.relname) END AS \"supportingIndex\", " +
        constraint_references + ' AS "dependencyReferences", json_array_length(' + constraint_references +
        ') AS "dependencyReferenceCount" FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid '
        'JOIN pg_namespace n ON n.oid=c.relnamespace LEFT JOIN pg_class rc ON rc.oid=k.confrelid '
        'LEFT JOIN pg_namespace rn ON rn.oid=rc.relnamespace LEFT JOIN pg_class ix ON ix.oid=k.conindid '
        'LEFT JOIN pg_namespace ixn ON ixn.oid=ix.relnamespace WHERE ' + user_namespace +
        ' ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C", k.conname COLLATE "C"')
    queries = {
        'sessionIdentity': "json_build_object('database', current_database(), 'user', current_user, "
                           "'serverVersionNum', current_setting('server_version_num')::integer)",
        'tableCensus': _array_sql('SELECT n.nspname AS schema, c.relname AS name FROM pg_class c '
            "JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind IN ('r','p','f','m') AND " +
            user_namespace + ' ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C"'),
        'schema': "json_build_object('database', " + database + ", 'namespaces', " + namespaces + ", 'relations', " + relations +
                  ", 'columns', " + columns + ')',
        'dataCounts': _array_sql(' UNION ALL '.join(table_queries)),
        'dataContents': _array_sql(' UNION ALL '.join('(' + q + ')' for q in content_queries)),
        'constraints': constraints,
        'indexes': indexes,
        'sequences': "json_build_object('definitions', " + definitions + ", 'ownership', " + ownership + ')',
        'sequenceCensus': _array_sql('SELECT n.nspname AS schema, c.relname AS name FROM pg_class c '
            "JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind='S' AND " + user_namespace +
            ' ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C"'),
        'extensions': _array_sql('SELECT e.extname AS name, e.extversion AS version, n.nspname AS schema '
            'FROM pg_extension e JOIN pg_namespace n ON n.oid=e.extnamespace ORDER BY e.extname COLLATE "C"'),
        'databaseActivation': "(SELECT json_build_object('database', datname, 'owner', "
            "pg_get_userbyid(datdba), 'acl', datacl) FROM pg_database WHERE datname=current_database())",
        'acls': _array_sql('SELECT pg_get_userbyid(d.defaclrole) AS owner, n.nspname AS schema, '
            'd.defaclobjtype AS type, d.defaclacl AS acl FROM pg_default_acl d '
            'LEFT JOIN pg_namespace n ON n.oid=d.defaclnamespace '
            'ORDER BY pg_get_userbyid(d.defaclrole) COLLATE "C", n.nspname COLLATE "C" NULLS FIRST, d.defaclobjtype'),
        'roleIsolation': "json_build_object('roles', " + _array_sql('SELECT rolname AS name, '
            'rolcanlogin AS "canLogin", rolsuper AS superuser, rolcreatedb AS "createDb", '
            'rolcreaterole AS "createRole", rolreplication AS replication, rolbypassrls AS "bypassRls", '
            'rolinherit AS inherit FROM pg_roles ORDER BY rolname COLLATE "C"') + ", 'memberships', " +
            _array_sql('SELECT pg_get_userbyid(m.roleid) AS role, pg_get_userbyid(m.member) AS member, '
                'pg_get_userbyid(m.grantor) AS grantor, m.admin_option AS admin, '
                'm.inherit_option AS inherit, m.set_option AS set FROM pg_auth_members m '
                'ORDER BY pg_get_userbyid(m.roleid) COLLATE "C", pg_get_userbyid(m.member) COLLATE "C", '
                'pg_get_userbyid(m.grantor) COLLATE "C"') + ')',
        'unsupportedObjects': _unsupported_objects_sql(),
    }
    sections = {'sequenceStateBefore': _sequence_state_sql('sequenceStateBefore')}
    sections.update({key: _record_sql(key, query) for key, query in queries.items()})
    sections['sequenceStateAfter'] = _sequence_state_sql('sequenceStateAfter')
    return sections


def _canary_sql(value):
    return '\n'.join(["SET LOCAL search_path = pg_catalog;",
                      "SET LOCAL statement_timeout = '15s'; SET LOCAL lock_timeout = '5s';",
                      "SET LOCAL timezone = 'UTC'; SET LOCAL datestyle = 'ISO, YMD';",
                      "SET LOCAL intervalstyle = 'iso_8601'; SET LOCAL bytea_output = 'hex';",
                      "SET LOCAL extra_float_digits = 3; SET LOCAL lc_numeric = 'C';",
                      "SET LOCAL row_security = off; SET LOCAL standard_conforming_strings = on;"] +
                     list(_canary_sections(value).values()))


def _json_tree(value, depth=0, budget=None):
    if budget is None:
        budget = [65536]
    budget[0] -= 1
    require(budget[0] >= 0 and depth <= 16)
    if type(value) is dict:
        for key, item in value.items():
            require(type(key) is str and len(key) <= 128)
            _json_tree(item, depth + 1, budget)
    elif type(value) is list:
        require(len(value) <= MAX_CATALOG_ROWS)
        for item in value:
            _json_tree(item, depth + 1, budget)
    elif type(value) is str:
        require('\x00' not in value and len(value.encode('utf8')) <= MAX_CANARY_ROW_BYTES)
    else:
        require(value is None or type(value) in (int, bool))


def _catalog_text(value, nullable=False):
    require(value is None and nullable or type(value) is str and '\x00' not in value and
            len(value.encode('utf8')) <= MAX_CANARY_ROW_BYTES)


def _catalog_rows(rows, columns, keys, nullable=(), boolean=(), integer=(), acl=(), structured=()):
    require(type(rows) is list and len(rows) <= MAX_CATALOG_ROWS)
    identities = set()
    for row in rows:
        fields(row, columns)
        for key, item in row.items():
            if key in structured:
                continue  # A bounded category-specific validator checks this.
            elif key in boolean:
                require(type(item) is bool)
            elif key in integer:
                c.integer(item, 0, 2**63 - 1)
            elif key in acl:
                require(item is None or type(item) is list and len(item) <= MAX_CATALOG_ROWS)
                if item is not None:
                    for entry in item:
                        _catalog_text(entry)
                    require(len(set(item)) == len(item))
                    row[key] = sorted(item)
            else:
                _catalog_text(item, key in nullable)
        identity = canonical([row[key] for key in keys])
        require(identity not in identities)
        identities.add(identity)
    return sorted(rows, key=canonical)


def _sequence_integer(value, lower, upper):
    require(type(value) is str and re.fullmatch(r'(?:0|-?[1-9][0-9]{0,18})', value) is not None)
    require(lower <= int(value) <= upper)
    return int(value)


def _validate_sequence_profile(sequences, samples):
    by_name = {}
    for row in sequences['definitions']:
        require(row['type'] in SEQUENCE_TYPES)
        lower, upper = SEQUENCE_TYPES[row['type']]
        minimum = _sequence_integer(row['minimum'], lower, upper)
        maximum = _sequence_integer(row['maximum'], lower, upper)
        require(minimum < maximum)
        _sequence_integer(row['start'], minimum, maximum)
        require(_sequence_integer(row['increment'], -(2**63), 2**63 - 1) != 0)
        _sequence_integer(row['cache'], 1, 2**63 - 1)
        by_name[(row['schema'], row['name'])] = (minimum, maximum)
    for rows in samples:
        for row in rows:
            minimum, maximum = by_name[(row['schema'], row['name'])]
            _sequence_integer(row['lastValue'], minimum, maximum)


def _validate_database_semantics(database):
    fields(database, 'encoding localeProvider collate ctype locale icuRules collationVersion '
                     'actualCollationVersion tablespace')
    for key, item in database.items():
        _catalog_text(item, key in ('locale', 'icuRules', 'collationVersion', 'actualCollationVersion'))
    require(database['encoding'] and database['localeProvider'] in ('b', 'c', 'i') and
            database['collate'] and database['ctype'] and database['tablespace'] == 'pg_default')
    require(database['locale'] is None if database['localeProvider'] == 'c' else bool(database['locale']))
    require(database['icuRules'] is None or database['localeProvider'] == 'i')
    require(database['collationVersion'] is None or
            database['collationVersion'] == database['actualCollationVersion'])


def _validate_column_collation(collation, database):
    if collation is None:
        return
    fields(collation, 'schema name provider encoding collate ctype locale icuRules version actualVersion deterministic')
    require(type(collation['deterministic']) is bool)
    for key, item in collation.items():
        if key != 'deterministic':
            _catalog_text(item, key not in ('schema', 'name', 'provider'))
    require(collation['schema'] == 'pg_catalog' and collation['name'] and collation['provider'] in ('b', 'c', 'i', 'd'))
    require(collation['encoding'] in (None, database['encoding']))
    if collation['provider'] == 'd':
        require(collation['schema'] == 'pg_catalog' and collation['name'] == 'default' and
                all(collation[key] is None for key in ('encoding', 'collate', 'ctype', 'locale', 'icuRules', 'version')) and
                collation['deterministic'] and collation['actualVersion'] == database['actualCollationVersion'])
    elif collation['provider'] == 'c':
        require(collation['collate'] and collation['ctype'] and collation['locale'] is None and
                collation['icuRules'] is None)
    else:
        require(collation['locale'] and collation['collate'] is None and collation['ctype'] is None and
                (collation['icuRules'] is None or collation['provider'] == 'i'))
    require(collation['version'] is None or collation['version'] == collation['actualVersion'])


def _validate_slots(rows, table, columns, count, index=False, allow_expression=False, key_count=0):
    c.integer(count, 0, 32 if index else 1600)
    rows = _catalog_rows(rows, 'ordinal position column expression' + (' included' if index else ''),
        ('ordinal',), nullable=('column',), boolean=('expression', 'included') if index else ('expression',),
        integer=('ordinal', 'position'))
    rows.sort(key=lambda row: row['ordinal'])
    require([row['ordinal'] for row in rows] == list(range(1, count + 1)))
    by_position = {row['position']: row['column'] for row in columns[table].values()}
    for row in rows:
        if row['expression']:
            require(allow_expression and row['position'] == 0 and row['column'] is None and
                    not row.get('included', False))
        else:
            require(row['position'] > 0 and by_position.get(row['position']) == row['column'] and row['column'])
        if index:
            require(row['included'] == (row['ordinal'] > key_count))
    return rows


def _validate_dependency_references(row, allowed_tables, columns):
    references = _catalog_rows(row['dependencyReferences'], 'schema name position column',
                               ('schema', 'name', 'position'), integer=('position',))
    c.integer(row['dependencyReferenceCount'], 0, MAX_CATALOG_ROWS)
    require(len(references) == row['dependencyReferenceCount'])
    for reference in references:
        table = reference['schema'], reference['name']
        require(table in allowed_tables and reference['position'] > 0 and reference['column'] in columns[table] and
                columns[table][reference['column']]['position'] == reference['position'])
    row['dependencyReferences'] = references


def _native_base_type(formatted_type):
    # Native format_type spellings differ in typmod detail for table/index
    # attributes. Keep the base spelling and array suffixes, not precision/size.
    return re.sub(r'\([^()]*\)', '', formatted_type)


def _validate_schema_profile(value, records):
    """Supported catalog coverage, not a claim of arbitrary database equality."""
    s, schema = value['source'], records['schema']
    _validate_database_semantics(schema['database'])
    tables = {(row['schema'], row['name']) for row in s['tables']}
    schemas, roles = set(s['schemas']), {row['name'] for row in s['roles']}
    relations = {(row['schema'], row['name']): row for row in schema['relations']}
    require({key for key, row in relations.items() if row['kind'] == 'r'} == tables)
    for row in schema['relations']:
        require(row['kind'] in ('r', 'i', 'S') and row['schema'] in schemas and row['name'] and
                row['owner'] in roles and row['persistence'] == 'p')
        require(row['accessMethod'] == {'r': 'heap', 'i': 'btree', 'S': None}[row['kind']] and
                row['options'] in (None, []) and row['toastOptions'] in (None, []) and
                row['tablespace'] in (None, 'pg_default'))
    require(all(row['owner'] in roles for row in schema['namespaces']))
    columns = {key: {} for key in relations}
    for row in schema['columns']:
        key = row['schema'], row['name']
        require(key in relations and row['column'] and 1 <= row['position'] <= 1600 and row['type'] and
                row['identity'] in ('', 'a', 'd') and row['generated'] in ('', 's', 'v'))
        require(row['column'] not in columns[key])
        require(not (row['identity'] and row['generated']))
        require(relations[key]['kind'] == 'r' or not (row['identity'] or row['generated']))
        require(row['storage'] in ('p', 'e', 'm', 'x') and row['compression'] in ('', 'p', 'l') and
                row['options'] in (None, []) and 0 <= row['dimensions'] <= 32767)
        if row['statisticsTarget'] is not None:
            c.integer(row['statisticsTarget'], 0, 10000)
        _validate_column_collation(row['collation'], schema['database'])
        scalar_type = re.sub(r'(?:\[\])+$', '', _native_base_type(row['type']))
        require((row['collation'] is not None) ==
                (scalar_type in ('text', 'name', 'character', 'character varying')))
        if row['identity']:
            require(row['notNull'] and row['default'] is None)
        if row['generated']:
            require(type(row['default']) is str and row['default'].strip())
        columns[key][row['column']] = row
    # Requiring metadata for every supported relation also refuses a sequence
    # or index census with its defining relation metadata silently omitted.
    require(all(columns[key] for key in relations))
    indexes = records['indexes']
    require({(row['schema'], row['index']) for row in indexes} ==
            {key for key, row in relations.items() if row['kind'] == 'i'})
    indexes_by_name = {(row['schema'], row['index']): row for row in indexes}
    for row in indexes:
        table, index = (row['schema'], row['name']), (row['schema'], row['index'])
        require(table in tables and row['definition'])
        c.integer(row['columnCount'], 1, 32)
        c.integer(row['keyColumnCount'], 1, row['columnCount'])
        row['columns'] = _validate_slots(row['columns'], table, columns, row['columnCount'], True, True, row['keyColumnCount'])
        require(sorted(column['position'] for column in columns[index].values()) == list(range(1, row['columnCount'] + 1)))
        index_columns = {column['position']: column for column in columns[index].values()}
        for slot in row['columns']:
            if not slot['expression']:
                require(_native_base_type(index_columns[slot['ordinal']]['type']) ==
                        _native_base_type(columns[table][slot['column']]['type']))
        _validate_dependency_references(row, {table}, columns)
        require(bool(row['expressions']) == any(slot['expression'] for slot in row['columns']))
        require(row['expressions'] is None or row['expressions'].strip())
        require(row['predicate'] is None or row['predicate'].strip())
    for row in records['constraints']:
        table = row['schema'], row['name']
        require(table in tables and row['constraint'] and row['definition'] and row['kind'] in ('c', 'n', 'p', 'u', 'f', 'x'))
        require(not row['initiallyDeferred'] or row['deferrable'])
        row['columns'] = _validate_slots(row['columns'], table, columns, row['columnCount'], allow_expression=row['kind'] == 'x')
        referenced = row['referencedTable']
        allowed_tables = {table}
        if row['kind'] == 'f':
            fields(referenced, 'schema name')
            referenced = referenced['schema'], referenced['name']
            require(referenced in tables and row['columnCount'] > 0 and row['referenceColumnCount'] == row['columnCount'])
            row['referencedColumns'] = _validate_slots(row['referencedColumns'], referenced, columns, row['referenceColumnCount'])
            allowed_tables.add(referenced)
        else:
            require(referenced is None and row['referenceColumnCount'] == 0 and row['referencedColumns'] == [])
        supporting = row['supportingIndex']
        if row['kind'] in ('p', 'u', 'f', 'x'):
            fields(supporting, 'schema name')
            supporting = supporting['schema'], supporting['name']
            require(supporting in indexes_by_name and row['columnCount'] > 0)
            index = indexes_by_name[supporting]
            require((index['schema'], index['name']) == (referenced if row['kind'] == 'f' else table))
            slots = row['referencedColumns'] if row['kind'] == 'f' else row['columns']
            require(len(slots) == index['keyColumnCount'] and
                    sorted(slot['position'] for slot in slots) ==
                    sorted(slot['position'] for slot in index['columns'] if not slot['included']))
        else:
            require(supporting is None)
        if row['kind'] == 'n':
            require(row['columnCount'] == 1 and columns[table][row['columns'][0]['column']]['notNull'])
        _validate_dependency_references(row, allowed_tables, columns)
        if row['kind'] == 'c':
            slot_positions = [slot['position'] for slot in row['columns']]
            require(len(slot_positions) == len(set(slot_positions)) and
                    set(slot_positions) == {reference['position'] for reference in row['dependencyReferences']})
    census = {(row['schema'], row['name']) for row in records['sequenceCensus']}
    require(census == {key for key, row in relations.items() if row['kind'] == 'S'})
    for row in records['sequences']['definitions']:
        require(row['owner'] == relations[(row['schema'], row['name'])]['owner'])
        fixed = sorted(columns[(row['schema'], row['name'])].values(), key=lambda column: column['position'])
        require([(column['position'], column['column'], column['type']) for column in fixed] ==
                [(1, 'last_value', 'bigint'), (2, 'log_cnt', 'bigint'), (3, 'is_called', 'boolean')])
        require(all(column['notNull'] and not column['identity'] and not column['generated'] and
                    column['default'] is None for column in fixed))
    owned = set()
    identity_edges = {}
    sequence_definitions = {(row['schema'], row['name']): row for row in records['sequences']['definitions']}
    for row in records['sequences']['ownership']:
        sequence, table = (row['schema'], row['name']), (row['tableSchema'], row['tableName'])
        require(sequence not in owned and table in tables and row['column'] in columns[table] and
                row['dependency'] in ('a', 'i'))
        owned.add(sequence)
        column = columns[table][row['column']]
        if row['dependency'] == 'i':
            require(column['identity'] and not column['generated'] and column['notNull'] and
                    column['type'] == sequence_definitions[sequence]['type'])
            identity_edges.setdefault((table, row['column']), []).append(sequence)
        else:
            require(not column['identity'] and not column['generated'])
    require(all(len(identity_edges.get((table, column['column']), [])) == 1
                for table in tables for column in columns[table].values() if column['identity']))
    for row in records['acls']:
        require(row['owner'] in roles and (row['schema'] is None or row['schema'] in schemas))


def _validate_role_observations(observations):
    fields(observations, 'roles memberships')
    observations['roles'] = _catalog_rows(observations['roles'], ['name'] + ROLE_FLAGS,
                                           ('name',), boolean=ROLE_FLAGS)
    roles = {row['name'] for row in observations['roles']}
    observations['memberships'] = _catalog_rows(observations['memberships'],
        'role member grantor admin inherit set', ('role', 'member', 'grantor'), boolean=('admin', 'inherit', 'set'))
    for row in observations['memberships']:
        require(all(row[key] in roles for key in ('role', 'member', 'grantor')) and
                row['role'] != row['member'])
    return observations


def _validate_canary_records(value, records):
    s, t = value['source'], value['target']
    unsupported = records['unsupportedObjects']
    fields(unsupported, UNSUPPORTED_CATEGORIES)
    require(all(type(rows) is list and not rows for rows in unsupported.values()))
    tables = sorted(s['tables'], key=canonical)
    records['tableCensus'] = _catalog_rows(records['tableCensus'], 'schema name', ('schema', 'name'))
    require(records['tableCensus'] == tables)
    for category in ('dataCounts', 'dataContents'):
        rows = records[category]
        expected = 'schema name rows' if category == 'dataCounts' else (
            'schema name rows rowBytesMaximum contentBytes profileAccepted sha256')
        require(type(rows) is list and len(rows) == 2)
        for row in rows:
            fields(row, expected)
            identifier(row['schema'])
            identifier(row['name'])
            c.integer(row['rows'], 0, MAX_CANARY_ROWS)
            if category == 'dataContents':
                require(row['profileAccepted'] is True)
                c.sha(row['sha256'])
                c.integer(row['rowBytesMaximum'], 0, MAX_CANARY_ROW_BYTES)
                c.integer(row['contentBytes'], 0, MAX_CANARY_CONTENT_BYTES)
                require((row['rows'] == 0) == (row['rowBytesMaximum'] == row['contentBytes'] == 0))
                require(row['contentBytes'] >= row['rowBytesMaximum'])
                if row['rows']:
                    require(row['rowBytesMaximum'] >= MIN_CANARY_ROW_BYTES and
                            row['rowBytesMaximum'] + (row['rows'] - 1) * MIN_CANARY_ROW_BYTES <= row['contentBytes'] <=
                            row['rows'] * row['rowBytesMaximum'])
                require(row['rows'] > 0 or row['sha256'] == digest(b''))
        require(sorted([{'schema': row['schema'], 'name': row['name']} for row in rows], key=canonical) == tables)
        records[category] = sorted(rows, key=canonical)
    counts = {(r['schema'], r['name']): r['rows'] for r in records['dataCounts']}
    require(all(counts[(r['schema'], r['name'])] == r['rows'] for r in records['dataContents']))
    schema = records['schema']
    fields(schema, 'database namespaces relations columns')
    schema['namespaces'] = _catalog_rows(schema['namespaces'], 'schema owner acl', ('schema',), acl=('acl',))
    require(sorted(row['schema'] for row in schema['namespaces']) == sorted(s['schemas']))
    schema['relations'] = _catalog_rows(schema['relations'],
        'schema name kind owner acl persistence accessMethod options toastOptions tablespace',
        ('schema', 'name'), nullable=('accessMethod', 'tablespace'), acl=('acl', 'options', 'toastOptions'))
    schema['columns'] = _catalog_rows(schema['columns'],
        'schema name position column type notNull identity generated default acl collation storage compression '
        'statisticsTarget options dimensions', ('schema', 'name', 'position'), nullable=('default',),
        boolean=('notNull',), integer=('position', 'dimensions'), acl=('acl', 'options'),
        structured=('collation', 'statisticsTarget'))
    for category, columns, keys, bools in (
        ('extensions', 'name schema version', ('name',), ()),
    ):
        records[category] = _catalog_rows(records[category], columns, keys, boolean=bools)
    records['indexes'] = _catalog_rows(records['indexes'],
        'schema name index definition columnCount keyColumnCount columns dependencyReferences dependencyReferenceCount expressions predicate',
        ('schema', 'index'), nullable=('expressions', 'predicate'),
        integer=('columnCount', 'keyColumnCount', 'dependencyReferenceCount'),
        structured=('columns', 'dependencyReferences'))
    records['constraints'] = _catalog_rows(records['constraints'],
        'schema name constraint validated definition kind enforced deferrable initiallyDeferred period '
        'columnCount columns referenceColumnCount referencedColumns referencedTable supportingIndex '
        'dependencyReferences dependencyReferenceCount', ('schema', 'name', 'constraint'),
        boolean=('validated', 'enforced', 'deferrable', 'initiallyDeferred', 'period'),
        integer=('columnCount', 'referenceColumnCount', 'dependencyReferenceCount'),
        structured=('columns', 'referencedColumns', 'referencedTable', 'supportingIndex', 'dependencyReferences'))
    require(records['extensions'] == sorted(s['extensions'], key=canonical))
    records['acls'] = _catalog_rows(records['acls'], 'owner schema type acl', ('owner', 'schema', 'type'),
                                     nullable=('schema',), acl=('acl',))
    sequences = records['sequences']
    fields(sequences, 'definitions ownership')
    sequences['definitions'] = _catalog_rows(sequences['definitions'],
        'schema name owner type start minimum maximum increment cycle cache', ('schema', 'name'), boolean=('cycle',))
    sequences['ownership'] = _catalog_rows(sequences['ownership'],
        'schema name tableSchema tableName column dependency', ('schema', 'name', 'tableSchema', 'tableName', 'column'))
    records['sequenceCensus'] = _catalog_rows(records['sequenceCensus'], 'schema name', ('schema', 'name'))
    census = records['sequenceCensus']
    require(sorted([{'schema': r['schema'], 'name': r['name']} for r in sequences['definitions']], key=canonical) == census)
    require(all({'schema': r['schema'], 'name': r['name']} in census for r in sequences['ownership']))
    for category in ('sequenceStateBefore', 'sequenceStateAfter'):
        rows = _catalog_rows(records[category], 'schema name lastValue isCalled',
                             ('schema', 'name'), boolean=('isCalled',))
        require(sorted([{'schema': r['schema'], 'name': r['name']} for r in rows], key=canonical) == census)
        records[category] = rows
    require(records['sequenceStateBefore'] == records['sequenceStateAfter'])
    _validate_sequence_profile(sequences, [records['sequenceStateBefore'], records['sequenceStateAfter']])
    _validate_schema_profile(value, records)
    records['roleIsolation'] = _validate_role_observations(records['roleIsolation'])
    fields(records['sessionIdentity'], 'database user serverVersionNum')
    require(records['sessionIdentity']['database'] == s['database'] and
            type(records['sessionIdentity']['serverVersionNum']) is int and
            records['sessionIdentity']['serverVersionNum'] == s['serverVersionNum'])
    identifier(records['sessionIdentity']['user'])
    activation = records['databaseActivation']
    fields(activation, 'database owner acl')
    require(activation['database'] == s['database'])
    _catalog_rows([{'schema': activation['database'], 'owner': activation['owner'], 'acl': activation['acl']}],
                  'schema owner acl', ('schema',), acl=('acl',))
    return records


@_safe
def parse_canaries(value, raw):
    """Bounded tagged NDJSON consumer. Only supplied data, no client or DB access."""
    validate(value)
    require(type(raw) is bytes and 0 < len(raw) <= c.MAX_JSON)
    lines = raw.split(b'\n')
    if lines[-1] == b'':
        lines.pop()
    require(len(lines) == len(CANARY_CATEGORIES))
    records = {}
    for line in lines:
        require(0 < len(line) <= 512 * 1024)
        record = strict_json(line)
        fields(record, 'category payload')
        category = record['category']
        require(type(category) is str and category in CANARY_CATEGORIES and category not in records)
        _json_tree(record['payload'])
        records[category] = record['payload']
    require(set(records) == set(CANARY_CATEGORIES))
    return _validate_canary_records(value, records)


@_safe
def compare_canaries(value, source_raw, target_raw):
    """Consistency comparison only; supplied transcripts are not authenticated."""
    source, target = parse_canaries(value, source_raw), parse_canaries(value, target_raw)
    require(all(source[key] == target[key] for key in EQUAL_CATEGORIES))
    s, t = value['source'], value['target']
    require(source['sessionIdentity']['user'] == s['user'] and
            target['sessionIdentity']['user'] == t['bootstrapUser'])
    require(sorted([{key: row[key] for key in ('name', 'canLogin', 'superuser')}
                    for row in source['roleIsolation']['roles']], key=canonical) == sorted(s['roles'], key=canonical))
    expected_roles = {r['name']: r for r in source['roleIsolation']['roles']}
    actual_roles = {r['name']: r for r in target['roleIsolation']['roles']}
    require(set(actual_roles) == set(expected_roles) | {t['bootstrapUser']})
    for role_name, actual in actual_roles.items():
        # Only the fresh bootstrap has activation authority. Builtins use the
        # supported stock-image profile; custom application roles have none.
        expected = {'name': role_name, **{flag: role_name == t['bootstrapUser'] for flag in ROLE_FLAGS}}
        if role_name.startswith('pg_'):
            expected['inherit'] = True
            require(expected_roles[role_name] == expected)
        require(actual == expected)
    for observations in (source['roleIsolation'], target['roleIsolation']):
        # We do not reconstruct arbitrary role graphs from a pg_dump archive.
        # Only stock builtin-to-builtin edges, granted by the bootstrap, are
        # supported. Any custom membership, even an apparently inert grant,
        # could change ownership/ACL meaning and requires a different proposal.
        require(all(row['role'].startswith('pg_') and row['member'].startswith('pg_') and
                    row['grantor'] == t['bootstrapUser'] for row in observations['memberships']))
    require(source['roleIsolation']['memberships'] == target['roleIsolation']['memberships'])
    require(source['databaseActivation']['owner'] in expected_roles and
            target['databaseActivation']['owner'] == t['bootstrapUser'] and
            target['databaseActivation']['acl'] is None)
    return {'equalCategories': EQUAL_CATEGORIES, 'expectedDifferenceCategories': DIFFERENT_CATEGORIES,
            'consistent': True, 'sourceTranscriptSha256': digest(source_raw),
            'targetTranscriptSha256': digest(target_raw), 'operationalQualification': None,
            'recoveryAcceptance': None, 'executionAuthorized': False}


def _manifests(value):
    s, t = value['source'], value['target']
    ns, sts, claim = t['namespace']['name'], t['statefulSetName'], t['pvcName']
    labels = {'recovery.hexalith.io/role': 'telemetry-postgresql-isolated'}
    return [
        {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': ns, 'labels': labels}},
        {'apiVersion': 'networking.k8s.io/v1', 'kind': 'NetworkPolicy',
         'metadata': {'name': t['networkPolicyName'], 'namespace': ns},
         'spec': {'podSelector': {}, 'policyTypes': ['Ingress', 'Egress'], 'ingress': [], 'egress': []}},
        {'apiVersion': 'v1', 'kind': 'PersistentVolumeClaim',
         'metadata': {'name': claim, 'namespace': ns},
         'spec': {'accessModes': ['ReadWriteOnce'], 'storageClassName': t['storageClass']['resource']['name'],
                  'resources': {'requests': {'storage': t['storage']}}}},
        # Intentionally not applyable: only fresh, future run-owned material may
        # replace this null. No source credential bytes or locations are inputs.
        {'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': t['secretName'], 'namespace': ns},
         'type': 'Opaque', 'data': {'bootstrap-password': None}},
        {'apiVersion': 'apps/v1', 'kind': 'StatefulSet', 'metadata': {'name': sts, 'namespace': ns},
         'spec': {'replicas': 1, 'serviceName': sts, 'selector': {'matchLabels': labels},
                  'template': {'metadata': {'labels': labels}, 'spec': {
                      'automountServiceAccountToken': False, 'enableServiceLinks': False,
                      'hostNetwork': False, 'hostPID': False, 'hostIPC': False,
                      'securityContext': {'runAsNonRoot': True, 'runAsUser': t['runAsUser'],
                                          'runAsGroup': t['runAsGroup'], 'fsGroup': t['fsGroup'],
                                          'seccompProfile': {'type': 'RuntimeDefault'}},
                      'containers': [{'name': t['container'], 'image': s['image'], 'imagePullPolicy': 'IfNotPresent',
                                      'securityContext': {'allowPrivilegeEscalation': False,
                                                          'capabilities': {'drop': ['ALL']}},
                                      'resources': _normalized_resources(t['resources']),
                                      'args': ['-c', 'listen_addresses=', '-c', 'port=' + str(t['port']),
                                               '-c', 'unix_socket_directories=' + t['socket']],
                                      'env': [{'name': 'POSTGRES_USER', 'value': t['bootstrapUser']},
                                              {'name': 'POSTGRES_DB', 'value': s['database']},
                                              {'name': 'POSTGRES_PASSWORD_FILE',
                                               'value': '/run/credentials/bootstrap-password'},
                                              {'name': 'POSTGRES_INITDB_ARGS',
                                               'value': '--auth-local=peer --auth-host=scram-sha-256'},
                                              {'name': 'PGDATA', 'value': '/var/lib/postgresql/18/recovery'}],
                                      'volumeMounts': [{'name': 'data', 'mountPath': '/var/lib/postgresql'},
                                                       {'name': 'socket', 'mountPath': t['socket']},
                                                       {'name': 'tmp', 'mountPath': '/tmp'},
                                                       {'name': 'credentials', 'mountPath': '/run/credentials',
                                                        'readOnly': True}]}],
                      'volumes': [{'name': 'data', 'persistentVolumeClaim': {'claimName': claim}},
                                  {'name': 'socket', 'emptyDir': {'sizeLimit': '16Mi'}},
                                  {'name': 'tmp', 'emptyDir': {'sizeLimit': '64Mi'}},
                                  {'name': 'credentials', 'secret': {'secretName': t['secretName'],
                                                                   'defaultMode': 0o440}}]}}}},
    ]


def _replica_patch(workload, old, new, rv):
    return [{'op': 'test', 'path': '/metadata/uid', 'value': workload['uid']},
            {'op': 'test', 'path': '/metadata/resourceVersion', 'value': rv},
            {'op': 'test', 'path': '/spec/replicas', 'value': old},
            {'op': 'replace', 'path': '/spec/replicas', 'value': new}]


GATES = ('freshAuthenticatedPolicy independentSignatureReadback distinctValidatorQualification '
         'providerProtectionAndGrantLineage exactExecutableAndPeerIdentityQualification '
         'sourceWorkerPauseAndCleanDrain parentMcpPauseAndPairedStoreRecovery '
         'exportedSnapshotHolderLifetime sameSnapshotCanaryDumpAgreement stableQuiescedSequences '
         'deterministicFullTableContentHashQualification taggedCanaryConsumerQualification '
         'supportedDatabaseProfileAndUnsupportedObjectCensus targetFullRoleAuthorityAndMembershipQualification '
         'databaseAndColumnLocaleCompatibility loggedNativeStorageProfile nativeIndexConstraintReferenceAgreement '
         'identityGeneratedAndFixedSequenceSchemaQualification '
         'independentComplianceRetentionAndEncryptionReadback authenticatedCaptureTimingAndProofFreshness '
         'immutableCaptureVersionAndIndependentReadback verifiedDecryptedExactStdin '
         'targetAbsentBeforeCreation createdTargetIdentities targetEmptySchema '
         'targetImageStartupNonrootPeerStorageAndCapacity targetIsolation '
         'postgresqlRestoreAndCanaryAgreement sourceResumeReconciliation '
         'qualifiedNamespaceCreationFence ownedInventoryAndStoragePathAbsence '
         'fullOrchestrationAndProofConsumerUpdates').split()


@_safe
def render_proposal(value):
    validate(value)
    # Reparse into independent JSON values; output mutation cannot change input.
    value = strict_json(canonical(value))
    s, t, worker = value['source'], value['target'], value['worker']
    target_ns = t['namespace']['name']
    canaries = _canary_sql(value)
    restore = _argv(value, 'pg_restore', True) + ['--format=custom', '--exit-on-error', '--single-transaction']
    roles = ['CREATE ROLE ' + quote(r['name']) + ' WITH NOLOGIN NOSUPERUSER NOCREATEDB '
             'NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;' for r in s['roles']
             if not r['name'].startswith('pg_') and r['name'] != t['bootstrapUser']]
    target_names = [('Namespace', target_ns, ''), ('StatefulSet', t['statefulSetName'], target_ns),
                    ('PersistentVolumeClaim', t['pvcName'], target_ns),
                    ('PersistentVolume', None, ''), ('Secret', t['secretName'], target_ns),
                    ('NetworkPolicy', t['networkPolicyName'], target_ns)]
    require(all((RESOURCE_TYPES[k][0], k) in c.DELETE_ROOT_KINDS for k, _, _ in target_names))
    owned_targets = [{'apiVersion': RESOURCE_TYPES[k][0], 'kind': k, 'namespace': ns, 'name': n,
                      'uid': None, 'resourceVersion': None} for k, n, ns in target_names]
    pod_census_member = {'apiVersion': 'v1', 'kind': 'Pod', 'namespace': target_ns,
                        'name': t['statefulSetName'] + '-0', 'uid': None, 'resourceVersion': None}
    return {
        'kind': 'hexalith.telemetry-postgresql-offline-proposal', 'schemaVersion': 1,
        'proposalInputSha256': digest(canonical(value)), 'sourcePinSha256': value['sourcePinSha256'],
        'parentProposalSha256': value['parentProposalSha256'],
        'retainedDecisionsSha256': value['retainedDecisionsSha256'],
        'commonAdapterSha256': COMMON_SOURCE_SHA256, 'evidence': value['evidence'],
        'source': s, 'targetProposal': t, 'transferProposal': value['transfer'],
        'canaryContract': {
            'sqlByCategory': _canary_sections(value),
            'requiredEqualCategories': EQUAL_CATEGORIES,
            'resultFormat': 'one-tagged-JSON-object-per-line; exact-category-and-structured-payload; null-is-not-empty',
            'contentHashProfile': {'algorithm': 'PG18-built-in-sha256-over-length-prefixed-UTF8-row_to_json-row-text',
                'ordering': 'row-json-C-collation-with-duplicates-preserved', 'maximumRowsPerTable': MAX_CANARY_ROWS,
                'minimumRowBytes': MIN_CANARY_ROW_BYTES,
                'maximumRowBytes': MAX_CANARY_ROW_BYTES, 'maximumContentBytesPerTable': MAX_CANARY_CONTENT_BYTES,
                'statementTimeoutSeconds': 15, 'maximumTranscriptBytes': c.MAX_JSON,
                'maximumCatalogRowsPerArray': MAX_CATALOG_ROWS, 'qualification': None},
            'supportedSchemaProfile': {'relations': ['ordinary-table', 'index', 'sequence'],
                'persistence': 'logged-only', 'accessMethods': {'ordinary-table': 'heap', 'index': 'btree', 'sequence': None},
                'databaseSemantics': None, 'columnAclCollationAndStorageSemantics': None,
                'nativeIndexConstraintReferences': None, 'identityGeneratedAndFixedSequenceSchema': None,
                'boundTableRelationsAndColumnsRequired': True, 'unsupportedObjectCensus': None,
                'unsupportedObjectCategories': UNSUPPORTED_CATEGORIES,
                'extensionCoverage': 'only-pinned-plpgsql-and-pgstattuple-identity-schema-version; no-routine-body-equality',
                'qualification': None},
            'roleAuthorityProfile': {'flags': ROLE_FLAGS, 'sourceAuthorityObservation': None,
                'sourceMemberships': None, 'targetAuthorityObservation': None, 'targetMemberships': None,
                'customTargetRoles': 'all-authority-flags-false-including-inherit; no-memberships',
                'supportedMemberships': 'builtin-to-builtin-only; bootstrap-grantor; exact-source-target-agreement',
                'builtinException': 'stock-builtin-flags-only-inherit-true; membership-graph-must-agree',
                'bootstrapException': 'fresh-isolated-postgres-bootstrap-all-recorded-flags-true', 'qualification': None},
            'sequenceObservation': {'census': None, 'before': None, 'after': None,
                'zeroSequenceCensusMustBeExplicit': True, 'quiescenceQualification': None},
            'sequenceSampleTiming': 'source-before-all-canaries-and-dump; source-after-dump-and-canaries-complete; '
                                    'retain-quiescence-through-both-samples; target-after-restore-before-and-after-canaries',
            'sequenceEqualityRequires': 'all-source-writers-quiesced-and-sequence-values-stable-outside-MVCC',
            'expectedDifferences': {
                'sessionIdentity': 'source-peer-user-versus-target-postgres-bootstrap; database-and-server-version-must-match',
                'roleIsolation': 'source-application-login-and-superuser-flags-versus-target-NOLOGIN-NOSUPERUSER; '
                                 'all-target-custom-role-authority-and-inherit-flags-false; no-custom-memberships; '
                                 'PG18-builtins-and-bootstrap-are-checked-as-target-local',
                'databaseActivation': 'source-database-owner-and-connect-ACL-are-recorded; '
                                      'new-target-database-remains-owned-by-isolated-bootstrap; '
                                      'no-create-restore-does-not-copy-source-database-activation-ACL',
            },
            'sourceTableCensusMustEqual': s['tables'],
            'censusTiming': 'before-export-and-again-in-the-exported-snapshot-before-dump',
            'unexpectedOrMissingTableAction': 'stop-and-rebind; no-row-coverage-claim-for-unbound-tables',
            'wholeTranscriptEquality': False, 'agreement': None,
        },
        'capture': {'state': 'blocked', 'argv': None, 'snapshotId': None,
                    'dumpArgvTemplate': _argv(value, 'pg_dump') + ['--format=custom',
                        '--lock-wait-timeout=15s', '--file=-', '--snapshot', None],
                    'coverage': 'whole-database-including-both-observed-schemas',
                    'snapshotHolder': {'argv': _argv(value, 'psql'),
                        'initialStdin': 'BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;\n'
                                        "SET LOCAL idle_in_transaction_session_timeout = '5min';\n"
                                        "SELECT json_build_object('category', 'snapshotBinding', 'payload', "
                                        "json_build_object('snapshotId', pg_export_snapshot(), "
                                        "'holderBackendPid', pg_backend_pid()))::text;\n",
                        'stdinProtocol': 'keep-the-same-stdin-and-backend-open-without-EOF-until-dump-and-canaries-complete',
                        'releaseStdin': 'ROLLBACK;\n', 'backendPid': None, 'transactionOpen': None},
                    'canaryArgv': _argv(value, 'psql'), 'canarySqlTemplate': None,
                    'canarySqlBody': canaries, 'snapshotImportedBeforeAnyCanaryQuery': None,
                    'canaryAgreement': None,
                    'sequenceLimit': 'sequence-state-is-not-MVCC-snapshot-isolated; all-writers-must-be-quiesced-and-values-stable'},
        'restore': {'state': 'blocked', 'argv': None, 'argvTemplate': restore,
                    'stdin': None, 'artifact': None, 'authenticatedCaptureRecord': None,
                    'independentlyReadBack': None, 'targetNamespaceUid': None,
                    'targetPodUid': None, 'targetPvcUid': None, 'emptySchema': None,
                    'bootstrapCredentials': None, 'freshRunOwnedBootstrapOnly': True,
                    'targetRoleArgv': _argv(value, 'psql', True),
                    'targetRoleSql': '\n'.join(roles), 'sourceRoles': s['roles'],
                    'roleDifference': 'source-application-roles-are-NOLOGIN-and-have-no-activation-authority; '
                                      'PG18-builtins-and-fresh-isolated-bootstrap-remain-target-local',
                    'canaryArgv': _argv(value, 'psql', True),
                    'canarySql': 'BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;\n' + canaries + '\nROLLBACK;\n',
                    'manifestTemplates': _manifests(value), 'manifestTemplatesApplyable': False},
        'pause': {'state': 'blocked', 'endpoint': s['cluster']['endpoint'],
                  'path': api_path(worker['resource']), 'method': 'PATCH',
                  'contentType': 'application/json-patch+json',
                  'body': _replica_patch(worker['resource'], 1, 0, worker['resource']['resourceVersion']),
                  'readback': None, 'cleanDrain': None},
        'resume': {'state': 'blocked', 'endpoint': s['cluster']['endpoint'],
                   'path': api_path(worker['resource']), 'method': 'PATCH',
                   'contentType': 'application/json-patch+json',
                   'body': _replica_patch(worker['resource'], 0, 1, None),
                   'preserveClock': value['clock'],
                   'requiredBeforePatch': ['authenticated-parent-scope', 'parent-MCP-and-paired-stores-restored-and-verified',
                       'PG18-restored-and-canaries-agree', 'source-stores-healthy-with-observed-runtime-identities',
                       'fresh-clean-drain-zero-pending-inflight-missing-duplicates-and-stable-stream',
                       'exact-worker-UID-fresh-resourceVersion-and-replicas-zero'],
                   'reconciliation': None},
        'cleanup': {'state': 'blocked', 'endpoint': t['cluster']['endpoint'],
                    'ownedTargets': owned_targets, 'cleanupCensus': None,
                    'cleanupCensusTemplate': owned_targets + [pod_census_member],
                    'creationFence': None, 'ownedVolumePaths': None, 'targetPaths': None, 'inventory': None,
                    'deleteOptionsTemplate': {'apiVersion': 'v1', 'kind': 'DeleteOptions',
                        'propagationPolicy': 'Foreground', 'preconditions': {'uid': None, 'resourceVersion': None}},
                    'order': 'fresh-enforced-creation-fence-and-complete-owned-inventory; bind-exact-complete-census; '
                             'refresh-qualified-fence-after-binding-before-any-delete; per-dispatch-fence-freshness; '
                             'UID/RV-preconditioned-deletes; namespace-last; independent-NotFound-and-same-node-path-absence',
                    'sourceWrites': [], 'complete': None},
        'unresolvedGates': {key: None for key in GATES}, 'snapshotBinding': None,
        'targetRuntimeBinding': None, 'artifact': None, 'creationFence': None,
        'runnable': False, 'backendRequests': 0, 'operationalGate': 'closed',
        'operationalQualification': None, 'policyAuthentication': None,
        'recoveryAcceptance': None, 'sourceResumed': None, 'targetCleanupComplete': None,
        'uploadOrAbortImplementation': None,
    }


@_safe
def snapshot_commands(value, binding):
    """Pure constructor. A caller must separately qualify/authorize all execution."""
    validate(value)
    fields(binding, 'proposalInputSha256 sourcePinSha256 snapshotId holderBackendPid '
                    'holderTransactionOpen systemIdentifier timeline serverVersionNum '
                    'preSnapshotTableCensus snapshotTableCensus')
    require(binding['proposalInputSha256'] == digest(canonical(value)) and
            binding['sourcePinSha256'] == value['sourcePinSha256'])
    snapshot = binding['snapshotId']
    require(type(snapshot) is str and re.fullmatch(r'[0-9A-F]{8}-[0-9A-F]{8}-[1-9][0-9]{0,9}', snapshot))
    require(int(snapshot.rsplit('-', 1)[1]) <= 2**32 - 1)
    c.integer(binding['holderBackendPid'], 1, 2147483647)
    require(binding['holderTransactionOpen'] is True)
    require(all(binding[k] == value['source'][k] and type(binding[k]) is type(value['source'][k])
                for k in ('systemIdentifier', 'timeline', 'serverVersionNum')))
    for key in ('preSnapshotTableCensus', 'snapshotTableCensus'):
        census = binding[key]
        require(type(census) is list and len(census) == 2)
        for table in census:
            fields(table, 'schema name')
            identifier(table['schema'])
            identifier(table['name'])
        require(sorted((x['schema'], x['name']) for x in census) ==
                sorted((x['schema'], x['name']) for x in value['source']['tables']))
    return {'dumpArgv': _argv(value, 'pg_dump') + ['--format=custom', '--lock-wait-timeout=15s',
                                                '--file=-', '--snapshot=' + snapshot],
            'canaryArgv': _argv(value, 'psql'),
            'canaryStdin': 'BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;\n'
                           "SET TRANSACTION SNAPSHOT '" + snapshot + "';\n" +
                           _canary_sql(value) + '\nROLLBACK;\n',
            'canaryStdinProtocol': 'stream-same-backend-input; wait-after-sequenceStateBefore-for-dump-start; '
                                  'withhold-sequenceStateAfter-and-ROLLBACK-until-dump-completes; '
                                  'all-writers-stay-quiesced-through-final-source-sample',
            'operationalQualification': None, 'executionAuthorized': False}


@_safe
def restore_command(value, binding):
    """Pure separate target/stdin consistency checks, without reading an archive."""
    validate(value)
    fields(binding, 'proposalInputSha256 namespace statefulSet pod pvc pv volumePath image imageId '
                    'container containerId pvcVolumeName pvClaimUid pvClaimName pvClaimNamespace '
                    'storageClassUid reclaimPolicy emptySchema artifact independentlyReadBack '
                    'authenticatedCaptureRecord decryptedStdinSha256 decryptedStdinSize isolationAssessmentSha256')
    require(binding['proposalInputSha256'] == digest(canonical(value)))
    s, t, transfer = value['source'], value['target'], value['transfer']
    ns = t['namespace']['name']
    source_uids = {s[k]['uid'] for k in ('namespace', 'statefulSet', 'pod', 'pvc', 'pv', 'storageClass')}
    source_uids |= {value[k]['resource']['uid'] for k in ('worker', 'clock')}
    identities = []
    for key, kind, expected_name in (
            ('namespace', 'Namespace', ns), ('statefulSet', 'StatefulSet', t['statefulSetName']),
            ('pod', 'Pod', t['statefulSetName'] + '-0'), ('pvc', 'PersistentVolumeClaim', t['pvcName']),
            ('pv', 'PersistentVolume', None)):
        r = resource(binding[key], kind, ns)
        require(r['uid'] not in source_uids and (expected_name is None or r['name'] == expected_name))
        identities.append(r['uid'])
    require(len(set(identities)) == len(identities))
    require(binding['image'] == s['image'] and binding['imageId'] == s['imageId'] and
            binding['container'] == t['container'] and binding['containerId'] != s['containerId'])
    c.text(binding['containerId'], 128, r'(?:containerd|docker)://[0-9a-f]{64}')
    require(binding['pvcVolumeName'] == binding['pv']['name'] and
            binding['pvClaimUid'] == binding['pvc']['uid'] and binding['pvClaimName'] == t['pvcName'] and
            binding['pvClaimNamespace'] == ns and binding['storageClassUid'] == t['storageClass']['resource']['uid'] and
            binding['reclaimPolicy'] == 'Delete' and binding['pv']['name'] != s['pv']['name'])
    require(s['volumePath'] is not None)
    target_path = volume(binding['volumePath'], binding['pv']['uid'])
    source_path = Path(s['volumePath']['path'])
    proposed_path = Path(target_path['path'])
    require(not (source_path == proposed_path or source_path in proposed_path.parents or
                 proposed_path in source_path.parents))
    fields(binding['emptySchema'], 'tables sequences functions types nondefaultSchemas')
    require(all(type(x) is list and not x for x in binding['emptySchema'].values()))
    artifact = c.Artifact.parse(binding['artifact'])
    require(artifact.store == transfer['store'] and artifact.key == transfer['key'])
    fields(binding['independentlyReadBack'], 'store key version sha256 size validatorPrincipal '
                                            'retentionMode retainUntil encryption')
    readback = binding['independentlyReadBack']
    c.integer(readback['size'], 1, c.MAX_ARTIFACT)
    require(transfer['validatorPrincipal'] is not None and
            readback == {'store': artifact.store, 'key': artifact.key, 'version': artifact.version,
                         'sha256': artifact.sha256, 'size': artifact.size,
                         'validatorPrincipal': transfer['validatorPrincipal'],
                         'retentionMode': 'COMPLIANCE', 'retainUntil': artifact.retain_until,
                         'encryption': 'AES256'})
    c.sha(binding['decryptedStdinSha256'])
    c.integer(binding['decryptedStdinSize'], 1, c.MAX_ARTIFACT)
    capture = binding['authenticatedCaptureRecord']
    fields(capture, 'recordSha256 artifactVersion artifactSha256 plaintextSha256 plaintextSize capturedAt expiresAt')
    c.sha(capture['recordSha256'])
    c.sha(capture['plaintextSha256'])
    c.integer(capture['plaintextSize'], 1, c.MAX_ARTIFACT)
    captured_at, expires_at = c.timestamp(capture['capturedAt']), c.timestamp(capture['expiresAt'])
    require(captured_at >= c.timestamp(value['observedAt']) and
            expires_at == captured_at + timedelta(seconds=transfer['maximumProofAgeSeconds']) and
            c.timestamp(artifact.retain_until) >= captured_at + timedelta(days=transfer['retentionDays']))
    require(capture['artifactVersion'] == artifact.version and capture['artifactSha256'] == artifact.sha256 and
            capture['plaintextSha256'] == binding['decryptedStdinSha256'] and
            capture['plaintextSize'] == binding['decryptedStdinSize'])
    c.sha(binding['isolationAssessmentSha256'])
    return {'argv': _argv(value, 'pg_restore', True) + ['--format=custom', '--exit-on-error', '--single-transaction'],
            'stdinContract': {'immutableArtifact': binding['artifact'],
                              'sha256': binding['decryptedStdinSha256'], 'size': binding['decryptedStdinSize'],
                              'transport': 'verified-decrypted-exact-bytes-on-stdin-no-file-or-ambient-source'},
            'operationalQualification': None, 'executionAuthorized': False}


class Parser(c.Parser):
    pass


def main(argv=None):
    try:
        argv = list(sys.argv[1:] if argv is None else argv)
        require(not any(x in ('live', 'activate', 'execute', '--live', '--activate', '--execute',
                             'capture', 'restore', 'resume', 'cleanup') for x in argv))
        parser = Parser(description='Offline PostgreSQL proposal only; operational gate closed.', add_help=False)
        parser.add_argument('mode', choices=('proposal',))
        parser.add_argument('--input', required=True)
        parser.add_argument('--sha256', required=True)
        parser.add_argument('--private-output')
        args = parser.parse_args(argv)
        value = strict_json(c.read_exact_local(args.input, args.sha256))
        plan = render_proposal(value)
        raw = canonical(plan)
        require(len(raw) <= c.MAX_JSON)
        if args.private_output is not None:
            output = Path(c.absolute_path(args.private_output))
            c.LocalReceipts(output.parent).create(output.name, raw)
        print(canonical({'result': 'consistent-offline-telemetry-postgresql-proposal',
                         'proposalInputSha256': plan['proposalInputSha256'], 'privatePlanSha256': digest(raw),
                         'commonAdapterSha256': COMMON_SOURCE_SHA256,
                         'backendRequests': 0, 'runnable': False, 'operationalGate': 'closed',
                         'operationalQualification': None, 'recoveryAcceptance': None}).decode('ascii'))
        return 0
    except Exception:
        print(canonical({'result': FAILURE, 'backendRequests': 0, 'operationalGate': 'closed',
                         'operationalQualification': None, 'recoveryAcceptance': None}).decode('ascii'))
        return 1


if __name__ == '__main__':
    sys.exit(main())
