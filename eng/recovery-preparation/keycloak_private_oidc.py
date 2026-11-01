"""Private native Keycloak recovery check, selected by an authenticated caller.

No CLI, signing, credential creation or record promotion. The caller must bind
current source and freshly created target identities to its authentic policy,
provide authorization at every native boundary, and persist the returned result
create-only. Earlier incident-specific helpers remain unchanged.
"""
import argparse, base64, contextlib, datetime as dt, hashlib, json, os, pathlib, select, socket, stat, subprocess, time, urllib.error, urllib.parse, urllib.request

CHECKS = frozenset(['clientCredentialsLoginSucceeded','tokenRoleClaimsEqualLive','oidcDiscoveryAndJwksAllRealms','realmCountEqualsDb','clientCountEqualsDb','roleCountEqualsDb','userCountEqualsDb','realmNamesDigestEqualsDb'])

class Refusal(Exception): pass

def require(ok, message):
    if not ok: raise Refusal(message)

def digest(data): return hashlib.sha256(data).hexdigest()

def json_strict(data):
    def pairs(items):
        result={}
        for k,v in items:
            require(k not in result,'duplicate JSON key');result[k]=v
        return result
    return json.loads(data,object_pairs_hook=pairs,parse_constant=lambda _: (_ for _ in ()).throw(Refusal('nonfinite JSON')))

def private_bytes(path):
    p=pathlib.Path(path).absolute()
    for parent in reversed(p.parents):
        info=parent.lstat();mode=stat.S_IMODE(info.st_mode)
        require(stat.S_ISDIR(info.st_mode) and info.st_uid in (0,os.getuid()) and
                (not mode&0o022 or info.st_uid==0 and mode&stat.S_ISVTX),'private input parent custody changed')
    s=p.lstat()
    require(stat.S_ISREG(s.st_mode) and s.st_nlink==1 and s.st_uid==os.getuid()
            and stat.S_IMODE(s.st_mode)==0o600 and s.st_size<=4*1024*1024,'private input custody changed')
    fd=os.open(p,os.O_RDONLY|os.O_NOFOLLOW)
    try:
        opened=os.fstat(fd)
        require((s.st_dev,s.st_ino)==(opened.st_dev,opened.st_ino),'private input replaced')
        raw=bytearray()
        while len(raw)<=4*1024*1024:
            part=os.read(fd,min(65536,4*1024*1024-len(raw)+1))
            if not part:break
            raw.extend(part)
        end=os.fstat(fd)
        require(len(raw)==s.st_size and all(getattr(s,k)==getattr(end,k) for k in
                ('st_dev','st_ino','st_size','st_mtime_ns','st_ctime_ns','st_uid','st_mode','st_nlink')),
                'private input changed')
        return bytes(raw)
    finally:os.close(fd)

def claims_digest(tok):
    p = tok.split(".")[1]; c = json.loads(base64.urlsafe_b64decode(p + "=" * (-len(p) % 4)))
    shape = {"azp": c.get("azp"), "aud": sorted(c["aud"]) if isinstance(c.get("aud"), list) else c.get("aud"),
             "realmRoles": sorted((c.get("realm_access") or {}).get("roles", [])),
             "resourceRoles": {k: sorted(v.get("roles", [])) for k, v in sorted((c.get("resource_access") or {}).items())},
             "scope": sorted((c.get("scope") or "").split()), "typ": c.get("typ"), "issRealm": c.get("iss", "").rsplit("/", 1)[-1]}
    return hashlib.sha256(json.dumps(shape, sort_keys=True).encode()).hexdigest(), {"realmRoles": len(shape["realmRoles"]), "resourceClients": len(shape["resourceRoles"])}

def checked_claims(token):
    require(isinstance(token,str) and len(token.split('.'))==3,'malformed token')
    try:
        c=json_strict(base64.urlsafe_b64decode(token.split('.')[1]+'='*(-len(token.split('.')[1])%4)))
        require(isinstance(c,dict) and all(k in c for k in ['azp','aud','scope','typ','iss']) and c['iss'].rsplit('/',1)[-1]=='master','unknown token claims')
        require(isinstance(c['azp'],str) and isinstance(c['scope'],str) and isinstance(c['typ'],str),'malformed token claims')
        return claims_digest(token)
    except (ValueError,TypeError,KeyError,IndexError):raise Refusal('malformed token claims') from None

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs):raise Refusal('unexpected HTTP redirect')

