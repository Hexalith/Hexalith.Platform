"""Public disposable launch graphs. Injected immutable flags are never installation evidence."""
import copy
import hashlib
import json
import os
from pathlib import Path
import platform
import struct
import tempfile
import unittest
from unittest.mock import patch
import launch_inventory as launch


class LaunchInventoryTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name);self.profile={'services':{},'listeners':fixture_listeners()}
        body=valid_static_elf()
        ui=self.profile['listeners']['agents-ui']['uiHost'];ui_root=self.root/'agents';ui_root.mkdir()
        for name,artifact_body in fixture_ui_artifacts(ui).items():(ui_root/name).write_bytes(artifact_body)
        graphs=[]
        for resource,service in launch.RESOURCES.items():
            root=self.root/service;root.mkdir(exist_ok=True);file=root/'executable';file.write_bytes(body);file.chmod(0o700);self.profile['services'][service]=str(root);info=os.stat(root)
            graphs.append({'resource':resource,'root':str(root),'directoryIdentity':f'{os.major(info.st_dev)}:{os.minor(info.st_dev)}:{info.st_ino}','entryPoint':'executable','executionProtocol':'SelfContainedStaticElfV1',
                'wholeImageNativeLoaderClosure':{'protocol':'IndependentlyAcceptedStaticElfKernelClosureV1','evidenceSha256':'f'*64},'listener':self.profile['listeners'][resource],'files':[{'path':path.name,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()} for path in sorted(root.iterdir()) if path.is_file()]})
        self.manifest={'protocol':'AcceptedCompleteImmutableLaunchGraphV1','resources':graphs}

    def test_real_mutable_fixture_cannot_be_misrepresented_as_an_installed_immutable_graph(self):
        self.assertTrue(launch.blockers(self.manifest,self.profile));self.assertTrue(launch.blockers({},self.profile))
        for malformed in (None,[],{'protocol':'AcceptedCompleteImmutableLaunchGraphV1','resources':[None]*7}):self.assertTrue(launch.blockers(malformed,self.profile))

    def test_exact_complete_source_graph_passes_only_under_explicit_fixture_kernel_proof_and_changed_bytes_refuse(self):
        with patch.object(launch,'_immutable',return_value=None):
            self.assertEqual([],launch.blockers(self.manifest,self.profile))
            file=self.root/'agents'/'executable';file.write_bytes(file.read_bytes()+b'changed')
            self.assertTrue(launch.blockers(self.manifest,self.profile))

    def test_even_reaccepted_script_or_dynamic_loader_bytes_need_their_selected_complete_protocol(self):
        for kind in ('script','dynamic'):
            with self.subTest(kind=kind):
                manifest=copy.deepcopy(self.manifest);file=self.root/'agents'/'executable';body=bytearray(file.read_bytes())
                if kind=='script':body=bytearray(b'#!/bin/sh\nexit 0\n')
                else:
                    body=bytearray((self.root/'eventStore'/'executable').read_bytes());struct.pack_into('<I',body,64,3)
                file.write_bytes(body)
                for graph in manifest['resources']:
                    if graph['root']==str(file.parent):graph['files'][0].update(bytes=len(body),sha256=hashlib.sha256(body).hexdigest())
                with patch.object(launch,'_immutable',return_value=None):self.assertTrue(launch.blockers(manifest,self.profile))

    def test_actual_listener_inventory_and_profile_require_exact_complete_distinct_role_bindings(self):
        for kind in ('missing','extra','conflict','duplicate-endpoint','wrong-role','substitution','out-of-range'):
            with self.subTest(kind=kind):
                manifest=copy.deepcopy(self.manifest);profile=copy.deepcopy(self.profile)
                if kind=='missing':del profile['listeners']['agents-ui']
                elif kind=='extra':profile['listeners']['extra']=copy.deepcopy(profile['listeners']['agents'])
                elif kind=='conflict':profile['listeners']['agents-ui']['port']=profile['listeners']['agents']['port']
                elif kind=='duplicate-endpoint':profile['listeners']['agents-ui']['endpointName']=profile['listeners']['agents']['endpointName']
                elif kind=='wrong-role':profile['listeners']['tenants-api']['role']='tenants-owner'
                elif kind=='out-of-range':profile['listeners']['agents-ui']['port']=65536
                else:manifest['resources'][-1]['listener']['port']=24999
                if kind!='substitution':
                    for graph in manifest['resources']:graph['listener']=profile['listeners'].get(graph['resource'])
                with patch.object(launch,'_immutable',return_value=None):self.assertTrue(launch.blockers(manifest,profile))

    def test_held_immutable_but_nonexecutable_entrypoint_refuses(self):
        (self.root/'agents'/'executable').chmod(0o600)
        with patch.object(launch,'_immutable',return_value=None):self.assertEqual(['External.AcceptedExecutableEntrypointAccessV1'],launch.blockers(self.manifest,self.profile))

    def test_hosted_razor_ui_requires_actual_accepted_host_contract_and_existing_component_identity(self):
        for kind in ('missing','component','registration','contract-bytes'):
            with self.subTest(kind=kind):
                manifest=copy.deepcopy(self.manifest);profile=copy.deepcopy(self.profile)
                if kind=='missing':del profile['listeners']['agents-ui']['uiHost']
                elif kind=='component':profile['listeners']['agents-ui']['uiHost']['contract']['uiComponentIdentity']='Unrelated.UI'
                elif kind=='registration':profile['listeners']['agents-ui']['uiHost']['contract']['frontComposerRegistrationIdentity']='Unrelated.Registration'
                else:
                    host=profile['listeners']['agents-ui']['uiHost'];body=copy.deepcopy(host['contract']);body['listenerArgument']='--unrelated';raw=json.dumps(body).encode()
                    file=self.root/'agents'/host['artifact'];file.write_bytes(raw);host['sha256']=hashlib.sha256(raw).hexdigest()
                    for graph in manifest['resources']:
                        if graph['root']==str(file.parent):
                            for pin in graph['files']:
                                if pin['path']==file.name:pin.update(bytes=len(raw),sha256=host['sha256'])
                if kind in ('component','registration'):
                    host=profile['listeners']['agents-ui']['uiHost'];raw=json.dumps(host['contract'],sort_keys=True,separators=(',',':')).encode();file=self.root/'agents'/host['artifact'];file.write_bytes(raw);host['sha256']=hashlib.sha256(raw).hexdigest()
                    for graph in manifest['resources']:
                        if graph['root']==str(file.parent):
                            for pin in graph['files']:
                                if pin['path']==file.name:pin.update(bytes=len(raw),sha256=host['sha256'])
                for graph in manifest['resources']:graph['listener']=copy.deepcopy(profile['listeners'][graph['resource']])
                with patch.object(launch,'_immutable',return_value=None):
                    self.assertTrue(any(launch.UI_HOST_REQUIREMENT in value for value in launch.blockers(manifest,profile)))


