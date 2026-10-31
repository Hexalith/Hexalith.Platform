"""Fixture parity and refusal regressions; all execution transports are synthetic."""
from contextlib import contextmanager
from datetime import datetime,timedelta,timezone
import base64
import copy
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import yaml
import prepare
import phases


class Control:
 def __enter__(self):
  self.temp=tempfile.TemporaryDirectory(prefix='mcpcli-v1-test-');self.root=Path(self.temp.name)
  self.directory=self.root/'packet';self.private=self.root/'private'
  self.packet=prepare.prepare(self.private,self.directory,auth={'auths':{'registry.hexalith.com':{'auth':'SYNTHETIC-ONLY'}}})
  self.hash=prepare.digest(self.directory/'review.json');self.bundle=json.loads((self.private/'private-inputs.json').read_text())
  return self
 def __exit__(self,*args):self.temp.cleanup()
 def save(self):prepare.dump(self.directory/'review.json',self.packet);self.hash=prepare.digest(self.directory/'review.json')
 def verify(self):return phases.verify(self.directory,self.hash)


class FakeRunner(phases.Runner):
 def __init__(self,*args):
  super().__init__(*args);self.exists=False;self.pods=[];self.jobs={};self.pod_overrides={};self.mutations=[];self.redis_uid='redis-unit';self.pvc_uid='pvc-unit';self.api='https://192.168.1.30:6443';self.size=b'0';self.rdb=b'REDIS0009 UNIT TEST ONLY';self.save_result=b'OK'
  self.resolver='nameserver '+prepare.NODE_LOCAL_DNS+'\n';self.dns_responses={};self.dns_reads=[];self.policy_override=None;self.native_apps=set()
 def application_pod(self,app):
  job=self.jobs[app];pod={'metadata':{'name':app+'-abc123','namespace':self.namespace,'uid':app+'-pod-unit','labels':copy.deepcopy(job['spec']['template']['metadata']['labels']),
   'ownerReferences':[{'controller':True,'kind':'Job','name':app,'uid':job['metadata']['uid']}]},'spec':copy.deepcopy(job['spec']['template']['spec']),
   'status':{'phase':'Running','containerStatuses':[{'name':app,'restartCount':0,'state':{'running':{}}},{'name':'daprd','ready':True,'restartCount':0,'state':{'running':{}},'containerID':'synthetic://'+app+'-sidecar'}]}}
  pod['spec']['containers'].append({'name':'daprd','image':'synthetic-sidecar-only'})
  if app in self.native_apps:
   pod['spec']['containers']=[x for x in pod['spec']['containers'] if x['name']!='daprd'];pod['spec']['initContainers']=[{'name':'daprd','restartPolicy':'Always'}]
   pod['status']['initContainerStatuses']=[x for x in pod['status']['containerStatuses'] if x['name']=='daprd'];pod['status']['containerStatuses']=[x for x in pod['status']['containerStatuses'] if x['name']!='daprd']
  return self.pod_overrides.get(app,pod)
 def kube(self,*args,input=None,timeout=30):
  argv=list(args)
  if argv[:1]==['-n']:argv=argv[2:]
  if argv[0]=='config':return self.api.encode()
  if argv[:2]==['get','namespace']:
   if not self.exists and '--ignore-not-found' in argv:return b''
   return json.dumps({'metadata':{'uid':'namespace-unit','labels':{'hexalith.com/operator-task':self.namespace}}}).encode()
  if argv[:2]==['get','pods']:
   if '-l' in argv:
    app=argv[argv.index('-l')+1].split('app=')[1];items=[self.application_pod(app)] if app in self.pods else []
   else:items=[{'metadata':{'name':p,'uid':self.redis_uid if p=='redis' else p+'-unit'}} for p in self.pods]
   return json.dumps({'items':items}).encode()
  if argv[:2]==['get','job']:return json.dumps(self.jobs[argv[2]]).encode()
  if argv[:3]==['get','pod','redis']:return json.dumps({'metadata':{'uid':self.redis_uid}}).encode()
  if argv[:3]==['get','pvc','task-data']:return json.dumps({'metadata':{'uid':self.pvc_uid}}).encode()
  if argv[:2]==['get','service']:return b'{"spec":{"publishNotReadyAddresses":true}}'
  if argv[:2]==['get','networkpolicy']:
   value=next(d for d in yaml.safe_load_all((self.directory/'infrastructure.yaml').read_text()) if d['kind']=='NetworkPolicy');value['metadata']['uid']='policy-unit'
   return json.dumps(self.policy_override or value).encode()
  if argv[0]=='create' and '--dry-run=client' in argv:return b'{"kind":"Secret","data":{"synthetic":"only"}}'
  if argv[0]=='create':
   self.mutations.append(argv)
   if '-f' in argv:
    file=argv[argv.index('-f')+1]
    if file!='-':
     name=Path(file).stem
     if name=='namespace':self.exists=True
     if name in ('redis','eventstore','tenants','parties'):self.pods.append(name)
     if name in prepare.APPS:
      self.jobs[name]=yaml.safe_load(Path(file).read_text());self.jobs[name]['metadata']['uid']=name+'-job-unit'
      return json.dumps(self.jobs[name]).encode()
   return b'created'
  if argv[0]=='wait':return b'condition met'
  if argv[0]=='exec':
   if argv[-1]=='/etc/resolv.conf':return self.resolver.encode()
   if argv[-1]=='DBSIZE':return self.size
   if argv[-1]=='SAVE':self.mutations.append(argv);return self.save_result
   if argv[-1]=='/data/dump.rdb':return self.rdb
  if argv[0]=='delete':self.mutations.append(argv);self.exists=False;return b'deleted'
  raise AssertionError('Unexpected synthetic command: '+str(argv))
 def dns_probe(self,host):
  self.dns_reads.append(host);values=self.dns_responses.get(host)
  return values.pop(0) if values else (0,'10.233.7.7 STREAM synthetic\n','')
 @contextmanager
 def forward(self,require_ready=True):
  phases.require(all(self.app_pod(app) is not None for app in prepare.APPS),'application-pod-missing')
  if require_ready:phases.require(all(self.sidecar_ready(app) for app in prepare.APPS),'sidecar-runtime-unready')
  yield


