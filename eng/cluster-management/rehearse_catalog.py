"""Actual-chart retirement with a synthetic source-sized catalog and fresh-node rollback.

Recovered from the private executed driver SHA-256
35d4724c8dcc6e7fbeabcd10f686219e06c3eba70d41ec88c05b9ab1a7fbf38b.
Only local setup was changed to the explicit rehearsal CLI and shared custody lifecycle.
"""
from pathlib import Path

from rehearse import Fixture, content_review_digest, parse_args, rehearse


def catalog_counts(source):
    counts = {kind: sum((v['apiVersion'], v['kind']) == ('application.kubesphere.io/v2', kind)
                        for v in source['resources'])
              for kind in ('Repo', 'Application', 'ApplicationVersion', 'Category')}
    if counts != {'Repo': 1, 'Application': 27, 'ApplicationVersion': 90, 'Category': 1}:
        raise ValueError('source-catalog-counts-changed')
    return counts


class RepresentativeFixture(Fixture):
    def populate_retirement_decisions(self):
        self.catalog_counts = catalog_counts(self.args.source)
        super().populate_retirement_decisions()
        category_name, repo_name = 's426-catalog-category', 's426-catalog-repo'
        self.create({'apiVersion': 'application.kubesphere.io/v2', 'kind': 'Category',
                        'metadata': {'name': category_name, 'finalizers': ['categories.application.kubesphere.io/finalizer']}, 'spec': {}})
        self.create({'apiVersion': 'application.kubesphere.io/v2', 'kind': 'Repo',
                        'metadata': {'name': repo_name, 'labels': {'kubesphere.io/workspace': 'system-workspace',
                            'application.kubesphere.io/sync-app-store': 'true', 'app.kubernetes.io/managed-by': 'Helm'},
                        'annotations': {'meta.helm.sh/release-name': 'ks-core', 'meta.helm.sh/release-namespace': 'kubesphere-system'}},
                        'spec': {'url': 'http://127.0.0.1:9/synthetic-index.yaml', 'syncPeriod': 900}})
        repo = self.get('repos.application.kubesphere.io', repo_name)
        owner = lambda raw: {k: raw[k] for k in ('apiVersion', 'kind')} | {
            'name': raw['metadata']['name'], 'uid': raw['metadata']['uid'], 'controller': True, 'blockOwnerDeletion': True}
        application_objects = []
        for index in range(27):
            name = 's426-catalog-app-' + str(index + 1)
            labels = {'application.kubesphere.io/app-type': 'helm', 'application.kubesphere.io/repo-name': repo_name,
                      'application.kubesphere.io/app-category-name': category_name, 'application.kubesphere.io/app-store': 'true',
                      'kubesphere.io/workspace': 'system-workspace'}
            application_objects.append({'apiVersion': 'application.kubesphere.io/v2', 'kind': 'Application',
                            'metadata': {'name': name, 'labels': labels, 'ownerReferences': [owner(repo)]},
                            'spec': {'appType': 'helm', 'abstraction': 'synthetic fixture only'}})
        self.create({'apiVersion': 'v1', 'kind': 'List', 'items': application_objects})
        applications = sorted([v for v in self.get('applications.application.kubesphere.io')['items']
                               if v['metadata']['name'].startswith('s426-catalog-app-')], key=lambda v: v['metadata']['name'])
        if len(applications) != 27:
            raise ValueError('synthetic-catalog-application-creation-count-mismatch')
        version_objects = []
        for index in range(90):
            app = applications[index % len(applications)]
            labels = {'application.kubesphere.io/app-type': 'helm', 'application.kubesphere.io/repo-name': repo_name,
                      'application.kubesphere.io/app-id': app['metadata']['name'], 'kubesphere.io/workspace': 'system-workspace'}
            version_objects.append({'apiVersion': 'application.kubesphere.io/v2', 'kind': 'ApplicationVersion',
                            'metadata': {'name': 's426-catalog-version-' + str(index + 1), 'labels': labels,
                                         'ownerReferences': [owner(app)], 'finalizers': ['application.kubesphere.io/cleanup']},
                            'spec': {'appType': 'helm', 'versionName': '0.0.' + str(index + 1),
                                     'pullUrl': 'http://127.0.0.1:9/synthetic-chart.tgz',
                                     'digest': '0' * 64}})
        self.create({'apiVersion': 'v1', 'kind': 'List', 'items': version_objects})
        self.catalog_category = self.get('categories.application.kubesphere.io', category_name)
        self.attempt.record('catalog-cohort.json', {'sourceCounts': self.catalog_counts, 'syntheticCounts': self.catalog_counts,
            'sourceInventorySha256': self.args.source_digest, 'schema': 'actual chart application.kubesphere.io/v2 CRDs',
            'ownerGraph': 'Repo -> 27 Applications -> 90 ApplicationVersions',
            'applicationVersionFinalizer': 'application.kubesphere.io/cleanup',
            'retainedCategoryFinalizer': 'categories.application.kubesphere.io/finalizer',
            'applicationPayload': 'synthetic metadata only; unreachable loopback URLs',
            'sourceCredentialsOrValuesImported': False, 'productionAccepted': False})
    def retire_phase(self, phase, baseline, by_uid, resources):
        if phase['phase'] == 'application-store':
            members = [by_uid[uid] for uid in phase['expected']]
            actual = {kind: len([v for v in members if v['kind'] == kind])
                      for kind in ('Repo', 'Application', 'ApplicationVersion')}
            if actual != {'Repo': 1, 'Application': 27, 'ApplicationVersion': 90} or len(members) != 118:
                raise ValueError('catalog-phase-not-exact')
            if any('application.kubesphere.io/cleanup' not in v['finalizers'] for v in members
                   if v['kind'] == 'ApplicationVersion'):
                raise ValueError('catalog-version-finalizer-pattern-not-represented')
        after = super().retire_phase(phase, baseline, by_uid, resources)
        if phase['phase'] == 'application-store':
            current = self.get('categories.application.kubesphere.io', self.catalog_category['metadata']['name'])
            if content_review_digest(self.catalog_category) != content_review_digest(current):
                raise ValueError('catalog-archive-category-drift')
            self.attempt.record('catalog-result.json', {'state': 'passed', 'sourceCounts': self.catalog_counts,
                'syntheticCounts': actual, 'exactRemovals': 118, 'applicationVersionCleanupFinalizersProcessed': 90,
                'categoryIdentityContentAndFinalizerPreserved': True, 'protectedStorageAndNamespacesPreserved': True,
                'nativeApplicationStorePhaseUsed': True, 'licensedApplicationWritesUsed': False,
                'manualFinalizerRemovalUsed': False, 'productionCredentialsOrDataImported': False,
                'productionAccepted': False})
        return after


def main():
    args = parse_args()
    if not args.ks_chart or not args.rollback:
        raise SystemExit('Representative rehearsal requires --ks-chart and --rollback.')
    directory, state = rehearse(args, fixture_type=RepresentativeFixture, driver_path=Path(__file__))
    print(f'Private fixture evidence: {directory}\nRehearsal: {state}; production retirement remains unaccepted')
    if state.startswith('failed'):
        raise SystemExit(2)


if __name__ == '__main__':
    main()
