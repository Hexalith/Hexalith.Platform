"""Historical topology restoration must keep exact Git bytes and declared dependency boundaries."""
import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest

import retained_works_host as retained


class RetainedWorksHostTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.bundle = self.root / 'bundle'
        self.bundle.mkdir()
        self.works = self.root / 'works'
        self.works.mkdir()
        self.baseline = 'a' * 40
        self.sources = {name: b'historical source\n' for name in retained.REQUIRED}
        self.sources['.gitmodules'] = b'[submodule "references/Hexalith.AI.Tools"]\n path = references/Hexalith.AI.Tools\n'
        (self.works / '.gitmodules').write_bytes(self.sources['.gitmodules'])
        (self.works / 'references/Hexalith.AI.Tools').mkdir(parents=True)
        self.package()

    def package(self, extras=(), sources=None):
        sources = self.sources if sources is None else sources
        archive = self.bundle / 'works-source.tar.gz'
        with tarfile.open(archive, 'w:gz') as output:
            for name, content in sources.items():
                member = tarfile.TarInfo(name)
                member.size = len(content)
                output.addfile(member, io.BytesIO(content))
            for member, content in extras:
                output.addfile(member, io.BytesIO(content))
        (self.bundle / 'manifest.json').write_text(json.dumps({'baselineCommit': self.baseline,
            'archiveSha256': retained.digest(archive),
            'sourceFiles': {name: hashlib.sha256(content).hexdigest() for name, content in sources.items()}}))

    def historical(self, works, commit, name):
        self.assertEqual(commit, self.baseline)
        self.assertEqual(works, self.works)
        return self.sources[name]

    def prepare(self, name='restored', historical=None):
        return retained.prepare(self.bundle, self.works, self.root / name, historical or self.historical)

    def test_exact_git_sources_and_only_root_declared_dependencies_are_restored(self):
        evidence = self.prepare()
        self.assertEqual(evidence['baselineCommit'], self.baseline)
        self.assertEqual(set(evidence['sourceFiles']), set(self.sources))
        self.assertEqual(evidence['dependencyLinks'], {'references/Hexalith.AI.Tools': str(self.works / 'references/Hexalith.AI.Tools')})
        generated = Path(evidence['root']) / 'src/Hexalith.Works.AppHost/obj/generated.cs'
        generated.parent.mkdir()
        generated.write_text('generated build output')
        self.assertTrue(retained.verify(evidence))

    def test_archive_and_historical_git_blob_substitution_refuse(self):
        archive = self.bundle / 'works-source.tar.gz'
        archive.write_bytes(archive.read_bytes() + b'changed')
        with self.assertRaises(ValueError): self.prepare()
        self.package()
        with self.assertRaises(ValueError): self.prepare(historical=lambda *_: b'foreign source')

    def test_unsafe_or_duplicate_members_refuse_even_with_an_updated_archive_digest(self):
        for index, member in enumerate([tarfile.TarInfo('../outside'), tarfile.TarInfo('unknown.cs'),
                                        tarfile.TarInfo('global.json'), tarfile.TarInfo('linked.cs')]):
            if index == 3:
                member.type = tarfile.SYMTYPE
                member.linkname = 'global.json'
            self.package(extras=[(member, b'')])
            with self.subTest(member=member.name), self.assertRaises(ValueError): self.prepare('unsafe-' + str(index))

    def test_missing_required_topology_source_refuses(self):
        self.package(sources={name: value for name, value in self.sources.items() if name != 'global.json'})
        with self.assertRaises(ValueError): self.prepare()

    def test_missing_historical_solution_marker_refuses_even_when_manifest_and_archive_match(self):
        self.package(sources={name: value for name, value in self.sources.items() if name != 'Hexalith.Works.slnx'})
        with self.assertRaisesRegex(ValueError, 'exact source binding'): self.prepare()

    def test_a_removed_root_dependency_declaration_cannot_be_borrowed(self):
        (self.works / '.gitmodules').write_text('')
        with self.assertRaises(ValueError): self.prepare()

    def test_later_source_changes_additions_and_symlinks_refuse(self):
        for index in range(3):
            evidence = self.prepare('changed-' + str(index))
            source = Path(evidence['root']) / 'src/Hexalith.Works.AppHost/Program.cs'
            if index == 0: source.write_bytes(b'changed')
            elif index == 1: source.with_name('NewGenericRuntime.cs').write_bytes(b'new source')
            else:
                original = self.root / 'same-bytes.cs'
                original.write_bytes(source.read_bytes())
                source.unlink()
                source.symlink_to(original)
            with self.subTest(index=index), self.assertRaises(ValueError): retained.verify(evidence)

    def test_later_dependency_substitution_refuses(self):
        evidence = self.prepare()
        dependency = Path(evidence['root']) / 'references/Hexalith.AI.Tools'
        dependency.unlink()
        dependency.symlink_to(self.root, target_is_directory=True)
        with self.assertRaises(ValueError): retained.verify(evidence)


if __name__ == '__main__':
    unittest.main()
