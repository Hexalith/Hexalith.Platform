"""Offline validation of actual source-emitted loop-4 component vectors; never invokes a role or owner command."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile

spec = importlib.util.spec_from_file_location('loop4_offline', Path(sys.argv[1]) / 'tools/ext_protection_1.py')
v = importlib.util.module_from_spec(spec); spec.loader.exec_module(v)
packet = json.load(sys.stdin)
with tempfile.TemporaryDirectory() as directory:
    root = Path(directory); anchor = root / 'capability.pem'; anchor.write_text(packet['publicKeyPem'])
    entries = [dict(issuer='qualified-issuer', audience='protection-audience', tenantId='tenant-a', keyFamily='DeletionBatchCapabilitySigningKey',
                    keyVersion=version, publicAnchorId='test-anchor', publicAnchorVersion='1', publicAnchorFile=str(anchor),
                    publicAnchorSha256=hashlib.sha256(anchor.read_bytes()).hexdigest(), trustProfileRevision=1) for version in ('healthy-key-a', 'healthy-key-b')]
    snapshot = v.validate_capability_trust(entries, {})
    if packet['kind'] == 'hold':
        observed = packet['observation']
        def verify(value):
            v.bind_hold_originals(value); v.bind_hold_scope(value, packet['targets'], snapshot)
        verify(observed)
        dispatch = next(i for i, item in enumerate(observed['guardAttempts']) if item['Transition']['Operation'] == 21)
        hold = next(i for i, item in enumerate(observed['guardAttempts']) if item['Transition']['OperationId'] == observed['holdOrders'][0]['holdOperationId'])
        mutations = [lambda x: x['guardAttempts'][dispatch]['Transition'].update(TenantId='tenant-b'),
                     lambda x: x['guardAttempts'][dispatch]['Transition'].update(DeletionRequestId='foreign-request'),
                     lambda x: x['guardAttempts'][dispatch]['Transition'].update(Scope=x['holdContexts'][0]['Scope']),
                     lambda x: x['guardAttempts'][dispatch]['Transition']['Batch']['Targets'].pop(),
                     lambda x: x['guardAttempts'][dispatch]['Transition']['Batch'].update(BatchId='foreign-batch'),
                     lambda x: x['guardAttempts'][dispatch]['Transition'].update(ReferenceId='foreign-reference'),
                     lambda x: x['guardAttempts'][hold]['Transition']['Hold'].update(HoldId='foreign-hold'),
                     lambda x: x['holdContexts'][0]['Scope'].update(SourceConversationId='foreign-conversation'),
                     lambda x: x['guardAttempts'][dispatch]['Transition']['Batch'].update(DetachedJws=x['guardAttempts'][dispatch]['Transition']['Batch']['DetachedJws'][:-3]+'AAA')]
        for mutate in mutations:
            altered = copy.deepcopy(observed); mutate(altered)
            for attempt in altered['guardAttempts']:
                attempt['Receipt']['IntentDigest'] = hashlib.sha256(v.native_json_bytes({'Transition': attempt['Transition'], 'TargetMutationDigest': attempt['TargetMutationDigest']})).hexdigest().upper()
            try: verify(altered)
            except (v.Failed, v.Blocked): pass
            else: raise AssertionError('Substituted outer hold authority accepted')
    else:
        role = root / 'role.pem'; role.write_text(packet['rolePublicKeyPem'])
        response = packet['response']; v.verify_statement(response['statement'], response['signature'], role, directory)
        observed = response['statement']['response']
        v.original_evidence(observed, packet['targets'], entries, {}, consumed=True, snapshot=snapshot)
        assert type(observed['providerResults'][0]['State']) is int and observed['providerResults'][0]['State'] == 1
        if packet['caseId'] == 'P29':
            assert type(observed['activationOriginal']['ActiveCredentialCount']) is int and observed['activationOriginal']['ActiveCredentialCount'] == 1
            v.bind_activation_original(observed)
print(json.dumps({'status': 'PASS', 'sourceComponentOnly': True, 'liveExecuted': False}))
