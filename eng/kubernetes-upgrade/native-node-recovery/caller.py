"""Explicit authenticated integration points for exact temporary capture helper.

No CLI and no implicit permission. A qualified caller authenticates the signed
policy, original immutable signature/version readbacks and all current source,
window, tool and destination bindings before this function can create anything.
Recovery acceptance and immutable publication/isolated restore are separate.
"""
import copy,hashlib,importlib.util,json,os,re,time,traceback
from pathlib import Path
from types import SimpleNamespace
HERE=Path(__file__).resolve().parent

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value
c=module('caller_node_capture',HERE/'capture.py')
t=module('caller_binary_transfer',HERE/'transfer.py')
# The16MiB public-tool input path uses the already bounded64MiB PG pipe helper.
import sys
sys.path.insert(0,str(HERE.parents[1]/'recovery-preparation'))
from telemetry_restore import bounded_run


def require(ok,why):
 if not ok:raise ValueError(why)


def assert_owned(observed,desired,uid):
 if desired['kind']=='Pod':return c.metadata.assert_owned(observed,desired,uid)
 require(observed.get('apiVersion')==desired['apiVersion'] and observed.get('kind')==desired['kind']
         and observed.get('metadata',{}).get('uid')==uid and uid
         and all(observed['metadata'].get(k)==desired['metadata'][k] for k in ('name','namespace','labels','annotations'))
         and not observed['metadata'].get('ownerReferences') and not observed['metadata'].get('finalizers'),
         'node-capture-owned-policy-identity-refused')
 actual=copy.deepcopy(observed.get('spec',{}));actual.setdefault('ingress',[])
 require(actual==desired['spec'],'node-capture-owned-policy-scope-refused')


def assert_running(observed,desired,uid):
 assert_owned(observed,desired,uid)
 statuses=observed.get('status',{}).get('containerStatuses',[])
 require(observed.get('status',{}).get('phase')=='Running' and len(statuses)==1
         and statuses[0].get('name')=='metadata' and statuses[0].get('restartCount')==0
         and statuses[0].get('state',{}).get('running') is not None
         and statuses[0].get('imageID','').rsplit('@',1)[-1]==desired['spec']['containers'][0]['image'].rsplit('@',1)[-1]
         and re.fullmatch(r'containerd://[a-f0-9]{64}',statuses[0].get('containerID','')),
         'node-capture-running-image-or-container-profile-refused')


