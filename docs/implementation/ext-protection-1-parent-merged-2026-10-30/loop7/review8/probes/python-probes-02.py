from pathlib import Path
import json,importlib.util,tempfile,hashlib,os,subprocess,shutil
from unittest.mock import patch
root=Path('/home/administrator/projects/hexalith/platform');es=root.parent/'eventstore';out=Path('/tmp/ext-protection-loop7-review8-parent-probe');results={}
def mod(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
v=mod('parent_r8_verifier',es/'tools/ext_protection_1.py');vector=json.loads((root/'docs/implementation/ext-protection-1-loop7/verification/recovery-vectors/P36.json').read_text());original=vector['observation']['operationProducerOriginals'][0]
try:v.native_record(original,'OperationId RequestSha256 Role BindingDigest AttemptId Producer','OPERATION_PRODUCER_ORIGINAL_SCHEMA_INVALID')
except v.Failed as e:results['actualProducerSchema']={'vector':'P36','actualFields':sorted(original),'failure':str(e)}
probe=mod('parent_r8_native',root/'eng/ext-protection-1/probe_erc_native.py')
with tempfile.TemporaryDirectory(prefix='protection-r8-runtime-') as td:
 d=Path(td);runtime=Path('/home/administrator/hexalith-protection-sessions/ext-protection-loop7-recovered-cd78452ebe/platform/artifacts/ext-protection-loop7-qualification-runtime-20');shutil.copytree(runtime,d,dirs_exist_ok=True);assembly=d/'Hexalith.Platform.Protection.Tests.dll';dependency=d/'Hexalith.Platform.Protection.dll';before=hashlib.sha256(dependency.read_bytes()).hexdigest();dependency.write_bytes(dependency.read_bytes()+b'owned-unlisted-dependency-mutation');manifest=d/'runtime-manifest.json';manifest.write_text(json.dumps({'files':[dict(path=assembly.name,sha256=hashlib.sha256(assembly.read_bytes()).hexdigest())]}))
 p=probe.ErcProbe(assembly,hashlib.sha256(assembly.read_bytes()).hexdigest(),runtime_manifest=manifest,runtime_manifest_sha256=hashlib.sha256(manifest.read_bytes()).hexdigest())
 class ImageReached(Exception):pass
 def stop(stage,args,*a,**kw):
  if stage=='image-inspect':raise ImageReached()
  raise AssertionError('Unexpected stage '+stage)
 p.command=stop
 try:p.execute()
 except ImageReached:results['incompleteNativeRuntime']={'oneFileManifestAcceptedBeforeImageInspection':True,'frozenManifestReported':p.result.get('frozenRuntimeManifestSha256') is not None,'actualSelectedDependencyUnlisted':'Hexalith.Platform.Protection.dll','selectedDependencyChanged':hashlib.sha256(dependency.read_bytes()).hexdigest()!=before,'containerCreated':False}
t=mod('parent_r8_installation_tests',es/'tools/tests/test_ext_protection_1_loop7_installation.py');cls=t.ShippedRoleClosureTests
try:
 cls.setUpClass();instance=cls();values={'EXT_PROTECTION_ROLE_LOOP7_'+role.upper():('x'*8186 if role=='executor' else 'distinct-'+role) for role in t.v.ROLE_NAMES}
 with patch.dict(os.environ,values):
  accepted=instance.validate();results['credentialLimit']={'acceptedProfile':isinstance(accepted['roles'],t.v.ValidatedRoleConfigurations),'asciiCredentialBytes':8186,'wireAuthorizationCharacters':8186+7,'serverMaxAuthorizationCharacters':8192,'serverRefuses':8186+7>8192}
finally:cls.doClassCleanups()
(out/'python-result-02.json').write_text(json.dumps(results,indent=2)+'\n');print(json.dumps(results,indent=2))
