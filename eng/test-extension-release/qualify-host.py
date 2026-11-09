#!/usr/bin/env python3
"""Observe only the declared fixture's extension readiness through the native handoff."""
from __future__ import annotations

import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET

MODULE = "platformtestextension"
PROFILE = "eventstore-extension-package"
SOURCE_ENTRY = "tests/Hexalith.Platform.TestExtension.Qualification.Tests/Hexalith.Platform.TestExtension.Qualification.Tests.csproj"
OBSERVER_ENTRY = "eng/test-extension-release/qualify-host.py"
SOURCE_FILES = {'tests/Hexalith.Platform.TestExtension.Qualification.Tests/Hexalith.Platform.TestExtension.Qualification.Tests.csproj': 'build/qualification/Hexalith.Platform.TestExtension.Qualification.Tests.csproj', 'tests/Hexalith.Platform.TestExtension.Qualification.Tests/ReadinessTests.cs': 'build/qualification/ReadinessTests.cs', 'eng/test-extension-release/qualify-host.py': 'build/qualification/qualify-host.py'}


def unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate fixture identity field")
        result[key] = value
    return result


def handoff():
    """Check the selected test and source bytes; this creates no admission authority."""
    entry = Path(__file__).resolve()
    root = entry.parents[2]
    fixture = root / "tests/fixtures/Hexalith.Platform.TestExtension"
    declaration_bytes = (fixture / "hexalith.module-manifest.v2.json").read_bytes()
    declaration = json.loads(declaration_bytes, object_pairs_hook=unique)
    producer = json.loads((fixture / "fixture-producer.json").read_bytes(), object_pairs_hook=unique)
    module = declaration["modules"][0]
    selection = module["tests"]
    expected = {"name": PROFILE, "executor": "mtp", "entry": SOURCE_ENTRY,
                "reportFormat": "trx", "executionTimeoutSeconds": 120,
                "stopTimeoutSeconds": 90, "mode": "package"}
    expected_files = {source: {"path": packaged, "sha256": hashlib.sha256((root / source).read_bytes()).hexdigest()}
                      for source, packaged in SOURCE_FILES.items()}
    entry_hash = expected_files[SOURCE_ENTRY]["sha256"]
    if (len(declaration["modules"]) != 1 or module["identity"]["moduleId"] != MODULE
            or selection != [expected] or entry.relative_to(root).as_posix() != OBSERVER_ENTRY
            or producer["publisherRepository"] != "Hexalith/Hexalith.Platform"
            or producer["ownerRepositoryIdentity"] != "Hexalith.Platform"
            or producer["moduleId"] != MODULE or producer["serviceEnrollment"] is not False
            or producer["test"] != {"name": PROFILE, "sourceEntry": SOURCE_ENTRY, "files": expected_files}):
        raise ValueError("Fixture producer or selected test identity differs")
    required = {"HEXALITH_G4_TEST_OWNER": MODULE, "HEXALITH_G4_TEST_NAME": PROFILE,
                "HEXALITH_G4_TEST_MODE": "package",
                "HEXALITH_G4_TEST_MANIFEST_HASH": hashlib.sha256(declaration_bytes).hexdigest(),
                "HEXALITH_G4_TEST_ENTRY_HASH": entry_hash}
    # The actual SDK emits uppercase SHA256; hex casing does not change the selected bytes.
    actual = {key: os.environ.get(key, "") for key in required}
    for key in ("HEXALITH_G4_TEST_MANIFEST_HASH", "HEXALITH_G4_TEST_ENTRY_HASH"):
        actual[key] = actual[key].lower()
    if any(actual[key] != value for key, value in required.items()):
        raise ValueError("Native test selection differs from the owned fixture")
    if re.fullmatch(r"[a-f0-9]{32}", os.environ.get("HEXALITH_G4_RUN_ID", "")) is None:
        raise ValueError("Native run identity is unavailable")
    deadline = datetime.datetime.fromisoformat(os.environ["HEXALITH_G4_EXECUTION_DEADLINE_UTC"].replace("Z", "+00:00"))
    if deadline.tzinfo is None:
        raise ValueError("Native execution deadline must identify UTC")
    remaining = (deadline - datetime.datetime.now(datetime.timezone.utc)).total_seconds()
    if remaining <= 0:
        raise ValueError("Native execution deadline has expired")
    endpoint = urllib.parse.urlsplit(os.environ["HEXALITH_G4_EVENTSTORE_URL"])
    if (endpoint.scheme not in ("http", "https") or not endpoint.hostname
            or endpoint.username is not None or endpoint.password is not None
            or endpoint.query or endpoint.fragment or endpoint.path not in ("", "/")):
        raise ValueError("Native EventStore endpoint is unavailable")
    report = Path(os.environ["HEXALITH_G4_TEST_REPORT_PATH"])
    if not report.is_absolute() or not report.parent.is_dir():
        raise ValueError("Native report path is unavailable")
    extensions = module["runtime"]["eventStoreExtensions"]
    if len(extensions) != 1:
        raise ValueError("Fixture declaration has no unique extension")
    return urllib.parse.urlunsplit(endpoint._replace(path="/ready")), min(5.0, remaining), report, extensions[0], deadline


class RefuseRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, new_url):
        return None


def observe(url, timeout, declared):
    """An unrelated distributed readiness failure does not erase a healthy extension."""
    request = urllib.request.Request(url, headers={"Accept": "application/json"})
    try:
        response = urllib.request.build_opener(RefuseRedirects()).open(request, timeout=timeout)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        if response.status not in (200, 503):
            raise ValueError("Extension readiness endpoint refused the request")
        payload = response.read(262145)
        if len(payload) > 262144:
            raise ValueError("Extension readiness response exceeds its bound")
        result = json.loads(payload, object_pairs_hook=unique)["results"]["eventstore-extensions"]
    actual_major = result["data"][declared["extensionId"]]
    if (result["status"] != "Healthy" or type(actual_major) is not int
            or actual_major != declared["extensionApiMajor"]
            or f"{declared['extensionId']}@{actual_major} ({declared['packageId']})" not in result["description"]):
        raise ValueError("Declared extension identity or health differs")


def write_report(path, passed):
    document = ET.Element("TestRun", xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
    results = ET.SubElement(document, "Results")
    ET.SubElement(results, "UnitTestResult", testName="Declared Platform extension readiness",
                  outcome="Passed" if passed else "Failed")
    summary = ET.SubElement(document, "ResultSummary", outcome="Completed" if passed else "Failed")
    ET.SubElement(summary, "Counters", total="1", executed="1", passed="1" if passed else "0",
                  failed="0" if passed else "1", notExecuted="0")
    with path.open("xb") as stream:
        stream.write(ET.tostring(document, encoding="utf-8", xml_declaration=True))


def main():
    try:
        url, timeout, report, declared, deadline = handoff()
    except (OSError, ValueError, KeyError, IndexError, TypeError):
        print("Fixture native handoff refused.", file=sys.stderr)
        return 1
    passed = True
    try:
        observe(url, timeout, declared)
        if datetime.datetime.now(datetime.timezone.utc) >= deadline:
            raise ValueError("Native execution deadline expired during readiness observation")
    except (OSError, ValueError, KeyError, TypeError):
        passed = False
    try:
        write_report(report, passed)
    except (OSError, ValueError):
        print("Fixture native report refused.", file=sys.stderr)
        return 1
    print("Declared extension readiness observed." if passed else "Declared extension readiness failed.")
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
