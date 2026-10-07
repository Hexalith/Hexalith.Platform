"""Public tache OIDC regression smoke via the public IP (no credentials, no bodies retained).

Usage: oidc_smoke.py OUT.json
"""
import http.cookiejar, json, re, socket, sys, urllib.parse, urllib.request
from datetime import datetime, timezone

PUBLIC_IP = "82.67.127.189"
PUBLIC_HOSTS = ("auth.tache.ai", "registry.hexalith.com", "repository.tache.ai")

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None

def opener(cj):
    return urllib.request.build_opener(NoRedirect, urllib.request.HTTPCookieProcessor(cj))

def get(op, url):
    try:
        r = op.open(urllib.request.Request(url, headers={"User-Agent": "story-4-2-oidc-smoke"}), timeout=15)
        return r.status, r.headers, r.read()
    except urllib.error.HTTPError as e:
        return e.code, e.headers, e.read()

def strip(u):
    return re.sub(r"(state|nonce|code_challenge|session_code|execution|tab_id|client_data)=[^&]*", r"\1=X", u)

def main(out_path):
    real_getaddrinfo = socket.getaddrinfo
    def via_public_ip(host, *a, **k):
        return real_getaddrinfo(PUBLIC_IP if host in PUBLIC_HOSTS else host, *a, **k)
    socket.getaddrinfo = via_public_ip
    checks = []
    def rec(name, url, status, ok, **extra):
        checks.append({"name": name, "url": strip(url), "status": status, "pass": ok, **extra})

    op = opener(http.cookiejar.CookieJar())
    base = "https://auth.tache.ai/realms/tache"
    s, h, b = get(op, base + "/.well-known/openid-configuration")
    disc = json.loads(b) if s == 200 else {}
    rec("discovery", base + "/.well-known/openid-configuration", s, s == 200 and disc.get("issuer") == base, issuer=disc.get("issuer"))
    s, h, b = get(op, disc.get("jwks_uri", base + "/protocol/openid-connect/certs"))
    rec("jwks", disc.get("jwks_uri", ""), s, s == 200 and len(json.loads(b).get("keys", [])) > 0)
    s, h, b = get(op, disc.get("token_endpoint", base + "/protocol/openid-connect/token"))
    rec("token-endpoint-routed", disc.get("token_endpoint", ""), s, s in (400, 401, 405))

    for app, start in (("zot", "https://registry.hexalith.com/zot/auth/login?provider=oidc"),
                       ("forgejo", "https://repository.tache.ai/user/oauth2/Keycloak")):
        cj = http.cookiejar.CookieJar(); aop = opener(cj)
        s, h, b = get(aop, start)
        loc = h.get("Location", "")
        rec(app + "-login-start", start, s, s in (302, 303, 307) and loc.startswith(base + "/protocol/openid-connect/auth"))
        if not loc.startswith("https://auth.tache.ai/"):
            continue
        s, h, b = get(aop, loc)
        hops = 0
        while s in (302, 303) and h.get("Location", "").startswith(base + "/broker/") and hops < 3:
            loc = h["Location"]; hops += 1
            s, h, b = get(aop, loc)
        if hops:
            nxt = h.get("Location", "")
            q = urllib.parse.parse_qs(urllib.parse.urlparse(nxt).query)
            rec(app + "-broker-handoff", loc, s, s in (302, 303) and nxt.startswith("https://login.microsoftonline.com/")
                and q.get("redirect_uri", [""])[0].startswith(base + "/broker/"), brokerHops=hops)
            cb = q.get("redirect_uri", [""])[0]
            if cb:
                st, _, _ = get(aop, cb)
                rec(app + "-broker-callback-routed", cb, st, st in (200, 400, 401, 403) and st != 404)
            continue
        html = b.decode("utf-8", "replace")
        form = re.search(r'<form[^>]+id="kc-form-login"[^>]+action="([^"]+)"', html)
        rec(app + "-login-page", loc, s, s == 200 and bool(form))
        assets = sorted(set(re.findall(r'(?:href|src)="(/[^"]+\.(?:css|js|svg|png|ico|woff2?)[^"]*)"', html)))
        bad = []
        for a in assets:
            st, _, _ = get(aop, "https://auth.tache.ai" + a.replace("&amp;", "&"))
            if st != 200:
                bad.append({"path": a, "status": st})
        rec(app + "-login-assets", loc, sorted({f["status"] for f in bad}) or [200], not bad and len(assets) > 0, assetCount=len(assets), failed=bad)
        if form:
            action = form.group(1).replace("&amp;", "&")
            data = urllib.parse.urlencode({"username": "story-4-2-nonexistent-probe", "password": "invalid", "credentialId": ""}).encode()
            try:
                r = aop.open(urllib.request.Request(action, data=data, headers={"User-Agent": "story-4-2-oidc-smoke"}), timeout=15); st = r.status; body = r.read().decode("utf-8", "replace")
            except urllib.error.HTTPError as e:
                st = e.code; body = e.read().decode("utf-8", "replace")
            rec(app + "-login-post-invalid-user", action, st, st in (200, 401) and ("Invalid username or password" in body or "kc-form-login" in body))

    out = {"capturedAt": datetime.now(timezone.utc).isoformat(), "vantage": "LAN client via public IP hairpin " + PUBLIC_IP,
           "credentialsUsed": False, "responseBodiesRetained": False, "checks": checks, "pass": all(c["pass"] for c in checks)}
    json.dump(out, open(out_path, "w"), indent=2)
    for c in checks:
        print(("PASS" if c["pass"] else "FAIL"), c["name"], c["status"], c.get("failed", ""))
    print("OVERALL", out["pass"])
    return out


if __name__ == "__main__":
    main(sys.argv[1])
