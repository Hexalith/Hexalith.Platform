"""Remove the obsolete main-only text assumption; exercise real source selection."""
import json, os, pathlib, subprocess
ROOT=pathlib.Path('/home/administrator/projects/hexalith/platform')
AREA=ROOT/'artifacts/2-8-maintenance-20261117'
OUT=AREA/'platform-support-03';OUT.mkdir(exist_ok=False)
REPO=pathlib.Path(os.environ['TMPDIR'])/'Hexalith.Platform'
NODE=pathlib.Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV=dict(os.environ,PATH=str(NODE.parent)+':'+os.environ['PATH'],PYTHONDONTWRITEBYTECODE='1')
R={'previousSource':'ad32547462a746945631c35cb994bcdb8f53d0ed','remoteMutations':False,'commands':[]}
def run(label,args,cwd=None,payload=None,required=True):
 p=subprocess.run(args,cwd=cwd or REPO,env=ENV,input=payload,capture_output=True,text=True,timeout=300)
 (OUT/(label+'.log')).write_text(p.stdout+p.stderr);R['commands'].append({'label':label,'exitCode':p.returncode});print(label,p.returncode,flush=True)
 if required and p.returncode:raise RuntimeError(label)
 return p.returncode
try:
 run('clone',['git','clone','--shared','--no-checkout',str(ROOT),str(REPO)],ROOT)
 run('origin',['git','remote','set-url','origin','https://github.com/Hexalith/Hexalith.Platform.git'])
 run('import',['git','fetch',str(AREA/'platform-support-02/Platform.bundle'),'refs/heads/1.7.x:refs/heads/1.7.x'])
 run('checkout',['git','checkout','1.7.x'])
 code=run('original-legacy-controls',['python3','-B','eng/test_legacy_tuple_source.py'],required=False)
 assert code!=0,'Expected the known obsolete main-only workflow text assertion to fail'
 path=REPO/'eng/test_legacy_tuple_source.py';text=path.read_text()
 old='        self.assertIn("  push:\\n    branches: [main]", workflow)'
 assert text.count(old)==1
 text=text.replace(old,'        self.assertEqual(legacy.resolve(self.root, self.current), self.old)\n        self.assertEqual(legacy.resolve(self.root, self.current, self.old), self.old)')
 path.write_text(text)
 run('corrected-legacy-controls',['python3','-B','eng/test_legacy_tuple_source.py'])
 run('npm-ci',['npm','ci','--ignore-scripts','--no-fund'])
 run('signatures',['npm','audit','signatures'])
 message='test(ci): exercise committed legacy source selection\n';(OUT/'commit-message.txt').write_text(message)
 run('commitlint',[str(NODE),str(REPO/'node_modules/@commitlint/cli/cli.js'),'--config',str(REPO/'commitlint.config.mjs'),'--color=false'],payload=message)
 run('stage',['git','add','--','eng/test_legacy_tuple_source.py'])
 run('diff-check',['git','diff','--cached','--check'])
 run('source-delta',['git','diff','--cached','--binary'])
 run('commit',['git','commit','-F',str(OUT/'commit-message.txt')])
 R['source']=subprocess.check_output(['git','rev-parse','HEAD'],cwd=REPO,text=True).strip()
 R['tree']=subprocess.check_output(['git','rev-parse','HEAD^{tree}'],cwd=REPO,text=True).strip()
 run('version',[str(NODE),str(AREA/'analyze-maintenance-version.mjs'),str(REPO),'0f4e27f1f78310bd731c10c19dc51bb1e0345d44','1.7.x',str(OUT/'version.json'),'conventionalcommits'])
 run('bundle',['git','bundle','create',str(OUT/'Platform.bundle'),'refs/heads/1.7.x','^0f4e27f1f78310bd731c10c19dc51bb1e0345d44'])
 R['status']='legacy-selection-control-corrected-and-verified; tuple data still original pending genuine Builds publication'
except BaseException as e:R.update(status='failed',reason=str(e));raise
finally:(OUT/'result.json').write_text(json.dumps(R,indent=2)+'\n')
