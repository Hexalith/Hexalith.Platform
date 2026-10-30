"""Shared packet admission. Public hashes are actual bytes; missing installed artifacts never acquire fixture substitutes."""
import hashlib
import json
from pathlib import Path
import re
from joint_live_phase_runner import calendar_date, digest, full_plan
from case_driver_inventory import blockers as driver_blockers
from packet_snapshot import PublicPacketSnapshot, AdmittedCoordination, snapshot_for

REQUIRED_ARTIFACTS = ("custodyConfiguration", "hostProfile", "protectionProfile", "providerProfile", "authorityEnrollment",
                      "publicTrust", "executorAnchor", "observerAnchor", "enrollmentAnchor", "installedImageInventory", "physicalQualificationPlan", "jointConfiguration", "launchInventory")


def section(text, record, *, complete=False):
    matches = list(re.finditer(r"^### " + re.escape(record) + r"\b[^\n]*\n(.*?)(?=^### |^## |\Z)", text, re.M | re.S))
    if not matches: return ''
    if len(matches) != 1:
        raise ValueError('ExactlyOneAcceptedSection.'+record)
    value=matches[0].group(0)
    rows=re.findall(r"^\| `([^`]+)` \| (.*?) \|\s*$",value,re.M)
    names=[name for name,_ in rows]
    if len(names)!=len(set(names)):
        raise ValueError('UniqueAcceptedCommitmentFields.'+record)
    for name in (('Owner','Repository','RequiredArtifact','TargetVersionOrCommit','TargetIntegrationDate','CompatibilityContractAndVerificationCommand','RequiredEvidenceLevel','AcceptedStatus','ConsumingStories') if complete else ()):
        if names.count(name)!=1: raise ValueError('ExactlyOneAcceptedCommitment.'+record+'.'+name)
    return value


def admission(context_path, register_path, candidate, clean, profile_date=None):
    import bounded_file_io as selected_io
    try: return selected_io.run(lambda:_admission(context_path,register_path,candidate,clean,profile_date))
    except TimeoutError: return ["CoordinatedAdmissionInputDeadlineOrCapacity"],{},{}


