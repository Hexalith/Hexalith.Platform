"""Derive the six attempt-2 producer failures' causes from the packet, with cited receipt indexes.

Usage: python3 -I diagnose_vectors.py PACKET_DIR RESULT_JSON
The packet is data only: JSON is parsed, nothing is executed or imported from it. Every claim in the
result is recomputed from receipts and retained process/broker logs, and the script asserts each one,
so a different packet fails loudly instead of producing a misleading record.
"""
import datetime
import hashlib
import json
import re
import sys
from pathlib import Path

packet, result = Path(sys.argv[1]), Path(sys.argv[2])
commands = json.loads((packet / 'commands.json').read_text())
summary = json.loads((packet / 'packet.json').read_text())


def observation(vector):
    return json.loads((packet / vector / 'observation.json').read_text())


def receipt(index):
    row = commands[index]
    return {'index': index, 'argv': row['argv'][:12], 'startedUtc': row['startedUtc'], 'endedUtc': row['endedUtc'],
            'exitCode': row['exitCode'], **({'httpStatus': row['httpStatus'], 'elapsedSeconds': round(row['elapsedSeconds'], 3)} if 'httpStatus' in row else {})}


def http_error(index):
    body = json.loads(commands[index]['output'])
    return next(d['metadata']['error'] for d in body['details'] if d.get('@type', '').endswith('ErrorInfo'))


def log_lines(index, pattern, start='', end='~'):
    rows = []
    for line in commands[index]['output'].splitlines():
        match = re.search(r'time="([^"]+)" level=(\w+) msg="(.*?)" app_id', line)
        if match and start <= match[1][11:23] <= end and re.search(pattern, match[3]):
            rows.append(f'{match[1][11:23]} {match[2]} {match[3]}')
    return rows


def sidecar_log(port):
    return next(i for i, row in enumerate(commands) if '--dapr-http-port' in row['argv'] and row.get('event') != 'native-launch'
                and len(row.get('output', '')) and row['argv'][row['argv'].index('--dapr-http-port') + 1] == port)


def publish_index(message_id):
    return next(i for i, row in enumerate(commands) if isinstance(row.get('input'), dict) and row['input'].get('id') == message_id)


def readback_index(vector):
    return json.loads((packet / vector / 'application-readback.json').read_text())['commandReceipt']


RECONNECT = 'connectionCount|reconnect'
TIMEOUT_SHAPE = 'Dapr resiliency outbound timeout 500 ms x 3 attempts + 2 x 200 ms backoff = 1.9 s'
vectors = {}

# RabbitMQ durable-quarantine / retention-expiry: setup publish on a publisher whose connection the previous
# vector's broker restart severed; candidate-01's 500 ms outbound timeout ends each attempt before the 2 s wait.
for vector, restart, ready, publishes, port in (
        ('rabbitmq/durable-quarantine', 128, 136, [149], '34709'),
        ('rabbitmq/retention-expiry', 396, 404, [416, 417], '50865')):
    assert observation(vector)['blockedBy'] == 'bounded readiness/readback condition not met'
    assert commands[restart]['argv'][:2] == ['docker', 'restart']
    assert 'fully booted and running' in commands[ready]['output']
    for index in publishes:
        assert commands[index]['httpStatus'] == 500 and http_error(index) == 'context deadline exceeded'
        assert abs(commands[index]['elapsedSeconds'] - 1.9) < 0.05
        assert commands[index]['argv'][2].startswith(f'http://127.0.0.1:{port}/')
    log = sidecar_log(port)
    start = commands[publishes[0]]['startedUtc'][11:23]
    lines = log_lines(log, RECONNECT, start, commands[readback_index(vector)]['endedUtc'][11:23])
    assert lines and all('publisher is reconnecting in 2s' in line for line in lines), lines
    vectors[vector] = {
        'classification': 'genuine-broker-behavior',
        'detail': 'candidate-01 configuration effect on the pinned Dapr RabbitMQ component, not a harness or verifier defect',
        'mechanism': ('The previous vector restarted the broker (docker restart) and recreated only the consumer. The '
                      'publisher sidecar found its AMQP channel closed on the next publish. The pinned component waits '
                      'reconnectWait (2 s) before reconnecting but returns when the request context ends '
                      '(rabbitmq.go:321-328). The 500 ms outbound timeout always ends it first, so no attempt ever '
                      'reconnects: ' + TIMEOUT_SHAPE + ', then HTTP 500 "context deadline exceeded". The vector never '
                      'exercised its guarantee; the harness then waited 45 s for an effect that could not arrive.'),
        'citedReceipts': {'brokerRestart': receipt(restart), 'brokerReady': receipt(ready),
                          'setupPublishes': [receipt(i) for i in publishes], 'readback': readback_index(vector),
                          'publisherProcessLog': log},
        'publisherLogAfterFirstPublish': lines,
        'noReconnectObserved': not any('connected with connectionCount' in line for line in log_lines(log, 'connected with', start)),
        'notFixed': ('Restarting the publisher after each broker restart would hide a real liveness regression of '
                     'this configuration. The harness reports the setup publish only as a generic readback timeout; '
                     'that is a diagnosability gap, not a verdict defect, and is left unchanged.'),
        'historicallyRejected': False,
        'historicalResult': 'PASS with the default 1 s reconnect wait and 3000 ms timeout (2026-11-10 hosted runs)',
    }

