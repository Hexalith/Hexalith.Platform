#!/usr/bin/env python3
"""Execute only an explicitly reviewed v1 phase; default verification is local/read-only."""
from contextlib import contextmanager
from datetime import datetime, timezone
import argparse
import base64
import hashlib
import hmac
from http.client import RemoteDisconnected
import ipaddress
import json
import os
from pathlib import Path
import signal
import socket
import stat
import subprocess
import time
import yaml
from urllib.error import HTTPError, URLError
from urllib.request import Request, ProxyHandler, HTTPRedirectHandler, build_opener
from prepare import APPS, PLATFORM, MCPCLI, JOB_FAILURE_POLICY, digest, require

PHASES=('infrastructure','dns','backup','gateway','tenants','parties','startup','sidecars','metadata','administrator','writer','seed','handoff','cleanup')
DEPENDENCY={'dns':'infrastructure','backup':'dns','gateway':'backup','tenants':'gateway','parties':'tenants','startup':'parties','sidecars':'startup','metadata':'sidecars','administrator':'metadata','writer':'administrator','seed':'writer','handoff':'seed','cleanup':'infrastructure'}


def save(path,value):Path(path).write_text(json.dumps(value,indent=2)+'\n')
def private_json(path):
 p=Path(path);require(p.is_absolute() and p.is_file() and not p.is_symlink() and stat.S_IMODE(p.stat().st_mode)==0o600,'private-file-mode-or-path-invalid')
 return json.loads(p.read_text())


def verify(packet_dir,approved_hash,now=None):
 packet_dir=Path(packet_dir);path=packet_dir/'review.json'
 require(digest(path)==approved_hash,'review-hash-mismatch')
 packet=json.loads(path.read_text());target=packet['target']
 require(digest(Path(__file__))==packet['runnerSha256'],'runner-changed-after-review')
 require(hashlib.sha256((Path(__file__).parent/'prepare.py').read_bytes()).hexdigest()==packet['prepareSha256'],'runner-helper-changed-after-review')
 require(all(digest(MCPCLI/name)==sha for name,sha in packet['fixtureHashes'].items()),'fixture-changed-after-review')
 require(all(digest(Path(__file__).parent/'evidence'/name)==sha for name,sha in packet['evidenceHashes'].items()),'evidence-changed-after-review')
 require(packet['schema']=='mcpcli-v1-operator-review/v1' and target['context']=='jpiquot@local' and target['api']=='https://192.168.1.30:6443'
  and target['namespace'].startswith('mcpcli-417-') and packet['tenant']==target['namespace'],'review-scope-mismatch')
 require((now or datetime.now(timezone.utc))<datetime.fromisoformat(packet['expiresAt']),'review-expired')
 require(all((packet_dir/name).is_file() and digest(packet_dir/name)==sha for name,sha in packet['files'].items()),'manifest-hash-mismatch')
 require(digest(packet['privateManifest'])==packet['privateManifestSha256'],'private-input-manifest-changed')
 private=private_json(packet['privateManifest']);directory=Path(packet['privateManifest']).parent
 require(not directory.is_relative_to(PLATFORM) and not directory.is_relative_to(MCPCLI),'private-inputs-inside-repository')
 for file,sha in private['files'].items():
  p=Path(file);require(p.is_relative_to(directory) and p.is_file() and not p.is_symlink() and stat.S_IMODE(p.stat().st_mode)==0o600 and digest(p)==sha,'private-input-changed-or-public')
 return packet,private


