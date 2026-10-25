"""Offline fixtures exercise capacity admission; no fixture is operational evidence."""
from contextlib import redirect_stdout
from copy import deepcopy
import hashlib
import hmac
import io
import json
from pathlib import Path
import tempfile
import subprocess
import sys
import time
import unittest
from unittest.mock import patch

import campaign_capacity as capacity


def initial_admission_fixture(profile, now):
    """Controlled authenticated issuer/UTC fixture, never an operational receipt."""
    key = b"outside-approved-capacity-fixture-key-32-bytes"
    tick = capacity._boottime()
    body = dict(utcMs=now, boottimeNs=tick, bootId=capacity._boot(), sourceId="fixture-independent-utc")
    sample = {**body, "signature": hmac.new(key, capacity.canonical(body), hashlib.sha256).hexdigest()}
    stamped = capacity.issue_initial_admission(profile, issuer="fixture-platform-admitter", key=key,
        key_sha256=hashlib.sha256(key).hexdigest(), clock_sample=sample, clock_source=body["sourceId"])
    raw = capacity.canonical(stamped)
    return dict(admission=raw, admission_sha256=hashlib.sha256(raw).hexdigest(),
                issuer="fixture-platform-admitter", key=key, key_sha256=hashlib.sha256(key).hexdigest())


def renewal_schedule_fixture():
    """Invented timing only for unit tests; never an operational proposal."""
    hour = 3600
    rounds = []
    for index, start in enumerate((0, 30 * hour, 92 * hour, 150 * hour, 214 * hour, 280 * hour)):
        bounds, pending, done, cursor = {}, set(range(1, 26)), set(), start
        while pending:
            gate = next(n for n in sorted(pending) if set(capacity.C1_PARENTS[n]) <= done)
            duration = {5: 3602, 9: 1802}.get(gate, 2)
            bounds['C1.' + str(gate)] = [cursor, cursor + duration]
            cursor += duration + 1; pending.remove(gate); done.add(gate)
        end = start + 72 * hour if index < 5 else 1140300
        rounds.append(dict(id=f'round-{index}', startSeconds=start, readySeconds=start + 4 * hour,
                           collectionEndSeconds=end, reviewEndSeconds=min(end + 24 * hour, capacity.CAMPAIGN_SECONDS), captureBoundsSeconds=bounds))
    start = 311400  # 86.5h; fake C2 draining reserve, never a measured value.
    return dict(campaignSeconds=capacity.CAMPAIGN_SECONDS,
        c3OperationBoundsSeconds=dict(seeds=60, purge=900, reclamation=300, controlAndReport=600),
        phases=dict(c2=[48 * hour, 62 * hour], c3=[start, start + 216 * hour], c4=[start + 216 * hour, start + 230 * hour]),
        rounds=rounds, protectedIntervals=[
            dict(kind='empty-and-seeds', startSeconds=start, endSeconds=start + 60),
            dict(kind='1h-purge', startSeconds=start + hour, endSeconds=start + hour + 1260),
            dict(kind='24h-purge', startSeconds=start + 24 * hour, endSeconds=start + 24 * hour + 1260),
            dict(kind='168h-purge-reclaim', startSeconds=start + 168 * hour, endSeconds=start + 168 * hour + 1860),
            dict(kind='newer-control-and-physical-report', startSeconds=start + 168 * hour, endSeconds=start + 168 * hour + 1860)])


def renewal_evidence_fixture(profile, plan, write):
    """Simulated retained receipts; no deployed timing or isolation evidence."""
    commands = {c['commandId']: c for c in plan['commandInventory']}
    def received(name, body):
        return {**body, 'evidence': write(name, body)}
    rounds = []
    for round_ in profile['renewalSchedule']['rounds']:
        gates = {}
        for gate, window in round_['captureBoundsSeconds'].items():
            gates[gate] = received(round_['id'] + '-' + gate + '.json',
                dict(commandSha256=capacity.digest(commands[round_['id'] + '/' + gate]), maximumElapsedSeconds=window[1] - window[0]))
        cleanup = received(round_['id'] + '-cleanup.json', dict(unrelatedLiveRecords=0, unrelatedDeadRecords=0, unrelatedSupportEntries=0,
            cohortBeforeSha256='a' * 64, cohortAfterSha256='a' * 64))
        rounds.append(dict(id=round_['id'], gates=gates, cleanup=cleanup))
    return dict(scheduleSha256=capacity.digest(profile['renewalSchedule']), sourceInputsSha256=capacity.digest(profile['inputs']['source']), workloadPlanSha256=capacity.digest(plan), rounds=rounds,
        c2Drain=received('c2-drain.json', dict(maximumElapsedSeconds=88200, liveRecords=0, deadRecords=0, supportEntries=0)),
        c3Readbacks=received('c3-isolation.json', dict(emptyRecords=0, emptyIndexEntries=0, newerControlRecords=125, foreignLiveRecords=0, foreignDeadRecords=0,
            dueReminderInventorySha256='b' * 64, physicalAttributionSha256='c' * 64, maximumOperationSeconds=dict(profile['renewalSchedule']['c3OperationBoundsSeconds']), commandSourcesSha256=capacity.digest({k: commands[k]['sourceSha256'] for k in ('C3.seeds', 'C3.controls')}))))


