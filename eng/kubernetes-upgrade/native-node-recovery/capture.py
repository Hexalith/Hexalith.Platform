"""Fixed node/configuration and external-etcd capture payload; no live dispatcher.

Only an authenticated source-bound caller may create the exact helper after
signed policy/signature immutable readback. All source mounts remain read-only;
plaintext snapshot exists only in bounded memory-backed helper storage. Archive
bytes enter age directly, and only ciphertext leaves through binary exec.
"""
import copy
import hashlib
import importlib.util
import inspect
import ipaddress
import json
from pathlib import Path
import re

DIRECTORY = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('capture_node_metadata', DIRECTORY.parent/'native-node-metadata/metadata.py')
metadata = importlib.util.module_from_spec(spec); spec.loader.exec_module(metadata)
spec = importlib.util.spec_from_file_location('capture_node_archive', DIRECTORY/'archive.py')
archive = importlib.util.module_from_spec(spec); spec.loader.exec_module(archive)


def require(ok, reason):
    if not ok:raise ValueError(reason)


def validate(config):
    require(type(config) is dict and set(config)=={
        'endpoint','nodeMetadata','ageSha256','ageBytes','recipients','deadlineEpoch','expectedEtcdIdentity'},
        'node-capture-config-shape-refused')
    endpoint=config['endpoint'];require(isinstance(endpoint,str) and re.fullmatch(r'https://[0-9.]+:[0-9]{1,5}',endpoint),
                                       'node-capture-endpoint-refused')
    host,port=endpoint[8:].rsplit(':',1);ipaddress.IPv4Address(host)
    require(0<int(port)<=65535,'node-capture-endpoint-refused')
    require(isinstance(config['ageSha256'],str) and re.fullmatch('[a-f0-9]{64}',config['ageSha256'])
            and type(config['ageBytes']) is int and 0<config['ageBytes']<=16*1024*1024,'node-capture-age-binding-refused')
    require(type(config['recipients']) is list and len(config['recipients'])==2
            and len(set(config['recipients']))==2 and all(isinstance(v,str) and re.fullmatch(
                r'ssh-ed25519 [A-Za-z0-9+/]+={0,2}(?: [^\r\n]{0,128})?',v) for v in config['recipients']),
            'node-capture-recipient-profile-refused')
    require(type(config['deadlineEpoch']) is int and config['deadlineEpoch']>0,'node-capture-deadline-refused')
    value=config['nodeMetadata'];require(type(value) is dict and value.get('kind')=='node-file-metadata-only'
            and value.get('processSecurityProfile')=={'onlyDacReadSearchEffectivePermittedAndBounded':True,
                'noNewPrivileges':True,'rootFilesystemReadOnly':True}
            and type(value.get('entries')) is dict and 0<len(value['entries'])<=8192
            and type(value.get('hashedBytes')) is int and 0<value['hashedBytes']<=512*1024*1024,
            'node-capture-source-metadata-refused')
    required={'etc/kubernetes/pki/etcd/ca.crt','etc/kubernetes/pki/etcd/client.crt',
              'etc/kubernetes/pki/etcd/client.key','usr/local/bin/etcdctl','usr/local/bin/etcdutl'}
    require(required<=set(value['entries']),'node-capture-required-source-files-missing')
    for name,entry in value['entries'].items():
        archive.parts(name)
        require(type(entry) is dict and entry.get('kind') in ('file','directory','symlink')
                and all(type(entry.get(k)) is int and entry[k]>=0 for k in ('uid','gid','mode'))
                and entry['mode']<=0o7777,'node-capture-member-metadata-refused')
        if entry['kind']=='file':require(type(entry.get('bytes')) is int and 0<=entry['bytes']<=128*1024*1024
            and isinstance(entry.get('sha256'),str) and re.fullmatch('[a-f0-9]{64}',entry['sha256']),
            'node-capture-member-file-refused')
        if entry['kind']=='symlink':require(isinstance(entry.get('target'),str) and '\x00' not in entry['target'],
                                           'node-capture-member-symlink-refused')
    identity=config['expectedEtcdIdentity'];require(type(identity) is dict and set(identity)=={'clusterId','memberId'}
            and all(type(v) is str and re.fullmatch('[1-9][0-9]{0,19}',v) and int(v)<2**64 for v in identity.values()),
            'node-capture-etcd-identity-refused')
    return config


