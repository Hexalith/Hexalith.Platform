#!/usr/bin/env python3
"""Explicit run-owned component controls; metadata-only evidence, no lifecycle qualification."""
import base64
import hashlib
import json
import os
from pathlib import Path
import secrets
import ssl
import subprocess
import sys
import signal
import shutil
import tempfile
from unittest.mock import patch
import time
import urllib.error
import urllib.parse
import urllib.request

import run_owned as owned


def inventory():
    tenant = "component-" + secrets.token_hex(8)
    names = ["eventstore", "tenants", "folders", "folders-replica", "folders-workers", "folders-workers-replica", "folders-workers-proof-a", "folders-workers-proof-b"]
    suffix = secrets.token_hex(6)
    workloads = {name: ("folders" if name == "folders-replica" else "folders-workers" if name == "folders-workers-replica" else name) + "-" + suffix for name in names}
    clients = []
    for name in names:
        probes = []
        def probe(audience, operation, bindings):
            probes.append(dict(grantId="run-" + hashlib.sha256(workloads[name].encode()).hexdigest()[:24] + "-" + str(len(probes)), revision=1,
                audience=audience, operation=operation, bindings=bindings))
        if name == "eventstore":
            for receiver in ("folders", "tenants"):
                for operation in ("process", "project", "metadata", "replay-state"):
                    probe(workloads[receiver], "domain-service:" + operation, {})
            for receiver, domain, selected in (("folders", "folders", tenant), ("tenants", "tenants", "system"), ("tenants", "global-administrators", "system")):
                probe(workloads[receiver], "domain-service:query", {"eventstore:bound-tenant": selected, "eventstore:bound-domain": domain, "eventstore:bound-query-authority-v1": "A" * 64})
        elif name == "tenants":
            for operation in ("eventstore:command-submit", "eventstore:stream-read"):
                probe(workloads["eventstore"], operation, {})
        else:
            probe(workloads["eventstore"], "eventstore:stream-read", {"eventstore:bound-tenant": tenant, "eventstore:bound-domain": "folders"})
        clients.append(dict(resource=name, clientId=workloads[name], workload=workloads[name], probes=probes))
    return dict(tenant=tenant, receivers=names, clients=clients)


def publish_request():
    directory = Path(os.environ[owned.PREFIX + "RECEIPT"]).parent
    receipt = owned.read_private(directory / "receipt.json")
    expected = inventory()
    owned.private_write(directory / "request.json", dict(runId=receipt["runId"], capability=receipt["capability"], childPid=os.getpid(),
        childIdentity=owned.process_identity(os.getpid()), deadline=receipt["deadline"], inventory=expected, inventorySha256=owned.cohort_digest(expected)))
    return directory, receipt