def _admission(context_path, register_path, candidate, clean, profile_date=None):
    missing, hashes, context = list(driver_blockers()), {}, {}
    if context_path is None:
        return ["ExplicitCoordinatedInstallationContext", "ExplicitPlatformIntegrationDate", "ExplicitProtectionIntegrationDate"], {}, {}
    import qualify as q
    try:
        snapshot = PublicPacketSnapshot()
        parsed_context = snapshot.json(context_path, capture=True)
        if not isinstance(parsed_context, dict):
            return ["ValidExplicitCoordinatedInstallationContext"], {}, {}
        context = AdmittedCoordination(parsed_context, snapshot)
        if not isinstance(context, dict) or not isinstance(context.get("installedArtifacts"), dict) or not isinstance(context.get("exactCommands"), dict):
            return ["ValidExplicitCoordinatedInstallationContext"], {}, {}
        if any(not isinstance(value, dict) for value in context["installedArtifacts"].values()):
            return ["ValidExplicitCoordinatedInstallationContext"], {}, {}
        raw = snapshot.capture(register_path)
        if hashlib.sha256(raw).hexdigest() != context.get("acceptedRegisterSha256"):
            missing.append("ExactAcceptedRegisterBytes")
        text = raw.decode("utf-8")
        for record in ("EXT-SECRETS-1","EXT-HOST-1","EXT-PROTECTION-1"):
            if not section(text,record,complete=True): raise ValueError('CompleteAcceptedSection.'+record)
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
                data = snapshot.capture(descriptor["path"])
                actual = hashlib.sha256(data).hexdigest()
                if descriptor.get("sha256") != actual:
                    raise ValueError("installed bytes changed")
                if name.endswith("Anchor"):
                    q.public_key_identity(data)
                hashes[name + "Sha256"] = actual
            except (OSError, KeyError, ValueError, TypeError, ImportError):
                hashes[name + "Sha256"] = None
                missing.append("installedArtifacts." + name)
        try:
            joint = snapshot.json(context["installedArtifacts"]["jointConfiguration"]["path"])
            missing.extend(validate_joint_configuration(joint, hashes, context, snapshot=snapshot))
            from launch_inventory import blockers as launch_blockers
            launch=snapshot.json(context["installedArtifacts"]["launchInventory"]["path"])
            if joint["binding"].get("launchInventorySha256")!=digest(launch):missing.append("ExactAcceptedCompleteLaunchInventoryBinding")
            missing.extend(launch_blockers(launch,snapshot.json(context["installedArtifacts"]["hostProfile"]["path"])))
            protection = section(text, "EXT-PROTECTION-1")
            expected_protection = {"targetCommit": joint["binding"]["protectionCommit"],
                "integrationDate": calendar_date(context["protectionIntegrationDate"]),
                "profileSha256": hashes["protectionProfileSha256"],
                "configurationSha256": hashes["jointConfigurationSha256"], "planSha256": digest(full_plan())}
            accepted_protection = re.search(r"\| `ProtectionQualificationBinding` \| `([^`]+)` \|", protection)
            if (accepted_protection is None or q.decode_json(accepted_protection[1]) != expected_protection
                    or f'| `TargetVersionOrCommit` | `{expected_protection["targetCommit"]}` |' not in protection
                    or not re.search(r"\| `AcceptedStatus` \| `(?:Committed|Available)` \|", protection)):
                missing.append("ExactAcceptedCompleteProtectionBinding")
            context["acceptedProtectionSectionSha256"] = hashlib.sha256(protection.encode()).hexdigest()
        except (OSError,ValueError,TypeError,KeyError,AttributeError):
            missing.append("JointConfiguration.Structure")
        anchors = []
        for name in ("executorAnchor", "observerAnchor", "enrollmentAnchor"):
            try:
                anchors.append(q.public_key_identity(snapshot.capture(context["installedArtifacts"][name]["path"],4096)))
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
            anchor = snapshot.capture(pin["anchorFile"],4096)
            if hashlib.sha256(anchor).hexdigest() != pin["anchorSha256"]:
                raise ValueError("enrolled authority key substituted")
            q.public_key_identity(anchor)
            envelope = snapshot.json(context["installationProofFile"],capture=True)
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
            else:
                import shlex
                try:
                    accepted_command=re.search(r'Command: `([^`]+)`',section(text,record))
                    tokens=shlex.split(command)
                    if not valid_owner_command(command,record,candidate,context["installedArtifacts"]["jointConfiguration"]["path"]):
                        missing.append(record+".ClosedCompleteOwnerCommand")
                    if accepted_command is None or accepted_command[1]!=command:
                        missing.append(record+".EqualAcceptedCoordinatedCommand")
                    joint_argument="PLATFORM_JOINT_COHORT_CONFIGURATION="+context["installedArtifacts"]["jointConfiguration"]["path"]
                    if tokens.count(joint_argument)!=1 or any(value.startswith("PLATFORM_JOINT_COHORT_CONFIGURATION=") and value!=joint_argument for value in tokens):
                        missing.append(record+".ExactJointConfigurationCommandPath")
                    marker="../platform/eng/verify-ext-secrets-1.ps1" if record=="EXT-SECRETS-1" else "../platform/eng/verify-agents-host.sh"
                    if tokens.count(marker)!=1 or any(value in tokens for value in (";","|","||","&")):
                        missing.append(record+".ClosedCompleteOwnerCommand")
                    if record=="EXT-SECRETS-1":
                        suffix=tokens[tokens.index(marker)+1:]
                        if len(suffix)!=8 or suffix[0:2]!=["-Mode","Live"] or suffix[2]!="-EventStoreSourceRoot" or suffix[4]!="-ArtifactsDirectory" or suffix[6]!="-EvidenceDirectory":
                            missing.append(record+".ClosedCompleteOwnerCommand")
                    else:
                        suffix=tokens[tokens.index(marker)+1:]
                        if len(suffix)!=6 or suffix[0:2]!=["--mode","Full"] or suffix[2]!="--artifacts-path" or suffix[4]!="--profile":
                            missing.append(record+".ClosedCompleteOwnerCommand")
                except (KeyError,TypeError,ValueError): missing.append(record+".ClosedCompleteOwnerCommand")
        # Explicit externally enrolled artifact inventory, never a local installed=true flag.
        if not all(context.get("installedArtifacts", {}).get(name, {}).get("enrollmentReference") for name in REQUIRED_ARTIFACTS):
            missing.append("IndependentInstallationEnrollmentReferences")
        return sorted(set(missing)), hashes, context
    except (OSError, ValueError, TypeError, KeyError, AttributeError, ImportError):
        return ["ValidExplicitCoordinatedInstallationContext"], hashes, context


