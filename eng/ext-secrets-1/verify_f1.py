#!/usr/bin/env python3
"""Build and audit synthetic F1 only. No F0, infrastructure, or Live invocation."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

from check_f1_packets import check_packets


ROOT = Path(__file__).resolve().parents[2]
PROJECT = "tests/Hexalith.Platform.Secrets.Runtime.Tests/Hexalith.Platform.Secrets.Runtime.Tests.csproj"
OWNER_CAPTURE_PATH = Path("src/Hexalith.EventStore.Server/Security/DeletionConsumptionIdentity.cs")
OWNER_CAPTURE_FIXTURE = ROOT / "tests/Hexalith.Platform.Secrets.Runtime.Tests/OwnerConformance/DeletionConsumptionIdentity.cs"
OWNER_CAPTURE_SHA256 = "117018c8279062487a7766c73f8443c4133e49a9b4f3c79edaceb802afda145c"


EXPECTED_DEPENDENCIES = {"Builds": "5d03f1db9c38db34ecac7fe6048937478ffb82cc",
                         "EventStore": "c9f47dd5ba72918445dfd24a674a5b2c310589bd"}
CANDIDATE_CAPTURES = {
    "RetainedMaximumManifest": (24692881, "72D99146B808B66DBA54DE3D336E284E5F37E5E9D8605A8ABE85640F70EC5DBF",
                                24692931, "50AB9B289AB78F2A6E2B70C77D6153E46D50A15A6F115DE685AFE377419CACCB"),
    "MaximumPendingTransition": (44740912, "0EFF8B317B90E1ED456DF4DDE1C8F7D229D8186639F47EBEE9DEFC0E50490D6C",
                                 44740962, "B37D39DFDC6E5F9FB0CCE40B89CB9EE814F1E8D97124271067EFF5DBD9DDBC38"),
}

# Explicit behavioral coverage contract; the measurement producer alone is insufficient.
BEHAVIORAL_COVERAGE = {
    "Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingContractTests": (
        "PinUnpinAndExactReadOnlyLookupRetainOriginalIdentityAndDigest",
        "PrivateProofFieldsAreBoundedWithoutChangingOwnerIdentity",
        "CustodyMetadataRetainsExistingUtf8ByteBoundsIndependently",
        "ManifestRejectsReversedDispatchAndInvalidJwsUnicodeIndependently",
        "ContradictoryWrapRegistrationEnvelopeIsRejected",
        "ExistingCanonicalFramingAndC0OuterVectorsRemainExact",
        "ExistingManifestDigestSignedJcsOrderAndOptionalBytesSurviveClosedTransport",
        "ClosedRequestRejectsSubstitutionAndAmbiguity",
        "NativeIntentsAndEphemeralDeliveryRetainOriginalAcrossUncertaintyAndReinit",
    ),
    "Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingDaprInvokerTests": (
        "PinUnpinAndLookupUseExactOriginalPayloadWithCompatibleUnverifiedResult",
        "LifecycleResultsRejectIncompatibleStatusAndReceiptShapes",
        "ManifestNonterminalStatusProofIsClosedExactAndAlwaysUnverified",
        "CancellationAtCompletedSendBoundariesCannotExposeAResult",
        "PageSourceChecksCancellationBeforeFactoryAndAfterCompletedFactory",
        "ActualPinnedSdkSerializesRawBytesOnceWithoutJsonByteArrayBase64Wrapping",
        "ForwardedMetadataIsAccountedSeparatelyAndCandidateSettingsDoNotOpenInvoker",
        "ForwardedMetadataReserveRefusesNearDefaultRequestBeforeSdkSend",
        "LostCancelledOrAmbiguousSdkResultIsUnknownWithOneAttempt",
        "MissingCurrentAuthorityUnsupportedKeyDeliveryAndOversizeRefuseBeforeSdkSend",
        "DistinctAnchorPhasesBindExactBytesAndRequireSeparateRecoveryAuthority",
        "ReadCurrentMakesFreshCallsAndRejectsStaleAttemptChallenge",
        "OriginalPagesUseSameRawSdkFramingWithFreshExactLookupAuthorization",
        "ExactPrivateAuthenticatorDeniesSubstitutionDespiteStableOriginalDigest",
    ),
    "Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingOriginalTests": (
        "CompletedTasksCannotBypassCancellationOrDeadlineBeforeConsumed",
        "ValidPageHashesCannotSubstituteForTheWholeOriginalHash",
        "OnlyCompleteAuthenticatedOriginalVectorAndSignatureBecomeConsumed",
        "InvalidMissingOrUnauthenticatedPagesRemainUnknownWithoutPartialVector",
        "MatchingWholeHashDoesNotAuthenticateMalformedOriginal",
        "CurrentAuthorizationTimeoutAndInterruptedAssemblyCannotExposePartialConsumed",
    ),
}

def git(root, *arguments):
    return subprocess.check_output(["git", "-C", str(root), *arguments], timeout=30).decode().strip()


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_owner_capture_fixture(eventstore, fixture=OWNER_CAPTURE_FIXTURE):
    external = Path(eventstore) / OWNER_CAPTURE_PATH
    local_hash, external_hash = sha(fixture), sha(external)
    if local_hash != OWNER_CAPTURE_SHA256 or external_hash != local_hash:
        raise ValueError("Pinned local owner Capture fixture and external source SHA must match the approved pin")
    return {"fixture": str(fixture), "external": str(external), "sha256": local_hash}


def snapshot(root):
    names = subprocess.check_output(
        ["git", "-C", str(root), "ls-files", "--cached", "--others", "--exclude-standard", "-z"], timeout=30)
    return {name: sha(root / name) for name in sorted(set(names.decode().split("\0")))
            if name and (root / name).is_file()}


def audit(xml_path, evidence_path):
    xml = ET.parse(xml_path).getroot()
    assemblies, tests = xml.findall("assembly"), xml.findall(".//test")
    if len(assemblies) != 1:
        raise ValueError("Exactly one fresh Runtime test assembly is required")
    assembly = assemblies[0]
    total = int(assembly.attrib["total"])
    if (total <= 0 or len(tests) != total or int(assembly.attrib["passed"]) != total
            or any(int(assembly.attrib.get(name, "0")) != 0 for name in ("failed", "errors", "skipped", "not-run"))
            or any(test.attrib.get("result") != "Pass" for test in tests)):
        raise ValueError("Missing, failed, skipped or unrun Runtime tests")
    f1_tests = [test for test in tests if ".CustodyBinding" in test.attrib.get("type", "")]
    if not f1_tests:
        raise ValueError("F1 contract tests did not execute")
    producers = [test for test in f1_tests
                 if test.attrib.get("type") == "Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingFeasibilityTests"
                 and test.attrib.get("method") == "EmitDeterministicF1Evidence"]
    if len(producers) != 1:
        raise ValueError("Exactly one passing F1 evidence producer is required")
    required = {(class_name, method) for class_name, methods in BEHAVIORAL_COVERAGE.items() for method in methods}
    observed = {(test.attrib.get("type"), test.attrib.get("method")) for test in f1_tests}
    missing = sorted(required - observed)
    if missing:
        raise ValueError("Missing F1 behavioral coverage: " + ", ".join(f"{cls}.{method}" for cls, method in missing))
    evidence = json.loads(evidence_path.read_text())
    # Synthetic checks cannot ever authorize a physical or Live claim.
    if evidence.get("DependencyAvailable") is not False or evidence.get("LiveReady") is not False:
        raise ValueError("F1 evidence must explicitly leave dependency and Live unavailable")
    captures = evidence.get("CandidateSdkCaptures", {})
    for name, (data_bytes, data_hash, api_bytes, api_hash) in CANDIDATE_CAPTURES.items():
        capture = captures.get(name, {})
        expected = {"DataBytes": data_bytes, "DataSha256": data_hash, "ApiProtobufBytes": api_bytes,
                    "ApiProtobufSha256": api_hash, "GrpcFrameBytes": api_bytes + 5, "SdkCalls": 1,
                    "MaxSendMessageBytes": 134217728, "MaxReceiveMessageBytes": 1048575}
        if any(type(capture.get(key)) is not type(value) or capture.get(key) != value for key, value in expected.items()):
            raise ValueError("Missing or changed complete candidate SDK capture: " + name)
        if capture.get("RealSidecarPathExecuted") is not False:
            raise ValueError("Candidate SDK serialization cannot establish a sidecar path")
    if (captures.get("SidecarOrComponentExecuted") is not False or captures.get("ProductionInvokerProfileChanged") is not False
            or captures.get("DecodeCapBytes") != 67108864 or captures.get("FullManifestRequestBoundBytes") != 45508608
            or captures.get("FullTransitionRequestBoundBytes") != 48933548 or captures.get("ComponentForwardedMetadataWireBudgetBytes") != 66560):
        raise ValueError("Candidate bounds or unqualified transport claims changed")
    return {"total": total, "passed": total, "f1Passed": len(f1_tests), "behavioralMethodsCovered": len(required)}, evidence


def run(builds, eventstore, artifacts, output):
    builds, eventstore, artifacts, output = [Path(p).resolve() for p in (builds, eventstore, artifacts, output)]
    if output.is_relative_to(ROOT) or artifacts.is_relative_to(ROOT) or output == artifacts:
        raise ValueError("Use separate detached artifact and evidence directories")
    if output.exists() or artifacts.exists():
        raise ValueError("Fresh artifact and evidence directories are required; retain earlier attempts")
    inputs = {}
    for name, root in (("Builds", builds), ("EventStore", eventstore)):
        if git(root, "status", "--porcelain"):
            raise ValueError(f"{name} source dependency must be clean")
        inputs[name] = {"root": str(root), "commit": git(root, "rev-parse", "HEAD")}
        if inputs[name]["commit"] != EXPECTED_DEPENDENCIES[name]:
            raise ValueError(f"{name} source dependency must match the spec revision")
    owner_capture = verify_owner_capture_fixture(eventstore)
    packet_report = check_packets()
    output.mkdir(parents=True)
    before = snapshot(ROOT)
    (output / "f1-packet-check.json").write_text(json.dumps(packet_report, sort_keys=True, indent=2) + "\n")
    (output / "source-sha256.json").write_text(json.dumps(before, sort_keys=True, indent=2) + "\n")
    (output / "owner-capture-source.json").write_text(json.dumps(owner_capture, sort_keys=True, indent=2) + "\n")
    evidence = output / "custody-binding-f1.json"
    xml = output / "runtime-tests.xml"
    environment = dict(os.environ, EXT_SECRETS_1_F1_EVIDENCE=str(evidence), HexalithBuildsRoot=str(builds))
    commands = [
        ["dotnet", "build", str(ROOT / PROJECT), "-c", "Debug", "--artifacts-path", str(artifacts), "-m:1",
         "-p:NuGetAudit=false", "-p:MinVerVersionOverride=1.0.0", "-p:UseHexalithProjectReferences=true",
         f"-p:HexalithBuildsRoot={builds}", f"-p:HexalithEventStoreRoot={eventstore}"],
        ["dotnet", str(artifacts / "bin/Hexalith.Platform.Secrets.Runtime.Tests/debug/Hexalith.Platform.Secrets.Runtime.Tests.dll"),
         "-result-xml", str(xml)],
    ]
    (output / "commands.json").write_text(json.dumps(commands, indent=2) + "\n")
    for command, log_name in zip(commands, ("runtime-build.log", "runtime-tests.log")):
        with (output / log_name).open("wb") as log:
            result = subprocess.run(command, cwd=ROOT, env=environment, stdout=log, stderr=subprocess.STDOUT,
                                    timeout=900, check=False)
        if result.returncode:
            raise ValueError(f"{log_name} failed with exit {result.returncode}; retained at {output}")
    build_log = (output / "runtime-build.log").read_text()
    if "0 Warning(s)" not in build_log or "0 Error(s)" not in build_log:
        raise ValueError("Runtime build was not warning/error free")
    counts, _ = audit(xml, evidence)
    if snapshot(ROOT) != before:
        raise ValueError("Source changed during F1 verification; retain this attempt and run a fresh one")
    for name, root in (("Builds", builds), ("EventStore", eventstore)):
        if git(root, "status", "--porcelain") or git(root, "rev-parse", "HEAD") != inputs[name]["commit"]:
            raise ValueError(f"{name} changed during F1 verification")
    report = {"kind": "SyntheticF1SourceContractOnly", "candidateBaseCommit": git(ROOT, "rev-parse", "HEAD"),
              "qualificationEffects": 0, "runtimeQualificationCompleted": False, "dependencyAvailable": False,
              "counts": counts, "sourceInputs": inputs,
              "sha256": {path.name: sha(path) for path in output.iterdir() if path.is_file()}}
    (output / "verification.json").write_text(json.dumps(report, sort_keys=True, indent=2) + "\n")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--builds-root", required=True)
    parser.add_argument("--eventstore-root", required=True)
    parser.add_argument("--artifacts", required=True)
    parser.add_argument("--output", required=True)
    arguments = parser.parse_args()
    print(json.dumps(run(arguments.builds_root, arguments.eventstore_root, arguments.artifacts, arguments.output), sort_keys=True))


if __name__ == "__main__":
    main()