def probe_child():
    directory, receipt = publish_request()
    while not (directory / "result.json").exists():
        if time.time() >= receipt["deadline"]:
            raise RuntimeError("Component producer deadline elapsed.")
        time.sleep(0.05)
    manifest = owned.read_private(directory / "manifest.json")
    assert manifest["expiresAt"] == receipt["deadline"] and len(manifest["clients"]) == 8
    files = {client["resource"]: client["secretFile"] for client in manifest["clients"]}
    assert files["folders"] == files["folders-replica"] and files["folders-workers"] == files["folders-workers-replica"]
    assert len({files[name] for name in ("folders-workers", "folders-workers-proof-a", "folders-workers-proof-b")}) == 3
    context = ssl.create_default_context()
    def token(client, probe, bindings=None):
        details = [dict(type=owned.PROFILE, audience=probe["audience"], operation=probe["operation"], bindings=probe["bindings"] if bindings is None else bindings)]
        form = dict(grant_type="client_credentials", client_id=client["clientId"], client_secret=Path(client["secretFile"]).read_text(),
            scope="eventstore-audience." + probe["audience"] + " eventstore-operation." + probe["operation"].replace(":", "."), authorization_details=json.dumps(details))
        request = urllib.request.Request(manifest["tokenEndpoint"], data=urllib.parse.urlencode(form).encode())
        with urllib.request.urlopen(request, context=context, timeout=5) as response:
            return json.load(response)["access_token"]
    decode = lambda value: base64.urlsafe_b64decode(value + "=" * (-len(value) % 4))
    verified = 0
    for client in manifest["clients"]:
        for probe in client["probes"]:
            raw = token(client, probe)
            header, payload, signature = raw.split(".")
            claims, key = json.loads(decode(payload)), json.loads(decode(header))
            assert key["kid"] == manifest["signingKeyId"] and key["alg"] == "RS256"
            assert claims["iss"] == manifest["issuer"] and claims["aud"] == probe["audience"] and claims["azp"] == client["workload"]
            assert claims["eventstore:operation"] == probe["operation"] and claims["eventstore:grant-id"] == probe["grantId"]
            assert claims["eventstore:grant-revision"] == probe["revision"] and claims["exp"] <= receipt["deadline"]
            assert {name: value for name, value in claims.items() if name.startswith("eventstore:bound-")} == probe["bindings"]
            owned.private_write(directory / "signed-input", (header + "." + payload).encode())
            owned.private_write(directory / "signature", decode(signature))
            assert "Verified OK" in owned.command("openssl", "dgst", "-sha256", "-verify", str(directory / "public.pem"), "-signature", str(directory / "signature"), str(directory / "signed-input"))
            verified += 1
    gateway = next(client for client in manifest["clients"] if client["resource"] == "eventstore")
    query = next(probe for probe in gateway["probes"] if probe["operation"] == "domain-service:query")
    token(gateway, query, dict(query["bindings"], **{"eventstore:bound-query-authority-v1": "B" * 64}))
    for field in ("eventstore:bound-tenant", "eventstore:bound-domain"):
        try:
            token(gateway, query, dict(query["bindings"], **{field: "other"}))
            raise AssertionError("Changed exact binding accepted.")
        except urllib.error.HTTPError as error:
            assert error.code == 400
    try:
        with urllib.request.urlopen(manifest["issuer"] + "/.well-known/openid-configuration", context=ssl.create_default_context(cafile=ssl.get_default_verify_paths().openssl_cafile), timeout=5):
            raise AssertionError("Missing run trust accepted.")
    except urllib.error.URLError:
        pass
    assert "users" not in owned.read_private(directory / "realm.json")
    print(json.dumps(dict(qualification="run-owned enrollment", freshRsaProbes=verified, exactVersion="26.6.4", replicas=True,
        independentProofCallers=2, dynamicQueryDigest=True, negativeControls=["untrusted-tls", "changed-tenant", "changed-domain"], administrativeIdentity=False)), flush=True)
    owned.private_write(directory / "release.json", dict(runId=receipt["runId"], capability=receipt["capability"]))
    while directory.exists():
        time.sleep(0.05)


def observe_created(lease_directory, container_id, observation):
    running = owned.command("docker", "inspect", container_id, "--format", "{{.State.Running}}", timeout=5)
    if running != "true":
        raise RuntimeError("Real lifecycle control never observed a running owned container.")
    owned.private_write(observation, dict(directory=str(lease_directory), containerId=container_id, creationObserved=True))


def terminal_child(observation, scenario):
    directory, receipt = publish_request()
    while True:
        journal = owned.read_private(directory / "journal.json")
        if "containerId" in journal:
            observe_created(directory, journal["containerId"], observation)
            break
        time.sleep(0.02)
    if scenario in ("child-exit", "child-failure"):
        return 0 if scenario == "child-exit" else 3
    while True:
        time.sleep(0.1)


