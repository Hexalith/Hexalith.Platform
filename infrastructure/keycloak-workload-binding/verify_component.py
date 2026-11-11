#!/usr/bin/env python3
"""Disposable Keycloak component qualification; never creates trusted owner enrollment.

Only metadata is printed. The temporary realm, TLS CA, credentials and container are owned
by this process and removed on exit. Run after `mvn package` from this directory.
"""
import base64
import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import ssl
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request


def command(*args):
    try:
        return subprocess.check_output(args, stderr=subprocess.STDOUT, text=True).strip()
    except subprocess.CalledProcessError:
        raise RuntimeError("Component command failed; protected arguments and output withheld.") from None


def run(sdk_assembly=None, apphost_assembly=None):
    root = Path(__file__).resolve().parent
    jar = root / "target/keycloak-workload-binding-1.0.0-SNAPSHOT.jar"
    if not jar.is_file():
        raise RuntimeError("Build the provider with mvn package first.")
    name = "hexalith-workload-component-" + secrets.token_hex(6)
    with tempfile.TemporaryDirectory(prefix=name + "-") as owned:
        directory = Path(owned)
        os.chmod(directory, 0o700)
        cert, key = directory / "tls.pem", directory / "tls.key"
        command("openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "1", "-subj", "/CN=localhost",
                "-addext", "subjectAltName=DNS:localhost", "-keyout", str(key), "-out", str(cert))
        os.chmod(key, 0o600)
        now = int(time.time())
        secret, admin_secret = secrets.token_urlsafe(32), secrets.token_urlsafe(32)
        grant = dict(grantId="component-source", revision=1, clientId="folders-component", workload="folders-component",
                     audience="eventstore", operations=["eventstore:stream-read"],
                     bindings={"eventstore:bound-tenant": ["tenant-a"], "eventstore:bound-domain": ["folders"]},
                     allowUnbound=False, allowQueryAuthorityDigest=False, notBefore=now - 10, expiresAt=now + 3600, revoked=False)
        other_scope = copy.deepcopy(grant)
        other_scope.update(grantId="component-other-scope", bindings={"eventstore:bound-tenant": ["tenant-b"], "eventstore:bound-domain": ["folders"]})
        scopes = ["eventstore-audience.eventstore", "eventstore-operation.eventstore.stream-read"]
        optional_scopes = scopes + ["eventstore-audience.folders", "eventstore-audience.tenants", "eventstore-operation.eventstore.command-submit"]
        optional_scopes += ["eventstore-operation.domain-service." + operation for operation in ("process", "query", "project", "metadata", "replay-state")]
        client = dict(clientId="folders-component", enabled=True, publicClient=False, secret=secret,
                      serviceAccountsEnabled=True, standardFlowEnabled=False, directAccessGrantsEnabled=False,
                      fullScopeAllowed=False, defaultClientScopes=[], optionalClientScopes=optional_scopes,
                      attributes={"hexalith.workload-binding.workload": "folders-component",
                                  "hexalith.workload-binding.grants": json.dumps([grant, other_scope])},
                      protocolMappers=[dict(name="workload-binding", protocol="openid-connect",
                                           protocolMapper="hexalith-workload-binding-v1", config={})])
        realm = dict(realm="component", enabled=True, sslRequired="all", defaultSignatureAlgorithm="RS256",
                     attributes={"hexalith.workload-binding.enabled": "true", "hexalith.workload-binding.signer-not-after": str(now + 1800)},
                     clientScopes=[dict(name=scope, protocol="openid-connect", attributes={}, protocolMappers=[]) for scope in optional_scopes],
                     clients=[client], clientProfiles={"profiles": [dict(name="workload-binding", executors=[dict(executor="hexalith-workload-binding-v1", configuration={})])]},
                     clientPolicies={"policies": [dict(name="workload-binding", enabled=True,
                         conditions=[dict(condition="any-client", configuration={})], profiles=["workload-binding"])]})
        (directory / "realm.json").write_text(json.dumps(realm))
        os.chmod(directory / "realm.json", 0o600)
        with socket.socket() as port_socket:
            port_socket.bind(("127.0.0.1", 0))
            port = port_socket.getsockname()[1]
        base = f"https://localhost:{port}"
        context = ssl.create_default_context(cafile=str(cert))
        issuer = base + "/realms/component"
        try:
            command("docker", "run", "--detach", "--rm", "--name", name, "--user", f"{os.getuid()}:{os.getgid()}", "--publish", f"127.0.0.1:{port}:8443",
                    "--env", "KC_BOOTSTRAP_ADMIN_USERNAME=component-admin", "--env", "KC_BOOTSTRAP_ADMIN_PASSWORD=" + admin_secret,
                    "--volume", f"{jar}:/opt/keycloak/providers/workload-binding.jar:ro",
                    "--volume", f"{directory}:/component:ro", "--volume", f"{directory / 'realm.json'}:/opt/keycloak/data/import/component-realm.json:ro",
                    "quay.io/keycloak/keycloak:26.6", "start-dev", "--import-realm", "--http-enabled=false",
                    "--https-certificate-file=/component/tls.pem", "--https-certificate-key-file=/component/tls.key", "--hostname=" + base)

            def request(path, payload=None, bearer=None, method=None, trusted=True):
                headers = {}
                data = None
                if payload is not None:
                    if isinstance(payload, dict):
                        data = json.dumps(payload).encode()
                        headers["Content-Type"] = "application/json"
                    else:
                        data = urllib.parse.urlencode(payload).encode()
                        headers["Content-Type"] = "application/x-www-form-urlencoded"
                if bearer:
                    headers["Authorization"] = "Bearer " + bearer
                req = urllib.request.Request(base + path, data=data, headers=headers, method=method)
                try:
                    with urllib.request.urlopen(req, context=context if trusted else ssl.create_default_context(), timeout=10) as response:
                        return response.status, json.load(response) if response.length != 0 else None
                except urllib.error.HTTPError as error:
                    return error.code, json.loads(error.read())

            deadline = time.monotonic() + 120
            while True:
                try:
                    status, metadata = request("/realms/component/.well-known/openid-configuration")
                    if status == 200:
                        break
                except (OSError, urllib.error.URLError):
                    pass
                if time.monotonic() > deadline:
                    raise RuntimeError("Disposable Keycloak readiness deadline elapsed.")
                time.sleep(0.5)
            assert metadata["issuer"] == issuer
            try:
                request("/realms/component/.well-known/openid-configuration", trusted=False)
                raise AssertionError("Untrusted component TLS certificate was accepted.")
            except urllib.error.URLError:
                pass
            details = [dict(type="https://hexalith.com/security/workload-binding/v1", audience="eventstore", operation="eventstore:stream-read",
                            bindings={"eventstore:bound-tenant": "tenant-a", "eventstore:bound-domain": "folders"})]
            form = [("grant_type", "client_credentials"), ("client_id", "folders-component"), ("client_secret", secret),
                    ("scope", " ".join(scopes)), ("authorization_details", json.dumps(details))]
            status, issued = request("/realms/component/protocol/openid-connect/token", form)
            assert status == 200, f"Exact component issuance refused: HTTP {status}, error={issued.get('error')}"
            token = issued["access_token"]
            header, body, signature = token.split(".")
            decode = lambda value: base64.urlsafe_b64decode(value + "=" * (-len(value) % 4))
            claims, token_header = json.loads(decode(body)), json.loads(decode(header))
            assert claims["iss"] == issuer and claims["aud"] == "eventstore" and claims["azp"] == "folders-component"
            assert claims["eventstore:operation"] == "eventstore:stream-read" and claims["exp"] - claims["iat"] <= 120
            assert {key: value for key, value in claims.items() if key.startswith("eventstore:bound-")} == details[0]["bindings"]
            _, jwks = request("/realms/component/protocol/openid-connect/certs")
            signing = next(value for value in jwks["keys"] if value["kid"] == token_header["kid"])
            (directory / "signer.pem").write_text("-----BEGIN CERTIFICATE-----\n" + signing["x5c"][0] + "\n-----END CERTIFICATE-----\n")
            public = command("openssl", "x509", "-in", str(directory / "signer.pem"), "-pubkey", "-noout")
            (directory / "public.pem").write_text(public)
            (directory / "input").write_bytes((header + "." + body).encode())
            (directory / "signature").write_bytes(decode(signature))
            assert "Verified OK" in command("openssl", "dgst", "-sha256", "-verify", str(directory / "public.pem"), "-signature", str(directory / "signature"), str(directory / "input"))
            status, admin = request("/realms/master/protocol/openid-connect/token", [("grant_type", "password"), ("client_id", "admin-cli"),
                                     ("username", "component-admin"), ("password", admin_secret)])
            assert status == 200
            bearer = admin["access_token"]
            status, server_info = request("/admin/serverinfo", bearer=bearer)
            assert status == 200 and server_info["systemInfo"]["version"] == "26.6.4"
            server_version = server_info["systemInfo"]["version"]
            image_id = command("docker", "inspect", name, "--format", "{{.Image}}")
            _, clients = request("/admin/realms/component/clients?clientId=folders-component", bearer=bearer)
            client_path = "/admin/realms/component/clients/" + clients[0]["id"]
            if sdk_assembly:
                # Obtain a naturally expired, still valid-signed token from this same own client.
                original_grants = client["attributes"]["hexalith.workload-binding.grants"]
                client["attributes"]["hexalith.workload-binding.grants"] = json.dumps([dict(grant, expiresAt=int(time.time()) + 2)])
                assert request(client_path, client, bearer, "PUT")[0] == 204
                status, short_lived = request("/realms/component/protocol/openid-connect/token", form)
                assert status == 200
                expired = short_lived["access_token"]
                expiry = json.loads(decode(expired.split(".")[1]))["exp"]
                client["attributes"]["hexalith.workload-binding.grants"] = original_grants
                assert request(client_path, client, bearer, "PUT")[0] == 204
                time.sleep(max(0, expiry - time.time() + 0.1))
                expired_file = directory / "expired-component.jwt"
                expired_file.write_text(expired)
                os.chmod(expired_file, 0o600)
                component_file = directory / "sdk-component.json"
                component_file.write_text(json.dumps(dict(issuer=issuer, secret=secret, certificatePath=str(cert), expiredTokenFile=str(expired_file))))
                os.chmod(component_file, 0o600)
                environment = dict(os.environ, HEXALITH_WORKLOAD_COMPONENT_FILE=str(component_file))
                completed = subprocess.run(["dotnet", str(Path(sdk_assembly).resolve()), "-explicit", "on", "-class",
                    "Hexalith.Folders.Server.Tests.EnrolledWorkloadSourceBoundaryTests"], env=environment, capture_output=True, text=True)
                print(completed.stdout, end="")
                if completed.returncode != 0:
                    raise RuntimeError("Explicit real-issuer SDK component tests failed; see test output.")
            controls = []
            for scenario in ["credential", "tenant", "domain", "audience", "operation", "missing", "extra", "duplicate", "oversized", "unbound"]:
                bad = list(form)
                altered = copy.deepcopy(details)
                if scenario == "credential": bad[2] = ("client_secret", secrets.token_urlsafe(32))
                elif scenario in ("tenant", "domain"): altered[0]["bindings"]["eventstore:bound-" + scenario] = "other"
                elif scenario in ("audience", "operation"): altered[0][scenario] = "other"
                elif scenario == "missing": del altered[0]["bindings"]["eventstore:bound-domain"]
                elif scenario == "extra": altered[0]["bindings"]["eventstore:bound-actor"] = "extra"
                if scenario not in ("credential", "unbound", "duplicate", "oversized"):
                    bad[-1] = ("authorization_details", json.dumps(altered))
                elif scenario == "unbound": bad = bad[:-1]
                elif scenario == "duplicate": bad.append(bad[-1])
                elif scenario == "oversized": bad[-1] = ("authorization_details", "x" * 8193)
                status, _ = request("/realms/component/protocol/openid-connect/token", bad)
                assert status in (400, 401), f"Component negative control {scenario} returned {status}"
                controls.append(scenario)
            if apphost_assembly:
                consumer_clients = []
                for resource in ("eventstore", "tenants", "folders", "folders-workers"):
                    own_secret = secrets.token_urlsafe(32)
                    secret_file = directory / (resource + ".secret")
                    secret_file.write_text(own_secret)
                    os.chmod(secret_file, 0o600)
                    probes = []
                    def probe(audience, operation, bindings):
                        probes.append(dict(grantId="component-" + resource + "-" + str(len(probes)), revision=1,
                                           audience=audience, operation=operation, bindings=bindings))
                    if resource == "eventstore":
                        for audience in ("folders", "tenants"):
                            for operation in ("process", "project", "metadata", "replay-state"):
                                probe(audience, "domain-service:" + operation, {})
                        for audience, domain in (("folders", "folders"), ("tenants", "tenants"), ("tenants", "global-administrators")):
                            probe(audience, "domain-service:query", {"eventstore:bound-tenant": "tenant-a", "eventstore:bound-domain": domain,
                                                                  "eventstore:bound-query-authority-v1": "A" * 64})
                    elif resource == "tenants":
                        for operation in ("eventstore:command-submit", "eventstore:stream-read"):
                            probe("eventstore", operation, {})
                    else:
                        probe("eventstore", "eventstore:stream-read", details[0]["bindings"])
                    own_client = copy.deepcopy(client)
                    own_client.update(clientId=resource + "-consumer-component", secret=own_secret)
                    own_client["attributes"]["hexalith.workload-binding.workload"] = resource
                    own_grants = []
                    for value in probes:
                        bindings = value["bindings"]
                        own_grants.append(dict(grant, grantId=value["grantId"], clientId=own_client["clientId"], workload=resource,
                            audience=value["audience"], operations=[value["operation"]], allowUnbound=not bindings,
                            allowQueryAuthorityDigest=value["operation"] == "domain-service:query",
                            bindings={name: [binding] for name, binding in bindings.items() if name != "eventstore:bound-query-authority-v1"}))
                    own_client["attributes"]["hexalith.workload-binding.grants"] = json.dumps(own_grants)
                    own_client["optionalClientScopes"] = optional_scopes
                    assert request("/admin/realms/component/clients", own_client, bearer, "POST")[0] == 201
                    consumer_clients.append(dict(resource=resource, clientId=own_client["clientId"], workload=resource, secretFile=str(secret_file), probes=probes))
                spki = subprocess.check_output(["openssl", "pkey", "-pubin", "-in", str(directory / "public.pem"), "-outform", "DER"])
                consumer_manifest = dict(schema="hexalith.workload-enrollment/v1", authority=issuer, issuer=issuer,
                    tokenEndpoint=issuer + "/protocol/openid-connect/token", notBefore=now - 10, expiresAt=now + 600,
                    signingKeyId=token_header["kid"], signingPublicKeySha256=hashlib.sha256(spki).hexdigest().upper(),
                    receivers=["eventstore", "tenants", "folders", "folders-workers"], clients=consumer_clients)
                manifest_file = directory / "consumer-manifest.json"
                manifest_file.write_text(json.dumps(consumer_manifest))
                os.chmod(manifest_file, 0o600)
                environment = dict(os.environ, HEXALITH_WORKLOAD_CONSUMER_COMPONENT_MANIFEST=str(manifest_file), SSL_CERT_FILE=str(cert))
                completed = subprocess.run(["dotnet", str(Path(apphost_assembly).resolve()), "-explicit", "on", "-class",
                    "Hexalith.Folders.AppHost.Tests.WorkloadEnrollmentComponentTests"], env=environment, capture_output=True, text=True)
                print(completed.stdout, end="")
                if completed.returncode != 0:
                    raise RuntimeError("Explicit consumer component tests failed; see test output.")
            _, clients = request("/admin/realms/component/clients?clientId=folders-component", bearer=bearer)
            client_path = "/admin/realms/component/clients/" + clients[0]["id"]
            for scenario in ("revoked", "expired", "ambiguous", "caller", "revision"):
                changed = copy.deepcopy(grant)
                if scenario == "revoked": changed["revoked"] = True
                elif scenario == "expired": changed["expiresAt"] = now - 1
                elif scenario == "caller": changed["workload"] = "other"
                elif scenario == "revision": changed["revision"] = 0
                grants = [changed]
                if scenario == "ambiguous":
                    grants.append(dict(changed, grantId="second"))
                client["attributes"]["hexalith.workload-binding.grants"] = json.dumps(grants)
                assert request(client_path, client, bearer, "PUT")[0] == 204
                assert request("/realms/component/protocol/openid-connect/token", form)[0] == 400, scenario
                controls.append(scenario)
            client["attributes"]["hexalith.workload-binding.grants"] = json.dumps([dict(grant, revision=2)])
            assert request(client_path, client, bearer, "PUT")[0] == 204
            assert request("/realms/component/protocol/openid-connect/token", form)[0] == 200
            callback = dict(grant, grantId="component-callback", operations=["domain-service:process"], bindings={}, allowUnbound=True)
            query = dict(grant, grantId="component-query", operations=["domain-service:query"], allowQueryAuthorityDigest=True)
            client["attributes"]["hexalith.workload-binding.grants"] = json.dumps([grant, callback, query])
            assert request(client_path, client, bearer, "PUT")[0] == 204
            for operation, bindings in [("domain-service:process", {}), ("domain-service:query", dict(details[0]["bindings"], **{"eventstore:bound-query-authority-v1": "A" * 64}))]:
                traffic = list(form)
                traffic[3] = ("scope", "eventstore-audience.eventstore eventstore-operation." + operation.replace(":", "."))
                traffic[-1] = ("authorization_details", json.dumps([dict(details[0], operation=operation, bindings=bindings)]))
                assert request("/realms/component/protocol/openid-connect/token", traffic)[0] == 200
                # Separate permissions cannot borrow the source grant or omit the query digest.
                invalid = list(traffic)
                invalid[-1] = ("authorization_details", json.dumps([dict(details[0], operation=operation)]))
                assert request("/realms/component/protocol/openid-connect/token", invalid)[0] == 400
            summary = dict(qualification="disposable-component-only", serverVersion=server_version, imageId=image_id, providerSha256=hashlib.sha256(jar.read_bytes()).hexdigest(),
                actualProtectedEnrollment=False, approvedTlsTrust=False, componentTlsValidation=True, independentRsaVerification=True,
                negativeControls=controls, liveGrantReload=True, separateCallbackAndQueryGrants=True,
                sdkValidSignedScopeAndNaturalExpiryControls=bool(sdk_assembly), consumerLiveControls=bool(apphost_assembly))
        finally:
            remaining = command("docker", "ps", "--all", "--filter", "name=^/" + name + "$", "--format", "{{.Names}}")
            if remaining:
                command("docker", "rm", "--force", "--volumes", name)
            if command("docker", "ps", "--all", "--filter", "name=^/" + name + "$", "--format", "{{.Names}}"):
                raise RuntimeError("Task-owned component container cleanup refused.")
    if Path(owned).exists():
        raise RuntimeError("Task-owned component directory cleanup refused.")
    summary.update(observedRemainingContainers=0, observedRemainingTemporaryPaths=0, privateDirectoryMode="0700", privateFileMode="0600")
    print(json.dumps(summary))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sdk-assembly", help="Run explicit source-boundary tests from this Debug assembly against the owned real issuer.")
    parser.add_argument("--apphost-assembly", help="Run explicit consumer controls against the owned component issuer and TLS fixture.")
    arguments = parser.parse_args()
    run(arguments.sdk_assembly, arguments.apphost_assembly)
