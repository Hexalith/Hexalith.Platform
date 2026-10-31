"""Serial command recorder for this authorized continuation only."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import subprocess
import sys
import time

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
label, *command = sys.argv[1:]
if not label.replace('-', '').isalnum() or not command:
    raise SystemExit('Bounded command label and argv required')
log = OUT / (label + '.log')
record = {'label': label, 'argv': command, 'cwd': str(ROOT),
          'startedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'environment': {'HexalithCommonsRoot': '/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons'}}
environment = dict(os.environ, **record['environment'])
started = time.monotonic()
with log.open('xb') as output:
    result = subprocess.run(command, cwd=ROOT, env=environment, stdout=output, stderr=subprocess.STDOUT, check=False)
record.update(finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              elapsedSeconds=round(time.monotonic() - started, 3), exitCode=result.returncode,
              log=log.name, logSha256=hashlib.sha256(log.read_bytes()).hexdigest())
with (OUT / (label + '.command.json')).open('x') as output:
    json.dump(record, output, indent=2)
    output.write('\n')
print(json.dumps(record))
raise SystemExit(result.returncode)
