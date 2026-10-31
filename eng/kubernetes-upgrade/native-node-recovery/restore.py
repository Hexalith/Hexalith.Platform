"""Exact temporary Docker targets for authenticated node/etcd recovery artifacts.

The integration supplies current signed policy/version readbacks through authorize.
Only ciphertext files are read locally. Decrypted bytes cross an owned pipe into
bounded target tmpfs; the operator identity is never copied into a target. File
restoration does not demonstrate OS boot or SSH access. No CLI or implicit grant.
"""
import base64
import hashlib
import importlib.util
import inspect
import ipaddress
import json
import os
from pathlib import Path
import re
import selectors
import signal
import stat
import subprocess
import time
import traceback
import urllib.parse

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('recovery_restore_capture', HERE/'capture.py')
capture = importlib.util.module_from_spec(spec); spec.loader.exec_module(capture)
spec = importlib.util.spec_from_file_location('recovery_restore_transfer', HERE/'transfer.py')
transfer = importlib.util.module_from_spec(spec); spec.loader.exec_module(transfer)


def require(ok, reason):
    if not ok:raise ValueError(reason)


def canonical(value):
    return (json.dumps(value,sort_keys=True,indent=2)+'\n').encode()


def sha(raw):return hashlib.sha256(raw).hexdigest()


def private_artifact(path, size, digest):
    path=Path(path);parent=path.parent.lstat();info=path.lstat()
    require(stat.S_ISDIR(parent.st_mode) and parent.st_uid==os.getuid() and stat.S_IMODE(parent.st_mode)==0o700
            and stat.S_ISREG(info.st_mode) and info.st_uid==os.getuid() and info.st_nlink==1
            and stat.S_IMODE(info.st_mode)==0o600 and info.st_size==size
            and type(size) is int and 0<size<=transfer.MAX_CIPHERTEXT
            and isinstance(digest,str) and re.fullmatch('[a-f0-9]{64}',digest),
            'restore-private-artifact-custody-or-binding-refused')
    fd=os.open(path,os.O_RDONLY|os.O_NOFOLLOW);h=hashlib.sha256();count=0
    try:
        opened=os.fstat(fd);require((opened.st_dev,opened.st_ino)==(info.st_dev,info.st_ino),'restore-artifact-replaced')
        while True:
            raw=os.read(fd,1048576)
            if not raw:break
            count+=len(raw);require(count<=size,'restore-artifact-size-drift');h.update(raw)
        after=os.fstat(fd)
        require(count==size and h.hexdigest()==digest and all(getattr(info,k)==getattr(after,k)
                for k in ('st_dev','st_ino','st_size','st_mtime_ns','st_ctime_ns')),'restore-artifact-digest-or-content-drift')
    finally:os.close(fd)
    return {k:getattr(info,k) for k in ('st_dev','st_ino','st_size','st_mtime_ns','st_ctime_ns')}


def inspect_owned(current, cid, name, image, attempt, role, caps):
    require(current.get('Id')==cid and re.fullmatch('[a-f0-9]{64}',cid)
            and current.get('Name')=='/'+name and current.get('Image')==image
            and current.get('Config',{}).get('Labels',{}).get('hexalith.io/node-recovery-attempt')==attempt
            and current['Config']['Labels'].get('hexalith.io/node-recovery-role')==role,
            'restore-target-owned-identity-drift')
    host=current.get('HostConfig',{})
    actual_caps={v.removeprefix('CAP_') for v in host.get('CapAdd') or []}
    require(host.get('NetworkMode')=='none' and host.get('ReadonlyRootfs') is True
            and host.get('Privileged') is False and set(host.get('CapDrop') or [])=={'ALL'}
            and actual_caps==set(caps) and not host.get('Binds') and not host.get('VolumesFrom') and not host.get('Devices') and not host.get('Mounts')
            and not host.get('PortBindings') and not current.get('Mounts')
            and host.get('PidMode','')=='' and host.get('IpcMode')=='private'
            and host.get('Memory')==1610612736 and host.get('MemorySwap')==1610612736
            and set(host.get('Tmpfs',{}))=={'/input','/restore','/tmp'},'restore-target-network-mount-or-capability-drift')


