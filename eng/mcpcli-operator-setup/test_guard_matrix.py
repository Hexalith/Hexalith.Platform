"""Table-driven refusal coverage from a complete valid control, without external calls."""
import contextlib
import copy
from datetime import datetime, timedelta, timezone
import io
import json
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import prepare
import seed
from guard_fixtures import Control
from test_seed import plan, TENANT, Transport, Response


def set_value(document, path, value):
    for key in path[:-1]: document = document[key]
    document[path[-1]] = value


class GuardMatrixTests(unittest.TestCase):
    def assert_refusal(self, code, action):
        with self.assertRaises(ValueError) as caught: action()
        self.assertEqual(code, str(caught.exception))

    def run_prepare(self, control):
        control.save_profile()
        return prepare.prepare(control.profile_path, control.graph, control.output, 'helm', 'skopeo', root=control.root)

    def test_valid_control_reaches_review_without_real_external_calls(self):
        with Control() as c, patch.object(prepare,'subprocess_git_revision',return_value='a'*40), \
             patch.object(prepare,'run',side_effect=c.fake_run), patch.object(prepare.subprocess,'run') as external, \
             patch.object(seed,'build_opener') as network:
            result = self.run_prepare(c)
            self.assertEqual('review-required',result['status']); self.assertFalse(result['applyAuthorized'])
            self.assertEqual({c.image},set(result['imagesPulled'])); self.assertTrue((c.output/'review.json').is_file())
            external.assert_not_called(); network.assert_not_called()

    def test_profile_field_source_target_expiry_and_scope_refusals(self):
        future = lambda hours:(datetime.now(timezone.utc)+timedelta(hours=hours)).isoformat()
        cases = [
            ('extra-profile-field',(),None,'profile-fields-invalid'),
            ('schema',('schema',),'unknown','profile-schema-invalid'),
            ('source-shape',('sourceRevision',),'short','source-revision-required'),
            ('source-drift',('sourceRevision',),'b'*40,'source-revision-drift'),
            ('source-hashes',('sourceHashes',),{},'source-inputs-drift-or-unbound'),
            ('graph-hash',('canonicalGraphSha256',),'0'*64,'canonical-graph-hash-mismatch'),
            ('target-fields',('target','extra'),'unexpected','target-fields-invalid'),
            ('context',('target','context'),'other','unapproved-target'),
            ('api',('target','api'),'https://192.168.1.31:6443','unapproved-target'),
            ('namespace',('target','namespace'),'other-app','task-identity-required'),
            ('release',('target','release'),'other-app','task-identity-required'),
            ('port-low',('target','localPort'),1023,'private-port-invalid'),
            ('port-high',('target','localPort'),65536,'private-port-invalid'),
            ('port-boolean',('target','localPort'),True,'private-port-invalid'),
            ('expired',('expiresAt',),future(-1),'expiry-must-be-within-24-hours'),
            ('too-far',('expiresAt',),future(25),'expiry-must-be-within-24-hours'),
            ('no-timezone',('expiresAt',),datetime.now().isoformat(),'expiry-must-be-within-24-hours'),
            ('tenant',('tenant',),'shared-tenant','task-tenant-required'),
            ('subject',('operatorSubject',),'','scoped-writer-role-required'),
            ('role',('operatorRole',),'TenantOwner','scoped-writer-role-required'),
            ('permissions',('operatorPermissions',),['query:read'],'operator-scope-invalid'),
            ('domains',('operatorDomains',),['party','global-administrators'],'operator-scope-invalid'),
            ('prerequisites',('prerequisites',),{},'prerequisites-required'),
            ('missing-binding',('prerequisites','writerActivation'),'','prerequisite-bindings-required'),
            ('placeholder-binding',('prerequisites','writerActivation'),'TODO','prerequisite-bindings-required'),
            ('images',('values','images'),{},'complete-image-and-input-bindings-required'),
            ('bindings',('values','bindings'),{},'complete-image-and-input-bindings-required'),
            ('tag-image',('values','images','eventstore/gateway'),'registry.internal/app:latest','real-immutable-image-required'),
            ('fake-image',('values','images','eventstore/gateway'),'qualification.invalid/app@sha256:'+'a'*64,'real-immutable-image-required'),
            ('mode',('values','mode'),'recovery','single-replica-normal-mode-required'),
            ('replicas',('values','replicas'),2,'single-replica-normal-mode-required'),
            ('capacity',('values','capacityProfile'),'another-campaign','single-replica-normal-mode-required')]
        for name,path,value,code in cases:
            with self.subTest(case=name), Control() as c, patch.object(prepare,'subprocess_git_revision',return_value='a'*40), \
                 patch.object(prepare,'run',side_effect=c.fake_run) as runner, patch.object(prepare.subprocess,'run') as external, \
                 patch.object(seed,'build_opener') as network:
                if not path: c.profile['extra']=None
                else: set_value(c.profile,path,value)
                self.assert_refusal(code,lambda:self.run_prepare(c))
                runner.assert_not_called(); external.assert_not_called(); network.assert_not_called()
                self.assertFalse(c.output.exists())

    def test_graph_parity_and_closure_refusals(self):
        for case,code in [('template','task-chart-template-drift'),('graph','canonical-chart-graph-mismatch'),
                          ('missing-workload','complete-eight-workload-closure-required'),('duplicate-workload','complete-eight-workload-closure-required')]:
            with self.subTest(case=case), Control() as c, patch.object(prepare,'subprocess_git_revision',return_value='a'*40), \
                 patch.object(prepare,'run') as runner, patch.object(prepare.subprocess,'run') as external:
                if case=='template':
                    template=c.root/'eng/mcpcli-operator-setup/chart/Chart.yaml';template.write_text(template.read_text()+'# changed\n');c.rebind_source()
                else:
                    graph=prepare.read(c.graph)
                    if case=='graph':
                        alternate=c.directory/'alternate.json';graph['schema']='changed';alternate.write_text(json.dumps(graph));c.graph=alternate
                        c.profile['canonicalGraphSha256']=prepare.sha(c.graph)
                    else:
                        if case=='missing-workload': graph['workloads'].pop()
                        else: graph['workloads'].append(copy.deepcopy(graph['workloads'][0]))
                        c.graph.write_text(json.dumps(graph));c.rebind_source()
                self.assert_refusal(code,lambda:self.run_prepare(c))
                runner.assert_not_called(); external.assert_not_called(); self.assertFalse(c.output.exists())

    def test_private_file_constraints_refuse_before_tools_or_output(self):
        for field in ('registryAuthFile','privateSeedFile'):
            for kind in ('missing','relative','directory','symlink','public','inside-root'):
                code=('private-file-location-or-mode-invalid:' if kind in ('public','inside-root') else 'private-file-unavailable:')+field
                with self.subTest(field=field,kind=kind), Control() as c, patch.object(prepare,'subprocess_git_revision',return_value='a'*40), \
                     patch.object(prepare,'run') as runner, patch.object(prepare.subprocess,'run') as external:
                    target=Path(c.profile[field])
                    if kind=='missing': c.profile[field]=str(c.directory/'absent')
                    elif kind=='relative': c.profile[field]='relative-private.json'
                    elif kind=='directory': c.profile[field]=str(c.directory)
                    elif kind=='symlink':
                        link=c.directory/'private-link';link.symlink_to(target);c.profile[field]=str(link)
                    elif kind=='public': target.chmod(0o644)
                    else:
                        inside=c.root/'private.json';inside.write_text(target.read_text());inside.chmod(0o600);c.profile[field]=str(inside)
                    self.assert_refusal(code,lambda:self.run_prepare(c))
                    runner.assert_not_called(); external.assert_not_called(); self.assertFalse(c.output.exists())
        with Control() as c, patch.object(prepare,'subprocess_git_revision',return_value='a'*40), patch.object(prepare,'run') as runner:
            c.profile['registryAuthFile']=c.profile['privateSeedFile']
            self.assert_refusal('private-authorities-must-be-separated',lambda:self.run_prepare(c));runner.assert_not_called()
            self.assertFalse(c.output.exists())

    def test_environment_and_secret_boundaries_refuse_before_tools_or_output(self):
        cases=[
            ('empty',lambda c:c.profile.update(environmentManifests=[]),'task-environment-manifests-required'),
            ('cluster',lambda c:c.profile['environmentManifests'][0].update(kind='ClusterRole'),'external-or-cluster-resource-refused'),
            ('namespace',lambda c:c.profile['environmentManifests'][0]['metadata'].update(namespace='other-app'),'external-or-cluster-resource-refused'),
            ('label',lambda c:c.profile['environmentManifests'][0]['metadata']['labels'].clear(),'duplicate-or-unowned-environment-resource'),
            ('duplicate',lambda c:c.profile['environmentManifests'].append(copy.deepcopy(c.profile['environmentManifests'][0])),'duplicate-or-unowned-environment-resource'),
            ('owner',lambda c:c.profile['environmentManifests'][0]['metadata'].update(ownerReferences=[]),'external-resource-owner-refused'),
            ('finalizer',lambda c:c.profile['environmentManifests'][0]['metadata'].update(finalizers=['other-owner']),'external-resource-owner-refused'),
            ('public-service',lambda c:c.profile['environmentManifests'][2]['spec'].update(type='LoadBalancer'),'public-service-refused'),
            ('storage-bound',lambda c:c.profile['environmentManifests'][1]['spec']['resources']['requests'].clear(),'bounded-storage-required'),
            ('no-storage',lambda c:c.profile['environmentManifests'].pop(1),'task-owned-persistent-storage-required'),
            ('no-component-files',lambda c:c.profile['values']['environment'].update(componentConfigMap='missing'),'component-file-configmap-required'),
            ('replicas',lambda c:c.profile['environmentManifests'][3]['spec'].update(replicas=2),'unsafe-data-workload'),
            ('host-network',lambda c:c.profile['environmentManifests'][3]['spec']['template']['spec'].update(hostNetwork=True),'unsafe-data-workload'),
            ('host-pid',lambda c:c.profile['environmentManifests'][3]['spec']['template']['spec'].update(hostPID=True),'unsafe-data-workload'),
            ('host-mount',lambda c:c.profile['environmentManifests'][3]['spec']['template']['spec']['volumes'].append({'hostPath':{'path':'/etc'}}),'host-mount-refused'),
            ('data-image',lambda c:c.profile['environmentManifests'][3]['spec']['template']['spec']['containers'][0].update(image='data:latest'),'data-image-digest-required'),
            ('data-resources',lambda c:c.profile['environmentManifests'][3]['spec']['template']['spec']['containers'][0]['resources']['limits'].pop('memory'),'bounded-data-resources-required'),
            ('privileged',lambda c:c.profile['environmentManifests'][3]['spec']['template']['spec']['containers'][0]['securityContext'].update(privileged=True),'privileged-workload-refused'),
            ('literal-env',lambda c:c.profile['environmentManifests'][3]['spec']['template']['spec']['containers'][0]['env'].append({'name':'API_TOKEN','value':'do-not-retain'}),'literal-environment-credential-refused'),
            ('secret-name',lambda c:c.profile['privateSecretFiles'].update({'INVALID':{'token':str(c.secret)}}),'private-secret-reference-invalid'),
            ('secret-keys',lambda c:c.profile['privateSecretFiles'].update({'empty':{}}),'private-secret-reference-invalid'),
            ('secret-key',lambda c:c.profile['privateSecretFiles'].update({'other':{'bad/key':str(c.secret)}}),'private-secret-file-unavailable-or-public'),
            ('secret-missing',lambda c:c.profile['privateSecretFiles']['task-channel'].update(token=str(c.directory/'missing')),'private-secret-file-unavailable-or-public'),
            ('secret-relative',lambda c:c.profile['privateSecretFiles']['task-channel'].update(token='relative-token'),'private-secret-file-unavailable-or-public'),
            ('secret-public',lambda c:c.secret.chmod(0o644),'private-secret-file-unavailable-or-public')]
        for key in ('password','SigningKey','PrivateKey','token','credential'):
            cases.append(('configmap-'+key,lambda c,key=key:c.profile['environmentManifests'][0]['data'].update({key:'do-not-retain'}),'configmap-credential-refused'))
        for name,mutate,code in cases:
            with self.subTest(case=name), Control() as c, patch.object(prepare,'subprocess_git_revision',return_value='a'*40), \
                 patch.object(prepare,'run') as runner, patch.object(prepare.subprocess,'run') as external, patch.object(seed,'build_opener') as network:
                mutate(c);self.assert_refusal(code,lambda:self.run_prepare(c))
                runner.assert_not_called();external.assert_not_called();network.assert_not_called();self.assertFalse(c.output.exists())

    def test_late_preparation_refusals_emit_no_success_receipt_and_no_external_calls(self):
        cases=['existing-output','wrong-helm','wrong-image-digest','unresolved-secret','source-changed','inventory-conflict']
        for case in cases:
            with self.subTest(case=case), Control() as c, patch.object(prepare,'subprocess_git_revision',return_value='a'*40), \
                 patch.object(prepare.subprocess,'run') as external, patch.object(seed,'build_opener') as network:
                expected={'existing-output':'output-already-exists','wrong-helm':'pinned-helm-4.2.0-required','wrong-image-digest':'registry-manifest-digest-mismatch',
                    'unresolved-secret':'unresolved-private-secret-reference:task-channel','source-changed':'source-changed-during-preparation',
                    'inventory-conflict':'resource-identity-conflict'}[case]
                if case=='existing-output': c.output.mkdir();(c.output/'prior').write_text('preserve')
                if case=='unresolved-secret': c.profile['privateSecretFiles'].pop('task-channel')
                if case=='inventory-conflict': c.profile['environmentManifests'].append(c.document('Deployment',c.rendered[0]['metadata']['name'],spec=copy.deepcopy(c.profile['environmentManifests'][3]['spec'])))
                original_source=prepare.source_inputs;source_calls=[]
                def sources(root):
                    value=original_source(root);source_calls.append(1)
                    return {**value,'changed':'hash'} if case=='source-changed' and len(source_calls)==3 else value
                def runner(argv):
                    if case=='wrong-helm' and argv[1:3]==['version','--short']: return b'v3.9.0'
                    if case=='wrong-image-digest' and argv[1:3]==['inspect','--raw']: return b'wrong-manifest'
                    return c.fake_run(argv)
                with patch.object(prepare,'run',side_effect=runner), patch.object(prepare,'source_inputs',side_effect=sources):
                    self.assert_refusal(expected,lambda:self.run_prepare(c))
                external.assert_not_called();network.assert_not_called();self.assertFalse((c.output/'review.json').exists())
                if case in ('wrong-helm','wrong-image-digest','unresolved-secret'): self.assertFalse(c.output.exists())
                if case=='existing-output': self.assertEqual('preserve',(c.output/'prior').read_text())

    def test_malformed_owner_seed_requests_refuse_without_network_or_journal(self):
        cases=[
            ('namespace',('namespace',),'other','seed-scope-mismatch'),
            ('tenant',('tenant',),'other','seed-scope-mismatch'),
            ('step-order',('steps',0,'name'),'create-tenant','complete-ordered-owner-seed-required'),
            ('role',('operatorRole',),'TenantReader','writer-subject-and-role-required'),
            ('subject',('operatorSubject',),'','writer-subject-and-role-required'),
            ('ports-missing',('forwardedPorts',),[18170],'private-forwarded-ports-required'),
            ('ports-duplicate',('forwardedPorts',),[18170,18170],'private-forwarded-ports-required'),
            ('ports-boolean',('forwardedPorts',),[True,18171],'private-forwarded-ports-required'),
            ('ports-low',('forwardedPorts',),[80,18171],'private-forwarded-ports-required'),
            ('ports-high',('forwardedPorts',),[18170,65536],'private-forwarded-ports-required'),
            ('external-host',('steps',0,'url'),'http://192.168.1.30:18170/api/v1/commands/bound/workload','external-seed-write-refused'),
            ('wrong-scheme',('steps',0,'url'),'https://127.0.0.1:18170/api/v1/commands/bound/workload','external-seed-write-refused'),
            ('wrong-port',('steps',0,'url'),'http://127.0.0.1:18172/api/v1/commands/bound/workload','external-seed-write-refused'),
            ('userinfo',('steps',0,'url'),'http://operator@127.0.0.1:18170/api/v1/commands/bound/workload','external-seed-write-refused'),
            ('query',('steps',0,'url'),'http://127.0.0.1:18170/api/v1/commands/bound/workload?other=true','external-seed-write-refused'),
            ('fragment',('steps',0,'url'),'http://127.0.0.1:18170/api/v1/commands/bound/workload#other','external-seed-write-refused'),
            ('method',('steps',0,'method'),'DELETE','seed-request-invalid'),
            ('credential',('steps',0,'credential'),'shared-admin','seed-request-invalid'),
            ('response-status',('steps',0,'expectedStatus'),204,'persisted-assertions-required'),
            ('assertion-shape',('steps',0,'assertions'),[],'persisted-assertions-required'),
            ('missing-read-assertion',('steps',1,'assertions'),{},'persisted-assertions-required'),
            ('create-method',('steps',2,'method'),'GET','seed-command-outside-task-tenant'),
            ('create-path',('steps',2,'url'),'http://127.0.0.1:18170/other','seed-command-outside-task-tenant'),
            ('create-tenant',('steps',2,'body','tenant'),'another','seed-command-outside-task-tenant'),
            ('create-domain',('steps',2,'body','domain'),'party','seed-command-outside-task-tenant'),
            ('create-aggregate',('steps',2,'body','aggregateId'),'another','seed-command-outside-task-tenant'),
            ('create-wire',('steps',2,'body','commandType'),'disable-tenant','seed-command-outside-task-tenant'),
            ('create-payload',('steps',2,'body','payload','TenantId'),'another','seed-command-outside-task-tenant'),
            ('message-case',('steps',2,'body','messageId'),'01arz3ndektsv4rrffq69g5faw','canonical-message-id-required'),
            ('member-subject',('steps',4,'body','payload','UserId'),'another','seed-member-scope-invalid'),
            ('member-role',('steps',4,'body','payload','Role'),'TenantOwner','seed-member-scope-invalid'),
            ('bootstrap-path',('steps',0,'url'),'http://127.0.0.1:18170/api/v1/commands','owner-bound-bootstrap-required'),
            ('bootstrap-credential',('steps',0,'credential'),'operator','owner-bound-bootstrap-required'),
            ('bootstrap-client-reuse',('steps',0,'body','principalId'),'operator-user','bootstrap-and-client-identities-must-be-separated'),
            ('bootstrap-principal',('steps',0,'body','command','payload','UserId'),'another','bootstrap-and-client-identities-must-be-separated'),
            ('bootstrap-tenant',('steps',0,'body','command','tenant'),'another','owner-bootstrap-command-invalid'),
            ('bootstrap-domain',('steps',0,'body','command','domain'),'tenants','owner-bootstrap-command-invalid'),
            ('bootstrap-wire',('steps',0,'body','command','commandType'),'create-tenant','owner-bootstrap-command-invalid'),
            ('status-path',('steps',1,'url'),'http://127.0.0.1:18170/api/v1/commands/status/another','status-message-binding-required'),
            ('status-message',('steps',1,'assertions','messageId'),'another','status-message-binding-required'),
            ('status-method',('steps',1,'method'),'POST','committed-owner-state-required'),
            ('status-completed',('steps',1,'assertions','status'),'Accepted','committed-owner-state-required'),
            ('committed-sequence',('steps',1,'assertions','minimum:committedEventSequence'),0,'committed-owner-state-required'),
            ('committed-events',('steps',1,'assertions','minimum:eventCount'),0,'committed-owner-state-required'),
            ('query-method',('steps',6,'method'),'GET','persisted-tenant-confirmation-required'),
            ('query-path',('steps',6,'url'),'http://127.0.0.1:18170/other','persisted-tenant-confirmation-required'),
            ('query-envelope',('steps',6,'body','tenant'),'another','persisted-tenant-confirmation-required'),
            ('query-payload',('steps',6,'body','payload','TenantId'),'another','persisted-tenant-confirmation-required'),
            ('query-document',('steps',6,'assertions','payload.tenantId'),'another','persisted-tenant-confirmation-required'),
            ('memories-method',('steps',7,'method'),'GET','task-memories-provisioning-required'),
            ('memories-path',('steps',7,'url'),'http://127.0.0.1:18171/tenants','task-memories-provisioning-required'),
            ('memories-tenant',('steps',7,'body','tenantId'),'another','task-memories-provisioning-required'),
            ('memories-read-method',('steps',8,'method'),'POST','persisted-memories-confirmation-required'),
            ('memories-read-path',('steps',8,'url'),'http://127.0.0.1:18171/api/v1/tenants/another','persisted-memories-confirmation-required'),
            ('memories-read-id',('steps',8,'assertions','id'),'another','persisted-memories-confirmation-required'),
            ('memories-read-active',('steps',8,'assertions','status'),'Provisioning','persisted-memories-confirmation-required')]
        seed.validate(plan(),TENANT,TENANT)
        for name,path,value,code in cases:
            with self.subTest(case=name), Control() as c, patch.object(seed,'build_opener') as network, patch.object(prepare.subprocess,'run') as external:
                document=plan();set_value(document,path,value)
                self.assert_refusal(code,lambda:seed.validate(document,TENANT,TENANT))
                network.assert_not_called();external.assert_not_called();self.assertFalse((c.directory/'journal').exists())

    def test_review_hash_scope_expiry_and_plan_changes_refuse_before_network(self):
        for kind,code in [('packet-hash','review-packet-hash-mismatch'),('schema','review-scope-mismatch'),
                          ('namespace','review-scope-mismatch'),('tenant','review-scope-mismatch'),
                          ('expired','review-expired'),('no-timezone','review-expired'),('plan-hash','reviewed-seed-plan-changed')]:
            with self.subTest(case=kind), Control() as c, patch.object(seed,'build_opener') as network, patch.object(prepare.subprocess,'run') as external:
                seed.verify_review(c.seed,c.review_path,c.review_sha,TENANT,TENANT)
                if kind=='packet-hash': c.review_sha='0'*64
                elif kind=='schema': c.review['schema']='other';c.save_review()
                elif kind=='namespace': c.review['target']['namespace']='another';c.save_review()
                elif kind=='tenant': c.review['tenant']='another';c.save_review()
                elif kind=='expired': c.review['expiresAt']=(datetime.now(timezone.utc)-timedelta(seconds=1)).isoformat();c.save_review()
                elif kind=='no-timezone': c.review['expiresAt']=datetime.now().isoformat();c.save_review()
                else: c.seed.write_text(json.dumps({**plan(),'operatorSubject':'changed-after-review'}))
                self.assert_refusal(code,lambda:seed.verify_review(c.seed,c.review_path,c.review_sha,TENANT,TENANT))
                network.assert_not_called();external.assert_not_called();self.assertFalse((c.directory/'journal').exists())

    def test_seed_private_input_modes_symlinks_and_paths(self):
        for kind in ('relative','missing','directory','symlink','public'):
            with self.subTest(case=kind), Control() as c, patch.object(seed,'build_opener') as network:
                self.assertEqual(plan(),seed.private_json(c.seed))
                target=c.seed
                if kind=='relative': target=Path('private-seed.json')
                elif kind=='missing': target=c.directory/'missing'
                elif kind=='directory': target=c.directory
                elif kind=='symlink': target=c.directory/'link';target.symlink_to(c.seed)
                else: c.seed.chmod(0o644)
                self.assert_refusal('private-input-mode-must-be-0600',lambda:seed.private_json(target));network.assert_not_called()

    def test_representative_cli_refusals_preserve_exact_channels_and_do_no_work(self):
        for kind in ('profile-schema','configmap-credential','seed-request','seed-private-file','missing-review','review-hash'):
            with self.subTest(case=kind), Control() as c, patch.object(prepare,'subprocess_git_revision',return_value='a'*40), \
                 patch.object(prepare,'run') as runner, patch.object(prepare.subprocess,'run') as external, patch.object(seed,'build_opener') as network:
                if kind.startswith('profile') or kind=='configmap-credential':
                    if kind=='profile-schema': c.profile['schema']='unknown';code='profile-schema-invalid'
                    else: c.profile['environmentManifests'][0]['data']['SigningKey']='do-not-retain';code='configmap-credential-refused'
                    c.save_profile();main=prepare.main
                    argv=['prepare.py','--profile',str(c.profile_path),'--canonical-graph',str(c.graph),'--output',str(c.output),'--helm','helm']
                    expected='Preparation refused: '+code+'\n'
                else:
                    main=seed.main;argv=['seed.py','--plan',str(c.seed),'--namespace',TENANT,'--tenant',TENANT]
                    if kind=='seed-request':
                        document=plan();document['steps'][2]['body']['domain']='party';c.seed.write_text(json.dumps(document));code='seed-command-outside-task-tenant'
                    elif kind=='seed-private-file': c.seed.chmod(0o644);code='private-input-mode-must-be-0600'
                    elif kind=='missing-review': argv+=['--execute-reviewed'];code='review-hash-private-authorities-and-fresh-journal-required'
                    else:
                        argv+=['--execute-reviewed','--review',str(c.review_path),'--review-sha256','0'*64,'--authorities',str(c.registry),'--journal',str(c.directory/'journal')]
                        code='review-packet-hash-mismatch'
                    expected='Seed stopped: '+code+'. An uncertain write must not be retried automatically.\n'
                stdout=io.StringIO();stderr=io.StringIO()
                original_prepare=prepare.prepare
                with patch('sys.argv',argv),contextlib.redirect_stdout(stdout),contextlib.redirect_stderr(stderr), \
                     patch.object(prepare,'prepare',side_effect=lambda *args,**kwargs:original_prepare(*args,root=c.root,**kwargs)):
                    result=main()
                self.assertEqual(1,result);self.assertEqual('',stdout.getvalue());self.assertEqual(expected,stderr.getvalue())
                self.assertNotIn('do-not-retain',stderr.getvalue());runner.assert_not_called();external.assert_not_called();network.assert_not_called()
                self.assertFalse(c.output.exists());self.assertFalse((c.directory/'journal').exists())

    def test_external_tool_failures_and_seed_runtime_guards(self):
        with patch.object(prepare.subprocess,'run',return_value=SimpleNamespace(returncode=1,stdout=b'')):
            self.assert_refusal('read-only-tool-failed:helm',lambda:prepare.run(['helm','version','--short']))
            self.assert_refusal('source-revision-unavailable',lambda:prepare.subprocess_git_revision(Path('/synthetic')))
        self.assert_refusal('seed-redirect-refused',lambda:seed.NoRedirect().redirect_request(None,None,302,'redirect',{},'https://another'))
        self.assert_refusal('persisted-owner-assertion-failed',lambda:seed.assertion({'status':'Accepted'},'status','Completed'))
        self.assert_refusal('persisted-owner-assertion-failed',lambda:seed.assertion({'sequence':0},'minimum:sequence',1))
        for case,code in [('response-status','owner-api-status-failed'),('write-assertion','submitted-write-outcome-uncertain')]:
            with self.subTest(case=case), Control() as c:
                document=plan();transport=Transport()
                if case=='response-status':
                    response=Response(b'{}');response.status=500
                    opener=SimpleNamespace(open=lambda *args,**kwargs:response)
                else:
                    document['steps'][0]['assertions']={'messageId':'expected-but-not-returned'};opener=transport
                with patch.object(seed,'build_opener',return_value=opener):
                    self.assert_refusal(code,lambda:seed.execute(document,{k:{} for k in ('bootstrap','admin','operator','memories')},c.directory/'journal'))
                receipt=json.loads((c.directory/'journal/00-bootstrap.json').read_text())
                self.assertEqual('uncertain',receipt['outcome']);self.assertEqual(1,receipt['attempts'])
