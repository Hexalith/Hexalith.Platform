import json, pathlib, subprocess, sys, time
folder = pathlib.Path(__file__).resolve().parent
name, cwd, *command = sys.argv[1:]
log = folder / (name + '.txt')
if log.exists():
    raise SystemExit('Refusing to replace existing check evidence: ' + str(log))
start = time.monotonic()
with log.open('w') as stream:
    result = subprocess.run(command, cwd=cwd, stdout=stream, stderr=subprocess.STDOUT)
receipt = {'command': command, 'cwd': cwd, 'exitCode': result.returncode, 'seconds': round(time.monotonic() - start, 3), 'output': str(log)}
(folder / (name + '.command.json')).write_text(json.dumps(receipt, indent=2) + '\n')
print(json.dumps(receipt))
raise SystemExit(result.returncode)
