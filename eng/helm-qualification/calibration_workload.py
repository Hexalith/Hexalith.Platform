"""Native independent calibration workload; no campaign producer or authority import.

Only the existing authenticated, bodyless fixed-workload endpoint emits records.
Each writer has an exclusive bounded-process log directory. No retries are made.
"""
from concurrent.futures import ThreadPoolExecutor, wait, FIRST_COMPLETED
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import threading
import tempfile
import time

from qualify import Qualification
import campaign_capacity as capacity
import campaign_lifecycle as lifecycle

ROUTE = 'http://127.0.0.1:8080/operations/access-telemetry/qualification/fixed-workload'


def require(value, code):
    lifecycle.require(value, 'calibration-workload-' + code)


def validate_segment(value, run_id, segment, pod):
    require(isinstance(value, dict) and value.get('runId') == run_id and value.get('segmentId') == segment
            and value.get('writer') == pod, 'foreign-segment')
    require(all(value.get(k) == 125 for k in ('attempted', 'enqueued', 'acknowledged', 'persisted', 'transactionAcknowledgements'))
            and all(value.get(k) == 0 for k in ('conflicted', 'dropped', 'rejected')), 'accounting-loss')
    ids = value.get('recordIds')
    require(isinstance(ids, list) and len(ids) == len(set(ids)) == 125
            and all(isinstance(v, str) and re.fullmatch('[0-9A-HJKMNP-TV-Z]{26}', v) for v in ids), 'record-inventory')
    start, end = value.get('startedUtcMs'), value.get('emitFinishedUtcMs')
    require(type(start) is int and type(end) is int and 950 <= end - start <= 1250, 'segment-timing')
    acknowledged = value.get('acknowledgedUtcMs')
    require(type(acknowledged) is int and acknowledged >= end, 'acknowledgement-timing')
    return start, end, hashlib.sha256(capacity.canonical(ids)).hexdigest()


