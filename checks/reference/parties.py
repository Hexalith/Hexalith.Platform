"""Parties positive persisted projection checks through the source-defined Gateway API."""
import secrets


def query(context, party_id=None, pending=False):
    invocation = context.invocation
    payload = {'tenant': invocation['syntheticTenant'], 'domain': 'party',
               'aggregateId': party_id or 'parties', 'queryType': 'PartyDetail' if party_id else 'PartyIndex',
               'projectionType': 'party-detail' if party_id else 'party-index',
               'projectionActorType': 'PartyDetailProjectionQueryActor' if party_id else 'PartyIndexProjectionQueryActor',
               'payload': {'PartyId': party_id} if party_id else {}, 'paging': {'pageSize': 1}}
    status, response = context.request('POST', '/api/v1/queries', payload)
    if pending and status == 404:
        return None
    if not (status == 200 and isinstance(response, dict) and response.get('success') is True and 'payload' in response):
        raise AssertionError()
    return response['payload']


def lifecycle(context):
    invocation = context.invocation
    party_id = invocation['invocationId'][:110] + '-party'
    # Source validator admits nonblank ULID-safe message identifiers; random bytes use its ULID alphabet.
    message_id = ''.join(secrets.choice('0123456789ABCDEFGHJKMNPQRSTVWXYZ') for _ in range(26))
    message_id = '0' + message_id[1:]
    status, _ = context.request('POST', '/api/v1/commands', {
        'messageId': message_id, 'tenant': invocation['syntheticTenant'], 'domain': 'party',
        'aggregateId': party_id, 'commandType': 'CreateParty',
        'payload': {'PartyId': party_id, 'Type': 'Person',
                    'PersonDetails': {'FirstName': 'Synthetic', 'LastName': invocation['invocationId']}}})
    if not (status == 202):
        raise AssertionError()

    def committed():
        status, response = context.request('GET', '/api/v1/commands/status/' + message_id)
        if not (status in (200, 404)):
            raise AssertionError()
        if status == 404:
            return False
        if not (response['status'] not in ('Rejected', 'Failed', 'TimedOut')):
            raise AssertionError()
        if response['status'] != 'Completed':
            return False
        if not (response['messageId'] == message_id and response['aggregateId'] == party_id):
            raise AssertionError()
        if not (response['tenantId'] == invocation['syntheticTenant'] and response['eventCount'] > 0):
            raise AssertionError()
        return True
    context.poll(committed)

    def projected():
        result = query(context, party_id, pending=True)
        if result is None:
            return False
        if not (isinstance(result, dict) and result.get('id') == party_id
                and result.get('type') == 'Person' and result.get('isActive') is True
                and result.get('displayName') == 'Synthetic ' + invocation['invocationId']
                and isinstance(result.get('personDetails'), dict)
                and result['personDetails'].get('firstName') == 'Synthetic'
                and result['personDetails'].get('lastName') == invocation['invocationId']):
            raise AssertionError()
        return True
    context.poll(projected)


def check(context, check_id):
    if check_id == 'parties.management':
        lifecycle(context)
    elif check_id == 'parties.read':
        result = query(context, context.invocation['fixturePartyId'])
        if not (isinstance(result, dict) and result.get('id', result.get('Id')) == context.invocation['fixturePartyId']):
            raise AssertionError()
    else:
        raise ValueError('unsupported check')
