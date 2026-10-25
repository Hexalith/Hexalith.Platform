"""Shared packet admission. Public hashes are actual bytes; missing installed artifacts never acquire fixture substitutes."""
import hashlib
import json
from pathlib import Path
import re
from joint_live_phase_runner import calendar_date, digest, full_plan

REQUIRED_ARTIFACTS = ("custodyConfiguration", "hostProfile", "protectionProfile", "providerProfile", "authorityEnrollment",
                      "publicTrust", "executorAnchor", "observerAnchor", "enrollmentAnchor", "installedImageInventory", "physicalQualificationPlan", "jointConfiguration")


def section(text, record):
    match = re.search(r"^### " + re.escape(record) + r"\b[^\n]*\n(.*?)(?=^### |^## |\Z)", text, re.M | re.S)
    return match.group(0) if match else ""


def admission(context_path, register_path, candidate, clean, profile_date=None):
    missing, hashes, context = [], {}, {}
    if context_path is None:
        return ["ExplicitCoordinatedInstallationContext", "ExplicitPlatformIntegrationDate", "ExplicitProtectionIntegrationDate"], {}, {}
    import qualify as q
    try:
        context = q.read_json(context_path)
        raw = q.bounded_public_file(register_path, 1024 * 1024)
        if hashlib.sha256(raw).hexdigest() != context.get("acceptedRegisterSha256"):
            missing.append("ExactAcceptedRegisterBytes")
        text = raw.decode("utf-8")
        for name in ("platformIntegrationDate", "protectionIntegrationDate"):
            try:
                calendar_date(context.get(name))
            except (TypeError, ValueError):
                missing.append(name)
        for record in ("EXT-SECRETS-1", "EXT-HOST-1", "EXT-PROTECTION-1"):
            date = context.get("platformIntegrationDate" if record != "EXT-PROTECTION-1" else "protectionIntegrationDate")
            if f'| `TargetIntegrationDate` | `{date}` |' not in section(text, record):
                missing.append(record + ".ExplicitAcceptedIntegrationDate")
        if profile_date is not None and profile_date != context.get("platformIntegrationDate"):
            missing.append("EqualRegisterPacketProfileDates")
        if candidate is None or context.get("sourceCommit") != candidate or not clean:
            missing.append("ImmutableCompleteSharedPlatformTarget")
        for name in REQUIRED_ARTIFACTS:
            descriptor = context.get("installedArtifacts", {}).get(name, {})
            try:
                data = q.bounded_public_file(descriptor["path"], 1024 * 1024)
                actual = hashlib.sha256(data).hexdigest()
                if descriptor.get("sha256") != actual:
                    raise ValueError("installed bytes changed")
                if name.endswith("Anchor"):
                    q.public_key_identity(data)
                hashes[name + "Sha256"] = actual
            except (OSError, KeyError, ValueError, TypeError, ImportError):
                hashes[name + "Sha256"] = None
                missing.append("installedArtifacts." + name)
        anchors = []
        for name in ("executorAnchor", "observerAnchor", "enrollmentAnchor"):
            try:
                anchors.append(q.public_key_identity(q.read_public_anchor(context["installedArtifacts"][name]["path"])))
            except (OSError, KeyError, ValueError, TypeError, ImportError):
                missing.append("IndependentInstalledTrustBytes")
        if len(anchors) != 3 or len(set(anchors)) != 3:
            missing.append("IndependentInstalledTrustBytes")
        if context.get("planSha256") != digest(full_plan()):
            missing.append("FullCoordinatedQualificationPlan")
        try:
            import base64
            import time
            from cryptography.hazmat.primitives import serialization, hashes as crypto_hashes
            from cryptography.hazmat.primitives.asymmetric import ec
            pins = []
            for record in ("EXT-SECRETS-1", "EXT-HOST-1"):
                match = re.search(r"\| `InstallationAuthorityBinding` \| `([^`]+)` \|", section(text, record))
                pins.append(q.decode_json(match.group(1)))
            if pins[0] != pins[1]:
                raise ValueError("independent installation authority differs")
            pin = pins[0]
            anchor = q.read_public_anchor(pin["anchorFile"])
            if hashlib.sha256(anchor).hexdigest() != pin["anchorSha256"]:
                raise ValueError("enrolled authority key substituted")
            q.public_key_identity(anchor)
            envelope = q.read_json(context["installationProofFile"])
            payload = envelope["payload"]
            key = serialization.load_pem_public_key(anchor)
            key.verify(base64.b64decode(envelope["signature"], validate=True), q.canonical(payload), ec.ECDSA(crypto_hashes.SHA256()))
            if (payload["identity"] != pin["identity"] or payload["authorityRevision"] != pin["revision"]
                    or payload["sourceCommit"] != candidate or payload["installedHashes"] != hashes
                    or payload["platformIntegrationDate"] != context["platformIntegrationDate"]
                    or payload["protectionIntegrationDate"] != context["protectionIntegrationDate"]
                    or payload["planSha256"] != digest(full_plan())
                    or payload["exactCommands"] != context.get("exactCommands")
                    or not time.time() - 30 <= payload["observedAt"] <= time.time() < payload["validUntil"] <= payload["observedAt"] + 30):
                raise ValueError("current exact independent installation proof required")
        except Exception:
            missing.append("IndependentCurrentInstallationProof")
        commands = context.get("exactCommands", {})
        for record in ("EXT-SECRETS-1", "EXT-HOST-1"):
            command = commands.get(record)
            if not isinstance(command, str) or not candidate or candidate not in command or "status --porcelain" not in command:
                missing.append(record + ".CompleteExactCommand")
        # Explicit externally enrolled artifact inventory, never a local installed=true flag.
        if not all(context.get("installedArtifacts", {}).get(name, {}).get("enrollmentReference") for name in REQUIRED_ARTIFACTS):
            missing.append("IndependentInstallationEnrollmentReferences")
        return sorted(set(missing)), hashes, context
    except (OSError, ValueError, TypeError, KeyError):
        return ["ValidExplicitCoordinatedInstallationContext"], hashes, context


