"""Negative controls for the Package B checkout (supplementary, run by checks.py).

Runs in the held disposable checkout. Each control temporarily removes or
inverts one Package B behavior, runs the mandatory-focused tests (or the
component metadata check) with the verified tools on PATH, and restores the
exact file bytes. A control passes only when the weakened source fails without
a build failure. Exits 0 only if every control is detected and the checkout is
byte-identical and clean afterwards. Nothing here is a broker or Dapr run.
"""
import json
from pathlib import Path
import subprocess
import sys

FOCUS = 'Mandatory|TestCreateMetadata|ReturnListener'


def tests(focus=FOCUS):
    return ['go', 'test', '-count=1', '-run', focus, './pubsub/rabbitmq']


METADATA = ['make', 'check-component-metadata']
CONTROLS = [
    ('post-ACK return check removed', 'rabbitmq.go',
     '\tif !c.mandatory {\n\t\treturn true, nil\n\t}\n', '\tif true {\n\t\treturn true, nil\n\t}\n'),
    # The adapter-level closed-listener test would call the SDK on a nil channel
    # and panic, so this control names the native peer test that must catch it.
    ('return drain removed', 'rabbitmq.go',
     '\t\tcase _, ok := <-c.returns:\n\t\t\tif !ok {\n\t\t\t\treturn nil, c.closedError(ctx)\n\t\t\t}\n\t\tdefault:', '\t\tdefault:',
     '^TestPublishPublicNativeMandatoryStaleReturnDrained$'),
    ('return listener not registered', 'rabbitmq.go', '\t\tc.returns = c.NotifyReturn(make(chan amqp.Return, 1))\n', ''),
    ('unresolved generation not marked', 'rabbitmq.go', '\tr.unresolvedPublish = true\n\treturn nil\n', '\treturn r.channel\n'),
    ('unresolved generation published on', 'rabbitmq.go', '\tif r.unresolvedPublish {\n\t\t// A return', '\tif false {\n\t\t// A return'),
    ('opt-out marks generations', 'rabbitmq.go',
     '\tif !r.metadata.PublishMandatory {\n\t\treturn r.channel\n\t}\n\tr.unresolvedPublish = true', '\tr.unresolvedPublish = true'),
    ('mark not cleared with the generation', 'rabbitmq.go', '\tr.unresolvedPublish = false\n', ''),
    ('unroutable not terminal', 'rabbitmq.go', ' || errors.Is(err, errPublishUnroutable) {\n\t\t\treturn pubsub.NewTerminalError(err)',
     ' {\n\t\t\treturn pubsub.NewTerminalError(err)'),
    ('mismatched return accepted', 'rabbitmq.go', 'returned.Exchange != c.exchange || returned.RoutingKey != c.key', 'false'),
    ('exchange mismatch accepted', 'rabbitmq.go',
     'returned.Exchange != c.exchange || returned.RoutingKey != c.key', 'returned.RoutingKey != c.key'),
    ('routing key mismatch accepted', 'rabbitmq.go',
     'returned.Exchange != c.exchange || returned.RoutingKey != c.key', 'returned.Exchange != c.exchange'),
    ('attempt routing key not recorded', 'rabbitmq.go',
     'mandatory: mandatory, exchange: exchange, key: key}, err', 'mandatory: mandatory, exchange: exchange}, err'),
    ('mandatory flag not sent', 'rabbitmq.go',
     'routingKey, r.metadata.PublishMandatory, false, p)', 'routingKey, false, false, p)'),
    ('NACK treated as unresolved', 'rabbitmq.go',
     'errors.Is(err, errPublishConfirmation) || errors.Is(err, errPublishUnroutable)', 'errors.Is(err, errPublishUnroutable)'),
    ('return treated as unresolved', 'rabbitmq.go',
     'errors.Is(err, errPublishConfirmation) || errors.Is(err, errPublishUnroutable)', 'errors.Is(err, errPublishConfirmation)'),
    ('closed listener after ACK fails the publication', 'rabbitmq.go',
     '\t\tif !ok {\n\t\t\treturn true, nil\n\t\t}\n\t\tif returned', '\t\tif !ok {\n\t\t\treturn false, errPublishReturnMismatch\n\t\t}\n\t\tif returned'),
    ('publisherConfirm requirement removed', 'metadata.go',
     'if result.PublishMandatory && !result.PublisherConfirm {', 'if false {'),
]
package = Path('pubsub/rabbitmq')
results = []


def status():
    return subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=all'], text=True)


def run(name, path, mutate, command, detected):
    original = path.read_bytes()
    try:
        path.write_bytes(mutate(original.decode()).encode())
        completed = subprocess.run(command, capture_output=True, text=True)
    finally:
        path.write_bytes(original)
    output = completed.stdout + completed.stderr
    failed = sorted({line.split()[2] for line in completed.stdout.splitlines()
                     if line.startswith('--- FAIL: ')})
    record = {'control': name, 'file': str(path), 'command': command, 'exitCode': completed.returncode,
              'failedTopLevelTests': failed, 'panicked': 'panic: ' in output,
              'buildFailed': '[build failed]' in output or '[setup failed]' in output}
    record['detected'] = detected(completed, output, record)
    results.append(record)
    print(json.dumps(record), flush=True)


before = status()
for name, file, old, new, *focus in CONTROLS:
    def mutate(text, old=old, new=new, name=name):
        if text.count(old) != 1:
            raise SystemExit('control does not match the checkout exactly once: ' + name)
        return text.replace(old, new)
    run(name, package / file, mutate, tests(*focus),
        lambda completed, output, record: completed.returncode != 0 and not record['buildFailed']
        and (bool(record['failedTopLevelTests']) or record['panicked']))


def drop_yaml_entry(text):
    start = text.index('  - name: publishMandatory\n')
    end = text.index('  - name: ', start + 1)
    return text[:start] + text[end:]


run('metadata.yaml entry removed', package / 'metadata.yaml', drop_yaml_entry, METADATA,
    lambda completed, output, record: completed.returncode != 0 and '"publishMandatory"' in output
    and 'missing metadata' in output)
after = status()
summary = {'controls': len(results), 'detected': sum(r['detected'] for r in results),
           'statusBefore': before, 'statusAfter': after, 'restored': before == after == ''}
summary['result'] = 'PASS' if summary['detected'] == summary['controls'] and summary['restored'] else 'FAIL'
print(json.dumps({'summary': summary}), flush=True)
sys.exit(0 if summary['result'] == 'PASS' else 1)
