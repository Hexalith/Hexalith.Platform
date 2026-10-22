"""Gateway submission/status and safe synthetic projection reads exercise its real API."""
from parties import lifecycle, query


def check(context, check_id):
    if check_id == 'eventstore.command-status':
        lifecycle(context)
    elif check_id == 'eventstore.query':
        result = query(context, context.invocation['fixturePartyId'])
        if not (isinstance(result, dict) and result.get('id', result.get('Id')) == context.invocation['fixturePartyId']):
            raise AssertionError()
    else:
        raise ValueError('unsupported check')
