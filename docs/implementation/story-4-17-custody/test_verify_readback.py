"""Focused local refusal checks; stdlib only, no network or mutation probes."""
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import tempfile

import verify_readback as v


def run():
    results = []

    def refuses(name, code, operation):
        try:
            operation()
        except v.Refused as error:
            passed = str(error) == code
        except Exception:
            passed = False
        else:
            passed = False
        results.append({'test': name, 'expectedRefusalCode': code,
                        'outcome': 'PASS' if passed else 'FAIL'})

    at = datetime(2026, 11, 14, 2, 0, tzinfo=timezone.utc)
    original = {'VersionId': 'expected-version', 'ContentLength': 7,
                'ServerSideEncryption': 'AES256', 'ObjectLockMode': 'COMPLIANCE',
                'LastModified': at - timedelta(hours=1),
                'ObjectLockRetainUntilDate': at - timedelta(hours=1) + timedelta(days=30)}
    retention = {'Retention': {'Mode': 'COMPLIANCE',
                              'RetainUntilDate': original['ObjectLockRetainUntilDate']}}

    def protection(head=None, get=None, hold=None, check_at=at):
        return v.version_protection(head or original, get or original, hold or retention,
                                    'expected-version', 7, check_at)

    protection()
    results.append({'test': 'complete-version-bound-protection-positive-control', 'outcome': 'PASS'})
    for name, response, field, value, code in (
            ('wrong-head-version', 'head', 'VersionId', 'wrong', 'response-version-mismatch'),
            ('wrong-get-version', 'get', 'VersionId', 'wrong', 'response-version-mismatch'),
            ('wrong-head-content-length', 'head', 'ContentLength', 8, 'response-length-mismatch'),
            ('missing-aes256', 'get', 'ServerSideEncryption', None, 'object-encryption-inadequate'),
            ('governance-instead-of-compliance', 'head', 'ObjectLockMode', 'GOVERNANCE',
             'object-retention-mode-inadequate')):
        changed = dict(original, **{field: value})
        refuses(name, code, lambda response=response, changed=changed: protection(**{response: changed}))
    short = dict(original, ObjectLockRetainUntilDate=original['LastModified'] + timedelta(days=29))
    short_retention = {'Retention': {'Mode': 'COMPLIANCE', 'RetainUntilDate': short['ObjectLockRetainUntilDate']}}
    refuses('retention-less-than-thirty-days', 'object-retention-duration-inadequate',
            lambda: protection(short, short, short_retention))
    refuses('retention-no-longer-valid', 'object-retention-duration-inadequate',
            lambda: protection(check_at=at + timedelta(days=31)))
    mismatched = {'Retention': {'Mode': 'COMPLIANCE',
                               'RetainUntilDate': original['ObjectLockRetainUntilDate'] + timedelta(days=1)}}
    refuses('retention-api-date-mismatch', 'retention-api-mismatch', lambda: protection(hold=mismatched))
    wrong_retention = deepcopy(retention)
    wrong_retention['ResponseMetadata'] = {'HTTPHeaders': {'x-amz-version-id': 'wrong'}}
    refuses('retention-response-version-mismatch', 'retention-response-version-mismatch',
            lambda: protection(hold=wrong_retention))

    # Local fixture copies exercise the downloaded-file verifier without touching originals.
    staging = v.ROOT / 'artifacts/story-4-17-custody-staging-2026-11-14'
    with tempfile.TemporaryDirectory(prefix='story-4-17-verifier-negative-', dir=v.ROOT / 'artifacts') as tmp:
        directory = Path(tmp)
        os.chmod(directory, 0o700)
        refuses('missing-downloaded-file', 'download-missing-or-unsafe',
                lambda: v.read_download(directory, '13638347236.zip'))
        for name in ('13638347236.zip', 'manifest.json'):
            source = (staging / name).read_bytes()
            v.content(source, name)
            changed = bytearray(source)
            changed[len(changed) // 2] ^= 1
            path = directory / name
            path.write_bytes(changed)
            path.chmod(0o600)
            refuses('tampered-downloaded-' + name, 'downloaded-commitment-mismatch',
                    lambda name=name: v.read_download(directory, name))
        refuses('tampered-manifest-content-gate', 'downloaded-commitment-mismatch',
                lambda: v.manifest_rows((directory / 'manifest.json').read_bytes()))

    # Independent full local source checks are only a positive control, never custody evidence.
    rows = v.manifest_rows((staging / 'manifest.json').read_bytes())
    for name, row in rows.items():
        v.inspect_archive((staging / name).read_bytes(), row)
    results.append({'test': 'four-approved-archive-inventory-safety-positive-controls', 'outcome': 'PASS'})

    preflight = json.loads((v.HERE / 'preflight.json').read_bytes())['protection']

    class LocalProtectionFixture:
        def __init__(self, observed):
            self.observed = observed

        def __getattr__(self, name):
            if name == 'get_bucket_policy':
                return lambda **ignored: {'Policy': json.dumps(preflight['bucketPolicy'])}
            return lambda **ignored: deepcopy(self.observed[name])

    observed = deepcopy(preflight['checks'])
    v.current_protection(LocalProtectionFixture(observed))
    results.append({'test': 'approved-current-protection-positive-control', 'outcome': 'PASS'})
    observed['get_bucket_lifecycle_configuration']['Rules'].append(
        {'ID': 'expire-evidence', 'Status': 'Enabled', 'Filter': {'Prefix': 'evidence/'},
         'Expiration': {'Days': 31}})
    refuses('evidence-expiry-rule-blocks-custody', 'current-lifecycle-drift-or-evidence-expiry',
            lambda: v.current_protection(LocalProtectionFixture(observed)))
    refuses('private-material-path', 'private-material-path', lambda: v.scan('private/transcript.txt', b'test'))
    refuses('private-key-signature', 'private-material-signature',
            lambda: v.scan('public.txt', b'-----BEGIN PRIVATE KEY-----'))
    refuses('zip-traversal-path', 'unsafe-entry-path', lambda: v.safe_path('../outside.txt'))

    positive = [{'file': name, 'accepted': True, 'outcome': 'PASS'} for name in v.EXPECTED]
    passed = {'outcome': 'PASS'}
    assert v.accepted(positive, passed, passed, passed)
    failed = deepcopy(positive)
    failed[2].update({'accepted': False, 'outcome': 'FAIL', 'failureCode': 'entry-bytes-mismatch'})
    before = deepcopy(failed)
    require_preserved = not v.accepted(failed, passed, passed, passed) and failed == before
    results.append({'test': 'one-failed-archive-preserved-despite-four-successes',
                    'outcome': 'PASS' if require_preserved else 'FAIL'})
    results.append({'test': 'missing-object-blocks-overall-custody',
                    'outcome': 'PASS' if not v.accepted(positive[:-1], passed, passed, passed) else 'FAIL'})
    results.append({'test': 'failed-current-protection-blocks-five-successes',
                    'outcome': 'PASS' if not v.accepted(positive, {'outcome': 'FAIL'}, passed, passed) else 'FAIL'})
    results.append({'test': 'failed-literal-inventory-blocks-five-successes',
                    'outcome': 'PASS' if not v.accepted(positive, passed, {'outcome': 'FAIL'}, passed) else 'FAIL'})
    results.append({'test': 'failed-negative-checks-block-five-successes',
                    'outcome': 'PASS' if not v.accepted(positive, passed, passed, {'outcome': 'FAIL'}) else 'FAIL'})
    report = {'outcome': 'PASS' if all(r['outcome'] == 'PASS' for r in results) else 'FAIL',
              'completedAtUtc': v.now().isoformat(), 'tests': len(results),
              'passed': sum(r['outcome'] == 'PASS' for r in results), 'results': results,
              'scope': 'Local refusal/positive controls; no network, remote mutation, or custody assertion',
              'scriptSha256': v.digest(Path(v.__file__).read_bytes()),
              'testScriptSha256': v.digest(Path(__file__).read_bytes())}
    v.save(v.HERE / 'verifier-negative-checks.json', report)
    print(json.dumps({'outcome': report['outcome'], 'tests': report['tests'], 'passed': report['passed']}))
    return 0 if report['outcome'] == 'PASS' else 1


if __name__ == '__main__':
    try:
        raise SystemExit(run())
    except Exception:
        print('{"outcome":"FAIL","details":"withheld"}')
        raise SystemExit(1)
