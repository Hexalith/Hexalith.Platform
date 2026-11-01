"""Bounded socket-probe program with direct ownership of its listener child."""
import ipaddress
import re


def program(peers):
    if type(peers) is not list or not 1<=len(peers)<=16:
        raise ValueError('isolation peer scope refused')
    for peer in peers:
        if (type(peer) is not dict or set(peer)!={'name','address','port'}
                or type(peer['name']) is not str or not re.fullmatch('[a-z0-9-]+',peer['name'])
                or type(peer['port']) is not int or not 1<=peer['port']<=65535
                or type(peer['address']) is not str):
            raise ValueError('isolation peer identity refused')
        try:address=ipaddress.ip_address(peer['address'])
        except ValueError:raise ValueError('isolation literal peer refused') from None
        if address.version!=4 or str(address)!=peer['address']:
            raise ValueError('isolation literal peer refused')
    # The shell owns OpenSSL itself. Killing an intermediate timeout process
    # can orphan OpenSSL into the database's PID1 and trigger crash recovery.
    text="set -eu\nif /usr/bin/timeout 1 /bin/bash -c '</dev/tcp/127.0.0.1/15443' >/dev/null 2>&1; then exit 63; fi\n"
    text+="/usr/bin/openssl s_server -nocert -accept 127.0.0.1:15443 -quiet -ign_eof </dev/null >/dev/null 2>&1 & listener=$!\n"
    text+="cleanup() { kill \"$listener\" 2>/dev/null || true; wait \"$listener\" 2>/dev/null || true; }\ntrap cleanup EXIT\ntrap 'exit 143' INT TERM\n"
    text+="ready=0; for i in 1 2 3 4 5 6 7 8 9 10; do if /usr/bin/timeout 1 /bin/bash -c '</dev/tcp/127.0.0.1/15443' >/dev/null 2>&1; then ready=1; break; fi; sleep 0.1; done\n"
    text+="[ \"$ready\" = 1 ]\nkill -0 \"$listener\"\nprintf 'control-loopback|open\\n'\n"
    for peer in peers:
        text+="if /usr/bin/timeout 2 /bin/bash -c '</dev/tcp/"+peer['address']+'/'+str(peer['port'])+"' >/dev/null 2>&1; then printf '"+peer['name']+"|open\\n'; else printf '"+peer['name']+"|blocked\\n'; fi\n"
    return text


def argv(peers):
    # Sixteen peers and the finite readiness loop fit inside this remote bound.
    # The caller also uses a separate outer command/transport deadline.
    return ['/usr/bin/timeout','--signal=TERM','--kill-after=5','50','/bin/bash','-ec',program(peers)]
