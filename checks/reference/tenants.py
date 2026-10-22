"""Denial is supplementary evidence; positive tenant/admin flows remain blocked."""


def check(context, check_id):
    if check_id != 'tenants.admin-denied':
        raise ValueError('unsupported check')
    status, _ = context.request('GET', '/api/global-administrators')
    if not (status == 403):
        raise AssertionError()