# RabbitMQ secret-rotation: the stale publisher's reconnect dial, the only place ACCESS_REFUSED could appear,
# never runs; even when it does, its error is discarded.
o = observation('rabbitmq/secret-rotation')
stale = o['denialObservation']['publicationReceipt']
assert commands[o['denialObservation']['revocationReceipts'][0]]['argv'][-2:] == ['delete_user', 'qualification']
assert commands[stale]['httpStatus'] == 500 and http_error(stale) == 'context deadline exceeded'
port = commands[stale]['argv'][2].split(':')[2].split('/')[0]
log = sidecar_log(port)
lines = log_lines(log, RECONNECT, commands[stale]['startedUtc'][11:23], commands[stale]['endedUtc'][11:23])
assert lines and all('publisher is reconnecting in 2s' in line for line in lines)
vectors['rabbitmq/secret-rotation'] = {
    'classification': 'genuine-broker-behavior',
    'detail': 'code-level in the pinned Dapr RabbitMQ component; candidate-01 knobs are inapplicable',
    'mechanism': ('After delete_user, the stale publisher sees its channel closed. The only call that could return '
                  'the broker\'s ACCESS_REFUSED is the reconnect dial, but it runs only after reconnectWait and its '
                  'error is discarded (`_ = r.reconnect(...)`, rabbitmq.go:328). Under candidate-01 the dial never '
                  'runs (' + TIMEOUT_SHAPE + '); with any admitted value its error still cannot become the publish '
                  'error. The 500 carries "context deadline exceeded", which attributable_denial correctly rejects.'),
    'citedReceipts': {'revocation': receipt(o['denialObservation']['revocationReceipts'][0]),
                      'userState': receipt(o['denialObservation']['stateReceipt']), 'stalePublish': receipt(stale),
                      'readback': readback_index('rabbitmq/secret-rotation'), 'stalePublisherProcessLog': log},
    'stalePublisherLog': lines,
    'historicallyRejected': True,
    'change': 'the stale response moved from the harness 599 timeout (2026-11-10) to Dapr 500 "context deadline exceeded"',
}

