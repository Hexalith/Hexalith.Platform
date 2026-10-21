package main

import (
	"bytes"
	"context"
	"encoding/binary"
	"encoding/json"
	"io"
	"net"
	"os"
	"testing"
	"time"

	components "github.com/dapr/dapr/pkg/proto/components/v1"
	"golang.org/x/net/http2"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/status"
	"google.golang.org/protobuf/proto"
)

func testLog(t *testing.T) string {
	t.Helper()
	old := logFile
	file, err := os.CreateTemp(t.TempDir(), "wire-log-")
	if err != nil {
		t.Fatal(err)
	}
	logFile = file
	t.Cleanup(func() { file.Close(); logFile = old })
	return file.Name()
}

func TestNestedMapUnknownBeforeLossyDecode(t *testing.T) {
	testLog(t)
	raw := []byte{0x12, 0x08, 0x0a, 0x01, 'k', 0x12, 0x01, 'v', 0x18, 0x01}
	request := &components.InvokeRequest{}
	if err := proto.Unmarshal(raw, request); err != nil {
		t.Fatal(err)
	}
	if request.Metadata["k"] != "v" || len(request.ProtoReflect().GetUnknown()) != 0 {
		t.Fatal("regression requires pinned decoder's lossy map behavior")
	}
	u, n, err := rawUnknown(raw, request.ProtoReflect().Descriptor(), 0)
	if err != nil || u != 2 || n != 2 {
		t.Fatalf("raw result unknown=%d nested=%d err=%v", u, n, err)
	}
	if err := (closedCodec{}).Unmarshal(raw, &components.InvokeRequest{}); err == nil {
		t.Fatal("nested request accepted")
	}
	response := &components.InvokeResponse{Metadata: map[string]string{"fixture-nested-response": "true"}}
	if _, err := (closedCodec{}).Marshal(response); err == nil {
		t.Fatal("encoded nested response accepted")
	}
	normal := &components.InvokeRequest{Metadata: map[string]string{"k": "v"}}
	encoded, _ := proto.Marshal(normal)
	if err := (closedCodec{}).Unmarshal(encoded, &components.InvokeRequest{}); err != nil {
		t.Fatal(err)
	}
}

// The observer receives actual HTTP/2 DATA frames in both directions. Its raw
// check must expose map-entry unknowns even though the generated decoder drops them.
func TestObserverNestedRequestAndResponseWire(t *testing.T) {
	path := testLog(t)
	for _, direction := range []string{"request", "response"} {
		raw := []byte{0x12, 0x08, 0x0a, 0x01, 'k', 0x12, 0x01, 'v', 0x18, 0x01}
		payload := make([]byte, 5)
		binary.BigEndian.PutUint32(payload[1:], uint32(len(raw)))
		payload = append(payload, raw...)
		var encoded bytes.Buffer
		if err := http2.NewFramer(&encoded, nil).WriteData(1, true, payload); err != nil {
			t.Fatal(err)
		}
		source, writer := net.Pipe()
		destination, reader := net.Pipe()
		done := make(chan struct{})
		go func() {
			observe(source, destination, "component", direction, 1, false)
			source.Close()
			destination.Close()
			close(done)
		}()
		go func() { writer.Write(encoded.Bytes()); writer.Close() }()
		relayed, err := io.ReadAll(reader)
		reader.Close()
		if err != nil || !bytes.Equal(relayed, encoded.Bytes()) {
			t.Fatalf("wire changed: %v", err)
		}
		<-done
	}
	content, _ := os.ReadFile(path)
	count := 0
	for _, line := range bytes.Split(bytes.TrimSpace(content), []byte("\n")) {
		var row map[string]any
		if err := json.Unmarshal(line, &row); err != nil {
			t.Fatal(err)
		}
		if row["kind"] == "grpcFrame" {
			count++
			if row["rawWireValid"] != false || row["nestedUnknownBytes"] != float64(2) || row["unknownBytes"] != float64(2) || row["topLevelUnknownBytes"] != float64(0) {
				t.Fatalf("lost raw nested unknown: %v", row)
			}
		}
	}
	if count != 2 {
		t.Fatalf("expected request and response observations, got %d", count)
	}
}

type invokedBinding struct {
	components.UnimplementedOutputBindingServer
	calls int
}

func (b *invokedBinding) Invoke(context.Context, *components.InvokeRequest) (*components.InvokeResponse, error) {
	b.calls++
	return &components.InvokeResponse{Metadata: map[string]string{"fixture-nested-response": "true"}}, nil
}
func TestNestedMapUnknownRealGrpcCodec(t *testing.T) {
	testLog(t)
	listener, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	service := &invokedBinding{}
	server := grpc.NewServer(grpc.ForceServerCodec(closedCodec{}))
	components.RegisterOutputBindingServer(server, service)
	go server.Serve(listener)
	defer server.Stop()
	conn, err := grpc.NewClient(listener.Addr().String(), grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		t.Fatal(err)
	}
	defer conn.Close()
	client := components.NewOutputBindingClient(conn)
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	_, err = client.Invoke(ctx, &components.InvokeRequest{Metadata: map[string]string{"k": "v"}}, grpc.ForceCodec(nestedProbeCodec{mapField: 2}))
	if status.Code(err) != codes.Internal || service.calls != 0 {
		t.Fatalf("request reached handler or wrong error: calls=%d error=%v", service.calls, err)
	}
	_, err = client.Invoke(ctx, &components.InvokeRequest{Metadata: map[string]string{"k": "v"}})
	if status.Code(err) != codes.Internal || service.calls != 1 {
		t.Fatalf("nested response not refused by codec: calls=%d error=%v", service.calls, err)
	}
}

func TestConsumedStreamReleasesBackingStorage(t *testing.T) {
	streams := map[uint32][]byte{1: make([]byte, 1024*1024)}
	consumeStream(streams, 1, len(streams[1]))
	if _, exists := streams[1]; exists {
		t.Fatal("completed stream retains storage")
	}
	streams[2] = make([]byte, 1024*1024)
	streams[2][len(streams[2])-1] = 7
	consumeStream(streams, 2, len(streams[2])-1)
	if len(streams[2]) != 1 || cap(streams[2]) > 16 || streams[2][0] != 7 {
		t.Fatal("residual retains large preceding backing array")
	}
}
