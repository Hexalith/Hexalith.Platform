// Synthetic transport instruments only. No secrets provider or semantic authority.
package main

import (
	"bytes"
	"compress/gzip"
	"context"
	"crypto/sha256"
	"encoding/binary"
	"encoding/hex"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"sync"
	"time"
	"unicode/utf8"

	components "github.com/dapr/dapr/pkg/proto/components/v1"
	runtimev1 "github.com/dapr/dapr/pkg/proto/runtime/v1"
	"golang.org/x/net/http2"
	"golang.org/x/net/http2/hpack"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/metadata"
	"google.golang.org/grpc/reflection"
	"google.golang.org/grpc/stats"
	"google.golang.org/grpc/status"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/encoding/protowire"
	"google.golang.org/protobuf/proto"
	"google.golang.org/protobuf/reflect/protodesc"
)

const requestCap = 128 * 1024 * 1024
const dataCap = 64 * 1024 * 1024
const responseExclusive = 1024 * 1024

var logMu sync.Mutex
var logFile *os.File
var incarnation string

func record(m map[string]any) {
	logMu.Lock()
	defer logMu.Unlock()
	m["incarnation"] = incarnation
	m["timeUnixNano"] = time.Now().UnixNano()
	if err := json.NewEncoder(logFile).Encode(m); err != nil {
		panic(err)
	}
	if err := logFile.Sync(); err != nil {
		panic(err)
	}
}
func digest(b []byte) string { h := sha256.Sum256(b); return hex.EncodeToString(h[:]) }
func die(err error) {
	if err != nil {
		panic(err)
	}
}
func listen(address string) net.Listener {
	network := "tcp"
	if strings.HasPrefix(address, "/") {
		network = "unix"
	}
	l, err := net.Listen(network, address)
	die(err)
	if network == "unix" {
		die(os.Chmod(address, 0600))
	}
	return l
}
func dial(address string) (net.Conn, error) {
	network := "tcp"
	if strings.HasPrefix(address, "/") {
		network = "unix"
	}
	return net.Dial(network, address)
}

