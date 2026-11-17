import json, os, pathlib, subprocess
ROOT=pathlib.Path('/home/administrator/projects/hexalith/platform')
AREA=ROOT/'artifacts/2-8-maintenance-20261117'
OUT=AREA/'carrier-source-01';OUT.mkdir(exist_ok=False)
REPO=pathlib.Path(os.environ['TMPDIR'])/'Hexalith.Builds'
NODE=pathlib.Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV=dict(os.environ,PATH=str(NODE.parent)+':'+os.environ['PATH'],PYTHONDONTWRITEBYTECODE='1')
BASE='51d56ec722e0e66c8957f2f7502015f2c9b7c5c1'
BRANCH='fix/story-2-8-maintenance-carrier'
R={'base':BASE,'remoteMutations':False,'commands':[]}
def run(label,args,cwd=None,payload=None):
 p=subprocess.run(args,cwd=cwd or REPO,env=ENV,input=payload,capture_output=True,text=True,timeout=300)
 (OUT/(label+'.log')).write_text(p.stdout+p.stderr);R['commands'].append({'label':label,'exitCode':p.returncode});print(label,p.returncode,flush=True)
 if p.returncode:raise RuntimeError(label)
 return p.stdout.strip()
try:
 run('clone',['git','clone','--shared','--no-checkout',str(ROOT/'references/Hexalith.Builds'),str(REPO)],ROOT)
 run('origin',['git','remote','set-url','origin','https://github.com/Hexalith/Hexalith.Builds.git'])
 run('fresh-maintenance',['git','fetch','origin','refs/heads/4.44.x:refs/remotes/origin/4.44.x'])
 assert run('base',['git','rev-parse','refs/remotes/origin/4.44.x'])==BASE
 run('branch',['git','checkout','-b',BRANCH,BASE])
 path=REPO/'Tools/archive-g4-qualification-evidence.py';text=path.read_text();old='heads/(?:main|prerelease)|pull/'
 assert text.count(old)==1;text=text.replace(old,r'heads/(?:main|prerelease|4\.44\.x)|pull/')
 marker='    if kind == "release":\n        if (identity["GITHUB_JOB"]'
 assert text.count(marker)==1
 text=text.replace(marker,'    if (kind == "release" and identity["GITHUB_WORKFLOW_REF"].endswith("@refs/heads/4.44.x")\n            and not re.fullmatch(r"4\\.44\\.(?:0|[1-9][0-9]*)", inventory["version"])):\n        raise ValueError("Maintenance release carrier requires an exact stable 4.44 version")\n'+marker)
 path.write_text(text)
 path=REPO/'Tools/test-archive-g4-qualification-evidence.py';text=path.read_text();marker='    def test_rejects_changed_hash_size_empty_duplicate_or_unsafe_inventory(self):'
 assert text.count(marker)==1
 additions='''    def test_exact_maintenance_ci_and_release_preserve_carrier_binding(self):
        for kind in ("ci", "release"):
            with self.subTest(kind=kind):
                self.inventory["version"] = "4.44.14" if kind == "release" else "0.0.0-ci.123"
                self.inventory["qualification"]["releaseEligible"] = kind == "release"
                workflow = "build-release.yml" if kind == "release" else "ci.yml"
                identity = {"GITHUB_JOB": "release" if kind == "release" else "contracts",
                            "GITHUB_EVENT_NAME": "workflow_dispatch" if kind == "release" else "push",
                            "GITHUB_WORKFLOW_REF": "Hexalith/Hexalith.Builds/.github/workflows/" + workflow + "@refs/heads/4.44.x"}
                original = self.write_inventory()
                with patch.dict(os.environ, identity):
                    path = carrier.archive_evidence(self.directory, kind)
                    self.assertEqual(path, carrier.verify_archive(self.directory, kind))
                    with zipfile.ZipFile(path) as archive:
                        self.assertEqual(original, archive.read(carrier.INVENTORY_NAME))
                        producer = json.loads(archive.read("producer-run.json"))
                        self.assertEqual(identity["GITHUB_WORKFLOW_REF"], producer["GITHUB_WORKFLOW_REF"])
                        self.assertEqual(SOURCE, producer["sourceRevision"])
                        self.assertEqual(kind, producer["kind"])
                        self.assertEqual("2", producer["GITHUB_RUN_ATTEMPT"])
                    path.unlink()

    def test_maintenance_carrier_refuses_other_branches_tags_and_versions(self):
        self.inventory["version"] = "4.44.14"
        self.write_inventory()
        for ref in ("heads/4.45.x", "heads/4.44", "heads/4x44xx", "heads/4.44.x/other", "tags/v4.44.14"):
            with self.subTest(ref=ref), patch.dict(os.environ, {"GITHUB_WORKFLOW_REF":
                    "Hexalith/Hexalith.Builds/.github/workflows/build-release.yml@refs/" + ref}):
                with self.assertRaises(ValueError):
                    carrier.archive_evidence(self.directory, "release")
                self.assertFalse((self.directory / carrier.ARCHIVE_NAME).exists())
        with patch.dict(os.environ, {"GITHUB_WORKFLOW_REF":
                "Hexalith/Hexalith.Builds/.github/workflows/build-release.yml@refs/heads/4.44.x"}):
            for version in ("4.45.0", "5.0.0", "4.44.14-preview.1", "4.44.014"):
                with self.subTest(version=version):
                    self.inventory["version"] = version
                    self.write_inventory()
                    with self.assertRaisesRegex(ValueError, "exact stable 4.44"):
                        carrier.archive_evidence(self.directory, "release")

    def test_maintenance_carrier_keeps_all_producer_identity_refusals(self):
        self.inventory["version"] = "4.44.14"
        self.write_inventory()
        valid = "Hexalith/Hexalith.Builds/.github/workflows/build-release.yml@refs/heads/4.44.x"
        with patch.dict(os.environ, {"GITHUB_WORKFLOW_REF": valid}):
            for key, value in (("GITHUB_WORKFLOW_SHA", "b" * 40), ("GITHUB_SHA", "b" * 40),
                               ("GITHUB_REPOSITORY", "Other/Builds"), ("GITHUB_RUN_ID", "0"),
                               ("GITHUB_RUN_ATTEMPT", "0"), ("GITHUB_JOB", "contracts"),
                               ("GITHUB_EVENT_NAME", "push"), ("GITHUB_WORKFLOW_REF", valid.replace("build-release.yml", "foreign.yml"))):
                with self.subTest(key=key), patch.dict(os.environ, {key: value}):
                    with self.assertRaises(ValueError):
                        carrier.archive_evidence(self.directory, "release")
                    self.assertFalse((self.directory / carrier.ARCHIVE_NAME).exists())

'''
 path.write_text(text.replace(marker,additions+marker))
 run('carrier-controls',['python3','-B','Tools/test-archive-g4-qualification-evidence.py'])
 run('source-guard-controls',['python3','-B','Tools/test-maintenance-release-source.py'])
 run('npm-ci',['npm','ci','--ignore-scripts','--no-fund'])
 run('signatures',['npm','audit','signatures'])
 message='fix(release): retain exact maintenance producer carriers\n';(OUT/'commit-message.txt').write_text(message)
 run('commitlint',[str(NODE),str(REPO/'node_modules/@commitlint/cli/cli.js'),'--config',str(REPO/'commitlint.config.mjs'),'--color=false'],payload=message)
 run('stage',['git','add','--','Tools/archive-g4-qualification-evidence.py','Tools/test-archive-g4-qualification-evidence.py'])
 run('diff-check',['git','diff','--cached','--check'])
 run('source-delta',['git','diff','--cached','--binary'])
 run('commit',['git','commit','-F',str(OUT/'commit-message.txt')])
 R['source']=run('source',['git','rev-parse','HEAD']);R['tree']=run('tree',['git','rev-parse','HEAD^{tree}'])
 run('version',[str(NODE),str(AREA/'analyze-maintenance-version.mjs'),str(REPO),'3c8096180ecf2aa539ac454815efbfceb8a92822','4.44.x',str(OUT/'version.json'),''])
 run('bundle',['git','bundle','create',str(OUT/'Builds.bundle'),'refs/heads/'+BRANCH,'^'+BASE])
 R['status']='carrier-correction-tested-and-committed'
except BaseException as e:R.update(status='failed',reason=str(e));raise
finally:(OUT/'result.json').write_text(json.dumps(R,indent=2)+'\n')