OPENER=urllib.request.build_opener(urllib.request.ProxyHandler({}),NoRedirect())

def request(base,method,path,data=None,token=None,form=False,*,authorize,deadline):
    require(base in ['http://127.0.0.1:38080','http://127.0.0.1:28080'] and path.startswith('/') and not path.startswith('//'),'unexpected HTTP destination')
    require(method=='GET' or (method=='POST' and path=='/realms/master/protocol/openid-connect/token' and form),'non-read-only HTTP operation refused')
    require(authorize() is True and time.monotonic()<deadline,'current HTTP policy or budget refused')
    body=None;headers={}
    if form:body=urllib.parse.urlencode(data).encode();headers['Content-Type']='application/x-www-form-urlencoded'
    if token:headers['Authorization']='Bearer '+token
    req=urllib.request.Request(base+path,data=body,method=method,headers=headers)
    try:
        with OPENER.open(req,timeout=min(30,deadline-time.monotonic())) as response:
            require(response.status==200 and response.url==base+path,'unexpected HTTP response')
            require(response.headers.get_content_type()=='application/json','non-JSON HTTP response')
            raw=bytearray()
            while not response.isclosed():
                require(authorize() is True and time.monotonic()<deadline,'current HTTP policy or budget refused')
                # read1 performs one underlying receive, so a trickling peer
                # cannot refill a large read indefinitely. Clamp that receive
                # to the remaining whole-check budget as well.
                response.fp.raw._sock.settimeout(min(30,deadline-time.monotonic()))
                chunk=response.read1(min(65536,2*1024*1024-len(raw)+1))
                require(authorize() is True and time.monotonic()<deadline,'current HTTP policy or budget refused')
                if not chunk:break
                raw.extend(chunk);require(len(raw)<=2*1024*1024,'oversized HTTP response')
            require(authorize() is True and time.monotonic()<deadline,'current HTTP policy or budget refused')
            return response.status,json_strict(bytes(raw))
    except urllib.error.HTTPError as ex:
        require(not 300<=ex.code<400,'unexpected HTTP redirect');return ex.code,None

def kube(b,*args,input=None):
    require(b['authorize']() is True,'current policy authorization refused')
    require(time.monotonic()<b['deadline'],'native check budget expired')
    argv=[b['kubectl'],'--kubeconfig',b['kubeconfig'],'--context',b['context'],'--request-timeout=20s',*args]
    p=subprocess.run(argv,input=input,capture_output=True,timeout=min(45,b['deadline']-time.monotonic()))
    require(p.returncode==0 and len(p.stdout)<=4*1024*1024,'bounded native read failed')
    require(b['authorize']() is True and time.monotonic()<b['deadline'],'current native policy or budget refused')
    return p.stdout

def get(b,*args):return json_strict(kube(b,*args,'-o','json'))

def verify_native(b):
    config=json_strict(kube(b,'config','view','--raw','-o','json'))
    ctx=next(c['context'] for c in config['contexts'] if c['name']==b['context']);user=next(u['user'] for u in config['users'] if u['name']==ctx['user']);cluster=next(c['cluster'] for c in config['clusters'] if c['name']==ctx['cluster'])
    require(set(user)=={'client-certificate-data','client-key-data'} and set(cluster)=={'certificate-authority-data','server'} and cluster['server']=='https://192.168.1.30:6443','unsafe static native authentication')
    require(get(b,'get','namespace','kube-system')['metadata']['uid']==b['sourceClusterUid'],'source cluster changed')
    queries={'sourceService':('-n','keycloak','get','svc','keycloak'),'sourceApplication':('-n','keycloak','get','deploy','keycloak'),'targetApplication':('-n','recovery-4-0-kc','get','deploy','keycloak-restore-app'),'sourceNamespace':('get','ns','keycloak'),'targetNamespace':('get','ns','recovery-4-0-kc'),'sourceDatabase':('-n','keycloak','get','cluster.postgresql.cnpg.io','keycloak-postgres'),'targetDatabase':('-n','recovery-4-0-kc','get','cluster.postgresql.cnpg.io','keycloak-restore'),'sourceAdminClientSecret':('-n','keycloak','get','secret','keycloak-admin-api')}
    objects={}
    for name,args in queries.items():
        x=get(b,*args);require(x['metadata']['uid']==b['identities'][name]['uid'],'source or target identity changed');objects[name]=x
        if name.endswith('Application'):require(x.get('status',{}).get('readyReplicas')==x['spec']['replicas']==b['identities'][name]['replicas'],'application is not ready')
    require(objects['sourceDatabase']['status']['currentPrimary']==b['sourcePrimary']['name'],'source primary changed')
    require(get(b,'-n','keycloak','get','pod',b['sourcePrimary']['name'])['metadata']['uid']==b['sourcePrimary']['uid'],'source primary pod changed')
    return objects

