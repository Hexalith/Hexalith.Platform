import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

art = Path(__file__).resolve().parent
repo = Path('/tmp/hexalith-job.ZK82D9vZ/components-contrib')
scratch = Path(os.environ['TMPDIR'])
cache = scratch / 'go-cache'
shutil.copytree('/tmp/hexalith-job.DzTCBVAw/go-build', cache, copy_function=os.link)
overlay = scratch / 'overlay.json'
overlay.write_text(json.dumps({'Replace': {
    str(repo/'pubsub/rabbitmq/blind_review_overlay_test.go'): str(art/'blind-review-repros_test.go'),
    str(repo/'pubsub/rabbitmq/rabbitmq_publish_transport_test.go'): str(art/'blind-review-transport-overlay_test.go'),
}}))
env = os.environ.copy()
env['PATH'] = '/tmp/hexalith-job.DzTCBVAw/go/bin:' + env['PATH']
env['GOMODCACHE'] = '/tmp/hexalith-job.DzTCBVAw/go-mod'
env['GOCACHE'] = str(cache)
command = ['go', 'test', '-overlay', str(overlay), './pubsub/rabbitmq', '-run', '^TestBlindReview', '-count=1', '-timeout', '40s']
result = subprocess.run(command, cwd=repo, env=env, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
(art/'blind-review-output.log').write_text(result.stdout)
for line in result.stdout.splitlines():
    if line.startswith(('--- FAIL:', '    blind_review_overlay_test.go:', 'FAIL')):
        print(line)
sys.exit(result.returncode)
