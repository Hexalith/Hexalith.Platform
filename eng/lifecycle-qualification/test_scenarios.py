"""Resource-free checks of the scenario driver's summary extraction and row verdicts."""
import copy
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))
import scenarios  # noqa: E402

OWNER, ATTACHED, PEER, ENVIRONMENT = 'a' * 32, 'b' * 32, 'c' * 32, 'e' * 32
TERMINALS = {'passed': 'tests-completed', 'failed': 'test-failure', 'cancelled': 'run-cancelled', 'withdrawn': 'hold-expired'}


def document(outcome='passed', cause=None, run_id=OWNER, role='owner', retention='not-retained', cleanup='complete',
             leftovers=(), actual=181.25, override='not-overridden', recorded='2026-11-18T10:00:00.0000000Z'):
    """A sample hexalith.test-result.v1 document with the fields 6.3.0 publishes."""
    return {
        'schema': 'hexalith.test-result.v1', 'policyId': 'hexalith.platform.candidate-and-attachment-policy.v1',
        'bindings': {
            'run': {'runId': run_id, 'role': role, 'ownerRunId': OWNER, 'environmentId': ENVIRONMENT, 'attemptId': 'f' * 32,
                    'attemptSequence': 1},
            'tool': {'state': 'populated', 'name': 'hexalith-module', 'version': '6.3.0', 'contentIdentity': 'sha256:' + '1' * 64},
            'deadlines': {'startup': {'state': 'populated', 'origin': 'environment-requested', 'effectiveSeconds': 600,
                                      'actualDurationSeconds': actual, 'override': {'state': override},
                                      'originAt': '2026-11-18T09:50:00Z', 'expiresAt': '2026-11-18T10:00:00Z'}},
            'environment': {'executionContext': {'kind': 'local'}, 'mode': 'package'},
            'attachments': {'state': 'populated', 'holds': []}},
        'terminal': {'outcome': outcome, 'cause': cause or TERMINALS[outcome], 'recordedAt': recorded, 'diagnosticIds': ['lifecycle']},
        'retention': {'state': 'populated', 'disposition': retention, 'failureRunIds': [], 'retainedResourceIds': []},
        'cleanup': {'state': 'populated', 'disposition': cleanup, 'observedAt': '2026-11-18T10:00:30Z',
                    'leftoverResourceIds': list(leftovers), 'ownedResourceIds': ['resource:redis'], 'failures': []},
        'diagnostics': [{'id': 'lifecycle', 'reference': 'lifecycle-0.json'}]}


def run(doc, exit_code=0, retention_at_exit=None, containers_at_exit=0, final=None, rules=(), run_id=OWNER):
    first = scenarios.extract(doc)
    return {'runId': run_id, 'environmentId': ENVIRONMENT, 'exitCode': exit_code, 'ruleIds': list(rules), 'first': first,
            'final': scenarios.extract(final or doc), 'listedAtExit': {'retention': retention_at_exit} if retention_at_exit else None,
            'containersAtExit': containers_at_exit, 'containersAfter': 0, 'listedAfter': False}


def row(scenario, runs, facts=None):
    return {'scenario': scenario, 'runs': [scenarios.summarize(role, value) for role, value in runs.items()],
            'facts': facts or {}, 'foreign': {'unchanged': True, 'containers': 5, 'listEntries': 28}}


