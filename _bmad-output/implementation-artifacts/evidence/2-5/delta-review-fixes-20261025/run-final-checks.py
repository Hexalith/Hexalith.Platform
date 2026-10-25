"""Verify final review corrections with sequential owning-project builds and tests."""
from pathlib import Path
import json,subprocess,sys
out=Path(__file__).resolve().parent
root=out.parents[4]
builds=root/'references/Hexalith.Builds'
def run(name,argv,cwd=root):
    subprocess.run([sys.executable,str(out/'run-check.py'),name,*map(str,argv)],cwd=cwd,check=True)
run('module-build-final',['dotnet','build','test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj','-c','Debug','-m:1'],builds)
run('evidence-build',['dotnet','build','test/Hexalith.Builds.Evidence.Tests/Hexalith.Builds.Evidence.Tests.csproj','-c','Debug','-m:1'],builds)
run('platform-build',['dotnet','build','tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj','-c','Debug','-m:1'])
module=json.loads((out/'pre-workflow-review/module-tests.command.json').read_text())['argv']
module[module.index('-result-xml')+1]=str(out/'module-tests.xml')
run('module-tests',module,builds)
run('evidence-tests',['dotnet','test/Hexalith.Builds.Evidence.Tests/bin/Debug/net10.0/Hexalith.Builds.Evidence.Tests.dll','-result-xml',out/'evidence-tests.xml'],builds)
run('platform-tests',['dotnet','tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll','-method','*FinalCleanupAttemptsEveryOwnerDespiteFailuresAsync','-result-xml',out/'platform-tests.xml'])
run('retained-evidence',['python3','_bmad-output/implementation-artifacts/evidence/2-5/local-lifecycle-20261024/test_audit.py'])
run('schema-parity',['python3','_bmad-output/implementation-artifacts/evidence/2-5/committed-review-fixes-20261025/audit-schema-parity.py'])
