"""Exact target-name creation deny with UID-checked lifecycle; no backend or implicit authority.

The admission expression denies CREATE for the exact namespace name, including
a same-name replacement until fence cleanup. Namespace UID is checked separately
by assert_bound before data access/cleanup; CEL does not expose that UID on1.34.
Actual enforcement, complete inventory and per-dispatch freshness must be
qualified separately. Kubernetes1.34 CEL namespaceObject reference:
https://v1-34.docs.kubernetes.io/docs/reference/access-authn-authz/validating-admission-policy/
"""
import copy,re

def require(ok,why):
 if not ok:raise ValueError(why)

def manifests(name,namespace,namespace_uid):
 require(all(isinstance(v,str) and re.fullmatch(r'[a-z0-9][a-z0-9.-]{0,62}',v) for v in (name,namespace)) and isinstance(namespace_uid,str) and re.fullmatch('[a-f0-9-]{36}',namespace_uid),'telemetry-fence-scope-refused')
 md={'name':name,'annotations':{'hexalith.io/target-namespace':namespace,'hexalith.io/target-namespace-uid':namespace_uid}}
 policy={'apiVersion':'admissionregistration.k8s.io/v1','kind':'ValidatingAdmissionPolicy','metadata':md,'spec':{'failurePolicy':'Fail','matchConstraints':{'matchPolicy':'Equivalent','namespaceSelector':{},'objectSelector':{},'resourceRules':[{'apiGroups':['*'],'apiVersions':['*'],'operations':['CREATE'],'resources':['*/*'],'scope':'Namespaced'}]},'matchConditions':[{'name':'exact-target-namespace-name','expression':'request.namespace == '+repr(namespace)}],'validations':[{'expression':'false','message':'s427 exact target namespace creation fence','reason':'Forbidden'}]}}
 binding={'apiVersion':'admissionregistration.k8s.io/v1','kind':'ValidatingAdmissionPolicyBinding','metadata':copy.deepcopy(md),'spec':{'policyName':name,'validationActions':['Deny'],'matchResources':{'matchPolicy':'Equivalent','namespaceSelector':{},'objectSelector':{}}}}
 return policy,binding

def assert_bound(current,desired,uid,namespace):
 require(current.get('kind')==desired['kind'] and current.get('apiVersion')==desired['apiVersion'] and current.get('metadata',{}).get('uid')==uid and uid and current['metadata'].get('name')==desired['metadata']['name'] and current['metadata'].get('annotations',{})==desired['metadata']['annotations'] and not current['metadata'].get('ownerReferences') and not current['metadata'].get('finalizers') and current.get('spec')==desired['spec'],'telemetry-fence-policy-or-binding-drift')
 require(namespace.get('metadata',{}).get('name')==desired['metadata']['annotations']['hexalith.io/target-namespace'] and namespace['metadata']['uid']==desired['metadata']['annotations']['hexalith.io/target-namespace-uid'],'telemetry-fence-target-namespace-replaced')
 if desired['kind']=='ValidatingAdmissionPolicy':require(not current.get('status',{}).get('typeChecking',{}).get('expressionWarnings'),'telemetry-fence-expression-type-warning')

def typed_denial(result,policy_name,binding_name):
 text=result.stderr.decode(errors='replace')
 return result.returncode==1 and 'Forbidden' in text and 'ValidatingAdmissionPolicy' in text and policy_name in text and binding_name in text and 's427 exact target namespace creation fence' in text
