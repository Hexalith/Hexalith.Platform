"""Disposable selected file IO and unapplied frontend manifest consistency; no K8s calls."""
import os
from pathlib import Path
import re
import tempfile
import unittest
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import ec
import bounded_file_io as selected_io
import qualify


class RegularRoleMountLayoutTests(unittest.TestCase):
    def test_all_three_proposed_frontends_mount_every_consumed_file_at_its_declared_locator(self):
        source=Path(__file__).resolve().parents[2]/'src'
        for role in ('executor','observer','enrollment'):
            with self.subTest(role=role):
                text=(Path(__file__).parent/'provisioning'/f'{role}.proposed.yaml').read_text()
                program=(source/f'Hexalith.Platform.Secrets.Qualification.{role.title()}'/'Program.cs').read_text()
                self.assertIn('SecretsRuntimeConfiguration.Load(args[0])',program)
                self.assertIn('builder.Configuration["PLATFORM_QUALIFICATION_INSTALLATION"]',program)
                self.assertIn('args: ["/references/runtime.json"]',text)
                self.assertIn('name: PLATFORM_QUALIFICATION_INSTALLATION\n              value: /references/qualification-installation.json',text)
                mounts=text.split('          volumeMounts:\n',1)[1].split('      volumes:\n',1)[0]
                blocks=re.findall(r'            - name: ([^\n]+)\n((?:              [^\n]*\n)+)',mounts)
                selected={}
                for name,body in blocks:
                    row=dict(line.strip().split(': ',1) for line in body.splitlines())
                    if name=='journal':continue
                    self.assertEqual('true',row['readOnly']);self.assertIn('subPath',row)
                    self.assertNotIn(row['mountPath'],('/references','/role-credentials'))
                    selected[row['mountPath']]=(name,row['subPath'])
                self.assertEqual(('references','runtime.json'),selected.pop('/references/runtime.json'))
                self.assertEqual(('references','qualification-installation.json'),selected.pop('/references/qualification-installation.json'))
                for kind in ('SCOPE_AUTHORITY','ORIGINAL_AUTHORITY','OWNER'):
                    prefix='${OWNER_'+role.upper()+'_'+kind+'_CREDENTIAL_'
                    self.assertEqual(('role-credentials',prefix+'KEY}'),selected.pop(prefix+'FILE}'))
                    self.assertIn('- key: '+prefix+'KEY}\n                path: '+prefix+'KEY}',text)
                self.assertEqual({},selected)
                for key in ('runtime.json','qualification-installation.json'):
                    self.assertIn('- key: '+key+'\n                path: '+key,text)

    def test_selected_config_anchor_and_private_key_atomicwriter_layout_rejects_and_same_regular_locators_work(self):
        key=ec.generate_private_key(ec.SECP256R1())
        private=key.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption())
        public=key.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo)
        for role in ('executor','observer','enrollment'):
            with self.subTest(role=role),tempfile.TemporaryDirectory() as temporary:
                root=Path(temporary);version=root/'..fixture-version';version.mkdir();(root/'..data').symlink_to(version,target_is_directory=True)
                files={'runtime.json':b'{"runtime":{"providerBinding":"fixture"}}','qualification-installation.json':b'{"Role":"'+role.encode()+b'"}','anchor.pem':public,'signer.pem':private}
                for name,body in files.items():
                    (version/name).write_bytes(body);(root/name).symlink_to('..data/'+name)
                    if name=='signer.pem':
                        with self.assertRaises(OSError):selected_io.use_private(root/name,8192,lambda raw:serialization.load_pem_private_key(raw,None))
                    else:
                        with self.assertRaises(OSError):qualify.bounded_public_file(root/name,8192)
                for name,body in files.items():
                    (root/name).unlink();(root/name).write_bytes(body)
                    if name=='signer.pem':
                        loaded=selected_io.use_private(root/name,8192,lambda raw:serialization.load_pem_private_key(raw,None))
                        self.assertEqual(key.public_key().public_numbers(),loaded.public_key().public_numbers())
                    else:self.assertEqual(body,qualify.bounded_public_file(root/name,8192))
