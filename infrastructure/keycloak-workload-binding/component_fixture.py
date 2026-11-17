"""Bounded disposable component lifetime and prebuilt xUnit execution."""
import json
import os
from pathlib import Path
import re
import shutil
import secrets
import subprocess
import tempfile
import xml.etree.ElementTree as ET

import run_owned as owned_processes


class ChildFailure(RuntimeError):
    """Preserve the test runner's native nonzero status."""

    def __init__(self, code):
        super().__init__("Component test child failed.")
        self.code = code


def command(*args):
    """Do not disclose command arguments or protected command output."""
    try:
        return subprocess.check_output(args, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace", timeout=45).strip()
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
        raise RuntimeError("Component command failed or exceeded its deadline; protected arguments and output withheld.") from None


def sanitize(output, secrets=(), limit=16000):
    """Redact credentials and tokens; bound emitted diagnostics by default."""
    for secret in sorted((value for value in secrets if value), key=len, reverse=True):
        output = output.replace(secret, "[redacted]")
    output = re.sub(r"[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]+", "[redacted-token]", output)
    output = re.sub(r'(?i)("?(?:secret|client_secret|password|access_token)"?\s*[:=]\s*)[^\s,}]+', r'\1[redacted]', output)
    return output[-limit:] if limit is not None else output


def verify_execution(output, result_file, project, complete):
    """Require one successful nonempty summary and actual passing security cases."""
    pattern = (r"(?m)^\s*" + re.escape(project) + r"\s+Total:\s*(\d+),\s*Errors:\s*(\d+),\s*Failed:\s*(\d+),"
               r"\s*Skipped:\s*(\d+),\s*Not Run:\s*(\d+)(?:,\s*Time:\s*\d+(?:\.\d+)?s)?\s*$")
    summaries = re.findall(pattern, output)
    if len(summaries) != 1 or int(summaries[0][0]) < 1 or any(int(value) for value in summaries[0][1:]):
        raise RuntimeError("Component child did not report one nonempty successful execution with zero error/failure/skip/not-run counters.")
    try:
        assemblies = ET.parse(result_file).getroot().findall("assembly")
        tests = [test for assembly in assemblies for test in assembly.findall(".//test")]
        if len(assemblies) != 1 or len(tests) != int(summaries[0][0]) or any(test.get("result") != "Pass" for test in tests):
            raise ValueError()
        assembly = assemblies[0]
        if int(assembly.get("total", "-1")) != len(tests) or int(assembly.get("passed", "-1")) != len(tests) \
                or any(int(assembly.attrib[counter]) != 0 for counter in ("errors", "failed", "skipped", "not-run")) or assembly.findall(".//error"):
            raise ValueError()
        if assembly.find("errors") is not None and len(assembly.find("errors")):
            raise ValueError()
        security = [test for test in tests if test.get("type") == "Hexalith.Folders.Server.Tests.EnrolledWorkloadSourceBoundaryTests"
                    and test.get("method") == "ActualIssuerAndIndependentSdkGuardEverySourceRequest"]
        faults = ("none", "credential", "caller", "tenant", "audience", "signature", "scope", "expired", "malformed", "missing-proof", "cancelled")
        names = [test.get("name", "").replace(chr(92) + '"', '"') for test in security]
        if len(names) != 11 or len(set(names)) != 11 or any(sum(f'fault: "{fault}"' in name for name in names) != 1 for fault in faults):
            raise ValueError()
        if complete and len(tests) <= 11:
            raise ValueError()
    except (OSError, ET.ParseError, ValueError, KeyError):
        raise RuntimeError("Component child XML does not prove the required executed test inventory.") from None
    return dict(total=len(tests), errors=0, failed=0, skipped=0, notRun=0, securityCases=len(security), completeProject=complete)


class ComponentFixture:
    """Clean only this invocation's container and private directory, including failures."""

    def __init__(self, name, runner=command):
        self.name = name
        self.owner_token = secrets.token_hex(16)
        self.runner = runner
        self.directory = None
        self.secrets = []

    def __enter__(self):
        self.directory = Path(tempfile.mkdtemp(prefix=self.name + "-"))
        self.old_umask = os.umask(0o077)
        return self

    def __exit__(self, exc_type, exc, traceback):
        failures = []
        remaining = None
        try:
            remaining = self.runner("docker", "ps", "--all", "--no-trunc", "--filter", "name=^/" + self.name + "$", "--filter", "label=hexalith.component.fixture=" + self.owner_token, "--format", "{{.ID}}")
            if remaining:
                self.runner("docker", "rm", "--force", "--volumes", remaining)
            remaining = self.runner("docker", "ps", "--all", "--no-trunc", "--filter", "name=^/" + self.name + "$", "--filter", "label=hexalith.component.fixture=" + self.owner_token, "--format", "{{.ID}}")
            if remaining:
                failures.append("owned-container-cleanup-incomplete")
        except (OSError, RuntimeError):
            failures.append("owned-container-cleanup-unverified")
        try:
            shutil.rmtree(self.directory)
            if self.directory.exists():
                failures.append("owned-private-material-cleanup-incomplete")
        except OSError:
            failures.append("owned-private-material-cleanup-incomplete")
        finally:
            os.umask(self.old_umask)
        self.emit(dict(componentFixtureCleanup=True, observedRemainingContainers=0 if remaining == "" else None,
                       observedRemainingTemporaryPaths=int(self.directory.exists()), failures=failures))
        if failures:
            raise RuntimeError("; ".join(failures)) from None

    def run_tests(self, assembly, complete=False, timeout=600, evidence_file=None):
        """Launch once, await the whole process group and validate native/semantic results."""
        assembly = Path(assembly).resolve(strict=True)
        result_file = self.directory / "sdk-results.xml"
        args = ["dotnet", str(assembly), "-noLogo", "-noColor", "-explicit", "on", "-xml", str(result_file)]
        if not complete:
            args += ["-class", "Hexalith.Folders.Server.Tests.EnrolledWorkloadSourceBoundaryTests"]
        environment = dict(os.environ, HEXALITH_WORKLOAD_COMPONENT_FILE=str(self.directory / "sdk-component.json"))
        owned_processes.enable_subreaper()
        existing_children = owned_processes.existing_process_tree()
        with tempfile.TemporaryFile(mode="w+", encoding="utf-8", errors="replace", dir=self.directory) as output:
            child = subprocess.Popen(args, env=environment, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
            timed_out = False
            try:
                child.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                timed_out = True
            finally:
                # Reuse the owned launcher's subreaper and PID-identity checks so
                # descendants that escape their group are also observed and reaped.
                owned_processes.stop_child(child, existing_children)
                child.wait()
            output.seek(0)
            captured = output.read()
        print(sanitize(captured, self.secrets), end="")
        evidence_errors = self.record_evidence(captured, result_file, evidence_file)
        if timed_out:
            raise RuntimeError("Component test child deadline elapsed.")
        if child.returncode != 0:
            raise ChildFailure(child.returncode)
        if evidence_errors:
            raise RuntimeError("Component child produced malformed or unretainable evidence.")
        return verify_execution(captured, result_file, assembly.stem, complete)

    def emit(self, value):
        """Sanitize and bound every structured diagnostic before emission."""
        # Redact values before JSON encoding too, so escaped credentials cannot evade matching.
        def clean(item):
            if isinstance(item, str):
                return sanitize(item, self.secrets)
            if isinstance(item, dict):
                return {sanitize(str(key), self.secrets): clean(value) for key, value in item.items()}
            if isinstance(item, list):
                return [clean(value) for value in item]
            return item
        encoded = json.dumps(clean(value))
        if len(encoded) > 16000:
            encoded = json.dumps(dict(componentDiagnosticTruncated=True, tail=encoded[-7000:]))
        print(encoded)

    def record_evidence(self, captured, result_file, evidence_file):
        """Retain sanitized observations best-effort without masking child status."""
        errors = []
        bindings = []
        for match in re.finditer(r"(?m)^COMPONENT_RUNTIME_BINDING (\{[^\n]+\})$", captured):
            try:
                binding = json.loads(match.group(1))
                allowed = {key: binding[key] for key in ("name", "authority", "version", "packageId", "packageVersion", "runtimeAsset", "packageSha512", "archiveSha512", "sha256", "mvid") if key in binding}
                bindings.append(allowed)
            except (ValueError, TypeError):
                errors.append("malformed-runtime-binding")
        if result_file.exists():
            try:
                result = ET.parse(result_file)
                for element in result.iter():
                    element.attrib = {key: sanitize(value, self.secrets, limit=None) for key, value in element.attrib.items()}
                    if element.text:
                        element.text = sanitize(element.text, self.secrets, limit=None)
                    if element.tail:
                        element.tail = sanitize(element.tail, self.secrets, limit=None)
                tests = result.getroot().findall(".//test")
                security = [test for test in tests if test.get("type") == "Hexalith.Folders.Server.Tests.EnrolledWorkloadSourceBoundaryTests"]
                self.emit(dict(componentExecutionObservation=True, total=len(tests), securityCases=len(security),
                    securityPassed=sum(test.get("result") == "Pass" for test in security),
                    securityOutcomes=[dict(name=test.get("name"), result=test.get("result")) for test in security]))
                failures = [test for test in tests if test.get("result") == "Fail"]
                if failures:
                    print(sanitize("\n".join(ET.tostring(test, encoding="unicode") for test in failures), self.secrets))
                if evidence_file:
                    result.write(Path(evidence_file).resolve(), encoding="utf-8", xml_declaration=True)
            except (OSError, ET.ParseError, ValueError):
                errors.append("malformed-or-unretainable-xunit-evidence")
        if bindings:
            self.emit(dict(componentRuntimeBindings=bindings))
        if errors:
            self.emit(dict(componentEvidenceErrors=errors))
        return errors
