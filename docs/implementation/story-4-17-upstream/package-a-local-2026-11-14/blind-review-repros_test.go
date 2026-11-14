package rabbitmq

import (
	"context"
	"errors"
	"io"
	"net"
	"sync"
	"testing"
	"time"

	"github.com/dapr/components-contrib/pubsub"
	amqp "github.com/rabbitmq/amqp091-go"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

func TestBlindReviewLiveContextDeadlineError(t *testing.T) {
	b := newPublishTestBroker()
	r := newPublicPublisher(b)
	b.publish = func(context.Context, amqp.Publishing) (rabbitMQConfirmation, error) {
		return nil, context.DeadlineExceeded
	}
	if err := r.Publish(t.Context(), &pubsub.PublishRequest{Topic: "topic"}); err == nil {
		t.Fatal("Publish returned nil after three DeadlineExceeded failures with live caller context")
	}
}

func TestBlindReviewNACKThenUncertainWrite(t *testing.T) {
	b := newPublishTestBroker()
	r := newPublicPublisher(b)
	entered := make(chan struct{})
	b.publish = func(context.Context, amqp.Publishing) (rabbitMQConfirmation, error) {
		switch b.publishes.Load() {
		case 1:
			return fakeConfirmation{acked: false}, nil
		case 2:
			return nil, io.ErrClosedPipe
		}
		return publishConfirmationFunc(func(ctx context.Context) (bool, error) {
			close(entered)
			<-ctx.Done()
			return false, ctx.Err()
		}), nil
	}
	ctx, cancel := context.WithCancel(t.Context())
	defer cancel()
	done := startPublicPublish(r, ctx, "topic")
	awaitPublishGate(t, entered)
	cancel()
	if err := awaitPublish(t, done); !errors.Is(err, errPublishConfirmation) {
		t.Fatalf("earlier NACK replaced by uncertain failure: %v", err)
	}
}

type blindReviewObservedContext struct {
	context.Context
	entered chan struct{}
	once sync.Once
}

func (c *blindReviewObservedContext) Err() error {
	c.once.Do(func() { close(c.entered) })
	return c.Context.Err()
}

func TestBlindReviewQueuedClosedPublish(t *testing.T) {
	b := newPublishTestBroker()
	r := newPublicPublisher(b)
	ctx := &blindReviewObservedContext{Context: t.Context(), entered: make(chan struct{})}
	if err := r.channelMutex.Lock(t.Context()); err != nil {
		t.Fatal(err)
	}
	done := startPublicPublish(r, ctx, "topic")
	awaitPublishGate(t, ctx.entered)
	r.closed.Store(true)
	r.channel, r.connection = nil, nil
	r.channelMutex.Unlock()
	err := awaitPublish(t, done)
	if status.Code(err) != codes.FailedPrecondition {
		t.Fatalf("closed queued publish classified %v: %v", status.Code(err), err)
	}
}

func TestBlindReviewNativeReset(t *testing.T) {
	count := 0
	for range 100 {
		cp, sp := net.Pipe()
		client := &publishObservedConn{Conn: cp, closed: make(chan struct{})}
		entered := make(chan struct{})
		peerDone := make(chan error, 1)
		go func() {
			defer sp.Close()
			peerDone <- (publishProtocolPeer{Conn: sp, clientClosed: client.closed}).run("ack", false, entered)
		}()
		conn, ch, err := dialWithConfig(t.Context(), "amqp://guest:guest@localhost/?heartbeat=0", amqp.Config{}, func(context.Context, string, string) (net.Conn, error) { return client, nil })
		if err != nil {
			t.Fatal(err)
		}
		r := newPublicPublisher(newPublishTestBroker())
		r.connection, r.channel = conn, ch
		r.metadata.PublisherConfirm = true
		if err := ch.Confirm(false); err != nil {
			t.Fatal(err)
		}
		if err := r.Publish(t.Context(), &pubsub.PublishRequest{Topic: "topic"}); err != nil {
			t.Fatal(err)
		}
		if err := r.reset(); err != nil {
			count++
		}
		client.Close()
		select {
		case <-peerDone:
		case <-time.After(time.Second):
			t.Fatal("peer did not exit")
		}
	}
	if count > 0 {
		t.Fatalf("reset returned errors from its own forced transport closure on %d/100 otherwise healthy connections", count)
	}
}

func TestBlindReviewNativeAccessRefused(t *testing.T) {
	cp, sp := net.Pipe()
	client := &publishObservedConn{Conn: cp, closed: make(chan struct{})}
	defer client.Close()
	defer sp.Close()
	entered := make(chan struct{})
	peerDone := make(chan error, 1)
	go func() {
		defer sp.Close()
		peerDone <- (publishProtocolPeer{Conn: sp, clientClosed: client.closed}).run("blind review access refused", false, entered)
	}()
	conn, ch, err := dialWithConfig(t.Context(), "amqp://guest:guest@localhost/?heartbeat=0", amqp.Config{}, func(context.Context, string, string) (net.Conn, error) { return client, nil })
	if err != nil {
		t.Fatal(err)
	}
	r := newPublicPublisher(newPublishTestBroker())
	r.connection, r.channel = conn, ch
	r.metadata.PublisherConfirm = true
	r.metadata.ReconnectWait = time.Hour
	if err := ch.Confirm(false); err != nil {
		t.Fatal(err)
	}
	ctx, cancel := context.WithCancel(t.Context())
	defer cancel()
	done := startPublicPublish(r, ctx, "topic")
	awaitPublishGate(t, entered)
	if err := r.channelMutex.Lock(t.Context()); err != nil {
		t.Fatal(err)
	}
	r.channelMutex.Unlock()
	cancel()
	err = awaitPublish(t, done)
	client.Close()
	select {
	case peerErr := <-peerDone:
		if peerErr != nil { t.Fatal(peerErr) }
	case <-time.After(time.Second):
		t.Fatal("peer did not exit")
	}
	if status.Code(err) != codes.FailedPrecondition {
		t.Fatalf("real broker ACCESS_REFUSED replaced by %v: %v", status.Code(err), err)
	}
}
