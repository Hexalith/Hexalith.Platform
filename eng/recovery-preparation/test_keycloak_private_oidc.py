"""Private OIDC acceptance and refusal tests with inert native/HTTP boundaries."""
import base64
import contextlib
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import time
from types import SimpleNamespace
import unittest
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('current_oidc',Path(__file__).with_name('keycloak_private_oidc.py'))
p=importlib.util.module_from_spec(spec);spec.loader.exec_module(p)


def token(roles):
    value={'azp':'same-client','aud':['realm-management'],'scope':'profile email','typ':'Bearer',
           'iss':'http://localhost/realms/master','resource_access':{'realm-management':{'roles':roles}}}
    return 'header.'+base64.urlsafe_b64encode(json.dumps(value).encode()).decode().rstrip('=')+'.signature'


def binding():
    return {'context':'jpiquot@local','sourceEndpoint':'http://127.0.0.1:38080',
            'targetEndpoint':'http://127.0.0.1:28080','kubectl':'/home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl',
            'kubeconfig':'/home/administrator/.kube/hexalith-production','sourceClusterUid':'synthetic-source',
            'bundle':'/unused-synthetic','sourcePrimary':{'name':'synthetic-primary'}}


class Checks(unittest.TestCase):
    def run_check(self,kind='pass'):
        objects={'sourceAdminClientSecret':{'data':{'client-id':base64.b64encode(b'synthetic-client').decode(),
                                                  'client-secret':base64.b64encode(b'synthetic-secret').decode()}}}
        calls=[];authority={'valid':True};clock={'now':0.0}
        @contextlib.contextmanager
        def forward(b,ns,target,port,tracker):
            state={'terminationConfirmed':False,'loopbackListenerAbsent':False};tracker.append(state)
            try:yield
            finally:state.update(terminationConfirmed=True,loopbackListenerAbsent=kind!='cleanup')
        def request(base,method,path,data=None,token=None,form=False,**kwargs):
            calls.append((base,method,path))
            if kind=='expired-policy':authority['valid']=False
            if kind=='expired-budget':clock['now']=181.0
            if kind=='live-unavailable' and base.endswith('38080'):return 404,None
            if path.endswith('/token'):
                roles=['manage-realm'] if kind=='claims' and base.endswith('28080') else ['view-realm']
                return 200,{'access_token':globals()['token'](roles)}
            if path=='/admin/realms':return 200,[{'realm':'master'}]
            if path.endswith('openid-configuration'):return 200,{'issuer':'http://isolated/realms/master','token_endpoint':'local'}
            if path.endswith('/certs'):return 200,{'keys':[{}]}
            if '/clients?' in path:return 200,[{'id':'one','clientId':'application'},{'id':'two','clientId':'story-4-0-verifier'}]
            if path.endswith('/roles') or '/roles?' in path:return 200,[]
            if path.endswith('/users/count'):return 200,2
            raise AssertionError('unexpected inert HTTP request')
        db={'restoredInventory':{'counts':{'realms':1,'clients':1,'roles':0},
                                 'realmNamesDigest':hashlib.sha256(b'master').hexdigest()}}
        with patch.object(p,'verify_native',return_value=objects), \
                patch.object(p,'existing_verifier_secret',return_value='synthetic-existing'), \
                patch.object(p,'read_human_count',return_value=2), \
                patch.object(p,'private_bytes',return_value=json.dumps(db).encode()), \
                patch.object(p,'forward',side_effect=forward),patch.object(p,'request',side_effect=request), \
                patch.object(p.subprocess,'run',side_effect=AssertionError('no child dispatch')), \
                patch.object(p.subprocess,'Popen',side_effect=AssertionError('no forward dispatch')), \
                patch.object(p.time,'monotonic',side_effect=lambda:clock['now']):
            try:result=p.check(binding(),lambda:authority['valid'])
            except p.Refusal:
                if kind!='expired-policy':raise
                result={'allPass':False,'policyExpired':True}
        return result,calls

    def test_all_eight_original_acceptance_checks_pass_with_private_same_client_reference(self):
        result,calls=self.run_check()
        self.assertTrue(result['allPass']);self.assertTrue(result['portForwardsClosed'])
        self.assertEqual(set(result['checks']),p.CHECKS)
        self.assertTrue(all(type(value) is bool and value for value in result['checks'].values()))
        self.assertFalse(result['recoveryAccepted'])
        self.assertTrue(all(base in (binding()['sourceEndpoint'],binding()['targetEndpoint']) for base,_,_ in calls))
        self.assertEqual(calls[0],(binding()['sourceEndpoint'],'POST','/realms/master/protocol/openid-connect/token'))
        self.assertEqual(p.exit_code(result),0)

    def test_claims_mismatch_is_nonzero_without_weakening_other_checks(self):
        result,_=self.run_check('claims')
        self.assertFalse(result['checks']['tokenRoleClaimsEqualLive'])
        self.assertEqual(sum(result['checks'].values()),7)
        self.assertEqual(p.exit_code(result),1)

    def test_missing_live_reference_stops_before_any_target_http_and_returns_failure(self):
        result,calls=self.run_check('live-unavailable')
        self.assertEqual(result['liveReferenceHttp'],404);self.assertFalse(result['allPass'])
        self.assertEqual(len(calls),1);self.assertEqual(p.exit_code(result),1)
        self.assertTrue(result['portForwardsClosed'])

    def test_uncertain_forward_cleanup_is_failure_even_if_every_acceptance_check_passes(self):
        result,_=self.run_check('cleanup')
        self.assertFalse(result['portForwardsClosed']);self.assertFalse(result['allPass'])
        self.assertEqual(p.exit_code(result),1)

    def test_original_claim_shape_algorithm_has_fixed_unchanged_digest(self):
        claims={'azp':'same-client','aud':['realm-management'],'realmRoles':[],
                'resourceRoles':{'realm-management':['view-realm']},'scope':['email','profile'],
                'typ':'Bearer','issRealm':'master'}
        self.assertEqual(p.checked_claims(token(['view-realm']))[0],
                         hashlib.sha256(json.dumps(claims,sort_keys=True).encode()).hexdigest())

    def test_unknown_or_nonboolean_acceptance_and_malformed_claims_refuse(self):
        for fields in ({k:True for k in p.CHECKS}|{'unknown':True},{k:'true' for k in p.CHECKS}):
            with self.assertRaises(p.Refusal):p.acceptance(fields)
        for value in (None,'x','header.e30.signature'):
            with self.assertRaises(p.Refusal):p.checked_claims(value)

    def test_current_policy_refusal_precedes_every_credential_native_or_http_access(self):
        with patch.object(p,'investigate') as run:
            with self.assertRaises(p.Refusal):p.check(binding(),lambda:False)
            run.assert_not_called()
        with patch.object(p.subprocess,'run') as run:
            with self.assertRaises(p.Refusal):p.kube({'authorize':lambda:False},'get','unused')
            run.assert_not_called()

    def test_changed_endpoint_or_configuration_refuses_before_investigation(self):
        for key,value in [('sourceEndpoint','https://public.invalid'),('context','other'),
                          ('kubeconfig','/tmp/untrusted'),('kubectl','executable-hook')]:
            changed=binding();changed[key]=value
            with self.subTest(key=key),patch.object(p,'investigate') as run:
                with self.assertRaises(p.Refusal):p.check(changed,lambda:True)
                run.assert_not_called()

    def test_redirect_and_unapproved_http_mutation_never_dispatch(self):
        with self.assertRaises(p.Refusal):p.NoRedirect().redirect_request(None,None,302,None,None,'http://public.invalid')
        for base,method,path in [('https://public.invalid','GET','/'),('http://127.0.0.1:38080','DELETE','/admin/realms'),
                                 ('http://127.0.0.1:28080','POST','/admin/realms')]:
            with patch.object(p.OPENER,'open') as request:
                with self.assertRaises(p.Refusal):p.request(base,method,path,authorize=lambda:True,deadline=time.monotonic()+5)
                request.assert_not_called()

    def test_changed_source_cluster_stops_before_source_or_target_credentials(self):
        config={'contexts':[{'name':'jpiquot@local','context':{'user':'test','cluster':'test'}}],
                'users':[{'name':'test','user':{'client-certificate-data':'synthetic','client-key-data':'synthetic'}}],
                'clusters':[{'name':'test','cluster':{'certificate-authority-data':'synthetic','server':'https://192.168.1.30:6443'}}]}
        with patch.object(p,'kube',return_value=json.dumps(config).encode()), \
                patch.object(p,'get',return_value={'metadata':{'uid':'replaced-source'}}) as get:
            with self.assertRaisesRegex(p.Refusal,'source cluster changed'):p.verify_native(binding())
            self.assertEqual(get.call_count,1)

    def test_policy_and_overall_budget_expiry_stop_before_next_http_request(self):
        for kind in ('expired-policy','expired-budget'):
            result,calls=self.run_check(kind)
            self.assertFalse(result['allPass']);self.assertEqual(len(calls),1)

    def test_unknown_forward_cleanup_never_returns_success(self):
        value={'allPass':True,'checks':{key:True for key in p.CHECKS}}
        self.assertEqual(p.exit_code(value),1)
        value['portForwardsClosed']=None
        self.assertEqual(p.exit_code(value),1)

    def test_forward_setup_failure_terminates_and_reaps_real_owned_inert_child(self):
        with socket.socket() as sock:
            sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
        real_popen=subprocess.Popen;children=[];tracker=[]
        def spawn(*args,**kwargs):
            child=real_popen([sys.executable,'-I','-S','-c','import time; time.sleep(30)'],
                             stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
            children.append(child);return child
        current=binding();current.update(authorize=lambda:True,deadline=time.monotonic()+5)
        try:
            with patch.object(p.subprocess,'Popen',side_effect=spawn), \
                    patch.object(p.os,'set_blocking',side_effect=OSError('synthetic setup refusal')):
                with self.assertRaises(OSError):
                    with p.forward(current,'keycloak','svc/keycloak',port,tracker):pass
            self.assertEqual(len(children),1);self.assertIsNotNone(children[0].poll())
            self.assertTrue(tracker[0]['terminationConfirmed']);self.assertTrue(tracker[0]['loopbackListenerAbsent'])
        finally:
            for child in children:
                if child.poll() is None:child.kill();child.wait(5)

    def test_private_input_replacement_with_symlink_refuses_without_following_it(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'input';target=Path(directory)/'other'
            path.write_bytes(b'approved');path.chmod(0o600);target.write_bytes(b'unrelated');target.chmod(0o600)
            original_open=os.open
            def replace_then_open(name,flags,*args,**kwargs):
                path.unlink();path.symlink_to(target)
                return original_open(name,flags,*args,**kwargs)
            with patch.object(p.os,'open',side_effect=replace_then_open):
                with self.assertRaises((p.Refusal,OSError)):p.private_bytes(path)
            self.assertEqual(target.read_bytes(),b'unrelated')

    def test_slow_http_chunks_recheck_budget_and_authority_before_another_receive(self):
        for cause in ('budget','authority','size'):
            clock={'now':0.0};authority={'valid':True};reads=[];timeouts=[]
            class Response:
                status=200;url='http://127.0.0.1:38080/realms/master/test'
                headers=SimpleNamespace(get_content_type=lambda:'application/json')
                fp=SimpleNamespace(raw=SimpleNamespace(_sock=SimpleNamespace(settimeout=timeouts.append)))
                def __enter__(self):return self
                def __exit__(self,*args):return False
                def isclosed(self):return False
                def read1(self,size):
                    reads.append(size)
                    if cause=='budget':clock['now']=5.0
                    if cause=='authority':authority['valid']=False
                    return b'x'*size if cause=='size' else b'{'
            with self.subTest(cause=cause),patch.object(p.OPENER,'open',return_value=Response()), \
                    patch.object(p.time,'monotonic',side_effect=lambda:clock['now']):
                with self.assertRaises(p.Refusal):
                    p.request('http://127.0.0.1:38080','GET','/realms/master/test',
                              authorize=lambda:authority['valid'],deadline=4.0)
            if cause!='size':self.assertEqual(len(reads),1)
            else:self.assertEqual(sum(reads),2*1024*1024+1)
            self.assertTrue(all(value<=4.0 for value in timeouts))

if __name__=='__main__':unittest.main()
