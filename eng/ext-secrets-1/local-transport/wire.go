package main

import (
	"fmt"

	components "github.com/dapr/dapr/pkg/proto/components/v1"
	"google.golang.org/protobuf/encoding/protowire"
	"google.golang.org/protobuf/proto"
	"google.golang.org/protobuf/reflect/protoreflect"
)

// Inspect bytes before protobuf map decoding discards unknown entry fields.
// This also recurses through ordinary nested messages and map values.
func rawUnknown(data []byte, descriptor protoreflect.MessageDescriptor, depth int) (unknown, nested int, err error) {
	for len(data) > 0 {
		number, kind, tag := protowire.ConsumeTag(data)
		if tag < 0 {
			return 0, 0, fmt.Errorf("invalid protobuf tag")
		}
		size := protowire.ConsumeFieldValue(number, kind, data[tag:])
		if size < 0 {
			return 0, 0, fmt.Errorf("invalid protobuf value")
		}
		field := descriptor.Fields().ByNumber(number)
		if field == nil {
			unknown += tag + size
			if depth > 0 {
				nested += tag + size
			}
		} else {
			expected := protowire.VarintType
			switch field.Kind() {
			case protoreflect.StringKind, protoreflect.BytesKind, protoreflect.MessageKind:
				expected = protowire.BytesType
			case protoreflect.Fixed32Kind, protoreflect.Sfixed32Kind, protoreflect.FloatKind:
				expected = protowire.Fixed32Type
			case protoreflect.Fixed64Kind, protoreflect.Sfixed64Kind, protoreflect.DoubleKind:
				expected = protowire.Fixed64Type
			}
			if kind != expected && !(field.IsPacked() && kind == protowire.BytesType) {
				return 0, 0, fmt.Errorf("invalid known-field wire type")
			}
			if field.Message() != nil {
				if kind != protowire.BytesType {
					return 0, 0, fmt.Errorf("invalid message wire type")
				}
				value, count := protowire.ConsumeBytes(data[tag:])
				if count < 0 {
					return 0, 0, fmt.Errorf("invalid message bytes")
				}
				u, n, e := rawUnknown(value, field.Message(), depth+1)
				if e != nil {
					return 0, 0, e
				}
				unknown += u
				nested += n
			}
		}
		data = data[tag+size:]
	}
	return unknown, nested, nil
}

func injectMapUnknown(data []byte, mapField protowire.Number) []byte {
	out := []byte{}
	for len(data) > 0 {
		number, kind, tag := protowire.ConsumeTag(data)
		size := protowire.ConsumeFieldValue(number, kind, data[tag:])
		if number == mapField && kind == protowire.BytesType {
			entry, _ := protowire.ConsumeBytes(data[tag:])
			out = protowire.AppendTag(out, number, kind)
			out = protowire.AppendBytes(out, append(append([]byte{}, entry...), 0x18, 0x01))
			return append(out, data[tag+size:]...)
		}
		out = append(out, data[:tag+size]...)
		data = data[tag+size:]
	}
	panic("nested unknown probe requires a metadata entry")
}

type closedCodec struct{}

func (closedCodec) Name() string { return "proto" }
func (closedCodec) Unmarshal(data []byte, value any) error {
	message := value.(proto.Message)
	u, n, err := rawUnknown(data, message.ProtoReflect().Descriptor(), 0)
	accepted := err == nil && u == 0
	record(map[string]any{"kind": "wireValidation", "direction": "request", "message": string(message.ProtoReflect().Descriptor().FullName()), "rawUnknownBytes": u, "nestedUnknownBytes": n, "accepted": accepted, "protobufBytes": len(data), "sha256": digest(data)})
	if !accepted {
		return fmt.Errorf("closed component request wire refused")
	}
	return proto.Unmarshal(data, message)
}
func (closedCodec) Marshal(value any) ([]byte, error) {
	message := value.(proto.Message)
	data, err := proto.Marshal(message)
	if err != nil {
		return nil, err
	}
	if response, ok := message.(*components.InvokeResponse); ok && response.Metadata["fixture-nested-response"] == "true" {
		data = injectMapUnknown(data, 2)
	}
	u, n, inspectErr := rawUnknown(data, message.ProtoReflect().Descriptor(), 0)
	accepted := inspectErr == nil && u == 0
	record(map[string]any{"kind": "wireValidation", "direction": "response", "message": string(message.ProtoReflect().Descriptor().FullName()), "rawUnknownBytes": u, "nestedUnknownBytes": n, "accepted": accepted, "protobufBytes": len(data), "sha256": digest(data)})
	if !accepted {
		return nil, fmt.Errorf("closed component response wire refused")
	}
	return data, nil
}

// Deliberately malformed client encoding for real negative transport probes.
type nestedProbeCodec struct{ mapField protowire.Number }

func (nestedProbeCodec) Name() string { return "proto" }
func (c nestedProbeCodec) Marshal(value any) ([]byte, error) {
	data, err := proto.Marshal(value.(proto.Message))
	if err != nil {
		return nil, err
	}
	return injectMapUnknown(data, c.mapField), nil
}
func (nestedProbeCodec) Unmarshal(data []byte, value any) error {
	return proto.Unmarshal(data, value.(proto.Message))
}

// Delete completed storage rather than keeping a zero-length slice backed by
// the entire preceding request on the persistent component connection.
func consumeStream(data map[uint32][]byte, stream uint32, consumed int) {
	remaining := data[stream][consumed:]
	if len(remaining) == 0 {
		delete(data, stream)
	} else {
		data[stream] = append([]byte(nil), remaining...)
	}
}
