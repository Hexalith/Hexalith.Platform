#!/usr/bin/env bash
# Reproduce native asynchronous ACCESS_REFUSED handling in a private repository copy.
# Requires the pinned review toolchain/dependency paths to remain available.
set -euo pipefail
review_evidence_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
run-with-temp bash -s -- "$review_evidence_dir" <<'BASH'
set -euo pipefail
cp -a /tmp/hexalith-job.ZK82D9vZ/components-contrib "$TMPDIR/components-contrib"
cp -a /tmp/hexalith-job.DzTCBVAw/go-cache "$TMPDIR/go-cache"
export GOCACHE="$TMPDIR/go-cache"
export GOMODCACHE=/tmp/hexalith-job.DzTCBVAw/go-mod
export GOTOOLCHAIN=local
cd "$TMPDIR/components-contrib"
cp "$1/verification-review-native-access-refused-test.go.txt" pubsub/rabbitmq/review_native_denial_test.go
python3 <<'PY'
from pathlib import Path
p = Path('pubsub/rabbitmq/rabbitmq_publish_transport_test.go')
s = p.read_text()
needle = '\tconfirmation := make([]byte, 9)'
replacement = '''	if stage == "access refused" {
		reason := "ACCESS_REFUSED - write permission denied"
		args := make([]byte, 3+len(reason)+4)
		binary.BigEndian.PutUint16(args, 403)
		args[2] = byte(len(reason))
		copy(args[3:], reason)
		binary.BigEndian.PutUint16(args[3+len(reason):], 60)
		binary.BigEndian.PutUint16(args[5+len(reason):], 40)
		if err = p.send(1, 20, 40, args); err != nil { return err }
		if _, err = p.method(20, 41); err != nil { return err }
		close(entered)
		<-p.clientClosed
		return nil
	}
'''+needle
assert needle in s
p.write_text(s.replace(needle,replacement))
PY
/tmp/hexalith-job.DzTCBVAw/go/bin/go test ./pubsub/rabbitmq -run '^TestReviewNativePublishAccessRefused$' -count=1 -timeout=30s
BASH