def terminal_launcher(scenario, observation):
    original = owned.Lease.run
    def fail_after_creation(lease, *args, **kwargs):
        result = original(lease, *args, **kwargs)
        if args[:2] == ("docker", "run"):
            observe_created(lease.directory, result, observation)
            owned.refuse("injected-post-creation-failure")
        return result
    with patch.object(owned.Lease, "run", fail_after_creation if scenario == "post-creation-failure" else original):
        return owned.launch(8 if scenario == "expiry" else 30, owned.OWNER,
            [sys.executable, "-B", str(Path(__file__).resolve()), "_terminal-child", str(observation), scenario])


def lifecycle_controls():
    # A separate live control exists before every owned launch and must survive each production cleanup.
    with tempfile.TemporaryDirectory(prefix="run-owned-live-lifecycle-") as name:
        directory = Path(name)
        directory.chmod(0o700)
        control_name = "hexalith-run-owned-unrelated-" + secrets.token_hex(8)
        owned.private_write(directory / "control-journal.json", dict(container=control_name))
        control_id = owned.command("docker", "run", "--detach", "--rm", "--name", control_name,
            "--label", "com.hexalith.run-owned-control=" + control_name, "--entrypoint", "/bin/sh", owned.IMAGE, "-c", "sleep 180")
        try:
            for scenario in ("post-creation-failure", "cancellation", "expiry", "child-exit", "child-failure"):
                observation = directory / (scenario + ".json")
                process = subprocess.Popen([sys.executable, "-B", str(Path(__file__).resolve()), "_terminal-launcher", scenario, str(observation)],
                    stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
                try:
                    deadline = time.monotonic() + 20
                    while not observation.exists() and process.poll() is None and time.monotonic() < deadline:
                        time.sleep(0.02)
                    if not observation.exists():
                        output, errors = process.communicate(timeout=15)
                        reason = next((line for line in errors.splitlines() if line.startswith("Run-owned workload enrollment refused:")), "producer exited")
                        raise RuntimeError("Real lifecycle control failed before owned creation: " + scenario + "; " + reason)
                    observed = owned.read_private(observation)
                    if scenario == "cancellation":
                        process.send_signal(signal.SIGTERM)
                    output, errors = process.communicate(timeout=20)
                    if process.returncode == 0 or not observed["creationObserved"]:
                        raise RuntimeError("Real lifecycle control did not exercise terminal provisioning: " + scenario)
                    if owned.command("docker", "ps", "--all", "--filter", "id=" + observed["containerId"], "--format", "{{.ID}}") or Path(observed["directory"]).exists():
                        raise RuntimeError("Real lifecycle control left owned resources: " + scenario)
                    if owned.command("docker", "inspect", control_id, "--format", "{{.State.Running}}") != "true":
                        raise RuntimeError("Unrelated live control was changed.")
                    if '"observedRemainingTemporaryPaths": 0' not in output or "PRIVATE KEY" in output + errors:
                        raise RuntimeError("Real lifecycle control lacks redacted cleanup proof.")
                finally:
                    if process.poll() is None:
                        process.send_signal(signal.SIGTERM)
                    process.communicate(timeout=15)
            print(json.dumps(dict(qualification="run-owned enrollment", realContainerTerminalControls=5,
                postCreationFailure=True, cancellation=True, expiry=True, childExitAndFailure=True,
                unrelatedLiveControlPreserved=True, observedRemainingContainers=0, observedRemainingTemporaryPaths=0)), flush=True)
        finally:
            owned.command("docker", "rm", "--force", "--volumes", control_id)


def nonstandard_caller_control():
    caller = os.getuid() if os.getuid() not in (0, 1000) else 1001
    identity = {}
    prefix = []
    with tempfile.TemporaryDirectory(prefix="run-owned-caller-control-") as name:
        directory = Path(name)
        for script in ("run_owned.py", "run_owned_component.py"):
            shutil.copyfile(owned.ROOT / script, directory / script)
        (directory / "target").mkdir(mode=0o700)
        shutil.copyfile(owned.JAR, directory / "target" / owned.JAR.name)
        elevated = os.getuid() == 1000
        if os.getuid() == 0:
            for path in [directory, *directory.rglob("*")]:
                os.chown(path, caller, caller)
            identity = dict(user=caller, group=caller, extra_groups=[Path("/var/run/docker.sock").stat().st_gid])
        elif elevated:
            # Validation-only caller switch; runtime files remain 0600 under their real non-1000 owner.
            owned.command("sudo", "-n", "chown", "--recursive", str(caller) + ":" + str(caller), str(directory))
            prefix = ["sudo", "-n", "--preserve-env=DOCKER_CONFIG", "setpriv", "--reuid=" + str(caller), "--regid=" + str(caller),
                "--groups=" + str(Path("/var/run/docker.sock").stat().st_gid), "--"]
        try:
            environment = dict(os.environ, DOCKER_CONFIG=str(directory))
            result = subprocess.run([*prefix, sys.executable, "-B", str(directory / "run_owned.py"), "--owner", owned.OWNER, "--lifetime-seconds", "90", "--",
                sys.executable, "-B", str(directory / "run_owned_component.py"), "_probe"], env=environment, cwd="/tmp", capture_output=True, text=True, timeout=100, **identity)
            if result.returncode or '"observedRemainingTemporaryPaths": 0' not in result.stdout:
                reason = next((line for line in result.stderr.splitlines() if line.startswith("Run-owned workload enrollment refused:")), "protected output withheld")
                raise RuntimeError("Real non-1000 caller enrollment or cleanup failed: " + reason)
            print(json.dumps(dict(qualification="run-owned enrollment", non1000Caller=caller, runtimeGroup=0,
                hostSecretPermissions="0600", realProviderAndCleanup=True)), flush=True)
        finally:
            if elevated:
                owned.command("sudo", "-n", "chown", "--recursive", str(os.getuid()) + ":" + str(os.getgid()), str(directory))


def run(apphost_assembly=None, platform_assembly=None):
    if apphost_assembly:
        environment = dict(os.environ, HEXALITH_RUN_OWNED_WORKLOAD_COMPONENT="true")
        arguments = ["dotnet", str(Path(apphost_assembly).resolve()), "-explicit", "on", "-method",
            "Hexalith.Folders.AppHost.Tests.WorkloadEnrollmentComponentTests.RunOwnedAuthorityVerifiesFinalCohortAndAllRefusals"]
    elif platform_assembly:
        environment = dict(os.environ, HEXALITH_RUN_OWNED_WORKLOAD_COMPONENT="true")
        arguments = ["dotnet", str(Path(platform_assembly).resolve()), "-explicit", "on", "-method",
            "Hexalith.Platform.Composition.Tests.PlatformRunOwnedWorkloadEnrollmentTests.RunOwnedAuthorityPassesUnchangedConsumerAndNegativeControls"]
    else:
        environment = os.environ
        arguments = [sys.executable, "-B", str(Path(__file__).resolve()), "_probe"]
    result = subprocess.run([sys.executable, "-B", str(owned.ROOT / "run_owned.py"), "--owner", owned.OWNER, "--lifetime-seconds", "180", "--", *arguments], env=environment)
    if result.returncode:
        raise RuntimeError("Explicit run-owned component controls failed.")
    if not apphost_assembly and not platform_assembly:
        lifecycle_controls()
        nonstandard_caller_control()


if __name__ == "__main__":
    if sys.argv[1:] == ["_probe"]:
        probe_child()
    elif sys.argv[1:2] == ["_terminal-child"]:
        sys.exit(terminal_child(Path(sys.argv[2]), sys.argv[3]))
    elif sys.argv[1:2] == ["_terminal-launcher"]:
        try:
            sys.exit(terminal_launcher(sys.argv[2], Path(sys.argv[3])))
        except RuntimeError as error:
            print(str(error), file=sys.stderr)
            sys.exit(1)
        except InterruptedError:
            sys.exit(1)
    else:
        run()