def shared_binding(installed_hashes, coordinated):
    from cases import plan_digest
    return {"jointConfigurationFile": coordinated["installedArtifacts"]["jointConfiguration"]["path"], "jointConfigurationSha256": installed_hashes["jointConfigurationSha256"],
            "configurationSha256": installed_hashes["custodyConfigurationSha256"],
            "profileSha256": installed_hashes["hostProfileSha256"], "planSha256": plan_digest(),
            "cohortPlanSha256": digest(full_plan()),
            **{name + "AnchorSha256": installed_hashes[name + "AnchorSha256"] for name in ("executor", "observer", "enrollment")}}


def records(register_path, target, platform_date, commands, bindings):
    """Preserve all nine fields and every extension verbatim except explicit proposed fields. Acceptance remains external."""
    import qualify as q
    text = q.bounded_public_file(register_path, 1024 * 1024).decode("utf-8")
    result = {}
    for record in ("EXT-SECRETS-1", "EXT-HOST-1"):
        original = section(text, record)
        proposed = re.sub(r"(\| `TargetVersionOrCommit` \| )`[^`]+`", lambda m: m[1] + "`" + target + "`", original)
        proposed = re.sub(r"(\| `TargetIntegrationDate` \| )`[^`]+`", lambda m: m[1] + "`" + calendar_date(platform_date) + "`", proposed)
        proposed = re.sub(r"Command: `[^`]+`", lambda _: "Command: `" + commands[record] + "`", proposed)
        joint = q.read_json(bindings["jointConfigurationFile"])
        joint_binding = {"configurationSha256": bindings["jointConfigurationSha256"], "bindingSha256": digest(joint["binding"]), "planSha256": digest(full_plan())}
        proposed += "\n| `JointQualificationBinding` | `" + json.dumps(joint_binding, sort_keys=True, separators=(",", ":")) + "` |\n"
        if record == "EXT-SECRETS-1":
            proposed = re.sub(r"(\| `RuntimeQualificationBinding` \| )`[^`]+`", lambda m: m[1] + "`" + json.dumps(bindings, sort_keys=True, separators=(",", ":")) + "`", proposed)
        result[record] = {"acceptedSectionSha256": hashlib.sha256(original.encode()).hexdigest(), "acceptedVerbatimSection": original,
                          "proposedVerbatimSection": proposed + ("\n| `HostQualificationBinding` | `" + json.dumps(bindings, sort_keys=True, separators=(",", ":")) + "` |\n" if record == "EXT-HOST-1" else ""), "RuntimeQualificationBinding": bindings, "accepted": False, "availability": False}
    return result
