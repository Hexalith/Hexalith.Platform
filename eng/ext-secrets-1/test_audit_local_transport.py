import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

from audit_local_transport import FLAGS, check_authority, check_fault_history, check_frame, check_inventory, check_probe_matrix

_spec = importlib.util.spec_from_file_location("local_protocol", Path(__file__).parent / "local-transport/protocol.py")
_protocol = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_protocol)


class ExecutedPacketAuthorityTests(unittest.TestCase):
    def packet(self):
        return dict.fromkeys(FLAGS, False) | {"kind": "ExecutedSyntheticLocalF1Transport", "status": "MeasuredLocalTransportOnly",
            "buildRoot": "/synthetic", "assembly": "/synthetic/driver.dll", "binarySha256": {}, "configuredRetries": 0,
            "transparentDuplicatesExcluded": False, "responseCompression": "closed", "incarnations": ["component-1", "component-2"],
            "originalUncertaintyRetained": True}

    def test_synthetic_packet_confers_no_authority(self):
        check_authority(self.packet())

    def test_every_authority_flag_is_required_and_strictly_false(self):
        for flag in FLAGS:
            for value in (True, 0, None, "false"):
                with self.subTest(flag=flag, value=value), self.assertRaises(ValueError):
                    check_authority(self.packet() | {flag: value})
            packet = self.packet(); del packet[flag]
            with self.assertRaises(ValueError):
                check_authority(packet)

    def test_synthetic_packet_cannot_add_authority_alias_or_replace_history(self):
        for changed in ({"ExactlyOnce": True}, {"status": "Available"}, {"configuredRetries": 1}, {"configuredRetries": False},
                        {"transparentDuplicatesExcluded": True}, {"incarnations": ["component-2"]}, {"originalUncertaintyRetained": False}):
            with self.subTest(changed=changed), self.assertRaises(ValueError):
                check_authority(self.packet() | changed)