def token(packet,private,operator=False):
 key=Path(private['secrets']['mcpcli-jwt']['signing-key']).read_bytes();now=int(time.time())
 payload={'iss':'mcpcli-live-tests','aud':'hexalith-eventstore','sub':packet['operatorSubject'] if operator else 'mcpcli-live-admin',
  'tenants':json.dumps(['system',packet['tenant']]),'domains':json.dumps(['tenants','party'] if operator else ['tenants','party','global-administrators']),
  'permissions':json.dumps(['command:submit','query:read']),'global_admin':'false' if operator else 'true','iat':now,'nbf':now-30,
  'exp':min(now+(packet['operatorTokenTtlSeconds'] if operator else 600),int(datetime.fromisoformat(packet['expiresAt']).timestamp()))}
 encode=lambda raw:base64.urlsafe_b64encode(raw).decode().rstrip('=')
 unsigned=encode(b'{"alg":"HS256","typ":"JWT"}')+'.'+encode(json.dumps(payload,separators=(',',':')).encode())
 return unsigned+'.'+encode(hmac.new(key,unsigned.encode(),hashlib.sha256).digest()),payload['exp']


class NoRedirect(HTTPRedirectHandler):
 def redirect_request(self,*args,**kwargs):raise ValueError('application-redirect-refused')


def request(port,path,credential,body=None):
 require(path.startswith('/') and '://' not in path,'local-owner-path-required')
 headers={}
 if credential:headers['Authorization']='Bearer '+credential
 if body is not None:headers['Content-Type']='application/json'
 req=Request('http://127.0.0.1:'+str(port)+path,headers=headers,data=json.dumps(body).encode() if body is not None else None)
 try:
  with build_opener(ProxyHandler({}),NoRedirect()).open(req,timeout=15) as response:
   raw=response.read();return response.status,json.loads(raw) if raw and response.headers.get('Content-Type','').startswith('application/json') else None
 except HTTPError as error:return error.code,None
 except (URLError,RemoteDisconnected,ConnectionError,TimeoutError) as error:
  if body is not None:raise
  return 0,{'observationTransportError':type(error).__name__}


def poll(action,accept,seconds=60,clock=time.monotonic,pause=time.sleep):
 deadline=clock()+seconds
 while clock()<deadline:
  result=action()
  if accept(result):return result
  pause(0.5)
 raise ValueError('observation-deadline-exceeded')