UI_COMPONENT=b'fixture-only existing UI component identity'
UI_REGISTRATION=b'fixture-only existing FrontComposer registration identity'


def fixture_listeners(entry_point='executable'):
    result={resource:{'protocol':'AspNetCoreHttpListenerV1','role':role,'address':'127.0.0.1','port':24100+index,'endpointName':resource+'-http'}
            for index,(resource,role) in enumerate(launch.LISTENER_ROLES.items())}
    contract={'protocol':'IndependentlyAcceptedFrontComposerUiHostHttpV1','entryPoint':entry_point,
              'uiComponentIdentity':'Hexalith.Agents.UI','frontComposerRegistrationIdentity':'Hexalith.Agents.UI.Composition.AgentsFrontComposerRegistration',
              'uiComponentArtifact':'fixture-ui-component.bin','uiComponentSha256':hashlib.sha256(UI_COMPONENT).hexdigest(),
              'frontComposerRegistrationArtifact':'fixture-frontcomposer-registration.bin','frontComposerRegistrationSha256':hashlib.sha256(UI_REGISTRATION).hexdigest(),
              'evidenceSha256':'f'*64,'listenerArgument':'--urls','listenerEnvironment':'ASPNETCORE_URLS'}
    body=json.dumps(contract,separators=(',',':'),sort_keys=True).encode()
    result['agents-ui']['uiHost']={'artifact':'fixture-ui-host-contract.json','sha256':hashlib.sha256(body).hexdigest(),'contract':contract}
    return result


def fixture_ui_artifacts(host):
    return {host['artifact']:json.dumps(host['contract'],separators=(',',':'),sort_keys=True).encode(),
            host['contract']['uiComponentArtifact']:UI_COMPONENT,host['contract']['frontComposerRegistrationArtifact']:UI_REGISTRATION}


def valid_static_elf():
    body=bytearray(129);body[:7]=b'\x7fELF\x02\x01\x01'
    struct.pack_into('<HHI',body,16,2,183 if platform.machine()=='aarch64' else 62,1)
    struct.pack_into('<QQ',body,24,0x400078,64);struct.pack_into('<HHH',body,52,64,56,1)
    struct.pack_into('<IIQQQQQQ',body,64,1,5,0,0x400000,0x400000,129,129,4096)
    return body
