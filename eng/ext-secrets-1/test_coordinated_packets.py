"""Ephemeral packet fixtures; signatures and artifacts here are never installed qualification."""
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import shlex
import tempfile
import time
import unittest
from unittest.mock import patch

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec

import coordinated_packets as coordinated
import prepare_acceptance as secrets_prepare
import qualify as q
from joint_live_phase_runner import digest, full_plan, PHASES, partitions
from test_qualify import Fixture

spec = importlib.util.spec_from_file_location("coordinated_host_prepare", Path(__file__).parents[1] / "ext-host-1" / "prepare_acceptance.py")
host = importlib.util.module_from_spec(spec)
spec.loader.exec_module(host)


class CoordinatedPacketTests(unittest.TestCase):
    def setUp(self):
        # Isolate packet binding mechanics from the explicitly incomplete shipped driver inventory.
        self.driver_patch = patch('coordinated_packets.driver_blockers', return_value=[])
        self.driver_patch.start(); self.addCleanup(self.driver_patch.stop)
        # Isolated packet/inventory mechanics cannot supply the absent independent whole-image protocol.
        image_patch=patch('execution_profile.installed_image_blockers',return_value=[])
        image_patch.start();self.addCleanup(image_patch.stop)
        launch_patch=patch('launch_inventory.blockers',return_value=[])
        launch_patch.start();self.addCleanup(launch_patch.stop)
        self.temp = tempfile.TemporaryDirectory(prefix="fixture-shared-packet-")
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.fixture = Fixture(self.directory)
        self.secret_output, self.host_output = self.directory / "secret-packet", self.directory / "host-packet"
        self.profile = host.default_profile()
        self.profile["integrationDate"] = "2028-02-29"
        self.profile["replicaCount"] = 3
        for name in host.REFERENCE_KEYS:
            self.profile[name] = "fixture-" + name
        from test_launch_inventory import fixture_listeners,fixture_ui_artifacts
        self.profile['listeners']=fixture_listeners()
        self.profile_path = self.directory / "host.json"
        self.profile_path.write_bytes(host.canonical(self.profile))
        self.joint = self.directory / "joint configuration.json"
        self.joint.write_bytes(q.canonical({"binding": {"platformCommit": self.fixture.target, "fixtureOnly": True}}))
        prefix = (f'test "$(git -C ../platform rev-parse HEAD)" = "{self.fixture.target}" && '
                  'test -z "$(git -C ../platform status --porcelain)" && '
                  'env PLATFORM_JOINT_COHORT_CONFIGURATION=' + shlex.quote(str(self.joint)) + ' ')
        self.commands = {
            "EXT-SECRETS-1": prefix + 'EXT_SECRETS_1_RUNTIME_CONFIGURATION=' + shlex.quote(str(self.secret_output / 'runtime-reference.json'))
                + ' EXT_SECRETS_1_ACCEPTANCE_PACKET=' + shlex.quote(str(self.secret_output / 'replacement-acceptance-packet.json'))
                + ' ' + q.command_suffix('../eventstore', '/tmp/ext-secrets-artifacts', '/tmp/ext-secrets-evidence'),
            "EXT-HOST-1": prefix + 'bash ../platform/eng/verify-agents-host.sh --mode Full --artifacts-path /tmp/ext-host-1-full --profile '
                + shlex.quote(str(self.host_output / 'agents-deployment-profile.json')),
        }
        self.key = ec.generate_private_key(ec.SECP256R1())
        anchor = self.directory / "fixture-installation-authority.pem"
        anchor.write_bytes(self.key.public_key().public_bytes(serialization.Encoding.PEM, serialization.PublicFormat.SubjectPublicKeyInfo))
        pin = {"anchorFile": str(anchor), "anchorSha256": hashlib.sha256(anchor.read_bytes()).hexdigest(), "identity": "fixture-authority", "revision": "fixture-1"}
        parts = []
        for record in ("EXT-SECRETS-1", "EXT-HOST-1", "EXT-PROTECTION-1"):
            command = self.commands.get(record, "fixture-protection-owner")
            fields = {"Owner": "fixture-owner", "Repository": "fixture-repository", "RequiredArtifact": "S1–S4/v23; H1–H4/v21/v23",
                      "TargetVersionOrCommit": self.fixture.target, "TargetIntegrationDate": self.profile["integrationDate"],
                      "CompatibilityContractAndVerificationCommand": "Complete fixture scope. Command: `" + command + "`.",
                      "RequiredEvidenceLevel": "Levels 4 and 5", "AcceptedStatus": "Committed", "ConsumingStories": "5.4; RQ-1"}
            body = "### " + record + " — isolated fixture\n"
            for name, value in fields.items():
                body += '| `' + name + '` | ' + (value if name == 'CompatibilityContractAndVerificationCommand' else '`' + value + '`') + ' |\n'
            if record != "EXT-PROTECTION-1":
                body += '| `InstallationAuthorityBinding` | `' + json.dumps(pin) + '` |\n'
            body += '\nRequired v21/v23 extensions; 365-day history; both Product gates remain Open.\n\n'
            parts.append(body)
        self.fixture.register.write_text(''.join(parts) + '### OQ-5.4-1 — isolated fixture\n')
        artifacts = {}
        for name in coordinated.REQUIRED_ARTIFACTS:
            if name == 'custodyConfiguration': path = self.fixture.path
            elif name == 'hostProfile': path = self.profile_path
            elif name == 'jointConfiguration': path = self.joint
            elif name == 'launchInventory':
                path=self.directory/'launchInventory.json';path.write_bytes(q.canonical({'protocol':'AcceptedCompleteImmutableLaunchGraphV1','resources':[{'resource':resource,'listener':value,'entryPoint':'executable','files':[{'path':name,'bytes':len(body),'sha256':hashlib.sha256(body).hexdigest()} for name,body in fixture_ui_artifacts(self.profile['listeners']['agents-ui']['uiHost']).items()]} for resource,value in self.profile['listeners'].items()]}))
            elif name.endswith('Anchor'): path = Path(self.fixture.config['qualification'][name[:-6]]['publicAnchorFile'])
            else:
                path = self.directory / (name + '.json')
                path.write_bytes(q.canonical({'fixtureOnly': name}))
            artifacts[name] = {'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'enrollmentReference': 'fixture-enrollment-' + name}
        protection = Path(artifacts['protectionProfile']['path'])
        protection.write_bytes(q.canonical({'targetCommit':'c'*40,'fixtureOnly':True}))
        artifacts['protectionProfile']['sha256'] = hashlib.sha256(protection.read_bytes()).hexdigest()
        executable = self.directory / 'fixture-owner-executable'
        executable.write_text('fixture executable: never invoked')
        binding = {'platformCommit':self.fixture.target,'secretsCommit':self.fixture.target,'hostCommit':self.fixture.target,'protectionCommit':'c'*40,
                   'platformIntegrationDate':self.profile['integrationDate'],'protectionIntegrationDate':self.profile['integrationDate'],
                   'planSha256':digest(full_plan()),'host':'192.168.1.30','partiesBranch':'B','timing':{'L':300,'S':30,'O':600,'H':86400,'R':604800},
                   'configurationSha256':q.digest(self.fixture.config),'configurationRawSha256':artifacts['custodyConfiguration']['sha256'],
                   'profileSha256':artifacts['hostProfile']['sha256'],'protectionProfileSha256':artifacts['protectionProfile']['sha256'],
                   'currentTrustSha256':artifacts['publicTrust']['sha256'],'installedImageSha256':artifacts['installedImageInventory']['sha256'],
                   **{r+'AnchorSha256':artifacts[r+'Anchor']['sha256'] for r in ('executor','observer','enrollment')}}
        from test_execution_profile import fixture_profile
        profiles = {str(phase):fixture_profile(self.directory/('phase-'+str(phase)),phase,partitions()[phase]) for phase in range(4)}
        binding['launchInventorySha256']=digest(q.read_json(artifacts['launchInventory']['path']))
        binding['phaseExecutionProfileSha256'] = {phase:digest(profile) for phase,profile in profiles.items()}
        for profile in profiles.values(): profile['bindingDigest'] = digest(binding)
        services = {}
        for role in (*PHASES,'authority'):
            key = ec.generate_private_key(ec.SECP256R1())
            anchor = self.directory / ('fixture-service-' + role + '.pem')
            anchor.write_bytes(key.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo))
            services[role] = {'appId':'fixture-'+role,'credentialReference':'file:/fixture/'+role,'publicAnchorFile':str(anchor),'publicAnchorSha256':hashlib.sha256(anchor.read_bytes()).hexdigest()}
        self.joint_configuration = {'binding':binding,'phaseExecutionProfiles':profiles,'services':services,'daprEndpoint':'http://127.0.0.1:3500','daprCredentialReference':'file:/fixture/dapr'}
        self.joint.write_bytes(q.canonical(self.joint_configuration))
        artifacts['jointConfiguration']['sha256'] = hashlib.sha256(self.joint.read_bytes()).hexdigest()
        protection_binding = {'targetCommit': binding['protectionCommit'], 'integrationDate': self.profile['integrationDate'],
            'profileSha256': artifacts['protectionProfile']['sha256'], 'configurationSha256': artifacts['jointConfiguration']['sha256'], 'planSha256': digest(full_plan())}
        protection_section = coordinated.section(self.fixture.register.read_text(), 'EXT-PROTECTION-1')
        protection_section_retargeted = protection_section.replace(f'| `TargetVersionOrCommit` | `{self.fixture.target}` |', f'| `TargetVersionOrCommit` | `{binding["protectionCommit"]}` |')
        revised = coordinated.replace_binding_row(protection_section_retargeted, 'ProtectionQualificationBinding', protection_binding)
        self.fixture.register.write_text(self.fixture.register.read_text().replace(protection_section, revised))
        self.context = {'sourceCommit': self.fixture.target, 'acceptedRegisterSha256': hashlib.sha256(self.fixture.register.read_bytes()).hexdigest(),
                        'platformIntegrationDate': self.profile['integrationDate'], 'protectionIntegrationDate': self.profile['integrationDate'],
                        'installedArtifacts': artifacts, 'planSha256': digest(full_plan()), 'exactCommands': self.commands,
                        'installationProofFile': str(self.directory / 'fixture-proof.json')}
        self.context_path = self.directory / 'fixture-context.json'
        self.sign_context()

    def sign_context(self):
        payload = {'identity': 'fixture-authority', 'authorityRevision': 'fixture-1', 'sourceCommit': self.fixture.target,
                   'installedHashes': {name + 'Sha256': descriptor['sha256'] for name, descriptor in self.context['installedArtifacts'].items()},
                   'platformIntegrationDate': self.context['platformIntegrationDate'], 'protectionIntegrationDate': self.context['protectionIntegrationDate'],
                   'planSha256': digest(full_plan()), 'exactCommands': self.context['exactCommands'], 'observedAt': int(time.time()), 'validUntil': int(time.time()) + 30}
        proof = {'payload': payload, 'signature': base64.b64encode(self.key.sign(q.canonical(payload), ec.ECDSA(hashes.SHA256()))).decode()}
        Path(self.context['installationProofFile']).write_bytes(q.canonical(proof))
        self.context_path.write_bytes(q.canonical(self.context))

    def test_common_runner_requires_exact_protection_binding_before_creating_live_ports(self):
        import joint_live_phase_runner as runner
        binding=self.joint_configuration['binding']
        pins={'configurationSha256':hashlib.sha256(self.joint.read_bytes()).hexdigest(),'bindingSha256':digest(binding),'planSha256':digest(full_plan())}
        text=self.fixture.register.read_text()
        for record in ('EXT-SECRETS-1','EXT-HOST-1'):
            before=coordinated.section(text,record);text=text.replace(before,coordinated.replace_binding_row(before,'JointQualificationBinding',pins))
        original=coordinated.section(text,'EXT-PROTECTION-1')
        for fault in ('missing','profile','configuration','plan'):
            if fault=='missing':
                changed=__import__('re').sub(r'\| `ProtectionQualificationBinding` \| `[^`]+` \|\n','',original)
            else:
                value=q.decode_json(__import__('re').search(r'\| `ProtectionQualificationBinding` \| `([^`]+)` \|',original).group(1))
                value[{'profile':'profileSha256','configuration':'configurationSha256','plan':'planSha256'}[fault]]='f'*64
                changed=coordinated.replace_binding_row(original,'ProtectionQualificationBinding',value)
            self.fixture.register.write_text(text.replace(original,changed))
            with patch.object(runner,'validate_source_checkout'),patch.object(runner,'DaprCohortRpc',side_effect=AssertionError('no live ports')) as ports:
                with self.assertRaises(runner.Blocked) as refusal:runner.run_installed_cohort(self.joint,self.fixture.register)
                self.assertIn('ExactAcceptedProtectionConfigurationProfileAndPlan',refusal.exception.missing);ports.assert_not_called()

    def test_complete_pair_preserves_commitments_scope_and_exact_coupled_commands(self):
        secret = secrets_prepare.prepare(self.fixture.root, self.fixture.path, self.secret_output,
                                         register=self.fixture.register, coordination_context=self.context_path)
        self.assertTrue(secret['packetPrepared'], secret)
        hosted = host.prepare(self.fixture.root, self.host_output, self.profile_path, self.fixture.register, self.context_path)
        self.assertTrue(hosted['packetPrepared'], hosted)
        for output, record in ((self.secret_output, 'EXT-SECRETS-1'), (self.host_output, 'EXT-HOST-1')):
            packet = q.read_json(output / 'replacement-acceptance-packet.json')
            self.assertEqual(self.commands[record], packet['exactCommand'])
            self.assertIn('PLATFORM_JOINT_COHORT_CONFIGURATION=', packet['exactCommand'])
            self.assertEqual(q.read_json(self.context['installedArtifacts']['launchInventory']['path']),packet['launchInventory'])
            self.assertFalse(packet['dependencyAvailable'])
            self.assertIsNone(packet['acceptedReplacementCommit'])
            self.assertEqual(self.fixture.target, packet['candidateCommit'])
            for dependency, preserved in packet['coordinatedRecords'].items():
                self.assertEqual(coordinated.section(self.fixture.register.read_text(), dependency), preserved['acceptedVerbatimSection'])
                self.assertIn('S1–S4/v23; H1–H4/v21/v23', preserved['proposedVerbatimSection'])
                self.assertIn('365-day history; both Product gates remain Open.', preserved['proposedVerbatimSection'])
                self.assertFalse(preserved['accepted'])
        self.assertEqual([], self.fixture.calls)

    def test_launch_inventory_is_required_and_cannot_be_substituted_under_an_old_joint_binding(self):
        path=Path(self.context['installedArtifacts']['launchInventory']['path']);path.write_bytes(q.canonical({'changed':'public launch bytes'}))
        self.context['installedArtifacts']['launchInventory']['sha256']=hashlib.sha256(path.read_bytes()).hexdigest();self.sign_context()
        missing,_,_=coordinated.admission(self.context_path,self.fixture.register,self.fixture.target,True)
        self.assertIn('ExactAcceptedCompleteLaunchInventoryBinding',missing)
        del self.context['installedArtifacts']['launchInventory'];self.sign_context()
        missing,_,_=coordinated.admission(self.context_path,self.fixture.register,self.fixture.target,True)
        self.assertIn('installedArtifacts.launchInventory',missing);self.assertEqual([],self.fixture.calls)

    def test_signed_installation_cannot_be_reused_for_changed_command_date_or_artifact(self):
        self.context['exactCommands'] = dict(self.commands, **{'EXT-HOST-1': self.commands['EXT-HOST-1'] + ' changed'})
        self.context_path.write_bytes(q.canonical(self.context))
        missing, _, _ = coordinated.admission(self.context_path, self.fixture.register, self.fixture.target, True)
        self.assertIn('IndependentCurrentInstallationProof', missing)
        self.context['exactCommands'] = self.commands
        self.context['platformIntegrationDate'] = '2027-02-29'
        self.context_path.write_bytes(q.canonical(self.context))
        missing, _, _ = coordinated.admission(self.context_path, self.fixture.register, self.fixture.target, True)
        self.assertIn('platformIntegrationDate', missing)
        self.context['platformIntegrationDate'] = self.profile['integrationDate']
        self.context_path.write_bytes(q.canonical(self.context))
        self.profile_path.write_text('changed')
        missing, _, _ = coordinated.admission(self.context_path, self.fixture.register, self.fixture.target, True)
        self.assertIn('installedArtifacts.hostProfile', missing)

    def test_malformed_joint_and_cross_artifact_substitution_block_complete_packets(self):
        for malformed in ([], {'binding':[]}, {'binding':{'platformCommit':self.fixture.target}}):
            self.joint.write_bytes(q.canonical(malformed))
            self.context['installedArtifacts']['jointConfiguration']['sha256'] = hashlib.sha256(self.joint.read_bytes()).hexdigest()
            self.sign_context()
            missing,_,_ = coordinated.admission(self.context_path,self.fixture.register,self.fixture.target,True)
            self.assertTrue(any(name.startswith('JointConfiguration.') for name in missing),missing)
        self.joint_configuration['binding']['profileSha256'] = '0'*64
        self.joint.write_bytes(q.canonical(self.joint_configuration))
        self.context['installedArtifacts']['jointConfiguration']['sha256'] = hashlib.sha256(self.joint.read_bytes()).hexdigest()
        self.sign_context()
        missing,_,_ = coordinated.admission(self.context_path,self.fixture.register,self.fixture.target,True)
        self.assertIn('JointConfiguration.CrossArtifact.profileSha256',missing)

    def test_raw_configuration_format_is_preserved_and_semantic_hash_retained(self):
        raw = json.dumps(self.fixture.config,indent=4).encode() + b'\n'
        self.fixture.path.write_bytes(raw)
        self.context['installedArtifacts']['custodyConfiguration']['sha256'] = hashlib.sha256(raw).hexdigest()
        binding = self.joint_configuration['binding']; binding['configurationRawSha256'] = hashlib.sha256(raw).hexdigest()
        for profile in self.joint_configuration['phaseExecutionProfiles'].values(): profile['bindingDigest'] = digest(binding)
        self.joint.write_bytes(q.canonical(self.joint_configuration))
        self.context['installedArtifacts']['jointConfiguration']['sha256'] = hashlib.sha256(self.joint.read_bytes()).hexdigest()
        text=self.fixture.register.read_text();section=coordinated.section(text,'EXT-PROTECTION-1')
        match=__import__('re').search(r'\| `ProtectionQualificationBinding` \| `([^`]+)` \|',section)
        pins=q.decode_json(match[1]);pins['configurationSha256']=self.context['installedArtifacts']['jointConfiguration']['sha256']
        self.fixture.register.write_text(text.replace(section,coordinated.replace_binding_row(section,'ProtectionQualificationBinding',pins)))
        self.context['acceptedRegisterSha256']=hashlib.sha256(self.fixture.register.read_bytes()).hexdigest()
        self.sign_context()
        report = secrets_prepare.prepare(self.fixture.root,self.fixture.path,self.secret_output,register=self.fixture.register,coordination_context=self.context_path)
        self.assertTrue(report['packetPrepared'],report)
        self.assertEqual(raw,(self.secret_output/'runtime-reference.json').read_bytes())
        packet = q.read_json(self.secret_output/'replacement-acceptance-packet.json')
        self.assertEqual(q.digest(self.fixture.config),packet['configurationSha256'])
        self.assertEqual(hashlib.sha256(raw).hexdigest(),packet['configurationRawSha256'])

    def test_binding_rows_are_replaced_and_malformed_context_has_named_blocker(self):
        original = 'before\n| `JointQualificationBinding` | `old` |\nmiddle\n| `JointQualificationBinding` | `older` |\nafter\n'
        replaced = coordinated.replace_binding_row(original,'JointQualificationBinding',{'new':True})
        self.assertEqual(1,replaced.count('| `JointQualificationBinding` |'))
        self.assertIn('before',replaced); self.assertIn('middle',replaced); self.assertIn('after',replaced)
        for malformed in ([],{'installedArtifacts':[]},{'installedArtifacts':{'x':[]},'exactCommands':{}}):
            self.context_path.write_bytes(q.canonical(malformed))
            missing,_,_ = coordinated.admission(self.context_path,self.fixture.register,self.fixture.target,True)
            self.assertEqual(['ValidExplicitCoordinatedInstallationContext'],missing)

    def test_complete_context_refuses_relative_installed_anchor_without_normalizing_raw_bytes(self):
        self.fixture.config['qualification']['executor']['publicAnchorFile'] = 'executor.public.pem'
        raw = json.dumps(self.fixture.config, indent=2).encode() + b'\n'
        self.fixture.path.write_bytes(raw)
        self.context['installedArtifacts']['custodyConfiguration']['sha256'] = hashlib.sha256(raw).hexdigest()
        binding = self.joint_configuration['binding']
        binding['configurationRawSha256'] = hashlib.sha256(raw).hexdigest()
        binding['configurationSha256'] = q.digest(self.fixture.config)
        for profile in self.joint_configuration['phaseExecutionProfiles'].values():
            profile['bindingDigest'] = digest(binding)
        self.joint.write_bytes(q.canonical(self.joint_configuration))
        self.context['installedArtifacts']['jointConfiguration']['sha256'] = hashlib.sha256(self.joint.read_bytes()).hexdigest()
        self.sign_context()
        report = secrets_prepare.prepare(self.fixture.root, self.fixture.path, self.secret_output,
                                         register=self.fixture.register, coordination_context=self.context_path)
        self.assertFalse(report['packetPrepared'])
        self.assertIn('qualification.executor.absoluteInstalledPublicAnchorLocator', report['missingInputs'])
        self.assertEqual(raw, self.fixture.path.read_bytes())
        self.assertFalse((self.secret_output / 'runtime-reference.json').exists())
        self.assertFalse((self.secret_output / 'replacement-acceptance-packet.json').exists())
        self.assertEqual([], self.fixture.calls)

    def test_closed_assertion_grammar_rejects_resigned_printf_and_weakened_prefixes(self):
        for record,command in self.commands.items():
            self.assertTrue(coordinated.valid_owner_command(command,record,self.fixture.target,str(self.joint)))
            suffix=command[command.index('env '):]
            mutations=[
                'printf '+shlex.quote(self.fixture.target+' status --porcelain')+' && '+suffix,
                command.replace('test "$(git','echo "$(git',1),
                command.replace('" = "','" != "',1),
                command.replace(' && test -z ',' || test -z ',1),
                command.replace('git -C ../platform rev-parse','git -C ../other rev-parse',1),
                command.replace('status --porcelain','status --porcelain --untracked-files=no'),
                '# '+command,
                'false && '+command,
                command.replace(' && env ',' ; env '),
            ]
            for mutation in mutations:
                self.assertFalse(coordinated.valid_owner_command(mutation,record,self.fixture.target,str(self.joint)),mutation)

    def test_resigned_printf_prefix_is_rejected_by_actual_packet_admission(self):
        missing,_,_=coordinated.admission(self.context_path,self.fixture.register,self.fixture.target,True)
        self.assertEqual([],missing)
        for record,original in self.commands.items():
            suffix=original[original.index('env '):]
            changed='printf '+shlex.quote(self.fixture.target+' status --porcelain')+' && '+suffix
            before=self.fixture.register.read_text()
            self.fixture.register.write_text(before.replace(original,changed))
            self.context['exactCommands']=dict(self.commands,**{record:changed});self.sign_context()
            missing,_,_=coordinated.admission(self.context_path,self.fixture.register,self.fixture.target,True)
            self.assertIn(record+'.ClosedCompleteOwnerCommand',missing)
            self.assertNotIn('IndependentCurrentInstallationProof',missing)
            self.fixture.register.write_text(before)
        self.context['exactCommands']=self.commands;self.sign_context()


if __name__ == '__main__':
    unittest.main()