class Runner:
 def __init__(self,packet_dir,packet,private):
  self.directory=Path(packet_dir);self.packet=packet;self.private=private;self.namespace=packet['target']['namespace']
  self.journal=Path(packet['privateManifest']).parent/'execution';self.journal.mkdir(mode=0o700,exist_ok=True)
  self.port=packet['gatewayPort']

 def kube(self,*args,input=None,timeout=30):
  cmd=['kubectl','--context=jpiquot@local','--request-timeout=10s',*args]
  result=subprocess.run(cmd,input=input,capture_output=True,timeout=timeout)
  require(result.returncode==0,'kubernetes-operation-failed')
  return result.stdout

 def scoped(self,*args,**kwargs):return self.kube('-n',self.namespace,*args,**kwargs)
 def prior(self,phase):
  path=self.journal/(phase+'.json');require(path.is_file() and json.loads(path.read_text()).get('status')=='complete','required-phase-incomplete')
  return json.loads(path.read_text())
 def uid(self):
  ns=json.loads(self.kube('get','namespace',self.namespace,'-o','json'))
  require(ns['metadata'].get('labels',{}).get('hexalith.com/operator-task')==self.namespace and ns['metadata']['uid']==json.loads((self.journal/'infrastructure.json').read_text()).get('namespaceUid'),'namespace-uid-or-owner-mismatch')
  return ns['metadata']['uid']

 def observe(self,action,accept,seconds=60):
  def read():
   result=action()
   with (self.journal/'observations.jsonl').open('a') as log:
    log.write(json.dumps({'at':datetime.now(timezone.utc).isoformat(),'status':result[0],
     'transportError':(result[1] if isinstance(result[1],dict) else {}).get('observationTransportError')})+'\n')
   return result
  return poll(read,accept,seconds)

 def app_pod(self,app,bind=False):
  self.uid();job=json.loads(self.scoped('get','job',app,'-o','json'));meta=job['metadata'];spec=job['spec']
  require(meta['uid']==json.loads((self.journal/(app+'-job.json')).read_text())['jobUid'] and
   meta.get('labels',{}).get('hexalith.com/operator-task')==self.namespace and not meta.get('deletionTimestamp') and
   spec.get('backoffLimit')==0 and spec.get('completions')==spec.get('parallelism')==1 and spec.get('podReplacementPolicy')=='Failed' and
   spec.get('podFailurePolicy')==JOB_FAILURE_POLICY,'application-job-identity-or-policy-changed')
  require(not any(c.get('status')=='True' and c['type'] in ('Failed','FailureTarget','Complete') for c in job.get('status',{}).get('conditions',[])),
   'application-job-terminal-stop')
  selected=json.loads(self.scoped('get','pods','-l','hexalith.com/operator-task='+self.namespace+',app='+app,'-o','json'))['items']
  require(len(selected)<=1,'application-pod-cardinality-invalid')
  if not selected:return None
  pod=selected[0];pmeta=pod['metadata'];owners=pmeta.get('ownerReferences',[])
  require(pmeta.get('namespace')==self.namespace and pmeta.get('labels',{}).get('app')==app and
   pmeta.get('labels',{}).get('hexalith.com/operator-task')==self.namespace and
   len([o for o in owners if o.get('controller') is True and o.get('kind')=='Job' and o.get('name')==app and o.get('uid')==meta['uid']])==1,
   'application-pod-owner-mismatch')
  require(not pmeta.get('deletionTimestamp') and pod.get('status',{}).get('phase') not in ('Failed','Succeeded') and
   all(s.get('restartCount',0)==0 and 'terminated' not in s.get('state',{}) for s in pod.get('status',{}).get('containerStatuses',[]) if s['name']==app),
   'application-pod-terminal-or-restarted')
  containers=pod['spec']['containers'];native=[c for c in pod['spec'].get('initContainers',[]) if c['name']=='daprd']
  ordinary=[c for c in containers if c['name']=='daprd']
  require(len(ordinary)+len(native)==1 and (not native or native[0].get('restartPolicy')=='Always'),'dapr-sidecar-injection-required')
  identity={'jobUid':meta['uid'],'podUid':pmeta['uid'],'podName':pmeta['name'],'sidecarLayout':'native-init' if native else 'ordinary'}
  path=self.journal/(app+'-identity.json')
  if path.exists():require(json.loads(path.read_text())==identity,'application-pod-identity-changed')
  elif bind:save(path,identity)
  else:raise ValueError('application-pod-identity-not-bound')
  return pod

 def sidecar_ready(self,app,bind=False):
  pod=self.app_pod(app)
  def record(ready,state):
   with (self.journal/'sidecar-observations.jsonl').open('a') as log:
    log.write(json.dumps({'at':datetime.now(timezone.utc).isoformat(),'app':app,'podUid':pod['metadata']['uid'] if pod else None,'ready':ready,'state':state})+'\n')
  if pod is None:record(False,['pod-missing']);return False
  native=any(c['name']=='daprd' for c in pod['spec'].get('initContainers',[]))
  statuses=pod.get('status',{}).get('initContainerStatuses' if native else 'containerStatuses',[])
  status=next((s for s in statuses if s['name']=='daprd'),None)
  if status is None:record(False,['status-missing']);return False
  ready=status.get('ready') is True and 'running' in status.get('state',{})
  record(ready,list(status.get('state',{})))
  require(status.get('restartCount',0)==0 and 'terminated' not in status.get('state',{}),'sidecar-terminal-or-restarted')
  if not ready:return False
  require(bool(status.get('containerID')),'sidecar-runtime-identity-required')
  identity={'podUid':pod['metadata']['uid'],'containerId':status['containerID'],'layout':'native-init' if native else 'ordinary'}
  path=self.journal/(app+'-sidecar-identity.json')
  if path.exists():require(json.loads(path.read_text())==identity,'sidecar-runtime-identity-changed')
  elif bind:save(path,identity)
  else:raise ValueError('sidecar-runtime-identity-not-bound')
  return True

 def gateway_api_boundary(self):
  pod=self.app_pod('eventstore');require(pod is not None,'application-pod-missing')
  spec=pod['spec'];app=next(c for c in spec['containers'] if c['name']=='eventstore')
  sidecar=next(c for c in spec['containers']+spec.get('initContainers',[]) if c['name']=='daprd')
  args=sidecar.get('args',[]);env={e['name']:e for e in app.get('env',[])};side_env={e['name']:e for e in sidecar.get('env',[])}
  address=args[args.index('--dapr-listen-addresses')+1] if '--dapr-listen-addresses' in args and args.index('--dapr-listen-addresses')+1<len(args) else None
  require(address=='127.0.0.1' and not spec.get('hostNetwork') and not spec.get('hostPID') and ('DAPR_HTTP_ENDPOINT' not in env or env['DAPR_HTTP_ENDPOINT'].get('value') in
   ('http://localhost:3500','http://127.0.0.1:3500')) and ('DAPR_HTTP_PORT' not in env or env['DAPR_HTTP_PORT'].get('value')=='3500'),'gateway-runtime-loopback-required')
  require('DAPR_API_TOKEN' not in env and 'DAPR_API_TOKEN' not in side_env and not app.get('envFrom') and not sidecar.get('envFrom') and
   'dapr.io/api-token-secret' not in pod['metadata'].get('annotations',{}),'gateway-runtime-token-mode-required')
  def reference(values,name,secret):
   ref=values.get(name,{}).get('valueFrom',{}).get('secretKeyRef',{})
   return ref.get('name')==secret and ref.get('key')==('signing-key' if name=='Authentication__JwtBearer__SigningKey' else 'token')
  require(reference(env,'APP_API_TOKEN','mcpcli-eventstore-app') and reference(side_env,'APP_API_TOKEN','mcpcli-eventstore-app'),'gateway-runtime-app-channel-required')
  require(reference(env,'Authentication__JwtBearer__SigningKey','mcpcli-jwt') and env.get('Authentication__JwtBearer__Issuer',{}).get('value')=='mcpcli-live-tests' and
   env.get('Authentication__JwtBearer__Audience',{}).get('value')=='hexalith-eventstore','gateway-runtime-jwt-required')
  require('--enable-mtls' in args,'gateway-runtime-mtls-required')
  return pod

 def dns_probe(self,host):
  cmd=['kubectl','--context=jpiquot@local','--request-timeout=10s','-n',self.namespace,'exec','redis','-c','redis','--',*self.packet['dns']['lookupCommand'],host]
  try:
   result=subprocess.run(cmd,capture_output=True,timeout=12)
   return result.returncode,result.stdout.decode(errors='replace'),result.stderr.decode(errors='replace')
  except subprocess.TimeoutExpired as error:
   return 124,(error.stdout or b'').decode(errors='replace'),(error.stderr or b'').decode(errors='replace')

 def check_dns(self):
  infrastructure=self.prior('infrastructure')
  def provider():
   require(json.loads(self.scoped('get','pod','redis','-o','json'))['metadata']['uid']==infrastructure['redisPodUid'] and
    json.loads(self.scoped('get','pvc','task-data','-o','json'))['metadata']['uid']==infrastructure['pvcUid'],'dns-provider-identity-changed')
  require([p['metadata']['name'] for p in json.loads(self.scoped('get','pods','-o','json'))['items']]==['redis'],'applications-present-before-dns')
  provider();actual=json.loads(self.scoped('get','networkpolicy','task-isolation','-o','json'))
  expected=next(d for d in yaml.safe_load_all((self.directory/'infrastructure.yaml').read_text()) if d['kind']=='NetworkPolicy')
  require(actual['metadata'].get('labels',{}).get('hexalith.com/operator-task')==self.namespace and actual['spec']==expected['spec'],'dns-policy-changed')
  raw=self.scoped('exec','redis','-c','redis','--',*self.packet['dns']['resolverReadCommand']).decode()
  resolvers=[line.split()[1] for line in raw.splitlines() if line.strip().startswith('nameserver ')]
  require(resolvers==self.packet['dns']['allowedResolvers'],'pod-resolver-not-reviewed')
  pending=list(self.packet['dns']['names'])
  def resolve():
   while pending:
    provider();host=pending[0];code,stdout,stderr=self.dns_probe(host);addresses=[]
    for line in stdout.splitlines():
     try:
      address=ipaddress.ip_address(line.split()[0])
      if address.version==4:addresses.append(str(address))
     except (ValueError,IndexError):pass
    with (self.journal/'dns-observations.jsonl').open('a') as log:
     log.write(json.dumps({'at':datetime.now(timezone.utc).isoformat(),'host':host,'exitCode':code,'stdout':stdout[:2000],'stderr':stderr[:2000],'ipv4Addresses':sorted(set(addresses))})+'\n')
    if code==0 and addresses:pending.pop(0)
    else:return False
   return True
  try:poll(resolve,bool,self.packet['dns']['phaseDeadlineSeconds'])
  except ValueError as error:
   if str(error)=='observation-deadline-exceeded':raise ValueError('dns-resolution-deadline-exceeded') from error
   raise
  return {'resolvers':resolvers,'namesResolved':self.packet['dns']['names'],'policyUid':actual['metadata']['uid'],
   'policySpecSha256':hashlib.sha256(json.dumps(actual['spec'],sort_keys=True,separators=(',',':')).encode()).hexdigest()}

 @contextmanager
 def forward(self,require_ready=True,metadata=False):
  pods={app:self.app_pod(app) for app in APPS};require(all(pods.values()),'application-pod-missing');pod=pods['eventstore']
  if require_ready:require(all(self.sidecar_ready(app) for app in APPS),'sidecar-runtime-unready')
  if metadata:self.gateway_api_boundary()
  port=self.packet['metadataPort'] if metadata else self.port;remote_port=3500 if metadata else 8080
  with socket.socket() as probe:
   try:probe.bind(('127.0.0.1',port))
   except OSError:raise ValueError('private-forward-port-in-use')
  log=self.journal/('metadata-port-forward.log' if metadata else 'port-forward.log')
  with log.open('w') as output:
   child=subprocess.Popen(['kubectl','--context=jpiquot@local','-n',self.namespace,'port-forward','--address=127.0.0.1','pod/'+pod['metadata']['name'],str(port)+':'+str(remote_port)],stdout=output,stderr=output,start_new_session=True)
   try:
    poll(lambda:child.poll() is None and 'Forwarding from 127.0.0.1:' in log.read_text(),bool,30)
    yield child
   finally:
    if child.poll() is None:
     os.killpg(child.pid,signal.SIGTERM)
     try:child.wait(timeout=5)
     except subprocess.TimeoutExpired:os.killpg(child.pid,signal.SIGKILL);child.wait(timeout=5)

 def backup(self):
  proof=json.loads((self.journal/'initial-redis-backup.json').read_text());file=Path(proof['backupFile'])
  require(proof['databaseSize']==0 and proof['createdBeforeApplications'] is True and proof['namespaceUid']==self.uid()
   and file.parent==self.journal and file.read_bytes().startswith(b'REDIS') and digest(file)==proof['sha256'],'provider-backup-proof-invalid')
  require(json.loads(self.scoped('get','pod','redis','-o','json'))['metadata']['uid']==proof['redisPodUid'] and
   json.loads(self.scoped('get','pvc','task-data','-o','json'))['metadata']['uid']==proof['pvcUid'],'provider-identity-changed')
  return proof

 def create_app(self,app):
  self.backup();job=json.loads(self.scoped('create','-f',str(self.directory/(app+'.yaml')),'-o','json'))
  save(self.journal/(app+'-job.json'),{'jobUid':job['metadata']['uid']})
  pod=poll(lambda:self.app_pod(app,bind=True),bool,60)
  self.scoped('wait','--for=jsonpath={.status.phase}=Running','pod/'+pod['metadata']['name'],'--timeout=180s',timeout=190)
  self.app_pod(app)

 def execute(self,phase):
  require(phase in PHASES,'unknown-phase')
  path=self.journal/(phase+'.json');require(not path.exists(),'phase-already-attempted-do-not-retry')
  if phase in DEPENDENCY and phase!='cleanup':self.prior(DEPENDENCY[phase])
  api=self.kube('config','view','--minify','-o','jsonpath={.clusters[0].cluster.server}').decode().strip()
  require(api==self.packet['target']['api'],'context-api-mismatch')
  if phase!='infrastructure':self.uid()
  receipt={'phase':phase,'status':'uncertain','namespace':self.namespace,'startedAt':datetime.now(timezone.utc).isoformat()};save(path,receipt)
  if phase=='infrastructure':
   existing=self.kube('get','namespace',self.namespace,'--ignore-not-found','-o','json')
   require(not existing.strip(),'namespace-already-exists')
   self.kube('create','-f',str(self.directory/'namespace.yaml'))
   receipt['namespaceUid']=json.loads(self.kube('get','namespace',self.namespace,'-o','json'))['metadata']['uid'];save(path,receipt)
   for name,keys in self.private['secrets'].items():
    cmd=['create','secret','generic',name,*['--from-file='+key+'='+file for key,file in keys.items()],'--dry-run=client','-o','json']
    if name=='mcpcli-pull':cmd+=['--type=kubernetes.io/dockerconfigjson']
    document=self.scoped(*cmd);self.scoped('create','-f','-',input=document)
   self.scoped('create','-f',str(self.directory/'infrastructure.yaml'))
   self.scoped('create','-f',str(self.directory/'redis.yaml'))
   self.scoped('wait','--for=condition=Ready','pod/redis','--timeout=180s',timeout=190)
   ns=json.loads(self.kube('get','namespace',self.namespace,'-o','json'));receipt['namespaceUid']=ns['metadata']['uid']
   receipt['redisPodUid']=json.loads(self.scoped('get','pod','redis','-o','json'))['metadata']['uid']
   receipt['pvcUid']=json.loads(self.scoped('get','pvc','task-data','-o','json'))['metadata']['uid']
  elif phase=='dns':receipt.update(self.check_dns())
  elif phase=='backup':
   pods=json.loads(self.scoped('get','pods','-o','json'))['items'];require([p['metadata']['name'] for p in pods]==['redis'],'applications-present-before-backup')
   require(self.scoped('exec','redis','-c','redis','--','redis-cli','DBSIZE').strip()==b'0','task-store-not-empty')
   require(self.scoped('exec','redis','-c','redis','--','redis-cli','SAVE').strip()==b'OK','provider-native-backup-failed')
   raw=self.scoped('exec','redis','-c','redis','--','cat','/data/dump.rdb');require(raw.startswith(b'REDIS'),'provider-native-backup-invalid')
   backup=self.journal/'initial-empty.rdb';backup.write_bytes(raw);backup.chmod(0o400)
   proof={'databaseSize':0,'createdBeforeApplications':True,'backupFile':str(backup),'sha256':digest(backup),'namespaceUid':self.uid(),
    'redisPodUid':pods[0]['metadata']['uid'],'pvcUid':self.prior('infrastructure')['pvcUid'],'capturedAt':datetime.now(timezone.utc).isoformat()}
   save(self.journal/'initial-redis-backup.json',proof);receipt.update(backupSha256=proof['sha256'],redisPodUid=proof['redisPodUid'],pvcUid=proof['pvcUid'])
  elif phase in ('gateway','tenants','parties'):
   self.create_app('eventstore' if phase=='gateway' else phase)
  elif phase=='startup':
   for app in APPS:require(self.app_pod(app) is not None,'application-pod-missing')
   require(all(json.loads(self.scoped('get','service',app+'-dapr','-o','json'))['spec'].get('publishNotReadyAddresses') is True for app in APPS),'bootstrap-dapr-routing-required')
   with self.forward(require_ready=False):self.observe(lambda:request(self.port,'/alive',''),lambda x:x[0]==200)
  elif phase=='sidecars':
   poll(lambda:all([self.sidecar_ready(app,bind=True) for app in APPS]),bool,self.packet['dns']['sidecarDeadlineSeconds'])
   receipt['sidecarRuntimeReadinessObserved']=True
  elif phase=='metadata':
   def observe_metadata():
    self.gateway_api_boundary();require(self.sidecar_ready('eventstore'),'sidecar-runtime-unready')
    result=request(self.packet['metadataPort'],'/v1.0/metadata','')
    require(result[0] not in (401,403),'gateway-metadata-auth-incompatible')
    if result[0]==200 and isinstance(result[1],dict) and result[1].get('id'):
     require(result[1]['id']=='eventstore','gateway-metadata-identity-mismatch')
    return result
   with self.forward(metadata=True):
    result=self.observe(observe_metadata,lambda x:x[0]==200 and isinstance(x[1],dict) and x[1].get('id')=='eventstore' and
     isinstance(x[1].get('actorRuntime'),dict) and x[1]['actorRuntime'].get('hostReady') is True,self.packet['gatewayLocalApi']['metadataDeadlineSeconds'])
   receipt.update(metadataObserved=True,unauthenticatedGet=True,hostReady=True,metadataId=result[1]['id'],loopbackPort=self.packet['metadataPort'],forwardStopped=True,
    placement=result[1]['actorRuntime'].get('placement'),runtimeStatus=result[1]['actorRuntime'].get('runtimeStatus'))
  elif phase in ('administrator','writer','seed'):
   credential,_=token(self.packet,self.private)
   with self.forward():
    if phase=='administrator':
     query={'tenant':'system','domain':'global-administrators','aggregateId':'global-administrators','queryType':'get-global-administrators','projectionType':'global-administrators'}
     self.observe(lambda:request(self.port,'/api/v1/queries',credential,query),lambda x:x[0]==200 and any(i.get('userId')=='mcpcli-live-admin' for i in (x[1] or {}).get('payload',{}).get('items',[])))
     receipt['persistedAdministratorObserved']=True
    elif phase=='writer':
     proof=self.backup();body={'cutoverCommit':self.packet['gatewaySourceCommit'],'backupReference':'sha256:'+proof['sha256']+';file:'+proof['backupFile'],
      'writersQuiesced':True,'retryWorkersQuiesced':True,'downgradeProhibitedAcknowledged':True}
     status,result=request(self.port,'/api/v1/admin/projections/delivery-writer-protocol/activate',credential,body)
     require(status==200 and result and result.get('status')=='Activated','writer-activation-uncertain-stop')
     self.observe(lambda:request(self.port,'/ready',credential),lambda x:x[0]==200,30);receipt.update(activationCount=1,sourceCommit=body['cutoverCommit'],backupSha256=proof['sha256'],readinessObserved=True)
     for app in APPS:
      pod=self.app_pod(app);require(pod is not None,'application-pod-missing')
      self.scoped('wait','--for=condition=Ready','pod/'+pod['metadata']['name'],'--timeout=180s',timeout=190);self.app_pod(app)
    else:
     tenant=self.packet['tenant'];subject=self.packet['operatorSubject']
     for wire,payload in [('create-tenant',{'TenantId':tenant,'Name':'McpCli operator setup','Description':'Task-owned synthetic operator tenant'}),('add-user-to-tenant',{'TenantId':tenant,'UserId':subject,'Role':'TenantContributor'})]:
      value=(int(time.time()*1000)<<80)|int.from_bytes(os.urandom(10),'big');alphabet='0123456789ABCDEFGHJKMNPQRSTVWXYZ';message=''
      for _ in range(26):message=alphabet[value&31]+message;value>>=5
      body={'messageId':message,'correlationId':message,'tenant':'system','domain':'tenants','aggregateId':tenant,'commandType':wire,'payload':payload}
      save(self.journal/(wire+'-intent.json'),body)
      status,_=request(self.port,'/api/v1/commands',credential,body);require(status==202,'seed-command-uncertain-stop')
      def observe():
       response=request(self.port,'/api/v1/commands/status/'+message,credential)
       require((response[1] or {}).get('status') not in ('Rejected','Failed','TimedOut','DeadLettered'),'seed-command-terminal-failure')
       return response
      self.observe(observe,lambda x:x[0]==200 and (x[1] or {}).get('status')=='Completed' and x[1].get('committedEventSequence',0)>0 and x[1].get('eventCount',0)>0,45)
     query={'tenant':'system','domain':'tenants','aggregateId':tenant,'queryType':'get-tenant','projectionType':'tenants','payload':{'TenantId':tenant}}
     self.observe(lambda:request(self.port,'/api/v1/queries',credential,query),lambda x:x[0]==200 and (x[1] or {}).get('payload',{}).get('tenantId')==tenant and any(m.get('userId')==subject and m.get('role')=='TenantContributor' for m in (x[1] or {}).get('payload',{}).get('members',[])),45)
     receipt.update(persistedTenantObserved=True,persistedContributorObserved=True)
  elif phase=='handoff':
   require(all(self.app_pod(app) is not None for app in APPS),'application-pod-missing')
   require(all(self.sidecar_ready(app) for app in APPS),'sidecar-runtime-unready')
   value,expiry=token(self.packet,self.private,operator=True);home=self.journal.parent/'operator-home';(home/'.eventstore').mkdir(mode=0o700,parents=True,exist_ok=True)
   profile=home/'.eventstore/mcpcli.json';require(not profile.exists(),'operator-profile-already-exists')
   profile.write_text(json.dumps({'version':1,'profiles':{'operator':{'url':'http://127.0.0.1:'+str(self.port),'token':value,'tenant':self.packet['tenant'],'actor':self.packet['operatorSubject'],'allowTenantOverride':False}}}));profile.chmod(0o600)
   receipt.update(profilePath=str(profile),tokenExpiresUnix=expiry,tokenExpiresAt=datetime.fromtimestamp(expiry,timezone.utc).isoformat(),globalAdministrator=False,role='TenantContributor')
  elif phase=='cleanup':
   self.scoped('delete','namespace',self.namespace,'--wait=true','--timeout=180s',timeout=190)
   require(not self.kube('get','namespace',self.namespace,'--ignore-not-found','-o','json').strip(),'cleanup-incomplete')
   receipt['namespaceAbsent']=True
  receipt.update(status='complete',endedAt=datetime.now(timezone.utc).isoformat());save(path,receipt)
  return receipt


