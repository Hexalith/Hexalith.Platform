"""Probe cancellation after native AMQP decoded 403 but its close-ok write stalls."""
import os
from pathlib import Path
import shutil
import subprocess
root = Path(os.environ['TMPDIR'])
repo = root / 'components-contrib'
shutil.copytree('/tmp/hexalith-job.ZK82D9vZ/components-contrib', repo, ignore=shutil.ignore_patterns('.git'))
p = repo / 'pubsub/rabbitmq/rabbitmq_publish_transport_test.go'
s = p.read_text()
s = s.replace('stage == "channel access refused" || stage == "connection access refused"', 'stage == "channel access refused" || stage == "connection access refused" || stage == "channel refused stalled reply" || stage == "connection refused stalled reply"')
s = s.replace('if stage == "connection access refused" {', 'if stage == "connection access refused" || stage == "connection refused stalled reply" {')
s = s.replace('if _, err = p.method(class, closeMethod+1); err != nil {', '''if strings.HasSuffix(stage, "stalled reply") {
            close(entered)
            <-p.clientClosed
            return nil
        }
        if _, err = p.method(class, closeMethod+1); err != nil {''')
s += '''
type reviewReplyPausedConn struct {
    net.Conn
    replyStarted chan struct{}
    abortStarted chan struct{}
    allowClose <-chan struct{}
    replyOnce sync.Once
    abortOnce sync.Once
}
func (c *reviewReplyPausedConn) Write(body []byte) (int, error) {
    if len(body) >= 11 && body[0] == 1 {
        class := binary.BigEndian.Uint16(body[7:9])
        method := binary.BigEndian.Uint16(body[9:11])
        if (class == 20 && method == 41) || (class == 10 && method == 51) {
            c.replyOnce.Do(func() { close(c.replyStarted) })
        }
    }
    return c.Conn.Write(body)
}
func (c *reviewReplyPausedConn) Close() error {
    c.abortOnce.Do(func() { close(c.abortStarted) })
    <-c.allowClose
    return c.Conn.Close()
}
type reviewCanceledErrContext struct {
    context.Context
    observed chan struct{}
    once sync.Once
}
func (c *reviewCanceledErrContext) Err() error {
    err := c.Context.Err()
    if err != nil { c.once.Do(func() { close(c.observed) }) }
    return err
}
func TestReviewNativeDeniedReplyCancellation(t *testing.T) {
    for _, stage := range []string{"channel refused stalled reply", "connection refused stalled reply"} {
        t.Run(stage, func(t *testing.T) {
            base, entered, peerDone := nativePublishPeer(t, stage)
            allowClose := make(chan struct{})
            var release sync.Once
            releaseClose := func() { release.Do(func() { close(allowClose) }) }
            defer releaseClose()
            transport := &reviewReplyPausedConn{Conn: base, replyStarted: make(chan struct{}), abortStarted: make(chan struct{}), allowClose: allowClose}
            r := newPublicPublisher(newPublishTestBroker())
            r.connection, r.channel, r.connectionCount = nil, nil, 0
            r.metadata.PublisherConfirm = true
            r.connectionDial = func(ctx context.Context, _ string, _ string, _ string, _ time.Duration, _ *tls.Config, _ bool) (rabbitMQConnectionBroker, rabbitMQChannelBroker, error) {
                r.metadata.ReconnectWait = time.Hour
                return dialWithConfig(ctx, "amqp://guest:guest@localhost/?heartbeat=0", amqp.Config{}, func(context.Context, string, string) (net.Conn, error) { return transport, nil })
            }
            underlying, cancel := context.WithCancel(t.Context())
            defer cancel()
            ctx := &reviewCanceledErrContext{Context: underlying, observed: make(chan struct{})}
            done := startPublicPublish(r, ctx, "topic")
            awaitPublishGate(t, entered)
            awaitPublishGate(t, transport.replyStarted)
            cancel()
            awaitPublishGate(t, transport.abortStarted)
            awaitPublishGate(t, ctx.observed)
            releaseClose()
            err := awaitPublish(t, done)
            t.Logf("result after native decoded broker 403 and tried close-ok: %v", err)
            _ = base.Close()
            awaitNativePeer(t, peerDone)
            require.ErrorIs(t, err, context.Canceled, "NotifyClose is not observable before this cancellation result")
        })
    }
}
'''
p.write_text(s)
env = os.environ.copy()
env.update({'GOCACHE': str(root / 'go-cache'), 'GOTMPDIR': str(root / 'go-tmp'), 'GOMODCACHE': '/tmp/hexalith-job.DzTCBVAw/go-mod', 'GOPROXY': 'off', 'GOSUMDB': 'off'})
Path(env['GOTMPDIR']).mkdir()
subprocess.run(['/tmp/hexalith-job.DzTCBVAw/go/bin/go', 'version'], check=True, env=env)
command = ['/tmp/hexalith-job.DzTCBVAw/go/bin/go', 'test', './pubsub/rabbitmq', '-run', '^(TestReviewNativeDeniedReplyCancellation|TestPublishPublicNativeAccessRefused|TestPublishPublicNativeConfirmationOutcomes|TestPublishPublicNACKSurvivesTransportUncertainty)$', '-count=1', '-v']
print('Test command:', ' '.join(command), flush=True)
result = subprocess.run(command, cwd=repo, env=env)
print('Test exit code:', result.returncode, flush=True)
raise SystemExit(result.returncode)
