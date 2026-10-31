"""Fixed metadata-only same-node source/owned-target directory probe.

No contents are read and no source path is created or changed. The caller binds
fresh PV/PVC/Node identities and uses the qualified temporary metadata lifecycle.
Bind-mounted parent metadata does not prove host ancestor symlink absence.
"""
import copy,hashlib,importlib.util,inspect,re
from pathlib import Path
spec=importlib.util.spec_from_file_location('volume_probe_transport',Path(__file__).resolve().parent.parent/'native-node-metadata/metadata.py')
transport=importlib.util.module_from_spec(spec);spec.loader.exec_module(transport)

def require(ok,why):
 if not ok:raise ValueError(why)

def validate(parent,expected):
 require(isinstance(parent,str) and parent.startswith('/') and parent not in ('/','/etc','/var','/var/lib','/root') and len(Path(parent).parts)>=3 and all(v not in ('.','..') for v in parent.split('/')[1:]),'volume-probe-parent-refused')
 require(type(expected) is dict and 1<=len(expected)<=16,'volume-probe-scope-refused')
 for leaf,record in expected.items():
  require(isinstance(leaf,str) and re.fullmatch('[A-Za-z0-9][A-Za-z0-9._-]{0,252}',leaf) and leaf not in ('.','..') and type(record) is dict and set(record)=={'state','stat'} and record['state'] in ('directory','absent'),'volume-probe-leaf-refused')
  require(record['stat'] is None or (type(record['stat']) is dict and set(record['stat'])=={'device','inode','uid','gid','mode'} and all(type(v) is int and v>=0 for v in record['stat'].values())),'volume-probe-stat-refused')
 require(any(v['state']=='directory' for v in expected.values()),'volume-probe-positive-control-required')

def probe(parent,expected):
 import os,stat,json
 with open('/proc/self/status') as stream:fields=dict(v.split(':',1) for v in stream.read(65537).splitlines() if ':' in v)
 if any(int(fields.get(k,'-1').strip(),16)!=4 for k in ('CapEff','CapPrm','CapBnd')) or fields.get('NoNewPrivs','').strip()!='1' or not os.statvfs('/').f_flag&os.ST_RDONLY:raise ValueError('volume-probe-process-profile-refused')
 mounted='/source'+parent
 if not os.statvfs(mounted).f_flag&os.ST_RDONLY:raise ValueError('volume-probe-source-parent-not-readonly')
 fd=os.open(mounted,os.O_RDONLY|os.O_DIRECTORY|os.O_NOFOLLOW);entries={}
 try:
  for leaf,wanted in expected.items():
   try:info=os.stat(leaf,dir_fd=fd,follow_symlinks=False)
   except FileNotFoundError:
    if wanted['state']!='absent':raise ValueError('volume-probe-positive-path-absent')
    entries[leaf]={'kind':'absent','absenceTypedFileNotFound':True};continue
   if wanted['state']!='directory' or not stat.S_ISDIR(info.st_mode):raise ValueError('volume-probe-path-exists-or-is-not-directory')
   child=os.open(leaf,os.O_RDONLY|os.O_DIRECTORY|os.O_NOFOLLOW,dir_fd=fd)
   try:
    current=os.fstat(child)
    if (current.st_dev,current.st_ino)!=(info.st_dev,info.st_ino):raise ValueError('volume-probe-directory-replaced')
    value={'device':info.st_dev,'inode':info.st_ino,'uid':info.st_uid,'gid':info.st_gid,'mode':stat.S_IMODE(info.st_mode)}
    if wanted['stat'] is not None and wanted['stat']!=value:raise ValueError('volume-probe-directory-identity-drift')
    entries[leaf]={'kind':'directory','stat':value}
   finally:os.close(child)
 finally:os.close(fd)
 return {'kind':'node-file-metadata-only','entries':entries,'selectedDirectoryPathsOnly':True,'sourceContentExported':False,'networkOperations':0,'processSecurityProfile':{'onlyDacReadSearchEffectivePermittedAndBounded':True,'noNewPrivileges':True,'rootFilesystemReadOnly':True},'sourceWritten':False,'bindMountedParentIsNotHostAncestorSymlinkProof':True}

def program(parent,expected):
 validate(parent,expected)
 return inspect.getsource(probe)+'\nimport json\nprint(json.dumps(probe('+repr(parent)+','+repr(expected)+'),sort_keys=True))\n'

def manifests(attempt,node,node_uid,namespace_uid,image,parent,expected):
 body=program(parent,expected);policy,pod=transport.manifests(attempt,node,node_uid,namespace_uid,image)
 for obj in (policy,pod):
  obj['metadata']=copy.deepcopy(obj['metadata']);obj['metadata']['name']='s427-volume-meta-'+attempt
  obj['metadata']['labels']['app.kubernetes.io/name']='s427-volume-metadata'
  obj['metadata']['annotations']['hexalith.io/program-sha256']=hashlib.sha256(body.encode()).hexdigest()
 container=pod['spec']['containers'][0];container['command'][-1]=body
 pod['spec']['volumes']=[{'name':'exact-parent','hostPath':{'path':parent,'type':'Directory'}}]
 container['volumeMounts']=[{'name':'exact-parent','mountPath':'/source'+parent,'readOnly':True}]
 policy['spec']['podSelector']={'matchLabels':copy.deepcopy(pod['metadata']['labels'])}
 return policy,pod
