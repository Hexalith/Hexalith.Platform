"""Start only an isolated diagnostic AppHost and dedicated ephemeral Redis; retain no credential values."""
from pathlib import Path
import os,subprocess,json,hashlib,uuid,base64,re
base=Path('/tmp/hexalith-story33-release-0_wjbwkj');owner=base/'Hexalith.Tenants';evidence=Path(__file__).resolve().parent;fixture=base/'legacy-runtime-fixtures';fixture.mkdir(exist_ok=True)
rows=[]
for name in ['DaprSelfHostedMtls.cs','DaprSelfHostedMtlsSubscriber.cs']:
 data=subprocess.check_output(['git','show','667e5a02a523cf8764ca54d6aa1b4b75db5dca64:'+name],cwd=base/'Hexalith.Platform');(fixture/name).write_bytes(data);rows.append({'owner':'Hexalith.Platform','commit':'667e5a02a523cf8764ca54d6aa1b4b75db5dca64','path':name,'sha256':hashlib.sha256(data).hexdigest()})
(evidence/'tenants-diagnostic-platform-fixtures.json').write_text(json.dumps(rows,indent=2)+'\n')
image=subprocess.check_output(['docker','inspect','dapr_redis','--format','{{.Image}}'],text=True).strip();name='hexalith-story33-diag-'+uuid.uuid4().hex[:12]
container=subprocess.check_output(['docker','run','--rm','-d','--name',name,'--label','hexalith.story33.diagnostic='+name,'-p','127.0.0.1::6379',image],text=True).strip();port=subprocess.check_output(['docker','port',container,'6379/tcp'],text=True).strip().rsplit(':',1)[1]
for component in ['statestore.yaml','pubsub.yaml']:
 source=owner/'src/Hexalith.Tenants.AppHost/DaprComponents'/component;(fixture/component).write_text(source.read_text().replace('localhost:6379','127.0.0.1:'+port))
attempt='tenants-legacy-'+uuid.uuid4().hex[:10]
record={'attempt':attempt,'apphost':str(owner/'src/Hexalith.Tenants.AppHost/Hexalith.Tenants.AppHost.csproj'),'source':'717a06e16c3afd81b904eaf4de646b74b13262d2','container':container,'containerName':name,'redisImage':image,'redisPort':int(port),'purpose':'diagnosis only; never package delivery qualification'}
(evidence/'tenants-legacy-diagnostic-session.json').write_text(json.dumps(record,indent=2)+'\n')
(evidence/(attempt+'-session.json')).write_text(json.dumps(record,indent=2)+'\n')
key=base64.b64encode(os.urandom(32)).decode();private=fixture/'diagnostic-jwt-key';fd=os.open(private,os.O_CREAT|os.O_EXCL|os.O_WRONLY,0o600);os.write(fd,key.encode());os.close(fd)
env=dict(os.environ,HexalithEventStoreRoot=str(base/'Hexalith.EventStore-legacy-topology'),HexalithBuildsRoot=str(base/'Hexalith.Builds-published-4.39.0'),HexalithPlatformRoot=str(fixture),UseHexalithProjectReferences='false',UseNuGetDeps='true',NUGET_PACKAGES=str(base/'ordinary-package-cache'),EnableKeycloak='false',EnableMemories='false',Tenants__BootstrapGlobalAdminEnabled='false',EventStore__Projections__WorkersEnabled='false',Authentication__JwtBearer__SigningKey=key,Dapr__StateStoreComponentPath=str(fixture/'statestore.yaml'),Dapr__PubSubComponentPath=str(fixture/'pubsub.yaml'))
cmd=['aspire','start','--non-interactive','--isolated','--apphost',record['apphost'],'--launch-profile','https','--format','Json'];print(json.dumps({'command':cmd,'session':record}),flush=True)
r=subprocess.run(cmd,cwd=owner,env=env,capture_output=True,text=True,timeout=600)
def safe(s):
 s=s.replace(key,'[omitted]');s=re.sub(r'([?&]t=)[^\s"\x27]+',r'\1[omitted]',s);return s
(evidence/(attempt+'.stdout')).write_text(safe(r.stdout));(evidence/(attempt+'.stderr')).write_text(safe(r.stderr));print(json.dumps({'exitCode':r.returncode,'stdout':safe(r.stdout)[-2000:],'stderr':safe(r.stderr)[-1000:]}),flush=True)
if r.returncode:
 subprocess.run(['aspire','stop','--non-interactive','--apphost',record['apphost']],cwd=owner,capture_output=True,text=True,timeout=90);subprocess.run(['docker','stop',container],capture_output=True);private.unlink(missing_ok=True)
raise SystemExit(r.returncode)
