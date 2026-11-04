"""Read actual completed EventStore run artifacts; never mutate a remote run."""
import datetime
import hashlib
import json
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path, PurePosixPath

NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
REQUIRED_NATIVE = {
    'Hexalith.EventStore.Platform.IntegrationTests.GatewayPersistenceTests.GatewayReadinessAndOwnedPersistenceEndStateAsync',
    'Hexalith.EventStore.Platform.IntegrationTests.GatewayPersistenceTests.GatewayRefusesAnonymousCommandStatusAsync',
}


def digest(body):
    return hashlib.sha256(body).hexdigest()


def trx(body, native=False):
    root = ET.fromstring(body)
    counters = root.find('.//t:ResultSummary/t:Counters', NS).attrib
    cases = [dict(c.attrib) for c in root.findall('.//t:UnitTestResult', NS)]
    assert int(counters['total']) == len(cases), counters
    assert int(counters['passed']) == sum(c['outcome'] == 'Passed' for c in cases)
    assert int(counters['failed']) == 0
    assert not any(int(counters.get(k, '0')) for k in ['error', 'timeout', 'aborted', 'notRunnable', 'disconnected', 'pending', 'inProgress'])
    assert all(c['outcome'] in ['Passed', 'NotExecuted'] for c in cases)
    if native:
        assert all(c['outcome'] == 'Passed' for c in cases)
        assert REQUIRED_NATIVE <= {c['testName'] for c in cases}
        assert int(counters['executed']) == len(cases)
    return {'sha256': digest(body), 'counters': counters, 'actualCases': len(cases), 'cases': cases}


