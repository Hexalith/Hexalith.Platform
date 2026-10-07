"""External probes: every closed administrative path/alias must be refused or not routed, for GET and POST, on every public host.

Usage: external_probe.py OUT.json
"""
import collections, datetime, http.client, json, socket, ssl, sys, urllib.request

PUB = "82.67.127.189"
# Public prefixes deliberately routed to Keycloak; a backend refusal of an alias under them is not admin exposure.
APPROVED_PREFIXES = {"auth.tache.ai": ("/realms/tache", "/resources")}
TRAEFIK_NOT_ROUTED = b"404 page not found\n"
PATHS = {
 "auth.tache.ai": ["/", "/admin", "/admin/", "/admin/master/console/", "/admin/realms", "/admin/realms/master", "/admin/serverinfo",
   "/%61dmin/", "/admin%2F", "/ADMIN/", "//admin/", "/./admin/", "/admin/..;/admin/",
   "/realms/master", "/realms/master/", "/realms/master/protocol/openid-connect/token", "/realms/master/protocol/openid-connect/token/",
   "/realms/master/protocol/openid-connect/auth", "/realms/%6daster/protocol/openid-connect/token", "/realms/Master/protocol/openid-connect/token",
   "/realms/tache/../master/protocol/openid-connect/token", "/realms/tache/..%2Fmaster/protocol/openid-connect/token",
   "/realms/tache/%2e%2e/master/protocol/openid-connect/token", "/realms/tache/..%2F..%2Fadmin/master/console/",
   "/realms/tache/%2e%2e/%2e%2e/admin/realms",
   "/resources/..%2F..%2Fadmin/master/console/", "/resources/%2e%2e/admin/realms", "/resources/../admin/",
   "/resources/..%2Frealms/master/protocol/openid-connect/token",
   "/js/", "/health", "/health/ready", "/metrics", "/robots.txt", "/welcome"],
 "kube.hexalith.com": ["/", "/login", "/kapis/", "/api/v1/namespaces", "/oauth/token"],
}
# Host-header variants that could hit a default or catch-all router.
HOST_VARIANTS = ["auth.tache.ai.", "AUTH.TACHE.AI", "kube.hexalith.com.", "KUBE.HEXALITH.COM", PUB, "unknown.invalid"]
VARIANT_PATHS = ["/admin/", "/realms/master", "/login"]


def classify(r, results_by_key):
    """Return the closure verdict for one probe result; only 'not-routed', 'redirect-to-closed-https' and
    'approved-prefix-backend-refusal' count as closed."""
    status = r["status"]
    if isinstance(status, str):
        return "inconclusive"
    if status == 404 and r["traefikNotRouted"]:
        return "not-routed"
    if r["port"] == 80 and status in (301, 302, 308) and r["location"] == f"https://{r['host']}{r['path']}":
        https = results_by_key.get((r["host"], 443, r["method"], r["path"]))
        return "redirect-to-closed-https" if https and https["verdict"] in ("not-routed", "approved-prefix-backend-refusal") else "redirect-to-open"
    prefixes = APPROVED_PREFIXES.get(r["host"], ())
    if status in (400, 404, 405) and any(r["path"] == p or r["path"].startswith(p + "/") for p in prefixes):
        return "approved-prefix-backend-refusal"
    return "open"


CLOSED = {"not-routed", "redirect-to-closed-https", "approved-prefix-backend-refusal"}
CTX = ssl.create_default_context(); CTX.check_hostname = False; CTX.verify_mode = ssl.CERT_NONE


def req(host, method, path, port=443, sni=None):
    """Send one request to the public IP with the given Host header (and SNI on 443); return a result record."""
    rec = {"host": host, "port": port, "method": method, "path": path, "status": None, "location": None, "traefikNotRouted": False}
    try:
        raw = socket.create_connection((PUB, port), timeout=15)
        c = http.client.HTTPConnection(host, port, timeout=15)
        c.sock = CTX.wrap_socket(raw, server_hostname=sni or host.rstrip(".")) if port == 443 else raw
        c.putrequest(method, path, skip_host=True, skip_accept_encoding=True)
        c.putheader("Host", host); c.putheader("User-Agent", "story-4-2-closure-probe")
        body = b"grant_type=password&client_id=admin-cli&username=x&password=y" if method == "POST" else b""
        if method == "POST":
            c.putheader("Content-Type", "application/x-www-form-urlencoded")
        c.putheader("Content-Length", str(len(body))); c.endheaders(body)
        resp = c.getresponse(); data = resp.read(); c.close()
        rec.update(status=resp.status, location=resp.getheader("Location"), traefikNotRouted=data == TRAEFIK_NOT_ROUTED)
    except Exception as e:
        rec["status"] = type(e).__name__
    return rec


def public_dns(name):
    """Public A/AAAA answers via DNS-over-HTTPS (the LAN resolver returns private addresses)."""
    out = {}
    for t, code in (("A", 1), ("AAAA", 28)):
        q = urllib.request.Request(f"https://cloudflare-dns.com/dns-query?name={name}&type={t}", headers={"accept": "application/dns-json"})
        with urllib.request.urlopen(q, timeout=15) as r:
            out[t] = sorted(a["data"] for a in json.load(r).get("Answer", []) if a["type"] == code)
    return out


def main(out_path):
    dns = {h: public_dns(h) for h in PATHS}
    dns_ok = all(d["A"] == [PUB] and not d["AAAA"] for d in dns.values())
    control = req("auth.tache.ai", "GET", "/realms/tache/.well-known/openid-configuration")
    control_ok = control["status"] == 200
    results = [req(h, m, p, port) for h, ps in PATHS.items() for p in ps for m in ("GET", "POST") for port in (443, 80)]
    results += [req(h, m, p, port, sni="auth.tache.ai") for h in HOST_VARIANTS for p in VARIANT_PATHS for m in ("GET", "POST") for port in (443, 80)]
    by_key = {}
    for r in sorted(results, key=lambda r: r["port"] != 443):  # HTTPS verdicts first, redirects consult them
        r["verdict"] = classify(r, by_key)
        by_key[(r["host"], r["port"], r["method"], r["path"])] = r
    bad = [r for r in results if r["verdict"] not in CLOSED]
    out = {"capturedAt": datetime.datetime.now(datetime.timezone.utc).isoformat(), "vantage": "LAN client via public IP " + PUB + " (hairpin)",
           "publicDns": dns, "publicDnsMatches": dns_ok, "positiveControl": {"path": control["path"], "status": control["status"], "pass": control_ok},
           "probeCount": len(results), "verdictCounts": dict(collections.Counter(r["verdict"] for r in results)),
           "notClosed": bad, "pass": control_ok and dns_ok and not bad, "results": results}
    json.dump(out, open(out_path, "w"), indent=1)
    print("probes", len(results), "notClosed", len(bad), "control", control["status"], "dns", dns_ok, "pass", out["pass"])
    for r in bad:
        print("  OPEN?", r)
    print(out["verdictCounts"])
    return out


if __name__ == "__main__":
    main(sys.argv[1])
