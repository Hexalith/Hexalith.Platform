#!/usr/bin/env python3
"""Owned local Dapr 1.18.7 + Redis source framing proof; no cluster or shared app lifecycle."""
import argparse, hashlib, json, os, re, socket, subprocess, tempfile, time, urllib.request, uuid
from pathlib import Path

def port():
    with socket.socket() as value:
        value.bind(('127.0.0.1', 0)); return value.getsockname()[1]

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--assembly',required=True); parser.add_argument('--output',required=True); args=parser.parse_args()
    output=Path(args.output); assert output.is_absolute() and not output.exists()
    nonce=uuid.uuid4().hex; name='erc-loop6-redis-'+nonce; daemon=['docker','--host','unix:///var/run/docker.sock']; sidecar=None; container=None
    result={'name':name,'scope':'Owned Dapr source framing only','liveExecuted':False,'productionQualified':False,'nonce':nonce,'attempts':[],'cleanup':False,'testAssemblySha256':hashlib.sha256(Path(args.assembly).read_bytes()).hexdigest(),'harnessSourceSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    prepared=Path(str(output)+'.prepared.json')
    descriptor=os.open(prepared,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
    with os.fdopen(descriptor,'w') as stream:
        json.dump({'nonce':nonce,'containerName':name,'ownershipLabel':{'hexalith.ext-protection-1.dapr-source':nonce}},stream,sort_keys=True); stream.flush(); os.fsync(stream.fileno())
    def docker(*parts): return subprocess.run(daemon+list(parts),capture_output=True,check=True,timeout=30).stdout.decode().strip()
    try:
        assembly=Path(args.assembly); dependencies=assembly.with_suffix('.deps.json'); libraries=json.loads(dependencies.read_bytes())['libraries']
        result['clientLibraries']=[name for name in sorted(libraries) if name.startswith(('Dapr.Client/','Grpc.Net.Client/'))]
        result['dependenciesSha256']=hashlib.sha256(dependencies.read_bytes()).hexdigest()
        manifest=assembly.parent/'runtime-manifest.json'
        if manifest.is_file(): result['runtimeManifestSha256']=hashlib.sha256(manifest.read_bytes()).hexdigest()
        result['daprCliVersion']=subprocess.run(['dapr','--version'],capture_output=True,check=True).stdout.decode().strip()
        daprd=Path.home()/'.dapr/bin/daprd'; result['daprVersion']=subprocess.run([str(daprd),'--version'],capture_output=True,check=True).stdout.decode().strip()
        assert '1.18.7' in result['daprVersion']; result['daprdSha256']=hashlib.sha256(daprd.read_bytes()).hexdigest()
        image=json.loads(docker('image','inspect','redis:7.4-alpine'))[0]; result['redisImageId']=image['Id']
        container=docker('create','--name',name,'--label','hexalith.ext-protection-1.dapr-source='+nonce,'--read-only','--log-driver','none','--tmpfs','/data:rw,noexec,nosuid,size=256m','--memory','512m','--publish','127.0.0.1::6379','redis:7.4-alpine','redis-server','--save','','--appendonly','no')
        result['containerId']=container
        docker('start',container); mapping=docker('port',container,'6379/tcp'); assert re.fullmatch(r'127\.0\.0\.1:[0-9]+',mapping)
        with tempfile.TemporaryDirectory(prefix='erc-loop6-dapr-') as temporary:
            root=Path(temporary); components=root/'components'; components.mkdir()
            component={'apiVersion':'dapr.io/v1alpha1','kind':'Component','metadata':{'name':'erc-source-test'},'spec':{'type':'state.redis','version':'v1','metadata':[{'name':'redisHost','value':mapping},{'name':'enableTLS','value':'false'}]}}
            (components/'source.yaml').write_text(json.dumps(component)); result['component']=component
            for phase,limit in [('large','4Mi'),('legacy-seed','8Mi'),('legacy-default-refusal','4Mi'),('legacy-read','8Mi')]:
                http,grpc,internal=port(),port(),port()
                argv=[str(daprd),'--app-id','erc-loop6-'+nonce,'--dapr-listen-addresses','127.0.0.1','--dapr-http-port',str(http),'--dapr-grpc-port',str(grpc),'--dapr-internal-grpc-port',str(internal),'--resources-path',str(components),'--enable-metrics=false','--log-level','error','--max-body-size',limit]
                with open(root/(phase+'.sidecar.log'),'wb') as log:
                    sidecar=subprocess.Popen(argv,stdout=log,stderr=log)
                    ready=False
                    for _ in range(100):
                        if sidecar.poll() is not None: break
                        try:
                            with urllib.request.urlopen('http://127.0.0.1:'+str(http)+'/v1.0/healthz/outbound',timeout=.5) as response: ready=response.status==204
                        except Exception: pass
                        if ready: break
                        time.sleep(.1)
                    assert ready,'Owned sidecar did not become ready'
                    with urllib.request.urlopen('http://127.0.0.1:'+str(http)+'/v1.0/metadata',timeout=2) as response:
                        metadata=json.load(response)
                    assert any(item.get('name')=='erc-source-test' and item.get('type')=='state.redis' for item in metadata.get('components',[])), 'Owned state component did not load'
                    environment=dict(os.environ,ERC_LOOP6_DAPR_GRPC='http://127.0.0.1:'+str(grpc),ERC_LOOP6_DAPR_PHASE=phase)
                    run=subprocess.run(['dotnet',args.assembly,'-class','Hexalith.Platform.Protection.Tests.ErasureRootLoop6DaprIntegrationTests','-noColor'],env=environment,capture_output=True,timeout=300)
                    text=run.stdout.decode(errors='replace')
                    summary=re.search(r'Total:\s*(\d+),\s*Errors:\s*(\d+),\s*Failed:\s*(\d+),\s*Skipped:\s*(\d+)',text)
                    measurements=[]
                    for line in text.splitlines():
                        try:
                            item=json.loads(line)
                            if isinstance(item,dict) and (item.get('realDapr') is True or item.get('daprDiagnostic') is True): measurements.append(item)
                        except ValueError: pass
                    result['attempts'].append({'phase':phase,'sidecarArguments':argv,'exitCode':run.returncode,'counts':list(map(int,summary.groups())) if summary else None,'measurements':measurements,'safeFailureTypes':sorted(set(re.findall(r'\b[A-Za-z][A-Za-z0-9_.]*Exception\b',text)))[:10], 'safeFailureLines':re.findall(r'ErasureRootLoop6DaprIntegrationTests\.cs\([0-9]+,0\)',text)[:10]})
                    sidecar.terminate(); sidecar.wait(timeout=20); sidecar=None
                    assert run.returncode==0 and summary and list(map(int,summary.groups()))==[1,0,0,0] and measurements,'Actual source case failed'
        result['outcome']='Passed'
    except Exception as error:
        result['outcome']='BlockedUnknown'; result['failureType']=type(error).__name__
    finally:
        if sidecar is not None:
            sidecar.terminate()
            try: sidecar.wait(timeout=20)
            except subprocess.TimeoutExpired: sidecar.kill(); sidecar.wait(timeout=10)
        if container:
            try:
                inspected=json.loads(docker('inspect',container))[0]; assert inspected['Config']['Labels'].get('hexalith.ext-protection-1.dapr-source')==nonce
                docker('rm','--force',container)
                absent=subprocess.run(daemon+['inspect',container],capture_output=True,timeout=10)
                result['cleanup']=absent.returncode!=0 and b'no such' in absent.stderr.lower()
            except Exception: result['cleanup']=False
        with output.open('x') as stream: json.dump(result,stream,sort_keys=True,indent=2)
    print(json.dumps({'outcome':result['outcome'],'cleanup':result['cleanup'],'evidence':str(output)}))
    return 0 if result['outcome']=='Passed' and result['cleanup'] else 1
if __name__=='__main__': raise SystemExit(main())
