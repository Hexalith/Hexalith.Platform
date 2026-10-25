import json, pathlib, subprocess, sys, time
root = pathlib.Path(__file__).parent
name = sys.argv[1]
argv = sys.argv[2:]
start = time.monotonic()
with (root / (name + '.log')).open('w') as output:
    completed = subprocess.run(argv, stdout=output, stderr=subprocess.STDOUT)
record = {'cwd': str(pathlib.Path.cwd()), 'argv': argv, 'exitCode': completed.returncode, 'durationSeconds': round(time.monotonic() - start, 3), 'log': name + '.log'}
(root / (name + '.command.json')).write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps(record))
sys.exit(completed.returncode)