def listener_owned(pid,port):
    found=[]
    for line in pathlib.Path('/proc/net/tcp').read_text().splitlines()[1:]:
        f=line.split()
        if int(f[1].split(':')[1],16)==port and f[3]=='0A':found.append((f[1].split(':')[0],f[9]))
    if len(found)!=1 or found[0][0]!='0100007F':return False
    expected='socket:['+found[0][1]+']'
    return any(os.readlink(p)==expected for p in pathlib.Path(f'/proc/{pid}/fd').iterdir())

def listener_absent(port):
    require(type(port) is int and 0<port<65536,'invalid private listener port')
    for table in ('tcp','tcp6'):
        with open('/proc/net/'+table,'rb') as source:
            raw=source.read(4*1024*1024+1)
        require(len(raw)<=4*1024*1024,'private listener census bound exceeded')
        for line in raw.decode('ascii').splitlines()[1:]:
            fields=line.split()
            require(len(fields)>=10,'private listener census malformed')
            # TIME_WAIT retains a TCP connection, but no process or listener.
            # Any actual listener on this port, including wildcard/IPv6 or an
            # unrelated owner, prevents startup and successful cleanup.
            if int(fields[1].rsplit(':',1)[1],16)==port and fields[3]=='0A':
                return False
    return True

@contextlib.contextmanager
def forward(b,ns,target,port,tracker):
    require(listener_absent(port),'unexpected existing listener')
    tracked={'port':port,'pid':None,'started':False,'terminationConfirmed':False,'loopbackListenerAbsent':False};tracker.append(tracked)
    p=None
    try:
        require(b['authorize']() is True and time.monotonic()<b['deadline'],'current policy authorization refused')
        argv=[b['kubectl'],'--kubeconfig',b['kubeconfig'],'--context',b['context'],'-n',ns,'port-forward','--address','127.0.0.1',target,f'{port}:8080']
        p=subprocess.Popen(argv,stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
        tracked.update(pid=p.pid,started=True)
        deadline=min(time.monotonic()+30,b['deadline']);ready=False;output=bytearray()
        os.set_blocking(p.stdout.fileno(),False)
        while time.monotonic()<deadline and p.poll() is None:
            if select.select([p.stdout],[],[],0.2)[0]:
                chunk=os.read(p.stdout.fileno(),4096)
                require(chunk and len(output)+len(chunk)<=8192,'private forward output refused')
                output.extend(chunk)
                if f'Forwarding from 127.0.0.1:{port} -> 8080\n'.encode() in output:ready=True;break
        require(ready and p.poll() is None and listener_owned(p.pid,port),'private forward/listener identity failed')
        yield
    finally:
        if p is not None and p.poll() is None:
            p.terminate()
            try:p.wait(10)
            except subprocess.TimeoutExpired:p.kill();p.wait(5)
        if p is not None:p.stdout.close()
        tracked['terminationConfirmed']=p is None or p.poll() is not None
        tracked['loopbackListenerAbsent']=listener_absent(port)
        require(tracked['terminationConfirmed'] and tracked['loopbackListenerAbsent'],'private forward cleanup uncertain')

def read_human_count(b,ns,pod):
    sql="BEGIN READ ONLY; SELECT count(*) FROM user_entity WHERE service_account_client_link IS NULL; ROLLBACK;\n"
    raw=kube(b,'-n',ns,'exec','-i',pod,'-c','postgres','--','psql','-X','-qAt','-d','keycloak','-v','ON_ERROR_STOP=1','-f','-',input=sql.encode())
    require(raw.strip().isdigit(),'malformed read-only count');return int(raw.strip())

def existing_verifier_secret(b):
    sql="BEGIN READ ONLY; SELECT json_build_object('matches',count(*),'secret',min(c.secret)) FROM client c JOIN realm r ON r.id=c.realm_id WHERE r.name='master' AND c.client_id='story-4-0-verifier'; ROLLBACK;\n"
    value=json_strict(kube(b,'-n','recovery-4-0-kc','exec','-i','keycloak-restore-1','-c','postgres','--','psql','-X','-qAt','-d','keycloak','-v','ON_ERROR_STOP=1','-f','-',input=sql.encode()))
    require(set(value)=={'matches','secret'} and value['matches']==1 and isinstance(value['secret'],str) and value['secret'],'existing isolated verifier credential unavailable')
    return value['secret']

def acceptance(checks):
    require(set(checks)==CHECKS and all(type(v) is bool for v in checks.values()),'unknown or malformed acceptance fields')
    return all(checks.values())

def exit_code(record):return 0 if record.get('allPass') is True and record.get('portForwardsClosed') is True and acceptance(record['checks']) else 1

def investigate(b,progress):
    def checked_request(*args,**kwargs):
        require(b['authorize']() is True and time.monotonic()<b['deadline'],'current HTTP policy or budget refused')
        result=request(*args,**kwargs,authorize=b['authorize'],deadline=b['deadline'])
        require(b['authorize']() is True and time.monotonic()<b['deadline'],'current HTTP policy or budget refused')
        return result
    objects=verify_native(b);sec=objects['sourceAdminClientSecret']['data']
    cid=base64.b64decode(sec['client-id']).decode().strip();csec=base64.b64decode(sec['client-secret']).decode().strip();require(cid and csec,'source client credential unavailable');sec=None;objects=None
    vsec=existing_verifier_secret(b)
    src_human=read_human_count(b,'keycloak',b['sourcePrimary']['name']);rst_human=read_human_count(b,'recovery-4-0-kc','keycloak-restore-1')
    db=json_strict(private_bytes(pathlib.Path(b['bundle'])/'keycloak/restore-verification-db.json'))['restoredInventory'];out=progress;out.update({'checkedAt':dt.datetime.now(dt.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),'target':'recovery-4-0-kc/keycloak-restore-app via explicit loopback-only native forward','liveReferenceEndpoint':b['sourceEndpoint'],'readOnlyRecoveryCheck':True,'recoveryAccepted':False})
    with forward(b,'keycloak','svc/keycloak',38080,progress['forwards']),forward(b,'recovery-4-0-kc','deploy/keycloak-restore-app',28080,progress['forwards']):
        s,lt=checked_request(b['sourceEndpoint'],'POST','/realms/master/protocol/openid-connect/token',{'grant_type':'client_credentials','client_id':cid,'client_secret':csec},form=True)
        out['liveReferenceHttp']=s;require(s==200 and isinstance(lt,dict) and 'access_token' in lt,'live reference unavailable');live_digest,live_shape=checked_claims(lt['access_token']);lt=None
        s,t=checked_request(b['targetEndpoint'],'POST','/realms/master/protocol/openid-connect/token',{'grant_type':'client_credentials','client_id':cid,'client_secret':csec},form=True);csec=None
        out['restoredReferenceHttp']=s
        require(s==200 and isinstance(t,dict) and 'access_token' in t,'restored reference unavailable');rest_digest,rest_shape=checked_claims(t['access_token']);t=None
        out['clientCredentialsLogin']={'client':'keycloak-admin-api client (id not recorded)','realm':'master','httpStatus':s,'tokenIssued':s==200,'claimsShapeDigest':rest_digest,'liveClaimsShapeDigest':live_digest,'claimShape':rest_shape}
        s,vt=checked_request(b['targetEndpoint'],'POST','/realms/master/protocol/openid-connect/token',{'grant_type':'client_credentials','client_id':'story-4-0-verifier','client_secret':vsec},form=True);vsec=None
        out['verifierLoginHttp']=s;require(s==200 and isinstance(vt,dict) and isinstance(vt.get('access_token'),str),'existing verifier login failed');tok=vt['access_token'];vt=None
        s,realms=checked_request(b['targetEndpoint'],'GET','/admin/realms',token=tok);require(s==200 and isinstance(realms,list) and realms,'realm inventory unavailable');names=sorted(r['realm'] for r in realms);require(len(names)==len(set(names)),'duplicate realm')
        rc=[];users=roles=clients_total=0;client_pairs=[]
        for realm in names:
            rq=urllib.parse.quote(realm,safe='');s,d=checked_request(b['targetEndpoint'],'GET',f'/realms/{rq}/.well-known/openid-configuration');require(s==200 and isinstance(d,dict),'discovery unavailable');disc_ok=d.get('issuer','').endswith('/realms/'+realm) and 'token_endpoint' in d
            sj,jw=checked_request(b['targetEndpoint'],'GET',f'/realms/{rq}/protocol/openid-connect/certs');require(sj==200 and isinstance(jw,dict) and isinstance(jw.get('keys'),list),'JWKS unavailable')
            s1,cl=checked_request(b['targetEndpoint'],'GET',f'/admin/realms/{rq}/clients?max=10000',token=tok);s2,rr=checked_request(b['targetEndpoint'],'GET',f'/admin/realms/{rq}/roles?max=10000',token=tok);s3,uc=checked_request(b['targetEndpoint'],'GET',f'/admin/realms/{rq}/users/count',token=tok)
            require([s1,s2,s3]==[200,200,200] and isinstance(cl,list) and isinstance(rr,list) and type(uc) is int and uc>=0,'admin inventory unavailable');croles=0
            for c in cl:
                s4,cr=checked_request(b['targetEndpoint'],'GET',f'/admin/realms/{rq}/clients/{urllib.parse.quote(c["id"],safe="")}/roles?max=10000',token=tok);require(s4==200 and isinstance(cr,list),'client role inventory unavailable');croles+=len(cr);client_pairs.append(f'{realm}/{c["clientId"]}')
            clients_total+=len(cl);roles+=len(rr)+croles;users+=uc;rc.append({'discoveryOk':disc_ok,'jwksKeys':len(jw['keys']),'adminHttp':[s1,s2,s3],'clients':len(cl),'realmRoles':len(rr),'clientRoles':croles,'users':uc})
        tok=None
    verifier_extra=sum(1 for p in client_pairs if p=='master/story-4-0-verifier');require(verifier_extra==1,'existing isolated verifier identity mismatch')
    out['perRealm']=rc;out['api']={'realms':len(names),'clients':clients_total,'roles':roles,'users':users,'temporaryVerifierClients':verifier_extra,'realmNamesDigest':digest('\n'.join(names).encode())}
    out['checks']={'clientCredentialsLoginSucceeded':out['clientCredentialsLogin']['tokenIssued'],'tokenRoleClaimsEqualLive':rest_digest is not None and rest_digest==live_digest,'oidcDiscoveryAndJwksAllRealms':all(x['discoveryOk'] and x['jwksKeys']>0 for x in rc) and len(rc)>0,'realmCountEqualsDb':len(names)==db['counts']['realms'],'clientCountEqualsDb':clients_total-verifier_extra==db['counts']['clients'],'roleCountEqualsDb':roles==db['counts']['roles'],'userCountEqualsDb':users==src_human==rst_human,'realmNamesDigestEqualsDb':out['api']['realmNamesDigest']==db['realmNamesDigest']}
    out['allPass']=acceptance(out['checks']);out['userCountBasis']={'sourceNonServiceAccountUsers':src_human,'restoredNonServiceAccountUsers':rst_human,'adminApiUsers':users};verify_native(b)
    return out


def check(binding, authorize, *, budget_seconds=180):
    """Return all eight original checks after exact current identity guards."""
    require(callable(authorize) and authorize() is True, 'current policy authorization refused')
    require(type(budget_seconds) is int and 0<budget_seconds<=300,'invalid check budget')
    b=dict(binding);b['authorize']=authorize;b['deadline']=time.monotonic()+budget_seconds
    require(b['context']=='jpiquot@local' and b['sourceEndpoint']=='http://127.0.0.1:38080'
            and b['targetEndpoint']=='http://127.0.0.1:28080'
            and b['kubeconfig']=='/home/administrator/.kube/hexalith-production'
            and b['kubectl']=='/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl',
            'unexpected native endpoint')
    progress={'forwards':[]}
    try:
        result=investigate(b,progress)
    except Exception as ex:
        result=dict(progress)
        result.update({'allPass':False,'checks':{k:False for k in sorted(CHECKS)},
                       'failure':str(ex) if isinstance(ex,Refusal) else type(ex).__name__,
                       'recoveryAccepted':False})
    closed=all(row['terminationConfirmed'] and row['loopbackListenerAbsent'] for row in progress['forwards'])
    result['portForwardsClosed']=closed
    result['allPass']=result.get('allPass') is True and closed and acceptance(result['checks'])
    require(authorize() is True, 'current policy authorization refused')
    return result