// The observer forwards original HTTP/2 bytes without terminating gRPC or
// reconstructing protobuf messages. Each direction has its own HPACK state.
func proxy(address, target, hop string) {
	l := listen(address)
	var connection int
	for {
		c, err := l.Accept()
		die(err)
		connection++
		id := connection
		go func() {
			defer c.Close()
			upstream, err := dial(target)
			if err != nil {
				record(map[string]any{"kind": "connectError", "hop": hop, "errorType": "dial"})
				return
			}
			defer upstream.Close()
			var wg sync.WaitGroup
			wg.Add(2)
			go func() { defer wg.Done(); observe(c, upstream, hop, "request", id, true); upstream.Close() }()
			go func() { defer wg.Done(); observe(upstream, c, hop, "response", id, false); c.Close() }()
			wg.Wait()
		}()
	}
}
func observe(src net.Conn, dst net.Conn, hop, direction string, connection int, preface bool) {
	if preface {
		b := make([]byte, len(http2.ClientPreface))
		if _, err := io.ReadFull(src, b); err != nil {
			return
		}
		if !bytes.Equal(b, []byte(http2.ClientPreface)) {
			return
		}
		if _, err := dst.Write(b); err != nil {
			return
		}
	}
	fr := http2.NewFramer(dst, src)
	fr.SetMaxReadFrameSize(16 * 1024 * 1024)
	decoder := hpack.NewDecoder(4096, nil)
	data := map[uint32][]byte{}
	discarded := map[uint32]bool{}
	headerBlocks := map[uint32][]byte{}
	cases := map[uint32]string{}
	for {
		f, err := fr.ReadFrame()
		if err != nil {
			return
		}
		h := f.Header()
		var payload []byte
		// Serialize the exact frame through a separate framer so no semantic
		// gRPC proxy can accidentally repair or omit a measured field.
		switch x := f.(type) {
		case *http2.DataFrame:
			b := x.Data()
			if !discarded[h.StreamID] {
				data[h.StreamID] = append(data[h.StreamID], b...)
			}
			if len(data[h.StreamID]) > requestCap+65536 {
				record(map[string]any{"kind": "observerCap", "hop": hop})
				return
			}
			for len(data[h.StreamID]) >= 5 {
				buf := data[h.StreamID]
				n := int(binary.BigEndian.Uint32(buf[1:5]))
				if n > requestCap {
					record(map[string]any{"kind": "declaredOverCap", "hop": hop, "direction": direction, "connection": connection, "stream": h.StreamID, "bytes": n})
					delete(data, h.StreamID)
					discarded[h.StreamID] = true
					break
				}
				if len(buf) < n+5 {
					break
				}
				body := buf[5 : n+5]
				m := map[string]any{"kind": "grpcFrame", "hop": hop, "direction": direction, "connection": connection, "stream": h.StreamID, "protobufBytes": n, "wireBytes": n + 5, "compressed": buf[0] != 0, "sha256": digest(body)}
				if value := cases[h.StreamID]; value != "" {
					m["syntheticCase"] = value
				}
				var message proto.Message
				if direction == "request" {
					if hop == "api" {
						message = &runtimev1.InvokeBindingRequest{}
					} else {
						message = &components.InvokeRequest{}
					}
				} else {
					if hop == "api" {
						message = &runtimev1.InvokeBindingResponse{}
					} else {
						message = &components.InvokeResponse{}
					}
				}
				if buf[0] == 0 {
					u, n, wireErr := rawUnknown(body, message.ProtoReflect().Descriptor(), 0)
					m["rawWireValid"] = wireErr == nil && u == 0
					m["rawUnknownBytes"] = u
					m["nestedUnknownBytes"] = n
					if proto.Unmarshal(body, message) == nil {
						switch p := message.(type) {
						case *runtimev1.InvokeBindingRequest:
							m["dataBytes"] = len(p.Data)
							m["dataSha256"] = digest(p.Data)
							m["operation"] = p.Operation
							m["metadata"] = metaSummary(p.Metadata)
						case *components.InvokeRequest:
							if value := p.Metadata["x-f1-case"]; value != "" {
								m["syntheticCase"] = value
							}
							m["dataBytes"] = len(p.Data)
							m["dataSha256"] = digest(p.Data)
							m["operation"] = p.Operation
							m["metadata"] = metaSummary(p.Metadata)
						case *components.InvokeResponse:
							m["dataBytes"] = len(p.Data)
							m["contentTypeBytes"] = len(p.ContentType)
							m["metadata"] = metaSummary(p.Metadata)
						case *runtimev1.InvokeBindingResponse:
							m["dataBytes"] = len(p.Data)
							m["metadata"] = metaSummary(p.Metadata)
						}
						m["topLevelUnknownBytes"] = len(message.ProtoReflect().GetUnknown())
						m["unknownBytes"] = u
					}
				}
				record(m)
				consumeStream(data, h.StreamID, n+5)
			}
			payload = append([]byte(nil), b...)
		case *http2.HeadersFrame:
			headerBlocks[h.StreamID] = append(headerBlocks[h.StreamID], x.HeaderBlockFragment()...)
		case *http2.ContinuationFrame:
			headerBlocks[h.StreamID] = append(headerBlocks[h.StreamID], x.HeaderBlockFragment()...)
		case *http2.SettingsFrame:
			_ = x.ForeachSetting(func(s http2.Setting) error {
				if s.ID == http2.SettingMaxHeaderListSize {
					record(map[string]any{"kind": "advertisedHeaderLimit", "hop": hop, "direction": direction, "bytes": s.Val})
				}
				return nil
			})
		}
		if (h.Type == http2.FrameHeaders || h.Type == http2.FrameContinuation) && h.Flags.Has(http2.FlagHeadersEndHeaders) {
			fields, err := decoder.DecodeFull(headerBlocks[h.StreamID])
			delete(headerBlocks, h.StreamID)
			if err == nil {
				size := 0
				entries := []map[string]any{}
				for _, v := range fields {
					if v.Name == "x-f1-case" {
						cases[h.StreamID] = v.Value
					}
					size += len(v.Name) + len(v.Value) + 32
					entries = append(entries, map[string]any{"name": v.Name, "nameBytes": len(v.Name), "valueBytes": len(v.Value)})
				}
				record(map[string]any{"kind": "decodedHeaders", "hop": hop, "direction": direction, "connection": connection, "stream": h.StreamID, "decodedListBytes": size, "entries": entries})
			}
		}
		// Framer does not expose a generic payload. Preserve frames using their
		// exact encoding; padding is intentionally unsupported and fails closed.
		var outErr error
		switch x := f.(type) {
		case *http2.DataFrame:
			if h.Flags.Has(http2.FlagDataPadded) {
				return
			}
			outErr = fr.WriteData(h.StreamID, x.StreamEnded(), payload)
		case *http2.HeadersFrame:
			if h.Flags.Has(http2.FlagHeadersPadded) || h.Flags.Has(http2.FlagHeadersPriority) {
				return
			}
			outErr = fr.WriteHeaders(http2.HeadersFrameParam{StreamID: h.StreamID, BlockFragment: x.HeaderBlockFragment(), EndStream: x.StreamEnded(), EndHeaders: x.HeadersEnded()})
		case *http2.ContinuationFrame:
			outErr = fr.WriteContinuation(h.StreamID, x.HeadersEnded(), x.HeaderBlockFragment())
		case *http2.SettingsFrame:
			if x.IsAck() {
				outErr = fr.WriteSettingsAck()
			} else {
				settings := []http2.Setting{}
				_ = x.ForeachSetting(func(s http2.Setting) error { settings = append(settings, s); return nil })
				outErr = fr.WriteSettings(settings...)
			}
		case *http2.WindowUpdateFrame:
			outErr = fr.WriteWindowUpdate(h.StreamID, x.Increment)
		case *http2.PingFrame:
			outErr = fr.WritePing(x.IsAck(), x.Data)
		case *http2.RSTStreamFrame:
			record(map[string]any{"kind": "streamReset", "hop": hop, "direction": direction, "connection": connection, "stream": h.StreamID, "code": x.ErrCode.String()})
			outErr = fr.WriteRSTStream(h.StreamID, x.ErrCode)
		case *http2.GoAwayFrame:
			outErr = fr.WriteGoAway(x.LastStreamID, x.ErrCode, x.DebugData())
		case *http2.PriorityFrame:
			outErr = fr.WritePriority(h.StreamID, x.PriorityParam)
		default:
			record(map[string]any{"kind": "unsupportedFrame", "hop": hop, "type": int(h.Type)})
			return
		}
		if outErr != nil {
			return
		}
		if h.Type == http2.FrameRSTStream || ((h.Type == http2.FrameData || h.Type == http2.FrameHeaders) && h.Flags.Has(http2.FlagDataEndStream)) {
			delete(data, h.StreamID)
			delete(discarded, h.StreamID)
			delete(headerBlocks, h.StreamID)
			delete(cases, h.StreamID)
		}
	}
}
func metaSummary(m map[string]string) map[string]any {
	total, maxKey, maxValue := 0, 0, 0
	for k, v := range m {
		total += len(k) + len(v)
		if len(k) > maxKey {
			maxKey = len(k)
		}
		if len(v) > maxValue {
			maxValue = len(v)
		}
	}
	return map[string]any{"entries": len(m), "aggregateUtf8Bytes": total, "maximumKeyUtf8Bytes": maxKey, "maximumValueUtf8Bytes": maxValue}
}
func validateMetadata(m map[string]string) error {
	if len(m) > 64 {
		return status.Error(codes.ResourceExhausted, "synthetic complete metadata entry cap")
	}
	total := 0
	for k, v := range m {
		if !utf8.ValidString(k) || !utf8.ValidString(v) || len(k) > 256 || len(v) > 4096 {
			return status.Error(codes.ResourceExhausted, "synthetic complete metadata field cap")
		}
		total += len(k) + len(v)
	}
	if total > 65536 {
		return status.Error(codes.ResourceExhausted, "synthetic complete metadata aggregate cap")
	}
	return nil
}