def receiver_program(name,size,digest):
    require(name in ('node.tar.gz','snapshot.db') and type(size) is int and 0<size<=transfer.MAX_PLAINTEXT
            and isinstance(digest,str) and re.fullmatch('[a-f0-9]{64}',digest),'restore-receiver-binding-refused')
    return ("import hashlib,json,os,selectors,sys,time\n"
        "fd=os.open("+repr('/input/'+name+'.partial')+",os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)\n"
        "count=0;digest=hashlib.sha256();deadline=time.monotonic()+240\n"
        "try:\n"
        " with selectors.DefaultSelector() as selected:\n"
        "  os.set_blocking(0,False);selected.register(0,selectors.EVENT_READ)\n"
        "  while selected.get_map():\n"
        "   if time.monotonic()>=deadline:raise ValueError('restore-plaintext-receiver-deadline')\n"
        "   for key,_ in selected.select(.1):\n"
        "    raw=os.read(key.fd,1048576)\n"
        "    if not raw:selected.unregister(key.fd);continue\n"
        "    count+=len(raw)\n"
        "    if count>"+str(size)+":raise ValueError('restore-plaintext-receiver-size')\n"
        "    digest.update(raw);view=memoryview(raw)\n"
        "    while view:\n"
        "     n=os.write(fd,view)\n"
        "     if n<=0:raise ValueError('restore-plaintext-receiver-write')\n"
        "     view=view[n:]\n"
        " if count!="+str(size)+" or digest.hexdigest()!="+repr(digest)+":raise ValueError('restore-plaintext-receiver-digest')\n"
        " os.fsync(fd)\n"
        "finally:os.close(fd)\n"
        "os.link("+repr('/input/'+name+'.partial')+","+repr('/input/'+name)+",follow_symlinks=False)\n"
        "os.unlink("+repr('/input/'+name+'.partial')+")\n"
        "print(json.dumps({'bytes':count,'sha256':digest.hexdigest(),'plaintextOnlyInTargetTmpfs':True}))\n")


def pipe_plaintext(source_argv,target_argv,timeout=270):
    """Owned pipe only; target receiver enforces complete plaintext length/digest."""
    require(0<timeout<=300,'restore-pipe-timeout-refused')
    source=target=None;selected=selectors.DefaultSelector();outputs={'source-error':bytearray(),'target-error':bytearray(),'target-output':bytearray()}
    primary=None;deadline=time.monotonic()+timeout
    def diagnostics():
        return {'sourceReturnCode':source.poll() if source else None,'targetReturnCode':target.poll() if target else None,**{k+'Base64':base64.b64encode(v).decode() for k,v in outputs.items()}}
    try:
        source=subprocess.Popen(source_argv,stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.PIPE,
                                start_new_session=True,env={'LANG':'C.UTF-8'})
        target=subprocess.Popen(target_argv,stdin=source.stdout,stdout=subprocess.PIPE,stderr=subprocess.PIPE,
                                start_new_session=True,env={'LANG':'C.UTF-8'})
        source.stdout.close()
        for stream,label in ((source.stderr,'source-error'),(target.stderr,'target-error'),(target.stdout,'target-output')):
            os.set_blocking(stream.fileno(),False);selected.register(stream,selectors.EVENT_READ,label)
        while selected.get_map():
            require(time.monotonic()<deadline,'restore-pipe-deadline')
            for key,_ in selected.select(.1):
                data=os.read(key.fd,65536)
                if not data:selected.unregister(key.fileobj);continue
                outputs[key.data].extend(data);require(len(outputs[key.data])<=131072,'restore-pipe-diagnostic-bound')
        for child in (source,target):
            child.wait(timeout=max(.001,deadline-time.monotonic()))
            require(child.returncode==0,'restore-pipe-source-or-target-refused')
        return {'receiver':json.loads(outputs['target-output']),
                'sourceStderrSha256':sha(outputs['source-error']),'targetStderrSha256':sha(outputs['target-error'])}
    except BaseException as exc:
        primary=exc;exc.pipe_diagnostics=diagnostics();raise
    finally:
        failures=[];selected.close()
        for child in (target,source):
            if child is not None:
                try:transfer.reap(child)
                except BaseException as exc:failures.append(exc)
        if failures:
            group=BaseExceptionGroup('restore-pipe-primary-and-cleanup',([primary] if primary else [])+failures)
            group.pipe_diagnostics=diagnostics();raise group


