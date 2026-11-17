import json, os, pathlib, subprocess

ROOT=pathlib.Path('/home/administrator/projects/hexalith/platform')
AREA=ROOT/'artifacts/2-8-maintenance-20261117'
OUT=AREA/'registration-fixtures-02'; OUT.mkdir(exist_ok=False)
REPO=pathlib.Path(os.environ['TMPDIR'])/'Hexalith.Builds'
ENV=dict(os.environ,PYTHONDONTWRITEBYTECODE='1')
RESULT={'previousSource':'20e3383b082ffec45dd1da5d20e237dd021e3f8e','remoteMutations':False,'commands':[]}
def run(label,args,cwd=None,payload=None):
    p=subprocess.run(args,cwd=cwd or REPO,env=ENV,input=payload,capture_output=True,text=True,timeout=600)
    (OUT/(label+'.log')).write_text(p.stdout+p.stderr)
    RESULT['commands'].append({'label':label,'exitCode':p.returncode});print(label,p.returncode,flush=True)
    if p.returncode:raise RuntimeError(label)
    return p.stdout.strip()
def replace(old,new):
    global data
    if data.count(old)!=1:raise RuntimeError('Unexpected fixture replacement count')
    data=data.replace(old,new)
try:
    run('clone',['git','clone','--shared','--no-checkout',str(ROOT/'references/Hexalith.Builds'),str(REPO)],ROOT)
    run('origin',['git','remote','set-url','origin','https://github.com/Hexalith/Hexalith.Builds.git'])
    run('import',['git','fetch',str(AREA/'registration-candidates-01/Builds.bundle'),'refs/heads/fix/story-2-8-maintenance-registration:refs/heads/fix/story-2-8-maintenance-registration'])
    run('checkout',['git','checkout','fix/story-2-8-maintenance-registration'])
    path=REPO/'Github/lib/tests/test_platform_support.py';data=path.read_text()
    replace('values = {"inventory": inventory, "package-policies": policies, "version": "2.3.5", "sha": SHA}',
        'values = {"inventory": inventory, "package-policies": policies, "version": "2.3.5", "sha": SHA,\n'
        '            "package-payloads": {"Fixture.Package": release_publish.publication.payload_map(original)},\n'
        '            "repository": "Example/Fixture", "artifact-run": "123", "artifact-id": "456",\n'
        '            "artifact-digest": digest(payload)}')
    replace('        self.assertEqual(1, len(commands))\n        args, kwargs = commands[0]',
        '        self.assertEqual(values["package-payloads"]["Fixture.Package"], values["package-publication"]["packages"][0]["files"])\n'
        '        self.assertEqual(sha(signed), values["package-publication"]["packages"][0]["archiveSha256"])\n'
        '        self.assertEqual(1, len(commands))\n        args, kwargs = commands[0]')
    replace('            release_publish.verify_packages({"inventory": inventory, "version": "2.3.5", "sha": SHA},\n'
        '                open_url=lambda *_a, **_k: response, attempts=1, interval=0)',
        '            values = {"inventory": inventory, "version": "2.3.5", "sha": SHA,\n'
        '                "package-payloads": {"Fixture.Package": release_publish.publication.payload_map(package(SHA))},\n'
        '                "repository": "Example/Fixture", "artifact-run": "123", "artifact-id": "456",\n'
        '                "artifact-digest": digest(payload)}\n'
        '            release_publish.verify_packages(values, open_url=lambda *_a, **_k: response, attempts=1, interval=0)\n'
        '        self.assertEqual(values["package-payloads"]["Fixture.Package"], values["package-publication"]["packages"][0]["files"])')
    path.write_text(data)
    run('publication-fixtures',['python3','-B','-m','unittest','test_platform_support.StrictPackageTests.test_authenticated_manifest_round_trip_and_strict_public_payload','test_platform_support.StrictPackageTests.test_legacy_manifest_and_legacy_verification_have_no_new_policy','-v'],REPO/'Github/lib/tests')
    # The owning guard package supplies the exact CLI/config versions without installing the unrelated root release toolchain.
    guard=REPO/'Github/guard'
    run('guard-npm-ci',['npm','ci','--ignore-scripts','--no-fund'],guard)
    run('guard-signatures',['npm','audit','signatures'],guard)
    (REPO/'node_modules').symlink_to(guard/'node_modules',target_is_directory=True)
    message='test(ci): retain producer payloads in publication fixtures\n'
    (OUT/'commit-message.txt').write_text(message)
    run('commitlint',['node',str(guard/'node_modules/@commitlint/cli/cli.js'),'--config',str(REPO/'commitlint.config.mjs'),'--color=false'],payload=message)
    run('stage',['git','add','--','Github/lib/tests/test_platform_support.py'])
    run('diff-check',['git','diff','--cached','--check'])
    run('source-delta',['git','diff','--cached','--binary'])
    run('commit',['git','commit','-F',str(OUT/'commit-message.txt')])
    RESULT['source']=run('source',['git','rev-parse','HEAD'])
    RESULT['tree']=run('tree',['git','rev-parse','HEAD^{tree}'])
    run('bundle',['git','bundle','create',str(OUT/'Builds.bundle'),'refs/heads/fix/story-2-8-maintenance-registration','^e8b86ba3ae6e27c2d6cba536d6002ac8f3c5a921'])
    RESULT['status']='fixture-correction-validated-and-committed'
except BaseException as e:RESULT.update(status='failed',reason=str(e));raise
finally:(OUT/'result.json').write_text(json.dumps(RESULT,indent=2)+'\n')