type binding struct {
	components.UnimplementedOutputBindingServer
	sink string
}

func (*binding) Init(context.Context, *components.OutputBindingInitRequest) (*components.OutputBindingInitResponse, error) {
	return &components.OutputBindingInitResponse{}, nil
}
func (*binding) Ping(context.Context, *components.PingRequest) (*components.PingResponse, error) {
	return &components.PingResponse{}, nil
}
func (*binding) ListOperations(context.Context, *components.ListOperationsRequest) (*components.ListOperationsResponse, error) {
	return &components.ListOperationsResponse{Operations: []string{"destroy-manifest", "admit-transition", "fixture-send", "fixture-status", "fixture-response"}}, nil
}
func (b *binding) Invoke(ctx context.Context, r *components.InvokeRequest) (*components.InvokeResponse, error) {
	record(map[string]any{"kind": "componentAdmission", "syntheticCase": r.Metadata["x-f1-case"], "operation": r.Operation, "dataBytes": len(r.Data), "dataSha256": digest(r.Data), "protobufBytes": proto.Size(r), "metadata": metaSummary(r.Metadata)})
	if len(r.Data) > dataCap {
		record(map[string]any{"kind": "componentRefusal", "stage": "data", "syntheticCase": r.Metadata["x-f1-case"]})
		return nil, status.Error(codes.ResourceExhausted, "synthetic data cap")
	}
	if err := validateMetadata(r.Metadata); err != nil {
		record(map[string]any{"kind": "componentRefusal", "stage": "metadata", "syntheticCase": r.Metadata["x-f1-case"]})
		return nil, err
	}
	if len(r.ProtoReflect().GetUnknown()) > 0 {
		return nil, status.Error(codes.InvalidArgument, "unknown request fields")
	}
	response := &components.InvokeResponse{Data: []byte(`{"Synthetic":true,"CompletionPermission":false}`), Metadata: map[string]string{"fixture": "synthetic"}, ContentType: "application/json"}
	if r.Operation == "fixture-response" {
		n, _ := strconv.Atoi(r.Metadata["response-wire-bytes"])
		response.Data = nil
		if n > 0 {
			response.Data = make([]byte, n-64)
			for proto.Size(response)+5 < n {
				response.Data = append(response.Data, 0)
			}
			for proto.Size(response)+5 > n {
				response.Data = response.Data[:len(response.Data)-1]
			}
		}
		if r.Metadata["response-unknown"] == "true" {
			response.ProtoReflect().SetUnknown([]byte{0x98, 0x06, 0x01})
		}
		if r.Metadata["response-nested-unknown"] == "true" {
			response.Metadata["fixture-nested-response"] = "true"
		}
		if r.Metadata["response-compression"] == "true" {
			record(map[string]any{"kind": "componentRefusal", "stage": "response-compression"})
			return nil, status.Error(codes.InvalidArgument, "closed profile refuses compression")
		}
	} else {
		transport := &http.Transport{DialContext: func(context.Context, string, string) (net.Conn, error) { return dial(b.sink) }}
		client := &http.Client{Transport: transport, Timeout: 10 * time.Second}
		defer transport.CloseIdleConnections()
		path := "/send"
		if r.Operation == "fixture-status" {
			path = "/status"
		}
		var sinkResult []byte
		call := func() error {
			req, err := http.NewRequest(http.MethodPost, "http://synthetic"+path, bytes.NewReader(r.Data))
			if err != nil {
				return err
			}
			req.Header.Set("X-F1-Identity", r.Metadata["fixture-identity"])
			req.Header.Set("X-F1-Incarnation", incarnation)
			req.Header.Set("X-F1-Case", r.Metadata["x-f1-case"])
			resp, err := client.Do(req)
			if err != nil {
				return err
			}
			defer resp.Body.Close()
			if resp.StatusCode != http.StatusOK {
				return fmt.Errorf("synthetic sink refused")
			}
			sinkResult, err = io.ReadAll(io.LimitReader(resp.Body, responseExclusive))
			if len(sinkResult) >= responseExclusive {
				return fmt.Errorf("synthetic sink response cap")
			}
			return err
		}
		if err := call(); err != nil {
			return nil, status.Error(codes.Unavailable, "synthetic sink unavailable")
		}
		if r.Operation == "fixture-status" {
			response.Data = sinkResult
		}
		if r.Metadata["fixture-fault"] == "late-duplicate" {
			time.Sleep(200 * time.Millisecond)
			if err := call(); err != nil {
				return nil, err
			}
		}
		if r.Metadata["fixture-fault"] == "cancellation" {
			time.Sleep(300 * time.Millisecond)
		}
		if r.Metadata["fixture-fault"] == "lost-result" {
			return nil, status.Error(codes.Unavailable, "synthetic lost result after sink")
		}
	}
	n := proto.Size(response) + 5
	record(map[string]any{"kind": "responseGuard", "wireBytes": n, "unknownBytes": len(response.ProtoReflect().GetUnknown()), "contentTypeBytes": len(response.ContentType), "metadata": metaSummary(response.Metadata)})
	if n >= responseExclusive || len(response.ProtoReflect().GetUnknown()) > 0 {
		record(map[string]any{"kind": "componentRefusal", "stage": "response-size-or-unknown"})
		return nil, status.Error(codes.ResourceExhausted, "synthetic complete response cap or unknown fields")
	}
	return response, nil
}
func component(address, sink string) {
	server := grpc.NewServer(grpc.ForceServerCodec(closedCodec{}), grpc.StatsHandler(wireStats{}), grpc.MaxRecvMsgSize(requestCap), grpc.MaxSendMsgSize(responseExclusive-1), grpc.MaxHeaderListSize(65536), grpc.UnaryInterceptor(func(ctx context.Context, req any, info *grpc.UnaryServerInfo, handler grpc.UnaryHandler) (any, error) {
		md, _ := metadata.FromIncomingContext(ctx)
		if vals := md.Get("grpc-encoding"); len(vals) > 0 && vals[0] != "identity" {
			return nil, status.Error(codes.InvalidArgument, "compressed requests refused")
		}
		return handler(ctx, req)
	}))
	components.RegisterOutputBindingServer(server, &binding{sink: sink})
	reflection.Register(server)
	die(server.Serve(listen(address)))
}