def installation_program(digest, size):
    require(isinstance(digest,str) and re.fullmatch('[a-f0-9]{64}',digest) and type(size) is int
            and 0<size<=16*1024*1024,'node-capture-public-tool-binding-refused')
    # Bound public binary stdin before installing its sole executable pathname.
    return ("import hashlib,os,selectors,sys,time\n"
        "fd=os.open('/work/age.partial',os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o500)\n"
        "count=0;digest=hashlib.sha256();deadline=time.monotonic()+45\n"
        "try:\n"
        " with selectors.DefaultSelector() as selected:\n"
        "  os.set_blocking(0,False);selected.register(0,selectors.EVENT_READ)\n"
        "  while selected.get_map():\n"
        "   if time.monotonic()>=deadline:raise ValueError('public-tool-install-deadline')\n"
        "   for key,_ in selected.select(.1):\n"
        "    data=os.read(key.fd,65536)\n"
        "    if not data:selected.unregister(key.fd);continue\n"
        "    count+=len(data)\n"
        "    if count>"+str(size)+":raise ValueError('public-tool-install-size')\n"
        "    digest.update(data);view=memoryview(data)\n"
        "    while view:\n"
        "     n=os.write(fd,view)\n"
        "     if n<=0:raise ValueError('public-tool-install-write')\n"
        "     view=view[n:]\n"
        " if count!="+str(size)+" or digest.hexdigest()!="+repr(digest)+":raise ValueError('public-tool-install-hash')\n"
        " os.fsync(fd)\n"
        "finally:os.close(fd)\n"
        "os.link('/work/age.partial','/work/age',follow_symlinks=False);os.unlink('/work/age.partial')\n"
        "print('public-tool-exact-bytes-installed')\n")


def visible_hash(observation, revision):
    """Hash actual complete revision-bound visible records, separately from CRC."""
    import base64
    import hashlib
    import json
    if (type(revision) is not int or revision<=0 or type(observation) is not dict
            or type(observation.get('kvs')) is not list
            or type(observation.get('count')) is not int
            or observation['count']!=len(observation['kvs'])
            or not 0<observation['count']<=100000):
        raise ValueError('node-capture-full-visible-keyspace-profile-refused')
    records=[];seen=set();total=0
    for row in observation['kvs']:
        if (type(row) is not dict or set(row)-{'key','value','create_revision','mod_revision','version','lease'}
                or not {'key','value','create_revision','mod_revision','version'}<=set(row)):
            raise ValueError('node-capture-visible-key-record-refused')
        for field in ('create_revision','mod_revision','version','lease'):
            value=row.get(field,0)
            if type(value) is not int or (field!='lease' and value<=0):
                raise ValueError('node-capture-visible-native-integer-refused')
        if row['mod_revision']>revision or row['create_revision']>row['mod_revision']:
            raise ValueError('node-capture-visible-revision-refused')
        try:
            key=base64.b64decode(row['key'],validate=True)
            value=base64.b64decode(row['value'],validate=True)
        except (ValueError,TypeError):raise ValueError('node-capture-visible-base64-refused') from None
        if not key or key in seen:raise ValueError('node-capture-visible-key-duplicate-or-empty')
        seen.add(key);total+=len(key)+len(value)
        if total>128*1024*1024:raise ValueError('node-capture-visible-content-size-bound')
        records.append(row)
    raw=json.dumps(sorted(records,key=lambda row:row['key']),sort_keys=True,separators=(',',':')).encode()
    if len(raw)>128*1024*1024:raise ValueError('node-capture-visible-canonical-size-bound')
    return {'sha256':hashlib.sha256(raw).hexdigest(),'keyCount':len(records),
            'canonicalBytes':len(raw),'revision':revision,
            'algorithm':'SHA256 sorted-base64-key native visible key/value/create_revision/mod_revision/version/optional-lease compact sorted-key JSON'}