class Writer(Qualification):
    """Use Qualification's real process bounds without its fixture initialization."""

    def _initialize(self):
        self.record = {'commands': [], 'commandCount': 0}
        self.env = {k: v for k, v in os.environ.items() if k in {'PATH', 'HOME'}}

    def invoke(self, config, pod, segment, emitted, phase):
        authorize(config)
        args = [config['kubectl']['path'], '--kubeconfig', config['kubeconfig'], '--context', config['context'],
            '-n', config['namespace'], 'exec', 'pod/' + pod, '-c', 'memories', '--', '/bin/sh', '-ec',
            'wget -qO- --header="dapr-api-token: $APP_API_TOKEN" '
            '--header="X-Hexalith-Qualification-Run: ' + config['runId'] + '" '
            '--header="X-Hexalith-Qualification-Segment: ' + segment + '" '
            '--header="X-Hexalith-Qualification-Emitted-Utc-Ms: ' + str(emitted) + '" '
            + ('--header="X-Hexalith-Qualification-Phase: emit" ' if phase == 'emit' else '')
            + '--post-data="" ' + ROUTE]
        lifecycle.binary(config['kubectl'])
        require(lifecycle.sha(config['kubeconfig']) == config['kubeconfigSha256'], 'transport-drift')
        # Each concurrent operation owns its own process/log context. Never
        # share command indices, locks or in-flight state between writer threads.
        with tempfile.TemporaryDirectory(prefix='command-', dir=self.evidence) as path:
            bounded = Writer(Path(path))
            try:
                def cancelled():
                    if self._cancel.is_set():
                        raise InterruptedError('calibration-workload-cancelled')
                result = bounded.run(args, timeout=120 if phase == 'emit' else 2400, sensitive=True, maximum_output_bytes=65536, inherit_process_group=True, heartbeat=cancelled)
            finally:
                bounded.close()
        return lifecycle.strict(result.stdout)

    def execute(self, config, index, barrier):
        pod = config['pods'][index]
        barrier.wait(timeout=10)
        origin = time.monotonic()
        first = last = None
        latency = []; inventory = hashlib.sha256()
        # Transport runs independently of the one-second dispatch clock. Both
        # pools have finite admission; an error cancels outstanding native calls.
        self._cancel = threading.Event()
        failure = []; failure_lock = threading.Lock()
        emits = {}; acks = {}; emit_receipts = {}; results = {}
        emit_pool = ThreadPoolExecutor(max_workers=4)
        ack_pool = ThreadPoolExecutor(max_workers=8)

        def failed(future):
            if future.cancelled():
                return
            error = future.exception()
            if error is not None:
                with failure_lock:
                    if not failure:
                        failure.append(error)
                    self._cancel.set()

        def check_failure():
            with failure_lock:
                if failure:
                    raise failure[0]

        def collect():
            check_failure()
            for future in [f for f in emits if f.done()]:
                n, segment, emitted = emits.pop(future)
                response = future.result()
                require(isinstance(response, dict) and response.get('runId') == config['runId']
                    and response.get('segmentId') == segment and response.get('writer') == pod, 'emit-binding')
                start, end = response.get('startedUtcMs'), response.get('emitFinishedUtcMs')
                require(type(start) is int and type(end) is int and 950 <= end - start <= 1250, 'emit-timing')
                emit_receipts[n] = (start, end)
                require(len(acks) < 8, 'acknowledgement-backlog')
                ack = ack_pool.submit(self.invoke, config, pod, segment, emitted, 'ack')
                acks[ack] = (n, segment)
                ack.add_done_callback(failed)
            for future in [f for f in acks if f.done()]:
                n, segment = acks.pop(future)
                response = future.result()
                start, end, digest = validate_segment(response, config['runId'], segment, pod)
                require((start, end) == emit_receipts.pop(n), 'emit-ack-timing-differs')
                results[n] = (segment, start, end, response['acknowledgedUtcMs'], digest)
            check_failure()

        try:
            for n in range(config['segments']):
                due = origin + n
                while time.monotonic() < due:
                    collect()
                    self._cancel.wait(min(.025, max(0, due - time.monotonic())))
                collect()
                require(time.monotonic() - due <= .250, 'dispatch-cadence')
                require(len(emits) < 4, 'emission-backlog')
                segment = config['lane'] + '-w' + str(index + 1) + '-s' + str(n + 1)
                emitted = int(time.time() * 1000)
                future = emit_pool.submit(self.invoke, config, pod, segment, emitted, 'emit')
                emits[future] = (n, segment, emitted)
                future.add_done_callback(failed)
            while emits or acks:
                collect()
                if emits or acks:
                    wait([*emits, *acks], timeout=.025, return_when=FIRST_COMPLETED)
        except BaseException:
            self._cancel.set()
            raise
        finally:
            # Actual invoke() checks cancellation through the existing bounded
            # process heartbeat, terminates its child, and releases its logs.
            emit_pool.shutdown(wait=True, cancel_futures=True)
            ack_pool.shutdown(wait=True, cancel_futures=True)
        for n in range(config['segments']):
            segment, start, end, acknowledged, digest = results[n]
            first = start if first is None else min(first, start)
            last = end if last is None else max(last, end)
            latency.append(acknowledged - start)
            inventory.update(digest.encode())
            proof = {'segment': segment, 'startedUtcMs': start, 'finishedUtcMs': end,
                'acknowledgedUtcMs': acknowledged, 'acceptedRecords': 125, 'inventorySha256': digest}
            with open(self.evidence / 'segments.jsonl', 'a', opener=lambda p, flags: os.open(p, flags | os.O_NOFOLLOW, 0o600)) as stream:
                stream.write(json.dumps(proof, sort_keys=True) + '\n')
        return {'pod': pod, 'acceptedRecords': config['segments'] * 125, 'startedUtcMs': first, 'finishedUtcMs': last,
            'latencyP99Ms': sorted(latency)[min(len(latency) - 1, (len(latency) * 99) // 100)],
            'inventorySha256': inventory.hexdigest()}


def authorize(config):
    # The child rechecks the actual independent authority; an environment flag
    # or a supplied configuration digest cannot authorize a native write.
    import independent_calibration as calibration
    ref = config['plan']
    plan = calibration.load_plan(ref['path'], ref['sha256'])
    state_root = Path(ref['path']).parent
    require(Path(ref['path']).name == calibration.CalibrationQualification.plan_name, 'owning-plan-path')
    record = lifecycle.strict(lifecycle.read_private(state_root / calibration.CalibrationQualification.state_name))
    require(record['schema'] == calibration.SCHEMA and record['planSha256'] == ref['sha256']
        and record['runId'] == plan['runId'] and record.get('measurementStarted')
        and record['terminal'] is None and not (state_root / 'stop-request.json').exists(), 'original-owner-required')
    require(config['context'] == plan['target']['context'] and config['namespace'] == plan['target']['namespace']
        and config['runId'] == 'run-' + plan['runId'][:24] and config['kubectl'] == plan['tools']['kubectl']
        and config['kubeconfig'] == str(state_root / 'kubeconfig')
        and config['kubeconfigSha256'] == record['kubeconfigSha256'], 'configuration-target')
    now = lifecycle.clock(plan)
    calibration.current_authority(plan, ref['sha256'], 'monitor', now)
    calibration.reservations(plan, now)
    require(now < record['executionDeadlineUtcMs'] and record['started']['bootId'] == capacity._boot()
        and 0 <= capacity._boottime() - record['started']['boottimeNs'] < 50400000000000, 'original-budget')


def execute(config, root):
    require(set(config) == {'kubectl', 'kubeconfig', 'kubeconfigSha256', 'context', 'namespace', 'runId', 'lane', 'pods', 'segments', 'plan'}, 'configuration')
    require(re.fullmatch('run-[0-9a-f]{24}', config['runId']) and config['lane'] in {'backlog', 'writers'}, 'identity')
    require(config['segments'] == (1200 if config['lane'] == 'backlog' else 1801), 'workload-shrunk')
    require(len(config['pods']) == (1 if config['lane'] == 'backlog' else 2) and len(set(config['pods'])) == len(config['pods'])
            and all(re.fullmatch('[a-z0-9][a-z0-9-]{0,252}', p) for p in config['pods']), 'writer-set')
    barrier = threading.Barrier(len(config['pods']))
    def run(index):
        path = root / ('writer-' + str(index)); path.mkdir(mode=0o700)
        writer = Writer(path)
        try:
            return writer.execute(config, index, barrier)
        finally:
            writer.close()
    with ThreadPoolExecutor(max_workers=len(config['pods'])) as pool:
        writers = list(pool.map(run, range(len(config['pods']))))
    overlap = min(w['finishedUtcMs'] for w in writers) - max(w['startedUtcMs'] for w in writers)
    require(config['lane'] != 'writers' or overlap >= 1800000, 'common-overlap')
    return {'lane': config['lane'], 'acceptedRecords': sum(w['acceptedRecords'] for w in writers),
        'commonOverlapMilliseconds': overlap, 'writers': writers, 'retries': 0}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--configuration', type=Path, required=True)
    parser.add_argument('--evidence', type=Path, required=True)
    args = parser.parse_args()
    # The owning supervisor authenticates plan, target and current authority.
    # This child is never an independent authorization route.
    config = lifecycle.strict(lifecycle.read_private(args.configuration))
    require(os.environ.get('HEXALITH_CALIBRATION_CHILD') == capacity.digest(config), 'supervisor-required')
    lifecycle.private(args.evidence, directory=True)
    authorize(config)
    require(args.configuration == Path(config['plan']['path']).parent / (config['lane'] + '-configuration.json')
        and args.evidence == args.configuration.parent / config['lane'], 'owning-command-path')
    print(json.dumps(execute(config, args.evidence), sort_keys=True))


if __name__ == '__main__':
    main()
