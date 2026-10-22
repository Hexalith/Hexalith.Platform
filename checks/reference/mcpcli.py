"""The retained CLI uses Gateway queries; McpCli has no hosted HTTP MCP endpoint."""


def check(context, check_id):
    if check_id not in ('mcpcli.query', 'mcpcli.read'):
        raise ValueError('unsupported check')
    response = context.run_candidate('parties.get-party', {'PartyId': context.invocation['fixturePartyId']})
    if not (isinstance(response, dict) and isinstance(response.get('document'), dict)):
        raise AssertionError()
    document = response['document']
    if not (document.get('id', document.get('Id')) == context.invocation['fixturePartyId']):
        raise AssertionError()