def target_profile_program(mask, endpoints):
    require(type(endpoints) is list and 1<=len(endpoints)<=8,'restore-source-endpoint-refused')
    seen=set()
    for endpoint in endpoints:
        require(type(endpoint) is dict and set(endpoint)=={'host','port'} and type(endpoint['host']) is str,'restore-source-endpoint-refused')
        address=ipaddress.IPv4Address(endpoint['host'])
        require(not address.is_loopback and not address.is_unspecified and not address.is_multicast and type(endpoint['port']) is int and 0<endpoint['port']<65536 and (endpoint['host'],endpoint['port']) not in seen,'restore-source-endpoint-refused')
        seen.add((endpoint['host'],endpoint['port']))
    return ("import errno,json,os,socket\n"
        "with open('/proc/self/status') as source:fields=dict(v.split(':',1) for v in source.read(65536).splitlines() if ':' in v)\n"
        "assert all(int(fields[k].strip(),16)=="+str(mask)+" for k in ['CapEff','CapPrm','CapBnd'])\n"
        "assert fields['NoNewPrivs'].strip()=='1' and os.statvfs('/').f_flag&os.ST_RDONLY\n"
        "for directory in ['/input','/restore','/tmp']:\n"
        " assert os.statvfs(directory).f_flag&(os.ST_NOEXEC|os.ST_NOSUID|os.ST_NODEV)==(os.ST_NOEXEC|os.ST_NOSUID|os.ST_NODEV)\n"
        "with open('/proc/self/mountinfo') as source:mounts=source.read(1048576).splitlines()\n"
        "assert all(any(row.split()[4]==name and ' - tmpfs ' in row for row in mounts) for name in ['/input','/restore','/tmp'])\n"
        "with open('/sys/fs/cgroup/memory.max') as source:assert source.read().strip()=='1610612736'\n"
        "with socket.socket() as listener:\n listener.bind(('127.0.0.1',0));listener.listen(1)\n with socket.create_connection(listener.getsockname(),timeout=1) as client:\n  connected,_=listener.accept();connected.close()\n"
        "denials=[]\n"
        "for endpoint in "+repr(endpoints)+":\n"
        " try:\n"
        "  with socket.create_connection((endpoint['host'],endpoint['port']),timeout=1):raise ValueError('restore-source-route-reachable')\n"
        " except OSError as exc:\n"
        "  assert exc.errno in [errno.ENETUNREACH,errno.EHOSTUNREACH]\n"
        "  denials.append({'host':endpoint['host'],'port':endpoint['port'],'errno':exc.errno})\n"
        "print(json.dumps({'capabilityMask':"+str(mask)+",'noNewPrivileges':True,'rootReadonly':True,'workTmpfsNoexec':True,'sourceRouteDenials':denials}))\n")