type wireStats struct{}

func (wireStats) TagRPC(ctx context.Context, _ *stats.RPCTagInfo) context.Context   { return ctx }
func (wireStats) TagConn(ctx context.Context, _ *stats.ConnTagInfo) context.Context { return ctx }
func (wireStats) HandleConn(context.Context, stats.ConnStats)                       {}
func (wireStats) HandleRPC(_ context.Context, s stats.RPCStats) {
	if h, ok := s.(*stats.InHeader); ok {
		record(map[string]any{"kind": "componentTransportHeader", "compression": h.Compression, "method": h.FullMethod})
	}
}
func sink(address string) {
	var mu sync.Mutex
	counts := map[string]int{}
	originalDigests := map[string]string{}
	originalProducers := map[string]string{}
	h := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		defer r.Body.Close()
		data, err := io.ReadAll(io.LimitReader(r.Body, dataCap+1))
		if err != nil {
			w.WriteHeader(400)
			return
		}
		identity := r.Header.Get("X-F1-Identity")
		if identity == "" {
			identity = digest(data)
		}
		if len(data) > dataCap {
			w.WriteHeader(http.StatusRequestEntityTooLarge)
			return
		}
		mu.Lock()
		defer mu.Unlock()
		dataHash := digest(data)
		if previous, exists := originalDigests[identity]; exists && previous != dataHash {
			w.WriteHeader(http.StatusConflict)
			return
		}
		if r.URL.Path == "/send" {
			if counts[identity] == 0 {
				originalDigests[identity] = dataHash
				originalProducers[identity] = r.Header.Get("X-F1-Incarnation")
			}
			counts[identity]++
		}
		record(map[string]any{"kind": "sink", "syntheticCase": r.Header.Get("X-F1-Case"), "path": r.URL.Path, "identity": identity, "attempts": counts[identity], "dataBytes": len(data), "dataSha256": digest(data), "producerIncarnation": r.Header.Get("X-F1-Incarnation")})
		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(map[string]any{"synthetic": true, "identity": identity, "attempts": counts[identity],
			"originalDataSha256": originalDigests[identity], "originalProducerIncarnation": originalProducers[identity],
			"statusOnly": r.URL.Path == "/status", "CompletionPermission": false})
	})
	die(http.Serve(listen(address), h))
}

