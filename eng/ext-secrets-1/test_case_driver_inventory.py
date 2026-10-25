import unittest
from cases import plan
from case_driver_inventory import load_inventory, blockers

class CaseDriverInventoryTests(unittest.TestCase):
    def test_every_case_has_exact_source_or_protocol_disposition_and_no_source_completion_claim(self):
        inventory = load_inventory()
        self.assertEqual([case.id for case in plan()],[row['caseId'] for row in inventory['cases']])
        self.assertEqual(234,len(inventory['cases']))
        for row in inventory['cases']:
            self.assertFalse(row['completeQualificationDriver'])
            self.assertIsNone(row['qualificationDriver'])
            self.assertTrue([*row['requiredSourceImplementations'],*row['requiredExternalInputs']])
            self.assertTrue(blockers([row['caseId']]))

    def test_installed_signature_cannot_remove_incomplete_source_blockers(self):
        self.assertIn('CaseDriver.S3.principal-direct-delivery.SelectedAuthenticatedPrincipalDeliveryTransportWithAtomicExpiryAndActorRevocation',blockers())
        self.assertTrue(any(name.startswith('CaseDriver.S4.migration.') for name in blockers()))

    def test_compiled_full_plan_is_exact_source_bytes(self):
        from joint_live_phase_runner import canonical,full_plan
        from pathlib import Path
        self.assertEqual(canonical(full_plan()),Path(__file__).with_name('public-joint-plan.json').read_bytes())

if __name__ == '__main__': unittest.main()
