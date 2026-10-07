"""Offline tests for the live-check verdict logic (no network)."""
import os, sys, unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import external_probe, keycloak_recovery_check, registry_closure


def probe(path, status, port=443, method="GET", host="auth.tache.ai", location=None, not_routed=False):
    return {"host": host, "port": port, "method": method, "path": path, "status": status, "location": location, "traefikNotRouted": not_routed}


class ClosureClassifierTests(unittest.TestCase):
    def verdict(self, r, by_key=None):
        return external_probe.classify(r, by_key or {})

    def test_traefik_not_routed_404_is_closed(self):
        self.assertEqual(self.verdict(probe("/admin/", 404, not_routed=True)), "not-routed")

    def test_backend_404_outside_approved_prefixes_is_open(self):
        self.assertEqual(self.verdict(probe("/realms/Master/protocol/openid-connect/token", 404)), "open")

    def test_reopened_master_token_405_is_open(self):
        self.assertEqual(self.verdict(probe("/realms/master/protocol/openid-connect/token", 405)), "open")

    def test_backend_400_outside_approved_prefixes_is_open(self):
        self.assertEqual(self.verdict(probe("/realms/master/protocol/openid-connect/token", 400, method="POST")), "open")

    def test_login_page_or_redirect_on_https_is_open(self):
        self.assertEqual(self.verdict(probe("/admin/", 200)), "open")
        self.assertEqual(self.verdict(probe("/admin/", 302, location="https://auth.tache.ai/admin/master/console/")), "open")

    def test_connection_failure_is_inconclusive(self):
        self.assertEqual(self.verdict(probe("/admin/", "TimeoutError")), "inconclusive")

    def test_backend_refusal_under_approved_prefix_is_closed(self):
        self.assertEqual(self.verdict(probe("/realms/tache/..%2Fmaster/protocol/openid-connect/token", 404)), "approved-prefix-backend-refusal")
        self.assertEqual(self.verdict(probe("/resources/..%2Frealms/master/protocol/openid-connect/token", 405, method="POST")),
                         "approved-prefix-backend-refusal")

    def test_prefix_match_respects_path_segments(self):
        self.assertEqual(self.verdict(probe("/realms/tachex/protocol/openid-connect/token", 404)), "open")

    def test_approved_prefix_success_is_open(self):
        self.assertEqual(self.verdict(probe("/realms/tache/..%2Fmaster/protocol/openid-connect/token", 200)), "open")

    def test_http_redirect_closed_only_when_https_counterpart_closed(self):
        path = "/realms/tache/..%2Fmaster/protocol/openid-connect/token"
        redirect = probe(path, 308, port=80, location="https://auth.tache.ai" + path)
        https_closed = dict(probe(path, 404), verdict="approved-prefix-backend-refusal")
        https_open = dict(probe(path, 200), verdict="open")
        self.assertEqual(self.verdict(redirect, {("auth.tache.ai", 443, "GET", path): https_closed}), "redirect-to-closed-https")
        self.assertEqual(self.verdict(redirect, {("auth.tache.ai", 443, "GET", path): https_open}), "redirect-to-open")
        self.assertEqual(self.verdict(redirect), "redirect-to-open")

    def test_http_redirect_to_other_location_is_open(self):
        r = probe("/admin/", 302, port=80, location="https://auth.tache.ai/admin/master/console/")
        self.assertEqual(self.verdict(r, {("auth.tache.ai", 443, "GET", "/admin/"): dict(probe("/admin/", 404, not_routed=True), verdict="not-routed")}),
                         "open")


class RecoveryEvaluationTests(unittest.TestCase):
    def passing(self, **changes):
        res = {"environmentIsolated": True, "publicOidcBlockedInSession": True, "recoveryTokenStatus": 200, "adminReadStatus": 200,
               "realmNames": ["master", "tache"], "namedAdministratorPresent": True, "wrongSecretStatus": 401}
        res.update(changes)
        return keycloak_recovery_check.evaluate(res)

    def test_complete_measurement_passes(self):
        self.assertTrue(self.passing())

    def test_each_failed_condition_fails(self):
        for change in ({"environmentIsolated": False}, {"publicOidcBlockedInSession": False}, {"recoveryTokenStatus": 401},
                       {"adminReadStatus": 403}, {"realmNames": ["master"]}, {"namedAdministratorPresent": False},
                       {"wrongSecretStatus": 200}):
            with self.subTest(change=change):
                self.assertFalse(self.passing(**change))


class ClosureCompareTests(unittest.TestCase):
    def closure(self, objects, tags):
        return {"repositories": {"r": {"objects": objects, "tags": tags}}}

    def test_new_content_only_passes(self):
        res = registry_closure.compare(self.closure(["a"], {"1": "a"}), self.closure(["a", "b"], {"1": "a", "2": "b"}))
        self.assertTrue(res["pass"]); self.assertEqual(res["newObjects"], {"r": ["b"]}); self.assertEqual(res["newTags"], {"r": ["2"]})

    def test_lost_object_fails(self):
        self.assertFalse(registry_closure.compare(self.closure(["a", "b"], {"1": "a"}), self.closure(["a"], {"1": "a"}))["pass"])

    def test_changed_or_removed_tag_fails(self):
        self.assertFalse(registry_closure.compare(self.closure(["a", "b"], {"1": "a"}), self.closure(["a", "b"], {"1": "b"}))["pass"])
        self.assertFalse(registry_closure.compare(self.closure(["a"], {"1": "a"}), self.closure(["a"], {}))["pass"])

    def test_missing_repository_fails(self):
        self.assertFalse(registry_closure.compare(self.closure(["a"], {"1": "a"}), {"repositories": {}})["pass"])


if __name__ == "__main__":
    unittest.main()
