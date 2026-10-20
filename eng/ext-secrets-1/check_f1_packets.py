#!/usr/bin/env python3
"""Audit F1 prerequisite packets only. Never grants completion or dependency availability."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKETS = ROOT / "docs/implementation/ext-secrets-1-f1-2026-10-20"
HOLDER_CLASSES = (
    "caller-owned", "sdk-protobuf", "grpc-dotnet", "daprd-grpc-go", "component",
    "native-sdk-tls", "uds-kernel", "host-memory", "diagnostics", "delayed-queues", "provider-internal",
)
STAGES = ("Admitted", "SendCommitted", "UseStopped", "BoundarySealed", "QuiescenceObserved",
          "TerminationObserved", "SanitizationVerified", "Closed")
OUTSTANDING = {"semantic-component-artifact", "backend-primitives-and-retention", "installed-transport-measurement",
               "header-units-and-metadata-measurement", "independent-completion-verifier",
               "all-holder-physical-proof", "separate-native-authorization"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unavailable(packet):
    for flag in ("DependencyAvailable", "LiveReady", "KeyDeliveryEnabled", "CompletionPermission", "PhysicalEvidence"):
        require(packet.get(flag) is False, f"Synthetic/proposed packet cannot claim {flag}")


def check_transport(packet, root=PACKETS):
    unavailable(packet)
    require(packet.get("kind") == "F1TransportPrerequisitePacket" and packet.get("status") == "ProposedUninstalled",
            "Expected an uninstalled transport prerequisite packet")
    manifest_path = root / "research-source-manifest.json"
    require(packet.get("sourceManifestSha256") == hashlib.sha256(manifest_path.read_bytes()).hexdigest(), "Source manifest hash changed")
    require(packet.get("sourcePins") == json.loads(manifest_path.read_text())["pins"], "Transport source revisions changed")
    require(packet.get("dependencies") == {"Builds": "5d03f1db9c38db34ecac7fe6048937478ffb82cc",
                                         "EventStore": "c9f47dd5ba72918445dfd24a674a5b2c310589bd"}, "Dependency revisions changed")
    limits = packet["limits"]
    require(limits.get("dataCapBytes") == 64 * 1024**2 and limits.get("requestProtobufBytes") == 128 * 1024**2,
            "Request data/protobuf bounds changed")
    require(limits.get("sdkSendBytes") == limits["requestProtobufBytes"] == limits.get("apiReceiveBytes") == limits.get("componentReceiveBytes"),
            "Each request hop needs an explicit 128 MiB setting")
    require(limits.get("responseWireBytesExclusive") == 1024**2 and limits.get("pageDataBytes") == 256 * 1024,
            "Full responses must stay below 1 MiB")
    require(limits.get("conservativeManifestDataBytes") == 45508608 and limits.get("conservativeTransitionDataBytes") == 48933548,
            "Complete request bounds changed")
    settings = packet["proposedSettings"]
    require(settings.get("sdk") == "GrpcChannelOptions { MaxSendMessageSize = 134217728, MaxReceiveMessageSize = 1048575 }"
            and settings.get("daprd") == ["--max-body-size", "128Mi", "--read-buffer-size", "64Ki"]
            and settings.get("componentGo") == "grpc.NewServer(grpc.MaxRecvMsgSize(134217728), grpc.MaxSendMsgSize(1048575), grpc.MaxHeaderListSize(65536))",
            "Exact SDK/API/component settings changed")
    captures = packet["syntheticSdkCaptures"]
    for name, expected in {
        "RetainedMaximumManifest": (24692881, "72D99146B808B66DBA54DE3D336E284E5F37E5E9D8605A8ABE85640F70EC5DBF", 24692931, "50AB9B289AB78F2A6E2B70C77D6153E46D50A15A6F115DE685AFE377419CACCB"),
        "MaximumPendingTransition": (44740912, "0EFF8B317B90E1ED456DF4DDE1C8F7D229D8186639F47EBEE9DEFC0E50490D6C", 44740962, "B37D39DFDC6E5F9FB0CCE40B89CB9EE814F1E8D97124271067EFF5DBD9DDBC38"),
    }.items():
        data_bytes, data_hash, api_bytes, api_hash = expected
        expected_capture = {"DataBytes": data_bytes, "DataSha256": data_hash, "ApiProtobufBytes": api_bytes,
                            "ApiProtobufSha256": api_hash, "GrpcFrameBytes": api_bytes + 5, "SdkCalls": 1,
                            "MaxSendMessageBytes": 134217728, "MaxReceiveMessageBytes": 1048575, "RealSidecarPathExecuted": False}
        actual = captures.get(name, {})
        require(actual == expected_capture and all(type(actual.get(key)) is type(value) for key, value in expected_capture.items()),
                "Exact complete synthetic SDK capture required")
    require(captures.get("SidecarOrComponentExecuted") is False and captures.get("ProductionInvokerProfileChanged") is False,
            "SDK witness cannot establish installed component availability")
    metadata = packet["forwardedMetadata"]
    require(metadata == {"maximumEntries": 64, "maximumKeyUtf8Bytes": 256, "maximumValueUtf8Bytes": 4096,
                         "aggregateUtf8Bytes": 65536, "protobufBudgetBytes": 66560,
                         "includes": ["request metadata", "incoming gRPC headers after sanitization", "generated tracing metadata"],
                         "installedEnforcement": None}, "Forwarded metadata needs a separate bounded, uninstalled profile")
    headers = packet["headers"]
    require(headers.get("readBufferSetting") == "64Ki" and headers.get("parsedBytes") == 65536
            and headers.get("pinnedGrpcHeaderListBytes") == 67108864 and headers.get("intendedHeaderListBytes") == 65536
            and headers.get("deploymentMeasuredBytes") is None, "Header units discrepancy must remain explicit and unproved")
    patch = packet["headerPatch"]
    require(patch.get("baseSourceSha256") == "1b4f56b441e92822a0b42c63d54b4450cfe45a94fe2a4736ad14ff54dc7dfbe3"
            and patch.get("resultSourceSha256") == "41d6010a43956eda01ae0d5c4bc94daa95e61e8523f5031c417e81cd1879be4a"
            and patch.get("applied") is False and patch.get("artifactDigest") is None,
            "Header patch must be pinned and unapplied")
    require(patch.get("sha256") == hashlib.sha256((root / patch["path"]).read_bytes()).hexdigest(), "Header patch hash changed")
    require(packet.get("requestSizeSidecarPatchRequired") is False, "Stock sidecar send is not the component receive ceiling")
    require(packet.get("installedComponentArtifact") is None and packet.get("backendProfile") is None,
            "Absent component/backend must stay absent")
    require(OUTSTANDING <= set(packet.get("outstandingProof", [])), "Outstanding transport/completion proof omitted")
    return {"transportPacketConforms": True, "installedPathProved": False}


def check_termination(packet):
    unavailable(packet)
    require(packet.get("kind") == "F1TerminationPrerequisitePacket" and packet.get("evidenceClass") == "SyntheticProtocolOnly",
            "Only synthetic prerequisite protocol evidence is audited")
    require(packet.get("installedIndependentVerifier") is None and packet.get("acceptedTerminationProfile") is None,
            "Missing installed verifier/profile must stay explicit")
    require(packet.get("stages") == list(STAGES), "Termination stages missing or reordered")
    require(set(packet.get("requiredHolderClasses", [])) == set(HOLDER_CLASSES), "Missing holder classes")
    require(packet.get("recovery") == {"preserveOldIncarnations": True, "preserveSendCommitted": True,
                                       "preserveInventoryWatermarks": True, "duplicateAndLostAck": "status-or-lookup-only",
                                       "authorityOriginalsAndWitnessesOutsideBoundary": True}, "Unsafe recovery protocol")
    witness = packet["syntheticWitness"]
    require(witness.get("phase") == "QuiescenceObserved" and witness.get("completionPermission") is False
            and witness.get("terminationObserved") is False and witness.get("sanitizationVerified") is False,
            "Synthetic witness or missing verifier cannot authorize terminal closure")
    original = witness["originalIdentity"]
    require(set(original) == {"operationId", "intentId", "requestDigest", "originalRootReference", "keyUseLeaseId", "deliveryAttemptId"}
            and all(isinstance(v, str) and v for v in original.values()), "Complete original identity required")
    admitted = witness["admissionInventory"]
    observed = witness["observedInventory"]
    fields = {"holderId", "holderClass", "processIncarnation", "transportIncarnation", "hostIncarnation", "copyLineage", "originalIdentity"}
    require(admitted and all(set(h) == fields and h["originalIdentity"] == original
                             and all(isinstance(h[f], str) and h[f] for f in fields - {"originalIdentity"}) for h in admitted),
            "Every admitted copy must retain original lineage and exact incarnations")
    ids = [h["holderId"] for h in admitted]
    require(len(set(ids)) == len(ids), "Duplicate holder identity")
    require(set(h["holderClass"] for h in admitted) == set(HOLDER_CLASSES), "Missing admitted holder classes")
    require(all(h["holderClass"] in HOLDER_CLASSES for h in admitted), "Unknown holder class")
    require(len(observed) == len(admitted) and {h["holderId"]: h for h in observed} == {h["holderId"]: h for h in admitted},
            "Missing/replaced holder or substituted incarnation; old inventory cannot be overwritten")
    watermark = witness["admissionHighWatermark"]
    require(type(watermark) is int and watermark >= len(admitted) and witness.get("observedHighWatermark") == watermark,
            "Inventory watermark must survive recovery and cover all admitted copies")
    seals = witness["queueSeals"]
    require({q["queueClass"] for q in seals} == {"sdk-retry", "sidecar", "component-native", "uds-kernel", "delayed-delivery"},
            "Every queue class needs a seal")
    require(all(q.get("sealed") is True and type(q.get("sealedThroughSequence")) is int
                and q["sealedThroughSequence"] >= watermark and q.get("observedThroughSequence") == q["sealedThroughSequence"]
                and q.get("newAdmissionRejected") is True for q in seals), "Unsealed queue or stale inventory watermark")
    capacity = packet["admissionCapacity"]
    require(capacity.get("reserveBeforeAdmission") is True and capacity.get("measuredCapacity") is None
            and set(capacity.get("resources", [])) == {"request-bytes", "checkpoint-original-retention", "native-intents", "holders",
                                                        "queue-seals", "termination-sanitation", "independent-evidence"},
            "Completion capacity is a measured admission prerequisite")
    return {"terminationPacketConforms": True, "CompletionPermission": False, "physicalCompletionProved": False}


def check_packets(root=PACKETS):
    root = Path(root)
    result = check_transport(json.loads((root / "transport-profile.json").read_text()), root)
    result.update(check_termination(json.loads((root / "termination-protocol.json").read_text())))
    result.update(DependencyAvailable=False, LiveReady=False, KeyDeliveryEnabled=False)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet-root", type=Path, default=PACKETS)
    print(json.dumps(check_packets(parser.parse_args().packet_root), sort_keys=True))


if __name__ == "__main__":
    main()
