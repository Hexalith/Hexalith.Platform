#!/usr/bin/env python3
"""Bounded actual SDK 11 process/IPC ownership experiment; no application services."""
import concurrent.futures
import json
import os
from pathlib import Path
import subprocess
import textwrap
import time
import xml.sax.saxutils as xml

ROOT = Path('/home/administrator/projects/hexalith/platform')
OUT = ROOT / 'artifacts/2-8-continuation-20261117/msbuild-ownership-probe'
OUT.mkdir(exist_ok=True)
SCRATCH = Path(os.environ['TMPDIR'])
source = subprocess.check_output(['git', '-C', str(ROOT / 'references/Hexalith.Parties'), 'show',
    '83916016e0fb64b5fbde79f05c2cf317d0900f99:tests/PackageTests.Shared/PackageTestProcessSupervisor.cs'], text=True)
supervisor = textwrap.dedent(source.split('private const string Script = """', 1)[1].split('""";', 1)[0]).strip() + '\n'

def identity(pid):
    try:
        s = Path('/proc/' + str(pid) + '/stat').read_text();f = s[s.rfind(')') + 2:].split()
        return {'pid': pid, 'parent': int(f[1]), 'birth': f[19], 'state': f[0],
            'role': [Path(x.decode(errors='replace')).name for x in Path('/proc/' + str(pid) + '/cmdline').read_bytes().split(b'\0')[:3] if x]}
    except (FileNotFoundError, ProcessLookupError): return None

def ancestry(pid):
    result = []
    while pid != os.getpid() and pid > 1:
        value = identity(pid)
        if value is None: break
        result.append(value);pid = value['parent']
    return result

caller = identity(os.getpid())
results = []
for mode in ['sdk-default', 'server-enabled', 'server-isolated']:
    work = SCRATCH / mode;work.mkdir()
    (work / 'global.json').write_text('{"sdk":{"version":"11.0.100","rollForward":"disable"}}')
    gate = work / 'gate.py'
    gate.write_text('''import json,os,sys,time
from pathlib import Path
marker,release=map(Path,sys.argv[1:])
marker.write_text(json.dumps({'pid':os.getpid(),'parent':os.getppid()}))
deadline=time.monotonic()+15
while not release.exists() and time.monotonic()<deadline:time.sleep(0.005)
''')
    env = dict(os.environ, TMPDIR=str(work), TMP=str(work), TEMP=str(work), DOTNET_CLI_HOME=str(work / 'cli'),
        DOTNET_NOLOGO='1', DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
    for key in ['DOTNET_CLI_USE_MSBUILD_SERVER', 'MSBUILDUSESERVER', 'CustomBeforeDirectoryBuildProps',
        'CustomAfterMicrosoftCommonProps', 'CustomBeforeDirectoryBuildTargets', 'CustomAfterDirectoryBuildTargets',
        'AfterMicrosoftNETSdkTargets', 'HEXALITH_SOURCE_MAPPING_HASH', 'Configuration']:
        env.pop(key, None)
    if mode != 'sdk-default':
        env['DOTNET_CLI_USE_MSBUILD_SERVER'] = 'false' if mode == 'server-isolated' else 'true'
        env['MSBUILDUSESERVER'] = '0' if mode == 'server-isolated' else '1'
    processes = [];handles = []
    try:
        for label in ['first', 'second']:
            marker, release = work / (label + '.json'), work / (label + '.release')
            project = work / (label + '.proj')
            command = 'python3 ' + str(gate) + ' ' + str(marker) + ' ' + str(release)
            project.write_text('<Project><Target Name="Run"><Exec Command="' + xml.escape(command, {'"':'&quot;'}) + '" /></Target></Project>')
            private = work / (label + '-supervisor');private.mkdir()
            out = (OUT / (mode + '-' + label + '.stdout')).open('w')
            err = (OUT / (mode + '-' + label + '.stderr')).open('w');handles.extend([out,err])
            args = ['python3', '-c', supervisor, str(private), '4900', str(os.getpid()), caller['birth'],
                'dotnet', 'msbuild', str(project), '-nologo', '-target:Run', '-verbosity:minimal']
            process = subprocess.Popen(args, cwd=work, env=env, stdout=out, stderr=err);processes.append(process)
            if label == 'first':
                deadline = time.monotonic() + 15
                while not marker.exists() and process.poll() is None and time.monotonic() < deadline:time.sleep(0.005)
                assert marker.exists(), 'First actual MSBuild target never started'
        time.sleep(0.25)
        chains = {}
        for label in ['first', 'second']:
            marker = work / (label + '.json')
            if marker.exists(): chains[label] = ancestry(json.loads(marker.read_text())['pid'])
        (work / 'first.release').touch()
        first_code = processes[0].wait(timeout=20)
        time.sleep(0.2)
        second_during_first_cleanup = processes[1].poll()
        if (work / 'second.json').exists():
            chains['secondAfterFirstCleanup'] = ancestry(json.loads((work / 'second.json').read_text())['pid'])
        (work / 'second.release').touch()
        second_code = processes[1].wait(timeout=20)
        for handle in handles: handle.flush()
        record = {'mode':mode,'sdk':'11.0.100','firstSupervisorExit':first_code,
            'secondExitAfterFirstCleanup':second_during_first_cleanup,'secondSupervisorExit':second_code,'ancestry':chains,
            'stdoutLengths':{label:(OUT/(mode+'-'+label+'.stdout')).stat().st_size for label in ['first','second']},
            'stderr':{label:(OUT/(mode+'-'+label+'.stderr')).read_text() for label in ['first','second']}}
        results.append(record);print(json.dumps(record),flush=True)
        (OUT/'results.json').write_text(json.dumps(results,indent=2)+'\n')
    finally:
        for label in ['first','second']:(work/(label+'.release')).touch()
        for process in processes:
            if process.poll() is None:
                (work/(('first' if process is processes[0] else 'second')+'-supervisor')/'stop').touch()
                process.wait(timeout=10)
        for handle in handles:handle.close()
