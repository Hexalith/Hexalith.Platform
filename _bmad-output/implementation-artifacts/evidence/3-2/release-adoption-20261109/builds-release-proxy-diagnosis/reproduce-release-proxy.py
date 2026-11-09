from pathlib import Path
import datetime,hashlib,json,os,shutil,subprocess,tempfile,zipfile
owner=Path('/tmp/story32-release-adoption-builds');out=Path(tempfile.mkdtemp(prefix='story32-g4-release-proxy-'))
consumer=Path(Path('/tmp/story32-g4-failed-command-locator.txt').read_text().strip())
original_git=shutil.which('git');assert original_git
roots={}
for mode,revision in [('old','9b9e03f69b77e4a8e64e4185a1df1cb4bc315ba4'),('fixed','32d2a06')]:
 root=out/mode;root.mkdir();proxy=root/'git';proxy.write_bytes(subprocess.check_output(['git','show',revision+':Github/create-release/git-with-retry.sh'],cwd=owner));proxy.chmod(0o755);(root/'original-git-path').write_text(original_git+'\n');roots[mode]=root
package=Path('/tmp/story32-builds-9b9e-g4-version-repro/Hexalith.Builds.Module.Cli.4.46.1.nupkg')
with zipfile.ZipFile(package) as z:z.extractall(out/'package')
assemblies={'source':owner/'src/libraries/Hexalith.Builds.Module.Cli/bin/Debug/net10.0/Hexalith.Builds.Module.Cli.dll','packaged':out/'package/tools/net10.0/any/Hexalith.Builds.Module.Cli.dll'}
report={'recordedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'ownerSource':'9b9e03f69b77e4a8e64e4185a1df1cb4bc315ba4','fixedProxySource':subprocess.check_output(['git','rev-parse','32d2a06'],cwd=owner,text=True).strip(),'localUnsignedArchive':str(package),'archiveSha256':hashlib.sha256(package.read_bytes()).hexdigest(),'cases':[],'qualificationCredit':False}
for mode,proxy in roots.items():
 for kind,assembly in assemblies.items():
  env=os.environ.copy();env['PATH']=str(proxy)+os.pathsep+env['PATH'];env['HEXALITH_RELEASE_GIT']=original_git;env['DOTNET_CLI_HOME']=str(consumer/'.dotnet');env['NUGET_PACKAGES']=str(consumer/'.nuget/packages')
  arguments=['dotnet',str(assembly),'test','--manifest','test/fixtures/module/positive/hexalith.module-manifest.v1.json','--profile','full','--evidence',f'evidence/{mode}-{kind}.json','--output','json']
  result=subprocess.run(arguments,cwd=consumer,env=env,capture_output=True,text=True,timeout=300)
  (out/f'{mode}-{kind}.stdout').write_text(result.stdout);(out/f'{mode}-{kind}.stderr').write_text(result.stderr)
  case={'proxy':mode,'commandKind':kind,'exitCode':result.returncode,'stdout':result.stdout,'stderr':result.stderr};report['cases'].append(case);print(json.dumps(case),flush=True)
(out/'receipt.json').write_text(json.dumps(report,indent=2)+'\n');Path('/tmp/story32-g4-release-proxy-locator.txt').write_text(str(out)+'\n')