class ExecutedInventoryTests(unittest.TestCase):
    def test_missing_extra_or_modified_file_is_refused(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            file = root / "observation.jsonl"; file.write_text('{"bytes":1}\n')
            (root / "sha256.json").write_text(json.dumps({file.name: hashlib.sha256(file.read_bytes()).hexdigest()}))
            check_inventory(root)
            file.write_text('{"bytes":2}\n')
            with self.assertRaises(ValueError): check_inventory(root)
            file.unlink()
            with self.assertRaises(ValueError): check_inventory(root)
            file.write_text('{"bytes":1}\n'); (root / "undeclared.log").write_text("extra")
            with self.assertRaises(ValueError): check_inventory(root)

    def test_traversal_and_external_symlink_are_refused_even_with_matching_hash(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "evidence"; root.mkdir()
            outside = root.parent / "outside"; outside.write_text("synthetic")
            digest = hashlib.sha256(outside.read_bytes()).hexdigest()
            (root / "sha256.json").write_text(json.dumps({"../outside": digest}))
            with self.assertRaises(ValueError): check_inventory(root)
            (root / "link").symlink_to(outside)
            (root / "sha256.json").write_text(json.dumps({"link": digest}))
            with self.assertRaises(ValueError): check_inventory(root)


class ExecutedBoundaryMatrixTests(unittest.TestCase):
    def complete(self):
        names = {f"data-{n}" for n in (45508608, 48933548, 67108863, 67108864, 67108865)} | {"metadata-baseline"}
        for hop in ("api", "component"):
            names |= {f"protobuf-{hop}-{n}" for n in (134217727, 134217728, 134217729)}
            names |= {f"header-{hop}-{n}" for n in (65535, 65536, 65537)} | {f"compression-{hop}"}
            for field, values in (("entries", (63, 64, 65)), ("key", (256, 257)), ("value", (4096, 4097)), ("aggregate", (65535, 65536, 65537)), ("response", (1048575, 1048576, 1048577))):
                names |= {f"{field}-{hop}-{n}" for n in values}
            names |= {f"response-unknown-{hop}", f"response-compression-{hop}", f"nested-request-{hop}", f"response-nested-unknown-{hop}"}
        return [{"name": name} for name in sorted(names)]

    def test_every_boundary_is_mandatory_including_over_limit_and_each_hop(self):
        complete = self.complete(); check_probe_matrix(complete)
        for index in range(len(complete)):
            with self.subTest(missing=complete[index]["name"]), self.assertRaises(ValueError):
                check_probe_matrix(complete[:index] + complete[index + 1:])

    def test_duplicate_and_relabelled_probe_cannot_replace_missing_case(self):
        complete = self.complete()
        with self.assertRaises(ValueError): check_probe_matrix(complete + complete[:1])
        complete[0] = {"name": "unmeasured-boundary"}
        with self.assertRaises(ValueError): check_probe_matrix(complete)


class IndependentFrameTests(unittest.TestCase):
    def frame(self):
        return {"dataBytes": 100, "dataSha256": "data-digest", "protobufBytes": 150, "wireBytes": 155,
                "sha256": "protobuf-digest", "compressed": False, "unknownBytes": 0, "rawWireValid":True, "nestedUnknownBytes":0,
                "metadata": {"entries": 64, "maximumKeyUtf8Bytes": 256, "maximumValueUtf8Bytes": 4096, "aggregateUtf8Bytes": 65536}}

    def test_complete_frame_and_full_metadata_boundaries_are_inclusive(self):
        check_frame(self.frame(), 100, "data-digest", 150, "protobuf-digest")

    def test_changed_payload_frame_compression_or_unknowns_fail_closed(self):
        for change in ({"dataBytes": 99}, {"dataSha256": "other"}, {"protobufBytes": 151}, {"wireBytes": 154},
                       {"sha256": "other"}, {"compressed": True}, {"unknownBytes": 1}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                check_frame(self.frame() | change, 100, "data-digest", 150, "protobuf-digest")

    def test_each_complete_metadata_budget_rejects_one_byte_or_entry_over(self):
        for field in self.frame()["metadata"]:
            frame = self.frame(); frame["metadata"][field] += 1
            with self.subTest(field=field), self.assertRaises(ValueError):
                check_frame(frame, 100, "data-digest", 150, "protobuf-digest")


class RetainedProtocolEquivalenceTests(unittest.TestCase):
    def descriptors(self):
        return [{"name": "one.proto", "package": "synthetic", "syntax": "proto3", "messageType": [
            {"name": "Data", "field": [{"name": "data", "number": 1, "type": "TYPE_BYTES", "label": "LABEL_OPTIONAL"}]}],
            "service": [{"name": "Binding", "method": [{"name": "Invoke", "inputType": ".synthetic.Data", "outputType": ".synthetic.Data"}]}]},
            {"name": "two.proto", "package": "synthetic", "syntax": "proto3", "messageType": [{"name": "Empty"}]}]

    def check(self, descriptors):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "one.proto").write_text('syntax="proto3"; package synthetic; message Data { bytes data = 1; } service Binding { rpc Invoke(Data) returns(Data) {} }')
            (root / "two.proto").write_text('syntax="proto3"; package synthetic; message Empty {}')
            path = root / "descriptor.json"; path.write_text(json.dumps(descriptors))
            return _protocol.verify(root, path)

    def test_imported_schema_is_compared_in_both_directions(self):
        self.assertEqual(len(self.check(self.descriptors())), 2)
        for mutation in ("field-number", "scalar-type", "extra-field", "streaming", "missing-service", "extra-message", "extra-nested", "nested-map-type"):
            descriptor = copy.deepcopy(self.descriptors()); message = descriptor[0]["messageType"][0]
            if mutation == "field-number": message["field"][0]["number"] = 2
            if mutation == "scalar-type": message["field"][0]["type"] = "TYPE_STRING"
            if mutation == "extra-field": message["field"].append({"name": "extra"})
            if mutation == "streaming": descriptor[0]["service"][0]["method"][0]["clientStreaming"] = True
            if mutation == "missing-service": descriptor[0]["service"] = []
            if mutation == "extra-message": descriptor[0]["messageType"].append({"name": "Unexpected"})
            if mutation in ("extra-nested", "nested-map-type"): message["nestedType"] = [{"name":"Unexpected","options":{"mapEntry": mutation == "nested-map-type"},"nestedType":[{"name":"Deeper"}]}]
            with self.subTest(mutation=mutation), self.assertRaises(ValueError): self.check(descriptor)


class OriginalFaultHistoryTests(unittest.TestCase):
    def observations(self):
        faults, sink = [], []
        wire = {"api":[], "component":[]}
        for fault, count in (("cancellation", 1), ("late-duplicate", 2), ("lost-result", 1), ("reconnect", 1)):
            identity = "synthetic-original-" + fault
            faults += [{"Name": fault, "SdkCalls": 1, "Result": "ReceivedSyntheticUnverified" if fault == "reconnect" else "Unknown:RpcException", "CancellationUnixMilliseconds": 11},
                       {"Name": "status-" + fault, "SdkCalls": 1, "Result": "ReceivedSyntheticUnverified", "LookupAttempts": count,
                        "LookupIdentity": identity, "LookupOriginalDataSha256": "digest-" + fault, "LookupOriginalProducerIncarnation": "component-1"}]
            for number in range(1, count + 1):
                sink.append({"kind": "sink", "path": "/send", "identity": identity, "attempts": number,
                             "dataSha256": "digest-" + fault, "producerIncarnation": "component-1", "timeUnixNano": number * 10000000})
            sink.append({"kind": "sink", "path": "/status", "identity": identity, "attempts": count, "dataSha256": "digest-" + fault,
                         "producerIncarnation": "component-2" if fault == "reconnect" else "component-1", "timeUnixNano": 30000000})
        for row in faults:
            fault = row["Name"].removeprefix("status-")
            row.update(Operation="fixture-status" if row["Name"].startswith("status-") else "fixture-send", DataBytes=7, DataSha256="digest-" + fault)
            for hop in wire:
                wire[hop].append(IndependentFrameTests().frame() | {"kind":"grpcFrame", "direction":"request", "syntheticCase":row["Name"],"operation":row["Operation"],"dataBytes":7,"dataSha256":row["DataSha256"]})
        for row in sink: row["dataBytes"] = 7
        return faults, sink, wire

    def test_original_delivery_and_status_keep_uncertainty_and_old_incarnation(self):
        check_fault_history(*self.observations())

    def test_sdk_original_cannot_borrow_sink_digest_or_another_wire_case(self):
        for change in ("bytes", "digest", "operation", "wire-case"):
            faults,sink,wire=self.observations()
            if change=="bytes":faults[0]["DataBytes"]=0
            if change=="digest":faults[0]["DataSha256"]="another-original"
            if change=="operation":faults[0]["Operation"]="fixture-status"
            if change=="wire-case":wire["component"][0]["syntheticCase"]="another-original"
            with self.subTest(change=change),self.assertRaises(ValueError):check_fault_history(faults,sink,wire)

    def test_missing_duplicate_or_changed_attempt_cannot_be_claimed_closed(self):
        original_faults, original_sink, original_wire = self.observations()
        for mutation in ("missing-original", "blind-resend", "changed-lookup-digest", "changed-status-count", "failed-lookup", "extra-sdk-send", "new-original-identity", "dropped-old-incarnation", "early-duplicate", "false-sequence"):
            faults, sink = copy.deepcopy(original_faults), copy.deepcopy(original_sink)
            if mutation == "missing-original": sink.pop(0)
            if mutation == "blind-resend": sink.append(copy.deepcopy(sink[0]))
            if mutation == "changed-lookup-digest": sink[1]["dataSha256"] = "other"
            if mutation == "changed-status-count": faults[1]["LookupAttempts"] = 2
            if mutation == "failed-lookup": faults[1]["Result"] = "Unknown:RpcException"
            if mutation == "extra-sdk-send": faults[0]["SdkCalls"] = 2
            if mutation == "new-original-identity": faults[1]["LookupIdentity"] = "replacement"
            if mutation == "dropped-old-incarnation": sink[-2]["producerIncarnation"] = "component-2"
            if mutation == "early-duplicate": sink[3]["timeUnixNano"] = 10000000
            if mutation == "false-sequence": sink[3]["attempts"] = 1
            with self.subTest(mutation=mutation), self.assertRaises(ValueError): check_fault_history(faults, sink, original_wire)

# Keep these offline: test the producer/consumer contracts using local bytes.
import os
import subprocess
import sys
import audit_local_transport as audit


def local_module(name):
    directory = Path(__file__).parent / "local-transport"
    sys.path.insert(0, str(directory))
    try:
        spec = importlib.util.spec_from_file_location("f1_test_" + name, directory / (name + ".py"))
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        return module
    finally:
        sys.path.pop(0)


class BuildProducerConsumerTests(unittest.TestCase):
    def test_source_record_written_by_builder_is_exact_auditor_input_without_downloads(self):
        builder = local_module("build")
        with tempfile.TemporaryDirectory() as directory:
            (Path(directory) / "source").mkdir()
            record = builder.emit_source_inputs(directory, builder.DAPR_ARCHIVE_SHA256, builder.GO_ARCHIVE_SHA256,
                                                builder.PATCH_SHA256, builder.PATCHED_SOURCE_SHA256)
            self.assertEqual(record, audit.read(Path(directory) / "source/source-inputs.json"))
            audit.check_source_inputs(record)
            for key in record:
                changed = record | {key:"unretained"}
                with self.subTest(key=key), self.assertRaises(ValueError): audit.check_source_inputs(changed)
            with self.assertRaises(ValueError): builder.emit_source_inputs(directory, "bad", builder.GO_ARCHIVE_SHA256, builder.PATCH_SHA256, builder.PATCHED_SOURCE_SHA256)

    def test_go_overrides_and_checksum_exclusions_are_isolated_and_audited(self):
        builder = local_module("build")
        inherited = {"PATH":os.environ["PATH"],"GOFLAGS":"-overlay=evil.json -modfile=evil.mod", "GOWORK":"evil.work", "GOENV":"evil-env",
                     "GONOSUMDB":"*", "GOPRIVATE":"*", "GOINSECURE":"*", "GOROOT":"evil", "CGO_CFLAGS":"evil"}
        with tempfile.TemporaryDirectory() as directory:
            environment = builder.isolated_go_environment(directory, inherited)
            self.assertNotIn("GOROOT", environment); self.assertNotIn("CGO_CFLAGS", environment)
            self.assertEqual(environment["GOENV"], "off")
            # go env -json normalizes the disabled environment file to no path.
            record = {"required":builder.GO_SETTINGS,"effective":environment | {"GOENV":""}}
            audit.check_go_environment(record, directory)
            for path in ("off", "/home/example/.config/go/env"):
                with self.subTest(goenv=path), self.assertRaises(ValueError):
                    audit.check_go_environment({"required":builder.GO_SETTINGS,"effective":environment | {"GOENV":path}}, directory)
            for key in builder.GO_SETTINGS:
                changed = copy.deepcopy(record); changed["effective"][key] = "inherited-override"
                with self.subTest(key=key), self.assertRaises(ValueError): audit.check_go_environment(changed, directory)

    def test_full_app_local_sdk_closure_includes_runtime_eventstore_grpc_and_runtimeconfig(self):
        sealer = local_module("seal_build")
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ("driver.dll", "driver.deps.json", "driver.runtimeconfig.json", "Hexalith.Platform.Secrets.Runtime.dll", "Hexalith.EventStore.Server.dll", "Grpc.Net.Client.dll"):
                (root / name).write_text(name)
            sealed = sealer.sdk_dependency_closure(root / "driver.dll")
            audit.check_sdk_closure(root / "driver.dll", sealed)
            for path in list(sealed):
                changed = sealed.copy(); del changed[path]
                with self.subTest(path=path), self.assertRaises(ValueError): audit.check_sdk_closure(root / "driver.dll", changed)
            (root / "driver.runtimeconfig.json").write_text("changed")
            with self.assertRaises(ValueError): audit.check_sdk_closure(root / "driver.dll", sealed)
            (root / "new-dependency.dll").write_text("new")
            with self.assertRaises(ValueError): audit.check_sdk_closure(root / "driver.dll", sealed)

    def test_sealed_retained_and_executed_binary_hashes_are_joined(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); (root / "bin").mkdir()
            for name in ("daprd-stable","f1fixture"): (root/"bin"/name).write_text(name)
            sealed={"binaries":{name:audit.sha(root/"bin"/name) for name in ("daprd-stable","f1fixture")}}
            execution={"binarySha256":{"daprd":sealed["binaries"]["daprd-stable"],"fixture":sealed["binaries"]["f1fixture"]}}
            audit.check_binary_bindings(root,execution,sealed)
            for name,label in (("daprd-stable","daprd"),("f1fixture","fixture")):
                changed=copy.deepcopy(sealed);changed["binaries"][name]="changed"
                with self.assertRaises(ValueError):audit.check_binary_bindings(root,execution,changed)
                changed=copy.deepcopy(execution);changed["binarySha256"][label]="changed"
                with self.assertRaises(ValueError):audit.check_binary_bindings(root,changed,sealed)

    def test_short_lived_executable_is_bound_before_exit(self):
        runner=local_module("run")
        with open(os.devnull,"wb") as stream:
            for argv in [["/usr/bin/true"]] * 12 + [[audit.shutil.which("dotnet"), "--version"]]:
                process,binding=runner.launch_bound(argv,os.environ.copy(),Path.cwd(),stream)
                self.assertEqual(process.wait(timeout=5),0)
                self.assertEqual(binding["executionImageSha256"],audit.sha(argv[0]))
                self.assertEqual(binding["imageBinding"],"opened-executable-fd")
                self.assertTrue(binding["startTicks"])
                terminal=runner.terminal_binding(process,binding)
                self.assertEqual(terminal["terminalImageSha256"],binding["executionImageSha256"])

    def test_fixture_seal_rejects_extra_compiled_source_and_module_replacements(self):
        sealer=local_module("seal_build")
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);local=root/"reviewed";fixture=root/"fixture"
            local.mkdir();fixture.mkdir()
            for name,value in {"main.go":"package main", "go.sum":"pinned", "go.mod":"module example\n\ngo 1.26.4\n"}.items():
                (local/name).write_text(value);(fixture/name).write_text(value)
            mod=(local/"go.mod").read_text().rstrip()+"\n\nreplace github.com/dapr/dapr => ../dapr-"+sealer.PIN+"\n"
            (fixture/"go.mod").write_text(mod);sealer.fixture_closure(fixture,local)
            (fixture/"extra.go").write_text("package main\nfunc init() {}")
            with self.assertRaises(ValueError):sealer.fixture_closure(fixture,local)
            (fixture/"extra.go").unlink()
            (fixture/"extra.syso").write_bytes(b"unreviewed linker input")
            with self.assertRaises(ValueError):sealer.fixture_closure(fixture,local)
            (fixture/"extra.syso").unlink()
            (fixture/"go.mod").write_text(mod+"replace google.golang.org/grpc => /unreviewed\n")
            with self.assertRaises(ValueError):sealer.fixture_closure(fixture,local)


class ProbeEnforcementTests(unittest.TestCase):
    def metadata(self, hop="component", size=64):
        digest=hashlib.sha256(b"").hexdigest()
        summary={"entries":size,"maximumKeyUtf8Bytes":1,"maximumValueUtf8Bytes":1,"aggregateUtf8Bytes":size*2}
        result={"kind":"probeResult","code":"OK","dataBytes":0,"dataSha256":digest,"timeUnixNano":5}
        probe={"name":f"entries-{hop}-{size}","hop":hop,"result":result}
        observations=[{"timeUnixNano":1},result]
        component=[{"kind":"componentAdmission","timeUnixNano":3,"dataBytes":0,"dataSha256":digest,"metadata":summary}]
        sink=[{"kind":"sink","path":"/send","timeUnixNano":4,"dataBytes":0,"dataSha256":digest}]
        return probe,observations,component,sink,{"api":[],"component":[]}

    def test_each_accepted_metadata_probe_requires_its_own_correct_sink_arrival(self):
        for hop in ("api","component"):
            args=self.metadata(hop);audit.check_probe_observations(*args)
            for change in ("missing","different-digest","outside-invocation","duplicate"):
                modified=copy.deepcopy(args)
                if change=="missing":modified[3].clear()
                if change=="different-digest":modified[3][0]["dataSha256"]="other"
                if change=="outside-invocation":modified[3][0]["timeUnixNano"]=6
                if change=="duplicate":modified[3].append(copy.deepcopy(modified[3][0]))
                with self.subTest(hop=hop,change=change),self.assertRaises(ValueError):audit.check_probe_observations(*modified)

    def test_negative_metadata_requires_expected_status_and_metadata_guard(self):
        args=list(self.metadata(size=65));args[0]["result"]["code"]="ResourceExhausted";args[3]=[]
        args[2].append({"kind":"componentRefusal","stage":"metadata","timeUnixNano":4})
        audit.check_probe_observations(*args)
        for change in ("wrong-status","wrong-boundary","sink-arrival"):
            modified=copy.deepcopy(args)
            if change=="wrong-status":modified[0]["result"]["code"]="Unavailable"
            if change=="wrong-boundary":modified[2][-1]["stage"]="data"
            if change=="sink-arrival":modified[3]=self.metadata()[3]
            with self.subTest(change=change),self.assertRaises(ValueError):audit.check_probe_observations(*modified)

    def test_over_limit_headers_require_actual_rst_frame_size_error(self):
        result={"timeUnixNano":5,"result":"RST_STREAM:FRAME_SIZE_ERROR","grpcStatus":""}
        probe={"name":"header-api-65537","hop":"api","result":result};obs=[{"timeUnixNano":1},result]
        wire={"api":[{"kind":"decodedHeaders","direction":"request","timeUnixNano":2,"decodedListBytes":65537},
                     {"kind":"streamReset","direction":"response","timeUnixNano":4,"code":"FRAME_SIZE_ERROR"}],"component":[]}
        audit.check_probe_observations(probe,obs,[],[],wire)
        for change in ("disconnect","wrong-reset","no-reset"):
            p,o,w=copy.deepcopy((probe,obs,wire))
            if change=="disconnect":p["result"]["result"]="connection-closed"
            if change=="wrong-reset":w["api"][-1]["code"]="CANCEL"
            if change=="no-reset":w["api"].pop()
            with self.subTest(change=change),self.assertRaises(ValueError):audit.check_probe_observations(p,o,[],[],w)

    def test_sdk_case_cannot_borrow_later_go_frames_or_sink_with_same_digest(self):
        n=45508608;case=f"data-{n}";digest="same-data"
        frame=IndependentFrameTests().frame() | {"dataBytes":n,"dataSha256":digest,"syntheticCase":case,"kind":"grpcFrame","direction":"request"}
        wire={hop:[copy.deepcopy(frame)] for hop in ("api","component")}
        sdk=[{"Name":case,"SdkCalls":1,"DataBytes":n,"Result":"ReceivedSyntheticUnverified","DataSha256":digest}]
        component=[{"kind":"componentAdmission","syntheticCase":case,"dataBytes":n,"dataSha256":digest}]
        sink=[{"kind":"sink","path":"/send","syntheticCase":case,"dataBytes":n,"dataSha256":digest}]
        audit.check_sdk_data_delivery(n,sdk,wire,component,sink)
        for target in (wire["component"][0],component[0],sink[0]):
            target["syntheticCase"]="later-go-probe"
            with self.assertRaises(ValueError):audit.check_sdk_data_delivery(n,sdk,wire,component,sink)
            target["syntheticCase"]=case

    def test_boundary_name_cannot_substitute_for_independent_measured_size(self):
        for name in ("data-67108864", "protobuf-api-134217728", "protobuf-component-134217728"):
            args=list(self.metadata());args[0]["name"]=name
            args[0]["hop"]="component" if "component" in name else "api"
            with self.subTest(name=name),self.assertRaises(ValueError):audit.check_probe_observations(*args)


class ProcessInventoryTests(unittest.TestCase):
    def inventory(self, root):
        names={"descriptors":"descriptors","sink":"sink","component-1":"component","component-2":"component","component-observer":"proxy","api-observer":"proxy","daprd":None,
               "sdk-witnesses":None,"sdk-faults":None,"sdk-reconnect-lookup":None}
        commands=[];cleanup=[]
        for index,(name,mode) in enumerate(names.items(),1):
            image=root/"bin"/("daprd-stable" if name=="daprd" else "f1fixture")
            argv=[str(image),"--mode",mode or "test"]
            if name.startswith("sdk-"):image=Path(audit.shutil.which("dotnet"));argv=[str(image),str(root/"driver.dll")]
            record={"name":name,"argv":argv,"pid":index,"startTicks":str(index),"executionImageSha256":audit.sha(image),"imageBinding":"opened-executable-fd",
                    "executableFile":str(image),"executableDevice":image.stat().st_dev,"executableInode":image.stat().st_ino,"sdkDriverSha256":"driver-hash"}
            commands.append(record)
            cleanup.append(record | {"stopped":True,"terminalObserved":True,"returncode":0,"terminalPid":index,"terminalStartTicks":str(index),
                "terminalImageSha256":record["executionImageSha256"],"terminalExecutableDevice":record["executableDevice"],"terminalExecutableInode":record["executableInode"],
                "terminalObservation":"waitpid-owned-child-and-opened-image-fd"})
        execution={"assembly":str(root/"driver.dll"),"binarySha256":{"daprd":audit.sha(root/"bin/daprd-stable"),"fixture":audit.sha(root/"bin/f1fixture"),"sdkDriver":"driver-hash"}}
        return commands,cleanup,[],execution,root

    def test_complete_inventory_and_matching_terminal_identity_are_mandatory(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);(root/"bin").mkdir()
            for name in ("daprd-stable","f1fixture"):(root/"bin"/name).write_text(name)
            args=self.inventory(root);audit.check_process_inventory(*args)
            for change in ("empty","missing","pid","startTicks","image","not-terminal","terminal-image","wrong-named-image","unknown-mode"):
                modified=copy.deepcopy(args)
                if change=="empty":modified[0].clear();modified[1].clear()
                if change=="missing":modified[1].pop()
                if change=="pid":modified[1][0]["pid"]=999
                if change=="startTicks":modified[1][0]["startTicks"]="other"
                if change=="image":modified[1][0]["executionImageSha256"]="other"
                if change=="not-terminal":modified[1][0]["terminalObserved"]=False
                if change=="terminal-image":modified[1][0]["terminalImageSha256"]="changed"
                if change=="wrong-named-image":modified[0][6]["argv"][0]=str(root/"bin/f1fixture")
                if change=="unknown-mode":modified[0][0]["argv"][-1]="sink"
                with self.subTest(change=change),self.assertRaises(ValueError):audit.check_process_inventory(*modified)

class PublishedPacketChecksTests(unittest.TestCase):
    def packet(self):
        return audit.read(Path(__file__).resolve().parents[2] / "docs/implementation/ext-secrets-1-f1-2026-10-20/local-transport.json")

    def test_exact_published_configuration_headers_metadata_and_derived_bounds(self):
        packet=self.packet()
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);(root/"resources").mkdir()
            for name in ("configuration.yaml","isolation.json","resources/binding.yaml","resources/resiliency.yaml"):
                (root/name).write_text(name)
            packet["configurationSha256"]={name:audit.sha(root/name) for name in ("configuration.yaml","isolation.json","resources/binding.yaml","resources/resiliency.yaml")}
            (root/"probes.json").write_text(json.dumps([{"name":"metadata-baseline","log":"baseline.jsonl"}]))
            (root/"baseline.jsonl").write_text('{"timeUnixNano":1}\n{"timeUnixNano":5}\n')
            (root/"component.jsonl").write_text(json.dumps({"kind":"componentAdmission","timeUnixNano":3,"metadata":packet["forwardedMetadata"]["goApiBaseline"]})+'\n')
            audit.check_published_limits(packet,root)
            for change in ("empty-config","missing-config","header-one","metadata-one","manifest-one","transition-one","api-one","drop-assumption"):
                changed=copy.deepcopy(packet)
                if change=="empty-config":changed["configurationSha256"]={}
                if change=="missing-config":changed["configurationSha256"].pop("isolation.json")
                if change=="header-one":changed["headers"]["advertisedLimitBytes"]=1
                if change=="metadata-one":changed["forwardedMetadata"]["maximumEntries"]=1
                if change=="manifest-one":changed["sufficiency"]["manifestDataUpperBoundBytes"]=1
                if change=="transition-one":changed["sufficiency"]["transitionDataUpperBoundBytes"]=1
                if change=="api-one":changed["sufficiency"]["apiProtobufLooseUpperBoundAtDataCap"]=1
                if change=="drop-assumption":changed["sufficiency"]["assumptions"]=[]
                with self.subTest(change=change),self.assertRaises(ValueError):audit.check_published_limits(changed,root)

    def test_template_cannot_drop_null_inputs_or_change_runtime_fixture_binding(self):
        packet=self.packet()
        template=audit.read(Path(__file__).resolve().parents[2] / "docs/implementation/ext-secrets-1-f1-2026-10-20/next-qualification.template.json")
        packet_hash=template["localTransport"]["packetSha256"];manifest_hash=template["localTransport"]["evidenceManifestSha256"]
        audit.check_template(template,packet,packet_hash,manifest_hash)
        for section,keys in audit.TEMPLATE_INPUTS.items():
            for key in keys:
                changed=copy.deepcopy(template);del changed[section][key]
                with self.subTest(section=section,key=key),self.assertRaises(ValueError):audit.check_template(changed,packet,packet_hash,manifest_hash)
        for key in ("fixtureArtifactSha256","runtimeArtifactSha256","evidenceManifestSha256","packetSha256"):
            changed=copy.deepcopy(template);changed["localTransport"][key]="different"
            with self.assertRaises(ValueError):audit.check_template(changed,packet,packet_hash,manifest_hash)

    def independent(self, root):
        (root/"bin").mkdir();(root/"bin/daprd-stable").write_text("runtime")
        independent=root/"independent";source=independent/"source-and-header";runtime=independent/"runtime-headers"
        source.mkdir(parents=True);runtime.mkdir()
        packet=self.packet();packet["build"]["binaries"]["daprd-stable"]=audit.sha(root/"bin/daprd-stable")
        packet["evidence"]["independentRoot"]=str(independent)
        def write(path,data):path.write_text(json.dumps(data))
        write(source/"verification.json",{"kind":"IndependentDisposableHeaderPatchCheck","sourceCommit":packet["build"]["daprCommit"],
              "patchSha256":"c19d126445132b1b08894a98f75a653f8d1f6ce53a9386fd4d3d8743d28efbfd",
              "resultSha256":"41d6010a43956eda01ae0d5c4bc94daa95e61e8523f5031c417e81cd1879be4a","parsedReadBufferBytes":65536,"patchedHeaderListBytes":65536,"productionArtifactQualified":False})
        tree={"pkg/api/grpc/server.go":"41d6010a43956eda01ae0d5c4bc94daa95e61e8523f5031c417e81cd1879be4a"}
        write(source/"candidate-source-tree-sha256.json",tree);write(root/"runtime-source-tree-sha256.json",tree)
        write(source/"candidate-source-archive-verification.json",{"filesCompared":4185,"sourceArchiveSha256":packet["build"]["sourceArchiveSha256"],
              "modifiedFiles":[{"path":"pkg/api/grpc/server.go","base":"1b4f56b441e92822a0b42c63d54b4450cfe45a94fe2a4736ad14ff54dc7dfbe3","candidate":tree["pkg/api/grpc/server.go"]}]})
        write(source/"retained-protocol-source-comparison.json",packet["build"]["retainedProtocols"])
        write(source/"go-toolchain-hash-verification.json",{"kind":"IndependentGoToolchainModuleHash","module":"golang.org/toolchain","version":"v0.0.1-go1.26.4.linux-amd64",
              "archiveSha256":"0221cfe82f9c88c717677cb5c1f59f598d177cd7eac18c11f90ae29432d356f9","moduleContentHash":"h1:wAoHYF9gHXZdyCyZT4yuWhbjdIC/Iv5dpQsE+H3FgGU=","publicSumdbLookupMatched":True,"sumdbSignatureIndependentlyVerified":False})
        write(source/"stable-candidate-binary-verification.json",{"sha256":packet["build"]["binaries"]["daprd-stable"],"bytes":7,"goVersion":"go1.26.4","stablecomponents":True,"grpcGo1_80_0":True,"componentsContrib1_18_0":True})
        (runtime/"config.yaml").write_text("synthetic")
        write(runtime/"cleanup.json",{"stopped":True,"returncode":0})
        header=source/"independent-runtime-header-boundaries.json"
        write(header,{"kind":"IndependentRawHpackPatchedDaprHeaderBoundary","binarySha256":packet["build"]["binaries"]["daprd-stable"],"CompletionPermission":False,"configurationSha256":audit.sha(runtime/"config.yaml"),
              "observations":[{"decodedHeaderListBytes":n,"advertisedHeaderListBytes":[65536],"headerTransportAccepted":n<=65536,"frames":[] if n<=65536 else [{"type":3,"rstCode":6}]} for n in (65535,65536,65537)]})
        packet["headers"]["independentDifferentImplementationReport"]=str(header)
        write(source/"bound-arithmetic.json",{"manifestDataUpperBound":45508608,"transitionDataUpperBound":48933548,"dataCap":67108864,"completeApiProtobufLooseUpperBoundAtDataCap":67181835,"completeComponentProtobufLooseUpperBoundAtDataCap":67175688,"protobufCap":134217728,"finiteWitnessIsExhaustive":False,"semanticOrCompletionAuthority":False})
        self.reseal_independent(root,packet)
        return packet

    def reseal_independent(self,root,packet):
        independent=root/"independent"
        (root/"independent-sha256.json").write_text(json.dumps({str(p.relative_to(independent)):audit.sha(p) for p in independent.rglob('*') if p.is_file()}))
        packet["evidence"]["independentManifestSha256"]=audit.sha(root/"independent-sha256.json")

    def test_independent_reports_require_retained_hashes_artifacts_results_and_false_authority(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);packet=self.independent(root);audit.check_independent_reports(packet,root)
            header=Path(packet["headers"]["independentDifferentImplementationReport"]);original=header.read_text()
            for change in ("unsealed-tamper","artifact","authority","result","rst"):
                header.write_text(original);data=json.loads(original)
                if change=="unsealed-tamper":data["binarySha256"]="changed"
                if change=="artifact":data["binarySha256"]="changed"
                if change=="authority":data["CompletionPermission"]=True
                if change=="result":data["observations"][-1]["headerTransportAccepted"]=True
                if change=="rst":data["observations"][-1]["frames"]=[]
                header.write_text(json.dumps(data))
                if change!="unsealed-tamper":self.reseal_independent(root,packet)
                with self.subTest(change=change),self.assertRaises(ValueError):audit.check_independent_reports(packet,root)
                header.write_text(original);self.reseal_independent(root,packet)

    def detached(self,root):
        output=root/"verification";output.mkdir()
        assembly=root/"artifacts/bin/Hexalith.Platform.Secrets.Runtime.Tests/debug/Hexalith.Platform.Secrets.Runtime.Tests.dll"
        assembly.parent.mkdir(parents=True);assembly.write_text("detached fixture")
        packet=self.packet();packet["build"]["dotnetSourceDependencies"]={}
        def write(name,data):(output/name).write_text(json.dumps(data))
        write("commands.json",[["dotnet","build","project","--artifacts-path",str(root/"artifacts")],["dotnet",str(assembly)]])
        write("f1-packet-check.json",{"CompletionPermission":False,"DependencyAvailable":False,"KeyDeliveryEnabled":False,"LiveReady":False,"installedPathProved":False,"physicalCompletionProved":False,"terminationPacketConforms":True,"transportPacketConforms":True})
        fixture={"Kind":"SyntheticF1SourceContractOnly","DaprClient":"1.18.10","DependencyAvailable":False,"LiveReady":False,"QualifiedComponentInstalled":False,"PhysicalEvidence":False,"RealSidecarPathExecuted":False,"NativeEffects":0}
        import verify_f1
        fixture["CandidateSdkCaptures"] = {name: {"DataBytes":a,"DataSha256":b,"ApiProtobufBytes":c,"ApiProtobufSha256":d,"GrpcFrameBytes":c+5,"SdkCalls":1,"MaxSendMessageBytes":134217728,"MaxReceiveMessageBytes":1048575,"RealSidecarPathExecuted":False} for name,(a,b,c,d) in verify_f1.CANDIDATE_CAPTURES.items()}
        fixture["CandidateSdkCaptures"].update(SidecarOrComponentExecuted=False,ProductionInvokerProfileChanged=False,DecodeCapBytes=67108864,FullManifestRequestBoundBytes=45508608,FullTransitionRequestBoundBytes=48933548,ComponentForwardedMetadataWireBudgetBytes=66560)
        write("custody-binding-f1.json",fixture)
        import xml.etree.ElementTree as ET
        xml=ET.Element("assemblies");assembly_xml=ET.SubElement(xml,"assembly",total="238",passed="238")
        cases=[(cls,method) for cls,methods in verify_f1.BEHAVIORAL_COVERAGE.items() for method in methods]
        cases.append(("Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingFeasibilityTests","EmitDeterministicF1Evidence"))
        cases += [("Synthetic.CustodyBindingCoverage",f"F1{i}") for i in range(101-len(cases))]
        cases += [("Synthetic.Other",f"Other{i}") for i in range(238-len(cases))]
        for cls,method in cases:ET.SubElement(assembly_xml,"test",type=cls,method=method,result="Pass")
        ET.ElementTree(xml).write(output/"runtime-tests.xml")
        (output/"runtime-build.log").write_text("0 Warning(s)\n0 Error(s)\n")
        for name in ("runtime-tests.log","owner-capture-source.json"):(output/name).write_text(name)
        repo=Path(__file__).resolve().parents[2]
        source_names={str(p.relative_to(repo)) for p in (repo/"eng/ext-secrets-1/local-transport").rglob("*") if p.is_file() and "__pycache__" not in p.parts}
        source_names |= {str(p.relative_to(repo)) for p in (repo/"eng/ext-secrets-1").glob("*.py")}
        source_names |= {"eng/ext-secrets-1/audit_local_transport.py","eng/ext-secrets-1/test_audit_local_transport.py","eng/ext-secrets-1/verify_f1.py","eng/ext-secrets-1/check_f1_packets.py","tests/Hexalith.Platform.Secrets.Runtime.Tests/CustodyBindingLocalTransport.cs","tests/Hexalith.Platform.Secrets.Runtime.Tests/CustodyBindingFeasibilityTests.cs"}
        write("source-sha256.json",{name:audit.sha(repo/name) for name in source_names})
        write("verification.json",{"kind":"SyntheticF1SourceContractOnly","candidateBaseCommit":packet["candidateSourceHead"],"counts":{"behavioralMethodsCovered":29,"f1Passed":101,"passed":238,"total":238},"dependencyAvailable":False,"runtimeQualificationCompleted":False,"qualificationEffects":0,"sourceInputs":{},"sha256":{p.name:audit.sha(p) for p in output.iterdir()}})
        log=root/"python.log";inventory=audit.python_test_inventory(repo)
        log.write_text("command: python3 -m unittest discover -s eng/ext-secrets-1 -p test_*.py -v\n" + "\n".join(test + " ... ok" for test in inventory) + f"\nRan {len(inventory)} tests in 1.0s\n\nOK\nexitCode: 0\n")
        verification={"authority":"Source contract only","f1Artifacts":str(root/"artifacts"),"f1Directory":str(output),"pythonSuiteLog":str(log),"requiredRuntimeTests":238,
                      "f1ReportSha256":audit.sha(output/"verification.json"),"pythonSuiteSha256":audit.sha(log),"f1AssemblySha256":audit.sha(assembly)}
        return verification,packet

    def test_detached_reports_and_executables_require_hashes_passing_results_false_authority(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);verification,packet=self.detached(root);audit.check_detached_verification(verification,packet,root)
            report=Path(verification["f1Directory"])/"verification.json";original=report.read_text()
            for change in ("unsealed-tamper","failed","authority","source-identity","assembly"):
                data=json.loads(original)
                if change=="unsealed-tamper":data["counts"]["passed"]=0
                if change=="failed":data["counts"]["passed"]=237
                if change=="authority":data["dependencyAvailable"]=True
                if change=="source-identity":data["candidateBaseCommit"]="different"
                if change=="assembly":verification["f1AssemblySha256"]="different"
                report.write_text(json.dumps(data))
                if change!="unsealed-tamper":verification["f1ReportSha256"]=audit.sha(report)
                with self.subTest(change=change),self.assertRaises(ValueError):audit.check_detached_verification(verification,packet,root)
                report.write_text(original);verification["f1ReportSha256"]=audit.sha(report)

    def test_hashed_invalid_xml_and_focused_python_log_cannot_claim_full_verification(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);verification,packet=self.detached(root)
            output=Path(verification["f1Directory"]);report=output/"verification.json"
            xml=output/"runtime-tests.xml";original=xml.read_text();xml.write_text("runtime-tests.xml")
            data=json.loads(report.read_text());data["sha256"]["runtime-tests.xml"]=audit.sha(xml)
            report.write_text(json.dumps(data));verification["f1ReportSha256"]=audit.sha(report)
            with self.assertRaises(ValueError):audit.check_detached_verification(verification,packet,root)
            xml.write_text(original);data["sha256"]["runtime-tests.xml"]=audit.sha(xml)
            report.write_text(json.dumps(data));verification["f1ReportSha256"]=audit.sha(report)
            log=Path(verification["pythonSuiteLog"]);log.write_text("Ran focused tests\n\nOK\n")
            verification["pythonSuiteSha256"]=audit.sha(log)
            with self.assertRaises(ValueError):audit.check_detached_verification(verification,packet,root)
