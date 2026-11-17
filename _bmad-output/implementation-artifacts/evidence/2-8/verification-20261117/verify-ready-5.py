#!/usr/bin/env python3
"""Read-only exact ready artifact admission. This program never requests cancellation."""
import datetime
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
import sys
import zipfile

ROOT = Path('/home/administrator/projects/hexalith/platform')
OUT = ROOT / 'artifacts/2-8-continuation-20261117'
RUN = int(json.loads((OUT / 'dispatch-5.json').read_text())['runId'])
SOURCE = 'bccab615f9e3d1a92e3bbf648ee89e78a925670b'
CONTEXT = {'kind': 'ci', 'repository': 'Hexalith/Hexalith.Parties',
    'workflowRunId': str(RUN), 'workflowAttempt': 1, 'jobId': 'qualification',
    'runnerKind': 'github-hosted', 'operatingSystem': 'Linux', 'disposable': True}

def require(condition, message):
    if not condition: raise ValueError(message)

def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON key')
        result[key] = value
    return result

def parse(data): return json.loads(data, object_pairs_hook=unique)
def utc(value):
    result = datetime.datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(result.tzinfo is not None, 'Missing timestamp timezone')
    return result

def main():
    archive, metadata, jobs = map(Path, sys.argv[1:])
    meta, jobs = parse(metadata.read_bytes()), parse(jobs.read_bytes())
    now = datetime.datetime.now(datetime.timezone.utc)
    require(meta['name'] == 'lifecycle-cancellation-ready' and not meta['expired'], 'Wrong/expired ready artifact')
    require(meta['workflow_run']['id'] == RUN and meta['workflow_run']['head_sha'] == SOURCE,
            'Ready artifact belongs to another run/source')
    actual_hash = hashlib.sha256(archive.read_bytes()).hexdigest()
    require(meta['digest'] == 'sha256:' + actual_hash, 'Ready archive digest differs')
    selected = [j for j in jobs['jobs'] if j['name'] == 'qualification']
    require(len(selected) == 1, 'No unique qualification job')
    job = selected[0]
    require(job['run_id'] == RUN and job['head_sha'] == SOURCE and job['status'] == 'in_progress',
            'Qualification job context differs or is not running')
    waiting = [s for s in job['steps'] if s['name'] == 'Await parent cancellation of this exact ready run']
    require(len(waiting) == 1 and waiting[0]['status'] == 'in_progress', 'Exact cancellation wait is not active')
    with zipfile.ZipFile(archive) as zipped:
        members = zipped.infolist(); names = [item.filename for item in members]
        require(len(names) == len(set(names)) and len(names) < 1000, 'Duplicate/excessive ZIP members')
        require(sum(item.file_size for item in members) < 64 * 1024 * 1024, 'Ready archive exceeds finite bound')
        for item in members:
            path = PurePosixPath(item.filename)
            require(not path.is_absolute() and '..' not in path.parts and '\\' not in item.filename,
                    'Unsafe ready member path')
            require(not stat.S_ISLNK(item.external_attr >> 16), 'Linked ready member')
        require(zipped.testzip() is None, 'Ready ZIP CRC failure')
        def member(suffix):
            selected = [name for name in names if name == suffix or name.endswith('/' + suffix)]
            require(len(selected) == 1, 'Ready member is absent or ambiguous: ' + suffix)
            return zipped.read(selected[0])
        journal = parse(member('ci-lifecycle/observations.json'))
        inputs = parse(member('ci-lifecycle/inputs.json'))
        events = [parse(line) for line in member('story-2-8-proof-observer/story-2-8-proof/observer.jsonl').splitlines() if line]
        trace_hash = hashlib.sha256(member('story-2-8-proof-observer/story-2-8-proof/signals.trace')).hexdigest()
    require(journal['source'] == inputs['source'] == SOURCE, 'Ready journal source differs')
    require(journal['executionContext'] == inputs['executionContext'] == CONTEXT, 'Ready exact-job binding differs')
    require(journal['mappingHash'] == inputs['mappingHash'] and re.fullmatch(r'[a-f0-9]{64}', inputs['mappingHash']),
            'Ready mapping identity differs')
    manifest = subprocess.check_output(['git', '-C', str(ROOT / 'references/Hexalith.Parties'),
        'show', SOURCE + ':.config/dotnet-tools.json'])
    require(inputs['toolManifestSha256'] == hashlib.sha256(manifest).hexdigest(), 'Ready tool manifest differs')
    owners = journal['owners']
    require(len(owners) == 1 and owners[0]['label'] == 'cancel' and owners[0]['cancellationPhase'] == 'execution',
            'Ready owner is not the unique execution-cancellation owner')
    owner = owners[0]; owner_id = owner['runId']; admission = owner['admission']
    require(re.fullmatch(r'[a-f0-9]{32}', owner_id), 'Missing real owner ID')
    ready = utc(owner['cancellationReadyAtUtc'])
    require(datetime.timedelta(0) <= now - ready <= datetime.timedelta(seconds=15), 'Ready snapshot is not fresh')
    require(admission['ownerRunId'] == owner_id and admission['requestedHoldSeconds'] == admission['effectiveHoldSeconds'] == 240,
            'Original finite hold identity/budget differs')
    accepted, expires = utc(admission['acceptedAtUtc']), utc(admission['expiresAtUtc'])
    require(expires - accepted == datetime.timedelta(seconds=240) and accepted <= ready <= now < expires,
            'Original hold is not active with its unchanged expiry')
    require(admission.get('endedAtUtc') is None and admission.get('outcome') is None, 'Ready hold already ended')
    native, attached = owner['executionCheckpoint'], owner['attachedExecutionCheckpoint']
    require(native['runId'] == owner_id and native['realPersistenceAssertionsPassed'] is True,
            'Owner real native checkpoint differs')
    require(attached['runId'] == admission['attachedRunId'] and attached['realServiceReadsPassed'] is True,
            'Attached native real-service checkpoint differs')
    for key in ['ownerRunId', 'environmentId', 'originalAttachmentRequestId', 'expiresAtUtc']:
        require(attached[key] == admission[key], 'Attached checkpoint lost admission lineage')
    require(any(e['kind'] == 'trace-started' and e['code'] == 0 and e['clock'] == 'mono' for e in events),
            'Successful kernel trace setup is absent')
    starts = [e for e in events if e['kind'] == 'observer-started']
    require(len(starts) == 1 and starts[0]['mode'] == 'lifecycle' and starts[0]['pid'] > 1, 'Observer identity differs')
    require(not any(e['kind'] == 'observer-stopping' for e in events), 'Observer already stopped')
    clocks = [e for e in events if e['kind'] == 'clock']
    require(clocks and 0 <= now.timestamp() * 1e9 - clocks[-1]['wallNs'] <= 15e9, 'No fresh live observer heartbeat')
    for checkpoint, tag in [(native, owner_id), (attached, admission['attachedRunId'])]:
        observations = [e for e in events if e['kind'] in ('process-seen', 'process-gone') and e['pid'] == checkpoint['processId']]
        require(observations and observations[-1]['kind'] == 'process-seen'
            and observations[-1]['startTime'] == checkpoint['birth'] and observations[-1]['runTag'] == tag
            and observations[-1]['composed'] is True, 'Native process identity is absent/reused/gone/unowned in fresh observer')
    states = [e for e in events if e['kind'] == 'owner-state' and e['runId'] == owner_id]
    require(states and states[-1]['status'] == 'Ready' and states[-1]['terminalStatus'] is None,
            'Observer does not show the active nonterminal ready owner')
    receipt = {'verifiedAtUtc': now.isoformat(), 'runId': RUN, 'source': SOURCE, 'jobId': job['id'],
        'executionContext': CONTEXT, 'artifactId': meta['id'], 'artifactSha256': actual_hash,
        'ownerRunId': owner_id, 'environmentId': admission['environmentId'],
        'attachedRunId': admission['attachedRunId'], 'acceptedAtUtc': admission['acceptedAtUtc'],
        'expiresAtUtc': admission['expiresAtUtc'], 'readyAtUtc': owner['cancellationReadyAtUtc'],
        'readyAgeSeconds': (now - ready).total_seconds(), 'observerPid': starts[0]['pid'],
        'lastObserverClock': clocks[-1], 'nativeExecution': native, 'attachedExecution': attached,
        'traceSetupSucceeded': True, 'traceSnapshotSha256': trace_hash, 'allChecksPassed': True,
        'cancellationRequests': 0, 'candidateAcceptance': False, 'toolAccepted': False}
    (OUT / 'run-5-ready-verification.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt, indent=2))

if __name__ == '__main__': main()
