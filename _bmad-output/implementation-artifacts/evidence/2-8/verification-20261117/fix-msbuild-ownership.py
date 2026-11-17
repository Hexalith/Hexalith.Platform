#!/usr/bin/env python3
"""Fix demonstrated bounded-child MSBuild ownership; execute causal and affected controls."""
from pathlib import Path
helper = Path(__file__).with_name('check-coherent-caller.py').read_text()
prefix = helper.split('paths = [')[0].replace("coherent-caller-2'", "bounded-msbuild-fix-final'")
prefix = prefix.replace("BASE = '4eca9e8fbad451a602a9ec7d2d6384345b6b0e4e'", "BASE = '83916016e0fb64b5fbde79f05c2cf317d0900f99'")
exec(compile(prefix, str(__file__), 'exec'))
import re
import xml.etree.ElementTree as ET

test_path = 'tests/Hexalith.Parties.Contracts.Tests/Package/PackageTestProcessTests.cs'
test = (WORK / test_path).read_text()
test = test.replace('using System.Text.Json;', 'using System.Text.Json;\nusing System.Xml.Linq;')
addition = '''
    /// <summary>Bounded direct and carried dotnet commands evaluate in a worker owned by their exact invocation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundedDotnetCommandsUseTheirOwnMsbuildWorker(bool throughPython)
    {
        string directory = Directory.CreateTempSubdirectory("parties-owned-msbuild-").FullName;
        try
        {
            string project = Path.Combine(directory, "ExclusiveWorker.proj");
            string marker = Path.Combine(directory, "worker.json");
            string probe = Path.Combine(directory, "worker.py");
            File.WriteAllText(Path.Combine(directory, "global.json"), """{"sdk":{"version":"11.0.100","rollForward":"disable"}}""");
            File.WriteAllText(probe, """
                import json,os,sys
                from pathlib import Path
                pid=os.getppid()
                while pid > 1:
                    root=Path('/proc')/str(pid)
                    arguments=[x.decode() for x in (root/'cmdline').read_bytes().split(bytes([0])) if x]
                    if arguments and Path(arguments[0]).name in ('dotnet','MSBuild','MSBuild.dll'):
                        Path(sys.argv[1]).write_text(json.dumps({'pid':pid,'commandLine':' '.join(arguments)}))
                        break
                    text=(root/'stat').read_text()
                    pid=int(text[text.rfind(')')+2:].split()[1])
                else:
                    raise RuntimeError('No actual Linux SDK worker in the controlled target ancestry')
                """);
            new XDocument(new XElement("Project", new XElement("Target", new XAttribute("Name", "ObserveWorker"),
                new XElement("Exec", new XAttribute("Command", $"python3 \\\"{probe}\\\" \\\"{marker}\\\""))))).Save(project);
            var command = new ProcessStartInfo(throughPython ? "python3" : "dotnet");
            if (throughPython)
            {
                command.ArgumentList.Add("-c");
                command.ArgumentList.Add("import os,sys; os.execvp('dotnet',['dotnet',*sys.argv[1:]])");
            }
            foreach (string argument in new[] { "msbuild", project, "-nologo", "-target:ObserveWorker", "-verbosity:quiet" })
            {
                command.ArgumentList.Add(argument);
            }
            // An ambient opt-in cannot move this bounded invocation onto a worker
            // owned and reaped by another independent probe. Keep IPC private too.
            command.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "true";
            command.Environment["MSBUILDUSESERVER"] = "1";
            command.Environment.Remove("MSBUILDDISABLENODEREUSE");
            command.Environment["TMPDIR"] = directory;
            command.Environment["TMP"] = directory;
            command.Environment["TEMP"] = directory;
            PackageTestProcessResult result = PackageTestProcess.Run(command, directory, 30_000);
            result.ExitCode.ShouldBe(0, result.Diagnostic);
            using JsonDocument observed = JsonDocument.Parse(File.ReadAllText(marker));
            observed.RootElement.GetProperty("pid").GetInt32().ShouldBeGreaterThan(1);
            // Read actual Linux worker argv from the target's ancestry; MSBuild's
            // Environment.CommandLine property can report the client's context.
            string worker = observed.RootElement.GetProperty("commandLine").GetString()!;
            worker.ShouldContain(project);
            worker.ShouldNotContain("nodemode:");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

'''
anchor = '    /// <summary>The real supervised child receives caller-scoped environment changes instead of ambient source imports.</summary>'
assert test.count(anchor) == 1
(WORK / test_path).write_text(test.replace(anchor, addition + anchor))

ENV['GITHUB_ENV'] = str(Path(os.environ['TMPDIR']) / 'github-env.txt')
run('prepare-real-522', ['python3', '-B', 'references/Hexalith.Builds/Github/prepare-module-build/prepare_module_build.py',
    '--manifest', 'module/hexalith.module-manifest.v2.json'])
for line in Path(ENV['GITHUB_ENV']).read_text().splitlines():
    key, value = line.split('=', 1); ENV[key] = value
ENV.pop('MSBUILDDISABLENODEREUSE', None)
contracts = 'tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj'
ci = 'tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj'
props = ['-p:UseHexalithProjectReferences=false', '-p:UseNuGetDeps=true']
def build(name, project):
    run(name, ['dotnet', 'build', project, '--configuration', 'Release', '--disable-build-servers', '-m:1', '-warnaserror', *props])