def passing_rows():
    retained = document('failed', retention='failure-retained', cleanup='not-requested')
    released = document('failed', retention='released')
    timeout = document('failed', 'startup-timeout', retention='failure-retained', cleanup='not-requested', actual=600.4)
    timed_out = document('failed', 'startup-timeout', retention='released', actual=600.4)
    attached_failed = document('failed', run_id=ATTACHED, role='attached')
    return {
        'success': row('success', {'owner': run(document())}),
        'failure': row('failure', {'owner': run(retained, 4, 'failure-retained', 6, released)}),
        'startup-timeout': row('startup-timeout', {'owner': run(timeout, 3, 'failure-retained', 4, timed_out, ['HXR021'])},
                               {'startupFailureRecorded': True, 'unreadyResources': ['parties']}),
        'cancellation': row('cancellation', {'owner': run(document('cancelled'), 130)}),
        'collision': row('collision', {'owner': run(document()), 'peer': {'exitCode': 3, 'ruleIds': ['HXR031']}},
                         {'namesFirstRun': True, 'firstUnaffected': True}),
        'owner-finish-hold': row('owner-finish-hold', {'owner': run(document())},
                                 {'holdAdmitted': True, 'passedWhileHeld': True, 'cleanupPending': True,
                                  'refusal': ['HXR035', 'owner-passed'], 'holdEnded': True}),
        'owner-cancel-attached': row('owner-cancel-attached',
                                     {'owner': run(document('cancelled'), 130), 'attached': run(document('withdrawn', 'owner-cancelled', ATTACHED, 'attached'), 130, run_id=ATTACHED)},
                                     {'attachedEnd': ['withdrawn', 'owner-cancelled'], 'withdrawnBeforeTeardown': True}),
        'attached-failure-expiry': row('attached-failure-expiry',
                                       {'owner': run(document(retention='failure-retained', cleanup='not-requested'), 0, 'failure-retained', 6, document(retention='released')),
                                        'attached': run(attached_failed, 4, run_id=ATTACHED),
                                        'expiring': run(document('withdrawn', run_id=PEER, role='attached'), 130, run_id=PEER)},
                                       {'retainedAfterAttachedFailure': True, 'ownerUntouched': True, 'expiryEnd': ['withdrawn', 'expired'],
                                        'expiryUnchanged': True, 'retryRefused': True}),
        'failure-then-cancellation': row('failure-then-cancellation',
                                         {'owner': run(document('cancelled', retention='failure-retained', cleanup='not-requested'), 130, 'failure-retained', 6,
                                                       document('cancelled', retention='released')),
                                          'attached': run(attached_failed, 4, run_id=ATTACHED)},
                                         {'attachmentUnchanged': True, 'retentionUnchanged': True, 'secondEndFirstOutcome': True}),
    }