def valid_owner_command(command, record, candidate, joint_path):
    """Recognize the shipped shell grammar without evaluating any untrusted command."""
    import shlex
    prefix = (f'test "$(git -C ../platform rev-parse HEAD)" = "{candidate}" && '
              'test -z "$(git -C ../platform status --porcelain)" && ')
    if not isinstance(command, str) or not re.fullmatch(r'[a-f0-9]{40}', candidate or '') or not command.startswith(prefix):
        return False
    remainder = command[len(prefix):]
    try:
        tokens = shlex.split(remainder)
        names = ['PLATFORM_JOINT_COHORT_CONFIGURATION']
        if record == 'EXT-SECRETS-1':
            names += ['EXT_SECRETS_1_RUNTIME_CONFIGURATION', 'EXT_SECRETS_1_ACCEPTANCE_PACKET']
            owner = ['pwsh', '-NoProfile', '-File', '../platform/eng/verify-ext-secrets-1.ps1']
            flags = ['-Mode', 'Live', '-EventStoreSourceRoot', None, '-ArtifactsDirectory', None, '-EvidenceDirectory', None]
        elif record == 'EXT-HOST-1':
            owner = ['bash', '../platform/eng/verify-agents-host.sh']
            flags = ['--mode', 'Full', '--artifacts-path', None, '--profile', None]
        else:
            return False
        if tokens[:1] != ['env'] or len(tokens) != 1 + len(names) + len(owner) + len(flags):
            return False
        assignments = tokens[1:1+len(names)]
        for name, item in zip(names, assignments):
            if not item.startswith(name+'=') or not Path(item[len(name)+1:]).is_absolute():
                return False
        if assignments[0] != names[0]+'='+joint_path:
            return False
        tail = tokens[1+len(names):]
        if tail[:len(owner)] != owner or any(expected is not None and actual != expected for actual, expected in zip(tail[len(owner):], flags)):
            return False
        # Canonical quoting makes every supplied path literal, including shell metacharacters.
        canonical = 'env ' + ' '.join(name+'='+shlex.quote(item[len(name)+1:]) for name,item in zip(names,assignments)) + ' ' + shlex.join(tail)
        return remainder == canonical
    except (ValueError, TypeError, IndexError):
        return False


def shared_binding(installed_hashes, coordinated):
    from cases import plan_digest
    return {"jointConfigurationFile": coordinated["installedArtifacts"]["jointConfiguration"]["path"], "jointConfigurationSha256": installed_hashes["jointConfigurationSha256"],
            "configurationSha256": __import__("qualify").digest(snapshot_for(coordinated).json(coordinated["installedArtifacts"]["custodyConfiguration"]["path"])),
            "configurationRawSha256": installed_hashes["custodyConfigurationSha256"],
            "profileSha256": installed_hashes["hostProfileSha256"], "planSha256": plan_digest(),
            "cohortPlanSha256": digest(full_plan()),"launchInventorySha256":installed_hashes["launchInventorySha256"],
            "protectionBinding": protection_binding(installed_hashes, coordinated),
            **{name + "AnchorSha256": installed_hashes[name + "AnchorSha256"] for name in ("executor", "observer", "enrollment")}}


def protection_binding(installed_hashes, coordinated):
    import qualify as q
    joint = snapshot_for(coordinated).json(coordinated["installedArtifacts"]["jointConfiguration"]["path"])
    return {"targetCommit": joint["binding"]["protectionCommit"], "integrationDate": calendar_date(coordinated["protectionIntegrationDate"]),
        "profileSha256": installed_hashes["protectionProfileSha256"], "configurationSha256": installed_hashes["jointConfigurationSha256"],
        "planSha256": digest(full_plan()), "acceptedSectionSha256": coordinated["acceptedProtectionSectionSha256"]}


