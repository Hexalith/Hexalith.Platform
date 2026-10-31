#!/usr/bin/env python3
"""Prepare the approved three-service fixture packet. Never applies resources."""
from datetime import datetime, timedelta, timezone
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import uuid
import yaml

HERE=Path(__file__).resolve().parent
PLATFORM=HERE.parents[2]
MCPCLI=Path('/home/administrator/projects/hexalith/mcpcli')
APPS=('eventstore','tenants','parties')
IMAGE_ROOTS={
 'eventstore':'registry.hexalith.com/eventstore@sha256:c14074f08cf990072912dd6284fc253a900f6a020a0cd11008195aa357528eed',
 'tenants':'registry.hexalith.com/tenants@sha256:2bc415a61466693bfa7e6c0df1c9aab319b0ef2e82c620897aa9faa1b58f75a5',
 'parties':'registry.hexalith.com/parties@sha256:85afc9e954146883da56edce78e2ddaf401a47b35241bdceed87dfc20217aceb'}
REDIS='redis@sha256:ff02b58f971e7d7d156a1267e283fcbbeee91773b6aa36c49dac28ecfe28eadf'
FIXTURES=('tests/Hexalith.McpCli.AppHost/Program.cs','tests/Hexalith.McpCli.AppHost/appsettings.json',
 'tests/Hexalith.McpCli.AppHost/TopologyConfiguration.cs','tests/Hexalith.McpCli.AppHost/DaprComponents/accesscontrol.yaml',
 'tests/Hexalith.McpCli.AppHost/DaprComponents/statestore.yaml','tests/Hexalith.McpCli.AppHost/DaprComponents/pubsub.yaml',
 'tests/Hexalith.McpCli.Integration.Tests/AspireTopologyFixture.cs','tests/Hexalith.McpCli.Integration.Tests/AdministratorAuthorityBootstrap.cs',
 'tests/Hexalith.McpCli.Integration.Tests/ProjectionWriterBootstrap.cs','tools/live-topology/run-isolated.py')


def require(condition,code):
 if not condition:raise ValueError(code)


def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def dump(path,value):Path(path).write_text(json.dumps(value,indent=2)+'\n')


def private_write(path,raw):
 fd=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
 with os.fdopen(fd,'w') as file:file.write(raw)


def registry_auth():
 """Resolve only the configured registry.hexalith.com identity, never log its value."""
 config=json.loads((Path.home()/'.docker/config.json').read_text())
 hosts=('registry.hexalith.com','https://registry.hexalith.com','https://registry.hexalith.com/v1/')
 for host in hosts:
  entry=config.get('auths',{}).get(host,{})
  if entry.get('auth'):return {'auths':{'registry.hexalith.com':{'auth':entry['auth']}}}
 helper=config.get('credHelpers',{}).get('registry.hexalith.com') or config.get('credsStore')
 require(helper and re.fullmatch(r'[a-zA-Z0-9_.-]+',helper),'configured-registry-auth-unavailable')
 result=subprocess.run(['docker-credential-'+helper,'get'],input=b'registry.hexalith.com\n',capture_output=True,timeout=15)
 require(result.returncode==0,'registry-credential-helper-failed')
 value=json.loads(result.stdout)
 require(value.get('Username') and value.get('Secret'),'registry-credential-incomplete')
 auth=base64.b64encode((value['Username']+':'+value['Secret']).encode()).decode()
 return {'auths':{'registry.hexalith.com':{'auth':auth}}}


def validate_fixture(settings):
 routes=settings['Gateway']['Routes']
 require(set(routes)=={'system|tenants|v1','system|global-administrators|v1','*|party|v1'},'fixture-route-set-changed')
 require([(r['TenantId'],r['Domain'],r['AppId'],r['MethodName'],r['Version']) for r in routes.values()]==
 [('system','tenants','tenants','process','v1'),('system','global-administrators','tenants','process','v1'),('*','party','parties','process','v1')],'fixture-route-bindings-changed')
 require(settings['Gateway']['TopicOverrides']=={'global-administrators':'tenants.events'} and
 settings['Tenants']=={'BootstrapGlobalAdminEnabled':True,'BootstrapGlobalAdminUserId':'mcpcli-live-admin'},'fixture-bootstrap-or-topic-changed')


