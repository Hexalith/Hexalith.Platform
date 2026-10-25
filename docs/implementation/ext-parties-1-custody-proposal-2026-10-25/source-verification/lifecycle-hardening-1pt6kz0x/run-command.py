import datetime, hashlib, json, pathlib, shlex, subprocess, sys
run = pathlib.Path(__file__).parent
label = sys.argv[1]
command = sys.argv[2:]
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
log = run / (label + '.log')
with log.open('xb') as stream:
    result = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT)
entry = {'label': label, 'cwd': str(pathlib.Path.cwd()), 'command': command, 'command_shell': shlex.join(command), 'started_at_utc': started, 'finished_at_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'exit_code': result.returncode, 'log': str(log), 'log_sha256': hashlib.sha256(log.read_bytes()).hexdigest()}
with (run / 'commands.jsonl').open('a') as stream:
    stream.write(json.dumps(entry) + '\n')
print(json.dumps(entry, indent=2))
print(''.join(log.read_text(errors='replace').splitlines(keepends=True)[-35:]))
sys.exit(result.returncode)
