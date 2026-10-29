"""Meaningful offline matrix tests. All complete operational inputs are synthetic."""

import ast
import builtins
import contextlib
import copy
import hashlib
import io
import json
from pathlib import Path
import re
import tempfile
import unittest
from unittest import mock

import prepare

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent


def fixture():
    return prepare.read_json(HERE / 'fixtures/complete.synthetic.json')


def observations():
    return prepare.read_json(HERE / 'fixtures/matching-observations.synthetic.json')


def assign(data, path, value):
    tokens = path.strip('/').split('/')
    node = data
    for key in tokens[:-1]:
        node = node[int(key)] if isinstance(node, list) else node[key]
    key = tokens[-1]
    if isinstance(node, list):
        node[int(key)] = value
    else:
        node[key] = value


def nulls(data, path=''):
    if data is None:
        yield path
    elif isinstance(data, dict):
        for key, value in data.items():
            if key != '_provenance':
                yield from nulls(value, path+'/'+key)
    elif isinstance(data, list):
        for i, value in enumerate(data):
            yield from nulls(value, f'{path}/{i}')


class InputMatrixTests(unittest.TestCase):
    def refuse(self, data, contains):
        with self.assertRaises(prepare.Refused) as caught:
            prepare.assess(data)
        self.assertTrue(any(contains in p for p in caught.exception.problems), caught.exception.problems)
        return caught.exception.problems

    def test_current_resolution_blocks_and_enumerates_exact_null_set(self):
        data = prepare.read_json(HERE/'inputs.resolved.json')
        problems = self.refuse(data, '/providers/openbao')
        self.assertEqual(set(nulls(data)), {p.split(': missing;')[0] for p in problems})
        self.assertEqual(len(problems), 43)
        self.assertTrue(all('owner=' in p and 'question=' in p for p in problems))

    def test_current_null_questions_are_exact_and_resolved_fields_are_separate(self):
        data = prepare.read_json(HERE/'inputs.resolved.json')
        doc = (HERE/'MISSING-INPUTS.md').read_text()
        section = doc.split('## Exact current null set')[1].split('## Conditional')[0]
        self.assertEqual(set(nulls(data)), set(re.findall(r'\| `(/[^`]+)` \|', section)))
        schema = prepare.read_json(HERE/'inputs.schema.json')
        for path in nulls(data):
            node = schema
            for key in path.strip('/').split('/'):
                node = node['properties'][key]
            self.assertIn(node['x-question'], section)
            self.assertIn(node['x-owner'], section)

    def test_schema_closed_and_every_nullable_member_has_owner_question(self):
        def walk(schema):
            if isinstance(schema['type'], list) and 'null' in schema['type']:
                # Source index location alternatives are metadata, not live choices.
                if 'x-question' in schema:
                    self.assertTrue(schema['x-owner'])
                    self.assertTrue(schema['x-question'].endswith('?'))
                else:
                    self.assertEqual(set(schema['type'])-{'null'}, {'string'} if schema['type'][0]=='string' else {'integer'})
            if schema['type'] == 'object' or schema['type'] == ['object','null']:
                if 'properties' in schema:
                    self.assertIs(schema['additionalProperties'], False)
                    self.assertEqual(set(schema['required']),set(schema['properties']))
                for item in schema.get('properties',{}).values():
                    walk(item)
            if 'items' in schema:
                walk(schema['items'])
        walk(prepare.read_json(HERE/'inputs.schema.json'))

    def test_complete_synthetic_fixture_is_deterministic_unapproved(self):
        data = fixture()
        before = copy.deepcopy(data)
        first = prepare.assess(data)
        self.assertEqual(first,prepare.assess(data))
        self.assertEqual(before,data)
        self.assertEqual(first['evidenceMode'],'synthetic-test-only')
        for key in ('approvalGranted','liveAcceptance','deployableOutput','processOrNetworkEffects'):
            self.assertIs(first[key],False)
        self.assertEqual(first['status'],'PREPARED_UNAPPROVED')

    def test_wrong_types_and_unknown_fields_refused(self):
        for path,value in [('/ordinaryStore/replicas',True),('/ordinaryStore/storage/volumeIds','x'),('/tenantStore/nativeTls/port',65536),('/providers/openbao/digest',7),('/secretContract/apps/0/token/renewal/ttlSeconds','600')]:
            with self.subTest(path=path):
                data=fixture();assign(data,path,value);self.refuse(data,path)
        data=fixture();data['ordinaryStore']['unexpectedSecretValue']='withheld-sentinel'
        errors=self.refuse(data,'unknown member')
        self.assertNotIn('withheld-sentinel',' '.join(errors))
        self.assertNotIn('unexpectedSecretValue',' '.join(errors))

    def test_absent_member_and_empty_inventory_refused(self):
        data=fixture();del data['ordinaryStore']['backend'];self.refuse(data,'backend: required member absent')
        for path in ('/secretContract/apps','/secretContract/apps/0/requiredSecrets','/ordinaryStore/storage/volumeIds'):
            with self.subTest(path=path):
                data=fixture();assign(data,path,[]);self.refuse(data,path)

    def test_placeholders_and_credential_like_material_refused(self):
        for value in ('unknown','<owner>','${TOKEN}','{{token}}','TBD','synthetic-credential-bytes','-----BEGIN PRIVATE KEY-----'):
            with self.subTest(value=value):
                data=fixture();data['authority']['environmentLayerPrincipal']=value
                errors=self.refuse(data,'/authority/environmentLayerPrincipal')
                self.assertNotIn(value,' '.join(errors))

    def test_nested_app_nulls_have_exact_questions(self):
        data=fixture();data['secretContract']['apps'][0]['requiredSecrets'][0]['generation']=None
        errors=self.refuse(data,'/secretContract/apps/0/requiredSecrets/0/generation')
        self.assertIn('question=What exact positive generation of this secret is required by the staging contract?',errors[0])
        self.assertIn('owner=Platform maintainer',errors[0])

    def test_all_conditional_nullable_questions_present(self):
        schema=prepare.read_json(HERE/'inputs.schema.json')
        doc=(HERE/'MISSING-INPUTS.md').read_text()
        resolved=prepare.read_json(HERE/'inputs.resolved.json')['_provenance']
        def walk(s,path=''):
            if 'x-question' in s and path not in resolved:
                self.assertIn('`'+path+'`',doc)
                self.assertIn(s['x-question'],doc)
                self.assertIn(s['x-owner'],doc)
            for key,item in s.get('properties',{}).items():walk(item,path+'/'+key)
            if 'items' in s:walk(s['items'],path+'/*')
        walk(schema)

    def test_synthetic_fixture_cannot_claim_recorded_provenance(self):
        data=fixture();data['evidenceMode']='recorded-evidence';self.refuse(data,'resolved value lacks source identity')
        data=fixture();data['_provenance']=prepare.read_json(HERE/'inputs.resolved.json')['_provenance'];self.refuse(data,'synthetic fixtures cannot claim')

    def test_recorded_provenance_cannot_launder_complete_synthetic_source(self):
        data=fixture();data['evidenceMode']='recorded-evidence'
        source=HERE/'fixtures/complete.synthetic.json'
        digest=hashlib.sha256(source.read_bytes()).hexdigest()
        data['_provenance']={path:{'path':str(source.relative_to(ROOT)), 'sha256':digest,
                                  'pointer':path, 'lineStart':None, 'lineEnd':None,
                                  'meaning':'Attempted synthetic provenance laundering'}
                             for path,_ in prepare.leaves(data)}
        self.refuse(data,'explicitly synthetic-test-only source')

    def test_malformed_dynamic_provenance_entry_name_is_withheld(self):
        sentinel='sensitive-provenance-entry-sentinel'
        data=fixture();data['_provenance']={sentinel:{'sha256':7}}
        errors=self.refuse(data,'/_provenance/[entry]')
        self.assertNotIn(sentinel,' '.join(errors))

    def test_duplicate_json_members_and_invalid_json_refused(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'inputs.json'
            for content in ('{"schemaVersion":1,"schemaVersion":1}','{"x":NaN}','not json'):
                path.write_text(content)
                with self.assertRaises(prepare.Refused):prepare.read_json(path)

    def test_missing_file_is_sanitized_refusal(self):
        with self.assertRaises(prepare.Refused) as caught:prepare.read_json(HERE/'absent-input-fixture')
        self.assertEqual(caught.exception.problems,['JSON: unreadable file or invalid JSON (contents withheld)'])


class BoundaryTests(unittest.TestCase):
    refuse=InputMatrixTests.refuse

    def test_logical_mounts_in_shared_state_cannot_exclude_tenant_keys(self):
        for key in ('instanceId','snapshotDomainId'):
            data=fixture();data['tenantStore'][key]=data['ordinaryStore'][key]
            self.refuse(data,'KV paths cannot filter')
        data=fixture();data['tenantStore']['storage']['stateId']=data['ordinaryStore']['storage']['stateId']
        self.refuse(data,'separate physical persisted state')

    def test_shared_physical_volume_refused(self):
        data=fixture();data['tenantStore']['storage']['volumeIds']=data['ordinaryStore']['storage']['volumeIds']
        self.refuse(data,'physical volumes must be disjoint')

    def test_snapshot_cross_membership_and_missing_store_membership_refused(self):
        data=fixture();data['snapshotBoundary']['ordinaryStateMembers'].append('synthetic-keys-state')
        self.refuse(data,'snapshot membership must be disjoint')
        data=fixture();data['snapshotBoundary']['tenantKeyStateMembers']=['synthetic-unrelated-state']
        self.refuse(data,'each selected physical store')

    def test_key_custody_cannot_share_ordinary_backup_authority(self):
        for key in ('custodyClass','destinationRef','accessPrincipal','decryptionCustodyRef'):
            with self.subTest(key=key):
                data=fixture();data['backup']['tenantKeys'][key]=data['backup']['ordinary'][key]
                self.refuse(data,'tenant-key custody must be separate')

    def test_wrong_namespace_or_tls_port_refused(self):
        data=fixture();data['ordinaryStore']['nativeTls']['namespace']='synthetic-other-data';self.refuse(data,'staging data namespace')
        data=fixture();data['ordinaryStore']['nativeTls']['port']=18444;self.refuse(data,'endpoint and listener port disagree')

    def test_endpoint_requires_explicit_owner_port(self):
        for store in ('ordinaryStore','tenantStore'):
            data=fixture();data[store]['nativeTls']['endpoint']='https://service.synthetic.invalid'
            self.refuse(data,'/'+store+'/nativeTls/endpoint')

    def test_identical_store_service_destination_refused(self):
        data=fixture()
        for key in ('endpoint','port','namespace','podLabels'):
            data['tenantStore']['nativeTls'][key]=copy.deepcopy(data['ordinaryStore']['nativeTls'][key])
        self.refuse(data,'same service destination')

    def test_layer_and_job_authority_must_differ(self):
        data=fixture();data['authority']['jobPrincipals'].append(data['authority']['environmentLayerPrincipal']);self.refuse(data,'job and layer principals')
        data=fixture();data['authority']['sharedInfrastructurePrincipal']=data['authority']['environmentLayerPrincipal'];self.refuse(data,'layer principals must differ')
        data=fixture();data['custody']['deputy']=data['custody']['administrator'];self.refuse(data,'independent Administrator and deputy')

    def test_component_scope_and_token_references_are_per_app(self):
        data=fixture();apps=data['secretContract']['apps'];apps[0]['scopes'].append(apps[1]['appId']);self.refuse(data,'only its app ID')
        data=fixture();apps=data['secretContract']['apps'];apps[1]['token']['bootstrapSecretRef']=apps[0]['token']['bootstrapSecretRef'];self.refuse(data,'shared bootstrap token references')
        data=fixture();data['secretContract']['apps'][0]['token']['bootstrapSecretRef']=data['bootstrapToken']['name'];self.refuse(data,'historical shared token')

    def test_duplicate_identities_and_secret_requirements_refused(self):
        for key in ('appId','serviceAccount','componentName'):
            data=fixture();apps=data['secretContract']['apps'];apps[1][key]=apps[0][key];self.refuse(data,'identities must be unique')
        data=fixture();secrets=data['secretContract']['apps'][0]['requiredSecrets'];secrets.append({**secrets[0],'generation':8});self.refuse(data,'duplicate logical secret requirement')

    def test_bounded_external_renewal_and_preexpiry_cutover(self):
        data=fixture();renewal=data['secretContract']['apps'][0]['token']['renewal'];renewal['renewBeforeSeconds']=renewal['ttlSeconds'];self.refuse(data,'lead time must be less')
        data=fixture();data['bootstrapToken']['cutoverDeadline']=data['bootstrapToken']['historicalExpiry'];self.refuse(data,'before the historically asserted expiry')

    def test_bootstrap_dates_are_actual_calendar_dates(self):
        for key,value in [('cutoverDeadline','2027-04-31'),('historicalExpiry','2027-02-29'),('historicalExpiry','2027-99-99')]:
            data=fixture();data['bootstrapToken'][key]=value
            self.refuse(data,'/bootstrapToken/'+key+': valid calendar date required')
        data=fixture();data['bootstrapToken'].update(cutoverDeadline='2028-02-29',historicalExpiry='2028-03-01')
        self.assertTrue(prepare.assess(data)['prepared'])

    def test_resource_requests_must_not_exceed_matching_limits(self):
        for store,resource,request,limit in [('ordinaryStore','cpu','10','1'),('tenantStore','memory','10Gi','1Gi')]:
            data=fixture();data[store]['resources'][resource+'Request']=request;data[store]['resources'][resource+'Limit']=limit
            self.refuse(data,'resource request exceeds its matching limit')
        data=fixture();data['ordinaryStore']['resources'].update(cpuRequest='10m',cpuLimit='0.010',memoryRequest='1024Ki',memoryLimit='1Mi')
        self.assertTrue(prepare.assess(data)['prepared'])

    def test_resource_quantity_subset_refuses_invalid_or_unsupported_forms(self):
        for key,value in [('cpuRequest','1e3'),('cpuRequest','NaN'),('cpuRequest','0.0001'),('cpuRequest','-1m'),('memoryRequest','1.5Mi'),('memoryRequest','8mb')]:
            data=fixture();data['ordinaryStore']['resources'][key]=value
            self.refuse(data,'documented conservative resource-quantity subset')

    def test_directory_projection_and_full_token_file_relationship(self):
        for key,value in [('mountPath','/synthetic-only/alpha'),('mountDirectory','relative-directory'),('projectedFileName','../token'),('mountPath','/synthetic-only/../token')]:
            data=fixture();data['secretContract']['apps'][0]['token'][key]=value
            self.refuse(data,'/secretContract/apps/0/token/')

    def test_scoped_dynamic_paths_allowed_global_admin_paths_refused(self):
        self.assertTrue(prepare.assess(fixture())['prepared'])
        for path in ('*','sys/policies','auth/token','sys*','auth*','s*','a*','+/policies'):
            data=fixture();data['secretContract']['apps'][0]['allowedSecretPaths']=[path]
            self.refuse(data,'cannot establish least privilege')

    def test_bootstrap_secret_volume_and_key_grammar(self):
        for key,value in [('bootstrapSecretRef','openbao-runtime-bootstrap/token'),
                          ('bootstrapSecretRef','other-namespace/bootstrap'),
                          ('bootstrapSecretRef','Bad:Name'),('bootstrapSecretRef','a'*254),
                          ('volumeName','bad:name'),('volumeName','a'*64),
                          ('sourceKey','bad/key'),('sourceKey','..key'),('sourceKey','.')]:
            data=fixture();data['secretContract']['apps'][0]['token'][key]=value
            self.refuse(data,'/secretContract/apps/0/token/'+key)


class ReadinessTests(unittest.TestCase):
    def refuse(self, data, evidence, contains):
        with self.assertRaises(prepare.Refused) as caught:prepare.readiness(data,evidence)
        self.assertTrue(any(contains in p for p in caught.exception.problems),caught.exception.problems)

    def test_matching_fixture_is_metadata_only_unapproved(self):
        result=prepare.readiness(fixture(),observations())
        self.assertTrue(result['metadataReady'])
        self.assertTrue(result['proposedContractOnly'])
        self.assertFalse(result['liveAcceptance'])
        self.assertFalse(result['approvalGranted'])
        self.assertIn('One supplied pod observation per app',result['meaning'])
        self.assertIn('No all-replica coverage',result['meaning'])
        self.assertEqual(result,prepare.readiness(fixture(),observations()))

    def test_pod_identity_cannot_be_reused_across_apps(self):
        o=observations();o['apps'][1]['podUid']=o['apps'][0]['podUid']
        self.refuse(fixture(),o,'one pod identity cannot be reused across apps')

    def test_readiness_cli_matching_and_stale_generation(self):
        stdout,stderr=io.StringIO(),io.StringIO()
        args=['readiness','--inputs',str(HERE/'fixtures/complete.synthetic.json'),
              '--observations',str(HERE/'fixtures/matching-observations.synthetic.json')]
        with contextlib.redirect_stdout(stdout),contextlib.redirect_stderr(stderr):
            self.assertEqual(prepare.main(args),0)
        self.assertTrue(json.loads(stdout.getvalue())['metadataReady'])
        self.assertFalse(json.loads(stdout.getvalue())['liveAcceptance'])
        self.assertEqual(stderr.getvalue(),'')
        with tempfile.TemporaryDirectory() as directory:
            stale=observations();stale['apps'][0]['generations'][0]['generation']=6
            path=Path(directory)/'stale-observations.json';path.write_text(json.dumps(stale))
            stdout,stderr=io.StringIO(),io.StringIO()
            with contextlib.redirect_stdout(stdout),contextlib.redirect_stderr(stderr):
                self.assertEqual(prepare.main(args[:-1]+[str(path)]),2)
            self.assertEqual(stdout.getvalue(),'')
            self.assertIn('nonexact required generation',stderr.getvalue())

    def test_absent_stale_and_future_required_generations_refused(self):
        for generation in (6,8):
            o=observations();o['apps'][0]['generations'][0]['generation']=generation;self.refuse(fixture(),o,'nonexact required generation')
        o=observations();o['apps'][0]['generations']=[];self.refuse(fixture(),o,'at least 1')
        o=observations();o['apps'][0]['generations'][0]['present']=False;self.refuse(fixture(),o,'fixed contract metadata')

    def test_wrong_environment_app_or_contract_refused(self):
        for key,value,match in [('environment','synthetic-other-environment','another environment'),('contractDigest','sha256:'+'c'*64,'exact required contract bytes')]:
            o=observations();o[key]=value;self.refuse(fixture(),o,match)
        o=observations();o['apps'][0]['appId']='synthetic-undeclared';self.refuse(fixture(),o,'undeclared app')
        o=observations();o['apps'].pop();self.refuse(fixture(),o,'complete matching app')

    def test_duplicate_observations_refused(self):
        o=observations();o['apps'].append(copy.deepcopy(o['apps'][0]));self.refuse(fixture(),o,'duplicate entries')
        o=observations();o['apps'].append({**o['apps'][0],'podUid':'synthetic-another-pod'});self.refuse(fixture(),o,'duplicate app observation')
        o=observations();o['apps'][0]['generations'].append({**o['apps'][0]['generations'][0],'generation':8});self.refuse(fixture(),o,'duplicate logical secret')

    def test_malformed_and_secret_bearing_observations_refused(self):
        o=observations();o['apps'][0]['generations'][0]['generation']=True;self.refuse(fixture(),o,'wrong type')
        o=observations();o['apps'][0]['podUid']='TBD';self.refuse(fixture(),o,'placeholder')
        o=observations();o['apps'][0]['generations'][0]['secretBytes']='withheld-sentinel';self.refuse(fixture(),o,'unknown member')
        o=observations();del o['apps'][0]['podUid'];self.refuse(fixture(),o,'required member absent')

    def test_unacknowledged_or_unrestarted_rotation_and_stale_delivery_refused(self):
        for key in ('sidecarStartedAfterDelivery','rotationAcknowledged'):
            o=observations();o['apps'][0][key]=False;self.refuse(fixture(),o,'fixed contract metadata')
        o=observations();o['apps'][0]['tokenDeliveryRevision']=3;self.refuse(fixture(),o,'stale rotation delivery revision')


class ProvenanceAndEffectsTests(unittest.TestCase):
    def test_resolved_source_hashes_and_values_are_intact(self):
        data=prepare.read_json(HERE/'inputs.resolved.json')
        self.assertEqual(prepare.provenance_errors(data),[])
        self.assertEqual(set(dict(prepare.leaves(data))),set(data['_provenance']))

    def test_changed_source_hash_pointer_or_value_refused(self):
        data=prepare.read_json(HERE/'inputs.resolved.json')
        for kind in ('hash','pointer','value','path'):
            with self.subTest(kind=kind):
                bad=copy.deepcopy(data);source=bad['_provenance']['/environment/context']
                if kind=='hash':source['sha256']='0'*64
                if kind=='pointer':source['pointer']='/absent'
                if kind=='value':bad['environment']['context']='synthetic-other-context'
                if kind=='path':source['path']='../outside-worktree'
                self.assertTrue(prepare.provenance_errors(bad))

    def test_line_cited_value_and_moved_line_range_refused(self):
        data=prepare.read_json(HERE/'inputs.resolved.json')
        for key in ('name','historicalExpiry'):
            path='/bootstrapToken/'+key
            bad=copy.deepcopy(data)
            bad['bootstrapToken'][key]='changed-planning-value'
            self.assertTrue(any(path in error for error in prepare.provenance_errors(bad)))
            bad=copy.deepcopy(data)
            bad['_provenance'][path].update(lineStart=1,lineEnd=1)
            self.assertTrue(any(path in error for error in prepare.provenance_errors(bad)))

    def test_all_documented_sources_have_hashes_and_valid_locations(self):
        index=prepare.read_json(HERE/'source-evidence.json')
        for source in index['sources']:
            path=ROOT/source['path'];raw=path.read_bytes()
            self.assertEqual(hashlib.sha256(raw).hexdigest(),source['sha256'],source['id'])
            if source['pointer'] is not None:self.assertIsNotNone(prepare.pointer(prepare.read_json(path),source['pointer']))
            if source['lineStart'] is not None:self.assertTrue(1<=source['lineStart']<=source['lineEnd']<=len(raw.decode().splitlines()))
        self.assertFalse((ROOT/index['absentContract']['path']).exists())
        self.assertFalse(index['newCollectionPerformed'])
        self.assertFalse(index['liveAcceptance'])

    def test_every_new_foundation_file_is_module_independent(self):
        modules=re.findall(r'\[submodule "references/([^\"]+)"\]',(ROOT/'.gitmodules').read_text())
        forbidden=[name.casefold() for name in modules]+['time'+'sheets']
        for file in HERE.rglob('*'):
            if file.is_file() and '__pycache__' not in file.parts:
                content=file.read_text().casefold()
                for name in forbidden:self.assertNotIn(name,content,str(file.relative_to(HERE)))

    def test_assessor_has_no_process_or_network_imports(self):
        tree=ast.parse((HERE/'prepare.py').read_text())
        names=set()
        for node in ast.walk(tree):
            if isinstance(node,ast.Import):names.update(a.name.split('.')[0] for a in node.names)
            if isinstance(node,ast.ImportFrom):names.add(node.module.split('.')[0])
        self.assertLessEqual(names,prepare.ALLOWED_IMPORTS)
        self.assertFalse(any(isinstance(n,ast.Call) and isinstance(n.func,ast.Name) and n.func.id in {'eval','exec','__import__','compile'} for n in ast.walk(tree)))

    def test_assessment_readiness_and_cli_do_not_write_or_import_live_adapters(self):
        original=builtins.__import__
        def restricted(name,*args,**kwargs):
            if name.split('.')[0] in {'subprocess','socket','urllib','http','requests','multiprocessing','asyncio'}:
                raise AssertionError('process/network adapter import forbidden')
            return original(name,*args,**kwargs)
        with mock.patch.object(builtins,'__import__',side_effect=restricted),mock.patch.object(Path,'write_text',side_effect=AssertionError('write forbidden')),mock.patch.object(Path,'write_bytes',side_effect=AssertionError('write forbidden')),mock.patch.object(Path,'mkdir',side_effect=AssertionError('mkdir forbidden')):
            self.assertTrue(prepare.assess(fixture())['prepared'])
            self.assertTrue(prepare.readiness(fixture(),observations())['metadataReady'])
            stdout,stderr=io.StringIO(),io.StringIO()
            with contextlib.redirect_stdout(stdout),contextlib.redirect_stderr(stderr):
                code=prepare.main(['assess','--inputs',str(HERE/'inputs.resolved.json')])
            self.assertEqual(code,2)
            self.assertEqual(stdout.getvalue(),'')
            self.assertIn('question=',stderr.getvalue())

    def test_cli_invalid_has_no_output_artifacts_or_mutation_commands(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'invalid.json';path.write_text('{"token":"withheld-sentinel"}')
            before=list(Path(directory).iterdir());stdout,stderr=io.StringIO(),io.StringIO()
            with contextlib.redirect_stdout(stdout),contextlib.redirect_stderr(stderr):
                self.assertEqual(prepare.main(['assess','--inputs',str(path)]),2)
            self.assertEqual(stdout.getvalue(),'')
            self.assertNotIn('withheld-sentinel',stderr.getvalue())
            self.assertEqual(before,list(Path(directory).iterdir()))
        stdout=io.StringIO()
        with contextlib.redirect_stdout(stdout):self.assertEqual(prepare.main(['assess','--inputs',str(HERE/'fixtures/complete.synthetic.json')]),0)
        result=json.loads(stdout.getvalue())
        self.assertFalse(result['approvalGranted'])
        self.assertNotIn('argv',stdout.getvalue())
        self.assertNotIn('kubectl',stdout.getvalue())

    def test_definitions_remain_parameterized_with_explicit_boundaries(self):
        d=prepare.read_json(HERE/'definitions.json')
        self.assertFalse(d['deployable']);self.assertFalse(d['approvalGranted']);self.assertFalse(d['liveAcceptance'])
        self.assertFalse(d['snapshotBoundary']['logicalPathExclusionSufficient'])
        self.assertFalse(d['snapshotBoundary']['ordinarySnapshotsContainTenantKeys'])
        self.assertTrue(d['snapshotBoundary']['separateInstancePhysicalStateVolumesAndSnapshotDomainsRequired'])
        self.assertEqual(d['perAppComponentProposal']['type'],'secretstores.hashicorp.vault')
        self.assertIsNone(d['perAppComponentProposal']['metadata']['vaultTokenMountPath'])
        metadata=d['perAppComponentProposal']['metadata']
        self.assertEqual(metadata['tlsServerNameFrom'],'/ordinaryStore/nativeTls/serverName')
        self.assertFalse(metadata['skipVerify'])
        ca=d['perAppComponentProposal']['caDeliveryContract']
        self.assertTrue(ca['mutuallyExclusive'])
        self.assertIsNone(ca['selectedMode'])
        self.assertEqual(ca['exactlyOneSelectedMetadataFieldRequired'],['caCert','caPem'])
        self.assertEqual(ca['caCert']['bindingsFromEvidence'],'/ordinaryStore/nativeTls/caBundleRef')
        self.assertEqual(ca['caPem']['bindingsFromEvidence'],'/ordinaryStore/nativeTls/caBundleRef')
        self.assertIn('one supplied pod observation per app',d['proposedReadinessContract']['scope'])
        self.assertFalse(d['proposedReadinessContract']['suppliedBooleansProveRestartOrNewness'])
        mount=d['perPodMountProposal']
        self.assertEqual(mount['annotation']['name'],'dapr.io/volume-mounts')
        self.assertIn('mountDirectory',mount['annotation']['binding'])
        self.assertIn('projectedFileName',mount['filePathInvariant'])
        self.assertIsNone(mount['projectedKeyToFile']['sourceKey'])

if __name__=='__main__':
    unittest.main()