def command_diagnostic(argv, result):
    """Retain actual stderr; complete source KV stdout contributes only a digest."""
    import base64
    import hashlib
    import json
    value={'argv':argv,'exitCode':result.returncode,'stdoutBytes':len(result.stdout),
           'stdoutSha256':hashlib.sha256(result.stdout).hexdigest(),
           'stderrBase64':base64.b64encode(result.stderr).decode()}
    return (json.dumps(value,sort_keys=True)+'\n').encode()


def payload(config, paths, mounts):
    """Runs inside the exact helper; never logs plaintext source or artifacts."""
    import hashlib
    import json
    import os
    import resource
    import selectors
    import signal
    import stat
    import subprocess
    import sys
    import time
    from datetime import datetime,timezone
    def utc():return datetime.now(timezone.utc).isoformat().replace('+00:00','Z')
    def bound():
        if time.time()>=config['deadlineEpoch']:raise ValueError('node-capture-absolute-deadline')
    def current():return collect_metadata('/source',paths,mounts,require_security_profile=True)
    def need(ok,why):
        if not ok:raise ValueError(why)
    bound()
    with open('/proc/meminfo') as stream:
        meminfo=dict(v.split(':',1) for v in stream.read(65537).splitlines() if ':' in v)
    available=meminfo.get('MemAvailable','').split()
    need(len(available)==2 and available[1]=='kB' and available[0].isdigit()
         and int(available[0])*1024>=2*1024*1024*1024,'node-capture-host-memory-margin-refused')
    limit_path=('/sys/fs/cgroup/memory.max' if os.path.exists('/sys/fs/cgroup/memory.max')
                else '/sys/fs/cgroup/memory/memory.limit_in_bytes')
    with open(limit_path) as stream:memory_limit=stream.read(128).strip()
    need(memory_limit.isdigit() and int(memory_limit)==1536*1024*1024,
         'node-capture-actual-cgroup-memory-limit-refused')
    work=os.statvfs('/work');need(work.f_blocks*work.f_frsize<=1024*1024*1024
        and work.f_bavail*work.f_frsize>=768*1024*1024,'node-capture-memory-volume-capacity-refused')
    with open('/proc/self/mountinfo') as stream:lines=stream.read(1024*1024).splitlines()
    need(any(v.split()[4]=='/work' and ' - tmpfs ' in v for v in lines),'node-capture-memory-volume-type-refused')
    first=current();need(first==config['nodeMetadata'],'node-capture-source-metadata-drift')
    deadline=min(config['deadlineEpoch'],time.time()+120)
    while not os.path.lexists('/work/age') or os.path.lexists('/work/age.partial'):
        need(time.time()<deadline,'node-capture-public-tool-install-timeout');time.sleep(.2)
    fd=os.open('/work/age',os.O_RDONLY|os.O_NOFOLLOW);info=os.fstat(fd)
    try:
        raw=os.read(fd,config['ageBytes']+1)
        need(stat.S_ISREG(info.st_mode) and info.st_uid==0 and info.st_nlink==1
            and stat.S_IMODE(info.st_mode)==0o500 and len(raw)==info.st_size==config['ageBytes']
            and hashlib.sha256(raw).hexdigest()==config['ageSha256'],'node-capture-installed-tool-drift')
    finally:os.close(fd)
    ca,cert,key=('/source/etc/kubernetes/pki/etcd/'+v for v in ('ca.crt','client.crt','client.key'))
    ctl=['/source/usr/local/bin/etcdctl','--endpoints='+config['endpoint'],'--cacert='+ca,'--cert='+cert,
         '--key='+key,'--dial-timeout=5s','--command-timeout=15s']
    def command(args,maximum=65536,timeout=25):
        bound();result=bounded_command(args,timeout=timeout,maximum=maximum)
        if result.returncode!=0:
            # Small original diagnostics are age-encrypted before any log output.
            # Source KV stdout is never put into a Pod log or plaintext file.
            sealed=bounded_command(['/work/age','-r',config['recipients'][0],'-r',config['recipients'][1]],
                                   data=command_diagnostic(args,result),timeout=20,maximum=262144)
            if sealed.returncode==0:
                import base64
                print(json.dumps({'kind':'encrypted-node-command-failure',
                    'ciphertextSha256':hashlib.sha256(sealed.stdout).hexdigest(),
                    'ageCiphertextBase64':base64.b64encode(sealed.stdout).decode()}),flush=True)
            need(False,'node-capture-etcd-command-refused')
        return json.loads(result.stdout)
    status=command(ctl+['endpoint','status','-w','json'])
    need(len(status)==1 and status[0]['Endpoint']==config['endpoint'],'node-capture-etcd-status-profile')
    header=status[0]['Status']['header'];identity=config['expectedEtcdIdentity']
    need(str(header['cluster_id'])==identity['clusterId'] and str(header['member_id'])==identity['memberId']
        and status[0]['Status']['version']=='3.6.5' and not status[0]['Status'].get('errors')
        and 0<status[0]['Status']['dbSize']<=96*1024*1024,'node-capture-etcd-source-identity-or-size-drift')
    need(command(ctl+['endpoint','health','-w','json'])[0].get('health') is True,'node-capture-etcd-health-refused')
    # Only the private memory-backed helper directory receives this snapshot.
    snap=ctl.copy();snap[-1]='--command-timeout=90s'
    started=utc();bound()
    limit_exec=('import os,resource,sys;resource.setrlimit(resource.RLIMIT_FSIZE,'
                '(134217728,134217728));os.execv(sys.argv[1],sys.argv[1:])')
    result=bounded_command([sys.executable,'-I','-S','-B','-c',limit_exec]
                          +snap+['snapshot','save','/work/snapshot.db'],timeout=100,maximum=65536)
    need(result.returncode==0,'node-capture-snapshot-save-refused')
    info=os.lstat('/work/snapshot.db');need(stat.S_ISREG(info.st_mode) and 0<info.st_size<=128*1024*1024,
                                         'node-capture-snapshot-size-refused')
    integrity=command(['/source/usr/local/bin/etcdutl','snapshot','status','/work/snapshot.db','-w','json'])
    revision=integrity['revision'];need(type(revision) is int and revision>0,'node-capture-snapshot-revision-refused')
    native=command(ctl+['endpoint','hashkv','--rev='+str(revision),'-w','json'])
    full_visible=visible_hash(command(ctl+['get','','--from-key','--rev='+str(revision),'-w','json'],
                                      maximum=128*1024*1024),revision)
    canaries={}
    for name in ('/registry/namespaces/default','/registry/namespaces/kube-system',
                 '/registry/services/specs/default/kubernetes'):
        observed=command(ctl+['get',name,'--rev='+str(revision),'-w','json'])
        need(observed.get('count')==1 and len(observed.get('kvs',[]))==1,'node-capture-source-canary-missing')
        canaries[name]=hashlib.sha256(json.dumps(observed['kvs'],sort_keys=True,separators=(',',':')).encode()).hexdigest()
    need(len(native)==1 and native[0]['HashKV']['hash_revision']==revision
        and integrity['totalKey']==full_visible['keyCount'],
        'node-capture-snapshot-count-or-hash-profile-refused')
    class CipherWriter:
        def __init__(self,name,maximum):
            self.fd=os.open('/work/'+name,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o400)
            def child_limits():resource.setrlimit(resource.RLIMIT_FSIZE,(maximum+1024*1024,maximum+1024*1024))
            self.child=subprocess.Popen(['/work/age','-r',*config['recipients'][:1],'-r',config['recipients'][1]],
                stdin=subprocess.PIPE,stdout=self.fd,stderr=subprocess.PIPE,start_new_session=True,preexec_fn=child_limits,
                env={'LANG':'C.UTF-8'})
            self.selected=selectors.DefaultSelector();os.set_blocking(self.child.stdin.fileno(),False)
            os.set_blocking(self.child.stderr.fileno(),False)
            self.selected.register(self.child.stderr,selectors.EVENT_READ,'err')
            self.sha=hashlib.sha256();self.count=0;self.errors=bytearray();self.maximum=maximum
            self.deadline=time.monotonic()+200
        def write(self,data):
            self.sha.update(data);self.count+=len(data)
            need(self.count<=self.maximum,'node-capture-plaintext-size-bound');sent=0
            self.selected.register(self.child.stdin,selectors.EVENT_WRITE,'in')
            try:
                while sent<len(data):
                    bound();need(time.monotonic()<self.deadline,'node-capture-encryption-deadline')
                    for selected,_ in self.selected.select(.1):
                        if selected.data=='in':sent+=os.write(selected.fd,data[sent:sent+65536])
                        else:
                            raw=os.read(selected.fd,65536)
                            if not raw:self.selected.unregister(selected.fileobj);continue
                            self.errors.extend(raw);need(len(self.errors)<=65536,'node-capture-encryption-diagnostic-bound')
            finally:self.selected.unregister(self.child.stdin)
            return sent
        def flush(self):pass
        def finish(self):
            self.child.stdin.close()
            while self.selected.get_map():
                bound();need(time.monotonic()<self.deadline,'node-capture-encryption-final-deadline')
                for selected,_ in self.selected.select(.1):
                    raw=os.read(selected.fd,65536)
                    if not raw:self.selected.unregister(selected.fileobj);continue
                    self.errors.extend(raw);need(len(self.errors)<=65536,'node-capture-encryption-diagnostic-bound')
            self.child.wait(timeout=max(.001,self.deadline-time.monotonic()))
            need(self.child.returncode==0,'node-capture-encryption-refused');os.fsync(self.fd)
            return {'plaintextSha256':self.sha.hexdigest(),'plaintextBytes':self.count}
        def close(self):
            if self.child.poll() is None:os.killpg(self.child.pid,signal.SIGKILL)
            self.child.wait(timeout=5);self.selected.close();self.child.stdin.close();self.child.stderr.close();os.close(self.fd)
    artifacts={}
    cipher=CipherWriter('snapshot.db.age',128*1024*1024)
    try:
        with open('/work/snapshot.db','rb') as stream:
            while True:
                part=stream.read(1048576)
                if not part:break
                cipher.write(part)
        artifacts['snapshot.db.age']=cipher.finish()
    finally:cipher.close()
    os.unlink('/work/snapshot.db')
    cipher=CipherWriter('node-recovery.tar.gz.age',512*1024*1024)
    try:
        manifest=create('/source',first,cipher)
        artifacts['node-recovery.tar.gz.age']=cipher.finish()
    finally:cipher.close()
    need(current()==first,'node-capture-source-drift-after-capture')
    for name,artifact in artifacts.items():
        digest=hashlib.sha256();size=0
        with open('/work/'+name,'rb') as stream:
            while True:
                data=stream.read(1048576)
                if not data:break
                size+=len(data);digest.update(data)
                need(size<=513*1024*1024,'node-capture-ciphertext-bound')
        artifact.update(ciphertextBytes=size,ciphertextSha256=digest.hexdigest(),encryptedBeforeTransport=True)
    summary={'kind':'local-node-etcd-capture-only','startedAt':started,'completedAt':utc(),
        'sourceEtcdIdentity':identity,'snapshotStatus':integrity,'sourceNativeHashkv':native,
        'sourceFullVisibleKeyspaceSha256':full_visible,
        'sourceCount':full_visible['keyCount'],'sourceCanaryDigests':canaries,'archiveManifest':manifest,
        'artifacts':artifacts,'sourceMountsReadonly':True,'snapshotPlaintextOnlyInMemoryBackedHelper':True,
        'memoryCapacity':{'hostMemAvailableBytes':int(available[0])*1024,
                          'actualHelperCgroupLimitBytes':int(memory_limit),
                          'privateTmpfsCapacityBytes':work.f_blocks*work.f_frsize},
        'plaintextArchiveFileWritten':False,'plaintextSnapshotRemoved':not os.path.lexists('/work/snapshot.db'),
        'sourceNodeMetadataUnchanged':True,'providerReadback':False,'recoveryAccepted':False}
    with open('/work/result.partial','x') as stream:
        os.chmod('/work/result.partial',0o400);json.dump(summary,stream,sort_keys=True)
        stream.flush();os.fsync(stream.fileno())
    os.link('/work/result.partial','/work/result.json',follow_symlinks=False)
    os.unlink('/work/result.partial')
    print('local-encrypted-capture-complete',flush=True)
    # Keep ciphertext available only until the prebound absolute deadline.
    while time.time()<config['deadlineEpoch']:time.sleep(min(.5,config['deadlineEpoch']-time.time()))


