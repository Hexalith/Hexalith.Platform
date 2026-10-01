"""Private attempt destinations cannot be redirected into forbidden custody."""
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from evidence import Attempt


class DestinationTests(unittest.TestCase):
    def test_category_symlink_cannot_write_into_git_or_recovery(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            project = base / 'repo'
            project.mkdir(mode=0o700)
            fake_home = base / 'home'
            fake_home.mkdir(mode=0o700)
            recovery = fake_home / 'hexalith-recovery-evidence'
            recovery.mkdir(mode=0o700)
            for category, target in [('qualification', project), ('rehearsal', recovery)]:
                with self.subTest(category=category):
                    root = base / category
                    root.mkdir(mode=0o700)
                    (root / category).symlink_to(target)
                    with patch('evidence.Path.home', return_value=fake_home):
                        with self.assertRaisesRegex(ValueError, 'symlink'):
                            Attempt(project, root, 'attempt', category)
                    self.assertFalse((target / 'attempt').exists())

    def test_attempt_symlink_is_refused_before_touching_existing_custody(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            project = base / 'repo'
            project.mkdir(mode=0o700)
            root = base / 'private'
            category = root / 'qualification'
            category.mkdir(parents=True, mode=0o700)
            root.chmod(0o700)
            retained = base / 'retained'
            retained.mkdir(mode=0o700)
            marker = retained / 'marker'
            marker.write_text('original')
            (category / 'attempt').symlink_to(retained)
            with self.assertRaisesRegex(ValueError, 'symlink'):
                Attempt(project, root, 'attempt')
            self.assertEqual(marker.read_text(), 'original')
            self.assertEqual(list(retained.iterdir()), [marker])

    def test_dangling_category_symlink_is_refused(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            project = base / 'repo'
            project.mkdir(mode=0o700)
            root = base / 'private'
            root.mkdir(mode=0o700)
            destination = base / 'missing'
            (root / 'qualification').symlink_to(destination)
            with self.assertRaisesRegex(ValueError, 'symlink'):
                Attempt(project, root, 'attempt')
            self.assertFalse(destination.exists())


if __name__ == '__main__':
    unittest.main()
