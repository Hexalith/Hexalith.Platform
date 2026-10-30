"""Inspect the exact running AppHost and make an explicitly signed, sanitized metadata request."""
from pathlib import Path
import base64
import hashlib
import hmac
import json
import re
import subprocess
import time
import urllib.error
import urllib.request

root = Path(__file__).resolve().parents[4]
evidence = max((Path(__file__).resolve().parent / "verification").glob("live-*/"), key=lambda path: path.stat().st_mtime)
arguments = ["--apphost", str(root / "apphost.cs"), "--non-interactive", "--format", "Json"]
described = subprocess.run(["aspire", "describe", "--include-hidden", *arguments], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=30)
if described.returncode:
    (evidence / "readback-unavailable.txt").write_text(described.stdout)
    raise SystemExit(described.returncode)
model = json.loads(described.stdout)
secrets = set()
for resource in model["resources"]:
    for key, value in resource.get("environment", {}).items():
        if isinstance(value, str) and len(value) >= 8 and any(word in key.lower() for word in ["key", "token", "secret", "password"]):
            secrets.add(value)
gateway = next(resource for resource in model["resources"] if resource["displayName"] == "eventstore")
environment = gateway["environment"]


def encode(value):
    return base64.urlsafe_b64encode(json.dumps(value, separators=(",", ":")).encode()).decode().rstrip("=")


message = encode({"alg": "HS256", "typ": "JWT"}) + "." + encode({"iss": environment["Authentication__JwtBearer__Issuer"],
    "aud": environment["Authentication__JwtBearer__Audience"], "sub": "catalog-live-probe", "eventstore:surface": "agent", "exp": int(time.time()) + 120})
token = message + "." + base64.urlsafe_b64encode(hmac.new(environment["Authentication__JwtBearer__SigningKey"].encode(), message.encode(), hashlib.sha256).digest()).decode().rstrip("=")
secrets.add(token)
endpoint = next((url["url"] for url in gateway["urls"] if url["name"] == "http"), None)
url = endpoint + "/api/v1/routing/catalog" if endpoint else None
http = {"method": "GET", "url": url, "authentication": "signed JWT; eventstore:surface=agent; bearer redacted"}
try:
    if url is None:
        raise OSError("The gateway has no allocated serving endpoint in its current state: " + gateway["state"])
    with urllib.request.urlopen(urllib.request.Request(url, headers={"Authorization": "Bearer " + token}), timeout=5) as response:
        http.update({"status": response.status, "body": json.loads(response.read())})
except urllib.error.HTTPError as failure:
    http.update({"status": failure.code, "body": failure.read().decode()})
except OSError as failure:
    http["connectionFailure"] = str(failure)
catalog = Path(environment["EventStore__RouteCatalog__DevelopmentBootstrap__CatalogPath"])
directory = catalog.parent
http["catalogPath"] = str(catalog)
if catalog.exists():
    canonical = catalog.read_bytes()
    parsed = json.loads(canonical)
    root_digest = hashlib.sha256(canonical).hexdigest()
    http.update({"expectedRootDigest": root_digest, "expectedSchemaVersion": parsed["schemaVersion"], "expectedGeneration": parsed["generation"]})
    (evidence / "catalog.json").write_bytes(canonical)
    for path in [directory / "receipt.json", *directory.glob("*.committed.json"), *directory.glob("*.ready.json"), directory / "composition-failure.txt"]:
        if path.exists():
            (evidence / path.name).write_bytes(path.read_bytes())
    http["committedHosts"] = [path.name.removesuffix(".committed.json") for path in directory.glob("*.committed.json")]
    if http.get("status") == 200:
        body = http["body"]
        assert (body["rootDigest"], body["schemaVersion"], body["generation"]) == (root_digest, parsed["schemaVersion"], parsed["generation"])
        expected = {operation["routeId"] for operation in parsed["operations"] if operation["ingress"] == "public"}
        assert {operation["routeId"] for operation in body["operations"]} == expected
        http["metadataMatchesCanonicalCatalog"] = True
for resource in model["resources"]:
    resource["environment"] = {key: "[redacted]" for key in resource.get("environment", {})}


def save(name, value):
    safe = value if isinstance(value, str) else json.dumps(value, indent=2)
    safe = re.sub(r'([?&]t=)[^\s"&]+', r'\1[redacted]', safe)
    for secret in sorted(secrets, key=len, reverse=True):
        safe = safe.replace(secret, "[redacted]")
    (evidence / name).write_text(safe + "\n")


save("live-metadata-http.json", http)
save("live-described.json", model)
logs = subprocess.run(["aspire", "logs", "--include-hidden", "--tail", "1500", *arguments], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=30)
save("live-resource-logs.json", logs.stdout)
print(json.dumps({"httpStatus": http.get("status"), "committedHosts": http.get("committedHosts"), "metadataMatches": http.get("metadataMatchesCanonicalCatalog"),
                  "hosts": [{"name": resource["displayName"], "state": resource.get("state"), "health": resource.get("healthStatus")} for resource in model["resources"] if resource["resourceType"] == "Project"]}), flush=True)
