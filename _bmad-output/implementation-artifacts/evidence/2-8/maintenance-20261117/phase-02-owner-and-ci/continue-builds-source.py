import json, os, pathlib, shutil, subprocess, time

ROOT = pathlib.Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-maintenance-20261117'
OUT = AREA / 'builds-source-03'
OUT.mkdir(exist_ok=False)
REPO = pathlib.Path(os.environ['TMPDIR']) / 'Hexalith.Builds'
NODE = pathlib.Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent)+':'+os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RESULT = {'previousSource':'e0cb4043fceda690457203f0895ea982a5ebbfd9','remoteMutations':False,'commands':[]}

def run(label, args, cwd=None, timeout=600, payload=None, required=True):
    start=time.monotonic()
    with (OUT/(label+'.log')).open('w') as log:
        p=subprocess.run(args,cwd=cwd or REPO,env=ENV,input=payload,text=True,stdout=log,stderr=subprocess.STDOUT,timeout=timeout)
    row={'label':label,'exitCode':p.returncode,'seconds':round(time.monotonic()-start,3)}
    RESULT['commands'].append(row); print(json.dumps(row),flush=True)
    if required and p.returncode: raise RuntimeError(label+' failed')
    return p.returncode

def replace(path, old, new):
    p=REPO/path; data=p.read_bytes(); before=old.encode(); after=new.encode()
    if b'\r\n' in data: before=before.replace(b'\n',b'\r\n');after=after.replace(b'\n',b'\r\n')
    if data.count(before)!=1:raise RuntimeError('Unexpected replacement count: '+path)
    p.write_bytes(data.replace(before,after))

try:
    run('clone',['git','clone','--shared','--no-checkout',str(ROOT/'references/Hexalith.Builds'),str(REPO)],ROOT)
    run('origin',['git','remote','set-url','origin','https://github.com/Hexalith/Hexalith.Builds.git'])
    run('import',['git','fetch',str(AREA/'builds-source-02/Builds.bundle'),'refs/heads/4.44.x:refs/heads/4.44.x'])
    run('checkout',['git','checkout','4.44.x'])
    shutil.copyfile(AREA/'builds-audit-refresh-01/package-version-audit.json',REPO/'Tools/package-version-audit.json')
    replace('Github/publish-containers/tests/test_publish_script_contract.py',
        '["main", {"name": "prerelease", "prerelease": True}],',
        '[{"name": "4.44.x", "range": "4.44.x"}, "main", {"name": "prerelease", "prerelease": True}],')
    replace('Tools/test-g4-release-contract.ps1',
        "if ($branches.Count -ne 2 -or $branches[0] -cne 'main' -or\n    [string]$branches[1].name -cne 'prerelease' -or $branches[1].prerelease -ne $true) {\n    throw 'Semantic-release must retain main stable and protected prerelease channels.'",
        "if ($branches.Count -ne 3 -or [string]$branches[0].name -cne '4.44.x' -or\n    [string]$branches[0].range -cne '4.44.x' -or $branches[1] -cne 'main' -or\n    [string]$branches[2].name -cne 'prerelease' -or $branches[2].prerelease -ne $true) {\n    throw 'Semantic-release must retain the bounded maintenance, main stable and protected prerelease channels.'")
    run('release-branch-contract',['python3','-B','-m','unittest','test_publish_script_contract.PublishScriptContractTests.test_builds_release_is_manual_protected_and_does_not_push_git','-v'],REPO/'Github/publish-containers/tests')
    run('release-wrapper-contracts',['pwsh','-NoProfile','-File','Tools/test-g4-release-contract.ps1'])
    run('normal-audit-validator',['pwsh','-NoProfile','-File','Tools/validate-package-version-audit.ps1'])
    # Preserve the exact failing build diagnostic without altering its assertions.
    target='test/Hexalith.Builds.Module.Tests/Workspace/WorkspaceMsBuildTests.cs'
    original=(REPO/target).read_bytes()
    replace(target,'            build.ExitCode.ShouldNotBe(0);\n            build.Output.ShouldContain("HXW006");\n            build.Output.ShouldContain("_HXWPreLateConfiguration");',
        '            await File.WriteAllTextAsync(Path.Combine("'+str(OUT)+'", "msbuild-" + mode + "-" + siblingPresent + ".log"), build.Output, TestContext.Current.CancellationToken).ConfigureAwait(true);\n            build.ExitCode.ShouldNotBe(0);\n            build.Output.ShouldContain("HXW006");\n            build.Output.ShouldContain("_HXWPreLateConfiguration");')
    run('build-focused-project',['dotnet','build','test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj','-c','Release','-m:1','-p:GeneratePackageOnBuild=false'],timeout=900)
    dll=REPO/'test/Hexalith.Builds.Module.Tests/bin/Release/net10.0/Hexalith.Builds.Module.Tests.dll'
    run('reproduce-msbuild-method',['dotnet',str(dll),'-method','*ToolModeOverridesConsumerProbesAsync'],timeout=300,required=False)
    (REPO/target).write_bytes(original)
    run('save-delta',['git','diff','--binary'])
    RESULT['status']='corrections-validated-and-msbuild-reproduced'
except BaseException as e:
    RESULT.update(status='failed',reason=type(e).__name__+': '+str(e));raise
finally:
    (OUT/'result.json').write_text(json.dumps(RESULT,indent=2)+'\n')
