"""Read-only complete accepted launch graph checks. No release, enrollment or immutable flag is created."""
import array
import fcntl
import hashlib
import json
import os
import platform
import re
import stat
import struct
from pathlib import Path
import bounded_file_io as selected_io

RESOURCES={'eventstore':'eventStore','tenants':'tenants','tenants-api':'tenants','parties':'parties','conversations':'conversations','agents':'agents','agents-ui':'agents'}


def blockers(manifest,profile):
    def inspect():
        if not isinstance(manifest,dict) or manifest.get('protocol')!='AcceptedCompleteImmutableLaunchGraphV1':return ['External.AcceptedCompleteImmutableLaunchGraphV1']
        graphs=manifest.get('resources')
        if not isinstance(graphs,list) or len(graphs)!=7 or {row.get('resource') for row in graphs}!=set(RESOURCES):return ['External.CompleteSelectedLaunchGraph']
        missing=listener_blockers(manifest,profile)
        if missing:return missing
        for graph in graphs:
            root=graph['root'];name=graph['resource'];entry=graph['entryPoint'];files=graph['files'];closure=graph.get('wholeImageNativeLoaderClosure',{})
            if (root!=profile['services'][RESOURCES[name]] or not os.path.isabs(root) or os.path.normpath(root)!=root
                    or graph.get('executionProtocol')!='SelfContainedStaticElfV1' or closure.get('protocol')!='IndependentlyAcceptedStaticElfKernelClosureV1'
                    or not re.fullmatch('[0-9a-f]{64}',str(closure.get('evidenceSha256','')))):
                return ['External.AcceptedWholeImageNativeLoaderClosureV1']
            fd=selected_io.acquire(root,directory=True)
            try:
                info=os.fstat(fd);identity=f'{os.major(info.st_dev)}:{os.minor(info.st_dev)}:{info.st_ino}'
                if identity!=graph['directoryIdentity']:return ['External.AcceptedImmutableLaunchNamespaceV1']
                _immutable(fd,True)
                _directory_tree(fd,0,[0])
                expected={row['path']:row for row in files}
                if not 1<=len(expected)==len(files)<=10000 or entry not in expected:return ['External.CompleteSelectedLaunchGraph']
                actual=selected_io.census(root)
                if actual!={str(Path(root)/path) for path in expected}:return ['External.CompleteSelectedLaunchGraph']
                for relative,pin in expected.items():
                    if relative.startswith('/') or '..' in relative.split('/') or str(Path(relative))!=relative:return ['External.ExactLaunchGraphLocator']
                    path=str(Path(root)/relative);source=selected_io.acquire(path)
                    try:
                        before=os.fstat(source);_immutable(source,False)
                        if not stat.S_ISREG(before.st_mode) or not 0<before.st_size<=1024*1024*1024 or before.st_size!=pin['bytes']:return ['External.ExactAcceptedLaunchFile']
                        hashed=hashlib.sha256()
                        while True:
                            selected_io.check();body=os.read(source,65536)
                            if not body:break
                            hashed.update(body)
                        if hashed.hexdigest()!=pin['sha256']:return ['External.ExactAcceptedLaunchFile']
                        _immutable(source,False)
                        if name=='agents-ui' and relative==graph['listener']['uiHost']['artifact']:
                            if before.st_size>65536:return [UI_HOST_REQUIREMENT]
                            contract=json.loads(os.pread(source,before.st_size,0),object_pairs_hook=_unique_object)
                            if contract!=graph['listener']['uiHost']['contract']:return [UI_HOST_REQUIREMENT+':ActualContractBytes']
                        if relative==entry:
                            if not os.access('/proc/self/fd/'+str(source),os.X_OK,effective_ids=True):return ['External.AcceptedExecutableEntrypointAccessV1']
                            _static_elf(source)
                    finally:os.close(source)
                    parent=Path(path).parent
                    while str(parent)!=root:
                        directory=selected_io.acquire(str(parent),directory=True)
                        try:_immutable(directory,True)
                        finally:os.close(directory)
                        parent=parent.parent
                check=selected_io.acquire(root,directory=True)
                try:
                    current=os.fstat(check)
                    if (current.st_dev,current.st_ino)!=(info.st_dev,info.st_ino):return ['External.AcceptedImmutableLaunchNamespaceV1']
                finally:os.close(check)
            finally:os.close(fd)
        return []
    try:return selected_io.run(inspect)
    except (OSError,ValueError,TypeError,KeyError,AttributeError,OverflowError,struct.error):return ['External.AcceptedImmutableLaunchNamespaceOrWholeImageNativeLoaderClosureV1']


