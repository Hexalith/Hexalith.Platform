"""Bind actual source/audit/documented graph controls to the final clean snapshot."""
from pathlib import Path
import json, subprocess, sys

out = Path(sys.argv[1]).resolve()
label = sys.argv[2] if len(sys.argv) > 2 else 'delivery'
tool = json.loads((out / 'published-artifact-identity.json').read_text())['toolRoot']
artifact_root = str(Path(tool).parents[3])
commands = [
    ['python3', '-B', '-c', "from pathlib import Path; import sys,json,subprocess; sys.path.insert(0,'eng'); from current_tuple_source import verify_checkout; revision=subprocess.check_output(['git','rev-parse','HEAD']).decode().strip(); print(json.dumps({'candidateRevision':revision,'sourceBinding':verify_checkout(Path.cwd(),revision)},indent=2))"],
    ['python3', '-B', 'eng/run_tuple_ci.py', '--audit-only', '--suite', 'current42', '--eventstore-sdk', '4.2.0', '--artifacts', artifact_root, '--evidence', str(out / ('audit-' + label))],
    ['python3', '-B', 'eng/test_tuple_collection_graph.py', '--evidence', str(out / ('collection-graph-' + label))],
    ['python3', '-B', 'eng/test_source_sdk_propagation.py', '--evidence', str(out / ('source-sdk-' + label)), '--published-tool-root', tool],
    ['dotnet', 'build', 'Hexalith.Platform.slnx', '--no-restore', '-c', 'Release', '-m:1', '-p:UseHexalithProjectReferences=false',
     '-p:HexalithBuildsRoot=' + str(Path.cwd() / 'references/Hexalith.Builds'), '-p:HexalithBuildsPublishedToolRoot=' + tool,
     '-p:HexalithEventStoreVersion=4.2.0', '-p:HexalithTenantsVersion=7.0.7', '-p:HexalithMemoriesVersion=3.0.27',
     '-p:HexalithPartiesVersion=3.1.1', '-p:HexalithMcpCliVersion=2.0.1'],
]
if len(sys.argv) > 3 and sys.argv[3] == 'source-only':
    commands = commands[:2]
revision = subprocess.check_output(['git', 'rev-parse', 'HEAD']).decode().strip()
records = []
for number, command in enumerate(commands):
    with (out / (label + '-control-' + str(number) + '.log')).open('x') as log:
        code = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=1200).returncode
    records.append({'candidateRevision': revision, 'cwd': str(Path.cwd()), 'command': command, 'exitCode': code})
    (out / (label + '-controls.json')).write_text(json.dumps(records, indent=2) + '\n')
    print(number, code, flush=True)
    if code:
        raise SystemExit(code)