def test_run(name, project, kind, selector, count, required=True):
    return run(name, ['dotnet', 'test', '--project', project, '--configuration', 'Release', '--no-build', '--no-restore', *props,
        '--filter-' + kind, selector, '--minimum-expected-tests', str(count), '--fail-skips', 'on', '--report-xunit-trx',
        '--results-directory', str(OUT / name)], required=required)

# The exact two-row fixture already ran against this unpatched base. Authenticate
# its primary UnitTestResult nodes (the root ResultSummary is not another case).
red = next((ROOT/'artifacts/2-8-continuation-20261117/bounded-msbuild-fix-4/red-own-worker').glob('*.trx'))
ns = {'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
red_cases = ET.parse(red).findall('.//t:UnitTestResult', ns)
assert len(red_cases) == 2 and all(case.attrib['outcome'] == 'Failed' for case in red_cases)
assert all('nodemode:8' in case.findtext('./t:Output/t:ErrorInfo/t:Message', '', ns) for case in red_cases)
(OUT/'authenticated-red-control.json').write_text(json.dumps({'primary':str(red),'sha256':hashlib.sha256(red.read_bytes()).hexdigest(),
    'failedCases':2,'actualSharedWorker':'MSBuild /nologo /nodemode:8','sourceHelperUnpatched':BASE},indent=2)+'\n')

path = 'tests/PackageTests.Shared/PackageTestProcess.cs'
source = (WORK / path).read_text()
anchor = '        startInfo.WorkingDirectory = workingDirectory;'
assert source.count(anchor) == 1
replacement = '''        // Every bounded child owns and reaps its descendants, including dotnet
        // reached through a Python/shell carrier. A shared SDK 11 worker can be
        // reaped by another probe, so keep all such evaluations in this process.
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "false";
        startInfo.Environment["MSBUILDUSESERVER"] = "0";
''' + anchor
(WORK / path).write_text(source.replace(anchor, replacement))
(OUT/'incremental.diff').write_bytes(subprocess.check_output(['git','diff','--binary'],cwd=WORK))
build('build-contracts', contracts);build('build-ci', ci)
count = len(re.findall(r'^\s*\[(?:Fact|InlineData)(?:\]|\()', (WORK/test_path).read_text(), re.MULTILINE))
test_run('helper-supervisor-full', contracts, 'class', 'Hexalith.Parties.Contracts.Tests.Package.PackageTestProcessTests', count)
test_run('memories-full', ci, 'class', 'Hexalith.Parties.Ci.Tests.MemoriesRefreshCompilationTests', 24)
test_run('commons-full', ci, 'class', 'Hexalith.Parties.Ci.Tests.CommonsHttpRestoreRoutingTests', 39)
run('diff-check', ['git','diff','--check'])
ENV['PATH'] = '/home/administrator/.nvm/versions/node/v24.22.1/bin:' + ENV['PATH']
run('npm-ci', ['npm','ci','--ignore-scripts','--no-audit','--no-fund'])
message = OUT/'commit-message.txt';message.write_text('fix(test): isolate owned msbuild workers\n')
with message.open() as stream, (OUT/'commitlint.log').open('w') as log:
    code = subprocess.run([str(WORK/'node_modules/.bin/commitlint'),'--config',str(WORK/'commitlint.config.mjs')],
        cwd=WORK,env=ENV,stdin=stream,stdout=log,stderr=subprocess.STDOUT).returncode
assert code == 0
run('stage', ['git','add','--',path,test_path])
run('staged-check', ['git','diff','--cached','--check'])
assert set(subprocess.check_output(['git','diff','--cached','--name-only'],cwd=WORK,text=True).splitlines()) == {path,test_path}
run('commit', ['git','commit','-F',str(message)])
head = subprocess.check_output(['git','rev-parse','HEAD'],cwd=WORK,text=True).strip()
bundle = OUT/'caller.bundle';run('bundle',['git','bundle','create',str(bundle),'HEAD','^'+BASE])
branch = 'proof/story-2-8-patched-cancel-ordering-owned-msbuild-20261117'
run('retain-local-commit',['git','-C',str(SOURCE),'fetch','--no-tags',str(bundle),'HEAD:refs/heads/'+branch])
record={'caller':head,'parent':BASE,'branch':branch,'changedPaths':{p:hashlib.sha256((WORK/p).read_bytes()).hexdigest() for p in [path,test_path]},
    'sourceInventoryHelperCount':count,'genuineCausalRegressionFailedBeforeFix':2,'helperSupervisorCasesPassed':count,
    'memoriesCasesPassed':24,'commonsCasesPassed':39,'allNativeSourcePinsObserverBudgetsUnchanged':True,
    'commitlintPassed':True,'pushed':False,'dispatched':False,'candidateAcceptance':False,'toolAccepted':False,
    'hostedAttributionLimit':'Original hosted worker PIDs were not retained; causal mechanism reproduced with actual SDK 11 and task-owned supervisors.'}
(OUT/'caller.json').write_text(json.dumps(record,indent=2)+'\n');print(json.dumps(record),flush=True)