def records(register_path, target, platform_date, commands, bindings, *, snapshot):
    """Preserve all nine fields and every extension verbatim except explicit proposed fields. Acceptance remains external."""
    import qualify as q
    text = snapshot.retained(register_path).decode("utf-8")
    result = {}
    for record in ("EXT-SECRETS-1", "EXT-HOST-1"):
        original = section(text, record)
        proposed = re.sub(r"(\| `TargetVersionOrCommit` \| )`[^`]+`", lambda m: m[1] + "`" + target + "`", original)
        proposed = re.sub(r"(\| `TargetIntegrationDate` \| )`[^`]+`", lambda m: m[1] + "`" + calendar_date(platform_date) + "`", proposed)
        proposed = re.sub(r"Command: `[^`]+`", lambda _: "Command: `" + commands[record] + "`", proposed)
        joint = snapshot.json(bindings["jointConfigurationFile"])
        joint_binding = {"configurationSha256": bindings["jointConfigurationSha256"], "bindingSha256": digest(joint["binding"]), "planSha256": digest(full_plan())}
        proposed = replace_binding_row(proposed, "JointQualificationBinding", joint_binding)
        if record == "EXT-SECRETS-1":
            proposed = replace_binding_row(proposed, "RuntimeQualificationBinding", bindings)
        result[record] = {"acceptedSectionSha256": hashlib.sha256(original.encode()).hexdigest(), "acceptedVerbatimSection": original,
                          "proposedVerbatimSection": replace_binding_row(proposed, "HostQualificationBinding", bindings) if record == "EXT-HOST-1" else proposed, "RuntimeQualificationBinding": bindings, "accepted": False, "availability": False}
    return result