class Oracle:
 def __init__(self,packet):self.packet=packet;self.writes=[]
 def request(self,port,path,credential,body=None):
  if path in ('/alive','/ready'):return 200,None
  if path.endswith('/activate'):self.writes.append(body);return 200,{'status':'Activated'}
  if path=='/api/v1/commands':self.writes.append(body);return 202,{}
  if '/status/' in path:return 200,{'status':'Completed','committedEventSequence':1,'eventCount':1}
  if body.get('domain')=='global-administrators':return 200,{'payload':{'items':[{'userId':'mcpcli-live-admin'}]}}
  return 200,{'payload':{'tenantId':self.packet['tenant'],'status':'Active','members':[{'userId':self.packet['operatorSubject'],'role':'TenantContributor'}]}}


class V1Tests(unittest.TestCase):
 def refuse(self,code,action):
  with self.assertRaises(ValueError) as caught:action()
  self.assertEqual(code,str(caught.exception))

 def test_filled_control_fixture_parity_and_sensitive_boundaries(self):
  with Control() as c,patch.object(subprocess,'run') as external:
   c.verify();docs=[]
   for name in c.packet['files']:docs+=list(yaml.safe_load_all((c.directory/name).read_text()))
   prepare.validate_manifests(docs,c.packet['target']['namespace'])
   self.assertEqual(set(prepare.APPS),{d['metadata']['name'] for d in docs if d['kind']=='Job'})
   self.assertEqual(['state.redis','pubsub.redis'],[d['spec']['type'] for d in docs if d['kind']=='Component'])
   for d in docs:
    if d['kind']=='Component':self.assertEqual(list(prepare.APPS),d['scopes'])
   self.assertTrue(all(d['spec']['publishNotReadyAddresses'] for d in docs if d['kind']=='Service' and d['metadata']['name'].endswith('-dapr')))
   public=''.join((c.directory/name).read_text() for name in c.packet['files'])
   for name,keys in c.bundle['secrets'].items():
    for key,file in keys.items():self.assertNotIn(Path(file).read_text(),public)
   self.assertEqual('prepared-for-review',c.packet['status']);self.assertFalse(c.packet['applyAuthorized']);external.assert_not_called()

 def test_fixture_route_and_bootstrap_refusals(self):
  source=json.loads((prepare.MCPCLI/prepare.FIXTURES[1]).read_text())
  cases=[('routes','fixture-route-set-changed'),('binding','fixture-route-bindings-changed'),('topic','fixture-bootstrap-or-topic-changed'),('bootstrap','fixture-bootstrap-or-topic-changed')]
  for case,code in cases:
   with self.subTest(case=case),patch.object(subprocess,'run') as external:
    d=copy.deepcopy(source)
    if case=='routes':d['Gateway']['Routes']['other']={}
    if case=='binding':d['Gateway']['Routes']['*|party|v1']['TenantId']='system'
    if case=='topic':d['Gateway']['TopicOverrides']={}
    if case=='bootstrap':d['Tenants']['BootstrapGlobalAdminEnabled']=False
    self.refuse(code,lambda:prepare.validate_fixture(d));external.assert_not_called()

 def test_fresh_private_and_packet_output_refusals(self):
  with tempfile.TemporaryDirectory() as root,patch.object(subprocess,'run') as external:
   root=Path(root)
   for private in [Path('relative'),root,prepare.PLATFORM/'new-private-inputs']:
    self.refuse('fresh-external-private-directory-required',lambda:prepare.prepare(private,root/'output',auth={}))
   self.refuse('fresh-packet-directory-required',lambda:prepare.prepare(root/'private',root,auth={}))
   self.assertEqual([],list(root.iterdir()));external.assert_not_called()

 def test_image_and_capability_refusals(self):
  actual=json.loads((prepare.HERE/'evidence/images.json').read_text())
  for case,code in [('image','inspected-images-required'),('architecture','inspected-images-required'),('channel','required-image-capability-missing'),('writer','required-image-capability-missing')]:
   with self.subTest(case=case),tempfile.TemporaryDirectory() as root,patch.object(subprocess,'run') as external:
    changed=copy.deepcopy(actual)
    if case=='image':changed[0]['reference']='registry.invalid/other@sha256:'+'0'*64
    if case=='architecture':changed[0]['architecture']='arm64'
    if case=='channel':changed[1]['markers']['AppChannelToken']=[]
    if case=='writer':changed[0]['markers']['delivery-writer-protocol/activate']=[]
    original=json.loads
    def loads(text,*args,**kwargs):return changed if isinstance(text,str) and text== (prepare.HERE/'evidence/images.json').read_text() else original(text,*args,**kwargs)
    with patch.object(json,'loads',side_effect=loads):self.refuse(code,lambda:prepare.prepare(Path(root)/'private',Path(root)/'packet',auth={}))
    self.assertEqual([],list(Path(root).iterdir()));external.assert_not_called()

 def test_manifest_refusal_matrix(self):
  with Control() as c:
   docs=[]
   for name in c.packet['files']:docs+=list(yaml.safe_load_all((c.directory/name).read_text()))
   cases=[('apps','exact-three-application-jobs-required'),('namespace','external-resource-refused'),('owner','unowned-resource-refused'),('secret','public-or-secret-resource-refused'),('service','public-service-refused'),('restart','automatic-restart-or-host-namespace-refused'),('image','immutable-bounded-container-required'),('security','container-security-required'),('credential','literal-credential-refused'),('ready','owner-readiness-required')]
   for case,code in cases:
    with self.subTest(case=case),patch.object(subprocess,'run') as external:
     d=copy.deepcopy(docs);pod=next(x for x in d if x['kind']=='Job' and x['metadata']['name']=='eventstore');spec=pod['spec']['template']['spec'];container=spec['containers'][0]
     if case=='apps':d.remove(pod)
     if case=='namespace':pod['metadata']['namespace']='other'
     if case=='owner':pod['metadata']['labels']={}
     if case=='secret':d.append({'kind':'Secret','metadata':copy.deepcopy(pod['metadata'])})
     if case=='service':next(x for x in d if x['kind']=='Service')['spec']['type']='NodePort'
     if case=='restart':spec['restartPolicy']='Always'
     if case=='image':container['image']='image:latest'
     if case=='security':container['securityContext']['readOnlyRootFilesystem']=False
     if case=='credential':container['env'].append({'name':'APP_API_TOKEN','value':'never-retain'})
     if case=='ready':container['readinessProbe']['httpGet']['path']='/alive'
     self.refuse(code,lambda:prepare.validate_manifests(d,c.packet['target']['namespace']));external.assert_not_called()

 def test_review_binding_refusals(self):
  for case,code in [('hash','review-hash-mismatch'),('runner','runner-changed-after-review'),('helper','runner-helper-changed-after-review'),('fixture','fixture-changed-after-review'),('evidence','evidence-changed-after-review'),('scope','review-scope-mismatch'),('expiry','review-expired'),('manifest','manifest-hash-mismatch'),('private-manifest','private-input-manifest-changed'),('private-mode','private-file-mode-or-path-invalid'),('private-file','private-input-changed-or-public')]:
   with self.subTest(case=case),Control() as c,patch.object(subprocess,'run') as external:
    if case=='hash':c.hash='0'*64
    if case=='runner':c.packet['runnerSha256']='0'*64;c.save()
    if case=='helper':c.packet['prepareSha256']='0'*64;c.save()
    if case=='fixture':c.packet['fixtureHashes'][prepare.FIXTURES[0]]='0'*64;c.save()
    if case=='evidence':c.packet['evidenceHashes']['images.json']='0'*64;c.save()
    if case=='scope':c.packet['tenant']='another';c.save()
    if case=='expiry':c.packet['expiresAt']=(datetime.now(timezone.utc)-timedelta(seconds=1)).isoformat();c.save()
    if case=='manifest':(c.directory/'redis.yaml').write_text('changed')
    if case=='private-manifest':(c.private/'private-inputs.json').write_text('{}')
    if case=='private-mode':(c.private/'private-inputs.json').chmod(0o644)
    if case=='private-file':Path(c.bundle['secrets']['mcpcli-jwt']['signing-key']).chmod(0o644)
    self.refuse(code,c.verify);external.assert_not_called();self.assertFalse((c.private/'execution').exists())

 def test_registry_helper_constraints_and_extension(self):
  with tempfile.TemporaryDirectory() as root,patch.object(Path,'home',return_value=Path(root)):
   config=Path(root)/'.docker/config.json';config.parent.mkdir()
   config.write_text(json.dumps({'auths':{}}))
   with patch.object(subprocess,'run') as external:self.refuse('configured-registry-auth-unavailable',prepare.registry_auth);external.assert_not_called()
   config.write_text(json.dumps({'credsStore':'../other'}))
   with patch.object(subprocess,'run') as external:self.refuse('configured-registry-auth-unavailable',prepare.registry_auth);external.assert_not_called()
   config.write_text(json.dumps({'credsStore':'desktop.exe'}))
   for result,code in [(subprocess.CompletedProcess([],1,b'',b'private diagnostics'),'registry-credential-helper-failed'),(subprocess.CompletedProcess([],0,b'{}',b''),'registry-credential-incomplete')]:
    with patch.object(subprocess,'run',return_value=result):self.refuse(code,prepare.registry_auth)
   result=subprocess.CompletedProcess([],0,b'{"Username":"unit","Secret":"synthetic"}',b'')
   with patch.object(subprocess,'run',return_value=result) as external:
    value=prepare.registry_auth();self.assertEqual({'registry.hexalith.com'},set(value['auths']));self.assertEqual('docker-credential-desktop.exe',external.call_args.args[0][0])

 def test_ordered_phases_real_backup_shape_writer_once_and_scoped_handoff(self):
  with Control() as c,patch.object(subprocess,'run') as external:
   runner=FakeRunner(c.directory,c.packet,c.bundle);oracle=Oracle(c.packet)
   with patch.object(phases,'request',side_effect=oracle.request):
    for phase in phases.PHASES:runner.execute(phase)
   self.assertEqual(3,len(oracle.writes));self.assertEqual(1,sum('backupReference' in w for w in oracle.writes))
   backup=json.loads((runner.journal/'initial-redis-backup.json').read_text());self.assertEqual(0,backup['databaseSize']);self.assertTrue(backup['createdBeforeApplications']);self.assertEqual('redis-unit',backup['redisPodUid'])
   profile=json.loads((c.private/'operator-home/.eventstore/mcpcli.json').read_text())['profiles']['operator'];payload=json.loads(base64.urlsafe_b64decode(profile['token'].split('.')[1]+'=='))
   self.assertEqual('false',payload['global_admin']);self.assertEqual(['system',c.packet['tenant']],json.loads(payload['tenants']));self.assertEqual(['tenants','party'],json.loads(payload['domains']));self.assertFalse(runner.exists)
   self.assertEqual(7200,payload['exp']-payload['iat'])
   external.assert_not_called()

 def test_token_expiry_is_capped_by_review_window(self):
  with Control() as c:
   c.packet['expiresAt']=(datetime.now(timezone.utc)+timedelta(minutes=20)).isoformat()
   _,expiry=phases.token(c.packet,c.bundle,operator=True)
   self.assertEqual(int(datetime.fromisoformat(c.packet['expiresAt']).timestamp()),expiry)

 def test_private_repository_boundary_and_existing_namespace_refuse_without_mutation(self):
  with Control() as c,patch.object(subprocess,'run') as external:
   with patch.object(phases,'PLATFORM',c.root):self.refuse('private-inputs-inside-repository',c.verify)
   runner=FakeRunner(c.directory,c.packet,c.bundle);runner.exists=True
   self.refuse('namespace-already-exists',lambda:runner.execute('infrastructure'))
   self.assertEqual([],runner.mutations);external.assert_not_called()

 def test_kubernetes_transport_failure_is_not_exposed_or_retried(self):
  with Control() as c:
   runner=phases.Runner(c.directory,c.packet,c.bundle)
   failure=subprocess.CompletedProcess([],1,b'private output',b'private error')
   with patch.object(subprocess,'run',return_value=failure) as external:
    self.refuse('kubernetes-operation-failed',lambda:runner.execute('infrastructure'))
    self.assertEqual(1,external.call_count);self.assertFalse((runner.journal/'infrastructure.json').exists())

 def test_phase_order_target_and_retry_refusals(self):
  with Control() as c:
   runner=FakeRunner(c.directory,c.packet,c.bundle)
   self.refuse('unknown-phase',lambda:runner.execute('other'))
   self.refuse('required-phase-incomplete',lambda:runner.execute('backup'))
   runner.api='https://other:6443';self.refuse('context-api-mismatch',lambda:runner.execute('infrastructure'));self.assertEqual([],runner.mutations)
   runner.api=c.packet['target']['api'];runner.execute('infrastructure');before=len(runner.mutations)
   self.refuse('phase-already-attempted-do-not-retry',lambda:runner.execute('infrastructure'));self.assertEqual(before,len(runner.mutations))
   receipt=runner.journal/'infrastructure.json';d=json.loads(receipt.read_text());d['namespaceUid']='other';prepare.dump(receipt,d)
   self.refuse('namespace-uid-or-owner-mismatch',runner.uid)

 def test_backup_and_provider_refusals(self):
  for case,code in [('apps','applications-present-before-backup'),('nonempty','task-store-not-empty'),('save','provider-native-backup-failed'),('rdb','provider-native-backup-invalid'),('proof','provider-backup-proof-invalid'),('provider','provider-identity-changed')]:
   with self.subTest(case=case),Control() as c:
    runner=FakeRunner(c.directory,c.packet,c.bundle);runner.execute('infrastructure');runner.execute('dns')
    if case=='apps':runner.pods.append('eventstore')
    if case=='nonempty':runner.size=b'1'
    if case=='save':runner.save_result=b'ERROR'
    if case=='rdb':runner.rdb=b'not-rdb'
    if case in ('proof','provider'):
     runner.execute('backup')
     if case=='proof':
      file=runner.journal/'initial-empty.rdb';file.chmod(0o600);file.write_bytes(b'changed')
     else:runner.redis_uid='replacement'
     self.refuse(code,runner.backup)
    else:self.refuse(code,lambda:runner.execute('backup'))

 def test_cli_requires_review_and_preserves_channels_without_external_calls(self):
  with Control() as c,patch.object(subprocess,'run') as external,patch.object(phases,'Runner') as runner:
   stdout=io.StringIO();stderr=io.StringIO()
   with patch('sys.argv',['phases.py','--packet',str(c.directory),'--review-sha256',c.hash,'--phase','infrastructure']),__import__('contextlib').redirect_stdout(stdout),__import__('contextlib').redirect_stderr(stderr):result=phases.main()
   self.assertEqual(1,result);self.assertEqual('',stdout.getvalue());self.assertEqual('Phase stopped: deployment-review-required. Preserve the journal; do not retry uncertain writes.\n',stderr.getvalue());external.assert_not_called();runner.assert_not_called()
   stdout=io.StringIO();stderr=io.StringIO()
   with patch('sys.argv',['phases.py','--packet',str(c.directory),'--review-sha256',c.hash]),__import__('contextlib').redirect_stdout(stdout),__import__('contextlib').redirect_stderr(stderr):result=phases.main()
   self.assertEqual(0,result);self.assertEqual('Reviewed packet/local inputs verified; no resources applied.\n',stdout.getvalue());self.assertEqual('',stderr.getvalue());runner.assert_not_called()

 def test_poll_and_redirect_refusals(self):
  values=iter([0,1,2,3,4,5]);self.refuse('observation-deadline-exceeded',lambda:phases.poll(lambda:False,bool,seconds=3,clock=lambda:next(values),pause=lambda _:None))
  self.refuse('application-redirect-refused',lambda:phases.NoRedirect().redirect_request(None))
  with patch.object(phases,'build_opener') as network:self.refuse('local-owner-path-required',lambda:phases.request(18170,'https://outside',''));network.assert_not_called()