def program(config):
    validate(config)
    body=('import os,selectors,signal,subprocess,time\n'+inspect.getsource(metadata.collect_metadata)+'\n'
            +inspect.getsource(metadata.bounded_command)+'\n'+(DIRECTORY/'archive.py').read_text()+'\n'
            +inspect.getsource(visible_hash)+'\n'+inspect.getsource(command_diagnostic)+'\n'
            +inspect.getsource(payload)+'\npayload('+repr(config)+', '+repr(metadata.PATHS+('root/.ssh',))
            +', '+repr(tuple(p for _,p,_ in metadata.MOUNTS)+('/root',))+')\n')
    require(len(body.encode())<=100000,'node-capture-program-argument-bound')
    return body


def manifests(attempt,node,node_uid,namespace_uid,image,config):
    validate(config);policy,pod=metadata.manifests(attempt,node,node_uid,namespace_uid,image)
    body=program(config)
    for obj in (policy,pod):
        obj['metadata']=copy.deepcopy(obj['metadata']);obj['metadata']['name']='s427-node-capture-'+attempt
        obj['metadata']['labels']['app.kubernetes.io/name']='s427-node-capture'
        obj['metadata']['annotations']['hexalith.io/program-sha256']=hashlib.sha256(body.encode()).hexdigest()
    host,port=config['endpoint'][8:].rsplit(':',1)
    policy['spec']['egress']=[{'to':[{'ipBlock':{'cidr':host+'/32'}}],'ports':[{'protocol':'TCP','port':int(port)}]}]
    pod['spec']['activeDeadlineSeconds']=1200
    container=pod['spec']['containers'][0];container['command'][-1]=body
    container['resources']={'requests':{'cpu':'100m','memory':'256Mi'},'limits':{'cpu':'1','memory':'1536Mi'}}
    pod['spec']['volumes'] += [{'name':'root-ssh-parent','hostPath':{'path':'/root','type':'Directory'}},
                              {'name':'private-work','emptyDir':{'medium':'Memory','sizeLimit':'1Gi'}}]
    container['volumeMounts'] += [{'name':'root-ssh-parent','mountPath':'/source/root','readOnly':True},
                                 {'name':'private-work','mountPath':'/work'}]
    policy['spec']['podSelector']={'matchLabels':copy.deepcopy(pod['metadata']['labels'])}
    return policy,pod
