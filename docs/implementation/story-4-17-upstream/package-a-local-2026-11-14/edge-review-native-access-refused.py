"""Run through run-with-temp; write all disposable files beneath its TMPDIR."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--source', required=True)
parser.add_argument('--go', required=True)
parser.add_argument('--mod-cache', required=True)
args = parser.parse_args()
root = Path(os.environ['TMPDIR'])
repo = root / 'components-contrib'
shutil.copytree(args.source, repo, ignore=shutil.ignore_patterns('.git'))
p = repo / 'pubsub/rabbitmq/rabbitmq_publish_transport_test.go'
s = p.read_text()
needle = 'if stage == "confirmation" {'
assert s.count(needle) == 1
s = s.replace(needle, '''if stage == "access refused" {
        reason := "ACCESS_REFUSED - publisher permission revoked"
        args := make([]byte, 2)
        binary.BigEndian.PutUint16(args, 403)
        args = append(args, byte(len(reason)))
        args = append(args, []byte(reason)...)
        args = append(args, 0, 60, 0, 40)
        if err := p.send(1, 20, 40, args); err != nil { return err }
        if _, err := p.method(20, 41); err != nil { return err }
        close(entered)
        <-p.clientClosed
        return nil
    }
    if stage == "confirmation" {''')
s += '''
func TestReviewNativeAccessRefused(t *testing.T) {
    clientPipe, server := net.Pipe()
    client := &publishObservedConn{Conn: clientPipe, closed: make(chan struct{})}
    defer client.Close()
    defer server.Close()
    entered := make(chan struct{})
    peerDone := make(chan error, 1)
    go func() { defer server.Close(); peerDone <- (publishProtocolPeer{Conn: server, clientClosed: client.closed}).run("access refused", false, entered) }()
    r := newPublicPublisher(newPublishTestBroker())
    r.connection, r.channel, r.connectionCount = nil, nil, 0
    r.metadata.PublisherConfirm = true
    r.connectionDial = func(ctx context.Context, _ string, _ string, _ string, _ time.Duration, _ *tls.Config, _ bool) (rabbitMQConnectionBroker, rabbitMQChannelBroker, error) {
        return dialWithConfig(ctx, "amqp://guest:guest@localhost/?heartbeat=0", amqp.Config{}, func(context.Context, string, string) (net.Conn, error) { return client, nil })
    }
    ctx, cancel := context.WithTimeout(t.Context(), 400*time.Millisecond)
    defer cancel()
    done := startPublicPublish(r, ctx, "topic")
    awaitPublishGate(t, entered)
    require.NoError(t, r.channelMutex.Lock(t.Context()))
    r.channelMutex.Unlock()
    err := awaitPublish(t, done)
    t.Logf("public result after actual server channel.close(403): %v", err)
    if r.connection != nil { _ = r.connection.Abort() }
    require.NoError(t, <-peerDone)
    require.Error(t, err)
    require.Contains(t, err.Error(), "ACCESS_REFUSED", "the native broker rejection must survive the public Publish path")
}
'''
p.write_text(s)
env = os.environ.copy()
env.update({'GOCACHE': str(root / 'go-cache'), 'GOTMPDIR': str(root / 'go-tmp'), 'GOMODCACHE': args.mod_cache, 'GOPROXY': 'off', 'GOSUMDB': 'off'})
Path(env['GOTMPDIR']).mkdir()
subprocess.run([args.go, 'version'], check=True, env=env)
command = [args.go, 'test', './pubsub/rabbitmq', '-run', '^TestReviewNativeAccessRefused$', '-count=1', '-v']
print('Test command:', ' '.join(command), flush=True)
result = subprocess.run(command, cwd=repo, env=env)
print('Test exit code:', result.returncode, flush=True)
raise SystemExit(result.returncode)
