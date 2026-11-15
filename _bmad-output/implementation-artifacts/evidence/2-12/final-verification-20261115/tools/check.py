#!/usr/bin/env python3
"""Run one verification check and append a metadata record.

Usage: check.py NAME CWD [--env K=V ...] [--unset K ...] [--hash PATH ...] [--xml PATH] [--timeout S] -- COMMAND...
"""
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LOGS = ROOT / "logs"
RECORDS = ROOT / "checks.jsonl"


def sha(path):
    p = Path(path)
    if not p.is_file():
        return None
    return hashlib.sha256(p.read_bytes()).hexdigest()


def stamp(path):
    p = Path(path)
    if not p.is_file():
        return None
    return datetime.datetime.fromtimestamp(p.stat().st_mtime, datetime.timezone.utc).isoformat()


def main():
    args = sys.argv[1:]
    name, cwd = args[0], args[1]
    rest = args[2:]
    env_set, env_unset, hashes, xml, timeout = {}, [], [], None, None
    while rest and rest[0] != "--":
        flag = rest.pop(0)
        if flag == "--env":
            key, value = rest.pop(0).split("=", 1)
            env_set[key] = value
        elif flag == "--unset":
            env_unset.append(rest.pop(0))
        elif flag == "--hash":
            hashes.append(rest.pop(0))
        elif flag == "--xml":
            xml = rest.pop(0)
        elif flag == "--timeout":
            timeout = float(rest.pop(0))
        else:
            raise SystemExit("unknown flag " + flag)
    command = rest[1:]
    environment = dict(os.environ)
    environment["PATH"] = str(Path.home() / ".dotnet/tools") + ":" + environment.get("PATH", "")
    for key in env_unset:
        environment.pop(key, None)
    environment.update(env_set)
    before = {h: dict(sha256=sha(h), mtime=stamp(h)) for h in hashes}
    started = datetime.datetime.now(datetime.timezone.utc)
    clock = time.monotonic()
    log = LOGS / (name + ".log")
    with open(log, "wb") as output:
        try:
            completed = subprocess.run(command, cwd=cwd, env=environment, stdout=output, stderr=subprocess.STDOUT, timeout=timeout)
            code = completed.returncode
        except subprocess.TimeoutExpired:
            code = "timeout"
    elapsed = round(time.monotonic() - clock, 2)
    text = log.read_text(errors="replace")
    after = {h: dict(sha256=sha(h), mtime=stamp(h)) for h in hashes}
    summary = re.findall(r"Total: (\d+), Errors: (\d+), Failed: (\d+), Skipped: (\d+), Not Run: (\d+)", re.sub(r"\x1b\[[0-9;]*m", "", text))
    build = re.findall(r"^\s*(\d+) Warning\(s\)\s*\n\s*(\d+) Error\(s\)", text, re.M)
    record = dict(name=name, cwd=cwd, command=command, environmentOverrides=env_set, environmentUnset=env_unset,
        startedAt=started.isoformat(), elapsedSeconds=elapsed, exitCode=code,
        outputSha256=hashlib.sha256(log.read_bytes()).hexdigest(),
        xunitSummaries=[dict(zip(["total", "errors", "failed", "skipped", "notRun"], map(int, s))) for s in summary],
        buildSummary=[dict(warnings=int(w), errors=int(e)) for w, e in build],
        artifactsBefore=before, artifactsAfter=after)
    if xml:
        record["resultXml"] = xml
        record["resultXmlSha256"] = sha(xml)
    with open(RECORDS, "a") as records:
        records.write(json.dumps(record) + "\n")
    print(json.dumps(dict(name=name, exitCode=code, elapsed=elapsed, xunit=record["xunitSummaries"], build=record["buildSummary"],
        after={Path(k).name: v for k, v in after.items()})))
    return 0


if __name__ == "__main__":
    sys.exit(main())