# RabbitMQ cancellation-backpressure: broker NACKs returned as HTTP 204.
o = observation('rabbitmq/cancellation-backpressure')
ids = [row['id'] for row in o['publishResults']]
publishes = [publish_index(i) for i in ids]
assert all(commands[i]['httpStatus'] == 204 for i in publishes)
lost = [row['id'] for row in o['publicationReconciliation'][:8] if row['httpStatus'] == 204 and row['effectCount'] == 0]
log = sidecar_log(commands[publishes[0]]['argv'][2].split(':')[2].split('/')[0])
nacks = log_lines(log, 'did not receive confirmation')
assert len(lost) == o['acknowledgedLoss'] == len(nacks) == 3
vectors['rabbitmq/cancellation-backpressure'] = {
    'classification': 'genuine-broker-behavior',
    'detail': 'code-level in the pinned Dapr RabbitMQ component (upstream negative-confirm fix unreleased per the 2026-11-10 record; not re-checked in this offline slice)',
    'mechanism': ('The effective reject-publish/max-length-4 policy was measured. All eight publishes returned 204 '
                  'while the publisher logged three failed confirmations; the pinned publishSync logs the NACK and '
                  'returns nil (rabbitmq.go:290-299), so the broker rejection becomes HTTP 204 and three '
                  'acknowledged IDs are absent at the bound.'),
    'citedReceipts': {'effectivePolicy': o['policyObservation']['queueReceipt'], 'publishes': publishes,
                      'pause': 752, 'cancelledPublish': receipt(publish_index(o['cancelledMessageId'])), 'unpause': 755,
                      'readback': readback_index('rabbitmq/cancellation-backpressure'), 'publisherProcessLog': log},
    'acknowledgedButAbsent': lost, 'publisherNackLog': nacks,
    'historicallyRejected': True,
}

# RabbitMQ missing-bindings: unroutable publish confirmed with mandatory=false.
o = observation('rabbitmq/missing-bindings')
unbound = publish_index(o['unboundMessageId'])
assert commands[unbound]['httpStatus'] == 204 and o['bindingAbsent'] is True
vectors['rabbitmq/missing-bindings'] = {
    'classification': 'genuine-broker-behavior',
    'detail': 'code-level in the pinned Dapr RabbitMQ component',
    'mechanism': ('Binding absence is physically measured before and after the unbound publish, beside a passing '
                  'bound control. The component publishes with mandatory=false (rabbitmq.go:282), so the broker '
                  'confirms the unroutable message and Dapr returns 204.'),
    'citedReceipts': {'boundControlPublish': publish_index(o['messageId']), 'bindingsBefore': o['bindingObservation']['beforeReceipt'],
                      'unboundPublish': receipt(unbound), 'bindingsAfter': o['bindingObservation']['afterReceipt'],
                      'readback': readback_index('rabbitmq/missing-bindings')},
    'historicallyRejected': True,
}

# Kafka cancellation-backpressure: retention removed the two acknowledged records; quota never rejects.
o = observation('kafka/cancellation-backpressure')
publishes = [publish_index(row['id']) for row in o['publishResults']]
acknowledged = [i for i in publishes if commands[i]['httpStatus'] == 204]
throttled = [i for i in publishes if commands[i]['httpStatus'] == 500]
assert len(acknowledged) == o['acknowledgedLoss'] == 2 and len(throttled) == 6
assert all(http_error(i) == 'context deadline exceeded' and abs(commands[i]['elapsedSeconds'] - 9.4) < 0.05 for i in throttled)
before, after_publish, after_readback = (o['brokerStateReceipts'][k] for k in ('before', 'afterPublish', 'afterReadback'))
offsets = {name: [commands[i]['output'].strip() for i in indexes[:2]] for name, indexes in
           (('before', before), ('afterPublish', after_publish), ('afterReadback', after_readback))}
assert offsets == {'before': ['qualification.backpressure:0:0', 'qualification.backpressure:0:0'],
                   'afterPublish': ['qualification.backpressure:0:0', 'qualification.backpressure:0:9'],
                   'afterReadback': ['qualification.backpressure:0:5', 'qualification.backpressure:0:20']}, offsets
assert 'qualification.backpressure' not in commands[before[2]]['output']
consumer = next(i for i in range(publish_index(o['cancelledMessageId']), len(commands)) if commands[i]['argv'][:3] == ['CONFIGURATION', 'render', 'components'])
kafka_log = next(i for i, row in enumerate(commands) if row['argv'][:2] == ['docker', 'logs'] and row['argv'][-1] == json.loads((packet / 'kafka/image-identity.json').read_text())['containerId'])
deletions = [line for line in commands[kafka_log]['output'].splitlines()
             if 'qualification.backpressure' in line and re.search('Deleting segment|Incremented log start offset', line)]
