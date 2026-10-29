"""Actual offline preparers and admission with disposable public inputs only."""
import builtins
from contextlib import contextmanager, redirect_stdout
import hashlib
import io
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

import coordinated_packets as packets
import detached_packet_output as output_io
from packet_snapshot import PublicPacketSnapshot
import test_coordinated_packets as packet_fixture
host, q, secrets_prepare = packet_fixture.host, packet_fixture.q, packet_fixture.secrets_prepare


@contextmanager
def fixture():
    value=packet_fixture.CoordinatedPacketTests();value.setUp()
    try:yield value
    finally:value.doCleanups()


def prepare(value, owner):
    if owner=='secrets':
        return secrets_prepare.prepare(value.fixture.root,value.fixture.path,value.secret_output,register=value.fixture.register,coordination_context=value.context_path)
    return host.prepare(value.fixture.root,value.host_output,value.profile_path,value.fixture.register,value.context_path)


class PacketSnapshotTests(unittest.TestCase):
    def test_pair_list_context_cannot_be_coerced_into_an_admitted_object(self):
        with fixture() as f:
            f.context_path.write_bytes(q.canonical(list(f.context.items())))
            self.assertEqual(["ValidExplicitCoordinatedInstallationContext"],packets.admission(f.context_path,f.fixture.register,f.fixture.target,True)[0])

    def test_both_actual_preparers_keep_one_snapshot_when_every_admitted_input_is_replaced(self):
        # Each subtest starts from an otherwise complete detached fixture. Whole-image,
        # driver and launch protocols retain their existing explicit fixture isolation.
        for owner in ('secrets','host'):
            for selected in (*packets.REQUIRED_ARTIFACTS,'register','context','proof','service-anchor','authority-anchor'):
                with self.subTest(owner=owner,input=selected),fixture() as f:
                    descriptors=f.context['installedArtifacts']
                    paths={name:Path(row['path']) for name,row in descriptors.items()}
                    paths.update(register=f.fixture.register,context=f.context_path,proof=Path(f.context['installationProofFile']),
                                 **{'service-anchor':Path(next(iter(f.joint_configuration['services'].values()))['publicAnchorFile']),
                                    'authority-anchor':f.directory/'fixture-installation-authority.pem'})
                    selected_path=paths[selected];before={name:path.read_bytes() for name,path in paths.items()}
                    capture=PublicPacketSnapshot.capture;changed=[]
                    def capture_then_replace(snapshot,path,maximum=1024*1024):
                        data=capture(snapshot,path,maximum)
                        if Path(path)==selected_path and not changed:
                            selected_path.write_bytes(b'REPLACEMENT NEVER ADMITTED');changed.append(str(path))
                        return data
                    with patch.object(PublicPacketSnapshot,'capture',capture_then_replace):
                        report=prepare(f,owner)
                    self.assertTrue(changed);self.assertTrue(report['packetPrepared'],report)
                    out=f.secret_output if owner=='secrets' else f.host_output
                    packet=q.read_json(out/'replacement-acceptance-packet.json')
                    self.assertEqual(q.decode_json(before['jointConfiguration'])['binding'],packet['jointBinding'])
                    self.assertEqual(q.decode_json(before['launchInventory']),packet['launchInventory'])
                    self.assertEqual({name+'Sha256':hashlib.sha256(before[name]).hexdigest() for name in packets.REQUIRED_ARTIFACTS},packet['installedHashes'])
                    for record,row in packet['coordinatedRecords'].items():
                        self.assertEqual(packets.section(before['register'].decode(),record),row['acceptedVerbatimSection'])
                        self.assertEqual(q.digest(q.decode_json(before['custodyConfiguration'])),row['RuntimeQualificationBinding']['configurationSha256'])
                    self.assertEqual(before['custodyConfiguration'] if owner=='secrets' else before['hostProfile'],
                                     (out/('runtime-reference.json' if owner=='secrets' else 'agents-deployment-profile.json')).read_bytes())
                    self.assertEqual([],f.fixture.calls)

    def test_both_actual_preparers_never_reopen_inputs_after_admission_for_bindings_or_records(self):
        for owner in ('secrets','host'):
            with self.subTest(owner=owner),fixture() as f:
                original=packets.admission;read=q.bounded_public_file;finished=[]
                out=f.secret_output if owner=='secrets' else f.host_output
                def admitted(*args,**kwargs):
                    result=original(*args,**kwargs);self.assertEqual([],result[0]);finished.append(True)
                    return result
                def refuse_reopen(path,*args,**kwargs):
                    if finished and not Path(path).is_relative_to(out):
                        raise AssertionError('Packet construction reopened an admitted public input: '+str(path))
                    return read(path,*args,**kwargs)
                with patch.object(packets,'admission',side_effect=admitted),patch.object(q,'bounded_public_file',side_effect=refuse_reopen):
                    self.assertTrue(prepare(f,owner)['packetPrepared'])

    def test_valid_changed_configuration_after_admission_never_enters_a_complete_record_binding(self):
        for owner in ('secrets','host'):
            with self.subTest(owner=owner),fixture() as f:
                admission=packets.admission;original_raw=f.fixture.path.read_bytes();original=q.digest(q.decode_json(original_raw))
                def change_after_admission(*args,**kwargs):
                    result=admission(*args,**kwargs);self.assertEqual([],result[0])
                    changed=q.decode_json(original_raw);changed['runtime']['issuer']='unadmitted-issuer'
                    f.fixture.path.write_bytes(q.canonical(changed));return result
                with patch.object(packets,'admission',side_effect=change_after_admission):
                    report=prepare(f,owner)
                self.assertTrue(report['packetPrepared'],report)
                out=f.secret_output if owner=='secrets' else f.host_output
                packet=q.read_json(out/'replacement-acceptance-packet.json')
                for row in packet['coordinatedRecords'].values():
                    self.assertEqual(original,row['RuntimeQualificationBinding']['configurationSha256'])
                    self.assertEqual(hashlib.sha256(original_raw).hexdigest(),row['RuntimeQualificationBinding']['configurationRawSha256'])

    def test_missing_cryptography_is_an_ordinary_persisted_joint_service_anchor_refusal_in_both_preparers(self):
        imported=builtins.__import__
        def missing_crypto(name,*args,**kwargs):
            if name.startswith('cryptography'):raise ImportError('isolated unavailable crypto package')
            return imported(name,*args,**kwargs)
        for owner in ('secrets','host'):
            with self.subTest(owner=owner),fixture() as f:
                with patch.object(builtins,'__import__',side_effect=missing_crypto):
                    report=prepare(f,owner)
                self.assertFalse(report['packetPrepared']);out=f.secret_output if owner=='secrets' else f.host_output
                persisted=q.read_json(out/'blocked-inputs.json');self.assertFalse(persisted['packetPrepared'])
                missing=persisted.get('missingInputs',persisted.get('missingReferences'))
                self.assertTrue(any(item.startswith('JointConfiguration.services.') for item in missing),missing)
                self.assertFalse((out/'replacement-acceptance-packet.json').exists())