class RecoveryTests(unittest.TestCase):
 refuse=V1Tests.refuse
 setup_runner=lambda self,c,until:WriteFailureTests.setup_runner(self,c,until)

 def test_job_retry_replacement_and_template_boundaries(self):
  for case in ('backoffLimit','completions','parallelism','replacement','failure','manual','selector','manager','template'):
   with self.subTest(case=case),Control() as c,patch.object(subprocess,'run') as external:
    docs=[]
    for name in c.packet['files']:docs+=list(yaml.safe_load_all((c.directory/name).read_text()))
    job=next(d for d in docs if d['kind']=='Job');spec=job['spec']
    if case in ('backoffLimit','completions','parallelism'):spec[case]=2
    if case=='replacement':spec['podReplacementPolicy']='TerminatingOrFailed'
    if case=='failure':spec['podFailurePolicy']['rules'][0]['action']='Ignore'
    if case=='manual':spec['manualSelector']=True
    if case=='selector':spec['selector']={}
    if case=='manager':spec['managedBy']='other-controller'
    if case=='template':spec['template']['metadata']['labels']['hexalith.com/operator-task']='other'
    self.refuse('job-template-ownership-required' if case=='template' else 'non-retrying-job-required',lambda:prepare.validate_manifests(docs,c.packet['target']['namespace']))
    external.assert_not_called()

 def test_all_application_jobs_exist_before_first_liveness_and_readiness_after_writer(self):
  with Control() as c,patch.object(subprocess,'run') as external:
   runner=FakeRunner(c.directory,c.packet,c.bundle);oracle=Oracle(c.packet);observations=[]
   def observe(port,path,credential,body=None):
    if path in ('/alive','/ready'):
     self.assertEqual(set(prepare.APPS),set(runner.jobs));observations.append(path)
     if path=='/ready':self.assertEqual(1,len(oracle.writes))
    return oracle.request(port,path,credential,body)
   with patch.object(phases,'request',side_effect=observe):
    for phase in phases.PHASES:runner.execute(phase)
   self.assertEqual(['/alive','/ready'],observations);external.assert_not_called()

 def test_job_and_pod_identity_injection_and_terminal_refusals(self):
  cases=[('job-owner','application-job-identity-or-policy-changed'),('job-uid','application-job-identity-or-policy-changed'),('job-policy','application-job-identity-or-policy-changed'),('job-deleted','application-job-identity-or-policy-changed'),('job-terminal','application-job-terminal-stop'),('owner','application-pod-owner-mismatch'),('label','application-pod-owner-mismatch'),('namespace','application-pod-owner-mismatch'),('duplicates','application-pod-cardinality-invalid'),('uid','application-pod-identity-changed'),('pod-terminal','application-pod-terminal-or-restarted'),('restart','application-pod-terminal-or-restarted'),('pod-deleted','application-pod-terminal-or-restarted'),('missing-sidecar','dapr-sidecar-injection-required'),('duplicate-sidecar','dapr-sidecar-injection-required'),('native-invalid','dapr-sidecar-injection-required'),('unbound','application-pod-identity-not-bound')]
  for case,code in cases:
   with self.subTest(case=case),Control() as c,patch.object(subprocess,'run') as external:
    runner,_=self.setup_runner(c,'tenants');job=runner.jobs['eventstore'];pod=runner.application_pod('eventstore');before=len(runner.mutations)
    if case=='job-owner':job['metadata']['labels']['hexalith.com/operator-task']='other'
    if case=='job-uid':job['metadata']['uid']='replaced'
    if case=='job-policy':job['spec']['backoffLimit']=2
    if case=='job-deleted':job['metadata']['deletionTimestamp']='now'
    if case=='job-terminal':job['status']={'conditions':[{'type':'Failed','status':'True'}]}
    if case=='owner':pod['metadata']['ownerReferences'][0]['uid']='unrelated-job'
    if case=='label':pod['metadata']['labels']['app']='other'
    if case=='namespace':pod['metadata']['namespace']='other'
    if case=='uid':pod['metadata']['uid']='replaced'
    if case=='pod-terminal':pod['status']['phase']='Failed'
    if case=='restart':pod['status']['containerStatuses'][0]['restartCount']=1
    if case=='pod-deleted':pod['metadata']['deletionTimestamp']='now'
    if case in ('missing-sidecar','native-invalid'):pod['spec']['containers']=[x for x in pod['spec']['containers'] if x['name']!='daprd']
    if case=='duplicate-sidecar':pod['spec']['containers'].append({'name':'daprd'})
    if case=='native-invalid':pod['spec']['initContainers']=[{'name':'daprd'}]
    if case=='unbound':(runner.journal/'eventstore-identity.json').unlink()
    runner.pod_overrides['eventstore']=pod
    original=runner.kube
    def transport(*args,**kwargs):
     raw=original(*args,**kwargs)
     if case=='duplicates' and 'pods' in args and '-l' in args:return json.dumps({'items':[pod,copy.deepcopy(pod)]}).encode()
     return raw
    with patch.object(runner,'kube',side_effect=transport):self.refuse(code,lambda:runner.app_pod('eventstore'))
    self.assertEqual(before,len(runner.mutations));external.assert_not_called()

 def test_native_sidecar_and_delayed_controller_pod_resolution(self):
  with Control() as c,patch.object(subprocess,'run') as external:
   runner,_=self.setup_runner(c,'tenants');pod=runner.application_pod('eventstore')
   pod['spec']['containers']=[x for x in pod['spec']['containers'] if x['name']!='daprd'];pod['spec']['initContainers']=[{'name':'daprd','restartPolicy':'Always'}]
   runner.pod_overrides['eventstore']=pod;(runner.journal/'eventstore-identity.json').unlink()
   runner.app_pod('eventstore',bind=True);self.assertEqual('native-init',json.loads((runner.journal/'eventstore-identity.json').read_text())['sidecarLayout'])
   runner.pods.remove('eventstore');self.assertIsNone(runner.app_pod('eventstore'))
   with patch.object(runner,'app_pod',return_value=None):
    with self.assertRaisesRegex(ValueError,'^application-pod-missing$'):
     with phases.Runner.forward(runner):pass
   runner.pods.append('eventstore');self.assertEqual(pod,runner.app_pod('eventstore'));external.assert_not_called()

 def test_changed_domain_pod_blocks_writer_before_post(self):
  with Control() as c,patch.object(subprocess,'run') as external:
   runner,_=self.setup_runner(c,'writer');pod=runner.application_pod('tenants');pod['metadata']['uid']='replaced';runner.pod_overrides['tenants']=pod
   before=len(runner.mutations)
   with patch.object(phases,'request') as request:
    self.refuse('application-pod-identity-changed',lambda:runner.execute('writer'));request.assert_not_called()
   self.assertEqual(before,len(runner.mutations));external.assert_not_called()

 def test_get_transient_startup_failures_are_bounded_and_retained(self):
  failures=[phases.RemoteDisconnected('private diagnostic'),phases.URLError(ConnectionRefusedError()),ConnectionResetError(),TimeoutError()]
  with Control() as c:
   runner=FakeRunner(c.directory,c.packet,c.bundle);responses=[]
   for error in failures:
    with patch.object(phases,'build_opener') as network:
     network.return_value.open.side_effect=error;responses.append(phases.request(18170,'/alive','private-token'))
     self.assertEqual(1,network.return_value.open.call_count)
   original=phases.poll;ticks=iter(range(20));values=iter(responses+[(200,None)])
   def bounded(action,accept,seconds=60):return original(action,accept,seconds=10,clock=lambda:next(ticks),pause=lambda _:None)
   with patch.object(phases,'poll',side_effect=bounded):self.assertEqual((200,None),runner.observe(lambda:next(values),lambda x:x[0]==200))
   log=(runner.journal/'observations.jsonl').read_text();self.assertNotIn('private-token',log);self.assertNotIn('private diagnostic',log);self.assertIn('RemoteDisconnected',log)
   ticks=iter(range(20))
   with patch.object(phases,'poll',side_effect=bounded):self.refuse('observation-deadline-exceeded',lambda:runner.observe(lambda:responses[0],lambda x:x[0]==200))

 def test_post_transport_uncertainty_is_not_retried_and_forward_cli_is_read_only(self):
  for error in (phases.RemoteDisconnected('private'),phases.URLError(ConnectionRefusedError()),TimeoutError()):
   with self.subTest(error=type(error).__name__),patch.object(phases,'build_opener') as network:
    network.return_value.open.side_effect=error
    with self.assertRaises(type(error)):phases.request(18170,'/api/v1/commands','private-token',{'synthetic':'only'})
    self.assertEqual(1,network.return_value.open.call_count)
  with Control() as c,patch.object(phases,'Runner') as runner:
   stdout=io.StringIO();stderr=io.StringIO()
   with patch('sys.argv',['phases.py','--packet',str(c.directory),'--review-sha256',c.hash,'--forward','--execute-reviewed']),__import__('contextlib').redirect_stdout(stdout),__import__('contextlib').redirect_stderr(stderr):result=phases.main()
   self.assertEqual(1,result);self.assertEqual('',stdout.getvalue());self.assertEqual('Phase stopped: forward-cannot-execute-phase. Preserve the journal; do not retry uncertain writes.\n',stderr.getvalue());runner.assert_not_called()


