"""Private synthetic control fixture for operator setup guard tests."""
import copy
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import yaml
import prepare
from test_seed import plan, TENANT


class Control:
    """Complete task inputs in a disposable root, with no real credentials or services."""
    def __enter__(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='mcpcli-417-guards-')
        self.directory = Path(self.temporary.name)
        self.root = self.directory / 'platform'; self.root.mkdir()
        for target in ('eng/helm-qualification/chart', 'eng/mcpcli-operator-setup/chart'):
            shutil.copytree(prepare.ROOT / target, self.root / target)
        self.graph = self.root / 'eng/mcpcli-operator-setup/chart/reference-graph.json'
        self.seed = self.directory / 'private-seed.json'
        self.seed.write_text(json.dumps(plan())); self.seed.chmod(0o600)
        self.secret = self.directory / 'private-secret'; self.secret.write_text('synthetic'); self.secret.chmod(0o600)
        self.registry = self.directory / 'registry.json'; self.registry.write_text('{}'); self.registry.chmod(0o600)
        self.manifest = b'{"schemaVersion":2}'
        self.image = 'registry.internal/task@sha256:' + hashlib.sha256(self.manifest).hexdigest()
        graph = prepare.read(self.graph)
        self.profile = prepare.read(prepare.HERE / 'profile.template.json')
        self.profile.update(sourceRevision='a' * 40, sourceHashes=prepare.source_inputs(self.root),
            canonicalGraphSha256=prepare.sha(self.graph),
            expiresAt=(datetime.now(timezone.utc) + timedelta(hours=1)).isoformat(), tenant=TENANT,
            operatorSubject='operator-user', privateSeedFile=str(self.seed), registryAuthFile=str(self.registry))
        self.profile['target'].update(namespace=TENANT, release=TENANT)
        self.profile['prerequisites'] = {k:'synthetic-binding' for k in self.profile['prerequisites']}
        self.profile['values'].update(images={w['identity']:self.image for w in graph['workloads']},
            bindings={w['identity']:{} for w in graph['workloads']}, imagePullSecret='task-registry')
        self.profile['values']['environment']['componentConfigMap'] = 'task-components'
        self.profile['privateSecretFiles'] = {'task-registry':{'.dockerconfigjson':str(self.secret)},
                                               'task-channel':{'token':str(self.secret)}}
        self.profile['environmentManifests'] = [
            self.document('ConfigMap', 'task-components', data={'components.yaml':'synthetic'}),
            self.document('PersistentVolumeClaim', 'task-data', spec={'resources':{'requests':{'storage':'1Gi'}}}),
            self.document('Service', 'task-data', spec={'type':'ClusterIP'}),
            self.document('StatefulSet', 'task-data', spec={'replicas':1,'template':{'spec':{
                'containers':[{'name':'data','image':self.image,'resources':{
                    'requests':{'cpu':'100m','memory':'128Mi'},'limits':{'cpu':'1','memory':'256Mi'}},
                    'securityContext':{'privileged':False},'env':[]}], 'volumes':[]}}})]
        self.profile_path = self.directory / 'profile.json'; self.output = self.directory / 'review'
        self.calls = []
        self.rendered = []
        for workload in graph['workloads']:
            self.rendered.append({'apiVersion':'apps/v1','kind':'Deployment','metadata':{'name':workload['resourceId']},
                'spec':{'template':{'metadata':{'annotations':{}},'spec':{'volumes':[{'emptyDir':{}}],
                    'containers':[{'env':[{'name':'TOKEN','valueFrom':{'secretKeyRef':{'name':'task-channel','key':'token'}}}]}]}}}})
        self.review_path = self.directory / 'approved-review.json'
        self.review = {'schema':'hexalith.mcpcli.operator-review/v1','target':{'namespace':TENANT},'tenant':TENANT,
            'expiresAt':self.profile['expiresAt'],'seed':{'privatePlanSha256':prepare.sha(self.seed)}}
        self.save_profile(); self.save_review()
        return self

    def __exit__(self, *unused):
        self.temporary.cleanup()

    def document(self, kind, name, **fields):
        return {'apiVersion':'v1','kind':kind,'metadata':{'name':name,'namespace':TENANT,
            'labels':{'hexalith.com/operator-task':TENANT}},**fields}

    def save_profile(self):
        self.profile_path.write_text(json.dumps(self.profile))

    def save_review(self):
        self.review_path.write_text(json.dumps(self.review))
        self.review_sha = prepare.sha(self.review_path)

    def rebind_source(self):
        self.profile['sourceHashes'] = prepare.source_inputs(self.root)
        self.profile['canonicalGraphSha256'] = prepare.sha(self.graph)

    def fake_run(self, argv):
        self.calls.append(argv)
        if argv[1:3] == ['version','--short']: return b'v4.2.0+synthetic'
        if argv[1:3] == ['template','mcpcli-417']: return yaml.safe_dump_all(self.rendered).encode()
        if argv[1:3] == ['inspect','--raw']: return self.manifest
        raise AssertionError('Unexpected external command')