def main():
    receipt_path, cache_path, output_path = map(Path, sys.argv[1:])
    receipt = json.loads(receipt_path.read_text())
    run = receipt['run']
    assert run['status'] == 'completed' and run['conclusion'] == 'success'
    assert run['run_attempt'] == 1
    artifacts = receipt['artifacts']['artifacts']
    selected = {a['name']: a for a in artifacts}
    assert len(selected) == len(artifacts)
    assert {'blocking-test-results', 'module-integration-diagnostics', 'contracts-test-results', 'tenants-source-mode-test-results'} <= set(selected)
    cache_path.mkdir(parents=True, exist_ok=True)
    all_reports = []
    bodies_by_artifact = {}
    for name in ['blocking-test-results', 'module-integration-diagnostics', 'contracts-test-results', 'tenants-source-mode-test-results']:
        a = selected[name]
        assert not a['expired'] and a['workflow_run']['id'] == run['id']
        assert a['workflow_run']['head_sha'] == run['head_sha']
        p = cache_path / (name + '.zip')
        if not p.exists():
            body = subprocess.check_output(['gh', 'api', f"repos/Hexalith/Hexalith.EventStore/actions/artifacts/{a['id']}/zip"])
            p.write_bytes(body)
        assert 'sha256:' + digest(p.read_bytes()) == a['digest']
        bodies = {}
        with zipfile.ZipFile(p) as z:
            assert len(z.namelist()) == len(set(z.namelist()))
            assert sum(i.file_size for i in z.infolist()) < 256 * 1024 * 1024
            for i in z.infolist():
                n = PurePosixPath(i.filename)
                assert not n.is_absolute() and '..' not in n.parts and '\\' not in i.filename
                assert not stat.S_ISLNK(i.external_attr >> 16)
                if not i.is_dir():
                    bodies[i.filename] = z.read(i)
        reports = [{'file': n, **trx(b, name == 'module-integration-diagnostics')} for n, b in bodies.items() if n.endswith('.trx')]
        assert reports, name
        all_reports.append({'name': name, 'artifactId': a['id'], 'archiveSha256': digest(p.read_bytes()), 'bytes': p.stat().st_size, 'members': [{'path': n, 'sha256': digest(b), 'bytes': len(b)} for n, b in bodies.items()], 'trx': reports})
        bodies_by_artifact[name] = bodies
    bodies = bodies_by_artifact['module-integration-diagnostics']
    result = json.loads(bodies['test-result.json'])
    invocation = json.loads(bodies['invocation.json'])
    execution = json.loads(bodies['execution.json'])
    cleanup = json.loads(bodies['cleanup.json'])
    context = result['bindings']['environment']['executionContext']
    assert context == invocation['executionContext']
    assert context['workflowRunId'] == str(run['id']) and context['workflowAttempt'] == run['run_attempt']
    assert context['repository'] == 'Hexalith/Hexalith.EventStore' and context['jobId'] == 'build-and-test'
    assert context['kind'] == 'ci' and context['runnerKind'] == 'github-hosted' and context['operatingSystem'] == 'Linux' and context['disposable']
    owner = result['bindings']['run']['ownerRunId']
    assert execution['runId'] == cleanup['runId'] == result['bindings']['run']['runId'] == owner
    assert invocation['classification'] == 'baseline-only' and invocation['profiles'] == ['platform-package'] and invocation['qualificationOwners'] == ['tenants']
    for response in [execution, cleanup]:
        assert response['status'] == 'completed' and response['outcome']['exitCode'] == 'Success'
        remaining = response['remainingResources']
        assert remaining['isEmpty'] and not remaining['unverified']
        assert all(not remaining[k] for k in ['claimIds', 'containerIds', 'discoveryFailures', 'processIds', 'resourceIds', 'workspaceIds'])
    readiness = result['bindings']['environment']['readiness']
    assert readiness['state'] == 'populated' and readiness['status'] == 'ready'
    assert len(readiness['resources']) == 10 and all(r['status'] == 'ready' for r in readiness['resources'])
    assert {r['resourceId'] for r in readiness['resources']} == {'dapr-placement-mtls', 'dapr-scheduler-mtls', 'dapr-sentry', 'eventstore', 'eventstore-dapr-cli', 'redis', 'tenants', 'tenants-api', 'tenants-api-dapr-cli', 'tenants-dapr-cli'}
    c = result['cleanup']
    assert c['state'] == 'populated' and c['disposition'] == 'complete' and not c['failures'] and not c['leftoverResourceIds']
    assert set(c['ownedResourceIds']) == {'resource:' + r['resourceId'] for r in readiness['resources']}
    assert result['terminal']['outcome'] == 'passed' and result['terminal']['cause'] == 'tests-completed'
    assert result['terminal']['recordedAt'] < c['observedAt']
    retained = []
    for d in result['diagnostics']:
        assert d['redacted'] and d['reference'] in bodies
        b = bodies[d['reference']]
        assert 'sha256:' + digest(b) == d['contentIdentity']
        retained.append(d)
    lifecycle = next(json.loads(bodies[d['reference']]) for d in retained if d['id'] == 'lifecycle')
    assert lifecycle['runId'] == owner and lifecycle['outcome'] == 'passed' and lifecycle['cause'] == 'tests-completed' and lifecycle['cleanup'] == 'complete'
    assert not lifecycle['discoveryUnverified'] and not lifecycle['discoveryFailures']
    deadlines = result['bindings']['deadlines']
    assert deadlines['startup']['effectiveSeconds'] == 600 and deadlines['startup']['override']['state'] == 'not-overridden'
    assert deadlines['execution']['effectiveSeconds'] == 120
    assert all(deadlines[k]['actualDurationSeconds'] <= deadlines[k]['effectiveSeconds'] for k in ['startup', 'execution'])
    observations = [{'file': n, 'body': json.loads(b)} for n, b in bodies.items() if n.startswith('loaded-assemblies-') and n.endswith('.json')]
    for observed in observations:
        o = observed['body']
        assert o['kind'] == 'real-native-test-loaded-assemblies' and o['ownerRunId'] == owner
        assert o['ownerAttemptId'] == result['bindings']['run']['attemptId']
        assert o['environmentId'] == result['bindings']['run']['environmentId'] and o['executionContext'] == context
        assert observed['file'] == 'loaded-assemblies-' + owner + '.json'
        assert len(o['mappingHash']) == 64 and len(o['revision']) == 40
        if receipt.get('testedSource'):
            assert o['revision'] == receipt['testedSource']
        assert 0 < len(o['assemblies']) <= 64
        assert len({a['identity'] for a in o['assemblies']}) == len(o['assemblies'])
        assert all(len(a['sha256']) == 64 and a['loadedPath'].endswith('.dll') for a in o['assemblies'])
    if receipt.get('requireNativeObservations'):
        assert len(observations) == 1
    audit = {'at': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'source': run['head_sha'], 'runId': run['id'], 'attempt': run['run_attempt'], 'event': run['event'], 'reports': all_reports, 'owner': result['bindings']['run'], 'executionContext': context, 'catalog': result['bindings']['environment']['catalog'], 'retainedDiagnosticReferences': retained, 'terminal': result['terminal'], 'cleanup': c, 'deadlines': deadlines, 'actualNativeObservations': observations, 'scope': 'Actual completed source/run only; public SDK identity, final story acceptance and other callers are separate gates.', 'storyComplete': False, 'candidateAcceptance': False}
    assert not output_path.exists()
    output_path.write_text(json.dumps(audit, indent=2) + '\n')
    print(json.dumps({'source': run['head_sha'], 'runId': run['id'], 'owner': owner, 'reports': [{'name': a['name'], 'trx': [{'file': r['file'], 'counters': r['counters']} for r in a['trx']]} for a in all_reports], 'retainedDiagnosticReferences': retained, 'terminal': result['terminal'], 'cleanup': c}, indent=2))


if __name__ == '__main__':
    main()