class ManifestSuitesTests(unittest.TestCase):
    def manifest(self, tests):
        return {'schema': 'hexalith.module-manifest.v2', 'modules': [{'tests': tests}]}

    def test_resolves_renamed_and_legacy_attached_suite_paths_in_profile_order(self):
        renamed = self.manifest([
            {'name': 'attached-lifecycle-package',
             'entry': 'tests/Hexalith.Parties.Platform.AttachedLifecycleTests/Hexalith.Parties.Platform.AttachedLifecycleQualification.Tests.csproj'},
            {'name': 'platform-package', 'entry': 'tests/Other/Other.csproj'},
            {'name': 'lifecycle-package',
             'entry': 'tests/Hexalith.Parties.Platform.LifecycleTests/Hexalith.Parties.Platform.LifecycleTests.csproj'},
        ])
        self.assertEqual(
            scenarios.manifest_suites(renamed),
            ('tests/Hexalith.Parties.Platform.LifecycleTests/Hexalith.Parties.Platform.LifecycleTests.csproj',
             'tests/Hexalith.Parties.Platform.AttachedLifecycleTests/Hexalith.Parties.Platform.AttachedLifecycleQualification.Tests.csproj'),
        )
        legacy = self.manifest([
            {'name': 'lifecycle-package', 'entry': 'tests/Owner/Owner.csproj'},
            {'name': 'attached-lifecycle-package', 'entry': 'tests/Attached/Attached.csproj'},
        ])
        self.assertEqual(scenarios.manifest_suites(legacy), ('tests/Owner/Owner.csproj', 'tests/Attached/Attached.csproj'))

    def test_rejects_missing_blank_duplicate_or_unsafe_suite_entries(self):
        cases = (
            (None, 'missing or invalid'),
            (self.manifest([{'name': 'lifecycle-package', 'entry': 'tests/Owner.csproj'}]), 'missing test profiles: attached-lifecycle-package'),
            (self.manifest([{'name': 'lifecycle-package', 'entry': ''},
                            {'name': 'attached-lifecycle-package', 'entry': 'tests/Attached.csproj'}]), 'has no entry path'),
            (self.manifest([{'name': 'lifecycle-package', 'entry': '/etc/passwd'},
                            {'name': 'attached-lifecycle-package', 'entry': 'tests/Attached.csproj'}]), 'invalid relative entry path'),
            (self.manifest([{'name': 'lifecycle-package', 'entry': '../escape.csproj'},
                            {'name': 'attached-lifecycle-package', 'entry': 'tests/Attached.csproj'}]), 'invalid relative entry path'),
            (self.manifest([{'name': 'lifecycle-package', 'entry': 'tests/A.csproj'},
                            {'name': 'lifecycle-package', 'entry': 'tests/B.csproj'},
                            {'name': 'attached-lifecycle-package', 'entry': 'tests/C.csproj'}]), 'duplicate test profile'),
        )
        for doc, pattern in cases:
            with self.subTest(pattern=pattern), self.assertRaisesRegex(ValueError, pattern):
                scenarios.manifest_suites(doc)

    def test_prepare_builds_suites_declared_in_manifest(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            checkout, work = root / 'parties', root / 'work'
            work.mkdir()
            manifest_path = checkout / scenarios.MANIFEST
            manifest_path.parent.mkdir(parents=True)
            manifest_path.write_text(json.dumps(self.manifest([
                {'name': 'lifecycle-package', 'entry': 'tests/Owner/Owner.csproj'},
                {'name': 'attached-lifecycle-package', 'entry': 'tests/Attached/AttachedQualification.Tests.csproj'},
            ])))
            driver = scenarios.Driver(checkout, work)
            with (patch.object(driver, 'tool', return_value=(0, {'buildPreparation': {'environment': {'X': '1'}, 'mappingHash': 'h'}})),
                  patch.object(scenarios.subprocess, 'run', return_value=SimpleNamespace(returncode=0)) as run_cmd):
                self.assertEqual(driver.prepare(), 'h')
            built = [call.args[0][2] for call in run_cmd.call_args_list]
            self.assertEqual(built, ['tests/Owner/Owner.csproj', 'tests/Attached/AttachedQualification.Tests.csproj'])


class ExtractTests(unittest.TestCase):
    def test_projects_only_bounded_public_fields(self):
        value = scenarios.extract(document('failed', 'startup-timeout', retention='failure-retained', leftovers=['resource:redis']))
        self.assertEqual(value['runId'], OWNER)
        self.assertEqual(value['environmentId'], ENVIRONMENT)
        self.assertEqual((value['toolVersion'], value['toolContentIdentity']), ('6.3.0', 'sha256:' + '1' * 64))
        self.assertEqual((value['outcome'], value['cause']), ('failed', 'startup-timeout'))
        self.assertEqual(value['startup'], {'effectiveSeconds': 600, 'actualDurationSeconds': 181.25, 'override': 'not-overridden'})
        self.assertEqual((value['retention'], value['cleanup'], value['leftovers']), ('failure-retained', 'complete', ['resource:redis']))
        self.assertEqual(value['diagnosticIds'], ['lifecycle'])
        self.assertNotIn('diagnostics', value)

    def test_unpopulated_or_missing_documents(self):
        self.assertIsNone(scenarios.extract(None))
        sparse = scenarios.extract({'terminal': {'outcome': 'failed'}})
        self.assertEqual(sparse['startup'], {'effectiveSeconds': None, 'actualDurationSeconds': None, 'override': None})
        self.assertEqual(sparse['leftovers'], [])

    def test_hold_view_and_command_output(self):
        attachment = {'attachedRunId': ATTACHED, 'originalAttachmentRequestId': PEER, 'requestedHoldSeconds': 45,
                      'effectiveHoldSeconds': 45, 'acceptedAtUtc': 'x', 'expiresAtUtc': 'y', 'endedAtUtc': 'y',
                      'endReason': 'expired', 'outcome': 'withdrawn', 'execution': {'profile': 'attached-lifecycle-package'},
                      'termination': {'ownedWork': []}}
        view = scenarios.hold_view(attachment)
        self.assertEqual(view['kind'], 'attached-suite')
        self.assertNotIn('termination', view)
        self.assertEqual(scenarios.hold_view({**attachment, 'execution': None})['kind'], 'hold-only')
        output = scenarios.last_json('noise\n' + json.dumps({'outcome': {'ruleId': 'HXR035'}, 'diagnostics': [
            {'ruleId': 'HXR035', 'field': 'owner-passed', 'message': '/home/someone/x'}]}))
        self.assertEqual(scenarios.rule_ids(output), ['HXR035'])
        self.assertEqual(scenarios.reasons(output), ['owner-passed'])
        self.assertEqual(scenarios.last_json(''), {})

    def test_first_outcome_stability_compares_terminal_fields(self):
        stable = scenarios.summarize('owner', run(document('failed'), final=document('failed', retention='released')))
        self.assertTrue(stable['firstOutcomeStable'])
        moved = scenarios.summarize('owner', run(document('failed'), final=document('failed', recorded='2026-11-18T10:05:00Z')))
        self.assertFalse(moved['firstOutcomeStable'])

    def test_startup_failure_facts_accepts_startup_failure_or_runner_summary_report(self):
        failure = {'ruleId': 'HXR021', 'resource': 'parties',
                   'unreadyResources': [{'name': 'parties', 'state': 'Unhealthy'}, {'name': 'redis', 'state': 'Healthy'}]}
        self.assertEqual(scenarios.startup_failure_facts(failure, None),
                         {'startupFailureRecorded': True, 'failingResource': 'parties', 'unreadyResources': ['parties']})
        report = {'schema': 'hexalith.g4-startup-report.v1', 'ruleId': 'HXR021',
                  'resources': [{'name': 'redis', 'state': 'Healthy'}, {'name': 'eventstore', 'state': 'Unhealthy'},
                                {'name': 'parties', 'state': 'Unhealthy'}]}
        self.assertEqual(scenarios.startup_failure_facts(None, report),
                         {'startupFailureRecorded': True, 'failingResource': 'eventstore', 'unreadyResources': ['eventstore', 'parties']})
        self.assertEqual(scenarios.startup_failure_facts(None, {'schema': 'hexalith.g4-startup-report.v1', 'ruleId': 'HXR021',
                                                                'resources': [{'name': 'redis', 'state': 'Healthy'}]}),
                         {'startupFailureRecorded': False, 'failingResource': None, 'unreadyResources': None})
        self.assertEqual(scenarios.startup_failure_facts(None, None),
                         {'startupFailureRecorded': False, 'failingResource': None, 'unreadyResources': None})


class VerdictTests(unittest.TestCase):
    def test_every_scenario_has_checks_and_its_passing_sample_passes(self):
        rows = passing_rows()
        self.assertEqual(set(rows), set(scenarios.SCENARIOS))
        self.assertEqual(set(scenarios.CHECKS), set(scenarios.SCENARIOS))
        for name, value in rows.items():
            with self.subTest(name):
                self.assertEqual(scenarios.verdict(name, value, '6.3.0'), [])

    def mutate(self, name, change):
        value = copy.deepcopy(passing_rows()[name])
        change(value)
        return scenarios.verdict(name, value, '6.3.0')

    def owner(self, value):
        return scenarios._run(value, 'owner')

    def test_wrong_outcomes_and_exit_codes_fail(self):
        self.assertEqual(self.mutate('success', lambda r: self.owner(r).update(outcome='failed', cause='test-failure')),
                         ['passed', 'tests-completed'])
        self.assertEqual(self.mutate('failure', lambda r: self.owner(r).update(exitCode=3)), ['exit-4'])
        self.assertEqual(self.mutate('cancellation', lambda r: self.owner(r).update(exitCode=4, outcome='failed')),
                         ['exit-130', 'cancelled'])

    def test_leftovers_and_listing_fail_cleanup(self):
        self.assertEqual(self.mutate('success', lambda r: self.owner(r).update(leftovers=['resource:redis'])), ['clean'])
        self.assertEqual(self.mutate('success', lambda r: self.owner(r).update(listedAfter=True)), ['clean'])
        self.assertEqual(self.mutate('cancellation', lambda r: self.owner(r).update(containersAtExit=2)), ['no-leftovers-at-exit'])
        self.assertEqual(self.mutate('failure', lambda r: self.owner(r).update(retentionAtExit='not-retained')), ['failure-retained'])

    def test_startup_deadline_requires_the_untouched_default(self):
        self.assertEqual(self.mutate('startup-timeout', lambda r: self.owner(r)['startup'].update(override='declared')),
                         ['not-overridden'])
        self.assertEqual(self.mutate('startup-timeout', lambda r: self.owner(r)['startup'].update(actualDurationSeconds=420.0)),
                         ['default-600-elapsed'])
        self.assertEqual(self.mutate('startup-timeout', lambda r: self.owner(r)['startup'].update(effectiveSeconds=900)),
                         ['default-600-elapsed'])
        self.assertEqual(self.mutate('startup-timeout', lambda r: self.owner(r).update(firstOutcomeStable=False)),
                         ['first-outcome-stable'])

    def test_attachment_rows_require_their_recorded_facts(self):
        self.assertEqual(self.mutate('owner-finish-hold', lambda r: r['facts'].update(refusal=['HXR035', 'owner-finished'])),
                         ['refused-owner-passed'])
        self.assertEqual(self.mutate('owner-cancel-attached', lambda r: r['facts'].update(attachedEnd=['cancelled', 'terminal-outcome'])),
                         ['attached-withdrawn'])
        self.assertEqual(self.mutate('attached-failure-expiry', lambda r: scenarios._run(r, 'expiring').update(cause='owner-cancelled')),
                         ['hold-expired'])
        self.assertEqual(self.mutate('collision', lambda r: scenarios._run(r, 'peer').update(ruleIds=['HXR021'])), ['refused-HXR031'])
        self.assertEqual(self.mutate('failure-then-cancellation', lambda r: r['facts'].update(secondEndFirstOutcome=False)),
                         ['second-end-first-outcome'])

    def test_foreign_change_tool_version_and_driver_error_fail(self):
        self.assertEqual(self.mutate('success', lambda r: r['foreign'].update(unchanged=False)), ['foreign-unchanged'])
        self.assertEqual(self.mutate('success', lambda r: self.owner(r).update(toolVersion='6.2.0')), ['tool-version'])
        self.assertEqual(self.mutate('success', lambda r: r.update(error='owner execution: process exited first')),
                         ['driver:owner execution: process exited first'])


class RenderTests(unittest.TestCase):
    def test_readme_links_ci_runs_states_deferral_and_has_no_paths(self):
        rows = list(passing_rows().values())
        for value in rows:
            value.update(verdict='passed', failedChecks=[])
        text = scenarios.render({'tool': {'version': '6.3.0', 'contentIdentity': 'sha256:' + '1' * 64},
                                 'source': {'revision': '8' * 40, 'buildsRevision': '7' * 40}, 'rows': rows})
        for url in scenarios.CI_RUNS:
            self.assertIn(url, text)
        self.assertIn('Story 2.14 and CiCd story 21', text)
        self.assertEqual(text.count('| passed |'), 9)
        self.assertNotIn('/home/', text)
        self.assertNotIn('/tmp/', text)


class CampaignTests(unittest.TestCase):
    def test_product_refusal_skips_all_remaining_rows(self):
        source = {'revision': 'a' * 40, 'buildsRevision': 'b' * 40}
        tool = {'version': '6.4.4'}
        with (tempfile.TemporaryDirectory() as directory,
              patch.object(scenarios, 'Driver') as driver_type,
              patch.object(scenarios.signal, 'signal'),
              redirect_stdout(io.StringIO())):
            driver = driver_type.return_value
            driver.preflight.return_value = source, tool
            driver.prepare.return_value = 'c' * 64
            driver.row.return_value = {'scenario': 'success', 'verdict': 'failed', 'refusal': True,
                                       'failedChecks': ['driver:HXP020'], 'runs': []}
            driver.residue.return_value = {'listedIssuedRuns': [], 'issuedContainers': 0}
            root = Path(directory)
            evidence = root / 'evidence'

            result = scenarios.main(['--checkout', str(root / 'parties'), '--evidence', str(evidence)])
            summary = json.loads((evidence / 'summary.json').read_text())

        self.assertEqual(result, 1)
        driver.row.assert_called_once_with('success', source['revision'], tool['version'])
        driver.release.assert_called_once()
        self.assertEqual([row['scenario'] for row in summary['rows']], list(scenarios.SCENARIOS))
        self.assertEqual([row['verdict'] for row in summary['rows']], ['failed'] + ['not-run'] * 8)

    def test_attached_exit_before_readiness_records_diagnostics_and_stops_campaign(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            controls = root / 'controls'
            owner_ready = controls / OWNER / 'execution-ready.json'
            owner_ready.parent.mkdir(parents=True)
            owner_ready.touch()
            output = root / 'attached.json'
            output.write_text(json.dumps({'diagnostics': [{'ruleId': 'HXT006'}]}) + '\n')
            driver = scenarios.Driver(root / 'parties', root / 'work')
            owner = {'label': 'owner', 'runId': OWNER, 'proc': SimpleNamespace(poll=lambda: None)}
            attached = {'label': 'attached', 'runId': ATTACHED, 'stdout': output,
                        'proc': SimpleNamespace(poll=lambda: 4, wait=lambda _: 4)}
            snapshot = {'containers': [], 'list': []}

            def admit_attached(*_):
                driver.current['attached'] = attached
                return attached

            def observe_result(run_id):
                release.assert_called_once()
                self.assertEqual(run_id, ATTACHED)
                return scenarios.extract(document('failed', run_id=ATTACHED, role='attached'))

            with (patch.object(scenarios, 'Driver', return_value=driver),
                  patch.object(scenarios, 'CONTROLS', controls),
                  patch.object(scenarios.signal, 'signal'),
                  patch.object(driver, 'preflight', return_value=(
                      {'revision': 'a' * 40, 'buildsRevision': 'b' * 40}, {'version': '6.3.0'})),
                  patch.object(driver, 'owner', return_value=owner),
                  patch.object(driver, 'attached', side_effect=admit_attached) as admit,
                  patch.object(driver, 'foreign', return_value=snapshot),
                  patch.object(driver, 'release') as release,
                  patch.object(driver, 'residue', return_value={'listedIssuedRuns': [], 'issuedContainers': 0}),
                  patch.object(driver, 'result', side_effect=observe_result),
                  redirect_stdout(io.StringIO())):
                evidence = root / 'evidence'
                result = scenarios.main(['--checkout', str(driver.checkout), '--evidence', str(evidence),
                                         '--scenario', 'owner-cancel-attached', '--scenario', 'attached-failure-expiry',
                                         '--skip-prepare'])
                summary = json.loads((evidence / 'summary.json').read_text())

        self.assertEqual(result, 1)
        admit.assert_called_once()
        self.assertEqual(release.call_count, 2)
        self.assertEqual([row['verdict'] for row in summary['rows']], ['failed', 'not-run'])
        row = summary['rows'][0]
        self.assertEqual(row['failedChecks'], ['driver:attached refused before execution: HXT006'])
        self.assertEqual(row['runs'][0]['exitCode'], 4)
        self.assertEqual(row['runs'][0]['ruleIds'], ['HXT006'])
        self.assertEqual(row['runs'][0]['diagnosticIds'], ['lifecycle'])
        self.assertTrue(row['foreign']['unchanged'])

    def test_attached_readiness_timeout_and_failure_after_readiness_are_not_refusals(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            driver = scenarios.Driver(root / 'parties', root / 'work')
            run = {'label': 'attached', 'runId': ATTACHED, 'proc': SimpleNamespace(poll=lambda: None)}
            with patch.object(scenarios, 'CONTROLS', root):
                with self.assertRaisesRegex(scenarios.Failure, 'attached execution: timed out') as error:
                    driver.ready(run, seconds=0)
                self.assertIs(type(error.exception), scenarios.Failure)

                ready = root / ATTACHED / 'execution-ready.json'
                ready.parent.mkdir()
                ready.touch()
                run['proc'] = SimpleNamespace(poll=lambda: 4)
                driver.ready(run)
                self.assertTrue(run['executing'])

    def test_unknown_final_inventory_fails_campaign_even_with_passing_rows(self):
        with (tempfile.TemporaryDirectory() as directory,
              patch.object(scenarios, 'Driver') as driver_type,
              patch.object(scenarios.signal, 'signal'),
              redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO())):
            driver = driver_type.return_value
            driver.preflight.return_value = {'revision': 'a' * 40, 'buildsRevision': 'b' * 40}, {'version': '6.4.4'}
            driver.row.return_value = dict(passing_rows()['success'], verdict='passed', failedChecks=[])
            driver.residue.side_effect = scenarios.InventoryFailure('Runner inventory failed (HXR024)')
            root = Path(directory)
            evidence = root / 'evidence'

            result = scenarios.main(['--checkout', str(root / 'parties'), '--evidence', str(evidence),
                                     '--scenario', 'success', '--skip-prepare'])
            summary = json.loads((evidence / 'summary.json').read_text())
            readme = (evidence / 'README.md').read_text()

        self.assertEqual(result, 1)
        driver.release.assert_called_once()
        self.assertEqual(summary['passed'], 1)
        self.assertEqual(summary['residue'], {'listedIssuedRuns': None, 'issuedContainers': None,
                                             'error': 'Runner inventory failed (HXR024)'})
        self.assertIn('Final cleanup inventory unavailable. Runner inventory failed (HXR024).', readme)


class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.driver = scenarios.Driver(Path('.'), Path('.'))

    def test_docker_failure_and_incomplete_runner_listing_are_rejected(self):
        with patch.object(scenarios.subprocess, 'run', side_effect=subprocess.CalledProcessError(1, ['docker', 'ps'])) as command:
            with self.assertRaisesRegex(scenarios.InventoryFailure, 'Docker inventory failed'):
                self.driver.containers(OWNER)
            self.assertTrue(command.call_args.kwargs['check'])

        for code, output, detail in ((3, {'environments': [], 'outcome': {'ruleId': 'HXR024'}}, 'HXR024'),
                                     (0, {}, 'JSON is invalid')):
            with self.subTest(code=code), patch.object(self.driver, 'tool', return_value=(code, output)):
                with self.assertRaisesRegex(scenarios.InventoryFailure, detail):
                    self.driver.listing()
        with patch.object(self.driver, 'tool', return_value=(0, {'environments': []})):
            self.assertEqual(self.driver.listing(), [])

    def test_missing_snapshot_fails_the_row_and_preserves_cleanup(self):
        snapshot = {'containers': [], 'list': []}
        failure = scenarios.InventoryFailure('Docker inventory failed')
        for snapshots, calls in (([failure, snapshot], 0), ([snapshot, failure], 1)):
            with (self.subTest(snapshot=snapshots),
                  patch.object(self.driver, 'foreign', side_effect=snapshots),
                  patch.object(self.driver, 'success', return_value={}) as run,
                  patch.object(self.driver, 'release') as release):
                row = self.driver.row('success', 'a' * 40, '6.4.4')

                self.assertEqual(row['verdict'], 'failed')
                self.assertEqual(row['failedChecks'], ['driver:Docker inventory failed'])
                self.assertIsNone(row['foreign']['unchanged'])
                self.assertEqual(run.call_count, calls)
                release.assert_called_once()


class OwnershipTests(unittest.TestCase):
    def test_discovery_requires_the_launched_process_to_own_the_run(self):
        with tempfile.TemporaryDirectory() as directory:
            state_dir = Path(directory)
            diagnostics = state_dir / 'diagnostics'
            driver = scenarios.Driver(state_dir, state_dir)
            driver.manifest_hash = 'a' * 64
            for run_id in (OWNER, PEER):
                state = {'runId': run_id, 'manifestHash': driver.manifest_hash, 'executionContext': {'kind': 'local'}}
                (state_dir / f'{run_id}.json').write_text(json.dumps(state))
            owner_dir = diagnostics / OWNER
            owner_dir.mkdir(parents=True)
            proc = SimpleNamespace(pid=os.getpid(), poll=lambda: None)

            with patch.object(scenarios, 'STATE', state_dir), patch.object(scenarios, 'DIAGNOSTICS', diagnostics):
                self.assertIsNone(driver.discover(proc, set()))
                with (owner_dir / 'provisioning.lock').open('w'):
                    self.assertEqual(driver.discover(proc, set())['runId'], OWNER)
                    self.assertIsNone(driver.discover(proc, {OWNER}))

    def test_final_release_retries_an_unsuccessful_down(self):
        driver = scenarios.Driver(Path('.'), Path('.'))
        driver.bind({}, OWNER, ENVIRONMENT)
        with patch.object(driver, 'tool', side_effect=[(3, {}), (0, {})]) as tool:
            driver._release()
            self.assertNotIn(OWNER, driver.downed)
            driver._release()
            driver._release()

        self.assertIn(OWNER, driver.downed)
        self.assertEqual(tool.call_count, 2)

    def test_terminal_output_only_binds_new_run_ids(self):
        driver = scenarios.Driver(Path('.'), Path('.'))
        driver.bind({}, OWNER, ENVIRONMENT)
        peer = {'knownStates': {ATTACHED}}
        with patch.object(scenarios, 'read_json', return_value=None) as read_state:
            for run_id in (OWNER, ATTACHED, '../unexpected'):
                driver.bind_reported_owner(peer, {'runId': run_id})
            self.assertNotIn('runId', peer)
            read_state.assert_not_called()
            driver.bind_reported_owner(peer, {'runId': PEER})

        self.assertEqual(peer['runId'], PEER)
        self.assertEqual(driver.owners, [OWNER, PEER])


if __name__ == '__main__':
    unittest.main()
