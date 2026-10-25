#!/usr/bin/env python3
"""Offline input inspection only: no network, provisioning, serving, worker or destructive calls."""
import argparse
import hashlib
import json
import re
from pathlib import Path

def strict_json(text):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result: raise ValueError("conflicting duplicate JSON property")
            result[key] = value
        return result
    return json.loads(text, object_pairs_hook=unique)


OWNERS = {
    "images": ("Platform release owner", "Build and pin the reviewed artifact by immutable image digest"),
    "artifacts": ("Platform infrastructure owner", "Supply retained exact artifact bytes, owner implementation and accepted profile pin"),
    "credentialReferences": ("Security installation owner", "Install the least-privilege opaque credential reference; never put credential values in this ledger"),
    "placement": ("Recovery administrator and deputy", "Enroll independent failure/recovery placement outside application backups"),
    "logicalRoles": ("Platform composition owner", "Bind the logical role to its authenticated installed Dapr service/component"),
    "kubernetes": ("Platform Kubernetes identity/host owner", "Install exact Dapr workload token/trust/control-plane/API-channel and receiving-host inputs; retain owner/volume proof"),
    "referenceCredentials": ("Infrastructure reference-backend credential owner", "Install the exact secret/key and shared-credential profile at the declared protected file; no metadata/default-chain fallback"),
    "profiles": ("Independent transport/closure observer", "Measure and attest the complete installed transport, buffer and host profile"),
}

REQUIRED = {
    "images": {"component", "custodyApplication", "dapr", "dotnetSdk", "dotnetAspnet"},
    "artifacts": {"custodyApplicationComposition", "componentProfile", "enrollment", "componentVerifier", "transportProfile", "holderClosureProfile", "capacityMeasurement", "independentPlacement", "authorityEnrollment", "originalArchiveEnrollment", "privateAuthorizerImplementation", "controlIssuerImplementation", "independentObserverImplementation", "eventStoreIntegration", "physicalQualification", "g1Evidence", "registrationOriginalLocatorImplementation", "reservedManifestOriginalLocatorImplementation", "registrationOriginalMapping", "reservedManifestOriginalMapping"},
    "credentialReferences": {"component", "worker", "privateAuthorizer", "controlIssuer", "independentObserver", "recoveryAdministrator", "recoveryDeputy"},
    "placement": {"applicationRecoveryClass", "authorityRecoveryClass", "originalRecoveryClass", "authorityFailureDomain", "applicationFailureDomain"},
    "logicalRoles": {"interaction-custody", "custody-authorizer"},
    "kubernetes": {"appPort", "appProtocol", "internalGrpcPort", "controlPlaneAddress", "controlPlaneNamespace", "controlPlaneTrustDomain", "sentryAddress", "sentryTokenAudience", "sentryTokenExpirationSeconds", "trustAnchorSecret", "trustAnchorKey", "apiTokenSecret", "apiTokenKey", "appApiTokenSecret", "appApiTokenKey", "workerGroupId", "workerOutputOwnershipEvidence"},
    "referenceCredentials": {"componentSecret", "componentKey", "componentProfile", "workerSecret", "workerKey", "workerProfile", "mountedFile"},
    "profiles": {"transport", "maximumRequestMessageBytes", "maximumResponseFrameBytes", "automaticRetries", "redirects", "kubernetesNativeSidecars", "swapDisabled", "coreDumpsDisabled", "memoryCaptureDisabled"},
}