def file_restore_program(manifest):
    archive_source=(HERE/'archive.py').read_text()
    body=archive_source+'\nMANIFEST='+repr(manifest)+'\n'+'''\nimport json,ssl\nos.mkdir('/restore/root',0o700)\nwith open('/input/node.tar.gz','rb') as source:result=extract('/restore/root',MANIFEST,source)\npairs=[]\nfor name,entry in MANIFEST['entries'].items():\n if entry['kind']!='file' or not name.endswith(('.crt','.pem')):continue\n certificate='/restore/root/'+name\n with open(certificate,'rb') as source:raw=source.read(262145)\n if b'-----BEGIN CERTIFICATE-----' not in raw:continue\n candidates=[name[:-4]+'.key',name[:-4]+'-key.pem']\n if b'PRIVATE KEY-----' in raw:candidates.insert(0,name)\n key=next((candidate for candidate in candidates if MANIFEST['entries'].get(candidate,{}).get('kind')=='file'),None)\n if key is None:continue\n context=ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT);context.load_cert_chain(certificate,'/restore/root/'+key)\n pairs.append({'certificate':name,'key':key,'matches':True})\nassert any(v['certificate']=='etc/kubernetes/pki/etcd/client.crt' for v in pairs)\nresult.update({'certificateKeyPairs':pairs,'allIncludedDiscoveredCertificateKeyPairsMatch':True,'operatingSystemBootProven':False,'sshAuthenticationProven':False})\nprint(json.dumps(result,sort_keys=True))\n'''
    require(len(body.encode())<=100000,'restore-file-program-argument-bound')
    return body


def etcd_restore_program(summary,attempt):
    # Binaries come from the independently approved immutable public image.
    return ("import hashlib,json,subprocess,time\n"+inspect.getsource(capture.visible_hash)+"\n"
        "def run(args):\n r=subprocess.run(args,stdin=subprocess.DEVNULL,capture_output=True,timeout=40)\n assert r.returncode==0\n return json.loads(r.stdout)\n"
        "status=run(['/tools/etcdutl','snapshot','status','/input/snapshot.db','-w','json'])\n"
        "assert status=="+repr(summary['snapshotStatus'])+"\n"
        "restored=subprocess.run(['/tools/etcdutl','snapshot','restore','/input/snapshot.db','--data-dir=/restore/recovered.etcd','--name=recovered','--initial-cluster=recovered=http://127.0.0.1:2380','--initial-advertise-peer-urls=http://127.0.0.1:2380','--initial-cluster-token='+"+repr(attempt)+"],capture_output=True,timeout=40)\n"
        "assert restored.returncode==0\n"
        "log=open('/restore/server.log','xb')\n"
        "server=subprocess.Popen(['/tools/etcd','--name=recovered','--data-dir=/restore/recovered.etcd','--listen-client-urls=http://127.0.0.1:2379','--advertise-client-urls=http://127.0.0.1:2379','--listen-peer-urls=http://127.0.0.1:2380','--initial-advertise-peer-urls=http://127.0.0.1:2380','--initial-cluster=recovered=http://127.0.0.1:2380'],stdout=log,stderr=subprocess.STDOUT)\n"
        "ctl=['/tools/etcdctl','--endpoints=http://127.0.0.1:2379','--dial-timeout=2s','--command-timeout=5s']\n"
        "deadline=time.monotonic()+30\n"
        "while True:\n"
        " ready=subprocess.run(ctl+['endpoint','health','-w','json'],capture_output=True,timeout=7)\n"
        " if ready.returncode==0:break\n"
        " assert server.poll() is None and time.monotonic()<deadline\n time.sleep(.2)\n"
        "health=json.loads(ready.stdout);assert len(health)==1 and health[0]['health'] is True\n"
        "current=run(ctl+['endpoint','status','-w','json'])[0]['Status'];header=current['header']\n"
        "assert str(header['cluster_id'])!="+repr(summary['sourceEtcdIdentity']['clusterId'])+" and str(header['member_id'])!="+repr(summary['sourceEtcdIdentity']['memberId'])+"\n"
        "assert header['revision']=="+str(summary['snapshotStatus']['revision'])+"\n"
        "visible=visible_hash(run(ctl+['get','','--from-key','-w','json']),header['revision'])\n"
        "assert visible=="+repr(summary['sourceFullVisibleKeyspaceSha256'])+"\n"
        "native=run(ctl+['endpoint','hashkv','-w','json'])[0]['HashKV']\n"
        "assert native['hash']=="+str(summary['sourceNativeHashkv'][0]['HashKV']['hash'])+" and native['hash_revision']==header['revision']\n"
        "canaries={}\n"
        "for name in "+repr(list(summary['sourceCanaryDigests']))+":\n"
        " value=run(ctl+['get',name,'-w','json']);assert value.get('count')==1\n"
        " canaries[name]=hashlib.sha256(json.dumps(value['kvs'],sort_keys=True,separators=(',',':')).encode()).hexdigest()\n"
        "assert canaries=="+repr(summary['sourceCanaryDigests'])+"\n"
        "print(json.dumps({'healthy':True,'sourceAndTargetVisibleStateEqual':True,'snapshotIntegrityVerified':True,'revision':header['revision'],'keyCount':visible['keyCount'],'keyHash':visible['sha256'],'canarySha256':hashlib.sha256(json.dumps(canaries,sort_keys=True,separators=(',',':')).encode()).hexdigest(),'targetClusterId':str(header['cluster_id']),'targetMemberId':str(header['member_id']),'revisionBump':False,'skipHashCheck':False,'nativeHashkvEqual':True},sort_keys=True))\n")


