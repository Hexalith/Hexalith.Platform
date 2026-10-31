"""Exact native creation/cleanup of a fresh telemetry target; no CLI or authority.

A qualified caller supplies authenticated policy/fence callbacks and the native
transport. Unknown create outcomes never gain ownership through a later GET.
"""
import base64
import copy
import secrets
import re
import time
from pathlib import Path
import telemetry_postgres_adapter as adapter


def require(ok, reason):
    if not ok:raise ValueError(reason)


def path(resource):
    kind=resource['kind'];md=resource['metadata'];namespace=md.get('namespace')
    kinds={'Namespace':('/api/v1/namespaces',False), 'Secret':('/api/v1/namespaces/'+str(namespace)+'/secrets',True),
           'PersistentVolumeClaim':('/api/v1/namespaces/'+str(namespace)+'/persistentvolumeclaims',True),
           'NetworkPolicy':('/apis/networking.k8s.io/v1/namespaces/'+str(namespace)+'/networkpolicies',True),
           'StatefulSet':('/apis/apps/v1/namespaces/'+str(namespace)+'/statefulsets',True)}
    require(kind in kinds,'target-kind-outside-exact-profile')
    return kinds[kind][0]+'/'+md['name']


def assert_created(current, desired, uid):
    require(current.get('metadata',{}).get('uid')==uid and uid,'target-created-uid-drift')
    md=current['metadata'];expected=desired['metadata']
    require(md.get('name')==expected['name'] and md.get('namespace')==expected.get('namespace')
            and not md.get('ownerReferences'),'target-created-identity-or-owner-drift')
    labels=dict(md.get('labels',{}))
    if desired['kind']=='Namespace':
        require(labels.pop('kubernetes.io/metadata.name',md['name'])==md['name'],'target-namespace-default-label-drift')
    require(labels==expected.get('labels',{}) and md.get('annotations',{})==expected.get('annotations',{}),
            'target-created-label-or-annotation-drift')
    kind=desired['kind']
    if kind=='Secret':
        require(current.get('type')==desired['type'] and current.get('data')==desired['data']
                and not current.get('immutable',False),'target-bootstrap-secret-drift')
    elif kind=='NetworkPolicy':
        spec=copy.deepcopy(current['spec']);spec.setdefault('ingress',[]);spec.setdefault('egress',[])
        require(spec==desired['spec'],'target-network-policy-drift')
    elif kind=='Namespace':
        require(current.get('spec',{}).get('finalizers',[]) in ([],['kubernetes']), 'target-namespace-finalizer-drift')
    elif kind=='PersistentVolumeClaim':
        spec=current['spec'];want=desired['spec']
        require(spec.get('accessModes')==want['accessModes'] and spec.get('storageClassName')==want['storageClassName']
                and spec.get('resources')==want['resources'] and spec.get('volumeMode','Filesystem')=='Filesystem'
                and not any(spec.get(k) for k in ('selector','dataSource','dataSourceRef','volumeAttributesClassName')),
                'target-pvc-spec-drift')
        require(set(md.get('finalizers',[]))<={'kubernetes.io/pvc-protection'},'target-pvc-finalizer-drift')
    elif kind=='StatefulSet':
        spec=current['spec'];want=desired['spec']
        template_metadata=copy.deepcopy(spec.get('template',{}).get('metadata',{}))
        if template_metadata.get('creationTimestamp') is None:template_metadata.pop('creationTimestamp',None)
        require(spec.get('replicas')==1 and spec.get('selector')==want['selector']
                and spec.get('serviceName')==want['serviceName'] and not spec.get('volumeClaimTemplates')
                and template_metadata==want['template']['metadata'], 'target-statefulset-spec-drift')
        assert_pod_spec(spec['template']['spec'],want['template']['spec'])