def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--packet',required=True,type=Path);parser.add_argument('--review-sha256',required=True);parser.add_argument('--phase',choices=('verify',*PHASES),default='verify');parser.add_argument('--execute-reviewed',action='store_true');parser.add_argument('--forward',action='store_true',help='After completed handoff, verify owned Pod and hold the private Gateway forward.');args=parser.parse_args()
 try:
  packet,private=verify(args.packet,args.review_sha256)
  if args.forward:
   require(args.phase=='verify' and not args.execute_reviewed,'forward-cannot-execute-phase')
   runner=Runner(args.packet,packet,private);runner.prior('handoff')
   with runner.forward() as child:
    print('Verified owned Gateway forward active on 127.0.0.1:'+str(runner.port)+'. Stop with Ctrl-C.',flush=True);child.wait()
   return 0
  if args.phase=='verify':print('Reviewed packet/local inputs verified; no resources applied.');return 0
  require(args.execute_reviewed,'deployment-review-required')
  result=Runner(args.packet,packet,private).execute(args.phase);print(json.dumps({'phase':result['phase'],'status':result['status']}));return 0
 except KeyboardInterrupt:return 130
 except Exception as error:
  print('Phase stopped: '+(str(error) if isinstance(error,ValueError) else type(error).__name__)+'. Preserve the journal; do not retry uncertain writes.',file=__import__('sys').stderr);return 1


if __name__=='__main__':raise SystemExit(main())