def restore(args,captured,source_metadata,attempt,authorize):
    require(callable(authorize) and authorize('node-etcd-isolated-restore-before-targets') is True,
            'restore-authenticated-policy-or-immutable-version-readback-refused')
    require(re.fullmatch('[a-z0-9][a-z0-9-]{0,63}',args.attempt_id)
            and re.fullmatch('sha256:[a-f0-9]{64}',args.image)
            and args.docker_host=='unix:///var/run/docker.sock','restore-target-input-profile-refused')
    target_profile_program(0,args.source_endpoints)  # All static fence inputs before any target operation.
    summary=captured['summary'];require(captured.get('error') is None and captured.get('cleanupPassed') is True
        and captured.get('localCaptureCompleted') is True and summary.get('sourceNodeMetadataUnchanged') is True
        and summary.get('recoveryAccepted') is False and set(summary['artifacts'])=={'snapshot.db.age','node-recovery.tar.gz.age'},
        'restore-authenticated-capture-binding-refused')
    entries=summary['archiveManifest']['entries'];require(set(entries)==set(source_metadata['entries'])
        and all(all(entries[name].get(k)==v for k,v in value.items()) for name,value in source_metadata['entries'].items()),
        'restore-source-metadata-manifest-mismatch')
    require(type(entries) is dict and 0<len(entries)<=8192
            and source_metadata.get('kind')=='node-file-metadata-only'
            and type(source_metadata.get('hashedBytes')) is int and 0<source_metadata['hashedBytes']<=512*1024*1024
            and {'etc/kubernetes/pki/etcd/ca.crt','etc/kubernetes/pki/etcd/client.crt',
                 'etc/kubernetes/pki/etcd/client.key','usr/local/bin/etcdctl','usr/local/bin/etcdutl'}<=set(entries),
            'restore-source-file-coverage-refused')
    config=Path(args.docker_config);config_info=config.lstat()
    require(stat.S_ISDIR(config_info.st_mode) and config_info.st_uid==os.getuid() and stat.S_IMODE(config_info.st_mode)==0o700,'restore-docker-config-custody-refused')
    config_file=config/'config.json';config_file_present=config_file.exists();docker_config=json.loads(config_file.read_bytes()) if config_file_present else {}
    require(not docker_config.get('credsStore') and not docker_config.get('credHelpers') and not docker_config.get('auths'),'restore-docker-credential-hook-refused')
    bindings={}
    for name,path in [('snapshot.db.age',args.snapshot),('node-recovery.tar.gz.age',args.archive)]:
        v=summary['artifacts'][name];bindings[str(path)]=private_artifact(path,v['ciphertextBytes'],v['ciphertextSha256'])
    files={Path(p):sha(Path(p).read_bytes()) for p in (args.docker,args.age,args.readback_identity,*([config_file] if config_file_present else []))}
    key=Path(args.readback_identity);require(not key.is_symlink() and key.stat().st_uid==os.getuid()
        and stat.S_IMODE(key.stat().st_mode)==0o600,'restore-agent-identity-custody-refused')
    base=[str(args.docker),'--host',args.docker_host,'--config',str(args.docker_config)]
    owned=[];cleaned=[];results={};sequence=0;error=None;original=None;custody_error=None
    def run(label,argv,data=None,allowed=(0,),timeout=60,cleanup_only=False):
        nonlocal sequence
        require(all(sha(p.read_bytes())==digest for p,digest in files.items()) and config_file.exists()==config_file_present,'restore-tool-or-identity-drift')
        scope='node-etcd-isolated-restore-'+('cleanup-' if cleanup_only else '')+label
        require(authorize(scope) is True,'restore-current-policy-or-window-refused')
        sequence+=1
        attempt.encrypt('restore-'+str(sequence).zfill(3)+'-intent',canonical({'argv':argv,'stdinBytes':len(data or b''),'cleanupOnly':cleanup_only}),args.age,args.recipient)
        r=capture.metadata.bounded_command(base+argv,data,timeout=timeout,maximum=4*1024*1024)
        attempt.encrypt('restore-'+str(sequence).zfill(3)+'-result',canonical({'exitCode':r.returncode,'stdoutBase64':base64.b64encode(r.stdout).decode(),'stderrBase64':base64.b64encode(r.stderr).decode()}),args.age,args.recipient)
        require(r.returncode in allowed,'restore-owned-command-refused');return r
    def check(entry,cleanup_only=False):
        name,cid,role,caps=entry
        current=json.loads(run('inspect-owned',['container','inspect',cid],cleanup_only=cleanup_only).stdout)[0]
        inspect_owned(current,cid,name,args.image,args.attempt_id,role,caps);return current
    try:
        image=json.loads(run('inspect-public-image',['image','inspect',args.image]).stdout)[0]
        require(image['Id']==args.image,'restore-approved-image-identity-refused')
        for role,caps,artifact,name in [('file',['CHOWN','FOWNER','DAC_READ_SEARCH'],'node-recovery.tar.gz.age','node.tar.gz'),
                                      ('etcd',[],'snapshot.db.age','snapshot.db')]:
            target='s427-recovery-'+role+'-'+sha(args.attempt_id.encode())[:12]
            absent=run('name-absent',['container','inspect',target],allowed=(0,1))
            require(absent.returncode==1 and b'No such container' in absent.stderr,'restore-target-name-preexists-or-unverified')
            entry=[target,None,role,caps];owned.append(entry)
            create=['create','--pull=never','--name',target,'--label','hexalith.io/node-recovery-attempt='+args.attempt_id,
                '--label','hexalith.io/node-recovery-role='+role,'--network','none','--ipc','private',
                '--read-only','--cap-drop','ALL','--security-opt','no-new-privileges','--memory','1536m',
                '--memory-swap','1536m','--cpus','1','--pids-limit','128']
            for cap in caps:create+=['--cap-add',cap]
            for mount in ('/input','/restore'):create+=['--tmpfs',mount+':rw,noexec,nodev,nosuid,size=768m,mode=0700']
            create+=['--tmpfs','/tmp:rw,noexec,nodev,nosuid,size=16m,mode=0700','--entrypoint','/usr/local/bin/python3',args.image,
                     '-I','-S','-B','-c','import time;time.sleep(1800)']
            result=run('create-owned',create);cid=result.stdout.decode().strip()
            require(re.fullmatch('[a-f0-9]{64}',cid),'restore-create-response-ownership-unverified');entry[1]=cid
            check(entry);run('start-owned',['start',cid]);check(entry)
            profile=json.loads(run('actual-target-profile',['exec',cid,'/usr/local/bin/python3','-I','-S','-B','-c',
                target_profile_program(13 if caps else 0,args.source_endpoints)]).stdout)
            tool_expected={name:source_metadata['entries']['usr/local/bin/'+name]['sha256'] for name in ('etcdctl','etcdutl')}
            tool_program=("import hashlib,json\nprint(json.dumps({name:hashlib.sha256(open('/tools/'+name,'rb').read(134217729)).hexdigest() for name in ['etcdctl','etcdutl']}))\n")
            measured_tools=json.loads(run('actual-public-tool-bytes',['exec',cid,'/usr/local/bin/python3','-I','-S','-B','-c',tool_program]).stdout)
            require(measured_tools==tool_expected,'restore-public-tool-source-byte-mismatch')
            v=summary['artifacts'][artifact];path=args.archive if role=='file' else args.snapshot
            require(private_artifact(path,v['ciphertextBytes'],v['ciphertextSha256'])==bindings[str(path)],'restore-ciphertext-incarnation-drift')
            check(entry)
            sink=base+['exec','-i',cid,'/usr/local/bin/python3','-I','-S','-B','-c',receiver_program(name,v['plaintextBytes'],v['plaintextSha256'])]
            require(authorize('node-etcd-isolated-restore-decrypt-pipe') is True,'restore-current-policy-or-window-refused')
            piped=pipe_plaintext([str(args.age),'-d','-i',str(args.readback_identity),str(path)],sink)
            attempt.encrypt('restore-'+role+'-plaintext-pipe',canonical(piped),args.age,args.recipient)
            require(piped['receiver']=={'bytes':v['plaintextBytes'],'sha256':v['plaintextSha256'],'plaintextOnlyInTargetTmpfs':True},
                    'restore-target-plaintext-receiver-binding-refused');check(entry)
            program=file_restore_program(summary['archiveManifest']) if role=='file' else etcd_restore_program(summary,args.attempt_id)
            result=run('actual-'+role+'-restoration',['exec',cid,'/usr/local/bin/python3','-I','-S','-B','-c',program],timeout=180)
            results[role]={'profile':profile,'restoration':json.loads(result.stdout),'containerId':cid}
    except BaseException as exc:
        original=exc
        error=type(exc).__name__+(':'+str(exc) if isinstance(exc,ValueError) else '')
        try:
            attempt.encrypt('restore-original-failure',canonical({'error':error,'traceback':traceback.format_exc(),'pipeDiagnostics':getattr(exc,'pipe_diagnostics',None)}),args.age,args.recipient)
        except BaseException as failed_custody:
            custody_error=failed_custody
    finally:
        for entry in reversed(owned):
            name,cid,role,caps=entry
            try:
                require(cid is not None,'restore-create-outcome-ownership-unverified');check(entry,cleanup_only=True)
                run('delete-owned',['rm','-f','-v',cid],cleanup_only=True);absent=run('verify-absent',['container','inspect',cid],allowed=(0,1),cleanup_only=True)
                require(absent.returncode==1 and b'No such container' in absent.stderr,'restore-owned-target-absence-unverified')
                cleaned.append({'containerId':cid,'role':role,'absenceVerified':True})
            except BaseException as exc:cleaned.append({'containerId':cid,'role':role,'absenceVerified':False,'errorType':type(exc).__name__})
    if custody_error is not None:
        group=BaseExceptionGroup('restore-original-and-custody-failure',[original,custody_error])
        group.restore_cleanup=cleaned
        raise group
    cleanup_pass=bool(owned) and len(cleaned)==len(owned) and all(v['absenceVerified'] for v in cleaned)
    return {'kind':'isolated-node-etcd-restoration-only','state':'passed' if error is None and cleanup_pass and len(results)==2 else 'failed',
            'error':error,'results':results,'cleanup':cleaned,'cleanupPassed':cleanup_pass,
            'targetCreationIntents':len(owned),'confirmedTargetCreates':sum(v[1] is not None for v in owned),
            'cleanupNotApplicableNoTargetsCreated':not owned,
            'configurationSha256':sha(canonical(source_metadata)),'operatorPlaintextFilesWritten':False,
            'operatorIdentityCopiedToTarget':False,'operatingSystemBootProven':False,'sshAuthenticationProven':False,
            'immutableProviderReadbackQualifiedHere':False,'sourceRecoveryAccepted':False}