def validate_manifests(documents,namespace):
 require({d['metadata']['name'] for d in documents if d['kind']=='Pod'}==set(APPS)|{'redis'},'exact-three-application-pods-required')
 for doc in documents:
  meta=doc['metadata'];kind=doc['kind']
  require(kind=='Namespace' and meta['name']==namespace or meta.get('namespace')==namespace,'external-resource-refused')
  require(meta.get('labels',{}).get('hexalith.com/operator-task')==namespace,'unowned-resource-refused')
  require(kind not in ('Ingress','HTTPRoute','ClusterRole','ClusterRoleBinding','Secret'),'public-or-secret-resource-refused')
  if kind=='Service':require(doc['spec'].get('type','ClusterIP')=='ClusterIP','public-service-refused')
  if kind=='Pod':
   spec=doc['spec'];require(spec['restartPolicy']=='Never' and not spec.get('hostNetwork') and not spec.get('hostPID'),'automatic-restart-or-host-namespace-refused')
   for c in spec['containers']:
    require('@sha256:' in c['image'] and c['resources'].get('limits',{}).get('memory') and c['resources']['limits'].get('cpu'),'immutable-bounded-container-required')
    require(c['securityContext'].get('allowPrivilegeEscalation') is False and c['securityContext'].get('readOnlyRootFilesystem') is True,'container-security-required')
    require(not any('value' in e and any(k in e['name'].lower() for k in ('signingkey','token','password')) for e in c.get('env',[])),'literal-credential-refused')
   if meta['name'] in APPS:
    require(spec['containers'][0]['readinessProbe']['httpGet']['path']=='/ready','owner-readiness-required')


