import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

from audit_local_transport import FLAGS, check_authority, check_frame, check_inventory, check_probe_matrix

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
            names |= {f"response-unknown-{hop}", f"response-compression-{hop}"}
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
                "sha256": "protobuf-digest", "compressed": False, "unknownBytes": 0,
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
        for mutation in ("field-number", "scalar-type", "extra-field", "streaming", "missing-service", "extra-message"):
            descriptor = copy.deepcopy(self.descriptors()); message = descriptor[0]["messageType"][0]
            if mutation == "field-number": message["field"][0]["number"] = 2
            if mutation == "scalar-type": message["field"][0]["type"] = "TYPE_STRING"
            if mutation == "extra-field": message["field"].append({"name": "extra"})
            if mutation == "streaming": descriptor[0]["service"][0]["method"][0]["clientStreaming"] = True
            if mutation == "missing-service": descriptor[0]["service"] = []
            if mutation == "extra-message": descriptor[0]["messageType"].append({"name": "Unexpected"})
            with self.subTest(mutation=mutation), self.assertRaises(ValueError): self.check(descriptor)
