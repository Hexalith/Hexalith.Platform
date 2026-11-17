"""Keep the actual executor authenticated before controller imports; no hosted execution."""
from pathlib import Path
helper = Path(__file__).with_name('check-coherent-caller.py').read_text()
prefix = helper.split('paths = [')[0].replace("coherent-caller-2'", "ordinary-proof-4'")
prefix = prefix.replace("BASE = '4eca9e8fbad451a602a9ec7d2d6384345b6b0e4e'", "BASE = '11053e562a2cba0196354cec655c01677e2afd46'")
exec(compile(prefix, str(__file__), 'exec'))
executor = WORK / '.hexalith/builds-execution'
executor.parent.mkdir(exist_ok=True)
run('clone-executor', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(executor)])
run('checkout-executor', ['git', '-C', str(executor), '-c', 'submodule.recurse=false', 'checkout', '--detach', '6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b'])
assert subprocess.check_output(['git', '-C', str(executor), 'rev-parse', 'HEAD'], text=True).strip() == '6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b'
assert not subprocess.check_output(['git', '-C', str(executor), 'status', '--porcelain', '--untracked-files=normal'], text=True)
previous = ROOT / 'artifacts/2-8-continuation-20261117/ordinary-proof-2'
files = json.loads((previous / 'projected-files.json').read_text())
for path, digest in files.items():
    content = (previous / 'changed-files' / path).read_bytes()
    assert hashlib.sha256(content).hexdigest() == digest
    destination = WORK / path
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(content)
workflow_path = WORK / '.github/workflows/ci-lifecycle-qualification.yml'
workflow = workflow_path.read_text()
controller = '      - name: Test the lifecycle controller before any service\n        run: python3 -B -m unittest discover -s tests/ci-lifecycle -v\n'
assert workflow.count(controller) == 1
workflow = workflow.replace(controller, '')
setup = '      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0\n'
assert workflow.count(setup) == 1
workflow_path.write_text(workflow.replace(setup, controller + setup))
test_path = WORK / 'tests/ci-lifecycle/test_review_fixes.py'
test = test_path.read_text()
old = "        self.assertEqual(steps[2][0], 'Test the lifecycle controller before any service')\n"
new = """        self.assertEqual(steps[2][0], 'Check out the exact released proof executor')
        self.assertEqual(steps[3][0], 'Verify the exact clean proof executor')
        self.assertEqual(steps[4][0], 'Test the lifecycle controller before any service')
        executor = self.step('Check out the exact released proof executor')
        self.assertIn('ref: 6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b', executor)
        self.assertIn('path: .hexalith/builds-execution', executor)
        self.assertIn('persist-credentials: false', executor)
        self.assertIn('submodules: false', executor)
        verification = self.step('Verify the exact clean proof executor')
        self.assertIn('rev-parse HEAD)\" = 6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b', verification)
        self.assertIn('status --porcelain --untracked-files=normal)', verification)
"""
assert test.count(old) == 1
test_path.write_text(test.replace(old, new))
for path in files:
    content = (WORK / path).read_bytes()
    target = OUT / 'changed-files' / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(content)
    files[path] = hashlib.sha256(content).hexdigest()
(OUT / 'projected-files.json').write_text(json.dumps(files, indent=2) + '\n')
(OUT / 'native-inputs-unchanged.json').write_bytes((previous / 'native-inputs-unchanged.json').read_bytes())
run('stage-projection', ['git', 'add', '--', *files])
(OUT / 'projection.diff').write_bytes(subprocess.check_output(['git', 'diff', '--cached', '--binary'], cwd=WORK))
(OUT / 'incremental-ordering.diff').write_text(''.join(__import__('difflib').unified_diff((previous / 'changed-files/.github/workflows/ci-lifecycle-qualification.yml').read_text().splitlines(True), workflow_path.read_text().splitlines(True), fromfile='previous/workflow', tofile='corrected/workflow')) + ''.join(__import__('difflib').unified_diff((previous / 'changed-files/tests/ci-lifecycle/test_review_fixes.py').read_text().splitlines(True), test_path.read_text().splitlines(True), fromfile='previous/test', tofile='corrected/test')))
run('actionlint', ['actionlint', '.github/workflows/ci-lifecycle-qualification.yml'])
run('lifecycle-controller-controls', ['python3', '-B', '-m', 'unittest', 'discover', '-s', 'tests/ci-lifecycle', '-v'])
(OUT / 'unchanged-controls.json').write_text(json.dumps({'owningCSharpClass': '../ordinary-proof-2/primary-workflow-class-audit.json', 'passed': 70, 'helperCasesPassed': 75, 'observerCasesPassed': 3, 'noneRepeated': True, 'knownProducerBlocker': '../ordinary-proof/build-parties.log'}, indent=2) + '\n')