def prepare(private_root,output,now=None,auth=None):
 private_root=Path(private_root);output=Path(output)
 require(private_root.is_absolute() and not private_root.exists() and not private_root.is_relative_to(PLATFORM) and not private_root.is_relative_to(MCPCLI),'fresh-external-private-directory-required')
 require(not output.exists(),'fresh-packet-directory-required')
 settings=json.loads((MCPCLI/FIXTURES[1]).read_text());validate_fixture(settings)
 images=json.loads((HERE/'evidence/images.json').read_text())
 require({i['name'] for i in images}==set(APPS) and all(i['reference']==IMAGE_ROOTS[i['name']] and i['os']=='linux' and i['architecture']=='amd64' for i in images),'inspected-images-required')
 require(all(i['markers']['AppChannelToken'] for i in images) and images[0]['markers']['delivery-writer-protocol/activate'] and next(i for i in images if i['name']=='tenants')['markers']['BootstrapGlobalAdminEnabled'],'required-image-capability-missing')
 now=now or datetime.now(timezone.utc);run=now.strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:8];namespace='mcpcli-417-'+run
 tenant=namespace;operator='mcpcli-417-operator-'+run
 private_root.mkdir(mode=0o700,parents=True)
 values={};secret_files={}
 def secret(name,keys):
  mapping={}
  for key,value in keys.items():
   path=private_root/(name+'-'+key);private_write(path,value);mapping[key]=str(path);values[str(path)]=digest(path)
  secret_files[name]=mapping
 signing=secrets.token_urlsafe(48);password=secrets.token_urlsafe(32)
 secret('mcpcli-jwt',{'signing-key':signing})
 for app in APPS:
  secret('mcpcli-'+app+'-app',{'token':secrets.token_urlsafe(48)})
  secret('mcpcli-'+app+'-api',{'token':secrets.token_urlsafe(48)})
 secret('mcpcli-redis',{'password':password,'users.acl':'user default on #'+hashlib.sha256(password.encode()).hexdigest()+' ~* &* +@all\n'})
 secret('mcpcli-pull', {'.dockerconfigjson':json.dumps(auth if auth is not None else registry_auth())})
 private_manifest=private_root/'private-inputs.json';private_write(private_manifest,json.dumps({'files':values,'secrets':secret_files},indent=2)+'\n')
 documents=[]
 def resource(kind,name,**body):
  value={'apiVersion':'v1','kind':kind,'metadata':{'name':name,'namespace':namespace,'labels':{'hexalith.com/operator-task':namespace}},**body}
  documents.append(value);return value
 ns=resource('Namespace',namespace);ns['metadata'].pop('namespace');ns['metadata']['labels'].update({'pod-security.kubernetes.io/enforce':'baseline','pod-security.kubernetes.io/warn':'restricted','pod-security.kubernetes.io/audit':'restricted'})
 resource('ServiceAccount','mcpcli-v1')
 resource('ResourceQuota','task-bounds',spec={'hard':{'pods':'4','persistentvolumeclaims':'1','requests.storage':'1Gi','limits.cpu':'6','limits.memory':'3Gi'}})
 role=resource('Role','mcpcli-task-secrets',rules=[{'apiGroups':[''],'resources':['secrets'],'resourceNames':[k for k in secret_files if k!='mcpcli-pull'],'verbs':['get']}]);role['apiVersion']='rbac.authorization.k8s.io/v1'
 binding=resource('RoleBinding','mcpcli-task-secrets',roleRef={'apiGroup':'rbac.authorization.k8s.io','kind':'Role','name':'mcpcli-task-secrets'},subjects=[{'kind':'ServiceAccount','name':'mcpcli-v1','namespace':namespace}]);binding['apiVersion']='rbac.authorization.k8s.io/v1'
 network=resource('NetworkPolicy','task-isolation',spec={'podSelector':{},'policyTypes':['Ingress','Egress'],
  'ingress':[{'from':[{'podSelector':{}}]},{'from':[{'namespaceSelector':{'matchLabels':{'kubernetes.io/metadata.name':'dapr-system'}}}]}],
  'egress':[{'to':[{'podSelector':{}}]},{'to':[{'namespaceSelector':{'matchLabels':{'kubernetes.io/metadata.name':'dapr-system'}}}]},
   {'to':[{'namespaceSelector':{'matchLabels':{'kubernetes.io/metadata.name':'kube-system'}}}],'ports':[{'protocol':'UDP','port':53},{'protocol':'TCP','port':53}]},
   {'to':[{'ipBlock':{'cidr':'10.233.0.1/32'}},{'ipBlock':{'cidr':'192.168.1.30/32'}}],'ports':[{'protocol':'TCP','port':443},{'protocol':'TCP','port':6443}]}]});network['apiVersion']='networking.k8s.io/v1'
 resource('PersistentVolumeClaim','task-data',spec={'accessModes':['ReadWriteOnce'],'storageClassName':'openebs-hostpath','resources':{'requests':{'storage':'1Gi'}}})
 resource('ConfigMap','redis-config',data={'redis.conf':'bind 0.0.0.0\nport 6379\ndir /data\ndbfilename dump.rdb\naclfile /run/private/users.acl\nappendonly no\nsave ""\nmaxmemory 192mb\nmaxmemory-policy noeviction\n'})
 security={'allowPrivilegeEscalation':False,'readOnlyRootFilesystem':True,'capabilities':{'drop':['ALL']}}
 redis=resource('Pod','redis',spec={'restartPolicy':'Never','serviceAccountName':'mcpcli-v1','nodeSelector':{'kubernetes.io/arch':'amd64'},'securityContext':{'runAsNonRoot':True,'runAsUser':999,'runAsGroup':999,'fsGroup':999,'seccompProfile':{'type':'RuntimeDefault'}},
  'containers':[{'name':'redis','image':REDIS,'command':['redis-server','/etc/redis/redis.conf'],'securityContext':security,
   'resources':{'requests':{'cpu':'100m','memory':'128Mi'},'limits':{'cpu':'1','memory':'256Mi'}},
   'env':[{'name':'REDISCLI_AUTH','valueFrom':{'secretKeyRef':{'name':'mcpcli-redis','key':'password'}}}],
   'readinessProbe':{'exec':{'command':['redis-cli','ping']}},'volumeMounts':[{'name':'data','mountPath':'/data'},{'name':'config','mountPath':'/etc/redis','readOnly':True},{'name':'private','mountPath':'/run/private','readOnly':True}]}],
  'volumes':[{'name':'data','persistentVolumeClaim':{'claimName':'task-data'}},{'name':'config','configMap':{'name':'redis-config'}},{'name':'private','secret':{'secretName':'mcpcli-redis','defaultMode':288}}]})
 redis['metadata']['labels']['app']='redis'
 resource('Service','redis',spec={'selector':{'app':'redis'},'ports':[{'name':'redis','port':6379,'targetPort':6379}]})
 access=json.loads((MCPCLI/FIXTURES[3]).read_text());access['metadata'].update(name='mcpcli-v1-access',namespace=namespace,labels={'hexalith.com/operator-task':namespace})
 access['spec']['accessControl']['trustDomain']='cluster.local'
 for p in access['spec']['accessControl']['policies']:p.update(namespace=namespace,trustDomain='cluster.local')
 access['spec']['mtls']={'enabled':True,'sentryAddress':'dapr-sentry.dapr-system.svc.cluster.local:443','controlPlaneTrustDomain':'cluster.local'}
 documents.append(access)
 components=[]
 for fixture in FIXTURES[4:6]:
  component=json.loads((MCPCLI/fixture).read_text());component['metadata'].update(namespace=namespace,labels={'hexalith.com/operator-task':namespace})
  for m in component['spec']['metadata']:
   if m['name']=='redisHost':m['value']='redis:6379'
   if m['name']=='redisPassword':m.pop('value',None);m['secretKeyRef']={'name':'mcpcli-redis','key':'password'}
  documents.append(component);components.append(component)
 gateway_config={'EventStore':{'DomainServices':{'Registrations':settings['Gateway']['Routes']},'Publisher':{'PubSubName':'pubsub','TopicOverrides':settings['Gateway']['TopicOverrides']}},
  'Authentication':{'JwtBearer':{'Authority':'','Issuer':'mcpcli-live-tests','Audience':'hexalith-eventstore','ValidAudiences':['hexalith-eventstore'],'AllowedAlgorithms':['HS256'],'RequireHttpsMetadata':False}}}
 resource('ConfigMap','gateway-settings',data={'appsettings.json':json.dumps(gateway_config,indent=2)})
 resource('ConfigMap','component-files',data={'accesscontrol.json':json.dumps(access),'statestore.json':json.dumps(components[0]),'pubsub.json':json.dumps(components[1])})
 for app in APPS:
  env={'ASPNETCORE_ENVIRONMENT':'Development','DOTNET_ENVIRONMENT':'Development','ASPNETCORE_URLS':'http://0.0.0.0:8080',
   'Authentication__JwtBearer__Authority':'','Authentication__JwtBearer__Issuer':'mcpcli-live-tests','Authentication__JwtBearer__Audience':'hexalith-eventstore',
   'Authentication__JwtBearer__ValidAudiences__0':'hexalith-eventstore','Authentication__JwtBearer__AllowedAlgorithms__0':'HS256','Authentication__JwtBearer__RequireHttpsMetadata':'false',
   'EventStore__DomainService__AppId':app,'EventStore__DomainService__ServiceVersion':'v1'}
  if app=='eventstore':env.update(ASPNETCORE_CONTENTROOT='/etc/gateway',EventStore__DaprConfigPath='/etc/components/accesscontrol.json',EventStore__StateStoreComponentPath='/etc/components/statestore.json',EventStore__PubSubComponentPath='/etc/components/pubsub.json')
  if app=='tenants':env.update(Tenants__BootstrapGlobalAdminEnabled='true',Tenants__BootstrapGlobalAdminUserId='mcpcli-live-admin',Tenants__DaprConfigPath='/etc/components/accesscontrol.json')
  if app=='parties':env.update(Tenants__Enabled='false',Parties__MemoriesSearch__Enabled='false')
  environment=[{'name':k,'value':v} for k,v in env.items()]
  for name,secret_name,key in [('Authentication__JwtBearer__SigningKey','mcpcli-jwt','signing-key'),('APP_API_TOKEN','mcpcli-'+app+'-app','token'),('DAPR_API_TOKEN','mcpcli-'+app+'-api','token')]:
   environment.append({'name':name,'valueFrom':{'secretKeyRef':{'name':secret_name,'key':key}}})
  pod=resource('Pod',app,spec={'restartPolicy':'Never','serviceAccountName':'mcpcli-v1','nodeSelector':{'kubernetes.io/arch':'amd64'},'imagePullSecrets':[{'name':'mcpcli-pull'}],
   'securityContext':{'runAsNonRoot':True,'runAsUser':1654,'runAsGroup':1654,'seccompProfile':{'type':'RuntimeDefault'}},
   'containers':[{'name':app,'image':IMAGE_ROOTS[app],'ports':[{'name':'http','containerPort':8080}],'securityContext':security,
    'resources':{'requests':{'cpu':'100m','memory':'128Mi'},'limits':{'cpu':'1','memory':'512Mi'}},'env':environment,
    'readinessProbe':{'httpGet':{'path':'/ready','port':'http'},'periodSeconds':5,'timeoutSeconds':3},
    'volumeMounts':[{'name':'tmp','mountPath':'/tmp'},{'name':'components','mountPath':'/etc/components','readOnly':True}]+([{'name':'gateway','mountPath':'/etc/gateway','readOnly':True}] if app=='eventstore' else [])}],
   'volumes':[{'name':'tmp','emptyDir':{'sizeLimit':'128Mi'}},{'name':'components','configMap':{'name':'component-files'}}]+([{'name':'gateway','configMap':{'name':'gateway-settings'}}] if app=='eventstore' else [])})
  pod['metadata']['labels'].update(app=app,**{'dapr.io/app-id':app})
  pod['metadata']['annotations']={'dapr.io/enabled':'true','dapr.io/app-id':app,'dapr.io/app-port':'8080','dapr.io/config':'mcpcli-v1-access',
   'dapr.io/app-token-secret':'mcpcli-'+app+'-app','dapr.io/api-token-secret':'mcpcli-'+app+'-api','dapr.io/sidecar-listen-addresses':'127.0.0.1',
   'dapr.io/sidecar-cpu-request':'50m','dapr.io/sidecar-cpu-limit':'500m','dapr.io/sidecar-memory-request':'64Mi','dapr.io/sidecar-memory-limit':'256Mi'}
  resource('Service',app,spec={'selector':{'app':app},'publishNotReadyAddresses':True,'ports':[{'name':'http','port':80,'targetPort':8080}]})
 # Explicit task-owned Dapr bootstrap service: preserve /ready while the writer marker is absent.
 for app in APPS:
  resource('Service',app+'-dapr',spec={'selector':{'dapr.io/app-id':app},'publishNotReadyAddresses':True,
  'ports':[{'name':'dapr-http','port':80,'targetPort':3500},{'name':'dapr-grpc','port':50001,'targetPort':50001},{'name':'dapr-internal','port':50002,'targetPort':50002},{'name':'dapr-metrics','port':9090,'targetPort':9090}]})
 validate_manifests(documents,namespace)
 output.mkdir(parents=True)
 groups={'namespace':[ns],'infrastructure':[d for d in documents if d['kind'] not in ('Pod','Namespace')],'redis':[redis],
         **{app:[d for d in documents if d['kind']=='Pod' and d['metadata']['name']==app] for app in APPS}}
 for name,docs in groups.items():(output/(name+'.yaml')).write_text(yaml.safe_dump_all(docs,sort_keys=False))
 packet={'schema':'mcpcli-v1-operator-review/v1','status':'prepared-for-review','applyAuthorized':False,
  'target':{'context':'jpiquot@local','api':'https://192.168.1.30:6443','namespace':namespace,'release':namespace},'tenant':tenant,'operatorSubject':operator,
  'createdAt':now.isoformat(),'expiresAt':(now+timedelta(hours=24)).isoformat(),'gatewayPort':18170,'operatorTokenTtlSeconds':7200,
  'images':IMAGE_ROOTS,'redisImage':REDIS,'gatewaySourceCommit':'717a06e16c3afd81b904eaf4de646b74b13262d2',
  'administratorSubject':'mcpcli-live-admin','seedOperations':[
   {'endpoint':'/api/v1/commands','tenant':'system','domain':'tenants','aggregateId':tenant,'commandType':'create-tenant','payload':{'TenantId':tenant,'Name':'McpCli operator setup','Description':'Task-owned synthetic operator tenant'}},
   {'endpoint':'/api/v1/commands','tenant':'system','domain':'tenants','aggregateId':tenant,'commandType':'add-user-to-tenant','payload':{'TenantId':tenant,'UserId':operator,'Role':'TenantContributor'}}],
  'fixtureHashes':{f:digest(MCPCLI/f) for f in FIXTURES},'runnerSha256':digest(HERE/'phases.py'),'prepareSha256':digest(HERE/'prepare.py'),
  'evidenceHashes':{p.name:digest(p) for p in (HERE/'evidence').glob('*.json') if p.name in ('images.json','tenants-projection-contract-diff.json','parties-projection-contract-diff.json','wire-bindings.json','target-observation.json')},'imageInspectionSha256':digest(HERE/'evidence/images.json'),
  'privateManifest':str(private_manifest),'privateManifestSha256':digest(private_manifest),'secretReferences':{name:list(keys) for name,keys in secret_files.items()},
  'files':{p.name:digest(p) for p in output.iterdir() if p.is_file()},'inventory':[{'kind':d['kind'],'name':d['metadata']['name'],'namespace':d['metadata'].get('namespace')} for d in documents],
  'bounds':{'applicationPods':3,'redisPods':1,'applicationCpuLimit':'3','applicationMemoryLimit':'1536Mi','sidecarCpuLimit':'1500m','sidecarMemoryLimit':'768Mi','redisCpuLimit':'1','redisMemoryLimit':'256Mi','persistentStorage':'1Gi'},
  'phases':['infrastructure','backup-empty-store','gateway','tenants','observe-administrator','activate-writer','parties','seed-tenant','handoff','cleanup'],
  'limits':['No deployment, backup, UID or persisted-state result has been invented.','No application was started during preparation.','This packet qualifies neither the full Platform closure nor actual client/timing acceptance.'],
  'adaptations':['Kubernetes task namespace and cluster.local mTLS replace self-hosted default/public scope.','Private password-protected Redis replaces fixture localhost Redis; the same provider serves state/pubsub.','Pods restart Never to prevent uncertain bootstrap resubmission.','Existing injector has non-root/read-only enabled but drop-all-capabilities=false; namespace enforces baseline and audits/warns restricted, while app containers drop ALL. No shared injector setting is changed.','Bootstrap service publishes private unready endpoints without weakening /ready; handoff requires /ready success.','Explicit Parties Tenants.Enabled=false and MemoriesSearch.Enabled=false prevent legacy Development side effects outside the task.']}
 dump(output/'review.json',packet)
 return packet


def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--private-root',required=True,type=Path);parser.add_argument('--output',required=True,type=Path);args=parser.parse_args()
 try:
  result=prepare(args.private_root,args.output);print(json.dumps({'status':result['status'],'namespace':result['target']['namespace'],'reviewSha256':digest(args.output/'review.json')}));return 0
 except Exception as error:
  print('Preparation refused: '+(str(error) if isinstance(error,ValueError) else type(error).__name__),file=__import__('sys').stderr);return 1


if __name__=='__main__':raise SystemExit(main())