class WriteFailureTests(unittest.TestCase):
 refuse=V1Tests.refuse
 def setup_runner(self,c,until):
  runner=FakeRunner(c.directory,c.packet,c.bundle);oracle=Oracle(c.packet)
  with patch.object(phases,'request',side_effect=oracle.request):
   for phase in phases.PHASES:
    if phase==until:break
    runner.execute(phase)
  return runner,oracle

 def test_uncertain_writer_is_submitted_once_and_blocks_further_phases(self):
  with Control() as c,patch.object(subprocess,'run') as external:
   runner,oracle=self.setup_runner(c,'writer');attempts=[]
   def uncertain(port,path,credential,body=None):
    if path.endswith('/activate'):attempts.append(body);return 504,None
    return oracle.request(port,path,credential,body)
   with patch.object(phases,'request',side_effect=uncertain):
    self.refuse('writer-activation-uncertain-stop',lambda:runner.execute('writer'))
    self.refuse('phase-already-attempted-do-not-retry',lambda:runner.execute('writer'))
    self.refuse('required-phase-incomplete',lambda:runner.execute('seed'))
   self.assertEqual(1,len(attempts));self.assertEqual(set(prepare.APPS)|{'redis'},set(runner.pods));external.assert_not_called()

 def test_terminal_seed_failure_never_submits_membership_or_replays_command(self):
  with Control() as c,patch.object(subprocess,'run') as external:
   runner,oracle=self.setup_runner(c,'seed');writes=[]
   def terminal(port,path,credential,body=None):
    if path=='/api/v1/commands':writes.append(body);return 202,{}
    if '/status/' in path:return 200,{'status':'Rejected'}
    return oracle.request(port,path,credential,body)
   with patch.object(phases,'request',side_effect=terminal):
    self.refuse('seed-command-terminal-failure',lambda:runner.execute('seed'))
    self.refuse('phase-already-attempted-do-not-retry',lambda:runner.execute('seed'))
    self.refuse('required-phase-incomplete',lambda:runner.execute('handoff'))
   self.assertEqual(['create-tenant'],[w['commandType'] for w in writes]);self.assertFalse((c.private/'operator-home').exists());external.assert_not_called()

 def test_missing_administrator_or_membership_stops_before_handoff(self):
  original=phases.poll
  for case in ('administrator','membership'):
   with self.subTest(case=case),Control() as c,patch.object(subprocess,'run') as external:
    runner,oracle=self.setup_runner(c,'administrator' if case=='administrator' else 'seed')
    def missing(port,path,credential,body=None):
     if path=='/api/v1/queries':
      if case=='administrator':return 200,{'payload':{'items':[]}}
      return 200,{'payload':{'tenantId':c.packet['tenant'],'members':[]}}
     return oracle.request(port,path,credential,body)
    def bounded(action,accept,seconds=60,**kwargs):
     ticks=iter(range(20));return original(action,accept,seconds=3,clock=lambda:next(ticks),pause=lambda _:None)
    with patch.object(phases,'request',side_effect=missing),patch.object(phases,'poll',side_effect=bounded):
     self.refuse('observation-deadline-exceeded',lambda:runner.execute('administrator' if case=='administrator' else 'seed'))
     self.refuse('required-phase-incomplete',lambda:runner.execute('writer' if case=='administrator' else 'handoff'))
    self.assertFalse((c.private/'operator-home').exists());self.assertEqual(0 if case=='administrator' else 3,len(oracle.writes));external.assert_not_called()


if __name__=='__main__':unittest.main()
