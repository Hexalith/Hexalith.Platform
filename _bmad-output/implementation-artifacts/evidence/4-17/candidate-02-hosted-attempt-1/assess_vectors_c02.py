"""Assess candidate-02 attempt-1 vectors against the verifier verdict, attempt 2 and the proposal's predictions.

Usage: python3 -I assess_vectors_c02.py PACKET_DIR LOCAL_VERIFICATION_JSON EVIDENCE_DIR ATTEMPT2_VECTORS_JSON OUTPUT_JSON
Packet data is parsed only. Verified status comes solely from the independent verifier's integrity verdict.
"""
import hashlib
import json
import sys
from pathlib import Path

packet, verification, evidence, attempt2, output = (Path(a) for a in sys.argv[1:6])
summary = json.loads((packet / 'packet.json').read_text())
local = json.loads(verification.read_text())
integrity_out = (evidence / 'local-verifier-integrity.stdout').read_text()
integrity = json.loads(integrity_out) if local['verifiers']['integrity']['exitCode'] == 0 else None
verified = integrity is not None and integrity.get('integrity') == 'PASS'
previous = {row['vector']: row['producerResult'] for row in json.loads(attempt2.read_text())['vectors']}
HISTORICAL = {'rabbitmq/secret-rotation', 'rabbitmq/cancellation-backpressure', 'rabbitmq/missing-bindings',
              'kafka/secret-rotation', 'kafka/cancellation-backpressure'}
PREDICTION = {
    'rabbitmq/cancellation-backpressure': 'FAIL (certain)', 'rabbitmq/missing-bindings': 'FAIL (certain)',
    'rabbitmq/secret-rotation': 'FAIL (certain)', 'kafka/cancellation-backpressure': 'FAIL (certain: negativeConfirmRejected)',
    'rabbitmq/durable-quarantine': 'PASS (predicted)', 'rabbitmq/retention-expiry': 'PASS (predicted)',
    'kafka/secret-rotation': 'unknown'}
rows = []
for broker, scenarios in summary['brokers'].items():
    for scenario, row in scenarios.items():
        vector = f'{broker}/{scenario}'
        observation = json.loads((packet / row['observation']).read_text())
        entry = {'vector': vector, 'producerResult': row['result'], 'attempt2ProducerResult': previous.get(vector),
                 'historicallyRejected': vector in HISTORICAL, 'prediction': PREDICTION.get(vector, 'PASS (unchanged)'),
                 'verified': verified,
                 'verifierResult': (None if not verified else ('FAIL' if vector in integrity.get('failedScenarios', []) else 'PASS')),
                 'observationSha256': hashlib.sha256((packet / row['observation']).read_bytes()).hexdigest()}
        if row['result'] != 'PASS':
            entry['failureDetail'] = {k: observation[k] for k in ('blockedBy', 'failureClass', 'acknowledgedLoss', 'negativeConfirmRejected',
                                                                 'oldGenerationDenied', 'stalePublishStatus', 'unboundPublishStatus', 'bindingAbsent',
                                                                 'survivedRestart', 'quarantineEffects', 'brokerRetentionObserved') if k in observation}
        if vector in ('kafka/cancellation-backpressure', 'rabbitmq/cancellation-backpressure'):
            entry['pressure'] = {k: observation.get(k) for k in ('acknowledgedLoss', 'negativeConfirmRejected', 'cancelled', 'cancelledEffects', 'boundedSeconds')}
        expected = entry['prediction'].split(' ')[0]
        entry['matchesPrediction'] = None if expected == 'unknown' else (entry['verifierResult'] or entry['producerResult']) == expected
        rows.append(entry)
result = {'packetJsonSha256': hashlib.sha256((packet / 'packet.json').read_bytes()).hexdigest(), 'integrityVerified': verified,
          'technicalQualification': integrity.get('technicalQualification') if integrity else None,
          'counts': {'observed': len(rows), 'producerPass': sum(r['producerResult'] == 'PASS' for r in rows),
                     'verifiedPass': sum(r['verifierResult'] == 'PASS' for r in rows) if verified else 0,
                     'verifiedFail': sum(r['verifierResult'] == 'FAIL' for r in rows) if verified else 0},
          'predictionMisses': [r['vector'] for r in rows if r['matchesPrediction'] is False], 'vectors': rows}
output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({k: result[k] for k in ('integrityVerified', 'technicalQualification', 'counts', 'predictionMisses')}, indent=1))
for r in rows:
    if r['producerResult'] != 'PASS' or r['vector'] in PREDICTION:
        print(r['vector'], r['producerResult'], r['verifierResult'], r['prediction'], r.get('failureDetail', ''))