def assert_pod_spec(actual, expected):
    actual=copy.deepcopy(actual);expected=copy.deepcopy(expected)
    defaults={'dnsPolicy':'ClusterFirst','restartPolicy':'Always','schedulerName':'default-scheduler',
              'terminationGracePeriodSeconds':30,'serviceAccountName':'default','serviceAccount':'default',
              'priority':0,'preemptionPolicy':'PreemptLowerPriority'}
    for key,value in defaults.items():
        if key not in expected and key in actual:
            require(actual.pop(key)==value,'target-pod-default-drift')
    for field in ('hostNetwork','hostPID','hostIPC'):
        if actual.get(field) is False and field not in expected:actual.pop(field)
        if field in expected and expected[field] is False:actual.setdefault(field,False)
    # Kubernetes serializes integral milliCPU as CPU units (1000m ==1).
    # https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/#cpu-resource-units
    # Compare only this exact numeric equivalence; no resource field is removed.
    for spec in (actual,expected):
        for container in spec.get('containers',[]):
            for name in ('requests','limits'):
                resource=container.get('resources',{}).get(name,{})
                if not isinstance(resource,dict):continue
                cpu=resource.get('cpu')
                if isinstance(cpu,str) and re.fullmatch(r'[1-9][0-9]*m',cpu) and int(cpu[:-1])%1000==0:
                    resource['cpu']=str(int(cpu[:-1])//1000)
    for container in actual.get('containers',[]):
        for key,value in [('terminationMessagePath','/dev/termination-log'),('terminationMessagePolicy','File')]:
            if key in container:require(container.pop(key)==value,'target-container-default-drift')
    require(actual==expected,'target-pod-command-volume-security-or-environment-drift')


def assert_runtime_pod(current, desired_statefulset, statefulset_uid, node):
    """Close admission/scheduler additions around the exact reviewed template."""
    md=current.get('metadata',{});name=desired_statefulset['metadata']['name']+'-0'
    require(md.get('name')==name and md.get('namespace')==desired_statefulset['metadata']['namespace']
            and md.get('uid') and md.get('ownerReferences')==[{'apiVersion':'apps/v1','kind':'StatefulSet',
                'name':desired_statefulset['metadata']['name'],'uid':statefulset_uid,
                'controller':True,'blockOwnerDeletion':True}], 'target-runtime-pod-ownership-drift')
    template=desired_statefulset['spec']['template']
    labels=copy.deepcopy(md.get('labels',{}))
    require(labels.pop('apps.kubernetes.io/pod-index',None)=='0'
            and labels.pop('statefulset.kubernetes.io/pod-name',None)==name
            and isinstance(labels.get('controller-revision-hash'),str)
            and 0<len(labels.pop('controller-revision-hash'))<=253
            and labels==template['metadata'].get('labels',{}),'target-runtime-pod-label-drift')
    actual=copy.deepcopy(current.get('spec',{}))
    require(actual.pop('nodeName',None)==node and actual.pop('hostname',None)==name
            and actual.pop('subdomain',None)==desired_statefulset['spec']['serviceName'],
            'target-runtime-pod-node-or-address-drift')
    allowed=[{'key':'node.kubernetes.io/not-ready','operator':'Exists','effect':'NoExecute','tolerationSeconds':300},
             {'key':'node.kubernetes.io/unreachable','operator':'Exists','effect':'NoExecute','tolerationSeconds':300}]
    require(sorted(actual.pop('tolerations',[]),key=lambda v:v.get('key',''))==sorted(allowed,key=lambda v:v['key']),
            'target-runtime-pod-toleration-drift')
    assert_pod_spec(actual,template['spec'])


class Target:
    def __init__(self,value,native,authorize,record_private):
        adapter.validate(value)
        require(callable(authorize) and callable(record_private),'target-integration-gates-missing')
        self.value,self.native,self.authorize,self.record_private=value,native,authorize,record_private
        self.owned=[]
        self.namespace=value['target']['namespace']['name']

    def intents(self):
        return [{'kind':obj['kind'],'path':path(obj),'uid':uid} for obj,uid in self.owned]

    def create(self):
        require(self.authorize('telemetry-target-create') is True,'target-policy-or-fence-refused')
        objects=adapter._manifests(self.value)
        secret=next(v for v in objects if v['kind']=='Secret')
        # PostgreSQL's UTF-8 init/psql path consumes text. Arbitrary random
        # octets can contain NUL or invalid UTF-8; retain256bits in URL-safe text.
        secret['data']['bootstrap-password']=base64.b64encode(secrets.token_urlsafe(32).encode('ascii')).decode()
        primary=None
        try:
            for desired in objects:
                endpoint=path(desired)
                require(self.native.run('get',endpoint,absent=True) is None,'target-object-name-preexists')
                require(self.authorize('telemetry-target-create') is True,'target-policy-or-fence-refused')
                entry=[desired,None];self.owned.append(entry)
                current=self.native.run('create',endpoint.rsplit('/',1)[0],desired)
                entry[1]=current['metadata']['uid']
                assert_created(current,desired,entry[1])
                require(self.record_private('target-creation-progress',self.intents()) is True,'target-creation-custody-refused')
        except BaseException as error:
            primary=error
            raise
        finally:
            # Retain both the actual request failure and a failed ownership journal.
            try:
                require(self.record_private('target-creation-outcome',self.intents()) is True,'target-creation-outcome-custody-refused')
            except BaseException as error:
                if primary is not None:
                    raise BaseExceptionGroup('target creation and custody both failed',[primary,error]) from None
                raise
        return self.intents()

    def cleanup(self, owned_paths, verify_node_paths):
        """Delete only confirmed exact owned namespace after complete inventory/fence.

        owned_paths contains measured PV UID/claim UID/node path bindings, which
        the caller's qualified same-node probe must prove absent afterwards.
        """
        require(callable(verify_node_paths),'target-node-path-verifier-missing')
        result={'creationIntents':len(self.owned),'confirmedCreates':sum(uid is not None for _,uid in self.owned),
                'unknownCreateOutcomes':sum(uid is None for _,uid in self.owned),'namespaceDeleted':False,
                'targetNamespaceAbsent':False,'targetPvAbsent':False,'ownedVolumePathAbsent':False}
        namespace=next(((obj,uid) for obj,uid in self.owned if obj['kind']=='Namespace'),None)
        require(namespace is not None and namespace[1] and all(uid is not None for _,uid in self.owned),
                'target-cleanup-ownership-unverified')
        require(type(owned_paths) is list and len(owned_paths)==1 and type(owned_paths[0]) is dict
                and set(owned_paths[0])=={'pvUid','pvcUid','nodeUid','path'},
                'target-cleanup-complete-volume-path-binding-required')
        volume=owned_paths[0]
        adapter.volume({k:volume[k] for k in ('pvUid','nodeUid','path')},volume['pvUid'])
        claim_uid=next(uid for obj,uid in self.owned if obj['kind']=='PersistentVolumeClaim')
        source=self.value['source']['volumePath']
        require(source is not None and volume['pvcUid']==claim_uid
                and volume['pvUid']!=self.value['source']['pv']['uid']
                and volume['nodeUid']==source['nodeUid']
                and all(Path(volume['path'])!=p and p not in Path(volume['path']).parents
                        and Path(volume['path']) not in p.parents for p in [Path(source['path'])]),
                'target-cleanup-source-or-claim-path-binding-refused')
        pvs=self.native.run('get','/api/v1/persistentvolumes?limit=500')
        require(not pvs.get('metadata',{}).get('continue'),'target-cleanup-pv-inventory-incomplete')
        targets=[v for v in pvs['items'] if v['metadata']['uid']==volume['pvUid']]
        require(len(targets)==1 and targets[0].get('spec',{}).get('claimRef',{}).get('uid')==claim_uid
                and targets[0]['spec']['claimRef'].get('namespace')==self.namespace
                and (targets[0]['spec'].get('local') or targets[0]['spec'].get('hostPath') or {}).get('path')==volume['path'],
                'target-cleanup-current-pv-path-binding-refused')
        require(self.authorize('telemetry-target-cleanup') is True,'target-cleanup-fence-refused')
        desired,uid=namespace
        current=self.native.run('get',path(desired),absent=True)
        if current is not None:
            assert_created(current,desired,uid)
            # The authenticated caller checks complete current namespaced
            # inventory and the creation fence before approving this operation.
            self.native.run('delete',path(desired),{'apiVersion':'v1','kind':'DeleteOptions','preconditions':{
                'uid':uid,'resourceVersion':current['metadata']['resourceVersion']},'propagationPolicy':'Foreground'})
            result['namespaceDeleted']=True
            deadline=time.monotonic()+600
            while self.native.run('get',path(desired),absent=True) is not None:
                require(time.monotonic()<deadline,'target-namespace-cleanup-timeout');time.sleep(2)
        result['targetNamespaceAbsent']=True
        for obj,uid in self.owned:
            require(self.native.run('get',path(obj),absent=True) is None,'target-child-object-still-present')
        pvs=self.native.run('get','/api/v1/persistentvolumes?limit=500')
        require(not pvs.get('metadata',{}).get('continue'),'target-cleanup-pv-inventory-incomplete')
        require(not any(v.get('spec',{}).get('claimRef',{}).get('namespace')==self.namespace for v in pvs['items']),
                'target-pv-claim-still-present')
        result['targetPvAbsent']=True
        require(verify_node_paths(owned_paths) is True,'target-owned-node-path-absence-unverified')
        result['ownedVolumePathAbsent']=True
        return result