assert any('Deleting segment LogSegment(baseOffset=0' in line and 'retention time 60000ms breach' in line and line.startswith('[2026-11-13 00:10:59,8') for line in deletions)
creation = next(i for i, row in enumerate(commands) if row['argv'][3:4] == ['/opt/kafka/bin/kafka-topics.sh'] and 'qualification.backpressure' in row['argv'])
assert 'retention.ms=60000' in commands[creation]['argv']
duplicates = {row['id']: row['receiptCount'] for row in o['publicationReconciliation'][:8] if row['httpStatus'] == 500}
vectors['kafka/cancellation-backpressure'] = {
    'classification': 'harness-timing-defect (acknowledgedLoss) + genuine-broker-behavior (negativeConfirmRejected)',
    'detail': 'two independent failing predicates; only the harness defect is fixed (EventStore c4b90c42); the vector still fails',
    'mechanism': {
        'acknowledgedLoss': ('The harness creates the pressure topic with retention.ms=60000 and segment.ms=1000 and keeps '
                             'its consumer stopped through six throttled publishes plus native diagnostics. The two 204 '
                             'records (offsets 0-1, 00:09:59.33) sat 65 s unconsumed; Kafka deleted segment 0 for a 60000 ms '
                             'retention breach at 00:10:59.820, 4.5 s before the consumer restarted at 00:11:04.29. The '
                             'group had no committed backpressure offset, so it began at the advanced log start.'),
        'negativeConfirmRejected': ('Produce byte-rate quotas throttle and never reject. Each over-quota publish ran '
                                    '3 x 3 s Dapr timeouts + 2 x 200 ms = 9.40 s and returned 500 "context deadline '
                                    'exceeded", while all three attempts were later appended (log end 20 = 2 + 6 x 3). '
                                    'The only accepted Kafka rejection, "kafka server: The throttling quota has been '
                                    'exceeded", is not produced by this fault.'),
    },
    'citedReceipts': {'topicCreation': receipt(creation), 'groupBefore': before[2], 'quota': o['quotaReceipt'],
                      'acknowledgedPublishes': [receipt(i) for i in acknowledged], 'throttledPublishes': throttled,
                      'offsets': {'before': before[:2], 'afterPublish': after_publish[:2], 'afterReadback': after_readback[:2]},
                      'cancelledPublish': receipt(publish_index(o['cancelledMessageId'])), 'consumerRestartRender': receipt(consumer),
                      'groupAfterReadback': after_readback[2], 'readback': readback_index('kafka/cancellation-backpressure'),
                      'kafkaBrokerLog': kafka_log},
    'offsets': offsets, 'kafkaRetentionLog': deletions, 'receiptCountsOfThrottledIds': duplicates,
    'historicallyRejected': True,
}

for vector in vectors:
    broker, scenario = vector.split('/')
    assert summary['brokers'][broker][scenario]['result'] == 'FAIL'
failed = sorted(f'{b}/{s}' for b, rows in summary['brokers'].items() for s, row in rows.items() if row['result'] != 'PASS')
assert failed == sorted(vectors), failed
result.write_text(json.dumps({
    'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'packetJsonSha256': hashlib.sha256((packet / 'packet.json').read_bytes()).hexdigest(),
    'commandsJsonSha256': hashlib.sha256((packet / 'commands.json').read_bytes()).hexdigest(),
    'status': 'offline diagnosis of producer-reported failures; no vector is verified or credited',
    'classes': {'genuine-broker-behavior': 5, 'harness-timing-defect': 1, 'verifier-gap': 0},
    'vectors': vectors,
    'otherVectors': 'the 18 producer PASS results, including historically rejected kafka/secret-rotation, stay unverified',
}, indent=2) + '\n')
print(json.dumps({vector: row['classification'] for vector, row in vectors.items()}, indent=1))
