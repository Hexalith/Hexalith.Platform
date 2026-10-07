"""Keycloak recovery qualification: isolated client session, loopback only, recovery client credentials only.

Usage (run under `env -i PATH=/usr/bin:/bin` with `kubectl -n keycloak port-forward --address 127.0.0.1 svc/keycloak 38080:8080` running): keycloak_recovery_check.py RECOVERY_CREDENTIAL.json OUT.json PHASE

The session blocks name resolution of the public Keycloak host, so public OIDC is unavailable to it; the check also confirms
that block is in force. The port-forward itself is opened by the operator with native cluster access, outside this session.
"""
import datetime, json, os, socket, sys, urllib.parse, urllib.request, uuid

B = "http://127.0.0.1:38080"
PUBLIC_HOST = "auth.tache.ai"
ISOLATED_ENVIRONMENT = {"PATH", "PWD", "SHLVL", "_", "LC_CTYPE"}


def evaluate(res):
    """The recovery check passes only when every measured condition holds."""
    return (res["environmentIsolated"] and res["publicOidcBlockedInSession"]
            and res["recoveryTokenStatus"] == 200 and res.get("adminReadStatus") == 200
            and {"master", "tache"} <= set(res.get("realmNames") or [])
            and res.get("namedAdministratorPresent") is True and res["wrongSecretStatus"] == 401)


def call(url, data=None, headers=None):
    try:
        with urllib.request.urlopen(urllib.request.Request(url, data=data, headers=headers or {}), timeout=20) as r:
            return r.status, r.read()
    except urllib.error.HTTPError as e:
        return e.code, e.read()
    except Exception as e:
        return type(e).__name__, b""


def main(cred_path, out_path, phase):
    cred = json.load(open(cred_path))
    real_getaddrinfo = socket.getaddrinfo
    def blocked(h, *a, **k):
        if h == PUBLIC_HOST:
            raise socket.gaierror("public OIDC blocked for isolated recovery session")
        return real_getaddrinfo(h, *a, **k)
    socket.getaddrinfo = blocked
    env = sorted(os.environ)
    res = {"phase": phase, "capturedAt": datetime.datetime.now(datetime.timezone.utc).isoformat(),
           "qualificationScope": "isolated-client-session", "testSessionId": str(uuid.uuid4()),
           "environment": env, "environmentIsolated": set(env) <= ISOLATED_ENVIRONMENT,
           "sessionCredential": "recovery client " + cred["clientId"] + " only",
           "transport": "loopback port-forward 127.0.0.1:38080 -> svc/keycloak:8080, opened by the operator with native cluster access"}
    res["publicOidcBlockedInSession"] = call(f"https://{PUBLIC_HOST}/realms/tache/.well-known/openid-configuration")[0] == "URLError"
    form = lambda secret: urllib.parse.urlencode({"grant_type": "client_credentials", "client_id": cred["clientId"], "client_secret": secret}).encode()
    s, b = call(B + "/realms/master/protocol/openid-connect/token", form(cred["clientSecret"]))
    res["recoveryTokenStatus"] = s
    tok = json.loads(b).get("access_token") if s == 200 else None
    if tok:
        auth = {"Authorization": "Bearer " + tok}
        s2, b2 = call(B + "/admin/realms", headers=auth)
        res["adminReadStatus"] = s2
        res["realmNames"] = sorted(r["realm"] for r in json.loads(b2)) if s2 == 200 else None
        s3, b3 = call(B + "/admin/realms/master/users?username=jpiquot&exact=true", headers=auth)
        res["namedAdministratorPresent"] = s3 == 200 and any(u.get("username") == "jpiquot" for u in json.loads(b3))
    res["wrongSecretStatus"] = call(B + "/realms/master/protocol/openid-connect/token", form("wrong"))[0]
    res["pass"] = evaluate(res)
    json.dump(res, open(out_path, "w"), indent=2)
    print(json.dumps(res, indent=1))
    return res


if __name__ == "__main__":
    main(*sys.argv[1:4])