def bound_fixture(directory, *, now_ms=None):
    """Actual temporary input/receipt files, simulated measurements only."""
    now = int(time.time() * 1000) if now_ms is None else now_ms
    directory = Path(directory).resolve()
    profile = capacity.candidate()
    profile["status"] = "bound"
    profile["renewalSchedule"] = renewal_schedule_fixture()
    profile["sizing"] = capacity.sizing(profile["renewalSchedule"])
    profile["environmentConfiguration"] = capacity.environment_configuration(profile["renewalSchedule"])

    def write(name, value):
        raw = value if isinstance(value, bytes) else capacity.canonical(value)
        path = directory / name
        path.write_bytes(raw)
        return {"path": str(path), "sha256": hashlib.sha256(raw).hexdigest()}

    for name in capacity.INPUT_GROUPS:
        profile["inputs"][name] = [write(name + ".json", {"fixture": name})]
    from test_chart import fixture_values, graph
    profile["inputs"]["values"] = [write("values.json", capacity.configure_application(fixture_values(), graph()))]
    profile["inputs"]["chart"] = [{"path": str(path.resolve()), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                                    for path in sorted((Path(capacity.__file__).parent / "chart").rglob("*")) if path.is_file()]
    profile["inputs"]["toolchain"] = [write("toolchain.json", json.loads((Path(capacity.__file__).parent / "toolchain.json").read_bytes()))]
    profile["inputs"]["runtimeImages"] = [write("runtimeImages.json", {"version": "1.18.7", "image": "ghcr.io/dapr/daprd:1.18.7@sha256:" + "a" * 64, "linuxAmd64Manifest": "sha256:" + "b" * 64})]
    profile["inputs"]["backendImages"] = [write("backendImages.json", {
        "postgresql": "docker.io/library/postgres:18.6-trixie@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722",
        "postgresqlLinuxAmd64Manifest": "sha256:0377e72c5289ed2f98cf61b1a9c2db9eb9d300317fe14244492fbc94343b3d04",
        "openbao": "quay.io/openbao/openbao:2.6.4@sha256:cf2340fc9a22cb9358ca0defd1f39b65673836bd23fe2bb8984a07e11fe13ef4",
        "openbaoLinuxAmd64Manifest": "sha256:bd3e8b6b67b5c4c3fc1064cd0f86eb8063d9ed92a7af608b6408d6748e6eef64"})]
    module_path = Path(capacity.__file__).resolve()
    profile["inputs"]["source"].append({"path": str(module_path), "sha256": hashlib.sha256(module_path.read_bytes()).hexdigest()})
    owner_source = Path(__file__).resolve().parents[4] / "tools/access_telemetry_c1_approval_policy.py"
    if owner_source.is_file():
        profile["inputs"]["source"].append({"path": str(owner_source), "sha256": hashlib.sha256(owner_source.read_bytes()).hexdigest()})
    # Fixtures model a complete reviewed per-command upper-bound inventory.
    source_sha = profile["inputs"]["source"][-1]["sha256"]
    commands = []
    for name in ["C0", *[r["id"] + "/" + g for r in profile["renewalSchedule"]["rounds"] for g in capacity.C1_GATES], "C2.writers", "C2.backlog", "C2.other", "C3.seeds", "C3.controls", "C4.other"]:
        base_name = name.split("/")[-1]
        category = {"C1.9": "c1Throughput", "C1.10": "c1Backlog", "C2.writers": "c2Writers", "C2.backlog": "c2BacklogBound", "C2.other": "c2OtherBound", "C3.seeds": "c3Seeds", "C3.controls": "c3NewerControls", "C4.other": "c4OtherBound"}.get(base_name, "c1OtherBound")
        count = capacity.INVENTORY[category] if category != "c1OtherBound" else 256
        arguments = {"gate": base_name, "target_sha256": "late-bound-target", "session_id": "late-bound-session"}
        commands.append({"commandId": name, "inventory": category, "sourceSha256": source_sha,
                         "argumentsSha256": capacity.digest(arguments), "acceptedRecordUpperBound": count, "maximumInvocations": 1})
    profile["inputs"]["workloadPlan"] = [write("workloadPlan.json", {
        "recordUpperBounds": profile["sizing"]["inventory"], "commandInventory": commands, "allC1Gates": ["C1." + str(n) for n in range(1, 26)],
        "sourceInputsSha256": capacity.digest(profile["inputs"]["source"]),
        "retryAccounting": "same-identities-no-additional-accepted-records",
        "supportState": "all-heap-toast-index-actor-due-catalogue-cursor-reminder-relations",
        "recordBytes": 1024, "transactionBytes": 1048576, "transactionRecords": 256})]
    profile["inputs"]["storageConfiguration"] = [write("storageConfiguration.json", capacity.environment_configuration(profile["renewalSchedule"]))]
    plan = json.loads(Path(profile["inputs"]["workloadPlan"][0]["path"]).read_bytes())
    profile["inputs"]["renewalEvidence"] = [write("renewalEvidence.json", renewal_evidence_fixture(profile, plan, write))]
    measurements = {"scopeSha256": capacity.scope(profile), "observedUtcMs": now,
                    "values": {**capacity.BOUNDS, "acceptedRecords": 450000, "componentOperations": 900000,
                               "overlapMilliseconds": 1800000, "backlogRecords": 150000}}
    measurements["evidence"] = write("measured.json", measurements)
    profile["measurements"] = measurements
    compute = {"observedUtcMs": now, "cpuMilliAvailable": 20000, "memoryBytesAvailable": 40 * capacity.GIB,
               "cpuMilliCompeting": 0, "memoryBytesCompeting": 0,
               "cpuMilliReserved": profile["sizing"]["cpuMilli"], "memoryBytesReserved": profile["sizing"]["memoryBytes"]}
    compute["evidence"] = write("compute.json", compute)
    profile["compute"] = compute
    profile["storage"] = []
    for name, allocations in (("primary", [name for name in profile["sizing"]["allocations"] if name != "backup"]), ("backup", ["backup"])):
        demand = sum(profile["sizing"]["allocations"][key] for key in allocations)
        pool = {"poolId": "fixture-" + name, "failureDomain": name, "linuxBacked": True, "observedUtcMs": now,
                "availableBytes": demand, "competingBytes": 0, "reservedBytes": demand, "allocations": allocations}
        pool["evidence"] = write(name + "-reservation.json", pool)
        profile["storage"].append(pool)
    return profile, now, write


class CampaignCapacityTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.profile, self.now, self.write = bound_fixture(self.temporary.name)

    def receipt(self, item):
        item["evidence"] = self.write("changed-receipt.json", {k: v for k, v in item.items() if k != "evidence"})

    def test_finite_inventory_and_allocation_math(self):
        self.assertEqual("blocked", capacity.sizing()["status"])
        result = capacity.sizing(self.profile["renewalSchedule"])
        self.assertEqual(4600500, result["cumulativeRecords"])
        self.assertEqual(375, result["c3PeakLiveRecords"])
        self.assertEqual(18843648000, result["rawRelationBytes"])
        self.assertEqual(23554560000, result["relationWithMarginBytes"])
        self.assertEqual(result["relationWithMarginBytes"], result["reclamationWorkspaceBytes"])
        self.assertEqual(64 * capacity.GIB, result["allocations"]["postgres"])
        self.assertEqual(97 * capacity.GIB, result["primaryBytes"])
        self.assertEqual(36 * capacity.GIB, result["backupBytes"])
        self.assertEqual((16250, 31 * capacity.GIB), (result["cpuMilli"], result["memoryBytes"]))
        self.assertEqual([1, 24, 168], capacity.LIMITS["cohortHours"])
        self.assertFalse(result["continuousSevenDayCredit"])
        self.assertFalse(result["historical400GiBCredit"])

    def test_missing_or_incompatible_renewal_schedule_refuses(self):
        self.assertIsNone(capacity.candidate()["environmentConfiguration"]["postgresql"]["volumeBytes"])
        self.assertNotIn("allocations", capacity.sizing())
        for mutate in (
            lambda p: p.update(renewalSchedule=None),
            lambda p: p["renewalSchedule"]["rounds"].pop(2),
            lambda p: p["renewalSchedule"]["rounds"][1].update(collectionEndSeconds=336 * 3600),
            lambda p: p["renewalSchedule"]["rounds"][2]["captureBoundsSeconds"].pop("C1.15"),
            lambda p: p["renewalSchedule"]["protectedIntervals"][3].update(endSeconds=1000000),
        ):
            profile = deepcopy(self.profile); mutate(profile)
            with self.assertRaises(capacity.CapacityError):
                capacity.validate_bound(profile, now_ms=self.now)

    def test_full_renewal_inventory_and_retained_feasibility_measurements_are_required(self):
        plan_ref = self.profile["inputs"]["workloadPlan"][0]
        plan = json.loads(Path(plan_ref["path"]).read_bytes())
        self.assertEqual(150, sum("/C1." in c["commandId"] for c in plan["commandInventory"]))
        commands = deepcopy(plan["commandInventory"])
        for mutate in (lambda rows: rows.pop(30), lambda rows: rows[1].update(maximumInvocations=2),
                       lambda rows: rows[1].update(acceptedRecordUpperBound=50001)):
            altered = deepcopy(commands); mutate(altered)
            changed = dict(plan, commandInventory=altered)
            self.profile["inputs"]["workloadPlan"] = [self.write("workloadPlan.json", changed)]
            with self.assertRaises(capacity.CapacityError):
                capacity.validate_bound(self.profile, now_ms=self.now)
        self.profile["inputs"]["workloadPlan"] = [self.write("workloadPlan.json", plan)]
        proof_ref = self.profile["inputs"]["renewalEvidence"][0]
        proof = json.loads(Path(proof_ref["path"]).read_bytes())
        for mutate in (lambda p: p["rounds"][2]["gates"]["C1.9"].update(maximumElapsedSeconds=99999),
                       lambda p: p["rounds"][2]["cleanup"].update(unrelatedDeadRecords=1),
                       lambda p: p["c2Drain"].update(maximumElapsedSeconds=999999),
                       lambda p: p["c3Readbacks"].update(newerControlRecords=126)):
            changed = deepcopy(proof); mutate(changed)
            self.profile["inputs"]["renewalEvidence"] = [self.write("renewalEvidence.json", changed)]
            with self.assertRaises(capacity.CapacityError):
                capacity.validate_bound(self.profile, now_ms=self.now)

    def test_complete_actual_file_binding_admits_only_capacity(self):
        path = self.write("profile.json", self.profile)
        actual = capacity.read_bound(path["path"], path["sha256"], now_ms=self.now)
        self.assertEqual(self.profile, actual)

    def test_candidate_or_incomplete_binding_never_admits(self):
        for field in ("measurements", "storage", "compute", "inputs"):
            candidate = deepcopy(self.profile); candidate[field] = None
            with self.subTest(field=field), self.assertRaises(capacity.CapacityError):
                capacity.validate_bound(candidate, now_ms=self.now)
        with self.assertRaises(capacity.CapacityError):
            capacity.validate_bound(capacity.candidate(), now_ms=self.now)

    def test_exact_input_drift_or_profile_tampering_refuses(self):
        path = self.write("profile.json", self.profile)
        Path(path["path"]).write_bytes(b'{}')
        with self.assertRaises(capacity.CapacityError):
            capacity.read_bound(path["path"], path["sha256"], now_ms=self.now)
        self.profile["limits"]["c3Seconds"] -= 1
        with self.assertRaises(capacity.CapacityError):
            capacity.validate_bound(self.profile, now_ms=self.now)

    def test_measurement_scope_and_retained_receipt_bytes_are_required(self):
        self.profile["measurements"]["values"]["walPeakBytes"] -= 1
        with self.assertRaisesRegex(capacity.CapacityError, "receipt-content"):
            capacity.validate_bound(self.profile, now_ms=self.now)
        self.receipt(self.profile["measurements"])
        capacity.validate_bound(self.profile, now_ms=self.now)
        self.profile["measurements"]["scopeSha256"] = "c" * 64
        with self.assertRaisesRegex(capacity.CapacityError, "scope"):
            capacity.validate_bound(self.profile, now_ms=self.now)

    def test_insufficient_cpu_memory_or_competing_demands_refuse(self):
        original = deepcopy(self.profile["compute"])
        for field, value in (("cpuMilliAvailable", 16249), ("memoryBytesReserved", 30 * capacity.GIB), ("cpuMilliCompeting", 4000), ("memoryBytesCompeting", 10 * capacity.GIB)):
            self.profile["compute"] = deepcopy(original)
            self.profile["compute"][field] = value; self.receipt(self.profile["compute"])
            with self.subTest(field=field), self.assertRaises(capacity.CapacityError):
                capacity.validate_bound(self.profile, now_ms=self.now)

    def test_aggregate_same_pool_allocation_reservation_and_competing_bytes(self):
        primary = self.profile["storage"][0]
        self.assertEqual(self.profile["sizing"]["primaryBytes"], primary["reservedBytes"])
        primary["reservedBytes"] = 24 * capacity.GIB; self.receipt(primary)
        with self.assertRaisesRegex(capacity.CapacityError, "storage-insufficient"):
            capacity.validate_bound(self.profile, now_ms=self.now)
        primary["reservedBytes"] = self.profile["sizing"]["primaryBytes"]; primary["competingBytes"] = 1; self.receipt(primary)
        with self.assertRaisesRegex(capacity.CapacityError, "storage-insufficient"):
            capacity.validate_bound(self.profile, now_ms=self.now)

    def test_backup_same_failure_domain_duplicate_pool_or_missing_allocation_refuses(self):
        backup = self.profile["storage"][1]
        backup["failureDomain"] = "primary"; self.receipt(backup)
        with self.assertRaisesRegex(capacity.CapacityError, "independent"):
            capacity.validate_bound(self.profile, now_ms=self.now)
        backup["failureDomain"] = "backup"; backup["poolId"] = "fixture-primary"; self.receipt(backup)
        with self.assertRaisesRegex(capacity.CapacityError, "duplicated"):
            capacity.validate_bound(self.profile, now_ms=self.now)
        backup["poolId"] = "fixture-backup"; backup["allocations"] = []; self.receipt(backup)
        with self.assertRaises(capacity.CapacityError):
            capacity.validate_bound(self.profile, now_ms=self.now)

    def test_stale_measurements_and_unreserved_or_nonlinux_backing_refuse(self):
        with self.assertRaisesRegex(capacity.CapacityError, "compute-stale"):
            capacity.validate_bound(self.profile, now_ms=self.now + 300001)
        self.profile["storage"][0]["linuxBacked"] = False
        with self.assertRaisesRegex(capacity.CapacityError, "backing"):
            capacity.validate_bound(self.profile, now_ms=self.now)

    def test_measured_amplification_and_full_workload_cannot_exceed_or_shrink_bound(self):
        measurements = self.profile["measurements"]
        for key, value in (("heapBytesPerRecord", 2049), ("componentOperations", 899999), ("backlogRecords", 149999), ("overlapMilliseconds", 1799999)):
            saved = measurements["values"][key]
            measurements["values"][key] = value; self.receipt(measurements)
            with self.subTest(key=key), self.assertRaises(capacity.CapacityError):
                capacity.validate_bound(self.profile, now_ms=self.now)
            measurements["values"][key] = saved

    def test_fresh_launch_receipt_does_not_reset_immutable_profile(self):
        before = capacity.digest(self.profile)
        original = initial_admission_fixture(self.profile, self.now)
        current = {"profileSha256": before, "compute": deepcopy(self.profile["compute"]), "storage": deepcopy(self.profile["storage"])}
        later = self.now + 3 * 86400000
        for item in [current["compute"], *current["storage"]]:
            item["observedUtcMs"] = later
            item["evidence"] = self.write("renew-" + str(len(json.dumps(item))) + str(item.get("poolId", "compute")) + ".json", {k: v for k, v in item.items() if k != "evidence"})
        receipt = self.write("current.json", current)
        admitted = json.loads(original["admission"])
        with patch.object(capacity, "_boottime", return_value=admitted["admittedBoottimeNs"] + 3 * 86400000000000):
            result = capacity.admit_launch(self.profile, receipt["path"], receipt["sha256"], now_ms=later, original_admission=original)
        self.assertTrue(result["capacityAdmitted"])
        self.assertEqual(before, result["profileSha256"])
        self.assertEqual(before, capacity.digest(self.profile))
        with patch.object(capacity, "_boottime", return_value=admitted["admittedBoottimeNs"] + 3 * 86400000000000 + 300001000000):
            with self.assertRaisesRegex(capacity.CapacityError, "stale"):
                capacity.admit_launch(self.profile, receipt["path"], receipt["sha256"], now_ms=later + 300001, original_admission=original)

    def test_new_stale_profile_and_self_declared_admission_cannot_authorize_first_use(self):
        old = self.profile["measurements"]
        old["observedUtcMs"] -= 48 * 3600000; self.receipt(old)
        ref = self.write("stale-profile.json", self.profile)
        current = self.write("stale-current.json", dict(profileSha256=capacity.digest(self.profile), compute=self.profile["compute"], storage=self.profile["storage"]))
        with self.assertRaises(capacity.CapacityError):
            capacity.admit_launch(self.profile, current["path"], current["sha256"], now_ms=self.now)
        with self.assertRaises(capacity.CapacityError):
            initial_admission_fixture(self.profile, self.now)
        with redirect_stdout(io.StringIO()):
            self.assertEqual(1, capacity.main(["--profile", ref["path"], "--approved-sha256", ref["sha256"],
                "--current-capacity-receipt", current["path"], "--approved-capacity-sha256", current["sha256"]]))

    def test_supported_offline_cli_forwards_outside_original_admission_anchors(self):
        original = initial_admission_fixture(self.profile, self.now)
        outer = self.write("cli-profile.json", self.profile)
        initial = self.write("cli-original.json", original["admission"])
        key = self.write("cli-admission.key", original["key"]); Path(key["path"]).chmod(0o600)
        current = self.write("cli-current.json", dict(profileSha256=capacity.digest(self.profile), compute=self.profile["compute"], storage=self.profile["storage"]))
        command = [sys.executable, "-B", str(Path(capacity.__file__).with_name("qualify.py")), "--campaign-capacity",
            "--campaign-profile", outer["path"], "--approved-campaign-sha256", outer["sha256"],
            "--current-capacity-receipt", current["path"], "--approved-capacity-sha256", current["sha256"],
            "--original-admission", initial["path"], "--approved-original-admission-sha256", initial["sha256"],
            "--admission-issuer", original["issuer"], "--admission-key", key["path"], "--approved-admission-key-sha256", key["sha256"]]
        result = subprocess.run(command, capture_output=True, text=True, timeout=10)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(json.loads(result.stdout)["capacityAdmitted"])
        command[command.index("--admission-issuer") + 1] = "foreign-issuer"
        result = subprocess.run(command, capture_output=True, text=True, timeout=10)
        self.assertEqual(1, result.returncode)
        self.assertFalse(json.loads(result.stdout)["capacityAdmitted"])

    def test_refresh_cannot_replace_bound_pool_domain_or_allocation_ownership(self):
        for changed in ("poolId", "failureDomain", "allocations"):
            current = dict(profileSha256=capacity.digest(self.profile), compute=deepcopy(self.profile["compute"]), storage=deepcopy(self.profile["storage"]))
            if changed == "allocations":
                current["storage"][0][changed], current["storage"][1][changed] = current["storage"][1][changed], current["storage"][0][changed]
            else:
                current["storage"][0][changed] = "foreign-backing"
            for pool in current["storage"]:
                self.receipt(pool)
            ref = self.write("foreign-refresh.json", current)
            with self.subTest(changed=changed), self.assertRaisesRegex(capacity.CapacityError, "backing-changed"):
                capacity.admit_launch(self.profile, ref["path"], ref["sha256"], now_ms=self.now)

    def test_original_admission_requires_approved_issuer_key_bytes_and_same_boot(self):
        original = initial_admission_fixture(self.profile, self.now)
        for changed in (dict(issuer="foreign"), dict(admission_sha256="a" * 64), dict(key=b"x" * 32)):
            with self.subTest(changed=tuple(changed)), self.assertRaises(capacity.CapacityError):
                capacity.verify_initial_admission(self.profile, **{**original, **changed}, now_ms=self.now)
        with patch.object(capacity, "_boot", return_value="different-boot"), self.assertRaises(capacity.CapacityError):
            capacity.verify_initial_admission(self.profile, **original, now_ms=self.now)

    def test_chart_configuration_has_explicit_resources_and_nine_pods(self):
        graph = json.loads((Path(capacity.__file__).parent / "chart/reference-graph.json").read_bytes())
        values = {"bindings": {w["identity"]: {} for w in graph["workloads"]}}
        configured = capacity.configure_application(values, graph)
        self.assertEqual(9, sum(b["replicas"] for b in configured["bindings"].values()))
        self.assertEqual("400m", configured["bindings"]["memories/server"]["resources"]["limits"]["cpu"])
        self.assertEqual({}, values["bindings"]["memories/server"])


if __name__ == "__main__":
    unittest.main()
