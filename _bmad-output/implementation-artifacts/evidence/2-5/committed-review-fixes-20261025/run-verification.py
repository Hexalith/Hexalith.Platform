import json, pathlib, subprocess, sys, time
root=pathlib.Path('/home/administrator/projects/hexalith/platform')
evidence=root/'_bmad-output/implementation-artifacts/evidence/2-5/committed-review-fixes-20261025'
label=sys.argv[1];cwd=root/('references/Hexalith.Builds' if sys.argv[2]=='builds' else '.')
argv=sys.argv[3:]
start=time.monotonic()
with (evidence/(label+'.log')).open('w') as log:
 p=subprocess.run(argv,cwd=cwd,stdout=log,stderr=subprocess.STDOUT)
record={'cwd':str(cwd),'argv':argv,'exitCode':p.returncode,'durationSeconds':round(time.monotonic()-start,3),'log':label+'.log'}
commands=evidence/'commands.json'
rows=json.loads(commands.read_text()) if commands.exists() else []
rows.append(record);commands.write_text(json.dumps(rows,indent=2)+'\n')
print(json.dumps(record));print((evidence/(label+'.log')).read_text()[-12000:]);sys.exit(p.returncode)