// The raw probe deliberately ignores peer SETTINGS for the one-byte-over header
// case, proving the server boundary instead of the client's preflight check.
func rawProbe(address, hop string, headerList int, compressed bool) {
	c, err := dial(address)
	die(err)
	defer c.Close()
	die(c.SetDeadline(time.Now().Add(10 * time.Second)))
	_, err = c.Write([]byte(http2.ClientPreface))
	die(err)
	fr := http2.NewFramer(c, c)
	die(fr.WriteSettings())
	method := "/dapr.proto.runtime.v1.Dapr/InvokeBinding"
	var request proto.Message = &runtimev1.InvokeBindingRequest{Name: "synthetic-configured-role", Operation: "fixture-send"}
	if hop == "component" {
		method = "/dapr.proto.components.v1.OutputBinding/Invoke"
		request = &components.InvokeRequest{Operation: "fixture-send"}
	}
	fields := []hpack.HeaderField{{Name: ":method", Value: "POST"}, {Name: ":scheme", Value: "http"}, {Name: ":path", Value: method}, {Name: ":authority", Value: "synthetic"}, {Name: "content-type", Value: "application/grpc"}, {Name: "te", Value: "trailers"}}
	if compressed {
		fields = append(fields, hpack.HeaderField{Name: "grpc-encoding", Value: "gzip"})
	}
	size := 0
	for _, f := range fields {
		size += len(f.Name) + len(f.Value) + 32
	}
	if headerList > 0 {
		padding := headerList - size - len("x-f1-padding") - 32
		if padding < 0 {
			panic("header target too small")
		}
		fields = append(fields, hpack.HeaderField{Name: "x-f1-padding", Value: strings.Repeat("h", padding)})
		size = headerList
	}
	var encoded bytes.Buffer
	encoder := hpack.NewEncoder(&encoded)
	for _, f := range fields {
		die(encoder.WriteField(f))
	}
	block := encoded.Bytes()
	first := true
	for len(block) > 0 {
		take := min(len(block), 16384)
		if first {
			die(fr.WriteHeaders(http2.HeadersFrameParam{StreamID: 1, BlockFragment: block[:take], EndHeaders: take == len(block)}))
			first = false
		} else {
			die(fr.WriteContinuation(1, take == len(block), block[:take]))
		}
		block = block[take:]
	}
	body, err := proto.Marshal(request)
	die(err)
	flagByte := byte(0)
	if compressed {
		var b bytes.Buffer
		writer := gzip.NewWriter(&b)
		_, err = writer.Write(body)
		die(err)
		die(writer.Close())
		body = b.Bytes()
		flagByte = 1
	}
	frame := make([]byte, len(body)+5)
	frame[0] = flagByte
	binary.BigEndian.PutUint32(frame[1:5], uint32(len(body)))
	copy(frame[5:], body)
	die(fr.WriteData(1, true, frame))
	decoder := hpack.NewDecoder(4096, nil)
	result := "Unknown"
	statusValue := ""
	var responseHeaders []byte
	for {
		f, err := fr.ReadFrame()
		if err != nil {
			if result == "Unknown" {
				result = "ConnectionClosed"
			}
			break
		}
		switch x := f.(type) {
		case *http2.SettingsFrame:
			if !x.IsAck() {
				die(fr.WriteSettingsAck())
			}
		case *http2.HeadersFrame:
			responseHeaders = append(responseHeaders, x.HeaderBlockFragment()...)
			if x.HeadersEnded() {
				decoded, err := decoder.DecodeFull(responseHeaders)
				die(err)
				responseHeaders = nil
				for _, v := range decoded {
					if v.Name == "grpc-status" {
						statusValue = v.Value
					}
				}
			}
			if x.StreamEnded() {
				result = "Trailers"
			}
		case *http2.ContinuationFrame:
			responseHeaders = append(responseHeaders, x.HeaderBlockFragment()...)
		case *http2.RSTStreamFrame:
			result = "RST_STREAM:" + x.ErrCode.String()
		case *http2.GoAwayFrame:
			result = "GOAWAY:" + x.ErrCode.String()
		}
		if result != "Unknown" {
			break
		}
	}
	record(map[string]any{"kind": "rawProbeResult", "hop": hop, "decodedListBytes": size, "compressed": compressed, "result": result, "grpcStatus": statusValue})
}

