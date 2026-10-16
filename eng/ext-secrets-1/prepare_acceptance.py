#!/usr/bin/env python3
"""Prepare detached public pins after committing the immutable candidate; never installs or accepts it."""
import argparse
import hashlib
import json
import shlex
import time
from pathlib import Path

import qualify as q
from cases import plan_digest


def prepare(root, configuration, output, eventstore="../eventstore", artifacts="/tmp/ext-secrets-artifacts", evidence="/tmp/ext-secrets-evidence"):
    root, configuration, output = Path(root).resolve(), Path(configuration).resolve(), Path(output).resolve()
    if output.is_relative_to(root):
        raise ValueError("detached output must be outside candidate checkout")
    if output.exists():
        raise ValueError("fresh detached output directory required")
    config = q.read_json(configuration)
    missing = [name for name in q.REQUIRED if not isinstance(q.get(config, name), str) or not q.get(config, name)]
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
    head = q.git(root, "rev-parse", "HEAD")
    if q.get(config, "runtime.acceptedReplacementCommit") != head:
        missing.append("runtime.acceptedReplacementCommit")
    if q.git(root, "status", "--porcelain"):
        missing.append("CleanImmutableCandidateCheckout")
    anchors, decoded = {}, []
    for role in ("executor", "observer", "enrollment"):
        try:
            data = Path(q.get(config, f"qualification.{role}.publicAnchorFile")).read_bytes()
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
    # Installation, current scopes and owner acceptance are independent external gates even with complete references.
    missing.extend(q.IMPLEMENTATIONS)
    missing.append("OwnerAcceptedTargetRuntimeQualificationBindingAndExactCommand")
    binding = {"configurationSha256": q.digest(config), "planSha256": plan_digest(), **anchors}
    packet_path = output / "replacement-acceptance-packet.json"
    detached_configuration = output / "runtime-reference.json"
    command = (f'test "$(git -C ../platform rev-parse HEAD)" = "{head}" && test -z "$(git -C ../platform status --porcelain)" && '
               f'env EXT_SECRETS_1_RUNTIME_CONFIGURATION={shlex.quote(str(detached_configuration))} EXT_SECRETS_1_ACCEPTANCE_PACKET={shlex.quote(str(packet_path))} '
               f'pwsh -NoProfile -File ../platform/eng/verify-ext-secrets-1.ps1 -Mode Live -EventStoreSourceRoot {shlex.quote(eventstore)} '
               f'-ArtifactsDirectory {shlex.quote(artifacts)} -EvidenceDirectory {shlex.quote(evidence)}')
    packet = {"schema": "EXT-SECRETS-1.replacement-acceptance.v2", "candidateCommit": head,
              "acceptedReplacementCommit": None, "dependencyAvailable": False, "liveCompatibilityAuthorized": False,
              "productionMutationAllowed": False, "ownerAcceptanceRequired": True, **binding,
              "publicAnchorSha256": q.digest(anchors), "RuntimeQualificationBinding": binding, "exactCommand": command}
    output.mkdir(parents=True)
    report = {"code": "DependencyNotAvailable", "dependency": "EXT-SECRETS-1", "candidateCommit": head,
              "qualificationEffects": 0, "dependencyAvailable": False, "missingInputs": sorted(set(missing)),
              "packetPrepared": "CleanImmutableCandidateCheckout" not in missing}
    (output / "blocked-inputs.json").write_bytes(q.canonical(report))
    if report["packetPrepared"]:
        packet_path.write_bytes(q.canonical(packet))
        detached_configuration.write_bytes(q.canonical(config))
        (output / "exact-command.sh").write_text(command + "\n")
        (output / "runtime-qualification-binding.json").write_bytes(q.canonical(binding))
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True)
    parser.add_argument("--configuration", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--eventstore-argument", default="../eventstore")
    parser.add_argument("--artifacts-argument", required=True)
    parser.add_argument("--evidence-argument", required=True)
    args = parser.parse_args()
    result = prepare(args.root, args.configuration, args.output, args.eventstore_argument, args.artifacts_argument, args.evidence_argument)
    print(json.dumps(result, sort_keys=True))
    return 2 if result["missingInputs"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
