"""Real Git/worktree write controls for the committed CiCd maintenance profile."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from copy import deepcopy

OWNER = Path(os.environ.get('MAINTENANCE_OWNER_ROOT', Path(__file__).resolve().parents[2]))
SOURCE = Path(os.environ.get('MAINTENANCE_BUILDS_SOURCE', OWNER / 'references/Hexalith.Builds'))
spec = importlib.util.spec_from_file_location('maintenance', OWNER / 'tools/maintenance.py')
tool = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tool)
OWNER_REVISION = subprocess.check_output(['git', '-C', str(OWNER), 'rev-parse', 'HEAD'], text=True).strip()
POLICY = json.loads(subprocess.check_output(['git', '-C', str(OWNER), 'show', OWNER_REVISION + ':' + tool.POLICY]))
REPOSITORY = 'Hexalith/Hexalith.Builds'
BASE = POLICY['lanes'][REPOSITORY]['baseRevision']


class MaintenanceWriteTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.target = self.root / 'target'
        self.output = self.root / 'output'
        self.run_git('clone', '--shared', '--no-checkout', str(SOURCE), str(self.target), cwd=self.root)
        self.run_git('remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Builds.git')
        self.run_git('-c', 'submodule.recurse=false', 'checkout', '-b', '4.44.x', BASE)
        self.workflow = self.target / '.github/workflows/ci.yml'
        self.before = self.workflow.read_bytes()

    def run_git(self, *arguments, cwd=None):
        result = subprocess.run(['git', *arguments], cwd=cwd or self.target, capture_output=True, check=False)
        self.assertEqual(result.returncode, 0, result.stderr.decode())
        return result.stdout

    def execute(self, apply=True, role='maintenance', revision=BASE, owner=OWNER):
        return tool.render(owner, OWNER_REVISION, self.target, revision, REPOSITORY, role, self.output, apply=apply)

    def refused_without_overwrite(self, **arguments):
        before = {str(path.relative_to(self.target)): path.read_bytes() for path in (self.target / '.github/workflows').glob('*.yml') if not path.is_symlink()}
        index = self.run_git('diff', '--cached', '--binary')
        with self.assertRaises(tool.MaintenanceError):
            self.execute(**arguments)
        after = {str(path.relative_to(self.target)): path.read_bytes() for path in (self.target / '.github/workflows').glob('*.yml') if not path.is_symlink()}
        self.assertEqual(after, before)
        self.assertEqual(self.run_git('diff', '--cached', '--binary'), index)

    def test_exact_named_branch_applies_only_committed_owned_templates(self):
        receipt = self.execute()
        self.assertEqual(receipt['branch'], '4.44.x')
        self.assertEqual(receipt['ownerSource'], OWNER_REVISION)
        self.assertEqual(receipt['intendedBranch'], '4.44.x')
        self.assertFalse(receipt['detachedProposal'])
        self.assertNotEqual(self.workflow.read_bytes(), self.before)
        self.assertTrue((self.output / 'maintenance-delivery.json').is_file())

    def test_detached_render_is_labelled_and_cannot_apply(self):
        self.run_git('-c', 'submodule.recurse=false', 'checkout', '--detach', BASE)
        receipt = self.execute(apply=False)
        self.assertIsNone(receipt['branch'])
        self.assertTrue(receipt['detachedProposal'])
        self.refused_without_overwrite()

    def test_another_named_branch_cannot_claim_the_maintenance_lane(self):
        self.run_git('checkout', '-b', 'other')
        self.refused_without_overwrite()

    def test_staged_owned_content_is_preserved(self):
        self.workflow.write_bytes(self.before + b'\n# incoming staged edit\n')
        self.run_git('add', '.github/workflows/ci.yml')
        self.refused_without_overwrite()

    def test_unstaged_owned_content_is_preserved(self):
        self.workflow.write_bytes(self.before + b'\n# incoming worktree edit\n')
        self.refused_without_overwrite()

    def test_tracked_worktree_deletion_is_preserved(self):
        self.workflow.unlink()
        self.refused_without_overwrite()
        self.assertFalse(self.workflow.exists())

    def test_staged_deletion_is_preserved(self):
        self.run_git('rm', '.github/workflows/ci.yml')
        self.refused_without_overwrite()
        self.assertFalse(self.workflow.exists())

    def test_staged_mode_change_is_preserved(self):
        self.run_git('update-index', '--chmod=+x', '.github/workflows/ci.yml')
        self.refused_without_overwrite()

    def test_executable_worktree_mode_refuses_even_when_git_ignores_filemode(self):
        self.run_git('config', 'core.filemode', 'false')
        self.workflow.chmod(0o755)
        self.refused_without_overwrite()
        self.assertTrue(self.workflow.stat().st_mode & 0o111)

    def test_empty_untracked_registration_workflow_is_preserved(self):
        revision = POLICY['ownedOutputs'][REPOSITORY]['registration']['baseRevision']
        self.run_git('-c', 'submodule.recurse=false', 'checkout', '--detach', revision)
        self.run_git('update-ref', 'refs/remotes/origin/main', revision)
        self.run_git('checkout', '-b', 'fix/registration-fixture')
        created = self.target / '.github/workflows/build-release.yml'
        self.assertFalse(created.exists())
        created.touch()
        self.refused_without_overwrite(role='registration', revision=revision)
        self.assertEqual(created.read_bytes(), b'')

    def test_foreign_target_origin_refuses_before_any_write(self):
        self.run_git('remote', 'set-url', 'origin', 'https://github.com/Other/Repo.git')
        self.refused_without_overwrite()

    def test_foreign_owner_origin_refuses_before_any_write(self):
        owner = self.root / 'foreign-owner'
        self.run_git('clone', '--shared', str(OWNER), str(owner), cwd=self.root)
        self.run_git('-C', str(owner), 'remote', 'set-url', 'origin', 'https://github.com/Other/Repo.git', cwd=self.root)
        self.refused_without_overwrite(owner=owner)

    def test_late_output_symlink_does_not_allow_an_earlier_output_write(self):
        directory = self.output / '.github/workflows'
        directory.mkdir(parents=True)
        (directory / 'build-release.yml').symlink_to(self.workflow)
        self.refused_without_overwrite(apply=False)
        self.assertFalse((directory / 'ci.yml').exists())

    def test_receipt_symlink_refuses_before_any_target_write(self):
        self.output.mkdir()
        marker = self.root / 'foreign-marker'; marker.write_bytes(b'unchanged')
        (self.output / 'maintenance-delivery.json').symlink_to(marker)
        self.refused_without_overwrite()
        self.assertEqual(marker.read_bytes(), b'unchanged')

    def test_unrelated_dirty_content_is_preserved(self):
        unrelated = self.target / 'unrelated-task.txt'; unrelated.write_bytes(b'user changes')
        self.execute()
        self.assertEqual(unrelated.read_bytes(), b'user changes')

    def test_environment_addition_is_one_exact_branch_with_reviewers_unchanged(self):
        environment = {'protection_rules': [{'type': 'required_reviewers', 'reviewers': [{'type': 'User', 'id': 1}]}],
                       'deployment_branch_policy': {'protected_branches': False, 'custom_branch_policies': True},
                       'can_admins_bypass': False}
        existing = {'id': 1, 'name': 'main', 'type': 'branch'}
        created = {'id': 2, 'name': '4.44.x', 'type': 'branch'}
        calls = []
        def api(method, endpoint, payload=None):
            calls.append((method, endpoint, payload))
            if method == 'POST':
                self.assertEqual(payload, {'name': '4.44.x', 'type': 'branch'})
                return deepcopy(created)
            if endpoint.endswith('/production'):
                return deepcopy(environment)
            policies = [existing, created] if any(call[0] == 'POST' for call in calls) else [existing]
            return {'total_count': len(policies), 'branch_policies': deepcopy(policies)}
        receipt = tool.production_branch(api, POLICY, apply=True)
        self.assertTrue(receipt['applied'])
        self.assertTrue(receipt['reviewersUnchanged'])
        self.assertEqual(len([call for call in calls if call[0] == 'POST']), 1)

    def test_missing_required_reviewers_cannot_be_repaired_by_environment_mutation(self):
        calls = []
        def api(method, endpoint, payload=None):
            calls.append(method)
            return {'protection_rules': [], 'deployment_branch_policy': {'protected_branches': False, 'custom_branch_policies': True}}
        with self.assertRaises(tool.MaintenanceError):
            tool.production_branch(api, POLICY, apply=True)
        self.assertEqual(calls, ['GET'])


if __name__ == '__main__':
    unittest.main(verbosity=2)
