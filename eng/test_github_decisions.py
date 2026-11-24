import hashlib
import json
import unittest
from unittest.mock import patch

from hexalith_github_decisions import ExactReviewBinding, GitHubDecisionError, authenticate_review, strict_json
from hexalith_github_reviews import GitHubReviewClient, RequestLimits, ReviewLocator


class ReviewBodyBudgetTests(unittest.TestCase):
    def setUp(self):
        self.client = GitHubReviewClient()
        self.locator = ReviewLocator("example", "repository", 1, 2)
        self.commit = "a" * 40
        self.body = {"first": "a" * 4096, "second": "é" * 3000}
        self.raw = json.dumps(self.body, ensure_ascii=False).encode("utf-8")
        self.binding = ExactReviewBinding(
            self.locator, "github:user:3", self.commit, hashlib.sha256(self.raw).hexdigest(),
            len(self.raw), self.raw,
        )
        self.envelope = {
            "id": 2, "user": {"type": "User", "id": 3}, "state": "APPROVED",
            "body": self.raw.decode("utf-8"), "commit_id": self.commit,
            "submitted_at": "2026-11-24T12:00:00Z",
            "pull_request_url": self.client.api_origin + self.locator.pull_path,
        }

    def authenticate(self):
        response = json.dumps(self.envelope, ensure_ascii=True).encode("ascii")
        with patch.object(GitHubReviewClient, "fetch_review", return_value=response), \
                patch("hexalith_github_decisions._elapsed", side_effect=[1.0, 2.0]):
            result = authenticate_review(
                client=self.client, token="fixture-token", binding=self.binding,
                expected_issuer=self.client.issuer, limits=RequestLimits(64_000, 1.0),
            )
        self.assertEqual(result.response_sha256, hashlib.sha256(response).hexdigest())
        return result

    def test_authenticates_large_review_with_individually_bounded_strings(self):
        result = self.authenticate()
        self.assertEqual(result.body, self.body)
        self.assertEqual(result.body_sha256, self.binding.body_sha256)

    def test_generic_parser_and_envelope_metadata_keep_individual_string_limit(self):
        self.envelope["user"]["body"] = "a" * 4097
        with self.assertRaisesRegex(GitHubDecisionError, "^string-budget-exceeded$"):
            self.authenticate()
        invalid = json.dumps({"body": "a" * 4097}).encode("ascii")
        with self.assertRaisesRegex(GitHubDecisionError, "^string-budget-exceeded$"):
            strict_json(invalid)
        with self.assertRaisesRegex(GitHubDecisionError, "^string-budget-exceeded$"):
            ExactReviewBinding(
                self.locator, "github:user:3", self.commit, hashlib.sha256(invalid).hexdigest(),
                len(invalid), invalid,
            )

    def test_large_review_must_still_match_retained_bytes(self):
        self.envelope["body"] = self.envelope["body"].replace("é", "è")
        with self.assertRaisesRegex(GitHubDecisionError, "^decision-body-mismatch$"):
            self.authenticate()