def _immutable(fd,directory):
    flags=array.array('I',[0,0]);fcntl.ioctl(fd,0x80086601,flags,True)
    if not flags[0]&0x10 or not directory and not flags[0]&0x100000:raise ValueError('Actual immutable namespace and fs-verity required')


def _static_elf(fd):
    selected_io.check();length=os.fstat(fd).st_size
    header=os.pread(fd,64,0);machine={'x86_64':62,'aarch64':183}.get(platform.machine(),-1)
    if (not 64<=length<=1024*1024*1024 or len(header)!=64 or header[:7]!=b'\x7fELF\x02\x01\x01'
            or struct.unpack_from('<HHI',header,16)!=(2,machine,1) or struct.unpack_from('<H',header,52)[0]!=64):
        raise ValueError('External.AcceptedWholeImageNativeLoaderClosureV1')
    entry,offset=struct.unpack_from('<QQ',header,24);size,count=struct.unpack_from('<HH',header,54)
    if size!=56 or not 1<=count<=128 or offset<64 or offset+size*count>length:raise ValueError('Complete bounded ELF map')
    loads=[];executable_entry=False
    for index in range(count):
        selected_io.check();raw=os.pread(fd,56,offset+index*size)
        if len(raw)!=56:raise ValueError('Truncated ELF map')
        kind,flags,position,address,physical,filesz,memsz,alignment=struct.unpack('<IIQQQQQQ',raw)
        if kind in (2,3):raise ValueError('External.SelectedDynamicInterpreterOrRuntimeClosureProtocol')
        if kind!=1:continue
        if (flags&~7 or not memsz or filesz>memsz or position>length or filesz>length-position
                or address>0x7fffffffffffffff or memsz>0x7fffffffffffffff-address or address%os.sysconf('SC_PAGE_SIZE')!=position%os.sysconf('SC_PAGE_SIZE')
                or alignment not in (0,1) and (alignment&(alignment-1) or address%alignment!=position%alignment)
                or any(address<other_end and other_start<address+memsz for other_start,other_end in loads)):
            raise ValueError('Incoherent ELF load segment')
        loads.append((address,address+memsz))
        if flags&1 and address<=entry and entry-address<filesz:executable_entry=True
    if not loads or not executable_entry:raise ValueError('Executable file-backed ELF entrypoint required')
    selected_io.check()


def _directory_tree(fd,depth,count):
    if depth>64:raise ValueError('Bounded immutable release directory depth')
    selected_io.check();_immutable(fd,True)
    with os.scandir(fd) as entries:
        for entry in entries:
            selected_io.check();count[0]+=1
            if count[0]>20000:raise ValueError('Bounded immutable release directory census')
            if entry.is_dir(follow_symlinks=False):
                child=os.open(entry.name,os.O_RDONLY|os.O_DIRECTORY|os.O_NOFOLLOW|os.O_CLOEXEC,dir_fd=fd)
                try:_directory_tree(child,depth+1,count)
                finally:os.close(child)


LISTENER_ROLES={'eventstore':'eventstore-owner','tenants':'tenants-owner','tenants-api':'tenants-api','parties':'parties-owner','conversations':'conversations-owner','agents':'agents-owner','agents-ui':'agents-ui-host'}
LISTENER_REQUIREMENT='External.IndependentlyAcceptedCompleteResourceListenersV1'


