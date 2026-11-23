"""Verify R4 review fixtures offline using the published v1 envelope and exact fixture joins."""

from collections import Counter
from copy import deepcopy
from datetime import datetime, timedelta
from hashlib import sha256
import json
from pathlib import Path
import unittest
from xml.etree import ElementTree

from jsonschema import Draft202012Validator, FormatChecker


PACKET = Path(__file__).resolve().parent
ROOT = PACKET.parents[2]
FIXTURES = PACKET / "fixtures"
UNAVAILABLE = {"state": "not-yet-populated"}
TRX_NAMESPACE = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate fixture key {key}")
        result[key] = value
    return result


def read_json(path):
    return json.loads(path.read_bytes().decode("utf-8"), object_pairs_hook=unique_object)


def fixture(name):
    return read_json(FIXTURES / name)


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode()


def digest(raw):
    return "sha256:" + sha256(raw).hexdigest()


def time(value):
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


class ReviewFixtures(unittest.TestCase):
    def setUp(self):
        self.descriptor = fixture("descriptor.json")
        self.result = fixture("result.json")
        self.partial = fixture("partial-startup-result.json")
        self.bindings = self.descriptor["bindings"]
        self.qualification = self.bindings["qualification"]
        self.scope = self.qualification["scope"]
        self.entry = fixture("selection-registry.json")["entries"][0]

    def test_public_envelopes(self):
        for kind, document in (("environment", self.descriptor), ("result", self.result), ("result", self.partial)):
            with self.subTest(kind=kind, terminal=document.get("terminal")):
                self.assertEqual(document["schema"], f"hexalith.test-{kind}.v2")
                inherited = deepcopy(document)
                inherited["schema"] = f"hexalith.test-{kind}.v1"
                del inherited["bindings"]["qualification"]
                schema = read_json(ROOT / f"docs/contracts/schemas/hexalith.test-{kind}.v1.json")
                Draft202012Validator(schema, format_checker=FormatChecker()).validate(inherited)
                self.assertEqual(document["bindings"]["run"], self.bindings["run"])
        partial_bindings = self.partial["bindings"]
        self.assertEqual(partial_bindings["qualification"], UNAVAILABLE)
        self.assertEqual(partial_bindings["suite"]["completion"], UNAVAILABLE)
        self.assertEqual(partial_bindings["environment"]["resources"], UNAVAILABLE)

    def test_immutable_declarations_and_holds(self):
        resolved = self.result["bindings"]
        q_result = resolved["qualification"]
        self.assertEqual(set(q_result), {"scope", "profile", "hosts", "operations", "controls", "observations"})
        for name in ("scope", "profile", "hosts", "operations", "controls"):
            self.assertEqual(q_result[name], self.qualification[name])
        for name in ("runId", "ownerRunId", "attemptId", "environmentId"):
            self.assertEqual(self.scope[name], self.bindings["run"][name])
        for name in ("suiteId", "suiteContentIdentity", "configurationContentIdentity", "requiredTestSetContentIdentity"):
            self.assertEqual(self.scope[name], self.bindings["suite"]["declared"][name])
        before = self.bindings["attachments"]["holds"][0]
        after = resolved["attachments"]["holds"][0]
        self.assertEqual(after, {**before, "end": after["end"]})
        self.assertEqual(after["end"]["reason"], "ci-job-completed")
        self.assertEqual(time(after["expiresAt"]) - time(after["acceptedAt"]), timedelta(seconds=120))
        self.assertLess(time(after["end"]["endedAt"]), time(after["expiresAt"]))
        self.assertEqual(self.bindings["deadlines"], resolved["deadlines"])

    def test_exact_artifact_bytes_and_references(self):
        for filename, reference in (
            ("profile.json", "profileArtifactRef"),
            ("configuration.json", "configurationArtifactRef"),
            ("required-test-set.json", "testSetArtifactRef"),
            ("writer-inventory.json", "inventoryArtifactRef"),
            ("selection-registry.json", None),
            ("control-request.json", None),
        ):
            with self.subTest(filename=filename):
                raw = (FIXTURES / filename).read_bytes()
                self.assertEqual(raw, canonical(fixture(filename)))
                if reference:
                    self.assertEqual(digest(raw), self.entry[reference]["contentIdentity"])
                    self.assertNotEqual(digest(raw[:-1]), self.entry[reference]["contentIdentity"])
        profile = fixture("profile.json")
        config = fixture("configuration.json")
        self.assertEqual(profile["configurationContentIdentity"], self.scope["configurationContentIdentity"])
        self.assertEqual(profile["requiredTestSetContentIdentity"], self.scope["requiredTestSetContentIdentity"])
        self.assertEqual(profile["writerAliasInventoryContentIdentity"], self.entry["inventoryArtifactRef"]["contentIdentity"])
        self.assertEqual(self.scope["profileContentIdentity"], self.entry["profileArtifactRef"]["contentIdentity"])
        self.assertEqual(fixture("writer-inventory.json")["operationContractRef"], config["operationContractRef"])
        self.assertEqual(self.qualification["profile"]["expected"], profile["expected"])
        test_ids = [test["testId"] for test in fixture("required-test-set.json")["tests"]]
        self.assertEqual(test_ids, sorted(set(test_ids)))
        self.assertEqual(self.bindings["suite"]["declared"]["requiredTestIds"], test_ids)
        control = self.qualification["controls"][0]
        origin = time(self.bindings["deadlines"]["startup"]["originAt"])
        execution_end = time(self.bindings["deadlines"]["execution"]["expiresAt"])
        template = profile["controls"][0]
        admission = min(origin + timedelta(seconds=template["admissionLimitSeconds"]), execution_end)
        reset = min(admission + timedelta(seconds=template["resetLimitSeconds"]),
                    execution_end + timedelta(seconds=config["stopSeconds"] + config["cleanupSeconds"]))
        self.assertEqual(time(control["expiresAt"]), admission)
        self.assertEqual(time(control["reset"]["deadline"]), reset)

    def test_selection_and_private_grants(self):
        registry = fixture("selection-registry.json")
        plan = fixture("run-plan.json")
        credentials = fixture("private-credential-canaries.json")
        key = {field: self.entry[field] for field in ("moduleId", "suiteName")}
        self.assertEqual(registry["qualificationSuites"], [key])
        self.assertEqual(plan["Schema"], "hexalith.g4-run-plan.v2")
        self.assertEqual(plan["QualificationSelection"], self.entry)
        selected = plan["SelectedTest"]
        self.assertEqual(plan["SelectedTests"], [selected])
        self.assertEqual(selected["ModuleId"], key["moduleId"])
        self.assertEqual(selected["Name"], key["suiteName"])
        self.assertEqual((selected["Executor"], selected["Mode"]), ("vstest", self.entry["mode"]))
        for member, entry_member in (("ManifestHash", "manifestContentIdentity"), ("EntryHash", "entryContentIdentity")):
            self.assertEqual("sha256:" + selected[member].lower(), self.entry[entry_member])
        self.assertEqual(
            (self.entry["descriptorSchema"], self.entry["resultSchema"], self.entry["credentialSchema"]),
            (self.descriptor["schema"], self.result["schema"], credentials["schema"]),
        )
        self.assertEqual(credentials["scope"], self.scope)
        resources = {resource["resourceId"]: resource for resource in self.bindings["environment"]["resources"]}
        combinations = {
            "dapr-http-api-token": ("http", "dapr-api-token", "invocation"),
            "dapr-grpc-api-token": ("grpc", "dapr-api-token", "invocation"),
            "control-http-token": ("http", "x-hexalith-control-token", "control"),
        }
        self.assertEqual({entry["credentialKind"] for entry in credentials["endpoints"]}, set(combinations))
        control = self.qualification["controls"][0]
        for entry in credentials["endpoints"]:
            with self.subTest(kind=entry["credentialKind"]):
                resource = resources[entry["resourceId"]]
                endpoint, = [ep for ep in resource["endpoints"] if ep["name"] == entry["endpointName"]]
                self.assertEqual((entry["moduleId"], entry["uri"]), (resource["moduleId"], endpoint["uri"]))
                grant = entry["grant"]
                self.assertEqual((entry["protocol"], entry["headerName"], grant["kind"]), combinations[entry["credentialKind"]])
                self.assertEqual(grant["partition"], self.qualification["operations"]["partition"])
                self.assertLess(time(entry["issuedAt"]), time(entry["expiresAt"]))
                self.assertLessEqual(time(entry["expiresAt"]), time(credentials["expiresAt"]))
                self.assertRegex(entry["value"], r"^SYNTHETIC_CANARY_[123]_FIXTURE_ONLY$")
                for path in FIXTURES.iterdir():
                    if path.name != "private-credential-canaries.json":
                        self.assertNotIn(entry["value"].encode(), path.read_bytes(), path.name)
                if grant["kind"] == "control":
                    self.assertEqual(grant["controlId"], control["controlId"])
                    self.assertEqual(grant["phase"], control["phase"])
                    self.assertEqual(grant["hostIds"], control["hostIds"])
                    self.assertEqual(grant["maximumUses"], control["maximumUses"])
                    self.assertEqual(grant["actions"], ["request", "observe", "release"])

    def test_observations_and_control_request(self):
        before = self.qualification["observations"]
        after = self.result["bindings"]["qualification"]["observations"]
        self.assertEqual(set(before), set(after))
        for key, value in before.items():
            if isinstance(value, dict) and "items" in value:
                prefix = value["items"]
                self.assertEqual(after[key]["items"][:len(prefix)], prefix, key)
        event_list = after["controlEvents"]["items"]
        request = fixture("control-request.json")
        requested = event_list[0]
        for key in ("scope", "hostId", "generationId", "partition", "phase", "controlId", "requestId"):
            self.assertTrue(all(event[key] == request[key] for event in event_list))
        self.assertEqual(requested["detail"]["requestContentIdentity"], digest((FIXTURES / "control-request.json").read_bytes()))
        self.assertEqual(request["barrierObservationId"]["observationId"], after["barriers"]["items"][0]["observationId"])
        self.assertEqual([event["sequence"] for event in event_list], list(range(1, 10)))
        self.assertEqual([event["event"] for event in event_list],
            ["requested", "reserved", "unknown", "reconciled", "released", "reset-requested", "reset-failed",
             "reset-requested", "reset"])
        self.assertEqual([event["detail"]["retryNumber"] for event in event_list
                          if event["event"] == "reset-requested"], [1, 2])
        readback = after["readbacks"]["items"][0]
        fresh = after["freshness"]["items"][0]
        self.assertEqual(readback["freshnessObservationId"], fresh["observationId"])
        self.assertEqual(readback["readerGenerationId"], fresh["generationId"])
        self.assertEqual(readback["readerCacheGenerationId"], fresh["cacheGenerationId"])
        self.assertGreater(time(readback["readObservedAt"]), time(fresh["observedAt"]))
        self.assertEqual(readback["actualLogicalReadbackRef"], UNAVAILABLE)

    def test_trx_bytes_joins_counts_and_comparisons(self):
        declared = fixture("required-test-set.json")["tests"]
        stable_by_tuple = {(row["assemblyFileName"], row["className"], row["methodName"], row["testName"]):
                           row["testId"] for row in declared}
        self.assertEqual(len(stable_by_tuple), len(declared))
        reports = fixture("report-bindings.json")
        outcome_maps = {}
        for filename, record in reports.items():
            with self.subTest(trx=filename):
                raw = (FIXTURES / filename).read_bytes()
                self.assertEqual(record["reportRef"]["contentIdentity"], digest(raw))
                self.assertEqual(record["retainedReportRef"]["contentIdentity"], digest(raw))
                self.assertEqual(record["terminalRef"]["contentIdentity"], digest(canonical(record["ownerTerminal"])))
                trx = ElementTree.fromstring(raw)
                self.assertEqual(trx.tag, "{" + TRX_NAMESPACE["t"] + "}TestRun")
                definitions = trx.findall("t:TestDefinitions/t:UnitTest", TRX_NAMESPACE)
                by_id = {row.attrib["id"]: row for row in definitions}
                self.assertEqual(len(by_id), len(definitions))
                mapping, executions = {}, {}
                outcomes = Counter()
                for row in trx.findall("t:Results/t:UnitTestResult", TRX_NAMESPACE):
                    definition = by_id[row.attrib["testId"]]
                    method = definition.find("t:TestMethod", TRX_NAMESPACE).attrib
                    native_tuple = (method["codeBase"], method["className"], method["name"], definition.attrib["name"])
                    stable = stable_by_tuple[native_tuple]
                    self.assertEqual(definition.attrib["name"], row.attrib["testName"])
                    self.assertEqual(definition.find("t:Execution", TRX_NAMESPACE).attrib["id"], row.attrib["executionId"])
                    self.assertNotIn(stable, mapping)
                    self.assertNotIn(row.attrib["executionId"], executions.values())
                    mapping[stable] = row.attrib["outcome"]
                    executions[stable] = row.attrib["executionId"]
                    outcomes[row.attrib["outcome"]] += 1
                self.assertEqual(record["executionIds"], executions)
                self.assertEqual(len(definitions), len(mapping))
                summary, = trx.findall("t:ResultSummary/t:Counters", TRX_NAMESPACE)
                self.assertEqual(int(summary.attrib["total"]), len(mapping))
                for outcome in ("Passed", "Failed", "Error", "Timeout", "Aborted", "NotExecuted", "Inconclusive"):
                    self.assertEqual(int(summary.attrib[outcome[0].lower() + outcome[1:]]), outcomes[outcome])
                passed = outcomes["Passed"]
                failed = outcomes["Failed"] + outcomes["Error"] + outcomes["Timeout"]
                counts = dict(required=len(declared), executed=passed + failed + outcomes["Aborted"],
                              completed=passed + failed, passed=passed, failed=failed, skipped=outcomes["Inconclusive"])
                self.assertEqual(counts, record["expectedCompletionCounts"])
                outcome_maps[filename] = mapping
        expected_public = self.result["bindings"]["suite"]["completion"]
        mixed = reports["mixed.trx"]
        self.assertEqual(expected_public["counts"], mixed["expectedCompletionCounts"])
        self.assertEqual(self.result["terminal"], mixed["ownerTerminal"])
        self.assertEqual(mixed["scope"], self.scope)
        self.assertEqual(set(mixed["executionIds"]), {test["testId"] for test in expected_public["tests"]})
        public_outcomes = {"Passed": "passed", "Failed": "failed", "Timeout": "failed", "Error": "failed",
                           "Aborted": "cancelled", "NotExecuted": "not-executed", "Inconclusive": "skipped"}
        for row in expected_public["tests"]:
            self.assertEqual(row["outcome"], public_outcomes[outcome_maps["mixed.trx"][row["testId"]]])
        self.assertEqual(set(stable_by_tuple.values()) - set(mixed["executionIds"]), {"08-missing"})
        comparisons = fixture("comparisons.json")
        self.assertEqual({record["removedRule"] for record in comparisons}, {"original-read-revision", "create-once-identity"})
        self.assertEqual(len({record["authorizationRef"]["referenceId"] for record in comparisons}), len(comparisons))
        for comparison in comparisons:
            self.assertEqual(comparison["intactRunRef"]["contentIdentity"], reports["intact.trx"]["terminalRef"]["contentIdentity"])
            for key, expected in (("lawfulControlRef", "Passed"), ("expectedFailedTestRef", "Failed")):
                ref = comparison[key]
                report = ref["reportRef"]["referenceId"]
                self.assertEqual(ref["reportRef"], reports[report]["reportRef"])
                self.assertEqual(ref["executionId"], reports[report]["executionIds"][ref["testId"]])
                self.assertEqual(comparison["alteredRunRef"]["contentIdentity"], reports[report]["terminalRef"]["contentIdentity"])
                self.assertEqual(outcome_maps[report][ref["testId"]], expected)
                self.assertEqual(outcome_maps["intact.trx"][ref["testId"]], "Passed")
                self.assertEqual(reports[report]["ownerTerminal"]["outcome"], "failed")

    def test_ci_lifecycle_traces(self):
        traces = fixture("ci-lifecycle.json")
        context = traces["caseContext"]
        receipt = context["receipt"]
        self.assertFalse(Path(context["externalReceiptPath"]).is_relative_to(context["runWorkspace"]))
        for field in ("runId", "attemptId", "environmentId"):
            self.assertEqual(receipt[field], self.scope[field])
        self.assertEqual(receipt["executionContext"], self.bindings["environment"]["executionContext"])
        self.assertEqual(context["durableOwner"], {key: receipt[key] for key in context["durableOwner"]})
        cases = {case["id"]: case for case in traces["cases"]}
        self.assertEqual(set(cases), {f"LC-0{i}" for i in range(1, 7)})
        timeout = cases["LC-01"]
        self.assertEqual((timeout["execution"]["timedOut"], timeout["execution"]["cancelled"], timeout["helperExit"]),
                         (True, False, 124))
        first, prior = timeout["firstTerminalVariants"]
        self.assertEqual(first["ownerTerminal"]["outcome"], "cancelled")
        self.assertEqual(prior["ownerTerminal"], self.result["terminal"])
        self.assertEqual(prior["holdReason"], prior["priorHoldEnd"]["reason"])
        self.assertLess(time(prior["priorHoldEnd"]["endedAt"]), time(prior["helperDeadlineAt"]))
        actual = cases["LC-02"]
        for job, reasons in actual["expectedHoldReasonsByJobStatus"].items():
            for terminal, reason in zip(actual["ownerTerminals"], reasons, strict=True):
                cancelled = job == "cancelled" or terminal in (None, "cancelled", "withdrawn")
                self.assertEqual(reason, "ci-job-cancelled" if cancelled else "ci-job-completed")
        interruption = cases["LC-03"]
        self.assertEqual(interruption["signalTargets"], ["gated-muxer", "tool"])
        self.assertEqual(interruption["missingReportCompletion"], self.partial["bindings"]["suite"]["completion"])
        recovery = cases["LC-04"]
        self.assertIn("--run-id=" + self.scope["ownerRunId"], recovery["exactArgv"])
        self.assertEqual(recovery["actualFinalizerJobStatus"], recovery["downEnvironment"]["HEXALITH_CI_JOB_STATUS"])
        for variant in recovery["variants"]:
            with self.subTest(recovery=variant):
                exact = variant["durableOwnerMatches"] is True and variant.get("actualJobMatches", True)
                self.assertEqual(variant["downInvocations"], int(exact))
                self.assertEqual(variant["result"] == "not-allocated", variant["lastReceipt"] == "absent")
        cleanup = cases["LC-05"]
        self.assertEqual(cleanup["priorTerminal"], cleanup["preservedTerminal"])
        for case in cleanup["cases"]:
            with self.subTest(condition=case["condition"]):
                complete = case["publicCleanup"].get("disposition") == "complete"
                remaining = case.get("downRemaining")
                verified = bool(remaining is not None and not any(remaining.values()))
                self.assertEqual(complete, verified)
                if "cleanupRecord" in case:
                    self.assertEqual(case["cleanupRecord"]["status"], "tool-unavailable")
                    self.assertFalse(case["cleanupRecord"]["verified"])
        reset = cases["LC-06"]
        self.assertLess(time(reset["cancellationAt"]), time(reset["credentialExpiresAt"]))
        self.assertLess(time(reset["verifiedResetAt"]), time(reset["originalResetDeadline"]))
        self.assertEqual(reset["expiredRetryAt"], reset["originalResetDeadline"])
        self.assertFalse(reset["moduleRequestAllowed"])
        self.assertFalse(reset["expiredRetryAllowed"])
        self.assertFalse(reset["reopenAccess"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
