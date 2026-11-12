"""Finite shared release format and original public handoff refusal controls."""
import copy
import hashlib
import unittest

from builds_publication import verify_handoff
from builds_tuple import load_binding, validate_binding


class SharedPublicationTests(unittest.TestCase):
    def setUp(self):
        self.binding = copy.deepcopy(load_binding(require_publication=False))
        publication = self.binding["publication"]
        source = self.binding["producer"]["sourceRevision"]
        self.body = (f"hexalith-release: {source}\nhexalith-release-run: 12\n"
                     f"hexalith-release-artifact: 13 sha256:{'a' * 64}\n"
                     "hexalith-release-packages: Hexalith.Builds.Evidence.Cli,Hexalith.Builds.Module.Cli\n"
                     "hexalith-release-containers: \n")
        self.binding["publication"] = {key: publication[key] for key in
            ("version", "sourceRevision", "releaseTag", "catalogSha256", "archiveSha256", "toolingSha256")}
        self.binding["publication"].update(kind="shared-release-handoff/v1", releaseId=11, runId=12, artifactId=13,
            artifactSha256="a" * 64, releaseBodySha256=hashlib.sha256(self.body.encode()).hexdigest())
        self.responses = {
            "releases/11": {"id": 11, "tag_name": publication["releaseTag"], "draft": False,
                            "prerelease": False, "published_at": "2026-11-12T09:13:04Z", "body": self.body},
            "git/ref/tags/" + publication["releaseTag"]: {"object": {"type": "commit", "sha": source}},
            "actions/runs/12": {"id": 12, "head_sha": source, "status": "completed", "conclusion": "success",
                                "path": ".github/workflows/builds-release.yml"},
            "actions/artifacts/13": {"id": 13, "name": "release", "digest": "sha256:" + "a" * 64,
                                     "workflow_run": {"id": 12, "head_sha": source}}}

    def verify(self):
        validate_binding(self.binding)
        return verify_handoff(self.binding, read=lambda path: self.responses[path])

    def test_shared_publication_is_identity_evidence_without_legacy_native_credit(self):
        receipt = self.verify()
        self.assertFalse(receipt["qualificationCredit"])
        self.assertFalse(receipt["legacyG4AcceptanceClaimed"])
        self.assertNotIn("inventoryUrl", self.binding["publication"])

    def test_unknown_missing_mixed_or_non_numeric_publication_fields_refuse(self):
        for key, value in (("kind", "other/v1"), ("inventoryUrl", "invented"), ("releaseId", True),
                           ("runId", "12"), ("artifactId", -1), ("artifactSha256", "x")):
            with self.subTest(key=key):
                previous = copy.deepcopy(self.binding)
                self.binding["publication"][key] = value
                with self.assertRaises(ValueError):
                    self.verify()
                self.binding = previous
        for key in tuple(self.binding["publication"]):
            previous = self.binding["publication"].pop(key)
            with self.subTest(missing=key), self.assertRaises(ValueError):
                self.verify()
            self.binding["publication"][key] = previous

    def test_changed_release_tag_body_run_and_artifact_refuse(self):
        for path, key, value in (("releases/11", "id", 99), ("releases/11", "draft", True),
                ("releases/11", "prerelease", True), ("releases/11", "body", self.body + "changed"),
                ("actions/runs/12", "head_sha", "b" * 40), ("actions/runs/12", "conclusion", "failure"),
                ("actions/runs/12", "path", ".github/workflows/other.yml"),
                ("actions/artifacts/13", "digest", "sha256:" + "b" * 64),
                ("actions/artifacts/13", "workflow_run", {"id": 99, "head_sha": "b" * 40})):
            with self.subTest(path=path, key=key):
                previous = self.responses[path][key]
                self.responses[path][key] = value
                with self.assertRaises(ValueError):
                    self.verify()
                self.responses[path][key] = previous
        path = "git/ref/tags/" + self.binding["publication"]["releaseTag"]
        self.responses[path]["object"]["sha"] = "b" * 40
        with self.assertRaisesRegex(ValueError, "tag differs"):
            self.verify()

    def test_rehashed_missing_duplicate_or_wrong_markers_still_refuse(self):
        for body in (self.body.replace("hexalith-release-run: 12", "hexalith-release-run: 99"),
                     self.body + "hexalith-release-run: 12\n", self.body.replace("hexalith-release-containers: \n", "")):
            with self.subTest(body=body):
                self.responses["releases/11"]["body"] = body
                self.binding["publication"]["releaseBodySha256"] = hashlib.sha256(body.encode()).hexdigest()
                with self.assertRaisesRegex(ValueError, "marker differs"):
                    self.verify()

    def test_annotated_tag_is_peeled_and_unbounded_tag_chain_refuses(self):
        path = "git/ref/tags/" + self.binding["publication"]["releaseTag"]
        self.responses[path] = {"object": {"type": "tag", "sha": "c" * 40}}
        self.responses["git/tags/" + "c" * 40] = {"object": {"type": "commit", "sha": self.binding["producer"]["sourceRevision"]}}
        self.verify()
        self.responses["git/tags/" + "c" * 40] = self.responses[path]
        with self.assertRaises(ValueError):
            self.verify()


if __name__ == "__main__":
    unittest.main()
