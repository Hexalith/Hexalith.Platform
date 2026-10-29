#!/usr/bin/env python3
"""Prepare detached public pins after committing the immutable candidate; never installs or accepts it."""
import argparse
import hashlib
import json
import shlex
import subprocess
import time
from pathlib import Path

import qualify as q
from cases import plan_digest
import detached_packet_output as packet_output
from packet_snapshot import snapshot_for


def prepare(root, configuration, output, eventstore="../eventstore", artifacts="/tmp/ext-secrets-artifacts", evidence="/tmp/ext-secrets-evidence", register=None, coordination_context=None):
    root, configuration, output = Path(root).resolve(), Path(configuration).absolute(), Path(output).resolve()
    if output.is_relative_to(root):
        raise ValueError("detached output must be outside candidate checkout")
    if output.exists():
        raise ValueError("fresh detached output directory required")
    output.mkdir(parents=True)
    packet_output.report(output / "blocked-inputs.json", {"packetPrepared": False, "completeReplacementPacket": False, "qualificationEffects": 0, "dependencyAvailable": False, "missingInputs": ["DetachedPacketOutputIncomplete"]})
    try:
        configuration_bytes = q.bounded_public_file(configuration, 1024 * 1024)
        config = q.parse_public_configuration(configuration_bytes)
    except (q.Unavailable, OSError, ValueError, TypeError) as error:
        inputs = error.inputs if isinstance(error,q.Unavailable) else ["PublicRuntimeConfiguration"]
        report = {"code": "DependencyNotAvailable", "dependency": "EXT-SECRETS-1", "qualificationEffects": 0,
                  "dependencyAvailable": False, "missingInputs": inputs, "packetPrepared": False}
        packet_output.report(output / "blocked-inputs.json", report)
        return report
    # Detached bytes are immutable installed input. Relocation cannot rewrite their semantic anchor locators.
    relative_anchors = []
    for role in ("executor", "observer", "enrollment"):
        anchor = q.get(config, f"qualification.{role}.publicAnchorFile")
        if isinstance(anchor, str) and not Path(anchor).is_absolute():
            relative_anchors.append(f"qualification.{role}.absoluteInstalledPublicAnchorLocator")
    missing = relative_anchors + [name for name in q.REQUIRED if not isinstance(q.get(config, name), str) or not q.get(config, name)]
    missing.extend(q.qualification_prerequisite_inputs(config))
    if type(q.get(config, "runtime.callTimeout")) is not int or q.get(config, "runtime.callTimeout") not in range(1, 31):
        missing.append("runtime.callTimeout")
    if config.get("schema") != q.PROTOCOL:
        missing.append("RuntimeConfigurationProtocolVersion")
    if q.get(config, "runtime.host") != "192.168.1.30":
        missing.append("AcceptedHost192.168.1.30")
    if q.get(config, "runtime.timing") != {"L": 300, "S": 30, "O": 600, "H": 86400, "R": 604800}:
        missing.append("AcceptedEnvelopeTiming")
    if q.get(config, "runtime.decisionKeysOutsideCustody") is not True:
        missing.append("IndependentDecisionKeyCustody")
    if q.get(config, "runtime.overlappingExportDisposition") != "Open":
        missing.append("PreservedOpenExportDisposition")
    if q.get(config, "runtime.tenantA") == q.get(config, "runtime.tenantB"):
        missing.append("DistinctEnrolledTenants")
    try:
        if not q.utc_epoch(q.get(config, "runtime.profileNotBefore")) <= time.time() < q.utc_epoch(q.get(config, "runtime.profileValidUntil")):
            missing.append("CurrentProfileValidity")
    except (ValueError, TypeError):
        missing.append("CurrentProfileValidity")
    head = None
    candidate_ready = False
    try:
        head = q.git(root, "rev-parse", "HEAD")
        if not q.COMMIT.fullmatch(head):
            head = None
            missing.append("ExactImmutableCandidateCheckout")
        else:
            candidate_ready = True
            if q.git(root, "status", "--porcelain"):
                missing.append("CleanImmutableCandidateCheckout")
                candidate_ready = False
    except FileNotFoundError:
        missing.append("CandidateGitExecutable")
        candidate_ready = False
    except subprocess.TimeoutExpired:
        missing.append("BoundedCandidateGitInspection")
        candidate_ready = False
    except (OSError, subprocess.SubprocessError):
        missing.append("ExactImmutableCandidateCheckout")
        candidate_ready = False
    if q.get(config, "runtime.acceptedReplacementCommit") != head or head is None:
        missing.append("runtime.acceptedReplacementCommit")
    anchors, decoded = {}, []
    for role in ("executor", "observer", "enrollment"):
        try:
            locator = q.get(config, f"qualification.{role}.publicAnchorFile")
            if not isinstance(locator, str) or not Path(locator).is_absolute():
                raise ValueError("absolute original installed public anchor locator required")
            data = q.read_public_anchor(locator)
            decoded.append(q.public_key_identity(data))
            anchors[f"{role}AnchorSha256"] = hashlib.sha256(data).hexdigest()
        except (OSError, TypeError, ValueError, ImportError):
            anchors[f"{role}AnchorSha256"] = None
            missing.append(f"qualification.{role}.installedP256PublicAnchor")
    if len(decoded) != 3 or len(set(decoded)) != 3:
        missing.append("IndependentInstalledQualificationAuthorities")
    for field in ("appId", "identity"):
        values = [q.get(config, f"qualification.{role}.{field}") for role in ("executor", "observer", "enrollment")]
        if not all(isinstance(v, str) and q.IDENTITY.fullmatch(v) for v in values) or len(set(values)) != 3:
            missing.append("IndependentInstalledQualificationAuthorities")
    from coordinated_packets import admission
    installation_missing, installed_hashes, coordinated = admission(coordination_context, register, head, candidate_ready)
    missing.extend(installation_missing)
    if not installation_missing and hashlib.sha256(configuration_bytes).hexdigest() != installed_hashes.get("custodyConfigurationSha256"):
        missing.append("ExactInstalledConfigurationBytes")
    if not installation_missing and any(anchors.get(name) != installed_hashes.get(name) for name in anchors):
        missing.append("ExactInstalledQualificationTrustBytes")
    cohort = []
    register_binding = None
    if register is not None:
        try:
            register_bytes = snapshot_for(coordinated).retained(register) if not installation_missing else q.bounded_public_file(register, 1024 * 1024)
            cohort = q.cohort_prerequisite_inputs(register, head, register_text=register_bytes.decode("utf-8"))
            register_binding = {"path": str(Path(register).resolve()),
                                "sha256": hashlib.sha256(register_bytes).hexdigest()}
        except (OSError, ValueError, TypeError):
            cohort = ["AuthoritativeCohortPrerequisites"]
        missing.extend(cohort)
    if not installation_missing:
        missing = [name for name in missing if name != "OQ-5.4-1.IndependentCurrentCohortBindingAndOwners"]
    binding = {"configurationRawSha256": hashlib.sha256(configuration_bytes).hexdigest(), "configurationSha256": q.digest(config), "planSha256": plan_digest(), **anchors}
    packet_path = output / "replacement-acceptance-packet.json"
    detached_configuration = output / "runtime-reference.json"
    command = (f'test "$(git -C ../platform rev-parse HEAD)" = "{head}" && test -z "$(git -C ../platform status --porcelain)" && '
               f'env PLATFORM_JOINT_COHORT_CONFIGURATION={shlex.quote(str(coordinated.get("installedArtifacts", {}).get("jointConfiguration", {}).get("path", "")))} '
               f'EXT_SECRETS_1_RUNTIME_CONFIGURATION={shlex.quote(str(detached_configuration))} EXT_SECRETS_1_ACCEPTANCE_PACKET={shlex.quote(str(packet_path))} '
               + q.command_suffix(eventstore, artifacts, evidence))
    if not installation_missing and coordinated["exactCommands"].get("EXT-SECRETS-1") != command:
        missing.append("EXT-SECRETS-1.ExactCoordinatedCommand")
    packet = {"schema": "EXT-SECRETS-1.replacement-acceptance.v2", "candidateCommit": head,
              "acceptedReplacementCommit": None, "dependencyAvailable": False, "liveCompatibilityAuthorized": False,
              "productionMutationAllowed": False, "ownerAcceptanceRequired": True, **binding,
              "publicAnchorSha256": q.digest(anchors), "RuntimeQualificationBinding": binding, "exactCommand": command,
              "readiness": "IncompleteSourceCandidate",
              "qualificationCompleted": False, "cohortBlockingInputs": cohort, "registerEvidence": register_binding,
              "platformIntegrationDate": coordinated.get("platformIntegrationDate"), "protectionIntegrationDate": coordinated.get("protectionIntegrationDate"),
              "installedHashes": installed_hashes, "completeReplacementPacket": not missing}
    report = {"code": "DependencyNotAvailable", "dependency": "EXT-SECRETS-1", "candidateCommit": head,
              "qualificationEffects": 0, "dependencyAvailable": False, "missingInputs": sorted(set(missing)),
              "packetPrepared": candidate_ready and not missing, "completeReplacementPacket": candidate_ready and not missing,
              "installedHashes": installed_hashes, "targetCommit": head if candidate_ready and not missing else None}
    incomplete = dict(report, packetPrepared=False, completeReplacementPacket=False, targetCommit=None)
    if report["packetPrepared"]:
        incomplete["missingInputs"] = ["DetachedPacketOutputIncomplete"]
    packet_output.report(output / "blocked-inputs.json", incomplete)
    if report["packetPrepared"]:
        from coordinated_packets import records, shared_binding
        from joint_live_phase_runner import full_plan, digest as cohort_digest
        packet["cohortPlanSha256"] = cohort_digest(full_plan())
        from coordinated_packets import protection_binding
        packet["protectionBinding"] = protection_binding(installed_hashes, coordinated)
        packet["jointBinding"] = snapshot_for(coordinated).json(coordinated["installedArtifacts"]["jointConfiguration"]["path"])["binding"]
        packet["jointConfigurationSha256"] = installed_hashes["jointConfigurationSha256"]
        packet["launchInventory"] = snapshot_for(coordinated).json(coordinated["installedArtifacts"]["launchInventory"]["path"])
        packet["coordinatedRecords"] = records(register, head, coordinated["platformIntegrationDate"], coordinated["exactCommands"], shared_binding(installed_hashes, coordinated), snapshot=snapshot_for(coordinated))
        outputs = {output / "joint-qualification-plan.json": q.canonical(full_plan()),
                   detached_configuration: configuration_bytes,
                   output / "exact-command.sh": (command + "\n").encode("utf-8"),
                   output / "runtime-qualification-binding.json": q.canonical(binding), packet_path: q.canonical(packet)}
        for path, data in outputs.items():
            packet_output.artifact(path, data)
        packet_output.verify(outputs)
        packet_output.report(output / "blocked-inputs.json", report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True)
    parser.add_argument("--configuration", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--eventstore-argument", default="../eventstore")
    parser.add_argument("--artifacts-argument", required=True)
    parser.add_argument("--evidence-argument", required=True)
    parser.add_argument("--coordination-context", help="Explicit independently enrolled public installation bundle and accepted dates")
    parser.add_argument("--register", help="Authoritative Agents register for cohort blockers; read-only, never accepted or edited")
    args = parser.parse_args()
    result = prepare(args.root, args.configuration, args.output, args.eventstore_argument, args.artifacts_argument, args.evidence_argument, args.register, args.coordination_context)
    print(json.dumps(result, sort_keys=True))
    return 2 if result["missingInputs"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