def listener_blockers(manifest,profile):
    """Exact accepted WebApplication role bindings. No port or actual installation is inferred."""
    try:
        bindings=profile.get('listeners')
        if not isinstance(bindings,dict) or set(bindings)!=set(LISTENER_ROLES):return [LISTENER_REQUIREMENT]
        observed=[]
        for resource,role in LISTENER_ROLES.items():
            value=bindings[resource]
            if resource=='agents-ui' and (not isinstance(value,dict) or 'uiHost' not in value):return [UI_HOST_REQUIREMENT]
            keys={'protocol','role','address','port','endpointName'}|({'uiHost'} if resource=='agents-ui' else set())
            if (not isinstance(value,dict) or set(value)!=keys
                    or value['protocol']!='AspNetCoreHttpListenerV1' or value['role']!=role
                    or value['address'] not in ('0.0.0.0','127.0.0.1','192.168.1.30')
                    or type(value['port']) is not int or not 1<=value['port']<=65535
                    or not re.fullmatch('[a-z][a-z0-9-]{0,62}',value['endpointName'])):return [LISTENER_REQUIREMENT+':'+resource]
            graphs=[graph for graph in manifest['resources'] if graph['resource']==resource]
            if len(graphs)!=1 or graphs[0].get('listener')!=value:return [LISTENER_REQUIREMENT+':AcceptedSubstitution:'+resource]
            if resource=='agents-ui' and ui_host_blockers(value['uiHost'],graphs[0]):return [UI_HOST_REQUIREMENT]
            if any(old['endpointName']==value['endpointName'] or old['port']==value['port'] and
                    (old['address']==value['address'] or '0.0.0.0' in (old['address'],value['address'])) for old in observed):return [LISTENER_REQUIREMENT+':ConflictingOwnership']
            observed.append(value)
        return []
    except (KeyError,TypeError,AttributeError):return [LISTENER_REQUIREMENT]


UI_HOST_REQUIREMENT='External.IndependentlyAcceptedAgentsUiHostListenerContractV1'


def _unique_object(pairs):
    value=dict(pairs)
    if len(value)!=len(pairs):raise ValueError('Duplicate UI host contract property')
    return value


def ui_host_blockers(host,graph):
    """Agents.UI is a hosted Razor library, not a seventh current WebApplication."""
    try:
        if not isinstance(host,dict) or set(host)!={'artifact','sha256','contract'}:return [UI_HOST_REQUIREMENT]
        value=host['contract']
        if (not isinstance(value,dict) or set(value)!={'protocol','entryPoint','uiComponentIdentity','frontComposerRegistrationIdentity','uiComponentArtifact','uiComponentSha256','frontComposerRegistrationArtifact','frontComposerRegistrationSha256','evidenceSha256','listenerArgument','listenerEnvironment'}
                or value['protocol']!='IndependentlyAcceptedFrontComposerUiHostHttpV1' or value['entryPoint']!=graph['entryPoint']
                or value['uiComponentIdentity']!='Hexalith.Agents.UI'
                or value['frontComposerRegistrationIdentity']!='Hexalith.Agents.UI.Composition.AgentsFrontComposerRegistration'
                or value['listenerArgument']!='--urls' or value['listenerEnvironment']!='ASPNETCORE_URLS'
                or not re.fullmatch('[0-9a-f]{64}',value['evidenceSha256'])):return [UI_HOST_REQUIREMENT]
        pins=[(host['artifact'],host['sha256']),(value['uiComponentArtifact'],value['uiComponentSha256']),(value['frontComposerRegistrationArtifact'],value['frontComposerRegistrationSha256'])]
        if len({pin[0] for pin in pins})!=3:return [UI_HOST_REQUIREMENT]
        for name,digest in pins:
            if not isinstance(name,str) or not 0<len(name)<=256 or '\0' in name or name.startswith('/') or any(part in ('','.','..') for part in name.split('/')) or not re.fullmatch('[0-9a-f]{64}',digest):return [UI_HOST_REQUIREMENT]
            files=[file for file in graph['files'] if file['path']==name]
            if len(files)!=1 or files[0]['sha256']!=digest or files[0]['bytes']<1 or name==host['artifact'] and files[0]['bytes']>65536:return [UI_HOST_REQUIREMENT]
        return []
    except (KeyError,TypeError,AttributeError):return [UI_HOST_REQUIREMENT]