class PacketPublicationTests(unittest.TestCase):
    def test_each_actual_secrets_output_boundary_preserves_an_incomplete_report_on_failure(self):
        for selected in ('joint-qualification-plan.json','runtime-reference.json','exact-command.sh','runtime-qualification-binding.json','replacement-acceptance-packet.json','verify','complete-report-sync'):
            for after in (False,True):
                with self.subTest(boundary=selected,after=after),fixture() as f:
                    artifact=output_io.artifact;verify=output_io.verify;sync=output_io._sync;reached=[]
                    def interrupted(path,data):
                        if Path(path).name==selected:
                            reached.append(selected)
                            if after:artifact(path,data)
                            raise OSError('isolated output interruption')
                        return artifact(path,data)
                    def interrupted_verify(outputs):
                        if selected=='verify':
                            reached.append(selected)
                            if after:verify(outputs)
                            raise OSError('isolated output verification failure')
                        return verify(outputs)
                    def interrupted_sync(directory):
                        report=f.secret_output/'blocked-inputs.json'
                        if selected=='complete-report-sync' and report.exists() and q.read_json(report).get('packetPrepared'):
                            reached.append(selected)
                            if after:sync(directory)
                            raise OSError('isolated completion report durability failure')
                        return sync(directory)
                    with patch.object(output_io,'artifact',side_effect=interrupted),patch.object(output_io,'verify',side_effect=interrupted_verify),patch.object(output_io,'_sync',side_effect=interrupted_sync):
                        with self.assertRaises(OSError):prepare(f,'secrets')
                    self.assertTrue(reached)
                    report=f.secret_output/'blocked-inputs.json'
                    self.assertTrue(not report.exists() or not q.read_json(report)['packetPrepared'])

    def test_success_report_performs_no_fallible_temporary_cleanup_after_publication(self):
        with fixture() as f:
            report=output_io.report;unlink=Path.unlink
            def reported(path,value):
                if not value.get('packetPrepared'):return report(path,value)
                def refuse_late_pending_cleanup(selected,*args,**kwargs):
                    if selected.name.startswith('.pending-'):
                        raise OSError('No cleanup may follow the final completion publication')
                    return unlink(selected,*args,**kwargs)
                with patch.object(Path,'unlink',refuse_late_pending_cleanup):
                    return report(path,value)
            with patch.object(output_io,'report',side_effect=reported):
                result=prepare(f,'secrets')
            self.assertTrue(result['packetPrepared'])
            self.assertTrue(q.read_json(f.secret_output/'blocked-inputs.json')['packetPrepared'])

    def test_success_report_is_published_only_after_all_exact_required_outputs_verify(self):
        required={
            'secrets':{'joint-qualification-plan.json','runtime-reference.json','exact-command.sh','runtime-qualification-binding.json','replacement-acceptance-packet.json'},
            'host':{'joint-qualification-plan.json','agents-deployment-profile.json','replacement-acceptance-packet.json','exact-command.sh'},
        }
        for owner in ('secrets','host'):
            with self.subTest(owner=owner),fixture() as f:
                out=f.secret_output if owner=='secrets' else f.host_output
                verify=output_io.verify;checked=[];report=output_io.report
                def verified(outputs):
                    self.assertFalse(q.read_json(out/'blocked-inputs.json')['packetPrepared'])
                    verify(outputs);checked.append(set(Path(path).name for path in outputs))
                def reported(path,value):
                    if value.get('packetPrepared'):
                        self.assertEqual([required[owner]],checked)
                    return report(path,value)
                with patch.object(output_io,'verify',side_effect=verified),patch.object(output_io,'report',side_effect=reported):
                    actual=prepare(f,owner)
                self.assertTrue(actual['packetPrepared']);self.assertEqual(actual,q.read_json(out/'blocked-inputs.json'))
                self.assertEqual(required[owner]|{'blocked-inputs.json'},{path.name for path in out.iterdir()})
                record='EXT-SECRETS-1' if owner=='secrets' else 'EXT-HOST-1'
                self.assertEqual((f.commands[record]+'\n').encode('utf-8'),(out/'exact-command.sh').read_bytes())
                self.assertEqual(f.commands[record],q.read_json(out/'replacement-acceptance-packet.json')['exactCommand'])
                if owner=='host':
                    from joint_live_phase_runner import full_plan
                    self.assertEqual(host.canonical(full_plan()),(out/'joint-qualification-plan.json').read_bytes())
                    self.assertEqual(f.profile_path.read_bytes(),(out/'agents-deployment-profile.json').read_bytes())



class HostMainExitTests(unittest.TestCase):
    def test_actual_main_returns_nonzero_on_missing_inputs_or_command_mismatch_and_zero_on_success(self):
        for state in ('missing','command-mismatch','success'):
            with self.subTest(state=state),fixture() as f:
                if state=='missing':
                    f.profile['replay']='';f.profile_path.write_bytes(q.canonical(f.profile))
                args=['prepare','--root',str(f.fixture.root),'--output',str(f.host_output),'--profile',str(f.profile_path),'--register',str(f.fixture.register),'--coordination-context',str(f.context_path)]
                original=packets.admission
                def changed_command(*args,**kwargs):
                    result=original(*args,**kwargs)
                    if state=='command-mismatch' and not result[0]:result[2]['exactCommands']['EXT-HOST-1']+=' substituted'
                    return result
                with patch.object(sys,'argv',args),patch.object(packets,'admission',side_effect=changed_command),redirect_stdout(io.StringIO()) as output:
                    code=host.main()
                value=json.loads(output.getvalue());self.assertEqual(0 if state=='success' else 2,code)
                self.assertEqual(state=='success',value['packetPrepared'])


if __name__=='__main__':unittest.main()
