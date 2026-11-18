"""Resource-free checks of the scenario driver's summary extraction and row verdicts."""
import copy
import json
import sys
import unittest
from pathlib import Path

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


if __name__ == '__main__':
    unittest.main()