def validate_joint_configuration(config, installed_hashes=None, context=None, *, snapshot=None):
    """Validate every public pin without network calls or reserving an attempt."""
    import qualify as q
    from joint_live_phase_runner import PHASES, partitions
    from urllib.parse import urlparse
    missing = []
    if not isinstance(config, dict) or not isinstance(config.get('binding'), dict):
        return ['JointConfiguration.Structure']
    binding = config['binding']
    commit = binding.get('platformCommit')
    if not isinstance(commit, str) or not re.fullmatch('[0-9a-f]{40}', commit) or any(binding.get(n) != commit for n in ('secretsCommit', 'hostCommit')):
        missing.append('JointConfiguration.SharedPlatformTarget')
    if not isinstance(binding.get('protectionCommit'), str) or not re.fullmatch('[0-9a-f]{40}', binding['protectionCommit']):
        missing.append('JointConfiguration.ProtectionTarget')
    for name in ('platformIntegrationDate', 'protectionIntegrationDate'):
        try: calendar_date(binding.get(name))
        except (TypeError, ValueError): missing.append('JointConfiguration.' + name)
    if binding.get('host') != '192.168.1.30' or binding.get('partiesBranch') != 'B' or binding.get('timing') != {'L':300,'S':30,'O':600,'H':86400,'R':604800}:
        missing.append('JointConfiguration.AcceptedConstants')
    if binding.get('planSha256') != digest(full_plan()): missing.append('JointConfiguration.FullPlan')
    for name in ('configurationSha256','profileSha256','protectionProfileSha256','executorAnchorSha256','observerAnchorSha256','enrollmentAnchorSha256','currentTrustSha256','installedImageSha256'):
        if not isinstance(binding.get(name), str) or not re.fullmatch('[0-9a-f]{64}', binding[name]): missing.append('JointConfiguration.' + name)
    endpoint = config.get('daprEndpoint')
    try:
        parsed = urlparse(endpoint)
        if parsed.scheme != 'http' or parsed.hostname not in ('localhost','127.0.0.1','::1') or parsed.path not in ('','/') or parsed.query or parsed.fragment or parsed.username or parsed.password:
            raise ValueError()
        _ = parsed.port
    except (TypeError, ValueError, AttributeError): missing.append('JointConfiguration.LoopbackDaprEndpoint')
    if not isinstance(config.get('daprCredentialReference'), str) or not config['daprCredentialReference'].startswith('file:/'):
        missing.append('JointConfiguration.DaprCredentialReference')
    services = config.get('services')
    fingerprints, apps = [], []
    if not isinstance(services, dict) or set(services) != {*PHASES, 'authority'}:
        missing.append('JointConfiguration.CompleteIndependentServices')
    else:
        for role, service in services.items():
            try:
                if not isinstance(service, dict) or not isinstance(service.get('appId'), str) or not re.fullmatch('[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}', service['appId']) \
                        or not isinstance(service.get('credentialReference'), str) or not service['credentialReference'].startswith('file:/'):
                    raise ValueError()
                raw = q.read_public_anchor(service['publicAnchorFile']) if snapshot is None else snapshot.capture(service['publicAnchorFile'],4096)
                if hashlib.sha256(raw).hexdigest() != service['publicAnchorSha256']: raise ValueError()
                fingerprints.append(q.public_key_identity(raw)); apps.append(service['appId'])
            except (OSError, KeyError, TypeError, ValueError, ImportError): missing.append('JointConfiguration.services.' + role)
        if len(set(fingerprints)) != 5 or len(set(apps)) != 5: missing.append('JointConfiguration.IndependentServiceTrust')
    profiles, pins = config.get('phaseExecutionProfiles'), binding.get('phaseExecutionProfileSha256')
    if not isinstance(profiles, dict) or not isinstance(pins, dict) or set(profiles) != {'0','1','2','3'} or set(pins) != set(profiles):
        missing.append('JointConfiguration.CompletePhaseProfiles')
    else:
        for phase in range(4):
            profile = profiles[str(phase)]
            try:
                if not isinstance(profile, dict) or profile.get('phase') != phase or profile.get('cases') != partitions()[phase] \
                        or pins[str(phase)] != digest({k:v for k,v in profile.items() if k != 'bindingDigest'}) \
                        or profile.get('bindingDigest') != digest(binding) or not isinstance(profile.get('command'), list) \
                        or not profile['command'] or any(not isinstance(arg, str) or not arg for arg in profile['command']) \
                        or profile['command'].count("--phase-command") != 1 or profile.get("recoveryCommand") != ["--phase-finalize" if arg == "--phase-command" else arg for arg in profile["command"]] \
                        or not isinstance(profile.get('environment'), dict) or any(not isinstance(k,str) or not isinstance(v,str) for k,v in profile['environment'].items()) \
                        or not isinstance(profile.get('workingDirectory'), str) or not Path(profile['workingDirectory']).is_absolute() \
                        or type(profile.get('completionTimeoutSeconds')) is not int or not 1 <= profile['completionTimeoutSeconds'] <= 86400 \
                        or not isinstance(profile.get('artifacts'), list) or not profile['artifacts']:
                    raise ValueError()
                from execution_profile import validate_execution_profile
                missing.extend('JointConfiguration.phaseExecutionProfiles.'+str(phase)+'.'+item for item in validate_execution_profile(profile))
            except (OSError, KeyError, ValueError, TypeError): missing.append('JointConfiguration.phaseExecutionProfiles.' + str(phase))
    if installed_hashes is not None and context is not None:
        mapping = {'profileSha256':'hostProfileSha256','protectionProfileSha256':'protectionProfileSha256','currentTrustSha256':'publicTrustSha256',
                   'installedImageSha256':'installedImageInventorySha256', **{r+'AnchorSha256':r+'AnchorSha256' for r in ('executor','observer','enrollment')}}
        for field, installed in mapping.items():
            if binding.get(field) != installed_hashes.get(installed): missing.append('JointConfiguration.CrossArtifact.' + field)
        try:
            raw = snapshot_for(context).retained(context['installedArtifacts']['custodyConfiguration']['path'])
            if binding.get('configurationSha256') != q.digest(q.decode_json(raw)) or binding.get('configurationRawSha256') != hashlib.sha256(raw).hexdigest():
                missing.append('JointConfiguration.CrossArtifact.configurationSha256')
            protection = snapshot_for(context).json(context['installedArtifacts']['protectionProfile']['path'])
            if protection.get('targetCommit') != binding.get('protectionCommit'): missing.append('JointConfiguration.CrossArtifact.protectionCommit')
            profile = snapshot_for(context).json(context['installedArtifacts']['hostProfile']['path'])
            if profile.get('integrationDate') != context.get('platformIntegrationDate'): missing.append('JointConfiguration.CrossArtifact.hostDate')
        except (OSError, KeyError, ValueError, TypeError): missing.append('JointConfiguration.CrossArtifact.Structure')
        if binding.get('platformCommit') != context.get('sourceCommit') or any(binding.get(n) != context.get(n) for n in ('platformIntegrationDate','protectionIntegrationDate')):
            missing.append('JointConfiguration.CrossArtifact.TargetAndDates')
    return sorted(set(missing))


def replace_binding_row(text, name, binding):
    row = '| `' + name + '` | `' + json.dumps(binding, sort_keys=True, separators=(',', ':')) + '` |'
    pattern = r'^\| `' + re.escape(name) + r'` \|.*\n?'
    matches = list(re.finditer(pattern, text, re.M))
    if not matches: return text + '\n' + row + '\n'
    first = matches[0].start()
    clean = re.sub(pattern, '', text, flags=re.M)
    return clean[:first] + row + '\n' + clean[first:]
