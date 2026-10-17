"""Per-workload native component identities and narrow Redis key/channel scopes."""
from install_isolated_registry import NS

# Redis 7.4 AOF MULTI/EXEC replay checks the disabled default user's ACL.
# Authentication stays disabled; only the actual isolated native namespaces are
# permitted for internal replay. Application users retain separate selectors.
INTERNAL_REPLAY_ACL = ('user default off resetkeys resetchannels ~eventstore||* ~tenants||* '
                       '~tenants.events ~deadletter.tenants.events ~\\*.\\*.projection-changed '
                       '~system.tenants.projection-changed ~system.global-administrators.projection-changed '
                       '~ratelimit:tenants ~ratelimit:*:permit-limit +@all')


def native_acl(name, password):
    if name not in ['eventstore', 'tenants']:
        return 'user ' + name + ' off resetkeys resetchannels -@all'
    state = '+get +set +del +exists +hget +hset +hincrby +hmget +hmset +hgetall +hdel +mget +mset +setnx +incr +incrby +decr +ttl +pttl +expire +pexpire +persist +eval +evalsha +script|load +script|exists +multi +exec +discard +watch +unwatch'
    streams = '+xadd +xread +xreadgroup +xgroup +xack +xpending +xclaim +xautoclaim +xinfo +xlen +xtrim +xdel +exists +del +eval +evalsha +script|load +script|exists'
    return ('user ' + name + ' on >' + password + ' resetkeys resetchannels -@all +ping +hello +select +info +client|setname +client|setinfo '
            '(~' + name + '||* ' + state + ') '
            '(~tenants.events ~deadletter.tenants.events ~\\*.\\*.projection-changed '
            '~system.tenants.projection-changed ~system.global-administrators.projection-changed ' + streams + ') '
            '(~ratelimit:tenants ~ratelimit:*:permit-limit +get +mget +hgetall)')


def components(app):
    result = []
    for base, kind, extra in [('statestore', 'state.redis', [{'name': 'actorStateStore', 'value': 'true'}]),
                              ('pubsub', 'pubsub.redis', []), ('configstore', 'configuration.redis', [])]:
        name = 'eventstore-' + base if app == 'eventstore' else base
        result.append({'apiVersion': 'dapr.io/v1alpha1', 'kind': 'Component', 'metadata': {'name': name, 'namespace': NS}, 'scopes': [app],
                       'spec': {'type': kind, 'version': 'v1', 'metadata': [{'name': 'redisHost', 'value': 'registry-state:6379'},
                           {'name': 'redisUsername', 'value': app}, {'name': 'redisPassword', 'secretKeyRef': {'name': 'ext-secrets-1-registry-state-credentials', 'key': app}}] + extra}})
    return result