def collect(args,objects,config,age_public_bytes,attempt,authorize):
 """Capture once, retrieve onlyciphertext, prove readback, exact UID cleanup."""
 c.validate(config)
 require(callable(authorize) and authorize('node-etcd-capture-before-helper') is True,
         'node-capture-signed-policy-or-immutable-readback-refused')
 require(type(age_public_bytes) is bytes and len(age_public_bytes)==config['ageBytes']
         and hashlib.sha256(age_public_bytes).hexdigest()==config['ageSha256'],
         'node-capture-public-tool-bytes-refused')
 require(len(objects)==2 and [v['kind'] for v in objects]==['NetworkPolicy','Pod'],
         'node-capture-helper-manifest-set-refused')
 require(objects==c.manifests(args.attempt_id,args.node,args.node_uid,args.namespace_uid,
                            objects[-1]['spec']['containers'][0]['image'],config),
         'node-capture-exact-manifest-reconstruction-refused')
 client=c.metadata.Client(args,attempt);owned=[];cleanup=[];before=None;summary=None;error=None;original=None
 artifacts={};readbacks={};source_revalidated=False;preserved=False;sequence=0
 def source():
  for path,uid in [('/api/v1/nodes/'+args.node,args.node_uid),('/api/v1/namespaces/kube-system',args.namespace_uid)]:
   require(client.run('get',path)['metadata']['uid']==uid,'node-capture-source-uid-drift')
 def native_prefix(pod,uid):
  source();assert_running(client.run('get',c.metadata.resource_path(pod)),pod,uid)
  require(time.time()<config['deadlineEpoch'],'node-capture-exec-absolute-deadline')
  return [str(args.kubectl),'--kubeconfig',str(args.kubeconfig),'--context',args.context,
          '--cache-dir',str(args.cache_dir),'--request-timeout=300s','-n','kube-system','exec','-i',
          pod['metadata']['name'],'-c','metadata','--','/usr/local/bin/python3','-I','-S','-B','-c']
 try:
  before=client.protected_state();source()
  for desired in objects:
   require(authorize('node-etcd-capture-helper-create') is True,'node-capture-authentication-expired')
   path=c.metadata.resource_path(desired);require(client.run('get',path,absent=True) is None,'node-capture-helper-name-preexists')
   entry=[desired,None];owned.append(entry)
   current=client.run('create',path.rsplit('/',1)[0],desired);entry[1]=current['metadata']['uid'];assert_owned(current,desired,entry[1])
  pod,uid=owned[-1];deadline=time.monotonic()+180
  while True:
   current=client.run('get',c.metadata.resource_path(pod));assert_owned(current,pod,uid)
   if current.get('status',{}).get('phase')=='Running':
    assert_running(current,pod,uid);break
   require(current.get('status',{}).get('phase') not in ('Failed','Succeeded') and time.monotonic()<deadline,
           'node-capture-helper-start-refused');time.sleep(2)
  require(authorize('node-etcd-capture-public-tool-install') is True,'node-capture-authentication-expired')
  argv=native_prefix(pod,uid)+[c.installation_program(config['ageSha256'],config['ageBytes'])]
  attempt.encrypt('node-capture-public-tool-intent',c.metadata.canonical({'argv':argv,'publicInputSha256':config['ageSha256'],'publicInputBytes':len(age_public_bytes)}),args.age,args.recipient)
  installed=bounded_run(argv,age_public_bytes,timeout=60)
  attempt.encrypt('node-capture-public-tool-result',c.metadata.canonical({'exitCode':installed.returncode,'stdout':installed.stdout.decode(errors='replace'),'stderr':installed.stderr.decode(errors='replace')}),args.age,args.recipient)
  require(installed.returncode==0 and installed.stdout.strip()==b'public-tool-exact-bytes-installed',
          'node-capture-public-tool-install-refused')
  poll=("import json,os\nif os.path.exists('/work/result.json') and not os.path.lexists('/work/result.partial'):\n"
        " with open('/work/result.json','rb') as source:raw=source.read(4194305)\n"
        " if len(raw)>4194304:raise ValueError('capture-summary-size')\n"
        " print(raw.decode('utf-8'))\nelse:print('null')\n")
  deadline=time.monotonic()+480
  while True:
   current=client.run('get',c.metadata.resource_path(pod));assert_owned(current,pod,uid)
   require(current.get('status',{}).get('phase')=='Running' and time.monotonic()<deadline,'node-capture-process-failed-or-timeout')
   result=bounded_run(native_prefix(pod,uid)+[poll],timeout=25)
   require(result.returncode==0,'node-capture-summary-exec-refused')
   value=json.loads(result.stdout)
   if value is not None:summary=value;break
   time.sleep(2)
  attempt.encrypt('node-capture-local-private-summary',c.metadata.canonical(summary),args.age,args.recipient)
  require(summary.get('kind')=='local-node-etcd-capture-only' and summary.get('sourceEtcdIdentity')==config['expectedEtcdIdentity']
          and all(summary.get(k) is True for k in ('sourceMountsReadonly','snapshotPlaintextOnlyInMemoryBackedHelper',
              'plaintextSnapshotRemoved','sourceNodeMetadataUnchanged')) and summary.get('plaintextArchiveFileWritten') is False
          and summary.get('providerReadback') is False and summary.get('recoveryAccepted') is False
          and set(summary.get('artifacts',{}))=={'snapshot.db.age','node-recovery.tar.gz.age'},
          'node-capture-local-summary-profile-refused')
  for name,binding in summary['artifacts'].items():
   prefix=native_prefix(pod,uid);path=attempt.directory/name
   require(binding.get('encryptedBeforeTransport') is True,'node-capture-private-transport-refused')
   artifacts[name]=t.read_stream(prefix+[t.retrieval_program(name,binding['ciphertextBytes'])],
        binding['ciphertextBytes'],binding['ciphertextSha256'],output=path,timeout=240)
   readbacks[name]=t.read_stream([str(args.age),'-d','-i',str(args.readback_identity),str(path)],
        binding['plaintextBytes'],binding['plaintextSha256'],timeout=240,maximum=t.MAX_PLAINTEXT)
  source();source_revalidated=True
  preserved=client.protected_state()==before
  require(preserved,'node-capture-protected-namespace-or-storage-drift')
 except BaseException as exc:
  original=exc;error=type(exc).__name__+(':'+str(exc) if isinstance(exc,ValueError) else '')
  try:attempt.encrypt('node-capture-original-failure',c.metadata.canonical({'error':error,'traceback':traceback.format_exc()}),args.age,args.recipient)
  except Exception:pass
  if owned and owned[-1][0]['kind']=='Pod' and owned[-1][1]:
   try:
    pod,uid=owned[-1];assert_owned(client.run('get',c.metadata.resource_path(pod)),pod,uid)
    argv=[str(args.kubectl),'--kubeconfig',str(args.kubeconfig),'--context',args.context,
          '--cache-dir',str(args.cache_dir),'--request-timeout=20s','-n','kube-system',
          'logs',pod['metadata']['name'],'-c','metadata','--tail=80']
    logs=bounded_run(argv,timeout=25)
    attempt.encrypt('node-capture-original-process-diagnostics',c.metadata.canonical({
      'exitCode':logs.returncode,'stdoutBase64':__import__('base64').b64encode(logs.stdout).decode(),
      'stderrBase64':__import__('base64').b64encode(logs.stderr).decode()}),args.age,args.recipient)
   except Exception:pass
 finally:
  for desired,uid in reversed(owned):
   try:
    path=c.metadata.resource_path(desired);current=client.run('get',path,absent=True)
    if current is not None:
     require(uid is not None,'node-capture-create-outcome-unverified');assert_owned(current,desired,uid)
     client.run('delete',path,{'apiVersion':'v1','kind':'DeleteOptions','preconditions':{'uid':uid,'resourceVersion':current['metadata']['resourceVersion']},'propagationPolicy':'Foreground'})
     deadline=time.monotonic()+60
     while client.run('get',path,absent=True) is not None:
      require(time.monotonic()<deadline,'node-capture-owned-cleanup-timeout');time.sleep(1)
    cleanup.append({'kind':desired['kind'],'uid':uid,'absenceVerified':True})
   except BaseException as exc:cleanup.append({'kind':desired['kind'],'uid':uid,'absenceVerified':False,'errorType':type(exc).__name__})
 result={'kind':'native-local-encrypted-node-etcd-capture-result','error':error,'localCaptureCompleted':summary is not None,
         'summary':summary,'artifacts':artifacts,'agentRecipientReadbacks':readbacks,'cleanup':cleanup,
         'cleanupPassed':bool(owned) and len(cleanup)==len(owned) and all(v['absenceVerified'] for v in cleanup),
         'helperCreationIntents':len(owned),'helperObjectsCreated':sum(uid is not None for _,uid in owned),
         'helperCreateOutcomesUnverified':sum(uid is None for _,uid in owned),'requests':client.requests,
         'sourceNodeAndNamespaceUidRevalidated':source_revalidated,'protectedNamespacePvcPvBindingsUnchanged':preserved,
         'immutableProviderReadback':False,'isolatedRestorePassed':False,'sourceRecoveryAccepted':False}
 return result
