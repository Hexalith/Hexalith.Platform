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

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec

import coordinated_packets as coordinated
import prepare_acceptance as secrets_prepare
import qualify as q
from joint_live_phase_runner import digest, full_plan
from test_qualify import Fixture

spec = importlib.util.spec_from_file_location("coordinated_host_prepare", Path(__file__).parents[1] / "ext-host-1" / "prepare_acceptance.py")
host = importlib.util.module_from_spec(spec)
spec.loader.exec_module(host)


class CoordinatedPacketTests(unittest.TestCase):
    def setUp(self):
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
            elif name.endswith('Anchor'): path = Path(self.fixture.config['qualification'][name[:-6]]['publicAnchorFile'])
            else:
                path = self.directory / (name + '.json')
                path.write_bytes(q.canonical({'fixtureOnly': name}))
            artifacts[name] = {'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'enrollmentReference': 'fixture-enrollment-' + name}
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
            self.assertFalse(packet['dependencyAvailable'])
            self.assertIsNone(packet['acceptedReplacementCommit'])
            self.assertEqual(self.fixture.target, packet['candidateCommit'])
            for dependency, preserved in packet['coordinatedRecords'].items():
                self.assertEqual(coordinated.section(self.fixture.register.read_text(), dependency), preserved['acceptedVerbatimSection'])
                self.assertIn('S1–S4/v23; H1–H4/v21/v23', preserved['proposedVerbatimSection'])
                self.assertIn('365-day history; both Product gates remain Open.', preserved['proposedVerbatimSection'])
                self.assertFalse(preserved['accepted'])
        self.assertEqual([], self.fixture.calls)

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


if __name__ == '__main__':
    unittest.main()
