"""Offline finite Memories campaign sizing and admission; never provisions resources.

The existing qualifier owns lifecycle. This module supplies its capacity/configuration
boundary and the exact bound profile consumed by Memories. Configured bounds are not
measurements and a candidate, even one that fits a filesystem, is never admitted.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import hmac
import json
import os
from pathlib import Path
import re
import stat
import time

GIB = 1024 ** 3
SCHEMA = "hexalith.platform.memories-finite-campaign/v1"
PROFILE_ID = "PG-FINITE-CAMPAIGN-1"
INPUT_GROUPS = frozenset("tool toolchain runtimeImages backendImages chart values render components applications reporter source storageConfiguration workloadPlan renewalEvidence".split())
BOUNDS = {
    # Serialized records remain <= 1024 bytes. Heap/TOAST and all indexes,
    # catalogues, due entries/cursors/reminders each receive a 2 KiB bound per
    # accepted record. Measurement must include these supporting relations.
    "heapBytesPerRecord": 2048, "indexAndStateBytesPerRecord": 2048,
    "walPeakBytes": 4 * GIB, "databaseFixedBytes": GIB,
    "controlBytes": 8 * GIB, "schedulerMemberBytes": GIB,
    "applicationSpillBytes": 4 * GIB, "hostWorkspaceBytes": 18 * GIB,
    "postgresCpuMilli": 8000, "postgresMemoryBytes": 16 * GIB,
    "applicationCpuMilli": 4500, "applicationMemoryBytes": 7 * GIB,
    "controlCpuMilli": 2000, "controlMemoryBytes": 3 * GIB,
    "schedulerCpuMilli": 750, "schedulerMemoryBytes": GIB,
    "hostCpuMilli": 1000, "hostMemoryBytes": 4 * GIB,
}
INVENTORY = {
    "c1Throughput": 450000, "c1Backlog": 150000, "c1OtherBound": 50000,
    "c2Writers": 450000, "c2BacklogBound": 150000, "c2OtherBound": 50000,
    "c3Seeds": 375, "c3NewerControls": 125, "c4OtherBound": 50000,
}
MEASUREMENTS = frozenset(BOUNDS) | {"acceptedRecords", "componentOperations", "overlapMilliseconds", "backlogRecords"}
LIMITS = {"c2Seconds": 50400, "c3Seconds": 777600, "c4Seconds": 50400,
          "purgeSeconds": 900, "reclamationSeconds": 86400, "stoppingSeconds": 900,
          "writers": 2, "acceptedRecordsPerSecond": 250, "componentOperationsPerSecond": 500,
          "workloadSeconds": 1800, "backlogRecords": 150000,
          "cohortHours": [1, 24, 168], "cohortRecords": 125, "retries": 0}


class CapacityError(ValueError):
    """Bounded content-free refusal, safe to retain in an admission packet."""


def require(condition, code):
    if not condition:
        raise CapacityError("campaign-capacity-" + code)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True, allow_nan=False).encode()


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def integer(value, minimum=0):
    require(type(value) is int and minimum <= value <= 2 ** 63 - 1, "integer-invalid")
    return value


def round_up(value, unit=GIB):
    return (integer(value) + unit - 1) // unit * unit


def _size_inventory(inventory):
    """Conservative cumulative live+dead bound, without credit for early reuse.

    C1.9 and C2 each emit 450000 records; separate backlog allowances do not
    assume those identities are reused. The additional 50000-record phase caps
    must be enforced by the environment's admission/monitoring configuration.
    C3 must start with an empty live table/index. Its three 125-row seeds peak
    at 375 live rows; the later 125 controls make 500 cumulative, not 7d*rate.
    Retaining every earlier tuple as dead overbounds sequential phase overlap.
    """
    rows = sum(inventory.values())
    raw = rows * (BOUNDS["heapBytesPerRecord"] + BOUNDS["indexAndStateBytesPerRecord"])
    relation = (raw * 5 + 3) // 4  # 25% relation margin, rounded upward to bytes.
    workspace = relation  # One complete relation+index rewrite, no old 128 GiB floor.
    used = relation + workspace + BOUNDS["walPeakBytes"] + BOUNDS["databaseFixedBytes"]
    # Leave 20% of the allocated PostgreSQL filesystem free at the modeled peak;
    # round to 4 GiB allocation units. WAL includes active/retained segments;
    # max_wal_size alone is not an enforced quota.
    postgres = round_up((used * 5 + 3) // 4, 4 * GIB)
    allocations = {
        "postgres": postgres, "control": BOUNDS["controlBytes"],
        "scheduler-0": BOUNDS["schedulerMemberBytes"],
        "scheduler-1": BOUNDS["schedulerMemberBytes"],
        "scheduler-2": BOUNDS["schedulerMemberBytes"],
        "application-spill": BOUNDS["applicationSpillBytes"],
        "host-workspace": BOUNDS["hostWorkspaceBytes"],
        # A full retained relation + bounded WAL + metadata, also with 20% free.
        "backup": round_up(((relation + BOUNDS["walPeakBytes"] + GIB) * 5 + 3) // 4, 4 * GIB),
    }
    cpu = sum(v for k, v in BOUNDS.items() if k.endswith("CpuMilli"))
    memory = sum(v for k, v in BOUNDS.items() if k.endswith("MemoryBytes"))
    return {"inventory": dict(inventory), "cumulativeRecords": rows, "c3PeakLiveRecords": 375,
            "rawRelationBytes": raw, "relationWithMarginBytes": relation,
            "reclamationWorkspaceBytes": workspace, "postgresPeakUsedBytes": used,
            "allocations": allocations, "primaryBytes": sum(v for k, v in allocations.items() if k != "backup"),
            "backupBytes": allocations["backup"], "cpuMilli": cpu, "memoryBytes": memory,
            "continuousSevenDayCredit": False, "historical400GiBCredit": False}


C1_PARENTS = {
    1: (12, 16), 2: (1, 16), 3: (1, 16), 4: (1, 3), 5: (1,),
    6: (1, 4, 15), 7: (6, 20), 8: (1,), 9: (7, 8, 18),
    10: (5, 7, 18), 11: (16,), 12: (16,), 13: (9, 14, 18),
    14: (5, 10, 16), 15: (), 16: (15,), 17: (15, 16), 18: (17,),
    19: (9, 16, 20), 20: (17,), 21: (17, 20), 22: (20, 21),
    23: (*range(1, 23), 24), 24: (17, 18, 19, 21, 22), 25: tuple(range(1, 25)),
}
C1_GATES = tuple("C1." + str(n) for n in range(1, 26))
CAMPAIGN_SECONDS = 336 * 3600


def validate_renewal_schedule(schedule):
    """Finite admitted timeline, not a launcher or a fabricated renewal count.

    Round capture bounds are separately measured/source-bound below. Each root
    refresh recaptures all 25 descendants. Collection/use/technical-age policies
    remain 72/24/96h; no reauthorization moves an old capture's timestamp.
    """
    require(type(schedule) is dict and set(schedule) == {"campaignSeconds", "phases", "rounds", "protectedIntervals", "c3OperationBoundsSeconds"}, "compatible-renewal-schedule-required")
    require(schedule["campaignSeconds"] == CAMPAIGN_SECONDS, "campaign-window-invalid")
    phases = schedule["phases"]
    require(type(phases) is dict and set(phases) == {"c2", "c3", "c4"}, "phase-schedule-incomplete")
    previous = 0
    for phase, duration in (("c2", LIMITS["c2Seconds"]), ("c3", LIMITS["c3Seconds"]), ("c4", LIMITS["c4Seconds"])):
        window = phases[phase]
        require(type(window) is list and len(window) == 2, "phase-window-invalid")
        start, end = (integer(v) for v in window)
        require(previous <= start < end <= CAMPAIGN_SECONDS and end - start == duration, "phase-budget-changed")
        previous = end
    rounds = schedule["rounds"]
    require(type(rounds) is list and 2 <= len(rounds) <= 128, "finite-renewal-inventory-required")
    names, previous_end = set(), 0
    for item in rounds:
        require(type(item) is dict and set(item) == {"id", "startSeconds", "readySeconds", "collectionEndSeconds", "reviewEndSeconds", "captureBoundsSeconds"}, "renewal-round-invalid")
        name = item["id"]
        require(type(name) is str and re.fullmatch(r"[a-z][a-z0-9-]{0,31}", name) is not None and name not in names, "renewal-round-identity-invalid")
        names.add(name)
        start, ready, end, review = (integer(item[k]) for k in ("startSeconds", "readySeconds", "collectionEndSeconds", "reviewEndSeconds"))
        require(previous_end <= start < ready < end <= start + 72 * 3600 and end < review <= end + 24 * 3600 and review <= CAMPAIGN_SECONDS, "renewal-policy-window-invalid")
        require(ready - start <= 24 * 3600, "renewal-capture-review-budget-invalid")
        previous_end = ready
        bounds = item["captureBoundsSeconds"]
        require(type(bounds) is dict and set(bounds) == set(C1_GATES), "all-descendants-must-recapture")
        for gate, interval in bounds.items():
            require(type(interval) is list and len(interval) == 2, "gate-capture-bound-invalid")
            left, right = (integer(v) for v in interval)
            require(start <= left < right <= ready and right - left >= {"C1.5": 3601, "C1.9": 1800}.get(gate, 1), "gate-capture-bound-invalid")
        # The existing source validator enforces exact parents. Requiring a
        # serial total order here additionally reserves every gate's full bound.
        require(all(bounds["C1." + str(parent)][1] <= bounds["C1." + str(child)][0] for child, parents in C1_PARENTS.items() for parent in parents), "renewal-parent-recapture-order-invalid")
        ordered = sorted(bounds.values())
        require(all(a[1] <= b[0] for a, b in zip(ordered, ordered[1:])), "gate-collection-overlap")
        for phase in ("c2", "c4"):
            left, right = phases[phase]
            require(ready <= left or start >= right, "renewal-overlaps-exclusive-phase")
    # Observe/fault/purge/reclaim need current collection authority throughout
    # each phase, not merely a 96h technically recent packet at phase entry.
    for left, right in phases.values():
        cursor = left
        for item in rounds:
            if item["readySeconds"] <= cursor < item["collectionEndSeconds"]:
                cursor = item["collectionEndSeconds"]
        require(cursor >= right, "renewal-coverage-gap")
    require(any(phases["c4"][1] <= r["collectionEndSeconds"] < r["reviewEndSeconds"] == CAMPAIGN_SECONDS for r in rounds), "final-review-coverage-gap")
    protected = schedule["protectedIntervals"]
    require(type(protected) is list and len(protected) == 5, "c3-isolation-windows-required")
    expected = {"empty-and-seeds", "1h-purge", "24h-purge", "168h-purge-reclaim", "newer-control-and-physical-report"}
    require({p.get("kind") for p in protected if type(p) is dict} == expected, "c3-isolation-windows-required")
    operation_bounds = schedule["c3OperationBoundsSeconds"]
    require(type(operation_bounds) is dict and set(operation_bounds) == {"seeds", "purge", "reclamation", "controlAndReport"}, "c3-measured-operation-bounds-required")
    for name, ceiling in (("seeds", 3600), ("purge", LIMITS["purgeSeconds"]), ("reclamation", LIMITS["reclamationSeconds"]), ("controlAndReport", 3600)):
        require(integer(operation_bounds[name], 1) <= ceiling, "c3-operation-bound-exceeds-policy")
    c3start, c3end = phases["c3"]
    by_kind = {}
    for item in protected:
        require(set(item) == {"kind", "startSeconds", "endSeconds"}, "c3-isolation-window-invalid")
        start, end = integer(item["startSeconds"]), integer(item["endSeconds"])
        require(c3start <= start < end <= c3end, "c3-isolation-window-invalid")
        for round_ in rounds:
            require(round_["readySeconds"] <= start or round_["startSeconds"] >= end, "renewal-interferes-with-c3")
        by_kind[item["kind"]] = (start, end)
    seed_start, seed_end = by_kind["empty-and-seeds"]
    require(seed_start == c3start and seed_end - seed_start >= operation_bounds["seeds"], "c3-empty-boundary-invalid")
    for hours, kind in ((1, "1h-purge"), (24, "24h-purge"), (168, "168h-purge-reclaim")):
        start, end = by_kind[kind]
        require(start <= seed_start + hours * 3600 and end >= seed_end + hours * 3600 + operation_bounds["purge"] + operation_bounds["reclamation"]
                + (operation_bounds["controlAndReport"] if hours == 168 else 0), "c3-original-deadline-not-reserved")
    control_start, control_end = by_kind["newer-control-and-physical-report"]
    require(control_start <= by_kind["168h-purge-reclaim"][0] and control_end >= by_kind["168h-purge-reclaim"][1]
            and control_end - control_start < 24 * 3600, "c3-control-attribution-window-invalid")
    return schedule


def sizing(renewal_schedule=None):
    """Smallest 4GiB data allocations under the *bound* finite inventory.

    No schedule means no selected data/backup allocation. The former 24/57/16
    numbers describe a single-collection lower bound and cannot cover a campaign.
    """
    if renewal_schedule is None:
        return {"status": "blocked", "reason": "compatible-finite-renewal-plan-required",
                "fixedPrimaryNonDataBytes": 33 * GIB, "cpuMilli": 16250, "memoryBytes": 31 * GIB,
                "singleCollectionLowerBound": _size_inventory(INVENTORY)}
    schedule = validate_renewal_schedule(renewal_schedule)
    inventory = dict(INVENTORY)
    for name in ("c1Throughput", "c1Backlog", "c1OtherBound"):
        inventory[name] *= len(schedule["rounds"])
    return {"status": "schedule-bound", "renewalRounds": len(schedule["rounds"]),
            "fixedPrimaryNonDataBytes": 33 * GIB, **_size_inventory(inventory)}


def validate_renewal_evidence(profile, plan):
    """Reopen the outside-approved measured feasibility proof, never infer it.

    Retained measured per-gate upper bounds and cleanup/readback inventories are
    prerequisites. Configured time slots or assertion-only plan text supply no
    substitute. The profile's outside approval authenticates these exact bytes.
    """
    refs = profile["inputs"]["renewalEvidence"]
    require(len(refs) == 1, "renewal-measurement-required")
    proof = retained_json(refs[0])
    require(type(proof) is dict and set(proof) == {"scheduleSha256", "sourceInputsSha256", "workloadPlanSha256", "rounds", "c2Drain", "c3Readbacks"}, "renewal-measurement-required")
    require(proof["scheduleSha256"] == digest(profile["renewalSchedule"])
            and proof["sourceInputsSha256"] == digest(profile["inputs"]["source"])
            and proof["workloadPlanSha256"] == digest(plan), "renewal-evidence-scope-mismatch")
    rounds = profile["renewalSchedule"]["rounds"]
    require(type(proof["rounds"]) is list and len(proof["rounds"]) == len(rounds), "renewal-evidence-incomplete")
    commands = {c["commandId"]: c for c in plan["commandInventory"]}
    for measured, round_ in zip(proof["rounds"], rounds):
        require(type(measured) is dict and set(measured) == {"id", "gates", "cleanup"} and measured["id"] == round_["id"], "renewal-evidence-round-mismatch")
        require(type(measured["gates"]) is dict and set(measured["gates"]) == set(C1_GATES), "renewal-measured-gates-incomplete")
        for gate, value in measured["gates"].items():
            require(type(value) is dict and set(value) == {"commandSha256", "maximumElapsedSeconds", "evidence"}, "renewal-gate-measurement-invalid")
            require(value["commandSha256"] == digest(commands[round_["id"] + "/" + gate]), "renewal-command-not-bound")
            require(integer(value["maximumElapsedSeconds"], 1) <= round_["captureBoundsSeconds"][gate][1] - round_["captureBoundsSeconds"][gate][0], "renewal-timing-does-not-fit")
            receipt(value)
        cleanup = measured["cleanup"]
        require(type(cleanup) is dict and set(cleanup) == {"unrelatedLiveRecords", "unrelatedDeadRecords", "unrelatedSupportEntries", "cohortBeforeSha256", "cohortAfterSha256", "evidence"}, "renewal-cleanup-proof-required")
        require(all(cleanup[n] == 0 for n in ("unrelatedLiveRecords", "unrelatedDeadRecords", "unrelatedSupportEntries"))
                and re.fullmatch("[0-9a-f]{64}", cleanup["cohortBeforeSha256"]) is not None
                and cleanup["cohortBeforeSha256"] == cleanup["cohortAfterSha256"], "renewal-c3-state-interference")
        receipt(cleanup)
    drain = proof["c2Drain"]
    require(type(drain) is dict and set(drain) == {"maximumElapsedSeconds", "liveRecords", "deadRecords", "supportEntries", "evidence"}, "c2-drain-proof-required")
    gap = profile["renewalSchedule"]["phases"]["c3"][0] - profile["renewalSchedule"]["phases"]["c2"][1]
    require(integer(drain["maximumElapsedSeconds"], 1) <= gap and all(drain[k] == 0 for k in ("liveRecords", "deadRecords", "supportEntries")), "c2-not-cleared-before-c3")
    receipt(drain)
    readbacks = proof["c3Readbacks"]
    require(type(readbacks) is dict and set(readbacks) == {"emptyRecords", "emptyIndexEntries", "newerControlRecords", "foreignLiveRecords", "foreignDeadRecords", "dueReminderInventorySha256", "physicalAttributionSha256", "maximumOperationSeconds", "commandSourcesSha256", "evidence"}, "c3-isolation-measurement-required")
    require(readbacks["emptyRecords"] == readbacks["emptyIndexEntries"] == readbacks["foreignLiveRecords"] == readbacks["foreignDeadRecords"] == 0
            and readbacks["newerControlRecords"] == 125
            and all(re.fullmatch("[0-9a-f]{64}", readbacks[k]) is not None for k in ("dueReminderInventorySha256", "physicalAttributionSha256")), "c3-isolation-measurement-invalid")
    observed_bounds = readbacks["maximumOperationSeconds"]
    require(type(observed_bounds) is dict and set(observed_bounds) == set(profile["renewalSchedule"]["c3OperationBoundsSeconds"])
            and all(integer(observed_bounds[k], 1) <= limit for k, limit in profile["renewalSchedule"]["c3OperationBoundsSeconds"].items())
            and readbacks["commandSourcesSha256"] == digest({k: commands[k]["sourceSha256"] for k in ("C3.seeds", "C3.controls")}), "c3-operation-timing-not-measured")
    receipt(readbacks)


def candidate():
    return {"schema": SCHEMA, "profileId": PROFILE_ID, "status": "provisional",
            "target": {"clusterName": "memories-qualification-27-4-20261026",
                       "context": "kind-memories-qualification-27-4-20261026",
                       "namespace": "hexalith-memories-qualification", "nodeHa": False},
            "bounds": dict(BOUNDS), "limits": dict(LIMITS), "renewalSchedule": None, "sizing": sizing(),
            "environmentConfiguration": environment_configuration(),
            "inputs": {}, "measurements": None, "storage": None, "compute": None}


def environment_configuration(renewal_schedule=None):
    """Explicit environment-owned allocation inputs; these create no services."""
    return {
        "postgresql": {"cpuMilli": 8000, "memoryBytes": 16 * GIB, "volumeBytes": sizing(renewal_schedule).get("allocations", {}).get("postgres"), "sslMode": "verify-full"},
        "dapr": {"schedulerEnabled": True, "schedulerMembers": 3, "placementMembers": 3,
                 "schedulerVolumeBytesPerMember": GIB, "schedulerCpuMilliPerMember": 250,
                 "schedulerMemoryBytesPerMember": GIB // 3},
        "controlVolumes": {"etcdBytes": 4 * GIB, "openbaoBytes": GIB, "logsAndOtherBytes": 3 * GIB},
        "boundsRequireMonitoringAndStop": {"walPeakBytes": BOUNDS["walPeakBytes"], "phaseRecordUpperBounds": sizing(renewal_schedule).get("inventory"), "perCollectionRecordUpperBounds": {k: v for k, v in INVENTORY.items() if k.startswith("c1")}},
        "backupBytes": sizing(renewal_schedule).get("backupBytes"), "backupFailureDomain": "independent-required",
        "applicationPods": 9, "applicationSpillBytes": 4 * GIB, "hostWorkspaceBytes": 18 * GIB,
    }


def configure_application(values, graph):
    """Apply finite-campaign requests/limits to the existing maintained chart.

    This produces an input candidate only. Endpoint, credentials, infrastructure,
    measured performance and the full environment lifecycle remain prerequisites.
    Seven declared workloads with two server/telemetry replicas total nine Pods.
    Combined app+sidecar ceilings are 500m/768Mi per Pod; 7GiB includes rounding.
    """
    configured = copy.deepcopy(values)
    require(len(graph["workloads"]) == 7, "application-graph-drifted")
    configured["capacityProfile"] = PROFILE_ID
    configured["replicas"] = 1
    for workload in graph["workloads"]:
        binding = configured["bindings"][workload["identity"]]
        binding["replicas"] = 2 if workload["identity"] in {"memories/server", "memories/telemetry"} else 1
        binding["resources"] = {"requests": {"cpu": "400m", "memory": "640Mi", "ephemeral-storage": "128Mi"},
                                "limits": {"cpu": "400m", "memory": "640Mi", "ephemeral-storage": "384Mi"}}
        if workload["identity"].startswith("memories/"):
            binding["containerName"] = {"memories/server": "memories", "memories/telemetry": "lifecycle", "memories/clock": "clock"}[workload["identity"]]
            binding.setdefault("configuration", {})["AccessTelemetryLifecycle:ComponentProfileHash"] = {
                "configMapKeyRef": {"name": "access-telemetry-campaign-identity", "key": "profileSha256"}}
    return configured


def runtime_binding(profile, profile_sha256):
    """Late-bound data behind an immutable named reference, avoiding a hash cycle.

    Values/render hash the ConfigMap name/key only. The outside-approved owner
    descriptor and C0/producer readback bind its exact data after canonical
    profile calculation. It is not part of its own canonical input bytes.
    """
    require(type(profile_sha256) is str and re.fullmatch("[0-9a-f]{64}", profile_sha256) is not None, "runtime-profile-invalid")
    return {"apiVersion": "v1", "kind": "ConfigMap",
            "metadata": {"name": "access-telemetry-campaign-identity", "namespace": profile["target"]["namespace"]},
            "data": {"profileId": PROFILE_ID, "profileSha256": profile_sha256, "capacityProfileSha256": digest(profile)}}


def snapshot(path, expected=None):
    """Reopen regular non-symlink input bytes; identity covers every launch input."""
    path = Path(path)
    require(path.is_absolute() and path == path.resolve() and not path.is_symlink(), "path-invalid")
    require(path.is_file() and stat.S_ISREG(path.stat().st_mode) and path.stat().st_size <= 16 * 1024 * 1024, "file-invalid")
    raw = path.read_bytes()
    if expected is not None:
        require(type(expected) is str and re.fullmatch("[0-9a-f]{64}", expected) is not None, "digest-invalid")
        require(hashlib.sha256(raw).hexdigest() == expected, "input-drifted")
    return raw


def reference(value):
    require(type(value) is dict and set(value) == {"path", "sha256"}, "reference-invalid")
    snapshot(value["path"], value["sha256"])


def retained_json(ref):
    reference(ref)
    return json.loads(snapshot(ref["path"], ref["sha256"]))


def receipt(value):
    """The retained receipt must actually contain these claimed operands."""
    body = {k: v for k, v in value.items() if k != "evidence"}
    require(retained_json(value["evidence"]) == body, "receipt-content-mismatch")


def scope(profile):
    """Non-circular measurement scope: exact launch inputs, target and limits."""
    return digest({k: profile[k] for k in ("schema", "profileId", "target", "bounds", "limits", "renewalSchedule", "sizing", "environmentConfiguration", "inputs")})


def validate_bound(profile, *, now_ms, check_freshness=True):
    """Require measurements, complete input binding and aggregate reservation.

    The caller independently pins the outer profile digest. This function does
    not treat a packet, PVC request, artifact hash or free-space reading as an
    approval. Capacity receipts must be outside-approved as part of that plan.
    """
    require(type(profile) is dict and set(profile) == set(candidate()), "shape-invalid")
    require(profile["schema"] == SCHEMA and profile["profileId"] == PROFILE_ID and profile["status"] == "bound", "unbound")
    expected_sizing = sizing(profile["renewalSchedule"])
    require(expected_sizing.get("status") != "blocked", "compatible-renewal-schedule-required")
    require(canonical(profile["bounds"]) == canonical(BOUNDS) and canonical(profile["limits"]) == canonical(LIMITS)
            and canonical(profile["sizing"]) == canonical(expected_sizing), "formula-drifted")
    require(canonical(profile["environmentConfiguration"]) == canonical(environment_configuration(profile["renewalSchedule"])), "configuration-drifted")
    target = profile["target"]
    require(type(target) is dict and set(target) == set(candidate()["target"]), "target-invalid")
    require(target["nodeHa"] is False and target["namespace"].endswith("-qualification")
            and target["context"] == "kind-" + target["clusterName"]
            and re.fullmatch(r"[a-z0-9][a-z0-9-]{0,62}", target["clusterName"]) is not None, "target-invalid")
    inputs = profile["inputs"]
    require(type(inputs) is dict and set(inputs) == INPUT_GROUPS, "inputs-incomplete")
    paths = set()
    for group in inputs.values():
        require(type(group) is list and 1 <= len(group) <= 128, "inputs-incomplete")
        for ref in group:
            reference(ref)
            require(ref["path"] not in paths, "input-duplicated")
            paths.add(ref["path"])
    chart_root = Path(__file__).parent / "chart"
    chart_paths = {str(path.resolve()) for path in chart_root.rglob("*") if path.is_file()}
    require({ref["path"] for ref in inputs["chart"]} == chart_paths, "chart-inventory-incomplete")
    require(len(inputs["values"]) == 1, "values-ambiguous")
    values = retained_json(inputs["values"][0])
    graph = json.loads((chart_root / "reference-graph.json").read_bytes())
    configured = configure_application(values, graph)
    require(canonical(values) == canonical(configured), "application-configuration-drifted")
    require(len(inputs["toolchain"]) == len(inputs["runtimeImages"]) == len(inputs["backendImages"]) == len(inputs["workloadPlan"]) == 1, "input-group-ambiguous")
    require(retained_json(inputs["toolchain"][0]) == json.loads((Path(__file__).parent / "toolchain.json").read_bytes()), "toolchain-drifted")
    runtime = retained_json(inputs["runtimeImages"][0])
    require(type(runtime) is dict and set(runtime) == {"version", "image", "linuxAmd64Manifest"} and runtime["version"] == "1.18.7", "runtime-invalid")
    require(re.fullmatch(r"ghcr.io/dapr/daprd:1\.18\.7@sha256:[0-9a-f]{64}", runtime["image"]) is not None
            and re.fullmatch(r"sha256:[0-9a-f]{64}", runtime["linuxAmd64Manifest"]) is not None, "runtime-unpinned")
    backend = retained_json(inputs["backendImages"][0])
    require(type(backend) is dict and set(backend) == {"postgresql", "postgresqlLinuxAmd64Manifest", "openbao", "openbaoLinuxAmd64Manifest"}, "backend-incomplete")
    require(backend["postgresql"] == "docker.io/library/postgres:18.6-trixie@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722"
            and backend["postgresqlLinuxAmd64Manifest"] == "sha256:0377e72c5289ed2f98cf61b1a9c2db9eb9d300317fe14244492fbc94343b3d04"
            and backend["openbao"] == "quay.io/openbao/openbao:2.6.4@sha256:cf2340fc9a22cb9358ca0defd1f39b65673836bd23fe2bb8984a07e11fe13ef4"
            and backend["openbaoLinuxAmd64Manifest"] == "sha256:bd3e8b6b67b5c4c3fc1064cd0f86eb8063d9ed92a7af608b6408d6748e6eef64", "backend-drifted")
    plan = retained_json(inputs["workloadPlan"][0])
    require(type(plan) is dict and set(plan) == {"recordUpperBounds", "commandInventory", "allC1Gates", "sourceInputsSha256", "retryAccounting", "supportState", "recordBytes", "transactionBytes", "transactionRecords"}, "workload-plan-incomplete")
    require(plan["sourceInputsSha256"] == digest(inputs["source"]) and plan["recordUpperBounds"] == expected_sizing["inventory"]
            and plan["allC1Gates"] == ["C1." + str(n) for n in range(1, 26)]
            and plan["retryAccounting"] == "same-identities-no-additional-accepted-records"
            and plan["supportState"] == "all-heap-toast-index-actor-due-catalogue-cursor-reminder-relations"
            and plan["recordBytes"] == 1024 and plan["transactionBytes"] == 1048576 and plan["transactionRecords"] == 256, "workload-plan-out-of-bound")
    commands = plan["commandInventory"]
    expected = {"C0", *[r["id"] + "/" + g for r in profile["renewalSchedule"]["rounds"] for g in C1_GATES], "C2.writers", "C2.backlog", "C2.other", "C3.seeds", "C3.controls", "C4.other"}
    require(type(commands) is list and len(commands) == len(expected), "command-inventory-incomplete")
    totals, seen_commands = {name: 0 for name in INVENTORY}, set()
    source_hashes = {ref["sha256"] for ref in inputs["source"]}
    for command in commands:
        require(type(command) is dict and set(command) == {"commandId", "inventory", "sourceSha256", "argumentsSha256", "acceptedRecordUpperBound", "maximumInvocations"}, "command-inventory-invalid")
        name = command["commandId"]
        require(name in expected and name not in seen_commands and command["inventory"] in totals
                and command["sourceSha256"] in source_hashes
                and re.fullmatch("[0-9a-f]{64}", command["argumentsSha256"]) is not None, "command-inventory-binding-invalid")
        seen_commands.add(name)
        count = integer(command["acceptedRecordUpperBound"])
        totals[command["inventory"]] += count * integer(command["maximumInvocations"], 1)
        base_name = name.split("/")[-1]
        category = {"C1.9": "c1Throughput", "C1.10": "c1Backlog", "C2.writers": "c2Writers", "C2.backlog": "c2BacklogBound", "C2.other": "c2OtherBound", "C3.seeds": "c3Seeds", "C3.controls": "c3NewerControls", "C4.other": "c4OtherBound"}.get(base_name, "c1OtherBound")
        require(command["inventory"] == category, "command-inventory-category-changed")
        require(command["maximumInvocations"] == 1, "unbudgeted-replay")
        minimum = {"C1.9": 450000, "C1.10": 150000, "C2.writers": 450000, "C2.backlog": 150000, "C3.seeds": 375, "C3.controls": 125}.get(base_name, 0)
        require(count >= minimum, "command-workload-shrunk")
    require(all(totals[name] <= maximum for name, maximum in expected_sizing["inventory"].items()), "plan-exceeds-finite-envelope")
    for round_ in profile["renewalSchedule"]["rounds"]:
        for category in ("c1Throughput", "c1Backlog", "c1OtherBound"):
            require(sum(c["acceptedRecordUpperBound"] for c in commands if c["commandId"].startswith(round_["id"] + "/") and c["inventory"] == category) <= INVENTORY[category], "renewal-round-exceeds-envelope")
    validate_renewal_evidence(profile, plan)
    require(len(inputs["storageConfiguration"]) == 1 and retained_json(inputs["storageConfiguration"][0]) == environment_configuration(profile["renewalSchedule"]), "storage-configuration-drifted")
    measurements = profile["measurements"]
    require(type(measurements) is dict and set(measurements) == {"scopeSha256", "observedUtcMs", "values", "evidence"}, "measurements-missing")
    require(measurements["scopeSha256"] == scope(profile), "measurement-scope-drifted")
    age = integer(now_ms, 1) - integer(measurements["observedUtcMs"], 1)
    require(age >= 0 and (not check_freshness or age <= 86400000), "measurement-stale")
    receipt(measurements)
    values = measurements["values"]
    require(type(values) is dict and set(values) == MEASUREMENTS, "measurements-incomplete")
    for key, limit in BOUNDS.items():
        require(integer(values[key], 1) <= limit, "measurement-exceeds-bound")
    for key, minimum in (("acceptedRecords", 450000), ("componentOperations", 900000), ("overlapMilliseconds", 1800000), ("backlogRecords", 150000)):
        require(integer(values[key], 1) >= minimum, "workload-not-measured")
    compute = profile["compute"]
    require(type(compute) is dict and set(compute) == {"observedUtcMs", "cpuMilliAvailable", "memoryBytesAvailable", "cpuMilliCompeting", "memoryBytesCompeting", "cpuMilliReserved", "memoryBytesReserved", "evidence"}, "compute-missing")
    age = now_ms - integer(compute["observedUtcMs"], 1)
    require(age >= 0 and (not check_freshness or age <= 300000), "compute-stale")
    receipt(compute)
    for kind in ("cpuMilli", "memoryBytes"):
        demand = expected_sizing[kind]
        require(integer(compute[kind + "Reserved"]) >= demand and
                integer(compute[kind + "Available"]) - integer(compute[kind + "Competing"]) >= demand, "compute-insufficient")
    pools = profile["storage"]
    require(type(pools) is list and 2 <= len(pools) <= 8, "storage-missing")
    seen, identities, domains = set(), set(), {}
    for pool in pools:
        require(type(pool) is dict and set(pool) == {"poolId", "failureDomain", "linuxBacked", "observedUtcMs", "availableBytes", "competingBytes", "reservedBytes", "allocations", "evidence"}, "pool-invalid")
        require(type(pool["poolId"]) is str and bool(pool["poolId"]) and pool["poolId"] not in identities, "pool-duplicated")
        identities.add(pool["poolId"])
        require(type(pool["failureDomain"]) is str and bool(pool["failureDomain"]) and pool["linuxBacked"] is True, "backing-unverified")
        age = now_ms - integer(pool["observedUtcMs"], 1)
        require(age >= 0 and (not check_freshness or age <= 300000), "storage-stale")
        receipt(pool)
        names = pool["allocations"]
        require(type(names) is list and bool(names) and len(names) == len(set(names)), "allocations-invalid")
        require(not seen.intersection(names) and set(names) <= set(expected_sizing["allocations"]), "allocations-invalid")
        seen.update(names)
        demand = sum(expected_sizing["allocations"][name] for name in names)
        require(integer(pool["reservedBytes"]) >= demand and integer(pool["availableBytes"]) - integer(pool["competingBytes"]) >= demand, "storage-insufficient")
        for name in names:
            domains[name] = pool["failureDomain"]
    require(seen == set(expected_sizing["allocations"]), "allocation-missing")
    require(all(domains["backup"] != domain for name, domain in domains.items() if name != "backup"), "backup-not-independent")
    return profile


def read_bound(path, expected_sha256, *, now_ms=None, check_freshness=True):
    require(type(expected_sha256) is str and re.fullmatch("[0-9a-f]{64}", expected_sha256) is not None, "outside-approved-digest-required")
    raw = snapshot(path, expected_sha256)
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, "duplicate-json-key")
            result[key] = value
        return result
    profile = json.loads(raw.decode("utf-8"), object_pairs_hook=pairs,
                         parse_constant=lambda _: require(False, "nonfinite-json"))
    return validate_bound(profile, now_ms=int(time.time() * 1000) if now_ms is None else now_ms, check_freshness=check_freshness)


def _admission_key(key, expected):
    require(type(key) is bytes and 32 <= len(key) <= 4096
            and hashlib.sha256(key).hexdigest() == expected, "admission-key-not-approved")


def _boot():
    return Path("/proc/sys/kernel/random/boot_id").read_text().strip()


def _boottime():
    return time.clock_gettime_ns(time.CLOCK_BOOTTIME)


def issue_initial_admission(profile, *, issuer, key, key_sha256, clock_sample, clock_source):
    """Trusted issuer stamps actual fresh admission, never a supplied backdate.

    Reuses the owner protocol's signed independent UTC sample, same-boot
    CLOCK_BOOTTIME advance and 60s sample limit. The issuer/key/source identities
    must be retained outside the proposed profile. Only test fixtures supply
    controlled samples; no operational issuer is established by this function.
    """
    _admission_key(key, key_sha256)
    require(type(issuer) is str and bool(issuer), "admission-issuer-required")
    require(type(clock_sample) is dict and set(clock_sample) == {"utcMs", "boottimeNs", "bootId", "sourceId", "signature"}, "admission-clock-invalid")
    body = {k: v for k, v in clock_sample.items() if k != "signature"}
    require(hmac.compare_digest(hmac.new(key, canonical(body), hashlib.sha256).hexdigest(), clock_sample["signature"]), "admission-clock-signature")
    tick = _boottime()
    age = tick - integer(clock_sample["boottimeNs"])
    require(clock_sample["bootId"] == _boot() and clock_sample["sourceId"] == clock_source
            and 0 <= age <= 60000000000, "admission-clock-stale-or-foreign")
    now = integer(clock_sample["utcMs"], 1) + age // 1000000
    validate_bound(profile, now_ms=now, check_freshness=True)
    body = {"schema": "hexalith.platform.capacity-initial-admission/v1", "issuer": issuer,
            "profileSha256": digest(profile), "admittedUtcMs": now,
            "admittedBoottimeNs": tick, "bootId": _boot()}
    return {**body, "signature": hmac.new(key, canonical(body), hashlib.sha256).hexdigest()}


def verify_initial_admission(profile, *, admission, admission_sha256, issuer, key, key_sha256, now_ms=None):
    """Authenticate original fresh admission without moving profile/C3 origins."""
    _admission_key(key, key_sha256)
    require(type(admission) is bytes and hashlib.sha256(admission).hexdigest() == admission_sha256, "original-admission-not-approved")
    value = json.loads(admission)
    require(type(value) is dict and set(value) == {"schema", "issuer", "profileSha256", "admittedUtcMs", "admittedBoottimeNs", "bootId", "signature"}, "original-admission-invalid")
    body = {k: v for k, v in value.items() if k != "signature"}
    require(value["schema"] == "hexalith.platform.capacity-initial-admission/v1"
            and value["issuer"] == issuer and value["profileSha256"] == digest(profile)
            and hmac.compare_digest(hmac.new(key, canonical(body), hashlib.sha256).hexdigest(), value["signature"]), "original-admission-signature-or-scope")
    # Recompute strict freshness at the authenticated original stamp. A forged
    # old timestamp alone supplies neither issuer authentication nor admission.
    validate_bound(profile, now_ms=integer(value["admittedUtcMs"], 1), check_freshness=True)
    elapsed = _boottime() - integer(value["admittedBoottimeNs"])
    require(value["bootId"] == _boot() and 0 <= elapsed <= 1209600000000000, "original-admission-boot-or-budget")
    trusted_now = value["admittedUtcMs"] + elapsed // 1000000
    require(now_ms is None or abs(integer(now_ms, 1) - trusted_now) <= 1000, "original-admission-clock-regressed")
    return trusted_now


def admit_launch(profile, receipt_path, receipt_sha256, *, now_ms=None, original_admission=None, require_original=False):
    """Fresh capacity observation separate from immutable multi-day identity.

    Refreshing observed free/reserved compute/storage does not rewrite the
    canonical profile or reset any producer deadline. A fresh outside-pinned
    receipt must name that exact immutable profile. Admission is repeated before
    every producer launch; the environment must continuously enforce the bounds.
    """
    now_ms = int(time.time() * 1000) if now_ms is None else now_ms
    if original_admission is not None:
        now_ms = verify_initial_admission(profile, now_ms=now_ms, **original_admission)
    else:
        require(not require_original, "authenticated-original-admission-required")
        age = now_ms - integer(profile["measurements"]["observedUtcMs"], 1)
        require(0 <= age <= 86400000, "initial-measurement-stale")
    require(type(receipt_sha256) is str and re.fullmatch("[0-9a-f]{64}", receipt_sha256) is not None, "outside-approved-capacity-digest-required")
    current = json.loads(snapshot(receipt_path, receipt_sha256))
    require(type(current) is dict and set(current) == {"profileSha256", "compute", "storage"}
            and current["profileSha256"] == digest(profile), "current-capacity-profile-mismatch")
    def backing(pools):
        return sorted(({k: v for k, v in pool.items() if k not in {
            "availableBytes", "competingBytes", "reservedBytes", "observedUtcMs", "evidence"}}
                       for pool in pools), key=lambda item: item["poolId"])
    require(canonical(backing(current["storage"])) == canonical(backing(profile["storage"])), "backing-changed-needs-adoption")
    refreshed = copy.deepcopy(profile)
    refreshed.update(compute=current["compute"], storage=current["storage"])
    validate_bound(refreshed, now_ms=now_ms, check_freshness=False)
    for item in [current["compute"], *current["storage"]]:
        require(0 <= now_ms - integer(item["observedUtcMs"], 1) <= 300000, "current-capacity-stale")
    return {"capacityAdmitted": True, "profileSha256": digest(profile), "qualificationCredit": False}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile")
    parser.add_argument("--approved-sha256")
    parser.add_argument("--current-capacity-receipt")
    parser.add_argument("--approved-capacity-sha256")
    parser.add_argument("--original-admission")
    parser.add_argument("--approved-original-admission-sha256")
    parser.add_argument("--admission-issuer")
    parser.add_argument("--admission-key")
    parser.add_argument("--approved-admission-key-sha256")
    args = parser.parse_args(argv)
    if not args.profile:
        print(json.dumps(candidate(), indent=2, sort_keys=True))
        return 0
    try:
        require(args.approved_sha256 is not None, "outside-approved-digest-required")
        profile = read_bound(args.profile, args.approved_sha256,
                             check_freshness=args.current_capacity_receipt is None)
        if args.current_capacity_receipt is not None:
            require(args.approved_capacity_sha256 is not None, "outside-approved-capacity-digest-required")
            original = None
            if args.original_admission is not None:
                require(args.admission_issuer and args.approved_original_admission_sha256 and args.admission_key and args.approved_admission_key_sha256, "original-admission-anchors-required")
                key_path = Path(args.admission_key)
                require(key_path.stat().st_uid == os.geteuid() and stat.S_IMODE(key_path.stat().st_mode) == 0o600
                        and stat.S_IMODE(key_path.parent.stat().st_mode) == 0o700, "admission-key-not-private")
                original = dict(admission=snapshot(args.original_admission, args.approved_original_admission_sha256),
                                admission_sha256=args.approved_original_admission_sha256, issuer=args.admission_issuer,
                                key=snapshot(args.admission_key, args.approved_admission_key_sha256), key_sha256=args.approved_admission_key_sha256)
            result = admit_launch(profile, args.current_capacity_receipt, args.approved_capacity_sha256, original_admission=original)
        else:
            result = {"capacityAdmitted": True, "profileSha256": digest(profile), "qualificationCredit": False}
        print(json.dumps(result))
        return 0
    except (CapacityError, OSError, ValueError, TypeError, KeyError, AttributeError):
        print('{"capacityAdmitted":false,"reason":"campaign-capacity-refused"}')
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
