/*
Copyright 2026 The Dapr Authors
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

package rabbitmq

import (
	"context"
	"crypto/tls"
	"encoding/binary"
	"fmt"
	"io"
	"net"
	"sync"
	"testing"
	"time"

	amqp "github.com/rabbitmq/amqp091-go"
	"github.com/stretchr/testify/require"

	"github.com/dapr/components-contrib/pubsub"
)

// This is a minimal in-process AMQP protocol peer, not a RabbitMQ broker. It
// exercises the pinned client's real handshake, writes and confirmations over
// net.Pipe without inspecting or modifying any AMQP private fields.
type publishObservedConn struct {
	net.Conn
	closed chan struct{}
	once   sync.Once
}

func (c *publishObservedConn) Close() error {
	err := c.Conn.Close()
	c.once.Do(func() { close(c.closed) })
	return err
}

type publishProtocolPeer struct {
	net.Conn
	clientClosed <-chan struct{}
}

func (p publishProtocolPeer) frame() (byte, []byte, error) {
	header := make([]byte, 7)
	if _, err := io.ReadFull(p.Conn, header); err != nil {
		return 0, nil, err
	}
	size := binary.BigEndian.Uint32(header[3:])
	if size > 1<<20 {
		return 0, nil, fmt.Errorf("unexpected AMQP frame size %d", size)
	}
	body := make([]byte, int(size)+1)
	if _, err := io.ReadFull(p.Conn, body); err != nil {
		return 0, nil, err
	}
	if body[len(body)-1] != 0xce {
		return 0, nil, fmt.Errorf("invalid AMQP frame end")
	}
	return header[0], body[:len(body)-1], nil
}

func (p publishProtocolPeer) method(class, method uint16) ([]byte, error) {
	kind, body, err := p.frame()
	if err != nil {
		return nil, err
	}
	if kind != 1 || len(body) < 4 || binary.BigEndian.Uint16(body) != class || binary.BigEndian.Uint16(body[2:]) != method {
		return nil, fmt.Errorf("expected AMQP method %d.%d, got type=%d body=%x", class, method, kind, body)
	}
	return body[4:], nil
}

func (p publishProtocolPeer) send(channel, class, method uint16, args []byte) error {
	frame := make([]byte, 7+4+len(args)+1)
	frame[0] = 1
	binary.BigEndian.PutUint16(frame[1:], channel)
	binary.BigEndian.PutUint32(frame[3:], uint32(4+len(args)))
	binary.BigEndian.PutUint16(frame[7:], class)
	binary.BigEndian.PutUint16(frame[9:], method)
	copy(frame[11:], args)
	frame[len(frame)-1] = 0xce
	_, err := p.Write(frame)
	return err
}

func amqpLongString(s string) []byte {
	value := make([]byte, 4+len(s))
	binary.BigEndian.PutUint32(value, uint32(len(s)))
	copy(value[4:], s)
	return value
}

func (p publishProtocolPeer) stall(entered chan<- struct{}) error {
	close(entered)
	// Cancellation must close the client's exact raw transport and release this
	// read as well as the client operation. The caller joins both workers.
	<-p.clientClosed
	return nil
}

func (p publishProtocolPeer) run(stage string, passive bool, entered chan<- struct{}) error {
	if stage == "TLS handshake" {
		first := make([]byte, 1)
		if _, err := io.ReadFull(p.Conn, first); err != nil {
			return err
		}
		return p.stall(entered)
	}
	header := make([]byte, 8)
	if _, err := io.ReadFull(p.Conn, header); err != nil {
		return err
	}
	if string(header) != "AMQP\x00\x00\x09\x01" {
		return fmt.Errorf("invalid AMQP protocol header %x", header)
	}
	if stage == "AMQP handshake" {
		return p.stall(entered)
	}
	start := []byte{0, 9, 0, 0, 0, 0}
	start = append(start, amqpLongString("PLAIN")...)
	start = append(start, amqpLongString("en_US")...)
	if err := p.send(0, 10, 10, start); err != nil {
		return err
	}
	if _, err := p.method(10, 11); err != nil {
		return err
	}
	if stage == "authentication handshake" {
		return p.stall(entered)
	}
	if err := p.send(0, 10, 30, []byte{0, 0, 0, 2, 0, 0, 0, 0}); err != nil {
		return err
	}
	if _, err := p.method(10, 31); err != nil {
		return err
	}
	if _, err := p.method(10, 40); err != nil {
		return err
	}
	if err := p.send(0, 10, 41, []byte{0}); err != nil {
		return err
	}
	if _, err := p.method(20, 10); err != nil {
		return err
	}
	if stage == "channel open" {
		return p.stall(entered)
	}
	if err := p.send(1, 20, 11, []byte{0, 0, 0, 0}); err != nil {
		return err
	}
	if _, err := p.method(85, 10); err != nil {
		return err
	}
	if stage == "confirm setup" {
		return p.stall(entered)
	}
	if err := p.send(1, 85, 11, nil); err != nil {
		return err
	}
	declaration, err := p.method(40, 10)
	if err != nil {
		return err
	}
	if len(declaration) < 3 {
		return fmt.Errorf("short exchange.declare")
	}
	kindOffset := 3 + int(declaration[2])
	if len(declaration) <= kindOffset {
		return fmt.Errorf("short exchange kind")
	}
	flagsOffset := kindOffset + 1 + int(declaration[kindOffset])
	if len(declaration) <= flagsOffset {
		return fmt.Errorf("missing exchange flags")
	}
	if (declaration[flagsOffset]&1 != 0) != passive {
		return fmt.Errorf("incorrect passive declaration flag")
	}
	if stage == "active declaration" || stage == "passive declaration" {
		return p.stall(entered)
	}
	if err := p.send(1, 40, 11, nil); err != nil {
		return err
	}
	if _, err := p.method(60, 40); err != nil {
		return err
	}
	if stage == "write" {
		return p.stall(entered)
	}
	kind, content, err := p.frame()
	if err != nil {
		return err
	}
	if kind != 2 || len(content) < 12 {
		return fmt.Errorf("expected content header")
	}
	remaining := binary.BigEndian.Uint64(content[4:])
	for remaining > 0 {
		kind, body, readErr := p.frame()
		if readErr != nil {
			return readErr
		}
		if kind != 3 || uint64(len(body)) > remaining {
			return fmt.Errorf("invalid content body")
		}
		remaining -= uint64(len(body))
	}
	if stage == "confirmation" {
		return p.stall(entered)
	}
	if stage == "shutdown during confirmation" {
		close(entered)
		return nil
	}
	if stage == "blind review access refused" {
        reason := "ACCESS_REFUSED - write permission denied"
        args := []byte{1, 147, byte(len(reason))}
        args = append(args, []byte(reason)...)
        args = append(args, 0, 60, 0, 40)
        if err := p.send(1, 20, 40, args); err != nil { return err }
        if _, err := p.method(20, 41); err != nil { return err }
        close(entered)
        <-p.clientClosed
        return nil
    }
	confirmation := make([]byte, 9)
	binary.BigEndian.PutUint64(confirmation, 1)
	method := uint16(80)
	if stage == "nack" {
		method = 120
	}
	if err = p.send(1, 60, method, confirmation); err != nil {
		return err
	}
	close(entered)
	<-p.clientClosed
	return nil
}

func TestPublishPublicNativeTransportCancellation(t *testing.T) {
	t.Parallel()
	for _, stage := range []string{"dial", "TLS handshake", "AMQP handshake", "authentication handshake", "channel open", "confirm setup", "active declaration", "passive declaration", "write", "confirmation"} {
		t.Run(stage, func(t *testing.T) {
			t.Parallel()
			clientPipe, server := net.Pipe()
			client := &publishObservedConn{Conn: clientPipe, closed: make(chan struct{})}
			defer client.Close()
			defer server.Close()
			entered := make(chan struct{})
			peerDone := make(chan error, 1)
			if stage == "dial" {
				peerDone <- nil
			} else {
				go func() {
					defer server.Close()
					peerDone <- (publishProtocolPeer{Conn: server, clientClosed: client.closed}).run(stage, stage == "passive declaration", entered)
				}()
			}
			r := newPublicPublisher(newPublishTestBroker())
			r.connection, r.channel, r.connectionCount = nil, nil, 0
			r.metadata.PublisherConfirm = true
			if stage == "passive declaration" {
				r.metadata.ExchangeDeclareMode = exchangeDeclareModePassive
			}
			r.connectionDial = func(ctx context.Context, _ string, _ string, _ string, _ time.Duration, _ *tls.Config, _ bool) (rabbitMQConnectionBroker, rabbitMQChannelBroker, error) {
				uri := "amqp://guest:guest@localhost/?heartbeat=0"
				if stage == "TLS handshake" {
					uri = "amqps://guest:guest@localhost/?heartbeat=0"
				}
				return dialWithConfig(ctx, uri, amqp.Config{TLSClientConfig: &tls.Config{InsecureSkipVerify: true}}, func(ctx context.Context, _, _ string) (net.Conn, error) {
					if stage == "dial" {
						close(entered)
						<-ctx.Done()
						return nil, ctx.Err()
					}
					return client, nil
				})
			}
			ctx, cancel := context.WithCancel(t.Context())
			defer cancel()
			done := make(chan error, 1)
			go func() { done <- r.Publish(ctx, &pubsub.PublishRequest{Topic: "topic", Data: make([]byte, 16*1024)}) }()
			awaitPublishGate(t, entered)
			cancel()
			err := awaitPublish(t, done)
			require.ErrorIs(t, err, context.Canceled)
			require.NotContains(t, err.Error(), "ACCESS_REFUSED")
			require.NotContains(t, err.Error(), "username or password not allowed")
			select {
			case peerErr := <-peerDone:
				require.NoError(t, peerErr)
			case <-time.After(time.Second):
				t.Fatal("protocol peer did not terminate after cancellation")
			}
		})
	}
}

func TestPublishPublicNativeConfirmationOutcomes(t *testing.T) {
	t.Parallel()
	for _, stage := range []string{"ack", "nack", "shutdown during confirmation"} {
		t.Run(stage, func(t *testing.T) {
			t.Parallel()
			clientPipe, server := net.Pipe()
			client := &publishObservedConn{Conn: clientPipe, closed: make(chan struct{})}
			defer client.Close()
			defer server.Close()
			entered := make(chan struct{})
			peerDone := make(chan error, 1)
			go func() {
				defer server.Close()
				peerDone <- (publishProtocolPeer{Conn: server, clientClosed: client.closed}).run(stage, false, entered)
			}()
			r := newPublicPublisher(newPublishTestBroker())
			r.connection, r.channel, r.connectionCount = nil, nil, 0
			r.metadata.PublisherConfirm = true
			r.connectionDial = func(ctx context.Context, _ string, _ string, _ string, _ time.Duration, _ *tls.Config, _ bool) (rabbitMQConnectionBroker, rabbitMQChannelBroker, error) {
				return dialWithConfig(ctx, "amqp://guest:guest@localhost/?heartbeat=0", amqp.Config{}, func(context.Context, string, string) (net.Conn, error) { return client, nil })
			}
			ctx, cancel := context.WithCancel(t.Context())
			defer cancel()
			done := startPublicPublish(r, ctx, "topic")
			awaitPublishGate(t, entered)
			// The native confirmation has to leave publishSync before cancellation.
			require.NoError(t, r.channelMutex.Lock(t.Context()))
			r.channelMutex.Unlock()
			if stage == "ack" {
				require.NoError(t, awaitPublish(t, done))
			} else {
				cancel()
				err := awaitPublish(t, done)
				if stage == "nack" {
					require.EqualError(t, err, "did not receive confirmation of publishing")
				} else {
					require.Error(t, err)
					require.NotContains(t, err.Error(), "did not receive confirmation of publishing")
				}
			}
			// A finished ACK/NACK no longer owns a cancellation callback. Explicitly
			// close the raw transport to join the protocol peer, without a Close RPC.
			require.NoError(t, r.channelMutex.Lock(t.Context()))
			if r.connection != nil {
				_ = r.connection.Abort()
			}
			r.channelMutex.Unlock()
			select {
			case peerErr := <-peerDone:
				require.NoError(t, peerErr)
			case <-time.After(time.Second):
				t.Fatal("protocol peer did not terminate")
			}
		})
	}
}
