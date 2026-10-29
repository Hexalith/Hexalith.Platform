#!/usr/bin/env python3
"""Assess metadata offline. Proposed readiness contract; never deployed readiness.

No live adapter, dispatcher, manifest renderer, credential input or output files.
Exit 2 refuses incomplete/invalid metadata. Complete synthetic metadata stays
unapproved and can establish only consistency of this proposed contract.
"""

import argparse
from datetime import date
from fnmatch import fnmatchcase
import hashlib
import json
from pathlib import Path
import re
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
PLACEHOLDERS = {'', 'todo', 'tbd', 'unknown', 'changeme', 'replace', 'replace-me',
                'null', 'none', 'n/a', 'example', 'placeholder'}
ALLOWED_IMPORTS = {'argparse', 'datetime', 'fnmatch', 'hashlib', 'json', 'pathlib', 're', 'sys'}


class Refused(ValueError):
    def __init__(self, problems):
        self.problems = problems
        super().__init__('offline metadata refused')


def _object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise Refused(['JSON: duplicate object member (value withheld)'])
        result[key] = value
    return result


def read_json(path):
    try:
        return json.loads(Path(path).read_text(), object_pairs_hook=_object,
                          parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
    except (OSError, ValueError, UnicodeError) as error:
        if isinstance(error, Refused):
            raise
        raise Refused(['JSON: unreadable file or invalid JSON (contents withheld)']) from None


def _kind(value):
    if value is None:
        return 'null'
    if isinstance(value, bool):
        return 'boolean'
    if isinstance(value, int):
        return 'integer'
    if isinstance(value, str):
        return 'string'
    if isinstance(value, list):
        return 'array'
    if isinstance(value, dict):
        return 'object'
    return 'unsupported'


def _escape(key):
    return key.replace('~', '~0').replace('/', '~1')


def schema_check(value, schema, path='', missing=None, errors=None):
    """Implement the small documented JSON-schema subset used by this package."""
    missing = [] if missing is None else missing
    errors = [] if errors is None else errors
    kinds = schema['type']
    kinds = kinds if isinstance(kinds, list) else [kinds]
    if _kind(value) not in kinds:
        errors.append(f'{path or "/"}: wrong type; expected {" or ".join(kinds)}')
        return missing, errors
    if value is None:
        if 'x-question' in schema:
            missing.append({'path': path, 'owner': schema['x-owner'], 'question': schema['x-question']})
        return missing, errors
    if 'const' in schema and value != schema['const']:
        errors.append(f'{path}: does not match fixed contract metadata')
    if 'enum' in schema and value not in schema['enum']:
        errors.append(f'{path}: outside the allowed metadata choices')
    if isinstance(value, dict):
        props = schema.get('properties', {})
        for key in schema.get('required', []):
            if key not in value:
                errors.append(f'{path}/{key}: required member absent')
        for key, item in value.items():
            child_path = path + '/' + _escape(key)
            if key in props:
                schema_check(item, props[key], child_path, missing, errors)
            elif isinstance(schema.get('additionalProperties'), dict):
                # Provenance entry names are untrusted dynamic input, too.
                schema_check(item, schema['additionalProperties'], path + '/[entry]', missing, errors)
            else:
                # Do not echo unknown keys: they may themselves contain secret bytes.
                errors.append(f'{path or "/"}: unknown member forbidden (name withheld)')
    elif isinstance(value, list):
        if len(value) < schema.get('minItems', 0):
            errors.append(f'{path}: must contain at least {schema["minItems"]} entries')
        if schema.get('uniqueItems') and len({json.dumps(v, sort_keys=True) for v in value}) != len(value):
            errors.append(f'{path}: duplicate entries')
        for i, item in enumerate(value):
            schema_check(item, schema['items'], f'{path}/{i}', missing, errors)
    elif isinstance(value, str):
        if (value != value.strip() or value.casefold() in PLACEHOLDERS or
                re.search(r'<[^>]*>|\$\{|\{\{|(?i:\b(?:todo|tbd|changeme|replace-me)\b)', value)):
            errors.append(f'{path}: placeholder or untrimmed metadata forbidden')
        if len(value) < schema.get('minLength', 0):
            errors.append(f'{path}: empty metadata forbidden')
        if len(value) > schema.get('maxLength', len(value)):
            errors.append(f'{path}: metadata exceeds the allowed length')
        if 'pattern' in schema and not re.search(schema['pattern'], value):
            errors.append(f'{path}: metadata format mismatch')
        if schema.get('format') == 'date':
            try:
                date.fromisoformat(value)
            except ValueError:
                errors.append(f'{path}: valid calendar date required')
        if ('-----BEGIN' in value or re.search(r'\b(?:hvs|hvb|hvr|s)\.[A-Za-z0-9_-]{8,}', value)
                or re.search(r'\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.', value)
                or '\n' in value or '\r' in value):
            errors.append(f'{path}: credential-like or multiline material forbidden')
    elif _kind(value) == 'integer':
        if value < schema.get('minimum', value) or value > schema.get('maximum', value):
            errors.append(f'{path}: outside the allowed integer range')
    return missing, errors


def pointer(document, path):
    if path == '':
        return document
    if not isinstance(path, str) or not path.startswith('/'):
        raise KeyError(path)
    node = document
    for token in path[1:].split('/'):
        key = token.replace('~1', '/').replace('~0', '~')
        node = node[int(key)] if isinstance(node, list) else node[key]
    return node


def leaves(value, path=''):
    if isinstance(value, dict):
        for key, item in value.items():
            if key not in {'_provenance', 'schemaVersion', 'evidenceMode'}:
                yield from leaves(item, path + '/' + _escape(key))
    elif isinstance(value, list):
        for i, item in enumerate(value):
            yield from leaves(item, f'{path}/{i}')
    elif value is not None:
        yield path, value


def provenance_errors(data):
    errors = []
    evidence = data['_provenance']
    values = dict(leaves(data))
    if data['evidenceMode'] == 'synthetic-test-only':
        if evidence:
            errors.append('/_provenance: synthetic fixtures cannot claim recorded evidence')
        return errors
    for path in sorted(values.keys() - evidence.keys()):
        errors.append(f'{path}: resolved value lacks source identity')
    for path, source in evidence.items():
        if path not in values:
            errors.append('/_provenance: source must identify a resolved leaf')
            continue
        try:
            relative = Path(source['path'])
            file = (ROOT / relative).resolve()
            if relative.is_absolute() or '..' in relative.parts or not file.is_relative_to(ROOT):
                raise ValueError()
            raw = file.read_bytes()
            if hashlib.sha256(raw).hexdigest() != source['sha256']:
                raise ValueError()
            document = read_json(file) if source['pointer'] is not None or file.suffix.casefold() == '.json' else None
            if isinstance(document, dict) and document.get('evidenceMode') == 'synthetic-test-only':
                errors.append(f'{path}: explicitly synthetic-test-only source cannot establish recorded evidence')
                continue
            if source['pointer'] is not None:
                if source['lineStart'] is not None or source['lineEnd'] is not None:
                    raise ValueError()
                observed = pointer(document, source['pointer'])
                # JSON pointers address values. A selector-key claim addresses its
                # containing mapping and is limited to that mapping's keys.
                if observed != values[path] and not (path.endswith('/key') and
                        isinstance(observed, dict) and values[path] in observed):
                    raise ValueError()
            else:
                start, end = source['lineStart'], source['lineEnd']
                lines = raw.decode().splitlines()
                if (type(start) is not int or type(end) is not int or not 1 <= start <= end <= len(lines)
                        or str(values[path]).casefold() not in '\n'.join(lines[start-1:end]).casefold()):
                    raise ValueError()
        except (OSError, ValueError, KeyError, TypeError, IndexError, UnicodeError):
            errors.append(f'{path}: source path, hash, pointer/lines or recorded value mismatch')
    return errors


def resource_quantity(value, resource):
    """Conservative exact subset: nonnegative CPU cores (<=3 decimals) or
    integer millicores; memory integer bytes or k/M/G/T/P/E, Ki/Mi/Gi/Ti/Pi/Ei.
    Exponents, signs and fractional memory are refused, never rounded/defaulted.
    """
    if resource == 'cpu':
        if re.fullmatch(r'(?:0|[1-9][0-9]*)m', value):
            return int(value[:-1])
        if re.fullmatch(r'(?:0|[1-9][0-9]*)(?:\.[0-9]{1,3})?', value):
            whole, _, fraction = value.partition('.')
            return int(whole) * 1000 + int((fraction or '0').ljust(3, '0'))
    else:
        match = re.fullmatch(r'(0|[1-9][0-9]*)(k|M|G|T|P|E|Ki|Mi|Gi|Ti|Pi|Ei)?', value)
        if match:
            suffix = match.group(2) or ''
            factors = {'': 1}
            factors.update({unit: 1000 ** exponent for exponent, unit in enumerate(('k', 'M', 'G', 'T', 'P', 'E'), 1)})
            factors.update({unit: 1024 ** exponent for exponent, unit in enumerate(('Ki', 'Mi', 'Gi', 'Ti', 'Pi', 'Ei'), 1)})
            return int(match.group(1)) * factors[suffix]
    raise ValueError('unsupported resource quantity')


def includes_administrative_root(path):
    """Conservative root-overlap guard, not a native policy evaluator."""
    first_segment = path.lstrip('/').split('/')[0]
    return first_segment == '+' or any(fnmatchcase(root, first_segment) for root in ('sys', 'auth'))


def invariants(data):
    errors = []
    ordinary, keys = data['ordinaryStore'], data['tenantStore']
    def bad(path, why):
        errors.append(f'{path}: {why}')
    if data['environment']['name'] != 'staging':
        bad('/environment/name', 'this preparation targets staging')
    if data['environment']['applicationNamespace'] == data['environment']['dataNamespace']:
        bad('/environment', 'application and data namespaces must differ')
    if ordinary and keys:
        ordinary_tls, key_tls = ordinary['nativeTls'], keys['nativeTls']
        if ordinary_tls and key_tls and all(ordinary_tls[field] and key_tls[field] for field in ('endpoint', 'namespace', 'podLabels')):
            if (ordinary_tls['endpoint'].rstrip('/') == key_tls['endpoint'].rstrip('/') and
                    ordinary_tls['namespace'] == key_tls['namespace'] and ordinary_tls['podLabels'] == key_tls['podLabels']):
                bad('/tenantStore/nativeTls', 'ordinary and tenant stores cannot identify the same service destination')
        for key in ('instanceId', 'snapshotDomainId'):
            if ordinary[key] is not None and ordinary[key] == keys[key]:
                bad('/tenantStore/'+key, 'must differ from ordinary OpenBao; KV paths cannot filter a whole-instance snapshot')
        if ordinary['storage'] and keys['storage']:
            o, k = ordinary['storage'], keys['storage']
            if o['stateId'] is not None and o['stateId'] == k['stateId']:
                bad('/tenantStore/storage/stateId', 'tenant keys require separate physical persisted state')
            if o['volumeIds'] and k['volumeIds'] and set(o['volumeIds']) & set(k['volumeIds']):
                bad('/tenantStore/storage/volumeIds', 'ordinary and tenant-key physical volumes must be disjoint')
        boundary = data['snapshotBoundary']
        om, km = boundary['ordinaryStateMembers'], boundary['tenantKeyStateMembers']
        if om and km:
            if set(om) & set(km):
                bad('/snapshotBoundary', 'ordinary and tenant-key whole-state snapshot membership must be disjoint')
            if ordinary['storage'] and keys['storage']:
                if ordinary['storage']['stateId'] not in om or keys['storage']['stateId'] not in km:
                    bad('/snapshotBoundary', 'each selected physical store must belong to its declared snapshot class')
                if keys['storage']['stateId'] in om or ordinary['storage']['stateId'] in km:
                    bad('/snapshotBoundary', 'tenant-key state cannot enter ordinary snapshots')
    for label, store in (('ordinaryStore', ordinary), ('tenantStore', keys)):
        if store and store['resources']:
            parsed = {}
            for key, value in store['resources'].items():
                if value is not None:
                    try:
                        parsed[key] = resource_quantity(value, 'cpu' if key.startswith('cpu') else 'memory')
                    except ValueError:
                        bad('/'+label+'/resources/'+key, 'outside the documented conservative resource-quantity subset')
            for resource in ('cpu', 'memory'):
                request, limit = resource+'Request', resource+'Limit'
                if request in parsed and limit in parsed and parsed[request] > parsed[limit]:
                    bad('/'+label+'/resources/'+request, 'resource request exceeds its matching limit')
        if store and store['nativeTls']:
            tls = store['nativeTls']
            if tls['namespace'] is not None and tls['namespace'] != data['environment']['dataNamespace']:
                bad('/'+label+'/nativeTls/namespace', 'store must be within the staging data namespace')
            if tls['endpoint'] and tls['port']:
                explicit = re.search(r':([0-9]+)/?$', tls['endpoint'])
                if not explicit:
                    bad('/'+label+'/nativeTls/endpoint', 'explicit owner-supplied endpoint port required')
                elif int(explicit.group(1)) != tls['port']:
                    bad('/'+label+'/nativeTls', 'endpoint and listener port disagree')
    backup = data['backup']
    if backup['ordinary'] and backup['tenantKeys']:
        for key in ('custodyClass', 'destinationRef', 'accessPrincipal', 'decryptionCustodyRef'):
            value = backup['ordinary'][key]
            if value is not None and value == backup['tenantKeys'][key]:
                bad('/backup/tenantKeys/'+key, 'tenant-key custody must be separate from ordinary ciphertext backups')
    auth = data['authority']
    if auth['environmentLayerPrincipal'] and auth['sharedInfrastructurePrincipal']:
        if auth['environmentLayerPrincipal'] == auth['sharedInfrastructurePrincipal']:
            bad('/authority', 'layer principals must differ')
        if auth['jobPrincipals'] and set(auth['jobPrincipals']) & {auth['environmentLayerPrincipal'], auth['sharedInfrastructurePrincipal']}:
            bad('/authority/jobPrincipals', 'job and layer principals must differ')
    custody = data['custody']
    if custody['administrator'] is not None and custody['administrator'] == custody['deputy']:
        bad('/custody/deputy', 'independent Administrator and deputy required')
    boot = data['bootstrapToken']
    if boot['cutoverDeadline'] and boot['historicalExpiry'] and date.fromisoformat(boot['cutoverDeadline']) >= date.fromisoformat(boot['historicalExpiry']):
        bad('/bootstrapToken/cutoverDeadline', 'replacement must complete before the historically asserted expiry')
    apps = data['secretContract']['apps']
    if apps:
        for key in ('appId', 'serviceAccount', 'componentName'):
            values = [app[key] for app in apps if app[key] is not None]
            if len(values) != len(set(values)):
                bad('/secretContract/apps', f'per-app {key} identities must be unique')
        tokens = [app['token'] for app in apps if app['token']]
        refs = [t['bootstrapSecretRef'] for t in tokens if t['bootstrapSecretRef']]
        if len(refs) != len(set(refs)):
            bad('/secretContract/apps', 'shared bootstrap token references cannot satisfy per-app delivery')
        for i, app in enumerate(apps):
            path = f'/secretContract/apps/{i}'
            if app['appId'] and app['scopes'] is not None and app['scopes'] != [app['appId']]:
                bad(path+'/scopes', 'Component scope must contain only its app ID')
            if app['allowedSecretPaths'] and any(includes_administrative_root(p) for p in app['allowedSecretPaths']):
                bad(path+'/allowedSecretPaths', 'global wildcard and administrative paths cannot establish least privilege')
            if app['token']:
                token = app['token']
                if token['bootstrapSecretRef'] == boot['name']:
                    bad(path+'/token/bootstrapSecretRef', 'historical shared token cannot become per-app delivery')
                if token['mountPath'] and (not token['mountPath'].startswith('/') or '..' in token['mountPath'].split('/')):
                    bad(path+'/token/mountPath', 'absolute non-traversing token file path required')
                directory, filename = token['mountDirectory'], token['projectedFileName']
                if directory and (not directory.startswith('/') or '..' in directory.split('/') or '.' in directory.split('/')):
                    bad(path+'/token/mountDirectory', 'absolute non-traversing directory required')
                if filename and ('/' in filename or filename in {'.', '..'}):
                    bad(path+'/token/projectedFileName', 'single relative file name required')
                if directory and filename and token['mountPath'] and token['mountPath'] != directory.rstrip('/') + '/' + filename:
                    bad(path+'/token/mountPath', 'full token file path must match directory mount plus projected filename')
                renewal = token['renewal']
                if renewal:
                    if renewal['ttlSeconds'] and renewal['renewBeforeSeconds'] and renewal['renewBeforeSeconds'] >= renewal['ttlSeconds']:
                        bad(path+'/token/renewal', 'renewal lead time must be less than the token TTL')
            secrets = app['requiredSecrets']
            if secrets:
                ids = [s['secretId'] for s in secrets if s['secretId'] is not None]
                if len(ids) != len(set(ids)):
                    bad(path+'/requiredSecrets', 'duplicate logical secret requirement')
    return errors


def assess(data):
    schema = read_json(HERE / 'inputs.schema.json')
    missing, errors = schema_check(data, schema)
    if not errors:
        errors += provenance_errors(data)
        errors += invariants(data)
    if missing or errors:
        problems = errors + [f'{m["path"]}: missing; owner={m["owner"]}; question={m["question"]}' for m in missing]
        raise Refused(problems)
    return {'kind': 'offline-staging-secrets-assessment', 'status': 'PREPARED_UNAPPROVED',
            'evidenceMode': data['evidenceMode'], 'prepared': True, 'approvalGranted': False,
            'liveAcceptance': False, 'proposedContractOnly': True, 'deployableOutput': False,
            'processOrNetworkEffects': False, 'missingInputs': [], 'invalidInputs': []}


def observation_schema():
    def obj(props):
        return {'type':'object', 'additionalProperties':False, 'required':list(props), 'properties':props}
    string = {'type':'string','minLength':1}
    positive = {'type':'integer','minimum':1}
    generations = obj({'secretId':string, 'generation':positive, 'present':{'type':'boolean','const':True}})
    app = obj({'appId':string, 'tokenDeliveryRevision':positive, 'podUid':string,
               'sidecarStartedAfterDelivery':{'type':'boolean','const':True},
               'rotationAcknowledged':{'type':'boolean','const':True},
               'generations':{'type':'array','minItems':1,'uniqueItems':True,'items':generations}})
    return obj({'environment':string, 'contractDigest':{'type':'string','pattern':'^sha256:[0-9a-f]{64}$'},
                'apps':{'type':'array','minItems':1,'uniqueItems':True,'items':app}})


def readiness(data, observations):
    report = assess(data)
    _, errors = schema_check(observations, observation_schema())
    if errors:
        raise Refused(errors)
    if observations['environment'] != data['environment']['name']:
        errors.append('/environment: observation belongs to another environment')
    if observations['contractDigest'] != data['secretContract']['digest']:
        errors.append('/contractDigest: observation does not match the exact required contract bytes')
    required = {a['appId']:a for a in data['secretContract']['apps']}
    observed = {}
    pod_uids = set()
    for app in observations['apps']:
        app_id = app['appId']
        if app_id in observed:
            errors.append('/apps: duplicate app observation')
        observed[app_id] = app
        if app['podUid'] in pod_uids:
            errors.append('/apps/podUid: one pod identity cannot be reused across apps')
        pod_uids.add(app['podUid'])
        if app_id not in required:
            errors.append('/apps: observation contains an undeclared app')
            continue
        wanted = required[app_id]
        if app['tokenDeliveryRevision'] != wanted['token']['deliveryRevision']:
            errors.append('/apps/tokenDeliveryRevision: absent/stale rotation delivery revision')
        generations = {}
        for record in app['generations']:
            if record['secretId'] in generations:
                errors.append('/apps/generations: duplicate logical secret observation')
            generations[record['secretId']] = record['generation']
        exact = {s['secretId']:s['generation'] for s in wanted['requiredSecrets']}
        if generations != exact:
            errors.append('/apps/generations: absent, stale, unexpected or nonexact required generation')
    if set(observed) != set(required):
        errors.append('/apps: complete matching app observations required')
    if errors:
        raise Refused(errors)
    report.update(kind='offline-proposed-readiness-assessment', metadataReady=True,
                  meaning='One supplied pod observation per app; metadata consistency only. No all-replica coverage, restart/new-pod authenticity, deployed readiness or live acceptance established.')
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    for command in ('assess', 'readiness'):
        child = sub.add_parser(command)
        child.add_argument('--inputs', required=True)
        if command == 'readiness':
            child.add_argument('--observations', required=True)
    args = parser.parse_args(argv)
    try:
        inputs = read_json(args.inputs)
        result = (assess(inputs) if args.command == 'assess' else
                  readiness(inputs, read_json(args.observations)))
        print(json.dumps(result, indent=2, sort_keys=True))
    except Refused as error:
        for problem in error.problems:
            print('refused: '+problem, file=sys.stderr)
        return 2
    return 0


if __name__ == '__main__':
    sys.exit(main())
