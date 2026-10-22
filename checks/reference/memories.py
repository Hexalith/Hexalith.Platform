"""Syntactic search and file ingestion use only run-scoped synthetic content."""
import base64
import urllib.parse


def search(context, marker):
    invocation = context.invocation
    status, response = context.request('GET', '/api/v1/search?' + urllib.parse.urlencode({
        'tenantId': invocation['syntheticTenant'], 'caseId': invocation['fixtureCaseId'],
        'query': marker, 'axis': 'syntactic', 'maxResults': 10}))
    if not (status == 200 and isinstance(response, dict) and isinstance(response.get('results'), list)):
        raise AssertionError()
    if not (response.get('degraded', False) is False and not response.get('unavailableAxes')):
        raise AssertionError()
    return response


def valid_memory_id(value):
    return isinstance(value, str) and bool(value) and value == value.strip() and all(ord(char) >= 32 and ord(char) != 127 for char in value)


def fixture_result(item, case_id):
    return isinstance(item, dict) and valid_memory_id(item.get('memoryUnitId')) and item.get('caseId') == case_id


def check(context, check_id):
    invocation = context.invocation
    if check_id == 'memories.search':
        response = search(context, 'synthetic')
        if not (response.get('hasIndexedMemoryUnits') is True and response.get('totalCount', 0) > 0
                and any(fixture_result(item, invocation['fixtureCaseId']) for item in response['results'])):
            raise AssertionError()
    elif check_id == 'memories.ingest-search':
        marker = invocation['invocationId'].replace('-', '')
        status, response = context.request('POST', '/api/v1/ingest', {
            'TenantId': invocation['syntheticTenant'], 'CaseId': invocation['fixtureCaseId'],
            'SourceUri': 'synthetic:' + invocation['invocationId'], 'SourceType': 'file',
            'ContentType': 'text/plain', 'ContentBytes': base64.b64encode(('synthetic ' + marker).encode()).decode(),
            'IngestedBy': 'synthetic-check', 'IdempotencyToken': invocation['invocationId']})
        if not (status == 202 and isinstance(response.get('instanceId'), str)):
            raise AssertionError()
        instance_id = response['instanceId']

        def completed():
            status, response = context.request('GET', '/api/v1/ingest/' + urllib.parse.quote(instance_id, safe=''))
            if status == 404:
                return False
            if not (status == 200 and response['tenantId'] == invocation['syntheticTenant'] and response['caseId'] == invocation['fixtureCaseId']):
                raise AssertionError()
            if not (response['runtimeStatus'] not in ('Failed', 'Terminated', 'Canceled')):
                raise AssertionError()
            if response['runtimeStatus'] != 'Completed':
                return False
            if response.get('memoryUnitStatus') != 'indexed' or response.get('failureSummary') is not None or not valid_memory_id(response.get('memoryUnitId')):
                raise AssertionError()
            return response['memoryUnitId']
        memory_id = context.poll(completed)
        context.poll(lambda: any(fixture_result(item, invocation['fixtureCaseId']) and item['memoryUnitId'] == memory_id
                                 for item in search(context, marker)['results']))
    else:
        raise ValueError('unsupported check')
