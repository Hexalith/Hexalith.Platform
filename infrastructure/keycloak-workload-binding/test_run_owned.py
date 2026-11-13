"""Ownership and lifecycle controls; tests retain no grants, credentials or private material."""
import copy
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from types import SimpleNamespace
from unittest.mock import patch

import run_owned as owned
import run_owned_component as component
from run_owned_component import inventory, lifecycle_controls, nonstandard_caller_control


class RunOwnedTests(unittest.TestCase):
    def test_lease_pins_original_docker_selection_across_default_context_switch(self):
        for selection in ({}, {"DOCKER_CONTEXT": "explicit"}, {"DOCKER_HOST": "unix:///original.sock"}):
            with self.subTest(selection=selection), patch.dict(os.environ, selection, clear=True):
                current = "original"
                removed = False
                calls = []
                def command(*args, **kwargs):
                    nonlocal removed
                    if args == ("docker", "context", "show"): return current
                    environment = kwargs["environment"]
                    actual = environment.get("DOCKER_CONTEXT") or environment.get("DOCKER_HOST") or current
                    calls.append((args[1], actual))
                    if args[1] == "ps":
                        return "" if removed or actual == "foreign" else json.dumps(dict(id="a" * 64, runId=lease.run_id))
                    if args[1] == "rm": removed = True
                    return ""
                with patch.object(owned, "command", side_effect=command):
                    lease = owned.Lease(60)
                    try:
                        current = "foreign"
                        for operation in ("run", "exec", "inspect"): lease.run("docker", operation)
                        lease.created = True
                        lease.container_id = "a" * 64
                        lease.cleanup()
                        expected = selection.get("DOCKER_CONTEXT") or selection.get("DOCKER_HOST") or "original"
                        self.assertTrue(removed)
                        self.assertEqual({expected}, {actual for _, actual in calls})
                        self.assertEqual("foreign", current)
                    finally:
                        lease.cleanup()

    def test_constructor_time_termination_cleans_before_child_launch(self):
        with tempfile.TemporaryDirectory() as name:
            observation, child_marker = Path(name) / "allocated", Path(name) / "child-started"
            script = "import os,pathlib,signal,sys; import run_owned as o; original=o.private_write\n" \
                + "def publish(path,data):\n if path.name=='journal.json':\n  pathlib.Path(" + repr(str(observation)) + ").write_text(str(path.parent)); os.kill(os.getpid(),signal.SIGTERM)\n original(path,data)\n" \
                + "o.private_write=publish\ntry:\n o.launch(30,o.OWNER,[sys.executable,'-c'," + repr("from pathlib import Path; Path(" + repr(str(child_marker)) + ").touch()") + "])\nexcept InterruptedError:\n sys.exit(1)"
            completed = subprocess.run([sys.executable, "-B", "-c", script], capture_output=True, text=True, timeout=15)
            self.assertEqual(1, completed.returncode, completed.stderr)
            self.assertTrue(observation.exists())
            self.assertFalse(Path(observation.read_text()).exists())
            self.assertFalse(child_marker.exists())
            self.assertIn('"observedRemainingTemporaryPaths": 0', completed.stdout)

    def test_preexisting_grandchild_survives_its_parent_exiting_during_launch(self):
        with tempfile.TemporaryDirectory() as name:
            observation, parent_exit = Path(name) / "grandchild", Path(name) / "parent-exit"
            parent = "import pathlib,subprocess,sys,time; child=subprocess.Popen([sys.executable,'-c','import time; time.sleep(60)']); pathlib.Path(" + repr(str(observation)) + ").write_text(str(child.pid))\nwhile not pathlib.Path(" + repr(str(parent_exit)) + ").exists(): time.sleep(.01)"
            child = "import pathlib,time; pathlib.Path(" + repr(str(parent_exit)) + ").touch()\nwhile True:\n try: state=pathlib.Path('/proc/PARENT/stat').read_text().split(') ',1)[1].split()[0]\n except FileNotFoundError: break\n if state=='Z': break\n time.sleep(.01)"
            wrapper = "import os,pathlib,signal,subprocess,sys,time; import run_owned as o; o.enable_subreaper(); parent=subprocess.Popen([sys.executable,'-c'," + repr(parent) + "])\n" \
                + "while not pathlib.Path(" + repr(str(observation)) + ").exists(): time.sleep(.01)\n" \
                + "pid=int(pathlib.Path(" + repr(str(observation)) + ").read_text())\ntry:\n result=o.launch(30,o.OWNER,[sys.executable,'-c'," + repr(child) + ".replace('PARENT',str(parent.pid))]); assert result==0; parent.wait(timeout=1); os.kill(pid,0)\nfinally:\n os.kill(pid,signal.SIGTERM); os.waitpid(pid,0); parent.wait()"
            completed = subprocess.run([sys.executable, "-B", "-c", wrapper], capture_output=True, text=True, timeout=15)
            self.assertEqual(0, completed.returncode, completed.stderr)

    def test_optimized_component_refuses_before_allocating_resources(self):
        script = "import tempfile; tempfile.mkdtemp=lambda *a,**k: (_ for _ in ()).throw(RuntimeError('resource-allocation-attempted')); import run_owned_component; run_owned_component.run()"
        completed = subprocess.run([sys.executable, "-B", "-c", script], env=dict(os.environ, PYTHONOPTIMIZE="1"), capture_output=True, text=True, timeout=10)
        self.assertNotEqual(0, completed.returncode)
        self.assertIn("require unoptimized Python execution", completed.stderr)
        self.assertEqual("RuntimeError: Run-owned component controls require unoptimized Python execution.", completed.stderr.splitlines()[-1])

    def test_assembly_harness_refuses_exit_zero_with_no_executed_test(self):
        def zero_test(arguments, **kwargs):
            Path(arguments[arguments.index("-xml") + 1]).write_text('<assemblies><assembly total="0" passed="0" failed="0" errors="0" skipped="0" not-run="0" /></assemblies>')
            return SimpleNamespace(returncode=0, stdout="Total: 0", stderr="")
        with patch.object(component.subprocess, "run", side_effect=zero_test):
            with self.assertRaisesRegex(RuntimeError, "lack executed-test"):
                component.run_assembly("unselected.dll", "Tests.Expected.ExplicitMethod")

    def test_assembly_harness_requires_consumer_and_provision_observations(self):
        method = "Tests.Expected.ExplicitMethod"
        with tempfile.TemporaryDirectory() as name:
            result = Path(name) / "result.xml"
            result.write_text('<assemblies><assembly total="1" passed="1" failed="0" errors="0" skipped="0" not-run="0"><collection><test type="Tests.Expected" method="ExplicitMethod" result="Pass" /></collection></assembly></assemblies>')
            summary = dict(qualification="run-owned enrollment", provisioned=True, resourceCount=8, workloadCount=6, serverVersion="26.6.4",
                startupImport=True, administrativeIdentity=False, observedRemainingContainers=0, observedRemainingTemporaryPaths=0)
            markers = ["WORKLOAD_ENROLLMENT_CONTEXT qualification=run-owned enrollment resources=8",
                "WORKLOAD_ENROLLMENT_VERIFIED manifestSha256=" + "A" * 64 + " receivers=8 clients=8",
                "RUN_OWNED_CONSUMER_CONTROLS resources=8 workloads=6 proofCallers=2"]
            component.validate_assembly_result("\n".join([*markers, json.dumps(summary)]), result, method)
            for missing in range(len(markers)):
                with self.subTest(missing=missing), self.assertRaisesRegex(RuntimeError, "lack executed-test"):
                    component.validate_assembly_result("\n".join([*(line for i, line in enumerate(markers) if i != missing), json.dumps(summary)]), result, method)
            summary["provisioned"] = False
            with self.assertRaisesRegex(RuntimeError, "lack executed-test"):
                component.validate_assembly_result("\n".join([*markers, json.dumps(summary)]), result, method)

    def test_command_keeps_machine_stdout_separate_from_stderr(self):
        output = owned.command(sys.executable, "-c", "import sys; print('a'*64); print('daemon warning', file=sys.stderr)")
        self.assertEqual("a" * 64, output)
        self.assertEqual(b"public-key-bytes", owned.command(sys.executable, "-c",
            "import sys; sys.stdout.buffer.write(b'public-key-bytes'); print('warning', file=sys.stderr)", binary=True))
        with self.assertRaisesRegex(RuntimeError, "permissions"):
            owned.command(sys.executable, "-c", "import sys; print('permission denied', file=sys.stderr); sys.exit(1)")

    def test_revision_requires_an_integer_one(self):
        for revision in (True, False, 1.0, "1", 2, None):
            with self.subTest(revision=repr(revision)):
                model = inventory()
                model["clients"][0]["probes"][0]["revision"] = revision
                with self.assertRaisesRegex(RuntimeError, "probe-fields"):
                    owned.validate_inventory(model)

    def test_request_digest_hashes_exact_inventory_value_bytes(self):
        model = inventory()
        compact = json.dumps(model, separators=(",", ":")).encode()
        variants = [compact, json.dumps(model, indent=2).encode(), compact.replace(b'"tenant"', b'"\\u0074enant"', 1)]
        for raw_inventory in variants:
            for matches in (True, False):
                with self.subTest(format=variants.index(raw_inventory), matches=matches):
                    lease = owned.Lease(60)
                    try:
                        owned.private_write(lease.directory / "trust.pem", b"isolated-command-control")
                        lease.bind(os.getpid(), "A" * 64)
                        request = {name: lease.receipt[name] for name in ("runId", "capability", "childPid", "childIdentity", "deadline")}
                        request.update(inventory="RAW-INVENTORY", inventorySha256=owned.cohort_digest(raw_inventory) if matches else "0" * 64)
                        raw = json.dumps(request).encode().replace(b'"RAW-INVENTORY"', raw_inventory)
                        owned.private_write(lease.directory / "request.json", raw)
                        self.assertEqual(raw_inventory, owned.request_inventory_bytes(raw))
                        with patch.object(lease, "run", side_effect=RuntimeError("stop-at-create")) as command:
                            with self.assertRaisesRegex(RuntimeError, "stop-at-create" if matches else "cohort-binding-changed"):
                                lease.provision()
                            self.assertEqual(1 if matches else 0, command.call_count)
                    finally:
                        with patch.object(owned, "command", return_value=""):
                            lease.cleanup()

    def test_readiness_retries_non_json_until_valid_metadata(self):
        lease = owned.Lease(60)
        base = "https://localhost:49153/realms/run-owned"
        metadata = dict(issuer=base, token_endpoint=base + "/protocol/openid-connect/token", jwks_uri=base + "/protocol/openid-connect/certs")
        jwks = dict(keys=[dict(kty="RSA", alg="RS256", use="sig", kid="test-signer", x5c=["certificate"])])
        def command(*args, **kwargs):
            if args[:2] == ("docker", "run"): return "e" * 64
            if args[:2] == ("docker", "exec"): return "Keycloak 26.6.4"
            return b"public-key" if kwargs.get("binary") else "public-key"
        try:
            lease.prepare()
            lease.bind(os.getpid(), "A" * 64)
            model = inventory()
            request = {name: lease.receipt[name] for name in ("runId", "capability", "childPid", "childIdentity", "deadline")}
            request.update(inventory=model, inventorySha256=owned.cohort_digest(json.dumps(model, separators=(",", ":")).encode()))
            owned.private_write(lease.directory / "request.json", request)
            with patch.object(lease, "run", side_effect=command), patch.object(lease, "https_origin", return_value="https://localhost:49153"), \
                patch.object(lease, "get", side_effect=[json.JSONDecodeError("startup response", "not JSON", 0), OSError(), metadata, jwks]) as get:
                self.assertEqual("26.6.4", lease.provision()["serverVersion"])
                self.assertEqual(4, get.call_count)
        finally:
            with patch.object(owned, "command", return_value=""):
                lease.cleanup()

    def test_replica_entries_share_only_identical_workload_credentials(self):
        with tempfile.TemporaryDirectory() as name:
            realm, entries = owned.build_realm(inventory(), Path(name), int(time.time()) - 1, int(time.time()) + 60)
            self.assertEqual(8, len(entries))
            self.assertEqual(6, len(realm["clients"]))
            paths = {c["resource"]: c["secretFile"] for c in entries}
            self.assertEqual(paths["folders"], paths["folders-replica"])
            self.assertEqual(paths["folders-workers"], paths["folders-workers-replica"])
            self.assertEqual(3, len({paths[c] for c in ("folders-workers", "folders-workers-proof-a", "folders-workers-proof-b")}))
            self.assertNotIn("users", realm)
            for client in realm["clients"]:
                for grant in json.loads(client["attributes"]["hexalith.workload-binding.grants"]):
                    self.assertLessEqual(grant["expiresAt"], int(time.time()) + 60)
                    if grant["operations"] == ["domain-service:query"]:
                        self.assertTrue(grant["allowQueryAuthorityDigest"])
                        self.assertNotIn("eventstore:bound-query-authority-v1", grant["bindings"])

    def test_exact_tenant_cohort_and_permissions_refuse(self):
        original = inventory()
        for fault in ("tenant", "tenants-storage", "global-administrators-storage", "missing", "callback", "proof", "replica", "foreign", "extra"):
            with self.subTest(fault=fault):
                changed = copy.deepcopy(original)
                clients = {client["resource"]: client for client in changed["clients"]}
                if fault == "tenant": changed["tenant"] = "other"
                if fault in ("tenants-storage", "global-administrators-storage"):
                    domain = "tenants" if fault == "tenants-storage" else "global-administrators"
                    next(probe for probe in clients["eventstore"]["probes"] if probe["bindings"].get("eventstore:bound-domain") == domain)["bindings"]["eventstore:bound-tenant"] = changed["tenant"]
                if fault == "missing": clients["eventstore"]["probes"].pop()
                if fault == "callback": clients["eventstore"]["probes"][0]["bindings"] = {"eventstore:bound-tenant": changed["tenant"]}
                if fault == "proof": clients["folders-workers-proof-b"]["workload"] = clients["folders-workers-proof-b"]["clientId"] = clients["folders-workers-proof-a"]["workload"]
                if fault == "replica": clients["folders-replica"]["probes"][0]["revision"] = 2
                if fault == "foreign": changed["receivers"].append("production")
                if fault == "extra": clients["folders"]["probes"].append(copy.deepcopy(clients["folders"]["probes"][0]))
                with self.assertRaises(RuntimeError): owned.validate_inventory(changed)

    def test_explicit_finite_disposable_owner_required(self):
        for lifetime, owner in ((0, owned.OWNER), (-1, owned.OWNER), (86401, owned.OWNER), (float("inf"), owned.OWNER), (60, "production")):
            with self.subTest(lifetime=lifetime, owner=owner):
                with self.assertRaises(RuntimeError): owned.Lease(lifetime, owner)

    def test_private_path_and_duplicate_fields_refuse(self):
        with tempfile.TemporaryDirectory() as name:
            path = Path(name) / "artifact"
            path.write_text('{"owner":1,"owner":2}')
            path.chmod(0o600)
            with self.assertRaises(RuntimeError): owned.read_private(path)
            path.write_text('{}')
            path.chmod(0o644)
            with self.assertRaises(RuntimeError): owned.read_private(path)
            link = Path(name) / "link"
            link.symlink_to(path)
            with self.assertRaises(RuntimeError): owned.read_private(link)

    def test_foreign_request_refuses_before_container_creation(self):
        for fault in ("runId", "capability", "childPid", "childIdentity", "deadline", "inventorySha256"):
            with self.subTest(fault=fault):
                lease = owned.Lease(60)
                try:
                    lease.prepare()
                    lease.bind(os.getpid(), "A" * 64)
                    model = inventory()
                    request = {k: lease.receipt[k] for k in ("runId", "capability", "childPid", "childIdentity", "deadline")}
                    request.update(inventory=model, inventorySha256=owned.cohort_digest(json.dumps(model, separators=(",", ":")).encode()))
                    request[fault] = "foreign" if isinstance(request[fault], str) else request[fault] + 1
                    owned.private_write(lease.directory / "request.json", request)
                    with patch.object(owned, "command") as command:
                        with self.assertRaises(RuntimeError): lease.provision()
                        command.assert_not_called()
                finally:
                    lease.cleanup()

    def test_cleanup_removes_verified_immutable_id_and_preserves_reused_name(self):
        lease = owned.Lease(60)
        unrelated = Path(tempfile.mkdtemp(prefix="unrelated-workload-control-"))
        sentinel = unrelated / "keep"
        sentinel.write_text("unrelated")
        calls = []
        lease.created = True
        def command(*args, **kwargs):
            calls.append(args)
            if args[1] == "ps": return json.dumps(dict(id="a" * 64, runId=lease.run_id)) if len([call for call in calls if call[1] == "ps"]) == 1 else ""
            return ""
        try:
            with patch.object(owned, "command", side_effect=command): lease.cleanup()
            self.assertIn(("docker", "rm", "--force", "--volumes", "a" * 64), calls)
            self.assertFalse(any(call[1] == "rm" and lease.name in call for call in calls))
            self.assertFalse(lease.directory.exists())
            self.assertEqual("unrelated", sentinel.read_text())
        finally:
            sentinel.unlink()
            unrelated.rmdir()

    def test_foreign_container_is_preserved_and_cleanup_failure_reported(self):
        lease = owned.Lease(60)
        lease.created = True
        calls = []
        def command(*args, **kwargs):
            calls.append(args)
            return json.dumps(dict(id="a" * 64, runId="foreign-run")) if args[1] == "ps" else ""
        with patch.object(owned, "command", side_effect=command), patch("builtins.print") as output:
            with self.assertRaisesRegex(RuntimeError, "owned-container-cleanup-incomplete"): lease.cleanup()
            output.assert_called_once()
        self.assertFalse(any(call[1] == "rm" for call in calls))
        self.assertTrue(lease.directory.exists())
        with patch.object(owned, "command", return_value=""):
            lease.cleanup()
        self.assertFalse(lease.directory.exists())

    def test_failed_container_removal_keeps_private_acknowledgement_incomplete_until_retry(self):
        lease = owned.Lease(60)
        lease.created = True
        def command(*args, **kwargs):
            if args[1] == "ps": return json.dumps(dict(id="b" * 64, runId=lease.run_id))
            raise RuntimeError("Injected removal failure")
        with patch.object(owned, "command", side_effect=command), patch("builtins.print"):
            with self.assertRaisesRegex(RuntimeError, "owned-container-cleanup-incomplete"): lease.cleanup()
        self.assertTrue((lease.directory / "journal.json").is_file())
        with patch.object(owned, "command", return_value=""):
            lease.cleanup()
        self.assertFalse(lease.directory.exists())

    def test_platform_default_and_explicit_launch_bundles_are_preserved(self):
        for scenario in ("platform", "explicit", "missing"):
            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory() as name:
                bundle = Path(name) / "platform-bundle.pem"
                bundle.write_bytes(b"PLATFORM ROOTS\n")
                explicit = Path(name) / "inherited-bundle.pem"
                explicit.write_bytes(b"INHERITED ROOTS\n")
                lease = owned.Lease(60)
                def openssl(*args, **kwargs):
                    for option in ("-out", "-keyout"):
                        if option in args: Path(args[args.index(option) + 1]).write_bytes(b"GENERATED CA\n")
                    return ""
                try:
                    with patch.dict(os.environ, {}, clear=True), patch.object(owned.ssl, "get_default_verify_paths", return_value=SimpleNamespace(cafile=None if scenario == "missing" else str(bundle))), patch.object(lease, "run", side_effect=openssl):
                        if scenario == "explicit": os.environ["SSL_CERT_FILE"] = str(explicit)
                        if scenario == "missing":
                            with self.assertRaisesRegex(RuntimeError, "system-or-inherited-launch-trust-required"): lease.prepare()
                        else:
                            lease.prepare()
                            self.assertIn(explicit.read_bytes() if scenario == "explicit" else bundle.read_bytes(), (lease.directory / "trust.pem").read_bytes())
                            self.assertTrue(all(path.stat().st_mode & 0o077 == 0 for path in lease.directory.iterdir()))
                finally:
                    lease.cleanup()

    def test_provisioning_subprocess_is_bounded_by_original_remaining_lifetime(self):
        lease = owned.Lease(60)
        try:
            lease.deadline = time.time() + 0.15
            started = time.monotonic()
            with self.assertRaises(RuntimeError): lease.run(sys.executable, "-c", "import time; time.sleep(5)")
            self.assertLess(time.monotonic() - started, 1)
        finally:
            lease.cleanup()

    def test_expired_preparation_never_creates_child(self):
        lease = owned.Lease(60)
        def expired(): lease.deadline = int(time.time()) - 1
        with patch.object(owned, "Lease", return_value=lease), patch.object(lease, "prepare", side_effect=expired), patch.object(owned, "enable_subreaper"), patch.object(owned.subprocess, "Popen") as child, patch("builtins.print"):
            with self.assertRaisesRegex(RuntimeError, "fixed-owner-deadline-expired"): owned.launch(60, owned.OWNER, [sys.executable, "-c", "pass"])
            child.assert_not_called()
        self.assertFalse(lease.directory.exists())

    def test_exited_group_leader_cannot_leave_a_surviving_descendant(self):
        with tempfile.TemporaryDirectory() as name:
            observation = Path(name) / "descendant.json"
            script = "import json,os,pathlib,subprocess,sys; p=subprocess.Popen([sys.executable,'-c','import time; time.sleep(60)']); pathlib.Path(" + repr(str(observation)) + ").write_text(json.dumps({'descendant':p.pid,'group':os.getpgrp()}))"
            completed = subprocess.run([sys.executable, "-B", str(owned.ROOT / "run_owned.py"), "--owner", owned.OWNER, "--lifetime-seconds", "30", "--", sys.executable, "-c", script], capture_output=True, text=True, timeout=15)
            self.assertEqual(0, completed.returncode)
            observed = json.loads(observation.read_text())
            self.assertFalse(Path("/proc/" + str(observed["descendant"])).exists())
            self.assertFalse(owned.group_members(observed["group"]))

    def test_escaped_session_descendant_is_reaped_and_preexisting_child_survives(self):
        with tempfile.TemporaryDirectory() as name:
            observation = Path(name) / "descendant.json"
            child = "import json,os,pathlib,subprocess,sys; p=subprocess.Popen([sys.executable,'-c','import signal,time; signal.signal(signal.SIGTERM,signal.SIG_IGN); time.sleep(60)'],start_new_session=True); pathlib.Path(" + repr(str(observation)) + ").write_text(json.dumps({'descendant':p.pid,'group':os.getpgrp()}))"
            wrapper = "import run_owned as o,subprocess,sys; control=subprocess.Popen([sys.executable,'-c','import time; time.sleep(60)']); " \
                + "\ntry:\n result=o.launch(30,o.OWNER,[sys.executable,'-c'," + repr(child) + "]); assert result==0; assert control.poll() is None" \
                + "\nfinally:\n control.terminate(); control.wait()"
            completed = subprocess.run([sys.executable, "-B", "-c", wrapper], capture_output=True, text=True, timeout=20)
            self.assertEqual(0, completed.returncode, completed.stderr)
            observed = json.loads(observation.read_text())
            self.assertFalse(Path("/proc/" + str(observed["descendant"])).exists())
            self.assertFalse(owned.group_members(observed["group"]))

    def test_docker_allocated_binding_is_owned_https_loopback_only(self):
        lease = owned.Lease(60)
        lease.container_id = "c" * 64
        snapshot = dict(id=lease.container_id, runId=lease.run_id,
            bindings={"8080/tcp": None, "8443/tcp": [dict(HostIp="127.0.0.1", HostPort="49153")]})
        try:
            with patch.object(lease, "run", return_value=json.dumps(snapshot)) as inspect:
                self.assertEqual("https://localhost:49153", lease.https_origin())
                self.assertEqual(("docker", "inspect", lease.container_id), inspect.call_args.args[:3])
            for fault in ("id", "owner", "wildcard", "ipv6", "missing", "extra-port", "extra-binding", "port-zero", "port-range", "port-type", "port-shape"):
                with self.subTest(fault=fault):
                    changed = copy.deepcopy(snapshot)
                    if fault == "id": changed["id"] = "d" * 64
                    if fault == "owner": changed["runId"] = "foreign"
                    if fault == "wildcard": changed["bindings"]["8443/tcp"][0]["HostIp"] = "0.0.0.0"
                    if fault == "ipv6": changed["bindings"]["8443/tcp"][0]["HostIp"] = "::1"
                    if fault == "missing": changed["bindings"]["8443/tcp"] = None
                    if fault == "extra-port": changed["bindings"]["8080/tcp"] = [dict(HostIp="127.0.0.1", HostPort="49154")]
                    if fault == "extra-binding": changed["bindings"]["8443/tcp"].append(dict(HostIp="127.0.0.1", HostPort="49154"))
                    if fault == "port-zero": changed["bindings"]["8443/tcp"][0]["HostPort"] = "0"
                    if fault == "port-range": changed["bindings"]["8443/tcp"][0]["HostPort"] = "65536"
                    if fault == "port-type": changed["bindings"]["8443/tcp"][0]["HostPort"] = 49153
                    if fault == "port-shape": changed["bindings"]["8443/tcp"][0]["HostPort"] = "049153"
                    with patch.object(lease, "run", return_value=json.dumps(changed)):
                        with self.assertRaisesRegex(RuntimeError, "owned-loopback-port-binding-required"): lease.https_origin()
        finally:
            lease.cleanup()

    def test_provision_asks_docker_for_a_port_before_deriving_origin(self):
        lease = owned.Lease(60)
        try:
            lease.prepare()
            lease.bind(os.getpid(), "A" * 64)
            model = inventory()
            request = {name: lease.receipt[name] for name in ("runId", "capability", "childPid", "childIdentity", "deadline")}
            request.update(inventory=model, inventorySha256=owned.cohort_digest(json.dumps(model, separators=(",", ":")).encode()))
            owned.private_write(lease.directory / "request.json", request)
            with patch.object(lease, "run", return_value="e" * 64) as run, patch.object(lease, "https_origin", side_effect=RuntimeError("Stop after binding inspection")) as origin:
                with self.assertRaisesRegex(RuntimeError, "Stop after binding inspection"): lease.provision()
                arguments = run.call_args.args
                self.assertEqual("127.0.0.1::8443", arguments[arguments.index("--publish") + 1])
                self.assertIn("--hostname=localhost", arguments)
                self.assertFalse(any("hostname-strict=false" in value for value in arguments))
                self.assertEqual("e" * 64, lease.container_id)
                origin.assert_called_once()
        finally:
            with patch.object(owned, "command", return_value=""):
                lease.cleanup()

    def test_real_post_creation_terminal_paths_preserve_unrelated_live_control(self):
        lifecycle_controls()

    def test_real_provider_supports_non_1000_caller_with_private_host_files(self):
        nonstandard_caller_control()

    def test_exit_failure_expiry_and_signal_clean_launcher_material(self):
        for scenario in ("success", "failure", "expiry", "signal", "quit", "abort", "repeated-signal"):
            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory() as name:
                observation = Path(name) / "observation.json"
                child = "import json,os,pathlib,time; p=pathlib.Path(os.environ['" + owned.PREFIX + "RECEIPT']); pathlib.Path(" + repr(str(observation)) + ").write_text(json.dumps({'directory':str(p.parent),'owner':json.loads(p.read_text())['ownerPid']})); "
                if scenario == "repeated-signal": child = "import signal; signal.signal(signal.SIGTERM,signal.SIG_IGN); " + child
                child += "raise SystemExit(3)" if scenario == "failure" else "time.sleep(30)" if scenario not in ("success", "repeated-signal") else "pass" if scenario == "success" else "import signal; signal.signal(signal.SIGTERM,signal.SIG_IGN); time.sleep(30)"
                process = subprocess.Popen([sys.executable, "-B", str(owned.ROOT / "run_owned.py"), "--owner", owned.OWNER,
                    "--lifetime-seconds", "2" if scenario == "expiry" else "30", "--", sys.executable, "-B", "-c", child], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
                limit = time.monotonic() + 15
                while not observation.exists() and process.poll() is None and time.monotonic() < limit: time.sleep(0.02)
                self.assertTrue(observation.exists())
                observed = json.loads(observation.read_text())
                if scenario in ("signal", "quit", "abort", "repeated-signal"):
                    os.kill(process.pid, signal.SIGQUIT if scenario == "quit" else signal.SIGABRT if scenario == "abort" else signal.SIGTERM)
                if scenario == "repeated-signal":
                    time.sleep(0.1)
                    for number in (signal.SIGTERM, signal.SIGINT, signal.SIGHUP, signal.SIGQUIT, signal.SIGABRT):
                        if process.poll() is None:
                            os.kill(process.pid, number)
                            time.sleep(0.02)
                out, err = process.communicate(timeout=15)
                self.assertFalse(Path(observed["directory"]).exists())
                self.assertEqual(0 if scenario == "success" else 3 if scenario == "failure" else 1, process.returncode)
                self.assertIn('"observedRemainingTemporaryPaths": 0', out)
                self.assertNotIn("PRIVATE KEY", out + err)
                self.assertNotIn("client_secret", out + err)


if __name__ == "__main__":
    unittest.main()
