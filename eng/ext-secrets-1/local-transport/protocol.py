"""Compare the retained closed .proto schema with imported Go descriptors."""
import json
from pathlib import Path
import re

SCALARS = {"string": "TYPE_STRING", "bytes": "TYPE_BYTES"}


def blocks(source, keyword):
    pattern = re.compile(r"\b" + keyword + r"\s+(\w+)\s*\{")
    offset = 0
    while match := pattern.search(source, offset):
        depth, end = 1, match.end()
        while depth:
            depth += (source[end] == "{") - (source[end] == "}")
            end += 1
        yield match[1], source[match.end():end - 1]
        offset = end


def verify(protocol_root, descriptor_path):
    observed = json.loads(Path(descriptor_path).read_text())
    if len(observed) != 2:
        raise ValueError("Exactly bindings/common imported descriptors required")
    results = []
    for descriptor in observed:
        source = (Path(protocol_root) / descriptor["name"]).read_text()
        source = re.sub(r"/\*.*?\*/|//[^\n]*", "", source, flags=re.S)
        package = re.search(r"\bpackage\s+([\w.]+)\s*;", source)[1]
        if descriptor["package"] != package or descriptor["syntax"] != "proto3":
            raise ValueError("Protocol package/syntax changed")
        messages = dict(blocks(source, "message"))
        if set(messages) != {m["name"] for m in descriptor.get("messageType", [])}:
            raise ValueError("Protocol message set changed")
        for message in descriptor.get("messageType", []):
            fields = re.findall(r"\b(repeated\s+)?(map\s*<\s*string\s*,\s*string\s*>|\w+)\s+(\w+)\s*=\s*(\d+)\s*;", messages[message["name"]])
            # These pinned sources contain only generated map-entry nesting.
            # Refuse additional declarations at every nested level.
            nested = message.get("nestedType", [])
            if (len(nested) != sum(kind.startswith("map") for _, kind, _, _ in fields)
                    or message.get("enumType") or message.get("oneofDecl")
                    or any(entry.get("nestedType") or entry.get("enumType") or entry.get("oneofDecl") for entry in nested)):
                raise ValueError("Protocol nested declaration set changed")
            if len(fields) != len(message.get("field", [])):
                raise ValueError("Protocol field count changed")
            for expected, field in zip(fields, message.get("field", [])):
                repeated, kind, name, number = expected
                if field["name"] != name or int(field["number"]) != int(number):
                    raise ValueError("Protocol field identity changed")
                if kind.startswith("map"):
                    entries = message.get("nestedType", [])
                    if len(entries) != 1 or entries[0].get("options", {}).get("mapEntry") is not True:
                        raise ValueError("Protocol map descriptor changed")
                    entry = entries[0]
                    if [(x["name"], x["type"], x["number"]) for x in entry["field"]] != [("key", "TYPE_STRING", 1), ("value", "TYPE_STRING", 2)]:
                        raise ValueError("Protocol map key/value changed")
                    kind_name, label = "." + package + "." + message["name"] + "." + entry["name"], "LABEL_REPEATED"
                    if field["type"] != "TYPE_MESSAGE" or field["typeName"] != kind_name:
                        raise ValueError("Protocol map field changed")
                else:
                    label = "LABEL_REPEATED" if repeated else "LABEL_OPTIONAL"
                    if kind in SCALARS:
                        if field["type"] != SCALARS[kind]:
                            raise ValueError("Protocol scalar changed")
                    elif field["type"] != "TYPE_MESSAGE" or field["typeName"] != "." + package + "." + kind:
                        raise ValueError("Protocol message field changed")
                if field["label"] != label:
                    raise ValueError("Protocol cardinality changed")
        services = dict(blocks(source, "service"))
        if set(services) != {s["name"] for s in descriptor.get("service", [])}:
            raise ValueError("Protocol service set changed")
        for service in descriptor.get("service", []):
            methods = re.findall(r"rpc\s+(\w+)\s*\(\s*(stream\s+)?(\w+)\s*\)\s*returns\s*\(\s*(stream\s+)?(\w+)\s*\)", services[service["name"]])
            if len(methods) != len(service["method"]):
                raise ValueError("Protocol service method count changed")
            for expected, method in zip(methods, service["method"]):
                name, incoming_stream, incoming, outgoing_stream, outgoing = expected
                if (method["name"] != name or method["inputType"] != "." + package + "." + incoming
                        or method["outputType"] != "." + package + "." + outgoing
                        or bool(method.get("clientStreaming")) != bool(incoming_stream)
                        or bool(method.get("serverStreaming")) != bool(outgoing_stream)):
                    raise ValueError("Protocol RPC shape changed")
        results.append({"name": descriptor["name"], "messages": len(messages), "services": len(services), "schemaEqual": True})
    return results