def inspect(path):
    data = strict_json(Path(path).read_text())
    if not isinstance(data, dict): raise ValueError("object required")
    missing = []
    gaps = {}
    def gap(field, reason):
        owner, action = OWNERS.get(field.split('.')[0], ("Platform and protection owners", "Return independently accepted installation/proof"))
        specific = {
            "artifacts.eventStoreIntegration": ("EventStore Maintainer", "Complete owning-source adapter/owner-state integration and return an exact published source/package target"),
            "artifacts.privateAuthorizerImplementation": ("Independent private-authority service maintainer", "Implement authenticated exact-intent/current-permission authorization; install separately after source review"),
            "artifacts.controlIssuerImplementation": ("Governance/guard and protection-owner maintainers", "Implement durable hold/dispatch/reservation handoff and original lookup; install separately after source review"),
            "artifacts.independentObserverImplementation": ("Independent infrastructure/security observer", "Implement native evidence, copy/buffer/termination and survivor-recovery verification; install separately after source review"),
            "artifacts.custodyApplicationComposition": ("Platform host composition owner", "Integrate the public adapters into the owning authenticated custody application; no production DI is activated here"),
            "artifacts.registrationOriginalLocatorImplementation": ("Platform custody application maintainer", "Implement and install ICustodyKeyRegistrationSource under its exact scoped original lookup binding"),
            "artifacts.registrationOriginalMapping": ("Platform custody application maintainer", "Retain full key identity to exact original registration/actor revision/KEK/opaque root/wrap receipt mappings, verified independently before authority use"),
            "artifacts.reservedManifestOriginalLocatorImplementation": ("EventStore Maintainer with Platform custody host integration owner", "Implement and install ICustodyReservedManifestSource with restricted authenticated protection-original lookup"),
            "artifacts.reservedManifestOriginalMapping": ("EventStore Maintainer", "Retain exact ConsumptionReserved receipt to complete immutable signed request, issued/dispatch guard facts and sorted manifest vector"),
            "artifacts.g1Evidence": ("Protection owner and independent erasure observer", "Return retained accepted physical non-revival/shared-cut proof; the OpenBao negative control remains unqualified"),
        }
        owner, action = specific.get(field, (owner, action))
        if field in gaps:
            if reason not in gaps[field]["reason"].split("; "): gaps[field]["reason"] += "; " + reason
            return
        entry = dict(input=field, reason=reason, owner=owner, action=action)
        gaps[field] = entry
        missing.append(entry)
    for unknown in data.keys() - (REQUIRED.keys() | {"protocol", "sourceStatus", "g1Qualified"}): gap(unknown, "unknown root field")
    if data.get("protocol") != "custody-lifecycle-v1": gap("protocol", "exact protocol required")
    if data.get("sourceStatus") != "reference-component-uninstalled": gap("sourceStatus", "this bundle packages an uninstalled reference component only")
    for section in OWNERS:
        if not isinstance(data.get(section), dict):
            gap(section, "required input section absent or malformed")
            for field in REQUIRED[section]: gap(section + '.' + field, "required input absent")
            continue
        required = REQUIRED[section]
        for absent in required - data[section].keys(): gap(section + '.' + absent, "required input absent")
        for unknown in data[section].keys() - required: gap(section + '.' + unknown, "unknown input field")
        for key, value in data[section].items():
            field = section + '.' + key
            if value is None or value == "": gap(field, "not installed or supplied"); continue
            if section in {"placement", "logicalRoles"} and (not isinstance(value, str) or not value.strip()): gap(field, "nonempty opaque identifier required")
            if section == "kubernetes":
                if key in {"appPort", "internalGrpcPort", "workerGroupId", "sentryTokenExpirationSeconds"}:
                    minimum, maximum = (1, 65535) if key in {"appPort", "internalGrpcPort"} else (1, 2147483647) if key == "workerGroupId" else (600, 2147483647)
                    if type(value) is not int or not minimum <= value <= maximum: gap(field, "valid explicit numeric workload input required")
                elif not isinstance(value, str) or not value.strip(): gap(field, "explicit workload identity/host reference required")
                elif key == "appProtocol" and value not in {"http", "https", "grpc", "grpcs", "h2c"}: gap(field, "supported exact Dapr app protocol required")
            if section == "kubernetes" and key == "internalGrpcPort" and value != 50002: gap(field, "pinned Kubernetes resolver/internal invocation port 50002 required")
            if section == "referenceCredentials" and (not isinstance(value, str) or not value.strip()): gap(field, "exact credential file/key/profile reference required")
            if section == "referenceCredentials" and key == "mountedFile" and value != "/credentials/credentials": gap(field, "exact consumed protected credential mount required")
            if section == "images" and (not isinstance(value, str) or not re.fullmatch(r"[a-z0-9][a-z0-9./:_-]*@sha256:[a-f0-9]{64}", value)):
                gap(field, "immutable digest-pinned image required")
            if section == "credentialReferences" and (not isinstance(value, str) or not value.startswith(("file:/", "secretref:", "workloadref:"))):
                gap(field, "scoped opaque credential reference required")
            if section == "artifacts":
                if not isinstance(value, dict) or set(value) != {"path", "sha256"}:
                    gap(field, "exact local file path and SHA-256 required"); continue
                if not isinstance(value["path"], str) or not isinstance(value["sha256"], str) or not re.fullmatch(r"[a-f0-9]{64}", value["sha256"]):
                    gap(field, "artifact path/hash types invalid"); continue
                target = Path(value["path"])
                if not target.is_absolute(): target = Path(path).resolve().parent / target
                if not target.is_file() or hashlib.sha256(target.read_bytes()).hexdigest() != value["sha256"]:
                    gap(field, "retained artifact missing or hash mismatch")
    expected = {"transport": "custody-lifecycle-v1", "maximumRequestMessageBytes": 134217728,
                "maximumResponseFrameBytes": 1048575, "automaticRetries": 0, "redirects": False,
                "kubernetesNativeSidecars": True, "swapDisabled": True, "coreDumpsDisabled": True, "memoryCaptureDisabled": True}
    profiles = data.get("profiles") if isinstance(data.get("profiles"), dict) else {}
    for key, value in expected.items():
        if type(profiles.get(key)) is not type(value) or profiles.get(key) != value: gap('profiles.' + key, "exact supported installed profile required")
    placement = data.get("placement") if isinstance(data.get("placement"), dict) else {}
    if placement.get("authorityFailureDomain") == placement.get("applicationFailureDomain"):
        gap("placement.authorityFailureDomain", "authority must survive the entire application restore/failure cut")
    if data.get("g1Qualified") is not True: gap("g1Qualified", "physical G1 proof absent; ObservedRevival remains unqualified")
    # Inspection never certifies evidence, grants deployment permission or produces a runnable installation.
    return {"status": "Blocked" if missing else "InputsPresentUnverified", "installedProvider": False,
            "physicalQualification": False, "dependencyAvailable": False, "runnableInstallationGenerated": False,
            "inputSha256": hashlib.sha256(Path(path).read_bytes()).hexdigest(), "missing": missing}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try: result = inspect(args.inputs)
    except (OSError, ValueError, TypeError, KeyError) as error:
        result = {"status": "Blocked", "error": type(error).__name__, "installedProvider": False,
                  "physicalQualification": False, "dependencyAvailable": False, "runnableInstallationGenerated": False}
    text = json.dumps(result, indent=2) + '\n'
    if args.output:
        with args.output.open('x') as stream: stream.write(text)
    print(text, end='')
    return 1 if result["status"] == "Blocked" else 0

if __name__ == '__main__': raise SystemExit(main())