// Probe sends one attempt with grpc-go retries explicitly disabled. SDK witnesses
// use the separately compiled .NET driver, never this helper.
func probe(address, hop, operation string, n int, meta map[string]string, headers int, pbBytes int, nested bool) {
	if nested {
		meta = map[string]string{"k": "v"}
	}
	options := []grpc.DialOption{grpc.WithTransportCredentials(insecure.NewCredentials()), grpc.WithDisableRetry(), grpc.WithDefaultCallOptions(grpc.MaxCallSendMsgSize(requestCap+65536), grpc.MaxCallRecvMsgSize(responseExclusive-1))}
	if strings.HasPrefix(address, "/") {
		socketPath := address
		options = append(options, grpc.WithContextDialer(func(context.Context, string) (net.Conn, error) { return dial(socketPath) }))
		address = "passthrough:///synthetic"
	}
	conn, err := grpc.NewClient(address, options...)
	die(err)
	defer conn.Close()
	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()
	if headers > 0 {
		ctx = metadata.NewOutgoingContext(ctx, metadata.Pairs("x-f1-padding", strings.Repeat("h", headers)))
	}
	var request proto.Message
	var response proto.Message
	var method string
	if hop == "api" {
		request = &runtimev1.InvokeBindingRequest{Name: "synthetic-configured-role", Operation: operation, Data: make([]byte, n), Metadata: meta}
		response = &runtimev1.InvokeBindingResponse{}
		method = "/dapr.proto.runtime.v1.Dapr/InvokeBinding"
	} else {
		request = &components.InvokeRequest{Operation: operation, Data: make([]byte, n), Metadata: meta}
		response = &components.InvokeResponse{}
		method = "/dapr.proto.components.v1.OutputBinding/Invoke"
	}
	if pbBytes > 0 {
		missing := pbBytes - proto.Size(request)
		if missing < 5 {
			panic("protobuf padding too small")
		}
		unknown := []byte{0xFA, 0x07}
		padding := missing - 2
		var prefix [10]byte
		for {
			k := binary.PutUvarint(prefix[:], uint64(padding))
			if k+2+padding == missing {
				unknown = append(unknown, prefix[:k]...)
				unknown = append(unknown, make([]byte, padding)...)
				break
			}
			padding = missing - 2 - k
		}
		request.ProtoReflect().SetUnknown(unknown)
	}
	callOptions := []grpc.CallOption{}
	if nested {
		field := protowire.Number(2)
		if hop == "api" {
			field = 3
		}
		callOptions = append(callOptions, grpc.ForceCodec(nestedProbeCodec{mapField: field}))
	}
	err = conn.Invoke(ctx, method, request, response, callOptions...)
	record(map[string]any{"kind": "probeResult", "hop": hop, "operation": operation, "dataBytes": n, "protobufBytes": proto.Size(request), "headerPaddingBytes": headers, "dataSha256": digest(make([]byte, n)), "code": status.Code(err).String(), "responseWireBytes": proto.Size(response) + 5})
}
func descriptors(path string) {
	files := []any{}
	for _, f := range []string{"dapr/proto/components/v1/bindings.proto", "dapr/proto/components/v1/common.proto"} {
		var desc []byte
		var err error
		if strings.Contains(f, "bindings") {
			desc, err = protojson.Marshal(protodesc.ToFileDescriptorProto(components.File_dapr_proto_components_v1_bindings_proto))
		} else {
			desc, err = protojson.Marshal(protodesc.ToFileDescriptorProto(components.File_dapr_proto_components_v1_common_proto))
		}
		die(err)
		var object any
		die(json.Unmarshal(desc, &object))
		files = append(files, object)
	}
	out, err := json.MarshalIndent(files, "", "  ")
	die(err)
	die(os.WriteFile(path, out, 0600))
}
func main() {
	mode := flag.String("mode", "", "component|sink|proxy|probe|raw-probe|descriptors")
	address := flag.String("listen", "", "address or absolute socket path")
	target := flag.String("target", "", "upstream or sink")
	log := flag.String("log", "", "value-free JSONL")
	inc := flag.String("incarnation", "synthetic-1", "synthetic incarnation")
	hop := flag.String("hop", "api", "api|component")
	operation := flag.String("operation", "fixture-send", "operation")
	n := flag.Int("data-bytes", 0, "zero-filled synthetic bytes")
	headers := flag.Int("header-padding-bytes", 0, "additional decoded header value bytes")
	pb := flag.Int("protobuf-bytes", 0, "pad request with unknown field")
	listBytes := flag.Int("header-list-bytes", 0, "complete decoded header-list bytes")
	compressed := flag.Bool("compressed", false, "actual gzip frame for refusal probe")
	nested := flag.Bool("nested-unknown", false, "inject real unknown field into metadata map entry")
	metadataJSON := flag.String("metadata", "{}", "synthetic metadata JSON")
	flag.Parse()
	incarnation = *inc
	if *mode == "descriptors" {
		descriptors(*target)
		return
	}
	if *log == "" {
		panic("log required")
	}
	die(os.MkdirAll(filepath.Dir(*log), 0700))
	var err error
	logFile, err = os.OpenFile(*log, os.O_CREATE|os.O_APPEND|os.O_WRONLY, 0600)
	die(err)
	defer logFile.Close()
	record(map[string]any{"kind": "process", "mode": *mode, "pid": os.Getpid(), "requestCap": requestCap, "dataCap": dataCap, "responseExclusive": responseExclusive, "retry": false})
	switch *mode {
	case "component":
		component(*address, *target)
	case "sink":
		sink(*address)
	case "proxy":
		proxy(*address, *target, *hop)
	case "raw-probe":
		rawProbe(*target, *hop, *listBytes, *compressed)
	case "probe":
		var m map[string]string
		die(json.Unmarshal([]byte(*metadataJSON), &m))
		probe(*target, *hop, *operation, *n, m, *headers, *pb, *nested)
	default:
		fmt.Fprintln(os.Stderr, "unknown mode")
		os.Exit(2)
	}
}
